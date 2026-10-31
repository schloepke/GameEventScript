// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

/// A syntax error of one source, with the module name declared before it. Lines and columns are one-based; a range
/// built from an expression without a range of its own has no start or end.
@_spi(Syntax) public struct GesSyntaxDiagnostic: Sendable {
    /// `parse.syntax` or `parse.sourceNestingExceeded`.
    @_spi(Syntax) public let code: String
    /// A message for people; its wording is not stable.
    @_spi(Syntax) public let message: String
    /// The first line of the range, if it has one.
    @_spi(Syntax) public let line: Int?
    /// The first column of the range, if it has one.
    @_spi(Syntax) public let column: Int?
    /// The last line of the range, if it has one.
    @_spi(Syntax) public let endLine: Int?
    /// The column after the range, if it has one.
    @_spi(Syntax) public let endColumn: Int?
    /// The module name declared before the error, or empty.
    @_spi(Syntax) public let moduleName: String

    static let syntax = "parse.syntax"
    static let sourceNestingExceeded = "parse.sourceNestingExceeded"
}

/// Parses one source into its lossless syntax tree and reports its syntax errors. After an error, parsing resumes at the
/// next declaration or statement, and the skipped tokens form an error node.
// The parser keeps of each expression only what the syntax rules test: its source nesting depth, its range for a
// nesting diagnostic, and a few literal shapes.
@_spi(Syntax) public final class GesSyntaxParser {
    // A failure that ends the current declaration or statement, or, for the nesting limit, the whole parse.
    struct Failure: Error {
        let diagnostic: GesSyntaxDiagnostic
    }

    // Literal shapes that syntax rules test: unitless literals before implicit multiplication, literals in constants.
    enum Shape {
        case none
        case number
        case boolean
        case quantity
        case scalar
        case negated
        case negatedOther
    }

    // The nesting of a map literal and of the values of its entries, for the depth limit of object patterns.
    final class Pattern {
        let depth: Int
        let start: Int
        let end: Int
        let entries: [Pattern]?

        init(depth: Int, start: Int, end: Int, entries: [Pattern]?) {
            self.depth = depth
            self.start = start
            self.end = end
            self.entries = entries
        }
    }

    // An expression as the parser keeps it: its depth, the tokens its range spans (-1 for a missing start or end), its
    // literal shape and, for a map literal, its pattern nesting.
    struct Expression {
        var depth: Int
        var start: Int
        var end: Int
        var shape = Shape.none
        var pattern: Pattern?
    }

    let text: String
    var tokens: [GesToken] = []
    var index = 0, expressionDepth = 0, statementDepth = 0
    var delimiterDepth = 0
    var forSourceDepth: Int?
    var choiceConditionDepth: Int?
    var module = ""
    var diagnostics: [GesSyntaxDiagnostic] = []
    var tree = GesSyntaxTreeBuilder()
    // Where the open declarations and statements start; a failure turns the innermost one into an error node.
    var recoveryPoints: [(depth: Int, start: GesSyntaxTreeBuilder.Checkpoint)] = []
    var completedInfixKind = GesSyntaxNodeKind.binaryExpression
    // Set while the brackets after `:has` form an object pattern rather than a collection literal.
    var objectPattern = false

    init(_ text: String) {
        self.text = text
        var lexer = GesLexer(text)
        repeat { tokens.append(lexer.read()) } while tokens.last!.kind != .endOfFile
    }

    /// Parses one source into its lossless syntax tree, whose diagnostics report its syntax errors in source order.
    @_spi(Syntax) public static func parse(_ text: String) -> GesSyntaxTree {
        let parser = GesSyntaxParser(text)
        do { try parser.parseModule() } catch let failure as Failure {
            // The nesting limit ends parsing; the syntax errors found before it stay reported. The innermost open
            // statement or declaration and the rest of the source form one error node.
            parser.diagnostics.append(failure.diagnostic)
            let (depth, start) = parser.recoveryPoints[parser.recoveryPoints.count - 1]
            parser.tree.discard(to: depth, start)
            parser.startNode(at: start, .error)
            while parser.currentKind != .endOfFile { parser.advance() }
        } catch {}
        return parser.completeTree()
    }

