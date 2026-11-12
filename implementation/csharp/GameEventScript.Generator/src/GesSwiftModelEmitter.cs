// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Globalization;
using GameEventScript.Api;

namespace GameEventScript.Generator;

// Writes Swift models: a Hashable, Sendable and Codable struct per schema with stored properties in declared order and a
// memberwise initializer, an enum per closed Tag and per union, and the runtime's GesPoint and GesVector for spatial fields.
internal static class GesSwiftModelEmitter
{
    internal static GesCodeDocument Emit(GesModelLayout layout, GesSchemaGenerateOptions options)
    {
        GesSchemaPlanner.CheckTopLevel(GesModelPlanner.TopLevel(layout, swift: true), swift: true);
        var access = options.Access == GesSchemaAccess.Public ? "public " : "";
        var header = GesModelPlanner.Header(layout);
        var suffix = GesSchemaPlanner.Suffix(header, options);
        var values = "__GesModelValues" + suffix;
        var support = "__GesModels" + suffix;
        var prologue = new GesSchemaPlanner.Code();
        prologue.Line("// swift-format-ignore-file");
        prologue.Blank();
        prologue.Line("import GameEventScriptRuntime");
        prologue.Blank();
        GesModelPlanner.CheckProtocols(layout, swift: true);
        var document = new GesCodeDocument
        {
            Header = header,
            Prologue = prologue.ToString(),
            Space = GesSchemaPlanner.Namespace(options.Namespace, GesSwiftEmitter.Keywords),
            Swift = true,
            Access = access,
            Holds = "model"
        };
        var computed = GesModelPlanner.Computed(layout, GesComputedEmitter.Helper(options.Namespace, suffix), swift: true);
        var documents = options.Documents ? "__GesModelDocuments" + suffix : null;
        var documentType = options.Documents ? GesModelPlanner.DocumentType(layout, document.Space) : "";
        foreach (var type in layout.Types)
            document.Units.Add(new GesCodeUnit(type.Name, code => Record(code, layout, type, access, options.Bindings, options.Values ? values : null, computed, layout.Roots.Contains(type.Name) ? documents : null)));
        foreach (var protocol in layout.Protocols) document.Units.Add(new GesCodeUnit(protocol.Name, code => Protocol(code, layout, protocol, access)));
        foreach (var (name, tags) in GesModelPlanner.TagSets(layout))
            document.Units.Add(new GesCodeUnit(name, code => TagEnum(code, name, tags, access, $"/// The tag set `:{name}`, in declared order; the raw value is the tag's name.")));
        if (options.Bindings) document.Units.Add(new GesCodeUnit("", code => Bindings(code, layout, access, support, computed)));
        if (options.Values) document.Units.Add(new GesCodeUnit("", code => Values(code, layout, values, support, computed)));
        if (documents is not null)
        {
            document.Units.Add(new GesCodeUnit("", code => DocumentType(code, layout, documentType, documents, access)));
            document.Units.Add(new GesCodeUnit("", code => DocumentsHolder(code, layout, documents)));
        }

        if (options.Bindings || options.Values) document.Units.Add(new GesCodeUnit("", code => Support(code, support)));
        if (layout.Types.Exists(type => type.Computed.Length > 0))
            document.Units.Add(new GesCodeUnit("", code => GesComputedLibrary.Write(code, computed.HelperName, swift: true, options.Bindings || options.Values || computed.NeedsValues(layout.Types.ConvertAll(type => (type.Name, type.Computed))))));
        return document;
    }

    private static string Name(string name) => GesSwiftEmitter.Keywords.Contains(name) ? "`" + name + "`" : name;

    // Reads a document of any root by its header, so that a host reading files of several kinds needs no switch on names.
    private static void DocumentType(GesSchemaPlanner.Code code, GesModelLayout layout, string name, string documents, string access)
    {
        code.Line("/// A GES document of one of the model roots of this file, chosen by its header `:Schema(:Name)`.");
        code.Open($"{access}enum {name}: Hashable, Sendable {{");
        foreach (var root in layout.Roots)
        {
            code.Line($"/// A document of `:{root}`.");
            code.Line($"case {Member(root)}({root})");
        }

        code.Blank();
        code.Line("/// Reads GES document text whose header names a model root of this file, as that root's `init(document:)` does.");
        code.Line("/// - Throws: `GesDocumentError` when the text is no document, names no root of this file (`document.schemaMismatch`), or");
        code.Line("///   breaks its schema, with the `document.*` or `wire.*` code, the line, the column and the field path.");
        code.Open($"{access}init(document text: String) throws(GesDocumentError) {{");
        code.Line($"let document = try {documents}.read(text, [{string.Join(", ", layout.Roots.ConvertAll(root => GesSchemaPlanner.Literal(root, true)))}])");
        code.Open("do {");
        code.Line("switch document.schema {");
        for (var index = 0; index < layout.Roots.Count; index++)
        {
            var root = layout.Roots[index];
            code.Line(index + 1 < layout.Roots.Count ? $"case {GesSchemaPlanner.Literal(root, true)}: self = .{Member(root)}(try {root}(value: document.value))" : $"default: self = .{Member(root)}(try {root}(value: document.value))");
        }

        code.Line("}");
        code.Close("} catch {");
        code.Line("    throw document.error(for: error)");
        code.Line("}");
        code.Close();
        code.Close();
    }

