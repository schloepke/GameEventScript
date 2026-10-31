// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Text;
using static GameEventScript.Syntax.GesTokenKind;

namespace GameEventScript.Syntax;

// Parses one source into its lossless syntax tree and reports its syntax errors. After an error, parsing resumes at the
// next declaration or statement, and the skipped tokens form an error node. The parser keeps of each expression only
// what the syntax rules test: its source nesting depth, its range for a nesting diagnostic, and a few literal shapes.
internal sealed partial class GesSyntaxParser
{
    private const int MaximumExpressionDepth = 32;
    private const int MaximumStatementDepth = 32;

    internal static GesSyntaxTree Parse(string text)
    {
        _ = text ?? throw new ArgumentNullException(nameof(text));
        var parser = new GesSyntaxParser(new GesTokenReader(new GesLexer(text)), text.Length);
        try
        {
            parser.ParseScript();
        }
        catch (NestingLimitException)
        {
            // The nesting limit ends parsing at once; the syntax errors found before it stay reported.
        }

        return parser.CompleteTree(text);
    }

    private readonly GesTokenReader _reader;
    private readonly GesSyntaxTreeBuilder _tree;
    private readonly List<GesSyntaxDiagnostic> _errors = [];
    private string _moduleName = string.Empty;
    private int _expressionNesting;
    private int _statementNesting;
    private int _delimiterDepth;
    private int? _forSourceDepth;
    private int? _choiceConditionDepth;

    private GesSyntaxParser(GesTokenReader reader, int sourceLength)
    {
        _reader = reader;
        _tree = new GesSyntaxTreeBuilder(sourceLength);
    }

    private sealed class ParseException(string message, int line, int column, int endLine, int endColumn) : Exception($"{message} (line {line}, col {column})")
    {
        public int Line { get; } = line;
        public int Column { get; } = column;
        public int EndLine { get; } = endLine;
        public int EndColumn { get; } = endColumn;

        public ParseException(string message, int line, int column) : this(message, line, column, line, column)
        {
        }

        public ParseException(string message, GesToken token) : this(message, token.Line, token.Column, token.EndLine, token.EndColumn)
        {
        }
    }

    private sealed class NestingLimitException : Exception;

    // Literal shapes that syntax rules test: number literals before implicit multiplication, scalar literals in constants.
    private enum Shape : byte
    {
        None,
        Number,
        Numeric,
        Scalar,
        NegatedNumeric
    }

    // An expression as the parser keeps it: its source nesting depth, the tokens its range spans (-1 without a range)
    // and its literal shape.
    private readonly struct Expression(int depth, int start, int end, Shape shape)
    {
        internal int Depth { get; } = depth;

        internal int Start { get; } = start;

        internal int End { get; } = end;

        internal Shape Shape { get; } = shape;

        internal Expression Nested() => new(Depth + 1, Start, End, Shape);
    }

    // The index the current token takes in the tree once consumed, and the index of the previous one.
    private int CurrentIndex => _tree.TokenCount;

    private int PreviousIndex => _tree.TokenCount - 1;

    private GesToken TokenAt(int index) => index < _tree.TokenCount ? _tree.TokenAt(index) : Current;

    // A node with the given greatest child depth, ranging from `start` to the previous token.
    private Expression Node(int childDepth, int start, Shape shape = Shape.None) => Checked(new Expression(childDepth + 1, start, PreviousIndex, shape));

    // A node ranging from the start of `first` to the end of `last`, or without a range when either has none.
    private Expression Merged(int childDepth, Expression first, Expression? last = null)
    {
        var end = (last ?? first).End;
        return first.Start < 0 || end < 0 ? Checked(new Expression(childDepth + 1, -1, -1, Shape.None)) : Checked(new Expression(childDepth + 1, first.Start, end, Shape.None));
    }

    private Expression Leaf(int index, Shape shape = Shape.None) => new(1, index, index, shape);

    private Expression Checked(Expression expression)
    {
        if (expression.Depth > MaximumExpressionDepth)
        {
            if (expression.Start < 0) ThrowNestingLimit(Current, Current);
            ThrowNestingLimit(TokenAt(expression.Start), TokenAt(expression.End));
        }

        return expression;
    }

    private static int Deepest(int first, int second) => Math.Max(first, second);

