// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using GameEventScript.Api;
using Facts = GameEventScript.Generator.GesComputedFacts;

namespace GameEventScript.Generator;

// What binding knows about a node: its range, whether it may give nothing (NaN or an absent field), a negative zero, or an
// exact Int64 that binary64 cannot hold.
internal sealed class GesComputedFacts
{
    internal double Lower = double.NegativeInfinity;
    internal double Upper = double.PositiveInfinity;
    internal bool MayBeNothing;
    internal bool NegativeZero;
    internal bool Unrepresentable;

    internal bool Infinite => double.IsInfinity(Lower) || double.IsInfinity(Upper);

    internal bool HasZero => Lower <= 0 && Upper >= 0;

    internal bool MayBeNegative => Lower < 0 || NegativeZero;

    internal double Magnitude => Math.Max(Math.Abs(Lower), Math.Abs(Upper));
}

// The static form of a computed field: plain binary64 and Boolean code from the bound expression. The virtual machine holds a
// number as an exact Int64 while it is whole, as binary64 otherwise, nothing for NaN and never a negative zero; binary64
// arithmetic gives the same bits whenever no exact Int64 beyond 2^53 meets another operand. Binding proves that from the
// declared ranges of the fields; NaN stands for nothing, and only the few places where IEEE and the virtual machine part,
// such as `not` of nothing or a negative zero, get a helper.
internal sealed partial class GesComputedEmitter
{
    private const double Limit = 9007199254740992d;

    private sealed class Analysis(string owner)
    {
        internal string Owner = owner;
        internal string Element = "";
        internal bool Risky;
    }

    private readonly Dictionary<GesComputedNode, Facts> _facts = [];
    private readonly Dictionary<(string Owner, string Name), Facts?> _fields = [];

    // The facts of a computed field's value after its conversion to the field's type, or null for the dynamic form.
    private Facts? Analyze(string owner, GesResolvedComputed field)
    {
        if (_fields.TryGetValue((owner, field.Name), out var known)) return known;
        var analysis = new Analysis(owner);
        var facts = Analyze(field.Expression, analysis);
        Facts? result = null;
        if (!analysis.Risky)
        {
            result = new Facts { Lower = facts.Lower, Upper = facts.Upper, Unrepresentable = facts.Unrepresentable };
            switch (field.Kind)
            {
                case GesSchemaTypeKind.Boolean:
                    break;
                case GesSchemaTypeKind.Percentage:
                    // A ratio that becomes infinite is no percentage, which the field's type turns into nothing.
                    result.MayBeNothing = facts.MayBeNothing || facts.Infinite || field.Optional;
                    break;
                default:
                    result.MayBeNothing = facts.MayBeNothing || field.Optional;
                    break;
            }
        }

        _fields[(owner, field.Name)] = result;
        return result;
    }

    private static Facts Unbounded(bool nothing) => new() { MayBeNothing = nothing, Unrepresentable = true };

    private static Facts Range(double lower, double upper)
    {
        var facts = new Facts();
        if (!double.IsNaN(lower)) facts.Lower = lower;
        if (!double.IsNaN(upper)) facts.Upper = upper;
        return facts;
    }

    private static double Min(params double[] values)
    {
        var result = double.PositiveInfinity;
        foreach (var value in values) result = double.IsNaN(value) ? double.NegativeInfinity : Math.Min(result, value);
        return result;
    }

    private static double Max(params double[] values)
    {
        var result = double.NegativeInfinity;
        foreach (var value in values) result = double.IsNaN(value) ? double.PositiveInfinity : Math.Max(result, value);
        return result;
    }

    private static Facts Product(Facts a, Facts b)
    {
        var corners = new[] { a.Lower * b.Lower, a.Lower * b.Upper, a.Upper * b.Lower, a.Upper * b.Upper };
        var facts = Range(Min(corners), Max(corners));
        facts.MayBeNothing = a.MayBeNothing || b.MayBeNothing || a.HasZero && b.Infinite || b.HasZero && a.Infinite;
        facts.NegativeZero = facts.HasZero && (a.MayBeNegative || b.MayBeNegative);
        return facts;
    }

    private static Facts Quotient(Facts a, Facts b)
    {
        Facts facts;
        if (b.HasZero) facts = new Facts();
        else
        {
            var corners = new[] { a.Lower / b.Lower, a.Lower / b.Upper, a.Upper / b.Lower, a.Upper / b.Upper };
            facts = Range(Min(corners), Max(corners));
            if (a.HasZero) facts = Range(Math.Min(facts.Lower, 0), Math.Max(facts.Upper, 0));
        }

        facts.MayBeNothing = a.MayBeNothing || b.MayBeNothing || a.HasZero && b.HasZero || a.Infinite && b.Infinite;
        facts.NegativeZero = facts.HasZero && (a.MayBeNegative || b.MayBeNegative);
        return facts;
    }

    private static Facts Sum(Facts a, Facts b, bool subtract)
    {
        var facts = subtract ? Range(a.Lower - b.Upper, a.Upper - b.Lower) : Range(a.Lower + b.Lower, a.Upper + b.Upper);
        facts.MayBeNothing = a.MayBeNothing || b.MayBeNothing || a.Infinite && b.Infinite;
        facts.NegativeZero = subtract ? a.NegativeZero && b.HasZero : a.NegativeZero && b.NegativeZero;
        return facts;
    }

