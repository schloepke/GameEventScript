// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace GameEventScript.Api;

/// <summary>
/// Identifies the role of a <see cref="GesBindingSegment"/> entry: an executable declaration, an outbound message signature
/// or a host import.
/// </summary>
public enum GesBinaryBindKind : byte
{
    /// <summary>
    /// Script handler matching a name and ordered argument labels.
    /// </summary>
    MessageHandler = 0x10,
    /// <summary>
    /// Script handler matching a message name regardless of argument labels.
    /// </summary>
    MessageNameHandler = 0x11,
    /// <summary>
    /// Script function returning a value.
    /// </summary>
    Function = 0x12,
    /// <summary>
    /// Script predicate returning a Boolean result.
    /// </summary>
    Predicate = 0x13,
    /// <summary>
    /// Import resolved against the host extension registry.
    /// </summary>
    ExtensionCall = 0x20,
    /// <summary>
    /// Message signature used by emit or publish instructions.
    /// </summary>
    OutboundMessage = 0x21,
    /// <summary>
    /// Script record constructor.
    /// </summary>
    Record = 0x30,
    /// <summary>
    /// Import resolved against the host external-type registry.
    /// </summary>
    ExternalType = 0x31
}

/// <summary>
/// Instruction flags stored in bits 5 to 7 of <see cref="GesBytecodeInstruction.UnitAndFlags"/>. Their meaning is
/// opcode-specific, so several flags share a bit.
/// </summary>
[Flags]
internal enum GesInstructionFlag : byte
{
    /// <summary>
    /// No flag is set.
    /// </summary>
    None = 0,
    /// <summary>
    /// Normalizes the result of a call to a Boolean or <c>nothing</c>, as a predicate result.
    /// </summary>
    NormalizeResultAsPredicate = 0x20,
    /// <summary>Selects the strict numeric view for Move and scalar arithmetic for binary arithmetic, Min, Max, Negate, Abs and Clamp.</summary>
    Numeric = 0x40,
    /// <summary>Makes ParseLiteral write Nothing instead of the original Text when recognition fails.</summary>
    Strict = 0x40,
    /// <summary>Supplies an additional tag register list to a result-bearing send instruction.</summary>
    WithTags = 0x40,
    /// <summary>Reads a Message value rather than an outbound binding in a result-bearing send instruction.</summary>
    Indirect = 0x80
}

/// <summary>
/// Identifies portable value kinds and VM-internal storage kinds used as bytecode type operands.
/// </summary>
public enum GesBytecodeTypeKind : byte
{
    /// <summary>
    /// Absence of a value.
    /// </summary>
    Nothing = 0x00,
    /// <summary>
    /// Boolean value.
    /// </summary>
    Boolean = 0x02,
    /// <summary>
    /// Exact signed Int64 number.
    /// </summary>
    Integer = 0x03,
    /// <summary>
    /// IEEE 754 binary64 number.
    /// </summary>
    Float = 0x04,
    /// <summary>
    /// Binary64 ratio carrying Percentage kind.
    /// </summary>
    Percentage = 0x05,
    /// <summary>
    /// Validated tag name.
    /// </summary>
    Tag = 0x06,
    /// <summary>
    /// Unicode text.
    /// </summary>
    Text = 0x07,
    /// <summary>
    /// Three-coordinate vector.
    /// </summary>
    Vector = 0x08,
    /// <summary>
    /// Three-coordinate point.
    /// </summary>
    Point = 0x09,
    /// <summary>
    /// Lazy numeric range.
    /// </summary>
    Range = 0x0a,
    /// <summary>
    /// Identifies an immutable sequence of bytes.
    /// </summary>
    Bytes = 0x0b,

    /// <summary>
    /// Message-handler signature.
    /// </summary>
    Handler = 0x10,
    /// <summary>
    /// Message signature, arguments and tags.
    /// </summary>
    Message = 0x11,

    /// <summary>
    /// Ordered immutable value collection.
    /// </summary>
    List = 0x20,
    /// <summary>
    /// Descending Int32 dice rolls.
    /// </summary>
    Dice = 0x21,
    /// <summary>
    /// Canonical Text-keyed value map.
    /// </summary>
    Map = 0x22,
    /// <summary>
    /// Internal mutable list construction state.
    /// </summary>
    ListBuilder = 0x23,
    /// <summary>
    /// Internal mutable map construction state.
    /// </summary>
    MapBuilder = 0x24,
    /// <summary>
    /// Internal distinct-value construction state.
    /// </summary>
    DistinctBuilder = 0x25,
    /// <summary>
    /// Internal grouping construction state.
    /// </summary>
    GroupBuilder = 0x26,
    /// <summary>
    /// Internal sorting construction state.
    /// </summary>
    OrderBuilder = 0x27,

    /// <summary>
    /// Internal collection iteration state.
    /// </summary>
    Iterator = 0x30,
    /// <summary>
    /// Lazy built-in numeric series.
    /// </summary>
    Series = 0x31,

    /// <summary>
    /// Record or host-defined external value.
    /// </summary>
    Custom = 0xFF,
}

/// <summary>
/// Identifies the dice pattern of <see cref="GesBytecodeOpCode.HasPattern"/> and <see cref="GesBytecodeOpCode.TakePattern"/>.
/// </summary>
public enum GesBytecodePatternKind : ushort
{
    /// <summary>
    /// Count a matching group of any face.
    /// </summary>
    CountAny = 0x00,
    /// <summary>
    /// Count occurrences of a specified face.
    /// </summary>
    CountFace = 0x01,
    /// <summary>
    /// Match a full-house pattern.
    /// </summary>
    FullHouse = 0x02,
    /// <summary>
    /// Match a consecutive-face pattern.
    /// </summary>
    Straight = 0x03,
}

/// <summary>
/// Identifies a built-in lazy numeric series for <see cref="GesBytecodeOpCode.CreateSeries"/>.
/// </summary>
public enum GesBytecodeSeriesKind : ushort
{
    /// <summary>
    /// The Fibonacci series.
    /// </summary>
    Fibonacci = 0x01,
    /// <summary>
    /// The factorial series.
    /// </summary>
    Factorial = 0x02,
}

