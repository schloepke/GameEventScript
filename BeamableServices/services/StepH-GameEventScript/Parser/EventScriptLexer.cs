#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace StepH.GameEventScript.Parser;

public enum EventScriptTokenKind
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

public readonly record struct EventScriptToken(
    EventScriptTokenKind Kind,
    string Text,
    int Line,
    int Column,
    int EndLine,
    int EndColumn,
    string UnitName = "")
{
    public EventScriptToken(EventScriptTokenKind kind, string text, int line, int column)
        : this(kind, text, line, column, line, column)
    {
    }

    public decimal DecimalValue => decimal.Parse(Text, CultureInfo.InvariantCulture);

    public bool TryGetIntegerValue(out long value)
    {
        if (Kind == EventScriptTokenKind.Decimal &&
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

public sealed class EventScriptLexer
{
    private readonly string _input;
    private readonly int _length;
    private int _index;
    private int _line = 1;
    private int _column = 1;

    public EventScriptLexer(string input)
    {
        _input = input ?? throw new ArgumentNullException(nameof(input));
        _length = _input.Length;
    }

    public IEnumerable<EventScriptToken> Tokenize()
    {
        while (true)
        {
            SkipTriviaExceptNewLine();

            if (IsAtEnd)
            {
                yield return new EventScriptToken(EventScriptTokenKind.EndOfFile, string.Empty, _line, _column, _line, _column);
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

                    yield return new EventScriptToken(EventScriptTokenKind.NewLine, "\\n", newlineLine, newlineColumn, _line, _column);
                    continue;
                }
                case '\n':
                {
                    var newlineLine = _line;
                    var newlineColumn = _column;
                    Advance();
                    yield return new EventScriptToken(EventScriptTokenKind.NewLine, "\\n", newlineLine, newlineColumn, _line, _column);
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

    private EventScriptToken CreateToken(EventScriptTokenKind kind, string text, int startLine, int startColumn)
        => new(kind, text, startLine, startColumn, _line, _column);

    private EventScriptToken CreateUnitDecimalToken(string text, string unitName, int startLine, int startColumn)
        => new(EventScriptTokenKind.UnitDecimal, text, startLine, startColumn, _line, _column, unitName);

    private static EventScriptToken CreateWordToken(string text, int line, int column, int endLine, int endColumn)
    {
        return text switch
        {
            "on" => new EventScriptToken(EventScriptTokenKind.On, text, line, column, endLine, endColumn),
            "record" => new EventScriptToken(EventScriptTokenKind.Record, text, line, column, endLine, endColumn),
            "rule" => new EventScriptToken(EventScriptTokenKind.Rule, text, line, column, endLine, endColumn),
            "select" => new EventScriptToken(EventScriptTokenKind.Select, text, line, column, endLine, endColumn),
            "means" => new EventScriptToken(EventScriptTokenKind.Means, text, line, column, endLine, endColumn),
            "publish" => new EventScriptToken(EventScriptTokenKind.Publish, text, line, column, endLine, endColumn),
            "let" => new EventScriptToken(EventScriptTokenKind.Let, text, line, column, endLine, endColumn),
            "as" => new EventScriptToken(EventScriptTokenKind.As, text, line, column, endLine, endColumn),
            "be" => new EventScriptToken(EventScriptTokenKind.Be, text, line, column, endLine, endColumn),
            "when" => new EventScriptToken(EventScriptTokenKind.When, text, line, column, endLine, endColumn),
            "otherwise" => new EventScriptToken(EventScriptTokenKind.Otherwise, text, line, column, endLine, endColumn),
            "has" => new EventScriptToken(EventScriptTokenKind.Has, text, line, column, endLine, endColumn),
            "empty" => new EventScriptToken(EventScriptTokenKind.Empty, text, line, column, endLine, endColumn),
            "if" => new EventScriptToken(EventScriptTokenKind.If, text, line, column, endLine, endColumn),
            "else" => new EventScriptToken(EventScriptTokenKind.Else, text, line, column, endLine, endColumn),
            "for" => new EventScriptToken(EventScriptTokenKind.For, text, line, column, endLine, endColumn),
            "in" => new EventScriptToken(EventScriptTokenKind.In, text, line, column, endLine, endColumn),
            "starts" => new EventScriptToken(EventScriptTokenKind.Starts, text, line, column, endLine, endColumn),
            "ends" => new EventScriptToken(EventScriptTokenKind.Ends, text, line, column, endLine, endColumn),
            "with" => new EventScriptToken(EventScriptTokenKind.With, text, line, column, endLine, endColumn),
            "is" => new EventScriptToken(EventScriptTokenKind.Is, text, line, column, endLine, endColumn),
            "or" => new EventScriptToken(EventScriptTokenKind.Or, text, line, column, endLine, endColumn),
            "xor" => new EventScriptToken(EventScriptTokenKind.Xor, text, line, column, endLine, endColumn),
            "and" => new EventScriptToken(EventScriptTokenKind.And, text, line, column, endLine, endColumn),
            "div" => new EventScriptToken(EventScriptTokenKind.IntegerDivide, text, line, column, endLine, endColumn),
            "mod" => new EventScriptToken(EventScriptTokenKind.Modulo, text, line, column, endLine, endColumn),
            "rem" => new EventScriptToken(EventScriptTokenKind.Remainder, text, line, column, endLine, endColumn),
            "not" => new EventScriptToken(EventScriptTokenKind.Not, text, line, column, endLine, endColumn),
            "to" => new EventScriptToken(EventScriptTokenKind.To, text, line, column, endLine, endColumn),
            "true" => new EventScriptToken(EventScriptTokenKind.True, text, line, column, endLine, endColumn),
            "false" => new EventScriptToken(EventScriptTokenKind.False, text, line, column, endLine, endColumn),
            "module" => new EventScriptToken(EventScriptTokenKind.Module, text, line, column, endLine, endColumn),
            _ when char.IsUpper(text[0]) => new EventScriptToken(EventScriptTokenKind.Message, text, line, column, endLine, endColumn),
            _ => new EventScriptToken(EventScriptTokenKind.Identifier, text, line, column, endLine, endColumn)
        };
    }

    private EventScriptToken ReadWordLikeToken(int line, int column)
    {
        var start = _index;
        var word = ReadWhile(char.IsLetterOrDigit);
        if (!IsAtEnd && (StartsAttachedIllegalOperatorSequence() || !IsValidWordBoundary(Current)))
        {
            while (!IsAtEnd && !char.IsWhiteSpace(Current))
            {
                Advance();
            }

            return CreateToken(EventScriptTokenKind.Illegal, _input[start.._index], line, column);
        }

        return CreateWordToken(word, line, column, _line, _column);
    }

    private EventScriptToken ReadSelectorToken(int line, int column)
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

            return CreateToken(EventScriptTokenKind.Illegal, _input[start.._index], line, column);
        }

        return selector switch
        {
            "any" => CreateToken(EventScriptTokenKind.SelectorAny, $":{selector}", line, column),
            "all" => CreateToken(EventScriptTokenKind.SelectorAll, $":{selector}", line, column),
            "filter" => CreateToken(EventScriptTokenKind.SelectorFilter, $":{selector}", line, column),
            "has" => CreateToken(EventScriptTokenKind.SelectorHas, $":{selector}", line, column),
            "take" => CreateToken(EventScriptTokenKind.SelectorTake, $":{selector}", line, column),
            "drop" => CreateToken(EventScriptTokenKind.SelectorDrop, $":{selector}", line, column),
            "count" => CreateToken(EventScriptTokenKind.SelectorCount, $":{selector}", line, column),
            "choose" => CreateToken(EventScriptTokenKind.SelectorChoose, $":{selector}", line, column),
            "draw" => CreateToken(EventScriptTokenKind.SelectorDraw, $":{selector}", line, column),
            "shuffle" => CreateToken(EventScriptTokenKind.SelectorShuffle, $":{selector}", line, column),
            "reverse" => CreateToken(EventScriptTokenKind.SelectorReverse, $":{selector}", line, column),
            "sum" => CreateToken(EventScriptTokenKind.SelectorSum, $":{selector}", line, column),
            "average" => CreateToken(EventScriptTokenKind.SelectorAverage, $":{selector}", line, column),
            "select" => CreateToken(EventScriptTokenKind.SelectorSelect, $":{selector}", line, column),
            "contains" => CreateToken(EventScriptTokenKind.SelectorContains, $":{selector}", line, column),
            "sort" => CreateToken(EventScriptTokenKind.SelectorSort, $":{selector}", line, column),
            _ => CreateToken(EventScriptTokenKind.Tag, $":{selector}", line, column)
        };
    }

    private EventScriptToken ReadNumberLikeToken(int line, int column)
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

                return CreateToken(EventScriptTokenKind.Illegal, _input[start.._index], line, column);
            }

            return CreateToken(EventScriptTokenKind.Percentage, text, line, column);
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

                return CreateToken(EventScriptTokenKind.Illegal, _input[start.._index], line, column);
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

                return CreateToken(EventScriptTokenKind.Illegal, _input[start.._index], line, column);
            }

            return CreateUnitDecimalToken(text, unitName, line, column);
        }

        if (!IsAtEnd && !IsValidNumberBoundary(Current))
        {
            while (!IsAtEnd && !char.IsWhiteSpace(Current))
            {
                Advance();
            }

            return CreateToken(EventScriptTokenKind.Illegal, _input[start.._index], line, column);
        }

        return CreateToken(EventScriptTokenKind.Decimal, text, line, column);
    }

    private EventScriptToken ReadTextToken(int line, int column)
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
                return CreateToken(EventScriptTokenKind.Text, builder.ToString(), line, column);
            }

            builder.Append(Current);
            Advance();
        }

        return CreateToken(EventScriptTokenKind.Illegal, _input[start.._index], line, column);
    }

    private EventScriptToken ReadOperatorToken(int line, int column)
    {
        var ch = Current;
        var next = Peek();
        Advance();
        switch (ch)
        {
            case '<' when next == '>':
                Advance();
                return CreateToken(EventScriptTokenKind.NotEqual, "<>", line, column);
            case '<' when next == '=':
                Advance();
                return CreateToken(EventScriptTokenKind.LessOrEqual, "<=", line, column);
            case '>' when next == '=':
                Advance();
                return CreateToken(EventScriptTokenKind.GreaterOrEqual, ">=", line, column);
            case '-' when next == '>':
                Advance();
                return CreateToken(EventScriptTokenKind.Arrow, "->", line, column);
            default:
                return ch switch
                {
                    '.' => CreateToken(EventScriptTokenKind.Dot, ".", line, column),
                    ',' => CreateToken(EventScriptTokenKind.Comma, ",", line, column),
                    ';' => CreateToken(EventScriptTokenKind.Semicolon, ";", line, column),
                    '(' => CreateToken(EventScriptTokenKind.LeftParen, "(", line, column),
                    ')' => CreateToken(EventScriptTokenKind.RightParen, ")", line, column),
                    '{' => CreateToken(EventScriptTokenKind.LeftBrace, "{", line, column),
                    '}' => CreateToken(EventScriptTokenKind.RightBrace, "}", line, column),
                    '[' => CreateToken(EventScriptTokenKind.LeftBracket, "[", line, column),
                    ']' => CreateToken(EventScriptTokenKind.RightBracket, "]", line, column),
                    ':' => CreateToken(EventScriptTokenKind.Colon, ":", line, column),
                    '_' => CreateToken(EventScriptTokenKind.Underscore, "_", line, column),
                    '=' => CreateToken(EventScriptTokenKind.Equal, "=", line, column),
                    '<' => CreateToken(EventScriptTokenKind.Less, "<", line, column),
                    '>' => CreateToken(EventScriptTokenKind.Greater, ">", line, column),
                    '|' => CreateToken(EventScriptTokenKind.Or, "|", line, column),
                    '^' => CreateToken(EventScriptTokenKind.Xor, "^", line, column),
                    '&' => CreateToken(EventScriptTokenKind.And, "&", line, column),
                    '+' => CreateToken(EventScriptTokenKind.Plus, "+", line, column),
                    '-' => CreateToken(EventScriptTokenKind.Minus, "-", line, column),
                    '*' => CreateToken(EventScriptTokenKind.Multiply, "*", line, column),
                    '/' => CreateToken(EventScriptTokenKind.Divide, "/", line, column),
                    '%' => CreateToken(EventScriptTokenKind.Illegal, "%", line, column),
                    '!' => CreateToken(EventScriptTokenKind.Not, "!", line, column),
                    '~' => CreateToken(EventScriptTokenKind.Not, "~", line, column),
                    _ => CreateToken(EventScriptTokenKind.Illegal, ch.ToString(), line, column)
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