    private static Facts Hull(IEnumerable<Facts> parts)
    {
        var facts = new Facts { Lower = double.PositiveInfinity, Upper = double.NegativeInfinity };
        foreach (var part in parts)
        {
            facts.Lower = Math.Min(facts.Lower, part.Lower);
            facts.Upper = Math.Max(facts.Upper, part.Upper);
            facts.MayBeNothing |= part.MayBeNothing;
            facts.NegativeZero |= part.NegativeZero;
            facts.Unrepresentable |= part.Unrepresentable;
        }

        return facts;
    }

    private Facts Analyze(GesComputedNode node, Analysis analysis)
    {
        var facts = NodeFacts(node, analysis);
        _facts[node] = facts;
        return facts;
    }

    // An operand that the virtual machine treats as an exact Int64 where binary64 rounds parts the two.
    private static void Exact(Analysis analysis, params Facts[] operands)
    {
        foreach (var operand in operands) analysis.Risky |= operand.Unrepresentable;
    }

    private Facts NodeFacts(GesComputedNode node, Analysis analysis)
    {
        var children = node.Children;
        var number = GesComputedType.Number;
        var percentage = GesComputedType.Percentage;
        switch (node.Op)
        {
            case GesComputedOp.Literal:
            {
                if (node.Type == GesComputedType.Boolean) return new Facts();
                var value = node.Literal.Kind == GesComputedKind.Integer ? node.Literal.Integer : node.Literal.Number;
                var facts = Range(value, value);
                facts.Unrepresentable = node.Literal.Kind == GesComputedKind.Integer && Math.Abs(value) > Limit;
                return facts;
            }
            case GesComputedOp.Field or GesComputedOp.Item:
                return Read(node, analysis);
            case GesComputedOp.Negate:
            {
                var a = Analyze(children[0], analysis);
                var facts = Range(-a.Upper, -a.Lower);
                facts.MayBeNothing = a.MayBeNothing;
                facts.NegativeZero = a.HasZero;
                facts.Unrepresentable = a.Unrepresentable;
                return facts;
            }
            case GesComputedOp.Abs:
            {
                var a = Analyze(children[0], analysis);
                var facts = Range(a.HasZero ? 0 : Math.Min(Math.Abs(a.Lower), Math.Abs(a.Upper)), a.Magnitude);
                facts.MayBeNothing = a.MayBeNothing;
                facts.Unrepresentable = a.Unrepresentable;
                return facts;
            }
            case GesComputedOp.Add or GesComputedOp.Subtract:
            {
                var a = Analyze(children[0], analysis);
                var b = Analyze(children[1], analysis);
                var subtract = node.Op == GesComputedOp.Subtract;
                if (children[0].Type == number && children[1].Type == percentage)
                {
                    // x ± x · p in binary64 from the rounded x, as the virtual machine computes it; an infinite p is a plain
                    // number there.
                    analysis.Risky |= b.Infinite;
                    return Sum(a, Product(a, b), subtract);
                }

                if (children[0].Type == percentage)
                {
                    analysis.Risky |= a.Infinite || b.Infinite;
                    return Sum(a, b, subtract);
                }

                Exact(analysis, a, b);
                var result = Sum(a, b, subtract);
                result.Unrepresentable = result.Magnitude > Limit;
                return result;
            }
            case GesComputedOp.Multiply:
            {
                var a = Analyze(children[0], analysis);
                var b = Analyze(children[1], analysis);
                var result = Product(a, b);
                if (children[0].Type == number && children[1].Type == number)
                {
                    Exact(analysis, a, b);
                    result.Unrepresentable = result.Magnitude > Limit;
                }

                return result;
            }
            case GesComputedOp.Divide:
                return Quotient(Analyze(children[0], analysis), Analyze(children[1], analysis));
            case GesComputedOp.Less or GesComputedOp.LessOrEqual or GesComputedOp.Greater or GesComputedOp.GreaterOrEqual or GesComputedOp.Equal or GesComputedOp.NotEqual:
            {
                var a = Analyze(children[0], analysis);
                var b = Analyze(children[1], analysis);
                Exact(analysis, a, b);
                return new Facts { MayBeNothing = a.MayBeNothing || b.MayBeNothing };
            }
            case GesComputedOp.Not:
                return new Facts { MayBeNothing = Analyze(children[0], analysis).MayBeNothing };
            case GesComputedOp.And or GesComputedOp.Or:
            {
                var a = Analyze(children[0], analysis);
                var b = Analyze(children[1], analysis);
                return new Facts { MayBeNothing = a.MayBeNothing || b.MayBeNothing };
            }
            case GesComputedOp.Choice:
            {
                var values = new List<Facts>();
                for (var index = 0; index < children.Length; index++)
                {
                    var facts = Analyze(children[index], analysis);
                    if (index % 2 == 0 || index == children.Length - 1) values.Add(facts);
                }

                return Hull(values);
            }
            case GesComputedOp.Min or GesComputedOp.Max or GesComputedOp.Clamp:
            {
                var parts = new List<Facts>();
                foreach (var child in children) parts.Add(Analyze(child, analysis));
                Exact(analysis, parts.ToArray());
                return Hull(parts);
            }
            case GesComputedOp.Default:
            {
                var a = Analyze(children[0], analysis);
                var b = Analyze(children[1], analysis);
                var facts = Hull([a, b]);
                facts.MayBeNothing = b.MayBeNothing;
                return facts;
            }
            case GesComputedOp.HasValue:
                Analyze(children[0], analysis);
                return new Facts();
            case GesComputedOp.Count:
            {
                var list = _access.Reach("", analysis.Owner, children[0].Path);
                return Range(0, Math.Min(list.MaxCount, Limit));
            }
            case GesComputedOp.Sum:
                return SumFacts(node, analysis);
            default:
                return FunctionFacts(node, analysis);
        }
    }

