// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using GameEventScript.Api;
using Wire = GameEventScript.Network.GesNetworkWire;

namespace GameEventScript.Network;

// Datagram delivery: packets may be lost, duplicated, delayed or reordered. The sender records its packets with the
// fragments they carried, detects losses as QUIC does and resends lost ranges; the receiver restores the order.
public sealed partial class GesNetworkConnection
{
    private const int RecordedPackets = 1024;
    private const int RecordedFragments = 16384;
    private const int ImmediateAckAsks = 16;
    private const long InitialRoundTrip = 333_000;
    private const long InitialVariation = 166_500;
    private const long MinimumDelay = 1000;
    private const long HelloInterval = MicrosecondsPerSecond;

    // Fragment lanes: cancel frames, reliable, bulk, then one per latest route.
    private const int LaneCancel = -1;
    private const int LaneReliable = 0;
    private const int LaneBulk = 1;
    private const int LaneLatest = 2;

    private readonly bool _datagram;

    // Receiving: the newest sequence, a bit for each of the 32 before it, and the asking packets since the last send.
    private bool _receivedAny;
    private ushort _newestSequence;
    private uint _sequenceBits;
    private int _asksSinceSend;
    private readonly GesNetworkOrderedReceive? _orderedReliable;
    private readonly GesNetworkOrderedReceive? _orderedBulk;
    private readonly GesNetworkPartial?[] _partialLatest;

    // Owed confirmation frames per route.
    private readonly bool[] _owed;
    private readonly ushort[] _owedNumbers;

    // Sent packets: time, whether it asked, whether it is outstanding, and its fragments in the fragment ring.
    private readonly long[] _recordTimes;
    private readonly bool[] _recordAsks;
    private readonly bool[] _recordOutstanding;
    private readonly long[] _recordFragments;
    private readonly int[] _recordFragmentCounts;
    private readonly int[] _fragmentLanes;
    private readonly long[] _fragmentKeys;
    private readonly int[] _fragmentOffsets;
    private readonly int[] _fragmentLengths;
    private long _fragmentTail;
    private int _fragmentRoom;
    private int _outstanding;
    private long _oldestOutstanding;

    private long _largestAcked = -1;
    private long _variation = InitialVariation;
    private long _latestSample = InitialRoundTrip;
    private long _lossTime = long.MaxValue;
    private int _probeCount;
    private bool _probeDue;
    private long _lastAskSend;

    // The client's hello repetition and the cookie of the latest retry.
    private bool _helloWritten;
    private long _helloNext;
    private long _helloInterval = HelloInterval;
    private byte[] _cookie = [];

    private long SmoothedRoundTrip => RoundTripMicroseconds < 0 ? InitialRoundTrip : RoundTripMicroseconds;

    private long ProbeDeadline
    {
        get
        {
            if (_outstanding == 0 || _lossTime != long.MaxValue) return long.MaxValue;
            var interval = SmoothedRoundTrip + Math.Max(4 * _variation, MinimumDelay) + _ackDelay;
            var shift = Math.Min(_probeCount, 10);
            return interval > (long.MaxValue - _lastAskSend) >> shift ? long.MaxValue : _lastAskSend + (interval << shift);
        }
    }

    // The deadlines only a datagram connection has.
    private long DatagramDeadline()
    {
        var deadline = long.MaxValue;
        if (_client && State == GesNetworkState.Hello && _helloSent) deadline = _helloNext;
        if (State != GesNetworkState.Connected) return deadline;
        deadline = Math.Min(deadline, _lossTime);
        if (_probeDue) return Math.Min(deadline, _now);
        return Math.Min(deadline, ProbeDeadline);
    }

    private void AdvanceDatagram()
    {
        if (_client && State == GesNetworkState.Hello && _helloSent && _now >= _helloNext) _helloSent = false;
        if (State != GesNetworkState.Connected) return;
        if (_now >= _lossTime) DetectLosses();
        if (!_probeDue && _now >= ProbeDeadline)
        {
            _probeDue = true;
            _probeCount++;
        }
    }

