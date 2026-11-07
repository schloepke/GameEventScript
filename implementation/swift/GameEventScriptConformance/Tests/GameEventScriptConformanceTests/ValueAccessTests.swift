// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import ConformanceInstrumentation
import GameEventScriptRuntime
import XCTest

/// The Swift spellings of entries, member reads and traversal that the value-based corpus cannot reach.
final class ValueAccessTests: XCTestCase {
    func testEntriesAreWrittenAsTupleLiterals() {
        let map: GesValue = .map([("b", .integer(2)), ("a", .integer(1)), ("b", .integer(3))])
        XCTAssertEqual(map, .map(keys: ["a", "b"], values: [.integer(1), .integer(3)]))
        XCTAssertEqual(GesValue.map([]), .map(keys: [], values: []))
        XCTAssertEqual(GesValue.record(typeName: "Hit", [(key: "a", value: .integer(1))]), .record(typeName: "Hit", keys: ["a"], values: [.integer(1)]))
    }

    func testSortedDictionaryEntriesCanBePassedDirectly() {
        let source: [String: GesValue] = ["a": .integer(1)]
        XCTAssertEqual(GesValue.map(source.sorted { $0.key < $1.key }), .map(keys: ["a"], values: [.integer(1)]))
    }

    func testTraversalDestructuresEntriesInOrder() {
        let map = GesValue.map([("b", .integer(2)), ("a", .integer(1))]).asMap!
        var seen: [String] = []
        for (key, value) in map { seen.append("\(key)\(value.asInteger)") }
        XCTAssertEqual(seen, ["a1", "b2"])
        XCTAssertEqual(map.underestimatedCount, 2)
        XCTAssertEqual(map.map(\.key), ["a", "b"])
    }

    func testBothFailureKindsAreFatalRuntimeErrors() throws {
        let value = GesValue.external(try Failing())
        for (name, code) in [("plain", "runtime.externalFieldAccessFailed"), ("fault", "probe.fault")] {
            XCTAssertThrowsError(try value.readField(name)) { error in
                XCTAssertEqual((error as? any GesFatalRuntimeError)?.diagnostic.code, code)
            }
        }
    }

    func testTraversalAndMemberReadsAllocateNothing() throws {
        #if !canImport(Darwin)
            throw XCTSkip("The allocation counter uses libmalloc.")
        #else
            guard ges_allocation_install(), ges_allocation_controls() else { throw XCTSkip("The allocation counter is not available.") }
            let value = GesValue.record(typeName: "Unit", [("hp", .integer(5)), ("position", .point(x: 1, y: 2, z: 3))])
            let map = value.asMap!

            func round() -> Int {
                var total = 0
                for (key, _) in map { total += key.utf8.count }
                total += Int(value["hp"].asInteger) + Int(value["position"]["y"].asNumber)
                return value["missing"]["x"].isNothing ? total : 0
            }

            XCTAssertEqual(round(), 17)
            XCTAssertTrue(ges_allocation_begin())
            for _ in 0..<1000 { _ = round() }
            let sample = ges_allocation_end()
            XCTAssertTrue(sample.valid)
            XCTAssertEqual(sample.allocations, 0)
            XCTAssertEqual(sample.bytes, 0)
        #endif
    }

    func testExternalFieldReadsAllocateNothing() throws {
        #if !canImport(Darwin)
            throw XCTSkip("The allocation counter uses libmalloc.")
        #else
            guard ges_allocation_install(), ges_allocation_controls() else { throw XCTSkip("The allocation counter is not available.") }
            let host = try Fixed()
            let value = GesValue.external(host)
            XCTAssertTrue(value.externalValue === host)

            func round() throws -> Double { try value.readField("y")!.asNumber + (value.externalValue as! Fixed).y }

            XCTAssertEqual(try round(), 4)
            XCTAssertTrue(ges_allocation_begin())
            for _ in 0..<1000 { _ = try round() }
            let sample = ges_allocation_end()
            XCTAssertTrue(sample.valid)
            XCTAssertEqual(sample.allocations, 0)
            XCTAssertEqual(sample.bytes, 0)
        #endif
    }

    private struct Plain: Error {}

    private final class Failing: GesExternalValue {
        let definition: GesExternalTypeDefinition

        init() throws {
            definition = try GesExternalTypeDefinition(name: "Failing", fields: [.init(name: "plain", kind: .float), .init(name: "fault", kind: .float)], constructors: [])
        }

        func field(_ name: String) throws -> GesValue? {
            if name == "fault" { throw try GesExtensionFaultError(code: "probe.fault", message: "Configured") }
            throw Plain()
        }
    }

    private final class Fixed: GesExternalValue {
        let y = 2.0
        let definition: GesExternalTypeDefinition

        init() throws {
            definition = try GesExternalTypeDefinition(name: "Fixed", fields: [.init(name: "x", kind: .float), .init(name: "y", kind: .float)], constructors: [])
        }

        func field(_ name: String) throws -> GesValue? { .float(2) }
    }
}
