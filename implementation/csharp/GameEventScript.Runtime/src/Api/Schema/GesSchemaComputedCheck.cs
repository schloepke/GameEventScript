// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Globalization;
using GameEventScript.Syntax;
using Code = GameEventScript.Syntax.GesSchemaDiagnostic;

namespace GameEventScript.Api;

// A field written `computed by`: its type, its checked expression, and where it was written, which a schema that takes it
// keeps for its constants and positions.
internal sealed class GesResolvedComputed
{
    internal string Name { get; init; } = "";

    internal GesSchemaTypeKind Kind { get; init; }

    internal GesQuantityUnit Unit { get; init; }

    internal bool Optional { get; init; }

    internal GesComputedNode Expression { get; set; } = null!;

    internal int Offset { get; init; }

    internal GesFileScope Scope { get; init; } = null!;

    internal GesSchemaFieldSyntax Syntax { get; init; } = null!;

    // The lets of the schema that wrote it, which its expression may name.
    internal GesResolvedLet[] Lets { get; init; } = [];

    internal GesComputedType Type => Kind switch
    {
        GesSchemaTypeKind.Boolean => GesComputedType.Boolean,
        GesSchemaTypeKind.Percentage => GesComputedType.Percentage,
        _ => Spatial(Kind) ?? GesComputedType.Number
    };

    // The computed type of a spatial schema type, or null for another one.
    internal static GesComputedType? Spatial(GesSchemaTypeKind kind) => kind switch
    {
        GesSchemaTypeKind.Vector => GesComputedType.Vector,
        GesSchemaTypeKind.Point => GesComputedType.Point,
        GesSchemaTypeKind.Box => GesComputedType.Box,
        GesSchemaTypeKind.Disc => GesComputedType.Disc,
        GesSchemaTypeKind.Poly => GesComputedType.Poly,
        _ => null
    };

    internal bool IsSpatial => Spatial(Kind) is not null;
}

internal sealed partial class GesFileScope
{
    // A checked subexpression and, for a path, what it reaches: the schema of a record, or the alternatives of a list's elements.
    private readonly struct Checked(GesComputedNode node, GesResolvedDeclaration? record = null, GesResolvedType[]? element = null, bool optional = false)
    {
        internal GesComputedNode Node { get; } = node;

        internal GesResolvedDeclaration? Record { get; } = record;

        internal GesResolvedType[]? Element { get; } = element;

        // Whether a field on the path is optional.
        internal bool Optional { get; } = optional;
    }

    // What a name inside an expression can mean: the schema's fields and earlier computed fields, the lets of the schema that
    // wrote the expression, the constants of its file, and the variable of the projection being checked.
    private sealed class ComputedContext(GesResolvedDeclaration declaration, List<GesResolvedComputed> earlier, GesResolvedComputed current)
    {
        internal GesResolvedDeclaration Declaration { get; } = declaration;

        internal List<GesResolvedComputed> Earlier { get; } = earlier;

        internal GesResolvedComputed Current { get; } = current;

        internal string Variable { get; set; } = "";

        internal Checked? Element { get; set; }
    }

    // Checks the computed fields of a composed schema in order: own ones where they stand and taken ones where their `with`
    // stands. Every expression is checked against this schema's fields, with the lets and constants of where it was written.
    internal static GesResolvedComputed[] ResolveComputed(GesResolvedDeclaration declaration, List<GesResolvedComputed> entries, HashSet<string> names)
    {
        var resolved = new List<GesResolvedComputed>();
        foreach (var entry in entries)
        {
            var scope = entry.Scope;
            var syntax = entry.Syntax;
            if (!names.Add(entry.Name)) throw scope.Error(Code.DuplicateName, entry.Offset, $"'{entry.Name}' is declared twice in '{declaration.Name}'.");
            var type = syntax.Alternatives;
            if (type.Length != 1 || type[0].Kind is not (GesSchemaTypeKind.Number or GesSchemaTypeKind.Quantity or GesSchemaTypeKind.Percentage or GesSchemaTypeKind.Boolean
                    or GesSchemaTypeKind.Vector or GesSchemaTypeKind.Point or GesSchemaTypeKind.Box or GesSchemaTypeKind.Disc or GesSchemaTypeKind.Poly)
                || type[0].Constraints is { } constraints && (constraints.Integer || constraints.Lower is not null || constraints.Step is not null || constraints.Delta is not null)
                || type[0].Axes is not null || type[0].Max is not null || type[0].MaxName.Length > 0)
                throw scope.Error(Code.InvalidExpression, entry.Offset, "A computed field is a Number, Quantity, Percentage, Boolean, Vector, Point, Box, Disc or Poly without constraints, axes or max.");
            if (syntax.Marker == GesSchemaFieldMarker.Key) throw scope.Error(Code.InvalidExpression, entry.Offset, "A computed field is no key.");
            if (entry.Optional && entry.Kind == GesSchemaTypeKind.Boolean)
                throw scope.Error(Code.InvalidExpression, entry.Offset, "A computed Boolean is never absent, as a record reads nothing as false; it is not optional.");
            var context = new ComputedContext(declaration, resolved, entry);
            var result = scope.CheckExpression(syntax.Computed!, context).Node;
            var expected = entry.Type;
            if (result.Type != expected)
                throw scope.Error(Code.InvalidExpression, syntax.Computed!.Offset, $"The expression gives a {Describe(result)}, but '{entry.Name}' is a {entry.Kind}.");
            if (expected == GesComputedType.Number && result.Unit != entry.Unit)
                throw scope.Error(Code.UnitMismatch, syntax.Computed!.Offset, $"The expression gives a {Describe(result)}, but '{entry.Name}' is a {(entry.Unit == GesQuantityUnit.UnitNone ? "Number" : "Quantity(" + entry.Unit.ToSuffix() + ")")}.");
            if (entry.IsSpatial && result.Unit != entry.Unit)
                throw scope.Error(Code.UnitMismatch, syntax.Computed!.Offset, $"The expression gives a {Describe(result)}, but '{entry.Name}' is a {DescribeSpatial(expected, entry.Unit)}.");
            scope.CheckAlone(syntax.Computed!, result, alone: true);
            Guard(result, []);
            if (result.MayBeAbsent && !entry.Optional)
                throw scope.Error(Code.ComputedMayBeAbsent, syntax.Computed!.Offset, entry.Kind == GesSchemaTypeKind.Boolean
                    ? $"'{entry.Name}' reads an optional field that may be absent; guard it with has value or give a fallback with default."
                    : $"'{entry.Name}' reads an optional field that may be absent; mark it optional, guard the field with has value or give a fallback with default.");
            entry.Expression = result;
            resolved.Add(entry);
        }

        return resolved.ToArray();
    }

