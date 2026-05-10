# Bytecode Opcode Instruction Shape

This document lists the current `GameEventScriptBytecodeOpCode` values and how
each opcode uses the compact linear instruction shape.

## Instruction Word

`GameEventScriptBytecodeInstruction` is a fixed 12-byte explicit-layout word.
The same bytes can be read through different typed views. This is intentional:
simple VM instructions read compact 16-bit operands, while literal loads read
the overlapped 64-bit payload directly.

```csharp
[StructLayout(LayoutKind.Explicit, Size = 12)]
public struct GameEventScriptBytecodeInstruction
{
    [FieldOffset(0)]  public GameEventScriptBytecodeOpCode OpCode; // byte-backed
    [FieldOffset(1)]  public byte UnitAndFlags;

    [FieldOffset(2)]  public ushort Dest16;
    [FieldOffset(4)]  public ushort A16;
    [FieldOffset(6)]  public ushort B16;
    [FieldOffset(8)]  public ushort C16;
    [FieldOffset(10)] public ushort D16;

    [FieldOffset(4)]  public int AI32;
    [FieldOffset(8)]  public int BI32;
    [FieldOffset(4)]  public uint AU32;
    [FieldOffset(8)]  public uint BU32;

    [FieldOffset(4)]  public long I64;
    [FieldOffset(4)]  public ulong U64;
    [FieldOffset(4)]  public double F64;
}
```

The byte layout is:

| Byte range | 16-bit view | 32-bit view | 64-bit view | Typical use |
| --- | --- | --- | --- | --- |
| `0` | `OpCode` | `OpCode` | `OpCode` | Opcode tag, backed by `byte`. |
| `1` | `UnitAndFlags` | `UnitAndFlags` | `UnitAndFlags` | Numeric units and small opcode flags. |
| `2..3` | `Dest16` | `Dest16` | `Dest16` | Destination slot, or `0xffff` when unused. |
| `4..5` | `A16` | `AI32`/`AU32` low half | `I64`/`U64`/`F64` bytes `0..1` | First operand, or payload bytes. |
| `6..7` | `B16` | `AI32`/`AU32` high half | `I64`/`U64`/`F64` bytes `2..3` | Second operand, or payload bytes. |
| `8..9` | `C16` | `BI32`/`BU32` low half | `I64`/`U64`/`F64` bytes `4..5` | Third operand/layout index, or payload bytes. |
| `10..11` | `D16` | `BI32`/`BU32` high half | `I64`/`U64`/`F64` bytes `6..7` | Fourth operand when needed, or payload bytes. |

The public `Dest`, `A`, `B`, `C`, and `D` properties are compatibility helpers
over `Dest16`, `A16`, `B16`, `C16`, and `D16`. They expose unused operands as
`-1`; internally `0xffff` is reserved as the unused sentinel. That means all
slot indexes, table indexes, and instruction addresses stored in the compact
operand view must fit in `0..65534`.

## Operand Views

- **16-bit operand view:** most instructions use `Dest16`, `A16`, `B16`, and
  `C16` through the `Dest/A/B/C` helper properties. Examples: slots, branch
  targets, side-table indexes, dice immediates, and publish kinds.
- **64-bit integer view:** `LoadInteger` reads `I64` directly. Negative integer
  values are stored as their normal two's-complement bit pattern.
- **64-bit float view:** `LoadFloat` reads `F64` directly. This keeps IEEE-754
  `NaN`, `Infinity`, and `-Infinity` portable as raw double bits.
- **Unsigned payload view:** `RandomPushConstant` reads `U64` directly as the
  portable seeded-random seed. Signed integer seeds are mapped to `U64` by their
  normal two's-complement bit pattern.
- **32-bit views:** `AI32/BI32` and `AU32/BU32` are available for future compact
  immediates. Current bytecode does not need a dedicated 32-bit immediate load.

## Conventions

- `Dest` is the destination frame slot for value-producing instructions.
- `A`, `B`, `C`, and `D` in the opcode table refer to the 16-bit helper
  properties over `A16`, `B16`, `C16`, and `D16`.
