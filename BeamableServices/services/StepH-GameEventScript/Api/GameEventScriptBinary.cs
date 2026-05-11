#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json.Serialization;

namespace StepH.GameEventScript.Api;

public readonly struct GameEventScriptBinary
{
    public GameEventScriptBinaryHeader Header { get; init; }
    public string ModuleName { get; init; }
    public GameEventScriptTextTable StringTable { get; init; }
    public GameEventScriptUInt16Table UInt16SliceTable { get; init; }
    public GameEventScriptBinaryBindTable BindTable { get; init; }
    
    public GameEventScriptBytecodeInstruction[] InstructionTable { get; init; }
}

public readonly struct GameEventScriptBinaryHeader
{
    [Flags]
    public enum GameEventScriptBinaryFlags : ushort
    {
        None = 0,
        Optimization = 1 << 0,
        Debug = 1 << 1,
        CompactInstruction = 1 << 2,
    }
    
    public const uint Magic = 0x42534547; // "GESB"

    public ushort Version { get; init; }
    public GameEventScriptBinaryFlags Flags { get; init; }
    public const uint HeaderSize = 16;
    public uint FileSize { get; init; } 
}

public readonly struct GameEventScriptTextTable
{
    public readonly struct SliceEntry
    {
        public ushort Start { get; init; }
        public ushort Length { get; init; }
    }
    
    public SliceEntry[] Slices { get; init; }
    public byte[] Data { get; init; }
    
    public string Resolve(ushort index) => Encoding.UTF8.GetString(Data.AsSpan(Slices[index].Start, Slices[index].Length));
}


public readonly struct GameEventScriptUInt16Table
{
    public readonly struct SliceEntry
    {
        public ushort Start { get; init; }
        public ushort Length { get; init; }
    }
    
    public SliceEntry[] Slices { get; init; }
    public ushort[] Data { get; init; }
    
    public ReadOnlySpan<ushort> Resolve(ushort index) => Data.AsSpan(Slices[index].Start, Slices[index].Length);
}

public readonly struct GameEventScriptBinaryBindTable
{
    private readonly GameEventScriptBinaryBindEntry[]? _entries;

    public GameEventScriptBinaryBindTable(IReadOnlyList<GameEventScriptBinaryBindEntry>? entries)
    {
        _entries = entries?.ToArray() ?? [];
        EntryCount = ToEntryCount(_entries.Length);
    }

    public ushort EntryCount { get; }

    public IReadOnlyList<GameEventScriptBinaryBindEntry> Entries => _entries ?? [];

    private static ushort ToEntryCount(int count) => count > ushort.MaxValue ? throw new ArgumentOutOfRangeException(nameof(count), "GameEventScriptBinary tables cannot exceed 65535 entries.") : checked((ushort)count);
}

public readonly struct GameEventScriptBinaryBindEntry(GameEventScriptBinaryBindKind kind, ushort name, IReadOnlyList<ushort>? argumentNames, uint entryAddress = 0)
{
    private readonly ushort[]? _argumentNames = argumentNames?.ToArray() ?? [];

    public GameEventScriptBinaryBindKind Kind { get; } = kind;

    public ushort Name { get; } = name;

    public IReadOnlyList<ushort> ArgumentNames => _argumentNames ?? [];

    public uint EntryAddress { get; } = entryAddress;
}

public enum GameEventScriptBinaryBindKind : byte
{
    MessageHandler = 0x10,
    Function = 0x11,
    Predicate = 0x12,
    ExtensionCall = 0x20,
    ExternalType = 0x30
}

public enum GameEventScriptBytecodeInstructionUnit : byte
{
    None = 0,
    Degree = 1,
    Meter = 2,
    Second = 3,
    Percentage = 4
}

[JsonConverter(typeof(GameEventScriptBytecodeInstructionJsonConverter))]
[StructLayout(LayoutKind.Explicit, Size = 12)]
public struct GameEventScriptBytecodeInstruction(GameEventScriptBytecodeOpCode opCode, ushort dest = 0, ushort a = 0, ushort b = 0, ushort c = 0, ushort d = 0, byte unitAndFlags = 0)
{
    [FieldOffset(0)]
    public GameEventScriptBytecodeOpCode OpCode = opCode;

