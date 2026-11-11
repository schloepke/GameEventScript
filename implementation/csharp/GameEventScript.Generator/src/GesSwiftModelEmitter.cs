// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;

namespace GameEventScript.Generator;

// Writes Swift models: a Hashable, Sendable and Codable struct per schema with stored properties in declared order and a
// memberwise initializer, an enum per closed Tag and per union, and the runtime's GesPoint and GesVector for spatial fields.
internal static class GesSwiftModelEmitter
{
    internal static string Emit(GesModelLayout layout, GesSchemaGenerateOptions options)
    {
        var access = options.Access == GesSchemaAccess.Public ? "public " : "";
        var space = GesSchemaPlanner.Namespace(options.Namespace, GesSwiftEmitter.Keywords);
        var values = "__GesModelValues" + GesSchemaPlanner.Hash(GesModelPlanner.Header(layout));
        var code = new GesSchemaPlanner.Code();
        code.Raw(GesModelPlanner.Header(layout));
        code.Line("// swift-format-ignore-file");
        code.Blank();
        code.Line("import GameEventScriptRuntime");
        code.Blank();
        foreach (var part in space)
        {
            code.Line($"/// The namespace `{part}` of generated model types.");
            code.Open($"{access}enum {part} {{");
        }

        for (var index = 0; index < layout.Types.Count; index++)
        {
            if (index > 0) code.Blank();
            Record(code, layout.Types[index], access, options.Bindings, options.Values ? values : null);
        }

        if (options.Bindings) Bindings(code, layout, access);
        if (options.Values) Values(code, layout, values);
        foreach (var _ in space) code.Close();
        return code.ToString();
    }

    private static string Name(string name) => GesSwiftEmitter.Keywords.Contains(name) ? "`" + name + "`" : name;

    private static string Member(string pascal) => Name(GesSchemaPlanner.Camel(pascal));

    private static void Record(GesSchemaPlanner.Code code, GesModelType type, string access, bool bindings, string? values)
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
        if (bindings) names.Add(("gesValue", "the generated member gesValue"));
        if (values is not null) names.Add(("toValue", "the generated member toValue"));
        if (bindings && fields.Exists(field => field.Field.Name == "fields"))
            throw new GesGeneratorException(GesGeneratorException.NameCollision, $"In '{type.Name}', the field fields and the generated field fields of its bindings share a name.");
        GesSchemaPlanner.Unique(type.Name, names);

        code.Line($"/// The model of the schema `:{type.Name}`: a plain value that is never encoded.");
        code.Open($"{access}struct {type.Name}: Hashable, Sendable, Codable{(key is null ? "" : ", Identifiable")} {{");
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

        code.Blank();
        code.Line("/// Creates a record from its fields; an absent value defaults to nil and a list to empty.");
        var parameters = new List<string>();
        foreach (var field in fields) parameters.Add($"{Member(field.Base)}: {Type(field.Value)}{Default(field.Value)}");
        code.Open($"{access}init({string.Join(", ", parameters)}) {{");
        foreach (var field in fields) code.Line($"self.{Member(field.Base)} = {Member(field.Base)}");
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

