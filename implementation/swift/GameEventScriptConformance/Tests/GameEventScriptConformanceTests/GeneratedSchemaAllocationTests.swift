// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import ConformanceInstrumentation
import GameEventScriptRuntime
import XCTest

/// Code generated from conformance/fixtures/SchemaGeneration/fleet.gess writes and reads records and deltas without
/// allocating once its storage has grown.
final class GeneratedSchemaAllocationTests: XCTestCase {
    func testGeneratedRecordsAllocateNothing() throws {
        #if !canImport(Darwin)
            throw XCTSkip("The allocation counter uses libmalloc.")
        #else
            guard ges_allocation_install(), ges_allocation_controls() else { throw XCTSkip("The allocation counter is not available.") }
            let name = Array("frigate".utf8)
            let fleet = Generated.Fleet()
            fleet.tick = 1
            for index in 0..<48 {
                let ship = Generated.Ship()
                ship.id = Int64(index * 2)
                ship.health = 50
                ship.pose.x = Double(index) * 10.5
                ship.pose.y = 100
                ship.nameUTF8 = name
                fleet.ships.append(ship)
            }
            let spare = fleet.ships[47]
            let baseline = Generated.Fleet()
            baseline.copy(from: fleet)
            let received = Generated.Fleet()
            let applied = Generated.Fleet()
            let record = UnsafeMutableRawBufferPointer.allocate(byteCount: Generated.Fleet.maxByteCount, alignment: 8)
            let delta = UnsafeMutableRawBufferPointer.allocate(byteCount: Generated.Fleet.maxByteCount, alignment: 8)
            defer {
                record.deallocate()
                delta.deallocate()
            }
            var sink = 0

            // Each round changes a ship, removes the last one or adds it again, writes and reads the full record and a delta.
            func round(_ round: Int) throws {
                fleet.tick = (fleet.tick + 1) % 65536
                let ship = fleet.ships[round % 40]
                ship.health = (ship.health + 1) % 101
                ship.pose.heading = Double(round % 720) * 0.5
                ship.hasTarget = round % 2 == 0
                ship.target = Int64(round % 65536)
                if round % 2 == 0 { fleet.ships.removeLast() } else { fleet.ships.append(spare) }
                let length = try fleet.write(into: record)
                sink &+= try received.read(from: UnsafeRawBufferPointer(rebasing: record[0..<length]))
                let deltaLength = try fleet.writeDelta(against: baseline, into: delta)
                sink &+= try applied.readDelta(from: UnsafeRawBufferPointer(rebasing: delta[0..<deltaLength]), against: baseline)
                baseline.copy(from: applied)
            }

            for index in 0..<100 { try round(index) }
            XCTAssertTrue(ges_allocation_begin())
            for index in 100..<1100 { try round(index) }
            let sample = ges_allocation_end()
            XCTAssertTrue(sample.valid)
            XCTAssertEqual(sample.allocations, 0)
            XCTAssertEqual(sample.bytes, 0)
            XCTAssertTrue(try received.wireEquals(fleet))
            XCTAssertNotEqual(sink, 0)
        #endif
    }
}
