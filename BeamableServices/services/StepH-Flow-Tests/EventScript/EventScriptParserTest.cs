using StepH.Flow.EventScript;

namespace StepH_Flow_Tests.EventScript;

[TestClass]
public class ParserTests
{
    private static List<Token> Lex(string source)
    {
        var lexer = new Lexer(source);
        return lexer.Tokenize();
    }

    [TestMethod]
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

        Assert.AreEqual(1, program.Handlers.Count);
        var handler = program.Handlers[0];
        Assert.AreEqual("Start", handler.Name);
        Assert.AreEqual(0, handler.Parameters.Count);
        Assert.AreEqual(1, handler.Body.Count);
        Assert.IsInstanceOfType<EmitStatement>(handler.Body[0]);

        var emit = (EmitStatement)handler.Body[0];
        Assert.AreEqual("Begin", emit.EventName);
        Assert.AreEqual(0, emit.Arguments.Count);
    }

    [TestMethod]
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
        Assert.IsInstanceOfType<LetStatement>(body[0]);
        var let = (LetStatement)body[0];
        Assert.AreEqual("score", let.Identifier);
        Assert.IsInstanceOfType<LiteralExpression>(let.Value);

        var literal = (LiteralExpression)let.Value;
        Assert.AreEqual(42.0, literal.Value);

        var emit = (EmitStatement)body[1];
        Assert.IsInstanceOfType<IdentifierExpression>(emit.Arguments[0]);
    }

    [TestMethod]
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
        CollectionAssert.Contains(handler.Parameters, "val");
        Assert.IsInstanceOfType<IfStatement>(handler.Body[0]);
        var ifStmt = (IfStatement)handler.Body[0];

        Assert.IsInstanceOfType<EmitStatement>(ifStmt.ThenBlock[0]);
        Assert.IsNotNull(ifStmt.ElseBlock);
        Assert.IsInstanceOfType<EmitStatement>(ifStmt.ElseBlock![0]);
    }
}