    // The schema sources of this file, parsed when a document is first read.
    private static void DocumentsHolder(GesSchemaPlanner.Code code, GesModelLayout layout, string holder)
    {
        code.Line("/// The schema sources of this file, which reading documents checks them against.");
        code.Open($"enum {holder} {{");
        code.Open("static let set = Result<GesSchemaSet, any Error> {");
        code.Open("try GesSchemaSet(sources: [");
        foreach (var source in layout.Sources)
        {
            code.Open($"GesSchemaSource(name: {GesSchemaPlanner.Literal(source.Name, true)}, text: [");
            foreach (var line in source.Text.Split('\n')) code.Line(GesSchemaPlanner.Literal(line, true) + ",");
            code.Close("].joined(separator: \"\\n\")),");
        }

        code.Close("])");
        code.Close();
        code.Blank();
        code.Open("static func read(_ text: String, _ roots: [String]) throws(GesDocumentError) -> GesSchemaDocument {");
        code.Open("guard let set = try? Self.set.get() else {");
        code.Line("throw GesDocumentError(code: GesSchemaDocument.schemaMismatch, line: 1, column: 1, message: \"The embedded schema sources no longer parse.\")");
        code.Close();
        code.Line("return try GesSchemaDocument(text, schemas: set, expected: roots)");
        code.Close();
        code.Close();
    }

    private static string Member(string pascal) => Name(GesSchemaPlanner.Camel(pascal));

    // The argument label of a declaration: Swift needs backticks there only for inout, var and let; a call needs none.
    private static string Label(string pascal)
    {
        var name = GesSchemaPlanner.Camel(pascal);
        return name is "inout" or "var" or "let" ? "`" + name + "`" : name;
    }

    // The read-only properties of the fields a schema lends with `with`.
    private static void Protocol(GesSchemaPlanner.Code code, GesModelLayout layout, GesModelProtocol protocol, string access)
    {
        var refines = protocol.Takes.ConvertAll(name => GesModelPlanner.ProtocolName(layout, name, swift: true));
        code.Line($"/// The fields of the schema `:{protocol.Name}`, which every model type that takes them unchanged with `with` has.");
        code.Open($"{access}protocol {GesModelPlanner.ProtocolName(layout, protocol.Name, swift: true)}{(refines.Count > 0 ? ": " + string.Join(", ", refines) : "")} {{");
        var first = true;
        foreach (var field in GesModelPlanner.Fields(new GesModelType { Name = protocol.Name, Fields = protocol.Fields }))
        {
            if (!first) code.Blank();
            first = false;
            code.Line($"/// `{field.Field.Name}: {GesModelPlanner.Describe(field.Field)}`.");
            code.Line($"var {Member(field.Base)}: {Type(field.Value)} {{ get }}");
        }

        code.Close();
    }

    private static void Record(GesSchemaPlanner.Code code, GesModelLayout layout, GesModelType type, string access, bool bindings, string? values, GesComputedEmitter computed, string? documents)
    {
        var fields = GesModelPlanner.Fields(type);
        var key = fields.Find(field => field.Field.Key);
        var names = new List<(string, string)>();
        foreach (var field in fields)
        {
            names.Add((GesSchemaPlanner.Camel(field.Base), "the field " + field.Field.Name));
            Nested(field.Value, names, field.Field.Name);
        }

        if (key is not null && GesSchemaPlanner.Camel(key.Base) != "id") names.Add(("id", "the identity of the key " + key.Field.Name));
        names.Add(("schemaHash", "the generated member schemaHash"));
        // Hashable's requirements: a stored hashValue would satisfy the deprecated requirement in place of the synthesized one.
        names.Add(("hashValue", "the Hashable member hashValue"));
        names.Add(("hash", "the Hashable member hash(into:)"));
        foreach (var constant in type.Constants) names.Add((constant.Name, "the let " + constant.Name));
        GesComputedEmitter.Names(type.Computed, type.Name, swift: true, names);
        if (bindings) names.Add(("gesValue", "the generated member gesValue"));
        if (values is not null) names.Add(("toValue", "the generated member toValue"));
        if (bindings && fields.Exists(field => field.Field.Name == "fields"))
            throw new GesGeneratorException(GesGeneratorException.NameCollision, $"In '{type.Name}', the field fields and the generated field fields of its bindings share a name.");
        GesSchemaPlanner.Unique(type.Name, names, GesModelPlanner.TopLevel(layout, swift: true));

        code.Line($"/// The model of the schema `:{type.Name}`: a plain value that is never encoded.");
        var adopted = string.Concat(GesModelPlanner.Adopted(layout, type, swift: true).ConvertAll(name => ", " + name));
        code.Open($"{access}struct {type.Name}: Hashable, Sendable, Codable{(key is null ? "" : ", Identifiable")}{adopted} {{");
        code.Line($"/// The hash of the schema `:{type.Name}`, which wire records of it carry too; a test that compares it notices stale generated code.");
        code.Line($"{access}static let schemaHash: UInt64 = 0x{GesSchemaLayoutJson.Hex(type.Hash)}");
        GesSwiftEmitter.Lets(code, type.Constants, access, "Int");
        if (fields.Count > 0) code.Blank();
        var first = true;
        foreach (var field in fields)
        {
            if (!first) code.Blank();
            first = false;
            code.Line($"/// `{field.Field.Name}: {GesModelPlanner.Describe(field.Field)}`.");
            code.Line($"{access}var {Member(field.Base)}: {Type(field.Value)}");
        }

        if (key is not null && GesSchemaPlanner.Camel(key.Base) != "id")
        {
            code.Blank();
            code.Line($"/// The key `{key.Field.Name}`, which identifies the record in a keyed list.");
            code.Line($"{access}var id: {Type(key.Value)} {{ {Member(key.Base)} }}");
        }

        computed.Members(code, type.Name, type.Computed, access.TrimEnd());

        code.Blank();
        var siblings = fields.Exists(field => GesModelPlanner.Sibling(fields, field) is not null);
        code.Line($"/// Creates a record from its fields; an absent value defaults to nil, a list to empty and a field with a default to it{(siblings ? ", where nil takes the field it names" : "")}.");
        var parameters = new List<string>();
        foreach (var field in fields)
            parameters.Add(GesModelPlanner.Sibling(fields, field) is null
                ? $"{Label(field.Base)}: {Type(field.Value)}{(field.Field.Default is { } fallback ? " = " + Native(field.Value, fallback) : Default(field.Value))}"
                : $"{Label(field.Base)}: {Plain(field.Value)}? = nil");
        code.Open($"{access}init({string.Join(", ", parameters)}) {{");
        foreach (var index in GesModelPlanner.ReadOrder(fields))
        {
            var field = fields[index];
            var sibling = GesModelPlanner.Sibling(fields, field);
            code.Line($"self.{Member(field.Base)} = {Member(field.Base)}{(sibling is { } named ? " ?? self." + Member(fields[named].Base) : "")}");
        }

        code.Close();
        if (bindings)
        {
            code.Blank();
            code.Line($"/// The record as the external value `:{type.Name}` of ``GesTypes``; scripts read its fields as this copy holds them.");
            code.Line($"{access}func gesValue() -> GesValue {{ .external(GesTypes.{type.Name}Value(self)) }}");
            code.Blank();
            code.Line($"/// Reads a `:{type.Name}` that a script holds, or fails for another value.");
            code.Open($"{access}init?(gesValue value: GesValue) {{");
            code.Line($"guard let held = value.externalValue as? GesTypes.{type.Name}Value else {{ return nil }}");
            code.Line("self = held.record");
            code.Close();
        }

        if (values is not null)
        {
            code.Blank();
            code.Line("/// The record as a plain GES Record of its fields, as `GesSchemaValues` decodes the bytes of the record.");
            code.Line($"{access}func toValue() -> GesValue {{ {values}.value(self) }}");
            code.Blank();
            code.Line("/// Reads a Record of this schema, or a Map of only its fields, as `GesSchemaValues` encodes it.");
            code.Line("/// - Throws: `GesWireError` with `wire.typeMismatch` at the path of a field that does not convert.");
            code.Line($"{access}init(value: GesValue) throws(GesWireError) {{ self = try {values}.read{type.Name}(value, \"\") }}");
        }

        if (documents is not null)
        {
            code.Blank();
            code.Line($"/// Reads a record from GES document text with the header `:Schema(:{type.Name})`: the document is checked against the schema");
            code.Line("/// sources of this file, and fields it leaves out take their defaults. Reading allocates and does no file I/O.");
            code.Line("/// - Throws: `GesDocumentError` when the text is no document, names another schema (`document.schemaMismatch`), or breaks the");
            code.Line("///   schema, with the `document.*` or `wire.*` code, the line, the column and the field path.");
            code.Open($"{access}init(document text: String) throws(GesDocumentError) {{");
            code.Line($"let document = try {documents}.read(text, [{GesSchemaPlanner.Literal(type.Name, true)}])");
            code.Open("do {");
            code.Line("self = try Self(value: document.value)");
            code.Close("} catch {");
            code.Line("    throw document.error(for: error)");
            code.Line("}");
            code.Close();
        }

        foreach (var field in fields) Declarations(code, field.Value, access);
        code.Close();
    }

