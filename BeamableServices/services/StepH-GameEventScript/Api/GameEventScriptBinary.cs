#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace StepH.GameEventScript.Api;

public sealed class GameEventScriptBinary(
    GameEventScriptBinaryHeader header,
    string moduleName,
    GameEventScriptTextTable textConstantTable,
    GameEventScriptUInt16Table uint16ConstantTable,
    GameEventScriptBinaryBindTable bindTable,
    GameEventScriptBytecodeInstruction[] instructionTable)
{
    public GameEventScriptBinaryHeader Header { get; } = header;
    public string ModuleName { get; } = moduleName;
    public GameEventScriptTextTable TextConstantTable = textConstantTable;
    public GameEventScriptUInt16Table Uint16ConstantTable { get; } = uint16ConstantTable;
    public GameEventScriptBinaryBindTable BindTable { get; } = bindTable;
    public GameEventScriptBytecodeInstruction[] InstructionTable { get; } = instructionTable;
}

public readonly struct GameEventScriptBinaryHeader
{
    [Flags]
    public enum GameEventScriptBinaryFlags : ushort
    {
        None = 0,
        Optimization = 1 << 0,
        Debug = 1 << 1,
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

    public int ResolveSize(ushort index) => Slices[index].Length;
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
    
    public readonly struct GameEventScriptBinaryBindEntry(
        GameEventScriptBinaryBindKind kind,
        ushort name,
        IReadOnlyList<ushort>? argumentNames,
        ushort entryAddress = 0xFFFF,
        ushort id = 0xFFFF,
        IReadOnlyList<ushort>? requiredTags = null,
        IReadOnlyList<ushort>? excludedTags = null)
    {
        private readonly ushort[]? _argumentNames = argumentNames?.ToArray() ?? [];
        private readonly ushort[]? _requiredTags = requiredTags?.ToArray() ?? [];
        private readonly ushort[]? _excludedTags = excludedTags?.ToArray() ?? [];

        public ushort Id { get; } = id;

        public GameEventScriptBinaryBindKind Kind { get; } = kind;

        public ushort Name { get; } = name;

        public IReadOnlyList<ushort> ArgumentNames => _argumentNames ?? [];

        public IReadOnlyList<ushort> RequiredTags => _requiredTags ?? [];

        public IReadOnlyList<ushort> ExcludedTags => _excludedTags ?? [];

        public ushort EntryAddress { get; } = entryAddress;
    }

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

public enum GameEventScriptBinaryBindKind : byte
{
    MessageHandler = 0x10,
    MessageNameHandler = 0x11,
    Function = 0x12,
    Predicate = 0x13,
    ExtensionCall = 0x20,
    OutboundMessage = 0x21,
    Record = 0x30,
    ExternalType = 0x31
}

public enum GameEventScriptBytecodeInstructionUnit : byte
{
    UnitNone = 0,
    UnitDegree = 1,
    UnitMeter = 2,
    UnitSecond = 3,

    UnitInvalid = 255 // Sentinel for invalid or unsupported unit input.
}

[Flags]
public enum GameEventScriptInstructionFlag : byte
{
    None = 0,
    NormalizeResultAsPredicate = 0x20
}

public enum GameEventScriptBytecodeTypeKind : byte
{
    Nothing = 0x00,
    Boolean = 0x02,
    Integer = 0x03,
    Float = 0x04,
    Percentage = 0x05,
    Tag = 0x06,
    Text = 0x07,
    Vector = 0x08,
    Point = 0x09,
    Range = 0x0a,

    Handler = 0x10,
    Message = 0x11,
    
    List = 0x20,
    Dice = 0x21,
    Map = 0x22,
    ListBuilder = 0x23,

    Stream = 0x30,
    Series = 0x31,
    
    Custom = 0xFF,
}

public enum GameEventScriptBytecodePatternKind : ushort
{
    CountAny = 0x00,
    CountFace = 0x01,
    FullHouse = 0x02,
    Straight = 0x03,
}

[StructLayout(LayoutKind.Explicit, Size = 16)]
public struct GameEventScriptBytecodeInstruction
{
    private const byte UnitMask = 0x1F;
    private const byte InstructionFlagMask = 0xE0;

    [FieldOffset(0)] public GameEventScriptBytecodeOpCode OpCode;

    [FieldOffset(1)] public byte UnitAndFlags;

    [FieldOffset(2)] public ushort DestinationSlot;
    [FieldOffset(2)] public ushort MessageDestination;
    
    [FieldOffset(4)] public short ImmediateX;
    [FieldOffset(4)] public ushort ConditionSlot;
    [FieldOffset(4)] public ushort XSlot;
    [FieldOffset(4)] public ushort StringIndex;
    [FieldOffset(4)] public ushort SecondaryListIndex;
    [FieldOffset(4)] public ushort BindId;
    [FieldOffset(4)] public ushort Index;
    [FieldOffset(4)] public short Count;

    [FieldOffset(6)] public short ImmediateY;
    [FieldOffset(6)] public ushort TargetAddress;
    [FieldOffset(6)] public ushort YSlot;
    [FieldOffset(6)] public ushort EntryAddress;
    [FieldOffset(6)] public ushort SecondaryStringIndex;
    [FieldOffset(6)] public ushort ListIndex;
    [FieldOffset(6)] public ushort TypeOperand;
    [FieldOffset(6)] public GameEventScriptBytecodeTypeKind TypeKind;

    #region Extra Payload for some opcodes
    
    [FieldOffset(8)] public ulong Payload;
    
    [FieldOffset(8)] public ushort AU;
    [FieldOffset(8)] public short AS;

    [FieldOffset(10)] public ushort BU;
    [FieldOffset(10)] public short BS;

    [FieldOffset(12)] public ushort CU;
    [FieldOffset(12)] public short CS;

    [FieldOffset(14)] public ushort DU;
    [FieldOffset(14)] public short DS;

    [FieldOffset(8)] public long I64;

    [FieldOffset(8)] public double F64;
    
    #endregion

    internal GameEventScriptBytecodeInstructionUnit Unit => DecodeUnit(UnitAndFlags);

    internal GameEventScriptInstructionFlag InstructionFlags => DecodeInstructionFlags(UnitAndFlags);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool HasInstructionFlag(GameEventScriptInstructionFlag flag)
        => HasInstructionFlag(UnitAndFlags, flag);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void AddInstructionFlag(GameEventScriptInstructionFlag flag)
        => UnitAndFlags = WithInstructionFlag(UnitAndFlags, flag);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static byte EncodeUnitAndFlags(GameEventScriptBytecodeInstructionUnit? unit, GameEventScriptInstructionFlag flags = GameEventScriptInstructionFlag.None)
        => EncodeUnitAndFlags(unit.ToStoredUnit(), flags);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static byte EncodeUnitAndFlags(GameEventScriptBytecodeInstructionUnit unit, GameEventScriptInstructionFlag flags = GameEventScriptInstructionFlag.None)
        => (byte)(((byte)unit.ToStoredUnit() & UnitMask) | ((byte)flags & InstructionFlagMask));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static GameEventScriptBytecodeInstructionUnit DecodeUnit(byte unitAndFlags)
        => ((GameEventScriptBytecodeInstructionUnit)(unitAndFlags & UnitMask)).ToStoredUnit();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static GameEventScriptInstructionFlag DecodeInstructionFlags(byte unitAndFlags)
        => (GameEventScriptInstructionFlag)(unitAndFlags & InstructionFlagMask);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool HasInstructionFlag(byte unitAndFlags, GameEventScriptInstructionFlag flag)
        => (unitAndFlags & (byte)flag) != 0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static byte WithInstructionFlag(byte unitAndFlags, GameEventScriptInstructionFlag flag)
        => (byte)(unitAndFlags | ((byte)flag & InstructionFlagMask));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static byte WithoutInstructionFlags(byte unitAndFlags)
        => (byte)(unitAndFlags & UnitMask);
}

public enum GameEventScriptBytecodeOpCode : byte
{
    #region Group 1 - control, calls, messages, types, values

    Nop = 0x00,
    SlotLocals = 0x01,
    Jump = 0x02,
    JumpIfTrue = 0x03,
    JumpIfFalse = 0x04,
    JumpIfNotTrue = 0x05,

    Call = 0x06,
    CallStandard = 0x07,
    CallExternal = 0x08,

    ReturnVoid = 0x09,
    ReturnValue = 0x0A,

    EmitMessage = 0x0B,
    EmitMessageWithTags = 0x0C,
    EmitMessageValue = 0x0D,
    EmitMessageValueWithTags = 0x0E,

    PublishMessage = 0x0F,
    PublishMessageWithTags = 0x10,
    PublishMessageValue = 0x11,
    PublishMessageValueWithTags = 0x12,

    Cast = 0x13,
    CastCustom = 0x14,
    CastUnit = 0x15,
    CastNumeric = 0x16,

    CheckType = 0x17,
    CheckCustomType = 0x18,
    CheckUnit = 0x19,
    CheckNumeric = 0x1A,
    CheckInteger = 0x1B,
    CheckFractional = 0x1C,

    Move = 0x1D,
    MemberAccess = 0x1E,
    IndexAccess = 0x1F,
    PropertyAccess = 0x20,
    BindHandler = 0x21,

    LoadNothing = 0x22,
    LoadTrue = 0x23,
    LoadFalse = 0x24,
    LoadInteger = 0x25,
    LoadFloat = 0x26,
    LoadPercentage = 0x27,
    LoadText = 0x28,
    LoadTag = 0x29,
    LoadHandler = 0x2A,
    LoadMessage = 0x2B,

    StageRegister = 0x2C,
    StageNothing = 0x2D,
    StageTrue = 0x2E,
    StageFalse = 0x2F,
    StageInteger = 0x30,
    StageFloat = 0x31,
    StageText = 0x32,
    StageTag = 0x33,
    StagePercentage = 0x34,

    CreateDice = 0x35,
    CreateVector = 0x36,
    CreatePoint = 0x37,
    CreateList = 0x38,
    CreateMap = 0x39,
    CreateRange = 0x3A,
    CreateRangeWithStep = 0x3B,
    CreateRangeIterator = 0x3C,
    CreateRangeIteratorWithStep = 0x3D,
    CreateRangeIteratorShort = 0x3E,
    CreateRecord = 0x3F,
    CreateExternalType = 0x40,

    HasValue = 0x41,
    IsEmpty = 0x42,
    Default = 0x43,

    #endregion

    #region Group 2 - boolean algebra, comparison, math and random

    Or = 0x50,
    And = 0x51,
    Xor = 0x52,
    Implies = 0x53,
    Not = 0x54,
    Equal = 0x55,
    NotEqual = 0x56,
    Less = 0x57,
    Greater = 0x58,
    LessOrEqual = 0x59,
    GreaterOrEqual = 0x5A,
    Add = 0x5B,
    Subtract = 0x5C,
    Multiply = 0x5D,
    Divide = 0x5E,
    Power = 0x5F,
    IntegerDivide = 0x60,
    Modulo = 0x61,
    Remainder = 0x62,
    Min = 0x63,
    Max = 0x64,
    Negate = 0x65,
    Abs = 0x66,
    LogN = 0x67,
    Chance = 0x68,
    Clamp = 0x69,
    RandomTake = 0x6A,
    RandomTakeFloat = 0x6B,
    RandomPush = 0x6C,
    RandomPushConstant = 0x6D,
    RandomPop = 0x6E,
    Term = 0x6F,
    Exp = 0x70,
    Floor = 0x71,
    Ceil = 0x72,
    Truncate = 0x73,
    RoundHalfEven = 0x74,
    RoundHalfUp = 0x75,
    RoundHalfDown = 0x76,
    DegreeToRadians = 0x77,
    DegreeFromRadians = 0x78,
    WrapDegree = 0x79,
    Sin = 0x7A,
    Cos = 0x7B,
    Tan = 0x7C,
    Asin = 0x7D,
    Acos = 0x7E,
    Atan = 0x7F,
    Atan2 = 0x80,
    Hypot2D = 0x81,
    Hypot3D = 0x82,
    Distance = 0x83,
    Distance2D = 0x84,
    Distance3D = 0x85,
    DistanceSquared = 0x86,
    DistanceSquared2D = 0x87,
    DistanceSquared3D = 0x88,
    LengthSquared = 0x89,
    LengthSquared2D = 0x8A,
    LengthSquared3D = 0x8B,
    Normalize = 0x8C,
    Normalize2D = 0x8D,
    Normalize3D = 0x8E,
    Dot = 0x8F,
    Dot2D = 0x90,
    Dot3D = 0x91,
    Cross = 0x92,
    Cross2D = 0x93,
    Cross3D = 0x94,
    AngleBetween = 0x95,
    AngleBetween2D = 0x96,
    AngleBetween3D = 0x97,

    #endregion

    #region Group 3 - text, collection, streams

    TakeFirst = 0xA0,
    DropFirst = 0xA1,
    TakeLast = 0xA2,
    DropLast = 0xA3,
    TakeHighest = 0xA4,
    TakeLowest = 0xA5,
    DropHighest = 0xA6,
    DropLowest = 0xA7,
    OneRandom = 0xA8,
    TakeRandom = 0xA9,
    Count = 0xAA,
    StartsWith = 0xAB,
    EndsWith = 0xAC,
    Contains = 0xAD,
    ContainsAny = 0xAE,
    ContainsAll = 0xAF,
    HasAny = 0xB0,
    HasAll = 0xB1,
    ContainsValue = 0xB2,
    Union = 0xB3,
    Intersect = 0xB4,
    Zip = 0xB5,
    KeysOfMap = 0xB6,
    ValuesOfMap = 0xB7,
    EntriesOfMap = 0xB8,
    First = 0xB9,
    Last = 0xBA,
    Single = 0xBB,
    StreamCreate = 0xBC,
    StreamNext = 0xBD,
    StreamClose = 0xBE,
    StreamMap = 0xBF,
    StreamFilter = 0xC0,
    Sum = 0xC1,
    Average = 0xC2,
    StreamMin = 0xC3,
    StreamMax = 0xC4,
    StreamOneWeighted = 0xC5,
    StreamTakeWeighted = 0xC6,
    StreamCollectList = 0xC7,
    StreamCollectMap = 0xC8,
    StreamCollectMapValue = 0xC9,
    Distinct = 0xCA,
    DistinctBy = 0xCB,
    GroupBy = 0xCC,
    SortAscending = 0xCD,
    SortDescending = 0xCE,
    OrderByAscending = 0xCF,
    OrderByDescending = 0xD0,
    Reverse = 0xD1,
    Shuffle = 0xD2,
    ListBuilderCreate = 0xD3,
    ListBuilderAdd = 0xD4,
    ListBuilderFinish = 0xD5,
    HasPattern = 0xD6,
    TakePattern = 0xD7,

    #endregion
}
