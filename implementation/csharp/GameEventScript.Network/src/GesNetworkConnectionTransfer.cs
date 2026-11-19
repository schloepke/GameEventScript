// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Security.Cryptography;
using GameEventScript.Api;
using Wire = GameEventScript.Network.GesNetworkWire;

namespace GameEventScript.Network;

// Bulk transfers: a message on a bulk route carries a header with its compression, uncompressed size and SHA-256 hash.
// Either side may cancel a transfer with a control frame on channel 0 without closing the session.
public sealed partial class GesNetworkConnection
{
    private const int MaximumCancels = 16384;
    private const int MaximumWrittenTransfers = 8192;

    private GesNetworkDeflater? _deflater;
    private GesNetworkInflater? _inflater;
    private readonly byte[] _hash = new byte[Wire.HashBytes];
    private SHA256? _sha256;

    // Cancel frames to send: the route, the id, the code and whether it is sent and unconfirmed or confirmed.
    private readonly GesNetworkCancels _cancels = new();
    private int _cancelCount => _cancels.PendingCount;

    // The ids of local transfers the peer cancelled, so a repeated cancel is reported once.
    private readonly ulong[] _peerCancelled = new ulong[65536 / 64];

    // Stream sender progress: written bulk bytes, their count at each sent packet, and the written transfers not yet
    // confirmed completely.
    private long _bulkWritten;
    private readonly long[] _bulkMarks = new long[SendTimeWindow];
    private readonly GesNetworkWrittenTransfers _written = new();

    /// <summary>Gets the id of the transfer the latest successful <see cref="Send"/> on a bulk route queued; -1 before the first.</summary>
    public int LastTransferId { get; private set; } = -1;

    // The pending limit of bulk bytes: a transfer being received and a delivered one.
    private long MaxPendingBulk => 2 * (_maxTransfer + Wire.TransferHeaderBytes);

    // The largest data of a transfer on a bulk route: the route's root limit and the transfer limit.
    private long TransferLimit(int route) => Math.Min(_maxBytes[route], _maxTransfer);

    /// <summary>
    /// Cancels a transfer this side sends, as long as not all of its bytes were sent once. The peer is told with a
    /// cancel frame, also when the transfer was not started, and the connection reports
    /// <see cref="GesNetworkEventKind.TransferCancelled"/>; the session continues.
    /// </summary>
    /// <param name="route">The route index of a bulk <c>sends</c> route of this side.</param>
    /// <param name="transferId">The id <see cref="LastTransferId"/> gave the transfer.</param>
    /// <param name="code">The code the peer receives, 1 to 64 characters from <c>!</c> to <c>~</c>; <c>network.transferCancelled</c> when omitted.</param>
    /// <returns>True when the transfer was cancelled; false when the connection is not connected or the transfer is unknown or sent completely.</returns>
    /// <exception cref="ArgumentException">The route is no bulk route this side sends, or the code is invalid.</exception>
    public bool Cancel(int route, int transferId, string? code = null)
    {
        if ((uint)route >= (uint)_routeCount || !_localSends[route] || _classes[route] != GesNetworkClass.Bulk)
            throw new ArgumentException("The route is no bulk route this side sends.", nameof(route));
        code ??= GesNetworkCodes.TransferCancelled;
        if (!IsValidCode(code)) throw new ArgumentException("A code holds 1 to 64 characters from '!' to '~'.", nameof(code));
        if (State != GesNetworkState.Connected || (uint)transferId > ushort.MaxValue) return false;
        if (!_sendBulk.FindTransfer(route, (ushort)transferId, out var index) || (_datagram && index < _sendBulk.Cursor)) return false;
        // On a stream the started transfer is the head; its written bytes are no longer reported. The stream writes the
        // cancel frame when the placeholder reaches the head; a datagram transport sends it at once.
        if (!_datagram && index == 0 && _sendBulk.Started) _written.CancelNewest();
        CancelQueued(index, code);
        if (_datagram && !QueueCancel(route, (ushort)transferId, code)) return true;
        if (!AddEvent(GesNetworkEventKind.TransferCancelled, route, default, Counter.None, code, string.Empty, false, transferId)) Fail(GesNetworkCodes.QueueFull);
        return true;
    }

    // Turns a queued transfer into a cancelled placeholder; a datagram transport moves its cursor past it and removes it
    // once it reaches the head.
    private void CancelQueued(int index, string code)
    {
        _sendBulk.Cancel(index, code);
        if (!_datagram) return;
        if (index == _sendBulk.Cursor) _sendBulk.Advance();
        _sendBulk.PopDone();
    }

