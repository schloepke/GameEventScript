// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using GameEventScript.Api;
using GameEventScript.Runtime;
using GameEventScript.Runtime.Values;

namespace GameEventScript.CSharpBridge;

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
    public GesFieldAttribute(string name, GesBytecodeTypeKind kind)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        TypeName = GesExternalTypeNames.ToTypeName(kind, unit: null);
        Kind = kind;
    }

    /// <summary>
    /// Initializes a new instance of Ges Field Attribute.
    /// </summary>
    /// <param name="name">The name value.</param>
    /// <param name="kind">The kind value.</param>
    /// <param name="unit">The unit value.</param>
    public GesFieldAttribute(string name, GesBytecodeTypeKind kind, GesQuantityUnit unit)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        TypeName = GesExternalTypeNames.ToTypeName(kind, unit);
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
    public GesBytecodeTypeKind? Kind { get; }

    /// <summary>
    /// Gets the unit.
    /// </summary>
    public GesQuantityUnit Unit { get; }
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
    public GesParamAttribute(string name, GesBytecodeTypeKind kind)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        TypeName = GesExternalTypeNames.ToTypeName(kind, unit: null);
        Kind = kind;
    }

    /// <summary>
    /// Initializes a new instance of Ges Param Attribute.
    /// </summary>
    /// <param name="name">The name value.</param>
    /// <param name="kind">The kind value.</param>
    /// <param name="unit">The unit value.</param>
    public GesParamAttribute(string name, GesBytecodeTypeKind kind, GesQuantityUnit unit)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        TypeName = GesExternalTypeNames.ToTypeName(kind, unit);
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
    public GesBytecodeTypeKind? Kind { get; }

    /// <summary>
    /// Gets the unit.
    /// </summary>
    public GesQuantityUnit Unit { get; }
}

/// <summary>
/// Immutable external-type catalog and constructor registry built from <see cref="GesTypeAttribute"/>-annotated CLR types.
/// </summary>
/// <remarks>Pass the same instance to the compiler builder as catalog and to the host builder as registry.</remarks>
public sealed class GesBridgeExternalTypeRegistry : IGesExternalTypeCatalog, IGesExternalTypeRegistry
{
    private readonly Dictionary<string, GesExternalTypeDefinition> _typesByName = new(StringComparer.Ordinal);
    private readonly Dictionary<string, IGesExternalTypeConstructor> _constructors = new(StringComparer.Ordinal);
    private readonly Dictionary<Type, GesBridgeExternalTypeBinding> _bindingsByClrType = new();
    private readonly List<(Type Type, string Use)> _externalUses = [];
    private IReadOnlyList<GesExternalTypeDefinition> _types = Array.Empty<GesExternalTypeDefinition>();

    private GesBridgeExternalTypeRegistry()
    {
    }

    /// <summary>
    /// Gets immutable registered type definitions consistent with name and constructor lookup.
    /// </summary>
    public IReadOnlyList<GesExternalTypeDefinition> Types => _types;

    /// <summary>
    /// Creates a registry from attributed CLR types.
    /// </summary>
    /// <param name="types">Types annotated with <see cref="GesTypeAttribute"/>.</param>
    /// <returns>The validated registry.</returns>
    /// <exception cref="ArgumentException">A type declaration is invalid or duplicated.</exception>
    public static GesBridgeExternalTypeRegistry Create(params Type[] types)
        => Create((IEnumerable<Type>)types);

    /// <summary>
    /// Creates a registry from attributed CLR types.
    /// </summary>
    /// <param name="types">Types annotated with <see cref="GesTypeAttribute"/>.</param>
    /// <returns>The validated registry.</returns>
    /// <exception cref="ArgumentException">A type declaration is invalid or duplicated.</exception>
    public static GesBridgeExternalTypeRegistry Create(IEnumerable<Type> types)
    {
        _ = types ?? throw new ArgumentNullException(nameof(types));
        var registry = new GesBridgeExternalTypeRegistry();
        foreach (var type in types)
        {
            registry.RegisterType(type ?? throw new ArgumentException("External type list contains null.", nameof(types)));
        }

        // A field or parameter of an external CLR type needs that type in the same registry.
        foreach (var (type, use) in registry._externalUses)
        {
            if (registry.FindBinding(type) is null)
                throw new ArgumentException($"{use} has CLR type '{type.FullName}', which this registry does not register.", nameof(types));
        }

        registry._types = GesReadOnlyArray<GesExternalTypeDefinition>.FromOwnedArray(registry._typesByName.Values.ToArray());
        return registry;
    }

