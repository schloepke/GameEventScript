// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

extension GesSyntaxParser {
    static let precedence: [GesTokenKind: Int] = [
        .operatorImplication: 1, .default: 2, .operatorCollectionUnion: 3, .operatorCollectionIntersect: 4, .zip: 5, .operatorOr: 6, .operatorXor: 7, .operatorAnd: 8,
        .operatorEqual: 9, .operatorNotEqual: 9, .in: 10, .notIn: 10, .starts: 10, .ends: 10, .has: 10, .as: 11, .is: 11,
        .operatorLess: 12, .operatorGreater: 12, .operatorLessOrEqual: 12, .operatorGreaterOrEqual: 12, .operatorPlus: 13, .operatorMinus: 13,
        .operatorMultiply: 14, .operatorDivide: 14, .operatorIntegerDivide: 14, .operatorModulo: 14, .operatorRemainder: 14, .operatorPower: 16,
    ]

    static let prefixOperators: Set<GesTokenKind> = [.operatorMinus, .operatorNot, .keywordParse, .empty]

    static let unaryIntrinsics: Set<GesTokenKind> = [
        .intrinsicAbs, .intrinsicLn, .intrinsicExp, .intrinsicSqrt, .intrinsicCbrt, .intrinsicChance, .intrinsicFloor, .intrinsicCeil, .intrinsicTruncate, .intrinsicRad, .intrinsicDeg,
        .intrinsicSin, .intrinsicCos, .intrinsicTan, .intrinsicAsin, .intrinsicAcos, .intrinsicAtan, .intrinsicWrap, .intrinsicRound,
    ]

    // Intrinsics that take a parenthesized, positional argument list.
    static let listIntrinsics: Set<GesTokenKind> = [.intrinsicAtan2, .intrinsicHypot, .intrinsicDistance, .intrinsicLength, .intrinsicNormalize, .intrinsicDot, .intrinsicCross, .intrinsicAngle]

    static let mathConstants: Set<GesTokenKind> = [.mathConstantPi, .mathConstantE, .mathConstantTau, .mathConstantInfinity]

    // Tokens besides numbers and names that start the single operand of an unparenthesized extension call, as C#
    // `IsExtensionOperandKind`; scripts/verify-swift-parser-tokens.py checks both sets.
    static let extensionOperandKinds: Set<GesTokenKind> = [
        .message, .tag, .selector, .typeName, .constantReference, .text, .`true`, .`false`, .nothing, .mathConstantPi, .mathConstantE, .mathConstantTau, .mathConstantInfinity,
        .intrinsicAbs, .intrinsicLn, .intrinsicExp, .intrinsicSqrt, .intrinsicCbrt, .intrinsicChance, .intrinsicFloor, .intrinsicCeil, .intrinsicTruncate, .intrinsicRad, .intrinsicDeg,
        .intrinsicWrap, .intrinsicRound, .intrinsicSin, .intrinsicCos, .intrinsicTan, .intrinsicAsin, .intrinsicAcos, .intrinsicAtan, .intrinsicAtan2, .intrinsicHypot, .intrinsicDistance,
        .intrinsicSquared, .intrinsicLength, .intrinsicNormalize, .intrinsicDot, .intrinsicCross, .intrinsicAngle, .keywordParse, .keywordRandom, .series, .clamp, .min, .max, .roll,
        .leftBracket, .leftParen, .operatorMinus, .has, .empty, .operatorNot,
    ]

    // Prefix intrinsic names, which are also extension function names and argument labels.
    static let intrinsicNames: Set<GesTokenKind> = [
        .intrinsicAbs, .intrinsicLn, .intrinsicExp, .intrinsicSqrt, .intrinsicCbrt, .intrinsicChance, .intrinsicFloor, .intrinsicCeil, .intrinsicTruncate, .intrinsicRad, .intrinsicDeg,
        .intrinsicWrap, .intrinsicRound, .intrinsicSin, .intrinsicCos, .intrinsicTan, .intrinsicAsin, .intrinsicAcos, .intrinsicAtan, .intrinsicAtan2, .intrinsicHypot, .intrinsicDistance,
        .intrinsicSquared, .intrinsicLength, .intrinsicNormalize, .intrinsicDot, .intrinsicCross, .intrinsicAngle,
    ]

    /// The keyword tokens that may spell an argument label besides identifiers, such as `to:`; other keywords are not
    /// labels. The Compiler binder reads labels by the same set.
    @_spi(Syntax) public static let labelKeywords: Set<GesTokenKind> = intrinsicNames.union([.to, .nothing, .mathConstantPi, .mathConstantE, .mathConstantTau, .mathConstantInfinity])

    static let combinedSelectors: Set<String> = [":cartesian", ":lockstep", ":zip", ":union", ":intersect", ":difference"]

    static let pipelineSelectors: Set<String> = [
        ":entries", ":keys", ":values", ":filter", ":select", ":foreach", ":any", ":all", ":count", ":sum", ":average", ":min", ":max", ":highest", ":lowest", ":fold", ":reduce", ":map", ":first", ":last", ":single", ":choose", ":take",
        ":drop", ":draw", ":shuffle", ":reverse", ":sort", ":distinct", ":group", ":order", ":contains", ":has", ":term", ":split",
    ]

    func assemblyOrExpression() throws -> Expression {
        if !current.isIdentifier("asm") { return try expression() }
        startNode(.assemblyBlock)
        let start = index
        advanceKeyword()
        try assemblyBlock(standalone: false)
        return finishNode(try node(start))
    }

