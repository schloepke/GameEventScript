// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

@_spi(Compiler) import GameEventScriptRuntime

extension GesParser {
    func prefix() throws -> GesExpression {
        let start = current
        if ["-", "!", "not", "parse", "empty"].contains(current.syntaxText) {
            let op = advance().text
            let value = try expression(15)
            return try combined(.unary(op == "not" ? "!" : op, value), node(.literal(.nothing), start), value)
        }
        let unary = [
            "abs", "ln", "exp", "sqrt", "cbrt", "chance", "floor", "ceil", "truncate", "rad", "deg", "sin", "cos",
            "tan", "asin", "acos", "atan", "wrap", "round",
        ]
        if unary.contains(current.syntaxText) {
            var op = advance().text
            newlines()
            if op == "wrap" {
                try expect("degree")
                op = "wrapDegree"
            }
            if op == "round" {
                try expect("half")
                newlines()
                let mode = advance().syntaxText
                guard ["even", "up", "down"].contains(mode) else { throw failure("Expected rounding mode.", previous) }
                op = "roundHalf" + mode.prefix(1).uppercased() + mode.dropFirst()
            }
            let value = try expression(15)
            if op == "sqrt" || op == "cbrt" {
                return try combined(
                    .binary("^", value, node(.literal(.float(op == "sqrt" ? 0.5 : 1.0 / 3)), start)), value)
            }
            return node(.unary(op, value), start)
        }
        if ["atan2", "hypot", "distance", "length", "normalize", "dot", "cross", "angle"].contains(current.syntaxText) {
            var op = advance().text
            newlines()
            if op == "distance" && match("squared") { op = "distanceSquared" }
            if op == "length" {
                try expect("squared")
                op = "lengthSquared"
            }
            if op == "angle" {
                try expect("between")
                op = "angleBetween"
            }
            newlines()
            let args = try arguments()
            if args.contains(where: { $0.label != "_" }) { throw failure("Intrinsic arguments are positional.", start) }
            return node(.intrinsic(op, args.map(\.value)), start)
        }
        if match("clamp") {
            let value = try expression(15)
            newlines()
            try expect("between")
            let low = try expression(9)
            try expect("and")
            let high = try expression(9)
            return node(.intrinsic("clamp", [value, low, high]), start)
        }
        if ["min", "max"].contains(current.syntaxText) && peek().syntaxText == "of" {
            let op = advance().text
            newlines()
            try expect("of")
            var args = [try expression(9)]
            while match("and") { args.append(try expression(9)) }
            return node(.intrinsic(op, args), start)
        }
        if current.kind == "selector" && peek().syntaxText == "." {
            let (ns, function) = try extensionSymbol()
            newlines()
            var args: [GesArgument] = []
            if current.syntaxText == "(" {
                args = try arguments()
            } else if match("of") {
                args = [.init(label: "_", value: try expression(9))]
                while match("and") { args.append(.init(label: "_", value: try expression(9))) }
            } else if argumentLabel() {
                repeat { args.append(try argument()) } while argumentLabel()
            } else if ["number", "text", "tag", "constant", "type"].contains(current.kind)
                || current.kind == "word" && !Self.reserved.contains(current.syntaxText)
                || [
                    "-", "!", "[", "true", "false", "nothing", "parse", "random", "roll", "abs", "ln", "exp", "sqrt",
                    "cbrt",
                ].contains(current.syntaxText)
            {
                args = [.init(label: "_", value: try expression(15))]
            }
            return node(.extensionCall(ns, function, args), start)
        }
        var result = try primary()
        while true {
            if match(".") {
                newlines()
                result = try combined(.member(result, identifier()), result)
            } else if match("[") {
                newlines()
                let selection = try selector()
                newlines()
                try expect("]")
                result = try combined(.selector(result, selection), result)
            } else {
                break
            }
        }
        return result
    }
    func primary() throws -> GesExpression {
        let start = current
        if current.kind == "number" {
            let token = advance()
            guard let value = GameEventScriptCompilerSupport.number(token.text, percentage: token.text.hasSuffix("%"))
            else { throw failure("Invalid numeric literal.", token) }
            let result = node(.literal(value), start)
            result.decimalLiteral = token.text.contains(".") || value.kind == .float
            return result
        }
        if current.kind == "text" { return node(.literal(.text(advance().text)), start) }
        if current.kind == "tag" { return node(.literal(try .tag(String(advance().text.dropFirst()))), start) }
        if current.kind == "constant" { return node(.constant(String(advance().text.dropFirst())), start) }
        if match("true") { return node(.literal(.boolean(true)), start) }
        if match("false") { return node(.literal(.boolean(false)), start) }
        if match("nothing") { return node(.literal(.nothing), start) }
        if let value = ["pi": Double.pi, "e": 2.718281828459045, "tau": Double.pi * 2, "infinity": Double.infinity][
            current.syntaxText]
        {
            advance()
            return node(.literal(.float(value)), start)
        }
        if match("(") {
            let result = try expression()
            newlines()
            try expect(")")
            result.depth += 1
            if result.depth > 32 {
                throw failure("Expression nesting exceeded.", start, code: "parse.sourceNestingExceeded")
            }
            return result
        }
        if match("[") { return try collection(start) }
        if match("from") { return try rangeExpression(start) }
        if match("random") {
            newlines()
            if match("with") {
                let seed = try expression()
                return node(.seeded(seed, try expression()), start)
            }
            _ = match("from")
            let from = try expression(13)
            newlines()
            try expect("to")
            let to = try expression(13)
            return node(.random(from, to), start)
        }
        if match("series") {
            newlines()
            let kind = advance().syntaxText
            guard ["fibonacci", "factorial"].contains(kind) else { throw failure("Expected series kind.", previous) }
            return node(.series(kind), start)
        }
        if match("roll") {
            newlines()
            try expect("dice")
            newlines()
            let count = try positiveInteger()
            newlines()
            let sides: Int
            if match("d") {
                sides = try positiveInteger()
            } else {
                let t = advance()
                guard t.text.hasPrefix("d"), let n = Int(t.text.dropFirst()), n > 0 && n <= Int(Int32.max) else {
                    throw failure("Invalid dice sides.", t)
                }
                sides = n
            }
            return node(.dice(count, sides), start)
        }
        if current.kind == "type" {
            let type = try typeName()
            newlines()
            if type == "dice" && match("[") {
                newlines()
                var items: [GesExpression] = []
                if current.syntaxText != "]" {
                    repeat {
                        newlines()
                        let t = current
                        items.append(node(.literal(.integer(Int64(try positiveInteger()))), t))
                        newlines()
                    } while match(",")
                }
                try expect("]")
                return node(.constructor(type, [.init(label: "_", value: node(.list(items), start))]), start)
            }
            if type == "list" && match("[") {
                newlines()
                try expect(":select")
                newlines()
                let name = try identifier()
                newlines()
                let range = match("from")
                let sequence: GesExpression
                if range {
                    sequence = try rangeExpression(previous)
                } else {
                    try expect("in")
                    newlines()
                    if current.syntaxText == "from" { throw failure("Expected collection.") }
                    sequence = try expression()
                }
                newlines()
                var condition: GesExpression?
                if match("where") { condition = try expression() }
                newlines()
                try expect("=>")
                let value = try expression()
                newlines()
                try expect("]")
                return node(.generated(type, name, sequence, range, condition, value), start)
            }
            return node(.constructor(type, try arguments()), start)
        }
        if current.kind == "word" && !Self.reserved.contains(current.syntaxText) || current.kind == "message" {
            let upper = current.kind == "message"
            let name = advance().text
            if peek(0).syntaxText == "(" {
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
        try expect("to")
        let to = try expression(13)
        let step = match("step") ? try expression() : nil
        return node(.range(from, to, step), start)
    }
    func positiveInteger() throws -> Int {
        let t = current
        guard t.kind == "number", let n = GameEventScriptCompilerSupport.number(t.text)?.integerValue,
            !t.text.hasSuffix("%"), !t.text.hasSuffix("m"), !t.text.hasSuffix("s"), !t.text.hasSuffix("°"),
            n > 0 && n <= Int32.max
        else { throw failure("Expected positive Int32 literal.") }
        advance()
        return Int(n)
    }
    func isMessageArguments() -> Bool {
        var i = index + 1
        while i < tokens.count && tokens[i].kind == "newline" { i += 1 }
        i += 1
        while i < tokens.count && tokens[i].kind == "newline" { i += 1 }
        return i < tokens.count && tokens[i].syntaxText == ":"
    }
    func argumentLabel() -> Bool { current.kind == "word" && peek().syntaxText == ":" }
    func argument() throws -> GesArgument {
        let label: String
        if argumentLabel() {
            label = advance().text
            newlines()
            try expect(":")
        } else {
            label = "_"
        }
        return .init(label: label, value: try expression())
    }
    func arguments() throws -> [GesArgument] {
        try expect("(")
        newlines()
        var result: [GesArgument] = []
        if match(")") { return result }
        while true {
            result.append(try argument())
            newlines()
            if match(",") {
                newlines()
                continue
            }
            if argumentLabel() { continue }
            try expect(")")
            return result
        }
    }
    func collection(_ start: GesToken) throws -> GesExpression {
        newlines()
        if match(":") {
            newlines()
            try expect("]")
            return node(.map([]), start)
        }
        if match("]") { return node(.list([]), start) }
        if argumentLabel() {
            var pairs: [(String, GesExpression)] = []
            repeat {
                newlines()
                let t = current
                let key = try identifier()
                try expect(":")
                newlines()
                let value =
                    current.syntaxText == "," || current.syntaxText == "]"
                    ? node(.literal(.boolean(true)), t) : try expression()
                pairs.append((key, value))
                newlines()
            } while match(",")
            try expect("]")
            return node(.map(pairs), start)
        }
        var items: [GesExpression] = []
        repeat {
            items.append(try expression())
            newlines()
        } while match(",")
        try expect("]")
        return node(.list(items), start)
    }
    func extensionSymbol() throws -> (String, String) {
        let t = advance()
        let ns = String(t.text.dropFirst())
        newlines()
        try expect(".")
        newlines()
        guard current.kind == "word" else { throw failure("Expected extension function.") }
        let function = advance().text
        if ["integer", "degree"].contains(ns) { throw failure("Use direct math intrinsics.", t) }
        return (ns, function)
    }
    func selector() throws -> GesSelector {
        let start = current
        if current.kind != "selector" { return .init(operation: "index", expressions: [try expression()]) }
        let op = String(advance().text.dropFirst())
        newlines()
        var s = GesSelector(operation: op)
        switch op {
        case "keys", "values", "entries", "shuffle", "reverse": break
        case "any", "all", "filter":
            s.name = try identifier()
            try expect("where")
            s.expressions = [try expression()]
        case "count", "sum", "average":
            if current.syntaxText == "]" { break }
            s.name = try identifier()
            try expect(op == "count" ? "where" : "=>")
            s.expressions = [try expression()]
        case "select", "min", "max", "highest", "lowest":
            s.name = try identifier()
            try expect("=>")
            s.expressions = [try expression()]
            if op == "highest" { s.operation = "max" }
            if op == "lowest" { s.operation = "min" }
        case "first", "last", "single":
            if current.syntaxText == "]" { break }
            s.name = try identifier()
            try expect("where")
            s.expressions = [try expression()]
        case "map", "group", "order", "distinct":
            if op == "distinct" && current.syntaxText == "]" { break }
            if op == "map" {
                s.name = try identifier()
                try expect("by")
            } else {
                try expect("by")
                s.name = try identifier()
                try expect("=>")
            }
            s.expressions = [try expression()]
            if op == "map" && match("=>") { s.expressions.append(try expression()) }
            if op == "order" { s.mode = try direction() }
        case "sort": s.mode = try direction()
        case "contains":
            if ["all", "any"].contains(current.syntaxText) { s.mode = advance().text }
            s.expressions = [try expression()]
        case "term": s.expressions = [try expression()]
        case "draw": s.count = try positiveInteger()
        case "choose":
            s.name = ""
            s.count = try positiveInteger()
            newlines()
            if match("at") {
                try expect("random")
                s.mode = "random"
            }
            newlines()
            if peek().syntaxText == "where" {
                s.name = try identifier()
                try expect("where")
                s.expressions = [try expression()]
            }
            newlines()
            if match("weighted") {
                try expect("by")
                s.weightName = try identifier()
                try expect("=>")
                if s.expressions.isEmpty { s.expressions = [node(.literal(.boolean(true)), start)] }
                s.expressions.append(try expression())
            }
        case "take", "drop":
            if ["first", "last", "highest", "lowest"].contains(current.syntaxText) {
                s.mode = advance().text
                s.count = try positiveInteger()
            } else if op == "take" {
                s = try pattern(operation: "takePattern")
            } else {
                throw failure("Expected slice scope.")
            }
        case "has":
            if match("[") {
                s.operation = "objectMatch"
                let value = try collection(previous)
                func patternDepth(_ value: GesExpression) -> Int {
                    if case .map(let entries) = value.kind {
                        return 1 + (entries.map { patternDepth($0.1) }.max() ?? 0)
                    }
                    return value.depth
                }
                if patternDepth(value) + 1 > 32 {
                    throw failure("Expression nesting exceeded.", start, code: "parse.sourceNestingExceeded")
                }
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
        guard ["ascending", "descending"].contains(current.syntaxText) else {
            throw failure("Expected sort direction.")
        }
        return advance().text
    }
    func pattern(operation: String) throws -> GesSelector {
        var s = GesSelector(operation: operation)
        if match("full") {
            try expect("house")
            s.mode = "fullHouse"
            return s
        }
        if match("straight") {
            s.mode = "straight"
            return s
        }
        let t = advance()
        guard let count = ["pair": 2, "three": 3, "four": 4, "five": 5, "six": 6, "seven": 7][t.syntaxText] else {
            throw failure("Expected dice pattern.", t)
        }
        s.count = count
        s.mode = "kind"
        newlines()
        if t.text != "pair" { try expect("of") } else if !match("of") { return s }
        if match("a") {
            try expect("kind")
        } else {
            s.mode = "face"
            s.expressions = [try expression(15)]
        }
        return s
    }
}
