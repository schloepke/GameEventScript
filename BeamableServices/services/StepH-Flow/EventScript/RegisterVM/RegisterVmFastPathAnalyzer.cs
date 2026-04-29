#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections.Generic;
using System.Linq;
using StepH.Flow.EventScript;
using StepH.Flow.EventScript.Linker;
using StepH.Flow.EventScript.Parser;
using StepH.Flow.EventScript.Types;

namespace StepH.Flow.EventScript.RegisterVM;

internal static class RegisterVmFastPathAnalyzer
{
    public static RegisterVmFastPathPlan CreateHandlerPlan(
        IReadOnlyList<string> parameters,
        IReadOnlyList<StatementNode> statements,
        IReadOnlyDictionary<string, LinkedCallableDefinition> callables)
    {
        if (!SupportsHandler(statements, callables, out var unsupportedReason))
        {
            return RegisterVmFastPathPlan.Unsupported(unsupportedReason);
        }

        var slotCollector = new SlotCollector(callables);
        foreach (var parameter in parameters)
        {
            slotCollector.AddSlot(parameter);
        }

        foreach (var statement in statements)
        {
            slotCollector.CollectStatement(statement);
        }

        var programCompiler = new ProgramCompiler(slotCollector.Slots, callables);
        foreach (var statement in statements)
        {
            programCompiler.CompileStatementPrograms(statement);
        }

        return RegisterVmFastPathPlan.Create(
            slotCollector.Slots,
            programCompiler.ExpressionPrograms,
            programCompiler.PublishLayouts,
            programCompiler.MaxStackDepth);
    }

    public static bool SupportsHandler(
        IReadOnlyList<StatementNode> statements,
        IReadOnlyDictionary<string, LinkedCallableDefinition> callables)
        => SupportsHandler(statements, callables, out _);

    private static bool SupportsHandler(
        IReadOnlyList<StatementNode> statements,
        IReadOnlyDictionary<string, LinkedCallableDefinition> callables,
        out string unsupportedReason)
    {
        for (var statementIndex = 0; statementIndex < statements.Count; statementIndex++)
        {
            if (!SupportsStatement(statements[statementIndex], callables, out unsupportedReason))
            {
                unsupportedReason = $"Statement {statementIndex}: {unsupportedReason}";
                return false;
            }
        }

        unsupportedReason = string.Empty;
        return true;
    }

    private static bool SupportsStatement(
        StatementNode statement,
        IReadOnlyDictionary<string, LinkedCallableDefinition> callables,
        out string unsupportedReason)
    {
        switch (statement)
        {
            case LetStatementNode let:
                if (!string.IsNullOrEmpty(let.DeclaredType) &&
                    !SupportsDeclaredType(let.DeclaredType!))
                {
                    unsupportedReason = $"Typed let '{let.Identifier}' with type '{let.DeclaredType}' is not supported by the RegisterVM fast path yet.";
                    return false;
                }

                if (!SupportsExpression(let.Expression, callables, out unsupportedReason))
                {
                    unsupportedReason = $"Let '{let.Identifier}' expression: {unsupportedReason}";
                    return false;
                }

                unsupportedReason = string.Empty;
                return true;

            case PublishStatementNode { MessageExpression: MessageLiteralExpressionNode message }:
                for (var argumentIndex = 0; argumentIndex < message.Arguments.Count; argumentIndex++)
                {
                    var argument = message.Arguments[argumentIndex];
                    if (!SupportsExpression(argument.Expression, callables, out unsupportedReason))
                    {
                        unsupportedReason = $"Publish argument '{argument.Name}': {unsupportedReason}";
                        return false;
                    }
                }

                unsupportedReason = string.Empty;
                return true;

            case PublishStatementNode publish:
                unsupportedReason = $"Publish expression '{publish.MessageExpression.GetType().Name}' is not supported by the RegisterVM fast path.";
                return false;

            case IfStatementNode ifStatement:
                if (!SupportsExpression(ifStatement.Condition, callables, out unsupportedReason))
                {
                    unsupportedReason = $"If condition: {unsupportedReason}";
                    return false;
                }

                if (!SupportsHandler(ifStatement.ThenBody.Statements, callables, out unsupportedReason))
                {
                    unsupportedReason = $"If then body: {unsupportedReason}";
                    return false;
                }

                if (ifStatement.ElseBody is not null &&
                    !SupportsHandler(ifStatement.ElseBody.Statements, callables, out unsupportedReason))
                {
                    unsupportedReason = $"If else body: {unsupportedReason}";
                    return false;
                }

                unsupportedReason = string.Empty;
                return true;

            case ForStatementNode { Source: RangeIterationSourceNode range } forStatement:
                if (!SupportsRange(range.RangeExpression, callables, out unsupportedReason))
                {
                    unsupportedReason = $"For range: {unsupportedReason}";
                    return false;
                }

                if (!SupportsHandler(forStatement.Body.Statements, callables, out unsupportedReason))
                {
                    unsupportedReason = $"For body: {unsupportedReason}";
                    return false;
                }

                unsupportedReason = string.Empty;
                return true;

            case ForStatementNode { Source: CollectionIterationSourceNode collection } forStatement:
                if (!SupportsExpression(collection.Expression, callables, out unsupportedReason))
                {
                    unsupportedReason = $"For collection source: {unsupportedReason}";
                    return false;
                }

                if (!SupportsHandler(forStatement.Body.Statements, callables, out unsupportedReason))
                {
                    unsupportedReason = $"For body: {unsupportedReason}";
                    return false;
                }

                unsupportedReason = string.Empty;
                return true;

            case ForStatementNode forStatement:
                unsupportedReason = $"For source '{forStatement.Source.GetType().Name}' is not supported by the RegisterVM fast path.";
                return false;

            case ExpressionStatementNode expressionStatement:
                if (!SupportsExpression(expressionStatement.Expression, callables, out unsupportedReason))
                {
                    unsupportedReason = $"Expression statement: {unsupportedReason}";
                    return false;
                }

                unsupportedReason = string.Empty;
                return true;

            default:
                unsupportedReason = $"Statement '{statement.GetType().Name}' is not supported by the RegisterVM fast path.";
                return false;
        }
    }

