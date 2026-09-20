<!-- Copyright 2026 Stephan Schlöpke -->
<!-- SPDX-License-Identifier: Apache-2.0 -->

# Swift bridge

`GameEventScriptSwiftBridge` is the optional native Swift adapter package. Its
only package dependency is `GameEventScriptRuntime`; it does not require Compiler,
Conformance, macros or Reflection. Foundation is used only by the optional
synchronized Host runner. Runtime remains synchronous and threadless.

Add this local SwiftPM dependency:

```swift
.package(path: "/path/to/GameEventScript/implementation/swift/GameEventScriptSwiftBridge")
```

Add `.product(name: "GameEventScriptSwiftBridge", package: "GameEventScriptSwiftBridge")`
to the consuming target. Import both `GameEventScriptRuntime` and
`GameEventScriptSwiftBridge`. A source-compiling application also adds the separate
Compiler product; a precompiled application needs only Runtime and Bridge.

## Messages and callbacks

```swift
import GameEventScriptRuntime
import GameEventScriptSwiftBridge

let host = try GameEventScriptHost(seed: 123)
let done = try GameEventScriptMessageSignature(name: "Done", parameters: ["value"])
let subscription = try host.subscribe(done) { message, context in
    let value = try Int.fromGesValue(message.arguments[0])
    print(value)
}
host.receive(try GameEventScriptMessage(name: "Done", swiftArguments: [("value", 42)]))
let result = try host.runToCompletion()
subscription.unsubscribe()
```

`subscribeMessageName` also accepts a closure. Both adapters support priorities,
required tags and excluded tags, and return the Runtime's ordinary subscription
handle. Closures are retained at registration and invoked synchronously, without
creating a new callback wrapper on each delivery. Keep Context and borrowed call
arguments within their active invocation. Errors propagate to the existing
Runtime boundary: declared `GameEventScriptExtensionFault` codes survive;
unexpected errors receive the owning Runtime diagnostic.

Ordered `swiftArguments` accept mixed native types and repeated positional
arguments using a `nil` label. Dictionary binding requires an existing signature:

```swift
let signature = try GameEventScriptMessageSignature(name: "Done", parameters: ["name", "score"])
let message = try signature.withSwiftArguments(["score": 42, "name": "Ada"])
// Done(name,score), irrespective of dictionary enumeration order.
```

The dictionary must contain exactly the signature's named arguments. Missing,
extra, duplicate normalized or positional labels are rejected. Context's
`emit` and `publish` have the same ordered `swiftArguments` convenience.
`GameEventScriptSwiftPublishSink` wraps a synchronous outbound closure.

## Native value conversion

Use `GameEventScriptSwiftValue.encode(value)` and
`GameEventScriptSwiftValue.decode(value, as: Int.self)`, or the corresponding
`toGesValue()` / `fromGesValue(_:)` methods. Custom types may implement
`GameEventScriptSwiftValueConvertible` explicitly.

| Swift type | GES representation / decoding |
| --- | --- |
| `Bool` | Boolean only |
| `String` | Text only, with unchanged Unicode scalars |
| `Int`, `UInt`, and their 8/16/32/64-bit variants | Exact unitless Number within both the signed Int64 storage range and the native type's range |
| `Double`, `Float` | Unitless Number; decoding rejects precision loss and narrowing overflow |
| `Optional<T>` | `nil` becomes Nothing; Nothing decodes to `nil` |
| `[T]` | List, recursively converted |
| `[String: T]` | Map in canonical scalar-key order, recursively converted |
| `GesValue` | Passed through unchanged, including specialized kinds and units |

These are native adapter conversions, not source-language `as` casts. Text `"12"`
does not decode to `Int`, and Percentage or a Quantity does not silently become
a plain `Double`. Fractional numbers cannot decode to an integer. Encoding
floating-point values uses the portable factory: NaN becomes Nothing, signed zero
is normalized, and representable integral values use Int64. Use `GesValue`
explicitly for units, Percentage, Tag, Dice and other specialized values.
Conversion errors use `GameEventScriptSwiftConversionError`.

Swift dictionaries compare canonically equivalent String keys as equal; GES
maps distinguish their scalar sequences. Decoding a GES map rejects collisions
such as `é` and `e` plus a combining accent instead of losing an entry. Encoding
cannot recover keys already merged by a Swift dictionary. Use ordered
`GesMapEntry` values when both spellings must remain distinct.

## Extensions

```swift
let extensions = try GameEventScriptSwiftExtensionRegistry([
    GameEventScriptSwiftExtension(namespace: "app", name: "double", parameters: ["_"]) { call in
        let number: Int = try call.arguments.swiftValue(at: 0)
        try call.setSwiftValue(number * 2)
    }
])
let host = try GameEventScriptHost(seed: 123, extensions: extensions)
// A loaded script can call :app.double(21).
```

