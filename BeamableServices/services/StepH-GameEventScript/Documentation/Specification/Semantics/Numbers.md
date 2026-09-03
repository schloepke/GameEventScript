# Portable number semantics

This document is the normative numeric contract for Game Event Script compiler,
runtime, `.gesb`, and JSON conformance implementations. Swift, Kotlin, C++, C#,
and future ports must implement these results independently of their language's
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

- A scalar NaN is the internal marker for invalid mathematics and canonicalizes
  to `nothing` at the value boundary. It is never an observable scalar number.
- Positive and negative infinity are observable binary64 values. Operators use
  the explicit rules in the language and bytecode specifications; scalar `/`
  follows IEEE division, including division by zero.
- Every stored numeric zero is canonicalized to positive zero. This applies to
  scalar floats, percentages, vector/point components, and floating ranges.
  Consequently `-0` formats as `0` and compares equal to `0`.
- A vector or point with a NaN component is invalid and canonicalizes to
  `nothing`. Floating ranges already require finite bounds and step.

## Conversion and rounding

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

The YAML value schema described here is test interchange data, not the future
product wire-message format.

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

Explicit numeric-to-text casts and nested value text formatting use the same
canonical finite/special binary64 spelling, followed by any DSL unit suffix.

Examples: `0`, `0.1`, `1e20`, `5e-324`, `Infinity`, `-Infinity`.

## Port requirements

Each language port must have direct tests for:

- exact and overflowing Int64 add/subtract/multiply, including operands above
  `2^53`;
- the exclusive `2^63` conversion boundary and saturation;
- negative `div`, `mod`, and `rem` combinations;
- all midpoint rounding modes and infinities;
- ULP ordering across negative values, both zeroes, infinities, and NaN;
- canonical float text roundtrip, exponent normalization, and special tokens.
