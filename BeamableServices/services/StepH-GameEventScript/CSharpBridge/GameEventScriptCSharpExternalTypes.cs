#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using StepH.GameEventScript.Api;
using StepH.GameEventScript.Runtime;
using StepH.GameEventScript.Runtime.Values;

namespace StepH.GameEventScript.CSharpBridge;

public static class GameEventScriptCSharpExternalTypes
{
    public static IGameEventScriptExternalTypeRegistry CreateRegistry(params Type[] types)
        => CreateRegistry((IEnumerable<Type>)types);

    public static IGameEventScriptExternalTypeRegistry CreateRegistry(IEnumerable<Type> types)
        => GameEventScriptCSharpExternalTypeRegistry.Create(types);
}

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

internal sealed class GameEventScriptCSharpExternalTypeRegistry : IGameEventScriptExternalTypeRegistry
{
    private GameEventScriptCSharpExternalTypeRegistry(IReadOnlyDictionary<string, GameEventScriptExternalTypeDefinition> types)
    {
        Types = types ?? throw new ArgumentNullException(nameof(types));
    }

    public IReadOnlyDictionary<string, GameEventScriptExternalTypeDefinition> Types { get; }

    public static GameEventScriptCSharpExternalTypeRegistry Create(params Type[] types)
        => Create((IEnumerable<Type>)types);

    public static GameEventScriptCSharpExternalTypeRegistry Create(IEnumerable<Type> types)
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

        return new GameEventScriptCSharpExternalTypeRegistry(definitions);
    }

    public IGameEventScriptExternalTypeConstructor? Resolve(GameEventScriptExternalTypeConstructorReference reference)
    {
        _ = reference ?? throw new ArgumentNullException(nameof(reference));
        if (Types.TryGetValue(reference.TypeName, out var typeDefinition) &&
            typeDefinition.ConstructorBindings.TryGetValue(reference.SignatureId, out var constructor))
        {
            return constructor;
        }

        return null;
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
        GameEventScriptCSharpExternalTypeRuntime.Register(clrType, definition);
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
            instance => GameEventScriptExternalTypeValueConverter.CoerceToDeclaredType(GameEventScriptCSharpExternalTypeValueConverter.ToValue(reader(instance)), definition));
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
                return GameEventScriptValueFactory.GesNothing();
            }

            var converted = new object?[arguments.Length];
            for (var index = 0; index < converted.Length; index++)
            {
                converted[index] = GameEventScriptCSharpExternalTypeValueConverter.ToClrValue(arguments[index], parameters[index].ClrType);
            }

            var result = invoke(converted);
            if (result is null) return GameEventScriptValueFactory.GesNothing();
            if (result is GameEventScriptValue scriptValue) return scriptValue;

            if (GameEventScriptCSharpExternalTypeRuntime.TryGetDefinitionForInstance(result, out var externalDefinition))
            {
                return GameEventScriptValueFactory.GesExternalObject(result, externalDefinition);
            }

            return GameEventScriptCSharpExternalTypeValueConverter.ToValue(result);
        }
    }
}

internal static class GameEventScriptCSharpExternalTypeRuntime
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

internal static class GameEventScriptCSharpExternalTypeValueConverter
{
    public static GameEventScriptValue ToValue(object? value)
    {
        switch (value)
        {
            case null:
                return GameEventScriptValueFactory.GesNothing();
            case GameEventScriptValue scriptValue:
                return scriptValue;
            case bool boolean:
                return GameEventScriptValueFactory.GesBoolean(boolean);
            case string text:
                return GameEventScriptValueFactory.GesText(text);
            case double number:
                return GameEventScriptValueFactory.GesFloat(number);
            case float number:
                return GameEventScriptValueFactory.GesFloat(number);
            case decimal number:
                return GameEventScriptValueFactory.GesFloat((double)number);
            case long integer:
                return GameEventScriptValueFactory.GesInteger(integer);
            case int integer:
                return GameEventScriptValueFactory.GesInteger(integer);
            case short integer:
                return GameEventScriptValueFactory.GesInteger(integer);
            case byte integer:
                return GameEventScriptValueFactory.GesInteger(integer);
            case GameEventScriptMessage message:
                return GameEventScriptValueFactory.GesMessage(message);
            case GameEventScriptMessageSignature signature:
                return GameEventScriptValueFactory.GesHandler(signature);
        }

        if (GameEventScriptCSharpExternalTypeRuntime.TryGetDefinitionForInstance(value, out var externalDefinition))
        {
            return GameEventScriptValueFactory.GesExternalObject(value, externalDefinition);
        }

        throw new InvalidOperationException($"Cannot convert CLR value of type '{value.GetType().FullName}' to GameEventScriptValue.");
    }

    public static object? ToClrValue(GameEventScriptValue value, Type targetType)
    {
        if (targetType == typeof(GameEventScriptValue))
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

        var externalObject = ToClrExternalObjectValue(value, targetType);
        if (externalObject is not null)
        {
            return externalObject;
        }

        if (targetType == typeof(object))
        {
            return value;
        }

        throw new InvalidOperationException($"Cannot convert GameEventScript value kind '{value.Kind}' to CLR type '{targetType.FullName}'.");
    }

    public static object? ToClrExternalObjectValue(GameEventScriptValue value, Type objectType)
    {
        _ = objectType ?? throw new ArgumentNullException(nameof(objectType));
        if (value.GetVmValue().ObjectValue is GesExternalObject externalObject && objectType.IsInstanceOfType(externalObject.Instance)) return externalObject.Instance;
        return null;
    }
}