    private bool HasPendingDatagramFrames()
    {
        if (_cancels.PendingCount > 0 || _sendReliable.Lost.Count > 0 || _sendReliable.HasNew || _sendBulk.Lost.Count > 0 || _sendBulk.HasNew) return true;
        foreach (var slot in _sendLatest)
        {
            if (slot is { HasPending: true }) return true;
        }

        return false;
    }

    private void DropDatagramSending()
    {
        if (!_datagram) return;
        Array.Clear(_owed, 0, _owed.Length);
        _probeDue = false;
        _outstanding = 0;
        _lossTime = long.MaxValue;
    }

    // ---- Handshake ----

    private void NoteHelloSent()
    {
        _helloWritten = true;
        _helloNext = _helloInterval > long.MaxValue - _now ? long.MaxValue : _now + _helloInterval;
        _helloInterval = Math.Min(_helloInterval, long.MaxValue / 2) * 2;
    }

    // Writes the cookie and pads the datagram hello to 1200 bytes.
    private int WriteHelloCookie(Span<byte> packet, int position)
    {
        position = Wire.WriteVarint(packet, position, (uint)_cookie.Length);
        _cookie.CopyTo(packet.Slice(position));
        position += _cookie.Length;
        if (position >= Wire.MinimumPacketBytes) return position;
        packet.Slice(position, Wire.MinimumPacketBytes - position).Clear();
        return Wire.MinimumPacketBytes;
    }

    // Skips the cookie of a datagram hello and checks its padding; false when it closed the connection.
    private bool ReadHelloCookie(ReadOnlySpan<byte> packet, ref int position)
    {
        if (Wire.ReadVarint(packet, ref position, out var length) != 1 || length > Wire.MaximumCookieBytes || packet.Length - position < length ||
            packet.Length < Wire.MinimumPacketBytes)
            return Fail(GesNetworkCodes.Malformed);
        position += (int)length;
        foreach (var value in packet.Slice(position))
        {
            if (value != 0) return Fail(GesNetworkCodes.Malformed);
        }

        position = packet.Length;
        return true;
    }

    private void ReceiveRetry(ReadOnlySpan<byte> packet)
    {
        var position = 1;
        if (Wire.ReadVarint(packet, ref position, out var length) != 1 || length is < 1 or > Wire.MaximumCookieBytes || packet.Length - position != length)
        {
            Fail(GesNetworkCodes.Malformed);
            return;
        }

        _cookie = packet.Slice(position).ToArray();
        _helloSent = false;
        _helloInterval = HelloInterval;
    }

    // ---- Receiving ----

    // Handles a datagram by type and state: repetitions and late packets of the handshake are ignored.
    private void ReceiveDatagramPacket(ReadOnlySpan<byte> packet)
    {
        switch (packet[0])
        {
            case Wire.Close:
                ReceiveClose(packet);
                break;
            case Wire.Hello when !_client && State == GesNetworkState.AwaitHello:
                ReceiveHello(packet);
                break;
            case Wire.Hello when !_client && State == GesNetworkState.AwaitAccept:
                break;
            case Wire.Hello when !_client && State == GesNetworkState.Connected:
                _welcomePending = true;
                break;
            case Wire.Retry when _client && State == GesNetworkState.Hello:
                ReceiveRetry(packet);
                break;
            case Wire.Retry when _client:
                break;
            case Wire.Welcome when _client && State == GesNetworkState.Hello && _helloWritten:
                ReceiveWelcome(packet);
                break;
            case Wire.Welcome when _client && State == GesNetworkState.Connected:
                break;
            case Wire.Data when State == GesNetworkState.Connected:
                ReceiveDatagramData(packet);
                break;
            case Wire.Data when _client && State == GesNetworkState.Hello:
                break;
            default:
                Fail(GesNetworkCodes.Malformed);
                break;
        }
    }