    /// <summary>
    /// Resolves the value.
    /// </summary>
    /// <param name="typeName">The type name value.</param>
    /// <returns>The result of the operation.</returns>
    public GesExternalTypeDefinition? Resolve(string typeName)
    {
        var normalized = GesExternalTypeNames.NormalizeTypeName(typeName);
        return _typesByName.TryGetValue(normalized, out var definition) ? definition : null;
    }

    /// <summary>
    /// Resolves the value.
    /// </summary>
    /// <param name="reference">The reference value.</param>
    /// <returns>The result of the operation.</returns>
    public IGesExternalTypeConstructor? Resolve(GesExternalTypeConstructorReference reference)
    {
        _ = reference ?? throw new ArgumentNullException(nameof(reference));
        return _constructors.TryGetValue(reference.SignatureId, out var constructor) ? constructor : null;
    }

    // An external value of a registered CLR type or one derived from it, or nothing for null.
    internal GesValue ToExternalValue(object? value)
    {
        var result = new GesValue();
        if (value is not null && CreateExternalValue(value) is { } externalValue) result.SetExternal(externalValue);
        return result;
    }

    internal IGesExternalValue? CreateExternalValue(object value)
        => FindBinding(value.GetType()) is { } binding ? new GesBridgeExternalValue(value, binding, this) : null;

    // The binding of a CLR type, or of the nearest registered base class, as a factory may return a derived instance.
    private GesBridgeExternalTypeBinding? FindBinding(Type type)
    {
        for (var current = type; current is not null; current = current.BaseType)
            if (_bindingsByClrType.TryGetValue(current, out var binding)) return binding;
        return null;
    }

    private void RegisterType(Type clrType)
    {
        var typeAttribute = clrType.GetCustomAttribute<GesTypeAttribute>() ??
                            throw new ArgumentException($"CLR type '{clrType.FullName}' must declare GesTypeAttribute.", nameof(clrType));
        var typeName = GesExternalTypeNames.NormalizeTypeName(typeAttribute.TypeName);
        var fields = BuildFields(clrType, _externalUses);
        var fieldReaders = fields.ToDictionary(
            field => field.Definition.Name,
            field => field,
            StringComparer.Ordinal);
        var constructors = BuildConstructors(clrType, typeName, fields.Select(field => field.Definition.Name).ToHashSet(StringComparer.Ordinal), this, _externalUses);

        var definition = new GesExternalTypeDefinition(
            typeName,
            fields.Select(field => field.Definition).ToArray(),
            constructors.Select(constructor => constructor.Definition).ToArray());
        if (!_typesByName.TryAdd(definition.Name, definition))
            throw new ArgumentException($"External GameEventScript type ':{definition.Name}' is registered more than once.", nameof(clrType));
        if (!_bindingsByClrType.TryAdd(clrType, new GesBridgeExternalTypeBinding(definition, fieldReaders)))
            throw new ArgumentException($"CLR type '{clrType.FullName}' is registered more than once.", nameof(clrType));
        for (var index = 0; index < constructors.Count; index++)
        {
            var constructor = constructors[index];
            if (!_constructors.TryAdd(constructor.Definition.SignatureId, constructor))
                throw new ArgumentException($"External GameEventScript constructor ':{constructor.Definition.SignatureId}' is registered more than once.", nameof(clrType));
        }
    }

    private static IReadOnlyList<GesBridgeExternalFieldBinding> BuildFields(Type clrType, List<(Type, string)> externalUses)
    {
        var fields = new List<GesBridgeExternalFieldBinding>();
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

            fields.Add(CreateFieldBinding(clrType, attribute, property.PropertyType, instance => property.GetValue(instance, BindingFlags.DoNotWrapExceptions, null, null, null), externalUses));
        }

