#pragma warning disable CS1591

using System;
using System.Collections.Generic;
using System.Text;

namespace StepH.GameEventScript.Api;

public static class GameEventScriptProgramWriter
{
    private const ushort SectionVersion = 1;
    private static readonly UTF8Encoding Utf8 = new(false, true);

    public static int GetEncodedSize(GameEventScriptProgram program)
    {
        _ = program ?? throw new ArgumentNullException(nameof(program));
        GameEventScriptProgramValidator.Validate(program);

        long size = GameEventScriptBinaryFormat.HeaderSize;
        size = AddSection(size, 16);
        size = AddSection(size, GetStringConstantsSize(program.StringConstants));
        size = AddSection(size, GetIndexListsSize(program.UInt16IndexLists));
        size = AddSection(size, GetBindingsSize(program.Bindings));
        size = AddSection(size, GetCodeSize(program.Code));
        if (program.DebugSymbols is not null) size = AddSection(size, GetDebugSymbolsSize(program.DebugSymbols));
        if (program.SourceMap is not null) size = AddSection(size, GetSourceMapSize(program.SourceMap));
        if (program.SourceArchive is not null) size = AddSection(size, GetSourceArchiveSize(program.SourceArchive));
        if (program.BuildMetadata is not null) size = AddSection(size, GetBuildMetadataSize(program.BuildMetadata));
        for (var index = 0; index < program.OpaqueSections.Count; index++)
            size = AddSection(size, program.OpaqueSections[index].RawPayload.Count);

        if (size > uint.MaxValue || size > int.MaxValue)
            throw Format(GameEventScriptProgramFormatErrorCode.SectionTooLarge, "The encoded program exceeds the V1 file-size range.");
        return checked((int)size);
    }

    public static byte[] ToArray(GameEventScriptProgram program)
    {
        var result = new byte[GetEncodedSize(program)];
        Write(program, result);
        return result;
    }

    public static int Write(GameEventScriptProgram program, Span<byte> destination)
    {
        _ = program ?? throw new ArgumentNullException(nameof(program));
        var size = GetEncodedSize(program);
        if (destination.Length < size) throw new ArgumentException("Destination is smaller than the encoded program.", nameof(destination));

        var writer = new SpanWriter(destination[..size]);
        writer.WriteByte(GameEventScriptBinaryFormat.MagicG);
        writer.WriteByte(GameEventScriptBinaryFormat.MagicE);
        writer.WriteByte(GameEventScriptBinaryFormat.MagicS);
        writer.WriteByte(GameEventScriptBinaryFormat.MagicB);
        writer.WriteUInt16(GameEventScriptBinaryFormat.Version);
        writer.WriteUInt16(0);
        writer.WriteUInt32(GameEventScriptBinaryFormat.HeaderSize);
        writer.WriteUInt32(checked((uint)size));

        WriteSectionHeader(ref writer, GameEventScriptSectionType.ProgramMetadata, GameEventScriptSectionFlags.Required, 16);
        WriteProgramMetadata(ref writer, program);
        WriteSectionHeader(ref writer, GameEventScriptSectionType.StringConstants, GameEventScriptSectionFlags.Required, GetStringConstantsSize(program.StringConstants));
        WriteStringConstants(ref writer, program.StringConstants);
        WriteSectionHeader(ref writer, GameEventScriptSectionType.UInt16IndexLists, GameEventScriptSectionFlags.Required, GetIndexListsSize(program.UInt16IndexLists));
        WriteIndexLists(ref writer, program.UInt16IndexLists);
        WriteSectionHeader(ref writer, GameEventScriptSectionType.Bindings, GameEventScriptSectionFlags.Required, GetBindingsSize(program.Bindings));
        WriteBindings(ref writer, program);
        WriteSectionHeader(ref writer, GameEventScriptSectionType.Code, GameEventScriptSectionFlags.Required, GetCodeSize(program.Code));
        WriteCode(ref writer, program.Code);

        if (program.DebugSymbols is not null)
        {
            var payloadSize = GetDebugSymbolsSize(program.DebugSymbols);
            WriteSectionHeader(ref writer, GameEventScriptSectionType.DebugSymbols, GameEventScriptSectionFlags.None, payloadSize);
            WriteDebugSymbols(ref writer, program.DebugSymbols);
        }
        if (program.SourceMap is not null)
        {
            var payloadSize = GetSourceMapSize(program.SourceMap);
            WriteSectionHeader(ref writer, GameEventScriptSectionType.SourceMap, GameEventScriptSectionFlags.None, payloadSize);
            WriteSourceMap(ref writer, program.SourceMap);
        }
        if (program.SourceArchive is not null)
        {
            var payloadSize = GetSourceArchiveSize(program.SourceArchive);
            WriteSectionHeader(ref writer, GameEventScriptSectionType.SourceArchive, GameEventScriptSectionFlags.None, payloadSize);
            WriteSourceArchive(ref writer, program.SourceArchive);
        }
        if (program.BuildMetadata is not null)
        {
            var payloadSize = GetBuildMetadataSize(program.BuildMetadata);
            WriteSectionHeader(ref writer, GameEventScriptSectionType.BuildMetadata, GameEventScriptSectionFlags.None, payloadSize);
            WriteBuildMetadata(ref writer, program.BuildMetadata);
        }

        var opaque = program.OpaqueSections.UnsafeItems;
        if (opaque.Length > 1)
        {
            opaque = (GameEventScriptOpaqueSection[])opaque.Clone();
            Array.Sort(opaque, static (left, right) => left.OriginalOrdinal.CompareTo(right.OriginalOrdinal));
        }
        for (var index = 0; index < opaque.Length; index++)
        {
            var section = opaque[index];
            WriteSectionHeader(ref writer, section.SectionType, section.Flags, section.RawPayload.Count, section.SectionVersion);
            writer.WriteBytes(section.RawPayload.UnsafeItems);
        }

        return writer.Offset;
    }

