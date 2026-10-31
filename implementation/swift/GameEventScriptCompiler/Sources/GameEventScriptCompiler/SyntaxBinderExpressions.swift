// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

@_spi(Compiler) import GameEventScriptRuntime
@_spi(Syntax) import GameEventScriptSyntax

extension GesSyntaxBinder {
    // Every expression is one element: a child node, or a single token for a literal or a name.
    func expression(_ c: inout Cursor) throws -> GesExpression {
        try c.newlines()
        if c.node != nil { return try bindExpression(c.takeNode()) }
        return try primary(c.advance())
    }

    func primary(_ token: GesToken) throws -> GesExpression {
        if token.isNumber {
            guard let value = GesCompilerSupport.number(token.text, percentage: token.text.hasSuffix("%")) else { throw Mismatch(token: 0) }
            let result = node(.literal(value), token, token)
            result.decimalLiteral = token.text.contains(".") || value.valueKind == .float
            return result
        }
        switch token.kind {
        case .text: return node(.literal(.text(token.text)), token, token)
        case .tag: return node(.literal(try .tag(String(token.text.dropFirst()))), token, token)
        case .constantReference: return node(.constant(String(token.text.dropFirst())), token, token)
        case .`true`: return node(.literal(.boolean(true)), token, token)
        case .`false`: return node(.literal(.boolean(false)), token, token)
        case .nothing: return node(.literal(.nothing), token, token)
        case .identifier: return node(.name(token.text), token, token)
        default:
            guard let constant = Self.mathConstants[token.kind] else { throw Mismatch(token: 0) }
            return node(.literal(.float(constant)), token, token)
        }
    }

    func bindExpression(_ node: GesSyntaxNode) throws -> GesExpression {
        switch node.kind {
        case .guardedChoiceExpression: return try guardedChoice(node)
        case .binaryExpression: return try binary(node)
        case .membershipExpression: return try membership(node)
        case .typeOperationExpression: return try typeOperation(node)
        case .unaryExpression: return try unary(node)
        case .intrinsicCallExpression: return try intrinsicCall(node)
        case .clampExpression: return try clamp(node)
        case .variadicExpression: return try variadic(node)
        case .extensionCallExpression: return try extensionCall(node)
        case .memberAccessExpression: return try memberAccess(node)
        case .collectionAccessExpression: return try collectionAccess(node)
        case .typeConstructorExpression: return try typeConstructor(node)
        case .diceLiteralExpression: return try diceLiteral(node)
        case .collectionFactoryExpression: return try collectionFactory(node)
        case .rangeExpression:
            var c = cursor(node)
            let start = try c.expect(.identifier)
            return try rangeExpression(&c, start)
        case .randomExpression: return try random(node)
        case .seededRandomExpression: return try seededRandom(node)
        case .seriesExpression: return try series(node)
        case .diceExpression: return try dice(node)
        case .listLiteralExpression, .mapLiteralExpression, .objectPattern: return try collection(node)
        case .messageExpression: return try messageLiteral(node)
        case .callExpression: return try call(node)
        case .handlerLiteralExpression: return try handlerLiteral(node)
        case .parenthesizedExpression: return try parenthesized(node)
        case .sendExpression:
            var c = cursor(node)
            let start = try c.advance()
            return try sendBody(&c, publish: start.kind == .publish, start: start)
        default: throw Mismatch(token: node.firstToken)
        }
    }