    // The range of a function in binary64. The virtual machine reads its operands as binary64, as static code does, and makes
    // a whole result within Int64 an integer, which may meet another operand as an exact Int64 beyond 2^53.
    private Facts FunctionFacts(GesComputedNode node, Analysis analysis)
    {
        var parts = new List<Facts>();
        foreach (var child in node.Children) parts.Add(Analyze(child, analysis));
        var a = parts[0];
        var nothing = false;
        foreach (var part in parts) nothing |= part.MayBeNothing;
        Facts facts;
        switch (node.Op)
        {
            case GesComputedOp.Power:
                facts = PowerFacts(a, node.Children[1], parts[1]);
                break;
            case GesComputedOp.Sqrt or GesComputedOp.Cbrt:
            {
                // A negative number has no root, and -infinity gives +infinity as pow does.
                var root = node.Op == GesComputedOp.Sqrt ? 0.5 : 1d / 3d;
                facts = double.IsNegativeInfinity(a.Lower) ? Range(0, double.PositiveInfinity) : Range(Math.Pow(Math.Max(a.Lower, 0), root), Math.Pow(Math.Max(a.Upper, 0), root));
                nothing |= a.Lower < 0;
                break;
            }
            case GesComputedOp.Sin or GesComputedOp.Cos:
                facts = Range(-1, 1);
                nothing |= a.Infinite;
                break;
            case GesComputedOp.Tan:
                facts = new Facts();
                nothing |= a.Infinite;
                break;
            case GesComputedOp.Asin or GesComputedOp.Atan:
                facts = Range(-Math.PI / 2, Math.PI / 2);
                nothing |= node.Op == GesComputedOp.Asin && (a.Lower < -1 || a.Upper > 1);
                break;
            case GesComputedOp.Acos:
                facts = Range(0, Math.PI);
                nothing |= a.Lower < -1 || a.Upper > 1;
                break;
            case GesComputedOp.Atan2:
                facts = Range(-Math.PI, Math.PI);
                break;
            case GesComputedOp.Hypot:
            {
                var sum = 0d;
                foreach (var part in parts) sum += part.Magnitude * part.Magnitude;
                facts = Range(0, Math.Sqrt(sum));
                break;
            }
            case GesComputedOp.Exp:
                facts = Range(Math.Exp(a.Lower), Math.Exp(a.Upper));
                break;
            case GesComputedOp.Ln:
                facts = Range(a.Lower <= 0 ? double.NegativeInfinity : Math.Log(a.Lower), Math.Log(Math.Max(a.Upper, 0)));
                nothing |= a.Lower < 0;
                break;
            case GesComputedOp.Rad or GesComputedOp.Deg:
            {
                // A conversion of an infinite angle is invalid.
                var radians = node.Op == GesComputedOp.Rad;
                facts = Range(radians ? a.Lower / 180 * Math.PI : a.Lower / Math.PI * 180, radians ? a.Upper / 180 * Math.PI : a.Upper / Math.PI * 180);
                nothing |= a.Infinite;
                break;
            }
            default:
                // Rounding gives an Int64, saturated beyond its range; binary64 keeps the bits only within it.
                facts = Range(Math.Floor(a.Lower), Math.Ceiling(a.Upper));
                analysis.Risky |= a.Infinite || a.Magnitude >= 9223372036854775808d;
                break;
        }

        // Only a negative operand, or a negative zero, gives a negative zero.
        var negative = false;
        foreach (var part in parts) negative |= part.MayBeNegative;
        facts.MayBeNothing |= nothing;
        facts.NegativeZero = node.Op is not (GesComputedOp.Cos or GesComputedOp.Exp or GesComputedOp.Hypot or GesComputedOp.Acos) && facts.HasZero && negative;
        facts.Unrepresentable = facts.Magnitude > Limit;
        return facts;
    }

    // A power is monotonic in its base on each side of zero for one exponent, and monotonic in its exponent for one base that
    // is not negative, so its range lies between the powers of the bounds and of both zeros. A negative base gives nothing for
    // an exponent that is no integer, and has no such bounds for more than one exponent.
    private static Facts PowerFacts(Facts a, GesComputedNode exponent, Facts b)
    {
        var literal = exponent.Op == GesComputedOp.Literal;
        if (!literal && a.Lower < 0) return new Facts { MayBeNothing = true };
        double[] exponents = literal ? [exponent.Literal.Numeric] : [b.Lower, b.Upper];
        var bases = new List<double> { a.Lower, a.Upper };
        if (a.HasZero) bases.AddRange([0d, -0d]);
        var corners = new List<double>();
        foreach (var x in bases)
            foreach (var e in exponents) corners.Add(GameEventScript.Runtime.GesNumber.Power(x, e));
        var facts = Range(Min(corners.ToArray()), Max(corners.ToArray()));
        facts.MayBeNothing = literal ? a.Lower < 0 && exponents[0] != Math.Floor(exponents[0]) : a.Infinite || b.Infinite;
        return facts;
    }

