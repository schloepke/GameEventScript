// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import GameEventScriptRuntime
import XCTest

/// Code generated from conformance/fixtures/SchemaGeneration/geometry.gess: Boxes and Polys in wire records, where a Poly is a
/// reusable array of Points with a polygon view, and in models, which hold GesBox and GesPoly.
final class GeneratedGeometryCodeTests: XCTestCase {
    private func makeZone() -> GeometryWire.Zone {
        let zone = GeometryWire.Zone()
        zone.id = 7
        zone.area = GesBox(GesPoint(x: 20, y: 3), GesPoint(x: 5, y: 1))
        zone.boundsOptional = GesBox(GesPoint(x: -1.5, y: 0, z: 2), GesPoint(x: 4, y: 8, z: -2))
        zone.pick = .box
        zone.pickBox = GesBox(GesPoint(x: 0, y: 0), GesPoint(x: 1, y: 2))
        zone.outline = [GesPoint(x: 0, y: 0), GesPoint(x: 10, y: 0), GesPoint(x: 0, y: 2)]
        zone.free = [GesPoint(x: 0.5, y: -0.25), GesPoint(x: 3, y: 1)]
        let ring = GeometryWire.Zone.RingsItem()
        ring.value = [GesPoint(x: 1, y: 1), GesPoint(x: 2, y: 2)]
        zone.rings = [ring]
        return zone
    }

    func testBoxesAndPolysRoundTripThroughBytesAndValues() throws {
        let zone = makeZone()
        XCTAssertEqual(GesPoint(x: 5, y: 1), zone.area.min, "A box orders its corners.")
        let bytes = try zone.encoded()
        let read = GeometryWire.Zone()
        _ = try read.read(from: bytes)
        XCTAssertTrue(try read.wireEquals(zone))
        XCTAssertEqual(zone.area, read.area)
        XCTAssertEqual(zone.boundsOptional, read.boundsOptional)
        XCTAssertEqual(zone.pickBox, read.pickBox)
        XCTAssertEqual(zone.outline, read.outline)
        XCTAssertEqual(GesPoly(zone.outline), read.outlinePoly)
        XCTAssertEqual(10, read.outlinePoly.area, "The view is a polygon of the points.")

        // The value path holds the geometry library's external values, which the wire record and the model convert alike.
        let value = try read.toValue()
        let fields = value.asMap!
        XCTAssertEqual(zone.area, GesBox(fields.get("area")!, unit: .meter))
        XCTAssertEqual(GesPoly(zone.free), GesPoly(fields.get("free")!))
        let copy = GeometryWire.Zone()
        try copy.read(value: value)
        XCTAssertEqual(bytes, try copy.encoded())

        let model = try GeometryModel.Zone(value: value)
        XCTAssertEqual(zone.area, model.area)
        XCTAssertEqual(zone.boundsOptional, model.bounds)
        XCTAssertEqual(GesPoly(zone.outline), model.outline)
        XCTAssertEqual(GesPoly(zone.rings[0].value), model.rings[0])
        XCTAssertEqual(try value.json(), try model.toValue().json())
    }

    func testBoxDeltasChangeOneCorner() throws {
        let baseline = makeZone()
        let next = makeZone()
        next.area = GesBox(next.area.min, GesPoint(x: 22, y: 3))
        let delta = try next.deltaEncoded(against: baseline)
        let read = GeometryWire.Zone()
        _ = try read.read(from: try baseline.encoded())
        _ = try read.readDelta(from: delta, against: baseline)
        XCTAssertEqual(next.area, read.area)
        XCTAssertTrue(try read.wireEquals(next))
    }

    private let zoneDocument = """
        :Schema(:Zone)
        id: 7
        area: [lower: :Point(20m, 1m), upper: :Point(5m, 3m)]
        outline: [:Point(0m, 0m), :Point(10m, 0m), :Point(0m, 2m)]
        free: [:Point(0.5, -0.25)]
        rings: [[:Point(1m, 1m), :Point(2m, 2m)]]

        """

    func testModelsReadTheirDocuments() throws {
        let zone = try GeometryModel.Zone(document: zoneDocument)
        XCTAssertEqual(zone.id, 7)
        XCTAssertEqual(zone.area, GesBox(GesPoint(x: 5, y: 1), GesPoint(x: 20, y: 3)))
        XCTAssertNil(zone.bounds)
        XCTAssertEqual(zone.outline.area, 10)
        XCTAssertEqual(zone.rings[0], GesPoly([GesPoint(x: 1, y: 1), GesPoint(x: 2, y: 2)]))

        // The document type chooses the root by the header.
        XCTAssertEqual(try GeometryModel.Document(document: zoneDocument), .zone(zone))
    }

    private func documentError(_ text: String, file: StaticString = #filePath, line: UInt = #line, _ read: (String) throws(GesDocumentError) -> Void) -> GesDocumentError? {
        do {
            try read(text)
            XCTFail("expected a document error", file: file, line: line)
            return nil
        } catch {
            return error
        }
    }

    func testDocumentErrorsHaveTheirPositionAndPath() {
        let other = documentError(zoneDocument.replacingOccurrences(of: ":Schema(:Zone)", with: ":Schema(:Area)")) { text throws(GesDocumentError) in _ = try GeometryModel.Zone(document: text) }
        XCTAssertEqual(other?.code, GesSchemaDocument.schemaMismatch)
        XCTAssertEqual(other.map { [$0.line, $0.column] }, [1, 1])
        XCTAssertTrue(other?.message.contains(":Area") == true && other?.message.contains(":Zone") == true)
        let range = documentError(zoneDocument.replacingOccurrences(of: "id: 7", with: "id: 300")) { text throws(GesDocumentError) in _ = try GeometryModel.Document(document: text) }
        XCTAssertEqual(range.map { [$0.code, "\($0.line)", "\($0.column)", $0.path] }, ["document.outOfRange", "2", "5", "id"])
        let unit = documentError(zoneDocument.replacingOccurrences(of: "[:Point(1m, 1m),", with: "[:Point(1s, 1s),")) { text throws(GesDocumentError) in _ = try GeometryModel.Zone(document: text) }
        XCTAssertEqual(unit.map { [$0.code, "\($0.line)", "\($0.column)", $0.path] }, ["document.fieldType", "6", "10", "rings[][]"])
    }

    func testPolyPointsOutsideThePlaneOrBeyondMaxCannotBeWritten() throws {
        var zone = makeZone()
        zone.free[0] = GesPoint(x: 0, y: 0, z: 1)
        XCTAssertThrowsError(try zone.encoded()) { error in
            XCTAssertEqual((error as? GesWireError)?.code, GesWireError.typeMismatch)
            XCTAssertEqual((error as? GesWireError)?.path, "free[]")
        }
        zone = makeZone()
        zone.outline.append(GesPoint(x: 1, y: 1))
        XCTAssertThrowsError(try zone.encoded()) { error in
            XCTAssertEqual((error as? GesWireError)?.code, GesWireError.tooLong)
            XCTAssertEqual((error as? GesWireError)?.path, "outline")
        }
    }
}
