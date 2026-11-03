// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using GameEventScript.Api;

namespace GameEventScript.Runtime.Vm;

// Locates the source of the instructions that use one import binding, for link diagnostics. Each distinct source range
// counts once, ordered by source ID, start and length. The most specific SourceMap entry covering an instruction
// gives its range; lines come from the SourceMap and Unicode-scalar columns from the archived source, so a Program
// without a SourceArchive yields lines only and one without a SourceMap no location at all.
internal static class GesSourceCallSites
{
    internal static List<GesSourceLocation> Locate(GameEventScriptProgram program, GesBytecodeOpCode opCode, ushort bindId)
    {
        var result = new List<GesSourceLocation>();
        if (program.SourceMap is not { } sourceMap) return result;
        var ranges = new List<GesSourceMapEntry>();
        var code = program.Code;
        for (var address = 0; address < code.Count; address++)
        {
            var instruction = code[address];
            if (instruction.OpCode != opCode || instruction.BindId != bindId) continue;
            if (Innermost(sourceMap, (uint)address) is not { } entry) continue;
            if (!ranges.Exists(range => range.SourceId == entry.SourceId && range.SourceStartByteOffset == entry.SourceStartByteOffset && range.SourceByteLength == entry.SourceByteLength))
                ranges.Add(entry);
        }

        ranges.Sort((left, right) => left.SourceId != right.SourceId ? left.SourceId.CompareTo(right.SourceId)
            : left.SourceStartByteOffset != right.SourceStartByteOffset ? left.SourceStartByteOffset.CompareTo(right.SourceStartByteOffset)
            : left.SourceByteLength.CompareTo(right.SourceByteLength));
        foreach (var range in ranges)
            if (Location(program, sourceMap, range) is { } location) result.Add(location);
        return result;
    }

    // The shortest entry that covers the address, the first one among equally long entries.
    private static GesSourceMapEntry? Innermost(GesSourceMapSegment sourceMap, uint address)
    {
        GesSourceMapEntry? best = null;
        foreach (var entry in sourceMap.Entries)
        {
            if (address < entry.CodeStart || address - entry.CodeStart >= entry.CodeLength) continue;
            if (best is null || entry.SourceByteLength < best.Value.SourceByteLength) best = entry;
        }

        return best;
    }

    private static GesSourceLocation? Location(GameEventScriptProgram program, GesSourceMapSegment sourceMap, GesSourceMapEntry range)
    {
        GesSourceMapSource? source = null;
        foreach (var candidate in sourceMap.Sources)
            if (candidate.SourceId == range.SourceId) source = candidate;
        if (source is null) return null;
        GesSourceArchiveEntry? archived = null;
        if (program.SourceArchive is { } archive)
            foreach (var candidate in archive.Sources)
                if (candidate.SourceId == range.SourceId) archived = candidate;

        var end = range.SourceStartByteOffset + range.SourceByteLength;
        var (line, lineStart) = Line(source, range.SourceStartByteOffset);
        var (endLine, endLineStart) = Line(source, end);
        int? column = archived is null ? null : Column(archived.Utf8Content, lineStart, range.SourceStartByteOffset);
        int? endColumn = archived is null ? null : Column(archived.Utf8Content, endLineStart, end);
        return new GesSourceLocation(source.SourceName, line, column, endLine, endColumn, program.ModuleName, range.SourceId);
    }

    // The one-based line of a byte offset and the offset where that line starts.
    private static (int Line, uint Start) Line(GesSourceMapSource source, uint offset)
    {
        var lines = source.LineStartByteOffsets;
        var index = 0;
        while (index + 1 < lines.Count && lines[index + 1] <= offset) index++;
        return (index + 1, lines.Count == 0 ? 0 : lines[index]);
    }

    // The one-based Unicode-scalar column of a byte offset; a leading byte order mark is not counted.
    private static int Column(GesReadOnlyArray<byte> content, uint lineStart, uint offset)
    {
        var column = 1;
        var start = (int)lineStart;
        if (start == 0 && content.Count >= 3 && content[0] == 0xEF && content[1] == 0xBB && content[2] == 0xBF) start = 3;
        for (var index = start; index < offset && index < content.Count; index++)
            if ((content[index] & 0xC0) != 0x80) column++;
        return column;
    }
}
