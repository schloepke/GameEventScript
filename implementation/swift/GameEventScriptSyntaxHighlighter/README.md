<!-- Copyright 2026 Stephan Schlöpke -->
<!-- SPDX-License-Identifier: Apache-2.0 -->

# Swift syntax highlighting

`GameEventScriptSyntaxHighlighter` is an optional SwiftPM library for GES and GESA
editors and terminals. It uses Foundation, with no AppKit, UIKit, Compiler or
Runtime dependency. Both CLI highlighting and this library use the canonical
TextMate grammars. The public repository-root package exports this product;
this directory is its standalone development package.

## Highlight an editor document

```swift
import GameEventScriptSyntaxHighlighter

let highlighter = try GameEventScriptSyntaxHighlighter() // .ges by default
let result = highlighter.highlight(source)
if result.isComplete {
    for span in result.spans {
        // start/length are UTF-16 offsets; scopes are TextMate scope names.
        applyStyle(span.kind, start: span.start, length: span.length)
    }
}
```

Create the highlighter once and reuse it. An AppKit adapter can use temporary
attributes without changing text or undo history:

```swift
import AppKit
import GameEventScriptSyntaxHighlighter

func highlight(_ view: NSTextView, using highlighter: GameEventScriptSyntaxHighlighter) {
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

For large documents, cache per-line results from
`try highlighter.highlightLine(line, state: previousState)`. Keep line terminators
and add each line's document offset to its spans. After edits, continue until
unchanged text and `nextState` match the cache. An incomplete result has no state;
render it plain and do not cache it as a successful result.

## Terminal and GESA

```swift
let highlighter = try GameEventScriptSyntaxHighlighter(language: .gesa)
let result = highlighter.highlight(dump)
let displayed = result.isComplete
    ? try GameEventScriptAnsiRenderer.render(dump, spans: result.spans)
    : dump
```

The renderer accepts a custom category-to-SGR-color callback. Terminal detection,
`NO_COLOR` and enabling colors remain application decisions. GESA source blocks
retain their embedded GES scopes.

## Verification

```sh
swift test --package-path implementation/swift/GameEventScriptSyntaxHighlighter \
  --scratch-path artifacts/swift/syntaxhighlighter \
  --disable-build-manifest-caching --configuration release
python3 scripts/sync-highlighter-grammars.py --check
```

C# and Swift execute the same [Markdown cases](../../../conformance/highlighting/SyntaxHighlighting.md).
See the [contract](../../../specs/SyntaxHighlighting.md) for limits, state semantics
and supported TextMate features. The new product is available from the next
release, or directly from a checkout containing it.
