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
| `2..3` | `DestinationSlot` / `MessageDestination` | `DestinationSlot` | Destination/result register or static outbound-message bind id. |
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
- `Dst` is the raw `DestinationSlot` register as hex.
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
  and debug tooling. Portable opcodes that use signed numeric payloads read `I64`
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

- `DestinationSlot` is the destination frame register for value-producing instructions.
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
| 0x01 | `SlotLocals` | - | - | `Count` | - | - | Adds `Count > 0` active local registers or releases `-Count` registers when `Count < 0`. Entry prologs reserve only locals beyond preloaded arguments. |
| 0x02 | `Jump` | - | - | - | `TargetAddress` | - | Unconditional branch. |
| 0x03 | `JumpIfTrue` | - | - | `ConditionSlot` | `TargetAddress` | - | Branches when `X.IsTrue()`. |
| 0x04 | `JumpIfFalse` | - | - | `ConditionSlot` | `TargetAddress` | - | Branches when `X.IsFalse()`. |
| 0x05 | `JumpIfNotTrue` | - | - | `ConditionSlot` | `TargetAddress` | - | Branches when `!X.IsTrue()`, including `nothing`. |
| 0x06 | `JumpIfNothing` | - | - | `ConditionSlot` | `TargetAddress` | - | Branches when `X.Kind` is `Nothing`. |
| 0x07 | `Call` | optional `NormalizeResultAsPredicate` | result register | - | `EntryAddress`=callable/predicate | - | Enters a VM-owned local call frame at a known code address. Arguments are the contiguous staged sequence immediately before the call. With `NormalizeResultAsPredicate`, the returned value is normalized to boolean or `nothing`. |
| 0x08 | `CallStandard` | optional `NormalizeResultAsPredicate` | result register | `SecondaryListIndex`=extension shape | `ListIndex`=argument register-list index | - | Calls a built-in standard extension. Shape is `[extensionNameStringIndex, functionNameStringIndex, argumentNameStringIndex...]`. With `NormalizeResultAsPredicate`, the result is normalized to boolean or `nothing`. |
| 0x09 | `CallExternal` | optional `NormalizeResultAsPredicate` | result register | `ExternalReferenceIndex` | `ListIndex`=argument register-list index | - | Calls a dynamically bound host extension. With `NormalizeResultAsPredicate`, the result is normalized to boolean or `nothing`. |
| 0x0A | `ReturnVoid` | - | - | - | - | - | Returns no value from the current frame; normal calls map this to DSL `nothing`. |
| 0x0B | `ReturnValue` | - | - | `XSlot`=return | - | - | Returns the value in `X` from the current frame. |
| 0x0C | `EmitMessage` | - | `MessageDestination`=outbound message bind id | - | `ListIndex`=argument register-list index | - | Emits a statically shaped message without tags. |
| 0x0D | `EmitMessageWithTags` | - | `MessageDestination`=outbound message bind id | `SecondaryListIndex`=tag register-list index | `ListIndex`=argument register-list index | - | Emits a statically shaped message with tags. |
| 0x0E | `EmitMessageValue` | - | - | `XSlot`=message | - | - | Emits a dynamic message value without tags. |
| 0x0F | `EmitMessageValueWithTags` | - | - | `XSlot`=message | `ListIndex`=tag register-list index | - | Emits a dynamic message value with tags. |
| 0x10 | `PublishMessage` | - | `MessageDestination`=outbound message bind id | - | `ListIndex`=argument register-list index | - | Publishes a statically shaped message without tags. |
| 0x11 | `PublishMessageWithTags` | - | `MessageDestination`=outbound message bind id | `SecondaryListIndex`=tag register-list index | `ListIndex`=argument register-list index | - | Publishes a statically shaped message with tags. |
| 0x12 | `PublishMessageValue` | - | - | `XSlot`=message | - | - | Publishes a dynamic message value without tags. |
| 0x13 | `PublishMessageValueWithTags` | - | - | `XSlot`=message | `ListIndex`=tag register-list index | - | Publishes a dynamic message value with tags. |
| 0x14 | `Cast` | - | result register | `XSlot`=source | `TypeOperand`=type kind | - | Converts `X` to the declared built-in type. Custom/record types use `CastCustom`. `Cast :tag` validates tag syntax; invalid tag text writes `nothing`. `Cast :vector`/`:point` structurally convert between vectors and points by copying components and unit. |
| 0x15 | `CastCustom` | - | result register | `XSlot`=source | `TypeOperand`=custom type string | - | Converts `X` to a custom/record type identified by `Y`. |
| 0x16 | `CastUnit` | target numeric unit | result register | `XSlot`=source | - | - | Converts `X` to the numeric unit carried in `UnitAndFlags`. Units are not represented as declared type kinds. |
| 0x17 | `CastNumeric` | - | result register | `XSlot`=source | - | - | Coerces `X` through the source-level `:number` type. This is the only numeric path that parses text; invalid text writes `nothing`. Integral values stay integer; otherwise the result is float. |
| 0x18 | `CheckType` | - | result register | `XSlot`=source | `TypeOperand`=type kind | - | Writes whether `X` has the declared built-in type. Custom/record types use `CheckCustomType`. |
| 0x19 | `CheckCustomType` | - | result register | `XSlot`=source | `TypeOperand`=custom type string | - | Writes whether `X` has the custom/record type identified by `Y`. |
| 0x1A | `CheckUnit` | target numeric unit | result register | `XSlot`=source | - | - | Writes whether `X` has the numeric unit carried in `UnitAndFlags`. Units are not represented as declared type kinds. |
| 0x1B | `CheckNumeric` | - | result register | `XSlot`=source | - | - | Writes whether `X` has a runtime numeric view. Numbers, percentages, booleans (`false` = `0`, `true` = `1`), and dice sums are numeric. Text and tags are not parsed here. |
| 0x1C | `CheckInteger` | - | result register | `XSlot`=source | - | - | Writes whether `X` has a finite integral numeric view. Booleans and dice are integer. Text is not parsed here. |
| 0x1D | `CheckFractional` | - | result register | `XSlot`=source | - | - | Writes whether `X` has a finite non-integral numeric view. Text is not parsed here. |
| 0x1E | `Move` | - | result register | `XSlot`=source | - | - | Copies a register value/reference; the source register remains unchanged. |
| 0x1F | `MemberAccess` | - | result register | `StringIndex`=member name | `YSlot`=object | - | Reads a named member. |
| 0x20 | `IndexAccess` | - | result register | `Index`=1-based index | `YSlot`=object | - | Reads a statically known positional element. |
| 0x21 | `PropertyAccess` | - | result register | `XSlot`=property/index selector | `YSlot`=object | - | Reads a dynamic property: integer selectors use index semantics; text/tag selectors use member semantics. |
| 0x22 | `BindHandler` | - | result register | `XSlot`=handler/signature register | `ListIndex`=argument register-list index | - | Binds ordered argument values to a handler signature. Argument names come from the signature. |
| 0x23 | `LoadNothing` | - | result register | - | - | - | Loads `nothing`. |
| 0x24 | `LoadTrue` | - | result register | - | - | - | Loads boolean `true`. |
| 0x25 | `LoadFalse` | - | result register | - | - | - | Loads boolean `false`. |
| 0x26 | `LoadInteger` | numeric unit | result register | - | - | `I64`=signed integer | Loads an inline signed `Int64`. |
| 0x27 | `LoadFloat` | numeric unit | result register | - | - | `F64`=float | Loads an inline IEEE-754 `Float64`. |
| 0x28 | `LoadPercentage` | - | result register | - | - | `F64`=ratio | Loads an inline percentage ratio as the dedicated percentage value kind. |
| 0x29 | `LoadText` | - | result register | `StringIndex` | - | - | Loads a text literal. |
| 0x2A | `LoadTag` | - | result register | `StringIndex` | - | - | Loads a tag literal. |
| 0x2B | `LoadHandler` | - | result register | - | `ListIndex`=message shape | - | Loads a handler literal. The shape list is `[messageNameStringIndex, argumentNameStringIndex...]`. |
| 0x2C | `LoadMessage` | - | result register | `SecondaryListIndex`=message shape | `ListIndex`=argument register-list index | - | Loads a statically shaped message value. Shape is `[messageNameStringIndex, argumentNameStringIndex...]`. |
| 0x2D | `StageRegister` | - | - | `XSlot`=source | - | - | Stages a register value for the next stage-consuming instruction. |
| 0x2E | `StageNothing` | - | - | - | - | - | Stages DSL `nothing` for the next stage-consuming instruction. |
| 0x2F | `StageTrue` | - | - | - | - | - | Stages `true` for the next stage-consuming instruction. |
| 0x30 | `StageFalse` | - | - | - | - | - | Stages `false` for the next stage-consuming instruction. |
| 0x31 | `StageInteger` | numeric unit | - | - | - | `I64`=integer payload | Stages an inline integer value. |
| 0x32 | `StageFloat` | numeric unit | - | - | - | `F64`=float payload | Stages an inline float value. |
| 0x33 | `StageText` | - | - | `StringIndex` | - | - | Stages a text literal from `StringPool`. |
| 0x34 | `StageTag` | - | - | `StringIndex` | - | - | Stages a tag literal from `StringPool`. |
| 0x35 | `StagePercentage` | - | - | - | - | `F64`=ratio | Stages an inline percentage ratio value. |
| 0x36 | `CreateDice` | - | result register | `Count`=dice count | `ImmediateY`=side count | - | Creates a dice value; `X` and `Y` are not registers. |
| 0x37 | `CreateVector` | - | result register | `ImmediateX`=first staged component index | - | - | Creates a vector from the staged component sequence. `ImmediateX` is `0` for x, `1` for y, or `2` for z; missing components become `0`. |
| 0x38 | `CreatePoint` | - | result register | `ImmediateX`=first staged component index | - | - | Creates a point from the staged component sequence. `ImmediateX` is `0` for x, `1` for y, or `2` for z; missing components become `0`. |
| 0x39 | `CreateList` | - | result register | - | - | - | Creates a list from the contiguous staged value sequence immediately before the opcode. |
| 0x3A | `CreateMap` | - | result register | `SecondaryListIndex`=key names | - | - | Creates a map from key names and the contiguous staged value sequence immediately before the opcode. |
| 0x3B | `CreateRange` | - | result register | `XSlot`=from | `YSlot`=to | - | Creates a range value with implicit step `1`. |
| 0x3C | `CreateRangeWithStep` | - | result register | `XSlot`=from | `YSlot`=to | `AU`=step register | Creates a range value with explicit step. |
| 0x3D | `CreateRangeIterator` | - | iterator register | `XSlot`=from | `YSlot`=to | - | Creates a VM-internal range iterator with default step `+1`. |
| 0x3E | `CreateRangeIteratorWithStep` | - | iterator register | `XSlot`=from | `YSlot`=to | `AU`=step register | Creates a VM-internal range iterator with an explicit step. |
| 0x3F | `CreateRangeIteratorShort` | - | iterator register | `ImmediateX`=from | `ImmediateY`=to | `AS`=step | Creates a compact literal range iterator. |
| 0x40 | `CreateRecord` | - | result register | `ExternalReferenceIndex`=record bind id | - | - | Calls the record constructor bind with staged constructor-parameter values; computed fields are derived inside the constructor. |
| 0x41 | `CreateExternalType` | - | result register | `ExternalReferenceIndex`=external type constructor reference | `ListIndex`=argument names | - | Constructs a host-bound external type value from named staged argument values. |
| 0x42 | `HasValue` | - | result register | `XSlot`=operand | - | - | Semantic value check; exact complement of `IsEmpty`. |
| 0x43 | `IsEmpty` | - | result register | `XSlot`=operand | - | - | Semantic emptiness check; true for `nothing`, `NaN`, and empty text/collections. |
| 0x44 | `Default` | - | result register | `XSlot`=left | `YSlot`=right | - | Presence/default operator. |
| 0x45..0x4F | reserved | - | - | - | - | - | Reserved tail of Group 1. |

