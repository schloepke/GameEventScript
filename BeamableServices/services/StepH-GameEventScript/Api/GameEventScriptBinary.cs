#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections.Generic;
using System.Linq;

namespace StepH.GameEventScript.Api;

public readonly struct GameEventScriptBinary
{
    public readonly GameEventScriptBinaryHeader Header { get; init; }
    public readonly string ModuleName { get; init; }
    public readonly string[] StringPool { get; init; }
    public readonly GameEventScriptBinaryExportTable ExportTable { get; init; }
    public readonly GameEventScriptBinaryImportTable ImportTable  { get; init; }
}

public readonly struct GameEventScriptBinaryHeader
{
    [Flags]
    public enum GameEventScriptBinaryFlags : ushort
    {
        None = 0,
        Optimization = 1 << 0,
        Debug = 1 << 1,
    }
    
    public const uint Magic = 0x42534547; // "GESB"

    public ushort Version { get; init; }
    public GameEventScriptBinaryFlags Flags { get; init; }
    public const uint HeaderSize = 16;
    public uint FileSize { get; init; } 
}




public readonly struct GameEventScriptBinaryExportTable
{
    private readonly GameEventScriptBinaryExportEntry[]? _entries;

    public GameEventScriptBinaryExportTable(IReadOnlyList<GameEventScriptBinaryExportEntry>? entries)
    {
        _entries = entries?.ToArray() ?? [];
        EntryCount = ToEntryCount(_entries.Length);
    }

    public ushort EntryCount { get; }

    public IReadOnlyList<GameEventScriptBinaryExportEntry> Entries => _entries ?? [];

    private static ushort ToEntryCount(int count) => count > ushort.MaxValue ? throw new ArgumentOutOfRangeException(nameof(count), "GameEventScriptBinary tables cannot exceed 65535 entries.") : checked((ushort)count);
}

public readonly struct GameEventScriptBinaryExportEntry(GameEventScriptBinaryExportKind kind, ushort name, IReadOnlyList<ushort>? argumentNames, uint entryAddress)
{
    private readonly ushort[]? _argumentNames = argumentNames?.ToArray() ?? [];

    public GameEventScriptBinaryExportKind Kind { get; } = kind;

    public ushort Name { get; } = name;

    public IReadOnlyList<ushort> ArgumentNames => _argumentNames ?? [];

    public uint EntryAddress { get; } = entryAddress;
}

public enum GameEventScriptBinaryExportKind : byte
{
    MessageHandler = 0x10,
    Function = 0x11,
    Predicate = 0x12
}





public readonly struct GameEventScriptBinaryImportTable
{
    private readonly GameEventScriptBinaryImportEntry[]? _entries;

    public GameEventScriptBinaryImportTable(IReadOnlyList<GameEventScriptBinaryImportEntry>? entries)
    {
        _entries = entries?.ToArray() ?? [];
        EntryCount = ToEntryCount(_entries.Length);
    }

    public ushort EntryCount { get; }

    public IReadOnlyList<GameEventScriptBinaryImportEntry> Entries => _entries ?? [];

    private static ushort ToEntryCount(int count) => count > ushort.MaxValue ? throw new ArgumentOutOfRangeException(nameof(count), "GameEventScriptBinary tables cannot exceed 65535 entries.") : checked((ushort)count);
}

public readonly struct GameEventScriptBinaryImportEntry(GameEventScriptBinaryImportKind kind, ushort name, IReadOnlyList<ushort>? argumentNames)
{
    private readonly ushort[]? _argumentNames = argumentNames?.ToArray() ?? [];

    public GameEventScriptBinaryImportKind Kind { get; } = kind;

    public ushort Name { get; } = name;

    public IReadOnlyList<ushort> ArgumentNames => _argumentNames ?? [];
}

public enum GameEventScriptBinaryImportKind : byte
{ 
    ExtensionCall = 0x20,
    ExternalType = 0x21
}
