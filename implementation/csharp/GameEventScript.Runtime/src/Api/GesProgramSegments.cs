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
/// The immutable decoded ProgramMetadataSegment (section <c>0x0001</c>) of a <c>.gesb</c> V1 program: module identity,
/// program-level resource bounds and the application-owned program version.
/// </summary>
public readonly struct GesProgramMetadataSegment
{
    /// <summary>
    /// Creates the metadata segment. Only the Compiler and the binary Reader construct Programs and their segments.
    /// </summary>
    /// <param name="moduleName">The module name; <see langword="null"/> is stored as an empty string.</param>
    /// <param name="requiredRegisterCount">The maximum register requirement across all message handlers.</param>
    /// <param name="requiredCallStackDepth">The maximum nested call depth across all message handlers.</param>
    /// <param name="programVersion">The application-owned program version; zero means development or unspecified.</param>
    internal GesProgramMetadataSegment(string moduleName, ushort requiredRegisterCount, ushort requiredCallStackDepth, ulong programVersion)
    {
        ModuleName = moduleName ?? string.Empty;
        RequiredRegisterCount = requiredRegisterCount;
        RequiredCallStackDepth = requiredCallStackDepth;
        ProgramVersion = programVersion;
    }

    /// <summary>
    /// Gets the decoded module name, the stable program identity. In <c>.gesb</c> it is stored as an index into the
    /// <see cref="GesStringConstantSegment"/>.
    /// </summary>
    public string ModuleName { get; }
    /// <summary>
    /// Gets the maximum <see cref="GesBindingSegment.Entry.RequiredRegisterCount"/> across all message handlers: the number
    /// of VM registers a host must provide to run any handler of this program.
    /// </summary>
    public ushort RequiredRegisterCount { get; }
    /// <summary>
    /// Gets the maximum <see cref="GesBindingSegment.Entry.RequiredCallStackDepth"/> across all message handlers: the
    /// deepest chain of nested calls below a root handler, where a handler that calls nothing has depth zero.
    /// </summary>
    public ushort RequiredCallStackDepth { get; }
    /// <summary>
    /// Gets the application-owned program version, distinct from the format and section versions; zero means
    /// development or unspecified.
    /// </summary>
    public ulong ProgramVersion { get; }
}

/// <summary>
/// The immutable StringConstantSegment (section <c>0x0002</c>): an ordered pool of at most 65,535 strict UTF-8 strings
/// that bytecode and bindings reference by zero-based <see cref="ushort"/> index. Order and duplicates are program data.
/// </summary>
/// <remarks>
/// All entries share one contiguous byte buffer, <see cref="Data"/>, addressed by <see cref="Slices"/>. A
/// <see langword="default"/> instance is an empty pool on which every index is invalid.
/// </remarks>
public readonly struct GesStringConstantSegment
{
    /// <summary>
    /// Locates one string entry inside <see cref="Data"/>.
    /// </summary>
    public readonly struct SliceEntry
    {
        /// <summary>
        /// Gets the zero-based byte offset of the entry's UTF-8 bytes in <see cref="Data"/>.
        /// </summary>
        public int Start { get; internal init; }
        /// <summary>
        /// Gets the entry's length in UTF-8 bytes (not UTF-16 code units).
        /// </summary>
        public int Length { get; internal init; }
    }

    private readonly GesReadOnlyArray<SliceEntry> _slices;
    private readonly GesReadOnlyArray<byte> _data;

    /// <summary>
    /// Creates the segment from defensive copies of <paramref name="slices"/> and <paramref name="data"/>. Only the
    /// Compiler and the binary Reader construct Programs and their segments.
    /// </summary>
    /// <param name="slices">One slice per string entry, in index order.</param>
    /// <param name="data">The concatenated UTF-8 bytes the slices address.</param>
    internal GesStringConstantSegment(IReadOnlyList<SliceEntry> slices, IReadOnlyList<byte> data)
    {
        _slices = new GesReadOnlyArray<SliceEntry>(slices);
        _data = new GesReadOnlyArray<byte>(data);
    }

    // Takes ownership of arrays the reader built, which nothing else references, instead of copying them again.
    internal static GesStringConstantSegment FromOwned(SliceEntry[] slices, byte[] data) => new(GesReadOnlyArray<SliceEntry>.FromOwnedArray(slices), GesReadOnlyArray<byte>.FromOwnedArray(data), owned: true);

    private GesStringConstantSegment(GesReadOnlyArray<SliceEntry> slices, GesReadOnlyArray<byte> data, bool owned)
    {
        _ = owned;
        _slices = slices;
        _data = data;
    }

    /// <summary>
    /// Gets the immutable slice table; its <see cref="GesReadOnlyArray{T}.Count"/> is the number of string entries.
    /// </summary>
    public GesReadOnlyArray<SliceEntry> Slices => _slices;
    /// <summary>
    /// Gets the immutable concatenated UTF-8 bytes of all entries.
    /// </summary>
    public GesReadOnlyArray<byte> Data => _data;

    /// <summary>
    /// Returns the UTF-8 byte length of a string entry without decoding it.
    /// </summary>
    /// <param name="index">The zero-based entry index, which must be less than the number of <see cref="Slices"/>.</param>
    /// <returns>The entry's length in UTF-8 bytes.</returns>
    /// <exception cref="IndexOutOfRangeException">The index does not name an entry.</exception>
    public int ResolveSize(ushort index) => _slices[index].Length;
    /// <summary>
    /// Decodes a string entry into a new <see cref="string"/>; each call decodes again.
    /// </summary>
    /// <param name="index">The zero-based entry index, which must be less than the number of <see cref="Slices"/>.</param>
    /// <returns>The decoded string.</returns>
    /// <exception cref="IndexOutOfRangeException">The index does not name an entry.</exception>
    public string Resolve(ushort index)
    {
        var slice = _slices[index];
        return _data.DecodeUtf8(Encoding.UTF8, slice.Start, slice.Length);
    }
}

