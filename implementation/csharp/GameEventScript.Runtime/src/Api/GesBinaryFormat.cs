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
/// Fixed constants of the canonical little-endian <c>.gesb</c> V1 container framing.
/// </summary>
public static class GesBinaryFormat
{
    /// <summary>
    /// The <c>.gesb</c> format version this implementation reads and writes, stored in the file header.
    /// </summary>
    public const ushort Version = 1;
    /// <summary>
    /// The size in bytes of the fixed file header: magic, version, flags, header size and file size.
    /// </summary>
    public const uint HeaderSize = 16;
    /// <summary>
    /// The size in bytes of each section header: type, flags, version, reserved word and payload length.
    /// </summary>
    public const uint SectionHeaderSize = 12;
    /// <summary>
    /// The first byte of the ASCII file magic <c>GESB</c>.
    /// </summary>
    public const byte MagicG = (byte)'G';
    /// <summary>
    /// The second byte of the ASCII file magic <c>GESB</c>.
    /// </summary>
    public const byte MagicE = (byte)'E';
    /// <summary>
    /// The third byte of the ASCII file magic <c>GESB</c>.
    /// </summary>
    public const byte MagicS = (byte)'S';
    /// <summary>
    /// The fourth byte of the ASCII file magic <c>GESB</c>.
    /// </summary>
    public const byte MagicB = (byte)'B';
}

/// <summary>
/// The <c>SectionFlags</c> word of a section header. Bits outside <see cref="Required"/> and
/// <see cref="CompressionMask"/> are invalid in V1.
/// </summary>
[Flags]
public enum GesSectionFlags : ushort
{
    /// <summary>
    /// An optional, uncompressed section.
    /// </summary>
    None = 0,
    /// <summary>
    /// The section must be understood to load the program; set exactly on the five required runtime sections.
    /// </summary>
    Required = 0x0001,
    /// <summary>
    /// The compression-codec field. Codec zero means uncompressed, the only codec V1 implements; a required section
    /// with another codec is rejected.
    /// </summary>
    CompressionMask = 0x00F0
}

/// <summary>
/// The assigned <c>.gesb</c> section type IDs, listed in canonical writer order. Types <c>0x0000</c> and <c>0xFFFF</c>
/// are invalid; <c>0x8000</c>–<c>0xFFFD</c> are free for private or experimental optional sections.
/// </summary>
public enum GesSectionType : ushort
{
    /// <summary>
    /// The required <see cref="GesProgramMetadataSegment"/>.
    /// </summary>
    ProgramMetadata = 0x0001,
    /// <summary>
    /// The required <see cref="GesStringConstantSegment"/>.
    /// </summary>
    StringConstants = 0x0002,
    /// <summary>
    /// The required <see cref="GesUInt16IndexListSegment"/>.
    /// </summary>
    UInt16IndexLists = 0x0003,
    /// <summary>
    /// The required <see cref="GesBindingSegment"/>.
    /// </summary>
    Bindings = 0x0004,
    /// <summary>
    /// The required <see cref="GesCodeSegment"/>; V1 has exactly one linear code address space.
    /// </summary>
    Code = 0x0010,
    /// <summary>
    /// The optional <see cref="GesDebugSymbolsSegment"/>.
    /// </summary>
    DebugSymbols = 0x0020,
    /// <summary>
    /// The optional <see cref="GesSourceMapSegment"/>.
    /// </summary>
    SourceMap = 0x0021,
    /// <summary>
    /// The optional <see cref="GesSourceArchiveSegment"/>.
    /// </summary>
    SourceArchive = 0x0022,
    /// <summary>
    /// The optional <see cref="GesBuildMetadataSegment"/>.
    /// </summary>
    BuildMetadata = 0x0030,
    /// <summary>
    /// Reserved for a future signature section; V1 defines no payload and treats it as an unknown section.
    /// </summary>
    ReservedSignature = 0x0040,
    /// <summary>
    /// Reserved for named custom sections; V1 defines no payload and treats it as an unknown section.
    /// </summary>
    NamedCustom = 0xFFFE
}

/// <summary>
/// Selects which optional debug sections the Compiler emits. Compilation includes all of them by default;
/// size-sensitive builds opt out with <see cref="None"/>.
/// </summary>
[Flags]
public enum GesDebugInfoOptions : byte
{
    /// <summary>
    /// Emits no debug sections.
    /// </summary>
    None = 0,
    /// <summary>
    /// Emits the <see cref="GesDebugSymbolsSegment"/> naming parameters and locals.
    /// </summary>
    DebugSymbols = 1 << 0,
    /// <summary>
    /// Emits the <see cref="GesSourceMapSegment"/> mapping code addresses to source ranges.
    /// </summary>
    SourceMap = 1 << 1,
    /// <summary>
    /// Emits the <see cref="GesSourceArchiveSegment"/> embedding the compiler inputs.
    /// </summary>
    SourceArchive = 1 << 2,
    /// <summary>
    /// Emits all three debug sections.
    /// </summary>
    All = DebugSymbols | SourceMap | SourceArchive
}
