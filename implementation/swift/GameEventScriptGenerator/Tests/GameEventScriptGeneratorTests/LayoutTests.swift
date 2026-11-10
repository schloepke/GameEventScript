// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import GameEventScriptGenerator
import GameEventScriptRuntime
import XCTest

/// Checks the layout model that the shared corpus cannot reach: deduplication and dependencies.
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

    func testTheLayoutIsCanonicalJson() throws {
        let set = try GesSchemaSet(parsing: "schema :S as {\n  on: :Boolean\n}\n")
        let text = try GesSchemaGenerator.layout(set, roots: ["S", "S"], parameters: GesSchemaParameters())
        XCTAssertTrue(text.hasPrefix("{\n  \"layout\": 1,\n  \"roots\": [\n    \"S\"\n  ],\n  \"parameters\": [],\n"))
        XCTAssertTrue(text.hasSuffix("}\n"))
        XCTAssertTrue(text.contains("\"encoding\": {\n            \"kind\": \"boolean\"\n          }"))
    }
}