/// <summary>
/// A read-only view of one list of a <see cref="GesUInt16IndexListSegment"/>, such as an argument register list or a
/// message shape; it shares the segment's storage without copying.
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
/// The immutable UInt16IndexListSegment (section <c>0x0003</c>): at most 65,535 ordered lists of <see cref="ushort"/>
/// elements that bindings and instructions reference by zero-based list index. The referencing field or opcode defines
/// what the elements mean, for example string indices of argument names and tags, or register lists.
/// </summary>
/// <remarks>
/// All lists share one contiguous element buffer, <see cref="Data"/>, addressed by <see cref="Slices"/>. A
/// <see langword="default"/> instance has no lists.
/// </remarks>
public readonly struct GesUInt16IndexListSegment
{
    /// <summary>
    /// Locates one list inside <see cref="Data"/>.
    /// </summary>
    public readonly struct SliceEntry
    {
        /// <summary>
        /// Gets the zero-based element offset of the list's first element in <see cref="Data"/>.
        /// </summary>
        public int Start { get; internal init; }
        /// <summary>
        /// Gets the number of elements in the list.
        /// </summary>
        public int Length { get; internal init; }
    }

    private readonly GesReadOnlyArray<SliceEntry> _slices;
    private readonly GesReadOnlyArray<ushort> _data;

    /// <summary>
    /// Creates the segment from defensive copies of <paramref name="slices"/> and <paramref name="data"/>. Only the
    /// Compiler and the binary Reader construct Programs and their segments.
    /// </summary>
    /// <param name="slices">One slice per list, in list-index order.</param>
    /// <param name="data">The concatenated elements the slices address.</param>
    internal GesUInt16IndexListSegment(IReadOnlyList<SliceEntry> slices, IReadOnlyList<ushort> data)
    {
        _slices = new GesReadOnlyArray<SliceEntry>(slices);
        _data = new GesReadOnlyArray<ushort>(data);
    }

    // Takes ownership of arrays the reader built, which nothing else references, instead of copying them again.
    internal static GesUInt16IndexListSegment FromOwned(SliceEntry[] slices, ushort[] data) => new(GesReadOnlyArray<SliceEntry>.FromOwnedArray(slices), GesReadOnlyArray<ushort>.FromOwnedArray(data), owned: true);

    private GesUInt16IndexListSegment(GesReadOnlyArray<SliceEntry> slices, GesReadOnlyArray<ushort> data, bool owned)
    {
        _ = owned;
        _slices = slices;
        _data = data;
    }

    /// <summary>
    /// Gets the immutable slice table; its <see cref="GesReadOnlyArray{T}.Count"/> is the number of lists.
    /// </summary>
    public GesReadOnlyArray<SliceEntry> Slices => _slices;
    /// <summary>
    /// Gets the immutable concatenated elements of all lists.
    /// </summary>
    public GesReadOnlyArray<ushort> Data => _data;

    /// <summary>
    /// Returns a read-only view of one list that shares this segment's storage without copying it.
    /// </summary>
    /// <param name="index">The zero-based list index, which must be less than the number of <see cref="Slices"/>.</param>
    /// <returns>The list's elements; <see cref="GesUInt16IndexList.Start"/> is its offset in <see cref="Data"/>.</returns>
    /// <exception cref="IndexOutOfRangeException">The index does not name a list.</exception>
    public GesUInt16IndexList Resolve(ushort index) => new(_data, _slices[index].Start, _slices[index].Length);
}