/// <summary>
/// One logical bytecode instruction: an opcode, a unit-and-flags byte, three 16-bit operand words and a 64-bit payload,
/// encoded as 16 bytes in <c>.gesb</c>. The named properties are opcode-specific views of the same words; the
/// in-memory layout does not define the encoding.
/// </summary>
[StructLayout(LayoutKind.Sequential, Size = 16)]
public struct GesBytecodeInstruction
{
    private const byte UnitMask = 0x1F;
    private const byte InstructionFlagMask = 0xE0;

    /// <summary>
    /// The operation, which determines how the operands are interpreted.
    /// </summary>
    public GesBytecodeOpCode OpCode;
    /// <summary>
    /// The quantity unit ID in bits 0 to 4 and opcode-specific instruction flags in bits 5 to 7.
    /// </summary>
    public byte UnitAndFlags;
    private ushort _word0;
    private ushort _word1;
    private ushort _word2;
    /// <summary>
    /// The raw 64-bit payload: an inline literal or four packed 16-bit operands.
    /// </summary>
    public ulong Payload;

    /// <summary>
    /// Gets or sets the register that receives the result; the first operand word.
    /// </summary>
    public ushort DestinationRegister { readonly get => _word0; set => _word0 = value; }
    /// <summary>
    /// Gets or sets the outbound message binding ID of a send instruction; the first operand word.
    /// </summary>
    public ushort MessageDestination { readonly get => _word0; set => _word0 = value; }

    /// <summary>
    /// Gets or sets a signed 16-bit immediate, such as a range start or a vector start form; the second operand word.
    /// </summary>
    public short ImmediateX { readonly get => unchecked((short)_word1); set => _word1 = unchecked((ushort)value); }
    /// <summary>
    /// Gets or sets the register a conditional branch tests; the second operand word.
    /// </summary>
    public ushort ConditionRegister { readonly get => _word1; set => _word1 = value; }
    /// <summary>
    /// Gets or sets the first source register; the second operand word.
    /// </summary>
    public ushort XRegister { readonly get => _word1; set => _word1 = value; }
    /// <summary>
    /// Gets or sets a string-constant index, such as a literal or member name; the second operand word.
    /// </summary>
    public ushort StringIndex { readonly get => _word1; set => _word1 = value; }
    /// <summary>
    /// Gets or sets a second index-list index, such as a tag register list or message shape; the second operand word.
    /// </summary>
    public ushort SecondaryListIndex { readonly get => _word1; set => _word1 = value; }
    /// <summary>
    /// Gets or sets a binding ID, such as an extension, record or external type binding; the second operand word.
    /// </summary>
    public ushort BindId { readonly get => _word1; set => _word1 = value; }
    /// <summary>
    /// Gets or sets the 1-based element index of <see cref="GesBytecodeOpCode.IndexAccess"/>; the second operand word.
    /// </summary>
    public ushort Index { readonly get => _word1; set => _word1 = value; }
    /// <summary>
    /// Gets or sets the signed local register delta of <see cref="GesBytecodeOpCode.RegisterLocals"/>; the second operand word.
    /// </summary>
    public short Count { readonly get => unchecked((short)_word1); set => _word1 = unchecked((ushort)value); }

    /// <summary>
    /// Gets or sets a second signed 16-bit immediate, such as a range end or a pattern count; the third operand word.
    /// </summary>
    public short ImmediateY { readonly get => unchecked((short)_word2); set => _word2 = unchecked((ushort)value); }
    /// <summary>
    /// Gets or sets the code address a branch or call jumps to; the third operand word.
    /// </summary>
    public ushort TargetAddress { readonly get => _word2; set => _word2 = value; }
    /// <summary>
    /// Gets or sets the second source register; the third operand word.
    /// </summary>
    public ushort YRegister { readonly get => _word2; set => _word2 = value; }
    /// <summary>
    /// Gets or sets the entry address of a called function, predicate or helper routine; the third operand word.
    /// </summary>
    public ushort EntryAddress { readonly get => _word2; set => _word2 = value; }
    /// <summary>
    /// Gets or sets a second string-constant index, such as the type name of a custom cast or check; the third operand word.
    /// </summary>
    public ushort SecondaryStringIndex { readonly get => _word2; set => _word2 = value; }
    /// <summary>
    /// Gets or sets an index-list index, such as an argument register list; the third operand word.
    /// </summary>
    public ushort ListIndex { readonly get => _word2; set => _word2 = value; }
    /// <summary>
    /// Gets or sets the type operand: a type kind, a series kind or a type-name string index; the third operand word.
    /// </summary>
    public ushort TypeOperand { readonly get => _word2; set => _word2 = value; }
    /// <summary>
    /// Gets or sets the type operand as a built-in <see cref="GesBytecodeTypeKind"/>; the third operand word.
    /// </summary>
    public GesBytecodeTypeKind TypeKind { readonly get => (GesBytecodeTypeKind)_word2; set => _word2 = (ushort)value; }

    /// <summary>
    /// Gets or sets payload bits 0 to 15 as an unsigned operand.
    /// </summary>
    public ushort AU { readonly get => (ushort)Payload; set => Payload = (Payload & 0xFFFFFFFFFFFF0000UL) | value; }
    /// <summary>
    /// Gets or sets payload bits 0 to 15 as a signed operand.
    /// </summary>
    public short AS { readonly get => unchecked((short)AU); set => AU = unchecked((ushort)value); }
    /// <summary>
    /// Gets or sets payload bits 16 to 31 as an unsigned operand.
    /// </summary>
    public ushort BU { readonly get => (ushort)(Payload >> 16); set => Payload = (Payload & 0xFFFFFFFF0000FFFFUL) | ((ulong)value << 16); }
    /// <summary>
    /// Gets or sets payload bits 16 to 31 as a signed operand.
    /// </summary>
    public short BS { readonly get => unchecked((short)BU); set => BU = unchecked((ushort)value); }
    /// <summary>
    /// Gets or sets payload bits 32 to 47 as an unsigned operand.
    /// </summary>
    public ushort CU { readonly get => (ushort)(Payload >> 32); set => Payload = (Payload & 0xFFFF0000FFFFFFFFUL) | ((ulong)value << 32); }
    /// <summary>
    /// Gets or sets payload bits 32 to 47 as a signed operand.
    /// </summary>
    public short CS { readonly get => unchecked((short)CU); set => CU = unchecked((ushort)value); }
    /// <summary>
    /// Gets or sets payload bits 48 to 63 as an unsigned operand.
    /// </summary>
    public ushort DU { readonly get => (ushort)(Payload >> 48); set => Payload = (Payload & 0x0000FFFFFFFFFFFFUL) | ((ulong)value << 48); }
    /// <summary>
    /// Gets or sets payload bits 48 to 63 as a signed operand.
    /// </summary>
    public short DS { readonly get => unchecked((short)DU); set => DU = unchecked((ushort)value); }

