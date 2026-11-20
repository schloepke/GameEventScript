// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using GameEventScript.Api;
using Wire = GameEventScript.Network.GesNetworkWire;

namespace GameEventScript.Network;

// Ticks: sets of latest topic states that the receiver reports together. One tick is in transmission at a time; a
// closed tick waits behind it and is replaced by a newer one.
public sealed partial class GesNetworkConnection
{
    private const byte StageNone = 0;
    private const byte StagePublished = 1;
    private const byte StageIncluded = 2;

    // The last published record of each latest topic this side publishes.
    private readonly byte[]?[] _lastRecords;
    private readonly int[] _lastLengths;
    private readonly bool[] _hasLast;

    // The members of the open tick, or of the closed tick that waits, with the records of the published ones.
    private bool _tickOpen;
    private bool _tickWaiting;
    private readonly byte[] _stageKinds;
    private readonly byte[]?[] _stageRecords;
    private readonly int[] _stageLengths;
    private int _stageCount;

    // The tick in transmission: its members, the states they name, the members whose state is not yet completely
    // sent, and whether its frame is due. After its frame is written it stays current for resends on a datagram
    // transport until the next tick starts.
    private bool _tickSending;
    private bool _tickCurrent;
    private bool _tickFrameDue;
    private ushort _tickNumber;
    private ushort _nextTick;
    private int _tickPending;
    private readonly bool[] _tickMembers;
    private readonly bool[] _tickUnsent;
    private readonly ushort[] _tickNames;

    // Receiving: the held member state per route, the last reported state per latest topic, the held incomplete
    // tick and the last reported tick.
    private readonly byte[]?[] _heldRecords;
    private readonly int[] _heldLengths;
    private readonly ushort[] _heldNumbers;
    private readonly bool[] _held;
    private readonly ushort[] _reportedNumbers;
    private readonly bool[] _reported;
    private bool _hasHeldTick;
    private ushort _heldTick;
    private int _heldTickCount;
    private readonly int[] _heldTickRoutes;
    private readonly ushort[] _heldTickStates;
    private bool _hasReportedTick;
    private ushort _reportedTick;

    /// <summary>
    /// Opens a tick: until <see cref="EndTick"/>, every publish on a latest topic this side publishes makes the state a
    /// member of the tick instead of sending it alone. Reliable topics, sends and transfers are never members.
    /// </summary>
    /// <exception cref="InvalidOperationException">A tick is open already.</exception>
    public void BeginTick()
    {
        if (_tickOpen) throw new InvalidOperationException("A tick is open already.");
        _tickOpen = true;
        // A closed tick that still waits is replaced by the new one: its published members become latest states.
        if (_tickWaiting) ReleaseStage();
        _tickWaiting = false;
    }

    /// <summary>
    /// Makes a latest topic a member of the open tick with its last published state, without publishing it again. Does
    /// nothing when the topic is a member already, and on a server when the client is not subscribed to it.
    /// </summary>
    /// <param name="route">The route index of a latest topic this side publishes.</param>
    /// <exception cref="InvalidOperationException">No tick is open, or the topic has no published state.</exception>
    /// <exception cref="ArgumentException">The route is no latest topic this side publishes.</exception>
    public void Include(int route)
    {
        if (!_tickOpen) throw new InvalidOperationException("No tick is open.");
        if ((uint)route >= (uint)_routeCount || !_localSends[route] || _topics[route] is not { } topic || _sendLatest[route] is null)
            throw new ArgumentException("The route is no latest topic this side publishes.", nameof(route));
        if (!_hasLast[route]) throw new InvalidOperationException("The topic has no published state.");
        if (!topic.Subscribed || _stageKinds[route] != StageNone) return;
        _stageKinds[route] = StageIncluded;
        _stageCount++;
    }