    private static void WriteProgramMetadata(ref SpanWriter writer, GameEventScriptProgram program)
    {
        writer.WriteUInt16(FindString(program.StringConstants, program.ModuleName));
        writer.WriteUInt16(program.RequiredRegisterCount);
        writer.WriteUInt16(program.RequiredCallStackDepth);
        writer.WriteUInt16(0);
        writer.WriteUInt64(program.ProgramVersion);
    }

    private static int GetStringConstantsSize(GameEventScriptStringConstantSegment segment)
    {
        long size = 4;
        for (var index = 0; index < segment.Slices.Count; index++) size += 4L + segment.Slices[index].Length;
        return CheckedPayloadSize(size);
    }

    private static void WriteStringConstants(ref SpanWriter writer, GameEventScriptStringConstantSegment segment)
    {
        writer.WriteUInt32(checked((uint)segment.Slices.Count));
        for (var index = 0; index < segment.Slices.Count; index++)
        {
            var slice = segment.Slices[index];
            writer.WriteUInt32(checked((uint)slice.Length));
            writer.WriteBytes(segment.Data.UnsafeItems.AsSpan(slice.Start, slice.Length));
        }
    }

    private static int GetIndexListsSize(GameEventScriptUInt16IndexListSegment segment)
    {
        long size = 4;
        for (var index = 0; index < segment.Slices.Count; index++) size += 4L + 2L * segment.Slices[index].Length;
        return CheckedPayloadSize(size);
    }

    private static void WriteIndexLists(ref SpanWriter writer, GameEventScriptUInt16IndexListSegment segment)
    {
        writer.WriteUInt32(checked((uint)segment.Slices.Count));
        for (var index = 0; index < segment.Slices.Count; index++)
        {
            var list = segment.Resolve(checked((ushort)index));
            writer.WriteUInt32(checked((uint)list.Length));
            for (var element = 0; element < list.Length; element++) writer.WriteUInt16(list[element]);
        }
    }

    private static int GetBindingsSize(GameEventScriptBindingSegment segment)
        => CheckedPayloadSize(4L + 20L * segment.Entries.Count);

    private static void WriteBindings(ref SpanWriter writer, GameEventScriptProgram program)
    {
        writer.WriteUInt32(checked((uint)program.Bindings.Entries.Count));
        for (var index = 0; index < program.Bindings.Entries.Count; index++)
        {
            var bind = program.Bindings.Entries[index];
            writer.WriteByte((byte)bind.Kind);
            writer.WriteByte(0);
            writer.WriteUInt16(bind.Id);
            writer.WriteUInt16(bind.Name);
            writer.WriteUInt16(FindList(program.UInt16IndexLists, bind.ArgumentNames));
            writer.WriteUInt16(FindList(program.UInt16IndexLists, bind.RequiredTags));
            writer.WriteUInt16(FindList(program.UInt16IndexLists, bind.ExcludedTags));
            writer.WriteUInt16(bind.EntryAddress);
            writer.WriteUInt16(bind.RequiredRegisterCount);
            writer.WriteUInt16(bind.RequiredCallStackDepth);
            writer.WriteUInt16(0);
        }
    }

