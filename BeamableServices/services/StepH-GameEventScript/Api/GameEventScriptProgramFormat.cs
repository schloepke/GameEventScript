using System;

namespace StepH.GameEventScript.Api;

/// <summary>
/// Defines the supported game event script program retention values.
/// </summary>
public enum GameEventScriptProgramRetention
{
    /// <summary>
    /// Identifies the preserve all value.
    /// </summary>
    PreserveAll = 0,
    /// <summary>
    /// Identifies the preserve known value.
    /// </summary>
    PreserveKnown = 1,
    /// <summary>
    /// Identifies the runtime only value.
    /// </summary>
    RuntimeOnly = 2
}

/// <summary>
/// Represents a game event script program read limits.
/// </summary>
public sealed class GameEventScriptProgramReadLimits
{
    /// <summary>
    /// Gets the default.
    /// </summary>
    public static GameEventScriptProgramReadLimits Default { get; } = new();

    /// <summary>
    /// Gets the max file bytes.
    /// </summary>
    public int MaxFileBytes { get; init; } = 64 * 1024 * 1024;
    /// <summary>
    /// Gets the max section count.
    /// </summary>
    public int MaxSectionCount { get; init; } = 256;
    /// <summary>
    /// Gets the max source archive bytes.
    /// </summary>
    public int MaxSourceArchiveBytes { get; init; } = 32 * 1024 * 1024;
    /// <summary>
    /// Gets the max retained opaque bytes.
    /// </summary>
    public int MaxRetainedOpaqueBytes { get; init; } = 16 * 1024 * 1024;
    /// <summary>
    /// Gets the max instructions.
    /// </summary>
    public int MaxInstructions { get; init; } = ushort.MaxValue;
    /// <summary>
    /// Gets the max bindings.
    /// </summary>
    public int MaxBindings { get; init; } = ushort.MaxValue;
    /// <summary>
    /// Gets the max string entries.
    /// </summary>
    public int MaxStringEntries { get; init; } = ushort.MaxValue;
    /// <summary>
    /// Gets the max index lists.
    /// </summary>
    public int MaxIndexLists { get; init; } = ushort.MaxValue;
}

/// <summary>
/// Represents a game event script program read options.
/// </summary>
public sealed class GameEventScriptProgramReadOptions
{
    /// <summary>
    /// Gets the retention.
    /// </summary>
    public GameEventScriptProgramRetention Retention { get; init; } = GameEventScriptProgramRetention.PreserveAll;
    /// <summary>
    /// Gets the limits.
    /// </summary>
    public GameEventScriptProgramReadLimits Limits { get; init; } = GameEventScriptProgramReadLimits.Default;
}

