// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

@_spi(Compiler) import GameEventScriptRuntime

extension GesParser {
    func prefix() throws -> GesExpression {
        let start = current
        if [.operatorMinus, .operatorNot, .keywordParse, .empty].contains(current.kind) {
            let op = advance().kind
            let value = try expression(15)
            let unary: GesUnaryOperator = op == .operatorMinus ? .negate : op == .keywordParse ? .parse : op == .empty ? .empty : .not
            // From the operator to the last consumed token, as C# WithRange(node, opToken, Previous).
            return try combined(.unary(unary, value), node(.literal(.nothing), start))
        }
        let unary: Set<GesTokenKind> = [
            .intrinsicAbs, .intrinsicLn, .intrinsicExp, .intrinsicSqrt, .intrinsicCbrt, .intrinsicChance, .intrinsicFloor, .intrinsicCeil, .intrinsicTruncate, .intrinsicRad, .intrinsicDeg,
            .intrinsicSin, .intrinsicCos, .intrinsicTan, .intrinsicAsin, .intrinsicAcos, .intrinsicAtan, .intrinsicWrap, .intrinsicRound,
        ]
        if unary.contains(current.kind) {
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
            let value = try expression(15)
            // The root power spans from the operator, as C# CreateRootPowerExpression ranges it.
            if op == "sqrt" || op == "cbrt" { return try combined(.binary(.power, value, node(.literal(.float(op == "sqrt" ? 0.5 : 1.0 / 3)), start)), node(.literal(.nothing), start)) }
            guard let unary = GesUnaryOperator(prefixText: op) else { throw failure("Expected an operator.", start) }
            return node(.unary(unary, value), start)
        }
        if [.intrinsicAtan2, .intrinsicHypot, .intrinsicDistance, .intrinsicLength, .intrinsicNormalize, .intrinsicDot, .intrinsicCross, .intrinsicAngle].contains(current.kind) {
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
            let args = try arguments()
            if args.contains(where: { $0.label != "_" }) { throw failure("Intrinsic arguments are positional.", start) }
            return node(.intrinsic(op, args.map(\.value)), start)
        }
        if match(.clamp) {
            let value = try expression(15)
            newlines()
            try expectWord("between")
            let low = try expression(9)
            try expect(.operatorAnd)
            let high = try expression(9)
            return node(.intrinsic("clamp", [value, low, high]), start)
        }
        if [.min, .max].contains(current.kind) && peek().kind == .of {
            let op = advance().text
            newlines()
            try expect(.of)
            var args = [try expression(9)]
            while !isForClauseStart() && match(.operatorAnd) { args.append(try expression(9)) }
            return node(.intrinsic(op, args), start)
        }
        if current.kind.isSelector && peek().kind == .dot {
            let (ns, function) = try extensionSymbol()
            newlines()
            var args: [GesArgument] = []
            let parenthesized = current.kind == .leftParen
            if parenthesized {
                args = try arguments()
            } else if match(.of) {
                args = [.init(label: "_", value: try expression(9))]
                while !isForClauseStart() && match(.operatorAnd) { args.append(.init(label: "_", value: try expression(9))) }
            } else if argumentLabel() {
                repeat { args.append(try argument()) } while argumentLabel()
            } else if !(choiceConditionDepth == delimiterDepth && current.isIdentifier("then"))
                && (current.isNumber || [.text, .tag, .constantReference, .typeName].contains(current.kind) || current.isName
                    || [.operatorMinus, .operatorNot, .leftBracket, .`true`, .`false`, .nothing, .keywordParse, .keywordRandom, .roll, .intrinsicAbs, .intrinsicLn, .intrinsicExp, .intrinsicSqrt, .intrinsicCbrt].contains(current.kind))
            {
                args = [.init(label: "_", value: try expression(15))]
            }
            let result = node(.extensionCall(ns, function, args), start)
            return parenthesized ? try postfixSuffixes(result) : result
        }
        return try postfixSuffixes(primary())
    }

    func postfixSuffixes(_ base: GesExpression) throws -> GesExpression {
        var result = base
        while true {
            if match(.dot) {
                newlines()
                result = try ranged(.member(result, identifier()), result)
            } else if match(.leftBracket) {
                newlines()
                result = try pipelineSteps(result)
                newlines()
                try expect(.rightBracket)
            } else {
                break
            }
        }
        return result
    }

    func sendExpression(_ publish: Bool, _ start: GesToken) throws -> GesExpression {
        newlines()
        let delay = matchWord("after") ? try expression() : nil
        let message: GesExpression
        if current.kind == .message {
            let token = advance()
            let args = current.kind == .leftParen ? try arguments() : []
            message = node(.message(token.text, args), token)
        } else {
            message = try expression(15)
        }
        var tags: [GesExpression] = []
        if match(.with) { repeat { tags.append(try expression(15)) } while match(.comma) }
        return node(.send(publish, message, tags, delay), start)
    }

