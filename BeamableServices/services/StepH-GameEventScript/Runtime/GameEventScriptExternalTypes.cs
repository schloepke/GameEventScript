#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using StepH.GameEventScript.Api;
using StepH.GameEventScript.Extensions;
using StepH.GameEventScript.Types;
using static StepH.GameEventScript.Api.GameEventScriptValueFactory;

namespace StepH.GameEventScript.Runtime;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
public sealed class GesTypeAttribute(string typeName) : Attribute
{
    public string TypeName { get; } = typeName ?? throw new ArgumentNullException(nameof(typeName));
}

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public sealed class GesFieldAttribute : Attribute
{
    public GesFieldAttribute(string name, string typeName)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        TypeName = typeName ?? throw new ArgumentNullException(nameof(typeName));
    }

    public GesFieldAttribute(string name, GameEventScriptBytecodeTypeKind kind)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        TypeName = GameEventScriptExternalTypeNames.ToTypeName(kind, unit: null);
        Kind = kind;
    }

    public GesFieldAttribute(string name, GameEventScriptBytecodeTypeKind kind, GameEventScriptBytecodeInstructionUnit unit)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        TypeName = GameEventScriptExternalTypeNames.ToTypeName(kind, unit);
        Kind = kind;
        Unit = unit.ToStoredUnit();
    }

    public string Name { get; }

    public string TypeName { get; }

    public GameEventScriptBytecodeTypeKind? Kind { get; }

    public GameEventScriptBytecodeInstructionUnit Unit { get; }
}

[AttributeUsage(AttributeTargets.Constructor | AttributeTargets.Method)]
public sealed class GesConstructAttribute : Attribute;

[AttributeUsage(AttributeTargets.Parameter)]
public sealed class GesParamAttribute : Attribute
{
    public GesParamAttribute(string name, string typeName)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        TypeName = typeName ?? throw new ArgumentNullException(nameof(typeName));
    }

    public GesParamAttribute(string name, GameEventScriptBytecodeTypeKind kind)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        TypeName = GameEventScriptExternalTypeNames.ToTypeName(kind, unit: null);
        Kind = kind;
    }

    public GesParamAttribute(string name, GameEventScriptBytecodeTypeKind kind, GameEventScriptBytecodeInstructionUnit unit)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        TypeName = GameEventScriptExternalTypeNames.ToTypeName(kind, unit);
        Kind = kind;
        Unit = unit.ToStoredUnit();
    }

    public string Name { get; }

    public string TypeName { get; }

    public GameEventScriptBytecodeTypeKind? Kind { get; }

    public GameEventScriptBytecodeInstructionUnit Unit { get; }
}

public interface IGameEventScriptExternalTypeRegistry
{
    IReadOnlyDictionary<string, GameEventScriptExternalTypeDefinition> Types { get; }

    bool TryResolve(GameEventScriptExternalTypeConstructorReference reference, out IGameEventScriptExternalTypeConstructor constructor);
}

public interface IGameEventScriptExternalTypeConstructor
{
    GameEventScriptExternalTypeConstructorDefinition Definition { get; }

    GameEventScriptBoxedValue Invoke(ReadOnlySpan<GameEventScriptBoxedValue> arguments);
}

public sealed class GameEventScriptExternalTypeDefinition
{
    public GameEventScriptExternalTypeDefinition(
        string name,
        IEnumerable<GameEventScriptExternalTypeFieldDefinition> fields,
        IEnumerable<GameEventScriptExternalTypeConstructorDefinition> constructors)
        : this(
            name,
            (fields ?? throw new ArgumentNullException(nameof(fields))).ToArray(),
            (constructors ?? throw new ArgumentNullException(nameof(constructors))).ToArray(),
            new Dictionary<string, Func<object, GameEventScriptBoxedValue>>(StringComparer.Ordinal),
            new Dictionary<string, IGameEventScriptExternalTypeConstructor>(StringComparer.Ordinal))
    {
    }