    /// <summary>
    /// Closes the open tick. Its members are sent once no earlier tick is in transmission; an empty tick sends nothing.
    /// </summary>
    /// <returns><see cref="GesNetworkSendStatus.NotConnected"/> when the connection is not connected, which drops the tick; otherwise <see cref="GesNetworkSendStatus.Queued"/>.</returns>
    /// <exception cref="InvalidOperationException">No tick is open.</exception>
    public GesNetworkSendStatus EndTick()
    {
        if (!_tickOpen) throw new InvalidOperationException("No tick is open.");
        _tickOpen = false;
        if (State != GesNetworkState.Connected)
        {
            ClearStage();
            return GesNetworkSendStatus.NotConnected;
        }

        if (_stageCount == 0) return GesNetworkSendStatus.Queued;
        _tickWaiting = true;
        if (!_tickSending) StartTick();
        return GesNetworkSendStatus.Queued;
    }

    // Publishes a state of a latest topic as a member of the open tick: it waits in the stage until the tick starts.
    private void StageState(int route, ReadOnlySpan<byte> payload)
    {
        Keep(ref _stageRecords[route], payload);
        _stageLengths[route] = payload.Length;
        if (_stageKinds[route] == StageNone) _stageCount++;
        _stageKinds[route] = StagePublished;
    }

    // Keeps the last published record of a latest topic.
    private void KeepLast(int route, ReadOnlySpan<byte> payload)
    {
        Keep(ref _lastRecords[route], payload);
        _lastLengths[route] = payload.Length;
        _hasLast[route] = true;
    }

    // Keeps a copy of a payload in a reusable buffer, which grows at least to double its size.
    internal static void Keep(ref byte[]? buffer, ReadOnlySpan<byte> payload)
    {
        if (buffer is null || buffer.Length < payload.Length) buffer = new byte[Math.Max(payload.Length, Math.Max(16, (buffer?.Length ?? 0) * 2))];
        payload.CopyTo(buffer);
    }

    // Turns the published members of the waiting tick into latest states of their routes and clears the stage.
    private void ReleaseStage()
    {
        for (var route = 0; route < _routeCount; route++)
        {
            if (_stageKinds[route] == StagePublished && _topics[route]!.Subscribed) _sendLatest[route]!.Set(_stageRecords[route].AsSpan(0, _stageLengths[route]));
        }

        ClearStage();
    }

    private void ClearStage()
    {
        Array.Clear(_stageKinds, 0, _stageKinds.Length);
        _stageCount = 0;
    }

    // Starts the waiting tick: published members become latest states of their routes, and an included member names
    // the topic's newest state when the receiver holds it, else publishes that state again.
    private void StartTick()
    {
        _tickWaiting = false;
        _tickSending = true;
        _tickCurrent = false;
        _tickFrameDue = false;
        _tickPending = 0;
        Array.Clear(_tickMembers, 0, _tickMembers.Length);
        Array.Clear(_tickUnsent, 0, _tickUnsent.Length);
        var members = 0;
        for (var route = 0; route < _routeCount; route++)
        {
            var kind = _stageKinds[route];
            if (kind == StageNone) continue;
            _stageKinds[route] = StageNone;
            if (!_topics[route]!.Subscribed) continue;
            var slot = _sendLatest[route]!;
            members++;
            _tickMembers[route] = true;
            if (kind == StageIncluded && NamedState(slot, route, out var named))
            {
                _tickNames[route] = named;
                continue;
            }

            slot.Set(kind == StagePublished ? _stageRecords[route].AsSpan(0, _stageLengths[route]) : _lastRecords[route].AsSpan(0, _lastLengths[route]), true);
            _tickUnsent[route] = true;
            _tickPending++;
        }

        _stageCount = 0;
        if (members == 0) _tickSending = false;
        else _tickFrameDue = _tickPending == 0;
    }