### Group 2 - Boolean Algebra, Comparison, Math And Random

Group 2 numeric operations treat `nothing` as absent input: if any direct
numeric source operand is `nothing`, the result register receives `nothing`.
Otherwise, a present but non-computable numeric operation writes numeric `NaN`
to the result register. Scalar `Divide` keeps IEEE floating-point behavior for zero
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
operands (numbers, percentages, booleans, and dice sums), requiring identical
quantity units or no unit on both operands; this
also applies to integer fast paths. If numeric comparison does not apply, exact
equality requires the same value kind and kind-specific structural equality.
When either numeric side is represented as double precision, finite values
compare equal when their IEEE 754 values are within two ULPs. There is no
separate approximate-equality opcode.

| Hex | Opcode | UnitAndFlags | DestinationSlot | X | Y | Payload | Notes |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 0x50 | `Or` | - | result register | `XSlot`=left | `YSlot`=right | - | Tri-state boolean combine. |
| 0x51 | `And` | - | result register | `XSlot`=left | `YSlot`=right | - | Tri-state boolean combine. |
| 0x52 | `Xor` | - | result register | `XSlot`=left | `YSlot`=right | - | Tri-state boolean combine. |
| 0x53 | `Implies` | - | result register | `XSlot`=antecedent | `YSlot`=consequent | - | Binary implication combine. |
| 0x54 | `Not` | - | result register | `XSlot`=operand | - | - | Logical negation. |
| 0x55 | `Equal` | - | result register | `XSlot`=left | `YSlot`=right | - | Binary comparison. |
| 0x56 | `NotEqual` | - | result register | `XSlot`=left | `YSlot`=right | - | Binary comparison. |
| 0x57 | `Less` | - | result register | `XSlot`=left | `YSlot`=right | - | Binary comparison. |
| 0x58 | `Greater` | - | result register | `XSlot`=left | `YSlot`=right | - | Binary comparison. |
| 0x59 | `LessOrEqual` | - | result register | `XSlot`=left | `YSlot`=right | - | Binary comparison. |
| 0x5A | `GreaterOrEqual` | - | result register | `XSlot`=left | `YSlot`=right | - | Binary comparison. |
| 0x5B | `Add` | - | result register | `XSlot`=left | `YSlot`=right | - | Binary numeric operation. |
| 0x5C | `Subtract` | - | result register | `XSlot`=left | `YSlot`=right | - | Binary numeric operation. |
| 0x5D | `Multiply` | - | result register | `XSlot`=left | `YSlot`=right | - | Binary numeric operation. |
| 0x5E | `Divide` | - | result register | `XSlot`=left | `YSlot`=right | - | Binary numeric operation. |
| 0x5F | `Power` | - | result register | `XSlot`=left | `YSlot`=right | - | Binary numeric operation. |
| 0x60 | `IntegerDivide` | - | result register | `XSlot`=left | `YSlot`=right | - | Floor-like integer division operation. |
| 0x61 | `Modulo` | - | result register | `XSlot`=left | `YSlot`=right | - | Numeric modulo operation. |
| 0x62 | `Remainder` | - | result register | `XSlot`=left | `YSlot`=right | - | Numeric remainder operation. |
| 0x63 | `Min` | - | result register | `XSlot`=left | `YSlot`=right | - | Binary extrema reduce step. |
| 0x64 | `Max` | - | result register | `XSlot`=left | `YSlot`=right | - | Binary extrema reduce step. |
| 0x65 | `Negate` | - | result register | `XSlot`=operand | - | - | Numeric negation. |
| 0x66 | `Abs` | - | result register | `XSlot`=operand | - | - | Absolute value. |
| 0x67 | `LogN` | - | result register | `XSlot`=operand | - | - | Natural logarithm. |
| 0x68 | `Chance` | - | result register | `XSlot`=operand | - | - | Chance evaluation. |
| 0x69 | `Clamp` | - | result register | `XSlot`=value | `YSlot`=minimum | `AU`=maximum register | The only opcode with three direct source registers. |
| 0x6A | `RandomTake` | - | result register | `XSlot`=from | `YSlot`=to | - | Takes an integer random value from integer bounds using the current random scope. |
| 0x6B | `RandomTakeFloat` | - | result register | `XSlot`=from | `YSlot`=to | - | Takes a float random value from numeric bounds using the current random scope. |
| 0x6C | `RandomPush` | - | - | `XSlot`=seed | - | - | Pushes a nested random scope from a dynamic unitless integer seed register. |
| 0x6D | `RandomPushConstant` | - | - | - | - | `I64`=signed seed | Pushes a nested random scope from inline signed `Int64`. |
| 0x6E | `RandomPop` | - | - | - | - | - | Restores the previous random scope. |
| 0x6F | `Term` | - | result register | `XSlot`=series | `YSlot`=index | - | Reads a zero-based mathematical series term; non-series sources yield `nothing`. |
| 0x70 | `Exp` | - | result register | `XSlot`=operand | - | - | Natural exponential. Unitless numeric input only; overflow to `+Infinity` is valid. |
| 0x71 | `Floor` | - | result register | `XSlot`=operand | - | - | Floors a unitless numeric value and writes an integer. |
| 0x72 | `Ceil` | - | result register | `XSlot`=operand | - | - | Ceils a unitless numeric value and writes an integer. |
| 0x73 | `Truncate` | - | result register | `XSlot`=operand | - | - | Truncates a unitless numeric value toward zero and writes an integer. |
| 0x74 | `RoundHalfEven` | - | result register | `XSlot`=operand | - | - | Rounds a unitless numeric value using midpoint-to-even and writes an integer. |
| 0x75 | `RoundHalfUp` | - | result register | `XSlot`=operand | - | - | Rounds midpoint values away from zero and writes an integer. |
| 0x76 | `RoundHalfDown` | - | result register | `XSlot`=operand | - | - | Rounds midpoint values toward zero and writes an integer. |
| 0x77 | `DegreeToRadians` | - | result register | `XSlot`=operand | - | - | Converts degrees to unitless radians. |
| 0x78 | `DegreeFromRadians` | - | result register | `XSlot`=operand | - | - | Converts unitless radians to a degree quantity. |
| 0x79 | `WrapDegree` | - | result register | `XSlot`=operand | - | - | Normalizes a degree or unitless numeric value into `[0, 360)` degrees. |
| 0x7A | `Sin` | - | result register | `XSlot`=operand | - | - | Sine. Unitless input is radians; degree input is converted to radians; result is unitless. |
| 0x7B | `Cos` | - | result register | `XSlot`=operand | - | - | Cosine. Unitless input is radians; degree input is converted to radians; result is unitless. |
| 0x7C | `Tan` | - | result register | `XSlot`=operand | - | - | Tangent. Unitless input is radians; degree input is converted to radians; result is unitless. |
| 0x7D | `Asin` | - | result register | `XSlot`=operand | - | - | Inverse sine for unitless numeric input in `[-1, 1]`; result is radians. |
| 0x7E | `Acos` | - | result register | `XSlot`=operand | - | - | Inverse cosine for unitless numeric input in `[-1, 1]`; result is radians. |
| 0x7F | `Atan` | - | result register | `XSlot`=operand | - | - | Inverse tangent for unitless numeric input; result is radians. |
| 0x80 | `Atan2` | - | result register | `XSlot`=y | `YSlot`=x | - | `atan2(y, x)` with matching units; result is radians. `(0, 0)` yields `0`. |
| 0x81 | `Hypot2D` | - | result register | `XSlot`=x | `YSlot`=y | - | 2D hypotenuse; matching units are retained in the result. |
| 0x82 | `Hypot3D` | - | result register | `XSlot`=x | `YSlot`=y | `AU`=z | 3D hypotenuse; matching units are retained in the result. |
| 0x83 | `Distance` | - | result register | `XSlot`=left | `YSlot`=right | - | Scalar distance or 3D vector/point distance; matching units are retained. |
| 0x84 | `Distance2D` | - | result register | `XSlot`=x1 | `YSlot`=y1 | `AU`=x2, `BU`=y2 | 2D coordinate distance; matching units are retained. |
| 0x85 | `Distance3D` | - | result register | `XSlot`=x1 | `YSlot`=y1 | `AU`=z1, `BU`=x2, `CU`=y2, `DU`=z2 | 3D coordinate distance; matching units are retained. |
| 0x86 | `DistanceSquared` | - | result register | `XSlot`=left | `YSlot`=right | - | Scalar or 3D vector/point squared distance; result is unitless. |
| 0x87 | `DistanceSquared2D` | - | result register | `XSlot`=x1 | `YSlot`=y1 | `AU`=x2, `BU`=y2 | 2D squared coordinate distance; result is unitless. |
| 0x88 | `DistanceSquared3D` | - | result register | `XSlot`=x1 | `YSlot`=y1 | `AU`=z1, `BU`=x2, `CU`=y2, `DU`=z2 | 3D squared coordinate distance; result is unitless. |
| 0x89 | `LengthSquared` | - | result register | `XSlot`=operand | - | - | Scalar square or vector/point squared length; result is unitless. |
| 0x8A | `LengthSquared2D` | - | result register | `XSlot`=x | `YSlot`=y | - | 2D squared length; result is unitless. |
| 0x8B | `LengthSquared3D` | - | result register | `XSlot`=x | `YSlot`=y | `AU`=z | 3D squared length; result is unitless. |
| 0x8C | `Normalize` | - | result register | `XSlot`=operand | - | - | Normalizes a 3D vector/point and returns a unitless vector; zero length yields `nothing`. |
| 0x8D | `Normalize2D` | - | result register | `XSlot`=x | `YSlot`=y | - | Normalizes 2D coordinates and returns a unitless vector with `z=0`; zero length yields `nothing`. |
| 0x8E | `Normalize3D` | - | result register | `XSlot`=x | `YSlot`=y | `AU`=z | Normalizes 3D coordinates and returns a unitless vector; zero length yields `nothing`. |
| 0x8F | `Dot` | - | result register | `XSlot`=left | `YSlot`=right | - | 3D vector/point dot product with matching units; result is unitless. |
| 0x90 | `Dot2D` | - | result register | `XSlot`=x1 | `YSlot`=y1 | `AU`=x2, `BU`=y2 | 2D coordinate dot product; result is unitless. |
| 0x91 | `Dot3D` | - | result register | `XSlot`=x1 | `YSlot`=y1 | `AU`=z1, `BU`=x2, `CU`=y2, `DU`=z2 | 3D coordinate dot product; result is unitless. |
| 0x92 | `Cross` | - | result register | `XSlot`=left | `YSlot`=right | - | 3D vector/point cross product with matching units; result is a unitless vector. |
| 0x93 | `Cross2D` | - | result register | `XSlot`=x1 | `YSlot`=y1 | `AU`=x2, `BU`=y2 | 2D coordinate cross product; result is the scalar z component, unitless. |
| 0x94 | `Cross3D` | - | result register | `XSlot`=x1 | `YSlot`=y1 | `AU`=z1, `BU`=x2, `CU`=y2, `DU`=z2 | 3D coordinate cross product; result is a unitless vector. |
| 0x95 | `AngleBetween` | - | result register | `XSlot`=left | `YSlot`=right | - | 3D vector/point angle in radians; zero length yields `nothing`. |
| 0x96 | `AngleBetween2D` | - | result register | `XSlot`=x1 | `YSlot`=y1 | `AU`=x2, `BU`=y2 | 2D coordinate angle in radians; zero length yields `nothing`. |
| 0x97 | `AngleBetween3D` | - | result register | `XSlot`=x1 | `YSlot`=y1 | `AU`=z1, `BU`=x2, `CU`=y2, `DU`=z2 | 3D coordinate angle in radians; zero length yields `nothing`. |
| 0x98..0x9F | reserved | - | - | - | - | - | Reserved tail of Group 2 after math/navigation expansion. |

