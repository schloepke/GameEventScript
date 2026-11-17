// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;

namespace GameEventScript.Network;

// A FIFO of reliable or bulk messages to send. The bytes and entries are reused. A stream or message transport sends
// the head in fragments and removes it when sent; a datagram transport keeps each message until all its bytes are
// acknowledged, sends new fragments at a cursor and resends lost ranges first.
internal sealed class GesNetworkSendQueue
{
    // The most messages a datagram transport has started and still holds.
    internal const int MaximumStarted = 8192;

    private byte[] _bytes = [];
    private int _head;
    private int _tail;
    private int[] _routes = new int[8];
    private int[] _starts = new int[8];
    private int[] _lengths = new int[8];
    private int[] _numbers = new int[8];
    private ushort[] _ids = new ushort[8];
    private int[] _acknowledged = new int[8];
    private bool[] _done = new bool[8];
    private int _first;
    private int _count;
    private long _firstSerial;

    internal int Count => _count;

    // Bytes of messages not yet completely sent, or on a datagram transport not yet acknowledged.
    internal long Bytes => _tail - _head;

    // The send state of the head on a stream or message transport.
    internal bool Started;
    internal int Offset;
    internal ushort Id;

    internal int CurrentRoute => _routes[_first];

    internal ReadOnlySpan<byte> Current => Entry(0);

    // On a datagram transport: the message, counted from the head, whose new fragments are sent next, whether it is
    // started, its offset, and the ranges of lost fragments to resend first.
    internal int Cursor;
    internal bool CursorStarted;
    internal int CursorOffset;
    internal readonly GesNetworkRanges Lost = new();

    private int Slot(int index) => (_first + index) & (_routes.Length - 1);

    internal ReadOnlySpan<byte> Entry(int index)
    {
        var slot = Slot(index);
        return _bytes.AsSpan(_starts[slot], _lengths[slot]);
    }

    internal int Route(int index) => _routes[Slot(index)];

    internal ushort EntryId(int index) => _ids[Slot(index)];

    internal void SetEntryId(int index, ushort id) => _ids[Slot(index)] = id;

    internal long Serial(int index) => _firstSerial + index;

    // Whether a datagram transport has new fragments to send.
    internal bool HasNew => Cursor < _count && (CursorStarted || Cursor < MaximumStarted);

    // Adds a message; number is the topic state's number, or -1.
    internal void Enqueue(int route, ReadOnlySpan<byte> payload, int number = -1)
    {
        if (_count == _routes.Length) Grow();
        if (_bytes.Length - _tail < payload.Length)
        {
            var used = _tail - _head;
            var target = _bytes.Length - used >= payload.Length && _head > 0
                ? _bytes
                : new byte[Math.Max(Math.Max(_bytes.Length * 2, 256), used + payload.Length)];
            _bytes.AsSpan(_head, used).CopyTo(target);
            _bytes = target;
            for (var index = 0; index < _count; index++) _starts[Slot(index)] -= _head;
            _head = 0;
            _tail = used;
        }

        payload.CopyTo(_bytes.AsSpan(_tail));
        var slot = Slot(_count);
        _routes[slot] = route;
        _starts[slot] = _tail;
        _lengths[slot] = payload.Length;
        _numbers[slot] = number;
        _acknowledged[slot] = 0;
        _done[slot] = false;
        _tail += payload.Length;
        _count++;
    }

    private void Grow()
    {
        var size = _routes.Length * 2;
        var routes = new int[size];
        var starts = new int[size];
        var lengths = new int[size];
        var numbers = new int[size];
        var ids = new ushort[size];
        var acknowledged = new int[size];
        var done = new bool[size];
        for (var index = 0; index < _count; index++)
        {
            var slot = Slot(index);
            routes[index] = _routes[slot];
            starts[index] = _starts[slot];
            lengths[index] = _lengths[slot];
            numbers[index] = _numbers[slot];
            ids[index] = _ids[slot];
            acknowledged[index] = _acknowledged[slot];
            done[index] = _done[slot];
        }

        _routes = routes;
        _starts = starts;
        _lengths = lengths;
        _numbers = numbers;
        _ids = ids;
        _acknowledged = acknowledged;
        _done = done;
        _first = 0;
    }

    // Removes the head message.
    internal void Pop()
    {
        _head += _lengths[_first];
        _first = Slot(1);
        _count--;
        _firstSerial++;
        Started = false;
        Offset = 0;
        if (Cursor > 0) Cursor--;
        if (_count == 0) _head = _tail = 0;
    }

    // Moves the datagram cursor past a message whose last new fragment is sent.
    internal void Advance()
    {
        Cursor++;
        CursorStarted = false;
        CursorOffset = 0;
    }

