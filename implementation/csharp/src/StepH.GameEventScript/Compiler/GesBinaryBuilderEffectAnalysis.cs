// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using StepH.GameEventScript.Api;
using OpCode = StepH.GameEventScript.Api.GameEventScriptBytecodeOpCode;

namespace StepH.GameEventScript.Compiler;

internal sealed partial class GesBinaryBuilder
{
    // Compiler-only summaries. Random consumption, allocation and runtime budgets do not make an operation effectful.
    internal sealed class EffectAnalysis
    {
        private readonly GesBinaryBuilder _builder;
        private readonly int[] _routineByEntryLabel;
        private readonly Dictionary<string, int> _recordRoutineByName = new(StringComparer.Ordinal);
        private readonly bool[] _routineHasEffects;

        internal EffectAnalysis(GesBinaryBuilder builder, IReadOnlyList<PlanItem> items)
        {
            _builder = builder;
            _routineByEntryLabel = new int[builder._labels.Count];
            Array.Fill(_routineByEntryLabel, NoRoutineId);
            _routineHasEffects = new bool[builder._routines.Count];
            foreach (var routine in builder._routines)
            {
                _routineByEntryLabel[routine.EntryLabel.Id] = routine.Id;
                if (routine.Kind == GameEventScriptBinaryBindKind.Record) _recordRoutineByName.Add(routine.Name, routine.Id);
            }

            var callers = new List<int>?[_routineHasEffects.Length];
            var pendingCalls = new int[_routineHasEffects.Length];
            foreach (var item in items)
            {
                if (item.Instruction is not { } instruction || instruction.RoutineId == NoRoutineId) continue;
                var caller = instruction.RoutineId;
                if (!IsRoutineCall(instruction.OpCode))
                {
                    _routineHasEffects[caller] |= HasDirectEffects(instruction);
                    continue;
                }

                var callee = ResolveCallee(instruction);
                if (callee == NoRoutineId)
                {
                    // Unknown targets must never be assumed pure.
                    _routineHasEffects[caller] = true;
                    continue;
                }

                (callers[callee] ??= []).Add(caller);
                pendingCalls[caller]++;
            }

            // Bottom-up propagation is linear in the instruction/call graph and uses no recursive native stack.
            var ready = new Queue<int>();
            for (var routine = 0; routine < pendingCalls.Length; routine++)
                if (pendingCalls[routine] == 0) ready.Enqueue(routine);
            while (ready.Count > 0)
            {
                var callee = ready.Dequeue();
                if (callers[callee] is not { } calleeCallers) continue;
                foreach (var caller in calleeCallers)
                {
                    _routineHasEffects[caller] |= _routineHasEffects[callee];
                    if (--pendingCalls[caller] == 0) ready.Enqueue(caller);
                }
            }

            // Keep cyclic or cycle-dependent calls for the existing call-graph validator to reject.
            for (var routine = 0; routine < pendingCalls.Length; routine++)
                if (pendingCalls[routine] > 0) _routineHasEffects[routine] = true;
        }

        internal bool HasEffects(InstructionPlan instruction)
        {
            if (!IsRoutineCall(instruction.OpCode)) return HasDirectEffects(instruction);
            var callee = ResolveCallee(instruction);
            return callee == NoRoutineId || _routineHasEffects[callee];
        }

        private static bool IsRoutineCall(OpCode opcode) => opcode is OpCode.Call or OpCode.CreateRecord or OpCode.CastCustom;

        private int ResolveCallee(InstructionPlan instruction)
        {
            if (instruction.OpCode == OpCode.CastCustom)
                return instruction.Y.Kind == GesOperandKind.Text && instruction.Y.TextValue is { } name && _recordRoutineByName.TryGetValue(name, out var routine) ? routine : NoRoutineId;

            if (instruction.OpCode == OpCode.Call)
                return instruction.Y.Kind == GesOperandKind.Label ? ResolveEntryLabel(instruction.Y.LabelRef.Id) : NoRoutineId;

            if (instruction.X.Kind == GesOperandKind.Bind && (uint)instruction.X.BindRef.Id < (uint)_builder._binds.Count &&
                _builder._binds[instruction.X.BindRef.Id] is { Kind: GameEventScriptBinaryBindKind.Record, EntryLabel: { } label })
                return ResolveEntryLabel(label.Id);

            return NoRoutineId;
        }

