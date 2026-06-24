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
    MapBuilder = 0x24,

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
    JumpIfNothing = 0x06,

    Call = 0x07,
    CallStandard = 0x08,
    CallExternal = 0x09,

    ReturnVoid = 0x0A,
    ReturnValue = 0x0B,

    EmitMessage = 0x0C,
    EmitMessageWithTags = 0x0D,
    EmitMessageValue = 0x0E,
    EmitMessageValueWithTags = 0x0F,

    PublishMessage = 0x10,
    PublishMessageWithTags = 0x11,
    PublishMessageValue = 0x12,
    PublishMessageValueWithTags = 0x13,

    Cast = 0x14,
    CastCustom = 0x15,
    CastUnit = 0x16,
    CastNumeric = 0x17,

    CheckType = 0x18,
    CheckCustomType = 0x19,
    CheckUnit = 0x1A,
    CheckNumeric = 0x1B,
    CheckInteger = 0x1C,
    CheckFractional = 0x1D,

    Move = 0x1E,
    MemberAccess = 0x1F,
    IndexAccess = 0x20,
    PropertyAccess = 0x21,
    BindHandler = 0x22,

    LoadNothing = 0x23,
    LoadTrue = 0x24,
    LoadFalse = 0x25,
    LoadInteger = 0x26,
    LoadFloat = 0x27,
    LoadPercentage = 0x28,
    LoadText = 0x29,
    LoadTag = 0x2A,
    LoadHandler = 0x2B,
    LoadMessage = 0x2C,

    StageRegister = 0x2D,
    StageNothing = 0x2E,
    StageTrue = 0x2F,
    StageFalse = 0x30,
    StageInteger = 0x31,
    StageFloat = 0x32,
    StageText = 0x33,
    StageTag = 0x34,
    StagePercentage = 0x35,

    CreateDice = 0x36,
    CreateVector = 0x37,
    CreatePoint = 0x38,
    CreateList = 0x39,
    CreateMap = 0x3A,
    CreateRange = 0x3B,
    CreateRangeWithStep = 0x3C,
    CreateRangeIterator = 0x3D,
    CreateRangeIteratorWithStep = 0x3E,
    CreateRangeIteratorShort = 0x3F,
    CreateRecord = 0x40,
    CreateExternalType = 0x41,

    HasValue = 0x42,
    IsEmpty = 0x43,
    Default = 0x44,

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
    OneWeighted = 0xAA,
    TakeWeighted = 0xAB,
    Count = 0xAC,
    StartsWith = 0xAD,
    EndsWith = 0xAE,
    Contains = 0xAF,
    ContainsAny = 0xB0,
    ContainsAll = 0xB1,
    HasAny = 0xB2,
    HasAll = 0xB3,
    ContainsValue = 0xB4,
    Union = 0xB5,
    Intersect = 0xB6,
    Zip = 0xB7,
    KeysOfMap = 0xB8,
    ValuesOfMap = 0xB9,
    EntriesOfMap = 0xBA,
    First = 0xBB,
    Last = 0xBC,
    Single = 0xBD,
    StreamCreate = 0xBE,
    StreamCreateOrJump = 0xBF,
    StreamNext = 0xC0,
    StreamClose = 0xC1,
    StreamMap = 0xC2,
    StreamFilter = 0xC3,
    StreamCollectList = 0xC4,
    Distinct = 0xC5,
    DistinctBy = 0xC6,
    GroupBy = 0xC7,
    SortAscending = 0xC8,
    SortDescending = 0xC9,
    OrderByAscending = 0xCA,
    OrderByDescending = 0xCB,
    Reverse = 0xCC,
    Shuffle = 0xCD,
    ListBuilderCreate = 0xCE,
    ListBuilderAdd = 0xCF,
    ListBuilderFinish = 0xD0,
    MapBuilderCreate = 0xD1,
    MapBuilderAdd = 0xD2,
    MapBuilderFinish = 0xD3,
    HasPattern = 0xD4,
    TakePattern = 0xD5,

    #endregion
}
