// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using GameEventScript.Api;
using GameEventScript.Runtime;
using GameEventScript.Runtime.Values;
using GameEventScript.Runtime.Vm;

namespace GameEventScript.CSharpBridge;

/// <summary>
/// Represents a ges extension attribute.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
public sealed class GesExtensionAttribute(string name) : Attribute
{
    /// <summary>
    /// Gets the name.
    /// </summary>
    public string Name { get; } = GesExternalTypeNames.NormalizeExtensionName(name);
}

/// <summary>
/// Represents a ges function attribute.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class GesFunctionAttribute(string name) : Attribute
{
    /// <summary>
    /// Initializes a new instance of Ges Function Attribute.
    /// </summary>
    /// <param name="name">The name value.</param>
    /// <param name="returnKind">The return kind value.</param>
    public GesFunctionAttribute(string name, GesBytecodeTypeKind returnKind) : this(name)
    {
        ReturnTypeName = GesExternalTypeNames.ToTypeName(returnKind, unit: null);
        ReturnKind = returnKind;
    }

    /// <summary>
    /// Initializes a new instance of Ges Function Attribute.
    /// </summary>
    /// <param name="name">The name value.</param>
    /// <param name="returnKind">The return kind value.</param>
    /// <param name="unit">The unit value.</param>
    public GesFunctionAttribute(string name, GesBytecodeTypeKind returnKind, GesQuantityUnit unit) : this(name)
    {
        ReturnTypeName = GesExternalTypeNames.ToTypeName(returnKind, unit);
        ReturnKind = returnKind;
        ReturnUnit = unit.ToStoredUnit();
    }

    /// <summary>
    /// Declares the unit of a numeric, spatial or shape result without restricting its kind.
    /// </summary>
    /// <param name="name">The function name.</param>
    /// <param name="unit">The unit attached to the result.</param>
    public GesFunctionAttribute(string name, GesQuantityUnit unit) : this(name)
    {
        ReturnUnit = unit.ToStoredUnit();
    }

    /// <summary>
    /// Gets the name.
    /// </summary>
    public string Name { get; } = GesExternalTypeNames.NormalizeIdentifier(name, nameof(name));

    /// <summary>
    /// Gets the return type name.
    /// </summary>
    public string? ReturnTypeName { get; }

    /// <summary>
    /// Gets the return kind.
    /// </summary>
    public GesBytecodeTypeKind? ReturnKind { get; }

    /// <summary>
    /// Gets the return unit.
    /// </summary>
    public GesQuantityUnit ReturnUnit { get; }
}

/// <summary>
/// Exposes a method with one parameter and a <see cref="bool"/> or nullable Boolean result as the predicate of
/// <c>value is :extension.name</c>. It is registered with one unlabeled argument, whatever its parameter declares; null gives
/// <c>nothing</c>.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class GesPredicateAttribute(string name) : Attribute
{
    /// <summary>
    /// Gets the predicate name.
    /// </summary>
    public string Name { get; } = GesExternalTypeNames.NormalizeIdentifier(name, nameof(name));
}

/// <summary>
/// Collects attributed extension types and optionally falls back to a base registry for unknown signatures.
/// </summary>
public sealed class GesBridgeExtensionRegistryBuilder
{
    private readonly IGesExtensionRegistry? _baseRegistry;
    private readonly List<Type> _extensionTypes = [];
    private readonly List<object> _instances = [];

    private GesBridgeExtensionRegistryBuilder(IGesExtensionRegistry? baseRegistry)
    {
        _baseRegistry = baseRegistry;
    }

    internal static GesBridgeExtensionRegistryBuilder Create(IGesExtensionRegistry? baseRegistry)
        => new(baseRegistry);

    /// <summary>
    /// Adds the value.
    /// </summary>
    /// <param name="extensionType">The extension type value.</param>
    /// <returns>The result of the operation.</returns>
    public GesBridgeExtensionRegistryBuilder Add(Type extensionType)
    {
        _extensionTypes.Add(extensionType ?? throw new ArgumentNullException(nameof(extensionType)));
        return this;
    }

    /// <summary>
    /// Adds the value.
    /// </summary>
    /// <typeparam name="T">The t type.</typeparam>
    /// <returns>The result of the operation.</returns>
    public GesBridgeExtensionRegistryBuilder Add<T>() => Add(typeof(T));

    /// <summary>
    /// Adds the attributed instance and static methods of an instance's type; the registry retains the instance and calls its
    /// methods on it.
    /// </summary>
    /// <param name="instance">An instance of a type that declares <see cref="GesExtensionAttribute"/>.</param>
    /// <returns>This builder.</returns>
    public GesBridgeExtensionRegistryBuilder AddInstance(object instance)
    {
        _instances.Add(instance ?? throw new ArgumentNullException(nameof(instance)));
        return this;
    }

