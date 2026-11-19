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
        for (route, name) in incoming { names.append((name, "the event of the route '\(route.side) \(route.verb) :\(route.root)'")) }
        try GesSchemaPlanner.unique("\(network.name).\(plan.name)", names)
        return plan
    }
}
