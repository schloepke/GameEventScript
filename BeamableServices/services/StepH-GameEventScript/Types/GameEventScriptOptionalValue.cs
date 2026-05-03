using System.Collections.Generic;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace StepH.GameEventScript.Types;

public sealed class GameEventScriptOptionalValue : GameEventScriptValue
{
    public static readonly GameEventScriptOptionalValue None = new(false, Nothing);

    public static GameEventScriptOptionalValue GameEventScriptOptionalSome(GameEventScriptValue? value) => value == null ? None : new GameEventScriptOptionalValue(true, value);

    private GameEventScriptOptionalValue(bool hasValue, GameEventScriptValue value)
    {
        HasValue = hasValue;
        _value = value;
    }

    private readonly GameEventScriptValue _value;

    public bool HasValue { get; }
    public GameEventScriptValue Value => HasValue ? _value : Nothing;
    public override GameEventScriptValueKind Kind => GameEventScriptValueKind.Optional;

    public override GameEventScriptOptionalValue AsOptional() => this;

    public override string AsText() => TryConvertToText(out var value) ? value.AsText() : string.Empty;

    public override bool AsBoolean() => TryConvertToBoolean(out var value) && value.AsBoolean();

    public override long AsInteger() => TryConvertToInteger(out var value) ? value.AsInteger() : 0;

    public override decimal AsNumber() => TryConvertToNumber(out var value) ? value.AsNumber() : 0m;

    public override IReadOnlyList<GameEventScriptValue> AsList() => TryConvertToList(out var value) ? value.AsList() : System.Array.Empty<GameEventScriptValue>();

    public override IReadOnlyDictionary<string, GameEventScriptValue> AsDictionary() => TryConvertToDictionary(out var value) ? value.AsDictionary() : GameEventScriptDictionaryValue.EmptyView;

    public override ISet<GameEventScriptValue> AsSet() => TryConvertToSet(out var value) ? value.AsSet() : new SortedSet<GameEventScriptValue>(StableComparer);

    public override GameEventScriptDiceValue AsDice() => TryConvertToDice(out var value) ? value.AsDice() : GameEventScriptDiceValue.Empty;

    public override bool HasSemanticValue() => HasValue;

    public override bool IsSemanticallyEmpty() => !HasValue || Value.IsSemanticallyEmpty();

    public override bool TryUnwrapOptional(out GameEventScriptValue unwrapped)
    {
        if (!HasValue)
        {
            unwrapped = default!;
            return false;
        }

        unwrapped = Value;
        return true;
    }

    internal override bool TryConvertToNumber(out GameEventScriptValue value)
        => TryConvertValue(static source => source.TryConvertToNumber(out var converted) ? converted : null, out value);

    internal override bool TryConvertToInteger(out GameEventScriptValue value)
        => TryConvertValue(static source => source.TryConvertToInteger(out var converted) ? converted : null, out value);

    internal override bool TryConvertToBoolean(out GameEventScriptValue value)
        => TryConvertValue(static source => source.TryConvertToBoolean(out var converted) ? converted : null, out value);

    internal override bool TryConvertToText(out GameEventScriptValue value)
        => TryConvertValue(static source => source.TryConvertToText(out var converted) ? converted : null, out value);

    internal override bool TryConvertToList(out GameEventScriptValue value)
        => TryConvertValue(static source => source.TryConvertToList(out var converted) ? converted : null, out value);

    internal override bool TryConvertToDictionary(out GameEventScriptValue value)
        => TryConvertValue(static source => source.TryConvertToDictionary(out var converted) ? converted : null, out value);

    internal override bool TryConvertToSet(out GameEventScriptValue value)
        => TryConvertValue(static source => source.TryConvertToSet(out var converted) ? converted : null, out value);

    internal override bool TryConvertToDice(out GameEventScriptValue value)
        => TryConvertValue(static source => source.TryConvertToDice(out var converted) ? converted : null, out value);

    private bool TryConvertValue(System.Func<GameEventScriptValue, GameEventScriptValue?> converter, out GameEventScriptValue value)
    {
        if (!HasValue)
        {
            value = default!;
            return false;
        }

        var converted = converter(Value);
        if (converted == null)
        {
            value = default!;
            return false;
        }

        value = converted;
        return true;
    }

}