    // The newest state of a topic when the receiver holds it and no newer state waits: on a stream or message
    // transport the newest completely sent state, on a datagram transport a confirmed one.
    private bool NamedState(GesNetworkLatestSlot slot, int route, out ushort number)
    {
        number = slot.Id;
        if (!slot.HasSent || slot.HasWaiting) return false;
        if (!_datagram) return !slot.HasCurrent;
        return slot.HasCurrent && slot.Sent && slot.Lost.Count == 0 && _topics[route]!.IsConfirmed(slot.Id);
    }

    // Notes that the member state of a route is completely sent with its number.
    private void CompleteMember(int route, ushort number)
    {
        if (!_tickSending || !_tickUnsent[route]) return;
        _tickUnsent[route] = false;
        _tickNames[route] = number;
        if (--_tickPending == 0) _tickFrameDue = true;
    }

    // Removes the member of a route whose state was dropped before it started; a tick left without members ends.
    private void DropMember(int route)
    {
        if (!_tickSending || !_tickUnsent[route]) return;
        _tickUnsent[route] = _tickMembers[route] = false;
        _tickPending--;
        var any = false;
        for (var index = 0; index < _routeCount; index++) any |= _tickMembers[index];
        if (!any)
        {
            _tickSending = false;
            if (_tickWaiting) StartTick();
        }
        else if (_tickPending == 0)
        {
            _tickFrameDue = true;
        }
    }

    private void DropTicks()
    {
        ClearStage();
        _tickWaiting = _tickSending = _tickCurrent = _tickFrameDue = false;
    }

    private int TickFrameSize()
    {
        var size = 4;
        var count = 0;
        for (var route = 0; route < _routeCount; route++)
        {
            if (!_tickMembers[route]) continue;
            count++;
            size += Wire.VarintSize((uint)route + 1) + 2;
        }

        return size + Wire.VarintSize((uint)count);
    }

    // Writes the due tick frame when it fits; the first write numbers the tick and starts a waiting one.
    private int WriteTickFrame(Span<byte> packet, int position, int limit)
    {
        if (!_tickFrameDue || limit - position < TickFrameSize() || (_datagram && _fragmentRoom <= 0)) return position;
        if (_tickSending) _tickNumber = _nextTick++;
        var count = 0;
        for (var route = 0; route < _routeCount; route++) count += _tickMembers[route] ? 1 : 0;
        position = Wire.WriteVarint(packet, position, 0);
        packet[position++] = Wire.ControlTick;
        Wire.WriteUInt16(packet, position, _tickNumber);
        position = Wire.WriteVarint(packet, position + 2, (uint)count);
        for (var route = 0; route < _routeCount; route++)
        {
            if (!_tickMembers[route]) continue;
            position = Wire.WriteVarint(packet, position, (uint)route + 1);
            Wire.WriteUInt16(packet, position, _tickNames[route]);
            position += 2;
        }

        if (_datagram) RecordFragment(LaneTick, _tickNumber, 0, 0);
        _tickFrameDue = false;
        if (!_tickSending) return position;
        _tickSending = false;
        _tickCurrent = true;
        if (_tickWaiting) StartTick();
        return position;
    }

    // Resends a lost tick frame while its tick is current and no newer one started.
    private void LoseTickFrame(ushort number)
    {
        if (_tickCurrent && !_tickSending && number == _tickNumber) _tickFrameDue = true;
    }

    // ---- Receiving ----

    // Holds a received member state until its tick is complete; it replaces an older held state of the route.
    private bool HoldMember(int route, ushort number, ReadOnlySpan<byte> record)
    {
        if (_held[route]) _pending -= _heldLengths[route];
        Keep(ref _heldRecords[route], record);
        _heldLengths[route] = record.Length;
        _heldNumbers[route] = number;
        _held[route] = true;
        _pending += record.Length;
        if (_pending > _maxPending) return Fail(GesNetworkCodes.QueueFull);
        return CompleteTick();
    }

