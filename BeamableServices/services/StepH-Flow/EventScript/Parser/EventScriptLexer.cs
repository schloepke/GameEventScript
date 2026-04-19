#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace StepH.Flow.EventScript.Parser;

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
    Modulo,
    Not
}

public readonly record struct EventScriptToken(EventScriptTokenKind Kind, string Text, int Line, int Column)
{
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
    
    public override string ToString() => $"{Kind}('{Text}', {Line}:{Column})";
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
            SkipWhitespaceExceptNewLine();

            if (IsAtEnd)
            {
                yield return new EventScriptToken(EventScriptTokenKind.EndOfFile, string.Empty, _line, _column);
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

                    yield return new EventScriptToken(EventScriptTokenKind.NewLine, "\\n", newlineLine, newlineColumn);
                    continue;
                }
                case '\n':
                {
                    var newlineLine = _line;
                    var newlineColumn = _column;
                    Advance();
                    yield return new EventScriptToken(EventScriptTokenKind.NewLine, "\\n", newlineLine, newlineColumn);
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

    private static EventScriptToken CreateWordToken(string text, int line, int column)
    {
        return text switch
        {
            "on" => new EventScriptToken(EventScriptTokenKind.On, text, line, column),
            "record" => new EventScriptToken(EventScriptTokenKind.Record, text, line, column),
            "rule" => new EventScriptToken(EventScriptTokenKind.Rule, text, line, column),
            "select" => new EventScriptToken(EventScriptTokenKind.Select, text, line, column),
            "means" => new EventScriptToken(EventScriptTokenKind.Means, text, line, column),
            "publish" => new EventScriptToken(EventScriptTokenKind.Publish, text, line, column),
            "let" => new EventScriptToken(EventScriptTokenKind.Let, text, line, column),
            "as" => new EventScriptToken(EventScriptTokenKind.As, text, line, column),
            "be" => new EventScriptToken(EventScriptTokenKind.Be, text, line, column),
            "when" => new EventScriptToken(EventScriptTokenKind.When, text, line, column),
            "otherwise" => new EventScriptToken(EventScriptTokenKind.Otherwise, text, line, column),
            "has" => new EventScriptToken(EventScriptTokenKind.Has, text, line, column),
            "empty" => new EventScriptToken(EventScriptTokenKind.Empty, text, line, column),
            "if" => new EventScriptToken(EventScriptTokenKind.If, text, line, column),
            "else" => new EventScriptToken(EventScriptTokenKind.Else, text, line, column),
            "for" => new EventScriptToken(EventScriptTokenKind.For, text, line, column),
            "in" => new EventScriptToken(EventScriptTokenKind.In, text, line, column),
            "starts" => new EventScriptToken(EventScriptTokenKind.Starts, text, line, column),
            "ends" => new EventScriptToken(EventScriptTokenKind.Ends, text, line, column),
            "with" => new EventScriptToken(EventScriptTokenKind.With, text, line, column),
            "is" => new EventScriptToken(EventScriptTokenKind.Is, text, line, column),
            "or" => new EventScriptToken(EventScriptTokenKind.Or, text, line, column),
            "xor" => new EventScriptToken(EventScriptTokenKind.Xor, text, line, column),
            "and" => new EventScriptToken(EventScriptTokenKind.And, text, line, column),
            "not" => new EventScriptToken(EventScriptTokenKind.Not, text, line, column),
            "to" => new EventScriptToken(EventScriptTokenKind.To, text, line, column),
            "true" => new EventScriptToken(EventScriptTokenKind.True, text, line, column),
            "false" => new EventScriptToken(EventScriptTokenKind.False, text, line, column),
            "module" => new EventScriptToken(EventScriptTokenKind.Module, text, line, column),
            _ when char.IsUpper(text[0]) => new EventScriptToken(EventScriptTokenKind.Message, text, line, column),
            _ => new EventScriptToken(EventScriptTokenKind.Identifier, text, line, column)
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

            return new EventScriptToken(EventScriptTokenKind.Illegal, _input[start.._index], line, column);
        }

        return CreateWordToken(word, line, column);
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

            return new EventScriptToken(EventScriptTokenKind.Illegal, _input[start.._index], line, column);
        }

        return selector switch
        {
            "any" => new EventScriptToken(EventScriptTokenKind.SelectorAny, $":{selector}", line, column),
            "all" => new EventScriptToken(EventScriptTokenKind.SelectorAll, $":{selector}", line, column),
            "filter" => new EventScriptToken(EventScriptTokenKind.SelectorFilter, $":{selector}", line, column),
            "has" => new EventScriptToken(EventScriptTokenKind.SelectorHas, $":{selector}", line, column),
            "take" => new EventScriptToken(EventScriptTokenKind.SelectorTake, $":{selector}", line, column),
            "drop" => new EventScriptToken(EventScriptTokenKind.SelectorDrop, $":{selector}", line, column),
            "count" => new EventScriptToken(EventScriptTokenKind.SelectorCount, $":{selector}", line, column),
            "choose" => new EventScriptToken(EventScriptTokenKind.SelectorChoose, $":{selector}", line, column),
            "draw" => new EventScriptToken(EventScriptTokenKind.SelectorDraw, $":{selector}", line, column),
            "shuffle" => new EventScriptToken(EventScriptTokenKind.SelectorShuffle, $":{selector}", line, column),
            "reverse" => new EventScriptToken(EventScriptTokenKind.SelectorReverse, $":{selector}", line, column),
            "sum" => new EventScriptToken(EventScriptTokenKind.SelectorSum, $":{selector}", line, column),
            "average" => new EventScriptToken(EventScriptTokenKind.SelectorAverage, $":{selector}", line, column),
            "select" => new EventScriptToken(EventScriptTokenKind.SelectorSelect, $":{selector}", line, column),
            "contains" => new EventScriptToken(EventScriptTokenKind.SelectorContains, $":{selector}", line, column),
            "sort" => new EventScriptToken(EventScriptTokenKind.SelectorSort, $":{selector}", line, column),
            _ => new EventScriptToken(EventScriptTokenKind.Tag, $":{selector}", line, column)
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

                return new EventScriptToken(EventScriptTokenKind.Illegal, _input[start.._index], line, column);
            }

            return new EventScriptToken(EventScriptTokenKind.Percentage, text, line, column);
        }

        if (!IsAtEnd && !IsValidNumberBoundary(Current))
        {
            while (!IsAtEnd && !char.IsWhiteSpace(Current))
            {
                Advance();
            }

            return new EventScriptToken(EventScriptTokenKind.Illegal, _input[start.._index], line, column);
        }

        return new EventScriptToken(EventScriptTokenKind.Decimal, text, line, column);
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
                return new EventScriptToken(EventScriptTokenKind.Text, builder.ToString(), line, column);
            }

            builder.Append(Current);
            Advance();
        }

        return new EventScriptToken(EventScriptTokenKind.Illegal, _input[start.._index], line, column);
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
                return new EventScriptToken(EventScriptTokenKind.NotEqual, "<>", line, column);
            case '<' when next == '=':
                Advance();
                return new EventScriptToken(EventScriptTokenKind.LessOrEqual, "<=", line, column);
            case '>' when next == '=':
                Advance();
                return new EventScriptToken(EventScriptTokenKind.GreaterOrEqual, ">=", line, column);
            case '-' when next == '>':
                Advance();
                return new EventScriptToken(EventScriptTokenKind.Arrow, "->", line, column);
            default:
                return ch switch
                {
                    '.' => new EventScriptToken(EventScriptTokenKind.Dot, ".", line, column),
                    ',' => new EventScriptToken(EventScriptTokenKind.Comma, ",", line, column),
                    ';' => new EventScriptToken(EventScriptTokenKind.Semicolon, ";", line, column),
                    '(' => new EventScriptToken(EventScriptTokenKind.LeftParen, "(", line, column),
                    ')' => new EventScriptToken(EventScriptTokenKind.RightParen, ")", line, column),
                    '{' => new EventScriptToken(EventScriptTokenKind.LeftBrace, "{", line, column),
                    '}' => new EventScriptToken(EventScriptTokenKind.RightBrace, "}", line, column),
                    '[' => new EventScriptToken(EventScriptTokenKind.LeftBracket, "[", line, column),
                    ']' => new EventScriptToken(EventScriptTokenKind.RightBracket, "]", line, column),
                    ':' => new EventScriptToken(EventScriptTokenKind.Colon, ":", line, column),
                    '=' => new EventScriptToken(EventScriptTokenKind.Equal, "=", line, column),
                    '<' => new EventScriptToken(EventScriptTokenKind.Less, "<", line, column),
                    '>' => new EventScriptToken(EventScriptTokenKind.Greater, ">", line, column),
                    '|' => new EventScriptToken(EventScriptTokenKind.Or, "|", line, column),
                    '^' => new EventScriptToken(EventScriptTokenKind.Xor, "^", line, column),
                    '&' => new EventScriptToken(EventScriptTokenKind.And, "&", line, column),
                    '+' => new EventScriptToken(EventScriptTokenKind.Plus, "+", line, column),
                    '-' => new EventScriptToken(EventScriptTokenKind.Minus, "-", line, column),
                    '*' => new EventScriptToken(EventScriptTokenKind.Multiply, "*", line, column),
                    '/' => new EventScriptToken(EventScriptTokenKind.Divide, "/", line, column),
                    '%' => new EventScriptToken(EventScriptTokenKind.Modulo, "%", line, column),
                    '!' => new EventScriptToken(EventScriptTokenKind.Not, "!", line, column),
                    '~' => new EventScriptToken(EventScriptTokenKind.Not, "~", line, column),
                    _ => new EventScriptToken(EventScriptTokenKind.Illegal, ch.ToString(), line, column)
                };
        }
    }

    private void SkipWhitespaceExceptNewLine()
    {
        while (!IsAtEnd && char.IsWhiteSpace(Current) && Current is not '\n' and not '\r') Advance();
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
            '+' or '-' or '*' or '/' or '%' or
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
            '%' when Peek() is '&' or '|' or '^' or '%' => true,
            '&' when Peek() == '&' => true,
            '|' when Peek() == '|' => true,
            '^' when Peek() is '&' or '|' or '^' or '%' => true,
            _ => false
        };
    }
}
