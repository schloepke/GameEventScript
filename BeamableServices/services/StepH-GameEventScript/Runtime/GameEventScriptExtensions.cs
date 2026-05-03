#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections.Generic;
using System.Linq;
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
        ArgumentLabels = (argumentLabels ?? Array.Empty<string?>())
            .Select(GameEventScriptMessageSignature.NormalizeParameterName)
            .ToArray();
        SignatureId = $"{ExtensionName}.{FunctionName}({string.Join(",", ArgumentLabels)})";
    }

    public string ExtensionName { get; }

    public string FunctionName { get; }

    public IReadOnlyList<string> ArgumentLabels { get; }

    public string SignatureId { get; }

    private static string NormalizeName(string? name)
        => string.IsNullOrWhiteSpace(name) ? string.Empty : name.Trim();
}

public sealed class GameEventScriptExtensionContext(GameEventScriptContext runtimeContext)
{
    public GameEventScriptContext RuntimeContext { get; } = runtimeContext ?? throw new ArgumentNullException(nameof(runtimeContext));

    public GameEventScriptRandomGenerator Random => RuntimeContext.Random;

    public GameEventScriptRuntimeLimits RuntimeLimits => RuntimeContext.RuntimeLimits;
}

public readonly struct GameEventScriptFastValue
{
    private readonly GameEventScriptValue? _reference;

    private GameEventScriptFastValue(GameEventScriptValueKind kind, long integer, decimal number, decimal x, decimal y, decimal z, bool boolean, GameEventScriptDecimalUnit? unit,
        GameEventScriptValue? reference)
    {
        Kind = kind;
        IntegerValue = integer;
        NumberValue = number;
        X = x;
        Y = y;
        Z = z;
        BooleanValue = boolean;
        Unit = unit;
        _reference = reference;
    }

    public GameEventScriptValueKind Kind { get; }

    public long Integer => Kind switch
    {
        GameEventScriptValueKind.Integer => IntegerValue,
        GameEventScriptValueKind.Decimal => IsReferenceBacked ? ToGameEventScriptValue().AsInteger() : GesValueOperations.ToIntegerSaturated(NumberValue),
        GameEventScriptValueKind.Percentage => ToIntegerPercentage(NumberValue),
        GameEventScriptValueKind.Boolean => BooleanValue ? 1 : 0,
        _ => ToGameEventScriptValue().AsInteger()
    };

    public decimal Number => Kind switch
    {
        GameEventScriptValueKind.Integer => IntegerValue,
        GameEventScriptValueKind.Decimal or GameEventScriptValueKind.Percentage => IsReferenceBacked ? ToGameEventScriptValue().AsNumber() : NumberValue,
        GameEventScriptValueKind.Boolean => BooleanValue ? 1m : 0m,
        _ => ToGameEventScriptValue().AsNumber()
    };

    public bool Boolean => Kind switch
    {
        GameEventScriptValueKind.Boolean => BooleanValue,
        GameEventScriptValueKind.Integer => IntegerValue != 0,
        GameEventScriptValueKind.Decimal or GameEventScriptValueKind.Percentage => IsReferenceBacked ? ToGameEventScriptValue().AsBoolean() : NumberValue != 0m,
        GameEventScriptValueKind.Vector2 => X != 0m || Y != 0m,
        GameEventScriptValueKind.Vector3 => X != 0m || Y != 0m || Z != 0m,
        _ => ToGameEventScriptValue().AsBoolean()
    };

    public string Text => ToGameEventScriptValue().AsText();

    public GameEventScriptDecimalUnit? Unit { get; }

    public decimal X { get; }

    public decimal Y { get; }

    public decimal Z { get; }

    public bool IsReferenceBacked => _reference is not null;

    private long IntegerValue { get; }

    private decimal NumberValue { get; }

    private bool BooleanValue { get; }

    public static GameEventScriptFastValue Nothing { get; } = new(GameEventScriptValueKind.Nothing, 0, 0m, 0m, 0m, 0m, false, null, null);

    public static GameEventScriptFastValue FromGameEventScriptValue(GameEventScriptValue value) => value switch
    {
        null => Nothing,
        _ when value.IsNothing() => Nothing,
        GameEventScriptBooleanValue boolean => FromBoolean(boolean.Value),
        GameEventScriptIntegerValue integer => FromInteger(integer.Value),
        GameEventScriptDecimalValue decimalValue when decimalValue.HasSemanticValue() => FromDecimal(decimalValue.Value, decimalValue.Unit),
        GameEventScriptPercentageValue percentage => FromPercentage(percentage.Ratio),
        GameEventScriptVector2Value vector2 => FromVector2(vector2.X, vector2.Y, vector2.Unit),
        GameEventScriptVector3Value vector3 => FromVector3(vector3.X, vector3.Y, vector3.Z, vector3.Unit),
        _ => new GameEventScriptFastValue(value.Kind, 0, 0m, 0m, 0m, 0m, value.AsBoolean(), null, value)
    };

    public static GameEventScriptFastValue FromBoolean(bool value)
        => new(GameEventScriptValueKind.Boolean, value ? 1 : 0, value ? 1m : 0m, 0m, 0m, 0m, value, null, null);

    public static GameEventScriptFastValue FromInteger(long value)
        => new(GameEventScriptValueKind.Integer, value, value, 0m, 0m, 0m, value != 0, null, null);

    public static GameEventScriptFastValue FromDecimal(decimal value, GameEventScriptDecimalUnit? unit = null)
        => new(GameEventScriptValueKind.Decimal, GesValueOperations.ToIntegerSaturated(value), value, 0m, 0m, 0m, value != 0m, unit, null);

    public static GameEventScriptFastValue FromPercentage(decimal ratio)
        => new(GameEventScriptValueKind.Percentage, ToIntegerPercentage(ratio), ratio, 0m, 0m, 0m, ratio != 0m, null, null);

    public static GameEventScriptFastValue FromVector2(decimal x, decimal y, GameEventScriptDecimalUnit? unit = null)
        => new(GameEventScriptValueKind.Vector2, 0, 0m, x, y, 0m, x != 0m || y != 0m, unit, null);

    public static GameEventScriptFastValue FromVector3(decimal x, decimal y, decimal z, GameEventScriptDecimalUnit? unit = null)
        => new(GameEventScriptValueKind.Vector3, 0, 0m, x, y, z, x != 0m || y != 0m || z != 0m, unit, null);

    public static GameEventScriptFastValue FromText(string value)
        => FromGameEventScriptValue(GesText(value));

    private static long ToIntegerPercentage(decimal ratio)
        => GesValueOperations.ToIntegerSaturated(decimal.Truncate(ratio * 100m));

    public GameEventScriptValue ToGameEventScriptValue()
    {
        if (_reference is not null) return _reference;
        return Kind switch
        {
            GameEventScriptValueKind.Nothing => GesNothing(),
            GameEventScriptValueKind.Boolean => GesBoolean(BooleanValue),
            GameEventScriptValueKind.Integer => GesInteger(IntegerValue),
            GameEventScriptValueKind.Decimal => GesDecimal(NumberValue, Unit),
            GameEventScriptValueKind.Percentage => GesPercentage(NumberValue),
            GameEventScriptValueKind.Vector2 => GesVector2(X, Y, Unit),
            GameEventScriptValueKind.Vector3 => GesVector3(X, Y, Z, Unit),
            _ => GesNothing()
        };
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