    // Reads a tick frame after its kind byte; false when it closed the connection.
    private bool ReceiveTick(ReadOnlySpan<byte> packet, ref int position)
    {
        if (packet.Length - position < 2) return Fail(GesNetworkCodes.Malformed);
        var number = Wire.ReadUInt16(packet, position);
        position += 2;
        if (Wire.ReadVarint(packet, ref position, out var count) != 1 || count == 0 || count > _routeCount) return Fail(GesNetworkCodes.Malformed);
        var start = position;
        var previous = 0u;
        for (var index = 0; index < count; index++)
        {
            if (Wire.ReadVarint(packet, ref position, out var channel) != 1 || channel <= previous || channel > _routeCount || packet.Length - position < 2)
                return Fail(GesNetworkCodes.Malformed);
            var route = (int)channel - 1;
            if (_localSends[route] || _classes[route] != GesNetworkClass.Latest || _topics[route] is null) return Fail(GesNetworkCodes.Malformed);
            position += 2;
            previous = channel;
        }

        // A tick 1 to 32767 ahead of the held one, or of the last reported one when none is held, replaces it.
        if (_hasHeldTick || _hasReportedTick)
        {
            var ahead = (ushort)(number - (_hasHeldTick ? _heldTick : _reportedTick));
            if (ahead is 0 or >= 32768) return true;
        }

        var end = position;
        position = start;
        for (var index = 0; index < count; index++)
        {
            Wire.ReadVarint(packet, ref position, out var channel);
            _heldTickRoutes[index] = (int)channel - 1;
            _heldTickStates[index] = Wire.ReadUInt16(packet, position);
            position += 2;
        }

        position = end;
        _hasHeldTick = true;
        _heldTick = number;
        _heldTickCount = (int)count;
        return CompleteTick();
    }

    // Whether the receiver holds the state of a route with the number: held, last reported or in the ring.
    private bool Holds(int route, ushort number) =>
        (_held[route] && _heldNumbers[route] == number) || (_reported[route] && _reportedNumbers[route] == number) || _topics[route]!.Find(number, out _);

    // Reports the held tick once every state it names is held: a message for each member whose state differs from the
    // last reported one, then the tick. False when it closed the connection.
    private bool CompleteTick()
    {
        if (!_hasHeldTick) return true;
        for (var index = 0; index < _heldTickCount; index++)
        {
            if (!Holds(_heldTickRoutes[index], _heldTickStates[index])) return true;
        }

        _hasHeldTick = false;
        for (var index = 0; index < _heldTickCount; index++)
        {
            var route = _heldTickRoutes[index];
            var number = _heldTickStates[index];
            var fromHeld = _held[route] && _heldNumbers[route] == number;
            ReleaseHeld(route, number);
            // A state no newer than the last reported one of its topic is not reported again.
            if (_reported[route] && (ushort)(number - _reportedNumbers[route]) is 0 or >= 32768) continue;
            ReadOnlySpan<byte> record;
            if (fromHeld) record = _heldRecords[route].AsSpan(0, _heldLengths[route]);
            else _topics[route]!.Find(number, out record);
            if (!AddEvent(GesNetworkEventKind.Message, route, record, Counter.Pending, string.Empty, string.Empty, false)) return Fail(GesNetworkCodes.QueueFull);
            _reported[route] = true;
            _reportedNumbers[route] = number;
        }

        _hasReportedTick = true;
        _reportedTick = _heldTick;
        return ReserveEvent(GesNetworkEventKind.Tick, -1, 0, Counter.None, string.Empty, string.Empty, false, _heldTick, 0, 0, out _) || Fail(GesNetworkCodes.QueueFull);
    }

    // Drops the held state of a route unless it is newer than the reported number.
    private void ReleaseHeld(int route, ushort number)
    {
        if (!_held[route] || (ushort)(_heldNumbers[route] - number) is > 0 and < 32768) return;
        _held[route] = false;
        _pending -= _heldLengths[route];
    }

    // Notes a latest topic state reported alone, which counts as the last reported state of its topic.
    private void NoteReported(int route, ushort number)
    {
        _reported[route] = true;
        _reportedNumbers[route] = number;
    }
}