    private static int Deepest(int first, int second, int third) => Math.Max(first, Math.Max(second, third));

    // Ends parsing at once, after the syntax errors already collected in this source.
    private void ThrowNestingLimit(GesToken start, GesToken end)
    {
        _errors.Add(new GesSyntaxDiagnostic(GesSyntaxDiagnostic.SourceNestingExceeded, "Source exceeds the portable expression or statement nesting limit.",
            start.Line, start.Column, end.EndLine, end.EndColumn, _moduleName));
        throw new NestingLimitException();
    }

    private void ThrowNestingLimitAtCurrent() => ThrowNestingLimit(Current, Current);

    // Closes the open nodes, attaches the tokens a failure left unread as an error node and ends with EndOfFile.
    private GesSyntaxTree CompleteTree(string text)
    {
        _tree.FinishTo(1);
        if (!Is(EndOfFile))
        {
            _tree.StartNode(GesSyntaxNodeKind.Error);
            while (!Is(EndOfFile)) Advance();
            _tree.FinishNode();
        }

        _tree.Token(Current);
        return _tree.Finish(text, _errors);
    }

    // Wraps everything parsed since the checkpoint into one node, for nodes whose kind is known only at their end.
    private T WrapNode<T>(GesSyntaxTreeBuilder.Checkpoint start, GesSyntaxNodeKind kind, T value)
    {
        _tree.StartNodeAt(start, kind);
        _tree.FinishNode();
        return value;
    }

    private T FinishNode<T>(T value)
    {
        _tree.FinishNode();
        return value;
    }

    // Recovery skips tokens until a synchronization point; they form one error node.
    private bool RecoverInto(int depth, Func<bool> synchronize)
    {
        _tree.FinishTo(depth);
        _tree.StartNode(GesSyntaxNodeKind.Error);
        var result = synchronize();
        _tree.FinishNode();
        return result;
    }

    private void ParseScript()
    {
        _tree.StartNode(GesSyntaxNodeKind.CompilationUnit);
        SkipStatementSeparators();
        try
        {
            ParseOptionalModuleDeclaration();
        }
        catch (ParseException ex)
        {
            AddParseError(ex);
            RecoverInto(1, () => { SynchronizeTopLevel(); return true; });
        }

        SkipStatementSeparators();
        while (!Is(EndOfFile))
        {
            var declarationStart = Current;
            try
            {
                _tree.StartNode(Current.Kind switch
                {
                    Constant => GesSyntaxNodeKind.ConstantDefinition,
                    Record => GesSyntaxNodeKind.RecordDefinition,
                    Predicate => GesSyntaxNodeKind.PredicateDefinition,
                    Function => GesSyntaxNodeKind.FunctionDefinition,
                    Module => GesSyntaxNodeKind.ModuleDeclaration,
                    Illegal => GesSyntaxNodeKind.Error,
                    _ => GesSyntaxNodeKind.EventHandler
                });
                if (Is(Illegal))
                {
                    var token = Advance();
                    throw new ParseException($"Illegal token '{token.Text}'", token);
                }

                if (Match(Module)) throw new ParseException("A module declaration must precede all other declarations and appear once.", Previous);
                if (Match(Constant)) ParseConstantDefinition();
                else if (Match(Record)) ParseTypeDefinition();
                else if (Match(Predicate) || Match(Function)) ParseCallableDefinition();
                else ParseEventHandler();
                _tree.FinishNode();

                RequireHandlerSeparatorOrEndOfFile();
                SkipStatementSeparators();
            }
            catch (ParseException ex)
            {
                AddParseError(ex);
                RecoverInto(1, () =>
                {
                    // A declaration that fails on its first token must still move on, or recovery would stop there again.
                    if (Current.Line == declarationStart.Line && Current.Column == declarationStart.Column) Advance();
                    SynchronizeTopLevel();
                    return true;
                });
            }
        }
    }

