// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using Code = GameEventScript.Syntax.GesSchemaDiagnostic;

namespace GameEventScript.Syntax;

// The expressions of computed fields: a subset of GES expressions with their precedence and spellings, so that the same text
// means the same in a script record. A line break may follow an operator, a keyword, a comma or an opening parenthesis or
// bracket, where the expression cannot end.
internal sealed partial class GesSchemaSourceReader
{
    private const int MaximumExpressionDepth = 32;

    private int _expressionNesting;

    // A guarded choice `value when condition, …, otherwise fallback`, or a single expression.
    private GesSchemaExpressionSyntax ReadExpression()
    {
        if (++_expressionNesting > MaximumExpressionDepth) throw Failure("The expression is nested too deeply.");
        var offset = _position;
        var first = ReadDefaultLevel();
        SkipSpaces();
        if (!AtWord("when"))
        {
            _expressionNesting--;
            return first;
        }

        var parts = new List<GesSchemaExpressionSyntax> { first };
        while (true)
        {
            ExpectWord("when");
            SkipContinuation();
            parts.Add(ReadDefaultLevel());
            var end = _position;
            SkipSpaces();
            var comma = Consume(',');
            SkipContinuation();
            if (AtWord("otherwise"))
            {
                ReadWord();
                SkipContinuation();
                parts.Add(ReadDefaultLevel());
                break;
            }

            if (!comma)
            {
                _position = end;
                throw Failure("Expected 'otherwise'.");
            }

            parts.Add(ReadDefaultLevel());
            SkipSpaces();
            if (!AtWord("when")) throw Failure("Expected 'when'.");
        }

        _expressionNesting--;
        return Node(GesSchemaExpressionKind.Choice, offset, parts.ToArray());
    }

    private GesSchemaExpressionSyntax ReadDefaultLevel()
    {
        var left = ReadBinary(0);
        while (true)
        {
            SkipSpaces();
            if (!AtWord("default")) return left;
            ReadWord();
            SkipContinuation();
            left = Node(GesSchemaExpressionKind.Default, left.Offset, left, ReadBinary(0));
        }
    }

    // The binary levels from `or` down to multiplication; each level reads the next higher one.
    private GesSchemaExpressionSyntax ReadBinary(int level)
    {
        if (level == 6) return ReadUnary();
        var left = ReadBinary(level + 1);
        while (true)
        {
            SkipSpaces();
            if (level == 3 && AtWord("has"))
            {
                ReadWord();
                SkipSpaces();
                ExpectWord("value");
                left = Node(GesSchemaExpressionKind.HasValue, left.Offset, left);
                continue;
            }

            if (Operator(level) is not { } found) return left;
            SkipContinuation();
            left = new GesSchemaExpressionSyntax
            {
                Kind = GesSchemaExpressionKind.Binary,
                Operator = found,
                Children = [left, ReadBinary(level + 1)],
                Offset = left.Offset
            };
            left = Checked(left);
        }
    }

    // Consumes the operator of a level: 0 or, 1 and, 2 equality, 3 relation, 4 addition, 5 multiplication.
    private GesSchemaBinaryOperator? Operator(int level)
    {
        switch (level)
        {
            case 0 when AtWord("or"):
                ReadWord();
                return GesSchemaBinaryOperator.Or;
            case 1 when AtWord("and"):
                ReadWord();
                return GesSchemaBinaryOperator.And;
            case 2 when At("<>"):
                _position += 2;
                return GesSchemaBinaryOperator.NotEqual;
            case 2 when At("=") && !At("=>"):
                _position++;
                return GesSchemaBinaryOperator.Equal;
            case 3 when At("<=") || At(">="):
                var lessOrEqual = _text[_position] == '<';
                _position += 2;
                return lessOrEqual ? GesSchemaBinaryOperator.LessOrEqual : GesSchemaBinaryOperator.GreaterOrEqual;
            case 3 when (At("<") && !At("<>")) || At(">"):
                var less = _text[_position] == '<';
                _position++;
                return less ? GesSchemaBinaryOperator.Less : GesSchemaBinaryOperator.Greater;
            case 4 when At("+") || (At("-") && !At("->")):
                var add = _text[_position] == '+';
                _position++;
                return add ? GesSchemaBinaryOperator.Add : GesSchemaBinaryOperator.Subtract;
            case 5 when At("*") || (At("/") && !AtComment):
                var multiply = _text[_position] == '*';
                _position++;
                return multiply ? GesSchemaBinaryOperator.Multiply : GesSchemaBinaryOperator.Divide;
            default:
                return null;
        }
    }

