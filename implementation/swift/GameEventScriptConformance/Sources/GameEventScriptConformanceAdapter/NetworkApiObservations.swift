// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import GameEventScriptNetwork
import GameEventScriptRuntime

// Runs scripted steps over a client and a server connection and records their events and packets as trace lines.
enum NetworkApiObservations {
    private static let steps: Set<String> = ["advance", "accept", "refuse", "close", "send", "publish", "subscribe", "unsubscribe", "receive", "transmit", "transportClosed", "budget", "deliver", "cookie", "cancel", "beginTick", "include", "endTick"]

    static func validate(_ input: Input) throws {
        try Validation.fields(input, ["sources", "clientSources", "network", "options", "clientOptions", "token", "link", "steps"])
        _ = try options(input.required("options"))
        _ = try options(input.required("clientOptions"))
        _ = try bytes(input.text("token"))
        for step in input.values("steps") {
            guard steps.contains(try step.text("action")) else { throw ConformanceExecutionError.invalidInput("Unknown network step") }
        }
    }

    static func run(_ input: Input) throws -> [String: Any] {
        let client: GesNetworkConnection
        let server: GesNetworkConnection
        do {
            let name = try input.text("network")
            let serverNetwork = try bind(input.values("sources"), name)
            let clientNetwork = try bind(input.values("clientSources"), name)
            client = try GesNetworkConnection.makeClient(network: clientNetwork, token: bytes(input.text("token")), now: 0, options: options(input.required("clientOptions")))
            server = try GesNetworkConnection.makeServer(network: serverNetwork, now: 0, options: options(input.required("options")))
        } catch let error as GesSchemaError {
            return ["status": "ok", "network": ["error": ["code": error.code]]]
        } catch GesApiError.invalidArgument {
            return ["status": "ok", "network": ["error": ["code": "invalidArgument"]]]
        }

        let link = try input.required("link")
        let simulator: GesNetworkSimulator?
        if link.objectValue != nil {
            do {
                simulator = try makeSimulator(client, server, link)
            } catch GesApiError.invalidArgument {
                return ["status": "ok", "network": ["error": ["code": "invalidArgument"]]]
            }
        } else if link.stringValue == "loopback" {
            simulator = try GesNetworkSimulator(client: client, server: server, seed: 0, latencyMicroseconds: 0, jitterMicroseconds: 0)
        } else {
            simulator = nil
        }

        var trace: [String] = []
        var clientSent: [[UInt8]] = []
        var serverSent: [[UInt8]] = []
        var now: Int64 = 0
        for step in input.values("steps") {
            let action = try step.text("action")
            let connection = step["side"]?.stringValue == "client" ? client : server
            let name = action == "cookie" ? "cookie" : connection === client ? "client" : "server"
            do {
                switch action {
                case "advance":
                    now = try integer(step.required("now"))
                    if let simulator {
                        try simulator.advance(now)
                    } else {
                        try client.advance(now)
                        try server.advance(now)
                    }
                case "accept": try server.accept()
                case "refuse": try server.refuse(step.text("detail"))
                case "close": connection.close(try step.text("detail"))
                case "send", "publish":
                    // count repeats the call; each run of equal results is traced once as "status xN".
                    let route = Int(try integer(step.required("route")))
                    let bytes = try payload(step.required("payload"))
                    let call = { action == "send" ? connection.send(route: route, payload: bytes) : connection.publish(route: route, payload: bytes) }
                    guard let count = step["count"] else {
                        trace.append("\(name) \(action) \(call())")
                        break
                    }
                    var last = GesNetworkSendStatus.queued
                    var run = 0
                    for _ in 0..<Int(try integer(count)) {
                        let status = call()
                        if run > 0 && status != last {
                            trace.append("\(name) \(action) \(last) x\(run)")
                            run = 0
                        }
                        last = status
                        run += 1
                    }
                    trace.append("\(name) \(action) \(last) x\(run)")
                case "cancel":
                    let cancelled = try connection.cancel(route: Int(try integer(step.required("route"))), transferId: Int(try integer(step.required("id"))), code: step["code"]?.stringValue)
                    trace.append("\(name) cancel \(cancelled)")
                case "subscribe": try connection.subscribe(route: Int(try integer(step.required("route"))))
                case "unsubscribe": try connection.unsubscribe(route: Int(try integer(step.required("route"))))
                case "beginTick": try connection.beginTick()
                case "include": try connection.include(route: Int(try integer(step.required("route"))))
                case "endTick": trace.append("\(name) endTick \(try connection.endTick())")
                case "receive": try connection.receive(bytes(step.text("bytes")), now: now)
                case "transmit":
                    var buffer = [UInt8](repeating: 0, count: connection.maxPacketBytes + 5)
                    if connection === client { clientSent.removeAll() } else { serverSent.removeAll() }
                    while true {
                        let length = try connection.transmit(into: &buffer)
                        if length == 0 { break }
                        let sent = Array(buffer[0..<length])
                        if connection === client { clientSent.append(sent) } else { serverSent.append(sent) }
                        trace.append("\(name) packet \(packet(sent))")
                    }
                case "deliver":
                    let from = connection === client ? serverSent : clientSent
                    for index in step.values("packets") {
                        let position = Int(try integer(index))
                        guard position < from.count else { throw GesApiError.invalidArgument("No such packet.") }
                        try connection.receive(from[position], now: now)
                    }
                case "cookie": trace.append("cookie \(try cookie(step, clientSent))")
                case "transportClosed": connection.transportClosed()
                default: try connection.setSendBudgetBytesPerSecond(integer(step.required("bytesPerSecond")))
                }
            } catch GesApiError.invalidOperation {
                trace.append("\(name) error invalidOperation")
            } catch GesApiError.invalidArgument {
                trace.append("\(name) error invalidArgument")
            }
            drain(client, "client", &trace)
            drain(server, "server", &trace)
        }

        return ["status": "ok", "network": ["trace": trace, "client": describe(client), "server": describe(server)]]
    }