    private void ParseOptionalModuleDeclaration()
    {
        if (!Is(Module)) return;
        _tree.StartNode(GesSyntaxNodeKind.ModuleDeclaration);
        Advance();

        SkipNewLines();
        ThrowIfIllegalToken();
        if (!Is(Identifier)) throw new ParseException($"Expected module name but found {Current.Kind}", Current.Line, Current.Column);

        var builder = new StringBuilder(Advance().Text);
        while (Match(Dot))
        {
            if (!Is(Identifier)) throw new ParseException($"Expected module segment but found {Current.Kind}", Current);
            builder.Append('.').Append(Advance().Text);
        }

        _moduleName = builder.ToString();
        if (!GesText.IsModuleName(_moduleName)) throw new ParseException($"Invalid module identifier '{_moduleName}'", Previous);
        _tree.FinishNode();
    }

    private void ParseConstantDefinition()
    {
        var nameToken = Expect(ConstantReference);
        Expect(Be);
        SkipNewLines();
        var value = ParseUnaryExpression();
        if (value.Shape == Shape.None) throw new ParseException("Constant initializers must be scalar literals.", nameToken);
    }

    private void ParseTypeDefinition()
    {
        ParseTypeName();
        Expect(As);
        SkipNewLines();
        Expect(LeftBrace);
        SkipStatementSeparators();

        while (!Is(RightBrace))
        {
            ParseTypeFieldDefinition();
            if (Match(Comma))
            {
                SkipStatementSeparators();
                continue;
            }

            SkipStatementSeparators();
        }

        Expect(RightBrace);
    }

    private void ParseTypeFieldDefinition()
    {
        _tree.StartNode(GesSyntaxNodeKind.RecordField);
        var startToken = Current;
        var isUnlabeledConstructorParameter = Match(Underscore);
        if (isUnlabeledConstructorParameter) SkipNewLines();

        ExpectIdentifier();
        Expect(Colon);
        SkipNewLines();
        ParseTypeName();
        SkipNewLines();

        if (MatchWord("clamped"))
        {
            ExpectWord("between");
            ParseEqualityExpression();
            Expect(OperatorAnd);
            ParseEqualityExpression();
            SkipNewLines();
        }

        var computed = MatchWord("computed");
        if (computed)
        {
            ExpectWord("by");
            ParseAssemblyOrExpression();
        }

        if (isUnlabeledConstructorParameter && computed) throw new ParseException("Computed record fields cannot be constructor parameters.", startToken);
        _tree.FinishNode();
    }

    private void ParseCallableDefinition()
    {
        ExpectIdentifier();
        ParseDefinitionParameters();
        Expect(Be);
        SkipNewLines();
        ParseAssemblyOrExpression();
    }

    private void ParseDefinitionParameters()
    {
        _tree.StartNode(GesSyntaxNodeKind.ParameterList);
        Expect(LeftParen);
        if (!Is(RightParen))
        {
            ParseParameter();
            while (Match(Comma)) ParseParameter();
        }

        Expect(RightParen);
        _tree.FinishNode();
    }

    private void ParseParameter(bool allowDeclaredType = true)
    {
        _tree.StartNode(GesSyntaxNodeKind.Parameter);
        if (Match(Underscore)) SkipNewLines();
        ExpectIdentifier();
        if (allowDeclaredType) ParseOptionalParameterDeclaredType();
        _tree.FinishNode();
    }

    private void ParseOptionalParameterDeclaredType()
    {
        SkipNewLines();
        if (!Match(As)) return;

        SkipNewLines();
        ParseTypeName();
    }

    private void ParseEventHandler()
    {
        Expect(On);
        SkipNewLines();
        ParseEventHandlerMessageName();

        if (Is(As))
        {
            _tree.StartNode(GesSyntaxNodeKind.Parameter);
            Advance();
            SkipNewLines();
            ExpectIdentifier();
            _tree.FinishNode();
        }
        else if (Is(LeftParen))
        {
            _tree.StartNode(GesSyntaxNodeKind.ParameterList);
            Advance();
            if (!Is(RightParen))
            {
                ParseParameter();
                while (Match(Comma)) ParseParameter();
            }

            Expect(RightParen);
            _tree.FinishNode();
        }

        ParseOptionalHandlerTagFilters();

        SkipNewLines();
        _tree.StartNode(GesSyntaxNodeKind.Block);
        Expect(LeftBrace);
        ParseStatementsUntil(RightBrace);
        Expect(RightBrace);
        _tree.FinishNode();
    }

