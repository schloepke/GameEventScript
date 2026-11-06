// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import GameEventScriptRuntime
import XCTest

/// Handler and Series values have no Conformance transport encoding, so their document roundtrip is pinned natively.
final class DocumentRoundTripTests: XCTestCase {
    private let values =
        "[handler: :Handler(Done(value, _)), series: :Series(fibonacci, offset: 3), "
        + "range: :Range(from: 0.5, to: 2.5, step: 0.5), signal: :Message(Done(power: 1kW, note: 'line\nbreak') with #ready), "
        + "nested: [[:Series(factorial, offset: 1), :Handler(Ping())], [deep: :Range(from: 1, to: 9, step: 2)]]]"

    func testDocumentsWithHandlerSeriesRangeAndMessageValuesReadBack() throws {
        let document = try GesDocument(GesValue(literal: values))
        for width in [1, 20, 1000] {
            let printed = document.text(format: GesDocumentFormat(lineWidth: width))
            XCTAssertEqual(document, try GesDocument(parsing: printed), printed)
        }
    }

    func testFormattedValuesReadBackWithLiteralParsingInEveryLayout() throws {
        let value = try GesValue(literal: values)
        for width in [1, 20, 1000] {
            let text = try value.formatted(GesDocumentFormat(indentation: 4, lineWidth: width))
            XCTAssertEqual(value, try GesValue(strictLiteral: text), text)
        }
        XCTAssertEqual("[1kW, 250ms]", try GesValue(literal: "[1000W, 0.25s]").formatted())
        // Prefixes keep the unit symbol, never its ASCII spelling; units without prefixes print their symbol.
        XCTAssertEqual(
            "[3cm², 2t, 14mW/m², 2.5kW/m², 3km², 4mW, 2m³, 6dB, 3Hz, 2.5kW/K, 250mW/K, 4MJ/K, 3J/K]",
            try GesValue(literal: "[0.0003m², 2000kg, 0.014W/m², 2500W/m², 3000000m², 0.004W, 2m³, 6dB, 3Hz, 2500W/K, 0.25W/K, 4000000J/K, 3J/K]").formatted()
        )
    }

    func testValuesWithoutReadableLiteralTextAreRejected() throws {
        for unreadable in [GesValue.dice([0]), .record(typeName: "lower", keys: ["damage"], values: [.integer(5)]), .handler(try GesMessageSignature(name: "undeliverable", parameters: ["message"]))] {
            let root = GesValue.map(keys: ["v"], values: [unreadable])
            XCTAssertThrowsError(try GesDocument(root))
            XCTAssertThrowsError(try root.formatted())
        }
        let recordRoot = GesValue.record(typeName: "Unit", keys: ["two words"], values: [.integer(1)])
        XCTAssertThrowsError(try GesDocument(recordRoot))
        XCTAssertEqual("[v: :Record(\"Unit\", [\"two words\": 1])]", try GesValue.map(keys: ["v"], values: [recordRoot]).formatted())
    }

    func testExtremeLayoutOptionsStayValid() throws {
        let value = try GesValue(literal: "[a: 1, b: [2, 3]]")
        XCTAssertEqual("[a: 1, b: [2, 3]]", try value.formatted(GesDocumentFormat(lineWidth: .max)))
        let deep = try GesValue(literal: "[[[[['a long text that cannot fit']]]]]")
        for format in [GesDocumentFormat(indentation: .max, lineWidth: 1), GesDocumentFormat(indentation: .min, lineWidth: .min)] {
            XCTAssertEqual(deep, try GesValue(strictLiteral: try deep.formatted(format)))
        }
    }
}