    func guardedChoice(_ choice: GesSyntaxNode) throws -> GesExpression {
        var c = cursor(choice)
        var branches: [(GesExpression, GesExpression)] = []
        if c.is(.when) {
            let start = try c.advance()
            while true {
                try c.newlines()
                var b = cursor(try c.takeNode(.choiceBranch))
                let condition = try expression(&b)
                try b.newlines()
                try b.expectWord("then")
                branches.append((try expression(&b), condition))
                if !(try c.match(.comma)) { break }
                try c.newlines()
                if c.is(.otherwise) { break }
                _ = try c.match(.operatorOr)
                try c.newlines()
                try c.expect(.when)
            }
            try c.newlines()
            try c.expect(.otherwise)
            let fallback = try expression(&c)
            return .init(.choice(branches, fallback), location(start, c.previous))
        }
        var b = cursor(try c.takeNode(.choiceBranch))
        let first = try expression(&b)
        try b.expect(.when)
        branches.append((first, try expression(&b)))
        while try c.match(.comma) {
            try c.newlines()
            if c.is(.otherwise) { break }
            _ = try c.match(.operatorOr)
            try c.newlines()
            var next = cursor(try c.takeNode(.choiceBranch))
            let value = try expression(&next)
            try next.expect(.when)
            branches.append((value, try expression(&next)))
        }
        try c.newlines()
        try c.expect(.otherwise)
        let fallback = try expression(&c)
        return combined(.choice(branches, fallback), first, fallback, c.previous)
    }

    func binary(_ binary: GesSyntaxNode) throws -> GesExpression {
        var c = cursor(binary)
        let left = try expression(&c)
        if c.is(.superscriptInteger) {
            let token = try c.advance()
            let exponent = node(.literal(.integer(Int64(token.text)!)), token, token)
            return combined(.binary(.power, left, exponent), left, exponent, c.previous)
        }
        // Implicit multiplication, such as `2pi`, has no operator token.
        guard c.isOwnToken, let operation = GesBinaryOperator(c.current.kind) else {
            let right = try expression(&c)
            return combined(.binary(.multiply, left, right), left, right, c.previous)
        }
        try c.advance()
        try c.newlines()
        let right = try expression(&c)
        return combined(.binary(operation, left, right), left, right, c.previous)
    }

    func membership(_ membership: GesSyntaxNode) throws -> GesExpression {
        var c = cursor(membership)
        let left = try expression(&c)
        let op = try c.advance().kind
        try c.newlines()
        var operation = GesBinaryOperator.contains
        switch op {
        case .has:
            try c.expectWord("value")
            return ranged(.unary(.hasValue, left), left)
        case .starts, .ends:
            try c.expect(.with)
            operation = op == .starts ? .startsWith : .endsWith
            try c.newlines()
        case .in where c.isWord("values") && c.peekSignificant(1).kind == .of:
            try c.advance()
            try c.newlines()
            try c.expect(.of)
            operation = .containsValue
        default: break
        }
        let right = try expression(&c)
        let result = combined(.binary(operation, left, right), left, right, c.previous)
        return op == .notIn ? ranged(.unary(.not, result), result) : result
    }

    func typeOperation(_ operation: GesSyntaxNode) throws -> GesExpression {
        var c = cursor(operation)
        if c.is(.is) {
            // `is name` without a subject tests the handler's message.
            let start = try c.advance()
            try c.newlines()
            let negated = try c.match(.operatorNot)
            try c.newlines()
            let name = try c.identifier()
            let value = node(.predicate(nil, name), start, c.previous)
            return negated ? node(.unary(.not, value), start, c.previous) : value
        }
        let subject = try expression(&c)
        let op = try c.advance().kind
        try c.newlines()
        if op == .as { return ranged(.cast(subject, try typeName(c.takeNode(.typeReference))), subject) }
        let negated = try c.match(.operatorNot)
        try c.newlines()
        let value = try isTest(&c, subject)
        return negated ? ranged(.unary(.not, value), value) : value
    }

