// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

/// The standard geometry library: the external types `:Box`, `:Poly` and `:Disc` over ``GesBox``, ``GesPoly`` and ``GesDisc``,
/// and the extension functions `:geometry.*`, so that hosts and scripts compute alike.
///
/// A host that wants it passes ``catalog(next:)`` to the compiler and ``externalTypes(next:)`` and ``extensions(next:)`` to
/// the host, each in front of its own catalog or registry, which resolves what the library does not.
/// `:Box(lower: p, upper: q)` takes two corners in any order, which its fields `lower` and `upper` return ordered (`min` and
/// `max` are GES keywords), and `:Box(center: p, size: v)` a centre and a size;
/// `:Poly(points: [...])` takes its points; `:Disc(center: p, radius: r)` a centre and a radius and `:Disc(radius: r)` a
/// radius around the origin. Corners, points, sizes and radii share one unit, which the value keeps; another kind or a mixed
/// unit constructs `nothing`. A polygon or disc need not be valid to exist, but its operations give `nothing` when it is not.
/// Functions: `size`, `center`, `union`, `inset(_, by:)` and `nearest`, the point of a shape nearest to a point (`clamp` is
/// a GES keyword), of boxes; `bounds`, `area` (signed and unitless, like `dot`), `distance` and `centroid` of polygons;
/// `center`, `bounds`, `area`, `distance`, `inset(_, by:)` and `nearest` of discs; `contains` and `intersects` of all, a box
/// with a polygon counting as its outline in the plane and a disc meeting a 2D box. Anything else, such as another unit,
/// gives `nothing`.
public enum GesGeometryLibrary {
    /// The extension namespace of the functions.
    public static let namespace = "geometry"

    nonisolated(unsafe) static let boxDefinition = try! GesExternalTypeDefinition(
        name: "Box",
        fields: [
            try! GesExternalTypeFieldDefinition(name: "lower", kind: .point), try! GesExternalTypeFieldDefinition(name: "upper", kind: .point),
            try! GesExternalTypeFieldDefinition(name: "center", kind: .point), try! GesExternalTypeFieldDefinition(name: "size", kind: .vector),
        ],
        constructors: [
            try! GesExternalTypeConstructorDefinition(typeName: "Box", parameters: [try! .init(name: "lower", kind: .point), try! .init(name: "upper", kind: .point)]),
            try! GesExternalTypeConstructorDefinition(typeName: "Box", parameters: [try! .init(name: "center", kind: .point), try! .init(name: "size", kind: .vector)]),
        ]
    )

    nonisolated(unsafe) static let polyDefinition = try! GesExternalTypeDefinition(
        name: "Poly",
        fields: [try! GesExternalTypeFieldDefinition(name: "points", kind: .list)],
        constructors: [try! GesExternalTypeConstructorDefinition(typeName: "Poly", parameters: [try! .init(name: "points", kind: .list)])]
    )

    nonisolated(unsafe) static let discDefinition = try! GesExternalTypeDefinition(
        name: "Disc",
        fields: [try! GesExternalTypeFieldDefinition(name: "center", kind: .point), try! GesExternalTypeFieldDefinition(name: "radius", kind: .float)],
        constructors: [
            try! GesExternalTypeConstructorDefinition(typeName: "Disc", parameters: [try! .init(name: "center", kind: .point), try! .init(name: "radius", kind: .float)]),
            try! GesExternalTypeConstructorDefinition(typeName: "Disc", parameters: [try! .init(name: "radius", kind: .float)]),
        ]
    )

    /// The declarations of `:Box`, `:Poly` and `:Disc`.
    public static var types: [GesExternalTypeDefinition] { [boxDefinition, polyDefinition, discDefinition] }

    /// Creates the compiler catalog of the library in front of another one, which resolves every other type.
    public static func catalog(next: (any GesExternalTypeCatalog)? = nil) -> any GesExternalTypeCatalog { CatalogOverlay(next: next) }

    /// Creates the host registry of the library's constructors in front of another one, which resolves every other constructor.
    public static func externalTypes(next: (any GesExternalTypeRegistry)? = nil) -> any GesExternalTypeRegistry { TypeOverlay(next: next) }

    /// Creates the host registry of the library's functions in front of another one, which resolves every other extension.
    public static func extensions(next: (any GesExtensionRegistry)? = nil) -> any GesExtensionRegistry { ExtensionOverlay(next: next) }

    private struct CatalogOverlay: GesExternalTypeCatalog {
        let next: (any GesExternalTypeCatalog)?

        var types: [GesExternalTypeDefinition] {
            [boxDefinition, polyDefinition, discDefinition] + (next?.types ?? []).filter { $0.name != "Box" && $0.name != "Poly" && $0.name != "Disc" }
        }

        func resolve(_ typeName: String) throws -> GesExternalTypeDefinition? {
            switch typeName {
            case "Box": boxDefinition
            case "Poly": polyDefinition
            case "Disc": discDefinition
            default: try next?.resolve(typeName)
            }
        }
    }

