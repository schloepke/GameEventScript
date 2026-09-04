# Portable determinism semantics

This document defines the language-neutral deterministic behavior that every
Game Event Script compiler and runtime port must reproduce. It complements
[Text semantics](Text.md), [Number semantics](Numbers.md), [Bytecode](../Bytecode.md), and
[Host runtime](../HostRuntime.md).

Determinism means that the same validated program, ordered inputs, host
configuration, initial random seed, and runtime limits produce the same visible
messages and values. Wall-clock time, thread scheduling, object identity,
platform hash iteration, and the non-seeded `Create()` random source are not
deterministic inputs.

## Seeded random generator

`GameEventScriptRandomGenerator.FromSeed(Int64)` is a portable seeded stream.
The signed seed is reinterpreted as its two's-complement `UInt64` bit pattern.
All operations in this section use wrapping unsigned 64-bit arithmetic.

The four-word xoshiro state is initialized by four consecutive SplitMix64
outputs. SplitMix64 adds `0x9E3779B97F4A7C15` before every output and applies:

```text
z = (z xor (z >> 30)) * 0xBF58476D1CE4E5B9
z = (z xor (z >> 27)) * 0x94D049BB133111EB
result = z xor (z >> 31)
```

The raw generator is xoshiro256**. For state words `s0..s3`, one output is:

```text
result = rotateLeft(s1 * 5, 7) * 9
t = s1 << 17
s2 = s2 xor s0
s3 = s3 xor s1
s1 = s1 xor s2
s0 = s0 xor s3
s2 = s2 xor t
s3 = rotateLeft(s3, 45)
```

Known raw `UInt64` outputs, in generation order:

| Seed | Outputs |
| ---: | --- |
| `0` | `99EC5F36CB75F2B4`, `BF6E1F784956452A`, `1A5F849D4933E6E0`, `6AA594F1262D2D2C`, `BBA5AD4A1F842E59`, `FFEF8375D9EBCACA`, `6C160DEED2F54C98`, `8920AD648FC30A3F` |
| `1` | `B3F2AF6D0FC710C5`, `853B559647364CEA`, `92F89756082A4514`, `642E1C7BC266A3A7`, `B27A48E29A233673`, `24C123126FFDA722`, `123004EF8DF510E6`, `61954DCC47B1E89D` |
| `-1` | `8F5520D52A7EAD08`, `C476A018CAA1802D`, `81DE31C0D260469E`, `BF658D7E065F3C2F`, `913593FDA1BCA32A`, `BB535E93941BA525`, `5ECDA415C3C6DFDE`, `C487398FC9DE9AE2` |
| `Int64.MinValue` | `D01BFA9B44A998C3`, `797C6B72FF690D62`, `4576AF98398380B1`, `E5CE401830AAA16C`, `A5EECCC1D5D5EE1A`, `CD43606B62171D67`, `4205C135C5E32535`, `6D11E27BBF34F857` |
| `Int64.MaxValue` | `0E1C2B4B82E8C0C5`, `19167A27A6E0D81B`, `7B5F1A55D35896BD`, `0D19F02BF9005C90`, `0EEE111B5F85ACA0`, `BB969C534267CF4F`, `BAEC81932902A56E`, `134E81D9C55B497C` |

Inclusive integer sampling first swaps reversed bounds. Equal bounds return that
value without consuming the stream. Otherwise it computes the inclusive span
modulo `2^64`. A zero span denotes the full `UInt64` range. For a non-zero span
`b`, sampling uses unbiased rejection:

```text
threshold = (0 - b) mod b
repeat raw = nextUInt64 until raw >= threshold
offset = raw mod b
result = minInclusive + offset
```

For seed `0`, eight samples from `[-100, 100]` are:
`16, -95, -9, -45, -76, 43, -62, -21`.

`NextFloat(firstBound, secondBound)` orders its two bounds and derives a unit
value from the upper 53 bits:

```text
unit = (nextUInt64 >> 11) * 2^-53
result = min + (max - min) * unit
```

