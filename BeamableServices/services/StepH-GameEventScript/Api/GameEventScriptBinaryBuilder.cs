#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using static StepH.GameEventScript.Api.GameEventScriptBinaryBindTable;
using static StepH.GameEventScript.Api.GameEventScriptBinaryHeader;

namespace StepH.GameEventScript.Api;

public class GameEventScriptBinaryBuilder
{
    private ushort _version = 1;
    private string _moduleName = "Unknown";
    private GameEventScriptBinaryFlags _flags = GameEventScriptBinaryFlags.None;
    private uint _fileSize = 0;
    private List<string> _stringPool = [];
    private Dictionary<string, ushort> _stringPoolIndexes = [];
    private List<GameEventScriptBinaryBindEntry> _binds = [];

    private List<IReadOnlyList<ushort>> _uint16Table = [];
    private List<GameEventScriptBytecodeInstruction> _bytecodeInstructions = [];


    public GameEventScriptBinaryBuilder WithVersion(ushort version)
    {
        _version = version;
        return this;
    }

    public GameEventScriptBinaryBuilder WithModuleName(string moduleName)
    {
        _moduleName = string.IsNullOrWhiteSpace(moduleName) ? throw new ArgumentException("Module name must be non-empty.", nameof(moduleName)) : moduleName;
        return this;
    }

    public GameEventScriptBinaryBuilder WithFlag(GameEventScriptBinaryFlags flag, bool enabled = true)
    {
        _flags = enabled
            ? _flags | flag
            : _flags & ~flag;
        return this;
    }

    public GameEventScriptBinaryBuilder WithFileSize(uint fileSize)
    {
        _fileSize = fileSize;
        return this;
    }

    public GameEventScriptBinaryBuilder AddStringPoolElement(string stringPoolElement, out ushort index)
    {
        _ = stringPoolElement ?? throw new ArgumentNullException(nameof(stringPoolElement));
        if (_stringPoolIndexes.TryGetValue(stringPoolElement, out var existing))
        {
            index = existing;
        }
        else
        {
            index = (ushort)_stringPool.Count;
            _stringPool.Add(stringPoolElement);
            _stringPoolIndexes.Add(stringPoolElement, (ushort)index);
        }

        return this;
    }

    public GameEventScriptBinaryBuilder AddStringPoolElements(IReadOnlyList<string> values, out ushort[] indexes)
    {
        var result = new ushort[values.Count];
        for (var index = 0; index < values.Count; index++)
        {
            AddStringPoolElement(values[index], out result[index]);
        }

        indexes = result;
        return this;
    }

    public GameEventScriptBinaryBuilder AddUint16TableEntry(IReadOnlyList<ushort> value, out ushort index)
    {
        index = (ushort)_uint16Table.Count;
        _uint16Table.Add(value);
        return this;
    }

    public GameEventScriptBinaryBuilder AddUint16TableEntries(IReadOnlyList<IReadOnlyList<ushort>> values, out ushort[] indexes)
    {
        indexes = new ushort[values.Count];
        for (var index = 0; index < values.Count; index++)
        {
            AddUint16TableEntry(values[index], out indexes[index]);
        }
        return this;
    }

    public GameEventScriptBinaryBuilder AddBind(GameEventScriptBinaryBindEntry bind)
    {
        _binds.Add(bind);
        return this;
    }
    
    public GameEventScriptBinaryBuilder AddBytecodeInstruction(GameEventScriptBytecodeInstruction instruction)
    {
        _bytecodeInstructions.Add(instruction);
        return this;
    }
    
    public GameEventScriptBinaryBuilder AddBytecodeInstructions(IReadOnlyList<GameEventScriptBytecodeInstruction> instructions)
    {
        _bytecodeInstructions.AddRange(instructions);
        return this;
    }

    public GameEventScriptBinary Build()
    {
        return new GameEventScriptBinary(
            new GameEventScriptBinaryHeader { Version = _version, Flags = _flags, FileSize = _fileSize },
            _moduleName,
            BuildTextTable(_stringPool),
            BuildUInt16SliceTable(_uint16Table),
            new GameEventScriptBinaryBindTable(_binds),
            _bytecodeInstructions.ToArray()
        );
    }

    private static GameEventScriptUInt16Table BuildUInt16SliceTable(IReadOnlyList<IReadOnlyList<ushort>> uint16Table)
    {
        List<ushort> data = [];
        List<GameEventScriptUInt16Table.SliceEntry> slices = [];
        foreach (var slice in uint16Table)
        {
            slices.Add(new GameEventScriptUInt16Table.SliceEntry { Start = (ushort)data.Count, Length = (ushort)slice.Count });
            data.AddRange(slice);
        }
        return new GameEventScriptUInt16Table { Slices = slices.ToArray(), Data = data.ToArray() };
    }

    private static GameEventScriptTextTable BuildTextTable(List<string> stringTable)
    {
        List<byte> stringData = [];
        List<GameEventScriptTextTable.SliceEntry> stringIndexes = [];
        foreach (var byteSpan in stringTable.Select(stringPoolElement => Encoding.UTF8.GetBytes(stringPoolElement)))
        {
            stringIndexes.Add(new GameEventScriptTextTable.SliceEntry { Start = (ushort)stringData.Count, Length = (ushort)byteSpan.Length });
            stringData.AddRange(byteSpan);
        }

        return new GameEventScriptTextTable() { Slices = stringIndexes.ToArray(), Data = stringData.ToArray() };
    }
}
