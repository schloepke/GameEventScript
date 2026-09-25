// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

/// Optional pure string renderer. Terminal detection, NO_COLOR, output and diagnostics belong to the embedding.
public enum GameEventScriptAnsiRenderer {
    /// The CLI's default 16-color foreground palette; nil leaves text unchanged.
    public static func defaultColor(_ kind: GameEventScriptSyntaxKind) -> Int? {
        switch kind {
        case .comment: 90
        case .string: 32
        case .number: 34
        case .keyword: 35
        case .constant, .register: 33
        case .builtin, .message, .type, .tag, .function, .module, .label: 36
        default: nil
        }
    }

    /// Renders UTF-16 spans with the default palette or a custom category-to-ANSI-color mapping, preserving source characters verbatim.
    /// - Throws: `invalidSpans` for overlapping/out-of-bounds spans or split Unicode scalars;
    ///   `invalidAnsiColor` when a palette returns a color outside 30...37, 39, 90...97.
    public static func render(_ source: String, spans: [GameEventScriptHighlightSpan], color: (GameEventScriptSyntaxKind) -> Int? = defaultColor) throws -> String {
        let units = Array(source.utf16)
        var output = ""
        var position = 0
        var active: Int?

        func switchColor(_ next: Int?) {
            guard active != next else { return }
            if active != nil { output += "\u{1b}[0m" }
            if let next { output += "\u{1b}[\(next)m" }
            active = next
        }

        func splits(_ index: Int) -> Bool {
            index > 0 && index < units.count && (0xd800...0xdbff).contains(units[index - 1]) && (0xdc00...0xdfff).contains(units[index])
        }

        for span in spans {
            guard span.start >= position, span.length > 0, span.start <= units.count - span.length, !splits(span.start), !splits(span.start + span.length) else { throw GameEventScriptHighlightingError.invalidSpans }
            if span.start > position {
                switchColor(nil)
                output += String(decoding: units[position..<span.start], as: UTF16.self)
            }
            let next = color(span.kind)
            if let next, !(30...37).contains(next) && next != 39 && !(90...97).contains(next) { throw GameEventScriptHighlightingError.invalidAnsiColor }
            switchColor(next)
            output += String(decoding: units[span.start..<(span.start + span.length)], as: UTF16.self)
            position = span.start + span.length
        }
        switchColor(nil)
        return output + String(decoding: units[position...], as: UTF16.self)
    }
}
