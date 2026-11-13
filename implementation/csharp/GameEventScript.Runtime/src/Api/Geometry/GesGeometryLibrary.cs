// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using GameEventScript.Runtime.Values;

namespace GameEventScript.Api;

/// <summary>
/// The standard geometry library: the external types <c>:Box</c>, <c>:Poly</c> and <c>:Disc</c> over <see cref="GesBox"/>,
/// <see cref="GesPoly"/> and <see cref="GesDisc"/>, and the extension functions <c>:geometry.*</c>, so that hosts and scripts compute alike. A host
/// that wants it passes <see cref="Catalog"/> to the compiler and <see cref="ExternalTypes"/> and <see cref="Extensions"/> to
/// the host, each in front of its own catalog or registry, which resolves what the library does not.
/// </summary>
/// <remarks>
/// <c>:Box(lower: p, upper: q)</c> takes two corners in any order, which its fields <c>lower</c> and <c>upper</c> return
/// ordered (<c>min</c> and <c>max</c> are GES keywords), and <c>:Box(center: p, size: v)</c> a centre and a size;
/// <c>:Poly(points: [...])</c> takes its points; <c>:Disc(center: p, radius: r)</c> a centre and a radius and
/// <c>:Disc(radius: r)</c> a radius around the origin. Corners, points, sizes and radii share one unit, which the value
/// keeps; another kind or a mixed unit constructs <c>nothing</c>. A polygon or disc need not be valid to exist, but its
/// operations give <c>nothing</c> when it is not. Functions: <c>size</c>, <c>center</c>, <c>union</c>, <c>inset(_, by:)</c>
/// and <c>nearest</c>, the point of a shape nearest to a point (<c>clamp</c> is a GES keyword), of boxes; <c>bounds</c>,
/// <c>area</c> (signed and unitless, like <c>dot</c>), <c>distance</c> and <c>centroid</c> of polygons; <c>center</c>,
/// <c>bounds</c>, <c>area</c>, <c>distance</c>, <c>inset(_, by:)</c> and <c>nearest</c> of discs; <c>contains</c> and
/// <c>intersects</c> of all, a box with a polygon counting as its outline in the plane and a disc meeting a 2D box. Anything
/// else, such as another unit, gives <c>nothing</c>.
/// </remarks>
public static class GesGeometryLibrary
{
    /// <summary>The extension namespace of the functions.</summary>
    public const string Namespace = "geometry";

    private static readonly GesExternalTypeDefinition BoxDefinition = new(
        "Box",
        [new GesExternalTypeFieldDefinition("lower", GesBytecodeTypeKind.Point), new GesExternalTypeFieldDefinition("upper", GesBytecodeTypeKind.Point),
         new GesExternalTypeFieldDefinition("center", GesBytecodeTypeKind.Point), new GesExternalTypeFieldDefinition("size", GesBytecodeTypeKind.Vector)],
        [new GesExternalTypeConstructorDefinition("Box", [new GesExternalTypeParameterDefinition("lower", GesBytecodeTypeKind.Point), new GesExternalTypeParameterDefinition("upper", GesBytecodeTypeKind.Point)]),
         new GesExternalTypeConstructorDefinition("Box", [new GesExternalTypeParameterDefinition("center", GesBytecodeTypeKind.Point), new GesExternalTypeParameterDefinition("size", GesBytecodeTypeKind.Vector)])]);

    private static readonly GesExternalTypeDefinition PolyDefinition = new(
        "Poly",
        [new GesExternalTypeFieldDefinition("points", GesBytecodeTypeKind.List)],
        [new GesExternalTypeConstructorDefinition("Poly", [new GesExternalTypeParameterDefinition("points", GesBytecodeTypeKind.List)])]);

    private static readonly GesExternalTypeDefinition DiscDefinition = new(
        "Disc",
        [new GesExternalTypeFieldDefinition("center", GesBytecodeTypeKind.Point), new GesExternalTypeFieldDefinition("radius", GesBytecodeTypeKind.Float)],
        [new GesExternalTypeConstructorDefinition("Disc", [new GesExternalTypeParameterDefinition("center", GesBytecodeTypeKind.Point), new GesExternalTypeParameterDefinition("radius", GesBytecodeTypeKind.Float)]),
         new GesExternalTypeConstructorDefinition("Disc", [new GesExternalTypeParameterDefinition("radius", GesBytecodeTypeKind.Float)])]);

    /// <summary>Gets the declarations of <c>:Box</c>, <c>:Poly</c> and <c>:Disc</c>.</summary>
    public static IReadOnlyList<GesExternalTypeDefinition> Types { get; } = new[] { BoxDefinition, PolyDefinition, DiscDefinition };

