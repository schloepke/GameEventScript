<!-- Copyright 2026 Stephan Schlöpke -->
<!-- SPDX-License-Identifier: Apache-2.0 -->

# TextMate editor bundles

Download syntax highlighting for Game Event Script source (`.ges`) and assembler
dumps (`.gesa`). Each ZIP includes both bundles, their installation notes and the
Apache-2.0 license. These are syntax grammars, not a compiler or language server.

## Downloads

| Download | Grammar format | Use with |
| --- | --- | --- |
| [TextMate bundles — JSON](https://gameeventscript.org/downloads/GameEventScript-TextMate.zip) | `.tmLanguage.json` and `.tmPreferences.json` | Tooling that accepts JSON TextMate grammars |
| [TextMate Classic bundles — XML](https://gameeventscript.org/downloads/GameEventScript-TextMate-Classic.zip) | `.tmLanguage` and `.tmPreferences` plist | CodeRunner 4 and importers that require classic plist bundles |

The downloads are generated from the same repository revision as this website.
They follow development syntax and are not pinned to a published library version.
For an older version, use the bundles under `tools/editors` at the corresponding
Git tag.

## Installation

1. Choose the format your editor supports and extract the ZIP.
2. Import or copy `GameEventScript.tmbundle` and
   `GameEventScriptAssembler.tmbundle` into the editor's bundle location.
   Install both so assembler dumps can also highlight embedded source code.
3. Reload the editor's bundles or restart it, then open a `.ges` or `.gesa` file.

Use the editor's bundle/grammar import workflow; support for whole `.tmbundle`
directories and raw grammar files varies by editor. Choose one format rather
than installing both variants, which describe the same syntax and scopes.

## Repository sources

- `TextMate Classic/`: XML plist bundle for CodeRunner 4 and older
  TextMate-compatible importers.
- `TextMate/`: JSON TextMate bundle for modern TextMate-compatible tooling.

Both editor folders contain bundles for GameEventScript source (`.ges`) and
GameEventScript assembler (`.gesa`). The modern and classic bundles describe the
same grammars; only the file format is different.

The JSON grammars under `TextMate/` are the maintained source. The XML plist
grammars under `TextMate Classic/` must be regenerated from them whenever syntax
support changes so both editor generations remain semantically identical.

`.ges` is the only canonical GameEventScript source extension. The assembler
grammar follows the current dumper output, including `.program-version`, current
opcodes and bytecode types, and debug-symbol register annotations such as
`r20(total)`. Embedded source archives use `.segment source "name.ges"`; its
content continues until the next `.segment` directive. Mapped source lines use
`.source-line "name.ges" 4 | source`. Both forms embed the normal
`source.gameeventscript` grammar in assembler dumps.

The dumper wraps source, text, list, binding, and code segments in free
`.region "Name"` / `.region-end "Name"` presentation blocks with visible
comment separators and surrounding blank lines. The assembler grammars render
the region directives with a comment scope and publish them as TextMate folding
markers for editors that support grammar-defined folding. Regions do not change
`.gesb` or runtime semantics.
