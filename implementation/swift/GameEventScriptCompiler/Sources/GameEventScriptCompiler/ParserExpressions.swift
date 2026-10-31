// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

@_spi(Compiler) import GameEventScriptRuntime
@_spi(Syntax) import GameEventScriptSyntax

extension GesParser {
    static let prefixOperators: Set<GesTokenKind> = [.operatorMinus, .operatorNot, .keywordParse, .empty]

    static let unaryIntrinsics: Set<GesTokenKind> = [
        .intrinsicAbs, .intrinsicLn, .intrinsicExp, .intrinsicSqrt, .intrinsicCbrt, .intrinsicChance, .intrinsicFloor, .intrinsicCeil, .intrinsicTruncate, .intrinsicRad, .intrinsicDeg,
        .intrinsicSin, .intrinsicCos, .intrinsicTan, .intrinsicAsin, .intrinsicAcos, .intrinsicAtan, .intrinsicWrap, .intrinsicRound,
    ]

    // Intrinsics that take a parenthesized, positional argument list.
    static let listIntrinsics: Set<GesTokenKind> = [.intrinsicAtan2, .intrinsicHypot, .intrinsicDistance, .intrinsicLength, .intrinsicNormalize, .intrinsicDot, .intrinsicCross, .intrinsicAngle]

    static let mathConstants: [GesTokenKind: Double] = [.mathConstantPi: Double.pi, .mathConstantE: 2.718281828459045, .mathConstantTau: Double.pi * 2, .mathConstantInfinity: Double.infinity]

    // Tokens besides numbers and names that start the single operand of an unparenthesized extension call.
    static let extensionOperandKinds: Set<GesTokenKind> = [
        .text, .tag, .constantReference, .typeName, .operatorMinus, .operatorNot, .leftBracket, .`true`, .`false`, .nothing, .keywordParse, .keywordRandom, .roll, .intrinsicAbs, .intrinsicLn, .intrinsicExp,
        .intrinsicSqrt, .intrinsicCbrt,
    ]

    // Nested expressions recurse through `expression`, `prefix` and `primary`. An unoptimized build reserves stack for
    // every local of a function on each call, so these stay small dispatchers: each form parses in its own function,
    // and work before or after a nested operand that needs many locals runs in a further function of its own.
    func prefix() throws -> GesExpression {
        let kind = currentKind
        if Self.prefixOperators.contains(kind) { return try prefixOperation() }
        if Self.unaryIntrinsics.contains(kind) { return try unaryIntrinsic() }
        if Self.listIntrinsics.contains(kind) { return try listIntrinsic() }
        if kind == .clamp { return try clampIntrinsic() }
        if kind == .min || kind == .max, peekKind() == .of { return try variadicIntrinsic() }
        if isExtensionCallStart() { return try extensionCall() }
        let start = mark()
        return try postfixSuffixes(primary(), start)
    }

    func prefixOperation() throws -> GesExpression {
        startNode(.unaryExpression)
        let start = current
        let op = prefixOperator()
        let value = try expression(15)
        // From the operator to the last consumed token, as C# WithRange(node, opToken, Previous).
        return finishNode(try combined(.unary(op, value), node(.literal(.nothing), start)))
    }

    func prefixOperator() -> GesUnaryOperator {
        let op = advance().kind
        // `strict` directly after `parse` on the same line is always the mode word, never an operand.
        let strict = op == .keywordParse && currentKind == .identifier && current.text == "strict"
        if strict { advance() }
        return op == .operatorMinus ? .negate : op == .keywordParse ? (strict ? .parseStrict : .parse) : op == .empty ? .empty : .not
    }

    func unaryIntrinsic() throws -> GesExpression {
        startNode(.unaryExpression)
        let start = current
        let op = try unaryIntrinsicName()
        // A parenthesized operand belongs to the intrinsic, as in C#: its parentheses add no depth and end the operand.
        let value = currentKind == .leftParen ? try intrinsicParenthesizedOperand() : try expression(15)
        return finishNode(try unaryIntrinsic(op, value, start))
    }

    func intrinsicParenthesizedOperand() throws -> GesExpression {
        advance()
        let value = try expression()
        newlines()
        try expect(.rightParen)
        return value
    }

