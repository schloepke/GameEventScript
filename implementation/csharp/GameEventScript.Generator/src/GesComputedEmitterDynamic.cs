// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using GameEventScript.Api;

namespace GameEventScript.Generator;

// The dynamic form of a computed field: the exact GES value through the helpers that repeat the virtual machine, for the few
// fields whose operands binding cannot prove to give the same bits in plain binary64, such as a chain of whole numbers beyond
// 2^53, which the virtual machine adds as exact Int64 values.
internal sealed partial class GesComputedEmitter
{
    private void Dynamic(GesSchemaPlanner.Code code, string owner, GesResolvedComputed field, string access)
    {
        if (field.IsSpatial)
        {
            SpatialField(code, owner, field, access);
            return;
        }

        var self = _swift ? "self" : "this";
        var expression = Cast(Expression(field.Expression, self), field);
        var property = Property(field.Name, owner, _swift);
        var method = Method(field.Name, owner, _swift);
        var result = field.Kind == GesSchemaTypeKind.Boolean ? "IsTrue" : field.Optional ? "OptionalNumber" : "Number";
        Summary(code, field);
        if (_swift)
        {
            code.Line($"{(access.Length > 0 ? access + " " : "")}var {Member(property)}: {PropertyType(field, true)} {{ {_helper}.{Camel(result)}({method}()) }}");
            code.Blank();
            code.Line($"/// The exact GES value of `{field.Name}`, which other computed fields read.");
            code.Line($"func {method}() -> {_helper}.Value {{ {expression} }}");
        }
        else
        {
            code.Line($"{access} {PropertyType(field, false)} {property} => {_helper}.{result}({method}());");
            code.Blank();
            code.Line($"/// <summary>The exact GES value of <c>{Escape(field.Name)}</c>, which other computed fields read.</summary>");
            code.Line($"internal {_helper}.Value {method}() => {expression};");
        }
    }

    // A spatial field: a property of the runtime's type over a method that gives it, absent where the expression gives nothing.
    private void SpatialField(GesSchemaPlanner.Code code, string owner, GesResolvedComputed field, string access)
    {
        var expression = Expression(field.Expression, _swift ? "self" : "this");
        var property = Property(field.Name, owner, _swift);
        var method = Method(field.Name, owner, _swift);
        var type = SpatialType(field.Kind)!;
        var getter = field.Optional ? $"{method}()" : Call("Required" + field.Kind, $"{method}()");
        Summary(code, field);
        if (_swift)
        {
            code.Line($"{(access.Length > 0 ? access + " " : "")}var {Member(property)}: {PropertyType(field, true)} {{ {getter} }}");
            code.Blank();
            code.Line($"/// The GES value of `{field.Name}`, which other computed fields read.");
            code.Line($"func {method}() -> {type}? {{ {expression} }}");
        }
        else
        {
            code.Line($"{access} {PropertyType(field, false)} {property} => {getter};");
            code.Blank();
            code.Line($"/// <summary>The GES value of <c>{Escape(field.Name)}</c>, which other computed fields read.</summary>");
            code.Line($"internal {type}? {method}() => {expression};");
        }
    }

    private static string SpatialTypeOf(GesComputedType type) => "Ges" + type;

    private static bool IsSpatial(GesComputedType type) => type is GesComputedType.Vector or GesComputedType.Point or GesComputedType.Box or GesComputedType.Disc or GesComputedType.Poly;

    private string Flag(bool value) => value ? "true" : "false";

    // A script record converts the value to the field's type: a Percentage takes a finite number, a Boolean only true.
    private string Cast(string expression, GesResolvedComputed field) => field.Kind switch
    {
        GesSchemaTypeKind.Percentage => Call("CastPercentage", expression),
        GesSchemaTypeKind.Boolean => Call("CastBoolean", expression),
        _ => expression
    };