    private void ReceiveDatagramData(ReadOnlySpan<byte> packet)
    {
        if (packet.Length < 4 || (packet[1] & ~3) != 0)
        {
            Fail(GesNetworkCodes.Malformed);
            return;
        }

        if (!PlaceSequence(Wire.ReadUInt16(packet, 2))) return;
        var position = 4;
        if ((packet[1] & 1) != 0)
        {
            if (packet.Length - position < 6 || !AcknowledgeDatagram(Wire.ReadUInt16(packet, position), Wire.ReadUInt32(packet, position + 2)))
            {
                Fail(GesNetworkCodes.Malformed);
                return;
            }

            position += 6;
        }

        var asks = (packet[1] & 2) != 0;
        while (position < packet.Length)
        {
            if (!ReceiveDatagramFrame(packet, ref position, asks)) return;
        }

        if (!asks) return;
        _asksSinceSend++;
        DueAck();
        if (_asksSinceSend >= ImmediateAckAsks) _ackDeadline = Math.Min(_ackDeadline, _now);
    }

    private void DueAck()
    {
        if (_ackDue) return;
        _ackDue = true;
        _ackDeadline = _now + _ackDelay;
    }

    // Places a received sequence; false for a duplicate or a sequence too old.
    private bool PlaceSequence(ushort sequence)
    {
        if (!_receivedAny)
        {
            _receivedAny = true;
            _newestSequence = sequence;
            _sequenceBits = 0;
            return true;
        }

        var ahead = (ushort)(sequence - _newestSequence);
        if (ahead is > 0 and < 32768)
        {
            _sequenceBits = ahead > 32 ? 0 : (uint)(((ulong)_sequenceBits << ahead) | (1UL << (ahead - 1)));
            _newestSequence = sequence;
            return true;
        }

        var behind = (ushort)(_newestSequence - sequence);
        if (behind == 0 || behind > 32) return false;
        var bit = 1u << (behind - 1);
        if ((_sequenceBits & bit) != 0) return false;
        _sequenceBits |= bit;
        return true;
    }

    // Applies an acknowledgement; false when it names a packet before the first or none was sent.
    private bool AcknowledgeDatagram(ushort sequence, uint bits)
    {
        var sent = _sentCount;
        if (sent == 0) return false;
        var named = sent - 1 - (ushort)((ushort)(sent - 1) - sequence);
        if (named < 0) return false;
        var acknowledged = false;
        if (named > _largestAcked)
        {
            if (IsRecorded(named) && _recordAsks[named % RecordedPackets]) Sample(_now - _recordTimes[named % RecordedPackets]);
            _largestAcked = named;
        }

        acknowledged |= AcknowledgePacket(named);
        for (var bit = 0; bit < 32; bit++)
        {
            if ((bits & (1u << bit)) != 0) acknowledged |= AcknowledgePacket(named - 1 - bit);
        }

        _sendReliable.PopDone();
        _sendBulk.PopDone();
        if (acknowledged) _probeCount = 0;
        DetectLosses();
        return true;
    }

    private bool IsRecorded(long packet) => packet >= 0 && packet < _sentCount && _sentCount - packet <= RecordedPackets;

    private void Sample(long sample)
    {
        if (RoundTripMicroseconds < 0)
        {
            RoundTripMicroseconds = sample;
            _variation = sample / 2;
        }
        else
        {
            _variation += (Math.Abs(RoundTripMicroseconds - sample) - _variation) / 4;
            RoundTripMicroseconds += (sample - RoundTripMicroseconds) / 8;
        }

        _latestSample = sample;
    }

    // Acknowledges a recorded outstanding packet and its fragments; false when it was not outstanding.
    private bool AcknowledgePacket(long packet)
    {
        if (!IsRecorded(packet) || !_recordOutstanding[packet % RecordedPackets]) return false;
        _recordOutstanding[packet % RecordedPackets] = false;
        _outstanding--;
        var slot = packet % RecordedPackets;
        for (var index = 0; index < _recordFragmentCounts[slot]; index++)
        {
            var fragment = (_recordFragments[slot] + index) % RecordedFragments;
            var lane = _fragmentLanes[fragment];
            var key = _fragmentKeys[fragment];
            if (lane == LaneCancel)
            {
                _cancels.Confirm(key);
                continue;
            }

            if (lane >= LaneLatest) continue;
            if (lane == LaneBulk)
            {
                AcknowledgeBulk(key, _fragmentLengths[fragment]);
                continue;
            }

            if (_sendReliable.Acknowledge(key, _fragmentLengths[fragment], out var route, out var number) && number >= 0)
                _topics[route]?.Confirm((ushort)number);
        }

        return true;
    }

