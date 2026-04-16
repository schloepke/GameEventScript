#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections.Generic;
using System.Globalization;

namespace StepH.Flow.EventScript;

public enum EventScriptTokenKind
{
    EndOfFile,
    Message,
    Identifier,
    Number,
    String,
    True,
    False,
    On,
    External,
    Emit,
    Let,
    If,
    Else,
    For,
    In,
    Random,
    To,
    Dice,
    Keep,
    Highest,
    Drop,
    Lowest,
    Count,
    Any,
    All,
    Filter,
    Where,
    Sum,
    Select,
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
    Assign,
    Arrow,
    Or,
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
    public decimal NumberValue => decimal.Parse(Text, CultureInfo.InvariantCulture);
}

public sealed class EventScriptParseException(string message, int line, int column) : Exception($"{message} (line {line}, col {column})")
{
    public int Line { get; } = line;
    public int Column { get; } = column;
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

    public IReadOnlyList<EventScriptToken> Tokenize()
    {
        var tokens = new List<EventScriptToken>();

        while (true)
        {
            SkipWhitespace();

            if (IsAtEnd)
            {
                tokens.Add(new EventScriptToken(EventScriptTokenKind.EndOfFile, string.Empty, _line, _column));
                return tokens;
            }

            var startLine = _line;
            var startColumn = _column;
            var ch = Current;

            if (char.IsLower(ch) || char.IsUpper(ch))
            {
                var text = ReadWhile(c => char.IsLetterOrDigit(c));
                tokens.Add(CreateWordToken(text, startLine, startColumn));
                continue;
            }

            if (char.IsDigit(ch))
            {
                tokens.Add(ReadNumberToken(startLine, startColumn));
                continue;
            }

            if (ch == '\'')
            {
                tokens.Add(ReadStringToken(startLine, startColumn));
                continue;
            }

            tokens.Add(ReadOperatorToken(startLine, startColumn));
        }
    }

    private bool IsAtEnd
        => _index >= _length;

    private char Current
        => _input[_index];

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
            "external" => new EventScriptToken(EventScriptTokenKind.External, text, line, column),
            "emit" => new EventScriptToken(EventScriptTokenKind.Emit, text, line, column),
            "let" => new EventScriptToken(EventScriptTokenKind.Let, text, line, column),
            "if" => new EventScriptToken(EventScriptTokenKind.If, text, line, column),
            "else" => new EventScriptToken(EventScriptTokenKind.Else, text, line, column),
            "for" => new EventScriptToken(EventScriptTokenKind.For, text, line, column),
            "in" => new EventScriptToken(EventScriptTokenKind.In, text, line, column),
            "random" => new EventScriptToken(EventScriptTokenKind.Random, text, line, column),
            "to" => new EventScriptToken(EventScriptTokenKind.To, text, line, column),
            "dice" => new EventScriptToken(EventScriptTokenKind.Dice, text, line, column),
            "keep" => new EventScriptToken(EventScriptTokenKind.Keep, text, line, column),
            "highest" => new EventScriptToken(EventScriptTokenKind.Highest, text, line, column),
            "drop" => new EventScriptToken(EventScriptTokenKind.Drop, text, line, column),
            "lowest" => new EventScriptToken(EventScriptTokenKind.Lowest, text, line, column),
            "true" => new EventScriptToken(EventScriptTokenKind.True, text, line, column),
            "false" => new EventScriptToken(EventScriptTokenKind.False, text, line, column),
            "count" => new EventScriptToken(EventScriptTokenKind.Count, text, line, column),
            "any" => new EventScriptToken(EventScriptTokenKind.Any, text, line, column),
            "all" => new EventScriptToken(EventScriptTokenKind.All, text, line, column),
            "filter" => new EventScriptToken(EventScriptTokenKind.Filter, text, line, column),
            "where" => new EventScriptToken(EventScriptTokenKind.Where, text, line, column),
            "sum" => new EventScriptToken(EventScriptTokenKind.Sum, text, line, column),
            "select" => new EventScriptToken(EventScriptTokenKind.Select, text, line, column),
            _ when char.IsUpper(text[0]) => new EventScriptToken(EventScriptTokenKind.Message, text, line, column),
            _ => new EventScriptToken(EventScriptTokenKind.Identifier, text, line, column)
        };
    }

    private EventScriptToken ReadNumberToken(int line, int column)
    {
        var start = _index;
        ReadWhile(char.IsDigit);

        if (!IsAtEnd && Current == '.' && char.IsDigit(Peek()))
        {
            Advance();
            ReadWhile(char.IsDigit);
        }

        var text = _input[start.._index];
        return new EventScriptToken(EventScriptTokenKind.Number, text, line, column);
    }

    private EventScriptToken ReadStringToken(int line, int column)
    {
        Advance();
        var start = _index;

        while (!IsAtEnd && Current != '\'')
        {
            Advance();
        }

        if (IsAtEnd) throw new EventScriptParseException("Unterminated string literal", line, column);

        var text = _input[start.._index];
        Advance();
        return new EventScriptToken(EventScriptTokenKind.String, text, line, column);
    }

    private EventScriptToken ReadOperatorToken(int line, int column)
    {
        var ch = Current;
        var next = Peek();

        Advance();
        switch (ch)
        {
            case '|' when next == '|':
                Advance();
                return new EventScriptToken(EventScriptTokenKind.Or, "||", line, column);
            case '&' when next == '&':
                Advance();
                return new EventScriptToken(EventScriptTokenKind.And, "&&", line, column);
            case '=' when next == '=':
                Advance();
                return new EventScriptToken(EventScriptTokenKind.Equal, "==", line, column);
            case '!' when next == '=':
                Advance();
                return new EventScriptToken(EventScriptTokenKind.NotEqual, "!=", line, column);
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
                    '=' => new EventScriptToken(EventScriptTokenKind.Assign, "=", line, column),
                    '<' => new EventScriptToken(EventScriptTokenKind.Less, "<", line, column),
                    '>' => new EventScriptToken(EventScriptTokenKind.Greater, ">", line, column),
                    '+' => new EventScriptToken(EventScriptTokenKind.Plus, "+", line, column),
                    '-' => new EventScriptToken(EventScriptTokenKind.Minus, "-", line, column),
                    '*' => new EventScriptToken(EventScriptTokenKind.Multiply, "*", line, column),
                    '/' => new EventScriptToken(EventScriptTokenKind.Divide, "/", line, column),
                    '%' => new EventScriptToken(EventScriptTokenKind.Modulo, "%", line, column),
                    '!' => new EventScriptToken(EventScriptTokenKind.Not, "!", line, column),
                    _ => throw new EventScriptParseException($"Unexpected character '{ch}'", line, column)
                };
        }
    }

    private void SkipWhitespace()
    {
        while (!IsAtEnd && char.IsWhiteSpace(Current))
        {
            Advance();
        }
    }
}