The seeded unit value lies in `[0, 1)`, but the final binary64 multiplication
and addition can round the result to the upper bound. Consequently the public
method is not named `NextInclusiveFloat` and callers must not infer the source
interval from the possible rounded result values.

Reversed bounds are swapped. Equal bounds return the first bound without
consuming the stream; this also preserves the first bound's signed-zero bit.
A NaN bound returns NaN without consuming the stream. These no-consumption
rules apply to seeded generators and to the `FromSequence` test adapter.

For seed `0`, the first five `NextFloat(0, 1)` result bit patterns are
`3FE33D8BE6D96EBE`, `3FE7EDC3EF092AC8`, `3FBA5F849D4933E0`,
`3FDAA9653C498B4A`, and `3FE774B5A943F085`.

For seed `0`, `NextFloat(1, 1.0000000000000002)` produces the upper-bound
bit pattern `3FF0000000000001` through binary64 rounding even though the unit
sample is less than one.

`Create()` intentionally chooses a non-deterministic seed and therefore has no
cross-platform output contract. `FromSequence` is a test adapter: supplied
values are copied, consumed in order, and clamped to requested non-collapsed
bounds. After exhaustion it continues with its private generator. The public
standalone factory seeds that fallback non-deterministically. Host configuration
may provide an explicit fallback seed; Markdown conformance uses seed `0`, so an
exhausted test sequence remains portable and reproducible.

A host never accepts or retains an externally owned mutable generator. Its
builder accepts a seed or immutable sequence values and constructs a private
generator for every built host. Two hosts built from one builder therefore have
independent state even when their initial streams are identical.

## Seeded random scopes

`random with seed` requires the seed to be statically known as a unitless exact
integer or explicitly converted with `as :number`. A runtime seed value creates
a newly seeded stream only when it is a unitless exact integer in the signed
64-bit range.

Every seeded-random construct first snapshots the complete active random state.
If the runtime seed is valid, the active state is reset from that seed. If it is
invalid, fractional, unit-bearing, or `nothing`, the active state remains an
identical copy of its parent state and no diagnostic is reported. The body may
consume any number of values from that copied state. Leaving the construct
always restores the saved parent state exactly, so an invalid scoped seed can
never advance or otherwise perturb its parent stream.

Scopes may nest and the same snapshot/restore rule applies at every level. The
random-state stack belongs to the host generator rather than the VM, allowing
extensions to observe and explicitly create scopes on the same active stream.
The host marks every native and script handler boundary, and every extension
call adds a nested marker. Handler cleanup unwinds outstanding snapshots if a
runtime limit or diagnostic prevents normal execution of a matching `RandomPop`.
Script handler markers persist across frame pauses and are independent of the
thread that executes a later frame.

`MaxRandomScopeDepth` limits simultaneously active regular scopes. The limit is
exact: an implementation reserves one additional state exclusively as an
overpush gate. The first rejected push saves the last valid stream in that gate,
latches the runtime-limit fault, and makes all subsequent random work disposable
until boundary release. Additional rejected pushes and their pops only maintain
a suppressed nesting count and cannot touch valid parent states. Emit and
Publish are rejected while the gate is active. Bytecode stops immediately;
atomic native and extension callbacks are checked when they return. No exception
is required or observable. Boundary release restores the valid stream, and the
host remains usable for later handlers and messages.

## Equality

The script operators `=` and `<>` use script equality:

- If either operand is `nothing`, the result is `nothing`.
- Boolean, integer, float, percentage, and dice values have a numeric view.
  Different numeric kinds compare through that view, quantities require the
  same unit, and binary64 comparison follows [Number semantics](Numbers.md).
  Top-level dice compare by the sum of their rolls.
- Text and tag are distinct kinds. Equal character content does not make a text
  equal to a tag.
- All other present cross-kind pairs are unequal. In particular, a map is not
  equal to a record, and records with different declared types are unequal.
- Vector and point require the same kind and unit. Handlers require the same
  signature. Series require the same signature and offset. Ranges require the
  same representation and bounds/step.
