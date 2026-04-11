using StepH.Flow.EventScript;

namespace StepH_Flow_Tests.EventScript;

[TestClass]
public class InterpreterTests
{
    private static void DoesNotThrow(Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            Assert.Fail($"Expected no exception, but got: {ex}");
        }
    }

    private ProgramNode Compile(string source)
    {
        var tokens = new Lexer(source).Tokenize();
        tokens.ForEach(it => Console.WriteLine($"[Trace] {it}"));
        var parser = new StreamParser(tokens.GetEnumerator());
        return parser.ParseProgram();
    }

    [TestMethod]
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

        DoesNotThrow(() => interpreter.Emit("Start"));
    }

    [TestMethod]
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

        DoesNotThrow(() => interpreter.Emit("Start"));
    }

    [TestMethod]
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

        DoesNotThrow(() => interpreter.Emit("Check", 20));
        DoesNotThrow(() => interpreter.Emit("Check", 5));
    }
}
