// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

#if !os(WASI)

    import GameEventScriptNetwork
    import GameEventScriptRuntime
    import NIOCore

    /// Opens the TCP, WebSocket or UDP transport of a client end. Opening runs in the background on SwiftNIO's event
    /// loops: the end's first poll starts it, packets wait for it, and a failure to open ends the transport, so the
    /// connection closes with `network.transportClosed`. One connector serves one client end.
    public final class GesNetworkSocketConnector: GesNetworkConnector {
        // Opens the transport into the inbox for a connection with the packet limit.
        private typealias Open = @Sendable (GesNetworkSocketInbox, Int) async throws -> (Channel, GesNetworkDatagramPeer?)

        /// The transport the connector opens.
        public let transport: GesNetworkTransport

        // Wakes the end that waits for the connector's session.
        let signal = GesNetworkSocketSignal()
        private let open: Open
        private let framing: GesNetworkSocketSession.Framing

        private init(_ transport: GesNetworkTransport, _ framing: GesNetworkSocketSession.Framing, _ open: @escaping Open) {
            self.transport = transport
            self.framing = framing
            self.open = open
        }

        /// Creates a connector that opens a TCP connection for the stream transport.
        /// - Parameters:
        ///   - host: The server's host name or address.
        ///   - port: The server's port.
        /// - Returns: The connector.
        /// - Throws: `GesApiError.invalidArgument` when the port is not 1 to 65535.
        public static func tcp(host: String, port: Int) throws -> GesNetworkSocketConnector {
            try check(port)
            return GesNetworkSocketConnector(.stream, .stream) { inbox, _ in
                (try await GesNetworkSocketSession.openTcp(host: host, port: port, inbox: inbox), nil)
            }
        }

        /// Creates a connector that opens a WebSocket for the message transport.
        /// - Parameter url: The `ws` or `wss` endpoint, such as `wss://example.org/game?room=1`; IPv6 hosts stand in brackets.
        /// - Returns: The connector.
        /// - Throws: `GesApiError.invalidArgument` when the URL is no `ws` or `wss` endpoint with a host.
        public static func webSocket(_ url: String) throws -> GesNetworkSocketConnector {
            let endpoint = try GesNetworkSocketEndpoint(url)
            return GesNetworkSocketConnector(.message, .webSocket) { inbox, limit in
                (try await GesNetworkSocketSession.openWebSocket(endpoint: endpoint, limit: limit, inbox: inbox), nil)
            }
        }

        /// Creates a connector that opens a UDP socket for the datagram transport. UDP has no connection setup, so a
        /// missing server shows only as the connection's timeout.
        /// - Parameters:
        ///   - host: The server's host name or address; the first address it resolves to is used.
        ///   - port: The server's port.
        /// - Returns: The connector.
        /// - Throws: `GesApiError.invalidArgument` when the port is not 1 to 65535.
        public static func udp(host: String, port: Int) throws -> GesNetworkSocketConnector {
            try check(port)
            return GesNetworkSocketConnector(.datagram, .datagram) { inbox, limit in
                let (channel, peer) = try await GesNetworkSocketSession.openUdp(host: host, port: port, limit: limit, inbox: inbox)
                return (channel, peer)
            }
        }

        /// Starts opening the transport of the client end's connection without blocking.
        /// - Parameter connection: The client connection, of this connector's transport.
        /// - Returns: The session, a `GesNetworkSocketSession`.
        /// - Throws: `GesApiError.invalidArgument` when the connection is no client of this transport or uses insecure
        ///   test keys.
        public func connect(_ connection: GesNetworkConnection) throws -> any GesNetworkLink {
            try GesNetworkSocketSession.require(connection, transport, client: true)
            let inbox = GesNetworkSocketInbox(signal: signal)
            let opening = GesNetworkSocketOpening()
            let session = GesNetworkSocketSession(connection: connection, inbox: inbox, framing: framing, opening: opening)
            let open = open
            let signal = signal
            let limit = connection.maxPacketBytes
            Task.detached {
                do {
                    let (channel, peer) = try await open(inbox, limit)
                    if opening.complete(channel, peer) {
                        signal.set()
                    } else {
                        GesNetworkSocketSession.release(channel, peer)
                    }
                } catch {
                    // Any failure to open, whatever its kind, is the end of the transport.
                    inbox.end()
                }
            }
            return session
        }

        private static func check(_ port: Int) throws {
            if port < 1 || port > 65535 { throw GesApiError.invalidArgument("The port is 1 to 65535.") }
        }
    }

#endif