    // The exact value at a path, nothing where an optional field on the way is absent.
    private string Read(string root, string owner, string[] path, bool percentage, GesComputedType? spatial = null)
    {
        var reach = _access.Reach(root, owner, path);
        string read;
        if (reach.ComputedName.Length > 0)
        {
            var target = Array.Find(_access.Computed(reach.ComputedOwner), candidate => candidate.Name == reach.ComputedName)!;
            read = Analyze(reach.ComputedOwner, target) is null
                ? Join(reach.Text, Method(target.Name, reach.ComputedOwner, _swift)) + "()"
                : Call(target.Kind == GesSchemaTypeKind.Percentage ? "ReadPercentage" : "Read", Join(reach.Text, Member(Property(target.Name, reach.ComputedOwner, _swift))));
        }
        else if (spatial is GesComputedType.Poly) read = Call("ReadPoly", reach.Text);
        else if (spatial is not null) read = reach.Text;
        else read = Call(percentage ? "ReadPercentage" : "Read", reach.Text);

        if (reach.Conditions.Count == 0) return read;
        var conditions = new List<string>();
        foreach (var (_, condition) in reach.Conditions) conditions.Add(condition);
        if (spatial is { } type)
        {
            var optional = _swift ? read : $"({SpatialTypeOf(type)}?){read}";
            return $"({string.Join(" && ", conditions)} ? {optional} : {(_swift ? "nil" : "null")})";
        }

        return $"({string.Join(" && ", conditions)} ? {read} : {_helper}.{Camel("Nothing")})";
    }

    // Whether the record at a path is present.
    private string Present(string root, string owner, string[] path)
    {
        var reach = _access.Reach(root, owner, path);
        if (reach.Nullable) return reach.Text + (_swift ? " != nil" : " != null");
        if (reach.Conditions.Count == 0) return "true";
        var conditions = new List<string>();
        foreach (var (_, condition) in reach.Conditions) conditions.Add(condition);
        return string.Join(" && ", conditions);
    }