        foreach (var field in clrType.GetFields(BindingFlags.Instance | BindingFlags.Public))
        {
            var attribute = field.GetCustomAttribute<GesFieldAttribute>();
            if (attribute is null)
            {
                continue;
            }

            fields.Add(CreateFieldBinding(clrType, attribute, field.FieldType, instance => field.GetValue(instance), externalUses));
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

    // The CLR type of a field decides its conversion at registration, so an unsupported type fails here and not when a
    // script first reads the field.
    private static GesBridgeExternalFieldBinding CreateFieldBinding(Type owner, GesFieldAttribute attribute, Type clrType, Func<object, object?> reader, List<(Type, string)> externalUses)
    {
        var definition = attribute.Kind is { } kind
            ? new GesExternalTypeFieldDefinition(attribute.Name, kind, attribute.Unit)
            : new GesExternalTypeFieldDefinition(attribute.Name, attribute.TypeName);
        var writer = GesBridgeValueConverter.CreateWriter(clrType, attribute.Unit);
        if (writer is null)
        {
            if (!clrType.IsClass || clrType.GetCustomAttribute<GesTypeAttribute>() is null)
                throw new ArgumentException($"External GameEventScript field '{attribute.Name}' on '{owner.FullName}' has CLR type '{clrType.FullName}', which does not convert to a GES value.");
            externalUses.Add((clrType, $"External GameEventScript field '{attribute.Name}' on '{owner.FullName}'"));
        }

        return new GesBridgeExternalFieldBinding(definition, reader, writer);
    }

    private static IReadOnlyList<ReflectionExternalTypeConstructor> BuildConstructors(Type clrType, string typeName, ISet<string> fieldNames, GesBridgeExternalTypeRegistry registry, List<(Type, string)> externalUses)
    {
        var constructors = new List<ReflectionExternalTypeConstructor>();
        foreach (var constructor in clrType.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            if (constructor.GetCustomAttribute<GesConstructAttribute>() is null)
            {
                continue;
            }

            constructors.Add(BuildConstructorBinding(typeName, fieldNames, constructor, values => constructor.Invoke(BindingFlags.DoNotWrapExceptions, null, values, null), registry, externalUses));
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

            constructors.Add(BuildConstructorBinding(typeName, fieldNames, method, values => method.Invoke(null, BindingFlags.DoNotWrapExceptions, null, values, null), registry, externalUses));
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

    private static ReflectionExternalTypeConstructor BuildConstructorBinding(string typeName, ISet<string> fieldNames, MethodBase method, Func<object?[], object?> invoke, GesBridgeExternalTypeRegistry registry, List<(Type, string)> externalUses)
    {
        var parameters = method.GetParameters()
            .Select(parameter => BuildParameterDefinition(method, fieldNames, parameter, externalUses))
            .ToArray();
        return new ReflectionExternalTypeConstructor(
            new GesExternalTypeConstructorDefinition(typeName, parameters.Select(parameter => parameter.Definition)),
            parameters,
            invoke,
            registry);
    }

    private static ExternalParameterBinding BuildParameterDefinition(MethodBase method, ISet<string> fieldNames, ParameterInfo parameter, List<(Type, string)> externalUses)
    {
        var attribute = parameter.GetCustomAttribute<GesParamAttribute>() ??
                        throw new ArgumentException($"External GameEventScript constructor '{method.DeclaringType?.FullName}.{method.Name}' parameter '{parameter.Name}' must declare GesParamAttribute.");
        var definition = attribute.Kind is { } kind
            ? new GesExternalTypeParameterDefinition(attribute.Name, kind, attribute.Unit)
            : new GesExternalTypeParameterDefinition(attribute.Name, attribute.TypeName);
        if (!fieldNames.Contains(definition.Name))
        {
            throw new ArgumentException($"External GameEventScript constructor '{method.DeclaringType?.FullName}.{method.Name}' parameter '{definition.Name}' is not a declared field.");
        }

        var use = $"External GameEventScript constructor '{method.DeclaringType?.FullName}.{method.Name}' parameter '{definition.Name}'";
        var reader = GesBridgeValueConverter.CreateReader(parameter.ParameterType, attribute.Unit) ??
                     throw new ArgumentException($"{use} has CLR type '{parameter.ParameterType.FullName}', which no GES value converts to.");
        if (parameter.ParameterType.IsClass && parameter.ParameterType.GetCustomAttribute<GesTypeAttribute>() is not null) externalUses.Add((parameter.ParameterType, use));
        return new ExternalParameterBinding(definition, parameter.ParameterType, reader);
    }

    private sealed record ExternalParameterBinding(GesExternalTypeParameterDefinition Definition, Type ClrType, GesBridgeValueConverter.ClrReader Reader);

    private sealed class ReflectionExternalTypeConstructor(
        GesExternalTypeConstructorDefinition definition,
        IReadOnlyList<ExternalParameterBinding> parameters,
        Func<object?[], object?> invoke,
        GesBridgeExternalTypeRegistry registry) : IGesExternalTypeConstructor
    {
        public GesExternalTypeConstructorDefinition Definition { get; } = definition;

        public void Invoke(GesExternalTypeConstructorCall call)
        {
            var arguments = call.Arguments;
            if (arguments.Length != parameters.Count)
            {
                call.SetNothing();
                return;
            }

            // An argument that does not convert exactly is a fault of the call, as a throwing Swift conversion is.
            var converted = new object?[arguments.Length];
            for (var index = 0; index < converted.Length; index++)
            {
                if (!parameters[index].Reader(in arguments[index], out converted[index]))
                    throw new InvalidOperationException($"The argument '{parameters[index].Definition.Name}' of :{Definition.TypeName} does not convert to CLR type '{parameters[index].ClrType.FullName}'.");
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

internal sealed record GesBridgeExternalFieldBinding(GesExternalTypeFieldDefinition Definition, Func<object, object?> Reader, Func<object?, GesValue>? Writer);

internal sealed class GesBridgeExternalTypeBinding(GesExternalTypeDefinition definition, IReadOnlyDictionary<string, GesBridgeExternalFieldBinding> fields)
{
    internal GesExternalTypeDefinition Definition { get; } = definition;
    internal IReadOnlyDictionary<string, GesBridgeExternalFieldBinding> Fields { get; } = fields;
}

internal sealed class GesBridgeExternalValue(object instance, GesBridgeExternalTypeBinding binding, GesBridgeExternalTypeRegistry registry) : IGesExternalValue
{
    internal object Instance { get; } = instance;

    public GesExternalTypeDefinition Definition => binding.Definition;

    public GesValue? GetField(string fieldName)
    {
        if (!binding.Fields.TryGetValue(fieldName, out var field)) return null;
        var clr = field.Reader(Instance);
        var value = field.Writer is { } writer ? writer(clr) : registry.ToExternalValue(clr);
        return GesExternalTypeValueConverter.CoerceToDeclaredType(in value, field.Definition);
    }
}

internal static class GesBridgeValueConverter
{
    // A strict conversion of a GES value to a CLR type; false when the value does not convert exactly.
    internal delegate bool ClrReader(in GesValue value, out object? result);

    // The CLR types a field may hold, as a conversion to its GES value, or null for a type no field may have. A class with
    // GesTypeAttribute is an external value of the registry; the caller checks that the registry holds it.
    internal static Func<object?, GesValue>? CreateWriter(Type type, GesQuantityUnit unit)
    {
        if (Nullable.GetUnderlyingType(type) is { } underlying)
            return CreateWriter(underlying, unit) is { } inner ? value => value is null ? GesValue.GesNothing() : inner(value) : null;
        if (type == typeof(GesValue)) return value => (GesValue)value!;
        if (type == typeof(bool)) return value => GesValue.GesBoolean((bool)value!);
        if (type == typeof(string)) return value => value is null ? GesValue.GesNothing() : GesValue.GesText((string)value);
        if (type == typeof(byte[])) return value => value is null ? GesValue.GesNothing() : GesValue.GesBytes((byte[])value);
        if (type == typeof(double)) return value => GesValue.GesFloat((double)value!);
        if (type == typeof(float)) return value => GesValue.GesFloat((float)value!);
        if (type == typeof(long)) return value => GesValue.GesInteger((long)value!);
        if (type == typeof(int)) return value => GesValue.GesInteger((int)value!);
        if (type == typeof(short)) return value => GesValue.GesInteger((short)value!);
        if (type == typeof(byte)) return value => GesValue.GesInteger((byte)value!);
        if (type == typeof(GesPoint)) return value => ((GesPoint)value!).ToValue(unit);
        if (type == typeof(GesVector)) return value => ((GesVector)value!).ToValue(unit);
        if (type == typeof(GesBox)) return value => ((GesBox)value!).ToValue(unit);
        if (type == typeof(GesPoly)) return value => value is null ? GesValue.GesNothing() : ((GesPoly)value).ToValue(unit);
        if (type == typeof(GameEventScriptMessage))
            return value =>
            {
                var result = new GesValue();
                if (value is GameEventScriptMessage message) result.SetMessage(message);
                return result;
            };
        if (type == typeof(GesMessageSignature))
            return value =>
            {
                var result = new GesValue();
                if (value is GesMessageSignature signature) result.SetHandler(signature);
                return result;
            };
        return null;
    }

    // The CLR types a constructor parameter may have, as a strict conversion, or null for an unsupported type. Nothing
    // converts only to a nullable value type or a GesValue; a number converts only exactly; a Boolean is no number.
    internal static ClrReader? CreateReader(Type type, GesQuantityUnit unit)
    {
        if (Nullable.GetUnderlyingType(type) is { } underlying)
        {
            if (CreateReader(underlying, unit) is not { } inner) return null;
            return (in GesValue value, out object? result) =>
            {
                result = null;
                return value.IsNothing || inner(in value, out result);
            };
        }

        if (type == typeof(GesValue))
            return (in GesValue value, out object? result) =>
            {
                result = value;
                return true;
            };
        if (type == typeof(string)) return Read(GesBytecodeTypeKind.Text, (in GesValue value) => value.AsText());
        if (type == typeof(byte[])) return Read(GesBytecodeTypeKind.Bytes, (in GesValue value) => value.AsBytes().ToArray());
        if (type == typeof(bool)) return Read(GesBytecodeTypeKind.Boolean, (in GesValue value) => value.AsBoolean());
        if (type == typeof(double))
            return (in GesValue value, out object? result) =>
            {
                var valid = TryReadDouble(in value, out var number);
                result = number;
                return valid;
            };
        if (type == typeof(float))
            return (in GesValue value, out object? result) =>
            {
                var valid = TryReadDouble(in value, out var number) && (double)(float)number == number;
                result = (float)number;
                return valid;
            };
        if (type == typeof(long)) return Integer(long.MinValue, long.MaxValue, integer => integer);
        if (type == typeof(int)) return Integer(int.MinValue, int.MaxValue, integer => (int)integer);
        if (type == typeof(short)) return Integer(short.MinValue, short.MaxValue, integer => (short)integer);
        if (type == typeof(byte)) return Integer(byte.MinValue, byte.MaxValue, integer => (byte)integer);
        if (type == typeof(GesPoint)) return Shape(value => GesPoint.FromValue(value, unit));
        if (type == typeof(GesVector)) return Shape(value => GesVector.FromValue(value, unit));
        if (type == typeof(GesBox)) return Shape(value => GesBox.FromValue(value, unit));
        if (type == typeof(GesPoly)) return Shape(value => GesPoly.FromValue(value, unit));
        if (type.IsClass && type.GetCustomAttribute<GesTypeAttribute>() is not null)
            return (in GesValue value, out object? result) =>
            {
                result = ToClrExternalObjectValue(in value, type);
                return result is not null;
            };
        return null;
    }

    private delegate object ValueRead(in GesValue value);

    private static ClrReader Read(GesBytecodeTypeKind kind, ValueRead read)
        => (in GesValue value, out object? result) =>
        {
            var valid = value.ValueKind == kind;
            result = valid ? read(in value) : null;
            return valid;
        };

    private static ClrReader Integer(long minimum, long maximum, Func<long, object> convert)
        => (in GesValue value, out object? result) =>
        {
            var valid = TryReadInt64(in value, out var integer) && integer >= minimum && integer <= maximum;
            result = valid ? convert(integer) : null;
            return valid;
        };

    private static ClrReader Shape(Func<GesValue, object?> read)
        => (in GesValue value, out object? result) =>
        {
            result = read(value);
            return result is not null;
        };

    // An Integer, or a Float with an integral value in the Int64 range.
    internal static bool TryReadInt64(in GesValue value, out long result)
    {
        result = 0;
        if (value.IsNothing) return false;
        if (value.ValueKind == GesBytecodeTypeKind.Integer)
        {
            result = value.AsInteger();
            return true;
        }

        if (value.ValueKind != GesBytecodeTypeKind.Float) return false;
        var number = value.AsNumber();
        if (!(number >= -9223372036854775808d && number < 9223372036854775808d && number == Math.Truncate(number))) return false;
        result = (long)number;
        return true;
    }

    // A Float, or an Integer that binary64 represents exactly.
    internal static bool TryReadDouble(in GesValue value, out double result)
    {
        result = 0d;
        if (value.IsNothing) return false;
        if (value.ValueKind == GesBytecodeTypeKind.Float)
        {
            result = value.AsNumber();
            return true;
        }

        if (value.ValueKind != GesBytecodeTypeKind.Integer) return false;
        var integer = value.AsInteger();
        result = integer;
        return result >= -9223372036854775808d && result < 9223372036854775808d && (long)result == integer;
    }

    public static object? ToClrExternalObjectValue(in GesValue value, Type objectType)
    {
        _ = objectType ?? throw new ArgumentNullException(nameof(objectType));
        if (value.ObjectValue is GesExternalStorage externalValue &&
            externalValue.Value is GesBridgeExternalValue csharpValue &&
            objectType.IsInstanceOfType(csharpValue.Instance)) return csharpValue.Instance;
        return null;
    }
}