    private Facts SumFacts(GesComputedNode node, Analysis analysis)
    {
        var source = node.Children[0];
        var list = _access.Reach("", analysis.Owner, source.Path);
        Facts element;
        if (node.Children.Length > 1)
        {
            var previous = analysis.Element;
            analysis.Element = _access.ElementOwner(analysis.Owner, source.Path);
            element = Analyze(node.Children[1], analysis);
            analysis.Element = previous;
        }
        else element = Declared(_access.Element(analysis.Owner, source.Path), node.Type == GesComputedType.Percentage);

        var count = list.MaxCount;
        var facts = Range(Math.Min(0, count * element.Lower), Math.Max(0, count * element.Upper));
        facts.MayBeNothing = element.MayBeNothing || element.Infinite;
        Exact(analysis, element);

        // Each partial sum is added to the next element: as exact Int64 values in the virtual machine while they are whole,
        // and as a percentage only while it stays finite.
        analysis.Risky |= node.Type == GesComputedType.Percentage ? facts.Infinite : facts.Magnitude > Limit;
        return facts;
    }

    private static Facts Declared(GesComputedReach reach, bool percentage)
    {
        var facts = Range(reach.Lower, reach.Upper);
        facts.Unrepresentable = reach.Integer && facts.Magnitude > Limit;
        return facts;
    }

    private Facts Read(GesComputedNode node, Analysis analysis)
    {
        var owner = node.Op == GesComputedOp.Item ? analysis.Element : analysis.Owner;
        if (node.Type == GesComputedType.Record) return new Facts();
        var reach = _access.Reach("", owner, node.Path);
        Facts facts;
        if (reach.ComputedName.Length > 0)
        {
            var target = Array.Find(_access.Computed(reach.ComputedOwner), candidate => candidate.Name == reach.ComputedName)!;
            var known = Analyze(reach.ComputedOwner, target);
            facts = known is null ? Unbounded(true) : new Facts { Lower = known.Lower, Upper = known.Upper, MayBeNothing = known.MayBeNothing, Unrepresentable = known.Unrepresentable };
        }
        else facts = node.Type == GesComputedType.Boolean ? new Facts() : Declared(reach, node.Type == GesComputedType.Percentage);

        facts.MayBeNothing |= node.MayBeAbsent;
        return facts;
    }

    // ---- Code ----

    private enum Want
    {
        // A number, or a Boolean where nothing counts as false.
        Plain,

        // A Boolean that keeps nothing, as an optional Bool.
        Tri
    }

    // Generated text with its precedence: 10 primary, 9 unary, 8 multiplicative, 7 additive, 6 relational, 5 equality,
    // 4 and, 3 or, 2 conditional.
    private readonly struct Text(string value, int precedence)
    {
        internal string Value { get; } = value;

        internal int Precedence { get; } = precedence;
    }

    private static string Wrap(Text text, int precedence) => text.Precedence < precedence ? "(" + text.Value + ")" : text.Value;

    private string Nan => _swift ? "Double.nan" : "double.NaN";

    private string IsNan(Text value) => _swift ? Wrap(value, 10) + ".isNaN" : $"double.IsNaN({value.Value})";

    private string Null => _swift ? "nil" : "(bool?)null";

    private void Static(GesSchemaPlanner.Code code, GesResolvedComputed field, Facts facts, string access)
    {
        var root = _facts[field.Expression];
        var value = Emit(field.Expression, Want.Plain, "");
        string text;
        if (field.Kind == GesSchemaTypeKind.Boolean) text = value.Value;
        else if (field.Kind == GesSchemaTypeKind.Percentage)
            text = field.Optional ? Call("OptionalRatio", value.Value) : root.Infinite || root.NegativeZero ? Call("Ratio", value.Value) : value.Value;
        else text = field.Optional ? Call("Optional", value.Value) : root.NegativeZero ? Call("PositiveZero", value.Value) : value.Value;

        Summary(code, field);
        var property = Property(field.Name, _owner, _swift);
        var type = PropertyType(field, _swift);
        if (_swift)
        {
            var head = $"{(access.Length > 0 ? access + " " : "")}var {Member(property)}: {type}";
            if (head.Length + text.Length + 6 <= 120) code.Line($"{head} {{ {text} }}");
            else
            {
                code.Open(head + " {");
                foreach (var line in Lines(text)) code.Line(line);
                code.Close();
            }
        }
        else
        {
            var head = $"{access} {type} {property} =>";
            if (head.Length + text.Length + 2 <= 120) code.Line($"{head} {text};");
            else
            {
                code.Line(head);
                code.Indent();
                var lines = Lines(text);
                for (var index = 0; index < lines.Count; index++) code.Line(lines[index] + (index == lines.Count - 1 ? ";" : ""));
                code.Dedent();
            }
        }
    }

    // A long chain of choices, one choice per line.
    private static List<string> Lines(string text)
    {
        var lines = new List<string>();
        var depth = 0;
        var start = 0;
        for (var index = 0; index + 2 < text.Length; index++)
        {
            var character = text[index];
            if (character == '(') depth++;
            else if (character == ')') depth--;
            else if (depth == 0 && character == ' ' && text[index + 1] == ':' && text[index + 2] == ' ')
            {
                lines.Add((lines.Count == 0 ? "" : ": ") + text.Substring(start, index - start).TrimStart(':', ' '));
                start = index + 1;
            }
        }

        lines.Add((lines.Count == 0 ? "" : ": ") + text.Substring(start).TrimStart(':', ' '));
        for (var index = 1; index < lines.Count; index++) lines[index] = "    " + lines[index];
        return lines;
    }