    /// <summary>
    /// Adds the range.
    /// </summary>
    /// <param name="extensionTypes">The extension types value.</param>
    /// <returns>The result of the operation.</returns>
    public GesBridgeExtensionRegistryBuilder AddRange(IEnumerable<Type> extensionTypes)
    {
        _ = extensionTypes ?? throw new ArgumentNullException(nameof(extensionTypes));
        foreach (var extensionType in extensionTypes)
        {
            Add(extensionType);
        }

        return this;
    }

    /// <summary>
    /// Performs the build operation.
    /// </summary>
    /// <returns>The result of the operation.</returns>
    public IGesExtensionRegistry Build()
    {
        var localRegistry = GesBridgeExtensionRegistry.Create(_extensionTypes, _instances);
        return _baseRegistry is null
            ? localRegistry
            : new GesBridgeOverlayExtensionRegistry(localRegistry, _baseRegistry);
    }
}

internal sealed class GesBridgeOverlayExtensionRegistry(IGesExtensionRegistry localRegistry, IGesExtensionRegistry baseRegistry) : IGesExtensionRegistry
{
    public IGesExtensionFunction? Resolve(GesExtensionReference reference)
        => localRegistry.Resolve(reference) ?? baseRegistry.Resolve(reference);
}

/// <summary>
/// Immutable extension registry built from <see cref="GesExtensionAttribute"/>-annotated types through Reflection.
/// </summary>
/// <remarks>Signatures are validated and indexed once at creation; linking resolves each function once.</remarks>
public sealed class GesBridgeExtensionRegistry : IGesExtensionRegistry
{
    private readonly IReadOnlyDictionary<string, IGesExtensionFunction> _functions;

    private GesBridgeExtensionRegistry(IReadOnlyDictionary<string, IGesExtensionFunction> functions)
    {
        _functions = functions ?? throw new ArgumentNullException(nameof(functions));
    }

    /// <summary>
    /// Creates a registry from attributed extension types.
    /// </summary>
    /// <param name="extensionTypes">Types annotated with <see cref="GesExtensionAttribute"/>.</param>
    /// <returns>The validated registry.</returns>
    /// <exception cref="ArgumentException">An extension declaration is invalid or a signature is duplicated.</exception>
    public static GesBridgeExtensionRegistry Create(params Type[] extensionTypes) => Create((IEnumerable<Type>)extensionTypes);

    /// <summary>
    /// Creates a registry from attributed extension types.
    /// </summary>
    /// <param name="extensionTypes">Types annotated with <see cref="GesExtensionAttribute"/>.</param>
    /// <returns>The validated registry.</returns>
    /// <exception cref="ArgumentException">An extension declaration is invalid or a signature is duplicated.</exception>
    public static GesBridgeExtensionRegistry Create(IEnumerable<Type> extensionTypes) => Create(extensionTypes, []);

    internal static GesBridgeExtensionRegistry Create(IEnumerable<Type> extensionTypes, IEnumerable<object> instances)
    {
        _ = extensionTypes ?? throw new ArgumentNullException(nameof(extensionTypes));
        var functions = new Dictionary<string, IGesExtensionFunction>(StringComparer.Ordinal);
        foreach (var extensionType in extensionTypes)
        {
            RegisterExtensionType(extensionType ?? throw new ArgumentException("Extension type list contains null.", nameof(extensionTypes)), null, functions);
        }

        foreach (var instance in instances) RegisterExtensionType(instance.GetType(), instance, functions);
        return new GesBridgeExtensionRegistry(functions);
    }

    /// <summary>
    /// Creates a builder for incrementally added extension types.
    /// </summary>
    /// <returns>A new empty builder.</returns>
    public static GesBridgeExtensionRegistryBuilder CreateBuilder()
        => GesBridgeExtensionRegistryBuilder.Create(baseRegistry: null);

    /// <summary>
    /// Creates a builder whose registry falls back to <paramref name="baseRegistry"/> for signatures it does not define.
    /// </summary>
    /// <param name="baseRegistry">The fallback registry.</param>
    /// <returns>A new empty builder.</returns>
    public static GesBridgeExtensionRegistryBuilder CreateBuilder(IGesExtensionRegistry baseRegistry)
        => GesBridgeExtensionRegistryBuilder.Create(baseRegistry ?? throw new ArgumentNullException(nameof(baseRegistry)));

    /// <summary>
    /// Resolves a referenced extension signature.
    /// </summary>
    /// <param name="reference">The Program's extension reference.</param>
    /// <returns>The function, or <see langword="null"/> when the signature is not registered.</returns>
    public IGesExtensionFunction? Resolve(GesExtensionReference reference)
    {
        _ = reference ?? throw new ArgumentNullException(nameof(reference));
        return _functions.GetValueOrDefault(reference.SignatureId);
    }