    // The intrinsic word including its mode words, such as `round half even` as roundHalfEven.
    func unaryIntrinsicName() throws -> String {
        var op = advance().text
        newlines()
        if op == "wrap" {
            try expectWord("degree")
            op = "wrapDegree"
        }
        if op == "round" {
            try expectWord("half")
            newlines()
            let token = advance()
            let mode = token.text
            guard token.kind == .identifier, ["even", "up", "down"].contains(mode) else { throw failure("Expected rounding mode.", previous) }
            op = "roundHalf" + mode.prefix(1).uppercased() + mode.dropFirst()
        }
        return op
    }

    func unaryIntrinsic(_ op: String, _ value: GesExpression, _ start: GesToken) throws -> GesExpression {
        // The root power spans from the operator, as C# CreateRootPowerExpression ranges it.
        if op == "sqrt" || op == "cbrt" { return try combined(.binary(.power, value, node(.literal(.float(op == "sqrt" ? 0.5 : 1.0 / 3)), start)), node(.literal(.nothing), start)) }
        guard let unary = GesUnaryOperator(prefixText: op) else { throw failure("Expected an operator.", start) }
        return node(.unary(unary, value), start)
    }

    func listIntrinsic() throws -> GesExpression {
        startNode(.intrinsicCallExpression)
        let start = current
        let op = try listIntrinsicName()
        return finishNode(try positionalIntrinsic(op, arguments(nodes: false), start))
    }

    func listIntrinsicName() throws -> String {
        var op = advance().text
        newlines()
        if op == "distance" && match(.intrinsicSquared) { op = "distanceSquared" }
        if op == "length" {
            try expect(.intrinsicSquared)
            op = "lengthSquared"
        }
        if op == "angle" {
            try expectWord("between")
            op = "angleBetween"
        }
        newlines()
        return op
    }

    func positionalIntrinsic(_ op: String, _ args: [GesArgument], _ start: GesToken) throws -> GesExpression {
        if args.contains(where: { $0.label != "_" }) { throw failure("Intrinsic arguments are positional.", start) }
        return node(.intrinsic(op, args.map(\.value)), start)
    }

    func clampIntrinsic() throws -> GesExpression {
        startNode(.clampExpression)
        defer { finishNode() }
        let start = advance()
        let value = try expression(15)
        newlines()
        try expectWord("between")
        let low = try expression(9)
        try expect(.operatorAnd)
        let high = try expression(9)
        return node(.intrinsic("clamp", [value, low, high]), start)
    }

    func variadicIntrinsic() throws -> GesExpression {
        startNode(.variadicExpression)
        defer { finishNode() }
        let start = current
        let op = advance().text
        newlines()
        try expect(.of)
        var args = [try expression(9)]
        while !isForClauseStart() && match(.operatorAnd) { args.append(try expression(9)) }
        return node(.intrinsic(op, args), start)
    }

    func extensionCall() throws -> GesExpression {
        let checkpoint = mark()
        let start = current
        let (ns, function) = try extensionSymbol()
        newlines()
        if currentKind == .leftParen { return try postfixSuffixes(wrap(checkpoint, .extensionCallExpression, node(.extensionCall(ns, function, arguments()), start)), checkpoint) }
        return wrap(checkpoint, .extensionCallExpression, node(.extensionCall(ns, function, try extensionOperands()), start))
    }

    // The operands of an unparenthesized extension call: `of` operands, labeled arguments or one operand.
    func extensionOperands() throws -> [GesArgument] {
        if match(.of) {
            var args: [GesArgument] = [.init(label: "_", value: try expression(9))]
            while !isForClauseStart() && match(.operatorAnd) { args.append(.init(label: "_", value: try expression(9))) }
            return args
        }
        if argumentLabel() {
            var args: [GesArgument] = []
            repeat { args.append(try argument()) } while argumentLabel()
            return args
        }
        return isExtensionOperandStart() ? [.init(label: "_", value: try expression(15))] : []
    }

    func isExtensionOperandStart() -> Bool {
        !(choiceConditionDepth == delimiterDepth && current.isIdentifier("then")) && (current.isNumber || current.isName || Self.extensionOperandKinds.contains(currentKind))
    }