    func assemblyBlock(standalone: Bool = true) throws {
        newlines()
        try expect(.leftBrace)
        separators()
        var lines = 0
        while currentKind != .rightBrace {
            let token = current
            let exported = match(.`let`)
            if exported || match(.dot) {
                if !exported { try expectWord("register") }
                if lines > 0 || exported && !standalone { throw failure("Assembly declarations must precede instructions and only standalone blocks export bindings.", token) }
                repeat {
                    _ = try identifier()
                    role(.assemblyDeclaration)
                } while match(.comma)
            } else if currentKind == .emit || currentKind == .publish {
                // An assembly send has the form of a statement send, but no statement node; a delayed send is no line.
                if peek().isIdentifier("after") {
                    let start = index
                    advance()
                    newlines()
                    _ = try sendExpression(start, mark())
                    throw failure("Expected a statement send.", token)
                }
                try publishStatement()
                lines += 1
            } else if current.isWord {
                _ = try identifier()
                role(.assemblyLabel)
                try expect(.colon)
                lines += 1
            } else {
                guard currentKind == .message else { throw failure("Expected an assembly instruction.", token) }
                let name = advance().text
                role(.opcode)
                var operands = 0
                if currentKind != .newLine && currentKind != .semicolon && currentKind != .rightBrace {
                    repeat {
                        if Self.assemblyMessageOperand(name, operands), currentKind == .message {
                            startNode(.messageExpression)
                            let messageStart = index
                            advance()
                            role(.messageName)
                            if currentKind == .leftParen { try arguments() }
                            _ = finishNode(try node(messageStart))
                        } else if currentKind == .operatorMinus, peek().text == "9223372036854775808" {
                            advance()
                            advance()
                        } else if currentKind == .typeName && tokens[min(index + 1, tokens.count - 1)].kind != .leftParen {
                            advance()
                        } else {
                            _ = try expression()
                        }
                        operands += 1
                    } while match(.comma)
                }
                lines += 1
            }
            if currentKind != .newLine && currentKind != .semicolon && currentKind != .rightBrace { throw failure("Expected assembly line separator.") }
            separators()
        }
        try expect(.rightBrace)
    }

    static func assemblyMessageOperand(_ opcode: String, _ index: Int) -> Bool {
        switch opcode {
        case "LoadMessage", "EmitInstant", "PublishInstant": return index == 1
        case "EmitAfter", "PublishAfter": return index == 2
        case "EmitMessage", "EmitMessageWithTags", "PublishMessage", "PublishMessageWithTags": return index == 0
        default: return false
        }
    }

    // The next step of the operator loop after a left operand.
    enum InfixStep {
        // A postfix form, applied completely.
        case complete(Expression)
        // An `is` test, which isExpression completes.
        case test
        // A binary operator whose right operand follows with the given minimum precedence.
        case operand(GesTokenKind, Int)
    }

    // Right operands recurse from this small frame; infixStep and infix read and combine the operators in their own.
    func expression(_ minimum: Int = 0) throws -> Expression {
        // As in C#, the limit names the first token of the operand, after any line break.
        newlines()
        if expressionDepth >= 32 { throw failure("Expression nesting limit exceeded.", code: GesSyntaxDiagnostic.sourceNestingExceeded) }
        expressionDepth += 1
        defer { expressionDepth -= 1 }
        let first = index
        let result = try operatorExpression(minimum)
        completedDepths.append((first, result.depth))
        return result
    }

    func operatorExpression(_ minimum: Int) throws -> Expression {
        if minimum == 0 && currentKind == .when {
            startNode(.guardedChoiceExpression)
            advance()
            return finishNode(try conditionFirstChoice(previousIndex))
        }
        // Each operator wraps its left operand, which starts here, into the operator's node.
        let start = mark()
        var left = try prefix()
        var ceiling = Int.max
        while let step = try infixStep(left, minimum, ceiling) {
            ceiling = min(ceiling, completedCeiling)
            switch step {
            case .complete(let value): left = wrap(start, completedInfixKind, value)
            case .test: left = wrap(start, .typeOperationExpression, try isExpression(left))
            case .operand(let op, let rightMinimum): left = wrap(start, Self.infixNodeKind(op), try infix(op, left, expression(rightMinimum)))
            }
        }
        if minimum == 0 && currentKind == .when {
            startNode(at: start, .guardedChoiceExpression)
            startNode(at: start, .choiceBranch)
            advance()
            left = finishNode(try trailingChoice(left))
        }
        return left
    }

    static func infixNodeKind(_ op: GesTokenKind) -> GesSyntaxNodeKind {
        switch op {
        case .in, .notIn, .starts, .ends, .has: .membershipExpression
        default: .binaryExpression
        }
    }

