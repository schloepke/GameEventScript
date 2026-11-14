// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using GameEventScript.Api;
using GameEventScript.Syntax;

namespace GameEventScript.Generator;

// Translates the checked expressions of computed fields into C# or Swift. A field whose operands binding proves to give the
// bits of the GES virtual machine in plain binary64 becomes one property in the language's own operators (static code); any
// other field keeps the dynamic form, a property over a method that returns the exact GES value with the rules of the
// virtual machine. The helpers of both forms live in one type per generation, `GesComputed`.
internal sealed partial class GesComputedEmitter
{
    private readonly bool _swift;
    private readonly string _helper;
    private readonly IGesComputedAccess _access;
    private readonly List<string> _sums = [];
    private string _owner = "";
    private string _field = "";
    private string _element = "";

    internal GesComputedEmitter(bool swift, string helper, IGesComputedAccess access)
    {
        _swift = swift;
        _helper = helper;
        _access = access;
    }

    // The helper type: `GesComputed` in a namespace, otherwise with the suffix of the generation, so that two generations
    // without namespaces can share a module.
    internal static string Helper(string space, string suffix) => space.Length > 0 ? "GesComputed" : "GesComputed" + suffix;

    internal string HelperName => _helper;

    // The member names of a computed field: the property and, in the dynamic form, the method with the exact value.
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

    // Whether any computed field of the types needs the exact values of the dynamic form.
    internal bool NeedsValues(IEnumerable<(string Owner, GesResolvedComputed[] Computed)> types)
    {
        foreach (var (owner, computed) in types)
            foreach (var field in computed)
                if (Analyze(owner, field) is null) return true;
        return false;
    }

    // The exact GES value of a computed field of a record, which conversions to GES values read.
    internal string ValueOf(string record, string owner, GesResolvedComputed field)
    {
        if (Analyze(owner, field) is null) return $"{record}.{Method(field.Name, owner, _swift)}()";
        var property = $"{record}.{Member(Property(field.Name, owner, _swift))}";
        return Call(field.Kind == GesSchemaTypeKind.Percentage ? "ReadPercentage" : "Read", property);
    }

    // Writes the properties and helper methods of a type's computed fields.
    internal void Members(GesSchemaPlanner.Code code, string owner, GesResolvedComputed[] computed, string access)
    {
        foreach (var field in computed)
        {
            _owner = owner;
            _field = Property(field.Name, owner, _swift);
            _element = "";
            _sums.Clear();
            code.Blank();
            if (Analyze(owner, field) is { } facts) Static(code, field, facts, access);
            else Dynamic(code, owner, field, access);
            foreach (var body in _sums)
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

    private void Summary(GesSchemaPlanner.Code code, GesResolvedComputed field)
    {
        var spelling = Spelling(field);
        code.Line(_swift
            ? $"/// `{field.Name}` computed by `{spelling}`; {Absent(field)}."
            : $"/// <summary><c>{Escape(field.Name)}</c> computed by <c>{Escape(spelling)}</c>; {Absent(field)}.</summary>");
    }

    private string Camel(string name) => _swift ? GesSchemaPlanner.Camel(name) : name;

    private string Call(string name, params string[] arguments) => $"{_helper}.{Camel(name)}({string.Join(", ", arguments)})";

    // A member name as the language spells it, with Swift keywords escaped.
    private string Member(string name) => _swift && GesSwiftEmitter.Keywords.Contains(name) ? "`" + name + "`" : name;

    // A read of a path from a record (empty root for the record itself in a property) or from a list element.
    private string Root(GesComputedNode node, string root) => node.Op == GesComputedOp.Item ? "item" : root;

    private string Owner(GesComputedNode node) => node.Op == GesComputedOp.Item ? _element : _owner;

    private static string Join(string root, string member) => root.Length == 0 ? member : root + "." + member;
}

// What a path reaches: the member text, the presence flags of the optional fields on it (wire records), whether the text gives
// an optional value (models), a computed field it ends at, and what the field holds as declared.
internal sealed class GesComputedReach
{
    internal string Text = "";

    // A presence flag per optional field on the path, with the index of that field.
    internal List<(int Step, string Condition)> Conditions = [];

    // Whether the text gives an optional value: an optional field, or a field behind an optional one.
    internal bool Nullable;

    // The schema and name of a computed field at the end of the path, where Text names the record that holds it.
    internal string ComputedOwner = "";
    internal string ComputedName = "";

    // Whether the field holds an Int64, and the closed range it declares, unbounded without one.
    internal bool Integer;
    internal double Lower = double.NegativeInfinity;
    internal double Upper = double.PositiveInfinity;

    // The largest element count of a list, unbounded without a max.
    internal double MaxCount = double.PositiveInfinity;
}

// How generated code reaches the fields that computed expressions read. Paths start at the record (an empty root, `self` or
// `this`) or at a list element (`item`, whose owner is its schema, or empty for a number).
internal interface IGesComputedAccess
{
    // What a path from a record of the owner reaches; with an empty path, a list element of numbers whose list `path` names.
    GesComputedReach Reach(string root, string owner, string[] path);

    // A list element that is a number, of the list at the path.
    GesComputedReach Element(string owner, string[] path);

    // The schema of a list's elements, or empty for numbers.
    string ElementOwner(string owner, string[] path);

    // The computed fields of a schema.
    GesResolvedComputed[] Computed(string owner);
}
