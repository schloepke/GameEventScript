#pragma warning disable CS1591

using System;

namespace StepH.GameEventScript.Api;

public enum GameEventScriptProgramRetention
{
    PreserveAll = 0,
    PreserveKnown = 1,
    RuntimeOnly = 2
}

public sealed class GameEventScriptProgramReadLimits
{
    public static GameEventScriptProgramReadLimits Default { get; } = new();

    public int MaxFileBytes { get; init; } = 64 * 1024 * 1024;
    public int MaxSectionCount { get; init; } = 256;
    public int MaxSourceArchiveBytes { get; init; } = 32 * 1024 * 1024;
    public int MaxRetainedOpaqueBytes { get; init; } = 16 * 1024 * 1024;
    public int MaxInstructions { get; init; } = ushort.MaxValue;
    public int MaxBindings { get; init; } = ushort.MaxValue;
    public int MaxStringEntries { get; init; } = ushort.MaxValue;
    public int MaxIndexLists { get; init; } = ushort.MaxValue;
}

public sealed class GameEventScriptProgramReadOptions
{
    public GameEventScriptProgramRetention Retention { get; init; } = GameEventScriptProgramRetention.PreserveAll;
    public GameEventScriptProgramReadLimits Limits { get; init; } = GameEventScriptProgramReadLimits.Default;
}

public enum GameEventScriptProgramFormatErrorCode
{
    InvalidMagic = 1,
    UnsupportedFormatVersion = 2,
    InvalidHeaderFlags = 3,
    InvalidHeaderSize = 4,
    FileSizeMismatch = 5,
    FileTooLarge = 6,
    TooManySections = 7,
    TruncatedSectionHeader = 8,
    TruncatedSectionPayload = 9,
    SectionTooLarge = 10,
    InvalidSectionType = 11,
    InvalidSectionFlags = 12,
    InvalidSectionReserved = 13,
    UnsupportedSectionVersion = 14,
    UnsupportedCompression = 15,
    MissingRequiredSection = 16,
    DuplicateSection = 17,
    UnknownRequiredSection = 18,
    InvalidPayloadLength = 19,
    InvalidUtf8 = 20,
    TooManyEntries = 21,
    InvalidStringIndex = 22,
    InvalidListIndex = 23,
    InvalidBindingKind = 24,
    DuplicateBindingId = 25,
    InvalidEntryAddress = 26,
    InvalidOpcode = 27,
    InvalidOperand = 28,
    InvalidJumpAddress = 29,
    InvalidCallAddress = 30,
    CyclicCallGraph = 31,
    InvalidResourceMetadata = 32,
    InvalidDebugSymbol = 33,
    InvalidSourceMap = 34,
    InvalidSourceArchive = 35,
    SourceMetadataMismatch = 36,
    ReaderLimitExceeded = 37,
    InvalidProgram = 38
}

public sealed class GameEventScriptProgramFormatException : Exception
{
    public GameEventScriptProgramFormatException(
        GameEventScriptProgramFormatErrorCode errorCode,
        string message,
        long? byteOffset = null,
        ushort? sectionType = null,
        int? entryIndex = null,
        Exception? innerException = null)
        : base(message, innerException)
    {
        ErrorCode = errorCode;
        ByteOffset = byteOffset;
        SectionType = sectionType;
        EntryIndex = entryIndex;
    }

    public GameEventScriptProgramFormatErrorCode ErrorCode { get; }
    public long? ByteOffset { get; }
    public ushort? SectionType { get; }
    public int? EntryIndex { get; }
}
