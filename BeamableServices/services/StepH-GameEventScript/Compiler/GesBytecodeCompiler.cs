#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections.Generic;
using System.Linq;
using StepH.GameEventScript.Api;
using StepH.GameEventScript.Runtime;
using StepH.GameEventScript.Types;

namespace StepH.GameEventScript.Compiler;

internal static class GesBytecodeCompiler
{
    public static GameEventScriptCompiled Compile(GesModule module, GameEventScriptCompileOptions? options = null)
    {
        _ = module ?? throw new ArgumentNullException(nameof(module));
        var compileOptions = (options ?? new GameEventScriptCompileOptions()).NormalizeDebugInfo();
        var builder = new CompilerBuilder(module, compileOptions);
        return builder.Build();
    }

    private sealed class CompilerBuilder(GesModule module, GameEventScriptCompileOptions options)
    {
        private readonly Dictionary<string, int> _stringIndex = new(StringComparer.Ordinal);
        private readonly List<string> _stringPool = [];
        private readonly Dictionary<string, int> _externalReferenceIndex = new(StringComparer.Ordinal);
        private readonly List<GameEventScriptExtensionReference> _externalReferences = [];
        private readonly Dictionary<string, int> _externalTypeConstructorReferenceIndex = new(StringComparer.Ordinal);
        private readonly List<GameEventScriptExternalTypeConstructorReference> _externalTypeConstructorReferences = [];
        private readonly Dictionary<string, int> _uShortListIndex = new(StringComparer.Ordinal);
        private readonly List<IReadOnlyList<ushort>> _uShortListPool = [];
        private readonly HashSet<string> _outboundMessageSignatureIds = [];
        private readonly List<GameEventScriptMessageSignature> _outboundMessageSignatures = [];
        private readonly Dictionary<GameEventScriptBytecodeHandler, EventHandlerNode> _handlerSources =
            new(ReferenceEqualityComparer<GameEventScriptBytecodeHandler>.Instance);

        public GameEventScriptCompiled Build()
        {
            CollectSourceMetadata();
            CollectSourceBytecodeMetadata();

            var typeDefinitions = GesBytecodeLowerer.CompileTypeDefinitions(
                module.Callables,
                module.TypeDefinitions);

            var callables = GesBytecodeLowerer.CompileCallableDefinitions(
                module.Callables,
                module.TypeDefinitions);

            var handlers = BuildHandlers();
            var linearBuilder = new GesLinearBytecodeBuilder(
                AddString,
                AddUShortList,
                AddOutboundMessageSignature,
                AddExternalReference,
                AddExternalTypeConstructorReference,
                module.Callables,
                module.TypeDefinitions,
                module.ExternalTypeDefinitions,
                options.EnableDebugInfo);
            linearBuilder.AddHandlers(handlers.Values.SelectMany(group => group), _handlerSources);
            linearBuilder.AddCallables(callables.Values, module.Callables);
            linearBuilder.AddTypeDefinitions(typeDefinitions.Values, module.TypeDefinitions);
            linearBuilder.PatchDeferredCallableAddresses();

            return new GameEventScriptCompiled(
                options,
                module.ModuleName,
                _stringPool.ToArray(),
                _uShortListPool.ToArray(),
                _outboundMessageSignatures.ToArray(),
                _externalReferences.ToArray(),
                _externalTypeConstructorReferences.ToArray(),
                callables,
                handlers,
                typeDefinitions,
                linearBuilder.Code.ToArray(),
                linearBuilder.MaxFrameSlots,
                linearBuilder.DebugSegment);
        }

        private IReadOnlyDictionary<string, IReadOnlyList<GameEventScriptBytecodeHandler>> BuildHandlers()
            => module.Handlers.ToDictionary(
                pair => pair.Key,
                pair => (IReadOnlyList<GameEventScriptBytecodeHandler>)pair.Value
                    .Select((handler, index) =>
                    {
                        var bytecodeHandler = new GameEventScriptBytecodeHandler(
                            pair.Key,
                            handler.DispatchKind == EventHandlerDispatchKind.MessageEnvelope
                                ? GameEventScriptBytecodeHandlerDispatchKind.MessageEnvelope
                                : GameEventScriptBytecodeHandlerDispatchKind.ExactSignature,
                            handler.Parameters,
                            handler.SignatureLabels,
                            index,
                            GesBytecodeLowerer.CollectHandlerSlots(
                                pair.Key,
                                index,
                                handler.Parameters,
                                handler.Statements,
                                module.Callables,
                                module.TypeDefinitions),
                            handler.ParameterList.Select(parameter => parameter.DeclaredType).ToArray(),
                            handler.MatchingTags,
                            handler.WithoutTags);
                        _handlerSources[bytecodeHandler] = handler;
                        return bytecodeHandler;
                    })
                    .ToArray(),
                StringComparer.Ordinal);

