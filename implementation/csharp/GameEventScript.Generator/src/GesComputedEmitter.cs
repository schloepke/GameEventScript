// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using GameEventScript.Api;
using GameEventScript.Syntax;

namespace GameEventScript.Generator;

// Translates the checked expressions of computed fields into C# or Swift. Each field becomes a property and a method that
// returns the exact GES value, which other computed fields read, and each sum a method with a loop. The arithmetic lives in
// one helper type per generation that repeats the rules of the GES virtual machine, so that the generated code gives the
// bits a script record gives.
internal sealed class GesComputedEmitter
{
    private readonly bool _swift;
    private readonly string _helper;
    private readonly IGesComputedAccess _access;
    private readonly List<(string Name, string Body)> _sums = [];
    private string _owner = "";
    private string _field = "";
    private string _element = "";

    internal GesComputedEmitter(bool swift, string helper, IGesComputedAccess access)
    {
        _swift = swift;
        _helper = helper;
        _access = access;
    }

    internal static string Helper(string suffix) => "__GesComputed" + suffix;

    // The member names of a computed field: the property and the method with the exact value.
    internal static string Property(string name, string type, bool swift)
    {
        var pascal = GesSchemaPlanner.Pascal(name);
        if (pascal == type) pascal += "Value";
        return swift ? GesSchemaPlanner.Camel(pascal) : pascal;
    }

    internal static string Method(string name, string type, bool swift) => "__" + Property(name, type, swift);

    // The names a type's computed fields add, for the collision check of its members.
    internal static void Names(GesResolvedComputed[] computed, string type, bool swift, List<(string, string)> names)
    {
        foreach (var field in computed)
        {
            names.Add((Property(field.Name, type, swift), "the computed field " + field.Name));
            names.Add((Method(field.Name, type, swift), "the value of the computed field " + field.Name));
        }
    }

    // The type of a computed field's property: binary64 for numbers and percentage ratios, optional when the field is.
    internal static string PropertyType(GesResolvedComputed field, bool swift)
    {
        if (field.Kind == GesSchemaTypeKind.Boolean) return swift ? "Bool" : "bool";
        return (swift ? "Double" : "double") + (field.Optional ? "?" : "");
    }

    // Writes the properties, value methods and sum methods of a type's computed fields.
    internal void Members(GesSchemaPlanner.Code code, string owner, GesResolvedComputed[] computed, string access)
    {
        var self = _swift ? "self" : "this";
        foreach (var field in computed)
        {
            _owner = owner;
            _field = Property(field.Name, owner, _swift);
            _sums.Clear();
            var expression = Cast(Expression(field.Expression, self), field);
            var property = Property(field.Name, owner, _swift);
            var method = Method(field.Name, owner, _swift);
            var result = field.Kind == GesSchemaTypeKind.Boolean ? "IsTrue" : field.Optional ? "OptionalNumber" : "Number";
            code.Blank();
            var spelling = Spelling(field);
            if (_swift)
            {
                code.Line($"/// `{field.Name}` computed by `{spelling}`; {Absent(field)}.");
                code.Line($"{(access.Length > 0 ? access + " " : "")}var {property}: {PropertyType(field, true)} {{ {_helper}.{Camel(result)}({method}()) }}");
                code.Blank();
                code.Line($"/// The exact GES value of `{field.Name}`, which other computed fields read.");
                code.Line($"func {method}() -> {_helper}.Value {{ {expression} }}");
            }
            else
            {
                code.Line($"/// <summary><c>{Escape(field.Name)}</c> computed by <c>{Escape(spelling)}</c>; {Absent(field)}.</summary>");
                code.Line($"{access} {PropertyType(field, false)} {property} => {_helper}.{result}({method}());");
                code.Blank();
                code.Line($"/// <summary>The exact GES value of <c>{Escape(field.Name)}</c>, which other computed fields read.</summary>");
                code.Line($"internal {_helper}.Value {method}() => {expression};");
            }

            foreach (var (name, body) in _sums)
            {
                code.Blank();
                foreach (var line in body.Split('\n')) code.Line(line);
            }
        }
    }

    private static string Absent(GesResolvedComputed field) => field.Kind == GesSchemaTypeKind.Boolean
        ? "false where the expression gives nothing, as a script record reads it"
        : field.Optional ? "absent where the expression gives nothing" : "NaN where the expression gives nothing";

    // The source spelling of the expression, as the formatter prints it.
    private static string Spelling(GesResolvedComputed field) => GesSchemaFormatter.Expression(field.Syntax.Computed!);

    private static string Escape(string text) => text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");

    private string Camel(string name) => _swift ? GesSchemaPlanner.Camel(name) : name;

    private string Call(string name, params string[] arguments) => $"{_helper}.{Camel(name)}({string.Join(", ", arguments)})";

    private string Flag(bool value) => value ? "true" : "false";

    // A script record converts the value to the field's type: a Percentage takes a finite number, a Boolean only true.
    private string Cast(string expression, GesResolvedComputed field) => field.Kind switch
    {
        GesSchemaTypeKind.Percentage => Call("CastPercentage", expression),
        GesSchemaTypeKind.Boolean => Call("CastBoolean", expression),
        _ => expression
    };

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
                return _access.Read(node.Op == GesComputedOp.Item ? "item" : root, node.Op == GesComputedOp.Item ? _element : _owner, node.Path, node.Type == GesComputedType.Percentage);
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
                return Call("Boolean", _access.Present(child.Op == GesComputedOp.Item ? "item" : root, child.Op == GesComputedOp.Item ? _element : _owner, child.Path));
            }
            case GesComputedOp.Count:
            {
                var list = _access.List(root, _owner, children[0].Path);
                return _swift ? Call("Integer", $"Int64({list}.count)") : Call("Integer", $"{list}.Count");
            }
            default:
                return Sum(node, root);
        }
    }

    // A sum adds the projected elements from left to right, starting with the first one, and is the integer 0 for none.
    private string Sum(GesComputedNode node, string root)
    {
        var list = _access.List(root, _owner, node.Children[0].Path);
        var name = $"__{_field}Sum{_sums.Count.ToString(CultureInfo.InvariantCulture)}";
        var previous = _element;
        _element = ElementOwner(node.Children[0]);
        var value = node.Children.Length > 1 ? Expression(node.Children[1], root) : _access.Read("item", "", [], node.Type == GesComputedType.Percentage);
        _element = previous;
        var body = new StringBuilder();
        if (_swift)
        {
            body.Append($"func {name}() -> {_helper}.Value {{\n");
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
            body.Append($"internal {_helper}.Value {name}()\n");
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

        _sums.Add((name, body.ToString()));
        return $"{name}()";
    }

    private string ElementOwner(GesComputedNode source) => _access.ElementOwner(_owner, source.Path);
}

// How generated code reaches the fields that computed expressions read. Paths start at the record (`self`, `this`) or at a
// list element (`item`, whose owner is its schema, or empty for a number).
internal interface IGesComputedAccess
{
    // The exact value at a path, nothing where an optional field on the way is absent.
    string Read(string root, string owner, string[] path, bool percentage);

    // Whether the record at a path is present.
    string Present(string root, string owner, string[] path);

    // The list at a path that holds no optional field.
    string List(string root, string owner, string[] path);

    // The schema of a list's elements, or empty for numbers.
    string ElementOwner(string owner, string[] path);
}
