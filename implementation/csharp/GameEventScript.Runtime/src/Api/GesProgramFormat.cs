// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;

namespace GameEventScript.Api;

/// <summary>
/// Selects which optional sections <see cref="GameEventScriptProgram.Read"/> keeps. The five required runtime sections
/// are always read, and an unknown section flagged as required always fails.
/// </summary>
public enum GesProgramRetention
{
    /// <summary>
    /// Parses supported known optional sections and retains every other optional section, unknown or with an unsupported
    /// version or compression, as a <see cref="GesOpaqueSection"/>. This is the default.
    /// </summary>
    PreserveAll = 0,
    /// <summary>
    /// Parses supported known optional sections and drops all other optional sections.
    /// </summary>
    PreserveKnown = 1,
    /// <summary>
    /// Reads only the five required runtime sections; debug symbols, source map, source archive, build metadata and opaque
    /// sections are dropped.
    /// </summary>
    RuntimeOnly = 2
}

/// <summary>
/// Resource bounds that <see cref="GameEventScriptProgram.Read"/> checks before allocating. Exceeding a count limit fails
/// with <see cref="GesProgramFormatErrorCode.TooManyEntries"/>, a byte limit with the code named on the property; a
/// limit outside its supported range makes Read throw <see cref="ArgumentOutOfRangeException"/>.
/// </summary>
public sealed class GesProgramReadLimits
{
    /// <summary>
    /// Gets the shared instance with the specification's default limits.
    /// </summary>
    public static GesProgramReadLimits Default { get; } = new();

    /// <summary>
    /// Gets the maximum size of the whole <c>.gesb</c> image in bytes (default 64 MiB, at least
    /// <see cref="GesBinaryFormat.HeaderSize"/>); larger images fail with <see cref="GesProgramFormatErrorCode.FileTooLarge"/>.
    /// </summary>
    public int MaxFileBytes { get; init; } = 64 * 1024 * 1024;
    /// <summary>
    /// Gets the maximum number of sections (default 256, at least 1); more fail with
    /// <see cref="GesProgramFormatErrorCode.TooManySections"/>.
    /// </summary>
    public int MaxSectionCount { get; init; } = 256;
    /// <summary>
    /// Gets the maximum total UTF-8 content bytes of all SourceArchive entries (default 32 MiB, nonnegative); more fail
    /// with <see cref="GesProgramFormatErrorCode.ReaderLimitExceeded"/>.
    /// </summary>
    public int MaxSourceArchiveBytes { get; init; } = 32 * 1024 * 1024;
    /// <summary>
    /// Gets the maximum total payload bytes retained as <see cref="GesOpaqueSection"/> under
    /// <see cref="GesProgramRetention.PreserveAll"/> (default 16 MiB, nonnegative); more fail with
    /// <see cref="GesProgramFormatErrorCode.ReaderLimitExceeded"/>.
    /// </summary>
    public int MaxRetainedOpaqueBytes { get; init; } = 16 * 1024 * 1024;
    /// <summary>
    /// Gets the maximum CodeSegment instruction count, from 0 to 65,535 (the default and V1 maximum).
    /// </summary>
    public int MaxInstructions { get; init; } = ushort.MaxValue;
    /// <summary>
    /// Gets the maximum BindingSegment entry count, from 0 to 65,535 (the default and V1 maximum).
    /// </summary>
    public int MaxBindings { get; init; } = ushort.MaxValue;
    /// <summary>
    /// Gets the maximum StringConstantSegment entry count, from 0 to 65,535 (the default and V1 maximum).
    /// </summary>
    public int MaxStringEntries { get; init; } = ushort.MaxValue;
    /// <summary>
    /// Gets the maximum UInt16IndexListSegment list count, from 0 to 65,535 (the default and V1 maximum).
    /// </summary>
    public int MaxIndexLists { get; init; } = ushort.MaxValue;
}

/// <summary>
/// Options for <see cref="GameEventScriptProgram.Read"/>.
/// </summary>
public sealed class GesProgramReadOptions
{
    /// <summary>
    /// Gets which optional sections to keep; the default is <see cref="GesProgramRetention.PreserveAll"/>.
    /// </summary>
    public GesProgramRetention Retention { get; init; } = GesProgramRetention.PreserveAll;
    /// <summary>
    /// Gets the resource bounds; the default is <see cref="GesProgramReadLimits.Default"/>. It must not be
    /// <see langword="null"/>.
    /// </summary>
    public GesProgramReadLimits Limits { get; init; } = GesProgramReadLimits.Default;
}

