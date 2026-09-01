using System;
using System.Globalization;
using System.Text;
using StepH.GameEventScript.Runtime;

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

    public long? GetIntegerValue()
    {
        if (Kind is GesTokenKind.Float or GesTokenKind.UnitNumber &&
            Text.IndexOf('.') < 0 &&
            long.TryParse(NormalizeNumericText(Text), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
        {
            return value;
        }

        return null;
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
    private bool _previousWasCarriageReturn;

    public GesLexer(string input)
    {
        _input = GameEventScriptText.PrepareSource(input ?? throw new ArgumentNullException(nameof(input)), nameof(input));
        _length = _input.Length;
    }

    public GesToken ReadNextToken()
    {
        while (true)
        {
            SkipTriviaExceptNewLine();

            if (IsAtEnd)
            {
                return new GesToken(GesTokenKind.EndOfFile, string.Empty, _line, _column, _line, _column);
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

            if (IsWordStart(ch))
            {
                return ReadWordLikeToken(startLine, startColumn);
            }

            if (GameEventScriptText.IsAsciiDigit(ch))
            {
                return ReadNumberLikeToken(startLine, startColumn);
            }

            switch (ch)
            {
                case '\'':
                case '"':
                    return ReadTextToken(startLine, startColumn);
                case ':' when GameEventScriptText.IsAsciiLower(Peek()):
                    return ReadSelectorToken(startLine, startColumn);
                case '#' when GameEventScriptText.IsAsciiLower(Peek()):
                    return ReadTagToken(startLine, startColumn);
                default:
                    return ReadOperatorToken(startLine, startColumn);
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
        _index += GameEventScriptText.ScalarUtf16LengthAt(_input, _index);
    }

    private void ReadWordLetters()
    {
        while (!IsAtEnd && IsWordLetter(Current))
        {
            Advance();
        }
    }

    private void ReadDigits()
    {
        while (!IsAtEnd && GameEventScriptText.IsAsciiDigit(Current))
        {
            Advance();
        }
    }

    private GesToken CreateToken(GesTokenKind kind, string text, int startLine, int startColumn)
        => new(kind, text, startLine, startColumn, _line, _column);

    private GesToken CreateUnitNumberToken(string text, string unitName, int startLine, int startColumn)
        => new(GesTokenKind.UnitNumber, text, startLine, startColumn, _line, _column, unitName);

    private GesToken CreateWordToken(int start, int length, int line, int column, int endLine, int endColumn)
    {
        if (IsWordAt(start, length, "on")) return new GesToken(GesTokenKind.On, "on", line, column, endLine, endColumn);
        if (IsWordAt(start, length, "record")) return new GesToken(GesTokenKind.Record, "record", line, column, endLine, endColumn);
        if (IsWordAt(start, length, "predicate")) return new GesToken(GesTokenKind.Predicate, "predicate", line, column, endLine, endColumn);
        if (IsWordAt(start, length, "function")) return new GesToken(GesTokenKind.Function, "function", line, column, endLine, endColumn);
        if (IsWordAt(start, length, "emit")) return new GesToken(GesTokenKind.Emit, "emit", line, column, endLine, endColumn);
        if (IsWordAt(start, length, "publish")) return new GesToken(GesTokenKind.Publish, "publish", line, column, endLine, endColumn);
        if (IsWordAt(start, length, "matching")) return new GesToken(GesTokenKind.Matching, "matching", line, column, endLine, endColumn);
        if (IsWordAt(start, length, "without")) return new GesToken(GesTokenKind.Without, "without", line, column, endLine, endColumn);
        if (IsWordAt(start, length, "let")) return new GesToken(GesTokenKind.Let, "let", line, column, endLine, endColumn);
        if (IsWordAt(start, length, "as")) return new GesToken(GesTokenKind.As, "as", line, column, endLine, endColumn);
        if (IsWordAt(start, length, "be")) return new GesToken(GesTokenKind.Be, "be", line, column, endLine, endColumn);
        if (IsWordAt(start, length, "when")) return new GesToken(GesTokenKind.When, "when", line, column, endLine, endColumn);
        if (IsWordAt(start, length, "otherwise")) return new GesToken(GesTokenKind.Otherwise, "otherwise", line, column, endLine, endColumn);
        if (IsWordAt(start, length, "has")) return new GesToken(GesTokenKind.Has, "has", line, column, endLine, endColumn);
        if (IsWordAt(start, length, "empty")) return new GesToken(GesTokenKind.Empty, "empty", line, column, endLine, endColumn);
        if (IsWordAt(start, length, "if")) return new GesToken(GesTokenKind.If, "if", line, column, endLine, endColumn);
        if (IsWordAt(start, length, "else")) return new GesToken(GesTokenKind.Else, "else", line, column, endLine, endColumn);
        if (IsWordAt(start, length, "for")) return new GesToken(GesTokenKind.For, "for", line, column, endLine, endColumn);
        if (IsWordAt(start, length, "in")) return new GesToken(GesTokenKind.In, "in", line, column, endLine, endColumn);
        if (IsWordAt(start, length, "starts")) return new GesToken(GesTokenKind.Starts, "starts", line, column, endLine, endColumn);
        if (IsWordAt(start, length, "ends")) return new GesToken(GesTokenKind.Ends, "ends", line, column, endLine, endColumn);
        if (IsWordAt(start, length, "with")) return new GesToken(GesTokenKind.With, "with", line, column, endLine, endColumn);
        if (IsWordAt(start, length, "is")) return new GesToken(GesTokenKind.Is, "is", line, column, endLine, endColumn);
        if (IsWordAt(start, length, "numeric")) return new GesToken(GesTokenKind.Numeric, "numeric", line, column, endLine, endColumn);
        if (IsWordAt(start, length, "or")) return new GesToken(GesTokenKind.OperatorOr, "or", line, column, endLine, endColumn);
        if (IsWordAt(start, length, "xor")) return new GesToken(GesTokenKind.OperatorXor, "xor", line, column, endLine, endColumn);
        if (IsWordAt(start, length, "and")) return new GesToken(GesTokenKind.OperatorAnd, "and", line, column, endLine, endColumn);
        if (IsWordAt(start, length, "div")) return new GesToken(GesTokenKind.OperatorIntegerDivide, "div", line, column, endLine, endColumn);
        if (IsWordAt(start, length, "mod")) return new GesToken(GesTokenKind.OperatorModulo, "mod", line, column, endLine, endColumn);
        if (IsWordAt(start, length, "rem")) return new GesToken(GesTokenKind.OperatorRemainder, "rem", line, column, endLine, endColumn);
        if (IsWordAt(start, length, "not")) return new GesToken(GesTokenKind.OperatorNot, "not", line, column, endLine, endColumn);
        if (IsWordAt(start, length, "to")) return new GesToken(GesTokenKind.To, "to", line, column, endLine, endColumn);
        if (IsWordAt(start, length, "true")) return new GesToken(GesTokenKind.True, "true", line, column, endLine, endColumn);
        if (IsWordAt(start, length, "false")) return new GesToken(GesTokenKind.False, "false", line, column, endLine, endColumn);
        if (IsWordAt(start, length, "nothing")) return new GesToken(GesTokenKind.Nothing, "nothing", line, column, endLine, endColumn);
        if (IsWordAt(start, length, "pi")) return new GesToken(GesTokenKind.MathConstantPi, "pi", line, column, endLine, endColumn);
        if (IsWordAt(start, length, "e")) return new GesToken(GesTokenKind.MathConstantE, "e", line, column, endLine, endColumn);
        if (IsWordAt(start, length, "tau")) return new GesToken(GesTokenKind.MathConstantTau, "tau", line, column, endLine, endColumn);
        if (IsWordAt(start, length, "infinity")) return new GesToken(GesTokenKind.MathConstantInfinity, "infinity", line, column, endLine, endColumn);
        if (IsWordAt(start, length, "abs")) return new GesToken(GesTokenKind.IntrinsicAbs, "abs", line, column, endLine, endColumn);
        if (IsWordAt(start, length, "ln")) return new GesToken(GesTokenKind.IntrinsicLn, "ln", line, column, endLine, endColumn);
        if (IsWordAt(start, length, "exp")) return new GesToken(GesTokenKind.IntrinsicExp, "exp", line, column, endLine, endColumn);
        if (IsWordAt(start, length, "sqrt")) return new GesToken(GesTokenKind.IntrinsicSqrt, "sqrt", line, column, endLine, endColumn);
        if (IsWordAt(start, length, "cbrt")) return new GesToken(GesTokenKind.IntrinsicCbrt, "cbrt", line, column, endLine, endColumn);
        if (IsWordAt(start, length, "chance")) return new GesToken(GesTokenKind.IntrinsicChance, "chance", line, column, endLine, endColumn);
        if (IsWordAt(start, length, "floor")) return new GesToken(GesTokenKind.IntrinsicFloor, "floor", line, column, endLine, endColumn);
        if (IsWordAt(start, length, "ceil")) return new GesToken(GesTokenKind.IntrinsicCeil, "ceil", line, column, endLine, endColumn);
        if (IsWordAt(start, length, "truncate")) return new GesToken(GesTokenKind.IntrinsicTruncate, "truncate", line, column, endLine, endColumn);
        if (IsWordAt(start, length, "rad")) return new GesToken(GesTokenKind.IntrinsicRad, "rad", line, column, endLine, endColumn);
        if (IsWordAt(start, length, "deg")) return new GesToken(GesTokenKind.IntrinsicDeg, "deg", line, column, endLine, endColumn);
        if (IsWordAt(start, length, "wrap")) return new GesToken(GesTokenKind.IntrinsicWrap, "wrap", line, column, endLine, endColumn);
        if (IsWordAt(start, length, "round")) return new GesToken(GesTokenKind.IntrinsicRound, "round", line, column, endLine, endColumn);
        if (IsWordAt(start, length, "sin")) return new GesToken(GesTokenKind.IntrinsicSin, "sin", line, column, endLine, endColumn);
        if (IsWordAt(start, length, "cos")) return new GesToken(GesTokenKind.IntrinsicCos, "cos", line, column, endLine, endColumn);
        if (IsWordAt(start, length, "tan")) return new GesToken(GesTokenKind.IntrinsicTan, "tan", line, column, endLine, endColumn);
        if (IsWordAt(start, length, "asin")) return new GesToken(GesTokenKind.IntrinsicAsin, "asin", line, column, endLine, endColumn);
        if (IsWordAt(start, length, "acos")) return new GesToken(GesTokenKind.IntrinsicAcos, "acos", line, column, endLine, endColumn);
        if (IsWordAt(start, length, "atan")) return new GesToken(GesTokenKind.IntrinsicAtan, "atan", line, column, endLine, endColumn);
        if (IsWordAt(start, length, "atan2")) return new GesToken(GesTokenKind.IntrinsicAtan2, "atan2", line, column, endLine, endColumn);
        if (IsWordAt(start, length, "hypot")) return new GesToken(GesTokenKind.IntrinsicHypot, "hypot", line, column, endLine, endColumn);
        if (IsWordAt(start, length, "distance")) return new GesToken(GesTokenKind.IntrinsicDistance, "distance", line, column, endLine, endColumn);
        if (IsWordAt(start, length, "squared")) return new GesToken(GesTokenKind.IntrinsicSquared, "squared", line, column, endLine, endColumn);
        if (IsWordAt(start, length, "length")) return new GesToken(GesTokenKind.IntrinsicLength, "length", line, column, endLine, endColumn);
        if (IsWordAt(start, length, "normalize")) return new GesToken(GesTokenKind.IntrinsicNormalize, "normalize", line, column, endLine, endColumn);
        if (IsWordAt(start, length, "dot")) return new GesToken(GesTokenKind.IntrinsicDot, "dot", line, column, endLine, endColumn);
        if (IsWordAt(start, length, "cross")) return new GesToken(GesTokenKind.IntrinsicCross, "cross", line, column, endLine, endColumn);
        if (IsWordAt(start, length, "angle")) return new GesToken(GesTokenKind.IntrinsicAngle, "angle", line, column, endLine, endColumn);
        if (IsWordAt(start, length, "random")) return new GesToken(GesTokenKind.KeywordRandom, "random", line, column, endLine, endColumn);
        if (IsWordAt(start, length, "series")) return new GesToken(GesTokenKind.Series, "series", line, column, endLine, endColumn);
        if (IsWordAt(start, length, "fibonacci")) return new GesToken(GesTokenKind.Fibonacci, "fibonacci", line, column, endLine, endColumn);
        if (IsWordAt(start, length, "factorial")) return new GesToken(GesTokenKind.Factorial, "factorial", line, column, endLine, endColumn);
        if (IsWordAt(start, length, "clamp")) return new GesToken(GesTokenKind.Clamp, "clamp", line, column, endLine, endColumn);
        if (IsWordAt(start, length, "min")) return new GesToken(GesTokenKind.Min, "min", line, column, endLine, endColumn);
        if (IsWordAt(start, length, "max")) return new GesToken(GesTokenKind.Max, "max", line, column, endLine, endColumn);
        if (IsWordAt(start, length, "zip")) return new GesToken(GesTokenKind.Zip, "zip", line, column, endLine, endColumn);
        if (IsWordAt(start, length, "roll")) return new GesToken(GesTokenKind.Roll, "roll", line, column, endLine, endColumn);
        if (IsWordAt(start, length, "default")) return new GesToken(GesTokenKind.Default, "default", line, column, endLine, endColumn);
        if (IsWordAt(start, length, "module")) return new GesToken(GesTokenKind.Module, "module", line, column, endLine, endColumn);

        var text = _input[start..(start + length)];
        return GameEventScriptText.IsAsciiUpper(text[0])
            ? new GesToken(GesTokenKind.Message, text, line, column, endLine, endColumn)
            : new GesToken(GesTokenKind.Identifier, text, line, column, endLine, endColumn);
    }

    private bool IsWordAt(int start, int length, string word)
        => length == word.Length && string.CompareOrdinal(_input, start, word, 0, length) == 0;

    private GesToken ReadWordLikeToken(int line, int column)
    {
        var start = _index;
        var first = Current;
        ReadWordLetters();
        if (GameEventScriptText.IsAsciiLower(first) && !IsAtEnd && Current == '_')
        {
            Advance();
            if (!ReadValidIdentifierSuffix())
            {
                while (!IsAtEnd && !GameEventScriptText.IsTokenWhitespace(Current))
                {
                    Advance();
                }

                return CreateToken(GesTokenKind.Illegal, _input[start.._index], line, column);
            }

            if (IsAtEnd || (!StartsAttachedIllegalOperatorSequence() && IsValidWordBoundary(Current)))
            {
                return CreateToken(GesTokenKind.Identifier, _input[start.._index], line, column);
            }
        }
        else if (_index - start == 1 && _input[start] == 'd' && !IsAtEnd && GameEventScriptText.IsAsciiDigit(Current))
        {
            return CreateWordToken(start, 1, line, column, _line, _column);
        }
        else if (_index - start == 4 && IsWordAt(start, 4, "atan") && !IsAtEnd && Current == '2')
        {
            Advance();
        }

        if (IsAtEnd || (!StartsAttachedIllegalOperatorSequence() && IsValidWordBoundary(Current))) return CreateWordToken(start, _index - start, line, column, _line, _column);
        while (!IsAtEnd && !GameEventScriptText.IsTokenWhitespace(Current))
        {
            Advance();
        }

        return CreateToken(GesTokenKind.Illegal, _input[start.._index], line, column);
    }

    private bool ReadValidIdentifierSuffix()
    {
        if (IsAtEnd || !GameEventScriptText.IsAsciiDigit(Current))
        {
            return false;
        }

        if (Current == '0')
        {
            Advance();
            return IsAtEnd || !GameEventScriptText.IsAsciiDigit(Current);
        }

        ReadDigits();
        return true;
    }

    private GesToken ReadSelectorToken(int line, int column)
    {
        var start = _index;
        Advance();
        var selectorStart = _index;
        ReadWordLetters();
        var selectorLength = _index - selectorStart;

        if (IsAtEnd || (!StartsAttachedIllegalOperatorSequence() && IsValidWordBoundary(Current)))
        {
            if (IsWordAt(selectorStart, selectorLength, "any")) return CreateToken(GesTokenKind.SelectorAny, ":any", line, column);
            if (IsWordAt(selectorStart, selectorLength, "all")) return CreateToken(GesTokenKind.SelectorAll, ":all", line, column);
            if (IsWordAt(selectorStart, selectorLength, "filter")) return CreateToken(GesTokenKind.SelectorFilter, ":filter", line, column);
            if (IsWordAt(selectorStart, selectorLength, "has")) return CreateToken(GesTokenKind.SelectorHas, ":has", line, column);
            if (IsWordAt(selectorStart, selectorLength, "take")) return CreateToken(GesTokenKind.SelectorTake, ":take", line, column);
            if (IsWordAt(selectorStart, selectorLength, "drop")) return CreateToken(GesTokenKind.SelectorDrop, ":drop", line, column);
            if (IsWordAt(selectorStart, selectorLength, "count")) return CreateToken(GesTokenKind.SelectorCount, ":count", line, column);
            if (IsWordAt(selectorStart, selectorLength, "choose")) return CreateToken(GesTokenKind.SelectorChoose, ":choose", line, column);
            if (IsWordAt(selectorStart, selectorLength, "draw")) return CreateToken(GesTokenKind.SelectorDraw, ":draw", line, column);
            if (IsWordAt(selectorStart, selectorLength, "shuffle")) return CreateToken(GesTokenKind.SelectorShuffle, ":shuffle", line, column);
            if (IsWordAt(selectorStart, selectorLength, "reverse")) return CreateToken(GesTokenKind.SelectorReverse, ":reverse", line, column);
            if (IsWordAt(selectorStart, selectorLength, "sum")) return CreateToken(GesTokenKind.SelectorSum, ":sum", line, column);
            if (IsWordAt(selectorStart, selectorLength, "average")) return CreateToken(GesTokenKind.SelectorAverage, ":average", line, column);
            if (IsWordAt(selectorStart, selectorLength, "select")) return CreateToken(GesTokenKind.SelectorSelect, ":select", line, column);
            if (IsWordAt(selectorStart, selectorLength, "contains")) return CreateToken(GesTokenKind.SelectorContains, ":contains", line, column);
            if (IsWordAt(selectorStart, selectorLength, "sort")) return CreateToken(GesTokenKind.SelectorSort, ":sort", line, column);
            return CreateToken(GesTokenKind.Tag, _input[start.._index], line, column);
        }

        while (!IsAtEnd && !GameEventScriptText.IsTokenWhitespace(Current))
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
        ReadWordLetters();
        if (!IsAtEnd && Current == '_')
        {
            Advance();
            if (!ReadValidIdentifierSuffix())
            {
                while (!IsAtEnd && !GameEventScriptText.IsTokenWhitespace(Current))
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

        while (!IsAtEnd && !GameEventScriptText.IsTokenWhitespace(Current))
        {
            Advance();
        }

        return CreateToken(GesTokenKind.Illegal, _input[start.._index], line, column);
    }

    private GesToken ReadNumberLikeToken(int line, int column)
    {
        var start = _index;
        ReadNumberDigits();
        if (!IsAtEnd && Current == '.' && GameEventScriptText.IsAsciiDigit(Peek()))
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
            while (!IsAtEnd && !GameEventScriptText.IsTokenWhitespace(Current))
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
            while (!IsAtEnd && !GameEventScriptText.IsTokenWhitespace(Current))
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

        if (!IsAtEnd && GameEventScriptText.IsAsciiLetter(Current)) return CreateToken(GesTokenKind.Float, text, line, column);

        if (IsAtEnd || IsValidNumberBoundary(Current)) return CreateToken(GesTokenKind.Float, text, line, column);
        while (!IsAtEnd && !GameEventScriptText.IsTokenWhitespace(Current))
        {
            Advance();
        }

        return CreateToken(GesTokenKind.Illegal, _input[start.._index], line, column);
    }

    private void ReadNumberDigits()
    {
        while (!IsAtEnd)
        {
            if (GameEventScriptText.IsAsciiDigit(Current))
            {
                Advance();
                continue;
            }

            if (Current == '_' && GameEventScriptText.IsAsciiDigit(Peek()))
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
            if (char.IsHighSurrogate(Current)) builder.Append(Peek());
            Advance();
        }

        return CreateToken(GesTokenKind.Illegal, _input[start.._index], line, column);
    }

    private GesToken ReadOperatorToken(int line, int column)
    {
        var ch = Current;
        var next = Peek();
        var text = GameEventScriptText.ScalarAtUtf16Offset(_input, _index);
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
                    _ => CreateToken(GesTokenKind.Illegal, text, line, column)
                };
        }
    }

    private void SkipTriviaExceptNewLine()
    {
        while (!IsAtEnd)
        {
            if (GameEventScriptText.IsInlineWhitespace(Current))
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

    private static bool IsValidWordBoundary(char ch) => GameEventScriptText.IsTokenWhitespace(ch) || IsStructuralBoundary(ch);

    private bool IsValidNumberBoundary(char ch) => GameEventScriptText.IsTokenWhitespace(ch) || IsStructuralBoundary(ch) || (ch == 'd' && GameEventScriptText.IsAsciiDigit(Peek()));

    private static bool IsValidUnitBoundary(char ch) => ch == '\0' || GameEventScriptText.IsTokenWhitespace(ch) || IsStructuralBoundary(ch);

    private static bool IsWordStart(char ch) => GameEventScriptText.IsAsciiLetter(ch);

    private static bool IsWordLetter(char ch) => GameEventScriptText.IsAsciiLetter(ch);

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
