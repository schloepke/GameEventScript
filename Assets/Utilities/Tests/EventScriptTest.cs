using System;
using System.Collections.Generic;
using NUnit.Framework;
using StepH.Utilities;
using StepH.Utilities.EventScript;

namespace Utilities.Tests
{
    [TestFixture]
    public class LexerTests
    {
        [Test]
        public void TokenizesSimpleScriptCorrectly()
        {
            string input = @"
                on Start() {
                    emit SpawnCoins(range(0, 5));
                    let name = 'Player';
                }
            ";

            var lexer = new Lexer(input);
            var tokens = lexer.Tokenize();
            
            Assert.IsNotEmpty(tokens);
            Assert.AreEqual(TokenType.On, tokens[0].Type);
            Assert.AreEqual("on", tokens[0].Lexeme);
            Assert.AreEqual(TokenType.Identifier, tokens[1].Type);
            Assert.AreEqual("Start", tokens[1].Lexeme);
            
            tokens.ForEach(it => ILoggingFacade.Default.Trace(it.ToString()));
            
        }

        [Test]
        public void TokenizesEmitWithNestedCall()
        {
            string input = "emit DoSomething(complexCall(1, 2));";

            var lexer = new Lexer(input);
            var tokens = lexer.Tokenize();

            var expected = new[]
            {
                TokenType.Emit,
                TokenType.Identifier,
                TokenType.LeftParen,
                TokenType.Identifier,
                TokenType.LeftParen,
                TokenType.Number,
                TokenType.Comma,
                TokenType.Number,
                TokenType.RightParen,
                TokenType.RightParen,
                TokenType.Semicolon,
                TokenType.EOF
            };

            Assert.AreEqual(expected.Length, tokens.Count);
            for (int i = 0; i < expected.Length; i++)
            {
                Assert.AreEqual(expected[i], tokens[i].Type, $"Token {i} mismatch: expected {expected[i]} but got {tokens[i].Type}");
            }
        }

        [Test]
        public void TokenizesStringLiteral()
        {
            string input = "let msg = 'Hello World';";
            var lexer = new Lexer(input);
            var tokens = lexer.Tokenize();

            Assert.That(tokens.Exists(t => t.Type == TokenType.String && t.Lexeme == "Hello World"));
        }

        [Test]
        public void TokenizesBooleanLiterals()
        {
            string input = "true false";
            var lexer = new Lexer(input);
            var tokens = lexer.Tokenize();

            Assert.AreEqual(TokenType.True, tokens[0].Type);
            Assert.AreEqual(TokenType.False, tokens[1].Type);
        }

        [Test]
        public void ThrowsOnUnknownCharacter()
        {
            string input = "let x = @";
            var lexer = new Lexer(input);

            Assert.Throws<Exception>(() => lexer.Tokenize());
        }
    }
}
