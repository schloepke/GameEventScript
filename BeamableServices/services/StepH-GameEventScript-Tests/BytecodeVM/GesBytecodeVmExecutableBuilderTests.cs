using StepH.GameEventScript;
using StepH.GameEventScript.Api;
using StepH.GameEventScript.Compiler;
using StepH.GameEventScript.BytecodeVM;
using StepH.GameEventScript.Extensions;
using StepH.GameEventScript.Runtime;
using StepH.GameEventScript.Types;
using static StepH.GameEventScript.Api.GameEventScriptMessage;

namespace StepH_GameEventScript_Tests.BytecodeVM;

[TestClass]
public sealed class GesBytecodeVmExecutableBuilderTests
{
    [TestMethod]
    public void DebugDumpIsDeterministic()
    {
        const string script =
            """
            module Dump

            on Start(value) {
              let total be value + 1
              if total > 1 {
                let done be Done(total: total)
                publish Done(total: total)
              }
            }
            """;

        var first = GameEventScriptManager.Compile(script).DumpBytecode();
        var second = GameEventScriptManager.Compile(script).DumpBytecode();

        Assert.AreEqual(first, second);
        StringAssert.Contains(first, "gameeventscript bytecode v1");
        StringAssert.Contains(first, "handlers[1]");
        StringAssert.Contains(first, "LoadConstant");
        StringAssert.Contains(first, "LoadSlot");
        StringAssert.Contains(first, "BuildMessage");
        StringAssert.Contains(first, "Publish");
        Assert.IsFalse(first.Contains("EvaluateExpression", StringComparison.Ordinal));
    }

    [TestMethod]
    public void CompiledArtifactDoesNotExposeBytecodeVmState()
    {
        var compiledType = typeof(GameEventScriptCompiled);
        var publicMembers = compiledType
            .GetProperties()
            .Select(property => property.PropertyType)
            .Concat(compiledType.GetFields().Select(field => field.FieldType));

        foreach (var type in publicMembers)
        {
            Assert.IsFalse(
                ContainsBytecodeVmType(type),
                $"GameEventScriptCompiled exposes BytecodeVM type '{type.FullName}'.");
        }

        Assert.IsFalse(
            compiledType.GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .Select(field => field.FieldType)
                .Any(ContainsBytecodeVmType),
            "GameEventScriptCompiled stores BytecodeVM state internally.");
        Assert.AreEqual(
            typeof(IReadOnlyList<GameEventScriptBytecodeConstant>),
            compiledType.GetProperty(nameof(GameEventScriptCompiled.ConstantPool))!.PropertyType,
            "GameEventScriptCompiled constants should use portable bytecode constants, not runtime values.");

        var disallowedPortableTypes = CollectPublicBytecodeBoundaryTypes(compiledType)
            .Where(IsDisallowedPortableBytecodeType)
            .Select(type => type.FullName)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.HasCount(
            0,
            disallowedPortableTypes,
            "GameEventScriptCompiled portable bytecode graph exposes compiler, AST, BytecodeVM, or runtime value types:\n" +
            string.Join("\n", disallowedPortableTypes));
    }

