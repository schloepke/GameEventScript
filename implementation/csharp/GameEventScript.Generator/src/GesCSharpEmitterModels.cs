// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;

namespace GameEventScript.Generator;

// The direct conversion between a wire record and the model of the same schema, which `--models` names: ToModel builds the
// model and CopyFrom(model) fills the record, field by field without GES values. Both walk the wire plan and the model plan of
// the schema side by side; a packed record is the same type on both sides, so its fields and its Lists copy as they are.
internal static partial class GesCSharpEmitter
{
    // The model layout and the qualifier of its namespace, and a counter for the names of locals.
    private sealed class ModelConversion(GesModelLayout layout, string qualifier)
    {
        internal string Qualifier { get; } = qualifier;

        internal GesModelType? Type(string name) => layout.Types.Find(type => type.Name == name);

        private int _local;

        internal string Local(string name) => name + (_local++).ToString(System.Globalization.CultureInfo.InvariantCulture);

        internal void Reset() => _local = 0;
    }

    private static void ModelMembers(GesSchemaPlanner.Code code, GesPlanType type, GesModelType model, ModelConversion conversion)
    {
        var name = conversion.Qualifier + model.Name;
        var fields = GesModelPlanner.Fields(model);
        code.Blank();
        code.Line("// Whether the model type was generated from the same schema; generated code of another schema state never converts.");
        code.Line($"private static readonly bool __modelMatches = {name}.SchemaHash == SchemaHash;");
        code.Blank();
        code.Line($"/// <summary>Creates the model of this record, field by field without GES values.</summary>");
        code.Line("/// <returns>The model.</returns>");
        code.Line("/// <exception cref=\"global::System.InvalidOperationException\">The model type was generated from another state of the schema.</exception>");
        code.Line($"public {(type.IsClass ? "" : "readonly ")}{name} ToModel()");
        code.Open("{");
        code.Line($"if (!__modelMatches) throw new global::System.InvalidOperationException(\"The model type {model.Name} was generated from another state of the schema than this wire record.\");");
        code.Line($"var model = new {name}();");
        conversion.Reset();
        foreach (var field in type.Fields)
        {
            var match = fields.Find(candidate => candidate.Field.Name == field.Layout.Name) ?? throw Unconvertible(type, field.Layout.Name);
            ToModelField(code, field, match, model.Name, "this", "", $"model.{match.Base}", conversion);
        }

        code.Line("return model;");
        code.Close();
        code.Blank();
        code.Line($"/// <summary>Fills this record from a model, field by field without GES values; the values are checked when the record is written.</summary>");
        code.Line("/// <param name=\"model\">The model.</param>");
        code.Line("/// <exception cref=\"global::System.InvalidOperationException\">The model type was generated from another state of the schema.</exception>");
        code.Line($"public void CopyFrom({name} model)");
        code.Open("{");
        code.Line("if (model is null) throw new global::System.ArgumentNullException(nameof(model));");
        code.Line($"if (!__modelMatches) throw new global::System.InvalidOperationException(\"The model type {model.Name} was generated from another state of the schema than this wire record.\");");
        conversion.Reset();
        foreach (var field in type.Fields)
        {
            var match = fields.Find(candidate => candidate.Field.Name == field.Layout.Name)!;
            FromModelField(code, field, match, model.Name, "this", "", $"model.{match.Base}", conversion);
        }

        code.Close();
    }

    // A statement header with the brace that opens its block on the next line.
    private static void Block(GesSchemaPlanner.Code code, string header)
    {
        code.Line(header);
        code.Open("{");
    }

    private static GesGeneratorException Unconvertible(GesPlanType type, string field)
        => new(GesGeneratorException.Usage, $"The field {field} of :{type.Name} has no counterpart in the model; generate the model from the same schema.");

    // Converts a field of `owner`, whose nested wire types `wired` qualifies, into the model member `target`.
    private static void ToModelField(GesSchemaPlanner.Code code, GesPlanField field, GesModelPlanField model, string modelOwner, string owner, string wired, string target, ModelConversion conversion)
    {
        if (!field.Optional)
        {
            ToModel(code, field.Value, model.Value, modelOwner, owner, wired, $"{owner}.{field.Value.Base}", target, conversion);
            return;
        }

        var local = conversion.Local("present");
        Block(code, $"if ({owner}.Has{field.Base})");
        code.Line($"{GesCSharpModelEmitter.Type(model.Value, conversion.Qualifier, modelOwner)} {local};");
        ToModel(code, field.Value, model.Value, modelOwner, owner, wired, $"{owner}.{field.Value.Base}", local, conversion);
        code.Line($"{target} = {local};");
        code.Close();
        code.Line($"else {target} = null;");
    }

