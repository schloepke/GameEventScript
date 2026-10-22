// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using GameEventScript.Runtime;
using GameEventScript.Runtime.Vm;
using static GameEventScript.Api.GesBinaryBindKind;

namespace GameEventScript.Api;

/// <summary>
/// Validates optional debug, source-map, source-archive, build-metadata and opaque sections.
/// </summary>
internal static class GesProgramDebugValidator
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    internal static void Validate(GameEventScriptProgram program)
    {
        if (program.DebugSymbols is not null)
        {
            for (var index = 0; index < program.DebugSymbols.Symbols.Count; index++)
            {
                var symbol = program.DebugSymbols.Symbols[index];
                if (!GesProgramEnumValidation.IsDefined(symbol.Kind) ||
                    symbol.Name is null || (symbol.Name != GesMessageSignature.UnlabeledParameterName && !GesText.IsIdentifier(symbol.Name)) || symbol.CodeLength == 0 ||
                    (ulong)symbol.CodeStart + symbol.CodeLength > (ulong)program.Code.Count || symbol.RegisterId == ushort.MaxValue)
                    GesProgramValidator.Throw(GesProgramFormatErrorCode.InvalidDebugSymbol, "Debug symbol has an invalid kind, name, register, or code range.", (ushort)GesSectionType.DebugSymbols, index);
            }
        }

        if (program.SourceMap is not null) ValidateSourceMap(program);
        if (program.SourceArchive is not null) ValidateSourceArchive(program.SourceArchive);
        if (program.SourceMap is not null && program.SourceArchive is not null) ValidateSourceAgreement(program.SourceMap, program.SourceArchive);
        if (program.BuildMetadata is not null)
        {
            ValidateMetadataText(program.BuildMetadata.CompilerId, "CompilerId", (ushort)GesSectionType.BuildMetadata);
            ValidateMetadataText(program.BuildMetadata.CompilerVersion, "CompilerVersion", (ushort)GesSectionType.BuildMetadata);
        }
    }

    private static void ValidateSourceMap(GameEventScriptProgram program)
    {
        var map = program.SourceMap!;
        var ids = new HashSet<uint>();
        for (var index = 0; index < map.Sources.Count; index++)
        {
            var source = map.Sources[index];
            if (source.SourceId != (uint)index || !ids.Add(source.SourceId) || source.SourceName is null || source.SourceName.Length == 0 || GesText.GetInvalidUtf16Offset(source.SourceName) >= 0 ||
                source.Sha256.Count != 32 || source.LineStartByteOffsets.Count == 0 || source.LineStartByteOffsets[0] != 0)
                GesProgramValidator.Throw(GesProgramFormatErrorCode.InvalidSourceMap, "SourceMap source metadata is invalid.", (ushort)GesSectionType.SourceMap, index);
            uint previous = 0;
            for (var line = 0; line < source.LineStartByteOffsets.Count; line++)
            {
                var offset = source.LineStartByteOffsets[line];
                if (offset > source.SourceByteLength || line > 0 && offset <= previous)
                    GesProgramValidator.Throw(GesProgramFormatErrorCode.InvalidSourceMap, "SourceMap line offsets must be strictly increasing and inside the source.", (ushort)GesSectionType.SourceMap, index);
                previous = offset;
            }
        }
        ulong previousCodeEnd = 0;
        for (var index = 0; index < map.Entries.Count; index++)
        {
            var entry = map.Entries[index];
            GesSourceMapSource? source = null;
            for (var sourceIndex = 0; sourceIndex < map.Sources.Count; sourceIndex++) if (map.Sources[sourceIndex].SourceId == entry.SourceId) { source = map.Sources[sourceIndex]; break; }
            if (source is null || entry.CodeLength == 0 || entry.SourceByteLength == 0 || entry.CodeStart < previousCodeEnd ||
                (ulong)entry.CodeStart + entry.CodeLength > (ulong)program.Code.Count ||
                (ulong)entry.SourceStartByteOffset + entry.SourceByteLength > source.SourceByteLength)
                GesProgramValidator.Throw(GesProgramFormatErrorCode.InvalidSourceMap, "SourceMap mapping is outside code or source bounds.", (ushort)GesSectionType.SourceMap, index);
            previousCodeEnd = (ulong)entry.CodeStart + entry.CodeLength;
        }
    }

    private static void ValidateSourceArchive(GesSourceArchiveSegment archive)
    {
        var ids = new HashSet<uint>();
        for (var index = 0; index < archive.Sources.Count; index++)
        {
            var source = archive.Sources[index];
            if (!ids.Add(source.SourceId) || source.SourceName is null || source.SourceName.Length == 0 || GesText.GetInvalidUtf16Offset(source.SourceName) >= 0)
                GesProgramValidator.Throw(
                    GesProgramFormatErrorCode.InvalidSourceArchive, "SourceArchive SourceIds must be unique and names valid non-empty Unicode.", (ushort)GesSectionType.SourceArchive, index);
            try
            {
                var text = source.DecodeText(StrictUtf8);
                if (text.Length > 0 && text[0] == '\uFEFF')
                    GesProgramValidator.Throw(GesProgramFormatErrorCode.InvalidSourceArchive, "SourceArchive stores logical source text and must not retain an initial UTF-8 BOM.", (ushort)GesSectionType.SourceArchive, index);
            }
            catch (DecoderFallbackException exception)
            {
                throw new GesProgramFormatException(
                    GesProgramFormatErrorCode.InvalidUtf8, "SourceArchive contains invalid UTF-8.", sectionType: (ushort)GesSectionType.SourceArchive, entryIndex: index, innerException: exception);
            }
        }
    }

    private static void ValidateSourceAgreement(GesSourceMapSegment map, GesSourceArchiveSegment archive)
    {
        if (map.Sources.Count != archive.Sources.Count)
            GesProgramValidator.Throw(GesProgramFormatErrorCode.SourceMetadataMismatch, "SourceMap and SourceArchive must describe the same source set.", (ushort)GesSectionType.SourceMap);
        using var sha256 = SHA256.Create();
        Span<byte> hash = stackalloc byte[32];
        for (var index = 0; index < map.Sources.Count; index++)
        {
            var metadata = map.Sources[index];
            GesSourceArchiveEntry? content = null;
            for (var archiveIndex = 0; archiveIndex < archive.Sources.Count; archiveIndex++) if (archive.Sources[archiveIndex].SourceId == metadata.SourceId) { content = archive.Sources[archiveIndex]; break; }
            if (content is null)
            {
                GesProgramValidator.Throw(GesProgramFormatErrorCode.SourceMetadataMismatch, "SourceMap has no matching SourceArchive entry.", (ushort)GesSectionType.SourceMap, index);
                continue;
            }
            if (!string.Equals(content.SourceName, metadata.SourceName, StringComparison.Ordinal) || content.Utf8Content.Count != metadata.SourceByteLength)
                GesProgramValidator.Throw(GesProgramFormatErrorCode.SourceMetadataMismatch, "SourceMap and SourceArchive metadata do not agree.", (ushort)GesSectionType.SourceMap, index);
            if (!sha256.TryComputeHash(content.Utf8Content.AsSpan(), hash, out var hashLength) || hashLength != hash.Length)
                GesProgramValidator.Throw(GesProgramFormatErrorCode.InvalidProgram, "SHA-256 could not hash SourceArchive content.", (ushort)GesSectionType.SourceMap, index);
            for (var byteIndex = 0; byteIndex < hash.Length; byteIndex++) if (hash[byteIndex] != metadata.Sha256[byteIndex])
                GesProgramValidator.Throw(GesProgramFormatErrorCode.SourceMetadataMismatch, "SourceMap SHA-256 does not match SourceArchive.", (ushort)GesSectionType.SourceMap, index);
            ValidateUtf8Offsets(content.Utf8Content.AsSpan(), metadata, index);
            ValidateLogicalLineOffsets(content.DecodeText(StrictUtf8), metadata, index);
            ValidateMappingUtf8Offsets(content.Utf8Content.AsSpan(), map, metadata.SourceId, index);
        }
    }

    private static void ValidateMappingUtf8Offsets(ReadOnlySpan<byte> bytes, GesSourceMapSegment map, uint sourceId, int sourceIndex)
    {
        for (var index = 0; index < map.Entries.Count; index++)
        {
            var entry = map.Entries[index];
            if (entry.SourceId != sourceId) continue;
            var start = checked((int)entry.SourceStartByteOffset);
            var end = checked((int)(entry.SourceStartByteOffset + entry.SourceByteLength));
            if (!IsUtf8Boundary(bytes, start) || !IsUtf8Boundary(bytes, end))
                GesProgramValidator.Throw(GesProgramFormatErrorCode.InvalidSourceMap, $"A source mapping for source {sourceIndex} is not on UTF-8 scalar boundaries.", (ushort)GesSectionType.SourceMap, index);
        }
    }

    private static bool IsUtf8Boundary(ReadOnlySpan<byte> bytes, int offset)
        => offset == 0 || offset == bytes.Length || offset > 0 && offset < bytes.Length && (bytes[offset] & 0xC0) != 0x80;

    private static void ValidateLogicalLineOffsets(string text, GesSourceMapSource metadata, int entryIndex)
    {
        var expected = new List<uint> { 0 };
        for (var utf16Offset = 0; utf16Offset < text.Length; utf16Offset++)
        {
            if (text[utf16Offset] == '\r')
            {
                if (utf16Offset + 1 < text.Length && text[utf16Offset + 1] == '\n') utf16Offset++;
                expected.Add(checked((uint)StrictUtf8.GetByteCount(text, 0, utf16Offset + 1)));
            }
            else if (text[utf16Offset] == '\n')
            {
                expected.Add(checked((uint)StrictUtf8.GetByteCount(text, 0, utf16Offset + 1)));
            }
        }

        if (expected.Count != metadata.LineStartByteOffsets.Count)
            GesProgramValidator.Throw(GesProgramFormatErrorCode.SourceMetadataMismatch, "SourceMap line offsets do not match the logical source newlines.", (ushort)GesSectionType.SourceMap, entryIndex);
        for (var index = 0; index < expected.Count; index++)
            if (expected[index] != metadata.LineStartByteOffsets[index])
                GesProgramValidator.Throw(GesProgramFormatErrorCode.SourceMetadataMismatch, "SourceMap line offsets do not match the logical source newlines.", (ushort)GesSectionType.SourceMap, entryIndex);
    }

    private static void ValidateMetadataText(string value, string role, ushort sectionType)
    {
        if (value is null || value.Length == 0 || GesText.GetInvalidUtf16Offset(value) >= 0)
            GesProgramValidator.Throw(GesProgramFormatErrorCode.InvalidProgram, $"{role} must be valid non-empty Unicode.", sectionType);
    }

    private static void ValidateUtf8Offsets(ReadOnlySpan<byte> bytes, GesSourceMapSource metadata, int entryIndex)
    {
        for (var index = 0; index < metadata.LineStartByteOffsets.Count; index++)
        {
            var offset = checked((int)metadata.LineStartByteOffsets[index]);
            if (offset > 0 && offset < bytes.Length && (bytes[offset] & 0xC0) == 0x80)
                GesProgramValidator.Throw(GesProgramFormatErrorCode.InvalidSourceMap, "A line offset is not on a UTF-8 code-point boundary.", (ushort)GesSectionType.SourceMap, entryIndex);
        }
    }

    internal static void ValidateOpaque(GameEventScriptProgram program)
    {
        var ordinals = new HashSet<int>();
        var knownTypes = new HashSet<ushort>();
        for (var index = 0; index < program.OpaqueSections.Count; index++)
        {
            var section = program.OpaqueSections[index];
            if (section.SectionType is 0 or ushort.MaxValue || section.OriginalOrdinal < 0 || (section.Flags & GesSectionFlags.Required) != 0)
                GesProgramValidator.Throw(GesProgramFormatErrorCode.UnknownRequiredSection, "Opaque sections must be valid optional sections.", section.SectionType, index);
            if (((ushort)section.Flags & ~(ushort)(GesSectionFlags.Required | GesSectionFlags.CompressionMask)) != 0)
                GesProgramValidator.Throw(GesProgramFormatErrorCode.InvalidSectionFlags, "Opaque section flags contain unassigned V1 bits.", section.SectionType, index);
            if (section.SectionType is (ushort)GesSectionType.ProgramMetadata or (ushort)GesSectionType.StringConstants or
                (ushort)GesSectionType.UInt16IndexLists or (ushort)GesSectionType.Bindings or (ushort)GesSectionType.Code)
                GesProgramValidator.Throw(GesProgramFormatErrorCode.DuplicateSection, "Required known sections cannot be stored as opaque data.", section.SectionType, index);
            if (section.SectionType == (ushort)GesSectionType.DebugSymbols && program.DebugSymbols is not null ||
                section.SectionType == (ushort)GesSectionType.SourceMap && program.SourceMap is not null ||
                section.SectionType == (ushort)GesSectionType.SourceArchive && program.SourceArchive is not null ||
                section.SectionType == (ushort)GesSectionType.BuildMetadata && program.BuildMetadata is not null)
                GesProgramValidator.Throw(GesProgramFormatErrorCode.DuplicateSection, "A known optional section cannot be both parsed and opaque.", section.SectionType, index);
            var knownOptional = section.SectionType is (ushort)GesSectionType.DebugSymbols or (ushort)GesSectionType.SourceMap or
                (ushort)GesSectionType.SourceArchive or (ushort)GesSectionType.BuildMetadata;
            var codec = (ushort)section.Flags & (ushort)GesSectionFlags.CompressionMask;
            if (knownOptional && section.SectionVersion == 1 && codec == 0)
                GesProgramValidator.Throw(GesProgramFormatErrorCode.InvalidProgram, "A supported known section must be represented by its parsed segment model.", section.SectionType, index);
            if (knownOptional && !knownTypes.Add(section.SectionType))
                GesProgramValidator.Throw(GesProgramFormatErrorCode.DuplicateSection, "A singleton known section occurs more than once.", section.SectionType, index);
            if (!ordinals.Add(section.OriginalOrdinal)) GesProgramValidator.Throw(GesProgramFormatErrorCode.InvalidProgram, "Opaque section ordinals must be unique.", section.SectionType, index);
        }
    }
}