    // Acknowledges a fragment of the message with the serial. Returns true when that completes the message, with its
    // route and topic state number.
    internal bool Acknowledge(long serial, int length, out int route, out int number)
    {
        route = number = -1;
        var index = serial - _firstSerial;
        if (index < 0 || index >= _count) return false;
        var slot = Slot((int)index);
        if (_done[slot]) return false;
        _acknowledged[slot] += length;
        if (_acknowledged[slot] != _lengths[slot]) return false;
        _done[slot] = true;
        route = _routes[slot];
        number = _numbers[slot];
        return true;
    }

    // Removes the acknowledged messages at the head.
    internal void PopDone()
    {
        while (_count > 0 && _done[_first]) Pop();
    }

    // Queues a lost fragment of a held message to be resent.
    internal void Lose(long serial, int offset, int length)
    {
        var index = serial - _firstSerial;
        if (index >= 0 && index < _count) Lost.Add(serial, offset, length);
    }

    // Finds the held message with the serial; false when it is no longer held.
    internal bool Find(long serial, out int index)
    {
        var relative = serial - _firstSerial;
        index = (int)relative;
        return relative >= 0 && relative < _count;
    }

    internal void Clear()
    {
        _head = _tail = _first = _count = 0;
        Started = false;
        Offset = 0;
        Cursor = 0;
        CursorStarted = false;
        CursorOffset = 0;
        Lost.Clear();
    }
}

// A FIFO of byte ranges of messages to resend: the message key, the offset and the length.
internal sealed class GesNetworkRanges
{
    private long[] _messages = new long[8];
    private int[] _offsets = new int[8];
    private int[] _lengths = new int[8];
    private int _first;
    private int _count;

    internal int Count => _count;

    internal long Message => _messages[_first];

    internal int Offset => _offsets[_first];

    internal int Length => _lengths[_first];

    internal void Add(long message, int offset, int length)
    {
        if (_count == _messages.Length)
        {
            var size = _messages.Length * 2;
            var messages = new long[size];
            var offsets = new int[size];
            var lengths = new int[size];
            for (var index = 0; index < _count; index++)
            {
                var slot = (_first + index) & (_messages.Length - 1);
                messages[index] = _messages[slot];
                offsets[index] = _offsets[slot];
                lengths[index] = _lengths[slot];
            }

            _messages = messages;
            _offsets = offsets;
            _lengths = lengths;
            _first = 0;
        }

        var target = (_first + _count) & (_messages.Length - 1);
        _messages[target] = message;
        _offsets[target] = offset;
        _lengths[target] = length;
        _count++;
    }

    // Removes the first bytes of the head range, and the range once none remain; a range of length 0 is removed.
    internal void Consume(int length)
    {
        _offsets[_first] += length;
        _lengths[_first] -= length;
        if (_lengths[_first] > 0) return;
        _first = (_first + 1) & (_messages.Length - 1);
        _count--;
    }

    internal void Clear() => _first = _count = 0;
}

// The message of a latest route being sent and the newer one waiting in its place. On a datagram transport the
// current message stays after it is sent once, so its lost ranges can be resent until a newer message replaces it.
internal sealed class GesNetworkLatestSlot(bool datagram)
{
    private byte[] _current = [];
    private byte[] _waiting = [];

    internal int CurrentLength;
    internal bool HasCurrent;
    internal bool Started;
    internal int Offset;
    internal ushort Id;
    internal int WaitingLength;
    internal bool HasWaiting;
    internal ushort NextId;

    // On a datagram transport: whether every fragment of the current message is sent once, and its lost ranges.
    internal bool Sent;
    internal readonly GesNetworkRanges Lost = new();

    internal ReadOnlySpan<byte> Current => _current.AsSpan(0, CurrentLength);

    // Whether the current message has fragments to send, new or lost.
    internal bool HasPending => (HasCurrent && !Sent) || Lost.Count > 0;

    internal void Set(ReadOnlySpan<byte> payload)
    {
        if (HasCurrent && Started && !Sent)
        {
            if (_waiting.Length < payload.Length) _waiting = new byte[Math.Max(payload.Length, _waiting.Length * 2)];
            payload.CopyTo(_waiting);
            WaitingLength = payload.Length;
            HasWaiting = true;
            Lost.Clear();
            return;
        }

        if (_current.Length < payload.Length) _current = new byte[Math.Max(payload.Length, _current.Length * 2)];
        payload.CopyTo(_current);
        CurrentLength = payload.Length;
        HasCurrent = true;
        Started = Sent = false;
        Offset = 0;
        Lost.Clear();
    }

