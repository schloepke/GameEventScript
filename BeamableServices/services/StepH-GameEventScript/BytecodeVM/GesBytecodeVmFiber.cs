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
            private List<BytecodeVmValue>? _stagedArguments;
            private int _pc = startAddress;
            private int _endAddress = endAddress;
            private int _randomScopeMark;
            private bool _initialized;

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
                    if (!session.TryExecuteLinearInstruction(
                            instruction,
                            ref _pc,
                            ref _endAddress,
                            ref _arguments,
                            ref _callFrames,
                            ref _stagedArguments,
                            out var returned,
                            out var returnValue,
                            out _))
                    {
                        fiber.Complete(BytecodeVmValue.Nothing, success: false);
                        return FrameSignal.Completed;
                    }

                    var callFrameCountAfter = _callFrames?.Count ?? 0;
                    if (callFrameCountAfter <= callFrameCountBefore ||
                        instruction.OpCode is not (GameEventScriptBytecodeOpCode.Call or GameEventScriptBytecodeOpCode.CallPredicate))
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
        }

        private sealed class HandlerFrame(
            GesBytecodeVmCompiledHandler handler,
            IReadOnlyDictionary<string, GameEventScriptValue> args) : Frame
        {
            private int _stage;

            public override FrameSignal Run(Fiber fiber)
            {
                var session = fiber._session;
                if (_stage == 0)
                {
                    var arguments = LinearArgumentSource.ForHandler(handler, args);
                    if (!session.TryInitializeLinearHandlerFrame(handler, arguments))
                    {
                        fiber.Complete(BytecodeVmValue.Nothing, success: false);
                        return FrameSignal.Completed;
                    }

                    _stage = 1;
                    fiber.Push(new LinearRangeFrame(
                        handler.EntryAddress,
                        session._compiledScript.LinearExecutable.Code.Count,
                        arguments));
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
            }
        }
    }
}
