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
    Series = 8,
    Envelope = 9,
    Tag = 10,
    Text = 11,
    List = 12,
    Range = 13,
    Message = 14,
    Handler = 15,
    Map = 16,
    Dice = 17,
    Stream = 18,
    
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
    CastNumeric = 0x19,
    CheckType = 0x1A,
    CheckCustomType = 0x1B,
    CheckUnit = 0x1C,
    CheckNumeric = 0x1D,
    CheckInteger = 0x22,
    CheckFractional = 0x23,
    MoveSlot = 0x1E,
    MemberAccess = 0x1F,
    IndexedAccess = 0x20,
    BindHandler = 0x21,

    #endregion

    #region Group 2 - loads, staging, type construction

    LoadNothing = 0x30,
    LoadTrue = 0x31,
    LoadFalse = 0x32,
    LoadInteger = 0x33,
    LoadFloat = 0x34,
    LoadPercentage = 0x35,
    LoadText = 0x36,
    LoadTag = 0x37,
    LoadHandler = 0x38,
    LoadMessage = 0x39,
    StageRegister = 0x3A,
    StageNothing = 0x3B,
    StageTrue = 0x3C,
    StageFalse = 0x3D,
    StageInteger = 0x3E,
    StageFloat = 0x3F,
    StageText = 0x40,
    StageTag = 0x41,
    StagePercentage = 0x42,
    TypeConstructor = 0x43,
    CreateVector = 0x44,
    CreatePoint = 0x45,

    #endregion

    #region Group 3 - boolean, comparison, presence

    Or = 0x50,
    And = 0x51,
    Xor = 0x52,
    Implies = 0x53,
    UnaryNot = 0x54,
    UnaryHasValue = 0x55,
    UnaryEmpty = 0x56,
    Equal = 0x57,
    NotEqual = 0x58,
    ApproxEqual = 0x59,
    Less = 0x5A,
    Greater = 0x5B,
    LessOrEqual = 0x5C,
    GreaterOrEqual = 0x5D,
    Default = 0x64,

    #endregion

    #region Group 4 - math and random

    Add = 0x70,
    Subtract = 0x71,
    Multiply = 0x72,
    Divide = 0x73,
    Power = 0x74,
    IntegerDivide = 0x75,
    Modulo = 0x76,
    Remainder = 0x77,
    Min = 0x7F,
    Max = 0x80,
    UnaryNegate = 0x81,
    UnaryAbs = 0x82,
    UnaryNaturalLog = 0x83,
    UnaryChance = 0x84,
    Clamp = 0x85,
    Random = 0x86,
    RandomPush = 0x87,
    RandomPushConstant = 0x88,
    RandomPop = 0x89,

    #endregion

    #region Group 5 - text, collection, streams

    UnaryLength = 0x90,
    StartsWith = 0x91,
    EndsWith = 0x92,
    Contains = 0x93,
    ContainsValue = 0x94,
    Intersect = 0x95,
    Combine = 0x96,
    Except = 0x97,
    Zip = 0x98,
    UnaryKeys = 0x99,
    UnaryValues = 0x9A,
    UnaryEntries = 0x9B,
    Range = 0x9C,
    RangeWithStep = 0x9D,
    RangeIterator = 0x9E,
    RangeIteratorWithStep = 0x9F,
    RangeIteratorShort = 0xA0,
    CollectionIterator = 0xA1,
    StreamNext = 0xA2,
    StreamClose = 0xA3,
    StreamReduce = 0xA4,
    StreamReduceOrDefault = 0xA5,
    StreamFold = 0xA6,
    SeriesTerm = 0xA7,
    SeriesTake = 0xA8,
    SeriesDrop = 0xA9,
    PipelineIterator = 0xAA,

    #endregion

    #region Group 6 - collection and value building

    BuildList = 0xB0,
    BuildMap = 0xB1,
    CollectionBuilderList = 0xB3,
    CollectionBuilderAdd = 0xB4,
    CollectionBuilderFinish = 0xB5,
    Dice = 0xB6,

    #endregion

    #region Group 7 - pipeline terminals and transforms

    PipelineCollectList = 0xC0,
    PipelineFirst = 0xC1,
    PipelineLast = 0xC2,
    PipelineSingle = 0xC3,
    PipelineHasAny = 0xC4,
    PipelineHasAll = 0xC5,
    PipelineContainsSingle = 0xC6,
    PipelineContainsAny = 0xC7,
    PipelineContainsAll = 0xC8,
    PipelineMap = 0xC9,
    PipelineMapValue = 0xCA,
    PipelineDistinct = 0xCB,
    PipelineDistinctBy = 0xCC,
    PipelineGroupBy = 0xCD,
    PipelineReverse = 0xCE,
    PipelineSortAscending = 0xCF,
    PipelineSortDescending = 0xD0,
    PipelineOrderByAscending = 0xD1,
    PipelineOrderByDescending = 0xD2,
    PipelineTakeFirst = 0xD3,
    PipelineTakeLast = 0xD4,
    PipelineTakeHighest = 0xD5,
    PipelineTakeLowest = 0xD6,
    PipelineDropFirst = 0xD7,
    PipelineDropLast = 0xD8,
    PipelineDropHighest = 0xD9,
    PipelineDropLowest = 0xDA,
    PipelineShuffle = 0xDB,
    PipelineDraw = 0xDC,
    PipelineChoose = 0xDD,
    PipelineChooseRandom = 0xDE,
    PipelineChooseWeighted = 0xDF,
    PipelineDicePatternCountAny = 0xE0,
    PipelineDicePatternCountFace = 0xE1,
    PipelineDicePatternFullHouse = 0xE2,
    PipelineDicePatternStraight = 0xE3,
    PipelineTakePatternCountAny = 0xE4,
    PipelineTakePatternCountFace = 0xE5,
    PipelineTakePatternFullHouse = 0xE6,
    PipelineTakePatternStraight = 0xE7

    #endregion
}
