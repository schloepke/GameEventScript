// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import GameEventScriptNetwork
import GameEventScriptRuntime
import XCTest

final class NetworkCookiesTests: XCTestCase {
    private static let source = """
        wire messages :Commands as (Move)
        message Move(x: :Number integer between 0 and 255)
        network :Game as { client sends :Commands reliable }
        """

    private static let window: Int64 = 10_000_000
    private static let key = (0..<32).map { UInt8($0) }
    private static let address: [UInt8] = [10, 0, 0, 1, 0x1f, 0x90]

    private static var options: GesNetworkOptions {
        var options = GesNetworkOptions.plain
        options.transport = .datagram
        return options
    }

    private func network() throws -> GesBoundNetwork { try GesSchemaSet(parsing: Self.source).network("Game").bind() }

    private func client() throws -> GesNetworkConnection {
        try GesNetworkConnection.makeClient(network: try network(), token: Array("t".utf8), now: 0, options: Self.options)
    }

    private func hello(_ client: GesNetworkConnection) throws -> [UInt8] {
        var buffer = [UInt8](repeating: 0, count: 2048)
        let length = try client.transmit(into: &buffer)
        XCTAssertEqual(length, 1200)
        return Array(buffer[0..<length])
    }

    private func check(_ gate: GesNetworkCookies, _ packet: [UInt8], _ now: Int64, _ address: [UInt8] = address) throws -> GesNetworkCookieResult {
        var retry = [UInt8](repeating: 0, count: GesNetworkCookies.retryBytes)
        return try gate.check(packet: packet, address: address, now: now, retry: &retry)
    }

    func testHelloWithoutCookieGetsARetryWhoseCookieIsAccepted() throws {
        let gate = try GesNetworkCookies(key: Self.key, windowMicroseconds: Self.window)
        let client = try client()
        var retry = [UInt8](repeating: 0, count: GesNetworkCookies.retryBytes)
        XCTAssertEqual(try gate.check(packet: try hello(client), address: Self.address, now: 5, retry: &retry), .retry)
        XCTAssertEqual(retry[0], 2)
        XCTAssertEqual(retry[1], 24)

        try client.receive(retry, now: 10)
        let hello = try hello(client)
        XCTAssertEqual(try check(gate, hello, 20), .accept)
        XCTAssertEqual(try check(gate, hello, Self.window + 20), .accept)
        XCTAssertEqual(try check(gate, hello, 2 * Self.window), .retry)
        XCTAssertEqual(try check(gate, hello, 20, [10, 0, 0, 2, 0x1f, 0x90]), .retry)
        let other = try GesNetworkCookies(key: [UInt8](repeating: 0, count: 32), windowMicroseconds: Self.window)
        XCTAssertEqual(try check(other, hello, 20), .retry)

        let server = try GesNetworkConnection.makeServer(network: try network(), now: 20, options: Self.options)
        try server.receive(hello, now: 20)
        XCTAssertEqual(server.state, .awaitAccept)
    }

    func testNegativeTimesUseTheFloorWindow() throws {
        let gate = try GesNetworkCookies(key: Self.key, windowMicroseconds: Self.window)
        var retry = [UInt8](repeating: 0, count: 27)
        XCTAssertEqual(try gate.check(packet: try hello(try client()), address: Self.address, now: -1, retry: &retry), .retry)
        XCTAssertEqual(retry[2..<10].reversed().reduce(UInt64(0)) { $0 << 8 | UInt64($1) }, UInt64.max)
    }

    func testShortForeignOrCutDatagramsAreDropped() throws {
        let gate = try GesNetworkCookies(key: Self.key, windowMicroseconds: Self.window)
        let hello = try hello(try client())
        XCTAssertEqual(try check(gate, Array(hello[0..<1199]), 0), .drop)
        var foreign = hello
        foreign[4] ^= 1
        XCTAssertEqual(try check(gate, foreign, 0), .drop)
        var data = hello
        data[0] = 5
        XCTAssertEqual(try check(gate, data, 0), .drop)
        XCTAssertEqual(try check(gate, [UInt8](repeating: 0, count: 1200), 0), .drop)
    }

    func testInvalidArgumentsThrow() throws {
        XCTAssertThrowsError(try GesNetworkCookies(key: [UInt8](repeating: 0, count: 31), windowMicroseconds: Self.window))
        XCTAssertThrowsError(try GesNetworkCookies(key: Self.key, windowMicroseconds: 0))
        let gate = try GesNetworkCookies(key: Self.key, windowMicroseconds: Self.window)
        var retry = [UInt8](repeating: 0, count: 25)
        XCTAssertThrowsError(try gate.check(packet: [UInt8](repeating: 0, count: 1200), address: Self.address, now: 0, retry: &retry))
    }
}
