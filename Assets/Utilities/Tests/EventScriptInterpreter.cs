using System;
using NUnit.Framework;
using StepH.Utilities.EventScript;

namespace Utilities.Tests
{
    [TestFixture]
    public class InterpreterTests
    {
        private ProgramNode Compile(string source)
        {
            var tokens = new Lexer(source).Tokenize();
            tokens.ForEach(it => Console.WriteLine($"[Trace] {it}"));
            var parser = new StreamParser(tokens.GetEnumerator());
            return parser.ParseProgram();
        }

        [Test]
        public void InterpretsSimpleEmit()
        {
            string script = @"
                on Start() {
                    emit Hello();
                }

                on Hello() {
                    let message = 'Hello, World!';
                }
            ";

            var interpreter = new Interpreter();
            interpreter.LoadProgram(Compile(script));

            Assert.DoesNotThrow(() => interpreter.Emit("Start"));
        }

        [Test]
        public void HandlesLetAndBinaryExpression()
        {
            string script = @"
                on Start() {
                    let x = 5 + 3;
                    emit Result(x);
                }

                on Result(val) {
                    // This would normally output or store result
                    let debug = val;
                }
            ";

            var interpreter = new Interpreter();
            interpreter.LoadProgram(Compile(script));

            Assert.DoesNotThrow(() => interpreter.Emit("Start"));
        }

        [Test]
        public void RunsIfElseCorrectly()
        {
            string script = @"
                on Check(value) {
                    if value > 10 {
                        emit Large();
                    } else {
                        emit Small();
                    }
                }

                on Large() {
                    let result = 'big';
                    emit Result(result);
                }

                on Small() {
                    let result = 'small';
                }
            ";

            var interpreter = new Interpreter();
            interpreter.LoadProgram(Compile(script));

            Assert.DoesNotThrow(() => interpreter.Emit("Check", 20));
            Assert.DoesNotThrow(() => interpreter.Emit("Check", 5));
        }
    }
}
