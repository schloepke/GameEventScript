// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import GameEventScriptSyntaxHighlighter

// CLI presentation policy; all syntax recognition lives in the standalone package.
final class RunHighlighting {
    private let source = try! GesSyntaxHighlighter()
    private let assembly = try! GesSyntaxHighlighter(language: .gesa)

    static func paint(_ text: String, _ color: Int) -> String { "\u{1b}[\(color)m" + text + "\u{1b}[0m" }

    func render(_ text: String) -> String { (try? GesAnsiRenderer.render(text, spans: source.highlight(text).spans)) ?? text }

    func renderAssembly(_ text: String) -> String { (try? GesAnsiRenderer.render(text, spans: assembly.highlight(text).spans)) ?? text }
}