    // Acknowledges a fragment of a transfer and reports the transfer's acknowledged bytes.
    private void AcknowledgeBulk(long key, int length)
    {
        if (!_sendBulk.Find(key, out var index) || _sendBulk.Cancelled(index)) return;
        var before = _sendBulk.AcknowledgedBytes(index);
        _sendBulk.Acknowledge(key, length, out _, out _);
        var after = _sendBulk.AcknowledgedBytes(index);
        if (after != before) AddProgress(_sendBulk.Route(index), _sendBulk.EntryId(index), after, _sendBulk.Entry(index).Length);
    }

    // Declares lost the outstanding packets older than the largest acknowledged one whose order or delay ran out.
    private void DetectLosses()
    {
        _lossTime = long.MaxValue;
        if (_largestAcked < 0) return;
        var delay = Math.Max(9 * Math.Max(SmoothedRoundTrip, _latestSample) / 8, MinimumDelay);
        var first = Math.Max(_oldestOutstanding, _sentCount - RecordedPackets);
        for (var packet = Math.Max(first, 0); packet < _largestAcked; packet++)
        {
            if (!_recordOutstanding[packet % RecordedPackets]) continue;
            var expiry = _recordTimes[packet % RecordedPackets] + delay;
            if (_largestAcked - packet >= 3 || _now >= expiry) LosePacket(packet);
            else _lossTime = Math.Min(_lossTime, expiry);
        }

        while (_oldestOutstanding < _sentCount && !(IsRecorded(_oldestOutstanding) && _recordOutstanding[_oldestOutstanding % RecordedPackets]))
            _oldestOutstanding++;
    }

    // Declares an outstanding packet lost and queues its fragments to be resent.
    private void LosePacket(long packet)
    {
        var slot = packet % RecordedPackets;
        _recordOutstanding[slot] = false;
        _outstanding--;
        for (var index = 0; index < _recordFragmentCounts[slot]; index++)
        {
            var fragment = (_recordFragments[slot] + index) % RecordedFragments;
            var lane = _fragmentLanes[fragment];
            var key = _fragmentKeys[fragment];
            var offset = _fragmentOffsets[fragment];
            var length = _fragmentLengths[fragment];
            if (lane == LaneCancel) _cancels.Lose(key);
            else if (lane == LaneReliable) _sendReliable.Lose(key, offset, length);
            else if (lane == LaneBulk) _sendBulk.Lose(key, offset, length);
            else _sendLatest[lane - LaneLatest]?.Lose((ushort)key, offset, length);
        }
    }