    // Static methods, and with an instance also its instance methods, bound to it.
    private static void RegisterExtensionType(Type extensionType, object? instance, IDictionary<string, IGesExtensionFunction> functions)
    {
        var extensionAttribute = extensionType.GetCustomAttribute<GesExtensionAttribute>() ??
                                 throw new ArgumentException($"CLR extension type '{extensionType.FullName}' must declare GesExtensionAttribute.", nameof(extensionType));

        var flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | (instance is null ? 0 : BindingFlags.Instance);
        foreach (var method in extensionType.GetMethods(flags))
        {
            var functionAttribute = method.GetCustomAttribute<GesFunctionAttribute>();
            var predicateAttribute = method.GetCustomAttribute<GesPredicateAttribute>();
            if (functionAttribute is null && predicateAttribute is null) continue;
            if (predicateAttribute is not null && functionAttribute is not null)
                throw new ArgumentException($"GameEventScript extension function '{extensionType.FullName}.{method.Name}' declares both GesFunction and GesPredicate.");
            if (method.DeclaringType == typeof(object)) continue;
            var function = CreateFunction(extensionAttribute.Name, functionAttribute ?? new GesFunctionAttribute(predicateAttribute!.Name), predicateAttribute is not null, method, method.IsStatic ? null : instance, out var signatureId);
            if (!functions.TryAdd(signatureId, function)) throw new ArgumentException($"GameEventScript extension function '{signatureId}' is registered more than once.", nameof(extensionType));
        }
    }

    private static IGesExtensionFunction CreateFunction(string extensionName, GesFunctionAttribute attribute, bool predicate, MethodInfo method, object? instance, out string signatureId)
    {
        if (!method.IsStatic && instance is null)
        {
            throw new ArgumentException($"GameEventScript extension function '{method.DeclaringType?.FullName}.{method.Name}' must be static, or registered with AddInstance.");
        }

        if (method.ReturnType == typeof(void))
        {
            throw new ArgumentException($"GameEventScript extension function '{method.DeclaringType?.FullName}.{method.Name}' must return a extension-compatible value.");
        }

        var parameters = method.GetParameters();
        var hasContext = parameters.Length > 0 && parameters[0].ParameterType == typeof(GesExtensionCall);
        var scriptParameters = parameters.Skip(hasContext ? 1 : 0).ToArray();
        var definitions = scriptParameters
            .Select(parameter => CreateParameterDefinition(method, parameter))
            .ToArray();
        if (predicate && (definitions.Length != 1 || method.ReturnType != typeof(bool) && method.ReturnType != typeof(bool?)))
            throw new ArgumentException($"GameEventScript predicate '{method.DeclaringType?.FullName}.{method.Name}' requires one parameter and a bool or bool? result.");
        var labels = predicate ? [GesMessageSignature.UnlabeledParameterName] : definitions.Select(definition => definition.Name).ToArray();
        signatureId = new GesExtensionReference(extensionName, attribute.Name, labels).SignatureId;
        var readers = scriptParameters
            .Select((parameter, index) => CreateArgumentReader(method, parameter.ParameterType, definitions[index]))
            .ToArray();
        var returnConverter = CreateReturnConverter(method, attribute);

        return CreateAdapter(method, instance, hasContext, readers, returnConverter);
    }

    private static ExtensionParameterDefinition CreateParameterDefinition(MethodInfo method, ParameterInfo parameter)
    {
        var attribute = parameter.GetCustomAttribute<GesParamAttribute>() ??
                        throw new ArgumentException(
                            $"GameEventScript extension function '{method.DeclaringType?.FullName}.{method.Name}' parameter '{parameter.Name}' must declare GesParamAttribute.");
        return attribute.Kind is { } kind
            ? new ExtensionParameterDefinition(attribute.Name, kind, attribute.Unit)
            : new ExtensionParameterDefinition(attribute.Name, attribute.TypeName);
    }

    private static object CreateArgumentReader(MethodInfo method, Type parameterType, ExtensionParameterDefinition definition)
    {
        if (parameterType == typeof(GesValue))
        {
            return GesValueArgumentReader.Instance;
        }

        if (Nullable.GetUnderlyingType(parameterType) is { } underlying)
        {
            var inner = CreateArgumentReader(method, underlying, definition);
            return Activator.CreateInstance(typeof(NullableArgumentReader<>).MakeGenericType(underlying), inner)!;
        }

        if (parameterType == typeof(double))
        {
            RequireParameterKind(method, definition, GesBytecodeTypeKind.Float, allowUnitTypes: true);
            return new FloatArgumentReader(definition.Unit);
        }

        if (parameterType == typeof(long))
        {
            RequireParameterKind(method, definition, GesBytecodeTypeKind.Float, allowUnitTypes: false);
            return new LongArgumentReader(definition.Unit);
        }

        if (parameterType == typeof(int))
        {
            RequireParameterKind(method, definition, GesBytecodeTypeKind.Float, allowUnitTypes: false);
            return new IntArgumentReader(definition.Unit);
        }