    func isTest(_ c: inout Cursor, _ input: GesExpression) throws -> GesExpression {
        if c.isNode(.typeReference) { return ranged(.check(input, try typeName(c.takeNode())), input) }
        if c.is(.numeric) || c.is(.nothing) || c.isWord("integer") || c.isWord("fractional") { return ranged(.check(input, try c.advance().text), input) }
        if try c.match(.empty) { return ranged(.unary(.empty, input), input) }
        if c.isOwnToken && c.current.kind.isSelector && c.peekSignificant(1).kind == .dot {
            let (ns, function) = try extensionSymbol(&c)
            return ranged(.unary(.predicate, .init(.extensionCall(ns, function, [.init(label: "_", value: input)]), input.location)), input)
        }
        if try c.matchWord("between") {
            try c.newlines()
            let minimum = try expression(&c)
            try c.newlines()
            try c.expect(.operatorAnd)
            try c.newlines()
            let maximum = try expression(&c)
            return combined(.between(input, minimum, maximum), input, maximum, c.previous)
        }
        if try c.matchWord("at") {
            let op: GesBinaryOperator = try c.advance().text == "least" ? .greaterOrEqual : .lessOrEqual
            let right = try expression(&c)
            return combined(.binary(op, input, right), input, right, c.previous)
        }
        if c.isWord("less") || c.isWord("more") {
            let op: GesBinaryOperator = try c.advance().text == "less" ? .less : .greater
            try c.expectWord("than")
            let right = try expression(&c)
            return combined(.binary(op, input, right), input, right, c.previous)
        }
        if c.is(.identifier) { return ranged(.predicate(input, try c.advance().text), input) }
        // `is value`, optionally followed by `or less` or `or more`.
        let right = try expression(&c)
        try c.newlines()
        var op = GesBinaryOperator.equal
        if try c.match(.operatorOr) {
            try c.newlines()
            op = try c.matchWord("less") ? .lessOrEqual : .greaterOrEqual
            if op == .greaterOrEqual { try c.expectWord("more") }
        }
        return combined(.binary(op, input, right), input, right, c.previous)
    }

    func extensionSymbol(_ c: inout Cursor) throws -> (String, String) {
        let namespace = String(try c.advance().text.dropFirst())
        try c.newlines()
        try c.expect(.dot)
        try c.newlines()
        return (namespace, try c.advance().text)
    }

    func unary(_ unary: GesSyntaxNode) throws -> GesExpression {
        var c = cursor(unary)
        let start = c.current
        if Self.prefixOperators.contains(start.kind) {
            let op = try c.advance().kind
            // `strict` directly after `parse` is the mode word.
            var strict = false
            if op == .keywordParse { strict = try c.matchWord("strict") }
            let operation: GesUnaryOperator = op == .operatorMinus ? .negate : op == .keywordParse ? (strict ? .parseStrict : .parse) : op == .empty ? .empty : .not
            let value = try expression(&c)
            // From the operator to the last consumed token.
            return .init(.unary(operation, value), location(start, c.previous))
        }
        var op = try c.advance().text
        try c.newlines()
        if op == "wrap" {
            try c.expectWord("degree")
            op = "wrapDegree"
        }
        if op == "round" {
            try c.expectWord("half")
            try c.newlines()
            let mode = try c.advance().text
            op = "roundHalf" + mode.prefix(1).uppercased() + mode.dropFirst()
        }
        let value: GesExpression
        if try c.match(.leftParen) {
            // A parenthesized operand belongs to the intrinsic: its parentheses add no depth.
            value = try expression(&c)
            try c.newlines()
            try c.expect(.rightParen)
        } else {
            value = try expression(&c)
        }
        if op == "sqrt" || op == "cbrt" {
            let exponent = node(.literal(.float(op == "sqrt" ? 0.5 : 1.0 / 3)), start, c.previous)
            return .init(.binary(.power, value, exponent), location(start, c.previous))
        }
        guard let operation = GesUnaryOperator(prefixText: op) else { throw Mismatch(token: unary.firstToken) }
        return node(.unary(operation, value), start, c.previous)
    }

    func intrinsicCall(_ call: GesSyntaxNode) throws -> GesExpression {
        var c = cursor(call)
        let start = c.current
        var op = try c.advance().text
        try c.newlines()
        if op == "distance", try c.match(.intrinsicSquared) { op = "distanceSquared" }
        if op == "length" {
            try c.expect(.intrinsicSquared)
            op = "lengthSquared"
        }
        if op == "angle" {
            try c.expectWord("between")
            op = "angleBetween"
        }
        try c.newlines()
        try c.expect(.leftParen)
        try c.newlines()
        var values: [GesExpression] = []
        if !(try c.match(.rightParen)) {
            while true {
                values.append(try expression(&c))
                try c.newlines()
                if try c.match(.comma) {
                    try c.newlines()
                    continue
                }
                try c.expect(.rightParen)
                break
            }
        }
        return node(.intrinsic(op, values), start, c.previous)
    }

