// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

@_spi(Compiler) import GameEventScriptRuntime
@_spi(Syntax) import GameEventScriptSyntax

final class GesParser {
    let source: GesSource
    var tokens: [GesToken] = []
    var index = 0, expressionDepth = 0, statementDepth = 0
    var delimiterDepth = 0
    var forSourceDepth: Int?
    var choiceConditionDepth: Int?
    var module = ""
    // Syntax errors collected while parsing recovers, as the C# parser collects them.
    var diagnostics: [GesDiagnostic] = []

    init(source: GesSource) {
        self.source = source
        var lexer = GesLexer(source.text)
        repeat { tokens.append(lexer.read()) } while tokens.last!.kind != .endOfFile
    }

    var current: GesToken { tokens[index] }
    // Only the kind, so that a recursive parse function does not hold a copy of the whole token for each test.
    var currentKind: GesTokenKind { tokens[index].kind }
    var previous: GesToken { tokens[max(0, index - 1)] }

    @discardableResult func advance() -> GesToken {
        let t = current
        switch t.kind {
        case .leftParen, .leftBracket, .leftBrace: delimiterDepth += 1
        case .rightParen, .rightBracket, .rightBrace: delimiterDepth -= 1
        default: break
        }
        if t.kind != .endOfFile { index += 1 }
        return t
    }

    func peek(_ distance: Int = 1) -> GesToken {
        var i = index
        var count = 0
        while i < tokens.count - 1 {
            if tokens[i].kind != .newLine {
                if count == distance { return tokens[i] }
                count += 1
            }
            i += 1
        }
        return tokens[i]
    }

    func peekKind(_ distance: Int = 1) -> GesTokenKind { peek(distance).kind }

    func match(_ kind: GesTokenKind) -> Bool {
        if currentKind == kind {
            advance()
            return true
        }
        return false
    }

    // Returns nothing, so that callers do not reserve space for a token they discard.
    func expect(_ kind: GesTokenKind) throws {
        if currentKind != kind { throw failure("Expected \(kind) but found \(currentKind).") }
        advance()
    }

    // Contextual words are identifiers compared by spelling, as C# MatchWord does.
    func matchWord(_ word: String) -> Bool {
        if current.isIdentifier(word) {
            advance()
            return true
        }
        return false
    }

    func expectWord(_ word: String) throws {
        if !current.isIdentifier(word) { throw failure("Expected '\(word)'.") }
        advance()
    }

    // A selector name that the C# lexer folds into Tag, compared by spelling as C# MatchTag does.
    func matchSelector(_ name: String) -> Bool {
        if currentKind == .selector && current.text == name {
            advance()
            return true
        }
        return false
    }

    func newlines() { while currentKind == .newLine { advance() } }

    func separators() { while currentKind == .newLine || currentKind == .semicolon { advance() } }

    func location(_ start: GesToken, _ end: GesToken? = nil) -> GesSourceLocation {
        let last = end ?? start
        return .init(sourceName: source.name, line: start.line, column: start.column, endLine: last.endLine, endColumn: last.endColumn, moduleName: module, sourceID: source.id)
    }

    func failure(_ message: String, _ token: GesToken? = nil, code: String = "parse.syntax") -> GesCompileError { compileError(.parse, code, message, location(token ?? current)) }

    func identifier() throws -> String {
        guard current.isName else { throw failure("Expected an identifier.") }
        return advance().text
    }

    static let builtinTypeNames: Set<String> = ["Nothing", "Number", "Boolean", "Percentage", "Vector", "Point", "Series", "Tag", "Text", "List", "Range", "Message", "Handler", "Map", "Dice", "Record"]

    static let quantityUnitKinds: Set<GesTokenKind> = [.identifier, .message, .operatorDivide, .operatorPower, .superscriptInteger, .float]

    func typeName() throws -> String {
        guard currentKind == .typeName else { throw failure("Expected a type name.") }
        var name = String(advance().text.dropFirst())
        if Self.builtinTypeNames.contains(name) { name = name.lowercased() }
        // Uppercase unit symbols such as N or Hz read as message-name tokens.
        if name == "Quantity", currentKind == .leftParen, peek().isWord || peek().isIdentifier("degree") || peekKind() == .message, quantityUnitAhead() {
            advance()
            newlines()
            var unit = ""
            while currentKind != .rightParen {
                unit += quantityUnitPart(advance())
                newlines()
            }
            try expect(.rightParen)
            name = "quantity:" + unit
        }
        return name
    }

