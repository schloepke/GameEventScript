// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace GameEventScript.Api;

/// <summary>
/// Represents a game event script program metadata segment.
/// </summary>
public readonly struct GesProgramMetadataSegment
{
    /// <summary>
    /// Initializes a new instance of Game Event Script Program Metadata Segment.
    /// </summary>
    /// <param name="moduleName">The module name value.</param>
    /// <param name="requiredRegisterCount">The required register count value.</param>
    /// <param name="requiredCallStackDepth">The required call stack depth value.</param>
    /// <param name="programVersion">The program version value.</param>
    public GesProgramMetadataSegment(string moduleName, ushort requiredRegisterCount, ushort requiredCallStackDepth, ulong programVersion)
    {
        ModuleName = moduleName ?? string.Empty;
        RequiredRegisterCount = requiredRegisterCount;
        RequiredCallStackDepth = requiredCallStackDepth;
        ProgramVersion = programVersion;
    }

    /// <summary>
    /// Gets the module name.
    /// </summary>
    public string ModuleName { get; }
    /// <summary>
    /// Gets the required register count.
    /// </summary>
    public ushort RequiredRegisterCount { get; }
    /// <summary>
    /// Gets the required call stack depth.
    /// </summary>
    public ushort RequiredCallStackDepth { get; }
    /// <summary>
    /// Gets the program version.
    /// </summary>
    public ulong ProgramVersion { get; }
}

/// <summary>
/// Represents a game event script string constant segment.
/// </summary>
public readonly struct GesStringConstantSegment
{
    /// <summary>
    /// Represents a slice entry.
    /// </summary>
    public readonly struct SliceEntry
    {
        /// <summary>
        /// Gets the start.
        /// </summary>
        public int Start { get; init; }
        /// <summary>
        /// Gets the length.
        /// </summary>
        public int Length { get; init; }
    }

    private readonly GesReadOnlyArray<SliceEntry> _slices;
    private readonly GesReadOnlyArray<byte> _data;

    /// <summary>
    /// Initializes a new instance of Game Event Script String Constant Segment.
    /// </summary>
    /// <param name="slices">The slices value.</param>
    /// <param name="data">The data value.</param>
    public GesStringConstantSegment(IReadOnlyList<SliceEntry> slices, IReadOnlyList<byte> data)
    {
        _slices = new GesReadOnlyArray<SliceEntry>(slices);
        _data = new GesReadOnlyArray<byte>(data);
    }

    /// <summary>
    /// Gets the slices.
    /// </summary>
    public GesReadOnlyArray<SliceEntry> Slices => _slices;
    /// <summary>
    /// Gets the data.
    /// </summary>
    public GesReadOnlyArray<byte> Data => _data;

    /// <summary>
    /// Resolves the size.
    /// </summary>
    /// <param name="index">The index value.</param>
    /// <returns>The result of the operation.</returns>
    public int ResolveSize(ushort index) => _slices[index].Length;
    /// <summary>
    /// Resolves the value.
    /// </summary>
    /// <param name="index">The index value.</param>
    /// <returns>The result of the operation.</returns>
    public string Resolve(ushort index)
    {
        var slice = _slices[index];
        return _data.DecodeUtf8(Encoding.UTF8, slice.Start, slice.Length);
    }
}

/// <summary>
/// Represents an immutable slice of unsigned 16-bit indices.
/// </summary>
public readonly struct GesUInt16IndexList
{
    private readonly GesReadOnlyArray<ushort> _data;

    internal GesUInt16IndexList(GesReadOnlyArray<ushort> data, int start, int length)
    {
        _data = data;
        Start = start;
        Length = length;
    }

    /// <summary>
    /// Gets the slice's starting offset in the segment storage.
    /// </summary>
    public int Start { get; }

    /// <summary>
    /// Gets the number of indices in the slice.
    /// </summary>
    public int Length { get; }

    /// <summary>
    /// Gets the value at the specified zero-based index relative to this slice.
    /// </summary>
    /// <param name="index">The relative index, which must be nonnegative and less than <see cref="Length"/>.</param>
    /// <exception cref="ArgumentOutOfRangeException">The index is outside this slice. An empty slice has no valid index.</exception>
    public ushort this[int index]
    {
        get
        {
            if (index < 0 || index >= Length) throw new ArgumentOutOfRangeException(nameof(index));
            return _data[Start + index];
        }
    }
}