- Lists, maps, records, and message arguments use structural value equality for
  their nested values. Structural equality requires the same value kind and
  unit; it does not apply top-level numeric coercion or the two-ULP tolerance.
  Thus `10 = 1000%` and two dice with the same sum can be true at top level,
  while `[10] = [1000%]` and lists containing different roll sequences are
  false.

Structural value equality is also used by membership, distinct, grouping keys,
set-like collection operators, public value equality, and message argument
equality. Finite Binary64 values compare exactly there; positive and negative
zero are equal, equal-signed infinities are equal, and internal NaN values are
never exposed as ordinary script values.

## Ordering and stable sorting

Text and map-key order is Unicode-scalar ordinal as defined by
[Text semantics](Text.md). Maps and map-backed records expose keys, values,
entries, iteration, `first`, and `last` in ascending key order, independent of
insertion order or hash-table behavior. Duplicate map construction keys use
last-entry-wins before the final key sort.

`:sort` and `:order by` are stable in both ascending and descending direction.
Items whose comparison keys are equal retain source order. Implementations may
use any sorting algorithm only if it preserves this observable rule.

Comparable numeric values require compatible units. Text and tags use scalar
ordinal order. Boolean values order `false` before `true`; vector and point
values use lexicographic `x`, then `y`, then `z` order when their kinds and units
are compatible. Incompatible units or an invalid comparison make the sort
result `nothing`. If both operands have numeric views, numeric comparison takes
precedence over kind rank; this includes booleans and dice sums. Otherwise the
portable heterogeneous rank is:

| Rank | Kinds |
| ---: | --- |
| 1 | integer, float, percentage |
| 2 | text |
| 3 | tag |
| 4 | vector |
| 5 | point |
| 6 | boolean when the other value is not numeric-capable |
| 8 | range, series, custom record, and otherwise unranked supported values |
| 9 | message |
| 10 | handler |
| 11 | list |
| 12 | map |
| 13 | dice when the other value is not numeric-capable |

There is intentionally no rank 7 in the V1 contract. When same-rank supported
values have no finer ordering, their comparison is equal and stable source
order is retained.

## Iteration and ranges

Iteration order is never inherited from a platform hash container:

- lists retain list order;
- dice retain their stored roll order;
- text and tags yield Unicode scalars in text order;
- vector and point yield `x`, `y`, then `z`;
- maps and records yield values in ascending key order;
- transformed iterators retain source order unless an explicit ordering or
  random operation changes it.

Ranges are inclusive. A zero step or a step whose direction cannot reach the
end produces an empty range. Integer range length and term calculation must use
overflow-safe unsigned-distance arithmetic; reaching `Int64.MaxValue` or
`Int64.MinValue` must never wrap into additional items. A range iterator emits
exactly its precomputed length and does not decide termination by incrementing
past the final value.

Floating ranges require finite bounds and a finite non-zero step. Their length
is computed with Binary64 operations as
`floor(distance / abs(step)) + 1`, saturated to `Int64.MaxValue`, after the same
direction checks. Iterators emit exactly that precomputed count. This also
prevents an infinite iterator when `current + step == current` at large
magnitudes. Host runtime limits may reject a valid but excessive range before
iteration.

Indexing remains one-based. Index zero, negative indexes, and indexes greater
than the finite item count yield `nothing`. Empty iterator terminals return
`nothing`, except boolean quantifiers: `any` is false and `all` is true for an
empty source.

## Host dispatch order

The host captures an immutable subscription snapshot when a logical message is
enqueued. Every captured handler for that message completes before the next
logical message starts. Matching handlers run by descending priority, then by
ascending registration order. Script handlers are registered in program bind
order when `Load` runs; equally prioritized programs therefore follow host load
order. Native subscriptions participate in the same ordering and have no
special script/native precedence.

Detach, unsubscribe, load, or subscribe during dispatch affects only messages
whose snapshots are captured afterward. These rules are independent of frame
budgeting and asynchronous adapters around the synchronous portable host.
