// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;

namespace GameEventScript.Generator;

// One typed end of a network: the routes its side sends and publishes, and the routes of the other side that it receives,
// each with the name of its event. Both emitters of both ports name events alike: by the root, or by verb and root when the
// other side both sends and publishes that root.
internal sealed class GesNetworkEndPlan
{
    private static readonly string[] ClientEvents = ["Connected", "Disconnected", "Tick", "TransferProgress", "TransferCancelled", "Received"];
    private static readonly string[] ServerEvents = ["Connecting", "ClientConnected", "ClientDisconnected", "Tick", "TransferProgress", "TransferCancelled", "Received"];

    private GesNetworkEndPlan(bool client, List<GesLayoutRoute> outgoing, List<(GesLayoutRoute Route, string Event)> incoming)
    {
        Client = client;
        Outgoing = outgoing;
        Incoming = incoming;
    }

    internal bool Client { get; }

    // `Client` or `Server`, the name of the typed end.
    internal string Name => Client ? "Client" : "Server";

    internal List<GesLayoutRoute> Outgoing { get; }

    internal List<(GesLayoutRoute Route, string Event)> Incoming { get; }

    internal IEnumerable<GesLayoutRoute> Topics
    {
        get
        {
            foreach (var route in Outgoing)
                if (route.Verb == "publishes") yield return route;
        }
    }

    internal bool HasTopics
    {
        get
        {
            foreach (var _ in Topics) return true;
            return false;
        }
    }

    // The events that every end of the side reports besides its received routes.
    internal string[] Fixed => Client ? ClientEvents : ServerEvents;

    internal static GesNetworkEndPlan Plan(GesLayoutNetwork network, bool client)
    {
        var side = client ? "client" : "server";
        var outgoing = network.Routes.FindAll(route => route.Side == side);
        var received = network.Routes.FindAll(route => route.Side != side);
        var incoming = received.ConvertAll(route =>
        {
            var both = received.Exists(other => other.Root == route.Root && other.Verb != route.Verb);
            return (route, both ? (route.Verb == "sends" ? "Sent" : "Published") + route.Root : route.Root);
        });
        var plan = new GesNetworkEndPlan(client, outgoing, incoming);
        var names = new List<(string, string)>();
        foreach (var name in plan.Fixed) names.Add((name, "the event " + name));
        foreach (var (route, name) in incoming) names.Add((name, $"the event of the route '{route.Side} {route.Verb} :{route.Root}'"));
        GesSchemaPlanner.Unique($"{network.Name}.{plan.Name}", names);
        return plan;
    }
}
