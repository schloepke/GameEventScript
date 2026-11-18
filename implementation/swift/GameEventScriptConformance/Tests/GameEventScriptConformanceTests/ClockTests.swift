// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import GameEventScriptRuntime
import XCTest

/// Native controls of the public system and virtual clocks.
final class ClockTests: XCTestCase {
    func testSystemClockStartsNearZeroAndNeverGoesBack() {
        let clock = GesSystemClock()
        var previous = clock.elapsedMicroseconds
        XCTAssertTrue(previous >= 0 && previous < 60_000_000, "\(previous)")
        for _ in 0..<1000 {
            let now = clock.elapsedMicroseconds
            XCTAssertGreaterThanOrEqual(now, previous)
            previous = now
        }
    }

    func testVirtualClockMovesOnlyWhenAdvanced() throws {
        let clock = GesVirtualClock()
        XCTAssertEqual(clock.elapsedMicroseconds, 0)
        try clock.advance(by: 0)
        try clock.advance(by: 1500)
        XCTAssertEqual(clock.elapsedMicroseconds, 1500)
        XCTAssertEqual(try GesVirtualClock(elapsedMicroseconds: 7).elapsedMicroseconds, 7)
        XCTAssertThrowsError(try GesVirtualClock(elapsedMicroseconds: -1))
        XCTAssertThrowsError(try clock.advance(by: -1))
        XCTAssertThrowsError(try clock.advance(by: .max))
        XCTAssertEqual(clock.elapsedMicroseconds, 1500)
        try clock.advance(by: .max - 1500)
        XCTAssertEqual(clock.elapsedMicroseconds, .max)
    }
}
