// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using StepH.GameEventScript.Api;
using StepH.GameEventScript.Runtime;
using StepH.GameEventScript.Runtime.Values;

namespace StepH.GameEventScript.CSharpBridge;

/// <summary>
/// Represents a game event script c sharp external types.
/// </summary>
public static class GameEventScriptCSharpExternalTypes
{
    /// <summary>
    /// Creates a registry.
    /// </summary>
    /// <param name="types">The types value.</param>
    /// <returns>The result of the operation.</returns>
    public static GameEventScriptCSharpExternalTypeRegistry CreateRegistry(params Type[] types)
        => CreateRegistry((IEnumerable<Type>)types);

    /// <summary>
    /// Creates a registry.
    /// </summary>
    /// <param name="types">The types value.</param>
    /// <returns>The result of the operation.</returns>
    public static GameEventScriptCSharpExternalTypeRegistry CreateRegistry(IEnumerable<Type> types)
        => GameEventScriptCSharpExternalTypeRegistry.Create(types);
}

/// <summary>
/// Represents a ges type attribute.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
public sealed class GesTypeAttribute(string typeName) : Attribute
{
    /// <summary>
    /// Gets the type name.
    /// </summary>
    public string TypeName { get; } = typeName ?? throw new ArgumentNullException(nameof(typeName));
}

/// <summary>
/// Represents a ges field attribute.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public sealed class GesFieldAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of Ges Field Attribute.
    /// </summary>
    /// <param name="name">The name value.</param>
    /// <param name="typeName">The type name value.</param>
    public GesFieldAttribute(string name, string typeName)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        TypeName = typeName ?? throw new ArgumentNullException(nameof(typeName));
    }

    /// <summary>
    /// Initializes a new instance of Ges Field Attribute.
    /// </summary>
    /// <param name="name">The name value.</param>
    /// <param name="kind">The kind value.</param>
    public GesFieldAttribute(string name, GameEventScriptBytecodeTypeKind kind)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        TypeName = GameEventScriptExternalTypeNames.ToTypeName(kind, unit: null);
        Kind = kind;
    }

    /// <summary>
    /// Initializes a new instance of Ges Field Attribute.
    /// </summary>
    /// <param name="name">The name value.</param>
    /// <param name="kind">The kind value.</param>
    /// <param name="unit">The unit value.</param>
    public GesFieldAttribute(string name, GameEventScriptBytecodeTypeKind kind, GameEventScriptBytecodeInstructionUnit unit)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        TypeName = GameEventScriptExternalTypeNames.ToTypeName(kind, unit);
        Kind = kind;
        Unit = unit.ToStoredUnit();
    }

    /// <summary>
    /// Gets the name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the type name.
    /// </summary>
    public string TypeName { get; }

    /// <summary>
    /// Gets the kind.
    /// </summary>
    public GameEventScriptBytecodeTypeKind? Kind { get; }

    /// <summary>
    /// Gets the unit.
    /// </summary>
    public GameEventScriptBytecodeInstructionUnit Unit { get; }
}

/// <summary>
/// Represents a ges construct attribute.
/// </summary>
[AttributeUsage(AttributeTargets.Constructor | AttributeTargets.Method)]
public sealed class GesConstructAttribute : Attribute;

/// <summary>
/// Represents a ges param attribute.
/// </summary>
[AttributeUsage(AttributeTargets.Parameter)]
public sealed class GesParamAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of Ges Param Attribute.
    /// </summary>
    /// <param name="name">The name value.</param>
    /// <param name="typeName">The type name value.</param>
    public GesParamAttribute(string name, string typeName)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        TypeName = typeName ?? throw new ArgumentNullException(nameof(typeName));
    }

    /// <summary>
    /// Initializes a new instance of Ges Param Attribute.
    /// </summary>
    /// <param name="name">The name value.</param>
    /// <param name="kind">The kind value.</param>
    public GesParamAttribute(string name, GameEventScriptBytecodeTypeKind kind)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        TypeName = GameEventScriptExternalTypeNames.ToTypeName(kind, unit: null);
        Kind = kind;
    }

    /// <summary>
    /// Initializes a new instance of Ges Param Attribute.
    /// </summary>
    /// <param name="name">The name value.</param>
    /// <param name="kind">The kind value.</param>
    /// <param name="unit">The unit value.</param>
    public GesParamAttribute(string name, GameEventScriptBytecodeTypeKind kind, GameEventScriptBytecodeInstructionUnit unit)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        TypeName = GameEventScriptExternalTypeNames.ToTypeName(kind, unit);
        Kind = kind;
        Unit = unit.ToStoredUnit();
    }

    /// <summary>
    /// Gets the name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the type name.
    /// </summary>
    public string TypeName { get; }

    /// <summary>
    /// Gets the kind.
    /// </summary>
    public GameEventScriptBytecodeTypeKind? Kind { get; }

    /// <summary>
    /// Gets the unit.
    /// </summary>
    public GameEventScriptBytecodeInstructionUnit Unit { get; }
}

