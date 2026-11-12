// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Globalization;
using GameEventScript.Api;

namespace GameEventScript.Generator;

// Writes C# 9 models for Unity: a sealed class per schema with value equality, settable properties in declared order, a
// constructor that sets every field and one that leaves every field at its zero value, an enum per closed Tag, an abstract
// class with one sealed subclass per alternative for a union, and the runtime's GesPoint and GesVector for spatial fields.
// Classes avoid struct copies that would share a list; no `record`, `init` accessor or Reflection is used.
internal static class GesCSharpModelEmitter
{
    // Members every model type declares or inherits.
    private static readonly string[] Reserved = ["Equals", "GetHashCode", "ToString", "GetType"];

    internal static GesCodeDocument Emit(GesModelLayout layout, GesSchemaGenerateOptions options)
    {
        var access = options.Access == GesSchemaAccess.Public ? "public" : "internal";
        var space = GesSchemaPlanner.Namespace(options.Namespace, GesCSharpEmitter.Keywords);
        var prologue = new GesSchemaPlanner.Code();
        prologue.Blank();
        prologue.Line("#nullable enable");
        prologue.Blank();
        prologue.Line("using System;");
        prologue.Line("using System.Collections.Generic;");
        prologue.Line("using GameEventScript.Api;");
        if (options.Bindings || options.Values) prologue.Line("using GameEventScript.Runtime.Values;");
        prologue.Blank();
        var header = GesModelPlanner.Header(layout);
        var suffix = GesSchemaPlanner.Suffix(header, options);
        var support = "__GesModels" + suffix;
        var values = "__GesModelValues" + suffix;
        var qualifier = "global::" + string.Concat(space.ConvertAll(part => part + "."));
        GesModelPlanner.CheckProtocols(layout, swift: false);
        var document = new GesCodeDocument { Header = header, Prologue = prologue.ToString(), Space = space };
        var computed = GesModelPlanner.Computed(layout, GesComputedEmitter.Helper(options.Namespace, suffix), swift: false);
        var documents = options.Documents ? "__GesModelDocuments" + suffix : null;
        var documentType = options.Documents ? GesModelPlanner.DocumentType(layout, space) : "";
        foreach (var type in layout.Types)
            document.Units.Add(new GesCodeUnit(type.Name, code => Record(code, layout, type, access, support, qualifier, options.Bindings, options.Values ? values : null, computed, layout.Roots.Contains(type.Name) ? documents : null)));
        foreach (var protocol in layout.Protocols) document.Units.Add(new GesCodeUnit(protocol.Name, code => Interface(code, layout, protocol, access)));
        foreach (var (name, tags) in GesModelPlanner.TagSets(layout))
            document.Units.Add(new GesCodeUnit(name, code => TagEnum(code, name, tags, access, $"/// <summary>The tag set <c>:{name}</c>, in declared order.</summary>")));
        if (options.Bindings) document.Units.Add(new GesCodeUnit("", code => Bindings(code, layout, access, qualifier, support, computed)));
        if (options.Values) document.Units.Add(new GesCodeUnit("", code => Values(code, layout, values, qualifier, support, computed)));
        if (documents is not null)
        {
            document.Units.Add(new GesCodeUnit("", code => DocumentType(code, layout, documentType, documents, access, qualifier)));
            document.Units.Add(new GesCodeUnit("", code => DocumentsHolder(code, layout, documents)));
        }

        var geometry = GesModelPlanner.UsesGeometry(layout);
        document.Units.Add(new GesCodeUnit("", code => Support(code, support, options.Bindings || options.Values, geometry)));
        if (layout.Types.Exists(type => type.Computed.Length > 0))
            document.Units.Add(new GesCodeUnit("", code => GesComputedLibrary.Write(code, computed.HelperName, swift: false, options.Bindings || options.Values || computed.NeedsValues(layout.Types.ConvertAll(type => (type.Name, type.Computed))))));
        return document;
    }

    // Reads a document of any root by its header, so that a host reading files of several kinds needs no switch on names.
    private static void DocumentType(GesSchemaPlanner.Code code, GesModelLayout layout, string name, string documents, string access, string qualifier)
    {
        code.Line($"/// <summary>A GES document of one of the model roots of this file, chosen by its header <c>:Schema(:Name)</c>.</summary>");
        code.Line($"{access} abstract class {name}");
        code.Open("{");
        code.Line($"private {name}()");
        code.Open("{");
        code.Close();
        foreach (var root in layout.Roots)
        {
            code.Blank();
            code.Line($"/// <summary>A document of <c>:{root}</c>.</summary>");
            code.Line($"public sealed class {root} : {name}");
            code.Open("{");
            code.Line("/// <summary>Holds a record.</summary>");
            code.Line("/// <param name=\"value\">The record.</param>");
            code.Line($"public {root}({qualifier}{root} value)");
            code.Open("{");
            code.Line("Value = value;");
            code.Close();
            code.Blank();
            code.Line("/// <summary>Gets the record.</summary>");
            code.Line($"public {qualifier}{root} Value {{ get; }}");
            code.Close();
        }

        code.Blank();
        code.Line("/// <summary>Reads GES document text whose header names a model root of this file, as that root's <c>FromDocument</c> does.</summary>");
        code.Line("/// <param name=\"text\">The document text.</param>");
        code.Line("/// <returns>The document of the root its header names.</returns>");
        code.Line("/// <exception cref=\"GesDocumentException\">The text is no document, names no root of this file (<c>document.schemaMismatch</c>), or");
        code.Line("/// breaks its schema, with the <c>document.*</c> or <c>wire.*</c> code, the line, the column and the field path.</exception>");
        code.Line($"public static {name} FromDocument(string text)");
        code.Open("{");
        code.Line($"var document = {documents}.Read(text{string.Concat(layout.Roots.ConvertAll(root => ", " + GesSchemaPlanner.Literal(root, false)))});");
        code.Line("try");
        code.Open("{");
        code.Line("switch (document.Schema)");
        code.Open("{");
        for (var index = 0; index < layout.Roots.Count; index++)
        {
            var root = layout.Roots[index];
            code.Line(index + 1 < layout.Roots.Count ? $"case {GesSchemaPlanner.Literal(root, false)}:" : "default:");
            code.Line($"    return new {root}({qualifier}{root}.FromValue(document.Value));");
        }

        code.Close();
        code.Close();
        code.Line("catch (GesWireException error)");
        code.Open("{");
        code.Line("throw document.Locate(error);");
        code.Close();
        code.Close();
        code.Close();
    }