    func clamp(_ clamp: GesSyntaxNode) throws -> GesExpression {
        var c = cursor(clamp)
        let start = try c.advance()
        let value = try expression(&c)
        try c.newlines()
        try c.expectWord("between")
        let low = try expression(&c)
        try c.expect(.operatorAnd)
        let high = try expression(&c)
        return node(.intrinsic("clamp", [value, low, high]), start, c.previous)
    }

    func variadic(_ variadic: GesSyntaxNode) throws -> GesExpression {
        var c = cursor(variadic)
        let start = try c.advance()
        try c.newlines()
        try c.expect(.of)
        var arguments = [try expression(&c)]
        while try c.match(.operatorAnd) { arguments.append(try expression(&c)) }
        return node(.intrinsic(start.text, arguments), start, c.previous)
    }

    func extensionCall(_ call: GesSyntaxNode) throws -> GesExpression {
        var c = cursor(call)
        let start = c.current
        let (ns, function) = try extensionSymbol(&c)
        try c.newlines()
        if c.is(.leftParen) { return node(.extensionCall(ns, function, try arguments(&c)), start, c.previous) }
        var operands: [GesArgument] = []
        if try c.match(.of) {
            operands = [.init(label: "_", value: try expression(&c))]
            while try c.match(.operatorAnd) { operands.append(.init(label: "_", value: try expression(&c))) }
        } else if c.isNode(.argument) {
            repeat { operands.append(try argument(c.takeNode())) } while c.isNode(.argument)
        } else if !c.atEnd {
            operands = [.init(label: "_", value: try expression(&c))]
        }
        return node(.extensionCall(ns, function, operands), start, c.previous)
    }

    func arguments(_ c: inout Cursor) throws -> [GesArgument] {
        try c.expect(.leftParen)
        try c.newlines()
        return try argumentsAfterOpening(&c)
    }

    func argumentsAfterOpening(_ c: inout Cursor) throws -> [GesArgument] {
        var result: [GesArgument] = []
        if try c.match(.rightParen) { return result }
        while true {
            result.append(try argument(c.takeNode(.argument)))
            try c.newlines()
            if try c.match(.comma) {
                try c.newlines()
                continue
            }
            if c.isNode(.argument) { continue }
            try c.expect(.rightParen)
            return result
        }
    }

    func argument(_ argument: GesSyntaxNode) throws -> GesArgument {
        var c = cursor(argument)
        let start = c.current
        var label = "_"
        if c.isOwnToken, c.current.kind == .identifier || GesSyntaxParser.labelKeywords.contains(c.current.kind), c.peekSignificant(1).kind == .colon {
            label = try c.advance().text
            try c.newlines()
            try c.expect(.colon)
        }
        let value = try expression(&c)
        return .init(label: label, value: value, location: location(start, c.previous))
    }

    func memberAccess(_ access: GesSyntaxNode) throws -> GesExpression {
        var c = cursor(access)
        let target = try expression(&c)
        try c.expect(.dot)
        try c.newlines()
        return ranged(.member(target, try c.identifier()), target)
    }

    func collectionAccess(_ access: GesSyntaxNode) throws -> GesExpression {
        var c = cursor(access)
        // A combined selector without a source, such as `[:cartesian a, b]`, starts with its bracket.
        let target = c.is(.leftBracket) ? nil : try expression(&c)
        try c.expect(.leftBracket)
        try c.newlines()
        let result = try pipelineSteps(&c, target)
        try c.newlines()
        try c.expect(.rightBracket)
        return result
    }