    [FieldOffset(1)]
    public readonly byte UnitAndFlags = unitAndFlags;

    [FieldOffset(2)]
    public readonly ushort Dest_U16 = dest;

    [FieldOffset(4)]
    public ushort A_U16 = a;

    [FieldOffset(6)]
    public ushort B_U16 = b;

    [FieldOffset(8)]
    public ushort C_U16 = c;

    [FieldOffset(10)]
    public ushort D_U16 = d;

    [FieldOffset(4)]
    public short A_I16;

    [FieldOffset(6)]
    public short B_I16;

    [FieldOffset(8)]
    public short C_I16;

    [FieldOffset(10)]
    public short D_I16;

    [FieldOffset(4)]
    public int A_I32;

    [FieldOffset(8)]
    public int B_I32;

    [FieldOffset(4)]
    public uint A_U32;

    [FieldOffset(8)]
    public uint B_U32;

    [FieldOffset(4)]
    public long I64;

    [FieldOffset(4)]
    public ulong U64;

    [FieldOffset(4)]
    public double F64;
}

public enum GameEventScriptBytecodeOpCode : byte
{
    #region Core loads, slot movement, branches, returns
    
    Nop = 0x00,
    LoadNothing = 0x01,
    LoadTrue = 0x02,
    LoadFalse = 0x03,
    LoadInteger = 0x04,
    LoadFloat = 0x05,
    LoadText = 0x06,
    LoadTag = 0x07,
    MoveSlot = 0x08,
    BindParameter = 0x09,
    Jump = 0x0A,
    JumpIfTrue = 0x0B,
    JumpIfFalse = 0x0C,
    JumpIfNotTrue = 0x0D,
    ReturnNothing = 0x0E,
    Return = 0x0F,
    
    #endregion
    #region Generic boolean/comparison/arithmetic/default operations

    Or = 0x10,
    And = 0x11,
    Xor = 0x12,
    Equal = 0x13,
    NotEqual = 0x14,
    ApproxEqual = 0x15,
    Less = 0x16,
    Greater = 0x17,
    LessOrEqual = 0x18,
    GreaterOrEqual = 0x19,
    Add = 0x1A,
    Subtract = 0x1B,
    Multiply = 0x1C,
    Divide = 0x1D,
    Power = 0x1E,
    Default = 0x1F,
    
    #endregion
    #region Primitive integer fast paths

    PrimitiveIntegerEqual = 0x20,
    PrimitiveIntegerNotEqual = 0x21,
    PrimitiveIntegerLess = 0x22,
    PrimitiveIntegerGreater = 0x23,
    PrimitiveIntegerLessOrEqual = 0x24,
    PrimitiveIntegerGreaterOrEqual = 0x25,
    PrimitiveIntegerAdd = 0x26,
    PrimitiveIntegerSubtract = 0x27,
    PrimitiveIntegerMultiply = 0x28,
    PrimitiveIntegerDivide = 0x29,
    PrimitiveIntegerFloorDivide = 0x2A,
    PrimitiveIntegerModulo = 0x2B,
    PrimitiveIntegerRemainder = 0x2C,
    IntegerDivide = 0x2D,
    Modulo = 0x2E,
    Remainder = 0x2F,
    
    #endregion
    #region Unary, random, dice, range value operations

    UnaryNegate = 0x30,
    UnaryNot = 0x31,
    UnaryHasValue = 0x32,
    UnaryEmpty = 0x33,
    UnaryLength = 0x34,
    UnaryChance = 0x35,
    UnaryAbs = 0x36,
    UnaryNaturalLog = 0x37,
    Clamp = 0x38,
    Random = 0x39,
    Dice = 0x3A,
    RandomPush = 0x3B,
    RandomPushConstant = 0x3C,
    RandomPop = 0x3D,
    Range = 0x3E,
    RangeWithStep = 0x3F,
    
    #endregion
    #region Collection/text operations, projections, short-circuit markers

    Contains = 0x40,
    ContainsValue = 0x41,
    StartsWith = 0x42,
    EndsWith = 0x43,
    Intersect = 0x44,
    Combine = 0x45,
    Except = 0x46,
    Zip = 0x47,
    UnaryKeys = 0x48,
    UnaryValues = 0x49,
    UnaryEntries = 0x4A,
    ShortCircuitOr = 0x4B,
    ShortCircuitAnd = 0x4C,
    ShortCircuitImplies = 0x4D,
    
