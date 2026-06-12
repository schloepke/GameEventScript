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
- `on Message as message { ... }`
- `on initialization { ... }`
- `on undeliverable as message { ... }`

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

- Identifiers start lowercase and contain letters. They may have one optional
  final numeric suffix written as `_` followed by digits, such as `target_2`.
  Underscores are otherwise not part of identifiers: `target2` and
  `target_name` are invalid identifiers.
- Identifiers must resolve to a local, parameter, capture, or callable visible
  at compile time. Unknown identifiers are compile errors; they do not evaluate
  to `nothing`.
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

Message-name handlers match by message name and tags, but receive one
`:message` value instead of normal message arguments:

```ges
on Hit as message matching :enemy {
  emit Heard(name: message.name, tags: message.tags)
}
```

The message value contains its name, signature string, argument map, and tags.
It exposes the read-only members `name`, `signature`, `arguments`, and `tags`.

`undeliverable` is a system endpoint. It receives messages that were not
otherwise dispatched, and it must use message binding:

`initialization` is a parameterless system endpoint. It is queued when a host
session starts and runs before later messages in that session:

```ges
on initialization {
  emit Ready
}
```

```ges
on undeliverable as message {
  emit Unknown(message: message)
}
```

Handlers can filter tags:

```ges
on Radio as message matching :open, :enemy without :encrypted {
  emit Intercepted(message: message)
}
```

`matching` requires all listed tags. `without` rejects messages containing any
listed tag. The message still contains all original tags.

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
9. Equality: `=`, `<>`, `≠`
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

`:len` counts text and tag characters by raw text, so `:len :active` is `6`.

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
- `:tag`
- `:text`
- `:list`
- `:map`
- `:dice`

### Casts

An explicit `as :type` cast reshapes a present value when the target type has a
defined representation for it. A cast that would create a syntactically invalid
value writes `nothing` instead of manufacturing a malformed value.

Tag casts are strict. A runtime tag name is valid only when it matches the same
shape as source tag names: the first character must be a lowercase letter and
all following characters must be letters. Empty tags, numeric text, unit text,
punctuation, whitespace, brackets, colons inside the value, and underscores are
invalid tag names. `nothing as :tag` is `nothing`; numeric values and quantities
cast to `:tag` as `nothing`; formatted vector, point, list, map, dice, and range
text also cast to `:tag` as `nothing` because those strings are not valid tag
names. Existing valid tags remain unchanged. Text casts to `:tag` only when the
text is a valid tag name; the text values `'true'`, `'True'`, `'false'`, and
`'False'` are normalized to `:true` and `:false`. Boolean casts to `:tag` also
write `:true` or `:false`.

Text casts are formatting casts and keep using the value's text representation;
they do not require the formatted text to be a valid tag. Numeric text is parsed
only by an explicit `as :number` cast. Invalid numeric text casts to `nothing`.

Vector and point casts are structural conversions. A vector can be cast to a
point by copying `x`, `y`, `z`, and the optional unit; a point can be cast to a
vector the same way. This is a type reshape, not vector/point arithmetic.

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
otherwise they are represented as IEEE 754 double precision. Equality uses a
strict numeric comparison for integer-only values and a two-ULP tolerant
comparison wherever double precision values participate.

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

Numeric checks and implicit numeric views use the following rules. A runtime
value has an `AsNumeric` value exactly when `is numeric` is true. The explicit
`as :number` cast uses the same numeric view, except that it may additionally
parse text as described below.

| Runtime value | `is numeric` | `is integer` | `is fractional` | Numeric view |
| --- | --- | --- | --- | --- |
| `:nothing` | false | false | false | none |
| `:boolean` | true | true | false | `false` = `0`, `true` = `1` |
| integer number | true | true | false | integer value, including quantity unit |
| float number | true | true when finite and exactly integral; otherwise false | true when finite and non-integral | float value, including quantity unit |
| percentage | true | true when the stored ratio is finite and exactly integral; otherwise false | true when finite and non-integral | stored ratio |
| numeric tag constant (`:true`, `:false`, `:pi`, `:e`, `:tau`, `:phi`, `:infinity`, `:negativeinfinity`) | true | true only for finite integral constants | true only for finite non-integral constants | constant value |
| dice | true | true | false | sum of rolls |
| text, list, map, range, vector, point, message, handler, series, custom values, non-numeric tags | false | false | false | none |

