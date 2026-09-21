// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import GameEventScriptRuntime
import XCTest

extension GameEventScriptHost {
    func startForTest() throws -> Self {
        XCTAssertEqual(try start().state, .ready)
        return self
    }
}
