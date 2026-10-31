// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

@_spi(Syntax) import GameEventScriptSyntax
import XCTest

@testable import GameEventScriptRuntime

/// Keeps the hand-written unit-suffix precheck of the text decoder in step with the suffix table.
final class UnitSuffixSetTests: XCTestCase {
    func testTextDecoderPrecheckAcceptsTheEndOfEverySuffix() {
        for suffix in GesQuantityUnit.suffixes {
            XCTAssertTrue(GesTextNumberCast.possibleSuffixEnd("1" + suffix.text), suffix.text)
        }
    }
}
