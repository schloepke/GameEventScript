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

        if (AtWord("not"))
        {
            ReadWord();
            SkipSpaces();
            return Node(GesSchemaExpressionKind.Not, offset, ReadUnary());
        }

        var abs = AtWord("abs");
        if (abs) ReadWord();
        if ((abs ? "abs" : ReadPrefixFunction()) is { } function)
        {
            SkipSpaces();
            if (!At("("))
            {
                var unary = ReadUnary();
                return abs ? Node(GesSchemaExpressionKind.Abs, offset, unary) : Function(function, offset, unary);
            }

            // A parenthesized operand is the whole operand, as in GES, and the function is then the base of a power.
            _position++;
            SkipContinuation();
            var operand = ReadExpression();
            SkipContinuation();
            Expect(')');
            return ReadPower(abs ? Node(GesSchemaExpressionKind.Abs, offset, operand) : Function(function, offset, operand));
        }

        return ReadPower(ReadPostfix());
    }

    // The unary functions of GES: roots, trigonometry, exponential and logarithm, rounding and angle conversions, written as a
    // word or as `√` and `∛`. Returns the ASCII spelling, or null with the position unchanged.
    private string? ReadPrefixFunction()
    {
        if (Consume('\u221A')) return "sqrt";
        if (Consume('\u221B')) return "cbrt";
        foreach (var word in PrefixFunctions)
        {
            if (!AtWord(word)) continue;
            ReadWord();
            if (word != "round") return word;
            SkipSpaces();
            ExpectWord("half");
            SkipSpaces();
            foreach (var mode in new[] { "even", "up", "down" })
                if (AtWord(mode))
                {
                    ReadWord();
                    return "round half " + mode;
                }

            throw Failure("Expected 'even', 'up' or 'down'.");
        }

        return null;
    }

    private static readonly string[] PrefixFunctions =
        ["sqrt", "cbrt", "sin", "cos", "tan", "asin", "acos", "atan", "exp", "ln", "floor", "ceil", "truncate", "round", "rad", "deg"];

    // A power `base ^ exponent`, `base²` or `base³` after its base: the exponent of `^` is a unary expression, so powers
    // associate to the right and `-x ^ 2` negates the power. A base takes at most one power.
    private GesSchemaExpressionSyntax ReadPower(GesSchemaExpressionSyntax value)
    {
        var end = _position;
        SkipSpaces();
        var offset = _position;
        if (Consume('^'))
        {
            SkipContinuation();
            return Power(value, ReadUnary());
        }

        if (Consume('\u00B2') || Consume('\u00B3'))
        {
            var text = _text[offset] == '\u00B2' ? "2" : "3";
            var exponent = GesSchemaLiterals.Read(text, 0, 1)!.Value;
            return Power(value, new GesSchemaExpressionSyntax
            {
                Kind = GesSchemaExpressionKind.Literal,
                Literal = new GesSchemaValueSyntax { Value = exponent.Value, Text = text, Offset = offset },
                Offset = offset
            });
        }

        _position = end;
        return value;
    }

    private GesSchemaExpressionSyntax Power(GesSchemaExpressionSyntax value, GesSchemaExpressionSyntax exponent)
        => Checked(new GesSchemaExpressionSyntax
        {
            Kind = GesSchemaExpressionKind.Binary,
            Operator = GesSchemaBinaryOperator.Power,
            Children = [value, exponent],
            Offset = value.Offset
        });

    private GesSchemaExpressionSyntax Function(string name, int offset, params GesSchemaExpressionSyntax[] arguments)
        => Checked(new GesSchemaExpressionSyntax { Kind = GesSchemaExpressionKind.Function, Name = name, Children = arguments, Offset = offset });

    // Member access `record.field`, the list aggregates `list[:sum]`, `list[:sum item => value]` and `list[:count]`, and the
    // projection `list[:select item => value]`.
    private GesSchemaExpressionSyntax ReadPostfix()
    {
        var value = ReadPrimary();
        while (true)
        {
            if (At(".") && _position + 1 < _text.Length && (GesText.IsAsciiLower(_text[_position + 1]) || _text[_position + 1] == '`'))
            {
                var offset = ++_position;
                var member = At("`") ? ReadQuotedName() : ReadName(GesText.IsFieldName, "field name");
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

            if (selector is not ("sum" or "select")) throw new SyntaxFailure(start, "A computed expression takes the list selectors :sum, :count and :select.");
            if (selector == "sum" && Consume(']'))
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
                Kind = selector == "sum" ? GesSchemaExpressionKind.Sum : GesSchemaExpressionKind.Select,
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

        if (At(":") && _position + 1 < _text.Length && _text[_position + 1] is >= 'A' and <= 'Z') return ReadConstruct();
        if (Consume('['))
        {
            SkipContinuation();
            var items = new List<GesSchemaExpressionSyntax>();
            if (!Consume(']'))
            {
                while (true)
                {
                    items.Add(ReadExpression());
                    SkipContinuation();
                    if (Consume(']')) break;
                    Expect(',');
                    SkipContinuation();
                }
            }

            if (items.Count == 0) throw new SyntaxFailure(offset, "A list in a computed expression holds at least one item.", Code.InvalidExpression);
            return Node(GesSchemaExpressionKind.List, offset, items.ToArray());
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

        if (AtWord("atan2") || AtWord("hypot"))
        {
            var function = ReadWord();
            SkipSpaces();
            Expect('(');
            SkipContinuation();
            var arguments = new List<GesSchemaExpressionSyntax> { ReadExpression() };
            SkipContinuation();
            while (Consume(','))
            {
                SkipContinuation();
                arguments.Add(ReadExpression());
                SkipContinuation();
            }

            Expect(')');
            if (function == "atan2" ? arguments.Count != 2 : arguments.Count is not (2 or 3))
                throw new SyntaxFailure(offset, function == "atan2" ? "atan2 takes two arguments: atan2(y, x)." : "hypot takes two or three arguments.", Code.InvalidExpression);
            return Function(function, offset, arguments.ToArray());
        }

        if (At("`")) return new GesSchemaExpressionSyntax { Kind = GesSchemaExpressionKind.Name, Name = ReadQuotedName(), Offset = offset };
        if (GesText.IsAsciiLower(_text[_position]))
        {
            var name = ReadName(GesText.IsFieldName, "name");
            if (!IsPlainName(name))
                throw new SyntaxFailure(offset, $"'{name}' is a GES keyword or constant; a computed expression names a field or let '{name}' in backticks, as `{name}`.", Code.InvalidExpression);
            return new GesSchemaExpressionSyntax { Kind = GesSchemaExpressionKind.Name, Name = name, Offset = offset };
        }

        if (_text[_position] is >= '0' and <= '9') return new GesSchemaExpressionSyntax { Kind = GesSchemaExpressionKind.Literal, Literal = ReadExpressionLiteral(), Offset = offset };
        throw Failure("Expected an expression.");
    }

    // A number with its suffix. Besides the ends of other literals it stops at an operator; a minus continues only an exponent.
    // A power sign, a `*` or a parenthesized part continues the literal only where it belongs to its unit, as in `16m²`,
    // `5N*m` or `3J/(kg·K)`; otherwise, as in `2²` or `2s²`, the longest literal before it is the base of a power.
    private GesSchemaValueSyntax ReadExpressionLiteral()
    {
        var offset = _position;
        var end = LiteralEnd(offset, expression: true);
        var value = GesSchemaLiterals.Read(_text, offset, end);
        for (var cut = end - 1; value is null && cut > offset; cut--)
            if ((_text[cut] is '^' or '\u00B2' or '\u00B3' or '*' || (_text[cut] == '/' && cut + 1 < end && _text[cut + 1] == '('))
                && GesSchemaLiterals.Read(_text, offset, cut) is { } shorter)
            {
                value = shorter;
                end = cut;
            }

        if (value is null) throw new SyntaxFailure(offset, "Expected an exact finite number with a unit known to literals.");
        _position = end;
        return new GesSchemaValueSyntax { Value = value.Value.Value, Unit = value.Value.Unit, IsPercent = value.Value.IsPercent, Text = _text.Substring(offset, end - offset), Offset = offset };
    }

    // A spatial value built with the constructor of its type, as in a script: `:Vector(x: 1m, y: 2m)`, `:Point(1m, 2m)`,
    // `:Box(lower: a, upper: b)`, `:Disc(radius: r)` or `:Poly(points: [a, b, c])`. Each argument is labeled or positional.
    private GesSchemaExpressionSyntax ReadConstruct()
    {
        var offset = _position++;
        var type = ReadName(name => name.Length > 0 && name[0] is >= 'A' and <= 'Z', "type name");
        if (type is not ("Vector" or "Point" or "Box" or "Disc" or "Poly"))
            throw new SyntaxFailure(offset, $"A computed expression constructs :Vector, :Point, :Box, :Disc or :Poly, not :{type}.", Code.InvalidExpression);
        SkipSpaces();
        Expect('(');
        SkipContinuation();
        var arguments = new List<GesSchemaExpressionSyntax>();
        var labels = new List<string>();
        if (!Consume(')'))
        {
            while (true)
            {
                var start = _position;
                var label = "";
                if (_position < _text.Length && GesText.IsAsciiLower(_text[_position]))
                {
                    var word = ReadWord();
                    SkipSpaces();
                    if (Consume(':')) label = word;
                    else _position = start;
                }

                SkipContinuation();
                arguments.Add(ReadExpression());
                labels.Add(label);
                SkipContinuation();
                if (Consume(')')) break;
                Expect(',');
                SkipContinuation();
            }
        }

        var depth = 0;
        foreach (var argument in arguments) depth = System.Math.Max(depth, argument.Depth);
        return Checked(new GesSchemaExpressionSyntax
        {
            Kind = GesSchemaExpressionKind.Construct,
            Name = type,
            Children = arguments.ToArray(),
            Labels = labels.ToArray(),
            Depth = depth + 1,
            Offset = offset
        });
    }

    // A name in backticks, which may be a word that GES reserves, such as `length` or `e`: a field or let of that name.
    private string ReadQuotedName()
    {
        var start = _position++;
        var close = _position;
        while (close < _text.Length && _text[close] is not ('`' or '\r' or '\n')) close++;
        var name = _text.Substring(_position, close - _position);
        if (close >= _text.Length || _text[close] != '`' || !GesText.IsFieldName(name)) throw new SyntaxFailure(start, "Expected a name between two backticks, such as `length`.");
        _position = close + 1;
        return name;
    }

    // Whether GES reads a name as a plain name, rather than as a keyword or constant that only backticks make a name.
    internal static bool IsPlainName(string name)
    {
        var tokens = GesLexer.Tokenize(name);
        return tokens.Count > 0 && tokens[0].Kind == GesTokenKind.Identifier && tokens[0].Text == name;
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
            Labels = node.Labels,
            Depth = depth + 1,
            Offset = node.Offset
        };
    }
}