    func typeConstructor(_ constructor: GesSyntaxNode) throws -> GesExpression {
        var c = cursor(constructor)
        let start = c.current
        let type = try typeName(c.takeNode(.typeReference))
        try c.newlines()
        try c.expect(.leftParen)
        try c.newlines()
        if type == "series" && (c.is(.fibonacci) || c.is(.factorial)) {
            let kind = try c.advance()
            // The series name, the handler literal and the tag list of these data forms have no range.
            var args: [GesArgument] = [.init(label: "_", value: unranged(.literal(.text(kind.text)), kind, c.previous))]
            if try c.match(.comma) {
                repeat {
                    try c.newlines()
                    args.append(try argument(c.takeNode(.argument)))
                    try c.newlines()
                } while try c.match(.comma)
            }
            try c.expect(.rightParen)
            return node(.constructor(type, args), start, c.previous)
        }
        if type == "handler" && c.is(.message) {
            let name = try c.advance()
            try c.expect(.leftParen)
            try c.newlines()
            var parameters: [GesParameter] = []
            if !c.is(.rightParen) {
                repeat {
                    try c.newlines()
                    let token = c.current
                    let label = try c.match(.underscore) ? "_" : try c.identifier()
                    parameters.append(.init(label: label, name: label == "_" ? "literal_" + String(parameters.count) : label, type: nil, location: location(token)))
                    try c.newlines()
                } while try c.match(.comma)
            }
            try c.expect(.rightParen)
            try c.newlines()
            try c.expect(.rightParen)
            return node(.constructor(type, [.init(label: "_", value: unranged(.handler(name.text, parameters), name, c.previous))]), start, c.previous)
        }
        if type == "message" && c.isNode(.messageExpression) {
            var args: [GesArgument] = [.init(label: "_", value: try messageLiteral(c.takeNode()))]
            if c.isNode(.tagClause) {
                var t = cursor(try c.takeNode())
                try t.expect(.with)
                var tags = [try expression(&t)]
                while try t.match(.comma) { tags.append(try expression(&t)) }
                args.append(.init(label: "tags", value: unranged(.list(tags), start, c.previous)))
            }
            try c.expect(.rightParen)
            return node(.constructor(type, args), start, c.previous)
        }
        return node(.constructor(type, try argumentsAfterOpening(&c)), start, c.previous)
    }

    func diceLiteral(_ literal: GesSyntaxNode) throws -> GesExpression {
        var c = cursor(literal)
        let start = c.current
        let type = try typeNameCore(&c)
        try c.newlines()
        let bracket = try c.expect(.leftBracket)
        try c.newlines()
        var items: [GesExpression] = []
        if !c.is(.rightBracket) {
            repeat {
                try c.newlines()
                let token = c.current
                items.append(node(.literal(.integer(Int64(try positiveInteger(&c)))), token, c.previous))
                try c.newlines()
            } while try c.match(.comma)
        }
        try c.expect(.rightBracket)
        return node(.constructor(type, [.init(label: "_", value: node(.list(items), bracket, c.previous))]), start, c.previous)
    }

    func collectionFactory(_ factory: GesSyntaxNode) throws -> GesExpression {
        var c = cursor(factory)
        let type = try typeNameCore(&c)
        try c.newlines()
        try c.expect(.leftBracket)
        try c.newlines()
        let selectToken = try c.expect(.selectorSelect)
        try c.newlines()
        let name = try c.identifier()
        try c.newlines()
        let range = c.isNode(.rangeExpression)
        let sequence: GesExpression
        if range {
            var r = cursor(try c.takeNode())
            let from = try r.advance()
            sequence = try rangeExpression(&r, from)
        } else {
            try c.expect(.in)
            try c.newlines()
            sequence = try expression(&c)
        }
        try c.newlines()
        var condition: GesExpression?
        if try c.matchWord("where") { condition = try expression(&c) }
        try c.newlines()
        try c.expect(.projectionArrow)
        let value = try expression(&c)
        let generated = node(.generated(type, name, sequence, range, condition, value), selectToken, c.previous)
        try c.newlines()
        try c.expect(.rightBracket)
        return generated
    }

    func rangeExpression(_ c: inout Cursor, _ start: GesToken) throws -> GesExpression {
        let from = try expression(&c)
        try c.newlines()
        try c.expect(.to)
        let to = try expression(&c)
        let step = try c.matchWord("step") ? try expression(&c) : nil
        return node(.range(from, to, step), start, c.previous)
    }

    func random(_ random: GesSyntaxNode) throws -> GesExpression {
        var c = cursor(random)
        let start = try c.advance()
        try c.newlines()
        _ = try c.matchWord("from")
        let from = try expression(&c)
        try c.newlines()
        try c.expect(.to)
        let to = try expression(&c)
        return node(.random(from, to), start, c.previous)
    }

