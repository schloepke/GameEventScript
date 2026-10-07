// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
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
    private bool _right;
    private bool _closed;

    public GesIteratorResult Next()
    {
        if (_closed) return default;
        if (!_right)
        {
            var next = left.Next();
            if (next.HasValue) return next;
            _right = true;
        }
        return right.Next();
    }

    public void Dispose()
    {
        _closed = true;
        if (left is IDisposable l) l.Dispose();
        if (right is IDisposable r) r.Dispose();
    }
}

internal sealed class GesMultisetIterator(IGesIterator left, GesValue[] right, bool intersect, GesRuntimeBudget budget) : IGesIterator, IDisposable
{
    private readonly bool[] _used = new bool[right.Length];
    private bool _closed;

    public GesIteratorResult Next()
    {
        while (!_closed && !budget.IsExhausted)
        {
            var candidate = left.Next();
            if (!candidate.HasValue) return default;
            if (!budget.ConsumeLoopIterationIfAvailable("Compound iterator candidate exceeds the configured limit.")) return default;
            var found = false;
            for (var index = 0; index < right.Length; index++)
            {
                if (!budget.ConsumeLoopIterationIfAvailable("Compound iterator search exceeds the configured limit.")) return default;
                if (_used[index] || !candidate.Value.EqualsValue(right[index])) continue;
                _used[index] = true;
                found = true;
                break;
            }
            if (found == intersect) return candidate;
        }
        return default;
    }

    public void Dispose()
    {
        _closed = true;
        if (left is IDisposable disposable) disposable.Dispose();
    }
}