/// <summary>
/// The stable portable error codes of <see cref="GesProgramFormatException"/>. The numeric values are fixed by the
/// binary format specification; the diagnostic code is <c>decode.</c> followed by the camel-case name.
/// </summary>
public enum GesProgramFormatErrorCode
{
    /// <summary>
    /// The image does not start with ASCII <c>GESB</c>.
    /// </summary>
    InvalidMagic = 1,
    /// <summary>
    /// The header format version is not <see cref="GesBinaryFormat.Version"/>.
    /// </summary>
    UnsupportedFormatVersion = 2,
    /// <summary>
    /// The reserved header flags are not zero.
    /// </summary>
    InvalidHeaderFlags = 3,
    /// <summary>
    /// The header size is not <see cref="GesBinaryFormat.HeaderSize"/>, or the image is shorter than the header.
    /// </summary>
    InvalidHeaderSize = 4,
    /// <summary>
    /// The header file size differs from the supplied byte count.
    /// </summary>
    FileSizeMismatch = 5,
    /// <summary>
    /// The image exceeds <see cref="GesProgramReadLimits.MaxFileBytes"/>.
    /// </summary>
    FileTooLarge = 6,
    /// <summary>
    /// The image exceeds <see cref="GesProgramReadLimits.MaxSectionCount"/>.
    /// </summary>
    TooManySections = 7,
    /// <summary>
    /// A section header extends beyond the image.
    /// </summary>
    TruncatedSectionHeader = 8,
    /// <summary>
    /// A section payload, or a list within it, extends beyond its bounds.
    /// </summary>
    TruncatedSectionPayload = 9,
    /// <summary>
    /// A section, a length or the encoded program exceeds the representable V1 or in-memory size range.
    /// </summary>
    SectionTooLarge = 10,
    /// <summary>
    /// A section uses the invalid type <c>0x0000</c> or <c>0xFFFF</c>.
    /// </summary>
    InvalidSectionType = 11,
    /// <summary>
    /// Section or record flags contain unassigned bits, or a required runtime section lacks the Required flag.
    /// </summary>
    InvalidSectionFlags = 12,
    /// <summary>
    /// A reserved section-header or payload field is not zero.
    /// </summary>
    InvalidSectionReserved = 13,
    /// <summary>
    /// A required section has an unsupported section version.
    /// </summary>
    UnsupportedSectionVersion = 14,
    /// <summary>
    /// A required section uses a compression codec other than zero.
    /// </summary>
    UnsupportedCompression = 15,
    /// <summary>
    /// One of the five required runtime sections is missing.
    /// </summary>
    MissingRequiredSection = 16,
    /// <summary>
    /// A singleton known section occurs more than once, or is both parsed and opaque.
    /// </summary>
    DuplicateSection = 17,
    /// <summary>
    /// An unknown section, or a known optional one, carries the Required flag.
    /// </summary>
    UnknownRequiredSection = 18,
    /// <summary>
    /// A payload length does not match its fixed size or declared entry count.
    /// </summary>
    InvalidPayloadLength = 19,
    /// <summary>
    /// A string, name or source payload is not strict UTF-8.
    /// </summary>
    InvalidUtf8 = 20,
    /// <summary>
    /// An entry count exceeds its configured or V1 limit.
    /// </summary>
    TooManyEntries = 21,
    /// <summary>
    /// A string-constant index is out of range.
    /// </summary>
    InvalidStringIndex = 22,
    /// <summary>
    /// An index-list reference is out of range or missing.
    /// </summary>
    InvalidListIndex = 23,
    /// <summary>
    /// A binding uses an unassigned kind.
    /// </summary>
    InvalidBindingKind = 24,
    /// <summary>
    /// A binding ID is duplicated within its binding scope.
    /// </summary>
    DuplicateBindingId = 25,
    /// <summary>
    /// An executable binding has an invalid entry address, or a non-executable one lacks the <c>0xFFFF</c> sentinel.
    /// </summary>
    InvalidEntryAddress = 26,
    /// <summary>
    /// An instruction uses an unassigned opcode.
    /// </summary>
    InvalidOpcode = 27,
    /// <summary>
    /// An instruction operand, unit or flag is invalid for its opcode.
    /// </summary>
    InvalidOperand = 28,
    /// <summary>
    /// An instruction references an address outside the code segment.
    /// </summary>
    InvalidJumpAddress = 29,
    /// <summary>
    /// The call graph references an invalid call target.
    /// </summary>
    InvalidCallAddress = 30,
    /// <summary>
    /// The synchronous call graph contains a cycle.
    /// </summary>
    CyclicCallGraph = 31,
    /// <summary>
    /// Declared register or call-depth requirements are lower than the code requires, or inconsistent.
    /// </summary>
    InvalidResourceMetadata = 32,
    /// <summary>
    /// A debug symbol is outside its code or register bounds.
    /// </summary>
    InvalidDebugSymbol = 33,
    /// <summary>
    /// A source-map source or mapping is malformed or out of bounds.
    /// </summary>
    InvalidSourceMap = 34,
    /// <summary>
    /// A source-archive entry has a duplicate ID, an invalid name or a byte order mark.
    /// </summary>
    InvalidSourceArchive = 35,
    /// <summary>
    /// The source map and the source archive disagree on IDs, names, lengths, hashes or line boundaries.
    /// </summary>
    SourceMetadataMismatch = 36,
    /// <summary>
    /// A configured byte limit for the source archive or retained opaque sections is exceeded.
    /// </summary>
    ReaderLimitExceeded = 37,
    /// <summary>
    /// Another program invariant is violated.
    /// </summary>
    InvalidProgram = 38
}

