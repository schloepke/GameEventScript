// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import XCTest

@testable import GameEventScriptNetwork

final class NetworkBuffersTests: XCTestCase {
    func testStartReservesExactlyTheTotal() {
        let message = GesNetworkPartial()
        message.start(0, 0, 1001)
        XCTAssertEqual(message.capacity, 1001 + 126)
    }

    func testCancelFreesTheStorage() {
        let message = GesNetworkPartial()
        message.start(0, 0, 4096)
        message.cancel(0, 0)
        XCTAssertEqual(message.capacity, 0)
        XCTAssertTrue(message.cancelled)
    }

    func testReleasedMessagesKeepStorageOnlyWithinTheRetention() {
        let receive = GesNetworkOrderedReceive(retention: 1500)
        let first = receive.message(0)
        first.start(0, 0, 1000)
        let second = receive.message(1)
        second.start(0, 1, 1000)
        receive.release(first)
        XCTAssertEqual(receive.retained, 1125)
        receive.release(second)
        XCTAssertEqual(second.capacity, 0)
        XCTAssertEqual(receive.retained, 1125)

        // Taking a kept message back counts its storage out of the retention.
        XCTAssertTrue(receive.message(2) === second)
        XCTAssertTrue(receive.message(3) === first)
        XCTAssertEqual(receive.retained, 0)
    }
}
