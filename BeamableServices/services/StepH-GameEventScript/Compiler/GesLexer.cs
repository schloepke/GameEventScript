using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using StepH.GameEventScript.Api;

namespace StepH.GameEventScript.Compiler;

internal enum GesTokenKind
{
    EndOfFile,
    Illegal,
    NewLine,
    Message,
    Identifier,
    Tag,
    Decimal,
    Percentage,
    UnitDecimal,
    Text,
    True,
    False,
    Module,
    Record,
    Rule,
    Select,
    Means,
    On,
    Publish,
    Let,
    As,
    Be,
    When,
    Otherwise,
    Has,
    Empty,
    If,
    Else,
    For,
    In,
    Starts,
    Ends,
    With,
    Is,
    To,
    SelectorAny,
    SelectorAll,
    SelectorFilter,
    SelectorHas,
    SelectorTake,
    SelectorDrop,
    SelectorCount,
    SelectorChoose,
    SelectorDraw,
    SelectorShuffle,
    SelectorReverse,
    SelectorSum,
    SelectorAverage,
    SelectorSelect,
    SelectorContains,
    SelectorSort,
    DiceSeparator,
    Dot,
    Comma,
    Semicolon,
    LeftParen,
    RightParen,
    LeftBrace,
    RightBrace,
    LeftBracket,
    RightBracket,
    Colon,
    Underscore,
    Arrow,
    Or,
    Xor,
    And,
    Equal,
    NotEqual,
    Less,
    Greater,
    LessOrEqual,
    GreaterOrEqual,
    Plus,
    Minus,
    Multiply,
    Divide,
    IntegerDivide,
    Modulo,
    Remainder,
    Not
}

internal readonly record struct GesToken(GesTokenKind Kind, string Text, int Line, int Column, int EndLine, int EndColumn, string UnitName = "")
{
    public decimal DecimalValue => decimal.Parse(Text, CultureInfo.InvariantCulture);

    public bool TryGetIntegerValue(out long value)
    {
        if (Kind == GesTokenKind.Decimal && Text.IndexOf('.') < 0 && long.TryParse(Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value)) return true;
        value = 0;
        return false;
    }

    public override string ToString() => $"{Kind}('{Text}', {Line}:{Column}-{EndLine}:{EndColumn})";
}

internal sealed class GesLexer
{
    private readonly string _input;
    private readonly int _length;
    private int _index;
    private int _line = 1;
    private int _column = 1;

    public GesLexer(string input, GameEventScriptCompileOptions? options = null)
    {
        _input = input ?? throw new ArgumentNullException(nameof(input));
        _length = _input.Length;
        _ = options ?? new GameEventScriptCompileOptions();
    }

    public IEnumerable<GesToken> Tokenize()
    {
        while (true)
        {
            SkipTriviaExceptNewLine();

            if (IsAtEnd)
            {
                yield return new GesToken(GesTokenKind.EndOfFile, string.Empty, _line, _column, _line, _column);
                yield break;
            }

            switch (Current)
            {
                case '\r':
                {
                    var newlineLine = _line;
                    var newlineColumn = _column;
                    Advance();
                    if (!IsAtEnd && Current == '\n')
                    {
                        Advance();
                    }

                    yield return new GesToken(GesTokenKind.NewLine, "\\n", newlineLine, newlineColumn, _line, _column);
                    continue;
                }
                case '\n':
                {
                    var newlineLine = _line;
                    var newlineColumn = _column;
                    Advance();
                    yield return new GesToken(GesTokenKind.NewLine, "\\n", newlineLine, newlineColumn, _line, _column);
                    continue;
                }
            }

            var startLine = _line;
            var startColumn = _column;
            var ch = Current;

            if (char.IsLower(ch) || char.IsUpper(ch))
            {
                yield return ReadWordLikeToken(startLine, startColumn);
                continue;
            }

            if (char.IsDigit(ch))
            {
                yield return ReadNumberLikeToken(startLine, startColumn);
                continue;
            }

            switch (ch)
            {
                case '\'':
                    yield return ReadTextToken(startLine, startColumn);
                    continue;
                case ':' when char.IsLower(Peek()):
                    yield return ReadSelectorToken(startLine, startColumn);
                    continue;
                default:
                    yield return ReadOperatorToken(startLine, startColumn);
                    continue;
            }
        }
    }

    private bool IsAtEnd => _index >= _length;

    private char Current => _input[_index];

    private char Peek(int offset = 1)
    {
        var idx = _index + offset;
        return idx >= _length ? '\0' : _input[idx];
    }

