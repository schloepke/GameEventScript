#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections.Generic;
using System.Linq;
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

public enum GameEventScriptBytecodeTypeKind : ushort
{
    Invalid = 0,
    Nothing = 1,
    Boolean = 2,
    Integer = 3,
    Float = 4,
    Percentage = 5,
    Vector = 6,
    Point = 7,
    Uuid = 8,
    Series = 9,
    Envelope = 10,
    Ref = 11,
    Tag = 12,
    Text = 13,
    List = 14,
    Range = 15,
    Message = 16,
    Handler = 17,
    Map = 18,
    Dice = 19,
    Stream = 20,
    
    Custom = 0x7FFF,
}

[JsonConverter(typeof(GameEventScriptBytecodeInstructionJsonConverter))]
[StructLayout(LayoutKind.Explicit, Size = 16)]
public struct GameEventScriptBytecodeInstruction
{
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
    [FieldOffset(6)] public ushort ListIndex;
    [FieldOffset(6)] public ushort TypeOperand;

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
}

public enum GameEventScriptBytecodeOpCode : byte
{
    #region Group 1 - control, calls, messages, types, access

    Nop = 0x00,
    SlotLocals = 0x01,
    Jump = 0x02,
    JumpIfTrue = 0x03,
    JumpIfFalse = 0x04,
    JumpIfNotTrue = 0x05,
    Call = 0x06,
    CallPredicate = 0x07,
    CallStandard = 0x08,
    CallStandardPredicate = 0x09,
    CallExternal = 0x0A,
    CallExternalPredicate = 0x0B,
    ReturnVoid = 0x0C,
    ReturnValue = 0x0D,
    EmitMessage = 0x0E,
    EmitMessageWithTags = 0x0F,
    EmitMessageValue = 0x10,
    EmitMessageValueWithTags = 0x11,
    PublishMessage = 0x12,
    PublishMessageWithTags = 0x13,
    PublishMessageValue = 0x14,
    PublishMessageValueWithTags = 0x15,
    Cast = 0x16,
    CastCustom = 0x17,
    CastUnit = 0x18,
    TypeCheck = 0x19,
    TypeCheckCustom = 0x1A,
    CheckUnit = 0x1B,
    MoveSlot = 0x1C,
    MemberAccess = 0x1D,
    IndexedAccess = 0x1E,
    BindHandler = 0x1F,

    #endregion

    #region Group 2 - loads, staging, type construction

    LoadNothing = 0x20,
    LoadTrue = 0x21,
    LoadFalse = 0x22,
    LoadInteger = 0x23,
    LoadFloat = 0x24,
    LoadPercentage = 0x25,
    LoadText = 0x26,
    LoadTag = 0x27,
    LoadHandler = 0x28,
    LoadMessage = 0x29,
    StageRegister = 0x2A,
    StageNothing = 0x2B,
    StageTrue = 0x2C,
    StageFalse = 0x2D,
    StageInteger = 0x2E,
    StageFloat = 0x2F,
    StageText = 0x30,
    StageTag = 0x31,
    StagePercentage = 0x32,
    TypeConstructor = 0x33,
    CastNumeric = 0x34,
    TypeCheckNumeric = 0x35,

    #endregion

    #region Group 3 - boolean, comparison, presence

    Or = 0x40,
    And = 0x41,
    Xor = 0x42,
    Implies = 0x43,
    UnaryNot = 0x44,
    UnaryHasValue = 0x45,
    UnaryEmpty = 0x46,
    Equal = 0x47,
    NotEqual = 0x48,
    ApproxEqual = 0x49,
    Less = 0x4A,
    Greater = 0x4B,
    LessOrEqual = 0x4C,
    GreaterOrEqual = 0x4D,
    IntEqual = 0x4E,
    IntNotEqual = 0x4F,
    IntLess = 0x50,
    IntGreater = 0x51,
    IntLessOrEqual = 0x52,
    IntGreaterOrEqual = 0x53,
    Default = 0x54,

    #endregion

    #region Group 4 - math and random

