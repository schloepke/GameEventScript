# Game Event Script language specification

Game Event Script (GES) is an event-oriented scripting language for deterministic
game logic. A source declares records, predicates, functions, and message
handlers. Handler execution can enqueue local messages or publish messages to
the host's outbound boundary.

This document is the normative language definition. It specifies accepted source
text, declarations, static semantics, evaluation, and script-visible values.
Exact Unicode rules belong to [Text semantics](Semantics/Text.md), numeric rules
to [Number semantics](Semantics/Numbers.md), and seeded randomness, iteration,
equality, and stable ordering to [Determinism](Semantics/Determinism.md). Host
loading, scheduling, queues, sinks, and observation belong to
[Host runtime](HostRuntime.md). Bytecode lowering is not part of source-language
meaning and is specified separately in [Bytecode](Bytecode.md).

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
- Static errors and execution limits
- Normative grammar

## Program Structure

A compilation unit consists of one or more source documents. Each source contains
an optional module declaration followed by top-level declarations:

```ges
module Battle

record :unit as {
  hp: :number clamped between 0 and maxHp,
  maxHp: :number
}

predicate alive(_ unit) be unit.hp > 0
function missingHp(_ unit) be unit.maxHp - unit.hp

on Damage(unit, amount) {
  if unit is alive {
    emit Damaged(unit: unit, amount: amount)
  }
}
```

Top-level declarations are:

- `record :name as { ... }`
- `predicate name(...) be expression`
- `function name(...) be expression`
- `on Message(...) { ... }`
- `on Message as localName { ... }`
- `on initialization { ... }`
- `on undeliverable as localName { ... }`

The module declaration is metadata and does not create a namespace. Definitions
from every source in one compilation unit share compilation-unit scope and may
refer to one another independent of source order. Functions and predicates share
one callable namespace. Records use a separate type namespace because their
source references are prefixed with `:`. Multiple handlers for the same endpoint
are allowed and retain their source registration order. When no non-empty module
name is supplied, the compiler derives a deterministic anonymous Program name
from the complete compiler output. Module identity does not change expression
semantics.

There are no global variables or mutable script storage. Each handler or callable
invocation receives fresh parameters and locals. State that outlives a handler
exists only through messages or host-provided facilities outside this language.

### Functions and predicates

Functions and predicates are immutable, globally named expression callables:

```ges
function damage(base, bonus as :number) be base + bonus
predicate alive(_ unit) be unit.hp > 0
```

Callable identity is the callable name plus its ordered external argument
labels. Functions may therefore overload a name when at least one label or the
arity differs:

```ges
function takeDamage(unit, enemy) be unit.hp - enemy.damage
function takeDamage(unit, collision) be unit.hp - collision.force
```

The calls `takeDamage(unit: hero, enemy: attacker)` and
`takeDamage(unit: hero, collision: impact)` select those two signatures
respectively. Parameter local names and declared types are not part of identity,
and there is no overload resolution by type. In particular, two `_` parameters
with different local names or types still have the same external label `_`.

Functions and predicates share one name namespace: if any function named
`takeDamage` exists, no predicate named `takeDamage` may exist, regardless of
either declaration's signature. Within one kind, duplicate signatures are
static errors.

Record identity is likewise the custom type name alone. A record cannot be
overloaded by changing its fields or constructor labels, and a script record
cannot use the name of a configured external type. The type namespace is
separate from the callable namespace, so `record :damage ...` and `function
damage(...) ...` may coexist.

A parameter written `label` has both the external argument label and local name
`label`. `_ localName` declares an unlabeled argument with the indicated local
name. Parameters are evaluated and bound left to right. A declared parameter
type casts the supplied value before evaluation of the body; a failed cast binds
`nothing`. Calls must match the declaration's ordered external labels exactly.
Thus `function f(x) ...` is called as `f(x: 10)`, while `function f(_ x) ...` is
called positionally as `f(10)`. Duplicate local parameter names and duplicate
non-`_` labels are static errors.

A function may return any value. A predicate must statically produce a boolean
or `nothing`; predicate results are normalized to that three-state result.
`value is predicateName` is shorthand for invoking the unique unary overload of
that predicate name. The shorthand has no argument label with which to select
between multiple unary overloads, so such a family is ambiguous and rejected;
an explicit ordinary call selects a predicate by its full signature. Every call
receives a fresh frame. The synchronous call graph must be acyclic: direct and
indirect recursion are not part of the
language.

## Lexical Rules

ASCII space and tab separate tokens. `LF`, `CRLF`, `CR`, and semicolons separate
statements where the grammar permits a separator. Line comments start with `//`
and continue up to, but do not consume, the next logical newline.

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
- Tags start with `#` and use the identifier grammar, including its optional
  numeric suffix. `#true`, `#false`, `#pi`,
  and `#infinity` are ordinary tag values.
- Custom type names start lowercase and contain ASCII letters only; they do not
  accept numeric suffixes. Type references, selectors, generated collection
  forms, and namespace-like extension references use `:`. Built-in type names
  are reserved by the language.

All of these name grammars use ASCII letters and digits; they do not depend on
platform Unicode classification. Source is strict UTF-8, an optional initial BOM
is removed, and only ASCII space/tab plus `LF`, `CRLF`, or `CR` are portable
whitespace/newlines. The complete normative contract is in
[Text semantics](Semantics/Text.md).

Text literals can use single or double quotes. The quote character is escaped by
doubling it:

```ges
let a be 'didn''t'
let b be "say ""hello"""
```

Text literals may contain logical newlines. Backslash has no escape meaning.
Tokens and keywords are case-sensitive. `of` is a reserved keyword token; it is
valid only in the grammar phrases that use it, including `min of`, `max of`,
extension argument lists, `in values of`, and dice patterns. It does not start a
list literal.

Numeric literals contain an integer part and an optional fractional part, use
`.` as decimal separator, and have no sign or exponent syntax; unary minus
supplies a negative sign. Single underscores may separate adjacent digits but
may not lead, trail, or repeat. A following `%`, `m`, `s`, or `°` forms a
percentage or unit literal without intervening whitespace:

```ges
let integer be 42
let groupedInteger be 1_000_000
let fraction be 12.5
let negative be -7             // unary minus followed by an integer literal
let probability be 25%         // percentage literal
let distance be 3.5m           // meter quantity
let duration be 2s             // second quantity
let angle be 90°               // degree quantity
```

## Messages and Dispatch

Messages have a name, ordered argument names, argument values, and tags. The
ordered argument names form the signature. An unlabeled argument uses `_`.
Argument order is observable; named arguments are not a dictionary. Duplicate
named labels are invalid, while multiple `_` positions are valid.

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

`on Ready` and `on Ready()` both declare the empty exact signature. A parameter
written `name` has signature label `name` and local name `name`. A parameter
written `_ localName` has signature label `_` and local name `localName`.
Parameter local names must be unique within the handler.

