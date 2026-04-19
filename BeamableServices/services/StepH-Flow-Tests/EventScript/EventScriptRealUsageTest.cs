using StepH.Flow.EventScript;
using StepH.Flow.EventScript.Interpreter;

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
                publish SetNumberOfPlayers(2)
                publish SetBoardSize(10, 20)
                publish SetNumberOfPushs(20)
                publish CreatePushSeed
                publish SetCorrectTiles('Smiley')
                publish SetIncorrectTiles('Whining', 'Mourning')
                publish SetDeadTile('Devil')
                publish SetInactiveTile('Blank')
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

        var context = new EventScriptCompilationContext();

        var game = new Dictionary<string, EventScriptValue>();


        context.BindExternal("SetBoardSize", p => { game["width"] = p[0]; game["height"] = p[1]; }, 2);
        context.BindExternal("SetNumberOfPlayer", p => { game["playerLimit"] = p[0]; }, 1);
        context.BindExternal("SetNumberOfPushs", p => { game["pushCount"] = p[0]; }, 1);
        context.BindExternal("CreatePushSeed", p => { game["gameSeed"] = System.Random.Shared.NextInt64(); }, 0);


        var interpreter = EventScriptInterpreter.Compile(script: script, context: context);


        Console.WriteLine(interpreter.Emit("Setup", EventScriptValue.Text("player1")));
        Console.WriteLine(interpreter.Emit("Start", EventScriptValue.Dictionary(game)));
        Console.WriteLine(EventScriptValue.Dictionary(game));

    }

    [TestMethod]
    public void CheckingErrorHandlingWorksForLexerParserAndLinker()
    {
        var scriptBroken =
            """
            #module BrokenCombat
            
            rule unitIsDead(unit) means unit[hp] is not at least 0
                     
            on FireAtUnit(unit) {
                let UnitIsDead be unitIsDead(unit); 
            }          
            """;

        var scriptOk1 =
            """
            #module BrokenCombat

            rule unitIsDead(unit) means unit[hp] <= 0
                     
            on FireAtUnit(unit) {
                let unitIsDead be unitIsDead(unit); 
            }          
            """;

        var scriptOk2 =
            """
            #module BrokenCombat

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