    /// <summary>Creates the compiler catalog of the library in front of another one.</summary>
    /// <param name="next">The catalog that resolves every other type, or null for none.</param>
    /// <returns>The catalog.</returns>
    public static IGesExternalTypeCatalog Catalog(IGesExternalTypeCatalog? next = null) => new CatalogOverlay(next);

    /// <summary>Creates the host registry of the library's constructors in front of another one.</summary>
    /// <param name="next">The registry that resolves every other constructor, or null for none.</param>
    /// <returns>The registry.</returns>
    public static IGesExternalTypeRegistry ExternalTypes(IGesExternalTypeRegistry? next = null) => new TypeOverlay(next);

    /// <summary>Creates the host registry of the library's functions in front of another one.</summary>
    /// <param name="next">The registry that resolves every other extension, or null for none.</param>
    /// <returns>The registry.</returns>
    public static IGesExtensionRegistry Extensions(IGesExtensionRegistry? next = null) => new ExtensionOverlay(next);

    private sealed class CatalogOverlay(IGesExternalTypeCatalog? next) : IGesExternalTypeCatalog
    {
        private readonly List<GesExternalTypeDefinition> _types = Merge(next);

        public IReadOnlyList<GesExternalTypeDefinition> Types => _types;

        public GesExternalTypeDefinition? Resolve(string typeName) => typeName switch
        {
            "Box" => BoxDefinition,
            "Poly" => PolyDefinition,
            "Disc" => DiscDefinition,
            _ => next?.Resolve(typeName)
        };

        private static List<GesExternalTypeDefinition> Merge(IGesExternalTypeCatalog? next)
        {
            var types = new List<GesExternalTypeDefinition> { BoxDefinition, PolyDefinition, DiscDefinition };
            if (next is not null)
                foreach (var type in next.Types)
                    if (type.Name is not ("Box" or "Poly" or "Disc")) types.Add(type);
            return types;
        }
    }

    private sealed class TypeOverlay(IGesExternalTypeRegistry? next) : IGesExternalTypeRegistry
    {
        public IGesExternalTypeConstructor? Resolve(GesExternalTypeConstructorReference reference)
        {
            if (reference is null) throw new ArgumentNullException(nameof(reference));
            foreach (var definition in new[] { BoxDefinition, PolyDefinition, DiscDefinition })
                foreach (var constructor in definition.Constructors)
                    if (constructor.SignatureId == reference.SignatureId) return new Constructor(constructor);
            return next?.Resolve(reference);
        }
    }

    private sealed class Constructor(GesExternalTypeConstructorDefinition definition) : IGesExternalTypeConstructor
    {
        public GesExternalTypeConstructorDefinition Definition { get; } = definition;

        public void Invoke(GesExternalTypeConstructorCall call)
        {
            var arguments = call.Arguments;
            if (Definition.TypeName == "Poly")
            {
                if (Points(arguments[0]) is not { } points) call.SetNothing();
                else call.SetExternalValue(new PolyValue(new GesPoly(points.Points), points.Unit));
                return;
            }

            if (Definition.TypeName == "Disc")
            {
                // The radius takes the unit of the centre; without one, the disc lies around the origin.
                var radius = arguments.Length - 1;
                var around = arguments.UnitAt(radius);
                if (!IsNumber(arguments, radius, around) || (radius == 1 && (arguments.KindAt(0) != GesBytecodeTypeKind.Point || arguments.UnitAt(0) != around))) call.SetNothing();
                else call.SetExternalValue(new DiscValue(new GesDisc(radius == 1 ? Point(arguments[0]) : GesPoint.Zero, arguments.GetAsNumber(radius)), around));
                return;
            }

            var unit = arguments.UnitAt(0);
            if (arguments.KindAt(0) != GesBytecodeTypeKind.Point || arguments.UnitAt(1) != unit) call.SetNothing();
            else if (Definition.Parameters[0].Name == "lower" && arguments.KindAt(1) == GesBytecodeTypeKind.Point)
                call.SetExternalValue(new BoxValue(new GesBox(Point(arguments[0]), Point(arguments[1])), unit));
            else if (Definition.Parameters[0].Name == "center" && arguments.KindAt(1) == GesBytecodeTypeKind.Vector)
                call.SetExternalValue(new BoxValue(GesBox.FromCenter(Point(arguments[0]), Vector(arguments[1])), unit));
            else call.SetNothing();
        }
    }

    private static GesPoint Point(in GesValue value) => new(value.X, value.Y, value.Z);

