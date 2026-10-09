// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using GameEventScript.Api;
using static GameEventScript.Api.GameEventScriptBytecodeTypeKind;

namespace GameEventScript.Runtime.Values;

internal interface IGesComponentIterator : IGesIterator
{
    bool MoveNext();
    GesValue Component(int index);
}

internal sealed class GesEntriesIterator(GesValueMap map, GesRuntimeBudget budget) : IGesComponentIterator, IDisposable
{
    private int _index = -1;
    private bool _closed;

    public bool MoveNext() => !_closed && ++_index < map.StorageLength;

    public GesValue Component(int index)
    {
        if (index == 1) return map.ValueAt(_index);
        var value = default(GesValue);
        if (index == 0) value.SetText(map.KeyAt(_index));
        return value;
    }

    public GesIteratorResult Next()
    {
        if (!MoveNext() || !budget.CheckGeneratedCollectionItemCountWithinLimit(2)) return default;
        var value = default(GesValue);
        value.SetMap(new GesValueMap(["key", "value"], [Component(0), Component(1)], 2));
        return new GesIteratorResult(value);
    }

    public void Dispose() => _closed = true;
}

internal sealed class GesProductIterator : IGesComponentIterator, IDisposable
{
    private readonly GesValue[] _sources;
    private readonly IGesIterator[] _iterators;
    private readonly GesValue[] _components;
    private readonly GesRuntimeBudget _budget;
    private readonly bool _cartesian;
    private bool _started;
    private bool _closed;

    internal GesProductIterator(GesValue[] sources, IGesIterator[] iterators, bool cartesian, GesRuntimeBudget budget)
    {
        _sources = sources;
        _iterators = iterators;
        _components = new GesValue[sources.Length];
        _cartesian = cartesian;
        _budget = budget;
    }

    public GesValue Component(int index) => index < _components.Length ? _components[index] : default;

    public bool MoveNext()
    {
        if (_closed) return false;
        if (!_started || !_cartesian)
        {
            _started = true;
            for (var index = 0; index < _iterators.Length; index++)
                if (!Advance(index)) { Dispose(); return false; }
            return true;
        }
        for (var index = _iterators.Length - 1; index >= 0; index--)
        {
            if (!Advance(index)) continue;
            for (var reset = index + 1; reset < _iterators.Length; reset++)
            {
                if (_iterators[reset] is IDisposable disposable) disposable.Dispose();
                _iterators[reset] = _sources[reset].CreateIterator()!;
                if (!Advance(reset)) { Dispose(); return false; }
            }
            return true;
        }
        Dispose();
        return false;
    }

    private bool Advance(int index)
    {
        var next = _iterators[index].Next();
        if (!next.HasValue) return false;
        _components[index] = next.Value;
        return true;
    }

    public GesIteratorResult Next()
    {
        if (!MoveNext() || !_budget.CheckGeneratedCollectionItemCountWithinLimit(_components.Length)) return default;
        var values = new GesValue[_components.Length];
        Array.Copy(_components, values, values.Length);
        var value = default(GesValue);
        value.SetList(values);
        return new GesIteratorResult(value);
    }

    public void Dispose()
    {
        if (_closed) return;
        _closed = true;
        foreach (var iterator in _iterators)
            if (iterator is IDisposable disposable) disposable.Dispose();
    }
}

internal sealed class GesUnionIterator(IGesIterator left, IGesIterator right) : IGesIterator, IDisposable
{
    private readonly List<IGesIterator> _sources = [left, right];
    private int _position;
    private bool _closed;

    internal void Append(IGesIterator source) => _sources.Add(source);

    public GesIteratorResult Next()
    {
        while (!_closed && _position < _sources.Count)
        {
            var next = _sources[_position].Next();
            if (next.HasValue) return next;
            _position++;
        }
        return default;
    }

    public void Dispose()
    {
        if (_closed) return;
        _closed = true;
        foreach (var source in _sources)
            if (source is IDisposable disposable) disposable.Dispose();
    }
}

internal sealed class GesMultisetIterator(IGesIterator left, GesValue[] right, bool intersect, GesRuntimeBudget budget) : IGesIterator, IDisposable
{
    private readonly List<(GesValue[] Values, bool[] Used)> _stages = [(right, new bool[right.Length])];
    private bool _closed;

    internal void Append(GesValue[] values) => _stages.Add((values, new bool[values.Length]));

    public GesIteratorResult Next()
    {
        while (!_closed && !budget.IsExhausted)
        {
            var candidate = left.Next();
            if (!candidate.HasValue) return default;
            var accepted = true;
            foreach (var stage in _stages)
            {
                if (!budget.ConsumeLoopIterationIfAvailable("Compound iterator candidate exceeds the configured limit.")) return default;
                var found = false;
                for (var index = 0; index < stage.Values.Length; index++)
                {
                    if (!budget.ConsumeLoopIterationIfAvailable("Compound iterator search exceeds the configured limit.")) return default;
                    if (stage.Used[index] || !candidate.Value.EqualsValue(stage.Values[index])) continue;
                    stage.Used[index] = true;
                    found = true;
                    break;
                }
                if (found == intersect) continue;
                accepted = false;
                break;
            }
            if (accepted) return candidate;
        }
        return default;
    }

    public void Dispose()
    {
        if (_closed) return;
        _closed = true;
        if (left is IDisposable disposable) disposable.Dispose();
    }
}