### Group 3 - Text, Collections, Iterators

| Hex | Opcode | UnitAndFlags | DestinationSlot | X | Y | Payload | Notes |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 0xA0 | `TakeFirst` | - | result register | `XSlot`=source | `ImmediateY`=count | - | Takes the first `Y` values from a series, list, dice, range, or iterator source. |
| 0xA1 | `DropFirst` | - | result register | `XSlot`=source | `ImmediateY`=count | - | Drops the first `Y` values from a series, list, dice, range, or iterator source. |
| 0xA2 | `TakeLast` | - | result register | `XSlot`=source | `ImmediateY`=count | - | Takes the last `Y` values from a finite list, dice, range, or iterator source; series yields `nothing`. |
| 0xA3 | `DropLast` | - | result register | `XSlot`=source | `ImmediateY`=count | - | Drops the last `Y` values from a finite list, dice, range, or iterator source; series yields `nothing`. |
| 0xA4 | `TakeHighest` | - | result register | `XSlot`=source | `ImmediateY`=count | - | Takes the highest `Y` values from a finite list, dice, range, or iterator source; series yields `nothing`. |
| 0xA5 | `TakeLowest` | - | result register | `XSlot`=source | `ImmediateY`=count | - | Takes the lowest `Y` values from a finite list, dice, range, or iterator source; series yields `nothing`. |
| 0xA6 | `DropHighest` | - | result register | `XSlot`=source | `ImmediateY`=count | - | Drops the highest `Y` values from a finite list, dice, range, or iterator source; series yields `nothing`. |
| 0xA7 | `DropLowest` | - | result register | `XSlot`=source | `ImmediateY`=count | - | Drops the lowest `Y` values from a finite list, dice, range, or iterator source; series yields `nothing`. |
| 0xA8 | `OneRandom` | - | result register | `XSlot`=source | - | - | Chooses one random element from a finite list, dice, range, or iterator source; empty/invalid/series sources yield `nothing`. |
| 0xA9 | `TakeRandom` | - | result register | `XSlot`=source | `ImmediateY`=count | - | Chooses up to `Y` random elements without replacement. Lists stay lists, dice stay dice, ranges and iterators materialize as lists. |
| 0xAA | `OneWeighted` | - | result register | `XSlot`=items list | `YSlot`=weights list | - | Selects one item using positive finite weights. Empty/no-positive-weight lists -> `nothing`. |
| 0xAB | `TakeWeighted` | - | result register | `XSlot`=items list | `ImmediateY`=count | `AU`=weights list register | Selects up to `Y` items without replacement using positive finite weights. Result is a list. |
| 0xAC | `Count` | - | result register | `XSlot`=source | - | - | Counts collection/string/range/map items or consumes an iterator to count. |
| 0xAD | `StartsWith` | - | result register | `XSlot`=left | `YSlot`=right | - | Text/tag raw-text prefix check or list/dice/range sequence prefix check. |
| 0xAE | `EndsWith` | - | result register | `XSlot`=left | `YSlot`=right | - | Text/tag raw-text suffix check or list/dice/range sequence suffix check. |
| 0xAF | `Contains` | - | result register | `XSlot`=needle | `YSlot`=container | - | Text/tag substring, map key, list/dice/range membership, or vector/point component membership. |
| 0xB0 | `ContainsAny` | - | result register | `XSlot`=needles | `YSlot`=container | - | Tests whether the container contains any value from the needle sequence. Iterators are consumed until the first match or exhaustion. |
| 0xB1 | `ContainsAll` | - | result register | `XSlot`=needles | `YSlot`=container | - | Tests whether the container contains every value from the needle sequence. Iterators are consumed until all needles match or exhaustion. |
| 0xB2 | `HasAny` | - | result register | `XSlot`=source | - | - | Truthiness `any` over list/dice/range/map values/text/tag/vector/point or iterator sources. Empty -> `false`; `nothing` -> `nothing`. |
| 0xB3 | `HasAll` | - | result register | `XSlot`=source | - | - | Truthiness `all` over list/dice/range/map values/text/tag/vector/point or iterator sources. Empty -> `true`; `nothing` -> `nothing`. |
| 0xB4 | `ContainsValue` | - | result register | `XSlot`=needle | `YSlot`=container | - | Value membership for map-like containers, vectors, and points. |
| 0xB5 | `Union` | - | result register | `XSlot`=left | `YSlot`=right | - | Collection union/merge for list, dice, and map shapes; invalid shapes -> `nothing`. |
| 0xB6 | `Intersect` | - | result register | `XSlot`=left | `YSlot`=right | - | Map key intersection or list/dice multiset intersection; invalid shapes -> `nothing`. |
| 0xB7 | `Zip` | - | result register | `XSlot`=left | `YSlot`=right | - | List zip into `{ left, right }` maps up to the shorter length; invalid shapes -> `nothing`. |
| 0xB8 | `KeysOfMap` | - | result register | `XSlot`=operand | - | - | Map/custom-type keys projection; `nothing` and non-map operands produce `nothing`. |
| 0xB9 | `ValuesOfMap` | - | result register | `XSlot`=operand | - | - | Map/custom-type values projection; `nothing` and non-map operands produce `nothing`. |
| 0xBA | `EntriesOfMap` | - | result register | `XSlot`=operand | - | - | Map/custom-type entries projection; `nothing` and non-map operands produce `nothing`. |
| 0xBB | `First` | - | result register | `XSlot`=source | - | - | Returns the first element from list, dice, range, map/custom values, text/tag, or iterator; invalid/empty sources -> `nothing`. |
| 0xBC | `Last` | - | result register | `XSlot`=source | - | - | Returns the last element from list, dice, range, map/custom values, text/tag, or iterator; invalid/empty sources -> `nothing`. |
| 0xBD | `Single` | - | result register | `XSlot`=source | - | - | Returns the only element from list, dice, range, map/custom values, text/tag, or iterator; invalid/empty/multiple-element sources -> `nothing`. |
| 0xBE | `IteratorCreate` | - | iterator register | `XSlot`=collection | - | - | Creates a VM-internal iterator over a collection or range value. Non-iterable sources write `nothing`. |
| 0xBF | `IteratorCreateOrJump` | - | iterator register | `XSlot`=collection | `TargetAddress`=not-iterable | - | Creates a VM-internal iterator, or writes `nothing` and jumps to `Y` when no iterator can be created. |
| 0xC0 | `IteratorNext` | - | item register | `XSlot`=iterator | `TargetAddress`=no-more | - | Writes the next item and continues, or jumps to `Y` when exhausted. |
| 0xC1 | `IteratorClose` | - | - | `XSlot`=iterator | - | - | Disposes/closes a VM-internal iterator. |
| 0xC2 | `Distinct` | - | result register | `XSlot`=source | - | - | Materializes distinct source items in source order. Supports direct collection fast paths and iterators. |
| 0xC3 | `SortAscending` | - | result register | `XSlot`=source | - | - | Sorts source items ascending. Supports direct list, dice, range, and iterator sources. |
| 0xC4 | `SortDescending` | - | result register | `XSlot`=source | - | - | Sorts source items descending. Supports direct list, dice, range, and iterator sources. |
| 0xC5 | `Reverse` | - | result register | `XSlot`=source | - | - | Reverses list, dice, range, or iterator sources. Dice and iterators materialize lists; ranges stay ranges. |
| 0xC6 | `Shuffle` | - | result register | `XSlot`=source | - | - | Shuffles list, dice, range, or iterator sources. Result is a list. |
| 0xC7 | `ListBuilderCreate` | - | builder register | - | - | - | Creates a VM-internal list builder for generated collections. |
| 0xC8 | `ListBuilderAdd` | - | - | `XSlot`=builder | `YSlot`=item | - | Adds an item and checks `MaxGeneratedCollectionItems`. |
| 0xC9 | `ListBuilderFinish` | - | result register | `XSlot`=builder | - | - | Materializes the list builder as a list. |
| 0xCA | `MapBuilderCreate` | - | builder register | - | - | - | Creates a VM-internal map builder for generated map projections. |
| 0xCB | `MapBuilderAdd` | - | - | `XSlot`=builder | `YSlot`=key | `AU`=value register | Adds or overwrites a map entry; empty/nothing keys are skipped. |
| 0xCC | `MapBuilderFinish` | - | result register | `XSlot`=builder | - | - | Materializes the map builder as a map. |
| 0xCD | `DistinctBuilderCreate` | - | builder register | - | - | - | Creates a VM-internal builder for `distinct by` loop lowering. |
| 0xCE | `DistinctBuilderAdd` | - | - | `XSlot`=builder | `YSlot`=key | `AU`=value register | Adds the value only when the key was not seen before. |
| 0xCF | `DistinctBuilderFinish` | - | result register | `XSlot`=builder | - | - | Materializes the distinct-by builder as a list. |
| 0xD0 | `GroupBuilderCreate` | - | builder register | - | - | - | Creates a VM-internal builder for `group by` loop lowering. |
| 0xD1 | `GroupBuilderAdd` | - | - | `XSlot`=builder | `YSlot`=key | `AU`=value register | Adds the value to the group identified by the key text. |
| 0xD2 | `GroupBuilderFinish` | - | result register | `XSlot`=builder | - | - | Materializes the group builder as a map from keys to grouped lists. |
| 0xD3 | `OrderBuilderCreate` | - | builder register | - | - | - | Creates a VM-internal builder for `order by` loop lowering. |
| 0xD4 | `OrderBuilderAdd` | - | - | `XSlot`=builder | `YSlot`=key | `AU`=value register | Adds a key/value pair preserving source order for stable ordering. |
| 0xD5 | `OrderBuilderFinishAscending` | - | result register | `XSlot`=builder | - | - | Sorts by stored keys ascending and materializes the ordered values as a list. |
| 0xD6 | `OrderBuilderFinishDescending` | - | result register | `XSlot`=builder | - | - | Sorts by stored keys descending and materializes the ordered values as a list. |
| 0xD7 | `HasPattern` | - | result register | `XSlot`=source/iterator | `ImmediateY`=count for count patterns | `AU`=pattern kind, `BU`=face register for `CountFace` | Tests a dice/card pattern and returns boolean. |
| 0xD8 | `TakePattern` | - | result register | `XSlot`=source/iterator | `ImmediateY`=count for count patterns | `AU`=pattern kind, `BU`=face register for `CountFace` | Takes items matching a dice/card pattern. Dice sources produce dice; list sources produce lists. |
| 0xD9..0xFF | reserved | - | - | - | - | - | Reserved tail of Group 3 for future collection, iterator, pipeline, extension, or VM opcodes. |

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
