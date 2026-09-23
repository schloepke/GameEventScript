// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

@_spi(Compiler) import GameEventScriptRuntime

final class GesParser {
    let source: GesSource
    var tokens: [GesToken] = []
    var index = 0, expressionDepth = 0, statementDepth = 0
    var module = ""

    init(source: GesSource) {
        self.source = source
        var lexer = GesLexer(source.text)
        repeat { tokens.append(lexer.read()) } while tokens.last!.kind != "eof"
    }

    var current: GesToken { tokens[index] }
    var previous: GesToken { tokens[max(0, index - 1)] }

    @discardableResult func advance() -> GesToken {
        let t = current
        if t.kind != "eof" { index += 1 }
        return t
    }

    func peek(_ distance: Int = 1) -> GesToken {
        var i = index
        var count = 0
        while i < tokens.count - 1 {
            if tokens[i].kind != "newline" {
                if count == distance { return tokens[i] }
                count += 1
            }
            i += 1
        }
        return tokens[i]
    }

    func match(_ text: String) -> Bool {
        if current.syntaxText == text {
            advance()
            return true
        }
        return false
    }

    @discardableResult func expect(_ text: String) throws -> GesToken {
        if current.syntaxText != text { throw failure("Expected '\(text)'.") }
        return advance()
    }

    func newlines() { while current.kind == "newline" { advance() } }

    func separators() { while current.kind == "newline" || current.syntaxText == ";" { advance() } }

    func location(_ start: GesToken, _ end: GesToken? = nil) -> GameEventScriptSourceLocation {
        let last = end ?? start
        return .init(sourceName: source.name, line: start.line, column: start.column, endLine: last.endLine, endColumn: last.endColumn, moduleName: module, sourceID: source.id)
    }

    func failure(_ message: String, _ token: GesToken? = nil, code: String = "parse.syntax") -> GameEventScriptCompileError { compileError(.parse, code, message, location(token ?? current)) }

    func identifier() throws -> String {
        guard current.kind == "word", !Self.reserved.contains(current.syntaxText) else { throw failure("Expected an identifier.") }
        return advance().text
    }

    static let reserved: Set<String> = Set(
        "on record predicate function emit publish matching without let as be when otherwise has parse empty if else for in of starts ends with is numeric or xor and div mod rem not to true false nothing pi e tau infinity abs ln exp sqrt cbrt chance floor ceil truncate rad deg wrap round sin cos tan asin acos atan atan2 hypot distance squared length normalize dot cross angle random series fibonacci factorial clamp min max zip roll default module constant"
            .split(separator: " ").map(String.init)
    )

    func typeName() throws -> String {
        guard current.kind == "type" else { throw failure("Expected a type name.") }
        var name = String(advance().text.dropFirst())
        let builtins = ["Nothing", "Number", "Boolean", "Percentage", "Vector", "Point", "Series", "Tag", "Text", "List", "Range", "Message", "Handler", "Map", "Dice", "Record"]
        if builtins.contains(name) { name = name.lowercased() }
        if name == "Quantity", current.syntaxText == "(", peek().kind == "word" || peek().syntaxText == "degree", peek(2).syntaxText == ")" {
            advance()
            newlines()
            guard current.kind == "word" || current.syntaxText == "degree" else { throw failure("Expected a unit name.") }
            let unit = advance().text
            newlines()
            try expect(")")
            name = "quantity:" + unit
        }
        return name
    }

    func parameters(_ allowType: Bool = true) throws -> [GesParameter] {
        try expect("(")
        newlines()
        var result: [GesParameter] = []
        if match(")") { return result }
        repeat {
            newlines()
            let start = current
            let unlabeled = match("_")
            newlines()
            let name = try identifier()
            newlines()
            var type: String?
            if allowType && match("as") {
                newlines()
                type = try typeName()
                newlines()
            }
            result.append(.init(label: unlabeled ? "_" : name, name: name, type: type, location: location(start, previous)))
        } while match(",")
        try expect(")")
        return result
    }