    // Ends the current message once every fragment is sent and moves the waiting one in its place. On a datagram
    // transport a message without a waiting successor stays current for resends.
    internal void Complete()
    {
        Started = false;
        Offset = 0;
        if (HasWaiting)
        {
            (_current, _waiting) = (_waiting, _current);
            CurrentLength = WaitingLength;
            HasWaiting = false;
            Lost.Clear();
            return;
        }

        if (datagram)
        {
            Started = Sent = true;
            return;
        }

        HasCurrent = false;
    }

    // Queues a lost fragment for resending while its message is still current and no newer one waits.
    internal void Lose(ushort id, int offset, int length)
    {
        if (HasCurrent && Started && Id == id && !HasWaiting) Lost.Add(id, offset, length);
    }

    internal void Clear()
    {
        HasCurrent = HasWaiting = Started = Sent = false;
        Offset = 0;
        Lost.Clear();
    }

    // Drops the messages not yet started; a started one is completed.
    internal void DropUnstarted()
    {
        HasWaiting = false;
        if (!Started) HasCurrent = false;
    }
}

// A message being received in fragments.
internal sealed class GesNetworkReassembly
{
    private byte[] _buffer = [];

    internal bool Active;
    internal uint Channel;
    internal ushort Id;
    internal uint Total;
    internal int Received;
    internal ushort NextId;

    internal ReadOnlySpan<byte> Message => _buffer.AsSpan(0, Received);

    internal void Start(uint channel, ushort id, uint total)
    {
        Active = true;
        Channel = channel;
        Id = id;
        Total = total;
        Received = 0;
    }

    internal void Append(ReadOnlySpan<byte> chunk)
    {
        var needed = Received + chunk.Length;
        if (_buffer.Length < needed)
        {
            var grown = new byte[(int)Math.Min(Total, Math.Max(needed, (long)_buffer.Length * 2))];
            _buffer.AsSpan(0, Received).CopyTo(grown);
            _buffer = grown;
        }

        chunk.CopyTo(_buffer.AsSpan(Received));
        Received = needed;
    }
}

// A message received in fragments in any order on a datagram transport: its bytes and which of them arrived. A latest
// route also keeps the id of the message it reported last.
internal sealed class GesNetworkPartial
{
    private byte[] _buffer = [];
    private byte[] _arrived = [];

    internal bool Active;
    internal uint Channel;
    internal ushort Id;
    internal uint Total;
    internal long Received;
    internal ushort Reported;
    internal bool HasReported;

    internal ReadOnlySpan<byte> Message => _buffer.AsSpan(0, (int)Total);

    internal void Start(uint channel, ushort id, uint total)
    {
        if (_buffer.Length < total) _buffer = new byte[Math.Max(total, Math.Min((long)_buffer.Length * 2, int.MaxValue))];
        var bits = (int)((total + 7) / 8);
        if (_arrived.Length < bits) _arrived = new byte[Math.Max(bits, Math.Min(_arrived.Length * 2, int.MaxValue))];
        else Array.Clear(_arrived, 0, bits);
        Active = true;
        Channel = channel;
        Id = id;
        Total = total;
        Received = 0;
    }

    // Writes the bytes of a chunk that did not arrive before; bytes that did keep their first value.
    internal void Write(int offset, ReadOnlySpan<byte> chunk)
    {
        for (var index = 0; index < chunk.Length; index++)
        {
            var position = offset + index;
            var mask = (byte)(1 << (position & 7));
            ref var bits = ref _arrived[position >> 3];
            if ((bits & mask) != 0) continue;
            bits |= mask;
            _buffer[position] = chunk[index];
            Received++;
        }
    }
}

// The reliable or bulk messages being received on a datagram transport: the window of ids from the next to report.
internal sealed class GesNetworkOrderedReceive
{
    internal const int Window = 8192;

    private readonly GesNetworkPartial?[] _messages = new GesNetworkPartial?[Window];

    // Delivered messages wait here for reuse, so a warmed session receives without allocating.
    private readonly Stack<GesNetworkPartial> _free = new();

    internal ushort NextId;

    internal GesNetworkPartial Message(ushort id) => _messages[id % Window] ??= _free.Count > 0 ? _free.Pop() : new GesNetworkPartial();

    // Frees the slot of a delivered message and keeps the message for reuse.
    internal void Release(GesNetworkPartial message)
    {
        message.Active = false;
        _messages[message.Id % Window] = null;
        _free.Push(message);
    }

    // The next message to report when it is complete; null otherwise.
    internal GesNetworkPartial? Ready => _messages[NextId % Window] is { Active: true } message && message.Received == message.Total ? message : null;
}
