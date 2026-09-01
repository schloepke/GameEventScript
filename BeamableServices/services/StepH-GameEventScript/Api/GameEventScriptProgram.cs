#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections.Generic;
using System.Collections;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace StepH.GameEventScript.Api;

public sealed class GameEventScriptProgram
{
    internal GameEventScriptProgram(
        ushort formatVersion,
        string moduleName,
        ulong programVersion,
        ushort requiredRegisterCount,
        ushort requiredCallStackDepth,
        GameEventScriptStringConstantSegment stringConstantSegment,
        GameEventScriptUInt16IndexListSegment uint16IndexListSegment,
        GameEventScriptBindingSegment bindingSegment,
        GameEventScriptCodeSegment codeSegment,
        GameEventScriptDebugSymbolsSegment? debugSymbolsSegment = null,
        GameEventScriptSourceMapSegment? sourceMapSegment = null,
        GameEventScriptSourceArchiveSegment? sourceArchiveSegment = null,
        GameEventScriptBuildMetadataSegment? buildMetadataSegment = null,
        IReadOnlyList<GameEventScriptOpaqueSection>? opaqueSections = null)
    {
        FormatVersion = formatVersion;
        Metadata = new GameEventScriptProgramMetadataSegment(moduleName, requiredRegisterCount, requiredCallStackDepth, programVersion);
        StringConstants = stringConstantSegment;
        UInt16IndexLists = uint16IndexListSegment;
        Bindings = bindingSegment;
        Code = codeSegment;
        DebugSymbols = debugSymbolsSegment;
        SourceMap = sourceMapSegment;
        SourceArchive = sourceArchiveSegment;
        BuildMetadata = buildMetadataSegment;
        OpaqueSections = new GameEventScriptReadOnlyArray<GameEventScriptOpaqueSection>(opaqueSections);
    }

    public ushort FormatVersion { get; }
    public GameEventScriptProgramMetadataSegment Metadata { get; }
    public string ModuleName => Metadata.ModuleName;
    public ulong ProgramVersion => Metadata.ProgramVersion;
    public ushort RequiredRegisterCount => Metadata.RequiredRegisterCount;
    public ushort RequiredCallStackDepth => Metadata.RequiredCallStackDepth;
    public GameEventScriptStringConstantSegment StringConstants { get; }
    public GameEventScriptUInt16IndexListSegment UInt16IndexLists { get; }
    public GameEventScriptBindingSegment Bindings { get; }
    public GameEventScriptCodeSegment Code { get; }
    public GameEventScriptDebugSymbolsSegment? DebugSymbols { get; }
    public GameEventScriptSourceMapSegment? SourceMap { get; }
    public GameEventScriptSourceArchiveSegment? SourceArchive { get; }
    public GameEventScriptBuildMetadataSegment? BuildMetadata { get; }
    public GameEventScriptReadOnlyArray<GameEventScriptOpaqueSection> OpaqueSections { get; }
}

public readonly struct GameEventScriptProgramMetadataSegment
{
    public GameEventScriptProgramMetadataSegment(string moduleName, ushort requiredRegisterCount, ushort requiredCallStackDepth, ulong programVersion)
    {
        ModuleName = moduleName ?? string.Empty;
        RequiredRegisterCount = requiredRegisterCount;
        RequiredCallStackDepth = requiredCallStackDepth;
        ProgramVersion = programVersion;
    }

    public string ModuleName { get; }
    public ushort RequiredRegisterCount { get; }
    public ushort RequiredCallStackDepth { get; }
    public ulong ProgramVersion { get; }
}