    func parse() throws -> GesModule {
        separators()
        let start = current
        if match("module") {
            newlines()
            module = try identifier()
            while match(".") { module += "." + (try identifier()) }
            if !module.split(separator: ".").allSatisfy({ $0.unicodeScalars.allSatisfy { GesLexer.lower($0) || GesLexer.digit($0) } }) { throw failure("Invalid module name.", previous) }
        }
        separators()
        var definitions: [GesDefinition] = []
        var constants: [(String, GesExpression)] = []
        var records: [GesRecord] = []
        while current.kind != "eof" {
            let begin = current
            if match("constant") {
                guard current.kind == "constant" else { throw failure("Expected constant name.") }
                let name = String(advance().text.dropFirst())
                try expect("be")
                newlines()
                let value = try expression(15)
                switch value.kind {
                case .literal: break
                case .unary("-", let operand): guard case .literal(let number) = operand.kind, [.integer, .float, .percentage].contains(number.kind) else { throw failure("Expected numeric literal after '-'.", begin) }
                default: throw failure("Expected scalar literal.", begin)
                }
                constants.append((name, value))
            } else if match("record") {
                let name = try typeName()
                try expect("as")
                newlines()
                try expect("{")
                separators()
                var fields: [GesField] = []
                while current.syntaxText != "}" {
                    let fieldStart = current
                    let unlabeled = match("_")
                    let field = try identifier()
                    try expect(":")
                    newlines()
                    let type = try typeName()
                    newlines()
                    var low: GesExpression?
                    var high: GesExpression?
                    var computed: GesExpression?
                    if match("clamped") {
                        try expect("between")
                        low = try expression(9)
                        try expect("and")
                        high = try expression(9)
                        newlines()
                    }
                    if match("computed") {
                        try expect("by")
                        computed = try expression()
                    }
                    if unlabeled && computed != nil { throw failure("Computed field cannot be a parameter.", fieldStart) }
                    fields.append(.init(name: field, type: type, label: computed == nil ? (unlabeled ? "_" : field) : nil, minimum: low, maximum: high, computed: computed, location: location(fieldStart, previous)))
                    _ = match(",")
                    separators()
                }
                try expect("}")
                records.append(.init(name: name, fields: fields, location: location(begin, previous)))
            } else if current.syntaxText == "function" || current.syntaxText == "predicate" {
                let kind = advance().text
                let name = try identifier()
                let params = try parameters()
                try expect("be")
                newlines()
                let body = try expression()
                definitions.append(.init(kind: kind, name: name, parameters: params, expression: body, statements: [], required: [], excluded: [], location: location(begin, previous)))
            } else {
                try expect("on")
                newlines()
                guard current.kind == "message" || ["initialization", "undeliverable"].contains(current.syntaxText) else { throw failure("Expected message name.") }
                let name = advance().text
                var kind = "handler"
                var params: [GesParameter] = []
                if match("as") {
                    newlines()
                    let p = current
                    let local = try identifier()
                    kind = "messageNameHandler"
                    params = [.init(label: "message", name: local, type: "message", location: location(p))]
                } else if current.syntaxText == "(" {
                    params = try parameters()
                }
                var required: [String] = []
                var excluded: [String] = []
                while true {
                    newlines()
                    guard current.syntaxText == "matching" || current.syntaxText == "without" else { break }
                    let include = advance().text == "matching"
                    repeat {
                        newlines()
                        guard current.kind == "tag" else { throw failure("Expected tag literal.") }
                        let tag = String(advance().text.dropFirst())
                        if include { if !required.contains(tag) { required.append(tag) } } else if !excluded.contains(tag) { excluded.append(tag) }
                        newlines()
                    } while match(",")
                }
                try expect("{")
                let body = try statements()
                try expect("}")
                definitions.append(.init(kind: kind, name: name, parameters: params, expression: nil, statements: body, required: required, excluded: excluded, location: location(begin, previous)))
            }
            if current.kind != "eof" && current.kind != "newline" && current.syntaxText != ";" { throw failure("Expected a declaration separator.") }
            separators()
        }
        return .init(name: module, definitions: definitions, constants: constants, records: records, location: location(start, previous))
    }