    /// <summary>
    /// Gets or sets the payload bits as a signed 64-bit integer literal.
    /// </summary>
    public long I64 { readonly get => unchecked((long)Payload); set => Payload = unchecked((ulong)value); }
    /// <summary>
    /// Gets or sets the payload bits as an IEEE 754 binary64 literal.
    /// </summary>
    public double F64
    {
        readonly get => BitConverter.Int64BitsToDouble(unchecked((long)Payload));
        set => Payload = unchecked((ulong)BitConverter.DoubleToInt64Bits(value));
    }

    internal GesQuantityUnit Unit => DecodeUnit(UnitAndFlags);

    internal GesInstructionFlag InstructionFlags => DecodeInstructionFlags(UnitAndFlags);

    internal bool HasInstructionFlag(GesInstructionFlag flag)
        => HasInstructionFlag(UnitAndFlags, flag);

    internal void AddInstructionFlag(GesInstructionFlag flag)
        => UnitAndFlags = WithInstructionFlag(UnitAndFlags, flag);

    internal static byte EncodeUnitAndFlags(GesQuantityUnit? unit, GesInstructionFlag flags = GesInstructionFlag.None)
        => EncodeUnitAndFlags(unit.ToStoredUnit(), flags);

    internal static byte EncodeUnitAndFlags(GesQuantityUnit unit, GesInstructionFlag flags = GesInstructionFlag.None)
        => (byte)(((byte)unit.ToStoredUnit() & UnitMask) | ((byte)flags & InstructionFlagMask));

    internal static GesQuantityUnit DecodeUnit(byte unitAndFlags)
        => ((GesQuantityUnit)(unitAndFlags & UnitMask)).ToStoredUnit();

    internal static GesInstructionFlag DecodeInstructionFlags(byte unitAndFlags)
        => (GesInstructionFlag)(unitAndFlags & InstructionFlagMask);

    internal static bool HasInstructionFlag(byte unitAndFlags, GesInstructionFlag flag)
        => (unitAndFlags & (byte)flag) != 0;

    internal static byte WithInstructionFlag(byte unitAndFlags, GesInstructionFlag flag)
        => (byte)(unitAndFlags | ((byte)flag & InstructionFlagMask));

    internal static byte WithoutInstructionFlags(byte unitAndFlags)
        => (byte)(unitAndFlags & UnitMask);
}

/// <summary>
/// Identifies a V1 bytecode instruction. The numeric values are part of the <c>.gesb</c> format; operand layouts and
/// semantics are defined in the bytecode specification.
/// </summary>
public enum GesBytecodeOpCode : byte
{
    #region Group 1 - control, calls, messages, types, values

    /// <summary>
    /// No operation.
    /// </summary>
    Nop = 0x00,
    /// <summary>
    /// Adds <c>Count &gt; 0</c> active local registers or releases <c>-Count</c> registers when <c>Count &lt; 0</c>.
    /// Entry prologs reserve only locals beyond preloaded arguments.
    /// </summary>
    RegisterLocals = 0x01,
    /// <summary>
    /// Unconditional branch.
    /// </summary>
    Jump = 0x02,
    /// <summary>
    /// Branches when <c>X.IsTrue()</c>.
    /// </summary>
    JumpIfTrue = 0x03,
    /// <summary>
    /// Branches when <c>X.IsFalse()</c>.
    /// </summary>
    JumpIfFalse = 0x04,
    /// <summary>
    /// Branches when <c>!X.IsTrue()</c>, including <c>nothing</c>.
    /// </summary>
    JumpIfNotTrue = 0x05,
    /// <summary>
    /// Branches when <c>X.Kind</c> is <c>Nothing</c>.
    /// </summary>
    JumpIfNothing = 0x06,

    /// <summary>
    /// Enters a VM-owned local call frame at a known code address. Arguments are the contiguous staged sequence
    /// immediately before the call. With <c>NormalizeResultAsPredicate</c>, the returned value is normalized to boolean
    /// or <c>nothing</c>.
    /// </summary>
    Call = 0x07,
    /// <summary>
    /// Creates a built-in mathematical series. Supported kinds are <c>Fibonacci</c> and <c>Factorial</c>.
    /// </summary>
    CreateSeries = 0x08,
    /// <summary>
    /// Calls a dynamically bound host extension. With <c>NormalizeResultAsPredicate</c>, the result is normalized to
    /// boolean or <c>nothing</c>.
    /// </summary>
    CallExternal = 0x09,

    /// <summary>
    /// Returns no value from the current frame; normal calls map this to DSL <c>nothing</c>.
    /// </summary>
    ReturnVoid = 0x0A,
    /// <summary>
    /// Returns the value in <c>X</c> from the current frame.
    /// </summary>
    ReturnValue = 0x0B,

    /// <summary>
    /// Emits a statically shaped message without tags.
    /// </summary>
    EmitMessage = 0x0C,
    /// <summary>
    /// Emits a statically shaped message with tags.
    /// </summary>
    EmitMessageWithTags = 0x0D,
    /// <summary>
    /// Emits a dynamic message value without tags.
    /// </summary>
    EmitMessageValue = 0x0E,
    /// <summary>
    /// Emits a dynamic message value with tags.
    /// </summary>
    EmitMessageValueWithTags = 0x0F,