    func primary() throws -> GesExpression {
        if match(.`is`) {
            let start = previous
            newlines()
            let negated = match(.operatorNot)
            newlines()
            let name = try identifier()
            let value = node(.predicate(nil, name), start)
            return negated ? node(.unary(.not, value), start) : value
        }
        if current.kind == .emit || current.kind == .publish {
            let start = advance()
            return try sendExpression(start.kind == .publish, start)
        }
        let start = current
        if current.isNumber {
            let token = advance()
            guard let value = GesCompilerSupport.number(token.text, percentage: token.text.hasSuffix("%")) else { throw failure("Invalid numeric literal.", token) }
            let result = node(.literal(value), start)
            result.decimalLiteral = token.text.contains(".") || value.valueKind == .float
            return result
        }
        if current.kind == .text { return node(.literal(.text(advance().text)), start) }
        if current.kind == .tag { return node(.literal(try .tag(String(advance().text.dropFirst()))), start) }
        if current.kind == .constantReference { return node(.constant(String(advance().text.dropFirst())), start) }
        if match(.`true`) { return node(.literal(.boolean(true)), start) }
        if match(.`false`) { return node(.literal(.boolean(false)), start) }
        if match(.nothing) { return node(.literal(.nothing), start) }
        if let value = [GesTokenKind.mathConstantPi: Double.pi, .mathConstantE: 2.718281828459045, .mathConstantTau: Double.pi * 2, .mathConstantInfinity: Double.infinity][current.kind] {
            advance()
            return node(.literal(.float(value)), start)
        }
        if match(.leftParen) {
            let result = try expression()
            newlines()
            try expect(.rightParen)
            result.depth += 1
            if result.depth > 32 { throw failure("Expression nesting exceeded.", start, code: "parse.sourceNestingExceeded") }
            return result
        }
        if match(.leftBracket) { return try collection(start) }
        if matchWord("from") { return try rangeExpression(start) }
        if match(.keywordRandom) {
            newlines()
            if match(.with) {
                // From `with`, as C# starts the seeded random expression.
                let withToken = previous
                let seed = try expression()
                return node(.seeded(seed, try expression()), withToken)
            }
            _ = matchWord("from")
            let from = try expression(13)
            newlines()
            try expect(.to)
            let to = try expression(13)
            return node(.random(from, to), start)
        }
        if match(.series) {
            newlines()
            let token = advance()
            guard token.kind == .fibonacci || token.kind == .factorial else { throw failure("Expected series kind.", previous) }
            return node(.series(token.text), start)
        }
        if match(.roll) {
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
        if current.kind == .typeName {
            let type = try typeName()
            newlines()
            if type == "dice" && match(.leftBracket) {
                // The value list spans its brackets, as in C#; the constructor spans the whole literal.
                let bracket = previous
                newlines()
                var items: [GesExpression] = []
                if current.kind != .rightBracket {
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
            if type == "list" && match(.leftBracket) {
                newlines()
                try expect(.selectorSelect)
                let selectToken = previous
                newlines()
                let name = try identifier()
                newlines()
                let range = matchWord("from")
                let sequence: GesExpression
                if range {
                    sequence = try rangeExpression(previous)
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
            if type == "series", current.kind == .leftParen, [.fibonacci, .factorial].contains(peek().kind) {
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
            if type == "handler", current.kind == .leftParen, peek().kind == .message {
                advance()
                newlines()
                let name = advance()
                try expect(.leftParen)
                newlines()
                var parameters: [GesParameter] = []
                if current.kind != .rightParen {
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
            if type == "message", current.kind == .leftParen, peek().kind == .message {
                advance()
                newlines()
                let name = advance()
                let message = node(.message(name.text, try arguments()), name)
                var args: [GesArgument] = [.init(label: "_", value: message)]
                if match(.with) {
                    var tags = [try expression()]
                    while match(.comma) { tags.append(try expression()) }
                    args.append(.init(label: "tags", value: unranged(.list(tags), start)))
                }
                try expect(.rightParen)
                return node(.constructor(type, args), start)
            }
            return node(.constructor(type, try arguments()), start)
        }
        if current.isName || current.kind == .message {
            let upper = current.kind == .message
            let name = advance().text
            if peek(0).kind == .leftParen {
                newlines()
                if upper && !isMessageArguments() { return node(.handler(name, try parameters(false)), start) }
                let args = try arguments()
                return node(upper ? .message(name, args) : .call(name, args), start)
            }
            if upper { throw failure("Expected handler signature.", start) }
            return node(.name(name), start)
        }
        throw failure("Unexpected token '\(current.text)'.")
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

    func isMessageArguments() -> Bool {
        var i = index + 1
        while i < tokens.count && tokens[i].kind == .newLine { i += 1 }
        i += 1
        while i < tokens.count && tokens[i].kind == .newLine { i += 1 }
        return i < tokens.count && tokens[i].kind == .colon
    }

    // Labels may be spelled like keywords, such as `to:`.
    func argumentLabel() -> Bool { current.isWord && peek().kind == .colon }

    func argument() throws -> GesArgument {
        let label: String
        if argumentLabel() {
            label = advance().text
            newlines()
            try expect(.colon)
        } else {
            label = "_"
        }
        return .init(label: label, value: try expression())
    }

    func arguments() throws -> [GesArgument] {
        try expect(.leftParen)
        newlines()
        var result: [GesArgument] = []
        if match(.rightParen) { return result }
        while true {
            result.append(try argument())
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

    func collection(_ start: GesToken) throws -> GesExpression {
        newlines()
        if isCombinedSelector {
            let result = try pipelineSteps(nil)
            newlines()
            try expect(.rightBracket)
            return result
        }
        newlines()
        if match(.colon) {
            newlines()
            try expect(.rightBracket)
            return node(.map([]), start)
        }
        if match(.rightBracket) { return node(.list([]), start) }
        // A non-empty literal starts at the last token before its first entry, which is a skipped line break for a
        // multi-line literal, as C# takes Previous after skipping new lines.
        let literalStart = previous
        if argumentLabel() {
            var pairs: [(String, GesExpression)] = []
            repeat {
                newlines()
                let t = current
                let key = try identifier()
                try expect(.colon)
                newlines()
                let value = current.kind == .comma || current.kind == .rightBracket ? node(.literal(.boolean(true)), t) : try expression()
                pairs.append((key, value))
                newlines()
            } while match(.comma)
            try expect(.rightBracket)
            return node(.map(pairs), literalStart)
        }
        var items: [GesExpression] = []
        repeat {
            items.append(try expression())
            newlines()
        } while match(.comma)
        try expect(.rightBracket)
        return node(.list(items), literalStart)
    }

    func extensionSymbol() throws -> (String, String) {
        let t = advance()
        let ns = String(t.text.dropFirst())
        newlines()
        let dot = current
        try expect(.dot)
        newlines()
        guard current.isWord else { throw failure("Expected extension function.") }
        let functionToken = advance()
        if t.endLine != dot.line || t.endColumn != dot.column || dot.endLine != functionToken.line || dot.endColumn != functionToken.column { throw failure("Extension names must be contiguous.", t) }
        let function = functionToken.text
        if ["integer", "degree"].contains(ns) { throw failure("Use direct math intrinsics.", t) }
        return (ns, function)
    }

    func selector() throws -> GesSelector {
        let start = current
        if !current.kind.isSelector { return .init(operation: "index", expressions: [try expression()]) }
        let op = String(advance().text.dropFirst())
        newlines()
        var s = GesSelector(operation: op)
        switch op {
        case "keys", "values", "entries", "shuffle", "reverse": break
        case "any", "all", "filter":
            s.names = try bindingNames()
            s.name = s.names[0]
            try expectWord("where")
            s.expressions = [try expression()]
        case "count", "sum", "average":
            if current.kind == .rightBracket || isPipelineSelector { break }
            s.names = try bindingNames()
            s.name = s.names[0]
            if op == "count" { try expectWord("where") } else { try expect(.projectionArrow) }
            s.expressions = [try expression()]
        case "fold", "reduce":
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
        case "foreach", "select", "min", "max", "highest", "lowest":
            s.names = try bindingNames()
            s.name = s.names[0]
            try expect(.projectionArrow)
            s.expressions = [try expression()]
            if op == "highest" { s.operation = "max" }
            if op == "lowest" { s.operation = "min" }
        case "first", "last", "single":
            if current.kind == .rightBracket || isPipelineSelector { break }
            s.names = try bindingNames()
            s.name = s.names[0]
            try expectWord("where")
            s.expressions = [try expression()]
        case "map", "group", "order", "distinct":
            if op == "distinct" && (current.kind == .rightBracket || isPipelineSelector) { break }
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
        case "sort": s.mode = try direction()
        case "contains":
            if current.kind == .identifier && ["all", "any"].contains(current.text) { s.mode = advance().text }
            s.expressions = [try expression()]
        case "term": s.expressions = [try expression()]
        case "draw": s.count = try positiveInteger()
        case "choose":
            s.name = ""
            s.count = try positiveInteger()
            newlines()
            if matchWord("at") {
                try expect(.keywordRandom)
                s.mode = "random"
            }
            newlines()
            if current.isWord && (peek().isIdentifier("where") || peek().kind == .comma) {
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
        case "take", "drop":
            if current.kind == .identifier && ["first", "last", "highest", "lowest"].contains(current.text) {
                s.mode = advance().text
                s.count = try positiveInteger()
            } else if op == "take" {
                s = try pattern(operation: "takePattern")
            } else {
                throw failure("Expected slice scope.")
            }
        case "has":
            if match(.leftBracket) {
                s.operation = "objectMatch"
                let value = try collection(previous)

                func patternDepth(_ value: GesExpression) -> Int {
                    if case .map(let entries) = value.kind { return 1 + (entries.map { patternDepth($0.1) }.max() ?? 0) }
                    return value.depth
                }

                if patternDepth(value) + 1 > 32 { throw failure("Expression nesting exceeded.", start, code: "parse.sourceNestingExceeded") }
                s.expressions = [value]
            } else {
                s = try pattern(operation: "hasPattern")
            }
        default: throw failure("Unknown selector.", start)
        }
        return s
    }

    func direction() throws -> String {
        newlines()
        guard current.kind == .identifier && ["ascending", "descending"].contains(current.text) else { throw failure("Expected sort direction.") }
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
