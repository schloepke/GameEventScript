<!-- Copyright 2026 Stephan Schlöpke -->
<!-- SPDX-License-Identifier: Apache-2.0 -->

# Learn Game Event Script

Write a rule, send it a message, and let your application handle the result.
Game Event Script (GES) is designed for this event-driven workflow: scripts
describe decisions, while your C# or Swift application owns the game world.

This guide follows the current development checkout. Features marked
**Unreleased** require a build from that checkout until the next package release.
The [changelog](../../CHANGELOG.md) identifies changes since the last release.

## Run your first script

Install one CLI from a repository checkout. The C# installer needs the repository's
.NET SDK and the .NET 8 runtime; the Swift installer needs Swift 6 or newer.
These commands build and install the tool locally; the CLI is not a public NuGet
library or a public SwiftPM product.

```sh
# Choose one, from the repository root:
./scripts/install-csharp-tool.sh
./scripts/install-swift-tool.sh
```

Save the following as `damage.ges`:

```ges
module tutorial.damage

function damage(base, bonus) be base + bonus

on Main(args) {
    emit Hit(amount: damage(base: 30, bonus: 12))
}

on Hit(amount) {
    emit ConsoleOut("Damage: ", amount)
}
```

Run it using the tool you installed:

```sh
dotnet ges run damage.ges
# Or:
ges run damage.ges
```

The script prints `Damage: 42`. The CLI also reports execution status on stderr;
add `--quiet` to hide the success summary.

The CLI sends `Main(args)` once. That handler enqueues `Hit(amount)`. When the
current message finishes, the host delivers `Hit` and eventually delivers
`ConsoleOut` to the CLI. An `emit` queues work; it is not a synchronous call to
another handler. `ConsoleOut` is supplied by the CLI, not built into the language.

An embedded application chooses its own starting message and native handlers.
Continue with [C#](CSharp.md) or [Swift](Swift.md) to run the same kind of flow
inside your product.

## Names and message arguments

Messages and types start with uppercase letters: `Hit`, `:Number`. Local names,
functions and tags start with lowercase letters: `amount`, `damage`, `#critical`.
A `module` name such as `tutorial.damage` identifies the program; it does not
create a namespace.

Argument labels are part of a message or function's signature:

```ges
function damage(base, bonus) be base + bonus
function double(_ value) be value + value

on Main(args) {
    emit ConsoleOut(damage(base: 30, bonus: 12))
    emit ConsoleOut(double(21))
}
```

`damage(base: 30, bonus: 12)` matches `damage(base, bonus)`. `double(21)` is
positional because `_ value` declares an unlabeled argument. Keep named argument
order consistent: labels and their order participate in signature matching.

## Values and local bindings

Use `let name be expression` to bind a value. Bindings are immutable: compute a
new value instead of assigning to an existing name.

```ges
on Main(args) {
    let base be 100
    let bonus be 20%
    let total be base + bonus
    let label be "Damage: " + (total as :Text)
    emit ConsoleOut(label)
}
```

Common data values include Number, Boolean, Text, Tag, List, Map and `nothing`.
Numbers may carry seconds (`0.2s`), meters (`10m`) or degrees (`90°`). A percentage
such as `20%` represents the ratio `0.2`, so `100 * 20%` gives `20`.

Adding a Percentage to a Number applies a relative increase: `base + bonus`
means `base + base * bonus`. The example therefore prints `Damage: 120`, while
`base` remains `100`. Similarly, `100 - 20%` gives `80`. The Percentage type
matters: `100 + 0.2` is ordinary addition and gives `100.2`.

There is no mutable global script state. Each handler invocation has fresh local
bindings. Keep persistent state in your application, or pass immutable data
along in subsequent messages.

## Missing values and decisions

An invalid list index or a missing map field yields `nothing`. Check for a value
before relying on it:

```ges
on Main(args) {
    let player be args[0]
    if player has value {
        emit ConsoleOut("Hello, ", player)
    } else {
        emit ConsoleOut("Hello, player")
    }
}
```

Run `ges run greeting.ges --args Ada`, or the corresponding `dotnet ges` command,
after saving this example as `greeting.ges`. All CLI arguments are Text, including
arguments that look like numbers. Convert deliberately with `as :Number`, or use
`parse` when you want literal recognition. Failed Number conversions yield
`nothing`; see [Number semantics](../../specs/Semantics/Numbers.md) for exact rules.

> **Since: Unreleased**

Conditional bindings combine a presence check and a local name:

```ges
on Main(args) {
    if let player be args[0]; player is :Text {
        emit ConsoleOut("Hello, ", player)
    } else {
        emit ConsoleOut("No player supplied")
    }
}
```

The checks short-circuit from left to right. `player` is available in subsequent
header checks and in the then branch, but not in the else branch or after the
`if`. This tests presence, not whether the value is truthy: `false` is a value.

## Work with collections

Lists preserve element order. Selectors express filtering, projection and
aggregation without mutable loop variables:

```ges
on Main(args) {
    let scores be [10, 0, 30, 20]
    let positive be scores[:filter score where score > 0]
    let doubled be positive[:select score => score * 2]
    emit ConsoleOut(doubled)
    emit ConsoleOut("Total: ", positive[:sum])
}
```

Use `for item in items { ... }` when each element should produce messages.
Use maps for named data; keys are text, with tag access available as a convenience.
Maps and lists, including nested values, are immutable.

> **Since: Unreleased**

`[:fold acc be seed, item => expression]` accumulates from an
explicit seed; `[:reduce acc, item => expression]` uses the first element:

```ges
on Main(args) {
    let scores be [10, 30, 20]
    let total be scores[:fold acc be 100, score => acc + score]
    let reduced be scores[:reduce acc, score => acc + score]
    emit ConsoleOut(total, ", ", reduced)
}
```

This prints `160, 60`. An empty fold returns its seed; an empty reduce returns
`nothing`.

## Schedule work and communicate

`emit` delivers locally. `publish` also crosses the configured host publish
boundary. Publishing does not automatically provide networking: your embedding
supplies the sink and decides how messages reach other hosts.

> **Since: Unreleased**

Use a time quantity to delay a message:

```ges
on Main(args) {
    emit after 0.2s Ready()
}

on Ready() {
    emit ConsoleOut("Ready")
}
```

The CLI waits for delayed work in batch mode. Embedded hosts remain synchronous:
your application's event loop or optional bridge runner resumes processing when
work becomes due. `emit after 10 Ready()` is invalid because `10` has no time unit.

## Explore and debug

```sh
ges check damage.ges
ges compile damage.ges -o artifacts/damage.gesb
ges dump artifacts/damage.gesb
ges run --interactive --color
```

Use `dotnet ges` instead of `ges` with the C# tool. In the interactive console,
`:help` lists commands, `:load damage.ges` adds a program, and `emit Main(args: [])`
sends its starting message. `:handler` lists registered handlers; `:source` and
`:dump` inspect a selected module. Ctrl+N inserts a newline; Enter submits input.
Temporary input bindings do not persist between submissions.

For exact syntax, read the [language reference](../../specs/Language.md).
For message ordering, initialization, limits and timing, read the
[host runtime contract](../../specs/HostRuntime.md). The
[CLI guide](../../implementation/csharp/GameEventScript.Tool/README.md) covers
commands and terminal behavior in full.