    /// <summary>
    /// Publishes a statically shaped message without tags.
    /// </summary>
    PublishMessage = 0x10,
    /// <summary>
    /// Publishes a statically shaped message with tags.
    /// </summary>
    PublishMessageWithTags = 0x11,
    /// <summary>
    /// Publishes a dynamic message value without tags.
    /// </summary>
    PublishMessageValue = 0x12,
    /// <summary>
    /// Publishes a dynamic message value with tags.
    /// </summary>
    PublishMessageValueWithTags = 0x13,

    /// <summary>
    /// Converts <c>X</c> to the declared built-in type. Custom/record types use <c>CastCustom</c>. <c>Cast :Tag</c>
    /// validates tag syntax; invalid tag text writes <c>nothing</c>. <c>Cast :Vector</c>/<c>:Point</c> structurally
    /// convert between vectors and points by copying components and unit.
    /// </summary>
    Cast = 0x14,
    /// <summary>
    /// Converts <c>X</c> to a custom/record type identified by <c>Y</c>.
    /// </summary>
    CastCustom = 0x15,
    /// <summary>
    /// Converts <c>X</c> to the numeric unit carried in <c>UnitAndFlags</c>. Units are not represented as declared type
    /// kinds.
    /// </summary>
    CastUnit = 0x16,
    /// <summary>
    /// Coerces <c>X</c> through the source-level <c>:Number</c> type, including explicit numeric text parsing; invalid
    /// text writes <c>nothing</c>. Integral values stay integer; otherwise the result is float.
    /// </summary>
    CastNumeric = 0x17,

    /// <summary>
    /// Writes whether <c>X</c> has the declared built-in type. Custom/record types use <c>CheckCustomType</c>.
    /// </summary>
    CheckType = 0x18,
    /// <summary>
    /// Writes whether <c>X</c> has the custom/record type identified by <c>Y</c>.
    /// </summary>
    CheckCustomType = 0x19,
    /// <summary>
    /// Writes whether <c>X</c> has the numeric unit carried in <c>UnitAndFlags</c>. Units are not represented as
    /// declared type kinds.
    /// </summary>
    CheckUnit = 0x1A,
    /// <summary>
    /// Writes whether <c>X</c> has a runtime numeric view. Numbers, percentages, booleans (<c>false</c> = <c>0</c>,
    /// <c>true</c> = <c>1</c>), and dice sums are numeric. Text and tags are not parsed here.
    /// </summary>
    CheckNumeric = 0x1B,
    /// <summary>
    /// Writes whether <c>X</c> has a finite integral numeric view. Booleans and dice are integer. Text is not parsed
    /// here.
    /// </summary>
    CheckInteger = 0x1C,
    /// <summary>
    /// Writes whether <c>X</c> has a finite non-integral numeric view. Text is not parsed here.
    /// </summary>
    CheckFractional = 0x1D,

    /// <summary>
    /// Copies a register value/reference; the source register remains unchanged.
    /// </summary>
    Move = 0x1E,
    /// <summary>
    /// Reads a named member.
    /// </summary>
    MemberAccess = 0x1F,
    /// <summary>
    /// Reads a statically known positional element.
    /// </summary>
    IndexAccess = 0x20,
    /// <summary>
    /// Reads a dynamic property: integer selectors use index semantics; text/tag selectors use member semantics.
    /// </summary>
    PropertyAccess = 0x21,
    /// <summary>
    /// Binds ordered argument values to a handler signature. Argument names come from the signature.
    /// </summary>
    BindHandler = 0x22,

    /// <summary>
    /// Loads <c>nothing</c>.
    /// </summary>
    LoadNothing = 0x23,
    /// <summary>
    /// Loads boolean <c>true</c>.
    /// </summary>
    LoadTrue = 0x24,
    /// <summary>
    /// Loads boolean <c>false</c>.
    /// </summary>
    LoadFalse = 0x25,
    /// <summary>
    /// Loads an inline signed <c>Int64</c>.
    /// </summary>
    LoadInteger = 0x26,
    /// <summary>
    /// Loads an inline IEEE-754 <c>Float64</c>.
    /// </summary>
    LoadFloat = 0x27,
    /// <summary>
    /// Loads an inline percentage ratio as the dedicated percentage value kind.
    /// </summary>
    LoadPercentage = 0x28,
    /// <summary>
    /// Loads a text literal.
    /// </summary>
    LoadText = 0x29,
    /// <summary>
    /// Loads a tag literal.
    /// </summary>
    LoadTag = 0x2A,
    /// <summary>
    /// Loads a handler literal. The shape list is <c>[messageNameStringIndex, argumentNameStringIndex...]</c>.
    /// </summary>
    LoadHandler = 0x2B,
    /// <summary>
    /// Loads a statically shaped message value. Shape is <c>[messageNameStringIndex, argumentNameStringIndex...]</c>.
    /// </summary>
    LoadMessage = 0x2C,

    /// <summary>
    /// Stages a register value for the next stage-consuming instruction.
    /// </summary>
    StageRegister = 0x2D,
    /// <summary>
    /// Stages DSL <c>nothing</c> for the next stage-consuming instruction.
    /// </summary>
    StageNothing = 0x2E,
    /// <summary>
    /// Stages <c>true</c> for the next stage-consuming instruction.
    /// </summary>
    StageTrue = 0x2F,
    /// <summary>
    /// Stages <c>false</c> for the next stage-consuming instruction.
    /// </summary>
    StageFalse = 0x30,
    /// <summary>
    /// Stages an inline integer value.
    /// </summary>
    StageInteger = 0x31,
    /// <summary>
    /// Stages an inline float value.
    /// </summary>
    StageFloat = 0x32,
    /// <summary>
    /// Stages a text literal from <c>StringPool</c>.
    /// </summary>
    StageText = 0x33,
    /// <summary>
    /// Stages a tag literal from <c>StringPool</c>.
    /// </summary>
    StageTag = 0x34,
    /// <summary>
    /// Stages an inline percentage ratio value.
    /// </summary>
    StagePercentage = 0x35,

