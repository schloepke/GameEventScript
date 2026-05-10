using System;
using System.Collections.Generic;
using StepH.GameEventScript.Api;
using StepH.GameEventScript.Runtime;
using StepH.GameEventScript.Types;

namespace StepH.GameEventScript.BytecodeVM;

internal sealed partial class GesBytecodeVmExecutionSession
{
    internal static Fiber CreateFiber(
        GesBytecodeVmExecutable compiledScript,
        GameEventScriptContext context,
        GesBytecodeVmCompiledHandler handler,
        IReadOnlyDictionary<string, GameEventScriptValue> args)
    {
        var session = new GesBytecodeVmExecutionSession(compiledScript, context, handler.Slots, handler.LocalSlotCount, handler.DiagnosticsEnabled);
        return new Fiber(session, handler, args);
    }

    internal sealed class Fiber
    {
        private readonly GesBytecodeVmExecutionSession _session;
        private readonly Stack<Frame> _frames = new();
        private BytecodeVmValue _lastValue = BytecodeVmValue.Nothing;
        private bool _lastSuccess = true;
        private bool _hasLastResult;
        private int _remainingOpcodes;

        internal Fiber(
            GesBytecodeVmExecutionSession session,
            GesBytecodeVmCompiledHandler handler,
            IReadOnlyDictionary<string, GameEventScriptValue> args)
        {
            _session = session;
            _frames.Push(new HandlerFrame(handler, args));
        }

        public bool IsCompleted { get; private set; }

        public bool Failed { get; private set; }

        public int ExecutedOpcodesInLastSlice { get; private set; }

        public int RunSlice(int maxOpcodes)
        {
            if (maxOpcodes <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxOpcodes), "VM slice opcode budget must be greater than zero.");
            }

            if (IsCompleted)
            {
                ExecutedOpcodesInLastSlice = 0;
                return 0;
            }

            _remainingOpcodes = maxOpcodes;
            ExecutedOpcodesInLastSlice = 0;

            while (!IsCompleted && !Failed && _frames.Count > 0)
            {
                var signal = _frames.Peek().Run(this);
                if (signal == FrameSignal.Paused)
                {
                    break;
                }

                if (signal != FrameSignal.Completed)
                {
                    continue;
                }

                var frame = _frames.Pop();
                frame.Exit(this);
                if (_hasLastResult && !_lastSuccess && _frames.Count == 0)
                {
                    Failed = true;
                    IsCompleted = true;
                }
            }

            if (_frames.Count == 0)
            {
                IsCompleted = true;
            }