    func seededRandom(_ random: GesSyntaxNode) throws -> GesExpression {
        var c = cursor(random)
        try c.advance()
        try c.newlines()
        // From `with`, as the parser starts the seeded random expression.
        let withToken = try c.expect(.with)
        let seed = try expression(&c)
        let body = try expression(&c)
        return node(.seeded(seed, body), withToken, c.previous)
    }

    func series(_ series: GesSyntaxNode) throws -> GesExpression {
        var c = cursor(series)
        let start = try c.advance()
        try c.newlines()
        return node(.series(try c.advance().text), start, c.previous)
    }

    func dice(_ dice: GesSyntaxNode) throws -> GesExpression {
        var c = cursor(dice)
        try c.advance()
        try c.newlines()
        try c.expectWord("dice")
        let start = c.previous
        try c.newlines()
        let count = try positiveInteger(&c)
        try c.newlines()
        let sides: Int
        if try c.matchWord("d") {
            sides = try positiveInteger(&c)
        } else {
            guard let value = Int(try c.advance().text.dropFirst()) else { throw Mismatch(token: c.position) }
            sides = value
        }
        return node(.dice(count, sides), start, c.previous)
    }

    func positiveInteger(_ c: inout Cursor) throws -> Int {
        guard c.is(.float), let value = GesCompilerSupport.number(c.current.text)?.integerValue else { throw Mismatch(token: c.position) }
        try c.advance()
        return Int(value)
    }

    // A list, map or object pattern literal from its opening bracket.
    func collection(_ literal: GesSyntaxNode) throws -> GesExpression {
        var c = cursor(literal)
        let start = try c.expect(.leftBracket)
        try c.newlines()
        if try c.match(.colon) {
            try c.newlines()
            try c.expect(.rightBracket)
            return node(.map([]), start, c.previous)
        }
        if try c.match(.rightBracket) { return node(.list([]), start, c.previous) }
        // A non-empty literal starts at the last token before its first entry, as in the parser.
        let literalStart = c.previous
        if c.isNode(.mapEntry) || c.isNode(.objectPatternEntry) {
            var pairs: [(String, GesExpression)] = []
            repeat {
                try c.newlines()
                var e = cursor(try c.takeNode())
                let token = e.current
                let key = try e.identifier()
                try e.expect(.colon)
                try e.newlines()
                let value = e.atEnd ? node(.literal(.boolean(true)), token, e.previous) : try expression(&e)
                pairs.append((key, value))
                try c.newlines()
            } while try c.match(.comma)
            try c.expect(.rightBracket)
            return node(.map(pairs), literalStart, c.previous)
        }
        var items: [GesExpression] = []
        repeat {
            items.append(try expression(&c))
            try c.newlines()
        } while try c.match(.comma)
        try c.expect(.rightBracket)
        return node(.list(items), literalStart, c.previous)
    }

    // A message literal; a send statement's literal starts at its emit or publish keyword.
    func messageLiteral(_ message: GesSyntaxNode, start: GesToken? = nil) throws -> GesExpression {
        var c = cursor(message)
        let name = try c.expect(.message)
        try c.newlines()
        let args = c.is(.leftParen) ? try arguments(&c) : []
        return node(.message(name.text, args), start ?? name, c.previous)
    }

    func call(_ call: GesSyntaxNode) throws -> GesExpression {
        var c = cursor(call)
        let start = try c.expect(.identifier)
        try c.newlines()
        return node(.call(start.text, try arguments(&c)), start, c.previous)
    }

    func handlerLiteral(_ handler: GesSyntaxNode) throws -> GesExpression {
        var c = cursor(handler)
        let start = try c.expect(.message)
        try c.newlines()
        return node(.handler(start.text, try parameters(&c, allowType: false)), start, c.previous)
    }