    private string Literal(GesComputedNode node)
    {
        var text = GesSchemaPlanner.Decimal(node.Decimal.Significand, node.Decimal.Exponent);
        return text.IndexOf('.') < 0 && text.IndexOf('e') < 0 ? text + ".0" : text;
    }

    // An operand of an addition or subtraction. A C++ compiler, such as the one behind IL2CPP, may contract a product that
    // an addition takes in the same expression into one fused operation with other rounding; a product computed by a helper
    // call stays a separate operation, as in the VM. Swift does not contract without an explicit option.
    private Text Operand(GesComputedNode node, string root)
    {
        if (!_swift && node.Op == GesComputedOp.Multiply) return new Text(Product(node, root), 10);
        if (!_swift && node.Op == GesComputedOp.Negate && node.Children[0].Op == GesComputedOp.Multiply) return new Text("-" + Product(node.Children[0], root), 9);
        return Emit(node, Want.Plain, root);
    }

    private string Product(GesComputedNode node, string root) => Call("Product", Emit(node.Children[0], Want.Plain, root).Value, Emit(node.Children[1], Want.Plain, root).Value);

    private Text Emit(GesComputedNode node, Want want, string root)
    {
        var children = node.Children;
        var facts = _facts[node];
        switch (node.Op)
        {
            case GesComputedOp.Literal:
                if (node.Type == GesComputedType.Boolean) return new Text(node.Literal.Kind == GesComputedKind.True ? "true" : "false", 10);
                {
                    // A negative literal is a negation, which a following member access would otherwise bind first.
                    var literal = Literal(node);
                    return new Text(literal, literal.StartsWith("-", System.StringComparison.Ordinal) ? 9 : 10);
                }
            case GesComputedOp.Field or GesComputedOp.Item:
                return Field(node, want, root);
            case GesComputedOp.Negate:
            {
                var operand = Emit(children[0], Want.Plain, root);
                return new Text("-" + Wrap(operand, operand.Value.StartsWith("-", StringComparison.Ordinal) ? 11 : 9), 9);
            }
            case GesComputedOp.Abs:
                return new Text((_swift ? "abs(" : "global::System.Math.Abs(") + Emit(children[0], Want.Plain, root).Value + ")", 10);
            case GesComputedOp.Add or GesComputedOp.Subtract:
            {
                var symbol = node.Op == GesComputedOp.Add ? " + " : " - ";
                var a = Operand(children[0], root);
                var b = Operand(children[1], root);
                if (children[0].Type == GesComputedType.Number && children[1].Type == GesComputedType.Percentage)
                {
                    if (_swift && a.Precedence == 10 && children[0].Op != GesComputedOp.Literal && !a.Value.Contains("("))
                        return new Text(a.Value + symbol + a.Value + " * " + Wrap(b, 9), 7);
                    return new Text(Call(node.Op == GesComputedOp.Add ? "AddPercentage" : "SubtractPercentage", a.Value, b.Value), 10);
                }

                return new Text(Wrap(a, 7) + symbol + Wrap(b, 8), 7);
            }
            case GesComputedOp.Multiply:
                return new Text(Wrap(Emit(children[0], Want.Plain, root), 8) + " * " + Wrap(Emit(children[1], Want.Plain, root), 9), 8);
            case GesComputedOp.Divide:
            {
                var a = Emit(children[0], Want.Plain, root);
                var b = Emit(children[1], Want.Plain, root);
                var divisor = _facts[children[1]].NegativeZero ? Call("PositiveZero", b.Value) : Wrap(b, 9);
                return new Text(Wrap(a, 8) + " / " + divisor, 8);
            }
            case GesComputedOp.Less or GesComputedOp.LessOrEqual or GesComputedOp.Greater or GesComputedOp.GreaterOrEqual:
            {
                var a = Emit(children[0], Want.Plain, root);
                var b = Emit(children[1], Want.Plain, root);
                if (want == Want.Tri && facts.MayBeNothing)
                    return new Text(Call(node.Op + "OrNothing", a.Value, b.Value), 10);
                var symbol = node.Op switch
                {
                    GesComputedOp.Less => " < ",
                    GesComputedOp.LessOrEqual => " <= ",
                    GesComputedOp.Greater => " > ",
                    _ => " >= "
                };
                return new Text(Wrap(a, 7) + symbol + Wrap(b, 7), 6);
            }
            case GesComputedOp.Equal or GesComputedOp.NotEqual:
                return Equality(node, want, root);
            case GesComputedOp.Not:
            {
                var child = _facts[children[0]];
                if (!child.MayBeNothing) return new Text("!" + Wrap(Emit(children[0], Want.Plain, root), 9), 9);
                var operand = Emit(children[0], Want.Tri, root);
                if (want == Want.Plain) return new Text(Wrap(operand, 6) + " == false", 5);
                return _swift ? new Text(Wrap(operand, 10) + ".map { !$0 }", 10) : new Text("!" + Wrap(operand, 9), 9);
            }
            case GesComputedOp.And or GesComputedOp.Or:
            {
                var and = node.Op == GesComputedOp.And;
                if (want == Want.Tri && facts.MayBeNothing)
                    return new Text(Call(and ? "And" : "Or", Emit(children[0], Want.Tri, root).Value, Emit(children[1], Want.Tri, root).Value), 10);
                var precedence = and ? 4 : 3;
                return new Text(Wrap(Emit(children[0], Want.Plain, root), precedence) + (and ? " && " : " || ") + Wrap(Emit(children[1], Want.Plain, root), precedence), precedence);
            }
            case GesComputedOp.Choice:
            {
                var result = Emit(children[^1], want, root);
                for (var index = children.Length - 3; index >= 0; index -= 2)
                {
                    var condition = Emit(children[index + 1], Want.Plain, root);
                    var value = Emit(children[index], want, root);

                    // A condition that binding proves, such as `has value` of a field that is never absent, needs no choice.
                    if (condition.Value == "true") result = value;
                    else if (condition.Value != "false") result = new Text(Wrap(condition, 3) + " ? " + Wrap(value, 3) + " : " + Wrap(result, 2), 2);
                }

                return result;
            }
            case GesComputedOp.Min or GesComputedOp.Max:
            {
                var result = Emit(children[0], Want.Plain, root);
                for (var index = 1; index < children.Length; index++)
                    result = new Text(Call(node.Op == GesComputedOp.Min ? "Min" : "Max", result.Value, Emit(children[index], Want.Plain, root).Value), 10);
                return result;
            }
            case GesComputedOp.Clamp:
                return new Text(Call("Clamp", Emit(children[0], Want.Plain, root).Value, Emit(children[1], Want.Plain, root).Value, Emit(children[2], Want.Plain, root).Value), 10);
            case GesComputedOp.Default:
                return Fallback(node, want, root);
            case GesComputedOp.HasValue:
                return HasValue(node, root);
            case GesComputedOp.Count:
            {
                var list = _access.Reach(root, _owner, children[0].Path).Text;
                return _swift ? new Text($"Double({list}.count)", 10) : new Text($"(double){list}.Count", 9);
            }
            case GesComputedOp.Sum:
                return StaticSum(node, root);
            default:
                return StaticFunction(node, root);
        }
    }