    private static int GetCodeSize(GameEventScriptCodeSegment segment)
        => CheckedPayloadSize(4L + 16L * segment.Count);

    private static void WriteCode(ref SpanWriter writer, GameEventScriptCodeSegment segment)
    {
        writer.WriteUInt32(checked((uint)segment.Count));
        for (var index = 0; index < segment.Count; index++)
        {
            var instruction = segment[index];
            writer.WriteByte((byte)instruction.OpCode);
            writer.WriteByte(instruction.UnitAndFlags);
            writer.WriteUInt16(instruction.DestinationRegister);
            writer.WriteUInt16(instruction.XRegister);
            writer.WriteUInt16(instruction.YRegister);
            writer.WriteUInt64(instruction.Payload);
        }
    }

    private static int GetDebugSymbolsSize(GameEventScriptDebugSymbolsSegment segment)
    {
        var names = BuildDebugNames(segment);
        long size = 4 + 4L + 16L * segment.Symbols.Count;
        for (var index = 0; index < names.Count; index++) size += 4L + Utf8.GetByteCount(names[index]);
        return CheckedPayloadSize(size);
    }

    private static void WriteDebugSymbols(ref SpanWriter writer, GameEventScriptDebugSymbolsSegment segment)
    {
        var names = BuildDebugNames(segment);
        writer.WriteUInt32(checked((uint)names.Count));
        for (var index = 0; index < names.Count; index++) writer.WriteString(names[index]);
        writer.WriteUInt32(checked((uint)segment.Symbols.Count));
        for (var index = 0; index < segment.Symbols.Count; index++)
        {
            var symbol = segment.Symbols[index];
            writer.WriteByte((byte)symbol.Kind);
            writer.WriteByte(0);
            writer.WriteUInt16(symbol.RegisterId);
            writer.WriteUInt32(checked((uint)names.IndexOf(symbol.Name)));
            writer.WriteUInt32(symbol.CodeStart);
            writer.WriteUInt32(symbol.CodeLength);
        }
    }

    private static List<string> BuildDebugNames(GameEventScriptDebugSymbolsSegment segment)
    {
        var names = new List<string>();
        for (var index = 0; index < segment.Symbols.Count; index++)
        {
            var name = segment.Symbols[index].Name;
            if (!names.Contains(name)) names.Add(name);
        }
        return names;
    }

    private static int GetSourceMapSize(GameEventScriptSourceMapSegment segment)
    {
        long size = 4;
        for (var index = 0; index < segment.Sources.Count; index++)
        {
            var source = segment.Sources[index];
            size += 4L + Utf8.GetByteCount(source.SourceName) + 4L + 32L + 4L + 4L * source.LineStartByteOffsets.Count;
        }
        size += 4L + 20L * segment.Entries.Count;
        return CheckedPayloadSize(size);
    }

    private static void WriteSourceMap(ref SpanWriter writer, GameEventScriptSourceMapSegment segment)
    {
        writer.WriteUInt32(checked((uint)segment.Sources.Count));
        for (var index = 0; index < segment.Sources.Count; index++)
        {
            var source = segment.Sources[index];
            writer.WriteString(source.SourceName);
            writer.WriteUInt32(source.SourceByteLength);
            writer.WriteBytes(source.Sha256.UnsafeItems);
            writer.WriteUInt32(checked((uint)source.LineStartByteOffsets.Count));
            for (var line = 0; line < source.LineStartByteOffsets.Count; line++) writer.WriteUInt32(source.LineStartByteOffsets[line]);
        }
        writer.WriteUInt32(checked((uint)segment.Entries.Count));
        for (var index = 0; index < segment.Entries.Count; index++)
        {
            var mapping = segment.Entries[index];
            writer.WriteUInt32(mapping.CodeStart);
            writer.WriteUInt32(mapping.CodeLength);
            writer.WriteUInt32(mapping.SourceId);
            writer.WriteUInt32(mapping.SourceStartByteOffset);
            writer.WriteUInt32(mapping.SourceByteLength);
        }
    }

    private static int GetSourceArchiveSize(GameEventScriptSourceArchiveSegment segment)
    {
        long size = 4;
        for (var index = 0; index < segment.Sources.Count; index++)
        {
            var source = segment.Sources[index];
            size += 4L + 4L + Utf8.GetByteCount(source.SourceName) + 4L + source.Utf8Content.Count;
        }
        return CheckedPayloadSize(size);
    }

