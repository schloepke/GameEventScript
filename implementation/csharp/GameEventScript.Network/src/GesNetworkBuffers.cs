// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;

namespace GameEventScript.Network;

// A FIFO of reliable or bulk messages to send. The bytes and entries are reused; the current message is
// the head and is sent in fragments.
internal sealed class GesNetworkSendQueue
{
    private byte[] _bytes = [];
    private int _head;
    private int _tail;
    private int[] _routes = new int[8];
    private int[] _lengths = new int[8];
    private int _first;
    private int _count;

    internal int Count => _count;

    // Bytes of messages not yet completely sent.
    internal long Bytes => _tail - _head;

    internal bool Started;
    internal int Offset;
    internal ushort Id;

    internal int CurrentRoute => _routes[_first];

    internal ReadOnlySpan<byte> Current => _bytes.AsSpan(_head, _lengths[_first]);

    internal void Enqueue(int route, ReadOnlySpan<byte> payload)
    {
        if (_count == _routes.Length)
        {
            var routes = new int[_routes.Length * 2];
            var lengths = new int[_routes.Length * 2];
            for (var index = 0; index < _count; index++)
            {
                routes[index] = _routes[(_first + index) % _routes.Length];
                lengths[index] = _lengths[(_first + index) % _routes.Length];
            }

            _routes = routes;
            _lengths = lengths;
            _first = 0;
        }

        if (_bytes.Length - _tail < payload.Length)
        {
            var used = _tail - _head;
            if (_bytes.Length - used >= payload.Length && _head > 0)
            {
                _bytes.AsSpan(_head, used).CopyTo(_bytes);
            }
            else
            {
                var grown = new byte[Math.Max(Math.Max(_bytes.Length * 2, 256), used + payload.Length)];
                _bytes.AsSpan(_head, used).CopyTo(grown);
                _bytes = grown;
            }

            _head = 0;
            _tail = used;
        }

        payload.CopyTo(_bytes.AsSpan(_tail));
        _tail += payload.Length;
        var slot = (_first + _count) % _routes.Length;
        _routes[slot] = route;
        _lengths[slot] = payload.Length;
        _count++;
    }

    // Removes the completely sent head message.
    internal void Pop()
    {
        _head += _lengths[_first];
        _first = (_first + 1) % _routes.Length;
        _count--;
        Started = false;
        Offset = 0;
        if (_count == 0) _head = _tail = 0;
    }

    internal void Clear()
    {
        _head = _tail = _first = _count = 0;
        Started = false;
        Offset = 0;
    }
}

// The message of a latest route being sent and the newer one waiting in its place.
internal sealed class GesNetworkLatestSlot
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

    internal ReadOnlySpan<byte> Current => _current.AsSpan(0, CurrentLength);

    internal void Set(ReadOnlySpan<byte> payload)
    {
        if (HasCurrent && Started)
        {
            if (_waiting.Length < payload.Length) _waiting = new byte[Math.Max(payload.Length, _waiting.Length * 2)];
            payload.CopyTo(_waiting);
            WaitingLength = payload.Length;
            HasWaiting = true;
            return;
        }

        if (_current.Length < payload.Length) _current = new byte[Math.Max(payload.Length, _current.Length * 2)];
        payload.CopyTo(_current);
        CurrentLength = payload.Length;
        HasCurrent = true;
    }

    // Ends the completed current message and moves the waiting one in its place.
    internal void Complete()
    {
        HasCurrent = false;
        Started = false;
        Offset = 0;
        if (!HasWaiting) return;
        (_current, _waiting) = (_waiting, _current);
        CurrentLength = WaitingLength;
        HasCurrent = true;
        HasWaiting = false;
    }

    internal void Clear()
    {
        HasCurrent = HasWaiting = Started = false;
        Offset = 0;
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
