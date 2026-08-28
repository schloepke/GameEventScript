#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections.Generic;
using System.Text;
using StepH.GameEventScript.Runtime.Values;

namespace StepH.GameEventScript.Api;

public interface IGameEventScriptExternalTypeRegistry
{
    IReadOnlyDictionary<string, GameEventScriptExternalTypeDefinition> Types { get; }

    IGameEventScriptExternalTypeConstructor? Resolve(GameEventScriptExternalTypeConstructorReference reference);
}

public interface IGameEventScriptExternalTypeConstructor
{
    GameEventScriptExternalTypeConstructorDefinition Definition { get; }

    void Invoke(GesExternalTypeConstructorCall call);
}

public sealed class GesExternalTypeConstructorCall
{
    private GesValueArguments _arguments = GesValueArguments.Empty;
    private Runtime.VM.GesVmState? _vmState;
    private ushort _destinationRegister;
    private GesValue _result;
    private bool _hasResult;

    public GesExternalTypeConstructorCall()
    {
    }

    public GesExternalTypeConstructorCall(GesValueArguments arguments)
    {
        _arguments = arguments;
    }

    public GesValueArguments Arguments => _arguments;

    public GesValue Result => _hasResult ? _result : GesValue.GesNothing();

    internal bool HasResult => _hasResult;

    internal void BeginCall(Runtime.VM.GesVmState vmState, ushort destinationRegister, GesValueArguments arguments)
    {
        _vmState = vmState ?? throw new ArgumentNullException(nameof(vmState));
        _destinationRegister = destinationRegister;
        _arguments = arguments;
        _result = default;
        _hasResult = false;
    }

    internal void EndCall()
    {
        _vmState = null;
        _destinationRegister = 0;
        _arguments = GesValueArguments.Empty;
        _result = default;
        _hasResult = false;
    }

    public void SetNothing() => SetValue(GesValue.GesNothing());

    public void SetValue(GesValue value)
    {
        _hasResult = true;
        _result = value;
        _vmState?.SetValue(_destinationRegister, in value);
    }

    public void SetBoolean(bool value)
    {
        _hasResult = true;
        _result = GesValue.GesBoolean(value);
        _vmState?.SetBoolean(_destinationRegister, value);
    }

    public void SetInteger(long value, GameEventScriptBytecodeInstructionUnit unit = GameEventScriptBytecodeInstructionUnit.UnitNone)
    {
        _hasResult = true;
        _result = GesValue.GesInteger(value, unit);
        _vmState?.SetInteger(_destinationRegister, value, unit);
    }

    public void SetFloat(double value, GameEventScriptBytecodeInstructionUnit unit = GameEventScriptBytecodeInstructionUnit.UnitNone)
    {
        _hasResult = true;
        _result = GesValue.GesFloat(value, unit);
        _vmState?.SetFloat(_destinationRegister, value, unit);
    }

    public void SetPercentage(double ratio)
    {
        _hasResult = true;
        _result = GesValue.GesPercentage(ratio);
        _vmState?.SetPercentage(_destinationRegister, ratio);
    }

    public void SetText(string text)
    {
        _hasResult = true;
        _result = GesValue.GesText(text);
        _vmState?.SetText(_destinationRegister, text);
    }

    public void SetTag(string tag)
    {
        _hasResult = true;
        _result = GesValue.GesTag(tag);
        _vmState?.SetTag(_destinationRegister, tag);
    }

    internal void SetExternalCustomType(Runtime.Values.GesExternalObject value)
    {
        _hasResult = true;
        _result = default;
        _result.SetExternalCustomType(value);
        _vmState?.SetExternalCustomType(_destinationRegister, value);
    }
}

public sealed class GameEventScriptExternalTypeDefinition
{
    public GameEventScriptExternalTypeDefinition(
        string name,
        IEnumerable<GameEventScriptExternalTypeFieldDefinition> fields,
        IEnumerable<GameEventScriptExternalTypeConstructorDefinition> constructors)
        : this(
            name,
            CopyFields(fields ?? throw new ArgumentNullException(nameof(fields))),
            CopyConstructors(constructors ?? throw new ArgumentNullException(nameof(constructors))),
            new Dictionary<string, Func<object, GesValue>>(StringComparer.Ordinal),
            new Dictionary<string, IGameEventScriptExternalTypeConstructor>(StringComparer.Ordinal))
    {
    }

