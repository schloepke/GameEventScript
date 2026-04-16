#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace StepH.Flow.EventScript;

public sealed class EventScriptParser
{
    private readonly IReadOnlyList<EventScriptToken> _tokens;
    private int _index;

    private EventScriptParser(IReadOnlyList<EventScriptToken> tokens)
    {
        _tokens = tokens;
    }

    public static EventScriptProgram Parse(string script)
    {
        var lexer = new EventScriptLexer(script);
        var tokens = lexer.Tokenize();
        return new EventScriptParser(tokens).ParseProgram();
    }

    private EventScriptProgram ParseProgram()
    {
        var handlers = new List<EventHandlerNode>();
        while (!Is(EventScriptTokenKind.EndOfFile))
        {
            handlers.Add(ParseEventHandler());
        }

        return new EventScriptProgram(handlers);
    }

    private EventHandlerNode ParseEventHandler()
    {
        var isExternal = Match(EventScriptTokenKind.External);
        Expect(EventScriptTokenKind.On);
        var message = Expect(EventScriptTokenKind.Message).Text;

        var parameters = new List<string>();
        if (Match(EventScriptTokenKind.LeftParen))
        {
            if (!Is(EventScriptTokenKind.RightParen))
            {
                parameters.Add(ExpectIdentifierLike());
                while (Match(EventScriptTokenKind.Comma))
                {
                    parameters.Add(ExpectIdentifierLike());
                }
            }

            Expect(EventScriptTokenKind.RightParen);
        }

        if (isExternal)
        {
            Expect(EventScriptTokenKind.Semicolon);
            return new EventHandlerNode(message, parameters, Array.Empty<StatementNode>(), true);
        }

        Expect(EventScriptTokenKind.LeftBrace);
        var statements = ParseStatementsUntil(EventScriptTokenKind.RightBrace);
        Expect(EventScriptTokenKind.RightBrace);

        return new EventHandlerNode(message, parameters, statements);
    }

    private IReadOnlyList<StatementNode> ParseStatementsUntil(EventScriptTokenKind closingKind)
    {
        var statements = new List<StatementNode>();

        while (!Is(closingKind))
        {
            if (Is(EventScriptTokenKind.EndOfFile))
            {
                var token = Current;
                throw new EventScriptParseException($"Expected '{closingKind}' before end of input", token.Line, token.Column);
            }

            statements.Add(ParseStatement());
        }

        return statements;
    }

    private StatementNode ParseStatement()
    {
        if (Match(EventScriptTokenKind.Emit))
        {
            return ParseEmitStatement();
        }

        if (Match(EventScriptTokenKind.Let))
        {
            return ParseLetStatement();
        }

        if (Match(EventScriptTokenKind.If))
        {
            return ParseIfStatement();
        }

        if (Match(EventScriptTokenKind.For))
        {
            return ParseForStatement();
        }

        var expression = ParseExpression();
        Expect(EventScriptTokenKind.Semicolon);
        return new ExpressionStatementNode(expression);
    }

    private EmitStatementNode ParseEmitStatement()
    {
        var message = Expect(EventScriptTokenKind.Message).Text;
        var arguments = new List<ExpressionNode>();

        if (Match(EventScriptTokenKind.LeftParen))
        {
            if (!Is(EventScriptTokenKind.RightParen))
            {
                arguments.Add(ParseExpression());
                while (Match(EventScriptTokenKind.Comma))
                {
                    arguments.Add(ParseExpression());
                }
            }

            Expect(EventScriptTokenKind.RightParen);
        }

        Expect(EventScriptTokenKind.Semicolon);
        return new EmitStatementNode(message, arguments);
    }

    private LetStatementNode ParseLetStatement()
    {
        var identifier = ExpectIdentifierLike();
        Expect(EventScriptTokenKind.Assign);
        var expression = ParseExpression();
        Expect(EventScriptTokenKind.Semicolon);
        return new LetStatementNode(identifier, expression);
    }