    private static bool IsValidCode(string code)
    {
        if (code.Length is < 1 or > Wire.MaximumCodeBytes) return false;
        foreach (var character in code)
        {
            if (character is < '!' or > '~') return false;
        }

        return true;
    }

    // Queues a transfer: the header and the data, compressed when that is shorter and both sides allow it.
    private GesNetworkSendStatus SendTransfer(int route, ReadOnlySpan<byte> data)
    {
        var limit = Math.Min(TransferLimit(route), _peerTransferLimit);
        if (data.Length > limit) return GesNetworkSendStatus.MessageTooLarge;
        if (_sendBulk.Count >= GesNetworkSendQueue.MaximumStarted || _sendBulk.Bytes + data.Length > Math.Min(_maxTransfer, _peerTransferLimit))
            return GesNetworkSendStatus.QueueFull;
        // On a datagram transport the receiver resolves ids in order; a cancel it has not confirmed holds its id open, so
        // the new id stays within the receive window of the oldest open one.
        if (_datagram && _cancels.Count > 0 && _cancels.OwnSpan(_nextBulkId, _localSends) >= GesNetworkSendQueue.MaximumStarted)
            return GesNetworkSendStatus.QueueFull;
        QueueTransfer(route, data);
        return GesNetworkSendStatus.Queued;
    }

    // Passes a transfer a client end kept before the handshake. The end bounded its kept transfers by this side's
    // limits, so the queue bounds do not apply; a transfer larger than the peer's transfer limit keeps its id and is
    // cancelled at once with network.transferTooLarge.
    internal void PassTransfer(int route, ReadOnlySpan<byte> data)
    {
        if (data.Length <= _peerTransferLimit)
        {
            QueueTransfer(route, data);
            return;
        }

        QueueTransfer(route, default);
        Cancel(route, LastTransferId, GesNetworkCodes.TransferTooLarge);
    }

    private void QueueTransfer(int route, ReadOnlySpan<byte> data)
    {
        var id = _nextBulkId++;
        var header = 1 + Wire.VarintSize64((ulong)data.Length) + Wire.HashBytes;
        var message = _sendBulk.Reserve(route, header + data.Length, -1, id);
        var length = -1;
        if (_deflate && _peerDeflate && data.Length > 0)
        {
            _deflater ??= new GesNetworkDeflater();
            length = _deflater.Compress(data, message.Slice(header, data.Length - 1));
        }

        if (length >= 0)
        {
            message[0] = Wire.CompressionDeflate;
            _sendBulk.Shrink(header + length);
        }
        else
        {
            message[0] = Wire.CompressionNone;
            data.CopyTo(message.Slice(header));
        }

        var position = Wire.WriteVarint64(message, 1, (ulong)data.Length);
        Hash(data, message.Slice(position, Wire.HashBytes));
        _peerCancelled[id >> 6] &= ~(1UL << (id & 63));
        LastTransferId = id;
    }

    // SHA-256 of the data; netstandard2.1 has no static one-shot hash.
    private void Hash(ReadOnlySpan<byte> data, Span<byte> hash)
    {
        _sha256 ??= SHA256.Create();
        _sha256.TryComputeHash(data, hash, out _);
    }

    private void DropTransfers()
    {
        _cancels.Clear();
        _written.Clear();
    }

    // Queues a cancel frame for the peer; false when it closed the connection because too many are pending.
    private bool QueueCancel(int route, ushort id, string code)
    {
        if (_cancels.Count >= MaximumCancels) return Fail(GesNetworkCodes.QueueFull);
        _cancels.Add(route, id, code);
        return true;
    }

    // The bytes of a cancel frame.
    private static int CancelFrameSize(int route, string code) =>
        Wire.VarintSize(0) + 1 + Wire.VarintSize((uint)route + 1) + 2 + Wire.VarintSize((uint)code.Length) + code.Length;

    private static int WriteCancelFrame(Span<byte> packet, int position, int route, ushort id, string code)
    {
        position = Wire.WriteVarint(packet, position, 0);
        packet[position++] = Wire.ControlCancel;
        position = Wire.WriteVarint(packet, position, (uint)route + 1);
        Wire.WriteUInt16(packet, position, id);
        position = Wire.WriteVarint(packet, position + 2, (uint)code.Length);
        foreach (var character in code) packet[position++] = (byte)character;
        return position;
    }

