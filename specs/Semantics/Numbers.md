<!-- Copyright 2026 Stephan Schlöpke -->
<!-- SPDX-License-Identifier: Apache-2.0 -->

# Portable number semantics

This document is the normative numeric contract for Game Event Script compiler,
runtime, `.gesb`, and portable Conformance implementations. Every language port
must implement these results independently of its language's
default overflow, conversion, formatting, or math-library conventions.

## Numeric representations

- Integer values are signed 64-bit integers in `[-2^63, 2^63 - 1]`.
- Fractional values use IEEE 754 binary64. `.gesb` stores their exact 64 bits in
  little-endian form; implementations must not serialize a host-language object
  layout.
- A finite binary64 result that is exactly integral and lies in the Int64 range
  is normally stored as an integer. The Int64 upper test is strictly `< 2^63`,
  not `<= (double)Int64.MaxValue`, because `Int64.MaxValue` rounds to `2^63` in
  binary64.
- Percentage values store their ratio as binary64. For example, `10%` stores
  `0.1`.
- Quantity units do not change the numeric representation.

The integer/float storage distinction is not an arbitrary-precision promise.
`is integer` also accepts finite, exactly integral binary64 values as described
in the language specification.

## Integer arithmetic and overflow

Integer `+`, `-`, and `*` first compute an exact signed-64 result without using
the host language's ambient checked/unchecked behavior. If the mathematical
result fits Int64, the result remains an integer. On overflow, both operands are
converted to binary64 and the operation is repeated as binary64. This can lose
integer precision, but it never wraps, traps, or invokes C++ signed-overflow
undefined behavior.

After binary64 fallback, normal value canonicalization still applies. Therefore
an overflow approximation that rounds to an exactly integral in-range binary64
value may be stored as an integer. This is a representation detail; all ports
must produce the same numeric value. Unary negation and `abs` of `-2^63` likewise
produce binary64 `2^63`.

Compiler constant folding and VM execution must use the same helpers and yield
the same result. In particular, operands above `2^53` must not be converted to
binary64 before an exact in-range integer operation.

## Binary64 special values

- A scalar NaN is the internal marker for invalid mathematics or numeric
  conversion and canonicalizes to `nothing` at the value boundary. It is never
  an observable scalar number. A conversion implementation may instead report
  an invalid result directly; this has the same script-visible outcome.
- Positive and negative infinity are observable binary64 values. Operators use
  the explicit rules in the language and bytecode specifications; scalar `/`
  follows IEEE division, including division by zero.
- Every stored numeric zero is canonicalized to positive zero. This applies to
  scalar floats, percentages, vector/point components, and floating ranges.
  Consequently `-0` formats as `0` and compares equal to `0`.
- A vector or point with a NaN component is invalid and canonicalizes to
  `nothing`. Floating ranges already require finite bounds and step.

## Conversion and rounding

An existing Int64 number cast with `as :Number` retains its exact value and
quantity unit. The cast must not pass that value through Binary64, including
when the result is used as a dynamic random seed. Compiler constant evaluation
and runtime conversion use the same rules.

