# GameEventScript Language Definition

GameEventScript is an event-oriented scripting language for deterministic game
logic. A script declares records, predicates, functions, and message handlers.
The runtime receives messages, dispatches matching handlers, and lets handlers
emit local messages or publish outbound messages.

This document describes the current language and runtime semantics.

## Contents

- Program structure
- Lexical rules
- Messages and dispatch
- Statements
- Expressions and operators
- Types and values
- Collections and selectors
- Randomness, dice, and series
- Extensions
- Runtime and host API
- Grammar

## Program Structure

A source file contains an optional module declaration followed by top-level
declarations:

```ges
module Battle

record :unit as {
  hp: :number clamped between 0 and maxHp,
  maxHp: :number
}

predicate alive(_ unit) means unit.hp > 0
function missingHp(_ unit) means unit.maxHp - unit.hp

on Damage(unit, amount) {
  if unit is alive {
    emit Damaged(unit: unit, amount: amount)
  }
}
```

Top-level declarations are:

- `record :name as { ... }`
- `predicate name(...) means expression`
- `function name(...) means expression`
- `on Message(...) { ... }`
- `on Message as envelope { ... }`
- `on initialization { ... }`
- `on undeliverable as envelope { ... }`

The module declaration is optional. If it is omitted, the compiler creates a
stable anonymous module name from the source text.

## Lexical Rules

Whitespace separates tokens. New lines and semicolons separate statements.
Line comments start with `//` and continue to the end of the line.

```ges
let x be 10
let y be 20; let z be x + y // same statement separator model
```

Names are intentionally narrow:

- Identifiers start lowercase and contain letters, with an optional final
  numeric suffix such as `target_2`.
- Message names start uppercase and contain letters.
- Tags start with `:` and a lowercase tag name.
- Type names are tags. Built-in type tags are reserved by the language.

Text literals can use single or double quotes. The quote character is escaped by
doubling it:

```ges
let a be 'didn''t'
let b be "say ""hello"""
```

## Messages and Dispatch

Messages have a name, ordered argument names, argument values, and tags. The
ordered argument names form the signature. An unlabeled argument uses `_`.

```ges
emit Hit(target: unit, amount: 10)
emit Log('ready')              // signature Log(_)
```

Exact handlers receive messages whose name and ordered argument names match:

```ges
on Hit(target, amount) {
  emit Applied(target: target, amount: amount)
}

on Log(_ text) {
  emit Logged(text: text)
}
```

Parameters can declare a type. The runtime casts the incoming value before the
handler body runs:

```ges
on Move(unit, speed as :quantity(m)) {
  emit Moving(unit: unit, speed: speed)
}
```

Envelope handlers match by message name and tags, but receive one `:envelope`
value instead of normal message arguments:

```ges
on Hit as envelope matching :enemy {
  emit Heard(message: envelope.message, tags: envelope.tags)
}
```

The envelope currently contains:

- `message`: the original `:message`
- `tags`: a `:list` of message tags

`undeliverable` is a system endpoint. It receives messages that were not
otherwise dispatched, and it must use envelope binding:

`initialization` is a parameterless system endpoint. It is queued when a host
session starts and runs before later messages in that session:

```ges
on initialization {
  emit Ready
}
```

```ges
on undeliverable as envelope {
  emit Unknown(message: envelope.message)
}
```

Handlers can filter tags:

```ges
on Radio as envelope matching :open, :enemy without :encrypted {
  emit Intercepted(message: envelope.message)
}
```

`matching` requires all listed tags. `without` rejects messages containing any
listed tag. The envelope still contains all original tags.

## Statements

### `emit` and `publish`

`emit` sends a message into the local event space. `publish` sends a message to
the host publish hook.

```ges
emit Done
emit Done(value: 10)
emit Done(value: 10) with :combat, :visible

publish Fire(target: enemy)
publish Scan with [:radar, :active]
```

Tags after `with` can be tags or lists of tags.

### `let`

`let` creates an immutable local binding:

```ges
let damage be base + bonus
let percent as :percentage be 25%
```

The optional declared type casts the value.

### `if`

`if` runs the then-branch only when the condition is true. `nothing` is not true.
`else` runs for every non-true condition.

```ges
if unit.hp > 0 {
  emit Alive(unit: unit)
} else {
  emit Defeated(unit: unit)
}
```

Single-statement bodies are allowed:

```ges
if ready emit Ready
```

### `for`

Loops can iterate a collection expression or a range.

```ges
for unit in units {
  emit Seen(unit: unit)
}

for index from 1 to 10 step 2 {
  emit Tick(index: index)
}
```

A direct range after `in` is rejected. Use `for x from ... to ...`, or bind the
range to a value first.

### Seeded Random Scope

`:` `random with` evaluates a body under a deterministic sub-random scope. The
seed must statically resolve to a unitless integer number; dynamic seeds should
be cast explicitly with `as :number`.

```ges
:random with seed as :number {
  emit Roll(value: :random from 1 to 6)
}

let value be :random with 123 (:random from 1 to 100)
```

## Expressions and Operators

Expressions are evaluated left to right according to precedence. `and`, `or`,
and implication short-circuit.

From high to low precedence:

1. Postfix: member access `x.y`, lookup/selector `x[...]`
2. Power: `^`, `²`, `³`
3. Unary: `-`, `not`, `!`, `~`, `¬`, `has value`, `empty`, tagged unary helpers
4. Multiplicative: `*`, `/`, `div`, `mod`, `rem`
5. Additive: `+`, `-`
6. Relational: `<`, `>`, `<=`, `>=`, `≤`, `≥`
7. Type and predicate operations: `is`, `is not`, `as`
8. Membership and text boundaries: `in`, `∈`, `∉`, `value in`, `starts with`, `ends with`
9. Equality: `=`, `<>`, `≠`, `=~`, `≈`, `≅`
10. `and`, `xor`, `or`
11. `:default`
12. Implication: `->`, `→`, `⇒`
13. Guarded choice: `value when condition, otherwise fallback`

### Boolean Logic

Predicates must evaluate to `:boolean` or `:nothing`. Use `as :boolean` when a
coercion is intentional.

Logical operators use tri-state logic over true, false, and `nothing`.

- `true or nothing` is `true`
- `false or nothing` is `nothing`
- `true and nothing` is `nothing`
- `false and nothing` is `false`
- `nothing and nothing` is `nothing`
- `nothing or nothing` is `nothing`

`if` and branch guards test only for true. A condition that is false or
`nothing` does not pass.

Implication `A -> B` is right-associative. It is true when `A` is false or `B` is
true; it is false when `A` is true and `B` is false; unresolved cases produce
`nothing`.

### Guarded Choice

Guarded choices select the first value whose condition is true. `otherwise` is a
keyword.

```ges
let status be
  :dead when hp <= 0,
  :wounded when hp < maxHp,
  otherwise :healthy
```

### Predicate Sugar

Unary predicates can be used with `is`:

```ges
predicate high(_ value) means value > 50

let ok be value is high
let notOk be value is not high
```

Extension predicates can use the same shape:

```ges
let visible be unit is :sensor.visible
```

### Domain Phrases

The parser accepts several readable comparison forms:

```ges
value is at least 10
value is at most 10
value is 10 or less
value is 10 or more
value is empty
value has value
```

### Prefix Helpers

```ges
:len items
:chance 25%
:keys map
:values map
:entries map
:abs value
:ln value
:sqrt value
√ value
:cbrt value
∛ value
:clamp value between 0 and 100
:min of a and b and c
:max of a and b and c
```

`:keys`, `:values`, and `:entries` are defined only for maps and
map-backed custom type values. `:keys` returns a list of tag keys, `:values`
returns the corresponding values, and `:entries` returns maps with `key` and
`value` fields. All three projections use stable ordinal key order. If the
operand is `nothing`, the result is `nothing`; if the operand is any other
non-map value, the result is also `nothing`.

Square and cube roots lower to powers with exponents `0.5` and `1/3`.

### Constants

Numeric constants are tag-like literals:

```ges
:infinity
:negativeinfinity
:pi      // alias: ∏
:e       // aliases: :euler, ℇ
:tau     // alias: τ
:phi     // alias: φ
```

## Types and Values

### Built-in Types

The source language recognizes these built-in type tags:

- `:nothing`
- `:boolean`
- `:number`
- `:percentage`
- `:quantity(m)`, `:quantity(meter)`, `:quantity(s)`, `:quantity(second)`, `:quantity(degree)`, `:quantity(°)`
- `:vector`
- `:point`
- `:series`
- `:range`
- `:message`
- `:handler`
- `:envelope`
- `:tag`
- `:text`
- `:list`
- `:map`
- `:dice`