    private void ParseEventHandlerMessageName()
    {
        ThrowIfIllegalToken();

        if (Is(Message))
        {
            Advance();
            return;
        }

        if (Current.Kind == Identifier && GesText.IsSystemEndpointName(Current.Text))
        {
            Advance();
            return;
        }

        var token = Current;
        throw new ParseException($"Expected {Message} or system endpoint but found {token.Kind}", token);
    }

    private void ParseOptionalHandlerTagFilters()
    {
        while (true)
        {
            SkipNewLines();
            if (!Is(Matching) && !Is(Without)) return;

            _tree.StartNode(GesSyntaxNodeKind.HandlerTagFilter);
            Advance();
            do
            {
                SkipNewLines();
                Expect(Tag);
                SkipNewLines();
            }
            while (Match(Comma));

            _tree.FinishNode();
        }
    }

    private void ParseStatementsUntil(GesTokenKind closingKind)
    {
        SkipStatementSeparators();

        while (!Is(closingKind))
        {
            if (Is(EndOfFile))
            {
                var token = Current;
                AddParseError(new ParseException($"Expected '{closingKind}' before end of input", token.Line, token.Column));
                return;
            }

            var depth = _tree.Depth;
            try
            {
                ParseStatement();
                RequireStatementSeparatorOrClosing(closingKind);
            }
            catch (ParseException ex)
            {
                AddParseError(ex);
                if (!RecoverInto(depth, () => SynchronizeStatement(closingKind))) return;
            }
        }
    }

    private void ParseStatement()
    {
        _tree.StartNode(IsWord("asm") ? GesSyntaxNodeKind.AssemblyBlock : IsSeededRandomStatementStart() ? GesSyntaxNodeKind.SeededRandomStatement : Current.Kind switch
        {
            Emit => GesSyntaxNodeKind.EmitStatement,
            Publish => GesSyntaxNodeKind.PublishStatement,
            Let => GesSyntaxNodeKind.LetStatement,
            If => GesSyntaxNodeKind.IfStatement,
            For => GesSyntaxNodeKind.ForStatement,
            _ => GesSyntaxNodeKind.ExpressionStatement
        });
        ParseStatementCore();
        _tree.FinishNode();
    }

    private void ParseStatementCore()
    {
        if (MatchWord("asm")) ParseAssemblyBlock(standalone: true);
        else if (IsSeededRandomStatementStart()) ParseSeededRandomStatement();
        else if (Match(Emit) || Match(Publish)) ParsePublishStatement();
        else if (Match(Let)) ParseLetStatement();
        else if (Match(If)) ParseIfStatement();
        else if (Match(For)) ParseForStatement();
        else ParseExpression();
    }

    private void ParsePublishStatement()
    {
        var startToken = PreviousIndex;
        SkipNewLines();
        if (IsWord("after"))
        {
            ParseSendExpression(startToken, _tree.Mark());
            return;
        }

        ParsePublishMessageExpression();
        ParseOptionalTagExpressions();
    }

    private Expression ParseSendExpression(int startToken, GesSyntaxTreeBuilder.Checkpoint start)
    {
        _tree.StartNodeAt(start, GesSyntaxNodeKind.SendExpression);
        SkipNewLines();
        var delay = MatchWord("after") ? ParseExpression().Depth : 0;
        var message = Current.Kind == Message ? ParseMessageLiteralExpressionCore() : ParseUnaryExpression();
        var tags = ParseOptionalTagExpressions(resultSend: true);
        return FinishNode(Node(Deepest(message.Depth, delay, tags), startToken));
    }

    private Expression ParsePublishMessageExpression() => Current.Kind == Message ? ParseMessageLiteralExpressionCore() : ParseExpression();

    // The greatest depth of the tag expressions after `with`, or zero without them.
    private int ParseOptionalTagExpressions(bool resultSend = false)
    {
        if (!Is(With)) return 0;

        _tree.StartNode(GesSyntaxNodeKind.TagClause);
        Advance();
        var depth = 0;
        do
        {
            SkipNewLines();
            depth = Deepest(depth, (resultSend ? ParseUnaryExpression() : ParseExpression()).Depth);
        }
        while (Match(Comma));

        return FinishNode(depth);
    }

    private int ParseArgument()
    {
        _tree.StartNode(GesSyntaxNodeKind.Argument);
        if (IsArgumentLabelStart())
        {
            ExpectArgumentLabel();
            SkipNewLines();
            Match(Colon);
            SkipNewLines();
        }

        return FinishNode(ParseExpression().Depth);
    }