    private static bool SupportsRange(
        RangeExpressionNode range,
        IReadOnlyDictionary<string, LinkedCallableDefinition> callables,
        out string unsupportedReason)
    {
        if (!SupportsExpression(range.FromExpression, callables, out unsupportedReason))
        {
            unsupportedReason = $"Range start: {unsupportedReason}";
            return false;
        }

        if (!SupportsExpression(range.ToExpression, callables, out unsupportedReason))
        {
            unsupportedReason = $"Range end: {unsupportedReason}";
            return false;
        }

        if (range.StepExpression is not null &&
            !SupportsExpression(range.StepExpression, callables, out unsupportedReason))
        {
            unsupportedReason = $"Range step: {unsupportedReason}";
            return false;
        }

        unsupportedReason = string.Empty;
        return true;
    }

    private static bool SupportsExpression(
        ExpressionNode expression,
        IReadOnlyDictionary<string, LinkedCallableDefinition> callables,
        out string unsupportedReason)
    {
        switch (expression)
        {
            case BooleanLiteralExpressionNode:
            case IntegerLiteralExpressionNode:
            case DecimalLiteralExpressionNode:
            case PercentageLiteralExpressionNode:
            case UnitDecimalLiteralExpressionNode:
            case TextLiteralExpressionNode:
            case TagLiteralExpressionNode:
            case IdentifierExpressionNode:
                unsupportedReason = string.Empty;
                return true;

            case MessageLiteralExpressionNode message:
                for (var argumentIndex = 0; argumentIndex < message.Arguments.Count; argumentIndex++)
                {
                    var argument = message.Arguments[argumentIndex];
                    if (!SupportsExpression(argument.Expression, callables, out unsupportedReason))
                    {
                        unsupportedReason = $"Message argument '{argument.Name}': {unsupportedReason}";
                        return false;
                    }
                }

                unsupportedReason = string.Empty;
                return true;

            case BinaryExpressionNode binary:
                if (!SupportsBinaryOperator(binary.Operator))
                {
                    unsupportedReason = $"Binary operator '{binary.Operator}' is not supported by the RegisterVM fast path.";
                    return false;
                }

                if (!SupportsExpression(binary.Left, callables, out unsupportedReason))
                {
                    unsupportedReason = $"Binary left operand: {unsupportedReason}";
                    return false;
                }

                if (!SupportsExpression(binary.Right, callables, out unsupportedReason))
                {
                    unsupportedReason = $"Binary right operand: {unsupportedReason}";
                    return false;
                }

                unsupportedReason = string.Empty;
                return true;

            case RulePredicateExpressionNode rulePredicate:
                if (!SupportsExpression(rulePredicate.Value, callables, out unsupportedReason))
                {
                    unsupportedReason = $"Rule predicate value: {unsupportedReason}";
                    return false;
                }

                if (!callables.TryGetValue(rulePredicate.RuleName, out var callable))
                {
                    unsupportedReason = $"Rule '{rulePredicate.RuleName}' was not found.";
                    return false;
                }

                if (callable.Kind != LinkedCallableKind.Rule)
                {
                    unsupportedReason = $"Callable '{rulePredicate.RuleName}' is a {callable.Kind}, not a rule.";
                    return false;
                }

                if (callable.Parameters.Count != 1)
                {
                    unsupportedReason = $"Rule '{rulePredicate.RuleName}' has {callable.Parameters.Count} parameters; only unary rule predicates are supported.";
                    return false;
                }

                if (!SupportsExpression(callable.Expression, callables, out unsupportedReason))
                {
                    unsupportedReason = $"Rule '{rulePredicate.RuleName}' expression: {unsupportedReason}";
                    return false;
                }

                unsupportedReason = string.Empty;
                return true;

            case TypeCastExpressionNode typeCast:
                if (!SupportsExpression(typeCast.Value, callables, out unsupportedReason))
                {
                    unsupportedReason = $"Type cast value: {unsupportedReason}";
                    return false;
                }

                if (!SupportsTypeCast(typeCast.TypeName))
                {
                    unsupportedReason = $"Type cast '{typeCast.TypeName}' is not supported by the RegisterVM fast path.";
                    return false;
                }

                unsupportedReason = string.Empty;
                return true;

            case MemberAccessExpressionNode memberAccess:
                if (!SupportsExpression(memberAccess.Target, callables, out unsupportedReason))
                {
                    unsupportedReason = $"Member access target: {unsupportedReason}";
                    return false;
                }

                unsupportedReason = string.Empty;
                return true;

            case CollectionAccessExpressionNode collectionAccess:
                if (SupportsPipelinedCollection(collectionAccess, callables, out unsupportedReason) ||
                    SupportsIndexedCollectionAccess(collectionAccess, callables, out unsupportedReason))
                {
                    unsupportedReason = string.Empty;
                    return true;
                }

                return false;

            default:
                unsupportedReason = $"Expression '{expression.GetType().Name}' is not supported by the RegisterVM fast path.";
                return false;
        }
    }

