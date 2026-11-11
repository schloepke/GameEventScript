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
        if (options.Bindings) code.Line("using GameEventScript.Runtime.Values;");
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
            Record(code, layout.Types[index], access, support, qualifier, options.Bindings);
            code.Blank();
        }

        if (options.Bindings)
        {
            Bindings(code, layout, access, qualifier);
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

    private static void Record(GesSchemaPlanner.Code code, GesModelType type, string access, string support, string qualifier, bool bindings)
    {
        var fields = GesModelPlanner.Fields(type);
        var names = new List<(string, string)>();
        foreach (var reserved in Reserved) names.Add((reserved, "the inherited member " + reserved));
        if (bindings)
            foreach (var reserved in new[] { "ToGesValue", "FromGesValue" }) names.Add((reserved, "the generated member " + reserved));
        if (bindings && fields.Exists(field => field.Field.Name == "fields"))
            throw new GesGeneratorException(GesGeneratorException.NameCollision, $"In '{type.Name}', the field fields and the generated field fields of its bindings share a name.");
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
        if (bindings)
        {
            code.Blank();
            code.Line($"/// <summary>Wraps the record as the external value <c>:{type.Name}</c> of <see cref=\"GesTypes\"/>; scripts read its fields when they read them.</summary>");
            code.Line("/// <returns>The GES value.</returns>");
            code.Line($"public GesValue ToGesValue() => GesValue.GesExternal(new GesTypes.{type.Name}Value(this));");
            code.Blank();
            code.Line($"/// <summary>Reads a <c>:{type.Name}</c> that a script holds.</summary>");
            code.Line("/// <param name=\"value\">The value.</param>");
            code.Line("/// <returns>The record, or null for another value.</returns>");
            code.Line($"public static {type.Name}? FromGesValue(in GesValue value) => value.ExternalValue is GesTypes.{type.Name}Value held ? held.Record : null;");
        }

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
    // Outside its owner, as in the generated type set, a nested type also names the owner.
    internal static string Type(GesModelPlanValue value, string? qualifier = null, string? owner = null)
    {
        var plain = Plain(value, qualifier, owner);
        return value.Nullable ? plain + "?" : plain;
    }

    private static string Plain(GesModelPlanValue value, string? qualifier = null, string? owner = null) => value.Kind switch
    {
        GesModelKind.Boolean => "bool",
        GesModelKind.Number => value.Model.Integer ? "long" : "double",
        GesModelKind.Tag => value.ClosedTag ? Nested(value, qualifier, owner) : "string",
        GesModelKind.Text => "string",
        GesModelKind.Bytes => "byte[]",
        GesModelKind.List => $"List<{Type(value.Element!, qualifier, owner)}>",
        GesModelKind.Dice => "List<int>",
        GesModelKind.Point => "GesPoint",
        GesModelKind.Vector => "GesVector",
        GesModelKind.Schema => qualifier + value.Model.Schema,
        _ => Nested(value, qualifier, owner)
    };

    private static string Nested(GesModelPlanValue value, string? qualifier, string? owner) => owner is null ? value.Nested : qualifier + owner + "." + value.Nested;

    private static bool IsReference(GesModelPlanValue value) => value.Kind is GesModelKind.Text or GesModelKind.Bytes or GesModelKind.List or GesModelKind.Dice or GesModelKind.Schema or GesModelKind.Union
        || (value.Kind == GesModelKind.Tag && !value.ClosedTag);

    // The zero value of a property that the type's default would not give: empty texts and lists, new records, the first
    // alternative of a union.
    private static string Initial(GesModelPlanValue value)
    {
        if (value.Nullable) return "";
        return value.Kind switch
        {
            GesModelKind.Text or GesModelKind.Tag when !value.ClosedTag => "\"\"",
            GesModelKind.Bytes => "Array.Empty<byte>()",
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
            GesModelKind.Flags => $"default({value.Nested})",
            GesModelKind.Point or GesModelKind.Vector => $"default({Plain(value)})",
            _ => Initial(value)
        };
    }

    // Nested lists compare and hash through lambdas whose parameters are numbered by depth: x0 and y0, then x1 and y1.
    private static string Equal(GesModelPlanValue value, string left, string right, string support, int depth = 0, string? qualifier = null)
    {
        if (value.Kind is GesModelKind.List or GesModelKind.Dice)
            return $"{support}.ListEquals({left}, {right}, {Comparer(value, support, depth, qualifier)})";
        if (value.Kind == GesModelKind.Bytes) return $"{support}.BytesEquals({left}, {right})";
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
        GesModelKind.Bytes => $"{support}.BytesHash({expression})",
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
        else if (value.Kind == GesModelKind.Flags)
        {
            var members = new List<(string, string)>();
            foreach (var member in value.Model.Tags) members.Add((GesSchemaPlanner.Pascal(member), "the member #" + member));
            GesSchemaPlanner.Unique(value.Nested, members);
            GesCSharpEmitter.FlagsDeclaration(code, value.Nested, value.Model.Tags);
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
        code.Blank();
        code.Line("/// <summary>Compares two byte arrays by their bytes.</summary>");
        code.Line("internal static bool BytesEquals(byte[]? left, byte[]? right)");
        code.Line("    => ReferenceEquals(left, right) || left is not null && right is not null && new ReadOnlySpan<byte>(left).SequenceEqual(right);");
        code.Blank();
        code.Line("/// <summary>Hashes a byte array from its bytes.</summary>");
        code.Line("internal static int BytesHash(byte[]? bytes)");
        code.Open("{");
        code.Line("if (bytes is null) return 0;");
        code.Line("unchecked");
        code.Open("{");
        code.Line("var result = 19;");
        code.Line("foreach (var item in bytes) result = (result * 31) + item;");
        code.Line("return result;");
        code.Close();
        code.Close();
        code.Close();
    }

    private static string UnitName(string unit) => "GesQuantityUnit.Unit" + GesSchemaPlanner.Pascal(unit);

    private static bool IsValueType(GesModelPlanValue value) => value.Kind is GesModelKind.Boolean or GesModelKind.Number or GesModelKind.Point or GesModelKind.Vector or GesModelKind.Flags
        || value.ClosedTag;

    private static string Depth(int depth) => depth.ToString(CultureInfo.InvariantCulture);

    // Converts a native value to a GES value; nullable values pattern-match a numbered variable.
    private static string Encode(GesModelPlanValue value, string owner, string expression, string qualifier, int depth = 0, bool nullable = true)
    {
        if (nullable && value.Nullable)
            return $"{expression} is {{ }} v{Depth(depth)} ? {Encode(value, owner, "v" + Depth(depth), qualifier, depth + 1, false)} : GesValue.GesNothing()";
        return value.Kind switch
        {
            GesModelKind.Boolean => $"GesValue.GesBoolean({expression})",
            GesModelKind.Number when value.Model.Type == "Percentage" => $"GesValue.GesPercentage({expression})",
            GesModelKind.Number when value.Model.Integer => $"GesValue.GesInteger({expression}, {UnitName(value.Model.Unit)})",
            GesModelKind.Number => $"GesValue.GesFloat({expression}, {UnitName(value.Model.Unit)})",
            GesModelKind.Text => $"GesValue.GesText({expression})",
            GesModelKind.Bytes => $"GesValue.GesBytes({expression})",
            GesModelKind.Tag when value.ClosedTag => $"Tag(Name({expression}))",
            GesModelKind.Tag => $"Tag({expression})",
            GesModelKind.List => $"List({expression}, x{Depth(depth)} => {Encode(value.Element!, owner, "x" + Depth(depth), qualifier, depth + 1)})",
            GesModelKind.Dice => $"Dice({expression})",
            GesModelKind.Point or GesModelKind.Vector => $"{expression}.ToValue({UnitName(value.Model.Unit)})",
            GesModelKind.Schema => $"{expression}.ToGesValue()",
            _ => $"Value({expression})"
        };
    }

    // A condition that converts a GES value into the `out` target, false when it does not convert.
    private static string Decode(GesModelPlanValue value, string owner, string expression, string target, string qualifier, int depth = 0, bool nullable = true)
    {
        if (nullable && value.Nullable)
        {
            var inner = $"(in GesValue x{Depth(depth)}, out {Plain(value, qualifier, owner)} y{Depth(depth)}) => {Decode(value, owner, "x" + Depth(depth), "out y" + Depth(depth), qualifier, depth + 1, false)}";
            return $"{(IsValueType(value) ? "OptionalValue" : "OptionalObject")}({expression}, {inner}, {target})";
        }

        return value.Kind switch
        {
            GesModelKind.Boolean => $"Boolean({expression}, {target})",
            GesModelKind.Number when value.Model.Type == "Percentage" => $"Percentage({expression}, {target})",
            GesModelKind.Number when value.Model.Integer => $"Integer({expression}, {UnitName(value.Model.Unit)}, {target})",
            GesModelKind.Number => $"Number({expression}, {UnitName(value.Model.Unit)}, {target})",
            GesModelKind.Text => $"Text({expression}, {target})",
            GesModelKind.Bytes => $"Bytes({expression}, {target})",
            GesModelKind.Tag when value.ClosedTag => $"Decode{owner}{value.Nested}({expression}, {target})",
            GesModelKind.Tag => $"Tag({expression}, {target})",
            GesModelKind.List => $"List({expression}, (in GesValue x{Depth(depth)}, out {Type(value.Element!, qualifier, owner)} y{Depth(depth)}) => {Decode(value.Element!, owner, "x" + Depth(depth), "out y" + Depth(depth), qualifier, depth + 1)}, {target})",
            GesModelKind.Dice => $"Dice({expression}, {target})",
            GesModelKind.Point => $"Point({expression}, {UnitName(value.Model.Unit)}, {target})",
            GesModelKind.Vector => $"Vector({expression}, {UnitName(value.Model.Unit)}, {target})",
            GesModelKind.Schema => $"Decode{value.Model.Schema}({expression}, {target})",
            _ => $"Decode{owner}{value.Nested}({expression}, {target})"
        };
    }

    private static string Definition(string name, GesModelPlanValue value, string kind)
    {
        var (declared, unit, typeName) = GesModelPlanner.Declared(value);
        if (declared is null) return $"new GesExternalType{kind}Definition({GesSchemaPlanner.Literal(name, false)}, {GesSchemaPlanner.Literal(typeName!, false)})";
        var kindName = "GesBytecodeTypeKind." + declared;
        return unit == "none" ? $"new GesExternalType{kind}Definition({GesSchemaPlanner.Literal(name, false)}, {kindName})" : $"new GesExternalType{kind}Definition({GesSchemaPlanner.Literal(name, false)}, {kindName}, {UnitName(unit)})";
    }

    private static void Conversions(GesModelPlanValue value, List<GesModelPlanValue> found)
    {
        if (value.Kind is GesModelKind.Union or GesModelKind.Flags || value.ClosedTag) found.Add(value);
        if (value.Element is not null) Conversions(value.Element, found);
        foreach (var alternative in value.Alternatives) Conversions(alternative, found);
    }

    // The catalog and constructor registry of the models, with an external value and a constructor per type.
    private static void Bindings(GesSchemaPlanner.Code code, GesModelLayout layout, string access, string qualifier)
    {
        var ordered = GesModelPlanner.DependencyOrder(layout);
        code.Line("/// <summary>");
        code.Line("/// The model types for scripts as external types: pass one instance to the compiler as its catalog and to the host as its");
        code.Line("/// external-type registry. Each type has its fields and one constructor that takes every field by name; a value that does not");
        code.Line("/// convert, such as another unit or kind, constructs <c>nothing</c>. Types and constructors that this set does not declare");
        code.Line("/// resolve through the next catalog and registry.");
        code.Line("/// </summary>");
        code.Line($"{access} sealed class GesTypes : IGesExternalTypeCatalog, IGesExternalTypeRegistry");
        code.Open("{");
        code.Line($"private static readonly GesExternalTypeDefinition[] Own = {{ {string.Join(", ", ordered.ConvertAll(type => type.Name + "Value.Declaration"))} }};");
        code.Line("private readonly IGesExternalTypeCatalog? _nextCatalog;");
        code.Line("private readonly IGesExternalTypeRegistry? _nextRegistry;");
        code.Line("private readonly List<GesExternalTypeDefinition> _types;");
        code.Blank();
        code.Line("/// <summary>Creates the set in front of another catalog and registry.</summary>");
        code.Line("/// <param name=\"nextCatalog\">The catalog that declares every other type, or null.</param>");
        code.Line("/// <param name=\"nextRegistry\">The registry that resolves every other constructor, or null.</param>");
        code.Line("/// <exception cref=\"ArgumentException\">The next catalog already declares a type of this set.</exception>");
        code.Line("public GesTypes(IGesExternalTypeCatalog? nextCatalog = null, IGesExternalTypeRegistry? nextRegistry = null)");
        code.Open("{");
        code.Line("_types = new List<GesExternalTypeDefinition>(Own);");
        code.Line("if (nextCatalog is not null)");
        code.Open("{");
        code.Line("foreach (var type in nextCatalog.Types)");
        code.Open("{");
        code.Line("foreach (var own in Own)");
        code.Line("    if (own.Name == type.Name) throw new ArgumentException(\"The type :\" + type.Name + \" is already declared.\", nameof(nextCatalog));");
        code.Line("_types.Add(type);");
        code.Close();
        code.Close();
        code.Blank();
        code.Line("_nextCatalog = nextCatalog;");
        code.Line("_nextRegistry = nextRegistry;");
        code.Close();
        code.Blank();
        code.Line("/// <summary>Gets the declarations of the generated types, each after the types it holds, then those of the next catalog.</summary>");
        code.Line("public IReadOnlyList<GesExternalTypeDefinition> Types => _types;");
        code.Blank();
        code.Line("/// <summary>Resolves a generated type, or asks the next catalog.</summary>");
        code.Line("/// <param name=\"typeName\">The type name.</param>");
        code.Line("/// <returns>The declaration, or null.</returns>");
        code.Line("public GesExternalTypeDefinition? Resolve(string typeName)");
        code.Open("{");
        code.Line("foreach (var own in Own)");
        code.Line("    if (own.Name == typeName) return own;");
        code.Line("return _nextCatalog?.Resolve(typeName);");
        code.Close();
        code.Blank();
        code.Line("/// <summary>Resolves the constructor of a generated type, or asks the next registry.</summary>");
        code.Line("/// <param name=\"reference\">The constructor reference.</param>");
        code.Line("/// <returns>The constructor, or null.</returns>");
        code.Line("public IGesExternalTypeConstructor? Resolve(GesExternalTypeConstructorReference reference)");
        code.Open("{");
        code.Line("if (reference is null) throw new ArgumentNullException(nameof(reference));");
        foreach (var type in ordered) code.Line($"if (reference.SignatureId == {type.Name}Value.Declaration.Constructors[0].SignatureId) return new {type.Name}Constructor();");
        code.Line("return _nextRegistry?.Resolve(reference);");
        code.Close();
        foreach (var type in ordered)
        {
            var fields = GesModelPlanner.Fields(type);
            var name = qualifier + type.Name;
            code.Blank();
            code.Line($"/// <summary>A <c>:{type.Name}</c> held by a script.</summary>");
            code.Line($"internal sealed class {type.Name}Value : IGesExternalValue");
            code.Open("{");
            code.Line("/// <summary>The declaration that every value of the type shares.</summary>");
            code.Line("internal static readonly GesExternalTypeDefinition Declaration = new GesExternalTypeDefinition(");
            code.Line($"    {GesSchemaPlanner.Literal(type.Name, false)},");
            code.Line($"    new[] {{ {string.Join(", ", fields.ConvertAll(field => Definition(field.Field.Name, field.Value, "Field")))}{(fields.Count > 0 ? ", " : "")}new GesExternalTypeFieldDefinition(\"fields\", GesBytecodeTypeKind.Map) }},");
            code.Line($"    new[] {{ new GesExternalTypeConstructorDefinition({GesSchemaPlanner.Literal(type.Name, false)}, new[] {{ new GesExternalTypeParameterDefinition(\"fields\", GesBytecodeTypeKind.Map) }}) }});");
            code.Blank();
            code.Line($"internal static readonly string[] Names = {{ {string.Join(", ", fields.ConvertAll(field => GesSchemaPlanner.Literal(field.Field.Name, false)))} }};");
            code.Blank();
            code.Line($"internal {type.Name}Value({name} record) => Record = record;");
            code.Blank();
            code.Line($"internal {name} Record {{ get; }}");
            code.Blank();
            code.Line("/// <inheritdoc />");
            code.Line("public GesExternalTypeDefinition Definition => Declaration;");
            code.Blank();
            code.Line("/// <inheritdoc />");
            code.Line("public GesValue? GetField(string fieldName) => fieldName switch");
            code.Open("{");
            foreach (var field in fields) code.Line($"{GesSchemaPlanner.Literal(field.Field.Name, false)} => {Encode(field.Value, type.Name, "Record." + field.Base, qualifier)},");
            code.Line("\"fields\" => Fields(Names, this),");
            code.Line("_ => null");
            code.Close("};");
            code.Close();
            code.Blank();
            code.Line($"/// <summary>Constructs a <c>:{type.Name}</c> from a Map or Record of its fields; an absent optional field may be left out.</summary>");
            code.Line($"private sealed class {type.Name}Constructor : IGesExternalTypeConstructor");
            code.Open("{");
            code.Line("/// <inheritdoc />");
            code.Line($"public GesExternalTypeConstructorDefinition Definition => {type.Name}Value.Declaration.Constructors[0];");
            code.Blank();
            code.Line("/// <inheritdoc />");
            code.Line("public void Invoke(GesExternalTypeConstructorCall call)");
            code.Open("{");
            code.Line("var fields = call.Arguments[0].AsMap();");
            var conditions = new List<string> { "fields is null", $"!Known(fields, {type.Name}Value.Names)" };
            foreach (var field in fields)
                conditions.Add("!" + Decode(field.Value, type.Name, $"Field(fields, {GesSchemaPlanner.Literal(field.Field.Name, false)})", "out var " + Parameter(field.Base), qualifier));
            for (var index = 0; index < conditions.Count; index++)
                code.Line((index == 0 ? "if (" : "    || ") + conditions[index] + (index == conditions.Count - 1 ? ")" : ""));
            code.Open("{");
            code.Line("call.SetNothing();");
            code.Line("return;");
            code.Close();
            code.Blank();

            code.Line($"call.SetExternalValue(new {type.Name}Value(new {name}({string.Join(", ", fields.ConvertAll(field => Parameter(field.Base)))})));");
            code.Close();
            code.Close();
            code.Blank();
            code.Line($"private static bool Decode{type.Name}(in GesValue value, out {name} result)");
            code.Open("{");
            code.Line($"result = {name}.FromGesValue(value)!;");
            code.Line("return result is not null;");
            code.Close();
            var conversions = new List<GesModelPlanValue>();
            foreach (var field in fields) Conversions(field.Value, conversions);
            foreach (var conversion in conversions) Conversion(code, type.Name, qualifier, conversion);
        }

        Helpers(code);
        code.Close();
    }

    private static void Conversion(GesSchemaPlanner.Code code, string owner, string qualifier, GesModelPlanValue value)
    {
        var name = qualifier + owner + "." + value.Nested;
        code.Blank();
        if (value.ClosedTag)
        {
            code.Line($"private static string Name({name} value) => value switch");
            code.Open("{");
            foreach (var tag in value.Model.Tags) code.Line($"{name}.{GesSchemaPlanner.Pascal(tag)} => {GesSchemaPlanner.Literal(tag, false)},");
            code.Line("_ => \"\"");
            code.Close("};");
            code.Blank();
            code.Line($"private static bool Decode{owner}{value.Nested}(in GesValue value, out {name} result)");
            code.Open("{");
            code.Line("result = default;");
            code.Line("if (!Tag(value, out var text)) return false;");
            code.Line("switch (text)");
            code.Open("{");
            foreach (var tag in value.Model.Tags) code.Line($"case {GesSchemaPlanner.Literal(tag, false)}: result = {name}.{GesSchemaPlanner.Pascal(tag)}; return true;");
            code.Line("default: return false;");
            code.Close();
            code.Close();
            return;
        }

        if (value.Kind == GesModelKind.Flags)
        {
            var names = $"{owner}{value.Nested}Names";
            code.Line($"private static readonly string[] {names} = {{ {string.Join(", ", value.Model.Tags.ConvertAll(member => GesSchemaPlanner.Literal(member, false)))} }};");
            code.Blank();
            code.Line($"private static GesValue Value({name} value) => Flags({names}, (ulong)value);");
            code.Blank();
            code.Line($"private static bool Decode{owner}{value.Nested}(in GesValue value, out {name} result)");
            code.Open("{");
            code.Line($"var valid = Flags(value, {names}, out var mask);");
            code.Line($"result = ({name})mask;");
            code.Line("return valid;");
            code.Close();
            return;
        }

        code.Line($"private static GesValue Value({name} value) => value switch");
        code.Open("{");
        foreach (var alternative in value.Alternatives)
            code.Line($"{name}.{alternative.Label} alternative => {Encode(alternative, owner, "alternative.Value", qualifier)},");
        code.Line("_ => GesValue.GesNothing()");
        code.Close("};");
        code.Blank();
        code.Line($"private static bool Decode{owner}{value.Nested}(in GesValue value, out {name} result)");
        code.Open("{");
        for (var index = 0; index < value.Alternatives.Count; index++)
        {
            var alternative = value.Alternatives[index];
            code.Line($"if ({Decode(alternative, owner, "value", "out var alternative" + Depth(index), qualifier)})");
            code.Open("{");
            code.Line($"result = new {name}.{alternative.Label}(alternative{Depth(index)});");
            code.Line("return true;");
            code.Close();
            code.Blank();
        }

        code.Line("result = null!;");
        code.Line("return false;");
        code.Close();
    }

    private static void Helpers(GesSchemaPlanner.Code code)
    {
        code.Blank();
        code.Line("private delegate bool Decoder<T>(in GesValue value, out T result);");
        code.Blank();
        code.Line("private static GesValue Field(GesValueMap fields, string name) => fields.Get(name) ?? GesValue.GesNothing();");
        code.Blank();
        code.Line("private static bool Known(GesValueMap fields, string[] names)");
        code.Open("{");
        code.Line("for (var index = 0; index < fields.Length; index++)");
        code.Line("    if (Array.IndexOf(names, fields.KeyAt(index)) < 0) return false;");
        code.Line("return true;");
        code.Close();
        code.Blank();
        code.Line("private static GesValue Fields(string[] names, IGesExternalValue value)");
        code.Open("{");
        code.Line("var values = new GesValue[names.Length];");
        code.Line("for (var index = 0; index < names.Length; index++) values[index] = value.GetField(names[index]) ?? GesValue.GesNothing();");
        code.Line("return GesValue.GesMap(names, values);");
        code.Close();
        code.Blank();
        code.Line("private static GesValue Flags(string[] names, ulong mask)");
        code.Open("{");
        code.Line("var keys = new List<string>();");
        code.Line("for (var index = 0; index < names.Length; index++)");
        code.Line("    if ((mask >> index & 1) != 0) keys.Add(names[index]);");
        code.Line("var values = new GesValue[keys.Count];");
        code.Line("for (var index = 0; index < values.Length; index++) values[index] = GesValue.GesBoolean(true);");
        code.Line("return GesValue.GesMap(keys.ToArray(), values);");
        code.Close();
        code.Blank();
        code.Line("private static bool Flags(in GesValue value, string[] names, out ulong result)");
        code.Open("{");
        code.Line("result = 0;");
        code.Line("if (value.ValueKind != GesBytecodeTypeKind.Map || value.CustomTypeName is not null || value.MaterializedMap() is not { } map) return false;");
        code.Line("for (var index = 0; index < map.Length; index++)");
        code.Open("{");
        code.Line("var member = Array.IndexOf(names, map.KeyAt(index));");
        code.Line("var set = map.ValueAt(index);");
        code.Line("if (member < 0 || set.ValueKind != GesBytecodeTypeKind.Boolean) return false;");
        code.Line("if (set.AsBoolean()) result |= 1UL << member;");
        code.Close();
        code.Line("return true;");
        code.Close();
        code.Blank();
        code.Line("private static GesValue Tag(string name)");
        code.Open("{");
        code.Line("try");
        code.Open("{");
        code.Line("return GesValue.GesTag(name);");
        code.Close();
        code.Line("catch (ArgumentException)");
        code.Open("{");
        code.Line("return GesValue.GesNothing();");
        code.Close();
        code.Close();
        foreach (var line in new[]
        {
            "private static bool Tag(in GesValue value, out string result) => Read(value.ValueKind is GesBytecodeTypeKind.Tag or GesBytecodeTypeKind.Text, value.AsText(), out result);",
            "private static bool Text(in GesValue value, out string result) => Read(value.ValueKind == GesBytecodeTypeKind.Text, value.AsText(), out result);",
            "private static bool Bytes(in GesValue value, out byte[] result) => Read(value.ValueKind == GesBytecodeTypeKind.Bytes, value.AsBytes().ToArray(), out result);",
            "private static bool Boolean(in GesValue value, out bool result) => Read(value.ValueKind == GesBytecodeTypeKind.Boolean, value.AsBoolean(), out result);",
            "private static bool Percentage(in GesValue value, out double result) => Read(value.ValueKind == GesBytecodeTypeKind.Percentage, value.AsNumber(), out result);",
            "private static bool Number(in GesValue value, GesQuantityUnit unit, out double result) => Read(value.ValueKind is GesBytecodeTypeKind.Integer or GesBytecodeTypeKind.Float && value.ValueUnit == unit, value.AsNumber(), out result);",
            "private static bool Point(in GesValue value, GesQuantityUnit unit, out GesPoint result) => Read(GesPoint.FromValue(value, unit), out result);",
            "private static bool Vector(in GesValue value, GesQuantityUnit unit, out GesVector result) => Read(GesVector.FromValue(value, unit), out result);",
        })
        {
            code.Blank();
            code.Line(line);
        }

        code.Blank();
        code.Line("private static bool Integer(in GesValue value, GesQuantityUnit unit, out long result)");
        code.Open("{");
        code.Line("result = 0;");
        code.Line("if (value.ValueUnit != unit) return false;");
        code.Line("if (value.ValueKind == GesBytecodeTypeKind.Integer) result = value.AsInteger();");
        code.Line("else if (value.ValueKind == GesBytecodeTypeKind.Float && value.AsNumber() is var number && number >= -9.2233720368547758E18 && number < 9.2233720368547758E18 && Math.Floor(number) == number) result = (long)number;");
        code.Line("else return false;");
        code.Line("return true;");
        code.Close();
        code.Blank();
        code.Line("private static bool Read<T>(bool valid, T value, out T result)");
        code.Open("{");
        code.Line("result = valid ? value : default!;");
        code.Line("return valid;");
        code.Close();
        code.Blank();
        code.Line("private static bool Read<T>(T? value, out T result) where T : struct");
        code.Open("{");
        code.Line("result = value.GetValueOrDefault();");
        code.Line("return value.HasValue;");
        code.Close();
        code.Blank();
        code.Line("private static bool OptionalValue<T>(in GesValue value, Decoder<T> decode, out T? result) where T : struct");
        code.Open("{");
        code.Line("result = null;");
        code.Line("if (value.IsNothing) return true;");
        code.Line("if (!decode(value, out var decoded)) return false;");
        code.Line("result = decoded;");
        code.Line("return true;");
        code.Close();
        code.Blank();
        code.Line("private static bool OptionalObject<T>(in GesValue value, Decoder<T> decode, out T? result) where T : class");
        code.Open("{");
        code.Line("result = null;");
        code.Line("if (value.IsNothing) return true;");
        code.Line("if (!decode(value, out var decoded)) return false;");
        code.Line("result = decoded;");
        code.Line("return true;");
        code.Close();
        code.Blank();
        code.Line("private static bool List<T>(in GesValue value, Decoder<T> element, out List<T> result)");
        code.Open("{");
        code.Line("result = null!;");
        code.Line("if (value.ValueKind != GesBytecodeTypeKind.List) return false;");
        code.Line("var items = value.AsList();");
        code.Line("var list = new List<T>(items.Length);");
        code.Line("for (var index = 0; index < items.Length; index++)");
        code.Open("{");
        code.Line("if (!element(items[index], out var item)) return false;");
        code.Line("list.Add(item);");
        code.Close();
        code.Blank();
        code.Line("result = list;");
        code.Line("return true;");
        code.Close();
        code.Blank();
        code.Line("private static GesValue List<T>(List<T> list, Func<T, GesValue> element)");
        code.Open("{");
        code.Line("var items = new GesValue[list.Count];");
        code.Line("for (var index = 0; index < items.Length; index++) items[index] = element(list[index]);");
        code.Line("return GesValue.GesList(items);");
        code.Close();
        code.Blank();
        code.Line("private static bool Dice(in GesValue value, out List<int> result)");
        code.Open("{");
        code.Line("result = null!;");
        code.Line("if (value.ValueKind != GesBytecodeTypeKind.Dice) return false;");
        code.Line("result = new List<int>(value.AsDice());");
        code.Line("return true;");
        code.Close();
        code.Blank();
        code.Line("private static GesValue Dice(List<int> rolls)");
        code.Open("{");
        code.Line("foreach (var roll in rolls)");
        code.Line("    if (roll <= 0) return GesValue.GesNothing();");
        code.Line("return GesValue.GesDice(rolls.ToArray());");
        code.Close();
    }
}
