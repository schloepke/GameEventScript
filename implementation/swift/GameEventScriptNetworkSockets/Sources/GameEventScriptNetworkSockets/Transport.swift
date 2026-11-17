// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import NIOConcurrencyHelpers
import NIOCore
import NIOWebSocket

/// Raised when a socket cannot carry a session, such as a WebSocket upgrade the server refused.
public struct GesNetworkSocketError: Error, Sendable, CustomStringConvertible {
    /// What failed, including the server's status line when it answered the upgrade.
    public let message: String

    /// Creates the error.
    /// - Parameter message: What failed.
    public init(_ message: String) { self.message = message }

    /// The message.
    public var description: String { message }
}

// The received chunks of one socket, written on its event loop and taken on the host thread. Reading pauses
// while the chunks reach the capacity and resumes when the host takes them; datagrams are dropped instead.
final class GesNetworkSocketInbox: @unchecked Sendable {
    static let capacity = 64
    static let readBytes = 16384

    // A datagram is dropped rather than waited for when this many are not yet taken.
    static let datagramCapacity = 256

    private let lock = NIOLock()
    private var chunks: [ByteBuffer] = []
    private var ended = false
    private var held = false

    func push(_ chunk: ByteBuffer) { lock.withLock { chunks.append(chunk) } }

    func pushDatagram(_ datagram: ByteBuffer) {
        lock.withLock {
            if chunks.count < Self.datagramCapacity { chunks.append(datagram) }
        }
    }

    func end() { lock.withLock { ended = true } }

    // Whether the socket may read on; otherwise it holds until the host takes the chunks.
    func mayRead() -> Bool {
        lock.withLock {
            if chunks.count < Self.capacity { return true }
            held = true
            return false
        }
    }

    // Moves the chunks into the target and reports, from the same moment, whether the transport ended and whether
    // reading must resume.
    func take(into target: inout [ByteBuffer]) -> (ended: Bool, resume: Bool) {
        lock.withLock {
            swap(&chunks, &target)
            let resume = held
            held = false
            return (ended, resume)
        }
    }
}

// Moves the bytes of a TCP stream into the inbox.
final class GesNetworkSocketStreamHandler: ChannelDuplexHandler {
    typealias InboundIn = ByteBuffer
    typealias OutboundIn = ByteBuffer
    typealias OutboundOut = ByteBuffer

    private let inbox: GesNetworkSocketInbox

    init(inbox: GesNetworkSocketInbox) { self.inbox = inbox }

    func channelRead(context: ChannelHandlerContext, data: NIOAny) { inbox.push(Self.unwrapInboundIn(data)) }

    func read(context: ChannelHandlerContext) {
        if inbox.mayRead() { context.read() }
    }

    func channelInactive(context: ChannelHandlerContext) {
        inbox.end()
        context.fireChannelInactive()
    }

    func errorCaught(context: ChannelHandlerContext, error: Error) {
        inbox.end()
        context.close(promise: nil)
    }
}

// Moves one binary WebSocket message per packet into the inbox. A text message is passed as an empty packet and a
// message longer than the packet limit as limit + 1 bytes, so the connection closes with network.malformed or
// network.packetTooLarge; reading ends there. Pings are answered; the channel closes once close frames went both ways.
final class GesNetworkSocketWebSocketHandler: ChannelDuplexHandler {
    typealias InboundIn = WebSocketFrame
    typealias OutboundIn = WebSocketFrame
    typealias OutboundOut = WebSocketFrame

    private let inbox: GesNetworkSocketInbox
    private let limit: Int
    private let masks: Bool
    private var message: ByteBuffer?
    private var stopped = false
    private var closeSent = false
    private var closeReceived = false

    init(inbox: GesNetworkSocketInbox, limit: Int, masks: Bool) {
        self.inbox = inbox
        self.limit = limit
        self.masks = masks
    }

    // The decoder admits one frame beyond the limit, so a longer message is recognized without reading it.
    static func handlers(inbox: GesNetworkSocketInbox, limit: Int, masks: Bool) -> [ChannelHandler] {
        [
            WebSocketFrameEncoder(), ByteToMessageHandler(WebSocketFrameDecoder(maxFrameSize: limit + 1)),
            GesNetworkSocketWebSocketHandler(inbox: inbox, limit: limit, masks: masks),
        ]
    }

    func channelRead(context: ChannelHandlerContext, data: NIOAny) {
        let frame = Self.unwrapInboundIn(data)
        switch frame.opcode {
        case .connectionClose:
            closeReceived = true
            stop()
            if closeSent { context.close(promise: nil) }
        case .ping:
            if !closeSent {
                let pong = WebSocketFrame(fin: true, opcode: .pong, maskKey: masks ? .random() : nil, data: frame.unmaskedData)
                context.writeAndFlush(Self.wrapOutboundOut(pong), promise: nil)
            }
        case .binary, .continuation:
            if stopped { return }
            var data = frame.unmaskedData
            if frame.opcode == .binary && message == nil {
                message = data
            } else if frame.opcode == .continuation && message != nil {
                message!.writeBuffer(&data)
            } else {
                // A fragment out of order breaks the message framing.
                inbox.push(context.channel.allocator.buffer(capacity: 0))
                stop()
                return
            }

            if message!.readableBytes > limit {
                tooLarge(context)
            } else if frame.fin {
                inbox.push(message!)
                message = nil
            }
        case .text:
            if stopped { return }
            inbox.push(context.channel.allocator.buffer(capacity: 0))
            stop()
        default:
            break
        }
    }

    func read(context: ChannelHandlerContext) {
        if inbox.mayRead() { context.read() }
    }

    func write(context: ChannelHandlerContext, data: NIOAny, promise: EventLoopPromise<Void>?) {
        let frame = Self.unwrapOutboundIn(data)
        guard frame.opcode == .connectionClose else {
            context.write(data, promise: promise)
            return
        }

        closeSent = true
        let written = context.eventLoop.makePromise(of: Void.self)
        context.write(data, promise: written)
        written.futureResult.assumeIsolated().whenComplete { [self] result in
            promise?.completeWith(result)
            if closeReceived { context.close(promise: nil) }
        }
    }

    func channelInactive(context: ChannelHandlerContext) {
        stop()
        context.fireChannelInactive()
    }

    func errorCaught(context: ChannelHandlerContext, error: Error) {
        if case NIOWebSocketError.invalidFrameLength = error {
            tooLarge(context)
            return
        }

        stop()
        context.close(promise: nil)
    }

    private func tooLarge(_ context: ChannelHandlerContext) {
        if stopped { return }
        message = nil
        var chunk = context.channel.allocator.buffer(capacity: limit + 1)
        chunk.writeRepeatingByte(0, count: limit + 1)
        inbox.push(chunk)
        stop()
    }

    private func stop() {
        if stopped { return }
        stopped = true
        inbox.end()
    }
}