Parameters can declare a type. The runtime casts the incoming value before the
handler body runs. Failed casts produce `nothing`; they do not prevent dispatch:

```ges
on Move(unit, speed as :quantity(m)) {
  emit Moving(unit: unit, speed: speed)
}
```

Message-name handlers match every signature having the declared message name,
subject to their tag filters. They receive one `:message` value instead of the
message's individual arguments. The identifier following `as` is a freely chosen
local binding name; the word `message` is not required:

```ges
on Hit as incoming matching #enemy without #silent {
  emit Heard(name: incoming.name, tags: incoming.tags)
}
```

The message value contains its name, signature string, argument map, and tags.
It exposes the read-only members `name`, `signature`, `arguments`, and `tags`.
The message itself retains ordered arguments. Accessing `arguments` materializes
a map keyed by argument label and therefore follows map key ordering and
last-entry-wins behavior; repeated `_` positions are not individually addressable
through that map.

`initialization` is a parameterless system endpoint. Each `Load(program)` queues
it exactly once for the returned program instance. It is placed after messages
already waiting at load time and before messages received later:

```ges
on initialization {
  emit Ready
}
```

`undeliverable` is the fallback endpoint for a message that has no matching
ordinary handler after signature, name, and tag filters have been evaluated. It
must use the message-name binding form. The chosen local receives the original
message as a `:message` value, and filters on the fallback handler inspect the
original message's tags:

```ges
on undeliverable as rejectedMessage without #ignored {
  emit Unknown(message: rejectedMessage)
}
```

Both exact-signature handlers and message-name handlers can filter tags:

```ges
on Radio(channel, payload) matching #open, #enemy without #encrypted {
  emit ExactIntercept(channel: channel, payload: payload)
}

on Radio as incoming matching #open, #enemy without #encrypted {
  emit AnyRadioMessage(message: incoming)
}
```

`matching` requires all listed tags. `without` rejects messages containing any
listed tag. The clauses may appear in either order and may be repeated; all
`matching` tags are combined into one required set and all `without` tags into
one excluded set. Repeating a tag has the same meaning as listing it once. The
message still contains all original tags. `initialization` is the exception and
cannot use tag filters. Exact-signature and message-name handlers are both
eligible for the same message; dispatch ordering and undeliverable selection are
defined by [Host runtime](HostRuntime.md).

## Statements

### `emit` and `publish`

`emit` queues a message only in the local host. `publish` first queues the same
message locally and then offers it to the host's outbound publish sink.

```ges
emit Done
emit Done(value: 10)
emit Done(value: 10) with #combat, #visible

publish Fire(target: enemy)
publish Scan with [#radar, #active]
```

Tags after `with` can be tags or lists of tags.

The message operand may instead be any expression. It is emitted or published
only when its result is a `:message`; any other result produces no message. Tag
expressions are evaluated left to right. Lists are recursively flattened, each
remaining value is converted to its text form and normalized by the portable tag
grammar, empty tags are omitted, and later duplicates are omitted while retaining
first occurrence order. Applying `with` to an existing message replaces that
message's tags with the normalized clause result.

### `let`

`let` creates an immutable local binding:

```ges
let damage be base + bonus
let percent as :percentage be 25%
```

The optional declared type casts the value.

A name may be declared only once in one lexical scope, and a binding may not
reuse a name that is visible from an enclosing lexical scope. This no-shadowing
rule applies uniformly to locals, loop variables, generated-collection
identifiers, and selector identifiers. Parameters occupy the routine's outer
scope. The `if` and `else` blocks are sibling child scopes, so both may declare
the same name when that name does not exist in their common parent. There is no
assignment statement and a binding never changes after initialization.

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

A braced body creates a child lexical scope; an unbraced single statement uses
the surrounding scope. Only the value `true` enters the then-branch. `false` and
`nothing` select `else` or continue after the statement.

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

The iteration expression is evaluated once. Items follow the deterministic order
defined for their value kind. A non-iterable or `nothing` source performs zero
iterations. Each iteration assigns the next value to the immutable loop binding;
bindings created inside a braced loop body do not escape the iteration body.

### Expression statements

Any expression may be used as a statement. It is evaluated for its observable
effects, such as an extension invocation, and its resulting value is discarded.
An expression statement does not implicitly emit or publish a message.

### Seeded Random Scope

`random with` evaluates a body under a deterministic sub-random scope. The
seed must statically resolve to a unitless integer number; dynamic seeds should
be cast explicitly with `as :number`.

```ges
random with seed as :number {
  emit Roll(value: random from 1 to 6)
}

let value be random with 123 (random from 1 to 100)
```

Random scopes may be nested. Leaving a nested scope resumes its parent at the
parent generator's next value. Integer draws include both ordered bounds.
Binary64 draws use a `[0, 1)` unit source, although binary64 rounding can make
the scaled result equal the upper bound.

The portable seeded PRNG algorithm, bounded sampling rules, and known-answer
vectors are specified in
[Determinism](Semantics/Determinism.md).

## Expressions and Operators

Operands are evaluated from left to right except where short-circuiting omits an
operand. Binary operators at one precedence level associate left-to-right, except
implication and power. `and`, `or`, and implication short-circuit. `xor` always
evaluates both operands.

From high to low precedence:

1. Postfix: member access `x.y`, lookup/selector `x[...]`
2. Power: `^`, `²`, `³`
3. Unary: `-`, `not`, `!`, `~`, `¬`, `empty`, prefix intrinsics
4. Multiplicative: `*`, `/`, `div`, `mod`, `rem`
5. Additive: `+`, `-`
6. Relational: `<`, `>`, `<=`, `>=`, `≤`, `≥`
7. Type and predicate operations: `is`, `is not`, `as`
8. Membership and text boundaries: `in`, `∈`, `∉`, `in values of`, `starts with`, `ends with`
9. Equality: `=`, `<>`, `≠`
10. Boolean conjunction: `and`, `∧`
11. Boolean exclusive-or: `xor`, `⊕`
12. Boolean disjunction: `or`, `∨`
13. Collection zip: `zip`
14. Collection intersection: `&`
15. Collection union: `|`
16. Presence fallback: `default`
17. Implication: `->`, `→`, `⇒`
18. Guarded choice: `value when condition, otherwise fallback`

Power accepts at most one `^` or superscript suffix at each unparenthesized
power node; its right operand is unary, which makes `a ^ b ^ c` right-associative.
Parentheses override precedence. The fixed Unicode aliases are token aliases and
have exactly the semantics and precedence of their ASCII counterparts.

### Boolean Logic

Predicate declarations must statically evaluate to `:boolean` or `nothing`.
Use `as :boolean` when coercion is intentional. Predicate-call results, including
extension predicates used through `is`, are normalized to boolean or `nothing`.

