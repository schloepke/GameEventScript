<!-- Copyright 2026 Stephan Schlöpke -->
<!-- SPDX-License-Identifier: Apache-2.0 -->

# GameEventScript Document TextMate Bundle

This bundle provides modern JSON TextMate-compatible syntax highlighting for
GameEventScript document files (`.gesd`).

## Install

Copy or symlink `GameEventScriptDocument.tmbundle` into a TextMate-compatible
bundle location, then reload bundles in the editor.

The grammar highlights the schema header, keys and labels, typed forms with
their message names, numbers with units, tags, strings and comments. It does
not validate documents.

## Commands

With `ges` on the editor's PATH, Check (Command-B) runs `ges document check` and
Format shows `ges document format` for the saved file. See the
[editor guide](https://gameeventscript.org/docs/tools/editor-bundles/).
