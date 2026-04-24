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
                .SubscribeForScript(EventScriptManager.CompileExperimental(script))
                .Subscribe("SetBoardSize", ["x", "y"], (message, context) =>
                {
                    game["width"] = message.Arguments["x"];
                    game["height"] = message.Arguments["y"];
                })
                .Subscribe("SetNumberOfPlayers", ["max"], (message, context) => { game["playerLimit"] = message.Arguments["max"]; })
                .Subscribe("SetNumberOfPushs", ["pushs"], (message, context) => { game["pushCount"] = message.Arguments["pushs"]; })
                .Subscribe("SetRandomSequence", ["sequence"], (message, context) => { game["randoms"] = message.Arguments["sequence"]; });

            host.Publish(EventScriptMessage.Message("Setup", ("player", EventScriptValue.Text("player1"))));
            host.Publish(EventScriptMessage.Message("Start", ("board", EventScriptValue.Dictionary(game))));
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

            record :meter as {
                current: :decimal clamped between 0 and maximum,
                maximum: :decimal clamped between 0 and :infinity,
                percentage: :percentage computed by
                    0% when maximum <= 0,
                    otherwise (current / maximum) as :percentage
            }

            rule wounded(unit) means unit[hp] <= 0
            select living(units) means units[:filter unit where unit[hp] > 0]
            
            on Setup(player) {
                let hp as :meter be [current: 25, maximum: 100];
                let someValue1 as :asDecimal be '10.2'
                let someValue2 as :asDecimal be 10.2
                let someValue3 be '10.2'
                let someValue4 as :decimal be 10.2 * 7.24 + 34
                let ruleResult be wounded(player)
                let selectResult be living(player)
                let someHandler as :handler be Shot(unit, target)
                let someMessage as :message be Shot(unit: 1, target: 2)
                for i from 1 to 10 {
                    let x be i *10
                    publish SpeedBoost(boost: x)
                }
                publish SetNumberOfPlayers(max: 2)
                let someValue be 10; let anotherValue be 20;
                publish SetBoardSize(x: 10, y: 20)
            }
            """;

        const string script2 =
            """
            module CombatModule

            rule unitIsDead(unit) means unit[hp] <= 0
            """;

        try
        {
            var parsedModule = EventScriptManager.ParseModule(script, "ast-debug.es");
            var parsedCombatModule = EventScriptManager.ParseModule(script2, "combat-debug.es");
            var linkedModule = EventScriptManager.LinkModules(parsedModule, parsedCombatModule);
            Console.WriteLine(parsedModule.ToJson());
            //Console.WriteLine(linkedModule.ToJson());
        }
        catch (Exception e)
        {
            Console.WriteLine(e.Message);
        }
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