    private static bool SupportsBinaryOperator(string operation)
        => operation is "+" or "*" or "mod" or "=" or "==" or "<>" or "<" or ">" or "<=" or ">=" or "&" or "|" or "^";

    private static bool SupportsTypeCast(string typeName)
        => typeName is "boolean" or "integer" or "decimal" or "number" or "percentage" or "degree" or "meter" or "second";

    private static bool SupportsDeclaredType(string typeName)
        => typeName is "nothing" or "tag" or "text" or
            "percentage" or "degree" or "meter" or "second" or
            "vector2" or "vector3" or
            "boolean" or "integer" or "decimal" or "number" or
            "list" or "range" or "message" or "handler" or
            "dictionary" or "set" or "dice" or "optional";

    private static bool SupportsPipelinedCollection(
        CollectionAccessExpressionNode expression,
        IReadOnlyDictionary<string, LinkedCallableDefinition> callables,
        out string unsupportedReason)
    {
        var selectors = new List<CollectionSelectorNode>();
        ExpressionNode source = expression;
        while (source is CollectionAccessExpressionNode collectionAccess)
        {
            selectors.Add(collectionAccess.Selector);
            source = collectionAccess.Target;
        }

        selectors.Reverse();
        if (selectors.Count == 0)
        {
            unsupportedReason = "Collection access did not contain a selector.";
            return false;
        }

        if (!SupportsExpression(source, callables, out unsupportedReason))
        {
            unsupportedReason = $"Collection source: {unsupportedReason}";
            return false;
        }

        for (var i = 0; i < selectors.Count; i++)
        {
            var isTerminal = i == selectors.Count - 1;
            if (!SupportsSelector(selectors[i], isTerminal, callables, out unsupportedReason))
            {
                unsupportedReason = $"Collection selector {i}: {unsupportedReason}";
                return false;
            }
        }

        unsupportedReason = string.Empty;
        return true;
    }

    private static bool SupportsIndexedCollectionAccess(
        CollectionAccessExpressionNode expression,
        IReadOnlyDictionary<string, LinkedCallableDefinition> callables,
        out string unsupportedReason)
    {
        if (expression.Selector is not ExpressionSelectorNode selector)
        {
            unsupportedReason = $"Collection selector '{expression.Selector.GetType().Name}' is not a direct index/key expression.";
            return false;
        }

        if (!SupportsExpression(expression.Target, callables, out unsupportedReason))
        {
            unsupportedReason = $"Collection access target: {unsupportedReason}";
            return false;
        }

        if (!SupportsExpression(selector.Expression, callables, out unsupportedReason))
        {
            unsupportedReason = $"Collection access selector: {unsupportedReason}";
            return false;
        }

        unsupportedReason = string.Empty;
        return true;
    }