    #endregion
    #region Primitive/domain casts

    CastNothing = 0x50,
    CastBoolean = 0x51,
    CastInteger = 0x52,
    CastFloat = 0x53,
    CastNumber = 0x54,
    CastPercentage = 0x55,
    CastDegree = 0x56,
    CastMeter = 0x57,
    CastSecond = 0x58,
    CastVector = 0x59,
    CastPoint = 0x5A,
    CastUuid = 0x5B,
    CastOptional = 0x5C,
    CastCustom = 0x5D,
    
    #endregion
    #region Collection/message/reference casts

    CastSequence = 0x60,
    CastSeries = 0x61,
    CastEnvelope = 0x62,
    CastRef = 0x63,
    CastTag = 0x64,
    CastText = 0x65,
    CastList = 0x66,
    CastRange = 0x67,
    CastMessage = 0x68,
    CastHandler = 0x69,
    CastDictionary = 0x6A,
    CastSet = 0x6B,
    CastDice = 0x6C,
    
    #endregion
    #region Primitive/domain type checks
    
    TypeCheckNothing = 0x70,
    TypeCheckBoolean = 0x71,
    TypeCheckInteger = 0x72,
    TypeCheckFloat = 0x73,
    TypeCheckPercentage = 0x74,
    TypeCheckDegree = 0x75,
    TypeCheckMeter = 0x76,
    TypeCheckSecond = 0x77,
    TypeCheckVector = 0x78,
    TypeCheckPoint = 0x79,
    TypeCheckUuid = 0x7A,
    TypeCheckOptional = 0x7B,
    TypeCheckTag = 0x7C,
    TypeCheckText = 0x7D,
    TypeCheckCustom = 0x7E,
    
    #endregion
    #region Collection/message/reference type checks

    TypeCheckSequence = 0x80,
    TypeCheckSeries = 0x81,
    TypeCheckEnvelope = 0x82,
    TypeCheckList = 0x83,
    TypeCheckRange = 0x84,
    TypeCheckMessage = 0x85,
    TypeCheckHandler = 0x86,
    TypeCheckRef = 0x87,
    TypeCheckDictionary = 0x88,
    TypeCheckSet = 0x89,
    TypeCheckDice = 0x8A,
    
    #endregion
    #region Construction, access, handlers, predicates, calls

    LoadHandler = 0x90,
    TypeConstructor = 0x91,
    MemberAccess = 0x93,
    IndexedAccess = 0x94,
    BuildList = 0x95,
    BuildSequence = 0x96,
    BuildSet = 0x97,
    BuildDictionary = 0x98,
    BuildMessage = 0x99,
    BindHandler = 0x9A,
    Variadic = 0x9B,
    
    #endregion
    #region Scopes and message emit/publish operations

    EnterScope = 0xA0,
    ExitScope = 0xA1,
    EmitMessage = 0xA2,
    EmitMessageWithTags = 0xA3,
    PublishMessage = 0xA4,
    PublishMessageWithTags = 0xA5,
    EmitMessageValue = 0xA6,
    EmitMessageValueWithTags = 0xA7,
    PublishMessageValue = 0xA8,
    PublishMessageValueWithTags = 0xA9,
    
    #endregion
    #region Iterators and collection builders

    RangeIterator = 0xB0,
    RangeIteratorWithStep = 0xB1,
    RangeIteratorShort = 0xB2,
    CollectionIterator = 0xB3,
    IteratorNext = 0xB4,
    IteratorClose = 0xB5,
    CollectionBuilderList = 0xB6,
    CollectionBuilderSet = 0xB7,
    CollectionBuilderAdd = 0xB8,
    CollectionBuilderFinish = 0xB9,
    
    #endregion
    
    #region Reserved
    Call = 0xC0,
    CallPredicate = 0xC1,
    CallStandard = 0xC2,
    CallStandardPredicate = 0xC3,
    CallExternal = 0xC4,
    CallExternalPredicate = 0xC5,
    #endregion
    
    #region Pipeline operations and future pipeline expansion

    Pipeline = 0xD0
    
    #endregion
}
