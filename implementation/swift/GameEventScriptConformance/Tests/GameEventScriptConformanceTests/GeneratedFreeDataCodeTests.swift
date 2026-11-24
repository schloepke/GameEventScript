// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import Foundation
import GameEventScriptRuntime
import XCTest

/// Code generated from conformance/fixtures/SchemaGeneration/freedata.gess: models that hold Maps and Lists without an
/// element type as GES values, with the empty defaults.
final class GeneratedFreeDataCodeTests: XCTestCase {
    private func program(_ fields: (String, GesValue)...) -> GesValue {
        var all: [(key: String, value: GesValue)] = [("script", .text("run")), ("layers", .list([])), ("pick", .text("first"))]
        for field in fields {
            all.removeAll { $0.key == field.0 }
            all.append((field.0, field.1))
        }
        return .record(typeName: "Program", all)
    }

    /// Free data with a nested map, a list holding mixed items and a point.
    private func data() -> GesDataMap {
        GesDataMap(
            value: .map([
                ("hp", .integer(3)),
                ("inner", .map([("deep", .boolean(true))])),
                ("items", .list([.integer(1), .text("two")])),
                ("pos", .point(x: 1, y: 2)),
            ])
        )!
    }

    private let items: [GesValue] = [.integer(1), .text("two"), .point(x: 0, y: 1), .record(typeName: "Other", [("a", .integer(2))])]

    private func mismatch(_ value: GesValue, file: StaticString = #filePath, line: UInt = #line) -> String? {
        do {
            _ = try FreeDataModel.Program(value: value)
            XCTFail("expected a type mismatch", file: file, line: line)
            return nil
        } catch {
            XCTAssertEqual(error.code, GesWireError.typeMismatch, file: file, line: line)
            return error.path
        }
    }

    func testInitializerTakesTheEmptyDefaults() {
        let program = FreeDataModel.Program(script: "run", pick: .text("first"))
        XCTAssertEqual(program.data, .empty)
        XCTAssertTrue(program.args.isEmpty)
        XCTAssertTrue(program.steps.isEmpty)
        XCTAssertTrue(FreeDataModel.Scene(name: "stage").programs.isEmpty)
    }

    func testMapsAndUntypedListsReadAndRoundTrip() throws {
        let program = try FreeDataModel.Program(
            value: program(
                ("data", data().toValue()),
                ("args", .list(items)),
                ("extra", .map([("note", .text("x"))])),
                ("layers", .list([.map([("z", .integer(1))]), .map([(key: String, value: GesValue)]())])),
                ("pick", .map([("p", .nothing)]))
            )
        )
        XCTAssertEqual(program.data, data())
        XCTAssertEqual(program.data.toValue()["inner"]["deep"], .boolean(true))
        XCTAssertEqual(program.data.get("pos"), .point(x: 1, y: 2))
        XCTAssertEqual(program.args.asList, items)
        XCTAssertEqual(program.extra?.get("note"), .text("x"))
        XCTAssertEqual(program.layers.count, 2)
        guard case .map = program.pick else { return XCTFail("expected the Map alternative") }

        var again = try FreeDataModel.Program(value: program.toValue())
        XCTAssertEqual(again, program)
        XCTAssertEqual(again.hashValue, program.hashValue)
        again.data = GesDataMap(value: .map([("hp", .integer(4))]))!
        XCTAssertNotEqual(again, program)
    }

    func testLeftOutDataTakesTheEmptyDefault() throws {
        let program = try FreeDataModel.Program(value: program())
        XCTAssertEqual(program.data, .empty)
        XCTAssertTrue(program.args.isEmpty)
        XCTAssertTrue(program.steps.isEmpty)
        XCTAssertNil(program.extra)
        let scene = try FreeDataModel.Scene(value: .record(typeName: "Scene", [("name", .text("stage"))]))
        XCTAssertTrue(scene.programs.isEmpty)
        XCTAssertNil(scene.notes)
    }

    func testOtherValuesAreTypeMismatches() {
        XCTAssertEqual(mismatch(program(("data", .text("x")))), "data")
        XCTAssertEqual(mismatch(program(("data", .list([])))), "data")
        // A Record is no Map, though both hold fields.
        XCTAssertEqual(mismatch(program(("data", .record(typeName: "Other", [("a", .integer(1))])))), "data")
        XCTAssertEqual(mismatch(program(("args", .map([(key: String, value: GesValue)]())))), "args")
        XCTAssertEqual(mismatch(program(("layers", .list([.integer(1)])))), "layers[]")
        XCTAssertEqual(mismatch(program(("pick", .integer(1)))), "pick")
    }

    func testExternalValuesAreNoFreeData() {
        let external = FreeDataModel.Scene(name: "stage").gesValue()
        XCTAssertEqual(mismatch(program(("data", .map([("held", .list([external]))])))), "data")
        XCTAssertEqual(mismatch(program(("args", .list([external])))), "args")
    }