/// <summary>
/// Represents a game event script u int16 index list segment.
/// </summary>
public readonly struct GesUInt16IndexListSegment
{
    /// <summary>
    /// Represents a slice entry.
    /// </summary>
    public readonly struct SliceEntry
    {
        /// <summary>
        /// Gets the start.
        /// </summary>
        public int Start { get; init; }
        /// <summary>
        /// Gets the length.
        /// </summary>
        public int Length { get; init; }
    }

    private readonly GesReadOnlyArray<SliceEntry> _slices;
    private readonly GesReadOnlyArray<ushort> _data;

    /// <summary>
    /// Initializes a new instance of Game Event Script U Int16 Index List Segment.
    /// </summary>
    /// <param name="slices">The slices value.</param>
    /// <param name="data">The data value.</param>
    public GesUInt16IndexListSegment(IReadOnlyList<SliceEntry> slices, IReadOnlyList<ushort> data)
    {
        _slices = new GesReadOnlyArray<SliceEntry>(slices);
        _data = new GesReadOnlyArray<ushort>(data);
    }

    /// <summary>
    /// Gets the slices.
    /// </summary>
    public GesReadOnlyArray<SliceEntry> Slices => _slices;
    /// <summary>
    /// Gets the data.
    /// </summary>
    public GesReadOnlyArray<ushort> Data => _data;

    /// <summary>
    /// Resolves the value.
    /// </summary>
    /// <param name="index">The index value.</param>
    /// <returns>The result of the operation.</returns>
    public GesUInt16IndexList Resolve(ushort index) => new(_data, _slices[index].Start, _slices[index].Length);
}

/// <summary>
/// Represents a game event script binding segment.
/// </summary>
public readonly struct GesBindingSegment
{

    /// <summary>
    /// Represents a game event script binary bind entry.
    /// </summary>
    public readonly struct Entry(
        GesBinaryBindKind kind,
        ushort name,
        IReadOnlyList<ushort>? argumentNames,
        ushort entryAddress = 0xFFFF,
        ushort id = 0xFFFF,
        IReadOnlyList<ushort>? requiredTags = null,
        IReadOnlyList<ushort>? excludedTags = null,
        ushort requiredRegisterCount = 0,
        ushort requiredCallStackDepth = 0)
    {
        private readonly GesReadOnlyArray<ushort> _argumentNames = new(argumentNames);
        private readonly GesReadOnlyArray<ushort> _requiredTags = new(requiredTags);
        private readonly GesReadOnlyArray<ushort> _excludedTags = new(excludedTags);

        /// <summary>
        /// Gets the id.
        /// </summary>
        public ushort Id { get; } = id;

        /// <summary>
        /// Gets the kind.
        /// </summary>
        public GesBinaryBindKind Kind { get; } = kind;

        /// <summary>
        /// Gets the name.
        /// </summary>
        public ushort Name { get; } = name;

        /// <summary>
        /// Gets the argument names.
        /// </summary>
        public GesReadOnlyArray<ushort> ArgumentNames => _argumentNames;

        /// <summary>
        /// Gets the required tags.
        /// </summary>
        public GesReadOnlyArray<ushort> RequiredTags => _requiredTags;

        /// <summary>
        /// Gets the excluded tags.
        /// </summary>
        public GesReadOnlyArray<ushort> ExcludedTags => _excludedTags;

        /// <summary>
        /// Gets the entry address.
        /// </summary>
        public ushort EntryAddress { get; } = entryAddress;

        /// <summary>
        /// Gets the required register count.
        /// </summary>
        public ushort RequiredRegisterCount { get; } = requiredRegisterCount;

        /// <summary>
        /// Gets the required call stack depth.
        /// </summary>
        public ushort RequiredCallStackDepth { get; } = requiredCallStackDepth;

    }

    private readonly GesReadOnlyArray<GesBindingSegment.Entry> _entries;

    /// <summary>
    /// Initializes a new instance of Game Event Script Binding Segment.
    /// </summary>
    /// <param name="entries">The entries value.</param>
    public GesBindingSegment(IReadOnlyList<GesBindingSegment.Entry>? entries)
    {
        _entries = new GesReadOnlyArray<GesBindingSegment.Entry>(entries);
        EntryCount = ToEntryCount(_entries.Count);
    }

    /// <summary>
    /// Gets the entry count.
    /// </summary>
    public ushort EntryCount { get; }

    /// <summary>
    /// Gets the entries.
    /// </summary>
    public GesReadOnlyArray<GesBindingSegment.Entry> Entries => _entries;

    private static ushort ToEntryCount(int count) => count > ushort.MaxValue ? throw new ArgumentOutOfRangeException(nameof(count), "GameEventScriptProgram tables cannot exceed 65535 entries.") : checked((ushort)count);

}

