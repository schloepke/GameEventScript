// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

@_spi(Syntax) import GameEventScriptSyntax
import XCTest

@testable import GameEventScriptRuntime

/// Keeps the hand-written unit-suffix precheck of the text decoder in step with the suffix table.
final class UnitSuffixSetTests: XCTestCase {
    func testTextDecoderPrecheckAcceptsTheEndOfEverySuffix() {
        for suffix in GesQuantityUnit.suffixes {
            XCTAssertTrue(GesNumberLiteral.possibleSuffixEnd("1" + suffix.text), suffix.text)
        }
    }

    // The five UnitId bits accept the defined ids 0 to 18 and reject the reserved rest.
    func testUnitIdsZeroToEighteenAreDefinedAndTheRestReserved() {
        for raw in UInt8(0)..<32 {
            XCTAssertEqual(GesQuantityUnit(unitID: raw) != nil, raw <= 18, "\(raw)")
            guard let unit = GesQuantityUnit(unitID: raw) else { continue }
            XCTAssertEqual(unit.unitID, raw)
            // The root search relies on every unit having its symbol in the spelling table.
            if unit != .none { XCTAssertTrue(GesQuantityUnit.suffixes.contains { $0.unit == unit && $0.exponent == 0 && $0.text == unit.suffix }, unit.rawValue) }
        }
    }

    func testEverySpellingPrecedesTheShorterSpellingsItStartsOrEndsWith() {
        let suffixes = GesQuantityUnit.suffixes
        for later in suffixes.indices {
            for earlier in suffixes.indices where earlier < later {
                let longer = suffixes[later].scalars
                let shorter = suffixes[earlier].scalars
                let contains = longer.starts(with: shorter) || longer.reversed().starts(with: shorter.reversed())
                XCTAssertFalse(longer.count > shorter.count && contains, "\(suffixes[later].text) after \(suffixes[earlier].text)")
            }
        }
    }
}
