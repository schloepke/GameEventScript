// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using GameEventScript.Api;

namespace GameEventScript.Network;

// What the ends ask a connection without sending: the checks of Send and Publish apart from the state, and the kept
// states of latest topics.
public sealed partial class GesNetworkConnection
{
    internal long MaxSendQueueBytes => _maxSendQueue;

    // The status Send or Publish would give for a payload of this length on a connected connection, apart from the
    // queue and, on a server, the subscription.
    internal GesNetworkSendStatus Check(int route, int length, bool publish)
    {
        if ((uint)route >= (uint)_routeCount || !_localSends[route] || (_topics[route] is null) == publish) return GesNetworkSendStatus.InvalidRoute;
        if (_classes[route] == GesNetworkClass.Bulk) return length > TransferLimit(route) ? GesNetworkSendStatus.MessageTooLarge : GesNetworkSendStatus.Queued;
        return publish ? length >= _maxBytes[route] ? GesNetworkSendStatus.MessageTooLarge : GesNetworkSendStatus.Queued
            : length > _maxBytes[route] ? GesNetworkSendStatus.MessageTooLarge : GesNetworkSendStatus.Queued;
    }

    // Whether the route is a latest topic this side publishes: one a tick can name.
    internal bool IsLocalLatestTopic(int route) =>
        (uint)route < (uint)_routeCount && _localSends[route] && _topics[route] is not null && _sendLatest[route] is not null;

    // Whether the route queues reliably: a reliable send or a reliable topic.
    internal bool IsReliable(int route) => _classes[route] == GesNetworkClass.Reliable;

    internal bool IsLatest(int route) => _classes[route] == GesNetworkClass.Latest;

    internal bool IsBulk(int route) => _classes[route] == GesNetworkClass.Bulk;

    // Whether the last published state of a latest topic is the record.
    internal bool LastIs(int route, ReadOnlySpan<byte> record) => _hasLast[route] && record.SequenceEqual(_lastRecords[route].AsSpan(0, _lastLengths[route]));

    internal bool IsSubscribed(int route) => _topics[route]?.Subscribed == true;

    internal int RouteCount => _routeCount;

    internal bool TickOpen => _tickOpen;
}
