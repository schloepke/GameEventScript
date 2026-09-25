// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import GameEventScriptSyntaxHighlighter

// CLI presentation policy; all syntax recognition lives in the standalone package.
final class Highlighting {
    private let source = try! GameEventScriptSyntaxHighlighter()
    private let assembly = try! GameEventScriptSyntaxHighlighter(language: .gesa)

    static func paint(_ text: String, _ color: Int) -> String { "\u{1b}[\(color)m" + text + "\u{1b}[0m" }

    func render(_ text: String) -> String { (try? GameEventScriptAnsiRenderer.render(text, spans: source.highlight(text).spans)) ?? text }

    func renderAssembly(_ text: String) -> String { (try? GameEventScriptAnsiRenderer.render(text, spans: assembly.highlight(text).spans)) ?? text }
}