    private static func makeSimulator(_ client: GesNetworkConnection, _ server: GesNetworkConnection, _ link: Input) throws -> GesNetworkSimulator {
        try GesNetworkSimulator(
            client: client,
            server: server,
            seed: integer(link.required("seed")),
            latencyMicroseconds: integer(link.required("latencyMicroseconds")),
            jitterMicroseconds: integer(link.required("jitterMicroseconds")),
            lossPerMille: Int(try link["lossPerMille"].map(integer) ?? 0),
            duplicatePerMille: Int(try link["duplicatePerMille"].map(integer) ?? 0),
            reorder: link["reorder"]?.boolValue ?? false
        )
    }

    // Checks a datagram at a fresh gate and names the result, with the retry's bytes.
    private static func cookie(_ step: Input, _ sent: [[UInt8]]) throws -> String {
        let packet: [UInt8]
        if let hex = step["bytes"]?.stringValue {
            packet = try bytes(hex)
        } else {
            let position = Int(try integer(step.required("packet")))
            guard position < sent.count else { throw GesApiError.invalidArgument("No such packet.") }
            packet = sent[position]
        }
        let cookies = try GesNetworkCookies(key: bytes(step.text("key")), windowMicroseconds: integer(step.required("windowMicroseconds")))
        var retry = [UInt8](repeating: 0, count: GesNetworkCookies.retryBytes)
        switch try cookies.check(packet: packet, address: bytes(step.text("address")), now: integer(step.required("now")), retry: &retry) {
        case .accept: return "accept"
        case .retry: return "retry \(hexadecimal(retry))"
        case .drop: return "drop"
        }
    }

    // A packet up to 512 bytes as hexadecimal, a longer one as its length and FNV-1a 64 hash.
    private static func packet(_ bytes: [UInt8]) -> String {
        bytes.count <= 512 ? hexadecimal(bytes) : bytes.withUnsafeBytes { text($0) }
    }

    private static func bind(_ sources: [Input], _ network: String) throws -> GesBoundNetwork {
        let parsed = sources.enumerated().map { GesSchemaSource(name: "schema\($0.offset).gess", text: $0.element.stringValue ?? "") }
        return try GesSchemaSet(sources: parsed).network(network).bind()
    }

    private static func options(_ raw: Input) throws -> GesNetworkOptions {
        var options = GesNetworkOptions()
        for entry in raw.objectValue ?? [] {
            switch entry.key {
            case "transport":
                switch entry.value.stringValue {
                case "stream": options.transport = .stream
                case "datagram": options.transport = .datagram
                default: options.transport = .message
                }
            case "maxPacketBytes": options.maxPacketBytes = Int(try integer(entry.value))
            case "maxBaselineBytes": options.maxBaselineBytes = try integer(entry.value)
            case "maxPendingReceiveBytes": options.maxPendingReceiveBytes = try integer(entry.value)
            case "maxFramesPerSecond": options.maxFramesPerSecond = Int(try integer(entry.value))
            case "maxSendQueueBytes": options.maxSendQueueBytes = try integer(entry.value)
            case "timeoutMicroseconds": options.timeoutMicroseconds = try integer(entry.value)
            case "keepaliveMicroseconds": options.keepaliveMicroseconds = try integer(entry.value)
            case "ackDelayMicroseconds": options.ackDelayMicroseconds = try integer(entry.value)
            case "sendBudgetBytesPerSecond": options.sendBudgetBytesPerSecond = try integer(entry.value)
            case "maxTransferBytes": options.maxTransferBytes = try integer(entry.value)
            case "deflateTransfers":
                guard let value = entry.value.boolValue else { throw ConformanceExecutionError.invalidInput("Expected a Boolean") }
                options.deflateTransfers = value
            case "clientSecurity":
                switch entry.value.stringValue {
                case "development": options.clientSecurity = .development
                case "pinned": options.clientSecurity = try .pinned(try bytes(raw["serverPublicKey"]?.stringValue ?? ""))
                default: options.clientSecurity = .unencrypted
                }
            case "serverSecurity":
                switch entry.value.stringValue {
                case "key": options.serverSecurity = .key(try GesNetworkKey(privateKey: try bytes(raw["key"]?.stringValue ?? "")))
                default: options.serverSecurity = .unencrypted
                }
            case "key", "serverPublicKey": break
            case "testKeySeed":
                guard let text = entry.value.numberValue, let value = UInt64(text) else { throw ConformanceExecutionError.invalidInput("Expected an unsigned integer") }
                options.insecureTestKeySeed = value
            default: throw ConformanceExecutionError.invalidInput("Unknown network option")
            }
        }
        return options
    }