/// <summary>
/// The immutable BindingSegment (section <c>0x0004</c>): the program's exported handlers and callables, imported
/// extensions and external types, outbound message signatures and Record constructors, in encoded order.
/// </summary>
/// <remarks>A <see langword="default"/> instance has no entries.</remarks>
public readonly struct GesBindingSegment
{

    /// <summary>
    /// One immutable binding record. In <c>.gesb</c> the argument-name and tag lists are UInt16IndexListSegment indices;
    /// this model holds their resolved elements, which are string-constant indices.
    /// </summary>
    /// <param name="kind">The binding kind.</param>
    /// <param name="name">The string-constant index of the binding name.</param>
    /// <param name="argumentNames">The string-constant indices of the ordered argument labels; <see langword="null"/> means none.</param>
    /// <param name="entryAddress">The code address of an executable binding's entry, or <c>0xFFFF</c> when there is none.</param>
    /// <param name="id">The binding ID, or <c>0xFFFF</c> when there is none.</param>
    /// <param name="requiredTags">The string-constant indices of tags a delivery must carry; <see langword="null"/> means none.</param>
    /// <param name="excludedTags">The string-constant indices of tags a delivery must not carry; <see langword="null"/> means none.</param>
    /// <param name="requiredRegisterCount">The declared register requirement of a message handler.</param>
    /// <param name="requiredCallStackDepth">The declared nested call depth of a message handler.</param>
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
        /// Gets the binding ID, unique per kind except for handlers, whose ID is an overload ordinal scoped to their message
        /// name; <c>0xFFFF</c> means no ID. Every Record binding carries an ID.
        /// </summary>
        public ushort Id { get; } = id;

        /// <summary>
        /// Gets the binding kind. Export kinds occupy <c>0x10</c>–<c>0x1F</c>, import kinds <c>0x20</c>–<c>0x2F</c>.
        /// </summary>
        public GesBinaryBindKind Kind { get; } = kind;

        /// <summary>
        /// Gets the string-constant index of the binding name: a message name, callable name, <c>extension.function</c>
        /// for extension imports, or a type name.
        /// </summary>
        public ushort Name { get; } = name;

        /// <summary>
        /// Gets the string-constant indices of the ordered argument labels, which together with <see cref="Name"/> form
        /// the signature. Empty when the binding has no arguments.
        /// </summary>
        public GesReadOnlyArray<ushort> ArgumentNames => _argumentNames;

        /// <summary>
        /// Gets the string-constant indices of a handler's <c>matching</c> tags, all of which a message must carry to be
        /// delivered; empty means no requirement.
        /// </summary>
        public GesReadOnlyArray<ushort> RequiredTags => _requiredTags;

        /// <summary>
        /// Gets the string-constant indices of a handler's <c>without</c> tags, any of which excludes a message; empty
        /// means none.
        /// </summary>
        public GesReadOnlyArray<ushort> ExcludedTags => _excludedTags;

        /// <summary>
        /// Gets the zero-based code address (instruction index) of an executable binding's <c>RegisterLocals</c> prolog;
        /// non-executable bindings such as imports use the sentinel <c>0xFFFF</c>.
        /// </summary>
        public ushort EntryAddress { get; } = entryAddress;

        /// <summary>
        /// Gets a message handler's declared conservative upper bound of VM registers retained while it runs, including
        /// staged values and all caller frames on its deepest synchronous call path.
        /// </summary>
        public ushort RequiredRegisterCount { get; } = requiredRegisterCount;

        /// <summary>
        /// Gets a message handler's declared conservative upper bound of nested calls below the handler; zero when it
        /// calls nothing.
        /// </summary>
        public ushort RequiredCallStackDepth { get; } = requiredCallStackDepth;

    }

    private readonly GesReadOnlyArray<GesBindingSegment.Entry> _entries;

    /// <summary>
    /// Creates the segment from a defensive copy of <paramref name="entries"/>. Only the Compiler and the binary Reader
    /// construct Programs and their segments.
    /// </summary>
    /// <param name="entries">The binding records in encoded order; <see langword="null"/> means none.</param>
    /// <exception cref="ArgumentOutOfRangeException">More than 65,535 entries are supplied.</exception>
    internal GesBindingSegment(IReadOnlyList<GesBindingSegment.Entry>? entries)
    {
        _entries = new GesReadOnlyArray<GesBindingSegment.Entry>(entries);
        EntryCount = ToEntryCount(_entries.Count);
    }

    /// <summary>
    /// Gets the number of binding records, equal to the count of <see cref="Entries"/>.
    /// </summary>
    public ushort EntryCount { get; }

    /// <summary>
    /// Gets the immutable binding records in encoded order.
    /// </summary>
    public GesReadOnlyArray<GesBindingSegment.Entry> Entries => _entries;

    private static ushort ToEntryCount(int count) => count > ushort.MaxValue ? throw new ArgumentOutOfRangeException(nameof(count), "GameEventScriptProgram tables cannot exceed 65535 entries.") : checked((ushort)count);

}

