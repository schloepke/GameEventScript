<!-- Copyright 2026 Stephan Schlöpke -->
<!-- SPDX-License-Identifier: Apache-2.0 -->

# Editor packages and syntax highlighting

Download highlighting for Game Event Script source (`.ges`) and assembler dumps
(`.gesa`), together with optional CLI commands, syntax completions and snippets.
Each ZIP includes installation notes and the Apache-2.0 license. Highlighting
works without the CLI; commands require `ges` on the editor process’s `PATH`.
No compiler or language server is bundled.

## Downloads

| Download | Grammar format | Use with |
| --- | --- | --- |
| [TextMate bundles — JSON](https://gameeventscript.org/downloads/GameEventScript-TextMate.zip) | `.tmLanguage.json` and `.tmPreferences.json` | Tooling that accepts JSON TextMate grammars |
| [Sublime Text and bat](https://gameeventscript.org/downloads/GameEventScript-Sublime-Text.zip) | `.sublime-syntax`, build system and completion plugin | Sublime Text; syntax files also work with `bat` |
| [TextMate Classic bundles — XML](https://gameeventscript.org/downloads/GameEventScript-TextMate-Classic.zip) | `.tmLanguage` and `.tmPreferences` plist | CodeRunner 4 and importers that require classic plist bundles |

The downloads are generated from the same repository revision as this website.
They follow development syntax and are not pinned to a published library version.
For an older version, use the bundles under `tools/editors` at the corresponding
Git tag.

## TextMate installation

1. For TextMate itself, choose the Classic XML download. Choose JSON only for
   importers that explicitly support JSON TextMate grammars. Extract the ZIP.
2. Import or copy `GameEventScript.tmbundle` and
   `GameEventScriptAssembler.tmbundle` into the editor's bundle location.
   Install both so assembler dumps can also highlight embedded source code.
3. Reload the editor's bundles or restart it, then open a `.ges` or `.gesa` file.

Use the editor's bundle/grammar import workflow; support for whole `.tmbundle`
directories and raw grammar files varies by editor. Choose one format rather
than installing both variants, which describe the same syntax and scopes.

## Sublime Text installation

> **Since: Unreleased**

Extract the download and copy its `GameEventScript` directory into the directory
opened by **Preferences → Browse Packages**. Keep that directory name: the
completion plugin loads its shared data from `Packages/GameEventScript`.
Restart Sublime Text or reload the package, then open a `.ges` or `.gesa` file.

## bat installation

Copy only `GameEventScript.sublime-syntax` and
`GameEventScriptAssembler.sublime-syntax` from the Sublime download into the
`syntaxes` subdirectory printed by `bat --config-dir`, then run:

```sh
bat cache --build
bat --list-languages
bat example.ges
bat example.gesa
```

Both grammars are required for GESA's embedded GES source. This only adds local
syntax assets; it does not replace your bat theme or install editor commands.
To update, replace those two files and rebuild the cache. To uninstall, remove
those two files and rebuild the cache again.

## CLI actions

> **Since: Unreleased**

Install the [GES CLI](https://gameeventscript.org/docs/tools/downloads/) and make
`ges` available on the **editor process's PATH**. Launching an editor from the
Dock need not inherit an interactive shell's PATH. Configure the editor's path
settings if necessary; shell aliases are not used. There is no `dotnet ges`
fallback and no automatic tool installation.

Save the current document before invoking an action. TextMate commands request
saving the active file. In Sublime, keep `save_all_on_build` enabled (the usual
build setting). These actions operate on a single file, with its containing
directory as the working directory:

| Action | Command | Result |
| --- | --- | --- |
| Check (default) | `ges check file.ges` | Validate without execution or a binary output |
| Compile | `ges compile file.ges` | Write the sibling `file.gesb` |
| Run | `ges run file.ges` | Execute the CLI `Main(args)` contract with an empty argument list |
| Dump | `ges dump file.gesb` | Display the sibling binary's assembler dump; compile first |

In Sublime choose **Tools → Build System → GameEventScript**. Build runs Check;
**Build With…** selects Compile, Run or Dump. In TextMate, use the bundle's
**GES: Check / Compile / Run / Dump** commands; Check also uses Command-B.
The GESA TextMate bundle provides Dump for the same-named sibling `.gesb`.

Sublime output captures source locations for next/previous build-result
navigation. TextMate uses clickable source links in an escaped HTML output
window. The TextMate adapters use Bash and Perl with core modules available on
macOS; no additional language package is required. Other TextMate grammar
importers may support only highlighting, not these commands or completion hooks.

Run is a batch command: delayed messages can keep it running. Use the editor's
cancellation controls to stop it. Interactive REPL use belongs in a terminal.
Project-wide compilation, custom arguments and scenarios are not configured by
these single-file actions.

## Syntax completions and snippets

> **Since: Unreleased**

Sublime offers completion through its popup; TextMate uses its normal completion
command (Escape by default). Suggestions are deliberately syntax-only:

- After `as` or `is`: built-in type names, with `:` supplied when needed.
- After `[:`: collection selectors such as `fold`, `reduce` and `count`.
- After a type colon: built-in type names.
- Otherwise: language keywords, outside comment and string scopes.

The rules inspect the current line before the caret. They do not infer receiver
types, visible variables, user-defined functions or native extensions. Suggestions
are editing aids, not a statement that every offered type/selector is valid for
the surrounding expression; `ges check` performs validation.

Both editors provide the same Tab-triggered snippets: `ges-on`, `ges-iflet`,
`ges-fold`, `ges-reduce` and `ges-after`. Tab moves between placeholders.

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

Sublime grammars, editor commands and snippets are generated by
`scripts/sync-editor-bundles.py` (`--check` checks freshness). TextMate JSON is the
syntax source; `tools/editors/support/completions.json` owns the syntax completion
catalog and snippet templates. Adapter sources are also in `support`.
Do not edit generated copies in the bundles. Run
`python3 scripts/test-editor-bundles.py` for generator and adapter checks; when
`bat` or `batcat` is installed it additionally loads both grammars into an isolated
cache and checks highlighted GES/GESA output. No user editor configuration is
modified by these tests.