- `-1` in the helper-property view means unused/no value.
- Branch opcodes use `A` as the target address.
- Conditional branches use `C` as the condition slot.
- Loop/block opcodes use `A` as body target, `B` as end target, and `C` as
  layout index.
- `LoadInteger` uses the overlapped `I64` payload and may use `UnitAndFlags`
  for numeric units.
- `LoadFloat` uses the overlapped `F64` payload. `UnitAndFlags` carries `None`,
  numeric units, or `Percentage`.
- Side-table-backed opcodes use `C` as the data/layout index. Their complete
  operand list lives in the side table; `A` and `B` only mirror the first one or
  two slots when that is useful.

## Opcode Table

| Hex | Opcode | UnitAndFlags | Dest16 | A16 | B16 | C16 | D16 | I64/U64 | F64 | Notes |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 0x00 | `LoadNothing` | - | result slot | - | - | - | - | - | - | Loads `nothing`. |
| 0x01 | `LoadTrue` | - | result slot | - | - | - | - | - | - | Loads boolean `true`. |
| 0x02 | `LoadFalse` | - | result slot | - | - | - | - | - | - | Loads boolean `false`. |
| 0x03 | `LoadInteger` | numeric unit | result slot | n/a | n/a | n/a | n/a | signed integer | - | Loads an inline signed `Int64`. |
| 0x04 | `LoadFloat` | numeric unit / `Percentage` | result slot | n/a | n/a | n/a | n/a | - | float / ratio | Loads an inline IEEE-754 `Float64`; `Percentage` makes the value a percentage ratio. |
| 0x05 | `LoadText` | - | result slot | - | - | `StringPool` index | - | - | - | Loads a text literal. |
| 0x06 | `LoadTag` | - | result slot | - | - | `StringPool` index | - | - | - | Loads a tag literal. |
| 0x07 | `LoadHandler` | - | result slot | message shape `UShortListPool` index | - | - | - | - | - | Loads a handler literal. The shape list is `[messageNameStringIndex, argumentNameStringIndex...]`. |
| 0x08 | `MoveSlot` | - | result slot | source slot | - | - | - | - | - | Copies a slot value/reference; the source slot remains unchanged. |
| 0x09 | `Or` | - | result slot | left slot | right slot | - | - | - | - | Tri-state boolean combine. |
| 0x0A | `Xor` | - | result slot | left slot | right slot | - | - | - | - | Binary operation. |
| 0x0B | `And` | - | result slot | left slot | right slot | - | - | - | - | Tri-state boolean combine. |
| 0x0C | `Equal` | - | result slot | left slot | right slot | - | - | - | - | Binary comparison. |
| 0x0D | `NotEqual` | - | result slot | left slot | right slot | - | - | - | - | Binary comparison. |
| 0x0E | `ApproxEqual` | - | result slot | left slot | right slot | - | - | - | - | Approximate equality. |
| 0x0F | `Less` | - | result slot | left slot | right slot | - | - | - | - | Binary comparison. |
| 0x10 | `Greater` | - | result slot | left slot | right slot | - | - | - | - | Binary comparison. |
| 0x11 | `LessOrEqual` | - | result slot | left slot | right slot | - | - | - | - | Binary comparison. |
| 0x12 | `GreaterOrEqual` | - | result slot | left slot | right slot | - | - | - | - | Binary comparison. |
| 0x13 | `Add` | - | result slot | left slot | right slot | - | - | - | - | Binary operation. |
| 0x14 | `Subtract` | - | result slot | left slot | right slot | - | - | - | - | Binary operation. |
| 0x15 | `Multiply` | - | result slot | left slot | right slot | - | - | - | - | Binary operation. |
| 0x16 | `Divide` | - | result slot | left slot | right slot | - | - | - | - | Binary operation. |
| 0x17 | `IntegerDivide` | - | result slot | left slot | right slot | - | - | - | - | Binary operation. |
| 0x18 | `Modulo` | - | result slot | left slot | right slot | - | - | - | - | Binary operation. |
| 0x19 | `Remainder` | - | result slot | left slot | right slot | - | - | - | - | Binary operation. |
| 0x1A | `PrimitiveIntegerEqual` | - | result slot | left slot | right slot | - | - | - | - | Integer fast-path comparison. |
| 0x1B | `PrimitiveIntegerNotEqual` | - | result slot | left slot | right slot | - | - | - | - | Integer fast-path comparison. |
| 0x1C | `PrimitiveIntegerLess` | - | result slot | left slot | right slot | - | - | - | - | Integer fast-path comparison. |
| 0x1D | `PrimitiveIntegerGreater` | - | result slot | left slot | right slot | - | - | - | - | Integer fast-path comparison. |
| 0x1E | `PrimitiveIntegerLessOrEqual` | - | result slot | left slot | right slot | - | - | - | - | Integer fast-path comparison. |
| 0x1F | `PrimitiveIntegerGreaterOrEqual` | - | result slot | left slot | right slot | - | - | - | - | Integer fast-path comparison. |
| 0x20 | `PrimitiveIntegerAdd` | - | result slot | left slot | right slot | - | - | - | - | Integer fast-path arithmetic. |
| 0x21 | `PrimitiveIntegerSubtract` | - | result slot | left slot | right slot | - | - | - | - | Integer fast-path arithmetic. |
| 0x22 | `PrimitiveIntegerMultiply` | - | result slot | left slot | right slot | - | - | - | - | Integer fast-path arithmetic. |
| 0x23 | `PrimitiveIntegerDivide` | - | result slot | left slot | right slot | - | - | - | - | Integer fast-path arithmetic. |
| 0x24 | `PrimitiveIntegerFloorDivide` | - | result slot | left slot | right slot | - | - | - | - | Integer fast-path arithmetic. |
| 0x25 | `PrimitiveIntegerModulo` | - | result slot | left slot | right slot | - | - | - | - | Integer fast-path arithmetic. |
| 0x26 | `PrimitiveIntegerRemainder` | - | result slot | left slot | right slot | - | - | - | - | Integer fast-path arithmetic. |
| 0x27 | `Default` | - | result slot | left slot | right slot | - | - | - | - | Nothing/default operator. |
| 0x28 | `Contains` | - | result slot | left slot | right slot | - | - | - | - | Collection/text operation. |
| 0x29 | `ContainsValue` | - | result slot | left slot | right slot | - | - | - | - | Collection/text operation. |
| 0x2A | `StartsWith` | - | result slot | left slot | right slot | - | - | - | - | Text operation. |
| 0x2B | `EndsWith` | - | result slot | left slot | right slot | - | - | - | - | Text operation. |
| 0x2C | `Intersect` | - | result slot | left slot | right slot | - | - | - | - | Collection operation. |
| 0x2D | `Combine` | - | result slot | left slot | right slot | - | - | - | - | Collection operation. |
| 0x2E | `Except` | - | result slot | left slot | right slot | - | - | - | - | Collection operation. |
| 0x2F | `Zip` | - | result slot | left slot | right slot | - | - | - | - | Collection operation. |
| 0x30 | `UnaryNegate` | - | result slot | operand slot | - | - | - | - | - | Numeric negation. |
| 0x31 | `UnaryNot` | - | result slot | operand slot | - | - | - | - | - | Logical negation. |
| 0x32 | `UnaryHasValue` | - | result slot | operand slot | - | - | - | - | - | Semantic value check. |
| 0x33 | `UnaryEmpty` | - | result slot | operand slot | - | - | - | - | - | Semantic emptiness check. |
| 0x34 | `UnaryLength` | - | result slot | operand slot | - | - | - | - | - | Length operation. |
| 0x35 | `UnaryChance` | - | result slot | operand slot | - | - | - | - | - | Chance evaluation. |
| 0x36 | `UnaryKeys` | - | result slot | operand slot | - | - | - | - | - | Dictionary/record keys projection. |
| 0x37 | `UnaryValues` | - | result slot | operand slot | - | - | - | - | - | Dictionary/record values projection. |
| 0x38 | `UnaryEntries` | - | result slot | operand slot | - | - | - | - | - | Dictionary/record entries projection. |
| 0x39 | `UnaryAbs` | - | result slot | operand slot | - | - | - | - | - | Absolute value. |
| 0x3A | `UnaryNaturalLog` | - | result slot | operand slot | - | - | - | - | - | Natural logarithm. |
| 0x3B | `Variadic` | - | result slot | first argument slot | second argument slot | `OperationLayouts` index | - | - | - | Full argument list is in layout. |
| 0x3C | `Clamp` | - | result slot | value slot | minimum slot | maximum slot | - | - | - | The only opcode with three direct source slots. |
| 0x3D | `Random` | - | result slot | from slot | to slot | - | - | - | - | Uses current random scope. |
| 0x3E | `Range` | - | result slot | from slot | to slot | - | - | - | - | Builds a range with implicit step `1`. |
| 0x3F | `RangeWithStep` | - | result slot | from slot | to slot | step slot | - | - | - | Builds a range with explicit step. |
| 0x40 | `Dice` | - | result slot | dice count immediate | side count immediate | - | - | - | - | `A16` and `B16` are not slots. |
| 0x41 | `RandomPush` | - | - | seed slot | - | - | - | - | - | Pushes a nested random scope from a dynamic unitless integer seed slot. |
| 0x42 | `RandomPushConstant` | - | - | n/a | n/a | n/a | n/a | unsigned seed | - | Pushes a nested random scope from inline `U64`. |
| 0x43 | `RandomPop` | - | - | - | - | - | - | - | - | Restores the previous random scope. |
| 0x44..0x5D | `CastNothing`..`CastOptional` | - | result slot | source slot | - | - | - | - | - | Direct built-in declared-type conversion. |
| 0x5E | `CastCustom` | - | result slot | source slot | - | `StringPool` index | - | - | - | Declared-type conversion for custom record/external types. |
| 0x5F | `TypeConstructor` | - | result slot | first argument slot | second argument slot | `OperationLayouts` index | - | - | - | Full argument list and names are in layout. |
| 0x60 | `PredicateTest` | - | result slot | tested value slot | - | `OperationLayouts` index | - | - | - | Enters a predicate frame or fast path. |
| 0x61 | `MemberAccess` | - | result slot | target slot | - | `StringPool` index | - | - | - | Reads a named member. |
| 0x62 | `IndexedAccess` | - | result slot | target slot | index slot | - | - | - | - | Direct indexed lookup. |
| 0x63 | `BuildList` | - | result slot | first item slot | second item slot | `OperationLayouts` index | - | - | - | Full item list is in layout. |
| 0x64 | `BuildSequence` | - | result slot | first item slot | second item slot | `OperationLayouts` index | - | - | - | Full item list is in layout. |
| 0x65 | `BuildSet` | - | result slot | first item slot | second item slot | `OperationLayouts` index | - | - | - | Full item list is in layout. |
| 0x66 | `BuildDictionary` | - | result slot | first value slot | second value slot | `OperationLayouts` index | - | - | - | Keys and full value list are in layout. |
| 0x67 | `BuildMessage` | - | result slot | first argument slot | second argument slot | `OperationLayouts` index | - | - | - | Message name, signature, names, and slots are in layout. |
| 0x68 | `BindHandler` | - | result slot | handler slot | first bound argument slot | `OperationLayouts` index | - | - | - | Full operand list is in layout. |
| 0x69 | `CallExtension` | - | result slot | first argument slot | second argument slot | `OperationLayouts` index | - | - | - | Extension reference and argument metadata are in layout. |
| 0x6A | `Call` | - | result slot | first argument slot | second argument slot | `OperationLayouts` index | - | - | - | Enters a VM-owned call frame. |
| 0x6B..0x83 | `TypeCheckNothing`..`TypeCheckDice` | - | result slot | source slot | - | - | - | - | - | Direct built-in type predicate. |
| 0x84 | `TypeCheckCustom` | - | result slot | source slot | - | `StringPool` index | - | - | - | Type predicate for custom record/external types. |
| 0x85 | `Pipeline` | - | result slot | - | - | `PipelinePool` index | - | - | - | Pipeline entry stores source slot and selector indexes. |
| 0x86 | `GeneratedCollection` | - | result slot | - | - | `GeneratedCollectionLayouts` index | - | - | - | Layout stores iteration source and helper entries. |
| 0x87 | `GuardedChoice` | - | result slot | - | - | `GuardedChoiceLayouts` index | - | - | - | Layout stores value/condition helper entries. |
| 0x88 | `Power` | - | result slot | left slot | right slot | - | - | - | - | Binary operation. |
| 0x89 | `ShortCircuitOr` | - | - | - | - | - | - | - | - | Lowering marker only; runtime uses jumps plus `Or`. |
| 0x8A | `ShortCircuitAnd` | - | - | - | - | - | - | - | - | Lowering marker only; runtime uses jumps plus `And`. |
| 0x8B | `ShortCircuitImplies` | - | result slot | antecedent slot | consequent slot | - | - | - | - | Binary implication combine. |
| 0x8C | `Nop` | - | - | - | - | - | - | - | - | No operation. |
| 0x8D | `BindParameter` | - | parameter slot | parameter index immediate | - | - | - | - | - | Reads invocation argument `A16` and writes it to `Dest16`. |
| 0x8E | `Jump` | - | - | target address | - | - | - | - | - | Unconditional branch. |
| 0x8F | `JumpIfTrue` | - | - | target address | - | condition slot | - | - | - | Branches when `C16.IsTrue()`. |
| 0x90 | `JumpIfFalse` | - | - | target address | - | condition slot | - | - | - | Branches when `C16.IsFalse()`. |
| 0x91 | `JumpIfNotTrue` | - | - | target address | - | condition slot | - | - | - | Branches when `!C16.IsTrue()`, including `nothing`. |
| 0x92 | `EnterScope` | - | - | - | - | - | - | - | - | Pushes a scope mark for local-slot cleanup. |
| 0x93 | `ExitScope` | - | - | - | - | - | - | - | - | Pops a scope and restores changed slots. |
| 0x94 | `Return` | - | - | optional return slot | - | - | - | - | - | `A16 = -1` returns `nothing`. |
| 0x95 | `EmitMessage` | - | - | message shape `UShortListPool` index | argument slot-list `UShortListPool` index | tag slot-list `UShortListPool` index | - | - | - | Emits a statically shaped message. |
| 0x96 | `PublishMessage` | - | - | message shape `UShortListPool` index | argument slot-list `UShortListPool` index | tag slot-list `UShortListPool` index | - | - | - | Publishes a statically shaped message. |
| 0x97 | `EmitMessageValue` | - | - | message slot | - | tag slot-list `UShortListPool` index | - | - | - | Emits a dynamic message value. |
| 0x98 | `PublishMessageValue` | - | - | message slot | - | tag slot-list `UShortListPool` index | - | - | - | Publishes a dynamic message value. |
| 0x99 | `ForRange` | - | - | body target address | end target address | `LoopLayouts` index | - | - | - | Layout links identifier slot and range iteration source. |
| 0x9A | `ForCollection` | - | - | body target address | end target address | `LoopLayouts` index | - | - | - | Layout links identifier slot and collection source. |

## Side-Table Summary

| Pool / table | Used by |
| --- | --- |
| `StringPool` | `LoadText`, `LoadTag`, `MemberAccess`; indirectly through message/name lists in `UShortListPool` |
| `UShortListPool` | `LoadHandler`, `EmitMessage`, `PublishMessage`, `EmitMessageValue`, `PublishMessageValue`, operation name lists |
| `OperationLayouts` | `Variadic`, `TypeConstructor`, `PredicateTest`, `BuildList`, `BuildSequence`, `BuildSet`, `BuildDictionary`, `BuildMessage`, `BindHandler`, `CallExtension`, `Call` |
| `LoopLayouts` | `ForRange`, `ForCollection` |
| `PipelinePool` | `Pipeline` |
| `PipelineSelectorPool` | Referenced by `PipelinePool` |
| `PipelinePatternPool` | Referenced by `PipelineSelectorPool` |
| `PipelineObjectPatternPool` | Referenced by `PipelineSelectorPool` |
| `GeneratedCollectionLayouts` | `GeneratedCollection` |
| `GuardedChoiceLayouts` | `GuardedChoice` |
