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

    public GesFieldAttribute(string name, GameEventScriptValueKind kind)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        TypeName = GameEventScriptExternalTypeNames.ToTypeName(kind, unit: null);
        Kind = kind;
    }

    public GesFieldAttribute(string name, GameEventScriptValueKind kind, GameEventScriptNumericUnit unit)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        TypeName = GameEventScriptExternalTypeNames.ToTypeName(kind, unit);
        Kind = kind;
        Unit = unit;
    }

    public string Name { get; }

    public string TypeName { get; }

    public GameEventScriptValueKind? Kind { get; }

    public GameEventScriptNumericUnit? Unit { get; }
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

    public GesParamAttribute(string name, GameEventScriptValueKind kind)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        TypeName = GameEventScriptExternalTypeNames.ToTypeName(kind, unit: null);
        Kind = kind;
    }

    public GesParamAttribute(string name, GameEventScriptValueKind kind, GameEventScriptNumericUnit unit)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        TypeName = GameEventScriptExternalTypeNames.ToTypeName(kind, unit);
        Kind = kind;
        Unit = unit;
    }

    public string Name { get; }

    public string TypeName { get; }

    public GameEventScriptValueKind? Kind { get; }

    public GameEventScriptNumericUnit? Unit { get; }
}

public interface IGameEventScriptExternalTypeRegistry
{
    IReadOnlyDictionary<string, GameEventScriptExternalTypeDefinition> Types { get; }

    bool TryResolve(GameEventScriptExternalTypeConstructorReference reference, out IGameEventScriptExternalTypeConstructor constructor);
}

public interface IGameEventScriptExternalTypeConstructor
{
    GameEventScriptExternalTypeConstructorDefinition Definition { get; }

    GameEventScriptValue Invoke(ReadOnlySpan<GameEventScriptValue> arguments);
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
            new Dictionary<string, Func<object, GameEventScriptValue>>(StringComparer.Ordinal),
            new Dictionary<string, IGameEventScriptExternalTypeConstructor>(StringComparer.Ordinal))
    {
    }

    internal GameEventScriptExternalTypeDefinition(
        string name,
        IReadOnlyList<GameEventScriptExternalTypeFieldDefinition> fields,
        IReadOnlyList<GameEventScriptExternalTypeConstructorDefinition> constructors,
        IReadOnlyDictionary<string, Func<object, GameEventScriptValue>> fieldReaders,
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

    internal IReadOnlyDictionary<string, Func<object, GameEventScriptValue>> FieldReaders { get; }

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

    internal bool TryGetField(string fieldName, object instance, out GameEventScriptValue value)
    {
        if (FieldReaders.TryGetValue(fieldName, out var reader))
        {
            value = reader(instance);
            return true;
        }

        value = GameEventScriptNothingValue.Instance;
        return false;
    }
}

public sealed class GameEventScriptExternalTypeFieldDefinition
{
    public GameEventScriptExternalTypeFieldDefinition(string name, string typeName)
    {
        Name = GameEventScriptExternalTypeNames.NormalizeIdentifier(name, nameof(name));
        TypeName = GameEventScriptExternalTypeNames.NormalizeTypeName(typeName);
        (Kind, Unit) = GameEventScriptExternalTypeNames.GetKindAndUnit(TypeName);
    }

    public GameEventScriptExternalTypeFieldDefinition(string name, GameEventScriptValueKind kind)
        : this(name, kind, unit: null)
    {
    }

    public GameEventScriptExternalTypeFieldDefinition(string name, GameEventScriptValueKind kind, GameEventScriptNumericUnit unit)
        : this(name, kind, (GameEventScriptNumericUnit?)unit)
    {
    }