    // Converts a wire value, read from `source` or, for a union, from the members of `owner`, into the model value `target`.
    private static void ToModel(GesSchemaPlanner.Code code, GesPlanValue value, GesModelPlanValue model, string modelOwner, string owner, string wired, string source, string target, ModelConversion conversion)
    {
        var encoding = value.Encoding;
        switch (encoding.Kind)
        {
            case GesLayoutKind.Union:
                ToModelUnion(code, value, model, modelOwner, owner, wired, target, conversion);
                return;
            case GesLayoutKind.List when model.Kind == GesModelKind.Poly:
                code.Line($"{target} = new GesPoly({source});");
                return;
            case GesLayoutKind.List when model.Kind == GesModelKind.Dice:
                var dice = conversion.Local("dice");
                var face = conversion.Local("face");
                code.Line($"var {dice} = new List<int>({source}.Count);");
                code.Line($"foreach (var {face} in {source}) {dice}.Add(checked((int){face}));");
                code.Line($"{target} = {dice};");
                return;
            case GesLayoutKind.List:
                ToModelList(code, value, model, modelOwner, wired, source, target, conversion);
                return;
            case GesLayoutKind.Schema when value.Packed is null:
                code.Line($"{target} = {source}.ToModel();");
                return;
            case GesLayoutKind.Bytes:
                code.Line($"{target} = {source}.ToArray();");
                return;
            default:
                code.Line($"{target} = {Converted(value, model, modelOwner, source, toModel: true, wired, conversion)};");
                return;
        }
    }

    // A value that converts by assignment or a cast: numbers, Booleans, tags, flags, texts, geometry and packed records.
    private static string Converted(GesPlanValue value, GesModelPlanValue model, string modelOwner, string source, bool toModel, string wired, ModelConversion conversion)
    {
        switch (value.Encoding.Kind)
        {
            case GesLayoutKind.Integer when model.Kind == GesModelKind.Number && !model.Model.Integer:
                return toModel ? $"(double){source}" : $"checked((long){source})";
            case GesLayoutKind.Quantized or GesLayoutKind.Float when model.Kind == GesModelKind.Number && model.Model.Integer:
                return toModel ? $"checked((long){source})" : $"(double){source}";
            case GesLayoutKind.Tag or GesLayoutKind.Flags:
                var type = toModel ? GesCSharpModelEmitter.Type(model, conversion.Qualifier, modelOwner).TrimEnd('?') : (value.TagType.Length > 0 ? TypeName(value) : wired + TypeName(value));
                return $"({type}){source}";
            default:
                return source;
        }
    }

    private static void ToModelList(GesSchemaPlanner.Code code, GesPlanValue value, GesModelPlanValue model, string modelOwner, string wired, string source, string target, ModelConversion conversion)
    {
        var element = model.Element!;
        var type = GesCSharpModelEmitter.Type(element, conversion.Qualifier, modelOwner);
        if (value.Item is null && Same(value.Element!, element))
        {
            // Elements of the same type, such as packed records, copy as a block.
            code.Line($"{target} = new List<{type}>({source});");
            return;
        }

        var list = conversion.Local("list");
        var item = conversion.Local("item");
        var converted = conversion.Local("element");
        code.Line($"var {list} = new List<{type}>({source}.Count);");
        Block(code, $"foreach (var {item} in {source})");
        code.Line($"{type} {converted};");
        if (value.Item is { } holder)
            ToModel(code, holder.Fields[0].Value, element, modelOwner, item, wired + holder.Name + ".", $"{item}.{holder.Fields[0].Value.Base}", converted, conversion);
        else ToModel(code, value.Element!, element, modelOwner, item, wired, item, converted, conversion);
        code.Line($"{list}.Add({converted});");
        code.Close();
        code.Line($"{target} = {list};");
    }

