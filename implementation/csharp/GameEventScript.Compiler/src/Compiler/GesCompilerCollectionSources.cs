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
            => selector is SelectSelectorNode { BindingNames.Count: < 2 } or SumSelectorNode { BindingNames.Count: < 2 } or AverageSelectorNode { BindingNames.Count: < 2 } or FilterSelectorNode or MapSelectorNode or DistinctSelectorNode or GroupBySelectorNode or OrderBySelectorNode or
                EdgeSelectorNode or MinSelectorNode or MaxSelectorNode or ObjectMatchSelectorNode or SequenceSliceSelectorNode or DrawSelectorNode;

        private void MaterializeRow(GesRegisterRef current)
        {
            if (!_componentRows.TryGetValue(current.Id, out var row)) return;
            foreach (var component in row.Components) _builder.StageRegister(component);
            if (row.IsEntry) _builder.CreateMap(current, new[] { "key", "value" });
            else _builder.CreateList(current);
        }

        private ComponentRow? EmitComponentIterator(GesRegisterRef iterator, ExpressionNode source, GesLabelRef invalid, LoweringContext context, ExpressionState state)
        {
            int arity;
            var entry = IsEntriesSource(source);
            if (source is CombinedCollectionExpressionNode combined)
            {
                if (combined.Operation is "union" or "intersect" or "difference")
                {
                    EmitSequenceIterator(iterator, combined, invalid, context, state);
                    return null;
                }
                arity = combined.Sources.Count;
                var sources = new GesRegisterRef[arity];
                var validation = state.AllocateTemporary(_builder, context);
                var invalidSource = _builder.AddLabel("sources_invalid");
                var ready = _builder.AddLabel("sources_ready");
                for (var index = 0; index < arity; index++)
                {
                    sources[index] = EmitExpressionForRead(combined.Sources[index], context, state);
                    _builder.IteratorCreateOrJump(validation, sources[index], invalidSource);
                    _builder.IteratorClose(validation);
                }
                _builder.IteratorCreateSources(iterator, sources, combined.Operation == "cartesian" ? (byte)5 : (byte)4, invalid);
                _builder.Jump(ready);
                _builder.MarkLabel(invalidSource);
                _builder.LoadNothing(iterator);
                _builder.Jump(invalid);
                _builder.MarkLabel(ready);
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

        private void EmitSequenceIterator(GesRegisterRef iterator, CombinedCollectionExpressionNode combined, GesLabelRef invalid, LoweringContext context, ExpressionState state)
        {
            var sources = new List<GesRegisterRef>();
            var first = EmitExpressionForRead(combined.Sources[0], context, state);
            var failed = _builder.AddLabel("sequence_invalid");
            var done = _builder.AddLabel("sequence_ready");
            EmitSourceTypeGuard(first, failed, context, state, GameEventScriptBytecodeTypeKind.List, GameEventScriptBytecodeTypeKind.Map, GameEventScriptBytecodeTypeKind.Dice);
            sources.Add(first);
            byte mode = combined.Operation == "union" ? (byte)1 : combined.Operation == "intersect" ? (byte)2 : (byte)3;
            for (var index = 1; index < combined.Sources.Count; index++)
            {
                sources.Add(EmitExpressionForRead(combined.Sources[index], context, state));
                _builder.IteratorCreateSources(iterator, sources.ToArray(), mode, failed);
                if (index + 1 < combined.Sources.Count) _builder.IteratorClose(iterator);
            }
            _builder.Jump(done);
            _builder.MarkLabel(failed);
            _builder.LoadNothing(iterator);
            _builder.Jump(invalid);
            _builder.MarkLabel(done);
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
            var current = EmitExpressionForRead(combined.Sources[0], context, state);
            var check = state.AllocateTemporary(_builder, context);
            for (var index = 1; index < combined.Sources.Count; index++)
            {
                EmitSourceTypeGuard(current, invalid, context, state, GameEventScriptBytecodeTypeKind.List, GameEventScriptBytecodeTypeKind.Map, GameEventScriptBytecodeTypeKind.Dice);
                var right = EmitExpressionForRead(combined.Sources[index], context, state);
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
