#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using static StepH.GameEventScript.Api.GameEventScriptValueFactory;

namespace StepH.GameEventScript.Types;

public sealed class GameEventScriptPointValue : GameEventScriptValue
{
    public static readonly GameEventScriptPointValue Zero = new(0d, 0d, 0d, null);

    public static GameEventScriptPointValue Create(double x, double y, double z, GameEventScriptNumericUnit? unit = null)
        => x == 0d && y == 0d && z == 0d && unit is null ? Zero : new GameEventScriptPointValue(x, y, z, unit);

    private GameEventScriptPointValue(double x, double y, double z, GameEventScriptNumericUnit? unit)
    {
        X = x;
        Y = y;
        Z = z;
        Unit = unit;
        Components = CreateReadOnlyList(new[] { GesFloat(x, unit), GesFloat(y, unit), GesFloat(z, unit) });
        Members = new ReadOnlyDictionary<string, GameEventScriptValue>(
            new Dictionary<string, GameEventScriptValue>(System.StringComparer.Ordinal)
            {
                ["x"] = GesFloat(x, unit),
                ["y"] = GesFloat(y, unit),
                ["z"] = GesFloat(z, unit)
            });
    }

    public double X { get; }
    public double Y { get; }
    public double Z { get; }
    public GameEventScriptNumericUnit? Unit { get; }
    public override GameEventScriptValueKind Kind => GameEventScriptValueKind.Point;

    private IReadOnlyList<GameEventScriptValue> Components { get; }
    private IReadOnlyDictionary<string, GameEventScriptValue> Members { get; }

    public override string AsText() => ToString();

    public override bool AsBoolean() => X != 0d || Y != 0d || Z != 0d;

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
        value = GesDictionary(Members);
        return true;
    }
}
