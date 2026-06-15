#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using StepH.GameEventScript.Api;
using StepH.GameEventScript.Types;
using static StepH.GameEventScript.Api.GameEventScriptValueFactory;

namespace StepH.GameEventScript.Runtime;

public interface IGameEventScriptExtensionRegistry
{
    bool TryResolve(GameEventScriptExtensionReference reference, out IGameEventScriptExtensionFunction function);
}

public interface IGameEventScriptExtensionFunction
{
    GameEventScriptFastValue Invoke(GameEventScriptExtensionContext context, ReadOnlySpan<GameEventScriptFastValue> arguments);
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
    public GesFunctionAttribute(string name, GameEventScriptValueKind returnKind) : this(name)
    {
        ReturnTypeName = GameEventScriptExternalTypeNames.ToTypeName(returnKind, unit: null);
        ReturnKind = returnKind;
    }

    public GesFunctionAttribute(string name, GameEventScriptValueKind returnKind, GameEventScriptBytecodeInstructionUnit unit) : this(name)
    {
        ReturnTypeName = GameEventScriptExternalTypeNames.ToTypeName(returnKind, unit);
        ReturnKind = returnKind;
        ReturnUnit = unit.ToStoredUnit();
    }

    public string Name { get; } = GameEventScriptExternalTypeNames.NormalizeIdentifier(name, nameof(name));

    public string? ReturnTypeName { get; }

    public GameEventScriptValueKind? ReturnKind { get; }

    public GameEventScriptBytecodeInstructionUnit ReturnUnit { get; }
}

public sealed class GameEventScriptExtensionContext(GameEventScriptSession runtimeSession)
{
    public GameEventScriptSession RuntimeSession { get; } = runtimeSession ?? throw new ArgumentNullException(nameof(runtimeSession));

    public GameEventScriptRandomGenerator Random => RuntimeSession.Random;

    public GameEventScriptRuntimeLimits RuntimeLimits => RuntimeSession.RuntimeLimits;
}

