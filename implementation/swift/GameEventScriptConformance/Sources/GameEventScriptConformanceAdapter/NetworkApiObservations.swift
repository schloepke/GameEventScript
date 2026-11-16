// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import GameEventScriptNetwork
import GameEventScriptRuntime

// Runs scripted steps over a client and a server connection and records their events and packets as trace lines.
enum NetworkApiObservations {
    private static let steps: Set<String> = ["advance", "accept", "refuse", "close", "send", "receive", "transmit", "transportClosed", "budget"]

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
            simulator = try GesNetworkSimulator(
                client: client,
                server: server,
                seed: Int64(link.required("seed").numberValue ?? "") ?? 0,
                latencyMicroseconds: Int64(link.required("latencyMicroseconds").numberValue ?? "") ?? 0,
                jitterMicroseconds: Int64(link.required("jitterMicroseconds").numberValue ?? "") ?? 0
            )
        } else if link.stringValue == "loopback" {
            simulator = try GesNetworkSimulator(client: client, server: server, seed: 0, latencyMicroseconds: 0, jitterMicroseconds: 0)
        } else {
            simulator = nil
        }

        var trace: [String] = []
        var now: Int64 = 0
        for step in input.values("steps") {
            let action = try step.text("action")
            let connection = step["side"]?.stringValue == "client" ? client : server
            let name = connection === client ? "client" : "server"
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
                case "send":
                    let status = connection.send(route: Int(try integer(step.required("route"))), payload: try payload(step.required("payload")))
                    trace.append("\(name) send \(status)")
                case "receive": try connection.receive(bytes(step.text("bytes")), now: now)
                case "transmit":
                    var buffer = [UInt8](repeating: 0, count: connection.maxPacketBytes + 5)
                    while true {
                        let length = try connection.transmit(into: &buffer)
                        if length == 0 { break }
                        trace.append("\(name) packet \(hexadecimal(buffer[0..<length]))")
                    }
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

    private static func bind(_ sources: [Input], _ network: String) throws -> GesBoundNetwork {
        let parsed = sources.enumerated().map { GesSchemaSource(name: "schema\($0.offset).gess", text: $0.element.stringValue ?? "") }
        return try GesSchemaSet(sources: parsed).network(network).bind()
    }

    private static func options(_ raw: Input) throws -> GesNetworkOptions {
        var options = GesNetworkOptions()
        for entry in raw.objectValue ?? [] {
            switch entry.key {
            case "transport": options.transport = entry.value.stringValue == "stream" ? .stream : .message
            case "maxPacketBytes": options.maxPacketBytes = Int(try integer(entry.value))
            case "maxPendingReceiveBytes": options.maxPendingReceiveBytes = try integer(entry.value)
            case "maxFramesPerSecond": options.maxFramesPerSecond = Int(try integer(entry.value))
            case "maxSendQueueBytes": options.maxSendQueueBytes = try integer(entry.value)
            case "timeoutMicroseconds": options.timeoutMicroseconds = try integer(entry.value)
            case "keepaliveMicroseconds": options.keepaliveMicroseconds = try integer(entry.value)
            case "ackDelayMicroseconds": options.ackDelayMicroseconds = try integer(entry.value)
            case "sendBudgetBytesPerSecond": options.sendBudgetBytesPerSecond = try integer(entry.value)
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
        return [
            "state": state, "roundTripMicroseconds": connection.roundTripMicroseconds, "queuedSendBytes": connection.queuedSendBytes,
            "pendingReceiveBytes": connection.pendingReceiveBytes,
        ]
    }
}
