using StepH.Flow.EventScript;
using StepH.Flow.EventScript.Parser;

namespace StepH_Flow_Tests.EventScript.Parser;

[TestClass]
public class EventScriptParsingScenarios
{
    [TestMethod]
    public void JoinedPlayerRulesCanSetStateAndBranch()
    {
        const string script =
            """
            on PlayerJoined(playerId) {
                let isActive be true
                if isActive {
                    publish PlayerReady(arg1: playerId)
                } else {
                    publish PlayerIdle
                }
            }
            """;

        var program = EventScriptParser.Parse(script);
        Assert.HasCount(1, program.Handlers);
        var handler = program.Handlers[0];
        Assert.AreEqual("PlayerJoined", handler.Message);
        CollectionAssert.AreEqual(new[] { "playerId" }, handler.Parameters.ToArray());
        Assert.HasCount(2, handler.Statements);
        Assert.IsInstanceOfType<LetStatementNode>(handler.Statements[0]);
        Assert.IsInstanceOfType<IfStatementNode>(handler.Statements[1]);
    }

    [TestMethod]
    public void ParseReadsModuleDirectiveAndSetsSourceName()
    {
        const string script =
            """
            module CoreRules
            on Start {
                publish Done
            }
            """;

        var module = EventScriptParser.Parse(script, "RulesFile.es");

        Assert.AreEqual("CoreRules", module.ModuleName);
        Assert.AreEqual("RulesFile.es", module.SourceName);
    }

    [TestMethod]
    public void ParseCreatesAnonymousModuleAndUnknownSourceWhenNoneAreProvided()
    {
        const string script =
            """
            on Start {
                publish Done
            }
            """;

        var module = EventScriptParser.Parse(script);

        StringAssert.StartsWith(module.ModuleName, "AnonymousModule_");
        StringAssert.StartsWith(module.SourceName, "UnknownSource_");
    }

    [TestMethod]
    public void TaggedTypesCanDeclareAndCheckValues()
    {
        const string script =
            """
            on Start(value) {
                let numberValue as :decimal be value;
                if numberValue is :decimal {
                    publish Ok;
                }
            }
            """;

        var program = EventScriptParser.Parse(script);
        var letStatement = (LetStatementNode)program.Handlers[0].Statements[0];
        var ifStatement = (IfStatementNode)program.Handlers[0].Statements[1];
        Assert.AreEqual("numberValue", letStatement.Identifier);
        Assert.AreEqual("decimal", letStatement.DeclaredType);
        Assert.IsInstanceOfType<TypeCheckExpressionNode>(ifStatement.Condition);
    }

    [TestMethod]
    public void RulesAndSelectsCanBeDeclaredAndInvoked()
    {
        const string script =
            """
            rule wounded(unit) means unit.hp < unit.maxHp
            select woundedUnits(units) means units[:filter unit where unit is wounded]

            on Start(unit, units) {
                let isWounded be wounded(unit);
                let sameCheck be unit is wounded;
                let result be woundedUnits(units);
                publish Done(arg1: isWounded, arg2: sameCheck, arg3: result);
            }
            """;

        var program = EventScriptParser.Parse(script);
        Assert.HasCount(1, program.RuleDefinitions);
        Assert.HasCount(1, program.SelectDefinitions);
        Assert.AreEqual("wounded", program.RuleDefinitions[0].Name);
        Assert.AreEqual("woundedUnits", program.SelectDefinitions[0].Name);

        var statements = program.Handlers[0].Statements.OfType<LetStatementNode>().ToArray();
        Assert.IsInstanceOfType<CallExpressionNode>(statements[0].Expression);
        Assert.IsInstanceOfType<RulePredicateExpressionNode>(statements[1].Expression);
        Assert.IsInstanceOfType<CallExpressionNode>(statements[2].Expression);
    }

    [TestMethod]
    public void DomainStyleBooleanChecksCanBeParsed()
    {
        const string script =
            """
            on Start(hp, mana, hand, target) {
                if hp is 0 or less {
                    publish Dead;
                };

                if mana is at least 3 {
                    publish Cast;
                };

                if hand is empty {
                    publish Draw;
                };

                if target has value {
                    publish Hit;
                };
            }
            """;

        var program = EventScriptParser.Parse(script);
        var statements = program.Handlers[0].Statements.Cast<IfStatementNode>().ToArray();

        var hpCheck = (BinaryExpressionNode)statements[0].Condition;
        Assert.AreEqual("<=", hpCheck.Operator);

        var manaCheck = (BinaryExpressionNode)statements[1].Condition;
        Assert.AreEqual(">=", manaCheck.Operator);

        var handCheck = (UnaryExpressionNode)statements[2].Condition;
        Assert.AreEqual("empty", handCheck.Operator);

        var targetCheck = (UnaryExpressionNode)statements[3].Condition;
        Assert.AreEqual("has value", targetCheck.Operator);
    }

    [TestMethod]
    public void ChanceAndWeightedChooseCanBeParsed()
    {
        const string script =
            """
            on Start(items, hitChance) {
                let hit be :chance hitChance;
                let target be items[:choose 1 weighted by item -> item.weight];
                publish Done(arg1: hit, arg2: target);
            }
            """;

        var program = EventScriptParser.Parse(script);
        var statements = program.Handlers[0].Statements;
        var hit = (LetStatementNode)statements[0];
        var target = (LetStatementNode)statements[1];

        Assert.IsInstanceOfType<UnaryExpressionNode>(hit.Expression);
        Assert.AreEqual("chance", ((UnaryExpressionNode)hit.Expression).Operator);

        var choose = (ChooseSelectorNode)((CollectionAccessExpressionNode)target.Expression).Selector;
        Assert.AreEqual(1, choose.Count);
        Assert.IsNull(choose.Predicate);
        Assert.AreEqual("item", choose.WeightIdentifier);
        Assert.IsNotNull(choose.WeightExpression);
    }

    [TestMethod]
    public void TaggedTypesWorkWithBeAssignments()
    {
        const string script =
            """
            on Start {
                let value as :decimal be '12';
            }
            """;

        var program = EventScriptParser.Parse(script);
        var letStatement = (LetStatementNode)program.Handlers[0].Statements[0];
        Assert.AreEqual("value", letStatement.Identifier);
        Assert.AreEqual("decimal", letStatement.DeclaredType);
    }

