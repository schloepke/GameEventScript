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
                    if (declaration.Abstract) builder.Append("abstract ");
                    builder.Append("schema :").Append(declaration.Name).Append(" as {\n");
                    foreach (var let in declaration.Lets) builder.Append("  let ").Append(let.Name).Append(" be ").Append(Value(let.Value)).Append('\n');
                    // Fields, computed fields and withs in their written order.
                    var withIndex = 0;
                    var fieldIndex = 0;
                    var computedIndex = 0;
                    while (true)
                    {
                        var with = withIndex < declaration.Withs.Length ? declaration.Withs[withIndex].Sequence : int.MaxValue;
                        var field = fieldIndex < declaration.Fields.Length ? declaration.Fields[fieldIndex].Sequence : int.MaxValue;
                        var computed = computedIndex < declaration.Computed.Length ? declaration.Computed[computedIndex].Sequence : int.MaxValue;
                        if (with == int.MaxValue && field == int.MaxValue && computed == int.MaxValue) break;
                        if (with < field && with < computed)
                        {
                            With(declaration.Withs[withIndex++], builder);
                            continue;
                        }

                        builder.Append("  ");
                        Field(field < computed ? declaration.Fields[fieldIndex++] : declaration.Computed[computedIndex++], builder, "  ");
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

    private static void With(GesSchemaWithSyntax with, StringBuilder builder)
    {
        builder.Append("  with :").Append(with.Name);
        if (with.AllOptional) builder.Append(" all optional");
        if (with.Without.Length > 0) builder.Append(" without (").Append(string.Join(", ", with.Without)).Append(')');
        builder.Append('\n');
    }

    private static void Field(GesSchemaFieldSyntax field, StringBuilder builder, string indent)
    {
        if (field.Unlabeled) builder.Append("_ ");
        builder.Append(field.Name).Append(": ");
        Union(field.Alternatives, builder, indent);
        if (field.Marker == GesSchemaFieldMarker.Optional) builder.Append(" optional");
        else if (field.Marker == GesSchemaFieldMarker.Key) builder.Append(" key");
        if (field.Computed is { } computed)
        {
            builder.Append(" computed by ");
            Expression(computed, 0, builder);
            return;
        }

        if (field.Default is not { } fallback) return;
        builder.Append(" default ");
        if (fallback.Tag is { } tag) builder.Append('#').Append(tag);
        else if (fallback.Boolean is { } boolean) builder.Append(boolean ? "true" : "false");
        else builder.Append(Value(fallback.Value!));
    }

    internal static string Expression(GesSchemaExpressionSyntax expression)
    {
        var builder = new StringBuilder();
        Expression(expression, 0, builder);
        return builder.ToString();
    }

    // Prints an expression on one line with the parentheses its precedence needs: 0 choice, 1 default, 2 or, 3 and,
    // 4 equality, 5 relation, 6 addition, 7 multiplication, 8 unary, 9 postfix and primary.
    private static void Expression(GesSchemaExpressionSyntax expression, int minimum, StringBuilder builder)
    {
        var level = Level(expression);
        if (level < minimum) builder.Append('(');
        var children = expression.Children;
        switch (expression.Kind)
        {
            case GesSchemaExpressionKind.Literal:
                builder.Append(expression.Literal!.Text);
                break;
            case GesSchemaExpressionKind.Boolean:
                builder.Append(expression.Boolean ? "true" : "false");
                break;
            case GesSchemaExpressionKind.Constant:
                builder.Append('$').Append(expression.Name);
                break;
            case GesSchemaExpressionKind.Name:
                builder.Append(expression.Name);
                break;
            case GesSchemaExpressionKind.Member:
                Expression(children[0], 9, builder);
                builder.Append('.').Append(expression.Name);
                break;
            case GesSchemaExpressionKind.Negate:
                builder.Append('-');
                Expression(children[0], 8, builder);
                break;
            case GesSchemaExpressionKind.Not or GesSchemaExpressionKind.Abs:
                builder.Append(expression.Kind == GesSchemaExpressionKind.Not ? "not " : "abs ");
                Expression(children[0], 8, builder);
                break;
            case GesSchemaExpressionKind.Binary:
                Expression(children[0], level, builder);
                builder.Append(' ').Append(Symbol(expression.Operator)).Append(' ');
                Expression(children[1], level + 1, builder);
                break;
            case GesSchemaExpressionKind.Choice:
                for (var index = 0; index + 1 < children.Length; index += 2)
                {
                    Expression(children[index], 1, builder);
                    builder.Append(" when ");
                    Expression(children[index + 1], 1, builder);
                    builder.Append(", ");
                }

                builder.Append("otherwise ");
                Expression(children[^1], 1, builder);
                break;
            case GesSchemaExpressionKind.Min or GesSchemaExpressionKind.Max:
                builder.Append(expression.Kind == GesSchemaExpressionKind.Min ? "min of " : "max of ");
                for (var index = 0; index < children.Length; index++)
                {
                    if (index > 0) builder.Append(" and ");
                    Expression(children[index], 6, builder);
                }

                break;
            case GesSchemaExpressionKind.Clamp:
                builder.Append("clamp ");
                Expression(children[0], 6, builder);
                builder.Append(" between ");
                Expression(children[1], 6, builder);
                builder.Append(" and ");
                Expression(children[2], 6, builder);
                break;
            case GesSchemaExpressionKind.Default:
                Expression(children[0], 1, builder);
                builder.Append(" default ");
                Expression(children[1], 2, builder);
                break;
            case GesSchemaExpressionKind.HasValue:
                Expression(children[0], 6, builder);
                builder.Append(" has value");
                break;
            case GesSchemaExpressionKind.Count:
                Expression(children[0], 9, builder);
                builder.Append("[:count]");
                break;
            default:
                Expression(children[0], 9, builder);
                builder.Append("[:sum");
                if (children.Length > 1)
                {
                    builder.Append(' ').Append(expression.Name).Append(" => ");
                    Expression(children[1], 0, builder);
                }

                builder.Append(']');
                break;
        }

        if (level < minimum) builder.Append(')');
    }

    private static int Level(GesSchemaExpressionSyntax expression) => expression.Kind switch
    {
        GesSchemaExpressionKind.Choice => 0,
        GesSchemaExpressionKind.Default => 1,
        GesSchemaExpressionKind.HasValue => 5,
        GesSchemaExpressionKind.Negate or GesSchemaExpressionKind.Not or GesSchemaExpressionKind.Abs => 8,
        GesSchemaExpressionKind.Binary => expression.Operator switch
        {
            GesSchemaBinaryOperator.Or => 2,
            GesSchemaBinaryOperator.And => 3,
            GesSchemaBinaryOperator.Equal or GesSchemaBinaryOperator.NotEqual => 4,
            GesSchemaBinaryOperator.Add or GesSchemaBinaryOperator.Subtract => 6,
            GesSchemaBinaryOperator.Multiply or GesSchemaBinaryOperator.Divide => 7,
            _ => 5
        },
        _ => 9
    };

    internal static string Symbol(GesSchemaBinaryOperator binary) => binary switch
    {
        GesSchemaBinaryOperator.Add => "+",
        GesSchemaBinaryOperator.Subtract => "-",
        GesSchemaBinaryOperator.Multiply => "*",
        GesSchemaBinaryOperator.Divide => "/",
        GesSchemaBinaryOperator.Less => "<",
        GesSchemaBinaryOperator.LessOrEqual => "<=",
        GesSchemaBinaryOperator.Greater => ">",
        GesSchemaBinaryOperator.GreaterOrEqual => ">=",
        GesSchemaBinaryOperator.Equal => "=",
        GesSchemaBinaryOperator.NotEqual => "<>",
        GesSchemaBinaryOperator.And => "and",
        _ => "or"
    };

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