        if (parameterType == typeof(bool))
        {
            RequireParameterKind(method, definition, GesBytecodeTypeKind.Boolean, allowUnitTypes: false);
            return BoolArgumentReader.Instance;
        }

        if (parameterType == typeof(string)) return TextArgumentReader.Instance;
        if (parameterType == typeof(byte[])) return BytesArgumentReader.Instance;
        if (parameterType == typeof(GesPoint)) return new SpatialArgumentReader<GesPoint>(value => GesPoint.FromValue(value, definition.Unit));
        if (parameterType == typeof(GesVector)) return new SpatialArgumentReader<GesVector>(value => GesVector.FromValue(value, definition.Unit));
        if (parameterType == typeof(GesBox)) return new SpatialArgumentReader<GesBox>(value => GesBox.FromValue(value, definition.Unit));
        if (parameterType == typeof(GesPoly)) return new PolyArgumentReader(definition.Unit);

        if (parameterType.IsValueType)
        {
            throw new ArgumentException(
                $"GameEventScript extension function '{method.DeclaringType?.FullName}.{method.Name}' parameter '{definition.Name}' type '{parameterType.FullName}' is not extension-compatible.");
        }

        if (definition.Kind is not null)
        {
            throw new ArgumentException(
                $"GameEventScript extension function '{method.DeclaringType?.FullName}.{method.Name}' parameter '{definition.Name}' uses CLR external type '{parameterType.FullName}' but declares primitive type ':{definition.TypeName}'.");
        }

        return Activator.CreateInstance(typeof(ExternalObjectArgumentReader<>).MakeGenericType(parameterType))!;
    }

    private static void RequireParameterKind(MethodInfo method, ExtensionParameterDefinition definition, GesBytecodeTypeKind kind, bool allowUnitTypes)
    {
        if (definition.Kind is null)
        {
            return;
        }

        if (definition.Kind == kind)
        {
            return;
        }

        if (allowUnitTypes && definition.Kind == GesBytecodeTypeKind.Float)
        {
            return;
        }

        throw new ArgumentException(
            $"GameEventScript extension function '{method.DeclaringType?.FullName}.{method.Name}' parameter '{definition.Name}' declares ':{definition.TypeName}' but CLR parameter requires '{kind}'.");
    }

    private static object CreateReturnConverter(MethodInfo method, GesFunctionAttribute attribute) => CreateReturnConverter(method, attribute, method.ReturnType);

    private static object CreateReturnConverter(MethodInfo method, GesFunctionAttribute attribute, Type returnType)
    {
        if (returnType == typeof(GesValue))
        {
            return GesValueReturnConverter.Instance;
        }

        if (returnType == typeof(double))
        {
            RequireReturnKind(method, attribute, GesBytecodeTypeKind.Float);
            return new FloatReturnConverter(attribute.ReturnUnit);
        }

        if (returnType == typeof(long))
        {
            RequireReturnKind(method, attribute, GesBytecodeTypeKind.Float);
            return new LongReturnConverter(attribute.ReturnUnit);
        }

        if (returnType == typeof(int))
        {
            RequireReturnKind(method, attribute, GesBytecodeTypeKind.Float);
            return new IntReturnConverter(attribute.ReturnUnit);
        }

        if (returnType == typeof(bool))
        {
            RequireReturnKind(method, attribute, GesBytecodeTypeKind.Boolean);
            return BoolReturnConverter.Instance;
        }

        if (Nullable.GetUnderlyingType(returnType) is { } underlying && underlying != typeof(ValueTuple<double, GesQuantityUnit>) && underlying != typeof(ValueTuple<double, GesQuantityUnit?>))
        {
            var inner = CreateReturnConverter(method, attribute, underlying);
            return Activator.CreateInstance(typeof(NullableReturnConverter<>).MakeGenericType(underlying), inner)!;
        }

        var unit = attribute.ReturnUnit;
        if (returnType == typeof(string)) return new SpatialReturnConverter<string>(value => value is null ? GesValue.GesNothing() : GesValue.GesText(value));
        if (returnType == typeof(byte[])) return new SpatialReturnConverter<byte[]>(value => value is null ? GesValue.GesNothing() : GesValue.GesBytes(value));
        if (returnType == typeof(GesPoint)) return new SpatialReturnConverter<GesPoint>(value => value.ToValue(unit));
        if (returnType == typeof(GesVector)) return new SpatialReturnConverter<GesVector>(value => value.ToValue(unit));
        if (returnType == typeof(GesBox)) return new SpatialReturnConverter<GesBox>(value => value.ToValue(unit));
        if (returnType == typeof(GesPoly)) return new SpatialReturnConverter<GesPoly>(value => value is null ? GesValue.GesNothing() : value.ToValue(unit));