    private IfStatementNode ParseIfStatement()
    {
        var condition = ParseExpression();
        Expect(EventScriptTokenKind.LeftBrace);
        var thenStatements = ParseStatementsUntil(EventScriptTokenKind.RightBrace);
        Expect(EventScriptTokenKind.RightBrace);

        var elseStatements = new List<StatementNode>();
        if (Match(EventScriptTokenKind.Else))
        {
            Expect(EventScriptTokenKind.LeftBrace);
            elseStatements.AddRange(ParseStatementsUntil(EventScriptTokenKind.RightBrace));
            Expect(EventScriptTokenKind.RightBrace);
        }

        return new IfStatementNode(condition, thenStatements, elseStatements);
    }

    private ForStatementNode ParseForStatement()
    {
        var identifier = ExpectIdentifierLike();
        Expect(EventScriptTokenKind.In);
        var source = ParseExpression();
        Expect(EventScriptTokenKind.LeftBrace);
        var statements = ParseStatementsUntil(EventScriptTokenKind.RightBrace);
        Expect(EventScriptTokenKind.RightBrace);
        return new ForStatementNode(identifier, source, statements);
    }

    private ExpressionNode ParseExpression() => ParseOrExpression();

    private ExpressionNode ParseOrExpression()
    {
        var expression = ParseAndExpression();

        while (Match(EventScriptTokenKind.Or))
        {
            var op = Previous.Text;
            var right = ParseAndExpression();
            expression = new BinaryExpressionNode(expression, op, right);
        }

        return expression;
    }

    private ExpressionNode ParseAndExpression()
    {
        var expression = ParseEqualityExpression();

        while (Match(EventScriptTokenKind.And))
        {
            var op = Previous.Text;
            var right = ParseEqualityExpression();
            expression = new BinaryExpressionNode(expression, op, right);
        }

        return expression;
    }

    private ExpressionNode ParseEqualityExpression()
    {
        var expression = ParseRelationalExpression();

        while (Match(EventScriptTokenKind.Equal, EventScriptTokenKind.NotEqual))
        {
            var op = Previous.Text;
            var right = ParseRelationalExpression();
            expression = new BinaryExpressionNode(expression, op, right);
        }

        return expression;
    }

    private ExpressionNode ParseRelationalExpression()
    {
        var expression = ParseAdditiveExpression();

        while (Match(
                   EventScriptTokenKind.Less,
                   EventScriptTokenKind.Greater,
                   EventScriptTokenKind.LessOrEqual,
                   EventScriptTokenKind.GreaterOrEqual))
        {
            var op = Previous.Text;
            var right = ParseAdditiveExpression();
            expression = new BinaryExpressionNode(expression, op, right);
        }

        return expression;
    }

    private ExpressionNode ParseAdditiveExpression()
    {
        var expression = ParseMultiplicativeExpression();

        while (Match(EventScriptTokenKind.Plus, EventScriptTokenKind.Minus))
        {
            var op = Previous.Text;
            var right = ParseMultiplicativeExpression();
            expression = new BinaryExpressionNode(expression, op, right);
        }

        return expression;
    }

    private ExpressionNode ParseMultiplicativeExpression()
    {
        var expression = ParseUnaryExpression();

        while (Match(EventScriptTokenKind.Multiply, EventScriptTokenKind.Divide, EventScriptTokenKind.Modulo))
        {
            var op = Previous.Text;
            var right = ParseUnaryExpression();
            expression = new BinaryExpressionNode(expression, op, right);
        }

        return expression;
    }

    private ExpressionNode ParseUnaryExpression()
    {
        if (Match(EventScriptTokenKind.Not))
        {
            var op = Previous.Text;
            var operand = ParseUnaryExpression();
            return new UnaryExpressionNode(op, operand);
        }

        return ParsePostfixExpression();
    }

    private ExpressionNode ParsePostfixExpression()
    {
        var expression = ParsePrimaryExpression();

        while (true)
        {
            if (Match(EventScriptTokenKind.Dot))
            {
                var member = ExpectIdentifierLike();
                expression = new MemberAccessExpressionNode(expression, member);
                continue;
            }

            if (Match(EventScriptTokenKind.LeftBracket))
            {
                var selector = ParseCollectionSelector();
                Expect(EventScriptTokenKind.RightBracket);
                expression = new CollectionAccessExpressionNode(expression, selector);
                continue;
            }

            return expression;
        }
    }