    private static void Nested(GesModelPlanValue value, List<(string, string)> names, string field)
    {
        if (value.Nested.Length > 0 && value.TagSet.Length == 0) names.Add((value.Nested, GesSchemaPlanner.NestedTypeOrigin + "of the field " + field));
        if (value.Element is not null) Nested(value.Element, names, field);
        foreach (var alternative in value.Alternatives) Nested(alternative, names, field);
    }

    // A literal default as a native value.
    private static string Native(GesModelPlanValue value, GesLayoutDefault fallback) => fallback.Kind switch
    {
        GesSchemaDefaultKind.Boolean => fallback.Boolean ? "true" : "false",
        GesSchemaDefaultKind.Tag when value.ClosedTag => "." + Name(fallback.Value),
        GesSchemaDefaultKind.Tag => GesSchemaPlanner.Literal(fallback.Value, true),
        _ => GesModelPlanner.Number(value, fallback)
    };

    // The GES value that a field left out of a Record or Map takes: its literal default, the value read for the sibling it
    // names, or else `missing`.
    private static string Held(List<GesModelPlanField> fields, int index, string owner, string support, Func<int, string> sibling, string missing)
    {
        var field = fields[index];
        var key = $"fields.get({GesSchemaPlanner.Literal(field.Field.Name, true)})";
        return field.Field.Default switch
        {
            null => missing,
            { Kind: GesSchemaDefaultKind.Field } => $"{key} ?? {sibling(GesModelPlanner.Sibling(fields, field)!.Value)}",
            { Kind: GesSchemaDefaultKind.Tag } fallback => $"{key} ?? {support}.tag({GesSchemaPlanner.Literal(fallback.Value, true)})",
            { } fallback => $"{key} ?? {Encode(field.Value, owner, Native(field.Value, fallback), "", support, false, true)}"
        };
    }

    // A field's GES value inline, following defaults to siblings; `.nothing` when no value or default is left.
    private static string Inline(List<GesModelPlanField> fields, int index, string owner, string support)
        => Held(fields, index, owner, support, sibling => Inline(fields, sibling, owner, support), $"fields.get({GesSchemaPlanner.Literal(fields[index].Field.Name, true)}) ?? .nothing");

    private static string Default(GesModelPlanValue value) => value.Nullable ? " = nil" : value.Kind is GesModelKind.List or GesModelKind.Dice or GesModelKind.Bytes or GesModelKind.Flags ? " = []" : "";

    internal static string Type(GesModelPlanValue value) => Plain(value) + (value.Nullable ? "?" : "");

    private static string Plain(GesModelPlanValue value) => value.Kind switch
    {
        GesModelKind.Boolean => "Bool",
        GesModelKind.Number => value.Model.Integer ? "Int" : "Double",
        GesModelKind.Tag => value.ClosedTag ? value.Nested : "String",
        GesModelKind.Text => "String",
        GesModelKind.Bytes => "[UInt8]",
        GesModelKind.List => $"[{Type(value.Element!)}]",
        GesModelKind.Dice => "[Int]",
        GesModelKind.Point => "GesPoint",
        GesModelKind.Vector => "GesVector",
        GesModelKind.Box => "GesBox",
        GesModelKind.Poly => "GesPoly",
        GesModelKind.Schema => value.Model.Schema,
        _ => value.Nested
    };