    private static bool SupportsSelector(
        CollectionSelectorNode selector,
        bool isTerminal,
        IReadOnlyDictionary<string, LinkedCallableDefinition> callables,
        out string unsupportedReason)
    {
        switch (selector)
        {
            case FilterSelectorNode filter when !isTerminal:
                if (!SupportsExpression(filter.Predicate, callables, out unsupportedReason))
                {
                    unsupportedReason = $"Filter predicate: {unsupportedReason}";
                    return false;
                }

                unsupportedReason = string.Empty;
                return true;

            case SelectSelectorNode select when !isTerminal:
                if (!SupportsExpression(select.Projection, callables, out unsupportedReason))
                {
                    unsupportedReason = $"Select projection: {unsupportedReason}";
                    return false;
                }

                unsupportedReason = string.Empty;
                return true;

            case SumSelectorNode sum when isTerminal:
                if (!SupportsExpression(sum.Projection, callables, out unsupportedReason))
                {
                    unsupportedReason = $"Sum projection: {unsupportedReason}";
                    return false;
                }

                unsupportedReason = string.Empty;
                return true;

            case AverageSelectorNode average when isTerminal:
                if (!SupportsExpression(average.Projection, callables, out unsupportedReason))
                {
                    unsupportedReason = $"Average projection: {unsupportedReason}";
                    return false;
                }

                unsupportedReason = string.Empty;
                return true;

            case CountSelectorNode count when isTerminal:
                if (!SupportsExpression(count.Predicate, callables, out unsupportedReason))
                {
                    unsupportedReason = $"Count predicate: {unsupportedReason}";
                    return false;
                }

                unsupportedReason = string.Empty;
                return true;

            case EdgeSelectorNode edge when isTerminal:
                if (edge.Predicate is not null &&
                    !SupportsExpression(edge.Predicate, callables, out unsupportedReason))
                {
                    unsupportedReason = $"Edge predicate: {unsupportedReason}";
                    return false;
                }

                unsupportedReason = string.Empty;
                return true;

            default:
                unsupportedReason = $"Selector '{selector.GetType().Name}' is not supported as a {(isTerminal ? "terminal" : "prefix")} RegisterVM fast-path selector.";
                return false;
        }
    }

    private sealed class SlotCollector(IReadOnlyDictionary<string, LinkedCallableDefinition> callables)
    {
        private readonly Dictionary<string, int> _slots = new(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> Slots => _slots;

        public void AddSlot(string name)
        {
            if (!_slots.ContainsKey(name))
            {
                _slots[name] = _slots.Count;
            }
        }

        public void CollectStatement(StatementNode statement)
        {
            switch (statement)
            {
                case LetStatementNode let:
                    CollectExpression(let.Expression);
                    AddSlot(let.Identifier);
                    break;

                case IfStatementNode ifStatement:
                    CollectExpression(ifStatement.Condition);
                    foreach (var nested in ifStatement.ThenBody.Statements)
                    {
                        CollectStatement(nested);
                    }

                    if (ifStatement.ElseBody is not null)
                    {
                        foreach (var nested in ifStatement.ElseBody.Statements)
                        {
                            CollectStatement(nested);
                        }
                    }

                    break;

                case PublishStatementNode { MessageExpression: MessageLiteralExpressionNode message }:
                    foreach (var argument in message.Arguments)
                    {
                        CollectExpression(argument.Expression);
                    }

                    break;

                case ForStatementNode { Source: RangeIterationSourceNode range } forStatement:
                    CollectRange(range.RangeExpression);
                    AddSlot(forStatement.Identifier);
                    foreach (var nested in forStatement.Body.Statements)
                    {
                        CollectStatement(nested);
                    }

                    break;

                case ForStatementNode { Source: CollectionIterationSourceNode collection } forStatement:
                    CollectExpression(collection.Expression);
                    AddSlot(forStatement.Identifier);
                    foreach (var nested in forStatement.Body.Statements)
                    {
                        CollectStatement(nested);
                    }

                    break;

                case ExpressionStatementNode expressionStatement:
                    CollectExpression(expressionStatement.Expression);
                    break;
            }
        }

        private void CollectRange(RangeExpressionNode range)
        {
            CollectExpression(range.FromExpression);
            CollectExpression(range.ToExpression);
            if (range.StepExpression is not null)
            {
                CollectExpression(range.StepExpression);
            }
        }

        private void CollectExpression(ExpressionNode expression)
        {
            switch (expression)
            {
                case IdentifierExpressionNode identifier:
                    AddSlot(identifier.Name);
                    break;

                case MessageLiteralExpressionNode message:
                    foreach (var argument in message.Arguments)
                    {
                        CollectExpression(argument.Expression);
                    }

                    break;

                case BinaryExpressionNode binary:
                    CollectExpression(binary.Left);
                    CollectExpression(binary.Right);
                    break;

                case RulePredicateExpressionNode rulePredicate:
                    CollectExpression(rulePredicate.Value);
                    if (callables.TryGetValue(rulePredicate.RuleName, out var callable))
                    {
                        foreach (var parameter in callable.Parameters)
                        {
                            AddSlot(parameter);
                        }

                        CollectExpression(callable.Expression);
                    }

                    break;

                case TypeCastExpressionNode typeCast:
                    CollectExpression(typeCast.Value);
                    break;

                case MemberAccessExpressionNode memberAccess:
                    CollectExpression(memberAccess.Target);
                    break;

                case CollectionAccessExpressionNode collectionAccess:
                    CollectCollectionAccess(collectionAccess);
                    break;
            }
        }

        private void CollectCollectionAccess(CollectionAccessExpressionNode expression)
        {
            if (expression.Selector is ExpressionSelectorNode selector)
            {
                CollectExpression(expression.Target);
                CollectExpression(selector.Expression);
                return;
            }

            var selectors = new List<CollectionSelectorNode>();
            ExpressionNode source = expression;
            while (source is CollectionAccessExpressionNode collectionAccess)
            {
                selectors.Add(collectionAccess.Selector);
                source = collectionAccess.Target;
            }

            CollectExpression(source);
            selectors.Reverse();
            foreach (var pipelineSelector in selectors)
            {
                CollectSelector(pipelineSelector);
            }
        }

        private void CollectSelector(CollectionSelectorNode selector)
        {
            switch (selector)
            {
                case FilterSelectorNode filter:
                    AddSlot(filter.Identifier);
                    CollectExpression(filter.Predicate);
                    break;

                case SelectSelectorNode select:
                    AddSlot(select.Identifier);
                    CollectExpression(select.Projection);
                    break;

                case SumSelectorNode sum:
                    AddSlot(sum.Identifier);
                    CollectExpression(sum.Projection);
                    break;

                case AverageSelectorNode average:
                    AddSlot(average.Identifier);
                    CollectExpression(average.Projection);
                    break;

                case CountSelectorNode count:
                    AddSlot(count.Identifier);
                    CollectExpression(count.Predicate);
                    break;

                case EdgeSelectorNode edge:
                    if (!string.IsNullOrEmpty(edge.Identifier))
                    {
                        AddSlot(edge.Identifier!);
                    }

                    if (edge.Predicate is not null)
                    {
                        CollectExpression(edge.Predicate);
                    }

                    break;
            }
        }
    }