    private CollectionSelectorNode ParseCollectionSelector()
    {
        if (Match(EventScriptTokenKind.Count))
        {
            return new CountSelectorNode();
        }

        if (Match(EventScriptTokenKind.Any, EventScriptTokenKind.All))
        {
            var op = Previous.Text;
            var identifier = ExpectIdentifierLike();
            Expect(EventScriptTokenKind.Where);
            var predicate = ParseExpression();
            return new PredicateSelectorNode(op, identifier, predicate);
        }

        if (Match(EventScriptTokenKind.Filter))
        {
            var identifier = ExpectIdentifierLike();
            Expect(EventScriptTokenKind.Where);
            var predicate = ParseExpression();
            return new FilterSelectorNode(identifier, predicate);
        }

        if (Match(EventScriptTokenKind.Sum))
        {
            var identifier = ExpectIdentifierLike();
            Expect(EventScriptTokenKind.Arrow);
            var projection = ParseExpression();
            return new SumSelectorNode(identifier, projection);
        }

        if (Match(EventScriptTokenKind.Select))
        {
            var identifier = ExpectIdentifierLike();
            Expect(EventScriptTokenKind.Arrow);
            var projection = ParseExpression();
            return new SelectSelectorNode(identifier, projection);
        }

        return new ExpressionSelectorNode(ParseExpression());
    }

    private ExpressionNode ParsePrimaryExpression()
    {
        if (Match(EventScriptTokenKind.Random))
        {
            return ParseRandomExpression();
        }

        if (Match(EventScriptTokenKind.Dice))
        {
            return ParseDiceExpression();
        }

        if (Match(EventScriptTokenKind.Number))
        {
            return new NumberLiteralExpressionNode(Previous.NumberValue, Previous.Text);
        }

        if (Match(EventScriptTokenKind.String))
        {
            return new StringLiteralExpressionNode(Previous.Text);
        }

        if (Match(EventScriptTokenKind.True))
        {
            return new BooleanLiteralExpressionNode(true);
        }

        if (Match(EventScriptTokenKind.False))
        {
            return new BooleanLiteralExpressionNode(false);
        }

        if (IsIdentifierLike(Current.Kind))
        {
            var identifierToken = Advance();
            return new IdentifierExpressionNode(identifierToken.Text);
        }

        if (Match(EventScriptTokenKind.LeftParen))
        {
            var expression = ParseExpression();
            Expect(EventScriptTokenKind.RightParen);
            return expression;
        }

        var token = Current;
        throw new EventScriptParseException($"Unexpected token '{token.Text}'", token.Line, token.Column);
    }

    private RandomExpressionNode ParseRandomExpression()
    {
        var fromExpression = ParseRandomBoundExpression();
        Expect(EventScriptTokenKind.To);
        var toExpression = ParseRandomBoundExpression();

        if (fromExpression is NumberLiteralExpressionNode fromNumber &&
            toExpression is NumberLiteralExpressionNode toNumber &&
            fromNumber.Value > toNumber.Value)
        {
            var token = Previous;
            throw new EventScriptParseException("Random expression has invalid range: start must be <= end", token.Line, token.Column);
        }

        return new RandomExpressionNode(fromExpression, toExpression);
    }

    private ExpressionNode ParseRandomBoundExpression() => ParseAdditiveExpression();

    private DiceExpressionNode ParseDiceExpression()
    {
        var diceCountToken = Expect(EventScriptTokenKind.Number);
        var sideCountToken = ParseDiceSideCountToken();

        var diceCount = ParsePositiveInteger(diceCountToken, "dice count");
        var sideCount = ParsePositiveInteger(sideCountToken, "side count");

        DiceModifierNode? modifier = null;
        if (Match(EventScriptTokenKind.Keep))
        {
            Expect(EventScriptTokenKind.Highest);
            var count = Match(EventScriptTokenKind.Number)
                ? ParsePositiveInteger(Previous, "keep count")
                : 1;
            if (count > diceCount)
            {
                throw new EventScriptParseException("Cannot keep more dice than are rolled", Previous.Line, Previous.Column);
            }

            modifier = new KeepHighestModifierNode(count);
        }
        else if (Match(EventScriptTokenKind.Drop))
        {
            Expect(EventScriptTokenKind.Lowest);
            var count = Match(EventScriptTokenKind.Number)
                ? ParsePositiveInteger(Previous, "drop count")
                : 1;
            if (count > diceCount)
            {
                throw new EventScriptParseException("Cannot drop more dice than are rolled", Previous.Line, Previous.Column);
            }

            modifier = new DropLowestModifierNode(count);
        }

        return new DiceExpressionNode(diceCount, sideCount, modifier);
    }