    // Whether a wire element and a model element have the same type, so that a List of them copies as a block.
    private static bool Same(GesPlanValue value, GesModelPlanValue model) => value.Encoding.Kind switch
    {
        GesLayoutKind.Boolean or GesLayoutKind.Point or GesLayoutKind.Vector or GesLayoutKind.Box or GesLayoutKind.Disc => true,
        GesLayoutKind.Integer => model.Kind == GesModelKind.Number && model.Model.Integer,
        GesLayoutKind.Quantized or GesLayoutKind.Float => model.Kind == GesModelKind.Number && !model.Model.Integer,
        GesLayoutKind.Schema => value.Packed is not null,
        _ => false
    };

    private static void ToModelUnion(GesSchemaPlanner.Code code, GesPlanValue value, GesModelPlanValue model, string modelOwner, string owner, string wired, string target, ModelConversion conversion)
    {
        var selector = $"{wired}{NestedName(value)}";
        Block(code, $"switch ({owner}.{value.Base})");
        foreach (var alternative in value.Alternatives)
        {
            code.Line($"case {selector}.{alternative.Label}:");
            code.Indent();
            if (alternative.Encoding.Kind == GesLayoutKind.Nothing) code.Line($"{target} = null;");
            else if (model.Kind != GesModelKind.Union) ToModel(code, alternative, model, modelOwner, owner, wired, $"{owner}.{alternative.Base}", target, conversion);
            else
            {
                var present = model.Alternatives.Find(candidate => candidate.Label == alternative.Label)!;
                var local = conversion.Local("alternative");
                code.Open("{");
                code.Line($"{GesCSharpModelEmitter.Type(present, conversion.Qualifier, modelOwner)} {local};");
                ToModel(code, alternative, present, modelOwner, owner, wired, $"{owner}.{alternative.Base}", local, conversion);
                code.Line($"{target} = new {GesCSharpModelEmitter.Type(model, conversion.Qualifier, modelOwner).TrimEnd('?')}.{alternative.Label}({local});");
                code.Close();
            }

            code.Line("break;");
            code.Dedent();
        }

        code.Line("default:");
        code.Line($"    throw new GesWireException(GesWireException.InvalidIndex, \"\");");
        code.Close();
    }

    // Fills a field of `owner` from the model member `source`.
    private static void FromModelField(GesSchemaPlanner.Code code, GesPlanField field, GesModelPlanField model, string modelOwner, string owner, string wired, string source, ModelConversion conversion)
    {
        if (!field.Optional)
        {
            FromModel(code, field.Value, model.Value, modelOwner, owner, wired, source, pooled: IsPooledList(field), conversion);
            return;
        }

        var local = conversion.Local("present");
        Block(code, $"if ({source} is {{ }} {local})");
        code.Line($"{owner}.Has{field.Base} = true;");
        FromModel(code, field.Value, model.Value, modelOwner, owner, wired, local, pooled: false, conversion);
        code.Close();
        code.Line($"else {owner}.Has{field.Base} = false;");
    }

    // Fills the wire member of `value` in `owner` from the model value `source`.
    private static void FromModel(GesSchemaPlanner.Code code, GesPlanValue value, GesModelPlanValue model, string modelOwner, string owner, string wired, string source, bool pooled, ModelConversion conversion)
    {
        var member = $"{owner}.{value.Base}";
        switch (value.Encoding.Kind)
        {
            case GesLayoutKind.Union:
                FromModelUnion(code, value, model, modelOwner, owner, wired, source, conversion);
                return;
            case GesLayoutKind.List when model.Kind == GesModelKind.Poly:
                code.Line($"{member}.Clear();");
                code.Line($"{member}.AddRange({source}.Points);");
                return;
            case GesLayoutKind.List when model.Kind == GesModelKind.Dice:
                var face = conversion.Local("face");
                code.Line($"{member}.Clear();");
                code.Line($"foreach (var {face} in {source}) {member}.Add({face});");
                return;
            case GesLayoutKind.List:
                FromModelList(code, value, model, modelOwner, owner, wired, source, pooled, conversion);
                return;
            case GesLayoutKind.Schema when value.Packed is null:
                code.Line($"{member}.CopyFrom({source});");
                return;
            case GesLayoutKind.Bytes:
                code.Line($"{owner}.Set{value.Base}({source});");
                return;
            default:
                code.Line($"{member} = {Converted(value, model, modelOwner, source, toModel: false, wired, conversion)};");
                return;
        }
    }

