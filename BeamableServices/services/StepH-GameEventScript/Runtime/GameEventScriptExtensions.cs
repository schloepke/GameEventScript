#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using StepH.GameEventScript.Api;
using StepH.GameEventScript.Types;
using StepH.GameEventScript.VirtualMachine;
using static StepH.GameEventScript.Api.GameEventScriptValueFactory;

namespace StepH.GameEventScript.Runtime;

public interface IGameEventScriptExtensionRegistry
{
    bool TryResolve(GameEventScriptExtensionReference reference, out IGameEventScriptExtensionFunction function);
}

public interface IGameEventScriptExtensionFunction
{
    GameEventScriptBoxedValue Invoke(GameEventScriptExtensionContext context, ReadOnlySpan<GameEventScriptBoxedValue> arguments);
}

public sealed class GameEventScriptExtensionReference
{
    public GameEventScriptExtensionReference(string? extensionName, string? functionName, IEnumerable<string?>? argumentLabels)
    {
        ExtensionName = NormalizeName(extensionName);
        FunctionName = NormalizeName(functionName);
        ArgumentLabels = (argumentLabels ?? Array.Empty<string?>()).Select(GameEventScriptMessageSignature.NormalizeParameterName).ToArray();
        SignatureId = $"{ExtensionName}.{FunctionName}({string.Join(",", ArgumentLabels)})";
    }

    public string ExtensionName { get; }

    public string FunctionName { get; }

    public IReadOnlyList<string> ArgumentLabels { get; }

    public string SignatureId { get; }

    private static string NormalizeName(string? name) => string.IsNullOrWhiteSpace(name) ? string.Empty : name.Trim();
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
public sealed class GesExtensionAttribute(string name) : Attribute
{
    public string Name { get; } = GameEventScriptExternalTypeNames.NormalizeTypeName(name);
}

[AttributeUsage(AttributeTargets.Method)]
public sealed class GesFunctionAttribute(string name) : Attribute
{
    public GesFunctionAttribute(string name, GameEventScriptBytecodeTypeKind returnKind) : this(name)
    {
        ReturnTypeName = GameEventScriptExternalTypeNames.ToTypeName(returnKind, unit: null);
        ReturnKind = returnKind;
    }

    public GesFunctionAttribute(string name, GameEventScriptBytecodeTypeKind returnKind, GameEventScriptBytecodeInstructionUnit unit) : this(name)
    {
        ReturnTypeName = GameEventScriptExternalTypeNames.ToTypeName(returnKind, unit);
        ReturnKind = returnKind;
        ReturnUnit = unit.ToStoredUnit();
    }

    public string Name { get; } = GameEventScriptExternalTypeNames.NormalizeIdentifier(name, nameof(name));

    public string? ReturnTypeName { get; }

    public GameEventScriptBytecodeTypeKind? ReturnKind { get; }

    public GameEventScriptBytecodeInstructionUnit ReturnUnit { get; }
}

public sealed class GameEventScriptExtensionContext(GameEventScriptSession runtimeSession)
{
    public GameEventScriptSession RuntimeSession { get; } = runtimeSession ?? throw new ArgumentNullException(nameof(runtimeSession));

    public GameEventScriptRandomGenerator Random => RuntimeSession.Random;

    public GameEventScriptRuntimeLimits RuntimeLimits => RuntimeSession.RuntimeLimits;
}

public sealed class GameEventScriptExtensionRegistry : IGameEventScriptExtensionRegistry
{
    private readonly IReadOnlyDictionary<string, IGameEventScriptExtensionFunction> _functions;

    private GameEventScriptExtensionRegistry(IReadOnlyDictionary<string, IGameEventScriptExtensionFunction> functions)
    {
        _functions = functions ?? throw new ArgumentNullException(nameof(functions));
    }

    public static GameEventScriptExtensionRegistry Create(params Type[] extensionTypes) => Create((IEnumerable<Type>)extensionTypes);

