// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Text;
using static StepH.GameEventScript.Api.GameEventScriptBindingSegment;

namespace StepH.GameEventScript.Api;

/// <summary>
/// Decodes bounded, portable <c>.gesb</c> V1 bytes into an immutable program.
/// </summary>
public static class GameEventScriptProgramReader
{
    private const ushort KnownFlagMask = (ushort)(GameEventScriptSectionFlags.Required | GameEventScriptSectionFlags.CompressionMask);
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    /// <summary>
    /// Reads and fully validates a program from one complete <c>.gesb</c> image.
    /// </summary>
    /// <param name="bytes">The complete byte image. The returned program owns all retained data.</param>
    /// <param name="options">Optional retention and resource limits; defaults preserve all supported and opaque optional sections.</param>
    /// <returns>A structurally and semantically validated immutable program.</returns>
    /// <exception cref="GameEventScriptProgramFormatException">Thrown with a stable error code when the image is malformed, unsupported, exceeds limits, or describes an invalid program.</exception>
    public static GameEventScriptProgram Read(ReadOnlySpan<byte> bytes, GameEventScriptProgramReadOptions? options = null)
    {
        var readOptions = options ?? new GameEventScriptProgramReadOptions();
        var limits = readOptions.Limits ?? throw new ArgumentException("Read limits must not be null.", nameof(options));
        ValidateLimits(limits);
        if (bytes.Length > limits.MaxFileBytes) Throw(GameEventScriptProgramFormatErrorCode.FileTooLarge, "The .gesb file exceeds MaxFileBytes.");
        if (bytes.Length < GameEventScriptBinaryFormat.HeaderSize) Throw(GameEventScriptProgramFormatErrorCode.InvalidHeaderSize, "The .gesb file is shorter than its fixed header.");

        var header = new PayloadReader(bytes, 0, null);
        if (header.ReadByte() != GameEventScriptBinaryFormat.MagicG || header.ReadByte() != GameEventScriptBinaryFormat.MagicE ||
            header.ReadByte() != GameEventScriptBinaryFormat.MagicS || header.ReadByte() != GameEventScriptBinaryFormat.MagicB)
            Throw(GameEventScriptProgramFormatErrorCode.InvalidMagic, "The file does not start with ASCII 'GESB'.");
        var formatVersion = header.ReadUInt16();
        if (formatVersion != GameEventScriptBinaryFormat.Version) Throw(GameEventScriptProgramFormatErrorCode.UnsupportedFormatVersion, $"Unsupported .gesb version '{formatVersion}'.", 4);
        if (header.ReadUInt16() != 0) Throw(GameEventScriptProgramFormatErrorCode.InvalidHeaderFlags, "V1 file-header flags must be zero.", 6);
        if (header.ReadUInt32() != GameEventScriptBinaryFormat.HeaderSize) Throw(GameEventScriptProgramFormatErrorCode.InvalidHeaderSize, "V1 HeaderSize must be 16.", 8);
        if (header.ReadUInt32() != bytes.Length) Throw(GameEventScriptProgramFormatErrorCode.FileSizeMismatch, "Header FileSize does not equal the supplied byte count.", 12);

        var sections = ScanSections(bytes, limits, readOptions.Retention, out var opaqueSections);
        var metadataSection = Require(sections, GameEventScriptSectionType.ProgramMetadata);
        var strings = ReadStringConstants(bytes, Require(sections, GameEventScriptSectionType.StringConstants), limits);
        var lists = ReadIndexLists(bytes, Require(sections, GameEventScriptSectionType.UInt16IndexLists), limits);
        var metadata = ReadProgramMetadata(bytes, metadataSection, strings);
        var bindings = ReadBindings(bytes, Require(sections, GameEventScriptSectionType.Bindings), strings, lists, limits);
        var code = ReadCode(bytes, Require(sections, GameEventScriptSectionType.Code), limits);

        GameEventScriptDebugSymbolsSegment? debug = null;
        GameEventScriptSourceMapSegment? sourceMap = null;
        GameEventScriptSourceArchiveSegment? sourceArchive = null;
        GameEventScriptBuildMetadataSegment? buildMetadata = null;
        if (readOptions.Retention != GameEventScriptProgramRetention.RuntimeOnly)
        {
            if (Find(sections, GameEventScriptSectionType.DebugSymbols) is { ParseKnown: true } debugSection) debug = ReadDebugSymbols(bytes, debugSection);
            if (Find(sections, GameEventScriptSectionType.SourceMap) is { ParseKnown: true } sourceMapSection) sourceMap = ReadSourceMap(bytes, sourceMapSection);
            if (Find(sections, GameEventScriptSectionType.SourceArchive) is { ParseKnown: true } archiveSection) sourceArchive = ReadSourceArchive(bytes, archiveSection, limits);
            if (Find(sections, GameEventScriptSectionType.BuildMetadata) is { ParseKnown: true } buildSection) buildMetadata = ReadBuildMetadata(bytes, buildSection);
        }

        var program = new GameEventScriptProgram(
            formatVersion,
            metadata.ModuleName,
            metadata.ProgramVersion,
            metadata.RequiredRegisters,
            metadata.RequiredCallDepth,
            strings,
            lists,
            bindings,
            code,
            debug,
            sourceMap,
            sourceArchive,
            buildMetadata,
            opaqueSections);
        GameEventScriptProgramValidator.Validate(program);
        return program;
    }

