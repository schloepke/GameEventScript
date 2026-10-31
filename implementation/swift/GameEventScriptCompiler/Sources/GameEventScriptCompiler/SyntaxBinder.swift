// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

@_spi(Compiler) import GameEventScriptRuntime
@_spi(Syntax) import GameEventScriptSyntax

// Builds the semantic module of one source from its lossless syntax tree. Each node's tokens are read in the order the
// parser consumed them, so every semantic node receives the source range and depth the parser gives it.
final class GesSyntaxBinder {
    // The tree does not have the form the binder expects at this token.
    struct Mismatch: Error {
        let token: Int
    }

    // Reads the elements of one node: its own tokens and its children, in source order.
    struct Cursor {
        let tokens: [GesToken]
        let children: [GesSyntaxNode]
        let end: Int
        var position: Int
        var child = 0
        var previousIndex: Int

        init(_ tokens: [GesToken], _ node: GesSyntaxNode) {
            self.tokens = tokens
            children = node.children
            end = node.endToken
            position = node.firstToken
            previousIndex = node.firstToken - 1
        }

        // The token at the cursor, also where a child starts; past the node's end it is the next token of the source.
        var current: GesToken { tokens[min(position, tokens.count - 1)] }

        // The last token consumed, as the parser's previous token.
        var previous: GesToken { tokens[max(previousIndex, 0)] }

        var atEnd: Bool { position >= end }

        var node: GesSyntaxNode? { child < children.count && children[child].firstToken == position ? children[child] : nil }

        func isNode(_ kind: GesSyntaxNodeKind) -> Bool { node?.kind == kind }

        // Whether the cursor is at a token of this node itself rather than at a child.
        var isOwnToken: Bool { position < end && node == nil }

        func `is`(_ kind: GesTokenKind) -> Bool { isOwnToken && tokens[position].kind == kind }

        func isWord(_ word: String) -> Bool { isOwnToken && tokens[position].isIdentifier(word) }

        // The own token `offset` significant tokens ahead; endOfFile where a child or the node's end comes first.
        func peekSignificant(_ offset: Int) -> GesToken {
            let limit = min(end, child < children.count ? children[child].firstToken : Int.max)
            var seen = 0
            var index = position
            while index < limit {
                if tokens[index].kind != .newLine {
                    if seen == offset { return tokens[index] }
                    seen += 1
                }
                index += 1
            }
            return tokens[tokens.count - 1]
        }

        @discardableResult mutating func advance() throws -> GesToken {
            guard isOwnToken else { throw Mismatch(token: position) }
            previousIndex = position
            position += 1
            return tokens[previousIndex]
        }

        mutating func takeNode() throws -> GesSyntaxNode {
            guard let node else { throw Mismatch(token: position) }
            child += 1
            if node.endToken > node.firstToken {
                position = node.endToken
                previousIndex = node.endToken - 1
            }
            return node
        }

        mutating func takeNode(_ kind: GesSyntaxNodeKind) throws -> GesSyntaxNode {
            guard isNode(kind) else { throw Mismatch(token: position) }
            return try takeNode()
        }

        @discardableResult mutating func expect(_ kind: GesTokenKind) throws -> GesToken {
            guard self.is(kind) else { throw Mismatch(token: position) }
            return try advance()
        }

        mutating func match(_ kind: GesTokenKind) throws -> Bool {
            guard self.is(kind) else { return false }
            try advance()
            return true
        }

        mutating func matchWord(_ word: String) throws -> Bool {
            guard isWord(word) else { return false }
            try advance()
            return true
        }

        mutating func matchSelector(_ text: String) throws -> Bool {
            guard isOwnToken, tokens[position].kind == .selector, tokens[position].text == text else { return false }
            try advance()
            return true
        }

        mutating func expectWord(_ word: String) throws {
            guard try matchWord(word) else { throw Mismatch(token: position) }
        }

        mutating func identifier() throws -> String {
            guard self.is(.identifier) else { throw Mismatch(token: position) }
            return try advance().text
        }

        mutating func newlines() throws {
            while self.is(.newLine) { try advance() }
        }

        mutating func separators() throws {
            while self.is(.newLine) || self.is(.semicolon) { try advance() }
        }
    }

