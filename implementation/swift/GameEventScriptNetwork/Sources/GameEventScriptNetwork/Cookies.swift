// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import GameEventScriptRuntime

private typealias Wire = GesNetworkWire

/// What a server does with a datagram from an address it does not know.
public enum GesNetworkCookieResult: Sendable {
    /// The datagram is not a readable `hello` of at least 1200 bytes; the server ignores it.
    case drop
    /// The cookie is empty or not valid; the server sends the `retry` the gate wrote.
    case retry
    /// The cookie is valid; the server creates a connection and passes it the datagram.
    case accept
}

/// The address validation gate of a datagram server: it answers a client it does not know without keeping state, so a
/// forged source address gains no connection.
///
/// A cookie is the time window number and an HMAC-SHA256 of it and the address with a secret key; the current and the
/// previous window are valid. Checking allocates nothing except when an address is longer than every address before.
/// Not thread-safe; one caller at a time.
public final class GesNetworkCookies {
    /// The number of bytes of a `retry` the gate writes.
    public static let retryBytes = 3 + GesNetworkWire.cookieBytes

    private static let hashBytes = 16
    private static let prefixBytes = 12

    private let hmac: GesNetworkHmacSha256
    private let window: Int64
    private let hash = UnsafeMutableRawBufferPointer.allocate(byteCount: GesNetworkSha256.digestBytes, alignment: 1)
    private var input = UnsafeMutableRawBufferPointer.allocate(byteCount: GesNetworkCookies.prefixBytes + 18, alignment: 1)

    /// Creates a gate.
    /// - Parameters:
    ///   - key: The secret key, exactly 32 bytes; it is not retained.
    ///   - windowMicroseconds: The length of a time window in microseconds, at least 1.
    /// - Throws: `GesApiError.invalidArgument` when the key does not hold 32 bytes or the window length is not positive.
    public init(key: UnsafeRawBufferPointer, windowMicroseconds: Int64) throws {
        if key.count != 32 { throw GesApiError.invalidArgument("The key holds 32 bytes.") }
        if windowMicroseconds < 1 { throw GesApiError.invalidArgument("The window length is positive.") }
        hmac = GesNetworkHmacSha256(key: key)
        window = windowMicroseconds
        input[0] = UInt8(ascii: "G")
        input[1] = UInt8(ascii: "E")
        input[2] = UInt8(ascii: "S")
        input[3] = UInt8(ascii: "C")
    }

    /// Creates a gate.
    /// - Parameters:
    ///   - key: The secret key, exactly 32 bytes; it is not retained.
    ///   - windowMicroseconds: The length of a time window in microseconds, at least 1.
    /// - Throws: `GesApiError.invalidArgument` when the key does not hold 32 bytes or the window length is not positive.
    public convenience init(key: [UInt8], windowMicroseconds: Int64) throws {
        // The designated initializer cannot run inside a closure, so the key passes through a temporary copy.
        let copy = UnsafeMutableRawBufferPointer.allocate(byteCount: key.count, alignment: 1)
        defer { copy.deallocate() }
        copy.copyBytes(from: key)
        try self.init(key: UnsafeRawBufferPointer(copy), windowMicroseconds: windowMicroseconds)
    }

    deinit {
        hash.deallocate()
        input.deallocate()
    }

    /// Checks a datagram of an unknown address and writes the `retry` to send when one is due.
    /// - Parameters:
    ///   - packet: The received datagram.
    ///   - address: The client's address as the transport gives it.
    ///   - now: The current time in microseconds.
    ///   - retry: The target of the `retry`, at least `retryBytes` bytes; written only for
    ///     `GesNetworkCookieResult.retry`.
    /// - Returns: What the server does with the datagram.
    /// - Throws: `GesApiError.invalidArgument` when `retry` holds fewer than `retryBytes` bytes.
    public func check(packet: UnsafeRawBufferPointer, address: UnsafeRawBufferPointer, now: Int64, retry: UnsafeMutableRawBufferPointer) throws
        -> GesNetworkCookieResult
    {
        if retry.count < Self.retryBytes { throw GesApiError.invalidArgument("The retry target holds at least 27 bytes.") }
        var cookie = UnsafeRawBufferPointer(start: nil, count: 0)
        if !Self.findCookie(packet, &cookie) { return .drop }
        let window = windowNumber(now)
        if cookie.count == Wire.cookieBytes {
            let named = Int64(bitPattern: Wire.readUInt64(cookie, 0))
            if (named == window || (window != .min && named == window - 1)) && matches(cookie, address) { return .accept }
        }

        retry[0] = Wire.retry
        retry[1] = UInt8(Wire.cookieBytes)
        Wire.writeUInt64(retry, 2, UInt64(bitPattern: window))
        compute(window, address)
        (retry.baseAddress! + 10).copyMemory(from: hash.baseAddress!, byteCount: Self.hashBytes)
        retry[2 + Wire.cookieBytes] = 0
        return .retry
    }

