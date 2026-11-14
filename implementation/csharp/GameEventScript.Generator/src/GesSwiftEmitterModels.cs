// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using System.Globalization;

namespace GameEventScript.Generator;

// The direct conversion between a wire record and the model of the same schema, which `--models` names: toModel() builds the
// model, read(model:) fills the record and the model gets init(wire:), field by field without GES values. Both walk the wire
// plan and the model plan of the schema side by side; a packed record is the same type on both sides, so its fields and its
// Lists copy as they are.
internal static partial class GesSwiftEmitter
{
    // The model layout and the qualifier of its namespace, and a counter for the names of locals.
    private sealed class ModelConversion(GesModelLayout layout, string qualifier, string wireSpace)
    {
        internal string Qualifier { get; } = qualifier;

        // The qualifier of the wire namespace: the conversion extends a wire record at file scope, where a bare name of a
        // record or tag set means the model's when the models live in the global namespace.
        internal string WireSpace { get; } = wireSpace;

        internal GesModelType? Type(string name) => layout.Types.Find(type => type.Name == name);

        private int _local;

        internal string Local(string name) => name + (_local++).ToString(CultureInfo.InvariantCulture);

        internal void Reset() => _local = 0;
    }

    // The conversion between a wire record and its model at file scope: an extension of the wire record with toModel() and
    // read(model:), and one of the model with init(wire:). Inside the wire namespace a bare name would mean the wire type.
    private static void ModelConversionUnit(GesSchemaPlanner.Code code, GesPlanType type, GesModelType model, string access, ModelConversion conversion)
    {
        var wire = conversion.WireSpace + type.Name;
        code.Line("/// The conversion of the wire record to and from the model of the same schema.");
        code.Open($"extension {wire} {{");
        ModelMembers(code, type, model, access, conversion);
        code.Close();
        code.Blank();
        ModelInitializer(code, conversion.Qualifier + model.Name, wire, access);
    }

    // The members of a wire record that convert it to and from its model.
    private static void ModelMembers(GesSchemaPlanner.Code code, GesPlanType type, GesModelType model, string access, ModelConversion conversion)
    {
        var name = conversion.Qualifier + model.Name;
        var fields = GesModelPlanner.Fields(model);
        var check = $"precondition({name}.schemaHash == Self.schemaHash, \"The model type {model.Name} was generated from another state of the schema than this wire record.\")";
        code.Line("/// Creates the model of this record, field by field without GES values.");
        code.Line("/// - Precondition: The model type was generated from the same state of the schema.");
        code.Open($"{access}func toModel() -> {name} {{");
        code.Line(check);
        conversion.Reset();
        var arguments = new List<string>();
        foreach (var field in fields)
        {
            var wire = type.Fields.Find(candidate => candidate.Layout.Name == field.Field.Name)
                ?? throw new GesGeneratorException(GesGeneratorException.Usage, $"The field {field.Field.Name} of the model :{model.Name} has no counterpart in the wire record; generate the model from the same schema.");
            var local = conversion.Local("field");
            code.Line($"let {local}: {ModelType(field.Value, model.Name, conversion)}");
            ToModelField(code, wire, field, model.Name, "self", local, conversion);
            arguments.Add($"{GesSchemaPlanner.Camel(field.Base)}: {local}");
        }

        code.Line($"return {name}({string.Join(", ", arguments)})");
        code.Close();
        code.Blank();
        code.Line("/// Fills this record from a model, field by field without GES values; the values are checked when the record is written.");
        code.Line("/// - Precondition: The model type was generated from the same state of the schema.");
        code.Open($"{access}{(type.IsClass ? "" : "mutating ")}func read(model: {name}) {{");
        code.Line(check);
        conversion.Reset();
        foreach (var field in type.Fields)
        {
            var match = fields.Find(candidate => candidate.Field.Name == field.Layout.Name)
                ?? throw new GesGeneratorException(GesGeneratorException.Usage, $"The field {field.Layout.Name} of :{type.Name} has no counterpart in the model; generate the model from the same schema.");
            FromModelField(code, field, match, model.Name, "self", $"model.{Member(match.Base)}", conversion);
        }

        code.Close();
    }

    // The model's initializer from a wire record, which extends the model type at file scope.
    private static void ModelInitializer(GesSchemaPlanner.Code code, string model, string wire, string access)
    {
        code.Line("/// The conversion from the wire record of the same schema.");
        code.Open($"extension {model} {{");
        code.Line("/// Creates the model of a wire record, field by field without GES values.");
        code.Line("/// - Precondition: The model type was generated from the same state of the schema.");
        code.Line($"{access}init(wire: {wire}) {{ self = wire.toModel() }}");
        code.Close();
    }