    // A closed Tag's enum, nested in its record or, for a named tag set, declared once beside the records.
    private static void TagEnum(GesSchemaPlanner.Code code, string name, List<string> tags, string access, string summary)
    {
        code.Line(summary);
        code.Open($"{access}enum {name}: String, Hashable, Sendable, Codable, CaseIterable {{");
        foreach (var tag in tags)
        {
            code.Line($"/// The tag `#{tag}`.");
            code.Line($"case {Name(tag)}");
        }

        code.Close();
    }

    private static void Declarations(GesSchemaPlanner.Code code, GesModelPlanValue value, string access)
    {
        if (value.ClosedTag)
        {
            if (value.TagSet.Length > 0) return;
            code.Blank();
            TagEnum(code, value.Nested, value.Model.Tags, access, "/// The tags, in declared order; the raw value is the tag's name.");
        }
        else if (value.Kind == GesModelKind.Flags)
        {
            GesSwiftEmitter.FlagsDeclaration(code, value.Nested, value.Model.Tags, access);
        }
        else if (value.Kind == GesModelKind.Union)
        {
            var cases = new List<(string, string)>();
            foreach (var alternative in value.Alternatives) cases.Add((GesSchemaPlanner.Camel(alternative.Label), "the alternative " + alternative.Label));
            GesSchemaPlanner.Unique(value.Nested, cases);
            code.Blank();
            code.Line("/// The alternatives, in declared order.");
            code.Open($"{access}enum {value.Nested}: Hashable, Sendable, Codable {{");
            foreach (var alternative in value.Alternatives)
            {
                code.Line($"/// The alternative `:{alternative.Label}`.");
                code.Line($"case {Member(alternative.Label)}({Type(alternative)})");
            }

            code.Close();
            foreach (var alternative in value.Alternatives) Declarations(code, alternative, access);
        }
        else if (value.Element is not null) Declarations(code, value.Element, access);
    }

    private static string Unit(string unit) => "." + unit;

    // Converts a native value to a GES value; a plain value holds nested records as Records rather than as external values.
    // `helper` declares the conversions of the file's unions and Flags, `support` the conversions that both share.
    private static string Encode(GesModelPlanValue value, string owner, string expression, string helper, string support, bool nullable = true, bool plain = false)
    {
        if (nullable && value.Nullable) return $"{support}.present({expression}, {{ {Encode(value, owner, "$0", helper, support, false, plain)} }})";
        return value.Kind switch
        {
            GesModelKind.Boolean => $".boolean({expression})",
            GesModelKind.Number when value.Model.Type == "Percentage" => $".percentage({expression})",
            GesModelKind.Number when value.Model.Integer => $".integer(Int64({expression}), unit: {Unit(value.Model.Unit)})",
            GesModelKind.Number => $".float({expression}, unit: {Unit(value.Model.Unit)})",
            GesModelKind.Text => $".text({expression})",
            GesModelKind.Bytes => $".bytes({expression})",
            GesModelKind.Tag when value.ClosedTag => $"{support}.tag({expression}.rawValue)",
            GesModelKind.Tag => $"{support}.tag({expression})",
            GesModelKind.List => $".list({expression}.map {{ {Encode(value.Element!, owner, "$0", helper, support, plain: plain)} }})",
            GesModelKind.Dice => $"{support}.dice({expression})",
            GesModelKind.Point or GesModelKind.Vector or GesModelKind.Box or GesModelKind.Poly => $"{expression}.value(unit: {Unit(value.Model.Unit)})",
            GesModelKind.Schema => plain ? $"{expression}.toValue()" : $"{expression}.gesValue()",
            _ => $"{helper}.value({expression})"
        };
    }

    // Converts a GES value to a native value, or nil when it does not convert.
    private static string Decode(GesModelPlanValue value, string owner, string expression, string helper, string support, bool nullable = true, bool plain = false)
    {
        if (nullable && value.Nullable) return $"{support}.optional({expression}, {{ {Decode(value, owner, "$0", helper, support, false, plain)} }})";
        return value.Kind switch
        {
            GesModelKind.Boolean => $"{support}.boolean({expression})",
            GesModelKind.Number when value.Model.Type == "Percentage" => $"{support}.percentage({expression})",
            GesModelKind.Number when value.Model.Integer => $"{support}.integer({expression}, {Unit(value.Model.Unit)})",
            GesModelKind.Number => $"{support}.number({expression}, {Unit(value.Model.Unit)})",
            GesModelKind.Text => $"{support}.text({expression})",
            GesModelKind.Bytes => $"{support}.bytes({expression})",
            GesModelKind.Tag when value.ClosedTag => $"{support}.tag({expression}).flatMap({(value.TagSet.Length > 0 ? value.TagSet : owner + "." + value.Nested)}.init(rawValue:))",
            GesModelKind.Tag => $"{support}.tag({expression})",
            GesModelKind.List => $"{support}.list({expression}, {{ {Decode(value.Element!, owner, "$0", helper, support, plain: plain)} }})",
            GesModelKind.Dice => $"{support}.dice({expression})",
            GesModelKind.Point => $"GesPoint({expression}, unit: {Unit(value.Model.Unit)})",
            GesModelKind.Vector => $"GesVector({expression}, unit: {Unit(value.Model.Unit)})",
            GesModelKind.Box => $"GesBox({expression}, unit: {Unit(value.Model.Unit)})",
            GesModelKind.Poly => $"GesPoly({expression}, unit: {Unit(value.Model.Unit)})",
            GesModelKind.Schema => plain ? $"{helper}.decode{value.Model.Schema}({expression})" : $"{value.Model.Schema}(gesValue: {expression})",
            _ => $"{helper}.decode{owner}{value.Nested}({expression})"
        };
    }

    // Whether reading a value descends into it, so that a failure inside reports its own path: a nested record or a list.
    private static bool Descends(GesModelPlanValue value) => value.Kind is GesModelKind.Schema or GesModelKind.List;

