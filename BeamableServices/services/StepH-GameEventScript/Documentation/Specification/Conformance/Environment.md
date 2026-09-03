# Conformance environment specification

This document defines the small, deterministic catalog used by portable
Conformance Markdown cases. It is test infrastructure, not an extension bundle
that a product Host must install. Every language port advertising the relevant
capability must provide equivalent definitions and behavior to its conformance
runner without Reflection or arbitrary code embedded in a test document.

## Extension operations

The fixed V1 extension registry contains:

| Reference | Arguments | Result |
| --- | --- | --- |
| `:math.floor _` | one numeric value | Binary64 floor with the input unit; otherwise `nothing` |
| `:math.max _...` | one or more unlabeled numeric values | maximum as Binary64; no values produce `nothing` |
| `:nav.shortestTurn from: _ to: _` | two numeric angles | `((to - from + 540) mod 360) - 180` in degrees |
| `value is :nav.isNorth` / `:nav.isNorth value` | one numeric angle | true when the angle wrapped to `[0,360)` is at most 45 or at least 315 |
| `:test.vectorSum _` | one vector | `x + y + z` as Binary64 with the vector unit; otherwise `nothing` |
| `:test.echo _` | exactly one value | the same value and value kind without conversion; otherwise `nothing` |
| `:test.truth()` | no values | `true` |
| `:test.fail()` | no values | throws a native failure for short-circuit and diagnostic tests |

Names, labels and arity are exact. Unknown references do not link. Integer
inputs accepted through numeric access follow the ordinary runtime numeric
conversion contract. `test.fail` exists only to prove that an expression was or
was not evaluated; its platform exception text is nonnormative.

## External type `aim`

The fixed V1 external-type catalog declares `aim` with this constructor:

```text
aim(bearing: Float<Degree>, range: Float<Meter>,
    steps: Float<Meter>, direction: Vector<Meter>)
```

Its fields, in catalog order, are:

```text
bearing: Float<Degree>
range: Float<Meter>
steps: Float<Meter>
direction: Vector<Meter>
checksum: Float<UnitNone>
```

The runtime constructor retains the four converted arguments and sets
`checksum` to the Binary64 representation of the Int64 truncation of
`bearing + range + steps`. Field lookup by any other name returns no value.
The runtime value exposes only `IGameEventScriptExternalValue`; the portable
case must not depend on a CLR/Kotlin/Swift/C++ backing object.

## Declarative Host environment

Native handlers, publish sinks and Host lifecycle actions use the closed forms
specified in [Markdown format](MarkdownFormat.md). A runner must not resolve a class name,
method name, script fragment, assembly, reflection target or platform callback
from test metadata. Adding another portable operation requires a named,
versioned addition to this document and matching conformance coverage.