/// <summary>
/// Immutable array view used by the portable program representation.
/// </summary>
public readonly struct GameEventScriptReadOnlyArray<T> : IReadOnlyList<T>
{
    private readonly T[]? _items;

    public GameEventScriptReadOnlyArray(IReadOnlyList<T>? items)
    {
        if (items is null || items.Count == 0)
        {
            _items = [];
            return;
        }

        _items = new T[items.Count];
        for (var index = 0; index < items.Count; index++)
        {
            _items[index] = items[index];
        }
    }

    public int Count => _items?.Length ?? 0;
    public int Length => Count;
    public T this[int index] => (_items ?? [])[index];
    public IEnumerator<T> GetEnumerator() => ((IEnumerable<T>)(_items ?? [])).GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    internal ReadOnlySpan<T> AsSpan() => _items;

    internal string DecodeUtf8(Encoding encoding, int start, int length)
    {
        if (typeof(T) != typeof(byte)) throw new InvalidOperationException("Only byte arrays can be decoded as UTF-8.");
        var bytes = (byte[])(object)(_items ?? Array.Empty<T>());
        return encoding.GetString(bytes, start, length);
    }
}

public static class GameEventScriptBinaryFormat
{
    public const ushort Version = 1;
    public const uint HeaderSize = 16;
    public const uint SectionHeaderSize = 12;
    public const byte MagicG = (byte)'G';
    public const byte MagicE = (byte)'E';
    public const byte MagicS = (byte)'S';
    public const byte MagicB = (byte)'B';
}

[Flags]
public enum GameEventScriptSectionFlags : ushort
{
    None = 0,
    Required = 0x0001,
    CompressionMask = 0x00F0
}

public enum GameEventScriptSectionType : ushort
{
    ProgramMetadata = 0x0001,
    StringConstants = 0x0002,
    UInt16IndexLists = 0x0003,
    Bindings = 0x0004,
    Code = 0x0010,
    DebugSymbols = 0x0020,
    SourceMap = 0x0021,
    SourceArchive = 0x0022,
    BuildMetadata = 0x0030,
    ReservedSignature = 0x0040,
    NamedCustom = 0xFFFE
}

[Flags]
public enum GameEventScriptDebugInfoOptions : byte
{
    None = 0,
    DebugSymbols = 1 << 0,
    SourceMap = 1 << 1,
    SourceArchive = 1 << 2,
    All = DebugSymbols | SourceMap | SourceArchive
}

public readonly struct GameEventScriptStringConstantSegment
{
    public readonly struct SliceEntry
    {
        public int Start { get; init; }
        public int Length { get; init; }
    }

    private readonly GameEventScriptReadOnlyArray<SliceEntry> _slices;
    private readonly GameEventScriptReadOnlyArray<byte> _data;

    public GameEventScriptStringConstantSegment(IReadOnlyList<SliceEntry> slices, IReadOnlyList<byte> data)
    {
        _slices = new GameEventScriptReadOnlyArray<SliceEntry>(slices);
        _data = new GameEventScriptReadOnlyArray<byte>(data);
    }

    public GameEventScriptReadOnlyArray<SliceEntry> Slices => _slices;
    public GameEventScriptReadOnlyArray<byte> Data => _data;

    public int ResolveSize(ushort index) => _slices[index].Length;
    public string Resolve(ushort index)
    {
        var slice = _slices[index];
        return _data.DecodeUtf8(Encoding.UTF8, slice.Start, slice.Length);
    }
}

public readonly struct GameEventScriptUInt16IndexList
{
    private readonly GameEventScriptReadOnlyArray<ushort> _data;

    internal GameEventScriptUInt16IndexList(GameEventScriptReadOnlyArray<ushort> data, int start, int length)
    {
        _data = data;
        Start = start;
        Length = length;
    }

    public int Start { get; }

    public int Length { get; }

    public ushort this[int index] => _data[Start + index];
}

