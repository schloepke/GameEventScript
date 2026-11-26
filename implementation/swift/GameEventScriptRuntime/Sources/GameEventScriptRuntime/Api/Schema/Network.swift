// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

@_spi(Syntax) import GameEventScriptSyntax

private typealias Code = GesSchemaDiagnostic

/// The side a network route starts from.
public enum GesNetworkSide: Sendable, Equatable {
    /// The route goes from the client to the server.
    case client
    /// The route goes from the server to the client.
    case server
}

/// How a network route carries its root.
public enum GesNetworkVerb: Sendable, Equatable {
    /// The route sends single values of a schema, a message or a message set.
    case sends
    /// The route publishes the states of a schema, a topic.
    case publishes
}

/// The delivery class of a network route.
public enum GesNetworkClass: Sendable, Equatable {
    /// Every message arrives once, in order.
    case reliable
    /// Only the newest message counts; an older one may be dropped.
    case latest
    /// Reliable, in order, and sent only when no other message waits.
    case bulk
}

/// One route of a network. Immutable.
public final class GesNetworkRoute: @unchecked Sendable {
    /// The route's position in its network, counting from 0.
    public let index: Int
    /// The side the route starts from.
    public let side: GesNetworkSide
    /// Whether the route sends or publishes.
    public let verb: GesNetworkVerb
    /// The schema, message or message set the route carries.
    public let root: GesSchema
    /// The delivery class.
    public let deliveryClass: GesNetworkClass
    /// The shortest interval between two updates of a topic in microseconds, rounded up; 0 without a rate.
    public let rateMicroseconds: Int64

    init(index: Int, side: GesNetworkSide, verb: GesNetworkVerb, root: GesSchema, deliveryClass: GesNetworkClass, rateMicroseconds: Int64) {
        self.index = index
        self.side = side
        self.verb = verb
        self.root = root
        self.deliveryClass = deliveryClass
        self.rateMicroseconds = rateMicroseconds
    }

    /// The route's channel on the wire, its index plus 1.
    public var channel: Int { index + 1 }
}

/// An `owns` route of a network: the side that owns the objects of a handle type. Immutable.
public final class GesNetworkOwner: @unchecked Sendable {
    /// The side that owns the objects and can resolve their handles.
    public let side: GesNetworkSide
    /// The owned handle type, which is never composite.
    public let handle: GesHandleType

    init(side: GesNetworkSide, handle: GesHandleType) {
        self.side = side
        self.handle = handle
    }
}

/// A network declaration: the routes of one client–server pair. Immutable; it can be bound any number of times.
public final class GesNetwork: @unchecked Sendable {
    /// The declared name without the leading `:`.
    public let name: String
    /// The routes in declared order; a route's position is its index.
    public let routes: [GesNetworkRoute]
    /// The `owns` routes in ascending scalar order of the handle type's name; they have no route index.
    public let owners: [GesNetworkOwner]

    init(name: String, routes: [GesNetworkRoute], owners: [GesNetworkOwner]) {
        self.name = name
        self.routes = routes
        self.owners = owners
    }

    /// Binds every route root with the same parameter values.
    /// - Parameter parameters: The values of the parameters the roots reach, if they reach any.
    /// - Returns: The immutable bound network.
    /// - Throws: `GesSchemaError` when a root cannot be bound, as for `GesSchema.bind(parameters:)`.
    public func bind(parameters: GesSchemaParameters = GesSchemaParameters()) throws -> GesBoundNetwork {
        GesBoundNetwork(network: self, roots: try routes.map { try $0.root.bind(parameters: parameters) })
    }
}

/// A network bound to parameter values. Immutable; it can be shared between threads and connections.
public final class GesBoundNetwork: @unchecked Sendable {
    /// The declaration this binding comes from.
    public let network: GesNetwork
    /// The bound root of each route, by route index.
    public let roots: [GesBoundSchema]
    /// The canonical form the network hash runs over.
    public let canonicalForm: String
    /// The network hash: FNV-1a 64 over the canonical form, which the handshake compares.
    public let hash: UInt64

    init(network: GesNetwork, roots: [GesBoundSchema]) {
        self.network = network
        self.roots = roots
        var text = "gesn1\nnetwork \(network.name)\n"
        for (index, route) in network.routes.enumerated() {
            text += "route \(route.side == .client ? "client" : "server") \(route.verb == .sends ? "sends" : "publishes") \(route.root.name) "
            text += "\(Self.className(route.deliveryClass)) \(route.rateMicroseconds) \(GesSchemaCanonical.hex(roots[index].bindingHash))\n"
        }
        for owner in network.owners {
            text += "owns \(owner.side == .client ? "client" : "server") \(owner.handle.name) \(GesSchemaCanonical.hex(owner.handle.hash)) "
            text += "\(GesSchemaCanonical.hex(owner.handle.resolved.state.schema!.hash))\n"
        }
        canonicalForm = text
        hash = GesSchemaCanonical.hash(text)
    }

    /// The source spelling of a delivery class: `reliable`, `latest` or `bulk`.
    @_spi(Generator) public static func className(_ deliveryClass: GesNetworkClass) -> String {
        switch deliveryClass {
        case .reliable: "reliable"
        case .latest: "latest"
        case .bulk: "bulk"
        }
    }
}