    // The greatest argument depth of a list after its opening parenthesis.
    private int ParseArgumentListAfterLeftParen()
    {
        SkipNewLines();
        if (Match(RightParen)) return 0;

        var depth = ParseArgumentListAfterFirstArgument(ParseArgument(), RightParen);
        Expect(RightParen);
        return depth;
    }

    private int ParseArgumentListAfterFirstArgument(int depth, GesTokenKind closingKind)
    {
        while (true)
        {
            SkipNewLines();
            if (Match(Comma))
            {
                SkipNewLines();
                depth = Deepest(depth, ParseArgument());
                continue;
            }

            if (!Is(closingKind) && IsArgumentLabelStart())
            {
                depth = Deepest(depth, ParseArgument());
                continue;
            }

            return depth;
        }
    }

    private int ParseUngroupedLabeledArgumentList()
    {
        var depth = 0;
        do depth = Deepest(depth, ParseArgument()); while (IsArgumentLabelStart());
        return depth;
    }

    private int ParseOfArgumentList()
    {
        Expect(Of);
        SkipNewLines();
        var depth = ParseEqualityExpression().Depth;
        while (!IsForClauseStart() && Match(OperatorAnd))
        {
            SkipNewLines();
            depth = Deepest(depth, ParseEqualityExpression().Depth);
        }

        return depth;
    }

    private void ParseLetStatement()
    {
        ExpectIdentifier();
        if (Match(Be))
        {
            ParseAssemblyOrExpression();
            return;
        }

        var token = Current;
        throw new ParseException($"Expected {Be} but found {token.Kind}", token.Line, token.Column);
    }

    private void ParseIfStatement()
    {
        var conditions = 0;
        do
        {
            SkipNewLines();
            if (Is(LeftBrace) && conditions > 0) break;
            _tree.StartNode(GesSyntaxNodeKind.IfCondition);
            if (Match(Let))
            {
                ExpectIdentifier();
                Expect(Be);
            }

            ParseExpression();
            conditions++;
            _tree.FinishNode();
            SkipNewLines();
        }
        while (Match(Semicolon));

        ParseStatementBody();
        if (IsElseClauseStart())
        {
            SkipNewLines();
            _tree.StartNode(GesSyntaxNodeKind.ElseClause);
            Expect(Else);
            ParseStatementBody();
            _tree.FinishNode();
        }
    }

    private void ParseForStatement()
    {
        ParseBindingNames();
        SkipNewLines();
        var previousSourceDepth = _forSourceDepth;
        _forSourceDepth = _delimiterDepth;
        try
        {
            if (Match(In))
            {
                SkipNewLines();
                if (Current.Kind == Identifier && string.Equals(Current.Text, "from", StringComparison.Ordinal))
                {
                    throw new ParseException("Direct ranges are not allowed after 'in'; use 'for item from ... to ...' or iterate a range variable", Current.Line, Current.Column);
                }

                ParseExpression();
            }
            else if (IsWord("from"))
            {
                _tree.StartNode(GesSyntaxNodeKind.RangeExpression);
                Advance();
                ParseRangeExpressionCore();
                _tree.FinishNode();
            }
            else
            {
                var token = Current;
                throw new ParseException($"Expected {In} or 'from' but found {token.Text}", token.Line, token.Column);
            }
        }
        finally { _forSourceDepth = previousSourceDepth; }

        SkipNewLines();
        if (IsForClauseStart(requireSourceContext: false))
        {
            if (_statementNesting >= MaximumStatementDepth) ThrowNestingLimitAtCurrent();
            // `and item in ...` continues with a nested for statement.
            _tree.StartNode(GesSyntaxNodeKind.ForStatement);
            Advance();
            SkipNewLines();
            _statementNesting++;
            try { ParseForStatement(); }
            finally { _statementNesting--; }
            _tree.FinishNode();
        }
        else ParseStatementBody();
    }

    private bool IsForClauseStart(bool requireSourceContext = true)
    {
        if (requireSourceContext && _forSourceDepth != _delimiterDepth || !Is(OperatorAnd)) return false;
        var offset = 1;
        if (_reader.PeekSignificant(offset++).Kind != Identifier) return false;
        while (_reader.PeekSignificant(offset).Kind == Comma)
        {
            offset++;
            if (_reader.PeekSignificant(offset++).Kind != Identifier) return false;
        }
        var token = _reader.PeekSignificant(offset);
        return token.Kind == In || token.Kind == Identifier && token.Text == "from";
    }

