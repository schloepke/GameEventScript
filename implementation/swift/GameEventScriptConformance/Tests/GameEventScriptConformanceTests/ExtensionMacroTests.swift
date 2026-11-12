// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import GameEventScriptCompiler
import GameEventScriptRuntime
import GameEventScriptSwiftBridge
import XCTest

@GesExtension("navigation")
enum NavigationExtensions {
    @GesFunction("destination", unit: .meter, units: ["from": .meter, "distance": .meter])
    static func destination(from: GesPoint, bearing: Double, distance: Double) -> GesPoint? {
        // East for 90°, otherwise north: enough to show the conversions.
        let point = from + (bearing == 90 ? GesVector(x: distance, y: 0) : GesVector(x: 0, y: distance))
        return point.isFinite ? point : nil
    }

    @GesFunction(units: ["a": .meter, "b": .meter])
    static func gap(_ a: GesPoint, _ b: GesPoint) -> Double { a.distance(to: b) }

    @GesPredicate("isFar", units: ["value": .meter])
    static func isFar(value: Double) -> Bool? { value.isFinite ? value > 100 : nil }

    @GesFunction
    static func kind(_ value: GesValue) -> String { String(describing: value.valueKind) }

    @GesFunction
    static func fail() throws -> Double { throw ExtensionMacroTests.Failure.deliberate }
}

@GesExtension("game")
final class GameExtensions {
    private(set) var damage = 0

    @GesFunction("damage")
    func damage(_ amount: Int) -> Int {
        damage += amount
        return damage
    }

    @GesFunction
    static func twice(_ value: Int) -> Int { value * 2 }
}

@GesExtension("strict")
enum StrictExtensions {
    @GesFunction
    static func whole(_ value: Int) -> Int { value }

    @GesFunction
    static func real(_ value: Double) -> Double { value }

    @GesFunction
    static func truth(_ value: Bool) -> Bool { value }

    @GesFunction
    static func maybe(_ value: Int?) -> String { value.map { "value \($0)" } ?? "none" }
}

/// Extensions declared with `@GesExtension`, `@GesFunction` and `@GesPredicate` take their signatures from the declarations
/// and convert arguments and results strictly, with `nothing` for a value that does not convert.
final class ExtensionMacroTests: XCTestCase {
    enum Failure: Error { case deliberate }

    private func run(_ body: String, _ registry: some GesExtensionRegistry) throws -> (GameEventScriptMessage?, GesExecutionResult) {
        let program = try GesProgramBuilder().addScript("on Start() {\n" + body + "\n}").compile()
        let host = try GameEventScriptHost.makeBuilder().withRegistry(registry).build()
        let instance = try host.load(program)
        XCTAssertNil(instance.startResult?.diagnostic, "\(String(describing: instance.startResult))")
        var seen: GameEventScriptMessage?
        _ = try host.subscribe(.init(name: "Seen", parameters: ["a", "b", "c", "d"])) { message, _ in seen = message }
        _ = try host.startForTest()
        XCTAssertTrue(host.receive(try .init(name: "Start")))
        let result = try host.runToCompletion()
        return (seen, result)
    }

    func testSignaturesConversionsAndUnits() throws {
        let registry = try GesBridgeExtensionRegistry(try NavigationExtensions.gesExtensions())
        let (seen, result) = try run(
            "emit Seen(a: :navigation.destination(from: :Point(1m, 2m), bearing: 90, distance: 10m), b: :navigation.gap(:Point(0m, 0m), :Point(3m, 4m)), c: :navigation.destination(from: :Point(1, 2), bearing: 90, distance: 10m), d: 150m is :navigation.isFar)",
            registry
        )
        XCTAssertEqual(result.state, .completed)
        let arguments = try XCTUnwrap(seen).arguments
        XCTAssertEqual(GesPoint(arguments[0], unit: .meter), GesPoint(x: 11, y: 2), "The declared result unit is attached.")
        XCTAssertEqual(arguments[1].asNumber, 5)
        XCTAssertTrue(arguments[2].isNothing, "A point of another unit converts to nothing.")
        XCTAssertEqual(arguments[3], .boolean(true))
        let labels = try NavigationExtensions.gesExtensions().map(\.reference.signatureID)
        XCTAssertEqual(labels, ["navigation.destination(from,bearing,distance)", "navigation.gap(_,_)", "navigation.isFar(_)", "navigation.kind(_)", "navigation.fail()"])
    }

    func testArgumentsConvertOnlyExactly() throws {
        let registry = try GesBridgeExtensionRegistry(try StrictExtensions.gesExtensions())
        let (rejected, _) = try run("emit Seen(a: :strict.whole(2.7), b: :strict.whole(true), c: :strict.real(50%), d: :strict.truth(5))", registry)
        let rejectedArguments = try XCTUnwrap(rejected).arguments
        for index in 0..<4 { XCTAssertTrue(rejectedArguments[index].isNothing, "A fraction, a Boolean, a Percentage and a number do not convert.") }
        let (accepted, _) = try run("emit Seen(a: :strict.whole(3.0), b: :strict.real(2), c: :strict.maybe(nothing), d: :strict.maybe(4))", registry)
        let arguments = try XCTUnwrap(accepted).arguments
        XCTAssertEqual(arguments[0], .integer(3))
        XCTAssertEqual(arguments[1].asNumber, 2)
        XCTAssertEqual(arguments[2], .text("none"), "Nothing reaches an Optional parameter as nil.")
        XCTAssertEqual(arguments[3], .text("value 4"))
    }

    func testRawValuesAndFaults() throws {
        let registry = try GesBridgeExtensionRegistry(try NavigationExtensions.gesExtensions())
        let (seen, _) = try run("emit Seen(a: :navigation.kind(#x), b: :navigation.gap(1, 2), c: 'x' is :navigation.isFar, d: nothing)", registry)
        let arguments = try XCTUnwrap(seen).arguments
        XCTAssertEqual(arguments[0].textValue, "tag")
        XCTAssertTrue(arguments[1].isNothing)
        XCTAssertTrue(arguments[2].isNothing)
        let (failed, result) = try run("emit Seen(a: :navigation.fail(), b: 1, c: 2, d: 3)", registry)
        XCTAssertNil(failed)
        XCTAssertEqual(result.diagnostic?.code, "runtime.extensionCallFailed", "A thrown Swift error stays an extension fault.")
    }

    func testInstanceExtensionsRetainTheirInstance() throws {
        let game = GameExtensions()
        let registry = try GesBridgeExtensionRegistry(try game.gesExtensions())
        let (seen, _) = try run("emit Seen(a: :game.damage(3), b: :game.damage(4), c: :game.twice(5), d: :game.damage(1.5))", registry)
        let arguments = try XCTUnwrap(seen).arguments
        XCTAssertEqual(arguments[0].asInteger, 3)
        XCTAssertEqual(arguments[1].asInteger, 7)
        XCTAssertEqual(arguments[2].asInteger, 10)
        XCTAssertTrue(arguments[3].isNothing, "A fraction does not convert to Int.")
        XCTAssertEqual(game.damage, 7)
        XCTAssertEqual(try GameExtensions.gesExtensions().map(\.reference.signatureID), ["game.twice(_)"])
    }
}