    private static bool IsNumber(in GesValueArguments arguments, int index, GesQuantityUnit unit)
        => arguments.KindAt(index) is GesBytecodeTypeKind.Integer or GesBytecodeTypeKind.Float && arguments.UnitAt(index) == unit;

    private static GesVector Vector(in GesValue value) => new(value.X, value.Y, value.Z);

    // The points of a List of Points that share one unit; an empty list has no unit.
    private static (List<GesPoint> Points, GesQuantityUnit Unit)? Points(in GesValue list)
    {
        if (list.ValueKind != GesBytecodeTypeKind.List) return null;
        var items = list.AsList();
        var points = new List<GesPoint>(items.Length);
        var unit = GesQuantityUnit.UnitNone;
        for (var index = 0; index < items.Length; index++)
        {
            var item = items[index];
            if (item.ValueKind != GesBytecodeTypeKind.Point || (index > 0 && item.ValueUnit != unit)) return null;
            unit = item.ValueUnit;
            points.Add(Point(item));
        }

        return (points, unit);
    }

    /// <summary>A <c>:Box</c> held by a script, with the unit of its corners.</summary>
    internal sealed class BoxValue(GesBox box, GesQuantityUnit unit) : IGesExternalValue
    {
        internal GesBox Box { get; } = box;

        internal GesQuantityUnit Unit { get; } = unit;

        public GesExternalTypeDefinition Definition => BoxDefinition;

        public GesValue? GetField(string fieldName) => fieldName switch
        {
            "lower" => Box.Min.ToValue(Unit),
            "upper" => Box.Max.ToValue(Unit),
            "center" => Box.Center.ToValue(Unit),
            "size" => Box.Size.ToValue(Unit),
            _ => null
        };
    }

    /// <summary>A <c>:Poly</c> held by a script, with the unit of its points.</summary>
    internal sealed class PolyValue(GesPoly poly, GesQuantityUnit unit) : IGesExternalValue
    {
        internal GesPoly Poly { get; } = poly;

        internal GesQuantityUnit Unit { get; } = unit;

        public GesExternalTypeDefinition Definition => PolyDefinition;

        public GesValue? GetField(string fieldName)
        {
            if (fieldName != "points") return null;
            var points = new GesValue[Poly.Points.Count];
            for (var index = 0; index < points.Length; index++) points[index] = Poly.Points[index].ToValue(Unit);
            return GesValue.GesList(points);
        }
    }

    /// <summary>A <c>:Disc</c> held by a script, with the unit of its centre and radius.</summary>
    internal sealed class DiscValue(GesDisc disc, GesQuantityUnit unit) : IGesExternalValue
    {
        internal GesDisc Disc { get; } = disc;

        internal GesQuantityUnit Unit { get; } = unit;

        public GesExternalTypeDefinition Definition => DiscDefinition;

        public GesValue? GetField(string fieldName) => fieldName switch
        {
            "center" => Disc.Center.ToValue(Unit),
            "radius" => GesValue.GesFloat(Disc.Radius, Unit),
            _ => null
        };
    }

    private sealed class ExtensionOverlay(IGesExtensionRegistry? next) : IGesExtensionRegistry
    {
        public IGesExtensionFunction? Resolve(GesExtensionReference reference)
        {
            if (reference is null) throw new ArgumentNullException(nameof(reference));
            if (reference.ExtensionName != Namespace) return next?.Resolve(reference);
            return reference.SignatureId switch
            {
                "geometry.size(_)" or "geometry.center(_)" or "geometry.bounds(_)" or "geometry.area(_)" or "geometry.centroid(_)"
                    or "geometry.contains(_,_)" or "geometry.intersects(_,_)" or "geometry.union(_,_)" or "geometry.inset(_,by)"
                    or "geometry.nearest(_,_)" or "geometry.distance(_,_)" => new Function(reference.FunctionName),
                _ => next?.Resolve(reference)
            };
        }
    }

