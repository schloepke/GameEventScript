#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections.Generic;
using System.Linq;
using static StepH.GameEventScript.Types.GameEventScriptValueFactory;

namespace StepH.GameEventScript.Types;

public enum GameEventScriptSequenceMode
{
    Values,
    Keys,
    Entries
}

public sealed class GameEventScriptSequenceValue : GameEventScriptValue
{
    public static readonly GameEventScriptSequenceValue Empty = new(GameEventScriptSequenceMode.Values, Nothing);

    public static GameEventScriptSequenceValue GameEventScriptSequence(GameEventScriptSequenceMode mode, GameEventScriptValue? source)
        => mode == GameEventScriptSequenceMode.Values && (source == null || source.IsNothing()) ? Empty : new GameEventScriptSequenceValue(mode, source ?? Nothing);

    private GameEventScriptSequenceValue(GameEventScriptSequenceMode mode, GameEventScriptValue source)
    {
        Mode = mode;
        Source = source;
    }

    public GameEventScriptSequenceMode Mode { get; }
    public GameEventScriptValue Source { get; }
    public override GameEventScriptValueKind Kind => GameEventScriptValueKind.Sequence;

    public override string AsText() => ToString();

    public override long AsInteger() => AsEnumerable().LongCount();

    public override decimal AsNumber() => AsEnumerable().Count();

    public override IReadOnlyList<GameEventScriptValue> AsList() => CreateReadOnlyList(AsEnumerable());

    public override ISet<GameEventScriptValue> AsSet() => TryConvertToSet(out var value) ? value.AsSet() : new SortedSet<GameEventScriptValue>(StableComparer);

    public override GameEventScriptDiceValue AsDice() => TryConvertToDice(out var value) ? value.AsDice() : GameEventScriptDiceValue.Empty;

    public override bool HasSemanticValue() => AsEnumerable().Any();

    public override bool IsSemanticallyEmpty() => !AsEnumerable().Any();

    public override IEnumerable<GameEventScriptValue> AsEnumerable()
    {
        if (Source.Kind == GameEventScriptValueKind.Optional)
        {
            var optional = Source.AsOptional();
            if (!optional.HasValue)
            {
                yield break;
            }

            foreach (var item in GameEventScriptSequence(Mode, optional.Value).AsEnumerable())
            {
                yield return item;
            }

            yield break;
        }

        if (Mode == GameEventScriptSequenceMode.Keys)
        {
            if (Source is not GameEventScriptDictionaryValue dictionarySource)
            {
                yield break;
            }

            foreach (var key in dictionarySource.VisibleView.Keys.OrderBy(key => key, StringComparer.Ordinal))
            {
                yield return Tag(key);
            }

            yield break;
        }

        if (Mode == GameEventScriptSequenceMode.Entries)
        {
            if (Source is not GameEventScriptDictionaryValue dictionarySource)
            {
                yield break;
            }

            foreach (var pair in dictionarySource.VisibleView.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            {
                yield return Dictionary(new Dictionary<string, GameEventScriptValue>(StringComparer.Ordinal)
                {
                    ["key"] = Tag(pair.Key),
                    ["value"] = pair.Value
                });
            }

            yield break;
        }

        switch (Source)
        {
            case GameEventScriptNothingValue:
                yield break;
            case GameEventScriptDictionaryValue dictionary:
                foreach (var value in dictionary.VisibleView.Values)
                {
                    yield return value;
                }

                yield break;
            case GameEventScriptListValue:
            case GameEventScriptSetValue:
            case GameEventScriptDiceValue:
            case GameEventScriptSequenceValue:
                foreach (var item in Source.AsEnumerable())
                {
                    yield return item;
                }

                yield break;
            default:
                yield return Source;
                yield break;
        }
    }

    internal override bool TryConvertToNumber(out GameEventScriptValue value)
    {
        value = Decimal(AsEnumerable().Count());
        return true;
    }

    internal override bool TryConvertToInteger(out GameEventScriptValue value)
    {
        value = Integer(AsEnumerable().LongCount());
        return true;
    }

    internal override bool TryConvertToText(out GameEventScriptValue value)
    {
        value = Text(ToString());
        return true;
    }

    internal override bool TryConvertToList(out GameEventScriptValue value)
    {
        value = List(AsEnumerable());
        return true;
    }

    internal override bool TryConvertToSet(out GameEventScriptValue value)
    {
        value = Set(AsEnumerable());
        return true;
    }

    internal override bool TryConvertToDice(out GameEventScriptValue value) => TryConvertSequenceToDice(AsEnumerable(), out value);
    
}