        if (returnType == typeof(ValueTuple<double, GesQuantityUnit>))
        {
            return FloatNumericUnitTupleReturnConverter.Instance;
        }

        if (returnType == typeof(ValueTuple<double, GesQuantityUnit?>))
        {
            return FloatNullableNumericUnitTupleReturnConverter.Instance;
        }

        throw new ArgumentException($"GameEventScript extension function '{method.DeclaringType?.FullName}.{method.Name}' return type '{returnType.FullName}' is not extension-compatible.");
    }

    private static void RequireReturnKind(MethodInfo method, GesFunctionAttribute attribute, GesBytecodeTypeKind kind)
    {
        if (attribute.ReturnKind is not null && attribute.ReturnKind != kind)
        {
            throw new ArgumentException(
                $"GameEventScript extension function '{method.DeclaringType?.FullName}.{method.Name}' return annotation ':{attribute.ReturnTypeName}' is incompatible with CLR return type '{method.ReturnType.FullName}'.");
        }
    }

    private static IGesExtensionFunction CreateAdapter(MethodInfo method, object? instance, bool hasContext, object[] readers, object returnConverter)
    {
        if (readers.Length > 4)
        {
            throw new ArgumentException(
                $"GameEventScript extension function '{method.DeclaringType?.FullName}.{method.Name}' has {readers.Length} script parameter(s); annotated extension functions currently support up to 4.");
        }

        var returnType = method.ReturnType;
        var scriptParameterTypes = method.GetParameters()
            .Skip(hasContext ? 1 : 0)
            .Select(parameter => parameter.ParameterType)
            .ToArray();
        var delegateTypes = hasContext
            ? new[] { typeof(GesExtensionCall) }.Concat(scriptParameterTypes).Append(returnType).ToArray()
            : scriptParameterTypes.Append(returnType).ToArray();
        var delegateType = GetFuncType(delegateTypes);
        var functionDelegate = instance is null ? method.CreateDelegate(delegateType) : method.CreateDelegate(delegateType, instance);
        var adapterType = GetAdapterType(readers.Length, hasContext).MakeGenericType(scriptParameterTypes.Append(returnType).ToArray());
        var args = new object[2 + readers.Length];
        args[0] = functionDelegate;
        Array.Copy(readers, 0, args, 1, readers.Length);
        args[^1] = returnConverter;
        return (IGesExtensionFunction)Activator.CreateInstance(adapterType, args)!;
    }

    private static Type GetFuncType(Type[] typeArguments)
        => typeArguments.Length switch
        {
            1 => typeof(Func<>).MakeGenericType(typeArguments),
            2 => typeof(Func<,>).MakeGenericType(typeArguments),
            3 => typeof(Func<,,>).MakeGenericType(typeArguments),
            4 => typeof(Func<,,,>).MakeGenericType(typeArguments),
            5 => typeof(Func<,,,,>).MakeGenericType(typeArguments),
            6 => typeof(Func<,,,,,>).MakeGenericType(typeArguments),
            _ => throw new ArgumentOutOfRangeException(nameof(typeArguments), typeArguments.Length, "Unsupported extension function arity.")
        };

    private static Type GetAdapterType(int arity, bool hasContext)
        => (arity, hasContext) switch
        {
            (0, false) => typeof(AnnotatedExtensionFunction0<>),
            (1, false) => typeof(AnnotatedExtensionFunction1<,>),
            (2, false) => typeof(AnnotatedExtensionFunction2<,,>),
            (3, false) => typeof(AnnotatedExtensionFunction3<,,,>),
            (4, false) => typeof(AnnotatedExtensionFunction4<,,,,>),
            (0, true) => typeof(AnnotatedContextExtensionFunction0<>),
            (1, true) => typeof(AnnotatedContextExtensionFunction1<,>),
            (2, true) => typeof(AnnotatedContextExtensionFunction2<,,>),
            (3, true) => typeof(AnnotatedContextExtensionFunction3<,,,>),
            (4, true) => typeof(AnnotatedContextExtensionFunction4<,,,,>),
            _ => throw new ArgumentOutOfRangeException(nameof(arity), arity, "Unsupported extension function arity.")
        };

    private interface IExtensionArgumentReader<T>
    {
        bool Read(GesValueArguments arguments, int index, out T value);
    }

    private interface IExtensionReturnConverter<T>
    {
        GesValue Convert(T value);
    }

    private sealed class GesValueArgumentReader : IExtensionArgumentReader<GesValue>
    {
        public static readonly GesValueArgumentReader Instance = new();

        public bool Read(GesValueArguments arguments, int index, out GesValue value)
        {
            value = arguments[index];
            return true;
        }
    }