/// <summary>
/// Represents a game event script code segment.
/// </summary>
public readonly struct GesCodeSegment
{
    private readonly GesReadOnlyArray<GesBytecodeInstruction> _instructions;

    /// <summary>
    /// Initializes a new instance of Game Event Script Code Segment.
    /// </summary>
    /// <param name="instructions">The instructions value.</param>
    public GesCodeSegment(IReadOnlyList<GesBytecodeInstruction>? instructions)
        => _instructions = new GesReadOnlyArray<GesBytecodeInstruction>(instructions);

    /// <summary>
    /// Gets the count.
    /// </summary>
    public int Count => _instructions.Count;
    /// <summary>
    /// Gets the length.
    /// </summary>
    public int Length => _instructions.Count;
    /// <summary>
    /// Gets the value at the specified index.
    /// </summary>
    /// <param name="index">The index value.</param>
    public GesBytecodeInstruction this[int index] => _instructions[index];
    /// <summary>
    /// Gets the instructions.
    /// </summary>
    public GesReadOnlyArray<GesBytecodeInstruction> Instructions => _instructions;
}

/// <summary>
/// Defines the supported game event script debug symbol kind values.
/// </summary>
public enum GesDebugSymbolKind : byte
{
    /// <summary>
    /// Identifies the parameter value.
    /// </summary>
    Parameter = 1,
    /// <summary>
    /// Identifies the local value.
    /// </summary>
    Local = 2
}

/// <summary>
/// Represents a game event script debug symbol.
/// </summary>
/// <param name="Kind">The kind value.</param>
/// <param name="RegisterId">The register id value.</param>
/// <param name="Name">The name value.</param>
/// <param name="CodeStart">The code start value.</param>
/// <param name="CodeLength">The code length value.</param>
public readonly record struct GesDebugSymbol(GesDebugSymbolKind Kind, ushort RegisterId, string Name, uint CodeStart, uint CodeLength);

/// <summary>
/// Represents a game event script debug symbols segment.
/// </summary>
public sealed class GesDebugSymbolsSegment
{
    /// <summary>
    /// Initializes a new instance of Game Event Script Debug Symbols Segment.
    /// </summary>
    /// <param name="symbols">The symbols value.</param>
    public GesDebugSymbolsSegment(IReadOnlyList<GesDebugSymbol>? symbols)
        => Symbols = new GesReadOnlyArray<GesDebugSymbol>(symbols);

    /// <summary>
    /// Gets the symbols.
    /// </summary>
    public GesReadOnlyArray<GesDebugSymbol> Symbols { get; }
}

/// <summary>
/// Represents a game event script source map source.
/// </summary>
public sealed class GesSourceMapSource
{
    /// <summary>
    /// Initializes a new instance of Game Event Script Source Map Source.
    /// </summary>
    /// <param name="sourceId">The source id value.</param>
    /// <param name="sourceName">The source name value.</param>
    /// <param name="sourceByteLength">The source byte length value.</param>
    /// <param name="sha256">The sha256 value.</param>
    /// <param name="lineStartByteOffsets">The line start byte offsets value.</param>
    public GesSourceMapSource(uint sourceId, string sourceName, uint sourceByteLength, IReadOnlyList<byte> sha256, IReadOnlyList<uint> lineStartByteOffsets)
    {
        SourceId = sourceId;
        SourceName = sourceName ?? string.Empty;
        SourceByteLength = sourceByteLength;
        Sha256 = new GesReadOnlyArray<byte>(sha256);
        LineStartByteOffsets = new GesReadOnlyArray<uint>(lineStartByteOffsets);
    }

    /// <summary>
    /// Gets the source id.
    /// </summary>
    public uint SourceId { get; }
    /// <summary>
    /// Gets the source name.
    /// </summary>
    public string SourceName { get; }
    /// <summary>
    /// Gets the source byte length.
    /// </summary>
    public uint SourceByteLength { get; }
    /// <summary>
    /// Gets the sha256.
    /// </summary>
    public GesReadOnlyArray<byte> Sha256 { get; }
    /// <summary>
    /// Gets the line start byte offsets.
    /// </summary>
    public GesReadOnlyArray<uint> LineStartByteOffsets { get; }
}

/// <summary>
/// Represents a game event script source map entry.
/// </summary>
/// <param name="CodeStart">The code start value.</param>
/// <param name="CodeLength">The code length value.</param>
/// <param name="SourceId">The source id value.</param>
/// <param name="SourceStartByteOffset">The source start byte offset value.</param>
/// <param name="SourceByteLength">The source byte length value.</param>
public readonly record struct GesSourceMapEntry(uint CodeStart, uint CodeLength, uint SourceId, uint SourceStartByteOffset, uint SourceByteLength);

/// <summary>
/// Represents a game event script source map segment.
/// </summary>
public sealed class GesSourceMapSegment
{
    /// <summary>
    /// Initializes a new instance of Game Event Script Source Map Segment.
    /// </summary>
    /// <param name="sources">The sources value.</param>
    /// <param name="entries">The entries value.</param>
    public GesSourceMapSegment(IReadOnlyList<GesSourceMapSource>? sources, IReadOnlyList<GesSourceMapEntry>? entries)
    {
        Sources = new GesReadOnlyArray<GesSourceMapSource>(sources);
        Entries = new GesReadOnlyArray<GesSourceMapEntry>(entries);
    }

