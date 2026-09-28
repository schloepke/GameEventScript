<!-- Copyright 2026 Stephan Schlöpke -->
<!-- SPDX-License-Identifier: Apache-2.0 -->

# GameEventScript TextMate Classic Bundle

This bundle provides classic XML plist TextMate-compatible syntax highlighting
for GameEventScript source files. `.ges` is the canonical and only registered
source extension.

## Install

Copy or symlink `GameEventScript.tmbundle` into a TextMate-compatible bundle
location, then reload bundles in the editor.

Common examples:

- TextMate: `~/Library/Application Support/TextMate/Bundles/`

Use this variant for CodeRunner 4 and older TextMate-compatible importers that
expect `.tmLanguage` and `.tmPreferences` plist files.

The grammar is intentionally lightweight. It highlights declarations, handlers,
messages, keywords, tags/types, pipeline selectors, literals, comments, and
ASCII/Unicode operators.

## Commands and completion

The bundle also includes optional TextMate CLI commands. Install `ges` on the
editor's PATH. The source bundle offers Check (Command-B), Compile, Run and Dump;
the assembler bundle offers Dump of its sibling `.gesb`. Commands save the active
file and show output with clickable diagnostic locations. Grammar-only importers
may not support these commands.

The source bundle additionally offers syntax-only completion and the Tab snippets
`ges-on`, `ges-iflet`, `ges-fold`, `ges-reduce`, and `ges-after`. There is no
semantic analysis. See the [editor guide](https://gameeventscript.org/docs/tools/editor-bundles/)
for scope, installation and CLI requirements. Use the separate Sublime package
for Sublime Text and bat.