    private static List<SectionDescriptor> ScanSections(ReadOnlySpan<byte> bytes, GameEventScriptProgramReadLimits limits, GameEventScriptProgramRetention retention, out GameEventScriptOpaqueSection[] opaque)
    {
        var sections = new List<SectionDescriptor>();
        var opaqueList = new List<GameEventScriptOpaqueSection>();
        var seenKnown = new HashSet<ushort>();
        long retainedOpaqueBytes = 0;
        var offset = checked((int)GameEventScriptBinaryFormat.HeaderSize);
        var ordinal = 0;
        while (offset < bytes.Length)
        {
            if (sections.Count >= limits.MaxSectionCount) Throw(GameEventScriptProgramFormatErrorCode.TooManySections, "The file exceeds MaxSectionCount.", offset);
            if (bytes.Length - offset < GameEventScriptBinaryFormat.SectionHeaderSize)
                Throw(GameEventScriptProgramFormatErrorCode.TruncatedSectionHeader, "A section header is truncated.", offset);
            var sectionHeader = new PayloadReader(bytes.Slice(offset, checked((int)GameEventScriptBinaryFormat.SectionHeaderSize)), offset, null);
            var type = sectionHeader.ReadUInt16();
            var flags = sectionHeader.ReadUInt16();
            var version = sectionHeader.ReadUInt16();
            var reserved = sectionHeader.ReadUInt16();
            var payloadLength = sectionHeader.ReadUInt32();
            if (type is 0 or ushort.MaxValue) Throw(GameEventScriptProgramFormatErrorCode.InvalidSectionType, "Section type 0x0000 and 0xFFFF are invalid.", offset, type);
            if ((flags & ~KnownFlagMask) != 0) Throw(GameEventScriptProgramFormatErrorCode.InvalidSectionFlags, "Section flags contain unassigned V1 bits.", offset + 2, type);
            if (reserved != 0) Throw(GameEventScriptProgramFormatErrorCode.InvalidSectionReserved, "Section Reserved must be zero.", offset + 6, type);
            if (payloadLength > int.MaxValue || payloadLength > bytes.Length - offset - GameEventScriptBinaryFormat.SectionHeaderSize)
                Throw(GameEventScriptProgramFormatErrorCode.TruncatedSectionPayload, "A section payload extends beyond FileSize.", offset + 8, type);

            var required = (flags & (ushort)GameEventScriptSectionFlags.Required) != 0;
            var codec = flags & (ushort)GameEventScriptSectionFlags.CompressionMask;
            var known = IsKnown(type);
            var requiredRuntime = IsRequiredRuntime(type);
            if (known && !seenKnown.Add(type)) Throw(GameEventScriptProgramFormatErrorCode.DuplicateSection, "A singleton known section occurs more than once.", offset, type);
            if (requiredRuntime && !required) Throw(GameEventScriptProgramFormatErrorCode.InvalidSectionFlags, "A runtime section must carry the Required flag.", offset + 2, type);
            if (known && !requiredRuntime && required) Throw(GameEventScriptProgramFormatErrorCode.UnknownRequiredSection, "Optional V1 sections cannot be required.", offset + 2, type);
            if (!known && required) Throw(GameEventScriptProgramFormatErrorCode.UnknownRequiredSection, "An unknown Required section cannot be loaded.", offset, type);

            var supportedKnown = known && version == 1 && codec == 0;
            if (requiredRuntime && version != 1) Throw(GameEventScriptProgramFormatErrorCode.UnsupportedSectionVersion, "Required V1 section has an unsupported version.", offset + 4, type);
            if (requiredRuntime && codec != 0) Throw(GameEventScriptProgramFormatErrorCode.UnsupportedCompression, "Required V1 section uses an unsupported compression codec.", offset + 2, type);

            var payloadOffset = checked(offset + (int)GameEventScriptBinaryFormat.SectionHeaderSize);
            var descriptor = new SectionDescriptor(type, (GameEventScriptSectionFlags)flags, version, payloadOffset, checked((int)payloadLength), ordinal, supportedKnown);
            sections.Add(descriptor);
            var retainOpaque = retention == GameEventScriptProgramRetention.PreserveAll && (!known || !supportedKnown);
            if (retainOpaque)
            {
                retainedOpaqueBytes = checked(retainedOpaqueBytes + payloadLength);
                if (retainedOpaqueBytes > limits.MaxRetainedOpaqueBytes) Throw(GameEventScriptProgramFormatErrorCode.ReaderLimitExceeded, "Opaque payloads exceed MaxRetainedOpaqueBytes.", offset, type);
                opaqueList.Add(new GameEventScriptOpaqueSection(type, (GameEventScriptSectionFlags)flags, version, bytes.Slice(payloadOffset, checked((int)payloadLength)).ToArray(), ordinal));
            }
            offset = checked(payloadOffset + (int)payloadLength);
            ordinal++;
        }
        opaque = opaqueList.ToArray();
        return sections;
    }

