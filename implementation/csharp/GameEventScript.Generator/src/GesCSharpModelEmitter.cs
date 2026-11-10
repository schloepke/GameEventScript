// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using System.Globalization;

namespace GameEventScript.Generator;

// Writes C# 9 models for Unity: a sealed class per schema with value equality, settable properties in declared order, a
// constructor that sets every field and one that leaves every field at its zero value, an enum per closed Tag, an abstract
// class with one sealed subclass per alternative for a union, and the runtime's GesPoint and GesVector for spatial fields.
// Classes avoid struct copies that would share a list; no `record`, `init` accessor or Reflection is used.
internal static class GesCSharpModelEmitter
{
    // Members every model type declares or inherits.
    private static readonly string[] Reserved = ["Equals", "GetHashCode", "ToString", "GetType"];

    internal static string Emit(GesModelLayout layout, GesSchemaGenerateOptions options)
    {
        var access = options.Access == GesSchemaAccess.Public ? "public" : "internal";
        var space = GesSchemaPlanner.Namespace(options.Namespace, GesCSharpEmitter.Keywords);
        var code = new GesSchemaPlanner.Code();
        code.Raw(GesModelPlanner.Header(layout));
        code.Blank();
        code.Line("#nullable enable");
        code.Blank();
        code.Line("using System;");
        code.Line("using System.Collections.Generic;");
        code.Line("using GameEventScript.Api;");
        code.Blank();
        if (space.Count > 0)
        {
            code.Line("namespace " + string.Join(".", space));
            code.Open("{");
        }

        var support = "__GesModels" + GesSchemaPlanner.Hash(GesModelPlanner.Header(layout));
        var qualifier = "global::" + string.Concat(space.ConvertAll(part => part + "."));
        for (var index = 0; index < layout.Types.Count; index++)
        {
            Record(code, layout.Types[index], access, support, qualifier);
            code.Blank();
        }

        Support(code, support);
        if (space.Count > 0) code.Close();
        return code.ToString();
    }

    private static string Parameter(string pascal)
    {
        var name = GesSchemaPlanner.Camel(pascal);
        return GesCSharpEmitter.Keywords.Contains(name) ? "@" + name : name;
    }

    private static void Record(GesSchemaPlanner.Code code, GesModelType type, string access, string support, string qualifier)
    {
        var fields = GesModelPlanner.Fields(type);
        var names = new List<(string, string)>();
        foreach (var reserved in Reserved) names.Add((reserved, "the inherited member " + reserved));
        foreach (var field in fields)
        {
            names.Add((field.Base, "the field " + field.Field.Name));
            Nested(field.Value, names, field.Field.Name);
        }

        GesSchemaPlanner.Unique(type.Name, names);

        code.Line($"/// <summary>The model of the schema <c>:{type.Name}</c>: a plain value that is never encoded and compares by value.</summary>");
        code.Line($"{access} sealed class {type.Name} : IEquatable<{type.Name}>");
        code.Open("{");
        code.Line("/// <summary>Creates a record with every field at its zero value: absent, empty or the first alternative.</summary>");
        code.Line($"public {type.Name}()");
        code.Open("{");
        code.Close();
        if (fields.Count > 0)
        {
            code.Blank();
            code.Line("/// <summary>Creates a record from its fields.</summary>");
            foreach (var field in fields) code.Line($"/// <param name=\"{GesSchemaPlanner.Camel(field.Base)}\">The field <c>{field.Field.Name}</c>.</param>");
            var parameters = new List<string>();
            foreach (var field in fields) parameters.Add($"{Type(field.Value)} {Parameter(field.Base)}");
            code.Line($"public {type.Name}({string.Join(", ", parameters)})");
            code.Open("{");
            foreach (var field in fields) code.Line($"{field.Base} = {Parameter(field.Base)};");
            code.Close();
        }

        foreach (var field in fields)
        {
            code.Blank();
            code.Line($"/// <summary><c>{Escape(field.Field.Name + ": " + GesModelPlanner.Describe(field.Field))}</c></summary>");
            var initial = Initial(field.Value);
            code.Line($"public {Type(field.Value)} {field.Base} {{ get; set; }}{(initial.Length > 0 ? " = " + initial + ";" : "")}");
        }

        code.Blank();
        code.Line("/// <summary>Compares every field by value.</summary>");
        code.Line("/// <param name=\"other\">The other record.</param>");
        code.Line("/// <returns>Whether both hold equal values.</returns>");
        code.Line($"public bool Equals({type.Name}? other)");
        code.Open("{");
        code.Line("if (other is null) return false;");
        code.Line("if (ReferenceEquals(this, other)) return true;");
        if (fields.Count == 0) code.Line("return true;");
        for (var index = 0; index < fields.Count; index++)
        {
            var test = Equal(fields[index].Value, fields[index].Base, "other." + fields[index].Base, support);
            var end = index == fields.Count - 1 ? ";" : "";
            code.Line(index == 0 ? "return " + test + end : "    && " + test + end);
        }
        code.Close();
        code.Blank();
        code.Line("/// <inheritdoc />");
        code.Line($"public override bool Equals(object? obj) => obj is {type.Name} other && Equals(other);");
        code.Blank();
        code.Line("/// <inheritdoc />");
        code.Line("public override int GetHashCode()");
        code.Open("{");
        code.Line("unchecked");
        code.Open("{");
        code.Line("var hash = 17;");
        foreach (var field in fields) code.Line($"hash = (hash * 31) + {Hash(field.Value, field.Base, support)};");
        code.Line("return hash;");
        code.Close();
        code.Close();
        code.Blank();
        Operators(code, type.Name, "record");
        foreach (var field in fields) Declarations(code, field.Value, support, qualifier);
        code.Close();
    }