    // A quantity unit is a name optionally followed by `/` and further unit parts, such as `m/s`, `m/s^2` or `°/s`.
    func quantityUnitAhead() -> Bool {
        for offset in 2..<8 {
            let kind = peekKind(offset)
            if kind == .rightParen { return true }
            if !Self.quantityUnitKinds.contains(kind) { return false }
        }
        return false
    }

    func quantityUnitPart(_ token: GesToken) -> String {
        token.kind == .superscriptInteger ? token.text == "2" ? "²" : "³" : token.text
    }

    func parameters(_ allowType: Bool = true) throws -> [GesParameter] {
        try expect(.leftParen)
        newlines()
        var result: [GesParameter] = []
        if match(.rightParen) { return result }
        repeat {
            newlines()
            let start = current
            let unlabeled = match(.underscore)
            newlines()
            let name = try identifier()
            newlines()
            var type: String?
            if allowType && match(.`as`) {
                newlines()
                type = try typeName()
                newlines()
            }
            result.append(.init(label: unlabeled ? "_" : name, name: name, type: type, location: location(start, previous)))
        } while match(.comma)
        try expect(.rightParen)
        return result
    }

    // Parses one source and reports every syntax error it can find: after an error, parsing resumes at the next
    // declaration keyword, or inside a block at the next statement separator, as the C# parser does. A nesting-limit
    // error ends parsing immediately.
    func parse() throws -> GesModule {
        // A failure that ends parsing, such as the nesting limit, follows the syntax errors already collected.
        do { return try parseModule() } catch let error as GesCompileError where !diagnostics.isEmpty && error.diagnostics.contains(where: { $0.code != "parse.syntax" }) {
            throw GesCompileError(diagnostics: diagnostics + error.diagnostics)
        }
    }

    func parseModule() throws -> GesModule {
        separators()
        let start = current
        do {
            if match(.module) {
                newlines()
                module = try identifier()
                while match(.dot) { module += "." + (try identifier()) }
                if !module.split(separator: ".").allSatisfy({ $0.unicodeScalars.allSatisfy { GesLexer.lower($0) || GesLexer.digit($0) } }) { throw failure("Invalid module name.", previous) }
            }
        } catch let error as GesCompileError {
            try record(error)
            synchronizeTopLevel()
        }
        separators()
        var definitions: [GesDefinition] = []
        var constants: [(name: String, value: GesExpression, location: GesSourceLocation)] = []
        var records: [GesRecord] = []
        while currentKind != .endOfFile {
            let declarationStart = index
            do { try declaration(&definitions, &constants, &records) } catch let error as GesCompileError {
                try record(error)
                // A declaration that fails on its first token must still move on, or recovery would stop there again.
                if index == declarationStart { advance() }
                synchronizeTopLevel()
            }
        }
        if !diagnostics.isEmpty { throw GesCompileError(diagnostics: diagnostics) }
        return .init(name: module, definitions: definitions, constants: constants, records: records, location: location(start, previous))
    }

    // Keeps a syntax error and continues; any other failure, such as the nesting limit, ends parsing.
    func record(_ error: GesCompileError) throws {
        guard error.diagnostics.allSatisfy({ $0.code == "parse.syntax" }) else { throw error }
        diagnostics += error.diagnostics
    }

    static let declarationKinds: Set<GesTokenKind> = [.module, .constant, .record, .predicate, .function, .on]

    func synchronizeTopLevel() {
        while currentKind != .endOfFile {
            if Self.declarationKinds.contains(currentKind) { return }
            advance()
        }
    }

    // Skips to the next statement; false when the block or the source ends first.
    func synchronizeStatement() -> Bool {
        while currentKind != .endOfFile {
            if currentKind == .rightBrace { return false }
            if match(.semicolon) || match(.newLine) {
                separators()
                return true
            }
            advance()
        }
        return false
    }