    private string Expression(GesComputedNode node, string root)
    {
        var children = node.Children;
        switch (node.Op)
        {
            case GesComputedOp.Literal:
                return node.Literal.Kind switch
                {
                    GesComputedKind.True or GesComputedKind.False => Call("Boolean", Flag(node.Literal.Kind == GesComputedKind.True)),
                    GesComputedKind.Integer => Call("Integer", node.Literal.Integer == long.MinValue ? _swift ? "Int64.min" : "long.MinValue" : node.Literal.Integer.ToString(CultureInfo.InvariantCulture)),
                    GesComputedKind.Percentage => Call("Percentage", node.Decimal.Canonical),
                    GesComputedKind.Float when node.Type == GesComputedType.Percentage => Call("Percentage", node.Decimal.Canonical),
                    _ => Call("Float", node.Decimal.Canonical)
                };
            case GesComputedOp.Field or GesComputedOp.Item:
                return Read(Root(node, root), Owner(node), node.Path, node.Type == GesComputedType.Percentage, IsSpatial(node.Type) ? node.Type : null);
            case GesComputedOp.Vector or GesComputedOp.Point:
                return Call(node.Op.ToString(), Expression(children[0], root), Expression(children[1], root), Expression(children[2], root));
            case GesComputedOp.Add or GesComputedOp.Subtract when IsSpatial(node.Type):
            {
                var name = (node.Op, children[0].Type, children[1].Type) switch
                {
                    (GesComputedOp.Add, GesComputedType.Vector, _) => "AddVectors",
                    (GesComputedOp.Subtract, GesComputedType.Vector, _) => "SubtractVectors",
                    (GesComputedOp.Add, _, _) => "MovePoint",
                    (_, _, GesComputedType.Vector) => "MovePointBack",
                    _ => "PointDifference"
                };
                return Call(name, Expression(children[0], root), Expression(children[1], root));
            }
            case GesComputedOp.Multiply when node.Type == GesComputedType.Vector:
                return children[0].Type == GesComputedType.Vector
                    ? Call("Scale", Expression(children[0], root), Expression(children[1], root))
                    : Call("Scale", Expression(children[1], root), Expression(children[0], root));
            case GesComputedOp.Divide when node.Type == GesComputedType.Vector:
                return Call("ScaleDown", Expression(children[0], root), Expression(children[1], root));
            case GesComputedOp.Negate when node.Type == GesComputedType.Vector:
                return Call("NegateVector", Expression(children[0], root));
            case GesComputedOp.Abs when children[0].Type == GesComputedType.Vector:
                return Call("Length", Expression(children[0], root));
            case GesComputedOp.Box or GesComputedOp.BoxAround or GesComputedOp.Disc:
                return Call(node.Op.ToString(), Expression(children[0], root), Expression(children[1], root));
            case GesComputedOp.DiscAround:
                return Call("DiscAround", Expression(children[0], root));
            case GesComputedOp.Poly:
            {
                var points = new List<string>();
                foreach (var child in children) points.Add(Expression(child, root));
                return Call("Poly", points.ToArray());
            }
            case GesComputedOp.PolyOf:
                return Points(node, root);
            case GesComputedOp.Part:
            {
                var target = Expression(children[0], root);
                return (children[0].Type, node.Path[0]) switch
                {
                    (GesComputedType.Vector or GesComputedType.Point, var axis) => Call("Axis", target, axis == "x" ? "0" : axis == "y" ? "1" : "2"),
                    (GesComputedType.Box, "lower") => Call("BoxLower", target),
                    (GesComputedType.Box, "upper") => Call("BoxUpper", target),
                    (GesComputedType.Box, "center") => Call("BoxCenter", target),
                    (GesComputedType.Box, _) => Call("BoxSize", target),
                    (_, "center") => Call("DiscCenter", target),
                    _ => Call("Radius", target)
                };
            }
            case GesComputedOp.PointCount:
                return Call("PointCount", Expression(children[0], root));
            case GesComputedOp.Choice when IsSpatial(node.Type):
            {
                var result = Expression(children[^1], root);
                for (var index = children.Length - 3; index >= 0; index -= 2)
                    result = $"({Call("IsTrue", Expression(children[index + 1], root))} ? {Expression(children[index], root)} : {result})";
                return result;
            }
            case GesComputedOp.Default when IsSpatial(node.Type):
                return $"({Expression(children[0], root)} ?? {Expression(children[1], root)})";
            case GesComputedOp.HasValue when IsSpatial(children[0].Type):
                return Call("Boolean", $"{Expression(children[0], root)} != {(_swift ? "nil" : "null")}");
            case GesComputedOp.Negate:
                return Call("Negate", Expression(children[0], root));
            case GesComputedOp.Not:
                return Call("Not", Expression(children[0], root));
            case GesComputedOp.Abs:
                return Call("Abs", Expression(children[0], root));
            case GesComputedOp.Add or GesComputedOp.Subtract:
                return Call(node.Op.ToString(), Expression(children[0], root), Expression(children[1], root), Flag(node.First), Flag(node.Second));
            case GesComputedOp.Multiply or GesComputedOp.Divide:
                return Call(node.Op.ToString(), Expression(children[0], root), Expression(children[1], root), Flag(node.First), Flag(node.Second), Flag(node.Third));
            case GesComputedOp.Less or GesComputedOp.LessOrEqual or GesComputedOp.Greater or GesComputedOp.GreaterOrEqual:
                return Call("Compare", Expression(children[0], root), Expression(children[1], root), Flag(node.First), ((int)(node.Op - GesComputedOp.Less)).ToString(CultureInfo.InvariantCulture));
            case GesComputedOp.Equal or GesComputedOp.NotEqual:
                return Call("Equal", Expression(children[0], root), Expression(children[1], root), Flag(node.First), Flag(node.Op == GesComputedOp.NotEqual));
            case GesComputedOp.And or GesComputedOp.Or:
                return Call(node.Op.ToString(), Expression(children[0], root), Expression(children[1], root));
            case GesComputedOp.Choice:
            {
                var result = Expression(children[^1], root);
                for (var index = children.Length - 3; index >= 0; index -= 2)
                    result = Call("When", Expression(children[index + 1], root), Expression(children[index], root), result);
                return result;
            }
            case GesComputedOp.Min or GesComputedOp.Max:
            {
                var result = Expression(children[0], root);
                for (var index = 1; index < children.Length; index++)
                    result = Call("Extreme", result, Expression(children[index], root), Flag(node.First), Flag(node.Op == GesComputedOp.Max));
                return result;
            }
            case GesComputedOp.Clamp:
                return Call("Clamp", Expression(children[0], root), Expression(children[1], root), Expression(children[2], root), Flag(node.First));
            case GesComputedOp.Default:
                return Call("Fallback", Expression(children[0], root), Expression(children[1], root));
            case GesComputedOp.HasValue:
            {
                var child = children[0];
                if (child.Type != GesComputedType.Record) return Call("Has", Expression(child, root));
                return Call("Boolean", Present(Root(child, root), Owner(child), child.Path));
            }
            case GesComputedOp.Count:
            {
                var list = _access.Reach(root, _owner, children[0].Path).Text;
                return _swift ? Call("Integer", $"Int64({list}.count)") : Call("Integer", $"{list}.Count");
            }
            case GesComputedOp.Sum:
                return Sum(node, root);
            default:
            {
                var arguments = new List<string>();
                foreach (var child in children) arguments.Add(Expression(child, root));
                if (node.Op is GesComputedOp.Sin or GesComputedOp.Cos or GesComputedOp.Tan) arguments.Add(Flag(node.First));
                return Call(FunctionHelper(node.Op), arguments.ToArray());
            }
        }
    }