    func postfixSuffixes(_ base: GesExpression, _ start: GesSyntaxTreeBuilder.Checkpoint) throws -> GesExpression {
        var result = base
        while true {
            if match(.dot) {
                startNode(at: start, .memberAccessExpression)
                newlines()
                result = finishNode(try ranged(.member(result, identifier()), result))
            } else if match(.leftBracket) {
                startNode(at: start, .collectionAccessExpression)
                newlines()
                result = try pipelineSteps(result)
                newlines()
                try expect(.rightBracket)
                finishNode()
            } else {
                break
            }
        }
        return result
    }

    func sendExpression(_ publish: Bool, _ start: GesToken, _ checkpoint: GesSyntaxTreeBuilder.Checkpoint) throws -> GesExpression {
        startNode(at: checkpoint, .sendExpression)
        newlines()
        let delay = matchWord("after") ? try expression() : nil
        let message: GesExpression
        if currentKind == .message {
            newlines()
            startNode(.messageExpression)
            let token = advance()
            let args = currentKind == .leftParen ? try arguments() : []
            message = finishNode(node(.message(token.text, args), token))
        } else {
            message = try expression(15)
        }
        var tags: [GesExpression] = []
        if currentKind == .with {
            startNode(.tagClause)
            advance()
            repeat { tags.append(try expression(15)) } while match(.comma)
            finishNode()
        }
        return finishNode(node(.send(publish, message, tags, delay), start))
    }

    func primary() throws -> GesExpression {
        switch currentKind {
        case .`is`: return try predicateReference()
        case .emit, .publish:
            // The send node of an expression includes its emit or publish keyword.
            let checkpoint = mark()
            let start = advance()
            return try sendExpression(start.kind == .publish, start, checkpoint)
        case .text, .tag, .constantReference, .`true`, .`false`, .nothing, .mathConstantPi, .mathConstantE, .mathConstantTau, .mathConstantInfinity: return try scalarLiteral()
        case .leftParen: return try parenthesized()
        case .leftBracket:
            let checkpoint = mark()
            return try collection(advance(), checkpoint)
        case .keywordRandom: return try randomExpression()
        case .series: return try seriesExpression()
        case .roll: return try diceRoll()
        case .typeName: return try typeExpression()
        case .identifier where current.isIdentifier("from"):
            startNode(.rangeExpression)
            return finishNode(try rangeExpression(advance()))
        case .identifier, .message: return try nameExpression()
        default:
            if current.isNumber { return try numberLiteral() }
            throw unexpectedToken(current)
        }
    }

    func unexpectedToken(_ token: GesToken) -> GesCompileError { failure("Unexpected token '\(token.text)'.", token) }

    // `is name` or `is not name`: a predicate of the implicit subject.
    func predicateReference() throws -> GesExpression {
        startNode(.typeOperationExpression)
        defer { finishNode() }
        let start = advance()
        newlines()
        let negated = match(.operatorNot)
        newlines()
        let name = try identifier()
        let value = node(.predicate(nil, name), start)
        return negated ? node(.unary(.not, value), start) : value
    }

    func numberLiteral() throws -> GesExpression {
        let token = advance()
        guard let value = GesCompilerSupport.number(token.text, percentage: token.text.hasSuffix("%")) else { throw failure("Invalid numeric literal.", token) }
        let result = node(.literal(value), token)
        result.decimalLiteral = token.text.contains(".") || value.valueKind == .float
        return result
    }

    func scalarLiteral() throws -> GesExpression {
        let token = advance()
        switch token.kind {
        case .text: return node(.literal(.text(token.text)), token)
        case .tag: return node(.literal(try .tag(String(token.text.dropFirst()))), token)
        case .constantReference: return node(.constant(String(token.text.dropFirst())), token)
        case .`true`: return node(.literal(.boolean(true)), token)
        case .`false`: return node(.literal(.boolean(false)), token)
        case .nothing: return node(.literal(.nothing), token)
        default: return node(.literal(.float(Self.mathConstants[token.kind]!)), token)
        }
    }

    func parenthesized() throws -> GesExpression {
        startNode(.parenthesizedExpression)
        defer { finishNode() }
        advance()
        let result = try expression()
        newlines()
        try expect(.rightParen)
        result.depth += 1
        if result.depth > 32 { throw nestingLimit(result.location) }
        return result
    }