        private int ResolveEntryLabel(int label) => (uint)label < (uint)_routineByEntryLabel.Length ? _routineByEntryLabel[label] : NoRoutineId;

        internal static bool HasDirectEffects(InstructionPlan instruction)
        {
            // Dynamic external-field access can call host code. Without a purity contract it is conservative, like an extension call.
            if (instruction.OpCode is OpCode.MemberAccess or OpCode.PropertyAccess or OpCode.KeysOfMap or OpCode.ValuesOfMap or OpCode.EntriesOfMap) return true;
            if (instruction.OpCode == OpCode.Cast) return instruction.Y.Kind != GesOperandKind.Type || instruction.Y.TypeKind == GameEventScriptBytecodeTypeKind.Map;
            if (CanDiscardResult(instruction.OpCode)) return false;

            return instruction.OpCode switch
            {
                // These operations affect only the current frame, its control flow, argument staging or private builders.
                OpCode.Nop or OpCode.RegisterLocals or OpCode.Jump or OpCode.JumpIfTrue or OpCode.JumpIfFalse or OpCode.JumpIfNotTrue or OpCode.JumpIfNothing or OpCode.ReturnVoid or OpCode.ReturnValue or
                    OpCode.StageRegister or OpCode.StageNothing or OpCode.StageTrue or OpCode.StageFalse or OpCode.StageInteger or OpCode.StageFloat or OpCode.StageText or OpCode.StageTag or OpCode.StagePercentage or
                    OpCode.CreateVector or OpCode.CreatePoint or OpCode.CreateList or OpCode.CreateMap or OpCode.CreateRecordValue or OpCode.RandomPush or OpCode.RandomPushConstant or OpCode.RandomPop or
                    OpCode.IteratorCreateOrJump or OpCode.IteratorNext or OpCode.IteratorClose or OpCode.ListBuilderCreate or OpCode.ListBuilderAdd or OpCode.ListBuilderFinish or
                    OpCode.MapBuilderCreate or OpCode.MapBuilderAdd or OpCode.MapBuilderFinish or OpCode.DistinctBuilderCreate or OpCode.DistinctBuilderAdd or OpCode.DistinctBuilderFinish or
                    OpCode.GroupBuilderCreate or OpCode.GroupBuilderAdd or OpCode.GroupBuilderFinish or OpCode.OrderBuilderCreate or OpCode.OrderBuilderAdd or OpCode.OrderBuilderFinishAscending or OpCode.OrderBuilderFinishDescending => false,
                // Emit/Publish, extension calls, external constructors and future unclassified operations may have outside effects.
                _ => true
            };
        }

