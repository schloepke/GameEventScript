#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json.Serialization;

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

    UnitNothing = 255 // Special non-unit type for nothing / void helping to evaluate unions to nothing 
}

[Flags]
public enum GameEventScriptInstructionFlag : byte
{
    None = 0,
    NormalizeResultAsPredicate = 0x20
}

public enum GameEventScriptBytecodeTypeKind : byte
{
    Invalid = 0x00,
    Nothing = 0x01,
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

[JsonConverter(typeof(GameEventScriptBytecodeInstructionJsonConverter))]
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

    MoveSlot = 0x1D,
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
    RandomPush = 0x6B,
    RandomPushConstant = 0x6C,
    RandomPop = 0x6D,
    Term = 0x6E,

    #endregion

    #region Group 3 - text, collection, streams

    TakeFirst = 0x80,
    DropFirst = 0x81,
    TakeLast = 0x82,
    DropLast = 0x83,
    TakeHighest = 0x84,
    TakeLowest = 0x85,
    DropHighest = 0x86,
    DropLowest = 0x87,
    OneRandom = 0x88,
    TakeRandom = 0x89,
    Length = 0x8A,
    StartsWith = 0x8B,
    EndsWith = 0x8C,
    Contains = 0x8D,
    ContainsAny = 0x8E,
    ContainsAll = 0x8F,
    HasAny = 0x90,
    HasAll = 0x91,
    ContainsValue = 0x92,
    Union = 0x93,
    Intersect = 0x94,
    Zip = 0x95,
    KeysOfMap = 0x96,
    ValuesOfMap = 0x97,
    EntriesOfMap = 0x98,
    First = 0x99,
    Last = 0x9A,
    Single = 0x9B,
    StreamCreate = 0x9C,
    StreamNext = 0x9D,
    StreamClose = 0x9E,
    StreamMap = 0x9F,
    StreamFilter = 0xA0,
    StreamCount = 0xA1,
    StreamSum = 0xA2,
    StreamAverage = 0xA3,
    StreamMin = 0xA4,
    StreamMax = 0xA5,
    StreamOneWeighted = 0xA6,
    StreamTakeWeighted = 0xA7,
    StreamCollectList = 0xA8,
    StreamCollectMap = 0xA9,
    StreamCollectMapValue = 0xAA,
    Distinct = 0xAB,
    DistinctBy = 0xAC,
    GroupBy = 0xAD,
    SortAscending = 0xAE,
    SortDescending = 0xAF,
    OrderByAscending = 0xB0,
    OrderByDescending = 0xB1,

    #endregion

    #region Group 4 - pipeline terminals and transforms

    PipelineReverse = 0xD0,
    PipelineShuffle = 0xD1,
    PipelineDicePatternCountAny = 0xD2,
    PipelineDicePatternCountFace = 0xD3,
    PipelineDicePatternFullHouse = 0xD4,
    PipelineDicePatternStraight = 0xD5,
    PipelineTakePatternCountAny = 0xD6,
    PipelineTakePatternCountFace = 0xD7,
    PipelineTakePatternFullHouse = 0xD8,
    PipelineTakePatternStraight = 0xD9,
    PipelineListCreateBuilder = 0xDA,
    PipelineListBuilderAdd = 0xDB,
    PipelineListBuilderFinish = 0xDC

    #endregion
}
