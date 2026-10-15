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
/// Represents a game event script binary format.
/// </summary>
public static class GesBinaryFormat
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
public enum GesSectionFlags : ushort
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
public enum GesSectionType : ushort
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
public enum GesDebugInfoOptions : byte
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