extension GesFileScope {
    // The largest rate, 3600 seconds, in microseconds.
    private static let maximumRateMicroseconds: Int64 = 3_600_000_000

    /// Resolves the networks of this source once every declaration has its public description.
    func resolveNetworks(_ all: [String: GesResolvedDeclaration], _ messages: [String: GesResolvedDeclaration], _ networks: inout [GesNetwork]) throws {
        for syntax in networkSyntaxes {
            var routes: [GesNetworkRoute] = []
            var owners: [GesNetworkOwner] = []
            var seen: Set<String> = []
            for route in syntax.routes {
                if route.owns {
                    let handle = try handle(route.root, route.rootOffset)
                    if handle.root != nil { throw error(Code.invalidHandle, route.rootOffset, "'@\(route.root)' is composite; its root's owner owns it.") }
                    if owners.contains(where: { $0.handle.name == route.root }) {
                        throw error(Code.duplicateRoute, route.offset, "The network names '@\(route.root)' in two owns routes.")
                    }
                    owners.append(GesNetworkOwner(side: route.server ? .server : .client, handle: handle.publicType!))
                    continue
                }
                let index = routes.count
                // A route names a message or message set without a colon and a schema with one.
                guard let root = (route.isMessage ? messages : all)[route.root] else {
                    let message =
                        route.isMessage
                        ? "Message or message set '\(route.root)' is not declared."
                        : messages[route.root]?.kind == .messages
                            ? "':\(route.root)' is no declared schema; a message set is named without ':', as '\(route.root)'." : "':\(route.root)' is no declared schema."
                    throw error(Code.unknownName, route.rootOffset, message)
                }
                if root.syntax.wireGroup == nil { throw error(Code.invalidRoute, route.rootOffset, "'\(route.root)' has no wire role.") }
                if root.isAbstract { throw error(Code.invalidRoute, route.rootOffset, "'\(route.root)' is abstract.") }
                if route.publishes && root.kind != .schema {
                    throw error(Code.invalidRoute, route.rootOffset, "'\(route.root)' is no schema; only a schema is published.")
                }
                let deliveryClass: GesNetworkClass
                switch route.routeClass {
                case .reliable: deliveryClass = .reliable
                case .latest: deliveryClass = .latest
                case .bulk: deliveryClass = .bulk
                }
                if route.publishes ? deliveryClass == .bulk : deliveryClass == .latest {
                    throw error(Code.invalidRoute, route.classOffset, route.publishes ? "A topic is published latest or reliable." : "A route sends reliable or bulk.")
                }
                var rate: Int64 = 0
                if let value = route.rate {
                    if !route.publishes { throw error(Code.invalidRoute, value.offset, "Only a published topic has a rate.") }
                    rate = try rateMicroseconds(value)
                }
                let side = route.server ? "server" : "client"
                let verb = route.publishes ? "publishes" : "sends"
                if !seen.insert("\(side) \(verb) \(route.isMessage ? "" : ":")\(route.root)").inserted {
                    throw error(Code.duplicateRoute, route.offset, "The \(side) \(verb) '\(route.root)' twice.")
                }
                routes.append(
                    GesNetworkRoute(
                        index: index,
                        side: route.server ? .server : .client,
                        verb: route.publishes ? .publishes : .sends,
                        root: root.schema!,
                        deliveryClass: deliveryClass,
                        rateMicroseconds: rate
                    )
                )
            }
            owners.sort { Array($0.handle.name.utf8).lexicographicallyPrecedes(Array($1.handle.name.utf8)) }
            networks.append(GesNetwork(name: syntax.name, routes: routes, owners: owners))
        }
    }

    /// A rate in microseconds, rounded up: a time quantity greater than zero and at most 3600 seconds.
    private func rateMicroseconds(_ value: GesSchemaValueSyntax) throws -> Int64 {
        var literal = value
        if !value.constant.isEmpty {
            guard let constant = constants[value.constant] else { throw error(Code.unknownName, value.offset, "Constant '$\(value.constant)' is not declared in this file.") }
            literal = constant
        }
        if literal.unit != .second || literal.isPercent { throw error(Code.invalidRoute, value.offset, "A rate is a time quantity.") }
        let significand = literal.value.significand
        if significand <= 0 { throw error(Code.invalidRoute, value.offset, "A rate is greater than zero.") }
        // The literal is significand × 10^exponent seconds, so significand × 10^(exponent + 6) microseconds.
        let shift = literal.value.exponent + 6
        var micros = significand
        if shift >= 0 {
            var step = 0
            while step < shift && micros <= Self.maximumRateMicroseconds {
                micros *= 10
                step += 1
            }
        } else {
            var remainder = false
            var step = 0
            while step < -shift && micros != 0 {
                remainder = remainder || micros % 10 != 0
                micros /= 10
                step += 1
            }
            if remainder { micros += 1 }
        }
        if micros > Self.maximumRateMicroseconds { throw error(Code.invalidRoute, value.offset, "A rate is at most 3600s.") }
        return micros
    }
}
