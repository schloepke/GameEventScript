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
    
    public readonly struct GameEventScriptBinaryBindEntry(GameEventScriptBinaryBindKind kind, ushort name, IReadOnlyList<ushort>? argumentNames, ushort entryAddress = 0xFFFF)
    {
        private readonly ushort[]? _argumentNames = argumentNames?.ToArray() ?? [];

        public GameEventScriptBinaryBindKind Kind { get; } = kind;

        public ushort Name { get; } = name;

        public IReadOnlyList<ushort> ArgumentNames => _argumentNames ?? [];

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
    Function = 0x11,
    Predicate = 0x12,
    ExtensionCall = 0x20,
    OutboundMessage = 0x21,
    ExternalType = 0x30
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
    Envelope = 0x12,
    
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
    [FieldOffset(4)] public ushort ExternalReferenceIndex;
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
    IndexedAccess = 0x1F,
    BindHandler = 0x20,

    LoadNothing = 0x21,
    LoadTrue = 0x22,
    LoadFalse = 0x23,
    LoadInteger = 0x24,
    LoadFloat = 0x25,
    LoadPercentage = 0x26,
    LoadText = 0x27,
    LoadTag = 0x28,
    LoadHandler = 0x29,
    LoadMessage = 0x2A,

    StageRegister = 0x2B,
    StageNothing = 0x2C,
    StageTrue = 0x2D,
    StageFalse = 0x2E,
    StageInteger = 0x2F,
    StageFloat = 0x30,
    StageText = 0x31,
    StageTag = 0x32,
    StagePercentage = 0x33,

    CreateDice = 0x34,
    CreateVector = 0x35,
    CreatePoint = 0x36,
    CreateList = 0x37,
    CreateMap = 0x38,
    CreateRange = 0x39,
    CreateRangeWithStep = 0x3A,
    CreateRangeIterator = 0x3B,
    CreateRangeIteratorWithStep = 0x3C,
    CreateRangeIteratorShort = 0x3D,
    CreateRecord = 0x3E,
    CreateExternalType = 0x3F,

    HasValue = 0x40,
    IsEmpty = 0x41,
    Default = 0x42,

    #endregion

    #region Group 2 - boolean algebra, comparison, math and random

    Or = 0x50,
    And = 0x51,
    Xor = 0x52,
    Implies = 0x53,
    Not = 0x54,
    Equal = 0x55,
    NotEqual = 0x56,
    ApproxEqual = 0x57,
    Less = 0x58,
    Greater = 0x59,
    LessOrEqual = 0x5A,
    GreaterOrEqual = 0x5B,
    Add = 0x5C,
    Subtract = 0x5D,
    Multiply = 0x5E,
    Divide = 0x5F,
    Power = 0x60,
    IntegerDivide = 0x61,
    Modulo = 0x62,
    Remainder = 0x63,
    Min = 0x64,
    Max = 0x65,
    Negate = 0x66,
    Abs = 0x67,
    LogN = 0x68,
    Chance = 0x69,
    Clamp = 0x6A,
    RandomTake = 0x6B,
    RandomPush = 0x6C,
    RandomPushConstant = 0x6D,
    RandomPop = 0x6E,
    SeriesTerm = 0x6F,
    SeriesTake = 0x70,
    SeriesDrop = 0x71,

    #endregion

    #region Group 3 - text, collection, streams

    Length = 0x90,
    StartsWith = 0x91,
    EndsWith = 0x92,
    Contains = 0x93,
    ContainsValue = 0x94,
    Intersect = 0x95,
    Combine = 0x96,
    Except = 0x97,
    Zip = 0x98,
    KeysOfMap = 0x99,
    ValuesOfMap = 0x9A,
    EntriesOfMap = 0x9B,
    StreamCreate = 0xA1,
    StreamNext = 0xA2,
    StreamClose = 0xA3,
    StreamReduce = 0xA4,
    StreamReduceOrDefault = 0xA5,
    StreamFold = 0xA6,
    StreamCollectList = 0xA7,
    StreamCollectMap = 0xA8,
    StreamCollectMapValue = 0xA9,
    StreamCollectFirst = 0xAA,
    StreamCollectLast = 0xAB,
    StreamCollectSingle = 0xAC,

    #endregion

    #region Group 4 - pipeline terminals and transforms

    PipelineStream = 0xC0,
    PipelineHasAny = 0xC5,
    PipelineHasAll = 0xC6,
    PipelineContainsSingle = 0xC7,
    PipelineContainsAny = 0xC8,
    PipelineContainsAll = 0xC9,
    PipelineDistinct = 0xCC,
    PipelineDistinctBy = 0xCD,
    PipelineGroupBy = 0xCE,
    PipelineReverse = 0xCF,
    PipelineSortAscending = 0xD0,
    PipelineSortDescending = 0xD1,
    PipelineOrderByAscending = 0xD2,
    PipelineOrderByDescending = 0xD3,
    PipelineTakeFirst = 0xD4,
    PipelineTakeLast = 0xD5,
    PipelineTakeHighest = 0xD6,
    PipelineTakeLowest = 0xD7,
    PipelineDropFirst = 0xD8,
    PipelineDropLast = 0xD9,
    PipelineDropHighest = 0xDA,
    PipelineDropLowest = 0xDB,
    PipelineShuffle = 0xDC,
    PipelineDraw = 0xDD,
    PipelineChoose = 0xDE,
    PipelineChooseRandom = 0xDF,
    PipelineChooseWeighted = 0xE0,
    PipelineDicePatternCountAny = 0xE1,
    PipelineDicePatternCountFace = 0xE2,
    PipelineDicePatternFullHouse = 0xE3,
    PipelineDicePatternStraight = 0xE4,
    PipelineTakePatternCountAny = 0xE5,
    PipelineTakePatternCountFace = 0xE6,
    PipelineTakePatternFullHouse = 0xE7,
    PipelineTakePatternStraight = 0xE8,
    PipelineListCreateBuilder = 0xE9,
    PipelineListBuilderAdd = 0xEA,
    PipelineListBuilderFinish = 0xEB

    #endregion
}
