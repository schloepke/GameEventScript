// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using GameEventScript.Api;

namespace GameEventScript.Network;

// The states of one topic in one direction: their numbers and the ring of recent full records. Both sides apply the
// same ring rule, so the receiver's ring holds every state from a base on that the sender's ring holds.
internal sealed class GesNetworkTopic
{
    internal const int MaximumStates = 32;

    private readonly int _maxRecord;
    private readonly int[] _offsets = new int[MaximumStates];
    private readonly int[] _lengths = new int[MaximumStates];
    private readonly ushort[] _numbers = new ushort[MaximumStates];
    // The index of the packet that carried the state's last fragment; -1 while it is not completely sent. Stream and
    // message transports confirm a state through it.
    private readonly long[] _packets = new long[MaximumStates];
    // Whether the receiver confirmed the state; datagram transports confirm through it.
    private readonly bool[] _confirmed = new bool[MaximumStates];
    private byte[] _bytes = [];
    private int _end;
    private int _first;
    private int _count;
    private long _held;

    internal GesNetworkTopic(GesBoundSchema schema, bool subscribed)
    {
        Schema = schema;
        Subscribed = subscribed;
        _maxRecord = (int)schema.MaxByteCount;
        Scratch = new byte[_maxRecord + 1];
    }

    internal GesBoundSchema Schema { get; }

    // Whether the server's host subscribed the client to this topic; a client's own topics always are.
    internal bool Subscribed;

    // The number of the next reliable state.
    internal ushort Next;

    // The count of states added and of states whose last fragment is sent.
    private long _added;
    private long _completed;

    // The message being encoded or decoded: the mode byte and up to one record.
    internal byte[] Scratch { get; }

    // The message of the latest state being sent and its length.
    internal byte[]? Message;
    internal int MessageLength;

    // Adds a state with its number and full record; older states leave while the ring would exceed its limits.
    internal void Add(ReadOnlySpan<byte> record, long limit, ushort number)
    {
        while (_count > 0 && (_count + 1 > MaximumStates || _held + record.Length > limit))
        {
            _held -= _lengths[_first];
            _first = (_first + 1) % MaximumStates;
            _count--;
        }

        if (_count == 0) _end = 0;
        if (_bytes.Length - _end < record.Length) MakeRoom(record.Length, limit);
        var slot = (_first + _count) % MaximumStates;
        record.CopyTo(_bytes.AsSpan(_end));
        _offsets[slot] = _end;
        _lengths[slot] = record.Length;
        _numbers[slot] = number;
        _packets[slot] = -1;
        _confirmed[slot] = false;
        _end += record.Length;
        _held += record.Length;
        _count++;
        _added++;
    }

    // Moves the held records to the start and grows the storage up to the most the ring can hold.
    private void MakeRoom(int length, long limit)
    {
        var start = _count == 0 ? 0 : _offsets[_first];
        var used = _end - start;
        var target = _bytes;
        if (_bytes.Length - used < length)
        {
            var bound = Math.Max(Math.Min(limit, (long)MaximumStates * _maxRecord), _maxRecord);
            target = new byte[(int)Math.Min(bound, Math.Max((long)_bytes.Length * 2, used + length))];
        }

        _bytes.AsSpan(start, used).CopyTo(target);
        _bytes = target;
        for (var index = 0; index < _count; index++) _offsets[(_first + index) % MaximumStates] -= start;
        _end = used;
    }

    // Records the packet that carried the last fragment of the next state; a state no longer held is skipped.
    internal void MarkSent(long packet)
    {
        var index = _completed++;
        var oldest = _added - _count;
        if (index >= oldest && index < _added) _packets[(_first + (int)(index - oldest)) % MaximumStates] = packet;
    }

    // Confirms the held state with the number; a state no longer held is skipped.
    internal void Confirm(ushort number)
    {
        for (var index = _count - 1; index >= 0; index--)
        {
            var slot = (_first + index) % MaximumStates;
            if (_numbers[slot] != number) continue;
            _confirmed[slot] = true;
            return;
        }
    }

    // Finds the newest confirmed state: confirmed directly or carried completely by a packet up to the acknowledged one.
    // Returns the distance of its number to the given number of the new state, or 0 when none is confirmed.
    internal int Base(long acknowledged, ushort number, out ReadOnlySpan<byte> record)
    {
        for (var index = _count - 1; index >= 0; index--)
        {
            var slot = (_first + index) % MaximumStates;
            var packet = _packets[slot];
            if (!_confirmed[slot] && (packet < 0 || packet > acknowledged)) continue;
            record = _bytes.AsSpan(_offsets[slot], _lengths[slot]);
            return (ushort)(number - _numbers[slot]);
        }

        record = default;
        return 0;
    }

    // Finds the held state with the number; false when the ring does not hold it.
    internal bool Find(ushort number, out ReadOnlySpan<byte> record)
    {
        for (var index = _count - 1; index >= 0; index--)
        {
            var slot = (_first + index) % MaximumStates;
            if (_numbers[slot] != number) continue;
            record = _bytes.AsSpan(_offsets[slot], _lengths[slot]);
            return true;
        }

        record = default;
        return false;
    }

    // Empties the ring; the numbering continues.
    internal void Clear()
    {
        _first = _count = _end = 0;
        _held = 0;
    }
}
