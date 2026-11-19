// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using Wire = GameEventScript.Network.GesNetworkWire;

namespace GameEventScript.Network;

public sealed partial class GesNetworkConnection
{
    /// <summary>
    /// Writes the next packet to transmit into the buffer: on a stream transport with its length prefix. Call it
    /// until it returns 0.
    /// </summary>
    /// <param name="buffer">The target, at least 1200 bytes.</param>
    /// <returns>The number of bytes written; 0 when nothing is due.</returns>
    /// <exception cref="ArgumentException">The buffer holds fewer than 1200 bytes.</exception>
    public int Transmit(Span<byte> buffer)
    {
        if (buffer.Length < Wire.MinimumPacketBytes) throw new ArgumentException("The buffer holds at least 1200 bytes.", nameof(buffer));
        // A stream packet is written behind room for the longest prefix of a 65535-byte packet and then moved.
        var prefix = _transport == GesNetworkTransport.Stream ? 3 : 0;
        var packet = buffer.Slice(prefix);
        int length;
        if (_closePending)
        {
            length = _sealing ? WriteSealedClose(packet) : WriteClose(packet);
            _closePending = false;
        }
        else if (State == GesNetworkState.Closed)
        {
            return 0;
        }
        else if (_client && !_helloSent)
        {
            length = WriteHello(packet);
            if (length < 0)
            {
                // The server key gives a shared secret of zeros: no hello can be sealed with it.
                CloseLocal(GesNetworkCodes.AuthenticationFailed, string.Empty, true);
                length = WriteClose(packet);
                _closePending = false;
            }
            else
            {
                _helloSent = true;
                if (_datagram) NoteHelloSent();
            }
        }
        else if (_retryPending)
        {
            length = WriteRetry(packet);
            _retryPending = false;
        }
        else if (_welcomePending)
        {
            length = WriteWelcome(packet);
            _welcomePending = false;
        }
        else if (State == GesNetworkState.Connected)
        {
            var limit = (int)Math.Min(SendPacketLimit, packet.Length);
            length = _encrypted ? WriteSealedData(packet, limit) : _datagram ? WriteDatagramData(packet, limit) : WriteData(packet, limit);
            if (length == 0) return 0;
        }
        else
        {
            return 0;
        }

        _lastSend = _now;
        if (prefix == 0) return length;
        var size = Wire.VarintSize((uint)length);
        buffer.Slice(prefix, length).CopyTo(buffer.Slice(size));
        Wire.WriteVarint(buffer, 0, (uint)length);
        return size + length;
    }

    private int WriteHello(Span<byte> packet)
    {
        packet[0] = Wire.Hello;
        Wire.WriteUInt32(packet, 1, Wire.Magic);
        var position = Wire.WriteVarint(packet, 5, Wire.Version);
        Wire.WriteUInt64(packet, position, Network.Hash);
        position = Wire.WriteVarint(packet, position + 8, (uint)_routeCount);
        for (var index = 0; index < _routeCount; index++)
        {
            Wire.WriteUInt64(packet, position, Network.Roots[index].BindingHash);
            position += 8;
        }

        position = Wire.WriteVarint(packet, position, (uint)_maxPacket);
        position = Wire.WriteVarint(packet, position, (uint)_maxBaseline);
        position = WriteTransferLimits(packet, position);
        position = Wire.WriteVarint(packet, position, (uint)_cookie.Length);
        _cookie.CopyTo(packet.Slice(position));
        position = WriteHelloSecurity(packet, position + _cookie.Length);
        if (position < 0 || !_datagram || position >= Wire.MinimumPacketBytes) return position;
        packet.Slice(position, Wire.MinimumPacketBytes - position).Clear();
        return Wire.MinimumPacketBytes;
    }

    private int WriteWelcome(Span<byte> packet)
    {
        if (_encrypted) return WriteSealedWelcome(packet);
        packet[0] = Wire.Welcome;
        return WriteWelcomeFields(packet, 1);
    }

