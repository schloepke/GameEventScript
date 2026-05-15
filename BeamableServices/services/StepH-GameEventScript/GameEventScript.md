# GameEventScript Language Guide

GameEventScript is a compact scripting language for event-driven game logic. It is
designed for predicates that react to messages, inspect immutable data, publish new
messages, and delegate specialized calculations to host-provided extensions.

This guide describes the current language as compiled to GameEventScript
bytecode and executed by the host's BytecodeVM runtime.

## Contents

- [Overview](#overview)
- [Program Structure](#program-structure)
- [Runtime Semantics](#runtime-semantics)
- [Language Semantics](#language-semantics)
- [Types and Conversions](#types-and-conversions)
- [Collection Language](#collection-language)
- [Extensions](#extensions)
- [Host API](#host-api)
- [Errors and Limits](#errors-and-limits)
- [Practical Examples](#practical-examples)

## Overview

At its core, a GameEventScript is a set of message handlers. A handler subscribes
to one message shape, reads the message arguments, and may publish follow-up
messages.

```eventscript
on Start(playerName) {
    publish Greeting(text: 'Hello ' + playerName)
}
```

The runtime is intentionally message-oriented and resultless. A handler does not
return a value to its caller. Observable behavior flows through `publish`.

The language favors readable domain expressions:

```eventscript
predicate wounded(_ unit) means unit.hp < unit.maxHp
function woundedUnits(_ units) means units[:filter unit where unit is wounded]

on BeginTurn(units) {
    let candidates be woundedUnits(units)

    if candidates is empty {
        publish NoHealingNeeded
    } else {
        publish HealRequested(unit: candidates[:first])
    }
}
```

Important design choices:

- GameEventScript is case-sensitive.
- Values are immutable.
- Missing data is usually represented as `nothing`, not as an exception.
- Numeric invalidity is usually represented as double `NaN`.
- Messages, handlers, collections, records, vectors, points, dice, ranges, and tags are
  first-class values.
- Runtime behavior is defined by the JSON conformance tests and executed through
  the host runtime.

## Program Structure

A GameEventScript file may contain a module declaration, custom record types,
predicates, functions, and event handlers.

```eventscript
module Combat

record :gauge as {
    current: :float clamped between 0 and maximum,
    maximum: :float clamped between 0 and :infinity,
    percentage: :percentage computed by
        0% when maximum <= 0,
        otherwise (current / maximum) as :percentage
}

predicate defeated(_ unit) means unit.hp is 0 or less
function livingUnits(_ units) means units[:filter unit where not (unit is defeated)]

on DamageTaken(unit, amount) {
    let hp as :gauge be [current: unit.hp - amount, maximum: unit.maxHp]
    publish HpChanged(unit: unit.id, hp: hp)
}
```

### Comments and separators

Line comments start with `//` and continue to the end of the line.

```eventscript
// This handler runs when combat starts.
on Start {
    let round be 1 // inline comments are allowed
    publish RoundStarted(value: round)
}
```

Statements are separated by newlines or semicolons. Block comments are not part
of the language.

```eventscript
on Start {
    let a be 1; let b be 2
    publish Done(value: a + b)
}
```

### Naming

GameEventScript uses casing to keep the grammar readable.

- Keywords are lowercase: `module`, `record`, `predicate`, `function`, `on`, `let`.
- Local names are lowercase identifiers with letters only: `unit`, `target`,
  `currentHp`.
- Predicate and function names are lowercase identifiers: `wounded`, `bestTarget`.
- Identifiers may use one explicit numeric suffix at the end: `player_0`,
  `player_1`, `player_22`. Forms like `player22`, `player_01`, and
  `player_1a` are invalid.
- Message and handler names start uppercase and use letters only: `Start`,
  `DamageTaken`, `Done`.
- `undeliverable` is a reserved lowercase system endpoint name for fallback
  dispatch.
- Type names are tags: `:float`, `:vector`, `:gauge`.
- Tags are also values and use letters only: `:boss`, `:ready`, `:fire`.

This casing matters. `Start` and `start` are different tokens with different
roles.

### Modules

A file may start with `module Name`.

```eventscript
module CombatRules

on Start {
    publish Ready
}
```

The module declaration is optional. If it is omitted, the parser creates an
anonymous module name. Module names may use either lowercase identifier style or
uppercase message style.

Multiple scripts can be built together. Predicates, functions, record types, and
handlers from all sources are merged into one runtime module.

### Top-level declarations

Top-level declarations are:

```eventscript
record :typeName as { ... }
predicate name(parameters) means expression
function name(parameters) means expression
on Message(parameters) { statements }
on Message as envelope { statements }
```

Predicates and functions are callable definitions. Records define custom value shapes.
Handlers subscribe to messages.

## Runtime Semantics

### FIFO message dispatch

The host runtime is a FIFO pub/sub queue.

When a message is published:

1. The message is appended to the current run queue if the queue limit allows it.
2. The queue is drained in order.
3. For each message, subscribers with the exact same signature and matching
   message-envelope subscribers are invoked.
4. Follow-up messages published by subscribers are appended to the same queue.

Host and context `Publish(...)` calls return `true` when the message was
accepted into the queue and `false` when it was rejected, for example because
`MaxQueuedMessagesPerRun` was reached. A rejected message is not observed by the
published-message observer and is not dispatched.

Publish chains are not recursive direct calls.

```eventscript
on Start {
    publish Step(value: 1)
    publish Step(value: 2)
}

on Step(value) {
    publish Seen(value: value)
}
```

The external input message passed to `host.Publish(...)` is not considered a
published output. Messages emitted through `publish` are observable outputs.

### Host dispatch modes

`host.Publish(message)` enqueues an external input message and returns without
draining the queue. This is the default game-oriented contract.

Manual hosts are pumped by the game loop:

```csharp
host.Publish(GameEventScriptMessage.Create("Start"));

void Update()
{
    var step = host.Update(maxOpcodes: 10);
}
```

Automatic hosts run the same serial dispatch queue on a background pump:

```csharp
var host = GameEventScriptHost.CreateBuilder()
    .WithAutomaticDispatch()
    .Build();

host.Publish(GameEventScriptMessage.Create("Start"));
```

Automatic dispatch uses a `GameEventScriptDispatcher`. `WithAutomaticDispatch()`
uses the shared dispatcher, so multiple hosts do not create one thread each. A
game can also provide its own dispatcher:

```csharp
using var dispatcher = GameEventScriptDispatcher.Create(workerCount: 1);

var host = GameEventScriptHost.CreateBuilder()
    .WithAutomaticDispatch(dispatcher)
    .Build();
```

Tools and tests that need synchronous dispatch can call
`host.PublishToCompletion(message)`.

`GameEventScriptRunStepResult` reports the run state, the number of consumed
internal VM instructions, processed messages, and accepted published messages
for a manual update. Manual updates run stackless BytecodeVM slices on the
caller thread. External C# subscribers are not bytecode and therefore run
atomically once dispatch reaches them.

`BeginRun(...)` is still available for an isolated resumable run object, but the
main host API is `Publish(...)` plus `Update(...)`.

### Subscriber order

Subscribers are matched by message signature, or explicitly by message name when
the handler uses envelope dispatch. Matching subscribers run by host priority,
then by registration order for equal priority. Higher priority values run
earlier; `0` is normal priority.

Script handlers are subscribers. Host callbacks registered with `Subscribe` are
also subscribers. They participate in the same dispatch order.

### Message envelope handlers

Use `on Message as envelope` to subscribe to every signature with the same
message name. The handler still uses normal `matching` and `without` tag
filters, but it receives a single `:envelope` value instead of the message
arguments.

```eventscript
on Damage as envelope matching :radio {
    emit HeardDamage(
        signature: envelope.message.signatureid,
        tags: envelope.tags)
}
```

This is an explicit name-based subscription. It counts as normal delivery, so a
matching envelope handler prevents `undeliverable` fallback. If the envelope
handler's tag filters do not match, delivery can still fall through to
`undeliverable`.

### Undeliverable messages

`undeliverable` is a reserved system endpoint. It receives messages that could
not be delivered to any normal subscriber after message signature and tag
filters were applied.

```eventscript
on undeliverable as envelope matching :radio {
    let message be envelope.message
    let tags be envelope.tags

    emit HeardUnknown(name: message.name, tags: tags)
}
```

The endpoint uses the same `matching` and `without` tag filters as normal
handlers. Without filters, it receives every undeliverable message. The envelope
is map-backed and currently guarantees:

- `message`: the original message as `:message`
- `tags`: all original envelope tags as a list of `:tag` values

The parenthesized form `on undeliverable(envelope as :envelope)` is invalid;
system endpoints bind their envelope through `as`.

The fallback endpoint does not receive itself recursively.

### Messages and ordered signatures

Message arguments are labeled positional slots. Labels are part of the signature
at their position. They are not sorted and cannot be reordered.

```eventscript
publish Travel(from: current, to: target)
publish Travel(to: target, from: current)
```

These produce different signatures:

```text
Travel(from,to)
Travel(to,from)
```

The order is intentional. It lets signatures remain stable and readable.

Unlabeled slots use `_` in the signature.

```eventscript
publish Point(10, 20) // Point(_,_)
```

### Parameters and arguments

Parameters use the same labeled positional model.

```eventscript
on Travel(from, to) {
    publish Seen(start: from, finish: to)
}

on Point(_ x, _ y) {
    publish Seen(x: x, y: y)
}
```

A labeled parameter must be called with the same label at the same position. An
unlabeled `_` parameter must be called without a label.

```eventscript
predicate wounded(_ unit) means unit.hp < unit.maxHp
predicate withinRange(source, target) means source.range >= target.distance

on Start(unit, source, target) {
    let a be wounded(unit)                    // ok, unlabeled
    let b be withinRange(source: source, target: target) // ok
}
```

Labels do not allow argument reordering:

```eventscript
withinRange(target: target, source: source) // different labels at positions
```

The `x is predicateName` and `x is :extension.predicate` forms are the only special
case. They are allowed only for single-parameter callables and bind `x` to the
first parameter, regardless of whether that parameter is labeled or `_`.

### Publishing

Use `publish` with a message literal or with an expression that evaluates to a
message value.

```eventscript
publish Done
publish Damage(unit: unit.id, amount: 5)
publish Point(10, 20)

let msg be Done(value: 10)
publish msg
```

Publishing an unknown message or a message with no normal subscribers is a valid
no-op unless an `undeliverable` endpoint matches it. The message is still
considered published.

Publishing a non-message expression is lenient and does nothing.

### Message and handler values

Messages and handlers are first-class values.

Uppercase calls are disambiguated by argument shape:

```eventscript
let handler be Success(message, value)
let message be Success(message: 'hello', value: true)
```

`Success(message, value)` is a handler value with two labeled parameters.
`Success(message: 'hello', value: true)` is a message value.

Handlers can be bound to message values:

```eventscript
on Start(success) {
    let successHandler be Success(message, value)
    let msg be successHandler(message: 'ok', value: success)

    publish msg
    publish successHandler(message: 'again', value: success)
}
```

If the binding labels do not match the handler signature, the binding evaluates
to `nothing`.

Messages expose:

- `name`
- `signatureid`
- `arguments`

Handlers expose:

- `name`
- `signatureid`
- `parameters`

These members are available through `.` and `[:]`.

```eventscript
let msg be Success(message: 'world', value: 42)
publish Debug(name: msg.name, signature: msg[:signatureid], text: msg.arguments.message)
```

Message and handler values are not maps for type checks.

## Language Semantics

### Variables and scope

Variables are introduced with `let`.

```eventscript
let hp be 10
let name be 'Ada'
let alive be true
```

A `let` may declare a target type:

```eventscript
let hp as :float be '12.5'
let heading as :degree be 450
let tags as :set be [1, 2, 2, 3]
```

Typed `let` applies the same conversion semantics as `value as :type`.

Blocks create child scopes. Variables declared inside `{ ... }` do not leak
outside the block. This applies to `if`, `else`, `for`, and seeded random
blocks.

```eventscript
on Start(value) {
    if value > 0 {
        let label be 'positive'
        publish Seen(label: label)
    }

    // label is not visible here
}
```

Duplicate local variables in the same scope are module build errors.

### Control flow

`if` supports block bodies and single-statement bodies.

```eventscript
if unit.hp <= 0 {
    publish Defeated(unit: unit.id)
} else {
    publish StillAlive(unit: unit.id)
}
```

```eventscript
if unit is defeated publish Defeated(unit: unit.id)
else publish StillAlive(unit: unit.id)
```

`else if` chains are just nested single-statement `if` forms.

```eventscript
if score >= 100 publish Rank(value: :gold)
else if score >= 50 publish Rank(value: :silver)
else publish Rank(value: :bronze)
```

`for` iterates collections, dice, sets, maps, and ranges.

```eventscript
for unit in units {
    publish UnitSeen(id: unit.id)
}

for index from 1 to 5 step 2 {
    publish Tick(value: index)
}
```

Direct range syntax is used without `in`:

```eventscript
for index from 1 to 5 publish Tick(value: index)
```

This is invalid:

```eventscript
for index in from 1 to 5 publish Tick(value: index)
```

### Guarded choices

Guarded choices are expression forms. They pick the first value whose condition
is true, otherwise the `otherwise` value.

```eventscript
let label be
    'critical' when hp <= 0,
    or 'wounded' when hp < maxHp,
    otherwise 'healthy'
```

They are especially useful in computed record fields:

```eventscript
percentage: :percentage computed by
    0% when maximum <= 0,
    otherwise (current / maximum) as :percentage
```

### Predicates

Predicates define reusable predicates. A predicate body must be statically identifiable
as `:boolean` or `:nothing`. Use `as :boolean` when a predicate should intentionally
coerce a value to boolean.
If a predicate cannot be evaluated because required information is missing, the
predicate result remains `nothing`.
`nothing` is neither true nor false: true checks and false checks both fail for it.
Control flow executes `else` when the condition is not true, and selectors such
as `:any` and `:all` use true checks for their predicates.

```eventscript
predicate wounded(_ unit) means unit.hp < unit.maxHp
predicate numeric(_ value) means value * 2 as :boolean

on Start(unit) {
    let a be wounded(unit)
    let b be unit is wounded
    let c be numeric(2) // true, explicit boolean coercion
}
```

Predicates are called like functions, or with `is` when the predicate has exactly one
parameter.

```eventscript
unit is wounded
wounded(unit)
```

Calling an unknown predicate, using the wrong arity, or using `x is predicate` with a
non-unary predicate is a module build error.

### Functions

Functions define reusable expressions. Unlike predicates, functions keep the original
result value.

```eventscript
function woundedUnits(_ units) means units[:filter unit where unit.hp < unit.maxHp]
function byId(_ units) means units[:map unit by unit.id]

on Start(units) {
    let wounded be woundedUnits(units)
    let lookup be byId(units)
    publish Done(count: :len wounded, firstName: wounded[1].name)
}
```

Predicates and functions share one callable namespace. A predicate and a function cannot have
the same name.

### Records and custom types

Records define closed custom types.

```eventscript
record :gauge as {
    current: :float,
    maximum: :float
}
```

A record field has a name and a type. It may also have a clamp and it may be
computed.

```eventscript
record :gauge as {
    current: :float clamped between 0 and maximum,
    maximum: :float clamped between 0 and :infinity,
    percentage: :percentage computed by
        0% when maximum <= 0,
        otherwise (current / maximum) as :percentage
}
```

Materialization converts each source field to the declared field type. Clamped
fields are clamped after conversion, then converted again to the declared type.
Computed fields are evaluated after non-computed fields have been materialized.

```eventscript
on Start {
    let hp as :gauge be [current: 125, maximum: 100]
    publish Done(current: hp.current, ratio: hp.percentage)
}
```

Custom type constructors are labeled-only:

```eventscript
let hp be :gauge(current: 125, maximum: 100)
```

This is invalid:

```eventscript
let hp be :gauge(125, 100)
```

Custom records expose their fields through member access and map-style
lookup, but `hp is :map` is false unless the value is actually a
map.

### Operators

GameEventScript operators are grouped roughly like C-style operators, with readable
aliases where helpful.

Equality:

```eventscript
x = y
x <> y
x =~ y
x ≈ y
x ≅ y
```

`=` and `<>` use exact value equality. For floats this follows normal
double-precision behavior, so `NaN = NaN` is false. `=~` is approximate numeric
equality; `≈` and `≅` are aliases.

Logical operators:

```eventscript
not a
!a
~a
¬a
a and b
a & b
a ∧ b
a xor b
a ⊕ b
a or b
a | b
a ∨ b
a -> b
a → b
a ⇒ b
```

Logical operators use three-valued truth tables. `nothing` represents an unknown
statement, not `false`: `not nothing` is `nothing`, `false and nothing` is
`false`, `true and nothing` is `nothing`, `true or nothing` is `true`, and
`false or nothing` is `nothing`. `xor` returns `nothing` when either operand is
`nothing`.

`a -> b` is implication; `→` and `⇒` are aliases. It is right-associative, so
`a -> b -> c` means `a -> (b -> c)`. It behaves like `not a or b` under the
same three-valued logic: if `a` is false, the result is true; if `a` is true,
`b` decides the result; if `a` is `nothing`, the result is true only when `b` is
true, otherwise it remains `nothing`.

`and`, `or`, and `->` short-circuit. The right-hand expression of `a and b` is
skipped when `a` is false. The right-hand expression of `a or b` is skipped when
`a` is true. The right-hand expression of `a -> b` is skipped when `a` is false.
When the left side is `nothing`, the right side is still evaluated because it can
determine `nothing and false`, `nothing or true`, or `nothing -> true`.

Relational operators:

```eventscript
x < y
x <= y
x ≤ y
x > y
x >= y
x ≥ y
x <> y
x ≠ y
```

Arithmetic operators:

```eventscript
a + b
a - b
a − b
a * b
a × b
a · b
a ⋅ b
a / b
a ÷ b
a ^ b
x²
x³
√x
∛x
a div b
a mod b
a rem b
```

`×`, `·`, `⋅`, `÷`, `−`, `≤`, `≥`, `¬`, `≠`, `∧`, `∨`, and `⊕` are aliases
for their ASCII forms. `/` is numeric division. `^` is exponentiation.
Superscript `²` and `³` are aliases for `^ 2` and `^ 3`. `√` and `∛` are
aliases for square root and cube root power expressions. `div` is floor
division. `mod` is mathematical modulo. `rem` is truncating remainder. The `%`
token is reserved for percentage literals such as `10%`; it is not the modulo
operator.

Numeric literals may use `_` as a digit separator. It is ignored by the parser
and must appear between two digits, for example `100_000`, `100_000.25`, or
`1_000m`.

Numbers can be written directly before an identifier as multiplication. Unit,
percentage, degree, and dice literals keep priority.

```eventscript
100_000  // 100000
2x       // 2 * x
2.5speed // 2.5 * speed
2m       // meter literal, not 2 * m
2 * m    // explicit multiplication by identifier m
2d6      // dice notation in :dice expressions
```

Integer `+`, `-`, `*`, `div`, `mod`, and `rem` preserve integer results when
both operands are integers and the result fits the integer operation. Integer
`/` returns a double when needed.

```eventscript
6 * 7      // 42 as :integer
3 ^ 2      // 9 as :integer
7 / 2      // 3.5 as :float
7 div 2    // 3 as :integer
7 mod 3    // 1 as :integer
0 - 7 rem 3 // -1 as :integer
```

Collection combination operators:

```eventscript
a :combine b
a :merge b
a :intersect b
a :except b
a :zip b
```

`+` also has collection behavior for lists and maps:

```eventscript
[1, 2] + 3
[1, 2] + [3, 4]
[name: 'Ada'] + [hp: 10]
```

### Prefix helpers

These prefix helpers are built into the language:

```eventscript
:len value
:chance percentage
:keys map
:values value
:entries map
:abs value
:ln value
:sqrt value
:cbrt value
:clamp value between min and max
:min of a and b and c
:max of a and b and c
```

Examples:

```eventscript
let count be :len units
let hit be :chance 25%
let keys be :keys stats
let entries be :entries stats
let distance be :abs :vector(3m, 4m)
let naturalLog be :ln :e
let root be :sqrt 81
let cubeRoot be ∛27
let bounded be :clamp hp between 0 and maxHp
let best be :max of 4 and 9 and 2
```

`:ln` is the natural logarithm. It accepts dimensionless numeric values:
`:ln 1` is `0`, `:ln 0` is `-Infinity`, negative values and values with units
produce `NaN`, and `:ln :infinity` is `Infinity`.

`:min` and `:max` work on any values using numeric comparison when all values
are numeric-compatible, otherwise using the stable GameEventScript value order.

### Domain-style boolean phrases

These phrases are equivalent to simpler operators but often read better in
predicates.

```eventscript
hp is 0 or less
hp is at most 10
hp is at least 3
hp is 5 or more
hp is 5 or greater
hand is empty
target has value
```

They map to:

```eventscript
hp <= 0
hp <= 10
hp >= 3
hp >= 5
empty hand
has value target
```

### Presence, emptiness, and defaulting

`nothing` is the language absence value. Missing lookups, unknown identifiers,
out-of-range indexes, and many unsupported operations evaluate to `nothing`.

```eventscript
let missingName be unit['name']
let missingItem be values[99]
let unknownVariable be doesNotExist
```

Use `has value` and `empty` for explicit checks.

```eventscript
has value target
target has value
empty values
values is empty
```

The main predicates are:

- `nothing` has no value and is empty.
- Empty text, list, map, set, dice, and range have no value and
  are empty.
- `NaN` and infinity have no semantic value, but are not considered empty.
- `0` and `false` are valid values.

Use `:default` for fallback values.

```eventscript
let hp be unit.hp :default 0
let name be entry['name'] :default 'unknown'
let first be (items :default [1])[1]
```

`:default` uses the right value when the left value has no semantic value.

### Truthiness

Conditions use boolean conversion.

Examples:

- `true` is true.
- `false` is false.
- `0` is false.
- Non-zero numbers are true.
- Empty values are generally false.
- `nothing` is false.
- `NaN` is false.

Prefer explicit forms in user-facing logic:

```eventscript
if target has value { ... }
if values is empty { ... }
if hp is 0 or less { ... }
```

### Randomness

Random numbers:

```eventscript
:random 1 to 6
:random from 1 to 6
:random from 0.0 to 1.0
```

If both bounds are integers, the result is an integer. If either bound is a
double, the result is a double. Reversed bounds are normalized.

Dice:

```eventscript
let roll be :dice 4d6
```

Dice rolls are stored in descending order. Dice convert to numbers as the sum of
their rolls.

Chance:

```eventscript
if :chance 25% {
    publish Hit
}
```

Seeded random scopes derive a deterministic local random stream from a
unitless integer seed. Dynamic seeds must be declared as `:integer` or cast
explicitly with `as :integer`; units belong on generated random values via
normal casts, not on the seed.

Expression form:

```eventscript
let rolls be :random with (seed as :integer) :list[:select item from 1 to 3 => :random from 1 to 6]
```

Statement form:

```eventscript
:random with seed {
    let roll be :random from 1 to 6
    publish Rolled(value: roll)
}
```

The seeded random scope does not disturb the outer random stream.

## Types and Conversions

Types are written as tags. Built-in public type tags are:

- `:nothing`
- `:tag`
- `:text`
- `:boolean`
- `:uuid`
- `:integer`
- `:float`
- `:percentage`
- `:degree`
- `:meter`
- `:second`
- `:vector`
- `:point`
- `:series`
- `:range`
- `:message`
- `:handler`
- `:envelope`
- `:ref`
- `:list`
- `:map`
- `:set`
- `:dice`

Custom record types are also written as tags, for example `:gauge`.

### Conversion syntax

There are two equivalent conversion forms:

```eventscript
let distance as :meter be 100
let distance2 be :meter(100)

let raw as :float be 90°
let raw2 be :float(90°)
```

`value as :type` is useful in declarations and readable expressions.
`:type(value)` is useful inline.

```eventscript
let scaled be :float(90°) * :float(100m)
```

Type constructors use the same syntax, but may accept more than one argument for
types that define construction shapes.

```eventscript
let offset be :vector(10m, 20m)
let position be :point(10m, 20m)
let hp be :gauge(current: 10, maximum: 20)
let targetRef be :ref(:unit, id: 'unit-42')
```

### `:nothing`

`nothing` is the absence value. It has no literal keyword of its own in script
code; it is produced by missing data and failed lenient operations.

```eventscript
let missing be unit.unknown
let none be missing
```

Converting `nothing` to primitive containers produces empty values in many
places.

### `:tag`

Tags are symbolic values written with a leading colon.

```eventscript
:ready
:boss
:fire
```

Tags are not text, but they convert to text using their name. Special tags
`:infinity`, `:negativeinfinity`, `:nan`, `:pi`, `:e`, `:tau`, and `:phi`
convert to double numeric values. `∞`, `∏`, `ℇ`, `τ`, and `φ` are aliases for
`:infinity`, `:pi`, `:e`, `:tau`, and `:phi`.

```eventscript
let limit as :float be :infinity
let shortLimit as :float be ∞
let circle as :float be ∏
let growth as :float be ℇ
let turn as :float be τ
let golden as :float be φ
```

### `:text`

Text can use single quotes or double quotes. Escape the active quote character by
doubling it.

```eventscript
'hello'
'Ada''s turn'
"didn't say ""stop"""
```

Text converts to numbers and booleans when it can be parsed. Text converts to a
list as a list of one-character text values.

```eventscript
let amount as :float be '12.5'
let flag as :boolean be 'true'
let chars as :list be 'abc'
```

### `:boolean`

Boolean literals are:

```eventscript
true
false
```

Booleans convert to numbers as `1` and `0`.

### `:integer` and `:float`

Numbers are written without a suffix.

```eventscript
12
12.5
0.75
100_000.25
```

Whole-number literals become `:integer`. Float literals become `:float`.
Use `_` between digits as a readability separator; the double separator is
always `.`.

`:integer(value)` erases units and truncates toward zero. Use the standard
integer extensions for other rounding modes.

```eventscript
:integer(10.9)          // 10
:integer.truncate -10.4 // -10
:integer.floor -10.4    // -11
```

`:float(value)` erases units and converts numeric-compatible values to a
unitless double.

```eventscript
:float(90°)   // 90
:float(25%)   // 0.25
:float(true)  // 1
```

### Numeric units: `:degree`, `:meter`, and `:second`

`degree`, `meter`, and `second` are scalar numeric units. They are not separate
value kinds. Whole-number unit literals stay `:integer`; fractional unit
literals stay `:float`.

```eventscript
90°    // integer with :degree
-10°
100m   // integer with :meter
100.5m // float with :meter
15s
```

The built-in unit type tags are:

- `:degree`
- `:meter`
- `:second`

`as :degree`, `as :meter`, and `as :second` apply a unit to a unitless number,
keep a matching unit, and return `NaN` for incompatible units.

```eventscript
let heading as :degree be 450
let distance as :meter be 100
let rawHeading as :float be heading
```

Unit arithmetic is intentionally strict.

```eventscript
100m + 50m // 150m as :integer
100m + 50  // NaN
100m + 5s  // NaN
100m * 2   // 200m as :integer
2 * 100m   // 200m as :integer
100m / 2   // 50m
100m / 25m // 4
370m mod 90m // 10m as :integer
100m mod 3   // NaN
```

Degree values are open numeric units. Arithmetic does not automatically wrap.

```eventscript
360° + 90° // 450°
10° - 40°  // -30°
```

Use `:degree.wrap` for compass-style wrapping.

```eventscript
:degree.wrap -10° // 350°
:degree.wrap 370  // 10°
```

### `:percentage`

Percentages are ratios, not units. `5%` stores the ratio `0.05`.

```eventscript
5%
25%
100%
```

Percentage arithmetic has special predicates so that right-hand percentages can be
relative to a left base value.

```eventscript
100 + 5%  // 105
100 - 5%  // 95
100 * 5%  // 5
100 / 5%  // 2000

100m + 5% // 105m
100m * 5% // 5m
5% * 100m // 5m
```

Pure percentage arithmetic keeps percentages where that is meaningful.

```eventscript
15% + 15% // 30%
15% - 5%  // 10%
15% * 2   // 30%
15% / 3   // 5%
15% * 15% // 2.25%
15% / 15% // 1
```

Left-hand percentage addition and subtraction against a base are invalid.

```eventscript
5% + 100  // NaN
5% - 100m // NaN
```

`:percentage(value)` treats integers as percent notation, so
`:percentage(25)` is `25%`. Unit values cannot be converted to percentages.

### `:vector`

Vectors store three double components. Two-dimensional values are represented
with `z = 0`. They may also have one shared unit for all components.

```eventscript
let a be :vector(10, 20)
let b be :vector(10m, 20m)
let c be :vector(1, 2, 3)
```

Component members are always `x`, `y`, and `z`.

```eventscript
let offset be :vector(3m, 4m)
offset.x // 3m
offset.y // 4m
```

Constructors support positional and labeled component forms.

```eventscript
:vector()
:vector(10)
:vector(10, 20)
:vector(x: 10, y: 20)
:vector(1, 2, 3)
:vector(x: 1, y: 2, z: 3)
:vector(y: 20m) // x and z default to 0m
:vector(z: 5m) // x and y default to 0m
```

Labeled component order must be `x`, then `y`, then `z`. Labels may be omitted,
but they cannot be reordered. Missing zero components inherit the detected
component unit.

Constructing from an existing vector keeps `x` and `y`; the two-argument form
sets `z` explicitly.

```eventscript
let flat be :vector(10m, 20m)
let lifted be :vector(flat)       // keeps z as 0m
let elevated be :vector(flat, 5m) // explicit z
```

Vectors can also be converted from lists or maps with matching
components.

```eventscript
let fromList as :vector be [1, 2, 3]
let fromDict as :vector be [x: 10, y: 20]
```

`:float(vector)` erases a vector's unit. `:meter(vector)` and other unit
conversions apply a unit to a unitless vector or keep a matching vector unit.

```eventscript
let v be :vector(3m, 4m)
let raw be :float(v) // vector[x: 3, y: 4, z: 0]
let remetered be :meter(raw)
```

Mixed component units evaluate to `NaN`.

```eventscript
:vector(3m, 4s) // NaN
:vector(3, 4m)  // NaN
```

Vector arithmetic:

```eventscript
:vector(1m, 2m) + :vector(3m, 4m) // vector[x: 4m, y: 6m, z: 0m]
:vector(1m, 2m) - :vector(3m, 4m) // vector[x: -2m, y: -2m, z: 0m]
-:vector(1m, 2m)                   // vector[x: -1m, y: -2m, z: 0m]
:vector(1m, 2m) * 2                // vector[x: 2m, y: 4m, z: 0m]
2 * :vector(1m, 2m)                // vector[x: 2m, y: 4m, z: 0m]
:vector(3m, 4m) / 2                // vector[x: 1.5m, y: 2m, z: 0m]
:abs :vector(3m, 4m)               // 5m
:abs :vector(1, 2, 2)              // 3
```

Vector addition and subtraction require the same unit.
`vector * vector`, `scalar / vector`, vector `div`, vector `mod`, vector `rem`,
division by zero, and incompatible units evaluate to `NaN`.

### `:point`

Points store absolute positions. They are also always three-dimensional, with
`z = 0` for two-dimensional values. A vector is a relative delta, while a point
is a place in a coordinate system.

```eventscript
let position be :point(10m, 20m)
let elevated be :point(10m, 20m, 5m)
```

Points use the same shared-unit predicate as vectors. All components must be unitless
or all components must use the same unit.

```eventscript
:point(10m, 20m) // ok
:point(10m, 20s) // NaN
:point(10, 20m)  // NaN
```

Component members are always `x`, `y`, and `z`.

Constructors support positional and labeled component forms, including partial
labeled construction where missing earlier components default to zero.

```eventscript
:point()
:point(10)
:point(10, 20)
:point(x: 10, y: 20)
:point(1, 2, 3)
:point(x: 1, y: 2, z: 3)
:point(y: 20m) // x and z default to 0m
:point(z: 5m) // x and y default to 0m
```

Constructing from an existing point keeps `x` and `y`; the two-argument form
sets `z` explicitly. Converting a vector to a point, or a point to a vector, is
not implicit because that would erase the distinction between absolute positions
and relative deltas.

```eventscript
let ground be :point(10m, 20m)
let copied be :point(ground)
let elevated be :point(ground, 5m)
```

Point arithmetic is affine:

```eventscript
:point(10m, 20m) + :vector(3m, -5m) // point[x: 13m, y: 15m, z: 0m]
:point(13m, 15m) - :vector(3m, -5m) // point[x: 10m, y: 20m, z: 0m]
:point(13m, 15m) - :point(10m, 20m) // vector[x: 3m, y: -5m, z: 0m]
```

`point + point`, `vector + point`, point scalar arithmetic, point `div`, point
`mod`, point `rem`, unary minus on points, `:abs point`, and incompatible units
evaluate to `NaN`.

### `:uuid`

A UUID is a 128-bit RFC-formatted id value stored in canonical byte order.
Construct one from the canonical 8-4-4-4-12 hexadecimal text form:

```eventscript
let id be :uuid('550e8400-e29b-41d4-a716-446655440000')
let ref be :ref(:unit, id: id)
```

UUID equality compares the 128-bit value. Text conversion returns lowercase
canonical form. UUIDs can be used as map lookup keys through that
canonical form, for example `items[id]` when the map was keyed by UUID
values. Arithmetic and other numeric operations on UUIDs evaluate to `nothing`.

### `of ... and ...`

Use `of ... and ...` to create a list without brackets:

```eventscript
let values be of 10 and 20 and 30
let listValues as :list be of 10 and 20 and 30
```

Helpers such as `:keys`, `:values`, and `:entries` also produce lists.

### `:series`

A series is a repeatable, index-addressed value for potentially infinite
mathematical terms. It is not a lookup collection and has no finite length.

```eventscript
let fib be :series.fibonacci()
let odds be :series.natural(start: 1, step: 2)
let fib7 be fib[:term 7]
let firstFive be fib[:take first 5]
let withoutFirstFive be fib[:drop first 5]
```

Built-in series helpers are `:series.natural()`, `:series.fibonacci()`, and
`:series.factorial()`. `:term` is zero-based. `:take first n` materializes a
list of the first `n` terms, while `:drop first n` returns another series. A
series used as a scalar value reads as its first term. Unsupported selectors,
lookups, non-integer term indexes, and finite-length requests evaluate to
`nothing`.

### `:range`

Ranges are iterable integer ranges.

```eventscript
let odds as :range be from 1 to 9 step 2
let descending be from 5 to 1 step (0 - 2)
```

Ranges support one-based lookup and containment without full materialization.

```eventscript
odds[1]     // 1
odds[3]     // 5
5 in odds   // true
1.5 in odds // false
```

A step of `0`, or a step direction that cannot reach the target, produces an
empty range.

### `:list`

Lists are ordered immutable collections.

```eventscript
[1, 2, 3]
['a', 'b', 'c']
[]
```

Lists use one-based indexing.

```eventscript
let values be [10, 20, 30]
values[1] // 10
values[3] // 30
values[0] // nothing
```

### `:map`

Maps use text keys. Literal keys are written without quotes.

```eventscript
[name: 'Ada', hp: 10]
[:]
```

Lookup can use text, tags, variables, or member syntax.

```eventscript
let unit be [name: 'Ada', hp: 10]

unit['name']
unit[:name]
unit.name
```

Missing keys return `nothing`.

### `:set`

Sets are immutable, deduplicated, and ordered by the stable GameEventScript value
order.

```eventscript
:set[1, 2, 2, 3] // set containing 1, 2, 3
```

Sets are useful for membership checks and set operations.

### `:dice`

Dice values are ordered descending.

```eventscript
let roll be :dice 4d6
roll[1] // highest roll
```

Dice convert to a number as the sum of their rolls and to a list as the ordered
roll values.

### `:message` and `:handler`

Message and handler values are first-class values with specialized members.

```eventscript
let h as :handler be Done(value)
let m as :message be h(value: 42)
```

They can be passed as arguments, stored in maps, published, and
inspected. They are not maps for type checks.

### `:envelope`

Envelope values are map-backed system values. They are intentionally open
so envelope metadata can be extended without changing the value shape. The
current system envelope is used by `undeliverable` and exposes `message` and
`tags`.

### `:ref`

Refs are immutable handles to record or external-type state. A ref stores only
the target type and a stable id; it does not read or mutate the target object by
itself.

```eventscript
record :unit as {
    id: :text,
    hp: :integer
}

let unitRef as :ref be :ref(:unit, id: 'unit-42')
unitRef.type // :unit
unitRef.id   // unit-42
```

`:ref(value)` is a cast and succeeds only when `value` is already a ref.

## Collection Language

Collections are central in GameEventScript. A value followed by `[...]` either does
a lookup or applies a collection selector.

```eventscript
values[1]
unit[:name]
units[:filter unit where unit.hp > 0]
```

### Lookup

For lists, dice, and ranges, numeric lookup is one-based. Series do
not support lookup; use `:term` instead.

```eventscript
[10, 20, 30][2] // 20
```

For maps and custom record values, lookup uses keys.

```eventscript
unit[:hp]
unit['hp']
unit.hp
```

For vectors and points, lookup and member access expose components.

```eventscript
position.x
position[:y]
```

### Generated collections

Generated collections produce lists or sets from ranges or iterable values.

```eventscript
let squares be :list[:select item from 1 to 5 => item * item]
let evens be :list[:select item from 1 to 10 where item mod 2 = 0 => item]
let doubled be :list[:select item in values => item * 2]
let residues be :set[:select item in values where item > 3 => item mod 2]
```

Use `from ... to ... [step ...]` directly inside generated collections. Do not
write `in from ...`.

### Filtering and projection

```eventscript
units[:filter unit where unit.alive]
units[:select unit => unit.name]
units[:map unit by unit.id]
units[:map unit by unit.id => unit.name]
```

Projection selectors use `=>`; `↦` is an alias for the same projection arrow.
Map projection uses last-wins semantics when duplicate keys occur.

### Quantifiers and count

```eventscript
units[:any unit where unit.hp <= 0]
units[:all unit where unit.alive]
units[:count unit where unit.hp < unit.maxHp]
```

`:any` and `:all` return booleans. `:count` returns an integer.

### Aggregates and extrema

```eventscript
units[:sum unit => unit.hp]
units[:average unit => unit.hp]
units[:min unit => unit.hp]
units[:max unit => unit.hp]
units[:highest unit => unit.hp]
units[:lowest unit => unit.hp]
```

`:sum` and `:average` aggregate projected numeric values. `:min`, `:max`,
`:highest`, and `:lowest` return the original item whose projection is the
smallest or largest.

### First, last, and single

```eventscript
units[:first]
units[:last]
units[:single]

units[:first unit where unit.alive]
units[:last unit where unit.alive]
units[:single unit where unit.role = :boss]
```

`:single` returns `nothing` unless exactly one item matches.

### Sorting, ordering, distinct, and grouping

```eventscript
items[:sort ascending]
items[:sort descending]

units[:order by unit => unit.initiative descending]
items[:distinct]
units[:distinct by unit => unit.faction]
units[:group by unit => unit.faction]
```

`:group by` returns a map whose keys are projected group values and whose
values are lists of matching items.

### Contains

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

For maps, `:contains` checks keys.

### Membership operators

Membership and boundary checks can be written as infix expressions.

```eventscript
:boss in unitTags
:boss ∈ unitTags
:ghost ∉ unitTags
'name' in unit
'Ada' value in unit
'tt' in 'battle'
[1, 2, 3] starts with [1, 2]
[1, 2, 3] ends with [2, 3]
```

For maps, `x in map` checks keys and `x value in map` checks values.
`∈` is an alias for `in`; `∉` is the negated membership operator.

### Collection operations

```eventscript
items[:reverse]
items[:shuffle]
items[:draw 3]

items[:take first 3]
items[:take last 2]
items[:take highest 2]
items[:take lowest 2]

items[:drop first 3]
items[:drop last 2]
items[:drop highest 1]
items[:drop lowest 1]
```

Because values are immutable, `:draw` returns drawn items but does not mutate the
source.

```eventscript
let cards be [1, 2, 3, 4, 5][:shuffle]
let hand be cards[:draw 3]
let rest be cards[:drop first 3]
```

`:shuffle` and `:draw` are supported for ordered collections such as lists and
dice. They are lenient no-ops for unordered sets and maps.

### Choosing

`:choose` functions items from a collection.

```eventscript
units[:choose 1]
units[:choose 1 unit where unit.alive]
units[:choose 2 at random unit where unit.alive]
units[:choose 1 weighted by unit => unit.weight]
```

Without `at random`, the first matching items are chosen. With `at random`, the
runtime random generator is used. Weighted choices use the projected weight.

### Dice and pattern matching

Dice and list-like collections support pattern checks.

```eventscript
roll[:has pair]
roll[:has pair of 6]
roll[:has three of a kind]
roll[:has three of 6]
roll[:has full house]
roll[:has straight]
```

Supported count patterns are `pair`, `three`, `four`, `five`, `six`, and
`seven`.

Pattern extraction uses `:take`.

```eventscript
roll[:take pair]
roll[:take full house]
cards[:take straight]
```

For straight checks, duplicates are ignored.

### Object matching

Object matching uses map-shaped subset patterns.

```eventscript
units[:has [faction: 'orc', alive: true]]
units[:has [owner: [team: 'red']]]
```

Predicates:

- Extra keys in the actual object are allowed.
- All specified keys must exist.
- Nested patterns match recursively.
- Non-map items do not match.

### Collection combination

The collection operators work by collection kind.

Lists and dice:

```eventscript
[1, 2] :combine [3, 4]  // [1, 2, 3, 4]
[1, 2, 3] :except [2]   // [1, 3]
[1, 2] :zip ['a', 'b']  // [{ left: 1, right: 'a' }, ...]
```

Maps:

```eventscript
[name: 'Ada'] :merge [hp: 10]
[name: 'Ada', hp: 5] :merge [hp: 10] // right side wins
[a: 1, b: 2] :intersect [b: 9]       // [b: 2]
```

Sets:

```eventscript
:set[1, 2] :merge :set[2, 3]
:set[1, 2, 3] :intersect :set[2, 4]
:set[1, 2, 3] :except :set[2]
```

Unsupported combinations evaluate to `nothing`.

## Extensions

Extensions let the host provide functions without making every function a core
language keyword. Extension calls use `:extension.function` syntax.

```eventscript
let floored be :math.floor value
let best be :math.max of a and b and c
let turn be :nav.shortestTurn from: current to: target
let sameTurn be :nav.shortestTurn(from: current, to: target)

if heading is :nav.isNorth {
    publish FacingNorth
}
```

### Extension call forms

Extensions support four call shapes.

Unary sugar:

```eventscript
:math.floor value
:math.floor(value)
```

Repeatable `of ... and ...` arguments:

```eventscript
:math.max of a and b and c
```

Ungrouped labeled arguments:

```eventscript
:nav.shortestTurn from: current to: target
```

Grouped arguments:

```eventscript
:nav.shortestTurn(from: current, to: target)
:nav.shortestTurn(from: current to: target)
```

Arguments are labeled positional. The signature includes labels in order.

```text
math.floor(_)
math.max(_,_,_)
nav.shortestTurn(from,to)
```

Label order is not flexible. `from: a to: b` and `to: b from: a` are different
signatures.

### Predicate extension sugar

Single-argument predicate extensions can be used with `is`.

```eventscript
if heading is :nav.isNorth {
    publish FacingNorth
}
```

This binds `heading` as the first argument.

### Required standard extensions

Some extension-looking functions are required standard intrinsics. They are
parsed as extension calls, but they are built into the runtime and do not need a
host registry.

Integer rounding:

```eventscript
:integer.floor value
:integer.ceil value
:integer.truncate value
:integer.halfEven value
:integer.halfUp value
:integer.halfDown value
```

Examples:

```eventscript
:integer.floor 10.9      // 10
:integer.floor -10.4     // -11
:integer.ceil -10.4      // -10
:integer.truncate -10.4  // -10
:integer.halfEven 12.5   // 12
:integer.halfEven 13.5   // 14
:integer.halfUp -12.5    // -13
:integer.halfDown -12.5  // -12
```

Degree helpers:

```eventscript
:degree.wrap value
:degree.toRadians value
:degree.fromRadians value
```

Examples:

```eventscript
:degree.wrap -10°       // 350°
:degree.wrap 370        // 10°
:degree.toRadians 180°  // 3.1415926535897932384626433833
:degree.fromRadians 3.1415926535897932384626433833 // 180°
```

`:degree.wrap` and `:degree.toRadians` accept unitless numeric values or degree
values. Other units produce `NaN`. `:degree.fromRadians` accepts only unitless
numeric values.

Standard intrinsics are not host-overridable and do not appear in compiled
external references.

### Host-provided extensions

Host extensions are dynamically bound when a compiled script is loaded into an
`GameEventScriptHost`.

The runtime records each external reference as:

- extension name
- function name
- ordered argument labels
- signature id

For example:

```eventscript
let turn be :nav.shortestTurn from: current to: target
```

requires:

```text
nav.shortestTurn(from,to)
```

If the compiled script contains external references, the host must provide a
registry. Missing registries or missing functions are dynamic-link errors during
host load, not late runtime lookups.

The extension API uses `GameEventScriptFastValue` to avoid boxing primitive values at
the runtime boundary.

```csharp
public interface IGameEventScriptExtensionFunction
{
    GameEventScriptFastValue Invoke(
        GameEventScriptExtensionContext context,
        ReadOnlySpan<GameEventScriptFastValue> arguments);
}

public interface IGameEventScriptExtensionRegistry
{
    bool TryResolve(
        GameEventScriptExtensionReference reference,
        out IGameEventScriptExtensionFunction function);
}
```

`GameEventScriptFastValue` exposes unboxed primitives for common values:

- `Kind`
- `Integer`
- `Number`
- `Boolean`
- `Text`
- `Unit`
- `X`, `Y`, `Z`
- `IsReferenceBacked`
- `ToGameEventScriptValue()`

Host functions should use the fast properties when possible and only call
`ToGameEventScriptValue()` for complex values or when full boxed semantics are
needed.

Minimal registry example:

```csharp
public sealed class MathFloorFunction : IGameEventScriptExtensionFunction
{
    public GameEventScriptFastValue Invoke(
        GameEventScriptExtensionContext context,
        ReadOnlySpan<GameEventScriptFastValue> arguments)
        => GameEventScriptFastValue.FromFloat(Math.Floor(arguments[0].Number));
}

public sealed class GameExtensionRegistry : IGameEventScriptExtensionRegistry
{
    public bool TryResolve(
        GameEventScriptExtensionReference reference,
        out IGameEventScriptExtensionFunction function)
    {
        if (reference.SignatureId == "math.floor(_)")
        {
            function = new MathFloorFunction();
            return true;
        }

        function = default!;
        return false;
    }
}
```

Load with a registry:

```csharp
var bytecode = GameEventScriptManager.Compile(script);

var host = GameEventScriptHost.CreateBuilder()
    .WithRegistry(new GameExtensionRegistry())
    .Build()
    .Load(bytecode);
```

## Host API

Compile GameEventScript source (`.ges`) to GameEventScript bytecode (`.gesb`)
with `GameEventScriptManager.Compile(...)`. The current executable artifact is an
in-memory `GameEventScriptCompiled` object. `GameEventScriptBinary` is the
emerging compact binary container shape that will become the physical `.gesb`
format.

```csharp
var bytecode = GameEventScriptManager.Compile(script);
```

Compile options use the public GameEventScript options type:

```csharp
var bytecode = GameEventScriptManager.Compile(
    script,
    new GameEventScriptCompileOptions
    {
        EnableDiagnostics = true
});
```

`EnableDebugInfo` emits the optional debug segment used by tooling and
diagnostic trace sites. `EnableDiagnostics` requests debug info automatically so
runtime collectors can resolve names and slots.

For multiple source strings or files, use `GameEventScriptBuilder` directly:

```csharp
var bytecode = GameEventScriptBuilder.Create()
    .AddScript(script)
    .AddFile("combat.ges")
    .WithOptimization()
    .Compile();
```

### Compiled bytecode artifact

`GameEventScriptCompiled` is the portable in-memory bytecode artifact produced by
the compiler. It does not contain AST nodes or BytecodeVM execution objects.
Loading it into a host builds the VM executable internally.

The artifact exposes neutral bytecode data:

- `ModuleName`
- `StringPool`
- `UShortListPool`
- `OutboundMessageSignatures`
- `ExternalReferences`
- `Callables`
- `Handlers`
- `TypeDefinitions`
- `Code`
- `MaxFrameSlots`
- Optional `DebugSegment`

Literal constants are encoded by typed linear load instructions instead of an
object-shaped constant pool. `LoadInteger` uses the overlapped `I64` payload plus
`UnitAndFlags`, `LoadFloat` uses the overlapped IEEE-754 `F64` payload plus
`UnitAndFlags`, `LoadText`/`LoadTag` reference the `StringPool`, and
booleans/nothing use dedicated opcodes. This preserves the exact constant kind
in bytecode without storing runtime values.

`GameEventScriptBytecodeDumper.DumpBytecode(...)` can be used to inspect this
structure during development. The dump is deterministic and diagnostic; it is not
the `.gesb` binary interchange format.

`ToGameEventScriptBinary(...)` projects the current compiled artifact
into the first compact binary container shape: a 16-byte `GESB` header, module
name, zero-based string pool, compact `UInt16SliceTable`, and one public bind
table. Statically shaped emitted/published messages are represented as
`OutboundMessage` bind entries with message name plus ordered argument names.
Bind kinds in the `0x10` range export message handlers/functions/predicates;
bind kinds in the `0x20` range import extension calls and outbound messages.

The host loads bytecode and builds the BytecodeVM executable internally.

```csharp
var published = new List<GameEventScriptMessage>();

var host = GameEventScriptHost.CreateBuilder()
    .WithPublishedMessageObserver(message => published.Add(message))
    .Build()
    .Load(bytecode);

host.Publish(GameEventScriptMessage.Create(
    "Start",
    ("value", GameEventScriptValueFactory.GesInteger(3))));

host.Update(maxOpcodes: 100);
```

### Host builder options

`GameEventScriptHostBuilder` supports:

- `WithRandom(...)`
- `WithRegistry(...)`
- `WithDiagnosticCollector(...)`
- `WithPublishedMessageObserver(...)`
- `WithRuntimeLimits(...)`
- `WithDispatchMode(...)`
- `WithAutomaticDispatch()`
- `WithAutomaticDispatch(dispatcher)`

External subscribers can be registered with `Subscribe`.

```csharp
host.Subscribe(
    GameEventScriptMessageSignature.Create("Done", ["value"]),
    (message, context) =>
    {
        // host callback
    });
```

`Load` and `Subscribe` accept an optional `priority`. The normal priority is
`0`; higher values run earlier and lower values run later. When priorities are
equal, registration order is preserved. Script handlers and external
subscribers use the same priority model.

External subscriber exceptions are swallowed by runtime dispatch so that host
callbacks remain lenient like script handlers.

### Diagnostics

Diagnostics are collected through `IGameEventScriptDiagnosticCollector`.
Compile with diagnostics enabled so the optional debug segment contains the
diagnostic sites consumed by the VM.

```csharp
var diagnostics = new GameEventScriptDiagnosticTraceCollector();

var bytecode = GameEventScriptManager.Compile(
    script,
    new GameEventScriptCompileOptions { EnableDiagnostics = true });

var host = GameEventScriptHost.CreateBuilder()
    .WithDiagnosticCollector(diagnostics)
    .Build()
    .Load(bytecode);
```

Diagnostic event kinds include:

- `DispatchStarted`
- `SubscriberMatched`
- `SubscriberInvoked`
- `DispatchCompleted`
- `HandlerInvoked`
- `ParameterBound`
- `LetEvaluated`
- `ExpressionEvaluatedToNothing`
- `PredicateCalled`
- `FunctionCalled`
- `EventPublished`
- `RuntimeLimitReached`

Host publish diagnostics are recorded independently of compile diagnostics.

## Errors and Limits

GameEventScript is lenient at runtime, but it still rejects malformed programs at
syntax, module-build, or VM-compile time.

Syntax errors include malformed tokens, missing expressions, removed syntax,
invalid handler headers, invalid dice counts, and invalid selector syntax.

Module build errors include:

- missing predicate/function calls
- wrong predicate/function arity
- wrong argument labels
- duplicate variables in a scope
- duplicate handler/predicate/function parameters
- predicate/function name conflicts
- invalid type constructors
- custom type constructor fields that do not exist

Dynamic-link errors occur when a script references host extensions that cannot
be bound by the configured registry.

### Runtime limits

Runtime limits prevent runaway scripts.

```csharp
var limits = new GameEventScriptRuntimeLimits
{
    MaxProcessedEventsPerRun = 64,
    MaxQueuedMessagesPerRun = 20,
    MaxExecutionSteps = 100_000,
    MaxLoopIterations = 100_000,
    MaxCallDepth = 64,
    MaxRangeItems = 10_000,
    MaxGeneratedCollectionItems = 10_000,
    MaxDiceCount = 1_000,
    MaxDiceSides = 1_000_000
};
```

`MaxProcessedEventsPerRun` limits explicit run-to-completion dispatches such as
`PublishToCompletion(...)` and isolated `BeginRun(...)` executions. Persistent
manual and automatic hosts use `MaxQueuedMessagesPerRun` plus opcode budgets for
flow control.

`MaxQueuedMessagesPerRun` limits how many messages may wait in an active queue
at once. When the queue is full, the newest published message is dropped and the
publish operation returns `false`. Values less than or equal to zero disable this
queue-length limit.

When a runtime budget is reached, execution stops leniently and a
`RuntimeLimitReached` diagnostic is recorded when diagnostics are available.

Range lookup and range containment can be checked without materializing the
whole range. Materializing a range as a list or set is subject to range limits.

## Practical Examples

### Damage and defeat

```eventscript
predicate defeated(_ unit) means unit.hp is 0 or less

on DamageTaken(unit, amount) {
    let hp be unit.hp - amount

    if hp is 0 or less {
        publish UnitDefeated(unit: unit.id)
    } else {
        publish UnitHpChanged(unit: unit.id, hp: hp)
    }
}
```

### Filtering candidates

```eventscript
record :unit as {
    alive: :boolean,
    hidden: :boolean,
    id: :text
}

predicate targetable(_ unit as :unit) means unit.alive and not unit.hidden
function targetableUnits(_ units) means units[:filter unit where unit is targetable]

on ChooseTarget(units) {
    let candidates be targetableUnits(units)
    let target be candidates[:choose 1 at random]
    publish TargetChosen(unit: target.id)
}
```

### Building a lookup map

```eventscript
function unitsById(_ units) means units[:map unit by unit.id]

on Start(units) {
    let byId be unitsById(units)
    let hero be byId[:hero]
    publish Ready(name: hero.name :default 'Unknown')
}
```

### Dice logic

```eventscript
on RollAttack {
    let roll be :dice 4d6
    let crit be roll[:has pair of 6]
    let total be roll[:sum die => die]

    if crit {
        publish CriticalHit(total: total)
    } else {
        publish NormalHit(total: total)
    }
}
```

### Navigation with points, units, and vectors

```eventscript
on Move(position, offset) {
    let next be position + offset
    publish PositionChanged(value: next)
}

on Aim(heading, targetHeading) {
    let current be :degree.wrap heading
    let target be :degree.wrap targetHeading
    publish HeadingSeen(current: current, target: target)
}
```

### Custom record type

```eventscript
record :gauge as {
    current: :float clamped between 0 and maximum,
    maximum: :float clamped between 0 and :infinity,
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

## Grammar Reference

This document explains the language semantically. For the compact grammar, see
[GameEventScript.bnf](./GameEventScript.bnf).