/// <summary>
/// Thrown when a <c>.gesb</c> image or a program is malformed, unsupported, exceeds read limits, or violates a program
/// invariant. Raised by reading, writing, size calculation, validation and <c>Host.Load</c>.
/// </summary>
public sealed class GesProgramFormatException : Exception
{
    /// <summary>
    /// Creates the exception and its <see cref="Diagnostic"/> in the decode phase.
    /// </summary>
    /// <param name="errorCode">The stable error code.</param>
    /// <param name="message">The human-readable message; callers must not depend on its text.</param>
    /// <param name="byteOffset">The zero-based byte offset in the image where the problem was found, if known.</param>
    /// <param name="sectionType">The raw type of the affected section, if known.</param>
    /// <param name="entryIndex">The zero-based index of the affected entry within its section, if known.</param>
    /// <param name="innerException">The underlying exception, if any.</param>
    public GesProgramFormatException(GesProgramFormatErrorCode errorCode, string message, long? byteOffset = null, ushort? sectionType = null, int? entryIndex = null, Exception? innerException = null)
        : base(message, innerException)
    {
        ErrorCode = errorCode;
        ByteOffset = byteOffset;
        SectionType = sectionType;
        EntryIndex = entryIndex;
        Diagnostic = new GesDiagnostic(
            GesDiagnosticPhase.Decode,
            GesDiagnosticCodes.Decode(errorCode),
            message,
            TechnicalDetails: BuildTechnicalDetails(byteOffset, sectionType, entryIndex));
    }

    /// <summary>
    /// Gets the stable portable error code.
    /// </summary>
    public GesProgramFormatErrorCode ErrorCode { get; }
    /// <summary>
    /// Gets the zero-based byte offset in the <c>.gesb</c> image, or <see langword="null"/> when the failure has no
    /// position, for example during validation of an in-memory program.
    /// </summary>
    public long? ByteOffset { get; }
    /// <summary>
    /// Gets the raw type of the affected section, or <see langword="null"/> when unknown.
    /// </summary>
    public ushort? SectionType { get; }
    /// <summary>
    /// Gets the zero-based index of the affected entry within its section, or <see langword="null"/> when unknown.
    /// </summary>
    public int? EntryIndex { get; }
    /// <summary>
    /// Gets the equivalent decode-phase diagnostic, whose code is derived from <see cref="ErrorCode"/> and whose technical
    /// details carry the byte offset, section type and entry index.
    /// </summary>
    public GesDiagnostic Diagnostic { get; }

    private static string? BuildTechnicalDetails(long? byteOffset, ushort? sectionType, int? entryIndex)
    {
        if (byteOffset is null && sectionType is null && entryIndex is null) return null;
        return $"byteOffset={byteOffset?.ToString() ?? "-"};sectionType={sectionType?.ToString() ?? "-"};entryIndex={entryIndex?.ToString() ?? "-"}";
    }
}
