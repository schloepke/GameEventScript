using StepH.Flow.EventScript;

namespace StepH_Flow_Tests.EventScript;

[TestClass]
public class OhEventScriptParserTests
{
    [TestMethod]
    public void Parse_BuildsHandlersAndStatements()
    {
        const string script = """
            on PlayerJoined(playerId) {
                let isActive = true;
                if isActive {
                    emit PlayerReady(playerId);
                } else {
                    emit PlayerIdle;
                }
            }
            """;

        var program = EventScriptParser.Parse(script);

        Assert.AreEqual(1, program.Handlers.Count);

        var handler = program.Handlers[0];
        Assert.AreEqual("PlayerJoined", handler.Message);
        CollectionAssert.AreEqual(new[] { "playerId" }, handler.Parameters.ToArray());
        Assert.AreEqual(2, handler.Statements.Count);

        Assert.IsInstanceOfType<LetStatementNode>(handler.Statements[0]);
        Assert.IsInstanceOfType<IfStatementNode>(handler.Statements[1]);
    }

    [TestMethod]
    public void Parse_BuildsExternalHandlers()
    {
        const string script = """
            external on Notify(playerId, points);

            on Start {
                emit Notify('p1', 3);
            }
            """;

        var program = EventScriptParser.Parse(script);

        Assert.AreEqual(2, program.Handlers.Count);
        Assert.IsTrue(program.Handlers[0].IsExternal);
        Assert.AreEqual("Notify", program.Handlers[0].Message);
        CollectionAssert.AreEqual(new[] { "playerId", "points" }, program.Handlers[0].Parameters.ToArray());
        Assert.AreEqual(0, program.Handlers[0].Statements.Count);
        Assert.IsFalse(program.Handlers[1].IsExternal);
    }

    [TestMethod]
    public void Parse_AllowsIdentifiersThatLookLikeCompactDiceTokens()
    {
        const string script = """
            on Start(d6) {
                let d6 = 1;
                emit Done(d6);
            }
            """;

        var program = EventScriptParser.Parse(script);
        var handler = program.Handlers[0];
        var letStatement = (LetStatementNode)handler.Statements[0];

        CollectionAssert.AreEqual(new[] { "d6" }, handler.Parameters.ToArray());
        Assert.AreEqual("d6", letStatement.Identifier);
    }

    [TestMethod]
    public void Parse_BuildsCollectionSelectors()
    {
        const string script = """
            on ScoreUpdated(items) {
                let hasAny = items[any item where item.points > 10];
                let allValid = items[all item where item.points >= 0];
                let filtered = items[filter item where item.points > 0];
                let total = items[sum item -> item.points];
                let count = items[count];
                let names = items[select item -> item.name];
            }
            """;

        var program = EventScriptParser.Parse(script);
        var statements = program.Handlers[0].Statements.Cast<LetStatementNode>().ToArray();

        Assert.IsInstanceOfType<PredicateSelectorNode>(((CollectionAccessExpressionNode)statements[0].Expression).Selector);
        Assert.IsInstanceOfType<PredicateSelectorNode>(((CollectionAccessExpressionNode)statements[1].Expression).Selector);
        Assert.IsInstanceOfType<FilterSelectorNode>(((CollectionAccessExpressionNode)statements[2].Expression).Selector);
        Assert.IsInstanceOfType<SumSelectorNode>(((CollectionAccessExpressionNode)statements[3].Expression).Selector);
        Assert.IsInstanceOfType<CountSelectorNode>(((CollectionAccessExpressionNode)statements[4].Expression).Selector);
        Assert.IsInstanceOfType<SelectSelectorNode>(((CollectionAccessExpressionNode)statements[5].Expression).Selector);
    }

    [TestMethod]
    public void Parse_AllowsNestedSelectorsInsideSelectProjection()
    {
        const string script = """
            on Nested(items) {
                let values = items[select item -> item.points[all point where point.x > 0]];
            }
            """;

        var program = EventScriptParser.Parse(script);
        var letStatement = (LetStatementNode)program.Handlers[0].Statements[0];
        var outerAccess = (CollectionAccessExpressionNode)letStatement.Expression;
        var select = (SelectSelectorNode)outerAccess.Selector;
        var nestedAccess = (CollectionAccessExpressionNode)select.Projection;

        Assert.IsInstanceOfType<PredicateSelectorNode>(nestedAccess.Selector);
    }

    [TestMethod]
    public void Parse_RespectsOperatorPrecedence()
    {
        const string script = """
            on Combat {
                let value = 1 + 2 * 3 == 7 && !false;
            }
            """;

        var program = EventScriptParser.Parse(script);
        var letStatement = (LetStatementNode)program.Handlers[0].Statements[0];

        var andExpression = (BinaryExpressionNode)letStatement.Expression;
        Assert.AreEqual("&&", andExpression.Operator);

        var equality = (BinaryExpressionNode)andExpression.Left;
        Assert.AreEqual("==", equality.Operator);

        var addition = (BinaryExpressionNode)equality.Left;
        Assert.AreEqual("+", addition.Operator);

        var multiplication = (BinaryExpressionNode)addition.Right;
        Assert.AreEqual("*", multiplication.Operator);

        var unary = (UnaryExpressionNode)andExpression.Right;
        Assert.AreEqual("!", unary.Operator);
    }