/// <summary>
/// The immutable CodeSegment (section <c>0x0010</c>): the program's single linear instruction table of at most 65,535
/// instructions, each encoded as 16 bytes. A code address is the zero-based index of an instruction in this table.
/// </summary>
/// <remarks>A <see langword="default"/> instance has no instructions.</remarks>
public readonly struct GesCodeSegment
{
    private readonly GesReadOnlyArray<GesBytecodeInstruction> _instructions;

    /// <summary>
    /// Creates the segment from a defensive copy of <paramref name="instructions"/>. Only the Compiler and the binary
    /// Reader construct Programs and their segments.
    /// </summary>
    /// <param name="instructions">The instructions in code-address order; <see langword="null"/> means none.</param>
    internal GesCodeSegment(IReadOnlyList<GesBytecodeInstruction>? instructions)
        => _instructions = new GesReadOnlyArray<GesBytecodeInstruction>(instructions);

    /// <summary>
    /// Gets the number of instructions.
    /// </summary>
    public int Count => _instructions.Count;
    /// <summary>
    /// Gets the number of instructions; identical to <see cref="Count"/>.
    /// </summary>
    public int Length => _instructions.Count;
    /// <summary>
    /// Gets a copy of the instruction at a code address; mutating the returned struct cannot change the program.
    /// </summary>
    /// <param name="index">The zero-based code address, which must be less than <see cref="Count"/>.</param>
    /// <exception cref="IndexOutOfRangeException">The address is outside the code segment.</exception>
    public GesBytecodeInstruction this[int index] => _instructions[index];
    /// <summary>
    /// Gets the immutable instruction table in code-address order.
    /// </summary>
    public GesReadOnlyArray<GesBytecodeInstruction> Instructions => _instructions;
}

/// <summary>
/// The kind of a named register in <see cref="GesDebugSymbolsSegment"/>; the numeric values are the encoded
/// <c>SymbolKind</c> byte.
/// </summary>
public enum GesDebugSymbolKind : byte
{
    /// <summary>
    /// An explicit script parameter of a handler or callable.
    /// </summary>
    Parameter = 1,
    /// <summary>
    /// An explicit script local binding.
    /// </summary>
    Local = 2
}

