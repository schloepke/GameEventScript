// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

/// An axis-aligned box between two corners, without a unit.
///
/// Creating one orders its corners, so `min` is never above `max` on an axis; a 2D box has `z` 0 on both. Scripts see it as
/// the external type `:Box` of ``GesGeometryLibrary``.
public struct GesBox: Hashable, Sendable, Codable, CustomStringConvertible {
    /// The corner with the smallest components.
    public let min: GesPoint
    /// The corner with the largest components.
    public let max: GesPoint

    /// Creates the box between two opposite corners, in any order.
    public init(_ corner: GesPoint, _ opposite: GesPoint) {
        min = GesPoint(x: Self.lower(corner.x, opposite.x), y: Self.lower(corner.y, opposite.y), z: Self.lower(corner.z, opposite.z))
        max = GesPoint(x: Self.upper(corner.x, opposite.x), y: Self.upper(corner.y, opposite.y), z: Self.upper(corner.z, opposite.z))
    }

    /// Creates the box with a centre and a size; a negative size component counts as its magnitude.
    public init(center: GesPoint, size: GesVector) {
        let half = size * 0.5
        self.init(center - half, center + half)
    }

    /// Whether both corners lie in the plane z = 0.
    public var is2D: Bool { min.z == 0 && max.z == 0 }

    /// Whether every component is finite.
    public var isFinite: Bool { min.isFinite && max.isFinite }

    /// The extent on each axis.
    public var size: GesVector { max - min }

    /// The centre.
    public var center: GesPoint { GesPoint(x: (min.x + max.x) * 0.5, y: (min.y + max.y) * 0.5, z: (min.z + max.z) * 0.5) }

    /// Whether a point lies in the box, its boundary included.
    public func contains(_ point: GesPoint) -> Bool {
        min.x <= point.x && point.x <= max.x && min.y <= point.y && point.y <= max.y && min.z <= point.z && point.z <= max.z
    }

    /// Whether two boxes share a point; touching boxes intersect.
    public func intersects(_ other: GesBox) -> Bool {
        min.x <= other.max.x && other.min.x <= max.x && min.y <= other.max.y && other.min.y <= max.y && min.z <= other.max.z && other.min.z <= max.z
    }

    /// The smallest box that holds both.
    public func union(_ other: GesBox) -> GesBox {
        GesBox(
            GesPoint(x: Self.lower(min.x, other.min.x), y: Self.lower(min.y, other.min.y), z: Self.lower(min.z, other.min.z)),
            GesPoint(x: Self.upper(max.x, other.max.x), y: Self.upper(max.y, other.max.y), z: Self.upper(max.z, other.max.z))
        )
    }

    /// Moves every side inward by a distance, outward when it is negative: x and y of a 2D box, all three axes otherwise.
    /// Nil when the distance is not finite or the sides would cross.
    public func inset(by distance: Double) -> GesBox? {
        guard distance.isFinite else { return nil }
        let depth = is2D ? 0 : distance
        let lower = GesPoint(x: min.x + distance, y: min.y + distance, z: min.z + depth)
        let upper = GesPoint(x: max.x - distance, y: max.y - distance, z: max.z - depth)
        if lower.x > upper.x || lower.y > upper.y || lower.z > upper.z { return nil }
        return GesBox(lower, upper)
    }

    /// Moves a point onto the nearest point of the box: the point itself when the box holds it.
    public func clamp(_ point: GesPoint) -> GesPoint {
        GesPoint(x: Self.clamp(point.x, min.x, max.x), y: Self.clamp(point.y, min.y, max.y), z: Self.clamp(point.z, min.z, max.z))
    }

    /// Reads a `:Box` of ``GesGeometryLibrary`` of exactly the given unit, or fails for another value or unit. Units never convert.
    public init?(_ value: GesValue, unit: GesQuantityUnit = .none) {
        guard let box = value.externalValue as? GesGeometryLibrary.BoxValue, box.unit == unit else { return nil }
        self = box.box
    }

    /// Converts the box to the external value `:Box` of ``GesGeometryLibrary`` with the unit of its corners.
    public func value(unit: GesQuantityUnit = .none) -> GesValue { .external(GesGeometryLibrary.BoxValue(self, unit)) }

    /// The corners, for diagnostics.
    public var description: String { ":Box(min: \(min), max: \(max))" }

