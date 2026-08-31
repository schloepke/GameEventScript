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
`r20(total)`.