    private GesSchemaExpressionSyntax ReadUnary()
    {
        SkipSpaces();
        var offset = _position;
        if (Consume('-'))
        {
            SkipSpaces();
            return Node(GesSchemaExpressionKind.Negate, offset, ReadUnary());
        }

        foreach (var (word, kind) in new[] { ("not", GesSchemaExpressionKind.Not), ("abs", GesSchemaExpressionKind.Abs) })
        {
            if (!AtWord(word)) continue;
            ReadWord();
            SkipSpaces();
            return Node(kind, offset, ReadUnary());
        }

        return ReadPostfix();
    }

    // Member access `record.field` and the list aggregates `list[:sum]`, `list[:sum item => value]` and `list[:count]`.
    private GesSchemaExpressionSyntax ReadPostfix()
    {
        var value = ReadPrimary();
        while (true)
        {
            if (At(".") && _position + 1 < _text.Length && GesText.IsAsciiLower(_text[_position + 1]))
            {
                var offset = ++_position;
                var member = ReadName(GesText.IsFieldName, "field name");
                value = Checked(new GesSchemaExpressionSyntax { Kind = GesSchemaExpressionKind.Member, Name = member, Children = [value], Depth = value.Depth + 1, Offset = offset });
                continue;
            }

            if (!At("[")) return value;
            var start = _position++;
            SkipTrivia();
            Expect(':');
            var selector = ReadWord();
            SkipTrivia();
            if (selector == "count")
            {
                Expect(']');
                value = Node(GesSchemaExpressionKind.Count, start, value);
                continue;
            }

            if (selector != "sum") throw new SyntaxFailure(start, "A computed expression takes the list selectors :sum and :count.");
            if (Consume(']'))
            {
                value = Node(GesSchemaExpressionKind.Sum, start, value);
                continue;
            }

            var variable = ReadName(GesText.IsFieldName, "variable name");
            SkipSpaces();
            if (!At("=>")) throw Failure("Expected '=>'.");
            _position += 2;
            SkipContinuation();
            var projection = ReadExpression();
            SkipTrivia();
            Expect(']');
            value = Checked(new GesSchemaExpressionSyntax
            {
                Kind = GesSchemaExpressionKind.Sum,
                Name = variable,
                Children = [value, projection],
                Depth = System.Math.Max(value.Depth, projection.Depth) + 1,
                Offset = start
            });
        }
    }