    // Reads one confirmation or message frame; false when it closed the connection.
    private bool ReceiveDatagramFrame(ReadOnlySpan<byte> packet, ref int position, bool asks)
    {
        if (_frameUnits < MicrosecondsPerSecond) return Fail(GesNetworkCodes.RateExceeded);
        _frameUnits -= MicrosecondsPerSecond;
        if (Wire.ReadVarint(packet, ref position, out var channel) != 1 || channel > _routeCount) return Fail(GesNetworkCodes.Malformed);
        if (channel == 0)
        {
            if (position == packet.Length) return Fail(GesNetworkCodes.Malformed);
            var kind = packet[position++];
            if (kind == Wire.ControlConfirmation) return ReceiveConfirmation(packet, ref position);
            // A cancel frame is resent until acknowledged, so its packet asks.
            if (kind != Wire.ControlCancel || !asks) return Fail(GesNetworkCodes.Malformed);
            return ReceiveCancel(packet, ref position);
        }

        if (!asks) return Fail(GesNetworkCodes.Malformed);
        var route = (int)channel - 1;
        if (_localSends[route]) return Fail(GesNetworkCodes.UnexpectedRoute);
        if (packet.Length - position < 2) return Fail(GesNetworkCodes.Malformed);
        var id = Wire.ReadUInt16(packet, position);
        position += 2;
        if (Wire.ReadVarint(packet, ref position, out var offset) != 1 || Wire.ReadVarint(packet, ref position, out var total) != 1)
            return Fail(GesNetworkCodes.Malformed);
        if (total > MessageLimit(route)) return Fail(GesNetworkCodes.MessageTooLarge);
        if (Wire.ReadVarint(packet, ref position, out var length) != 1 || packet.Length - position < length) return Fail(GesNetworkCodes.Malformed);
        var chunk = packet.Slice(position, (int)length);
        position += chunk.Length;
        if ((length == 0 && total != 0) || (ulong)offset + length > total) return Fail(GesNetworkCodes.Malformed);

        if (_classes[route] == GesNetworkClass.Latest) return ReceiveLatestFragment(route, channel, id, (int)offset, total, chunk);
        var bulk = _classes[route] == GesNetworkClass.Bulk;
        var ordered = bulk ? _orderedBulk! : _orderedReliable!;
        if ((ushort)(id - ordered.NextId) >= GesNetworkOrderedReceive.Window)
            return (ushort)(ordered.NextId - id) <= GesNetworkOrderedReceive.Window || Fail(GesNetworkCodes.Malformed);
        var message = ordered.Message(id);
        if (!message.Active || message.Id != id)
        {
            message.Start(channel, id, total);
            if (bulk)
            {
                _pendingBulk += total;
                if (_pendingBulk > MaxPendingBulk) return Fail(GesNetworkCodes.QueueFull);
            }
            else
            {
                _pending += total;
                if (_pending > _maxPending) return Fail(GesNetworkCodes.QueueFull);
            }
        }
        else if (message.Cancelled)
        {
            // Fragments of a transfer the sender cancelled are still in flight.
            return true;
        }
        else if (message.Channel != channel || message.Total != total)
        {
            return Fail(GesNetworkCodes.Malformed);
        }

        var received = message.Received;
        message.Write((int)offset, chunk);
        if (bulk && message.Received != received) AddProgress(route, id, message.Received, total);
        return DeliverOrdered(ordered);
    }

    // Reports the messages that are complete in order and skips cancelled ones; false when it closed the connection.
    private bool DeliverOrdered(GesNetworkOrderedReceive ordered)
    {
        while (ordered.Ready is { } ready)
        {
            ordered.NextId++;
            var delivered = true;
            if (!ready.Cancelled)
            {
                var route = (int)ready.Channel - 1;
                // The message's bytes are pending already; the event counts them again.
                if (_classes[route] == GesNetworkClass.Bulk) _pendingBulk -= ready.Total;
                else _pending -= ready.Total;
                delivered = Deliver(route, ready.Id, ready.Message);
            }

            ordered.Release(ready);
            if (!delivered) return false;
        }

        return true;
    }

    // Continues, starts or ignores the message of a latest route; false when it closed the connection.
    private bool ReceiveLatestFragment(int route, uint channel, ushort id, int offset, uint total, ReadOnlySpan<byte> chunk)
    {
        var message = _partialLatest[route]!;
        if (message.Active && message.Id == id)
        {
            if (message.Total != total) return Fail(GesNetworkCodes.Malformed);
        }
        else
        {
            var reference = message.Active ? message.Id : message.Reported;
            var ahead = (ushort)(id - reference);
            if ((message.Active || message.HasReported) && ahead is 0 or >= 32768) return true;
            if (message.Active) _pending -= message.Total;
            message.Start(channel, id, total);
            _pending += total;
            if (_pending > _maxPending) return Fail(GesNetworkCodes.QueueFull);
        }

        message.Write(offset, chunk);
        if (message.Received != message.Total) return true;
        message.Active = false;
        message.Reported = id;
        message.HasReported = true;
        _pending -= total;
        return Deliver(route, id, message.Message);
    }