Explicit text-to-Number conversion follows the text grammar and value rules in
[Text and number conversion](#text-and-number-conversion). These rules apply
equally to literals, named constants, and values received at runtime.

Binary64-to-Int64 conversion first truncates toward zero and then saturates:

| Input | Result |
| --- | --- |
| NaN | `0` for low-level conversion helpers; observable NaN has already become `nothing` |
| `+Infinity` or value `>= 2^63` | `Int64.MaxValue` |
| `-Infinity` or value `< -2^63` | `Int64.MinValue` |
| otherwise | truncation toward zero |

Integer-valued intrinsics use the following definitions and then the same
saturating conversion:

- `floor`: toward negative infinity.
- `ceil`: toward positive infinity.
- `truncate`: toward zero.
- `round half even`: nearest; an exact half selects the even integer.
- `round half up`: nearest; an exact half goes away from zero.
- `round half down`: nearest; an exact half goes toward zero.

These names describe the existing DSL semantics; they are not delegated to a
host language's similarly named default overload.

## Text and number conversion

### Accepted numeric text

An explicit `as :Number` or `as :Percentage` cast consumes one complete numeric
text token. It ignores ASCII space (`U+0020`) and tab (`U+0009`) before and after
the token.
No whitespace is allowed inside it. The grammar is:

```text
NumericText = Padding NumberToken Padding
Padding     = { " " | "\t" }
NumberToken = (Finite | Infinity | "NaN") [Suffix]
Finite      = [Sign] (Digits | GroupedDigits) ["." Digits] [Exponent]
Infinity    = [Sign] "Infinity"
Exponent    = ("e" | "E") [Sign] Digits
Sign        = "+" | "-"
Digits      = Digit {["_"] Digit}
GroupedDigits = FirstGroup "," ThreeDigits {"," ThreeDigits}
FirstGroup  = Digit [Digit [Digit]]
ThreeDigits = Digit Digit Digit
Digit       = "0".."9"
Suffix      = "m" | "s" | "°" | "%"
```

All digits and signs are ASCII. Special tokens and suffixes are case-sensitive.
Leading zeroes are allowed in the integer part and exponent. Both `e` and `E`,
an explicit positive exponent sign, and exponent leading zeroes are accepted.
Thus `1000`, `100.2`, `1.02e20`, `1e03`, and `1E+003` are valid inputs.

Single underscores may separate adjacent digits, matching numeric source
literals. They may occur in the integer part, fractional part, or text
exponent, without imposing a group size. They may not lead, trail, repeat, or
touch the decimal point, exponent marker, sign, or suffix. Thus `1_003.33`,
`1_223.32`, `1000.2_5`, and `1e0_3` are valid.

A comma is an invariant thousands separator in the integer part only. The
first group has one to three digits; every subsequent group has exactly three.
Thus `1,003.24` and `1,234,567.89` are valid, and `1,003` means `1003`, never
`1.003`. Commas and underscores must not be combined in one numeric token.
Commas are not allowed in the fractional part or exponent. Separator validation
precedes removal; a parser must not simply discard all commas or underscores.

Decimal commas, malformed grouping, hexadecimal notation, non-ASCII digits,
trailing signs, multiple suffixes, unknown suffixes, and unconsumed characters
are invalid. A decimal point requires digits on both sides. An exponent
requires at least one digit. Examples of invalid input are `12,34`, `100,2`,
`1__000`, `1_.25`, `1,_000`, `1,000.2_5`, `.5`, `1.`, `1e`, `1e+`, `1000-`,
`10 m`, and `10ms`.

Invalid input is a failed numeric conversion. An internal NaN or invalid-result
marker is normalized to script-visible `nothing`, just as invalid mathematics
is. Explicit `NaN` text has the same result. There is no observable scalar NaN
and no script exception for a failed text cast.

This grammar belongs to runtime text conversion. It does not extend numeric
source literals, whose syntax is owned by [Language](../Language.md).

### Shared conversion and source token boundaries

Compiler literal decoding and explicit text casts must use the same decimal
value, precision, and rounding rules for corresponding numeric input. A shared
numeric conversion core can serve both paths; token recognition and the target
value kind remain responsibilities of the caller.

The source lexer still applies the source-literal grammar before conversion.
In particular, source commas separate arguments and list items: `[1,003]`
contains two items, `1` and `3`, whereas `'1,003' as :Number` yields `1003`.
Source underscores follow the common digit-separator rule. Text-only padding,
signs, exponent notation, special-value spellings, and comma grouping do not
implicitly become source-token syntax. Source signs and mathematical constants
retain their expression-level meaning.

A Percentage literal constructs a Percentage value; `as :Number` reads the
numeric ratio as defined below. A common conversion core must preserve this
distinction. Invalid source syntax remains a structured compiler diagnostic;
the runtime cast's `nothing` result must not silently replace a malformed source
literal.

### Numeric value and suffix interpretation

For `as :Number`, the decimal significand and exponent denote one exact
mathematical decimal value before conversion to GES storage:

- With no suffix, the result is a unitless Number.
- With `m`, `s`, or `°`, the result is a Number carrying that Quantity unit.
  The suffix neither rescales nor rounds the numeric value.
- With `%`, the mathematical decimal value is divided by exactly 100 before
  numeric conversion. The result of `as :Number` is the unitless ratio, not a
  Percentage value. This is the same numeric view as casting a Percentage to
  Number. The `%` suffix is responsible for the scaling.

If the resulting exact mathematical value is an integer within signed Int64,
it is parsed directly and exactly as Int64, irrespective of decimal or exponent
notation. In particular, `9007199254740993`, `9007199254740993.0`, and
`9007199254740993e0` must all retain that integer. Binary64 is not an intermediate
representation for this case.

All other finite decimal inputs are rounded once to the nearest Binary64 value,
with exact halfway cases selecting the even significand. Normal GES numeric
storage canonicalization then applies. Overflow yields signed infinity;
underflow follows the same rounding rule through subnormal values to zero.
Zero is canonicalized to positive zero. Parsing does not saturate at Int64
bounds and does not use the runtime equality ULP tolerance.

Percentage scaling is part of the exact decimal interpretation, before that
single rounding step. Parsing a percentage by first rounding its displayed
number to Binary64 and then dividing by 100 is permitted only if it produces
the same required result. This also avoids intermediate overflow for a finite
ratio whose percentage magnitude exceeds the Binary64 range.

`Infinity`, `+Infinity`, and `-Infinity` produce the corresponding signed
Binary64 infinity. A Quantity suffix is retained; `%` leaves the infinity
unitless. `NaN` produces `nothing` with or without a suffix.

| Text input | Result of `as :Number` |
| --- | --- |
| `1000` or `1e03` | Int64 `1000` |
| `1_003.33` | nearest Binary64 to decimal `1003.33` |
| `1_223.32` | nearest Binary64 to decimal `1223.32` |
| `1,003.24` | nearest Binary64 to decimal `1003.24` |
| `100.2` | nearest Binary64 to decimal `100.2` |
| `1.02e20` | nearest Binary64 to decimal `1.02e20` |
| `1000.01` or `1.00001e03` | the same Binary64 value |
| `1.00001e02` | nearest Binary64 to decimal `100.001` |
| `1e03m` | Int64 Quantity `1000m` |
| `100.2s` | Binary64 Quantity with unit `s` |
| `90°` | Int64 Quantity `90°` |
| `10%` or `1e1%` | unitless Number with the same numeric value as `0.1` |
| `100%` | unitless Int64 `1` |

### Conversion to Percentage

For a numeric input, `as :Percentage` always interprets the finite, unitless
numeric view as a ratio and stores that ratio as Binary64. It does not divide
by 100 based on integer storage, integrality, sign, or magnitude. Ratios greater
than one and negative ratios are valid; this cast does not clamp probabilities.
An existing Percentage is preserved, including ratios outside `[-1, 1]`.
Int64 inputs are converted to the nearest Binary64 ratio using ties to even.

Text input uses the numeric text grammar and exact decimal interpretation above:

- A token without a suffix denotes the ratio directly, just like numeric input.
- A token with `%` denotes a percentage magnitude. Its exact decimal value is
  divided by 100 once, before rounding to the Binary64 ratio.
- A Quantity suffix is invalid for this target type.

The parsed ratio is rounded once to Binary64 with ties to even, and zero is
canonicalized to positive zero. A finite ratio produces a Percentage value.
Quantity inputs, non-finite ratios, invalid numeric text, unsupported values,
and `nothing` produce `nothing`. No branch reinterprets a parsed ratio as a
percentage magnitude based on its value or storage kind.

| Expression | Percentage value |
| --- | --- |
| `0.1 as :Percentage` | `10%` |
| `1 as :Percentage` | `100%` |
| `1.02 as :Percentage` | `102%` |
| `20 as :Percentage` | `2000%` |
| `(1 / 100) as :Percentage` | `1%` |
| `(20 / 100) as :Percentage` | `20%` |
| `'0.1' as :Percentage` | `10%` |
| `'20' as :Percentage` | `2000%` |
| `'10%' as :Percentage` | `10%` |
| `'102%' as :Percentage` | `102%` |
| `102% as :Percentage` | `102%` |

For every Percentage value `p`, each of the following must recover its exact
stored Binary64 ratio and Percentage kind after zero canonicalization:

```ges
(p as :Number) as :Percentage
(p as :Text) as :Percentage
((p as :Text) as :Number) as :Percentage
((p as :Number) as :Text) as :Percentage
```

This includes zero, negative values, `100%`, values above `100%`, subnormal
ratios, and very large finite ratios. These guarantees start with a Percentage,
whose representation is Binary64. They do not imply lossless conversion of
every arbitrary Int64 Number to Percentage and back when that integer cannot
be represented exactly in Binary64.

### Numeric text output and roundtrip

Numeric `as :Text` output is culture-independent and must be accepted by the
numeric text grammar above. A formatter may choose decimal or exponential
notation, `e` or `E`, exponent padding, and optional insignificant zeroes.
The language does not require a shortest spelling or a particular threshold
for switching notation. Output has no surrounding whitespace, comma grouping,
or underscore separators. Numeric zero is written as `0`, followed by its suffix
when present.
Infinity is written as `Infinity` or `-Infinity`, with a Quantity suffix when
present. An observable scalar NaN does not exist.

For every unitless Number or scalar Quantity `n`, parsing its formatted text
must recover its exact numeric value and unit after normal GES storage
canonicalization. Int64 roundtrips preserve every bit. Binary64 roundtrips
preserve every bit after zero and integral-storage canonicalization; approximate
numeric equality is insufficient. This guarantee holds when one conforming
implementation writes the text and another reads it.

Quantity formatting appends its unit suffix. Percentage formatting appends `%`
and expresses the stored ratio as a decimal percentage magnitude. Parsing
that text with `as :Number` must recover the Percentage's numeric ratio after
normal Number canonicalization. This guarantee preserves the numeric meaning;
`as :Number` does not preserve the Percentage runtime kind. A formatter must
choose enough digits to satisfy this rule, including subnormal and very large
finite ratios. Rounding an intermediate Binary64 product by 100 must not break
the roundtrip. If the ratio's Number view canonicalizes to Int64, the text must
recover that exact integer, not a nearby decimal integer that would merely
round to the same Binary64 bits.
Parsing the same formatted text with `as :Percentage` restores the Percentage
kind and exact ratio as defined in [Conversion to Percentage](#conversion-to-percentage).

For example, `1000.01` and `1.00001e03` are both permitted output spellings for
the same Number. Parsing `1e03` yields `1000`; its subsequent text may be `1000`
or an equivalent exponent spelling. There is no guarantee of retaining the
original input spelling or of producing an expanded decimal display.

A fixed implementation and version must format the same canonical value
consistently across compiler evaluation, VM execution, concatenation, and
numeric components of nested value formatting. Formatting must not depend on
ambient locale or call history. Different implementations or runtime versions
may choose different permitted spellings. Text equality, ordering, length, and
indexing continue to observe the actual text exactly; they do not compare the
numeric meanings of differently spelled texts.

The input grammar, separator validation, suffix interpretation, exact Int64
selection, and Binary64 rounding above are language rules on every platform.
A platform library's accepted spellings, grouping rules, locale, or default
numeric representation do not define GES behavior.

The implementation may use its platform's numeric library or a dedicated
conversion algorithm for decimal/binary conversion. Either choice must enforce
the complete input grammar, exact conversion and roundtrip rules, including
input produced by another port. Recognizing a numeric token and converting its
exact decimal value to storage are separate responsibilities; a shared literal
scanner alone does not establish correctly rounded numeric conversion.
The contract does not prescribe a parser or formatter implementation.

## Division, modulo, and remainder

- `/` converts scalar operands to binary64 and performs IEEE binary64 division.
  `1 / 0` is `+Infinity`, `-1 / 0` is `-Infinity`, and `0 / 0` becomes `nothing`
  through NaN canonicalization.
- `div` is floor division: `floor(a / b)`. Integer operands use exact integer
  quotient/remainder arithmetic so values above `2^53` do not lose precision.
  The exceptional integer case `Int64.MinValue div -1` produces binary64 `2^63`.
- `rem` is the truncating remainder. A non-zero result has the sign of the left
  operand: `-7 rem 3 = -1`, `7 rem -3 = 1`.
- `mod` is the floor-modulo companion of `div`. A non-zero result has the sign
  of the right operand: `-7 mod 3 = 2`, `7 mod -3 = -2`.
- `mod` or `rem` by zero is invalid mathematics and becomes `nothing`.
- `Int64.MinValue mod -1` and `Int64.MinValue rem -1` are exactly integer zero;
  they do not overflow. Integer `div` by zero uses the same infinity or
  `nothing` outcomes as `/` by zero.
- For binary64 operands, `rem` uses the IEEE/C `fmod`-style truncating remainder;
  `mod` adjusts a non-zero remainder once by the divisor to obtain its sign.

## Comparisons

Integer-only equality is exact. Whenever binary64 participates, runtime numeric
equality uses distance in the ordered IEEE 754 representation and accepts at
most two ULPs. NaN is never equal; infinities are equal only to the same signed
infinity; positive and negative zero are equal.

Ordering operators use ordinary IEEE comparisons after the language's type and
unit checks. They do not apply the equality ULP window.

## Transcendental functions

`sin`, `cos`, `tan`, inverse trigonometric functions, `atan2`, `ln`, `exp`, and
non-integer `power` use the target platform's IEEE binary64 math library after
the DSL's unit, domain, absence, and special-value checks. Exact bit identity
between different correctly rounded or near-correctly-rounded math libraries is
not required.

Conformance expectations therefore compare finite binary64 results by ULP distance.
The default is 4096 ULPs, allowing bounded cross-library differences. A test or individual step
can set `maxFloatUlps`; `0` requires bit-exact finite results. NaN/nothing,
infinities, kinds, units, integers, booleans, text, and structure remain exact.
Runtime `=` is unaffected and always uses its two-ULP rule.

## Canonical conformance scalar encoding

The YAML value schema described here is test interchange data and does not
define a product wire-message format.

- Int64 and binary64 payloads are strings so YAML implementations cannot first
  round them through an implementation-defined numeric type.
- A finite binary64 is written as the shortest decimal that parses back to the
  same binary64 bits.
- Decimal point is `.`, exponent marker is lowercase `e`, a positive exponent
  has no `+`, and exponent leading zeroes are removed.
- Both signed zeroes write as `0`.
- Infinity writes as `Infinity` or `-Infinity`.
- `NaN` is accepted in expected YAML as the spelling of invalid mathematics and
  decodes to `nothing`; it is not an observable scalar NaN value.

These canonical spellings are the Conformance transport representation. They
do not constrain the permitted numeric `as :Text` output spellings defined
above. In particular, a `:Text` payload is ordinary exact text; the transport
must not rewrite it merely because its contents resemble a number.

Examples: `0`, `0.1`, `1e20`, `5e-324`, `Infinity`, `-Infinity`.

## Port requirements

Each language port must have direct tests for:

- exact and overflowing Int64 add/subtract/multiply, including operands above
  `2^53`;
- the exclusive `2^63` conversion boundary and saturation;
- negative `div`, `mod`, and `rem` combinations;
- all midpoint rounding modes and infinities;
- ULP ordering across negative values, both zeroes, infinities, and NaN;
- decimal and exponent input, underscore separators, strict text-only comma
  grouping, exact Int64 text conversion, Binary64 halfway and range boundaries,
  Quantity suffixes, and exact Percentage scaling;
- matching source-literal and text-cast numeric conversion with source token
  boundaries and error handling preserved;
- ratio-based Percentage casts and exact Percentage/Number/Text roundtrips,
  including integers, negative values, and percentages at or above `100%`;
- numeric text roundtrip with permitted output spellings and special tokens;
- canonical Conformance scalar encoding and exponent normalization.
