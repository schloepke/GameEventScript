// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

@_spi(Syntax) import GameEventScriptCompiler
@_spi(Syntax) import GameEventScriptSyntax

// Lexes every source of a request losslessly. Offsets are Unicode scalar offsets, so both ports report the same values.
enum SyntaxTokens {
    static func run(_ input: Input) throws -> [String: Any] {
        var sources: [String: Any] = [:]
        for test in input.values("cases") {
            let id = try test.text("id")
            for (index, source) in test.values("sources").enumerated() {
                let text = try source.text("text")
                let offsets = scalarOffsets(text)
                sources[id + "#" + String(index)] = GesLexer.tokenize(text).map { token -> [Any] in
                    [kindName(token.kind), offsets[token.fullStart]!, offsets[token.start]!, offsets[token.end]!, token.text]
                }
            }
        }
        return sources
    }

    // Parses every source; the lossless syntax tree also exists for sources with errors.
    static func runTrees(_ input: Input) throws -> [String: Any] {
        var sources: [String: Any] = [:]
        for test in input.values("cases") {
            let id = try test.text("id")
            for (index, source) in test.values("sources").enumerated() {
                let text = try source.text("text")
                let tree = GesProgramBuilder.syntaxTree(text)
                let offsets = scalarOffsets(text)
                sources[id + "#" + String(index)] = [
                    "tokens": tree.tokens.map { token -> [Any] in [kindName(token.kind), offsets[token.fullStart]!, offsets[token.start]!, offsets[token.end]!, token.text] },
                    "roles": tree.roles.map { name(String(describing: $0)) },
                    "tree": nodes(tree.root),
                ]
            }
        }
        return sources
    }

    // Nodes in preorder as [kind, first token, end token, parent index, range start, range end]; flat, so deep trees stay
    // within JSON depth limits.
    private static func nodes(_ root: GesSyntaxNode) -> [[Any]] {
        var result: [[Any]] = []
        var pending: [(GesSyntaxNode, Int)] = [(root, -1)]
        while let (node, parent) = pending.popLast() {
            let index = result.count
            result.append([name(node.kind), node.firstToken, node.endToken, parent, node.rangeStart, node.rangeEnd])
            for child in node.children.reversed() { pending.append((child, index)) }
        }
        return result
    }

    private static func name(_ kind: GesSyntaxNodeKind) -> String { name(String(describing: kind)) }

    // The C# spelling of a case name, such as `LetBinding` for `letBinding`.
    private static func name(_ text: String) -> String { text.prefix(1).uppercased() + text.dropFirst() }

    // The C# GesTokenKind spelling of a kind, such as `OperatorPlus` for `.operatorPlus`.
    private static func kindName(_ kind: GesTokenKind) -> String {
        let name = String(describing: kind)
        return name.prefix(1).uppercased() + name.dropFirst()
    }

    // The scalar offset of every UTF-8 offset at a scalar boundary, including the end of the text.
    private static func scalarOffsets(_ text: String) -> [Int: Int] {
        var offsets: [Int: Int] = [:]
        var byte = 0
        for (scalar, value) in text.unicodeScalars.enumerated() {
            offsets[byte] = scalar
            byte += String(value).utf8.count
        }
        offsets[byte] = text.unicodeScalars.count
        return offsets
    }
}
