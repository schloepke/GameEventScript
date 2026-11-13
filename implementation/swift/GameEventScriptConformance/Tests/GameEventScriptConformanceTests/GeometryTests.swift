// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import Foundation
import GameEventScriptRuntime
import XCTest

/// The native spatial and shape values; the C# tests use the same inputs and results.
final class GeometryTests: XCTestCase {
    func testEqualityIsBinary64Equality() {
        XCTAssertEqual(GesPoint(x: 0, y: 1, z: 2), GesPoint(x: -0.0, y: 1, z: 2))
        XCTAssertNotEqual(GesPoint(x: .nan, y: 0), GesPoint(x: .nan, y: 0), "A NaN component is equal to nothing, as in C#.")
        XCTAssertNotEqual(GesVector(x: 0, y: .nan), GesVector(x: 0, y: .nan))
    }

    func testPointsAndVectorsFollowTheLanguage() {
        let start = GesPoint(x: 1, y: 2)
        let end = GesPoint(x: 4, y: 6)
        let step = end - start
        XCTAssertEqual(step, GesVector(x: 3, y: 4))
        XCTAssertEqual(step.length, 5)
        XCTAssertEqual(step.lengthSquared, 25)
        XCTAssertEqual(start.distance(to: end), 5)
        XCTAssertEqual(start + step, end)
        XCTAssertEqual(end - step, start)
        XCTAssertEqual(step.normalized, GesVector(x: 0.6, y: 0.8))
        XCTAssertNil(GesVector.zero.normalized)
        XCTAssertEqual(step * 2, GesVector(x: 6, y: 8))
        XCTAssertEqual(-step, GesVector(x: -3, y: -4))
        XCTAssertEqual(step / 2, GesVector(x: 1.5, y: 2))
        XCTAssertEqual(step.dot(GesVector(x: 1, y: 2)), 11)
        XCTAssertEqual(step.cross(GesVector(x: 1, y: 2)), GesVector(x: 0, y: 0, z: 2))
        XCTAssertEqual(GesPoint(x: 0, y: -0.0), GesPoint.zero)
        XCTAssertEqual(GesPoint(x: 0, y: -0.0).hashValue, GesPoint.zero.hashValue)
        XCTAssertFalse(GesVector(x: .nan, y: 0).isFinite)
    }

    func testCompoundAssignmentsEqualTheirBinaryForms() {
        let start = GesPoint(x: 1, y: 2, z: 3)
        let step = GesVector(x: 4, y: -6, z: 0.5)
        var point = start
        point += step
        XCTAssertEqual(point, start + step)
        point -= step
        XCTAssertEqual(point, start + step - step)
        var vector = step
        vector += GesVector(x: 1, y: 1, z: 1)
        XCTAssertEqual(vector, step + GesVector(x: 1, y: 1, z: 1))
        vector -= GesVector(x: 1, y: 1, z: 1)
        XCTAssertEqual(vector, step + GesVector(x: 1, y: 1, z: 1) - GesVector(x: 1, y: 1, z: 1))
        vector *= 2
        XCTAssertEqual(vector, step * 2)
        vector /= 2
        XCTAssertEqual(vector, step * 2 / 2)
    }

    func testBoxesExposeComponentBoundsAndASize() {
        let box = GesBox(GesPoint(x: 10, y: -2, z: 3), GesPoint(x: 0, y: 4, z: -1))
        XCTAssertEqual(box.minX, 0)
        XCTAssertEqual(box.maxX, 10)
        XCTAssertEqual(box.minY, -2)
        XCTAssertEqual(box.maxY, 4)
        XCTAssertEqual(box.minZ, -1)
        XCTAssertEqual(box.maxZ, 3)
        let arena = GesBox(size: GesVector(x: 70, y: 50))
        XCTAssertEqual(arena, GesBox(.zero, GesPoint(x: 70, y: 50)))
        XCTAssertEqual(arena.size, GesVector(x: 70, y: 50))
        let negative = GesBox(size: GesVector(x: -3, y: 2))
        XCTAssertEqual(negative.min, GesPoint(x: -3, y: 0))
        XCTAssertEqual(negative.max, GesPoint(x: 0, y: 2))
    }

