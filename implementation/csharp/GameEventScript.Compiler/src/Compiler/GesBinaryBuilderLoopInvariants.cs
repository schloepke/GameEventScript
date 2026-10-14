// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using GameEventScript.Api;
using OpCode = GameEventScript.Api.GesBytecodeOpCode;

namespace GameEventScript.Compiler;

internal sealed partial class GesBinaryBuilder
{
    // Moves loop-invariant instructions in front of their loop, so they run once per loop entry
    // instead of once per iteration. Only pure, total and deterministic operations qualify: they
    // read no random state, call no host code, emit nothing, consume no runtime budget and cannot
    // fail. Results are therefore unchanged; executed opcode counts and register requirements are not.
    private sealed class LoopInvariantHoistingPass : IGesBinaryOptimizationPass
    {
        public static readonly LoopInvariantHoistingPass Instance = new();

        private LoopInvariantHoistingPass()
        {
        }

        public bool Rewrite(RewriteContext context)
        {
            if (!HasBackEdge(context)) return false;
            var changed = false;
            // Each round moves at least one instruction outward, so this terminates. Only a loop
            // deferred because it contains a rewritten loop can still have candidates afterwards.
            bool deferred;
            do
            {
                changed |= HoistRound(context, out deferred);
            } while (deferred);

            return changed;
        }

        private static bool HasBackEdge(RewriteContext context)
        {
            var marked = new bool[context.LabelCount];
            for (var index = 0; index < context.Count; index++)
            {
                if (context.GetLabel(index) is { } label) marked[label.Id] = true;
                else if (context.GetInstruction(index) is { } instruction && ReadJumpTarget(instruction) is { } target && marked[target.Id]) return true;
            }

            return false;
        }

        // Moving instructions inside one loop changes no position outside it, so one analysis serves
        // every loop of a round that neither contains nor overlaps an already rewritten loop. A loop
        // containing a rewritten one waits for the next round and its fresh analysis.
        private static bool HoistRound(RewriteContext context, out bool deferred)
        {
            deferred = false;
            var analysis = new Analysis(context);
            var rewrittenStart = -1;
            var rewrittenEnd = -1;
            var moved = false;
            for (var end = 0; end < context.Count; end++)
            {
                if (context.GetInstruction(end) is not { } backEdge ||
                    ReadJumpTarget(backEdge) is not { } header ||
                    analysis.LabelIndex[header.Id] is var start && (start < 0 || start >= end))
                {
                    continue;
                }

                if (start <= rewrittenEnd && rewrittenStart <= end)
                {
                    deferred = true;
                    continue;
                }

                if (context.IsRoutineEntryLabel(header) ||
                    !analysis.HasSingleEntry(context, start, end))
                {
                    continue;
                }

                // One forward sweep finds chains such as a constant load and the product reading it.
                bool[]? marked = null;
                bool[]? hoistedRegisters = null;
                for (var index = start + 1; index < end; index++)
                {
                    if (context.GetInstruction(index) is not { } candidate ||
                        candidate.RoutineId != backEdge.RoutineId ||
                        !analysis.IsHoistable(context, candidate, index, start, end, hoistedRegisters))
                    {
                        continue;
                    }

                    (marked ??= new bool[context.Count])[index] = true;
                    (hoistedRegisters ??= new bool[context.RegisterCount])[candidate.Destination.RegisterRef.Id] = true;
                }

                if (marked is null) continue;
                context.MoveMarkedBefore(start, marked);
                // Loops are visited by ascending end, so later disjoint loops start after this one.
                rewrittenStart = rewrittenStart < 0 ? start : Math.Min(rewrittenStart, start);
                rewrittenEnd = end;
                moved = true;
            }

            return moved;
        }

        private sealed class Analysis
        {
            private readonly int[] _writeCount;
            private readonly int[] _lastWrite;
            private readonly int[] _firstRead;
            private readonly int[] _lastRead;
            private readonly int[] _firstReference;
            private readonly int[] _lastReference;

            internal Analysis(RewriteContext context)
            {
                _writeCount = new int[context.RegisterCount];
                _lastWrite = Filled(context.RegisterCount, -1);
                _firstRead = Filled(context.RegisterCount, int.MaxValue);
                _lastRead = Filled(context.RegisterCount, -1);
                LabelIndex = Filled(context.LabelCount, -1);
                _firstReference = Filled(context.LabelCount, int.MaxValue);
                _lastReference = Filled(context.LabelCount, -1);
                for (var index = 0; index < context.Count; index++)
                {
                    if (context.GetLabel(index) is { } label)
                    {
                        LabelIndex[label.Id] = index;
                        continue;
                    }

                    if (context.GetInstruction(index) is not { } instruction) continue;
                    RecordReference(instruction.Destination, index);
                    RecordReference(instruction.X, index);
                    RecordReference(instruction.Y, index);
                    RecordReference(instruction.A, index);
                    RecordReference(instruction.B, index);
                    RecordReference(instruction.C, index);
                    RecordReference(instruction.D, index);
                    RecordReference(instruction.SecondaryList, index);
                    RecordWrite(instruction.Destination, index);
                    // Closing an iterator also clears its register.
                    if (instruction.OpCode == OpCode.IteratorClose) RecordWrite(instruction.X, index);
                    RecordRead(instruction.X, index);
                    RecordRead(instruction.Y, index);
                    RecordRead(instruction.A, index);
                    RecordRead(instruction.B, index);
                    RecordRead(instruction.C, index);
                    RecordRead(instruction.D, index);
                    RecordRead(instruction.SecondaryList, index);
                }
            }