    // The type of a model value, qualified so that it resolves from wire code.
    private static string ModelType(GesModelPlanValue value, string owner, ModelConversion conversion)
    {
        var plain = value.Kind switch
        {
            GesModelKind.Boolean => "Bool",
            GesModelKind.Number => value.Model.Integer ? "Int" : "Double",
            GesModelKind.Tag => value.ClosedTag ? ModelNested(value, owner, conversion) : "String",
            GesModelKind.Text => "String",
            GesModelKind.Bytes => "[UInt8]",
            GesModelKind.List => $"[{ModelType(value.Element!, owner, conversion)}]",
            GesModelKind.Dice => "[Int]",
            GesModelKind.Point => "GesPoint",
            GesModelKind.Vector => "GesVector",
            GesModelKind.Box => "GesBox",
            GesModelKind.Poly => "GesPoly",
            GesModelKind.Disc => "GesDisc",
            GesModelKind.Schema => conversion.Qualifier + value.Model.Schema,
            _ => ModelNested(value, owner, conversion)
        };
        return value.Nullable ? plain + "?" : plain;
    }

    private static string ModelNested(GesModelPlanValue value, string owner, ModelConversion conversion)
        => conversion.Qualifier + (value.TagSet.Length > 0 ? value.TagSet : owner + "." + value.Nested);

    private static string WireMember(string owner, GesPlanValue value) => $"{owner}.{Member(value.Base)}";

    private static void ToModelField(GesSchemaPlanner.Code code, GesPlanField field, GesModelPlanField model, string modelOwner, string owner, string target, ModelConversion conversion)
    {
        if (!field.Optional)
        {
            ToModel(code, field.Value, model.Value, modelOwner, owner, "", WireMember(owner, field.Value), target, conversion);
            return;
        }

        code.Open($"if {owner}.has{field.Base} {{");
        var plain = model.Value;
        ToModel(code, field.Value, plain, modelOwner, owner, "", WireMember(owner, field.Value), target, conversion);
        code.Close("} else {");
        code.Indent();
        code.Line($"{target} = nil");
        code.Close();
    }

    private static void ToModel(GesSchemaPlanner.Code code, GesPlanValue value, GesModelPlanValue model, string modelOwner, string owner, string wired, string source, string target, ModelConversion conversion)
    {
        switch (value.Encoding.Kind)
        {
            case GesLayoutKind.Union:
                ToModelUnion(code, value, model, modelOwner, owner, wired, target, conversion);
                return;
            case GesLayoutKind.List when model.Kind == GesModelKind.Poly:
                code.Line($"{target} = GesPoly({source})");
                return;
            case GesLayoutKind.List when model.Kind == GesModelKind.Dice:
                code.Line($"{target} = {source}.map {{ Int($0) }}");
                return;
            case GesLayoutKind.List:
                ToModelList(code, value, model, modelOwner, wired, source, target, conversion);
                return;
            case GesLayoutKind.Schema when value.Packed is null:
                code.Line($"{target} = {source}.toModel()");
                return;
            default:
                code.Line($"{target} = {Converted(value, model, modelOwner, source, toModel: true, wired, conversion)}");
                return;
        }
    }

    // A value that converts by assignment or a conversion: numbers, Booleans, tags, flags, texts, bytes, geometry and packed
    // records. A tag converts by its index, which is its declared position on both sides.
    private static string Converted(GesPlanValue value, GesModelPlanValue model, string modelOwner, string source, bool toModel, string wired, ModelConversion conversion)
    {
        var modelType = ModelType(model, modelOwner, conversion).TrimEnd('?');
        var wireType = value.TagType.Length > 0 ? conversion.WireSpace + TypeName(value) : wired + TypeName(value);
        switch (value.Encoding.Kind)
        {
            case GesLayoutKind.Integer:
                return model.Model.Integer ? (toModel ? $"Int({source})" : $"Int64({source})") : (toModel ? $"Double({source})" : $"Int64({source})");
            case GesLayoutKind.Quantized or GesLayoutKind.Float:
                return model.Model.Integer ? (toModel ? $"Int({source})" : $"Double({source})") : source;
            case GesLayoutKind.Tag:
                return toModel
                    ? $"{modelType}.allCases[Int({source}.rawValue)]"
                    : $"{wireType}(rawValue: {wireType}.RawValue(truncatingIfNeeded: {modelType}.allCases.firstIndex(of: {source})!))!";
            case GesLayoutKind.Flags:
                return toModel ? $"{modelType}(rawValue: {source}.rawValue)" : $"{wireType}(rawValue: {source}.rawValue)";
            default:
                return source;
        }
    }

