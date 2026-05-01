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
- Type names are written as tags: `:decimal`, `:text`, `:list`, `:message`, `:handler`, `:gauge`.
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

record :gauge as {
    current: :decimal,
    maximum: :decimal
}

rule wounded(_ unit) means unit.hp < unit.maxHp
select woundedUnits(_ units) means units[:filter unit where unit is wounded]

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

Parameters are labeled positional slots. `on DamageTaken(unit, amount)` subscribes to
`DamageTaken(unit,amount)`, so the matching message must use those labels in that order.
Use `_ localName` for an unlabeled slot:

```eventscript
on Point(_ x, _ y) {
    publish PointSeen(x: x, y: y)
}
```

Multiple handlers for the same message may use different ordered signatures.
Dispatch matches by exact `SignatureId`, including label order.

## Publishing Events

Use `publish` to send a message.

```eventscript
publish UnitDied(unit: unit.id)
publish Travel(from: here to: there)
publish Point(10, 20)
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

- `EventScriptManager.Compile(...)` compiles scripts to RegisterVM bytecode.
- The compiled script exposes the `IEventScriptMessageHandlerCollection` host contract.

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

Percentages are ratios, not decimal units. `5%` is the ratio `0.05`.
When a percentage appears on the right side of `+` or `-`, it is relative to the left base value:

```eventscript
100 + 5%  // 105
100 - 5%  // 95
100m + 5% // 105m
```

Percentage arithmetic keeps percentages when the percentage is the subject:

```eventscript
15% + 15% // 30%
15% * 2   // 30%
2 * 15%   // 0.3
```

### Decimal Units

```eventscript
43.9°
90°
360°
-10°
100m
15s
```

Decimal unit literals are regular `:decimal` values with an attached unit. Built-in units are `:degree`, `:meter`, and `:second`. Unit
values preserve their unit in text output. `as :decimal` erases the unit. Unit casts such as `as :meter` or `:meter(value)` apply the
unit to unitless numeric values, keep matching units, and return `NaN` for mismatched units. Use `:degree.wrap` to wrap a unitless
decimal or degree value into the canonical `0°` up to, but not including, `360°` range.

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
- `Shot(unit, target)` is a handler literal in expression position (`Shot` is uppercase, parameter names).
- `Shot(unit: source, target: victim)` is a message literal (`Shot` is uppercase, labeled args).
- `publish Shot(source, victim)` publishes an unlabeled positional message in publish position.
- `Shot(unit + 1)` is invalid in expression position because uppercase positional forms define handler parameters.
- Prefix literal forms are removed: `:handler Shot(...)` and `:message Shot(...)` are not valid.

Argument labels are positional, not reorderable. `Travel(from: a, to: b)` and
`Travel(to: b, from: a)` have different signatures. A labeled parameter must be
called with the same label at the same position; an `_` parameter must be called
without a label.

Naming rules are strict:

- variable-style names are lowercase identifiers (`let`, parameters, loop vars, selector bind names, rule/select names, call targets)
- message/handler names are uppercase message lexemes

## Built-In Types

Built-in type tags:

- `:tag`
- `:text`
- `:percentage`
- `:degree`
- `:meter`
- `:second`
- `:vector2`
- `:vector3`
- `:decimal`
- `:integer`
- `:boolean`
- `:optional`
- `:range`
- `:message`
- `:handler`
- `:sequence`
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
let distance as :meter be 100
let duration as :second be 15
let position as :vector2 be [x: 10, y: 20]
let point as :vector3 be [x: 10, y: 20, z: 5]
let amount as :decimal be '12.5'
let flags as :list be 'abc'
```

The same conversion can be written inline with `:type(value)`.

```eventscript
let scaled be :decimal(90°) * :decimal(100m)
let heading be :degree(180)
let distance be :meter(100)
let duration be :second(15)
let rawMove be :decimal(:vector2(10m, 20m))
let meterMove be :meter(rawMove)
```

`vector2` exposes `x` and `y`; `vector3` exposes `x`, `y`, and `z`.
Both can be cast from dictionaries with matching component names or from lists in component order.

Vector constructors use component arguments:

```eventscript
let position be :vector2(10, 20)
let labeledPosition be :vector2(x: 10, y: 20)
let yOnly be :vector2(y: 20m)
let point be :vector3(10, 20, 5)
let labeledPoint be :vector3(x: 10, y: 20, z: 5)
let zOnly be :vector3(z: 5m)
let offset be :vector2(3m, 4m)
let lifted be :vector3(position)
let liftedWithHeight be :vector3(position, 5)
let flattened be :vector2(point)
```

Labels must follow component order, but labeled vector constructors may omit components.
Missing components default to `0` in the common unit of the provided components.
For example, `:vector2(y: 20m)` is `vector2[x: 0m, y: 20m]`.
`:vector2(y: 20, x: 10)` is invalid because the labels are out of order.
Single-argument vector constructors are conversions: `:vector3(vector2)` adds `z: 0`,
and `:vector2(vector3)` drops `z`.
`:vector3(vector2, z)` lifts a 2D vector with an explicit z component.

Vectors may have one shared decimal unit. Component access, `as :list`, and `as :dictionary`
preserve that unit:

```eventscript
let offset be :vector2(3m, 4m)
offset.x // 3m
:decimal(offset) // vector2[x: 3, y: 4]
:meter(:decimal(offset)) // vector2[x: 3m, y: 4m]
```

Mixed component units such as `:vector2(3m, 4s)` or `:vector2(3, 4m)` evaluate to `NaN`.

Custom record constructors are labeled-only:

```eventscript
record :gauge as {
    current: :decimal clamped between 0 and maximum,
    maximum: :decimal clamped between 0 and :infinity,
    percentage: :percentage computed by
        0% when maximum <= 0,
        otherwise (current / maximum) as :percentage
}

let hp be :gauge(current: 125, maximum: 100)
```