    private sealed class ExtensionParameterDefinition
    {
        public ExtensionParameterDefinition(string name, string typeName)
        {
            Name = NormalizeName(name);
            TypeName = GesExternalTypeNames.NormalizeTypeName(typeName);
            var (kind, unit) = GesExternalTypeNames.GetKindAndUnit(TypeName);
            Kind = kind;
            Unit = unit.ToStoredUnit();
        }

        public ExtensionParameterDefinition(string name, GesBytecodeTypeKind kind, GesQuantityUnit? unit)
        {
            Name = NormalizeName(name);
            TypeName = GesExternalTypeNames.ToTypeName(kind, unit);
            Kind = kind;
            Unit = unit.ToStoredUnit();
        }

        public string Name { get; }

        public string TypeName { get; }

        public GesBytecodeTypeKind? Kind { get; }

        public GesQuantityUnit Unit { get; }

        private static string NormalizeName(string name)
            => string.Equals(name, GesMessageSignature.UnlabeledParameterName, StringComparison.Ordinal)
                ? GesMessageSignature.UnlabeledParameterName
                : GesExternalTypeNames.NormalizeIdentifier(name, nameof(name));
    }

    // A numeric argument must have the declared unit, or none; the unit is removed.
    private sealed class FloatArgumentReader(GesQuantityUnit unit) : IExtensionArgumentReader<double>
    {
        public bool Read(GesValueArguments arguments, int index, out double value)
        {
            if (arguments.KindAt(index) is GesBytecodeTypeKind.Integer or GesBytecodeTypeKind.Float or GesBytecodeTypeKind.Percentage && arguments.UnitAt(index) == unit)
            {
                value = arguments.GetAsNumber(index);
                return true;
            }

            value = 0d;
            return false;
        }
    }

    private sealed class LongArgumentReader(GesQuantityUnit unit) : IExtensionArgumentReader<long>
    {
        public bool Read(GesValueArguments arguments, int index, out long value)
        {
            if (arguments.KindAt(index) is GesBytecodeTypeKind.Integer or GesBytecodeTypeKind.Float or GesBytecodeTypeKind.Percentage or GesBytecodeTypeKind.Boolean && arguments.UnitAt(index) == unit)
            {
                value = arguments.GetAsInteger(index);
                return true;
            }

            value = 0;
            return false;
        }
    }

    private sealed class IntArgumentReader(GesQuantityUnit unit) : IExtensionArgumentReader<int>
    {
        private readonly LongArgumentReader _long = new(unit);

        public bool Read(GesValueArguments arguments, int index, out int value)
        {
            if (_long.Read(arguments, index, out var integer) &&
                integer is >= int.MinValue and <= int.MaxValue)
            {
                value = (int)integer;
                return true;
            }

            value = 0;
            return false;
        }
    }

    private sealed class BoolArgumentReader : IExtensionArgumentReader<bool>
    {
        public static readonly BoolArgumentReader Instance = new();

        public bool Read(GesValueArguments arguments, int index, out bool value)
        {
            if (arguments.KindAt(index) is GesBytecodeTypeKind.Boolean or GesBytecodeTypeKind.Integer or GesBytecodeTypeKind.Float or GesBytecodeTypeKind.Percentage
                or GesBytecodeTypeKind.Vector or GesBytecodeTypeKind.Point)
            {
                value = arguments.GetAsBoolean(index);
                return true;
            }

            value = false;
            return false;
        }
    }

    private sealed class NullableArgumentReader<T>(IExtensionArgumentReader<T> inner) : IExtensionArgumentReader<T?> where T : struct
    {
        public bool Read(GesValueArguments arguments, int index, out T? value)
        {
            value = null;
            if (arguments.IsNothingAt(index)) return true;
            if (!inner.Read(arguments, index, out var present)) return false;
            value = present;
            return true;
        }
    }

    // Spatial values and boxes with the declared unit, which they leave behind.
    private sealed class SpatialArgumentReader<T>(Func<GesValue, T?> read) : IExtensionArgumentReader<T> where T : struct
    {
        public bool Read(GesValueArguments arguments, int index, out T value)
        {
            var result = read(arguments[index]);
            value = result.GetValueOrDefault();
            return result.HasValue;
        }
    }

    private sealed class TextArgumentReader : IExtensionArgumentReader<string>
    {
        public static readonly TextArgumentReader Instance = new();

        public bool Read(GesValueArguments arguments, int index, out string value)
        {
            var valid = arguments.KindAt(index) == GesBytecodeTypeKind.Text;
            value = valid ? arguments[index].AsText() : "";
            return valid;
        }
    }

    // A byte[] parameter receives a copy of a Bytes argument.
    private sealed class BytesArgumentReader : IExtensionArgumentReader<byte[]>
    {
        public static readonly BytesArgumentReader Instance = new();

        public bool Read(GesValueArguments arguments, int index, out byte[] value)
        {
            var valid = arguments.KindAt(index) == GesBytecodeTypeKind.Bytes;
            value = valid ? arguments[index].AsBytes().ToArray() : [];
            return valid;
        }
    }