    // An expression that reads a value that descends, or throws wire.typeMismatch at the path of the part that does not convert:
    // a nested record's field, or a list element with `[]`, as the value path names them.
    private static string Read(GesModelPlanValue value, string owner, string expression, string path, string helper, string support, int depth = 0, bool nullable = true)
    {
        var x = "x" + depth.ToString(CultureInfo.InvariantCulture);
        var p = "p" + depth.ToString(CultureInfo.InvariantCulture);
        if (nullable && value.Nullable) return $"try readOptional({expression}, {path}) {{ ({x}: GesValue, {p}: String) throws(GesWireError) in {Read(value, owner, x, p, helper, support, depth + 1, false)} }}";
        if (value.Kind == GesModelKind.Schema) return $"try read{value.Model.Schema}({expression}, {path})";
        var element = Descends(value.Element!)
            ? Read(value.Element!, owner, x, p, helper, support, depth + 1)
            : $"try require({Decode(value.Element!, owner, x, helper, support, plain: true)}, {x}, {p})";
        return $"try readList({expression}, {path}) {{ ({x}: GesValue, {p}: String) throws(GesWireError) in {element} }}";
    }

    private static string Definition(string name, GesModelPlanValue value)
    {
        var (kind, unit, typeName) = GesModelPlanner.Declared(value);
        if (kind is null) return $"try! .init(name: {GesSchemaPlanner.Literal(name, true)}, typeName: {GesSchemaPlanner.Literal(typeName!, true)})";
        var kindName = GesSchemaPlanner.Camel(kind);
        return unit == "none" ? $"try! .init(name: {GesSchemaPlanner.Literal(name, true)}, kind: .{kindName})" : $"try! .init(name: {GesSchemaPlanner.Literal(name, true)}, kind: .{kindName}, unit: {Unit(unit)})";
    }

    // The unions of a type, including those in lists and alternatives, for their conversion functions.
    private static void Unions(GesModelPlanValue value, List<GesModelPlanValue> unions)
    {
        if (value.Kind is GesModelKind.Union or GesModelKind.Flags) unions.Add(value);
        if (value.Element is not null) Unions(value.Element, unions);
        foreach (var alternative in value.Alternatives) Unions(alternative, unions);
    }

