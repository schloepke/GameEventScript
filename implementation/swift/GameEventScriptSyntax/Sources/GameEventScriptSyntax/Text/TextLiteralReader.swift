// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

/// Decodes a source Text literal: single or double quotes, where only a doubled delimiter escapes the quote.
@_spi(Syntax) public enum GesTextLiteralReader {
    /// Reads the literal opening at `position` and moves `position` behind its closing quote; nil when it is unterminated.
    @_spi(Syntax) public static func read(_ scalars: [Unicode.Scalar], _ position: inout Int) -> String? {
        guard position < scalars.count, scalars[position] == "'" || scalars[position] == "\"" else { return nil }
        let quote = scalars[position]
        position += 1
        var text = String.UnicodeScalarView()
        while position < scalars.count {
            let scalar = scalars[position]
            position += 1
            if scalar != quote {
                text.append(scalar)
                continue
            }
            if position < scalars.count && scalars[position] == quote {
                text.append(quote)
                position += 1
                continue
            }
            return String(text)
        }
        return nil
    }
}
