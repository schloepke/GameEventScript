<!-- Copyright 2026 Stephan Schlöpke -->
<!-- SPDX-License-Identifier: Apache-2.0 -->

# Game Event Script for Visual Studio Code

Syntax highlighting for `.ges` source and `.gesa` assembler dumps, including
embedded GES source in dumps. Includes line comments, GES bracket pairing and
automatic bracket indentation, GESA `.region` folding, and GES snippets:
`ges-on`, `ges-iflet`, `ges-fold`, `ges-reduce`, and `ges-after`.

Markdown fenced blocks tagged `ges` or `gesa` are highlighted both in the source
editor and in VS Code's built-in Markdown preview. Backtick and tilde fences are
supported, including uppercase language names. The editor uses the selected
theme; the preview uses a light/dark palette and preserves high-contrast text.
Both use the same GES/GESA TextMate grammars, including GES embedded in dumps.

Highlighting needs no CLI or account. Optional commands use the installed GES
CLI; no compiler or runtime is bundled. Semantic completion and debugging are
not provided.

## CLI commands

For saved GES files, the editor title bar offers **Check**, **Run**, and **Dump**.
All three save pending changes first; cancelling a save cancels the command.
**GES: Compile** is additionally available in the Command Palette.

The editor title bar also provides buttons for running with arguments (`run-all`),
starting the REPL (`terminal`), and restarting it (`debug-restart`). All commands
are also available in the Command Palette. VS Code may move buttons into the
overflow menu when the editor is narrow.

- **GES: Run with Arguments…** asks for arguments delivered as Text values to
  `Main(args)`. Separate arguments with spaces; single or double quotes preserve
  spaces and `""` supplies an empty argument. A backslash escapes quotes,
  backslashes and unquoted whitespace. Other backslashes remain literal.
  Arguments follow CLI `--`; no shell expansion or commands are evaluated.
- **GES: Start Interactive Session** saves the source and opens
  `ges run <file> --interactive --color` in an interactive terminal. Use `:help`
  for CLI commands. Starting again focuses the existing session for that file.
- **GES: Restart Interactive Session** saves the current source, closes its old
  terminal and starts a fresh session. Runtime state is lost. Changes are not
  automatically reloaded; `:load` adds a program rather than replacing one.
  Closing the terminal ends the session. Different files have separate sessions.

- **Check** validates the source in a VS Code task terminal.
- **Run** executes it in a task terminal. Use VS Code's **Terminate Task** to stop it.
- **Compile** writes the sibling `.gesb` file in a task terminal.
- **Dump** compiles to a temporary binary and opens a read-only GESA document
  with highlighting. Compilation errors prevent opening stale dumps. Temporary
  files are removed afterwards; failures are shown in the **GES** output channel.

Each command works on the current file, with its directory as working directory.
Configure **GES: Cli Path** (`ges.cliPath`) to `ges` (default), `dotnet-ges`, or an
absolute executable path. This is an executable path, not a shell command with
arguments. VS Code must have the CLI on its own PATH unless an absolute path is
configured. Commands require a trusted workspace; highlighting remains available
in Restricted Mode.

## Install locally

The prebuilt `GameEventScript-VSCode.vsix` is included
in this directory. Installation needs no Python, Node.js, or build step.

In VS Code:

1. Open the Command Palette (`Cmd+Shift+P` on macOS).
2. Choose **Extensions: Install from VSIX...** and select
   `tools/editors/VS Code/GameEventScript-VSCode.vsix`.
3. Reload the window if prompted and open a `.ges` or `.gesa` file.
   The status bar should show **Game Event Script** or **Game Event Script Assembler**.

Alternatively, if `code` is available on your PATH:

```sh
code --install-extension "tools/editors/VS Code/GameEventScript-VSCode.vsix"
```

To update a locally installed copy, install the updated VSIX again (add `--force`
when using the CLI for the same version). To uninstall, remove **Game Event Script**
from the Extensions view. No Marketplace publication is required.

## Maintaining the extension

From the repository root, with Python 3 and Node.js/npm installed:

```sh
python3 scripts/sync-editor-bundles.py
bash scripts/package-vscode.sh
```

The packaging script downloads the pinned official `@vscode/vsce` build tool
and the lockfile-pinned preview dependencies through npm if needed. It stages
the extension and installs dependencies under `artifacts/editors`, then replaces the
prebuilt VSIX in this directory after successful packaging. Its npm cache stays
under `artifacts`. Commit the updated VSIX together with its sources. The script
does not publish or install the extension.

Edit the canonical JSON TextMate grammars under `tools/editors/TextMate` and
the snippets in `tools/editors/support/completions.json`, then run
`python3 scripts/sync-editor-bundles.py`. Do not hand-edit this extension's
generated `syntaxes`, `snippets`, or `LICENSE` files.

The manifest and language configurations are maintained here. Verify with
`python3 scripts/sync-editor-bundles.py --check` and
`python3 scripts/test-editor-bundles.py`. Strict JSON files inherit the copyright
and Apache-2.0 license notice above, as specified by the repository licensing policy.
Run `node --test scripts/test-vscode-extension.cjs` for CLI command regression checks.
After packaging, `node --test scripts/test-vscode-markdown.cjs` checks preview
HTML escaping, grammar reuse and Markdown fence injection. The Python editor
checks also run these tests against the extracted VSIX when Node.js is available.

The preview bundles Microsoft's MIT-licensed `vscode-textmate` and
`vscode-oniguruma` packages. Their licenses and Oniguruma's third-party notices
are retained inside the VSIX. These dependencies belong only to this editor
extension, not the GES libraries or CLI.