    private static void Operators(GesSchemaPlanner.Code code, string name, string what)
    {
        code.Line($"/// <summary>Compares two {what}s by value.</summary>");
        code.Line("/// <param name=\"left\">The first value.</param>");
        code.Line("/// <param name=\"right\">The second value.</param>");
        code.Line("/// <returns>Whether both are null or equal.</returns>");
        code.Line($"public static bool operator ==({name}? left, {name}? right) => left is null ? right is null : left.Equals(right);");
        code.Blank();
        code.Line($"/// <summary>Compares two {what}s by value.</summary>");
        code.Line("/// <param name=\"left\">The first value.</param>");
        code.Line("/// <param name=\"right\">The second value.</param>");
        code.Line("/// <returns>Whether they differ.</returns>");
        code.Line($"public static bool operator !=({name}? left, {name}? right) => !(left == right);");
    }

    private static void Nested(GesModelPlanValue value, List<(string, string)> names, string field)
    {
        if (value.Nested.Length > 0) names.Add((value.Nested, "a type of the field " + field));
        if (value.Element is not null) Nested(value.Element, names, field);
        foreach (var alternative in value.Alternatives) Nested(alternative, names, field);
    }

    private static string Escape(string text) => text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");

    // A union subclass named after a schema hides that schema's type, so its members name schema types with `global::`.
    internal static string Type(GesModelPlanValue value, string? qualifier = null)
    {
        var plain = Plain(value, qualifier);
        return value.Nullable ? plain + "?" : plain;
    }

    private static string Plain(GesModelPlanValue value, string? qualifier = null) => value.Kind switch
    {
        GesModelKind.Boolean => "bool",
        GesModelKind.Number => value.Model.Integer ? "long" : "double",
        GesModelKind.Tag => value.ClosedTag ? value.Nested : "string",
        GesModelKind.Text => "string",
        GesModelKind.List => $"List<{Type(value.Element!, qualifier)}>",
        GesModelKind.Dice => "List<int>",
        GesModelKind.Point => "GesPoint",
        GesModelKind.Vector => "GesVector",
        GesModelKind.Schema => qualifier + value.Model.Schema,
        _ => value.Nested
    };

    private static bool IsReference(GesModelPlanValue value) => value.Kind is GesModelKind.Text or GesModelKind.List or GesModelKind.Dice or GesModelKind.Schema or GesModelKind.Union
        || (value.Kind == GesModelKind.Tag && !value.ClosedTag);