/// <summary>
/// Represents a game event script c sharp external type registry.
/// </summary>
public sealed class GameEventScriptCSharpExternalTypeRegistry : IGameEventScriptExternalTypeCatalog, IGameEventScriptExternalTypeRegistry
{
    private readonly Dictionary<string, GameEventScriptExternalTypeDefinition> _typesByName = new(StringComparer.Ordinal);
    private readonly Dictionary<string, IGameEventScriptExternalTypeConstructor> _constructors = new(StringComparer.Ordinal);
    private readonly Dictionary<Type, GameEventScriptCSharpExternalTypeBinding> _bindingsByClrType = new();
    private IReadOnlyList<GameEventScriptExternalTypeDefinition> _types = Array.Empty<GameEventScriptExternalTypeDefinition>();

    private GameEventScriptCSharpExternalTypeRegistry()
    {
    }

    /// <summary>
    /// Gets the types.
    /// </summary>
    public IReadOnlyList<GameEventScriptExternalTypeDefinition> Types => _types;

    internal static GameEventScriptCSharpExternalTypeRegistry Create(params Type[] types)
        => Create((IEnumerable<Type>)types);

    internal static GameEventScriptCSharpExternalTypeRegistry Create(IEnumerable<Type> types)
    {
        _ = types ?? throw new ArgumentNullException(nameof(types));
        var registry = new GameEventScriptCSharpExternalTypeRegistry();
        foreach (var type in types)
        {
            registry.RegisterType(type ?? throw new ArgumentException("External type list contains null.", nameof(types)));
        }

        registry._types = Array.AsReadOnly(registry._typesByName.Values.ToArray());
        return registry;
    }

    /// <summary>
    /// Resolves the value.
    /// </summary>
    /// <param name="typeName">The type name value.</param>
    /// <returns>The result of the operation.</returns>
    public GameEventScriptExternalTypeDefinition? Resolve(string typeName)
    {
        var normalized = GameEventScriptExternalTypeNames.NormalizeTypeName(typeName);
        return _typesByName.TryGetValue(normalized, out var definition) ? definition : null;
    }

    /// <summary>
    /// Resolves the value.
    /// </summary>
    /// <param name="reference">The reference value.</param>
    /// <returns>The result of the operation.</returns>
    public IGameEventScriptExternalTypeConstructor? Resolve(GameEventScriptExternalTypeConstructorReference reference)
    {
        _ = reference ?? throw new ArgumentNullException(nameof(reference));
        return _constructors.TryGetValue(reference.SignatureId, out var constructor) ? constructor : null;
    }

    internal GesValue ToValue(object? value)
    {
        if (value is not null && CreateExternalValue(value) is { } externalValue)
        {
            var result = new GesValue();
            result.SetExternalType(externalValue);
            return result;
        }

        return GameEventScriptCSharpExternalTypeValueConverter.ToValue(value);
    }

    internal IGameEventScriptExternalValue? CreateExternalValue(object value)
        => _bindingsByClrType.TryGetValue(value.GetType(), out var binding)
            ? new GameEventScriptCSharpExternalValue(value, binding, this)
            : null;