    // Consumes the operator after `left`; nil ends the operator loop. A completed postfix form also sets
    // `completedInfixKind`, kept outside the step so that the recursive frames stay small.
    func infixStep(_ left: Expression, _ minimum: Int, _ ceiling: Int) throws -> InfixStep? {
        if isForClauseStart() { return nil }
        if minimum <= 16 && ceiling >= 16 && currentKind == .superscriptInteger {
            completedInfixKind = .binaryExpression
            completedCeiling = 14
            advance()
            let exponent = leaf(previousIndex)
            return .complete(try combined(left, exponent))
        }
        let unitless = left.shape == .number || left.shape == .boolean
        if minimum <= 14, ceiling >= 14, unitless && previous.end == current.start, current.isName || minimum <= 14 && previous.end == current.start && Self.mathConstants.contains(currentKind) {
            completedCeiling = 14
            return .operand(.operatorMultiply, 15)
        }
        guard let precedence = Self.precedence[currentKind], precedence >= minimum, precedence <= ceiling else { return nil }
        let op = advance().kind
        newlines()
        // A power takes one `^` or superscript, whose right operand may be a power again; then only multiplication and
        // looser operators follow.
        completedCeiling = op == .operatorPower ? 14 : precedence
        if op == .as {
            completedInfixKind = .typeOperationExpression
            try typeName()
            return .complete(try ranged(left))
        }
        if op == .is { return .test }
        if op == .has {
            completedInfixKind = .membershipExpression
            try expectWord("value")
            return .complete(try ranged(left))
        }
        if op == .starts || op == .ends {
            try expect(.with)
            newlines()
        }
        if op == .in && current.isIdentifier("values") && peekKind() == .of {
            advanceKeyword()
            newlines()
            try expect(.of)
        }
        return .operand(op, op == .operatorPower ? 15 : op == .operatorImplication ? precedence : precedence + 1)
    }

    func infix(_ op: GesTokenKind, _ left: Expression, _ right: Expression) throws -> Expression {
        // `not in` counts the membership and its negation.
        if op == .notIn { return try ranged(try combined(left, right)) }
        return try combined(left, right)
    }

    // `value when condition, … otherwise fallback` after its first value and `when`, inside the open first branch.
    func trailingChoice(_ first: Expression) throws -> Expression {
        var depth = max(first.depth, try expression(1).depth)
        finishNode()
        while match(.comma) {
            newlines()
            if currentKind == .otherwise { break }
            _ = match(.operatorOr)
            newlines()
            startNode(.choiceBranch)
            depth = max(depth, try expression(1).depth)
            newlines()
            try expect(.when)
            depth = max(depth, try expression(1).depth)
            finishNode()
        }
        newlines()
        try expect(.otherwise)
        let fallback = try expression(1)
        var result = try combined(first, fallback)
        result.depth = max(depth, fallback.depth) + 1
        if result.depth > 32 { throw nestingLimit(result.start, result.end) }
        return result
    }

