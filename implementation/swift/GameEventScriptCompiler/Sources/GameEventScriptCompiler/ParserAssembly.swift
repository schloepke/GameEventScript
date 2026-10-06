// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

@_spi(Compiler) import GameEventScriptRuntime

extension GesParser {
    func assemblyOrExpression(_ output: String, predicate: Bool = false, allowSend: Bool = false) throws -> GesExpression {
        if !match("asm") { return try expression() }
        let start = previous
        return node(.assembly(try assemblyBlock(output, predicate: predicate, allowSend: allowSend)), start)
    }

    func assemblyBlock(_ output: String?, predicate: Bool = false, allowSend: Bool = false) throws -> GesAssembly {
        let start = previous
        newlines()
        try expect("{")
        separators()
        var declarations: [(name: String, export: Bool)] = []
        var lines: [GesAssemblyLine] = []
        while current.syntaxText != "}" {
            let token = current
            let exported = match("let")
            if exported || match(".") {
                if !exported { try expect("register") }
                if !lines.isEmpty || exported && output != nil { throw failure("Assembly declarations must precede instructions and only standalone blocks export bindings.", token) }
                repeat { declarations.append((try identifier(), exported)) } while match(",")
            } else if current.syntaxText == "emit" || current.syntaxText == "publish" {
                let statement = try statement()
                guard case .publish(let publish, let message, let tags) = statement.kind else { throw failure("Expected a statement send.", token) }
                lines.append(.init(name: publish ? "publish" : "emit", label: false, operands: [message] + tags, location: location(token, previous)))
            } else if current.kind == "word" {
                let name = try identifier()
                try expect(":")
                lines.append(.init(name: name, label: true, operands: [], location: location(token, previous)))
            } else {
                guard current.kind == "message" else { throw failure("Expected an assembly instruction.", token) }
                let name = advance().text
                var operands: [GesExpression] = []
                if current.kind != "newline" && current.syntaxText != ";" && current.syntaxText != "}" {
                    repeat {
                        if current.kind == "type" && tokens[min(index + 1, tokens.count - 1)].syntaxText != "(" {
                            let type = advance()
                            operands.append(node(.literal(.text(String(type.text.dropFirst()))), type))
                        } else {
                            let value = try expression()
                            if case .unary("-", let number) = value.kind, case .literal(let literal) = number.kind, literal.isNumeric {
                                if let integer = literal.integerValue, integer != Int64.min { operands.append(node(.literal(.integer(-integer)), token)) } else { operands.append(node(.literal(.float(-literal.asNumber)), token)) }
                            } else {
                                operands.append(value)
                            }
                        }
                    } while match(",")
                }
                lines.append(.init(name: name, label: false, operands: operands, location: location(token, previous)))
            }
            if current.kind != "newline" && current.syntaxText != ";" && current.syntaxText != "}" { throw failure("Expected assembly line separator.") }
            separators()
        }
        try expect("}")
        return .init(output: output, declarations: declarations, lines: lines, predicate: predicate, allowSend: allowSend, location: location(start, previous))
    }
}