/// <summary>
/// Names one physical register over a half-open range of code addresses. Only explicit script parameters and locals are
/// named; compiler temporaries are not.
/// </summary>
/// <param name="Kind">Whether the register holds a parameter or a local.</param>
/// <param name="RegisterId">The physical register after final allocation, below the frame length of the enclosing routine.</param>
/// <param name="Name">The decoded source name; the encoding stores it in the segment's own name pool.</param>
/// <param name="CodeStart">The zero-based code address where the name becomes valid.</param>
/// <param name="CodeLength">The nonzero number of instructions the name stays valid, within one routine.</param>
public readonly record struct GesDebugSymbol(GesDebugSymbolKind Kind, ushort RegisterId, string Name, uint CodeStart, uint CodeLength);

/// <summary>
/// The immutable optional DebugSymbolsSegment (section <c>0x0020</c>), which names script parameters and locals by
/// register and code range. Runtime behaviour never depends on it.
/// </summary>
public sealed class GesDebugSymbolsSegment
{
    /// <summary>
    /// Creates the segment from a defensive copy of <paramref name="symbols"/>. Only the Compiler and the binary Reader
    /// construct Programs and their segments.
    /// </summary>
    /// <param name="symbols">The symbols in encoded order; <see langword="null"/> means none.</param>
    internal GesDebugSymbolsSegment(IReadOnlyList<GesDebugSymbol>? symbols)
        => Symbols = new GesReadOnlyArray<GesDebugSymbol>(symbols);

    /// <summary>
    /// Gets the immutable symbols in encoded order.
    /// </summary>
    public GesReadOnlyArray<GesDebugSymbol> Symbols { get; }
}

/// <summary>
/// Describes one compiler input in the <see cref="GesSourceMapSegment"/>. All offsets and lengths count bytes of the
/// logical UTF-8 source, not UTF-16 code units.
/// </summary>
public sealed class GesSourceMapSource
{
    /// <summary>
    /// Creates the source description from defensive copies of its byte and offset tables. Only the Compiler and the
    /// binary Reader construct Programs and their segments.
    /// </summary>
    /// <param name="sourceId">The stable source ID, equal to the source's index in the table.</param>
    /// <param name="sourceName">The source name; <see langword="null"/> is stored as an empty string.</param>
    /// <param name="sourceByteLength">The source length in UTF-8 bytes.</param>
    /// <param name="sha256">The 32-byte SHA-256 of the logical UTF-8 source.</param>
    /// <param name="lineStartByteOffsets">The UTF-8 byte offset at which each line starts.</param>
    internal GesSourceMapSource(uint sourceId, string sourceName, uint sourceByteLength, IReadOnlyList<byte> sha256, IReadOnlyList<uint> lineStartByteOffsets)
    {
        SourceId = sourceId;
        SourceName = sourceName ?? string.Empty;
        SourceByteLength = sourceByteLength;
        Sha256 = new GesReadOnlyArray<byte>(sha256);
        LineStartByteOffsets = new GesReadOnlyArray<uint>(lineStartByteOffsets);
    }

    /// <summary>
    /// Gets the stable source ID, which equals this source's zero-based index in <see cref="GesSourceMapSegment.Sources"/>
    /// and follows the compiler's input order.
    /// </summary>
    public uint SourceId { get; }
    /// <summary>
    /// Gets the source name as given to the compiler.
    /// </summary>
    public string SourceName { get; }
    /// <summary>
    /// Gets the length of the logical source in UTF-8 bytes.
    /// </summary>
    public uint SourceByteLength { get; }
    /// <summary>
    /// Gets the 32 bytes of the SHA-256 hash of the logical UTF-8 source content.
    /// </summary>
    public GesReadOnlyArray<byte> Sha256 { get; }
    /// <summary>
    /// Gets the UTF-8 byte offset at which each zero-based line starts. The first offset is zero; later offsets are
    /// strictly increasing UTF-8 boundaries and may equal <see cref="SourceByteLength"/> for a final empty line.
    /// </summary>
    public GesReadOnlyArray<uint> LineStartByteOffsets { get; }
}

