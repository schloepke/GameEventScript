#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections.Generic;
using System.Linq;
using StepH.GameEventScript.Api;
using StepH.GameEventScript.BytecodeVM;
using StepH.GameEventScript.Runtime;
using StepH.GameEventScript.Types;
using static StepH.GameEventScript.Api.GameEventScriptValueFactory;

namespace StepH.GameEventScript.Compiler;

internal static class GesBytecodeCompiler
{
    public static GameEventScriptCompiled Compile(GseModule module, GameEventScriptCompileOptions? options = null)
    {
        _ = module ?? throw new ArgumentNullException(nameof(module));
        var compileOptions = options ?? new GameEventScriptCompileOptions();
        var builder = new CompilerBuilder(module, compileOptions);
        return builder.Build();
    }

    private sealed class CompilerBuilder(GseModule module, GameEventScriptCompileOptions options)
    {
        private readonly Dictionary<string, int> _stringIndex = new(StringComparer.Ordinal);
        private readonly List<string> _stringPool = [];
        private readonly Dictionary<GameEventScriptValue, int> _constantIndex = new();
        private readonly List<GameEventScriptValue> _constantPool = [];
        private readonly Dictionary<string, int> _signatureIndex = new(StringComparer.Ordinal);
        private readonly List<string> _signatures = [];
        private readonly Dictionary<string, GameEventScriptExtensionReference> _externalReferenceIndex = new(StringComparer.Ordinal);
        private readonly List<GameEventScriptExtensionReference> _externalReferences = [];
        private readonly Dictionary<string, int> _namedArgumentLayoutIndex = new(StringComparer.Ordinal);
        private readonly List<IReadOnlyList<string>> _namedArgumentLayouts = [];
        private readonly Dictionary<string, int> _typeMetadataIndex = new(StringComparer.Ordinal);
        private readonly List<string> _typeMetadata = [];
        private readonly List<GameEventScriptBytecodeProgram> _programs = [];
        public GameEventScriptCompiled Build()
        {
            CompileMetadata();
            CompileGlobalDefinitions();
            CompileHandlers();
            var typeDefinitions = GseBytecodeVmExecutionPlanBuilder.CompileTypeDefinitions(
                module.Callables,
                module.TypeDefinitions,
                ResolveExternalReference);
            var handlers = BuildHandlers();
            var maxStackDepth = Math.Max(GetMaxStackDepth(handlers), GetMaxStackDepth(typeDefinitions));

            return new GameEventScriptCompiled(
                options,
                _stringPool.ToArray(),
                _constantPool.ToArray(),
                _signatures.ToArray(),
                _externalReferences.ToArray(),
                _namedArgumentLayouts.ToArray(),
                _typeMetadata.ToArray(),
                _programs.ToArray(),
                handlers,
                typeDefinitions,
                maxStackDepth);
        }

        private IReadOnlyDictionary<string, IReadOnlyList<BytecodeVmCompiledHandler>> BuildHandlers()
        {
            return module.Handlers.ToDictionary(
                pair => pair.Key,
                pair => (IReadOnlyList<BytecodeVmCompiledHandler>)pair.Value
                    .Select((handler, index) => new BytecodeVmCompiledHandler(
                        pair.Key,
                        handler.Parameters,
                        handler.SignatureLabels,
                        GameEventScriptMessageSignature.CreateSignatureId(pair.Key, handler.SignatureLabels),
                        index,
                        FindProgramIndex($"handler:{pair.Key}#{index}"),
                        options.EnableDiagnostics,
                        GseBytecodeVmExecutionPlanBuilder.CompileHandlerPlan(
                            pair.Key,
                            index,
                            handler.Parameters,
                            handler.Statements,
                            module.Callables,
                            module.TypeDefinitions,
                            ResolveExternalReference)))
                    .ToArray(),
                StringComparer.Ordinal);
        }

        private int FindProgramIndex(string programName)
        {
            for (var index = 0; index < _programs.Count; index++)
            {
                if (string.Equals(_programs[index].Name, programName, StringComparison.Ordinal))
                {
                    return index;
                }
            }

            return -1;
        }

        private int ResolveExternalReference(GameEventScriptExtensionReference reference)
        {
            if (GesStandardExtensions.IsStandardReference(reference))
            {
                return -1;
            }

            for (var index = 0; index < _externalReferences.Count; index++)
            {
                if (string.Equals(_externalReferences[index].SignatureId, reference.SignatureId, StringComparison.Ordinal))
                {
                    return index;
                }
            }

            throw new InvalidOperationException($"BytecodeVM invariant failed: external reference '{reference.SignatureId}' was not emitted into bytecode.");
        }

