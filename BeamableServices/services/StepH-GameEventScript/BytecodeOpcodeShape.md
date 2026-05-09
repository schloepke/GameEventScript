# Bytecode Opcode Instruction Shape

This document lists the current `GameEventScriptBytecodeOpCode` values and how
each opcode uses the linear instruction shape.

```csharp
GameEventScriptBytecodeInstruction(
    OpCode,
    Dest = -1,
    A = -1,
    B = -1,
    C = -1,
    Target = -1,
    Target2 = -1,
    Data = -1)
```

Conventions:

- `Dest` is the destination frame slot for value-producing instructions.
- `A`, `B`, and `C` are usually source slots. Rows call out immediates or
  indexes when a field is not a slot.
- `Target` and `Target2` are absolute instruction addresses.
- `Data` is either unused or an index into a side table.
- `-1` means unused/no value.
- For operation-layout opcodes, `A`/`B`/`C` may mirror the first three argument
  slots, but the authoritative full operand list is
  `OperationLayouts[Data].ArgumentSlots`.

## Opcode Table

| Opcode | Dest | A/B/C usage | Targets | Data | Notes |
| --- | --- | --- | --- | --- | --- |
| `LoadConstant` | result slot | unused | unused | `ConstantPool` index | Loads a portable bytecode constant. |
| `LoadSlot` | result slot | `A` = source slot | unused | unused | Reads an existing frame slot. |
| `Or` | result slot | `A` = left slot, `B` = right slot | unused | unused | Tri-state boolean combine; short-circuit is handled by surrounding jumps. |
| `Xor` | result slot | `A` = left slot, `B` = right slot | unused | unused | Binary operation. |
| `And` | result slot | `A` = left slot, `B` = right slot | unused | unused | Tri-state boolean combine; short-circuit is handled by surrounding jumps. |
| `Equal` | result slot | `A` = left slot, `B` = right slot | unused | unused | Binary operation. |
| `NotEqual` | result slot | `A` = left slot, `B` = right slot | unused | unused | Binary operation. |
| `ApproxEqual` | result slot | `A` = left slot, `B` = right slot | unused | unused | Approximate equality operation. |
| `Less` | result slot | `A` = left slot, `B` = right slot | unused | unused | Binary comparison. |
| `Greater` | result slot | `A` = left slot, `B` = right slot | unused | unused | Binary comparison. |
| `LessOrEqual` | result slot | `A` = left slot, `B` = right slot | unused | unused | Binary comparison. |
| `GreaterOrEqual` | result slot | `A` = left slot, `B` = right slot | unused | unused | Binary comparison. |
| `Add` | result slot | `A` = left slot, `B` = right slot | unused | unused | Binary operation. |
| `Subtract` | result slot | `A` = left slot, `B` = right slot | unused | unused | Binary operation. |
| `Multiply` | result slot | `A` = left slot, `B` = right slot | unused | unused | Binary operation. |
| `Divide` | result slot | `A` = left slot, `B` = right slot | unused | unused | Binary operation. |
| `IntegerDivide` | result slot | `A` = left slot, `B` = right slot | unused | unused | Binary operation. |
| `Modulo` | result slot | `A` = left slot, `B` = right slot | unused | unused | Binary operation. |
| `Remainder` | result slot | `A` = left slot, `B` = right slot | unused | unused | Binary operation. |
| `PrimitiveIntegerEqual` | result slot | `A` = left slot, `B` = right slot | unused | unused | Integer fast-path comparison. |
| `PrimitiveIntegerNotEqual` | result slot | `A` = left slot, `B` = right slot | unused | unused | Integer fast-path comparison. |
| `PrimitiveIntegerLess` | result slot | `A` = left slot, `B` = right slot | unused | unused | Integer fast-path comparison. |
| `PrimitiveIntegerGreater` | result slot | `A` = left slot, `B` = right slot | unused | unused | Integer fast-path comparison. |
| `PrimitiveIntegerLessOrEqual` | result slot | `A` = left slot, `B` = right slot | unused | unused | Integer fast-path comparison. |
| `PrimitiveIntegerGreaterOrEqual` | result slot | `A` = left slot, `B` = right slot | unused | unused | Integer fast-path comparison. |
| `PrimitiveIntegerAdd` | result slot | `A` = left slot, `B` = right slot | unused | unused | Integer fast-path arithmetic. |
| `PrimitiveIntegerSubtract` | result slot | `A` = left slot, `B` = right slot | unused | unused | Integer fast-path arithmetic. |
| `PrimitiveIntegerMultiply` | result slot | `A` = left slot, `B` = right slot | unused | unused | Integer fast-path arithmetic. |
| `PrimitiveIntegerDivide` | result slot | `A` = left slot, `B` = right slot | unused | unused | Integer fast-path arithmetic. |
| `PrimitiveIntegerFloorDivide` | result slot | `A` = left slot, `B` = right slot | unused | unused | Integer fast-path arithmetic. |
| `PrimitiveIntegerModulo` | result slot | `A` = left slot, `B` = right slot | unused | unused | Integer fast-path arithmetic. |
| `PrimitiveIntegerRemainder` | result slot | `A` = left slot, `B` = right slot | unused | unused | Integer fast-path arithmetic. |
| `Default` | result slot | `A` = left slot, `B` = right slot | unused | unused | Null/nothing default operator. |
| `Contains` | result slot | `A` = left slot, `B` = right slot | unused | unused | Binary collection/text operation. |
| `ContainsValue` | result slot | `A` = left slot, `B` = right slot | unused | unused | Binary collection/text operation. |
| `StartsWith` | result slot | `A` = left slot, `B` = right slot | unused | unused | Binary text operation. |
| `EndsWith` | result slot | `A` = left slot, `B` = right slot | unused | unused | Binary text operation. |
| `Intersect` | result slot | `A` = left slot, `B` = right slot | unused | unused | Binary collection operation. |
| `Combine` | result slot | `A` = left slot, `B` = right slot | unused | unused | Binary collection operation. |
| `Except` | result slot | `A` = left slot, `B` = right slot | unused | unused | Binary collection operation. |
| `Zip` | result slot | `A` = left slot, `B` = right slot | unused | unused | Binary collection operation. |
| `Unary` | result slot | `A` = operand slot | unused | `OperationLayouts` index | Layout stores unary operator name and argument slots. |
| `Variadic` | result slot | `A`/`B`/`C` = first argument slots, full list in layout | unused | `OperationLayouts` index | Layout stores operator name and all argument slots. |
| `Clamp` | result slot | `A` = value slot, `B` = minimum slot, `C` = maximum slot | unused | unused | Three-slot value operation. |
| `Random` | result slot | `A` = from slot, `B` = to slot | unused | unused | Uses current random scope. |
| `Range` | result slot | `A` = from slot, `B` = to slot, `C` = optional step slot | unused | `OperationLayouts` index | Layout keeps operand metadata/count. |
| `Dice` | result slot | `A` = dice count immediate, `B` = side count immediate | unused | unused | `A` and `B` are not slots. |
| `SeededRandom` | result slot | `A` = seed slot | unused | `OperationLayouts` index | Layout stores helper expression entry address. |
| `Cast` | result slot | `A` = source slot | unused | `OperationLayouts` index | Layout stores `CastKind`. |
| `TypeConstructor` | result slot | `A`/`B`/`C` = first argument slots, full list in layout | unused | `OperationLayouts` index | Layout stores type name, argument names, and slots. |
| `PredicateTest` | result slot | `A` = tested value slot | unused | `OperationLayouts` index | Enters a predicate frame or uses predicate fast paths. |
| `MemberAccess` | result slot | `A` = target slot | unused | `OperationLayouts` index | Layout stores member name. |
| `IndexedAccess` | result slot | `A` = target slot, `B` = index slot | unused | unused | Direct indexed lookup. |
| `BuildList` | result slot | `A`/`B`/`C` = first item slots, full list in layout | unused | `OperationLayouts` index | Layout stores all item slots. |
| `BuildSequence` | result slot | `A`/`B`/`C` = first item slots, full list in layout | unused | `OperationLayouts` index | Layout stores all item slots. |
| `BuildSet` | result slot | `A`/`B`/`C` = first item slots, full list in layout | unused | `OperationLayouts` index | Layout stores all item slots. |
| `BuildDictionary` | result slot | `A`/`B`/`C` = first value slots, full list in layout | unused | `OperationLayouts` index | Layout stores keys in `Names` and value slots in `ArgumentSlots`. |
| `BuildMessage` | result slot | `A`/`B`/`C` = first argument slots, full list in layout | unused | `OperationLayouts` index | Layout stores message name, signature, argument names, and slots. |
| `BindHandler` | result slot | `A`/`B`/`C` = handler plus first bound argument slots, full list in layout | unused | `OperationLayouts` index | First operand is the handler value; remaining operands are bound arguments. |
| `CallExtension` | result slot | `A`/`B`/`C` = first argument slots, full list in layout | unused | `OperationLayouts` index | Layout stores extension name, function name, import reference, argument names, and callable kind. |
| `Call` | result slot | `A`/`B`/`C` = first argument slots, full list in layout | unused | `OperationLayouts` index | Enters a VM-owned call frame for a function/predicate. |
| `TypeCheck` | result slot | `A` = source slot | unused | `OperationLayouts` index | Layout stores type name. |
| `Pipeline` | result slot | unused | unused | `PipelineLayouts` index | Layout stores source slot, prefix selector indexes, and terminal selector index. |
| `GeneratedCollection` | result slot | unused | unused | `GeneratedCollectionLayouts` index | Layout stores collection type, iteration source, identifier slot, and helper entry addresses. |
| `GuardedChoice` | result slot | unused | unused | `GuardedChoiceLayouts` index | Layout stores value/condition helper entry addresses and optional otherwise entry. |
| `Power` | result slot | `A` = left slot, `B` = right slot | unused | unused | Binary operation. |
| `ShortCircuitOr` | unused | unused | unused | unused | Lowering marker only; current runtime uses `JumpIfTrue` plus `Or` instead. |
| `ShortCircuitAnd` | unused | unused | unused | unused | Lowering marker only; current runtime uses `JumpIfFalse` plus `And` instead. |
| `ShortCircuitImplies` | result slot | `A` = antecedent slot, `B` = consequent slot | unused | unused | Runtime binary helper used by implication lowering. |
| `Nop` | unused | unused | unused | unused | No operation. |
| `BindParameter` | parameter slot | `A` = parameter index immediate | unused | unused | Reads invocation argument `A` and writes it to `Dest`. |
| `CoerceSlot` | result slot | `A` = source slot | unused | `TypeMetadata` index | Coerces a slot to a declared type. |
| `CopySlot` | result slot | `A` = source slot | unused | unused | Defines/copies a slot value. |
| `Jump` | unused | unused | `Target` = next pc | unused | Unconditional branch. |
| `JumpIfTrue` | unused | `A` = condition slot | `Target` = branch pc | unused | Branches when `A.IsTrue()`. |
| `JumpIfFalse` | unused | `A` = condition slot | `Target` = branch pc | unused | Branches when `A.IsFalse()`. |
| `JumpIfNotTrue` | unused | `A` = condition slot | `Target` = branch pc | unused | Branches when `!A.IsTrue()`, including `nothing`. |
| `EnterScope` | unused | unused | unused | unused | Pushes a scope mark for local-slot cleanup. |
| `ExitScope` | unused | unused | unused | unused | Pops a scope and restores changed slots. |
| `Return` | unused | `A` = optional return slot | unused | unused | Returns from current handler/helper/callable frame; `A = -1` returns `nothing`. |
| `PublishValue` | unused | unused | unused | `PublishLayouts` index | Publishes from a precomputed publish layout. |
| `PublishMessageValue` | unused | `A` = message slot | unused | `PublishLayouts` index | Publishes a dynamic message value with layout metadata. |
| `ForRange` | unused | unused | `Target` = body entry, `Target2` = end address | `LoopLayouts` index | Layout links identifier slot and range iteration source slots. |
| `ForCollection` | unused | unused | `Target` = body entry, `Target2` = end address | `LoopLayouts` index | Layout links identifier slot and collection source slot. |
| `SeededRandomBlock` | unused | unused | `Target` = body entry, `Target2` = end address | `SeededRandomBlockLayouts` index | Layout stores seed slot. |

## Side-Table Summary

| Data target | Used by |
| --- | --- |
| `ConstantPool` | `LoadConstant` |
| `TypeMetadata` | `CoerceSlot` |
| `OperationLayouts` | `Unary`, `Variadic`, `Range`, `SeededRandom`, `Cast`, `TypeConstructor`, `PredicateTest`, `MemberAccess`, `BuildList`, `BuildSequence`, `BuildSet`, `BuildDictionary`, `BuildMessage`, `BindHandler`, `CallExtension`, `Call`, `TypeCheck` |
| `PublishLayouts` | `PublishValue`, `PublishMessageValue` |
| `LoopLayouts` | `ForRange`, `ForCollection` |
| `SeededRandomBlockLayouts` | `SeededRandomBlock` |
| `PipelineLayouts` | `Pipeline` |
| `GeneratedCollectionLayouts` | `GeneratedCollection` |
| `GuardedChoiceLayouts` | `GuardedChoice` |