Text values are not numeric for implicit mathematics or numeric checks:
`'100' is numeric` is false, and `'100' + 200` is text concatenation. An
explicit `as :number` cast parses text with invariant numeric syntax; invalid
text casts to `nothing`. Series are also not numeric for implicit mathematics
or numeric checks, but an explicit `as :number` cast reads the first term and
casts that term to a number.

Dice have a numeric view for numeric checks, explicit numeric casts, equality,
and numeric comparison: `dice is numeric` and `dice is integer` are true, and
`dice as :number` is the sum of the rolls. Dice-specific collection operations
keep priority, so `dice + integer` adds a roll and `dice - integer` removes a
roll instead of using the dice sum.

### Equality

Equality operators are `=` and `<>`/`≠`. If either operand is `nothing`, or an invalid numeric result that is
observed as `nothing`, the equality result is `nothing`; `nothing = nothing`
therefore produces `nothing`, not `true`.

For present operands, exact equality first tries the same numeric view used by
numeric operators. Numeric values, percentages, booleans (`false` = `0`,
`true` = `1`), dice sums, and numeric tag constants such as `:pi`,
`:infinity`, and `:negativeinfinity` compare by numeric value when both
operands have a numeric view. Quantity units must match exactly; unitless and
unit-bearing values are not equal, including integer fast paths such as
`10 = 10m`, which is `false`. Internal `NaN` numeric values are never equal.
When either numeric side is represented as double precision, finite values
compare equal when their IEEE 754 values are within two ULPs. This handles
binary floating-point artifacts such as `0.1 + 0.2 = 0.3` without adding a
separate approximate-equality operator. Infinities compare equal only when they
have the same sign.
Text is not numeric for equality, so `'10.3' = 10.3` is false while
`('10.3' as :number) = 10.3` is true.

If the numeric view does not apply, exact equality requires the same value kind:
tags and text compare ordinal text, vectors and points compare `x`, `y`, `z`,
and unit, ranges compare `from`, `to`, and `step`, series compare signature and
offset, messages compare signature id, arguments, and tag sequence, handlers
compare signature id, lists compare ordered items, dice compare ordered rolls,
and maps/records/custom map-like values compare their visible key/value pairs.
Hidden map fields such as record type markers do not participate in map
equality.

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

Tags are not empty. Tags are text-like symbolic values: they use the same raw
text as quoted text values, but are written without quotes and must follow the
tag naming rules. Text and tags can be used interchangeably for map keys, member
lookup selectors, text containment, and text boundary operations.

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

Series are deterministic numeric series. They support zero-based `:term` lookup
and forward `:take first` / `:drop first` selectors. Because series are not
finite, `:take last` and `:drop last` evaluate to `nothing` for series.

```ges
let fib be :series.fibonacci
let fifth be fib[:term 5]
let later be fib[:drop first 3]
let sample be fib[:take first 5]
```

`:term` is a series-only selector. Lists, dice, ranges, and all other values
return `nothing` for `[:term n]`. Direct `:take first`, `:drop first`, `:take
last`, and `:drop last` selectors are defined for finite lists, dice, and
ranges; range slices stay ranges instead of being materialized as lists.

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

`x in y` checks membership in `y`. Text and tags use ordinal substring
matching over their raw text, without a leading `:` for tags. Lists and dice
check whether one item equals `x`. Ranges check whether numeric `x` is one of
the range terms. Maps and map-like values check whether text/tag `x` is a
visible key; non-text keys are false. Vector and point values check their
numeric components.

`x value in y` checks visible values of map-like `y`. It is defined for maps,
records/custom values, external map-like values, vectors, and points. For maps
and records it compares only visible, non-hidden fields. For vectors and points
it compares the `x`, `y`, and `z` components. `nothing value in y` follows the
same comparison rule; `x value in nothing` is `nothing`. Lists, dice, ranges,
text, tags, and scalar values are not value-membership containers and return
`false`.