    /// Checks a datagram of an unknown address and writes the `retry` to send when one is due.
    /// - Parameters:
    ///   - packet: The received datagram.
    ///   - address: The client's address as the transport gives it.
    ///   - now: The current time in microseconds.
    ///   - retry: The target of the `retry`, at least `retryBytes` bytes; written only for
    ///     `GesNetworkCookieResult.retry`.
    /// - Returns: What the server does with the datagram.
    /// - Throws: `GesApiError.invalidArgument` when `retry` holds fewer than `retryBytes` bytes.
    public func check(packet: [UInt8], address: [UInt8], now: Int64, retry: inout [UInt8]) throws -> GesNetworkCookieResult {
        try packet.withUnsafeBytes { packet in
            try address.withUnsafeBytes { address in
                try retry.withUnsafeMutableBytes { try check(packet: packet, address: address, now: now, retry: $0) }
            }
        }
    }

    // The window number: the time divided by the window length, rounded toward negative infinity.
    private func windowNumber(_ now: Int64) -> Int64 {
        let quotient = now / window
        return now % window < 0 ? quotient - 1 : quotient
    }

    private func matches(_ cookie: UnsafeRawBufferPointer, _ address: UnsafeRawBufferPointer) -> Bool {
        compute(Int64(bitPattern: Wire.readUInt64(cookie, 0)), address)
        var difference: UInt8 = 0
        for index in 0..<Self.hashBytes { difference |= cookie[8 + index] ^ hash[index] }
        return difference == 0
    }

    private func compute(_ window: Int64, _ address: UnsafeRawBufferPointer) {
        if input.count < Self.prefixBytes + address.count {
            let grown = UnsafeMutableRawBufferPointer.allocate(byteCount: Self.prefixBytes + address.count, alignment: 1)
            grown.baseAddress!.copyMemory(from: input.baseAddress!, byteCount: input.count)
            input.deallocate()
            input = grown
        }
        Wire.writeUInt64(input, 4, UInt64(bitPattern: window))
        if !address.isEmpty { (input.baseAddress! + Self.prefixBytes).copyMemory(from: address.baseAddress!, byteCount: address.count) }
        hmac.compute(UnsafeRawBufferPointer(rebasing: input[0..<Self.prefixBytes + address.count]), into: hash)
    }

    // Reads the fields of a hello up to its cookie within their bounds; false when the datagram is to be dropped.
    private static func findCookie(_ packet: UnsafeRawBufferPointer, _ cookie: inout UnsafeRawBufferPointer) -> Bool {
        if packet.count < Wire.minimumPacketBytes || packet[0] != Wire.hello || Wire.readUInt32(packet, 1) != Wire.magic { return false }
        var position = 5
        var value: UInt32 = 0
        if Wire.readVarint(packet, &position, &value) != 1 || packet.count - position < 8 { return false }
        position += 8
        var routes: UInt32 = 0
        if Wire.readVarint(packet, &position, &routes) != 1 || routes < 1 || routes > UInt32(Wire.maximumRoutes) || packet.count - position < 8 * Int(routes) {
            return false
        }
        position += 8 * Int(routes)
        // The packet, baseline and transfer limits and the compression mask.
        for _ in 0..<4 {
            if Wire.readVarint(packet, &position, &value) != 1 { return false }
        }
        var length: UInt32 = 0
        if Wire.readVarint(packet, &position, &length) != 1 || length > UInt32(Wire.maximumCookieBytes) || packet.count - position < Int(length) { return false }
        cookie = UnsafeRawBufferPointer(rebasing: packet[position..<position + Int(length)])
        return true
    }
}
