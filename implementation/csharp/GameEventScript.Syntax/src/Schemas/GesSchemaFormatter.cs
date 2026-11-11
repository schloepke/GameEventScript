// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System.Text;
using GameEventScript.Api;

namespace GameEventScript.Syntax;

// Prints schema source in the recommended layout. Literals keep their spelling; comments and layout are dropped.
internal static class GesSchemaFormatter
{
    internal static string Print(GesSchemaSourceSyntax source)
    {
        var builder = new StringBuilder();
        GesSchemaDeclarationKind? previous = null;
        foreach (var declaration in source.Declarations)
        {
            var simple = declaration.Kind is GesSchemaDeclarationKind.Constant or GesSchemaDeclarationKind.Parameter or GesSchemaDeclarationKind.TagSet;
            if (previous is { } last && !(simple && last == declaration.Kind)) builder.Append('\n');
            previous = declaration.Kind;
            Role("wire", declaration.WireGroup, builder);
            Role("model", declaration.ModelGroup, builder);
            switch (declaration.Kind)
            {
                case GesSchemaDeclarationKind.Constant:
                    builder.Append("constant $").Append(declaration.Name).Append(" be ").Append(declaration.Value!.Text).Append('\n');
                    break;
                case GesSchemaDeclarationKind.Parameter:
                    builder.Append("parameter ").Append(declaration.Name).Append(": ");
                    Type(declaration.Type!, builder, "");
                    builder.Append('\n');
                    break;
                case GesSchemaDeclarationKind.Schema:
                    builder.Append("schema :").Append(declaration.Name).Append(" as {\n");
                    foreach (var let in declaration.Lets) builder.Append("  let ").Append(let.Name).Append(" be ").Append(Value(let.Value)).Append('\n');
                    foreach (var field in declaration.Fields)
                    {
                        builder.Append("  ");
                        Field(field, builder, "  ");
                        builder.Append('\n');
                    }

                    builder.Append("}\n");
                    break;
                case GesSchemaDeclarationKind.Message:
                    builder.Append("message ").Append(declaration.Name).Append('(');
                    for (var index = 0; index < declaration.Fields.Length; index++)
                    {
                        if (index > 0) builder.Append(", ");
                        Field(declaration.Fields[index], builder, "");
                    }

                    builder.Append(')');
                    if (declaration.Tags.Length > 0)
                    {
                        builder.Append(" tags ");
                        Tags(declaration.Tags, builder);
                    }

                    builder.Append('\n');
                    break;
                case GesSchemaDeclarationKind.TagSet:
                    builder.Append("tags :").Append(declaration.Name).Append(" as ");
                    Tags(declaration.Tags, builder);
                    builder.Append('\n');
                    break;
                default:
                    builder.Append("messages :").Append(declaration.Name).Append(" as (").Append(string.Join(", ", declaration.Members)).Append(")\n");
                    break;
            }
        }

        return builder.ToString();
    }

    // The group `default` is the one a role without a group names.
    private static void Role(string role, string? group, StringBuilder builder)
    {
        if (group is null) return;
        builder.Append(role);
        if (group != "default") builder.Append('(').Append(group).Append(')');
        builder.Append(' ');
    }

    private static void Field(GesSchemaFieldSyntax field, StringBuilder builder, string indent)
    {
        if (field.Unlabeled) builder.Append("_ ");
        builder.Append(field.Name).Append(": ");
        Union(field.Alternatives, builder, indent);
        if (field.Marker == GesSchemaFieldMarker.Optional) builder.Append(" optional");
        else if (field.Marker == GesSchemaFieldMarker.Key) builder.Append(" key");
        if (field.Default is not { } fallback) return;
        builder.Append(" default ");
        if (fallback.Tag is { } tag) builder.Append('#').Append(tag);
        else if (fallback.Boolean is { } boolean) builder.Append(boolean ? "true" : "false");
        else builder.Append(Value(fallback.Value!));
    }

    private static void Union(GesSchemaTypeSyntax[] alternatives, StringBuilder builder, string indent)
    {
        for (var index = 0; index < alternatives.Length; index++)
        {
            if (index > 0) builder.Append(" | ");
            if (alternatives[index].Label.Length > 0) builder.Append(alternatives[index].Label).Append(": ");
            Type(alternatives[index], builder, indent);
        }
    }

