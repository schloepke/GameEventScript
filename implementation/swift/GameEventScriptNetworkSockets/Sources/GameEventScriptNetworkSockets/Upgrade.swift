// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import GameEventScriptRuntime
import NIOCore
import NIOWebSocket

// The HTTP upgrade of RFC 6455 on both sides, without Foundation.
enum GesNetworkSocketUpgrade {
    static let maxRequestBytes = 8192
    static let timeout = TimeAmount.seconds(10)
    static let guid = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11"

    // The answer of a server listening on `path` to the request head, which ends with an empty line.
    static func answer(_ request: String, path: String) -> (text: String, upgrade: Bool) {
        let lines = request.split(separator: "\r\n", omittingEmptySubsequences: false)
        let start = lines[0].split(separator: " ", omittingEmptySubsequences: false)
        if start.count != 3 || start[0] != "GET" || !start[2].hasPrefix("HTTP/1.") { return status(400, "Bad Request") }
        let target = start[1]
        let resource = target.firstIndex(of: "?").map { target[..<$0] } ?? target
        if resource != path { return status(404, "Not Found") }
        var upgrade: Substring?
        var connection: Substring?
        var version: Substring?
        var key: Substring?
        for line in lines.dropFirst() {
            guard let colon = line.firstIndex(of: ":"), colon != line.startIndex else { continue }
            let name = trim(line[..<colon])
            let value = trim(line[line.index(after: colon)...])
            if equalIgnoringCase(name, "Upgrade") {
                upgrade = value
            } else if equalIgnoringCase(name, "Connection") {
                connection = value
            } else if equalIgnoringCase(name, "Sec-WebSocket-Version") {
                version = value
            } else if equalIgnoringCase(name, "Sec-WebSocket-Key") {
                key = value
            }
        }

        guard let upgrade, equalIgnoringCase(upgrade, "websocket"), let connection,
            connection.split(separator: ",", omittingEmptySubsequences: false).contains(where: { equalIgnoringCase(trim($0), "Upgrade") }), let key
        else { return status(400, "Bad Request") }
        if version != "13" { return ("HTTP/1.1 426 Upgrade Required\r\nSec-WebSocket-Version: 13\r\nContent-Length: 0\r\nConnection: close\r\n\r\n", false) }
        return ("HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Accept: \(accept(key))\r\n\r\n", true)
    }

    static func status(_ code: Int, _ reason: String) -> (text: String, upgrade: Bool) {
        ("HTTP/1.1 \(code) \(reason)\r\nContent-Length: 0\r\nConnection: close\r\n\r\n", false)
    }

    // The Sec-WebSocket-Accept value of a key.
    static func accept<Key: StringProtocol>(_ key: Key) -> String { base64(sha1(Array((String(key) + guid).utf8))) }

    // The length of the request or response head including its empty line, or nil while it is incomplete.
    static func headLength(_ buffer: ByteBuffer) -> Int? {
        buffer.withUnsafeReadableBytes { bytes in
            if bytes.count < 4 { return nil }
            for end in 4...bytes.count where bytes[end - 4] == 13 && bytes[end - 3] == 10 && bytes[end - 2] == 13 && bytes[end - 1] == 10 { return end }
            return nil
        }
    }

    // Trims the characters C#'s string.Trim removes from an ASCII header: spaces and tabs.
    static func trim(_ text: Substring) -> Substring {
        var text = text
        while let first = text.first, first == " " || first == "\t" { text.removeFirst() }
        while let last = text.last, last == " " || last == "\t" { text.removeLast() }
        return text
    }

    static func equalIgnoringCase(_ text: Substring, _ other: String) -> Bool {
        let left = text.utf8
        let right = other.utf8
        return left.count == right.count && zip(left, right).allSatisfy { lower($0) == lower($1) }
    }

    private static func lower(_ byte: UInt8) -> UInt8 { byte >= 65 && byte <= 90 ? byte + 32 : byte }

