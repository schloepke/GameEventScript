// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using GameEventScript.Syntax;
using Code = GameEventScript.Syntax.GesSchemaDiagnostic;

namespace GameEventScript.Api;

/// <summary>The side a network route starts from.</summary>
public enum GesNetworkSide
{
    /// <summary>The route goes from the client to the server.</summary>
    Client,

    /// <summary>The route goes from the server to the client.</summary>
    Server
}

/// <summary>How a network route carries its root.</summary>
public enum GesNetworkVerb
{
    /// <summary>The route sends single values of a schema, a message or a message set.</summary>
    Sends,

    /// <summary>The route publishes the states of a schema, a topic.</summary>
    Publishes
}

/// <summary>The delivery class of a network route.</summary>
public enum GesNetworkClass
{
    /// <summary>Every message arrives once, in order.</summary>
    Reliable,

    /// <summary>Only the newest message counts; an older one may be dropped.</summary>
    Latest,

    /// <summary>Reliable, in order, and sent only when no other message waits.</summary>
    Bulk
}

/// <summary>One route of a network. Immutable.</summary>
public sealed class GesNetworkRoute
{
    internal GesNetworkRoute(int index, GesNetworkSide side, GesNetworkVerb verb, GesSchema root, GesNetworkClass deliveryClass, long rateMicroseconds)
    {
        Index = index;
        Side = side;
        Verb = verb;
        Root = root;
        DeliveryClass = deliveryClass;
        RateMicroseconds = rateMicroseconds;
    }

    /// <summary>Gets the route's position in its network, counting from 0.</summary>
    public int Index { get; }

    /// <summary>Gets the route's channel on the wire, its index plus 1.</summary>
    public int Channel => Index + 1;

    /// <summary>Gets the side the route starts from.</summary>
    public GesNetworkSide Side { get; }

    /// <summary>Gets whether the route sends or publishes.</summary>
    public GesNetworkVerb Verb { get; }

    /// <summary>Gets the schema, message or message set the route carries.</summary>
    public GesSchema Root { get; }

    /// <summary>Gets the delivery class.</summary>
    public GesNetworkClass DeliveryClass { get; }

    /// <summary>Gets the shortest interval between two updates of a topic in microseconds, rounded up; 0 without a rate.</summary>
    public long RateMicroseconds { get; }
}

/// <summary>An <c>owns</c> route of a network: the side that owns the objects of a handle type. Immutable.</summary>
public sealed class GesNetworkOwner
{
    internal GesNetworkOwner(GesNetworkSide side, GesHandleType handle)
    {
        Side = side;
        Handle = handle;
    }

    /// <summary>Gets the side that owns the objects and can resolve their handles.</summary>
    public GesNetworkSide Side { get; }

    /// <summary>Gets the owned handle type, which is never composite.</summary>
    public GesHandleType Handle { get; }
}

/// <summary>A network declaration: the routes of one client–server pair. Immutable; it can be bound any number of times.</summary>
public sealed class GesNetwork
{
    internal GesNetwork(string name, GesNetworkRoute[] routes, GesNetworkOwner[] owners)
    {
        Name = name;
        Routes = new GesReadOnlyArray<GesNetworkRoute>(routes);
        Owners = new GesReadOnlyArray<GesNetworkOwner>(owners);
    }

    /// <summary>Gets the declared name without the leading <c>:</c>.</summary>
    public string Name { get; }

    /// <summary>Gets the routes in declared order; a route's position is its index.</summary>
    public GesReadOnlyArray<GesNetworkRoute> Routes { get; }

    /// <summary>Gets the <c>owns</c> routes in ascending scalar order of the handle type's name; they have no route index.</summary>
    public GesReadOnlyArray<GesNetworkOwner> Owners { get; }

    /// <summary>Binds every route root with the same parameter values.</summary>
    /// <param name="parameters">The values of the parameters the roots reach, if they reach any.</param>
    /// <returns>The immutable bound network.</returns>
    /// <exception cref="GesSchemaException">A root cannot be bound, as for <see cref="GesSchema.Bind"/>.</exception>
    public GesBoundNetwork Bind(GesSchemaParameters? parameters = null)
    {
        parameters ??= new GesSchemaParameters();
        var roots = new GesBoundSchema[Routes.Count];
        for (var index = 0; index < roots.Length; index++) roots[index] = Routes[index].Root.Bind(parameters);
        return new GesBoundNetwork(this, roots);
    }
}

