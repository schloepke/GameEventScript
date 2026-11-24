// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

// One typed end of a network: the routes its side sends and publishes, and the routes of the other side that it receives,
// each with the name of its event. Both emitters of both ports name events alike: by the root, or by verb and root when the
// other side both sends and publishes that root.
struct GesNetworkEndPlan {
    private static let clientEvents = ["Connected", "Disconnected", "Tick", "TransferProgress", "TransferCancelled", "Received"]
    private static let serverEvents = ["Connecting", "ClientConnected", "ClientDisconnected", "Tick", "TransferProgress", "TransferCancelled", "Received"]

    let client: Bool
    let outgoing: [GesLayoutRoute]
    let incoming: [(route: GesLayoutRoute, event: String)]

    // `Client` or `Server`, the name of the typed end.
    var name: String { client ? "Client" : "Server" }

    var topics: [GesLayoutRoute] { outgoing.filter { $0.verb == "publishes" } }

    var hasTopics: Bool { outgoing.contains { $0.verb == "publishes" } }

    // The events that every end of the side reports besides its received routes.
    var fixed: [String] { client ? Self.clientEvents : Self.serverEvents }

    static func plan(_ network: GesLayoutNetwork, client: Bool) throws(GesGeneratorError) -> GesNetworkEndPlan {
        let side = client ? "client" : "server"
        let outgoing = network.routes.filter { $0.side == side }
        let received = network.routes.filter { $0.side != side }
        let incoming = received.map { route in
            let both = received.contains { $0.root == route.root && $0.verb != route.verb }
            return (route: route, event: both ? (route.verb == "sends" ? "Sent" : "Published") + route.root : route.root)
        }
        let plan = GesNetworkEndPlan(client: client, outgoing: outgoing, incoming: incoming)
        var names: [(name: String, origin: String)] = plan.fixed.map { ($0, "the event " + $0) }
        for (route, name) in incoming { names.append((name, "the event of the route '\(route.side) \(route.verb) \(route.written)'")) }
        try GesSchemaPlanner.unique("\(network.name).\(plan.name)", names)
        return plan
    }

    // The types that the emitters nest in a network and its typed ends hide every top-level type of their name for the code
    // inside, which names roots, tag sets and the network itself: Swift without qualification, C# through the namespace, whose
    // network class must not contain a member of its own name. Both ports and languages reject such a hiding alike.
    static func checkNested(_ network: GesLayoutNetwork, _ topLevel: Set<String>, swift: Bool) throws(GesGeneratorError) {
        var names: [(name: String, origin: String)] = []
        for client in [true, false] {
            let end = try plan(network, client: client)
            let side = client ? "client" : "server"
            names.append((end.name, "\(GesSchemaPlanner.nestedTypeOrigin)of the \(side) end"))
            names.append((swift ? end.name + "Event" : "I\(end.name)Handler", "\(GesSchemaPlanner.nestedTypeOrigin)of the \(side) \(swift ? "events" : "handler")"))
            guard end.hasTopics else { continue }
            try GesSchemaPlanner.unique(
                "\(network.name).\(end.name)",
                [("Topic", "\(GesSchemaPlanner.nestedTypeOrigin)of the \(side) topics"), ("Tick", "\(GesSchemaPlanner.nestedTypeOrigin)of the \(side) ticks")],
                topLevel
            )
        }
        // The members of the network type, in each language's spelling, must not repeat one another.
        names.append((swift ? "hash" : "Hash", "the network hash constant"))
        names.append((swift ? "bound" : "Bound", "the bound network property"))
        for route in network.routes {
            let label = "the route \(route.side) \(route.verb) \(route.written)"
            let constant = swift ? GesSchemaPlanner.camel(route.name) : route.name
            names.append((constant, "the index constant of \(label)"))
            if route.rateMicroseconds != 0 { names.append((constant + "EveryMicroseconds", "the rate constant of \(label)")) }
            names.append(("\(swift ? route.side : GesSchemaPlanner.pascal(route.side))\(route.verb == "publishes" ? "Publish" : "Send")\(route.root)", "the function of \(label)"))
        }
        if !swift {
            // C# forbids a member named like its enclosing type, so no member of the network class may take the network's name.
            for (name, origin) in names where name == network.name {
                throw GesGeneratorError(GesGeneratorError.nameCollision, "In '\(network.name)', \(origin) is generated as '\(name)', the name of the network class that holds it.")
            }
        }
        try GesSchemaPlanner.unique(network.name, names, topLevel)
    }
}