    // Written out rather than Swift.min and Swift.max, so that both ports treat NaN alike.
    static func lower(_ a: Double, _ b: Double) -> Double { b < a ? b : a }

    static func upper(_ a: Double, _ b: Double) -> Double { b > a ? b : a }

    private static func clamp(_ value: Double, _ lower: Double, _ upper: Double) -> Double { value < lower ? lower : value > upper ? upper : value }
}

/// A closed polygon in the plane z = 0, without a unit: its points in order, the last connecting to the first.
///
/// A polygon is valid with at least three finite points in the plane, no edge of zero length, no two edges that cross or
/// overlap, and an area other than zero; the operations of an invalid one give nil, as GES operations give `nothing` for a
/// domain failure. Equality compares the points in order. Scripts see it as the external type `:Poly` of
/// ``GesGeometryLibrary``.
public struct GesPoly: Hashable, Sendable, Codable, CustomStringConvertible {
    /// The points in order.
    public let points: [GesPoint]
    /// Whether the polygon is a valid simple polygon with an area.
    public let isValid: Bool

    /// Creates a polygon from its points; validity is checked once.
    public init(_ points: [GesPoint]) {
        self.points = points
        isValid = Self.valid(points)
    }

    private enum CodingKeys: String, CodingKey { case points }

    /// Decodes the points and checks validity again.
    public init(from decoder: any Decoder) throws {
        self.init(try decoder.container(keyedBy: CodingKeys.self).decode([GesPoint].self, forKey: .points))
    }

    /// Encodes the points.
    public func encode(to encoder: any Encoder) throws {
        var container = encoder.container(keyedBy: CodingKeys.self)
        try container.encode(points, forKey: .points)
    }

    /// Compares the points in order.
    public static func == (left: GesPoly, right: GesPoly) -> Bool { left.points == right.points }

    /// Hashes the points.
    public func hash(into hasher: inout Hasher) { hasher.combine(points) }

    /// The signed area: positive when the points run counterclockwise, negative when clockwise.
    public var area: Double? { isValid ? Self.signedArea(points) : nil }

    /// The smallest box that holds every point.
    public var bounds: GesBox? {
        guard isValid else { return nil }
        var lower = points[0]
        var upper = points[0]
        for point in points {
            lower = GesPoint(x: GesBox.lower(lower.x, point.x), y: GesBox.lower(lower.y, point.y))
            upper = GesPoint(x: GesBox.upper(upper.x, point.x), y: GesBox.upper(upper.y, point.y))
        }
        return GesBox(lower, upper)
    }

    /// The centre of the area.
    public var centroid: GesPoint? {
        guard isValid else { return nil }
        var x = 0.0
        var y = 0.0
        var twice = 0.0
        for index in points.indices {
            let a = points[index]
            let b = points[(index + 1) % points.count]
            let cross = (a.x * b.y) - (b.x * a.y)
            twice += cross
            x += (a.x + b.x) * cross
            y += (a.y + b.y) * cross
        }
        let divisor = twice * 3
        return GesPoint(x: x / divisor, y: y / divisor)
    }

    /// Whether a point lies in the polygon, its outline included; nil when the polygon is invalid or the point not finite.
    public func contains(_ point: GesPoint) -> Bool? {
        guard isValid, point.isFinite else { return nil }
        if point.z != 0 { return false }
        var inside = false
        for index in points.indices {
            let a = points[index]
            let b = points[(index + 1) % points.count]
            if Self.onSegment(a, b, point) { return true }
            if (a.y > point.y) != (b.y > point.y) && point.x < a.x + ((point.y - a.y) / (b.y - a.y) * (b.x - a.x)) { inside.toggle() }
        }
        return inside
    }

    /// The distance from a point to the outline, zero on it, for a point inside as well as outside; nil when the polygon is
    /// invalid or the point not finite.
    public func distance(to point: GesPoint) -> Double? {
        guard isValid, point.isFinite else { return nil }
        var best = Double.infinity
        for index in points.indices {
            let distance = Self.segmentDistance(points[index], points[(index + 1) % points.count], point)
            if distance < best { best = distance }
        }
        return best
    }