    let tree: GesSyntaxTree
    let tokens: [GesToken]
    let source: GesSource
    var module = ""

    init(tree: GesSyntaxTree, source: GesSource) {
        self.tree = tree
        tokens = tree.tokens
        self.source = source
    }

    // The tree must come from a source without syntax errors.
    static func bind(_ tree: GesSyntaxTree, _ source: GesSource) throws -> GesModule { try GesSyntaxBinder(tree: tree, source: source).bindModule() }

    func cursor(_ node: GesSyntaxNode) -> Cursor { Cursor(tokens, node) }

    func location(_ start: GesToken, _ end: GesToken? = nil) -> GesSourceLocation {
        let last = end ?? start
        return .init(sourceName: source.name, line: start.line, column: start.column, endLine: last.endLine, endColumn: last.endColumn, moduleName: module, sourceID: source.id)
    }

    func node(_ kind: GesExpression.Kind, _ start: GesToken, _ end: GesToken) -> GesExpression { .init(kind, location(start, end)) }

    // A node without a source range, as the parser's unranged nodes.
    func unranged(_ kind: GesExpression.Kind, _ start: GesToken, _ end: GesToken) -> GesExpression { .init(kind, GesAstOptimizer.noRange(location(start, end))) }

    // A node with exactly the range of `node`, as the parser's ranged nodes.
    func ranged(_ kind: GesExpression.Kind, _ node: GesExpression) -> GesExpression { .init(kind, node.location, depth: node.depth + 1) }

    // From the start of `first` to the end of `last`, or to `previous` without `last`, as the parser's combined nodes.
    func combined(_ kind: GesExpression.Kind, _ first: GesExpression, _ last: GesExpression?, _ previous: GesToken) -> GesExpression {
        let a = first.location
        let b = last?.location ?? location(previous)
        let range = GesSourceLocation(sourceName: a.sourceName, line: a.line, column: a.column, endLine: b.endLine, endColumn: b.endColumn, moduleName: module, sourceID: source.id)
        return .init(kind, range, depth: max(first.depth, last?.depth ?? 0) + 1)
    }

    func bindModule() throws -> GesModule {
        var c = cursor(tree.root)
        try c.separators()
        let start = c.current
        if c.isNode(.moduleDeclaration) {
            var m = cursor(try c.takeNode())
            try m.expect(.module)
            try m.newlines()
            module = try m.identifier()
            while try m.match(.dot) { module += "." + (try m.identifier()) }
        }
        try c.separators()
        var definitions: [GesDefinition] = []
        var constants: [(name: String, value: GesExpression, location: GesSourceLocation)] = []
        var records: [GesRecord] = []
        while let declaration = c.node {
            _ = try c.takeNode()
            switch declaration.kind {
            case .constantDefinition: constants.append(try bindConstant(declaration))
            case .recordDefinition: records.append(try bindRecord(declaration))
            case .predicateDefinition, .functionDefinition: definitions.append(try bindRoutine(declaration))
            case .eventHandler: definitions.append(try bindHandler(declaration))
            default: throw Mismatch(token: declaration.firstToken)
            }
            try c.separators()
        }
        guard c.is(.endOfFile) else { throw Mismatch(token: c.position) }
        return .init(name: module, definitions: definitions, constants: constants, records: records, location: location(start, c.previous))
    }

    func bindConstant(_ declaration: GesSyntaxNode) throws -> (name: String, value: GesExpression, location: GesSourceLocation) {
        var c = cursor(declaration)
        let begin = try c.expect(.constant)
        let name = String(try c.expect(.constantReference).text.dropFirst())
        try c.expect(.be)
        try c.newlines()
        let value = try expression(&c)
        return (name, value, location(begin, c.previous))
    }

    func bindRecord(_ declaration: GesSyntaxNode) throws -> GesRecord {
        var c = cursor(declaration)
        let begin = try c.expect(.record)
        let name = try typeName(c.takeNode(.typeReference))
        try c.expect(.as)
        try c.newlines()
        try c.expect(.leftBrace)
        try c.separators()
        var fields: [GesField] = []
        while c.isNode(.recordField) {
            fields.append(try bindField(c.takeNode()))
            _ = try c.match(.comma)
            try c.separators()
        }
        try c.expect(.rightBrace)
        return .init(name: name, fields: fields, location: location(begin, c.previous))
    }