    private static ProgramMetadata ReadProgramMetadata(ReadOnlySpan<byte> bytes, SectionDescriptor section, GameEventScriptStringConstantSegment strings)
    {
        if (section.PayloadLength != 16) Throw(GameEventScriptProgramFormatErrorCode.InvalidPayloadLength, "ProgramMetadataSegment payload must be 16 bytes.", section.PayloadOffset, section.Type);
        var reader = Reader(bytes, section);
        var moduleIndex = reader.ReadUInt16();
        var registers = reader.ReadUInt16();
        var callDepth = reader.ReadUInt16();
        if (reader.ReadUInt16() != 0) Throw(GameEventScriptProgramFormatErrorCode.InvalidSectionReserved, "ProgramMetadata Reserved must be zero.", section.PayloadOffset + 6, section.Type);
        var programVersion = reader.ReadUInt64();
        if (moduleIndex >= strings.Slices.Count) Throw(GameEventScriptProgramFormatErrorCode.InvalidStringIndex, "ModuleNameStringIndex is invalid.", section.PayloadOffset, section.Type);
        return new ProgramMetadata(strings.Resolve(moduleIndex), programVersion, registers, callDepth);
    }

    private static GameEventScriptStringConstantSegment ReadStringConstants(ReadOnlySpan<byte> bytes, SectionDescriptor section, GameEventScriptProgramReadLimits limits)
    {
        var reader = Reader(bytes, section);
        var count = reader.ReadCount(limits.MaxStringEntries);
        var slices = new GameEventScriptStringConstantSegment.SliceEntry[count];
        var data = new List<byte>();
        for (var index = 0; index < count; index++)
        {
            var length = reader.ReadLength();
            var value = reader.ReadBytes(length, index);
            try { _ = StrictUtf8.GetString(value); }
            catch (DecoderFallbackException exception) { throw Format(GameEventScriptProgramFormatErrorCode.InvalidUtf8, "StringConstantSegment contains invalid UTF-8.", reader.AbsoluteOffset - length, section.Type, index, exception); }
            slices[index] = new GameEventScriptStringConstantSegment.SliceEntry { Start = data.Count, Length = length };
            for (var byteIndex = 0; byteIndex < value.Length; byteIndex++) data.Add(value[byteIndex]);
        }
        reader.RequireEnd();
        return new GameEventScriptStringConstantSegment(slices, data.ToArray());
    }