    internal GameEventScriptExternalTypeFieldDefinition(string name, GameEventScriptValueKind? kind, GameEventScriptNumericUnit? unit)
    {
        Name = GameEventScriptExternalTypeNames.NormalizeIdentifier(name, nameof(name));
        TypeName = kind is { } resolvedKind
            ? GameEventScriptExternalTypeNames.ToTypeName(resolvedKind, unit)
            : throw new ArgumentException("External GameEventScript field kind must be specified.", nameof(kind));
        Kind = kind;
        Unit = unit;
    }

    public string Name { get; }

    public string TypeName { get; }

    public GameEventScriptValueKind? Kind { get; }

    public GameEventScriptNumericUnit? Unit { get; }
}

public sealed class GameEventScriptExternalTypeParameterDefinition
{
    public GameEventScriptExternalTypeParameterDefinition(string name, string typeName)
    {
        Name = GameEventScriptExternalTypeNames.NormalizeIdentifier(name, nameof(name));
        TypeName = GameEventScriptExternalTypeNames.NormalizeTypeName(typeName);
        (Kind, Unit) = GameEventScriptExternalTypeNames.GetKindAndUnit(TypeName);
    }

    public GameEventScriptExternalTypeParameterDefinition(string name, GameEventScriptValueKind kind)
        : this(name, kind, unit: null)
    {
    }

    public GameEventScriptExternalTypeParameterDefinition(string name, GameEventScriptValueKind kind, GameEventScriptNumericUnit unit)
        : this(name, kind, (GameEventScriptNumericUnit?)unit)
    {
    }

    internal GameEventScriptExternalTypeParameterDefinition(string name, GameEventScriptValueKind? kind, GameEventScriptNumericUnit? unit)
    {
        Name = GameEventScriptExternalTypeNames.NormalizeIdentifier(name, nameof(name));
        TypeName = kind is { } resolvedKind
            ? GameEventScriptExternalTypeNames.ToTypeName(resolvedKind, unit)
            : throw new ArgumentException("External GameEventScript parameter kind must be specified.", nameof(kind));
        Kind = kind;
        Unit = unit;
    }

    public string Name { get; }

    public string TypeName { get; }

    public GameEventScriptValueKind? Kind { get; }

    public GameEventScriptNumericUnit? Unit { get; }
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

