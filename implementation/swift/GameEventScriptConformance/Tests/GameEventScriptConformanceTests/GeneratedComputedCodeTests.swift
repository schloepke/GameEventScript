// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import GameEventScriptRuntime
import XCTest

/// Code generated from conformance/fixtures/SchemaGeneration/computed.gess: computed fields in plain binary64 and Boolean
/// code where binding proves the bits of the virtual machine, the dynamic form where it cannot, and both agree with the value
/// path of the runtime.
final class GeneratedComputedCodeTests: XCTestCase {
    private func reactor(_ energy: Double, _ duration: Double, _ trim: Double?, _ ticks: Int64, _ draws: Double...) -> ComputedWire.Reactor {
        let reactor = ComputedWire.Reactor()
        reactor.energy = energy
        reactor.duration = duration
        reactor.boost = 0.25
        reactor.trimOptional = trim
        reactor.ticks = ticks
        for draw in draws {
            var cell = ComputedWire.Cell()
            cell.draw = draw
            reactor.cells.append(cell)
        }
        return reactor
    }

    private func assertNumber(_ expected: GesValue?, _ actual: Double?, _ name: String, file: StaticString = #filePath, line: UInt = #line) {
        guard let expected, !expected.isNothing else {
            XCTAssertTrue(actual == nil || actual!.isNaN, "\(name): expected nothing, got \(String(describing: actual))", file: file, line: line)
            return
        }
        XCTAssertEqual(expected.asNumber.bitPattern, actual?.bitPattern, "\(name): expected \(expected.asNumber), got \(String(describing: actual))", file: file, line: line)
    }

    func testStaticAndDynamicFieldsGiveTheBitsOfTheValuePath() throws {
        let reactors = [
            reactor(900_000, 0.3, 2.5, 12, 100, 250),
            reactor(0, 0, nil, -7),
            reactor(1, 3, -10, Int64.max / 4, 5000, 5000, 5000),
            reactor(1_000_000, 60, 1, 1 << 53, 1),
            reactor(250, 0.001, 0.5, 0),
        ]
        for reactor in reactors {
            let value = try reactor.toValue()
            let fields = value.asMap!
            assertNumber(fields.get("power"), reactor.power, "power")
            assertNumber(fields.get("boosted"), reactor.boosted, "boosted")
            assertNumber(fields.get("level"), reactor.level, "level")
            assertNumber(fields.get("trimmed"), reactor.trimmed, "trimmed")
            assertNumber(fields.get("load"), reactor.load, "load")
            assertNumber(fields.get("cellCount"), reactor.cellCount, "cellCount")
            assertNumber(fields.get("doubled"), reactor.doubled, "doubled")
            XCTAssertEqual(fields.get("marked")?.asBoolean, reactor.marked, "marked")
            XCTAssertEqual(fields.get("steady")?.asBoolean, reactor.steady, "steady")
            XCTAssertEqual(fields.get("balanced")?.asBoolean, reactor.balanced, "balanced")

            // A model computes the same values from the same fields and converts them back unchanged.
            let model = try ComputedModel.Reactor(value: value)
            XCTAssertEqual(model.power.bitPattern, reactor.power.bitPattern)
            XCTAssertEqual(model.steady, reactor.steady)
            XCTAssertEqual(model.toValue(), value)
        }
    }
}