    private EventScriptToken ParseDiceSideCountToken()
    {
        if (Match(EventScriptTokenKind.DiceSeparator))
        {
            return Expect(EventScriptTokenKind.Number);
        }

        if (Is(EventScriptTokenKind.Identifier))
        {
            var token = Current;
            if (string.Equals(token.Text, "d", StringComparison.Ordinal))
            {
                Advance();
                return Expect(EventScriptTokenKind.Number);
            }

            if (IsCompactDiceToken(token.Text))
            {
                Advance();
                return new EventScriptToken(
                    EventScriptTokenKind.Number,
                    token.Text[1..],
                    token.Line,
                    token.Column + 1);
            }
        }

        var current = Current;
        throw new EventScriptParseException($"Expected dice separator but found {current.Kind}", current.Line, current.Column);
    }

    private static bool IsCompactDiceToken(string text)
    {
        if (text.Length <= 1 || text[0] != 'd')
        {
            return false;
        }

        for (var i = 1; i < text.Length; i++)
        {
            if (!char.IsDigit(text[i]))
            {
                return false;
            }
        }

        return true;
    }

    private int ParsePositiveInteger(EventScriptToken numberToken, string name)
    {
        if (!decimal.TryParse(numberToken.Text, NumberStyles.Number, CultureInfo.InvariantCulture, out var value) ||
            value <= 0 ||
            value != decimal.Truncate(value))
        {
            throw new EventScriptParseException($"Expected positive integer for {name}", numberToken.Line, numberToken.Column);
        }

        if (value > int.MaxValue)
        {
            throw new EventScriptParseException($"{name} is too large", numberToken.Line, numberToken.Column);
        }

        return (int)value;
    }

    private EventScriptToken Expect(EventScriptTokenKind kind)
    {
        if (Is(kind))
        {
            return Advance();
        }

        var token = Current;
        throw new EventScriptParseException($"Expected {kind} but found {token.Kind}", token.Line, token.Column);
    }

    private string ExpectIdentifierLike()
    {
        if (IsIdentifierLike(Current.Kind))
        {
            return Advance().Text;
        }

        var token = Current;
        throw new EventScriptParseException($"Expected Identifier but found {token.Kind}", token.Line, token.Column);
    }

    private bool Match(params EventScriptTokenKind[] kinds)
    {
        if (!kinds.Any(Is)) return false;
        Advance();
        return true;
    }

    private bool Is(EventScriptTokenKind kind) => Current.Kind == kind;

    private static bool IsIdentifierLike(EventScriptTokenKind kind)
    {
        return kind is
            EventScriptTokenKind.Identifier or
            EventScriptTokenKind.On or
            EventScriptTokenKind.Emit or
            EventScriptTokenKind.Let or
            EventScriptTokenKind.If or
            EventScriptTokenKind.Else or
            EventScriptTokenKind.For or
            EventScriptTokenKind.In or
            EventScriptTokenKind.Count or
            EventScriptTokenKind.Any or
            EventScriptTokenKind.All or
            EventScriptTokenKind.Filter or
            EventScriptTokenKind.Where or
            EventScriptTokenKind.Sum or
            EventScriptTokenKind.Select;
    }

    private EventScriptToken Advance()
    {
        if (!Is(EventScriptTokenKind.EndOfFile))
        {
            _index++;
        }

        return Previous;
    }

    private EventScriptToken Current => _tokens[_index];

    private EventScriptToken Previous => _tokens[_index - 1];
}