    private bool ReceiveConfirmation(ReadOnlySpan<byte> packet, ref int position)
    {
        if (Wire.ReadVarint(packet, ref position, out var channel) != 1 || channel == 0 || channel > _routeCount || packet.Length - position < 2)
            return Fail(GesNetworkCodes.Malformed);
        var route = (int)channel - 1;
        if (!_localSends[route] || _classes[route] != GesNetworkClass.Latest || _topics[route] is not { } topic) return Fail(GesNetworkCodes.Malformed);
        topic.Confirm(Wire.ReadUInt16(packet, position));
        position += 2;
        return true;
    }

    // Owes the sender a confirmation of a latest state; it replaces one not yet sent.
    private void OweConfirmation(int route, ushort number)
    {
        _owed[route] = true;
        _owedNumbers[route] = number;
        DueAck();
    }

    // ---- Sending ----

    // Writes a data packet when frames, an acknowledgement, the keepalive or a probe are due; 0 otherwise.
    private int WriteDatagramData(Span<byte> packet, int limit)
    {
        var sent = _sentCount;
        var slot = sent % RecordedPackets;
        // The record of the packet 1024 before leaves; it is lost when still outstanding.
        var evicted = sent >= RecordedPackets && _recordOutstanding[slot];
        var oldest = sent - RecordedPackets + 1;
        var used = oldest >= 0 && oldest < sent ? _fragmentTail - _recordFragments[oldest % RecordedPackets] : sent > 0 && oldest < 0 ? _fragmentTail - _recordFragments[0] : 0;
        var room = (int)(RecordedFragments - used);
        var frames = (evicted || HasPendingDatagramFrames()) && (_budget == 0 || _budgetUnits >= 0) && room > 0;
        var keepalive = _now - _lastSend >= _keepalive;
        if (!frames && !(_ackDue && _now >= _ackDeadline) && !keepalive && !_probeDue) return 0;
        if (evicted) LosePacket(sent - RecordedPackets);

        packet[0] = Wire.Data;
        packet[1] = 0;
        Wire.WriteUInt16(packet, 2, (ushort)sent);
        var position = 4;
        if (_receivedAny)
        {
            packet[1] = 1;
            Wire.WriteUInt16(packet, position, _newestSequence);
            Wire.WriteUInt32(packet, position + 2, _sequenceBits);
            position += 6;
        }

        for (var route = 0; route < _routeCount; route++)
        {
            if (!_owed[route]) continue;
            _owed[route] = false;
            position = Wire.WriteVarint(packet, position, 0);
            packet[position++] = Wire.ControlConfirmation;
            position = Wire.WriteVarint(packet, position, (uint)route + 1);
            Wire.WriteUInt16(packet, position, _owedNumbers[route]);
            position += 2;
        }

        _recordFragments[slot] = _fragmentTail;
        _fragmentRoom = room;
        var start = position;
        if (frames)
        {
            position = WriteDatagramCancels(packet, position, limit);
            position = WriteDatagramQueue(_sendReliable, ref _nextReliableId, LaneReliable, packet, position, limit);
            for (var route = 0; route < _routeCount; route++)
            {
                if (_sendLatest[route] is { } latest) position = WriteDatagramLatest(route, latest, packet, position, limit);
            }

            position = WriteDatagramQueue(_sendBulk, ref _nextBulkId, LaneBulk, packet, position, limit);
        }

        var asks = position > start || keepalive || _probeDue;
        if (asks) packet[1] |= 2;
        _recordTimes[slot] = _now;
        _recordAsks[slot] = asks;
        _recordOutstanding[slot] = asks;
        _recordFragmentCounts[slot] = (int)(_fragmentTail - _recordFragments[slot]);
        if (asks)
        {
            if (_outstanding == 0) _oldestOutstanding = sent;
            _outstanding++;
            _lastAskSend = _now;
        }

        _sentCount++;
        _ackDue = false;
        _probeDue = false;
        _asksSinceSend = 0;
        if (position > start && _budget > 0) _budgetUnits -= position * MicrosecondsPerSecond;
        return position;
    }

