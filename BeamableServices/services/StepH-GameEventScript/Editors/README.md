# GameEventScript Editor Support

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