    private struct TypeOverlay: GesExternalTypeRegistry {
        let next: (any GesExternalTypeRegistry)?

        func resolve(_ reference: GesExternalTypeConstructorReference) throws -> (any GesExternalTypeConstructor)? {
            for definition in [boxDefinition, polyDefinition, discDefinition] {
                for constructor in definition.constructors where constructor.signatureID == reference.signatureID { return Constructor(definition: constructor) }
            }
            return try next?.resolve(reference)
        }
    }

    private struct Constructor: GesExternalTypeConstructor {
        let definition: GesExternalTypeConstructorDefinition

        func invoke(_ call: GesExternalTypeConstructorCall) throws {
            let arguments = call.arguments
            if definition.typeName == "Poly" {
                if let (points, unit) = GesGeometryLibrary.points(arguments[0]) {
                    try call.setExternalValue(PolyValue(GesPoly(points), unit))
                } else {
                    call.setNothing()
                }
                return
            }
            if definition.typeName == "Disc" {
                // The radius takes the unit of the centre; without one, the disc lies around the origin.
                let radius = arguments[arguments.count - 1]
                let around = radius.unit
                if !GesGeometryLibrary.isNumber(radius, around) || (arguments.count == 2 && (arguments[0].valueKind != .point || arguments[0].unit != around)) {
                    call.setNothing()
                } else {
                    try call.setExternalValue(DiscValue(GesDisc(center: arguments.count == 2 ? point(arguments[0]) : .zero, radius: radius.asNumber), around))
                }
                return
            }
            let first = arguments[0]
            let second = arguments[1]
            let unit = first.unit
            if first.valueKind != .point || second.unit != unit {
                call.setNothing()
            } else if definition.parameters[0].name == "lower" && second.valueKind == .point {
                try call.setExternalValue(BoxValue(GesBox(point(first), point(second)), unit))
            } else if definition.parameters[0].name == "center" && second.valueKind == .vector {
                try call.setExternalValue(BoxValue(GesBox(center: point(first), size: vector(second)), unit))
            } else {
                call.setNothing()
            }
        }
    }

    private static func point(_ value: GesValue) -> GesPoint { GesPoint(x: value.x, y: value.y, z: value.z) }

    private static func vector(_ value: GesValue) -> GesVector { GesVector(x: value.x, y: value.y, z: value.z) }

    private static func isNumber(_ value: GesValue, _ unit: GesQuantityUnit) -> Bool { (value.valueKind == .integer || value.valueKind == .float) && value.unit == unit }

    // The points of a List of Points that share one unit; an empty list has no unit.
    private static func points(_ list: GesValue) -> ([GesPoint], GesQuantityUnit)? {
        guard let items = list.listValue else { return nil }
        var points: [GesPoint] = []
        points.reserveCapacity(items.count)
        var unit = GesQuantityUnit.none
        for (index, item) in items.enumerated() {
            if item.valueKind != .point || (index > 0 && item.unit != unit) { return nil }
            unit = item.unit
            points.append(point(item))
        }
        return (points, unit)
    }

    /// A `:Box` held by a script, with the unit of its corners.
    final class BoxValue: GesExternalValue {
        let box: GesBox
        let unit: GesQuantityUnit

        init(_ box: GesBox, _ unit: GesQuantityUnit) {
            self.box = box
            self.unit = unit
        }

        var definition: GesExternalTypeDefinition { boxDefinition }

        func field(_ name: String) throws -> GesValue? {
            switch name {
            case "lower": box.min.value(unit: unit)
            case "upper": box.max.value(unit: unit)
            case "center": box.center.value(unit: unit)
            case "size": box.size.value(unit: unit)
            default: nil
            }
        }
    }

    /// A `:Poly` held by a script, with the unit of its points.
    final class PolyValue: GesExternalValue {
        let poly: GesPoly
        let unit: GesQuantityUnit

        init(_ poly: GesPoly, _ unit: GesQuantityUnit) {
            self.poly = poly
            self.unit = unit
        }

        var definition: GesExternalTypeDefinition { polyDefinition }

        func field(_ name: String) throws -> GesValue? {
            name == "points" ? .list(poly.points.map { $0.value(unit: unit) }) : nil
        }
    }

    /// A `:Disc` held by a script, with the unit of its centre and radius.
    final class DiscValue: GesExternalValue {
        let disc: GesDisc
        let unit: GesQuantityUnit

        init(_ disc: GesDisc, _ unit: GesQuantityUnit) {
            self.disc = disc
            self.unit = unit
        }

        var definition: GesExternalTypeDefinition { discDefinition }

        func field(_ name: String) throws -> GesValue? {
            switch name {
            case "center": disc.center.value(unit: unit)
            case "radius": .float(disc.radius, unit: unit)
            default: nil
            }
        }
    }

    private static let signatures: Set<String> = [
        "geometry.size(_)", "geometry.center(_)", "geometry.bounds(_)", "geometry.area(_)", "geometry.centroid(_)", "geometry.contains(_,_)",
        "geometry.intersects(_,_)", "geometry.union(_,_)", "geometry.inset(_,by)", "geometry.nearest(_,_)", "geometry.distance(_,_)",
    ]

