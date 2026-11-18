// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Text;
using GameEventScript.Api;
using Wire = GameEventScript.Network.GesNetworkWire;

namespace GameEventScript.Network;

public sealed partial class GesNetworkConnection
{
    /// <summary>
    /// Passes received bytes: one packet on a message transport, any chunk of the byte stream on a stream transport.
    /// The connection first advances to <paramref name="now"/>. Errors close the connection with their code; once
    /// closed, received bytes are ignored.
    /// </summary>
    /// <param name="data">The received bytes; they are not retained.</param>
    /// <param name="now">The current time in microseconds, not earlier than <see cref="Now"/>.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="now"/> is earlier than <see cref="Now"/>.</exception>
    public void Receive(ReadOnlySpan<byte> data, long now)
    {
        Advance(now);
        if (State == GesNetworkState.Closed) return;
        if (_transport != GesNetworkTransport.Stream)
        {
            ReceivePacket(data);
            return;
        }

        while (!data.IsEmpty && State != GesNetworkState.Closed)
        {
            if (_streamLength == 0)
            {
                var position = 0;
                var result = Wire.ReadVarint(data, ref position, out var length);
                if (result > 0 && !CheckStreamLength(length)) return;
                if (result < 0)
                {
                    Fail(GesNetworkCodes.Malformed);
                    return;
                }

                if (result > 0 && data.Length - position >= length)
                {
                    ReceivePacket(data.Slice(position, (int)length));
                    data = data.Slice(position + (int)length);
                    continue;
                }

                // The prefix or the packet is incomplete: keep the bytes, which are at most a prefix and a packet.
                if (_stream.Length == 0) _stream = new byte[_maxPacket + 5];
                data.CopyTo(_stream);
                _streamLength = data.Length;
                return;
            }

            var buffered = 0;
            var prefix = Wire.ReadVarint(_stream.AsSpan(0, _streamLength), ref buffered, out var packetLength);
            if (prefix == 0)
            {
                _stream[_streamLength++] = data[0];
                data = data.Slice(1);
                prefix = Wire.ReadVarint(_stream.AsSpan(0, _streamLength), ref buffered, out packetLength);
                if (prefix < 0)
                {
                    Fail(GesNetworkCodes.Malformed);
                    return;
                }

                if (prefix == 0 || !CheckStreamLength(packetLength)) continue;
            }

            var end = buffered + (int)packetLength;
            var take = Math.Min(end - _streamLength, data.Length);
            data.Slice(0, take).CopyTo(_stream.AsSpan(_streamLength));
            _streamLength += take;
            data = data.Slice(take);
            if (_streamLength < end) return;
            _streamLength = 0;
            ReceivePacket(_stream.AsSpan(buffered, (int)packetLength));
        }
    }

    // Checks a stream length prefix; closes the connection and returns false when it is not allowed.
    private bool CheckStreamLength(uint length)
    {
        if (length == 0) Fail(GesNetworkCodes.Malformed);
        else if (length > _maxPacket) Fail(GesNetworkCodes.PacketTooLarge);
        else return true;
        return false;
    }

    private void ReceivePacket(ReadOnlySpan<byte> packet)
    {
        if (packet.IsEmpty)
        {
            Fail(GesNetworkCodes.Malformed);
            return;
        }

        if (packet.Length > _maxPacket)
        {
            Fail(GesNetworkCodes.PacketTooLarge);
            return;
        }

        if (_datagram)
        {
            ReceiveDatagramPacket(packet);
            return;
        }

        _lastReceive = _now;
        switch (packet[0])
        {
            case Wire.Close when _keys is not { Largest: >= 0 }:
                ReceiveClose(packet);
                break;
            case Wire.Retry when _client && State == GesNetworkState.Hello && _helloSent:
                ReceiveRetry(packet);
                break;
            case Wire.Hello when !_client && State == GesNetworkState.AwaitHello:
                ReceiveHello(packet);
                break;
            case Wire.Welcome when _client && State == GesNetworkState.Hello && _helloSent:
                ReceiveWelcome(packet);
                break;
            case Wire.Data when State == GesNetworkState.Connected && !_encrypted:
                ReceiveData(packet);
                break;
            case Wire.Sealed when State == GesNetworkState.Connected && _encrypted:
                ReceiveSealed(packet);
                break;
            default:
                Fail(GesNetworkCodes.Malformed);
                break;
        }
    }