    // Writes the welcome fields from the position on and returns the end.
    private int WriteWelcomeFields(Span<byte> packet, int position)
    {
        position = Wire.WriteVarint(packet, position, Wire.Version);
        position = Wire.WriteVarint(packet, position, (uint)_maxPacket);
        position = Wire.WriteVarint(packet, position, (uint)_maxBaseline);
        return WriteTransferLimits(packet, position);
    }

    // Writes the transfer limit and the mask of the compressions this side accepts.
    private int WriteTransferLimits(Span<byte> packet, int position)
    {
        position = Wire.WriteVarint(packet, position, (uint)_maxTransfer);
        return Wire.WriteVarint(packet, position, _deflate ? Wire.CompressionMaskDeflate : 0);
    }

    private int WriteClose(Span<byte> packet)
    {
        packet[0] = Wire.Close;
        var position = Wire.WriteVarint(packet, 1, (uint)_closeCode.Length);
        foreach (var character in _closeCode) packet[position++] = (byte)character;
        position = Wire.WriteVarint(packet, position, (uint)_closeDetail.Length);
        _closeDetail.CopyTo(packet.Slice(position));
        return position + _closeDetail.Length;
    }

    // Writes a data packet when frames, an acknowledgement or the keepalive are due; 0 otherwise.
    private int WriteData(Span<byte> packet, int limit)
    {
        var frames = HasPendingFrames() && (_budget == 0 || _budgetUnits >= 0);
        var keepalive = _keepalive != 0 && _now - _lastSend >= _keepalive;
        if (!frames && !(_ackDue && _now >= _ackDeadline) && !keepalive) return 0;
        packet[0] = Wire.Data;
        packet[1] = 0;
        Wire.WriteUInt16(packet, 2, (ushort)_sentCount);
        var position = 4;
        if (_receivedCount > 0)
        {
            packet[1] = 1;
            Wire.WriteUInt16(packet, position, (ushort)(_receivedCount - 1));
            var earlier = (int)Math.Min(32, _receivedCount - 1);
            Wire.WriteUInt32(packet, position + 2, earlier == 32 ? uint.MaxValue : (1u << earlier) - 1);
            position += 6;
        }

        var start = position;
        if (frames)
        {
            position = WriteCancels(packet, position, limit);
            position = WriteQueue(_sendReliable, ref _nextReliableId, packet, position, limit);
            for (var route = 0; route < _routeCount; route++)
            {
                if (_sendLatest[route] is { } slot) position = WriteLatest(route, slot, packet, position, limit);
            }

            position = WriteTickFrame(packet, position, limit);

            position = WriteQueue(_sendBulk, ref _nextBulkId, packet, position, limit);
        }

        // Frames and keepalives ask for acknowledgement; a packet that only acknowledges does not.
        var asks = position > start || keepalive;
        if (asks) packet[1] |= 2;
        _sendTimes[_sentCount % SendTimeWindow] = _now;
        _sendAsks[_sentCount % SendTimeWindow] = asks;
        _bulkMarks[_sentCount % SendTimeWindow] = _bulkWritten;
        _sentCount++;
        _ackDue = false;
        if (position > start && _budget > 0) _budgetUnits -= position * MicrosecondsPerSecond;
        return position;
    }

