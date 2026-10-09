<!-- Copyright 2026 Stephan Schlöpke -->
<!-- SPDX-License-Identifier: Apache-2.0 -->

# Inline assembly

Inline assembly is an unreleased compiler feature for instruction-level experiments
and comparisons. Both native compilers accept the same syntax. Programs remain
ordinary validated `.gesb` files; the Runtime needs no assembly-specific mode.

Use ordinary GES for rules. An ASM block is useful when the exact VM instruction
sequence is what you want to study. The [runnable example](../../examples/inline-assembly/operations.ges)
works with `ges run` or `dotnet ges run`.

## A result binding

```ges
let product be asm {
    Multiply product, first, second
}
```

`product` names the output register. `first` and `second` are read-only inputs
from the surrounding scope. No function call, scope frame or `ReturnValue`
instruction is introduced by the block itself.

For a complete function or predicate body, the callable name identifies the
output. A computed record field uses its field name. Normal parameter and field
casts still apply, and a predicate must produce Boolean or Nothing.

## Multiple results and temporary storage

A standalone block declares exported results with `let`. Temporary registers
use `.register` and are visible only inside that block. Declarations come first.

```ges
asm {
    let low, high
    .register adjustment
    LoadInteger adjustment, 2
    Subtract low, value, adjustment
    Add high, value, adjustment
}
emit Bounds(low: low, high: high)
```

An output or temporary must be initialized before every reachable read. All
normal exits must initialize the outputs. For an intentionally empty value,
write `LoadNothing`; do not rely on the contents of a recycled register.
Bindings outside the block cannot be overwritten or shadowed.

## Branches and internal resources

Labels are block-local and end with a colon. `Jump` and conditional jumps can
refer forward or backward. Loop paths participate in definite-assignment checks.
The example's `total` function demonstrates an iterator loop.

Iterator and builder handles belong in `.register` temporaries. Close an iterator
and finish a builder before leaving the block. Do not copy, export or pass these
handles to messages, functions or extensions. Balanced `RandomPush`/`RandomPop`
pairs can isolate random experiments; a block cannot pop a surrounding scope.

### Compound iterators

Iterator creation can name a source register list and a mode. Component output
writes directly to a target register list:

```ges
on Main(args) {
    let left be [1, 2]
    let right be [10, 20]
    let total be asm {
        .register iterator, first, second
        LoadInteger total, 0
        IteratorCreate iterator, [left, right], #cartesian
        next:
        IteratorNext [first, second], iterator, done
        Add total, total, first
        Add total, total, second
        Jump next
        done:
        IteratorClose iterator
    }
    emit ConsoleOut(total)
}
```

This prints `66` without constructing a pair List per iteration. Other creation
modes are `#union`, `#intersect`, `#difference`, and `#lockstep`.
`IteratorCreate iterator, source, #entries` uses a single Map source;
`#normal` is the default for the existing single-source form. Use
`IteratorCreateOrJump iterator, [left, right], invalid, #cartesian` when an
invalid source should branch explicitly.

Component targets must be distinct writable registers and cannot include the
iterator. Every source must already be initialized. Missing components and
exhausted targets become `nothing`; iterator handles still require closing.
See [collection pipeline measurements](../development/CollectionPipelines.md#verification-evidence-step-8)
for allocation and timing evidence; direct components are not a promise of
identical speed to nested loops.

## Symbolic operations

The compiler owns signatures, pools and argument staging. Use symbolic operands:

```ges
asm {
    .register answer
    Call answer, square(value: 7)
    emit Result(value: answer)
}
```

`CallExternal`, message creation, record construction and result-bearing sends
have corresponding symbolic forms. Raw `Stage…`, `Return…`, `RegisterLocals` and
`CreateRecordValue` are rejected. See the complete
[operand table and contract](../../specs/InlineAssembly.md#symbolic-source-operands).

Direct instructions retain their VM semantics: for example `CastNumeric` is the
numeric conversion operation; `Cast ..., :Number` names the typed VM cast.
Instruction operands accept scalar literals or registers, not arbitrary nested
GES calculations. A literal in a register position expands to an explicit load.

## Measuring instructions

ASM instructions retain their order. Currently the containing routine is kept
as lowered, and ASM temporaries use pinned register storage to preserve values
across arbitrary back edges. This can affect register count and code size.
Compare complete workloads and report those differences alongside timing and
allocation results. Compilation and input creation should be outside timing.

There is no dedicated `Sum` opcode: ordinary `[:sum]` already lowers to iteration
and arithmetic. Existing operations such as `Count`, `HasAny` and `First` are
better initial candidates for comparisons with explicit iterator loops.

The [measured comparison](InlineAssemblyBenchmarks.md) includes a reproducible
C# harness, List workloads, allocation and frame-budget observations.

### Numeric arithmetic

Use `Add result, left, right, #numeric` (also Subtract, Multiply, Divide, Power, IntegerDivide, Modulo, Remainder, Min and Max)
to require operands recognized by `is numeric`. Boolean uses 0/1 and Dice uses
the roll sum, bypassing collection overloads.
Numbers with units and percentages retain their usual arithmetic rules.

`Move`, `Negate` and `Abs` accept `#numeric` after their source; `Clamp` accepts it after
value, minimum and maximum.

`Move result, value, #numeric` copies an existing numeric value, preserving units
and Percentage. Boolean becomes 0/1 and Dice becomes its roll sum. Text is not
parsed; nonnumeric inputs become Nothing.
