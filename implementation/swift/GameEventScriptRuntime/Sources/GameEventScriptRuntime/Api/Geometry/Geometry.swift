// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

/// A displacement with three binary64 components and no unit, the native counterpart of a GES Vector.
///
/// A 2D vector has `z` 0. The unit belongs to the schema or host that uses the value and is applied when it converts to or
/// from a ``GesValue``. Equality compares components with binary64 equality, so 0 and -0 are equal.
public struct GesVector: Hashable, Sendable, Codable, CustomStringConvertible {
    /// The x component.
    public var x: Double
    /// The y component.
    public var y: Double
    /// The z component, 0 for a 2D vector.
    public var z: Double

    /// Creates a vector from its components; `z` defaults to 0 for a 2D vector.
    public init(x: Double, y: Double, z: Double = 0) {
        self.x = x
        self.y = y
        self.z = z
    }

    /// Creates the zero vector.
    public init() { self.init(x: 0, y: 0) }

    /// The zero vector.
    public static var zero: GesVector { GesVector(x: 0, y: 0) }

    /// Whether every component is finite.
    public var isFinite: Bool { x.isFinite && y.isFinite && z.isFinite }

    /// The Euclidean length, computed as the language's `length` computes it.
    public var length: Double { lengthSquared.squareRoot() }

    /// The squared length.
    public var lengthSquared: Double { x * x + y * y + z * z }

    /// The vector of length 1 in the same direction, or nil for a zero or non-finite vector, as `normalize` gives `nothing`.
    public var normalized: GesVector? {
        let length = self.length
        guard length != 0, length.isFinite else { return nil }
        return GesVector(x: x / length, y: y / length, z: z / length)
    }

    /// The dot product: the sum of the component products.
    public func dot(_ other: GesVector) -> Double { x * other.x + y * other.y + z * other.z }

    /// The three-dimensional cross product; for 2D vectors only its `z` is nonzero.
    public func cross(_ other: GesVector) -> GesVector {
        GesVector(x: y * other.z - z * other.y, y: z * other.x - x * other.z, z: x * other.y - y * other.x)
    }

    /// Reads a GES Vector of exactly the given unit, or fails when the value is no Vector or has another unit. Units never convert.
    public init?(_ value: GesValue, unit: GesQuantityUnit = .none) {
        guard value.valueKind == .vector, value.unit == unit else { return nil }
        self.init(x: value.x, y: value.y, z: value.z)
    }

    /// Converts the vector to a GES Vector with a unit; the value is Nothing when a component is NaN, as in the language.
    public func value(unit: GesQuantityUnit = .none) -> GesValue { .vector(x: x, y: y, z: z, unit: unit) }

    /// Adds two vectors.
    public static func + (left: GesVector, right: GesVector) -> GesVector { GesVector(x: left.x + right.x, y: left.y + right.y, z: left.z + right.z) }

    /// Subtracts a vector.
    public static func - (left: GesVector, right: GesVector) -> GesVector { GesVector(x: left.x - right.x, y: left.y - right.y, z: left.z - right.z) }

    /// Negates every component.
    public static prefix func - (vector: GesVector) -> GesVector { GesVector(x: -vector.x, y: -vector.y, z: -vector.z) }

    /// Scales a vector.
    public static func * (vector: GesVector, scale: Double) -> GesVector { GesVector(x: vector.x * scale, y: vector.y * scale, z: vector.z * scale) }

    /// Scales a vector.
    public static func * (scale: Double, vector: GesVector) -> GesVector { vector * scale }

    /// Divides every component by a scalar, following IEEE division.
    public static func / (vector: GesVector, divisor: Double) -> GesVector { GesVector(x: vector.x / divisor, y: vector.y / divisor, z: vector.z / divisor) }

    /// The vector as a unitless GES literal, such as `:Vector(x: 1, y: 2, z: 0)`; informative only.
    public var description: String { value().toText }
}

/// A position with three binary64 components and no unit, the native counterpart of a GES Point.
///
/// A 2D point has `z` 0. As in the language, a point moves by a ``GesVector``, two points give a vector, and a point does
/// not scale. The unit belongs to the schema or host and is applied when it converts to or from a ``GesValue``. Equality
/// compares components with binary64 equality, so 0 and -0 are equal.
public struct GesPoint: Hashable, Sendable, Codable, CustomStringConvertible {
    /// The x component.
    public var x: Double
    /// The y component.
    public var y: Double
    /// The z component, 0 for a 2D point.
    public var z: Double

    /// Creates a point from its components; `z` defaults to 0 for a 2D point.
    public init(x: Double, y: Double, z: Double = 0) {
        self.x = x
        self.y = y
        self.z = z
    }

    /// Creates the origin.
    public init() { self.init(x: 0, y: 0) }

    /// The origin.
    public static var zero: GesPoint { GesPoint(x: 0, y: 0) }

    /// Whether every component is finite.
    public var isFinite: Bool { x.isFinite && y.isFinite && z.isFinite }

    /// The Euclidean distance, as the language's `distance` computes it.
    public func distance(to other: GesPoint) -> Double { (other - self).length }

    /// Reads a GES Point of exactly the given unit, or fails when the value is no Point or has another unit. Units never convert.
    public init?(_ value: GesValue, unit: GesQuantityUnit = .none) {
        guard value.valueKind == .point, value.unit == unit else { return nil }
        self.init(x: value.x, y: value.y, z: value.z)
    }

    /// Converts the point to a GES Point with a unit; the value is Nothing when a component is NaN, as in the language.
    public func value(unit: GesQuantityUnit = .none) -> GesValue { .point(x: x, y: y, z: z, unit: unit) }

    /// Moves a point by a vector.
    public static func + (point: GesPoint, vector: GesVector) -> GesPoint { GesPoint(x: point.x + vector.x, y: point.y + vector.y, z: point.z + vector.z) }

    /// Moves a point back by a vector.
    public static func - (point: GesPoint, vector: GesVector) -> GesPoint { GesPoint(x: point.x - vector.x, y: point.y - vector.y, z: point.z - vector.z) }

    /// The vector from `start` to `end`.
    public static func - (end: GesPoint, start: GesPoint) -> GesVector { GesVector(x: end.x - start.x, y: end.y - start.y, z: end.z - start.z) }

    /// The point as a unitless GES literal, such as `:Point(x: 1, y: 2, z: 0)`; informative only.
    public var description: String { value().toText }
}