    [TestMethod]
    public void ConditionalValuesCanChooseASingleFallback()
    {
        const string script =
            """
            on Start(age) {
                let score as :decimal be 12 when age is :decimal or age is :boolean otherwise 5;
            }
            """;

        var program = EventScriptParser.Parse(script);
        var letStatement = (LetStatementNode)program.Handlers[0].Statements[0];
        var guarded = (GuardedChoiceExpressionNode)letStatement.Expression;
        Assert.AreEqual("score", letStatement.Identifier);
        Assert.AreEqual("decimal", letStatement.DeclaredType);
        Assert.HasCount(1, guarded.Branches);
    }

    [TestMethod]
    public void ConditionalValuesCanOfferMultipleBranches()
    {
        const string script =
            """
            on Start(age) {
                let score as :decimal be 12 when age is :boolean, or 15 when age is :decimal, otherwise 5;
            }
            """;

        var program = EventScriptParser.Parse(script);
        var letStatement = (LetStatementNode)program.Handlers[0].Statements[0];
        var guarded = (GuardedChoiceExpressionNode)letStatement.Expression;
        Assert.AreEqual("score", letStatement.Identifier);
        Assert.AreEqual("decimal", letStatement.DeclaredType);
        Assert.HasCount(2, guarded.Branches);
    }

    [TestMethod]
    public void TypeTagsStayCaseSensitive()
    {
        const string script =
            """
            on Start(value) {
                let numberValue as :Decimal be value;
            }
            """;

        Assert.ThrowsExactly<EventScriptSyntaxException>(() => EventScriptParser.Parse(script));
    }

    [TestMethod]
    public void LegacyExternalHandlersAreRejected()
    {
        const string script =
            """
            external on Notify(playerId, points)
            """;

        Assert.ThrowsExactly<EventScriptSyntaxException>(() => EventScriptParser.Parse(script));
    }

    [TestMethod]
    public void StatementsCanShareALineWhenSeparatedBySemicolons()
    {
        const string script =
            """
            on Start {
                let lines be 10; let values be 20
            }
            """;

        var program = EventScriptParser.Parse(script);
        var statements = program.Handlers[0].Statements.Cast<LetStatementNode>().ToArray();
        Assert.HasCount(2, statements);
        Assert.AreEqual("lines", statements[0].Identifier);
        Assert.AreEqual("values", statements[1].Identifier);
    }

    [TestMethod]
    public void IncompleteExpressionsCanContinueOnTheNextLine()
    {
        const string script =
            """
            on Start {
                let result be 1 +
                    2
            }
            """;

        var program = EventScriptParser.Parse(script);
        var letStatement = (LetStatementNode)program.Handlers[0].Statements[0];
        var expression = (BinaryExpressionNode)letStatement.Expression;
        Assert.AreEqual("+", expression.Operator);
        Assert.AreEqual(1L, ((IntegerLiteralExpressionNode)expression.Left).Value);
        Assert.AreEqual(2L, ((IntegerLiteralExpressionNode)expression.Right).Value);
    }

    [TestMethod]
    public void CompactDiceLikeNamesRemainValidIdentifiers()
    {
        const string script =
            """
            on Start(d6) {
                let d6 be 1;
                publish Done(arg1: d6);
            }
            """;

        var program = EventScriptParser.Parse(script);
        var handler = program.Handlers[0];
        var letStatement = (LetStatementNode)handler.Statements[0];
        CollectionAssert.AreEqual(new[] { "d6" }, handler.Parameters.ToArray());
        Assert.AreEqual("d6", letStatement.Identifier);
    }