    // The schema sources of this file, parsed when a document is first read.
    private static void DocumentsHolder(GesSchemaPlanner.Code code, GesModelLayout layout, string holder)
    {
        code.Line("/// <summary>The schema sources of this file, which reading documents checks them against.</summary>");
        code.Line($"internal static class {holder}");
        code.Open("{");
        code.Line("private static GesSchemaSet? __set;");
        code.Blank();
        code.Line("internal static GesSchemaDocument Read(string text, params string[] roots)");
        code.Open("{");
        code.Line("var set = __set ??= GesSchemaSet.Parse(");
        code.Indent();
        for (var index = 0; index < layout.Sources.Count; index++)
        {
            var source = layout.Sources[index];
            code.Line($"new GesSchemaSource({GesSchemaPlanner.Literal(source.Name, false)}, string.Join(\"\\n\", new[]");
            code.Open("{");
            foreach (var line in source.Text.Split('\n')) code.Line(GesSchemaPlanner.Literal(line, false) + ",");
            code.Close(index + 1 < layout.Sources.Count ? "}))," : "})));");
        }

        code.Dedent();
        code.Line("return GesSchemaDocument.Read(set, text, roots);");
        code.Close();
        code.Close();
    }

    private static string Parameter(string pascal)
    {
        var name = GesSchemaPlanner.Camel(pascal);
        return GesCSharpEmitter.Keywords.Contains(name) ? "@" + name : name;
    }

    // The read-only properties of the fields a schema lends with `with`.
    private static void Interface(GesSchemaPlanner.Code code, GesModelLayout layout, GesModelProtocol protocol, string access)
    {
        var refines = protocol.Takes.ConvertAll(name => GesModelPlanner.ProtocolName(layout, name, swift: false));
        code.Line($"/// <summary>The fields of the schema <c>:{protocol.Name}</c>, which every model type that takes them unchanged with <c>with</c> has.</summary>");
        code.Line($"{access} interface {GesModelPlanner.ProtocolName(layout, protocol.Name, swift: false)}{(refines.Count > 0 ? " : " + string.Join(", ", refines) : "")}");
        code.Open("{");
        var first = true;
        foreach (var field in GesModelPlanner.Fields(new GesModelType { Name = protocol.Name, Fields = protocol.Fields }))
        {
            if (!first) code.Blank();
            first = false;
            code.Line($"/// <summary><c>{Escape(field.Field.Name + ": " + GesModelPlanner.Describe(field.Field))}</c></summary>");
            code.Line($"{Type(field.Value)} {field.Base} {{ get; }}");
        }

        code.Close();
    }