    func conditionFirstChoice(_ start: Int) throws -> Expression {
        var depth = 0
        while true {
            newlines()
            startNode(.choiceBranch)
            depth = max(depth, try choiceCondition().depth)
            newlines()
            try expectWord("then")
            depth = max(depth, try expression(1).depth)
            finishNode()
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
        let result = Expression(depth: max(depth, fallback.depth) + 1, start: start, end: previousIndex)
        if result.depth > 32 { throw nestingLimit(result.start, result.end) }
        return result
    }

    func choiceCondition() throws -> Expression {
        let saved = choiceConditionDepth
        choiceConditionDepth = delimiterDepth
        defer { choiceConditionDepth = saved }
        return try expression(1)
    }

    func isExpression(_ input: Expression) throws -> Expression {
        let negated = match(.operatorNot)
        newlines()
        let value = try isTest(input)
        return negated ? try ranged(value) : value
    }

    // The test after `is` and an optional `not`; tests with a nested operand parse it in their own function.
    func isTest(_ input: Expression) throws -> Expression {
        if currentKind == .typeName {
            try typeName()
            return try ranged(input)
        }
        if currentKind == .numeric || currentKind == .nothing || current.isIdentifier("integer") || current.isIdentifier("fractional") {
            if currentKind == .identifier { advanceKeyword() } else { advance() }
            return try ranged(input)
        }
        if match(.empty) { return try ranged(input) }
        if isExtensionCallStart() {
            try extensionSymbol()
            return try ranged(input)
        }
        if matchWord("between") { return try betweenTest(input) }
        if matchWord("at") {
            let mode = advance()
            guard mode.isIdentifier("least") || mode.isIdentifier("most") else { throw failure("Expected least or most.", previous) }
            role(.keyword)
            return try combined(input, try expression(13))
        }
        if current.isIdentifier("less") || current.isIdentifier("more") {
            advanceKeyword()
            try expectWord("than")
            return try combined(input, try expression(13))
        }
        if current.isName {
            advance()
            role(.predicateTarget)
            return try ranged(input)
        }
        return try equalityTest(input)
    }

    func betweenTest(_ input: Expression) throws -> Expression {
        newlines()
        let minimum = try expression(13)
        newlines()
        try expect(.operatorAnd)
        newlines()
        let maximum = try expression(13)
        var value = try combined(input, maximum)
        value.depth = max(value.depth, minimum.depth + 1)
        if value.depth > 32 { throw nestingLimit(value.start, value.end) }
        return value
    }

    // `is value`, optionally followed by `or less` or `or more`.
    func equalityTest(_ input: Expression) throws -> Expression {
        let right = try expression(13)
        newlines()
        if match(.operatorOr) {
            newlines()
            if !matchWord("less") { try expectWord("more") }
        }
        return try combined(input, right)
    }

    // Nested expressions recurse through `expression`, `prefix` and `primary`. An unoptimized build reserves stack for
    // every local of a function on each call, so these stay small dispatchers: each form parses in its own function.
    func prefix() throws -> Expression {
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

    // A prefix operation measures two levels from a placeholder start, whatever its operand.
    func prefixOperation() throws -> Expression {
        startNode(.unaryExpression)
        let start = index
        let op = advance().kind
        // `strict` directly after `parse` on the same line is always the mode word, never an operand.
        // The mode word stands on the line of `parse`; inside parentheses a line break is trivia, so compare lines.
        if op == .keywordParse && currentKind == .identifier && current.text == "strict" && current.line == previous.endLine { advanceKeyword() }
        let value = try expression(15)
        let negated: Shape = value.shape == .number || value.shape == .quantity ? .negated : .negatedOther
        return finishNode(Expression(depth: 2, start: start, end: previousIndex, shape: op == .operatorMinus ? negated : .none))
    }

    func unaryIntrinsic() throws -> Expression {
        startNode(.unaryExpression)
        let start = index
        let root = currentKind == .intrinsicSqrt || currentKind == .intrinsicCbrt
        try unaryIntrinsicName()
        // A parenthesized operand belongs to the intrinsic: its parentheses add no depth and end the operand.
        if currentKind == .leftParen { try intrinsicParenthesizedOperand() } else { _ = try expression(15) }
        // A root lowers to a power ranged from the operator.
        return finishNode(root ? Expression(depth: 2, start: start, end: previousIndex) : try node(start))
    }

    func intrinsicParenthesizedOperand() throws {
        advance()
        _ = try expression()
        newlines()
        try expect(.rightParen)
    }

    // The intrinsic word including its mode words, such as `round half even`.
    func unaryIntrinsicName() throws {
        let op = advance().text
        newlines()
        if op == "wrap" { try expectWord("degree") }
        if op == "round" {
            try expectWord("half")
            newlines()
            let token = advance()
            guard token.kind == .identifier, ["even", "up", "down"].contains(token.text) else { throw failure("Expected rounding mode.", previous) }
            role(.keyword)
        }
    }

    func listIntrinsic() throws -> Expression {
        startNode(.intrinsicCallExpression)
        let start = index
        let op = advance().text
        newlines()
        if op == "distance" { _ = match(.intrinsicSquared) }
        if op == "length" { try expect(.intrinsicSquared) }
        if op == "angle" { try expectWord("between") }
        newlines()
        try arguments(nodes: false)
        return finishNode(try node(start))
    }

    func clampIntrinsic() throws -> Expression {
        startNode(.clampExpression)
        let start = index
        advance()
        _ = try expression(15)
        newlines()
        try expectWord("between")
        _ = try expression(9)
        try expect(.operatorAnd)
        _ = try expression(9)
        return finishNode(try node(start))
    }

    func variadicIntrinsic() throws -> Expression {
        startNode(.variadicExpression)
        let start = index
        advance()
        newlines()
        try expect(.of)
        _ = try expression(9)
        while !isForClauseStart() && match(.operatorAnd) { _ = try expression(9) }
        return finishNode(try node(start))
    }

    func extensionCall() throws -> Expression {
        let checkpoint = mark()
        let start = index
        try extensionSymbol()
        newlines()
        if currentKind == .leftParen {
            try arguments()
            return try postfixSuffixes(wrap(checkpoint, .extensionCallExpression, try node(start)), checkpoint)
        }
        try extensionOperands()
        return wrap(checkpoint, .extensionCallExpression, try node(start))
    }

    // The operands of an unparenthesized extension call: `of` operands, labeled arguments or one operand.
    func extensionOperands() throws {
        if match(.of) {
            _ = try expression(9)
            while !isForClauseStart() && match(.operatorAnd) { _ = try expression(9) }
            return
        }
        if argumentLabel() {
            repeat { try argument() } while argumentLabel()
            return
        }
        if isExtensionOperandStart() { _ = try expression(15) }
    }

    func isExtensionOperandStart() -> Bool {
        !(choiceConditionDepth == delimiterDepth && current.isIdentifier("then")) && (current.isNumber || current.isName || Self.extensionOperandKinds.contains(currentKind))
    }

    func postfixSuffixes(_ base: Expression, _ start: GesSyntaxTreeBuilder.Checkpoint) throws -> Expression {
        var result = base
        while true {
            if match(.dot) {
                startNode(at: start, .memberAccessExpression)
                newlines()
                _ = try identifier()
                role(.member)
                result = finishNode(try ranged(result))
            } else if match(.leftBracket) {
                startNode(at: start, .collectionAccessExpression)
                newlines()
                result = try pipelineSteps(result)
                newlines()
                try expect(.rightBracket)
                result = finishNode(result)
            } else {
                break
            }
        }
        return result
    }

    func sendExpression(_ start: Int, _ checkpoint: GesSyntaxTreeBuilder.Checkpoint) throws -> Expression {
        startNode(at: checkpoint, .sendExpression)
        newlines()
        if matchWord("after") { _ = try expression() }
        if currentKind == .message {
            newlines()
            startNode(.messageExpression)
            let messageStart = index
            advance()
            role(.messageName)
            if currentKind == .leftParen { try arguments() }
            _ = finishNode(try node(messageStart))
        } else {
            _ = try expression(15)
        }
        if currentKind == .with {
            startNode(.tagClause)
            advance()
            repeat { _ = try expression(15) } while match(.comma)
            finishNode()
        }
        return finishNode(try node(start))
    }

    func primary() throws -> Expression {
        switch currentKind {
        case .`is`: return try predicateReference()
        case .emit, .publish:
            // The send node of an expression includes its emit or publish keyword.
            let checkpoint = mark()
            let start = index
            advance()
            return try sendExpression(start, checkpoint)
        case .text, .tag, .nothing: return scalar(.scalar)
        case .`true`, .`false`: return scalar(.boolean)
        case .constantReference: return scalar(.none)
        case .mathConstantPi, .mathConstantE, .mathConstantTau, .mathConstantInfinity: return scalar(.number)
        case .leftParen: return try parenthesized()
        case .leftBracket:
            let checkpoint = mark()
            let start = index
            advance()
            return try collection(start, checkpoint)
        case .keywordRandom: return try randomExpression()
        case .series: return try seriesExpression()
        case .roll:
            // As in C#, `roll` starts a dice roll only before `dice`; otherwise the word itself is unexpected.
            var next = index + 1
            while next < tokens.count && tokens[next].kind == .newLine { next += 1 }
            guard next < tokens.count, tokens[next].isIdentifier("dice") else { throw unexpectedToken(current) }
            return try diceRoll()
        case .typeName: return try typeExpression()
        case .identifier where current.isIdentifier("from"):
            startNode(.rangeExpression)
            advanceKeyword()
            return finishNode(try rangeExpression(previousIndex))
        case .identifier, .message: return try nameExpression()
        default:
            if current.isNumber { return numberLiteral() }
            throw unexpectedToken(current)
        }
    }

    func unexpectedToken(_ token: GesToken) -> Failure { failure("Unexpected token '\(token.text)'.", token) }

    // `is name` or `is not name`: a predicate of the implicit subject.
    func predicateReference() throws -> Expression {
        startNode(.typeOperationExpression)
        let start = index
        advance()
        newlines()
        _ = match(.operatorNot)
        newlines()
        _ = try identifier()
        role(.predicateTarget)
        return finishNode(try node(start))
    }

    func scalar(_ shape: Shape) -> Expression {
        advance()
        return leaf(previousIndex, shape: shape)
    }

    // A number literal; a unit makes it a quantity, and a percentage beyond the binary64 range reads as nothing.
    func numberLiteral() -> Expression {
        let token = advance()
        switch token.kind {
        case .unitNumber: return leaf(previousIndex, shape: .quantity)
        case .percentage: return leaf(previousIndex, shape: GesNumberText.isFinitePercentage(token.text) ? .number : .scalar)
        default: return leaf(previousIndex, shape: .number)
        }
    }

    func parenthesized() throws -> Expression {
        startNode(.parenthesizedExpression)
        advance()
        var result = try expression()
        newlines()
        try expect(.rightParen)
        result.depth += 1
        if result.depth > 32 { throw nestingLimit(result.start, result.end) }
        return finishNode(result)
    }

    func randomExpression() throws -> Expression {
        let checkpoint = mark()
        var start = index
        advance()
        newlines()
        // As in C#, the range starts at the last token before its bounds, so a line break after `random` begins it.
        if previous.kind == .newLine { start = previousIndex }
        if match(.with) {
            let withToken = previousIndex
            _ = try expression()
            _ = try expression()
            return wrap(checkpoint, .seededRandomExpression, try node(withToken))
        }
        _ = matchWord("from")
        _ = try expression(13)
        newlines()
        try expect(.to)
        _ = try expression(13)
        return wrap(checkpoint, .randomExpression, try node(start))
    }

    func seriesExpression() throws -> Expression {
        startNode(.seriesExpression)
        let start = index
        advance()
        newlines()
        // As in C#, a missing kind fails at the next token without consuming it.
        guard currentKind == .fibonacci || currentKind == .factorial else { throw failure("Expected series kind.") }
        advance()
        return finishNode(try node(start))
    }

    func diceRoll() throws -> Expression {
        startNode(.diceExpression)
        advance()
        newlines()
        try expectWord("dice")
        let start = previousIndex
        // As in C#, the count and the sides follow on the same line and are checked after both are read.
        guard currentKind == .float else { throw failure("Expected a dice count.") }
        let count = advance()
        let sides: GesToken
        if current.isIdentifier("d") {
            advance()
            guard currentKind == .float else { throw failure("Expected a side count.") }
            sides = advance()
        } else if currentKind == .identifier, current.text.unicodeScalars.count > 1, current.text.hasPrefix("d"), current.text.unicodeScalars.dropFirst().allSatisfy({ GesLexer.digit($0) }) {
            // The rest of a compact dice token such as `d6` is its side count.
            let token = advance()
            sides = .init(
                kind: .float,
                text: String(token.text.dropFirst()),
                isWord: false,
                line: token.line,
                column: token.column + 1,
                endLine: token.endLine,
                endColumn: token.endColumn,
                fullStart: token.start + 1,
                start: token.start + 1,
                end: token.end
            )
        } else {
            throw failure("Expected dice separator.")
        }
        for token in [count, sides] where GesNumberText.positiveInt32(token.text) == nil { throw failure("Expected a positive Int32 literal.", token) }
        return finishNode(try node(start))
    }

    // A type name starts a data form or a constructor call.
    func typeExpression() throws -> Expression {
        let start = index
        // `:Dice[...]`, `:Bytes[...]` and `:List[...]` are data forms that own their type token.
        if current.text == ":Dice" || current.text == ":Bytes" || current.text == ":List", peekKind() == .leftBracket {
            startNode(current.text == ":Dice" ? .diceLiteralExpression : current.text == ":Bytes" ? .bytesLiteralExpression : .collectionFactoryExpression)
            let type = try typeNameCore()
            newlines()
            advance()
            return finishNode(type == "dice" || type == "bytes" ? try diceLiteral(start, bytes: type == "bytes") : try generatedList())
        }
        startNode(.typeConstructorExpression)
        return finishNode(try typeConstructor(start))
    }

    func typeConstructor(_ start: Int) throws -> Expression {
        let type = try typeName()
        // A type name that starts no data form or constructor is itself the unexpected token; as in C#, the failure
        // leaves a following line break unread, so recovery stops there.
        var next = index
        while next < tokens.count && tokens[next].kind == .newLine { next += 1 }
        guard next < tokens.count, tokens[next].kind == .leftParen else { throw unexpectedToken(tokens[start]) }
        newlines()
        if type == "series", currentKind == .leftParen, [.fibonacci, .factorial].contains(peekKind()) {
            advance()
            newlines()
            advance()
            newlines()
            if match(.comma) {
                repeat {
                    newlines()
                    try argument()
                    newlines()
                } while match(.comma)
            }
            try expect(.rightParen)
            return try node(start)
        }
        if type == "handler", currentKind == .leftParen, peekKind() == .message {
            advance()
            newlines()
            advance()
            role(.messageName)
            try expect(.leftParen)
            newlines()
            if currentKind != .rightParen {
                repeat {
                    newlines()
                    if !match(.underscore) {
                        _ = try identifier()
                        role(.parameter)
                    }
                    newlines()
                } while match(.comma)
            }
            try expect(.rightParen)
            newlines()
            try expect(.rightParen)
            return try node(start)
        }
        if type == "message", currentKind == .leftParen, peekKind() == .message {
            advance()
            newlines()
            startNode(.messageExpression)
            let messageStart = index
            advance()
            role(.messageName)
            if currentKind == .leftParen { try arguments() }
            _ = finishNode(try node(messageStart))
            newlines()
            if currentKind == .with {
                startNode(.tagClause)
                advance()
                _ = try expression()
                newlines()
                while match(.comma) {
                    _ = try expression()
                    newlines()
                }
                finishNode()
            }
            newlines()
            try expect(.rightParen)
            return try node(start)
        }
        // A type name that starts no data form or constructor is itself the unexpected token.
        guard currentKind == .leftParen else { throw unexpectedToken(tokens[start]) }
        try arguments(series: type == "series")
        return try node(start)
    }

    // A Dice or Bytes literal reads as a constructor over a list of its values.
    func diceLiteral(_ start: Int, bytes: Bool) throws -> Expression {
        newlines()
        if currentKind != .rightBracket {
            repeat {
                newlines()
                if bytes {
                    guard currentKind == .float, GesNumberText.isByte(current.text) else { throw failure("Expected an integer literal from 0 to 255.") }
                    advance()
                } else {
                    try positiveInteger()
                }
                newlines()
            } while match(.comma)
        }
        try expect(.rightBracket)
        return try node(start)
    }

    func generatedList() throws -> Expression {
        newlines()
        try expect(.selectorSelect)
        let selectToken = previousIndex
        newlines()
        _ = try identifier()
        role(.generatedBinding)
        newlines()
        if current.isIdentifier("from") {
            startNode(.rangeExpression)
            advanceKeyword()
            _ = finishNode(try rangeExpression(previousIndex))
        } else {
            try expect(.`in`)
            newlines()
            if current.isIdentifier("from") { throw failure("Expected collection.") }
            _ = try expression()
        }
        newlines()
        if matchWord("where") { _ = try expression() }
        newlines()
        try expect(.projectionArrow)
        _ = try expression()
        let generated = try node(selectToken)
        newlines()
        try expect(.rightBracket)
        return generated
    }

    // A name, a function call, a message or a handler literal.
    func nameExpression() throws -> Expression {
        let checkpoint = mark()
        let start = index
        let upper = currentKind == .message
        advance()
        if peekKind(0) == .leftParen {
            role(upper ? .messageName : .callTarget)
            newlines()
            if upper && !isMessageArguments() {
                try parameters(false, listNode: false)
                return wrap(checkpoint, .handlerLiteralExpression, try node(start))
            }
            try arguments()
            return wrap(checkpoint, upper ? .messageExpression : .callExpression, try node(start))
        }
        if upper { throw failure("Expected handler signature.", tokens[start]) }
        role(.variable)
        return try node(start)
    }

    func rangeExpression(_ start: Int) throws -> Expression {
        _ = try expression(13)
        newlines()
        try expect(.to)
        _ = try expression(13)
        if matchWord("step") { _ = try expression() }
        return try node(start)
    }

    @discardableResult func positiveInteger() throws -> Int {
        let t = current
        guard t.kind == .float, let n = GesNumberText.positiveInt32(t.text) else { throw failure("Expected positive Int32 literal.") }
        advance()
        return n
    }

    // A labeled first argument after the opening parenthesis.
    func isMessageArguments() -> Bool {
        var i = index + 1
        while i < tokens.count && tokens[i].kind == .newLine { i += 1 }
        guard i < tokens.count, tokens[i].kind == .identifier || Self.labelKeywords.contains(tokens[i].kind) else { return false }
        i += 1
        while i < tokens.count && tokens[i].kind == .newLine { i += 1 }
        return i < tokens.count && tokens[i].kind == .colon
    }

    func argumentLabel() -> Bool { (currentKind == .identifier || Self.labelKeywords.contains(currentKind)) && peekKind() == .colon }

    func argument(series: Bool = false) throws {
        startNode(.argument)
        defer { finishNode() }
        var label = ""
        if argumentLabel() {
            label = current.text
            advance()
            role(.argumentLabel)
            newlines()
            try expect(.colon)
        }
        // Only the `kind` argument of `:Series` names a series without quotes.
        if series && label == "kind" {
            newlines()
            if currentKind == .fibonacci || currentKind == .factorial {
                advance()
                return
            }
        }
        _ = try expression()
    }

    // Arguments in parentheses; positional intrinsic operands have no argument nodes.
    func arguments(nodes: Bool = true, series: Bool = false) throws {
        try expect(.leftParen)
        newlines()
        if match(.rightParen) { return }
        while true {
            if nodes { try argument(series: series) } else { _ = try expression() }
            newlines()
            if match(.comma) {
                newlines()
                continue
            }
            // Only argument nodes take a label without a comma before it; positional intrinsic operands do not.
            if nodes && argumentLabel() { continue }
            try expect(.rightParen)
            return
        }
    }

    func collection(_ start: Int, _ checkpoint: GesSyntaxTreeBuilder.Checkpoint) throws -> Expression {
        newlines()
        if isCombinedSelector {
            // A combined selector without a source, such as `[:cartesian a, b]`.
            let result = try pipelineSteps(nil)
            newlines()
            try expect(.rightBracket)
            return wrap(checkpoint, .collectionAccessExpression, result)
        }
        newlines()
        // In an object pattern after `:has`, brackets form patterns and their entries.
        let pattern = objectPattern
        objectPattern = false
        defer { objectPattern = pattern }
        // As in C#, an object pattern holds only `key: value` entries, so anything else fails at its first token.
        if pattern {
            if match(.rightBracket) { return wrap(checkpoint, .objectPattern, try node(start)) }
            return wrap(checkpoint, .objectPattern, try mapLiteral(previousIndex, pattern: true))
        }
        if match(.colon) {
            newlines()
            try expect(.rightBracket)
            var map = try node(start)
            map.pattern = Pattern(depth: 1, start: map.start, end: map.end, entries: [])
            return wrap(checkpoint, pattern ? .objectPattern : .mapLiteralExpression, map)
        }
        if match(.rightBracket) { return wrap(checkpoint, pattern ? .objectPattern : .listLiteralExpression, try node(start)) }
        // A non-empty literal starts at the last token before its first entry, a skipped line break for a multi-line
        // literal. A map entry starts with a plain identifier key.
        if currentKind == .identifier && peekKind() == .colon { return wrap(checkpoint, pattern ? .objectPattern : .mapLiteralExpression, try mapLiteral(previousIndex, pattern: pattern)) }
        return wrap(checkpoint, pattern ? .objectPattern : .listLiteralExpression, try listLiteral(previousIndex))
    }

    func mapLiteral(_ literalStart: Int, pattern: Bool = false) throws -> Expression {
        var entries: [Pattern] = []
        repeat {
            newlines()
            startNode(pattern ? .objectPatternEntry : .mapEntry)
            let t = index
            _ = try identifier()
            role(.mapKey)
            try expect(.colon)
            newlines()
            // A nested pattern value is itself a pattern.
            objectPattern = pattern && currentKind == .leftBracket
            let value = currentKind == .comma || currentKind == .rightBracket ? try node(t) : try expression()
            objectPattern = false
            entries.append(value.pattern ?? Pattern(depth: value.depth, start: value.start, end: value.end, entries: nil))
            finishNode()
            newlines()
        } while match(.comma)
        try expect(.rightBracket)
        var map = try node(literalStart)
        map.pattern = Pattern(depth: 1 + (entries.map(\.depth).max() ?? 0), start: map.start, end: map.end, entries: entries)
        return map
    }

    func listLiteral(_ literalStart: Int) throws -> Expression {
        repeat {
            _ = try expression()
            newlines()
        } while match(.comma)
        try expect(.rightBracket)
        return try node(literalStart)
    }

    // A namespace, a dot and an extension function name.
    func isExtensionCallStart() -> Bool {
        currentKind.isSelector && peekKind() == .dot && (peekKind(2) == .identifier || Self.intrinsicNames.contains(peekKind(2)))
    }

    func extensionSymbol() throws {
        let t = advance()
        let ns = String(t.text.dropFirst())
        newlines()
        let dot = current
        try expect(.dot)
        newlines()
        // Identifiers and prefix intrinsic names; other keywords are not names.
        guard currentKind == .identifier || Self.intrinsicNames.contains(currentKind) else { throw failure("Expected extension function.") }
        let functionToken = advance()
        if t.endLine != dot.line || t.endColumn != dot.column || dot.endLine != functionToken.line || dot.endColumn != functionToken.column { throw failure("Extension names must be contiguous.", t) }
        if ["integer", "degree"].contains(ns) { throw failure("Use direct math intrinsics.", t) }
    }

    var isCombinedSelector: Bool { currentKind == .selector && Self.combinedSelectors.contains(current.text) }

    var isPipelineSelector: Bool { currentKind.isSelector && peekKind() != .dot && (isCombinedSelector || Self.pipelineSelectors.contains(current.text)) }

    // Each step extends the expression of the steps before it.
    func pipelineSteps(_ base: Expression?) throws -> Expression {
        var result = base
        repeat {
            newlines()
            startNode(.collectionSelector)
            let start = index
            if isCombinedSelector {
                result = try combinedStep(result, start)
            } else if matchSelector(":split") {
                guard let input = result else { throw failure("Selector requires a source.", tokens[start]) }
                newlines()
                try expect(.on)
                newlines()
                if !matchWord("whitespace") { _ = try expression() }
                result = try ranged(input)
            } else {
                guard let input = result else { throw failure("Selector requires a source.", tokens[start]) }
                let objectMatch = try selector()
                let access = try combined(input)
                // The object-pattern selector has no range, which leaves the whole access without one.
                result = objectMatch ? Expression(depth: access.depth, start: -1, end: -1) : access
            }
            finishNode()
            newlines()
        } while isPipelineSelector
        return result!
    }

    func combinedStep(_ base: Expression?, _ start: Int) throws -> Expression {
        advance()
        newlines()
        var count = base == nil ? 0 : 1
        var depth = base?.depth ?? 0
        depth = max(depth, try expression().depth)
        count += 1
        while match(.comma) {
            newlines()
            depth = max(depth, try expression().depth)
            count += 1
        }
        guard count >= 2 else { throw failure("Combined selectors require at least two sources.", tokens[start]) }
        var result = try node(start)
        result.depth = depth + 1
        if result.depth > 32 { throw failure("Expression nesting exceeded.", tokens[start], code: GesSyntaxDiagnostic.sourceNestingExceeded) }
        return result
    }

    // The clauses after a selector keyword; selectorClauses decides them before any nested expression is parsed.
    enum SelectorClauses {
        case none
        case binding(word: String?)
        case accumulator, key, direction, contains, term, count, choose, slice, pattern
    }

    // Returns whether the selector is an object-pattern match.
    func selector() throws -> Bool {
        let start = current
        if !currentKind.isSelector {
            _ = try expression()
            return false
        }
        let op = String(advance().text.dropFirst())
        newlines()
        switch try selectorClauses(op, start) {
        case .none: break
        case .binding(let word): try bindingClause(word: word)
        case .accumulator: try accumulatorClause(op)
        case .key: try keyClause(op)
        case .direction: try direction()
        case .contains:
            if currentKind == .identifier && ["all", "any"].contains(current.text) { advanceKeyword() }
            _ = try expression()
        case .term: _ = try expression()
        case .count: try positiveInteger()
        case .choose: try chooseClauses()
        case .slice: try sliceClause(op)
        case .pattern: return try hasClause()
        }
        return false
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
    func bindingClause(word: String?) throws {
        _ = try bindingNames(.selectorBinding)
        if let word { try expectWord(word) } else { try expect(.projectionArrow) }
        _ = try expression()
    }

    func accumulatorClause(_ op: String) throws {
        _ = try identifier()
        role(.foldAccumulator)
        if op == "fold" {
            try expect(.be)
            _ = try expression()
        }
        try expect(.comma)
        newlines()
        _ = try bindingNames(.selectorBinding)
        try expect(.projectionArrow)
        newlines()
        _ = try expression()
    }

    func keyClause(_ op: String) throws {
        if op == "map" {
            _ = try bindingNames(.selectorBinding)
            try expectWord("by")
        } else {
            try expectWord("by")
            _ = try bindingNames(.selectorBinding)
            try expect(.projectionArrow)
        }
        _ = try expression()
        if op == "map" && match(.projectionArrow) { _ = try expression() }
        if op == "order" { try direction() }
    }

    func chooseClauses() throws {
        try positiveInteger()
        newlines()
        if matchWord("at") { try expect(.keywordRandom) }
        newlines()
        if current.isWord && (peek().isIdentifier("where") || peekKind() == .comma) {
            _ = try bindingNames(.selectorBinding)
            try expectWord("where")
            _ = try expression()
        }
        newlines()
        if matchWord("weighted") {
            try expectWord("by")
            _ = try bindingNames(.weightBinding)
            try expect(.projectionArrow)
            _ = try expression()
        }
    }

    func sliceClause(_ op: String) throws {
        if currentKind == .identifier && ["first", "last", "highest", "lowest"].contains(current.text) {
            advanceKeyword()
            try positiveInteger()
        } else if op == "take" {
            startNode(.dicePattern)
            try pattern()
            finishNode()
        } else {
            throw failure("Expected slice scope.")
        }
    }

    // Returns whether the clause is an object pattern rather than a dice pattern.
    func hasClause() throws -> Bool {
        guard currentKind == .leftBracket else {
            startNode(.dicePattern)
            try pattern()
            finishNode()
            return false
        }
        // As in C#, the outermost object pattern counts one recursion level, so the limit names the same pattern.
        if expressionDepth >= 32 { throw failure("Expression nesting limit exceeded.", code: GesSyntaxDiagnostic.sourceNestingExceeded) }
        expressionDepth += 1
        defer { expressionDepth -= 1 }
        let checkpoint = mark()
        objectPattern = true
        let start = index
        advance()
        let value = try collection(start, checkpoint)
        let root = value.pattern ?? Pattern(depth: value.depth, start: value.start, end: value.end, entries: nil)
        // Stopping where the first pattern enters level 32 names that pattern.
        if root.depth + 1 > 32 {
            var pattern = root
            for level in 1..<32 {
                guard let entries = pattern.entries, let next = entries.first(where: { $0.depth >= 32 - level }) else { break }
                pattern = next
            }
            throw nestingLimit(pattern.start, pattern.end)
        }
        return true
    }

    func direction() throws {
        newlines()
        guard currentKind == .identifier && ["ascending", "descending"].contains(current.text) else { throw failure("Expected sort direction.") }
        advanceKeyword()
    }

    func pattern() throws {
        if matchWord("full") {
            try expectWord("house")
            return
        }
        if matchWord("straight") { return }
        let t = advance()
        guard t.kind == .identifier, ["pair", "three", "four", "five", "six", "seven"].contains(t.text) else { throw failure("Expected dice pattern.", t) }
        role(.keyword)
        newlines()
        // `pair` takes an optional face; the larger counts take a face or `a kind`.
        if t.text == "pair" {
            if match(.of) {
                newlines()
                _ = try expression(15)
            }
            return
        }
        try expect(.of)
        newlines()
        if matchWord("a") {
            newlines()
            try expectWord("kind")
        } else {
            _ = try expression(15)
        }
    }
}