/// <summary>
/// Defines the supported game event script program format error code values.
/// </summary>
public enum GameEventScriptProgramFormatErrorCode
{
    /// <summary>
    /// Identifies the invalid magic value.
    /// </summary>
    InvalidMagic = 1,
    /// <summary>
    /// Identifies the unsupported format version value.
    /// </summary>
    UnsupportedFormatVersion = 2,
    /// <summary>
    /// Identifies the invalid header flags value.
    /// </summary>
    InvalidHeaderFlags = 3,
    /// <summary>
    /// Identifies the invalid header size value.
    /// </summary>
    InvalidHeaderSize = 4,
    /// <summary>
    /// Identifies the file size mismatch value.
    /// </summary>
    FileSizeMismatch = 5,
    /// <summary>
    /// Identifies the file too large value.
    /// </summary>
    FileTooLarge = 6,
    /// <summary>
    /// Identifies the too many sections value.
    /// </summary>
    TooManySections = 7,
    /// <summary>
    /// Identifies the truncated section header value.
    /// </summary>
    TruncatedSectionHeader = 8,
    /// <summary>
    /// Identifies the truncated section payload value.
    /// </summary>
    TruncatedSectionPayload = 9,
    /// <summary>
    /// Identifies the section too large value.
    /// </summary>
    SectionTooLarge = 10,
    /// <summary>
    /// Identifies the invalid section type value.
    /// </summary>
    InvalidSectionType = 11,
    /// <summary>
    /// Identifies the invalid section flags value.
    /// </summary>
    InvalidSectionFlags = 12,
    /// <summary>
    /// Identifies the invalid section reserved value.
    /// </summary>
    InvalidSectionReserved = 13,
    /// <summary>
    /// Identifies the unsupported section version value.
    /// </summary>
    UnsupportedSectionVersion = 14,
    /// <summary>
    /// Identifies the unsupported compression value.
    /// </summary>
    UnsupportedCompression = 15,
    /// <summary>
    /// Identifies the missing required section value.
    /// </summary>
    MissingRequiredSection = 16,
    /// <summary>
    /// Identifies the duplicate section value.
    /// </summary>
    DuplicateSection = 17,
    /// <summary>
    /// Identifies the unknown required section value.
    /// </summary>
    UnknownRequiredSection = 18,
    /// <summary>
    /// Identifies the invalid payload length value.
    /// </summary>
    InvalidPayloadLength = 19,
    /// <summary>
    /// Identifies the invalid utf8 value.
    /// </summary>
    InvalidUtf8 = 20,
    /// <summary>
    /// Identifies the too many entries value.
    /// </summary>
    TooManyEntries = 21,
    /// <summary>
    /// Identifies the invalid string index value.
    /// </summary>
    InvalidStringIndex = 22,
    /// <summary>
    /// Identifies the invalid list index value.
    /// </summary>
    InvalidListIndex = 23,
    /// <summary>
    /// Identifies the invalid binding kind value.
    /// </summary>
    InvalidBindingKind = 24,
    /// <summary>
    /// Identifies the duplicate binding id value.
    /// </summary>
    DuplicateBindingId = 25,
    /// <summary>
    /// Identifies the invalid entry address value.
    /// </summary>
    InvalidEntryAddress = 26,
    /// <summary>
    /// Identifies the invalid opcode value.
    /// </summary>
    InvalidOpcode = 27,
    /// <summary>
    /// Identifies the invalid operand value.
    /// </summary>
    InvalidOperand = 28,
    /// <summary>
    /// Identifies the invalid jump address value.
    /// </summary>
    InvalidJumpAddress = 29,
    /// <summary>
    /// Identifies the invalid call address value.
    /// </summary>
    InvalidCallAddress = 30,
    /// <summary>
    /// Identifies the cyclic call graph value.
    /// </summary>
    CyclicCallGraph = 31,
    /// <summary>
    /// Identifies the invalid resource metadata value.
    /// </summary>
    InvalidResourceMetadata = 32,
    /// <summary>
    /// Identifies the invalid debug symbol value.
    /// </summary>
    InvalidDebugSymbol = 33,
    /// <summary>
    /// Identifies the invalid source map value.
    /// </summary>
    InvalidSourceMap = 34,
    /// <summary>
    /// Identifies the invalid source archive value.
    /// </summary>
    InvalidSourceArchive = 35,
    /// <summary>
    /// Identifies the source metadata mismatch value.
    /// </summary>
    SourceMetadataMismatch = 36,
    /// <summary>
    /// Identifies the reader limit exceeded value.
    /// </summary>
    ReaderLimitExceeded = 37,
    /// <summary>
    /// Identifies the invalid program value.
    /// </summary>
    InvalidProgram = 38
}

/// <summary>
/// Represents a game event script program format exception.
/// </summary>
public sealed class GameEventScriptProgramFormatException : Exception
{
    /// <summary>
    /// Initializes a new instance of Game Event Script Program Format Exception.
    /// </summary>
    /// <param name="errorCode">The error code value.</param>
    /// <param name="message">The message value.</param>
    /// <param name="byteOffset">The byte offset value.</param>
    /// <param name="sectionType">The section type value.</param>
    /// <param name="entryIndex">The entry index value.</param>
    /// <param name="innerException">The inner exception value.</param>
    public GameEventScriptProgramFormatException(GameEventScriptProgramFormatErrorCode errorCode, string message, long? byteOffset = null, ushort? sectionType = null, int? entryIndex = null, Exception? innerException = null)
        : base(message, innerException)
    {
        ErrorCode = errorCode;
        ByteOffset = byteOffset;
        SectionType = sectionType;
        EntryIndex = entryIndex;
        Diagnostic = new GameEventScriptDiagnostic(
            GameEventScriptDiagnosticPhase.Decode,
            GameEventScriptDiagnosticCodes.Decode(errorCode),
            message,
            TechnicalDetails: BuildTechnicalDetails(byteOffset, sectionType, entryIndex));
    }

    /// <summary>
    /// Gets the error code.
    /// </summary>
    public GameEventScriptProgramFormatErrorCode ErrorCode { get; }
    /// <summary>
    /// Gets the byte offset.
    /// </summary>
    public long? ByteOffset { get; }
    /// <summary>
    /// Gets the section type.
    /// </summary>
    public ushort? SectionType { get; }
    /// <summary>
    /// Gets the entry index.
    /// </summary>
    public int? EntryIndex { get; }
    /// <summary>
    /// Gets the diagnostic.
    /// </summary>
    public GameEventScriptDiagnostic Diagnostic { get; }

    private static string? BuildTechnicalDetails(long? byteOffset, ushort? sectionType, int? entryIndex)
    {
        if (byteOffset is null && sectionType is null && entryIndex is null) return null;
        return $"byteOffset={byteOffset?.ToString() ?? "-"};sectionType={sectionType?.ToString() ?? "-"};entryIndex={entryIndex?.ToString() ?? "-"}";
    }
}
