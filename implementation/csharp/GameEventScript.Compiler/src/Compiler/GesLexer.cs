// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Globalization;
using GameEventScript.Api;
using GameEventScript.Runtime;
using GameEventScript.Syntax;

namespace GameEventScript.Compiler;

internal enum GesTokenKind
{
    EndOfFile,
    Illegal,
    NewLine,
    Message,
    Identifier,
    TypeName,
    ConstantReference,
    Tag,
    // A `:word` that is not a selector keyword, such as an extension namespace or a pipeline selector name.
    Selector,
    Float,
    Percentage,
    UnitNumber,
    Text,
    True,
    False,
    Nothing,
    MathConstantPi,
    MathConstantE,
    MathConstantTau,
    MathConstantInfinity,
    IntrinsicAbs,
    IntrinsicLn,
    IntrinsicExp,
    IntrinsicSqrt,
    IntrinsicCbrt,
    IntrinsicChance,
    IntrinsicFloor,
    IntrinsicCeil,
    IntrinsicTruncate,
    IntrinsicRad,
    IntrinsicDeg,
    IntrinsicWrap,
    IntrinsicRound,
    IntrinsicSin,
    IntrinsicCos,
    IntrinsicTan,
    IntrinsicAsin,
    IntrinsicAcos,
    IntrinsicAtan,
    IntrinsicAtan2,
    IntrinsicHypot,
    IntrinsicDistance,
    IntrinsicSquared,
    IntrinsicLength,
    IntrinsicNormalize,
    IntrinsicDot,
    IntrinsicCross,
    IntrinsicAngle,
    KeywordRandom,
    KeywordParse,
    Series,
    Fibonacci,
    Factorial,
    Clamp,
    Min,
    Max,
    Zip,
    Roll,
    Default,
    Module,
    Constant,
    Record,
    Predicate,
    Function,
    On,
    Emit,
    Publish,
    Matching,
    Without,
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
    Of,
    NotIn,
    Starts,
    Ends,
    With,
    Is,
    Numeric,
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
    OperatorImplication,
    ProjectionArrow,
    OperatorOr,
    OperatorXor,
    OperatorAnd,
    OperatorCollectionUnion,
    OperatorCollectionIntersect,
    OperatorEqual,
    OperatorNotEqual,
    OperatorLess,
    OperatorGreater,
    OperatorLessOrEqual,
    OperatorGreaterOrEqual,
    OperatorPlus,
    OperatorMinus,
    OperatorMultiply,
    OperatorDivide,
    OperatorIntegerDivide,
    OperatorModulo,
    OperatorRemainder,
    OperatorPower,
    SuperscriptInteger,
    OperatorNot
}