    func testValuesCarryAnExplicitUnit() {
        let value = GesPoint(x: 1, y: 2, z: 3).value(unit: .meter)
        XCTAssertEqual(value.valueKind, .point)
        XCTAssertEqual(value.unit, .meter)
        XCTAssertEqual(GesPoint(value, unit: .meter), GesPoint(x: 1, y: 2, z: 3))
        XCTAssertNil(GesPoint(value), "Units never convert.")
        XCTAssertNil(GesVector(value, unit: .meter), "A Point is no Vector.")
        XCTAssertTrue(GesVector(x: .nan, y: 0).value().isNothing)
        let box = GesBox(GesPoint(x: 0, y: 0), GesPoint(x: 2, y: 2))
        XCTAssertEqual(GesBox(box.value(unit: .meter), unit: .meter), box)
        XCTAssertNil(GesBox(box.value(unit: .meter)))
        let poly = GesPoly([GesPoint(x: 0, y: 0), GesPoint(x: 1, y: 0), GesPoint(x: 0, y: 1)])
        XCTAssertEqual(GesPoly(poly.value()), poly)
    }

    func testBoxesOrderTheirCorners() {
        let box = GesBox(GesPoint(x: 10, y: 0), GesPoint(x: 0, y: 4))
        XCTAssertEqual(box.min, GesPoint(x: 0, y: 0))
        XCTAssertEqual(box.max, GesPoint(x: 10, y: 4))
        XCTAssertTrue(box.is2D)
        XCTAssertEqual(box.size, GesVector(x: 10, y: 4))
        XCTAssertEqual(box.center, GesPoint(x: 5, y: 2))
        XCTAssertTrue(box.contains(GesPoint(x: 5, y: 4)))
        XCTAssertFalse(box.contains(GesPoint(x: 5, y: 5)))
        XCTAssertFalse(box.contains(GesPoint(x: 1, y: 1, z: 1)))
        let other = GesBox(GesPoint(x: 8, y: 2), GesPoint(x: 12, y: 6))
        XCTAssertTrue(box.intersects(other))
        XCTAssertEqual(box.union(other), GesBox(GesPoint(x: 0, y: 0), GesPoint(x: 12, y: 6)))
        XCTAssertEqual(box.inset(by: 1), GesBox(GesPoint(x: 1, y: 1), GesPoint(x: 9, y: 3)))
        XCTAssertEqual(box.inset(by: -1), GesBox(GesPoint(x: -1, y: -1), GesPoint(x: 11, y: 5)))
        XCTAssertNil(box.inset(by: 3))
        XCTAssertNil(box.inset(by: .nan))
        XCTAssertEqual(box.clamp(GesPoint(x: 12, y: -1)), GesPoint(x: 10, y: 0))
        let far = GesBox(center: GesPoint(x: 20, y: 20), size: GesVector(x: 2, y: -4))
        XCTAssertEqual(far.min, GesPoint(x: 19, y: 18))
        XCTAssertEqual(far.max, GesPoint(x: 21, y: 22))
        XCTAssertFalse(box.intersects(far))
        let deep = GesBox(GesPoint(x: 0, y: 0, z: 0), GesPoint(x: 4, y: 4, z: 4))
        XCTAssertEqual(deep.inset(by: 1)?.max, GesPoint(x: 3, y: 3, z: 3))
        XCTAssertEqual(deep.clamp(GesPoint(x: 5, y: -1, z: 2)), GesPoint(x: 4, y: 0, z: 2))
    }

    func testPolygonsMeasureTheirArea() {
        let square = GesPoly([GesPoint(x: 0, y: 0), GesPoint(x: 4, y: 0), GesPoint(x: 4, y: 4), GesPoint(x: 0, y: 4)])
        XCTAssertTrue(square.isValid)
        XCTAssertEqual(square.area, 16)
        XCTAssertEqual(GesPoly([GesPoint(x: 0, y: 0), GesPoint(x: 0, y: 4), GesPoint(x: 4, y: 4), GesPoint(x: 4, y: 0)]).area, -16)
        XCTAssertEqual(square.centroid, GesPoint(x: 2, y: 2))
        XCTAssertEqual(square.bounds, GesBox(GesPoint(x: 0, y: 0), GesPoint(x: 4, y: 4)))
        XCTAssertEqual(square.contains(GesPoint(x: 2, y: 2)), true)
        XCTAssertEqual(square.contains(GesPoint(x: 4, y: 2)), true, "The outline counts as inside.")
        XCTAssertEqual(square.contains(GesPoint(x: 5, y: 5)), false)
        XCTAssertEqual(square.distance(to: GesPoint(x: 2, y: 1)), 1)
        XCTAssertEqual(square.distance(to: GesPoint(x: 7, y: 8)), 5)
        XCTAssertEqual(square.intersects(GesPoly([GesPoint(x: 3, y: 3), GesPoint(x: 6, y: 3), GesPoint(x: 6, y: 6)])), true)
        XCTAssertEqual(square.intersects(GesPoly([GesPoint(x: 1, y: 1), GesPoint(x: 2, y: 1), GesPoint(x: 2, y: 2)])), true, "A polygon inside another intersects it.")
        XCTAssertEqual(square.intersects(GesPoly([GesPoint(x: 10, y: 10), GesPoint(x: 12, y: 10), GesPoint(x: 11, y: 12)])), false)
        let triangle = GesPoly([GesPoint(x: 0, y: 0), GesPoint(x: 3, y: 0), GesPoint(x: 0, y: 3)])
        XCTAssertEqual(triangle.area, 4.5)
        XCTAssertEqual(triangle.centroid, GesPoint(x: 1, y: 1))
    }