    [TestMethod]
    public void CollectionSelectorsUseTheirOwnTaggedMiniLanguage()
    {
        const string script =
            """
            on ScoreUpdated(items) {
                let hasAny be items[:any item where item.points > 10];
                let allValid be items[:all item where item.points >= 0];
                let topPair be items[:take pair];
                let firstTwo be items[:take first 2];
                let withoutLast be items[:drop last 1];
                let woundedCount be items[:count item where item.hp < item.maxHp];
                let target be items[:choose 1 item where item.alive];
                let randomTarget be items[:choose 1 at random item where item.alive];
                let hand be items[:draw 3];
                let shuffled be items[:shuffle];
                let reversed be items[:reverse];
                let firstItem be items[:first];
                let lastItem be items[:last];
                let firstAlive be items[:first item where item.alive];
                let lastAlive be items[:last item where item.alive];
                let singleBoss be items[:single item where item.role = :boss];
                let filtered be items[:filter item where item.points > 0];
                let total be items[:sum item -> item.points];
                let averagePoints be items[:average item -> item.points];
                let weakest be items[:min item -> item.points];
                let strongest be items[:max item -> item.points];
                let topItem be items[:highest item -> item.points];
                let lowItem be items[:lowest item -> item.points];
                let distinctItems be items[:distinct];
                let distinctNames be items[:distinct by item -> item.name];
                let groupedByFaction be items[:group by item -> item.faction];
                let hasTwo be items[:contains 2];
                let hasAll be items[:contains all [1, 2]];
                let hasAny be items[:contains any [0, 1]];
                let hasOrc be items[:has [faction: 'orc', alive: true]];
                let hasNested be items[:has [owner: [team: 'red']]];
                let orderedByPoints be items[:order by item -> item.points descending];
                let count be :len items;
                let bounded be :clamp 12 between 0 and 10;
                let highest be :max of 1 and 2 and 3;
                let names be items[:select item -> item.name];
            }
            """;

        var program = EventScriptParser.Parse(script);
        var statements = program.Handlers[0].Statements.Cast<LetStatementNode>().ToArray();
        Assert.IsInstanceOfType<PredicateSelectorNode>(((CollectionAccessExpressionNode)statements[0].Expression).Selector);
        Assert.IsInstanceOfType<PredicateSelectorNode>(((CollectionAccessExpressionNode)statements[1].Expression).Selector);
        Assert.IsInstanceOfType<TakePatternSelectorNode>(((CollectionAccessExpressionNode)statements[2].Expression).Selector);
        Assert.IsInstanceOfType<SequenceSliceSelectorNode>(((CollectionAccessExpressionNode)statements[3].Expression).Selector);
        Assert.IsInstanceOfType<SequenceSliceSelectorNode>(((CollectionAccessExpressionNode)statements[4].Expression).Selector);
        Assert.IsInstanceOfType<CountSelectorNode>(((CollectionAccessExpressionNode)statements[5].Expression).Selector);
        Assert.IsInstanceOfType<ChooseSelectorNode>(((CollectionAccessExpressionNode)statements[6].Expression).Selector);
        Assert.IsInstanceOfType<ChooseSelectorNode>(((CollectionAccessExpressionNode)statements[7].Expression).Selector);
        Assert.IsInstanceOfType<DrawSelectorNode>(((CollectionAccessExpressionNode)statements[8].Expression).Selector);
        Assert.IsInstanceOfType<ShuffleSelectorNode>(((CollectionAccessExpressionNode)statements[9].Expression).Selector);
        Assert.IsInstanceOfType<ReverseSelectorNode>(((CollectionAccessExpressionNode)statements[10].Expression).Selector);
        Assert.IsInstanceOfType<EdgeSelectorNode>(((CollectionAccessExpressionNode)statements[11].Expression).Selector);
        Assert.IsInstanceOfType<EdgeSelectorNode>(((CollectionAccessExpressionNode)statements[12].Expression).Selector);
        Assert.IsInstanceOfType<EdgeSelectorNode>(((CollectionAccessExpressionNode)statements[13].Expression).Selector);
        Assert.IsInstanceOfType<EdgeSelectorNode>(((CollectionAccessExpressionNode)statements[14].Expression).Selector);
        Assert.IsInstanceOfType<EdgeSelectorNode>(((CollectionAccessExpressionNode)statements[15].Expression).Selector);
        Assert.IsInstanceOfType<FilterSelectorNode>(((CollectionAccessExpressionNode)statements[16].Expression).Selector);
        Assert.IsInstanceOfType<SumSelectorNode>(((CollectionAccessExpressionNode)statements[17].Expression).Selector);
        Assert.IsInstanceOfType<AverageSelectorNode>(((CollectionAccessExpressionNode)statements[18].Expression).Selector);
        Assert.IsInstanceOfType<MinSelectorNode>(((CollectionAccessExpressionNode)statements[19].Expression).Selector);
        Assert.IsInstanceOfType<MaxSelectorNode>(((CollectionAccessExpressionNode)statements[20].Expression).Selector);
        Assert.IsInstanceOfType<MaxSelectorNode>(((CollectionAccessExpressionNode)statements[21].Expression).Selector);
        Assert.IsInstanceOfType<MinSelectorNode>(((CollectionAccessExpressionNode)statements[22].Expression).Selector);
        Assert.IsInstanceOfType<DistinctSelectorNode>(((CollectionAccessExpressionNode)statements[23].Expression).Selector);
        Assert.IsInstanceOfType<DistinctSelectorNode>(((CollectionAccessExpressionNode)statements[24].Expression).Selector);
        Assert.IsInstanceOfType<GroupBySelectorNode>(((CollectionAccessExpressionNode)statements[25].Expression).Selector);
        Assert.IsInstanceOfType<ContainsSelectorNode>(((CollectionAccessExpressionNode)statements[26].Expression).Selector);
        Assert.IsInstanceOfType<ContainsSelectorNode>(((CollectionAccessExpressionNode)statements[27].Expression).Selector);
        Assert.IsInstanceOfType<ContainsSelectorNode>(((CollectionAccessExpressionNode)statements[28].Expression).Selector);
        Assert.IsInstanceOfType<ObjectMatchSelectorNode>(((CollectionAccessExpressionNode)statements[29].Expression).Selector);
        Assert.IsInstanceOfType<ObjectMatchSelectorNode>(((CollectionAccessExpressionNode)statements[30].Expression).Selector);
        Assert.IsInstanceOfType<OrderBySelectorNode>(((CollectionAccessExpressionNode)statements[31].Expression).Selector);
        Assert.IsInstanceOfType<UnaryExpressionNode>(statements[32].Expression);
        Assert.IsInstanceOfType<ClampExpressionNode>(statements[33].Expression);
        Assert.IsInstanceOfType<VariadicTaggedExpressionNode>(statements[34].Expression);
        Assert.IsInstanceOfType<SelectSelectorNode>(((CollectionAccessExpressionNode)statements[35].Expression).Selector);
    }

    [TestMethod]
    public void TaggedUnaryOperatorsCanBeParsedAsValueOperations()
    {
        const string script =
            """
            on Start(values) {
                let penalty be -12;
                let debt be -12.5;
                let size be :len values;
                let distance be :abs -12.5;
                let rounded be :floor 12.5;
                let fallback be values :default [1];
                let present be has value values;
                let missing be empty values;
                let inverted be not false;
            }
            """;

        var program = EventScriptParser.Parse(script);
        var penaltyStatement = (LetStatementNode)program.Handlers[0].Statements[0];
        var penaltyUnary = (UnaryExpressionNode)penaltyStatement.Expression;
        Assert.AreEqual("-", penaltyUnary.Operator);
        Assert.IsInstanceOfType<IntegerLiteralExpressionNode>(penaltyUnary.Operand);
        Assert.AreEqual(12L, ((IntegerLiteralExpressionNode)penaltyUnary.Operand).Value);

        var debtStatement = (LetStatementNode)program.Handlers[0].Statements[1];
        var debtUnary = (UnaryExpressionNode)debtStatement.Expression;
        Assert.AreEqual("-", debtUnary.Operator);
        Assert.IsInstanceOfType<DecimalLiteralExpressionNode>(debtUnary.Operand);
        Assert.AreEqual(12.5m, ((DecimalLiteralExpressionNode)debtUnary.Operand).Value);

        var sizeStatement = (LetStatementNode)program.Handlers[0].Statements[2];
        var sizeUnary = (UnaryExpressionNode)sizeStatement.Expression;
        Assert.AreEqual("len", sizeUnary.Operator);
        Assert.IsInstanceOfType<IdentifierExpressionNode>(sizeUnary.Operand);
        var absStatement = (LetStatementNode)program.Handlers[0].Statements[3];
        var absUnary = (UnaryExpressionNode)absStatement.Expression;
        Assert.AreEqual("abs", absUnary.Operator);
        Assert.IsInstanceOfType<UnaryExpressionNode>(absUnary.Operand);
        Assert.AreEqual("-", ((UnaryExpressionNode)absUnary.Operand).Operator);
        var roundedStatement = (LetStatementNode)program.Handlers[0].Statements[4];
        var roundedUnary = (UnaryExpressionNode)roundedStatement.Expression;
        Assert.AreEqual("floor", roundedUnary.Operator);
        var fallbackStatement = (LetStatementNode)program.Handlers[0].Statements[5];
        var fallbackBinary = (BinaryExpressionNode)fallbackStatement.Expression;
        Assert.AreEqual("default", fallbackBinary.Operator);
        var presentStatement = (LetStatementNode)program.Handlers[0].Statements[6];
        var presentUnary = (UnaryExpressionNode)presentStatement.Expression;
        Assert.AreEqual("has value", presentUnary.Operator);
        var missingStatement = (LetStatementNode)program.Handlers[0].Statements[7];
        var missingUnary = (UnaryExpressionNode)missingStatement.Expression;
        Assert.AreEqual("empty", missingUnary.Operator);
        var invertedStatement = (LetStatementNode)program.Handlers[0].Statements[8];
        var invertedUnary = (UnaryExpressionNode)invertedStatement.Expression;
        Assert.AreEqual("!", invertedUnary.Operator);
    }