    internal GameEventScriptExternalTypeDefinition(
        string name,
        IReadOnlyList<GameEventScriptExternalTypeFieldDefinition> fields,
        IReadOnlyList<GameEventScriptExternalTypeConstructorDefinition> constructors,
        IReadOnlyDictionary<string, Func<object, GameEventScriptBoxedValue>> fieldReaders,
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

    internal IReadOnlyDictionary<string, Func<object, GameEventScriptBoxedValue>> FieldReaders { get; }

    internal IReadOnlyDictionary<string, IGameEventScriptExternalTypeConstructor> ConstructorBindings { get; }

    internal bool TryGetConstructor(IReadOnlyCollection<string> argumentLabels, out IGameEventScriptExternalTypeConstructor constructor)
    {
        var signatureId = GameEventScriptExternalTypeConstructorReference.CreateSignatureId(Name, argumentLabels);
        if (ConstructorBindings.TryGetValue(signatureId, out constructor!))
        {
            return true;
        }

        constructor = default!;
        return Constructors.Any(constructorDefinition => string.Equals(constructorDefinition.SignatureId, signatureId, StringComparison.Ordinal));
    }

    internal bool TryGetField(string fieldName, object instance, out GameEventScriptBoxedValue value)
    {
        if (FieldReaders.TryGetValue(fieldName, out var reader))
        {
            value = reader(instance);
            return true;
        }

        value = GameEventScriptBoxedValue.Nothing();
        return false;
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

public sealed class GameEventScriptExternalTypeConstructorDefinition(string typeName, IEnumerable<GameEventScriptExternalTypeParameterDefinition> parameters)
{
    public string TypeName { get; } = GameEventScriptExternalTypeNames.NormalizeTypeName(typeName);

    public IReadOnlyList<GameEventScriptExternalTypeParameterDefinition> Parameters { get; } =
        (parameters ?? throw new ArgumentNullException(nameof(parameters))).ToArray();

    public string SignatureId => GameEventScriptExternalTypeConstructorReference.CreateSignatureId(TypeName, Parameters.Select(parameter => parameter.Name));
}

public sealed class GameEventScriptExternalTypeConstructorReference
{
    public GameEventScriptExternalTypeConstructorReference(string typeName, IEnumerable<string?>? argumentLabels)
    {
        TypeName = GameEventScriptExternalTypeNames.NormalizeTypeName(typeName);
        ArgumentLabels = (argumentLabels ?? Array.Empty<string?>())
            .Select(label => GameEventScriptExternalTypeNames.NormalizeIdentifier(label, nameof(argumentLabels)))
            .OrderBy(label => label, StringComparer.Ordinal)
            .ToArray();
        SignatureId = CreateSignatureId(TypeName, ArgumentLabels);
    }

    public string TypeName { get; }

    public IReadOnlyList<string> ArgumentLabels { get; }

    public string SignatureId { get; }

    public static string CreateSignatureId(string typeName, IEnumerable<string?>? argumentLabels)
    {
        var normalizedTypeName = GameEventScriptExternalTypeNames.NormalizeTypeName(typeName);
        var labels = (argumentLabels ?? Array.Empty<string?>())
            .Select(label => GameEventScriptExternalTypeNames.NormalizeIdentifier(label, nameof(argumentLabels)))
            .OrderBy(label => label, StringComparer.Ordinal);
        return $"{normalizedTypeName}({string.Join(",", labels)})";
    }
}

public sealed class GameEventScriptExternalTypeRegistry : IGameEventScriptExternalTypeRegistry
{
    private GameEventScriptExternalTypeRegistry(IReadOnlyDictionary<string, GameEventScriptExternalTypeDefinition> types)
    {
        Types = types ?? throw new ArgumentNullException(nameof(types));
    }

    public IReadOnlyDictionary<string, GameEventScriptExternalTypeDefinition> Types { get; }

    public static GameEventScriptExternalTypeRegistry Create(params Type[] types)
        => Create((IEnumerable<Type>)types);

    public static GameEventScriptExternalTypeRegistry Create(IEnumerable<Type> types)
    {
        _ = types ?? throw new ArgumentNullException(nameof(types));
        var definitions = new Dictionary<string, GameEventScriptExternalTypeDefinition>(StringComparer.Ordinal);
        foreach (var type in types)
        {
            var definition = BuildDefinition(type ?? throw new ArgumentException("External type list contains null.", nameof(types)));
            if (!definitions.TryAdd(definition.Name, definition))
            {
                throw new ArgumentException($"External GameEventScript type ':{definition.Name}' is registered more than once.", nameof(types));
            }
        }

        return new GameEventScriptExternalTypeRegistry(definitions);
    }

    public bool TryResolve(GameEventScriptExternalTypeConstructorReference reference, out IGameEventScriptExternalTypeConstructor constructor)
    {
        _ = reference ?? throw new ArgumentNullException(nameof(reference));
        if (Types.TryGetValue(reference.TypeName, out var typeDefinition) &&
            typeDefinition.ConstructorBindings.TryGetValue(reference.SignatureId, out constructor!))
        {
            return true;
        }

        constructor = default!;
        return false;
    }

    private static GameEventScriptExternalTypeDefinition BuildDefinition(Type clrType)
    {
        var typeAttribute = clrType.GetCustomAttribute<GesTypeAttribute>() ??
                            throw new ArgumentException($"CLR type '{clrType.FullName}' must declare GesTypeAttribute.", nameof(clrType));
        var typeName = GameEventScriptExternalTypeNames.NormalizeTypeName(typeAttribute.TypeName);
        var fields = BuildFields(clrType);
        var fieldReaders = fields.ToDictionary(
            field => field.Definition.Name,
            field => field.Reader,
            StringComparer.Ordinal);
        var constructors = BuildConstructors(clrType, typeName, fields.Select(field => field.Definition.Name).ToHashSet(StringComparer.Ordinal));

        var definition = new GameEventScriptExternalTypeDefinition(
            typeName,
            fields.Select(field => field.Definition).ToArray(),
            constructors.Select(constructor => constructor.Definition).ToArray(),
            fieldReaders,
            constructors.ToDictionary(constructor => constructor.Definition.SignatureId, constructor => (IGameEventScriptExternalTypeConstructor)constructor, StringComparer.Ordinal));
        GameEventScriptExternalTypeRuntime.Register(clrType, definition);
        return definition;
    }

    private static IReadOnlyList<ExternalFieldBinding> BuildFields(Type clrType)
    {
        var fields = new List<ExternalFieldBinding>();
        foreach (var property in clrType.GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            var attribute = property.GetCustomAttribute<GesFieldAttribute>();
            if (attribute is null)
            {
                continue;
            }

            if (!property.CanRead || property.GetIndexParameters().Length != 0)
            {
                throw new ArgumentException($"External GameEventScript field '{property.Name}' on '{clrType.FullName}' must be a readable non-indexer property.");
            }

            if (property.PropertyType == typeof(GameEventScriptValue) || property.PropertyType.IsSubclassOf(typeof(GameEventScriptValue)))
            {
                throw new ArgumentException($"External GameEventScript field '{property.Name}' on '{clrType.FullName}' cannot use boxed GameEventScriptValue types. Use GameEventScriptBoxedValue instead.");
            }

            fields.Add(CreateFieldBinding(attribute, instance => property.GetValue(instance)));
        }

        foreach (var field in clrType.GetFields(BindingFlags.Instance | BindingFlags.Public))
        {
            var attribute = field.GetCustomAttribute<GesFieldAttribute>();
            if (attribute is null)
            {
                continue;
            }

            if (field.FieldType == typeof(GameEventScriptValue) || field.FieldType.IsSubclassOf(typeof(GameEventScriptValue)))
            {
                throw new ArgumentException($"External GameEventScript field '{field.Name}' on '{clrType.FullName}' cannot use boxed GameEventScriptValue types. Use GameEventScriptBoxedValue instead.");
            }

            fields.Add(CreateFieldBinding(attribute, instance => field.GetValue(instance)));
        }

        var duplicates = fields
            .GroupBy(field => field.Definition.Name, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicates is not null)
        {
            throw new ArgumentException($"External GameEventScript type '{clrType.FullName}' declares field '{duplicates.Key}' more than once.");
        }

        return fields;
    }

    private static ExternalFieldBinding CreateFieldBinding(GesFieldAttribute attribute, Func<object, object?> reader)
    {
        var definition = attribute.Kind is { } kind
            ? new GameEventScriptExternalTypeFieldDefinition(attribute.Name, kind, attribute.Unit)
            : new GameEventScriptExternalTypeFieldDefinition(attribute.Name, attribute.TypeName);
        return new ExternalFieldBinding(
            definition,
            instance => GameEventScriptExternalTypeValueConverter.CoerceToDeclaredType(GameEventScriptExternalTypeValueConverter.ToBoxedValue(reader(instance)), definition));
    }

    private static IReadOnlyList<ReflectionExternalTypeConstructor> BuildConstructors(Type clrType, string typeName, ISet<string> fieldNames)
    {
        var constructors = new List<ReflectionExternalTypeConstructor>();
        foreach (var constructor in clrType.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            if (constructor.GetCustomAttribute<GesConstructAttribute>() is null)
            {
                continue;
            }

            constructors.Add(BuildConstructorBinding(typeName, fieldNames, constructor, values => constructor.Invoke(values)));
        }

        foreach (var method in clrType.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
        {
            if (method.GetCustomAttribute<GesConstructAttribute>() is null)
            {
                continue;
            }

            if (method.ReturnType == typeof(void))
            {
                throw new ArgumentException($"External GameEventScript constructor '{clrType.FullName}.{method.Name}' must return a value.");
            }

            if (method.ReturnType == typeof(GameEventScriptValue) || method.ReturnType.IsSubclassOf(typeof(GameEventScriptValue)))
            {
                throw new ArgumentException($"External GameEventScript constructor '{clrType.FullName}.{method.Name}' cannot return boxed GameEventScriptValue types. Use GameEventScriptBoxedValue instead.");
            }

            constructors.Add(BuildConstructorBinding(typeName, fieldNames, method, values => method.Invoke(null, values)));
        }

        var duplicates = constructors
            .GroupBy(constructor => constructor.Definition.SignatureId, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicates is not null)
        {
            throw new ArgumentException($"External GameEventScript type ':{typeName}' declares constructor '{duplicates.Key}' more than once.");
        }

        return constructors;
    }

    private static ReflectionExternalTypeConstructor BuildConstructorBinding(
        string typeName,
        ISet<string> fieldNames,
        MethodBase method,
        Func<object?[], object?> invoke)
    {
        var parameters = method.GetParameters()
            .Select(parameter => BuildParameterDefinition(method, fieldNames, parameter))
            .ToArray();
        return new ReflectionExternalTypeConstructor(
            new GameEventScriptExternalTypeConstructorDefinition(typeName, parameters.Select(parameter => parameter.Definition)),
            parameters,
            values =>
            {
                var result = invoke(values);
                return result;
            });
    }

    private static ExternalParameterBinding BuildParameterDefinition(MethodBase method, ISet<string> fieldNames, ParameterInfo parameter)
    {
        var attribute = parameter.GetCustomAttribute<GesParamAttribute>() ??
                        throw new ArgumentException($"External GameEventScript constructor '{method.DeclaringType?.FullName}.{method.Name}' parameter '{parameter.Name}' must declare GesParamAttribute.");
        if (parameter.ParameterType == typeof(GameEventScriptValue) || parameter.ParameterType.IsSubclassOf(typeof(GameEventScriptValue)))
        {
            throw new ArgumentException(
                $"External GameEventScript constructor '{method.DeclaringType?.FullName}.{method.Name}' parameter '{parameter.Name}' cannot use boxed GameEventScriptValue types. Use GameEventScriptBoxedValue instead.");
        }

        var definition = attribute.Kind is { } kind
            ? new GameEventScriptExternalTypeParameterDefinition(attribute.Name, kind, attribute.Unit)
            : new GameEventScriptExternalTypeParameterDefinition(attribute.Name, attribute.TypeName);
        if (!fieldNames.Contains(definition.Name))
        {
            throw new ArgumentException($"External GameEventScript constructor '{method.DeclaringType?.FullName}.{method.Name}' parameter '{definition.Name}' is not a declared field.");
        }

        return new ExternalParameterBinding(definition, parameter.ParameterType);
    }

    private sealed record ExternalFieldBinding(GameEventScriptExternalTypeFieldDefinition Definition, Func<object, GameEventScriptBoxedValue> Reader);

    private sealed record ExternalParameterBinding(GameEventScriptExternalTypeParameterDefinition Definition, Type ClrType);

    private sealed class ReflectionExternalTypeConstructor(
        GameEventScriptExternalTypeConstructorDefinition definition,
        IReadOnlyList<ExternalParameterBinding> parameters,
        Func<object?[], object?> invoke) : IGameEventScriptExternalTypeConstructor
    {
        public GameEventScriptExternalTypeConstructorDefinition Definition { get; } = definition;

        public GameEventScriptBoxedValue Invoke(ReadOnlySpan<GameEventScriptBoxedValue> arguments)
        {
            if (arguments.Length != parameters.Count)
            {
                return GameEventScriptBoxedValue.Nothing();
            }

            var converted = new object?[arguments.Length];
            for (var index = 0; index < converted.Length; index++)
            {
                converted[index] = GameEventScriptExternalTypeValueConverter.ToClrValue(arguments[index], parameters[index].ClrType);
            }

            var result = invoke(converted);
            if (result is null) return GameEventScriptBoxedValue.Nothing();
            if (result is GameEventScriptBoxedValue boxedValue) return boxedValue;
            if (result is GameEventScriptValue)
            {
                throw new InvalidOperationException($"External GameEventScript constructor '{Definition.SignatureId}' returned a boxed GameEventScriptValue. Use GameEventScriptBoxedValue instead.");
            }

            if (GameEventScriptExternalTypeRuntime.TryGetDefinitionForInstance(result, out var externalDefinition))
            {
                return GameEventScriptBoxedValue.FromExternalObject(result, externalDefinition);
            }

            return GameEventScriptExternalTypeValueConverter.ToBoxedValue(result);
        }
    }
}

public sealed class GameEventScriptEmptyExternalTypeRegistry : IGameEventScriptExternalTypeRegistry
{
    public static readonly GameEventScriptEmptyExternalTypeRegistry Instance = new();

    private GameEventScriptEmptyExternalTypeRegistry()
    {
    }

    public IReadOnlyDictionary<string, GameEventScriptExternalTypeDefinition> Types { get; } =
        new Dictionary<string, GameEventScriptExternalTypeDefinition>(StringComparer.Ordinal);

    public bool TryResolve(GameEventScriptExternalTypeConstructorReference reference, out IGameEventScriptExternalTypeConstructor constructor)
    {
        constructor = default!;
        return false;
    }
}

internal static class GameEventScriptExternalTypeRuntime
{
    private static readonly Dictionary<Type, GameEventScriptExternalTypeDefinition> DefinitionsByClrType = new();
    private static readonly object Gate = new();

    public static void Register(Type clrType, GameEventScriptExternalTypeDefinition definition)
    {
        lock (Gate)
        {
            DefinitionsByClrType[clrType] = definition;
        }
    }

    public static bool TryGetDefinitionForInstance(object instance, out GameEventScriptExternalTypeDefinition definition)
    {
        var clrType = instance.GetType();
        lock (Gate)
        {
            return DefinitionsByClrType.TryGetValue(clrType, out definition!);
        }
    }
}

internal static class GameEventScriptExternalTypeNames
{
    public static string ToTypeName(GameEventScriptBytecodeTypeKind kind, GameEventScriptBytecodeInstructionUnit? unit)
    {
        unit = unit is { } numericUnit && numericUnit.IsNumericUnit() ? numericUnit : null;

        if (unit is not null && kind is not (GameEventScriptBytecodeTypeKind.Float or GameEventScriptBytecodeTypeKind.Vector or GameEventScriptBytecodeTypeKind.Point))
        {
            throw new ArgumentException($"External GameEventScript type '{kind}' cannot declare a numeric unit.", nameof(unit));
        }

        return kind switch
        {
            GameEventScriptBytecodeTypeKind.Nothing => "nothing",
            GameEventScriptBytecodeTypeKind.Tag => "tag",
            GameEventScriptBytecodeTypeKind.Text => "text",
            GameEventScriptBytecodeTypeKind.Percentage => "percentage",
            GameEventScriptBytecodeTypeKind.Vector => "vector",
            GameEventScriptBytecodeTypeKind.Point => "point",
            GameEventScriptBytecodeTypeKind.Float => unit?.ToTypeName() ?? "number",
            GameEventScriptBytecodeTypeKind.Boolean => "boolean",
            GameEventScriptBytecodeTypeKind.Series => "series",
            GameEventScriptBytecodeTypeKind.Range => "range",
            GameEventScriptBytecodeTypeKind.Handler => "handler",
            GameEventScriptBytecodeTypeKind.List => "list",
            GameEventScriptBytecodeTypeKind.Map => "map",
            GameEventScriptBytecodeTypeKind.Dice => "dice",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown GameEventScript value kind.")
        };
    }

    public static (GameEventScriptBytecodeTypeKind? Kind, GameEventScriptBytecodeInstructionUnit? Unit) GetKindAndUnit(string typeName)
    {
        if (GameEventScriptBytecodeInstructionUnits.TryParseTypeName(typeName, out var unit))
        {
            return (GameEventScriptBytecodeTypeKind.Float, unit);
        }

        return typeName switch
        {
            "nothing" => (GameEventScriptBytecodeTypeKind.Nothing, null),
            "tag" => (GameEventScriptBytecodeTypeKind.Tag, null),
            "text" => (GameEventScriptBytecodeTypeKind.Text, null),
            "percentage" => (GameEventScriptBytecodeTypeKind.Percentage, null),
            "vector" => (GameEventScriptBytecodeTypeKind.Vector, null),
            "point" => (GameEventScriptBytecodeTypeKind.Point, null),
            "number" => (GameEventScriptBytecodeTypeKind.Float, null),
            "boolean" => (GameEventScriptBytecodeTypeKind.Boolean, null),
            "series" => (GameEventScriptBytecodeTypeKind.Series, null),
            "range" => (GameEventScriptBytecodeTypeKind.Range, null),
            "handler" => (GameEventScriptBytecodeTypeKind.Handler, null),
            "list" => (GameEventScriptBytecodeTypeKind.List, null),
            "map" => (GameEventScriptBytecodeTypeKind.Map, null),
            "dice" => (GameEventScriptBytecodeTypeKind.Dice, null),
            _ => (null, null)
        };
    }

    public static string NormalizeTypeName(string? typeName)
    {
        var normalized = NormalizeName(typeName, "typeName");
        if (normalized.StartsWith(":", StringComparison.Ordinal))
        {
            normalized = normalized[1..];
        }

        if (!IsLetterOnlyLowerStart(normalized))
        {
            throw new ArgumentException($"External GameEventScript type name ':{normalized}' must start with a lower-case letter and contain letters only.");
        }

        return normalized;
    }

    public static string NormalizeIdentifier(string? name, string parameterName)
    {
        var normalized = NormalizeName(name, parameterName);
        if (!IsIdentifier(normalized))
        {
            throw new ArgumentException($"External GameEventScript identifier '{normalized}' must start with a lower-case letter, contain letters only, and may end with _<index>.");
        }

        return normalized;
    }

    private static string NormalizeName(string? name, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Name must not be null or whitespace.", parameterName);
        }

        return name.Trim();
    }

    private static bool IsLetterOnlyLowerStart(string value)
        => value.Length > 0 &&
           char.IsLower(value[0]) &&
           value.All(char.IsLetter);

    private static bool IsIdentifier(string value)
    {
        if (value.Length == 0 || !char.IsLower(value[0]))
        {
            return false;
        }

        var underscore = value.LastIndexOf('_');
        if (underscore < 0)
        {
            return value.All(char.IsLetter);
        }

        if (underscore == 0 || underscore == value.Length - 1)
        {
            return false;
        }

        var prefix = value[..underscore];
        var suffix = value[(underscore + 1)..];
        if (!prefix.All(char.IsLetter) || !suffix.All(char.IsDigit))
        {
            return false;
        }

        return suffix.Length == 1 || suffix[0] != '0';
    }
}

internal static class GameEventScriptExternalTypeValueConverter
{
    public static GameEventScriptBoxedValue CoerceToDeclaredType(GameEventScriptBoxedValue value, GameEventScriptExternalTypeFieldDefinition definition)
        => CoerceToDeclaredType(value, definition.Kind, definition.Unit, definition.TypeName);

    public static GameEventScriptBoxedValue CoerceToDeclaredType(GameEventScriptBoxedValue value, GameEventScriptExternalTypeParameterDefinition definition)
        => CoerceToDeclaredType(value, definition.Kind, definition.Unit, definition.TypeName);

    public static GameEventScriptBoxedValue CoerceToDeclaredType(GameEventScriptBoxedValue value, string typeName)
    {
        var (kind, unit) = GameEventScriptExternalTypeNames.GetKindAndUnit(typeName);
        return CoerceToDeclaredType(value, kind, unit, typeName);
    }

    private static GameEventScriptBoxedValue CoerceToDeclaredType(
        GameEventScriptBoxedValue value,
        GameEventScriptBytecodeTypeKind? kind,
        GameEventScriptBytecodeInstructionUnit? unit,
        string typeName)
    {
        if (kind == GameEventScriptBytecodeTypeKind.Vector &&
            unit is not null)
        {
            return GameEventScriptBoxedValue.FromVector(value.X, value.Y, value.Z, unit.ToStoredUnit());
        }

        if (kind == GameEventScriptBytecodeTypeKind.Point &&
            unit is not null)
        {
            return GameEventScriptBoxedValue.FromPoint(value.X, value.Y, value.Z, unit.ToStoredUnit());
        }

        if (kind == GameEventScriptBytecodeTypeKind.Float &&
            unit is not null)
        {
            return GameEventScriptBoxedValue.FromFloat(value.AsNumber(), unit.ToStoredUnit());
        }

        return CoerceToDeclaredTypeCore(value, typeName);
    }

    private static GameEventScriptBoxedValue CoerceToDeclaredTypeCore(GameEventScriptBoxedValue value, string typeName)
    {
        return typeName switch
        {
            "nothing" => GameEventScriptBoxedValue.Nothing(),
            "tag" => CoerceToTag(value),
            "text" => GameEventScriptBoxedValue.FromText(value.AsText()),
            "percentage" => GameEventScriptBoxedValue.FromPercentage(value.AsNumber()),
            "degree" => GameEventScriptBoxedValue.FromFloat(value.AsNumber(), GameEventScriptBytecodeInstructionUnit.UnitDegree),
            "meter" => GameEventScriptBoxedValue.FromFloat(value.AsNumber(), GameEventScriptBytecodeInstructionUnit.UnitMeter),
            "second" => GameEventScriptBoxedValue.FromFloat(value.AsNumber(), GameEventScriptBytecodeInstructionUnit.UnitSecond),
            "number" => GameEventScriptBoxedValue.FromFloat(value.AsNumber()),
            "boolean" => GameEventScriptBoxedValue.FromBoolean(value.AsBoolean()),
            "map" => GameEventScriptBoxedValue.FromMap(value.AsMap()),
            "list" => GameEventScriptBoxedValue.FromList(value.AsList()),
            _ => value
        };
    }

    private static GameEventScriptBoxedValue CoerceToTag(GameEventScriptBoxedValue value)
    {
        if (value.Kind == GameEventScriptBytecodeTypeKind.Boolean)
        {
            return GameEventScriptBoxedValue.FromTag(value.AsBoolean() ? "true" : "false");
        }

        var text = value.AsText();
        if (value.Kind == GameEventScriptBytecodeTypeKind.Text)
        {
            return GameEventScriptTagRules.TryNormalizeTextCast(text, out var normalized)
                ? GameEventScriptBoxedValue.FromTag(normalized)
                : GameEventScriptBoxedValue.Nothing();
        }

        return GameEventScriptTagRules.IsValidTagName(text)
            ? GameEventScriptBoxedValue.FromTag(text)
            : GameEventScriptBoxedValue.Nothing();
    }

    public static GameEventScriptBoxedValue ToBoxedValue(object? value)
    {
        switch (value)
        {
            case null:
                return GameEventScriptBoxedValue.Nothing();
            case GameEventScriptBoxedValue boxed:
                return boxed;
            case GameEventScriptValue:
                throw new InvalidOperationException("External GameEventScript reflection cannot materialize boxed GameEventScriptValue values. Use GameEventScriptBoxedValue instead.");
            case bool boolean:
                return GameEventScriptBoxedValue.FromBoolean(boolean);
            case string text:
                return GameEventScriptBoxedValue.FromText(text);
            case double number:
                return GameEventScriptBoxedValue.FromFloat(number);
            case float number:
                return GameEventScriptBoxedValue.FromFloat(number);
            case decimal number:
                return GameEventScriptBoxedValue.FromFloat((double)number);
            case long integer:
                return GameEventScriptBoxedValue.FromInteger(integer);
            case int integer:
                return GameEventScriptBoxedValue.FromInteger(integer);
            case short integer:
                return GameEventScriptBoxedValue.FromInteger(integer);
            case byte integer:
                return GameEventScriptBoxedValue.FromInteger(integer);
            case GameEventScriptMessage message:
                return GameEventScriptBoxedValue.FromMessage(message);
            case GameEventScriptMessageSignature signature:
                return GameEventScriptBoxedValue.FromHandler(signature);
        }

        if (GameEventScriptExternalTypeRuntime.TryGetDefinitionForInstance(value, out var externalDefinition))
        {
            return GameEventScriptBoxedValue.FromExternalObject(value, externalDefinition);
        }

        throw new InvalidOperationException($"Cannot convert CLR value of type '{value.GetType().FullName}' to GameEventScriptBoxedValue.");
    }

    public static object? ToClrValue(GameEventScriptBoxedValue value, Type targetType)
    {
        if (targetType == typeof(GameEventScriptValue) || targetType.IsSubclassOf(typeof(GameEventScriptValue)))
        {
            throw new InvalidOperationException($"Cannot convert GameEventScriptBoxedValue to boxed GameEventScriptValue CLR type '{targetType.FullName}'. Use GameEventScriptBoxedValue instead.");
        }

        if (targetType == typeof(GameEventScriptBoxedValue))
        {
            return value;
        }

        if (targetType == typeof(string))
        {
            return value.AsText();
        }

        if (targetType == typeof(bool))
        {
            return value.AsBoolean();
        }

        if (targetType == typeof(double))
        {
            return value.AsNumber();
        }

        if (targetType == typeof(long))
        {
            return value.AsInteger();
        }

        if (targetType == typeof(int))
        {
            return checked((int)value.AsInteger());
        }

        if (targetType == typeof(short))
        {
            return checked((short)value.AsInteger());
        }

        if (targetType == typeof(byte))
        {
            return checked((byte)value.AsInteger());
        }

        if (value.TryGetExternalObject(targetType, out var externalObject))
        {
            return externalObject;
        }

        if (targetType == typeof(object))
        {
            return value;
        }

        throw new InvalidOperationException($"Cannot convert GameEventScript value kind '{value.Kind}' to CLR type '{targetType.FullName}'.");
    }
}