    func declaration(_ definitions: inout [GesDefinition], _ constants: inout [(name: String, value: GesExpression, location: GesSourceLocation)], _ records: inout [GesRecord]) throws {
        let begin = current
        if currentKind == .illegal {
            let token = advance()
            throw failure("Illegal token '\(token.text)'.", token)
        }
        if match(.module) { throw failure("A module declaration must precede all other declarations and appear once.", previous) }
        if match(.constant) {
            constants.append(try constantDeclaration(begin))
        } else if match(.record) {
            records.append(try recordDeclaration(begin))
        } else if currentKind == .function || currentKind == .predicate {
            definitions.append(try routineDeclaration(begin))
        } else {
            definitions.append(try handlerDeclaration(begin))
        }
        if currentKind != .endOfFile && currentKind != .newLine && currentKind != .semicolon { throw failure("Expected a declaration separator.") }
        separators()
    }

    func constantDeclaration(_ begin: GesToken) throws -> (name: String, value: GesExpression, location: GesSourceLocation) {
        guard currentKind == .constantReference else { throw failure("Expected constant name.") }
        let nameToken = advance()
        let name = String(nameToken.text.dropFirst())
        try expect(.be)
        newlines()
        let value = try expression(15)
        switch value.kind {
        case .literal: break
        case .unary(.negate, let operand): guard case .literal(let number) = operand.kind, [.integer, .float, .percentage].contains(number.valueKind) else { throw failure("Expected numeric literal after '-'.", nameToken) }
        default: throw failure("Expected scalar literal.", nameToken)
        }
        return (name, value, location(begin, previous))
    }

    func recordDeclaration(_ begin: GesToken) throws -> GesRecord {
        let name = try typeName()
        try expect(.`as`)
        newlines()
        try expect(.leftBrace)
        separators()
        var fields: [GesField] = []
        while currentKind != .rightBrace {
            fields.append(try recordField())
            _ = match(.comma)
            separators()
        }
        try expect(.rightBrace)
        return .init(name: name, fields: fields, location: location(begin, previous))
    }

    func recordField() throws -> GesField {
        let fieldStart = current
        let unlabeled = match(.underscore)
        let field = try identifier()
        try expect(.colon)
        newlines()
        let type = try typeName()
        newlines()
        var low: GesExpression?
        var high: GesExpression?
        var computed: GesExpression?
        if matchWord("clamped") {
            try expectWord("between")
            low = try expression(9)
            try expect(.operatorAnd)
            high = try expression(9)
            newlines()
        }
        if matchWord("computed") {
            try expectWord("by")
            computed = try assemblyOrExpression(field)
        }
        if unlabeled && computed != nil { throw failure("Computed field cannot be a parameter.", fieldStart) }
        return .init(name: field, type: type, label: computed == nil ? (unlabeled ? "_" : field) : nil, minimum: low, maximum: high, computed: computed, location: location(fieldStart, previous))
    }

    func routineDeclaration(_ begin: GesToken) throws -> GesDefinition {
        let kind = advance().text
        let name = try identifier()
        let params = try parameters()
        try expect(.be)
        newlines()
        let body = try assemblyOrExpression(name, predicate: kind == "predicate")
        return .init(kind: kind, name: name, parameters: params, expression: body, statements: [], required: [], excluded: [], location: location(begin, previous))
    }

    func handlerDeclaration(_ begin: GesToken) throws -> GesDefinition {
        try expect(.on)
        newlines()
        guard currentKind == .message || currentKind == .identifier && ["initialization", "undeliverable"].contains(current.text) else { throw failure("Expected message name.") }
        let name = advance().text
        var kind = "handler"
        var params: [GesParameter] = []
        if match(.`as`) {
            newlines()
            let p = current
            let local = try identifier()
            kind = "messageNameHandler"
            params = [.init(label: "message", name: local, type: "message", location: location(p))]
        } else if currentKind == .leftParen {
            params = try parameters()
        }
        var required: [String] = []
        var excluded: [String] = []
        while true {
            newlines()
            guard currentKind == .matching || currentKind == .without else { break }
            let include = advance().kind == .matching
            repeat {
                newlines()
                guard currentKind == .tag else { throw failure("Expected tag literal.") }
                let tag = String(advance().text.dropFirst())
                if include { if !required.contains(tag) { required.append(tag) } } else if !excluded.contains(tag) { excluded.append(tag) }
                newlines()
            } while match(.comma)
        }
        try expect(.leftBrace)
        let body = try statements()
        try expect(.rightBrace)
        return .init(kind: kind, name: name, parameters: params, expression: nil, statements: body, required: required, excluded: excluded, location: location(begin, previous))
    }