    private static void FromModelList(GesSchemaPlanner.Code code, GesPlanValue value, GesModelPlanValue model, string modelOwner, string owner, string wired, string source, bool pooled, ModelConversion conversion)
    {
        var member = $"{owner}.{value.Base}";
        var element = model.Element!;
        if (value.Item is null && Same(value.Element!, element))
        {
            code.Line($"{member}.Clear();");
            code.Line($"{member}.AddRange({source});");
            return;
        }

        var index = conversion.Local("index");
        var target = conversion.Local("target");
        var wireType = value.Item is not null ? wired + value.Item.Name : wired + ElementName(value);
        if (pooled)
        {
            code.Line($"{owner}.Set{value.Base}Count({source}.Count);");
            Block(code, $"for (var {index} = 0; {index} < {source}.Count; {index}++)");
            code.Line($"var {target} = {member}[{index}];");
        }
        else
        {
            code.Line($"{member}.Clear();");
            Block(code, $"for (var {index} = 0; {index} < {source}.Count; {index}++)");
            if (value.Item is not null || value.Element!.Encoding.Kind == GesLayoutKind.Schema) code.Line($"var {target} = new {wireType}();");
        }

        var from = $"{source}[{index}]";
        if (value.Item is { } holder)
        {
            var inner = holder.Fields[0];
            FromModel(code, inner.Value, element, modelOwner, target, wired + holder.Name + ".", from, pooled: IsPooledList(inner), conversion);
            if (!pooled) code.Line($"{member}.Add({target});");
        }
        else if (value.Element!.Encoding.Kind == GesLayoutKind.Schema && value.Element.Packed is null)
        {
            code.Line($"{target}.CopyFrom({from});");
            if (!pooled) code.Line($"{member}.Add({target});");
        }
        else code.Line($"{member}.Add({Converted(value.Element!, element, modelOwner, from, toModel: false, wired, conversion)});");

        code.Close();
    }

    private static void FromModelUnion(GesSchemaPlanner.Code code, GesPlanValue value, GesModelPlanValue model, string modelOwner, string owner, string wired, string source, ModelConversion conversion)
    {
        var selector = $"{wired}{NestedName(value)}";
        var nothing = value.Alternatives.Find(alternative => alternative.Encoding.Kind == GesLayoutKind.Nothing);
        var absent = nothing is null
            ? $"throw new global::System.ArgumentException(\"The model holds no value of {value.Base}.\", nameof(model));"
            : $"{owner}.{value.Base} = {selector}.{nothing.Label};";
        if (model.Kind != GesModelKind.Union)
        {
            // One alternative besides :Nothing is a nullable model value.
            var present = value.Alternatives.Find(alternative => alternative.Encoding.Kind != GesLayoutKind.Nothing)!;
            var local = conversion.Local("present");
            Block(code, $"if ({source} is {{ }} {local})");
            code.Line($"{owner}.{value.Base} = {selector}.{present.Label};");
            FromModel(code, present, model, modelOwner, owner, wired, local, pooled: false, conversion);
            code.Close();
            code.Line($"else {absent}");
            return;
        }

        var union = GesCSharpModelEmitter.Type(model, conversion.Qualifier, modelOwner).TrimEnd('?');
        Block(code, $"switch ({source})");
        foreach (var alternative in value.Alternatives)
        {
            if (alternative.Encoding.Kind == GesLayoutKind.Nothing) continue;
            var present = model.Alternatives.Find(candidate => candidate.Label == alternative.Label)!;
            var local = conversion.Local("alternative");
            code.Line($"case {union}.{alternative.Label} {local}:");
            code.Indent();
            code.Line($"{owner}.{value.Base} = {selector}.{alternative.Label};");
            FromModel(code, alternative, present, modelOwner, owner, wired, $"{local}.Value", pooled: false, conversion);
            code.Line("break;");
            code.Dedent();
        }

        code.Line("default:");
        code.Line("    " + absent);
        if (nothing is not null) code.Line("    break;");
        code.Close();
    }
}