    private void ParseSeededRandomStatement()
    {
        Expect(KeywordRandom);
        SkipNewLines();
        Expect(With);
        SkipNewLines();
        ParseExpression();
        ParseStatementBody();
    }

    private void ParseStatementBody()
    {
        if (_statementNesting >= MaximumStatementDepth) ThrowNestingLimitAtCurrent();
        _statementNesting++;
        try { ParseStatementBodyCore(); }
        finally { _statementNesting--; }
    }

    private void ParseStatementBodyCore()
    {
        SkipNewLines();
        if (Is(LeftBrace))
        {
            _tree.StartNode(GesSyntaxNodeKind.Block);
            Advance();
            ParseStatementsUntil(RightBrace);
            Expect(RightBrace);
            _tree.FinishNode();
            return;
        }

        ParseStatement();
    }

    // The type name without its colon, such as `Number` or `Quantity`.
    private string ParseTypeName()
    {
        ThrowIfIllegalToken();
        _tree.StartNode(GesSyntaxNodeKind.TypeReference);
        return FinishNode(ParseTypeNameCore());
    }

    private string ParseTypeNameCore()
    {
        if (!Is(TypeName))
        {
            var token = Current;
            throw new ParseException($"Expected type name but found {token.Kind}", token);
        }

        var typeToken = Advance();
        var typeName = typeToken.Text[1..];

        if (string.Equals(typeName, "Numeric", StringComparison.Ordinal)) throw new ParseException("The numeric type helper is a keyword; use numeric without ':'.", typeToken);

        if (string.Equals(typeName, "Quantity", StringComparison.Ordinal) &&
            Is(LeftParen) &&
            IsQuantityTypeSpecifierAhead())
        {
            SkipNewLines();
            Expect(LeftParen);
            SkipNewLines();
            while (!Is(RightParen))
            {
                Advance();
                SkipNewLines();
            }
            Expect(RightParen);
        }

        return typeName;
    }

    // A quantity unit is a name optionally followed by `/` and further unit parts, such as `m/s`, `m/s^2` or `°/s`.
    private bool IsQuantityTypeSpecifierAhead()
    {
        if (_reader.PeekSignificant(0).Kind != LeftParen) return false;

        // Uppercase unit symbols such as N or Hz read as message-name tokens.
        if (_reader.PeekSignificant(1).Kind is not (Identifier or Message or IntrinsicDeg)) return false;

        for (var offset = 2; offset < 8; offset++)
        {
            var kind = _reader.PeekSignificant(offset).Kind;
            if (kind == RightParen) return true;
            if (kind is not (Identifier or Message or OperatorDivide or OperatorPower or SuperscriptInteger or GesTokenKind.Float)) return false;
        }

        return false;
    }

    private GesToken Expect(GesTokenKind kind)
    {
        ThrowIfIllegalToken();

        if (Is(kind)) return Advance();

        var token = Current;
        throw new ParseException($"Expected {kind} but found {token.Kind}", token);
    }

    private string ExpectIdentifier()
    {
        ThrowIfIllegalToken();

        if (Is(Identifier)) return Advance().Text;

        var token = Current;
        throw new ParseException($"Expected Identifier but found {token.Kind}", token);
    }

    private bool Match(GesTokenKind kind)
    {
        if (!Is(kind)) return false;
        Advance();
        return true;
    }

    // Matches any of the given kinds; the span is stack-allocated, so matching allocates nothing.
    private bool Match(params ReadOnlySpan<GesTokenKind> kinds)
    {
        foreach (var kind in kinds)
        {
            if (!Is(kind)) continue;
            Advance();
            return true;
        }

        return false;
    }

    private bool MatchSelector(string selectorText)
    {
        if (Current.Kind == Selector && string.Equals(Current.Text, selectorText, StringComparison.Ordinal))
        {
            Advance();
            return true;
        }

        return false;
    }

    private bool Is(GesTokenKind kind) => Current.Kind == kind;

    private bool IsNextSignificantToken(GesTokenKind kind) => _reader.PeekSignificant(1).Kind == kind;

