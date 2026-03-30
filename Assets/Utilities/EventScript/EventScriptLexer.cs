#nullable enable

using System;
using System.Collections.Generic;

namespace StepH.Utilities.EventScript
{
    public enum TokenType
    {
        // Keywords
        On, Emit, Let, If, Else, For, In, True, False,

        // Literals
        Identifier, Number, String,

        // Symbols
        Equal, Comma, Semicolon,
        LeftParen, RightParen,
        LeftBrace, RightBrace,
        Dot, Plus, Minus, Star, Slash,
        Bang, And, Or,
        EqualEqual, BangEqual, Less, Greater, LessEqual, GreaterEqual,

        // Misc
        Eof
    }

    public class Token
    {
        public TokenType Type { get; }
        public string Lexeme { get; }

        public Token(TokenType type, string lexeme)
        {
            Type = type;
            Lexeme = lexeme;
        }

        public override string ToString() => $"{Type}: {Lexeme}";
    }

    public class Lexer
    {
        private readonly string _source;
        private readonly List<Token> _tokens = new();
        private int _start;
        private int _current;

        private static readonly Dictionary<string, TokenType> Keywords = new()
        {
            ["on"] = TokenType.On,
            ["emit"] = TokenType.Emit,
            ["let"] = TokenType.Let,
            ["if"] = TokenType.If,
            ["else"] = TokenType.Else,
            ["for"] = TokenType.For,
            ["in"] = TokenType.In,
            ["true"] = TokenType.True,
            ["false"] = TokenType.False
        };

        public Lexer(string source)
        {
            _source = source;
        }

        public List<Token> Tokenize()
        {
            while (!IsAtEnd())
            {
                _start = _current;
                ScanToken();
            }
            _tokens.Add(new Token(TokenType.Eof, ""));
            return _tokens;
        }

        private void ScanToken()
        {
            char c = Advance();
            switch (c)
            {
                case '(': AddToken(TokenType.LeftParen); break;
                case ')': AddToken(TokenType.RightParen); break;
                case '{': AddToken(TokenType.LeftBrace); break;
                case '}': AddToken(TokenType.RightBrace); break;
                case ',': AddToken(TokenType.Comma); break;
                case ';': AddToken(TokenType.Semicolon); break;
                case '.': AddToken(TokenType.Dot); break;
                case '+': AddToken(TokenType.Plus); break;
                case '-': AddToken(TokenType.Minus); break;
                case '*': AddToken(TokenType.Star); break;
                case '/': AddToken(TokenType.Slash); break;
                case '!': AddToken(Match('=') ? TokenType.BangEqual : TokenType.Bang); break;
                case '=': AddToken(Match('=') ? TokenType.EqualEqual : TokenType.Equal); break;
                case '<': AddToken(Match('=') ? TokenType.LessEqual : TokenType.Less); break;
                case '>': AddToken(Match('=') ? TokenType.GreaterEqual : TokenType.Greater); break;
                case '\'': String(); break;
                case ' ': case '\r': case '\t': case '\n': break; // ignore whitespace
                default:
                    if (IsDigit(c)) Number();
                    else if (IsAlpha(c)) Identifier();
                    else throw new Exception($"Unexpected character: {c}");
                    break;
            }
        }

        private void Identifier()
        {
            while (IsAlphaNumeric(Peek())) Advance();
            string text = _source[_start.._current];
            if (Keywords.TryGetValue(text, out var type))
                AddToken(type);
            else
                AddToken(TokenType.Identifier);
        }

        private void Number()
        {
            while (IsDigit(Peek())) Advance();
            if (Peek() == '.' && IsDigit(PeekNext()))
            {
                Advance();
                while (IsDigit(Peek())) Advance();
            }
            AddToken(TokenType.Number);
        }

        private void String()
        {
            while (!IsAtEnd() && Peek() != '\'') Advance();
            if (IsAtEnd()) throw new Exception("Unterminated string.");
            Advance();
            string value = _source[(_start + 1)..(_current - 1)];
            _tokens.Add(new Token(TokenType.String, value));
        }

        private bool Match(char expected)
        {
            if (IsAtEnd() || _source[_current] != expected) return false;
            _current++;
            return true;
        }

        private char Peek() => IsAtEnd() ? '\0' : _source[_current];
        private char PeekNext() => (_current + 1 >= _source.Length) ? '\0' : _source[_current + 1];
        private char Advance() => _source[_current++];
        private bool IsAtEnd() => _current >= _source.Length;
        private void AddToken(TokenType type) => _tokens.Add(new Token(type, _source[_start.._current]));

        private static bool IsDigit(char c) => c >= '0' && c <= '9';
        private static bool IsAlpha(char c) => char.IsLetter(c) || c == '_';
        private static bool IsAlphaNumeric(char c) => IsAlpha(c) || IsDigit(c);
    }
}
