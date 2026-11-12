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
        _ => GesComputedType.Number
    };
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
            if (type.Length != 1 || type[0].Kind is not (GesSchemaTypeKind.Number or GesSchemaTypeKind.Quantity or GesSchemaTypeKind.Percentage or GesSchemaTypeKind.Boolean)
                || type[0].Constraints is { } constraints && (constraints.Integer || constraints.Lower is not null || constraints.Step is not null || constraints.Delta is not null))
                throw scope.Error(Code.InvalidExpression, entry.Offset, "A computed field is a Number, Quantity, Percentage or Boolean without constraints.");
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
            case GesComputedOp.Sum:
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
        _ => node.Type.ToString()
    };

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
                if (operand.Type is not (GesComputedType.Number or GesComputedType.Percentage)) throw Mismatch(syntax, "a number or percentage", operand);
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
        => new(new GesComputedNode { Op = op, Type = field.Type, Unit = field.Kind == GesSchemaTypeKind.Quantity ? field.Unit : GesQuantityUnit.UnitNone, Path = path, OptionalSteps = [field.Optional] }, optional: field.Optional);

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
            case GesSchemaTypeKind.List when type.Element is { } alternatives:
                element = alternatives;
                return new GesComputedNode { Op = op, Type = GesComputedType.List, Path = path };
            case GesSchemaTypeKind.Reference when type.Reference is { Kind: GesSchemaKind.Schema } reference:
                record = reference;
                return new GesComputedNode { Op = op, Type = GesComputedType.Record, Path = path };
            default:
                // Text, tags, spatial values and other types are not read; only has value may name them, and it rejects them.
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
