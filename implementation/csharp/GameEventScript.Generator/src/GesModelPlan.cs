// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using System.Text;
using GameEventScript.Api;

namespace GameEventScript.Generator;

// What both model emitters generate: per field its value in language-neutral terms with the names of the nested types it
// declares. A union with a :Nothing alternative becomes a nullable value without it, and a union that then holds one
// alternative becomes that alternative, so that absence reads as null or nil like an optional field.
internal sealed class GesModelPlanField
{
    internal GesModelField Field = new();
    internal string Base = "";
    internal GesModelPlanValue Value = new();
}

internal sealed class GesModelPlanValue
{
    internal GesModelKind Kind;
    internal GesModelValue Model = new();

    // The value may be absent: an optional field or a union with a :Nothing alternative.
    internal bool Nullable;

    // The nested type of a closed Tag (its enum), of Flags or of a union; empty for other kinds.
    internal string Nested = "";

    // The named tag set of a closed Tag, whose enum is declared once beside the records, or empty.
    internal string TagSet = "";

    // The label of a union alternative, its case or subclass.
    internal string Label = "";
    internal GesModelPlanValue? Element;
    internal List<GesModelPlanValue> Alternatives = [];

    internal bool ClosedTag => Kind == GesModelKind.Tag && Model.Tags.Count > 0;
}

internal static class GesModelPlanner
{
    // The named tag sets the types use, in order of first use, with their members.
    internal static List<(string Name, List<string> Tags)> TagSets(GesModelLayout layout)
    {
        var sets = new List<(string Name, List<string> Tags)>();
        var seen = new HashSet<string>(System.StringComparer.Ordinal);
        foreach (var type in layout.Types)
            foreach (var field in Fields(type)) TagSets(field.Value, sets, seen);
        return sets;
    }

    private static void TagSets(GesModelPlanValue value, List<(string Name, List<string> Tags)> sets, HashSet<string> seen)
    {
        if (value.TagSet.Length > 0 && seen.Add(value.TagSet)) sets.Add((value.TagSet, value.Model.Tags));
        if (value.Element is not null) TagSets(value.Element, sets, seen);
        foreach (var alternative in value.Alternatives) TagSets(alternative, sets, seen);
    }

    // The name of the conversion functions of a closed Tag, Flags or union: per owner, or once per named tag set.
    internal static string ConversionName(string owner, GesModelPlanValue value) => value.TagSet.Length > 0 ? value.TagSet : owner + value.Nested;

    // The translator of a model layout's computed fields: paths read the fields of model types, with optional chaining
    // through optional fields, and a computed field through its property or exact value.
    internal static GesComputedEmitter Computed(GesModelLayout layout, string helper, bool swift)
        => new(swift, helper, new ModelAccess(layout, swift));

    private sealed class ModelAccess(GesModelLayout layout, bool swift) : IGesComputedAccess
    {
        public GesComputedReach Reach(string root, string owner, string[] path)
        {
            var reach = new GesComputedReach { Text = root };
            var type = owner.Length > 0 ? layout.Type(owner) : null;
            GesModelPlanValue? value = null;
            foreach (var name in path)
            {
                var separator = reach.Text.Length == 0 ? "" : reach.Nullable ? "?." : ".";
                var field = Fields(type!).Find(candidate => candidate.Field.Name == name);
                if (field is null)
                {
                    reach.ComputedOwner = type!.Name;
                    reach.ComputedName = name;
                    if (reach.Nullable) reach.Text += "?";
                    return reach;
                }

                reach.Text += separator + Member(field.Base);
                reach.Nullable |= field.Value.Nullable;
                value = field.Value;
                type = value.Kind == GesModelKind.Schema ? layout.Type(value.Model.Schema) : null;
            }

            if (value is not null) Declare(value, reach);
            return reach;
        }

        public GesComputedReach Element(string owner, string[] path)
        {
            var reach = new GesComputedReach { Text = "item" };
            Declare(Value(owner, path).Element!, reach);
            return reach;
        }

        public string ElementOwner(string owner, string[] path)
        {
            var element = Value(owner, path).Element!;
            return element.Kind == GesModelKind.Schema ? element.Model.Schema : "";
        }

        public GesResolvedComputed[] Computed(string owner) => layout.Type(owner).Computed;