/// <summary>A network bound to parameter values. Immutable; it can be shared between threads and connections.</summary>
public sealed class GesBoundNetwork
{
    internal GesBoundNetwork(GesNetwork network, GesBoundSchema[] roots)
    {
        Network = network;
        Roots = new GesReadOnlyArray<GesBoundSchema>(roots);
        CanonicalForm = Form(network, roots);
        Hash = GesSchemaCanonical.Hash(CanonicalForm);
    }

    /// <summary>Gets the declaration this binding comes from.</summary>
    public GesNetwork Network { get; }

    /// <summary>Gets the bound root of each route, by route index.</summary>
    public GesReadOnlyArray<GesBoundSchema> Roots { get; }

    /// <summary>Gets the canonical form the network hash runs over.</summary>
    public string CanonicalForm { get; }

    /// <summary>Gets the network hash: FNV-1a 64 over the canonical form, which the handshake compares.</summary>
    public ulong Hash { get; }

    private static string Form(GesNetwork network, GesBoundSchema[] roots)
    {
        var text = new StringBuilder();
        text.Append("gesn1\nnetwork ").Append(network.Name).Append('\n');
        for (var index = 0; index < roots.Length; index++)
        {
            var route = network.Routes[index];
            text.Append("route ")
                .Append(route.Side == GesNetworkSide.Client ? "client" : "server").Append(' ')
                .Append(route.Verb == GesNetworkVerb.Sends ? "sends" : "publishes").Append(' ')
                .Append(route.Root.Name).Append(' ')
                .Append(ClassName(route.DeliveryClass)).Append(' ')
                .Append(route.RateMicroseconds.ToString(CultureInfo.InvariantCulture)).Append(' ')
                .Append(GesSchemaCanonical.Hex(roots[index].BindingHash)).Append('\n');
        }

        foreach (var owner in network.Owners)
        {
            text.Append("owns ")
                .Append(owner.Side == GesNetworkSide.Client ? "client" : "server").Append(' ')
                .Append(owner.Handle.Name).Append(' ')
                .Append(GesSchemaCanonical.Hex(owner.Handle.Hash)).Append(' ')
                .Append(GesSchemaCanonical.Hex(owner.Handle.Resolved.State.Public.Hash)).Append('\n');
        }

        return text.ToString();
    }

    internal static string ClassName(GesNetworkClass deliveryClass) => deliveryClass switch
    {
        GesNetworkClass.Reliable => "reliable",
        GesNetworkClass.Latest => "latest",
        _ => "bulk"
    };
}

internal sealed partial class GesFileScope
{
    // The largest rate, 3600 seconds, in microseconds.
    private const long MaximumRateMicroseconds = 3_600_000_000;