        private int AddExternalReference(GameEventScriptExtensionReference reference)
        {
            if (GesStandardExtensions.IsStandardReference(reference))
            {
                return -1;
            }

            if (_externalReferenceIndex.TryGetValue(reference.SignatureId, out var existing))
            {
                return existing;
            }

            var index = _externalReferences.Count;
            _externalReferences.Add(reference);
            _externalReferenceIndex[reference.SignatureId] = index;
            AddString(reference.ExtensionName);
            AddString(reference.FunctionName);
            foreach (var label in reference.ArgumentLabels)
            {
                AddString(label);
            }

            return index;
        }

        private int AddExternalTypeConstructorReference(GameEventScriptExternalTypeConstructorReference reference)
        {
            if (!module.ExternalTypeDefinitions.TryGetValue(reference.TypeName, out var typeDefinition))
            {
                throw new GameEventScriptCompileException($"GameEventScript external type ':{reference.TypeName}' is not registered.");
            }

            if (!typeDefinition.TryGetConstructor(reference.ArgumentLabels, out _))
            {
                throw new GameEventScriptCompileException($"GameEventScript external type constructor ':{reference.SignatureId}' is not registered.");
            }

            if (_externalTypeConstructorReferenceIndex.TryGetValue(reference.SignatureId, out var existing))
            {
                return existing;
            }

            var index = _externalTypeConstructorReferences.Count;
            _externalTypeConstructorReferences.Add(reference);
            _externalTypeConstructorReferenceIndex[reference.SignatureId] = index;
            AddString(reference.TypeName);
            foreach (var label in reference.ArgumentLabels)
            {
                AddString(label);
            }

            return index;
        }

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

        private int AddUShortList(IReadOnlyList<ushort> values)
        {
            var key = string.Join("\u001f", values.Select(value => value.ToString(System.Globalization.CultureInfo.InvariantCulture)));
            if (_uShortListIndex.TryGetValue(key, out var index))
            {
                return index;
            }

            index = _uShortListPool.Count;
            _uShortListPool.Add(values.ToArray());
            _uShortListIndex[key] = index;
            return index;
        }

        private int AddMessageShape(string messageName, IReadOnlyList<string> argumentNames)
        {
            var shape = new ushort[argumentNames.Count + 1];
            shape[0] = ToUShortIndex(AddString(GameEventScriptMessageSignature.NormalizeMessageName(messageName)), "message name string pool index");
            for (var index = 0; index < argumentNames.Count; index++)
            {
                shape[index + 1] = ToUShortIndex(AddString(argumentNames[index]), "message argument string pool index");
            }

            return AddUShortList(shape);
        }

        private int AddOutboundMessageSignature(MessageLiteralExpressionNode message)
        {
            var argumentNames = message.Arguments.Select(argument => argument.Name).ToArray();
            var signature = GameEventScriptMessageSignature.Create(message.Message, argumentNames);
            if (!_outboundMessageSignatureIds.Add(signature.SignatureId))
            {
                for (var index = 0; index < _outboundMessageSignatures.Count; index++)
                {
                    if (string.Equals(_outboundMessageSignatures[index].SignatureId, signature.SignatureId, StringComparison.Ordinal)) return index;
                }

                throw new GameEventScriptCompileException($"GameEventScript outbound message signature '{signature.SignatureId}' was indexed but not stored.");
            }

            var signatureIndex = _outboundMessageSignatures.Count;
            _outboundMessageSignatures.Add(signature);
            AddString(signature.Name);
            foreach (var argumentName in signature.Parameters)
            {
                AddString(argumentName);
            }

            return signatureIndex;
        }

        private static ushort ToUShortIndex(int value, string name)
        {
            if (value < 0 || value > ushort.MaxValue)
            {
                throw new GameEventScriptCompileException($"GameEventScript bytecode {name} must fit into an unsigned 16-bit index.");
            }

            return (ushort)value;
        }