    private static void Record(GesSchemaPlanner.Code code, GesModelLayout layout, GesModelType type, string access, string support, string qualifier, bool bindings, string? values, GesComputedEmitter computed, string? documents)
    {
        var fields = GesModelPlanner.Fields(type);
        var names = new List<(string, string)>();
        foreach (var reserved in Reserved) names.Add((reserved, "the inherited member " + reserved));
        names.Add(("SchemaHash", "the generated member SchemaHash"));
        if (bindings)
            foreach (var reserved in new[] { "ToGesValue", "FromGesValue" }) names.Add((reserved, "the generated member " + reserved));
        if (values is not null)
            foreach (var reserved in new[] { "ToValue", "FromValue" }) names.Add((reserved, "the generated member " + reserved));
        if (documents is not null) names.Add(("FromDocument", "the generated member FromDocument"));
        if (bindings && fields.Exists(field => field.Field.Name == "fields"))
            throw new GesGeneratorException(GesGeneratorException.NameCollision, $"In '{type.Name}', the field fields and the generated field fields of its bindings share a name.");
        foreach (var field in fields)
        {
            names.Add((field.Base, "the field " + field.Field.Name));
            Nested(field.Value, names, field.Field.Name);
        }

        foreach (var constant in type.Constants) names.Add((GesSchemaPlanner.Pascal(constant.Name), "the let " + constant.Name));
        GesComputedEmitter.Names(type.Computed, type.Name, swift: false, names);
        GesSchemaPlanner.Unique(type.Name, names);

        code.Line($"/// <summary>The model of the schema <c>:{type.Name}</c>: a plain value that is never encoded and compares by value.</summary>");
        code.Line($"{access} sealed class {type.Name} : IEquatable<{type.Name}>{string.Concat(GesModelPlanner.Adopted(layout, type, swift: false).ConvertAll(name => ", " + name))}");
        code.Open("{");
        code.Line($"/// <summary>The hash of the schema <c>:{type.Name}</c>, which wire records of it carry too; a test that compares it notices stale generated code.</summary>");
        code.Line($"public const ulong SchemaHash = 0x{GesSchemaLayoutJson.Hex(type.Hash)}UL;");
        GesCSharpEmitter.Lets(code, type.Constants);
        code.Blank();
        var order = GesModelPlanner.ReadOrder(fields);
        code.Line("/// <summary>Creates a record with every field at its default or zero value: absent, empty or the first alternative.</summary>");
        code.Line($"public {type.Name}()");
        code.Open("{");
        foreach (var index in order)
            if (GesModelPlanner.Sibling(fields, fields[index]) is { } named) code.Line($"{fields[index].Base} = {fields[named].Base};");
        code.Close();
        if (fields.Count > 0)
        {
            // Optional parameters end the list, so a default becomes one only where every later field has a default too.
            var trailing = fields.Count;
            while (trailing > 0 && fields[trailing - 1].Field.Default is not null) trailing--;
            code.Blank();
            code.Line("/// <summary>Creates a record from its fields.</summary>");
            foreach (var field in fields)
                code.Line($"/// <param name=\"{GesSchemaPlanner.Camel(field.Base)}\">The field <c>{field.Field.Name}</c>{(GesModelPlanner.Sibling(fields, field) is { } named ? $"; null takes <c>{fields[named].Field.Name}</c>" : "")}.</param>");
            var parameters = new List<string>();
            for (var index = 0; index < fields.Count; index++)
            {
                var field = fields[index];
                var optional = index >= trailing;
                parameters.Add(GesModelPlanner.Sibling(fields, field) is null
                    ? $"{Type(field.Value)} {Parameter(field.Base)}{(optional ? " = " + Native(field.Value, field.Field.Default!) : "")}"
                    : $"{Plain(field.Value)}? {Parameter(field.Base)}{(optional ? " = null" : "")}");
            }

            code.Line($"public {type.Name}({string.Join(", ", parameters)})");
            code.Open("{");
            foreach (var index in order)
            {
                var field = fields[index];
                code.Line($"{field.Base} = {Parameter(field.Base)}{(GesModelPlanner.Sibling(fields, field) is { } named ? " ?? " + fields[named].Base : "")};");
            }

            code.Close();
        }

        foreach (var field in fields)
        {
            code.Blank();
            code.Line($"/// <summary><c>{Escape(field.Field.Name + ": " + GesModelPlanner.Describe(field.Field))}</c></summary>");
            var initial = field.Field.Default is { Kind: not GesSchemaDefaultKind.Field } fallback ? Native(field.Value, fallback) : Initial(field.Value);
            code.Line($"public {Type(field.Value)} {field.Base} {{ get; set; }}{(initial.Length > 0 ? " = " + initial + ";" : "")}");
        }

        computed.Members(code, type.Name, type.Computed, "public");

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

        if (values is not null)
        {
            code.Blank();
            code.Line("/// <summary>Converts the record into a plain GES Record of its fields, as <see cref=\"GesSchemaValues\"/> decodes the bytes of the record.</summary>");
            code.Line("/// <returns>The Record.</returns>");
            code.Line($"public GesValue ToValue() => {values}.Value(this);");
            code.Blank();
            code.Line("/// <summary>Reads a Record of this schema, or a Map of only its fields, as <see cref=\"GesSchemaValues\"/> encodes it.</summary>");
            code.Line("/// <param name=\"value\">The value.</param>");
            code.Line("/// <returns>The record.</returns>");
            code.Line("/// <exception cref=\"GesWireException\">A field does not convert: <c>wire.typeMismatch</c> at its path.</exception>");
            code.Line($"public static {type.Name} FromValue(GesValue value) => {values}.Read{type.Name}(value, \"\");");
        }

        if (documents is not null)
        {
            code.Blank();
            code.Line($"/// <summary>Reads a record from GES document text with the header <c>:Schema(:{type.Name})</c>: the document is checked against");
            code.Line("/// the schema sources of this file, and fields it leaves out take their defaults. Reading allocates and does no file I/O.</summary>");
            code.Line("/// <param name=\"text\">The document text.</param>");
            code.Line("/// <returns>The record.</returns>");
            code.Line("/// <exception cref=\"GesDocumentException\">The text is no document, names another schema (<c>document.schemaMismatch</c>), or breaks");
            code.Line("/// the schema, with the <c>document.*</c> or <c>wire.*</c> code, the line, the column and the field path.</exception>");
            code.Line($"public static {type.Name} FromDocument(string text)");
            code.Open("{");
            code.Line($"var document = {documents}.Read(text, {GesSchemaPlanner.Literal(type.Name, false)});");
            code.Line("try");
            code.Open("{");
            code.Line("return FromValue(document.Value);");
            code.Close();
            code.Line("catch (GesWireException error)");
            code.Open("{");
            code.Line("throw document.Locate(error);");
            code.Close();
            code.Close();
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
        if (value.Nested.Length > 0 && value.TagSet.Length == 0) names.Add((value.Nested, "a type of the field " + field));
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
        GesModelKind.Box => "GesBox",
        GesModelKind.Poly => "GesPoly",
        GesModelKind.Schema => qualifier + value.Model.Schema,
        _ => Nested(value, qualifier, owner)
    };

    private static string Nested(GesModelPlanValue value, string? qualifier, string? owner)
        => value.TagSet.Length > 0 ? qualifier + value.TagSet : owner is null ? value.Nested : qualifier + owner + "." + value.Nested;

    private static bool IsReference(GesModelPlanValue value) => value.Kind is GesModelKind.Text or GesModelKind.Bytes or GesModelKind.List or GesModelKind.Dice or GesModelKind.Schema or GesModelKind.Union or GesModelKind.Poly
        || (value.Kind == GesModelKind.Tag && !value.ClosedTag);

    // The zero value of a property that the type's default would not give: empty texts and lists, new records, the first
    // alternative of a union.
    // A literal default as a native constant.
    private static string Native(GesModelPlanValue value, GesLayoutDefault fallback) => fallback.Kind switch
    {
        GesSchemaDefaultKind.Boolean => fallback.Boolean ? "true" : "false",
        GesSchemaDefaultKind.Tag when value.ClosedTag => Plain(value) + "." + GesSchemaPlanner.Pascal(fallback.Value),
        GesSchemaDefaultKind.Tag => GesSchemaPlanner.Literal(fallback.Value, false),
        _ => GesModelPlanner.Number(value, fallback)
    };

    // The GES value that a field left out of a Record or Map takes: its literal default, the value of the sibling it names,
    // or else `missing`.
    private static string Held(List<GesModelPlanField> fields, int index, string owner, string qualifier, string support, Func<int, string> sibling, string missing)
    {
        var field = fields[index];
        var key = $"fields.Get({GesSchemaPlanner.Literal(field.Field.Name, false)})";
        return field.Field.Default switch
        {
            null => missing,
            { Kind: GesSchemaDefaultKind.Field } => $"{key} ?? {sibling(GesModelPlanner.Sibling(fields, field)!.Value)}",
            { Kind: GesSchemaDefaultKind.Tag } fallback => $"{key} ?? GesValue.GesTag({GesSchemaPlanner.Literal(fallback.Value, false)})",
            { } fallback => $"{key} ?? {Encode(field.Value, owner, Native(field.Value, fallback), qualifier, support, nullable: false, plain: true)}"
        };
    }

    // A field's GES value inline, following defaults to siblings; nothing when no value or default is left.
    private static string Inline(List<GesModelPlanField> fields, int index, string owner, string qualifier, string support)
        => Held(fields, index, owner, qualifier, support, sibling => Inline(fields, sibling, owner, qualifier, support), $"{support}.Field(fields, {GesSchemaPlanner.Literal(fields[index].Field.Name, false)})");

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
            GesModelKind.Poly => "new GesPoly(Array.Empty<GesPoint>())",
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
            GesModelKind.Point or GesModelKind.Vector or GesModelKind.Box => $"default({Plain(value)})",
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
            if (value.TagSet.Length > 0) return;
            code.Blank();
            TagEnum(code, value.Nested, value.Model.Tags, "public", "/// <summary>The tags, in declared order.</summary>");
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

    // A closed Tag's enum, nested in its record or, for a named tag set, declared once beside the records.
    private static void TagEnum(GesSchemaPlanner.Code code, string name, List<string> tags, string access, string summary)
    {
        var members = new List<(string, string)>();
        foreach (var tag in tags) members.Add((GesSchemaPlanner.Pascal(tag), "the tag #" + tag));
        GesSchemaPlanner.Unique(name, members);
        code.Line(summary);
        code.Line($"{access} enum {name}");
        code.Open("{");
        foreach (var tag in tags)
        {
            code.Line($"/// <summary>The tag <c>#{tag}</c>.</summary>");
            code.Line(GesSchemaPlanner.Pascal(tag) + ",");
        }

        code.Close();
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

    // Equality and hashing helpers of the file, named after its header so that two generated files can share a namespace, and
    // the conversions that the script bindings and the plain values share.
    private static void Support(GesSchemaPlanner.Code code, string name, bool conversions, bool geometry)
    {
        code.Line($"/// <summary>Value equality of lists and numbers{(conversions ? " and GES value conversions" : "")} for the models of this file.</summary>");
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
        if (conversions) Helpers(code);
        if (conversions && geometry)
        {
            code.Blank();
            code.Line("internal static bool Box(in GesValue value, GesQuantityUnit unit, out GesBox result) => Read(GesBox.FromValue(value, unit), out result);");
            code.Blank();
            code.Line("internal static bool Poly(in GesValue value, GesQuantityUnit unit, out GesPoly result)");
            code.Open("{");
            code.Line("result = GesPoly.FromValue(value, unit)!;");
            code.Line("return result is not null;");
            code.Close();
        }

        code.Close();
    }

    private static string UnitName(string unit) => "GesQuantityUnit.Unit" + GesSchemaPlanner.Pascal(unit);

    private static bool IsValueType(GesModelPlanValue value) => value.Kind is GesModelKind.Boolean or GesModelKind.Number or GesModelKind.Point or GesModelKind.Vector or GesModelKind.Box or GesModelKind.Flags
        || value.ClosedTag;

    private static string Depth(int depth) => depth.ToString(CultureInfo.InvariantCulture);

    // Converts a native value to a GES value; nullable values pattern-match a numbered variable.
    // A plain value holds nested records as Records rather than as the external values of the bindings. Pattern variables
    // start with `pattern`, so that statements in one scope keep them apart.
    private static string Encode(GesModelPlanValue value, string owner, string expression, string qualifier, string support, int depth = 0, bool nullable = true, bool plain = false, string pattern = "v")
    {
        if (nullable && value.Nullable)
            return $"{expression} is {{ }} {pattern}{Depth(depth)} ? {Encode(value, owner, pattern + Depth(depth), qualifier, support, depth + 1, false, plain, pattern)} : GesValue.GesNothing()";
        return value.Kind switch
        {
            GesModelKind.Boolean => $"GesValue.GesBoolean({expression})",
            GesModelKind.Number when value.Model.Type == "Percentage" => $"GesValue.GesPercentage({expression})",
            GesModelKind.Number when value.Model.Integer => $"GesValue.GesInteger({expression}, {UnitName(value.Model.Unit)})",
            GesModelKind.Number => $"GesValue.GesFloat({expression}, {UnitName(value.Model.Unit)})",
            GesModelKind.Text => $"GesValue.GesText({expression})",
            GesModelKind.Bytes => $"GesValue.GesBytes({expression})",
            GesModelKind.Tag when value.ClosedTag => $"{support}.Tag(Name({expression}))",
            GesModelKind.Tag => $"{support}.Tag({expression})",
            GesModelKind.List => $"{support}.List({expression}, x{Depth(depth)} => {Encode(value.Element!, owner, "x" + Depth(depth), qualifier, support, depth + 1, plain: plain, pattern: pattern)})",
            GesModelKind.Dice => $"{support}.Dice({expression})",
            GesModelKind.Point or GesModelKind.Vector or GesModelKind.Box or GesModelKind.Poly => $"{expression}.ToValue({UnitName(value.Model.Unit)})",
            GesModelKind.Schema => plain ? $"{expression}.ToValue()" : $"{expression}.ToGesValue()",
            _ => $"Value({expression})"
        };
    }

    // A condition that converts a GES value into the `out` target, false when it does not convert.
    private static string Decode(GesModelPlanValue value, string owner, string expression, string target, string qualifier, string support, int depth = 0, bool nullable = true)
    {
        if (nullable && value.Nullable)
        {
            var inner = $"(in GesValue x{Depth(depth)}, out {Plain(value, qualifier, owner)} y{Depth(depth)}) => {Decode(value, owner, "x" + Depth(depth), "out y" + Depth(depth), qualifier, support, depth + 1, false)}";
            return $"{support}.{(IsValueType(value) ? "OptionalValue" : "OptionalObject")}({expression}, {inner}, {target})";
        }

        return value.Kind switch
        {
            GesModelKind.Boolean => $"{support}.Boolean({expression}, {target})",
            GesModelKind.Number when value.Model.Type == "Percentage" => $"{support}.Percentage({expression}, {target})",
            GesModelKind.Number when value.Model.Integer => $"{support}.Integer({expression}, {UnitName(value.Model.Unit)}, {target})",
            GesModelKind.Number => $"{support}.Number({expression}, {UnitName(value.Model.Unit)}, {target})",
            GesModelKind.Text => $"{support}.Text({expression}, {target})",
            GesModelKind.Bytes => $"{support}.Bytes({expression}, {target})",
            GesModelKind.Tag when value.ClosedTag => $"Decode{GesModelPlanner.ConversionName(owner, value)}({expression}, {target})",
            GesModelKind.Tag => $"{support}.Tag({expression}, {target})",
            GesModelKind.List => $"{support}.List({expression}, (in GesValue x{Depth(depth)}, out {Type(value.Element!, qualifier, owner)} y{Depth(depth)}) => {Decode(value.Element!, owner, "x" + Depth(depth), "out y" + Depth(depth), qualifier, support, depth + 1)}, {target})",
            GesModelKind.Dice => $"{support}.Dice({expression}, {target})",
            GesModelKind.Point => $"{support}.Point({expression}, {UnitName(value.Model.Unit)}, {target})",
            GesModelKind.Vector => $"{support}.Vector({expression}, {UnitName(value.Model.Unit)}, {target})",
            GesModelKind.Box => $"{support}.Box({expression}, {UnitName(value.Model.Unit)}, {target})",
            GesModelKind.Poly => $"{support}.Poly({expression}, {UnitName(value.Model.Unit)}, {target})",
            GesModelKind.Schema => $"Decode{value.Model.Schema}({expression}, {target})",
            _ => $"Decode{owner}{value.Nested}({expression}, {target})"
        };
    }

    // Whether reading a value descends into it, so that a failure inside reports its own path: a nested record or a list.
    private static bool Descends(GesModelPlanValue value) => value.Kind is GesModelKind.Schema or GesModelKind.List;

    // An expression that reads a value that descends, or throws wire.typeMismatch at the path of the part that does not convert:
    // a nested record's field, or a list element with `[]`, as the value path names them.
    private static string Read(GesModelPlanValue value, string owner, string expression, string path, string qualifier, string support, int depth = 0, bool nullable = true)
    {
        var x = "x" + Depth(depth);
        var p = "p" + Depth(depth);
        if (nullable && value.Nullable) return $"ReadOptional({expression}, {path}, (in GesValue {x}, string {p}) => {Read(value, owner, x, p, qualifier, support, depth + 1, false)})";
        if (value.Kind == GesModelKind.Schema) return $"Read{value.Model.Schema}({expression}, {path})";
        var element = Descends(value.Element!)
            ? Read(value.Element!, owner, x, p, qualifier, support, depth + 1)
            : $"{Decode(value.Element!, owner, x, "out var y" + Depth(depth), qualifier, support, depth + 1)} ? y{Depth(depth)} : throw Failure({x}, {p})";
        return $"ReadList({expression}, {path}, (in GesValue {x}, string {p}) => {element})";
    }

    private static string Definition(string name, GesModelPlanValue value, string kind)
    {
        var (declared, unit, typeName) = GesModelPlanner.Declared(value);
        if (declared is null) return $"new GesExternalType{kind}Definition({GesSchemaPlanner.Literal(name, false)}, {GesSchemaPlanner.Literal(typeName!, false)})";
        var kindName = "GesBytecodeTypeKind." + declared;
        return unit == "none" ? $"new GesExternalType{kind}Definition({GesSchemaPlanner.Literal(name, false)}, {kindName})" : $"new GesExternalType{kind}Definition({GesSchemaPlanner.Literal(name, false)}, {kindName}, {UnitName(unit)})";
    }

    // A computed field reads like a field of its type; scripts cannot set it.
    private static string ComputedDefinition(GesResolvedComputed field) => field.Kind switch
    {
        GesSchemaTypeKind.Boolean => $"new GesExternalTypeFieldDefinition({GesSchemaPlanner.Literal(field.Name, false)}, GesBytecodeTypeKind.Boolean)",
        GesSchemaTypeKind.Percentage => $"new GesExternalTypeFieldDefinition({GesSchemaPlanner.Literal(field.Name, false)}, GesBytecodeTypeKind.Percentage)",
        GesSchemaTypeKind.Quantity => $"new GesExternalTypeFieldDefinition({GesSchemaPlanner.Literal(field.Name, false)}, GesBytecodeTypeKind.Float, {UnitName(field.Unit.ToTypeName())})",
        _ => $"new GesExternalTypeFieldDefinition({GesSchemaPlanner.Literal(field.Name, false)}, GesBytecodeTypeKind.Float)"
    };

    private static void Conversions(GesModelPlanValue value, List<GesModelPlanValue> found)
    {
        if (value.Kind is GesModelKind.Union or GesModelKind.Flags || value.ClosedTag) found.Add(value);
        if (value.Element is not null) Conversions(value.Element, found);
        foreach (var alternative in value.Alternatives) Conversions(alternative, found);
    }

    // The plain values of the models: a Record of each schema with its fields in declared order, as the value path decodes the
    // bytes of a record, so that wire records read them and give them back.
    private static void Values(GesSchemaPlanner.Code code, GesModelLayout layout, string name, string qualifier, string support, GesComputedEmitter computed)
    {
        code.Line("/// <summary>The plain GES values of the models of this file, as <see cref=\"GesSchemaValues\"/> reads and writes them.</summary>");
        code.Line($"internal static class {name}");
        code.Open("{");
        var first = true;
        var emitted = new HashSet<string>(System.StringComparer.Ordinal);
        foreach (var type in layout.Types)
        {
            var fields = GesModelPlanner.Fields(type);
            var record = qualifier + type.Name;
            var names = type.Name + "Fields";
            if (!first) code.Blank();
            first = false;
            code.Line($"private static readonly string[] {names} = {{ {string.Join(", ", fields.ConvertAll(field => GesSchemaPlanner.Literal(field.Field.Name, false)))} }};");
            code.Blank();
            // One statement per field, so that the pattern variables of optional fields stay apart.
            code.Line($"internal static GesValue Value({record} record)");
            code.Open("{");
            code.Line($"var values = new GesValue[{(fields.Count + type.Computed.Length).ToString(CultureInfo.InvariantCulture)}];");
            for (var index = 0; index < fields.Count; index++) code.Line($"values[{Depth(index)}] = {Encode(fields[index].Value, type.Name, "record." + fields[index].Base, qualifier, support, plain: true, pattern: "value" + Depth(index) + "_")};");

            // A value also carries the computed fields, as decoding the record's bytes gives them; reading ignores them.
            for (var index = 0; index < type.Computed.Length; index++)
            {
                var field = type.Computed[index];
                var unit = UnitName(field.Kind == GesSchemaTypeKind.Quantity ? field.Unit.ToTypeName() : "none");
                code.Line($"values[{Depth(fields.Count + index)}] = {computed.HelperName}.ToGesValue({computed.ValueOf("record", type.Name, field)}, {unit});");
            }

            var keyNames = fields.ConvertAll(field => GesSchemaPlanner.Literal(field.Field.Name, false));
            foreach (var field in type.Computed) keyNames.Add(GesSchemaPlanner.Literal(field.Name, false));
            var keys = type.Computed.Length == 0 ? names : $"new[] {{ {string.Join(", ", keyNames)} }}";
            code.Line($"return GesValue.GesRecord({GesSchemaPlanner.Literal(type.Name, false)}, {keys}, values);");
            code.Close();
            code.Blank();
            code.Line($"internal static {record} Read{type.Name}(in GesValue value, string path)");
            code.Open("{");
            code.Line($"var fields = RecordFields(value, {GesSchemaPlanner.Literal(type.Name, false)}, {names}) ?? throw Failure(value, path);");
            foreach (var index in GesModelPlanner.ReadOrder(fields))
            {
                var field = fields[index];
                var literal = GesSchemaPlanner.Literal(field.Field.Name, false);
                var held = "value" + Depth(index);
                var missing = field.Value.Nullable ? $"{support}.Field(fields, {literal})" : $"Required(fields, {literal}, path)";
                code.Line($"var {held} = {Held(fields, index, type.Name, qualifier, support, sibling => "value" + Depth(sibling), missing)};");
                if (Descends(field.Value)) code.Line($"var field{Depth(index)} = {Read(field.Value, type.Name, held, $"Path(path, {literal})", qualifier, support)};");
                else code.Line($"if (!{Decode(field.Value, type.Name, held, "out var field" + Depth(index), qualifier, support)}) throw Failure({held}, Path(path, {literal}));");
            }

            var kept = GesModelPlanner.Kept(fields);
            if (kept.Count == 0) code.Line($"return new {record}({string.Join(", ", fields.ConvertAll(field => "field" + Depth(fields.IndexOf(field))))});");
            else
            {
                // An optional field read as absent stays absent, though the constructor would take its sibling for null.
                code.Line($"var record = new {record}({string.Join(", ", fields.ConvertAll(field => "field" + Depth(fields.IndexOf(field))))});");
                foreach (var index in kept) code.Line($"record.{fields[index].Base} = field{Depth(index)};");
                code.Line("return record;");
            }
            code.Close();
            code.Blank();
            code.Line($"private static bool Decode{type.Name}(in GesValue value, out {record} result)");
            code.Open("{");
            code.Line("result = null!;");
            code.Line($"var fields = RecordFields(value, {GesSchemaPlanner.Literal(type.Name, false)}, {names});");
            var conditions = new List<string> { "fields is null" };
            for (var index = 0; index < fields.Count; index++)
                conditions.Add("!" + Decode(fields[index].Value, type.Name, Inline(fields, index, type.Name, qualifier, support), "out var field" + Depth(index), qualifier, support));
            for (var index = 0; index < conditions.Count; index++)
                code.Line((index == 0 ? "if (" : "    || ") + conditions[index] + (index == conditions.Count - 1 ? ") return false;" : ""));
            code.Line($"result = new {record}({string.Join(", ", fields.ConvertAll(field => "field" + Depth(fields.IndexOf(field))))});");
            foreach (var index in GesModelPlanner.Kept(fields)) code.Line($"result.{fields[index].Base} = field{Depth(index)};");
            code.Line("return true;");
            code.Close();
            var conversions = new List<GesModelPlanValue>();
            foreach (var field in fields) Conversions(field.Value, conversions);
            foreach (var conversion in conversions)
                if (conversion.TagSet.Length == 0 || emitted.Add(conversion.TagSet)) Conversion(code, type.Name, qualifier, support, conversion, plain: true);
        }

        code.Blank();
        code.Line("// The fields of a Record of the schema, or of a Map whose keys are all fields of it.");
        code.Line("private static GesValueMap? RecordFields(in GesValue value, string name, string[] names)");
        code.Open("{");
        code.Line("var fields = value.AsMap();");
        code.Line("if (fields is null) return null;");
        code.Line("if (value.CustomTypeName is { } type) return type == name ? fields : null;");
        code.Line($"return {support}.Known(fields, names) ? fields : null;");
        code.Close();
        code.Blank();
        code.Line("private static string Path(string path, string field) => path.Length == 0 ? field : path + \".\" + field;");
        code.Blank();
        code.Line("// A value that does not convert: nothing where a value is required, or a value of another type.");
        code.Line("private static GesWireException Failure(in GesValue value, string path) => new GesWireException(value.IsNothing ? GesWireException.NothingValue : GesWireException.TypeMismatch, path);");
        code.Blank();
        code.Line("private static GesValue Required(GesValueMap fields, string name, string path) => fields.Get(name) ?? throw new GesWireException(GesWireException.MissingField, Path(path, name));");
        code.Blank();
        code.Line("private delegate T Reader<T>(in GesValue value, string path);");
        code.Blank();
        code.Line("// The elements of a List, each read at the list's path with `[]`.");
        code.Line("private static List<T> ReadList<T>(in GesValue value, string path, Reader<T> element)");
        code.Open("{");
        code.Line("if (value.ValueKind != GesBytecodeTypeKind.List) throw Failure(value, path);");
        code.Line("var items = value.AsList();");
        code.Line("var list = new List<T>(items.Length);");
        code.Line("var item = path + \"[]\";");
        code.Line("for (var index = 0; index < items.Length; index++) list.Add(element(items[index], item));");
        code.Line("return list;");
        code.Close();
        code.Blank();
        code.Line("private static T? ReadOptional<T>(in GesValue value, string path, Reader<T> read) where T : class => value.IsNothing ? null : read(value, path);");
        code.Close();
    }

    // The catalog and constructor registry of the models, with an external value and a constructor per type.
    private static void Bindings(GesSchemaPlanner.Code code, GesModelLayout layout, string access, string qualifier, string support, GesComputedEmitter computed)
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
        var emitted = new HashSet<string>(System.StringComparer.Ordinal);
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
            var definitions = fields.ConvertAll(field => Definition(field.Field.Name, field.Value, "Field"));
            foreach (var field in type.Computed) definitions.Add(ComputedDefinition(field));
            code.Line($"    new[] {{ {string.Join(", ", definitions)}{(definitions.Count > 0 ? ", " : "")}new GesExternalTypeFieldDefinition(\"fields\", GesBytecodeTypeKind.Map) }},");
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
            foreach (var field in fields) code.Line($"{GesSchemaPlanner.Literal(field.Field.Name, false)} => {Encode(field.Value, type.Name, "Record." + field.Base, qualifier, support)},");
            foreach (var field in type.Computed)
                code.Line($"{GesSchemaPlanner.Literal(field.Name, false)} => {computed.HelperName}.ToGesValue({computed.ValueOf("Record", type.Name, field)}, {UnitName(field.Kind == GesSchemaTypeKind.Quantity ? field.Unit.ToTypeName() : "none")}),");
            code.Line($"\"fields\" => {support}.Fields(Names, this),");
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
            var conditions = new List<string> { "fields is null", $"!{support}.Known(fields, {type.Name}Value.Names)" };
            for (var index = 0; index < fields.Count; index++)
                conditions.Add("!" + Decode(fields[index].Value, type.Name, Inline(fields, index, type.Name, qualifier, support), "out var " + Parameter(fields[index].Base), qualifier, support));
            for (var index = 0; index < conditions.Count; index++)
                code.Line((index == 0 ? "if (" : "    || ") + conditions[index] + (index == conditions.Count - 1 ? ")" : ""));
            code.Open("{");
            code.Line("call.SetNothing();");
            code.Line("return;");
            code.Close();
            code.Blank();

            var created = $"new {name}({string.Join(", ", fields.ConvertAll(field => Parameter(field.Base)))})";
            var kept = GesModelPlanner.Kept(fields);
            if (kept.Count == 0) code.Line($"call.SetExternalValue(new {type.Name}Value({created}));");
            else
            {
                code.Line($"var record = {created};");
                foreach (var index in kept) code.Line($"record.{fields[index].Base} = {Parameter(fields[index].Base)};");
                code.Line($"call.SetExternalValue(new {type.Name}Value(record));");
            }
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
            foreach (var conversion in conversions)
                if (conversion.TagSet.Length == 0 || emitted.Add(conversion.TagSet)) Conversion(code, type.Name, qualifier, support, conversion);
        }

        code.Close();
    }