`x starts with y` and `x ends with y` are boundary checks. Text and tags compare
raw text with ordinal rules. Lists, dice, and ranges compare sequence prefixes
or suffixes; the right operand must also be a list, dice, or range. Empty
right-hand sequences match. If the left sequence is shorter than the right
sequence, the result is `false`. `nothing starts with y` and
`nothing ends with y` produce `nothing`; other unsupported shapes return
`false`.

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

Without a filter, `:first`, `:last`, and `:single` are direct terminals for
lists, dice, ranges, maps, custom map-backed values, text, tags, and streams.
Empty, unsupported, or invalid sources yield `nothing`; `:single` also yields
`nothing` when more than one element is present.

### Sorting, Distinct, and Grouping

```ges
units[:sort ascending]
units[:sort descending]
units[:order by unit => unit.hp ascending]
units[:distinct]
units[:distinct by unit => unit.kind]
units[:group by unit => unit.team]
```

`:sort ascending` and `:sort descending` are defined for lists, dice, ranges,
and streams. Lists and streams materialize sorted lists. Dice also materialize a
list so the requested order is preserved instead of being normalized back into
dice order. Ranges stay ranges when the requested direction can be represented
by swapping the range bounds and negating the step. Maps are already
key-canonical and are not sort targets; sorting a map yields `nothing`.

`:order by` is defined for lists and streams only. It orders the original items
by the projected key and materializes a list. Dice, ranges, maps, scalars, and
`nothing` yield `nothing` for `:order by` because projecting a sort key over
those direct values is not a meaningful collection operation.

`:distinct` is defined for lists, dice, and streams. Lists keep their first
occurrence order, dice keep their dice result type, and streams materialize a
list. `:distinct by` is defined only for lists and streams because the
projection operates on structured items. Dice and other non-list values yield
`nothing` for `:distinct by`.

`:group by` is defined for lists, maps, and streams. Lists and streams group
their source items by the projected key and return a map from the projected key
text to a list of matching source items. Maps group their visible values in
stable key order. Direct dice, ranges, text, tags, scalars, and `nothing` yield
`nothing`; dice and ranges are scalar-like direct values for grouping and must
be streamed explicitly when per-element grouping is wanted.

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
:active starts with 'act'
'active' ends with :ive
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

`:draw 1` is equivalent to `:first`; `:draw n` with `n > 1` is equivalent to
`:take first n`. Deterministic `:choose` follows the same rule:
`:choose 1` is equivalent to `:first`, and `:choose n` with `n > 1` is
equivalent to `:take first n`. Random choice uses random selection without
replacement: `:choose 1 at random` returns one item or `nothing`, while
`:choose n at random` returns up to `n` randomly selected items. Weighted
choice evaluates the `weighted by` expression per candidate and chooses without
replacement from positive finite weights. `:choose 1 weighted by ...` returns
one item or `nothing`; `:choose n weighted by ...` returns a list.

`:reverse` is defined for lists, dice, ranges, and streams. Lists reverse into
lists. Dice reverse into lists so the requested order is preserved instead of
being normalized back into dice order. Ranges reverse into ranges by swapping
the effective bounds and negating the step. Streams materialize reversed lists.
Maps, scalars, and `nothing` yield `nothing`.

`:shuffle` is defined for lists, dice, ranges, and streams and always
materializes a list. This allows game-oriented cases such as shuffling a card
range with `from 1 to 32[:shuffle]`. Maps, scalars, and `nothing` yield
`nothing`.

### Dice Patterns and Object Matching

Dice and dice-like collections support pattern selectors:

```ges
roll[:has pair]
roll[:has three of a kind]
roll[:has pair of 6]
cards[:has four of 'King']
roll[:has full house]
roll[:has straight]
roll[:take pair]
```

Pattern selectors are defined for dice and lists. Other values are not pattern
sequences: `:has ...` yields `false`, while `:take ...` yields `nothing`.

Count patterns use normal value equality. `pair` means at least two equal
values. `three/four/five/six/seven of a kind` mean at least that many equal
values. `... of expression` checks the concrete evaluated face/value instead.