    // Writes the pending cancel frames of a stream or message transport; they need no confirmation.
    private int WriteCancels(Span<byte> packet, int position, int limit)
    {
        while (_cancels.PendingCount > 0)
        {
            var serial = _cancels.FirstPending;
            var route = _cancels.Route(serial);
            var code = _cancels.Code(serial);
            if (limit - position < CancelFrameSize(route, code)) break;
            position = WriteCancelFrame(packet, position, route, _cancels.Id(serial), code);
            _cancels.MarkSent(serial);
            _cancels.Confirm(serial);
        }

        return position;
    }

    // Writes the pending cancel frames of a datagram transport and records them to be resent until confirmed.
    private int WriteDatagramCancels(Span<byte> packet, int position, int limit)
    {
        while (_cancels.PendingCount > 0 && _fragmentRoom > 0)
        {
            var serial = _cancels.FirstPending;
            var route = _cancels.Route(serial);
            var code = _cancels.Code(serial);
            if (limit - position < CancelFrameSize(route, code)) break;
            position = WriteCancelFrame(packet, position, route, _cancels.Id(serial), code);
            _cancels.MarkSent(serial);
            RecordFragment(LaneCancel, serial, 0, 0);
        }

        return position;
    }

    // Reads a cancel frame after its kind byte; false when it closed the connection.
    private bool ReceiveCancel(ReadOnlySpan<byte> packet, ref int position)
    {
        if (Wire.ReadVarint(packet, ref position, out var channel) != 1 || channel == 0 || channel > _routeCount || packet.Length - position < 2)
            return Fail(GesNetworkCodes.Malformed);
        var route = (int)channel - 1;
        var id = Wire.ReadUInt16(packet, position);
        position += 2;
        if (Wire.ReadVarint(packet, ref position, out var codeLength) != 1 || codeLength is < 1 or > Wire.MaximumCodeBytes || packet.Length - position < codeLength)
            return Fail(GesNetworkCodes.Malformed);
        var codeBytes = packet.Slice(position, (int)codeLength);
        position += codeBytes.Length;
        Span<char> characters = stackalloc char[codeBytes.Length];
        for (var index = 0; index < codeBytes.Length; index++)
        {
            if (codeBytes[index] is < (byte)'!' or > (byte)'~') return Fail(GesNetworkCodes.Malformed);
            characters[index] = (char)codeBytes[index];
        }

        if (_classes[route] != GesNetworkClass.Bulk) return Fail(GesNetworkCodes.Malformed);
        if (_localSends[route])
        {
            // The receiver refused a transfer of ours: stop sending it and report it once.
            if (_sendBulk.FindTransfer(route, id, out var index)) CancelQueued(index, string.Empty);

            if (IsPeerCancelled(id)) return true;
            _peerCancelled[id >> 6] |= 1UL << (id & 63);
            return AddCancelled(route, id, characters);
        }

        if (_datagram)
        {
            var ordered = _orderedBulk!;
            if ((ushort)(id - ordered.NextId) >= GesNetworkOrderedReceive.Window)
                return (ushort)(ordered.NextId - id) <= GesNetworkOrderedReceive.Window || Fail(GesNetworkCodes.Malformed);
            var message = ordered.Message(id);
            if (message.Active && message.Id == id)
            {
                if (message.Cancelled) return true;
                _pendingBulk -= message.Total;
            }

            message.Cancel(channel, id);
            return AddCancelled(route, id, characters) && DeliverOrdered(ordered);
        }

        var assembly = _receiveBulk;
        if (assembly.Active && assembly.Id == id && assembly.Channel == channel)
        {
            assembly.Active = false;
            _pendingBulk -= assembly.Received;
        }
        else if (!assembly.Active && id == assembly.NextId)
        {
            assembly.NextId++;
        }
        else
        {
            return Fail(GesNetworkCodes.Malformed);
        }

        return AddCancelled(route, id, characters);
    }

    private bool IsPeerCancelled(ushort id) => (_peerCancelled[id >> 6] & (1UL << (id & 63))) != 0;

    private bool AddCancelled(int route, ushort id, ReadOnlySpan<char> code)
    {
        return AddEvent(GesNetworkEventKind.TransferCancelled, route, default, Counter.None, code.ToString(), string.Empty, true, id)
            || Fail(GesNetworkCodes.QueueFull);
    }

