#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace StepH.GameEventScript.Compiler;

public enum GseTokenKind
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

public readonly record struct GseToken(
    GseTokenKind Kind,
    string Text,
    int Line,
    int Column,
    int EndLine,
    int EndColumn,
    string UnitName = "")
{
    public GseToken(GseTokenKind kind, string text, int line, int column)
        : this(kind, text, line, column, line, column)
    {
    }

    public decimal DecimalValue => decimal.Parse(Text, CultureInfo.InvariantCulture);

    public bool TryGetIntegerValue(out long value)
    {
        if (Kind == GseTokenKind.Decimal &&
            Text.IndexOf('.') < 0 &&
            long.TryParse(Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
        {
            return true;
        }

        value = 0;
        return false;
    }
    
    public override string ToString() => $"{Kind}('{Text}', {Line}:{Column}-{EndLine}:{EndColumn})";
}

public sealed class GseLexer
{
    private readonly string _input;
    private readonly int _length;
    private int _index;
    private int _line = 1;
    private int _column = 1;

    public GseLexer(string input)
    {
        _input = input ?? throw new ArgumentNullException(nameof(input));
        _length = _input.Length;
    }

    public IEnumerable<GseToken> Tokenize()
    {
        while (true)
        {
            SkipTriviaExceptNewLine();

            if (IsAtEnd)
            {
                yield return new GseToken(GseTokenKind.EndOfFile, string.Empty, _line, _column, _line, _column);
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

                    yield return new GseToken(GseTokenKind.NewLine, "\\n", newlineLine, newlineColumn, _line, _column);
                    continue;
                }
                case '\n':
                {
                    var newlineLine = _line;
                    var newlineColumn = _column;
                    Advance();
                    yield return new GseToken(GseTokenKind.NewLine, "\\n", newlineLine, newlineColumn, _line, _column);
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

    private GseToken CreateToken(GseTokenKind kind, string text, int startLine, int startColumn)
        => new(kind, text, startLine, startColumn, _line, _column);

    private GseToken CreateUnitDecimalToken(string text, string unitName, int startLine, int startColumn)
        => new(GseTokenKind.UnitDecimal, text, startLine, startColumn, _line, _column, unitName);

    private static GseToken CreateWordToken(string text, int line, int column, int endLine, int endColumn)
    {
        return text switch
        {
            "on" => new GseToken(GseTokenKind.On, text, line, column, endLine, endColumn),
            "record" => new GseToken(GseTokenKind.Record, text, line, column, endLine, endColumn),
            "rule" => new GseToken(GseTokenKind.Rule, text, line, column, endLine, endColumn),
            "select" => new GseToken(GseTokenKind.Select, text, line, column, endLine, endColumn),
            "means" => new GseToken(GseTokenKind.Means, text, line, column, endLine, endColumn),
            "publish" => new GseToken(GseTokenKind.Publish, text, line, column, endLine, endColumn),
            "let" => new GseToken(GseTokenKind.Let, text, line, column, endLine, endColumn),
            "as" => new GseToken(GseTokenKind.As, text, line, column, endLine, endColumn),
            "be" => new GseToken(GseTokenKind.Be, text, line, column, endLine, endColumn),
            "when" => new GseToken(GseTokenKind.When, text, line, column, endLine, endColumn),
            "otherwise" => new GseToken(GseTokenKind.Otherwise, text, line, column, endLine, endColumn),
            "has" => new GseToken(GseTokenKind.Has, text, line, column, endLine, endColumn),
            "empty" => new GseToken(GseTokenKind.Empty, text, line, column, endLine, endColumn),
            "if" => new GseToken(GseTokenKind.If, text, line, column, endLine, endColumn),
            "else" => new GseToken(GseTokenKind.Else, text, line, column, endLine, endColumn),
            "for" => new GseToken(GseTokenKind.For, text, line, column, endLine, endColumn),
            "in" => new GseToken(GseTokenKind.In, text, line, column, endLine, endColumn),
            "starts" => new GseToken(GseTokenKind.Starts, text, line, column, endLine, endColumn),
            "ends" => new GseToken(GseTokenKind.Ends, text, line, column, endLine, endColumn),
            "with" => new GseToken(GseTokenKind.With, text, line, column, endLine, endColumn),
            "is" => new GseToken(GseTokenKind.Is, text, line, column, endLine, endColumn),
            "or" => new GseToken(GseTokenKind.Or, text, line, column, endLine, endColumn),
            "xor" => new GseToken(GseTokenKind.Xor, text, line, column, endLine, endColumn),
            "and" => new GseToken(GseTokenKind.And, text, line, column, endLine, endColumn),
            "div" => new GseToken(GseTokenKind.IntegerDivide, text, line, column, endLine, endColumn),
            "mod" => new GseToken(GseTokenKind.Modulo, text, line, column, endLine, endColumn),
            "rem" => new GseToken(GseTokenKind.Remainder, text, line, column, endLine, endColumn),
            "not" => new GseToken(GseTokenKind.Not, text, line, column, endLine, endColumn),
            "to" => new GseToken(GseTokenKind.To, text, line, column, endLine, endColumn),
            "true" => new GseToken(GseTokenKind.True, text, line, column, endLine, endColumn),
            "false" => new GseToken(GseTokenKind.False, text, line, column, endLine, endColumn),
            "module" => new GseToken(GseTokenKind.Module, text, line, column, endLine, endColumn),
            _ when char.IsUpper(text[0]) => new GseToken(GseTokenKind.Message, text, line, column, endLine, endColumn),
            _ => new GseToken(GseTokenKind.Identifier, text, line, column, endLine, endColumn)
        };
    }

    private GseToken ReadWordLikeToken(int line, int column)
    {
        var start = _index;
        var word = ReadWhile(char.IsLetterOrDigit);
        if (!IsAtEnd && (StartsAttachedIllegalOperatorSequence() || !IsValidWordBoundary(Current)))
        {
            while (!IsAtEnd && !char.IsWhiteSpace(Current))
            {
                Advance();
            }

            return CreateToken(GseTokenKind.Illegal, _input[start.._index], line, column);
        }

        return CreateWordToken(word, line, column, _line, _column);
    }

    private GseToken ReadSelectorToken(int line, int column)
    {
        var start = _index;
        Advance();
        var selector = ReadWhile(char.IsLetterOrDigit);

        if (!IsAtEnd && (StartsAttachedIllegalOperatorSequence() || !IsValidWordBoundary(Current)))
        {
            while (!IsAtEnd && !char.IsWhiteSpace(Current))
            {
                Advance();
            }

            return CreateToken(GseTokenKind.Illegal, _input[start.._index], line, column);
        }

        return selector switch
        {
            "any" => CreateToken(GseTokenKind.SelectorAny, $":{selector}", line, column),
            "all" => CreateToken(GseTokenKind.SelectorAll, $":{selector}", line, column),
            "filter" => CreateToken(GseTokenKind.SelectorFilter, $":{selector}", line, column),
            "has" => CreateToken(GseTokenKind.SelectorHas, $":{selector}", line, column),
            "take" => CreateToken(GseTokenKind.SelectorTake, $":{selector}", line, column),
            "drop" => CreateToken(GseTokenKind.SelectorDrop, $":{selector}", line, column),
            "count" => CreateToken(GseTokenKind.SelectorCount, $":{selector}", line, column),
            "choose" => CreateToken(GseTokenKind.SelectorChoose, $":{selector}", line, column),
            "draw" => CreateToken(GseTokenKind.SelectorDraw, $":{selector}", line, column),
            "shuffle" => CreateToken(GseTokenKind.SelectorShuffle, $":{selector}", line, column),
            "reverse" => CreateToken(GseTokenKind.SelectorReverse, $":{selector}", line, column),
            "sum" => CreateToken(GseTokenKind.SelectorSum, $":{selector}", line, column),
            "average" => CreateToken(GseTokenKind.SelectorAverage, $":{selector}", line, column),
            "select" => CreateToken(GseTokenKind.SelectorSelect, $":{selector}", line, column),
            "contains" => CreateToken(GseTokenKind.SelectorContains, $":{selector}", line, column),
            "sort" => CreateToken(GseTokenKind.SelectorSort, $":{selector}", line, column),
            _ => CreateToken(GseTokenKind.Tag, $":{selector}", line, column)
        };
    }

    private GseToken ReadNumberLikeToken(int line, int column)
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
            if (!IsAtEnd && !IsValidNumberBoundary(Current))
            {
                while (!IsAtEnd && !char.IsWhiteSpace(Current))
                {
                    Advance();
                }

                return CreateToken(GseTokenKind.Illegal, _input[start.._index], line, column);
            }

            return CreateToken(GseTokenKind.Percentage, text, line, column);
        }

        if (!IsAtEnd && Current == '\u00B0')
        {
            Advance();
            text = _input[start..(_index - 1)];
            if (!IsAtEnd && !IsValidNumberBoundary(Current))
            {
                while (!IsAtEnd && !char.IsWhiteSpace(Current))
                {
                    Advance();
                }

                return CreateToken(GseTokenKind.Illegal, _input[start.._index], line, column);
            }

            return CreateUnitDecimalToken(text, "degree", line, column);
        }

        if (!IsAtEnd && Current is 'm' or 's')
        {
            var unitName = Current == 'm' ? "meter" : "second";
            Advance();
            text = _input[start..(_index - 1)];
            if (!IsAtEnd && !IsValidNumberBoundary(Current))
            {
                while (!IsAtEnd && !char.IsWhiteSpace(Current))
                {
                    Advance();
                }

                return CreateToken(GseTokenKind.Illegal, _input[start.._index], line, column);
            }

            return CreateUnitDecimalToken(text, unitName, line, column);
        }

        if (!IsAtEnd && !IsValidNumberBoundary(Current))
        {
            while (!IsAtEnd && !char.IsWhiteSpace(Current))
            {
                Advance();
            }

            return CreateToken(GseTokenKind.Illegal, _input[start.._index], line, column);
        }

        return CreateToken(GseTokenKind.Decimal, text, line, column);
    }

    private GseToken ReadTextToken(int line, int column)
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
                return CreateToken(GseTokenKind.Text, builder.ToString(), line, column);
            }

            builder.Append(Current);
            Advance();
        }

        return CreateToken(GseTokenKind.Illegal, _input[start.._index], line, column);
    }

    private GseToken ReadOperatorToken(int line, int column)
    {
        var ch = Current;
        var next = Peek();
        Advance();
        switch (ch)
        {
            case '<' when next == '>':
                Advance();
                return CreateToken(GseTokenKind.NotEqual, "<>", line, column);
            case '<' when next == '=':
                Advance();
                return CreateToken(GseTokenKind.LessOrEqual, "<=", line, column);
            case '>' when next == '=':
                Advance();
                return CreateToken(GseTokenKind.GreaterOrEqual, ">=", line, column);
            case '-' when next == '>':
                Advance();
                return CreateToken(GseTokenKind.Arrow, "->", line, column);
            default:
                return ch switch
                {
                    '.' => CreateToken(GseTokenKind.Dot, ".", line, column),
                    ',' => CreateToken(GseTokenKind.Comma, ",", line, column),
                    ';' => CreateToken(GseTokenKind.Semicolon, ";", line, column),
                    '(' => CreateToken(GseTokenKind.LeftParen, "(", line, column),
                    ')' => CreateToken(GseTokenKind.RightParen, ")", line, column),
                    '{' => CreateToken(GseTokenKind.LeftBrace, "{", line, column),
                    '}' => CreateToken(GseTokenKind.RightBrace, "}", line, column),
                    '[' => CreateToken(GseTokenKind.LeftBracket, "[", line, column),
                    ']' => CreateToken(GseTokenKind.RightBracket, "]", line, column),
                    ':' => CreateToken(GseTokenKind.Colon, ":", line, column),
                    '_' => CreateToken(GseTokenKind.Underscore, "_", line, column),
                    '=' => CreateToken(GseTokenKind.Equal, "=", line, column),
                    '<' => CreateToken(GseTokenKind.Less, "<", line, column),
                    '>' => CreateToken(GseTokenKind.Greater, ">", line, column),
                    '|' => CreateToken(GseTokenKind.Or, "|", line, column),
                    '^' => CreateToken(GseTokenKind.Xor, "^", line, column),
                    '&' => CreateToken(GseTokenKind.And, "&", line, column),
                    '+' => CreateToken(GseTokenKind.Plus, "+", line, column),
                    '-' => CreateToken(GseTokenKind.Minus, "-", line, column),
                    '*' => CreateToken(GseTokenKind.Multiply, "*", line, column),
                    '/' => CreateToken(GseTokenKind.Divide, "/", line, column),
                    '%' => CreateToken(GseTokenKind.Illegal, "%", line, column),
                    '!' => CreateToken(GseTokenKind.Not, "!", line, column),
                    '~' => CreateToken(GseTokenKind.Not, "~", line, column),
                    _ => CreateToken(GseTokenKind.Illegal, ch.ToString(), line, column)
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

    private bool IsValidWordBoundary(char ch)
        => char.IsWhiteSpace(ch) || IsStructuralBoundary(ch);

    private bool IsValidNumberBoundary(char ch)
        => char.IsWhiteSpace(ch) ||
           IsStructuralBoundary(ch) ||
           (ch == 'd' && char.IsDigit(Peek()));

    private static bool IsStructuralBoundary(char ch)
        => ch is
            '(' or ')' or '{' or '}' or '[' or ']' or
            ',' or ';' or '.' or ':' or
            '+' or '-' or '*' or '/' or
            '!' or '~' or '&' or '|' or '^' or
            '=' or '<' or '>';

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
