#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections.Generic;
using System.Linq;
using StepH.Flow.EventScript.Types;

namespace StepH.Flow.EventScript.Runtime;

public sealed record EventScriptExtensionReference(
    string ExtensionName,
    string FunctionName,
    IReadOnlyList<string> ArgumentLabels)
{
    public string SignatureId { get; } =
        $"{ExtensionName}.{FunctionName}({string.Join(",", ArgumentLabels.Select(EventScriptMessageSignature.NormalizeParameterName))})";
}

public sealed class EventScriptExtensionContext(EventScriptContext runtimeContext)
{
    public EventScriptContext RuntimeContext { get; } = runtimeContext ?? throw new ArgumentNullException(nameof(runtimeContext));

    public EventScriptRandomGenerator Random => RuntimeContext.Random;

    public EventScriptRuntimeLimits RuntimeLimits => RuntimeContext.RuntimeLimits;
}

public readonly struct EventScriptFastValue
{
    private readonly EventScriptValue? _value;

    private EventScriptFastValue(EventScriptValue value)
    {
        _value = value ?? EventScriptValue.Nothing;
    }

    public EventScriptValueKind Kind => ToEventScriptValue().Kind;

    public long Integer => ToEventScriptValue().AsInteger();

    public decimal Number => ToEventScriptValue().AsNumber();

    public bool Boolean => ToEventScriptValue().AsBoolean();

    public string Text => ToEventScriptValue().AsText();

    public static EventScriptFastValue Nothing => FromEventScriptValue(EventScriptValue.Nothing);

    public static EventScriptFastValue FromEventScriptValue(EventScriptValue value) => new(value);

    public static EventScriptFastValue FromBoolean(bool value) => new(EventScriptValueFactory.Boolean(value));

    public static EventScriptFastValue FromInteger(long value) => new(EventScriptValueFactory.Integer(value));

    public static EventScriptFastValue FromDecimal(decimal value, EventScriptDecimalUnit? unit = null) => new(EventScriptValueFactory.Decimal(value, unit));

    public static EventScriptFastValue FromPercentage(decimal ratio) => new(EventScriptValueFactory.Percentage(ratio));

    public static EventScriptFastValue FromText(string value) => new(EventScriptValueFactory.Text(value));

    public EventScriptValue ToEventScriptValue() => _value ?? EventScriptValue.Nothing;
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