    private sealed class PolyArgumentReader(GesQuantityUnit unit) : IExtensionArgumentReader<GesPoly>
    {
        public bool Read(GesValueArguments arguments, int index, out GesPoly value)
        {
            value = GesPoly.FromValue(arguments[index], unit)!;
            return value is not null;
        }
    }

    private sealed class NullableReturnConverter<T>(IExtensionReturnConverter<T> inner) : IExtensionReturnConverter<T?> where T : struct
    {
        public GesValue Convert(T? value) => value is { } present ? inner.Convert(present) : GesValue.GesNothing();
    }

    private sealed class SpatialReturnConverter<T>(Func<T, GesValue> convert) : IExtensionReturnConverter<T>
    {
        public GesValue Convert(T value) => convert(value);
    }

    private sealed class ExternalObjectArgumentReader<T> : IExtensionArgumentReader<T>
    {
        public bool Read(GesValueArguments arguments, int index, out T value)
            => (value = (GesBridgeValueConverter.ToClrExternalObjectValue(in arguments[index], typeof(T)) is T typed ? typed : default)!) is not null;
    }

    private sealed class GesValueReturnConverter : IExtensionReturnConverter<GesValue>
    {
        public static readonly GesValueReturnConverter Instance = new();

        public GesValue Convert(GesValue value) => value;
    }

    private sealed class FloatReturnConverter(GesQuantityUnit? unit) : IExtensionReturnConverter<double>
    {
        public GesValue Convert(double value) => GesValue.GesFloat(value, unit.ToStoredUnit());
    }

    private sealed class LongReturnConverter(GesQuantityUnit? unit) : IExtensionReturnConverter<long>
    {
        public GesValue Convert(long value) => GesValue.GesInteger(value, unit.ToStoredUnit());
    }

    private sealed class IntReturnConverter(GesQuantityUnit? unit) : IExtensionReturnConverter<int>
    {
        public GesValue Convert(int value) => GesValue.GesInteger(value, unit.ToStoredUnit());
    }

    private sealed class BoolReturnConverter : IExtensionReturnConverter<bool>
    {
        public static readonly BoolReturnConverter Instance = new();

        public GesValue Convert(bool value) => GesValue.GesBoolean(value);
    }

    private sealed class FloatNumericUnitTupleReturnConverter : IExtensionReturnConverter<(double Value, GesQuantityUnit Unit)>
    {
        public static readonly FloatNumericUnitTupleReturnConverter Instance = new();

        public GesValue Convert((double Value, GesQuantityUnit Unit) value)
            => GesValue.GesFloat(value.Value, value.Unit);
    }

    private sealed class FloatNullableNumericUnitTupleReturnConverter : IExtensionReturnConverter<(double Value, GesQuantityUnit? Unit)>
    {
        public static readonly FloatNullableNumericUnitTupleReturnConverter Instance = new();

        public GesValue Convert((double Value, GesQuantityUnit? Unit) value)
            => GesValue.GesFloat(value.Value, value.Unit.ToStoredUnit());
    }

    private sealed class AnnotatedExtensionFunction0<R>(Func<R> invoke, IExtensionReturnConverter<R> returnConverter) : IGesExtensionFunction
    {
        public void Invoke(GesExtensionCall call)
            => call.SetValue(call.Arguments.Length == 0 ? returnConverter.Convert(invoke()) : GesValue.GesNothing());
    }

    private sealed class AnnotatedExtensionFunction1<T1, R>(Func<T1, R> invoke, IExtensionArgumentReader<T1> reader1, IExtensionReturnConverter<R> returnConverter) : IGesExtensionFunction
    {
        public void Invoke(GesExtensionCall call)
            => call.SetValue(call.Arguments.Length == 1 && reader1.Read(call.Arguments, 0, out var value1)
                ? returnConverter.Convert(invoke(value1))
                : GesValue.GesNothing());
    }

    private sealed class AnnotatedExtensionFunction2<T1, T2, R>(Func<T1, T2, R> invoke, IExtensionArgumentReader<T1> reader1, IExtensionArgumentReader<T2> reader2, IExtensionReturnConverter<R> returnConverter)
        : IGesExtensionFunction
    {
        public void Invoke(GesExtensionCall call)
            => call.SetValue(call.Arguments.Length == 2 &&
               reader1.Read(call.Arguments, 0, out var value1) &&
               reader2.Read(call.Arguments, 1, out var value2)
                ? returnConverter.Convert(invoke(value1, value2))
                : GesValue.GesNothing());
    }