/// <summary>
/// Maps a half-open range of code addresses to a range of UTF-8 bytes in one source.
/// </summary>
/// <param name="CodeStart">The zero-based code address of the first mapped instruction.</param>
/// <param name="CodeLength">The nonzero number of mapped instructions.</param>
/// <param name="SourceId">The <see cref="GesSourceMapSource.SourceId"/> of the mapped source.</param>
/// <param name="SourceStartByteOffset">The zero-based UTF-8 byte offset of the mapped source range.</param>
/// <param name="SourceByteLength">The nonzero length of the mapped source range in UTF-8 bytes.</param>
public readonly record struct GesSourceMapEntry(uint CodeStart, uint CodeLength, uint SourceId, uint SourceStartByteOffset, uint SourceByteLength);

/// <summary>
/// The immutable optional SourceMapSegment (section <c>0x0021</c>), which maps code addresses to source byte ranges. It
/// is useful without an embedded <see cref="GesSourceArchiveSegment"/>; when both are present they must agree.
/// </summary>
public sealed class GesSourceMapSegment
{
    /// <summary>
    /// Creates the segment from defensive copies of <paramref name="sources"/> and <paramref name="entries"/>. Only the
    /// Compiler and the binary Reader construct Programs and their segments.
    /// </summary>
    /// <param name="sources">The source table, indexed by source ID; <see langword="null"/> means none.</param>
    /// <param name="entries">The mappings in ascending, non-overlapping code order; <see langword="null"/> means none.</param>
    internal GesSourceMapSegment(IReadOnlyList<GesSourceMapSource>? sources, IReadOnlyList<GesSourceMapEntry>? entries)
    {
        Sources = new GesReadOnlyArray<GesSourceMapSource>(sources);
        Entries = new GesReadOnlyArray<GesSourceMapEntry>(entries);
    }

    /// <summary>
    /// Gets the immutable source table; a source's index equals its <see cref="GesSourceMapSource.SourceId"/>.
    /// </summary>
    public GesReadOnlyArray<GesSourceMapSource> Sources { get; }
    /// <summary>
    /// Gets the immutable mappings, ordered by ascending, non-overlapping code ranges.
    /// </summary>
    public GesReadOnlyArray<GesSourceMapEntry> Entries { get; }
}

/// <summary>
/// One embedded compiler input in the <see cref="GesSourceArchiveSegment"/>: the exact logical source as UTF-8, without
/// the original byte order mark or file encoding.
/// </summary>
public sealed class GesSourceArchiveEntry
{
    private readonly GesReadOnlyArray<byte> _utf8Content;

    /// <summary>
    /// Creates the entry from a defensive copy of <paramref name="utf8Content"/>. Only the Compiler and the binary Reader
    /// construct Programs and their segments.
    /// </summary>
    /// <param name="sourceId">The source ID, matching the source map's ID when both are present.</param>
    /// <param name="sourceName">The source name; <see langword="null"/> is stored as an empty string.</param>
    /// <param name="utf8Content">The logical source content as UTF-8 bytes.</param>
    internal GesSourceArchiveEntry(uint sourceId, string sourceName, IReadOnlyList<byte> utf8Content)
    {
        SourceId = sourceId;
        SourceName = sourceName ?? string.Empty;
        _utf8Content = new GesReadOnlyArray<byte>(utf8Content);
    }

    /// <summary>
    /// Gets the source ID, unique within the archive and equal to the matching <see cref="GesSourceMapSource.SourceId"/>.
    /// </summary>
    public uint SourceId { get; }
    /// <summary>
    /// Gets the source name, nonempty in a valid program.
    /// </summary>
    public string SourceName { get; }
    /// <summary>
    /// Gets the immutable logical source content as strict UTF-8 bytes.
    /// </summary>
    public GesReadOnlyArray<byte> Utf8Content => _utf8Content;
    /// <summary>
    /// Decodes <see cref="Utf8Content"/> into a new <see cref="string"/>; each call decodes again.
    /// </summary>
    /// <returns>The source text.</returns>
    public string ResolveText() => DecodeText(Encoding.UTF8);

    internal string DecodeText(Encoding encoding) => _utf8Content.DecodeUtf8(encoding, 0, _utf8Content.Count);
}

