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