    Add = 0x60,
    Subtract = 0x61,
    Multiply = 0x62,
    Divide = 0x63,
    Power = 0x64,
    IntegerDivide = 0x65,
    Modulo = 0x66,
    Remainder = 0x67,
    IntAdd = 0x68,
    IntSubtract = 0x69,
    IntMultiply = 0x6A,
    IntDivide = 0x6B,
    IntFloorDivide = 0x6C,
    IntModulo = 0x6D,
    IntRemainder = 0x6E,
    Min = 0x6F,
    Max = 0x70,
    UnaryNegate = 0x71,
    UnaryAbs = 0x72,
    UnaryNaturalLog = 0x73,
    UnaryChance = 0x74,
    Clamp = 0x75,
    Random = 0x76,
    RandomPush = 0x77,
    RandomPushConstant = 0x78,
    RandomPop = 0x79,

    #endregion

    #region Group 5 - text, collection, streams

    UnaryLength = 0x80,
    StartsWith = 0x81,
    EndsWith = 0x82,
    Contains = 0x83,
    ContainsValue = 0x84,
    Intersect = 0x85,
    Combine = 0x86,
    Except = 0x87,
    Zip = 0x88,
    UnaryKeys = 0x89,
    UnaryValues = 0x8A,
    UnaryEntries = 0x8B,
    Range = 0x8C,
    RangeWithStep = 0x8D,
    RangeIterator = 0x8E,
    RangeIteratorWithStep = 0x8F,
    RangeIteratorShort = 0x90,
    CollectionIterator = 0x91,
    StreamNext = 0x92,
    StreamClose = 0x93,
    StreamReduce = 0x94,
    StreamReduceOrDefault = 0x95,
    StreamFold = 0x96,
    SeriesTerm = 0x97,
    SeriesTake = 0x98,
    SeriesDrop = 0x99,
    PipelineIterator = 0x9A,

    #endregion

    #region Group 6 - collection and value building

    BuildList = 0xA0,
    BuildMap = 0xA1,
    CollectionBuilderList = 0xA3,
    CollectionBuilderAdd = 0xA4,
    CollectionBuilderFinish = 0xA5,
    Dice = 0xA6,

    #endregion

    #region Group 7 - pipeline terminals and transforms

    PipelineCollectList = 0xB0,
    PipelineFirst = 0xB1,
    PipelineLast = 0xB2,
    PipelineSingle = 0xB3,
    PipelineHasAny = 0xB4,
    PipelineHasAll = 0xB5,
    PipelineContainsSingle = 0xB6,
    PipelineContainsAny = 0xB7,
    PipelineContainsAll = 0xB8,
    PipelineMap = 0xB9,
    PipelineMapValue = 0xBA,
    PipelineDistinct = 0xBB,
    PipelineDistinctBy = 0xBC,
    PipelineGroupBy = 0xBD,
    PipelineReverse = 0xBE,
    PipelineSortAscending = 0xBF,
    PipelineSortDescending = 0xC0,
    PipelineOrderByAscending = 0xC1,
    PipelineOrderByDescending = 0xC2,
    PipelineTakeFirst = 0xC3,
    PipelineTakeLast = 0xC4,
    PipelineTakeHighest = 0xC5,
    PipelineTakeLowest = 0xC6,
    PipelineDropFirst = 0xC7,
    PipelineDropLast = 0xC8,
    PipelineDropHighest = 0xC9,
    PipelineDropLowest = 0xCA,
    PipelineShuffle = 0xCB,
    PipelineDraw = 0xCC,
    PipelineChoose = 0xCD,
    PipelineChooseRandom = 0xCE,
    PipelineChooseWeighted = 0xCF,
    PipelineDicePatternCountAny = 0xD0,
    PipelineDicePatternCountFace = 0xD1,
    PipelineDicePatternFullHouse = 0xD2,
    PipelineDicePatternStraight = 0xD3,
    PipelineTakePatternCountAny = 0xD4,
    PipelineTakePatternCountFace = 0xD5,
    PipelineTakePatternFullHouse = 0xD6,
    PipelineTakePatternStraight = 0xD7

    #endregion
}