    private void ReceiveHello(ReadOnlySpan<byte> packet)
    {
        if (packet.Length < 5 || Wire.ReadUInt32(packet, 1) != Wire.Magic)
        {
            Fail(GesNetworkCodes.ProtocolMismatch);
            return;
        }

        var position = 5;
        if (!ReadVersion(packet, ref position)) return;
        if (packet.Length - position < 8)
        {
            Fail(GesNetworkCodes.Malformed);
            return;
        }

        var hash = Wire.ReadUInt64(packet, position);
        position += 8;
        if (Wire.ReadVarint(packet, ref position, out var count) != 1 || count is < 1 or > Wire.MaximumRoutes || packet.Length - position < 8 * (int)count)
        {
            Fail(GesNetworkCodes.Malformed);
            return;
        }

        var hashes = packet.Slice(position, 8 * (int)count);
        position += hashes.Length;
        if (!ReadPacketLimit(packet, ref position, out var limit) || !ReadBaselineLimit(packet, ref position, out var baseline) ||
            !ReadTransferLimits(packet, ref position)) return;
        // The cookie: up to 64 bytes on a datagram transport, which the gate checks, and none on the others.
        if (Wire.ReadVarint(packet, ref position, out var cookie) != 1 || cookie > (_datagram ? Wire.MaximumCookieBytes : 0) || packet.Length - position < cookie)
        {
            Fail(GesNetworkCodes.Malformed);
            return;
        }

        position += (int)cookie;
        if (count != _routeCount || hash != Network.Hash)
        {
            Fail(GesNetworkCodes.SchemaMismatch, MismatchDetail(hashes));
            return;
        }

        _peerPacketLimit = limit;
        _baselineLimit = Math.Min(_maxBaseline, baseline);
        ReceiveHelloSecurity(packet, position);
    }

    // The roots of the local routes the client lacks or binds differently; the network name when none differs.
    private string MismatchDetail(ReadOnlySpan<byte> hashes)
    {
        var text = new StringBuilder();
        var clientCount = hashes.Length / 8;
        for (var index = 0; index < _routeCount; index++)
        {
            if (index < clientCount && Wire.ReadUInt64(hashes, 8 * index) == Network.Roots[index].BindingHash) continue;
            if (text.Length > 0) text.Append(", ");
            text.Append(Network.Network.Routes[index].Root.Name);
        }

        return text.Length == 0 ? Network.Network.Name : text.ToString();
    }

    private void ReceiveWelcome(ReadOnlySpan<byte> packet)
    {
        if (_helloSealed)
        {
            if (!ReceiveSealedWelcome(packet) && !_datagram) Fail(GesNetworkCodes.AuthenticationFailed);
            return;
        }

        // A key request asks for a retry; only a plain hello is answered by a plain welcome.
        if (_minimumSecurity != GesNetworkSecurity.None)
        {
            Fail(GesNetworkCodes.Malformed);
            return;
        }

        AcceptWelcome(packet, 1);
    }

    // Reads the welcome fields from the position to the end and connects.
    private void AcceptWelcome(ReadOnlySpan<byte> packet, int position)
    {
        if (!ReadVersion(packet, ref position) || !ReadPacketLimit(packet, ref position, out var limit) ||
            !ReadBaselineLimit(packet, ref position, out var baseline) || !ReadTransferLimits(packet, ref position)) return;
        if (position != packet.Length)
        {
            Fail(GesNetworkCodes.Malformed);
            return;
        }

        _peerPacketLimit = limit;
        _baselineLimit = Math.Min(_maxBaseline, baseline);
        State = GesNetworkState.Connected;
        _security = !_encrypted ? GesNetworkSecurity.None : _pinned ? GesNetworkSecurity.Verified : GesNetworkSecurity.Encrypted;
        AddEvent(GesNetworkEventKind.Connected, -1, default, Counter.None, string.Empty, string.Empty, false);
    }

    private bool ReadVersion(ReadOnlySpan<byte> packet, ref int position)
    {
        if (Wire.ReadVarint(packet, ref position, out var version) != 1) Fail(GesNetworkCodes.Malformed);
        else if (version != Wire.Version) Fail(GesNetworkCodes.ProtocolMismatch);
        else return true;
        return false;
    }

    private bool ReadPacketLimit(ReadOnlySpan<byte> packet, ref int position, out int limit)
    {
        limit = 0;
        if (Wire.ReadVarint(packet, ref position, out var value) != 1 || value is < Wire.MinimumPacketBytes or > Wire.MaximumPacketBytes)
        {
            Fail(GesNetworkCodes.Malformed);
            return false;
        }

        limit = (int)value;
        return true;
    }

    private bool ReadBaselineLimit(ReadOnlySpan<byte> packet, ref int position, out long limit)
    {
        limit = 0;
        if (Wire.ReadVarint(packet, ref position, out var value) != 1 || value > Wire.MaximumBaselineBytes)
        {
            Fail(GesNetworkCodes.Malformed);
            return false;
        }

        limit = value;
        return true;
    }