    private void RegisterType(Type clrType)
    {
        var typeAttribute = clrType.GetCustomAttribute<GesTypeAttribute>() ??
                            throw new ArgumentException($"CLR type '{clrType.FullName}' must declare GesTypeAttribute.", nameof(clrType));
        var typeName = GameEventScriptExternalTypeNames.NormalizeTypeName(typeAttribute.TypeName);
        var fields = BuildFields(clrType);
        var fieldReaders = fields.ToDictionary(
            field => field.Definition.Name,
            field => field,
            StringComparer.Ordinal);
        var constructors = BuildConstructors(clrType, typeName, fields.Select(field => field.Definition.Name).ToHashSet(StringComparer.Ordinal), this);

        var definition = new GameEventScriptExternalTypeDefinition(
            typeName,
            fields.Select(field => field.Definition).ToArray(),
            constructors.Select(constructor => constructor.Definition).ToArray());
        if (!_typesByName.TryAdd(definition.Name, definition))
            throw new ArgumentException($"External GameEventScript type ':{definition.Name}' is registered more than once.", nameof(clrType));
        if (!_bindingsByClrType.TryAdd(clrType, new GameEventScriptCSharpExternalTypeBinding(definition, fieldReaders)))
            throw new ArgumentException($"CLR type '{clrType.FullName}' is registered more than once.", nameof(clrType));
        for (var index = 0; index < constructors.Count; index++)
        {
            var constructor = constructors[index];
            if (!_constructors.TryAdd(constructor.Definition.SignatureId, constructor))
                throw new ArgumentException($"External GameEventScript constructor ':{constructor.Definition.SignatureId}' is registered more than once.", nameof(clrType));
        }
    }