Conditions and logical operators use a three-state truth view:

| Value | Truth view |
| --- | --- |
| `:boolean` | its boolean value |
| number, quantity, or percentage | false for numeric zero, true otherwise |
| text | true for ASCII-case-insensitive `true` or exact `1`; false otherwise |
| tag | false, including `#true` and `#false` |
| vector or point | false when every component is zero; true otherwise |
| `nothing`, list, map, record, range, dice, series, message, or handler | indeterminate |

Logical operators return `nothing` for an indeterminate result. Their complete
truth tables are:

| `a` | `b` | `a and b` | `a or b` | `a xor b` | `a -> b` |
| --- | --- | --- | --- | --- | --- |
| false | false | false | false | false | true |
| false | true | false | true | true | true |
| true | false | false | true | true | false |
| true | true | true | true | false | true |
| false | unknown | false | unknown | unknown | true |
| true | unknown | unknown | true | unknown | unknown |
| unknown | false | false | unknown | unknown | unknown |
| unknown | true | unknown | true | unknown | true |
| unknown | unknown | unknown | unknown | unknown | unknown |

`not` swaps true and false and returns `nothing` for unknown.

`if`, selector predicates, and guarded-choice conditions pass only for a true
truth view. False and indeterminate values do not pass.

Implication `A -> B` is right-associative. It is true when `A` is false or `B` is
true; it is false when `A` is true and `B` is false; unresolved cases produce
`nothing`.

### Guarded Choice

Guarded choices select the first value whose condition is true. `otherwise` is a
keyword.

```ges
let status be
  #dead when hp <= 0,
  #wounded when hp < maxHp,
  otherwise #healthy
```

Branch conditions are evaluated in source order. The first condition whose truth
view is true selects its associated value; false and indeterminate conditions are
skipped. If none is true, only the `otherwise` expression is evaluated.

### Predicate Sugar

Unary predicates can be used with `is`:

```ges
predicate high(_ value) be value > 50

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
value is less than 10
value is more than 10
value is 10 or less
value is 10 or more
value is empty
value has value
```

### Prefix Intrinsics and Selectors

```ges
items[:count]
chance 25%
map[:keys]
map[:values]
map[:entries]
abs value
ln value
exp value
sin value
cos value
tan value
asin value
acos value
atan value
sqrt value
√ value
cbrt value
∛ value
floor value
ceil(value)
truncate value
round half even value
round half up value
round half down value
rad degrees
deg radians
wrap degree value
clamp value between 0 and 100
min of a and b and c
max of a and b and c
```

`x[:count]` counts Unicode scalar values in text and raw tag text, so
`#active[:count]` is `6`.

`x[:keys]`, `x[:values]`, and `x[:entries]` are defined only for maps and
map-backed custom type values. `x[:keys]` returns a list of tag keys,
`x[:values]` returns the corresponding values, and `x[:entries]` returns maps
with `key` and `value` fields. All three projections use stable Unicode-scalar
key order. If the operand is `nothing`, the result is `nothing`; if the operand is
any other non-map value, the result is also `nothing`.

Square and cube roots lower to powers with exponents `0.5` and `1/3`. `exp`
is the natural exponential counterpart of `ln`.

Unary intrinsics accept either `operation expression` or
`operation(expression)`. `floor`, `ceil`, `truncate`, and the three `round`
forms return integers using [Number semantics](Semantics/Numbers.md). `rad`
converts degrees to unitless radians; `deg` converts unitless radians to a degree
quantity; `wrap degree` reduces a unitless or degree-valued angle to `[0°, 360°)`.
`chance` accepts a unitless percentage or numeric probability: integer inputs and
numeric magnitudes outside `[-1, 1]` are interpreted as percentages, values at or
below zero are false, values at or above one are true, and invalid, unit-bearing,
or non-finite inputs produce `nothing`.

`sin`, `cos`, and `tan` accept unitless radians or degree quantities and return a
unitless number. `asin` and `acos` accept unitless numbers in `[-1, 1]`; `atan`
accepts any unitless number. Invalid domains, incompatible units, unsupported
values, and `nothing` produce `nothing`. Transcendental portability follows
[Number semantics](Semantics/Numbers.md).

### Multi-argument navigation intrinsics

The following forms require parentheses and comma-separated arguments. Incorrect
arity is a compile error.

| Form | Accepted arities | Result |
| --- | ---: | --- |
| `atan2(y, x)` | 2 | unitless angle in radians; `(0, 0)` is `0` |
| `hypot(x, y)` / `hypot(x, y, z)` | 2 or 3 | Euclidean length retaining the common component unit |
| `distance(a, b)` | 2 | absolute scalar difference or distance between compatible vectors/points |
| `distance(x1, y1, x2, y2)` | 4 | two-dimensional distance |
| `distance(x1, y1, z1, x2, y2, z2)` | 6 | three-dimensional distance |
| `distance squared(...)` | 2, 4, or 6 | corresponding squared distance |
| `length squared(value)` | 1 | squared scalar magnitude or squared vector/point length |
| `length squared(x, y)` / `length squared(x, y, z)` | 2 or 3 | squared component length |
| `normalize(value)` | 1 | unitless vector from a vector or point |
| `normalize(x, y)` / `normalize(x, y, z)` | 2 or 3 | unitless normalized vector |
| `dot(a, b)` | 2 | dot product of compatible vectors/points |
| `dot(x1, y1, x2, y2)` | 4 | two-dimensional dot product |
| `dot(x1, y1, z1, x2, y2, z2)` | 6 | three-dimensional dot product |
| `cross(a, b)` | 2 | three-dimensional vector cross product |
| `cross(x1, y1, x2, y2)` | 4 | scalar two-dimensional cross product |
| `cross(x1, y1, z1, x2, y2, z2)` | 6 | three-dimensional vector cross product |
| `angle between(a, b)` | 2 | unitless angle in radians between compatible vectors/points |
| `angle between(x1, y1, x2, y2)` | 4 | two-dimensional angle |
| `angle between(x1, y1, z1, x2, y2, z2)` | 6 | three-dimensional angle |

Numeric component overloads require numeric operands with one common unit.
Object-pair overloads require matching spatial kinds and units. Dot, cross,
squared-distance, and squared-length results are unitless because the language
has no compound-unit representation. Normalizing a zero vector and computing an
angle with a zero-length operand produce `nothing`. Unsupported operand shapes,
incompatible units, and absent operands likewise produce `nothing`.

### Constants

Numeric constants are keywords, not tags:

```ges
infinity
0 - infinity
pi      // aliases: π, ∏
e       // alias: ℇ
tau     // alias: τ
```