    // Parentheses leave no semantic node; the enclosed expression keeps its own range.
    func parenthesized(_ parenthesized: GesSyntaxNode) throws -> GesExpression {
        var c = cursor(parenthesized)
        try c.expect(.leftParen)
        let result = try expression(&c)
        try c.newlines()
        try c.expect(.rightParen)
        return result
    }

    // The part of a send after its emit or publish keyword; a delayed send statement owns that keyword.
    func sendBody(_ c: inout Cursor, publish: Bool, start: GesToken) throws -> GesExpression {
        try c.newlines()
        let delay = try c.matchWord("after") ? try expression(&c) : nil
        let message = try expression(&c)
        var tags: [GesExpression] = []
        if c.isNode(.tagClause) {
            var t = cursor(try c.takeNode())
            try t.expect(.with)
            repeat { tags.append(try expression(&t)) } while try t.match(.comma)
        }
        return node(.send(publish, message, tags, delay), start, c.previous)
    }

    func bindSend(_ send: GesSyntaxNode, publish: Bool, start: GesToken) throws -> GesExpression {
        var c = cursor(send)
        return try sendBody(&c, publish: publish, start: start)
    }

    // Each pipeline step is one selector node that extends the expression of the steps before it.
    func pipelineSteps(_ c: inout Cursor, _ base: GesExpression?) throws -> GesExpression {
        var result = base
        repeat {
            try c.newlines()
            var s = cursor(try c.takeNode(.collectionSelector))
            let start = s.current
            if s.isOwnToken && start.kind == .selector && Self.combinedSelectors.contains(start.text) {
                let operation = String(try s.advance().text.dropFirst())
                try s.newlines()
                var sources = result.map { [$0] } ?? []
                sources.append(try expression(&s))
                while try s.match(.comma) {
                    try s.newlines()
                    sources.append(try expression(&s))
                }
                result = node(.combined(operation == "zip" ? "lockstep" : operation, sources), start, s.previous)
            } else if try s.matchSelector(":split") {
                guard let input = result else { throw Mismatch(token: s.position) }
                try s.newlines()
                try s.expect(.on)
                try s.newlines()
                let whitespace = try s.matchWord("whitespace")
                let delimiter = whitespace ? node(.literal(.nothing), s.previous, s.previous) : try expression(&s)
                result = ranged(.constructor(whitespace ? "__splitWhitespace" : "__split", whitespace ? [.init(label: "_", value: input)] : [.init(label: "_", value: input), .init(label: "_", value: delimiter)]), input)
            } else {
                guard let input = result else { throw Mismatch(token: s.position) }
                let selector = try self.selector(&s)
                let access = combined(.selector(input, selector), input, nil, s.previous)
                // The object-pattern selector has no range, which leaves the whole access without one.
                result = selector.operation == "objectMatch" ? GesExpression(access.kind, GesAstOptimizer.noRange(access.location)) : access
            }
            try c.newlines()
        } while c.isNode(.collectionSelector)
        return result!
    }

    func selector(_ c: inout Cursor) throws -> GesSelector {
        let start = c.current
        if !(c.isOwnToken && start.kind.isSelector) { return .init(operation: "index", expressions: [try expression(&c)]) }
        let op = String(try c.advance().text.dropFirst())
        try c.newlines()
        var s = GesSelector(operation: op)
        let ends = c.atEnd
        switch op {
        case "keys", "values", "entries", "shuffle", "reverse": break
        case "any", "all", "filter": try bindingClause(&c, &s, word: "where")
        case "count", "sum", "average": if !ends { try bindingClause(&c, &s, word: op == "count" ? "where" : nil) }
        case "fold", "reduce":
            s.accumulator = try c.identifier()
            if op == "fold" {
                try c.expect(.be)
                s.expressions.append(try expression(&c))
            }
            try c.expect(.comma)
            try c.newlines()
            s.names = try bindingNames(&c)
            s.name = s.names[0]
            try c.expect(.projectionArrow)
            try c.newlines()
            s.expressions.append(try expression(&c))
        case "foreach", "select", "min", "max", "highest", "lowest": try bindingClause(&c, &s, word: nil)
        case "first", "last", "single": if !ends { try bindingClause(&c, &s, word: "where") }
        case "map", "group", "order": try keyClause(&c, &s, op)
        case "distinct": if !ends { try keyClause(&c, &s, op) }
        case "sort": s.mode = try direction(&c)
        case "contains":
            if c.isWord("all") || c.isWord("any") { s.mode = try c.advance().text }
            s.expressions = [try expression(&c)]
        case "term": s.expressions = [try expression(&c)]
        case "draw": s.count = try positiveInteger(&c)
        case "choose": try chooseClauses(&c, &s, start)
        case "take", "drop":
            if c.isWord("first") || c.isWord("last") || c.isWord("highest") || c.isWord("lowest") {
                s.mode = try c.advance().text
                s.count = try positiveInteger(&c)
            } else {
                s = try pattern(c.takeNode(.dicePattern), operation: "takePattern")
            }
        case "has":
            if c.isNode(.objectPattern) {
                s.operation = "objectMatch"
                s.expressions = [try collection(c.takeNode())]
            } else {
                s = try pattern(c.takeNode(.dicePattern), operation: "hasPattern")
            }
        default: throw Mismatch(token: c.position)
        }
        if op == "highest" { s.operation = "max" }
        if op == "lowest" { s.operation = "min" }
        s.location = location(start, c.previous)
        return s
    }

