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
        GesStringConstantSegment stringConstantSegment,
        GesUInt16IndexListSegment uint16IndexListSegment,
        GesBindingSegment bindingSegment,
        GesCodeSegment codeSegment,
        GesDebugSymbolsSegment? debugSymbolsSegment = null,
        GesSourceMapSegment? sourceMapSegment = null,
        GesSourceArchiveSegment? sourceArchiveSegment = null,
        GesBuildMetadataSegment? buildMetadataSegment = null,
        IReadOnlyList<GesOpaqueSection>? opaqueSections = null)
    {
        FormatVersion = formatVersion;
        Metadata = new GesProgramMetadataSegment(moduleName, requiredRegisterCount, requiredCallStackDepth, programVersion);
        StringConstants = stringConstantSegment;
        UInt16IndexLists = uint16IndexListSegment;
        Bindings = bindingSegment;
        Code = codeSegment;
        DebugSymbols = debugSymbolsSegment;
        SourceMap = sourceMapSegment;
        SourceArchive = sourceArchiveSegment;
        BuildMetadata = buildMetadataSegment;
        OpaqueSections = new GesReadOnlyArray<GesOpaqueSection>(opaqueSections);
    }

    /// <summary>
    /// Gets the format version.
    /// </summary>
    public ushort FormatVersion { get; }
    /// <summary>
    /// Gets the metadata.
    /// </summary>
    public GesProgramMetadataSegment Metadata { get; }
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
    public GesStringConstantSegment StringConstants { get; }
    /// <summary>
    /// Gets the u int16 index lists.
    /// </summary>
    public GesUInt16IndexListSegment UInt16IndexLists { get; }
    /// <summary>
    /// Gets the bindings.
    /// </summary>
    public GesBindingSegment Bindings { get; }
    /// <summary>
    /// Gets the code.
    /// </summary>
    public GesCodeSegment Code { get; }
    /// <summary>
    /// Gets the debug symbols.
    /// </summary>
    public GesDebugSymbolsSegment? DebugSymbols { get; }
    /// <summary>
    /// Gets the source map.
    /// </summary>
    public GesSourceMapSegment? SourceMap { get; }
    /// <summary>
    /// Gets the source archive.
    /// </summary>
    public GesSourceArchiveSegment? SourceArchive { get; }
    /// <summary>
    /// Gets the build metadata.
    /// </summary>
    public GesBuildMetadataSegment? BuildMetadata { get; }
    /// <summary>
    /// Gets the opaque sections.
    /// </summary>
    public GesReadOnlyArray<GesOpaqueSection> OpaqueSections { get; }

    /// <summary>
    /// Decodes bounded, portable <c>.gesb</c> V1 bytes into an immutable program.
    /// </summary>
    /// <param name="bytes">The complete byte image. The returned program owns all retained data.</param>
    /// <param name="options">Optional retention and resource limits; defaults preserve all supported and opaque optional sections.</param>
    /// <returns>A structurally and semantically validated immutable program.</returns>
    /// <exception cref="GesProgramFormatException">Thrown with a stable error code when the image is malformed, unsupported, exceeds limits, or describes an invalid program.</exception>
    public static GameEventScriptProgram Read(ReadOnlySpan<byte> bytes, GesProgramReadOptions? options = null) => GesProgramReader.Read(bytes, options);

    /// <summary>
    /// Validates this program and computes its canonical <c>.gesb</c> V1 size without encoding it.
    /// </summary>
    /// <returns>The required destination size in bytes.</returns>
    /// <exception cref="GesProgramFormatException">Thrown when the program is invalid or cannot be represented by V1.</exception>
    public int GetEncodedSize() => GesProgramWriter.GetEncodedSize(this);

    /// <summary>
    /// Validates and encodes this program into a newly allocated canonical Little-Endian <c>.gesb</c> V1 byte array.
    /// </summary>
    /// <returns>The complete canonical byte image.</returns>
    /// <exception cref="GesProgramFormatException">Thrown when the program is invalid or cannot be represented by V1.</exception>
    public byte[] Encode() => GesProgramWriter.ToArray(this);

    /// <summary>
    /// Validates and encodes this program into existing storage without allocating the image.
    /// </summary>
    /// <param name="destination">The destination span, which may be larger than the encoded image.</param>
    /// <returns>The number of destination bytes written.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="destination"/> is too small.</exception>
    /// <exception cref="GesProgramFormatException">Thrown when the program is invalid or cannot be represented by V1.</exception>
    public int Write(Span<byte> destination) => GesProgramWriter.Write(this, destination);

    /// <summary>
    /// Enforces the shared structural and semantic invariants required by the reader, writer, and host linker.
    /// </summary>
    /// <exception cref="GesProgramFormatException">Thrown with a stable error code when an invariant is violated.</exception>
    public void Validate() => GesProgramValidator.Validate(this);

    /// <summary>
    /// Dumps program metadata, tables, binds, and code as assembler-like GESA text.
    /// </summary>
    /// <returns>The disassembly without instruction addresses.</returns>
    public string Dump() => GesProgramDumper.Dump(this);

    /// <summary>
    /// Dumps program metadata, tables, binds, and code as assembler-like GESA text.
    /// </summary>
    /// <param name="includeInstructionAddresses">Whether each instruction line starts with its code address.</param>
    /// <returns>The disassembly.</returns>
    public string Dump(bool includeInstructionAddresses) => GesProgramDumper.Dump(this, includeInstructionAddresses);
}