    public static GameEventScriptExtensionRegistry Create(IEnumerable<Type> extensionTypes)
    {
        _ = extensionTypes ?? throw new ArgumentNullException(nameof(extensionTypes));
        var functions = new Dictionary<string, IGameEventScriptExtensionFunction>(StringComparer.Ordinal);
        foreach (var extensionType in extensionTypes)
        {
            RegisterExtensionType(extensionType ?? throw new ArgumentException("Extension type list contains null.", nameof(extensionTypes)), functions);
        }

        return new GameEventScriptExtensionRegistry(functions);
    }

    public bool TryResolve(GameEventScriptExtensionReference reference, out IGameEventScriptExtensionFunction function)
    {
        _ = reference ?? throw new ArgumentNullException(nameof(reference));
        return _functions.TryGetValue(reference.SignatureId, out function!);
    }

    private static void RegisterExtensionType(Type extensionType, IDictionary<string, IGameEventScriptExtensionFunction> functions)
    {
        var extensionAttribute = extensionType.GetCustomAttribute<GesExtensionAttribute>() ??
                                 throw new ArgumentException($"CLR extension type '{extensionType.FullName}' must declare GesExtensionAttribute.", nameof(extensionType));

        foreach (var method in extensionType.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
        {
            var functionAttribute = method.GetCustomAttribute<GesFunctionAttribute>();
            if (functionAttribute is null) continue;
            var function = CreateFunction(extensionAttribute.Name, functionAttribute, method, out var signatureId);
            if (!functions.TryAdd(signatureId, function)) throw new ArgumentException($"GameEventScript extension function '{signatureId}' is registered more than once.", nameof(extensionType));
        }
    }

    private static IGameEventScriptExtensionFunction CreateFunction(
        string extensionName,
        GesFunctionAttribute attribute,
        MethodInfo method,
        out string signatureId)
    {
        if (!method.IsStatic)
        {
            throw new ArgumentException($"GameEventScript extension function '{method.DeclaringType?.FullName}.{method.Name}' must be static.");
        }

        if (method.ReturnType == typeof(void))
        {
            throw new ArgumentException($"GameEventScript extension function '{method.DeclaringType?.FullName}.{method.Name}' must return a extension-compatible value.");
        }

        var parameters = method.GetParameters();
        var hasContext = parameters.Length > 0 && parameters[0].ParameterType == typeof(GameEventScriptExtensionContext);
        var scriptParameters = parameters.Skip(hasContext ? 1 : 0).ToArray();
        var definitions = scriptParameters
            .Select(parameter => CreateParameterDefinition(method, parameter))
            .ToArray();
        signatureId = new GameEventScriptExtensionReference(extensionName, attribute.Name, definitions.Select(definition => definition.Name)).SignatureId;
        var readers = scriptParameters
            .Select((parameter, index) => CreateArgumentReader(method, parameter.ParameterType, definitions[index]))
            .ToArray();
        var returnConverter = CreateReturnConverter(method, attribute);

        return CreateAdapter(method, hasContext, readers, returnConverter);
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
        if (parameterType == typeof(GameEventScriptBoxedValue))
        {
            return BoxedValueArgumentReader.Instance;
        }

        if (parameterType == typeof(double))
        {
            RequireParameterKind(method, definition, GameEventScriptBytecodeTypeKind.Float, allowUnitTypes: true);
            return FloatArgumentReader.Instance;
        }

        if (parameterType == typeof(long))
        {
            RequireParameterKind(method, definition, GameEventScriptBytecodeTypeKind.Float, allowUnitTypes: false);
            return LongArgumentReader.Instance;
        }

        if (parameterType == typeof(int))
        {
            RequireParameterKind(method, definition, GameEventScriptBytecodeTypeKind.Float, allowUnitTypes: false);
            return IntArgumentReader.Instance;
        }

        if (parameterType == typeof(bool))
        {
            RequireParameterKind(method, definition, GameEventScriptBytecodeTypeKind.Boolean, allowUnitTypes: false);
            return BoolArgumentReader.Instance;
        }

        if (parameterType == typeof(GameEventScriptValue) || parameterType.IsSubclassOf(typeof(GameEventScriptValue)))
        {
            throw new ArgumentException(
                $"GameEventScript extension function '{method.DeclaringType?.FullName}.{method.Name}' parameter '{definition.Name}' cannot use boxed GameEventScriptValue types. Use GameEventScriptBoxedValue instead.");
        }

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

    private static void RequireParameterKind(
        MethodInfo method,
        ExtensionParameterDefinition definition,
        GameEventScriptBytecodeTypeKind kind,
        bool allowUnitTypes)
    {
        if (definition.Kind is null)
        {
            return;
        }

        if (definition.Kind == kind)
        {
            return;
        }

        if (allowUnitTypes && definition.Kind == GameEventScriptBytecodeTypeKind.Float)
        {
            return;
        }

        throw new ArgumentException(
            $"GameEventScript extension function '{method.DeclaringType?.FullName}.{method.Name}' parameter '{definition.Name}' declares ':{definition.TypeName}' but CLR parameter requires '{kind}'.");
    }

    private static object CreateReturnConverter(MethodInfo method, GesFunctionAttribute attribute)
    {
        var returnType = method.ReturnType;
        if (returnType == typeof(GameEventScriptBoxedValue))
        {
            return BoxedValueReturnConverter.Instance;
        }

        if (typeof(GameEventScriptValue).IsAssignableFrom(returnType))
        {
            throw new ArgumentException(
                $"GameEventScript extension function '{method.DeclaringType?.FullName}.{method.Name}' return type '{returnType.FullName}' cannot use boxed GameEventScriptValue types. Use GameEventScriptBoxedValue instead.");
        }

        if (returnType == typeof(double))
        {
            RequireReturnKind(method, attribute, GameEventScriptBytecodeTypeKind.Float);
            return new FloatReturnConverter(attribute.ReturnUnit);
        }

        if (returnType == typeof(long))
        {
            RequireReturnKind(method, attribute, GameEventScriptBytecodeTypeKind.Float);
            return new LongReturnConverter(attribute.ReturnUnit);
        }

        if (returnType == typeof(int))
        {
            RequireReturnKind(method, attribute, GameEventScriptBytecodeTypeKind.Float);
            return new IntReturnConverter(attribute.ReturnUnit);
        }

        if (returnType == typeof(bool))
        {
            RequireReturnKind(method, attribute, GameEventScriptBytecodeTypeKind.Boolean);
            return BoolReturnConverter.Instance;
        }

        if (returnType == typeof(ValueTuple<double, GameEventScriptBytecodeInstructionUnit>))
        {
            return FloatNumericUnitTupleReturnConverter.Instance;
        }

        if (returnType == typeof(ValueTuple<double, GameEventScriptBytecodeInstructionUnit?>))
        {
            return FloatNullableNumericUnitTupleReturnConverter.Instance;
        }

        throw new ArgumentException($"GameEventScript extension function '{method.DeclaringType?.FullName}.{method.Name}' return type '{returnType.FullName}' is not extension-compatible.");
    }

    private static void RequireReturnKind(MethodInfo method, GesFunctionAttribute attribute, GameEventScriptBytecodeTypeKind kind)
    {
        if (attribute.ReturnKind is not null && attribute.ReturnKind != kind)
        {
            throw new ArgumentException(
                $"GameEventScript extension function '{method.DeclaringType?.FullName}.{method.Name}' return annotation ':{attribute.ReturnTypeName}' is incompatible with CLR return type '{method.ReturnType.FullName}'.");
        }
    }

    private static IGameEventScriptExtensionFunction CreateAdapter(MethodInfo method, bool hasContext, object[] readers, object returnConverter)
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
            ? new[] { typeof(GameEventScriptExtensionContext) }.Concat(scriptParameterTypes).Append(returnType).ToArray()
            : scriptParameterTypes.Append(returnType).ToArray();
        var delegateType = GetFuncType(delegateTypes);
        var functionDelegate = method.CreateDelegate(delegateType);
        var adapterType = GetAdapterType(readers.Length, hasContext).MakeGenericType(scriptParameterTypes.Append(returnType).ToArray());
        var args = new object[2 + readers.Length];
        args[0] = functionDelegate;
        Array.Copy(readers, 0, args, 1, readers.Length);
        args[^1] = returnConverter;
        return (IGameEventScriptExtensionFunction)Activator.CreateInstance(adapterType, args)!;
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
        bool TryRead(GameEventScriptBoxedValue input, out T value);
    }

    private interface IExtensionReturnConverter<T>
    {
        GameEventScriptBoxedValue Convert(T value);
    }

    private sealed class ExtensionParameterDefinition
    {
        public ExtensionParameterDefinition(string name, string typeName)
        {
            Name = NormalizeName(name);
            TypeName = GameEventScriptExternalTypeNames.NormalizeTypeName(typeName);
            var (kind, unit) = GameEventScriptExternalTypeNames.GetKindAndUnit(TypeName);
            Kind = kind;
            Unit = unit.ToStoredUnit();
        }

        public ExtensionParameterDefinition(string name, GameEventScriptBytecodeTypeKind kind, GameEventScriptBytecodeInstructionUnit? unit)
        {
            Name = NormalizeName(name);
            TypeName = GameEventScriptExternalTypeNames.ToTypeName(kind, unit);
            Kind = kind;
            Unit = unit.ToStoredUnit();
        }

        public string Name { get; }

        public string TypeName { get; }

        public GameEventScriptBytecodeTypeKind? Kind { get; }

        public GameEventScriptBytecodeInstructionUnit Unit { get; }

        private static string NormalizeName(string name)
            => string.Equals(name, GameEventScriptMessageSignature.UnlabeledParameterName, StringComparison.Ordinal)
                ? GameEventScriptMessageSignature.UnlabeledParameterName
                : GameEventScriptExternalTypeNames.NormalizeIdentifier(name, nameof(name));
    }

    private sealed class BoxedValueArgumentReader : IExtensionArgumentReader<GameEventScriptBoxedValue>
    {
        public static readonly BoxedValueArgumentReader Instance = new();

        public bool TryRead(GameEventScriptBoxedValue input, out GameEventScriptBoxedValue value)
        {
            value = input;
            return true;
        }
    }

    private sealed class FloatArgumentReader : IExtensionArgumentReader<double>
    {
        public static readonly FloatArgumentReader Instance = new();

        public bool TryRead(GameEventScriptBoxedValue input, out double value)
        {
            if (input.Kind is GameEventScriptBytecodeTypeKind.Integer or GameEventScriptBytecodeTypeKind.Float or GameEventScriptBytecodeTypeKind.Percentage)
            {
                value = input.Number;
                return true;
            }

            value = 0d;
            return false;
        }
    }

    private sealed class LongArgumentReader : IExtensionArgumentReader<long>
    {
        public static readonly LongArgumentReader Instance = new();

        public bool TryRead(GameEventScriptBoxedValue input, out long value)
        {
            if (input.Kind is GameEventScriptBytecodeTypeKind.Integer or GameEventScriptBytecodeTypeKind.Float or GameEventScriptBytecodeTypeKind.Percentage or GameEventScriptBytecodeTypeKind.Boolean)
            {
                value = input.Integer;
                return true;
            }

            value = 0;
            return false;
        }
    }

    private sealed class IntArgumentReader : IExtensionArgumentReader<int>
    {
        public static readonly IntArgumentReader Instance = new();

        public bool TryRead(GameEventScriptBoxedValue input, out int value)
        {
            if (LongArgumentReader.Instance.TryRead(input, out var integer) &&
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

        public bool TryRead(GameEventScriptBoxedValue input, out bool value)
        {
            if (input.Kind is GameEventScriptBytecodeTypeKind.Boolean or GameEventScriptBytecodeTypeKind.Integer or GameEventScriptBytecodeTypeKind.Float or GameEventScriptBytecodeTypeKind.Percentage
                or GameEventScriptBytecodeTypeKind.Vector or GameEventScriptBytecodeTypeKind.Point)
            {
                value = input.Boolean;
                return true;
            }

            value = false;
            return false;
        }
    }

    private sealed class ExternalObjectArgumentReader<T> : IExtensionArgumentReader<T>
    {
        public bool TryRead(GameEventScriptBoxedValue input, out T value)
            => input.TryGetExternalObject(out value);
    }

    private sealed class BoxedValueReturnConverter : IExtensionReturnConverter<GameEventScriptBoxedValue>
    {
        public static readonly BoxedValueReturnConverter Instance = new();

        public GameEventScriptBoxedValue Convert(GameEventScriptBoxedValue value) => value;
    }

    private sealed class FloatReturnConverter(GameEventScriptBytecodeInstructionUnit? unit) : IExtensionReturnConverter<double>
    {
        public GameEventScriptBoxedValue Convert(double value) => GameEventScriptBoxedValue.FromFloat(value, unit.ToStoredUnit());
    }

    private sealed class LongReturnConverter(GameEventScriptBytecodeInstructionUnit? unit) : IExtensionReturnConverter<long>
    {
        public GameEventScriptBoxedValue Convert(long value) => GameEventScriptBoxedValue.FromInteger(value, unit.ToStoredUnit());
    }

    private sealed class IntReturnConverter(GameEventScriptBytecodeInstructionUnit? unit) : IExtensionReturnConverter<int>
    {
        public GameEventScriptBoxedValue Convert(int value) => GameEventScriptBoxedValue.FromInteger(value, unit.ToStoredUnit());
    }

    private sealed class BoolReturnConverter : IExtensionReturnConverter<bool>
    {
        public static readonly BoolReturnConverter Instance = new();

        public GameEventScriptBoxedValue Convert(bool value) => GameEventScriptBoxedValue.FromBoolean(value);
    }

    private sealed class FloatNumericUnitTupleReturnConverter : IExtensionReturnConverter<(double Value, GameEventScriptBytecodeInstructionUnit Unit)>
    {
        public static readonly FloatNumericUnitTupleReturnConverter Instance = new();

        public GameEventScriptBoxedValue Convert((double Value, GameEventScriptBytecodeInstructionUnit Unit) value)
            => GameEventScriptBoxedValue.FromFloat(value.Value, value.Unit);
    }

    private sealed class FloatNullableNumericUnitTupleReturnConverter : IExtensionReturnConverter<(double Value, GameEventScriptBytecodeInstructionUnit? Unit)>
    {
        public static readonly FloatNullableNumericUnitTupleReturnConverter Instance = new();

        public GameEventScriptBoxedValue Convert((double Value, GameEventScriptBytecodeInstructionUnit? Unit) value)
            => GameEventScriptBoxedValue.FromFloat(value.Value, value.Unit.ToStoredUnit());
    }

    private sealed class AnnotatedExtensionFunction0<R>(Func<R> invoke, IExtensionReturnConverter<R> returnConverter) : IGameEventScriptExtensionFunction
    {
        public GameEventScriptBoxedValue Invoke(GameEventScriptExtensionContext context, ReadOnlySpan<GameEventScriptBoxedValue> arguments)
            => arguments.Length == 0 ? returnConverter.Convert(invoke()) : GameEventScriptBoxedValue.Nothing();
    }

    private sealed class AnnotatedExtensionFunction1<T1, R>(Func<T1, R> invoke, IExtensionArgumentReader<T1> reader1, IExtensionReturnConverter<R> returnConverter) : IGameEventScriptExtensionFunction
    {
        public GameEventScriptBoxedValue Invoke(GameEventScriptExtensionContext context, ReadOnlySpan<GameEventScriptBoxedValue> arguments)
            => arguments.Length == 1 && reader1.TryRead(arguments[0], out var value1)
                ? returnConverter.Convert(invoke(value1))
                : GameEventScriptBoxedValue.Nothing();
    }

    private sealed class AnnotatedExtensionFunction2<T1, T2, R>(Func<T1, T2, R> invoke, IExtensionArgumentReader<T1> reader1, IExtensionArgumentReader<T2> reader2, IExtensionReturnConverter<R> returnConverter)
        : IGameEventScriptExtensionFunction
    {
        public GameEventScriptBoxedValue Invoke(GameEventScriptExtensionContext context, ReadOnlySpan<GameEventScriptBoxedValue> arguments)
            => arguments.Length == 2 &&
               reader1.TryRead(arguments[0], out var value1) &&
               reader2.TryRead(arguments[1], out var value2)
                ? returnConverter.Convert(invoke(value1, value2))
                : GameEventScriptBoxedValue.Nothing();
    }

    private sealed class AnnotatedExtensionFunction3<T1, T2, T3, R>(
        Func<T1, T2, T3, R> invoke,
        IExtensionArgumentReader<T1> reader1,
        IExtensionArgumentReader<T2> reader2,
        IExtensionArgumentReader<T3> reader3,
        IExtensionReturnConverter<R> returnConverter) : IGameEventScriptExtensionFunction
    {
        public GameEventScriptBoxedValue Invoke(GameEventScriptExtensionContext context, ReadOnlySpan<GameEventScriptBoxedValue> arguments)
            => arguments.Length == 3 &&
               reader1.TryRead(arguments[0], out var value1) &&
               reader2.TryRead(arguments[1], out var value2) &&
               reader3.TryRead(arguments[2], out var value3)
                ? returnConverter.Convert(invoke(value1, value2, value3))
                : GameEventScriptBoxedValue.Nothing();
    }

    private sealed class AnnotatedExtensionFunction4<T1, T2, T3, T4, R>(
        Func<T1, T2, T3, T4, R> invoke,
        IExtensionArgumentReader<T1> reader1,
        IExtensionArgumentReader<T2> reader2,
        IExtensionArgumentReader<T3> reader3,
        IExtensionArgumentReader<T4> reader4,
        IExtensionReturnConverter<R> returnConverter) : IGameEventScriptExtensionFunction
    {
        public GameEventScriptBoxedValue Invoke(GameEventScriptExtensionContext context, ReadOnlySpan<GameEventScriptBoxedValue> arguments)
            => arguments.Length == 4 &&
               reader1.TryRead(arguments[0], out var value1) &&
               reader2.TryRead(arguments[1], out var value2) &&
               reader3.TryRead(arguments[2], out var value3) &&
               reader4.TryRead(arguments[3], out var value4)
                ? returnConverter.Convert(invoke(value1, value2, value3, value4))
                : GameEventScriptBoxedValue.Nothing();
    }

    private sealed class AnnotatedContextExtensionFunction0<R>(Func<GameEventScriptExtensionContext, R> invoke, IExtensionReturnConverter<R> returnConverter) : IGameEventScriptExtensionFunction
    {
        public GameEventScriptBoxedValue Invoke(GameEventScriptExtensionContext context, ReadOnlySpan<GameEventScriptBoxedValue> arguments)
            => arguments.Length == 0 ? returnConverter.Convert(invoke(context)) : GameEventScriptBoxedValue.Nothing();
    }

    private sealed class AnnotatedContextExtensionFunction1<T1, R>(Func<GameEventScriptExtensionContext, T1, R> invoke, IExtensionArgumentReader<T1> reader1, IExtensionReturnConverter<R> returnConverter)
        : IGameEventScriptExtensionFunction
    {
        public GameEventScriptBoxedValue Invoke(GameEventScriptExtensionContext context, ReadOnlySpan<GameEventScriptBoxedValue> arguments)
            => arguments.Length == 1 && reader1.TryRead(arguments[0], out var value1)
                ? returnConverter.Convert(invoke(context, value1))
                : GameEventScriptBoxedValue.Nothing();
    }

    private sealed class AnnotatedContextExtensionFunction2<T1, T2, R>(
        Func<GameEventScriptExtensionContext, T1, T2, R> invoke,
        IExtensionArgumentReader<T1> reader1,
        IExtensionArgumentReader<T2> reader2,
        IExtensionReturnConverter<R> returnConverter) : IGameEventScriptExtensionFunction
    {
        public GameEventScriptBoxedValue Invoke(GameEventScriptExtensionContext context, ReadOnlySpan<GameEventScriptBoxedValue> arguments)
            => arguments.Length == 2 &&
               reader1.TryRead(arguments[0], out var value1) &&
               reader2.TryRead(arguments[1], out var value2)
                ? returnConverter.Convert(invoke(context, value1, value2))
                : GameEventScriptBoxedValue.Nothing();
    }

    private sealed class AnnotatedContextExtensionFunction3<T1, T2, T3, R>(
        Func<GameEventScriptExtensionContext, T1, T2, T3, R> invoke,
        IExtensionArgumentReader<T1> reader1,
        IExtensionArgumentReader<T2> reader2,
        IExtensionArgumentReader<T3> reader3,
        IExtensionReturnConverter<R> returnConverter) : IGameEventScriptExtensionFunction
    {
        public GameEventScriptBoxedValue Invoke(GameEventScriptExtensionContext context, ReadOnlySpan<GameEventScriptBoxedValue> arguments)
            => arguments.Length == 3 &&
               reader1.TryRead(arguments[0], out var value1) &&
               reader2.TryRead(arguments[1], out var value2) &&
               reader3.TryRead(arguments[2], out var value3)
                ? returnConverter.Convert(invoke(context, value1, value2, value3))
                : GameEventScriptBoxedValue.Nothing();
    }

    private sealed class AnnotatedContextExtensionFunction4<T1, T2, T3, T4, R>(
        Func<GameEventScriptExtensionContext, T1, T2, T3, T4, R> invoke,
        IExtensionArgumentReader<T1> reader1,
        IExtensionArgumentReader<T2> reader2,
        IExtensionArgumentReader<T3> reader3,
        IExtensionArgumentReader<T4> reader4,
        IExtensionReturnConverter<R> returnConverter) : IGameEventScriptExtensionFunction
    {
        public GameEventScriptBoxedValue Invoke(GameEventScriptExtensionContext context, ReadOnlySpan<GameEventScriptBoxedValue> arguments)
            => arguments.Length == 4 &&
               reader1.TryRead(arguments[0], out var value1) &&
               reader2.TryRead(arguments[1], out var value2) &&
               reader3.TryRead(arguments[2], out var value3) &&
               reader4.TryRead(arguments[3], out var value4)
                ? returnConverter.Convert(invoke(context, value1, value2, value3, value4))
                : GameEventScriptBoxedValue.Nothing();
    }
}

public sealed class GameEventScriptEmptyExtensionRegistry : IGameEventScriptExtensionRegistry
{
    public static readonly GameEventScriptEmptyExtensionRegistry Instance = new();

    private GameEventScriptEmptyExtensionRegistry()
    {
    }

    public bool TryResolve(GameEventScriptExtensionReference reference, out IGameEventScriptExtensionFunction function)
    {
        function = default!;
        return false;
    }
}
