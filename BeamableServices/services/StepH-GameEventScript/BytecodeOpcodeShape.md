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

    [FieldOffset(2)]  public ushort Dest_U16;
    [FieldOffset(4)]  public ushort A_U16;
    [FieldOffset(6)]  public ushort B_U16;
    [FieldOffset(8)]  public ushort C_U16;
    [FieldOffset(10)] public ushort D_U16;

    [FieldOffset(4)]  public short A_I16;
    [FieldOffset(6)]  public short B_I16;
    [FieldOffset(8)]  public short C_I16;
    [FieldOffset(10)] public short D_I16;

    [FieldOffset(4)]  public int A_I32;
    [FieldOffset(8)]  public int B_I32;
    [FieldOffset(4)]  public uint A_U32;
    [FieldOffset(8)]  public uint B_U32;

    [FieldOffset(4)]  public long I64;
    [FieldOffset(4)]  public ulong U64;
    [FieldOffset(4)]  public double F64;
}
```

The byte layout is:

| Byte range | 16-bit view | 32-bit view | 64-bit view | Typical use |
| --- | --- | --- | --- | --- |
| `0` | `OpCode` | `OpCode` | `OpCode` | Opcode tag, backed by `byte`. |
| `1` | `UnitAndFlags` | `UnitAndFlags` | `UnitAndFlags` | Low 5 bits carry the numeric unit id; high 3 bits are reserved flags. |
| `2..3` | `Dest_U16` | `Dest_U16` | `Dest_U16` | Destination slot for value-producing instructions. |
| `4..5` | `A_U16` / `A_I16` | `A_I32` / `A_U32` low half | `I64`/`U64`/`F64` bytes `0..1` | First operand, or payload bytes. |
| `6..7` | `B_U16` / `B_I16` | `A_I32` / `A_U32` high half | `I64`/`U64`/`F64` bytes `2..3` | Second operand, or payload bytes. |
| `8..9` | `C_U16` / `C_I16` | `B_I32` / `B_U32` low half | `I64`/`U64`/`F64` bytes `4..5` | Third operand/layout index, or payload bytes. |
| `10..11` | `D_U16` / `D_I16` | `B_I32` / `B_U32` high half | `I64`/`U64`/`F64` bytes `6..7` | Fourth operand when needed, or payload bytes. |

There are no compatibility helper operands and no instruction-word sentinel for
"unused". Unused fields are undefined/ignored. Only the fields documented for a
specific opcode may be read or validated. Optional instruction forms are encoded
with dedicated opcodes or concrete empty pool entries, not with sentinel
operands.

## JSON Shape

`System.Text.Json` serializes a bytecode instruction through a stable transport
view instead of exposing the overlapping runtime fields:

```json
{
  "Opcode": "LoadInteger",
  "Flags": "0x04",
  "Dst": "0x0007",
  "Parameter": "0x000000000000002A"
}
```

- `Opcode` is the enum name. Numeric byte values and hex byte strings are also
  accepted while reading.
- `Flags` is the raw `UnitAndFlags` byte as hex.
- `Dst` is the raw `Dest_U16` slot as hex.
- `Parameter` is the raw unsigned 64-bit payload at bytes `4..11` as hex. For
  16-bit operand opcodes, bits `0..15` are `A`, `16..31` are `B`, `32..47` are
  `C`, and `48..63` are `D`. Signed operand views use the same bits as
  two's-complement values. `LoadFloat` stores the IEEE-754 double bit pattern in
  the same payload.

## Operand Views

- **Unsigned 16-bit operand view:** most instructions use `Dest_U16`, `A_U16`,
  `B_U16`, and `C_U16`. Examples: slots, branch targets, side-table indexes,
  dice immediates, and message-shape/list indexes.
- **Signed 16-bit operand view:** `A_I16`, `B_I16`, `C_I16`, and `D_I16` are
  reserved for compact signed immediates such as short range bounds.
- **64-bit integer view:** `LoadInteger` reads `I64` directly. Negative integer
  values are stored as their normal two's-complement bit pattern.
- **64-bit float view:** `LoadFloat` reads `F64` directly. This keeps IEEE-754
  `NaN`, `Infinity`, and `-Infinity` portable as raw double bits.
- **Unsigned payload view:** `RandomPushConstant` reads `U64` directly as the
  portable seeded-random seed. Signed integer seeds are mapped to `U64` by their
  normal two's-complement bit pattern.
- **32-bit views:** `A_I32/B_I32` and `A_U32/B_U32` are available for future
  compact immediates. Current bytecode does not need a dedicated 32-bit
  immediate load.

## Unit And Flags Byte

`UnitAndFlags` is byte-sized and is interpreted as:

```text
bits 0..4  UnitId        0..31
bits 5..7  Reserved      must be zero in portable bytecode
```

The reserved bits are intentionally allocated from the most significant bit
downward if they are ever needed. Bit 5 should remain the last reserved bit to
consume, so the low field can grow from 5 to 6 unit bits later if opcode flags
never need the full reserved range.

The target UnitId map is:

| UnitId | Unit | Meaning |
| --- | --- | --- |
| `0` | none | Unitless scalar. |
| `1` | degree | Angle, heading, field of view, rotation. |
| `2` | meter | Position, distance, range, radius. |
| `3` | second | Duration, cooldown, cast time, tick time. |
| `4` | ratio / percent | Chance, multiplier, resistance; `%` literals store ratios. |
| `5` | meter per second | Speed and velocity magnitude. |
| `6` | meter per second squared | Acceleration. |
| `7` | kilogram | Mass, inventory load, inertia. |
| `8` | newton | Force, thrust, recoil. |
| `9` | joule | Energy, battery charge, heat energy. |
| `10` | watt | Power, generator output, consumption over time. |
| `11` | volt | Voltage for electrotechnical systems. |
| `12` | ampere | Current, charge flow, overload/thermal balancing. |
| `13` | hertz | Frequency, fire rate, sensor polling, radio rate. |
| `14` | bit | Information amount. |
| `15` | byte | Storage amount. |
| `16` | bit per second | Bandwidth and communication throughput. |
| `17` | kelvin | Temperature; Celsius/Fahrenheit syntax should normalize to Kelvin. |
| `18..31` | reserved | Reserved for future built-in or domain units. |

Only the units implemented by the current runtime may be emitted by the
compiler. The full map above is the portable binary target, not a promise that
every unit has DSL syntax today.

## Conventions

- `Dest_U16` is the destination frame slot for value-producing instructions.
- `A_U16`, `B_U16`, `C_U16`, and `D_U16` in the opcode table are unsigned
  16-bit typed fields.
- Unused instruction fields are intentionally not specified.
- Branch opcodes use `A_U16` as the target address.
- Conditional branches use `C_U16` as the condition slot.
- Loop/block opcodes use `A_U16` as body target, `B_U16` as end target, and `C_U16` as
  layout index.
- `LoadInteger` uses the overlapped `I64` payload and may use `UnitAndFlags`
  for numeric units.
- `LoadFloat` uses the overlapped `F64` payload. `UnitAndFlags` carries `None`,
  numeric units, or `Percentage`.
- Side-table-backed opcodes use `C_U16` as the data/layout index. Their complete
  operand list lives in the side table; `A_U16` and `B_U16` only mirror the
  first one or two slots when that is useful.

## Opcode Table

| Hex | Opcode | UnitAndFlags | Dest_U16 | A_U16 | B_U16 | C_U16 | D_U16 | I64/U64 | F64 | Notes |
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
| 0x40 | `Dice` | - | result slot | dice count immediate | side count immediate | - | - | - | - | `A_U16` and `B_U16` are not slots. |
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
| 0x86 | `CollectionBuilderList` | - | builder slot | - | - | - | - | - | - | Creates a VM-internal list builder. |
| 0x87 | `CollectionBuilderSet` | - | builder slot | - | - | - | - | - | - | Creates a VM-internal set builder. |
| 0x88 | `CollectionBuilderAdd` | - | - | builder slot | item slot | - | - | - | - | Adds an item and checks `MaxGeneratedCollectionItems`. |
| 0x89 | `CollectionBuilderFinish` | - | result slot | builder slot | - | - | - | - | - | Materializes the builder as a list or set. |
| 0x8A | `Power` | - | result slot | left slot | right slot | - | - | - | - | Binary operation. |
| 0x8B | `ShortCircuitOr` | - | - | - | - | - | - | - | - | Lowering marker only; runtime uses jumps plus `Or`. |
| 0x8C | `ShortCircuitAnd` | - | - | - | - | - | - | - | - | Lowering marker only; runtime uses jumps plus `And`. |
| 0x8D | `ShortCircuitImplies` | - | result slot | antecedent slot | consequent slot | - | - | - | - | Binary implication combine. |
| 0x8E | `Nop` | - | - | - | - | - | - | - | - | No operation. |
| 0x8F | `BindParameter` | - | parameter slot | parameter index immediate | - | - | - | - | - | Reads invocation argument `A_U16` and writes it to `Dest_U16`. |
| 0x90 | `Jump` | - | - | target address | - | - | - | - | - | Unconditional branch. |
| 0x91 | `JumpIfTrue` | - | - | target address | - | condition slot | - | - | - | Branches when `C_U16.IsTrue()`. |
| 0x92 | `JumpIfFalse` | - | - | target address | - | condition slot | - | - | - | Branches when `C_U16.IsFalse()`. |
| 0x93 | `JumpIfNotTrue` | - | - | target address | - | condition slot | - | - | - | Branches when `!C_U16.IsTrue()`, including `nothing`. |
| 0x94 | `EnterScope` | - | - | - | - | - | - | - | - | Pushes a scope mark for local-slot cleanup. |
| 0x95 | `ExitScope` | - | - | - | - | - | - | - | - | Pops a scope and restores changed slots. |
| 0x96 | `ReturnNothing` | - | - | - | - | - | - | - | - | Returns `nothing` from the current frame. |
| 0x97 | `Return` | - | - | return slot | - | - | - | - | - | Returns the value in `A_U16` from the current frame. |
| 0x98 | `EmitMessage` | - | - | message shape `UShortListPool` index | argument slot-list `UShortListPool` index | - | - | - | - | Emits a statically shaped message without tags. |
| 0x99 | `EmitMessageWithTags` | - | - | message shape `UShortListPool` index | argument slot-list `UShortListPool` index | tag slot-list `UShortListPool` index | - | - | - | Emits a statically shaped message with tags. |
| 0x9A | `PublishMessage` | - | - | message shape `UShortListPool` index | argument slot-list `UShortListPool` index | - | - | - | - | Publishes a statically shaped message without tags. |
| 0x9B | `PublishMessageWithTags` | - | - | message shape `UShortListPool` index | argument slot-list `UShortListPool` index | tag slot-list `UShortListPool` index | - | - | - | Publishes a statically shaped message with tags. |
| 0x9C | `EmitMessageValue` | - | - | message slot | - | - | - | - | - | Emits a dynamic message value without tags. |
| 0x9D | `EmitMessageValueWithTags` | - | - | message slot | - | tag slot-list `UShortListPool` index | - | - | - | Emits a dynamic message value with tags. |
| 0x9E | `PublishMessageValue` | - | - | message slot | - | - | - | - | - | Publishes a dynamic message value without tags. |
| 0x9F | `PublishMessageValueWithTags` | - | - | message slot | - | tag slot-list `UShortListPool` index | - | - | - | Publishes a dynamic message value with tags. |
| 0xA0 | `RangeIterator` | - | iterator slot | from slot | to slot | - | - | - | - | Creates a VM-internal range iterator with default step `+1`. |
| 0xA1 | `RangeIteratorWithStep` | - | iterator slot | from slot | to slot | step slot | - | - | - | Creates a VM-internal range iterator with an explicit step. |
| 0xA2 | `RangeIteratorShort` | - | iterator slot | from I16 | to I16 | step I16 | - | - | - | Creates a compact literal range iterator. |
| 0xA3 | `CollectionIterator` | - | iterator slot | collection slot | - | - | - | - | - | Creates a VM-internal iterator over a collection/range/sequence value. |
| 0xA4 | `IteratorNext` | - | item slot | iterator slot | no-more target address | - | - | - | - | Writes the next item and continues, or jumps to `B_U16` when exhausted. |
| 0xA5 | `IteratorClose` | - | - | iterator slot | - | - | - | - | - | Disposes/closes a VM-internal iterator. |

## Side-Table Summary

| Pool / table | Used by |
| --- | --- |
| `StringPool` | `LoadText`, `LoadTag`, `MemberAccess`; indirectly through message/name lists in `UShortListPool` |
| `UShortListPool` | `LoadHandler`, `EmitMessage*`, `PublishMessage*`, operation name lists |
| `OperationLayouts` | `Variadic`, `TypeConstructor`, `PredicateTest`, `BuildList`, `BuildSequence`, `BuildSet`, `BuildDictionary`, `BuildMessage`, `BindHandler`, `CallExtension`, `Call` |
| `PipelinePool` | `Pipeline` |
| `PipelineSelectorPool` | Referenced by `PipelinePool` |
| `PipelinePatternPool` | Referenced by `PipelineSelectorPool` |
| `PipelineObjectPatternPool` | Referenced by `PipelineSelectorPool` |
