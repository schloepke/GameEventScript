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
    Clamp,
    Min,
    Max,
    Zip,
    Roll,
    Default,
    Module,
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
    public double FloatValue => double.Parse(NormalizedNumericText, CultureInfo.InvariantCulture);

    public bool TryGetIntegerValue(out long value)
    {
        if (Kind is GesTokenKind.Float or GesTokenKind.UnitNumber &&
            Text.IndexOf('.') < 0 &&
            long.TryParse(NormalizeNumericText(Text), NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
        {
            return true;
        }

        value = 0;
        return false;
    }

    public string NormalizedNumericText => NormalizeNumericText(Text);

    private static string NormalizeNumericText(string text)
        => text.IndexOf('_') < 0 ? text : text.Replace("_", string.Empty, StringComparison.Ordinal);

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

            if (IsWordStart(ch))
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
                case '"':
                    yield return ReadTextToken(startLine, startColumn);
                    continue;
                case ':' when char.IsLower(Peek()):
                    yield return ReadSelectorToken(startLine, startColumn);
                    continue;
                case '#' when char.IsLower(Peek()):
                    yield return ReadTagToken(startLine, startColumn);
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

    private GesToken CreateUnitNumberToken(string text, string unitName, int startLine, int startColumn)
        => new(GesTokenKind.UnitNumber, text, startLine, startColumn, _line, _column, unitName);

    private static GesToken CreateWordToken(string text, int line, int column, int endLine, int endColumn)
    {
        return text switch
        {
            "on" => new GesToken(GesTokenKind.On, text, line, column, endLine, endColumn),
            "record" => new GesToken(GesTokenKind.Record, text, line, column, endLine, endColumn),
            "predicate" => new GesToken(GesTokenKind.Predicate, text, line, column, endLine, endColumn),
            "function" => new GesToken(GesTokenKind.Function, text, line, column, endLine, endColumn),
            "emit" => new GesToken(GesTokenKind.Emit, text, line, column, endLine, endColumn),
            "publish" => new GesToken(GesTokenKind.Publish, text, line, column, endLine, endColumn),
            "matching" => new GesToken(GesTokenKind.Matching, text, line, column, endLine, endColumn),
            "without" => new GesToken(GesTokenKind.Without, text, line, column, endLine, endColumn),
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
            "numeric" => new GesToken(GesTokenKind.Numeric, text, line, column, endLine, endColumn),
            "or" => new GesToken(GesTokenKind.OperatorOr, text, line, column, endLine, endColumn),
            "xor" => new GesToken(GesTokenKind.OperatorXor, text, line, column, endLine, endColumn),
            "and" => new GesToken(GesTokenKind.OperatorAnd, text, line, column, endLine, endColumn),
            "div" => new GesToken(GesTokenKind.OperatorIntegerDivide, text, line, column, endLine, endColumn),
            "mod" => new GesToken(GesTokenKind.OperatorModulo, text, line, column, endLine, endColumn),
            "rem" => new GesToken(GesTokenKind.OperatorRemainder, text, line, column, endLine, endColumn),
            "not" => new GesToken(GesTokenKind.OperatorNot, text, line, column, endLine, endColumn),
            "to" => new GesToken(GesTokenKind.To, text, line, column, endLine, endColumn),
            "true" => new GesToken(GesTokenKind.True, text, line, column, endLine, endColumn),
            "false" => new GesToken(GesTokenKind.False, text, line, column, endLine, endColumn),
            "nothing" => new GesToken(GesTokenKind.Nothing, text, line, column, endLine, endColumn),
            "pi" => new GesToken(GesTokenKind.MathConstantPi, text, line, column, endLine, endColumn),
            "e" => new GesToken(GesTokenKind.MathConstantE, text, line, column, endLine, endColumn),
            "tau" => new GesToken(GesTokenKind.MathConstantTau, text, line, column, endLine, endColumn),
            "infinity" => new GesToken(GesTokenKind.MathConstantInfinity, text, line, column, endLine, endColumn),
            "abs" => new GesToken(GesTokenKind.IntrinsicAbs, text, line, column, endLine, endColumn),
            "ln" => new GesToken(GesTokenKind.IntrinsicLn, text, line, column, endLine, endColumn),
            "exp" => new GesToken(GesTokenKind.IntrinsicExp, text, line, column, endLine, endColumn),
            "sqrt" => new GesToken(GesTokenKind.IntrinsicSqrt, text, line, column, endLine, endColumn),
            "cbrt" => new GesToken(GesTokenKind.IntrinsicCbrt, text, line, column, endLine, endColumn),
            "chance" => new GesToken(GesTokenKind.IntrinsicChance, text, line, column, endLine, endColumn),
            "floor" => new GesToken(GesTokenKind.IntrinsicFloor, text, line, column, endLine, endColumn),
            "ceil" => new GesToken(GesTokenKind.IntrinsicCeil, text, line, column, endLine, endColumn),
            "truncate" => new GesToken(GesTokenKind.IntrinsicTruncate, text, line, column, endLine, endColumn),
            "rad" => new GesToken(GesTokenKind.IntrinsicRad, text, line, column, endLine, endColumn),
            "deg" => new GesToken(GesTokenKind.IntrinsicDeg, text, line, column, endLine, endColumn),
            "wrap" => new GesToken(GesTokenKind.IntrinsicWrap, text, line, column, endLine, endColumn),
            "round" => new GesToken(GesTokenKind.IntrinsicRound, text, line, column, endLine, endColumn),
            "sin" => new GesToken(GesTokenKind.IntrinsicSin, text, line, column, endLine, endColumn),
            "cos" => new GesToken(GesTokenKind.IntrinsicCos, text, line, column, endLine, endColumn),
            "tan" => new GesToken(GesTokenKind.IntrinsicTan, text, line, column, endLine, endColumn),
            "asin" => new GesToken(GesTokenKind.IntrinsicAsin, text, line, column, endLine, endColumn),
            "acos" => new GesToken(GesTokenKind.IntrinsicAcos, text, line, column, endLine, endColumn),
            "atan" => new GesToken(GesTokenKind.IntrinsicAtan, text, line, column, endLine, endColumn),
            "atan2" => new GesToken(GesTokenKind.IntrinsicAtan2, text, line, column, endLine, endColumn),
            "hypot" => new GesToken(GesTokenKind.IntrinsicHypot, text, line, column, endLine, endColumn),
            "distance" => new GesToken(GesTokenKind.IntrinsicDistance, text, line, column, endLine, endColumn),
            "squared" => new GesToken(GesTokenKind.IntrinsicSquared, text, line, column, endLine, endColumn),
            "length" => new GesToken(GesTokenKind.IntrinsicLength, text, line, column, endLine, endColumn),
            "normalize" => new GesToken(GesTokenKind.IntrinsicNormalize, text, line, column, endLine, endColumn),
            "dot" => new GesToken(GesTokenKind.IntrinsicDot, text, line, column, endLine, endColumn),
            "cross" => new GesToken(GesTokenKind.IntrinsicCross, text, line, column, endLine, endColumn),
            "angle" => new GesToken(GesTokenKind.IntrinsicAngle, text, line, column, endLine, endColumn),
            "random" => new GesToken(GesTokenKind.KeywordRandom, text, line, column, endLine, endColumn),
            "clamp" => new GesToken(GesTokenKind.Clamp, text, line, column, endLine, endColumn),
            "min" => new GesToken(GesTokenKind.Min, text, line, column, endLine, endColumn),
            "max" => new GesToken(GesTokenKind.Max, text, line, column, endLine, endColumn),
            "zip" => new GesToken(GesTokenKind.Zip, text, line, column, endLine, endColumn),
            "roll" => new GesToken(GesTokenKind.Roll, text, line, column, endLine, endColumn),
            "default" => new GesToken(GesTokenKind.Default, text, line, column, endLine, endColumn),
            "module" => new GesToken(GesTokenKind.Module, text, line, column, endLine, endColumn),
            _ when char.IsUpper(text[0]) => new GesToken(GesTokenKind.Message, text, line, column, endLine, endColumn),
            _ => new GesToken(GesTokenKind.Identifier, text, line, column, endLine, endColumn)
        };
    }

    private GesToken ReadWordLikeToken(int line, int column)
    {
        var start = _index;
        var first = Current;
        var word = ReadWhile(IsWordLetter);
        if (char.IsLower(first) && !IsAtEnd && Current == '_')
        {
            Advance();
            if (!ReadValidIdentifierSuffix())
            {
                while (!IsAtEnd && !char.IsWhiteSpace(Current))
                {
                    Advance();
                }

                return CreateToken(GesTokenKind.Illegal, _input[start.._index], line, column);
            }

            word = _input[start.._index];
        }
        else if (string.Equals(word, "d", StringComparison.Ordinal) && !IsAtEnd && char.IsDigit(Current))
        {
            return CreateWordToken(word, line, column, _line, _column);
        }
        else if (string.Equals(word, "atan", StringComparison.Ordinal) && !IsAtEnd && Current == '2')
        {
            Advance();
            word = _input[start.._index];
        }

        if (IsAtEnd || (!StartsAttachedIllegalOperatorSequence() && IsValidWordBoundary(Current))) return CreateWordToken(word, line, column, _line, _column);
        while (!IsAtEnd && !char.IsWhiteSpace(Current))
        {
            Advance();
        }

        return CreateToken(GesTokenKind.Illegal, _input[start.._index], line, column);
    }

    private bool ReadValidIdentifierSuffix()
    {
        if (IsAtEnd || !char.IsDigit(Current))
        {
            return false;
        }

        if (Current == '0')
        {
            Advance();
            return IsAtEnd || !char.IsDigit(Current);
        }

        ReadWhile(char.IsDigit);
        return true;
    }

    private GesToken ReadSelectorToken(int line, int column)
    {
        var start = _index;
        Advance();
        var selector = ReadWhile(char.IsLetter);

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

    private GesToken ReadTagToken(int line, int column)
    {
        var start = _index;
        Advance();
        var tagStart = _index;
        ReadWhile(IsWordLetter);
        if (!IsAtEnd && Current == '_')
        {
            Advance();
            if (!ReadValidIdentifierSuffix())
            {
                while (!IsAtEnd && !char.IsWhiteSpace(Current))
                {
                    Advance();
                }

                return CreateToken(GesTokenKind.Illegal, _input[start.._index], line, column);
            }
        }

        if (_index == tagStart)
        {
            return CreateToken(GesTokenKind.Illegal, _input[start.._index], line, column);
        }

        if (IsAtEnd || (!StartsAttachedIllegalOperatorSequence() && IsValidWordBoundary(Current)))
            return CreateToken(GesTokenKind.Tag, _input[start.._index], line, column);

        while (!IsAtEnd && !char.IsWhiteSpace(Current))
        {
            Advance();
        }

        return CreateToken(GesTokenKind.Illegal, _input[start.._index], line, column);
    }

    private GesToken ReadNumberLikeToken(int line, int column)
    {
        var start = _index;
        ReadNumberDigits();
        if (!IsAtEnd && Current == '.' && char.IsDigit(Peek()))
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
            if (IsAtEnd || IsValidNumberBoundary(Current)) return CreateUnitNumberToken(text, "degree", line, column);
            while (!IsAtEnd && !char.IsWhiteSpace(Current))
            {
                Advance();
            }

            return CreateToken(GesTokenKind.Illegal, _input[start.._index], line, column);
        }

        if (!IsAtEnd &&
            Current is 'm' or 's' &&
            IsValidUnitBoundary(Peek()))
        {
            var unitName = Current == 'm' ? "meter" : "second";
            Advance();
            text = _input[start..(_index - 1)];
            return CreateUnitNumberToken(text, unitName, line, column);
        }

        if (!IsAtEnd && char.IsLetter(Current)) return CreateToken(GesTokenKind.Float, text, line, column);

        if (IsAtEnd || IsValidNumberBoundary(Current)) return CreateToken(GesTokenKind.Float, text, line, column);
        while (!IsAtEnd && !char.IsWhiteSpace(Current))
        {
            Advance();
        }

        return CreateToken(GesTokenKind.Illegal, _input[start.._index], line, column);
    }

    private void ReadNumberDigits()
    {
        while (!IsAtEnd)
        {
            if (char.IsDigit(Current))
            {
                Advance();
                continue;
            }

            if (Current == '_' && char.IsDigit(Peek()))
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
        var quote = Current;
        Advance();
        var builder = new StringBuilder();
        while (!IsAtEnd)
        {
            if (Current == quote)
            {
                if (Peek() == quote)
                {
                    builder.Append(quote);
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
                return CreateToken(GesTokenKind.OperatorNotEqual, "<>", line, column);
            case '<' when next == '=':
                Advance();
                return CreateToken(GesTokenKind.OperatorLessOrEqual, "<=", line, column);
            case '>' when next == '=':
                Advance();
                return CreateToken(GesTokenKind.OperatorGreaterOrEqual, ">=", line, column);
            case '-' when next == '>':
                Advance();
                return CreateToken(GesTokenKind.OperatorImplication, "->", line, column);
            case '=' when next == '>':
                Advance();
                return CreateToken(GesTokenKind.ProjectionArrow, "=>", line, column);
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

    private static bool IsValidUnitBoundary(char ch) => ch == '\0' || char.IsWhiteSpace(ch) || IsStructuralBoundary(ch);

    private static bool IsWordStart(char ch) => (char.IsLower(ch) || char.IsUpper(ch)) && !IsUnicodeTagAlias(ch);

    private static bool IsWordLetter(char ch) => char.IsLetter(ch) && !IsUnicodeTagAlias(ch);

    private static bool IsUnicodeTagAlias(char ch)
        => ch is '\u220F' or '\u2107' or '\u03C4' or '\u03C6' or '\u221A' or '\u221B';

    private static bool IsStructuralBoundary(char ch)
        => ch is '(' or ')' or '{' or '}' or '[' or ']' or ',' or ';' or '.' or ':' or '#' or '+' or '-' or '*' or '/' or '!' or '~' or '&' or '|' or '^' or '=' or '<' or '>' or
            '\u00B7' or '\u00D7' or '\u00F7' or '\u2212' or '\u221E' or '\u220F' or '\u2107' or '\u03C4' or '\u03C6' or '\u221A' or '\u221B' or '\u00B0' or
            '\u2227' or '\u2228' or '\u2208' or '\u2209' or '\u2295' or '\u22C5' or
            '\u2264' or '\u2265' or '\u00AC' or '\u2260' or '\u2248' or '\u2245' or
            '\u2192' or '\u21D2' or '\u21A6' or '\u00B2' or '\u00B3';

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