        private void CollectSourceMetadata()
        {
            AddUShortList([]);

            foreach (var type in module.TypeDefinitions.Values.OrderBy(type => type.Name, StringComparer.Ordinal))
            {
                AddString(type.Name);
                foreach (var field in type.Fields)
                {
                    AddString(field.Name);
                    AddString(field.TypeName);
                }
            }

            foreach (var callable in module.Callables.Values.OrderBy(callable => callable.Name, StringComparer.Ordinal))
            {
                AddString(callable.Name);
                foreach (var parameter in callable.Parameters)
                {
                    AddString(parameter);
                }

                foreach (var label in callable.SignatureLabels)
                {
                    AddString(label);
                }

                foreach (var parameter in callable.ParameterList)
                {
                    if (!string.IsNullOrEmpty(parameter.DeclaredType))
                    {
                        AddString(parameter.DeclaredType!);
                    }
                }

            }

            foreach (var pair in module.Handlers.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            {
                AddString(pair.Key);
                foreach (var handler in pair.Value)
                {
                    foreach (var parameter in handler.Parameters)
                    {
                        AddString(parameter);
                    }

                    foreach (var label in handler.SignatureLabels)
                    {
                        AddString(label);
                    }

                    foreach (var parameter in handler.ParameterList)
                    {
                        if (!string.IsNullOrEmpty(parameter.DeclaredType))
                        {
                            AddString(parameter.DeclaredType!);
                        }
                    }

                    foreach (var tag in handler.MatchingTags)
                    {
                        AddString(tag);
                    }

                    foreach (var tag in handler.WithoutTags)
                    {
                        AddString(tag);
                    }

                }
            }

            foreach (var type in module.ExternalTypeDefinitions.Values.OrderBy(type => type.Name, StringComparer.Ordinal))
            {
                AddString(type.Name);
                foreach (var field in type.Fields)
                {
                    AddString(field.Name);
                    AddString(field.TypeName);
                }
            }
        }

        private void CollectSourceBytecodeMetadata()
        {
            foreach (var type in module.TypeDefinitions.Values.OrderBy(type => type.Name, StringComparer.Ordinal))
            {
                foreach (var field in type.Fields)
                {
                    CollectSourceExpressionMetadata(field.MinimumExpression);
                    CollectSourceExpressionMetadata(field.MaximumExpression);
                    CollectSourceExpressionMetadata(field.ComputedExpression);
                }
            }

            foreach (var callable in module.Callables.Values.OrderBy(callable => callable.Name, StringComparer.Ordinal))
            {
                CollectSourceExpressionMetadata(callable.Expression);
            }

            foreach (var pair in module.Handlers.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            {
                foreach (var handler in pair.Value)
                {
                    foreach (var statement in handler.Statements)
                    {
                        CollectSourceStatementMetadata(statement);
                    }
                }
            }
        }

        private void CollectSourceStatementMetadata(StatementNode statement)
        {
            switch (statement)
            {
                case LetStatementNode let:
                    AddString(let.Identifier);
                    AddTypeIfPresent(let.DeclaredType);
                    CollectSourceExpressionMetadata(let.Expression);
                    break;

                case PublishStatementNode publish:
                    CollectSourcePublishMetadata(publish);
                    break;

                case IfStatementNode ifStatement:
                    CollectSourceExpressionMetadata(ifStatement.Condition);
                    foreach (var nested in ifStatement.ThenBody.Statements)
                    {
                        CollectSourceStatementMetadata(nested);
                    }

                    if (ifStatement.ElseBody is not null)
                    {
                        foreach (var nested in ifStatement.ElseBody.Statements)
                        {
                            CollectSourceStatementMetadata(nested);
                        }
                    }

                    break;

                case ForStatementNode forStatement:
                    AddString(forStatement.Identifier);
                    CollectSourceIterationSourceMetadata(forStatement.Source);
                    foreach (var nested in forStatement.Body.Statements)
                    {
                        CollectSourceStatementMetadata(nested);
                    }

                    break;

                case SeededRandomStatementNode seededRandom:
                    CollectSourceExpressionMetadata(seededRandom.SeedExpression);
                    foreach (var nested in seededRandom.Body.Statements)
                    {
                        CollectSourceStatementMetadata(nested);
                    }

                    break;

                case ExpressionStatementNode expressionStatement:
                    CollectSourceExpressionMetadata(expressionStatement.Expression);
                    break;
            }
        }

        private void CollectSourcePublishMetadata(PublishStatementNode publish)
        {
            foreach (var tagExpression in publish.TagExpressions)
            {
                CollectSourceExpressionMetadata(tagExpression);
            }

            if (publish.MessageExpression is MessageLiteralExpressionNode message)
            {
                AddOutboundMessageSignature(message);
                CollectSourceMessageMetadata(message);
                return;
            }

            CollectSourceExpressionMetadata(publish.MessageExpression);
        }

        private void CollectSourceExpressionMetadata(ExpressionNode? expression)
        {
            switch (expression)
            {
                case null:
                case BooleanLiteralExpressionNode:
                case IntegerLiteralExpressionNode:
                case UnitIntegerLiteralExpressionNode:
                case FloatLiteralExpressionNode:
                case PercentageLiteralExpressionNode:
                case UnitFloatLiteralExpressionNode:
                case IdentifierExpressionNode:
                case DiceExpressionNode:
                    return;

                case TextLiteralExpressionNode text:
                    AddString(text.Value);
                    return;

                case TagLiteralExpressionNode tag:
                    AddString(tag.Name);
                    return;

                case HandlerLiteralExpressionNode handler:
                    AddString(GameEventScriptMessageSignature.NormalizeMessageName(handler.Message));
                    foreach (var label in handler.SignatureLabels)
                    {
                        AddString(label);
                    }

                    return;

                case MessageLiteralExpressionNode message:
                    CollectSourceMessageMetadata(message);
                    return;

                case ExtensionCallExpressionNode extensionCall:
                    AddExternalReference(new GameEventScriptExtensionReference(
                        extensionCall.ExtensionName,
                        extensionCall.FunctionName,
                        extensionCall.Arguments.Select(argument => argument.Name)));
                    foreach (var argument in extensionCall.Arguments)
                    {
                        CollectSourceExpressionMetadata(argument.Expression);
                    }

                    return;

                case ExtensionPredicateExpressionNode extensionPredicate:
                    AddExternalReference(new GameEventScriptExtensionReference(
                        extensionPredicate.ExtensionName,
                        extensionPredicate.FunctionName,
                        [GameEventScriptMessageSignature.UnlabeledParameterName]));
                    CollectSourceExpressionMetadata(extensionPredicate.Value);
                    return;

                case ListLiteralExpressionNode list:
                    foreach (var item in list.Items)
                    {
                        CollectSourceExpressionMetadata(item);
                    }

                    return;

                case MapLiteralExpressionNode dictionary:
                    foreach (var entry in dictionary.Entries)
                    {
                        AddString(entry.Key);
                        CollectSourceExpressionMetadata(entry.Value);
                    }

                    return;

                case UnaryExpressionNode unary:
                    AddString(unary.Operator.ToSourceText());
                    CollectSourceExpressionMetadata(unary.Operand);
                    return;

                case VariadicTaggedExpressionNode variadic:
                    foreach (var argument in variadic.Arguments)
                    {
                        CollectSourceExpressionMetadata(argument);
                    }

                    return;

                case ClampExpressionNode clamp:
                    CollectSourceExpressionMetadata(clamp.Value);
                    CollectSourceExpressionMetadata(clamp.Minimum);
                    CollectSourceExpressionMetadata(clamp.Maximum);
                    return;

                case RandomExpressionNode random:
                    CollectSourceExpressionMetadata(random.FromExpression);
                    CollectSourceExpressionMetadata(random.ToExpression);
                    return;

                case RangeExpressionNode range:
                    CollectSourceRangeMetadata(range);
                    return;

                case SeededRandomExpressionNode seededRandom:
                    CollectSourceExpressionMetadata(seededRandom.SeedExpression);
                    CollectSourceExpressionMetadata(seededRandom.BodyExpression);
                    return;

                case GeneratedCollectionExpressionNode generatedCollection:
                    AddString(generatedCollection.CollectionType);
                    AddString(generatedCollection.Identifier);
                    CollectSourceIterationSourceMetadata(generatedCollection.Source);
                    CollectSourceExpressionMetadata(generatedCollection.Predicate);
                    CollectSourceExpressionMetadata(generatedCollection.Projection);
                    return;

                case GuardedChoiceExpressionNode guardedChoice:
                    foreach (var branch in guardedChoice.Branches)
                    {
                        CollectSourceExpressionMetadata(branch.ConditionExpression);
                        CollectSourceExpressionMetadata(branch.ValueExpression);
                    }

                    CollectSourceExpressionMetadata(guardedChoice.OtherwiseExpression);
                    return;

                case BinaryExpressionNode binary:
                    CollectSourceExpressionMetadata(binary.Left);
                    CollectSourceExpressionMetadata(binary.Right);
                    return;

                case PredicateCallExpressionNode predicateCall:
                    AddString(predicateCall.PredicateName);
                    CollectSourceExpressionMetadata(predicateCall.Value);
                    return;

                case CallExpressionNode call:
                    AddString(call.Name);
                    if (!module.Callables.ContainsKey(call.Name))
                    {
                        foreach (var argumentName in call.ArgumentList.Arguments.Select(argument => argument.Name))
                        {
                            AddString(argumentName);
                        }
                    }

                    foreach (var argument in call.ArgumentList.Arguments)
                    {
                        CollectSourceExpressionMetadata(argument.Expression);
                    }

                    return;

                case TypeCastExpressionNode typeCast:
                    AddString(typeCast.TypeName);
                    CollectSourceExpressionMetadata(typeCast.Value);
                    return;

                case TypeConstructorExpressionNode typeConstructor:
                    CollectSourceTypeConstructorMetadata(typeConstructor);
                    return;

                case TypeCheckExpressionNode typeCheck:
                    AddString(typeCheck.TypeName);
                    CollectSourceExpressionMetadata(typeCheck.Value);
                    return;

                case MemberAccessExpressionNode memberAccess:
                    AddString(memberAccess.Member);
                    CollectSourceExpressionMetadata(memberAccess.Target);
                    return;

                case CollectionAccessExpressionNode collectionAccess:
                    CollectSourceCollectionAccessMetadata(collectionAccess);
                    return;
            }
        }

        private void CollectSourceMessageMetadata(MessageLiteralExpressionNode message)
        {
            var argumentNames = message.Arguments.Select(argument => argument.Name).ToArray();
            AddString(GameEventScriptMessageSignature.NormalizeMessageName(message.Message));
            foreach (var argumentName in argumentNames)
            {
                AddString(argumentName);
            }

            foreach (var argument in message.Arguments)
            {
                CollectSourceExpressionMetadata(argument.Expression);
            }
        }

        private void CollectSourceTypeConstructorMetadata(TypeConstructorExpressionNode typeConstructor)
        {
            AddString(typeConstructor.TypeName);
            var argumentNames = typeConstructor.Arguments.Select(argument => argument.Name).ToArray();
            foreach (var argumentName in argumentNames)
            {
                AddString(argumentName);
            }

            if (module.ExternalTypeDefinitions.ContainsKey(typeConstructor.TypeName))
            {
                AddExternalTypeConstructorReference(new GameEventScriptExternalTypeConstructorReference(
                    typeConstructor.TypeName,
                    argumentNames));
            }

            foreach (var argument in typeConstructor.Arguments)
            {
                CollectSourceExpressionMetadata(argument.Expression);
            }
        }

        private void CollectSourceIterationSourceMetadata(IterationSourceNode source)
        {
            switch (source)
            {
                case CollectionIterationSourceNode collection:
                    CollectSourceExpressionMetadata(collection.Expression);
                    break;

                case RangeIterationSourceNode range:
                    CollectSourceRangeMetadata(range.RangeExpression);
                    break;
            }
        }

        private void CollectSourceRangeMetadata(RangeExpressionNode range)
        {
            CollectSourceExpressionMetadata(range.FromExpression);
            CollectSourceExpressionMetadata(range.ToExpression);
            CollectSourceExpressionMetadata(range.StepExpression);
        }

        private void CollectSourceCollectionAccessMetadata(CollectionAccessExpressionNode expression)
        {
            if (expression.Selector is ExpressionSelectorNode indexSelector)
            {
                CollectSourceExpressionMetadata(expression.Target);
                CollectSourceExpressionMetadata(indexSelector.Expression);
                return;
            }

            var selectors = new List<CollectionSelectorNode>();
            ExpressionNode source = expression;
            while (source is CollectionAccessExpressionNode collectionAccess)
            {
                selectors.Add(collectionAccess.Selector);
                source = collectionAccess.Target;
            }

            CollectSourceExpressionMetadata(source);
            selectors.Reverse();
            foreach (var selector in selectors)
            {
                CollectSourceSelectorMetadata(selector);
            }
        }

        private void CollectSourceSelectorMetadata(CollectionSelectorNode selector)
        {
            switch (selector)
            {
                case FilterSelectorNode filter:
                    AddString(filter.Identifier);
                    CollectSourceExpressionMetadata(filter.Predicate);
                    break;

                case SelectSelectorNode select:
                    AddString(select.Identifier);
                    CollectSourceExpressionMetadata(select.Projection);
                    break;

                case PredicateSelectorNode predicate:
                    AddString(predicate.Identifier);
                    AddString(predicate.Operator);
                    CollectSourceExpressionMetadata(predicate.Predicate);
                    break;

                case SumSelectorNode sum:
                    AddString(sum.Identifier);
                    CollectSourceExpressionMetadata(sum.Projection);
                    break;

                case AverageSelectorNode average:
                    AddString(average.Identifier);
                    CollectSourceExpressionMetadata(average.Projection);
                    break;

                case CountSelectorNode count:
                    AddString(count.Identifier);
                    CollectSourceExpressionMetadata(count.Predicate);
                    break;

                case SeriesTermSelectorNode term:
                    CollectSourceExpressionMetadata(term.IndexExpression);
                    break;

                case EdgeSelectorNode edge:
                    AddStringIfPresent(edge.Identifier);
                    AddString(edge.Mode);
                    CollectSourceExpressionMetadata(edge.Predicate);
                    break;

                case PatternSelectorNode pattern:
                    CollectSourceDicePatternMetadata(pattern.Pattern);
                    break;

                case ObjectMatchSelectorNode objectMatch:
                    CollectSourceObjectMatchPatternMetadata(objectMatch.Pattern);
                    break;

                case TakePatternSelectorNode takePattern:
                    CollectSourceDicePatternMetadata(takePattern.Pattern);
                    break;

                case MinSelectorNode min:
                    AddString(min.Identifier);
                    CollectSourceExpressionMetadata(min.Projection);
                    break;

                case MaxSelectorNode max:
                    AddString(max.Identifier);
                    CollectSourceExpressionMetadata(max.Projection);
                    break;

                case MapSelectorNode dictionary:
                    AddString(dictionary.Identifier);
                    CollectSourceExpressionMetadata(dictionary.KeyProjection);
                    CollectSourceExpressionMetadata(dictionary.ValueProjection);
                    break;

                case ContainsSelectorNode contains:
                    AddString(contains.Mode);
                    CollectSourceExpressionMetadata(contains.ValueExpression);
                    break;

                case ChooseSelectorNode choose:
                    AddStringIfPresent(choose.Identifier);
                    AddStringIfPresent(choose.WeightIdentifier);
                    CollectSourceExpressionMetadata(choose.Predicate);
                    CollectSourceExpressionMetadata(choose.WeightExpression);
                    break;

                case SortSelectorNode sort:
                    AddString(sort.Direction);
                    break;

                case DistinctSelectorNode distinct:
                    AddStringIfPresent(distinct.Identifier);
                    CollectSourceExpressionMetadata(distinct.Projection);
                    break;

                case GroupBySelectorNode groupBy:
                    AddString(groupBy.Identifier);
                    CollectSourceExpressionMetadata(groupBy.Projection);
                    break;

                case OrderBySelectorNode orderBy:
                    AddString(orderBy.Direction);
                    AddString(orderBy.Identifier);
                    CollectSourceExpressionMetadata(orderBy.Projection);
                    break;
            }
        }

        private void CollectSourceDicePatternMetadata(DicePatternNode pattern)
        {
            if (pattern is DiceCountPatternNode { Face: { } face })
            {
                CollectSourceExpressionMetadata(face);
            }
        }

        private void CollectSourceObjectMatchPatternMetadata(ObjectMatchPatternNode pattern)
        {
            foreach (var entry in pattern.Entries)
            {
                AddString(entry.Key);
                switch (entry.Value)
                {
                    case ObjectMatchExpressionValueNode expression:
                        CollectSourceExpressionMetadata(expression.Expression);
                        break;

                    case ObjectMatchNestedValueNode nested:
                        CollectSourceObjectMatchPatternMetadata(nested.Pattern);
                        break;
                }
            }
        }

        private void AddStringIfPresent(string? value)
        {
            if (!string.IsNullOrEmpty(value))
            {
                AddString(value);
            }
        }

        private void AddTypeIfPresent(string? value)
        {
            if (!string.IsNullOrEmpty(value))
            {
                AddString(value);
            }
        }

    }
}