    [TestMethod]
    public void TypeDefinitionsCanDeclareCustomMeterFields()
    {
        const string script =
            """
            record :meter as {
                current: :decimal clamped between 0 and maximum,
                maximum: :decimal clamped between 0 and :infinity,
                percentage: :percentage computed by
                    0% when maximum <= 0,
                    otherwise (current / maximum) as :percentage
            }

            on Start {
                let hp as :meter be [current: 25, maximum: 100];
                publish Done(arg1: hp[:percentage]);
            }
            """;

        var program = EventScriptParser.Parse(script);
        Assert.HasCount(1, program.TypeDefinitions);
        Assert.AreEqual("meter", program.TypeDefinitions[0].Name);
        Assert.HasCount(3, program.TypeDefinitions[0].Fields);
        Assert.AreEqual("percentage", program.TypeDefinitions[0].Fields[2].TypeName);
        Assert.IsNotNull(program.TypeDefinitions[0].Fields[2].ComputedExpression);
        Assert.HasCount(1, program.Handlers);
    }

    [TestMethod]
    public void TagsCanBeUsedAsFirstClassValuesAndDictionaryKeys()
    {
        const string script =
            """
            on Start(myDict) {
                let lookupProperty as :tag be :name;
                let direct be myDict[:name];
                let dynamic be myDict[lookupProperty];
            }
            """;

        var program = EventScriptParser.Parse(script);
        var statements = program.Handlers[0].Statements.Cast<LetStatementNode>().ToArray();
        Assert.IsInstanceOfType<TagLiteralExpressionNode>(statements[0].Expression);
        Assert.IsInstanceOfType<ExpressionSelectorNode>(((CollectionAccessExpressionNode)statements[1].Expression).Selector);
        Assert.IsInstanceOfType<ExpressionSelectorNode>(((CollectionAccessExpressionNode)statements[2].Expression).Selector);
    }

    [TestMethod]
    public void KeysAndValuesCanBeUsedAsPrefixIterators()
    {
        const string script =
            """
            on Start(myDict, myList) {
                let dictKeys be :keys myDict;
                let listValues be :values myList;
                let dictEntries be :entries myDict;
            }
            """;

        var program = EventScriptParser.Parse(script);
        var statements = program.Handlers[0].Statements.Cast<LetStatementNode>().ToArray();

        Assert.AreEqual("keys", ((UnaryExpressionNode)statements[0].Expression).Operator);
        Assert.AreEqual("values", ((UnaryExpressionNode)statements[1].Expression).Operator);
        Assert.AreEqual("entries", ((UnaryExpressionNode)statements[2].Expression).Operator);
    }

    [TestMethod]
    public void MembershipChecksCanDescribeContainmentAndBoundaries()
    {
        const string script =
            """
            on Start(values) {
                let hasItem be 'a' in values;
                let hasValue be 'Ada' value in values;
                let prefixMatch be values starts with ['a'];
                let suffixMatch be values ends with ['z'];
            }
            """;

        var program = EventScriptParser.Parse(script);
        var statements = program.Handlers[0].Statements.Cast<LetStatementNode>().ToArray();
        Assert.AreEqual("in", ((BinaryExpressionNode)statements[0].Expression).Operator);
        Assert.AreEqual("value in", ((BinaryExpressionNode)statements[1].Expression).Operator);
        Assert.AreEqual("starts with", ((BinaryExpressionNode)statements[2].Expression).Operator);
        Assert.AreEqual("ends with", ((BinaryExpressionNode)statements[3].Expression).Operator);
    }

    [TestMethod]
    public void PatternSelectorsCanDescribeCommonHands()
    {
        const string script =
            """
            on Start(roll) {
                let hasPair be roll[:has pair];
                let hasSpecificPair be roll[:has pair of 6];
                let hasThreeKind be roll[:has three of a kind];
                let hasThreeSixes be roll[:has three of 6];
                let hasSixKind be roll[:has six of a kind];
                let hasSevenSixes be roll[:has seven of 6];
                let hasFullHouse be roll[:has full house];
                let hasStraight be roll[:has straight];
            }
            """;

        var program = EventScriptParser.Parse(script);
        var statements = program.Handlers[0].Statements.Cast<LetStatementNode>().ToArray();
        Assert.IsInstanceOfType<CollectionAccessExpressionNode>(statements[0].Expression);
        Assert.IsInstanceOfType<PatternSelectorNode>(((CollectionAccessExpressionNode)statements[0].Expression).Selector);
        Assert.IsInstanceOfType<CollectionAccessExpressionNode>(statements[1].Expression);
        Assert.IsInstanceOfType<PatternSelectorNode>(((CollectionAccessExpressionNode)statements[1].Expression).Selector);
        Assert.IsInstanceOfType<CollectionAccessExpressionNode>(statements[2].Expression);
        Assert.IsInstanceOfType<PatternSelectorNode>(((CollectionAccessExpressionNode)statements[2].Expression).Selector);
        Assert.IsInstanceOfType<CollectionAccessExpressionNode>(statements[3].Expression);
        Assert.IsInstanceOfType<PatternSelectorNode>(((CollectionAccessExpressionNode)statements[3].Expression).Selector);
        Assert.IsInstanceOfType<CollectionAccessExpressionNode>(statements[4].Expression);
        Assert.IsInstanceOfType<PatternSelectorNode>(((CollectionAccessExpressionNode)statements[4].Expression).Selector);
        Assert.IsInstanceOfType<CollectionAccessExpressionNode>(statements[5].Expression);
        Assert.IsInstanceOfType<PatternSelectorNode>(((CollectionAccessExpressionNode)statements[5].Expression).Selector);
        Assert.IsInstanceOfType<CollectionAccessExpressionNode>(statements[6].Expression);
        Assert.IsInstanceOfType<PatternSelectorNode>(((CollectionAccessExpressionNode)statements[6].Expression).Selector);
        Assert.IsInstanceOfType<CollectionAccessExpressionNode>(statements[7].Expression);
        Assert.IsInstanceOfType<PatternSelectorNode>(((CollectionAccessExpressionNode)statements[7].Expression).Selector);
    }

