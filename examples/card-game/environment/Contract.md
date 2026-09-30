<!-- Copyright 2026 Stephan Schlöpke -->
<!-- SPDX-License-Identifier: Apache-2.0 -->

# Card environment contract — prototype

This is an experimental host contract, not a change to the GES language or public
Runtime API. Bindings are handwritten while the first game exercises the design.
There is no schema generator yet.

## Ownership and values

Swift owns the mutable board. GES defines the deck, zones, setup, legal actions,
turn progression, and victory conditions. A card has a stable positive ID and an
immutable property map defined by GES. A zone has a unique text ID, optional
zero-based player owner, visibility, and an ordered sequence of card IDs. The
last element is the top of a pile. Every created card belongs to exactly one zone.

Queries return immutable GES snapshots. They are evaluated when a handler runs,
not when its triggering message was queued. All calls and actions are serial.

## Read-only extensions

| Extension | Result |
| --- | --- |
| `:board.cards(zone: Text)` | List of `[id: Number, properties: Map]`, bottom to top |
| `:board.card(id: Number)` | Card snapshot, or Nothing for an unknown ID |
| `:board.top(zone: Text)` | Top card snapshot, or Nothing for an empty zone |
| `:board.current()` | Active player's index, or Nothing |
| `:board.playercount()` | Number of players |
| `:board.players()` | List of player indices in registration order |

Unknown zones are binding errors. Parenthesize extension calls before applying a
collection selector: `(:board.cards(zone: 'draw'))[:count]`.

## Setup commands

The embedding sends `Setup(players)`. GES may emit these commands during setup:

| Message | Effect |
| --- | --- |
| `CreateZone(zone, owner, visibility)` | Creates a zone; visibility is hidden, owner, top, or public |
| `CreateCard(zone, properties)` | Creates one card from its property map |
| `Shuffle(zone)` | Uses the host's seeded random generator to shuffle a zone |
| `Take(source, destination, count)` | Moves the top count cards, one at a time |

Setup is bounded to 64 zones and 512 cards. These commands become unavailable
once the initial setup pump completes. They are intended for trusted game rules.

## Turns and requests

| Message | Effect |
| --- | --- |
| `BeginTurn(player)` | Selects the active player, clears offers, emits `PlayerRoundStart(player)` |
| `Allowed(player, actions)` | Replaces offers for the active player with a list of `[kind: Text, card: Number?]` |
| `MoveCard(request, card, source, destination)` | Validates membership, moves the card, completes the request |
| `DrawCard(request, source, destination, recycle, keep)` | Draws one card; if source is empty, recycles all but the top keep cards from recycle and shuffles |
| `DrawCards(source, destination, count, recycle, keep)` | Rule effect: draws exactly count cards after a completed request, recycling as necessary |
| `Notice(text)` | Sets a display explanation for the last completed action |
| `Complete(request)` | Completes an action without moving cards, e.g. pass |
| `Reject(request, reason)` | Explicitly rejects the pending action |
| `Finish(winner)` | Ends the game, or declares a draw when winner is Nothing |

The embedding submits an action with a player, action kind, optional card, and
expected board revision. Invalid player IDs, stale revisions, and requests after
the game ends are rejected immediately. Otherwise GES receives
`ActionRequested(request, player, action, card)` and rechecks its rules.

A successful command clears the pending request and emits
`ActionCompleted(request, player, action)`. The rule then chooses the next turn
or finishes. A rejection keeps the existing action offer. Successful actions
increment the revision only after the complete cascade finishes. Offers are UI
hints; they do not replace the GES rule check.

A move or draw validates its mechanical changes before committing a replacement
board. The environment does not roll back an entire GES cascade: a later rule
fault stops the session, clears offers, and requires a restart. Unanswered
requests also fail the session. Source size, per-handler work, queued messages,
and total pump count are bounded. The browser additionally terminates a worker
that exceeds its wall-time limit.

## Adapters

The browser provides two-player hot-seat play; the environment accepts 2–4 players.
The Wasm adapter exposes a persistent two-player session through a small C ABI.
Its JSON projection provides display strings for card properties. It is a local
presentation protocol, not a lossless general-purpose GES value serialization.

Player-filtered views omit hidden card IDs and opponents' hands. Trusted native
code can inspect the full board. Browser users control their own worker and have
access to the rules and seed: this is not a secure multiplayer implementation.
A network game would require an authoritative host and authenticated players.

The first game uses 7–A in four suits, five cards per player, matching suit or
rank, one-card draws ending the turn, with an 8 skipping the next player and a 7 drawing two cards for that player
and skipping their turn. No stacking. With fewer than two available cards, GES
requests the remaining count. A final card wins immediately; the initial
discard triggers no effect. The top discard
survives recycling. With no possible draw, a player without a legal card passes;
if every player is blocked, the rules declare a draw. An empty hand wins.


## Web highlighting

The Wasm adapter imports the unchanged Swift SyntaxHighlighter package and exports
`cardgame_highlight`. It returns completion plus `[start, length, kind]` arrays in
UTF-16 offsets. An independent worker debounces editor input and ignores stale
results. The native textarea retains input, selection, accessibility, and undo;
an aria-hidden pre element paints categories with CSS. Empty, incomplete, and
non-ASCII source is supported. If highlighting fails or times out, plain editing
remains available. This browser adapter adds no public highlighter API.

`cardgame_check` compiles source without creating a host, preserving any active
session. Diagnostics expose code, message and optional one-based start/end line
and column. Compiler columns count Unicode scalars; the editor maps these to
UTF-16 offsets, including CRLF and astral characters. EOF diagnostics receive a
visible marker. Automatic checks share the editor worker and revision guard;
editing immediately clears stale diagnostics. This is compilation only, not
linking against the board or execution of setup/game rules.