    private sealed class AnnotatedExtensionFunction3<T1, T2, T3, R>(
        Func<T1, T2, T3, R> invoke,
        IExtensionArgumentReader<T1> reader1,
        IExtensionArgumentReader<T2> reader2,
        IExtensionArgumentReader<T3> reader3,
        IExtensionReturnConverter<R> returnConverter) : IGesExtensionFunction
    {
        public void Invoke(GesExtensionCall call)
            => call.SetValue(call.Arguments.Length == 3 &&
               reader1.Read(call.Arguments, 0, out var value1) &&
               reader2.Read(call.Arguments, 1, out var value2) &&
               reader3.Read(call.Arguments, 2, out var value3)
                ? returnConverter.Convert(invoke(value1, value2, value3))
                : GesValue.GesNothing());
    }

    private sealed class AnnotatedExtensionFunction4<T1, T2, T3, T4, R>(
        Func<T1, T2, T3, T4, R> invoke,
        IExtensionArgumentReader<T1> reader1,
        IExtensionArgumentReader<T2> reader2,
        IExtensionArgumentReader<T3> reader3,
        IExtensionArgumentReader<T4> reader4,
        IExtensionReturnConverter<R> returnConverter) : IGesExtensionFunction
    {
        public void Invoke(GesExtensionCall call)
            => call.SetValue(call.Arguments.Length == 4 &&
               reader1.Read(call.Arguments, 0, out var value1) &&
               reader2.Read(call.Arguments, 1, out var value2) &&
               reader3.Read(call.Arguments, 2, out var value3) &&
               reader4.Read(call.Arguments, 3, out var value4)
                ? returnConverter.Convert(invoke(value1, value2, value3, value4))
                : GesValue.GesNothing());
    }

    private sealed class AnnotatedContextExtensionFunction0<R>(Func<GesExtensionCall, R> invoke, IExtensionReturnConverter<R> returnConverter) : IGesExtensionFunction
    {
        public void Invoke(GesExtensionCall call)
            => call.SetValue(call.Arguments.Length == 0 ? returnConverter.Convert(invoke(call)) : GesValue.GesNothing());
    }

    private sealed class AnnotatedContextExtensionFunction1<T1, R>(Func<GesExtensionCall, T1, R> invoke, IExtensionArgumentReader<T1> reader1, IExtensionReturnConverter<R> returnConverter)
        : IGesExtensionFunction
    {
        public void Invoke(GesExtensionCall call)
            => call.SetValue(call.Arguments.Length == 1 && reader1.Read(call.Arguments, 0, out var value1)
                ? returnConverter.Convert(invoke(call, value1))
                : GesValue.GesNothing());
    }

    private sealed class AnnotatedContextExtensionFunction2<T1, T2, R>(
        Func<GesExtensionCall, T1, T2, R> invoke,
        IExtensionArgumentReader<T1> reader1,
        IExtensionArgumentReader<T2> reader2,
        IExtensionReturnConverter<R> returnConverter) : IGesExtensionFunction
    {
        public void Invoke(GesExtensionCall call)
            => call.SetValue(call.Arguments.Length == 2 &&
               reader1.Read(call.Arguments, 0, out var value1) &&
               reader2.Read(call.Arguments, 1, out var value2)
                ? returnConverter.Convert(invoke(call, value1, value2))
                : GesValue.GesNothing());
    }

    private sealed class AnnotatedContextExtensionFunction3<T1, T2, T3, R>(
        Func<GesExtensionCall, T1, T2, T3, R> invoke,
        IExtensionArgumentReader<T1> reader1,
        IExtensionArgumentReader<T2> reader2,
        IExtensionArgumentReader<T3> reader3,
        IExtensionReturnConverter<R> returnConverter) : IGesExtensionFunction
    {
        public void Invoke(GesExtensionCall call)
            => call.SetValue(call.Arguments.Length == 3 &&
               reader1.Read(call.Arguments, 0, out var value1) &&
               reader2.Read(call.Arguments, 1, out var value2) &&
               reader3.Read(call.Arguments, 2, out var value3)
                ? returnConverter.Convert(invoke(call, value1, value2, value3))
                : GesValue.GesNothing());
    }

    private sealed class AnnotatedContextExtensionFunction4<T1, T2, T3, T4, R>(
        Func<GesExtensionCall, T1, T2, T3, T4, R> invoke,
        IExtensionArgumentReader<T1> reader1,
        IExtensionArgumentReader<T2> reader2,
        IExtensionArgumentReader<T3> reader3,
        IExtensionArgumentReader<T4> reader4,
        IExtensionReturnConverter<R> returnConverter) : IGesExtensionFunction
    {
        public void Invoke(GesExtensionCall call)
            => call.SetValue(call.Arguments.Length == 4 &&
               reader1.Read(call.Arguments, 0, out var value1) &&
               reader2.Read(call.Arguments, 1, out var value2) &&
               reader3.Read(call.Arguments, 2, out var value3) &&
               reader4.Read(call.Arguments, 3, out var value4)
                ? returnConverter.Convert(invoke(call, value1, value2, value3, value4))
                : GesValue.GesNothing());
    }
}