    // Reads the peer's transfer limit and the mask of the compressions it accepts; unknown compressions are malformed.
    private bool ReadTransferLimits(ReadOnlySpan<byte> packet, ref int position)
    {
        if (Wire.ReadVarint(packet, ref position, out var limit) != 1 || limit > Wire.MaximumTransferBytes ||
            Wire.ReadVarint(packet, ref position, out var mask) != 1 || (mask & ~Wire.CompressionMaskDeflate) != 0)
        {
            Fail(GesNetworkCodes.Malformed);
            return false;
        }

        _peerTransferLimit = limit;
        _peerDeflate = (mask & Wire.CompressionMaskDeflate) != 0;
        return true;
    }

    private void ReceiveClose(ReadOnlySpan<byte> packet)
    {
        var position = 1;
        if (Wire.ReadVarint(packet, ref position, out var codeLength) != 1 || codeLength is < 1 or > Wire.MaximumCodeBytes || packet.Length - position < codeLength)
        {
            Fail(GesNetworkCodes.Malformed);
            return;
        }

        var code = packet.Slice(position, (int)codeLength);
        position += code.Length;
        foreach (var character in code)
        {
            if (character is < (byte)'!' or > (byte)'~')
            {
                Fail(GesNetworkCodes.Malformed);
                return;
            }
        }

        if (Wire.ReadVarint(packet, ref position, out var detailLength) != 1 || detailLength > Wire.MaximumDetailBytes || packet.Length - position != detailLength)
        {
            Fail(GesNetworkCodes.Malformed);
            return;
        }

        var detail = packet.Slice(position, (int)detailLength);
        if (!Wire.IsValidUtf8(detail))
        {
            Fail(GesNetworkCodes.Malformed);
            return;
        }

        State = GesNetworkState.Closed;
        DropSending();
        _closePending = false;
        AddEvent(GesNetworkEventKind.Closed, -1, default, Counter.None, Encoding.ASCII.GetString(code), Encoding.UTF8.GetString(detail), true);
    }

    private void ReceiveData(ReadOnlySpan<byte> packet)
    {
        if (packet.Length < 4 || (packet[1] & ~3) != 0 || Wire.ReadUInt16(packet, 2) != (ushort)_receivedCount)
        {
            Fail(GesNetworkCodes.Malformed);
            return;
        }

        var position = 4;
        if ((packet[1] & 1) != 0)
        {
            if (packet.Length - position < 6 || !Acknowledge(Wire.ReadUInt16(packet, position)))
            {
                Fail(GesNetworkCodes.Malformed);
                return;
            }

            position += 6;
        }

        // A packet with frames asks for acknowledgement.
        var asks = (packet[1] & 2) != 0;
        if (!asks && position < packet.Length)
        {
            Fail(GesNetworkCodes.Malformed);
            return;
        }

        _receivedCount++;
        while (position < packet.Length)
        {
            if (!ReceiveFrame(packet, ref position)) return;
        }

        if (asks && !_ackDue)
        {
            _ackDue = true;
            _ackDeadline = _now + _ackDelay;
        }
    }

    // Applies an acknowledgement; false when it names a sequence not sent or older than the previous one.
    private bool Acknowledge(ushort sequence)
    {
        if (_sentCount == 0) return false;
        var start = Math.Max(_ackedIndex, 0);
        var index = start + (ushort)(sequence - (ushort)start);
        if (index >= _sentCount) return false;
        if (index <= _ackedIndex) return true;
        if (_sentCount - index <= SendTimeWindow && _sendAsks[index % SendTimeWindow])
        {
            var sample = _now - _sendTimes[index % SendTimeWindow];
            RoundTripMicroseconds = RoundTripMicroseconds < 0 ? sample : RoundTripMicroseconds + (sample - RoundTripMicroseconds) / 8;
        }

        _ackedIndex = index;
        ConfirmWritten(index);
        return true;
    }

