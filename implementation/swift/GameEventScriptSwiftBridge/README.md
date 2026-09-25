<!-- Copyright 2026 Stephan Schlöpke -->
<!-- SPDX-License-Identifier: Apache-2.0 -->

# Swift bridge

`GameEventScriptSwiftBridge` is the optional native Swift adapter package. Its
only GES dependency is `GameEventScriptRuntime`; it does not require Compiler,
Conformance or Reflection. Its internal macro target additionally uses the official
SwiftSyntax package at build time. Foundation is used only by the optional
synchronized Host runner. Runtime remains synchronous and threadless.

Add this local SwiftPM dependency:

```swift
.package(path: "/path/to/GameEventScript/implementation/swift/GameEventScriptSwiftBridge")
```

Add `.product(name: "GameEventScriptSwiftBridge", package: "GameEventScriptSwiftBridge")`
to the consuming target. Import both `GameEventScriptRuntime` and
`GameEventScriptSwiftBridge`. A source-compiling application also adds the separate
Compiler product; a precompiled application needs only Runtime and Bridge.

## Annotation-based external types

Import `GameEventScriptSwiftBridge` and annotate the native data you want to expose:

```swift
@GesType("BotState")
struct BotState {
    @GesField
    let energy: Double

    @GesField(unit: .meter)
    let distance: Double

    @GesField("isNearWall")
    var nearWall: Bool { distance < 5 }

    @GesConstruct
    init(energy: Double, distance: Double) {
        self.energy = energy
        self.distance = distance
    }
}

let bot = try BotState.createGesType()
let registry = try GameEventScriptSwiftExternalTypeRegistry([bot.binding])
let value = bot.wrap(BotState(energy: 100, distance: 4))
```

Pass the same registry to the compiler's `withExternalTypeCatalog` and the host's
`withExternalTypeRegistry`. Retain `bot`: each `createGesType()` creates a distinct
descriptor, and `unwrap` requires the original descriptor. No global registry or
shared mutable binding cache is introduced.

Only annotated properties and constructors are exposed. `@GesType` supports
nongeneric structs and final classes; omitted names use the Swift type/property
name. Computed properties are read when accessed, not when registering. Fields
must have explicit Swift type annotations. Bool, String, standard fixed-width
integers, Float/Double, Optional, Array and Dictionary infer the corresponding
GES type. Aliases, custom convertible types and `GesValue` require an explicit
`@GesField(typeName: "...")`. For example, a `GesValue` getter returning a Tag uses
`@GesField(typeName: "Tag")`.

`unit: .meter`, `.second` or `.degree` explicitly maps native numeric magnitudes
to quantities, without scaling. Constructor decoding requires exactly that unit
before applying the normal lossless native conversion. Units cannot be combined
with `typeName`; nonnumeric fields cannot use a numeric unit annotation.

`@GesConstruct` supports synchronous, nonfailable initializers and synchronous
static factory functions returning the enclosing type. They may throw. Parameters
match exposed field names by external label, then Swift property names by internal
parameter name. Defaults do not create additional GES overloads. Unannotated
constructors, including implicit memberwise initializers, remain unavailable to
scripts. Async, variadic, inout and generic constructors are unsupported. Conditional
`#if` field/constructor declarations require manual bindings, so marked members
cannot silently disappear from a generated descriptor.

The annotations expand into the existing manual descriptors. SwiftSyntax and the
macro plugin run on the build host, including consumers' builds; they are not
application runtime dependencies. The package pins SwiftSyntax 600.0.1 to preserve
its Swift 6.0 minimum toolchain contract. Both the root distribution and local
Bridge manifest explicitly declare macOS 10.15 to match SwiftSyntax's deployment
minimum; SwiftPM consumers must select this or a newer macOS target. There is one
Bridge library product and one import; no separate public macro product is needed. Runtime-only and
Compiler-only builds do not compile the macro target. Existing manual bindings
remain useful for foreign types, generic types and custom throwing getters.

## Messages and callbacks

```swift
import GameEventScriptRuntime
import GameEventScriptSwiftBridge

let host = try GameEventScriptHost.createBuilder().withRandomSeed(123).build()
let done = try GameEventScriptMessageSignature(name: "Done", parameters: ["value"])
let subscription = try host.subscribe(done) { message, context in
    let value = try Int.fromGesValue(message.arguments[0])
    print(value)
}
guard try host.start().state == .ready else { fatalError("Host startup failed") }
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
let runner = try GameEventScriptSwiftHostRunner(GameEventScriptHost.createBuilder().withRandomSeed(123).build())
let registration = try runner.subscribe(.init(name: "Done", parameters: ["value"])) { message, _ in
    print(message.arguments[0])
}
guard try runner.start().state == .ready else { fatalError("Host startup failed") }
try runner.receive(.init(name: "Done", swiftArguments: [("value", 42)]))
_ = try runner.runToCompletion() // Optional synchronous wait before inspecting output.
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

A loading host waits for explicit `start()`. After successful Start, accepted
receives and later loads schedule work on a shared serial background dispatcher.
An already ready transferred host schedules its pending work automatically.
A callback's recursive receive only enqueues; recursive pumping is rejected.
Do not wait inside a callback for another thread to access this runner.

`runToCompletion()` drains a ready host synchronously under the same gate.
`lastResult` is the latest pump outcome; `isReady` reports the initial Start barrier.
Program registrations expose their optional `startResult`, including after init
failure. Automatic pumping continues while the ready host has work. For bounded
game-loop stepping, use Runtime's `executeFrame` directly.
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