    // The catalog and constructor registry of the models, with an external value and a constructor per type.
    private static void Bindings(GesSchemaPlanner.Code code, GesModelLayout layout, string access, string support, GesComputedEmitter computed)
    {
        var ordered = GesModelPlanner.DependencyOrder(layout);
        code.Blank();
        code.Line("/// The model types for scripts as external types: pass one instance to the compiler as its catalog and to the host as");
        code.Line("/// its external-type registry. Each type has its fields and one constructor that takes every field by name; a value that");
        code.Line("/// does not convert, such as another unit or kind, constructs `nothing`. Types and constructors that this set does not");
        code.Line("/// declare resolve through the next catalog and registry.");
        code.Open($"{access}final class GesTypes: GesExternalTypeCatalog, GesExternalTypeRegistry {{");
        code.Line("/// The declarations of the generated types, each after the types it holds, then those of the next catalog.");
        code.Line($"{access}let types: [GesExternalTypeDefinition]");
        code.Line("private let nextCatalog: (any GesExternalTypeCatalog)?");
        code.Line("private let nextRegistry: (any GesExternalTypeRegistry)?");
        code.Line($"nonisolated(unsafe) private static let own: [GesExternalTypeDefinition] = [{string.Join(", ", ordered.ConvertAll(type => type.Name + "Value.definition"))}]");
        code.Blank();
        code.Line("/// Creates the set in front of another catalog and registry.");
        code.Line("/// - Throws: `GesApiError.invalidArgument` when the next catalog already declares a type of this set.");
        code.Open($"{access}init(nextCatalog: (any GesExternalTypeCatalog)? = nil, nextRegistry: (any GesExternalTypeRegistry)? = nil) throws {{");
        code.Line("let others = nextCatalog?.types ?? []");
        code.Open("if let clash = others.first(where: { other in Self.own.contains { $0.name == other.name } }) {");
        code.Line("throw GesApiError.invalidArgument(\"The type :\\(clash.name) is already declared.\")");
        code.Close();
        code.Line("types = Self.own + others");
        code.Line("self.nextCatalog = nextCatalog");
        code.Line("self.nextRegistry = nextRegistry");
        code.Close();
        code.Blank();
        code.Line("/// Resolves a generated type, or asks the next catalog.");
        code.Open($"{access}func resolve(_ typeName: String) throws -> GesExternalTypeDefinition? {{");
        code.Line("if let own = Self.own.first(where: { $0.name == typeName }) { return own }");
        code.Line("return try nextCatalog?.resolve(typeName)");
        code.Close();
        code.Blank();
        code.Line("/// Resolves the constructor of a generated type, or asks the next registry.");
        code.Open($"{access}func resolve(_ reference: GesExternalTypeConstructorReference) throws -> (any GesExternalTypeConstructor)? {{");
        code.Line("switch reference.signatureID {");
        foreach (var type in ordered) code.Line($"case {type.Name}Value.definition.constructors[0].signatureID: return {type.Name}Constructor()");
        code.Line("default: return try nextRegistry?.resolve(reference)");
        code.Line("}");
        code.Close();
        foreach (var type in ordered)
        {
            var fields = GesModelPlanner.Fields(type);
            code.Blank();
            code.Line($"/// A `:{type.Name}` held by a script.");
            code.Open($"final class {type.Name}Value: GesExternalValue {{");
            code.Line("nonisolated(unsafe) static let definition = try! GesExternalTypeDefinition(");
            code.Line($"    name: {GesSchemaPlanner.Literal(type.Name, true)},");
            var definitions = fields.ConvertAll(field => Definition(field.Field.Name, field.Value));
            foreach (var field in type.Computed)
            {
                var name = GesSchemaPlanner.Literal(field.Name, true);
                definitions.Add(field.Kind switch
                {
                    GesSchemaTypeKind.Boolean => $"try! .init(name: {name}, kind: .boolean)",
                    GesSchemaTypeKind.Percentage => $"try! .init(name: {name}, kind: .percentage)",
                    GesSchemaTypeKind.Quantity => $"try! .init(name: {name}, kind: .float, unit: .{field.Unit.ToTypeName()})",
                    _ => $"try! .init(name: {name}, kind: .float)"
                });
            }

            code.Line($"    fields: [{string.Join(", ", definitions)}{(definitions.Count > 0 ? ", " : "")}try! .init(name: \"fields\", kind: .map)],");
            code.Line($"    constructors: [try! .init(typeName: {GesSchemaPlanner.Literal(type.Name, true)}, parameters: [try! .init(name: \"fields\", kind: .map)])]");
            code.Line(")");
            code.Line($"static let names = [{string.Join(", ", fields.ConvertAll(field => GesSchemaPlanner.Literal(field.Field.Name, true)))}]");
            code.Line($"let record: {type.Name}");
            code.Blank();
            code.Line($"init(_ record: {type.Name}) {{ self.record = record }}");
            code.Blank();
            code.Line("var definition: GesExternalTypeDefinition { Self.definition }");
            code.Blank();
            code.Open("func field(_ name: String) throws -> GesValue? {");
            code.Line("switch name {");
            foreach (var field in fields) code.Line($"case {GesSchemaPlanner.Literal(field.Field.Name, true)}: {Encode(field.Value, type.Name, "record." + Member(field.Base), "GesTypes", support)}");
            foreach (var field in type.Computed)
                code.Line($"case {GesSchemaPlanner.Literal(field.Name, true)}: {computed.HelperName}.gesValue({computed.ValueOf("record", type.Name, field)}, .{(field.Kind == GesSchemaTypeKind.Quantity ? field.Unit.ToTypeName() : "none")})");
            code.Line($"case \"fields\": .map(keys: Self.names, values: [{string.Join(", ", fields.ConvertAll(field => $"try field({GesSchemaPlanner.Literal(field.Field.Name, true)})!"))}])");
            code.Line("default: nil");
            code.Line("}");
            code.Close();
            code.Close();
            code.Blank();
            code.Line($"/// Constructs a `:{type.Name}` from a Map or Record of its fields; an absent optional field may be left out.");
            code.Open($"private struct {type.Name}Constructor: GesExternalTypeConstructor {{");
            code.Line($"var definition: GesExternalTypeConstructorDefinition {{ {type.Name}Value.definition.constructors[0] }}");
            code.Blank();
            code.Open("func invoke(_ call: GesExternalTypeConstructorCall) throws {");
            code.Open("guard");
            code.Line($"let fields = call.arguments[0].asMap, {support}.known(fields, {type.Name}Value.names){(fields.Count > 0 ? "," : "")}");
            for (var index = 0; index < fields.Count; index++)
                code.Line($"let {Member(fields[index].Base)} = {Decode(fields[index].Value, type.Name, Inline(fields, index, type.Name, support), "GesTypes", support)}{(index == fields.Count - 1 ? "" : ",")}");
            code.Dedent();
            {
                code.Open("else {");
                code.Line("call.setNothing()");
                code.Line("return");
                code.Close();
            }

            var created = $"{type.Name}({string.Join(", ", fields.ConvertAll(field => $"{GesSchemaPlanner.Camel(field.Base)}: {Member(field.Base)}"))})";
            var kept = GesModelPlanner.Kept(fields);
            if (kept.Count == 0) code.Line($"try call.setExternalValue({type.Name}Value({created}))");
            else
            {
                code.Line($"var record = {created}");
                foreach (var index in kept) code.Line($"record.{Member(fields[index].Base)} = {Member(fields[index].Base)}");
                code.Line($"try call.setExternalValue({type.Name}Value(record))");
            }
            code.Close();
            code.Close();
            Conversions(code, type.Name, fields, "GesTypes", support, false);
        }

        code.Close();
    }

    // Returns the record read from `field<n>`, setting afterwards the optional fields whose initializer would take a sibling.
    private static void Construct(GesSchemaPlanner.Code code, string type, List<string> arguments, List<GesModelPlanField> fields)
    {
        var kept = GesModelPlanner.Kept(fields);
        if (kept.Count == 0)
        {
            code.Line($"return {type}({string.Join(", ", arguments)})");
            return;
        }

        code.Line($"var record = {type}({string.Join(", ", arguments)})");
        foreach (var index in kept) code.Line($"record.{Member(fields[index].Base)} = field{index.ToString(CultureInfo.InvariantCulture)}");
        code.Line("return record");
    }

    // The conversions of the unions and Flags of a type, including those in lists and alternatives.
    private static void Conversions(GesSchemaPlanner.Code code, string owner, List<GesModelPlanField> fields, string helper, string support, bool plain)
    {
        var unions = new List<GesModelPlanValue>();
        foreach (var field in fields) Unions(field.Value, unions);
        foreach (var union in unions)
        {
            var name = owner + "." + union.Nested;
            code.Blank();
            if (union.Kind == GesModelKind.Flags)
            {
                var names = $"{owner}{union.Nested}Names";
                code.Line($"static let {names} = [{string.Join(", ", union.Model.Tags.ConvertAll(member => GesSchemaPlanner.Literal(member, true)))}]");
                code.Blank();
                code.Line($"static func value(_ value: {name}) -> GesValue {{ {support}.flags({names}, value.rawValue) }}");
                code.Blank();
                code.Line($"static func decode{owner}{union.Nested}(_ value: GesValue) -> {name}? {{ {support}.flags(value, {names}).map({name}.init(rawValue:)) }}");
                continue;
            }

            code.Line($"static func value(_ value: {name}) -> GesValue {{");
            code.Line("    switch value {");
            foreach (var alternative in union.Alternatives) code.Line($"    case .{Member(alternative.Label)}(let alternative): {Encode(alternative, owner, "alternative", helper, support, plain: plain)}");
            code.Line("    }");
            code.Line("}");
            code.Blank();
            code.Open($"static func decode{owner}{union.Nested}(_ value: GesValue) -> {name}? {{");
            foreach (var alternative in union.Alternatives) code.Line($"if let alternative = {Decode(alternative, owner, "value", helper, support, plain: plain)} {{ return .{Member(alternative.Label)}(alternative) }}");
            code.Line("return nil");
            code.Close();
        }
    }