Custom record types are also type tags:

```ges
record :unit as {
  hp: :number,
  name: :text
}
```

### Numbers

`:number` is the source-level numeric type. Runtime values are represented as
integer when a finite result is exactly integral and fits signed 64-bit;
otherwise they are represented as IEEE 754 double precision. Equality uses
normal IEEE equality; approximate equality uses `=~`, `≈`, or `≅`.

Numeric operations distinguish absence from invalid mathematics. If any operand
of a numeric operation is `nothing`, the result is `nothing`. When all operands
are present but the operation cannot be computed as valid mathematics, the
result is numeric `NaN`.

Examples of invalid mathematics include non-numeric operands in numeric
operators, incompatible quantity units, invalid vector or point arithmetic, and
`mod`/`rem` by zero. Scalar `/` follows IEEE floating-point behavior, so
division by zero can produce `Infinity`, `-Infinity`, or `NaN`.

Vector and point arithmetic is intentionally not symmetric. Vectors support
`vector + vector`, `vector - vector`, `vector * scalar`, `scalar * vector`, and
`vector / scalar` when units are compatible and the scalar is computable.
Points support affine operations only: `point + vector` and `point - vector`
produce points, while `point - point` produces a vector. Point scaling is not
valid mathematics: `point * scalar`, `scalar * point`, `point / scalar`,
`scalar / point`, and point operands in `div`, `mod`, or `rem` produce numeric
`NaN` unless one operand is `nothing`, in which case the result is `nothing`.
Unary negation follows the same model: `-vector` negates each component, while
`-point` is invalid mathematics and produces numeric `NaN`.

Power with quantity units is intentionally narrow because compound units such
as `m²` are not represented. A quantity may be raised only to a unitless
numeric exponent of `0` or `1`; booleans participate in the normal lenient
numeric coercion, so `10m ^ true` is `10m` and `10m ^ false` is unitless `1`.
Other quantity powers produce numeric `NaN` unless an operand is `nothing`.

Numeric checks are keyword constructs, not `:` type tags:

```ges
value is numeric
value is integer
value is fractional
```

`:integer` and `:float` are not built-in type tags. In expression positions they
are ordinary tags.

`:abs` preserves the operand's numeric family for percentages and quantities:
absolute percentages remain `:percentage`, and absolute quantities keep their
unit. Finite numeric results that are exactly integral are represented as
integer values.

### Quantities

Quantities attach a numeric unit to an integer or float:

```ges
let distance be 10m
let duration be 2s
let angle be 90°

let a be 100 as :quantity(m)
let b be 90 as :quantity(°)
```

`:` `percentage` is not a quantity unit; it is a separate value kind. The names
`:meter`, `:second`, `:degree`, and `:seconds` are ordinary free tags, not
quantity type aliases. Use `:quantity(m)`, `:quantity(s)`, or
`:quantity(°)`/`:quantity(degree)` for casts and checks.

### Percentages

A percentage literal stores a ratio:

```ges
25%       // ratio 0.25
100%      // ratio 1.0
```

Percentages can be combined with percentages. For value-plus-percentage forms,
the non-percentage value must be on the left:

```ges
10m + 50%     // 15m
10m - 50%     // 5m
50% + 10m     // invalid numeric result
```

In multiplication with a non-percentage scalar or quantity, percentages behave
as their stored ratio and the result follows the other operand's value family:
`10% * 10` and `10 * 10%` both produce numeric `1`, while `10% * 10m` and
`10m * 10%` both produce `1m`.

### Text and Tags

Text is quoted. Tags are lowercase symbolic values:

```ges
let name be 'Scout'
let state be :active
```

Tags are not empty. Tags and text can be used as map keys and member lookup
selectors.

### Vector and Point

Vectors and points have `x`, `y`, and `z` numeric components plus an optional
unit.

```ges
let v be :vector(x: 1, y: 2, z: 0)
let p be :point(x: 10m, y: 20m, z: 0m)

let x be p.x
```

Vectors describe deltas and may be scaled by scalar numeric values. Points
describe positions and may only be translated by vectors or subtracted from
points; points are not scalar-multiplied or scalar-divided.

### Range

Ranges are finite integer ranges:

```ges
let r be from 1 to 10
let stepped be from 10 to 0 step -2
```