    private sealed class ProgramCompiler(
        IReadOnlyDictionary<string, int> slots,
        IReadOnlyDictionary<string, LinkedCallableDefinition> callables)
    {
        private readonly IReadOnlyDictionary<string, LinkedCallableDefinition> _callables = callables;
        private readonly Dictionary<ExpressionNode, RegisterFastExpressionProgram> _expressionPrograms = new(ReferenceEqualityComparer<ExpressionNode>.Instance);
        private readonly Dictionary<PublishStatementNode, RegisterFastPublishLayout> _publishLayouts = new(ReferenceEqualityComparer<PublishStatementNode>.Instance);

        public IReadOnlyDictionary<ExpressionNode, RegisterFastExpressionProgram> ExpressionPrograms => _expressionPrograms;

        public IReadOnlyDictionary<PublishStatementNode, RegisterFastPublishLayout> PublishLayouts => _publishLayouts;

        public int MaxStackDepth { get; private set; } = 1;

        public void CompileStatementPrograms(StatementNode statement)
        {
            switch (statement)
            {
                case LetStatementNode let:
                    CompileExpression(let.Expression);
                    break;

                case PublishStatementNode publish:
                    CompilePublishLayout(publish);
                    break;

                case IfStatementNode ifStatement:
                    CompileExpression(ifStatement.Condition);
                    foreach (var nested in ifStatement.ThenBody.Statements)
                    {
                        CompileStatementPrograms(nested);
                    }

                    if (ifStatement.ElseBody is not null)
                    {
                        foreach (var nested in ifStatement.ElseBody.Statements)
                        {
                            CompileStatementPrograms(nested);
                        }
                    }

                    break;

                case ForStatementNode { Source: RangeIterationSourceNode range } forStatement:
                    CompileExpression(range.RangeExpression.FromExpression);
                    CompileExpression(range.RangeExpression.ToExpression);
                    if (range.RangeExpression.StepExpression is not null)
                    {
                        CompileExpression(range.RangeExpression.StepExpression);
                    }

                    foreach (var nested in forStatement.Body.Statements)
                    {
                        CompileStatementPrograms(nested);
                    }

                    break;

                case ForStatementNode { Source: CollectionIterationSourceNode collection } forStatement:
                    CompileExpression(collection.Expression);

                    foreach (var nested in forStatement.Body.Statements)
                    {
                        CompileStatementPrograms(nested);
                    }

                    break;

                case ExpressionStatementNode expressionStatement:
                    CompileExpression(expressionStatement.Expression);
                    break;
            }
        }

        private RegisterFastExpressionProgram CompileExpression(ExpressionNode expression)
        {
            if (_expressionPrograms.TryGetValue(expression, out var program))
            {
                return program;
            }

            var instructions = new List<RegisterFastInstruction>();
            var builder = new ExpressionBuilder(this, instructions);
            builder.EmitExpression(expression);
            program = new RegisterFastExpressionProgram(instructions.ToArray(), Math.Max(1, builder.MaxStackDepth));
            _expressionPrograms[expression] = program;
            MaxStackDepth = Math.Max(MaxStackDepth, program.MaxStackDepth + 8);
            return program;
        }

        private RegisterFastPublishLayout CompilePublishLayout(PublishStatementNode publish)
        {
            if (_publishLayouts.TryGetValue(publish, out var layout))
            {
                return layout;
            }

            if (publish.MessageExpression is not MessageLiteralExpressionNode message)
            {
                throw new InvalidOperationException("Unsupported RegisterVM publish expression.");
            }

            var argumentNames = new string[message.Arguments.Count];
            var argumentPrograms = new RegisterFastExpressionProgram[message.Arguments.Count];
            for (var argumentIndex = 0; argumentIndex < message.Arguments.Count; argumentIndex++)
            {
                var argument = message.Arguments[argumentIndex];
                argumentNames[argumentIndex] = argument.Name;
                argumentPrograms[argumentIndex] = CompileExpression(argument.Expression);
            }

            layout = new RegisterFastPublishLayout(
                EventScriptMessageSignature.NormalizeMessageName(message.Message),
                EventScriptMessageSignature.CreateSignatureId(message.Message, argumentNames),
                argumentNames,
                argumentPrograms);
            _publishLayouts[publish] = layout;
            return layout;
        }

        private bool TryGetSlot(string name, out int slot)
            => slots.TryGetValue(name, out slot);

        private bool TryGetCastKind(string typeName, out RegisterFastCastKind kind)
        {
            kind = typeName switch
            {
                "boolean" => RegisterFastCastKind.Boolean,
                "integer" => RegisterFastCastKind.Integer,
                "decimal" => RegisterFastCastKind.Decimal,
                "number" => RegisterFastCastKind.Number,
                "percentage" => RegisterFastCastKind.Percentage,
                "degree" => RegisterFastCastKind.Degree,
                "meter" => RegisterFastCastKind.Meter,
                "second" => RegisterFastCastKind.Second,
                _ => default
            };

            return SupportsTypeCast(typeName);
        }

        private sealed class ExpressionBuilder(ProgramCompiler compiler, List<RegisterFastInstruction> instructions)
        {
            private int _stackDepth;

            public int MaxStackDepth { get; private set; }

            public void EmitExpression(ExpressionNode expression)
            {
                switch (expression)
                {
                    case BooleanLiteralExpressionNode boolean:
                        EmitLoadConstant(RegisterFastValue.Boolean(boolean.Value));
                        return;

                    case IntegerLiteralExpressionNode integer:
                        EmitLoadConstant(RegisterFastValue.Integer(integer.Value));
                        return;

                    case DecimalLiteralExpressionNode decimalLiteral:
                        EmitLoadConstant(RegisterFastValue.Decimal(decimalLiteral.Value));
                        return;

                    case PercentageLiteralExpressionNode percentage:
                        EmitLoadConstant(RegisterFastValue.Percentage(percentage.PercentValue / 100m));
                        return;

                    case UnitDecimalLiteralExpressionNode unitDecimal:
                        EmitLoadConstant(EventScriptDecimalUnits.TryParseTypeName(unitDecimal.UnitName, out var unit)
                            ? RegisterFastValue.Decimal(unitDecimal.Value, unit)
                            : RegisterFastValue.NaN());
                        return;

                    case TextLiteralExpressionNode text:
                        EmitLoadConstant(RegisterFastValue.Reference(EventScriptValueFactory.Text(text.Value)));
                        return;

                    case TagLiteralExpressionNode tag:
                        EmitLoadConstant(RegisterFastValue.Reference(EventScriptValueFactory.Tag(tag.Name)));
                        return;

                    case IdentifierExpressionNode identifier:
                        if (!compiler.TryGetSlot(identifier.Name, out var slot))
                        {
                            throw new InvalidOperationException($"Missing RegisterVM local slot '{identifier.Name}'.");
                        }

                        instructions.Add(new RegisterFastInstruction(RegisterFastOpCode.LoadSlot, slot));
                        Push();
                        return;

                    case BinaryExpressionNode binary:
                        EmitExpression(binary.Left);
                        EmitExpression(binary.Right);
                        instructions.Add(new RegisterFastInstruction(ToBinaryOpCode(binary.Operator)));
                        Pop();
                        return;

                    case RulePredicateExpressionNode rulePredicate:
                        if (!compiler._callables.TryGetValue(rulePredicate.RuleName, out var callable) ||
                            callable.Parameters.Count != 1)
                        {
                            throw new InvalidOperationException($"Unsupported RegisterVM rule predicate '{rulePredicate.RuleName}'.");
                        }

                        if (!compiler.TryGetSlot(callable.Parameters[0], out var parameterSlot))
                        {
                            throw new InvalidOperationException($"Unsupported RegisterVM rule predicate '{rulePredicate.RuleName}'.");
                        }

                        EmitExpression(rulePredicate.Value);
                        instructions.Add(new RegisterFastInstruction(
                            RegisterFastOpCode.RulePredicate,
                            parameterSlot,
                            ExpressionProgram: compiler.CompileExpression(callable.Expression),
                            DiagnosticName: callable.Name,
                            DiagnosticArgumentName: callable.Parameters[0]));
                        return;

                    case TypeCastExpressionNode typeCast:
                        if (!compiler.TryGetCastKind(typeCast.TypeName, out var castKind))
                        {
                            throw new InvalidOperationException($"Unsupported RegisterVM type cast '{typeCast.TypeName}'.");
                        }

                        EmitExpression(typeCast.Value);
                        instructions.Add(new RegisterFastInstruction(RegisterFastOpCode.Cast, CastKind: castKind));
                        return;

                    case MemberAccessExpressionNode memberAccess:
                        EmitExpression(memberAccess.Target);
                        instructions.Add(new RegisterFastInstruction(
                            RegisterFastOpCode.MemberAccess,
                            DiagnosticName: memberAccess.Member));
                        return;

                    case CollectionAccessExpressionNode collectionAccess:
                        if (collectionAccess.Selector is ExpressionSelectorNode selector)
                        {
                            EmitExpression(collectionAccess.Target);
                            EmitExpression(selector.Expression);
                            instructions.Add(new RegisterFastInstruction(RegisterFastOpCode.IndexedAccess));
                            Pop();
                            return;
                        }

                        instructions.Add(new RegisterFastInstruction(
                            RegisterFastOpCode.Pipeline,
                            PipelineProgram: CompilePipeline(collectionAccess)));
                        Push();
                        return;

                    default:
                        throw new InvalidOperationException($"Unsupported RegisterVM expression program node '{expression.GetType().Name}'.");
                }
            }

            private RegisterFastPipelineProgram CompilePipeline(CollectionAccessExpressionNode expression)
            {
                var selectors = new List<CollectionSelectorNode>();
                ExpressionNode source = expression;
                while (source is CollectionAccessExpressionNode collectionAccess)
                {
                    selectors.Add(collectionAccess.Selector);
                    source = collectionAccess.Target;
                }

                selectors.Reverse();
                var prefixSelectors = new List<RegisterFastSelectorProgram>(Math.Max(0, selectors.Count - 1));
                for (var i = 0; i < selectors.Count - 1; i++)
                {
                    prefixSelectors.Add(CompileSelector(selectors[i], isTerminal: false));
                }

                return new RegisterFastPipelineProgram(
                    compiler.CompileExpression(source),
                    prefixSelectors.ToArray(),
                    CompileSelector(selectors[^1], isTerminal: true));
            }

            private RegisterFastSelectorProgram CompileSelector(CollectionSelectorNode selector, bool isTerminal)
            {
                switch (selector)
                {
                    case FilterSelectorNode filter when !isTerminal:
                        return new RegisterFastSelectorProgram(
                            RegisterFastSelectorKind.Filter,
                            RequireSlot(filter.Identifier),
                            compiler.CompileExpression(filter.Predicate));

                    case SelectSelectorNode select when !isTerminal:
                        return new RegisterFastSelectorProgram(
                            RegisterFastSelectorKind.Select,
                            RequireSlot(select.Identifier),
                            compiler.CompileExpression(select.Projection));

                    case SumSelectorNode sum when isTerminal:
                        return new RegisterFastSelectorProgram(
                            RegisterFastSelectorKind.Sum,
                            RequireSlot(sum.Identifier),
                            compiler.CompileExpression(sum.Projection));

                    case AverageSelectorNode average when isTerminal:
                        return new RegisterFastSelectorProgram(
                            RegisterFastSelectorKind.Average,
                            RequireSlot(average.Identifier),
                            compiler.CompileExpression(average.Projection));

                    case CountSelectorNode count when isTerminal:
                        return new RegisterFastSelectorProgram(
                            RegisterFastSelectorKind.Count,
                            RequireSlot(count.Identifier),
                            compiler.CompileExpression(count.Predicate));

                    case EdgeSelectorNode edge when isTerminal:
                        return new RegisterFastSelectorProgram(
                            RegisterFastSelectorKind.Edge,
                            string.IsNullOrEmpty(edge.Identifier) ? -1 : RequireSlot(edge.Identifier!),
                            edge.Predicate is null ? null : compiler.CompileExpression(edge.Predicate),
                            edge.Mode);

                    default:
                        throw new InvalidOperationException($"Unsupported RegisterVM selector program node '{selector.GetType().Name}'.");
                }
            }

            private int RequireSlot(string name)
            {
                if (!compiler.TryGetSlot(name, out var slot))
                {
                    throw new InvalidOperationException($"Missing RegisterVM local slot '{name}'.");
                }

                return slot;
            }

            private void EmitLoadConstant(RegisterFastValue value)
            {
                instructions.Add(new RegisterFastInstruction(RegisterFastOpCode.LoadConstant, Constant: value));
                Push();
            }

            private void Push()
            {
                _stackDepth++;
                MaxStackDepth = Math.Max(MaxStackDepth, _stackDepth);
            }

            private void Pop() => _stackDepth = Math.Max(0, _stackDepth - 1);

            private static RegisterFastOpCode ToBinaryOpCode(string operation)
                => operation switch
                {
                    "|" => RegisterFastOpCode.Or,
                    "^" => RegisterFastOpCode.Xor,
                    "&" => RegisterFastOpCode.And,
                    "=" or "==" => RegisterFastOpCode.Equal,
                    "<>" => RegisterFastOpCode.NotEqual,
                    "<" => RegisterFastOpCode.Less,
                    ">" => RegisterFastOpCode.Greater,
                    "<=" => RegisterFastOpCode.LessOrEqual,
                    ">=" => RegisterFastOpCode.GreaterOrEqual,
                    "+" => RegisterFastOpCode.Add,
                    "*" => RegisterFastOpCode.Multiply,
                    "mod" => RegisterFastOpCode.Modulo,
                    _ => throw new InvalidOperationException($"Unsupported RegisterVM binary operator '{operation}'.")
                };
        }
    }
}

internal sealed class RegisterVmFastPathPlan
{
    private readonly IReadOnlyDictionary<string, int> _slots;
    private readonly IReadOnlyDictionary<ExpressionNode, RegisterFastExpressionProgram> _expressionPrograms;
    private readonly IReadOnlyDictionary<PublishStatementNode, RegisterFastPublishLayout> _publishLayouts;

