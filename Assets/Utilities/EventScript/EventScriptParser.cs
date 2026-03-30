using System;
using System.Collections.Generic;

namespace StepH.Utilities.EventScript
{
    public class StreamParser
    {
        private readonly IEnumerator<Token> tokens;
        private Token current;

        public StreamParser(IEnumerator<Token> tokenStream)
        {
            tokens = tokenStream;
            Advance();
        }
        
        public ProgramNode ParseProgram()
        {
            var program = new ProgramNode();
            while (current.Type != TokenType.Eof)
            {
                program.Handlers.Add(ParseEventHandler());
            }

            return program;
        }
        
        private void Advance()
        {
            current = tokens.MoveNext() ? tokens.Current : new Token(TokenType.Eof, "");
        }

        private bool Match(TokenType type)
        {
            if (current.Type == type)
            {
                Advance();
                return true;
            }

            return false;
        }

        private Token Expect(TokenType type, string message)
        {
            if (current.Type == type)
            {
                var token = current;
                Advance();
                return token;
            }

            throw new Exception($"{message} Got '{current.Lexeme}' instead.");
        }

        private EventHandlerNode ParseEventHandler()
        {
            Expect(TokenType.On, "Expected 'on' keyword.");
            var name = Expect(TokenType.Identifier, "Expected event name.").Lexeme;
            Expect(TokenType.LeftParen, "Expected '('.");

            var parameters = new List<string>();
            if (current.Type != TokenType.RightParen)
            {
                do
                {
                    parameters.Add(Expect(TokenType.Identifier, "Expected parameter name.").Lexeme);
                } while (Match(TokenType.Comma));
            }

            Expect(TokenType.RightParen, "Expected ')'.");
            Expect(TokenType.LeftBrace, "Expected '{'.");

            var body = new List<StatementNode>();
            while (current.Type != TokenType.RightBrace && current.Type != TokenType.Eof)
            {
                body.Add(ParseStatement());
            }

            Expect(TokenType.RightBrace, "Expected '}' at end of event handler.");

            return new EventHandlerNode
            {
                Name = name,
                Parameters = parameters,
                Body = body
            };
        }

        private StatementNode ParseStatement()
        {
            if (Match(TokenType.Emit)) return ParseEmit();
            if (Match(TokenType.Let)) return ParseLet();
            if (Match(TokenType.If)) return ParseIf();
            if (Match(TokenType.For)) return ParseFor();
            return ParseExpressionStatement();
        }

        private EmitStatement ParseEmit()
        {
            var name = Expect(TokenType.Identifier, "Expected event name after 'emit'.").Lexeme;
            Expect(TokenType.LeftParen, "Expected '(' after event name.");
            var args = new List<ExpressionNode>();
            if (current.Type != TokenType.RightParen)
            {
                do
                {
                    args.Add(ParseExpression());
                } while (Match(TokenType.Comma));
            }

            Expect(TokenType.RightParen, "Expected ')' after arguments.");
            Expect(TokenType.Semicolon, "Expected ';' after emit statement.");

            return new EmitStatement { EventName = name, Arguments = args };
        }

        private LetStatement ParseLet()
        {
            var id = Expect(TokenType.Identifier, "Expected variable name.").Lexeme;
            Expect(TokenType.Equal, "Expected '=' after variable name.");
            var value = ParseExpression();
            Expect(TokenType.Semicolon, "Expected ';' after let statement.");
            return new LetStatement { Identifier = id, Value = value };
        }

        private ExpressionStatement ParseExpressionStatement()
        {
            var expr = ParseExpression();
            Expect(TokenType.Semicolon, "Expected ';' after expression.");
            return new ExpressionStatement { Expression = expr };
        }

        private ExpressionNode ParseExpression()
        {
            // Platzhalter für einfache Implementierung, kann später erweitert werden
            if (current.Type == TokenType.String)
            {
                var val = current.Lexeme;
                Advance();
                return new LiteralExpression { Value = val };
            }

            if (current.Type == TokenType.Number)
            {
                var val = double.Parse(current.Lexeme);
                Advance();
                return new LiteralExpression { Value = val };
            }

            if (current.Type == TokenType.True || current.Type == TokenType.False)
            {
                var val = current.Type == TokenType.True;
                Advance();
                return new LiteralExpression { Value = val };
            }

            if (current.Type == TokenType.Identifier)
            {
                var name = current.Lexeme;
                Advance();
                return new IdentifierExpression { Name = name };
            }

            throw new Exception("Expected expression.");
        }

        private IfStatement ParseIf()
        {
            var condition = ParseExpression();
            Expect(TokenType.LeftBrace, "Expected '{' after condition.");
            var thenBlock = new List<StatementNode>();
            while (current.Type != TokenType.RightBrace && current.Type != TokenType.Eof)
                thenBlock.Add(ParseStatement());
            Expect(TokenType.RightBrace, "Expected '}' after if block.");

            List<StatementNode>? elseBlock = null;
            if (Match(TokenType.Else))
            {
                Expect(TokenType.LeftBrace, "Expected '{' after 'else'.");
                elseBlock = new List<StatementNode>();
                while (current.Type != TokenType.RightBrace && current.Type != TokenType.Eof)
                    elseBlock.Add(ParseStatement());
                Expect(TokenType.RightBrace, "Expected '}' after else block.");
            }

            return new IfStatement
            {
                Condition = condition,
                ThenBlock = thenBlock,
                ElseBlock = elseBlock
            };
        }

        private ForStatement ParseFor()
        {
            var variable = Expect(TokenType.Identifier, "Expected loop variable name.").Lexeme;
            Expect(TokenType.In, "Expected 'in' keyword.");
            var collection = ParseExpression();
            Expect(TokenType.LeftBrace, "Expected '{' after for-in statement.");
            var body = new List<StatementNode>();
            while (current.Type != TokenType.RightBrace && current.Type != TokenType.Eof)
                body.Add(ParseStatement());
            Expect(TokenType.RightBrace, "Expected '}' after for block.");

            return new ForStatement
            {
                Variable = variable,
                Collection = collection,
                Body = body
            };
        }
    }
}