### Series

Series are deterministic numeric series. They support `:term`, `:take`, and
`:drop` selectors.

```ges
let fib be :series.fibonacci
let fifth be fib[:term 5]
let later be fib[:drop first 3]
let sample be fib[:take first 5]
```

Standard series:

- `:series.fibonacci`
- `:series.factorial`
- `:series.natural`
- `:series.natural(start: n)`
- `:series.natural(start: n, step: s)`

### Message and Handler Values

Messages can be values when created with labeled arguments:

```ges
let msg be Damage(target: unit, amount: 10)
```

Handler values are created with message-style parameter declarations:

```ges
let callback be Done(value)
```

### Presence and Emptiness

`nothing` is the absence value. NaN is not a DSL value or special tag. If an
internal numeric operation produces NaN, the script-visible value behaves as
`nothing`. The spelling `:nan` is an ordinary tag with no numeric meaning.

`empty` is true for:

- `nothing`
- empty text
- empty lists, maps, dice, and ranges

It is false for ordinary values, including `0`, `false`, `Infinity`, and
`-Infinity`.

`has value` is the exact complement of `empty`.

`:default` uses the same presence semantics as `has value`:

```ges
let displayName be unit.name :default 'Unknown'
```

## Collections and Selectors

### Lists

Lists preserve order and can contain mixed values:

```ges
let values be [10, 'hello', [1, 2, 3]]
let alsoValues be of 1 and 2 and 3
```

Generated lists use a selector-like form:

```ges
let squares be :list[:select x from 1 to 5 => x * x]
let evens be :list[:select x from 1 to 10 where x mod 2 = 0 => x]
let names be :list[:select unit in units => unit.name]
```

### Maps

Maps are keyed by identifier names in literals. Key-only entries default to
`true`.

```ges
let emptyMap be [:]
let unit be [name: 'Ada', hp: 10]
let flags be [enemy:, visible:, armed:]
```

Lookups use member syntax or bracket syntax. Literal text and tag selectors are
member lookups; dynamic selectors are resolved at runtime.

```ges
unit.name
unit['name']
unit[:name]
```

Dynamic map lookup with a non-text and non-tag key returns `nothing`.

### Lookup and Membership

`x[y]` performs positional index lookup for integer selectors and member lookup
for text/tag selectors unless `y` starts with a structured selector. Positional
indexes are 1-based.

```ges
let firstItem be items[1]
let hp be unit[:hp]
let hasEnemyFlag be :enemy in flags
let containsUnit be unit value in units
```

### Streamable Selectors

Selectors operate on lists, ranges, dice, and other enumerable values.

```ges
units[:filter unit where unit.hp > 0]
units[:select unit => unit.name]
units[:any unit where unit.hp <= 0]
units[:all unit where unit.hp > 0]
units[:count unit where unit.hp > 0]
```

`=>` is the projection arrow. `↦` is an alias.

### Aggregates

```ges
units[:sum unit => unit.hp]
units[:average unit => unit.hp]
units[:min unit => unit.hp]
units[:max unit => unit.hp]
units[:highest unit => unit.hp]
units[:lowest unit => unit.hp]
```

`min`/`max` and `highest`/`lowest` return the winning source item, selected by
the projection.

### First, Last, and Single

```ges
units[:first]
units[:last]
units[:single unit where unit.id = targetId]
units[:first unit where unit.hp > 0]
```

### Sorting, Distinct, and Grouping

```ges
units[:sort ascending]
units[:sort descending]
units[:order by unit => unit.hp ascending]
units[:distinct]
units[:distinct by unit => unit.kind]
units[:group by unit => unit.team]
```

### Map Selector

The `:map` selector builds a map. Without a value projection, the selected item
is used as the value.

```ges
let byName be units[:map unit by unit.name]
let hpByName be units[:map unit by unit.name => unit.hp]
```

### Contains and Boundaries

```ges
items[:contains target]
items[:contains any candidates]
items[:contains all required]

text starts with 'A'
text ends with 'Z'
```

### Slice, Shuffle, Draw, and Choose

```ges
items[:take first 3]
items[:take last 3]
items[:take highest 2]
items[:drop lowest 1]
items[:shuffle]
items[:draw 2]
items[:choose 1 at random]
items[:choose 3 unit where unit.hp > 0 weighted by unit => unit.priority]
```

