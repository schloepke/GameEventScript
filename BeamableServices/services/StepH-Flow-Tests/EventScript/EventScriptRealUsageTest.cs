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
}