    // A block's statements up to its closing brace; an error resumes at the next statement of the same block.
    func statements() throws -> [GesStatement] {
        separators()
        var result: [GesStatement] = []
        while currentKind != .rightBrace {
            if currentKind == .endOfFile {
                try record(failure("Expected '}' before end of input."))
                return result
            }
            do {
                result.append(try statement())
                if currentKind != .rightBrace && currentKind != .newLine && currentKind != .semicolon { throw failure("Expected a statement separator.") }
                separators()
            } catch let error as GesCompileError {
                try record(error)
                if !synchronizeStatement() { return result }
            }
        }
        return result
    }

    func body() throws -> [GesStatement] {
        if statementDepth >= 32 { throw failure("Statement nesting limit exceeded.", code: "parse.sourceNestingExceeded") }
        statementDepth += 1
        defer { statementDepth -= 1 }
        newlines()
        if match(.leftBrace) {
            let result = try statements()
            try expect(.rightBrace)
            return result
        }
        return [try statement()]
    }

    // Nested blocks recurse through `statement`, `body` and `statements`, so each statement form parses in its own
    // function, as nested expressions do.
    func statement() throws -> GesStatement {
        let start = current
        let kind: GesStatement.Kind
        if matchWord("asm") {
            kind = .assembly(try assemblyBlock(nil, allowSend: true))
        } else if currentKind == .emit || currentKind == .publish {
            if peek().isIdentifier("after") { return try sendStatement(start) }
            kind = try publishStatement()
        } else if match(.`let`) {
            kind = try letStatement()
        } else if match(.`if`) {
            kind = try conditionStatement()
        } else if match(.`for`) {
            kind = try forClause(start)
        } else if currentKind == .keywordRandom && peekKind() == .with {
            kind = try seededStatement()
        } else {
            kind = .expression(try expression())
        }
        return .init(kind: kind, location: location(start, previous))
    }

    // A send with a delay is a result-bearing send expression in statement position.
    func sendStatement(_ start: GesToken) throws -> GesStatement {
        let publish = advance().kind == .publish
        let send = try sendExpression(publish, start)
        return GesStatement(kind: .expression(send), location: send.location)
    }

    func publishStatement() throws -> GesStatement.Kind {
        let start = current
        let publish = advance().kind == .publish
        newlines()
        let message: GesExpression
        if currentKind == .message {
            let name = advance().text
            let args = currentKind == .leftParen ? try arguments() : []
            message = node(.message(name, args), start)
        } else {
            message = try expression()
        }
        var tags: [GesExpression] = []
        if match(.with) { repeat { tags.append(try expression()) } while match(.comma) }
        return .publish(publish, message, tags)
    }

    func letStatement() throws -> GesStatement.Kind {
        let name = try identifier()
        try expect(.be)
        return .letBinding(name, try assemblyOrExpression(name, allowSend: true))
    }

    func conditionStatement() throws -> GesStatement.Kind {
        var conditions: [(binding: String?, value: GesExpression, location: GesSourceLocation)] = []
        repeat {
            newlines()
            if currentKind == .leftBrace && !conditions.isEmpty { break }
            conditions.append(try condition())
            newlines()
        } while match(.semicolon)
        let then = try body()
        var otherwise: [GesStatement] = []
        if peekKind(0) == .`else` {
            newlines()
            try expect(.`else`)
            otherwise = try body()
        }
        return .condition(conditions, then, otherwise)
    }

    // One `if` condition, optionally binding its value.
    func condition() throws -> (binding: String?, value: GesExpression, location: GesSourceLocation) {
        let conditionStart = current
        var binding: String?
        if match(.`let`) {
            binding = try identifier()
            try expect(.be)
        }
        let value = try expression()
        return (binding, value, location(conditionStart, previous))
    }

