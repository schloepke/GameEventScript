# EventScript Language Guide

## Overview

EventScript is a small, domain-oriented scripting language for event-driven game logic.

It is designed around a few core ideas:

- Scripts react to messages with `on Message { ... }`.
- Scripts publish new messages with `publish Message(name: value, ...)`.
- The runtime uses a FIFO pub/sub model.
- The language is intentionally lenient:
  missing data often becomes `nothing` instead of throwing.
- Dictionaries and immutable collections are first-class.
- The syntax favors readable, domain-like expressions over technical ceremony.

EventScript is case-sensitive.

## Design Principles

- Keywords are lowercase: `module`, `on`, `publish`, `let`, `if`, `for`, `rule`, `select`.
- Messages start with an uppercase letter: `Start`, `DamageTaken`, `TurnEnded`.
- Local variables and identifiers start with a lowercase letter: `hp`, `target`, `woundedUnits`.
- Type names are written as tags: `:decimal`, `:text`, `:list`, `:message`, `:handler`, `:meter`.
- Tags are also first-class values: `:name`, `:boss`, `:fire`.
- Collection mini-language lives inside `[...]`.
- Many failures are represented as `nothing` rather than exceptions.

## Hello World

```eventscript
on Start {
    publish Hello(arg1: 'world')
}
```

## Top-Level Structure

An EventScript can contain:

- an optional `module` declaration at the top
- `record` definitions for custom types
- `rule` definitions for reusable predicates
- `select` definitions for reusable expressions
- event handlers with `on`

Example:

```eventscript
module CombatRules

record :meter as {
    current: :decimal,
    maximum: :decimal
}

rule wounded(unit) means unit.hp < unit.maxHp
select woundedUnits(units) means units[:filter unit where unit is wounded]

on Start(unit, units) {
    if unit is wounded {
        publish HealRequested(arg1: unit)
    }

    let choices be woundedUnits(units)
    publish Done(arg1: :len choices)
}
```

## Module Declaration

Use `module Name` to declare the module name inside the script.

```eventscript
module CombatRules

on Start {
    publish Done
}
```

Rules:

- `module` is optional
- if omitted, the parser generates an anonymous module name
- the declaration must appear at the top level before handlers and definitions
- module names can use either identifier style or message style names

Examples:

```eventscript
module combatRules
module CombatRules
```

## Event Handlers

Event handlers subscribe to a message.

```eventscript
on DamageTaken(unit, amount) {
    let remainingHp be unit.hp - amount
    publish HpChanged(arg1: unit.id, arg2: remainingHp)
}
```

Handlers:

- are matched by message name
- may declare zero or more parameters
- may have multiple handlers for the same message
- run in declaration order

Multiple handlers for the same message may use different parameter-name sets.
Dispatch matches by exact argument names.

## Publishing Events

Use `publish` to send a message.

```eventscript
publish UnitDied(unit: unit.id)
publish TurnEnded
```

Publishing is lenient:

- publishing an unknown message is allowed
- publishing a message with no subscribers is a valid no-op
- the event is still considered published

## Runtime Model

The runtime uses FIFO pub/sub semantics.

When an event is published:

1. it is appended to the FIFO queue
2. it is processed by the queue drain loop
3. follow-up publishes are appended to the same active run

For each queued event:

1. all subscribers with the exact `SignatureId` are matched
2. matched subscribers run by priority
3. subscribers with the same priority run in registration order

This means publish chains are not recursive direct calls. They are queued message deliveries.
There is no fixed script-before-external rule; host priority controls the order.

## Public Runtime API

EventScript runtime is message/context driven and resultless.

Direct compiled invocation:

```csharp
var compiled = EventScriptManager.Compile(script);
var published = new List<EventScriptMessage>();

var context = new EventScriptContext(
    EventScriptRandomGenerator.Create(),
    message => published.Add(message));

compiled.Invoke(
    EventScriptMessage.Message("Start", ("value", EventScriptValue.Integer(3))),
    context);
```

Host orchestration (queue + subscriptions):

```csharp
var host = EventScriptHost.CreateBuilder()
    .WithRandom(EventScriptRandomGenerator.Create())
    .Build()
    .Load(compiled);

host.Publish(EventScriptMessage.Message("Start", ("value", EventScriptValue.Integer(3))));
```

Compilation paths:

- The classic runtime is an optimized AST runtime.
- The experimental runtime currently compiles statements to opcodes and keeps expressions AST-backed.
- Both expose the same `IEventScriptMessageHandlerCollection` host contract.

## Comments

EventScript supports line comments with `//`.

Comments may appear on their own line or at the end of a line.

```eventscript
// Runs once at startup
on Start {
    let hp be 10 // base hit points
    publish Done
}
```

Rules:

- comments run from `//` to the end of the line
- block comments are not supported
- the line break after a comment still acts as a normal statement separator

`#...` directives are not part of the language syntax at the moment.
They are reserved for possible future pragma-style preprocessing, but are not currently supported.

## Variables and `let`

Variables are introduced with `let`.

```eventscript
let hp be 10
let name be 'Ada'
let alive be true
```

Optional explicit typing:

```eventscript
let hp as :decimal be 10
let name as :text be 'Ada'
let tags as :set be :set[:select item from 1 to 3 -> item]
```

Blocks create a child variable scope. Values declared inside a block do not leak outside that block.
This applies to blocks used by `if`, `else`, `for`, and `:random with ... { ... }`.

Single-statement control flow forms do not introduce an extra block scope on their own.

Typed `let` uses conversion semantics where possible.

## Guarded Assignment

`let` can use guarded choices.

```eventscript
let score be 12 when x = 10,
    or 20 when x = 15,
    otherwise 5
```

This is an expression form, not a statement-only special case.

## Primitive Literals

### Decimal numbers

```eventscript
12
12.5
0.75
```

### Percentages

```eventscript
25%
75%
0%
```

### Degrees

```eventscript
43.9°
90°
360°
-10°
```

Degree values use the built-in `:degree` type. A degree is an open angle value, so negative values and values greater than `360°` are preserved. Use `:wrapDegree` to wrap an angle into the canonical `0°` up to, but not including, `360°` range.

### Text

```eventscript
'hello'
'Ada''s turn'
```

Single quotes are escaped by doubling them.

### Booleans

```eventscript
true
false
```

### Tags

```eventscript
:name
:boss
:fire
```

Tags are values. They are not strings, although they can often be converted to text as needed.

## Messages, Identifiers, and Case

Examples:

```eventscript
on Start {
    let playerId be 10
    publish TurnStarted(arg1: playerId)
}
```

- `Start` and `TurnStarted` are messages
- `playerId` is a local identifier

The language is not case-insensitive.

Call/message disambiguation is case-based:

- `wounded(unit)` is a `rule/select` call (`wounded` is lowercase).
- `Shot(unit, target)` is a handler literal (`Shot` is uppercase, positional identifiers).
- `Shot(unit: source, target: victim)` is a message literal (`Shot` is uppercase, named args).
- `Shot(unit + 1)` is invalid (uppercase positional arguments must be identifier names).
- Prefix literal forms are removed: `:handler Shot(...)` and `:message Shot(...)` are not valid.

Naming rules are strict:

- variable-style names are lowercase identifiers (`let`, parameters, loop vars, selector bind names, rule/select names, call targets)
- message/handler names are uppercase message lexemes

## Built-In Types

Built-in type tags:

- `:tag`
- `:text`
- `:percentage`
- `:degree`
- `:vector2`
- `:vector3`
- `:decimal`
- `:integer`
- `:boolean`
- `:optional`
- `:range`
- `:message`
- `:handler`
- `:list`
- `:dictionary`
- `:set`
- `:dice`
- `:nothing`

`message` and `handler` are first-class built-in types.
They expose read-only members (`name`, `signatureid`, plus `arguments`/`parameters`) via both `.` and `[:]`,
but they are not treated as `:dictionary` for type checks.

## Type Checks and Type Casts

### Type checks

```eventscript
if value is :decimal {
    publish Numeric
}
```

### Type casts

```eventscript
let ratio as :percentage be 75
let heading as :degree be 450
let position as :vector2 be [x: 10, y: 20]
let point as :vector3 be [x: 10, y: 20, z: 5]
let amount as :decimal be '12.5'
let flags as :list be 'abc'
```

`vector2` exposes `x` and `y`; `vector3` exposes `x`, `y`, and `z`.
Both can be cast from dictionaries with matching component names or from lists in component order.

## Domain-Style Boolean Phrases

The language supports readable boolean phrases.

```eventscript
if hp is 0 or less { ... }
if mana is at least 3 { ... }
if hand is empty { ... }
if target has value { ... }
```

