<!-- Copyright 2026 Stephan Schlöpke -->
<!-- SPDX-License-Identifier: Apache-2.0 -->

# GES card game prototype

A playable, local Mau Mau: Swift owns cards and zones; GES defines the deck,
setup, legal moves, turn changes, and winner. The Swift environment runs as a persistent WebAssembly session in a browser worker.

The source lives here in version control. The Website workflow builds and tests
the example and publishes its static output at `/examples/card-game/`.
All generated files and downloaded tools live below `artifacts/card-game`.

## Game

The default game uses a 32-card deck (7–A, four suits), five cards each, and a
shared draw and discard pile. Play the same suit or rank, or draw one card and
end the turn. Empty hand wins. An 8 skips the next player. A 7 makes the next player draw two cards and skip
their turn; sevens cannot be stacked. When fewer cards are available, they draw
what remains. The final card wins immediately without applying a penalty, and
the initial discard has no effect. Jack effects and Mau-call penalties are not
implemented. The discard pile is recycled while preserving its top card.
If no draw is possible, blocked players pass; when all players are blocked, the
rules declare a draw. The library also supports three or four players.

## Play in the browser

The prepared build can be served immediately:

```sh
python3 examples/card-game/scripts/serve.py
```

Open <http://127.0.0.1:8766/>. Click highlighted cards or **Draw a card**.
Expand the GES editor, change a rule, and choose **New game** to compile it
in the browser. The editor uses the existing Swift syntax highlighter through
Wasm, including while editing incomplete code. Its dedicated worker returns
UTF-16 ranges; a text layer paints them behind the native textarea without
changing selection or undo. Highlighting failures fall back to plain text.
**Stop** terminates the worker. Startup allows 120 seconds for the first download; a 15-second watchdog also
terminates unresponsive compilation/execution.

To reproduce the Wasm build on macOS:

```sh
python3 examples/card-game/scripts/setup-wasm.py
python3 examples/card-game/scripts/build.py wasm
python3 examples/card-game/scripts/serve.py
```

Setup downloads the official Swift 6.4.0 Wasm SDK and matching Swift.org macOS
toolchain, checks pinned SHA-256 hashes, checks the toolchain installer signature
before first extraction, and expands it locally without running its installer.
It installs the pinned `@bjorn3/browser_wasi_shim` 0.4.2 package into artifacts
with npm scripts disabled. Python 3.11+, npm, curl, tar, and pkgutil are needed.
The toolchain download is about 1.5 GB; subsequent runs reuse it. System Swift,
Xcode selection, and global SDK installations are not changed.

Xcode's bundled Swift lacks some Wasm linker tools. See the official [Swift Wasm setup](https://www.swift.org/documentation/articles/wasm-getting-started.html).
The browser uses a small [WASI reactor interface](https://book.swiftwasm.org/examples/exporting-function.html),
without a JavaScriptKit or SwiftSyntax dependency. The uncompressed prototype
Wasm including Foundation/ICU for the Swift highlighter is about 63 MB; download/startup optimization is not part of this first pass.

The Wasm build stages Runtime, Compiler, and SyntaxHighlighter sources under
`artifacts/card-game/wasm-source`. It adds WASILibc imports and a tiny monotonic
clock C shim **only to that staged Runtime copy**. No tracked repository source
is patched. Patch anchors deliberately fail if the source changes incompatibly.
This proves this game's browser path, not full Runtime Wasm conformance.

The generated web directory contains local JS, CSS, GES, Wasm, and the browser
shim's license files. The demo makes no external network calls and needs no game
server. The website stages these assets during preparation; no SDK or generated binary
is checked into the source branch.

## Tests

```sh
python3 examples/card-game/scripts/build.py test
python3 examples/card-game/scripts/build.py wasm
node examples/card-game/scripts/test-wasm.mjs
```

Native tests cover setup, determinism, hidden-hand projection, invalid/stale
requests, draw/turn changes, card conservation, recycling, victory, blocked-game
draws, four players, invalid mechanical commands, missing rule responses, and
execution limits. Rule-edit tests change the GES matching function and initial
hand size without changing Swift.

The Wasm test runs 30 complete seeded games and checks card conservation,
turn changes, and special-card effects.
It also checks rejections, stale revisions, recycling, edited GES, restart,
compiler errors, and bounded message loops. Browser interaction has additionally
been checked for playing, drawing, rule editing, stopping, and restarting.

## Files and contract

- `games/mau-mau/rules.ges`: complete game setup and rules.
- `swift/Sources/CardGameEnvironment/`: board mechanics, GES bindings, and views.
- `swift/Sources/CardGameWasm/`: serial buffer-based C ABI for a persistent session.
- `web/`: browser worker and small hot-seat interface.
- `environment/Contract.md`: experimental host/query/message contract.
- `scripts/`: local build, setup, serve, and Wasm verification entry points.

Bindings are handwritten. A declarative generator is not implemented yet; this
example supplies the concrete API to evaluate before designing that generator.
There are no changes to the GES language or core public API snapshots.

Player-filtered views omit hidden card identities, but local players can inspect
the worker and seed. This is a hot-seat prototype, not secure online multiplayer.
The JSON view renders card properties as display strings; it is not the lossless
GES product-value codec. No persistence, bots with strategy, or graphical editor
is included.

The web editor compiles after a short typing pause or when **Check code** is
clicked. Compiler diagnostics underline source ranges in red; click a diagnostic
to select its source location. This check does not run initialization or change
the current game. It checks compilation, not host linking or game-rule correctness.

Game startup, runtime/native-handler, worker and timeout failures open a modal
with the full error text, including available handler and technical details.
Close it with **Close** or Escape; **Error details** reopens the last error.
Starting a new game clears it. Closing the dialog does not resume a failed game.
