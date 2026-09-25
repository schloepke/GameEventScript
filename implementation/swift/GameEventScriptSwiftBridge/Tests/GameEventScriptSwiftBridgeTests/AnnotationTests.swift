// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import GameEventScriptRuntime
import GameEventScriptSwiftBridge
import XCTest

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
        guard distance >= 0 else { throw GameEventScriptSwiftConversionError.outOfRange("distance") }
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
        let registry = try GameEventScriptSwiftExternalTypeRegistry([type.binding])
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
        let registry = try GameEventScriptSwiftExternalTypeRegistry([type.binding])
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

    func testUnitsComputedFieldsCollectionsAndStaticFactory() throws {
        let type = try AnnotatedSensor.createGesType()
        XCTAssertEqual(type.definition.fields.first?.unit, .meter)
        let registry = try GameEventScriptSwiftExternalTypeRegistry([type.binding])
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
        XCTAssertEqual(try GameEventScriptSwiftValue.encode(Optional<Double>.none, unit: .meter), .nothing)
        let value: Double? = try GameEventScriptSwiftValue.decode(.nothing, unit: .meter)
        XCTAssertNil(value)
        XCTAssertThrowsError(try GameEventScriptSwiftValue.encode(GesValue.integer(2, unit: .second), unit: .meter))
        XCTAssertThrowsError(try GameEventScriptSwiftValue.encode(true, unit: .meter))
        XCTAssertThrowsError(try GameEventScriptSwiftValue.decode(.float(1.5, unit: .meter), as: Int.self, unit: .meter))
    }
}