    internal GameEventScriptExternalTypeDefinition(
        string name,
        IReadOnlyList<GameEventScriptExternalTypeFieldDefinition> fields,
        IReadOnlyList<GameEventScriptExternalTypeConstructorDefinition> constructors,
        IReadOnlyDictionary<string, Func<object, GesValue>> fieldReaders,
        IReadOnlyDictionary<string, IGameEventScriptExternalTypeConstructor> constructorBindings)
    {
        Name = GameEventScriptExternalTypeNames.NormalizeTypeName(name);
        Fields = fields ?? throw new ArgumentNullException(nameof(fields));
        Constructors = constructors ?? throw new ArgumentNullException(nameof(constructors));
        FieldReaders = fieldReaders ?? throw new ArgumentNullException(nameof(fieldReaders));
        ConstructorBindings = constructorBindings ?? throw new ArgumentNullException(nameof(constructorBindings));
    }

    public string Name { get; }

    public IReadOnlyList<GameEventScriptExternalTypeFieldDefinition> Fields { get; }

    public IReadOnlyList<GameEventScriptExternalTypeConstructorDefinition> Constructors { get; }

    internal IReadOnlyDictionary<string, Func<object, GesValue>> FieldReaders { get; }

    internal IReadOnlyDictionary<string, IGameEventScriptExternalTypeConstructor> ConstructorBindings { get; }