    func testInvalidPolygonsGiveNothing() {
        let invalid: [[GesPoint]] = [
            [GesPoint(x: 0, y: 0), GesPoint(x: 4, y: 0)],
            [GesPoint(x: 0, y: 0), GesPoint(x: 4, y: 4), GesPoint(x: 4, y: 0), GesPoint(x: 0, y: 4)],
            [GesPoint(x: 0, y: 0), GesPoint(x: 4, y: 0), GesPoint(x: 4, y: 0), GesPoint(x: 0, y: 4)],
            [GesPoint(x: 0, y: 0), GesPoint(x: 2, y: 0), GesPoint(x: 4, y: 0)],
            [GesPoint(x: 0, y: 0), GesPoint(x: 4, y: 0), GesPoint(x: 2, y: 0), GesPoint(x: 2, y: 3)],
            [GesPoint(x: 0, y: 0, z: 1), GesPoint(x: 4, y: 0, z: 1), GesPoint(x: 0, y: 4, z: 1)],
            [GesPoint(x: 0, y: 0), GesPoint(x: .nan, y: 0), GesPoint(x: 0, y: 4)],
        ]
        for points in invalid {
            let poly = GesPoly(points)
            XCTAssertFalse(poly.isValid, poly.description)
            XCTAssertNil(poly.area)
            XCTAssertNil(poly.bounds)
            XCTAssertNil(poly.centroid)
            XCTAssertNil(poly.contains(GesPoint(x: 1, y: 1)))
            XCTAssertNil(poly.distance(to: GesPoint(x: 1, y: 1)))
        }
    }

    func testDiscsTakeTheMagnitudeOfTheirRadius() throws {
        let disc = GesDisc(center: GesPoint(x: 2, y: 3), radius: -5)
        XCTAssertEqual(disc.radius, 5)
        XCTAssertEqual(GesDisc(radius: 2), GesDisc(center: .zero, radius: 2))
        XCTAssertEqual(disc.bounds, GesBox(GesPoint(x: -3, y: -2), GesPoint(x: 7, y: 8)))
        XCTAssertEqual(disc.contains(GesPoint(x: 5, y: 7)), true)
        XCTAssertEqual(disc.contains(GesPoint(x: 6, y: 7)), false)
        XCTAssertEqual(disc.distance(to: GesPoint(x: 2, y: 0)), 2)
        XCTAssertEqual(disc.clamp(GesPoint(x: 2, y: 13)), GesPoint(x: 2, y: 8))
        XCTAssertEqual(disc.inset(by: 1), GesDisc(center: GesPoint(x: 2, y: 3), radius: 4))
        XCTAssertNil(disc.inset(by: 6))
        XCTAssertEqual(disc.intersects(GesDisc(center: GesPoint(x: 12, y: 3), radius: 5)), true)
        XCTAssertEqual(GesDisc(radius: 2).intersects(GesBox(GesPoint(x: 1, y: 1), GesPoint(x: 4, y: 4))), true)
        XCTAssertNil(GesDisc(radius: 2).intersects(GesBox(.zero, GesPoint(x: 1, y: 1, z: 1))))
        XCTAssertEqual(GesDisc(radius: 2).intersects(GesPoly([GesPoint(x: 2, y: -1), GesPoint(x: 6, y: -1), GesPoint(x: 6, y: 1), GesPoint(x: 2, y: 1)])), true)
        let lifted = GesDisc(center: GesPoint(x: 0, y: 0, z: 1), radius: 1)
        XCTAssertFalse(lifted.isValid)
        XCTAssertNil(lifted.area)
        XCTAssertEqual(GesDisc(disc.value(unit: .meter), unit: .meter), disc)
        XCTAssertNil(GesDisc(disc.value(unit: .meter)))
        let decoded = try JSONDecoder().decode(GesDisc.self, from: Data(#"{"center":{"x":1,"y":2,"z":0},"radius":-3}"#.utf8))
        XCTAssertEqual(decoded, GesDisc(center: GesPoint(x: 1, y: 2), radius: 3))
    }
}