    /// <summary>
    /// Creates a dice value; <c>X</c> and <c>Y</c> are not registers.
    /// </summary>
    CreateDice = 0x36,
    /// <summary>
    /// Creates a vector from the staged components. ImmediateX is 0 for the positional form starting at x, whose first
    /// component may lift a Vector or Point, 1 for labeled y, 2 for labeled z, or 3 for labeled x, which never lifts.
    /// </summary>
    CreateVector = 0x37,
    /// <summary>
    /// Creates a point from the staged components. ImmediateX is 0 for the positional form starting at x, whose first
    /// component may lift a Vector or Point, 1 for labeled y, 2 for labeled z, or 3 for labeled x, which never lifts.
    /// </summary>
    CreatePoint = 0x38,
    /// <summary>
    /// Creates a list from the contiguous staged value sequence immediately before the opcode.
    /// </summary>
    CreateList = 0x39,
    /// <summary>
    /// Creates a map from key names and the contiguous staged value sequence immediately before the opcode.
    /// </summary>
    CreateMap = 0x3A,
    /// <summary>
    /// Creates a range value with implicit step <c>1</c>.
    /// </summary>
    CreateRange = 0x3B,
    /// <summary>
    /// Creates a range value with explicit step.
    /// </summary>
    CreateRangeWithStep = 0x3C,
    /// <summary>
    /// Creates a VM-internal range iterator with default step <c>+1</c>.
    /// </summary>
    CreateRangeIterator = 0x3D,
    /// <summary>
    /// Creates a VM-internal range iterator with an explicit step.
    /// </summary>
    CreateRangeIteratorWithStep = 0x3E,
    /// <summary>
    /// Creates a compact literal range iterator.
    /// </summary>
    CreateRangeIteratorShort = 0x3F,
    /// <summary>
    /// Calls the record constructor bind with staged constructor-parameter values; computed fields are derived inside
    /// the constructor.
    /// </summary>
    CreateRecord = 0x40,
    /// <summary>
    /// Creates the concrete custom record value from the constructor-produced field map.
    /// </summary>
    CreateRecordValue = 0x41,
    /// <summary>
    /// Constructs a host-bound external type value from named staged argument values.
    /// </summary>
    CreateExternalType = 0x42,

    /// <summary>
    /// Semantic value check; exact complement of <c>IsEmpty</c>.
    /// </summary>
    HasValue = 0x43,
    /// <summary>
    /// Semantic emptiness check; true for <c>nothing</c>, <c>NaN</c>, and empty text/collections.
    /// </summary>
    IsEmpty = 0x44,
    /// <summary>
    /// Writes <c>X</c> when it has a value, otherwise <c>Y</c>.
    /// </summary>
    Default = 0x45,

    #endregion

    #region Group 2 - boolean algebra, comparison, math and random

