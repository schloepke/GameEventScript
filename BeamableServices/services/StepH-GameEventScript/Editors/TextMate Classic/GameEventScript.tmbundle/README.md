# GameEventScript TextMate Classic Bundle

This bundle provides classic XML plist TextMate-compatible syntax highlighting
for GameEventScript source files. `.ges` is the canonical and only registered
source extension.

## Install

Copy or symlink `GameEventScript.tmbundle` into a TextMate-compatible bundle
location, then reload bundles in the editor.

Common examples:

- TextMate: `~/Library/Application Support/TextMate/Bundles/`
- Sublime Text: `~/Library/Application Support/Sublime Text/Packages/`

Use this variant for CodeRunner 4 and older TextMate-compatible importers that
expect `.tmLanguage` and `.tmPreferences` plist files.

The grammar is intentionally lightweight. It highlights declarations, handlers,
messages, keywords, tags/types, pipeline selectors, literals, comments, and
ASCII/Unicode operators.