    private static GameEventScriptUInt16IndexListSegment ReadIndexLists(ReadOnlySpan<byte> bytes, SectionDescriptor section, GameEventScriptProgramReadLimits limits)
    {
        var reader = Reader(bytes, section);
        var count = reader.ReadCount(limits.MaxIndexLists);
        var slices = new GameEventScriptUInt16IndexListSegment.SliceEntry[count];
        var data = new List<ushort>();
        for (var index = 0; index < count; index++)
        {
            var elementCount = reader.ReadLength();
            if (elementCount > reader.Remaining / 2) Throw(GameEventScriptProgramFormatErrorCode.TruncatedSectionPayload, "Index list exceeds its section payload.", reader.AbsoluteOffset, section.Type, index);
            slices[index] = new GameEventScriptUInt16IndexListSegment.SliceEntry { Start = data.Count, Length = elementCount };
            for (var element = 0; element < elementCount; element++) data.Add(reader.ReadUInt16());
        }
        reader.RequireEnd();
        return new GameEventScriptUInt16IndexListSegment(slices, data.ToArray());
    }

    private static GameEventScriptBindingSegment ReadBindings(ReadOnlySpan<byte> bytes, SectionDescriptor section, GameEventScriptStringConstantSegment strings, GameEventScriptUInt16IndexListSegment lists, GameEventScriptProgramReadLimits limits)
    {
        var reader = Reader(bytes, section);
        var count = reader.ReadCount(limits.MaxBindings);
        if (count > reader.Remaining / 20 || reader.Remaining != count * 20) Throw(GameEventScriptProgramFormatErrorCode.InvalidPayloadLength, "BindingSegment length does not match EntryCount.", reader.AbsoluteOffset, section.Type);
        var entries = new GameEventScriptBinaryBindEntry[count];
        for (var index = 0; index < count; index++)
        {
            var kind = (GameEventScriptBinaryBindKind)reader.ReadByte();
            if (reader.ReadByte() != 0) Throw(GameEventScriptProgramFormatErrorCode.InvalidSectionFlags, "Binding Flags must be zero.", reader.AbsoluteOffset - 1, section.Type, index);
            var id = reader.ReadUInt16();
            var name = reader.ReadUInt16();
            var arguments = ResolveList(lists, reader.ReadUInt16(), section, index);
            var requiredTags = ResolveList(lists, reader.ReadUInt16(), section, index);
            var excludedTags = ResolveList(lists, reader.ReadUInt16(), section, index);
            var entryAddress = reader.ReadUInt16();
            var requiredRegisters = reader.ReadUInt16();
            var requiredDepth = reader.ReadUInt16();
            if (reader.ReadUInt16() != 0) Throw(GameEventScriptProgramFormatErrorCode.InvalidSectionReserved, "Binding Reserved must be zero.", reader.AbsoluteOffset - 2, section.Type, index);
            entries[index] = new GameEventScriptBinaryBindEntry(kind, name, arguments, entryAddress, id, requiredTags, excludedTags, requiredRegisters, requiredDepth);
        }
        reader.RequireEnd();
        return new GameEventScriptBindingSegment(entries);
    }

    private static GameEventScriptCodeSegment ReadCode(ReadOnlySpan<byte> bytes, SectionDescriptor section, GameEventScriptProgramReadLimits limits)
    {
        var reader = Reader(bytes, section);
        var count = reader.ReadCount(limits.MaxInstructions);
        if (count > reader.Remaining / 16 || reader.Remaining != count * 16) Throw(GameEventScriptProgramFormatErrorCode.InvalidPayloadLength, "CodeSegment length does not match InstructionCount.", reader.AbsoluteOffset, section.Type);
        var instructions = new GameEventScriptBytecodeInstruction[count];
        for (var index = 0; index < count; index++)
        {
            instructions[index] = new GameEventScriptBytecodeInstruction
            {
                OpCode = (GameEventScriptBytecodeOpCode)reader.ReadByte(),
                UnitAndFlags = reader.ReadByte(),
                DestinationRegister = reader.ReadUInt16(),
                XRegister = reader.ReadUInt16(),
                YRegister = reader.ReadUInt16(),
                Payload = reader.ReadUInt64()
            };
        }
        reader.RequireEnd();
        return new GameEventScriptCodeSegment(instructions);
    }