    // A function: the platform's own call where binary64 gives the bits of the virtual machine, and a helper of
    // `GesComputed` where the virtual machine adds a rule, as for the root of -infinity, `atan2(0, 0)` or the angle of an
    // infinity. Swift reaches the math library through the helpers, so that no member of a record hides it. A base or angle
    // that may be a negative zero, which GES does not have, becomes a positive one first.
    private Text StaticFunction(GesComputedNode node, string root)
    {
        var children = node.Children;
        var operands = new List<Text>();
        foreach (var child in children)
        {
            // Only a power and an angle tell a negative zero from a positive one in more than the sign of a zero.
            var operand = Emit(child, Want.Plain, root);
            var sign = node.Op == GesComputedOp.Atan2 || node.Op == GesComputedOp.Power && operands.Count == 0;
            operands.Add(sign && _facts[child].NegativeZero ? new Text(Call("PositiveZero", operand.Value), 10) : operand);
        }

        var x = operands[0];
        var math = _swift ? "" : "global::System.Math.";
        switch (node.Op)
        {
            case GesComputedOp.Power:
            {
                var exponent = children[1];
                if (exponent.Op == GesComputedOp.Literal && exponent.Literal.Numeric == 0.5) return Root(x, _facts[children[0]]);
                if (exponent.Op == GesComputedOp.Literal && !_facts[children[0]].MayBeNothing)
                    return new Text(_swift ? Call("Power", x.Value, operands[1].Value) : $"{math}Pow({x.Value}, {operands[1].Value})", 10);
                return new Text(Call("Power", x.Value, operands[1].Value), 10);
            }
            case GesComputedOp.Sqrt:
                return Root(x, _facts[children[0]]);
            case GesComputedOp.Sin or GesComputedOp.Cos or GesComputedOp.Tan:
            {
                var name = node.Op switch { GesComputedOp.Sin => "Sine", GesComputedOp.Cos => "Cosine", _ => "Tangent" };
                return new Text(Call(name, node.First ? Call("Radians", x.Value) : x.Value), 10);
            }
            case GesComputedOp.Floor or GesComputedOp.Ceil or GesComputedOp.Truncate or GesComputedOp.RoundHalfEven or GesComputedOp.RoundHalfUp:
            {
                if (_swift)
                {
                    var rule = node.Op switch
                    {
                        GesComputedOp.Floor => ".down",
                        GesComputedOp.Ceil => ".up",
                        GesComputedOp.Truncate => ".towardZero",
                        GesComputedOp.RoundHalfEven => ".toNearestOrEven",
                        _ => ".toNearestOrAwayFromZero"
                    };
                    return new Text($"{Wrap(x, 10)}.rounded({rule})", 10);
                }

                return new Text(node.Op switch
                {
                    GesComputedOp.Floor => $"{math}Floor({x.Value})",
                    GesComputedOp.Ceil => $"{math}Ceiling({x.Value})",
                    GesComputedOp.Truncate => $"{math}Truncate({x.Value})",
                    GesComputedOp.RoundHalfEven => $"{math}Round({x.Value}, global::System.MidpointRounding.ToEven)",
                    _ => $"{math}Round({x.Value}, global::System.MidpointRounding.AwayFromZero)"
                }, 10);
            }
            default:
            {
                var arguments = new List<string>();
                foreach (var operand in operands) arguments.Add(operand.Value);
                return new Text(Call(FunctionHelper(node.Op), arguments.ToArray()), 10);
            }
        }
    }

