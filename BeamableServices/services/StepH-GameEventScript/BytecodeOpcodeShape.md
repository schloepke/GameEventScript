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

The instruction word has no sentinel for "unused". Unused fields are
undefined/ignored. Only the fields documented for a specific opcode may be read
or validated. Optional instruction forms are encoded with dedicated opcodes or
concrete empty pool entries, not with sentinel operands.

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
| `4` | meter per second | Speed and velocity magnitude. |
| `5` | meter per second squared | Acceleration. |
| `6` | kilogram | Mass, inventory load, inertia. |
| `7` | newton | Force, thrust, recoil. |
| `8` | joule | Energy, battery charge, heat energy. |
| `9` | watt | Power, generator output, consumption over time. |
| `10` | volt | Voltage for electrotechnical systems. |
| `11` | ampere | Current, charge flow, overload/thermal balancing. |
| `12` | hertz | Frequency, fire rate, sensor polling, radio rate. |
| `13` | bit | Information amount. |
| `14` | byte | Storage amount. |
| `15` | bit per second | Bandwidth and communication throughput. |
| `16` | kelvin | Temperature; Celsius/Fahrenheit syntax should normalize to Kelvin. |
| `17..31` | reserved | Reserved for future built-in or domain units. |

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
- `LoadFloat` uses the overlapped `F64` payload. `UnitAndFlags` carries `None`
  or a numeric unit. `LoadPercentage` uses the same `F64` payload with no unit
  flags and creates the dedicated percentage value kind.
- Pool-backed opcodes use the documented `StringPool` or `UShortListPool`
  indices directly in `A_U16`, `B_U16`, `C_U16`, or `D_U16`.

## Opcode Table

Opcode values are split into 16-value operation-family groups. The VM dispatches
on the complete byte value; the high nibble is a format convention, not a second
runtime dispatch step.

| Group | Purpose |
| --- | --- |
| `0x00` | Core loads, slot movement, branches, returns |
| `0x10` | Generic boolean/comparison/arithmetic/default operations |
| `0x20` | Primitive integer fast paths |
| `0x30` | Unary, random, dice, range value operations |
| `0x40` | Collection/text operations, projections, implication |
| `0x50` | Generic declared-type and unit casts |
| `0x60` | Reserved former cast space |
| `0x70` | Generic declared-type and unit checks |
| `0x80` | Reserved former type-check space |
| `0x90` | Construction, access, handlers, predicates, calls |
| `0xA0` | Scopes and message emit/publish operations |
| `0xB0` | Iterators, collection builders, reduction, and series operations |
| `0xC0` | Calls |
| `0xD0` | Pipeline iterator, materializer, and collection terminals |
| `0xE0` | Pipeline materializing, ordering, and slicing terminals |
| `0xF0` | Pipeline random and pattern terminals |