    static func sha1(_ message: [UInt8]) -> [UInt8] {
        var h: [UInt32] = [0x6745_2301, 0xEFCD_AB89, 0x98BA_DCFE, 0x1032_5476, 0xC3D2_E1F0]
        var data = message
        data.append(0x80)
        while data.count % 64 != 56 { data.append(0) }
        let bits = UInt64(message.count) * 8
        for shift in stride(from: 56, through: 0, by: -8) { data.append(UInt8(truncatingIfNeeded: bits >> UInt64(shift))) }
        var w = [UInt32](repeating: 0, count: 80)
        for block in stride(from: 0, to: data.count, by: 64) {
            for index in 0..<16 {
                let offset = block + index * 4
                w[index] = UInt32(data[offset]) << 24 | UInt32(data[offset + 1]) << 16 | UInt32(data[offset + 2]) << 8 | UInt32(data[offset + 3])
            }
            for index in 16..<80 {
                let value = w[index - 3] ^ w[index - 8] ^ w[index - 14] ^ w[index - 16]
                w[index] = value << 1 | value >> 31
            }
            var a = h[0]
            var b = h[1]
            var c = h[2]
            var d = h[3]
            var e = h[4]
            for index in 0..<80 {
                let f: UInt32
                let k: UInt32
                switch index {
                case 0..<20:
                    f = (b & c) | (~b & d)
                    k = 0x5A82_7999
                case 20..<40:
                    f = b ^ c ^ d
                    k = 0x6ED9_EBA1
                case 40..<60:
                    f = (b & c) | (b & d) | (c & d)
                    k = 0x8F1B_BCDC
                default:
                    f = b ^ c ^ d
                    k = 0xCA62_C1D6
                }
                let temp = (a << 5 | a >> 27) &+ f &+ e &+ k &+ w[index]
                e = d
                d = c
                c = b << 30 | b >> 2
                b = a
                a = temp
            }
            h[0] &+= a
            h[1] &+= b
            h[2] &+= c
            h[3] &+= d
            h[4] &+= e
        }
        return h.flatMap { word in [24, 16, 8, 0].map { UInt8(truncatingIfNeeded: word >> UInt32($0)) } }
    }

    static func base64(_ bytes: [UInt8]) -> String {
        let alphabet = Array("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/".utf8)
        var output: [UInt8] = []
        output.reserveCapacity((bytes.count + 2) / 3 * 4)
        for start in stride(from: 0, to: bytes.count, by: 3) {
            let count = min(3, bytes.count - start)
            var group = UInt32(bytes[start]) << 16
            if count > 1 { group |= UInt32(bytes[start + 1]) << 8 }
            if count > 2 { group |= UInt32(bytes[start + 2]) }
            for index in 0..<4 {
                output.append(index <= count ? alphabet[Int(group >> UInt32(18 - index * 6) & 63)] : UInt8(ascii: "="))
            }
        }
        return String(decoding: output, as: UTF8.self)
    }
}

// A parsed ws or wss URL.
struct GesNetworkSocketEndpoint: Sendable {
    let secure: Bool
    let host: String
    let port: Int
    let target: String

    init(_ url: String) throws {
        let invalid = GesApiError.invalidArgument("The URL must be a ws or wss endpoint with a host.")
        // A scheme has no colon, so the first one must open the "://" separator.
        guard let colon = url.firstIndex(of: ":"), url[colon...].hasPrefix("://") else { throw invalid }
        let scheme = url[..<colon]
        if GesNetworkSocketUpgrade.equalIgnoringCase(scheme, "ws") {
            secure = false
        } else if GesNetworkSocketUpgrade.equalIgnoringCase(scheme, "wss") {
            secure = true
        } else {
            throw invalid
        }

        let rest = url[url.index(colon, offsetBy: 3)...]
        let authorityEnd = rest.firstIndex { $0 == "/" || $0 == "?" || $0 == "#" } ?? rest.endIndex
        let authority = rest[..<authorityEnd]
        var path = String(rest[authorityEnd...])
        if let fragment = path.firstIndex(of: "#") { path = String(path[..<fragment]) }
        target = path.isEmpty ? "/" : path.hasPrefix("?") ? "/" + path : path
        var portText: Substring?
        if authority.hasPrefix("[") {
            guard let close = authority.firstIndex(of: "]") else { throw invalid }
            host = String(authority[authority.index(after: authority.startIndex)..<close])
            let after = authority[authority.index(after: close)...]
            if !after.isEmpty {
                guard after.hasPrefix(":") else { throw invalid }
                portText = after.dropFirst()
            }
        } else if let colon = authority.lastIndex(of: ":") {
            host = String(authority[..<colon])
            portText = authority[authority.index(after: colon)...]
        } else {
            host = String(authority)
        }

        if host.isEmpty || host.contains("@") { throw invalid }
        if let portText {
            guard let value = Int(portText), value > 0, value <= 65535 else { throw invalid }
            port = value
        } else {
            port = secure ? 443 : 80
        }
    }