            return ExecutedOpcodesInLastSlice;
        }

        private void Push(Frame frame) => _frames.Push(frame);

        private void Complete(BytecodeVmValue value, bool success = true)
        {
            _lastValue = value;
            _lastSuccess = success;
            _hasLastResult = true;
        }

        private bool TryTakeResult(out BytecodeVmValue value, out bool success)
        {
            if (!_hasLastResult)
            {
                value = BytecodeVmValue.Nothing;
                success = false;
                return false;
            }

            value = _lastValue;
            success = _lastSuccess;
            _hasLastResult = false;
            return true;
        }

        private bool TryConsumeInstruction(string detail)
        {
            if (_remainingOpcodes <= 0)
            {
                return false;
            }

            if (!_session._runtimeBudget.TryConsumeExecutionStep(detail))
            {
                Halt();
                return false;
            }

            _remainingOpcodes--;
            ExecutedOpcodesInLastSlice++;
            return true;
        }

        private void Halt()
        {
            while (_frames.Count > 0)
            {
                var frame = _frames.Pop();
                frame.Exit(this);
            }

            IsCompleted = true;
        }

        private enum FrameSignal
        {
            Running,
            Paused,
            Completed
        }

        private abstract class Frame
        {
            public abstract FrameSignal Run(Fiber fiber);

            public virtual void Exit(Fiber fiber)
            {
            }
        }

        private sealed class LinearRangeFrame(
            int startAddress,
            int endAddress,
            LinearArgumentSource? arguments) : Frame
        {
            private List<LinearCallFrame>? _callFrames;
            private LinearArgumentSource? _arguments = arguments;
            private int _pc = startAddress;
            private int _endAddress = endAddress;
            private int _randomScopeMark;
            private bool _initialized;
            private bool _waitingForChild;

            public override FrameSignal Run(Fiber fiber)
            {
                var session = fiber._session;
                var code = session._compiledScript.LinearExecutable.Code;
                if (!_initialized)
                {
                    _initialized = true;
                    _randomScopeMark = session._randomScopes.Count;
                    if ((uint)_pc > (uint)code.Count ||
                        (uint)_endAddress > (uint)code.Count ||
                        _pc > _endAddress)
                    {
                        fiber.Complete(BytecodeVmValue.Nothing, success: false);
                        return FrameSignal.Completed;
                    }
                }

                if (_waitingForChild)
                {
                    if (!fiber.TryTakeResult(out _, out var success) || !success)
                    {
                        fiber.Complete(BytecodeVmValue.Nothing, success: false);
                        return FrameSignal.Completed;
                    }

                    _waitingForChild = false;
                }

                while (_pc < _endAddress)
                {
                    if (!fiber.TryConsumeInstruction("Linear instruction execution budget exhausted."))
                    {
                        return FrameSignal.Paused;
                    }

                    var instructionAddress = _pc;
                    if (!session.RecordLinearHandlerInvokedIfReady(_arguments))
                    {
                        fiber.Complete(BytecodeVmValue.Nothing, success: false);
                        return FrameSignal.Completed;
                    }

                    session.RecordLinearDiagnosticsBefore(instructionAddress);
                    var instruction = code[instructionAddress];
                    var callFrameCountBefore = _callFrames?.Count ?? 0;
                    if (TryStartLinearChildFrame(session, instruction, out var childFrame))
                    {
                        _pc = instruction.B_U16;
                        session.RecordLinearDiagnosticsAfter(instructionAddress);
                        if (childFrame is not null)
                        {
                            _waitingForChild = true;
                            fiber.Push(childFrame);
                            return FrameSignal.Running;
                        }

                        continue;
                    }

                    if (!session.TryExecuteLinearInstruction(
                            instruction,
                            ref _pc,
                            ref _endAddress,
                            ref _arguments,
                            ref _callFrames,
                            out var returned,
                            out var returnValue))
                    {
                        fiber.Complete(BytecodeVmValue.Nothing, success: false);
                        return FrameSignal.Completed;
                    }

                    var callFrameCountAfter = _callFrames?.Count ?? 0;
                    if (callFrameCountAfter <= callFrameCountBefore ||
                        instruction.OpCode is not (GameEventScriptBytecodeOpCode.Call or GameEventScriptBytecodeOpCode.PredicateTest))
                    {
                        session.RecordLinearDiagnosticsAfter(instructionAddress);
                    }

                    if (returned)
                    {
                        fiber.Complete(returnValue);
                        return FrameSignal.Completed;
                    }

                    if (session._halted)
                    {
                        fiber.Halt();
                        return FrameSignal.Paused;
                    }
                }

                if (!session.RecordLinearHandlerInvokedIfReady(_arguments))
                {
                    fiber.Complete(BytecodeVmValue.Nothing, success: false);
                    return FrameSignal.Completed;
                }

                fiber.Complete(BytecodeVmValue.Nothing);
                return FrameSignal.Completed;
            }

            public override void Exit(Fiber fiber)
            {
                fiber._session.UnwindLinearCallFrames(_callFrames);
                fiber._session.UnwindRandomScopes(_randomScopeMark);
            }

            private static bool TryStartLinearChildFrame(
                GesBytecodeVmExecutionSession session,
                GameEventScriptBytecodeInstruction instruction,
                out Frame? frame)
            {
                frame = null;
                switch (instruction.OpCode)
                {
                    case GameEventScriptBytecodeOpCode.ForRange:
                        return TryCreateLinearRangeLoopFrame(session, instruction, out frame);

                    case GameEventScriptBytecodeOpCode.ForCollection:
                        return TryCreateLinearCollectionLoopFrame(session, instruction, out frame);

                    default:
                        return false;
                }
            }

            private static bool TryCreateLinearRangeLoopFrame(
                GesBytecodeVmExecutionSession session,
                GameEventScriptBytecodeInstruction instruction,
                out Frame? frame)
            {
                frame = null;
                if (!session.TryGetLoopLayout(instruction.C_U16, out var loopLayout) ||
                    !session.TryGetIterationSourceLayout(loopLayout.IterationSourceLayoutIndex, out var sourceLayout) ||
                    !session.ResolveSlot(sourceLayout.RangeFromSlot).TryGetRangeInteger(out var from) ||
                    !session.ResolveSlot(sourceLayout.RangeToSlot).TryGetRangeInteger(out var to))
                {
                    return false;
                }

                var step = 1L;
                if (sourceLayout.RangeStepSlot >= 0 &&
                    !session.ResolveSlot(sourceLayout.RangeStepSlot).TryGetRangeInteger(out step))
                {
                    return false;
                }

                if (step == 0)
                {
                    return true;
                }

                var length = GesRuntimeLimitUtilities.GetRangeLength(from, to, step);
                if (!session._runtimeBudget.TryCheckRangeLength(length, "For loop range would enumerate more range items than allowed."))
                {
                    return true;
                }

                frame = new LinearRangeLoopFrame(loopLayout.IdentifierSlot, from, to, step, instruction.A_U16, instruction.B_U16);
                return true;
            }

            private static bool TryCreateLinearCollectionLoopFrame(
                GesBytecodeVmExecutionSession session,
                GameEventScriptBytecodeInstruction instruction,
                out Frame? frame)
            {
                frame = null;
                if (!session.TryGetLoopLayout(instruction.C_U16, out var loopLayout) ||
                    !session.TryGetIterationSourceLayout(loopLayout.IterationSourceLayoutIndex, out var sourceLayout))
                {
                    return false;
                }

                var sourceValue = session.ResolveSlot(sourceLayout.CollectionSlot).ToGameEventScriptValue();
                if (GesRuntimeLimitUtilities.TryGetRangeLength(sourceValue, out var length) &&
                    !session._runtimeBudget.TryCheckRangeLength(length, "Iteration source would enumerate more range items than allowed."))
                {
                    return true;
                }

                frame = new LinearCollectionLoopFrame(loopLayout.IdentifierSlot, sourceValue.AsEnumerable().GetEnumerator(), instruction.A_U16, instruction.B_U16);
                return true;
            }

        }

        private abstract class LinearLoopFrame(int identifierSlot, int bodyStart, int bodyEnd) : Frame
        {
            private bool _waitingForBody;

            protected abstract bool TryGetCurrent(Fiber fiber, out BytecodeVmValue item);

            protected abstract void AdvanceAfterBody();

            public override FrameSignal Run(Fiber fiber)
            {
                if (_waitingForBody)
                {
                    if (!fiber.TryTakeResult(out _, out var bodySuccess) || !bodySuccess)
                    {
                        fiber.Complete(BytecodeVmValue.Nothing, success: false);
                        return FrameSignal.Completed;
                    }

                    AdvanceAfterBody();
                    _waitingForBody = false;
                }

                if (!TryGetCurrent(fiber, out var item))
                {
                    fiber.Complete(BytecodeVmValue.Nothing);
                    return FrameSignal.Completed;
                }

                if (!fiber.TryConsumeInstruction("Loop iteration budget exhausted."))
                {
                    return FrameSignal.Paused;
                }

                if (!fiber._session._runtimeBudget.TryConsumeLoopIteration("Loop iteration budget exhausted."))
                {
                    fiber.Halt();
                    return FrameSignal.Paused;
                }

                _waitingForBody = true;
                fiber.Push(new LinearLoopIterationFrame(identifierSlot, item, bodyStart, bodyEnd));
                return FrameSignal.Running;
            }
        }

        private sealed class LinearRangeLoopFrame(
            int identifierSlot,
            long from,
            long to,
            long step,
            int bodyStart,
            int bodyEnd) : LinearLoopFrame(identifierSlot, bodyStart, bodyEnd)
        {
            private long _current = from;
            private bool _completed;

            protected override bool TryGetCurrent(Fiber fiber, out BytecodeVmValue item)
            {
                item = BytecodeVmValue.Nothing;
                if (_completed || (step > 0 ? _current > to : _current < to))
                {
                    return false;
                }

                item = BytecodeVmValue.Integer(_current);
                return true;
            }

            protected override void AdvanceAfterBody()
            {
                if (_completed)
                {
                    return;
                }

                if (step > 0)
                {
                    if (long.MaxValue - _current < step)
                    {
                        _completed = true;
                        return;
                    }
                }
                else if (long.MinValue - _current > step)
                {
                    _completed = true;
                    return;
                }

                _current += step;
            }
        }

        private sealed class LinearCollectionLoopFrame(
            int identifierSlot,
            IEnumerator<GameEventScriptValue> items,
            int bodyStart,
            int bodyEnd) : LinearLoopFrame(identifierSlot, bodyStart, bodyEnd)
        {
            private bool _hasCurrent;
            private BytecodeVmValue _current = BytecodeVmValue.Nothing;

            protected override bool TryGetCurrent(Fiber fiber, out BytecodeVmValue item)
            {
                if (!_hasCurrent)
                {
                    if (!items.MoveNext())
                    {
                        item = BytecodeVmValue.Nothing;
                        return false;
                    }

                    _current = BytecodeVmValue.FromGameEventScriptValue(items.Current);
                    _hasCurrent = true;
                }

                item = _current;
                return true;
            }

            protected override void AdvanceAfterBody()
            {
                _current = BytecodeVmValue.Nothing;
                _hasCurrent = false;
            }

            public override void Exit(Fiber fiber)
                => items.Dispose();
        }

        private sealed class LinearLoopIterationFrame(
            int identifierSlot,
            BytecodeVmValue item,
            int bodyStart,
            int bodyEnd) : Frame
        {
            private int _stage;
            private bool _enteredScope;

            public override FrameSignal Run(Fiber fiber)
            {
                var session = fiber._session;
                if (_stage == 0)
                {
                    session.EnterScope();
                    _enteredScope = true;
                    if (!session.DefineSlot(identifierSlot, item))
                    {
                        fiber.Complete(BytecodeVmValue.Nothing, success: false);
                        return FrameSignal.Completed;
                    }

                    _stage = 1;
                    fiber.Push(new LinearRangeFrame(bodyStart, bodyEnd, null));
                    return FrameSignal.Running;
                }

                if (!fiber.TryTakeResult(out _, out var success) || !success)
                {
                    fiber.Complete(BytecodeVmValue.Nothing, success: false);
                    return FrameSignal.Completed;
                }

                fiber.Complete(BytecodeVmValue.Nothing);
                return FrameSignal.Completed;
            }

            public override void Exit(Fiber fiber)
            {
                if (_enteredScope)
                {
                    fiber._session.ExitScope();
                    _enteredScope = false;
                }
            }
        }

        private sealed class HandlerFrame(
            GesBytecodeVmCompiledHandler handler,
            IReadOnlyDictionary<string, GameEventScriptValue> args) : Frame
        {
            private int _stage;
            private bool _enteredScope;

            public override FrameSignal Run(Fiber fiber)
            {
                var session = fiber._session;
                if (_stage == 0)
                {
                    session.EnterScope();
                    _enteredScope = true;
                    _stage = 1;
                    fiber.Push(new LinearRangeFrame(
                        handler.EntryAddress,
                        session._compiledScript.LinearExecutable.Code.Count,
                        LinearArgumentSource.ForHandler(handler, args)));
                    return FrameSignal.Running;
                }

                if (!fiber.TryTakeResult(out _, out var success) || !success)
                {
                    fiber.Complete(BytecodeVmValue.Nothing, success: false);
                    return FrameSignal.Completed;
                }

                fiber.Complete(BytecodeVmValue.Nothing);
                return FrameSignal.Completed;
            }

            public override void Exit(Fiber fiber)
            {
                if (_enteredScope)
                {
                    fiber._session.ExitScope();
                    _enteredScope = false;
                }
            }
        }
    }
}