    /// <summary>
    /// Gets the sources.
    /// </summary>
    public GesReadOnlyArray<GesSourceMapSource> Sources { get; }
    /// <summary>
    /// Gets the entries.
    /// </summary>
    public GesReadOnlyArray<GesSourceMapEntry> Entries { get; }
}

/// <summary>
/// Represents a game event script source archive entry.
/// </summary>
public sealed class GesSourceArchiveEntry
{
    private readonly GesReadOnlyArray<byte> _utf8Content;

    /// <summary>
    /// Initializes a new instance of Game Event Script Source Archive Entry.
    /// </summary>
    /// <param name="sourceId">The source id value.</param>
    /// <param name="sourceName">The source name value.</param>
    /// <param name="utf8Content">The utf8 content value.</param>
    public GesSourceArchiveEntry(uint sourceId, string sourceName, IReadOnlyList<byte> utf8Content)
    {
        SourceId = sourceId;
        SourceName = sourceName ?? string.Empty;
        _utf8Content = new GesReadOnlyArray<byte>(utf8Content);
    }

    /// <summary>
    /// Gets the source id.
    /// </summary>
    public uint SourceId { get; }
    /// <summary>
    /// Gets the source name.
    /// </summary>
    public string SourceName { get; }
    /// <summary>
    /// Gets the utf8 content.
    /// </summary>
    public GesReadOnlyArray<byte> Utf8Content => _utf8Content;
    /// <summary>
    /// Resolves the text.
    /// </summary>
    /// <returns>The result of the operation.</returns>
    public string ResolveText() => DecodeText(Encoding.UTF8);

    internal string DecodeText(Encoding encoding) => _utf8Content.DecodeUtf8(encoding, 0, _utf8Content.Count);
}

/// <summary>
/// Represents a game event script source archive segment.
/// </summary>
public sealed class GesSourceArchiveSegment
{
    /// <summary>
    /// Initializes a new instance of Game Event Script Source Archive Segment.
    /// </summary>
    /// <param name="sources">The sources value.</param>
    public GesSourceArchiveSegment(IReadOnlyList<GesSourceArchiveEntry>? sources)
        => Sources = new GesReadOnlyArray<GesSourceArchiveEntry>(sources);

    /// <summary>
    /// Gets the sources.
    /// </summary>
    public GesReadOnlyArray<GesSourceArchiveEntry> Sources { get; }
}

/// <summary>
/// Represents a game event script build metadata segment.
/// </summary>
public sealed class GesBuildMetadataSegment
{
    /// <summary>
    /// Initializes a new instance of Game Event Script Build Metadata Segment.
    /// </summary>
    /// <param name="compilerId">The compiler id value.</param>
    /// <param name="compilerVersion">The compiler version value.</param>
    public GesBuildMetadataSegment(string compilerId, string compilerVersion)
    {
        CompilerId = compilerId ?? string.Empty;
        CompilerVersion = compilerVersion ?? string.Empty;
    }

    /// <summary>
    /// Gets the compiler id.
    /// </summary>
    public string CompilerId { get; }
    /// <summary>
    /// Gets the compiler version.
    /// </summary>
    public string CompilerVersion { get; }
}

/// <summary>
/// Represents a game event script opaque section.
/// </summary>
public sealed class GesOpaqueSection
{
    private readonly GesReadOnlyArray<byte> _rawPayload;

    /// <summary>
    /// Initializes a new instance of Game Event Script Opaque Section.
    /// </summary>
    /// <param name="sectionType">The section type value.</param>
    /// <param name="flags">The flags value.</param>
    /// <param name="sectionVersion">The section version value.</param>
    /// <param name="rawPayload">The raw payload value.</param>
    /// <param name="originalOrdinal">The original ordinal value.</param>
    public GesOpaqueSection(ushort sectionType, GesSectionFlags flags, ushort sectionVersion, IReadOnlyList<byte> rawPayload, int originalOrdinal)
    {
        SectionType = sectionType;
        Flags = flags;
        SectionVersion = sectionVersion;
        _rawPayload = new GesReadOnlyArray<byte>(rawPayload);
        OriginalOrdinal = originalOrdinal;
    }

    /// <summary>
    /// Gets the section type.
    /// </summary>
    public ushort SectionType { get; }
    /// <summary>
    /// Gets the flags.
    /// </summary>
    public GesSectionFlags Flags { get; }
    /// <summary>
    /// Gets the section version.
    /// </summary>
    public ushort SectionVersion { get; }
    /// <summary>
    /// Gets the raw payload.
    /// </summary>
    public GesReadOnlyArray<byte> RawPayload => _rawPayload;
    /// <summary>
    /// Gets the original ordinal.
    /// </summary>
    public int OriginalOrdinal { get; }
}