internal readonly record struct GesToken(GesTokenKind Kind, string Text, int Line, int Column, int EndLine, int EndColumn, string UnitName = "")
{
    private Runtime.Values.GesValue NumericValue => GesTextNumberCast.Read(Text, percentage: Kind == GesTokenKind.Percentage,
        allowGrouping: false, percentageMagnitude: Kind == GesTokenKind.Percentage) ?? Runtime.Values.GesValue.GesNothing();

    public double FloatValue => NumericValue.AsNumber();

    public long? GetIntegerValue()
    {
        var value = NumericValue;
        return value.Kind == Api.GesBytecodeTypeKind.Integer ? value.IntegerValue : null;
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
    private bool _previousWasCarriageReturn;

    public GesLexer(string input)
    {
        _input = GesText.PrepareSource(input ?? throw new ArgumentNullException(nameof(input)), nameof(input));
        _length = _input.Length;
    }

    public GesToken ReadNextToken()
    {
        while (true)
        {
            SkipTriviaExceptNewLine();

            if (IsAtEnd) return new GesToken(GesTokenKind.EndOfFile, string.Empty, _line, _column, _line, _column);

            switch (Current)
            {
                case '\r':
                {
                    var newlineLine = _line;
                    var newlineColumn = _column;
                    Advance();
                    if (!IsAtEnd && Current == '\n') Advance();

                    return new GesToken(GesTokenKind.NewLine, "\\n", newlineLine, newlineColumn, _line, _column);
                }
                case '\n':
                {
                    var newlineLine = _line;
                    var newlineColumn = _column;
                    Advance();
                    return new GesToken(GesTokenKind.NewLine, "\\n", newlineLine, newlineColumn, _line, _column);
                }
            }

            var startLine = _line;
            var startColumn = _column;
            var ch = Current;

            if (IsWordStart(ch)) return ReadWordLikeToken(startLine, startColumn);

            if (GesText.IsAsciiDigit(ch)) return ReadNumberLikeToken(startLine, startColumn);

            switch (ch)
            {
                case '\'':
                case '"': return ReadTextToken(startLine, startColumn);
                case ':' when GesText.IsAsciiUpper(Peek()): return ReadTypeToken(startLine, startColumn);
                case ':' when GesText.IsAsciiLower(Peek()): return ReadSelectorToken(startLine, startColumn);
                case '#' when GesText.IsAsciiLower(Peek()): return ReadTagToken(startLine, startColumn);
                case '$' when GesText.IsAsciiLower(Peek()): return ReadConstantReferenceToken(startLine, startColumn);
                default: return ReadOperatorToken(startLine, startColumn);
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
        if (Current == '\r')
        {
            _line++;
            _column = 1;
            _previousWasCarriageReturn = true;
            _index++;
            return;
        }

        if (Current == '\n')
        {
            if (!_previousWasCarriageReturn) _line++;
            _column = 1;
            _previousWasCarriageReturn = false;
            _index++;
            return;
        }

        _previousWasCarriageReturn = false;
        _column++;
        _index += GesText.ScalarUtf16LengthAt(_input, _index);
    }

    private void ReadWordName()
    {
        while (!IsAtEnd && (IsWordLetter(Current) || GesText.IsAsciiDigit(Current))) Advance();
    }

    private void ReadDigits()
    {
        while (!IsAtEnd && GesText.IsAsciiDigit(Current)) Advance();
    }

    private GesToken CreateToken(GesTokenKind kind, string text, int startLine, int startColumn) => new(kind, text, startLine, startColumn, _line, _column);

    private GesToken CreateUnitNumberToken(string text, string unitName, int startLine, int startColumn) => new(GesTokenKind.UnitNumber, text, startLine, startColumn, _line, _column, unitName);

    private GesToken CreateWordToken(int start, int length, int line, int column, int endLine, int endColumn)
    {
        // Keywords reuse their constant spelling, so only identifiers and messages allocate text.
        var (kind, text) = _input.AsSpan(start, length) switch
        {
            "on" => (GesTokenKind.On, "on"),
            "record" => (GesTokenKind.Record, "record"),
            "predicate" => (GesTokenKind.Predicate, "predicate"),
            "function" => (GesTokenKind.Function, "function"),
            "emit" => (GesTokenKind.Emit, "emit"),
            "publish" => (GesTokenKind.Publish, "publish"),
            "matching" => (GesTokenKind.Matching, "matching"),
            "without" => (GesTokenKind.Without, "without"),
            "let" => (GesTokenKind.Let, "let"),
            "as" => (GesTokenKind.As, "as"),
            "be" => (GesTokenKind.Be, "be"),
            "when" => (GesTokenKind.When, "when"),
            "otherwise" => (GesTokenKind.Otherwise, "otherwise"),
            "has" => (GesTokenKind.Has, "has"),
            "parse" => (GesTokenKind.KeywordParse, "parse"),
            "empty" => (GesTokenKind.Empty, "empty"),
            "if" => (GesTokenKind.If, "if"),
            "else" => (GesTokenKind.Else, "else"),
            "for" => (GesTokenKind.For, "for"),
            "in" => (GesTokenKind.In, "in"),
            "of" => (GesTokenKind.Of, "of"),
            "starts" => (GesTokenKind.Starts, "starts"),
            "ends" => (GesTokenKind.Ends, "ends"),
            "with" => (GesTokenKind.With, "with"),
            "is" => (GesTokenKind.Is, "is"),
            "numeric" => (GesTokenKind.Numeric, "numeric"),
            "or" => (GesTokenKind.OperatorOr, "or"),
            "xor" => (GesTokenKind.OperatorXor, "xor"),
            "and" => (GesTokenKind.OperatorAnd, "and"),
            "div" => (GesTokenKind.OperatorIntegerDivide, "div"),
            "mod" => (GesTokenKind.OperatorModulo, "mod"),
            "rem" => (GesTokenKind.OperatorRemainder, "rem"),
            "not" => (GesTokenKind.OperatorNot, "not"),
            "to" => (GesTokenKind.To, "to"),
            "true" => (GesTokenKind.True, "true"),
            "false" => (GesTokenKind.False, "false"),
            "nothing" => (GesTokenKind.Nothing, "nothing"),
            "pi" => (GesTokenKind.MathConstantPi, "pi"),
            "e" => (GesTokenKind.MathConstantE, "e"),
            "tau" => (GesTokenKind.MathConstantTau, "tau"),
            "infinity" => (GesTokenKind.MathConstantInfinity, "infinity"),
            "abs" => (GesTokenKind.IntrinsicAbs, "abs"),
            "ln" => (GesTokenKind.IntrinsicLn, "ln"),
            "exp" => (GesTokenKind.IntrinsicExp, "exp"),
            "sqrt" => (GesTokenKind.IntrinsicSqrt, "sqrt"),
            "cbrt" => (GesTokenKind.IntrinsicCbrt, "cbrt"),
            "chance" => (GesTokenKind.IntrinsicChance, "chance"),
            "floor" => (GesTokenKind.IntrinsicFloor, "floor"),
            "ceil" => (GesTokenKind.IntrinsicCeil, "ceil"),
            "truncate" => (GesTokenKind.IntrinsicTruncate, "truncate"),
            "rad" => (GesTokenKind.IntrinsicRad, "rad"),
            "deg" => (GesTokenKind.IntrinsicDeg, "deg"),
            "wrap" => (GesTokenKind.IntrinsicWrap, "wrap"),
            "round" => (GesTokenKind.IntrinsicRound, "round"),
            "sin" => (GesTokenKind.IntrinsicSin, "sin"),
            "cos" => (GesTokenKind.IntrinsicCos, "cos"),
            "tan" => (GesTokenKind.IntrinsicTan, "tan"),
            "asin" => (GesTokenKind.IntrinsicAsin, "asin"),
            "acos" => (GesTokenKind.IntrinsicAcos, "acos"),
            "atan" => (GesTokenKind.IntrinsicAtan, "atan"),
            "atan2" => (GesTokenKind.IntrinsicAtan2, "atan2"),
            "hypot" => (GesTokenKind.IntrinsicHypot, "hypot"),
            "distance" => (GesTokenKind.IntrinsicDistance, "distance"),
            "squared" => (GesTokenKind.IntrinsicSquared, "squared"),
            "length" => (GesTokenKind.IntrinsicLength, "length"),
            "normalize" => (GesTokenKind.IntrinsicNormalize, "normalize"),
            "dot" => (GesTokenKind.IntrinsicDot, "dot"),
            "cross" => (GesTokenKind.IntrinsicCross, "cross"),
            "angle" => (GesTokenKind.IntrinsicAngle, "angle"),
            "random" => (GesTokenKind.KeywordRandom, "random"),
            "series" => (GesTokenKind.Series, "series"),
            "fibonacci" => (GesTokenKind.Fibonacci, "fibonacci"),
            "factorial" => (GesTokenKind.Factorial, "factorial"),
            "clamp" => (GesTokenKind.Clamp, "clamp"),
            "min" => (GesTokenKind.Min, "min"),
            "max" => (GesTokenKind.Max, "max"),
            "zip" => (GesTokenKind.Zip, "zip"),
            "roll" => (GesTokenKind.Roll, "roll"),
            "default" => (GesTokenKind.Default, "default"),
            "module" => (GesTokenKind.Module, "module"),
            "constant" => (GesTokenKind.Constant, "constant"),
            _ => (GesText.IsAsciiUpper(_input[start]) ? GesTokenKind.Message : GesTokenKind.Identifier, null)
        };
        return new GesToken(kind, text ?? _input[start..(start + length)], line, column, endLine, endColumn);
    }

    private GesToken ReadWordLikeToken(int line, int column)
    {
        var start = _index;
        var first = Current;
        ReadWordName();
        if (GesText.IsAsciiLower(first) && !IsAtEnd && Current == '_')
        {
            Advance();
            if (!ReadValidIdentifierSuffix())
            {
                while (!IsAtEnd && !GesText.IsTokenWhitespace(Current)) Advance();

                return CreateToken(GesTokenKind.Illegal, _input[start.._index], line, column);
            }

            if (IsAtEnd || (!StartsAttachedIllegalOperatorSequence() && IsValidWordBoundary(Current))) return CreateToken(GesTokenKind.Identifier, _input[start.._index], line, column);
        }
        if (IsAtEnd || (!StartsAttachedIllegalOperatorSequence() && IsValidWordBoundary(Current))) return CreateWordToken(start, _index - start, line, column, _line, _column);
        while (!IsAtEnd && !GesText.IsTokenWhitespace(Current)) Advance();

        return CreateToken(GesTokenKind.Illegal, _input[start.._index], line, column);
    }

    private bool ReadValidIdentifierSuffix()
    {
        if (IsAtEnd || !GesText.IsAsciiDigit(Current)) return false;

        if (Current == '0')
        {
            Advance();
            return IsAtEnd || !GesText.IsAsciiDigit(Current);
        }

        ReadDigits();
        return true;
    }

    private GesToken ReadSelectorToken(int line, int column)
    {
        var start = _index;
        Advance();
        var selectorStart = _index;
        ReadWordName();
        var selectorLength = _index - selectorStart;

        if (IsAtEnd || (!StartsAttachedIllegalOperatorSequence() && IsValidWordBoundary(Current)))
        {
            var (kind, text) = _input.AsSpan(selectorStart, selectorLength) switch
            {
                "any" => (GesTokenKind.SelectorAny, ":any"),
                "all" => (GesTokenKind.SelectorAll, ":all"),
                "filter" => (GesTokenKind.SelectorFilter, ":filter"),
                "has" => (GesTokenKind.SelectorHas, ":has"),
                "take" => (GesTokenKind.SelectorTake, ":take"),
                "drop" => (GesTokenKind.SelectorDrop, ":drop"),
                "count" => (GesTokenKind.SelectorCount, ":count"),
                "choose" => (GesTokenKind.SelectorChoose, ":choose"),
                "draw" => (GesTokenKind.SelectorDraw, ":draw"),
                "shuffle" => (GesTokenKind.SelectorShuffle, ":shuffle"),
                "reverse" => (GesTokenKind.SelectorReverse, ":reverse"),
                "sum" => (GesTokenKind.SelectorSum, ":sum"),
                "average" => (GesTokenKind.SelectorAverage, ":average"),
                "select" => (GesTokenKind.SelectorSelect, ":select"),
                "contains" => (GesTokenKind.SelectorContains, ":contains"),
                "sort" => (GesTokenKind.SelectorSort, ":sort"),
                _ => (GesTokenKind.Selector, null)
            };
            return CreateToken(kind, text ?? _input[start.._index], line, column);
        }

        while (!IsAtEnd && !GesText.IsTokenWhitespace(Current)) Advance();

        return CreateToken(GesTokenKind.Illegal, _input[start.._index], line, column);
    }

    private GesToken ReadTagToken(int line, int column)
    {
        var start = _index;
        Advance();
        var tagStart = _index;
        ReadWordName();

        if (_index == tagStart) return CreateToken(GesTokenKind.Illegal, _input[start.._index], line, column);

        if (IsAtEnd || (!StartsAttachedIllegalOperatorSequence() && IsValidWordBoundary(Current)))
            return CreateToken(GesTokenKind.Tag, _input[start.._index], line, column);

        while (!IsAtEnd && !GesText.IsTokenWhitespace(Current)) Advance();

        return CreateToken(GesTokenKind.Illegal, _input[start.._index], line, column);
    }

    private GesToken ReadTypeToken(int line, int column)
    {
        var start = _index;
        Advance();
        ReadWordName();
        if (IsAtEnd || (!StartsAttachedIllegalOperatorSequence() && IsValidWordBoundary(Current)))
            return CreateToken(GesTokenKind.TypeName, _input[start.._index], line, column);
        while (!IsAtEnd && !GesText.IsTokenWhitespace(Current)) Advance();
        return CreateToken(GesTokenKind.Illegal, _input[start.._index], line, column);
    }

    private GesToken ReadConstantReferenceToken(int line, int column)
    {
        var start = _index;
        Advance();
        ReadWordName();
        if (IsAtEnd || (!StartsAttachedIllegalOperatorSequence() && IsValidWordBoundary(Current)))
            return CreateToken(GesTokenKind.ConstantReference, _input[start.._index], line, column);
        while (!IsAtEnd && !GesText.IsTokenWhitespace(Current)) Advance();
        return CreateToken(GesTokenKind.Illegal, _input[start.._index], line, column);
    }

    private GesToken ReadNumberLikeToken(int line, int column)
    {
        var start = _index;
        ReadNumberDigits();
        if (!IsAtEnd && Current == '.' && GesText.IsAsciiDigit(Peek()))
        {
            Advance();
            ReadNumberDigits();
        }

        var text = _input[start.._index];
        if (!IsAtEnd && Current == '%')
        {
            Advance();
            text = _input[start..(_index - 1)];
            if (IsAtEnd || IsValidNumberBoundary(Current)) return CreateToken(GesTokenKind.Percentage, text, line, column);
            while (!IsAtEnd && !GesText.IsTokenWhitespace(Current)) Advance();

            return CreateToken(GesTokenKind.Illegal, _input[start.._index], line, column);
        }

        // The longest unit spelling directly followed by a boundary forms one quantity literal, so `10m/s` is a speed
        // and `10m / s` a division. The token text keeps the spelling, whose decoding applies prefix scaling.
        if (!IsAtEnd && MayStartUnitSuffix(Current))
            foreach (var suffix in GesQuantityUnits.Suffixes)
            {
                var length = suffix.Text.Length;
                var next = _index + length;
                if (next > _length || string.CompareOrdinal(_input, _index, suffix.Text, 0, length) != 0 || next < _length && !IsValidUnitBoundary(_input[next])) continue;
                for (var i = 0; i < length; i++) Advance();
                return CreateUnitNumberToken(_input[start.._index], suffix.Unit.ToTypeName(), line, column);
            }

        if (!IsAtEnd && Current == '\u00B0')
        {
            while (!IsAtEnd && !GesText.IsTokenWhitespace(Current)) Advance();

            return CreateToken(GesTokenKind.Illegal, _input[start.._index], line, column);
        }

        if (!IsAtEnd && GesText.IsAsciiLetter(Current)) return CreateToken(GesTokenKind.Float, text, line, column);

        if (IsAtEnd || IsValidNumberBoundary(Current)) return CreateToken(GesTokenKind.Float, text, line, column);
        while (!IsAtEnd && !GesText.IsTokenWhitespace(Current)) Advance();

        return CreateToken(GesTokenKind.Illegal, _input[start.._index], line, column);
    }

    private void ReadNumberDigits()
    {
        while (!IsAtEnd)
        {
            if (GesText.IsAsciiDigit(Current))
            {
                Advance();
                continue;
            }

            if (Current == '_' && GesText.IsAsciiDigit(Peek()))
            {
                Advance();
                continue;
            }

            break;
        }
    }

    private GesToken ReadTextToken(int line, int column)
    {
        var start = _index;
        var end = _index;
        var text = GesTextLiteralReader.Read(_input, ref end);
        while (_index < end) Advance();
        return text is null
            ? CreateToken(GesTokenKind.Illegal, _input[start.._index], line, column)
            : CreateToken(GesTokenKind.Text, text, line, column);
    }

    private GesToken ReadOperatorToken(int line, int column)
    {
        var ch = Current;
        var next = Peek();
        var text = GesText.ScalarAtUtf16Offset(_input, _index);
        Advance();
        switch (ch)
        {
            case '<' when next == '>': Advance(); return CreateToken(GesTokenKind.OperatorNotEqual, "<>", line, column);
            case '<' when next == '=': Advance(); return CreateToken(GesTokenKind.OperatorLessOrEqual, "<=", line, column);
            case '>' when next == '=': Advance(); return CreateToken(GesTokenKind.OperatorGreaterOrEqual, ">=", line, column);
            case '-' when next == '>': Advance(); return CreateToken(GesTokenKind.OperatorImplication, "->", line, column);
            case '=' when next == '>': Advance(); return CreateToken(GesTokenKind.ProjectionArrow, "=>", line, column);
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
                    '=' => CreateToken(GesTokenKind.OperatorEqual, "=", line, column),
                    '<' => CreateToken(GesTokenKind.OperatorLess, "<", line, column),
                    '>' => CreateToken(GesTokenKind.OperatorGreater, ">", line, column),
                    '|' => CreateToken(GesTokenKind.OperatorCollectionUnion, "|", line, column),
                    '^' => CreateToken(GesTokenKind.OperatorPower, "^", line, column),
                    '&' => CreateToken(GesTokenKind.OperatorCollectionIntersect, "&", line, column),
                    '+' => CreateToken(GesTokenKind.OperatorPlus, "+", line, column),
                    '-' => CreateToken(GesTokenKind.OperatorMinus, "-", line, column),
                    '*' => CreateToken(GesTokenKind.OperatorMultiply, "*", line, column),
                    '/' => CreateToken(GesTokenKind.OperatorDivide, "/", line, column),
                    '\u00B7' => CreateToken(GesTokenKind.OperatorMultiply, "*", line, column),
                    '\u00D7' => CreateToken(GesTokenKind.OperatorMultiply, "*", line, column),
                    '\u00F7' => CreateToken(GesTokenKind.OperatorDivide, "/", line, column),
                    '\u2212' => CreateToken(GesTokenKind.OperatorMinus, "-", line, column),
                    '\u221E' => CreateToken(GesTokenKind.MathConstantInfinity, "infinity", line, column),
                    '\u03C0' => CreateToken(GesTokenKind.MathConstantPi, "pi", line, column),
                    '\u220F' => CreateToken(GesTokenKind.MathConstantPi, "pi", line, column),
                    '\u2107' => CreateToken(GesTokenKind.MathConstantE, "e", line, column),
                    '\u03C4' => CreateToken(GesTokenKind.MathConstantTau, "tau", line, column),
                    '\u221A' => CreateToken(GesTokenKind.IntrinsicSqrt, "sqrt", line, column),
                    '\u221B' => CreateToken(GesTokenKind.IntrinsicCbrt, "cbrt", line, column),
                    '\u00B0' => CreateToken(GesTokenKind.Identifier, "degree", line, column),
                    '\u2227' => CreateToken(GesTokenKind.OperatorAnd, "\u2227", line, column),
                    '\u2228' => CreateToken(GesTokenKind.OperatorOr, "\u2228", line, column),
                    '\u2208' => CreateToken(GesTokenKind.In, "in", line, column),
                    '\u2209' => CreateToken(GesTokenKind.NotIn, "not in", line, column),
                    '\u2295' => CreateToken(GesTokenKind.OperatorXor, "xor", line, column),
                    '\u22C5' => CreateToken(GesTokenKind.OperatorMultiply, "*", line, column),
                    '\u2264' => CreateToken(GesTokenKind.OperatorLessOrEqual, "<=", line, column),
                    '\u2265' => CreateToken(GesTokenKind.OperatorGreaterOrEqual, ">=", line, column),
                    '\u00AC' => CreateToken(GesTokenKind.OperatorNot, "!", line, column),
                    '\u2260' => CreateToken(GesTokenKind.OperatorNotEqual, "<>", line, column),
                    '\u2248' => CreateToken(GesTokenKind.Illegal, "\u2248", line, column),
                    '\u2245' => CreateToken(GesTokenKind.Illegal, "\u2245", line, column),
                    '\u2192' => CreateToken(GesTokenKind.OperatorImplication, "->", line, column),
                    '\u21D2' => CreateToken(GesTokenKind.OperatorImplication, "->", line, column),
                    '\u21A6' => CreateToken(GesTokenKind.ProjectionArrow, "=>", line, column),
                    '\u00B2' => CreateToken(GesTokenKind.SuperscriptInteger, "2", line, column),
                    '\u00B3' => CreateToken(GesTokenKind.SuperscriptInteger, "3", line, column),
                    '%' => CreateToken(GesTokenKind.Illegal, "%", line, column),
                    '!' => CreateToken(GesTokenKind.OperatorNot, "!", line, column),
                    '~' => CreateToken(GesTokenKind.OperatorNot, "~", line, column),
                    _ => CreateToken(GesTokenKind.Illegal, text, line, column)
                };
        }
    }

    private void SkipTriviaExceptNewLine()
    {
        while (!IsAtEnd)
        {
            if (GesText.IsInlineWhitespace(Current))
            {
                Advance();
                continue;
            }

            if (Current == '/' && Peek() == '/')
            {
                Advance();
                Advance();
                while (!IsAtEnd && Current is not '\n' and not '\r') Advance();

                continue;
            }

            break;
        }
    }

    private static bool IsValidWordBoundary(char ch) => GesText.IsTokenWhitespace(ch) || IsStructuralBoundary(ch);

    private bool IsValidNumberBoundary(char ch) => GesText.IsTokenWhitespace(ch) || IsStructuralBoundary(ch) || (ch == 'd' && GesText.IsAsciiDigit(Peek()));

    // Only these characters start a unit spelling, so unitless numbers skip the suffix table. A native test keeps the
    // set in step with GesQuantityUnits.Suffixes.
    internal static bool MayStartUnitSuffix(char ch) =>
        ch is 'm' or 's' or 'd' or 'k' or 'M' or 'W' or 'H' or 'N' or 'J' or 'V' or 'A' or 'K' or '\u00B0';

    private static bool IsValidUnitBoundary(char ch) => ch == '\0' || GesText.IsTokenWhitespace(ch) || IsStructuralBoundary(ch);

    private static bool IsWordStart(char ch) => GesText.IsAsciiLetter(ch);

    private static bool IsWordLetter(char ch) => GesText.IsAsciiLetter(ch);

    private static bool IsStructuralBoundary(char ch)
        => ch is '(' or ')' or '{' or '}' or '[' or ']' or ',' or ';' or '.' or ':' or '#' or '+' or '-' or '*' or '/' or '!' or '~' or '&' or '|' or '^' or '=' or '<' or '>' or
            '\u00B7' or '\u00D7' or '\u00F7' or '\u2212' or '\u221E' or '\u220F' or '\u2107' or '\u03C4' or '\u03C6' or '\u221A' or '\u221B' or '\u00B0' or
            '\u2227' or '\u2228' or '\u2208' or '\u2209' or '\u2295' or '\u22C5' or
            '\u2264' or '\u2265' or '\u00AC' or '\u2260' or '\u2248' or '\u2245' or
            '\u2192' or '\u21D2' or '\u21A6' or '\u00B2' or '\u00B3';

    private bool StartsAttachedIllegalOperatorSequence()
    {
        if (IsAtEnd) return false;

        return Current switch
        {
            '&' when Peek() == '&' => true,
            '|' when Peek() == '|' => true,
            '^' when Peek() is '&' or '|' or '^' => true,
            _ => false
        };
    }
}