    private void Advance()
    {
        if (IsAtEnd) return;
        if (Current == '\n')
        {
            _line++;
            _column = 1;
        }
        else
        {
            _column++;
        }

        _index++;
    }

    private string ReadWhile(Func<char, bool> predicate)
    {
        var start = _index;

        while (!IsAtEnd && predicate(Current))
        {
            Advance();
        }

        return _input[start.._index];
    }

    private GesToken CreateToken(GesTokenKind kind, string text, int startLine, int startColumn)
        => new(kind, text, startLine, startColumn, _line, _column);

    private GesToken CreateUnitDecimalToken(string text, string unitName, int startLine, int startColumn)
        => new(GesTokenKind.UnitDecimal, text, startLine, startColumn, _line, _column, unitName);

    private static GesToken CreateWordToken(string text, int line, int column, int endLine, int endColumn)
    {
        return text switch
        {
            "on" => new GesToken(GesTokenKind.On, text, line, column, endLine, endColumn),
            "record" => new GesToken(GesTokenKind.Record, text, line, column, endLine, endColumn),
            "rule" => new GesToken(GesTokenKind.Rule, text, line, column, endLine, endColumn),
            "select" => new GesToken(GesTokenKind.Select, text, line, column, endLine, endColumn),
            "means" => new GesToken(GesTokenKind.Means, text, line, column, endLine, endColumn),
            "publish" => new GesToken(GesTokenKind.Publish, text, line, column, endLine, endColumn),
            "let" => new GesToken(GesTokenKind.Let, text, line, column, endLine, endColumn),
            "as" => new GesToken(GesTokenKind.As, text, line, column, endLine, endColumn),
            "be" => new GesToken(GesTokenKind.Be, text, line, column, endLine, endColumn),
            "when" => new GesToken(GesTokenKind.When, text, line, column, endLine, endColumn),
            "otherwise" => new GesToken(GesTokenKind.Otherwise, text, line, column, endLine, endColumn),
            "has" => new GesToken(GesTokenKind.Has, text, line, column, endLine, endColumn),
            "empty" => new GesToken(GesTokenKind.Empty, text, line, column, endLine, endColumn),
            "if" => new GesToken(GesTokenKind.If, text, line, column, endLine, endColumn),
            "else" => new GesToken(GesTokenKind.Else, text, line, column, endLine, endColumn),
            "for" => new GesToken(GesTokenKind.For, text, line, column, endLine, endColumn),
            "in" => new GesToken(GesTokenKind.In, text, line, column, endLine, endColumn),
            "starts" => new GesToken(GesTokenKind.Starts, text, line, column, endLine, endColumn),
            "ends" => new GesToken(GesTokenKind.Ends, text, line, column, endLine, endColumn),
            "with" => new GesToken(GesTokenKind.With, text, line, column, endLine, endColumn),
            "is" => new GesToken(GesTokenKind.Is, text, line, column, endLine, endColumn),
            "or" => new GesToken(GesTokenKind.Or, text, line, column, endLine, endColumn),
            "xor" => new GesToken(GesTokenKind.Xor, text, line, column, endLine, endColumn),
            "and" => new GesToken(GesTokenKind.And, text, line, column, endLine, endColumn),
            "div" => new GesToken(GesTokenKind.IntegerDivide, text, line, column, endLine, endColumn),
            "mod" => new GesToken(GesTokenKind.Modulo, text, line, column, endLine, endColumn),
            "rem" => new GesToken(GesTokenKind.Remainder, text, line, column, endLine, endColumn),
            "not" => new GesToken(GesTokenKind.Not, text, line, column, endLine, endColumn),
            "to" => new GesToken(GesTokenKind.To, text, line, column, endLine, endColumn),
            "true" => new GesToken(GesTokenKind.True, text, line, column, endLine, endColumn),
            "false" => new GesToken(GesTokenKind.False, text, line, column, endLine, endColumn),
            "module" => new GesToken(GesTokenKind.Module, text, line, column, endLine, endColumn),
            _ when char.IsUpper(text[0]) => new GesToken(GesTokenKind.Message, text, line, column, endLine, endColumn),
            _ => new GesToken(GesTokenKind.Identifier, text, line, column, endLine, endColumn)
        };
    }

    private GesToken ReadWordLikeToken(int line, int column)
    {
        var start = _index;
        var word = ReadWhile(char.IsLetterOrDigit);
        if (IsAtEnd || (!StartsAttachedIllegalOperatorSequence() && IsValidWordBoundary(Current))) return CreateWordToken(word, line, column, _line, _column);
        while (!IsAtEnd && !char.IsWhiteSpace(Current))
        {
            Advance();
        }

        return CreateToken(GesTokenKind.Illegal, _input[start.._index], line, column);
    }