public readonly struct GameEventScriptUInt16IndexListSegment
{
    public readonly struct SliceEntry
    {
        public int Start { get; init; }
        public int Length { get; init; }
    }

    private readonly GameEventScriptReadOnlyArray<SliceEntry> _slices;
    private readonly GameEventScriptReadOnlyArray<ushort> _data;

    public GameEventScriptUInt16IndexListSegment(IReadOnlyList<SliceEntry> slices, IReadOnlyList<ushort> data)
    {
        _slices = new GameEventScriptReadOnlyArray<SliceEntry>(slices);
        _data = new GameEventScriptReadOnlyArray<ushort>(data);
    }

    public GameEventScriptReadOnlyArray<SliceEntry> Slices => _slices;
    public GameEventScriptReadOnlyArray<ushort> Data => _data;

    public GameEventScriptUInt16IndexList Resolve(ushort index) => new(_data, _slices[index].Start, _slices[index].Length);
}

public readonly struct GameEventScriptBindingSegment
{
    
    public readonly struct GameEventScriptBinaryBindEntry(
        GameEventScriptBinaryBindKind kind,
        ushort name,
        IReadOnlyList<ushort>? argumentNames,
        ushort entryAddress = 0xFFFF,
        ushort id = 0xFFFF,
        IReadOnlyList<ushort>? requiredTags = null,
        IReadOnlyList<ushort>? excludedTags = null,
        ushort requiredRegisterCount = 0,
        ushort requiredCallStackDepth = 0)
    {
        private readonly GameEventScriptReadOnlyArray<ushort> _argumentNames = new(argumentNames);
        private readonly GameEventScriptReadOnlyArray<ushort> _requiredTags = new(requiredTags);
        private readonly GameEventScriptReadOnlyArray<ushort> _excludedTags = new(excludedTags);

        public ushort Id { get; } = id;

        public GameEventScriptBinaryBindKind Kind { get; } = kind;

        public ushort Name { get; } = name;

        public GameEventScriptReadOnlyArray<ushort> ArgumentNames => _argumentNames;

        public GameEventScriptReadOnlyArray<ushort> RequiredTags => _requiredTags;

        public GameEventScriptReadOnlyArray<ushort> ExcludedTags => _excludedTags;

        public ushort EntryAddress { get; } = entryAddress;

        public ushort RequiredRegisterCount { get; } = requiredRegisterCount;

        public ushort RequiredCallStackDepth { get; } = requiredCallStackDepth;

    }

    private readonly GameEventScriptReadOnlyArray<GameEventScriptBinaryBindEntry> _entries;

    public GameEventScriptBindingSegment(IReadOnlyList<GameEventScriptBinaryBindEntry>? entries)
    {
        _entries = new GameEventScriptReadOnlyArray<GameEventScriptBinaryBindEntry>(entries);
        EntryCount = ToEntryCount(_entries.Count);
    }

    public ushort EntryCount { get; }

    public GameEventScriptReadOnlyArray<GameEventScriptBinaryBindEntry> Entries => _entries;

    private static ushort ToEntryCount(int count) => count > ushort.MaxValue ? throw new ArgumentOutOfRangeException(nameof(count), "GameEventScriptProgram tables cannot exceed 65535 entries.") : checked((ushort)count);

}

public readonly struct GameEventScriptCodeSegment
{
    private readonly GameEventScriptReadOnlyArray<GameEventScriptBytecodeInstruction> _instructions;

    public GameEventScriptCodeSegment(IReadOnlyList<GameEventScriptBytecodeInstruction>? instructions)
        => _instructions = new GameEventScriptReadOnlyArray<GameEventScriptBytecodeInstruction>(instructions);

    public int Count => _instructions.Count;
    public int Length => _instructions.Count;
    public GameEventScriptBytecodeInstruction this[int index] => _instructions[index];
    public GameEventScriptReadOnlyArray<GameEventScriptBytecodeInstruction> Instructions => _instructions;
}

public enum GameEventScriptDebugSymbolKind : byte
{
    Parameter = 1,
    Local = 2
}

public readonly record struct GameEventScriptDebugSymbol(
    GameEventScriptDebugSymbolKind Kind,
    ushort RegisterId,
    string Name,
    uint CodeStart,
    uint CodeLength);