    // The Host header value.
    var hostHeader: String {
        let name = host.contains(":") ? "[\(host)]" : host
        return port == (secure ? 443 : 80) ? name : "\(name):\(port)"
    }

    // The TLS server name; IP literals send none.
    var serverName: String? {
        if host.contains(":") { return nil }
        if host.utf8.allSatisfy({ ($0 >= 48 && $0 <= 57) || $0 == 46 }) { return nil }
        return host
    }
}

// Sends the upgrade request, checks the answer and replaces itself with the WebSocket handlers.
final class GesNetworkSocketClientUpgradeHandler: ChannelInboundHandler, RemovableChannelHandler {
    typealias InboundIn = ByteBuffer
    typealias InboundOut = ByteBuffer
    typealias OutboundOut = ByteBuffer

    private let endpoint: GesNetworkSocketEndpoint
    private let inbox: GesNetworkSocketInbox
    private let limit: Int
    private let upgraded: EventLoopPromise<Void>
    private var key = ""
    private var buffer: ByteBuffer?
    private var sent = false
    private var done = false

    init(endpoint: GesNetworkSocketEndpoint, inbox: GesNetworkSocketInbox, limit: Int, upgraded: EventLoopPromise<Void>) {
        self.endpoint = endpoint
        self.inbox = inbox
        self.limit = limit
        self.upgraded = upgraded
    }

    func handlerAdded(context: ChannelHandlerContext) {
        if context.channel.isActive { send(context) }
    }

    func channelActive(context: ChannelHandlerContext) {
        send(context)
        context.fireChannelActive()
    }

    func channelRead(context: ChannelHandlerContext, data: NIOAny) {
        if done { return }
        var chunk = Self.unwrapInboundIn(data)
        if buffer == nil {
            buffer = chunk
        } else {
            buffer!.writeBuffer(&chunk)
        }

        guard let length = GesNetworkSocketUpgrade.headLength(buffer!) else {
            if buffer!.readableBytes > GesNetworkSocketUpgrade.maxRequestBytes { fail(context, GesNetworkSocketError("The WebSocket upgrade answer is too long.")) }
            return
        }

        let head = buffer!.readString(length: length) ?? ""
        let lines = head.split(separator: "\r\n", omittingEmptySubsequences: false)
        let statusLine = String(lines[0])
        guard statusLine.hasPrefix("HTTP/1.1 101") || statusLine.hasPrefix("HTTP/1.0 101") else {
            fail(context, GesNetworkSocketError("The server refused the WebSocket upgrade: \(statusLine)"))
            return
        }

        let expected = GesNetworkSocketUpgrade.accept(key)
        let accepted = lines.dropFirst().contains { line in
            guard let colon = line.firstIndex(of: ":") else { return false }
            return GesNetworkSocketUpgrade.equalIgnoringCase(GesNetworkSocketUpgrade.trim(line[..<colon]), "Sec-WebSocket-Accept")
                && GesNetworkSocketUpgrade.trim(line[line.index(after: colon)...]) == expected
        }
        guard accepted else {
            fail(context, GesNetworkSocketError("The server answered the WebSocket upgrade without the expected accept value."))
            return
        }

        done = true
        do {
            try context.pipeline.syncOperations.addHandlers(
                GesNetworkSocketWebSocketHandler.handlers(inbox: inbox, limit: limit, masks: true),
                position: .after(self)
            )
        } catch {
            fail(context, error)
            return
        }

        let rest = buffer!
        buffer = nil
        if rest.readableBytes > 0 { context.fireChannelRead(Self.wrapInboundOut(rest)) }
        context.pipeline.syncOperations.removeHandler(context: context, promise: nil)
        upgraded.succeed()
    }

    func channelInactive(context: ChannelHandlerContext) {
        if !done {
            done = true
            upgraded.fail(GesNetworkSocketError("The connection closed during the WebSocket upgrade."))
        }
        context.fireChannelInactive()
    }

    func errorCaught(context: ChannelHandlerContext, error: Error) {
        if done {
            context.fireErrorCaught(error)
            return
        }
        fail(context, error)
    }