    // Reads one frame; false when it closed the connection.
    private bool ReceiveFrame(ReadOnlySpan<byte> packet, ref int position)
    {
        if (_frameUnits < MicrosecondsPerSecond) return Fail(GesNetworkCodes.RateExceeded);
        _frameUnits -= MicrosecondsPerSecond;
        if (Wire.ReadVarint(packet, ref position, out var channel) != 1 || channel > _routeCount) return Fail(GesNetworkCodes.Malformed);
        if (channel == 0)
        {
            // Control frames: a stream or message transport carries only cancels.
            if (position == packet.Length || packet[position++] != Wire.ControlCancel) return Fail(GesNetworkCodes.Malformed);
            return ReceiveCancel(packet, ref position);
        }

        var route = (int)channel - 1;
        if (_localSends[route]) return Fail(GesNetworkCodes.UnexpectedRoute);
        if (packet.Length - position < 2) return Fail(GesNetworkCodes.Malformed);
        var id = Wire.ReadUInt16(packet, position);
        position += 2;
        if (Wire.ReadVarint(packet, ref position, out var offset) != 1 || Wire.ReadVarint(packet, ref position, out var total) != 1)
            return Fail(GesNetworkCodes.Malformed);
        var bulk = _classes[route] == GesNetworkClass.Bulk;
        if (total > MessageLimit(route)) return Fail(GesNetworkCodes.MessageTooLarge);
        if (Wire.ReadVarint(packet, ref position, out var length) != 1 || packet.Length - position < length) return Fail(GesNetworkCodes.Malformed);
        var chunk = packet.Slice(position, (int)length);
        position += chunk.Length;
        if ((length == 0 && total != 0) || (ulong)offset + length > total) return Fail(GesNetworkCodes.Malformed);

        var assembly = _classes[route] switch
        {
            GesNetworkClass.Reliable => _receiveReliable,
            GesNetworkClass.Bulk => _receiveBulk,
            _ => _receiveLatest[route]!
        };
        if (offset == 0)
        {
            if (assembly.Active || id != assembly.NextId) return Fail(GesNetworkCodes.Malformed);
            assembly.NextId++;
            if (length == total)
            {
                if (bulk) AddProgress(route, id, total, total);
                return Deliver(route, id, chunk);
            }

            assembly.Start(channel, id, total);
        }
        else if (!assembly.Active || assembly.Channel != channel || assembly.Id != id || assembly.Total != total || assembly.Received != offset)
        {
            return Fail(GesNetworkCodes.Malformed);
        }

        if (bulk)
        {
            _pendingBulk += length;
            if (_pendingBulk > MaxPendingBulk) return Fail(GesNetworkCodes.QueueFull);
        }
        else
        {
            _pending += length;
            if (_pending > _maxPending) return Fail(GesNetworkCodes.QueueFull);
        }

        assembly.Append(chunk);
        if (bulk) AddProgress(route, id, assembly.Received, total);
        if (assembly.Received != total) return true;
        assembly.Active = false;
        // The message's bytes are pending already; the event counts them again.
        if (bulk) _pendingBulk -= total;
        else _pending -= total;
        return Deliver(route, id, assembly.Message);
    }

    // Reports a complete message with its id; a topic message becomes its state's full record, numbered by the id of a
    // latest message or the topic's count of reliable states. False when it closed the connection.
    private bool Deliver(int route, ushort id, ReadOnlySpan<byte> message)
    {
        if (_classes[route] == GesNetworkClass.Bulk) return DeliverTransfer(route, id, message);
        if (_topics[route] is { } topic)
        {
            if (message.IsEmpty || message[0] > GesNetworkTopic.MaximumStates) return Fail(GesNetworkCodes.Malformed);
            var number = _classes[route] == GesNetworkClass.Latest ? id : topic.Next++;
            var rest = message.Slice(1);
            if (message[0] != 0)
            {
                if (!topic.Find((ushort)(number - message[0]), out var baseline)) return Fail(GesNetworkCodes.Malformed);
                try
                {
                    var decoded = GesSchemaDelta.Decode(topic.Schema, rest, baseline, topic.Scratch);
                    if (decoded.DeltaByteCount != rest.Length) return Fail(GesNetworkCodes.Malformed);
                    rest = topic.Scratch.AsSpan(0, decoded.ByteCount);
                }
                catch (GesWireException)
                {
                    return Fail(GesNetworkCodes.Malformed);
                }
            }

            topic.Add(rest, _baselineLimit, number);
            if (_datagram && _classes[route] == GesNetworkClass.Latest) OweConfirmation(route, number);
            message = rest;
        }

        return AddEvent(GesNetworkEventKind.Message, route, message, Counter.Pending, string.Empty, string.Empty, false) || Fail(GesNetworkCodes.QueueFull);
    }

    // The largest message of a received route: on a bulk route a transfer's header and data.
    private long MessageLimit(int route) => _classes[route] == GesNetworkClass.Bulk ? TransferLimit(route) + Wire.TransferHeaderBytes : _maxBytes[route];

    // Closes the connection with an error found in received data and returns false.
    private bool Fail(string code, string detail = "")
    {
        CloseLocal(code, detail, true);
        return false;
    }
}