Tags use `#name`. `#true`, `#false`, `#pi`, and `#infinity` are ordinary tag
values without boolean or numeric meaning. `:name` is reserved for selectors,
types, and namespace-like extension references.

## Types and Values

### Built-in Types

The source language recognizes these built-in types:

- `nothing`
- `:boolean`
- `:number`
- `:percentage`
- `:quantity(m)`, `:quantity(meter)`, `:quantity(s)`, `:quantity(second)`, `:quantity(degree)`
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

An explicit `as TypeReference` cast reshapes a value when the target type has a
defined representation. A cast that cannot produce that representation returns
`nothing`, except that collection casts deliberately produce the empty target for
unsupported scalar inputs as listed below. Built-in constructor syntax with one
unlabeled argument, such as `:text(value)` or `:dice(values)`, has the same
semantics as the corresponding cast. `:vector` and `:point` additionally have
component constructors.

| Target | Cast behavior |
| --- | --- |
| `nothing` | always `nothing` |
| `:boolean` | true exactly when the source truth view is true; false otherwise |
| `:number` | uses the numeric view, parses invariant numeric text, or reads series term zero; failure is `nothing` |
| `:percentage` | rejects units and non-numeric values; integers are percentages divided by 100, while finite fractional ratios in `[-1, 1]` remain ratios and other finite numeric magnitudes are divided by 100 |
| `:quantity(unit)` | applies the unit to numeric or spatial input that is unitless or already has that unit; a conflicting unit is invalid |
| `:text` | returns the canonical text representation of any value |
| `:tag` | follows the strict tag rules below |
| `:vector`, `:point` | follows the structural conversion rules below |
| `:list` | preserves lists; expands text/tags into Unicode scalars, spatial values into three components, dice into rolls, and ranges into terms; unsupported values become `[]` |
| `:map` | preserves maps; exposes record/external fields and spatial `x`, `y`, `z`; unsupported values become `[:]` |
| `:dice` | preserves dice; converts a list of positive Int32 integer rolls; a non-list becomes empty dice and an invalid list becomes `nothing` |
| `:range`, `:series`, `:message`, `:handler` | preserves the same runtime kind and returns `nothing` for another kind |
| custom record | preserves the same record type or invokes that record constructor from a map; other values become `nothing` |

Tag casts are strict. A runtime tag name is valid only when it matches the same
shape as `#` tag names: the first character must be a lowercase ASCII letter,
followed by ASCII letters and optionally one final `_` plus a canonical numeric
suffix. Empty tags, numeric text, unit text, punctuation, whitespace, brackets,
and colons inside the value are invalid tag names. `nothing as :tag` is
`nothing`; numeric values and quantities
cast to `:tag` as `nothing`; formatted vector, point, list, map, dice, and range
text also cast to `:tag` as `nothing` because those strings are not valid tag
names. Existing valid tags remain unchanged. Text casts to `:tag` only when the
text is a valid tag name; the text values `'true'`, `'True'`, `'false'`, and
`'False'` are normalized to `#true` and `#false`. Boolean casts to `:tag` also
write `#true` or `#false`.

Text casts are formatting casts and keep using the value's text representation;
they do not require the formatted text to be a valid tag. Numeric text is parsed
only by an explicit `as :number` cast. Invalid numeric text casts to `nothing`.

Vector and point casts are structural conversions. A vector can be cast to a
point by copying `x`, `y`, `z`, and the optional unit; a point can be cast to a
vector the same way. This is a type reshape, not vector/point arithmetic.

Spatial casts from a number, percentage, boolean, tag with a numeric view, dice,
list, range, map, or record read up to three components and fill missing
components with zero. List items and named `x`, `y`, `z` fields must be finite
numeric values. Direct numeric inputs become the `x` component. An invalid
component makes the whole result `nothing`.

### Type checks

`value is TypeReference` and `value is not TypeReference` test without converting.
`is :number` and `is numeric` use the same numeric-view test. `is integer` and
`is fractional` are numeric checks rather than type names. `is :map` is true for
plain maps and map-backed custom values. Quantity checks require the exact unit
and accept numeric, vector, or point values. Other built-in checks require the
corresponding runtime kind; a custom check requires the exact declared or
external type name. `nothing` is written without `:` in casts and checks.

Custom record types are type references using the same `:name` syntax:

```ges
record :unit as {
  hp: :number,
  name: :text
}
```

### Numbers

The normative cross-language rules for Int64 overflow, binary64 special values,
rounding, negative division/modulo, ULP comparison, transcendental functions,
and canonical conformance JSON are defined in
[Number semantics](Semantics/Numbers.md). Implementations must
not inherit these semantics from host-language overflow or formatting defaults.

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

Numeric checks are keyword constructs, not `:` type references:

```ges
value is numeric
value is integer
value is fractional
```

`:integer` and `:float` are not source-level type references. Use `is integer`
or `is fractional` for checks, and `as :number` for explicit numeric casts.

Numeric checks and implicit numeric views use the following rules. A runtime
value has an `AsNumeric` value exactly when `is numeric` is true. The explicit
`as :number` cast uses the same numeric view, except that it may additionally
parse text as described below.

| Runtime value | `is numeric` | `is integer` | `is fractional` | Numeric view |
| --- | --- | --- | --- | --- |
| `nothing` | false | false | false | none |
| `:boolean` | true | true | false | `false` = `0`, `true` = `1` |
| integer number | true | true | false | integer value, including quantity unit |
| float number | true | true when finite and exactly integral; otherwise false | true when finite and non-integral | float value, including quantity unit |
| percentage | true | true when the stored ratio is finite and exactly integral; otherwise false | true when finite and non-integral | stored ratio |
| dice | true | true | false | sum of rolls |
| text, tag, list, map, range, vector, point, message, handler, series, custom values | false | false | false | none |

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
`true` = `1`), and dice sums compare by numeric value when both operands have
a numeric view. Quantity units must match exactly; unitless and
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
tags and text compare exact Unicode-scalar sequences within their own kind, vectors and points compare `x`, `y`, `z`,
and unit, ranges compare `from`, `to`, and `step`, series compare signature and
offset, messages compare signature id, arguments, and tag sequence, handlers
compare signature id, lists compare ordered items, dice compare ordered rolls,
and maps/records/custom map-like values compare their key/value pairs. Records
also require the same declared record type. Nested collection, record, and
message values use strict structural equality without top-level numeric
coercion; the complete contract is in
[Determinism](Semantics/Determinism.md).

`abs` preserves the operand's numeric family for percentages and quantities:
absolute percentages remain `:percentage`, and absolute quantities keep their
unit. Finite numeric results that are exactly integral are represented as
integer values.

### Quantities
,
Quantities attach a numeric unit to an integer or float:

```ges
let distance be 10m
let duration be 2s
let angle be 90°

let a be 100 as :quantity(m)
let b be 90 as :quantity(°)
```

