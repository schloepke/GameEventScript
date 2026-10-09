// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using GameEventScript.Api;

namespace GameEventScript.Compiler;

internal static partial class GesCompiler
{
    private sealed partial class BinaryCompiler
    {
        private readonly HashSet<int> _nonListBindings = [];

        private bool IsKnownNonList(ExpressionNode value, LoweringContext context)
            => value switch
            {
                IdentifierExpressionNode name => _nonListBindings.Contains(context.Require(name.Name).Id),
                IntegerLiteralExpressionNode or FloatLiteralExpressionNode or UnitIntegerLiteralExpressionNode or UnitFloatLiteralExpressionNode or
                    PercentageLiteralExpressionNode or BooleanLiteralExpressionNode or NothingLiteralExpressionNode or TextLiteralExpressionNode or TagLiteralExpressionNode => true,
                BinaryExpressionNode { Operator: GesBinaryOperator.Divide or GesBinaryOperator.IntegerDivide or GesBinaryOperator.Modulo or GesBinaryOperator.Remainder or GesBinaryOperator.Power } => true,
                _ => false
            };

        private sealed record ComponentRow(GesRegisterRef[] Components, bool IsEntry);

        private readonly Dictionary<int, ComponentRow> _componentRows = [];

        private static bool IsEntriesSource(ExpressionNode source)
            => source is CollectionAccessExpressionNode { Selector: ExpressionSelectorNode { Expression: TagLiteralExpressionNode { Name: "entries" } } };

        private static bool NeedsWholeRow(CollectionSelectorNode selector)
            => selector is SelectSelectorNode { BindingNames.Count: < 2 } or SumSelectorNode { BindingNames.Count: < 2 } or AverageSelectorNode { BindingNames.Count: < 2 } or
                FilterSelectorNode or MapSelectorNode or DistinctSelectorNode or GroupBySelectorNode or OrderBySelectorNode or EdgeSelectorNode or MinSelectorNode or MaxSelectorNode or
                ObjectMatchSelectorNode or SequenceSliceSelectorNode or DrawSelectorNode;

        private void MaterializeRow(GesRegisterRef current)
        {
            if (!_componentRows.TryGetValue(current.Id, out var row)) return;
            foreach (var component in row.Components) _builder.StageRegister(component);
            if (row.IsEntry) _builder.CreateMap(current, new[] { "key", "value" });
            else _builder.CreateList(current);
        }

        private ComponentRow? EmitComponentIterator(GesRegisterRef iterator, ExpressionNode source, GesLabelRef invalid, LoweringContext context, ExpressionState state, CollectionSelectorNode? guardedTerminal = null)
        {
            int arity;
            var entry = IsEntriesSource(source);
            if (source is CombinedCollectionExpressionNode combined)
            {
                arity = combined.Sources.Count;
                var sources = EmitCombinedSources(combined, context, state);
                byte mode = combined.Operation switch { "union" => 1, "intersect" => 2, "difference" => 3, "lockstep" => 4, _ => 5 };
                if (mode < 4 && guardedTerminal is OrderBySelectorNode or DistinctSelectorNode { Identifier: not null, Projection: not null } or GroupBySelectorNode)
                    EmitCombinedResultGuard(sources, guardedTerminal is GroupBySelectorNode, invalid, context, state);
                _builder.IteratorCreateSources(iterator, sources, mode, invalid);
                if (mode < 4) return null;
            }
            else
            {
                arity = 2;
                var target = EmitExpressionForRead(((CollectionAccessExpressionNode)source).Target, context, state);
                _builder.IteratorCreateEntries(iterator, target, invalid);
            }
            var components = new GesRegisterRef[arity];
            for (var index = 0; index < arity; index++) components[index] = state.AllocateTemporary(_builder, context);
            return new ComponentRow(components, entry);
        }

        private void EmitCombinedResultGuard(GesRegisterRef[] sources, bool allowMap, GesLabelRef invalid, LoweringContext context, ExpressionState state)
        {
            // Map-left operations retain Map; otherwise any List source changes
            // a valid Dice/List combination to List. All-Dice results stay Dice.
            var check = state.AllocateTemporary(_builder, context);
            var valid = _builder.AddLabel("combined_result_valid");
            _builder.CheckType(check, sources[0], GameEventScriptBytecodeTypeKind.Map);
            _builder.JumpIfTrue(check, allowMap ? valid : invalid);
            foreach (var source in sources)
            {
                _builder.CheckType(check, source, GameEventScriptBytecodeTypeKind.List);
                _builder.JumpIfTrue(check, valid);
            }
            _builder.Jump(invalid);
            _builder.MarkLabel(valid);
        }

        private GesRegisterRef[] EmitCombinedSources(CombinedCollectionExpressionNode combined, LoweringContext context, ExpressionState state)
        {
            var sources = new GesRegisterRef[combined.Sources.Count];
            for (var index = 0; index < sources.Length; index++)
                sources[index] = EmitExpressionForRead(combined.Sources[index], context, state);
            return sources;
        }

        private void EmitCombinedCollection(GesRegisterRef destination, CombinedCollectionExpressionNode combined, LoweringContext context, ExpressionState state)
        {
            if (combined.Operation is "cartesian" or "lockstep")
            {
                var select = new SelectSelectorNode("item", new IdentifierExpressionNode("item"));
                EmitInlineIteratorPipeline(destination, default, new[] { select }, 0, select, context, state, combined);
                return;
            }

            var invalid = _builder.AddLabel("combined_invalid");
            var done = _builder.AddLabel("combined_done");
            var sources = EmitCombinedSources(combined, context, state);
            var current = sources[0];
            var check = state.AllocateTemporary(_builder, context);
            for (var index = 1; index < combined.Sources.Count; index++)
            {
                EmitSourceTypeGuard(current, invalid, context, state, GameEventScriptBytecodeTypeKind.List, GameEventScriptBytecodeTypeKind.Map, GameEventScriptBytecodeTypeKind.Dice);
                var right = sources[index];
                var result = state.AllocateTemporary(_builder, context);
                if (combined.Operation == "union") _builder.Union(result, current, right);
                else if (combined.Operation == "intersect") _builder.Intersect(result, current, right);
                else _builder.Subtract(result, current, right);
                _builder.CheckType(check, result, GameEventScriptBytecodeTypeKind.Nothing);
                _builder.JumpIfTrue(check, invalid);
                current = result;
            }
            _builder.Move(destination, current);
            _builder.Jump(done);
            _builder.MarkLabel(invalid);
            _builder.LoadNothing(destination);
            _builder.MarkLabel(done);
        }
    }
}