    func statements() throws -> [GesStatement] {
        separators()
        var result: [GesStatement] = []
        while current.syntaxText != "}" && current.kind != "eof" {
            result.append(try statement())
            if current.syntaxText != "}" && current.kind != "newline" && current.syntaxText != ";" { throw failure("Expected a statement separator.") }
            separators()
        }
        return result
    }

    func body() throws -> [GesStatement] {
        if statementDepth >= 32 { throw failure("Statement nesting limit exceeded.", code: "parse.sourceNestingExceeded") }
        statementDepth += 1
        defer { statementDepth -= 1 }
        newlines()
        if match("{") {
            let result = try statements()
            try expect("}")
            return result
        }
        return [try statement()]
    }

    func statement() throws -> GesStatement {
        let start = current
        let kind: GesStatement.Kind
        if current.syntaxText == "emit" || current.syntaxText == "publish" {
            let publish = advance().text == "publish"
            newlines()
            if current.syntaxText == "after" {
                let send = try sendExpression(publish, start)
                return GesStatement(kind: .expression(send), location: send.location)
            }
            let message: GesExpression
            if current.kind == "message" {
                let name = advance().text
                let args = current.syntaxText == "(" ? try arguments() : []
                message = node(.message(name, args), start)
            } else {
                message = try expression()
            }
            var tags: [GesExpression] = []
            if match("with") { repeat { tags.append(try expression()) } while match(",") }
            kind = .publish(publish, message, tags)
        } else if match("let") {
            let name = try identifier()
            try expect("be")
            kind = .letBinding(name, try expression())
        } else if match("if") {
            var conditions: [(String?, GesExpression)] = []
            repeat {
                newlines()
                if current.syntaxText == "{" && !conditions.isEmpty { break }
                var binding: String?
                if match("let") {
                    binding = try identifier()
                    try expect("be")
                }
                conditions.append((binding, try expression()))
                newlines()
            } while match(";")
            let then = try body()
            var otherwise: [GesStatement] = []
            if peek(0).syntaxText == "else" {
                newlines()
                try expect("else")
                otherwise = try body()
            }
            kind = .condition(conditions, then, otherwise)
        } else if match("for") {
            let name = try identifier()
            newlines()
            let range = match("from")
            let sequence: GesExpression
            if range {
                sequence = try rangeExpression(start)
            } else {
                try expect("in")
                newlines()
                if current.syntaxText == "from" { throw failure("Direct range requires 'from'.") }
                sequence = try expression()
            }
            kind = .loop(name, sequence, range, try body())
        } else if current.syntaxText == "random" && peek().syntaxText == "with" {
            advance()
            newlines()
            try expect("with")
            let seed = try expression()
            kind = .seeded(seed, try body())
        } else {
            kind = .expression(try expression())
        }
        return .init(kind: kind, location: location(start, previous))
    }

    func node(_ kind: GesExpression.Kind, _ start: GesToken) -> GesExpression { .init(kind, location(start, previous)) }

    func combined(_ kind: GesExpression.Kind, _ first: GesExpression, _ last: GesExpression? = nil) throws -> GesExpression {
        let a = first.location
        let b = last?.location ?? location(previous)
        let depth = max(first.depth, last?.depth ?? 0) + 1
        if depth > 32 { throw failure("Expression nesting limit exceeded.", code: "parse.sourceNestingExceeded") }
        return .init(kind, .init(sourceName: a.sourceName, line: a.line, column: a.column, endLine: b.endLine, endColumn: b.endColumn, moduleName: module, sourceID: source.id), depth: depth)
    }

    static let precedence: [String: Int] = [
        "->": 1, "default": 2, "|": 3, "&": 4, "zip": 5, "or": 6, "xor": 7, "and": 8, "=": 9, "<>": 9, "in": 10, "not in": 10, "starts": 10, "ends": 10, "has": 10, "as": 11, "is": 11, "<": 12, ">": 12, "<=": 12, ">=": 12, "+": 13, "-": 13, "*": 14,
        "/": 14, "div": 14, "mod": 14, "rem": 14, "^": 16,
    ]