    // The points of a polygon from a list field: its Points, or each element projected to a Point; nothing where one is.
    private string Points(GesComputedNode node, string root)
    {
        var list = _access.Reach(root, _owner, node.Children[0].Path).Text;
        var name = $"__{_field}Points{_sums.Count.ToString(CultureInfo.InvariantCulture)}";
        var previous = _element;
        _element = _access.ElementOwner(_owner, node.Children[0].Path);
        var value = node.Children.Length > 1 ? Expression(node.Children[1], root) : "item";
        _element = previous;
        var body = new StringBuilder();
        if (_swift)
        {
            body.Append($"private func {name}() -> GesPoly? {{\n");
            body.Append("    var points: [GesPoint] = []\n");
            body.Append($"    for item in {list} {{\n");
            body.Append($"        guard let point = {value} as GesPoint? else {{ return nil }}\n");
            body.Append("        points.append(point)\n");
            body.Append("    }\n");
            body.Append("    return GesPoly(points)\n");
            body.Append('}');
        }
        else
        {
            body.Append($"private GesPoly? {name}()\n");
            body.Append("{\n");
            body.Append("    var points = new List<GesPoint>();\n");
            body.Append($"    foreach (var item in {list})\n");
            body.Append("    {\n");
            body.Append($"        if ((GesPoint?){value} is not {{ }} point) return null;\n");
            body.Append("        points.Add(point);\n");
            body.Append("    }\n");
            body.Append('\n');
            body.Append("    return new GesPoly(points);\n");
            body.Append('}');
        }

        _sums.Add(body.ToString());
        return $"{name}()";
    }

    // A sum adds the projected elements from left to right, starting with the first one, and is the integer 0 for none.
    private string Sum(GesComputedNode node, string root)
    {
        var list = _access.Reach(root, _owner, node.Children[0].Path).Text;
        var name = $"__{_field}Sum{_sums.Count.ToString(CultureInfo.InvariantCulture)}";
        var previous = _element;
        _element = _access.ElementOwner(_owner, node.Children[0].Path);
        var value = node.Children.Length > 1 ? Expression(node.Children[1], root) : Call(node.Type == GesComputedType.Percentage ? "ReadPercentage" : "Read", "item");
        _element = previous;
        var body = new StringBuilder();
        if (_swift)
        {
            body.Append($"private func {name}() -> {_helper}.Value {{\n");
            body.Append($"    var sum = {_helper}.integer(0)\n");
            body.Append("    var first = true\n");
            body.Append($"    for item in {list} {{\n");
            body.Append($"        let value = {value}\n");
            body.Append($"        sum = first ? value : {_helper}.add(sum, value, true, true)\n");
            body.Append("        first = false\n");
            body.Append("    }\n");
            body.Append("    return sum\n");
            body.Append('}');
        }
        else
        {
            body.Append($"private {_helper}.Value {name}()\n");
            body.Append("{\n");
            body.Append($"    var sum = {_helper}.Integer(0);\n");
            body.Append("    var first = true;\n");
            body.Append($"    foreach (var item in {list})\n");
            body.Append("    {\n");
            body.Append($"        var value = {value};\n");
            body.Append($"        sum = first ? value : {_helper}.Add(sum, value, true, true);\n");
            body.Append("        first = false;\n");
            body.Append("    }\n");
            body.Append('\n');
            body.Append("    return sum;\n");
            body.Append('}');
        }

        _sums.Add(body.ToString());
        return $"{name}()";
    }
}