    [TestMethod]
    public void InlineCollectionsCanBeDeclaredWithListDictionaryAndSetLiterals()
    {
        const string script =
            """
            on Start {
                let myList be [1, 2, 3];
                let myValues be [name: 'Hello', position: 1];
                let emptyValues be [:];
                let mySet be :set[1, 2, 2];
            }
            """;

        var program = EventScriptParser.Parse(script);
        var statements = program.Handlers[0].Statements.Cast<LetStatementNode>().ToArray();
        var list = (ListLiteralExpressionNode)statements[0].Expression;
        Assert.HasCount(3, list.Items);
        Assert.IsInstanceOfType<IntegerLiteralExpressionNode>(list.Items[0]);
        var dictionary = (DictionaryLiteralExpressionNode)statements[1].Expression;
        Assert.HasCount(2, dictionary.Entries);
        Assert.AreEqual("name", dictionary.Entries[0].Key);
        Assert.AreEqual("position", dictionary.Entries[1].Key);
        Assert.IsInstanceOfType<TextLiteralExpressionNode>(dictionary.Entries[0].Value);
        var emptyDictionary = (DictionaryLiteralExpressionNode)statements[2].Expression;
        Assert.HasCount(0, emptyDictionary.Entries);
        var set = (SetLiteralExpressionNode)statements[3].Expression;
        Assert.HasCount(3, set.Items);
    }

    [TestMethod]
    public void EscapedQuotesRemainPartOfTextValues()
    {
        const string script =
            """
            on Start {
                let text be 'Hello ''World'', I''m here';
            }
            """;

        var program = EventScriptParser.Parse(script);
        var letStatement = (LetStatementNode)program.Handlers[0].Statements[0];
        var stringLiteral = (TextLiteralExpressionNode)letStatement.Expression;
        Assert.AreEqual("Hello 'World', I'm here", stringLiteral.Value);
    }