    private static void Type(GesSchemaTypeSyntax type, StringBuilder builder, string indent)
    {
        switch (type.Kind)
        {
            case GesSchemaTypeKind.Quantity:
                builder.Append(":Quantity(").Append(type.Unit.ToSuffix()).Append(')');
                break;
            case GesSchemaTypeKind.Point:
            case GesSchemaTypeKind.Vector:
                builder.Append(type.Kind == GesSchemaTypeKind.Point ? ":Point" : ":Vector");
                if (type.Unit != GesQuantityUnit.UnitNone) builder.Append('(').Append(type.Unit.ToSuffix()).Append(')');
                if (type.Axes is { } axes)
                {
                    builder.Append(" {\n");
                    foreach (var axis in axes)
                    {
                        builder.Append(indent).Append("  ").Append(axis.Axis);
                        Constraints(axis.Constraints, builder);
                        builder.Append('\n');
                    }

                    builder.Append(indent).Append('}');
                }

                return;
            case GesSchemaTypeKind.Tag:
                builder.Append(":Tag");
                if (type.Tags is { } tags) Tags(tags, builder);
                if (type.Max is { } tagMax) builder.Append(" max ").Append(tagMax);
                else if (type.MaxName.Length > 0) builder.Append(" max ").Append(type.MaxName);
                return;
            case GesSchemaTypeKind.Flags:
                builder.Append(":Flags");
                Tags(type.Tags!, builder);
                return;
            case GesSchemaTypeKind.Text:
                builder.Append(":Text");
                if (type.Max is { } textMax) builder.Append(" max ").Append(textMax);
                else if (type.MaxName.Length > 0) builder.Append(" max ").Append(type.MaxName);
                return;
            case GesSchemaTypeKind.Bytes:
                builder.Append(":Bytes");
                if (type.Max is { } bytesMax) builder.Append(" max ").Append(bytesMax);
                else if (type.MaxName.Length > 0) builder.Append(" max ").Append(type.MaxName);
                return;
            case GesSchemaTypeKind.List:
                builder.Append(":List");
                if (type.Element is { } element)
                {
                    builder.Append('(');
                    Union(element, builder, indent);
                    builder.Append(')');
                }

                if (type.Max is { } listMax) builder.Append(" max ").Append(listMax);
                else if (type.MaxName.Length > 0) builder.Append(" max ").Append(type.MaxName);
                return;
            case GesSchemaTypeKind.Dice:
                builder.Append(":Dice");
                if (type.Max is { } diceMax) builder.Append(" max ").Append(diceMax);
                else if (type.MaxName.Length > 0) builder.Append(" max ").Append(type.MaxName);
                break;
            case GesSchemaTypeKind.Reference:
                builder.Append(':').Append(type.Name);
                return;
            default:
                builder.Append(':').Append(type.Kind.ToString());
                break;
        }

        Constraints(type.Constraints, builder);
    }

    private static void Constraints(GesSchemaConstraintsSyntax constraints, StringBuilder builder)
    {
        if (constraints.Integer) builder.Append(" integer");
        if (constraints.Lower is { } lower)
        {
            builder.Append(constraints.Wrapping ? " wrapping between " : " between ");
            builder.Append(Value(lower)).Append(" and ").Append(Value(constraints.Upper!));
        }

        if (constraints.Step is { } step) builder.Append(" step ").Append(Value(step));
        if (constraints.Delta is { } delta) builder.Append(" delta within ").Append(Value(delta));
    }

    private static string Value(GesSchemaValueSyntax value)
        => value.Name.Length > 0 ? value.Name : value.Constant.Length > 0 ? "$" + value.Constant : value.Text;

    private static void Tags(string[] tags, StringBuilder builder)
    {
        builder.Append('(');
        for (var index = 0; index < tags.Length; index++)
        {
            if (index > 0) builder.Append(", ");
            builder.Append('#').Append(tags[index]);
        }

        builder.Append(')');
    }
}
