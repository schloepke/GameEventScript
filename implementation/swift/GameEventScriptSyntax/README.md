<!-- Copyright 2026 Stephan Schlöpke -->
<!-- SPDX-License-Identifier: Apache-2.0 -->

# Swift Syntax module

> **Since: Unreleased**

`GameEventScriptSyntax` is the SwiftPM module with the GES lexer, parser and
single-source validator that Runtime and Compiler build on. For editors and
terminals it highlights GES and GESA from the lossless syntax tree, without
Foundation, AppKit, UIKit, Compiler or Runtime dependency, alike to the C#
package. The public repository-root package exports this product; this directory
is its standalone development package.

## Highlight an editor document

```swift
import GameEventScriptSyntax

let highlighter = try GesSyntaxHighlighter() // .ges by default
let result = highlighter.highlight(source)
if result.isComplete {
    for span in result.spans {
        // start/length are UTF-16 offsets; span.diagnostics lists the diagnostics overlapping it.
        applyStyle(span.kind, start: span.start, length: span.length, underlined: !span.diagnostics.isEmpty)
    }
}
```

Create the highlighter once and reuse it. An AppKit adapter can use temporary
attributes without changing text or undo history:

```swift
import AppKit
import GameEventScriptSyntax

func highlight(_ view: NSTextView, using highlighter: GesSyntaxHighlighter) {
    guard let layout = view.layoutManager else { return }
    let source = view.string
    layout.removeTemporaryAttribute(.foregroundColor,
        forCharacterRange: NSRange(location: 0, length: source.utf16.count))
    let result = highlighter.highlight(source)
    guard result.isComplete else { return }
    for span in result.spans {
        let color: NSColor
        switch span.kind {
        case .keyword: color = .systemPurple
        case .builtin, .type, .function: color = .systemTeal
        case .message, .constant: color = .systemOrange
        case .number, .tag: color = .systemBlue
        case .string: color = .systemRed
        case .comment: color = .secondaryLabelColor
        default: color = .textColor
        }
        layout.addTemporaryAttribute(.foregroundColor, value: color,
            forCharacterRange: NSRange(location: span.start, length: span.length))
    }
}
```

Highlight the whole document after each edit; input may be a fragment with
statements outside a handler, as console input is. `result.diagnostics` lists the
syntax errors, or else the violated single-source rules, with UTF-16 ranges. An
incomplete result, for an input above the length bound, has no spans: render it
plain.

## Terminal and GESA

```swift
let highlighter = try GesSyntaxHighlighter(language: .gesa)
let result = highlighter.highlight(dump)
let displayed = result.isComplete
    ? try GesAnsiRenderer.render(dump, spans: result.spans)
    : dump
```

The renderer accepts a custom category-to-SGR-color callback. Terminal detection,
`NO_COLOR` and enabling colors remain application decisions. GESA source blocks
and source-line annotations are highlighted as GES.

## Verification

```sh
swift test --package-path implementation/swift/GameEventScriptSyntax \
  --scratch-path artifacts/swift/syntax \
  --disable-build-manifest-caching --configuration release
```

C# and Swift execute the same [Markdown cases](../../../conformance/highlighting/SyntaxHighlighting.md).
See the [contract](../../../specs/SyntaxHighlighting.md) for categories,
diagnostics and limits. The new product is available from the next
release, or directly from a checkout containing it.