        foreach (var field in fields) Declarations(code, field.Value, access);
        code.Close();
    }

    private static void Nested(GesModelPlanValue value, List<(string, string)> names, string field)
    {
        if (value.Nested.Length > 0) names.Add((value.Nested, "a type of the field " + field));
        if (value.Element is not null) Nested(value.Element, names, field);
        foreach (var alternative in value.Alternatives) Nested(alternative, names, field);
    }

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
        GesModelKind.Schema => value.Model.Schema,
        _ => value.Nested
    };

    private static void Declarations(GesSchemaPlanner.Code code, GesModelPlanValue value, string access)
    {
        if (value.ClosedTag)
        {
            code.Blank();
            code.Line("/// The tags, in declared order; the raw value is the tag's name.");
            code.Open($"{access}enum {value.Nested}: String, Hashable, Sendable, Codable, CaseIterable {{");
            foreach (var tag in value.Model.Tags)
            {
                code.Line($"/// The tag `#{tag}`.");
                code.Line($"case {Name(tag)}");
            }

            code.Close();
        }
        else if (value.Kind == GesModelKind.Flags)
        {
            var members = new List<(string, string)>();
            foreach (var member in value.Model.Tags) members.Add((member, "the member #" + member));
            GesSchemaPlanner.Unique(value.Nested, members);
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
    private static string Encode(GesModelPlanValue value, string owner, string expression, bool nullable = true, string helper = "GesTypes", bool plain = false)
    {
        if (nullable && value.Nullable) return $"{helper}.present({expression}, {{ {Encode(value, owner, "$0", false, helper, plain)} }})";
        return value.Kind switch
        {
            GesModelKind.Boolean => $".boolean({expression})",
            GesModelKind.Number when value.Model.Type == "Percentage" => $".percentage({expression})",
            GesModelKind.Number when value.Model.Integer => $".integer(Int64({expression}), unit: {Unit(value.Model.Unit)})",
            GesModelKind.Number => $".float({expression}, unit: {Unit(value.Model.Unit)})",
            GesModelKind.Text => $".text({expression})",
            GesModelKind.Bytes => $".bytes({expression})",
            GesModelKind.Tag when value.ClosedTag => $"{helper}.tag({expression}.rawValue)",
            GesModelKind.Tag => $"{helper}.tag({expression})",
            GesModelKind.List => $".list({expression}.map {{ {Encode(value.Element!, owner, "$0", helper: helper, plain: plain)} }})",
            GesModelKind.Dice => $"{helper}.dice({expression})",
            GesModelKind.Point or GesModelKind.Vector => $"{expression}.value(unit: {Unit(value.Model.Unit)})",
            GesModelKind.Schema => plain ? $"{expression}.toValue()" : $"{expression}.gesValue()",
            _ => $"{helper}.value({expression})"
        };
    }

    // Converts a GES value to a native value, or nil when it does not convert.
    private static string Decode(GesModelPlanValue value, string owner, string expression, bool nullable = true, string helper = "GesTypes", bool plain = false)
    {
        if (nullable && value.Nullable) return $"{helper}.optional({expression}, {{ {Decode(value, owner, "$0", false, helper, plain)} }})";
        return value.Kind switch
        {
            GesModelKind.Boolean => $"{helper}.boolean({expression})",
            GesModelKind.Number when value.Model.Type == "Percentage" => $"{helper}.percentage({expression})",
            GesModelKind.Number when value.Model.Integer => $"{helper}.integer({expression}, {Unit(value.Model.Unit)})",
            GesModelKind.Number => $"{helper}.number({expression}, {Unit(value.Model.Unit)})",
            GesModelKind.Text => $"{helper}.text({expression})",
            GesModelKind.Bytes => $"{helper}.bytes({expression})",
            GesModelKind.Tag when value.ClosedTag => $"{helper}.tag({expression}).flatMap({owner}.{value.Nested}.init(rawValue:))",
            GesModelKind.Tag => $"{helper}.tag({expression})",
            GesModelKind.List => $"{helper}.list({expression}, {{ {Decode(value.Element!, owner, "$0", helper: helper, plain: plain)} }})",
            GesModelKind.Dice => $"{helper}.dice({expression})",
            GesModelKind.Point => $"GesPoint({expression}, unit: {Unit(value.Model.Unit)})",
            GesModelKind.Vector => $"GesVector({expression}, unit: {Unit(value.Model.Unit)})",
            GesModelKind.Schema => plain ? $"{helper}.decode{value.Model.Schema}({expression})" : $"{value.Model.Schema}(gesValue: {expression})",
            _ => $"{helper}.decode{owner}{value.Nested}({expression})"
        };
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
    private static void Bindings(GesSchemaPlanner.Code code, GesModelLayout layout, string access)
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
            code.Line($"    fields: [{string.Join(", ", fields.ConvertAll(field => Definition(field.Field.Name, field.Value)))}{(fields.Count > 0 ? ", " : "")}try! .init(name: \"fields\", kind: .map)],");
            code.Line($"    constructors: [try! .init(typeName: {GesSchemaPlanner.Literal(type.Name, true)}, parameters: [try! .init(name: \"fields\", kind: .map)])]");
            code.Line(")");
            code.Line($"let record: {type.Name}");
            code.Blank();
            code.Line($"init(_ record: {type.Name}) {{ self.record = record }}");
            code.Blank();
            code.Line("var definition: GesExternalTypeDefinition { Self.definition }");
            code.Blank();
            code.Open("func field(_ name: String) throws -> GesValue? {");
            code.Line("switch name {");
            foreach (var field in fields) code.Line($"case {GesSchemaPlanner.Literal(field.Field.Name, true)}: {Encode(field.Value, type.Name, "record." + Member(field.Base))}");
            code.Line($"case \"fields\": .map(keys: [{string.Join(", ", fields.ConvertAll(field => GesSchemaPlanner.Literal(field.Field.Name, true)))}], values: [{string.Join(", ", fields.ConvertAll(field => $"try field({GesSchemaPlanner.Literal(field.Field.Name, true)})!"))}])");
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
            code.Line($"let fields = call.arguments[0].asMap, GesTypes.known(fields, [{string.Join(", ", fields.ConvertAll(field => GesSchemaPlanner.Literal(field.Field.Name, true)))}]){(fields.Count > 0 ? "," : "")}");
            for (var index = 0; index < fields.Count; index++)
                code.Line($"let {Member(fields[index].Base)} = {Decode(fields[index].Value, type.Name, $"fields.get({GesSchemaPlanner.Literal(fields[index].Field.Name, true)}) ?? .nothing")}{(index == fields.Count - 1 ? "" : ",")}");
            code.Dedent();
            {
                code.Open("else {");
                code.Line("call.setNothing()");
                code.Line("return");
                code.Close();
            }

            code.Line($"try call.setExternalValue({type.Name}Value({type.Name}({string.Join(", ", fields.ConvertAll(field => $"{Member(field.Base)}: {Member(field.Base)}"))})))");
            code.Close();
            code.Close();
            Conversions(code, type.Name, fields, "GesTypes", false);
        }

        Helpers(code);
        code.Close();
    }

    // The conversions of the unions and Flags of a type, including those in lists and alternatives.
    private static void Conversions(GesSchemaPlanner.Code code, string owner, List<GesModelPlanField> fields, string helper, bool plain)
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
                code.Line($"static func value(_ value: {name}) -> GesValue {{ flags({names}, value.rawValue) }}");
                code.Blank();
                code.Line($"static func decode{owner}{union.Nested}(_ value: GesValue) -> {name}? {{ flags(value, {names}).map({name}.init(rawValue:)) }}");
                continue;
            }

            code.Line($"static func value(_ value: {name}) -> GesValue {{");
            code.Line("    switch value {");
            foreach (var alternative in union.Alternatives) code.Line($"    case .{Member(alternative.Label)}(let alternative): {Encode(alternative, owner, "alternative", helper: helper, plain: plain)}");
            code.Line("    }");
            code.Line("}");
            code.Blank();
            code.Open($"static func decode{owner}{union.Nested}(_ value: GesValue) -> {name}? {{");
            foreach (var alternative in union.Alternatives) code.Line($"if let alternative = {Decode(alternative, owner, "value", helper: helper, plain: plain)} {{ return .{Member(alternative.Label)}(alternative) }}");
            code.Line("return nil");
            code.Close();
        }
    }

    // The plain values of the models: a Record of each schema with its fields in declared order, as the value path decodes the
    // bytes of a record, so that wire records read them and give them back.
    private static void Values(GesSchemaPlanner.Code code, GesModelLayout layout, string name)
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
            if (!first) code.Blank();
            first = false;
            code.Line($"static let {names} = [{string.Join(", ", fields.ConvertAll(field => GesSchemaPlanner.Literal(field.Field.Name, true)))}]");
            code.Blank();
            code.Open($"static func value(_ record: {type.Name}) -> GesValue {{");
            code.Line($".record(typeName: {typeLiteral}, keys: {names}, values: [");
            foreach (var field in fields) code.Line("    " + Encode(field.Value, type.Name, "record." + Member(field.Base), helper: name, plain: true) + ",");
            code.Line("])");
            code.Close();
            code.Blank();
            code.Open($"static func read{type.Name}(_ value: GesValue, _ path: String) throws(GesWireError) -> {type.Name} {{");
            if (fields.Count == 0)
            {
                code.Line($"guard recordFields(value, {typeLiteral}, {names}) != nil else {{ throw mismatch(path) }}");
                code.Line($"return {type.Name}()");
            }
            else
            {
                code.Line($"guard let fields = recordFields(value, {typeLiteral}, {names}) else {{ throw mismatch(path) }}");
                for (var index = 0; index < fields.Count; index++)
                {
                    var fieldLiteral = GesSchemaPlanner.Literal(fields[index].Field.Name, true);
                    code.Line($"guard let field{index} = {Decode(fields[index].Value, type.Name, $"fields.get({fieldLiteral}) ?? .nothing", helper: name, plain: true)} else {{ throw mismatch(path, {fieldLiteral}) }}");
                }

                var arguments = new List<string>();
                for (var index = 0; index < fields.Count; index++) arguments.Add($"{Member(fields[index].Base)}: field{index}");
                code.Line($"return {type.Name}({string.Join(", ", arguments)})");
            }

            code.Close();
            code.Blank();
            code.Line($"static func decode{type.Name}(_ value: GesValue) -> {type.Name}? {{ try? read{type.Name}(value, \"\") }}");
            Conversions(code, type.Name, fields, name, true);
        }

        code.Blank();
        code.Line("// The fields of a Record of the schema, or of a Map whose keys are all fields of it.");
        code.Open("static func recordFields(_ value: GesValue, _ name: String, _ names: [String]) -> GesValueMap? {");
        code.Line("guard let fields = value.asMap else { return nil }");
        code.Line("if let type = value.customTypeName { return type == name ? fields : nil }");
        code.Line("return known(fields, Set(names)) ? fields : nil");
        code.Close();
        code.Blank();
        code.Open("static func mismatch(_ path: String, _ field: String? = nil) -> GesWireError {");
        code.Line("GesWireError(code: GesWireError.typeMismatch, path: field.map { path.isEmpty ? $0 : path + \".\" + $0 } ?? path)");
        code.Close();
        Helpers(code);
        code.Close();
    }

    private static void Helpers(GesSchemaPlanner.Code code)
    {
        code.Blank();
        code.Line("static func known(_ fields: GesValueMap, _ names: Set<String>) -> Bool { (0..<fields.length).allSatisfy { names.contains(fields.key(at: $0)) } }");
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