    /// <summary>
    /// Combines <c>X</c> and <c>Y</c> with three-valued logical or, treating <c>nothing</c> as unknown.
    /// </summary>
    Or = 0x50,
    /// <summary>
    /// Combines <c>X</c> and <c>Y</c> with three-valued logical and, treating <c>nothing</c> as unknown.
    /// </summary>
    And = 0x51,
    /// <summary>
    /// Combines <c>X</c> and <c>Y</c> with three-valued exclusive or, treating <c>nothing</c> as unknown.
    /// </summary>
    Xor = 0x52,
    /// <summary>
    /// Combines antecedent <c>X</c> and consequent <c>Y</c> with three-valued implication.
    /// </summary>
    Implies = 0x53,
    /// <summary>
    /// Logical negation.
    /// </summary>
    Not = 0x54,
    /// <summary>
    /// Writes whether <c>X</c> equals <c>Y</c>.
    /// </summary>
    Equal = 0x55,
    /// <summary>
    /// Writes whether <c>X</c> differs from <c>Y</c>.
    /// </summary>
    NotEqual = 0x56,
    /// <summary>
    /// Compares whether <c>X</c> is less than <c>Y</c>.
    /// </summary>
    Less = 0x57,
    /// <summary>
    /// Compares whether <c>X</c> is greater than <c>Y</c>.
    /// </summary>
    Greater = 0x58,
    /// <summary>
    /// Compares whether <c>X</c> is less than or equal to <c>Y</c>.
    /// </summary>
    LessOrEqual = 0x59,
    /// <summary>
    /// Compares whether <c>X</c> is greater than or equal to <c>Y</c>.
    /// </summary>
    GreaterOrEqual = 0x5A,
    /// <summary>
    /// Adds <c>Y</c> to <c>X</c>; the <c>Numeric</c> flag selects scalar arithmetic.
    /// </summary>
    Add = 0x5B,
    /// <summary>
    /// Subtracts <c>Y</c> from <c>X</c>; the <c>Numeric</c> flag selects scalar arithmetic.
    /// </summary>
    Subtract = 0x5C,
    /// <summary>
    /// Multiplies <c>X</c> by <c>Y</c>; the <c>Numeric</c> flag selects scalar arithmetic.
    /// </summary>
    Multiply = 0x5D,
    /// <summary>
    /// Divides <c>X</c> by <c>Y</c>; the <c>Numeric</c> flag selects scalar arithmetic.
    /// </summary>
    Divide = 0x5E,
    /// <summary>
    /// Raises <c>X</c> to the power <c>Y</c>; the <c>Numeric</c> flag selects scalar arithmetic.
    /// </summary>
    Power = 0x5F,
    /// <summary>
    /// Floor-like integer division operation.
    /// </summary>
    IntegerDivide = 0x60,
    /// <summary>
    /// Numeric modulo operation.
    /// </summary>
    Modulo = 0x61,
    /// <summary>
    /// Numeric remainder operation.
    /// </summary>
    Remainder = 0x62,
    /// <summary>
    /// Writes the smaller of <c>X</c> and <c>Y</c>; also the reduce step of a minimum.
    /// </summary>
    Min = 0x63,
    /// <summary>
    /// Writes the larger of <c>X</c> and <c>Y</c>; also the reduce step of a maximum.
    /// </summary>
    Max = 0x64,
    /// <summary>
    /// Numeric negation.
    /// </summary>
    Negate = 0x65,
    /// <summary>
    /// Absolute value.
    /// </summary>
    Abs = 0x66,
    /// <summary>
    /// Natural logarithm.
    /// </summary>
    LogN = 0x67,
    /// <summary>
    /// Draws <c>true</c> with the probability given by <c>X</c> as a ratio or percentage; invalid input writes <c>nothing</c>.
    /// </summary>
    Chance = 0x68,
    /// <summary>
    /// The only opcode with three direct source registers.
    /// </summary>
    Clamp = 0x69,
    /// <summary>
    /// Takes an integer random value from integer bounds using the current random scope.
    /// </summary>
    RandomTake = 0x6A,
    /// <summary>
    /// Takes a float random value from numeric bounds using the current random scope.
    /// </summary>
    RandomTakeFloat = 0x6B,
    /// <summary>
    /// Saves the active random state and resets it from a valid dynamic unitless integer seed; an invalid seed retains
    /// the copied state.
    /// </summary>
    RandomPush = 0x6C,
    /// <summary>
    /// Saves the active random state and resets it from an inline signed <c>Int64</c>.
    /// </summary>
    RandomPushConstant = 0x6D,
    /// <summary>
    /// Restores the previous random scope.
    /// </summary>
    RandomPop = 0x6E,
    /// <summary>
    /// Reads a zero-based mathematical series term; non-series sources yield <c>nothing</c>.
    /// </summary>
    Term = 0x6F,
    /// <summary>
    /// Natural exponential. Unitless numeric input only; overflow to <c>+Infinity</c> is valid.
    /// </summary>
    Exp = 0x70,
    /// <summary>
    /// Floors a unitless numeric value and writes an integer.
    /// </summary>
    Floor = 0x71,
    /// <summary>
    /// Ceils a unitless numeric value and writes an integer.
    /// </summary>
    Ceil = 0x72,
    /// <summary>
    /// Truncates a unitless numeric value toward zero and writes an integer.
    /// </summary>
    Truncate = 0x73,
    /// <summary>
    /// Rounds a unitless numeric value using midpoint-to-even and writes an integer.
    /// </summary>
    RoundHalfEven = 0x74,
    /// <summary>
    /// Rounds midpoint values away from zero and writes an integer.
    /// </summary>
    RoundHalfUp = 0x75,
    /// <summary>
    /// Rounds midpoint values toward zero and writes an integer.
    /// </summary>
    RoundHalfDown = 0x76,
    /// <summary>
    /// Converts degrees to unitless radians.
    /// </summary>
    DegreeToRadians = 0x77,
    /// <summary>
    /// Converts unitless radians to a degree quantity.
    /// </summary>
    DegreeFromRadians = 0x78,
    /// <summary>
    /// Normalizes a degree or unitless numeric value into <c>[0, 360)</c> degrees.
    /// </summary>
    WrapDegree = 0x79,
    /// <summary>
    /// Sine. Unitless input is radians; degree input is converted to radians; result is unitless.
    /// </summary>
    Sin = 0x7A,
    /// <summary>
    /// Cosine. Unitless input is radians; degree input is converted to radians; result is unitless.
    /// </summary>
    Cos = 0x7B,
    /// <summary>
    /// Tangent. Unitless input is radians; degree input is converted to radians; result is unitless.
    /// </summary>
    Tan = 0x7C,
    /// <summary>
    /// Inverse sine for unitless numeric input in <c>[-1, 1]</c>; result is radians.
    /// </summary>
    Asin = 0x7D,
    /// <summary>
    /// Inverse cosine for unitless numeric input in <c>[-1, 1]</c>; result is radians.
    /// </summary>
    Acos = 0x7E,
    /// <summary>
    /// Inverse tangent for unitless numeric input; result is radians.
    /// </summary>
    Atan = 0x7F,
    /// <summary>
    /// <c>atan2(y, x)</c> with matching units; result is radians. <c>(0, 0)</c> yields <c>0</c>.
    /// </summary>
    Atan2 = 0x80,
    /// <summary>
    /// 2D hypotenuse; matching units are retained in the result.
    /// </summary>
    Hypot2D = 0x81,
    /// <summary>
    /// 3D hypotenuse; matching units are retained in the result.
    /// </summary>
    Hypot3D = 0x82,
    /// <summary>
    /// Scalar distance or 3D vector/point distance; matching units are retained.
    /// </summary>
    Distance = 0x83,
    /// <summary>
    /// 2D coordinate distance; matching units are retained.
    /// </summary>
    Distance2D = 0x84,
    /// <summary>
    /// 3D coordinate distance; matching units are retained.
    /// </summary>
    Distance3D = 0x85,
    /// <summary>
    /// Scalar or 3D vector/point squared distance; result is unitless.
    /// </summary>
    DistanceSquared = 0x86,
    /// <summary>
    /// 2D squared coordinate distance; result is unitless.
    /// </summary>
    DistanceSquared2D = 0x87,
    /// <summary>
    /// 3D squared coordinate distance; result is unitless.
    /// </summary>
    DistanceSquared3D = 0x88,
    /// <summary>
    /// Scalar square or vector/point squared length; result is unitless.
    /// </summary>
    LengthSquared = 0x89,
    /// <summary>
    /// 2D squared length; result is unitless.
    /// </summary>
    LengthSquared2D = 0x8A,
    /// <summary>
    /// 3D squared length; result is unitless.
    /// </summary>
    LengthSquared3D = 0x8B,
    /// <summary>
    /// Normalizes a 3D vector/point and returns a unitless vector; zero length yields <c>nothing</c>.
    /// </summary>
    Normalize = 0x8C,
    /// <summary>
    /// Normalizes 2D coordinates and returns a unitless vector with <c>z=0</c>; zero length yields <c>nothing</c>.
    /// </summary>
    Normalize2D = 0x8D,
    /// <summary>
    /// Normalizes 3D coordinates and returns a unitless vector; zero length yields <c>nothing</c>.
    /// </summary>
    Normalize3D = 0x8E,
    /// <summary>
    /// 3D vector/point dot product with matching units; result is unitless.
    /// </summary>
    Dot = 0x8F,
    /// <summary>
    /// 2D coordinate dot product; result is unitless.
    /// </summary>
    Dot2D = 0x90,
    /// <summary>
    /// 3D coordinate dot product; result is unitless.
    /// </summary>
    Dot3D = 0x91,
    /// <summary>
    /// 3D vector/point cross product with matching units; result is a unitless vector.
    /// </summary>
    Cross = 0x92,
    /// <summary>
    /// 2D coordinate cross product; result is the scalar z component, unitless.
    /// </summary>
    Cross2D = 0x93,
    /// <summary>
    /// 3D coordinate cross product; result is a unitless vector.
    /// </summary>
    Cross3D = 0x94,
    /// <summary>
    /// 3D vector/point angle in radians; zero length yields <c>nothing</c>.
    /// </summary>
    AngleBetween = 0x95,
    /// <summary>
    /// 2D coordinate angle in radians; zero length yields <c>nothing</c>.
    /// </summary>
    AngleBetween2D = 0x96,
    /// <summary>
    /// 3D coordinate angle in radians; zero length yields <c>nothing</c>.
    /// </summary>
    AngleBetween3D = 0x97,

