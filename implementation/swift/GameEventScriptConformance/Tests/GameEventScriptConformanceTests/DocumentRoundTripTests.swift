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
    }

    func testValuesWithoutReadableLiteralTextAreRejected() {
        for unreadable in [GesValue.dice([0]), .record(typeName: "lower", keys: ["damage"], values: [.integer(5)])] {
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