    func seededStatement() throws -> GesStatement.Kind {
        advance()
        newlines()
        try expect(.with)
        let seed = try expression()
        return .seeded(seed, try body())
    }

    func forClause(_ start: GesToken) throws -> GesStatement.Kind {
        let names = try bindingNames()
        newlines()
        let range = matchWord("from")
        let sequence = try forSource(start, range: range)
        newlines()
        if isForClauseStart(requireSourceContext: false) {
            if statementDepth >= 32 { throw failure("Statement nesting limit exceeded.", code: "parse.sourceNestingExceeded") }
            let next = advance()
            newlines()
            statementDepth += 1
            defer { statementDepth -= 1 }
            let inner = try forClause(next)
            return .loop(names, sequence, range, [.init(kind: inner, location: location(next, previous))])
        }
        return .loop(names, sequence, range, try body())
    }

    func forSource(_ start: GesToken, range: Bool) throws -> GesExpression {
        let saved = forSourceDepth
        forSourceDepth = delimiterDepth
        defer { forSourceDepth = saved }
        if range { return try rangeExpression(start) }
        try expect(.`in`)
        newlines()
        if current.isIdentifier("from") { throw failure("Direct range requires 'from'.") }
        return try expression()
    }

    func isForClauseStart(requireSourceContext: Bool = true) -> Bool {
        if requireSourceContext && forSourceDepth != delimiterDepth || currentKind != .operatorAnd { return false }
        var offset = 1

        func binding() -> Bool {
            let token = peek(offset)
            offset += 1
            return token.isName
        }

        guard binding() else { return false }
        while peekKind(offset) == .comma {
            offset += 1
            guard binding() else { return false }
        }
        return peekKind(offset) == .in || peek(offset).isIdentifier("from")
    }

    func node(_ kind: GesExpression.Kind, _ start: GesToken) -> GesExpression { .init(kind, location(start, previous)) }

    // A node without a source range, for nodes the C# parser creates without WithRange.
    func unranged(_ kind: GesExpression.Kind, _ start: GesToken) -> GesExpression { .init(kind, GesAstOptimizer.noRange(location(start, previous))) }

    // A node with exactly the range of `node`, as C# WithRange(node, first) for postfix forms such as member access,
    // casts and checks: the operator and its operand do not extend the range.
    func ranged(_ kind: GesExpression.Kind, _ node: GesExpression) throws -> GesExpression {
        let depth = node.depth + 1
        if depth > 32 { throw nestingLimit(node.location) }
        return .init(kind, node.location, depth: depth)
    }

    // A node that exceeds the expression depth reports at its own range, as C# WithRange does.
    func nestingLimit(_ location: GesSourceLocation) -> GesCompileError { compileError(.parse, "parse.sourceNestingExceeded", "Expression nesting limit exceeded.", location) }

    func combined(_ kind: GesExpression.Kind, _ first: GesExpression, _ last: GesExpression? = nil) throws -> GesExpression {
        let a = first.location
        let b = last?.location ?? location(previous)
        let depth = max(first.depth, last?.depth ?? 0) + 1
        let range = GesSourceLocation(sourceName: a.sourceName, line: a.line, column: a.column, endLine: b.endLine, endColumn: b.endColumn, moduleName: module, sourceID: source.id)
        if depth > 32 { throw nestingLimit(range) }
        return .init(kind, range, depth: depth)
    }

    static let precedence: [GesTokenKind: Int] = [
        .operatorImplication: 1, .default: 2, .operatorCollectionUnion: 3, .operatorCollectionIntersect: 4, .zip: 5, .operatorOr: 6, .operatorXor: 7, .operatorAnd: 8,
        .operatorEqual: 9, .operatorNotEqual: 9, .in: 10, .notIn: 10, .starts: 10, .ends: 10, .has: 10, .as: 11, .is: 11,
        .operatorLess: 12, .operatorGreater: 12, .operatorLessOrEqual: 12, .operatorGreaterOrEqual: 12, .operatorPlus: 13, .operatorMinus: 13,
        .operatorMultiply: 14, .operatorDivide: 14, .operatorIntegerDivide: 14, .operatorModulo: 14, .operatorRemainder: 14, .operatorPower: 16,
    ]

