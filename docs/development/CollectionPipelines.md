<!-- Copyright 2026 Stephan Schlöpke -->
<!-- SPDX-License-Identifier: Apache-2.0 -->

# Collection pipeline implementation plan

Status: approved design, implementation in progress. This document is not a
claim of implemented language support. Completed contracts move into their
owning specifications; this plan records sequencing and acceptance criteria.

## Delivery sequence

1. [x] Record the language, instruction, ownership and verification decisions.
2. [x] Extend C# bytecode descriptions, validation, codecs, GESA and fixtures.
   Source ASM emission follows with compiler support in step 5; Swift follows
   in steps 6/7. This checkpoint validates transport, not new execution paths.
3. [x] Implement the C# iterator/runtime reference and atomic Conformance cases.
4. [x] Extend the C# parser and both canonical editor grammars.
   Compact existing selectors and contiguous extensions execute in C#; new
   combined expressions and binding patterns await lowering in step 5.
5. [x] Implement C# pipeline lowering, binding and register analysis.
   Includes symbolic ASM source/target lists and 26 shared source cases.
   Swift parity and resource/performance acceptance remain steps 6–8.
6. [x] Port the runtime architecture and verification to Swift.
   All 1,503 independent Runtime cases pass using C#-compiled inputs;
   native source support remains step 7. The public API gate passes.
7. [ ] Port source compilation and assembly support to Swift.
8. [ ] Complete shared behavioral and native resource/performance verification.
9. [ ] Update Card Lab/examples, guides and changelog; review the entire branch.

Commit a verified checkpoint after each step; do not push automatically.
Intermediate commits may deliberately precede cross-language support, but must
identify that status rather than claiming completion of the whole feature.

## Language decisions

- `A * B` on two Lists materializes ordered two-element Lists. Multiplication
  remains left-associative: `A * B * C` nests the first pair. Existing numeric
  multiplication behavior is retained. No selector-to-opcode optimization.
- `[:cartesian A, B, C]` and `A[:cartesian B, C]` enumerate flat three-component
  rows, with the last source advancing fastest. Cartesian and lockstep require
  at least two total sources and accept normal iterable values. Lockstep stops
  at the shortest source; `:zip` aliases lockstep. Infix `zip` retains its
  existing List-of-Maps result with `left` and `right` keys.
- `union`, `intersect`, and `difference` selectors apply existing `|`, `&`, and
  `-` rules from left to right, including operand validity, duplicate handling,
  Map keys, Dice ordering and result kind. They are not generic concatenators.
  In particular Map/List means keys, not Map values; List/Map remains invalid.
  Difference preserves valid scalar-right cases, including List minus Nothing.
- Source expressions evaluate once, left to right. Stop before later source
  expressions when the operation is already invalid. Invalid is distinct from
  empty: an empty valid collection does not skip later source expressions.
  Source validity is operation-dependent, not a blanket iterable requirement.
- Direct Map iteration retains values in key order. `:entries` retains visible
  `[key: ..., value: ...]` Maps but may pass its components directly internally.
- Multiple binding names destructure List elements positionally and Map values
  in ascending scalar-key order. A scalar supplies the first component; missing
  components are Nothing and excess components are not bound. One binding keeps
  the complete item. Bindings are local to their loop or selector.
- Filter preserves the complete input item even if fewer components are bound.
  Select replaces it with the projection result. An internal row descriptor
  distinguishes a component List from an entry Map; final materialization must
  preserve the appropriate public value form.
- Support multiple bindings in `for`, filter, select, foreach, quantified and
  projected aggregate selectors wherever the existing item binding occurs.
  Fold/reduce retain their separate accumulator binding and accept multiple
  item bindings. Order/group projection bindings follow the same rule.
- `foreach` evaluates one expression per item and returns Nothing, including
  empty input. Side effects follow ordinary extension/message-expression rules;
  no rollback or mutable script bindings are introduced.
- `[:entries :filter ... :select ...]` is the same chain as separate brackets.
  `[:]` stays the empty Map. Type annotations, nested expressions and extension
  calls must not be mistaken for selector boundaries.
- `:namespace.name` is a contiguous qualified name: spaces, comments or newlines
  inside it are invalid. Whitespace before an argument list remains allowed.

## Instruction design

Keep the 16-byte instruction and existing UInt16 list pool. Iterator creation
uses the high three bits of UnitAndFlags as a mode field; the low five bits
must be zero. Modes are Normal=0, Union=1, Intersect=2, Difference=3,
Lockstep=4, Cartesian=5, Entries=6; mode 7 is invalid.

Normal and Entries take one source register in X. Other modes take a source
register-list index in X, with at least two registers. IteratorCreateOrJump
retains Y as the invalid-source branch target. All reserved fields are zero.

IteratorNext uses bit 5 for component output; other flag bits are zero. With
bit 5 clear, destination is one register. With bit 5 set, destination is a
nonempty target-register-list index. X is the iterator and Y the exhausted
branch. Targets are distinct and cannot overwrite the iterator register.
Exhaustion clears destinations to Nothing, matching existing single output.
Component output must not allocate an item container just to destructure it.

Source-list operands read registers; target-list operands write registers.
All compiler liveness, frame bounds, internal-value escape validation, ASM
initialization analysis, GESA printing and binary validation follow these roles.
IteratorClose releases owned iteration resources. Program data contains only
immutable source/target indices, never iterators, views or cached row values.

The binary Cartesian instruction accepts two Lists and returns Nothing for
other operands. Assign a regular unused opcode, never an experimental one.

## Resource decisions

Successful IteratorNext calls consume the existing loop-iteration budget.
Compound iterators additionally charge internal candidate/search work to that
same budget before performing it; exhausted calls are not themselves successful
steps. Define the precise charge points identically in C# and Swift and test
exact boundaries. Intersection/difference must not hide unbounded comparisons
inside one instruction. Limit retained internal membership/search structures
as well as public result builders; check sizes before allocation and products
without overflow. Direct component output does not charge a nonexistent pair
List. Existing native operator resource contracts remain authoritative.

Map merging may need retained indexing to resolve right-hand overrides before
exposing sorted keys. The contract promises correct values/order and avoidance
of unnecessary per-row containers, not constant-memory execution for every
operator or universal streaming through every source expression.

## Acceptance matrix and normative owners

- Language.md: syntax, operand/result kinds, order, bindings, evaluation effects.
- Bytecode.md, ProgramModel.md, BinaryFormat.md: modes, operands, validation,
  ordinary Cartesian instruction, ownership and canonical bytes.
- HostRuntime.md: exact search, loop and materialization limit accounting.
- AssemblerFormat.md and InlineAssembly.md: flags, lists, safe operand roles.
- SyntaxHighlighting.md and tools/editors: new selectors and qualified names.
- PublicApi.md and approved snapshots: public opcode/operand additions.
- Shared Markdown Conformance: empty/invalid/mixed sources, duplicate counts,
  Map/List keys and scalar difference, evaluation short-circuit, all binding
  shapes, pipeline preservation, early terminals, effects/randomness, limits,
  malformed Programs and cross-language behavior.
- Native tests only for implementation-specific register/allocation properties,
  bootstrap/gates and measurements. Compare nested loops, component Cartesian
  and materialized Cartesian; do not make semantics depend on timing results.

General public lazy collections, automatic Cartesian rewriting and broad VM
performance redesign are outside this implementation.