    // Which reads `has value` proves present, and which nodes may give nothing for an absent optional field. A `has value` of a
    // path proves it and every leading part of it present on the right of `and` and in the value its `when` condition selects.
    private static void Guard(GesComputedNode node, List<(GesComputedOp Op, string[] Path)> present)
    {
        var children = node.Children;
        switch (node.Op)
        {
            case GesComputedOp.Field or GesComputedOp.Item:
                foreach (var (op, path) in present)
                    if (op == node.Op && path.Length > node.Present && StartsWith(node.Path, path)) node.Present = path.Length;
                for (var index = node.Present; index < node.OptionalSteps.Length; index++) node.MayBeAbsent |= node.OptionalSteps[index];
                return;
            case GesComputedOp.HasValue:
                Guard(children[0], present);
                return;
            case GesComputedOp.And:
            {
                Guard(children[0], present);
                var proven = new List<(GesComputedOp, string[])>(present);
                Proven(children[0], proven);
                Guard(children[1], proven);
                node.MayBeAbsent = children[0].MayBeAbsent || children[1].MayBeAbsent;
                return;
            }
            case GesComputedOp.Choice:
                for (var index = 0; index + 1 < children.Length; index += 2)
                {
                    Guard(children[index + 1], present);
                    var proven = new List<(GesComputedOp, string[])>(present);
                    Proven(children[index + 1], proven);
                    Guard(children[index], proven);
                    node.MayBeAbsent |= children[index].MayBeAbsent;
                }

                Guard(children[^1], present);
                node.MayBeAbsent |= children[^1].MayBeAbsent;
                return;
            case GesComputedOp.Default:
                Guard(children[0], present);
                Guard(children[1], present);
                node.MayBeAbsent = children[1].MayBeAbsent;
                return;
            case GesComputedOp.Sum or GesComputedOp.PolyOf:
            {
                // A projection reads the element afresh, so no proof about an element carries into it.
                Guard(children[0], present);
                if (children.Length > 1)
                {
                    Guard(children[1], present.FindAll(entry => entry.Op == GesComputedOp.Field));
                    node.MayBeAbsent = children[1].MayBeAbsent;
                }

                return;
            }
            case GesComputedOp.Count:
                Guard(children[0], present);
                return;
            default:
                foreach (var child in children)
                {
                    Guard(child, present);
                    node.MayBeAbsent |= child.MayBeAbsent;
                }

                return;
        }
    }

    // The paths that a true condition proves present: its `has value` conjuncts.
    private static void Proven(GesComputedNode node, List<(GesComputedOp, string[])> proven)
    {
        if (node.Op == GesComputedOp.And)
        {
            Proven(node.Children[0], proven);
            Proven(node.Children[1], proven);
        }
        else if (node.Op == GesComputedOp.HasValue && node.Children[0].Op is GesComputedOp.Field or GesComputedOp.Item)
            proven.Add((node.Children[0].Op, node.Children[0].Path));
    }

    private static bool StartsWith(string[] path, string[] prefix)
    {
        if (prefix.Length > path.Length) return false;
        for (var index = 0; index < prefix.Length; index++)
            if (path[index] != prefix[index]) return false;
        return true;
    }

    private static string Describe(GesComputedNode node) => node.Type switch
    {
        GesComputedType.Number => node.Unit == GesQuantityUnit.UnitNone ? "Number" : "Quantity(" + node.Unit.ToSuffix() + ")",
        GesComputedType.List => "List",
        GesComputedType.Record => "record",
        GesComputedType.Vector or GesComputedType.Point or GesComputedType.Box or GesComputedType.Disc or GesComputedType.Poly => DescribeSpatial(node.Type, node.Unit),
        _ => node.Type.ToString()
    };

    private static string DescribeSpatial(GesComputedType type, GesQuantityUnit unit)
        => type + (unit == GesQuantityUnit.UnitNone ? "" : "(" + unit.ToSuffix() + ")");

    private static bool IsSpatial(GesComputedType type) => type is GesComputedType.Vector or GesComputedType.Point or GesComputedType.Box or GesComputedType.Disc or GesComputedType.Poly;

    // A sum of quantities or percentages is the unitless 0 for an empty list, which only the final conversion of the field
    // turns into a value of the field's type. So such a sum stands alone: as the expression, a value of a choice, or a side
    // of `default`.
    private void CheckAlone(GesSchemaExpressionSyntax syntax, GesComputedNode node, bool alone)
    {
        if (node.Op == GesComputedOp.Sum && !alone && (node.Type == GesComputedType.Percentage || node.Unit != GesQuantityUnit.UnitNone))
            throw Error(Code.InvalidExpression, syntax.Offset, "A sum of quantities or percentages is 0 without a unit for an empty list, so it stands alone: as the expression, a value of a choice or a side of default.");
        for (var index = 0; index < node.Children.Length && index < syntax.Children.Length; index++)
        {
            var passes = node.Op switch
            {
                GesComputedOp.Choice => index % 2 == 0 || index == node.Children.Length - 1,
                GesComputedOp.Default => true,
                _ => false
            };
            CheckAlone(ChildSyntax(syntax, node, index), node.Children[index], alone && passes);
        }
    }

    // The syntax of a child; a Name or Member has none of its own path.
    private static GesSchemaExpressionSyntax ChildSyntax(GesSchemaExpressionSyntax syntax, GesComputedNode node, int index)
        => node.Op is GesComputedOp.Field or GesComputedOp.Item ? syntax : syntax.Children[index];

