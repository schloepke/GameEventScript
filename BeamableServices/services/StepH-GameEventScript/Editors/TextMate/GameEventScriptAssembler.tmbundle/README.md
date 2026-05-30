# GameEventScript Assembler TextMate Bundle

This bundle provides modern JSON TextMate-compatible syntax highlighting for
GameEventScript assembler files (`.gesa`).

## Install

Copy or symlink `GameEventScriptAssembler.tmbundle` into a TextMate-compatible
bundle location, then reload bundles in the editor.

Common examples:

- TextMate: `~/Library/Application Support/TextMate/Bundles/`
- Sublime Text: `~/Library/Application Support/Sublime Text/Packages/`

Use this variant for tooling that accepts `.tmLanguage.json` and
`.tmPreferences.json`. For CodeRunner 4, use the sibling `TextMate Classic`
bundle instead.

The grammar is intentionally lightweight. It highlights `.gesa` directives,
segments, labels, opcodes, registers, immediates, bind kinds, type names,
comments, and strings.