    func randomExpression() throws -> GesExpression {
        let checkpoint = mark()
        let start = advance()
        newlines()
        if match(.with) {
            // From `with`, as C# starts the seeded random expression.
            let withToken = previous
            let seed = try expression()
            return wrap(checkpoint, .seededRandomExpression, node(.seeded(seed, try expression()), withToken))
        }
        return wrap(checkpoint, .randomExpression, try randomRange(start))
    }

    func randomRange(_ start: GesToken) throws -> GesExpression {
        _ = matchWord("from")
        let from = try expression(13)
        newlines()
        try expect(.to)
        let to = try expression(13)
        return node(.random(from, to), start)
    }

    func seriesExpression() throws -> GesExpression {
        startNode(.seriesExpression)
        defer { finishNode() }
        let start = advance()
        newlines()
        let token = advance()
        guard token.kind == .fibonacci || token.kind == .factorial else { throw failure("Expected series kind.", previous) }
        return node(.series(token.text), start)
    }

    func diceRoll() throws -> GesExpression {
        startNode(.diceExpression)
        defer { finishNode() }
        advance()
        newlines()
        try expectWord("dice")
        // From `dice`, as C# starts the dice expression.
        let start = previous
        newlines()
        let count = try positiveInteger()
        newlines()
        let sides: Int
        if matchWord("d") {
            sides = try positiveInteger()
        } else {
            let t = advance()
            guard t.text.hasPrefix("d"), let n = Int(t.text.dropFirst()), n > 0 && n <= Int(Int32.max) else { throw failure("Invalid dice sides.", t) }
            sides = n
        }
        return node(.dice(count, sides), start)
    }

    // A type name starts a data form or a constructor call.
    func typeExpression() throws -> GesExpression {
        let start = current
        // `:Dice[...]` and `:List[...]` are data forms that own their type token, as in C#.
        if current.text == ":Dice" || current.text == ":List", peekKind() == .leftBracket {
            startNode(current.text == ":Dice" ? .diceLiteralExpression : .collectionFactoryExpression)
            let type = try typeNameCore()
            newlines()
            advance()
            return finishNode(type == "dice" ? try diceLiteral(type, start) : try generatedList(type))
        }
        startNode(.typeConstructorExpression)
        return finishNode(try typeConstructor(start))
    }

    func typeConstructor(_ start: GesToken) throws -> GesExpression {
        let type = try typeName()
        newlines()
        if type == "series", currentKind == .leftParen, [.fibonacci, .factorial].contains(peekKind()) { return try seriesConstructor(type, start) }
        if type == "handler", currentKind == .leftParen, peekKind() == .message { return try handlerConstructor(type, start) }
        if type == "message", currentKind == .leftParen, peekKind() == .message { return try messageConstructor(type, start) }
        // A type name that starts no data form or constructor is itself the unexpected token, as in C#.
        guard currentKind == .leftParen else { throw unexpectedToken(start) }
        return node(.constructor(type, try arguments()), start)
    }

    func diceLiteral(_ type: String, _ start: GesToken) throws -> GesExpression {
        // The value list spans its brackets, as in C#; the constructor spans the whole literal.
        let bracket = previous
        newlines()
        var items: [GesExpression] = []
        if currentKind != .rightBracket {
            repeat {
                newlines()
                let t = current
                items.append(node(.literal(.integer(Int64(try positiveInteger()))), t))
                newlines()
            } while match(.comma)
        }
        try expect(.rightBracket)
        return node(.constructor(type, [.init(label: "_", value: node(.list(items), bracket))]), start)
    }

    func generatedList(_ type: String) throws -> GesExpression {
        newlines()
        try expect(.selectorSelect)
        let selectToken = previous
        newlines()
        let name = try identifier()
        newlines()
        let range = current.isIdentifier("from")
        let sequence: GesExpression
        if range {
            startNode(.rangeExpression)
            sequence = finishNode(try rangeExpression(advance()))
        } else {
            try expect(.`in`)
            newlines()
            if current.isIdentifier("from") { throw failure("Expected collection.") }
            sequence = try expression()
        }
        newlines()
        var condition: GesExpression?
        if matchWord("where") { condition = try expression() }
        newlines()
        try expect(.projectionArrow)
        let value = try expression()
        // From :select to the projection, as C# ranges the generated collection.
        let generated = node(.generated(type, name, sequence, range, condition, value), selectToken)
        newlines()
        try expect(.rightBracket)
        return generated
    }