These map to existing runtime semantics:

- `x is 0 or less` -> `x <= 0`
- `x is at least 3` -> `x >= 3`
- `x is empty` -> `empty x`
- `x has value` -> `has value x`

Related forms:

```eventscript
if hp is at most 10 { ... }
if value is 5 or more { ... }
if value is 5 or greater { ... }
```

## Operators

### Equality

```eventscript
x = y
x <> y
```

### Logical operators

```eventscript
a and b
a & b
a xor b
a ^ b
a or b
a | b
not a
!a
~a
```

### Comparison

```eventscript
x < y
x <= y
x > y
x >= y
```

### Arithmetic

```eventscript
a + b
a - b
a * b
a / b
a % b
```

`%` is the modulo operator. Percentage values are written as literals such as `10%`.

### Collection combination

```eventscript
a :intersect b
a :combine b
a :merge b
a :except b
a :zip b
```

Semantics depend on the operand kinds.

## `nothing` and Lenient Evaluation

`nothing` is the language's absence value.

Typical sources of `nothing`:

- unknown identifier
- missing dictionary key
- out-of-range list access
- unsupported operator for a type
- missing optional value in some operations

Examples:

```eventscript
let missing be unit['unknown']
let alsoMissing be values[99]
let unknownVariable be doesNotExist
```

These do not throw script exceptions. They become `nothing`.

This leniency is intentional.

## Defaulting

Use `:default` to provide a fallback.

```eventscript
let hp be unit.hp :default 0
let name be entry['name'] :default 'unknown'
let hand be maybeHand :default [1, 2, 3]
```

`x :default y` uses `y` when `x` has no value.

This includes:

- `nothing`
- empty text
- empty list
- empty dictionary
- empty set
- empty dice
- `optional none`
- `NaN`
- infinity

## Presence and Emptiness

Use:

```eventscript
has value x
empty x
```

or their domain-style equivalents:

```eventscript
x has value
x is empty
```

Behavior:

- non-empty text/list/dictionary/set/dice -> has value
- empty containers -> no value
- `nothing` -> no value
- `optional none` -> no value
- `NaN` and infinity -> no value

## Membership and Boundary Checks

```eventscript
'name' in entry
'Ada' value in entry
'ab' in 'cabin'
[1, 2] starts with [1]
[1, 2, 3] ends with [2, 3]
```

Supported operators:

- `in`
- `value in`
- `starts with`
- `ends with`

For dictionaries:

- `x in dict` checks keys
- `x value in dict` checks values

## Collections

EventScript has four main collection-like data shapes:

- `:list`
- `:dictionary`
- `:set`
- `:dice`

### List literals

```eventscript
[1, 2, 3]
['a', 'b', 'c']
[]
```

### Dictionary literals

```eventscript
[name: 'Ada', age: 25]
[:]
```

### Set literals

```eventscript
:set[1, 2, 2, 3]
```

Sets deduplicate values and are sorted with the stable EventScript value order.

### Dice

```eventscript
:dice 4d6
```

Dice are ordered descending by value.

## Indexing and Lookup

### One-based sequential indexing

Lists and dice use one-based indexing:

```eventscript
let values be [10, 20, 30]
let first be values[1]
let third be values[3]
```

Out of range returns `nothing`:

```eventscript
values[0]
values[99]
```

### Dictionary lookup

```eventscript
let entry be [name: 'Ada', age: 25]

let a be entry['name']
let b be entry[:name]
let key be :name
let c be entry[key]
let d be entry.name
```

Missing keys return `nothing`.

## Collection Mini-Language

The `[...]` syntax after a value is also used for collection selectors.

Example:

```eventscript
units[:filter unit where unit.hp > 0]
```

This is distinct from plain indexed lookup:

```eventscript
values[1]
```

## Collection Predicates

### `:any` and `:all`

```eventscript
units[:any unit where unit.hp <= 0]
units[:all unit where unit.alive]
```

### `:count`

```eventscript
units[:count unit where unit.hp < unit.maxHp]
```

## Filtering, Projection, and Dictionary Building

### Filter

```eventscript
units[:filter unit where unit.alive]
```

### Select

```eventscript
units[:select unit -> unit.name]
```

### Dictionary projection

```eventscript
units[:dictionary unit by unit.id]
units[:dictionary unit by unit.id -> unit.name]
```

If duplicate keys occur, the last value wins.