    private GesToken ReadSelectorToken(int line, int column)
    {
        var start = _index;
        Advance();
        var selector = ReadWhile(char.IsLetterOrDigit);

        if (IsAtEnd || (!StartsAttachedIllegalOperatorSequence() && IsValidWordBoundary(Current)))
            return selector switch
            {
                "any" => CreateToken(GesTokenKind.SelectorAny, $":{selector}", line, column),
                "all" => CreateToken(GesTokenKind.SelectorAll, $":{selector}", line, column),
                "filter" => CreateToken(GesTokenKind.SelectorFilter, $":{selector}", line, column),
                "has" => CreateToken(GesTokenKind.SelectorHas, $":{selector}", line, column),
                "take" => CreateToken(GesTokenKind.SelectorTake, $":{selector}", line, column),
                "drop" => CreateToken(GesTokenKind.SelectorDrop, $":{selector}", line, column),
                "count" => CreateToken(GesTokenKind.SelectorCount, $":{selector}", line, column),
                "choose" => CreateToken(GesTokenKind.SelectorChoose, $":{selector}", line, column),
                "draw" => CreateToken(GesTokenKind.SelectorDraw, $":{selector}", line, column),
                "shuffle" => CreateToken(GesTokenKind.SelectorShuffle, $":{selector}", line, column),
                "reverse" => CreateToken(GesTokenKind.SelectorReverse, $":{selector}", line, column),
                "sum" => CreateToken(GesTokenKind.SelectorSum, $":{selector}", line, column),
                "average" => CreateToken(GesTokenKind.SelectorAverage, $":{selector}", line, column),
                "select" => CreateToken(GesTokenKind.SelectorSelect, $":{selector}", line, column),
                "contains" => CreateToken(GesTokenKind.SelectorContains, $":{selector}", line, column),
                "sort" => CreateToken(GesTokenKind.SelectorSort, $":{selector}", line, column),
                _ => CreateToken(GesTokenKind.Tag, $":{selector}", line, column)
            };
        while (!IsAtEnd && !char.IsWhiteSpace(Current))
        {
            Advance();
        }

        return CreateToken(GesTokenKind.Illegal, _input[start.._index], line, column);
    }

    private GesToken ReadNumberLikeToken(int line, int column)
    {
        var start = _index;
        ReadWhile(char.IsDigit);
        if (!IsAtEnd && Current == '.' && char.IsDigit(Peek()))
        {
            Advance();
            ReadWhile(char.IsDigit);
        }

        var text = _input[start.._index];
        if (!IsAtEnd && Current == '%')
        {
            Advance();
            text = _input[start..(_index - 1)];
            if (IsAtEnd || IsValidNumberBoundary(Current)) return CreateToken(GesTokenKind.Percentage, text, line, column);
            while (!IsAtEnd && !char.IsWhiteSpace(Current))
            {
                Advance();
            }

            return CreateToken(GesTokenKind.Illegal, _input[start.._index], line, column);
        }

        if (!IsAtEnd && Current == '\u00B0')
        {
            Advance();
            text = _input[start..(_index - 1)];
            if (IsAtEnd || IsValidNumberBoundary(Current)) return CreateUnitDecimalToken(text, "degree", line, column);
            while (!IsAtEnd && !char.IsWhiteSpace(Current))
            {
                Advance();
            }

            return CreateToken(GesTokenKind.Illegal, _input[start.._index], line, column);
        }

        if (!IsAtEnd && Current is 'm' or 's')
        {
            var unitName = Current == 'm' ? "meter" : "second";
            Advance();
            text = _input[start..(_index - 1)];
            if (IsAtEnd || IsValidNumberBoundary(Current)) return CreateUnitDecimalToken(text, unitName, line, column);
            while (!IsAtEnd && !char.IsWhiteSpace(Current))
            {
                Advance();
            }

            return CreateToken(GesTokenKind.Illegal, _input[start.._index], line, column);
        }

        if (IsAtEnd || IsValidNumberBoundary(Current)) return CreateToken(GesTokenKind.Decimal, text, line, column);
        while (!IsAtEnd && !char.IsWhiteSpace(Current))
        {
            Advance();
        }

        return CreateToken(GesTokenKind.Illegal, _input[start.._index], line, column);
    }

