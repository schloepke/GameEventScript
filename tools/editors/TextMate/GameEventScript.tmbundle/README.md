<!-- Copyright 2026 Stephan Schlöpke -->
<!-- SPDX-License-Identifier: Apache-2.0 -->

# GameEventScript TextMate Bundle

This bundle provides modern JSON TextMate-compatible syntax highlighting for
GameEventScript source files. `.ges` is the canonical and only registered source
extension.

## Install

Copy or symlink `GameEventScript.tmbundle` into a TextMate-compatible bundle
location, then reload bundles in the editor.

Common examples:

- TextMate: `~/Library/Application Support/TextMate/Bundles/`
- Sublime Text: `~/Library/Application Support/Sublime Text/Packages/`

Use this variant for tooling that accepts `.tmLanguage.json` and
`.tmPreferences.json`. For CodeRunner 4, use the sibling `TextMate Classic`
bundle instead.

The grammar is intentionally lightweight. It highlights declarations, handlers,
messages, keywords, tags/types, pipeline selectors, literals, comments, and
ASCII/Unicode operators.
