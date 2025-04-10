using System.Collections.Generic;
using NUnit.Framework;
using StepH.Utilities.EventScript;

namespace Utilities.Tests
{
    [TestFixture]
    public class ParserTests
    {
        private static List<Token> Lex(string source)
        {
            var lexer = new Lexer(source);
            return lexer.Tokenize();
        }

        [Test]
        public void ParsesSimpleStartEvent()
        {
            string code = @"
                on Start() {
                    emit Begin();
                }
            ";

            var tokens = Lex(code);
            var parser = new StreamParser(tokens.GetEnumerator());
            ProgramNode program = parser.ParseProgram();

            Assert.That(program.Handlers, Has.Count.EqualTo(1));
            var handler = program.Handlers[0];
            Assert.That(handler.Name, Is.EqualTo("Start"));
            Assert.That(handler.Parameters, Is.Empty);
            Assert.That(handler.Body, Has.Count.EqualTo(1));
            Assert.That(handler.Body[0], Is.TypeOf<EmitStatement>());

            var emit = (EmitStatement)handler.Body[0];
            Assert.That(emit.EventName, Is.EqualTo("Begin"));
            Assert.That(emit.Arguments, Is.Empty);
        }

        [Test]
        public void ParsesLetAndExpression()
        {
            string code = @"
                on Init() {
                    let score = 42;
                    emit Show(score);
                }
            ";

            var tokens = Lex(code);
            var parser = new StreamParser(tokens.GetEnumerator());
            var program = parser.ParseProgram();

            var body = program.Handlers[0].Body;
            Assert.That(body[0], Is.TypeOf<LetStatement>());
            var let = (LetStatement)body[0];
            Assert.That(let.Identifier, Is.EqualTo("score"));
            Assert.That(let.Value, Is.TypeOf<LiteralExpression>());

            var literal = (LiteralExpression)let.Value;
            Assert.That(literal.Value, Is.EqualTo(42));

            var emit = (EmitStatement)body[1];
            Assert.That(emit.Arguments[0], Is.TypeOf<IdentifierExpression>());
        }

        [Test]
        public void ParsesIfWithElse()
        {
            string code = @"
                on Check(val) {
                    if val {
                        emit Pass();
                    } else {
                        emit Fail();
                    }
                }
            ";

            var tokens = Lex(code);
            var parser = new StreamParser(tokens.GetEnumerator());
            var program = parser.ParseProgram();

            var handler = program.Handlers[0];
            Assert.That(handler.Parameters, Contains.Item("val"));
            Assert.That(handler.Body[0], Is.TypeOf<IfStatement>());
            var ifStmt = (IfStatement)handler.Body[0];

            Assert.That(ifStmt.ThenBlock[0], Is.TypeOf<EmitStatement>());
            Assert.That(ifStmt.ElseBlock, Is.Not.Null);
            Assert.That(ifStmt.ElseBlock![0], Is.TypeOf<EmitStatement>());
        }
    }
}