    /// Whether two polygons share a point: crossing or touching outlines, or one inside the other; nil when either is invalid.
    public func intersects(_ other: GesPoly) -> Bool? {
        guard isValid, other.isValid else { return nil }
        for index in points.indices {
            let a = points[index]
            let b = points[(index + 1) % points.count]
            for next in other.points.indices where Self.segmentsIntersect(a, b, other.points[next], other.points[(next + 1) % other.points.count]) { return true }
        }
        return contains(other.points[0]) == true || other.contains(points[0]) == true
    }

    /// Reads a `:Poly` of ``GesGeometryLibrary`` of exactly the given unit, or fails for another value or unit. Units never convert.
    public init?(_ value: GesValue, unit: GesQuantityUnit = .none) {
        guard let poly = value.externalValue as? GesGeometryLibrary.PolyValue, poly.unit == unit else { return nil }
        self = poly.poly
    }

    /// Converts the polygon to the external value `:Poly` of ``GesGeometryLibrary`` with the unit of its points.
    public func value(unit: GesQuantityUnit = .none) -> GesValue { .external(GesGeometryLibrary.PolyValue(self, unit)) }

    /// The points, for diagnostics.
    public var description: String { ":Poly(points: [\(points.map(\.description).joined(separator: ", "))])" }

    private static func signedArea(_ points: [GesPoint]) -> Double {
        var twice = 0.0
        for index in points.indices {
            let a = points[index]
            let b = points[(index + 1) % points.count]
            twice += (a.x * b.y) - (b.x * a.y)
        }
        return twice * 0.5
    }

    private static func valid(_ points: [GesPoint]) -> Bool {
        let count = points.count
        if count < 3 { return false }
        for point in points where !point.isFinite || point.z != 0 { return false }
        for index in 0..<count where points[index] == points[(index + 1) % count] { return false }
        for first in 0..<count {
            let a = points[first]
            let b = points[(first + 1) % count]
            for second in (first + 1)..<count {
                let c = points[second]
                let d = points[(second + 1) % count]
                if second == first + 1 {
                    // Neighbours share b; they must not fold back over each other.
                    if orientation(a, b, d) == 0 && dot(a - b, d - b) > 0 { return false }
                } else if first == 0 && second == count - 1 {
                    // The closing edge shares a with the first edge.
                    if orientation(c, a, b) == 0 && dot(c - a, b - a) > 0 { return false }
                } else if segmentsIntersect(a, b, c, d) {
                    return false
                }
            }
        }
        return signedArea(points) != 0
    }

    private static func dot(_ a: GesVector, _ b: GesVector) -> Double { (a.x * b.x) + (a.y * b.y) }

    // The sign of the turn from a over b to c: positive counterclockwise, negative clockwise, zero when collinear.
    private static func orientation(_ a: GesPoint, _ b: GesPoint, _ c: GesPoint) -> Int {
        let turn = ((b.x - a.x) * (c.y - a.y)) - ((b.y - a.y) * (c.x - a.x))
        return turn > 0 ? 1 : turn < 0 ? -1 : 0
    }

    // Whether c lies on the closed segment from a to b.
    private static func onSegment(_ a: GesPoint, _ b: GesPoint, _ c: GesPoint) -> Bool {
        orientation(a, b, c) == 0 && GesBox.lower(a.x, b.x) <= c.x && c.x <= GesBox.upper(a.x, b.x) && GesBox.lower(a.y, b.y) <= c.y && c.y <= GesBox.upper(a.y, b.y)
    }

    // Whether the closed segments ab and cd share a point.
    private static func segmentsIntersect(_ a: GesPoint, _ b: GesPoint, _ c: GesPoint, _ d: GesPoint) -> Bool {
        let o1 = orientation(a, b, c)
        let o2 = orientation(a, b, d)
        let o3 = orientation(c, d, a)
        let o4 = orientation(c, d, b)
        if o1 != o2 && o3 != o4 { return true }
        return (o1 == 0 && onSegment(a, b, c)) || (o2 == 0 && onSegment(a, b, d)) || (o3 == 0 && onSegment(c, d, a)) || (o4 == 0 && onSegment(c, d, b))
    }

    private static func segmentDistance(_ a: GesPoint, _ b: GesPoint, _ point: GesPoint) -> Double {
        let edge = b - a
        var t = dot(point - a, edge) / dot(edge, edge)
        t = t < 0 ? 0 : t > 1 ? 1 : t
        let closest = GesPoint(x: a.x + (edge.x * t), y: a.y + (edge.y * t))
        return (point - closest).length
    }
}