Custom constructors use the same clamp and computed-field semantics as typed record
materialization. Positional custom construction such as `:gauge(10, 20)` is invalid.

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
a div b
a mod b
a rem b
```

`/` is numeric division. `div` is floor division. `mod` is mathematical modulo, and `rem` is the truncating remainder.
Percentage values are written as literals such as `10%`.

Vectors support basic vector arithmetic:

```eventscript
:vector2(1m, 2m) + :vector2(3m, 4m) // vector2[x: 4m, y: 6m]
:vector2(1m, 2m) - :vector2(3m, 4m) // vector2[x: -2m, y: -2m]
-:vector2(1m, 2m)                   // vector2[x: -1m, y: -2m]
:vector2(1m, 2m) * 2                // vector2[x: 2m, y: 4m]
2 * :vector2(1m, 2m)                // vector2[x: 2m, y: 4m]
:vector2(3m, 4m) / 2                // vector2[x: 1.5m, y: 2m]
:abs :vector2(3m, 4m)               // 5m
```

Vector addition and subtraction require the same dimension and the same shared unit.
`vector * vector`, `scalar / vector`, vector `div`/`mod`/`rem`, mixed dimensions, and
incompatible scalar units evaluate to `NaN`. Dot/cross/normalize are intentionally left
for explicit helpers or extensions.

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

EventScript has five main collection-like data shapes:

- `:list`
- `:dictionary`
- `:set`
- `:dice`
- `:sequence`

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

### Sequence literals

Use `of ... and ...` for a repeatable sequence value:

```eventscript
let values be of 10 and 20 and 30
let materialized as :list be of 10 and 20 and 30
```

Sequences are useful for readable argument-like value lists and can be materialized
with `as :list` when a concrete list value is needed.

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
:set[:select item from 1 to 4 where item >= 2 -> item mod 2]
:set[:select item in values where item >= 2 -> item mod 2]
```

Optional parts:

- `step`
- `where`

Example:

```eventscript
let evens be :list[:select item from 1 to 10 step 2 -> item]
let doubled be :list[:select item in values -> item * 2]
let filtered be :list[:select item from 1 to 6 where item mod 2 = 0 -> item * item]
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
rule wounded(_ unit) means unit.hp < unit.maxHp
rule intersects(first, second) means first.id <> second.id
rule defeated(unit) means unit.hp is 0 or less
```

Use rules in two ways:

### Call syntax

```eventscript
wounded(unit)
intersects(first: source, second: target)
```

### Predicate syntax for single-parameter rules

```eventscript
unit is wounded
```

The `is ruleName` form only works for rules with exactly one parameter.
It binds the tested value to the first parameter, whether that parameter is labeled
or `_`.

## Select Definitions

Select definitions define reusable expressions.

```eventscript
select woundedUnits(_ units) means units[:filter unit where unit is wounded]
select unitsById(_ units) means units[:dictionary unit by unit.id]
select travelTime(from, to) means from.distanceTo / to.speed
```

Use them with normal call syntax:

```eventscript
let choices be woundedUnits(units)
let byId be unitsById(units)
let eta be travelTime(from: start, to: destination)
```

## Records

Records define closed custom types.

```eventscript
record :gauge as {
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
record :gauge as {
    current: :decimal clamped between 0 and maximum,
    maximum: :decimal clamped between 0 and :infinity,
    percentage: :percentage computed by
        0% when maximum <= 0,
        otherwise (current / maximum) as :percentage
}
```

Usage:

```eventscript
let hp as :gauge be [current: 25, maximum: 100]
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
```

Negative values can be written directly with unary minus, such as `-12` or `-12.34`.

## Required Standard Extensions

The standard extension namespace is parsed like any other extension call, but these functions are intrinsic and do not require a host registry:

- `:integer.floor value`
- `:integer.ceil value`
- `:integer.truncate value`
- `:integer.halfEven value`
- `:integer.halfUp value`
- `:integer.halfDown value`
- `:degree.wrap value`
- `:degree.toRadians value`
- `:degree.fromRadians value`

Examples:

```eventscript
let roundedDown be :integer.floor 12.9
let roundedUp be :integer.ceil(12.1)
let heading be :degree.wrap -10°
let radians be :degree.toRadians 180°
let degrees be :degree.fromRadians 3.1415926535897932384626433833
```

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

Use `in` for collection or sequence expressions.
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

## Extensions

Host-provided functions use `:extension.function` syntax and are dynamically bound
when a RegisterVM script is loaded into an `EventScriptHost`.

```eventscript
let floored be :math.floor value
let best be :math.max of a and b and c
let turn be :nav.shortestTurn from: current to: target
let sameTurn be :nav.shortestTurn(from: current, to: target)

if heading is :nav.isNorth {
    publish FacingNorth
}
```

Extension arguments use the same labeled positional rules as rules, selects, and
messages. The `of ... and ...` form is for repeatable unlabeled argument lists.
Missing extension bindings are dynamic-link errors when the host loads a
RegisterVM script.

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
- wrong rule/select argument labels
- using `x is ruleName` with a non-rule or with a rule that does not have exactly one parameter

## Current Limitations

At the current language stage:

- line comments use `//`
- there are no user-defined mutable variables
- host functions must be exposed through `:extension.function`
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
record :gauge as {
    current: :decimal clamped between 0 and maximum,
    maximum: :decimal clamped between 0 and :infinity,
    percentage: :percentage computed by
        0% when maximum <= 0,
        otherwise (current / maximum) as :percentage
}

on Start {
    let mana as :gauge be [current: 30, maximum: 50]
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
