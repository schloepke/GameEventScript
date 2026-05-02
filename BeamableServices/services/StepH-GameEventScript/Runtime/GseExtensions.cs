#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections.Generic;
using System.Linq;
using StepH.GameEventScript.Types;

namespace StepH.GameEventScript.Runtime;

public sealed class GseExtensionReference
{
    public GseExtensionReference(
        string? extensionName,
        string? functionName,
        IEnumerable<string?>? argumentLabels)
    {
        ExtensionName = NormalizeName(extensionName);
        FunctionName = NormalizeName(functionName);
        ArgumentLabels = (argumentLabels ?? Array.Empty<string?>())
            .Select(GseMessageSignature.NormalizeParameterName)
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

public sealed class GseExtensionContext(GseContext runtimeContext)
{
    public GseContext RuntimeContext { get; } = runtimeContext ?? throw new ArgumentNullException(nameof(runtimeContext));

    public GseRandomGenerator Random => RuntimeContext.Random;

    public GseRuntimeLimits RuntimeLimits => RuntimeContext.RuntimeLimits;
}

public readonly struct GseFastValue
{
    private readonly GseValue? _reference;

    private GseFastValue(
        GseValueKind kind,
        long integer,
        decimal number,
        decimal x,
        decimal y,
        decimal z,
        bool boolean,
        GseDecimalUnit? unit,
        GseValue? reference)
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

    public GseValueKind Kind { get; }

    public long Integer => Kind switch
    {
        GseValueKind.Integer => IntegerValue,
        GseValueKind.Decimal => IsReferenceBacked
            ? ToGseValue().AsInteger()
            : GseValueAlu.ToIntegerSaturated(NumberValue),
        GseValueKind.Percentage => ToIntegerPercentage(NumberValue),
        GseValueKind.Boolean => BooleanValue ? 1 : 0,
        _ => ToGseValue().AsInteger()
    };

    public decimal Number => Kind switch
    {
        GseValueKind.Integer => IntegerValue,
        GseValueKind.Decimal or GseValueKind.Percentage => IsReferenceBacked
            ? ToGseValue().AsNumber()
            : NumberValue,
        GseValueKind.Boolean => BooleanValue ? 1m : 0m,
        _ => ToGseValue().AsNumber()
    };

    public bool Boolean => Kind switch
    {
        GseValueKind.Boolean => BooleanValue,
        GseValueKind.Integer => IntegerValue != 0,
        GseValueKind.Decimal or GseValueKind.Percentage => IsReferenceBacked
            ? ToGseValue().AsBoolean()
            : NumberValue != 0m,
        GseValueKind.Vector2 => X != 0m || Y != 0m,
        GseValueKind.Vector3 => X != 0m || Y != 0m || Z != 0m,
        _ => ToGseValue().AsBoolean()
    };

    public string Text => ToGseValue().AsText();

    public GseDecimalUnit? Unit { get; }

    public decimal X { get; }

    public decimal Y { get; }

    public decimal Z { get; }

    public bool IsReferenceBacked => _reference is not null;

    private long IntegerValue { get; }

    private decimal NumberValue { get; }

    private bool BooleanValue { get; }

    public static GseFastValue Nothing { get; } = new(
        GseValueKind.Nothing,
        0,
        0m,
        0m,
        0m,
        0m,
        false,
        null,
        null);

    public static GseFastValue FromGseValue(GseValue value)
        => value switch
        {
            null => Nothing,
            _ when value.IsNothing() => Nothing,
            GseBooleanValue boolean => FromBoolean(boolean.Value),
            GseIntegerValue integer => FromInteger(integer.Value),
            GseDecimalValue decimalValue when decimalValue.HasSemanticValue() => FromDecimal(decimalValue.Value, decimalValue.Unit),
            GsePercentageValue percentage => FromPercentage(percentage.Ratio),
            GseVector2Value vector2 => FromVector2(vector2.X, vector2.Y, vector2.Unit),
            GseVector3Value vector3 => FromVector3(vector3.X, vector3.Y, vector3.Z, vector3.Unit),
            _ => new GseFastValue(
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

    public static GseFastValue FromBoolean(bool value)
        => new(
            GseValueKind.Boolean,
            value ? 1 : 0,
            value ? 1m : 0m,
            0m,
            0m,
            0m,
            value,
            null,
            null);

    public static GseFastValue FromInteger(long value)
        => new(
            GseValueKind.Integer,
            value,
            value,
            0m,
            0m,
            0m,
            value != 0,
            null,
            null);

    public static GseFastValue FromDecimal(decimal value, GseDecimalUnit? unit = null)
        => new(
            GseValueKind.Decimal,
            GseValueAlu.ToIntegerSaturated(value),
            value,
            0m,
            0m,
            0m,
            value != 0m,
            unit,
            null);

    public static GseFastValue FromPercentage(decimal ratio)
        => new(
            GseValueKind.Percentage,
            ToIntegerPercentage(ratio),
            ratio,
            0m,
            0m,
            0m,
            ratio != 0m,
            null,
            null);

    public static GseFastValue FromVector2(decimal x, decimal y, GseDecimalUnit? unit = null)
        => new(
            GseValueKind.Vector2,
            0,
            0m,
            x,
            y,
            0m,
            x != 0m || y != 0m,
            unit,
            null);

    public static GseFastValue FromVector3(decimal x, decimal y, decimal z, GseDecimalUnit? unit = null)
        => new(
            GseValueKind.Vector3,
            0,
            0m,
            x,
            y,
            z,
            x != 0m || y != 0m || z != 0m,
            unit,
            null);

    public static GseFastValue FromText(string value) => FromGseValue(GseValueFactory.Text(value));

    public GseValue ToGseValue()
    {
        if (_reference is not null)
        {
            return _reference;
        }

        return Kind switch
        {
            GseValueKind.Nothing => GseValue.Nothing,
            GseValueKind.Boolean => GseValueFactory.Boolean(BooleanValue),
            GseValueKind.Integer => GseValueFactory.Integer(IntegerValue),
            GseValueKind.Decimal => GseValueFactory.Decimal(NumberValue, Unit),
            GseValueKind.Percentage => GseValueFactory.Percentage(NumberValue),
            GseValueKind.Vector2 => GseValueFactory.Vector2(X, Y, Unit),
            GseValueKind.Vector3 => GseValueFactory.Vector3(X, Y, Z, Unit),
            _ => GseValue.Nothing
        };
    }

    private static long ToIntegerPercentage(decimal ratio)
    {
        var percent = decimal.Truncate(ratio * 100m);
        return GseValueAlu.ToIntegerSaturated(percent);
    }
}

public interface IGseExtensionFunction
{
    GseFastValue Invoke(GseExtensionContext context, ReadOnlySpan<GseFastValue> arguments);
}

public interface IGseExtensionRegistry
{
    bool TryResolve(GseExtensionReference reference, out IGseExtensionFunction function);
}

public sealed class GseEmptyExtensionRegistry : IGseExtensionRegistry
{
    public static readonly GseEmptyExtensionRegistry Instance = new();

    private GseEmptyExtensionRegistry()
    {
    }

    public bool TryResolve(GseExtensionReference reference, out IGseExtensionFunction function)
    {
        function = default!;
        return false;
    }
}
