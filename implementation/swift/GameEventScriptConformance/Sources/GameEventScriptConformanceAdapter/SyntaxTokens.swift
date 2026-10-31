// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

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