    func seriesConstructor(_ type: String, _ start: GesToken) throws -> GesExpression {
        advance()
        newlines()
        let kind = advance()
        // The series name, the handler literal and the tag list of these data forms have no range in C#.
        var args: [GesArgument] = [.init(label: "_", value: unranged(.literal(.text(kind.text)), kind))]
        if match(.comma) {
            repeat {
                newlines()
                args.append(try argument())
                newlines()
            } while match(.comma)
        }
        try expect(.rightParen)
        return node(.constructor(type, args), start)
    }

    func handlerConstructor(_ type: String, _ start: GesToken) throws -> GesExpression {
        advance()
        newlines()
        let name = advance()
        try expect(.leftParen)
        newlines()
        var parameters: [GesParameter] = []
        if currentKind != .rightParen {
            repeat {
                newlines()
                let token = current
                let label = match(.underscore) ? "_" : try identifier()
                parameters.append(.init(label: label, name: label == "_" ? "literal_" + String(parameters.count) : label, type: nil, location: location(token)))
                newlines()
            } while match(.comma)
        }
        try expect(.rightParen)
        newlines()
        try expect(.rightParen)
        return node(.constructor(type, [.init(label: "_", value: unranged(.handler(name.text, parameters), name))]), start)
    }

    func messageConstructor(_ type: String, _ start: GesToken) throws -> GesExpression {
        advance()
        newlines()
        startNode(.messageExpression)
        let name = advance()
        let message = finishNode(node(.message(name.text, try arguments()), name))
        var args: [GesArgument] = [.init(label: "_", value: message)]
        if currentKind == .with {
            startNode(.tagClause)
            advance()
            var tags = [try expression()]
            while match(.comma) { tags.append(try expression()) }
            finishNode()
            args.append(.init(label: "tags", value: unranged(.list(tags), start)))
        }
        try expect(.rightParen)
        return node(.constructor(type, args), start)
    }

    // A name, a function call, a message or a handler literal.
    func nameExpression() throws -> GesExpression {
        let checkpoint = mark()
        let start = current
        let upper = currentKind == .message
        let name = advance().text
        if peekKind(0) == .leftParen { return try callExpression(name, upper, start, checkpoint) }
        if upper { throw failure("Expected handler signature.", start) }
        return node(.name(name), start)
    }

    func callExpression(_ name: String, _ upper: Bool, _ start: GesToken, _ checkpoint: GesSyntaxTreeBuilder.Checkpoint) throws -> GesExpression {
        newlines()
        if upper && !isMessageArguments() { return wrap(checkpoint, .handlerLiteralExpression, node(.handler(name, try parameters(false, listNode: false)), start)) }
        let args = try arguments()
        return wrap(checkpoint, upper ? .messageExpression : .callExpression, node(upper ? .message(name, args) : .call(name, args), start))
    }

    func rangeExpression(_ start: GesToken) throws -> GesExpression {
        let from = try expression(13)
        newlines()
        try expect(.to)
        let to = try expression(13)
        let step = matchWord("step") ? try expression() : nil
        return node(.range(from, to, step), start)
    }

    func positiveInteger() throws -> Int {
        let t = current
        guard t.kind == .float, let n = GesCompilerSupport.number(t.text)?.integerValue, n > 0 && n <= Int32.max else {
            throw failure("Expected positive Int32 literal.")
        }
        advance()
        return Int(n)
    }

    // A labeled first argument, as C# IsArgumentLabelStart after the opening parenthesis.
    func isMessageArguments() -> Bool {
        var i = index + 1
        while i < tokens.count && tokens[i].kind == .newLine { i += 1 }
        guard i < tokens.count, tokens[i].kind == .identifier || Self.labelKeywords.contains(tokens[i].kind) else { return false }
        i += 1
        while i < tokens.count && tokens[i].kind == .newLine { i += 1 }
        return i < tokens.count && tokens[i].kind == .colon
    }