    private Checked CheckExpression(GesSchemaExpressionSyntax syntax, ComputedContext context)
    {
        var children = syntax.Children;
        switch (syntax.Kind)
        {
            case GesSchemaExpressionKind.Literal:
                return new Checked(Literal(syntax.Literal!.Value, syntax.Literal.Unit, syntax.Literal.IsPercent));
            case GesSchemaExpressionKind.Boolean:
                return new Checked(new GesComputedNode { Op = GesComputedOp.Literal, Type = GesComputedType.Boolean, Literal = GesComputedValue.OfBoolean(syntax.Boolean) });
            case GesSchemaExpressionKind.Constant:
                if (!_constants.TryGetValue(syntax.Name, out var constant)) throw Error(Code.UnknownName, syntax.Offset, $"Constant '${syntax.Name}' is not declared in this file.");
                return new Checked(Literal(constant.Value, constant.Unit, constant.IsPercent));
            case GesSchemaExpressionKind.Name:
                return Name(syntax, context);
            case GesSchemaExpressionKind.Member:
            {
                var target = CheckExpression(children[0], context);
                if (IsSpatial(target.Node.Type)) return Part(syntax, target.Node);
                if (target.Node.Op is not (GesComputedOp.Field or GesComputedOp.Item) || target.Record is null)
                    throw Error(Code.InvalidExpression, syntax.Offset, $"'.{syntax.Name}' reads a field of a schema, which this is not.");
                var field = Array.Find(target.Record.Fields, candidate => candidate.Name == syntax.Name);
                var computed = Array.Find(target.Record.Computed, candidate => candidate.Name == syntax.Name);
                if (field is null && computed is null) throw Error(Code.UnknownName, syntax.Offset, $"'{syntax.Name}' is no field of '{target.Record.Name}'.");
                var path = new string[target.Node.Path.Length + 1];
                target.Node.Path.CopyTo(path, 0);
                path[^1] = syntax.Name;
                var reached = field is not null ? Reach(field, target.Node.Op, path, syntax.Offset) : Reach(computed!, target.Node.Op, path);
                var steps = new bool[path.Length];
                target.Node.OptionalSteps.CopyTo(steps, 0);
                steps[^1] = reached.Optional;
                reached.Node.OptionalSteps = steps;
                return new Checked(reached.Node, reached.Record, reached.Element, target.Optional || reached.Optional);
            }
            case GesSchemaExpressionKind.Negate or GesSchemaExpressionKind.Abs:
            {
                var operand = Value(children[0], context);
                if (operand.Type == GesComputedType.Vector)
                    return syntax.Kind == GesSchemaExpressionKind.Negate
                        ? Node(GesComputedOp.Negate, GesComputedType.Vector, operand.Unit, operand)
                        : Node(GesComputedOp.Abs, GesComputedType.Number, operand.Unit, operand);
                if (operand.Type is not (GesComputedType.Number or GesComputedType.Percentage)) throw Mismatch(syntax, "a number, percentage or vector", operand);
                return Node(syntax.Kind == GesSchemaExpressionKind.Negate ? GesComputedOp.Negate : GesComputedOp.Abs, operand.Type, operand.Unit, operand);
            }
            case GesSchemaExpressionKind.Not:
            {
                var operand = Value(children[0], context);
                if (operand.Type != GesComputedType.Boolean) throw Mismatch(syntax, "a Boolean", operand);
                return Node(GesComputedOp.Not, GesComputedType.Boolean, GesQuantityUnit.UnitNone, operand);
            }
            case GesSchemaExpressionKind.Binary:
                return Binary(syntax, Value(children[0], context), Value(children[1], context));
            case GesSchemaExpressionKind.Choice:
            {
                var parts = new GesComputedNode[children.Length];
                for (var index = 0; index < children.Length; index++) parts[index] = Value(children[index], context);
                for (var index = 1; index < parts.Length; index += 2)
                    if (parts[index].Type != GesComputedType.Boolean) throw Mismatch(children[index], "a Boolean condition", parts[index]);
                var first = parts[0];
                for (var index = 2; index < parts.Length; index += 2)
                    if (!SameType(first, parts[index])) throw Different(children[index], first, parts[index]);
                return Node(GesComputedOp.Choice, first.Type, first.Unit, parts);
            }
            case GesSchemaExpressionKind.Min or GesSchemaExpressionKind.Max or GesSchemaExpressionKind.Clamp:
            {
                var parts = new GesComputedNode[children.Length];
                for (var index = 0; index < children.Length; index++)
                {
                    parts[index] = Value(children[index], context);
                    if (parts[index].Type != GesComputedType.Number) throw Mismatch(children[index], "a number", parts[index]);
                    if (parts[index].Unit != parts[0].Unit) throw Different(children[index], parts[0], parts[index]);
                }

                var op = syntax.Kind switch { GesSchemaExpressionKind.Min => GesComputedOp.Min, GesSchemaExpressionKind.Max => GesComputedOp.Max, _ => GesComputedOp.Clamp };
                return new Checked(new GesComputedNode { Op = op, Type = GesComputedType.Number, Unit = parts[0].Unit, Children = parts, First = true });
            }
            case GesSchemaExpressionKind.Default:
            {
                var value = Value(children[0], context);
                var fallback = Value(children[1], context);
                if (!SameType(value, fallback)) throw Different(children[1], value, fallback);
                return Node(GesComputedOp.Default, value.Type, value.Unit, value, fallback);
            }
            case GesSchemaExpressionKind.Function:
                return Function(syntax, context);
            case GesSchemaExpressionKind.Construct:
                return Construct(syntax, context);
            case GesSchemaExpressionKind.List:
                throw Error(Code.InvalidExpression, syntax.Offset, "A list in a computed expression holds the points of a :Poly.");
            case GesSchemaExpressionKind.Select:
                throw Error(Code.InvalidExpression, syntax.Offset, "A projection with :select gives the points of a :Poly.");
            case GesSchemaExpressionKind.HasValue:
            {
                var operand = CheckExpression(children[0], context);
                var node = operand.Node;
                if (node.Type == GesComputedType.List || node.Type == GesComputedType.Record && operand.Record is null)
                    throw Error(Code.InvalidExpression, children[0].Offset, "has value takes a number, percentage, Boolean or schema field; a list or text is never read by a computed field.");
                return Node(GesComputedOp.HasValue, GesComputedType.Boolean, GesQuantityUnit.UnitNone, node);
            }
            default:
                return Aggregate(syntax, context);
        }
    }

    // A value that arithmetic, comparisons and choices read: a number, percentage or Boolean.
    private GesComputedNode Value(GesSchemaExpressionSyntax syntax, ComputedContext context)
    {
        var node = CheckExpression(syntax, context).Node;
        if (node.Type is GesComputedType.Record or GesComputedType.List)
            throw Error(Code.InvalidExpression, syntax.Offset, $"A {Describe(node)} is read only through its fields, an aggregate or has value.");
        return node;
    }