    internal bool HasConstructor(IReadOnlyCollection<string> argumentLabels)
    {
        var signatureId = GameEventScriptExternalTypeConstructorReference.CreateSignatureId(Name, argumentLabels);
        if (ConstructorBindings.ContainsKey(signatureId))
        {
            return true;
        }

        for (var index = 0; index < Constructors.Count; index++)
        {
            if (string.Equals(Constructors[index].SignatureId, signatureId, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    internal GesValue? GetField(string fieldName, object instance)
    {
        return FieldReaders.TryGetValue(fieldName, out var reader) ? reader(instance) : null;
    }

    private static GameEventScriptExternalTypeFieldDefinition[] CopyFields(IEnumerable<GameEventScriptExternalTypeFieldDefinition> fields)
    {
        if (fields is IReadOnlyCollection<GameEventScriptExternalTypeFieldDefinition> collection)
        {
            if (collection.Count == 0)
            {
                return [];
            }

            var result = new GameEventScriptExternalTypeFieldDefinition[collection.Count];
            var index = 0;
            foreach (var field in collection)
            {
                result[index++] = field;
            }

            return result;
        }

        var list = new List<GameEventScriptExternalTypeFieldDefinition>();
        foreach (var field in fields)
        {
            list.Add(field);
        }

        var values = new GameEventScriptExternalTypeFieldDefinition[list.Count];
        for (var index = 0; index < list.Count; index++)
        {
            values[index] = list[index];
        }

        return values;
    }

    private static GameEventScriptExternalTypeConstructorDefinition[] CopyConstructors(IEnumerable<GameEventScriptExternalTypeConstructorDefinition> constructors)
    {
        if (constructors is IReadOnlyCollection<GameEventScriptExternalTypeConstructorDefinition> collection)
        {
            if (collection.Count == 0)
            {
                return [];
            }

            var result = new GameEventScriptExternalTypeConstructorDefinition[collection.Count];
            var index = 0;
            foreach (var constructor in collection)
            {
                result[index++] = constructor;
            }

            return result;
        }

        var list = new List<GameEventScriptExternalTypeConstructorDefinition>();
        foreach (var constructor in constructors)
        {
            list.Add(constructor);
        }

        var values = new GameEventScriptExternalTypeConstructorDefinition[list.Count];
        for (var index = 0; index < list.Count; index++)
        {
            values[index] = list[index];
        }

        return values;
    }
}

public sealed class GameEventScriptExternalTypeFieldDefinition
{
    public GameEventScriptExternalTypeFieldDefinition(string name, string typeName)
    {
        Name = GameEventScriptExternalTypeNames.NormalizeIdentifier(name, nameof(name));
        TypeName = GameEventScriptExternalTypeNames.NormalizeTypeName(typeName);
        var (kind, unit) = GameEventScriptExternalTypeNames.GetKindAndUnit(TypeName);
        Kind = kind;
        Unit = unit.ToStoredUnit();
    }

    public GameEventScriptExternalTypeFieldDefinition(string name, GameEventScriptBytecodeTypeKind kind)
        : this(name, kind, unit: null)
    {
    }

    public GameEventScriptExternalTypeFieldDefinition(string name, GameEventScriptBytecodeTypeKind kind, GameEventScriptBytecodeInstructionUnit unit)
        : this(name, kind, (GameEventScriptBytecodeInstructionUnit?)unit)
    {
    }

    internal GameEventScriptExternalTypeFieldDefinition(string name, GameEventScriptBytecodeTypeKind? kind, GameEventScriptBytecodeInstructionUnit? unit)
    {
        Name = GameEventScriptExternalTypeNames.NormalizeIdentifier(name, nameof(name));
        TypeName = kind is { } resolvedKind
            ? GameEventScriptExternalTypeNames.ToTypeName(resolvedKind, unit)
            : throw new ArgumentException("External GameEventScript field kind must be specified.", nameof(kind));
        Kind = kind;
        Unit = unit.ToStoredUnit();
    }

    public string Name { get; }

    public string TypeName { get; }

    public GameEventScriptBytecodeTypeKind? Kind { get; }

    public GameEventScriptBytecodeInstructionUnit Unit { get; }
}

public sealed class GameEventScriptExternalTypeParameterDefinition
{
    public GameEventScriptExternalTypeParameterDefinition(string name, string typeName)
    {
        Name = GameEventScriptExternalTypeNames.NormalizeIdentifier(name, nameof(name));
        TypeName = GameEventScriptExternalTypeNames.NormalizeTypeName(typeName);
        var (kind, unit) = GameEventScriptExternalTypeNames.GetKindAndUnit(TypeName);
        Kind = kind;
        Unit = unit.ToStoredUnit();
    }

    public GameEventScriptExternalTypeParameterDefinition(string name, GameEventScriptBytecodeTypeKind kind)
        : this(name, kind, unit: null)
    {
    }

    public GameEventScriptExternalTypeParameterDefinition(string name, GameEventScriptBytecodeTypeKind kind, GameEventScriptBytecodeInstructionUnit unit)
        : this(name, kind, (GameEventScriptBytecodeInstructionUnit?)unit)
    {
    }

    internal GameEventScriptExternalTypeParameterDefinition(string name, GameEventScriptBytecodeTypeKind? kind, GameEventScriptBytecodeInstructionUnit? unit)
    {
        Name = GameEventScriptExternalTypeNames.NormalizeIdentifier(name, nameof(name));
        TypeName = kind is { } resolvedKind
            ? GameEventScriptExternalTypeNames.ToTypeName(resolvedKind, unit)
            : throw new ArgumentException("External GameEventScript parameter kind must be specified.", nameof(kind));
        Kind = kind;
        Unit = unit.ToStoredUnit();
    }

    public string Name { get; }

    public string TypeName { get; }

    public GameEventScriptBytecodeTypeKind? Kind { get; }

    public GameEventScriptBytecodeInstructionUnit Unit { get; }
}

public sealed class GameEventScriptExternalTypeConstructorDefinition
{
    public GameEventScriptExternalTypeConstructorDefinition(string typeName, IEnumerable<GameEventScriptExternalTypeParameterDefinition> parameters)
    {
        TypeName = GameEventScriptExternalTypeNames.NormalizeTypeName(typeName);
        Parameters = CopyParameters(parameters ?? throw new ArgumentNullException(nameof(parameters)));
        SignatureId = GameEventScriptExternalTypeConstructorReference.CreateSignatureId(TypeName, CreateParameterNameArray(Parameters));
    }

    public string TypeName { get; }

    public IReadOnlyList<GameEventScriptExternalTypeParameterDefinition> Parameters { get; }

    public string SignatureId { get; }

    private static GameEventScriptExternalTypeParameterDefinition[] CopyParameters(IEnumerable<GameEventScriptExternalTypeParameterDefinition> parameters)
    {
        if (parameters is IReadOnlyCollection<GameEventScriptExternalTypeParameterDefinition> collection)
        {
            if (collection.Count == 0)
            {
                return [];
            }

            var result = new GameEventScriptExternalTypeParameterDefinition[collection.Count];
            var index = 0;
            foreach (var parameter in collection)
            {
                result[index++] = parameter;
            }

            return result;
        }

        var list = new List<GameEventScriptExternalTypeParameterDefinition>();
        foreach (var parameter in parameters)
        {
            list.Add(parameter);
        }

        var values = new GameEventScriptExternalTypeParameterDefinition[list.Count];
        for (var index = 0; index < list.Count; index++)
        {
            values[index] = list[index];
        }

        return values;
    }

    private static string[] CreateParameterNameArray(IReadOnlyList<GameEventScriptExternalTypeParameterDefinition> parameters)
    {
        if (parameters.Count == 0)
        {
            return [];
        }

        var values = new string[parameters.Count];
        for (var index = 0; index < parameters.Count; index++)
        {
            values[index] = parameters[index].Name;
        }

        return values;
    }
}

public sealed class GameEventScriptExternalTypeConstructorReference
{
    public GameEventScriptExternalTypeConstructorReference(string typeName, IEnumerable<string?>? argumentLabels)
    {
        TypeName = GameEventScriptExternalTypeNames.NormalizeTypeName(typeName);
        ArgumentLabels = NormalizeAndSortLabels(argumentLabels);
        SignatureId = CreateSignatureId(TypeName, ArgumentLabels);
    }

    public string TypeName { get; }

    public IReadOnlyList<string> ArgumentLabels { get; }

    public string SignatureId { get; }

    public static string CreateSignatureId(string typeName, IEnumerable<string?>? argumentLabels)
    {
        var normalizedTypeName = GameEventScriptExternalTypeNames.NormalizeTypeName(typeName);
        var labels = NormalizeAndSortLabels(argumentLabels);
        var builder = new StringBuilder();
        builder.Append(normalizedTypeName).Append('(');
        for (var index = 0; index < labels.Length; index++)
        {
            if (index > 0)
            {
                builder.Append(',');
            }

            builder.Append(labels[index]);
        }

        return builder.Append(')').ToString();
    }

    private static string[] NormalizeAndSortLabels(IEnumerable<string?>? argumentLabels)
    {
        if (argumentLabels is null)
        {
            return [];
        }

        string[] values;
        if (argumentLabels is IReadOnlyCollection<string?> collection)
        {
            if (collection.Count == 0)
            {
                return [];
            }

            values = new string[collection.Count];
            var index = 0;
            foreach (var label in collection)
            {
                values[index++] = GameEventScriptExternalTypeNames.NormalizeIdentifier(label, nameof(argumentLabels));
            }
        }
        else
        {
            var list = new List<string>();
            foreach (var label in argumentLabels)
            {
                list.Add(GameEventScriptExternalTypeNames.NormalizeIdentifier(label, nameof(argumentLabels)));
            }

            if (list.Count == 0)
            {
                return [];
            }

            values = new string[list.Count];
            for (var index = 0; index < list.Count; index++)
            {
                values[index] = list[index];
            }
        }

        Array.Sort(values, StringComparer.Ordinal);
        return values;
    }
}

internal sealed class GameEventScriptEmptyExternalTypeRegistry : IGameEventScriptExternalTypeRegistry
{
    public static readonly GameEventScriptEmptyExternalTypeRegistry Instance = new();

    private GameEventScriptEmptyExternalTypeRegistry()
    {
    }

    public IReadOnlyDictionary<string, GameEventScriptExternalTypeDefinition> Types { get; } =
        new Dictionary<string, GameEventScriptExternalTypeDefinition>(StringComparer.Ordinal);

    public IGameEventScriptExternalTypeConstructor? Resolve(GameEventScriptExternalTypeConstructorReference reference) => null;
}