    private bool IsSeededRandomStatementStart() => Current.Kind == KeywordRandom && _reader.PeekSignificant(1).Kind == With;

    private bool IsElseClauseStart() => _reader.PeekSignificant(0).Kind == Else;

    private bool IsCallExpressionStart() => Current.Kind is Identifier or Message && _reader.PeekSignificant(1).Kind == LeftParen;

    private bool IsExtensionCallStart()
        => Current.Text.StartsWith(":", StringComparison.Ordinal) && Current.Kind != TypeName &&
           _reader.PeekSignificant(1).Kind == Dot && IsExtensionFunctionNameKind(_reader.PeekSignificant(2).Kind);

    private bool IsTypeConstructorStart() => Current.Kind == TypeName && _reader.PeekSignificant(1).Kind == LeftParen;

    // Returns the namespace token of the symbol, such as `:math`.
    private GesToken ParseExtensionSymbol()
    {
        var extensionToken = Advance();
        SkipNewLines();
        var dot = Expect(Dot);
        SkipNewLines();
        var functionToken = ExpectExtensionFunctionName();
        if (extensionToken.EndLine != dot.Line || extensionToken.EndColumn != dot.Column || dot.EndLine != functionToken.Line || dot.EndColumn != functionToken.Column)
            throw new ParseException("Extension names must be contiguous: :namespace.name.", extensionToken);
        return extensionToken;
    }

    private bool IsExtensionUnaryArgumentStart()
        => !IsPipelineSelectorStart() && Current.Kind is Identifier or Message or Tag or Selector or TypeName or ConstantReference or GesTokenKind.Float or Percentage or UnitNumber or Text or True or False or Nothing or
            MathConstantPi or MathConstantE or MathConstantTau or MathConstantInfinity or
            IntrinsicAbs or IntrinsicLn or IntrinsicExp or IntrinsicSqrt or IntrinsicCbrt or IntrinsicChance or
            IntrinsicFloor or IntrinsicCeil or IntrinsicTruncate or IntrinsicRad or IntrinsicDeg or IntrinsicWrap or IntrinsicRound or
            IntrinsicSin or IntrinsicCos or IntrinsicTan or IntrinsicAsin or IntrinsicAcos or IntrinsicAtan or IntrinsicAtan2 or
            IntrinsicHypot or IntrinsicDistance or IntrinsicSquared or IntrinsicLength or IntrinsicNormalize or IntrinsicDot or IntrinsicCross or IntrinsicAngle or
            KeywordParse or KeywordRandom or Series or Clamp or Min or Max or Roll or
            LeftBracket or LeftParen or OperatorMinus or Has or Empty or OperatorNot;

    private bool IsArgumentLabelStart() => IsArgumentLabelKind(Current.Kind) && _reader.PeekSignificant(1).Kind == Colon;

    private void ExpectArgumentLabel()
    {
        ThrowIfIllegalToken();

        if (IsArgumentLabelKind(Current.Kind))
        {
            Advance();
            return;
        }

        var token = Current;
        throw new ParseException($"Expected argument label but found {token.Kind}", token);
    }

    // The token kinds an argument label may have; the Compiler binder reads labels by the same rule.
    internal static bool IsArgumentLabelKind(GesTokenKind kind)
        => kind is Identifier or To or Nothing or MathConstantPi or MathConstantE or MathConstantTau or MathConstantInfinity or
            IntrinsicAbs or IntrinsicLn or IntrinsicExp or IntrinsicSqrt or IntrinsicCbrt or IntrinsicChance or
            IntrinsicFloor or IntrinsicCeil or IntrinsicTruncate or IntrinsicRad or IntrinsicDeg or IntrinsicWrap or IntrinsicRound or
            IntrinsicSin or IntrinsicCos or IntrinsicTan or IntrinsicAsin or IntrinsicAcos or IntrinsicAtan or IntrinsicAtan2 or
            IntrinsicHypot or IntrinsicDistance or IntrinsicSquared or IntrinsicLength or IntrinsicNormalize or IntrinsicDot or IntrinsicCross or IntrinsicAngle;