    // The plain values of the models: a Record of each schema with its fields in declared order, as the value path decodes the
    // bytes of a record, so that wire records read them and give them back.
    private static void Values(GesSchemaPlanner.Code code, GesModelLayout layout, string name, string support, GesComputedEmitter computed)
    {
        code.Blank();
        code.Line("/// The plain GES values of the models of this file, as `GesSchemaValues` reads and writes them.");
        code.Open($"enum {name} {{");
        var first = true;
        foreach (var type in layout.Types)
        {
            var fields = GesModelPlanner.Fields(type);
            var names = type.Name + "Fields";
            var typeLiteral = GesSchemaPlanner.Literal(type.Name, true);
            var arguments = new List<string>();
            for (var index = 0; index < fields.Count; index++) arguments.Add($"{GesSchemaPlanner.Camel(fields[index].Base)}: field{index}");
            if (!first) code.Blank();
            first = false;
            code.Line($"static let {names} = [{string.Join(", ", fields.ConvertAll(field => GesSchemaPlanner.Literal(field.Field.Name, true)))}]");
            code.Blank();
            // A value also carries the computed fields, as decoding the record's bytes gives them; reading ignores them.
            var keys = names;
            if (type.Computed.Length > 0)
            {
                keys = type.Name + "Keys";
                code.Line($"static let {keys} = {names} + [{string.Join(", ", Array.ConvertAll(type.Computed, field => GesSchemaPlanner.Literal(field.Name, true)))}]");
                code.Blank();
            }

            code.Open($"static func value(_ record: {type.Name}) -> GesValue {{");
            code.Line($".record(typeName: {typeLiteral}, keys: {keys}, values: [");
            foreach (var field in fields) code.Line("    " + Encode(field.Value, type.Name, "record." + Member(field.Base), name, support, plain: true) + ",");
            foreach (var field in type.Computed)
                code.Line($"    {computed.HelperName}.gesValue({computed.ValueOf("record", type.Name, field)}, .{(field.Kind == GesSchemaTypeKind.Quantity ? field.Unit.ToTypeName() : "none")}),");
            code.Line("])");
            code.Close();
            code.Blank();
            code.Open($"static func read{type.Name}(_ value: GesValue, _ path: String) throws(GesWireError) -> {type.Name} {{");
            if (fields.Count == 0)
            {
                code.Line($"guard recordFields(value, {typeLiteral}, {names}) != nil else {{ throw failure(value, path) }}");
                code.Line($"return {type.Name}()");
            }
            else
            {
                code.Line($"guard let fields = recordFields(value, {typeLiteral}, {names}) else {{ throw failure(value, path) }}");
                foreach (var index in GesModelPlanner.ReadOrder(fields))
                {
                    var fieldLiteral = GesSchemaPlanner.Literal(fields[index].Field.Name, true);
                    var held = "value" + index.ToString(CultureInfo.InvariantCulture);
                    var missing = fields[index].Value.Nullable ? $"fields.get({fieldLiteral}) ?? .nothing" : $"try required(fields, {fieldLiteral}, path)";
                    code.Line($"let {held} = {Held(fields, index, type.Name, support, sibling => "value" + sibling.ToString(CultureInfo.InvariantCulture), missing)}");
                    if (Descends(fields[index].Value)) code.Line($"let field{index} = {Read(fields[index].Value, type.Name, held, $"fieldPath(path, {fieldLiteral})", name, support)}");
                    else code.Line($"guard let field{index} = {Decode(fields[index].Value, type.Name, held, name, support, plain: true)} else {{ throw failure({held}, fieldPath(path, {fieldLiteral})) }}");
                }

                Construct(code, type.Name, arguments, fields);
            }

            code.Close();
            code.Blank();
            code.Open($"static func decode{type.Name}(_ value: GesValue) -> {type.Name}? {{");
            if (fields.Count == 0) code.Line($"guard recordFields(value, {typeLiteral}, {names}) != nil else {{ return nil }}");
            else
            {
                code.Open("guard");
                code.Line($"let fields = recordFields(value, {typeLiteral}, {names}),");
                for (var index = 0; index < fields.Count; index++)
                    code.Line($"let field{index} = {Decode(fields[index].Value, type.Name, Inline(fields, index, type.Name, support), name, support, plain: true)}{(index == fields.Count - 1 ? "" : ",")}");
                code.Dedent();
                code.Line("else { return nil }");
            }

            Construct(code, type.Name, arguments, fields);
            code.Close();
            Conversions(code, type.Name, fields, name, support, true);
        }

        code.Blank();
        code.Line("// The fields of a Record of the schema, or of a Map whose keys are all fields of it.");
        code.Open("static func recordFields(_ value: GesValue, _ name: String, _ names: [String]) -> GesValueMap? {");
        code.Line("guard let fields = value.asMap else { return nil }");
        code.Line("if let type = value.customTypeName { return type == name ? fields : nil }");
        code.Line($"return {support}.known(fields, names) ? fields : nil");
        code.Close();
        code.Blank();
        code.Line("static func fieldPath(_ path: String, _ field: String) -> String { path.isEmpty ? field : path + \".\" + field }");
        code.Blank();
        code.Line("// A value that does not convert: nothing where a value is required, or a value of another type.");
        code.Open("static func failure(_ value: GesValue, _ path: String) -> GesWireError {");
        code.Line("GesWireError(code: value.isNothing ? GesWireError.nothingValue : GesWireError.typeMismatch, path: path)");
        code.Close();
        code.Blank();
        code.Open("static func required(_ fields: GesValueMap, _ name: String, _ path: String) throws(GesWireError) -> GesValue {");
        code.Line("guard let value = fields.get(name) else { throw GesWireError(code: GesWireError.missingField, path: fieldPath(path, name)) }");
        code.Line("return value");
        code.Close();
        code.Blank();
        code.Open("static func require<T>(_ converted: T?, _ value: GesValue, _ path: String) throws(GesWireError) -> T {");
        code.Line("guard let converted else { throw failure(value, path) }");
        code.Line("return converted");
        code.Close();
        code.Blank();
        code.Line("// The elements of a List, each read at the list's path with `[]`.");
        code.Open("static func readList<T>(_ value: GesValue, _ path: String, _ element: (GesValue, String) throws(GesWireError) -> T) throws(GesWireError) -> [T] {");
        code.Line("guard let items = value.listValue else { throw failure(value, path) }");
        code.Line("let item = path + \"[]\"");
        code.Line("var result: [T] = []");
        code.Line("result.reserveCapacity(items.count)");
        code.Line("for value in items { result.append(try element(value, item)) }");
        code.Line("return result");
        code.Close();
        code.Blank();
        code.Open("static func readOptional<T>(_ value: GesValue, _ path: String, _ read: (GesValue, String) throws(GesWireError) -> T) throws(GesWireError) -> T? {");
        code.Line("value.isNothing ? nil : try read(value, path)");
        code.Close();
        code.Close();
    }