    private Checked Aggregate(GesSchemaExpressionSyntax syntax, ComputedContext context)
    {
        var children = syntax.Children;
        var source = CheckExpression(children[0], context);
        if (source.Node.Op == GesComputedOp.Part)
        {
            if (syntax.Kind != GesSchemaExpressionKind.Count) throw Error(Code.InvalidExpression, syntax.Offset, "The points of a Poly take only [:count].");
            return Node(GesComputedOp.PointCount, GesComputedType.Number, GesQuantityUnit.UnitNone, source.Node.Children[0]);
        }

        if (source.Node.Type != GesComputedType.List) throw Error(Code.InvalidExpression, children[0].Offset, "An aggregate reads a list field.");
        if (source.Optional) throw Error(Code.InvalidExpression, children[0].Offset, "An aggregate reads a list that is no optional field and lies in none.");
        if (syntax.Kind == GesSchemaExpressionKind.Count) return Node(GesComputedOp.Count, GesComputedType.Number, GesQuantityUnit.UnitNone, source.Node);
        var element = source.Element!;
        if (element.Length != 1) throw Error(Code.InvalidExpression, children[0].Offset, "A sum reads a list without a union.");
        var item = Element(element[0], children[0].Offset);
        if (children.Length == 1)
        {
            if (item.Node.Type is not (GesComputedType.Number or GesComputedType.Percentage)) throw Error(Code.InvalidExpression, children[0].Offset, "A sum without a projection reads a list of numbers or percentages.");
            return Node(GesComputedOp.Sum, item.Node.Type, item.Node.Unit, source.Node);
        }

        if (context.Variable.Length > 0) throw Error(Code.InvalidExpression, syntax.Offset, "A projection does not contain another one.");
        if (IsTaken(context, syntax.Name)) throw Error(Code.DuplicateName, syntax.Offset, $"'{syntax.Name}' already names a field or let; the variable needs another name.");
        context.Variable = syntax.Name;
        context.Element = item;
        try
        {
            var projection = Value(children[1], context);
            if (projection.Type is not (GesComputedType.Number or GesComputedType.Percentage)) throw Mismatch(children[1], "a number or percentage", projection);
            return Node(GesComputedOp.Sum, projection.Type, projection.Unit, source.Node, projection);
        }
        finally
        {
            context.Variable = "";
            context.Element = null;
        }
    }

    private static bool IsTaken(ComputedContext context, string name)
        => Array.Exists(context.Declaration.Fields, field => field.Name == name) || context.Earlier.Exists(field => field.Name == name)
            || context.Current.Name == name || Array.Exists(context.Current.Lets, let => let.Name == name);

    // An element of a list as the projection variable: a record of a schema or a number.
    private Checked Element(GesResolvedType type, int offset)
    {
        var node = TypeNode(type, GesComputedOp.Item, [], offset, out var record, out var element);
        return new Checked(node, record, element);
    }

    private Checked Name(GesSchemaExpressionSyntax syntax, ComputedContext context)
    {
        var name = syntax.Name;
        if (context.Variable.Length > 0 && name == context.Variable) return context.Element!.Value;
        if (Array.Find(context.Declaration.Fields, field => field.Name == name) is { } stored) return Reach(stored, GesComputedOp.Field, [name], syntax.Offset);
        if (context.Earlier.Find(field => field.Name == name) is { } earlier) return Reach(earlier, GesComputedOp.Field, [name]);
        if (name == context.Current.Name || Array.Exists(context.Declaration.Computed, field => field.Name == name) || context.Declaration.Syntax.Computed is var computed && Array.Exists(computed, field => field.Name == name))
            throw Error(Code.InvalidExpression, syntax.Offset, $"'{name}' is computed after '{context.Current.Name}'; a computed field reads only fields and the computed fields before it.");
        if (Array.Find(context.Current.Lets, let => let.Name == name) is { } let) return new Checked(Literal(let.Value, let.Unit, let.Kind == GesSchemaTypeKind.Percentage));
        throw Error(Code.UnknownName, syntax.Offset, $"'{name}' is neither a field nor a let of '{context.Declaration.Name}'.");
    }

    private Checked Reach(GesResolvedField field, GesComputedOp op, string[] path, int offset)
    {
        if (field.Alternatives.Length != 1) throw Error(Code.InvalidExpression, offset, $"'{field.Name}' is a union, which a computed field does not read.");
        var node = TypeNode(field.Alternatives[0], op, path, offset, out var record, out var element);
        node.OptionalSteps = [field.Marker == GesSchemaFieldMarker.Optional];
        return new Checked(node, record, element, field.Marker == GesSchemaFieldMarker.Optional);
    }

    private static Checked Reach(GesResolvedComputed field, GesComputedOp op, string[] path)
        => new(new GesComputedNode { Op = op, Type = field.Type, Unit = field.Kind == GesSchemaTypeKind.Quantity || field.IsSpatial ? field.Unit : GesQuantityUnit.UnitNone, Path = path, OptionalSteps = [field.Optional] }, optional: field.Optional);

    private GesComputedNode TypeNode(GesResolvedType type, GesComputedOp op, string[] path, int offset, out GesResolvedDeclaration? record, out GesResolvedType[]? element)
    {
        record = null;
        element = null;
        switch (type.Kind)
        {
            case GesSchemaTypeKind.Number:
                return new GesComputedNode { Op = op, Type = GesComputedType.Number, Path = path };
            case GesSchemaTypeKind.Quantity:
                return new GesComputedNode { Op = op, Type = GesComputedType.Number, Unit = type.Unit, Path = path };
            case GesSchemaTypeKind.Percentage:
                return new GesComputedNode { Op = op, Type = GesComputedType.Percentage, Path = path };
            case GesSchemaTypeKind.Boolean:
                return new GesComputedNode { Op = op, Type = GesComputedType.Boolean, Path = path };
            case GesSchemaTypeKind.Vector or GesSchemaTypeKind.Point or GesSchemaTypeKind.Box or GesSchemaTypeKind.Disc or GesSchemaTypeKind.Poly:
                return new GesComputedNode { Op = op, Type = GesResolvedComputed.Spatial(type.Kind)!.Value, Unit = type.Unit, Path = path };
            case GesSchemaTypeKind.List when type.Element is { } alternatives:
                element = alternatives;
                return new GesComputedNode { Op = op, Type = GesComputedType.List, Path = path };
            case GesSchemaTypeKind.Reference when type.Reference is { Kind: GesSchemaKind.Schema } reference:
                record = reference;
                return new GesComputedNode { Op = op, Type = GesComputedType.Record, Path = path };
            default:
                // Text, tags and other types are not read; only has value may name them, and it rejects them.
                return new GesComputedNode { Op = op, Type = GesComputedType.Record, Path = path };
        }
    }