    // Prefix intrinsic names, which C# accepts as extension function names and argument labels.
    static let intrinsicNames: Set<GesTokenKind> = [
        .intrinsicAbs, .intrinsicLn, .intrinsicExp, .intrinsicSqrt, .intrinsicCbrt, .intrinsicChance, .intrinsicFloor, .intrinsicCeil, .intrinsicTruncate, .intrinsicRad, .intrinsicDeg,
        .intrinsicWrap, .intrinsicRound, .intrinsicSin, .intrinsicCos, .intrinsicTan, .intrinsicAsin, .intrinsicAcos, .intrinsicAtan, .intrinsicAtan2, .intrinsicHypot, .intrinsicDistance,
        .intrinsicSquared, .intrinsicLength, .intrinsicNormalize, .intrinsicDot, .intrinsicCross, .intrinsicAngle,
    ]

    // Labels are identifiers or the keyword-like words C# accepts, such as `to:`; other keywords are not labels.
    static let labelKeywords: Set<GesTokenKind> = intrinsicNames.union([.to, .nothing, .mathConstantPi, .mathConstantE, .mathConstantTau, .mathConstantInfinity])

    func argumentLabel() -> Bool { (currentKind == .identifier || Self.labelKeywords.contains(currentKind)) && peekKind() == .colon }

    func argument() throws -> GesArgument {
        startNode(.argument)
        defer { finishNode() }
        let start = current
        let label: String
        if argumentLabel() {
            label = advance().text
            newlines()
            try expect(.colon)
        } else {
            label = "_"
        }
        let value = try expression()
        return .init(label: label, value: value, location: location(start, previous))
    }

    // Arguments in parentheses; positional intrinsic operands have no argument nodes, as in C#.
    func arguments(nodes: Bool = true) throws -> [GesArgument] {
        try expect(.leftParen)
        newlines()
        var result: [GesArgument] = []
        if match(.rightParen) { return result }
        while true {
            result.append(try nodes ? argument() : .init(label: "_", value: expression()))
            newlines()
            if match(.comma) {
                newlines()
                continue
            }
            if argumentLabel() { continue }
            try expect(.rightParen)
            return result
        }
    }

    func collection(_ start: GesToken, _ checkpoint: GesSyntaxTreeBuilder.Checkpoint) throws -> GesExpression {
        newlines()
        if isCombinedSelector {
            // A combined selector without a source, such as `[:cartesian a, b]`.
            let result = try pipelineSteps(nil)
            newlines()
            try expect(.rightBracket)
            return wrap(checkpoint, .collectionAccessExpression, result)
        }
        newlines()
        // In an object pattern after `:has`, brackets form patterns and their entries, as in C#.
        let pattern = objectPattern
        objectPattern = false
        defer { objectPattern = pattern }
        if match(.colon) {
            newlines()
            try expect(.rightBracket)
            return wrap(checkpoint, pattern ? .objectPattern : .mapLiteralExpression, node(.map([]), start))
        }
        if match(.rightBracket) { return wrap(checkpoint, pattern ? .objectPattern : .listLiteralExpression, node(.list([]), start)) }
        // A non-empty literal starts at the last token before its first entry, which is a skipped line break for a
        // multi-line literal, as C# takes Previous after skipping new lines.
        // A map entry starts with a plain identifier key, as C# IsMapLiteralEntryStart.
        if currentKind == .identifier && peekKind() == .colon { return wrap(checkpoint, pattern ? .objectPattern : .mapLiteralExpression, try mapLiteral(previous, pattern: pattern)) }
        return wrap(checkpoint, pattern ? .objectPattern : .listLiteralExpression, try listLiteral(previous))
    }

    func mapLiteral(_ literalStart: GesToken, pattern: Bool = false) throws -> GesExpression {
        var pairs: [(String, GesExpression)] = []
        repeat {
            newlines()
            startNode(pattern ? .objectPatternEntry : .mapEntry)
            let t = current
            let key = try identifier()
            try expect(.colon)
            newlines()
            // A nested pattern value is itself a pattern.
            objectPattern = pattern && currentKind == .leftBracket
            let value = currentKind == .comma || currentKind == .rightBracket ? node(.literal(.boolean(true)), t) : try expression()
            objectPattern = false
            pairs.append((key, value))
            finishNode()
            newlines()
        } while match(.comma)
        try expect(.rightBracket)
        return node(.map(pairs), literalStart)
    }