    func bindField(_ field: GesSyntaxNode) throws -> GesField {
        var c = cursor(field)
        let fieldStart = c.current
        let unlabeled = try c.match(.underscore)
        let name = try c.identifier()
        try c.expect(.colon)
        try c.newlines()
        let type = try typeName(c.takeNode(.typeReference))
        try c.newlines()
        var low: GesExpression?
        var high: GesExpression?
        var computed: GesExpression?
        if try c.matchWord("clamped") {
            try c.expectWord("between")
            low = try expression(&c)
            try c.expect(.operatorAnd)
            high = try expression(&c)
            try c.newlines()
        }
        if try c.matchWord("computed") {
            try c.expectWord("by")
            computed = try assemblyOrExpression(&c, name)
        }
        return .init(name: name, type: type, label: computed == nil ? (unlabeled ? "_" : name) : nil, minimum: low, maximum: high, computed: computed, location: location(fieldStart, c.previous))
    }

    func bindRoutine(_ declaration: GesSyntaxNode) throws -> GesDefinition {
        var c = cursor(declaration)
        let begin = c.current
        let kind = try c.advance().text
        let name = try c.identifier()
        var list = cursor(try c.takeNode(.parameterList))
        let parameters = try self.parameters(&list)
        try c.expect(.be)
        try c.newlines()
        let body = try assemblyOrExpression(&c, name, predicate: kind == "predicate")
        return .init(kind: kind, name: name, parameters: parameters, expression: body, statements: [], required: [], excluded: [], location: location(begin, c.previous))
    }

    // Parameters in parentheses; a handler literal's parameters have no list node of their own.
    func parameters(_ c: inout Cursor, allowType: Bool = true) throws -> [GesParameter] {
        try c.expect(.leftParen)
        try c.newlines()
        var result: [GesParameter] = []
        if try c.match(.rightParen) { return result }
        repeat {
            try c.newlines()
            var p = cursor(try c.takeNode(.parameter))
            let start = p.current
            let unlabeled = try p.match(.underscore)
            try p.newlines()
            let name = try p.identifier()
            try p.newlines()
            var type: String?
            if allowType, try p.match(.as) {
                try p.newlines()
                type = try typeName(p.takeNode(.typeReference))
                try p.newlines()
            }
            result.append(.init(label: unlabeled ? "_" : name, name: name, type: type, location: location(start, p.previous)))
        } while try c.match(.comma)
        try c.expect(.rightParen)
        return result
    }

    func bindHandler(_ declaration: GesSyntaxNode) throws -> GesDefinition {
        var c = cursor(declaration)
        let begin = try c.expect(.on)
        try c.newlines()
        let name = try c.advance().text
        var kind = "handler"
        var parameters: [GesParameter] = []
        if c.isNode(.parameter) {
            var p = cursor(try c.takeNode())
            try p.expect(.as)
            try p.newlines()
            let token = p.current
            let local = try p.identifier()
            kind = "messageNameHandler"
            parameters = [.init(label: "message", name: local, type: "message", location: location(token))]
        } else if c.isNode(.parameterList) {
            var list = cursor(try c.takeNode())
            parameters = try self.parameters(&list)
        }
        var required: [String] = []
        var excluded: [String] = []
        while true {
            try c.newlines()
            guard c.isNode(.handlerTagFilter) else { break }
            var f = cursor(try c.takeNode())
            let include = try f.advance().kind == .matching
            repeat {
                try f.newlines()
                let tag = String(try f.expect(.tag).text.dropFirst())
                if include { if !required.contains(tag) { required.append(tag) } } else if !excluded.contains(tag) { excluded.append(tag) }
                try f.newlines()
            } while try f.match(.comma)
        }
        let body = try block(c.takeNode(.block))
        return .init(kind: kind, name: name, parameters: parameters, expression: nil, statements: body, required: required, excluded: excluded, location: location(begin, c.previous))
    }

    func typeName(_ reference: GesSyntaxNode) throws -> String {
        var c = cursor(reference)
        return try typeNameCore(&c)
    }

