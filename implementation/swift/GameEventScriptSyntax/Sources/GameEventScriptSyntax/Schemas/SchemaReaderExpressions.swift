// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

// The expressions of computed fields: a subset of GES expressions with their precedence and spellings, so that the same text
// means the same in a script record. A line break may follow an operator, a keyword, a comma or an opening parenthesis or
// bracket, where the expression cannot end.
extension GesSchemaSourceReader {
    private static let maximumExpressionDepth = 32

    /// A guarded choice `value when condition, …, otherwise fallback`, or a single expression.
    mutating func readExpression() throws -> GesSchemaExpressionSyntax {
        expressionNesting += 1
        if expressionNesting > Self.maximumExpressionDepth { throw failure("The expression is nested too deeply.") }
        let offset = position
        let first = try readDefaultLevel()
        skipSpaces()
        if !atWord("when") {
            expressionNesting -= 1
            return first
        }
        var parts = [first]
        while true {
            try expectWord("when")
            skipTrivia()
            parts.append(try readDefaultLevel())
            let end = position
            skipSpaces()
            let comma = consume(44)
            skipTrivia()
            if atWord("otherwise") {
                _ = word()
                skipTrivia()
                parts.append(try readDefaultLevel())
                break
            }
            if !comma {
                position = end
                throw failure("Expected 'otherwise'.")
            }
            parts.append(try readDefaultLevel())
            skipSpaces()
            if !atWord("when") { throw failure("Expected 'when'.") }
        }
        expressionNesting -= 1
        return try node(.choice, offset, parts)
    }

    private mutating func readDefaultLevel() throws -> GesSchemaExpressionSyntax {
        var left = try readBinary(0)
        while true {
            skipSpaces()
            if !atWord("default") { return left }
            _ = word()
            skipTrivia()
            left = try node(.default, left.offset, [left, try readBinary(0)])
        }
    }

    /// The binary levels from `or` down to multiplication; each level reads the next higher one.
    private mutating func readBinary(_ level: Int) throws -> GesSchemaExpressionSyntax {
        if level == 6 { return try readUnary() }
        var left = try readBinary(level + 1)
        while true {
            skipSpaces()
            if level == 3 && atWord("has") {
                _ = word()
                skipSpaces()
                try expectWord("value")
                left = try node(.hasValue, left.offset, [left])
                continue
            }
            guard let found = binaryOperator(level) else { return left }
            skipTrivia()
            left = try checked(GesSchemaExpressionSyntax(.binary, offset: left.offset, binary: found, children: [left, try readBinary(level + 1)]))
        }
    }

    /// Consumes the operator of a level: 0 or, 1 and, 2 equality, 3 relation, 4 addition, 5 multiplication.
    private mutating func binaryOperator(_ level: Int) -> GesSchemaBinaryOperator? {
        switch level {
        case 0 where atWord("or"):
            _ = word()
            return .or
        case 1 where atWord("and"):
            _ = word()
            return .and
        case 2 where at("<>"):
            position += 2
            return .notEqual
        case 2 where at("=") && !at("=>"):
            position += 1
            return .equal
        case 3 where at("<=") || at(">="):
            let lessOrEqual = bytes[position] == 60
            position += 2
            return lessOrEqual ? .lessOrEqual : .greaterOrEqual
        case 3 where (at("<") && !at("<>")) || at(">"):
            let less = bytes[position] == 60
            position += 1
            return less ? .less : .greater
        case 4 where at("+") || (at("-") && !at("->")):
            let add = bytes[position] == 43
            position += 1
            return add ? .add : .subtract
        case 5 where at("*") || (at("/") && !atComment):
            let multiply = bytes[position] == 42
            position += 1
            return multiply ? .multiply : .divide
        default:
            return nil
        }
    }

    private mutating func readUnary() throws -> GesSchemaExpressionSyntax {
        skipSpaces()
        let offset = position
        if consume(45) {
            skipSpaces()
            return try node(.negate, offset, [try readUnary()])
        }
        for (written, kind) in [("not", GesSchemaExpressionKind.not), ("abs", .abs)] where atWord(written) {
            _ = word()
            skipSpaces()
            return try node(kind, offset, [try readUnary()])
        }
        return try readPostfix()
    }

