<!-- Copyright 2026 Stephan Schlöpke -->
<!-- SPDX-License-Identifier: Apache-2.0 -->

# Refer to host objects with handles

> **Since: Unreleased**

A handle points at an object that lives outside the script: a drone in the simulation, a contact on the radar, a unit
definition in a repository. The script passes handles around, compares them, uses them as Map keys and timer names,
and reads the object's state when it needs it. The host owns the object and answers each state read through a
resolver. GES never stores or changes the object.

This guide builds one small example from the schema to the host. Exact rules are in
[Language](../../specs/Language.md#handles), [Schemas](../../specs/Schemas.md#handles),
[Host runtime](../../specs/HostRuntime.md#handles) and the [Handle API](../../specs/PublicApi.md#handle-api).

## Declare a handle type

A handle type lives in a schema source (`.gess`). It names the schema of the object's state and the form of its
identity: an `id`, which is an integer Number or a Text, and a `version`.

```gess
schema :Contact as { label: :Text max 16, strength: :Number integer between 0 and 100 }
handle @Target of :Contact as { id: :Number integer between 0 and 65535, version: :Nothing }
```

`@Target` has integer ids and no versions, so every `@Target` handle means the contact's current state. A type with
`version: :Number integer` counts revisions, `:Hash(#sha1)` or `:Hash(#sha256)` carries a hash the owner computes
(a Git commit, for example), and `:ContentHash` lets GES hash the state itself. A *composite* type such as
`handle @UnitSlot of :Slot composite @Unit as { part: :Number integer between 0 and 15 }` addresses one part of a
root object. [Schemas](../../specs/Schemas.md#handles) lists every form.

## Use handles in a script

Scripts write handle types with `@`. A parameter `target as @Target` binds only handles of that type, `is @Target`
tests a value, and a member such as `target.strength` reads the object's state:

```ges
module radar
on Spotted(target as @Target) {
  if target.strength > 50 { emit Track(target: target as :Text, label: target.label) }
}
```

`target.id` and `target.version` need no state, and neither do comparing handles, using them as Map keys or naming a
timer with one. Every other member, and `target as :Contact`, reads the state. An object that does not exist reads as
`nothing`, so `target.strength > 50` is simply not true for it. A script has no handle literal and no cast to
`@Target`: handles arrive in messages, documents, state and extension results.

The compiler has to know the handle types. Pass them from the schema set to the Program builder; a type the
compilation names but no schema declares is `validate.unknownHandleType`.

## Resolve state in C\#

The host registers one resolver per handle type whose state a Program reads. Loading such a Program without one
fails with `link.missingHandleResolver`. A resolver answers each read with the state as a Record, as `.gesd` text
whose schema header names the state schema, or as missing:

```csharp
using System;
using GameEventScript.Api;
using GameEventScript.CSharpBridge;
using GameEventScript.Runtime.Values;
using static GameEventScript.Runtime.Values.GesValue;

var schemas = GesSchemaSet.Parse("""
    schema :Contact as { label: :Text max 16, strength: :Number integer between 0 and 100 }
    handle @Target of :Contact as { id: :Number integer between 0 and 65535, version: :Nothing }
    """);
var target = schemas.HandleType("Target");

var program = new GesProgramBuilder()
    .WithHandleTypes(schemas.HandleTypes)
    .AddScript("""
        module radar
        on Spotted(target as @Target) {
          if target.strength > 50 { emit Track(target: target as :Text, label: target.label) }
        }
        """, "radar.ges")
    .Compile();

var host = GameEventScriptHost.CreateBuilder()
    .WithHandleResolver(target, new Radar())
    .Build();
var output = host.Subscribe("Track", ["target", "label"], (message, _) =>
    Console.WriteLine($"{message.Arguments.GetAsText("label")} {message.Arguments.GetAsText("target")}"));
host.Load(program);
if (host.Start().State != GesStartState.Ready)
    throw new InvalidOperationException("Host startup failed.");

foreach (var id in new long[] { 3, 4 })
{
    host.Receive(GameEventScriptMessage.Create("Spotted", [
        new GesMessageArgument("target", GesHandle(GameEventScript.Api.GesHandle.Create("Target", GesInteger(id))))
    ]));
}
host.RunToCompletion();
output.Unsubscribe();

sealed class Radar : IGesHandleResolver
{
    public void Resolve(GesHandleResolveCall call)
    {
        if (call.Handle.Id.Equals(GesInteger(3)))
            call.SetDocument(":Schema(:Contact)\nlabel: 'Alpha'\nstrength: 72\n");
        else
            call.SetMissing();
    }
}
```

The program prints `Alpha @Target:3`; contact 4 is missing, so no `Track` follows. The host calls the resolver from
script execution on its serial path, at most once per object and handler invocation: every read of `@Target:3` in
one handler sees the same answer.

## Resolve state in Swift

The Swift port has the same steps with Swift spelling:

```swift
import GameEventScriptCompiler
import GameEventScriptRuntime
import GameEventScriptSwiftBridge

struct Radar: GesHandleResolver {
    func resolve(_ call: GesHandleResolveCall) throws {
        if call.handle.id == .integer(3) {
            try call.setDocument(":Schema(:Contact)\nlabel: 'Alpha'\nstrength: 72\n")
        } else {
            try call.setMissing()
        }
    }
}

let schemas = try GesSchemaSet(parsing: """
    schema :Contact as { label: :Text max 16, strength: :Number integer between 0 and 100 }
    handle @Target of :Contact as { id: :Number integer between 0 and 65535, version: :Nothing }
    """)
let target = try schemas.handleType("Target")

let program = try GesProgramBuilder()
    .withHandleTypes(schemas.handleTypes)
    .addScript("""
        module radar
        on Spotted(target as @Target) {
          if target.strength > 50 { emit Track(target: target as :Text, label: target.label) }
        }
        """, sourceName: "radar.ges")
    .compile()

let host = try GameEventScriptHost.makeBuilder()
    .withHandleResolver(Radar(), for: target)
    .build()
let track = try GesMessageSignature(name: "Track", parameters: ["target", "label"])
let output = try host.subscribe(track) { message, _ in
    print("\(message.arguments[1].textValue ?? "") \(message.arguments[0].textValue ?? "")")
}
_ = try host.load(program)
guard try host.start().state == .ready else {
    fatalError("Host startup failed.")
}

for id: Int64 in [3, 4] {
    try host.receive(GameEventScriptMessage(name: "Spotted", arguments: [
        GesMessageArgument(name: "target", value: .handle(GesHandle(typeName: "Target", id: .integer(id)))),
    ]))
}
_ = try host.runToCompletion()
output.unsubscribe()
```

It prints the same `Alpha @Target:3`.

## Write handles as text and in documents

A handle's text is `@Type:id`, followed by `~version` when it carries one and `/part` for a composite: `@Target:3`,
`@Drone:7~120`, `@UnitSlot:drone.gnabblo/1`. A Text id that is not plain ASCII letters, digits, `.`, `_` and `-`, or
that would read as an integer, is quoted: `@Unit:'One Two'`, `@Unit:'7'`. `as :Text`, console output and the API's
`ToString`/`description` all write this form. `parse` does not read handles back; a value that arrives as text stays
text.

[Documents](../../specs/Documents.md#handles) (`.gesd`) read handles as values and as Map keys, and the document
formatter prints them back in the same form. Checked against a schema, a handle may carry a version or a ref only in
an `@Name~` field, such as `unit` here; on `target`, `@Target:3~5` fails with `document.unexpectedVersion`:

```gess
schema :Composition as { name: :Text max 64, slots: :Number integer between 0 and 16 }
handle @Unit of :Composition as { id: :Text max 64, version: :ContentHash }
schema :Sighting as {
  target: @Target
  seen: :List(@Target) max 16
  unit: @Unit~ optional
  memory: :Map(@Target → :Contact) max 64
}
```

```gesd
:Schema(:Sighting)
target: @Target:3
seen: [@Target:3, @Target:17]
unit: @Unit:drone.gnabblo~#live
memory: [
  @Target:3: [label: 'Alpha', strength: 42]
]
```

Handle Map keys follow the Text keys, ordered by type name and then by id.

## Refs

In a document, a version may also be a *ref*: `@Unit:drone.gnabblo~#live` or `@Unit:drone.gnabblo~'1.0'` names a
version through the owner instead of stating it, like a branch or a tag. A ref with the characters of a tag is written
`~#name`; any other is quoted Text, such as `~'feature/x'`, so a `/` always starts the part. A ref never reaches a script. Before a
handle that carries one goes into a message, the host turns it into a version with
`GameEventScriptHost.ResolveRef(handle)` (Swift `resolveRef(_:)`), which asks the type's resolver through
`ResolveRef`/`resolveRef(_:)` and returns the handle with the concrete version. `Receive`, `Emit` and `Publish` of a
message that still holds a ref fail with `wire.unresolvedRef`.

## Try it with the CLI

Both CLIs load a schema set with the repeatable `--schema` option of `compile`, `check` and `run` and declare its
handle types to every compilation, including scenarios, `:load`, `:reload`, `:edit` and interactive input:

```sh
ges check radar.ges --schema radar.gess
ges run game.ges --schema "schemas/*.gess"
ges run game.gesb --schema radar.gess
```

With `--schema`, every handle type a `.gesb` input imports must match the one the schemas declare, or loading fails with
`link.mismatchedHandleType`. `:reload` reads the schema files again and keeps the session if they fail.

The CLI registers no resolver. A Program that only passes, compares and tests handles runs; one that reads state,
like the radar above, fails to load with `link.missingHandleResolver`. See the
[C# CLI](../../implementation/csharp/GameEventScript.Tool/README.md#handle-types) and
[Swift CLI](../../implementation/swift/GameEventScriptTool/README.md#handle-types) guides.