    func typeNameCore(_ c: inout Cursor) throws -> String {
        var name = String(try c.expect(.typeName).text.dropFirst())
        if GesParser.builtinTypeNames.contains(name) { name = name.lowercased() }
        // Only a quantity type owns a parenthesized unit, such as `:Quantity(m/s)`.
        if name == "Quantity", c.is(.leftParen) {
            try c.advance()
            try c.newlines()
            var unit = ""
            while !c.is(.rightParen) {
                let part = try c.advance()
                unit += part.kind == .superscriptInteger ? part.text == "2" ? "²" : "³" : part.text
                try c.newlines()
            }
            try c.expect(.rightParen)
            name = "quantity:" + unit
        }
        return name
    }

    func block(_ node: GesSyntaxNode) throws -> [GesStatement] {
        var c = cursor(node)
        try c.expect(.leftBrace)
        try c.separators()
        var result: [GesStatement] = []
        while let statement = c.node {
            _ = try c.takeNode()
            result.append(try self.statement(statement))
            try c.separators()
        }
        try c.expect(.rightBrace)
        return result
    }

    func body(_ c: inout Cursor) throws -> [GesStatement] {
        try c.newlines()
        if c.isNode(.block) { return try block(c.takeNode()) }
        return [try statement(c.takeNode())]
    }

    func statement(_ node: GesSyntaxNode) throws -> GesStatement {
        var c = cursor(node)
        let start = c.current
        let kind: GesStatement.Kind
        switch node.kind {
        case .assemblyBlock:
            try c.expectWord("asm")
            kind = .assembly(try assemblyBlock(&c, nil, allowSend: true))
        case .emitStatement, .publishStatement:
            // A delayed send statement is a send expression directly after its keyword.
            if let send = node.children.first, send.kind == .sendExpression, send.firstToken == node.firstToken + 1 {
                let publish = try c.advance().kind == .publish
                let send = try bindSend(c.takeNode(.sendExpression), publish: publish, start: start)
                return GesStatement(kind: .expression(send), location: send.location)
            }
            kind = try publishStatement(&c)
        case .letStatement:
            try c.expect(.let)
            let name = try c.identifier()
            try c.expect(.be)
            kind = .letBinding(name, try assemblyOrExpression(&c, name, allowSend: true))
        case .ifStatement:
            try c.expect(.if)
            kind = try condition(&c)
        case .forStatement:
            try c.expect(.for)
            kind = try forClause(&c, start)
        case .seededRandomStatement:
            try c.advance()
            try c.newlines()
            try c.expect(.with)
            let seed = try expression(&c)
            kind = .seeded(seed, try body(&c))
        case .expressionStatement:
            kind = .expression(try expression(&c))
        default:
            throw Mismatch(token: node.firstToken)
        }
        return .init(kind: kind, location: location(start, c.previous))
    }

    func publishStatement(_ c: inout Cursor) throws -> GesStatement.Kind {
        let start = c.current
        let publish = try c.advance().kind == .publish
        try c.newlines()
        // A message literal of a send statement starts at the emit or publish keyword, as in the parser.
        let message = c.isNode(.messageExpression) ? try messageLiteral(c.takeNode(), start: start) : try expression(&c)
        var tags: [GesExpression] = []
        if c.isNode(.tagClause) {
            var t = cursor(try c.takeNode())
            try t.expect(.with)
            repeat { tags.append(try expression(&t)) } while try t.match(.comma)
        }
        return .publish(publish, message, tags)
    }

    func condition(_ c: inout Cursor) throws -> GesStatement.Kind {
        var conditions: [(binding: String?, value: GesExpression, location: GesSourceLocation)] = []
        repeat {
            try c.newlines()
            if c.isNode(.block) && !conditions.isEmpty { break }
            var condition = cursor(try c.takeNode(.ifCondition))
            let conditionStart = condition.current
            var binding: String?
            if try condition.match(.let) {
                binding = try condition.identifier()
                try condition.expect(.be)
            }
            let value = try expression(&condition)
            conditions.append((binding, value, location(conditionStart, condition.previous)))
            try c.newlines()
        } while try c.match(.semicolon)
        let then = try body(&c)
        var otherwise: [GesStatement] = []
        try c.newlines()
        if c.isNode(.elseClause) {
            var e = cursor(try c.takeNode())
            try e.expect(.else)
            otherwise = try body(&e)
        }
        return .condition(conditions, then, otherwise)
    }

