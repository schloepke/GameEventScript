using System;
using System.Collections.Generic;
using System.Linq;
using static StepH.GameEventScript.Api.GameEventScriptBinaryHeader;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

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

    public GameEventScriptBinaryBuilder WithVersion(ushort version)
    {
        _version = version;
        return this;
    }

    public GameEventScriptBinaryBuilder WithModuleName(string moduleName)
    {
        _moduleName = string.IsNullOrWhiteSpace(moduleName) ? throw new ArgumentException("Module name must be non-empty.", nameof(moduleName)) : moduleName;
        ;
        return this;
    }

    public GameEventScriptBinaryBuilder WithFlag(GameEventScriptBinaryFlags flag, bool enabled = true)
    {
        _flags |= (_flags & ~flag) | (enabled ? flag : 0);
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

    public GameEventScriptBinaryBuilder AddBind(GameEventScriptBinaryBindEntry bind)
    {
        _binds.Add(bind);
        return this;
    }

    public GameEventScriptBinary Build()
    {
        return new GameEventScriptBinary
        {
            Header = new GameEventScriptBinaryHeader { Version = 1, Flags = _flags, FileSize = _fileSize },
            ModuleName = _moduleName,
            StringPool = _stringPool.ToArray(),
            BindTable = new GameEventScriptBinaryBindTable(_binds)
        };
    }
}

public static class GameEventScriptBinaryExtensions
{
    public static GameEventScriptBinary ToGameEventScriptBinary(this GameEventScriptCompiled compiled)
    {
        _ = compiled ?? throw new ArgumentNullException(nameof(compiled));
        var builder = new GameEventScriptBinaryBuilder();

        builder.WithVersion(1).WithModuleName(compiled.ModuleName).AddStringPoolElements(compiled.StringPool, out _);

        foreach (var handler in compiled.Handlers.OrderBy(pair => pair.Key, StringComparer.Ordinal).SelectMany(pair => pair.Value.OrderBy(handler => handler.DeclarationOrder)))
        {
            builder
                .AddStringPoolElement(handler.Message, out var messageIndex)
                .AddStringPoolElements(handler.SignatureLabels, out var argumentIndexes)
                .AddBind(new GameEventScriptBinaryBindEntry(GameEventScriptBinaryBindKind.MessageHandler, messageIndex, argumentIndexes,
                    handler.EntryAddress < 0 ? throw new InvalidOperationException("GameEventScriptBinary exports require non-negative entry addresses.") : checked((uint)handler.EntryAddress)));
        }

        foreach (var callable in compiled.Callables.Values.OrderBy(callable => callable.Name, StringComparer.Ordinal))
        {
            builder.AddStringPoolElement(callable.Name, out var messageIndex);
            builder.AddStringPoolElements(callable.SignatureLabels, out var argumentIndexes);
            builder.AddBind(new GameEventScriptBinaryBindEntry(callable.Kind == GameEventScriptBytecodeCallableKind.Predicate ? GameEventScriptBinaryBindKind.Predicate : GameEventScriptBinaryBindKind.Function, messageIndex, argumentIndexes,
                callable.EntryAddress < 0 ? throw new InvalidOperationException("GameEventScriptBinary exports require non-negative entry addresses.") : checked((uint)callable.EntryAddress)));
        }

        foreach (var reference in compiled.ExternalReferences.OrderBy(reference => reference.SignatureId, StringComparer.Ordinal))
        {
            builder.AddStringPoolElement(reference.ExtensionName + "." + reference.FunctionName, out var functionIndex);
            builder.AddStringPoolElements(reference.ArgumentLabels, out var argumentIndexes);
            builder.AddBind(new GameEventScriptBinaryBindEntry(GameEventScriptBinaryBindKind.ExtensionCall, functionIndex, argumentIndexes));
        }

        foreach (var reference in compiled.ExternalTypeConstructorReferences.OrderBy(reference => reference.SignatureId, StringComparer.Ordinal))
        {
            builder.AddStringPoolElement(reference.TypeName, out var typeNameIndex);
            builder.AddStringPoolElements(reference.ArgumentLabels, out var argumentIndexes);
            builder.AddBind(new GameEventScriptBinaryBindEntry(GameEventScriptBinaryBindKind.ExternalType, typeNameIndex, argumentIndexes));
        }

        return builder.Build();
    }
}
