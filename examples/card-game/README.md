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
shared draw and discard pile. Match suit or rank, draw once, then play or pass.
An 8 skips the next player. A 7 starts a two-card debt; another 7 adds two and
passes it on. After the first penalty draw, stacking is no longer available.
Penalty cards are drawn individually as required actions, followed by the normal
turn (including one optional draw). Shortages draw all available cards.
A jack may be played on any card except another jack and requires an explicit choice of suit, including
when it is the final card. Other final cards win immediately; the initial discard
has no special effect. GES recycles discards while keeping the top card.
If no draw is possible, blocked players pass; when all players are blocked, the
rules declare a draw. Select two, three, or four players before starting a new game.

## Play in the browser

The prepared build can be served immediately:

```sh
python3 examples/card-game/scripts/serve.py
```

Open <http://127.0.0.1:8766/>. Choose 2–4 players and start a game. Click highlighted cards or **Draw a card**.
Expand the GES editor, change a rule, and choose **New game** to compile it
in the browser. The editor uses the existing Swift syntax highlighter through
Wasm, including while editing incomplete code. Its dedicated worker returns
UTF-16 ranges; a text layer paints them behind the native textarea without
changing selection or undo. Highlighting failures fall back to plain text.
A loading bar shows Wasm download progress (percent and MB when the response size
is known), followed by a separate initialization status. Unknown or compressed
response sizes use an indeterminate bar. The editor shows its own loading status.
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

- `games/mau-mau/mau-mau.ges`: Mau Mau setup and rules.
- `games/high-card/high-card.ges`: Highest Card, a small example with mandatory reveals, open tricks and round scoring.
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

## Board setup and lifecycle

Table zones support piles and open card spreads at nine compass positions; zones in each row
follow creation order from left to right. Players sit in bottom/left/top/right order with three or four players;
two players sit at bottom/top. `:board.create(setup: board(players: players))` creates the entire board
atomically from flat table and player zone lists. Zone IDs are tags. Table positions
use nine compass tags; player positions use #left, #center or #right. Optional
newRow: true starts a row within the player area or table position. Side players rotate
±90° while top and bottom stay upright. Initial cards can use `deck()[:shuffle]`.

The environment runs `PrepareGame(players)` and all its queued messages, then
`BeginRound`, `BeginTurn`, action requests, `EndTurn`, `EndRound` and `EndGame`.
Rounds count a circuit from the starting player, including skipped seats; an
incomplete final round closes before EndGame. An EndRound handler controls
continuation with NextRound and may offer between-round actions; without one
continuation is automatic. `:board.reverse()` supports reversed seat order. GES owns discard recycling and
shuffling through `[:shuffle]` and `:board.movecards`; native draws never refill a pile. Rules offer named actions
with `Action` and typed handler references. Consumption may be automatic,
manual, counted, or disabled. Required actions must be consumed before turn end.
`NextPlayersTurn(nextPlayer: X)` chooses the next player;
`NextPlayersTurn(repeatTurnForPlayer: true)` repeats the current player after a full circuit.
Optional setup `actions` persist across turns and invoke handlers without player.
`Notice` opens dialogs; `NoticeTable` updates a persistent centered table status. See `environment/Contract.md` for signatures and restrictions.

Board creation, card movement, rule-state writes and direction changes are synchronous
`:board` extensions. They return results immediately; old values remain immutable
snapshots. Actions explicitly emit `Complete(action: action)` after applying their
effects. Lifecycle transitions, offers and notices remain queued messages.

The browser seed field is optional. Leave it empty for a fresh random seed on
each new game, or enter an Int32 seed (including 0) for reproducible deals.

Mau Mau creates the board, deals cards and places the first discard directly in
PrepareGame. Custom preparation messages remain possible. Once all queued
preparation messages finish, the first round starts automatically with player 0.

The editor automatically stores its current source in localStorage for this
browser and origin, including unfinished code. It restores the draft on reload.
Game progress is not saved. “Load example” explicitly replaces the source, with
“Undo example load” available until the next page reload; “New game” applies it.
Storage failures are shown beside the editor. Clearing browser site data removes
the saved draft. To add an example, create games/<id>/<id>.ges and register its
id, name, and examples/<id>.ges path in web/examples.json. The build copies all
game sources into the example catalog's target directory.

Player-specific duties can be queued with `Action(spec: [player: …, …])` or
`ActionGroup(spec: [player: …, actions: […], …])`. Required offers set a priority
floor, and exclusive groups let the player choose one alternative. Mau Mau uses
this for stacking sevens or drawing the counted penalty before the normal turn.
The group carries the penalty to its recipient; no global penalty state is needed.
Eights store a per-player #skip flag, which BeginTurn handles with ClearActions()
and NextPlayersTurn(). `:board.actions(player)` supports selective removal by
stable references. See the environment contract and browser help for details.

## Highest Card

Select **Highest Card** and choose **Load example**. Each of 2–4 players receives
a shuffled 13-card pile in their own suit (2 through ace). Reveal one card per
turn; the highest value wins the round, with the first reveal winning ties.
**Collect trick** keeps the open cards visible until the user collects them.
After 13 rounds, all players tied for the most collected cards win. The game
uses the existing environment unchanged.

## Skat – Trick Play

Load **Skat – Trick Play**; the example selects three players automatically.
This first stage fixes Player 1 as declarer, hearts as trump and Player 1 as the
opening leader. It deals 3–skat–4–3, enforces following suit/trump, ranks the four
jacks above the seven heart cards, and lets the trick winner lead next. Click
**Collect trick** after the third card to keep the completed trick visible.
The untouched skat counts toward the declarer's 61-eye target; otherwise both
defenders win. The score totals 120 eyes. Other player counts end immediately
with an explanatory notice.

Tricks are counted explicitly in GES by three played cards, independently of the
environment's seat-circuit rounds. This needs no Swift special case. Bidding,
skat exchange, game selection, Schneider/Schwarz and game-value scoring are not
part of this stage. Card ordering, following and eye counting follow the
[International Skat Rules](https://dskv.de/app/uploads/sites/50/2020/08/Internationale-Skatordnung-2018.pdf).