    // The zero value of a property that the type's default would not give: empty texts and lists, new records, the first
    // alternative of a union.
    private static string Initial(GesModelPlanValue value)
    {
        if (value.Nullable) return "";
        return value.Kind switch
        {
            GesModelKind.Text or GesModelKind.Tag when !value.ClosedTag => "\"\"",
            GesModelKind.List or GesModelKind.Dice => $"new {Plain(value)}()",
            GesModelKind.Schema => $"new {value.Model.Schema}()",
            GesModelKind.Union => $"new {value.Nested}.{value.Alternatives[0].Label}({Zero(value.Alternatives[0])})",
            _ => ""
        };
    }

    private static string Zero(GesModelPlanValue value)
    {
        if (value.Nullable) return "null";
        return value.Kind switch
        {
            GesModelKind.Boolean => "false",
            GesModelKind.Number => value.Model.Integer ? "0L" : "0d",
            GesModelKind.Tag when value.ClosedTag => $"default({value.Nested})",
            GesModelKind.Point or GesModelKind.Vector => $"default({Plain(value)})",
            _ => Initial(value)
        };
    }

    // Nested lists compare and hash through lambdas whose parameters are numbered by depth: x0 and y0, then x1 and y1.
    private static string Equal(GesModelPlanValue value, string left, string right, string support, int depth = 0, string? qualifier = null)
    {
        if (value.Kind is GesModelKind.List or GesModelKind.Dice)
            return $"{support}.ListEquals({left}, {right}, {Comparer(value, support, depth, qualifier)})";
        return $"EqualityComparer<{Type(value, qualifier)}>.Default.Equals({left}, {right})";
    }

    private static string Comparer(GesModelPlanValue list, string support, int depth, string? qualifier)
    {
        var (x, y) = ("x" + depth.ToString(CultureInfo.InvariantCulture), "y" + depth.ToString(CultureInfo.InvariantCulture));
        if (list.Kind == GesModelKind.Dice) return $"({x}, {y}) => {x} == {y}";
        return $"({x}, {y}) => {Equal(list.Element!, x, y, support, depth + 1, qualifier)}";
    }

    private static string Hash(GesModelPlanValue value, string expression, string support, int depth = 0) => value.Kind switch
    {
        GesModelKind.List or GesModelKind.Dice => $"{support}.ListHash({expression}, {ElementHash(value, support, depth)})",
        GesModelKind.Number when !value.Model.Integer => $"{support}.Hash({expression})",
        _ when IsReference(value) || value.Nullable => $"({expression}?.GetHashCode() ?? 0)",
        _ => $"{expression}.GetHashCode()"
    };

    private static string ElementHash(GesModelPlanValue list, string support, int depth)
    {
        var x = "x" + depth.ToString(CultureInfo.InvariantCulture);
        return list.Kind == GesModelKind.Dice ? $"{x} => {x}" : $"{x} => " + Hash(list.Element!, x, support, depth + 1);
    }

    private static void Declarations(GesSchemaPlanner.Code code, GesModelPlanValue value, string support, string qualifier)
    {
        if (value.ClosedTag)
        {
            var tags = new List<(string, string)>();
            foreach (var tag in value.Model.Tags) tags.Add((GesSchemaPlanner.Pascal(tag), "the tag #" + tag));
            GesSchemaPlanner.Unique(value.Nested, tags);
            code.Blank();
            code.Line("/// <summary>The tags, in declared order.</summary>");
            code.Line($"public enum {value.Nested}");
            code.Open("{");
            foreach (var tag in value.Model.Tags)
            {
                code.Line($"/// <summary>The tag <c>#{tag}</c>.</summary>");
                code.Line(GesSchemaPlanner.Pascal(tag) + ",");
            }

            code.Close();
        }
        else if (value.Kind == GesModelKind.Union) Union(code, value, support, qualifier);
        else if (value.Element is not null) Declarations(code, value.Element, support, qualifier);
    }

