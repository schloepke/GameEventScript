<!-- Copyright 2026 Stephan Schlöpke -->
<!-- SPDX-License-Identifier: Apache-2.0 -->

# C# bridge

Optional C# adapters for delegates, Reflection, CLR values and host pumping.
The production project depends only on Runtime.

Install `GameEventScript.CSharpBridge` from NuGet alongside Runtime. Add Compiler
when the application compiles source text; a product loading precompiled `.gesb`
programs can omit Compiler. The [C# embedding guide](../../../docs/guide/CSharp.md)
walks through installation and the basic host lifecycle.

## Attribute-based external types

C# annotations are ordinary attributes. `[GesType]` names an external script
type, `[GesField]` exposes readable native data, and `[GesConstruct]` explicitly
allows a native constructor or static factory to be called by scripts.
`[GesParam]` supplies each constructor parameter's script name and type.

The following complete console example exposes the same `BotState` concept as
the [Swift bridge guide](../../swift/GameEventScriptSwiftBridge/README.md):

```csharp
using System;
using GameEventScript.Api;
using GameEventScript.CSharpBridge;
using GameEventScript.Runtime;

var registry = GameEventScriptCSharpExternalTypes.CreateRegistry(typeof(BotState));
var program = GameEventScriptBuilder.Create()
    .WithExternalTypeCatalog(registry)
    .AddScript("""
        module tutorial.bindings
        on Start() {
            let bot be :BotState(energy: 100, distance: 4m)
            emit Done(energy: bot.energy, nearWall: bot.isNearWall)
        }
        """, "bindings.ges")
    .Compile();

var host = GameEventScriptHost.CreateBuilder()
    .WithExternalTypeRegistry(registry)
    .Build();
host.Subscribe("Done", ["energy", "nearWall"], (message, _) =>
    Console.WriteLine($"Energy: {message.Arguments.GetAsNumber("energy")}, near wall: {message.Arguments.GetAsBoolean("nearWall")}"));
host.Load(program);

if (host.Start().State != GameEventScriptStartState.Ready)
    throw new InvalidOperationException("Host startup failed.");

host.Receive(GameEventScriptMessage.Create("Start"));
var result = host.RunToCompletion();
if (result.State != GameEventScriptExecutionState.Completed)
    throw new InvalidOperationException($"Execution stopped: {result.State}");

[GesType("BotState")]
public sealed class BotState
{
    [GesField("energy", GameEventScriptBytecodeTypeKind.Float)]
    public double Energy { get; }

    [GesField("distance", GameEventScriptBytecodeTypeKind.Float,
        GameEventScriptBytecodeInstructionUnit.UnitMeter)]
    public double Distance { get; }

    [GesField("isNearWall", GameEventScriptBytecodeTypeKind.Boolean)]
    public bool IsNearWall => Distance < 5;

    [GesConstruct]
    public BotState(
        [GesParam("energy", GameEventScriptBytecodeTypeKind.Float)] double energy,
        [GesParam("distance", GameEventScriptBytecodeTypeKind.Float,
            GameEventScriptBytecodeInstructionUnit.UnitMeter)] double distance)
    {
        Energy = energy;
        Distance = distance;
    }
}
```

It prints `Energy: 100, near wall: True`. The compiler uses the registry's
**declarative catalog** to validate `:BotState(...)` and its fields. The host uses
the **runtime registry** to invoke the CLR constructor and getters. A catalog
alone does not install executable bindings. The resulting Program contains
portable declarations, never CLR types, reflection objects or native instances.

### What each attribute exposes

| Attribute | Purpose |
| --- | --- |
| `[GesType("BotState")]` | Gives a CLR class or struct its script type name `:BotState` |
| `[GesField("energy", ...)]` | Exposes a public instance field or readable, non-indexer property |
| `[GesConstruct]` | Enables an annotated constructor or static factory returning that CLR type |
| `[GesParam("energy", ...)]` | Defines the script label and declared type of a constructor or extension argument |

Unannotated members do not become script fields. A CLR constructor is not exposed
merely because it exists; omit `[GesConstruct]` when scripts should only observe
native values. Constructor parameters must refer to exposed field names.

The numeric enum spelling `GameEventScriptBytecodeTypeKind.Float` belongs to the
binding API. The script-visible type remains `:Number`; this does not introduce
an `:Int`/`:Float` distinction in source code. For textual type declarations,
attributes also accept a type-name string, such as `[GesField("energy", "Number")]`.

Declare units explicitly on both the field and its constructor parameter. In
the example, `4m` supplies the native magnitude `4` in meters, and reading
`bot.distance` produces `4m` again. `UnitMeter` is a unit declaration, not an
automatic scaling or conversion between measurement systems.

### Lifetime and implementation

Registry creation discovers attributes using Reflection. Native construction
and field reads use the registered CLR bindings at runtime. This differs from
Swift's build-time macro expansion; C# does not need a GES source generator or
an additional macro package.

Property getters run when the script reads them. Exposing a CLR object does not
copy it into an immutable script Record or freeze its state. Prefer read-only
native data, as in this example, and coordinate any application-side mutation
with the host's serial execution. Programs are reusable; the executable bindings
and native objects belong to the embedding.

## Annotated extension functions

Use extensions for native operations called from script expressions. A static
class carries `[GesExtension]`; its callable methods carry `[GesFunction]` and
each script argument carries `[GesParam]`:

```csharp
[GesExtension("combat")]
public static class CombatFunctions
{
    [GesFunction("bonus", GameEventScriptBytecodeTypeKind.Float)]
    public static double Bonus(
        [GesParam("energy", GameEventScriptBytecodeTypeKind.Float)] double energy)
        => energy * 0.1;
}
```

Register it on the host builder with
`.WithRegistry(GameEventScriptCSharpExtensions.CreateRegistry(typeof(CombatFunctions)))`.
The script calls `:combat.bonus(energy: 100)`. Extension registries and external
type registries have distinct roles: `WithRegistry` installs functions;
`WithExternalTypeRegistry` installs native types.

## Native messages and values

`host.Subscribe("Done", ["value"], (message, context) => { ... })` adapts a
delegate to a native message handler and returns an unsubscribe handle. Arguments
remain ordered and labeled; a dictionary adapter requires an existing named
signature. Keep callback Context and borrowed arguments within their invocation.

Native callbacks run synchronously as part of the host's pump. Emit/publish
operations enqueue messages; they do not recursively call every receiver before
returning. Use the publish-sink adapter when messages should leave the local
host, and let the embedding decide their transport.

Conversion must preserve the declared value kind and unit. For example, checking
only a numeric magnitude is insufficient when the application needs to distinguish
seconds from meters. Declared `GameEventScriptExtensionFaultException` errors
retain their diagnostic codes across callback boundaries; unexpected native
exceptions become the corresponding runtime failure diagnostic.

## Automatic host pumping

`GameEventScriptCSharpHostRunner` supplies optional synchronization and background
pumping outside the portable Runtime. A loading host still needs an explicit
Start.

> **Since: Unreleased — automatic wakeups for delayed messages**

The runner also schedules wakeups for delayed messages.

Use the runner's API for a host it owns; do not mix it with unsynchronized raw
host calls. Callbacks are not implicitly dispatched onto a UI thread. Keep UI
marshalling in the application, and dispose runners before their dispatcher.
For lifecycle and execution-state details, see
[Host runtime](../../../specs/HostRuntime.md#c-automatic-runner).

## Local development

- `src/GameEventScript.CSharpBridge.csproj`: library project.
- `tests/GameEventScript.CSharpBridge.Tests.csproj`: native implementation tests.

Tests can use Compiler to prepare inputs without adding product dependencies.
Portable behavior is verified by the separate Conformance module against the
shared [Markdown corpus](../../../conformance).

See the [C# guide](../README.md) for build commands, API examples and distribution,
and the [test guide](../verification/README.md) for the full verification workflow.