    private static void WriteSourceArchive(ref SpanWriter writer, GameEventScriptSourceArchiveSegment segment)
    {
        writer.WriteUInt32(checked((uint)segment.Sources.Count));
        for (var index = 0; index < segment.Sources.Count; index++)
        {
            var source = segment.Sources[index];
            writer.WriteUInt32(source.SourceId);
            writer.WriteString(source.SourceName);
            writer.WriteUInt32(checked((uint)source.Utf8Content.Count));
            writer.WriteBytes(source.Utf8Content.UnsafeItems);
        }
    }

    private static int GetBuildMetadataSize(GameEventScriptBuildMetadataSegment segment)
        => CheckedPayloadSize(8L + Utf8.GetByteCount(segment.CompilerId) + Utf8.GetByteCount(segment.CompilerVersion));

    private static void WriteBuildMetadata(ref SpanWriter writer, GameEventScriptBuildMetadataSegment segment)
    {
        writer.WriteString(segment.CompilerId);
        writer.WriteString(segment.CompilerVersion);
    }

    private static ushort FindString(GameEventScriptStringConstantSegment segment, string value)
    {
        for (var index = 0; index < segment.Slices.Count; index++)
            if (string.Equals(segment.Resolve(checked((ushort)index)), value, StringComparison.Ordinal)) return checked((ushort)index);
        throw Format(GameEventScriptProgramFormatErrorCode.InvalidStringIndex, "ModuleName is not present in StringConstantSegment.");
    }

    private static ushort FindList(GameEventScriptUInt16IndexListSegment segment, GameEventScriptReadOnlyArray<ushort> values)
    {
        for (var index = 0; index < segment.Slices.Count; index++)
        {
            var candidate = segment.Resolve(checked((ushort)index));
            if (candidate.Length != values.Count) continue;
            var equal = true;
            for (var element = 0; element < candidate.Length; element++)
                if (candidate[element] != values[element]) { equal = false; break; }
            if (equal) return checked((ushort)index);
        }
        if (values.Count == 0) return ushort.MaxValue;
        throw Format(GameEventScriptProgramFormatErrorCode.InvalidListIndex, "A binding list is not present in UInt16IndexListSegment.");
    }

    private static void WriteSectionHeader(ref SpanWriter writer, GameEventScriptSectionType type, GameEventScriptSectionFlags flags, int payloadLength, ushort version = SectionVersion)
        => WriteSectionHeader(ref writer, (ushort)type, flags, payloadLength, version);

    private static void WriteSectionHeader(ref SpanWriter writer, ushort type, GameEventScriptSectionFlags flags, int payloadLength, ushort version)
    {
        writer.WriteUInt16(type);
        writer.WriteUInt16((ushort)flags);
        writer.WriteUInt16(version);
        writer.WriteUInt16(0);
        writer.WriteUInt32(checked((uint)payloadLength));
    }

    private static long AddSection(long total, int payloadLength)
        => checked(total + GameEventScriptBinaryFormat.SectionHeaderSize + payloadLength);

    private static int CheckedPayloadSize(long value)
    {
        if (value < 0 || value > uint.MaxValue || value > int.MaxValue)
            throw Format(GameEventScriptProgramFormatErrorCode.SectionTooLarge, "A section payload exceeds the V1 size range.");
        return checked((int)value);
    }

    private static GameEventScriptProgramFormatException Format(GameEventScriptProgramFormatErrorCode code, string message)
        => new(code, message);

    private ref struct SpanWriter
    {
        private readonly Span<byte> _destination;
        internal SpanWriter(Span<byte> destination) { _destination = destination; Offset = 0; }
        internal int Offset { get; private set; }
        internal void WriteByte(byte value) => _destination[Offset++] = value;
        internal void WriteUInt16(ushort value)
        {
            _destination[Offset++] = (byte)value;
            _destination[Offset++] = (byte)(value >> 8);
        }
        internal void WriteUInt32(uint value)
        {
            WriteUInt16((ushort)value);
            WriteUInt16((ushort)(value >> 16));
        }
        internal void WriteUInt64(ulong value)
        {
            WriteUInt32((uint)value);
            WriteUInt32((uint)(value >> 32));
        }
        internal void WriteBytes(ReadOnlySpan<byte> bytes)
        {
            bytes.CopyTo(_destination[Offset..]);
            Offset += bytes.Length;
        }
        internal void WriteString(string value)
        {
            var count = Utf8.GetByteCount(value);
            WriteUInt32(checked((uint)count));
            Offset += Utf8.GetBytes(value.AsSpan(), _destination[Offset..]);
        }
    }
}
