// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

@_spi(Compiler) import GameEventScriptRuntime
@_spi(Syntax) import GameEventScriptSyntax

extension GesParser {
    func assemblyOrExpression(_ output: String, predicate: Bool = false, allowSend: Bool = false) throws -> GesExpression {
        if !current.isIdentifier("asm") { return try expression() }
        startNode(.assemblyBlock)
        defer { finishNode() }
        let start = advance()
        return node(.assembly(try assemblyBlock(output, predicate: predicate, allowSend: allowSend)), start)
    }

    func assemblyBlock(_ output: String?, predicate: Bool = false, allowSend: Bool = false) throws -> GesAssembly {
        let start = previous
        newlines()
        try expect(.leftBrace)
        separators()
        var declarations: [(name: String, export: Bool)] = []
        var lines: [GesAssemblyLine] = []
        while currentKind != .rightBrace {
            let token = current
            let exported = match(.`let`)
            if exported || match(.dot) {
                if !exported { try expectWord("register") }
                if !lines.isEmpty || exported && output != nil { throw failure("Assembly declarations must precede instructions and only standalone blocks export bindings.", token) }
                repeat { declarations.append((try identifier(), exported)) } while match(.comma)
            } else if currentKind == .emit || currentKind == .publish {
                // An assembly send has the form of a statement send, but no statement node, as in C#.
                let statement = try statementCore()
                guard case .publish(let publish, let message, let tags) = statement.kind else { throw failure("Expected a statement send.", token) }
                lines.append(.init(name: publish ? "publish" : "emit", label: false, operands: [message] + tags, location: location(token, previous)))
            } else if current.isWord {
                let name = try identifier()
                try expect(.colon)
                lines.append(.init(name: name, label: true, operands: [], location: location(token, previous)))
            } else {
                guard currentKind == .message else { throw failure("Expected an assembly instruction.", token) }
                let name = advance().text
                var operands: [GesExpression] = []
                if currentKind != .newLine && currentKind != .semicolon && currentKind != .rightBrace {
                    repeat {
                        if assemblyMessageOperand(name, operands.count), currentKind == .message {
                            // The message operand spans from its name, as the C# message literal does.
                            startNode(.messageExpression)
                            let messageToken = current
                            let message = advance().text
                            let arguments = currentKind == .leftParen ? try arguments() : []
                            operands.append(finishNode(node(.message(message, arguments), messageToken)))
                        } else if currentKind == .operatorMinus, peek().text == "9223372036854775808" {
                            advance()
                            advance()
                            operands.append(node(.literal(.integer(Int64.min)), token))
                        } else if currentKind == .typeName && tokens[min(index + 1, tokens.count - 1)].kind != .leftParen {
                            let type = advance()
                            operands.append(node(.literal(.text(String(type.text.dropFirst()))), type))
                        } else {
                            operands.append(try expression())
                        }
                    } while match(.comma)
                }
                lines.append(.init(name: name, label: false, operands: operands, location: location(token, previous)))
            }
            if currentKind != .newLine && currentKind != .semicolon && currentKind != .rightBrace { throw failure("Expected assembly line separator.") }
            separators()
        }
        try expect(.rightBrace)
        let normalizedLines = lines.map { line in
            GesAssemblyLine(name: line.name, label: line.label, operands: line.operands.map(Self.normalizeAssemblyOperand), location: line.location)
        }
        return .init(output: output, declarations: declarations, lines: normalizedLines, predicate: predicate, allowSend: allowSend, location: location(start, previous))
    }

    static func normalizeAssemblyOperand(_ expression: GesExpression) -> GesExpression {
        func arguments(_ args: [GesArgument]) -> [GesArgument] {
            args.map { .init(label: $0.label, value: normalizeAssemblyOperand($0.value)) }
        }

        let kind: GesExpression.Kind
        switch expression.kind {
        case .unary(.negate, let operand):
            guard case .literal(let value) = operand.kind, value.isNumeric else { return expression }
            if value.valueKind == .percentage {
                kind = .literal(.percentage(-value.asNumber))
            } else if let integer = value.integerValue, integer != Int64.min {
                kind = .literal(.integer(-integer, unit: value.unit))
            } else {
                kind = .literal(.float(-value.asNumber, unit: value.unit))
            }
        case .call(let name, let args): kind = .call(name, arguments(args))
        case .extensionCall(let namespace, let name, let args): kind = .extensionCall(namespace, name, arguments(args))
        case .constructor(let type, let args): kind = .constructor(type, arguments(args))
        case .message(let name, let args): kind = .message(name, arguments(args))
        case .list(let items): kind = .list(items.map(normalizeAssemblyOperand))
        case .map(let entries): kind = .map(entries.map { ($0.0, normalizeAssemblyOperand($0.1)) })
        default: return expression
        }
        return .init(kind, expression.location, depth: expression.depth)
    }

    func assemblyMessageOperand(_ opcode: String, _ index: Int) -> Bool {
        switch opcode {
        case "LoadMessage", "EmitInstant", "PublishInstant": return index == 1
        case "EmitAfter", "PublishAfter": return index == 2
        case "EmitMessage", "EmitMessageWithTags", "PublishMessage", "PublishMessageWithTags": return index == 0
        default: return false
        }
    }

}
