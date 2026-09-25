<!-- Copyright 2026 Stephan Schlöpke -->
<!-- SPDX-License-Identifier: Apache-2.0 -->

# Embed GES in Swift

This walkthrough runs a script and receives its output in a Swift closure. It
uses the same program and message flow as the [C# guide](CSharp.md), so the two
examples can be compared directly.

This guide tracks the development checkout. Match the package tag to the
documentation for your release. **Unreleased** features, including the new
binding macros, are listed in the [changelog](../../CHANGELOG.md).

## Add the package

Use Swift 6 or newer. In Xcode, choose **File → Add Package Dependencies** and
enter `https://github.com/schloepke/GameEventScript.git`. This is a package
dependency URL, not a package collection URL.

Select a published tag and add `GameEventScriptRuntime`,
`GameEventScriptCompiler` and `GameEventScriptSwiftBridge` to your target.
`GameEventScriptSyntaxHighlighter` is optional for editors and terminal displays.

For a command-line SwiftPM project, create this `Package.swift` and replace
`<release-version>` with a real [release](https://github.com/schloepke/GameEventScript/releases)
such as `0.1.0`. Features added after that tag require a newer release or a local
checkout dependency.

```swift
// swift-tools-version: 6.0
import PackageDescription

let package = Package(
    name: "GesExample",
    platforms: [.macOS(.v10_15)],
    dependencies: [
        .package(url: "https://github.com/schloepke/GameEventScript.git", exact: "<release-version>")
    ],
    targets: [
        .executableTarget(name: "GesExample", dependencies: [
            .product(name: "GameEventScriptRuntime", package: "GameEventScript"),
            .product(name: "GameEventScriptCompiler", package: "GameEventScript"),
            .product(name: "GameEventScriptSwiftBridge", package: "GameEventScript")
        ])
    ]
)
```

The current root package declares macOS 10.15 to satisfy its SwiftSyntax build
dependency. This is a macOS deployment minimum, not a restriction to Apple
platforms; platform acceptance is recorded in the
[implementation guide](../../implementation/swift/README.md).

## Compile and execute a rule

Save this as `Sources/GesExample/main.swift`:

```swift
import GameEventScriptRuntime
import GameEventScriptCompiler
import GameEventScriptSwiftBridge

let program = try GameEventScriptBuilder.create()
    .addScript("""
        module tutorial.damage
        function damage(base, bonus) be base + bonus
        on Calculate(base, bonus) {
            emit Done(value: damage(base: base, bonus: bonus))
        }
        """, sourceName: "damage.ges")
    .compile()

let host = try GameEventScriptHost.createBuilder()
    .withRandomSeed(123)
    .build()

let done = try GameEventScriptMessageSignature(name: "Done", parameters: ["value"])
let output = try host.subscribe(done) { message, _ in
    print(try Int.fromGesValue(message.arguments[0]))
}
let instance = try host.load(program)

guard try host.start().state == .ready else {
    fatalError("Host startup failed.")
}

host.receive(try GameEventScriptMessage(name: "Calculate", swiftArguments: [
    ("base", 30),
    ("bonus", 12),
]))

let result = try host.runToCompletion()
guard result.state == .completed else {
    fatalError("Execution stopped: \(result.state)")
}

instance.detach()
output.unsubscribe()
```

Run `swift run GesExample`. The closure prints `42`. In an application, place the
code in a throwing function on the host's serial execution path. The
`fatalError` calls make failures visible in this small example; production code
should present or record structured diagnostics instead of terminating the app.

The compiler accepts source text. The host loads the resulting immutable program,
starts the initial group, and queues an external message. `runToCompletion()`
performs dispatch; `receive` alone does not run the script. Argument labels and
their order must match `Calculate(base, bonus)`.

## Use the host lifecycle deliberately

Load all initial programs and register native handlers before calling `start()`.
Initializations run in load order without ordinary message dispatch, and the
host becomes ready only when the entire initial group succeeds.

Loading into a ready host instead queues per-instance initialization. The
instance's `startResult` is absent while pending. Pump the host and inspect that
result before relying on the new program being ready. Failed initialization
removes the affected instance and cancels its captured deliveries.

Retain handles when you need to detach a program or unsubscribe a callback.
Letting a handle leave scope does not automatically remove its registration.
Ordinary detach preserves already captured delivery snapshots.

## Ship Runtime without Compiler

Precompile content with the Swift CLI:

```sh
ges compile damage.ges -o artifacts/damage.gesb
```

Then load bytes provided by your application using
`try GameEventScriptProgramReader.read(bytes)`, where `bytes` is `[UInt8]`.
Your app performs file or network I/O before this call. The reader and host
validate the program; they do not require Compiler.

Use Runtime plus optional SwiftBridge in the shipped app. The public package
contains multiple products, but a Runtime-only build does not compile the
Compiler or bridge macros.

## Bind native data with annotations

> **Since: Unreleased**

SwiftBridge macros generate explicit field descriptors:

```swift
@GesType("BotState")
struct BotState {
    @GesField
    let energy: Double

    @GesField(unit: .meter)
    let distance: Double
}

let bot = try BotState.createGesType()
let registry = try GameEventScriptSwiftExternalTypeRegistry([bot.binding])
let value = bot.wrap(BotState(energy: 100, distance: 4))
```

Supply the registry to the compiler's `withExternalTypeCatalog` and the host
builder's `withExternalTypeRegistry`. Keep the descriptor used to wrap values;
unwrapping requires that same descriptor. Add `@GesConstruct` only when scripts
should also be allowed to construct the type. Unannotated members remain hidden.

SwiftSyntax is a build-time dependency, including in consuming applications. The
generated descriptors do not require SwiftSyntax at runtime. Manual KeyPath and
getter bindings remain available for foreign or unsupported native types.
See the [SwiftBridge guide](../../implementation/swift/GameEventScriptSwiftBridge/README.md)
for complete binding examples and conversion guarantees.

## Integrate with your event loop

The portable host is synchronous and expects serialized access. It does not
create threads or implicitly move callbacks to the main actor. Keep application
UI work on the appropriate actor.

> **Since: Unreleased**

Future-only work returns `.waiting`. Consult `nextMessageDelay`
and arrange a later pump, or use the optional synchronized Swift host runner.
`runToCompletion()` does not sleep until delayed messages become due. The
runner takes exclusive ownership of the host; use its API instead of accessing
the raw host concurrently.

Next: [learn the language](Language.md), explore the
[Swift implementation](../../implementation/swift/README.md), and consult
[host runtime](../../specs/HostRuntime.md) for ordering and failure guarantees.
