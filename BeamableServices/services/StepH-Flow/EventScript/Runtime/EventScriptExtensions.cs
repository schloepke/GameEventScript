#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections.Generic;
using System.Linq;
using StepH.Flow.EventScript.Types;

namespace StepH.Flow.EventScript.Runtime;

public sealed class EventScriptExtensionReference
{
    public EventScriptExtensionReference(
        string? extensionName,
        string? functionName,
        IEnumerable<string?>? argumentLabels)
    {
        ExtensionName = NormalizeName(extensionName);
        FunctionName = NormalizeName(functionName);
        ArgumentLabels = (argumentLabels ?? Array.Empty<string?>())
            .Select(EventScriptMessageSignature.NormalizeParameterName)
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

public sealed class EventScriptExtensionContext(EventScriptContext runtimeContext)
{
    public EventScriptContext RuntimeContext { get; } = runtimeContext ?? throw new ArgumentNullException(nameof(runtimeContext));

    public EventScriptRandomGenerator Random => RuntimeContext.Random;

    public EventScriptRuntimeLimits RuntimeLimits => RuntimeContext.RuntimeLimits;
}

public readonly struct EventScriptFastValue
{
    private readonly EventScriptValue? _reference;

    private EventScriptFastValue(
        EventScriptValueKind kind,
        long integer,
        decimal number,
        decimal x,
        decimal y,
        decimal z,
        bool boolean,
        EventScriptDecimalUnit? unit,
        EventScriptValue? reference)
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

    public EventScriptValueKind Kind { get; }

    public long Integer => Kind switch
    {
        EventScriptValueKind.Integer => IntegerValue,
        EventScriptValueKind.Decimal => IsReferenceBacked
            ? ToEventScriptValue().AsInteger()
            : EventScriptValueAlu.ToIntegerSaturated(NumberValue),
        EventScriptValueKind.Percentage => ToIntegerPercentage(NumberValue),
        EventScriptValueKind.Boolean => BooleanValue ? 1 : 0,
        _ => ToEventScriptValue().AsInteger()
    };

    public decimal Number => Kind switch
    {
        EventScriptValueKind.Integer => IntegerValue,
        EventScriptValueKind.Decimal or EventScriptValueKind.Percentage => IsReferenceBacked
            ? ToEventScriptValue().AsNumber()
            : NumberValue,
        EventScriptValueKind.Boolean => BooleanValue ? 1m : 0m,
        _ => ToEventScriptValue().AsNumber()
    };

    public bool Boolean => Kind switch
    {
        EventScriptValueKind.Boolean => BooleanValue,
        EventScriptValueKind.Integer => IntegerValue != 0,
        EventScriptValueKind.Decimal or EventScriptValueKind.Percentage => IsReferenceBacked
            ? ToEventScriptValue().AsBoolean()
            : NumberValue != 0m,
        EventScriptValueKind.Vector2 => X != 0m || Y != 0m,
        EventScriptValueKind.Vector3 => X != 0m || Y != 0m || Z != 0m,
        _ => ToEventScriptValue().AsBoolean()
    };

    public string Text => ToEventScriptValue().AsText();

    public EventScriptDecimalUnit? Unit { get; }

    public decimal X { get; }

    public decimal Y { get; }

    public decimal Z { get; }

    public bool IsReferenceBacked => _reference is not null;

    private long IntegerValue { get; }

    private decimal NumberValue { get; }

    private bool BooleanValue { get; }

    public static EventScriptFastValue Nothing { get; } = new(
        EventScriptValueKind.Nothing,
        0,
        0m,
        0m,
        0m,
        0m,
        false,
        null,
        null);

    public static EventScriptFastValue FromEventScriptValue(EventScriptValue value)
        => value switch
        {
            null => Nothing,
            _ when value.IsNothing() => Nothing,
            EventScriptBooleanValue boolean => FromBoolean(boolean.Value),
            EventScriptIntegerValue integer => FromInteger(integer.Value),
            EventScriptDecimalValue decimalValue when decimalValue.HasSemanticValue() => FromDecimal(decimalValue.Value, decimalValue.Unit),
            EventScriptPercentageValue percentage => FromPercentage(percentage.Ratio),
            EventScriptVector2Value vector2 => FromVector2(vector2.X, vector2.Y, vector2.Unit),
            EventScriptVector3Value vector3 => FromVector3(vector3.X, vector3.Y, vector3.Z, vector3.Unit),
            _ => new EventScriptFastValue(
                value.Kind,
                0,
                0m,
                0m,
                0m,
                0m,
                value.AsBoolean(),
                null,
                value)
        };

    public static EventScriptFastValue FromBoolean(bool value)
        => new(
            EventScriptValueKind.Boolean,
            value ? 1 : 0,
            value ? 1m : 0m,
            0m,
            0m,
            0m,
            value,
            null,
            null);

    public static EventScriptFastValue FromInteger(long value)
        => new(
            EventScriptValueKind.Integer,
            value,
            value,
            0m,
            0m,
            0m,
            value != 0,
            null,
            null);

    public static EventScriptFastValue FromDecimal(decimal value, EventScriptDecimalUnit? unit = null)
        => new(
            EventScriptValueKind.Decimal,
            EventScriptValueAlu.ToIntegerSaturated(value),
            value,
            0m,
            0m,
            0m,
            value != 0m,
            unit,
            null);

    public static EventScriptFastValue FromPercentage(decimal ratio)
        => new(
            EventScriptValueKind.Percentage,
            ToIntegerPercentage(ratio),
            ratio,
            0m,
            0m,
            0m,
            ratio != 0m,
            null,
            null);

    public static EventScriptFastValue FromVector2(decimal x, decimal y, EventScriptDecimalUnit? unit = null)
        => new(
            EventScriptValueKind.Vector2,
            0,
            0m,
            x,
            y,
            0m,
            x != 0m || y != 0m,
            unit,
            null);

    public static EventScriptFastValue FromVector3(decimal x, decimal y, decimal z, EventScriptDecimalUnit? unit = null)
        => new(
            EventScriptValueKind.Vector3,
            0,
            0m,
            x,
            y,
            z,
            x != 0m || y != 0m || z != 0m,
            unit,
            null);

    public static EventScriptFastValue FromText(string value) => FromEventScriptValue(EventScriptValueFactory.Text(value));

    public EventScriptValue ToEventScriptValue()
    {
        if (_reference is not null)
        {
            return _reference;
        }

        return Kind switch
        {
            EventScriptValueKind.Nothing => EventScriptValue.Nothing,
            EventScriptValueKind.Boolean => EventScriptValueFactory.Boolean(BooleanValue),
            EventScriptValueKind.Integer => EventScriptValueFactory.Integer(IntegerValue),
            EventScriptValueKind.Decimal => EventScriptValueFactory.Decimal(NumberValue, Unit),
            EventScriptValueKind.Percentage => EventScriptValueFactory.Percentage(NumberValue),
            EventScriptValueKind.Vector2 => EventScriptValueFactory.Vector2(X, Y, Unit),
            EventScriptValueKind.Vector3 => EventScriptValueFactory.Vector3(X, Y, Z, Unit),
            _ => EventScriptValue.Nothing
        };
    }

    private static long ToIntegerPercentage(decimal ratio)
    {
        var percent = decimal.Truncate(ratio * 100m);
        return EventScriptValueAlu.ToIntegerSaturated(percent);
    }
}

public interface IEventScriptExtensionFunction
{
    EventScriptFastValue Invoke(EventScriptExtensionContext context, ReadOnlySpan<EventScriptFastValue> arguments);
}

public interface IEventScriptExtensionRegistry
{
    bool TryResolve(EventScriptExtensionReference reference, out IEventScriptExtensionFunction function);
}

public sealed class EventScriptEmptyExtensionRegistry : IEventScriptExtensionRegistry
{
    public static readonly EventScriptEmptyExtensionRegistry Instance = new();

    private EventScriptEmptyExtensionRegistry()
    {
    }

    public bool TryResolve(EventScriptExtensionReference reference, out IEventScriptExtensionFunction function)
    {
        function = default!;
        return false;
    }
}
