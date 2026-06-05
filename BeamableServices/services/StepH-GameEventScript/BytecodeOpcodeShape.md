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
    [FieldOffset(2)]  public ushort MessageDestination;

    [FieldOffset(4)]  public ushort XSlot;
    [FieldOffset(4)]  public ushort ConditionSlot;
    [FieldOffset(4)]  public ushort StringIndex;
    [FieldOffset(4)]  public ushort SecondaryListIndex;
    [FieldOffset(4)]  public ushort ExternalReferenceIndex;
    [FieldOffset(4)]  public short ImmediateX;
    [FieldOffset(4)]  public ushort Index;
    [FieldOffset(4)]  public short Count;

    [FieldOffset(6)]  public ushort YSlot;
    [FieldOffset(6)]  public ushort TargetAddress;
    [FieldOffset(6)]  public ushort EntryAddress;
    [FieldOffset(6)]  public ushort ListIndex;
    [FieldOffset(6)]  public ushort TypeOperand;
    [FieldOffset(6)]  public short ImmediateY;

    [FieldOffset(8)]  public ushort AU;
    [FieldOffset(10)] public ushort BU;
    [FieldOffset(12)] public ushort CU;
    [FieldOffset(14)] public ushort DU;

    [FieldOffset(8)]  public short AS;
    [FieldOffset(10)] public short BS;
    [FieldOffset(12)] public short CS;
    [FieldOffset(14)] public short DS;

    [FieldOffset(8)]  public long I64;
    [FieldOffset(8)]  public ulong Payload;
    [FieldOffset(8)]  public double F64;
}
```

The byte layout is:

| Byte range | Primary/payload view | 64-bit payload view | Typical use |
| --- | --- | --- | --- |
| `0` | `OpCode` | `OpCode` | Opcode tag, backed by `byte`. |
| `1` | `UnitAndFlags` | `UnitAndFlags` | Low 5 bits carry the numeric unit id; high 3 bits are reserved flags. |
| `2..3` | `DestinationSlot` / `MessageDestination` | `DestinationSlot` | Destination/result slot or static outbound-message bind id. |
| `4..5` | `XSlot` / `ConditionSlot` / `StringIndex` / `SecondaryListIndex` / `ExternalReferenceIndex` / `ImmediateX` / `Index` / `Count` | primary X bytes | First primary operand, signed immediate, unsigned index, or table index. |
| `6..7` | `YSlot` / `TargetAddress` / `EntryAddress` / `ListIndex` / `TypeOperand` / `ImmediateY` | primary Y bytes | Second primary operand, target/entry address, type operand, or primary list index. |
| `8..9` | `AU` / `AS` | `I64`/`Payload`/`F64` bytes `0..1` | Payload word bytes `0..1`, or first payload 16-bit operand. |
| `10..11` | `BU` / `BS` | `I64`/`Payload`/`F64` bytes `2..3` | Payload word bytes `2..3`, or second payload 16-bit operand. |
| `12..13` | `CU` / `CS` | `I64`/`Payload`/`F64` bytes `4..5` | Payload word bytes `4..5`, or third payload 16-bit operand. |
| `14..15` | `DU` / `DS` | `I64`/`Payload`/`F64` bytes `6..7` | Payload word bytes `6..7`, or fourth payload 16-bit operand. |

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
  and `Y`. These fields are stored through semantic aliases such as
  `DestinationSlot`, `MessageDestination`, `XSlot`, `YSlot`, `ConditionSlot`,
  `TargetAddress`, `EntryAddress`, `StringIndex`, `ListIndex`,
  `SecondaryListIndex`, `ExternalReferenceIndex`, `TypeOperand`, `Count`,
  `ImmediateX`, `ImmediateY`, and `Index`.
- **Primary signed 16-bit operand view:** `ImmediateX` and `ImmediateY` are available for
  compact signed immediates in the primary word.
- **Primary unsigned index operand view:** `Index` is available for `IndexAccess`
  and stores a statically known 1-based selector in the same bytes as `XSlot`.
- **Payload 16-bit operand view:** wider instructions use the aligned payload
  word as `AU..DU` or `AS..DS`. The opcode table documents this compactly as
  `AU`=..., `BU`=..., `AS`=..., `BS`=..., and so on.
- **64-bit integer view:** `LoadInteger` reads `I64` directly. Negative integer
  values are stored as their normal two's-complement bit pattern.
- **64-bit float view:** `LoadFloat` reads `F64` directly. This keeps IEEE-754
  `NaN`, `Infinity`, and `-Infinity` portable as raw double bits.
- **Unsigned payload view:** `Payload` exposes the raw payload bits for transport
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

Opcode values are split into operation-family blocks. Larger families may span
multiple 16-value pages. The VM dispatches on the complete byte value; the high
nibble is a format convention, not a second runtime dispatch step.

### Group 1 - Control, Calls, Messages, Types, Values

| Hex | Opcode | UnitAndFlags | DestinationSlot | X | Y | Payload | Notes |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 0x00 | `Nop` | - | - | - | - | - | No operation. |
| 0x01 | `SlotLocals` | - | - | `Count` | - | - | Adds `Count > 0` active local slots or releases `-Count` slots when `Count < 0`. Entry prologs reserve only locals beyond preloaded arguments. |
| 0x02 | `Jump` | - | - | - | `TargetAddress` | - | Unconditional branch. |
| 0x03 | `JumpIfTrue` | - | - | `ConditionSlot` | `TargetAddress` | - | Branches when `X.IsTrue()`. |
| 0x04 | `JumpIfFalse` | - | - | `ConditionSlot` | `TargetAddress` | - | Branches when `X.IsFalse()`. |
| 0x05 | `JumpIfNotTrue` | - | - | `ConditionSlot` | `TargetAddress` | - | Branches when `!X.IsTrue()`, including `nothing`. |
| 0x06 | `Call` | optional `NormalizeResultAsPredicate` | result slot | - | `EntryAddress`=callable/predicate | - | Enters a VM-owned local call frame at a known code address. Arguments are the contiguous staged sequence immediately before the call. With `NormalizeResultAsPredicate`, the returned value is normalized to boolean or `nothing`. |
| 0x07 | `CallStandard` | optional `NormalizeResultAsPredicate` | result slot | `SecondaryListIndex`=extension shape | `ListIndex`=argument slots | - | Calls a built-in standard extension. Shape is `[extensionNameStringIndex, functionNameStringIndex, argumentNameStringIndex...]`. With `NormalizeResultAsPredicate`, the result is normalized to boolean or `nothing`. |
| 0x08 | `CallExternal` | optional `NormalizeResultAsPredicate` | result slot | `ExternalReferenceIndex` | `ListIndex`=argument slots | - | Calls a dynamically bound host extension. With `NormalizeResultAsPredicate`, the result is normalized to boolean or `nothing`. |
| 0x09 | `ReturnVoid` | - | - | - | - | - | Returns no value from the current frame; normal calls map this to DSL `nothing`. |
| 0x0A | `ReturnValue` | - | - | `XSlot`=return | - | - | Returns the value in `X` from the current frame. |
| 0x0B | `EmitMessage` | - | `MessageDestination`=outbound message bind id | - | `ListIndex`=argument slot-list index | - | Emits a statically shaped message without tags. |
| 0x0C | `EmitMessageWithTags` | - | `MessageDestination`=outbound message bind id | `SecondaryListIndex`=tag slot-list index | `ListIndex`=argument slot-list index | - | Emits a statically shaped message with tags. |
| 0x0D | `EmitMessageValue` | - | - | `XSlot`=message | - | - | Emits a dynamic message value without tags. |
| 0x0E | `EmitMessageValueWithTags` | - | - | `XSlot`=message | `ListIndex`=tag slot-list index | - | Emits a dynamic message value with tags. |
| 0x0F | `PublishMessage` | - | `MessageDestination`=outbound message bind id | - | `ListIndex`=argument slot-list index | - | Publishes a statically shaped message without tags. |
| 0x10 | `PublishMessageWithTags` | - | `MessageDestination`=outbound message bind id | `SecondaryListIndex`=tag slot-list index | `ListIndex`=argument slot-list index | - | Publishes a statically shaped message with tags. |
| 0x11 | `PublishMessageValue` | - | - | `XSlot`=message | - | - | Publishes a dynamic message value without tags. |
| 0x12 | `PublishMessageValueWithTags` | - | - | `XSlot`=message | `ListIndex`=tag slot-list index | - | Publishes a dynamic message value with tags. |
| 0x13 | `Cast` | - | result slot | `XSlot`=source | `TypeOperand`=type kind | - | Converts `X` to the declared built-in type. Custom/record types use `CastCustom`. `Cast :tag` validates tag syntax; invalid tag text writes `nothing`. `Cast :vector`/`:point` structurally convert between vectors and points by copying components and unit. |
| 0x14 | `CastCustom` | - | result slot | `XSlot`=source | `TypeOperand`=custom type string | - | Converts `X` to a custom/record type identified by `Y`. |
| 0x15 | `CastUnit` | target numeric unit | result slot | `XSlot`=source | - | - | Converts `X` to the numeric unit carried in `UnitAndFlags`. Units are not represented as declared type kinds. |
| 0x16 | `CastNumeric` | - | result slot | `XSlot`=source | - | - | Coerces `X` through the source-level `:number` type. This is the only numeric path that parses text; invalid text writes `nothing`. Integral values stay integer; otherwise the result is float. |
| 0x17 | `CheckType` | - | result slot | `XSlot`=source | `TypeOperand`=type kind | - | Writes whether `X` has the declared built-in type. Custom/record types use `CheckCustomType`. |
| 0x18 | `CheckCustomType` | - | result slot | `XSlot`=source | `TypeOperand`=custom type string | - | Writes whether `X` has the custom/record type identified by `Y`. |
| 0x19 | `CheckUnit` | target numeric unit | result slot | `XSlot`=source | - | - | Writes whether `X` has the numeric unit carried in `UnitAndFlags`. Units are not represented as declared type kinds. |
| 0x1A | `CheckNumeric` | - | result slot | `XSlot`=source | - | - | Writes whether `X` can be read as a numeric value through the source-level `is numeric` helper. Text is not parsed here; dice are read as their roll sum. |
| 0x1B | `CheckInteger` | - | result slot | `XSlot`=source | - | - | Writes whether `X` can be read as a finite integral numeric value. Text is not parsed here. |
| 0x1C | `CheckFractional` | - | result slot | `XSlot`=source | - | - | Writes whether `X` can be read as a finite non-integral numeric value. Text is not parsed here. |
| 0x1D | `MoveSlot` | - | result slot | `XSlot`=source | - | - | Copies a slot value/reference; the source slot remains unchanged. |
| 0x1E | `MemberAccess` | - | result slot | `StringIndex`=member name | `YSlot`=object | - | Reads a named member. |
| 0x1F | `IndexAccess` | - | result slot | `Index`=1-based index | `YSlot`=object | - | Reads a statically known positional element. |
| 0x20 | `PropertyAccess` | - | result slot | `XSlot`=property/index selector | `YSlot`=object | - | Reads a dynamic property: integer selectors use index semantics; text/tag selectors use member semantics. |
| 0x21 | `BindHandler` | - | result slot | `XSlot`=handler/signature slot | `ListIndex`=argument slots | - | Binds ordered argument values to a handler signature. Argument names come from the signature. |
| 0x22 | `LoadNothing` | - | result slot | - | - | - | Loads `nothing`. |
| 0x23 | `LoadTrue` | - | result slot | - | - | - | Loads boolean `true`. |
| 0x24 | `LoadFalse` | - | result slot | - | - | - | Loads boolean `false`. |
| 0x25 | `LoadInteger` | numeric unit | result slot | - | - | `I64`=signed integer | Loads an inline signed `Int64`. |
| 0x26 | `LoadFloat` | numeric unit | result slot | - | - | `F64`=float | Loads an inline IEEE-754 `Float64`. |
| 0x27 | `LoadPercentage` | - | result slot | - | - | `F64`=ratio | Loads an inline percentage ratio as the dedicated percentage value kind. |
| 0x28 | `LoadText` | - | result slot | `StringIndex` | - | - | Loads a text literal. |
| 0x29 | `LoadTag` | - | result slot | `StringIndex` | - | - | Loads a tag literal. |
| 0x2A | `LoadHandler` | - | result slot | - | `ListIndex`=message shape | - | Loads a handler literal. The shape list is `[messageNameStringIndex, argumentNameStringIndex...]`. |
| 0x2B | `LoadMessage` | - | result slot | `SecondaryListIndex`=message shape | `ListIndex`=argument slots | - | Loads a statically shaped message value. Shape is `[messageNameStringIndex, argumentNameStringIndex...]`. |
| 0x2C | `StageRegister` | - | - | `XSlot`=source | - | - | Stages a register value for the next stage-consuming instruction. |
| 0x2D | `StageNothing` | - | - | - | - | - | Stages DSL `nothing` for the next stage-consuming instruction. |
| 0x2E | `StageTrue` | - | - | - | - | - | Stages `true` for the next stage-consuming instruction. |
| 0x2F | `StageFalse` | - | - | - | - | - | Stages `false` for the next stage-consuming instruction. |
| 0x30 | `StageInteger` | numeric unit | - | - | - | `I64`=integer payload | Stages an inline integer value. |
| 0x31 | `StageFloat` | numeric unit | - | - | - | `F64`=float payload | Stages an inline float value. |
| 0x32 | `StageText` | - | - | `StringIndex` | - | - | Stages a text literal from `StringPool`. |
| 0x33 | `StageTag` | - | - | `StringIndex` | - | - | Stages a tag literal from `StringPool`. |
| 0x34 | `StagePercentage` | - | - | - | - | `F64`=ratio | Stages an inline percentage ratio value. |
| 0x35 | `CreateDice` | - | result slot | `Count`=dice count | `ImmediateY`=side count | - | Creates a dice value; `X` and `Y` are not slots. |
| 0x36 | `CreateVector` | - | result slot | `ImmediateX`=first staged component index | - | - | Creates a vector from the staged component sequence. `ImmediateX` is `0` for x, `1` for y, or `2` for z; missing components become `0`. |
| 0x37 | `CreatePoint` | - | result slot | `ImmediateX`=first staged component index | - | - | Creates a point from the staged component sequence. `ImmediateX` is `0` for x, `1` for y, or `2` for z; missing components become `0`. |
| 0x38 | `CreateList` | - | result slot | - | - | - | Creates a list from the contiguous staged value sequence immediately before the opcode. |
| 0x39 | `CreateMap` | - | result slot | `SecondaryListIndex`=key names | - | - | Creates a map from key names and the contiguous staged value sequence immediately before the opcode. |
| 0x3A | `CreateRange` | - | result slot | `XSlot`=from | `YSlot`=to | - | Creates a range value with implicit step `1`. |
| 0x3B | `CreateRangeWithStep` | - | result slot | `XSlot`=from | `YSlot`=to | `AU`=step slot | Creates a range value with explicit step. |
| 0x3C | `CreateRangeIterator` | - | iterator slot | `XSlot`=from | `YSlot`=to | - | Creates a VM-internal range iterator with default step `+1`. |
| 0x3D | `CreateRangeIteratorWithStep` | - | iterator slot | `XSlot`=from | `YSlot`=to | `AU`=step slot | Creates a VM-internal range iterator with an explicit step. |
| 0x3E | `CreateRangeIteratorShort` | - | iterator slot | `ImmediateX`=from | `ImmediateY`=to | `AS`=step | Creates a compact literal range iterator. |
| 0x3F | `CreateRecord` | - | result slot | `ExternalReferenceIndex`=record bind id | - | - | Calls the record constructor bind with staged constructor-parameter values; computed fields are derived inside the constructor. |
| 0x40 | `CreateExternalType` | - | result slot | `ExternalReferenceIndex`=external type constructor reference | `ListIndex`=argument names | - | Constructs a host-bound external type value from named staged argument values. |
| 0x41 | `HasValue` | - | result slot | `XSlot`=operand | - | - | Semantic value check; exact complement of `IsEmpty`. |
| 0x42 | `IsEmpty` | - | result slot | `XSlot`=operand | - | - | Semantic emptiness check; true for `nothing`, `NaN`, and empty text/collections. |
| 0x43 | `Default` | - | result slot | `XSlot`=left | `YSlot`=right | - | Presence/default operator. |
| 0x44..0x4F | reserved | - | - | - | - | - | Reserved tail of Group 1. |

### Group 2 - Boolean Algebra, Comparison, Math And Random

Group 2 numeric operations treat `nothing` as absent input: if any direct
numeric source operand is `nothing`, the result slot receives `nothing`.
Otherwise, a present but non-computable numeric operation writes numeric `NaN`
to the result slot. Scalar `Divide` keeps IEEE floating-point behavior for zero
denominators; `Modulo` and `Remainder` by zero write `NaN`.
Text is not implicitly numeric in Group 2. `Add` is the exception: when either
operand is text it concatenates text before numeric or collection handling.
Vector arithmetic supports compatible vector addition/subtraction, scalar
multiplication from either side, and vector-by-scalar division. Point arithmetic
supports only affine operations: point plus/minus vector and point minus point.
Point scaling, scalar divided by point, and point operands in integer division,
modulo, or remainder write numeric `NaN` unless a direct operand is `nothing`.
Unary negation of a vector negates its components; unary negation of a point
writes numeric `NaN`. `Power` preserves a left quantity unit only for a
unitless exponent of `1`, including `true` after numeric coercion; unit exponent
`0`, including `false`, produces a unitless result. Other quantity powers write
numeric `NaN`.
`Abs` preserves percentage values as percentages and preserves quantity
units on numeric quantities. Finite exactly integral numeric results are stored
as integer values when they fit signed 64-bit.
Percentage multiplication with a non-percentage scalar or quantity treats the
percentage as its stored ratio and writes a numeric result in the other
operand's value family, so `10% * 10` and `10 * 10%` both write numeric `1`.
`Equal` and `NotEqual` propagate `Nothing` when either operand
is absent. Exact equality first compares the numeric view for numeric-capable
operands (numbers, percentages, booleans, dice sums, and numeric tag
constants), requiring identical quantity units or no unit on both operands; this
also applies to integer fast paths. If numeric comparison does not apply, exact
equality requires the same value kind and kind-specific structural equality.
When either numeric side is represented as double precision, finite values
compare equal when their IEEE 754 values are within two ULPs. There is no
separate approximate-equality opcode.

| Hex | Opcode | UnitAndFlags | DestinationSlot | X | Y | Payload | Notes |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 0x50 | `Or` | - | result slot | `XSlot`=left | `YSlot`=right | - | Tri-state boolean combine. |
| 0x51 | `And` | - | result slot | `XSlot`=left | `YSlot`=right | - | Tri-state boolean combine. |
| 0x52 | `Xor` | - | result slot | `XSlot`=left | `YSlot`=right | - | Tri-state boolean combine. |
| 0x53 | `Implies` | - | result slot | `XSlot`=antecedent | `YSlot`=consequent | - | Binary implication combine. |
| 0x54 | `Not` | - | result slot | `XSlot`=operand | - | - | Logical negation. |
| 0x55 | `Equal` | - | result slot | `XSlot`=left | `YSlot`=right | - | Binary comparison. |
| 0x56 | `NotEqual` | - | result slot | `XSlot`=left | `YSlot`=right | - | Binary comparison. |
| 0x57 | `Less` | - | result slot | `XSlot`=left | `YSlot`=right | - | Binary comparison. |
| 0x58 | `Greater` | - | result slot | `XSlot`=left | `YSlot`=right | - | Binary comparison. |
| 0x59 | `LessOrEqual` | - | result slot | `XSlot`=left | `YSlot`=right | - | Binary comparison. |
| 0x5A | `GreaterOrEqual` | - | result slot | `XSlot`=left | `YSlot`=right | - | Binary comparison. |
| 0x5B | `Add` | - | result slot | `XSlot`=left | `YSlot`=right | - | Binary numeric operation. |
| 0x5C | `Subtract` | - | result slot | `XSlot`=left | `YSlot`=right | - | Binary numeric operation. |
| 0x5D | `Multiply` | - | result slot | `XSlot`=left | `YSlot`=right | - | Binary numeric operation. |
| 0x5E | `Divide` | - | result slot | `XSlot`=left | `YSlot`=right | - | Binary numeric operation. |
| 0x5F | `Power` | - | result slot | `XSlot`=left | `YSlot`=right | - | Binary numeric operation. |
| 0x60 | `IntegerDivide` | - | result slot | `XSlot`=left | `YSlot`=right | - | Floor-like integer division operation. |
| 0x61 | `Modulo` | - | result slot | `XSlot`=left | `YSlot`=right | - | Numeric modulo operation. |
| 0x62 | `Remainder` | - | result slot | `XSlot`=left | `YSlot`=right | - | Numeric remainder operation. |
| 0x63 | `Min` | - | result slot | `XSlot`=left | `YSlot`=right | - | Binary extrema reduce step. |
| 0x64 | `Max` | - | result slot | `XSlot`=left | `YSlot`=right | - | Binary extrema reduce step. |
| 0x65 | `Negate` | - | result slot | `XSlot`=operand | - | - | Numeric negation. |
| 0x66 | `Abs` | - | result slot | `XSlot`=operand | - | - | Absolute value. |
| 0x67 | `LogN` | - | result slot | `XSlot`=operand | - | - | Natural logarithm. |
| 0x68 | `Chance` | - | result slot | `XSlot`=operand | - | - | Chance evaluation. |
| 0x69 | `Clamp` | - | result slot | `XSlot`=value | `YSlot`=minimum | `AU`=maximum slot | The only opcode with three direct source slots. |
| 0x6A | `RandomTake` | - | result slot | `XSlot`=from | `YSlot`=to | - | Takes a random value from the requested range using the current random scope. |
| 0x6B | `RandomPush` | - | - | `XSlot`=seed | - | - | Pushes a nested random scope from a dynamic unitless integer seed slot. |
| 0x6C | `RandomPushConstant` | - | - | - | - | `I64`=signed seed | Pushes a nested random scope from inline signed `Int64`. |
| 0x6D | `RandomPop` | - | - | - | - | - | Restores the previous random scope. |
| 0x6E | `Term` | - | result slot | `XSlot`=series | `YSlot`=index | - | Reads a zero-based mathematical series term; non-series sources yield `nothing`. |
| 0x6F | `TakeFirst` | - | result slot | `XSlot`=source | `ImmediateY`=count | - | Takes the first `Y` values from a series, list, dice, or range source. |
| 0x70 | `DropFirst` | - | result slot | `XSlot`=source | `ImmediateY`=count | - | Drops the first `Y` values from a series, list, dice, or range source. |
| 0x71 | `TakeLast` | - | result slot | `XSlot`=source | `ImmediateY`=count | - | Takes the last `Y` values from a finite list, dice, or range source; series yields `nothing`. |
| 0x72 | `DropLast` | - | result slot | `XSlot`=source | `ImmediateY`=count | - | Drops the last `Y` values from a finite list, dice, or range source; series yields `nothing`. |
| 0x73..0x8F | reserved | - | - | - | - | - | Reserved after compacting boolean algebra, math, random, and series into Group 2. |

### Group 3 - Text, Collections, Streams

| Hex | Opcode | UnitAndFlags | DestinationSlot | X | Y | Payload | Notes |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 0x90 | `Length` | - | result slot | `XSlot`=operand | - | - | Length operation. Text and tags use raw text length. |
| 0x91 | `StartsWith` | - | result slot | `XSlot`=left | `YSlot`=right | - | Text/tag raw-text prefix check or list/dice/range sequence prefix check. |
| 0x92 | `EndsWith` | - | result slot | `XSlot`=left | `YSlot`=right | - | Text/tag raw-text suffix check or list/dice/range sequence suffix check. |
| 0x93 | `Contains` | - | result slot | `XSlot`=needle | `YSlot`=container | - | Text/tag substring, map key, list/dice/range membership, or vector/point component membership. |
| 0x94 | `ContainsValue` | - | result slot | `XSlot`=needle | `YSlot`=container | - | Value membership for map-like containers, vectors, and points. |
| 0x95 | `Union` | - | result slot | `XSlot`=left | `YSlot`=right | - | Collection union/merge for list, dice, and map shapes; invalid shapes -> `nothing`. |
| 0x96 | `Intersect` | - | result slot | `XSlot`=left | `YSlot`=right | - | Map key intersection or list/dice multiset intersection; invalid shapes -> `nothing`. |
| 0x97 | `Zip` | - | result slot | `XSlot`=left | `YSlot`=right | - | List zip into `{ left, right }` maps up to the shorter length; invalid shapes -> `nothing`. |
| 0x98 | `KeysOfMap` | - | result slot | `XSlot`=operand | - | - | Map/custom-type keys projection; `nothing` and non-map operands produce `nothing`. |
| 0x99 | `ValuesOfMap` | - | result slot | `XSlot`=operand | - | - | Map/custom-type values projection; `nothing` and non-map operands produce `nothing`. |
| 0x9A | `EntriesOfMap` | - | result slot | `XSlot`=operand | - | - | Map/custom-type entries projection; `nothing` and non-map operands produce `nothing`. |
| 0x9B..0xA0 | reserved | - | - | - | - | - | Reserved space after map projections. |
| 0xA1 | `StreamCreate` | - | iterator slot | `XSlot`=collection | - | - | Creates a VM-internal iterator over a collection or range value. |
| 0xA2 | `StreamNext` | - | item slot | `XSlot`=iterator | `TargetAddress`=no-more | - | Writes the next item and continues, or jumps to `Y` when exhausted. |
| 0xA3 | `StreamClose` | - | - | `XSlot`=iterator | - | - | Disposes/closes a VM-internal iterator/stream. |
| 0xA4 | `StreamMap` | - | iterator slot | `XSlot`=source iterator | `EntryAddress`=map entry | `AU`=helper item slot, `BU`=capture slot-list index | Creates a lazy one-to-one stream transform. The entry result is yielded. |
| 0xA5 | `StreamFilter` | - | iterator slot | `XSlot`=source iterator | `EntryAddress`=predicate entry | `AU`=helper item slot, `BU`=capture slot-list index | Creates a lazy filtering stream transform. Truthy predicate results yield the original item. |
| 0xA6 | `StreamCount` | - | result slot | `XSlot`=iterator | - | - | Counts finite stream elements. Empty -> `0`; series -> `nothing`. |
| 0xA7 | `StreamSum` | - | result slot | `XSlot`=iterator | - | - | Sums finite stream elements. Empty -> `0`; series -> `nothing`. |
| 0xA8 | `StreamAverage` | - | result slot | `XSlot`=iterator | - | - | Averages finite stream elements. Empty/series -> `nothing`. |
| 0xA9 | `StreamMin` | - | result slot | `XSlot`=iterator | `YSlot`=item binding | `AU`=projection entry address | Selects the source item with the lowest projected numeric value. Empty/series -> `nothing`. |
| 0xAA | `StreamMax` | - | result slot | `XSlot`=iterator | `YSlot`=item binding | `AU`=projection entry address | Selects the source item with the highest projected numeric value. Empty/series -> `nothing`. |
| 0xAB | `StreamCollectList` | - | result slot | `XSlot`=iterator | - | - | Materializes an iterator as a list. |
| 0xAC | `StreamCollectMap` | - | result slot | `XSlot`=iterator | `YSlot`=item binding | `AU`=key entry address | Materializes an iterator as a map with each source item as the value. |
| 0xAD | `StreamCollectMapValue` | - | result slot | `XSlot`=iterator | `YSlot`=item binding | `AU`=key entry address, `BU`=value entry address | Materializes an iterator as a map from key and value helper entries. |
| 0xAE | `StreamCollectFirst` | - | result slot | `XSlot`=iterator | - | - | Returns the first stream element or `nothing`. Short-circuits after the first item. |
| 0xAF | `StreamCollectLast` | - | result slot | `XSlot`=iterator | - | - | Returns the last stream element or `nothing`. Consumes the iterator. |
| 0xB0 | `StreamCollectSingle` | - | result slot | `XSlot`=iterator | - | - | Returns the only stream element or `nothing`. Consumes enough of the iterator to detect multiple items. |

### Group 4 - Pipeline Operations

| Hex | Opcode | UnitAndFlags | DestinationSlot | X | Y | Payload | Notes |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 0xD0 | `PipelineHasAny` | - | result slot | `XSlot`=iterator | - | - | Tri-state `any` over projected predicate values. |
| 0xD1 | `PipelineHasAll` | - | result slot | `XSlot`=iterator | - | - | Tri-state `all` over projected predicate values. |
| 0xD2 | `PipelineContainsSingle` | - | result slot | `XSlot`=iterator | `YSlot`=needle | - | Tests whether the pipeline target contains one value. |
| 0xD3 | `PipelineContainsAny` | - | result slot | `XSlot`=iterator | `YSlot`=needle | - | Tests whether the pipeline target contains any values from the needle collection. |
| 0xD4 | `PipelineContainsAll` | - | result slot | `XSlot`=iterator | `YSlot`=needle | - | Tests whether the pipeline target contains all values from the needle collection. |
| 0xD5 | `PipelineDistinct` | - | result slot | `XSlot`=iterator | - | - | Materializes distinct source items in source order. |
| 0xD6 | `PipelineDistinctBy` | - | result slot | `XSlot`=iterator | `YSlot`=item binding | `AU`=projection entry address | Materializes source items distinct by projected key. |
| 0xD7 | `PipelineGroupBy` | - | result slot | `XSlot`=iterator | `YSlot`=item binding | `AU`=key entry address | Groups source items by projected key. |
| 0xD8 | `PipelineReverse` | - | result slot | `XSlot`=iterator | - | - | Materializes source items in reverse order. |
| 0xD9 | `PipelineSortAscending` | - | result slot | `XSlot`=iterator | - | - | Sorts source items ascending. |
| 0xDA | `PipelineSortDescending` | - | result slot | `XSlot`=iterator | - | - | Sorts source items descending. |
| 0xDB | `PipelineOrderByAscending` | - | result slot | `XSlot`=iterator | `YSlot`=item binding | `AU`=key entry address | Orders source items by projected key ascending. |
| 0xDC | `PipelineOrderByDescending` | - | result slot | `XSlot`=iterator | `YSlot`=item binding | `AU`=key entry address | Orders source items by projected key descending. |
| 0xDD | `PipelineTakeFirst` | - | result slot | `XSlot`=iterator | `ImmediateY`=count | - | Takes the first `Y` items. |
| 0xDE | `PipelineTakeLast` | - | result slot | `XSlot`=iterator | `ImmediateY`=count | - | Takes the last `Y` items. |
| 0xDF | `PipelineTakeHighest` | - | result slot | `XSlot`=iterator | `ImmediateY`=count | - | Takes the highest `Y` items. |
| 0xE0 | `PipelineTakeLowest` | - | result slot | `XSlot`=iterator | `ImmediateY`=count | - | Takes the lowest `Y` items. |
| 0xE1 | `PipelineDropFirst` | - | result slot | `XSlot`=iterator | `ImmediateY`=count | - | Drops the first `Y` items. |
| 0xE2 | `PipelineDropLast` | - | result slot | `XSlot`=iterator | `ImmediateY`=count | - | Drops the last `Y` items. |
| 0xE3 | `PipelineDropHighest` | - | result slot | `XSlot`=iterator | `ImmediateY`=count | - | Drops the highest `Y` items. |
| 0xE4 | `PipelineDropLowest` | - | result slot | `XSlot`=iterator | `ImmediateY`=count | - | Drops the lowest `Y` items. |
| 0xE5 | `PipelineShuffle` | - | result slot | `XSlot`=iterator | - | - | Shuffles source items. |
| 0xE6 | `PipelineDraw` | - | result slot | `XSlot`=iterator | `ImmediateY`=count | - | Draws `Y` source items. |
| 0xE7 | `PipelineChoose` | - | result slot | `XSlot`=iterator | `ImmediateY`=count | - | Chooses up to `Y` source items. |
| 0xE8 | `PipelineChooseRandom` | - | result slot | `XSlot`=iterator | `ImmediateY`=count | - | Randomly chooses up to `Y` source items. |
| 0xE9 | `PipelineChooseWeighted` | - | result slot | `XSlot`=iterator | `ImmediateY`=count | `AU`=item binding slot, `BU`=weight entry address | Randomly chooses using projected positive weights. |
| 0xEA | `PipelineDicePatternCountAny` | - | result slot | `XSlot`=iterator | `ImmediateY`=count | - | Tests whether any dice face count reaches `Y`. |
| 0xEB | `PipelineDicePatternCountFace` | - | result slot | `XSlot`=iterator | `ImmediateY`=count | `AU`=face entry address | Tests whether a projected face count reaches `Y`. |
| 0xEC | `PipelineDicePatternFullHouse` | - | result slot | `XSlot`=iterator | - | - | Tests the full-house dice pattern. |
| 0xED | `PipelineDicePatternStraight` | - | result slot | `XSlot`=iterator | - | - | Tests the straight dice pattern. |
| 0xEE | `PipelineTakePatternCountAny` | - | result slot | `XSlot`=iterator | `ImmediateY`=count | - | Takes dice matching any-face count pattern. |
| 0xEF | `PipelineTakePatternCountFace` | - | result slot | `XSlot`=iterator | `ImmediateY`=count | `AU`=face entry address | Takes dice matching projected-face count pattern. |
| 0xF0 | `PipelineTakePatternFullHouse` | - | result slot | `XSlot`=iterator | - | - | Takes dice matching full-house pattern. |
| 0xF1 | `PipelineTakePatternStraight` | - | result slot | `XSlot`=iterator | - | - | Takes dice matching straight pattern. |
| 0xF2 | `PipelineListCreateBuilder` | - | builder slot | - | - | - | Creates a VM-internal list builder for generated pipeline collections. |
| 0xF3 | `PipelineListBuilderAdd` | - | - | `XSlot`=builder | `YSlot`=item | - | Adds an item and checks `MaxGeneratedCollectionItems`. |
| 0xF4 | `PipelineListBuilderFinish` | - | result slot | `XSlot`=builder | - | - | Materializes the pipeline list builder as a list. |

### Reserved Opcode Space

| Hex | Opcode | UnitAndFlags | DestinationSlot | X | Y | Payload | Notes |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 0xB1..0xCF | reserved | - | - | - | - | - | Reserved for future stream operators. |
| 0xF5..0xFF | reserved | - | - | - | - | - | Reserved for future pipeline, extension, or VM opcodes. |

## Side-Table Summary

| Pool / table | Used by |
| --- | --- |
| `StringPool` | `LoadText`, `LoadTag`, `MemberAccess`; indirectly through message/name lists in `UShortListPool` |
| `UShortListPool` | `LoadHandler`, `EmitMessage*`, `PublishMessage*`, `CreateExternalType`, `CreateMap`, `BindHandler`, `CallStandard*`, `CallExternal*` |
| `OutboundMessageSignatures` / binary `OutboundMessage` binds | Statically shaped `emit`/`publish` message signatures, used by loaders without scanning code |

Local calls, predicate calls, construction, extension calls, and collection/message
builder opcodes now reference bind ids, entry addresses, or `StringPool`/`UShortListPool`
directly from the instruction word. Record constructor code addresses live in
`Record` bind entries, not in `CreateRecord` instructions.
Pipeline selectors are lowered into linear helper entries and fixed iterator or
terminal opcodes; there are no pipeline selector or pattern pools.