    func startNode(_ kind: GesSyntaxNodeKind) { tree.startNode(kind, at: index) }

    func finishNode() { tree.finishNode(at: index) }

    func finishNode<T>(_ value: T) -> T {
        tree.finishNode(at: index)
        return value
    }

    func mark() -> GesSyntaxTreeBuilder.Checkpoint { tree.mark(index) }

    func startNode(at checkpoint: GesSyntaxTreeBuilder.Checkpoint, _ kind: GesSyntaxNodeKind) { tree.startNode(at: checkpoint, kind) }

    // Wraps everything parsed since the checkpoint into one node, for nodes whose kind is known only at their end.
    func wrap<T>(_ checkpoint: GesSyntaxTreeBuilder.Checkpoint, _ kind: GesSyntaxNodeKind, _ value: T) -> T {
        tree.startNode(at: checkpoint, kind)
        tree.finishNode(at: index)
        return value
    }

    // Recovery skips tokens until a synchronization point. The failed declaration or statement, from its start, and the
    // skipped tokens form one error node, whatever part of it was parsed before the failure.
    @discardableResult func recover(_ synchronize: () -> Bool) -> Bool {
        let (depth, start) = recoveryPoints[recoveryPoints.count - 1]
        tree.discard(to: depth, start)
        startNode(at: start, .error)
        let result = synchronize()
        finishNode()
        return result
    }

    func enterRecoveryPoint() { recoveryPoints.append((tree.depth, mark())) }

    func leaveRecoveryPoint() { recoveryPoints.removeLast() }

    // Closes the open nodes, attaches the tokens a failure left unread as an error node and ends with endOfFile.
    func completeTree() -> GesSyntaxTree {
        tree.finish(to: 1, at: index)
        if currentKind != .endOfFile {
            startNode(.error)
            while currentKind != .endOfFile { advance() }
            finishNode()
        }
        return tree.finish(text: text, tokens: tokens, diagnostics: diagnostics)
    }

    var current: GesToken { tokens[index] }
    // Only the kind, so that a recursive parse function does not hold a copy of the whole token for each test.
    var currentKind: GesTokenKind { tokens[index].kind }
    var previous: GesToken { tokens[max(0, index - 1)] }
    var previousIndex: Int { max(0, index - 1) }

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

    // Contextual words are identifiers compared by spelling.
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

    func matchSelector(_ name: String) -> Bool {
        if currentKind == .selector && current.text == name {
            advance()
            return true
        }
        return false
    }

    func newlines() { while currentKind == .newLine { advance() } }

    func separators() { while currentKind == .newLine || currentKind == .semicolon { advance() } }

    func diagnostic(_ code: String, _ message: String, start: Int, end: Int) -> GesSyntaxDiagnostic {
        let first = start >= 0 ? tokens[start] : nil
        let last = end >= 0 ? tokens[end] : nil
        return .init(code: code, message: message, line: first?.line, column: first?.column, endLine: last?.endLine, endColumn: last?.endColumn, moduleName: module)
    }

    func failure(_ message: String, _ token: GesToken? = nil, code: String = GesSyntaxDiagnostic.syntax) -> Failure {
        let token = token ?? current
        return .init(diagnostic: .init(code: code, message: message, line: token.line, column: token.column, endLine: token.endLine, endColumn: token.endColumn, moduleName: module))
    }

    // An expression that exceeds the depth limit reports at its own range.
    func nestingLimit(_ start: Int, _ end: Int) -> Failure { .init(diagnostic: diagnostic(GesSyntaxDiagnostic.sourceNestingExceeded, "Expression nesting limit exceeded.", start: start, end: end)) }

    func identifier() throws -> String {
        guard current.isName else { throw failure("Expected an identifier.") }
        return advance().text
    }