    func listLiteral(_ literalStart: GesToken) throws -> GesExpression {
        var items: [GesExpression] = []
        repeat {
            items.append(try expression())
            newlines()
        } while match(.comma)
        try expect(.rightBracket)
        return node(.list(items), literalStart)
    }

    // A namespace, a dot and an extension function name, as C# IsExtensionCallStart.
    func isExtensionCallStart() -> Bool {
        currentKind.isSelector && peekKind() == .dot && (peekKind(2) == .identifier || Self.intrinsicNames.contains(peekKind(2)))
    }

    func extensionSymbol() throws -> (String, String) {
        let t = advance()
        let ns = String(t.text.dropFirst())
        newlines()
        let dot = current
        try expect(.dot)
        newlines()
        // Identifiers and prefix intrinsic names, as C# IsExtensionFunctionNameKind; other keywords are not names.
        guard currentKind == .identifier || Self.intrinsicNames.contains(currentKind) else { throw failure("Expected extension function.") }
        let functionToken = advance()
        if t.endLine != dot.line || t.endColumn != dot.column || dot.endLine != functionToken.line || dot.endColumn != functionToken.column { throw failure("Extension names must be contiguous.", t) }
        let function = functionToken.text
        if ["integer", "degree"].contains(ns) { throw failure("Use direct math intrinsics.", t) }
        return (ns, function)
    }

    // The clauses after a selector keyword; selectorClauses decides them before any nested expression is parsed.
    enum SelectorClauses {
        case none
        case binding(word: String?)
        case accumulator, key, direction, contains, term, count, choose, slice, pattern
    }

    func selector() throws -> GesSelector {
        let start = current
        if !currentKind.isSelector { return .init(operation: "index", expressions: [try expression()]) }
        let op = String(advance().text.dropFirst())
        newlines()
        var s = GesSelector(operation: op)
        switch try selectorClauses(op, start) {
        case .none: break
        case .binding(let word): try bindingClause(&s, word: word)
        case .accumulator: try accumulatorClause(&s, op)
        case .key: try keyClause(&s, op)
        case .direction: s.mode = try direction()
        case .contains:
            if currentKind == .identifier && ["all", "any"].contains(current.text) { s.mode = advance().text }
            s.expressions = [try expression()]
        case .term: s.expressions = [try expression()]
        case .count: s.count = try positiveInteger()
        case .choose: try chooseClauses(&s, start)
        case .slice: try sliceClause(&s, op)
        case .pattern: try hasClause(&s)
        }
        if op == "highest" { s.operation = "max" }
        if op == "lowest" { s.operation = "min" }
        s.location = location(start, previous)
        return s
    }

    func selectorClauses(_ op: String, _ start: GesToken) throws -> SelectorClauses {
        let ends = currentKind == .rightBracket || isPipelineSelector
        switch op {
        case "keys", "values", "entries", "shuffle", "reverse": return .none
        case "any", "all", "filter": return .binding(word: "where")
        case "count", "sum", "average": return ends ? .none : .binding(word: op == "count" ? "where" : nil)
        case "fold", "reduce": return .accumulator
        case "foreach", "select", "min", "max", "highest", "lowest": return .binding(word: nil)
        case "first", "last", "single": return ends ? .none : .binding(word: "where")
        case "map", "group", "order": return .key
        case "distinct": return ends ? .none : .key
        case "sort": return .direction
        case "contains": return .contains
        case "term": return .term
        case "draw": return .count
        case "choose": return .choose
        case "take", "drop": return .slice
        case "has": return .pattern
        default: throw failure("Unknown selector.", start)
        }
    }

    // Binding names, then `word` or the projection arrow, then one expression.
    func bindingClause(_ s: inout GesSelector, word: String?) throws {
        s.names = try bindingNames()
        s.name = s.names[0]
        if let word { try expectWord(word) } else { try expect(.projectionArrow) }
        s.expressions = [try expression()]
    }