    private sealed class Function(string name) : IGesExtensionFunction
    {
        public void Invoke(GesExtensionCall call)
        {
            var arguments = call.Arguments;
            var first = arguments[0].ExternalValue;
            var box = first as BoxValue;
            var poly = first as PolyValue;
            var disc = first as DiscValue;
            var unit = box?.Unit ?? poly?.Unit ?? disc?.Unit ?? GesQuantityUnit.UnitNone;
            var result = GesValue.GesNothing();
            switch (name)
            {
                case "size" when box is not null:
                    result = box.Box.Size.ToValue(unit);
                    break;
                case "center" when box is not null:
                    result = box.Box.Center.ToValue(unit);
                    break;
                case "center" when disc is not null:
                    result = disc.Disc.Center.ToValue(unit);
                    break;
                case "bounds" when disc is not null:
                    if (disc.Disc.Bounds is { } around) result = around.ToValue(unit);
                    break;
                case "area" when disc is not null:
                    if (disc.Disc.Area is { } surface) result = GesValue.GesFloat(surface);
                    break;
                case "bounds" when poly is not null:
                    if (poly.Poly.Bounds is { } bounds) result = bounds.ToValue(unit);
                    break;
                case "bounds" when box is not null:
                    result = arguments[0];
                    break;
                case "area" when poly is not null:
                    if (poly.Poly.Area is { } area) result = GesValue.GesFloat(area);
                    break;
                case "centroid" when poly is not null:
                    if (poly.Poly.Centroid is { } centroid) result = centroid.ToValue(unit);
                    break;
                case "contains" when PointOf(arguments, 1, unit) is { } point:
                    if (box is not null) result = GesValue.GesBoolean(box.Box.Contains(point));
                    else if ((poly?.Poly.Contains(point) ?? disc?.Disc.Contains(point)) is { } inside) result = GesValue.GesBoolean(inside);
                    break;
                case "intersects":
                    if (Shape(arguments[1].ExternalValue, unit) is { } other && Intersects(first, other) is { } touching) result = GesValue.GesBoolean(touching);
                    break;
                case "union" when box is not null && arguments[1].ExternalValue is BoxValue second && second.Unit == unit:
                    result = box.Box.Union(second.Box).ToValue(unit);
                    break;
                case "inset" when box is not null && IsNumber(arguments, 1, unit):
                    if (box.Box.Inset(arguments.GetAsNumber(1)) is { } inset) result = inset.ToValue(unit);
                    break;
                case "inset" when disc is not null && IsNumber(arguments, 1, unit):
                    if (disc.Disc.Inset(arguments.GetAsNumber(1)) is { } shrunk) result = shrunk.ToValue(unit);
                    break;
                case "nearest" when box is not null && PointOf(arguments, 1, unit) is { } target:
                    result = box.Box.Clamp(target).ToValue(unit);
                    break;
                case "nearest" when disc is not null && PointOf(arguments, 1, unit) is { } toward:
                    if (disc.Disc.Clamp(toward) is { } nearest) result = nearest.ToValue(unit);
                    break;
                case "distance" when poly is not null && PointOf(arguments, 1, unit) is { } from:
                    if (poly.Poly.DistanceTo(from) is { } distance) result = GesValue.GesFloat(distance, unit);
                    break;
                case "distance" when disc is not null && PointOf(arguments, 1, unit) is { } origin:
                    if (disc.Disc.DistanceTo(origin) is { } gap) result = GesValue.GesFloat(gap, unit);
                    break;
            }

            call.SetValue(result);
        }

        private static GesPoint? PointOf(in GesValueArguments arguments, int index, GesQuantityUnit unit)
            => arguments.KindAt(index) == GesBytecodeTypeKind.Point && arguments.UnitAt(index) == unit ? Point(arguments[index]) : null;

        private static IGesExternalValue? Shape(IGesExternalValue? value, GesQuantityUnit unit) => value switch
        {
            BoxValue box when box.Unit == unit => box,
            PolyValue poly when poly.Unit == unit => poly,
            DiscValue disc when disc.Unit == unit => disc,
            _ => null
        };

        // Boxes intersect as boxes; a 2D box meets a polygon as the polygon of its outline; a disc meets every shape as a disc.
        private static bool? Intersects(IGesExternalValue? first, IGesExternalValue second)
        {
            if (first is BoxValue a && second is BoxValue b) return a.Box.Intersects(b.Box);
            if (first is DiscValue disc) return Meets(disc.Disc, second);
            if (second is DiscValue other) return first is null ? null : Meets(other.Disc, first);
            var left = first is BoxValue boxA ? Outline(boxA.Box) : (first as PolyValue)?.Poly;
            var right = second is BoxValue boxB ? Outline(boxB.Box) : ((PolyValue)second).Poly;
            return left is null || right is null ? null : left.Intersects(right);
        }

        private static bool? Meets(GesDisc disc, IGesExternalValue shape) => shape switch
        {
            DiscValue other => disc.Intersects(other.Disc),
            BoxValue box => disc.Intersects(box.Box),
            PolyValue poly => disc.Intersects(poly.Poly),
            _ => null
        };

        private static GesPoly? Outline(GesBox box)
            => box.Is2D ? new GesPoly([box.Min, new GesPoint(box.Max.X, box.Min.Y), box.Max, new GesPoint(box.Min.X, box.Max.Y)]) : null;
    }
}