public sealed class GameEventScriptDebugSymbolsSegment
{
    public GameEventScriptDebugSymbolsSegment(IReadOnlyList<GameEventScriptDebugSymbol>? symbols)
        => Symbols = new GameEventScriptReadOnlyArray<GameEventScriptDebugSymbol>(symbols);

    public GameEventScriptReadOnlyArray<GameEventScriptDebugSymbol> Symbols { get; }
}

public sealed class GameEventScriptSourceMapSource
{
    public GameEventScriptSourceMapSource(uint sourceId, string sourceName, uint sourceByteLength, IReadOnlyList<byte> sha256, IReadOnlyList<uint> lineStartByteOffsets)
    {
        SourceId = sourceId;
        SourceName = sourceName ?? string.Empty;
        SourceByteLength = sourceByteLength;
        Sha256 = new GameEventScriptReadOnlyArray<byte>(sha256);
        LineStartByteOffsets = new GameEventScriptReadOnlyArray<uint>(lineStartByteOffsets);
    }

    public uint SourceId { get; }
    public string SourceName { get; }
    public uint SourceByteLength { get; }
    public GameEventScriptReadOnlyArray<byte> Sha256 { get; }
    public GameEventScriptReadOnlyArray<uint> LineStartByteOffsets { get; }
}

public readonly record struct GameEventScriptSourceMapEntry(
    uint CodeStart,
    uint CodeLength,
    uint SourceId,
    uint SourceStartByteOffset,
    uint SourceByteLength);

public sealed class GameEventScriptSourceMapSegment
{
    public GameEventScriptSourceMapSegment(IReadOnlyList<GameEventScriptSourceMapSource>? sources, IReadOnlyList<GameEventScriptSourceMapEntry>? entries)
    {
        Sources = new GameEventScriptReadOnlyArray<GameEventScriptSourceMapSource>(sources);
        Entries = new GameEventScriptReadOnlyArray<GameEventScriptSourceMapEntry>(entries);
    }

    public GameEventScriptReadOnlyArray<GameEventScriptSourceMapSource> Sources { get; }
    public GameEventScriptReadOnlyArray<GameEventScriptSourceMapEntry> Entries { get; }
}

public sealed class GameEventScriptSourceArchiveEntry
{
    private readonly GameEventScriptReadOnlyArray<byte> _utf8Content;

    public GameEventScriptSourceArchiveEntry(uint sourceId, string sourceName, IReadOnlyList<byte> utf8Content)
    {
        SourceId = sourceId;
        SourceName = sourceName ?? string.Empty;
        _utf8Content = new GameEventScriptReadOnlyArray<byte>(utf8Content);
    }

    public uint SourceId { get; }
    public string SourceName { get; }
    public GameEventScriptReadOnlyArray<byte> Utf8Content => _utf8Content;
    public string ResolveText() => DecodeText(Encoding.UTF8);

    internal string DecodeText(Encoding encoding) => _utf8Content.DecodeUtf8(encoding, 0, _utf8Content.Count);
}

public sealed class GameEventScriptSourceArchiveSegment
{
    public GameEventScriptSourceArchiveSegment(IReadOnlyList<GameEventScriptSourceArchiveEntry>? sources)
        => Sources = new GameEventScriptReadOnlyArray<GameEventScriptSourceArchiveEntry>(sources);

    public GameEventScriptReadOnlyArray<GameEventScriptSourceArchiveEntry> Sources { get; }
}

public sealed class GameEventScriptBuildMetadataSegment
{
    public GameEventScriptBuildMetadataSegment(string compilerId, string compilerVersion)
    {
        CompilerId = compilerId ?? string.Empty;
        CompilerVersion = compilerVersion ?? string.Empty;
    }

    public string CompilerId { get; }
    public string CompilerVersion { get; }
}

public sealed class GameEventScriptOpaqueSection
{
    private readonly GameEventScriptReadOnlyArray<byte> _rawPayload;

