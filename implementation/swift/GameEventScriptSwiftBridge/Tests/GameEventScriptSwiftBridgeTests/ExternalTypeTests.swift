// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import GameEventScriptRuntime
import GameEventScriptSwiftBridge
import XCTest

final class ExternalTypeTests: XCTestCase {
    struct Player {
        let name: String
        let score: Int
    }

    final class MutablePlayer { var name = "before" }

    func playerType() throws -> GesBridgeTypeBinding<Player> {
        try .init(
            "Player",
            fields: [.init("name", typeName: "Text", keyPath: \Player.name), .init("score", typeName: "Number", keyPath: \Player.score)],
            constructors: [.init(parameters: ["score", "name"]) { args in try Player(name: args.nativeValue(at: 1), score: args.nativeValue(at: 0)) }]
        )
    }

    func testDescriptorCatalogConstructorAndKeyPathAccess() throws {
        let type = try playerType()
        let registry = try GesBridgeExternalTypeRegistry([type.binding])
        XCTAssertEqual(registry.types.count, 1)
        XCTAssertEqual(try registry.resolve(":Player")?.fields.map(\.name), ["name", "score"])
        let reference = try GesExternalTypeConstructorReference(typeName: "Player", argumentLabels: ["name", "score"])
        let constructor = try XCTUnwrap(registry.resolve(reference))
        XCTAssertEqual(constructor.definition.parameters.map(\.name), ["score", "name"])
        let call = GesExternalTypeConstructorCall(arguments: .init([.integer(42), .text("Ada")]))
        try constructor.invoke(call)
        let player = try type.unwrap(call.result)
        XCTAssertEqual(player.name, "Ada")
        XCTAssertEqual(player.score, 42)
        XCTAssertEqual(try call.result.materializedMap()?.get("name"), .text("Ada"))
        XCTAssertNil(try call.result.externalValue?.field("missing"))
        XCTAssertNil(registry.resolve(try .init(typeName: "Player", argumentLabels: [])))
        XCTAssertThrowsError(try playerType().unwrap(call.result))
    }

    func testClassBindingsRetainIdentityAndGettersCanThrow() throws {
        let type = try GesBridgeTypeBinding<MutablePlayer>("Player", fields: [.init("name", typeName: "Text", keyPath: \MutablePlayer.name)])
        let player = MutablePlayer()
        let wrapped = type.wrap(player)
        player.name = "after"
        XCTAssertTrue(try type.unwrap(wrapped) === player)
        XCTAssertEqual(try wrapped.externalValue?.field("name"), .text("after"))
        let broken = try GesBridgeTypeBinding<Player>("Broken", fields: [.init("name", typeName: "Text", get: { _ in throw try GesExtensionFaultError(code: "app.field", message: "Failed") })])
        XCTAssertThrowsError(try broken.wrap(Player(name: "", score: 0)).materializedMap()) { XCTAssertEqual(($0 as? GesExtensionFaultError)?.diagnostic.code, "app.field") }
    }

    func testDuplicateAndInconsistentBindingsFailBeforeUse() throws {
        let type = try playerType()
        XCTAssertThrowsError(try GesBridgeExternalTypeRegistry([type.binding, type.binding]))
        let field = try GesBridgeFieldBinding<Player>("name", typeName: "Text", keyPath: \Player.name)
        XCTAssertThrowsError(try GesBridgeTypeBinding("Player", fields: [field, field]))
        let make = GesBridgeConstructorBinding<Player>(parameters: ["missing"]) { _ in Player(name: "", score: 0) }
        XCTAssertThrowsError(try GesBridgeTypeBinding("Player", fields: [field], constructors: [make]))
        let zero = GesBridgeConstructorBinding<Player> { _ in Player(name: "", score: 0) }
        XCTAssertThrowsError(try GesBridgeTypeBinding("Player", fields: [field], constructors: [zero, zero]))
    }
}