    // The next step of the operator loop after a left operand.
    enum InfixStep {
        // A postfix form, applied completely.
        case complete(GesExpression)
        // An `is` test, which isExpression completes.
        case test
        // A binary operator whose right operand follows with the given minimum precedence.
        case operand(GesTokenKind, GesBinaryOperator?, Int)
    }

    // Right operands recurse from this small frame; infixStep and infix read and combine the operators in their own.
    func expression(_ minimum: Int = 0) throws -> GesExpression {
        if expressionDepth >= 32 { throw failure("Expression nesting limit exceeded.", code: "parse.sourceNestingExceeded") }
        expressionDepth += 1
        defer { expressionDepth -= 1 }
        newlines()
        if minimum == 0 && match(.when) { return try conditionFirstChoice(previous) }
        var left = try prefix()
        while let step = try infixStep(left, minimum) {
            switch step {
            case .complete(let value): left = value
            case .test: left = try isExpression(left)
            case .operand(let op, let binary, let rightMinimum): left = try infix(op, binary, left, expression(rightMinimum))
            }
        }
        if minimum == 0 && match(.when) { left = try trailingChoice(left) }
        return left
    }

    // Consumes the operator after `left`; nil ends the operator loop.
    func infixStep(_ left: GesExpression, _ minimum: Int) throws -> InfixStep? {
        if isForClauseStart() { return nil }
        if minimum <= 16 && currentKind == .superscriptInteger {
            let exponent = node(.literal(.integer(Int64(advance().text)!)), previous)
            return .complete(try combined(.binary(.power, left, exponent), left, exponent))
        }
        if minimum <= 14, case .literal(let value) = left.kind, value.isNumeric && !value.hasUnit && previous.end == current.start,
            current.isName || minimum <= 14 && previous.end == current.start && Self.mathConstants[currentKind] != nil
        {
            return .operand(.operatorMultiply, .multiply, 15)
        }
        guard let precedence = Self.precedence[currentKind], precedence >= minimum else { return nil }
        let op = advance().kind
        newlines()
        if op == .as { return .complete(try ranged(.cast(left, typeName()), left)) }
        if op == .is { return .test }
        if op == .has {
            try expectWord("value")
            return .complete(try ranged(.unary(.hasValue, left), left))
        }
        var binary = GesBinaryOperator(op)
        if op == .starts || op == .ends {
            try expect(.with)
            binary = op == .starts ? .startsWith : .endsWith
            newlines()
        }
        if op == .in && current.isIdentifier("values") && peekKind() == .of {
            advance()
            newlines()
            try expect(.of)
            binary = .containsValue
        }
        return .operand(op, binary, op == .operatorPower ? 15 : op == .operatorImplication ? precedence : precedence + 1)
    }

    func infix(_ op: GesTokenKind, _ binary: GesBinaryOperator?, _ left: GesExpression, _ right: GesExpression) throws -> GesExpression {
        if op == .notIn {
            let membership = try combined(.binary(.contains, left, right), left, right)
            return try ranged(.unary(.not, membership), membership)
        }
        guard let binary else { throw failure("Expected an operator.", previous) }
        return try combined(.binary(binary, left, right), left, right)
    }

    // `value when condition, … otherwise fallback` after its first value and `when`.
    func trailingChoice(_ first: GesExpression) throws -> GesExpression {
        var branches = [(first, try expression(1))]
        while match(.comma) {
            newlines()
            if currentKind == .otherwise { break }
            _ = match(.operatorOr)
            newlines()
            let value = try expression(1)
            try expect(.when)
            branches.append((value, try expression(1)))
        }
        newlines()
        try expect(.otherwise)
        let fallback = try expression(1)
        let result = try combined(.choice(branches, fallback), first, fallback)
        result.depth = try guardedChoiceDepth(branches, fallback: fallback, result.location)
        return result
    }