    // Resolves the networks of this source once every declaration has its public description.
    internal void ResolveNetworks(Dictionary<string, GesResolvedDeclaration> all, Dictionary<string, GesResolvedDeclaration> messages, List<GesNetwork> networks)
    {
        foreach (var syntax in _networks)
        {
            var routes = new List<GesNetworkRoute>(syntax.Routes.Length);
            var owners = new List<GesNetworkOwner>();
            var seen = new HashSet<(bool, bool, GesResolvedDeclaration)>();
            foreach (var route in syntax.Routes)
            {
                if (route.Owns)
                {
                    var handle = Handle(route.Root, route.RootOffset);
                    if (handle.Root is not null)
                        throw Error(Code.InvalidHandle, route.RootOffset, $"'@{route.Root}' is composite; its root's owner owns it.");
                    if (owners.Exists(owner => owner.Handle.Name == route.Root))
                        throw Error(Code.DuplicateRoute, route.Offset, $"The network names '@{route.Root}' in two owns routes.");
                    owners.Add(new GesNetworkOwner(route.Server ? GesNetworkSide.Server : GesNetworkSide.Client, handle.Public));
                    continue;
                }

                var index = routes.Count;
                // A route names a message or message set without a colon and a schema with one.
                if (!(route.IsMessage ? messages : all).TryGetValue(route.Root, out var root))
                    throw Error(Code.UnknownName, route.RootOffset, route.IsMessage ? $"Message or message set '{route.Root}' is not declared."
                        : messages.TryGetValue(route.Root, out var set) && set.Kind == GesSchemaKind.Messages
                            ? $"':{route.Root}' is no declared schema; a message set is named without ':', as '{route.Root}'."
                            : $"':{route.Root}' is no declared schema.");
                if (root.Syntax.WireGroup is null)
                    throw Error(Code.InvalidRoute, route.RootOffset, $"'{route.Root}' has no wire role.");
                if (root.Abstract) throw Error(Code.InvalidRoute, route.RootOffset, $"'{route.Root}' is abstract.");
                if (route.Publishes && root.Kind != GesSchemaKind.Schema)
                    throw Error(Code.InvalidRoute, route.RootOffset, $"'{route.Root}' is no schema; only a schema is published.");
                var deliveryClass = route.Class switch
                {
                    GesSchemaRouteClass.Reliable => GesNetworkClass.Reliable,
                    GesSchemaRouteClass.Latest => GesNetworkClass.Latest,
                    _ => GesNetworkClass.Bulk
                };
                if (route.Publishes ? deliveryClass == GesNetworkClass.Bulk : deliveryClass == GesNetworkClass.Latest)
                    throw Error(Code.InvalidRoute, route.ClassOffset, route.Publishes ? "A topic is published latest or reliable." : "A route sends reliable or bulk.");
                long rate = 0;
                if (route.Rate is { } value)
                {
                    if (!route.Publishes) throw Error(Code.InvalidRoute, value.Offset, "Only a published topic has a rate.");
                    rate = RateMicroseconds(value);
                }

                if (!seen.Add((route.Server, route.Publishes, root)))
                    throw Error(Code.DuplicateRoute, route.Offset, $"The {(route.Server ? "server" : "client")} {(route.Publishes ? "publishes" : "sends")} '{route.Root}' twice.");
                routes.Add(new GesNetworkRoute(index, route.Server ? GesNetworkSide.Server : GesNetworkSide.Client,
                    route.Publishes ? GesNetworkVerb.Publishes : GesNetworkVerb.Sends, root.Public, deliveryClass, rate));
            }

            owners.Sort((left, right) => string.CompareOrdinal(left.Handle.Name, right.Handle.Name));
            networks.Add(new GesNetwork(syntax.Name, routes.ToArray(), owners.ToArray()));
        }
    }

    // A rate in microseconds, rounded up: a time quantity greater than zero and at most 3600 seconds.
    private long RateMicroseconds(GesSchemaValueSyntax value)
    {
        var literal = value;
        if (value.Constant.Length > 0 && !_constants.TryGetValue(value.Constant, out literal!))
            throw Error(Code.UnknownName, value.Offset, $"Constant '${value.Constant}' is not declared in this file.");
        if (literal.Unit != GesQuantityUnit.UnitSecond || literal.IsPercent)
            throw Error(Code.InvalidRoute, value.Offset, "A rate is a time quantity.");
        var significand = literal.Value.Significand;
        if (significand <= 0) throw Error(Code.InvalidRoute, value.Offset, "A rate is greater than zero.");
        // The literal is significand × 10^exponent seconds, so significand × 10^(exponent + 6) microseconds.
        var shift = literal.Value.Exponent + 6;
        var micros = significand;
        if (shift >= 0)
        {
            for (var step = 0; step < shift && micros <= MaximumRateMicroseconds; step++) micros *= 10;
        }
        else
        {
            var remainder = false;
            for (var step = 0; step < -shift && micros != 0; step++)
            {
                remainder |= micros % 10 != 0;
                micros /= 10;
            }

            if (remainder) micros++;
        }

        if (micros > MaximumRateMicroseconds) throw Error(Code.InvalidRoute, value.Offset, "A rate is at most 3600s.");
        return micros;
    }
}