    func accumulatorClause(_ s: inout GesSelector, _ op: String) throws {
        s.accumulator = try identifier()
        if op == "fold" {
            try expect(.be)
            s.expressions.append(try expression())
        }
        try expect(.comma)
        newlines()
        s.names = try bindingNames()
        s.name = s.names[0]
        try expect(.projectionArrow)
        newlines()
        s.expressions.append(try expression())
    }

    func keyClause(_ s: inout GesSelector, _ op: String) throws {
        if op == "map" {
            s.names = try bindingNames()
            s.name = s.names[0]
            try expectWord("by")
        } else {
            try expectWord("by")
            s.names = try bindingNames()
            s.name = s.names[0]
            try expect(.projectionArrow)
        }
        s.expressions = [try expression()]
        if op == "map" && match(.projectionArrow) { s.expressions.append(try expression()) }
        if op == "order" { s.mode = try direction() }
    }

    func chooseClauses(_ s: inout GesSelector, _ start: GesToken) throws {
        s.name = ""
        s.count = try positiveInteger()
        newlines()
        if matchWord("at") {
            try expect(.keywordRandom)
            s.mode = "random"
        }
        newlines()
        if current.isWord && (peek().isIdentifier("where") || peekKind() == .comma) {
            s.names = try bindingNames()
            s.name = s.names[0]
            try expectWord("where")
            s.expressions = [try expression()]
        }
        newlines()
        if matchWord("weighted") {
            try expectWord("by")
            s.weightNames = try bindingNames()
            s.weightName = s.weightNames[0]
            try expect(.projectionArrow)
            if s.expressions.isEmpty { s.expressions = [node(.literal(.boolean(true)), start)] }
            s.expressions.append(try expression())
        }
    }

    func sliceClause(_ s: inout GesSelector, _ op: String) throws {
        if currentKind == .identifier && ["first", "last", "highest", "lowest"].contains(current.text) {
            s.mode = advance().text
            s.count = try positiveInteger()
        } else if op == "take" {
            startNode(.dicePattern)
            s = try pattern(operation: "takePattern")
            finishNode()
        } else {
            throw failure("Expected slice scope.")
        }
    }

    func hasClause(_ s: inout GesSelector) throws {
        guard currentKind == .leftBracket else {
            startNode(.dicePattern)
            s = try pattern(operation: "hasPattern")
            finishNode()
            return
        }
        s.operation = "objectMatch"
        let checkpoint = mark()
        objectPattern = true
        let value = try collection(advance(), checkpoint)

        func patternDepth(_ value: GesExpression) -> Int {
            if case .map(let entries) = value.kind { return 1 + (entries.map { patternDepth($0.1) }.max() ?? 0) }
            return value.depth
        }

        // C# stops when it enters the first pattern at level 32, so the report names that pattern.
        if patternDepth(value) + 1 > 32 {
            var pattern = value
            for level in 1..<32 {
                guard case .map(let entries) = pattern.kind, let next = entries.first(where: { patternDepth($0.1) >= 32 - level }) else { break }
                pattern = next.1
            }
            throw nestingLimit(pattern.location)
        }
        s.expressions = [value]
    }

    func direction() throws -> String {
        newlines()
        guard currentKind == .identifier && ["ascending", "descending"].contains(current.text) else { throw failure("Expected sort direction.") }
        return advance().text
    }

    func pattern(operation: String) throws -> GesSelector {
        var s = GesSelector(operation: operation)
        if matchWord("full") {
            try expectWord("house")
            s.mode = "fullHouse"
            return s
        }
        if matchWord("straight") {
            s.mode = "straight"
            return s
        }
        let t = advance()
        guard let count = ["pair": 2, "three": 3, "four": 4, "five": 5, "six": 6, "seven": 7][t.kind == .identifier ? t.text : ""] else { throw failure("Expected dice pattern.", t) }
        s.count = count
        s.mode = "kind"
        newlines()
        if t.text != "pair" { try expect(.of) } else if !match(.of) { return s }
        if matchWord("a") {
            try expectWord("kind")
        } else {
            s.mode = "face"
            s.expressions = [try expression(15)]
        }
        return s
    }
}