    // A square root: the platform's correctly rounded one, through the helper where the operand may be -infinity, whose
    // root is +infinity as with pow.
    private Text Root(Text x, Facts operand)
    {
        if (double.IsNegativeInfinity(operand.Lower)) return new Text(Call("SquareRoot", x.Value), 10);
        return _swift ? new Text($"{Wrap(x, 10)}.squareRoot()", 10) : new Text($"global::System.Math.Sqrt({x.Value})", 10);
    }

    // The helper of a function in both forms: a binary64 one in static code, a GES value one in the dynamic form.
    private static string FunctionHelper(GesComputedOp op) => op switch
    {
        GesComputedOp.Power => "Power",
        GesComputedOp.Sqrt => "SquareRoot",
        GesComputedOp.Cbrt => "CubeRoot",
        GesComputedOp.Sin => "Sine",
        GesComputedOp.Cos => "Cosine",
        GesComputedOp.Tan => "Tangent",
        GesComputedOp.Asin => "Arcsine",
        GesComputedOp.Acos => "Arccosine",
        GesComputedOp.Atan or GesComputedOp.Atan2 => "Arctangent",
        GesComputedOp.Hypot => "Hypotenuse",
        GesComputedOp.Exp => "Exponential",
        GesComputedOp.Ln => "Logarithm",
        GesComputedOp.Floor => "Floor",
        GesComputedOp.Ceil => "Ceiling",
        GesComputedOp.Truncate => "Truncate",
        GesComputedOp.RoundHalfEven => "RoundHalfEven",
        GesComputedOp.RoundHalfUp => "RoundHalfUp",
        GesComputedOp.RoundHalfDown => "RoundHalfDown",
        GesComputedOp.Rad => "Radians",
        _ => "Degrees"
    };

    // Whether any computed field of the types uses a function or a power, whose helpers and, in Swift, math library the
    // generated code then holds.
    internal static bool UsesMath(IEnumerable<GesResolvedComputed[]> types)
    {
        foreach (var computed in types)
            foreach (var field in computed)
                if (UsesMath(field.Expression)) return true;
        return false;
    }

    private static bool UsesMath(GesComputedNode node)
    {
        if (node.Op >= GesComputedOp.Power) return true;
        foreach (var child in node.Children)
            if (UsesMath(child)) return true;
        return false;
    }

    // A read: a member converted to binary64, NaN where an optional field on the way is absent; Booleans stay Booleans.
    private Text Field(GesComputedNode node, Want want, string root)
    {
        var reach = _access.Reach(Root(node, root), Owner(node), node.Path);
        var conditions = new List<string>();
        foreach (var (step, condition) in reach.Conditions)
            if (step >= node.Present) conditions.Add(condition);
        var when = string.Join(" && ", conditions);
        if (reach.ComputedName.Length > 0)
        {
            var target = Array.Find(_access.Computed(reach.ComputedOwner), candidate => candidate.Name == reach.ComputedName)!;
            var text = Join(reach.Text, Member(Property(target.Name, reach.ComputedOwner, _swift)));
            if (target.Kind == GesSchemaTypeKind.Boolean)
            {
                if (reach.Nullable) return want == Want.Tri ? new Text(text, 10) : new Text(text + " == true", 5);
                if (conditions.Count == 0) return new Text(text, 10);
                return want == Want.Tri ? new Text($"{when} ? {text} : {Null}", 2) : new Text($"{when} && {Wrap(new Text(text, 10), 4)}", 4);
            }

            if (target.Optional || reach.Nullable) text = $"({text} ?? {Nan})";
            return conditions.Count == 0 ? new Text(text, 10) : new Text($"{when} ? {text} : {Nan}", 2);
        }

        var member = reach.Text;
        if (node.Type == GesComputedType.Boolean)
        {
            if (reach.Nullable) return want == Want.Tri ? new Text(member, 10) : new Text(member + " == true", 5);
            if (conditions.Count == 0) return new Text(member, 10);
            return want == Want.Tri ? new Text($"{when} ? {member} : {Null}", 2) : new Text($"{when} && {member}", 4);
        }

        if (reach.Nullable)
        {
            var number = reach.Integer ? _swift ? $"{member}.map {{ Double($0) }}" : member : member;
            return new Text($"({number} ?? {Nan})", 10);
        }

        var value = reach.Integer ? _swift ? $"Double({member})" : $"(double){member}" : member;
        if (conditions.Count == 0) return new Text(value, reach.Integer && !_swift ? 9 : 10);
        return new Text($"{when} ? {value} : {Nan}", 2);
    }

    private Text Equality(GesComputedNode node, Want want, string root)
    {
        var children = node.Children;
        var equal = node.Op == GesComputedOp.Equal;
        var facts = _facts[node];
        if (children[0].Type == GesComputedType.Boolean)
        {
            if (!facts.MayBeNothing)
                return new Text(Wrap(Emit(children[0], Want.Plain, root), 6) + (equal ? " == " : " != ") + Wrap(Emit(children[1], Want.Plain, root), 6), 5);
            var same = new Text(Call("Same", Emit(children[0], Want.Tri, root).Value, Emit(children[1], Want.Tri, root).Value), 10);
            return Truth(same, equal, want);
        }

        var name = children[0].Type == GesComputedType.Percentage ? "EqualRatios" : "Equal";
        var a = Emit(children[0], Want.Plain, root).Value;
        var b = Emit(children[1], Want.Plain, root).Value;
        if (!facts.MayBeNothing)
        {
            var call = new Text(Call(name, a, b), 10);
            return equal ? call : new Text("!" + call.Value, 9);
        }

        if (want == Want.Plain && equal) return new Text(Call(name, a, b), 10);
        return Truth(new Text(Call(name + "OrNothing", a, b), 10), equal, want);
    }