    private static bool IsExtensionFunctionNameKind(GesTokenKind kind)
        => kind is Identifier or
            IntrinsicAbs or IntrinsicLn or IntrinsicExp or IntrinsicSqrt or IntrinsicCbrt or IntrinsicChance or
            IntrinsicFloor or IntrinsicCeil or IntrinsicTruncate or IntrinsicRad or IntrinsicDeg or IntrinsicWrap or IntrinsicRound or
            IntrinsicSin or IntrinsicCos or IntrinsicTan or IntrinsicAsin or IntrinsicAcos or IntrinsicAtan or IntrinsicAtan2 or
            IntrinsicHypot or IntrinsicDistance or IntrinsicSquared or IntrinsicLength or IntrinsicNormalize or IntrinsicDot or IntrinsicCross or IntrinsicAngle;

    private GesToken ExpectExtensionFunctionName()
    {
        ThrowIfIllegalToken();

        if (IsExtensionFunctionNameKind(Current.Kind)) return Advance();

        var token = Current;
        throw new ParseException($"Expected extension function name but found {token.Kind}", token);
    }

    private void ExpectValueWord()
    {
        if (Current.Kind == Identifier && string.Equals(Current.Text, "value", StringComparison.Ordinal))
        {
            Advance();
            return;
        }

        throw new ParseException($"Expected value but found {Current.Text}", Current);
    }

    private bool MatchWord(string word)
    {
        if (!IsWord(word)) return false;
        Advance();
        return true;
    }

    private bool IsWord(string word) => Current.Kind == Identifier && string.Equals(Current.Text, word, StringComparison.Ordinal);

    private bool IsNextSignificantWord(string word)
    {
        var lookahead = _reader.PeekSignificant(1);
        return lookahead.Kind == Identifier && string.Equals(lookahead.Text, word, StringComparison.Ordinal);
    }

    private void ExpectWord(string word)
    {
        ThrowIfIllegalToken();

        if (MatchWord(word)) return;

        throw new ParseException($"Expected {word} but found {Current.Text}", Current);
    }

    private void ThrowIfIllegalToken()
    {
        if (Current.Kind == Illegal) throw new ParseException($"Illegal token '{Current.Text}'", Current);
    }

    private void SkipNewLines()
    {
        while (Is(NewLine)) Advance();
    }

    private void SkipStatementSeparators()
    {
        while (Match(Semicolon, NewLine))
        {
        }
    }

    private void RequireStatementSeparatorOrClosing(GesTokenKind closingKind)
    {
        if (Is(closingKind)) return;

        if (Match(Semicolon, NewLine))
        {
            SkipStatementSeparators();
            return;
        }

        var token = Current;
        throw new ParseException($"Expected statement separator or '{closingKind}' but found {token.Kind}", token);
    }

    private void RequireHandlerSeparatorOrEndOfFile()
    {
        if (Is(EndOfFile)) return;

        if (Match(Semicolon, NewLine))
        {
            SkipStatementSeparators();
            return;
        }

        var token = Current;
        throw new ParseException($"Expected event handler separator but found {token.Kind}", token);
    }

    private GesToken Advance()
    {
        var token = _reader.Advance();
        // EndOfFile is never consumed; the tree receives it once, when it is completed.
        if (token.Kind != EndOfFile) _tree.Token(token);
        if (token.Kind is LeftParen or LeftBracket or LeftBrace) _delimiterDepth++;
        else if (token.Kind is RightParen or RightBracket or RightBrace) _delimiterDepth--;
        return token;
    }

    private GesToken Current => _reader.Current;

    private GesToken Previous => _reader.Previous;

    private void AddParseError(ParseException exception)
        => _errors.Add(new GesSyntaxDiagnostic(GesSyntaxDiagnostic.Syntax, exception.Message, exception.Line, exception.Column, exception.EndLine, exception.EndColumn, _moduleName));

    private void SynchronizeTopLevel()
    {
        while (!Is(EndOfFile))
        {
            if (Is(Module) || Is(Constant) || Is(Record) || Is(Predicate) || Is(Function) || Is(On)) return;

            Advance();
        }
    }

    private bool SynchronizeStatement(GesTokenKind closingKind)
    {
        while (!Is(EndOfFile))
        {
            if (Is(closingKind)) return false;

            if (Match(Semicolon, NewLine))
            {
                SkipStatementSeparators();
                return true;
            }

            Advance();
        }

        return false;
    }
}