    [TestMethod]
    public void SelectorsCanBeNestedInsideProjections()
    {
        const string script =
            """
            on Nested(items) {
                let values be items[:select item -> item.points[:all point where point.x > 0]];
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
    public void DictionarySelectorsCanBuildLookupObjects()
    {
        const string script =
            """
            on Build(items) {
                let byId be items[:dictionary item by item.id];
                let namesById be items[:dictionary item by item.id -> item.name];
            }
            """;

        var program = EventScriptParser.Parse(script);
        var statements = program.Handlers[0].Statements.Cast<LetStatementNode>().ToArray();

        var byId = (DictionarySelectorNode)((CollectionAccessExpressionNode)statements[0].Expression).Selector;
        Assert.AreEqual("item", byId.Identifier);
        Assert.IsNull(byId.ValueProjection);

        var namesById = (DictionarySelectorNode)((CollectionAccessExpressionNode)statements[1].Expression).Selector;
        Assert.AreEqual("item", namesById.Identifier);
        Assert.IsNotNull(namesById.ValueProjection);
    }

    [TestMethod]
    public void GeneratedCollectionsCanBeBuiltFromRangesWithOptionalWhere()
    {
        const string script =
            """
            on Build {
                let squares be :list[:select item from 1 to 5 -> item * item];
                let evenSquares be :list[:select item from 1 to 10 step 2 where item > 3 -> item * item];
                let tags be :set[:select item from 1 to 4 where item >= 2 -> item % 2];
            }
            """;

        var program = EventScriptParser.Parse(script);
        var statements = program.Handlers[0].Statements.Cast<LetStatementNode>().ToArray();

        var squares = (GeneratedCollectionExpressionNode)statements[0].Expression;
        Assert.AreEqual("list", squares.CollectionType);
        Assert.AreEqual("item", squares.Identifier);
        Assert.IsInstanceOfType<RangeIterationSourceNode>(squares.Source);
        Assert.IsNull(((RangeIterationSourceNode)squares.Source).RangeExpression.StepExpression);
        Assert.IsNull(squares.Predicate);

        var evenSquares = (GeneratedCollectionExpressionNode)statements[1].Expression;
        Assert.AreEqual("list", evenSquares.CollectionType);
        Assert.IsInstanceOfType<RangeIterationSourceNode>(evenSquares.Source);
        Assert.IsNotNull(((RangeIterationSourceNode)evenSquares.Source).RangeExpression.StepExpression);
        Assert.IsNotNull(evenSquares.Predicate);

        var tags = (GeneratedCollectionExpressionNode)statements[2].Expression;
        Assert.AreEqual("set", tags.CollectionType);
        Assert.IsInstanceOfType<RangeIterationSourceNode>(tags.Source);
        Assert.IsNotNull(tags.Predicate);
    }

    [TestMethod]
    public void GeneratedCollectionsCanBeBuiltFromCollectionExpressions()
    {
        const string script =
            """
            on Build(values) {
                let doubled be :list[:select item in values -> item * 2];
                let filteredTags be :set[:select item in values where item > 3 -> item % 2];
            }
            """;

        var program = EventScriptParser.Parse(script);
        var statements = program.Handlers[0].Statements.Cast<LetStatementNode>().ToArray();

        var doubled = (GeneratedCollectionExpressionNode)statements[0].Expression;
        Assert.IsInstanceOfType<CollectionIterationSourceNode>(doubled.Source);
        Assert.IsNull(doubled.Predicate);

        var filteredTags = (GeneratedCollectionExpressionNode)statements[1].Expression;
        Assert.IsInstanceOfType<CollectionIterationSourceNode>(filteredTags.Source);
        Assert.IsNotNull(filteredTags.Predicate);
    }

    [TestMethod]
    public void SortingSelectorsCanChooseAscendingOrDescending()
    {
        const string script =
            """
            on Sorted(items) {
                let ascendingItems be items[:sort ascending];
                let descendingItems be items[:sort descending];
            }
            """;

        var program = EventScriptParser.Parse(script);
        var statements = program.Handlers[0].Statements.Cast<LetStatementNode>().ToArray();
        var ascendingSelector = (SortSelectorNode)((CollectionAccessExpressionNode)statements[0].Expression).Selector;
        var descendingSelector = (SortSelectorNode)((CollectionAccessExpressionNode)statements[1].Expression).Selector;
        Assert.AreEqual("ascending", ascendingSelector.Direction);
        Assert.AreEqual("descending", descendingSelector.Direction);
    }

    [TestMethod]
    public void SortingSelectorsRequireAnExplicitDirection()
    {
        const string script =
            """
            on Sorted(items) {
                let values be items[:sort];
            }
            """;

        Assert.ThrowsExactly<EventScriptSyntaxException>(() => EventScriptParser.Parse(script));
    }

    [TestMethod]
    public void LegacyTypeAndUnaryKeywordFormsAreRejected()
    {
        const string legacyTypeScript =
            """
            on Start(value) {
                let score as decimal be value;
            }
            """;

        const string legacyUnaryScript =
            """
            on Start(values) {
                let size be len values;
            }
            """;

        Assert.ThrowsExactly<EventScriptSyntaxException>(() => EventScriptParser.Parse(legacyTypeScript));
        Assert.ThrowsExactly<EventScriptSyntaxException>(() => EventScriptParser.Parse(legacyUnaryScript));
    }

    [TestMethod]
    public void ArithmeticAndLogicKeepTheirPrecedence()
    {
        const string script =
            """
            on Combat {
                let value be 1 + 2 * 3 = 7 ^ true & ~false | false;
            }
            """;

        var program = EventScriptParser.Parse(script);
        var letStatement = (LetStatementNode)program.Handlers[0].Statements[0];
        var orExpression = (BinaryExpressionNode)letStatement.Expression;
        Assert.AreEqual("|", orExpression.Operator);
        Assert.IsInstanceOfType<BooleanLiteralExpressionNode>(orExpression.Right);

        var xorExpression = (BinaryExpressionNode)orExpression.Left;
        Assert.AreEqual("^", xorExpression.Operator);

        var equality = (BinaryExpressionNode)xorExpression.Left;
        Assert.AreEqual("=", equality.Operator);
        var addition = (BinaryExpressionNode)equality.Left;
        Assert.AreEqual("+", addition.Operator);
        Assert.IsInstanceOfType<IntegerLiteralExpressionNode>(addition.Left);
        var multiplication = (BinaryExpressionNode)addition.Right;
        Assert.AreEqual("*", multiplication.Operator);
        Assert.IsInstanceOfType<IntegerLiteralExpressionNode>(multiplication.Left);
        Assert.IsInstanceOfType<IntegerLiteralExpressionNode>(multiplication.Right);

        var andExpression = (BinaryExpressionNode)xorExpression.Right;
        Assert.AreEqual("&", andExpression.Operator);
        Assert.IsInstanceOfType<BooleanLiteralExpressionNode>(andExpression.Left);

        var unary = (UnaryExpressionNode)andExpression.Right;
        Assert.AreEqual("!", unary.Operator);
    }

    [TestMethod]
    public void SqlStyleInequalityCanBeParsed()
    {
        const string script =
            """
            on Combat {
                let value be 1 <> 2;
            }
            """;

        var program = EventScriptParser.Parse(script);
        var letStatement = (LetStatementNode)program.Handlers[0].Statements[0];
        var expression = (BinaryExpressionNode)letStatement.Expression;
        Assert.AreEqual("<>", expression.Operator);
        Assert.IsInstanceOfType<IntegerLiteralExpressionNode>(expression.Left);
        Assert.IsInstanceOfType<IntegerLiteralExpressionNode>(expression.Right);
    }

    [TestMethod]
    public void BrokenStatementsProduceStructuredSyntaxErrors()
    {
        const string script =
            """
            on Broken {
                let x be ;
            }
            """;

        var exception = Assert.ThrowsExactly<EventScriptSyntaxException>(() => EventScriptParser.Parse(script));
        Assert.HasCount(1, exception.Errors);
        Assert.AreEqual(EventScriptSyntaxErrorKind.Parser, exception.Errors[0].Kind);
        Assert.IsTrue(exception.Errors[0].Message.Contains("Unexpected token", StringComparison.Ordinal));
    }

    [TestMethod]
    public void ParserCollectsMultipleStatementErrorsWithinOneHandler()
    {
        const string script =
            """
            on Broken {
                let x be ;
                let y as decimal be 10;
                publish Done(arg1: arg1: }
            """;

        var exception = Assert.ThrowsExactly<EventScriptSyntaxException>(() => EventScriptParser.Parse(script));

        Assert.IsGreaterThanOrEqualTo(2, exception.Errors.Count);
        Assert.IsTrue(exception.Errors.All(error => error.Kind == EventScriptSyntaxErrorKind.Parser));
    }

    [TestMethod]
    public void RandomRangesCanUseLiteralsValuesAndExpressions()
    {
        const string script =
            """
            on Randomized(min, max, bonus, board) {
                let x be :random from 1 to 6;
                let y be :random from min to max;
                let z be :random 1 + bonus to :len board.fields;
                let d be :random from 0.0 to 1.0;
            }
            """;

        var program = EventScriptParser.Parse(script);
        var statements = program.Handlers[0].Statements.Cast<LetStatementNode>().ToArray();
        Assert.IsInstanceOfType<RandomExpressionNode>(statements[0].Expression);
        Assert.IsInstanceOfType<RandomExpressionNode>(statements[1].Expression);
        Assert.IsInstanceOfType<RandomExpressionNode>(statements[2].Expression);
        Assert.IsInstanceOfType<RandomExpressionNode>(statements[3].Expression);
        var literalRandom = (RandomExpressionNode)statements[0].Expression;
        Assert.IsInstanceOfType<IntegerLiteralExpressionNode>(literalRandom.FromExpression);
        Assert.IsInstanceOfType<IntegerLiteralExpressionNode>(literalRandom.ToExpression);
        var decimalRandom = (RandomExpressionNode)statements[3].Expression;
        Assert.IsInstanceOfType<DecimalLiteralExpressionNode>(decimalRandom.FromExpression);
        Assert.IsInstanceOfType<DecimalLiteralExpressionNode>(decimalRandom.ToExpression);
    }

    [TestMethod]
    public void IfAndForCanUseSingleStatementsAndElseIfChains()
    {
        const string script =
            """
            on Start(first, second, items) {
                if first publish One
                else if second publish Two
                else publish Three

                for item in items publish Seen(item: item)
            }
            """;

        var program = EventScriptParser.Parse(script);
        var statements = program.Handlers[0].Statements;
        var ifStatement = (IfStatementNode)statements[0];
        var forStatement = (ForStatementNode)statements[1];

        Assert.IsFalse(ifStatement.ThenBody.IsBlock);
        Assert.IsNotNull(ifStatement.ElseBody);
        Assert.IsFalse(ifStatement.ElseBody.IsBlock);
        Assert.IsInstanceOfType<IfStatementNode>(ifStatement.ElseBody.Statements[0]);

        var nestedIf = (IfStatementNode)ifStatement.ElseBody.Statements[0];
        Assert.IsFalse(nestedIf.ThenBody.IsBlock);
        Assert.IsNotNull(nestedIf.ElseBody);
        Assert.IsFalse(nestedIf.ElseBody.IsBlock);

        Assert.IsInstanceOfType<CollectionIterationSourceNode>(forStatement.Source);
        Assert.IsFalse(forStatement.Body.IsBlock);
        Assert.IsInstanceOfType<PublishStatementNode>(forStatement.Body.Statements[0]);
    }

    [TestMethod]
    public void ForCanIterateCollectionsRangesAndStandaloneRangeExpressions()
    {
        const string script =
            """
            on Start(values) {
                let fullRange be from 1 to 20
                let odds as :range be from 1 to 9 step 2
                for item in values publish Seen(value: item)
                for item from 1 to 20 publish Seen(value: item)
                for item from 10 to 1 step (0 - 2) publish Seen(value: item)
            }
            """;

        var program = EventScriptParser.Parse(script);
        var statements = program.Handlers[0].Statements;
        var fullRangeLet = (LetStatementNode)statements[0];
        var rangeLet = (LetStatementNode)statements[1];
        var firstFor = (ForStatementNode)statements[2];
        var secondFor = (ForStatementNode)statements[3];
        var thirdFor = (ForStatementNode)statements[4];

        Assert.IsInstanceOfType<RangeExpressionNode>(fullRangeLet.Expression);
        Assert.AreEqual("range", rangeLet.DeclaredType);
        Assert.IsInstanceOfType<RangeExpressionNode>(rangeLet.Expression);
        Assert.IsInstanceOfType<CollectionIterationSourceNode>(firstFor.Source);
        Assert.IsInstanceOfType<RangeIterationSourceNode>(secondFor.Source);
        Assert.IsInstanceOfType<RangeIterationSourceNode>(thirdFor.Source);
    }

    [TestMethod]
    public void DirectRangesAreRejectedAfterInForLoopsAndGeneratedCollections()
    {
        const string invalidForScript =
            """
            on Start {
                for item in from 1 to 10 publish Seen(value: item)
            }
            """;

        const string invalidSelectScript =
            """
            on Start {
                let values be :list[:select item in from 1 to 5 -> item]
            }
            """;

        Assert.ThrowsExactly<EventScriptSyntaxException>(() => EventScriptParser.Parse(invalidForScript));
        Assert.ThrowsExactly<EventScriptSyntaxException>(() => EventScriptParser.Parse(invalidSelectScript));
    }

    [TestMethod]
    public void SeededRandomScopesCanBeParsedAsExpressionOrStatement()
    {
        const string script =
            """
            on Start(seed) {
                let values be :random with seed :list[:select item from 1 to 3 -> :random from 1 to 6]
                :random with seed {
                    publish Done(value: :random from 1 to 6)
                }
            }
            """;

        var program = EventScriptParser.Parse(script);
        var letStatement = (LetStatementNode)program.Handlers[0].Statements[0];
        var seededStatement = (SeededRandomStatementNode)program.Handlers[0].Statements[1];

        Assert.IsInstanceOfType<SeededRandomExpressionNode>(letStatement.Expression);
        var seededExpression = (SeededRandomExpressionNode)letStatement.Expression;
        Assert.IsInstanceOfType<IdentifierExpressionNode>(seededExpression.SeedExpression);
        Assert.IsInstanceOfType<GeneratedCollectionExpressionNode>(seededExpression.BodyExpression);

        Assert.IsTrue(seededStatement.Body.IsBlock);
        Assert.IsInstanceOfType<PublishStatementNode>(seededStatement.Body.Statements[0]);
    }

    [TestMethod]
    public void SeededRandomBlocksCannotBeUsedAsValueExpressions()
    {
        const string script =
            """
            on Start(seed) {
                let values be :random with seed {
                    publish Done
                }
            }
            """;

        Assert.ThrowsExactly<EventScriptSyntaxException>(() => EventScriptParser.Parse(script));
    }

    [TestMethod]
    public void IntegerAndDecimalLiteralsAreRepresentedByDistinctAstNodes()
    {
        const string script =
            """
            on Start {
                let integerValue be 12;
                let decimalValue be 12.34;
                let randomValue be :random from 0.0 to 1.0;
            }
            """;

        var program = EventScriptParser.Parse(script);
        var statements = program.Handlers[0].Statements.Cast<LetStatementNode>().ToArray();

        Assert.IsInstanceOfType<IntegerLiteralExpressionNode>(statements[0].Expression);
        Assert.AreEqual(12L, ((IntegerLiteralExpressionNode)statements[0].Expression).Value);
        Assert.IsInstanceOfType<DecimalLiteralExpressionNode>(statements[1].Expression);
        Assert.AreEqual(12.34m, ((DecimalLiteralExpressionNode)statements[1].Expression).Value);

        var randomExpression = (RandomExpressionNode)statements[2].Expression;
        Assert.IsInstanceOfType<DecimalLiteralExpressionNode>(randomExpression.FromExpression);
        Assert.IsInstanceOfType<DecimalLiteralExpressionNode>(randomExpression.ToExpression);
    }

    [TestMethod]
    public void DiceRollsCanUseTaggedExpressionsAndSliceSelectors()
    {
        const string script =
            """
            on DiceRolls {
                let a be :dice 3d6;
                let b be :dice 4d6[:take highest 1];
                let c be :dice 4d6[:drop lowest 1];
                let d be :dice 4d6[:take highest 2];
                let e be :dice 4d6[:drop lowest 2];
            }
            """;

        var program = EventScriptParser.Parse(script);
        var statements = program.Handlers[0].Statements.Cast<LetStatementNode>().ToArray();
        var a = (DiceExpressionNode)statements[0].Expression;
        Assert.AreEqual(3, a.DiceCount);
        Assert.AreEqual(6, a.SideCount);
        Assert.IsInstanceOfType<SequenceSliceSelectorNode>(((CollectionAccessExpressionNode)statements[1].Expression).Selector);
        Assert.IsInstanceOfType<SequenceSliceSelectorNode>(((CollectionAccessExpressionNode)statements[2].Expression).Selector);
        Assert.IsInstanceOfType<SequenceSliceSelectorNode>(((CollectionAccessExpressionNode)statements[3].Expression).Selector);
        Assert.IsInstanceOfType<SequenceSliceSelectorNode>(((CollectionAccessExpressionNode)statements[4].Expression).Selector);
    }

    [TestMethod]
    public void RandomAndDiceValuesCanParticipateInLargerExpressions()
    {
        const string script =
            """
            on Mixed {
                let x be :dice 3d6 + 2;
                if :random 1 to 6 > 3 {
                    publish Passed;
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
    public void RandomUpperBoundsKeepInnerArithmeticTogether()
    {
        const string script =
            """
            on Mixed(max) {
                if :random 1 to max * 2 > 3 {
                    publish Passed;
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
    public void DropSelectorsRequireASupportedScope()
    {
        const string script =
            """
            on Broken {
                let x be :dice 4d6[:drop middle 1];
            }
            """;

        Assert.ThrowsExactly<EventScriptSyntaxException>(() => EventScriptParser.Parse(script));
    }

    [TestMethod]
    public void DiceCountsMustBePositiveIntegers()
    {
        const string script =
            """
            on Broken {
                let x be :dice 0d6;
            }
            """;

        Assert.ThrowsExactly<EventScriptSyntaxException>(() => EventScriptParser.Parse(script));
    }

    [TestMethod]
    public void TakingOrDroppingMoreItemsThanExistStillParses()
    {
        const string script =
            """
            on Broken {
                let x be :dice 4d6[:take highest 5];
            }
            """;

        var program = EventScriptParser.Parse(script);
        Assert.HasCount(1, program.Handlers);
    }

    [TestMethod]
    public void DescendingRandomLiteralBoundsStillParse()
    {
        const string script =
            """
            on Broken {
                let x be :random 6 to 1;
            }
            """;

        var program = EventScriptParser.Parse(script);
        var letStatement = (LetStatementNode)program.Handlers[0].Statements[0];
        Assert.IsInstanceOfType<RandomExpressionNode>(letStatement.Expression);
    }

    [TestMethod]
    public void HandlerHeadersSupportHandlerTagSugar()
    {
        const string script =
            """
            on :handler Shoot(unit, target) {
                publish Done
            }
            """;

        var module = EventScriptParser.Parse(script);
        Assert.HasCount(1, module.Handlers);
        var handler = module.Handlers[0];
        Assert.AreEqual("Shoot", handler.Message);
        CollectionAssert.AreEqual(new[] { "unit", "target" }, handler.Parameters.ToArray());
    }

    [TestMethod]
    public void NamedInvocationParsesAsHandlerBindingWhilePositionalStaysCall()
    {
        const string script =
            """
            on Start(unit, myHandler) {
                myHandler(unit: unit)
                wounded(unit)
            }
            """;

        var module = EventScriptParser.Parse(script);
        var statements = module.Handlers[0].Statements.Cast<ExpressionStatementNode>().ToArray();
        Assert.IsInstanceOfType<HandlerBindExpressionNode>(statements[0].Expression);
        Assert.IsInstanceOfType<CallExpressionNode>(statements[1].Expression);
    }

    [TestMethod]
    public void PublishSupportsMessageExpressionsAndRejectsPositionalMessageArguments()
    {
        const string validScript =
            """
            on Start(unit, target, myHandler, myMessage) {
                let explicit be :message Shoot(unit: unit, target: target)
                publish explicit
                publish myMessage
                publish myHandler(unit: unit, target: target)
                publish Shoot(unit: unit, target: target)
            }
            """;

        var module = EventScriptParser.Parse(validScript);
        var statements = module.Handlers[0].Statements;
        Assert.IsInstanceOfType<MessageLiteralExpressionNode>(((LetStatementNode)statements[0]).Expression);
        Assert.IsInstanceOfType<IdentifierExpressionNode>(((PublishStatementNode)statements[1]).MessageExpression);
        Assert.IsInstanceOfType<IdentifierExpressionNode>(((PublishStatementNode)statements[2]).MessageExpression);
        Assert.IsInstanceOfType<HandlerBindExpressionNode>(((PublishStatementNode)statements[3]).MessageExpression);
        Assert.IsInstanceOfType<MessageLiteralExpressionNode>(((PublishStatementNode)statements[4]).MessageExpression);

        const string invalidScript =
            """
            on Start(unit, target) {
                publish Shoot(unit, target)
            }
            """;

        Assert.ThrowsExactly<EventScriptSyntaxException>(() => EventScriptParser.Parse(invalidScript));
    }

    [TestMethod]
    public void UppercaseInvocationDisambiguatesBetweenHandlerAndMessage()
    {
        const string script =
            """
            on Start(unit, target) {
                let handler be Shoot(unit, target)
                let message be Shoot(unit: unit, target: target)
                let check be wounded(unit)
            }
            """;

        var module = EventScriptParser.Parse(script);
        var statements = module.Handlers[0].Statements.Cast<LetStatementNode>().ToArray();

        Assert.IsInstanceOfType<HandlerLiteralExpressionNode>(statements[0].Expression);
        Assert.IsInstanceOfType<MessageLiteralExpressionNode>(statements[1].Expression);
        Assert.IsInstanceOfType<CallExpressionNode>(statements[2].Expression);
    }

    [TestMethod]
    public void UppercasePositionalInvocationRequiresIdentifierParameters()
    {
        const string script =
            """
            on Start(unit) {
                let invalid be Shoot(unit + 1)
            }
            """;

        Assert.ThrowsExactly<EventScriptSyntaxException>(() => EventScriptParser.Parse(script));
    }

    [TestMethod]
    public void UppercaseEmptyInvocationParsesAsHandlerLiteral()
    {
        const string script =
            """
            on Start {
                let handler be Shoot()
            }
            """;

        var module = EventScriptParser.Parse(script);
        var statement = (LetStatementNode)module.Handlers[0].Statements[0];
        var handler = (HandlerLiteralExpressionNode)statement.Expression;
        Assert.AreEqual("Shoot", handler.Message);
        Assert.HasCount(0, handler.Parameters);
    }

    [TestMethod]
    public void RuleAndSelectNamesAndParametersMustUseIdentifierCasing()
    {
        const string script =
            """
            rule Wounded(unit) means unit.hp < unit.maxHp
            select filter(Units) means Units
            on Start(Target) {
                publish Done
            }
            """;

        Assert.ThrowsExactly<EventScriptSyntaxException>(() => EventScriptParser.Parse(script));
    }
}