        private static int GetMaxStackDepth(IReadOnlyDictionary<string, IReadOnlyList<BytecodeVmCompiledHandler>> handlers)
            => handlers.Count == 0
                ? 1
                : handlers.Values.SelectMany(group => group).Select(handler => handler.ExecutionPlan.MaxStackDepth).DefaultIfEmpty(1).Max();

        private static int GetMaxStackDepth(IReadOnlyDictionary<string, BytecodeVmTypeDefinition> typeDefinitions)
            => typeDefinitions.Count == 0
                ? 1
                : typeDefinitions.Values.SelectMany(type => type.Fields).SelectMany(field => new[]
                {
                    field.MinimumProgram,
                    field.MaximumProgram,
                    field.ComputedProgram
                }).Where(program => program is not null).Select(program => program!.MaxStackDepth).DefaultIfEmpty(1).Max();

        private void CompileMetadata()
        {
            foreach (var typeName in module.TypeDefinitions.Keys.OrderBy(name => name, StringComparer.Ordinal))
            {
                AddTypeMetadata(typeName);
            }

            foreach (var callable in module.Callables.Values.OrderBy(callable => callable.Name, StringComparer.Ordinal))
            {
                AddString(callable.Name);
                AddSignature(GameEventScriptMessageSignature.CreateSignatureId(callable.Name, callable.SignatureLabels));
            }

            foreach (var type in module.TypeDefinitions.Values)
            {
                foreach (var field in type.Fields)
                {
                    CollectExternalReferences(field.MinimumExpression);
                    CollectExternalReferences(field.MaximumExpression);
                    CollectExternalReferences(field.ComputedExpression);
                }
            }
        }

        private void CompileGlobalDefinitions()
        {
            foreach (var callable in module.Callables.Values.OrderBy(callable => callable.Name, StringComparer.Ordinal))
            {
                CompileCallable(callable);
            }
        }

        private void CompileCallable(GseCallableDefinition callable)
        {
            var instructions = new List<GameEventScriptInstruction>
            {
                new(GameEventScriptOpCode.EvaluateExpression, AddString(GetExpressionDebugName(callable.Expression)), AllocateRegister()),
                new(GameEventScriptOpCode.Return, 0)
            };
            _programs.Add(new GameEventScriptBytecodeProgram(
                $"{callable.Kind}:{callable.Name}",
                instructions.ToArray(),
                registerCount: Math.Max(1, instructions.Count),
                localCount: callable.Parameters.Count));
        }

