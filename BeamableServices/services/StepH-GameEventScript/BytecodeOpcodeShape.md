# Bytecode Opcode Instruction Shape

This document lists the current `GameEventScriptBytecodeOpCode` values and how
each opcode uses the compact linear instruction shape.

## Instruction Word

`GameEventScriptBytecodeInstruction` is currently a fixed 16-byte explicit-layout
runtime word. The first 8 bytes form the primary instruction word. The second
8 bytes form an aligned payload word that can be read through different typed
views. This is intentional: simple VM instructions read only the primary
operands, while literal loads and wider opcodes read the payload word directly.

```csharp
[StructLayout(LayoutKind.Explicit, Size = 16)]
public struct GameEventScriptBytecodeInstruction
{
    [FieldOffset(0)]  public GameEventScriptBytecodeOpCode OpCode; // byte-backed
    [FieldOffset(1)]  public byte UnitAndFlags;

    [FieldOffset(2)]  public ushort DestinationSlot;
    [FieldOffset(4)]  public ushort X_U16;
    [FieldOffset(6)]  public ushort Y_U16;

    [FieldOffset(4)]  public short ImmediateX;
    [FieldOffset(6)]  public short ImmediateY;
    [FieldOffset(4)]  public short Count;

    [FieldOffset(8)]  public ushort AU;
    [FieldOffset(10)] public ushort BU;
    [FieldOffset(12)] public ushort CU;
    [FieldOffset(14)] public ushort DU;

    [FieldOffset(8)]  public short AS;
    [FieldOffset(10)] public short BS;
    [FieldOffset(12)] public short CS;
    [FieldOffset(14)] public short DS;

    [FieldOffset(8)]  public long I64;
    [FieldOffset(8)]  public ulong U64;
    [FieldOffset(8)]  public double F64;
}
```

The byte layout is:

| Byte range | Primary/payload view | 64-bit payload view | Typical use |
| --- | --- | --- | --- |
| `0` | `OpCode` | `OpCode` | Opcode tag, backed by `byte`. |
| `1` | `UnitAndFlags` | `UnitAndFlags` | Low 5 bits carry the numeric unit id; high 3 bits are reserved flags. |
| `2..3` | `DestinationSlot` | `DestinationSlot` | Destination/result slot or primary operation destination. |
| `4..5` | `X_U16` / `ImmediateX` / `Count` | `X_U16` | First primary operand, or signed local slot delta for `SlotLocals`. |
| `6..7` | `Y_U16` / `ImmediateY` | `Y_U16` | Second primary operand, often an entry/target address or secondary slot. |
| `8..9` | `AU` / `AS` | `I64`/`U64`/`F64` bytes `0..1` | Payload word bytes `0..1`, or first payload 16-bit operand. |
| `10..11` | `BU` / `BS` | `I64`/`U64`/`F64` bytes `2..3` | Payload word bytes `2..3`, or second payload 16-bit operand. |
| `12..13` | `CU` / `CS` | `I64`/`U64`/`F64` bytes `4..5` | Payload word bytes `4..5`, or third payload 16-bit operand. |
| `14..15` | `DU` / `DS` | `I64`/`U64`/`F64` bytes `6..7` | Payload word bytes `6..7`, or fourth payload 16-bit operand. |

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
  "X": "0x0000",
  "Y": "0x0000",
  "Parameter": "0x000000000000002A"
}
```

- `Opcode` is the enum name. Numeric byte values and hex byte strings are also
  accepted while reading.
- `Flags` is the raw `UnitAndFlags` byte as hex.
- `Dst` is the raw `DestinationSlot` slot as hex.
- `X` and `Y` are the raw primary 16-bit operands as hex.
- `Parameter` is the raw unsigned 64-bit payload word at bytes `8..15` as hex.
  Signed payload views use the same bits as two's-complement values.
  `LoadFloat` stores the IEEE-754 double bit pattern in the same payload.

## Operand Views

- **Primary 16-bit operand view:** most instructions use `DestinationSlot`, `X`,
  and `Y`. These fields are stored as `DestinationSlot`, `X_U16`/`ImmediateX`,
  and `Y_U16`/`ImmediateY`, with semantic aliases such as `XSlot`, `ConditionSlot`,
  `TargetAddress`, `EntryAddress`, `StringIndex`, `Count`, and `ImmediateY`.
- **Primary signed 16-bit operand view:** `ImmediateX` and `ImmediateY` are available for
  compact signed immediates in the primary word.
- **Payload 16-bit operand view:** wider instructions use the aligned payload
  word as `AU..DU` or `AS..DS`. The opcode table documents this compactly as
  `AU=...`, `BU=...`, `AS=...`, `BS=...`, and so on.
- **64-bit integer view:** `LoadInteger` reads `I64` directly. Negative integer
  values are stored as their normal two's-complement bit pattern.
- **64-bit float view:** `LoadFloat` reads `F64` directly. This keeps IEEE-754
  `NaN`, `Infinity`, and `-Infinity` portable as raw double bits.
- **Unsigned payload view:** `U64` exposes the raw payload bits for transport
  and diagnostics. Portable opcodes that use signed numeric payloads read `I64`
  instead.
- **32-bit views:** no portable bytecode shape currently depends on a 32-bit
  immediate view.

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

- `DestinationSlot` is the destination frame slot for value-producing instructions.
- `X` and `Y` in the opcode table are primary-word 16-bit fields. Cells use
  semantic aliases where one is established, for example `ConditionSlot`,
  `TargetAddress`, `EntryAddress`, `StringIndex`, `Count`, or `ImmediateY`.
- Unused instruction fields are intentionally not specified.
- Branch opcodes use primary-word fields for target/condition operands as
  documented per opcode.
- `LoadInteger` uses the overlapped `I64` payload and may use `UnitAndFlags`
  for numeric units.
- `LoadFloat` uses the overlapped `F64` payload. `UnitAndFlags` carries `None`
  or a numeric unit. `LoadPercentage` uses the same `F64` payload with no unit
  flags and creates the dedicated percentage value kind.
- Pool-backed opcodes use the documented `StringPool` or `UShortListPool`
  indices directly in `X`, `Y`, `DestinationSlot`, or the payload word.

## Opcode Table

Opcode values are split into aligned operation-family blocks. Every family starts
at a `0x_0` boundary; larger families may span multiple 16-value pages. The VM
dispatches on the complete byte value; the high nibble is a format convention,
not a second runtime dispatch step.

| Group | Purpose |
| --- | --- |
| `0x00` | Group 1: control flow, frame slots, local calls, handler binding, access, emit/publish |
| `0x10` | Group 1 continuation: move, casts, checks, member/index access, message operations |
| `0x20` | Group 2: loads, argument staging, type construction |
| `0x30` | Group 2 continuation and reserved tail |
| `0x40` | Group 3: boolean, comparison, implication, presence checks |
| `0x50` | Group 3 integer comparison fast-path tail and reserved space |
| `0x60` | Group 4: math, integer arithmetic fast paths, random |
| `0x70` | Group 4 continuation and reserved tail |
| `0x80` | Group 5: text/collection operators, range/iterator setup |
| `0x90` | Group 5 iterator next/close/reduce/fold, series, `PipelineIterator` |
| `0xA0` | Group 6: collection and value building |
| `0xB0` | Group 7: pipeline materializers, transforms, membership terminals |
| `0xC0` | Group 7 pipeline ordering, slicing, random terminals |
| `0xD0` | Group 7 pipeline dice/pattern terminals |
| `0xE0` | Reserved for future pipeline, extension, or VM opcodes |
| `0xF0` | Reserved for future pipeline, extension, or VM opcodes |

| Hex | Opcode | UnitAndFlags | DestinationSlot | X | Y | Payload | Notes |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 0x00 | `Nop` | - | - | - | - | - | No operation. |
| 0x01 | `Jump` | - | - | - | `TargetAddress` | - | Unconditional branch. |
| 0x02 | `JumpIfTrue` | - | - | `ConditionSlot` | `TargetAddress` | - | Branches when `X.IsTrue()`. |
| 0x03 | `JumpIfFalse` | - | - | `ConditionSlot` | `TargetAddress` | - | Branches when `X.IsFalse()`. |
| 0x04 | `JumpIfNotTrue` | - | - | `ConditionSlot` | `TargetAddress` | - | Branches when `!X.IsTrue()`, including `nothing`. |
| 0x05 | `SlotLocals` | - | - | `Count` | - | - | Adds `Count > 0` active local slots or releases `-Count` slots when `Count < 0`. Entry prologs reserve only locals beyond preloaded arguments. |
| 0x06 | `ReturnVoid` | - | - | - | - | - | Returns no value from the current frame; normal calls map this to DSL `nothing`. |
| 0x07 | `ReturnValue` | - | - | `XSlot`=return | - | - | Returns the value in `X` from the current frame. |
| 0x08 | `Call` | - | result slot | - | `EntryAddress`=callable | - | Enters a VM-owned local call frame at a known code address. Arguments are the contiguous staged sequence immediately before the call. |
| 0x09 | `CallPredicate` | - | result slot | - | `EntryAddress`=predicate | - | Enters a VM-owned predicate call frame and normalizes the result to boolean or `nothing`. Arguments are the contiguous staged sequence immediately before the call. |
| 0x0A | `CallStandard` | - | result slot | extension shape `UShortListPool` index | argument slot-list `UShortListPool` index | - | Calls a built-in standard extension. Shape is `[extensionNameStringIndex, functionNameStringIndex, argumentNameStringIndex...]`. |
| 0x0B | `CallStandardPredicate` | - | result slot | extension shape `UShortListPool` index | argument slot-list `UShortListPool` index | - | Calls a built-in standard extension and normalizes the result to boolean or `nothing`. |
| 0x0C | `CallExternal` | - | result slot | `ExternalReferences` index | argument slot-list `UShortListPool` index | - | Calls a dynamically bound host extension. |
| 0x0D | `CallExternalPredicate` | - | result slot | `ExternalReferences` index | argument slot-list `UShortListPool` index | - | Calls a dynamically bound host extension and normalizes the result to boolean or `nothing`. |
| 0x0E | `BindHandler` | - | result slot | operand slot-list `UShortListPool` index | argument name-list `UShortListPool` index | - | Binds a handler value plus named arguments. Operand slot-list starts with the handler slot. |
| 0x0F | `MoveSlot` | - | result slot | `XSlot`=source | - | - | Copies a slot value/reference; the source slot remains unchanged. |
| 0x10 | `Cast` | - | result slot | `XSlot`=source | `GameEventScriptBytecodeTypeKind` | - | Converts `X` to the declared built-in type. Custom/record types use `CastCustom`. |
| 0x11 | `CastCustom` | - | result slot | `XSlot`=source | custom type `StringPool` index | - | Converts `X` to a custom/record type identified by `Y`. |
| 0x12 | `CastUnit` | target numeric unit | result slot | `XSlot`=source | - | - | Converts `X` to the numeric unit carried in `UnitAndFlags`. Units are not represented as declared type kinds. |
| 0x13 | `TypeCheck` | - | result slot | `XSlot`=source | `GameEventScriptBytecodeTypeKind` | - | Writes whether `X` has the declared built-in type. Custom/record types use `TypeCheckCustom`. |
| 0x14 | `TypeCheckCustom` | - | result slot | `XSlot`=source | custom type `StringPool` index | - | Writes whether `X` has the custom/record type identified by `Y`. |
| 0x15 | `CheckUnit` | target numeric unit | result slot | `XSlot`=source | - | - | Writes whether `X` has the numeric unit carried in `UnitAndFlags`. Units are not represented as declared type kinds. |
| 0x16 | `MemberAccess` | - | result slot | `StringIndex`=member name | `YSlot`=object | - | Reads a named member. |
| 0x17 | `IndexedAccess` | - | result slot | `XSlot`=index | `YSlot`=object | - | Direct indexed lookup. |
| 0x18 | `EmitMessage` | - | `MessageDestination`=message shape index | - | `ListIndex`=argument slot-list index | - | Emits a statically shaped message without tags. |
| 0x19 | `EmitMessageWithTags` | - | `MessageDestination`=message shape index | `SecondaryListIndex`=tag slot-list index | `ListIndex`=argument slot-list index | - | Emits a statically shaped message with tags. |
| 0x1A | `PublishMessage` | - | `MessageDestination`=message shape index | - | `ListIndex`=argument slot-list index | - | Publishes a statically shaped message without tags. |
| 0x1B | `PublishMessageWithTags` | - | `MessageDestination`=message shape index | `SecondaryListIndex`=tag slot-list index | `ListIndex`=argument slot-list index | - | Publishes a statically shaped message with tags. |
| 0x1C | `EmitMessageValue` | - | - | `XSlot`=message | - | - | Emits a dynamic message value without tags. |
| 0x1D | `EmitMessageValueWithTags` | - | - | `XSlot`=message | - | AU=tag slot-list `UShortListPool` index | Emits a dynamic message value with tags. |
| 0x1E | `PublishMessageValue` | - | - | `XSlot`=message | - | - | Publishes a dynamic message value without tags. |
| 0x1F | `PublishMessageValueWithTags` | - | - | `XSlot`=message | - | AU=tag slot-list `UShortListPool` index | Publishes a dynamic message value with tags. |
| 0x20 | `LoadNothing` | - | result slot | - | - | - | Loads `nothing`. |
| 0x21 | `LoadTrue` | - | result slot | - | - | - | Loads boolean `true`. |
| 0x22 | `LoadFalse` | - | result slot | - | - | - | Loads boolean `false`. |
| 0x23 | `LoadInteger` | numeric unit | result slot | - | - | I64=signed integer | Loads an inline signed `Int64`. |
| 0x24 | `LoadFloat` | numeric unit | result slot | - | - | F64=float | Loads an inline IEEE-754 `Float64`. |
| 0x25 | `LoadPercentage` | - | result slot | - | - | F64=ratio | Loads an inline percentage ratio as the dedicated percentage value kind. |
| 0x26 | `LoadText` | - | result slot | `StringIndex` | - | - | Loads a text literal. |
| 0x27 | `LoadTag` | - | result slot | `StringIndex` | - | - | Loads a tag literal. |
| 0x28 | `LoadHandler` | - | result slot | message shape `UShortListPool` index | - | - | Loads a handler literal. The shape list is `[messageNameStringIndex, argumentNameStringIndex...]`. |
| 0x29 | `StageRegister` | - | - | `XSlot`=source | - | - | Stages a register value as the next local call argument. |
| 0x2A | `StageNothing` | - | - | - | - | - | Stages DSL `nothing` as the next local call argument. |
| 0x2B | `StageTrue` | - | - | - | - | - | Stages `true` as the next local call argument. |
| 0x2C | `StageFalse` | - | - | - | - | - | Stages `false` as the next local call argument. |
| 0x2D | `StageInteger` | numeric unit | - | - | - | I64=integer payload | Stages an inline integer argument. |
| 0x2E | `StageFloat` | numeric unit | - | - | - | F64=float payload | Stages an inline float argument. |
| 0x2F | `StageText` | - | - | `StringIndex` | - | - | Stages a text literal from `StringPool`. |
| 0x30 | `StageTag` | - | - | `StringIndex` | - | - | Stages a tag literal from `StringPool`. |
| 0x31 | `StagePercentage` | - | - | - | - | F64=ratio | Stages an inline percentage ratio argument. |
| 0x32 | `TypeConstructor` | - | result slot | `StringIndex`=type name | argument name-list `UShortListPool` index | AU=argument slot-list `UShortListPool` index | Constructs a record/external value from named argument slots. |
| 0x33..0x3F | reserved | - | - | - | - | - | Reserved tail of Group 2. |
| 0x40 | `Or` | - | result slot | `XSlot`=left | `YSlot`=right | - | Tri-state boolean combine. |
| 0x41 | `And` | - | result slot | `XSlot`=left | `YSlot`=right | - | Tri-state boolean combine. |
| 0x42 | `Xor` | - | result slot | `XSlot`=left | `YSlot`=right | - | Tri-state boolean combine. |
| 0x43 | `Implies` | - | result slot | `XSlot`=antecedent | `YSlot`=consequent | - | Binary implication combine. |
| 0x44 | `UnaryNot` | - | result slot | `XSlot`=operand | - | - | Logical negation. |
| 0x45 | `UnaryHasValue` | - | result slot | `XSlot`=operand | - | - | Semantic value check. |
| 0x46 | `UnaryEmpty` | - | result slot | `XSlot`=operand | - | - | Semantic emptiness check. |
| 0x47 | `Equal` | - | result slot | `XSlot`=left | `YSlot`=right | - | Binary comparison. |
| 0x48 | `NotEqual` | - | result slot | `XSlot`=left | `YSlot`=right | - | Binary comparison. |
| 0x49 | `ApproxEqual` | - | result slot | `XSlot`=left | `YSlot`=right | - | Approximate equality. |
| 0x4A | `Less` | - | result slot | `XSlot`=left | `YSlot`=right | - | Binary comparison. |
| 0x4B | `Greater` | - | result slot | `XSlot`=left | `YSlot`=right | - | Binary comparison. |
| 0x4C | `LessOrEqual` | - | result slot | `XSlot`=left | `YSlot`=right | - | Binary comparison. |
| 0x4D | `GreaterOrEqual` | - | result slot | `XSlot`=left | `YSlot`=right | - | Binary comparison. |
| 0x4E | `IntEqual` | - | result slot | `XSlot`=left | `YSlot`=right | - | Integer fast-path equality comparison. |
| 0x4F | `IntNotEqual` | - | result slot | `XSlot`=left | `YSlot`=right | - | Integer fast-path inequality comparison. |
| 0x50 | `IntLess` | - | result slot | `XSlot`=left | `YSlot`=right | - | Integer fast-path less-than comparison. |
| 0x51 | `IntGreater` | - | result slot | `XSlot`=left | `YSlot`=right | - | Integer fast-path greater-than comparison. |
| 0x52 | `IntLessOrEqual` | - | result slot | `XSlot`=left | `YSlot`=right | - | Integer fast-path less-or-equal comparison. |
| 0x53 | `IntGreaterOrEqual` | - | result slot | `XSlot`=left | `YSlot`=right | - | Integer fast-path greater-or-equal comparison. |
| 0x54 | `Default` | - | result slot | `XSlot`=left | `YSlot`=right | - | Presence/default operator. |
| 0x55..0x5F | reserved | - | - | - | - | - | Reserved tail of Group 3. |
| 0x60 | `Add` | - | result slot | `XSlot`=left | `YSlot`=right | - | Binary numeric operation. |
| 0x61 | `Subtract` | - | result slot | `XSlot`=left | `YSlot`=right | - | Binary numeric operation. |
| 0x62 | `Multiply` | - | result slot | `XSlot`=left | `YSlot`=right | - | Binary numeric operation. |
| 0x63 | `Divide` | - | result slot | `XSlot`=left | `YSlot`=right | - | Binary numeric operation. |
| 0x64 | `Power` | - | result slot | `XSlot`=left | `YSlot`=right | - | Binary numeric operation. |
| 0x65 | `IntegerDivide` | - | result slot | `XSlot`=left | `YSlot`=right | - | Floor-like integer division operation. |
| 0x66 | `Modulo` | - | result slot | `XSlot`=left | `YSlot`=right | - | Numeric modulo operation. |
| 0x67 | `Remainder` | - | result slot | `XSlot`=left | `YSlot`=right | - | Numeric remainder operation. |
| 0x68 | `IntAdd` | - | result slot | `XSlot`=left | `YSlot`=right | - | Integer fast-path addition. |
| 0x69 | `IntSubtract` | - | result slot | `XSlot`=left | `YSlot`=right | - | Integer fast-path subtraction. |
| 0x6A | `IntMultiply` | - | result slot | `XSlot`=left | `YSlot`=right | - | Integer fast-path multiplication. |
| 0x6B | `IntDivide` | - | result slot | `XSlot`=left | `YSlot`=right | - | Integer fast-path division. |
| 0x6C | `IntFloorDivide` | - | result slot | `XSlot`=left | `YSlot`=right | - | Integer fast-path floor division. |
| 0x6D | `IntModulo` | - | result slot | `XSlot`=left | `YSlot`=right | - | Integer fast-path modulo. |
| 0x6E | `IntRemainder` | - | result slot | `XSlot`=left | `YSlot`=right | - | Integer fast-path remainder. |
| 0x6F | `Min` | - | result slot | `XSlot`=left | `YSlot`=right | - | Binary extrema reduce step. |
| 0x70 | `Max` | - | result slot | `XSlot`=left | `YSlot`=right | - | Binary extrema reduce step. |
| 0x71 | `UnaryNegate` | - | result slot | `XSlot`=operand | - | - | Numeric negation. |
| 0x72 | `UnaryAbs` | - | result slot | `XSlot`=operand | - | - | Absolute value. |
| 0x73 | `UnaryNaturalLog` | - | result slot | `XSlot`=operand | - | - | Natural logarithm. |
| 0x74 | `UnaryChance` | - | result slot | `XSlot`=operand | - | - | Chance evaluation. |
| 0x75 | `Clamp` | - | result slot | `XSlot`=value | `YSlot`=minimum | AU=maximum slot | The only opcode with three direct source slots. |
| 0x76 | `Random` | - | result slot | `XSlot`=from | `YSlot`=to | - | Uses current random scope. |
| 0x77 | `RandomPush` | - | - | `XSlot`=seed | - | - | Pushes a nested random scope from a dynamic unitless integer seed slot. |
| 0x78 | `RandomPushConstant` | - | - | - | - | I64=signed seed | Pushes a nested random scope from inline signed `Int64`. |
| 0x79 | `RandomPop` | - | - | - | - | - | Restores the previous random scope. |
| 0x7A..0x7F | reserved | - | - | - | - | - | Reserved tail of Group 4. |
| 0x80 | `UnaryLength` | - | result slot | `XSlot`=operand | - | - | Length operation. |
| 0x81 | `StartsWith` | - | result slot | `XSlot`=left | `YSlot`=right | - | Text operation. |
| 0x82 | `EndsWith` | - | result slot | `XSlot`=left | `YSlot`=right | - | Text operation. |
| 0x83 | `Contains` | - | result slot | `XSlot`=left | `YSlot`=right | - | Collection/text membership operation. |
| 0x84 | `ContainsValue` | - | result slot | `XSlot`=left | `YSlot`=right | - | Collection value membership operation. |
| 0x85 | `Intersect` | - | result slot | `XSlot`=left | `YSlot`=right | - | Collection operation. |
| 0x86 | `Combine` | - | result slot | `XSlot`=left | `YSlot`=right | - | Collection operation. |
| 0x87 | `Except` | - | result slot | `XSlot`=left | `YSlot`=right | - | Collection operation. |
| 0x88 | `Zip` | - | result slot | `XSlot`=left | `YSlot`=right | - | Collection operation. |
| 0x89 | `UnaryKeys` | - | result slot | `XSlot`=operand | - | - | Map/record keys projection. |
| 0x8A | `UnaryValues` | - | result slot | `XSlot`=operand | - | - | Map/record values projection. |
| 0x8B | `UnaryEntries` | - | result slot | `XSlot`=operand | - | - | Map/record entries projection. |
| 0x8C | `Range` | - | result slot | `XSlot`=from | `YSlot`=to | - | Builds a range with implicit step `1`. |
| 0x8D | `RangeWithStep` | - | result slot | `XSlot`=from | `YSlot`=to | AU=step slot | Builds a range with explicit step. |
| 0x8E | `RangeIterator` | - | iterator slot | `XSlot`=from | `YSlot`=to | - | Creates a VM-internal range iterator with default step `+1`. |
| 0x8F | `RangeIteratorWithStep` | - | iterator slot | `XSlot`=from | `YSlot`=to | AU=step slot | Creates a VM-internal range iterator with an explicit step. |
| 0x90 | `RangeIteratorShort` | - | iterator slot | `ImmediateX`=from | `ImmediateY`=to | AS=step | Creates a compact literal range iterator. |
| 0x91 | `CollectionIterator` | - | iterator slot | `XSlot`=collection | - | - | Creates a VM-internal iterator over a collection or range value. |
| 0x92 | `IteratorNext` | - | item slot | `XSlot`=iterator | `TargetAddress`=no-more | - | Writes the next item and continues, or jumps to `Y` when exhausted. |
| 0x93 | `IteratorClose` | - | - | `XSlot`=iterator | - | - | Disposes/closes a VM-internal iterator. |
| 0x94 | `IteratorReduce` | - | accumulator/result slot | `XSlot`=iterator | `YSlot`=item binding | AU=reducer entry address | Empty -> `nothing`; one item -> item; otherwise reducer combines accumulator and item. |
| 0x95 | `IteratorReduceOrDefault` | - | accumulator/result slot | `XSlot`=iterator | `YSlot`=default | AU=item binding slot, BU=reducer entry address | Empty -> default; one item -> item; otherwise reducer combines accumulator and item. |
| 0x96 | `IteratorFold` | - | accumulator/result slot | `XSlot`=iterator | `YSlot`=seed | AU=item binding slot, BU=reducer entry address | Starts with seed and runs reducer for every item. |
| 0x97 | `SeriesTerm` | - | result slot | `XSlot`=series | `YSlot`=index | - | Reads a series term. |
| 0x98 | `SeriesTake` | - | result slot | `XSlot`=source | `ImmediateY`=count | - | Takes the first `Y` values from a series or list-like source. |
| 0x99 | `SeriesDrop` | - | result slot | `XSlot`=source | `ImmediateY`=count | - | Drops the first `Y` values from a series or list-like source. |
| 0x9A | `PipelineIterator` | - | iterator slot | `XSlot`=source iterator | `EntryAddress`=next | AU=helper item slot, BU=capture slot-list index | Creates a lazy one-time adapter. `ReturnValue` yields; `ReturnVoid` skips/exhausts. |
| 0x9B..0x9F | reserved | - | - | - | - | - | Reserved tail of Group 5. |
| 0xA0 | `BuildList` | - | result slot | item slot-list `UShortListPool` index | - | - | Builds a list from slot-list operands. |
| 0xA1 | `BuildMap` | - | result slot | key name-list `UShortListPool` index | value slot-list `UShortListPool` index | - | Builds a map from key names and value slots. |
| 0xA2 | `BuildMessage` | - | result slot | message shape `UShortListPool` index | argument slot-list `UShortListPool` index | - | Builds a message value. Shape is `[messageNameStringIndex, argumentNameStringIndex...]`. |
| 0xA3 | `CollectionBuilderList` | - | builder slot | - | - | - | Creates a VM-internal list builder. |
| 0xA4 | `CollectionBuilderAdd` | - | - | `XSlot`=builder | `YSlot`=item | - | Adds an item and checks `MaxGeneratedCollectionItems`. |
| 0xA5 | `CollectionBuilderFinish` | - | result slot | `XSlot`=builder | - | - | Materializes the builder as a list. |
| 0xA6 | `Dice` | - | result slot | `Count`=dice count | `ImmediateY`=side count | - | `X` and `Y` are not slots. |
| 0xA7..0xAF | reserved | - | - | - | - | - | Reserved tail of Group 6. |
| 0xB0 | `PipelineCollectList` | - | result slot | `XSlot`=iterator | - | - | Materializes an iterator as a list. |
| 0xB1 | `PipelineFirst` | - | result slot | `XSlot`=iterator | - | - | Returns the first element or `nothing`. |
| 0xB2 | `PipelineLast` | - | result slot | `XSlot`=iterator | - | - | Returns the last element or `nothing`. |
| 0xB3 | `PipelineSingle` | - | result slot | `XSlot`=iterator | - | - | Returns the only element or `nothing`. |
| 0xB4 | `PipelineHasAny` | - | result slot | `XSlot`=iterator | - | - | Tri-state `any` over projected predicate values. |
| 0xB5 | `PipelineHasAll` | - | result slot | `XSlot`=iterator | - | - | Tri-state `all` over projected predicate values. |
| 0xB6 | `PipelineContainsSingle` | - | result slot | `XSlot`=iterator | `YSlot`=needle | - | Tests whether the pipeline target contains one value. |
| 0xB7 | `PipelineContainsAny` | - | result slot | `XSlot`=iterator | `YSlot`=needle | - | Tests whether the pipeline target contains any values from the needle collection. |
| 0xB8 | `PipelineContainsAll` | - | result slot | `XSlot`=iterator | `YSlot`=needle | - | Tests whether the pipeline target contains all values from the needle collection. |
| 0xB9 | `PipelineMap` | - | result slot | `XSlot`=iterator | `YSlot`=item binding | AU=key entry address | Builds a map with each source item as the value. |
| 0xBA | `PipelineMapValue` | - | result slot | `XSlot`=iterator | `YSlot`=item binding | AU=key entry address, BU=value entry address | Builds a map from key and value helper entries. |
| 0xBB | `PipelineDistinct` | - | result slot | `XSlot`=iterator | - | - | Materializes distinct source items in source order. |
| 0xBC | `PipelineDistinctBy` | - | result slot | `XSlot`=iterator | `YSlot`=item binding | AU=projection entry address | Materializes source items distinct by projected key. |
| 0xBD | `PipelineGroupBy` | - | result slot | `XSlot`=iterator | `YSlot`=item binding | AU=key entry address | Groups source items by projected key. |
| 0xBE | `PipelineReverse` | - | result slot | `XSlot`=iterator | - | - | Materializes source items in reverse order. |
| 0xBF | `PipelineSortAscending` | - | result slot | `XSlot`=iterator | - | - | Sorts source items ascending. |
| 0xC0 | `PipelineSortDescending` | - | result slot | `XSlot`=iterator | - | - | Sorts source items descending. |
| 0xC1 | `PipelineOrderByAscending` | - | result slot | `XSlot`=iterator | `YSlot`=item binding | AU=key entry address | Orders source items by projected key ascending. |
| 0xC2 | `PipelineOrderByDescending` | - | result slot | `XSlot`=iterator | `YSlot`=item binding | AU=key entry address | Orders source items by projected key descending. |
| 0xC3 | `PipelineTakeFirst` | - | result slot | `XSlot`=iterator | `ImmediateY`=count | - | Takes the first `Y` items. |
| 0xC4 | `PipelineTakeLast` | - | result slot | `XSlot`=iterator | `ImmediateY`=count | - | Takes the last `Y` items. |
| 0xC5 | `PipelineTakeHighest` | - | result slot | `XSlot`=iterator | `ImmediateY`=count | - | Takes the highest `Y` items. |
| 0xC6 | `PipelineTakeLowest` | - | result slot | `XSlot`=iterator | `ImmediateY`=count | - | Takes the lowest `Y` items. |
| 0xC7 | `PipelineDropFirst` | - | result slot | `XSlot`=iterator | `ImmediateY`=count | - | Drops the first `Y` items. |
| 0xC8 | `PipelineDropLast` | - | result slot | `XSlot`=iterator | `ImmediateY`=count | - | Drops the last `Y` items. |
| 0xC9 | `PipelineDropHighest` | - | result slot | `XSlot`=iterator | `ImmediateY`=count | - | Drops the highest `Y` items. |
| 0xCA | `PipelineDropLowest` | - | result slot | `XSlot`=iterator | `ImmediateY`=count | - | Drops the lowest `Y` items. |
| 0xCB | `PipelineShuffle` | - | result slot | `XSlot`=iterator | - | - | Shuffles source items. |
| 0xCC | `PipelineDraw` | - | result slot | `XSlot`=iterator | `ImmediateY`=count | - | Draws `Y` source items. |
| 0xCD | `PipelineChoose` | - | result slot | `XSlot`=iterator | `ImmediateY`=count | - | Chooses up to `Y` source items. |
| 0xCE | `PipelineChooseRandom` | - | result slot | `XSlot`=iterator | `ImmediateY`=count | - | Randomly chooses up to `Y` source items. |
| 0xCF | `PipelineChooseWeighted` | - | result slot | `XSlot`=iterator | `ImmediateY`=count | AU=item binding slot, BU=weight entry address | Randomly chooses using projected positive weights. |
| 0xD0 | `PipelineDicePatternCountAny` | - | result slot | `XSlot`=iterator | `ImmediateY`=count | - | Tests whether any dice face count reaches `Y`. |
| 0xD1 | `PipelineDicePatternCountFace` | - | result slot | `XSlot`=iterator | `ImmediateY`=count | AU=face entry address | Tests whether a projected face count reaches `Y`. |
| 0xD2 | `PipelineDicePatternFullHouse` | - | result slot | `XSlot`=iterator | - | - | Tests the full-house dice pattern. |
| 0xD3 | `PipelineDicePatternStraight` | - | result slot | `XSlot`=iterator | - | - | Tests the straight dice pattern. |
| 0xD4 | `PipelineTakePatternCountAny` | - | result slot | `XSlot`=iterator | `ImmediateY`=count | - | Takes dice matching any-face count pattern. |
| 0xD5 | `PipelineTakePatternCountFace` | - | result slot | `XSlot`=iterator | `ImmediateY`=count | AU=face entry address | Takes dice matching projected-face count pattern. |
| 0xD6 | `PipelineTakePatternFullHouse` | - | result slot | `XSlot`=iterator | - | - | Takes dice matching full-house pattern. |
| 0xD7 | `PipelineTakePatternStraight` | - | result slot | `XSlot`=iterator | - | - | Takes dice matching straight pattern. |
| 0xD8..0xFF | reserved | - | - | - | - | - | Reserved for future pipeline, extension, or VM opcodes. |

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