    private static void Conversion(GesSchemaPlanner.Code code, string owner, string qualifier, string support, GesModelPlanValue value, bool plain = false)
    {
        var name = Nested(value, qualifier, owner);
        var decoder = "Decode" + GesModelPlanner.ConversionName(owner, value);
        code.Blank();
        if (value.ClosedTag)
        {
            code.Line($"private static string Name({name} value) => value switch");
            code.Open("{");
            foreach (var tag in value.Model.Tags) code.Line($"{name}.{GesSchemaPlanner.Pascal(tag)} => {GesSchemaPlanner.Literal(tag, false)},");
            code.Line("_ => \"\"");
            code.Close("};");
            code.Blank();
            code.Line($"private static bool {decoder}(in GesValue value, out {name} result)");
            code.Open("{");
            code.Line("result = default;");
            code.Line($"if (!{support}.Tag(value, out var text)) return false;");
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
            code.Line($"private static GesValue Value({name} value) => {support}.Flags({names}, (ulong)value);");
            code.Blank();
            code.Line($"private static bool Decode{owner}{value.Nested}(in GesValue value, out {name} result)");
            code.Open("{");
            code.Line($"var valid = {support}.Flags(value, {names}, out var mask);");
            code.Line($"result = ({name})mask;");
            code.Line("return valid;");
            code.Close();
            return;
        }

        code.Line($"private static GesValue Value({name} value) => value switch");
        code.Open("{");
        foreach (var alternative in value.Alternatives)
            code.Line($"{name}.{alternative.Label} alternative => {Encode(alternative, owner, "alternative.Value", qualifier, support, plain: plain)},");
        code.Line("_ => GesValue.GesNothing()");
        code.Close("};");
        code.Blank();
        code.Line($"private static bool Decode{owner}{value.Nested}(in GesValue value, out {name} result)");
        code.Open("{");
        for (var index = 0; index < value.Alternatives.Count; index++)
        {
            var alternative = value.Alternatives[index];
            code.Line($"if ({Decode(alternative, owner, "value", "out var alternative" + Depth(index), qualifier, support)})");
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
        code.Line("internal delegate bool Decoder<T>(in GesValue value, out T result);");
        code.Blank();
        code.Line("internal static GesValue Field(GesValueMap fields, string name) => fields.Get(name) ?? GesValue.GesNothing();");
        code.Blank();
        code.Line("internal static bool Known(GesValueMap fields, string[] names)");
        code.Open("{");
        code.Line("for (var index = 0; index < fields.Length; index++)");
        code.Line("    if (Array.IndexOf(names, fields.KeyAt(index)) < 0) return false;");
        code.Line("return true;");
        code.Close();
        code.Blank();
        code.Line("internal static GesValue Fields(string[] names, IGesExternalValue value)");
        code.Open("{");
        code.Line("var values = new GesValue[names.Length];");
        code.Line("for (var index = 0; index < names.Length; index++) values[index] = value.GetField(names[index]) ?? GesValue.GesNothing();");
        code.Line("return GesValue.GesMap(names, values);");
        code.Close();
        code.Blank();
        code.Line("internal static GesValue Flags(string[] names, ulong mask)");
        code.Open("{");
        code.Line("var keys = new List<string>();");
        code.Line("for (var index = 0; index < names.Length; index++)");
        code.Line("    if ((mask >> index & 1) != 0) keys.Add(names[index]);");
        code.Line("var values = new GesValue[keys.Count];");
        code.Line("for (var index = 0; index < values.Length; index++) values[index] = GesValue.GesBoolean(true);");
        code.Line("return GesValue.GesMap(keys.ToArray(), values);");
        code.Close();
        code.Blank();
        code.Line("internal static bool Flags(in GesValue value, string[] names, out ulong result)");
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
        code.Line("internal static GesValue Tag(string name)");
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
            "internal static bool Tag(in GesValue value, out string result) => Read(value.ValueKind is GesBytecodeTypeKind.Tag or GesBytecodeTypeKind.Text, value.AsText(), out result);",
            "internal static bool Text(in GesValue value, out string result) => Read(value.ValueKind == GesBytecodeTypeKind.Text, value.AsText(), out result);",
            "internal static bool Bytes(in GesValue value, out byte[] result) => Read(value.ValueKind == GesBytecodeTypeKind.Bytes, value.AsBytes().ToArray(), out result);",
            "internal static bool Boolean(in GesValue value, out bool result) => Read(value.ValueKind == GesBytecodeTypeKind.Boolean, value.AsBoolean(), out result);",
            "internal static bool Percentage(in GesValue value, out double result) => Read(value.ValueKind == GesBytecodeTypeKind.Percentage, value.AsNumber(), out result);",
            "internal static bool Number(in GesValue value, GesQuantityUnit unit, out double result) => Read(value.ValueKind is GesBytecodeTypeKind.Integer or GesBytecodeTypeKind.Float && value.ValueUnit == unit, value.AsNumber(), out result);",
            "internal static bool Point(in GesValue value, GesQuantityUnit unit, out GesPoint result) => Read(GesPoint.FromValue(value, unit), out result);",
            "internal static bool Vector(in GesValue value, GesQuantityUnit unit, out GesVector result) => Read(GesVector.FromValue(value, unit), out result);",
        })
        {
            code.Blank();
            code.Line(line);
        }

