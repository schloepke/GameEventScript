#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace StepH.Flow.EventScript.Parser;

public enum EventScriptTokenKind
{
    EndOfFile,
    NewLine,
    Message,
    Identifier,
    Tag,
    Decimal,
    Percentage,
    Text,
    True,
    False,
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

        value = default;
        return false;
    }
}

public sealed record EventScriptLexingResult(IReadOnlyList<EventScriptToken> Tokens, IReadOnlyList<EventScriptSyntaxError> Errors);

public sealed class EventScriptLexer
{
    private readonly string _input;
    private readonly string _moduleName;
    private readonly string _sourceName;
    private readonly int _length;
    private int _index;
    private int _line = 1;
    private int _column = 1;

    public EventScriptLexer(string input, string moduleName, string sourceName)
    {
        _input = input ?? throw new ArgumentNullException(nameof(input));
        _moduleName = string.IsNullOrWhiteSpace(moduleName) ? "AnonymousModule_Unknown" : moduleName;
        _sourceName = string.IsNullOrWhiteSpace(sourceName) ? "UnknownSource_Unknown" : sourceName;
        _length = _input.Length;
    }

    public EventScriptLexingResult Tokenize()
    {
        var tokens = new List<EventScriptToken>();
        var errors = new List<EventScriptSyntaxError>();

        while (true)
        {
            SkipWhitespaceExceptNewLine();

            if (IsAtEnd)
            {
                tokens.Add(new EventScriptToken(EventScriptTokenKind.EndOfFile, string.Empty, _line, _column));
                return new EventScriptLexingResult(tokens, errors);
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

                    tokens.Add(new EventScriptToken(EventScriptTokenKind.NewLine, "\\n", newlineLine, newlineColumn));
                    continue;
                }
                case '\n':
                {
                    var newlineLine = _line;
                    var newlineColumn = _column;
                    Advance();
                    tokens.Add(new EventScriptToken(EventScriptTokenKind.NewLine, "\\n", newlineLine, newlineColumn));
                    continue;
                }
            }

            var startLine = _line;
            var startColumn = _column;
            var ch = Current;

            if (char.IsLower(ch) || char.IsUpper(ch))
            {
                tokens.Add(CreateWordToken(ReadWhile(char.IsLetterOrDigit), startLine, startColumn));
                continue;
            }

            if (char.IsDigit(ch))
            {
                tokens.Add(ReadDecimalToken(startLine, startColumn));
                continue;
            }

            switch (ch)
            {
                case '\'':
                {
                    var textToken = ReadTextToken(startLine, startColumn, errors);
                    if (textToken is not null)
                    {
                        tokens.Add(textToken.Value);
                    }

                    continue;
                }
                case ':' when char.IsLower(Peek()):
                    tokens.Add(ReadSelectorToken(startLine, startColumn));
                    continue;
                default:
                {
                    var operatorToken = ReadOperatorToken(startLine, startColumn, errors);
                    if (operatorToken is not null)
                    {
                        tokens.Add(operatorToken.Value);
                    }

                    break;
                }
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
            _ when char.IsUpper(text[0]) => new EventScriptToken(EventScriptTokenKind.Message, text, line, column),
            _ => new EventScriptToken(EventScriptTokenKind.Identifier, text, line, column)
        };
    }

    private EventScriptToken ReadSelectorToken(int line, int column)
    {
        Advance();
        var selector = ReadWhile(char.IsLetterOrDigit);

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

    private EventScriptToken ReadDecimalToken(int line, int column)
    {
        var start = _index;
        ReadWhile(char.IsDigit);
        if (!IsAtEnd && Current == '.' && char.IsDigit(Peek()))
        {
            Advance();
            ReadWhile(char.IsDigit);
        }

        var text = _input[start.._index];
        if (IsAtEnd || Current != '%') return new EventScriptToken(EventScriptTokenKind.Decimal, text, line, column);
        Advance();
        return new EventScriptToken(EventScriptTokenKind.Percentage, text, line, column);
    }

    private EventScriptToken? ReadTextToken(int line, int column, List<EventScriptSyntaxError> errors)
    {
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

        errors.Add(CreateSyntaxError("Unterminated string literal", EventScriptSyntaxErrorKind.Lexer, line, column));
        return null;
    }

    private EventScriptToken? ReadOperatorToken(int line, int column, List<EventScriptSyntaxError> errors)
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
                    _ => AddUnexpectedCharacterError(ch, line, column, errors)
                };
        }
    }

    private EventScriptToken? AddUnexpectedCharacterError(char ch, int line, int column, List<EventScriptSyntaxError> errors)
    {
        errors.Add(CreateSyntaxError($"Unexpected character '{ch}'", EventScriptSyntaxErrorKind.Lexer, line, column));
        return null;
    }

    private EventScriptSyntaxError CreateSyntaxError(string message, EventScriptSyntaxErrorKind kind, int line, int column)
        => new(message, _moduleName, kind, new EventScriptSourceLocation(_sourceName, line, column));

    private void SkipWhitespaceExceptNewLine()
    {
        while (!IsAtEnd && char.IsWhiteSpace(Current) && Current is not '\n' and not '\r') Advance();
    }
}