    private static IReadOnlyList<GameEventScriptCSharpExternalFieldBinding> BuildFields(Type clrType)
    {
        var fields = new List<GameEventScriptCSharpExternalFieldBinding>();
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

    private static GameEventScriptCSharpExternalFieldBinding CreateFieldBinding(GesFieldAttribute attribute, Func<object, object?> reader)
    {
        var definition = attribute.Kind is { } kind
            ? new GameEventScriptExternalTypeFieldDefinition(attribute.Name, kind, attribute.Unit)
            : new GameEventScriptExternalTypeFieldDefinition(attribute.Name, attribute.TypeName);
        return new GameEventScriptCSharpExternalFieldBinding(definition, reader);
    }

    private static IReadOnlyList<ReflectionExternalTypeConstructor> BuildConstructors(Type clrType, string typeName, ISet<string> fieldNames, GameEventScriptCSharpExternalTypeRegistry registry)
    {
        var constructors = new List<ReflectionExternalTypeConstructor>();
        foreach (var constructor in clrType.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            if (constructor.GetCustomAttribute<GesConstructAttribute>() is null)
            {
                continue;
            }

            constructors.Add(BuildConstructorBinding(typeName, fieldNames, constructor, values => constructor.Invoke(values), registry));
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

            if (!clrType.IsAssignableFrom(method.ReturnType))
            {
                throw new ArgumentException($"External GameEventScript constructor '{clrType.FullName}.{method.Name}' must return '{clrType.FullName}'.");
            }

            constructors.Add(BuildConstructorBinding(typeName, fieldNames, method, values => method.Invoke(null, values), registry));
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

    private static ReflectionExternalTypeConstructor BuildConstructorBinding(string typeName, ISet<string> fieldNames, MethodBase method, Func<object?[], object?> invoke, GameEventScriptCSharpExternalTypeRegistry registry)
    {
        var parameters = method.GetParameters()
            .Select(parameter => BuildParameterDefinition(method, fieldNames, parameter))
            .ToArray();
        return new ReflectionExternalTypeConstructor(
            new GameEventScriptExternalTypeConstructorDefinition(typeName, parameters.Select(parameter => parameter.Definition)),
            parameters,
            invoke,
            registry);
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

    private sealed record ExternalParameterBinding(GameEventScriptExternalTypeParameterDefinition Definition, Type ClrType);

    private sealed class ReflectionExternalTypeConstructor(
        GameEventScriptExternalTypeConstructorDefinition definition,
        IReadOnlyList<ExternalParameterBinding> parameters,
        Func<object?[], object?> invoke,
        GameEventScriptCSharpExternalTypeRegistry registry) : IGameEventScriptExternalTypeConstructor
    {
        public GameEventScriptExternalTypeConstructorDefinition Definition { get; } = definition;

        public void Invoke(GesExternalTypeConstructorCall call)
        {
            var arguments = call.Arguments;
            if (arguments.Length != parameters.Count)
            {
                call.SetNothing();
                return;
            }

            var converted = new object?[arguments.Length];
            for (var index = 0; index < converted.Length; index++)
            {
                converted[index] = GameEventScriptCSharpExternalTypeValueConverter.ToClrValue(arguments, index, parameters[index].ClrType);
            }

            var result = invoke(converted);
            if (result is null)
            {
                call.SetNothing();
                return;
            }

            var externalValue = registry.CreateExternalValue(result);
            if (externalValue is null)
            {
                call.SetNothing();
                return;
            }

            call.SetExternalValue(externalValue);
        }
    }
}

internal sealed record GameEventScriptCSharpExternalFieldBinding(GameEventScriptExternalTypeFieldDefinition Definition, Func<object, object?> Reader);

internal sealed class GameEventScriptCSharpExternalTypeBinding(GameEventScriptExternalTypeDefinition definition, IReadOnlyDictionary<string, GameEventScriptCSharpExternalFieldBinding> fields)
{
    internal GameEventScriptExternalTypeDefinition Definition { get; } = definition;
    internal IReadOnlyDictionary<string, GameEventScriptCSharpExternalFieldBinding> Fields { get; } = fields;
}

internal sealed class GameEventScriptCSharpExternalValue(object instance, GameEventScriptCSharpExternalTypeBinding binding, GameEventScriptCSharpExternalTypeRegistry registry) : IGameEventScriptExternalValue
{
    internal object Instance { get; } = instance;

    public GameEventScriptExternalTypeDefinition Definition => binding.Definition;

    public GesValue? GetField(string fieldName)
    {
        if (!binding.Fields.TryGetValue(fieldName, out var field)) return null;
        var value = registry.ToValue(field.Reader(Instance));
        return GameEventScriptExternalTypeValueConverter.CoerceToDeclaredType(in value, field.Definition);
    }
}

internal static class GameEventScriptCSharpExternalTypeValueConverter
{
    public static GesValue ToValue(object? value)
    {
        switch (value)
        {
            case null:
                return GesValue.GesNothing();
            case GesValue vmValue:
                return vmValue;
            case bool boolean:
                return GesValue.GesBoolean(boolean);
            case string text:
                return GesValue.GesText(text);
            case double number:
                return GesValue.GesFloat(number);
            case float number:
                return GesValue.GesFloat(number);
            case decimal number:
                return GesValue.GesFloat((double)number);
            case long integer:
                return GesValue.GesInteger(integer);
            case int integer:
                return GesValue.GesInteger(integer);
            case short integer:
                return GesValue.GesInteger(integer);
            case byte integer:
                return GesValue.GesInteger(integer);
            case GameEventScriptMessage message:
            {
                var result = new GesValue();
                result.SetMessage(message);
                return result;
            }
            case GameEventScriptMessageSignature signature:
            {
                var result = new GesValue();
                result.SetMessageHandler(signature);
                return result;
            }
        }

        throw new InvalidOperationException($"Cannot convert CLR value of type '{value.GetType().FullName}' to GesValue.");
    }

    public static object? ToClrValue(in GesValue value, Type targetType)
    {
        if (targetType == typeof(GesValue))
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

        throw new InvalidOperationException($"Cannot convert GameEventScript value kind '{value.ValueKind}' to CLR type '{targetType.FullName}'.");
    }

    public static object? ToClrValue(GesValueArguments values, int index, Type targetType)
    {
        if (targetType == typeof(GesValue))
        {
            return values[index];
        }

        if (targetType == typeof(string))
        {
            return values.GetAsText(index);
        }

        if (targetType == typeof(bool))
        {
            return values.GetAsBoolean(index);
        }

        if (targetType == typeof(double))
        {
            return values.GetAsNumber(index);
        }

        if (targetType == typeof(long))
        {
            return values.GetAsInteger(index);
        }

        if (targetType == typeof(int))
        {
            return checked((int)values.GetAsInteger(index));
        }

        if (targetType == typeof(short))
        {
            return checked((short)values.GetAsInteger(index));
        }

        if (targetType == typeof(byte))
        {
            return checked((byte)values.GetAsInteger(index));
        }

        return ToClrValue(in values[index], targetType);
    }

    public static object? ToClrExternalObjectValue(in GesValue value, Type objectType)
    {
        _ = objectType ?? throw new ArgumentNullException(nameof(objectType));
        if (value.ObjectValue is GesExternalValue externalValue &&
            externalValue.Value is GameEventScriptCSharpExternalValue csharpValue &&
            objectType.IsInstanceOfType(csharpValue.Instance)) return csharpValue.Instance;
        return null;
    }
}