    private static GesComputedNode Literal(GesSchemaDecimal value, GesQuantityUnit unit, bool percent)
    {
        var number = double.Parse(value.Significand.ToString(CultureInfo.InvariantCulture) + "e" + value.Exponent.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
        GesComputedValue literal;
        if (percent) literal = GesComputedValue.OfPercentage(number);
        else if (Exact(value) is { } integer) literal = GesComputedValue.OfInteger(integer);
        else literal = GesComputedValue.OfFloat(number);
        return new GesComputedNode
        {
            Op = GesComputedOp.Literal,
            Type = percent ? GesComputedType.Percentage : GesComputedType.Number,
            Unit = percent ? GesQuantityUnit.UnitNone : unit,
            Literal = literal,
            Decimal = value
        };
    }

    // A whole decimal within Int64, which a literal keeps exactly.
    private static long? Exact(GesSchemaDecimal value)
    {
        if (value.Exponent < 0) return null;
        var result = value.Significand;
        for (var index = 0; index < value.Exponent; index++)
        {
            if (Runtime.GesNumber.MultiplyExact(result, 10) is not { } next) return null;
            result = next;
        }

        return result;
    }

    private Checked Binary(GesSchemaExpressionSyntax syntax, GesComputedNode left, GesComputedNode right)
    {
        var a = left.Unit;
        var b = right.Unit;
        var number = GesComputedType.Number;
        var percentage = GesComputedType.Percentage;
        if (IsSpatial(left.Type) || IsSpatial(right.Type)) return SpatialBinary(syntax, left, right);
        switch (syntax.Operator)
        {
            case GesSchemaBinaryOperator.Add or GesSchemaBinaryOperator.Subtract:
            {
                var op = syntax.Operator == GesSchemaBinaryOperator.Add ? GesComputedOp.Add : GesComputedOp.Subtract;
                if (left.Type == number && right.Type == number && a != b) throw Units(syntax, left, right);
                if (left.Type == number && right.Type == percentage && !a.AcceptsPercentage()) throw Error(Code.UnitMismatch, syntax.Offset, "A percentage does not scale a decibel level.");
                if (!(left.Type == number && right.Type is GesComputedType.Number or GesComputedType.Percentage || left.Type == percentage && right.Type == percentage))
                    throw Error(Code.InvalidExpression, syntax.Offset, $"{Describe(left)} {GesSchemaFormatter.Symbol(syntax.Operator)} {Describe(right)} has no value; a percentage stands right of a number or beside another percentage.");
                return Flags(op, left.Type, a, left, right, a == b, a.AcceptsPercentage(), false);
            }
            case GesSchemaBinaryOperator.Multiply:
            {
                var product = ProductUnit(a, b);
                if (left.Type == number && right.Type == number && product is null) throw Units(syntax, left, right);
                if (left.Type == number && right.Type == percentage && !a.AcceptsPercentage() || left.Type == percentage && right.Type == number && !b.AcceptsPercentage())
                    throw Error(Code.UnitMismatch, syntax.Offset, "A percentage does not scale a decibel level.");
                if (left.Type is not (GesComputedType.Number or GesComputedType.Percentage) || right.Type is not (GesComputedType.Number or GesComputedType.Percentage))
                    throw Error(Code.InvalidExpression, syntax.Offset, $"{Describe(left)} * {Describe(right)} has no value.");
                var type = left.Type == percentage && right.Type == percentage ? percentage : number;
                var unit = left.Type == percentage ? b : right.Type == percentage ? a : product!.Value;
                return Flags(GesComputedOp.Multiply, type, unit, left, right, product is not null, a.AcceptsPercentage(), b.AcceptsPercentage());
            }
            case GesSchemaBinaryOperator.Divide:
            {
                var quotient = QuotientUnit(a, b);
                if (left.Type == number && right.Type == number && quotient is null) throw Units(syntax, left, right);
                if (left.Type == percentage && right.Type == number && b != GesQuantityUnit.UnitNone) throw Units(syntax, left, right);
                if (left.Type == number && right.Type == percentage && !a.AcceptsPercentage()) throw Error(Code.UnitMismatch, syntax.Offset, "A percentage does not scale a decibel level.");
                if (left.Type is not (GesComputedType.Number or GesComputedType.Percentage) || right.Type is not (GesComputedType.Number or GesComputedType.Percentage))
                    throw Error(Code.InvalidExpression, syntax.Offset, $"{Describe(left)} / {Describe(right)} has no value.");
                var type = left.Type == percentage && right.Type == number ? percentage : number;
                var unit = left.Type == percentage ? GesQuantityUnit.UnitNone : right.Type == percentage ? a : quotient!.Value;
                return Flags(GesComputedOp.Divide, type, unit, left, right, quotient is not null, a.AcceptsPercentage(), b == GesQuantityUnit.UnitNone);
            }
            case GesSchemaBinaryOperator.Power:
            {
                // A quantity takes the unit of its power from the derivation table, which needs the exponent at binding.
                Numeric(syntax.Children[0], left);
                Numeric(syntax.Children[1], right);
                if (right.Type == number && b != GesQuantityUnit.UnitNone)
                    throw Error(Code.UnitMismatch, syntax.Children[1].Offset, $"An exponent has no unit, but this one is a {Describe(right)}.");
                var unit = GesQuantityUnit.UnitNone;
                if (left.Type == number && a != GesQuantityUnit.UnitNone)
                {
                    if (right.Op != GesComputedOp.Literal)
                        throw Error(Code.UnitMismatch, syntax.Children[1].Offset, $"The power of a {Describe(left)} takes a literal exponent, which gives the unit of the result.");
                    unit = GesQuantityUnits.Power(a, right.Literal.Numeric)
                        ?? throw Error(Code.UnitMismatch, syntax.Offset, $"A {Describe(left)} to the power {right.Decimal.Canonical} has no unit of the closed unit set.");
                }

                return Node(GesComputedOp.Power, number, unit, left, right);
            }
            case GesSchemaBinaryOperator.And or GesSchemaBinaryOperator.Or:
                if (left.Type != GesComputedType.Boolean) throw Mismatch(syntax.Children[0], "a Boolean", left);
                if (right.Type != GesComputedType.Boolean) throw Mismatch(syntax.Children[1], "a Boolean", right);
                return Node(syntax.Operator == GesSchemaBinaryOperator.And ? GesComputedOp.And : GesComputedOp.Or, GesComputedType.Boolean, GesQuantityUnit.UnitNone, left, right);
            default:
            {
                var equality = syntax.Operator is GesSchemaBinaryOperator.Equal or GesSchemaBinaryOperator.NotEqual;
                if (left.Type == GesComputedType.Boolean && !equality) throw Mismatch(syntax.Children[0], "a number or percentage", left);
                if (left.Type == number && right.Type == number && a != b) throw Units(syntax, left, right);
                if (!SameType(left, right)) throw Different(syntax.Children[1], left, right);
                var op = syntax.Operator switch
                {
                    GesSchemaBinaryOperator.Less => GesComputedOp.Less,
                    GesSchemaBinaryOperator.LessOrEqual => GesComputedOp.LessOrEqual,
                    GesSchemaBinaryOperator.Greater => GesComputedOp.Greater,
                    GesSchemaBinaryOperator.GreaterOrEqual => GesComputedOp.GreaterOrEqual,
                    GesSchemaBinaryOperator.Equal => GesComputedOp.Equal,
                    _ => GesComputedOp.NotEqual
                };
                return Flags(op, GesComputedType.Boolean, GesQuantityUnit.UnitNone, left, right, true, false, false);
            }
        }
    }

    // The arithmetic of vectors and points as the virtual machine has it: vectors add and subtract, a point moves by a vector,
    // two points subtract to a vector, and a vector scales by a number, with the product or quotient unit, or a percentage.
    private Checked SpatialBinary(GesSchemaExpressionSyntax syntax, GesComputedNode left, GesComputedNode right)
    {
        var vector = GesComputedType.Vector;
        var point = GesComputedType.Point;
        switch (syntax.Operator)
        {
            case GesSchemaBinaryOperator.Add or GesSchemaBinaryOperator.Subtract:
            {
                var subtract = syntax.Operator == GesSchemaBinaryOperator.Subtract;
                GesComputedType? result = (left.Type, right.Type) switch
                {
                    (GesComputedType.Vector, GesComputedType.Vector) => vector,
                    (GesComputedType.Point, GesComputedType.Vector) => point,
                    (GesComputedType.Point, GesComputedType.Point) when subtract => vector,
                    _ => null
                };
                if (result is null) throw Error(Code.InvalidExpression, syntax.Offset, $"{Describe(left)} {GesSchemaFormatter.Symbol(syntax.Operator)} {Describe(right)} has no value; vectors add and subtract, a point moves by a vector and two points subtract to a vector.");
                if (left.Unit != right.Unit) throw Units(syntax, left, right);
                return Node(subtract ? GesComputedOp.Subtract : GesComputedOp.Add, result.Value, left.Unit, left, right);
            }
            case GesSchemaBinaryOperator.Multiply or GesSchemaBinaryOperator.Divide:
            {
                var divide = syntax.Operator == GesSchemaBinaryOperator.Divide;
                var scaled = left.Type == vector ? left : right;
                var scalar = left.Type == vector ? right : left;
                if (scaled.Type != vector || scalar.Type is not (GesComputedType.Number or GesComputedType.Percentage) || divide && left.Type != vector)
                    throw Error(Code.InvalidExpression, syntax.Offset, $"{Describe(left)} {GesSchemaFormatter.Symbol(syntax.Operator)} {Describe(right)} has no value; a vector scales by a number or percentage, and a point does not scale.");
                GesQuantityUnit unit;
                if (scalar.Type == GesComputedType.Percentage)
                {
                    if (!scaled.Unit.AcceptsPercentage()) throw Error(Code.UnitMismatch, syntax.Offset, "A percentage does not scale a decibel level.");
                    unit = scaled.Unit;
                }
                else
                {
                    unit = (divide ? QuotientUnit(scaled.Unit, scalar.Unit) : left.Type == vector ? ProductUnit(scaled.Unit, scalar.Unit) : ProductUnit(scalar.Unit, scaled.Unit))
                        ?? throw Units(syntax, left, right);
                }

                return Node(divide ? GesComputedOp.Divide : GesComputedOp.Multiply, vector, unit, left, right);
            }
            default:
                throw Error(Code.InvalidExpression, syntax.Offset, $"{Describe(left)} {GesSchemaFormatter.Symbol(syntax.Operator)} {Describe(right)} has no value; spatial values are not compared.");
        }
    }

    // A part of a spatial value: an axis of a vector or point, the corners, centre and size of a box, the centre and radius of
    // a disc, or the points of a polygon, which only [:count] reads.
    private Checked Part(GesSchemaExpressionSyntax syntax, GesComputedNode target)
    {
        var name = syntax.Name;
        var (type, unit) = (target.Type, name) switch
        {
            (GesComputedType.Vector or GesComputedType.Point, "x" or "y" or "z") => (GesComputedType.Number, target.Unit),
            (GesComputedType.Box, "lower" or "upper" or "center") => (GesComputedType.Point, target.Unit),
            (GesComputedType.Box, "size") => (GesComputedType.Vector, target.Unit),
            (GesComputedType.Disc, "center") => (GesComputedType.Point, target.Unit),
            (GesComputedType.Disc, "radius") => (GesComputedType.Number, target.Unit),
            (GesComputedType.Poly, "points") => (GesComputedType.List, target.Unit),
            _ => throw Error(Code.UnknownName, syntax.Offset, $"'{name}' is no part of a {Describe(target)}; " + target.Type switch
            {
                GesComputedType.Box => "a Box has lower, upper, center and size.",
                GesComputedType.Disc => "a Disc has center and radius.",
                GesComputedType.Poly => "a Poly has points.",
                _ => "it has x, y and z."
            })
        };
        return new Checked(new GesComputedNode { Op = GesComputedOp.Part, Type = type, Unit = unit, Path = [name], Children = [target] });
    }

    // A spatial value built with the constructor of its type, as a script record builds it.
    private Checked Construct(GesSchemaExpressionSyntax syntax, ComputedContext context)
    {
        var children = syntax.Children;
        var labels = string.Join(",", syntax.Labels);
        switch (syntax.Name)
        {
            case "Vector" or "Point":
            {
                var axes = new[] { "x", "y", "z" };
                var start = 0;
                if (children.Length > 0 && syntax.Labels[0].Length > 0)
                {
                    start = Array.IndexOf(axes, syntax.Labels[0]);
                    for (var index = 0; index < children.Length; index++)
                        if (start < 0 || start + index >= 3 || syntax.Labels[index] != axes[start + index])
                            throw Error(Code.InvalidExpression, children[index].Offset, $"The components of :{syntax.Name} are labeled as a run of x, y and z without gaps, or all positional.");
                }
                else
                {
                    for (var index = 0; index < children.Length; index++)
                        if (syntax.Labels[index].Length > 0) throw Error(Code.InvalidExpression, children[index].Offset, $"The components of :{syntax.Name} are all labeled or all positional.");
                    if (children.Length is 1 or > 3) throw Error(Code.InvalidExpression, syntax.Offset, $":{syntax.Name} takes two or three positional components.");
                }

                var components = new GesComputedNode?[3];
                var unit = (GesQuantityUnit?)null;
                for (var index = 0; index < children.Length; index++)
                {
                    var component = Value(children[index], context);
                    if (component.Type != GesComputedType.Number) throw Mismatch(children[index], "a number", component);
                    if (unit is { } first && component.Unit != first)
                        throw Error(Code.UnitMismatch, children[index].Offset, $"The components of :{syntax.Name} have one unit, but a {Describe(component)} meets a {Describe(components[start])}.");
                    unit = component.Unit;
                    components[start + index] = component;
                }

                var resolved = unit ?? GesQuantityUnit.UnitNone;
                var parts = new GesComputedNode[3];
                for (var index = 0; index < 3; index++) parts[index] = components[index] ?? Zero(resolved);
                var point = syntax.Name == "Point";
                return Node(point ? GesComputedOp.Point : GesComputedOp.Vector, point ? GesComputedType.Point : GesComputedType.Vector, resolved, parts);
            }
            case "Box" when labels is "lower,upper" or "center,size":
            {
                var around = labels == "center,size";
                var first = Value(children[0], context);
                var second = Value(children[1], context);
                if (first.Type != GesComputedType.Point) throw Mismatch(children[0], "a Point", first);
                if (second.Type != (around ? GesComputedType.Vector : GesComputedType.Point)) throw Mismatch(children[1], around ? "a Vector" : "a Point", second);
                if (first.Unit != second.Unit) throw Error(Code.UnitMismatch, children[1].Offset, $"The parts of a :Box have one unit, but a {Describe(second)} meets a {Describe(first)}.");
                return Node(around ? GesComputedOp.BoxAround : GesComputedOp.Box, GesComputedType.Box, first.Unit, first, second);
            }
            case "Disc" when labels is "center,radius" or "radius":
            {
                var radius = Value(children[^1], context);
                if (radius.Type != GesComputedType.Number) throw Mismatch(children[^1], "a number", radius);
                if (children.Length == 1) return Node(GesComputedOp.DiscAround, GesComputedType.Disc, radius.Unit, radius);
                var center = Value(children[0], context);
                if (center.Type != GesComputedType.Point) throw Mismatch(children[0], "a Point", center);
                if (center.Unit != radius.Unit) throw Error(Code.UnitMismatch, children[1].Offset, $"The centre and radius of a :Disc have one unit, but a {Describe(radius)} meets a {Describe(center)}.");
                return Node(GesComputedOp.Disc, GesComputedType.Disc, center.Unit, center, radius);
            }
            case "Poly" when labels == "points":
                return Poly(children[0], context);
            default:
                throw Error(Code.InvalidExpression, syntax.Offset, syntax.Name switch
                {
                    "Box" => ":Box takes lower: and upper:, or center: and size:.",
                    "Disc" => ":Disc takes center: and radius:, or radius:.",
                    _ => ":Poly takes points:."
                });
        }
    }

    // The points of a :Poly: a list of point expressions, a list field of Points, or a projection of a list field to Points.
    private Checked Poly(GesSchemaExpressionSyntax syntax, ComputedContext context)
    {
        if (syntax.Kind == GesSchemaExpressionKind.List)
        {
            var points = new GesComputedNode[syntax.Children.Length];
            for (var index = 0; index < points.Length; index++)
            {
                points[index] = Value(syntax.Children[index], context);
                if (points[index].Type != GesComputedType.Point) throw Mismatch(syntax.Children[index], "a Point", points[index]);
                if (points[index].Unit != points[0].Unit) throw Error(Code.UnitMismatch, syntax.Children[index].Offset, $"The points of a :Poly have one unit, but a {Describe(points[index])} meets a {Describe(points[0])}.");
            }

            return Node(GesComputedOp.Poly, GesComputedType.Poly, points[0].Unit, points);
        }

        var select = syntax.Kind == GesSchemaExpressionKind.Select;
        var sourceSyntax = select ? syntax.Children[0] : syntax;
        var source = CheckExpression(sourceSyntax, context);
        if (source.Node.Type != GesComputedType.List || source.Element is null) throw Error(Code.InvalidExpression, sourceSyntax.Offset, "The points of a :Poly are a list of points, a list field of Points or a projection of a list field with :select.");
        if (source.Optional) throw Error(Code.InvalidExpression, sourceSyntax.Offset, "The points of a :Poly read a list that is no optional field and lies in none.");
        if (source.Element.Length != 1) throw Error(Code.InvalidExpression, sourceSyntax.Offset, "The points of a :Poly read a list without a union.");
        var item = Element(source.Element[0], sourceSyntax.Offset);
        if (!select)
        {
            if (item.Node.Type != GesComputedType.Point) throw Error(Code.InvalidExpression, sourceSyntax.Offset, "A list field gives the points of a :Poly when it holds Points; project other elements with :select.");
            return Node(GesComputedOp.PolyOf, GesComputedType.Poly, item.Node.Unit, source.Node);
        }

        if (context.Variable.Length > 0) throw Error(Code.InvalidExpression, syntax.Offset, "A projection does not contain another one.");
        if (IsTaken(context, syntax.Name)) throw Error(Code.DuplicateName, syntax.Offset, $"'{syntax.Name}' already names a field or let; the variable needs another name.");
        context.Variable = syntax.Name;
        context.Element = item;
        try
        {
            var projection = Value(syntax.Children[1], context);
            if (projection.Type != GesComputedType.Point) throw Mismatch(syntax.Children[1], "a Point", projection);
            return Node(GesComputedOp.PolyOf, GesComputedType.Poly, projection.Unit, source.Node, projection);
        }
        finally
        {
            context.Variable = "";
            context.Element = null;
        }
    }

    // The zero of an omitted component, in the unit of the others.
    private static GesComputedNode Zero(GesQuantityUnit unit)
        => new() { Op = GesComputedOp.Literal, Type = GesComputedType.Number, Unit = unit, Literal = GesComputedValue.OfInteger(0), Decimal = new GesSchemaDecimal(0, 0) };

    // A number or percentage, which functions and powers read through its numeric view: a percentage as its ratio.
    private void Numeric(GesSchemaExpressionSyntax syntax, GesComputedNode node)
    {
        if (node.Type is not (GesComputedType.Number or GesComputedType.Percentage)) throw Mismatch(syntax, "a number or percentage", node);
    }

    // The unit of a number, none for a percentage, which functions read as its ratio.
    private static GesQuantityUnit NumericUnit(GesComputedNode node) => node.Type == GesComputedType.Number ? node.Unit : GesQuantityUnit.UnitNone;

    // The functions of GES with the unit rules of the virtual machine, each giving a number: roots take the unit of the root
    // in the derivation table, trigonometry takes radians as a plain number or degrees, the inverse functions and `atan2` give
    // radians, rounding keeps the unit, and the others read plain numbers.
    private Checked Function(GesSchemaExpressionSyntax syntax, ComputedContext context)
    {
        var children = syntax.Children;
        var arguments = new GesComputedNode[children.Length];
        for (var index = 0; index < children.Length; index++)
        {
            arguments[index] = Value(children[index], context);
            Numeric(children[index], arguments[index]);
        }

        var number = GesComputedType.Number;
        var unit = NumericUnit(arguments[0]);
        switch (syntax.Name)
        {
            case "sqrt" or "cbrt":
            {
                var root = GesQuantityUnits.Power(unit, syntax.Name == "sqrt" ? 0.5d : 1d / 3d)
                    ?? throw Error(Code.UnitMismatch, syntax.Offset, $"A {Describe(arguments[0])} has no {(syntax.Name == "sqrt" ? "square" : "cube")} root in the closed unit set.");
                return Node(syntax.Name == "sqrt" ? GesComputedOp.Sqrt : GesComputedOp.Cbrt, number, root, arguments);
            }
            case "sin" or "cos" or "tan":
            {
                if (unit != GesQuantityUnit.UnitNone && unit != GesQuantityUnit.UnitDegree) throw Angle(syntax, arguments[0]);
                var op = syntax.Name switch { "sin" => GesComputedOp.Sin, "cos" => GesComputedOp.Cos, _ => GesComputedOp.Tan };
                return new Checked(new GesComputedNode { Op = op, Type = number, Unit = GesQuantityUnit.UnitNone, Children = arguments, First = unit == GesQuantityUnit.UnitDegree });
            }
            case "rad":
                if (unit != GesQuantityUnit.UnitNone && unit != GesQuantityUnit.UnitDegree) throw Angle(syntax, arguments[0]);
                return Node(GesComputedOp.Rad, number, GesQuantityUnit.UnitNone, arguments);
            case "atan2" or "hypot":
                for (var index = 1; index < arguments.Length; index++)
                    if (NumericUnit(arguments[index]) != unit)
                        throw Error(Code.UnitMismatch, children[index].Offset, $"The arguments of {syntax.Name} have one unit, but a {Describe(arguments[index])} meets a {Describe(arguments[0])}.");
                return syntax.Name == "atan2" ? Node(GesComputedOp.Atan2, number, GesQuantityUnit.UnitNone, arguments) : Node(GesComputedOp.Hypot, number, unit, arguments);
            case "floor" or "ceil" or "truncate" or "round half even" or "round half up" or "round half down":
            {
                var op = syntax.Name switch
                {
                    "floor" => GesComputedOp.Floor,
                    "ceil" => GesComputedOp.Ceil,
                    "truncate" => GesComputedOp.Truncate,
                    "round half even" => GesComputedOp.RoundHalfEven,
                    "round half up" => GesComputedOp.RoundHalfUp,
                    _ => GesComputedOp.RoundHalfDown
                };
                return Node(op, number, unit, arguments);
            }
            default:
            {
                if (unit != GesQuantityUnit.UnitNone)
                    throw Error(Code.UnitMismatch, syntax.Offset, $"{syntax.Name} reads a plain number, but this is a {Describe(arguments[0])}.");
                var op = syntax.Name switch
                {
                    "asin" => GesComputedOp.Asin,
                    "acos" => GesComputedOp.Acos,
                    "atan" => GesComputedOp.Atan,
                    "exp" => GesComputedOp.Exp,
                    "ln" => GesComputedOp.Ln,
                    _ => GesComputedOp.Deg
                };
                return Node(op, number, op == GesComputedOp.Deg ? GesQuantityUnit.UnitDegree : GesQuantityUnit.UnitNone, arguments);
            }
        }
    }

    private GesSchemaException Angle(GesSchemaExpressionSyntax syntax, GesComputedNode argument)
        => Error(Code.UnitMismatch, syntax.Offset, $"{syntax.Name} reads an angle in radians as a plain number or in degrees, but this is a {Describe(argument)}.");

    private static GesQuantityUnit? ProductUnit(GesQuantityUnit a, GesQuantityUnit b)
        => a == GesQuantityUnit.UnitNone ? b : b == GesQuantityUnit.UnitNone ? a : GesQuantityUnits.Product(a, b);

    private static GesQuantityUnit? QuotientUnit(GesQuantityUnit a, GesQuantityUnit b)
        => b == GesQuantityUnit.UnitNone ? a : a == b ? GesQuantityUnit.UnitNone : GesQuantityUnits.Quotient(a, b);

    private static bool SameType(GesComputedNode a, GesComputedNode b) => a.Type == b.Type && a.Unit == b.Unit;

    private static Checked Node(GesComputedOp op, GesComputedType type, GesQuantityUnit unit, params GesComputedNode[] children)
        => new(new GesComputedNode { Op = op, Type = type, Unit = unit, Children = children });

    private static Checked Flags(GesComputedOp op, GesComputedType type, GesQuantityUnit unit, GesComputedNode left, GesComputedNode right, bool first, bool second, bool third)
        => new(new GesComputedNode { Op = op, Type = type, Unit = unit, Children = [left, right], First = first, Second = second, Third = third });

    private GesSchemaException Mismatch(GesSchemaExpressionSyntax syntax, string expected, GesComputedNode found)
        => Error(Code.InvalidExpression, syntax.Offset, $"Expected {expected}, found a {Describe(found)}.");

    private GesSchemaException Different(GesSchemaExpressionSyntax syntax, GesComputedNode first, GesComputedNode other)
        => first.Type == GesComputedType.Number && other.Type == GesComputedType.Number
            ? Error(Code.UnitMismatch, syntax.Offset, $"A {Describe(other)} meets a {Describe(first)}.")
            : Error(Code.InvalidExpression, syntax.Offset, $"A {Describe(other)} meets a {Describe(first)}.");

    private GesSchemaException Units(GesSchemaExpressionSyntax syntax, GesComputedNode left, GesComputedNode right)
        => Error(Code.UnitMismatch, syntax.Offset, $"{Describe(left)} {GesSchemaFormatter.Symbol(syntax.Operator)} {Describe(right)} has no unit of the closed unit set.");
}