## First, Last, and Single

```eventscript
units[:first]
units[:last]
units[:single]
```

With predicate:

```eventscript
units[:first unit where unit.alive]
units[:last unit where unit.alive]
units[:single unit where unit.role = :boss]
```

`[:single ...]` returns `nothing` unless exactly one item matches.

## Sorting and Ordering

### Stable value sort

```eventscript
items[:sort ascending]
items[:sort descending]
```

### Projection-based order

```eventscript
units[:order by unit -> unit.initiative descending]
items[:order by item -> item.name ascending]
```

## Distinct and Grouping

### Distinct

```eventscript
items[:distinct]
items[:distinct by item -> item.id]
```

### Group by

```eventscript
units[:group by unit -> unit.team]
```

The result is a dictionary where each group key maps to a list of matching items.

## Sum, Average, Min, Max, Highest, Lowest

```eventscript
units[:sum unit -> unit.hp]
units[:average unit -> unit.hp]
units[:min unit -> unit.hp]
units[:max unit -> unit.hp]
units[:highest unit -> unit.hp]
units[:lowest unit -> unit.hp]
```

Notes:

- `:sum` and `:average` compute numeric results
- `:min`, `:max`, `:highest`, and `:lowest` return the original item, not the projected value
- empty collections usually return `nothing`

## Contains

```eventscript
items[:contains 2]
items[:contains all [1, 2]]
items[:contains any [5, 9]]
```

For text:

```eventscript
'battle'[:contains 'tt']
'battle'[:contains all ['ba', 'tt']]
'battle'[:contains any ['xx', 'tt']]
```

For dictionaries, `:contains` checks keys.

## Sequence Operations

### Reverse

```eventscript
items[:reverse]
```

Supported for ordered collections:

- `list`
- `dice`

For `dice`, reversing returns a list.

### Shuffle

```eventscript
deck[:shuffle]
```

Supported for:

- `list`
- `dice`

Not supported for sets and dictionaries.

### Draw

```eventscript
deck[:draw 3]
```

Because EventScript is immutable, `:draw` returns the drawn value(s) but does not mutate the original source.

Typical usage:

```eventscript
let cards be [1, 2, 3, 4, 5][:shuffle]
let hand be cards[:draw 3]
let restCards be cards[:drop first 3]
```

### Take and Drop

```eventscript
items[:take first 3]
items[:take last 2]
items[:take highest 2]
items[:take lowest 2]

items[:drop first 3]
items[:drop last 2]
items[:drop highest 1]
items[:drop lowest 1]
```

## Dice and Pattern Matching

Patterns are written with `:has` and `:take`.

```eventscript
roll[:has pair]
roll[:has pair of 6]
roll[:has three of a kind]
roll[:has three of 6]
roll[:has full house]
roll[:has straight]
```

Supported count patterns:

- `pair`
- `three`
- `four`
- `five`
- `six`
- `seven`

Extraction:

```eventscript
roll[:take pair]
roll[:take full house]
cards[:take straight]
```

For straight checks, duplicates are ignored.

## Object and Dictionary Matching

Collections can match dictionary-shaped items with subset semantics.

```eventscript
units[:has [faction: 'orc', alive: true]]
units[:has [owner: [team: 'red']]]
```

Rules:

- extra keys in the actual object are allowed
- all specified keys must exist
- nested matches are recursive
- non-dictionary items do not match

## Generated Collections

EventScript can generate lists and sets from ranges or other iterable sources.

### Generated list

```eventscript
:list[:select item from 1 to 5 -> item * item]
:list[:select item in values -> item * item]
```

### Generated set

```eventscript
:set[:select item from 1 to 4 where item >= 2 -> item % 2]
:set[:select item in values where item >= 2 -> item % 2]
```

Optional parts:

- `step`
- `where`

Example:

```eventscript
let evens be :list[:select item from 1 to 10 step 2 -> item]
let doubled be :list[:select item in values -> item * 2]
let filtered be :list[:select item from 1 to 6 where item % 2 = 0 -> item * item]
```

If the step direction does not reach the target range, the result is empty.

## Ranges

Ranges are reusable iterable values.

```eventscript
let odds as :range be from 1 to 9 step 2
for item in odds publish Seen(value: item)
```

You can also iterate a range directly in `for`:

```eventscript
for item from 1 to 9 step 2 publish Seen(value: item)
```

## Rules