| Hex | Opcode | UnitAndFlags | Dest_U16 | A_U16 | B_U16 | C_U16 | D_U16 | I64/U64 | F64 | Notes |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 0x00 | `Nop` | - | - | - | - | - | - | - | - | No operation. |
| 0x01 | `LoadNothing` | - | result slot | - | - | - | - | - | - | Loads `nothing`. |
| 0x02 | `LoadTrue` | - | result slot | - | - | - | - | - | - | Loads boolean `true`. |
| 0x03 | `LoadFalse` | - | result slot | - | - | - | - | - | - | Loads boolean `false`. |
| 0x04 | `LoadInteger` | numeric unit | result slot | n/a | n/a | n/a | n/a | signed integer | - | Loads an inline signed `Int64`. |
| 0x05 | `LoadFloat` | numeric unit | result slot | n/a | n/a | n/a | n/a | - | float | Loads an inline IEEE-754 `Float64`. |
| 0x06 | `LoadText` | - | result slot | - | - | `StringPool` index | - | - | - | Loads a text literal. |
| 0x07 | `LoadTag` | - | result slot | - | - | `StringPool` index | - | - | - | Loads a tag literal. |
| 0x08 | `MoveSlot` | - | result slot | source slot | - | - | - | - | - | Copies a slot value/reference; the source slot remains unchanged. |
| 0x09 | `LoadPercentage` | - | result slot | n/a | n/a | n/a | n/a | - | ratio | Loads an inline percentage ratio as the dedicated percentage value kind. |
| 0x0A | `Jump` | - | - | target address | - | - | - | - | - | Unconditional branch. |
| 0x0B | `JumpIfTrue` | - | - | target address | - | condition slot | - | - | - | Branches when `C_U16.IsTrue()`. |
| 0x0C | `JumpIfFalse` | - | - | target address | - | condition slot | - | - | - | Branches when `C_U16.IsFalse()`. |
| 0x0D | `JumpIfNotTrue` | - | - | target address | - | condition slot | - | - | - | Branches when `!C_U16.IsTrue()`, including `nothing`. |
| 0x0E | `ReturnVoid` | - | - | - | - | - | - | - | - | Returns no value from the current frame; normal calls map this to DSL `nothing`. |
| 0x0F | `ReturnValue` | - | - | return slot | - | - | - | - | - | Returns the value in `A_U16` from the current frame. |
| 0x10 | `Or` | - | result slot | left slot | right slot | - | - | - | - | Tri-state boolean combine. |
| 0x11 | `And` | - | result slot | left slot | right slot | - | - | - | - | Tri-state boolean combine. |
| 0x12 | `Xor` | - | result slot | left slot | right slot | - | - | - | - | Binary operation. |
| 0x13 | `Equal` | - | result slot | left slot | right slot | - | - | - | - | Binary comparison. |
| 0x14 | `NotEqual` | - | result slot | left slot | right slot | - | - | - | - | Binary comparison. |
| 0x15 | `ApproxEqual` | - | result slot | left slot | right slot | - | - | - | - | Approximate equality. |
| 0x16 | `Less` | - | result slot | left slot | right slot | - | - | - | - | Binary comparison. |
| 0x17 | `Greater` | - | result slot | left slot | right slot | - | - | - | - | Binary comparison. |
| 0x18 | `LessOrEqual` | - | result slot | left slot | right slot | - | - | - | - | Binary comparison. |
| 0x19 | `GreaterOrEqual` | - | result slot | left slot | right slot | - | - | - | - | Binary comparison. |
| 0x1A | `Add` | - | result slot | left slot | right slot | - | - | - | - | Binary operation. |
| 0x1B | `Subtract` | - | result slot | left slot | right slot | - | - | - | - | Binary operation. |
| 0x1C | `Multiply` | - | result slot | left slot | right slot | - | - | - | - | Binary operation. |
| 0x1D | `Divide` | - | result slot | left slot | right slot | - | - | - | - | Binary operation. |
| 0x1E | `Power` | - | result slot | left slot | right slot | - | - | - | - | Binary operation. |
| 0x1F | `Default` | - | result slot | left slot | right slot | - | - | - | - | Nothing/default operator. |
| 0x20 | `PrimitiveIntegerEqual` | - | result slot | left slot | right slot | - | - | - | - | Integer fast-path equality comparison. |
| 0x21 | `PrimitiveIntegerNotEqual` | - | result slot | left slot | right slot | - | - | - | - | Integer fast-path inequality comparison. |
| 0x22 | `PrimitiveIntegerLess` | - | result slot | left slot | right slot | - | - | - | - | Integer fast-path less-than comparison. |
| 0x23 | `PrimitiveIntegerGreater` | - | result slot | left slot | right slot | - | - | - | - | Integer fast-path greater-than comparison. |
| 0x24 | `PrimitiveIntegerLessOrEqual` | - | result slot | left slot | right slot | - | - | - | - | Integer fast-path less-or-equal comparison. |
| 0x25 | `PrimitiveIntegerGreaterOrEqual` | - | result slot | left slot | right slot | - | - | - | - | Integer fast-path greater-or-equal comparison. |
| 0x26 | `PrimitiveIntegerAdd` | - | result slot | left slot | right slot | - | - | - | - | Integer fast-path addition. |
| 0x27 | `PrimitiveIntegerSubtract` | - | result slot | left slot | right slot | - | - | - | - | Integer fast-path subtraction. |
| 0x28 | `PrimitiveIntegerMultiply` | - | result slot | left slot | right slot | - | - | - | - | Integer fast-path multiplication. |
| 0x29 | `PrimitiveIntegerDivide` | - | result slot | left slot | right slot | - | - | - | - | Integer fast-path division. |
| 0x2A | `PrimitiveIntegerFloorDivide` | - | result slot | left slot | right slot | - | - | - | - | Integer fast-path floor division. |
| 0x2B | `PrimitiveIntegerModulo` | - | result slot | left slot | right slot | - | - | - | - | Integer fast-path modulo. |
| 0x2C | `PrimitiveIntegerRemainder` | - | result slot | left slot | right slot | - | - | - | - | Integer fast-path remainder. |
| 0x2D | `IntegerDivide` | - | result slot | left slot | right slot | - | - | - | - | Binary operation. |
| 0x2E | `Modulo` | - | result slot | left slot | right slot | - | - | - | - | Binary operation. |
| 0x2F | `Remainder` | - | result slot | left slot | right slot | - | - | - | - | Binary operation. |
| 0x30 | `UnaryNegate` | - | result slot | operand slot | - | - | - | - | - | Numeric negation. |
| 0x31 | `UnaryNot` | - | result slot | operand slot | - | - | - | - | - | Logical negation. |
| 0x32 | `UnaryHasValue` | - | result slot | operand slot | - | - | - | - | - | Semantic value check. |
| 0x33 | `UnaryEmpty` | - | result slot | operand slot | - | - | - | - | - | Semantic emptiness check. |
| 0x34 | `UnaryLength` | - | result slot | operand slot | - | - | - | - | - | Length operation. |
| 0x35 | `UnaryChance` | - | result slot | operand slot | - | - | - | - | - | Chance evaluation. |
| 0x36 | `UnaryAbs` | - | result slot | operand slot | - | - | - | - | - | Absolute value. |
| 0x37 | `UnaryNaturalLog` | - | result slot | operand slot | - | - | - | - | - | Natural logarithm. |
| 0x38 | `Clamp` | - | result slot | value slot | minimum slot | maximum slot | - | - | - | The only opcode with three direct source slots. |
| 0x39 | `Random` | - | result slot | from slot | to slot | - | - | - | - | Uses current random scope. |
| 0x3A | `Dice` | - | result slot | dice count immediate | side count immediate | - | - | - | - | `A_U16` and `B_U16` are not slots. |
| 0x3B | `RandomPush` | - | - | seed slot | - | - | - | - | - | Pushes a nested random scope from a dynamic unitless integer seed slot. |
| 0x3C | `RandomPushConstant` | - | - | n/a | n/a | n/a | n/a | unsigned seed | - | Pushes a nested random scope from inline `U64`. |
| 0x3D | `RandomPop` | - | - | - | - | - | - | - | - | Restores the previous random scope. |
| 0x3E | `Range` | - | result slot | from slot | to slot | - | - | - | - | Builds a range with implicit step `1`. |
| 0x3F | `RangeWithStep` | - | result slot | from slot | to slot | step slot | - | - | - | Builds a range with explicit step. |
| 0x40 | `Contains` | - | result slot | left slot | right slot | - | - | - | - | Collection/text operation. |
| 0x41 | `ContainsValue` | - | result slot | left slot | right slot | - | - | - | - | Collection/text operation. |
| 0x42 | `StartsWith` | - | result slot | left slot | right slot | - | - | - | - | Text operation. |
| 0x43 | `EndsWith` | - | result slot | left slot | right slot | - | - | - | - | Text operation. |
| 0x44 | `Intersect` | - | result slot | left slot | right slot | - | - | - | - | Collection operation. |
| 0x45 | `Combine` | - | result slot | left slot | right slot | - | - | - | - | Collection operation. |
| 0x46 | `Except` | - | result slot | left slot | right slot | - | - | - | - | Collection operation. |
| 0x47 | `Zip` | - | result slot | left slot | right slot | - | - | - | - | Collection operation. |
| 0x48 | `UnaryKeys` | - | result slot | operand slot | - | - | - | - | - | Map/record keys projection. |
| 0x49 | `UnaryValues` | - | result slot | operand slot | - | - | - | - | - | Map/record values projection. |
| 0x4A | `UnaryEntries` | - | result slot | operand slot | - | - | - | - | - | Map/record entries projection. |
| 0x4B | `Min` | - | result slot | left slot | right slot | - | - | - | - | Binary extrema reduce step. |
| 0x4C | `Max` | - | result slot | left slot | right slot | - | - | - | - | Binary extrema reduce step. |
| 0x4D | `Implies` | - | result slot | antecedent slot | consequent slot | - | - | - | - | Binary implication combine. |
| 0x4E | `ReserveSlots` | - | - | additional local slot count | - | - | - | - | - | Adds `A_U16` active local slots to the current frame. Entry prologs reserve only locals beyond preloaded arguments. |
| 0x4F | reserved | - | - | - | - | - | - | - | - | Reserved for future collection/text, frame, or short-circuit operations. |
| 0x50 | `Cast` | - | result slot | source slot | `GameEventScriptBytecodeTypeKind` | custom type `StringPool` index only when kind is `Custom` | - | - | - | Converts `A_U16` to the declared built-in/custom type. Built-ins use `B_U16`; custom types use `B_U16=Custom` plus `C_U16`. |
| 0x51 | `CastUnit` | target numeric unit | result slot | source slot | - | - | - | - | - | Converts `A_U16` to the numeric unit carried in `UnitAndFlags`. Units are not represented as declared type kinds. |
| 0x52..0x6F | reserved | - | - | - | - | - | - | - | - | Reserved former specialized cast opcode range. |
| 0x70 | `TypeCheck` | - | result slot | source slot | `GameEventScriptBytecodeTypeKind` | custom type `StringPool` index only when kind is `Custom` | - | - | - | Writes whether `A_U16` has the declared built-in/custom type. Built-ins use `B_U16`; custom types use `B_U16=Custom` plus `C_U16`. |
| 0x71 | `CheckUnit` | target numeric unit | result slot | source slot | - | - | - | - | - | Writes whether `A_U16` has the numeric unit carried in `UnitAndFlags`. Units are not represented as declared type kinds. |
| 0x72..0x8F | reserved | - | - | - | - | - | - | - | - | Reserved former specialized type-check opcode range. |
| 0x90 | `LoadHandler` | - | result slot | message shape `UShortListPool` index | - | - | - | - | - | Loads a handler literal. The shape list is `[messageNameStringIndex, argumentNameStringIndex...]`. |
| 0x91 | `TypeConstructor` | - | result slot | type name `StringPool` index | argument name-list `UShortListPool` index | argument slot-list `UShortListPool` index | - | - | - | Constructs a record/external value from named argument slots. |
| 0x92 | reserved | - | - | - | - | - | - | - | - | Reserved for future construction/access expansion. |
| 0x93 | `MemberAccess` | - | result slot | target slot | - | `StringPool` index | - | - | - | Reads a named member. |
| 0x94 | `IndexedAccess` | - | result slot | target slot | index slot | - | - | - | - | Direct indexed lookup. |
| 0x95 | `BuildList` | - | result slot | item slot-list `UShortListPool` index | - | - | - | - | - | Builds a list from slot-list operands. |
| 0x96 | reserved | - | - | - | - | - | - | - | - | Reserved for future construction/access operations. |
| 0x97 | reserved | - | - | - | - | - | - | - | - | Removed set construction slot. |
| 0x98 | `BuildMap` | - | result slot | key name-list `UShortListPool` index | value slot-list `UShortListPool` index | - | - | - | - | Builds a map from key names and value slots. |
| 0x99 | `BuildMessage` | - | result slot | message shape `UShortListPool` index | argument slot-list `UShortListPool` index | - | - | - | - | Builds a message value. Shape is `[messageNameStringIndex, argumentNameStringIndex...]`. |
| 0x9A | `BindHandler` | - | result slot | operand slot-list `UShortListPool` index | argument name-list `UShortListPool` index | - | - | - | - | Binds a handler value plus named arguments. Operand slot-list starts with the handler slot. |
| 0x9B | reserved | - | - | - | - | - | - | - | - | Removed variadic construction/access slot; `:min`/`:max` lower to `Min`/`Max` reduce chains. |
| 0x9C..0x9F | reserved | - | - | - | - | - | - | - | - | Reserved for future construction/access operations. |
| 0xA0 | reserved | - | - | - | - | - | - | - | - | Reserved; scope growth uses `ReserveSlots`. |
| 0xA1 | `ReleaseSlots` | - | - | removed local slot count | - | - | - | - | - | Clears and removes `A_U16` active local slots from the current frame. |
| 0xA2 | `EmitMessage` | - | - | message shape `UShortListPool` index | argument slot-list `UShortListPool` index | - | - | - | - | Emits a statically shaped message without tags. |
| 0xA3 | `EmitMessageWithTags` | - | - | message shape `UShortListPool` index | argument slot-list `UShortListPool` index | tag slot-list `UShortListPool` index | - | - | - | Emits a statically shaped message with tags. |
| 0xA4 | `PublishMessage` | - | - | message shape `UShortListPool` index | argument slot-list `UShortListPool` index | - | - | - | - | Publishes a statically shaped message without tags. |
| 0xA5 | `PublishMessageWithTags` | - | - | message shape `UShortListPool` index | argument slot-list `UShortListPool` index | tag slot-list `UShortListPool` index | - | - | - | Publishes a statically shaped message with tags. |
| 0xA6 | `EmitMessageValue` | - | - | message slot | - | - | - | - | - | Emits a dynamic message value without tags. |
| 0xA7 | `EmitMessageValueWithTags` | - | - | message slot | - | tag slot-list `UShortListPool` index | - | - | - | Emits a dynamic message value with tags. |
| 0xA8 | `PublishMessageValue` | - | - | message slot | - | - | - | - | - | Publishes a dynamic message value without tags. |
| 0xA9 | `PublishMessageValueWithTags` | - | - | message slot | - | tag slot-list `UShortListPool` index | - | - | - | Publishes a dynamic message value with tags. |
| 0xAA..0xAF | reserved | - | - | - | - | - | - | - | - | Reserved for future scope or message operations. |
| 0xB0 | `RangeIterator` | - | iterator slot | from slot | to slot | - | - | - | - | Creates a VM-internal range iterator with default step `+1`. |
| 0xB1 | `RangeIteratorWithStep` | - | iterator slot | from slot | to slot | step slot | - | - | - | Creates a VM-internal range iterator with an explicit step. |
| 0xB2 | `RangeIteratorShort` | - | iterator slot | from I16 | to I16 | step I16 | - | - | - | Creates a compact literal range iterator. |
| 0xB3 | `CollectionIterator` | - | iterator slot | collection slot | - | - | - | - | - | Creates a VM-internal iterator over a collection or range value. |
| 0xB4 | `IteratorNext` | - | item slot | iterator slot | no-more target address | - | - | - | - | Writes the next item and continues, or jumps to `B_U16` when exhausted. |
| 0xB5 | `IteratorClose` | - | - | iterator slot | - | - | - | - | - | Disposes/closes a VM-internal iterator. |
| 0xB6 | `CollectionBuilderList` | - | builder slot | - | - | - | - | - | - | Creates a VM-internal list builder. |
| 0xB7 | reserved | - | - | - | - | - | - | - | - | Removed set builder slot. |
| 0xB8 | `CollectionBuilderAdd` | - | - | builder slot | item slot | - | - | - | - | Adds an item and checks `MaxGeneratedCollectionItems`. |
| 0xB9 | `CollectionBuilderFinish` | - | result slot | builder slot | - | - | - | - | - | Materializes the builder as a list. |
| 0xBA | `IteratorReduce` | - | accumulator/result slot | iterator slot | item binding slot | reducer entry address | - | - | - | Empty -> `nothing`; one item -> item; otherwise reducer combines accumulator and item. |
| 0xBB | `IteratorReduceOrDefault` | - | accumulator/result slot | iterator slot | default slot | item binding slot | reducer entry address | - | - | Empty -> default; one item -> item; otherwise reducer combines accumulator and item. |
| 0xBC | `IteratorFold` | - | accumulator/result slot | iterator slot | seed slot | item binding slot | reducer entry address | - | - | Starts with seed and runs reducer for every item. |
| 0xBD | `SeriesTerm` | - | result slot | series slot | index slot | - | - | - | - | Reads a series term. |
| 0xBE | `SeriesTake` | - | result slot | source slot | count immediate | - | - | - | - | Takes the first `B_U16` values from a series or list-like source. |
| 0xBF | `SeriesDrop` | - | result slot | source slot | count immediate | - | - | - | - | Drops the first `B_U16` values from a series or list-like source. |
| 0xC0 | `Call` | - | result slot | callable entry address | - | - | - | - | - | Enters a VM-owned local call frame at a known code address. Arguments are the contiguous staged sequence immediately before the call. |
| 0xC1 | `CallPredicate` | - | result slot | predicate entry address | - | - | - | - | - | Enters a VM-owned predicate call frame and normalizes the result to `boolean | nothing`. Arguments are the contiguous staged sequence immediately before the call. |
| 0xC2 | `CallStandard` | - | result slot | extension shape `UShortListPool` index | argument slot-list `UShortListPool` index | - | - | - | - | Calls a built-in standard extension. Shape is `[extensionNameStringIndex, functionNameStringIndex, argumentNameStringIndex...]`. |
| 0xC3 | `CallStandardPredicate` | - | result slot | extension shape `UShortListPool` index | argument slot-list `UShortListPool` index | - | - | - | - | Calls a built-in standard extension and normalizes the result to `boolean | nothing`. |
| 0xC4 | `CallExternal` | - | result slot | `ExternalReferences` index | argument slot-list `UShortListPool` index | - | - | - | - | Calls a dynamically bound host extension. |
| 0xC5 | `CallExternalPredicate` | - | result slot | `ExternalReferences` index | argument slot-list `UShortListPool` index | - | - | - | - | Calls a dynamically bound host extension and normalizes the result to `boolean | nothing`. |
| 0xC6 | `StageRegister` | - | - | source slot | - | - | - | - | - | Stages a register value as the next local call argument. |
| 0xC7 | `StageNothing` | - | - | - | - | - | - | - | - | Stages DSL `nothing` as the next local call argument. |
| 0xC8 | `StageTrue` | - | - | - | - | - | - | - | - | Stages `true` as the next local call argument. |
| 0xC9 | `StageFalse` | - | - | - | - | - | - | - | - | Stages `false` as the next local call argument. |
| 0xCA | `StageInteger` | unit/flags | - | n/a | n/a | n/a | n/a | integer payload | n/a | Stages an inline integer argument. |
| 0xCB | `StageFloat` | numeric unit | - | n/a | n/a | n/a | n/a | n/a | float payload | Stages an inline float argument. |
| 0xCC | `StageText` | - | - | - | - | string index | - | - | - | Stages a text literal from `StringPool`. |
| 0xCD | `StageTag` | - | - | - | - | string index | - | - | - | Stages a tag literal from `StringPool`. |
| 0xCE | `StagePercentage` | - | - | n/a | n/a | n/a | n/a | n/a | ratio | Stages an inline percentage ratio argument. |
| 0xCF | reserved | - | - | - | - | - | - | - | - | Reserved for future call opcodes. |
| 0xD0 | `PipelineIterator` | - | iterator slot | source iterator slot | next-entry address | helper item slot | capture slot-list index | - | - | Creates a lazy one-time adapter. `ReturnValue` yields; `ReturnVoid` skips/exhausts. |
| 0xD1 | `PipelineCollectList` | - | result slot | iterator slot | - | - | - | - | - | Materializes an iterator as a list. |
| 0xD2 | reserved | - | - | - | - | - | - | - | - | Removed pipeline set materializer slot. |
| 0xD3 | `PipelineFirst` | - | result slot | iterator slot | - | - | - | - | - | Returns the first element or `nothing`. |
| 0xD4 | `PipelineLast` | - | result slot | iterator slot | - | - | - | - | - | Returns the last element or `nothing`. |
| 0xD5 | `PipelineSingle` | - | result slot | iterator slot | - | - | - | - | - | Returns the only element or `nothing`. |
| 0xD6 | `PipelineHasAny` | - | result slot | iterator slot | - | - | - | - | - | Tri-state `any` over projected predicate values. |
| 0xD7 | `PipelineHasAll` | - | result slot | iterator slot | - | - | - | - | - | Tri-state `all` over projected predicate values. |
| 0xD8 | `PipelineContainsSingle` | - | result slot | iterator slot | needle slot | - | - | - | - | Tests whether the pipeline target contains one value. |
| 0xD9 | `PipelineContainsAny` | - | result slot | iterator slot | needle slot | - | - | - | - | Tests whether the pipeline target contains any values from the needle collection. |
| 0xDA | `PipelineContainsAll` | - | result slot | iterator slot | needle slot | - | - | - | - | Tests whether the pipeline target contains all values from the needle collection. |
| 0xDB | `PipelineMap` | - | result slot | iterator slot | item binding slot | key entry address | - | - | - | Builds a map with each source item as the value. |
| 0xDC | `PipelineMapValue` | - | result slot | iterator slot | item binding slot | key entry address | value entry address | - | - | Builds a map from key and value helper entries. |
| 0xDD | `PipelineDistinct` | - | result slot | iterator slot | - | - | - | - | - | Materializes distinct source items in source order. |
| 0xDE | `PipelineDistinctBy` | - | result slot | iterator slot | item binding slot | projection entry address | - | - | - | Materializes source items distinct by projected key. |
| 0xDF | `PipelineGroupBy` | - | result slot | iterator slot | item binding slot | key entry address | - | - | - | Groups source items by projected key. |
| 0xE0 | `PipelineReverse` | - | result slot | iterator slot | - | - | - | - | - | Materializes source items in reverse order. |
| 0xE1 | `PipelineSortAscending` | - | result slot | iterator slot | - | - | - | - | - | Sorts source items ascending. |
| 0xE2 | `PipelineSortDescending` | - | result slot | iterator slot | - | - | - | - | - | Sorts source items descending. |
| 0xE3 | `PipelineOrderByAscending` | - | result slot | iterator slot | item binding slot | key entry address | - | - | - | Orders source items by projected key ascending. |
| 0xE4 | `PipelineOrderByDescending` | - | result slot | iterator slot | item binding slot | key entry address | - | - | - | Orders source items by projected key descending. |
| 0xE5 | `PipelineTakeFirst` | - | result slot | iterator slot | count immediate | - | - | - | - | Takes the first `B_U16` items. |
| 0xE6 | `PipelineTakeLast` | - | result slot | iterator slot | count immediate | - | - | - | - | Takes the last `B_U16` items. |
| 0xE7 | `PipelineTakeHighest` | - | result slot | iterator slot | count immediate | - | - | - | - | Takes the highest `B_U16` items. |
| 0xE8 | `PipelineTakeLowest` | - | result slot | iterator slot | count immediate | - | - | - | - | Takes the lowest `B_U16` items. |
| 0xE9 | `PipelineDropFirst` | - | result slot | iterator slot | count immediate | - | - | - | - | Drops the first `B_U16` items. |
| 0xEA | `PipelineDropLast` | - | result slot | iterator slot | count immediate | - | - | - | - | Drops the last `B_U16` items. |
| 0xEB | `PipelineDropHighest` | - | result slot | iterator slot | count immediate | - | - | - | - | Drops the highest `B_U16` items. |
| 0xEC | `PipelineDropLowest` | - | result slot | iterator slot | count immediate | - | - | - | - | Drops the lowest `B_U16` items. |
| 0xED | `PipelineShuffle` | - | result slot | iterator slot | - | - | - | - | - | Shuffles source items. |
| 0xEE | `PipelineDraw` | - | result slot | iterator slot | count immediate | - | - | - | - | Draws `B_U16` source items. |
| 0xEF | `PipelineChoose` | - | result slot | iterator slot | count immediate | - | - | - | - | Chooses up to `B_U16` source items. |
| 0xF0 | `PipelineChooseRandom` | - | result slot | iterator slot | count immediate | - | - | - | - | Randomly chooses up to `B_U16` source items. |
| 0xF1 | `PipelineChooseWeighted` | - | result slot | iterator slot | count immediate | item binding slot | weight entry address | - | - | Randomly chooses using projected positive weights. |
| 0xF2 | `PipelineDicePatternCountAny` | - | result slot | iterator slot | count immediate | - | - | - | - | Tests whether any dice face count reaches `B_U16`. |
| 0xF3 | `PipelineDicePatternCountFace` | - | result slot | iterator slot | count immediate | face entry address | - | - | - | Tests whether a projected face count reaches `B_U16`. |
| 0xF4 | `PipelineDicePatternFullHouse` | - | result slot | iterator slot | - | - | - | - | - | Tests the full-house dice pattern. |
| 0xF5 | `PipelineDicePatternStraight` | - | result slot | iterator slot | - | - | - | - | - | Tests the straight dice pattern. |
| 0xF6 | `PipelineTakePatternCountAny` | - | result slot | iterator slot | count immediate | - | - | - | - | Takes dice matching any-face count pattern. |
| 0xF7 | `PipelineTakePatternCountFace` | - | result slot | iterator slot | count immediate | face entry address | - | - | - | Takes dice matching projected-face count pattern. |
| 0xF8 | `PipelineTakePatternFullHouse` | - | result slot | iterator slot | - | - | - | - | - | Takes dice matching full-house pattern. |
| 0xF9 | `PipelineTakePatternStraight` | - | result slot | iterator slot | - | - | - | - | - | Takes dice matching straight pattern. |
| 0xFA..0xFF | reserved | - | - | - | - | - | - | - | - | Reserved for future pipeline or extension opcodes. |

## Side-Table Summary

| Pool / table | Used by |
| --- | --- |
| `StringPool` | `LoadText`, `LoadTag`, `MemberAccess`; indirectly through message/name lists in `UShortListPool` |
| `UShortListPool` | `LoadHandler`, `EmitMessage*`, `PublishMessage*`, `TypeConstructor`, `Build*`, `BindHandler`, `CallStandard*`, `CallExternal*` |
| `OutboundMessageSignatures` / binary `OutboundMessage` binds | Statically shaped `emit`/`publish` message signatures, used by loaders without scanning code |

Local calls, predicate calls, construction, extension calls, and collection/message
builder opcodes now reference entry addresses or `StringPool`/`UShortListPool`
directly from the instruction word.
Pipeline selectors are lowered into linear helper entries and fixed iterator or
terminal opcodes; there are no pipeline selector or pattern pools.