    private GesToken ReadTextToken(int line, int column)
    {
        var start = _index;
        Advance();
        var builder = new StringBuilder();
        while (!IsAtEnd)
        {
            if (Current == '\'')
            {
                if (Peek() == '\'')
                {
                    builder.Append('\'');
                    Advance();
                    Advance();
                    continue;
                }

                Advance();
                return CreateToken(GesTokenKind.Text, builder.ToString(), line, column);
            }

            builder.Append(Current);
            Advance();
        }

        return CreateToken(GesTokenKind.Illegal, _input[start.._index], line, column);
    }

    private GesToken ReadOperatorToken(int line, int column)
    {
        var ch = Current;
        var next = Peek();
        Advance();
        switch (ch)
        {
            case '<' when next == '>':
                Advance();
                return CreateToken(GesTokenKind.NotEqual, "<>", line, column);
            case '<' when next == '=':
                Advance();
                return CreateToken(GesTokenKind.LessOrEqual, "<=", line, column);
            case '>' when next == '=':
                Advance();
                return CreateToken(GesTokenKind.GreaterOrEqual, ">=", line, column);
            case '-' when next == '>':
                Advance();
                return CreateToken(GesTokenKind.Arrow, "->", line, column);
            default:
                return ch switch
                {
                    '.' => CreateToken(GesTokenKind.Dot, ".", line, column),
                    ',' => CreateToken(GesTokenKind.Comma, ",", line, column),
                    ';' => CreateToken(GesTokenKind.Semicolon, ";", line, column),
                    '(' => CreateToken(GesTokenKind.LeftParen, "(", line, column),
                    ')' => CreateToken(GesTokenKind.RightParen, ")", line, column),
                    '{' => CreateToken(GesTokenKind.LeftBrace, "{", line, column),
                    '}' => CreateToken(GesTokenKind.RightBrace, "}", line, column),
                    '[' => CreateToken(GesTokenKind.LeftBracket, "[", line, column),
                    ']' => CreateToken(GesTokenKind.RightBracket, "]", line, column),
                    ':' => CreateToken(GesTokenKind.Colon, ":", line, column),
                    '_' => CreateToken(GesTokenKind.Underscore, "_", line, column),
                    '=' => CreateToken(GesTokenKind.Equal, "=", line, column),
                    '<' => CreateToken(GesTokenKind.Less, "<", line, column),
                    '>' => CreateToken(GesTokenKind.Greater, ">", line, column),
                    '|' => CreateToken(GesTokenKind.Or, "|", line, column),
                    '^' => CreateToken(GesTokenKind.Xor, "^", line, column),
                    '&' => CreateToken(GesTokenKind.And, "&", line, column),
                    '+' => CreateToken(GesTokenKind.Plus, "+", line, column),
                    '-' => CreateToken(GesTokenKind.Minus, "-", line, column),
                    '*' => CreateToken(GesTokenKind.Multiply, "*", line, column),
                    '/' => CreateToken(GesTokenKind.Divide, "/", line, column),
                    '%' => CreateToken(GesTokenKind.Illegal, "%", line, column),
                    '!' => CreateToken(GesTokenKind.Not, "!", line, column),
                    '~' => CreateToken(GesTokenKind.Not, "~", line, column),
                    _ => CreateToken(GesTokenKind.Illegal, ch.ToString(), line, column)
                };
        }
    }

    private void SkipTriviaExceptNewLine()
    {
        while (!IsAtEnd)
        {
            if (char.IsWhiteSpace(Current) && Current is not '\n' and not '\r')
            {
                Advance();
                continue;
            }

            if (Current == '/' && Peek() == '/')
            {
                Advance();
                Advance();
                while (!IsAtEnd && Current is not '\n' and not '\r')
                {
                    Advance();
                }

                continue;
            }

            break;
        }
    }

    private static bool IsValidWordBoundary(char ch) => char.IsWhiteSpace(ch) || IsStructuralBoundary(ch);

    private bool IsValidNumberBoundary(char ch) => char.IsWhiteSpace(ch) || IsStructuralBoundary(ch) || (ch == 'd' && char.IsDigit(Peek()));

    private static bool IsStructuralBoundary(char ch)
        => ch is '(' or ')' or '{' or '}' or '[' or ']' or ',' or ';' or '.' or ':' or '+' or '-' or '*' or '/' or '!' or '~' or '&' or '|' or '^' or '=' or '<' or '>';

    private bool StartsAttachedIllegalOperatorSequence()
    {
        if (IsAtEnd)
        {
            return false;
        }

        return Current switch
        {
            '&' when Peek() == '&' => true,
            '|' when Peek() == '|' => true,
            '^' when Peek() is '&' or '|' or '^' => true,
            _ => false
        };
    }
}