Registration validates names, ordered argument labels and duplicate signatures.
An optional fallback registry is consulted for signatures absent from the local
registry. Linking resolves the callbacks once. The extension call and argument
view are borrowed; `swiftValue(at:as:)` checks bounds and converts one argument
without materializing the complete argument list.

## External Swift types

Describe exposed fields with KeyPaths or throwing getters and provide explicit
constructor closures. Only declared fields are visible to scripts:

```swift
struct Player {
    let name: String
    let score: Int
}

let player = try GameEventScriptSwiftType<Player>("Player", fields: [
    .init("name", typeName: "Text", keyPath: \Player.name),
    .init("score", typeName: "Number", keyPath: \Player.score),
], constructors: [
    .init(parameters: ["name", "score"]) { arguments in
        try Player(name: arguments.swiftValue(at: 0), score: arguments.swiftValue(at: 1))
    }
])
let types = try GameEventScriptSwiftExternalTypeRegistry([player.binding])
let host = try GameEventScriptHost(seed: 123, externalTypes: types)

let value = player.wrap(Player(name: "Ada", score: 42))
let original = try player.unwrap(value)
```

Pass the same `types` to the Compiler builder's `withExternalTypeCatalog(types)`.
Scripts can then construct `:Player(name: "Ada", score: 42)` and read `player.name`.
The catalog contains declarative definitions; the immutable compiled Program
never retains Swift closures, KeyPaths or native instances. Host linking uses
the registry's separate constructor bindings.

Constructor labels refer to fields, inherit their declared types, and determine
the callback argument order. Field declarations and constructor definitions are
validated before use. A getter can instead return `GesValue` explicitly; the
`kind`/`unit` field initializer supports declared units. Getter and constructor
errors retain the Runtime's existing error classification.

Struct roots retain Swift value semantics; class roots retain the original
object and identity. Native object synchronization remains the embedding's
responsibility. Runtime field coercion and external-map materialization follow
the existing external-value contract. `unwrap` accepts only values produced by
the exact descriptor instance, preventing accidental binding to an unrelated
descriptor with the same name.

## Optional synchronized runner

```swift
let runner = try GameEventScriptSwiftHostRunner(GameEventScriptHost(seed: 123))
let registration = try runner.subscribe(.init(name: "Done", parameters: ["value"])) { message, _ in
    print(message.arguments[0])
}
try runner.receive(.init(name: "Done", swiftArguments: [("value", 42)]))
let result = runner.lastResult
registration.detach()
runner.close()
```

The runner takes exclusive ownership using Swift 6 `sending` parameters and
serializes access with a recursive lock. It is `Sendable`; the underlying Host,
callbacks and external objects must not be accessed concurrently outside it.
The runner exposes synchronized registration handles instead of raw Runtime
handles. Handles retain their runner, contain stable registration IDs and detach
idempotently.

Programs are immutable transport data: `load` retains them without transferring
exclusive ownership, so the same Program can be reused across independent runners.

Each accepted `receive` and successful `load` pumps synchronously on its caller's
thread. A callback's recursive receive only enqueues; it does not recursively
dispatch. Explicit recursive pumping is rejected. There is no background task
or fixed thread affinity, so UI work must use the embedding's own scheduling.
Do not wait inside a callback for another thread to access this runner.

`runToCompletion()` drains work already queued before ownership transfer.
`lastResult` is the synchronized snapshot of the most recent pump, including
diagnostics and limits; another caller can replace it after an operation returns.
Faults and limits do not trigger an automatic retry loop. Subsequent explicit
operations may resume remaining work according to the Host contract. For bounded
game-loop stepping, use Runtime's `executeFrame` directly instead of this runner.
`close()` rejects further receive/load/subscribe/pump calls, detaches owned
registrations and releases the Host; a pump already executing may finish.
Avoid strong callback captures that create ownership cycles, or close explicitly.

## Verification

From the repository root:

```bash
./scripts/test-swift-bridge.sh
./scripts/format-swift.sh
python3 scripts/verify-swift-api.py
```

Native tests cover strict conversion, Unicode dictionary collisions, ordered
message binding, callback diagnostics, descriptors, constructor ordering,
lifecycle and concurrent runner access. The Conformance package's native adapter
tests additionally compile, serialize and execute programs using these bindings.
That Compiler dependency is test-only and is absent from the Bridge package.
No .NET installation is required by this verification script. The full Swift
verification script and CI include both groups.

The [Swift workspace](../GameEventScript.xcworkspace) includes the Bridge. Build
or test its `GameEventScriptSwiftBridge` scheme in Xcode; the shared Conformance
scheme also includes the Bridge tests. Outputs stay below `artifacts/swift`.
The [public API specification](../../../specs/PublicApi.md#swift) owns the binding
contract; shared language and Host semantics continue to use Markdown Conformance.