    // Writes fragments of reliable or bulk messages until the packet is full or the queue is empty.
    private int WriteQueue(GesNetworkSendQueue queue, ref ushort nextId, Span<byte> packet, int position, int limit)
    {
        while (queue.Count > 0)
        {
            if (queue.Preassigned && queue.Cancelled(0))
            {
                // A cancelled transfer tells the peer in queue order; one the peer refused needs no cancel.
                var code = queue.Code(0);
                if (code.Length > 0)
                {
                    if (limit - position < CancelFrameSize(queue.CurrentRoute, code)) break;
                    position = WriteCancelFrame(packet, position, queue.CurrentRoute, queue.EntryId(0), code);
                }

                queue.Pop();
                continue;
            }

            var message = queue.Current;
            var id = queue.Preassigned ? queue.EntryId(0) : queue.Started ? queue.Id : nextId;
            var next = WriteFrame(packet, position, limit, (uint)queue.CurrentRoute + 1, id, queue.Offset, message, out var chunk);
            if (next < 0) break;
            position = next;
            if (!queue.Started)
            {
                queue.Started = true;
                queue.Id = queue.Preassigned ? id : nextId++;
                if (queue.Preassigned) NoteTransferStarted(queue.CurrentRoute, id, message.Length);
            }

            if (queue.Preassigned) _bulkWritten += chunk;
            queue.Offset += chunk;
            if (queue.Offset < message.Length) break;
            _topics[queue.CurrentRoute]?.MarkSent(_sentCount);
            queue.Pop();
        }

        return position;
    }

    // Writes fragments of a latest route's current message and of the newer one that then takes its place. A topic
    // state is encoded when its first fragment is written and numbered only when that fragment fits.
    private int WriteLatest(int route, GesNetworkLatestSlot slot, Span<byte> packet, int position, int limit)
    {
        var topic = _topics[route];
        while (slot.HasCurrent)
        {
            var message = slot.Current;
            if (topic is not null)
            {
                if (!slot.Started)
                {
                    topic.MessageLength = EncodeState(topic, message, slot.NextId, slot.CurrentMember);
                    topic.Message ??= new byte[topic.Scratch.Length];
                    topic.Scratch.AsSpan(0, topic.MessageLength).CopyTo(topic.Message);
                }

                message = topic.Message.AsSpan(0, topic.MessageLength);
            }

            var id = slot.Started ? slot.Id : slot.NextId;
            var next = WriteFrame(packet, position, limit, (uint)route + 1, id, slot.Offset, message, out var chunk);
            if (next < 0) break;
            position = next;
            if (!slot.Started)
            {
                slot.Started = true;
                slot.Id = slot.NextId++;
                topic?.Add(slot.Current, _baselineLimit, slot.Id);
            }

            slot.Offset += chunk;
            if (slot.Offset < message.Length) break;
            topic?.MarkSent(_sentCount);
            if (slot.CurrentMember) CompleteMember(route, slot.Id);
            slot.Complete();
        }

        return position;
    }

    // Writes the largest fragment of a message at its offset that fits; -1 when none fits.
    private static int WriteFrame(Span<byte> packet, int position, int limit, uint channel, ushort id, int offset, ReadOnlySpan<byte> message, out int chunk) =>
        WriteFrame(packet, position, limit, channel, id, offset, message.Length, message, out chunk);

    // Writes the largest fragment of the message's bytes from the offset before the end that fits; -1 when none fits.
    private static int WriteFrame(Span<byte> packet, int position, int limit, uint channel, ushort id, int offset, int end, ReadOnlySpan<byte> message, out int chunk)
    {
        chunk = 0;
        var total = message.Length;
        var space = limit - position - Wire.VarintSize(channel) - 2 - Wire.VarintSize((uint)offset) - Wire.VarintSize((uint)total);
        if (space < 1) return -1;
        var remaining = end - offset;
        var length = Math.Min(remaining, space - 1);
        while (Wire.VarintSize((uint)length) + length > space) length--;
        if (length == 0 && remaining > 0) return -1;
        position = Wire.WriteVarint(packet, position, channel);
        Wire.WriteUInt16(packet, position, id);
        position = Wire.WriteVarint(packet, position + 2, (uint)offset);
        position = Wire.WriteVarint(packet, position, (uint)total);
        position = Wire.WriteVarint(packet, position, (uint)length);
        message.Slice(offset, length).CopyTo(packet.Slice(position));
        chunk = length;
        return position + length;
    }
}
