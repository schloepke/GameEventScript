<!-- Copyright 2026 Stephan Schlöpke -->
<!-- SPDX-License-Identifier: Apache-2.0 -->

# GameEventScript for Sublime Text and bat

Copy this `GameEventScript` folder into Sublime's **Preferences → Browse Packages**
directory. Restart the editor. `.ges` and `.gesa` are detected automatically.
For bat, copy only the two `.sublime-syntax` files to the `syntaxes` subdirectory
of `bat --config-dir`, then run `bat cache --build`.

CLI actions require `ges` on the editor's PATH. **Build** checks the current saved
source file; **Build With…** offers Compile, Run and Dump. Dump reads the sibling
`.gesb` from a previous compile. Compiler locations support build-result navigation.
Run uses `Main(args)` with no arguments; it is not an interactive terminal.

Completions suggest built-in types after `as`/`is` or a type colon, selectors after
`[:`, and keywords otherwise. Strings and comments are excluded. No semantic
inference or host API discovery is performed. Snippet Tab triggers are `ges-on`,
`ges-iflet`, `ges-fold`, `ges-reduce`, and `ges-after`.

See the [editor guide](https://gameeventscript.org/docs/tools/editor-bundles/)
for installation, updates, removal and command behavior. Generated files follow
the current development grammar, not a fixed released language version.
