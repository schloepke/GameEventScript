<!-- Copyright 2026 Stephan Schlöpke -->
<!-- SPDX-License-Identifier: Apache-2.0 -->

# Embed GES in C#

This walkthrough compiles a script, loads it into a host and receives its result
in a C# callback. Your application owns input, persistent state and output; the
script supplies the event-driven rule.

This guide tracks the development checkout. Use matching package versions and
check the [changelog](../../CHANGELOG.md) before using new APIs with a published
release. The complete API contract is in [Public API](../../specs/PublicApi.md).

## Create a console application

Use a .NET 8 or newer application for this example. The libraries themselves
target .NET Standard 2.1.

```sh
dotnet new console -n GesExample
cd GesExample
dotnet add package GameEventScript.Runtime
dotnet add package GameEventScript.Compiler
dotnet add package GameEventScript.CSharpBridge
```

Without `--version`, NuGet selects the latest stable package. To pin a release,
add `--version <release-version>` to each package command, using the same version.
To use unreleased functionality, use project references to a matching repository
checkout instead; see the [implementation guide](../../implementation/csharp/README.md).

| Package | When you need it |
| --- | --- |
| Runtime | Always: values, programs and execution |
| Compiler | When your application compiles source text |
| CSharpBridge | Delegate callbacks, native bindings and optional background pumping |
| SyntaxHighlighter | Optional editor or terminal highlighting |

## Compile and execute a rule

Replace `Program.cs` with this complete example:

```csharp
using System;
using GameEventScript.Api;
using GameEventScript.CSharpBridge;
using GameEventScript.Runtime.Values;

var program = GameEventScriptBuilder.Create()
    .AddScript("""
        module tutorial.damage
        function damage(base, bonus) be base + bonus
        on Calculate(base, bonus) {
            emit Done(value: damage(base: base, bonus: bonus))
        }
        """, "damage.ges")
    .Compile();

var host = GameEventScriptHost.CreateBuilder()
    .WithRandomSeed(123)
    .Build();

var output = host.Subscribe("Done", ["value"], (message, _) =>
    Console.WriteLine(message.Arguments.GetAsInteger("value")));
var instance = host.Load(program);

if (host.Start().State != GameEventScriptStartState.Ready)
    throw new InvalidOperationException("Host startup failed.");

host.Receive(GameEventScriptMessage.Create("Calculate", [
    new GameEventScriptMessageArgument("base", GesValue.GesInteger(30)),
    new GameEventScriptMessageArgument("bonus", GesValue.GesInteger(12))
]));

var result = host.RunToCompletion();
if (result.State != GameEventScriptExecutionState.Completed)
    throw new InvalidOperationException($"Execution stopped: {result.State}");

instance.Detach();
output.Unsubscribe();
```

Run `dotnet run`. The callback prints `42`.

`Compile()` creates an immutable program that you can reuse in multiple hosts.
`Load()` registers a program instance; `Start()` completes the initial loading
phase. `Receive()` queues the external `Calculate(base, bonus)` message, and
`RunToCompletion()` processes the queue. Receiving a message does not by itself
execute the script.

The seed makes this host's random stream repeatable. Reproducible execution also
requires the same program, inputs, ordering and relevant clock observations;
see [Determinism](../../specs/Semantics/Determinism.md).

## Load several programs before starting

Register initial native handlers and load all initial programs before calling
`Start()` once. Initializations run in load order; ordinary messages wait until
the initial group has succeeded. If startup fails, create a new host rather than
continuing with a partly initialized group.

After a host is ready, an additional `Load()` queues that instance's initialization.
Its `StartResult` remains absent while pending. Continue pumping and inspect the
result before treating the new instance as ready. A failed later initialization
removes that instance without undoing other running programs.

Keep instance and subscription handles when you need to detach or unsubscribe.
The host retains registrations independently of your local handle variables.
Ordinary detachment does not retroactively cancel already captured message
deliveries; initialization failure has different cancellation rules.

## Ship precompiled programs

Compile during your content build instead of inside the shipped application:

```sh
dotnet ges compile damage.ges -o artifacts/damage.gesb
```

Read the file in your application and pass its bytes to Runtime:

```csharp
var bytes = System.IO.File.ReadAllBytes("damage.gesb");
var program = GameEventScriptProgramReader.Read(bytes);
```

Replace the builder section of the previous example with this code. The
application can now omit Compiler. CSharpBridge still depends only on Runtime.
File access remains in your application, not in the portable runtime.

## Handle failures and delayed work

Compilation failures throw `GameEventScriptCompileException`, whose `Diagnostics`
contain stable codes and source locations. Check startup and execution results;
do not assume a returned result means all handlers succeeded. Record structured
diagnostics rather than parsing English messages.

> **Since: Unreleased**

A queue containing only future messages returns `Waiting`.
`RunToCompletion()` does not sleep. Use `NextMessageDelay` to arrange a later pump
in your event loop, or use the optional C# host runner. Avoid repeatedly calling
the pump in a busy loop. The simple example above has no delayed work and expects
`Completed`.

A host accepts one caller at a time. Keep raw host access serialized. The native
runner supplies synchronization and automatic pumping; use its API consistently
rather than mixing runner calls with unsynchronized raw host access.

## Connect native data

CSharpBridge supplies attribute-based external type bindings with `GesType`,
`GesField` and `GesConstruct`. The compiler needs a declarative type catalog;
the host separately needs the executable registry. Passing a catalog to the
compiler does not install runtime bindings.

The [C# bridge guide](../../implementation/csharp/GameEventScript.CSharpBridge/README.md)
contains a complete `BotState` example using these attributes, units and
`GesParam`, plus annotated extension functions.

Expose only the fields and constructors your script needs. Programs contain
portable type declarations, not CLR objects or reflection state. Use native
message handlers for application actions, and extensions or external fields for
the read-only data your scripts consult.

Next: [learn the language](Language.md), explore the
[C# implementation and embedding examples](../../implementation/csharp/README.md),
or consult the [host lifecycle](../../specs/HostRuntime.md) and
[external type contract](../../specs/PublicApi.md).