`full house` is exact: the sequence must contain exactly two distinct values
with counts `3` and `2`. For `:take full house`, the first triple in source
order and then the first matching pair in source order are returned.

`straight` is numeric/integer based. It coerces each item to its integer value,
ignores duplicate integers, sorts the unique integers descending, and requires
the whole unique sequence to be consecutive with no gaps. This is intended for
dice and numeric dice-like lists today; non-numeric card names need an explicit
future value model before they can express rank-based straights reliably.

`:take ...` returns dice for dice sources and lists for list sources.

Object matching checks map-like structures:

```ges
units[:has [hp: 10]]
units[:has [position: [x: 1, y: 2]]]
```

### Collection Operators

Collection-level binary operators:

```ges
a | b
a & b
a :zip b
```

Collection addition uses `+` for single-value insertion. `list + any` appends
exactly one value and `any + list` prepends exactly one value, so
`[1, 2] + [3, 4]` becomes `[1, 2, [3, 4]]`. Dice values preserve dice semantics
only for unitless positive integers: `dice + integer` and `integer + dice`
insert one roll and return sorted dice. Other dice/scalar additions produce
`nothing`.

Collection union uses `|`. `list | list` combines both lists. `list | dice` and
`dice | list` produce a list; `dice | dice` produces sorted dice. `map | map`
merges keys and values with right-hand keys replacing left-hand keys.
`map | listOfKeys` adds missing keys as flag entries with value `true` and
keeps existing values. Other operand combinations produce `nothing`.

Collection intersection uses `&`. `list & list`, `list & dice`, and
`dice & list` use multiset semantics and produce a list. `dice & dice` produces
sorted dice. `map & map` keeps keys present in both maps and values from the
left map. `map & listOfKeys` keeps only listed keys. `dice & integer` and
`integer & dice` are not defined; write `dice & :dice([integer])` when a
single-roll dice intersection is intended.

Collection subtraction uses `-`. `list - scalar` removes one matching item, and
`list - list` and `list - dice` remove matching items with multiset semantics
and produce a list. `dice - integer` removes one roll and returns dice,
`dice - dice` performs multiset subtraction and returns dice, and `dice - list`
returns a list. `scalar - list` and `list - map` produce `nothing`. `map - map`
removes keys present in the right map. `map - tag`, `map - text`, and
`map - listOfKeys` remove matching keys. Other unsupported operand
combinations produce `nothing`.

`:zip` is defined for lists only. It pairs items by index up to the shorter
operand length and returns a list of maps with `left` and `right` entries.
Other operand combinations produce `nothing`.

`listOfKeys` means a list containing only text or tag values. A key list
containing any other value produces `nothing`.

`+` concatenates text when either operand is text. The non-text operand is
formatted with the same text representation used by `as :text`, so
`'100' + '200'` is `'100200'` and `'hp: ' + 10` is `'hp: 10'`. `:combine`,
`:merge`, `:except`, and `:intersect` are not reserved collection operators; in
expression position they are ordinary tags.

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
- Computed fields are not constructor parameters and cannot be set by record
  construction.
- Prefixing a non-computed field with `_` makes that constructor parameter
  positional/unlabeled while keeping the field name for member access:

```ges
record :super as {
  _ xValue: :number clamped between 1 and 10,
  yValue: :number,
  zValue: :number computed by xValue * yValue + 10%
}

let rec be :super(10, yValue: 10)
```

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

The runtime resolves handlers by signature/name and tag filters. Exact-signature
handlers and message-name handlers can both observe the same message. A loaded host
creates sessions with `StartSession()`. Published messages are sent through the
host publish hook; emitted messages stay in the session dispatch queue. The host
also exposes convenience publish/update methods for existing integrations.

## Errors and Limits

Compilation validates:

- naming conventions
- duplicate definitions and duplicate parameters
- callable arity
- predicate return type (`:boolean` or `:nothing`)
- handler message-name shape
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

on undeliverable as message {
  emit Heard(message: message, tags: message.tags)
}
```

## Grammar

The formal grammar is maintained in [GameEventScript.bnf](GameEventScript.bnf).
