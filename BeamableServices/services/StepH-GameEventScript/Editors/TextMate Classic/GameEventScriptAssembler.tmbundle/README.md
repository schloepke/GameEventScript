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
comments, and strings. Version directives such as `.program-version 42` are
recognized explicitly. If embedded debug symbols are present in a dump,
`r20(total)` highlights `r20` as the physical register and `total` as its source
variable annotation; execution flags such as `NormalizeResultAsPredicate` retain
their separate constant scope. Source archives in
`.segment source "name.ges"` blocks and the source portion of
`.source-line "name.ges" 4 | source` use the normal GameEventScript source
grammar. A source segment ends at `.region-end "Name"` or the next `.segment`
directive. Free `.region "Name"` / `.region-end "Name"` blocks wrap dump
segments, use a comment scope, and are exposed through the standard TextMate
folding markers.