    private static GameEventScriptDebugSymbolsSegment ReadDebugSymbols(ReadOnlySpan<byte> bytes, SectionDescriptor section)
    {
        var reader = Reader(bytes, section);
        var nameCount = reader.ReadCount(ushort.MaxValue);
        var names = new string[nameCount];
        for (var index = 0; index < nameCount; index++) names[index] = reader.ReadString(index);
        var symbolCount = reader.ReadCount(ushort.MaxValue);
        if (symbolCount > reader.Remaining / 16 || reader.Remaining != symbolCount * 16) Throw(GameEventScriptProgramFormatErrorCode.InvalidPayloadLength, "DebugSymbols length does not match SymbolCount.", reader.AbsoluteOffset, section.Type);
        var symbols = new GameEventScriptDebugSymbol[symbolCount];
        for (var index = 0; index < symbolCount; index++)
        {
            var kind = (GameEventScriptDebugSymbolKind)reader.ReadByte();
            if (reader.ReadByte() != 0) Throw(GameEventScriptProgramFormatErrorCode.InvalidSectionReserved, "Debug symbol Reserved must be zero.", reader.AbsoluteOffset - 1, section.Type, index);
            var register = reader.ReadUInt16();
            var nameIndex = reader.ReadUInt32();
            var codeStart = reader.ReadUInt32();
            var codeLength = reader.ReadUInt32();
            if (nameIndex >= names.Length) Throw(GameEventScriptProgramFormatErrorCode.InvalidStringIndex, "Debug symbol NameIndex is invalid.", reader.AbsoluteOffset - 12, section.Type, index);
            symbols[index] = new GameEventScriptDebugSymbol(kind, register, names[nameIndex], codeStart, codeLength);
        }
        reader.RequireEnd();
        return new GameEventScriptDebugSymbolsSegment(symbols);
    }

    private static GameEventScriptSourceMapSegment ReadSourceMap(ReadOnlySpan<byte> bytes, SectionDescriptor section)
    {
        var reader = Reader(bytes, section);
        var sourceCount = reader.ReadCount(ushort.MaxValue);
        var sources = new GameEventScriptSourceMapSource[sourceCount];
        for (var index = 0; index < sourceCount; index++)
        {
            var name = reader.ReadString(index);
            var byteLength = reader.ReadUInt32();
            var hash = reader.ReadBytes(32, index).ToArray();
            var lineCount = reader.ReadCount(int.MaxValue);
            if (lineCount > reader.Remaining / 4) Throw(GameEventScriptProgramFormatErrorCode.TruncatedSectionPayload, "SourceMap line offsets exceed the payload.", reader.AbsoluteOffset, section.Type, index);
            var lines = new uint[lineCount];
            for (var line = 0; line < lineCount; line++) lines[line] = reader.ReadUInt32();
            sources[index] = new GameEventScriptSourceMapSource(checked((uint)index), name, byteLength, hash, lines);
        }
        var mappingCount = reader.ReadCount(int.MaxValue);
        if (mappingCount > reader.Remaining / 20 || reader.Remaining != mappingCount * 20) Throw(GameEventScriptProgramFormatErrorCode.InvalidPayloadLength, "SourceMap mapping length is invalid.", reader.AbsoluteOffset, section.Type);
        var mappings = new GameEventScriptSourceMapEntry[mappingCount];
        for (var index = 0; index < mappingCount; index++) mappings[index] = new GameEventScriptSourceMapEntry(reader.ReadUInt32(), reader.ReadUInt32(), reader.ReadUInt32(), reader.ReadUInt32(), reader.ReadUInt32());
        reader.RequireEnd();
        return new GameEventScriptSourceMapSegment(sources, mappings);
    }