    static let builtinTypeNames: Set<String> = ["Nothing", "Number", "Boolean", "Percentage", "Vector", "Point", "Series", "Tag", "Text", "List", "Range", "Message", "Handler", "Map", "Dice", "Record"]

    static let quantityUnitKinds: Set<GesTokenKind> = [.identifier, .message, .operatorDivide, .operatorPower, .superscriptInteger, .float]

    @discardableResult func typeName() throws -> String {
        guard currentKind == .typeName else { throw failure("Expected a type name.") }
        startNode(.typeReference)
        return finishNode(try typeNameCore())
    }

    // The canonical type name: built-in types are lowercase, a quantity type names its unit.
    func typeNameCore() throws -> String {
        var name = String(advance().text.dropFirst())
        if Self.builtinTypeNames.contains(name) { name = name.lowercased() }
        // Uppercase unit symbols such as N or Hz read as message-name tokens.
        if name == "Quantity", currentKind == .leftParen, peek().isWord || peek().isIdentifier("degree") || peekKind() == .message, quantityUnitAhead() {
            advance()
            newlines()
            while currentKind != .rightParen {
                advance()
                newlines()
            }
            try expect(.rightParen)
            name = "quantity"
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

    // A handler literal's parameters have no list node of their own.
    func parameters(_ allowType: Bool = true, listNode: Bool = true) throws {
        if listNode { startNode(.parameterList) }
        defer { if listNode { finishNode() } }
        try expect(.leftParen)
        newlines()
        if match(.rightParen) { return }
        repeat {
            newlines()
            startNode(.parameter)
            _ = match(.underscore)
            newlines()
            _ = try identifier()
            newlines()
            if allowType && match(.`as`) {
                newlines()
                try typeName()
                newlines()
            }
            finishNode()
        } while match(.comma)
        try expect(.rightParen)
    }

    func parseModule() throws {
        startNode(.compilationUnit)
        separators()
        enterRecoveryPoint()
        do {
            if currentKind == .module {
                startNode(.moduleDeclaration)
                advance()
                newlines()
                module = try identifier()
                while match(.dot) { module += "." + (try identifier()) }
                if !module.split(separator: ".").allSatisfy({ $0.unicodeScalars.allSatisfy { GesLexer.lower($0) || GesLexer.digit($0) } }) { throw failure("Invalid module name.", previous) }
                finishNode()
            }
        } catch let error as Failure {
            try record(error)
            recover {
                synchronizeTopLevel()
                return true
            }
        }
        leaveRecoveryPoint()
        separators()
        while currentKind != .endOfFile {
            let declarationStart = index
            enterRecoveryPoint()
            do { try declaration() } catch let error as Failure {
                try record(error)
                recover {
                    // A declaration that fails on its first token must still move on, or recovery would stop there again.
                    if index == declarationStart { advance() }
                    synchronizeTopLevel()
                    return true
                }
            }
            leaveRecoveryPoint()
        }
    }

    // Keeps a syntax error and continues; the nesting limit ends parsing.
    func record(_ error: Failure) throws {
        guard error.diagnostic.code == GesSyntaxDiagnostic.syntax else { throw error }
        diagnostics.append(error.diagnostic)
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

    func declaration() throws {
        switch currentKind {
        case .constant: startNode(.constantDefinition)
        case .record: startNode(.recordDefinition)
        case .predicate: startNode(.predicateDefinition)
        case .function: startNode(.functionDefinition)
        case .module: startNode(.moduleDeclaration)
        case .illegal: startNode(.error)
        default: startNode(.eventHandler)
        }
        if currentKind == .illegal {
            let token = advance()
            throw failure("Illegal token '\(token.text)'.", token)
        }
        if match(.module) { throw failure("A module declaration must precede all other declarations and appear once.", previous) }
        if match(.constant) {
            try constantDeclaration()
        } else if match(.record) {
            try recordDeclaration()
        } else if currentKind == .function || currentKind == .predicate {
            try routineDeclaration()
        } else {
            try handlerDeclaration()
        }
        finishNode()
        if currentKind != .endOfFile && currentKind != .newLine && currentKind != .semicolon { throw failure("Expected a declaration separator.") }
        separators()
    }

    func constantDeclaration() throws {
        guard currentKind == .constantReference else { throw failure("Expected constant name.") }
        let nameToken = advance()
        try expect(.be)
        newlines()
        switch try expression(15).shape {
        case .number, .boolean, .quantity, .scalar, .negated: break
        case .negatedOther: throw failure("Expected numeric literal after '-'.", nameToken)
        case .none: throw failure("Expected scalar literal.", nameToken)
        }
    }

    func recordDeclaration() throws {
        try typeName()
        try expect(.`as`)
        newlines()
        try expect(.leftBrace)
        separators()
        while currentKind != .rightBrace {
            try recordField()
            _ = match(.comma)
            separators()
        }
        try expect(.rightBrace)
    }

    func recordField() throws {
        startNode(.recordField)
        let fieldStart = current
        let unlabeled = match(.underscore)
        _ = try identifier()
        try expect(.colon)
        newlines()
        try typeName()
        newlines()
        if matchWord("clamped") {
            try expectWord("between")
            _ = try expression(9)
            try expect(.operatorAnd)
            _ = try expression(9)
            newlines()
        }
        var computed = false
        if matchWord("computed") {
            try expectWord("by")
            _ = try assemblyOrExpression()
            computed = true
        }
        if unlabeled && computed { throw failure("Computed field cannot be a parameter.", fieldStart) }
        finishNode()
    }

    func routineDeclaration() throws {
        advance()
        _ = try identifier()
        try parameters()
        try expect(.be)
        newlines()
        _ = try assemblyOrExpression()
    }

    func handlerDeclaration() throws {
        try expect(.on)
        newlines()
        guard currentKind == .message || currentKind == .identifier && ["initialization", "undeliverable"].contains(current.text) else { throw failure("Expected message name.") }
        advance()
        if currentKind == .`as` {
            startNode(.parameter)
            advance()
            newlines()
            _ = try identifier()
            finishNode()
        } else if currentKind == .leftParen {
            try parameters()
        }
        while true {
            newlines()
            guard currentKind == .matching || currentKind == .without else { break }
            startNode(.handlerTagFilter)
            advance()
            repeat {
                newlines()
                guard currentKind == .tag else { throw failure("Expected tag literal.") }
                advance()
                newlines()
            } while match(.comma)
            finishNode()
        }
        startNode(.block)
        try expect(.leftBrace)
        try statements()
        try expect(.rightBrace)
        finishNode()
    }

    // A block's statements up to its closing brace; an error resumes at the next statement of the same block.
    func statements() throws {
        separators()
        while currentKind != .rightBrace {
            if currentKind == .endOfFile {
                try record(failure("Expected '}' before end of input."))
                return
            }
            enterRecoveryPoint()
            do {
                try statement()
                if currentKind != .rightBrace && currentKind != .newLine && currentKind != .semicolon { throw failure("Expected a statement separator.") }
                separators()
            } catch let error as Failure {
                try record(error)
                if !recover({ synchronizeStatement() }) {
                    leaveRecoveryPoint()
                    return
                }
            }
            leaveRecoveryPoint()
        }
    }

    func body() throws {
        if statementDepth >= 32 { throw failure("Statement nesting limit exceeded.", code: GesSyntaxDiagnostic.sourceNestingExceeded) }
        statementDepth += 1
        defer { statementDepth -= 1 }
        newlines()
        if currentKind == .leftBrace {
            startNode(.block)
            advance()
            try statements()
            try expect(.rightBrace)
            finishNode()
            return
        }
        try statement()
    }

    // Nested blocks recurse through `statement`, `body` and `statements`, so each statement form parses in its own
    // function, as nested expressions do.
    func statement() throws {
        if current.isIdentifier("asm") {
            startNode(.assemblyBlock)
        } else if currentKind == .keywordRandom && peekKind() == .with {
            startNode(.seededRandomStatement)
        } else {
            switch currentKind {
            case .emit: startNode(.emitStatement)
            case .publish: startNode(.publishStatement)
            case .`let`: startNode(.letStatement)
            case .`if`: startNode(.ifStatement)
            case .`for`: startNode(.forStatement)
            default: startNode(.expressionStatement)
            }
        }
        try statementCore()
        finishNode()
    }

    func statementCore() throws {
        let start = index
        if matchWord("asm") {
            try assemblyBlock()
        } else if currentKind == .emit || currentKind == .publish {
            if peek().isIdentifier("after") {
                advance()
                _ = try sendExpression(start, mark())
                return
            }
            try publishStatement()
        } else if match(.`let`) {
            _ = try identifier()
            try expect(.be)
            _ = try assemblyOrExpression()
        } else if match(.`if`) {
            try conditionStatement()
        } else if match(.`for`) {
            try forClause()
        } else if currentKind == .keywordRandom && peekKind() == .with {
            advance()
            newlines()
            try expect(.with)
            _ = try expression()
            try body()
        } else {
            _ = try expression()
        }
    }

    func publishStatement() throws {
        advance()
        newlines()
        if currentKind == .message {
            startNode(.messageExpression)
            advance()
            if currentKind == .leftParen { try arguments() }
            finishNode()
        } else {
            _ = try expression()
        }
        if currentKind == .with {
            startNode(.tagClause)
            advance()
            repeat { _ = try expression() } while match(.comma)
            finishNode()
        }
    }

    func conditionStatement() throws {
        var conditions = 0
        repeat {
            newlines()
            if currentKind == .leftBrace && conditions > 0 { break }
            startNode(.ifCondition)
            if match(.`let`) {
                _ = try identifier()
                try expect(.be)
            }
            _ = try expression()
            conditions += 1
            finishNode()
            newlines()
        } while match(.semicolon)
        try body()
        if peekKind(0) == .`else` {
            newlines()
            startNode(.elseClause)
            try expect(.`else`)
            try body()
            finishNode()
        }
    }

    func forClause() throws {
        _ = try bindingNames()
        newlines()
        let range = current.isIdentifier("from")
        if range {
            startNode(.rangeExpression)
            advance()
            _ = try forSource(range: true)
            finishNode()
        } else {
            _ = try forSource(range: false)
        }
        newlines()
        if isForClauseStart(requireSourceContext: false) {
            if statementDepth >= 32 { throw failure("Statement nesting limit exceeded.", code: GesSyntaxDiagnostic.sourceNestingExceeded) }
            // `and item in ...` continues with a nested for statement.
            startNode(.forStatement)
            advance()
            newlines()
            statementDepth += 1
            defer { statementDepth -= 1 }
            try forClause()
            finishNode()
            return
        }
        try body()
    }

    func forSource(range: Bool) throws -> Expression {
        let saved = forSourceDepth
        forSourceDepth = delimiterDepth
        defer { forSourceDepth = saved }
        if range { return try rangeExpression(previousIndex) }
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

    func bindingNames() throws -> Int {
        _ = try identifier()
        var count = 1
        while match(.comma) {
            newlines()
            _ = try identifier()
            count += 1
        }
        return count
    }

    // A node ranging from `start` to the previous token, with the depth of a parser node.
    func node(_ start: Int, shape: Shape = .none) -> Expression { .init(depth: 1, start: start, end: previousIndex, shape: shape) }

    // A node with exactly the range of `node`, one level deeper.
    func ranged(_ node: Expression) throws -> Expression {
        let depth = node.depth + 1
        if depth > 32 { throw nestingLimit(node.start, node.end) }
        return .init(depth: depth, start: node.start, end: node.end)
    }

    // From the start of `first` to the end of `last`, or to the previous token without `last`.
    func combined(_ first: Expression, _ last: Expression? = nil) throws -> Expression {
        let depth = max(first.depth, last?.depth ?? 0) + 1
        let end = last?.end ?? previousIndex
        if depth > 32 { throw nestingLimit(first.start, end) }
        return .init(depth: depth, start: first.start, end: end)
    }
}
