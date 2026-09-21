// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import GameEventScriptRuntime
import GameEventScriptSwiftBridge
import XCTest

final class ValueTests: XCTestCase {
    func testScalarConversionsAndExactIntegerBoundaries() throws {
        for value: Int64 in [.min, .max, 0, 9_007_199_254_740_993] { XCTAssertEqual(try Int64.fromGesValue(value.toGesValue()), value) }
        XCTAssertEqual(try GameEventScriptSwiftValue.decode(.boolean(true), as: Bool.self), true)
        XCTAssertEqual(try GameEventScriptSwiftValue.encode("e\u{301}"), .text("e\u{301}"))
        XCTAssertEqual(try UInt64.fromGesValue(.integer(.max)), UInt64(Int64.max))
        XCTAssertThrowsError(try UInt64.max.toGesValue())
        XCTAssertThrowsError(try UInt64.fromGesValue(.float(9_223_372_036_854_775_808)))
        XCTAssertThrowsError(try UInt.fromGesValue(.integer(-1)))
        XCTAssertThrowsError(try Int8.fromGesValue(.integer(128)))
        XCTAssertThrowsError(try Int.fromGesValue(.float(1.5)))
        XCTAssertThrowsError(try Int64.fromGesValue(.float(.infinity)))
    }

    func testFloatingConversionRejectsPrecisionLossAndPreservesFactoryNormalization() throws {
        XCTAssertEqual(try Double.fromGesValue(.integer(42)), 42)
        XCTAssertEqual(try Float.fromGesValue(.float(0.5)), 0.5)
        XCTAssertEqual(try Double.fromGesValue(Double.infinity.toGesValue()), .infinity)
        XCTAssertEqual(try Float.fromGesValue(.float(.infinity)), .infinity)
        XCTAssertThrowsError(try Double.fromGesValue(.integer(.max)))
        XCTAssertThrowsError(try Float.fromGesValue(.float(0.1)))
        XCTAssertTrue(Double.nan.toGesValue().isNothing)
        XCTAssertThrowsError(try Double.fromGesValue(.nothing))
    }

    func testNativeConversionIsStrictAndGesValuePreservesSpecializedKinds() throws {
        XCTAssertThrowsError(try String.fromGesValue(.integer(1)))
        XCTAssertThrowsError(try String.fromGesValue(.tag("ready")))
        XCTAssertThrowsError(try Bool.fromGesValue(.text("true")))
        XCTAssertThrowsError(try Double.fromGesValue(.percentage(0.1)))
        XCTAssertThrowsError(try Int.fromGesValue(.integer(3, unit: .meter)))
        for value in [GesValue.percentage(0.1), .integer(3, unit: .meter), .dice([2, 5]), .nothing] { XCTAssertEqual(try GameEventScriptSwiftValue.decode(value, as: GesValue.self), value) }
    }

    func testNestedCollectionsAndOptionalValuesAreSnapshots() throws {
        var values: [String: [Int?]] = ["z": [1, nil], "a": [2]]
        let encoded = try values.toGesValue()
        values["z"] = [8]
        XCTAssertEqual(encoded.mapEntries?.map(\.key), ["a", "z"])
        XCTAssertEqual(try [String: [Int?]].fromGesValue(encoded), ["z": [1, nil], "a": [2]])
        let optionalMap = try [String: Int?].fromGesValue(.map([.init(key: "x", value: .nothing)]))
        XCTAssertEqual(optionalMap.count, 1)
        XCTAssertNotNil(optionalMap.index(forKey: "x"))
        XCTAssertNil(try Int?.fromGesValue(.nothing))
        XCTAssertThrowsError(try [Int].fromGesValue(.list([.text("1")])))
    }

    func testDictionaryDecodeRejectsCanonicalEquivalenceCollisions() throws {
        let value = GesValue.map([.init(key: "é", value: .integer(1)), .init(key: "e\u{301}", value: .integer(2))])
        XCTAssertEqual(value.length, 2)
        XCTAssertThrowsError(try [String: Int].fromGesValue(value)) { guard case GameEventScriptSwiftConversionError.dictionaryKeyCollision = $0 else { return XCTFail("\($0)") } }
        XCTAssertThrowsError(try [String: Int].fromGesValue(.record(typeName: "Thing", entries: [])))
    }
}