    private RegisterVmFastPathPlan(
        bool isSupported,
        IReadOnlyDictionary<string, int> slots,
        IReadOnlyDictionary<ExpressionNode, RegisterFastExpressionProgram> expressionPrograms,
        IReadOnlyDictionary<PublishStatementNode, RegisterFastPublishLayout> publishLayouts,
        int maxStackDepth,
        string? unsupportedReason)
    {
        IsSupported = isSupported;
        _slots = slots;
        _expressionPrograms = expressionPrograms;
        _publishLayouts = publishLayouts;
        SlotCount = slots.Count;
        MaxStackDepth = maxStackDepth;
        UnsupportedReason = unsupportedReason;
    }

    public bool IsSupported { get; }

    public string? UnsupportedReason { get; }

    public int SlotCount { get; }

    public int MaxStackDepth { get; }

    public static RegisterVmFastPathPlan Unsupported(string unsupportedReason)
        => new(
            false,
            new Dictionary<string, int>(StringComparer.Ordinal),
            new Dictionary<ExpressionNode, RegisterFastExpressionProgram>(ReferenceEqualityComparer<ExpressionNode>.Instance),
            new Dictionary<PublishStatementNode, RegisterFastPublishLayout>(ReferenceEqualityComparer<PublishStatementNode>.Instance),
            1,
            string.IsNullOrWhiteSpace(unsupportedReason)
                ? "Handler is not supported by the RegisterVM fast path."
                : unsupportedReason);