    func expression(_ minimum: Int = 0) throws -> GesExpression {
        if expressionDepth >= 32 { throw failure("Expression nesting limit exceeded.", code: "parse.sourceNestingExceeded") }
        expressionDepth += 1
        defer { expressionDepth -= 1 }
        newlines()
        var left = try prefix()
        while true {
            if minimum <= 16 && current.kind == "superscript" {
                let exponent = node(.literal(.integer(Int64(advance().text)!)), previous)
                left = try combined(.binary("^", left, exponent), left, exponent)
                continue
            }
            if minimum <= 14, case .literal(let value) = left.kind, value.isNumeric && !value.hasUnit && previous.end == current.start,
                current.kind == "word" && !Self.reserved.contains(current.syntaxText) || minimum <= 14 && previous.end == current.start && ["pi", "e", "tau", "infinity"].contains(current.syntaxText)
            {
                let right = try expression(15)
                left = try combined(.binary("*", left, right), left, right)
                continue
            }
            guard let precedence = Self.precedence[current.syntaxText], precedence >= minimum else { break }
            let op = advance().text
            newlines()
            if op == "as" {
                left = try combined(.cast(left, typeName()), left)
                continue
            }
            if op == "is" {
                left = try isExpression(left)
                continue
            }
            if op == "has" {
                try expect("value")
                left = try combined(.unary("hasValue", left), left)
                continue
            }
            var actual = op
            if op == "starts" || op == "ends" {
                try expect("with")
                actual += "With"
                newlines()
            }
            if op == "in" && current.syntaxText == "values" && peek().syntaxText == "of" {
                advance()
                newlines()
                try expect("of")
                actual = "inValues"
            }
            let right = try expression(op == "^" ? 15 : op == "->" ? precedence : precedence + 1)
            left = try combined(.binary(actual, left, right), left, right)
        }
        if minimum == 0 && match("when") {
            var branches = [(left, try expression(1))]
            while match(",") {
                newlines()
                if current.syntaxText == "otherwise" { break }
                _ = match("or")
                newlines()
                let value = try expression(1)
                try expect("when")
                branches.append((value, try expression(1)))
            }
            newlines()
            try expect("otherwise")
            let fallback = try expression(1)
            left = try combined(.choice(branches, fallback), left, fallback)
        }
        return left
    }

    func isExpression(_ input: GesExpression) throws -> GesExpression {
        let negated = match("not") || match("!")
        newlines()
        var value: GesExpression
        if current.kind == "type" {
            value = try combined(.check(input, typeName()), input)
        } else if ["numeric", "integer", "fractional", "nothing"].contains(current.syntaxText) {
            let type = advance().text
            value = try combined(.check(input, type), input)
        } else if match("empty") {
            value = try combined(.unary("empty", input), input)
        } else if current.kind == "selector" && peek().syntaxText == "." {
            let (ns, function) = try extensionSymbol()
            value = try combined(.unary("predicate", .init(.extensionCall(ns, function, [.init(label: "_", value: input)]), input.location)), input)
        } else if match("at") {
            let mode = advance().syntaxText
            guard mode == "least" || mode == "most" else { throw failure("Expected least or most.", previous) }
            let right = try expression(13)
            value = try combined(.binary(mode == "least" ? ">=" : "<=", input, right), input, right)
        } else if current.syntaxText == "less" || current.syntaxText == "more" {
            let mode = advance().syntaxText
            try expect("than")
            let right = try expression(13)
            value = try combined(.binary(mode == "less" ? "<" : ">", input, right), input, right)
        } else if current.kind == "word" && !Self.reserved.contains(current.syntaxText) {
            value = try combined(.predicate(input, advance().text), input)
        } else {
            let right = try expression(13)
            newlines()
            var op = "="
            if match("or") {
                newlines()
                if match("less") {
                    op = "<="
                } else {
                    try expect("more")
                    op = ">="
                }
            }
            value = try combined(.binary(op, input, right), input, right)
        }
        if negated { value = try combined(.unary("!", value), value) }
        return value
    }
}