    // Checks a received transfer and reports its data as a message; a transfer that fails a check is cancelled. False
    // when it closed the connection.
    private bool DeliverTransfer(int route, ushort id, ReadOnlySpan<byte> message)
    {
        var position = 1;
        var code = GesNetworkCodes.TransferCorrupt;
        var compression = message.IsEmpty ? (byte)0xff : message[0];
        if (message.IsEmpty || Wire.ReadVarint64(message, ref position, out var size) != 1) return RefuseTransfer(route, id, code);
        if (size > (ulong)TransferLimit(route)) return RefuseTransfer(route, id, GesNetworkCodes.TransferTooLarge);
        if (compression > Wire.CompressionDeflate || (compression == Wire.CompressionDeflate && !_deflate)) return RefuseTransfer(route, id, code);
        if (message.Length - position < Wire.HashBytes) return RefuseTransfer(route, id, code);
        var hash = message.Slice(position, Wire.HashBytes);
        var data = message.Slice(position + Wire.HashBytes);
        if (compression == Wire.CompressionNone ? (ulong)data.Length != size : (ulong)data.Length >= size) return RefuseTransfer(route, id, code);

        if (!ReserveEvent(GesNetworkEventKind.Message, route, (int)size, Counter.Bulk, string.Empty, string.Empty, false, id, 0, 0, out var target))
            return Fail(GesNetworkCodes.QueueFull);
        if (compression == Wire.CompressionNone)
        {
            data.CopyTo(target);
        }
        else
        {
            _inflater ??= new GesNetworkInflater();
            if (!_inflater.Inflate(data, target)) return DropEvent((int)size) && RefuseTransfer(route, id, code);
        }

        Hash(target, _hash);
        return hash.SequenceEqual(_hash) || (DropEvent((int)size) && RefuseTransfer(route, id, code));
    }

    // Removes the newest event, a delivered transfer whose data failed a check.
    private bool DropEvent(int length)
    {
        _eventCount--;
        _arenaLength -= length;
        _pendingBulk -= length;
        return true;
    }

    // Cancels a received transfer that failed a check and tells the sender.
    private bool RefuseTransfer(int route, ushort id, string code)
    {
        if (!AddEvent(GesNetworkEventKind.TransferCancelled, route, default, Counter.None, code, string.Empty, false, id)) return Fail(GesNetworkCodes.QueueFull);
        return QueueCancel(route, id, code);
    }

    // ---- Stream sender progress ----

    // Notes the first written bytes of a transfer, which start at the written count before them.
    private void NoteTransferStarted(int route, ushort id, int length)
    {
        if (_written.Count == MaximumWrittenTransfers) _written.Pop();
        _written.Add(route, id, _bulkWritten, length);
    }

    // Reports the progress of the written transfers the peer confirmed up to the written count of the packet.
    private void ConfirmWritten(long index)
    {
        if (_sentCount - index > SendTimeWindow) return;
        var confirmed = _bulkMarks[index % SendTimeWindow];
        while (_written.Count > 0)
        {
            if (_written.Cancelled || IsPeerCancelled(_written.Id))
            {
                _written.Pop();
                continue;
            }

            var bytes = Math.Min(confirmed - _written.Start, _written.Total);
            if (bytes <= _written.Reported) return;
            if (!AddProgress(_written.Route, _written.Id, bytes, _written.Total)) return;
            _written.Reported = bytes;
            if (bytes < _written.Total) return;
            _written.Pop();
        }
    }
}

// The cancel frames a connection sends, in order. Each has a serial; a frame is pending, sent and unconfirmed, or
// confirmed. Confirmed frames leave from the head.
internal sealed class GesNetworkCancels
{
    private const byte Pending = 0;
    private const byte Sent = 1;
    private const byte Confirmed = 2;

    private int[] _routes = new int[8];
    private ushort[] _ids = new ushort[8];
    private string[] _codes = new string[8];
    private byte[] _states = new byte[8];
    private int _first;
    private long _firstSerial;
    private long _pendingSerial;

    internal int Count { get; private set; }

    internal int PendingCount { get; private set; }

    // The serial of the first pending frame; valid while one is pending.
    internal long FirstPending
    {
        get
        {
            while (_states[Slot(_pendingSerial)] != Pending) _pendingSerial++;
            return _pendingSerial;
        }
    }

    private int Slot(long serial) => (int)((_first + (serial - _firstSerial)) & (_routes.Length - 1));

    private bool Holds(long serial) => serial >= _firstSerial && serial < _firstSerial + Count;

    internal int Route(long serial) => _routes[Slot(serial)];

    internal ushort Id(long serial) => _ids[Slot(serial)];

    internal string Code(long serial) => _codes[Slot(serial)];

    internal void Add(int route, ushort id, string code)
    {
        if (Count == _routes.Length) Grow();
        var slot = (_first + Count) & (_routes.Length - 1);
        _routes[slot] = route;
        _ids[slot] = id;
        _codes[slot] = code;
        _states[slot] = Pending;
        if (PendingCount == 0) _pendingSerial = _firstSerial + Count;
        Count++;
        PendingCount++;
    }