    // A for clause; a nested `and` clause starts at its `and` token.
    func forClause(_ c: inout Cursor, _ start: GesToken) throws -> GesStatement.Kind {
        let names = try bindingNames(&c)
        try c.newlines()
        let range = c.isNode(.rangeExpression)
        let sequence: GesExpression
        if range {
            var r = cursor(try c.takeNode())
            try r.expectWord("from")
            sequence = try rangeExpression(&r, start)
        } else {
            try c.expect(.in)
            try c.newlines()
            sequence = try expression(&c)
        }
        try c.newlines()
        if c.isNode(.forStatement) {
            var n = cursor(try c.takeNode())
            let next = try n.advance()
            try n.newlines()
            let inner = try forClause(&n, next)
            return .loop(names, sequence, range, [.init(kind: inner, location: location(next, n.previous))])
        }
        return .loop(names, sequence, range, try body(&c))
    }

    func bindingNames(_ c: inout Cursor) throws -> [String] {
        var result = [try c.identifier()]
        while try c.match(.comma) {
            try c.newlines()
            result.append(try c.identifier())
        }
        return result
    }

    func assemblyOrExpression(_ c: inout Cursor, _ output: String, predicate: Bool = false, allowSend: Bool = false) throws -> GesExpression {
        guard c.isNode(.assemblyBlock) else { return try expression(&c) }
        var a = cursor(try c.takeNode())
        let start = try a.advance()
        return node(.assembly(try assemblyBlock(&a, output, predicate: predicate, allowSend: allowSend)), start, a.previous)
    }

    func assemblyBlock(_ c: inout Cursor, _ output: String?, predicate: Bool = false, allowSend: Bool = false) throws -> GesAssembly {
        let start = c.previous
        try c.newlines()
        try c.expect(.leftBrace)
        try c.separators()
        var declarations: [(name: String, export: Bool)] = []
        var lines: [GesAssemblyLine] = []
        while !c.is(.rightBrace) {
            let token = c.current
            let exported = try c.match(.let)
            var declaration = exported
            if !declaration { declaration = try c.match(.dot) }
            if declaration {
                if !exported { try c.expectWord("register") }
                repeat { declarations.append((try c.identifier(), exported)) } while try c.match(.comma)
            } else if c.is(.emit) || c.is(.publish) {
                guard case .publish(let publish, let message, let tags) = try publishStatement(&c) else { throw Mismatch(token: c.position) }
                lines.append(.init(name: publish ? "publish" : "emit", label: false, operands: [message] + tags, location: location(token, c.previous)))
            } else if c.isOwnToken && c.current.isWord {
                let name = try c.identifier()
                try c.expect(.colon)
                lines.append(.init(name: name, label: true, operands: [], location: location(token, c.previous)))
            } else {
                let name = try c.expect(.message).text
                var operands: [GesExpression] = []
                if !c.is(.newLine) && !c.is(.semicolon) && !c.is(.rightBrace) {
                    repeat {
                        if c.is(.operatorMinus) {
                            // A minus of its own token is the Int64 minimum, which has no positive literal.
                            try c.advance()
                            try c.advance()
                            operands.append(node(.literal(.integer(Int64.min)), token, c.previous))
                        } else if c.is(.typeName) {
                            let type = try c.advance()
                            operands.append(node(.literal(.text(String(type.text.dropFirst()))), type, type))
                        } else if c.isNode(.messageExpression) {
                            operands.append(try messageLiteral(c.takeNode()))
                        } else {
                            operands.append(try expression(&c))
                        }
                    } while try c.match(.comma)
                }
                lines.append(.init(name: name, label: false, operands: operands, location: location(token, c.previous)))
            }
            try c.separators()
        }
        try c.expect(.rightBrace)
        let normalizedLines = lines.map { line in
            GesAssemblyLine(name: line.name, label: line.label, operands: line.operands.map(GesParser.normalizeAssemblyOperand), location: line.location)
        }
        return .init(output: output, declarations: declarations, lines: normalizedLines, predicate: predicate, allowSend: allowSend, location: location(start, c.previous))
    }
}
