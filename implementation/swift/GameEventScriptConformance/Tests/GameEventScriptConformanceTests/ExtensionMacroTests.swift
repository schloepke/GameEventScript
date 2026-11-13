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

// A GesRandomGenerator parameter receives the call's generator in any position and is no script argument.
@GesExtension("arena")
enum ArenaExtensions {
    @GesFunction
    static func first(random: GesRandomGenerator, count: Int) -> String { draw(random, count, 0) }

    @GesFunction
    static func middle(count: Int, _ random: GesRandomGenerator, _ offset: Double) -> String { draw(random, count, offset) }

    @GesFunction
    static func spawnSpots(count: Int, margin: Double, spacing: Double, random: GesRandomGenerator) -> GesValue? { .text(draw(random, count, margin + spacing)) }

    @GesFunction
    static func only(_ random: GesRandomGenerator) -> Int { Int(random.nextInclusiveInteger(1, 1000)) }

    @GesFunction(units: ["d": .meter])
    static func four(a: Int, b: Int, random: GesRandomGenerator, c: Int, d: Int) -> Int { 1000 * a + 100 * b + 10 * c + d + Int(random.nextInclusiveInteger(0, 0)) }

    static func draw(_ random: GesRandomGenerator, _ count: Int, _ offset: Double) -> String {
        (0..<count).map { _ in String(describing: Double(random.nextInclusiveInteger(1, 1000)) + offset) }.joined(separator: ",")
    }
}

/// Extensions declared with `@GesExtension`, `@GesFunction` and `@GesPredicate` take their signatures from the declarations
/// and convert arguments and results strictly, with `nothing` for a value that does not convert.
final class ExtensionMacroTests: XCTestCase {
    enum Failure: Error { case deliberate }

    private func run(_ body: String, _ registry: some GesExtensionRegistry, seed: Int64? = nil) throws -> (GameEventScriptMessage?, GesExecutionResult) {
        let program = try GesProgramBuilder().addScript("on Start() {\n" + body + "\n}").compile()
        let builder = GameEventScriptHost.makeBuilder().withExtensionRegistry(registry)
        if let seed { _ = builder.withRandomSeed(seed) }
        let host = try builder.build()
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

    func testRandomGeneratorParametersDrawFromTheHostGenerator() throws {
        let registry = try GesBridgeExtensionRegistry(try ArenaExtensions.gesExtensions())
        let body = "emit Seen(a: :arena.first(count: 2), b: :arena.middle(count: 3, 0.5), c: :arena.spawnSpots(count: 2, margin: 1, spacing: 2), d: :arena.only())"
        let (seen, result) = try run(body, registry, seed: 42)
        XCTAssertEqual(result.state, .completed)
        let arguments = try XCTUnwrap(seen).arguments
        let expected = GesRandomGenerator(seed: 42)
        XCTAssertEqual(arguments[0], .text(ArenaExtensions.draw(expected, 2, 0)), "The first parameter receives the host generator.")
        XCTAssertEqual(arguments[1], .text(ArenaExtensions.draw(expected, 3, 0.5)), "A middle parameter receives the host generator.")
        XCTAssertEqual(arguments[2], .text(ArenaExtensions.draw(expected, 2, 3)), "The last parameter receives the host generator.")
        XCTAssertEqual(arguments[3], .integer(expected.nextInclusiveInteger(1, 1000)), "A generator alone leaves no script parameter.")
        let (again, _) = try run(body, registry, seed: 42)
        XCTAssertEqual(try XCTUnwrap(again).arguments.map { $0 }, arguments.map { $0 }, "The same seed draws the same numbers.")
    }

    func testRandomGeneratorParametersFollowRandomScopes() throws {
        let registry = try GesBridgeExtensionRegistry(try ArenaExtensions.gesExtensions())
        let (seen, result) = try run("emit Seen(a: :arena.first(count: 2), b: random with 7 (:arena.first(count: 2)), c: :arena.first(count: 2), d: nothing)", registry, seed: 42)
        XCTAssertEqual(result.state, .completed)
        let arguments = try XCTUnwrap(seen).arguments
        let outer = GesRandomGenerator(seed: 42)
        XCTAssertEqual(arguments[0], .text(ArenaExtensions.draw(outer, 2, 0)))
        XCTAssertEqual(arguments[1], .text(ArenaExtensions.draw(GesRandomGenerator(seed: 7), 2, 0)), "A seeded scope supplies its stream.")
        XCTAssertEqual(arguments[2], .text(ArenaExtensions.draw(outer, 2, 0)), "The outer stream continues after the scope.")
    }

    func testRandomGeneratorParametersAreNoScriptArguments() throws {
        let registry = try GesBridgeExtensionRegistry(try ArenaExtensions.gesExtensions())
        XCTAssertEqual(
            try ArenaExtensions.gesExtensions().map(\.reference.signatureID),
            ["arena.first(count)", "arena.middle(count,_)", "arena.spawnSpots(count,margin,spacing)", "arena.only()", "arena.four(a,b,c,d)"]
        )
        let (seen, result) = try run(
            "emit Seen(a: :arena.four(a: 1, b: 2, c: 3, d: 4m), b: :arena.four(a: 1, b: 2, c: 3, d: 4), c: :arena.spawnSpots(count: 1, margin: 1m, spacing: 2), d: :arena.first(count: 'x'))",
            registry,
            seed: 1
        )
        XCTAssertEqual(result.state, .completed)
        let arguments = try XCTUnwrap(seen).arguments
        XCTAssertEqual(arguments[0], .integer(1234), "Script arguments around the generator keep their order and units.")
        XCTAssertTrue(arguments[1].isNothing, "The unit of an argument after the generator is checked.")
        XCTAssertTrue(arguments[2].isNothing, "An argument after the generator converts strictly.")
        XCTAssertTrue(arguments[3].isNothing)
    }
}
