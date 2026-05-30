# GameEventScript Assembler TextMate Classic Bundle

This bundle provides classic XML plist TextMate-compatible syntax highlighting
for GameEventScript assembler files (`.gesa`).

## Install

Copy or symlink `GameEventScriptAssembler.tmbundle` into a TextMate-compatible
bundle location, then reload bundles in the editor.

Common examples:

- TextMate: `~/Library/Application Support/TextMate/Bundles/`
- Sublime Text: `~/Library/Application Support/Sublime Text/Packages/`

Use this variant for CodeRunner 4 and older TextMate-compatible importers that
expect `.tmLanguage` and `.tmPreferences` plist files.

The grammar is intentionally lightweight. It highlights `.gesa` directives,
segments, labels, opcodes, registers, immediates, bind kinds, type names,
comments, and strings.
