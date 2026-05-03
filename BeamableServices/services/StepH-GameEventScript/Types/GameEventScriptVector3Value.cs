#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using static StepH.GameEventScript.Api.GameEventScriptValueFactory;

namespace StepH.GameEventScript.Types;

public sealed class GameEventScriptVector3Value : GameEventScriptValue
{
    public static readonly GameEventScriptVector3Value Zero = new(0m, 0m, 0m, null);

    public static GameEventScriptVector3Value Create(decimal x, decimal y, decimal z, GameEventScriptDecimalUnit? unit = null)
        => x == 0m && y == 0m && z == 0m && unit is null ? Zero : new GameEventScriptVector3Value(x, y, z, unit);

    private GameEventScriptVector3Value(decimal x, decimal y, decimal z, GameEventScriptDecimalUnit? unit)
    {
        X = x;
        Y = y;
        Z = z;
        Unit = unit;
        Components = CreateReadOnlyList(new[] { GesDecimal(x, unit), GesDecimal(y, unit), GesDecimal(z, unit) });
        Members = new ReadOnlyDictionary<string, GameEventScriptValue>(
            new Dictionary<string, GameEventScriptValue>(StringComparer.Ordinal)
            {
                ["x"] = GesDecimal(x, unit),
                ["y"] = GesDecimal(y, unit),
                ["z"] = GesDecimal(z, unit)
            });
    }

    public decimal X { get; }
    public decimal Y { get; }
    public decimal Z { get; }
    public GameEventScriptDecimalUnit? Unit { get; }
    public override GameEventScriptValueKind Kind => GameEventScriptValueKind.Vector3;

    private IReadOnlyList<GameEventScriptValue> Components { get; }
    private IReadOnlyDictionary<string, GameEventScriptValue> Members { get; }

    public override string AsText() => ToString();

    public override bool AsBoolean() => X != 0m || Y != 0m || Z != 0m;

    public override IReadOnlyList<GameEventScriptValue> AsList() => Components;

    public override IReadOnlyDictionary<string, GameEventScriptValue> AsDictionary() => Members;

    public override IEnumerable<GameEventScriptValue> AsEnumerable() => Components;

    public override bool Contains(GameEventScriptValue needle) => Components.Contains(needle);

    public override bool ContainsValue(GameEventScriptValue needle) => Contains(needle);

    public override bool TryGetDictionaryMember(string key, out GameEventScriptValue value)
    {
        if (Members.TryGetValue(key, out value))
        {
            return true;
        }

        value = GameEventScriptNothingValue.Instance;
        return false;
    }

    internal override bool TryConvertToBoolean(out GameEventScriptValue value)
    {
        value = GesBoolean(AsBoolean());
        return true;
    }

    internal override bool TryConvertToText(out GameEventScriptValue value)
    {
        value = GesText(ToString());
        return true;
    }

    internal override bool TryConvertToList(out GameEventScriptValue value)
    {
        value = GesList(Components);
        return true;
    }

    internal override bool TryConvertToDictionary(out GameEventScriptValue value)
    {
        value = GseDictionary(Members);
        return true;
    }
}