`:percentage` is not a quantity unit; it is a separate value kind. The names
`meter`, `second`, `degree`, and `seconds` are not quantity type aliases. Use
`:quantity(m)`, `:quantity(s)`, or `:quantity(°)`/`:quantity(degree)` for casts
and checks; use `#meter` or `#degree` only when an ordinary tag value is meant.

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
let state be #active
```

Tags are not empty. Tags are text-like symbolic values: they use the same raw
text as quoted text values, but are written without quotes and must follow the
tag naming rules. Text and tags can be used interchangeably for map keys, member
lookup selectors, text containment, and text boundary operations.

Text preserves its exact Unicode scalar sequence without normalization. Text
length, positional indexing, iteration, text-to-list conversion, and terminal
operations count or return Unicode scalar values rather than UTF-8 bytes,
UTF-16 code units, or grapheme clusters. Indexes remain 1-based. Equality is
exact and ordering is lexicographic by Unicode scalar value; see
[Text semantics](Semantics/Text.md).

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

Ranges are finite numeric ranges. Bounds and step values can be integer or
floating-point numeric values:

```ges
let r be from 1 to 10
let stepped be from 10 to 0 step -2
let fractional be from 1.5 to 3.5 step 0.5
```

Both bounds are inclusive. A zero step or a step pointing away from the end
produces an empty range. Integer boundary handling is overflow-safe, and
floating iterators emit exactly their precomputed finite range length; see
[Determinism](Semantics/Determinism.md).

### Series

Series are deterministic numeric series. They support zero-based `:term` lookup
and forward `:take first` / `:drop first` selectors. Because series are not
finite, `:take last` and `:drop last` evaluate to `nothing` for series.

```ges
let fib be series fibonacci
let fifth be fib[:term 5]
let later be fib[:drop first 3]
let sample be fib[:take first 5]
```

`:term` is a series-only selector. Lists, dice, ranges, and all other values
return `nothing` for `[:term n]`. Direct `:take first`, `:drop first`, `:take
last`, and `:drop last` selectors are defined for finite lists, dice, and
ranges; range slices stay ranges instead of being materialized as lists.

Built-in series:

- `series fibonacci`
- `series factorial`

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
`nothing`. The spelling `#nan` is an ordinary tag with no numeric meaning.

`empty` is true for:

- `nothing`
- empty text
- empty lists, maps, dice, and ranges

It is false for ordinary values, including `0`, `false`, `Infinity`, and
`-Infinity`.

`has value` is the exact complement of `empty`.

`default` uses the same presence semantics as `has value`:

```ges
let displayName be unit.name default 'Unknown'
```

## Collections and Selectors

### Lists

Lists preserve order and can contain mixed values:

