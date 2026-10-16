// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

@_spi(Compiler) import GameEventScriptRuntime

final class GesParser {
    let source: GesSource
    var tokens: [GesToken] = []
    var index = 0, expressionDepth = 0, statementDepth = 0
    var delimiterDepth = 0
    var forSourceDepth: Int?
    var choiceConditionDepth: Int?
    var module = ""

    init(source: GesSource) {
        self.source = source
        var lexer = GesLexer(source.text)
        repeat { tokens.append(lexer.read()) } while tokens.last!.kind != .endOfFile
    }

    var current: GesToken { tokens[index] }
    var previous: GesToken { tokens[max(0, index - 1)] }

    @discardableResult func advance() -> GesToken {
        let t = current
        if [.leftParen, .leftBracket, .leftBrace].contains(t.kind) { delimiterDepth += 1 } else if [.rightParen, .rightBracket, .rightBrace].contains(t.kind) { delimiterDepth -= 1 }
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

    func match(_ kind: GesTokenKind) -> Bool {
        if current.kind == kind {
            advance()
            return true
        }
        return false
    }

    @discardableResult func expect(_ kind: GesTokenKind) throws -> GesToken {
        if current.kind != kind { throw failure("Expected \(kind) but found \(current.kind).") }
        return advance()
    }

    // Contextual words are identifiers compared by spelling, as C# MatchWord does.
    func matchWord(_ word: String) -> Bool {
        if current.isIdentifier(word) {
            advance()
            return true
        }
        return false
    }

    @discardableResult func expectWord(_ word: String) throws -> GesToken {
        if !current.isIdentifier(word) { throw failure("Expected '\(word)'.") }
        return advance()
    }

    // A selector name that the C# lexer folds into Tag, compared by spelling as C# MatchTag does.
    func matchSelector(_ name: String) -> Bool {
        if current.kind == .selector && current.text == name {
            advance()
            return true
        }
        return false
    }

    func newlines() { while current.kind == .newLine { advance() } }

    func separators() { while current.kind == .newLine || current.kind == .semicolon { advance() } }

    func location(_ start: GesToken, _ end: GesToken? = nil) -> GesSourceLocation {
        let last = end ?? start
        return .init(sourceName: source.name, line: start.line, column: start.column, endLine: last.endLine, endColumn: last.endColumn, moduleName: module, sourceID: source.id)
    }

    func failure(_ message: String, _ token: GesToken? = nil, code: String = "parse.syntax") -> GesCompileError { compileError(.parse, code, message, location(token ?? current)) }

    func identifier() throws -> String {
        guard current.isName else { throw failure("Expected an identifier.") }
        return advance().text
    }

    func typeName() throws -> String {
        guard current.kind == .typeName else { throw failure("Expected a type name.") }
        var name = String(advance().text.dropFirst())
        let builtins = ["Nothing", "Number", "Boolean", "Percentage", "Vector", "Point", "Series", "Tag", "Text", "List", "Range", "Message", "Handler", "Map", "Dice", "Record"]
        if builtins.contains(name) { name = name.lowercased() }
        if name == "Quantity", current.kind == .leftParen, peek().isWord || peek().isIdentifier("degree"), peek(2).kind == .rightParen {
            advance()
            newlines()
            guard current.isWord || current.isIdentifier("degree") else { throw failure("Expected a unit name.") }
            let unit = advance().text
            newlines()
            try expect(.rightParen)
            name = "quantity:" + unit
        }
        return name
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

    func parse() throws -> GesModule {
        separators()
        let start = current
        if match(.module) {
            newlines()
            module = try identifier()
            while match(.dot) { module += "." + (try identifier()) }
            if !module.split(separator: ".").allSatisfy({ $0.unicodeScalars.allSatisfy { GesLexer.lower($0) || GesLexer.digit($0) } }) { throw failure("Invalid module name.", previous) }
        }
        separators()
        var definitions: [GesDefinition] = []
        var constants: [(String, GesExpression)] = []
        var records: [GesRecord] = []
        while current.kind != .endOfFile {
            let begin = current
            if match(.constant) {
                guard current.kind == .constantReference else { throw failure("Expected constant name.") }
                let name = String(advance().text.dropFirst())
                try expect(.be)
                newlines()
                let value = try expression(15)
                switch value.kind {
                case .literal: break
                case .unary(.negate, let operand): guard case .literal(let number) = operand.kind, [.integer, .float, .percentage].contains(number.valueKind) else { throw failure("Expected numeric literal after '-'.", begin) }
                default: throw failure("Expected scalar literal.", begin)
                }
                constants.append((name, value))
            } else if match(.record) {
                let name = try typeName()
                try expect(.`as`)
                newlines()
                try expect(.leftBrace)
                separators()
                var fields: [GesField] = []
                while current.kind != .rightBrace {
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
                    fields.append(.init(name: field, type: type, label: computed == nil ? (unlabeled ? "_" : field) : nil, minimum: low, maximum: high, computed: computed, location: location(fieldStart, previous)))
                    _ = match(.comma)
                    separators()
                }
                try expect(.rightBrace)
                records.append(.init(name: name, fields: fields, location: location(begin, previous)))
            } else if current.kind == .function || current.kind == .predicate {
                let kind = advance().text
                let name = try identifier()
                let params = try parameters()
                try expect(.be)
                newlines()
                let body = try assemblyOrExpression(name, predicate: kind == "predicate")
                definitions.append(.init(kind: kind, name: name, parameters: params, expression: body, statements: [], required: [], excluded: [], location: location(begin, previous)))
            } else {
                try expect(.on)
                newlines()
                guard current.kind == .message || current.kind == .identifier && ["initialization", "undeliverable"].contains(current.text) else { throw failure("Expected message name.") }
                let name = advance().text
                var kind = "handler"
                var params: [GesParameter] = []
                if match(.`as`) {
                    newlines()
                    let p = current
                    let local = try identifier()
                    kind = "messageNameHandler"
                    params = [.init(label: "message", name: local, type: "message", location: location(p))]
                } else if current.kind == .leftParen {
                    params = try parameters()
                }
                var required: [String] = []
                var excluded: [String] = []
                while true {
                    newlines()
                    guard current.kind == .matching || current.kind == .without else { break }
                    let include = advance().kind == .matching
                    repeat {
                        newlines()
                        guard current.kind == .tag else { throw failure("Expected tag literal.") }
                        let tag = String(advance().text.dropFirst())
                        if include { if !required.contains(tag) { required.append(tag) } } else if !excluded.contains(tag) { excluded.append(tag) }
                        newlines()
                    } while match(.comma)
                }
                try expect(.leftBrace)
                let body = try statements()
                try expect(.rightBrace)
                definitions.append(.init(kind: kind, name: name, parameters: params, expression: nil, statements: body, required: required, excluded: excluded, location: location(begin, previous)))
            }
            if current.kind != .endOfFile && current.kind != .newLine && current.kind != .semicolon { throw failure("Expected a declaration separator.") }
            separators()
        }
        return .init(name: module, definitions: definitions, constants: constants, records: records, location: location(start, previous))
    }

    func statements() throws -> [GesStatement] {
        separators()
        var result: [GesStatement] = []
        while current.kind != .rightBrace && current.kind != .endOfFile {
            result.append(try statement())
            if current.kind != .rightBrace && current.kind != .newLine && current.kind != .semicolon { throw failure("Expected a statement separator.") }
            separators()
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

    func statement() throws -> GesStatement {
        let start = current
        let kind: GesStatement.Kind
        if matchWord("asm") {
            kind = .assembly(try assemblyBlock(nil, allowSend: true))
        } else if current.kind == .emit || current.kind == .publish {
            let publish = advance().kind == .publish
            newlines()
            if current.isIdentifier("after") {
                let send = try sendExpression(publish, start)
                return GesStatement(kind: .expression(send), location: send.location)
            }
            let message: GesExpression
            if current.kind == .message {
                let name = advance().text
                let args = current.kind == .leftParen ? try arguments() : []
                message = node(.message(name, args), start)
            } else {
                message = try expression()
            }
            var tags: [GesExpression] = []
            if match(.with) { repeat { tags.append(try expression()) } while match(.comma) }
            kind = .publish(publish, message, tags)
        } else if match(.`let`) {
            let name = try identifier()
            try expect(.be)
            kind = .letBinding(name, try assemblyOrExpression(name, allowSend: true))
        } else if match(.`if`) {
            var conditions: [(String?, GesExpression)] = []
            repeat {
                newlines()
                if current.kind == .leftBrace && !conditions.isEmpty { break }
                var binding: String?
                if match(.`let`) {
                    binding = try identifier()
                    try expect(.be)
                }
                conditions.append((binding, try expression()))
                newlines()
            } while match(.semicolon)
            let then = try body()
            var otherwise: [GesStatement] = []
            if peek(0).kind == .`else` {
                newlines()
                try expect(.`else`)
                otherwise = try body()
            }
            kind = .condition(conditions, then, otherwise)
        } else if match(.`for`) {
            kind = try forClause(start)
        } else if current.kind == .keywordRandom && peek().kind == .with {
            advance()
            newlines()
            try expect(.with)
            let seed = try expression()
            kind = .seeded(seed, try body())
        } else {
            kind = .expression(try expression())
        }
        return .init(kind: kind, location: location(start, previous))
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
        if requireSourceContext && forSourceDepth != delimiterDepth || current.kind != .operatorAnd { return false }
        var offset = 1

        func binding() -> Bool {
            let token = peek(offset)
            offset += 1
            return token.isName
        }

        guard binding() else { return false }
        while peek(offset).kind == .comma {
            offset += 1
            guard binding() else { return false }
        }
        return peek(offset).kind == .in || peek(offset).isIdentifier("from")
    }

    func node(_ kind: GesExpression.Kind, _ start: GesToken) -> GesExpression { .init(kind, location(start, previous)) }

    // A node without a source range, for nodes the C# parser creates without WithRange.
    func unranged(_ kind: GesExpression.Kind, _ start: GesToken) -> GesExpression { .init(kind, GesAstOptimizer.noRange(location(start, previous))) }

    // A node with exactly the range of `node`, as C# WithRange(node, first) for postfix forms such as member access,
    // casts and checks: the operator and its operand do not extend the range.
    func ranged(_ kind: GesExpression.Kind, _ node: GesExpression) throws -> GesExpression {
        let depth = node.depth + 1
        if depth > 32 { throw failure("Expression nesting limit exceeded.", code: "parse.sourceNestingExceeded") }
        return .init(kind, node.location, depth: depth)
    }

    func combined(_ kind: GesExpression.Kind, _ first: GesExpression, _ last: GesExpression? = nil) throws -> GesExpression {
        let a = first.location
        let b = last?.location ?? location(previous)
        let depth = max(first.depth, last?.depth ?? 0) + 1
        if depth > 32 { throw failure("Expression nesting limit exceeded.", code: "parse.sourceNestingExceeded") }
        return .init(kind, .init(sourceName: a.sourceName, line: a.line, column: a.column, endLine: b.endLine, endColumn: b.endColumn, moduleName: module, sourceID: source.id), depth: depth)
    }

    static let precedence: [GesTokenKind: Int] = [
        .operatorImplication: 1, .default: 2, .operatorCollectionUnion: 3, .operatorCollectionIntersect: 4, .zip: 5, .operatorOr: 6, .operatorXor: 7, .operatorAnd: 8,
        .operatorEqual: 9, .operatorNotEqual: 9, .in: 10, .notIn: 10, .starts: 10, .ends: 10, .has: 10, .as: 11, .is: 11,
        .operatorLess: 12, .operatorGreater: 12, .operatorLessOrEqual: 12, .operatorGreaterOrEqual: 12, .operatorPlus: 13, .operatorMinus: 13,
        .operatorMultiply: 14, .operatorDivide: 14, .operatorIntegerDivide: 14, .operatorModulo: 14, .operatorRemainder: 14, .operatorPower: 16,
    ]

    func expression(_ minimum: Int = 0) throws -> GesExpression {
        if expressionDepth >= 32 { throw failure("Expression nesting limit exceeded.", code: "parse.sourceNestingExceeded") }
        expressionDepth += 1
        defer { expressionDepth -= 1 }
        newlines()
        if minimum == 0 && match(.when) { return try conditionFirstChoice(previous) }
        var left = try prefix()
        while true {
            if isForClauseStart() { break }
            if minimum <= 16 && current.kind == .superscriptInteger {
                let exponent = node(.literal(.integer(Int64(advance().text)!)), previous)
                left = try combined(.binary(.power, left, exponent), left, exponent)
                continue
            }
            if minimum <= 14, case .literal(let value) = left.kind, value.isNumeric && !value.hasUnit && previous.end == current.start,
                current.isName || minimum <= 14 && previous.end == current.start && [.mathConstantPi, .mathConstantE, .mathConstantTau, .mathConstantInfinity].contains(current.kind)
            {
                let right = try expression(15)
                left = try combined(.binary(.multiply, left, right), left, right)
                continue
            }
            guard let precedence = Self.precedence[current.kind], precedence >= minimum else { break }
            let op = advance().kind
            newlines()
            if op == .as {
                left = try ranged(.cast(left, typeName()), left)
                continue
            }
            if op == .is {
                left = try isExpression(left)
                continue
            }
            if op == .has {
                try expectWord("value")
                left = try ranged(.unary(.hasValue, left), left)
                continue
            }
            var binary = GesBinaryOperator(op)
            if op == .starts || op == .ends {
                try expect(.with)
                binary = op == .starts ? .startsWith : .endsWith
                newlines()
            }
            if op == .in && current.isIdentifier("values") && peek().kind == .of {
                advance()
                newlines()
                try expect(.of)
                binary = .containsValue
            }
            let right = try expression(op == .operatorPower ? 15 : op == .operatorImplication ? precedence : precedence + 1)
            if op == .notIn {
                let membership = try combined(.binary(.contains, left, right), left, right)
                left = try ranged(.unary(.not, membership), membership)
                continue
            }
            guard let binary else { throw failure("Expected an operator.", previous) }
            left = try combined(.binary(binary, left, right), left, right)
        }
        if minimum == 0 && match(.when) {
            var branches = [(left, try expression(1))]
            while match(.comma) {
                newlines()
                if current.kind == .otherwise { break }
                _ = match(.operatorOr)
                newlines()
                let value = try expression(1)
                try expect(.when)
                branches.append((value, try expression(1)))
            }
            newlines()
            try expect(.otherwise)
            let fallback = try expression(1)
            let depth = try guardedChoiceDepth(branches, fallback: fallback)
            left = try combined(.choice(branches, fallback), left, fallback)
            left.depth = depth
        }
        return left
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
            if current.kind == .otherwise { break }
            _ = match(.operatorOr)
            newlines()
            try expect(.when)
        }
        newlines()
        try expect(.otherwise)
        let fallback = try expression(1)
        return .init(.choice(branches, fallback), location(start, previous), depth: try guardedChoiceDepth(branches, fallback: fallback))
    }

    func guardedChoiceDepth(_ branches: [(GesExpression, GesExpression)], fallback: GesExpression) throws -> Int {
        let depth = branches.reduce(fallback.depth) { max($0, max($1.0.depth, $1.1.depth)) } + 1
        if depth > 32 { throw failure("Expression nesting limit exceeded.", code: "parse.sourceNestingExceeded") }
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
        var value: GesExpression
        if current.kind == .typeName {
            value = try ranged(.check(input, typeName()), input)
        } else if current.kind == .numeric || current.kind == .nothing || current.isIdentifier("integer") || current.isIdentifier("fractional") {
            let type = advance().text
            value = try ranged(.check(input, type), input)
        } else if match(.empty) {
            value = try ranged(.unary(.empty, input), input)
        } else if current.kind.isSelector && peek().kind == .dot {
            let (ns, function) = try extensionSymbol()
            value = try ranged(.unary(.predicate, .init(.extensionCall(ns, function, [.init(label: "_", value: input)]), input.location)), input)
        } else if matchWord("between") {
            newlines()
            let minimum = try expression(13)
            newlines()
            try expect(.operatorAnd)
            newlines()
            let maximum = try expression(13)
            value = try combined(.between(input, minimum, maximum), input, maximum)
            value.depth = max(value.depth, minimum.depth + 1)
            if value.depth > 32 { throw failure("Expression nesting limit exceeded.", code: "parse.sourceNestingExceeded") }
        } else if matchWord("at") {
            let mode = advance()
            guard mode.isIdentifier("least") || mode.isIdentifier("most") else { throw failure("Expected least or most.", previous) }
            let right = try expression(13)
            value = try combined(.binary(mode.text == "least" ? .greaterOrEqual : .lessOrEqual, input, right), input, right)
        } else if current.isIdentifier("less") || current.isIdentifier("more") {
            let mode = advance().text
            try expectWord("than")
            let right = try expression(13)
            value = try combined(.binary(mode == "less" ? .less : .greater, input, right), input, right)
        } else if current.isName {
            value = try ranged(.predicate(input, advance().text), input)
        } else {
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
            value = try combined(.binary(op, input, right), input, right)
        }
        if negated { value = try ranged(.unary(.not, value), value) }
        return value
    }
}
