#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections.Generic;
using System.Linq;
using static StepH.GameEventScript.Types.GseValueFactory;

namespace StepH.GameEventScript.Types;

public enum GseSequenceMode
{
    Values,
    Keys,
    Entries
}

public sealed class GseSequenceValue : GseValue
{
    public static readonly GseSequenceValue Empty = new(GseSequenceMode.Values, Nothing);

    public static GseSequenceValue GseSequence(GseSequenceMode mode, GseValue? source)
        => mode == GseSequenceMode.Values && (source == null || source.IsNothing()) ? Empty : new GseSequenceValue(mode, source ?? Nothing);

    private GseSequenceValue(GseSequenceMode mode, GseValue source)
    {
        Mode = mode;
        Source = source;
    }

    public GseSequenceMode Mode { get; }
    public GseValue Source { get; }
    public override GseValueKind Kind => GseValueKind.Sequence;

    public override string AsText() => ToString();

    public override long AsInteger() => AsEnumerable().LongCount();

    public override decimal AsNumber() => AsEnumerable().Count();

    public override IReadOnlyList<GseValue> AsList() => CreateReadOnlyList(AsEnumerable());

    public override ISet<GseValue> AsSet() => TryConvertToSet(out var value) ? value.AsSet() : new SortedSet<GseValue>(StableComparer);

    public override GseDiceValue AsDice() => TryConvertToDice(out var value) ? value.AsDice() : GseDiceValue.Empty;

    public override bool HasSemanticValue() => AsEnumerable().Any();

    public override bool IsSemanticallyEmpty() => !AsEnumerable().Any();

    public override IEnumerable<GseValue> AsEnumerable()
    {
        if (Source.Kind == GseValueKind.Optional)
        {
            var optional = Source.AsOptional();
            if (!optional.HasValue)
            {
                yield break;
            }

            foreach (var item in GseSequence(Mode, optional.Value).AsEnumerable())
            {
                yield return item;
            }

            yield break;
        }

        if (Mode == GseSequenceMode.Keys)
        {
            if (Source is not GseDictionaryValue dictionarySource)
            {
                yield break;
            }

            foreach (var key in dictionarySource.VisibleView.Keys.OrderBy(key => key, StringComparer.Ordinal))
            {
                yield return Tag(key);
            }

            yield break;
        }

        if (Mode == GseSequenceMode.Entries)
        {
            if (Source is not GseDictionaryValue dictionarySource)
            {
                yield break;
            }

            foreach (var pair in dictionarySource.VisibleView.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            {
                yield return Dictionary(new Dictionary<string, GseValue>(StringComparer.Ordinal)
                {
                    ["key"] = Tag(pair.Key),
                    ["value"] = pair.Value
                });
            }

            yield break;
        }

        switch (Source)
        {
            case GseNothingValue:
                yield break;
            case GseDictionaryValue dictionary:
                foreach (var value in dictionary.VisibleView.Values)
                {
                    yield return value;
                }

                yield break;
            case GseListValue:
            case GseSetValue:
            case GseDiceValue:
            case GseSequenceValue:
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

    internal override bool TryConvertToNumber(out GseValue value)
    {
        value = Decimal(AsEnumerable().Count());
        return true;
    }

    internal override bool TryConvertToInteger(out GseValue value)
    {
        value = Integer(AsEnumerable().LongCount());
        return true;
    }

    internal override bool TryConvertToText(out GseValue value)
    {
        value = Text(ToString());
        return true;
    }

    internal override bool TryConvertToList(out GseValue value)
    {
        value = List(AsEnumerable());
        return true;
    }

    internal override bool TryConvertToSet(out GseValue value)
    {
        value = Set(AsEnumerable());
        return true;
    }

    internal override bool TryConvertToDice(out GseValue value) => TryConvertSequenceToDice(AsEnumerable(), out value);
    
}
