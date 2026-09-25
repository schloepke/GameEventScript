<!-- Copyright 2026 Stephan Schlöpke -->
<!-- SPDX-License-Identifier: Apache-2.0 -->

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
| `:test.toJson _` | exactly one value | invokes the product value-envelope writer; stable codec failures become declared faults with the same code |
| `:test.fromJson _` | exactly one Text value | invokes the product value-envelope reader; non-Text yields Nothing and stable codec failures become declared faults |
| `:test.echo _` | exactly one value | the same value and value kind without conversion; otherwise `nothing` |
| `:test.notify _` | exactly one value | emits `Effect(value: _)` locally and returns the same value without conversion |
| `:test.truth()` | no values | `true` |
| `:test.fail()` | no values | throws an unanticipated native failure for short-circuit and diagnostic tests |
| `:test.declaredFault()` | no values | deliberately reports a defined runtime fault with stable code `test.declaredFault` |
| `:test.failWithRandomScope()` | no values | opens a random scope with seed `7`, draws one inclusive integer from `1` to `100`, then throws an unanticipated native failure without closing the scope |
| `:test.declaredFaultWithRandomScope()` | no values | opens the same scope and draws as `test.failWithRandomScope`, then deliberately reports `test.declaredFault` without closing the scope |

Names, labels and arity are exact. Unknown references do not link. Integer
inputs accepted through numeric access follow the ordinary runtime numeric
conversion contract. `test.fail` exists only to prove that an expression was or
was not evaluated, or that an unanticipated extension exception is reported with
the stable `runtime.extensionCallFailed` code; its platform exception text is
nonnormative. `test.declaredFault` proves that a deliberately reported extension
fault keeps its own stable code and phase instead of that generic code.
The variants with an open random scope verify that boundary cleanup preserves
the original diagnostic and restores the parent random stream.

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
case must not depend on a language-specific backing object.

## External type `CallbackProbe`

The fixed V1 external-type catalog also declares
`CallbackProbe(failure: Text, context: Text)` with fields `failure: Text`,
`context: Text`, and `value: Float<UnitNone>` in that order. The first two fields
retain the constructor inputs. The fixture provides controlled callback failures
for the portable diagnostic contract.

| `failure` | Behavior |
| --- | --- |
| `constructorUnexpected` | Construction raises an unanticipated platform failure. |
| `constructorDeclared` | Construction reports the deliberate diagnostic below with symbol `CallbackProbe`. |
| `fieldUnexpected` | Construction succeeds; reading `value` raises an unanticipated platform failure. |
| `fieldDeclared` | Construction succeeds; reading `value` reports the deliberate diagnostic below with symbol `CallbackProbe.value`. |

Deliberate diagnostics have phase `runtime` and code `test.callbackFault`.
`context` controls the context supplied by the fixture before Host/VM enrichment:

| `context` | `programName` | `handlerName` |
| --- | --- | --- |
| `none` | absent | absent |
| `program` | `reported.program` | absent |
| `handler` | absent | `Reported()` |
| `both` | `reported.program` | `Reported()` |

Unexpected failures supply no diagnostic context of their own. Unknown `failure`
or `context` values make construction return `nothing`; unknown field names return
no value. Exception types and technical details are platform-specific and must
not decide these cases. Each port implements its own failure transport; C# uses
`GameEventScriptExtensionFaultException` or the trusted fatal-runtime base when
supplying explicit context. No Reflection or host-bound CLR object is required.

## External type `SpatialProbe`

The fixed catalog declares `SpatialProbe(vector: Vector, point: Point)` with
four fields, in order: `vector: Vector`, `point: Point`, `storedVector: Vector`,
and `storedPoint: Point`. None of these declarations or constructor parameters
specifies a unit constraint. The constructor retains the converted arguments
as `vector` and `point`. Independently of those arguments, `storedVector` returns
`:Vector(1m, 2m, 3m)` and `storedPoint` returns `:Point(4s, 5s, 6s)`.
Unknown field names return no value. Direct field access and Map materialization
must preserve the units as required by the external-type API contract.

## Declarative Host environment

Native handlers, publish sinks and Host lifecycle actions use the closed forms
specified in [Markdown format](MarkdownFormat.md). A runner must not resolve a class name,
method name, script fragment, assembly, reflection target or platform callback
from test metadata. Adding another portable operation requires a named,
versioned addition to this document and matching conformance coverage.