    private static void ToModelList(GesSchemaPlanner.Code code, GesPlanValue value, GesModelPlanValue model, string modelOwner, string wired, string source, string target, ModelConversion conversion)
    {
        var element = model.Element!;
        if (value.Item is null && Same(value.Element!, element))
        {
            // Elements of the same type, such as packed records, share the array's storage.
            code.Line($"{target} = {source}");
            return;
        }

        var type = ModelType(element, modelOwner, conversion);
        var list = conversion.Local("list");
        var item = conversion.Local("item");
        var converted = conversion.Local("element");
        code.Line($"var {list}: [{type}] = []");
        code.Line($"{list}.reserveCapacity({source}.count)");
        code.Open($"for {item} in {source} {{");
        code.Line($"let {converted}: {type}");
        if (value.Item is { } holder)
            ToModel(code, holder.Fields[0].Value, element, modelOwner, item, wired + holder.Name + ".", WireMember(item, holder.Fields[0].Value), converted, conversion);
        else ToModel(code, value.Element!, element, modelOwner, item, wired, item, converted, conversion);
        code.Line($"{list}.append({converted})");
        code.Close();
        code.Line($"{target} = {list}");
    }

    // Whether a wire element and a model element have the same type, so that a List of them is the same array.
    private static bool Same(GesPlanValue value, GesModelPlanValue model) => value.Encoding.Kind switch
    {
        GesLayoutKind.Boolean or GesLayoutKind.Point or GesLayoutKind.Vector or GesLayoutKind.Box or GesLayoutKind.Disc => true,
        GesLayoutKind.Quantized or GesLayoutKind.Float => model.Kind == GesModelKind.Number && !model.Model.Integer,
        GesLayoutKind.Schema => value.Packed is not null,
        _ => false
    };

    private static void ToModelUnion(GesSchemaPlanner.Code code, GesPlanValue value, GesModelPlanValue model, string modelOwner, string owner, string wired, string target, ModelConversion conversion)
    {
        code.Line($"switch {WireMember(owner, value)} {{");
        foreach (var alternative in value.Alternatives)
        {
            code.Line($"case .{Member(alternative.Label)}:");
            code.Indent();
            if (alternative.Encoding.Kind == GesLayoutKind.Nothing) code.Line($"{target} = nil");
            else if (model.Kind != GesModelKind.Union) ToModel(code, alternative, model, modelOwner, owner, wired, WireMember(owner, alternative), target, conversion);
            else
            {
                var present = model.Alternatives.Find(candidate => candidate.Label == alternative.Label)!;
                var local = conversion.Local("alternative");
                code.Line($"let {local}: {ModelType(present, modelOwner, conversion)}");
                ToModel(code, alternative, present, modelOwner, owner, wired, WireMember(owner, alternative), local, conversion);
                code.Line($"{target} = .{Member(alternative.Label)}({local})");
            }

            code.Dedent();
        }

        code.Line("}");
    }

    private static void FromModelField(GesSchemaPlanner.Code code, GesPlanField field, GesModelPlanField model, string modelOwner, string owner, string source, ModelConversion conversion)
    {
        if (!field.Optional)
        {
            FromModel(code, field.Value, model.Value, modelOwner, owner, "", source, GesCSharpEmitter.IsPooledList(field), conversion);
            return;
        }

        var local = conversion.Local("present");
        code.Open($"if let {local} = {source} {{");
        code.Line($"{owner}.has{field.Base} = true");
        FromModel(code, field.Value, model.Value, modelOwner, owner, "", local, pooled: false, conversion);
        code.Close("} else {");
        code.Indent();
        code.Line($"{owner}.has{field.Base} = false");
        code.Close();
    }

    private static void FromModel(GesSchemaPlanner.Code code, GesPlanValue value, GesModelPlanValue model, string modelOwner, string owner, string wired, string source, bool pooled, ModelConversion conversion)
    {
        var member = WireMember(owner, value);
        switch (value.Encoding.Kind)
        {
            case GesLayoutKind.Union:
                FromModelUnion(code, value, model, modelOwner, owner, wired, source, conversion);
                return;
            case GesLayoutKind.List when model.Kind == GesModelKind.Poly:
                code.Line($"{member} = {source}.points");
                return;
            case GesLayoutKind.List when model.Kind == GesModelKind.Dice:
                code.Line($"{member} = {source}.map {{ Int64($0) }}");
                return;
            case GesLayoutKind.List:
                FromModelList(code, value, model, modelOwner, owner, wired, source, pooled, conversion);
                return;
            case GesLayoutKind.Schema when value.Packed is null:
                code.Line($"{member}.read(model: {source})");
                return;
            default:
                code.Line($"{member} = {Converted(value, model, modelOwner, source, toModel: false, wired, conversion)}");
                return;
        }
    }