    internal void MarkSent(long serial)
    {
        _states[Slot(serial)] = Sent;
        PendingCount--;
    }

    // Confirms a sent frame and removes the confirmed frames at the head.
    internal void Confirm(long serial)
    {
        if (!Holds(serial) || _states[Slot(serial)] != Sent) return;
        _states[Slot(serial)] = Confirmed;
        while (Count > 0 && _states[_first] == Confirmed)
        {
            _first = (_first + 1) & (_routes.Length - 1);
            _firstSerial++;
            Count--;
        }

        if (_pendingSerial < _firstSerial) _pendingSerial = _firstSerial;
    }

    // Makes a sent frame whose packet was lost pending again.
    internal void Lose(long serial)
    {
        if (!Holds(serial) || _states[Slot(serial)] != Sent) return;
        _states[Slot(serial)] = Pending;
        if (PendingCount == 0 || serial < _pendingSerial) _pendingSerial = serial;
        PendingCount++;
    }

    // The largest distance from the next transfer id back to an unconfirmed cancel of a transfer of this side; 0 when
    // there is none.
    internal int OwnSpan(ushort next, bool[] localSends)
    {
        var span = 0;
        for (var index = 0; index < Count; index++)
        {
            var slot = (_first + index) & (_routes.Length - 1);
            if (_states[slot] != Confirmed && localSends[_routes[slot]]) span = Math.Max(span, (ushort)(next - _ids[slot]));
        }

        return span;
    }

    private void Grow()
    {
        var size = _routes.Length * 2;
        var routes = new int[size];
        var ids = new ushort[size];
        var codes = new string[size];
        var states = new byte[size];
        for (var index = 0; index < Count; index++)
        {
            var slot = (_first + index) & (_routes.Length - 1);
            routes[index] = _routes[slot];
            ids[index] = _ids[slot];
            codes[index] = _codes[slot];
            states[index] = _states[slot];
        }

        _routes = routes;
        _ids = ids;
        _codes = codes;
        _states = states;
        _first = 0;
    }

    internal void Clear()
    {
        _firstSerial += Count;
        _pendingSerial = _firstSerial;
        _first = Count = PendingCount = 0;
    }
}

// The transfers a stream connection wrote, oldest first, with the written count before their first byte and the
// confirmed bytes already reported.
internal sealed class GesNetworkWrittenTransfers
{
    private int[] _routes = new int[8];
    private ushort[] _ids = new ushort[8];
    private long[] _starts = new long[8];
    private long[] _totals = new long[8];
    private long[] _reported = new long[8];
    private bool[] _cancelled = new bool[8];
    private int _first;

    internal int Count { get; private set; }

    internal int Route => _routes[_first];

    internal ushort Id => _ids[_first];

    internal long Start => _starts[_first];

    internal long Total => _totals[_first];

    internal bool Cancelled => _cancelled[_first];

    internal long Reported
    {
        get => _reported[_first];
        set => _reported[_first] = value;
    }

    internal void Add(int route, ushort id, long start, long total)
    {
        if (Count == _routes.Length) Grow();
        var slot = (_first + Count) & (_routes.Length - 1);
        _routes[slot] = route;
        _ids[slot] = id;
        _starts[slot] = start;
        _totals[slot] = total;
        _reported[slot] = 0;
        _cancelled[slot] = false;
        Count++;
    }

    // Marks the newest transfer cancelled: it is no longer reported.
    internal void CancelNewest() => _cancelled[(_first + Count - 1) & (_routes.Length - 1)] = true;

    internal void Pop()
    {
        _first = (_first + 1) & (_routes.Length - 1);
        Count--;
    }

    private void Grow()
    {
        var size = _routes.Length * 2;
        var routes = new int[size];
        var ids = new ushort[size];
        var starts = new long[size];
        var totals = new long[size];
        var reported = new long[size];
        var cancelled = new bool[size];
        for (var index = 0; index < Count; index++)
        {
            var slot = (_first + index) & (_routes.Length - 1);
            routes[index] = _routes[slot];
            ids[index] = _ids[slot];
            starts[index] = _starts[slot];
            totals[index] = _totals[slot];
            reported[index] = _reported[slot];
            cancelled[index] = _cancelled[slot];
        }

        _routes = routes;
        _ids = ids;
        _starts = starts;
        _totals = totals;
        _reported = reported;
        _cancelled = cancelled;
        _first = 0;
    }

    internal void Clear() => _first = Count = 0;
}