    func conditionFirstChoice(_ start: GesToken) throws -> GesExpression {
        var branches: [(GesExpression, GesExpression)] = []
        while true {
            let condition = try choiceCondition()
            newlines()
            try expectWord("then")
            let value = try expression(1)
            branches.append((value, condition))
            if !match(.comma) { break }
            newlines()
            if currentKind == .otherwise { break }
            _ = match(.operatorOr)
            newlines()
            try expect(.when)
        }
        newlines()
        try expect(.otherwise)
        let fallback = try expression(1)
        let range = location(start, previous)
        return .init(.choice(branches, fallback), range, depth: try guardedChoiceDepth(branches, fallback: fallback, range))
    }

    func guardedChoiceDepth(_ branches: [(GesExpression, GesExpression)], fallback: GesExpression, _ range: GesSourceLocation) throws -> Int {
        let depth = branches.reduce(fallback.depth) { max($0, max($1.0.depth, $1.1.depth)) } + 1
        if depth > 32 { throw nestingLimit(range) }
        return depth
    }

    func choiceCondition() throws -> GesExpression {
        let saved = choiceConditionDepth
        choiceConditionDepth = delimiterDepth
        defer { choiceConditionDepth = saved }
        return try expression(1)
    }

    func isExpression(_ input: GesExpression) throws -> GesExpression {
        let negated = match(.operatorNot)
        newlines()
        let value = try isTest(input)
        return negated ? try ranged(.unary(.not, value), value) : value
    }

    // The test after `is` and an optional `not`; tests with a nested operand parse it in their own function.
    func isTest(_ input: GesExpression) throws -> GesExpression {
        if currentKind == .typeName { return try ranged(.check(input, typeName()), input) }
        if currentKind == .numeric || currentKind == .nothing || current.isIdentifier("integer") || current.isIdentifier("fractional") { return try ranged(.check(input, advance().text), input) }
        if match(.empty) { return try ranged(.unary(.empty, input), input) }
        if isExtensionCallStart() { return try extensionPredicate(input) }
        if matchWord("between") { return try betweenTest(input) }
        if matchWord("at") { return try boundTest(input) }
        if current.isIdentifier("less") || current.isIdentifier("more") { return try strictComparison(input) }
        if current.isName { return try ranged(.predicate(input, advance().text), input) }
        return try equalityTest(input)
    }

    func extensionPredicate(_ input: GesExpression) throws -> GesExpression {
        let (ns, function) = try extensionSymbol()
        return try ranged(.unary(.predicate, .init(.extensionCall(ns, function, [.init(label: "_", value: input)]), input.location)), input)
    }

    func betweenTest(_ input: GesExpression) throws -> GesExpression {
        newlines()
        let minimum = try expression(13)
        newlines()
        try expect(.operatorAnd)
        newlines()
        let maximum = try expression(13)
        let value = try combined(.between(input, minimum, maximum), input, maximum)
        value.depth = max(value.depth, minimum.depth + 1)
        if value.depth > 32 { throw nestingLimit(value.location) }
        return value
    }

    // `at least` or `at most`.
    func boundTest(_ input: GesExpression) throws -> GesExpression {
        let mode = advance()
        guard mode.isIdentifier("least") || mode.isIdentifier("most") else { throw failure("Expected least or most.", previous) }
        let op: GesBinaryOperator = mode.text == "least" ? .greaterOrEqual : .lessOrEqual
        let right = try expression(13)
        return try combined(.binary(op, input, right), input, right)
    }

    // `less than` or `more than`.
    func strictComparison(_ input: GesExpression) throws -> GesExpression {
        let op: GesBinaryOperator = advance().text == "less" ? .less : .greater
        try expectWord("than")
        let right = try expression(13)
        return try combined(.binary(op, input, right), input, right)
    }

    // `is value`, optionally followed by `or less` or `or more`.
    func equalityTest(_ input: GesExpression) throws -> GesExpression {
        let right = try expression(13)
        newlines()
        var op = GesBinaryOperator.equal
        if match(.operatorOr) {
            newlines()
            if matchWord("less") {
                op = .lessOrEqual
            } else {
                try expectWord("more")
                op = .greaterOrEqual
            }
        }
        return try combined(.binary(op, input, right), input, right)
    }
}
