# Bytecode Opcode Instruction Shape

This document lists the current `GameEventScriptBytecodeOpCode` values and how
each opcode uses the compact linear instruction shape.

```csharp
GameEventScriptBytecodeInstruction(
    OpCode,
    Dest = -1,
    A = -1,
    B = -1,
    C = -1)
```

Conventions:

- `Dest` is the destination frame slot for value-producing instructions.
- `A`, `B`, and `C` are opcode-specific operands.
- `-1` means unused/no value.
- Branch opcodes use `A` as the target address.
- Conditional branches use `C` as the condition slot.
- Loop/block opcodes use `A` as body target, `B` as end target, and `C` as
  layout index.
- Side-table-backed opcodes use `C` as the data/layout index. Their complete
  operand list lives in the side table; `A` and `B` only mirror the first one or
  two slots when that is useful.

## Opcode Table

| Opcode | Dest | A | B | C | Notes |
| --- | --- | --- | --- | --- | --- |
| `LoadConstant` | result slot | unused | unused | `ConstantPool` index | Loads a portable bytecode constant. |
| `LoadSlot` | result slot | source slot | unused | unused | Reads an existing frame slot. |
| `Or` | result slot | left slot | right slot | unused | Tri-state boolean combine. |
| `Xor` | result slot | left slot | right slot | unused | Binary operation. |
| `And` | result slot | left slot | right slot | unused | Tri-state boolean combine. |
| `Equal` | result slot | left slot | right slot | unused | Binary comparison. |
| `NotEqual` | result slot | left slot | right slot | unused | Binary comparison. |
| `ApproxEqual` | result slot | left slot | right slot | unused | Approximate equality. |
| `Less` | result slot | left slot | right slot | unused | Binary comparison. |
| `Greater` | result slot | left slot | right slot | unused | Binary comparison. |
| `LessOrEqual` | result slot | left slot | right slot | unused | Binary comparison. |
| `GreaterOrEqual` | result slot | left slot | right slot | unused | Binary comparison. |
| `Add` | result slot | left slot | right slot | unused | Binary operation. |
| `Subtract` | result slot | left slot | right slot | unused | Binary operation. |
| `Multiply` | result slot | left slot | right slot | unused | Binary operation. |
| `Divide` | result slot | left slot | right slot | unused | Binary operation. |
| `IntegerDivide` | result slot | left slot | right slot | unused | Binary operation. |
| `Modulo` | result slot | left slot | right slot | unused | Binary operation. |
| `Remainder` | result slot | left slot | right slot | unused | Binary operation. |
| `PrimitiveIntegerEqual` | result slot | left slot | right slot | unused | Integer fast-path comparison. |
| `PrimitiveIntegerNotEqual` | result slot | left slot | right slot | unused | Integer fast-path comparison. |
| `PrimitiveIntegerLess` | result slot | left slot | right slot | unused | Integer fast-path comparison. |
| `PrimitiveIntegerGreater` | result slot | left slot | right slot | unused | Integer fast-path comparison. |
| `PrimitiveIntegerLessOrEqual` | result slot | left slot | right slot | unused | Integer fast-path comparison. |
| `PrimitiveIntegerGreaterOrEqual` | result slot | left slot | right slot | unused | Integer fast-path comparison. |
| `PrimitiveIntegerAdd` | result slot | left slot | right slot | unused | Integer fast-path arithmetic. |
| `PrimitiveIntegerSubtract` | result slot | left slot | right slot | unused | Integer fast-path arithmetic. |
| `PrimitiveIntegerMultiply` | result slot | left slot | right slot | unused | Integer fast-path arithmetic. |
| `PrimitiveIntegerDivide` | result slot | left slot | right slot | unused | Integer fast-path arithmetic. |
| `PrimitiveIntegerFloorDivide` | result slot | left slot | right slot | unused | Integer fast-path arithmetic. |
| `PrimitiveIntegerModulo` | result slot | left slot | right slot | unused | Integer fast-path arithmetic. |
| `PrimitiveIntegerRemainder` | result slot | left slot | right slot | unused | Integer fast-path arithmetic. |
| `Default` | result slot | left slot | right slot | unused | Nothing/default operator. |
| `Contains` | result slot | left slot | right slot | unused | Collection/text operation. |
| `ContainsValue` | result slot | left slot | right slot | unused | Collection/text operation. |
| `StartsWith` | result slot | left slot | right slot | unused | Text operation. |
| `EndsWith` | result slot | left slot | right slot | unused | Text operation. |
| `Intersect` | result slot | left slot | right slot | unused | Collection operation. |
| `Combine` | result slot | left slot | right slot | unused | Collection operation. |
| `Except` | result slot | left slot | right slot | unused | Collection operation. |
| `Zip` | result slot | left slot | right slot | unused | Collection operation. |
| `UnaryNegate` | result slot | operand slot | unused | unused | Numeric negation. |
| `UnaryNot` | result slot | operand slot | unused | unused | Logical negation. |
| `UnaryHasValue` | result slot | operand slot | unused | unused | Semantic value check. |
| `UnaryEmpty` | result slot | operand slot | unused | unused | Semantic emptiness check. |
| `UnaryLength` | result slot | operand slot | unused | unused | Length operation. |
| `UnaryChance` | result slot | operand slot | unused | unused | Chance evaluation. |
| `UnaryKeys` | result slot | operand slot | unused | unused | Dictionary/record keys projection. |
| `UnaryValues` | result slot | operand slot | unused | unused | Dictionary/record values projection. |
| `UnaryEntries` | result slot | operand slot | unused | unused | Dictionary/record entries projection. |
| `UnaryAbs` | result slot | operand slot | unused | unused | Absolute value. |
| `UnaryNaturalLog` | result slot | operand slot | unused | unused | Natural logarithm. |
| `Variadic` | result slot | first argument slot | second argument slot | `OperationLayouts` index | Full argument list is in layout. |
| `Clamp` | result slot | value slot | minimum slot | maximum slot | The only opcode with three direct source slots. |
| `Random` | result slot | from slot | to slot | unused | Uses current random scope. |
| `Range` | result slot | from slot | to slot | unused | Builds a range with implicit step `1`. |
| `RangeWithStep` | result slot | from slot | to slot | step slot | Builds a range with explicit step. |
| `Dice` | result slot | dice count immediate | side count immediate | unused | `A` and `B` are not slots. |
| `SeededRandom` | result slot | seed slot | unused | helper entry address | Executes helper expression under a seeded random scope. |
| `CastNothing`..`CastOptional` | result slot | source slot | unused | unused | Direct built-in declared-type conversion. |
| `CastCustom` | result slot | source slot | unused | `StringPool` index | Declared-type conversion for custom record/external types. |
| `TypeConstructor` | result slot | first argument slot | second argument slot | `OperationLayouts` index | Full argument list and names are in layout. |
| `PredicateTest` | result slot | tested value slot | unused | `OperationLayouts` index | Enters a predicate frame or fast path. |
| `MemberAccess` | result slot | target slot | unused | `StringPool` index | Reads a named member. |
| `IndexedAccess` | result slot | target slot | index slot | unused | Direct indexed lookup. |
| `BuildList` | result slot | first item slot | second item slot | `OperationLayouts` index | Full item list is in layout. |
| `BuildSequence` | result slot | first item slot | second item slot | `OperationLayouts` index | Full item list is in layout. |
| `BuildSet` | result slot | first item slot | second item slot | `OperationLayouts` index | Full item list is in layout. |
| `BuildDictionary` | result slot | first value slot | second value slot | `OperationLayouts` index | Keys and full value list are in layout. |
| `BuildMessage` | result slot | first argument slot | second argument slot | `OperationLayouts` index | Message name, signature, names, and slots are in layout. |
| `BindHandler` | result slot | handler slot | first bound argument slot | `OperationLayouts` index | Full operand list is in layout. |
| `CallExtension` | result slot | first argument slot | second argument slot | `OperationLayouts` index | Extension reference and argument metadata are in layout. |
| `Call` | result slot | first argument slot | second argument slot | `OperationLayouts` index | Enters a VM-owned call frame. |
| `TypeCheckNothing`..`TypeCheckDice` | result slot | source slot | unused | unused | Direct built-in type predicate. |
| `TypeCheckCustom` | result slot | source slot | unused | `StringPool` index | Type predicate for custom record/external types. |
| `Pipeline` | result slot | unused | unused | `PipelineLayouts` index | Layout stores source slot and selector indexes. |
| `GeneratedCollection` | result slot | unused | unused | `GeneratedCollectionLayouts` index | Layout stores iteration source and helper entries. |
| `GuardedChoice` | result slot | unused | unused | `GuardedChoiceLayouts` index | Layout stores value/condition helper entries. |
| `Power` | result slot | left slot | right slot | unused | Binary operation. |
| `ShortCircuitOr` | unused | unused | unused | unused | Lowering marker only; runtime uses jumps plus `Or`. |
| `ShortCircuitAnd` | unused | unused | unused | unused | Lowering marker only; runtime uses jumps plus `And`. |
| `ShortCircuitImplies` | result slot | antecedent slot | consequent slot | unused | Binary implication combine. |
| `Nop` | unused | unused | unused | unused | No operation. |
| `BindParameter` | parameter slot | parameter index immediate | unused | unused | Reads invocation argument `A` and writes it to `Dest`. |
| `CopySlot` | result slot | source slot | unused | unused | Defines/copies a slot value. |
| `Jump` | unused | target address | unused | unused | Unconditional branch. |
| `JumpIfTrue` | unused | target address | unused | condition slot | Branches when `C.IsTrue()`. |
| `JumpIfFalse` | unused | target address | unused | condition slot | Branches when `C.IsFalse()`. |
| `JumpIfNotTrue` | unused | target address | unused | condition slot | Branches when `!C.IsTrue()`, including `nothing`. |
| `EnterScope` | unused | unused | unused | unused | Pushes a scope mark for local-slot cleanup. |
| `ExitScope` | unused | unused | unused | unused | Pops a scope and restores changed slots. |
| `Return` | unused | optional return slot | unused | unused | `A = -1` returns `nothing`. |
| `PublishValue` | unused | publish kind immediate | unused | `PublishLayouts` index | Publishes from a layout. |
| `PublishMessageValue` | unused | message slot | publish kind immediate | `PublishLayouts` index | Publishes a dynamic message value. |
| `ForRange` | unused | body target address | end target address | `LoopLayouts` index | Layout links identifier slot and range iteration source. |
| `ForCollection` | unused | body target address | end target address | `LoopLayouts` index | Layout links identifier slot and collection source. |
| `SeededRandomBlock` | unused | body target address | end target address | `SeededRandomBlockLayouts` index | Layout stores seed slot. |

## Side-Table Summary

| C target | Used by |
| --- | --- |
| `ConstantPool` | `LoadConstant` |
| `StringPool` | `MemberAccess` |
| `OperationLayouts` | `Variadic`, `TypeConstructor`, `PredicateTest`, `BuildList`, `BuildSequence`, `BuildSet`, `BuildDictionary`, `BuildMessage`, `BindHandler`, `CallExtension`, `Call` |
| `PublishLayouts` | `PublishValue`, `PublishMessageValue` |
| `LoopLayouts` | `ForRange`, `ForCollection` |
| `SeededRandomBlockLayouts` | `SeededRandomBlock` |
| `PipelineLayouts` | `Pipeline` |
| `GeneratedCollectionLayouts` | `GeneratedCollection` |
| `GuardedChoiceLayouts` | `GuardedChoice` |