    private static void Union(GesSchemaPlanner.Code code, GesModelPlanValue value, string support, string qualifier)
    {
        var names = new List<(string, string)>();
        foreach (var reserved in Reserved) names.Add((reserved, "the inherited member " + reserved));
        foreach (var alternative in value.Alternatives) names.Add((alternative.Label, "the alternative " + alternative.Label));
        GesSchemaPlanner.Unique(value.Nested, names);
        code.Blank();
        code.Line("/// <summary>The alternatives, one sealed subclass each, in declared order.</summary>");
        code.Line($"public abstract class {value.Nested} : IEquatable<{value.Nested}>");
        code.Open("{");
        code.Line($"private {value.Nested}()");
        code.Open("{");
        code.Close();
        code.Blank();
        code.Line("/// <summary>Compares the alternative and its value.</summary>");
        code.Line("/// <param name=\"other\">The other value.</param>");
        code.Line("/// <returns>Whether both are the same alternative with equal values.</returns>");
        code.Line($"public abstract bool Equals({value.Nested}? other);");
        code.Blank();
        code.Line("/// <inheritdoc />");
        code.Line($"public override bool Equals(object? obj) => obj is {value.Nested} other && Equals(other);");
        code.Blank();
        code.Line("/// <inheritdoc />");
        code.Line("public abstract override int GetHashCode();");
        code.Blank();
        Operators(code, value.Nested, "value");
        for (var index = 0; index < value.Alternatives.Count; index++)
        {
            var alternative = value.Alternatives[index];
            var type = Type(alternative, qualifier);
            code.Blank();
            code.Line($"/// <summary>The alternative <c>:{alternative.Label}</c>.</summary>");
            code.Line($"public sealed class {alternative.Label} : {value.Nested}");
            code.Open("{");
            code.Line("/// <summary>Creates the alternative.</summary>");
            code.Line("/// <param name=\"value\">Its value.</param>");
            code.Line($"public {alternative.Label}({type} value)");
            code.Open("{");
            code.Line("Value = value;");
            code.Close();
            code.Blank();
            code.Line("/// <summary>Gets or sets the value.</summary>");
            code.Line($"public {type} Value {{ get; set; }}");
            code.Blank();
            code.Line("/// <inheritdoc />");
            code.Line($"public override bool Equals({value.Nested}? other) => other is {alternative.Label} alternative && {Equal(alternative, "Value", "alternative.Value", support, 0, qualifier)};");
            code.Blank();
            code.Line("/// <inheritdoc />");
            code.Line($"public override int GetHashCode() => unchecked(({index.ToString(CultureInfo.InvariantCulture)} * 397) ^ {Hash(alternative, "Value", support)});");
            code.Close();
        }

        code.Close();
        foreach (var alternative in value.Alternatives) Declarations(code, alternative, support, qualifier);
    }

    // Equality and hashing helpers of the file, named after its header so that two generated files can share a namespace.
    private static void Support(GesSchemaPlanner.Code code, string name)
    {
        code.Line("/// <summary>Value equality of lists and numbers for the models of this file.</summary>");
        code.Line($"internal static class {name}");
        code.Open("{");
        code.Line("/// <summary>Hashes a number so that 0 and -0, which compare equal, hash alike.</summary>");
        code.Line("internal static int Hash(double value) => value == 0 ? 0 : value.GetHashCode();");
        code.Blank();
        code.Line("/// <summary>Hashes an optional number.</summary>");
        code.Line("internal static int Hash(double? value) => value is { } present ? Hash(present) : 0;");
        code.Blank();
        code.Line("/// <summary>Compares two lists element by element.</summary>");
        code.Line("internal static bool ListEquals<T>(List<T>? left, List<T>? right, Func<T, T, bool> equals)");
        code.Open("{");
        code.Line("if (ReferenceEquals(left, right)) return true;");
        code.Line("if (left is null || right is null || left.Count != right.Count) return false;");
        code.Line("for (var index = 0; index < left.Count; index++)");
        code.Line("    if (!equals(left[index], right[index])) return false;");
        code.Line("return true;");
        code.Close();
        code.Blank();
        code.Line("/// <summary>Hashes a list from its elements.</summary>");
        code.Line("internal static int ListHash<T>(List<T>? list, Func<T, int> hash)");
        code.Open("{");
        code.Line("if (list is null) return 0;");
        code.Line("unchecked");
        code.Open("{");
        code.Line("var result = 19;");
        code.Line("foreach (var item in list) result = (result * 31) + hash(item);");
        code.Line("return result;");
        code.Close();
        code.Close();
        code.Close();
    }
}
