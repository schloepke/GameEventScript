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
        try
        {
            const string script2 =
                """
                on Setup(player) {
                    publish SetNumberOfPlayers(max: 2)
                    publish SetBoardSize(x: 10, y: 20)
                    publish SetNumberOfPushs(pushs: 20)
                    publish CreatePushSeed
                    publish SetCorrectTiles(tiles: 'Smiley')
                    publish SetIncorrectTiles(tiles: ['Whining', 'Mourning'])
                    publish SetDeadTile(tiles: 'Devil')
                    publish SetInactiveTile(tiles: 'Blank')
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
                    } else { 
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

            const string script =
                """
                on Setup(player) {
                    let seed be :random from 1 to 10000000
                    let x be seed + :list[:select y from 1 to 20 -> :random from 1 to 20]
                    publish SetRandomSequence(sequence: x)
                    
                    let rga as :range be from 1 to 10
                    
                    for i in rga publish ShowInfo(info: i)
                }
                """;


            var game = new Dictionary<string, EventScriptValue>();

            var collector = new EventScriptDiagnosticTraceCollector();

            var host = EventScriptHost.CreateBuilder()
                .WithDiagnosticCollector(collector)
                .Build()
                .Load(EventScriptManager.Compile(script))
                .Subscribe("SetBoardSize", ["x", "y"], context =>
                {
                    game["width"] = context.Arguments["x"];
                    game["height"] = context.Arguments["y"];
                })
                .Subscribe("SetNumberOfPlayers", ["max"], context => { game["playerLimit"] = context.Arguments["max"]; })
                .Subscribe("SetNumberOfPushs", ["pushs"], context => { game["pushCount"] = context.Arguments["pushs"]; })
                .Subscribe("SetRandomSequence", ["sequence"], context => { game["randoms"] = context.Arguments["sequence"]; });

            Console.WriteLine(host.Publish(EventScriptMessage.Message("Setup", ("player", EventScriptValue.Text("player1")))));
            Console.WriteLine(host.Publish(EventScriptMessage.Message("Start", ("board", EventScriptValue.Dictionary(game)))));
            Console.WriteLine(EventScriptValue.Dictionary(game));

            Console.WriteLine(collector.ToString());
        }
        catch (Exception e)
        {
            Console.WriteLine(e.Message);
        }
    }

    [TestMethod]
    public void ShowLexerStream()
    {
        const string script =
            """
            module TestModule

            on Setup(player) {
                let someValue be 10; let AnotherValue be 20;
                publish SetNumberOfPlayers(max: 2)
                publish SetBoardSize(x: 10, y: 20)
                publish SetNumberOfPushs(pushs: 20)
                publish CreatePushSeed
                publish SetCorrectTiles(tiles: 'Smiley')
                publish SetIncorrectTiles(tiles: ['Whining', 'Mourning'])
                publish SetDeadTile(tiles: 'Devil')
                publish SetInactiveTile(tiles: 'Blank')
            }
            """;

        var lexer = new EventScriptLexer(script);

        foreach (var token in lexer.Tokenize()) Console.WriteLine(token);
    }

    [TestMethod]
    public void ShowSyntaxTreeJson()
    {
        const string script =
            """
            module TestModule
            
            rule wounded(unit) means unit[hp] <= 0
            select living(units) means units[:filter unit where unit[hp] > 0]

            on Setup(player) {
                let someValue1 as :asDecimal be '10.2'
                let someValue2 as :asDecimal be 10.2
                let someValue3 be '10.2'
                let someValue4 as :decimal be 10.2
                let ruleResult be wounded(player)
                let selectResult be living(player)
                let someHandler be :handler Shot(unit, target)
                let someMessage be :message Shot(unit: 1, target: 2)
                for i from 1 to 10 {
                    let x be i *10
                    publish SpeedBoost(boost: x)
                }
                publish SetNumberOfPlayers(max: 2)
                let someValue be 10; let anotherValue be 20;
                publish SetBoardSize(x: 10, y: 20)
            }
            """;

        Console.WriteLine(EventScriptManager.ParseModule(script, "ast-debug.es").ToJson());
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
            EventScriptManager.ParseModule(scriptBroken);
        }
        catch (Exception e)
        {
            Console.WriteLine(e.Message);
        }

        try
        {
            EventScriptManager.LinkScripts(scriptOk1, scriptOk2, scriptBroken);
        }
        catch (Exception e)
        {
            Console.WriteLine(e.Message);
        }

        try
        {
            EventScriptManager.LinkScripts(scriptOk1, scriptOk2);
        }
        catch (Exception e)
        {
            Console.WriteLine(e.Message);
        }
    }
}