        code.Blank();
        code.Line("internal static bool Integer(in GesValue value, GesQuantityUnit unit, out long result)");
        code.Open("{");
        code.Line("result = 0;");
        code.Line("if (value.ValueUnit != unit) return false;");
        code.Line("if (value.ValueKind == GesBytecodeTypeKind.Integer) result = value.AsInteger();");
        code.Line("else if (value.ValueKind == GesBytecodeTypeKind.Float && value.AsNumber() is var number && number >= -9.2233720368547758E18 && number < 9.2233720368547758E18 && Math.Floor(number) == number) result = (long)number;");
        code.Line("else return false;");
        code.Line("return true;");
        code.Close();
        code.Blank();
        code.Line("internal static bool Read<T>(bool valid, T value, out T result)");
        code.Open("{");
        code.Line("result = valid ? value : default!;");
        code.Line("return valid;");
        code.Close();
        code.Blank();
        code.Line("internal static bool Read<T>(T? value, out T result) where T : struct");
        code.Open("{");
        code.Line("result = value.GetValueOrDefault();");
        code.Line("return value.HasValue;");
        code.Close();
        code.Blank();
        code.Line("internal static bool OptionalValue<T>(in GesValue value, Decoder<T> decode, out T? result) where T : struct");
        code.Open("{");
        code.Line("result = null;");
        code.Line("if (value.IsNothing) return true;");
        code.Line("if (!decode(value, out var decoded)) return false;");
        code.Line("result = decoded;");
        code.Line("return true;");
        code.Close();
        code.Blank();
        code.Line("internal static bool OptionalObject<T>(in GesValue value, Decoder<T> decode, out T? result) where T : class");
        code.Open("{");
        code.Line("result = null;");
        code.Line("if (value.IsNothing) return true;");
        code.Line("if (!decode(value, out var decoded)) return false;");
        code.Line("result = decoded;");
        code.Line("return true;");
        code.Close();
        code.Blank();
        code.Line("internal static bool List<T>(in GesValue value, Decoder<T> element, out List<T> result)");
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
        code.Line("internal static GesValue List<T>(List<T> list, Func<T, GesValue> element)");
        code.Open("{");
        code.Line("var items = new GesValue[list.Count];");
        code.Line("for (var index = 0; index < items.Length; index++) items[index] = element(list[index]);");
        code.Line("return GesValue.GesList(items);");
        code.Close();
        code.Blank();
        code.Line("internal static bool Dice(in GesValue value, out List<int> result)");
        code.Open("{");
        code.Line("result = null!;");
        code.Line("if (value.ValueKind != GesBytecodeTypeKind.Dice) return false;");
        code.Line("result = new List<int>(value.AsDice());");
        code.Line("return true;");
        code.Close();
        code.Blank();
        code.Line("internal static GesValue Dice(List<int> rolls)");
        code.Open("{");
        code.Line("foreach (var roll in rolls)");
        code.Line("    if (roll <= 0) return GesValue.GesNothing();");
        code.Line("return GesValue.GesDice(rolls.ToArray());");
        code.Close();
    }
}