    // The conversions that the script bindings and the plain values share, named after the header so that two generated
    // files can share a module.
    private static void Support(GesSchemaPlanner.Code code, string name)
    {
        code.Blank();
        code.Line("/// The GES value conversions of the models of this file.");
        code.Open($"enum {name} {{");
        Helpers(code);
        code.Close();
    }

    private static void Helpers(GesSchemaPlanner.Code code)
    {
        code.Line("static func known(_ fields: GesValueMap, _ names: [String]) -> Bool { (0..<fields.length).allSatisfy { names.contains(fields.key(at: $0)) } }");
        code.Blank();
        code.Line("static func present<T>(_ value: T?, _ encode: (T) -> GesValue) -> GesValue { value.map(encode) ?? .nothing }");
        code.Blank();
        code.Line("static func optional<T>(_ value: GesValue, _ decode: (GesValue) -> T?) -> T?? { value.isNothing ? .some(nil) : decode(value).map { .some($0) } }");
        code.Blank();
        code.Open("static func flags(_ names: [String], _ mask: UInt64) -> GesValue {");
        code.Line("let keys = names.indices.filter { mask >> UInt64($0) & 1 != 0 }.map { names[$0] }");
        code.Line("return .map(keys: keys, values: keys.map { _ in .boolean(true) })");
        code.Close();
        code.Blank();
        code.Open("static func flags(_ value: GesValue, _ names: [String]) -> UInt64? {");
        code.Line("guard value.valueKind == .map, let fields = value.asMap else { return nil }");
        code.Line("var mask: UInt64 = 0");
        code.Open("for index in 0..<fields.length {");
        code.Line("let set = fields.value(at: index)");
        code.Line("guard let member = names.firstIndex(of: fields.key(at: index)), set.valueKind == .boolean else { return nil }");
        code.Line("if set.asBoolean { mask |= 1 << UInt64(member) }");
        code.Close();
        code.Line("return mask");
        code.Close();
        code.Blank();
        code.Line("static func tag(_ name: String) -> GesValue { (try? GesValue.tag(name)) ?? .nothing }");
        code.Blank();
        code.Line("static func tag(_ value: GesValue) -> String? { value.valueKind == .tag || value.valueKind == .text ? value.textValue : nil }");
        code.Blank();
        code.Line("static func text(_ value: GesValue) -> String? { value.valueKind == .text ? value.textValue : nil }");
        code.Blank();
        code.Line("static func bytes(_ value: GesValue) -> [UInt8]? { value.bytesValue }");
        code.Blank();
        code.Line("static func boolean(_ value: GesValue) -> Bool? { value.valueKind == .boolean ? value.asBoolean : nil }");
        code.Blank();
        code.Line("static func percentage(_ value: GesValue) -> Double? { value.valueKind == .percentage ? value.floatValue : nil }");
        code.Blank();
        code.Open("static func number(_ value: GesValue, _ unit: GesQuantityUnit) -> Double? {");
        code.Line("guard value.unit == unit else { return nil }");
        code.Line("if let integer = value.integerValue { return Double(exactly: integer) }");
        code.Line("return value.valueKind == .float ? value.floatValue : nil");
        code.Close();
        code.Blank();
        code.Open("static func integer(_ value: GesValue, _ unit: GesQuantityUnit) -> Int? {");
        code.Line("guard value.unit == unit else { return nil }");
        code.Line("if let integer = value.integerValue { return Int(exactly: integer) }");
        code.Line("return value.valueKind == .float ? value.floatValue.flatMap { Int(exactly: $0) } : nil");
        code.Close();
        code.Blank();
        code.Open("static func list<T>(_ value: GesValue, _ element: (GesValue) -> T?) -> [T]? {");
        code.Line("guard let items = value.listValue else { return nil }");
        code.Line("var result: [T] = []");
        code.Line("result.reserveCapacity(items.count)");
        code.Open("for item in items {");
        code.Line("guard let decoded = element(item) else { return nil }");
        code.Line("result.append(decoded)");
        code.Close();
        code.Line("return result");
        code.Close();
        code.Blank();
        code.Line("static func dice(_ value: GesValue) -> [Int]? { value.diceRolls?.map { Int($0) } }");
        code.Blank();
        code.Line("static func dice(_ rolls: [Int]) -> GesValue { rolls.allSatisfy { $0 > 0 && $0 <= Int(Int32.max) } ? .dice(rolls.map { Int32($0) }) : .nothing }");
    }
}