    /// Member access `record.field` and the list aggregates `list[:sum]`, `list[:sum item => value]` and `list[:count]`.
    private mutating func readPostfix() throws -> GesSchemaExpressionSyntax {
        var value = try readPrimary()
        while true {
            if at("."), position + 1 < bytes.count, GesNames.lower(bytes[position + 1]) {
                position += 1
                let offset = position
                let member = try name(GesNames.plain, "field name")
                value = try checked(GesSchemaExpressionSyntax(.member, offset: offset, name: member, children: [value]))
                continue
            }
            if !at("[") { return value }
            let start = position
            position += 1
            skipTrivia()
            try expect(58)
            let selector = word()
            skipTrivia()
            if selector == "count" {
                try expect(93)
                value = try node(.count, start, [value])
                continue
            }
            if selector != "sum" { throw Failure(offset: start, message: "A computed expression takes the list selectors :sum and :count.") }
            if consume(93) {
                value = try node(.sum, start, [value])
                continue
            }
            let variable = try name(GesNames.plain, "variable name")
            skipSpaces()
            if !at("=>") { throw failure("Expected '=>'.") }
            position += 2
            skipTrivia()
            let projection = try readExpression()
            skipTrivia()
            try expect(93)
            value = try checked(GesSchemaExpressionSyntax(.sum, offset: start, name: variable, children: [value, projection]))
        }
    }

    private mutating func readPrimary() throws -> GesSchemaExpressionSyntax {
        skipSpaces()
        let offset = position
        if position >= bytes.count { throw failure("Expected an expression.") }
        if consume(40) {
            skipTrivia()
            let inner = try readExpression()
            skipTrivia()
            try expect(41)
            return inner
        }
        if consume(36) { return GesSchemaExpressionSyntax(.constant, offset: offset, name: try name(GesNames.plain, "constant name")) }
        for written in ["true", "false"] where atWord(written) {
            _ = word()
            return GesSchemaExpressionSyntax(.boolean, offset: offset, boolean: written == "true")
        }
        if atWord("min") || atWord("max") {
            let kind: GesSchemaExpressionKind = word() == "min" ? .min : .max
            skipSpaces()
            try expectWord("of")
            skipTrivia()
            var arguments = [try readBinary(4)]
            while true {
                skipSpaces()
                if !atWord("and") { break }
                _ = word()
                skipTrivia()
                arguments.append(try readBinary(4))
            }
            if arguments.count < 2 { throw Failure(offset: offset, message: "min of and max of take at least two values joined by and.") }
            return try node(kind, offset, arguments)
        }
        if atWord("clamp") {
            _ = word()
            skipSpaces()
            let value = try readBinary(4)
            skipSpaces()
            try expectWord("between")
            skipTrivia()
            let lower = try readBinary(4)
            skipSpaces()
            try expectWord("and")
            skipTrivia()
            return try node(.clamp, offset, [value, lower, try readBinary(4)])
        }
        if GesNames.lower(bytes[position]) {
            let written = try name(GesNames.plain, "name")
            let tokens = GesLexer.tokenize(written)
            if tokens.isEmpty || tokens[0].kind != .identifier {
                throw Failure(offset: offset, message: "'\(written)' is a GES keyword or constant, which a computed expression cannot name.", code: GesSchemaDiagnostic.invalidExpression)
            }
            return GesSchemaExpressionSyntax(.name, offset: offset, name: written)
        }
        if bytes[position] >= 48 && bytes[position] <= 57 { return GesSchemaExpressionSyntax(.literal, offset: offset, literal: try readExpressionLiteral()) }
        throw failure("Expected an expression.")
    }

    /// A number with its suffix. Besides the ends of other literals it stops at an operator; a minus continues only an exponent.
    private mutating func readExpressionLiteral() throws -> GesSchemaValueSyntax {
        let offset = position
        var end = position
        while end < bytes.count && !isExpressionLiteralEnd(end) { end += 1 }
        guard let read = GesSchemaLiterals.read(Array(bytes[offset..<end])) else {
            throw Failure(offset: offset, message: "Expected an exact finite number with a unit known to literals.")
        }
        position = end
        var result = GesSchemaValueSyntax()
        result.value = read.value
        result.unit = read.unit
        result.isPercent = read.isPercent
        result.text = String(decoding: bytes[offset..<end], as: UTF8.self)
        result.offset = offset
        return result
    }

    private func isExpressionLiteralEnd(_ index: Int) -> Bool {
        let byte = bytes[index]
        if byte == 45 { return !(index > 1 && (bytes[index - 1] == 101 || bytes[index - 1] == 69) && bytes[index - 2] >= 48 && bytes[index - 2] <= 57) }
        return isLiteralEnd(index) || byte == 91 || byte == 93 || byte == 43 || byte == 42 || byte == 60 || byte == 62 || byte == 61
    }

    private func at(_ text: String) -> Bool {
        let written = Array(text.utf8)
        return position + written.count <= bytes.count && bytes[position..<(position + written.count)].elementsEqual(written)
    }

    private func node(_ kind: GesSchemaExpressionKind, _ offset: Int, _ children: [GesSchemaExpressionSyntax]) throws -> GesSchemaExpressionSyntax {
        try checked(GesSchemaExpressionSyntax(kind, offset: offset, children: children))
    }

    private func checked(_ node: GesSchemaExpressionSyntax) throws -> GesSchemaExpressionSyntax {
        if node.depth > Self.maximumExpressionDepth { throw Failure(offset: node.offset, message: "The expression is nested too deeply.") }
        return node
    }
}
