// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import GameEventScriptSwiftBridge
import XCTest

// Enumerating every runtime unit needs the internal UnitId initializer.
@testable import GameEventScriptRuntime

// One field per unit, named after its case, so the macro's mirrored unit table is checked against the runtime.
@GesType
private struct EveryUnit {
    @GesField(unit: .degree)
    var degree: Double = 1
    @GesField(unit: .meter)
    var meter: Double = 1
    @GesField(unit: .second)
    var second: Double = 1
    @GesField(unit: .meterPerSecond)
    var meterPerSecond: Double = 1
    @GesField(unit: .meterPerSecondSquared)
    var meterPerSecondSquared: Double = 1
    @GesField(unit: .kilogram)
    var kilogram: Double = 1
    @GesField(unit: .newton)
    var newton: Double = 1
    @GesField(unit: .joule)
    var joule: Double = 1
    @GesField(unit: .watt)
    var watt: Double = 1
    @GesField(unit: .volt)
    var volt: Double = 1
    @GesField(unit: .ampere)
    var ampere: Double = 1
    @GesField(unit: .hertz)
    var hertz: Double = 1
    @GesField(unit: .kelvin)
    var kelvin: Double = 1
    @GesField(unit: .degreePerSecond)
    var degreePerSecond: Double = 1
}

final class MacroUnitTableTests: XCTestCase {
    func testMacroUnitTableMatchesEveryRuntimeUnit() throws {
        let fields = try EveryUnit.createGesType().definition.fields
        let units = (1...UInt8.max).compactMap(GesQuantityUnit.init(unitID:))
        XCTAssertEqual(Set(fields.map(\.name)), Set(units.map(\.rawValue)))
        for unit in units {
            let field = try XCTUnwrap(fields.first { $0.name == unit.rawValue }, "Missing @GesField unit \(unit)")
            XCTAssertEqual(field.unit, unit)
            XCTAssertEqual(field.typeName, "Quantity(\(unit.suffix))")
        }
    }
}