    [TestMethod]
    public void BytecodeVmCanRunThroughHostContract()
    {
        const string script =
            """
            module Runtime

            on Start {
              publish Done(value: 3)
            }
            """;

        var published = new List<GameEventScriptMessage>();
        var host = GameEventScriptHost.CreateBuilder()
            .WithPublishedMessageObserver(published.Add)
            .Build()
            .Load(GameEventScriptManager.Compile(script));

        host.Publish(Create("Start"));

        Assert.HasCount(1, published);
        Assert.AreEqual("Done", published[0].Name);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(3), published[0].Arguments["value"]);
    }

    [TestMethod]
    public void BytecodeVmCanRunFromCompiledArtifactRebuiltFromPublicBytecodeData()
    {
        const string script =
            """
            module Runtime

            select boosted(_ value) means value + 2

            on Start(value) {
              let total be boosted(value)
              publish Done(value: total)
            }
            """;

        var bytecode = GameEventScriptManager.Compile(script);
        var rebuiltBytecode = RebuildCompiledArtifactFromPublicData(bytecode);

        var published = new List<GameEventScriptMessage>();
        var host = GameEventScriptHost.CreateBuilder()
            .WithPublishedMessageObserver(published.Add)
            .Build()
            .Load(rebuiltBytecode);

        host.Publish(Create("Start", ("value", GameEventScriptValueFactory.GesInteger(5))));

        Assert.HasCount(1, published);
        Assert.AreEqual("Done", published[0].Name);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(7), published[0].Arguments["value"]);
    }

    [TestMethod]
    public void BytecodeVmRejectsUnknownStatementNodesAtCompileTime()
    {
        var module = new GesModule(
            new Dictionary<string, TypeDefinitionNode>(StringComparer.Ordinal),
            new Dictionary<string, GesCallableDefinition>(StringComparer.Ordinal),
            new Dictionary<string, IReadOnlyList<EventHandlerNode>>(StringComparer.Ordinal)
            {
                ["Start"] =
                [
                    new EventHandlerNode(
                        "Start",
                        Array.Empty<ParameterNode>(),
                        [new UnknownStatementNode()])
                ]
            });

        var exception = Assert.ThrowsExactly<GameEventScriptCompileException>(() => GesBytecodeCompiler.Compile(module));
        StringAssert.Contains(exception.Message, "GameEventScript bytecode lowerer does not support handler 'Start' #0");
        StringAssert.Contains(exception.Message, nameof(UnknownStatementNode));
    }

    private static bool ContainsBytecodeVmType(Type type)
    {
        if ((type.Namespace ?? string.Empty).Contains("StepH.GameEventScript.BytecodeVM", StringComparison.Ordinal))
        {
            return true;
        }

        if (type.IsGenericType && type.GetGenericArguments().Any(ContainsBytecodeVmType))
        {
            return true;
        }

        return type.IsArray && type.GetElementType() is { } elementType && ContainsBytecodeVmType(elementType);
    }

    private static IReadOnlySet<Type> CollectPublicBytecodeBoundaryTypes(Type rootType)
    {
        var visited = new HashSet<Type>();
        var stack = new Stack<Type>();
        stack.Push(rootType);

        while (stack.Count > 0)
        {
            var type = stack.Pop();
            if (!visited.Add(type))
            {
                continue;
            }

            foreach (var nestedType in ExpandType(type))
            {
                stack.Push(nestedType);
            }

            if (!IsGameEventScriptPublicModelType(type))
            {
                continue;
            }

            foreach (var property in type.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.DeclaredOnly))
            {
                stack.Push(property.PropertyType);
            }

            foreach (var field in type.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.DeclaredOnly))
            {
                stack.Push(field.FieldType);
            }

            foreach (var constructor in type.GetConstructors())
            {
                foreach (var parameter in constructor.GetParameters())
                {
                    stack.Push(parameter.ParameterType);
                }
            }
        }

        return visited;
    }

    private static IEnumerable<Type> ExpandType(Type type)
    {
        if (type.IsArray && type.GetElementType() is { } elementType)
        {
            yield return elementType;
        }

        if (type.IsGenericType)
        {
            foreach (var argument in type.GetGenericArguments())
            {
                yield return argument;
            }
        }
    }

    private static bool IsGameEventScriptPublicModelType(Type type)
        => type.IsPublic &&
           type.Namespace is { } typeNamespace &&
           (string.Equals(typeNamespace, "StepH.GameEventScript.Api", StringComparison.Ordinal) ||
            string.Equals(typeNamespace, "StepH.GameEventScript.Runtime", StringComparison.Ordinal));

    private static bool IsDisallowedPortableBytecodeType(Type type)
    {
        var typeNamespace = type.Namespace ?? string.Empty;
        return typeNamespace.StartsWith("StepH.GameEventScript.Compiler", StringComparison.Ordinal) ||
               typeNamespace.StartsWith("StepH.GameEventScript.BytecodeVM", StringComparison.Ordinal) ||
               type == typeof(GameEventScriptValue) ||
               type.IsSubclassOf(typeof(GameEventScriptValue));
    }

    private static GameEventScriptCompiled RebuildCompiledArtifactFromPublicData(GameEventScriptCompiled original)
        => new(
            new GameEventScriptCompileOptions
            {
                EnableDiagnostics = original.Options.EnableDiagnostics,
                Optimize = original.Options.Optimize
            },
            original.StringPool.ToArray(),
            original.ConstantPool.Select(CloneConstant).ToArray(),
            original.Signatures.ToArray(),
            original.ExternalReferences
                .Select(reference => new GameEventScriptExtensionReference(reference.ExtensionName, reference.FunctionName, reference.ArgumentLabels.ToArray()))
                .ToArray(),
            original.NamedArgumentLayouts.Select(layout => (IReadOnlyList<string>)layout.ToArray()).ToArray(),
            original.TypeMetadata.ToArray(),
            original.Callables.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal),
            original.Handlers.ToDictionary(pair => pair.Key, pair => (IReadOnlyList<GameEventScriptBytecodeHandler>)pair.Value.ToArray(), StringComparer.Ordinal),
            original.TypeDefinitions.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal),
            original.MaxStackDepth);

    private static GameEventScriptBytecodeConstant CloneConstant(GameEventScriptBytecodeConstant constant)
        => constant.Kind switch
        {
            GameEventScriptBytecodeConstantKind.Nothing => GameEventScriptBytecodeConstant.Nothing(),
            GameEventScriptBytecodeConstantKind.Boolean => GameEventScriptBytecodeConstant.FromBoolean(constant.Boolean),
            GameEventScriptBytecodeConstantKind.Integer => GameEventScriptBytecodeConstant.FromInteger(constant.Integer),
            GameEventScriptBytecodeConstantKind.Decimal => GameEventScriptBytecodeConstant.FromDecimal(
                constant.Number,
                constant.Unit,
                constant.IsNaN,
                constant.IsInfinity,
                constant.IsNegativeInfinity),
            GameEventScriptBytecodeConstantKind.Percentage => GameEventScriptBytecodeConstant.FromPercentage(constant.Number),
            GameEventScriptBytecodeConstantKind.Text => GameEventScriptBytecodeConstant.FromText(constant.Text ?? string.Empty),
            GameEventScriptBytecodeConstantKind.Tag => GameEventScriptBytecodeConstant.FromTag(constant.Text ?? string.Empty),
            GameEventScriptBytecodeConstantKind.Handler => GameEventScriptBytecodeConstant.FromHandler(constant.Text ?? string.Empty, constant.Labels.ToArray()),
            _ => throw new ArgumentOutOfRangeException(nameof(constant), constant.Kind, "Unknown bytecode constant kind.")
        };

    [TestMethod]
    public void BytecodeVmRunsGeneratedCollectionsByDefault()
    {
        const string script =
            """
            module Collections

            on Start {
              let values be :list[:select item from 1 to 3 -> item]
              publish Done(count: :len values)
            }
            """;

        var published = new List<GameEventScriptMessage>();
        var host = GameEventScriptHost.CreateBuilder()
            .WithPublishedMessageObserver(published.Add)
            .Build()
            .Load(GameEventScriptManager.Compile(script));

        host.Publish(Create("Start"));

        Assert.HasCount(1, published);
        Assert.AreEqual("Done", published[0].Name);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(3), published[0].Arguments["count"]);
    }

    [TestMethod]
    public void BytecodeVmRunsIfElse()
    {
        const string script =
            """
            module Branches

            on Start(first, second) {
              if first {
                let branch be 1
                publish Branch(value: branch)
              } else if second {
                let branch be 2
                publish Branch(value: branch)
              } else {
                let branch be 3
                publish Branch(value: branch)
              }
            }
            """;

        var published = new List<GameEventScriptMessage>();
        var host = GameEventScriptHost.CreateBuilder()
            .WithPublishedMessageObserver(published.Add)
            .Build()
            .Load(GameEventScriptManager.Compile(script));

        host.Publish(Create("Start", ("first", GameEventScriptValueFactory.GesBoolean(true)), ("second", GameEventScriptValueFactory.GesBoolean(false))));
        host.Publish(Create("Start", ("first", GameEventScriptValueFactory.GesBoolean(false)), ("second", GameEventScriptValueFactory.GesBoolean(true))));
        host.Publish(Create("Start", ("first", GameEventScriptValueFactory.GesBoolean(false)), ("second", GameEventScriptValueFactory.GesBoolean(false))));

        Assert.HasCount(3, published);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(1), published[0].Arguments["value"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(2), published[1].Arguments["value"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(3), published[2].Arguments["value"]);
    }

    [TestMethod]
    public void BytecodeVmIfBlockDoesNotLeakLocals()
    {
        const string script =
            """
            module Branches

            on Start(flag) {
              if flag {
                let inner be 7
              }

              publish Done(inner: inner)
            }
            """;

        var published = new List<GameEventScriptMessage>();
        var host = GameEventScriptHost.CreateBuilder()
            .WithPublishedMessageObserver(published.Add)
            .Build()
            .Load(GameEventScriptManager.Compile(script));

        host.Publish(Create("Start", ("flag", GameEventScriptValueFactory.GesBoolean(true))));

        Assert.HasCount(1, published);
        Assert.AreEqual(GameEventScriptNothingValue.Instance, published[0].Arguments["inner"]);
    }

    [TestMethod]
    public void BytecodeVmRunsCollectionFor()
    {
        const string script =
            """
            module Loops

            on Start(items) {
              for item in items {
                if item > 1 {
                  publish Item(value: item * 2)
                }
              }
            }
            """;

        var published = new List<GameEventScriptMessage>();
        var host = GameEventScriptHost.CreateBuilder()
            .WithPublishedMessageObserver(published.Add)
            .Build()
            .Load(GameEventScriptManager.Compile(script));

        host.Publish(Create(
            "Start",
            ("items", GameEventScriptValueFactory.GesList(
            [
                GameEventScriptValueFactory.GesInteger(1),
                GameEventScriptValueFactory.GesInteger(2),
                GameEventScriptValueFactory.GesInteger(3)
            ]))));

        Assert.HasCount(2, published);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(4), published[0].Arguments["value"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(6), published[1].Arguments["value"]);
    }

    [TestMethod]
    public void BytecodeVmCollectionForBlockDoesNotLeakLocals()
    {
        const string script =
            """
            module Loops

            on Start(items) {
              for item in items {
                let inner be item
              }

              publish Done(item: item, inner: inner)
            }
            """;

        var published = new List<GameEventScriptMessage>();
        var host = GameEventScriptHost.CreateBuilder()
            .WithPublishedMessageObserver(published.Add)
            .Build()
            .Load(GameEventScriptManager.Compile(script));

        host.Publish(Create(
            "Start",
            ("items", GameEventScriptValueFactory.GesList(
            [
                GameEventScriptValueFactory.GesInteger(1)
            ]))));

        Assert.HasCount(1, published);
        Assert.AreEqual(GameEventScriptNothingValue.Instance, published[0].Arguments["item"]);
        Assert.AreEqual(GameEventScriptNothingValue.Instance, published[0].Arguments["inner"]);
    }

    [TestMethod]
    public void BytecodeVmRunsTypedLet()
    {
        const string script =
            """
            module TypedLets

            on Start {
              let numberOk as :decimal be '12.2'
              let numberFail as :decimal be 'abc'
              let integerOk as :integer be '12.7'
              let percentageOk as :percentage be 5
              let degreeOk as :degree be 450
              let textOk as :text be 43.9°
              let unitErased as :decimal be 43.9°
              let listOk as :list be 'ab'
              let optionalNone as :optional be missing
              publish Done(numberOk: numberOk, numberFail: numberFail, integerOk: integerOk, percentageOk: percentageOk, degreeOk: degreeOk, textOk: textOk, unitErased: unitErased, listOk: listOk, optionalNone: optionalNone)
            }
            """;

        var published = new List<GameEventScriptMessage>();
        var host = GameEventScriptHost.CreateBuilder()
            .WithPublishedMessageObserver(published.Add)
            .Build()
            .Load(GameEventScriptManager.Compile(script));

        host.Publish(Create("Start"));

        Assert.HasCount(1, published);
        Assert.AreEqual(GameEventScriptValueFactory.GesDecimal(12.2m), published[0].Arguments["numberOk"]);
        Assert.IsTrue(published[0].Arguments["numberFail"].IsNaN());
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(12), published[0].Arguments["integerOk"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesPercentage(0.05m), published[0].Arguments["percentageOk"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesDecimal(450m, GameEventScriptDecimalUnit.Degree), published[0].Arguments["degreeOk"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesText("43.9°"), published[0].Arguments["textOk"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesDecimal(43.9m), published[0].Arguments["unitErased"]);
        Assert.HasCount(2, published[0].Arguments["listOk"].AsList());
        Assert.IsFalse(published[0].Arguments["optionalNone"].AsOptional().HasValue);
    }

    [TestMethod]
    public void BytecodeVmRunsMemberAndIndexedAccess()
    {
        const string script =
            """
            module Access

            on Start(player, key, items, index, units) {
              publish Done(
                hpByMember: player.hp,
                hpByTagKey: player[:hp],
                hpByVariableKey: player[key],
                itemByIndex: items[index],
                nestedName: units[2].name,
                missingMember: player.missing,
                missingIndex: items[99])
            }
            """;

        var unitOne = GameEventScriptValueFactory.GesDictionary(new Dictionary<string, GameEventScriptValue>
        {
            ["name"] = GameEventScriptValueFactory.GesText("Scout")
        });
        var unitTwo = GameEventScriptValueFactory.GesDictionary(new Dictionary<string, GameEventScriptValue>
        {
            ["name"] = GameEventScriptValueFactory.GesText("Knight")
        });

        var published = new List<GameEventScriptMessage>();
        var host = GameEventScriptHost.CreateBuilder()
            .WithPublishedMessageObserver(published.Add)
            .Build()
            .Load(GameEventScriptManager.Compile(script));

        host.Publish(Create(
            "Start",
            ("player", GameEventScriptValueFactory.GesDictionary(new Dictionary<string, GameEventScriptValue>
            {
                ["hp"] = GameEventScriptValueFactory.GesInteger(12)
            })),
            ("key", GameEventScriptValueFactory.GesText("hp")),
            ("items", GameEventScriptValueFactory.GesList(
            [
                GameEventScriptValueFactory.GesInteger(10),
                GameEventScriptValueFactory.GesInteger(20),
                GameEventScriptValueFactory.GesInteger(30)
            ])),
            ("index", GameEventScriptValueFactory.GesInteger(2)),
            ("units", GameEventScriptValueFactory.GesList([unitOne, unitTwo]))));

        Assert.HasCount(1, published);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(12), published[0].Arguments["hpByMember"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(12), published[0].Arguments["hpByTagKey"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(12), published[0].Arguments["hpByVariableKey"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(20), published[0].Arguments["itemByIndex"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesText("Knight"), published[0].Arguments["nestedName"]);
        Assert.AreEqual(GameEventScriptNothingValue.Instance, published[0].Arguments["missingMember"]);
        Assert.AreEqual(GameEventScriptNothingValue.Instance, published[0].Arguments["missingIndex"]);
    }

    [TestMethod]
    public void BytecodeVmRunsCollectionLiterals()
    {
        const string script =
            """
            module Literals

            on Start(seed) {
              let doubled be seed * 2
              let list be [seed, doubled, [label: 'nested']]
              let setValues be :set[seed, seed, 3]
              let dict be [hp: seed + 5, name: 'Scout', nested: [values: [1, 2]], tags: setValues]
              let emptyList be []
              let emptySet be :set[]
              let emptyDict be [:]

              publish Done(
                list: list,
                listFirst: list[1],
                listSecond: list[2],
                nestedLabel: list[3].label,
                setValues: setValues,
                dict: dict,
                hp: dict.hp,
                nestedSecond: dict.nested.values[2],
                tagValues: dict.tags,
                emptyList: emptyList,
                emptySet: emptySet,
                emptyDict: emptyDict)
            }
            """;

        var published = new List<GameEventScriptMessage>();
        var host = GameEventScriptHost.CreateBuilder()
            .WithPublishedMessageObserver(published.Add)
            .Build()
            .Load(GameEventScriptManager.Compile(script));

        host.Publish(Create("Start", ("seed", GameEventScriptValueFactory.GesInteger(7))));

        Assert.HasCount(1, published);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(7), published[0].Arguments["listFirst"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(14), published[0].Arguments["listSecond"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesText("nested"), published[0].Arguments["nestedLabel"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(12), published[0].Arguments["hp"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(2), published[0].Arguments["nestedSecond"]);

        var list = published[0].Arguments["list"].AsList();
        Assert.HasCount(3, list);

        var set = published[0].Arguments["setValues"].AsSet();
        Assert.HasCount(2, set);
        CollectionAssert.Contains(set.ToList(), GameEventScriptValueFactory.GesInteger(3));
        CollectionAssert.Contains(set.ToList(), GameEventScriptValueFactory.GesInteger(7));

        var dictionary = published[0].Arguments["dict"].AsDictionary();
        Assert.AreEqual(GameEventScriptValueFactory.GesText("Scout"), dictionary["name"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(12), dictionary["hp"]);

        var tagValues = published[0].Arguments["tagValues"].AsSet();
        Assert.HasCount(2, tagValues);
        Assert.AreEqual(GameEventScriptValueFactory.GesList([]), published[0].Arguments["emptyList"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesSet([]), published[0].Arguments["emptySet"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesDictionary(new Dictionary<string, GameEventScriptValue>()), published[0].Arguments["emptyDict"]);
    }

    [TestMethod]
    public void BytecodeVmRunsTypeCheck()
    {
        const string script =
            """
            module TypeChecks

            on Start(custom, msg, handler) {
              let integerValue be 12
              let percentValue be 5%
              let degreeValue be 90°
              let meterValue be 100m
              let secondValue be 15s
              let listValue be [1]
              let dictValue be [name: 'Ada']
              let setValue be :set[1, 1, 2]

              publish Done(
                intIsInteger: integerValue is :integer,
                intIsDecimal: integerValue is :decimal,
                percentIsDecimal: percentValue is :decimal,
                degreeIsDecimal: degreeValue is :decimal,
                degreeIsDegree: degreeValue is :degree,
                meterIsMeter: meterValue is :meter,
                secondIsSecond: secondValue is :second,
                textIsText: 'x' is :text,
                tagIsTag: :ready is :tag,
                boolIsBoolean: true is :boolean,
                listIsList: listValue is :list,
                dictIsDictionary: dictValue is :dictionary,
                setIsSet: setValue is :set,
                customIsGauge: custom is :gauge,
                customIsDictionary: custom is :dictionary,
                msgIsMessage: msg is :message,
                msgIsDictionary: msg is :dictionary,
                handlerIsHandler: handler is :handler,
                missingIsNothing: missing is :nothing)
            }
            """;

        var published = new List<GameEventScriptMessage>();
        var host = GameEventScriptHost.CreateBuilder()
            .WithPublishedMessageObserver(published.Add)
            .Build()
            .Load(GameEventScriptManager.Compile(script));

        host.Publish(Create(
            "Start",
            ("custom", GameEventScriptValueFactory.GesCustomType("gauge", new Dictionary<string, GameEventScriptValue>
            {
                ["current"] = GameEventScriptValueFactory.GesInteger(5)
            })),
            ("msg", GameEventScriptValueFactory.GesMessage(Create("Ping", ("value", GameEventScriptValueFactory.GesInteger(1))))),
            ("handler", GameEventScriptValueFactory.GesHandler(GameEventScriptMessageSignature.Create("Ping", ["value"])))));

        Assert.HasCount(1, published);
        Assert.AreEqual(GameEventScriptValueFactory.GesBoolean(true), published[0].Arguments["intIsInteger"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesBoolean(true), published[0].Arguments["intIsDecimal"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesBoolean(true), published[0].Arguments["percentIsDecimal"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesBoolean(true), published[0].Arguments["degreeIsDecimal"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesBoolean(true), published[0].Arguments["degreeIsDegree"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesBoolean(true), published[0].Arguments["meterIsMeter"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesBoolean(true), published[0].Arguments["secondIsSecond"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesBoolean(true), published[0].Arguments["textIsText"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesBoolean(true), published[0].Arguments["tagIsTag"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesBoolean(true), published[0].Arguments["boolIsBoolean"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesBoolean(true), published[0].Arguments["listIsList"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesBoolean(true), published[0].Arguments["dictIsDictionary"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesBoolean(true), published[0].Arguments["setIsSet"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesBoolean(true), published[0].Arguments["customIsGauge"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesBoolean(true), published[0].Arguments["customIsDictionary"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesBoolean(true), published[0].Arguments["msgIsMessage"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesBoolean(false), published[0].Arguments["msgIsDictionary"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesBoolean(true), published[0].Arguments["handlerIsHandler"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesBoolean(true), published[0].Arguments["missingIsNothing"]);
    }

    [TestMethod]
    public void BytecodeVmRunsDirectMessageLiteralExpression()
    {
        const string script =
            """
            module MessageExpressions

            on Start(value) {
              let scaled be value * 2
              let myMessageDirect be Success(message: 'world', value: scaled)

              publish Done(
                isMessage: myMessageDirect is :message,
                name: myMessageDirect.name,
                signature: myMessageDirect.signatureid,
                text: myMessageDirect.arguments.message,
                value: myMessageDirect.arguments.value)
            }
            """;

        var published = new List<GameEventScriptMessage>();
        var host = GameEventScriptHost.CreateBuilder()
            .WithPublishedMessageObserver(published.Add)
            .Build()
            .Load(GameEventScriptManager.Compile(script));

        host.Publish(Create("Start", ("value", GameEventScriptValueFactory.GesInteger(21))));

        Assert.HasCount(1, published);
        Assert.AreEqual(GameEventScriptValueFactory.GesBoolean(true), published[0].Arguments["isMessage"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesText("Success"), published[0].Arguments["name"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesText("Success(message,value)"), published[0].Arguments["signature"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesText("world"), published[0].Arguments["text"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesDecimal(42m), published[0].Arguments["value"]);
    }

    [TestMethod]
    public void BytecodeVmRunsHandlerLiteralAndBind()
    {
        const string script =
            """
            module HandlerExpressions

            on Start(success) {
              let myHandler be Success(message, value)
              let myMessage be myHandler(message: 'hello', value: success)
              let invalidMessage be myHandler(message: 'hello', other: success)

              publish Done(
                handlerIsHandler: myHandler is :handler,
                handlerName: myHandler.name,
                handlerSignature: myHandler.signatureid,
                secondParameter: myHandler.parameters[2],
                messageIsMessage: myMessage is :message,
                messageName: myMessage.name,
                messageSignature: myMessage.signatureid,
                text: myMessage.arguments.message,
                value: myMessage.arguments.value,
                invalidIsNothing: invalidMessage is :nothing)
            }
            """;

        var published = new List<GameEventScriptMessage>();
        var host = GameEventScriptHost.CreateBuilder()
            .WithPublishedMessageObserver(published.Add)
            .Build()
            .Load(GameEventScriptManager.Compile(script));

        host.Publish(Create("Start", ("success", GameEventScriptValueFactory.GesBoolean(true))));

        Assert.HasCount(1, published);
        Assert.AreEqual(GameEventScriptValueFactory.GesBoolean(true), published[0].Arguments["handlerIsHandler"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesText("Success"), published[0].Arguments["handlerName"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesText("Success(message,value)"), published[0].Arguments["handlerSignature"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesText("value"), published[0].Arguments["secondParameter"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesBoolean(true), published[0].Arguments["messageIsMessage"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesText("Success"), published[0].Arguments["messageName"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesText("Success(message,value)"), published[0].Arguments["messageSignature"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesText("hello"), published[0].Arguments["text"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesBoolean(true), published[0].Arguments["value"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesBoolean(true), published[0].Arguments["invalidIsNothing"]);
    }

    [TestMethod]
    public void BytecodeVmEmitsDiagnosticsWhenEnabled()
    {
        const string script =
            """
            module Diagnostics

            rule high(value) means value > 3

            on Start(value) {
              let score be value + 2
              let missingValue be missing
              let isHigh be score is high
              publish Done(score: score, missingValue: missingValue, isHigh: isHigh)
            }
            """;

        var collector = new GameEventScriptDiagnosticTraceCollector();
        var published = new List<GameEventScriptMessage>();
        var host = GameEventScriptHost.CreateBuilder()
            .WithDiagnosticCollector(collector)
            .WithPublishedMessageObserver(published.Add)
            .Build()
            .Load(GameEventScriptManager.Compile(
                script,
                new GameEventScriptCompileOptions { EnableDiagnostics = true }));

        host.Publish(Create("Start", ("value", GameEventScriptValueFactory.GesInteger(5))));

        Assert.HasCount(1, published);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(7), published[0].Arguments["score"]);
        Assert.AreEqual(GameEventScriptNothingValue.Instance, published[0].Arguments["missingValue"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesBoolean(true), published[0].Arguments["isHigh"]);
        Assert.IsTrue(collector.Events.Any(diagnostic => diagnostic.Kind == GameEventScriptDiagnosticEventKind.ParameterBound && diagnostic.Name == "value"));
        Assert.IsTrue(collector.Events.Any(diagnostic => diagnostic.Kind == GameEventScriptDiagnosticEventKind.HandlerInvoked && diagnostic.Name == "Start"));
        Assert.IsTrue(collector.Events.Any(diagnostic => diagnostic.Kind == GameEventScriptDiagnosticEventKind.LetEvaluated && diagnostic.Name == "score"));
        Assert.IsTrue(collector.Events.Any(diagnostic => diagnostic.Kind == GameEventScriptDiagnosticEventKind.LetEvaluated && diagnostic.Name == "missingValue"));
        Assert.IsTrue(collector.Events.Any(diagnostic => diagnostic.Kind == GameEventScriptDiagnosticEventKind.ExpressionEvaluatedToNothing && diagnostic.Name == "missingValue"));
        Assert.IsTrue(collector.Events.Any(diagnostic => diagnostic.Kind == GameEventScriptDiagnosticEventKind.RuleCalled && diagnostic.Name == "high"));
    }

    [TestMethod]
    public void BytecodeVmStandardExtensionsAreIntrinsicAndDoNotRequireDynamicLinking()
    {
        const string script =
            """
            module StandardExtensions

            on Start(value, heading) {
              publish Done(floor: :integer.floor value, wrapped: :degree.wrap heading)
            }
            """;

        var compiled = GameEventScriptManager.Compile(script);
        var dump = compiled.DumpBytecode();

        StringAssert.Contains(dump, "externalReferences[0]");
        Assert.IsFalse(dump.Contains("integer.floor(_)", StringComparison.Ordinal));
        Assert.IsFalse(dump.Contains("degree.wrap(_)", StringComparison.Ordinal));

        var published = new List<GameEventScriptMessage>();
        var host = GameEventScriptHost.CreateBuilder()
            .WithPublishedMessageObserver(published.Add)
            .Build()
            .Load(compiled);

        host.Publish(Create(
            "Start",
            ("value", GameEventScriptValueFactory.GesDecimal(10.4m)),
            ("heading", GameEventScriptValueFactory.GesDegree(-10))));

        Assert.HasCount(1, published);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(10), published[0].Arguments["floor"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesDegree(350), published[0].Arguments["wrapped"]);
    }

    [TestMethod]
    public void BytecodeVmDynamicLinkBindsExtensionReferencesOnHostLoad()
    {
        const string script =
            """
            module Extensions

            on Start(values) {
              let floored be values[:select item -> :math.floor item]
              publish Done(first: floored[1])
            }
            """;

        var compiled = GameEventScriptManager.Compile(script);
        var dump = compiled.DumpBytecode();

        StringAssert.Contains(dump, "externalReferences[1]");
        StringAssert.Contains(dump, "math.floor(_)");
        var exception = Assert.ThrowsExactly<GameEventScriptDynamicLinkException>(() =>
            GameEventScriptHost.CreateBuilder().Build().Load(compiled));
        StringAssert.Contains(exception.Message, "math.floor(_)");
        StringAssert.Contains(exception.Message, "registry is required");

        var published = new List<GameEventScriptMessage>();
        var host = GameEventScriptHost.CreateBuilder()
            .WithRegistry(TestExtensionRegistry.Instance)
            .WithPublishedMessageObserver(published.Add)
            .Build()
            .Load(compiled);

        host.Publish(Create(
            "Start",
            ("values", GameEventScriptValueFactory.GesList(
            [
                GameEventScriptValueFactory.GesDecimal(2.9m),
                GameEventScriptValueFactory.GesDecimal(5.1m)
            ]))));

        Assert.HasCount(1, published);
        Assert.AreEqual(GameEventScriptValueFactory.GesDecimal(2m), published[0].Arguments["first"]);
    }

    [TestMethod]
    public void BytecodeVmDynamicLinkReportsMissingExtensionFunctionWithSignature()
    {
        const string script =
            """
            module MissingExtensions

            on Start {
              let value be :missing.floor 10.4
              publish Done(value: value)
            }
            """;

        var compiled = GameEventScriptManager.Compile(script);
        var exception = Assert.ThrowsExactly<GameEventScriptDynamicLinkException>(() =>
            GameEventScriptHost.CreateBuilder()
                .WithRegistry(TestExtensionRegistry.Instance)
                .Build()
                .Load(compiled));

        StringAssert.Contains(exception.Message, "missing.floor(_)");
        StringAssert.Contains(exception.Message, "not registered in the configured registry");
    }

    [TestMethod]
    public void BytecodeVmDynamicLinkKeepsLabeledArgumentsPositional()
    {
        const string script =
            """
            module OrderedLabels

            on Start(heading, target) {
              let turn be :nav.shortestTurn to: target from: heading
              publish Done(turn: turn)
            }
            """;

        var compiled = GameEventScriptManager.Compile(script);
        var dump = compiled.DumpBytecode();

        StringAssert.Contains(dump, "nav.shortestTurn(to,from)");
        var exception = Assert.ThrowsExactly<GameEventScriptDynamicLinkException>(() =>
            GameEventScriptHost.CreateBuilder()
                .WithRegistry(NavExtensionRegistry.Instance)
                .Build()
                .Load(compiled));
        StringAssert.Contains(exception.Message, "nav.shortestTurn(to,from)");
    }

    [TestMethod]
    public void BytecodeVmStandardExtensionsCannotBeOverriddenByRegistry()
    {
        const string script =
            """
            module StandardOverride

            on Start(value) {
              publish Done(floor: :integer.floor value)
            }
            """;

        var published = new List<GameEventScriptMessage>();
        var host = GameEventScriptHost.CreateBuilder()
            .WithRegistry(StandardOverrideRegistry.Instance)
            .WithPublishedMessageObserver(published.Add)
            .Build()
            .Load(GameEventScriptManager.Compile(script));

        host.Publish(Create("Start", ("value", GameEventScriptValueFactory.GesDecimal(10.9m))));

        Assert.HasCount(1, published);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(10), published[0].Arguments["floor"]);
    }

    private sealed class TestExtensionRegistry : IGameEventScriptExtensionRegistry
    {
        public static readonly TestExtensionRegistry Instance = new();

        private static readonly IGameEventScriptExtensionFunction MathFloor = new DelegateExtensionFunction((_, args) =>
            GameEventScriptFastValue.FromDecimal(Math.Floor(args[0].Number)));

        private TestExtensionRegistry()
        {
        }

        public bool TryResolve(GameEventScriptExtensionReference reference, out IGameEventScriptExtensionFunction function)
        {
            if (reference.SignatureId == "math.floor(_)")
            {
                function = MathFloor;
                return true;
            }

            function = default!;
            return false;
        }
    }

    private sealed class NavExtensionRegistry : IGameEventScriptExtensionRegistry
    {
        public static readonly NavExtensionRegistry Instance = new();

        private static readonly IGameEventScriptExtensionFunction ShortestTurn = new DelegateExtensionFunction((_, args) =>
        {
            var delta = (args[1].Number - args[0].Number + 540m) % 360m - 180m;
            return GameEventScriptFastValue.FromDecimal(delta, GameEventScriptDecimalUnit.Degree);
        });

        private NavExtensionRegistry()
        {
        }

        public bool TryResolve(GameEventScriptExtensionReference reference, out IGameEventScriptExtensionFunction function)
        {
            if (reference.SignatureId == "nav.shortestTurn(from,to)")
            {
                function = ShortestTurn;
                return true;
            }

            function = default!;
            return false;
        }
    }

    private sealed class StandardOverrideRegistry : IGameEventScriptExtensionRegistry
    {
        public static readonly StandardOverrideRegistry Instance = new();

        private static readonly IGameEventScriptExtensionFunction FakeIntegerFloor = new DelegateExtensionFunction((_, _) =>
            GameEventScriptFastValue.FromInteger(999));

        private StandardOverrideRegistry()
        {
        }

        public bool TryResolve(GameEventScriptExtensionReference reference, out IGameEventScriptExtensionFunction function)
        {
            if (reference.SignatureId == "integer.floor(_)")
            {
                function = FakeIntegerFloor;
                return true;
            }

            function = default!;
            return false;
        }
    }

    private delegate GameEventScriptFastValue ExtensionInvoke(GameEventScriptExtensionContext context, ReadOnlySpan<GameEventScriptFastValue> arguments);

    private sealed class DelegateExtensionFunction(ExtensionInvoke invoke) : IGameEventScriptExtensionFunction
    {
        public GameEventScriptFastValue Invoke(GameEventScriptExtensionContext context, ReadOnlySpan<GameEventScriptFastValue> arguments)
            => invoke(context, arguments);
    }

    private sealed record UnknownStatementNode : StatementNode;
}