    private struct ExtensionOverlay: GesExtensionRegistry {
        let next: (any GesExtensionRegistry)?

        func resolve(_ reference: GesExtensionReference) throws -> (any GesExtensionFunction)? {
            if reference.extensionName == namespace && signatures.contains(reference.signatureID) { return Function(name: reference.functionName) }
            return try next?.resolve(reference)
        }
    }

    private struct Function: GesExtensionFunction {
        let name: String

        func invoke(_ call: GesExtensionCall) throws {
            let arguments = call.arguments
            let first = arguments[0].externalValue
            let box = first as? BoxValue
            let poly = first as? PolyValue
            let disc = first as? DiscValue
            let unit = box?.unit ?? poly?.unit ?? disc?.unit ?? .none
            var result = GesValue.nothing
            switch name {
            case "size": if let box { result = box.box.size.value(unit: unit) }
            case "center":
                if let box { result = box.box.center.value(unit: unit) } else if let disc { result = disc.disc.center.value(unit: unit) }
            case "bounds":
                if let bounds = poly?.poly.bounds ?? disc?.disc.bounds { result = bounds.value(unit: unit) } else if box != nil { result = arguments[0] }
            case "area": if let area = poly?.poly.area ?? disc?.disc.area { result = .float(area) }
            case "centroid": if let centroid = poly?.poly.centroid { result = centroid.value(unit: unit) }
            case "contains":
                if let point = Self.point(arguments, 1, unit) {
                    if let box { result = .boolean(box.box.contains(point)) } else if let inside = poly?.poly.contains(point) ?? disc?.disc.contains(point) { result = .boolean(inside) }
                }
            case "intersects":
                if let other = Self.shape(arguments[1].externalValue, unit), let touching = Self.intersects(first, other) { result = .boolean(touching) }
            case "union":
                if let box, let second = arguments[1].externalValue as? BoxValue, second.unit == unit { result = box.box.union(second.box).value(unit: unit) }
            case "inset":
                if GesGeometryLibrary.isNumber(arguments[1], unit) {
                    if let box, let inset = box.box.inset(by: arguments[1].asNumber) {
                        result = inset.value(unit: unit)
                    } else if let disc, let inset = disc.disc.inset(by: arguments[1].asNumber) {
                        result = inset.value(unit: unit)
                    }
                }
            case "nearest":
                if let target = Self.point(arguments, 1, unit) {
                    if let box { result = box.box.clamp(target).value(unit: unit) } else if let nearest = disc?.disc.clamp(target) { result = nearest.value(unit: unit) }
                }
            case "distance":
                if let from = Self.point(arguments, 1, unit), let distance = poly?.poly.distance(to: from) ?? disc?.disc.distance(to: from) { result = .float(distance, unit: unit) }
            default: break
            }
            call.setValue(result)
        }

        private static func point(_ arguments: GesValueArguments, _ index: Int, _ unit: GesQuantityUnit) -> GesPoint? {
            let value = arguments[index]
            return value.valueKind == .point && value.unit == unit ? GesGeometryLibrary.point(value) : nil
        }

        private static func shape(_ value: (any GesExternalValue)?, _ unit: GesQuantityUnit) -> (any GesExternalValue)? {
            if let box = value as? BoxValue, box.unit == unit { return box }
            if let poly = value as? PolyValue, poly.unit == unit { return poly }
            if let disc = value as? DiscValue, disc.unit == unit { return disc }
            return nil
        }

        // Boxes intersect as boxes; a 2D box meets a polygon as the polygon of its outline; a disc meets every shape as a disc.
        private static func intersects(_ first: (any GesExternalValue)?, _ second: any GesExternalValue) -> Bool? {
            if let a = first as? BoxValue, let b = second as? BoxValue { return a.box.intersects(b.box) }
            if let disc = first as? DiscValue { return meets(disc.disc, second) }
            if let other = second as? DiscValue { return first.flatMap { meets(other.disc, $0) } }
            let left = (first as? BoxValue).map { outline($0.box) } ?? (first as? PolyValue)?.poly
            let right = (second as? BoxValue).map { outline($0.box) } ?? (second as? PolyValue)?.poly
            guard let left, let right else { return nil }
            return left.intersects(right)
        }

        private static func meets(_ disc: GesDisc, _ shape: any GesExternalValue) -> Bool? {
            if let other = shape as? DiscValue { return disc.intersects(other.disc) }
            if let box = shape as? BoxValue { return disc.intersects(box.box) }
            if let poly = shape as? PolyValue { return disc.intersects(poly.poly) }
            return nil
        }

        private static func outline(_ box: GesBox) -> GesPoly? {
            box.is2D ? GesPoly([box.min, GesPoint(x: box.max.x, y: box.min.y), box.max, GesPoint(x: box.min.x, y: box.max.y)]) : nil
        }
    }
}