        // The declared range of a number, where its bounds are literals, and the largest count of a list.
        private static void Declare(GesModelPlanValue value, GesComputedReach reach)
        {
            var model = value.Model;
            reach.Integer = model.Integer;
            if (model.Integer)
            {
                reach.Lower = long.MinValue;
                reach.Upper = long.MaxValue;
            }

            if (Number(model.Lower) is { } lower) reach.Lower = lower;
            if (Number(model.Upper) is { } upper) reach.Upper = upper;
            if (value.Kind == GesModelKind.List && model.Max is { } max) reach.MaxCount = max;
        }

        // A bound that is a canonical decimal, not a parameter's name.
        private static double? Number(string? bound)
            => bound is { Length: > 0 } && (bound[0] == '-' || bound[0] is >= '0' and <= '9')
                && double.TryParse(bound, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var value) ? value : null;

        private GesModelPlanValue Value(string owner, string[] path)
        {
            var type = layout.Type(owner);
            GesModelPlanValue? value = null;
            foreach (var name in path)
            {
                value = Fields(type).Find(candidate => candidate.Field.Name == name)!.Value;
                if (value.Kind == GesModelKind.Schema) type = layout.Type(value.Model.Schema);
            }

            return value!;
        }

        private string Member(string pascal)
        {
            if (!swift) return pascal;
            var camel = GesSchemaPlanner.Camel(pascal);
            return GesSwiftEmitter.Keywords.Contains(camel) ? "`" + camel + "`" : camel;
        }
    }

    internal static List<GesModelPlanField> Fields(GesModelType type)
    {
        var fields = new List<GesModelPlanField>();
        foreach (var field in type.Fields)
        {
            var pascal = GesSchemaPlanner.Pascal(field.Name);
            var member = pascal == type.Name || field.Renamed ? pascal + "Value" : pascal;
            var value = Value(field.Value, member, "");
            value.Nullable |= field.Optional;
            fields.Add(new GesModelPlanField { Field = field, Base = member, Value = value });
        }

        return fields;
    }

    private static GesModelPlanValue Value(GesModelValue model, string name, string label)
    {
        if (model.Kind == GesModelKind.Union)
        {
            var present = model.Alternatives.FindAll(alternative => alternative.Value.Kind != GesModelKind.Nothing);
            var nullable = present.Count < model.Alternatives.Count;
            if (present.Count == 1)
            {
                var single = Value(present[0].Value, name, label);
                single.Nullable = true;
                return single;
            }

            var union = new GesModelPlanValue { Kind = GesModelKind.Union, Model = model, Nullable = nullable, Nested = name + "Alternative", Label = label };
            foreach (var alternative in present) union.Alternatives.Add(Value(alternative.Value, name + GesSchemaPlanner.Pascal(alternative.Name), GesSchemaPlanner.Pascal(alternative.Name)));
            return union;
        }

        var value = new GesModelPlanValue { Kind = model.Kind, Model = model, Label = label };
        if (value.ClosedTag)
        {
            value.TagSet = model.TagSet;
            value.Nested = model.TagSet.Length > 0 ? model.TagSet : name + "Tag";
        }

        if (model.Kind == GesModelKind.Flags) value.Nested = name + "Flags";
        if (model.Kind == GesModelKind.List) value.Element = Value(model.Element!.Value, name + "Item", "");
        return value;
    }

    // The types in an order where each comes after the types it holds, for registration: by name, depth first.
    // The protocol (Swift) or interface (C#) of a schema that types take with `with`: an abstract schema's own name in Swift,
    // since no type takes it, `<Name>Fields` beside a type of that name, and `I<Name>` in C#.
    internal static string ProtocolName(GesModelLayout layout, string name, bool swift)
        => !swift ? "I" + name : layout.Protocols.Find(protocol => protocol.Name == name)!.Abstract ? name : name + "Fields";

    // The protocols a type adopts: those of the schemas it takes unchanged and, for a plain schema that others take, its own.
    internal static List<string> Adopted(GesModelLayout layout, GesModelType type, bool swift)
    {
        var adopted = type.Takes.ConvertAll(name => ProtocolName(layout, name, swift));
        if (layout.Protocols.Exists(protocol => protocol.Name == type.Name)) adopted.Add(ProtocolName(layout, type.Name, swift));
        return adopted;
    }