    private static GameEventScriptSourceArchiveSegment ReadSourceArchive(ReadOnlySpan<byte> bytes, SectionDescriptor section, GameEventScriptProgramReadLimits limits)
    {
        var reader = Reader(bytes, section);
        var count = reader.ReadCount(ushort.MaxValue);
        var sources = new GameEventScriptSourceArchiveEntry[count];
        long contentBytes = 0;
        for (var index = 0; index < count; index++)
        {
            var id = reader.ReadUInt32();
            var name = reader.ReadString(index);
            var length = reader.ReadLength();
            contentBytes = checked(contentBytes + length);
            if (contentBytes > limits.MaxSourceArchiveBytes) Throw(GameEventScriptProgramFormatErrorCode.ReaderLimitExceeded, "SourceArchive exceeds MaxSourceArchiveBytes.", reader.AbsoluteOffset, section.Type, index);
            var content = reader.ReadBytes(length, index).ToArray();
            try { _ = StrictUtf8.GetString(content); }
            catch (DecoderFallbackException exception) { throw Format(GameEventScriptProgramFormatErrorCode.InvalidUtf8, "SourceArchive contains invalid UTF-8.", reader.AbsoluteOffset - length, section.Type, index, exception); }
            sources[index] = new GameEventScriptSourceArchiveEntry(id, name, content);
        }
        reader.RequireEnd();
        return new GameEventScriptSourceArchiveSegment(sources);
    }

    private static GameEventScriptBuildMetadataSegment ReadBuildMetadata(ReadOnlySpan<byte> bytes, SectionDescriptor section)
    {
        var reader = Reader(bytes, section);
        var compilerId = reader.ReadString(0);
        var compilerVersion = reader.ReadString(1);
        reader.RequireEnd();
        return new GameEventScriptBuildMetadataSegment(compilerId, compilerVersion);
    }

    private static ushort[] ResolveList(GameEventScriptUInt16IndexListSegment lists, ushort index, SectionDescriptor section, int entryIndex)
    {
        if (index == ushort.MaxValue) return [];
        if (index >= lists.Slices.Count) Throw(GameEventScriptProgramFormatErrorCode.InvalidListIndex, "Binding references a missing UInt16 index list.", section.PayloadOffset, section.Type, entryIndex);
        var list = lists.Resolve(index);
        var result = new ushort[list.Length];
        for (var element = 0; element < list.Length; element++) result[element] = list[element];
        return result;
    }

    private static SectionDescriptor Require(List<SectionDescriptor> sections, GameEventScriptSectionType type)
        => Find(sections, type) ?? throw Format(GameEventScriptProgramFormatErrorCode.MissingRequiredSection, $"Required section {type} is missing.", sectionType: (ushort)type);

    private static SectionDescriptor? Find(List<SectionDescriptor> sections, GameEventScriptSectionType type)
    {
        for (var index = 0; index < sections.Count; index++) if (sections[index].Type == (ushort)type) return sections[index];
        return null;
    }

    private static bool IsRequiredRuntime(ushort type)
        => type is (ushort)GameEventScriptSectionType.ProgramMetadata or (ushort)GameEventScriptSectionType.StringConstants or (ushort)GameEventScriptSectionType.UInt16IndexLists or
            (ushort)GameEventScriptSectionType.Bindings or (ushort)GameEventScriptSectionType.Code;

    private static bool IsKnown(ushort type)
        => IsRequiredRuntime(type) || type is (ushort)GameEventScriptSectionType.DebugSymbols or (ushort)GameEventScriptSectionType.SourceMap or (ushort)GameEventScriptSectionType.SourceArchive or (ushort)GameEventScriptSectionType.BuildMetadata;

    private static PayloadReader Reader(ReadOnlySpan<byte> bytes, SectionDescriptor section)
        => new(bytes.Slice(section.PayloadOffset, section.PayloadLength), section.PayloadOffset, section.Type);

