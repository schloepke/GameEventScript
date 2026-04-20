using StepH.Flow.EventScript;
using StepH.Flow.EventScript.Interpreter;
using StepH.Flow.EventScript.Parser;
using StepH.Flow.EventScript.Runtime;
using StepH.Flow.EventScript.Types;

namespace StepH_Flow_Tests.EventScript;

[TestClass]
public class EventScriptRealUsageTest
{
    [TestMethod]
    public void Invoke_ExecutesControlFlowAndEmitsEvents()
    {
        const string script =
            """
            on Setup(player) {
                publish SetNumberOfPlayers(arg1: 2)
                publish SetBoardSize(arg1: 10, arg2: 20)
                publish SetNumberOfPushs(arg1: 20)
                publish CreatePushSeed
                publish SetCorrectTiles(arg1: 'Smiley')
                publish SetIncorrectTiles(arg1: 'Whining', arg2: 'Mourning')
                publish SetDeadTile(arg1: 'Devil')
                publish SetInactiveTile(arg1: 'Blank')
            }

            on Start(board) {
                publish GeneratePushs
            }
            
            on PlayerStartRound(player) {
                publish StartTimer
            }

            on TilePressed(tile) {
                if tile[:isLit] {
                    publish SuccessfulPressed
                } else {ja 
                    publish FailedPressed
                    publish RestartGame
                }
            }

            on PlayerFinishedRound() {
                publish TimerStop
            }
            
            on RoundFinished() {
                publish ShowScoreboard
            }
            """;

        var game = new Dictionary<string, EventScriptValue>();

        var host = new EventScriptHost()
            .Load(EventScriptInterpreter.CompileScript(script))
            .BindExternal("SetBoardSize", p => { game["width"] = p[0]; game["height"] = p[1]; }, 2)
            .BindExternal("SetNumberOfPlayer", p => { game["playerLimit"] = p[0]; }, 1)
            .BindExternal("SetNumberOfPushs", p => { game["pushCount"] = p[0]; }, 1)
            .BindExternal("CreatePushSeed", _ => { game["gameSeed"] = System.Random.Shared.NextInt64(); }, 0);

        Console.WriteLine(host.Emit("Setup", EventScriptValue.Text("player1")));
        Console.WriteLine(host.Emit("Start", EventScriptValue.Dictionary(game)));
        Console.WriteLine(EventScriptValue.Dictionary(game));

    }

    [TestMethod]
    public void ShowLexerStream()
    {
        const string script =
            """
            module TestModule
            
            on Setup(player) {
                let someValue be 10; let AnotherValue be 20;
                publish SetNumberOfPlayers(arg1: arg1: 2)
                publish Set_BoardSize(arg1: arg1: 10, arg2: arg2: 20)
                publish SetNumberOfPushs(arg1: arg1: 20)
                publish CreatePushSeed
                publish SetCorrectTiles(arg1: arg1: 'Smiley')
                publish SetIncorrectTiles(arg1: arg1: 'Whining', arg2: arg2: 'Mourning')
                publish SetDeadTile(arg1: arg1: 'Devil')
                publish SetInactiveTile(arg1: arg1: 'Blank')
            }
            """;

        var lexer = new EventScriptLexer(script);

        foreach (var token in lexer.Tokenize()) Console.WriteLine(token);

    }

    [TestMethod]
    public void CheckingErrorHandlingWorksForLexerParserAndLinker()
    {
        var scriptBroken =
            """
            module BrokenCombat
            
            rule unitIsDead(un%it) means unit[hp] is not at least 0
                     
            on FireAtUnit(unit) {
                let UnitIsDead be unitIsDead(unit); 
            }          
            """;

        var scriptOk1 =
            """
            module BrokenCombat

            rule unitIsDead(unit) means unit[hp] <= 0
                     
            on FireAtUnit(unit) {
                let unitIsDead be unitIsDead(unit); 
            }          
            """;

        var scriptOk2 =
            """
            module BrokenCombat

            rule unitIsDead(unit) means unit[hp] <= 0
                     
            on FireAtUnit(unit, origin) {
                let unitIsDead be unitIsDead(unit); 
            }          
            """;

        try
        {
            StepH.Flow.EventScript.EventScript.Parse(scriptBroken);
        }
        catch (Exception e)
        {
            Console.WriteLine(e.Message);
        }
        try
        {
            StepH.Flow.EventScript.EventScript.Link(scriptOk1, scriptOk2, scriptBroken);
        }
        catch (Exception e)
        {
            Console.WriteLine(e.Message);
        }
        try
        {
            StepH.Flow.EventScript.EventScript.Link(scriptOk1, scriptOk2);
        }
        catch (Exception e)
        {
            Console.WriteLine(e.Message);
        }
    }
}