    func testDataTypesHoldNoExternalValues() throws {
        let external = FreeDataModel.Scene(name: "stage").gesValue()
        let held = GesValue.map([("held", .list([external]))])
        XCTAssertThrowsError(try GesDataMap(held.asMap!))
        XCTAssertThrowsError(try GesDataList([.integer(1), held]))
        XCTAssertNil(GesDataMap(value: held))
        XCTAssertNil(GesDataList(value: .list([external])))
        // A Record is no plain Map, though its fields may become one.
        let record = GesValue.record(typeName: "Other", [("a", .integer(1))])
        XCTAssertNil(GesDataMap(value: record))
        XCTAssertEqual(try GesDataMap(record.asMap!), GesDataMap(value: .map([("a", .integer(1))])))
        XCTAssertEqual(GesDataMap.empty.toValue(), .map([(key: String, value: GesValue)]()))
        XCTAssertEqual(GesDataList.empty.toValue(), .list([]))
        XCTAssertEqual(try GesDataList([.integer(1)]).description, "[1]")
    }

    func testDeeplyNestedDataIsCheckedWithoutRecursion() {
        // Deeper than a recursive check could descend on a 512 KB thread stack, the stack of Swift concurrency threads;
        // the bottom decides. The values are built and released on the test thread, whose stack their release needs.
        final class Outcome: @unchecked Sendable {
            var values: [GesValue] = []
            var accepted: [Bool] = []
        }

        func nested(_ bottom: GesValue) -> GesValue {
            var value = bottom
            for _ in 0..<20_000 { value = .list([value]) }
            return value
        }

        let outcome = Outcome()
        outcome.values = [nested(.integer(1)), nested(FreeDataModel.Scene(name: "stage").gesValue())]
        let done = DispatchSemaphore(value: 0)
        let thread = Thread {
            outcome.accepted = outcome.values.map { (try? GesDataList([$0])) != nil }
            done.signal()
        }
        thread.stackSize = 512 * 1024
        thread.start()
        done.wait()
        XCTAssertEqual(outcome.accepted, [true, false])
    }

    private let programDocument = """
        :Schema(:Program)
        script: "run"
        layers: [[z: 1]]
        pick: "first"
        data: [hp: 3, inner: [deep: true], items: [1, "two"], pos: :Point(1, 2)]
        args: [1, "two"]

        """

    func testModelsReadTheirDocuments() throws {
        let program = try FreeDataModel.Program(document: programDocument)
        XCTAssertEqual(program.data, data())
        XCTAssertEqual(Array(program.args), [.integer(1), .text("two")])
        XCTAssertTrue(program.steps.isEmpty)
        let changed = programDocument.replacingOccurrences(of: #"data: [hp: 3, inner: [deep: true], items: [1, "two"], pos: :Point(1, 2)]"#, with: "data: [1, 2]")
        do {
            _ = try FreeDataModel.Program(document: changed)
            XCTFail("expected a document error")
        } catch {
            XCTAssertEqual([error.code, "\(error.line)", "\(error.column)", error.path], ["document.fieldType", "5", "7", "data"])
        }
    }

    /// Codable goes over the value-object JSON form of the free data.
    func testCodableRoundTripsFreeData() throws {
        let program = FreeDataModel.Program(script: "run", extra: data(), pick: .map(data()), data: data(), args: try GesDataList(items), steps: [1, 2])
        let scene = FreeDataModel.Scene(name: "stage", notes: try GesDataList(items), programs: [program])
        let decoded = try JSONDecoder().decode(FreeDataModel.Scene.self, from: JSONEncoder().encode(scene))
        XCTAssertEqual(decoded, scene)
        XCTAssertEqual(try JSONDecoder().decode(GesDataMap.self, from: JSONEncoder().encode(data())), data())
        XCTAssertEqual(try JSONDecoder().decode(GesValue.self, from: JSONEncoder().encode(data())), data().toValue())
        XCTAssertEqual(try JSONDecoder().decode(GesDataList.self, from: JSONEncoder().encode(GesValue.list(items))).asList, items)
        XCTAssertThrowsError(try JSONDecoder().decode(GesDataMap.self, from: JSONEncoder().encode(GesValue.list(items))))
        XCTAssertThrowsError(try JSONDecoder().decode(GesDataList.self, from: JSONEncoder().encode(data())))
    }

    /// Numbers the value JSON rejects are corrupted data with the codec's error underneath, as every other rejection.
    func testCodableRejectsNumbersWithoutInt64Form() {
        for number in ["1.5", "-0.25", "9223372036854775808"] {
            let json = Data(#"{"type":"Text","value":\#(number)}"#.utf8)
            XCTAssertThrowsError(try JSONDecoder().decode(GesValue.self, from: json), number) { error in
                guard case .dataCorrupted(let context) = error as? DecodingError else { return XCTFail("\(number): \(error)") }
                XCTAssertEqual((context.underlyingError as? GesMessageFormatError)?.code, "message.invalidJson", number)
            }
        }
    }
}