    private static void ValidateLimits(GameEventScriptProgramReadLimits limits)
    {
        if (limits.MaxFileBytes < GameEventScriptBinaryFormat.HeaderSize || limits.MaxSectionCount < 1 || limits.MaxSourceArchiveBytes < 0 || limits.MaxRetainedOpaqueBytes < 0 ||
            limits.MaxInstructions is < 0 or > ushort.MaxValue || limits.MaxBindings is < 0 or > ushort.MaxValue || limits.MaxStringEntries is < 0 or > ushort.MaxValue || limits.MaxIndexLists is < 0 or > ushort.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(limits), "Reader limits are outside the V1 supported range.");
    }

    private static GameEventScriptProgramFormatException Format(GameEventScriptProgramFormatErrorCode code, string message, long? offset = null, ushort? sectionType = null, int? entryIndex = null, Exception? inner = null)
        => new(code, message, offset, sectionType, entryIndex, inner);

    private static void Throw(GameEventScriptProgramFormatErrorCode code, string message, long? offset = null, ushort? sectionType = null, int? entryIndex = null)
        => throw Format(code, message, offset, sectionType, entryIndex);

    private readonly record struct SectionDescriptor(ushort Type, GameEventScriptSectionFlags Flags, ushort Version, int PayloadOffset, int PayloadLength, int Ordinal, bool ParseKnown);
    private readonly record struct ProgramMetadata(string ModuleName, ulong ProgramVersion, ushort RequiredRegisters, ushort RequiredCallDepth);

    private ref struct PayloadReader
    {
        private readonly ReadOnlySpan<byte> _bytes;
        private readonly int _baseOffset;
        private readonly ushort? _sectionType;
        private int _offset;

        internal PayloadReader(ReadOnlySpan<byte> bytes, int baseOffset, ushort? sectionType) { _bytes = bytes; _baseOffset = baseOffset; _sectionType = sectionType; _offset = 0; }
        internal int Remaining => _bytes.Length - _offset;
        internal int AbsoluteOffset => _baseOffset + _offset;
        internal byte ReadByte() { Require(1); return _bytes[_offset++]; }
        internal ushort ReadUInt16()
        {
            Require(2);
            var value = (ushort)(_bytes[_offset] | _bytes[_offset + 1] << 8);
            _offset += 2;
            return value;
        }
        internal uint ReadUInt32()
        {
            var low = ReadUInt16();
            var high = ReadUInt16();
            return (uint)(low | high << 16);
        }
        internal ulong ReadUInt64()
        {
            var low = ReadUInt32();
            var high = ReadUInt32();
            return low | (ulong)high << 32;
        }
        internal int ReadCount(int maximum)
        {
            var value = ReadUInt32();
            if (value > maximum || value > int.MaxValue) Throw(GameEventScriptProgramFormatErrorCode.TooManyEntries, "Entry count exceeds its configured or V1 limit.", AbsoluteOffset - 4, _sectionType);
            return checked((int)value);
        }
        internal int ReadLength()
        {
            var value = ReadUInt32();
            if (value > int.MaxValue) Throw(GameEventScriptProgramFormatErrorCode.SectionTooLarge, "Length exceeds the portable in-memory range.", AbsoluteOffset - 4, _sectionType);
            return checked((int)value);
        }
        internal ReadOnlySpan<byte> ReadBytes(int length, int? entryIndex = null)
        {
            Require(length, entryIndex);
            var value = _bytes.Slice(_offset, length);
            _offset += length;
            return value;
        }
        internal string ReadString(int? entryIndex = null)
        {
            var length = ReadLength();
            var value = ReadBytes(length, entryIndex);
            try { return StrictUtf8.GetString(value); }
            catch (DecoderFallbackException exception) { throw Format(GameEventScriptProgramFormatErrorCode.InvalidUtf8, "String contains invalid UTF-8.", AbsoluteOffset - length, _sectionType, entryIndex, exception); }
        }
        internal void RequireEnd()
        {
            if (_offset != _bytes.Length) Throw(GameEventScriptProgramFormatErrorCode.InvalidPayloadLength, "Section payload has trailing or unconsumed bytes.", AbsoluteOffset, _sectionType);
        }
        private void Require(int count, int? entryIndex = null)
        {
            if (count < 0 || count > Remaining) Throw(GameEventScriptProgramFormatErrorCode.TruncatedSectionPayload, "Section payload is truncated.", AbsoluteOffset, _sectionType, entryIndex);
        }
    }
}