    #endregion

    #region Group 3 - text, collection, iterators

    /// <summary>
    /// Takes the first <c>Y</c> values from a series, list, dice, range, or iterator source.
    /// </summary>
    TakeFirst = 0xA0,
    /// <summary>
    /// Drops the first <c>Y</c> values from a series, list, dice, range, or iterator source.
    /// </summary>
    DropFirst = 0xA1,
    /// <summary>
    /// Takes the last <c>Y</c> values from a finite list, dice, range, or iterator source; series yields
    /// <c>nothing</c>.
    /// </summary>
    TakeLast = 0xA2,
    /// <summary>
    /// Drops the last <c>Y</c> values from a finite list, dice, range, or iterator source; series yields
    /// <c>nothing</c>.
    /// </summary>
    DropLast = 0xA3,
    /// <summary>
    /// Takes the highest <c>Y</c> values from a finite list, dice, range, or iterator source; series yields
    /// <c>nothing</c>.
    /// </summary>
    TakeHighest = 0xA4,
    /// <summary>
    /// Takes the lowest <c>Y</c> values from a finite list, dice, range, or iterator source; series yields
    /// <c>nothing</c>.
    /// </summary>
    TakeLowest = 0xA5,
    /// <summary>
    /// Drops the highest <c>Y</c> values from a finite list, dice, range, or iterator source; series yields
    /// <c>nothing</c>.
    /// </summary>
    DropHighest = 0xA6,
    /// <summary>
    /// Drops the lowest <c>Y</c> values from a finite list, dice, range, or iterator source; series yields
    /// <c>nothing</c>.
    /// </summary>
    DropLowest = 0xA7,
    /// <summary>
    /// Chooses one random element from a finite list, dice, range, or iterator source; empty/invalid/series sources
    /// yield <c>nothing</c>.
    /// </summary>
    OneRandom = 0xA8,
    /// <summary>
    /// Chooses up to <c>Y</c> random elements without replacement. Lists stay lists, dice stay dice, ranges and
    /// iterators materialize as lists.
    /// </summary>
    TakeRandom = 0xA9,
    /// <summary>
    /// Selects one item using positive finite weights. Empty/no-positive-weight lists -&gt; <c>nothing</c>.
    /// </summary>
    OneWeighted = 0xAA,
    /// <summary>
    /// Selects up to <c>Y</c> items without replacement using positive finite weights. Result is a list.
    /// </summary>
    TakeWeighted = 0xAB,
    /// <summary>
    /// Counts collection/string/range/map items or consumes an iterator to count.
    /// </summary>
    Count = 0xAC,
    /// <summary>
    /// Text/tag raw-text prefix check or list/dice/range sequence prefix check.
    /// </summary>
    StartsWith = 0xAD,
    /// <summary>
    /// Text/tag raw-text suffix check or list/dice/range sequence suffix check.
    /// </summary>
    EndsWith = 0xAE,
    /// <summary>
    /// Text/tag substring, map key, list/dice/range membership, or vector/point component membership.
    /// </summary>
    Contains = 0xAF,
    /// <summary>
    /// Tests whether the container contains any value from the needle sequence. Iterators are consumed until the first
    /// match or exhaustion.
    /// </summary>
    ContainsAny = 0xB0,
    /// <summary>
    /// Tests whether the container contains every value from the needle sequence. Iterators are consumed until all
    /// needles match or exhaustion.
    /// </summary>
    ContainsAll = 0xB1,
    /// <summary>
    /// Truthiness <c>any</c> over list/dice/range/map values/text/tag/vector/point or iterator sources. Empty -&gt;
    /// <c>false</c>; <c>nothing</c> -&gt; <c>nothing</c>.
    /// </summary>
    HasAny = 0xB2,
    /// <summary>
    /// Truthiness <c>all</c> over list/dice/range/map values/text/tag/vector/point or iterator sources. Empty -&gt;
    /// <c>true</c>; <c>nothing</c> -&gt; <c>nothing</c>.
    /// </summary>
    HasAll = 0xB3,
    /// <summary>
    /// Value membership for map-like containers, vectors, and points.
    /// </summary>
    ContainsValue = 0xB4,
    /// <summary>
    /// Collection union/merge for list, dice, and map shapes; invalid shapes -&gt; <c>nothing</c>.
    /// </summary>
    Union = 0xB5,
    /// <summary>
    /// Map key intersection or list/dice multiset intersection; invalid shapes -&gt; <c>nothing</c>.
    /// </summary>
    Intersect = 0xB6,
    /// <summary>
    /// List zip into <c>{ left, right }</c> maps up to the shorter length; invalid shapes -&gt; <c>nothing</c>.
    /// </summary>
    Zip = 0xB7,
    /// <summary>
    /// Map/custom-type keys projection; <c>nothing</c> and non-map operands produce <c>nothing</c>.
    /// </summary>
    KeysOfMap = 0xB8,
    /// <summary>
    /// Map/custom-type values projection; <c>nothing</c> and non-map operands produce <c>nothing</c>.
    /// </summary>
    ValuesOfMap = 0xB9,
    /// <summary>
    /// Map/custom-type entries projection; <c>nothing</c> and non-map operands produce <c>nothing</c>.
    /// </summary>
    EntriesOfMap = 0xBA,
    /// <summary>
    /// Returns the first element from list, dice, range, map/custom values, text/tag, or iterator; invalid/empty
    /// sources -&gt; <c>nothing</c>.
    /// </summary>
    First = 0xBB,
    /// <summary>
    /// Returns the last element from list, dice, range, map/custom values, text/tag, or iterator; invalid/empty sources
    /// -&gt; <c>nothing</c>.
    /// </summary>
    Last = 0xBC,
    /// <summary>
    /// Returns the only element from list, dice, range, map/custom values, text/tag, or iterator;
    /// invalid/empty/multiple-element sources -&gt; <c>nothing</c>.
    /// </summary>
    Single = 0xBD,
    /// <summary>
    /// Creates a VM-internal iterator over a collection or range value. Non-iterable sources write <c>nothing</c>.
    /// </summary>
    IteratorCreate = 0xBE,
    /// <summary>
    /// Creates a VM-internal iterator, or writes <c>nothing</c> and jumps to <c>Y</c> when no iterator can be created.
    /// </summary>
    IteratorCreateOrJump = 0xBF,
    /// <summary>
    /// Writes the next item and continues, or jumps to <c>Y</c> when exhausted.
    /// </summary>
    IteratorNext = 0xC0,
    /// <summary>
    /// Disposes/closes a VM-internal iterator.
    /// </summary>
    IteratorClose = 0xC1,
    /// <summary>
    /// Materializes distinct source items in source order. Supports direct collection fast paths and iterators.
    /// </summary>
    Distinct = 0xC2,
    /// <summary>
    /// Sorts source items ascending. Supports direct list, dice, range, and iterator sources.
    /// </summary>
    SortAscending = 0xC3,
    /// <summary>
    /// Sorts source items descending. Supports direct list, dice, range, and iterator sources.
    /// </summary>
    SortDescending = 0xC4,
    /// <summary>
    /// Reverses list, dice, range, or iterator sources. Dice and iterators materialize lists; ranges stay ranges.
    /// </summary>
    Reverse = 0xC5,
    /// <summary>
    /// Shuffles list, dice, range, or iterator sources. Result is a list.
    /// </summary>
    Shuffle = 0xC6,
    /// <summary>
    /// Creates a VM-internal list builder for generated collections.
    /// </summary>
    ListBuilderCreate = 0xC7,
    /// <summary>
    /// Checks <c>MaxGeneratedCollectionItems</c> before adding an item.
    /// </summary>
    ListBuilderAdd = 0xC8,
    /// <summary>
    /// Materializes the list builder as a list.
    /// </summary>
    ListBuilderFinish = 0xC9,
    /// <summary>
    /// Creates a VM-internal map builder for generated map projections.
    /// </summary>
    MapBuilderCreate = 0xCA,
    /// <summary>
    /// Adds or overwrites a map entry; empty/nothing keys are skipped.
    /// </summary>
    MapBuilderAdd = 0xCB,
    /// <summary>
    /// Materializes the map builder as a map.
    /// </summary>
    MapBuilderFinish = 0xCC,
    /// <summary>
    /// Creates a VM-internal builder for <c>distinct by</c> loop lowering.
    /// </summary>
    DistinctBuilderCreate = 0xCD,
    /// <summary>
    /// Adds the value only when the key was not seen before.
    /// </summary>
    DistinctBuilderAdd = 0xCE,
    /// <summary>
    /// Materializes the distinct-by builder as a list.
    /// </summary>
    DistinctBuilderFinish = 0xCF,
    /// <summary>
    /// Creates a VM-internal builder for <c>group by</c> loop lowering.
    /// </summary>
    GroupBuilderCreate = 0xD0,
    /// <summary>
    /// Adds the value to the group identified by the key text.
    /// </summary>
    GroupBuilderAdd = 0xD1,
    /// <summary>
    /// Materializes the group builder as a map from keys to grouped lists.
    /// </summary>
    GroupBuilderFinish = 0xD2,
    /// <summary>
    /// Creates a VM-internal builder for <c>order by</c> loop lowering.
    /// </summary>
    OrderBuilderCreate = 0xD3,
    /// <summary>
    /// Adds a key/value pair preserving source order for stable ordering.
    /// </summary>
    OrderBuilderAdd = 0xD4,
    /// <summary>
    /// Sorts by stored keys ascending and materializes the ordered values as a list.
    /// </summary>
    OrderBuilderFinishAscending = 0xD5,
    /// <summary>
    /// Sorts by stored keys descending and materializes the ordered values as a list.
    /// </summary>
    OrderBuilderFinishDescending = 0xD6,
    /// <summary>
    /// Tests a dice/card pattern and returns boolean.
    /// </summary>
    HasPattern = 0xD7,
    /// <summary>
    /// Takes items matching a dice/card pattern. Dice sources produce dice; list sources produce lists.
    /// </summary>
    TakePattern = 0xD8,
    /// <summary>
    /// Recognizes a complete data literal from Text, preserving the original Text on recognition failure.
    /// </summary>
    ParseLiteral = 0xD9,
    /// <summary>Emits without a delay and writes host acceptance to the destination register.</summary>
    EmitInstant = 0xDA,
    /// <summary>Emits after a time quantity and writes host acceptance to the destination register.</summary>
    EmitAfter = 0xDB,
    /// <summary>Publishes without a delay and writes host acceptance to the destination register.</summary>
    PublishInstant = 0xDC,
    /// <summary>Publishes after a time quantity and writes host acceptance to the destination register.</summary>
    PublishAfter = 0xDD,
    /// <summary>Constructs a portable data value from a type name, ordered argument values and labels.</summary>
    ConstructData = 0xDE,
    /// <summary>Splits Text on a Text delimiter, or on portable whitespace when the delimiter is Nothing.</summary>
    SplitText = 0xDF,
    /// <summary>Materializes the ordered Cartesian product of two Lists as two-element Lists.</summary>
    Cartesian = 0xE0,

    #endregion
}