        private void CompileHandlers()
        {
            foreach (var pair in module.Handlers.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            {
                for (var declarationOrder = 0; declarationOrder < pair.Value.Count; declarationOrder++)
                {
                    var handler = pair.Value[declarationOrder];
                    CompileStatements(
                        $"handler:{pair.Key}#{declarationOrder}",
                        handler.Statements,
                        createsScope: false);
                    AddSignature(GameEventScriptMessageSignature.CreateSignatureId(pair.Key, handler.SignatureLabels));
                }
            }
        }

        private int CompileStatements(string name, IReadOnlyList<StatementNode> statements, bool createsScope)
        {
            var instructions = new List<GameEventScriptInstruction>();
            var localCount = 0;
            foreach (var statement in statements)
            {
                CompileStatement(statement, instructions, ref localCount);
            }

            instructions.Add(new GameEventScriptInstruction(GameEventScriptOpCode.Return));
            var programIndex = _programs.Count;
            _programs.Add(new GameEventScriptBytecodeProgram(
                name,
                instructions.ToArray(),
                registerCount: Math.Max(1, instructions.Count),
                localCount,
                createsScope));
            return programIndex;
        }

        private void CompileStatement(StatementNode statement, List<GameEventScriptInstruction> instructions, ref int localCount)
        {
            switch (statement)
            {
                case PublishStatementNode publish:
                    AddNamedArgumentLayout(publish.MessageExpression);
                    instructions.Add(new GameEventScriptInstruction(
                        GameEventScriptOpCode.EvaluateExpression,
                        AddString(GetExpressionDebugName(publish.MessageExpression)),
                        AllocateRegister()));
                    instructions.Add(new GameEventScriptInstruction(GameEventScriptOpCode.Publish, instructions.Count - 1));
                    break;

                case LetStatementNode let:
                    var localSlot = localCount++;
                    instructions.Add(new GameEventScriptInstruction(
                        GameEventScriptOpCode.EvaluateExpression,
                        AddString(GetExpressionDebugName(let.Expression)),
                        AllocateRegister()));
                    instructions.Add(new GameEventScriptInstruction(
                        GameEventScriptOpCode.StoreLocal,
                        AddString(let.Identifier),
                        localSlot,
                        string.IsNullOrEmpty(let.DeclaredType) ? -1 : AddTypeMetadata(let.DeclaredType!)));
                    break;

                case IfStatementNode ifStatement:
                    var thenIndex = CompileStatements("if.then", ifStatement.ThenBody.Statements, ifStatement.ThenBody.IsBlock);
                    var elseIndex = ifStatement.ElseBody is null
                        ? -1
                        : CompileStatements("if.else", ifStatement.ElseBody.Statements, ifStatement.ElseBody.IsBlock);
                    instructions.Add(new GameEventScriptInstruction(
                        GameEventScriptOpCode.EvaluateExpression,
                        AddString(GetExpressionDebugName(ifStatement.Condition)),
                        AllocateRegister()));
                    instructions.Add(new GameEventScriptInstruction(GameEventScriptOpCode.JumpIfFalse, instructions.Count - 1, thenIndex, elseIndex));
                    break;

                case ForStatementNode forStatement:
                    var bodyIndex = CompileStatements("for.body", forStatement.Body.Statements, forStatement.Body.IsBlock);
                    instructions.Add(new GameEventScriptInstruction(
                        GameEventScriptOpCode.ForEach,
                        AddString(forStatement.Identifier),
                        AddString(GetIterationSourceDebugName(forStatement.Source)),
                        bodyIndex));
                    break;

                case SeededRandomStatementNode seededRandom:
                    var randomBodyIndex = CompileStatements("seededRandom.body", seededRandom.Body.Statements, seededRandom.Body.IsBlock);
                    instructions.Add(new GameEventScriptInstruction(
                        GameEventScriptOpCode.EvaluateExpression,
                        AddString(GetExpressionDebugName(seededRandom.SeedExpression)),
                        AllocateRegister()));
                    instructions.Add(new GameEventScriptInstruction(GameEventScriptOpCode.SeededRandom, instructions.Count - 1, randomBodyIndex));
                    break;

                case ExpressionStatementNode expressionStatement:
                    instructions.Add(new GameEventScriptInstruction(
                        GameEventScriptOpCode.EvaluateExpression,
                        AddString(GetExpressionDebugName(expressionStatement.Expression)),
                        AllocateRegister()));
                    break;
            }
        }

        private int AllocateRegister() => 0;

        private int AddString(string value)
        {
            if (_stringIndex.TryGetValue(value, out var index))
            {
                return index;
            }

            index = _stringPool.Count;
            _stringPool.Add(value);
            _stringIndex[value] = index;
            return index;
        }

        private int AddConstant(GameEventScriptValue value)
        {
            if (_constantIndex.TryGetValue(value, out var index))
            {
                return index;
            }

            index = _constantPool.Count;
            _constantPool.Add(value);
            _constantIndex[value] = index;
            return index;
        }

        private int AddSignature(string value)
        {
            if (_signatureIndex.TryGetValue(value, out var index))
            {
                return index;
            }

            index = _signatures.Count;
            _signatures.Add(value);
            _signatureIndex[value] = index;
            return index;
        }

        private int AddExternalReference(GameEventScriptExtensionReference reference)
        {
            if (GesStandardExtensions.IsStandardReference(reference))
            {
                return -1;
            }

            if (_externalReferenceIndex.TryGetValue(reference.SignatureId, out var existing))
            {
                return _externalReferences.IndexOf(existing);
            }

            var index = _externalReferences.Count;
            _externalReferences.Add(reference);
            _externalReferenceIndex[reference.SignatureId] = reference;
            AddString(reference.ExtensionName);
            AddString(reference.FunctionName);
            foreach (var label in reference.ArgumentLabels)
            {
                AddString(label);
            }

            return index;
        }

        private int AddTypeMetadata(string value)
        {
            if (_typeMetadataIndex.TryGetValue(value, out var index))
            {
                return index;
            }

            index = _typeMetadata.Count;
            _typeMetadata.Add(value);
            _typeMetadataIndex[value] = index;
            AddString(value);
            return index;
        }

        private int AddNamedArgumentLayout(ExpressionNode expression)
        {
            if (expression is not MessageLiteralExpressionNode message)
            {
                return -1;
            }

            var orderedNames = message.Arguments.Select(argument => argument.Name).ToArray();
            var key = string.Join("\u001f", orderedNames);
            if (_namedArgumentLayoutIndex.TryGetValue(key, out var index))
            {
                return index;
            }

            index = _namedArgumentLayouts.Count;
            _namedArgumentLayouts.Add(orderedNames);
            _namedArgumentLayoutIndex[key] = index;
            foreach (var name in orderedNames)
            {
                AddString(name);
            }

            return index;
        }

        private string GetExpressionDebugName(ExpressionNode expression)
        {
            CollectExternalReferences(expression);
            switch (expression)
            {
                case BooleanLiteralExpressionNode boolean:
                    AddConstant(GameEventScriptValueFactory.GesBoolean(boolean.Value));
                    return "literal:boolean";
                case IntegerLiteralExpressionNode integer:
                    AddConstant(GesInteger(integer.Value));
                    return "literal:integer";
                case DecimalLiteralExpressionNode decimalLiteral:
                    AddConstant(GesDecimal(decimalLiteral.Value));
                    return "literal:decimal";
                case PercentageLiteralExpressionNode percentage:
                    AddConstant(GesPercentage(percentage.PercentValue / 100m));
                    return "literal:percentage";
                case UnitDecimalLiteralExpressionNode unitDecimal:
                    AddConstant(GesDecimal(
                        unitDecimal.Value,
                        GameEventScriptDecimalUnits.TryParseTypeName(unitDecimal.UnitName, out var unit) ? unit : null));
                    return "literal:unitDecimal";
                case TextLiteralExpressionNode text:
                    AddConstant(GesText(text.Value));
                    return "literal:text";
                case TagLiteralExpressionNode tag:
                    AddConstant(GesTag(tag.Name));
                    return "literal:tag";
                case ListLiteralExpressionNode list:
                    foreach (var item in list.Items)
                    {
                        GetExpressionDebugName(item);
                    }

                    return "literal:list";
                case SequenceLiteralExpressionNode sequence:
                    foreach (var item in sequence.Items)
                    {
                        GetExpressionDebugName(item);
                    }

                    return "literal:sequence";
                case SetLiteralExpressionNode set:
                    foreach (var item in set.Items)
                    {
                        GetExpressionDebugName(item);
                    }

                    return "literal:set";
                case DictionaryLiteralExpressionNode dictionary:
                    foreach (var entry in dictionary.Entries)
                    {
                        AddString(entry.Key);
                        GetExpressionDebugName(entry.Value);
                    }

                    return "literal:dictionary";
                case IdentifierExpressionNode identifier:
                    AddString(identifier.Name);
                    return "identifier";
                case MessageLiteralExpressionNode message:
                    AddString(message.Message);
                    AddNamedArgumentLayout(message);
                    return "message";
                case HandlerLiteralExpressionNode handler:
                    AddString(handler.Message);
                    foreach (var parameter in handler.Parameters)
                    {
                        AddString(parameter);
                    }

                    return "handler";
                case HandlerBindExpressionNode handlerBind:
                    GetExpressionDebugName(handlerBind.CalleeExpression);
                    foreach (var argument in handlerBind.Arguments)
                    {
                        AddString(argument.Name);
                        GetExpressionDebugName(argument.Expression);
                    }

                    return "handlerBind";
                case CallExpressionNode call:
                    AddString(call.Name);
                    foreach (var argument in call.ArgumentList.Arguments)
                    {
                        GetExpressionDebugName(argument.Expression);
                    }

                    return "call";
                case ExtensionCallExpressionNode extensionCall:
                    foreach (var argument in extensionCall.Arguments)
                    {
                        GetExpressionDebugName(argument.Expression);
                    }

                    return "extensionCall";
                case TypeConstructorExpressionNode typeConstructor:
                    AddTypeMetadata(typeConstructor.TypeName);
                    foreach (var argument in typeConstructor.Arguments)
                    {
                        AddString(argument.Name);
                        GetExpressionDebugName(argument.Expression);
                    }

                    return "typeConstructor";
                case BinaryExpressionNode binary:
                    AddString(binary.Operator);
                    return "binary";
                case CollectionAccessExpressionNode:
                    return "collectionAccess";
                case MemberAccessExpressionNode member:
                    AddString(member.Member);
                    return "memberAccess";
                case TypeCastExpressionNode typeCast:
                    AddTypeMetadata(typeCast.TypeName);
                    return "cast";
                case TypeCheckExpressionNode typeCheck:
                    AddTypeMetadata(typeCheck.TypeName);
                    return "typeCheck";
                case RulePredicateExpressionNode rulePredicate:
                    AddString(rulePredicate.RuleName);
                    return "rulePredicate";
                case ExtensionPredicateExpressionNode extensionPredicate:
                    return "extensionPredicate";
                case RangeExpressionNode:
                    return "range";
                case DiceExpressionNode dice:
                    return $"dice:{dice.DiceCount}d{dice.SideCount}";
                default:
                    return expression.GetType().Name;
            }
        }

        private void CollectExternalReferences(ExpressionNode? expression)
        {
            if (expression is null)
            {
                return;
            }

            switch (expression)
            {
                case ExtensionCallExpressionNode extensionCall:
                    AddExternalReference(new GameEventScriptExtensionReference(
                        extensionCall.ExtensionName,
                        extensionCall.FunctionName,
                        extensionCall.Arguments.Select(argument => argument.Name).ToArray()));
                    foreach (var argument in extensionCall.Arguments)
                    {
                        CollectExternalReferences(argument.Expression);
                    }

                    return;
                case ExtensionPredicateExpressionNode extensionPredicate:
                    AddExternalReference(new GameEventScriptExtensionReference(
                        extensionPredicate.ExtensionName,
                        extensionPredicate.FunctionName,
                        [GameEventScriptMessageSignature.UnlabeledParameterName]));
                    CollectExternalReferences(extensionPredicate.Value);
                    return;
                case UnaryExpressionNode unary:
                    CollectExternalReferences(unary.Operand);
                    return;
                case VariadicTaggedExpressionNode variadic:
                    foreach (var argument in variadic.Arguments)
                    {
                        CollectExternalReferences(argument);
                    }

                    return;
                case ClampExpressionNode clamp:
                    CollectExternalReferences(clamp.Value);
                    CollectExternalReferences(clamp.Minimum);
                    CollectExternalReferences(clamp.Maximum);
                    return;
                case BinaryExpressionNode binary:
                    CollectExternalReferences(binary.Left);
                    CollectExternalReferences(binary.Right);
                    return;
                case GuardedChoiceExpressionNode guarded:
                    foreach (var branch in guarded.Branches)
                    {
                        CollectExternalReferences(branch.ValueExpression);
                        CollectExternalReferences(branch.ConditionExpression);
                    }

                    CollectExternalReferences(guarded.OtherwiseExpression);
                    return;
                case CallExpressionNode call:
                    foreach (var argument in call.ArgumentList.Arguments)
                    {
                        CollectExternalReferences(argument.Expression);
                    }

                    return;
                case MessageLiteralExpressionNode message:
                    foreach (var argument in message.Arguments)
                    {
                        CollectExternalReferences(argument.Expression);
                    }

                    return;
                case HandlerBindExpressionNode bind:
                    CollectExternalReferences(bind.CalleeExpression);
                    foreach (var argument in bind.Arguments)
                    {
                        CollectExternalReferences(argument.Expression);
                    }

                    return;
                case TypeConstructorExpressionNode typeConstructor:
                    foreach (var argument in typeConstructor.Arguments)
                    {
                        CollectExternalReferences(argument.Expression);
                    }

                    return;
                case ListLiteralExpressionNode list:
                    foreach (var item in list.Items) CollectExternalReferences(item);
                    return;
                case SequenceLiteralExpressionNode sequence:
                    foreach (var item in sequence.Items) CollectExternalReferences(item);
                    return;
                case SetLiteralExpressionNode set:
                    foreach (var item in set.Items) CollectExternalReferences(item);
                    return;
                case DictionaryLiteralExpressionNode dictionary:
                    foreach (var entry in dictionary.Entries) CollectExternalReferences(entry.Value);
                    return;
                case TypeCastExpressionNode cast:
                    CollectExternalReferences(cast.Value);
                    return;
                case TypeCheckExpressionNode check:
                    CollectExternalReferences(check.Value);
                    return;
                case MemberAccessExpressionNode member:
                    CollectExternalReferences(member.Target);
                    return;
                case CollectionAccessExpressionNode access:
                    CollectExternalReferences(access.Target);
                    CollectExternalReferences(access.Selector);
                    return;
                case RangeExpressionNode range:
                    CollectExternalReferences(range.FromExpression);
                    CollectExternalReferences(range.ToExpression);
                    CollectExternalReferences(range.StepExpression);
                    return;
                case RandomExpressionNode random:
                    CollectExternalReferences(random.FromExpression);
                    CollectExternalReferences(random.ToExpression);
                    return;
                case SeededRandomExpressionNode seededRandom:
                    CollectExternalReferences(seededRandom.SeedExpression);
                    CollectExternalReferences(seededRandom.BodyExpression);
                    return;
                case GeneratedCollectionExpressionNode generated:
                    CollectExternalReferences(generated.Source);
                    CollectExternalReferences(generated.Predicate);
                    CollectExternalReferences(generated.Projection);
                    return;
            }
        }

        private void CollectExternalReferences(IterationSourceNode source)
        {
            switch (source)
            {
                case CollectionIterationSourceNode collection:
                    CollectExternalReferences(collection.Expression);
                    return;
                case RangeIterationSourceNode range:
                    CollectExternalReferences(range.RangeExpression);
                    return;
            }
        }

        private void CollectExternalReferences(CollectionSelectorNode selector)
        {
            switch (selector)
            {
                case ExpressionSelectorNode expression:
                    CollectExternalReferences(expression.Expression);
                    return;
                case PatternSelectorNode pattern:
                    CollectExternalReferences(pattern.Pattern);
                    return;
                case ObjectMatchSelectorNode objectMatch:
                    CollectExternalReferences(objectMatch.Pattern);
                    return;
                case TakePatternSelectorNode takePattern:
                    CollectExternalReferences(takePattern.Pattern);
                    return;
                case PredicateSelectorNode predicate:
                    CollectExternalReferences(predicate.Predicate);
                    return;
                case CountSelectorNode count:
                    CollectExternalReferences(count.Predicate);
                    return;
                case ChooseSelectorNode choose:
                    CollectExternalReferences(choose.Predicate);
                    CollectExternalReferences(choose.WeightExpression);
                    return;
                case EdgeSelectorNode edge:
                    CollectExternalReferences(edge.Predicate);
                    return;
                case FilterSelectorNode filter:
                    CollectExternalReferences(filter.Predicate);
                    return;
                case SumSelectorNode sum:
                    CollectExternalReferences(sum.Projection);
                    return;
                case AverageSelectorNode average:
                    CollectExternalReferences(average.Projection);
                    return;
                case SelectSelectorNode select:
                    CollectExternalReferences(select.Projection);
                    return;
                case DictionarySelectorNode dictionary:
                    CollectExternalReferences(dictionary.KeyProjection);
                    CollectExternalReferences(dictionary.ValueProjection);
                    return;
                case MinSelectorNode min:
                    CollectExternalReferences(min.Projection);
                    return;
                case MaxSelectorNode max:
                    CollectExternalReferences(max.Projection);
                    return;
                case ContainsSelectorNode contains:
                    CollectExternalReferences(contains.ValueExpression);
                    return;
                case DistinctSelectorNode distinct:
                    CollectExternalReferences(distinct.Projection);
                    return;
                case GroupBySelectorNode group:
                    CollectExternalReferences(group.Projection);
                    return;
                case OrderBySelectorNode order:
                    CollectExternalReferences(order.Projection);
                    return;
            }
        }

        private void CollectExternalReferences(DicePatternNode pattern)
        {
            if (pattern is DiceCountPatternNode diceCount)
            {
                CollectExternalReferences(diceCount.Face);
            }
        }

        private void CollectExternalReferences(ObjectMatchPatternNode pattern)
        {
            foreach (var entry in pattern.Entries)
            {
                CollectExternalReferences(entry.Value);
            }
        }

        private void CollectExternalReferences(ObjectMatchValueNode value)
        {
            switch (value)
            {
                case ObjectMatchExpressionValueNode expression:
                    CollectExternalReferences(expression.Expression);
                    return;
                case ObjectMatchNestedValueNode nested:
                    CollectExternalReferences(nested.Pattern);
                    return;
            }
        }

        private string GetIterationSourceDebugName(IterationSourceNode source)
            => source switch
            {
                CollectionIterationSourceNode collection => $"collection:{GetExpressionDebugName(collection.Expression)}",
                RangeIterationSourceNode range => $"range:{GetExpressionDebugName(range.RangeExpression)}",
                _ => source.GetType().Name
            };
    }
}