            fields.Add(CreateFieldBinding(attribute, instance => property.GetValue(instance)));
        }

        foreach (var field in clrType.GetFields(BindingFlags.Instance | BindingFlags.Public))
        {
            var attribute = field.GetCustomAttribute<GesFieldAttribute>();
            if (attribute is null)
            {
                continue;
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
            instance => GameEventScriptExternalTypeValueConverter.CoerceToDeclaredType(reader(instance).ToGameEventScriptValue(), definition));
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
        var definition = attribute.Kind is { } kind
            ? new GameEventScriptExternalTypeParameterDefinition(attribute.Name, kind, attribute.Unit)
            : new GameEventScriptExternalTypeParameterDefinition(attribute.Name, attribute.TypeName);
        if (!fieldNames.Contains(definition.Name))
        {
            throw new ArgumentException($"External GameEventScript constructor '{method.DeclaringType?.FullName}.{method.Name}' parameter '{definition.Name}' is not a declared field.");
        }

        return new ExternalParameterBinding(definition, parameter.ParameterType);
    }

    private sealed record ExternalFieldBinding(GameEventScriptExternalTypeFieldDefinition Definition, Func<object, GameEventScriptValue> Reader);

    private sealed record ExternalParameterBinding(GameEventScriptExternalTypeParameterDefinition Definition, Type ClrType);

    private sealed class ReflectionExternalTypeConstructor(
        GameEventScriptExternalTypeConstructorDefinition definition,
        IReadOnlyList<ExternalParameterBinding> parameters,
        Func<object?[], object?> invoke) : IGameEventScriptExternalTypeConstructor
    {
        public GameEventScriptExternalTypeConstructorDefinition Definition { get; } = definition;

        public GameEventScriptValue Invoke(ReadOnlySpan<GameEventScriptValue> arguments)
        {
            if (arguments.Length != parameters.Count)
            {
                return GameEventScriptNothingValue.Instance;
            }

            var converted = new object?[arguments.Length];
            for (var index = 0; index < converted.Length; index++)
            {
                converted[index] = GameEventScriptExternalTypeValueConverter.ToClrValue(arguments[index], parameters[index].ClrType);
            }

            var result = invoke(converted);
            if (result is null)
            {
                return GameEventScriptNothingValue.Instance;
            }

            if (result is GameEventScriptValue scriptValue)
            {
                return scriptValue;
            }

            if (GameEventScriptExternalTypeRuntime.TryGetDefinitionForInstance(result, out var externalDefinition))
            {
                return new GameEventScriptExternalObjectValue(result, externalDefinition);
            }

            return result.ToGameEventScriptValue();
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
    public static string ToTypeName(GameEventScriptValueKind kind, GameEventScriptNumericUnit? unit)
    {
        if (unit is not null && kind is not (GameEventScriptValueKind.Integer or GameEventScriptValueKind.Float or GameEventScriptValueKind.Vector or GameEventScriptValueKind.Point))
        {
            throw new ArgumentException($"External GameEventScript type '{kind}' cannot declare a numeric unit.", nameof(unit));
        }

        return kind switch
        {
            GameEventScriptValueKind.Nothing => "nothing",
            GameEventScriptValueKind.Tag => "tag",
            GameEventScriptValueKind.Text => "text",
            GameEventScriptValueKind.Percentage => "percentage",
            GameEventScriptValueKind.Vector => "vector",
            GameEventScriptValueKind.Point => "point",
            GameEventScriptValueKind.Float => unit?.ToTypeName() ?? "float",
            GameEventScriptValueKind.Integer => unit?.ToTypeName() ?? "integer",
            GameEventScriptValueKind.Boolean => "boolean",
            GameEventScriptValueKind.Uuid => "uuid",
            GameEventScriptValueKind.Series => "series",
            GameEventScriptValueKind.Range => "range",
            GameEventScriptValueKind.Message => "message",
            GameEventScriptValueKind.Handler => "handler",
            GameEventScriptValueKind.Ref => "ref",
            GameEventScriptValueKind.List => "list",
            GameEventScriptValueKind.Map => "map",
            GameEventScriptValueKind.Dice => "dice",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown GameEventScript value kind.")
        };
    }

    public static (GameEventScriptValueKind? Kind, GameEventScriptNumericUnit? Unit) GetKindAndUnit(string typeName)
    {
        if (GameEventScriptNumericUnits.TryParseTypeName(typeName, out var unit))
        {
            return (GameEventScriptValueKind.Float, unit);
        }

        return typeName switch
        {
            "nothing" => (GameEventScriptValueKind.Nothing, null),
            "tag" => (GameEventScriptValueKind.Tag, null),
            "text" => (GameEventScriptValueKind.Text, null),
            "percentage" => (GameEventScriptValueKind.Percentage, null),
            "vector" => (GameEventScriptValueKind.Vector, null),
            "point" => (GameEventScriptValueKind.Point, null),
            "float" or "number" => (GameEventScriptValueKind.Float, null),
            "integer" => (GameEventScriptValueKind.Integer, null),
            "boolean" => (GameEventScriptValueKind.Boolean, null),
            "uuid" => (GameEventScriptValueKind.Uuid, null),
            "series" => (GameEventScriptValueKind.Series, null),
            "range" => (GameEventScriptValueKind.Range, null),
            "message" => (GameEventScriptValueKind.Message, null),
            "handler" => (GameEventScriptValueKind.Handler, null),
            "ref" => (GameEventScriptValueKind.Ref, null),
            "list" => (GameEventScriptValueKind.List, null),
            "map" => (GameEventScriptValueKind.Map, null),
            "dice" => (GameEventScriptValueKind.Dice, null),
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
    public static GameEventScriptValue CoerceToDeclaredType(GameEventScriptValue value, GameEventScriptExternalTypeFieldDefinition definition)
        => CoerceToDeclaredType(value, definition.Kind, definition.Unit, definition.TypeName);

    public static GameEventScriptValue CoerceToDeclaredType(GameEventScriptValue value, GameEventScriptExternalTypeParameterDefinition definition)
        => CoerceToDeclaredType(value, definition.Kind, definition.Unit, definition.TypeName);

    public static GameEventScriptValue CoerceToDeclaredType(GameEventScriptValue value, string typeName)
    {
        var (kind, unit) = GameEventScriptExternalTypeNames.GetKindAndUnit(typeName);
        return CoerceToDeclaredType(value, kind, unit, typeName);
    }

    private static GameEventScriptValue CoerceToDeclaredType(
        GameEventScriptValue value,
        GameEventScriptValueKind? kind,
        GameEventScriptNumericUnit? unit,
        string typeName)
    {
        if (kind == GameEventScriptValueKind.Vector &&
            unit is not null &&
            value is GameEventScriptVectorValue vector)
        {
            return GesVector(vector.X, vector.Y, vector.Z, unit);
        }

        if (kind == GameEventScriptValueKind.Point &&
            unit is not null &&
            value is GameEventScriptPointValue point)
        {
            return GesPoint(point.X, point.Y, point.Z, unit);
        }

        if (kind == GameEventScriptValueKind.Float &&
            unit is not null)
        {
            return GesFloat(value.AsNumber(), unit);
        }

        if (kind == GameEventScriptValueKind.Integer &&
            unit is not null)
        {
            return GesInteger(value.AsInteger(), unit);
        }

        return CoerceToDeclaredTypeCore(value, typeName);
    }

    private static GameEventScriptValue CoerceToDeclaredTypeCore(GameEventScriptValue value, string typeName)
    {
        if (value.IsUuid() && typeName is not "uuid" and not "text")
        {
            return GesNothing();
        }

        return typeName switch
        {
            "nothing" => GesNothing(),
            "tag" => GesTag(value.AsText()),
            "text" => GesText(GesValueOperations.ToText(value)),
            "percentage" => GesPercentage(value.AsNumber()),
            "degree" => GesDegree(value.AsNumber()),
            "meter" => GesMeter(value.AsNumber()),
            "second" => GesSeconds(value.AsNumber()),
            "integer" => GesInteger(value.AsInteger()),
            "float" or "number" => GesFloat(value.AsNumber()),
            "boolean" => GesBoolean(value.AsBoolean()),
            "uuid" => value.IsUuid()
                ? value
                : GameEventScriptUuidValue.TryParse(GesValueOperations.ToText(value), out var uuid)
                    ? uuid
                    : GesNothing(),
            "ref" => value.IsRef() ? value : GesNothing(),
            "map" => GesMap(value.AsMap()),
            "list" => GesList(value.AsList()),
            _ => value
        };
    }

    public static object? ToClrValue(GameEventScriptValue value, Type targetType)
    {
        if (targetType == typeof(GameEventScriptValue) || targetType.IsAssignableFrom(value.GetType()))
        {
            return value;
        }

        if (targetType == typeof(GameEventScriptFastValue))
        {
            return GameEventScriptFastValue.FromGameEventScriptValue(value);
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

        if (targetType == typeof(double))
        {
            return (double)value.AsNumber();
        }

        if (targetType == typeof(double))
        {
            return (double)value.AsNumber();
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

        if (targetType == typeof(GameEventScriptVectorValue) && value is GameEventScriptVectorValue vector)
        {
            return vector;
        }

        if (targetType == typeof(GameEventScriptPointValue) && value is GameEventScriptPointValue point)
        {
            return point;
        }

        if (targetType == typeof(object))
        {
            return value;
        }

        throw new InvalidOperationException($"Cannot convert GameEventScript value '{value.DescribeType()}' to CLR type '{targetType.FullName}'.");
    }
}