    private static void FromModelList(GesSchemaPlanner.Code code, GesPlanValue value, GesModelPlanValue model, string modelOwner, string owner, string wired, string source, bool pooled, ModelConversion conversion)
    {
        var member = WireMember(owner, value);
        var element = model.Element!;
        if (value.Item is null && Same(value.Element!, element))
        {
            code.Line($"{member} = {source}");
            return;
        }

        var index = conversion.Local("index");
        var target = conversion.Local("target");
        var wireType = value.Item is not null ? wired + value.Item.Name : conversion.WireSpace + ElementName(value);
        var from = $"{source}[{index}]";
        var holder = value.Item;
        var record = holder is null && value.Element!.Encoding.Kind == GesLayoutKind.Schema && value.Element.Packed is null;
        if (holder is null && !record)
        {
            var each = conversion.Local("each");
            code.Line($"{member} = {source}.map {{ {each} in {Converted(value.Element!, element, modelOwner, each, toModel: false, wired, conversion)} }}");
            return;
        }

        if (pooled)
        {
            code.Line($"{owner}.set{value.Base}Count({source}.count)");
            code.Open($"for {index} in 0..<{source}.count {{");
            code.Line($"{(value.Element!.IsClass ? "let" : "var")} {target} = {member}[{index}]");
        }
        else
        {
            var list = conversion.Local("list");
            code.Line($"var {list}: [{wireType}] = []");
            code.Line($"{list}.reserveCapacity({source}.count)");
            code.Open($"for {index} in 0..<{source}.count {{");
            code.Line($"{(value.Element!.IsClass ? "let" : "var")} {target} = {wireType}()");
            if (holder is not null) FromModel(code, holder.Fields[0].Value, element, modelOwner, target, wired + holder.Name + ".", from, GesCSharpEmitter.IsPooledList(holder.Fields[0]), conversion);
            else code.Line($"{target}.read(model: {from})");
            code.Line($"{list}.append({target})");
            code.Close();
            code.Line($"{member} = {list}");
            return;
        }

        if (holder is not null) FromModel(code, holder.Fields[0].Value, element, modelOwner, target, wired + holder.Name + ".", from, GesCSharpEmitter.IsPooledList(holder.Fields[0]), conversion);
        else code.Line($"{target}.read(model: {from})");
        if (!value.Element!.IsClass) code.Line($"{member}[{index}] = {target}");
        code.Close();
    }

    private static void FromModelUnion(GesSchemaPlanner.Code code, GesPlanValue value, GesModelPlanValue model, string modelOwner, string owner, string wired, string source, ModelConversion conversion)
    {
        var member = WireMember(owner, value);
        var nothing = value.Alternatives.Find(alternative => alternative.Encoding.Kind == GesLayoutKind.Nothing);
        if (model.Kind != GesModelKind.Union)
        {
            // One alternative besides :Nothing is a nullable model value.
            var present = value.Alternatives.Find(alternative => alternative.Encoding.Kind != GesLayoutKind.Nothing)!;
            var local = conversion.Local("present");
            code.Open($"if let {local} = {source} {{");
            code.Line($"{member} = .{Member(present.Label)}");
            FromModel(code, present, model, modelOwner, owner, wired, local, pooled: false, conversion);
            code.Close("} else {");
            code.Indent();
            code.Line($"{member} = .{Member(nothing!.Label)}");
            code.Close();
            return;
        }

        var optional = model.Nullable ? "?" : "";
        code.Line($"switch {source} {{");
        foreach (var alternative in value.Alternatives)
        {
            if (alternative.Encoding.Kind == GesLayoutKind.Nothing) continue;
            var present = model.Alternatives.Find(candidate => candidate.Label == alternative.Label)!;
            var local = conversion.Local("alternative");
            code.Line($"case .{Member(alternative.Label)}(let {local}){optional}:");
            code.Indent();
            code.Line($"{member} = .{Member(alternative.Label)}");
            FromModel(code, alternative, present, modelOwner, owner, wired, local, pooled: false, conversion);
            code.Dedent();
        }

        if (model.Nullable)
        {
            code.Line("case nil:");
            code.Line($"    {member} = .{Member(nothing!.Label)}");
        }

        code.Line("}");
    }
}
