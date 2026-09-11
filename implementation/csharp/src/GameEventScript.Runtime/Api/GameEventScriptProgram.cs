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
/// Represents a game event script program.
/// </summary>
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

    /// <summary>
    /// Gets the format version.
    /// </summary>
    public ushort FormatVersion { get; }
    /// <summary>
    /// Gets the metadata.
    /// </summary>
    public GameEventScriptProgramMetadataSegment Metadata { get; }
    /// <summary>
    /// Gets the module name.
    /// </summary>
    public string ModuleName => Metadata.ModuleName;
    /// <summary>
    /// Gets the program version.
    /// </summary>
    public ulong ProgramVersion => Metadata.ProgramVersion;
    /// <summary>
    /// Gets the required register count.
    /// </summary>
    public ushort RequiredRegisterCount => Metadata.RequiredRegisterCount;
    /// <summary>
    /// Gets the required call stack depth.
    /// </summary>
    public ushort RequiredCallStackDepth => Metadata.RequiredCallStackDepth;
    /// <summary>
    /// Gets the string constants.
    /// </summary>
    public GameEventScriptStringConstantSegment StringConstants { get; }
    /// <summary>
    /// Gets the u int16 index lists.
    /// </summary>
    public GameEventScriptUInt16IndexListSegment UInt16IndexLists { get; }
    /// <summary>
    /// Gets the bindings.
    /// </summary>
    public GameEventScriptBindingSegment Bindings { get; }
    /// <summary>
    /// Gets the code.
    /// </summary>
    public GameEventScriptCodeSegment Code { get; }
    /// <summary>
    /// Gets the debug symbols.
    /// </summary>
    public GameEventScriptDebugSymbolsSegment? DebugSymbols { get; }
    /// <summary>
    /// Gets the source map.
    /// </summary>
    public GameEventScriptSourceMapSegment? SourceMap { get; }
    /// <summary>
    /// Gets the source archive.
    /// </summary>
    public GameEventScriptSourceArchiveSegment? SourceArchive { get; }
    /// <summary>
    /// Gets the build metadata.
    /// </summary>
    public GameEventScriptBuildMetadataSegment? BuildMetadata { get; }
    /// <summary>
    /// Gets the opaque sections.
    /// </summary>
    public GameEventScriptReadOnlyArray<GameEventScriptOpaqueSection> OpaqueSections { get; }
}

