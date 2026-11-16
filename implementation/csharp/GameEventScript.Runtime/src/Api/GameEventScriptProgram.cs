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
/// The immutable, reusable parsed representation of one <c>.gesb</c> V1 program, produced only by the Compiler or by
/// <see cref="Read"/>, both of which run the complete program validator before returning it.
/// </summary>
/// <remarks>
/// A program holds only portable data that round-trips through <c>.gesb</c>: every table is a defensive immutable copy, and
/// no host binding, registry, cache or VM state is ever stored in it. It may be read concurrently and loaded into any
/// number of independent hosts. There is no public constructor.
/// </remarks>
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
    /// Gets the <c>.gesb</c> header format version; a valid V1 program carries <see cref="GesBinaryFormat.Version"/>.
    /// </summary>
    public ushort FormatVersion { get; }
    /// <summary>
    /// Gets the decoded ProgramMetadataSegment; the module and resource properties below forward to it.
    /// </summary>
    public GesProgramMetadataSegment Metadata { get; }
    /// <summary>
    /// Gets the module name, the stable program identity (<see cref="GesProgramMetadataSegment.ModuleName"/>).
    /// </summary>
    public string ModuleName => Metadata.ModuleName;
    /// <summary>
    /// Gets the application-owned program version; zero means development or unspecified
    /// (<see cref="GesProgramMetadataSegment.ProgramVersion"/>).
    /// </summary>
    public ulong ProgramVersion => Metadata.ProgramVersion;
    /// <summary>
    /// Gets the maximum register requirement across all message handlers
    /// (<see cref="GesProgramMetadataSegment.RequiredRegisterCount"/>).
    /// </summary>
    public ushort RequiredRegisterCount => Metadata.RequiredRegisterCount;
    /// <summary>
    /// Gets the maximum nested call depth across all message handlers
    /// (<see cref="GesProgramMetadataSegment.RequiredCallStackDepth"/>).
    /// </summary>
    public ushort RequiredCallStackDepth => Metadata.RequiredCallStackDepth;
    /// <summary>
    /// Gets the required string pool that names, labels, tags and text operands index.
    /// </summary>
    public GesStringConstantSegment StringConstants { get; }
    /// <summary>
    /// Gets the required pool of <see cref="ushort"/> lists that bindings and instructions index.
    /// </summary>
    public GesUInt16IndexListSegment UInt16IndexLists { get; }
    /// <summary>
    /// Gets the required binding table of handlers, callables, imports, outbound messages and Record constructors.
    /// </summary>
    public GesBindingSegment Bindings { get; }
    /// <summary>
    /// Gets the required linear instruction table; code addresses index into it.
    /// </summary>
    public GesCodeSegment Code { get; }
    /// <summary>
    /// Gets the optional parameter and local names, or <see langword="null"/> when the program has none or the reader
    /// did not retain them.
    /// </summary>
    public GesDebugSymbolsSegment? DebugSymbols { get; }
    /// <summary>
    /// Gets the optional code-to-source mapping, or <see langword="null"/> when the program has none or the reader did not
    /// retain it.
    /// </summary>
    public GesSourceMapSegment? SourceMap { get; }
    /// <summary>
    /// Gets the optional embedded compiler inputs, or <see langword="null"/> when the program has none or the reader did
    /// not retain them.
    /// </summary>
    public GesSourceArchiveSegment? SourceArchive { get; }
    /// <summary>
    /// Gets the optional producing-compiler identification, or <see langword="null"/> when the program has none or the
    /// reader did not retain it.
    /// </summary>
    public GesBuildMetadataSegment? BuildMetadata { get; }
    /// <summary>
    /// Gets the optional sections retained without interpretation under <see cref="GesProgramRetention.PreserveAll"/>;
    /// empty otherwise and for compiled programs.
    /// </summary>
    public GesReadOnlyArray<GesOpaqueSection> OpaqueSections { get; }

    /// <summary>
    /// Decodes bounded, portable <c>.gesb</c> V1 bytes into an immutable program.
    /// </summary>
    /// <param name="bytes">The complete byte image. The returned program owns all retained data.</param>
    /// <param name="options">Optional retention and resource limits; defaults preserve all supported and opaque optional sections.</param>
    /// <returns>A structurally and semantically validated immutable program.</returns>
    /// <exception cref="GesProgramFormatException">Thrown with a stable error code when the image is malformed, unsupported, exceeds limits, or describes an invalid program.</exception>
    /// <exception cref="ArgumentException">Thrown when <see cref="GesProgramReadOptions.Limits"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when a configured limit lies outside its supported V1 range.</exception>
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
    public void Validate()
    {
        GesProgramValidator.Validate(this);
        IsValidated = true;
    }

    // Program data is immutable, so one successful validation holds for every later Load and encode
    // (specs/ProgramModel.md). Concurrent first validations may both run; each writes the same value.
    internal bool IsValidated { get; private set; }

    internal void EnsureValid()
    {
        if (!IsValidated) Validate();
    }

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