            internal int[] LabelIndex { get; }

            // The loop body starts at its header label and ends with the back edge. It must be entered
            // only by falling through the header, so code placed before the header runs on every entry.
            internal bool HasSingleEntry(RewriteContext context, int start, int end)
            {
                for (var index = start; index <= end; index++)
                {
                    if (context.GetLabel(index) is { } label && (_firstReference[label.Id] < start || _lastReference[label.Id] > end)) return false;
                }

                return true;
            }

            internal bool IsHoistable(RewriteContext context, InstructionPlan instruction, int index, int start, int end, bool[]? hoistedRegisters)
            {
                if (!IsPureTotalOperation(instruction.OpCode) ||
                    instruction.Destination.Kind != GesOperandKind.Register ||
                    !context.IsTemporary(instruction.Destination.RegisterRef))
                {
                    return false;
                }

                // The destination has no other definition and is read only later in this loop body,
                // so no path observes it before this instruction or after a zero-iteration loop.
                var destination = instruction.Destination.RegisterRef.Id;
                if (_writeCount[destination] != 1) return false;
                if (_lastRead[destination] >= 0 && (_firstRead[destination] <= index || _lastRead[destination] > end)) return false;

                // Every input is final before the loop starts, possibly by an earlier hoist of this sweep.
                return IsFinalBefore(instruction.X, destination, start, hoistedRegisters) &&
                       IsFinalBefore(instruction.Y, destination, start, hoistedRegisters) &&
                       IsFinalBefore(instruction.A, destination, start, hoistedRegisters) &&
                       IsFinalBefore(instruction.B, destination, start, hoistedRegisters) &&
                       IsFinalBefore(instruction.C, destination, start, hoistedRegisters) &&
                       IsFinalBefore(instruction.D, destination, start, hoistedRegisters) &&
                       IsFinalBefore(instruction.SecondaryList, destination, start, hoistedRegisters);
            }

            private bool IsFinalBefore(GesOperand operand, int destination, int start, bool[]? hoistedRegisters)
            {
                if (operand.Kind == GesOperandKind.RegisterList) return false;
                if (operand.Kind != GesOperandKind.Register) return true;
                var source = operand.RegisterRef.Id;
                return source != destination && (_lastWrite[source] < start || hoistedRegisters is not null && hoistedRegisters[source]);
            }

            private void RecordReference(GesOperand operand, int index)
            {
                if (operand.Kind != GesOperandKind.Label) return;
                var label = operand.LabelRef.Id;
                _firstReference[label] = Math.Min(_firstReference[label], index);
                _lastReference[label] = index;
            }

            private void RecordWrite(GesOperand operand, int index)
            {
                if (operand.Kind == GesOperandKind.Register) Write(operand.RegisterRef.Id, index);
                else if (operand.Kind == GesOperandKind.RegisterList)
                    for (var item = 0; item < operand.RegisterListValue!.Count; item++) Write(operand.RegisterListValue[item].Id, index);
            }

            private void Write(int register, int index)
            {
                _writeCount[register]++;
                _lastWrite[register] = index;
            }

            private void RecordRead(GesOperand operand, int index)
            {
                if (operand.Kind == GesOperandKind.Register) Read(operand.RegisterRef.Id, index);
                else if (operand.Kind == GesOperandKind.RegisterList)
                    for (var item = 0; item < operand.RegisterListValue!.Count; item++) Read(operand.RegisterListValue[item].Id, index);
            }

            private void Read(int register, int index)
            {
                _firstRead[register] = Math.Min(_firstRead[register], index);
                _lastRead[register] = index;
            }

            private static int[] Filled(int length, int value)
            {
                var values = new int[length];
                for (var index = 0; index < length; index++) values[index] = value;
                return values;
            }
        }

        // Constant loads and arithmetic are side-effect free, never fail (invalid input yields
        // Nothing or a float result), draw no random numbers and charge no runtime budget.
        private static bool IsPureTotalOperation(OpCode opcode)
            => opcode is OpCode.LoadNothing or OpCode.LoadTrue or OpCode.LoadFalse or OpCode.LoadInteger or OpCode.LoadFloat or OpCode.LoadPercentage or
                OpCode.LoadText or OpCode.LoadTag or OpCode.Add or OpCode.Subtract or OpCode.Multiply or OpCode.Divide or OpCode.IntegerDivide or
                OpCode.Modulo or OpCode.Remainder or OpCode.Negate or OpCode.Abs;
    }
}