### Dice Patterns and Object Matching

Dice and dice-like collections support pattern selectors:

```ges
roll[:has pair]
roll[:has three of a kind]
roll[:has full house]
roll[:has straight]
roll[:take pair]
```

Object matching checks map-like structures:

```ges
units[:has [hp: 10]]
units[:has [position: [x: 1, y: 2]]]
```

### Collection Operators

Collection-level binary operators:

```ges
a :combine b
a :merge b
a :intersect b
a :except b
a :zip b
```

## Randomness, Dice, and Series

Random values are drawn from the runtime random generator:

```ges
let roll be :random from 1 to 6
let chance be :chance 25%
```

Dice rolls use `:dice NdM` and produce sorted dice values:

```ges
let roll be :dice 5d6
let hasFullHouse be roll[:has full house]
```

Series are deterministic and numeric. Use selectors to read or derive finite
samples from them.

## Extensions

Extension references use `:extension.function`.

Supported call forms:

```ges
:integer.floor value
:integer.ceil(value)
:integer.halfEven of value
:degree.wrap value
:series.natural(start: 1, step: 2)
```

Standard extensions:

- `:integer.floor`
- `:integer.ceil`
- `:integer.truncate`
- `:integer.halfEven`
- `:integer.halfUp`
- `:integer.halfDown`
- `:degree.wrap`
- `:degree.toRadians`
- `:degree.fromRadians`
- `:series.fibonacci`
- `:series.factorial`
- `:series.natural`

Host-provided extensions can be linked by the host extension registry.

## Records and Custom Types

Records are immutable map-like values with declared fields:

```ges
record :gauge as {
  current: :number clamped between 0 and maximum,
  maximum: :number clamped between 0 and :infinity,
  percentage: :percentage computed by
    0% when maximum <= 0,
    otherwise (current / maximum) as :percentage
}

let hp be :gauge(current: 25, maximum: 100)
```

Field constraints:

- A field type casts the input value.
- `clamped between min and max` clamps numeric values.
- `computed by expression` derives the field from other fields.

Custom type checks use `is :typeName`.

## Runtime and Host API

The host compiles scripts through `GameEventScriptBuilder` or
`GameEventScriptManager`, loads the compiled result into a host, and publishes
messages to completion.

Core host concepts:

- `GameEventScriptCompiled`: compiled module data.
- `GameEventScriptBinary`: portable binary-oriented representation.
- `GameEventScriptHost`: dispatch host with local queue and publish hook.
- `GameEventScriptSession`: mutable runtime state created from a loaded host;
  it owns the active queue, context, random stream, runtime budget and active
  fibers.
- `GameEventScriptRuntimeLimits`: execution, loop, range, dice, and queue limits.
- `GameEventScriptDiagnosticTraceCollector`: optional diagnostics collector.

The runtime resolves handlers by signature and tag filters. Exact-signature
handlers and envelope handlers can both observe the same message. A loaded host
creates sessions with `StartSession()`. Published messages are sent through the
host publish hook; emitted messages stay in the session dispatch queue. The host
also exposes convenience publish/update methods for existing integrations.

## Errors and Limits

Compilation validates:

- naming conventions
- duplicate definitions and duplicate parameters
- callable arity
- predicate return type (`:boolean` or `:nothing`)
- handler envelope shape
- record field definitions
- seeded random seed type
- removed or unknown built-in type forms

Runtime limits cover:

- maximum processed events
- maximum queued messages
- maximum execution steps
- maximum loop iterations
- maximum call depth
- maximum range items
- maximum generated collection items
- maximum dice count and dice sides

Runtime failures prefer safe values (`nothing` or invalid numeric values) over
host exceptions where the language defines a lenient result.

## Practical Example

```ges
module Robot

record :scan as {
  distance: :quantity(m),
  angle: :quantity(°),
  strength: :percentage
}

predicate close(_ scan) means scan.distance < 30m
predicate strong(_ scan) means scan.strength > 50%

on Tick {
  publish Scan
}

on Scanner(data as :scan) {
  if data is close and data is strong {
    publish Fire
  } else {
    publish Turn(angle: 15°)
  }
}

on undeliverable as envelope {
  emit Heard(message: envelope.message, tags: envelope.tags)
}
```

## Grammar

The formal grammar is maintained in [GameEventScript.bnf](GameEventScript.bnf).