    [TestMethod]
    public void Parse_InvalidSyntax_ThrowsParseException()
    {
        const string script = """
            on Broken {
                let x = ;
            }
            """;

        Assert.ThrowsExactly<EventScriptParseException>(() => EventScriptParser.Parse(script));
    }

    [TestMethod]
    public void Parse_BuildsRandomExpressions()
    {
        const string script = """
            on Randomized(min, max, bonus, board) {
                let x = random 1 to 6;
                let y = random min to max;
                let z = random 1 + bonus to board.fields[count];
            }
            """;

        var program = EventScriptParser.Parse(script);
        var statements = program.Handlers[0].Statements.Cast<LetStatementNode>().ToArray();

        Assert.IsInstanceOfType<RandomExpressionNode>(statements[0].Expression);
        Assert.IsInstanceOfType<RandomExpressionNode>(statements[1].Expression);
        Assert.IsInstanceOfType<RandomExpressionNode>(statements[2].Expression);
    }

    [TestMethod]
    public void Parse_BuildsDiceExpressions()
    {
        const string script = """
            on DiceRolls {
                let a = dice 3d6;
                let b = dice 4d6 keep highest;
                let c = dice 4d6 drop lowest;
                let d = dice 4d6 keep highest 2;
                let e = dice 4d6 drop lowest 2;
            }
            """;

        var program = EventScriptParser.Parse(script);
        var statements = program.Handlers[0].Statements.Cast<LetStatementNode>().ToArray();

        var a = (DiceExpressionNode)statements[0].Expression;
        Assert.AreEqual(3, a.DiceCount);
        Assert.AreEqual(6, a.SideCount);
        Assert.IsNull(a.Modifier);

        Assert.IsInstanceOfType<KeepHighestModifierNode>(((DiceExpressionNode)statements[1].Expression).Modifier);
        Assert.IsInstanceOfType<DropLowestModifierNode>(((DiceExpressionNode)statements[2].Expression).Modifier);
        Assert.AreEqual(2, ((KeepHighestModifierNode)((DiceExpressionNode)statements[3].Expression).Modifier!).Count);
        Assert.AreEqual(2, ((DropLowestModifierNode)((DiceExpressionNode)statements[4].Expression).Modifier!).Count);
    }

    [TestMethod]
    public void Parse_IntegratesRandomAndDiceIntoExpressions()
    {
        const string script = """
            on Mixed {
                let x = dice 3d6 + 2;
                if random 1 to 6 > 3 {
                    emit Passed;
                }
            }
            """;

        var program = EventScriptParser.Parse(script);
        var letStatement = (LetStatementNode)program.Handlers[0].Statements[0];
        var ifStatement = (IfStatementNode)program.Handlers[0].Statements[1];

        var letBinary = (BinaryExpressionNode)letStatement.Expression;
        Assert.IsInstanceOfType<DiceExpressionNode>(letBinary.Left);

        var ifBinary = (BinaryExpressionNode)ifStatement.Condition;
        Assert.IsInstanceOfType<RandomExpressionNode>(ifBinary.Left);
    }

    [TestMethod]
    public void Parse_RandomUpperBound_KeepsArithmeticInsideRandomBeforeOuterComparison()
    {
        const string script = """
            on Mixed(max) {
                if random 1 to max * 2 > 3 {
                    emit Passed;
                }
            }
            """;

        var program = EventScriptParser.Parse(script);
        var ifStatement = (IfStatementNode)program.Handlers[0].Statements[0];
        var condition = (BinaryExpressionNode)ifStatement.Condition;
        var random = (RandomExpressionNode)condition.Left;
        var upperBound = (BinaryExpressionNode)random.ToExpression;

        Assert.AreEqual(">", condition.Operator);
        Assert.AreEqual("*", upperBound.Operator);
    }

    [TestMethod]
    public void Parse_InvalidDiceModifier_ThrowsParseException()
    {
        const string script = """
            on Broken {
                let x = dice 4d6 keep middle;
            }
            """;

        Assert.ThrowsExactly<EventScriptParseException>(() => EventScriptParser.Parse(script));
    }

    [TestMethod]
    public void Parse_InvalidDiceCount_ThrowsParseException()
    {
        const string script = """
            on Broken {
                let x = dice 0d6;
            }
            """;

        Assert.ThrowsExactly<EventScriptParseException>(() => EventScriptParser.Parse(script));
    }

    [TestMethod]
    public void Parse_KeepOrDropMoreThanDice_ThrowsParseException()
    {
        const string script = """
            on Broken {
                let x = dice 4d6 keep highest 5;
            }
            """;

        Assert.ThrowsExactly<EventScriptParseException>(() => EventScriptParser.Parse(script));
    }

    [TestMethod]
    public void Parse_InvalidRandomLiteralRange_ThrowsParseException()
    {
        const string script = """
            on Broken {
                let x = random 6 to 1;
            }
            """;

        Assert.ThrowsExactly<EventScriptParseException>(() => EventScriptParser.Parse(script));
    }
}


/*

on PlayerJoined(match, player) {

    let startFeld = dice 6d5
    emit StartNode(dice[sum d -> d.value])

}


*/