    private void RecordFragment(int lane, long key, int offset, int length)
    {
        var fragment = _fragmentTail % RecordedFragments;
        _fragmentLanes[fragment] = lane;
        _fragmentKeys[fragment] = key;
        _fragmentOffsets[fragment] = offset;
        _fragmentLengths[fragment] = length;
        _fragmentTail++;
        _fragmentRoom--;
    }

    // Writes the lost ranges, then new fragments of reliable or bulk messages until the packet is full.
    private int WriteDatagramQueue(GesNetworkSendQueue queue, ref ushort nextId, int lane, Span<byte> packet, int position, int limit)
    {
        var lost = queue.Lost;
        while (lost.Count > 0 && _fragmentRoom > 0)
        {
            if (!queue.Find(lost.Message, out var index) || queue.Cancelled(index))
            {
                lost.Consume(lost.Length);
                continue;
            }

            var offset = lost.Offset;
            var next = WriteFrame(packet, position, limit, (uint)queue.Route(index) + 1, queue.EntryId(index), offset, offset + lost.Length, queue.Entry(index), out var chunk);
            if (next < 0) return position;
            position = next;
            RecordFragment(lane, lost.Message, offset, chunk);
            lost.Consume(chunk);
        }

        while (queue.HasNew && _fragmentRoom > 0)
        {
            var index = queue.Cursor;
            if (queue.Cancelled(index))
            {
                queue.Advance();
                continue;
            }

            var message = queue.Entry(index);
            var id = queue.CursorStarted || queue.Preassigned ? queue.EntryId(index) : nextId;
            var offset = queue.CursorOffset;
            var next = WriteFrame(packet, position, limit, (uint)queue.Route(index) + 1, id, offset, message.Length, message, out var chunk);
            if (next < 0) break;
            position = next;
            if (!queue.CursorStarted)
            {
                queue.CursorStarted = true;
                if (!queue.Preassigned) queue.SetEntryId(index, nextId++);
            }

            RecordFragment(lane, queue.Serial(index), offset, chunk);
            queue.CursorOffset += chunk;
            if (queue.CursorOffset < message.Length) break;
            queue.Advance();
        }

        return position;
    }

    // Writes the lost ranges of a latest route's current message, then its new fragments and those of a newer message.
    private int WriteDatagramLatest(int route, GesNetworkLatestSlot slot, Span<byte> packet, int position, int limit)
    {
        var topic = _topics[route];
        var lane = LaneLatest + route;
        var lost = slot.Lost;
        while (lost.Count > 0 && _fragmentRoom > 0)
        {
            var message = topic is null ? slot.Current : topic.Message.AsSpan(0, topic.MessageLength);
            var offset = lost.Offset;
            var next = WriteFrame(packet, position, limit, (uint)route + 1, slot.Id, offset, offset + lost.Length, message, out var chunk);
            if (next < 0) return position;
            position = next;
            RecordFragment(lane, slot.Id, offset, chunk);
            lost.Consume(chunk);
        }

        while (slot.HasCurrent && !slot.Sent && _fragmentRoom > 0)
        {
            var message = slot.Current;
            if (topic is not null)
            {
                if (!slot.Started)
                {
                    topic.MessageLength = EncodeState(topic, message, slot.NextId);
                    topic.Message ??= new byte[topic.Scratch.Length];
                    topic.Scratch.AsSpan(0, topic.MessageLength).CopyTo(topic.Message);
                }

                message = topic.Message.AsSpan(0, topic.MessageLength);
            }

            var id = slot.Started ? slot.Id : slot.NextId;
            var offset = slot.Offset;
            var next = WriteFrame(packet, position, limit, (uint)route + 1, id, offset, message.Length, message, out var chunk);
            if (next < 0) break;
            position = next;
            if (!slot.Started)
            {
                slot.Started = true;
                slot.Id = slot.NextId++;
                topic?.Add(slot.Current, _baselineLimit, slot.Id);
            }

            RecordFragment(lane, slot.Id, offset, chunk);
            slot.Offset += chunk;
            if (slot.Offset < message.Length) break;
            slot.Complete();
        }

        return position;
    }
}