/// <summary>
/// Represents a game event script program metadata segment.
/// </summary>
public readonly struct GameEventScriptProgramMetadataSegment
{
    /// <summary>
    /// Initializes a new instance of Game Event Script Program Metadata Segment.
    /// </summary>
    /// <param name="moduleName">The module name value.</param>
    /// <param name="requiredRegisterCount">The required register count value.</param>
    /// <param name="requiredCallStackDepth">The required call stack depth value.</param>
    /// <param name="programVersion">The program version value.</param>
    public GameEventScriptProgramMetadataSegment(string moduleName, ushort requiredRegisterCount, ushort requiredCallStackDepth, ulong programVersion)
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
/// Immutable array view used by portable programs and other immutable API models.
/// </summary>
public readonly struct GameEventScriptReadOnlyArray<T> : IReadOnlyList<T>
{
    private readonly T[]? _items;

    private GameEventScriptReadOnlyArray(T[] ownedItems) => _items = ownedItems;

    // The caller transfers exclusive ownership; no mutable reference may escape or be retained.
    internal static GameEventScriptReadOnlyArray<T> FromOwnedArray(T[] items) => new(items);

    /// <summary>
    /// Takes an immutable snapshot, reusing storage only when the input is already a GameEventScriptReadOnlyArray of the same element type.
    /// </summary>
    /// <param name="items">The items value.</param>
    public GameEventScriptReadOnlyArray(IReadOnlyList<T>? items)
    {
        if (items is GameEventScriptReadOnlyArray<T> immutable)
        {
            _items = immutable._items;
            return;
        }

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

    /// <summary>
    /// Gets the count.
    /// </summary>
    public int Count => _items?.Length ?? 0;
    /// <summary>
    /// Gets the length.
    /// </summary>
    public int Length => Count;
    /// <summary>
    /// Gets the value at the specified index.
    /// </summary>
    /// <param name="index">The index value.</param>
    public T this[int index] => (_items ?? [])[index];
    /// <summary>
    /// Gets the enumerator.
    /// </summary>
    /// <returns>The result of the operation.</returns>
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

/// <summary>
/// Represents a game event script binary format.
/// </summary>
public static class GameEventScriptBinaryFormat
{
    /// <summary>
    /// Defines the version value.
    /// </summary>
    public const ushort Version = 1;
    /// <summary>
    /// Defines the header size value.
    /// </summary>
    public const uint HeaderSize = 16;
    /// <summary>
    /// Defines the section header size value.
    /// </summary>
    public const uint SectionHeaderSize = 12;
    /// <summary>
    /// Defines the magic g value.
    /// </summary>
    public const byte MagicG = (byte)'G';
    /// <summary>
    /// Defines the magic e value.
    /// </summary>
    public const byte MagicE = (byte)'E';
    /// <summary>
    /// Defines the magic s value.
    /// </summary>
    public const byte MagicS = (byte)'S';
    /// <summary>
    /// Defines the magic b value.
    /// </summary>
    public const byte MagicB = (byte)'B';
}

/// <summary>
/// Defines the supported game event script section flags values.
/// </summary>
[Flags]
public enum GameEventScriptSectionFlags : ushort
{
    /// <summary>
    /// Identifies the none value.
    /// </summary>
    None = 0,
    /// <summary>
    /// Identifies the required value.
    /// </summary>
    Required = 0x0001,
    /// <summary>
    /// Identifies the compression mask value.
    /// </summary>
    CompressionMask = 0x00F0
}

/// <summary>
/// Defines the supported game event script section type values.
/// </summary>
public enum GameEventScriptSectionType : ushort
{
    /// <summary>
    /// Identifies the program metadata value.
    /// </summary>
    ProgramMetadata = 0x0001,
    /// <summary>
    /// Identifies the string constants value.
    /// </summary>
    StringConstants = 0x0002,
    /// <summary>
    /// Identifies the u int16 index lists value.
    /// </summary>
    UInt16IndexLists = 0x0003,
    /// <summary>
    /// Identifies the bindings value.
    /// </summary>
    Bindings = 0x0004,
    /// <summary>
    /// Identifies the code value.
    /// </summary>
    Code = 0x0010,
    /// <summary>
    /// Identifies the debug symbols value.
    /// </summary>
    DebugSymbols = 0x0020,
    /// <summary>
    /// Identifies the source map value.
    /// </summary>
    SourceMap = 0x0021,
    /// <summary>
    /// Identifies the source archive value.
    /// </summary>
    SourceArchive = 0x0022,
    /// <summary>
    /// Identifies the build metadata value.
    /// </summary>
    BuildMetadata = 0x0030,
    /// <summary>
    /// Identifies the reserved signature value.
    /// </summary>
    ReservedSignature = 0x0040,
    /// <summary>
    /// Identifies the named custom value.
    /// </summary>
    NamedCustom = 0xFFFE
}

/// <summary>
/// Defines the supported game event script debug info options values.
/// </summary>
[Flags]
public enum GameEventScriptDebugInfoOptions : byte
{
    /// <summary>
    /// Identifies the none value.
    /// </summary>
    None = 0,
    /// <summary>
    /// Identifies the debug symbols value.
    /// </summary>
    DebugSymbols = 1 << 0,
    /// <summary>
    /// Identifies the source map value.
    /// </summary>
    SourceMap = 1 << 1,
    /// <summary>
    /// Identifies the source archive value.
    /// </summary>
    SourceArchive = 1 << 2,
    /// <summary>
    /// Identifies the all value.
    /// </summary>
    All = DebugSymbols | SourceMap | SourceArchive
}

/// <summary>
/// Represents a game event script string constant segment.
/// </summary>
public readonly struct GameEventScriptStringConstantSegment
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

    private readonly GameEventScriptReadOnlyArray<SliceEntry> _slices;
    private readonly GameEventScriptReadOnlyArray<byte> _data;

    /// <summary>
    /// Initializes a new instance of Game Event Script String Constant Segment.
    /// </summary>
    /// <param name="slices">The slices value.</param>
    /// <param name="data">The data value.</param>
    public GameEventScriptStringConstantSegment(IReadOnlyList<SliceEntry> slices, IReadOnlyList<byte> data)
    {
        _slices = new GameEventScriptReadOnlyArray<SliceEntry>(slices);
        _data = new GameEventScriptReadOnlyArray<byte>(data);
    }

    /// <summary>
    /// Gets the slices.
    /// </summary>
    public GameEventScriptReadOnlyArray<SliceEntry> Slices => _slices;
    /// <summary>
    /// Gets the data.
    /// </summary>
    public GameEventScriptReadOnlyArray<byte> Data => _data;

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
public readonly struct GameEventScriptUInt16IndexList
{
    private readonly GameEventScriptReadOnlyArray<ushort> _data;

    internal GameEventScriptUInt16IndexList(GameEventScriptReadOnlyArray<ushort> data, int start, int length)
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
public readonly struct GameEventScriptUInt16IndexListSegment
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

    private readonly GameEventScriptReadOnlyArray<SliceEntry> _slices;
    private readonly GameEventScriptReadOnlyArray<ushort> _data;

    /// <summary>
    /// Initializes a new instance of Game Event Script U Int16 Index List Segment.
    /// </summary>
    /// <param name="slices">The slices value.</param>
    /// <param name="data">The data value.</param>
    public GameEventScriptUInt16IndexListSegment(IReadOnlyList<SliceEntry> slices, IReadOnlyList<ushort> data)
    {
        _slices = new GameEventScriptReadOnlyArray<SliceEntry>(slices);
        _data = new GameEventScriptReadOnlyArray<ushort>(data);
    }

    /// <summary>
    /// Gets the slices.
    /// </summary>
    public GameEventScriptReadOnlyArray<SliceEntry> Slices => _slices;
    /// <summary>
    /// Gets the data.
    /// </summary>
    public GameEventScriptReadOnlyArray<ushort> Data => _data;

    /// <summary>
    /// Resolves the value.
    /// </summary>
    /// <param name="index">The index value.</param>
    /// <returns>The result of the operation.</returns>
    public GameEventScriptUInt16IndexList Resolve(ushort index) => new(_data, _slices[index].Start, _slices[index].Length);
}

/// <summary>
/// Represents a game event script binding segment.
/// </summary>
public readonly struct GameEventScriptBindingSegment
{

    /// <summary>
    /// Represents a game event script binary bind entry.
    /// </summary>
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

        /// <summary>
        /// Gets the id.
        /// </summary>
        public ushort Id { get; } = id;

        /// <summary>
        /// Gets the kind.
        /// </summary>
        public GameEventScriptBinaryBindKind Kind { get; } = kind;

        /// <summary>
        /// Gets the name.
        /// </summary>
        public ushort Name { get; } = name;

        /// <summary>
        /// Gets the argument names.
        /// </summary>
        public GameEventScriptReadOnlyArray<ushort> ArgumentNames => _argumentNames;

        /// <summary>
        /// Gets the required tags.
        /// </summary>
        public GameEventScriptReadOnlyArray<ushort> RequiredTags => _requiredTags;

        /// <summary>
        /// Gets the excluded tags.
        /// </summary>
        public GameEventScriptReadOnlyArray<ushort> ExcludedTags => _excludedTags;

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

    private readonly GameEventScriptReadOnlyArray<GameEventScriptBinaryBindEntry> _entries;

    /// <summary>
    /// Initializes a new instance of Game Event Script Binding Segment.
    /// </summary>
    /// <param name="entries">The entries value.</param>
    public GameEventScriptBindingSegment(IReadOnlyList<GameEventScriptBinaryBindEntry>? entries)
    {
        _entries = new GameEventScriptReadOnlyArray<GameEventScriptBinaryBindEntry>(entries);
        EntryCount = ToEntryCount(_entries.Count);
    }

    /// <summary>
    /// Gets the entry count.
    /// </summary>
    public ushort EntryCount { get; }

    /// <summary>
    /// Gets the entries.
    /// </summary>
    public GameEventScriptReadOnlyArray<GameEventScriptBinaryBindEntry> Entries => _entries;

    private static ushort ToEntryCount(int count) => count > ushort.MaxValue ? throw new ArgumentOutOfRangeException(nameof(count), "GameEventScriptProgram tables cannot exceed 65535 entries.") : checked((ushort)count);

}

/// <summary>
/// Represents a game event script code segment.
/// </summary>
public readonly struct GameEventScriptCodeSegment
{
    private readonly GameEventScriptReadOnlyArray<GameEventScriptBytecodeInstruction> _instructions;

    /// <summary>
    /// Initializes a new instance of Game Event Script Code Segment.
    /// </summary>
    /// <param name="instructions">The instructions value.</param>
    public GameEventScriptCodeSegment(IReadOnlyList<GameEventScriptBytecodeInstruction>? instructions)
        => _instructions = new GameEventScriptReadOnlyArray<GameEventScriptBytecodeInstruction>(instructions);

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
    public GameEventScriptBytecodeInstruction this[int index] => _instructions[index];
    /// <summary>
    /// Gets the instructions.
    /// </summary>
    public GameEventScriptReadOnlyArray<GameEventScriptBytecodeInstruction> Instructions => _instructions;
}

/// <summary>
/// Defines the supported game event script debug symbol kind values.
/// </summary>
public enum GameEventScriptDebugSymbolKind : byte
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
public readonly record struct GameEventScriptDebugSymbol(GameEventScriptDebugSymbolKind Kind, ushort RegisterId, string Name, uint CodeStart, uint CodeLength);

/// <summary>
/// Represents a game event script debug symbols segment.
/// </summary>
public sealed class GameEventScriptDebugSymbolsSegment
{
    /// <summary>
    /// Initializes a new instance of Game Event Script Debug Symbols Segment.
    /// </summary>
    /// <param name="symbols">The symbols value.</param>
    public GameEventScriptDebugSymbolsSegment(IReadOnlyList<GameEventScriptDebugSymbol>? symbols)
        => Symbols = new GameEventScriptReadOnlyArray<GameEventScriptDebugSymbol>(symbols);

    /// <summary>
    /// Gets the symbols.
    /// </summary>
    public GameEventScriptReadOnlyArray<GameEventScriptDebugSymbol> Symbols { get; }
}

/// <summary>
/// Represents a game event script source map source.
/// </summary>
public sealed class GameEventScriptSourceMapSource
{
    /// <summary>
    /// Initializes a new instance of Game Event Script Source Map Source.
    /// </summary>
    /// <param name="sourceId">The source id value.</param>
    /// <param name="sourceName">The source name value.</param>
    /// <param name="sourceByteLength">The source byte length value.</param>
    /// <param name="sha256">The sha256 value.</param>
    /// <param name="lineStartByteOffsets">The line start byte offsets value.</param>
    public GameEventScriptSourceMapSource(uint sourceId, string sourceName, uint sourceByteLength, IReadOnlyList<byte> sha256, IReadOnlyList<uint> lineStartByteOffsets)
    {
        SourceId = sourceId;
        SourceName = sourceName ?? string.Empty;
        SourceByteLength = sourceByteLength;
        Sha256 = new GameEventScriptReadOnlyArray<byte>(sha256);
        LineStartByteOffsets = new GameEventScriptReadOnlyArray<uint>(lineStartByteOffsets);
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
    public GameEventScriptReadOnlyArray<byte> Sha256 { get; }
    /// <summary>
    /// Gets the line start byte offsets.
    /// </summary>
    public GameEventScriptReadOnlyArray<uint> LineStartByteOffsets { get; }
}

/// <summary>
/// Represents a game event script source map entry.
/// </summary>
/// <param name="CodeStart">The code start value.</param>
/// <param name="CodeLength">The code length value.</param>
/// <param name="SourceId">The source id value.</param>
/// <param name="SourceStartByteOffset">The source start byte offset value.</param>
/// <param name="SourceByteLength">The source byte length value.</param>
public readonly record struct GameEventScriptSourceMapEntry(uint CodeStart, uint CodeLength, uint SourceId, uint SourceStartByteOffset, uint SourceByteLength);

/// <summary>
/// Represents a game event script source map segment.
/// </summary>
public sealed class GameEventScriptSourceMapSegment
{
    /// <summary>
    /// Initializes a new instance of Game Event Script Source Map Segment.
    /// </summary>
    /// <param name="sources">The sources value.</param>
    /// <param name="entries">The entries value.</param>
    public GameEventScriptSourceMapSegment(IReadOnlyList<GameEventScriptSourceMapSource>? sources, IReadOnlyList<GameEventScriptSourceMapEntry>? entries)
    {
        Sources = new GameEventScriptReadOnlyArray<GameEventScriptSourceMapSource>(sources);
        Entries = new GameEventScriptReadOnlyArray<GameEventScriptSourceMapEntry>(entries);
    }

    /// <summary>
    /// Gets the sources.
    /// </summary>
    public GameEventScriptReadOnlyArray<GameEventScriptSourceMapSource> Sources { get; }
    /// <summary>
    /// Gets the entries.
    /// </summary>
    public GameEventScriptReadOnlyArray<GameEventScriptSourceMapEntry> Entries { get; }
}

/// <summary>
/// Represents a game event script source archive entry.
/// </summary>
public sealed class GameEventScriptSourceArchiveEntry
{
    private readonly GameEventScriptReadOnlyArray<byte> _utf8Content;

    /// <summary>
    /// Initializes a new instance of Game Event Script Source Archive Entry.
    /// </summary>
    /// <param name="sourceId">The source id value.</param>
    /// <param name="sourceName">The source name value.</param>
    /// <param name="utf8Content">The utf8 content value.</param>
    public GameEventScriptSourceArchiveEntry(uint sourceId, string sourceName, IReadOnlyList<byte> utf8Content)
    {
        SourceId = sourceId;
        SourceName = sourceName ?? string.Empty;
        _utf8Content = new GameEventScriptReadOnlyArray<byte>(utf8Content);
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
    public GameEventScriptReadOnlyArray<byte> Utf8Content => _utf8Content;
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
public sealed class GameEventScriptSourceArchiveSegment
{
    /// <summary>
    /// Initializes a new instance of Game Event Script Source Archive Segment.
    /// </summary>
    /// <param name="sources">The sources value.</param>
    public GameEventScriptSourceArchiveSegment(IReadOnlyList<GameEventScriptSourceArchiveEntry>? sources)
        => Sources = new GameEventScriptReadOnlyArray<GameEventScriptSourceArchiveEntry>(sources);

    /// <summary>
    /// Gets the sources.
    /// </summary>
    public GameEventScriptReadOnlyArray<GameEventScriptSourceArchiveEntry> Sources { get; }
}

/// <summary>
/// Represents a game event script build metadata segment.
/// </summary>
public sealed class GameEventScriptBuildMetadataSegment
{
    /// <summary>
    /// Initializes a new instance of Game Event Script Build Metadata Segment.
    /// </summary>
    /// <param name="compilerId">The compiler id value.</param>
    /// <param name="compilerVersion">The compiler version value.</param>
    public GameEventScriptBuildMetadataSegment(string compilerId, string compilerVersion)
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
public sealed class GameEventScriptOpaqueSection
{
    private readonly GameEventScriptReadOnlyArray<byte> _rawPayload;

    /// <summary>
    /// Initializes a new instance of Game Event Script Opaque Section.
    /// </summary>
    /// <param name="sectionType">The section type value.</param>
    /// <param name="flags">The flags value.</param>
    /// <param name="sectionVersion">The section version value.</param>
    /// <param name="rawPayload">The raw payload value.</param>
    /// <param name="originalOrdinal">The original ordinal value.</param>
    public GameEventScriptOpaqueSection(ushort sectionType, GameEventScriptSectionFlags flags, ushort sectionVersion, IReadOnlyList<byte> rawPayload, int originalOrdinal)
    {
        SectionType = sectionType;
        Flags = flags;
        SectionVersion = sectionVersion;
        _rawPayload = new GameEventScriptReadOnlyArray<byte>(rawPayload);
        OriginalOrdinal = originalOrdinal;
    }

    /// <summary>
    /// Gets the section type.
    /// </summary>
    public ushort SectionType { get; }
    /// <summary>
    /// Gets the flags.
    /// </summary>
    public GameEventScriptSectionFlags Flags { get; }
    /// <summary>
    /// Gets the section version.
    /// </summary>
    public ushort SectionVersion { get; }
    /// <summary>
    /// Gets the raw payload.
    /// </summary>
    public GameEventScriptReadOnlyArray<byte> RawPayload => _rawPayload;
    /// <summary>
    /// Gets the original ordinal.
    /// </summary>
    public int OriginalOrdinal { get; }
}

/// <summary>
/// Defines the supported game event script binary bind kind values.
/// </summary>
public enum GameEventScriptBinaryBindKind : byte
{
    /// <summary>
    /// Identifies the message handler value.
    /// </summary>
    MessageHandler = 0x10,
    /// <summary>
    /// Identifies the message name handler value.
    /// </summary>
    MessageNameHandler = 0x11,
    /// <summary>
    /// Identifies the function value.
    /// </summary>
    Function = 0x12,
    /// <summary>
    /// Identifies the predicate value.
    /// </summary>
    Predicate = 0x13,
    /// <summary>
    /// Identifies the extension call value.
    /// </summary>
    ExtensionCall = 0x20,
    /// <summary>
    /// Identifies the outbound message value.
    /// </summary>
    OutboundMessage = 0x21,
    /// <summary>
    /// Identifies the record value.
    /// </summary>
    Record = 0x30,
    /// <summary>
    /// Identifies the external type value.
    /// </summary>
    ExternalType = 0x31
}

/// <summary>
/// Defines the supported game event script bytecode instruction unit values.
/// </summary>
public enum GameEventScriptBytecodeInstructionUnit : byte
{
    /// <summary>
    /// Identifies the unit none value.
    /// </summary>
    UnitNone = 0,
    /// <summary>
    /// Identifies the unit degree value.
    /// </summary>
    UnitDegree = 1,
    /// <summary>
    /// Identifies the unit meter value.
    /// </summary>
    UnitMeter = 2,
    /// <summary>
    /// Identifies the unit second value.
    /// </summary>
    UnitSecond = 3,

    /// <summary>
    /// Identifies the unit invalid value.
    /// </summary>
    UnitInvalid = 255 // Sentinel for invalid or unsupported unit input.
}

/// <summary>
/// Defines the supported game event script instruction flag values.
/// </summary>
[Flags]
public enum GameEventScriptInstructionFlag : byte
{
    /// <summary>
    /// Identifies the none value.
    /// </summary>
    None = 0,
    /// <summary>
    /// Identifies the normalize result as predicate value.
    /// </summary>
    NormalizeResultAsPredicate = 0x20
}

/// <summary>
/// Defines the supported game event script bytecode type kind values.
/// </summary>
public enum GameEventScriptBytecodeTypeKind : byte
{
    /// <summary>
    /// Identifies the nothing value.
    /// </summary>
    Nothing = 0x00,
    /// <summary>
    /// Identifies the boolean value.
    /// </summary>
    Boolean = 0x02,
    /// <summary>
    /// Identifies the integer value.
    /// </summary>
    Integer = 0x03,
    /// <summary>
    /// Identifies the float value.
    /// </summary>
    Float = 0x04,
    /// <summary>
    /// Identifies the percentage value.
    /// </summary>
    Percentage = 0x05,
    /// <summary>
    /// Identifies the tag value.
    /// </summary>
    Tag = 0x06,
    /// <summary>
    /// Identifies the text value.
    /// </summary>
    Text = 0x07,
    /// <summary>
    /// Identifies the vector value.
    /// </summary>
    Vector = 0x08,
    /// <summary>
    /// Identifies the point value.
    /// </summary>
    Point = 0x09,
    /// <summary>
    /// Identifies the range value.
    /// </summary>
    Range = 0x0a,

    /// <summary>
    /// Identifies the handler value.
    /// </summary>
    Handler = 0x10,
    /// <summary>
    /// Identifies the message value.
    /// </summary>
    Message = 0x11,

    /// <summary>
    /// Identifies the list value.
    /// </summary>
    List = 0x20,
    /// <summary>
    /// Identifies the dice value.
    /// </summary>
    Dice = 0x21,
    /// <summary>
    /// Identifies the map value.
    /// </summary>
    Map = 0x22,
    /// <summary>
    /// Identifies the list builder value.
    /// </summary>
    ListBuilder = 0x23,
    /// <summary>
    /// Identifies the map builder value.
    /// </summary>
    MapBuilder = 0x24,
    /// <summary>
    /// Identifies the distinct builder value.
    /// </summary>
    DistinctBuilder = 0x25,
    /// <summary>
    /// Identifies the group builder value.
    /// </summary>
    GroupBuilder = 0x26,
    /// <summary>
    /// Identifies the order builder value.
    /// </summary>
    OrderBuilder = 0x27,

    /// <summary>
    /// Identifies the iterator value.
    /// </summary>
    Iterator = 0x30,
    /// <summary>
    /// Identifies the series value.
    /// </summary>
    Series = 0x31,

    /// <summary>
    /// Identifies the custom value.
    /// </summary>
    Custom = 0xFF,
}

/// <summary>
/// Defines the supported game event script bytecode pattern kind values.
/// </summary>
public enum GameEventScriptBytecodePatternKind : ushort
{
    /// <summary>
    /// Identifies the count any value.
    /// </summary>
    CountAny = 0x00,
    /// <summary>
    /// Identifies the count face value.
    /// </summary>
    CountFace = 0x01,
    /// <summary>
    /// Identifies the full house value.
    /// </summary>
    FullHouse = 0x02,
    /// <summary>
    /// Identifies the straight value.
    /// </summary>
    Straight = 0x03,
}

/// <summary>
/// Defines the supported game event script bytecode series kind values.
/// </summary>
public enum GameEventScriptBytecodeSeriesKind : ushort
{
    /// <summary>
    /// Identifies the fibonacci value.
    /// </summary>
    Fibonacci = 0x01,
    /// <summary>
    /// Identifies the factorial value.
    /// </summary>
    Factorial = 0x02,
}

/// <summary>
/// Represents a game event script bytecode instruction.
/// </summary>
[StructLayout(LayoutKind.Sequential, Size = 16)]
public struct GameEventScriptBytecodeInstruction
{
    private const byte UnitMask = 0x1F;
    private const byte InstructionFlagMask = 0xE0;

    /// <summary>
    /// Defines the op code value.
    /// </summary>
    public GameEventScriptBytecodeOpCode OpCode;
    /// <summary>
    /// Defines the unit and flags value.
    /// </summary>
    public byte UnitAndFlags;
    private ushort _word0;
    private ushort _word1;
    private ushort _word2;
    /// <summary>
    /// Defines the payload value.
    /// </summary>
    public ulong Payload;

    /// <summary>
    /// Gets the destination register.
    /// </summary>
    public ushort DestinationRegister { readonly get => _word0; set => _word0 = value; }
    /// <summary>
    /// Gets the message destination.
    /// </summary>
    public ushort MessageDestination { readonly get => _word0; set => _word0 = value; }

    /// <summary>
    /// Gets the immediate x.
    /// </summary>
    public short ImmediateX { readonly get => unchecked((short)_word1); set => _word1 = unchecked((ushort)value); }
    /// <summary>
    /// Gets the condition register.
    /// </summary>
    public ushort ConditionRegister { readonly get => _word1; set => _word1 = value; }
    /// <summary>
    /// Gets the x register.
    /// </summary>
    public ushort XRegister { readonly get => _word1; set => _word1 = value; }
    /// <summary>
    /// Gets the string index.
    /// </summary>
    public ushort StringIndex { readonly get => _word1; set => _word1 = value; }
    /// <summary>
    /// Gets the secondary list index.
    /// </summary>
    public ushort SecondaryListIndex { readonly get => _word1; set => _word1 = value; }
    /// <summary>
    /// Gets the bind id.
    /// </summary>
    public ushort BindId { readonly get => _word1; set => _word1 = value; }
    /// <summary>
    /// Gets the index.
    /// </summary>
    public ushort Index { readonly get => _word1; set => _word1 = value; }
    /// <summary>
    /// Gets the count.
    /// </summary>
    public short Count { readonly get => unchecked((short)_word1); set => _word1 = unchecked((ushort)value); }

    /// <summary>
    /// Gets the immediate y.
    /// </summary>
    public short ImmediateY { readonly get => unchecked((short)_word2); set => _word2 = unchecked((ushort)value); }
    /// <summary>
    /// Gets the target address.
    /// </summary>
    public ushort TargetAddress { readonly get => _word2; set => _word2 = value; }
    /// <summary>
    /// Gets the y register.
    /// </summary>
    public ushort YRegister { readonly get => _word2; set => _word2 = value; }
    /// <summary>
    /// Gets the entry address.
    /// </summary>
    public ushort EntryAddress { readonly get => _word2; set => _word2 = value; }
    /// <summary>
    /// Gets the secondary string index.
    /// </summary>
    public ushort SecondaryStringIndex { readonly get => _word2; set => _word2 = value; }
    /// <summary>
    /// Gets the list index.
    /// </summary>
    public ushort ListIndex { readonly get => _word2; set => _word2 = value; }
    /// <summary>
    /// Gets the type operand.
    /// </summary>
    public ushort TypeOperand { readonly get => _word2; set => _word2 = value; }
    /// <summary>
    /// Gets the type kind.
    /// </summary>
    public GameEventScriptBytecodeTypeKind TypeKind { readonly get => (GameEventScriptBytecodeTypeKind)_word2; set => _word2 = (ushort)value; }

    /// <summary>
    /// Gets the au.
    /// </summary>
    public ushort AU { readonly get => (ushort)Payload; set => Payload = (Payload & 0xFFFFFFFFFFFF0000UL) | value; }
    /// <summary>
    /// Gets the as.
    /// </summary>
    public short AS { readonly get => unchecked((short)AU); set => AU = unchecked((ushort)value); }
    /// <summary>
    /// Gets the bu.
    /// </summary>
    public ushort BU { readonly get => (ushort)(Payload >> 16); set => Payload = (Payload & 0xFFFFFFFF0000FFFFUL) | ((ulong)value << 16); }
    /// <summary>
    /// Gets the bs.
    /// </summary>
    public short BS { readonly get => unchecked((short)BU); set => BU = unchecked((ushort)value); }
    /// <summary>
    /// Gets the cu.
    /// </summary>
    public ushort CU { readonly get => (ushort)(Payload >> 32); set => Payload = (Payload & 0xFFFF0000FFFFFFFFUL) | ((ulong)value << 32); }
    /// <summary>
    /// Gets the cs.
    /// </summary>
    public short CS { readonly get => unchecked((short)CU); set => CU = unchecked((ushort)value); }
    /// <summary>
    /// Gets the du.
    /// </summary>
    public ushort DU { readonly get => (ushort)(Payload >> 48); set => Payload = (Payload & 0x0000FFFFFFFFFFFFUL) | ((ulong)value << 48); }
    /// <summary>
    /// Gets the ds.
    /// </summary>
    public short DS { readonly get => unchecked((short)DU); set => DU = unchecked((ushort)value); }

    /// <summary>
    /// Gets the i64.
    /// </summary>
    public long I64 { readonly get => unchecked((long)Payload); set => Payload = unchecked((ulong)value); }
    /// <summary>
    /// Gets the f64.
    /// </summary>
    public double F64
    {
        readonly get => BitConverter.Int64BitsToDouble(unchecked((long)Payload));
        set => Payload = unchecked((ulong)BitConverter.DoubleToInt64Bits(value));
    }

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

/// <summary>
/// Defines the supported game event script bytecode op code values.
/// </summary>
public enum GameEventScriptBytecodeOpCode : byte
{
    #region Group 1 - control, calls, messages, types, values

    /// <summary>
    /// Identifies the nop value.
    /// </summary>
    Nop = 0x00,
    /// <summary>
    /// Identifies the register locals value.
    /// </summary>
    RegisterLocals = 0x01,
    /// <summary>
    /// Identifies the jump value.
    /// </summary>
    Jump = 0x02,
    /// <summary>
    /// Identifies the jump if true value.
    /// </summary>
    JumpIfTrue = 0x03,
    /// <summary>
    /// Identifies the jump if false value.
    /// </summary>
    JumpIfFalse = 0x04,
    /// <summary>
    /// Identifies the jump if not true value.
    /// </summary>
    JumpIfNotTrue = 0x05,
    /// <summary>
    /// Identifies the jump if nothing value.
    /// </summary>
    JumpIfNothing = 0x06,

    /// <summary>
    /// Identifies the call value.
    /// </summary>
    Call = 0x07,
    /// <summary>
    /// Identifies the create series value.
    /// </summary>
    CreateSeries = 0x08,
    /// <summary>
    /// Identifies the call external value.
    /// </summary>
    CallExternal = 0x09,

    /// <summary>
    /// Identifies the return void value.
    /// </summary>
    ReturnVoid = 0x0A,
    /// <summary>
    /// Identifies the return value value.
    /// </summary>
    ReturnValue = 0x0B,

    /// <summary>
    /// Identifies the emit message value.
    /// </summary>
    EmitMessage = 0x0C,
    /// <summary>
    /// Identifies the emit message with tags value.
    /// </summary>
    EmitMessageWithTags = 0x0D,
    /// <summary>
    /// Identifies the emit message value value.
    /// </summary>
    EmitMessageValue = 0x0E,
    /// <summary>
    /// Identifies the emit message value with tags value.
    /// </summary>
    EmitMessageValueWithTags = 0x0F,

    /// <summary>
    /// Identifies the publish message value.
    /// </summary>
    PublishMessage = 0x10,
    /// <summary>
    /// Identifies the publish message with tags value.
    /// </summary>
    PublishMessageWithTags = 0x11,
    /// <summary>
    /// Identifies the publish message value value.
    /// </summary>
    PublishMessageValue = 0x12,
    /// <summary>
    /// Identifies the publish message value with tags value.
    /// </summary>
    PublishMessageValueWithTags = 0x13,

    /// <summary>
    /// Identifies the cast value.
    /// </summary>
    Cast = 0x14,
    /// <summary>
    /// Identifies the cast custom value.
    /// </summary>
    CastCustom = 0x15,
    /// <summary>
    /// Identifies the cast unit value.
    /// </summary>
    CastUnit = 0x16,
    /// <summary>
    /// Identifies the cast numeric value.
    /// </summary>
    CastNumeric = 0x17,

    /// <summary>
    /// Identifies the check type value.
    /// </summary>
    CheckType = 0x18,
    /// <summary>
    /// Identifies the check custom type value.
    /// </summary>
    CheckCustomType = 0x19,
    /// <summary>
    /// Identifies the check unit value.
    /// </summary>
    CheckUnit = 0x1A,
    /// <summary>
    /// Identifies the check numeric value.
    /// </summary>
    CheckNumeric = 0x1B,
    /// <summary>
    /// Identifies the check integer value.
    /// </summary>
    CheckInteger = 0x1C,
    /// <summary>
    /// Identifies the check fractional value.
    /// </summary>
    CheckFractional = 0x1D,

    /// <summary>
    /// Identifies the move value.
    /// </summary>
    Move = 0x1E,
    /// <summary>
    /// Identifies the member access value.
    /// </summary>
    MemberAccess = 0x1F,
    /// <summary>
    /// Identifies the index access value.
    /// </summary>
    IndexAccess = 0x20,
    /// <summary>
    /// Identifies the property access value.
    /// </summary>
    PropertyAccess = 0x21,
    /// <summary>
    /// Identifies the bind handler value.
    /// </summary>
    BindHandler = 0x22,

    /// <summary>
    /// Identifies the load nothing value.
    /// </summary>
    LoadNothing = 0x23,
    /// <summary>
    /// Identifies the load true value.
    /// </summary>
    LoadTrue = 0x24,
    /// <summary>
    /// Identifies the load false value.
    /// </summary>
    LoadFalse = 0x25,
    /// <summary>
    /// Identifies the load integer value.
    /// </summary>
    LoadInteger = 0x26,
    /// <summary>
    /// Identifies the load float value.
    /// </summary>
    LoadFloat = 0x27,
    /// <summary>
    /// Identifies the load percentage value.
    /// </summary>
    LoadPercentage = 0x28,
    /// <summary>
    /// Identifies the load text value.
    /// </summary>
    LoadText = 0x29,
    /// <summary>
    /// Identifies the load tag value.
    /// </summary>
    LoadTag = 0x2A,
    /// <summary>
    /// Identifies the load handler value.
    /// </summary>
    LoadHandler = 0x2B,
    /// <summary>
    /// Identifies the load message value.
    /// </summary>
    LoadMessage = 0x2C,

    /// <summary>
    /// Identifies the stage register value.
    /// </summary>
    StageRegister = 0x2D,
    /// <summary>
    /// Identifies the stage nothing value.
    /// </summary>
    StageNothing = 0x2E,
    /// <summary>
    /// Identifies the stage true value.
    /// </summary>
    StageTrue = 0x2F,
    /// <summary>
    /// Identifies the stage false value.
    /// </summary>
    StageFalse = 0x30,
    /// <summary>
    /// Identifies the stage integer value.
    /// </summary>
    StageInteger = 0x31,
    /// <summary>
    /// Identifies the stage float value.
    /// </summary>
    StageFloat = 0x32,
    /// <summary>
    /// Identifies the stage text value.
    /// </summary>
    StageText = 0x33,
    /// <summary>
    /// Identifies the stage tag value.
    /// </summary>
    StageTag = 0x34,
    /// <summary>
    /// Identifies the stage percentage value.
    /// </summary>
    StagePercentage = 0x35,

    /// <summary>
    /// Identifies the create dice value.
    /// </summary>
    CreateDice = 0x36,
    /// <summary>
    /// Identifies the create vector value.
    /// </summary>
    CreateVector = 0x37,
    /// <summary>
    /// Identifies the create point value.
    /// </summary>
    CreatePoint = 0x38,
    /// <summary>
    /// Identifies the create list value.
    /// </summary>
    CreateList = 0x39,
    /// <summary>
    /// Identifies the create map value.
    /// </summary>
    CreateMap = 0x3A,
    /// <summary>
    /// Identifies the create range value.
    /// </summary>
    CreateRange = 0x3B,
    /// <summary>
    /// Identifies the create range with step value.
    /// </summary>
    CreateRangeWithStep = 0x3C,
    /// <summary>
    /// Identifies the create range iterator value.
    /// </summary>
    CreateRangeIterator = 0x3D,
    /// <summary>
    /// Identifies the create range iterator with step value.
    /// </summary>
    CreateRangeIteratorWithStep = 0x3E,
    /// <summary>
    /// Identifies the create range iterator short value.
    /// </summary>
    CreateRangeIteratorShort = 0x3F,
    /// <summary>
    /// Identifies the create record value.
    /// </summary>
    CreateRecord = 0x40,
    /// <summary>
    /// Identifies the create record value value.
    /// </summary>
    CreateRecordValue = 0x41,
    /// <summary>
    /// Identifies the create external type value.
    /// </summary>
    CreateExternalType = 0x42,

    /// <summary>
    /// Identifies the has value value.
    /// </summary>
    HasValue = 0x43,
    /// <summary>
    /// Identifies the is empty value.
    /// </summary>
    IsEmpty = 0x44,
    /// <summary>
    /// Identifies the default value.
    /// </summary>
    Default = 0x45,

    #endregion

    #region Group 2 - boolean algebra, comparison, math and random

    /// <summary>
    /// Identifies the or value.
    /// </summary>
    Or = 0x50,
    /// <summary>
    /// Identifies the and value.
    /// </summary>
    And = 0x51,
    /// <summary>
    /// Identifies the xor value.
    /// </summary>
    Xor = 0x52,
    /// <summary>
    /// Identifies the implies value.
    /// </summary>
    Implies = 0x53,
    /// <summary>
    /// Identifies the not value.
    /// </summary>
    Not = 0x54,
    /// <summary>
    /// Identifies the equal value.
    /// </summary>
    Equal = 0x55,
    /// <summary>
    /// Identifies the not equal value.
    /// </summary>
    NotEqual = 0x56,
    /// <summary>
    /// Identifies the less value.
    /// </summary>
    Less = 0x57,
    /// <summary>
    /// Identifies the greater value.
    /// </summary>
    Greater = 0x58,
    /// <summary>
    /// Identifies the less or equal value.
    /// </summary>
    LessOrEqual = 0x59,
    /// <summary>
    /// Identifies the greater or equal value.
    /// </summary>
    GreaterOrEqual = 0x5A,
    /// <summary>
    /// Identifies the add value.
    /// </summary>
    Add = 0x5B,
    /// <summary>
    /// Identifies the subtract value.
    /// </summary>
    Subtract = 0x5C,
    /// <summary>
    /// Identifies the multiply value.
    /// </summary>
    Multiply = 0x5D,
    /// <summary>
    /// Identifies the divide value.
    /// </summary>
    Divide = 0x5E,
    /// <summary>
    /// Identifies the power value.
    /// </summary>
    Power = 0x5F,
    /// <summary>
    /// Identifies the integer divide value.
    /// </summary>
    IntegerDivide = 0x60,
    /// <summary>
    /// Identifies the modulo value.
    /// </summary>
    Modulo = 0x61,
    /// <summary>
    /// Identifies the remainder value.
    /// </summary>
    Remainder = 0x62,
    /// <summary>
    /// Identifies the min value.
    /// </summary>
    Min = 0x63,
    /// <summary>
    /// Identifies the max value.
    /// </summary>
    Max = 0x64,
    /// <summary>
    /// Identifies the negate value.
    /// </summary>
    Negate = 0x65,
    /// <summary>
    /// Identifies the abs value.
    /// </summary>
    Abs = 0x66,
    /// <summary>
    /// Identifies the log n value.
    /// </summary>
    LogN = 0x67,
    /// <summary>
    /// Identifies the chance value.
    /// </summary>
    Chance = 0x68,
    /// <summary>
    /// Identifies the clamp value.
    /// </summary>
    Clamp = 0x69,
    /// <summary>
    /// Identifies the random take value.
    /// </summary>
    RandomTake = 0x6A,
    /// <summary>
    /// Identifies the random take float value.
    /// </summary>
    RandomTakeFloat = 0x6B,
    /// <summary>
    /// Identifies the random push value.
    /// </summary>
    RandomPush = 0x6C,
    /// <summary>
    /// Identifies the random push constant value.
    /// </summary>
    RandomPushConstant = 0x6D,
    /// <summary>
    /// Identifies the random pop value.
    /// </summary>
    RandomPop = 0x6E,
    /// <summary>
    /// Identifies the term value.
    /// </summary>
    Term = 0x6F,
    /// <summary>
    /// Identifies the exp value.
    /// </summary>
    Exp = 0x70,
    /// <summary>
    /// Identifies the floor value.
    /// </summary>
    Floor = 0x71,
    /// <summary>
    /// Identifies the ceil value.
    /// </summary>
    Ceil = 0x72,
    /// <summary>
    /// Identifies the truncate value.
    /// </summary>
    Truncate = 0x73,
    /// <summary>
    /// Identifies the round half even value.
    /// </summary>
    RoundHalfEven = 0x74,
    /// <summary>
    /// Identifies the round half up value.
    /// </summary>
    RoundHalfUp = 0x75,
    /// <summary>
    /// Identifies the round half down value.
    /// </summary>
    RoundHalfDown = 0x76,
    /// <summary>
    /// Identifies the degree to radians value.
    /// </summary>
    DegreeToRadians = 0x77,
    /// <summary>
    /// Identifies the degree from radians value.
    /// </summary>
    DegreeFromRadians = 0x78,
    /// <summary>
    /// Identifies the wrap degree value.
    /// </summary>
    WrapDegree = 0x79,
    /// <summary>
    /// Identifies the sin value.
    /// </summary>
    Sin = 0x7A,
    /// <summary>
    /// Identifies the cos value.
    /// </summary>
    Cos = 0x7B,
    /// <summary>
    /// Identifies the tan value.
    /// </summary>
    Tan = 0x7C,
    /// <summary>
    /// Identifies the asin value.
    /// </summary>
    Asin = 0x7D,
    /// <summary>
    /// Identifies the acos value.
    /// </summary>
    Acos = 0x7E,
    /// <summary>
    /// Identifies the atan value.
    /// </summary>
    Atan = 0x7F,
    /// <summary>
    /// Identifies the atan2 value.
    /// </summary>
    Atan2 = 0x80,
    /// <summary>
    /// Identifies the hypot2 d value.
    /// </summary>
    Hypot2D = 0x81,
    /// <summary>
    /// Identifies the hypot3 d value.
    /// </summary>
    Hypot3D = 0x82,
    /// <summary>
    /// Identifies the distance value.
    /// </summary>
    Distance = 0x83,
    /// <summary>
    /// Identifies the distance2 d value.
    /// </summary>
    Distance2D = 0x84,
    /// <summary>
    /// Identifies the distance3 d value.
    /// </summary>
    Distance3D = 0x85,
    /// <summary>
    /// Identifies the distance squared value.
    /// </summary>
    DistanceSquared = 0x86,
    /// <summary>
    /// Identifies the distance squared2 d value.
    /// </summary>
    DistanceSquared2D = 0x87,
    /// <summary>
    /// Identifies the distance squared3 d value.
    /// </summary>
    DistanceSquared3D = 0x88,
    /// <summary>
    /// Identifies the length squared value.
    /// </summary>
    LengthSquared = 0x89,
    /// <summary>
    /// Identifies the length squared2 d value.
    /// </summary>
    LengthSquared2D = 0x8A,
    /// <summary>
    /// Identifies the length squared3 d value.
    /// </summary>
    LengthSquared3D = 0x8B,
    /// <summary>
    /// Identifies the normalize value.
    /// </summary>
    Normalize = 0x8C,
    /// <summary>
    /// Identifies the normalize2 d value.
    /// </summary>
    Normalize2D = 0x8D,
    /// <summary>
    /// Identifies the normalize3 d value.
    /// </summary>
    Normalize3D = 0x8E,
    /// <summary>
    /// Identifies the dot value.
    /// </summary>
    Dot = 0x8F,
    /// <summary>
    /// Identifies the dot2 d value.
    /// </summary>
    Dot2D = 0x90,
    /// <summary>
    /// Identifies the dot3 d value.
    /// </summary>
    Dot3D = 0x91,
    /// <summary>
    /// Identifies the cross value.
    /// </summary>
    Cross = 0x92,
    /// <summary>
    /// Identifies the cross2 d value.
    /// </summary>
    Cross2D = 0x93,
    /// <summary>
    /// Identifies the cross3 d value.
    /// </summary>
    Cross3D = 0x94,
    /// <summary>
    /// Identifies the angle between value.
    /// </summary>
    AngleBetween = 0x95,
    /// <summary>
    /// Identifies the angle between2 d value.
    /// </summary>
    AngleBetween2D = 0x96,
    /// <summary>
    /// Identifies the angle between3 d value.
    /// </summary>
    AngleBetween3D = 0x97,

    #endregion

    #region Group 3 - text, collection, iterators

    /// <summary>
    /// Identifies the take first value.
    /// </summary>
    TakeFirst = 0xA0,
    /// <summary>
    /// Identifies the drop first value.
    /// </summary>
    DropFirst = 0xA1,
    /// <summary>
    /// Identifies the take last value.
    /// </summary>
    TakeLast = 0xA2,
    /// <summary>
    /// Identifies the drop last value.
    /// </summary>
    DropLast = 0xA3,
    /// <summary>
    /// Identifies the take highest value.
    /// </summary>
    TakeHighest = 0xA4,
    /// <summary>
    /// Identifies the take lowest value.
    /// </summary>
    TakeLowest = 0xA5,
    /// <summary>
    /// Identifies the drop highest value.
    /// </summary>
    DropHighest = 0xA6,
    /// <summary>
    /// Identifies the drop lowest value.
    /// </summary>
    DropLowest = 0xA7,
    /// <summary>
    /// Identifies the one random value.
    /// </summary>
    OneRandom = 0xA8,
    /// <summary>
    /// Identifies the take random value.
    /// </summary>
    TakeRandom = 0xA9,
    /// <summary>
    /// Identifies the one weighted value.
    /// </summary>
    OneWeighted = 0xAA,
    /// <summary>
    /// Identifies the take weighted value.
    /// </summary>
    TakeWeighted = 0xAB,
    /// <summary>
    /// Identifies the count value.
    /// </summary>
    Count = 0xAC,
    /// <summary>
    /// Identifies the starts with value.
    /// </summary>
    StartsWith = 0xAD,
    /// <summary>
    /// Identifies the ends with value.
    /// </summary>
    EndsWith = 0xAE,
    /// <summary>
    /// Identifies the contains value.
    /// </summary>
    Contains = 0xAF,
    /// <summary>
    /// Identifies the contains any value.
    /// </summary>
    ContainsAny = 0xB0,
    /// <summary>
    /// Identifies the contains all value.
    /// </summary>
    ContainsAll = 0xB1,
    /// <summary>
    /// Identifies the has any value.
    /// </summary>
    HasAny = 0xB2,
    /// <summary>
    /// Identifies the has all value.
    /// </summary>
    HasAll = 0xB3,
    /// <summary>
    /// Identifies the contains value value.
    /// </summary>
    ContainsValue = 0xB4,
    /// <summary>
    /// Identifies the union value.
    /// </summary>
    Union = 0xB5,
    /// <summary>
    /// Identifies the intersect value.
    /// </summary>
    Intersect = 0xB6,
    /// <summary>
    /// Identifies the zip value.
    /// </summary>
    Zip = 0xB7,
    /// <summary>
    /// Identifies the keys of map value.
    /// </summary>
    KeysOfMap = 0xB8,
    /// <summary>
    /// Identifies the values of map value.
    /// </summary>
    ValuesOfMap = 0xB9,
    /// <summary>
    /// Identifies the entries of map value.
    /// </summary>
    EntriesOfMap = 0xBA,
    /// <summary>
    /// Identifies the first value.
    /// </summary>
    First = 0xBB,
    /// <summary>
    /// Identifies the last value.
    /// </summary>
    Last = 0xBC,
    /// <summary>
    /// Identifies the single value.
    /// </summary>
    Single = 0xBD,
    /// <summary>
    /// Identifies the iterator create value.
    /// </summary>
    IteratorCreate = 0xBE,
    /// <summary>
    /// Identifies the iterator create or jump value.
    /// </summary>
    IteratorCreateOrJump = 0xBF,
    /// <summary>
    /// Identifies the iterator next value.
    /// </summary>
    IteratorNext = 0xC0,
    /// <summary>
    /// Identifies the iterator close value.
    /// </summary>
    IteratorClose = 0xC1,
    /// <summary>
    /// Identifies the distinct value.
    /// </summary>
    Distinct = 0xC2,
    /// <summary>
    /// Identifies the sort ascending value.
    /// </summary>
    SortAscending = 0xC3,
    /// <summary>
    /// Identifies the sort descending value.
    /// </summary>
    SortDescending = 0xC4,
    /// <summary>
    /// Identifies the reverse value.
    /// </summary>
    Reverse = 0xC5,
    /// <summary>
    /// Identifies the shuffle value.
    /// </summary>
    Shuffle = 0xC6,
    /// <summary>
    /// Identifies the list builder create value.
    /// </summary>
    ListBuilderCreate = 0xC7,
    /// <summary>
    /// Identifies the list builder add value.
    /// </summary>
    ListBuilderAdd = 0xC8,
    /// <summary>
    /// Identifies the list builder finish value.
    /// </summary>
    ListBuilderFinish = 0xC9,
    /// <summary>
    /// Identifies the map builder create value.
    /// </summary>
    MapBuilderCreate = 0xCA,
    /// <summary>
    /// Identifies the map builder add value.
    /// </summary>
    MapBuilderAdd = 0xCB,
    /// <summary>
    /// Identifies the map builder finish value.
    /// </summary>
    MapBuilderFinish = 0xCC,
    /// <summary>
    /// Identifies the distinct builder create value.
    /// </summary>
    DistinctBuilderCreate = 0xCD,
    /// <summary>
    /// Identifies the distinct builder add value.
    /// </summary>
    DistinctBuilderAdd = 0xCE,
    /// <summary>
    /// Identifies the distinct builder finish value.
    /// </summary>
    DistinctBuilderFinish = 0xCF,
    /// <summary>
    /// Identifies the group builder create value.
    /// </summary>
    GroupBuilderCreate = 0xD0,
    /// <summary>
    /// Identifies the group builder add value.
    /// </summary>
    GroupBuilderAdd = 0xD1,
    /// <summary>
    /// Identifies the group builder finish value.
    /// </summary>
    GroupBuilderFinish = 0xD2,
    /// <summary>
    /// Identifies the order builder create value.
    /// </summary>
    OrderBuilderCreate = 0xD3,
    /// <summary>
    /// Identifies the order builder add value.
    /// </summary>
    OrderBuilderAdd = 0xD4,
    /// <summary>
    /// Identifies the order builder finish ascending value.
    /// </summary>
    OrderBuilderFinishAscending = 0xD5,
    /// <summary>
    /// Identifies the order builder finish descending value.
    /// </summary>
    OrderBuilderFinishDescending = 0xD6,
    /// <summary>
    /// Identifies the has pattern value.
    /// </summary>
    HasPattern = 0xD7,
    /// <summary>
    /// Identifies the take pattern value.
    /// </summary>
    TakePattern = 0xD8,
    /// <summary>
    /// Recognizes a complete data literal from Text, preserving the original Text on recognition failure.
    /// </summary>
    ParseLiteral = 0xD9,

    #endregion
}
