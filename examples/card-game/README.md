<!-- Copyright 2026 Stephan Schlöpke -->
<!-- SPDX-License-Identifier: Apache-2.0 -->

# GES card game prototype

Playable local card games: Highest Card, Blackjack, Mau Mau and Skat. Swift owns
cards and zones; GES defines each deck, setup, legal moves, turn changes and results. The Swift environment runs as a persistent WebAssembly session in a browser worker.

The source lives here in version control. The Website workflow builds and tests
the example and publishes its static output at `/examples/card-game/`.
All generated files and downloaded tools live below `artifacts/card-game`.

## Deck definitions

Mau Mau, Blackjack and Skat combine suits and ranks with
`[:cartesian suits, ranks :select suit, rank => ...]`. The projection builds
one card Map per combination without an intermediate List of pairs. The
example uses the repository's current compiler/runtime; released versions
without collection-source support cannot compile these scripts.

Skat also uses Cartesian projection for bid values and multiple bindings through
`filter` and `select` to arrange cards by suit and rank. Numeric point totals use
`[:sum card => ...]`; text summaries and stateful accumulations retain `fold`.
Adjacent selectors can share one bracket pair, such as `[:distinct :sort ascending]`.
Mau Mau deals in rounds with `for amount in [1, 1, 1, 1, 1] and player in players`,
which runs the same nested loops without constructing a Cartesian-product list.

## Mau Mau

Mau Mau uses a 32-card deck (7–A, four suits), five cards each, and a
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

Open <http://127.0.0.1:8766/>. Choose a game and its player count, then start a game. Click highlighted cards or **Draw a card**.
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
Wasm including Foundation/ICU for the Swift highlighter is about 67 MB; download/startup optimization is not part of this first pass.

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
node examples/card-game/scripts/test-game-library.mjs
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

The top Game selector switches and starts examples, **Draft · autosaved**, or
**Saved**. Editing code or changing the player count updates Draft automatically;
loading another game never overwrites it. **Save** copies the current source and
player count into the single Saved slot. Further edits go to Draft, leaving Saved
unchanged until the next Save. The selected entry and both slots persist in
localStorage; old single-draft storage is imported into Draft. Game progress and
seed are not saved. “New game” applies edits to the running game. GES controls game announcements through NoticeTable and Notice; all examples
announce the winners both on the table and in a dialog. No separate turn, round
or winner status is generated by the view. Load/runtime errors open a dialog. Storage failures are
reported visibly, and in-memory slots remain usable until reload.

To add an example, create games/<id>/<id>.ges and register its id, name, and
examples/<id>.ges path in web/examples.json. Optional players chooses its default
player count. The build copies game sources into the catalog's target directory.

Player-specific duties can be queued with `Action(spec: [player: …, …])` or
`ActionGroup(spec: [player: …, actions: […], …])`. Required offers set a priority
floor, and exclusive groups let the player choose one alternative. Mau Mau uses
this for stacking sevens or drawing the counted penalty before the normal turn.
The group carries the penalty to its recipient; no global penalty state is needed.
Eights store a per-player #skip flag, which BeginTurn handles with ClearActions()
and NextPlayersTurn(). `:board.actions(player)` supports selective removal by
stable references. See the environment contract and browser help for details.

## Highest Card

Select **Highest Card** in the top Game selector. Each of 2–4 players receives
a shuffled 13-card pile in their own suit (2 through ace). Reveal one card per
turn; the highest value wins the round, with the first reveal winning ties.
**Collect trick** keeps the open cards visible until the user collects them.
After 13 rounds, all players tied for the most collected cards win. The game
uses the existing environment unchanged.

## Blackjack

Select **Blackjack** for one to three human players against an automatic dealer
on the table. The default is solo play. Each new game shuffles a fresh 52-card
deck and deals two cards to everyone, with one dealer card hidden. Player hands
are public. Choose **Hit** by clicking the deck, or **Stand** in the player header.

