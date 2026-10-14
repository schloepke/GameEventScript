// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import XCTest

@testable import GameEventScriptRuntime

/// Native weak-ownership control; message behavior is covered by the shared corpus.
final class ContextOwnershipTests: XCTestCase {
    private weak var borrowedHost: GameEventScriptHost?

    func testContextDoesNotRetainHost() throws {
        var host: GameEventScriptHost? = try GameEventScriptHost.makeBuilder().build()
        borrowedHost = host
        let context = GesContext(host: host!, random: .init(seed: 0), limits: .init(), extensions: nil, observer: nil)
        host = nil
        XCTAssertNil(borrowedHost)
        let message = try GameEventScriptMessage(name: "Test")
        XCTAssertEqual(context.pendingMessageCount, 0)
        XCTAssertTrue(context.isIdle)
        XCTAssertFalse(context.emit(message))
        let result = context.publish(message)
        XCTAssertFalse(result.localAccepted)
        XCTAssertFalse(result.outboundAttempted)
        XCTAssertFalse(result.outboundAccepted)
        XCTAssertFalse(result.outboundDeferred)
        XCTAssertFalse(context.send(message, publish: false, microseconds: 0))
    }
}