    private GesSchemaExpressionSyntax ReadPrimary()
    {
        SkipSpaces();
        var offset = _position;
        if (_position >= _text.Length) throw Failure("Expected an expression.");
        if (Consume('('))
        {
            SkipContinuation();
            var inner = ReadExpression();
            SkipContinuation();
            Expect(')');
            return inner;
        }

        if (Consume('$')) return new GesSchemaExpressionSyntax { Kind = GesSchemaExpressionKind.Constant, Name = ReadName(GesText.IsConstantName, "constant name"), Offset = offset };
        foreach (var word in new[] { "true", "false" })
            if (AtWord(word))
            {
                ReadWord();
                return new GesSchemaExpressionSyntax { Kind = GesSchemaExpressionKind.Boolean, Boolean = word == "true", Offset = offset };
            }

        if (AtWord("min") || AtWord("max"))
        {
            var kind = ReadWord() == "min" ? GesSchemaExpressionKind.Min : GesSchemaExpressionKind.Max;
            SkipSpaces();
            ExpectWord("of");
            SkipContinuation();
            var arguments = new List<GesSchemaExpressionSyntax> { ReadBinary(4) };
            while (true)
            {
                SkipSpaces();
                if (!AtWord("and")) break;
                ReadWord();
                SkipContinuation();
                arguments.Add(ReadBinary(4));
            }

            if (arguments.Count < 2) throw new SyntaxFailure(offset, "min of and max of take at least two values joined by and.");
            return Node(kind, offset, arguments.ToArray());
        }

        if (AtWord("clamp"))
        {
            ReadWord();
            SkipSpaces();
            var value = ReadBinary(4);
            SkipSpaces();
            ExpectWord("between");
            SkipContinuation();
            var lower = ReadBinary(4);
            SkipSpaces();
            ExpectWord("and");
            SkipContinuation();
            return Node(GesSchemaExpressionKind.Clamp, offset, value, lower, ReadBinary(4));
        }

        if (GesText.IsAsciiLower(_text[_position]))
        {
            var name = ReadName(GesText.IsFieldName, "name");
            var tokens = GesLexer.Tokenize(name);
            if (tokens.Count == 0 || tokens[0].Kind != GesTokenKind.Identifier)
                throw new SyntaxFailure(offset, $"'{name}' is a GES keyword or constant, which a computed expression cannot name.", Code.InvalidExpression);
            return new GesSchemaExpressionSyntax { Kind = GesSchemaExpressionKind.Name, Name = name, Offset = offset };
        }

        if (_text[_position] is >= '0' and <= '9') return new GesSchemaExpressionSyntax { Kind = GesSchemaExpressionKind.Literal, Literal = ReadExpressionLiteral(), Offset = offset };
        throw Failure("Expected an expression.");
    }

    // A number with its suffix. Besides the ends of other literals it stops at an operator; a minus continues only an exponent.
    private GesSchemaValueSyntax ReadExpressionLiteral()
    {
        var offset = _position;
        var end = _position;
        while (end < _text.Length && !IsExpressionLiteralEnd(end)) end++;
        var value = GesSchemaLiterals.Read(_text, offset, end) ?? throw new SyntaxFailure(offset, "Expected an exact finite number with a unit known to literals.");
        _position = end;
        return new GesSchemaValueSyntax { Value = value.Value, Unit = value.Unit, IsPercent = value.IsPercent, Text = _text.Substring(offset, end - offset), Offset = offset };
    }

    private bool IsExpressionLiteralEnd(int index)
    {
        var c = _text[index];
        if (c == '-') return !(index > 1 && _text[index - 1] is 'e' or 'E' && _text[index - 2] is >= '0' and <= '9');
        return IsLiteralEnd(index) || c is '[' or ']' or '+' or '*' or '<' or '>' or '=';
    }

    // Spaces, line breaks and comments where an expression continues.
    private void SkipContinuation() => SkipTrivia();

    private bool At(string text) => string.CompareOrdinal(_text, _position, text, 0, text.Length) == 0;

    private GesSchemaExpressionSyntax Node(GesSchemaExpressionKind kind, int offset, params GesSchemaExpressionSyntax[] children)
    {
        var depth = 0;
        foreach (var child in children) depth = System.Math.Max(depth, child.Depth);
        return Checked(new GesSchemaExpressionSyntax { Kind = kind, Children = children, Depth = depth + 1, Offset = offset });
    }

    private GesSchemaExpressionSyntax Checked(GesSchemaExpressionSyntax node)
    {
        var depth = 0;
        foreach (var child in node.Children) depth = System.Math.Max(depth, child.Depth);
        if (depth + 1 > MaximumExpressionDepth) throw new SyntaxFailure(node.Offset, "The expression is nested too deeply.");
        return node.Depth == depth + 1 ? node : new GesSchemaExpressionSyntax
        {
            Kind = node.Kind,
            Operator = node.Operator,
            Literal = node.Literal,
            Boolean = node.Boolean,
            Name = node.Name,
            Children = node.Children,
            Depth = depth + 1,
            Offset = node.Offset
        };
    }
}