    private func send(_ context: ChannelHandlerContext) {
        if sent { return }
        sent = true
        var random = SystemRandomNumberGenerator()
        key = GesNetworkSocketUpgrade.base64((0..<16).map { _ in UInt8.random(in: 0...255, using: &random) })
        let request =
            "GET \(endpoint.target) HTTP/1.1\r\nHost: \(endpoint.hostHeader)\r\nUpgrade: websocket\r\nConnection: Upgrade\r\n"
            + "Sec-WebSocket-Key: \(key)\r\nSec-WebSocket-Version: 13\r\n\r\n"
        context.writeAndFlush(Self.wrapOutboundOut(context.channel.allocator.buffer(string: request)), promise: nil)
    }

    private func fail(_ context: ChannelHandlerContext, _ error: Error) {
        if done { return }
        done = true
        upgraded.fail(error)
        context.close(promise: nil)
    }
}

// Reads the request head of one client, answers it and on success hands the upgraded channel to the listener.
final class GesNetworkSocketServerUpgradeHandler: ChannelInboundHandler, RemovableChannelHandler {
    typealias InboundIn = ByteBuffer
    typealias InboundOut = ByteBuffer
    typealias OutboundOut = ByteBuffer

    private let path: String
    private let limit: Int
    private let queue: GesNetworkSocketQueue
    // The client's IP address, which holds a place in the queue while the upgrade runs and while the client waits.
    private let address: String?
    private var buffer: ByteBuffer?
    private var timeout: Scheduled<Void>?
    private var done = false

    init(path: String, limit: Int, address: String?, queue: GesNetworkSocketQueue) {
        self.path = path
        self.limit = limit
        self.address = address
        self.queue = queue
    }

    func handlerAdded(context: ChannelHandlerContext) {
        let loopBound = NIOLoopBound(context, eventLoop: context.eventLoop)
        timeout = context.eventLoop.assumeIsolated().scheduleTask(in: GesNetworkSocketUpgrade.timeout) { [self] in
            if done { return }
            stop(loopBound.value)
            loopBound.value.close(promise: nil)
        }
    }

    func handlerRemoved(context: ChannelHandlerContext) {
        timeout?.cancel()
        if !done {
            done = true
            queue.upgradeEnded(address, queued: false)
        }
    }

    func channelRead(context: ChannelHandlerContext, data: NIOAny) {
        if done { return }
        var chunk = Self.unwrapInboundIn(data)
        if buffer == nil {
            buffer = chunk
        } else {
            buffer!.writeBuffer(&chunk)
        }

        let length = GesNetworkSocketUpgrade.headLength(buffer!)
        if length == nil && buffer!.readableBytes < GesNetworkSocketUpgrade.maxRequestBytes { return }
        // Like the C# listener, a head is at most 8192 bytes long including its empty line.
        let response: (text: String, upgrade: Bool)
        if let length, length <= GesNetworkSocketUpgrade.maxRequestBytes {
            response = GesNetworkSocketUpgrade.answer(buffer!.readString(length: length) ?? "", path: path)
        } else {
            response = GesNetworkSocketUpgrade.status(400, "Bad Request")
        }

        let written = context.writeAndFlush(Self.wrapOutboundOut(context.channel.allocator.buffer(string: response.text)))
        if !response.upgrade {
            stop(context)
            let channel = context.channel
            written.whenComplete { _ in channel.close(promise: nil) }
            return
        }

        // A write that fails at once may already have closed the channel.
        if done { return }
        let inbox = GesNetworkSocketInbox()
        do {
            try context.pipeline.syncOperations.addHandlers(
                GesNetworkSocketWebSocketHandler.handlers(inbox: inbox, limit: limit, masks: false),
                position: .after(self)
            )
        } catch {
            stop(context)
            context.close(promise: nil)
            return
        }

        let rest = buffer!
        buffer = nil
        if rest.readableBytes > 0 { context.fireChannelRead(Self.wrapInboundOut(rest)) }
        // The queued client keeps its address's place until the host takes it.
        queue.offer(context.channel, inbox, address)
        stop(context, queued: true)
        context.pipeline.syncOperations.removeHandler(context: context, promise: nil)
    }

    func channelInactive(context: ChannelHandlerContext) {
        stop(context)
        context.fireChannelInactive()
    }

    // The upgrade is over; the listener counts it no longer.
    private func stop(_ context: ChannelHandlerContext, queued: Bool = false) {
        if done { return }
        done = true
        timeout?.cancel()
        queue.upgradeEnded(address, queued: queued)
    }
}