Rules define reusable predicates.

```eventscript
rule wounded(unit) means unit.hp < unit.maxHp
rule defeated(unit) means unit.hp is 0 or less
```

Use rules in two ways:

### Call syntax

```eventscript
wounded(unit)
```

### Predicate syntax for single-parameter rules

```eventscript
unit is wounded
```

The `is ruleName` form only works for rules with exactly one parameter.

## Select Definitions

Select definitions define reusable expressions.

```eventscript
select woundedUnits(units) means units[:filter unit where unit is wounded]
select unitsById(units) means units[:dictionary unit by unit.id]
```

Use them with normal call syntax:

```eventscript
let choices be woundedUnits(units)
let byId be unitsById(units)
```

## Records

Records define closed custom types.

```eventscript
record :meter as {
    current: :decimal,
    maximum: :decimal
}
```

Records may use:

- typed fields
- field clamping
- computed fields

Example:

```eventscript
record :meter as {
    current: :decimal clamped between 0 and maximum,
    maximum: :decimal clamped between 0 and :infinity,
    percentage: :percentage computed by
        0% when maximum <= 0,
        otherwise (current / maximum) as :percentage
}
```

Usage:

```eventscript
let hp as :meter be [current: 25, maximum: 100]
let ratio be hp.percentage
```

Custom records expose dictionary-like lookup behavior for their defined fields.

## Prefix Value Operators

Current prefix tag operators:

- `:len`
- `:chance`
- `:keys`
- `:values`
- `:entries`
- `:abs`
- `:floor`
- `:ceil`
- `:round`
- `:rounddown`
- `:roundup`
- `:roundeven`
- `:wrapDegree`

Examples:

```eventscript
let count be :len items
let hit be :chance 25%
let keys be :keys entry
let values be :values items
let entries be :entries entry
let penalty be -12
let debt be -12.5
let distance be :abs -5
let roundedDown be :floor 12.9
let heading be :wrapDegree -10°
```

Negative values can be written directly with unary minus, such as `-12` or `-12.34`.

## Keys, Values, and Entries

### `:keys`

```eventscript
for key in :keys entry {
    publish Seen(arg1: key)
}
```

### `:values`

```eventscript
for value in :values items {
    publish Seen(arg1: value)
}
```

### `:entries`

```eventscript
for item in :entries entry {
    publish Pair(arg1: item.key, arg2: item.value)
}
```

Entry values behave like dictionary-like objects with `key` and `value`.

## Randomness

### Integer range

```eventscript
:random from 1 to 6
:random from 0.0 to 1.0
```

If both bounds are integers, the result is an integer.
If either bound is decimal, the result is a decimal.

### Seeded random scope

Use `:random with ...` when you need a deterministic local random sequence derived from a seed value.

Expression form:

```eventscript
let values be :random with gameSeed :list[:select item from 1 to 3 -> :random from 1 to 6]
```

Statement form:

```eventscript
:random with gameSeed {
    let roll be :random from 1 to 6
    publish Rolled(value: roll)
}
```

Inside the seeded scope, all `:random ...` evaluations use the local deterministic generator.
Outside the scope, the outer random context is unchanged.

The expression form returns the value of its body expression.
The block form is statement-only and does not produce a value.

### Dice

```eventscript
:dice 4d6
```

### Chance

```eventscript
:chance 25%
```

### Random choice

```eventscript
units[:choose 1 at random]
units[:choose 1 at random unit where unit.alive]
units[:choose 1 weighted by unit -> unit.weight]
```

## Combining Collections

### Lists

```eventscript
[1, 2, 3] + 4
[1, 2, 3] + [4, 5]
```

### Dictionaries

```eventscript
[name: 'Mark', age: 32] + [city: 'Somewhere']
[name: 'Mark', age: 32] :merge [age: 33]
```

For dictionaries, overlapping keys are overwritten by the right-hand side.

### Sets

```eventscript
:set[1, 2] :merge :set[2, 3]
:set[1, 2, 3] :intersect :set[2, 4]
:set[1, 2, 3] :except :set[2]
```

## Flow Control

### `if`

```eventscript
if unit is wounded {
    publish HealRequested(arg1: unit)
} else {
    publish Continue
}
```

Single-statement forms are also valid:

```eventscript
if unit is wounded publish HealRequested(arg1: unit)
else publish Continue
```

This also enables `else if` chains:

```eventscript
if first publish One
else if second publish Two
else publish Three
```

### `for`

```eventscript
for unit in units {
    if unit.alive {
        publish UnitReady(arg1: unit.id)
    }
}
```

Single-statement loops are also valid:

```eventscript
for unit in units publish UnitReady(arg1: unit.id)
for index from 1 to 5 publish Tick(value: index)
```

Use `in` for collection or iterator expressions.
Use `from ... to ... [step ...]` for direct range loops.

`for unit in from 1 to 5 ...` is not valid.

`if` and `for` are control-flow statements, not expressions.

## Truthiness

Many conditions are evaluated through boolean conversion.

Examples:

- `true` is true
- `false` is false
- `nothing` is false
- empty values often become false in meaningful contexts

For explicit intent, prefer readable forms such as:

- `has value x`
- `empty x`
- `x is 0 or less`

## External Bindings

The runtime can attach external bindings to messages through the host API.

Important behavior:

- external bindings are subscribers, like script handlers
- they run by priority and registration order, just like script handlers
- exceptions from external bindings are swallowed
- multiple bindings per message are allowed

This makes external integration compatible with the same pub/sub message model.

## Event Queue Limits

The runtime prevents infinite event loops with a per-run processed event limit.

The relevant host-side setting is:

- `MaxProcessedEventsPerRun`

If the limit is reached, processing stops silently.

No script exception is thrown.

## Common Lenient Behaviors

The following are intentionally allowed:

- unknown published messages
- events without listeners
- missing dictionary keys
- out-of-range list access
- rules that evaluate through missing data and yield `nothing`
- external binding exceptions

Examples:

```eventscript
let missingName be unit['name']
let missingItem be values[99]
publish UnknownMessage(arg1: 1, arg2: 2, arg3: 3)
```

## Compilation Errors

Some things fail at compile time instead of returning `nothing`.

Examples:

- duplicate rule names
- duplicate select names
- a name defined as both rule and select
- unknown called rule/select
- wrong rule/select arity
- using `x is ruleName` with a non-rule or with a rule that does not have exactly one parameter

## Current Limitations

At the current language stage:

- line comments use `//`
- there are no user-defined mutable variables
- there are no traditional functions beyond `rule` and `select`
- there is no direct mutation of collections or dictionaries

## Practical Examples

### Example: Damage and defeat

```eventscript
rule defeated(unit) means unit.hp is 0 or less

on DamageTaken(unit, amount) {
    let hp be unit.hp - amount

    if hp is 0 or less {
        publish UnitDefeated(arg1: unit.id)
    } else {
        publish UnitHpChanged(arg1: unit.id, arg2: hp)
    }
}
```

### Example: Filtering candidates

```eventscript
rule targetable(unit) means unit.alive and not unit.hidden
select targetableUnits(units) means units[:filter unit where unit is targetable]

on ChooseTarget(units) {
    let candidates be targetableUnits(units)
    let target be candidates[:choose 1 at random]
    publish TargetChosen(arg1: target.id)
}
```

### Example: Building a lookup dictionary

```eventscript
select unitsById(units) means units[:dictionary unit by unit.id]

on Start(units) {
    let byId be unitsById(units)
    let hero be byId[:hero]
    publish Ready(arg1: hero.name :default 'Unknown')
}
```

### Example: Dice logic

```eventscript
on RollAttack {
    let roll be :dice 4d6
    let crit be roll[:has pair of 6]
    let total be roll[:sum die -> die]

    if crit {
        publish CriticalHit(arg1: total)
    } else {
        publish NormalHit(arg1: total)
    }
}
```

### Example: Custom record type

```eventscript
record :meter as {
    current: :decimal clamped between 0 and maximum,
    maximum: :decimal clamped between 0 and :infinity,
    percentage: :percentage computed by
        0% when maximum <= 0,
        otherwise (current / maximum) as :percentage
}

on Start {
    let mana as :meter be [current: 30, maximum: 50]
    if mana.percentage >= 50% {
        publish ReadyToCast
    }
}
```

## Summary

EventScript is a readable, immutable, lenient, event-driven scripting language with:

- FIFO pub/sub execution
- dictionary-first data modeling
- strong collection tooling
- reusable predicates with `rule`
- reusable expressions with `select`
- custom structured types with `record`
- graceful `nothing`-based failure semantics

For the exact grammar, see [EventScript.bnf](./EventScript.bnf).
