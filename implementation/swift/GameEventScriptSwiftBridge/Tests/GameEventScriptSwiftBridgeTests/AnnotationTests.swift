// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import GameEventScriptSwiftBridge
import XCTest

@testable import GameEventScriptRuntime

@GesType("Player")
private struct AnnotatedPlayer {
    @GesField
    let name: String
    @GesField("score")
    let points: Int
    @GesField
    var doubled: Int { points * 2 }
    let secret: String = "hidden"

    @GesConstruct
    init(points: Int, name: String) {
        self.points = points
        self.name = name
    }
}

@GesType
private struct AnnotatedThruster {
    @GesField(unit: .meterPerSecond)
    var speed: Double = 3
    @GesField(unit: .newton)
    var thrust: Double = 21400
    @GesField(unit: .degreePerSecond)
    var turnRate: Double = 120
}

@GesType
private final class AnnotatedSensor {
    @GesField(unit: .meter)
    var distance: Double
    @GesField
    var samples: [Int?] = [1, nil, 3]
    @GesField
    var metadata: [String: Bool] = ["ready": true]
    @GesField(typeName: "Tag")
    var category: GesValue { try! .tag("sensor") }

    init(_ distance: Double) { self.distance = distance }

    @GesConstruct
    static func make(distance: Double) throws -> AnnotatedSensor {
        guard distance >= 0 else { throw GesBridgeConversionError.outOfRange("distance") }
        return AnnotatedSensor(distance)
    }
}

@GesType
private struct ReadOnlyData {
    @GesField
    let active: Swift.Bool
    @GesField
    let amount: Swift.Double?
    @GesField
    let names: [String]
}

@GesType
private struct PositionalData {
    @GesField("count")
    let value: UInt8

    @GesConstruct
    init(_ value: UInt8) { self.value = value }
}

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

final class AnnotationTests: XCTestCase {
    func testUnmarkedInitializerIsNotExposedAndOptionalTypesAreInferred() throws {
        let type = try ReadOnlyData.createGesType()
        XCTAssertTrue(type.definition.constructors.isEmpty)
        XCTAssertEqual(type.definition.fields.map(\.typeName), ["Boolean", "Number", "List"])
        let wrapped = type.wrap(ReadOnlyData(active: true, amount: nil, names: ["Ada"]))
        XCTAssertEqual(try wrapped.externalValue?.field("amount"), .nothing)
    }

    func testPositionalNativeParameterUsesRenamedFieldAndChecksOverflow() throws {
        let type = try PositionalData.createGesType()
        let registry = try GesBridgeExternalTypeRegistry([type.binding])
        let constructor = try XCTUnwrap(registry.resolve(.init(typeName: "PositionalData", argumentLabels: ["count"])))
        let call = GesExternalTypeConstructorCall(arguments: .init([.integer(255)]))
        try constructor.invoke(call)
        XCTAssertEqual(try type.unwrap(call.result).value, 255)
        XCTAssertThrowsError(try constructor.invoke(.init(arguments: .init([.integer(256)]))))
    }

    func testGeneratedBindingsExposeOnlyMarkedFieldsAndPreserveIdentity() throws {
        let type = try AnnotatedPlayer.createGesType()
        XCTAssertEqual(type.definition.name, "Player")
        XCTAssertEqual(type.definition.fields.map(\.name), ["name", "score", "doubled"])
        XCTAssertEqual(type.definition.fields.map(\.typeName), ["Text", "Number", "Number"])
        let registry = try GesBridgeExternalTypeRegistry([type.binding])
        let constructor = try XCTUnwrap(registry.resolve(.init(typeName: "Player", argumentLabels: ["name", "score"])))
        XCTAssertEqual(constructor.definition.parameters.map(\.name), ["score", "name"])
        let call = GesExternalTypeConstructorCall(arguments: .init([.integer(21), .text("Ada")]))
        try constructor.invoke(call)
        XCTAssertEqual(try type.unwrap(call.result).points, 21)
        XCTAssertEqual(try call.result.externalValue?.field("doubled"), .integer(42))
        XCTAssertNil(try call.result.externalValue?.field("secret"))
        XCTAssertThrowsError(try AnnotatedPlayer.createGesType().unwrap(call.result))
        let invalid = GesExternalTypeConstructorCall(arguments: .init([.float(1.5), .text("Ada")]))
        XCTAssertThrowsError(try constructor.invoke(invalid))
    }

    func testPhysicalUnitFieldsKeepTheirUnits() throws {
        let type = try AnnotatedThruster.createGesType()
        XCTAssertEqual(type.definition.fields.map { $0.unit }, [GesQuantityUnit.meterPerSecond, .newton, .degreePerSecond])
        let value = type.wrap(AnnotatedThruster())
        XCTAssertEqual(try value.externalValue?.field("speed"), .integer(3, unit: .meterPerSecond))
        XCTAssertEqual(try value.externalValue?.field("thrust"), .integer(21400, unit: .newton))
        XCTAssertEqual(try value.externalValue?.field("turnRate"), .integer(120, unit: .degreePerSecond))
    }

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

    func testUnitsComputedFieldsCollectionsAndStaticFactory() throws {
        let type = try AnnotatedSensor.createGesType()
        XCTAssertEqual(type.definition.fields.first?.unit, .meter)
        let registry = try GesBridgeExternalTypeRegistry([type.binding])
        let constructor = try XCTUnwrap(registry.resolve(.init(typeName: "AnnotatedSensor", argumentLabels: ["distance"])))
        XCTAssertEqual(constructor.definition.parameters.first?.unit, .meter)
        let call = GesExternalTypeConstructorCall(arguments: .init([.float(1.5, unit: .meter)]))
        try constructor.invoke(call)
        let sensor = try type.unwrap(call.result)
        XCTAssertEqual(sensor.distance, 1.5)
        sensor.distance = 2
        XCTAssertEqual(try call.result.externalValue?.field("distance"), .integer(2, unit: .meter))
        XCTAssertEqual(try call.result.externalValue?.field("samples"), .list([.integer(1), .nothing, .integer(3)]))
        XCTAssertEqual(try call.result.externalValue?.field("category"), try .tag("sensor"))
        XCTAssertNil(registry.resolve(try .init(typeName: "AnnotatedSensor")))
        for value in [GesValue.float(1.5), .float(1.5, unit: .second), .text("1.5m"), .float(-1, unit: .meter)] {
            XCTAssertThrowsError(try constructor.invoke(GesExternalTypeConstructorCall(arguments: .init([value]))))
        }
    }

    func testQuantityConversionNeverDiscardsUnexpectedUnitsOrKinds() throws {
        XCTAssertEqual(try GesBridgeValueConversion.encode(Optional<Double>.none, unit: .meter), .nothing)
        let value: Double? = try GesBridgeValueConversion.decode(.nothing, unit: .meter)
        XCTAssertNil(value)
        XCTAssertThrowsError(try GesBridgeValueConversion.encode(GesValue.integer(2, unit: .second), unit: .meter))
        XCTAssertThrowsError(try GesBridgeValueConversion.encode(true, unit: .meter))
        XCTAssertThrowsError(try GesBridgeValueConversion.decode(.float(1.5, unit: .meter), as: Int.self, unit: .meter))
    }
}