    // The names declared beside the records, which a record's nested type must not hide.
    internal static HashSet<string> TopLevel(GesModelLayout layout, bool swift)
    {
        var names = new HashSet<string>(System.StringComparer.Ordinal);
        foreach (var type in layout.Types) names.Add(type.Name);
        foreach (var (name, _) in TagSets(layout)) names.Add(name);
        foreach (var protocol in layout.Protocols) names.Add(ProtocolName(layout, protocol.Name, swift));
        return names;
    }

    // Protocol names must not meet a type or tag set of the file.
    internal static void CheckProtocols(GesModelLayout layout, bool swift)
    {
        var names = new List<(string, string)>();
        foreach (var type in layout.Types) names.Add((type.Name, "the type " + type.Name));
        foreach (var (name, _) in TagSets(layout)) names.Add((name, "the tag set " + name));
        foreach (var protocol in layout.Protocols) names.Add((ProtocolName(layout, protocol.Name, swift), "the protocol of " + protocol.Name));
        GesSchemaPlanner.Unique("the generated file", names);
    }

    // The order in which a record's fields are read: declared order, except that a field defaulting to a sibling follows it.
    internal static List<int> ReadOrder(List<GesModelPlanField> fields)
    {
        var order = new List<int>();
        for (var index = 0; index < fields.Count; index++) Place(fields, index, order);
        return order;
    }

    private static void Place(List<GesModelPlanField> fields, int index, List<int> order)
    {
        if (order.Contains(index)) return;
        if (Sibling(fields, fields[index]) is { } sibling) Place(fields, sibling, order);
        order.Add(index);
    }

    // The optional fields that default to a sibling: an initializer takes nil or null for the sibling's value, so reading sets
    // them afterwards, and a value that leaves them out or gives nothing keeps what it says.
    internal static List<int> Kept(List<GesModelPlanField> fields)
    {
        var kept = new List<int>();
        for (var index = 0; index < fields.Count; index++)
            if (fields[index].Value.Nullable && Sibling(fields, fields[index]) is not null) kept.Add(index);
        return kept;
    }

    // The sibling a field defaults to, or null.
    internal static int? Sibling(List<GesModelPlanField> fields, GesModelPlanField field)
        => field.Field.Default is { Kind: GesSchemaDefaultKind.Field } fallback ? fields.FindIndex(candidate => candidate.Field.Name == fallback.Value) : null;