    func bindingClause(_ c: inout Cursor, _ s: inout GesSelector, word: String?) throws {
        s.names = try bindingNames(&c)
        s.name = s.names[0]
        if let word { try c.expectWord(word) } else { try c.expect(.projectionArrow) }
        s.expressions = [try expression(&c)]
    }

    func keyClause(_ c: inout Cursor, _ s: inout GesSelector, _ op: String) throws {
        if op == "map" {
            s.names = try bindingNames(&c)
            s.name = s.names[0]
            try c.expectWord("by")
        } else {
            try c.expectWord("by")
            s.names = try bindingNames(&c)
            s.name = s.names[0]
            try c.expect(.projectionArrow)
        }
        s.expressions = [try expression(&c)]
        if op == "map", try c.match(.projectionArrow) { s.expressions.append(try expression(&c)) }
        if op == "order" { s.mode = try direction(&c) }
    }

    func direction(_ c: inout Cursor) throws -> String {
        try c.newlines()
        return try c.advance().text
    }

    func chooseClauses(_ c: inout Cursor, _ s: inout GesSelector, _ start: GesToken) throws {
        s.name = ""
        s.count = try positiveInteger(&c)
        try c.newlines()
        if try c.matchWord("at") {
            try c.expect(.keywordRandom)
            s.mode = "random"
        }
        try c.newlines()
        if c.isOwnToken && c.current.isWord && (c.peekSignificant(1).isIdentifier("where") || c.peekSignificant(1).kind == .comma) {
            s.names = try bindingNames(&c)
            s.name = s.names[0]
            try c.expectWord("where")
            s.expressions = [try expression(&c)]
        }
        try c.newlines()
        if try c.matchWord("weighted") {
            try c.expectWord("by")
            s.weightNames = try bindingNames(&c)
            s.weightName = s.weightNames[0]
            try c.expect(.projectionArrow)
            if s.expressions.isEmpty { s.expressions = [node(.literal(.boolean(true)), start, c.previous)] }
            s.expressions.append(try expression(&c))
        }
    }

    func pattern(_ pattern: GesSyntaxNode, operation: String) throws -> GesSelector {
        var c = cursor(pattern)
        var s = GesSelector(operation: operation)
        if try c.matchWord("full") {
            try c.expectWord("house")
            s.mode = "fullHouse"
            return s
        }
        if try c.matchWord("straight") {
            s.mode = "straight"
            return s
        }
        let token = try c.advance()
        s.count = ["pair": 2, "three": 3, "four": 4, "five": 5, "six": 6, "seven": 7][token.text] ?? 0
        s.mode = "kind"
        try c.newlines()
        if token.text != "pair" { try c.expect(.of) } else if !(try c.match(.of)) { return s }
        if try c.matchWord("a") {
            try c.expectWord("kind")
        } else {
            s.mode = "face"
            s.expressions = [try expression(&c)]
        }
        return s
    }
}