    // An optional Boolean as asked for: kept, or true only for true (or for false when negated).
    private Text Truth(Text value, bool positive, Want want)
    {
        if (want == Want.Plain) return new Text(value.Value + (positive ? " == true" : " == false"), 5);
        if (positive) return value;
        return _swift ? new Text(value.Value + ".map { !$0 }", 10) : new Text("!" + value.Value, 9);
    }

    private Text Fallback(GesComputedNode node, Want want, string root)
    {
        var children = node.Children;
        var left = _facts[children[0]];
        if (!left.MayBeNothing) return Emit(children[0], want, root);
        if (node.Type == GesComputedType.Boolean)
        {
            var value = Emit(children[0], Want.Tri, root);
            var fallback = Emit(children[1], want, root);
            // `??` binds tighter than comparisons and conditionals in C# and Swift, so each side stays whole.
            return new Text($"({Wrap(value, 10)} ?? {Wrap(fallback, 10)})", 10);
        }

        var right = Emit(children[1], Want.Plain, root);

        // An optional field that is only ever absent reads its fallback directly.
        if (children[0].Op is GesComputedOp.Field or GesComputedOp.Item && children[0].Type != GesComputedType.Record)
        {
            var reach = _access.Reach(Root(children[0], root), Owner(children[0]), children[0].Path);
            if (reach.ComputedName.Length == 0 && reach.Conditions.Count + (reach.Nullable ? 1 : 0) > 0 && (reach.Conditions.Count == 0 || !reach.Nullable))
            {
                var conditions = new List<string>();
                foreach (var (step, condition) in reach.Conditions)
                    if (step >= children[0].Present) conditions.Add(condition);
                var member = reach.Integer ? _swift ? $"Double({reach.Text})" : $"(double){reach.Text}" : reach.Text;
                if (reach.Nullable)
                {
                    var number = reach.Integer && _swift ? $"{reach.Text}.map {{ Double($0) }}" : reach.Text;
                    return new Text($"({number} ?? {Wrap(right, 10)})", 10);
                }

                if (conditions.Count > 0) return new Text($"{string.Join(" && ", conditions)} ? {member} : {Wrap(right, 3)}", 2);
            }
        }

        return new Text(Call("Otherwise", Emit(children[0], Want.Plain, root).Value, right.Value), 10);
    }

    private Text HasValue(GesComputedNode node, string root)
    {
        var child = node.Children[0];
        if (child.Type == GesComputedType.Record) return new Text(Present(Root(child, root), Owner(child), child.Path), 4);
        var facts = _facts[child];
        if (!facts.MayBeNothing) return new Text("true", 10);

        // A stored field is nothing only where it is absent, which its presence flags or its optional value tell.
        if (child.Op is GesComputedOp.Field or GesComputedOp.Item)
        {
            var reach = _access.Reach(Root(child, root), Owner(child), child.Path);
            if (reach.ComputedName.Length == 0)
            {
                if (reach.Nullable) return new Text(reach.Text + (_swift ? " != nil" : " != null"), 5);
                var conditions = new List<string>();
                foreach (var (step, condition) in reach.Conditions)
                    if (step >= child.Present) conditions.Add(condition);
                if (conditions.Count == 0) return new Text("true", 10);
                return new Text(string.Join(" && ", conditions), conditions.Count == 1 ? 10 : 4);
            }
        }

        if (child.Type == GesComputedType.Boolean) return new Text(Wrap(Emit(child, Want.Tri, root), 6) + (_swift ? " != nil" : " != null"), 5);
        return new Text("!" + IsNan(Emit(child, Want.Plain, root)), 9);
    }

    // A sum adds the projected elements from left to right; binary64 from 0 gives the bits of the virtual machine, which
    // starts with the first element and has no negative zero.
    private Text StaticSum(GesComputedNode node, string root)
    {
        var self = _swift ? "self" : "this";
        var list = _access.Reach(self, _owner, node.Children[0].Path).Text;
        var name = $"__{_field}Sum{_sums.Count.ToString(CultureInfo.InvariantCulture)}";
        var previous = _element;
        _element = _access.ElementOwner(_owner, node.Children[0].Path);
        string value;
        if (node.Children.Length > 1) value = Emit(node.Children[1], Want.Plain, self).Value;
        else
        {
            var element = _access.Element(_owner, node.Children[0].Path);
            value = element.Integer ? _swift ? "Double(item)" : "(double)item" : "item";
        }

        _element = previous;
        var body = new StringBuilder();
        if (_swift)
        {
            body.Append($"private func {name}() -> Double {{\n");
            body.Append("    var sum = 0.0\n");
            body.Append($"    for item in {list} {{ sum += {value} }}\n");
            body.Append("    return sum\n");
            body.Append('}');
        }
        else
        {
            body.Append($"private double {name}()\n");
            body.Append("{\n");
            body.Append("    var sum = 0.0;\n");
            body.Append($"    foreach (var item in {list}) sum += {value};\n");
            body.Append("    return sum;\n");
            body.Append('}');
        }

        _sums.Add(body.ToString());
        return new Text($"{name}()", 10);
    }
}
