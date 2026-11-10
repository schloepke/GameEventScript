// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import GameEventScriptCompiler
import GameEventScriptRuntime
import GameEventScriptSwiftBridge
import XCTest

/// Handler values have no Conformance transport encoding, so what they can create is pinned natively.
final class SystemEndpointTests: XCTestCase {
    func testSystemEndpointSignaturesCreateNoMessage() throws {
        XCTAssertNil(try GesMessageSignature(name: "initialization", parameters: []).makeMessage([]))
        let signature = try GesMessageSignature(name: "undeliverable", parameters: ["message"])
        XCTAssertNil(signature.makeMessage([.nothing]))
        XCTAssertThrowsError(try signature.withArguments([.nothing]))
        XCTAssertThrowsError(try GesValue.handler(signature).json())
    }

    func testBindingAHandlerOfASystemEndpointCreatesNoMessage() throws {
        let program = try GesProgramBuilder().addScript("on Start(handler) { let bound be asm { BindHandler bound, handler(message: 42) }\n emit Seen(missing: bound is nothing) }").compile()
        let host = try GameEventScriptHost.makeBuilder().build().startForTest()
        var missing = false
        _ = try host.subscribe(.init(name: "Seen", parameters: ["missing"])) { message, _ in missing = message.arguments[0].asBoolean == true }
        _ = try host.load(program)
        host.receive(try .init(name: "Start", arguments: [.init(name: "handler", value: .handler(try .init(name: "undeliverable", parameters: ["message"])))]))
        XCTAssertEqual(try host.runToCompletion().state, .completed)
        XCTAssertTrue(missing)
    }
}