    // The number of a numeric default as a literal: whole digits for an integer field, otherwise a floating literal.
    internal static string Number(GesModelPlanValue value, GesLayoutDefault fallback)
    {
        var e = fallback.Value.IndexOf('e');
        var significand = long.Parse(fallback.Value.Substring(0, e), System.Globalization.CultureInfo.InvariantCulture);
        var exponent = int.Parse(fallback.Value.Substring(e + 1), System.Globalization.CultureInfo.InvariantCulture);
        if (!value.Model.Integer) return GesSchemaPlanner.Decimal(significand, exponent);
        for (var power = 0; power < exponent; power++) significand *= 10;
        return significand.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    internal static List<GesModelType> DependencyOrder(GesModelLayout layout)
    {
        var ordered = new List<GesModelType>();
        var seen = new HashSet<string>(System.StringComparer.Ordinal);
        foreach (var type in layout.Types) Visit(layout, type, seen, ordered);
        return ordered;
    }

    private static void Visit(GesModelLayout layout, GesModelType type, HashSet<string> seen, List<GesModelType> ordered)
    {
        if (!seen.Add(type.Name)) return;
        foreach (var field in type.Fields) References(layout, field.Value, seen, ordered);
        ordered.Add(type);
    }

    private static void References(GesModelLayout layout, GesModelValue value, HashSet<string> seen, List<GesModelType> ordered)
    {
        if (value.Kind == GesModelKind.Schema) Visit(layout, layout.Type(value.Schema), seen, ordered);
        if (value.Element is not null) References(layout, value.Element.Value, seen, ordered);
        foreach (var alternative in value.Alternatives) References(layout, alternative.Value, seen, ordered);
    }

    // The type that reads a document of any root of a generation: Document within a namespace, which qualifies it, and
    // ModelDocument without one, where Document would collide too easily. It must not take the name of a generated type.
    internal static string DocumentType(GesModelLayout layout, List<string> space)
    {
        var name = space.Count > 0 ? "Document" : "ModelDocument";
        if (layout.Types.Exists(type => type.Name == name) || layout.Protocols.Exists(protocol => protocol.Name == name) || TagSets(layout).Exists(set => set.Name == name))
            throw new GesGeneratorException(GesGeneratorException.NameCollision, $"The document type '{name}' takes the name of a generated type; choose another namespace.");
        return name;
    }

    // Whether a model holds a Box, a Poly or a Disc, whose conversions the support class then offers.
    internal static bool UsesGeometry(GesModelLayout layout) => layout.Types.Exists(type => type.Fields.Exists(field => UsesGeometry(field.Value)));

    private static bool UsesGeometry(GesModelValue value)
        => value.Kind is GesModelKind.Box or GesModelKind.Poly or GesModelKind.Disc || (value.Element is { } element && UsesGeometry(element.Value)) || value.Alternatives.Exists(alternative => UsesGeometry(alternative.Value));

    // The type a script sees for a field: a built-in kind with its unit, or a type name; a union keeps its value as `Value`.
    internal static (string? Kind, string Unit, string? TypeName) Declared(GesModelPlanValue value) => value.Kind switch
    {
        GesModelKind.Boolean => ("Boolean", "none", null),
        GesModelKind.Number when value.Model.Type == "Percentage" => ("Percentage", "none", null),
        GesModelKind.Number => ("Float", value.Model.Unit, null),
        GesModelKind.Text => ("Text", "none", null),
        GesModelKind.Bytes => ("Bytes", "none", null),
        GesModelKind.Tag => ("Tag", "none", null),
        GesModelKind.Flags => ("Map", "none", null),
        GesModelKind.List => ("List", "none", null),
        GesModelKind.Dice => ("Dice", "none", null),
        GesModelKind.Point => ("Point", value.Model.Unit, null),
        GesModelKind.Vector => ("Vector", value.Model.Unit, null),
        GesModelKind.Box => (null, "none", "Box"),
        GesModelKind.Poly => (null, "none", "Poly"),
        GesModelKind.Disc => (null, "none", "Disc"),
        GesModelKind.Schema => (null, "none", value.Model.Schema),
        _ => (null, "none", "Value")
    };

    // The declaration as a reader would write it, for documentation: `:Quantity(meter) between 0 and worldWidth step 0.01`.
    internal static string Describe(GesModelLayout layout, GesModelField field)
    {
        var text = new StringBuilder();
        Describe(field.Value, text);
        if (field.Optional) text.Append(" optional");
        if (field.Key) text.Append(" key");
        if (field.Default is { } fallback) text.Append(" default ").Append(DescribeDefault(layout, field.Value, fallback));
        return text.ToString();
    }

    // A default as a reader would write it, numbers in base units: `0.9`, `90%`, `#rear`, `[efficiency: 90%]`.
    private static string DescribeDefault(GesModelLayout layout, GesModelValue value, GesLayoutDefault fallback)
    {
        switch (fallback.Kind)
        {
            case GesSchemaDefaultKind.Number: return Decimal(fallback.Value, value.Type == "Percentage" ? 2 : 0);
            case GesSchemaDefaultKind.Boolean: return fallback.Boolean ? "true" : "false";
            case GesSchemaDefaultKind.Tag: return "#" + fallback.Value;
            case GesSchemaDefaultKind.Record:
                if (fallback.Entries.Count == 0) return "[:]";
                var fields = layout.Type(value.Schema).Fields;
                var entries = fallback.Entries.ConvertAll(entry => entry.Name + ": " + DescribeDefault(layout, fields.Find(field => field.Name == entry.Name)!.Value, entry.Value));
                return "[" + string.Join(", ", entries) + "]";
            default: return fallback.Value;
        }
    }

    // The fields of the record's schema that a record default names, with their values, in field order.
    internal static List<(GesModelPlanField Field, GesLayoutDefault Value)> Entries(GesModelLayout layout, GesModelPlanValue value, GesLayoutDefault fallback)
    {
        var fields = Fields(layout.Type(value.Model.Schema));
        return fallback.Entries.ConvertAll(entry => (fields.Find(field => field.Field.Name == entry.Name)!, entry.Value));
    }

    private static void Describe(GesModelValue value, StringBuilder text)
    {
        switch (value.Kind)
        {
            case GesModelKind.Boolean:
                text.Append(":Boolean");
                break;
            case GesModelKind.Number:
                text.Append(':').Append(value.Type);
                if (value.Type == "Quantity") text.Append('(').Append(value.Unit).Append(')');
                if (value.Integer) text.Append(" integer");
                var shift = value.Type == "Percentage" ? 2 : 0;
                if (value.Lower is not null) text.Append(value.Wrapping ? " wrapping" : "").Append(" between ").Append(Decimal(value.Lower, shift)).Append(" and ").Append(Decimal(value.Upper!, shift));
                if (value.Step is not null) text.Append(" step ").Append(Decimal(value.Step, shift));
                break;
            case GesModelKind.Tag:
                text.Append(":Tag");
                if (value.Tags.Count > 0) text.Append("(#").Append(string.Join(", #", value.Tags)).Append(')');
                else if (value.Max is { } tagMax) text.Append(" max ").Append(tagMax);
                break;
            case GesModelKind.Flags:
                text.Append(":Flags(#").Append(string.Join(", #", value.Tags)).Append(')');
                break;
            case GesModelKind.Text:
                text.Append(":Text");
                if (value.Max is { } textMax) text.Append(" max ").Append(textMax);
                break;
            case GesModelKind.Bytes:
                text.Append(":Bytes");
                if (value.Max is { } bytesMax) text.Append(" max ").Append(bytesMax);
                break;
            case GesModelKind.List:
                text.Append(":List(");
                Describe(value.Element!.Value, text);
                text.Append(')');
                if (value.Max is { } listMax) text.Append(" max ").Append(listMax);
                if (value.Depth > 0) text.Append(" depth ").Append(value.Depth);
                break;
            case GesModelKind.Dice:
                text.Append(":Dice");
                if (value.Max is { } diceMax) text.Append(" max ").Append(diceMax);
                if (value.Lower is not null) text.Append(" between ").Append(Decimal(value.Lower)).Append(" and ").Append(Decimal(value.Upper!));
                break;
            case GesModelKind.Point or GesModelKind.Vector or GesModelKind.Box or GesModelKind.Poly or GesModelKind.Disc:
                text.Append(':').Append(value.Kind.ToString());
                if (value.Unit != "none") text.Append('(').Append(value.Unit).Append(')');
                if (value.Kind == GesModelKind.Poly && value.Max is { } polyMax) text.Append(" max ").Append(polyMax);
                if (value.Axes.Count > 0) text.Append(" { ").Append(string.Join(", ", value.Axes)).Append(" }");
                break;
            case GesModelKind.Schema:
                text.Append(':').Append(value.Schema);
                break;
            case GesModelKind.Union:
                for (var index = 0; index < value.Alternatives.Count; index++)
                {
                    if (index > 0) text.Append(" | ");
                    Describe(value.Alternatives[index].Value, text);
                }

                break;
            default:
                text.Append(":Nothing");
                break;
        }
    }

    // A canonical decimal `<significand>e<exponent>` as plain digits; a parameter name stays as it is.
    // A Percentage's limits are fractions, written with a shift of two and a percent sign.
    internal static string Decimal(string canonical, int shift = 0)
    {
        var e = canonical.IndexOf('e');
        if (e < 0) return canonical;
        var digits = canonical.Substring(0, e);
        var exponent = int.Parse(canonical.Substring(e + 1), System.Globalization.CultureInfo.InvariantCulture) + shift;
        var percent = shift == 2 ? "%" : "";
        var negative = digits.StartsWith("-", System.StringComparison.Ordinal);
        if (negative) digits = digits.Substring(1);
        if (exponent >= 0) digits = digits == "0" ? "0" : digits + new string('0', exponent);
        else
        {
            var point = digits.Length + exponent;
            digits = point > 0 ? digits.Substring(0, point) + "." + digits.Substring(point) : "0." + new string('0', -point) + digits;
        }

        return (negative ? "-" + digits : digits) + percent;
    }

    // The header of a model file: the roots and their schema hashes, without parameters, which models never use.
    internal static string Header(GesModelLayout layout)
    {
        var text = new StringBuilder();
        text.Append("// <auto-generated>\n");
        text.Append("//     Generated by `ges schema generate` as models of ").Append(layout.Roots.Count == 1 ? "the root " : "the roots ").Append(string.Join(", ", layout.Roots)).Append(".\n");
        text.Append("//     Do not edit it; generate it again when a schema changes.\n");
        foreach (var root in layout.Roots)
            text.Append("//     ").Append(root).Append(": schema hash ").Append(GesSchemaLayoutJson.Hex(layout.Type(root).Hash)).Append(".\n");
        text.Append("// </auto-generated>\n");
        return text.ToString();
    }
}