/// <summary>
/// The immutable optional SourceArchiveSegment (section <c>0x0022</c>), which embeds the compiler inputs.
/// </summary>
public sealed class GesSourceArchiveSegment
{
    /// <summary>
    /// Creates the segment from a defensive copy of <paramref name="sources"/>. Only the Compiler and the binary Reader
    /// construct Programs and their segments.
    /// </summary>
    /// <param name="sources">The embedded sources in encoded order; <see langword="null"/> means none.</param>
    internal GesSourceArchiveSegment(IReadOnlyList<GesSourceArchiveEntry>? sources)
        => Sources = new GesReadOnlyArray<GesSourceArchiveEntry>(sources);

    /// <summary>
    /// Gets the immutable embedded sources in encoded order.
    /// </summary>
    public GesReadOnlyArray<GesSourceArchiveEntry> Sources { get; }
}

/// <summary>
/// The immutable optional BuildMetadataSegment (section <c>0x0030</c>), which names the producing compiler. Runtime
/// behaviour never depends on it.
/// </summary>
public sealed class GesBuildMetadataSegment
{
    /// <summary>
    /// Creates the segment. Only the Compiler and the binary Reader construct Programs and their segments.
    /// </summary>
    /// <param name="compilerId">The stable compiler implementation ID; <see langword="null"/> is stored as an empty string.</param>
    /// <param name="compilerVersion">The compiler version; <see langword="null"/> is stored as an empty string.</param>
    internal GesBuildMetadataSegment(string compilerId, string compilerVersion)
    {
        CompilerId = compilerId ?? string.Empty;
        CompilerVersion = compilerVersion ?? string.Empty;
    }

    /// <summary>
    /// Gets the stable ID of the producing compiler implementation, for example <c>steph.ges.compiler.csharp</c>.
    /// </summary>
    public string CompilerId { get; }
    /// <summary>
    /// Gets the version of the producing compiler.
    /// </summary>
    public string CompilerVersion { get; }
}

/// <summary>
/// An optional section the reader retained without interpreting it under <see cref="GesProgramRetention.PreserveAll"/>:
/// an unknown optional section, or a known optional section with an unsupported version or compression. The writer
/// re-emits it unchanged after the known sections.
/// </summary>
public sealed class GesOpaqueSection
{
    private readonly GesReadOnlyArray<byte> _rawPayload;

    /// <summary>
    /// Creates the section from a defensive copy of <paramref name="rawPayload"/>. Only the binary Reader constructs it.
    /// </summary>
    /// <param name="sectionType">The raw encoded section type.</param>
    /// <param name="flags">The encoded section flags.</param>
    /// <param name="sectionVersion">The encoded section version.</param>
    /// <param name="rawPayload">The section payload bytes.</param>
    /// <param name="originalOrdinal">The zero-based position of the section among all sections of the read file.</param>
    internal GesOpaqueSection(ushort sectionType, GesSectionFlags flags, ushort sectionVersion, IReadOnlyList<byte> rawPayload, int originalOrdinal)
    {
        SectionType = sectionType;
        Flags = flags;
        SectionVersion = sectionVersion;
        _rawPayload = new GesReadOnlyArray<byte>(rawPayload);
        OriginalOrdinal = originalOrdinal;
    }

    /// <summary>
    /// Gets the raw encoded section type, which may be outside <see cref="GesSectionType"/>.
    /// </summary>
    public ushort SectionType { get; }
    /// <summary>
    /// Gets the encoded section flags; <see cref="GesSectionFlags.Required"/> is never set on an opaque section.
    /// </summary>
    public GesSectionFlags Flags { get; }
    /// <summary>
    /// Gets the encoded section version.
    /// </summary>
    public ushort SectionVersion { get; }
    /// <summary>
    /// Gets the immutable payload bytes exactly as read, excluding the 12-byte section header.
    /// </summary>
    public GesReadOnlyArray<byte> RawPayload => _rawPayload;
    /// <summary>
    /// Gets the zero-based position of this section among all sections of the file it was read from; unique per
    /// program, it determines the relative order in which the writer re-emits opaque sections.
    /// </summary>
    public int OriginalOrdinal { get; }
}
