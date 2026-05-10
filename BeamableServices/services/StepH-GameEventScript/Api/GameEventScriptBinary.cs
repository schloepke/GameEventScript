#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace StepH.GameEventScript.Api;

public readonly struct GameEventScriptBinary
{
    public readonly GameEventScriptBinaryHeader Header { get; init; }
    public readonly string ModuleName { get; init; }
    public readonly GameEventScriptTextTable StringTable { get; init; }
    public readonly GameEventScriptUInt16Table UInt16SliceTable { get; init; }
    public readonly GameEventScriptBinaryBindTable BindTable { get; init; }
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

public readonly struct GameEventScriptTextTable
{
    public readonly struct SliceEntry
    {
        public ushort Start { get; init; }
        public ushort Length { get; init; }
    }
    
    public SliceEntry[] Slices { get; init; }
    public byte[] Data { get; init; }
    
    public string Resolve(ushort index) => Encoding.UTF8.GetString(Data.AsSpan(Slices[index].Start, Slices[index].Length));
}


public readonly struct GameEventScriptUInt16Table
{
    public readonly struct SliceEntry
    {
        public ushort Start { get; init; }
        public ushort Length { get; init; }
    }
    
    public SliceEntry[] Slices { get; init; }
    public ushort[] Data { get; init; }
    
    public ReadOnlySpan<ushort> Resolve(ushort index) => Data.AsSpan(Slices[index].Start, Slices[index].Length);
}





public readonly struct GameEventScriptBinaryBindTable
{
    private readonly GameEventScriptBinaryBindEntry[]? _entries;

    public GameEventScriptBinaryBindTable(IReadOnlyList<GameEventScriptBinaryBindEntry>? entries)
    {
        _entries = entries?.ToArray() ?? [];
        EntryCount = ToEntryCount(_entries.Length);
    }

    public ushort EntryCount { get; }

    public IReadOnlyList<GameEventScriptBinaryBindEntry> Entries => _entries ?? [];

    private static ushort ToEntryCount(int count) => count > ushort.MaxValue ? throw new ArgumentOutOfRangeException(nameof(count), "GameEventScriptBinary tables cannot exceed 65535 entries.") : checked((ushort)count);
}

public readonly struct GameEventScriptBinaryBindEntry(GameEventScriptBinaryBindKind kind, ushort name, IReadOnlyList<ushort>? argumentNames, uint entryAddress = 0)
{
    private readonly ushort[]? _argumentNames = argumentNames?.ToArray() ?? [];

    public GameEventScriptBinaryBindKind Kind { get; } = kind;

    public ushort Name { get; } = name;

    public IReadOnlyList<ushort> ArgumentNames => _argumentNames ?? [];

    public uint EntryAddress { get; } = entryAddress;
}

public enum GameEventScriptBinaryBindKind : byte
{
    MessageHandler = 0x10,
    Function = 0x11,
    Predicate = 0x12,
    ExtensionCall = 0x20,
    ExternalType = 0x21
}