    public static RegisterVmFastPathPlan Create(
        IReadOnlyDictionary<string, int> slots,
        IReadOnlyDictionary<ExpressionNode, RegisterFastExpressionProgram> expressionPrograms,
        IReadOnlyDictionary<PublishStatementNode, RegisterFastPublishLayout> publishLayouts,
        int maxStackDepth)
        => new(
            true,
            new Dictionary<string, int>(slots, StringComparer.Ordinal),
            new Dictionary<ExpressionNode, RegisterFastExpressionProgram>(expressionPrograms, ReferenceEqualityComparer<ExpressionNode>.Instance),
            new Dictionary<PublishStatementNode, RegisterFastPublishLayout>(publishLayouts, ReferenceEqualityComparer<PublishStatementNode>.Instance),
            maxStackDepth,
            null);

    public bool TryGetSlot(string name, out int slot)
        => _slots.TryGetValue(name, out slot);

    public bool TryGetExpressionProgram(ExpressionNode expression, out RegisterFastExpressionProgram program)
        => _expressionPrograms.TryGetValue(expression, out program!);

    public bool TryGetPublishLayout(PublishStatementNode publish, out RegisterFastPublishLayout layout)
        => _publishLayouts.TryGetValue(publish, out layout!);
}

internal sealed class ReferenceEqualityComparer<T> : IEqualityComparer<T>
    where T : class
{
    public static ReferenceEqualityComparer<T> Instance { get; } = new();

    public bool Equals(T? x, T? y) => ReferenceEquals(x, y);

    public int GetHashCode(T obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
}