    private static func integer(_ raw: Input) throws -> Int64 {
        guard let text = raw.numberValue, let value = Int64(text) else { throw ConformanceExecutionError.invalidInput("Expected an integer") }
        return value
    }

    private static func payload(_ raw: Input) throws -> [UInt8] {
        if let hex = raw["hex"]?.stringValue { return try bytes(hex) }
        let length = Int(try integer(raw.required("length")))
        let seed = Int(try integer(raw.required("seed")))
        return (0..<length).map { UInt8(($0 + seed) % 251) }
    }

    private static func drain(_ connection: GesNetworkConnection, _ name: String, _ trace: inout [String]) {
        while true {
            let next = connection.nextEvent()
            switch next.kind {
            case .none: return
            case .connecting: trace.append("\(name) connecting \(text(next.payload))")
            case .connected: trace.append("\(name) connected")
            case .message: trace.append("\(name) message \(next.route) \(text(next.payload))")
            case .closed: trace.append("\(name) closed \(next.code) \(next.byPeer ? "peer" : "local") \"\(next.detail)\"")
            case .transferProgress: trace.append("\(name) progress \(next.route) \(next.transferId) \(next.bytes)/\(next.total)")
            case .transferCancelled: trace.append("\(name) cancelled \(next.route) \(next.transferId) \(next.code) \(next.byPeer ? "peer" : "local")")
            case .tick: trace.append("\(name) tick \(next.tickNumber)")
            }
        }
    }

    // Up to 32 bytes as hexadecimal, longer payloads as their length and FNV-1a 64 hash.
    private static func text(_ bytes: UnsafeRawBufferPointer) -> String {
        if bytes.isEmpty { return "-" }
        if bytes.count <= 32 { return hexadecimal(bytes) }
        var hash: UInt64 = 0xcbf2_9ce4_8422_2325
        for value in bytes { hash = (hash ^ UInt64(value)) &* 0x100_0000_01b3 }
        let digits = String(hash, radix: 16)
        return "\(bytes.count):\(String(repeating: "0", count: 16 - digits.count))\(digits)"
    }

    private static func hexadecimal<Bytes: Sequence>(_ bytes: Bytes) -> String where Bytes.Element == UInt8 {
        bytes.map { $0 < 16 ? "0" + String($0, radix: 16) : String($0, radix: 16) }.joined()
    }

    private static func bytes(_ hex: String) throws -> [UInt8] {
        let digits = Array(hex.utf8)
        guard digits.count % 2 == 0 else { throw ConformanceExecutionError.invalidInput("Odd hexadecimal length") }
        return try stride(from: 0, to: digits.count, by: 2).map {
            guard let value = UInt8(String(decoding: digits[$0..<($0 + 2)], as: UTF8.self), radix: 16) else { throw ConformanceExecutionError.invalidInput("Invalid hexadecimal") }
            return value
        }
    }

    private static func describe(_ connection: GesNetworkConnection) -> [String: Any] {
        let state: String
        switch connection.state {
        case .hello: state = "hello"
        case .awaitHello: state = "awaitHello"
        case .awaitAccept: state = "awaitAccept"
        case .connected: state = "connected"
        case .closed: state = "closed"
        }
        let security: String
        switch connection.security {
        case .verified: security = "verified"
        case .encrypted: security = "encrypted"
        case .none: security = "none"
        }
        return [
            "state": state, "security": security, "roundTripMicroseconds": connection.roundTripMicroseconds, "queuedSendBytes": connection.queuedSendBytes,
            "pendingReceiveBytes": connection.pendingReceiveBytes,
        ]
    }
}