Aces count as 1 or 11; face cards count as 10. Two initial cards totaling 21 are
Blackjack, which beats a three-or-more-card 21. The dealer checks for Blackjack
before players act. Player Blackjack skips that player's turn; hitting to 21 or
busting also ends the turn automatically. After all players finish, the dealer
reveals the hole card and draws below 17, standing on every 17 including soft 17.
No further dealer cards are needed when all players have Blackjack or have busted.

Each hand is evaluated against the dealer separately. A busted player loses even
if the dealer subsequently busts. Equal totals push, and two Blackjacks push.
Badges, table text and a result popup distinguish **Win**, **Push** and **Loss**;
only winning players enter the environment's winner list. The example has no
bets, payouts, double down, split, insurance or surrender. All blackjack rules
and dealer behavior are in GES. See [Blackjack rules](https://bicyclecards.com/how-to-play/blackjack/).

The environment accepts 1–4 players. Catalog entries can constrain their player
selector with `playerCounts`; Draft and Saved preserve that choice along with
source and player count. Existing examples default to their 2–4-player selector.

## Skat

Select **Skat**; the example selects three players automatically. Each new
game is one deal: Player 1 is forehand, Player 2 middlehand and Player 3 rearhand.
The table’s **Reizwerte** button opens a reusable reference with the bid sequence,
Null values, Grand values and the scoring formula, without advancing play.
After dealing 3–skat–4–3, middlehand bids against forehand. Rearhand then bids
against their winner. **Bid** advances to the next legal value, **Hold** accepts
it and **Pass** leaves that duel permanently. Bids range from 18 to 264. If both
others pass without a bid, forehand can play for 18 or pass the deal too.

The declarer chooses **Take skat** or **Play Hand**. Taking the skat adds two cards
to the declarer's hand; two card clicks discard cards into the hidden skat before
game selection. Hand leaves the skat unseen. Choose Clubs, Spades, Hearts,
Diamonds, Grand or Null. Hand suit/Grand games additionally offer Schneider
announced (at least 90 eyes), Schwarz announced (all ten tricks) or Ouvert
(all ten tricks with the hand displayed publicly on the table). Null also offers
Ouvert, with or without taking the skat; only variants covering the bid are offered.

Forehand always leads the first trick, regardless of who declares. Suit games
use the four jacks and the chosen suit as trumps; Grand uses only the jacks.
Follow suit or trump when possible. Null has no trumps and orders cards
A–K–Q–J–10–9–8–7. Click **Collect trick** to collect the visible cards; its winner
leads next. Suit/Grand normally require 61 eyes including the skat. Null requires
taking no tricks and ends in defeat after the declarer's first trick is collected.
Other games play all ten tricks, including unsuccessful Hand announcements.
Before playing your own card, **View last trick** shows the previous collected
trick and its winner in a popup. It can be used repeatedly, including after
another player has led, and neither moves cards nor ends the turn. Once you
play, the action disappears until your next turn.

Scoring includes with/without matadors (including the hidden skat), Hand,
Schneider, Schwarz, announcements and Ouvert. Null values are 23, 35, 46 and 59.
A failed announcement loses at least at its declared level. Overbidding loses at
least the next multiple of the base value covering the bid. Lost games score
double negatively. Winner badges, a popup and the table text show the outcome and
the declarer's signed score; the two defenders win together when the declarer
loses. All-pass deals score zero and have no winner.

Bidding and preparation transfer action ownership through `NextPlayersTurn`;
GES counts tricks separately from the environment's seat-circuit rounds. All
Skat rules live in GES; no Swift game-specific code is needed. This example plays
one deal, without a running match ledger, rotating dealer, bidding jumps,
concessions or tournament dispute procedures. Rules follow the
[International Skat Rules](https://dskv.de/app/uploads/sites/43/2022/11/ISkO-2022.pdf).