public readonly struct GameEventScriptFastValue
{
    private readonly GameEventScriptValue? _reference;

    private GameEventScriptFastValue(GameEventScriptValueKind kind, long integer, double number, double x, double y, double z, bool boolean, bool isIntegerNumber, GameEventScriptBytecodeInstructionUnit? unit, GameEventScriptValue? reference)
    {
        Kind = kind;
        IntegerValue = integer;
        NumberValue = number;
        X = x;
        Y = y;
        Z = z;
        BooleanValue = boolean;
        IsIntegerNumber = isIntegerNumber;
        Unit = unit.ToStoredUnit();
        _reference = reference;
    }

    public GameEventScriptValueKind Kind { get; }

    public long Integer => Kind switch
    {
        GameEventScriptValueKind.Number => IsReferenceBacked ? ToGameEventScriptValue().AsInteger() : IsIntegerNumber ? IntegerValue : GesValueOperations.ToIntegerSaturated(NumberValue),
        GameEventScriptValueKind.Percentage => ToIntegerPercentage(NumberValue),
        GameEventScriptValueKind.Boolean => BooleanValue ? 1 : 0,
        _ => ToGameEventScriptValue().AsInteger()
    };

    public double Number => Kind switch
    {
        GameEventScriptValueKind.Number => IsReferenceBacked ? ToGameEventScriptValue().AsNumber() : IsIntegerNumber ? IntegerValue : NumberValue,
        GameEventScriptValueKind.Percentage => IsReferenceBacked ? ToGameEventScriptValue().AsNumber() : NumberValue,
        GameEventScriptValueKind.Boolean => BooleanValue ? 1d : 0d,
        _ => ToGameEventScriptValue().AsNumber()
    };

    public bool Boolean => Kind switch
    {
        GameEventScriptValueKind.Boolean => BooleanValue,
        GameEventScriptValueKind.Number => IsReferenceBacked ? ToGameEventScriptValue().AsBoolean() : IsIntegerNumber ? IntegerValue != 0 : NumberValue != 0d,
        GameEventScriptValueKind.Percentage => IsReferenceBacked ? ToGameEventScriptValue().AsBoolean() : NumberValue != 0d,
        GameEventScriptValueKind.Vector or GameEventScriptValueKind.Point => X != 0d || Y != 0d || Z != 0d,
        _ => ToGameEventScriptValue().AsBoolean()
    };

    public string Text => ToGameEventScriptValue().AsText();

    public GameEventScriptBytecodeInstructionUnit Unit { get; }

    public double X { get; }

    public double Y { get; }

    public double Z { get; }

    public bool IsReferenceBacked => _reference is not null;

    public bool IsIntegerNumber { get; }

    private long IntegerValue { get; }

    private double NumberValue { get; }

    private bool BooleanValue { get; }

    public static GameEventScriptFastValue Nothing { get; } = new(GameEventScriptValueKind.Nothing, 0, 0d, 0d, 0d, 0d, false, false, null, null);

    public static GameEventScriptFastValue FromGameEventScriptValue(GameEventScriptValue value) => value switch
    {
        null => Nothing,
        _ when value.IsNothing() => Nothing,
        GameEventScriptBooleanValue boolean => FromBoolean(boolean.Value),
        GameEventScriptNumberValue { IsIntegerValue: true } integer => FromInteger(integer.IntegerValue, integer.Unit),
        GameEventScriptNumberValue numberValue when numberValue.HasSemanticValue() => FromFloat(numberValue.NumberValue, numberValue.Unit),
        { Kind: GameEventScriptValueKind.Percentage } => FromPercentage(value.AsNumber()),
        GameEventScriptVectorValue vector => FromVector(vector.X, vector.Y, vector.Z, vector.Unit),
        GameEventScriptPointValue point => FromPoint(point.X, point.Y, point.Z, point.Unit),
        _ => new GameEventScriptFastValue(value.Kind, 0, 0d, 0d, 0d, 0d, value.AsBoolean(), false, null, value)
    };

    public static GameEventScriptFastValue FromBoolean(bool value)
        => new(GameEventScriptValueKind.Boolean, value ? 1 : 0, value ? 1d : 0d, 0d, 0d, 0d, value, false, null, null);

    public static GameEventScriptFastValue FromInteger(long value, GameEventScriptBytecodeInstructionUnit? unit = null)
        => new(GameEventScriptValueKind.Number, value, value, 0d, 0d, 0d, value != 0, true, unit, null);

    public static GameEventScriptFastValue FromFloat(double value, GameEventScriptBytecodeInstructionUnit? unit = null)
        => double.IsFinite(value) &&
           value >= long.MinValue &&
           value <= long.MaxValue &&
           value == Math.Truncate(value)
            ? FromInteger((long)value, unit)
            : new GameEventScriptFastValue(GameEventScriptValueKind.Number, GesValueOperations.ToIntegerSaturated(value), value, 0d, 0d, 0d, value != 0d, false, unit, null);

    public static GameEventScriptFastValue FromPercentage(double ratio)
        => new(GameEventScriptValueKind.Percentage, ToIntegerPercentage(ratio), ratio, 0d, 0d, 0d, ratio != 0d, false, null, null);

    public static GameEventScriptFastValue FromVector(double x, double y = 0d, double z = 0d, GameEventScriptBytecodeInstructionUnit? unit = null)
        => new(GameEventScriptValueKind.Vector, 0, 0d, x, y, z, x != 0d || y != 0d || z != 0d, false, unit, null);

    public static GameEventScriptFastValue FromPoint(double x, double y = 0d, double z = 0d, GameEventScriptBytecodeInstructionUnit? unit = null)
        => new(GameEventScriptValueKind.Point, 0, 0d, x, y, z, x != 0d || y != 0d || z != 0d, false, unit, null);

    public static GameEventScriptFastValue FromText(string value) => FromGameEventScriptValue(GesText(value));

    private static long ToIntegerPercentage(double ratio) => GesValueOperations.ToIntegerSaturated(Math.Truncate(ratio * 100d));

    public GameEventScriptValue ToGameEventScriptValue()
    {
        if (_reference is not null) return _reference;
        return Kind switch
        {
            GameEventScriptValueKind.Nothing => GesNothing(),
            GameEventScriptValueKind.Boolean => GesBoolean(BooleanValue),
            GameEventScriptValueKind.Number => IsIntegerNumber ? GesInteger(IntegerValue, Unit) : GesFloat(NumberValue, Unit),
            GameEventScriptValueKind.Percentage => GesPercentage(NumberValue),
            GameEventScriptValueKind.Vector => GesVector(X, Y, Z, Unit),
            GameEventScriptValueKind.Point => GesPoint(X, Y, Z, Unit),
            _ => GesNothing()
        };
    }

    public bool TryGetExternalObject<T>(out T value)
    {
        if (_reference is not null) return _reference.TryGetExternalObject(out value);
        value = default!;
        return false;
    }

    public bool TryGetExternalObject(Type objectType, out object value)
    {
        if (_reference is not null) return _reference.TryGetExternalObject(objectType, out value);
        value = default!;
        return false;
    }
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
            throw new ArgumentException($"GameEventScript extension function '{method.DeclaringType?.FullName}.{method.Name}' must return a fast-compatible value.");
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
        if (parameterType == typeof(GameEventScriptFastValue))
        {
            return FastValueArgumentReader.Instance;
        }

        if (parameterType == typeof(double))
        {
            RequireParameterKind(method, definition, GameEventScriptValueKind.Number, allowUnitTypes: true);
            return FloatArgumentReader.Instance;
        }

        if (parameterType == typeof(long))
        {
            RequireParameterKind(method, definition, GameEventScriptValueKind.Number, allowUnitTypes: false);
            return LongArgumentReader.Instance;
        }

        if (parameterType == typeof(int))
        {
            RequireParameterKind(method, definition, GameEventScriptValueKind.Number, allowUnitTypes: false);
            return IntArgumentReader.Instance;
        }

        if (parameterType == typeof(bool))
        {
            RequireParameterKind(method, definition, GameEventScriptValueKind.Boolean, allowUnitTypes: false);
            return BoolArgumentReader.Instance;
        }

        if (parameterType == typeof(GameEventScriptValue) || parameterType.IsSubclassOf(typeof(GameEventScriptValue)))
        {
            throw new ArgumentException(
                $"GameEventScript extension function '{method.DeclaringType?.FullName}.{method.Name}' parameter '{definition.Name}' cannot use boxed GameEventScriptValue types. Use GameEventScriptFastValue instead.");
        }

        if (parameterType.IsValueType)
        {
            throw new ArgumentException(
                $"GameEventScript extension function '{method.DeclaringType?.FullName}.{method.Name}' parameter '{definition.Name}' type '{parameterType.FullName}' is not fast-compatible.");
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
        GameEventScriptValueKind kind,
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

        if (allowUnitTypes && definition.Kind == GameEventScriptValueKind.Number)
        {
            return;
        }

        throw new ArgumentException(
            $"GameEventScript extension function '{method.DeclaringType?.FullName}.{method.Name}' parameter '{definition.Name}' declares ':{definition.TypeName}' but CLR parameter requires '{kind}'.");
    }

    private static object CreateReturnConverter(MethodInfo method, GesFunctionAttribute attribute)
    {
        var returnType = method.ReturnType;
        if (returnType == typeof(GameEventScriptFastValue))
        {
            return FastValueReturnConverter.Instance;
        }

        if (typeof(GameEventScriptValue).IsAssignableFrom(returnType))
        {
            return Activator.CreateInstance(typeof(GameEventScriptValueReturnConverter<>).MakeGenericType(returnType))!;
        }

        if (typeof(IGameEventScriptSeries).IsAssignableFrom(returnType))
        {
            RequireReturnKind(method, attribute, GameEventScriptValueKind.Series);
            return Activator.CreateInstance(typeof(SeriesReturnConverter<>).MakeGenericType(returnType))!;
        }

        if (returnType == typeof(double))
        {
            RequireReturnKind(method, attribute, GameEventScriptValueKind.Number);
            return new FloatReturnConverter(attribute.ReturnUnit);
        }

        if (returnType == typeof(long))
        {
            RequireReturnKind(method, attribute, GameEventScriptValueKind.Number);
            return new LongReturnConverter(attribute.ReturnUnit);
        }

        if (returnType == typeof(int))
        {
            RequireReturnKind(method, attribute, GameEventScriptValueKind.Number);
            return new IntReturnConverter(attribute.ReturnUnit);
        }

        if (returnType == typeof(bool))
        {
            RequireReturnKind(method, attribute, GameEventScriptValueKind.Boolean);
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

        throw new ArgumentException($"GameEventScript extension function '{method.DeclaringType?.FullName}.{method.Name}' return type '{returnType.FullName}' is not fast-compatible.");
    }

    private static void RequireReturnKind(MethodInfo method, GesFunctionAttribute attribute, GameEventScriptValueKind kind)
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
                $"GameEventScript extension function '{method.DeclaringType?.FullName}.{method.Name}' has {readers.Length} script parameter(s); annotated fast extensions currently support up to 4.");
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

    private interface IFastArgumentReader<T>
    {
        bool TryRead(GameEventScriptFastValue input, out T value);
    }

    private interface IFastReturnConverter<T>
    {
        GameEventScriptFastValue Convert(T value);
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

        public ExtensionParameterDefinition(string name, GameEventScriptValueKind kind, GameEventScriptBytecodeInstructionUnit? unit)
        {
            Name = NormalizeName(name);
            TypeName = GameEventScriptExternalTypeNames.ToTypeName(kind, unit);
            Kind = kind;
            Unit = unit.ToStoredUnit();
        }

        public string Name { get; }

        public string TypeName { get; }

        public GameEventScriptValueKind? Kind { get; }

        public GameEventScriptBytecodeInstructionUnit Unit { get; }

        private static string NormalizeName(string name)
            => string.Equals(name, GameEventScriptMessageSignature.UnlabeledParameterName, StringComparison.Ordinal)
                ? GameEventScriptMessageSignature.UnlabeledParameterName
                : GameEventScriptExternalTypeNames.NormalizeIdentifier(name, nameof(name));
    }

    private sealed class FastValueArgumentReader : IFastArgumentReader<GameEventScriptFastValue>
    {
        public static readonly FastValueArgumentReader Instance = new();

        public bool TryRead(GameEventScriptFastValue input, out GameEventScriptFastValue value)
        {
            value = input;
            return true;
        }
    }

    private sealed class FloatArgumentReader : IFastArgumentReader<double>
    {
        public static readonly FloatArgumentReader Instance = new();

        public bool TryRead(GameEventScriptFastValue input, out double value)
        {
            if (input.Kind is GameEventScriptValueKind.Number or GameEventScriptValueKind.Percentage)
            {
                value = input.Number;
                return true;
            }

            value = 0d;
            return false;
        }
    }

    private sealed class LongArgumentReader : IFastArgumentReader<long>
    {
        public static readonly LongArgumentReader Instance = new();

        public bool TryRead(GameEventScriptFastValue input, out long value)
        {
            if (input.Kind is GameEventScriptValueKind.Number or GameEventScriptValueKind.Percentage or GameEventScriptValueKind.Boolean)
            {
                value = input.Integer;
                return true;
            }

            value = 0;
            return false;
        }
    }

    private sealed class IntArgumentReader : IFastArgumentReader<int>
    {
        public static readonly IntArgumentReader Instance = new();

        public bool TryRead(GameEventScriptFastValue input, out int value)
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

    private sealed class BoolArgumentReader : IFastArgumentReader<bool>
    {
        public static readonly BoolArgumentReader Instance = new();

        public bool TryRead(GameEventScriptFastValue input, out bool value)
        {
            if (input.Kind is GameEventScriptValueKind.Boolean or GameEventScriptValueKind.Number or GameEventScriptValueKind.Percentage
                or GameEventScriptValueKind.Vector or GameEventScriptValueKind.Point)
            {
                value = input.Boolean;
                return true;
            }

            value = false;
            return false;
        }
    }

    private sealed class ExternalObjectArgumentReader<T> : IFastArgumentReader<T>
    {
        public bool TryRead(GameEventScriptFastValue input, out T value)
            => input.TryGetExternalObject(out value);
    }

    private sealed class FastValueReturnConverter : IFastReturnConverter<GameEventScriptFastValue>
    {
        public static readonly FastValueReturnConverter Instance = new();

        public GameEventScriptFastValue Convert(GameEventScriptFastValue value) => value;
    }

    private sealed class GameEventScriptValueReturnConverter<T> : IFastReturnConverter<T>
        where T : GameEventScriptValue
    {
        public GameEventScriptFastValue Convert(T value)
            => GameEventScriptFastValue.FromGameEventScriptValue(value is null ? GameEventScriptNothingValue.Instance : value);
    }

    private sealed class SeriesReturnConverter<T> : IFastReturnConverter<T>
        where T : IGameEventScriptSeries
    {
        public GameEventScriptFastValue Convert(T value)
            => GameEventScriptFastValue.FromGameEventScriptValue(value is null
                ? GameEventScriptNothingValue.Instance
                : GameEventScriptValueFactory.GesSeries(value));
    }

    private sealed class FloatReturnConverter(GameEventScriptBytecodeInstructionUnit? unit) : IFastReturnConverter<double>
    {
        public GameEventScriptFastValue Convert(double value) => GameEventScriptFastValue.FromFloat(value, unit);
    }

    private sealed class LongReturnConverter(GameEventScriptBytecodeInstructionUnit? unit) : IFastReturnConverter<long>
    {
        public GameEventScriptFastValue Convert(long value) => GameEventScriptFastValue.FromInteger(value, unit);
    }

    private sealed class IntReturnConverter(GameEventScriptBytecodeInstructionUnit? unit) : IFastReturnConverter<int>
    {
        public GameEventScriptFastValue Convert(int value) => GameEventScriptFastValue.FromInteger(value, unit);
    }

    private sealed class BoolReturnConverter : IFastReturnConverter<bool>
    {
        public static readonly BoolReturnConverter Instance = new();

        public GameEventScriptFastValue Convert(bool value) => GameEventScriptFastValue.FromBoolean(value);
    }

    private sealed class FloatNumericUnitTupleReturnConverter : IFastReturnConverter<(double Value, GameEventScriptBytecodeInstructionUnit Unit)>
    {
        public static readonly FloatNumericUnitTupleReturnConverter Instance = new();

        public GameEventScriptFastValue Convert((double Value, GameEventScriptBytecodeInstructionUnit Unit) value)
            => GameEventScriptFastValue.FromFloat(value.Value, value.Unit);
    }

    private sealed class FloatNullableNumericUnitTupleReturnConverter : IFastReturnConverter<(double Value, GameEventScriptBytecodeInstructionUnit? Unit)>
    {
        public static readonly FloatNullableNumericUnitTupleReturnConverter Instance = new();

        public GameEventScriptFastValue Convert((double Value, GameEventScriptBytecodeInstructionUnit? Unit) value)
            => GameEventScriptFastValue.FromFloat(value.Value, value.Unit);
    }

    private sealed class AnnotatedExtensionFunction0<R>(Func<R> invoke, IFastReturnConverter<R> returnConverter) : IGameEventScriptExtensionFunction
    {
        public GameEventScriptFastValue Invoke(GameEventScriptExtensionContext context, ReadOnlySpan<GameEventScriptFastValue> arguments)
            => arguments.Length == 0 ? returnConverter.Convert(invoke()) : GameEventScriptFastValue.Nothing;
    }

    private sealed class AnnotatedExtensionFunction1<T1, R>(Func<T1, R> invoke, IFastArgumentReader<T1> reader1, IFastReturnConverter<R> returnConverter) : IGameEventScriptExtensionFunction
    {
        public GameEventScriptFastValue Invoke(GameEventScriptExtensionContext context, ReadOnlySpan<GameEventScriptFastValue> arguments)
            => arguments.Length == 1 && reader1.TryRead(arguments[0], out var value1)
                ? returnConverter.Convert(invoke(value1))
                : GameEventScriptFastValue.Nothing;
    }

    private sealed class AnnotatedExtensionFunction2<T1, T2, R>(Func<T1, T2, R> invoke, IFastArgumentReader<T1> reader1, IFastArgumentReader<T2> reader2, IFastReturnConverter<R> returnConverter)
        : IGameEventScriptExtensionFunction
    {
        public GameEventScriptFastValue Invoke(GameEventScriptExtensionContext context, ReadOnlySpan<GameEventScriptFastValue> arguments)
            => arguments.Length == 2 &&
               reader1.TryRead(arguments[0], out var value1) &&
               reader2.TryRead(arguments[1], out var value2)
                ? returnConverter.Convert(invoke(value1, value2))
                : GameEventScriptFastValue.Nothing;
    }

    private sealed class AnnotatedExtensionFunction3<T1, T2, T3, R>(
        Func<T1, T2, T3, R> invoke,
        IFastArgumentReader<T1> reader1,
        IFastArgumentReader<T2> reader2,
        IFastArgumentReader<T3> reader3,
        IFastReturnConverter<R> returnConverter) : IGameEventScriptExtensionFunction
    {
        public GameEventScriptFastValue Invoke(GameEventScriptExtensionContext context, ReadOnlySpan<GameEventScriptFastValue> arguments)
            => arguments.Length == 3 &&
               reader1.TryRead(arguments[0], out var value1) &&
               reader2.TryRead(arguments[1], out var value2) &&
               reader3.TryRead(arguments[2], out var value3)
                ? returnConverter.Convert(invoke(value1, value2, value3))
                : GameEventScriptFastValue.Nothing;
    }

    private sealed class AnnotatedExtensionFunction4<T1, T2, T3, T4, R>(
        Func<T1, T2, T3, T4, R> invoke,
        IFastArgumentReader<T1> reader1,
        IFastArgumentReader<T2> reader2,
        IFastArgumentReader<T3> reader3,
        IFastArgumentReader<T4> reader4,
        IFastReturnConverter<R> returnConverter) : IGameEventScriptExtensionFunction
    {
        public GameEventScriptFastValue Invoke(GameEventScriptExtensionContext context, ReadOnlySpan<GameEventScriptFastValue> arguments)
            => arguments.Length == 4 &&
               reader1.TryRead(arguments[0], out var value1) &&
               reader2.TryRead(arguments[1], out var value2) &&
               reader3.TryRead(arguments[2], out var value3) &&
               reader4.TryRead(arguments[3], out var value4)
                ? returnConverter.Convert(invoke(value1, value2, value3, value4))
                : GameEventScriptFastValue.Nothing;
    }

    private sealed class AnnotatedContextExtensionFunction0<R>(Func<GameEventScriptExtensionContext, R> invoke, IFastReturnConverter<R> returnConverter) : IGameEventScriptExtensionFunction
    {
        public GameEventScriptFastValue Invoke(GameEventScriptExtensionContext context, ReadOnlySpan<GameEventScriptFastValue> arguments)
            => arguments.Length == 0 ? returnConverter.Convert(invoke(context)) : GameEventScriptFastValue.Nothing;
    }

    private sealed class AnnotatedContextExtensionFunction1<T1, R>(Func<GameEventScriptExtensionContext, T1, R> invoke, IFastArgumentReader<T1> reader1, IFastReturnConverter<R> returnConverter)
        : IGameEventScriptExtensionFunction
    {
        public GameEventScriptFastValue Invoke(GameEventScriptExtensionContext context, ReadOnlySpan<GameEventScriptFastValue> arguments)
            => arguments.Length == 1 && reader1.TryRead(arguments[0], out var value1)
                ? returnConverter.Convert(invoke(context, value1))
                : GameEventScriptFastValue.Nothing;
    }

    private sealed class AnnotatedContextExtensionFunction2<T1, T2, R>(
        Func<GameEventScriptExtensionContext, T1, T2, R> invoke,
        IFastArgumentReader<T1> reader1,
        IFastArgumentReader<T2> reader2,
        IFastReturnConverter<R> returnConverter) : IGameEventScriptExtensionFunction
    {
        public GameEventScriptFastValue Invoke(GameEventScriptExtensionContext context, ReadOnlySpan<GameEventScriptFastValue> arguments)
            => arguments.Length == 2 &&
               reader1.TryRead(arguments[0], out var value1) &&
               reader2.TryRead(arguments[1], out var value2)
                ? returnConverter.Convert(invoke(context, value1, value2))
                : GameEventScriptFastValue.Nothing;
    }

    private sealed class AnnotatedContextExtensionFunction3<T1, T2, T3, R>(
        Func<GameEventScriptExtensionContext, T1, T2, T3, R> invoke,
        IFastArgumentReader<T1> reader1,
        IFastArgumentReader<T2> reader2,
        IFastArgumentReader<T3> reader3,
        IFastReturnConverter<R> returnConverter) : IGameEventScriptExtensionFunction
    {
        public GameEventScriptFastValue Invoke(GameEventScriptExtensionContext context, ReadOnlySpan<GameEventScriptFastValue> arguments)
            => arguments.Length == 3 &&
               reader1.TryRead(arguments[0], out var value1) &&
               reader2.TryRead(arguments[1], out var value2) &&
               reader3.TryRead(arguments[2], out var value3)
                ? returnConverter.Convert(invoke(context, value1, value2, value3))
                : GameEventScriptFastValue.Nothing;
    }

    private sealed class AnnotatedContextExtensionFunction4<T1, T2, T3, T4, R>(
        Func<GameEventScriptExtensionContext, T1, T2, T3, T4, R> invoke,
        IFastArgumentReader<T1> reader1,
        IFastArgumentReader<T2> reader2,
        IFastArgumentReader<T3> reader3,
        IFastArgumentReader<T4> reader4,
        IFastReturnConverter<R> returnConverter) : IGameEventScriptExtensionFunction
    {
        public GameEventScriptFastValue Invoke(GameEventScriptExtensionContext context, ReadOnlySpan<GameEventScriptFastValue> arguments)
            => arguments.Length == 4 &&
               reader1.TryRead(arguments[0], out var value1) &&
               reader2.TryRead(arguments[1], out var value2) &&
               reader3.TryRead(arguments[2], out var value3) &&
               reader4.TryRead(arguments[3], out var value4)
                ? returnConverter.Convert(invoke(context, value1, value2, value3, value4))
                : GameEventScriptFastValue.Nothing;
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
