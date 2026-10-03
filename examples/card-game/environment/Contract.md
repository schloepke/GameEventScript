<!-- Copyright 2026 Stephan Schlöpke -->
<!-- SPDX-License-Identifier: Apache-2.0 -->

# Card environment contract — prototype

Swift owns mutable cards, zones and the serial lifecycle. GES owns the deck,
actions, effects and winner. This handwritten example does not change the GES
language or portable Runtime API. No generator or compatibility layer is included.

## Declarative board setup

Emit `CreateGame(setup: board(players: players))` once during preparation.
The entire board validates before replacing any state; no partial setup is committed.
CreateZone is no longer a native message. The shape is:

```ges
function hand(player) be ('hand' + (player as :Text)) as :Tag
function board(players) be [
    table: [
        [id: #draw, label: 'Draw pile', position: #center,
            visibility: #hidden, cards: deck()[:shuffle]],
        [id: #discard, label: 'Discard pile', visibility: #top],
        [id: #reserve, label: 'Reserve', position: #center, newRow: true]
    ],
    players: players[:select player => [
        id: player,
        zones: [
            [id: hand(player: player), label: 'Hand', position: #left, layout: #spread]
        ]
    ]]
]
```

Both table and players use flat zone lists in definition order. Table positions
are `#nw, #n, #ne, #w, #center, #e, #sw, #s, #se` (default #center).
Table aliases are #left → #w, #right → #e, #top → #n, #bottom → #s.
They share the same rows as their compass equivalents; snapshots return compass tags.
Each position maintains its own current row. `newRow: true` starts another row
at that position; intervening zones at other positions do not reset it.
Zones in each table row are centered together. Missing outer tracks collapse;
zones wrap rather than clip.

Players have zero-based numeric IDs; every selected player must occur exactly once.
Player zone `position` aligns the whole row: #left (initial default), #center,
or #right. An omitted position inherits the current player's row alignment,
including on a new row. An explicit change requires `newRow: true`.
The first zone always starts row zero, even with newRow: true; it creates no empty row.
newRow defaults to false and must be Boolean when supplied.
The left player area rotates 90° clockwise and the right 90° counterclockwise,
including cards, labels and action buttons. Top and bottom stay upright.
Rows and alignment are interpreted before rotation. Side areas retain the table
height and a narrow width; additional rows remain accessible by scrolling.

Zone id is a required Tag; label is required Text. All zone references in commands,
action bindings and extensions use Tags too (for example `zone: #draw`).
Player IDs and individual card IDs remain numeric. Zone IDs are globally unique.
Optional defaults: layout #pile, visibility #public for table zones or #owner for
player zones, cards []. Both table and player zones permit #pile and #spread. Use #spread with #public for an open card layout.
Visibility accepts #hidden, #owner, #top, #public.
Unknown fields, invalid tags and duplicate IDs fail setup.
Limits: 64 zones, 512 cards across the board, 16 rows per player or table position.

Cards are property maps. GES shuffles initial lists using `[:shuffle]` with
the host's seeded random stream. Cards receive unique IDs in traversal order
(table, then players/zones); the last card is the top.
All recycling and shuffling belongs to GES. MoveCards moves explicit card IDs
in the supplied order without consuming randomness. DrawCard never refills a pile.
There are no native CreateDecks or Shuffle messages.
Take is available during preparation, round hooks, actions and EndTurn. Emit DealCards after CreateGame
to deal from the fully created board.

## Lifecycle

The environment drains each phase before sending the next event:

1. `PrepareGame(players)` receives all player IDs in seat order. It creates zones and decks and may emit custom messages to deal cards. All resulting queued messages drain before the environment automatically starts the first round and the first turn with player 0.
2. `BeginRound(number, players)` runs before the first turn of each round.
3. `BeginTurn(player)` must offer actions, request `NextPlayersTurn()` to skip, or request `EndGame()`.
4. Selecting an action invokes its registered GES handler with an activation ID, player, and optional selected card.
5. When a consumed action requests automatic turn end (or GES emits `NextPlayersTurn()`) and all required actions are consumed, `EndTurn(player)` applies effects.
   The environment then advances to the next player. Crossing the round boundary
   sends `EndRound(number, players)`. A matching GES handler must request `NextRound()`
   before the next BeginRound/BeginTurn. Without a matching handler this is automatic.
6. `EndGame()` requests `EndGame(players)` after the current phase drains. This
   handler must emit `Finish(winners)` with a list of distinct player IDs; `[]` means nobody wins.
   Before EndGame, the open round receives EndRound exactly once, even if incomplete.

Rounds are numbered from 1. Every player-count seat advancements completes a
round; skipped seats count too. Reversing direction changes the next seat but
never resets round progress. Repeating the player advances one complete circuit.
Extra skipped seats beyond a boundary count toward the next round.
Round hooks receive all players in seat order. EndRound can offer Action choices
with finishTurn: false, allowing human card draws between rounds. The current
player during this pause is the selected next starting player. NextRound may be
emitted in EndRound or after accepting a between-round action. It waits until all
required actions are consumed. It does not dispatch another EndRound.
A handler that neither requests NextRound nor provides an action leaves the game
paused. No fallback Continue button bypasses the rules. No EndRound handler means
automatic continuation. A name-only EndRound handler also counts.
EndGame bypasses the pause; the final partial round still receives EndRound once.
Use `:board.ending()` to avoid offering inter-round actions during game shutdown.
NextRound during shutdown is ignored. `:board.roundnumber()` returns 0 in preparation.

```ges
on EndRound(number, players) {
    if not :board.ending() {
        emit Action(action: #roundDraw, label: 'Draw round cards', optional: false,
            finishTurn: false, consumable: 2, handler: RoundDraw(action, player), zone: #draw)
    }
}
on RoundDraw(action, player) {
    // Refill first in GES if this game requires it.
    emit DrawCard(action: action, source: #draw, destination: hand(player: player))
    emit NextRound()
}
```

The first draw requests continuation, but the second required draw must complete
before it takes effect. Inter-round offers expire before the next BeginRound; queued offers retain their own lifetime.

`NextPlayersTurn()` and `EndTurn(player)` are different signatures: the former is a
native command, the latter a rule event. The same distinction applies to
`EndGame()` and `EndGame(players)`. Initial startup and ordinary turn advancement belong to the environment.

`NextPlayersTurn()` requests normal advancement. `NextPlayersTurn(nextPlayer: X)` selects a
valid player ID; `NextPlayersTurn(repeatTurnForPlayer: true)` selects the current player.
`false` requests normal advancement. These commands may be emitted in `BeginTurn`, after accepting
an action, or inside the `on EndTurn(player)` effect handler. A direct skip still
runs EndTurn and the round hooks; at most 128 automatic turns may run without
returning to human actions or a round pause. Required actions block a direct skip. The last processed
NextPlayersTurn command determines advancement; it does not recursively dispatch EndTurn(player).
Without an explicit command, finishTurn uses normal advancement. Required actions
must still be consumed. The target is reached forward in seat order; selecting the
current player advances a full circuit. Skipped seats count toward round boundaries.
The next-player override resets at the next turn. There are no SkipNextPlayer or
SkipRound messages. `:board.next(player)` reflects already processed overrides.
Direction starts at +1 (0, 1, 2, 3). `ReverseDirection()` toggles it to -1 and back;
`:board.direction()` returns +1/-1. It is available in preparation, round hooks,
actions and EndTurn. Emit a reversal before selecting an explicit next player.
`:board.next(player)` respects direction; two nested calls skip one player.
With two players, reversing alone still selects the other player. A game such as
UNO can explicitly repeat the turn for its two-player reverse rule.

### Dealing order

Use nested loops to choose batches per player. For example, three cards to each
player, then two to each, then three to each:

```ges
// At the end of PrepareGame(players), after creating zones and decks:
// emit DealCards(players: players)

on DealCards(players) {
    for amount in [3, 2, 3] {
        for player in players {
            emit Take(source: #draw, destination: hand(player: player), count: amount)
        }
    }
}
```

Mau Mau uses `[1, 1, 1, 1, 1]` for five rounds of one card per player.
Queued takes execute in emitted order and finish before the first round begins.
`DealCards` and `BeginGame` are ordinary script-defined messages. The environment
neither sends them nor reserves their names; any custom preparation messages can
be used. Once the entire preparation queue drains, the first round starts automatically with player 0.

## Actions and effects

Activate current-turn actions in BeginTurn or after accepting an action; lifecycle hooks can also queue offers:

```ges
emit Action(action: #draw, label: 'Draw', optional: true,
    finishTurn: false, consumable: #auto, handler: Draw(action, player), zone: #draw)
```

`action` is a tag unique within the turn; `label` is Text. A fresh numeric
activation ID is passed to the handler as `action`; use it in completion and
consumption commands. The handler is a GES Handler value, not a name string.
A final `zone: Tag` argument associates a button action with an existing
zone. The browser makes that zone clickable and highlights it in green, including
empty piles that can be recycled. The highlight disappears when the action is
consumed. With multiple actions on one zone, the first uses the zone and the rest
appear as buttons in the zone heading. Alternatively, `area: #table` or
`area: player` places a button beside the table or player heading.
Each activation requires exactly one presentation: `zone`, `area`, or `cards`.
There is no global action bar.
Buttons use a handler with `(action, player)`. Card actions add a final
`cards: [id, …]` argument and use `(action, player, card)`. All selections for
one activation share its counter. An empty card list temporarily offers no
selection. `SetActionCards(action: #play, cards: …)` refreshes the choices
without resetting consumption. Current-turn activations expire at turn end; queued offers have the lifetime described below.
A global tag is reserved for the game; other tags are unique per player.

| Consumption | Meaning |
| --- | --- |
| `#auto` | Consume after one successful execution |
| Positive integer (1–1000) | Consume after that many successful executions |
| `#manual` | Handler explicitly emits ConsumeAction after successful completion |
| `#never` | Reusable throughout the turn; requires optional: true and finishTurn: false |

`optional: false` prevents turn end until the activation is fully consumed.
`finishTurn: true` requests turn end when fully consumed. If other required
actions remain, turn end waits until those are consumed too.
Only successful executions count; rejected requests retain offers and counters.
The browser submits an offer index and revision, and optionally matches its
activation ID. Stale and wrong-player requests never execute GES.

| Command | Effect |
| --- | --- |
| `MoveCard(action, card, source, destination)` | Move and accept the current action |
| `DrawCard(action, source, destination)` | Draw one from a nonempty source and accept; never refill or shuffle |
| `Complete(action)` | Accept without moving a card |
| `Reject(action, reason)` | Reject with Text reason |
| `ConsumeAction(action)` | Consume the successfully completed current #manual activation |
| `ClearActions()` / `ClearActions(actions)` | Remove current-player offers or queried entries, including groups and queued duties |
| `SetState(key, value)` | Store immutable rule data under a Tag key; nothing deletes it, maximum 64 keys |
| `SetCardOrder(zone, cards)` | Replace zone order with an exact permutation of its card IDs; never shuffle natively |
| `SetActionCards(action, cards)` | Update an active card action by tag |
| `Take(source, destination, count)` | Move top cards during preparation, round hooks, actions or EndTurn; does not accept an action |
| `MoveCards(source, destination, cards)` | Move distinct card IDs in supplied order in the same phases; does not accept an action |
| `Notice(text)` | Enqueue a modal dialog; multiple notices display in order |
| `NoticeTable(text)` | Replace the centered table status text; empty Text clears it |

State is owned by this embedding, not by mutable GES variables. `:board.state(key)`
returns an immutable value or nothing. Test absence with `is nothing`, not equality.
Mau Mau stores #wish globally and #skip per player; Swift has no special handling for either key.

Complete exactly once per invocation. Automatic consumption and turn completion
happen after the handler's entire message cascade drains. Explicit consumption
must follow acceptance. A missing response or a turn with no selectable actions
fails the session unless the game ends. After acceptance GES may activate more
actions, refresh cards, or request EndGame. `:board.lastaction()` returns the
last accepted action tag name as Text. No central ActionRequested event remains.

DrawCard selects the source’s top card; MoveCard selects an explicit card ID. Both
use the same validated movement and action-completion path. Neither command
recycles or shuffles cards. Mau Mau shares its recycling rule in the GES DrawOne
handler for normal and penalty draws.

Mechanical moves/draws and deck batches validate before commit. There is no
rollback of an entire multi-command rule cascade. Runtime faults stop the game
and require a restart. Compiler checks do not validate native message signatures.

### Player offers, priorities and alternative groups

`Action(spec: map)` accepts the existing action fields plus numeric `priority`
(default 0) and optional `player`. `optional` defaults to true, `finishTurn` to
false and `consumable` to #auto. The positional Action signatures remain concise
forms for ordinary current-turn actions. Creation/editing is allowed in lifecycle
hooks or after accepting an action; offers created during preparation require a
player. Exactly one of area, zone and cards is required. Area controls presentation,
not the recipient.

Without player, an offer belongs to the current turn (or the current inter-round
pause). An explicit player queues it for that player's **next BeginTurn**, even if
that player is currently active. It survives other players' turns and rounds.
BeginTurn can inspect and clear it before anything is presented to the user.
No out-of-turn execution is supported.

```ges
emit ActionGroup(spec: [
    group: #sevenPenalty, player: :board.next(player: player),
    priority: 10, optional: false, exclusive: true, data: 4,
    actions: [
        [action: #stack, label: 'Stack 7', handler: StackSeven(action, player, card), cards: sevens],
        [action: #penalty, label: 'Draw penalty card', consumable: 4,
            handler: PenaltyDraw(action, player), zone: #draw]
    ]
])
```

A group defaults to priority 0, optional false and exclusive true. `data` is
optional immutable rule metadata, returned by queries; the engine never interprets
it. Group alternatives have no separate player, priority or optional field.
Nested groups are not supported. Completing any alternative completes the group
and disables its other alternatives. An exclusive group locks to the first
**successfully executed** alternative; rejection does not lock it. Counted actions
must finish all executions, manual actions require ConsumeAction.

The highest priority among active, unfinished mandatory actions/groups is the
priority floor. Player offers below it cannot execute and are not presented.
Offers at the same or higher priority remain selectable. Optional offers never
raise the floor. If no mandatory offer remains, all remaining offers are available.
Global setup actions remain accessible independently of this floor.
No priority automatically executes an action. Required groups/actions also block
turn completion regardless of priority; queued offers that have not reached their
recipient's turn do not block the current player.

At turn end, current-turn offers and completed queued offers are removed, including
all alternatives of completed groups. Open queued offers remain, with counters and
exclusive selection preserved. EndTurn may queue new offers before cleanup. All
gameplay offers stop at EndGame. Tags are unique per player across groups and
individual actions; global tags may not collide with any player offer. A consumed
tag remains reserved until cleanup or explicit removal. Maximum 256 registered
actions and groups combined.

`:board.actions(player: player)` returns groups and their individual alternatives,
as well as standalone actions, including queued, consumed and priority-blocked
entries. Fields: `ref` (stable numeric identity), `id` (Tag), `kind` (#group/#action),
`player`, `priority`, `optional`, `queued`, `active`, `consumed`. Actions additionally
have `group` (Tag or nothing) and `remaining`; groups have `exclusive` and `data`.
Here active means the offer reached its recipient's current turn, not necessarily
that priority or exclusive selection currently permits it.

```ges
emit ClearActions() // all current-player offers, including queued groups
emit ClearActions(actions: (:board.actions(player: player))[:filter item where item.id = #sevenPenalty])
```

Explicit lists use the queried ref, not just tags. Stale references are harmless;
they never delete a newly created offer using the same tag. Removing a group removes
its alternatives. Removing its last alternative removes the empty group. Removing
the selected alternative releases the exclusive selection for remaining alternatives.
ClearActions without arguments preserves other players' offers and global actions.
Queries and explicit removal can address another player's queued offers.

`SetState(player, key, value)` and `:board.state(player, key)` provide a separate
64-key state store for each player. Nothing deletes a key. For example, GES stores
#skip on the next player. Their BeginTurn clears the flag, calls ClearActions(),
and then NextPlayersTurn(). This explicitly cancels their duties rather than
bypassing required actions. Mau Mau transfers the seven group by reading its data,
clearing the current player's offers and creating the next player's group with
two more penalty draws. There is no global penalty counter.

### Global setup actions

The optional `actions` list in CreateGame has maps with the same fields as Action:

```ges
actions: [
    [action: #rules, label: 'Rules', optional: true, finishTurn: false,
        consumable: #never, handler: ShowRules(action), area: #table]
]
```

Supply action, label and handler, and exactly one area, zone or cards target.
The same defaults apply: optional true, finishTurn false and consumable #auto. Global handlers receive `(action)`
or `(action, card)` with no player. They must Complete or Reject as usual.
They are available to any player during the game, retain their activation ID and
consumption counter across turns/rounds, and disappear when consumed or the game ends.
They share the action tag namespace with player actions. Consumption and required
flags have the same meaning; finishTurn ends the active player's turn. Usually
informational global actions use optional: true, finishTurn: false, consumable: #never.
The Mau Mau example includes a global Rules action.

Notice messages are delivered to the browser after the synchronous rule cascade;
a modal blocks browser interaction, not GES execution. Multiple notices are queued
in order and are not erased by Complete. NoticeTable persists across actions and
turns until changed or cleared. It is independent of modal notices.

### Reshuffling an existing deck

```ges
emit SetCardOrder(zone: #draw, cards: (:board.cards(zone: #draw))[:select card => card.id][:shuffle])
```

The command validates an exact permutation before changing anything.

### Recycling in GES

```ges
function recycled() be (:board.cards(zone: #discard))[:filter card where card.id <> (:board.top(zone: #discard)).id][:select card => card.id][:shuffle]

// Within an action handler, before DrawCard:
if (:board.cards(zone: #draw))[:count] = 0 {
    emit MoveCards(source: #discard, destination: #draw, cards: recycled())
}
emit DrawCard(action: action, source: #draw, destination: hand(player: player))
```

MoveCards validates the entire list before mutation: all IDs must be unique and
belong to source, and source/destination must differ. Cards append to destination
in list order; its final card is the top. There is no native DrawCards, Shuffle,
or recycling command. Mau Mau implements penalties as counted required actions. Each human draw
uses DrawCard, preceded by MoveCards with a GES-shuffled list when needed.
Emitted commands execute FIFO; state queries in the same handler still see the
state before those queued commands. Use explicit calculations or continuation
messages when later decisions need the updated state.

## Read-only extensions

Zone parameters and returned zone IDs are Tags. Zone snapshots expose a zero-based
row and a position Tag for both table and player zones.

| Extension | Result |
| --- | --- |
| `:board.cards(zone)` | Card snapshots `[id, properties]`, bottom to top |
| `:board.card(id)` | Card snapshot or nothing |
| `:board.top(zone)` | Top card or nothing |
| `:board.roundnumber()` | Current round, 0 during preparation |
| `:board.current()` | Current player or nothing before selection |
| `:board.players()` | Player IDs in seat order |
| `:board.playercount()` | Selected player count |
| `:board.next(player)` | Next player with current direction and turn override |
| `:board.direction()` | +1 or -1 |
| `:board.ending()` | Whether EndGame has been requested |
| `:board.state(key)` | Host-owned rule value for a Tag key, or nothing |
| `:board.table()` | Table zone snapshots `[id, label, position, row, layout, count]` in creation order |
| `:board.zones(player)` | That player's zone snapshots in creation order |
| `:board.lastaction()` | Last accepted action kind, initially empty text |

Queries return immutable snapshots at evaluation time. Emits are queued: a query
in the same handler cannot observe effects of its not-yet-delivered emits.
Unknown zones and invalid player IDs are errors. Extensions are trusted rule
queries and may inspect other players; filtering happens at the UI boundary.

## Browser adapter

The JSON view exposes player-filtered cards plus placement metadata. It renders
properties as display strings; it is not the lossless GES value codec. This is
local hot-seat play, not secure multiplayer. Action labels appear on buttons; card actions use clickable card faces. A single
card action executes immediately. Multiple actions on the same card open a chooser
at that card; selecting an entry executes only that action. Escape or clicking
outside dismisses the chooser.

Mau Mau retains 32 cards, matching suit/rank, one optional draw followed by playing or passing, 8 skip, and 7
(stackable two-card debt, individually drawn before the normal turn). Jacks
cannot be played on another jack and require a suit choice. An empty hand wins after any required suit choice, before
a final-card penalty. The initial discard has no effect. Empty piles recycle with
the top discard retained. A completely blocked game ends in a draw.

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