    public GameEventScriptOpaqueSection(ushort sectionType, GameEventScriptSectionFlags flags, ushort sectionVersion, IReadOnlyList<byte> rawPayload, int originalOrdinal)
    {
        SectionType = sectionType;
        Flags = flags;
        SectionVersion = sectionVersion;
        _rawPayload = new GameEventScriptReadOnlyArray<byte>(rawPayload);
        OriginalOrdinal = originalOrdinal;
    }

    public ushort SectionType { get; }
    public GameEventScriptSectionFlags Flags { get; }
    public ushort SectionVersion { get; }
    public GameEventScriptReadOnlyArray<byte> RawPayload => _rawPayload;
    public int OriginalOrdinal { get; }
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
    DistinctBuilder = 0x25,
    GroupBuilder = 0x26,
    OrderBuilder = 0x27,

    Iterator = 0x30,
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

public enum GameEventScriptBytecodeSeriesKind : ushort
{
    Fibonacci = 0x01,
    Factorial = 0x02,
}

[StructLayout(LayoutKind.Explicit, Size = 16)]
public struct GameEventScriptBytecodeInstruction
{
    private const byte UnitMask = 0x1F;
    private const byte InstructionFlagMask = 0xE0;

    [FieldOffset(0)] public GameEventScriptBytecodeOpCode OpCode;

    [FieldOffset(1)] public byte UnitAndFlags;

    [FieldOffset(2)] public ushort DestinationRegister;
    [FieldOffset(2)] public ushort MessageDestination;
    
    [FieldOffset(4)] public short ImmediateX;
    [FieldOffset(4)] public ushort ConditionRegister;
    [FieldOffset(4)] public ushort XRegister;
    [FieldOffset(4)] public ushort StringIndex;
    [FieldOffset(4)] public ushort SecondaryListIndex;
    [FieldOffset(4)] public ushort BindId;
    [FieldOffset(4)] public ushort Index;
    [FieldOffset(4)] public short Count;

    [FieldOffset(6)] public short ImmediateY;
    [FieldOffset(6)] public ushort TargetAddress;
    [FieldOffset(6)] public ushort YRegister;
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
    RegisterLocals = 0x01,
    Jump = 0x02,
    JumpIfTrue = 0x03,
    JumpIfFalse = 0x04,
    JumpIfNotTrue = 0x05,
    JumpIfNothing = 0x06,

    Call = 0x07,
    CreateSeries = 0x08,
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
    CreateRecordValue = 0x41,
    CreateExternalType = 0x42,

    HasValue = 0x43,
    IsEmpty = 0x44,
    Default = 0x45,

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

    #region Group 3 - text, collection, iterators

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
    IteratorCreate = 0xBE,
    IteratorCreateOrJump = 0xBF,
    IteratorNext = 0xC0,
    IteratorClose = 0xC1,
    Distinct = 0xC2,
    SortAscending = 0xC3,
    SortDescending = 0xC4,
    Reverse = 0xC5,
    Shuffle = 0xC6,
    ListBuilderCreate = 0xC7,
    ListBuilderAdd = 0xC8,
    ListBuilderFinish = 0xC9,
    MapBuilderCreate = 0xCA,
    MapBuilderAdd = 0xCB,
    MapBuilderFinish = 0xCC,
    DistinctBuilderCreate = 0xCD,
    DistinctBuilderAdd = 0xCE,
    DistinctBuilderFinish = 0xCF,
    GroupBuilderCreate = 0xD0,
    GroupBuilderAdd = 0xD1,
    GroupBuilderFinish = 0xD2,
    OrderBuilderCreate = 0xD3,
    OrderBuilderAdd = 0xD4,
    OrderBuilderFinishAscending = 0xD5,
    OrderBuilderFinishDescending = 0xD6,
    HasPattern = 0xD7,
    TakePattern = 0xD8,

    #endregion
}
