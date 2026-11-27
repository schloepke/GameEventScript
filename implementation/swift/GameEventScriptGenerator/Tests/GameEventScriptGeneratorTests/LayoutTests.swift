// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import GameEventScriptGenerator
import GameEventScriptRuntime
import XCTest

/// Checks the layout model that the shared corpus cannot reach: deduplication, dependencies and network owners.
final class LayoutTests: XCTestCase {
    func testSharedTypesAppearOnceWithTheirDependencies() throws {
        let set = try GesSchemaSet(
            parsing: """
                parameter size: :Quantity(m)

                schema :Cell as {
                  x: :Quantity(m) between 0m and size step 1m
                }

                schema :A as {
                  cell: :Cell
                }

                schema :B as {
                  cells: :List(:Cell) max 2
                }
                """
        )
        let parameters = GesSchemaParameters()
        try parameters.with("size", literal: "10m")
        let text = try GesSchemaGenerator.layout(set, roots: ["A", "B"], parameters: parameters)
        XCTAssertEqual(text.components(separatedBy: "\"name\": \"Cell\",\n      \"kind\"").count, 2)
        XCTAssertTrue(text.contains("\"maxByteCountDepends\": [\n        \"size\"\n      ]"))
        XCTAssertThrowsError(try GesSchemaGenerator.layout(set, roots: [], parameters: parameters)) { XCTAssertEqual(($0 as? GesGeneratorError)?.code, "generate.usage") }
    }

    func testOwnersCarryTheirProjectionAndSendingState() throws {
        let set = try GesSchemaSet(
            parsing: """
                schema :Composition as { name: :Text max 8, mass: :Number integer between 0 and 9 }
                projection :UnitPublic of :Composition as { name }
                handle @Unit of :Composition as { id: :Text max 8, version: :ContentHash }
                handle @Drone of :Composition as { id: :Number integer between 0 and 9, version: :ContentHash }
                wire schema :World as { unit: @Unit(:UnitPublic), drone: @Drone }
                network :Game as {
                  server publishes :World latest
                  server owns @Unit as :UnitPublic sending state
                  server owns @Drone
                }
                """
        )
        let text = try GesSchemaGenerator.layout(set, roots: ["Game"], parameters: GesSchemaParameters())
        XCTAssertTrue(text.contains("\"handle\": \"Drone\"\n"))
        XCTAssertTrue(text.contains("\"handle\": \"Unit\",\n          \"projection\": \"UnitPublic\",\n          \"sendingState\": true\n"))
    }

    func testTheLayoutIsCanonicalJson() throws {
        let set = try GesSchemaSet(parsing: "schema :S as {\n  on: :Boolean\n}\n")
        let text = try GesSchemaGenerator.layout(set, roots: ["S", "S"], parameters: GesSchemaParameters())
        XCTAssertTrue(text.hasPrefix("{\n  \"layout\": 1,\n  \"roots\": [\n    \"S\"\n  ],\n  \"parameters\": [],\n"))
        XCTAssertTrue(text.hasSuffix("}\n"))
        XCTAssertTrue(text.contains("\"encoding\": {\n            \"kind\": \"boolean\"\n          }"))
    }
}