        internal static bool CanDiscardResult(OpCode opcode)
        {
            return opcode switch
            {
                OpCode.CastCustom or
                    OpCode.ParseLiteral or
                    OpCode.RandomTake or
                    OpCode.RandomTakeFloat or
                    OpCode.Chance or
                    OpCode.CreateDice or
                    OpCode.OneRandom or
                    OpCode.TakeRandom or
                    OpCode.OneWeighted or
                    OpCode.TakeWeighted or
                    OpCode.Shuffle or
                    OpCode.Cast or
                    OpCode.CastUnit or
                    OpCode.CastNumeric or
                    OpCode.CheckType or
                    OpCode.CheckCustomType or
                    OpCode.CheckUnit or
                    OpCode.CheckNumeric or
                    OpCode.CheckInteger or
                    OpCode.CheckFractional or
                    OpCode.Move or
                    OpCode.MemberAccess or
                    OpCode.IndexAccess or
                    OpCode.PropertyAccess or
                    OpCode.BindHandler or
                    OpCode.LoadNothing or
                    OpCode.LoadTrue or
                    OpCode.LoadFalse or
                    OpCode.LoadInteger or
                    OpCode.LoadFloat or
                    OpCode.LoadPercentage or
                    OpCode.LoadText or
                    OpCode.LoadTag or
                    OpCode.LoadHandler or
                    OpCode.LoadMessage or
                    OpCode.CreateSeries or
                    OpCode.CreateRange or
                    OpCode.CreateRangeWithStep or
                    OpCode.CreateRangeIterator or
                    OpCode.CreateRangeIteratorWithStep or
                    OpCode.CreateRangeIteratorShort or
                    OpCode.HasValue or
                    OpCode.IsEmpty or
                    OpCode.Default or
                    OpCode.Or or
                    OpCode.And or
                    OpCode.Xor or
                    OpCode.Implies or
                    OpCode.Not or
                    OpCode.Equal or
                    OpCode.NotEqual or
                    OpCode.Less or
                    OpCode.Greater or
                    OpCode.LessOrEqual or
                    OpCode.GreaterOrEqual or
                    OpCode.Add or
                    OpCode.Subtract or
                    OpCode.Multiply or
                    OpCode.Divide or
                    OpCode.Power or
                    OpCode.IntegerDivide or
                    OpCode.Modulo or
                    OpCode.Remainder or
                    OpCode.Min or
                    OpCode.Max or
                    OpCode.Negate or
                    OpCode.Abs or
                    OpCode.LogN or
                    OpCode.Clamp or
                    OpCode.Term or
                    OpCode.Exp or
                    OpCode.Floor or
                    OpCode.Ceil or
                    OpCode.Truncate or
                    OpCode.RoundHalfEven or
                    OpCode.RoundHalfUp or
                    OpCode.RoundHalfDown or
                    OpCode.DegreeToRadians or
                    OpCode.DegreeFromRadians or
                    OpCode.WrapDegree or
                    OpCode.Sin or
                    OpCode.Cos or
                    OpCode.Tan or
                    OpCode.Asin or
                    OpCode.Acos or
                    OpCode.Atan or
                    OpCode.Atan2 or
                    OpCode.Hypot2D or
                    OpCode.Hypot3D or
                    OpCode.Distance or
                    OpCode.Distance2D or
                    OpCode.Distance3D or
                    OpCode.DistanceSquared or
                    OpCode.DistanceSquared2D or
                    OpCode.DistanceSquared3D or
                    OpCode.LengthSquared or
                    OpCode.LengthSquared2D or
                    OpCode.LengthSquared3D or
                    OpCode.Normalize or
                    OpCode.Normalize2D or
                    OpCode.Normalize3D or
                    OpCode.Dot or
                    OpCode.Dot2D or
                    OpCode.Dot3D or
                    OpCode.Cross or
                    OpCode.Cross2D or
                    OpCode.Cross3D or
                    OpCode.AngleBetween or
                    OpCode.AngleBetween2D or
                    OpCode.AngleBetween3D or
                    OpCode.TakeFirst or
                    OpCode.DropFirst or
                    OpCode.TakeLast or
                    OpCode.DropLast or
                    OpCode.TakeHighest or
                    OpCode.TakeLowest or
                    OpCode.DropHighest or
                    OpCode.DropLowest or
                    OpCode.Count or
                    OpCode.StartsWith or
                    OpCode.EndsWith or
                    OpCode.Contains or
                    OpCode.ContainsAny or
                    OpCode.ContainsAll or
                    OpCode.HasAny or
                    OpCode.HasAll or
                    OpCode.ContainsValue or
                    OpCode.Union or
                    OpCode.Intersect or
                    OpCode.Zip or
                    OpCode.KeysOfMap or
                    OpCode.ValuesOfMap or
                    OpCode.EntriesOfMap or
                    OpCode.First or
                    OpCode.Last or
                    OpCode.Single or
                    OpCode.IteratorCreate or
                    OpCode.Distinct or
                    OpCode.SortAscending or
                    OpCode.SortDescending or
                    OpCode.Reverse or
                    OpCode.HasPattern or
                    OpCode.TakePattern => true,
                _ => false
            };
        }
    }
}