```ges
let values be [10, 'hello', [1, 2, 3]]
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

Lookups use member syntax or bracket syntax. Literal text and tag keys are
member lookups; dynamic selectors are resolved at runtime. `x[:name]` is the
selector shorthand for the member/key named `name`, while `x[#name]` uses the
tag value `#name` as a dynamic key.

```ges
unit.name
unit['name']
unit[:name]
unit[#name]
```

Dynamic map lookup with a non-text and non-tag key returns `nothing`.

### Lookup and Membership

`x[y]` performs positional index lookup for integer selectors and member lookup
for text/tag selectors unless `y` starts with a structured selector. Positional
indexes are 1-based.

```ges
let firstItem be items[1]
let hp be unit[:hp]
let hasEnemyFlag be #enemy in flags
let containsUnit be unit in values of units
```

`x in y` checks membership in `y`. Text and tags use ordinal substring
matching over their raw text, without the leading `#` for tags. Lists and dice
check whether one item equals `x`. Ranges check whether numeric `x` is one of
the range terms. Maps and map-like values check whether text/tag `x` is a
key; non-text keys are false. Vector and point values check their
numeric components.

`x in values of y` checks values of map-like `y`. It is defined for maps,
records/custom values, external map-like values, vectors, and points. For maps
and records it compares all stored fields. For vectors and points it compares
the `x`, `y`, and `z` components. `nothing in values of y` follows the same
comparison rule; `x in values of nothing` is `nothing`. Lists, dice, ranges, text,
tags, and scalar values are not value-membership containers and return `false`.

`x starts with y` and `x ends with y` are boundary checks. Text and tags compare
raw text with ordinal rules. Lists, dice, and ranges compare sequence prefixes
or suffixes; the right operand must also be a list, dice, or range. Empty
right-hand sequences match. If the left sequence is shorter than the right
sequence, the result is `false`. `nothing starts with y` and
`nothing ends with y` produce `nothing`; other unsupported shapes return
`false`.

### Iterator-Backed Selectors

Selectors operate on lists, ranges, dice, maps, custom map-backed values, text,
tags, and other iterable values. Selector chains lower to explicit iterator
loops when no direct fast path exists.

```ges
units[:filter unit where unit.hp > 0]
units[:select unit => unit.name]
units[:any unit where unit.hp <= 0]
units[:all unit where unit.hp > 0]
units[:count unit where unit.hp > 0]
units[:count]
```

`=>` is the projection arrow. `↦` is an alias.

Iterator predicates are evaluated once per item in deterministic source order.
Only a true truth view passes a filter or satisfies a predicate; false and
indeterminate results do not. `:any` short-circuits on the first true result and
is false for an empty source. `:all` short-circuits on the first non-true result
and is true for an empty source. `:select` materializes projected values in
source order. Iterator-backed operations are bounded by the host's loop and
generated-collection limits.

### Aggregates

```ges
units[:sum unit => unit.hp]
units[:average unit => unit.hp]
units[:sum]
units[:average]
units[:min unit => unit.hp]
units[:max unit => unit.hp]
units[:highest unit => unit.hp]
units[:lowest unit => unit.hp]
```

Bare `:count`, `:sum`, and `:average` are identity forms. `items[:count]`
counts all finite items, `items[:sum]` is equivalent to
`items[:sum item => item]`, and `items[:average]` is equivalent to
`items[:average item => item]`.

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
lists, dice, ranges, maps, custom map-backed values, text, tags, and iterator
chains.
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
and iterator chains. Lists and iterator chains materialize sorted lists. Dice
also materialize a list so the requested order is preserved instead of being
normalized back into dice order. Ranges stay ranges when the requested direction
can be represented by swapping the range bounds and negating the step. Maps are
already key-canonical and are not sort targets; sorting a map yields `nothing`.

`:order by` is defined for lists and iterator chains only. It orders the
original items by the projected key and materializes a list. Dice, ranges, maps,
scalars, and `nothing` yield `nothing` for `:order by` because projecting a sort
key over those direct values is not a meaningful collection operation.

`:distinct` is defined for lists, dice, and iterator chains. Lists keep their
first occurrence order, dice keep their dice result type, and iterator chains
materialize a list. `:distinct by` is defined only for lists and iterator
chains because the projection operates on structured items. Dice and other
non-list values yield `nothing` for `:distinct by`.

`:group by` is defined for lists, maps, and iterator chains. Lists and iterator
chains group their source items by the projected key and return a map from the
projected key text to a list of matching source items. Maps group their visible
values in stable key order. Direct dice, ranges, text, tags, scalars, and
`nothing` yield `nothing`; dice and ranges are scalar-like direct values for
grouping and must be iterated explicitly when per-element grouping is wanted.

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
#active starts with 'act'
'active' ends with #ive
```

`:contains value` applies ordinary membership to the selected source.
`:contains any candidates` and `:contains all candidates` require the right-hand
source to contain respectively at least one or every item from the candidate
list, dice, text/tag scalar sequence, range, or iterator. Empty candidates make
`any` false and `all` true. If the selected source is `nothing`, the result is
`nothing`; an unsupported candidate shape follows the same empty-candidate rule.

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

`:reverse` is defined for lists, dice, ranges, and iterator chains. Lists
reverse into lists. Dice reverse into lists so the requested order is preserved
instead of being normalized back into dice order. Ranges reverse into ranges by
swapping the effective bounds and negating the step. Iterator chains materialize
reversed lists. Maps, scalars, and `nothing` yield `nothing`.

`:shuffle` is defined for lists, dice, ranges, and iterator chains and always
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
the whole unique sequence to be consecutive with no gaps. A sequence containing
a value without an integer representation does not match a straight.

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
a zip b
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

`zip` is defined for lists only. It pairs items by index up to the shorter
operand length and returns a list of maps with `left` and `right` entries.
Other operand combinations produce `nothing`.

`listOfKeys` means a list containing only text or tag values. A key list
containing any other value produces `nothing`.

`+` concatenates text when either operand is text. The non-text operand is
formatted with the same text representation used by `as :text`, so
`'100' + '200'` is `'100200'` and `'hp: ' + 10` is `'hp: 10'`.

## Randomness, Dice, and Series

`random` is a built-in random expression form. Bounds are evaluated left to
right. Two integral, unitless bounds draw a uniformly distributed signed-64-bit
integer including both ordered bounds. Otherwise compatible finite numeric
bounds draw a Binary64 value by scaling a `[0, 1)` source; final Binary64
rounding may nevertheless equal the upper bound. Reversed bounds are accepted.
Equal bounds return that bound without consuming random state. Incompatible
units, absent values, non-numeric values, and invalid non-finite bounds produce
`nothing`:

```ges
let dieRoll be random from 1 to 6
let chance be chance 25%
```

Dice rolls use `roll dice NdM`, where both `N` and `M` are positive integer
literals. They draw `N` independent integers from `1` through `M` inclusive and
produce a sorted dice value. This phrase consumes random. `:dice(...)` remains
the deterministic dice constructor and `:dice` remains the dice type reference:

```ges
let diceRoll be roll dice 5d6
let hasFullHouse be diceRoll[:has full house]
```

`series fibonacci` and `series factorial` create deterministic, unbounded lazy
series. `series[:term index]` uses a zero-based, unitless, non-negative integer
index; an invalid index yields `nothing`. Series do not expose mutable position.
Collection limits still apply when a selector materializes multiple values.

The exact generator transition, seed interpretation, integer rejection
sampling, floating-point construction, nested seeded scopes, and known-answer
vectors belong to [Determinism](Semantics/Determinism.md).

## Extensions

Extension references use `:extension.function`. Calls support the same ordered
argument model as script callables and may use any of these source shapes:

```ges
:combat.damage(base: 10, multiplier: 2)
:math.maximum of left and right
:log.write value: result
:text.length value
:clock.now
```

The compiler binds an extension by namespace, function name, and ordered
argument labels. The declarative compiler registry must contain that exact
signature. Runtime linking then resolves the same identity against the host
registry; no reflection or host-language callable is part of source semantics.
`value is :extension.predicate` is permitted only for a unary extension with one
unlabeled argument, and its result is normalized to boolean or `nothing`.

Built-in math intrinsics are opcodes rather than standard extensions:

- `floor`, `ceil`, `truncate`
- `round half even`, `round half up`, `round half down`
- `rad`, `deg`, `wrap degree`
- `abs`, `ln`, `exp`, `sqrt`, `cbrt`, `chance`
- `sin`, `cos`, `tan`, `asin`, `acos`, `atan`
- `atan2`, `hypot`, `distance`, `distance squared`, `length squared`,
  `normalize`, `dot`, `cross`, `angle between`
- `series fibonacci`
- `series factorial`

Unknown extension signatures are static errors. A known compile-time signature
that cannot be linked by a particular Host is a link error, not a language
evaluation result.

## Records and Custom Types

Records are immutable map-like values with declared fields:

```ges
record :gauge as {
  current: :number clamped between 0 and maximum,
  maximum: :number clamped between 0 and infinity,
  percentage: :percentage computed by
    0% when maximum <= 0,
    otherwise (current / maximum) as :percentage
}

let hp be :gauge(current: 25, maximum: 100)
```

Field constraints:

- Fields and their final map entries retain declaration order. Field names must
  be unique.
- A non-computed field is a constructor parameter. Omitted constructor arguments
  bind `nothing`; unknown labels, duplicate labels, or excess positional
  arguments are static errors.
- A field type casts the input value. Fields are processed in declaration order.
- `clamped between min and max` evaluates both bounds in the constructor frame,
  clamps the already cast numeric value, and casts the result to the field type
  again.
- `computed by expression` derives the field from constructor fields and fields
  computed earlier in declaration order, then casts the result to its field
  type.
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

Record values are immutable, expose fields through member access, participate in
map ordering and iteration through their field map, and keep their exact custom
type identity. Custom type checks use `is :typeName`. Casting a map to a record
invokes the same constructor by matching map keys to constructor labels.

## Language and host boundary

The language defines handler matching, handler evaluation, `emit`, and
`publish`. Program loading, dynamic linking, priorities, subscription snapshots,
queue acceptance, scheduling, runtime observers, outbound sinks, and execution
results are not source-language constructs. Their complete portable contract is
owned by [Host runtime](HostRuntime.md). The transport representation produced
by compilation is owned by [Program model](ProgramModel.md), [Bytecode](Bytecode.md),
and [Binary format](BinaryFormat.md).

## Errors and Limits

Source processing reports distinct parse, validation, and compile diagnostics.
Their stable language-neutral codes and locations are defined by
[Diagnostics](Diagnostics.md). Compilation validates at least:

- naming conventions
- duplicate definitions and duplicate parameters
- callable arity
- direct and indirect cyclic calls (recursion is not part of the language)
- predicate return type (`:boolean` or `nothing`)
- handler message-name shape
- record field definitions
- seeded random seed type
- unknown or invalid type forms

Runtime limits cover:

- maximum processed events
- maximum queued messages
- maximum execution steps
- maximum register values
- maximum loop iterations
- maximum call depth
- maximum range items
- maximum generated collection items
- maximum dice count and dice sides

Runtime limits are reset per handler, while a host frame budget is an independent
scheduling constraint. Crossing a limit terminates the active handler through a
structured runtime diagnostic; it must not escape as an implementation-language
exception or corrupt later dispatch. Ordinary unsupported value operations use
the explicitly documented `nothing`, boolean, empty-collection, or numeric `NaN`
result instead.

## Informative example

```ges
module Robot

record :scan as {
  distance: :quantity(m),
  angle: :quantity(°),
  strength: :percentage
}

predicate close(_ scan) be scan.distance < 30m
predicate strong(_ scan) be scan.strength > 50%

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

The grammar uses the extended BNF expression syntax understood by JetBrains
Grammar-Kit so that `bnf` code fences receive useful syntax highlighting in
IntelliJ-based editors. A rule uses `::=`; adjacent expressions form a sequence;
`|` separates alternatives; `[ X ]` is optional; and `( X )*` / `( X )+` mean
zero-or-more / one-or-more repetitions. Quoted text is a terminal. Uppercase
bare names such as `IDENTIFIER`, `MESSAGE_NAME`, `NUMBER`, and `NL` are lexical
token categories governed by the lexical rules above.

This is only a notation contract. The specification does not adopt Grammar-Kit
rule attributes, parser generation, recovery, pinning, or PEG conflict
resolution. Separators are required only where shown; `NL` is `LF`, `CRLF`, or
`CR`, and the parser also accepts it around punctuation and operators where
token separation is unambiguous.

```bnf
compilation_unit ::= separators [module_declaration separators] (top_level_declaration separators)* EOF
separators ::= (NL | ';')*
module_declaration ::= 'module' module_name
top_level_declaration ::= record_definition | predicate_definition | function_definition | event_handler

record_definition ::= 'record' custom_type_tag 'as' '{' separators [record_field (record_field_separator record_field)*] separators '}'
record_field_separator ::= ',' separators | separators
record_field ::= ['_'] IDENTIFIER ':' type_reference ['clamped' 'between' expression 'and' expression] ['computed' 'by' expression]

predicate_definition ::= 'predicate' IDENTIFIER definition_parameters 'be' expression
function_definition ::= 'function' IDENTIFIER definition_parameters 'be' expression
definition_parameters ::= '(' [parameter (',' parameter)*] ')'
parameter ::= IDENTIFIER ['as' type_reference] | '_' IDENTIFIER ['as' type_reference]

event_handler ::= 'on' handler_endpoint [handler_binding] handler_tag_filter* '{' separators [statement (statement_separator statement)*] separators '}'
handler_endpoint ::= MESSAGE_NAME | 'initialization' | 'undeliverable'
handler_binding ::= '(' [parameter (',' parameter)*] ')' | 'as' IDENTIFIER
handler_tag_filter ::= ('matching' | 'without') TAG_LITERAL (',' TAG_LITERAL)*
statement_separator ::= NL | ';'

statement ::= emit_statement | publish_statement | let_statement | if_statement | for_statement | seeded_random_statement | expression_statement
emit_statement ::= 'emit' message_expression [tag_clause]
publish_statement ::= 'publish' message_expression [tag_clause]
message_expression ::= MESSAGE_NAME [parenthesized_arguments] | expression
tag_clause ::= 'with' expression (',' expression)*
let_statement ::= 'let' IDENTIFIER ['as' type_reference] 'be' expression
if_statement ::= 'if' expression statement_body ['else' statement_body]
for_statement ::= 'for' IDENTIFIER ('in' expression | range_source) statement_body
seeded_random_statement ::= 'random' 'with' expression statement_body
expression_statement ::= expression
statement_body ::= statement | '{' separators [statement (statement_separator statement)*] separators '}'

expression ::= guarded_choice_expression
guarded_choice_expression ::= implication_expression ['when' implication_expression (',' ['or'] implication_expression 'when' implication_expression)* 'otherwise' implication_expression]
implication_expression ::= default_expression [implication_arrow implication_expression]
implication_arrow ::= '->' | '→' | '⇒'
default_expression ::= collection_union_expression ('default' collection_union_expression)*
collection_union_expression ::= collection_intersection_expression ('|' collection_intersection_expression)*
collection_intersection_expression ::= collection_zip_expression ('&' collection_zip_expression)*
collection_zip_expression ::= or_expression ('zip' or_expression)*
or_expression ::= xor_expression (('or' | '∨') xor_expression)*
xor_expression ::= and_expression (('xor' | '⊕') and_expression)*
and_expression ::= equality_expression (('and' | '∧') equality_expression)*
equality_expression ::= membership_expression (('=' | '<>' | '≠') membership_expression)*

membership_expression ::= type_operation_expression (membership_operator type_operation_expression)*
membership_operator ::= 'in' | '∈' | '∉' | 'has' 'value' | 'in' 'values' 'of' | 'starts' 'with' | 'ends' 'with'
type_operation_expression ::= relational_expression type_operation*
type_operation ::= 'as' type_reference |
                   'is' [not_operator] (type_reference | numeric_check | 'empty' | extension_reference | IDENTIFIER |
                   'at' ('least' | 'most') additive_expression | ('less' | 'more') 'than' additive_expression |
                   additive_expression ['or' ('less' | 'more')])
relational_expression ::= additive_expression (('<' | '>' | '<=' | '>=' | '≤' | '≥') additive_expression)*
additive_expression ::= multiplicative_expression (('+' | '-' | '−') multiplicative_expression)*
multiplicative_expression ::= unary_expression (('*' | '×' | '·' | '⋅' | '/' | '÷' | 'div' | 'mod' | 'rem') unary_expression)*
unary_expression ::= ('-' | '−' | not_operator | 'empty') unary_expression | unary_intrinsic_expression | power_expression
not_operator ::= 'not' | '!' | '~' | '¬'
power_expression ::= power_base_expression [('^' unary_expression) | '²' | '³']
power_base_expression ::= series_expression | extension_call_expression | intrinsic_call_expression | clamp_expression | variadic_expression | postfix_expression

unary_intrinsic_expression ::= unary_intrinsic unary_intrinsic_operand
unary_intrinsic_operand ::= unary_expression | '(' expression ')'
unary_intrinsic ::= 'abs' | 'ln' | 'exp' | 'sqrt' | '√' | 'cbrt' | '∛' |
                    'chance' | 'floor' | 'ceil' | 'truncate' |
                    'round' 'half' ('even' | 'up' | 'down') |
                    'rad' | 'deg' | 'sin' | 'cos' | 'tan' | 'asin' |
                    'acos' | 'atan' | 'wrap' 'degree'
intrinsic_call_expression ::= intrinsic_name '(' [expression (',' expression)*] ')'
intrinsic_name ::= 'atan2' | 'hypot' | 'distance' | 'distance' 'squared' | 'length' 'squared' | 'normalize' | 'dot' | 'cross' | 'angle' 'between'
clamp_expression ::= 'clamp' unary_expression 'between' expression 'and' expression
variadic_expression ::= ('min' | 'max') 'of' expression ('and' expression)*

postfix_expression ::= primary_expression postfix_suffix*
postfix_suffix ::= '.' IDENTIFIER | '[' collection_selector ']'
primary_expression ::= literal | call_expression | uppercase_call_expression |
                       type_constructor_expression | IDENTIFIER |
                       '(' expression ')' | bracket_literal | generated_list_expression |
                       range_expression | random_expression | seeded_random_expression | dice_expression
call_expression ::= IDENTIFIER parenthesized_arguments
uppercase_call_expression ::= MESSAGE_NAME parenthesized_arguments
type_constructor_expression ::= (built_in_type_tag | custom_type_tag) parenthesized_arguments
parenthesized_arguments ::= '(' [argument (',' argument)*] ')'
argument ::= [argument_label ':'] expression
argument_label ::= IDENTIFIER | 'to'

literal ::= NUMBER | PERCENTAGE | UNIT_NUMBER | TEXT_LITERAL | TAG_LITERAL | 'true' | 'false' | 'nothing' | math_constant
math_constant ::= 'infinity' | '∞' | 'pi' | 'π' | '∏' | 'e' | 'ℇ' | 'tau' | 'τ'
bracket_literal ::= list_literal | map_literal
list_literal ::= '[' [expression (',' expression)*] ']'
map_literal ::= '[' ':' ']' | '[' map_entry (',' map_entry)* ']'
map_entry ::= IDENTIFIER ':' [expression]
generated_list_expression ::= ':list' '[' ':select' IDENTIFIER (range_source | 'in' expression) ['where' expression] projection_arrow expression ']'
range_expression ::= range_source
range_source ::= 'from' expression 'to' expression ['step' expression]
random_expression ::= 'random' ['from'] additive_expression 'to' additive_expression
seeded_random_expression ::= 'random' 'with' expression expression
dice_expression ::= 'roll' 'dice' POSITIVE_INTEGER 'd' POSITIVE_INTEGER
series_expression ::= 'series' ('fibonacci' | 'factorial')

extension_reference ::= ':' IDENTIFIER '.' IDENTIFIER
extension_call_expression ::= extension_reference [parenthesized_arguments |
                              'of' expression ('and' expression)* |
                              argument_label ':' expression (argument_label ':' expression)* |
                              unary_expression]

collection_selector ::= structured_selector | expression
structured_selector ::= quantified_selector | pattern_selector | take_selector | drop_selector |
                        term_selector | count_selector | choose_selector | draw_selector |
                        shuffle_selector | reverse_selector | edge_selector | filter_selector |
                        sum_selector | average_selector | extrema_selector | projection_selector |
                        map_selector | contains_selector | distinct_selector | group_selector |
                        sort_selector | order_selector | ':keys' | ':values' | ':entries'
quantified_selector ::= (':any' | ':all') IDENTIFIER 'where' expression
pattern_selector ::= ':has' (object_pattern | dice_pattern)
take_selector ::= ':take' (slice | dice_pattern)
drop_selector ::= ':drop' slice
term_selector ::= ':term' expression
count_selector ::= ':count' [IDENTIFIER 'where' expression]
choose_selector ::= ':choose' POSITIVE_INTEGER ['at' 'random'] [IDENTIFIER 'where' expression] ['weighted' 'by' IDENTIFIER projection_arrow expression]
draw_selector ::= ':draw' POSITIVE_INTEGER
shuffle_selector ::= ':shuffle'
reverse_selector ::= ':reverse'
edge_selector ::= (':first' | ':last' | ':single') [IDENTIFIER 'where' expression]
filter_selector ::= ':filter' IDENTIFIER 'where' expression
sum_selector ::= ':sum' [IDENTIFIER projection_arrow expression]
average_selector ::= ':average' [IDENTIFIER projection_arrow expression]
extrema_selector ::= (':min' | ':max' | ':highest' | ':lowest') IDENTIFIER projection_arrow expression
projection_selector ::= ':select' IDENTIFIER projection_arrow expression
map_selector ::= ':map' IDENTIFIER 'by' expression [projection_arrow expression]
contains_selector ::= ':contains' ['all' | 'any'] expression
distinct_selector ::= ':distinct' ['by' IDENTIFIER projection_arrow expression]
group_selector ::= ':group' 'by' IDENTIFIER projection_arrow expression
sort_selector ::= ':sort' sort_direction
order_selector ::= ':order' 'by' IDENTIFIER projection_arrow expression sort_direction
projection_arrow ::= '=>' | '↦'
sort_direction ::= 'ascending' | 'descending'
slice ::= ('first' | 'last' | 'highest' | 'lowest') POSITIVE_INTEGER

dice_pattern ::= 'pair' ['of' unary_expression] |
                 dice_count_word 'of' ('a' 'kind' | unary_expression) |
                 'full' 'house' | 'straight'
dice_count_word ::= 'three' | 'four' | 'five' | 'six' | 'seven'
object_pattern ::= '[' [object_entry (',' object_entry)*] ']'
object_entry ::= IDENTIFIER ':' (expression | object_pattern)

type_reference ::= 'nothing' | built_in_type_tag | custom_type_tag
built_in_type_tag ::= ':boolean' | ':number' | ':percentage' |
                      ':quantity' '(' unit_name ')' | ':vector' | ':point' |
                      ':series' | ':range' | ':message' | ':handler' | ':tag' |
                      ':text' | ':list' | ':map' | ':dice'
custom_type_tag ::= TYPE_TAG
unit_name ::= 'm' | 'meter' | 's' | 'second' | 'degree'
numeric_check ::= 'numeric' | 'integer' | 'fractional'
module_name ::= IDENTIFIER | MESSAGE_NAME
```

An uppercase call is resolved contextually because messages and handler values
share the same leading syntax. An empty argument list, or a list containing only
unlabeled parameter declarations (`name` or `_ name`), creates a handler value.
A list containing a labeled argument (`name: expression`) creates a message
value. A bare uppercase name in `emit` or `publish` is a zero-argument message.

Adjacent `NUMBER IDENTIFIER` tokens without whitespace are also accepted as
implicit multiplication. This applies only to numeric literals followed by an
identifier or math constant; ordinary expressions require an explicit
multiplication operator.
