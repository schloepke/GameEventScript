using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
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
        var session = new GesBytecodeVmExecutionSession(compiledScript, context, handler.ExecutionPlan, handler.DiagnosticsEnabled);
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

        private void Fail()
        {
            while (_frames.Count > 0)
            {
                var frame = _frames.Pop();
                frame.Exit(this);
            }

            Failed = true;
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
            private bool _initialized;
            private bool _waitingForChild;

            public override FrameSignal Run(Fiber fiber)
            {
                var session = fiber._session;
                var code = session._compiledScript.LinearExecutable.Code;
                if (!_initialized)
                {
                    _initialized = true;
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
                        _pc = instruction.Target2;
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
                => fiber._session.UnwindLinearCallFrames(_callFrames);

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

                    case GameEventScriptBytecodeOpCode.SeededRandomBlock:
                        return TryCreateLinearSeededRandomBlockFrame(session, instruction, out frame);

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
                if (!session.TryGetLoopLayout(instruction.Data, out var loopLayout) ||
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

                frame = new LinearRangeLoopFrame(loopLayout.IdentifierSlot, from, to, step, instruction.Target, instruction.Target2);
                return true;
            }

            private static bool TryCreateLinearCollectionLoopFrame(
                GesBytecodeVmExecutionSession session,
                GameEventScriptBytecodeInstruction instruction,
                out Frame? frame)
            {
                frame = null;
                if (!session.TryGetLoopLayout(instruction.Data, out var loopLayout) ||
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

                frame = new LinearCollectionLoopFrame(loopLayout.IdentifierSlot, sourceValue.AsEnumerable().GetEnumerator(), instruction.Target, instruction.Target2);
                return true;
            }

            private static bool TryCreateLinearSeededRandomBlockFrame(
                GesBytecodeVmExecutionSession session,
                GameEventScriptBytecodeInstruction instruction,
                out Frame? frame)
            {
                frame = null;
                if (!session.TryGetSeededRandomBlockLayout(instruction.Data, out var layout))
                {
                    return false;
                }

                frame = new LinearSeededRandomBlockFrame(session.ResolveSlot(layout.SeedSlot).ToGameEventScriptValue(), instruction.Target, instruction.Target2);
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

        private sealed class LinearSeededRandomBlockFrame(
            GameEventScriptValue seed,
            int bodyStart,
            int bodyEnd) : Frame
        {
            private bool _pushed;
            private bool _waiting;
            private bool _scopePushed;

            public override FrameSignal Run(Fiber fiber)
            {
                if (!_pushed)
                {
                    fiber._session.PushSeededRandomScope(seed);
                    _scopePushed = true;
                    _pushed = true;
                    _waiting = true;
                    fiber.Push(new LinearRangeFrame(bodyStart, bodyEnd, null));
                    return FrameSignal.Running;
                }

                if (_waiting)
                {
                    if (!fiber.TryTakeResult(out _, out var success) || !success)
                    {
                        fiber.Complete(BytecodeVmValue.Nothing, success: false);
                        return FrameSignal.Completed;
                    }

                    _waiting = false;
                }

                fiber.Complete(BytecodeVmValue.Nothing);
                return FrameSignal.Completed;
            }

            public override void Exit(Fiber fiber)
            {
                if (_scopePushed)
                {
                    fiber._session.PopSeededRandomScope();
                    _scopePushed = false;
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
                    if (session.CanExecuteLinearFiberHandler(handler))
                    {
                        _stage = 1;
                        fiber.Push(new LinearRangeFrame(
                            handler.EntryAddress,
                            session._compiledScript.LinearExecutable.Code.Count,
                            LinearArgumentSource.ForHandler(handler, args)));
                        return FrameSignal.Running;
                    }

                    var parameters = handler.Parameters;
                    for (var parameterIndex = 0; parameterIndex < parameters.Count; parameterIndex++)
                    {
                        var parameter = parameters[parameterIndex];
                        if (!TryGetArgumentValue(args, parameter, parameterIndex, out var value))
                        {
                            fiber.Complete(BytecodeVmValue.Nothing, success: false);
                            return FrameSignal.Completed;
                        }

                        var parameterValue = BytecodeVmValue.FromGameEventScriptValue(value);
                        var hasParameterType = HasParameterType(handler.ParameterTypes, parameterIndex);
                        if (!session.TryConvertParameterType(handler.ParameterTypes, parameterIndex, parameterValue, out parameterValue))
                        {
                            fiber.Complete(BytecodeVmValue.Nothing, success: false);
                            return FrameSignal.Completed;
                        }

                        if (session._diagnosticsEnabled)
                        {
                            session.RecordParameterBound(parameter, hasParameterType ? parameterValue.ToGameEventScriptValue() : value);
                        }

                        if (!session.Define(parameter, parameterValue))
                        {
                            fiber.Complete(BytecodeVmValue.Nothing, success: false);
                            return FrameSignal.Completed;
                        }
                    }

                    session.RecordHandlerInvoked(handler.Message, args);
                    _stage = 1;
                    fiber.Push(new StatementProgramFrame(handler.ExecutionPlan.StatementProgram));
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

        private sealed class StatementProgramFrame(GameEventScriptBytecodeStatementProgram program) : Frame
        {
            private int _index;
            private bool _enteredScope;
            private bool _waitingForStatement;

            public override FrameSignal Run(Fiber fiber)
            {
                var session = fiber._session;
                if (!_enteredScope && program.CreatesScope)
                {
                    session.EnterScope();
                    _enteredScope = true;
                }

                if (_waitingForStatement)
                {
                    if (!fiber.TryTakeResult(out _, out var success) || !success)
                    {
                        fiber.Complete(BytecodeVmValue.Nothing, success: false);
                        return FrameSignal.Completed;
                    }

                    _waitingForStatement = false;
                }

                if (_index >= program.Statements.Length)
                {
                    fiber.Complete(BytecodeVmValue.Nothing);
                    return FrameSignal.Completed;
                }

                var statement = program.Statements[_index++];
                _waitingForStatement = true;
                fiber.Push(new StatementFrame(statement));
                return FrameSignal.Running;
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

        private sealed class StatementFrame(GameEventScriptBytecodeStatement statement) : Frame
        {
            private int _stage;
            private BytecodeVmValue _first;
            private BytecodeVmValue _second;
            private long _rangeFrom;
            private long _rangeTo;
            private long _rangeStep = 1L;
            private long _current;
            private long _to;
            private long _step;
            private GameEventScriptValue[]? _collectionItems;
            private int _collectionIndex;
            private KeyValuePair<string, GameEventScriptValue>[]? _publishPairs;
            private GameEventScriptMessage? _publishMessage;
            private GesBytecodeVmExecutionSession.MessageTagBuilder? _publishTags;

            public override FrameSignal Run(Fiber fiber)
            {
                return statement.Kind switch
                {
                    GameEventScriptBytecodeStatementKind.Let => RunLet(fiber),
                    GameEventScriptBytecodeStatementKind.Publish => RunPublish(fiber),
                    GameEventScriptBytecodeStatementKind.If => RunIf(fiber),
                    GameEventScriptBytecodeStatementKind.ForRange => RunForRange(fiber),
                    GameEventScriptBytecodeStatementKind.ForCollection => RunForCollection(fiber),
                    GameEventScriptBytecodeStatementKind.Expression => RunExpression(fiber),
                    GameEventScriptBytecodeStatementKind.SeededRandom => RunSeededRandom(fiber),
                    _ => CompleteFailure(fiber)
                };
            }

            private FrameSignal RunLet(Fiber fiber)
            {
                var session = fiber._session;
                if (_stage == 0)
                {
                    if (!fiber.TryConsumeInstruction("Statement execution budget exhausted."))
                    {
                        return FrameSignal.Paused;
                    }

                    if (statement.ExpressionProgram is null || string.IsNullOrEmpty(statement.Name))
                    {
                        return CompleteFailure(fiber);
                    }

                    _stage = 1;
                    fiber.Push(ExpressionFrame.Create(fiber._session, statement.ExpressionProgram, 0));
                    return FrameSignal.Running;
                }

                if (!fiber.TryTakeResult(out var letValue, out var success) || !success)
                {
                    return CompleteFailure(fiber);
                }

                if (!string.IsNullOrEmpty(statement.DeclaredType) &&
                    !session.TryConvertDeclaredType(statement.DeclaredType!, letValue, out letValue))
                {
                    return CompleteFailure(fiber);
                }

                session.RecordLetEvaluated(statement.Name!, letValue);
                session.RecordLetExpressionEvaluatedToNothing(statement.Name!, letValue);
                fiber.Complete(BytecodeVmValue.Nothing, session.Define(statement.Name!, letValue));
                return FrameSignal.Completed;
            }

            private FrameSignal RunPublish(Fiber fiber)
            {
                var session = fiber._session;
                if (_stage == 0)
                {
                    if (!fiber.TryConsumeInstruction("Statement execution budget exhausted."))
                    {
                        return FrameSignal.Paused;
                    }

                    if (statement.PublishLayout is not null)
                    {
                        var layout = statement.PublishLayout;
                        if (layout.ArgumentNames.Length == 0)
                        {
                            _publishMessage = GameEventScriptMessage.CreatePrecomputed(
                                layout.MessageName,
                                GameEventScriptNamedArguments.Empty,
                                layout.SignatureId);
                            return StartPublishTagsOrDispatch(fiber);
                        }

                        _publishPairs = new KeyValuePair<string, GameEventScriptValue>[layout.ArgumentNames.Length];
                        _stage = 1;
                        fiber.Push(ExpressionFrame.Create(fiber._session, layout.ArgumentPrograms[0], 0));
                        return FrameSignal.Running;
                    }

                    if (statement.ExpressionProgram is null)
                    {
                        return CompleteFailure(fiber);
                    }

                    _stage = 1000;
                    fiber.Push(ExpressionFrame.Create(fiber._session, statement.ExpressionProgram, 0));
                    return FrameSignal.Running;
                }

                if (_stage == 1000)
                {
                    if (!fiber.TryTakeResult(out var publishValue, out var success) || !success)
                    {
                        return CompleteFailure(fiber);
                    }

                    var boxed = publishValue.ToGameEventScriptValue();
                    if (GesMessageValueCodec.TryReadMessageValue(boxed, out var message))
                    {
                        _publishMessage = message;
                        return StartPublishTagsOrDispatch(fiber);
                    }

                    fiber.Complete(BytecodeVmValue.Nothing);
                    return FrameSignal.Completed;
                }

                if (_stage >= 2000)
                {
                    return ContinuePublishTags(fiber);
                }

                var publishLayout = statement.PublishLayout!;
                if (!fiber.TryTakeResult(out var argumentValue, out var argumentSuccess) || !argumentSuccess)
                {
                    return CompleteFailure(fiber);
                }

                var argumentIndex = _stage - 1;
                session.RecordPublishArgumentEvaluatedToNothing(publishLayout.ArgumentNames[argumentIndex], argumentValue);
                _publishPairs![argumentIndex] = new KeyValuePair<string, GameEventScriptValue>(
                    publishLayout.ArgumentNames[argumentIndex],
                    argumentValue.ToGameEventScriptValue());
                argumentIndex++;
                if (argumentIndex < publishLayout.ArgumentNames.Length)
                {
                    _stage = argumentIndex + 1;
                    fiber.Push(ExpressionFrame.Create(fiber._session, publishLayout.ArgumentPrograms[argumentIndex], 0));
                    return FrameSignal.Running;
                }

                _publishMessage = GameEventScriptMessage.CreatePrecomputed(
                    publishLayout.MessageName,
                    GameEventScriptNamedArguments.CreateOrdered(_publishPairs),
                    publishLayout.SignatureId);
                return StartPublishTagsOrDispatch(fiber);
            }

            private FrameSignal StartPublishTagsOrDispatch(Fiber fiber)
            {
                if (statement.TagPrograms.Length == 0)
                {
                    return DispatchPublishedMessage(fiber);
                }

                _publishTags = new GesBytecodeVmExecutionSession.MessageTagBuilder();
                _stage = 2000;
                fiber.Push(ExpressionFrame.Create(fiber._session, statement.TagPrograms[0], 0));
                return FrameSignal.Running;
            }

            private FrameSignal ContinuePublishTags(Fiber fiber)
            {
                if (!fiber.TryTakeResult(out var tagValue, out var tagSuccess) || !tagSuccess)
                {
                    return CompleteFailure(fiber);
                }

                GesBytecodeVmExecutionSession.AddTags(_publishTags!, tagValue.ToGameEventScriptValue());
                var tagIndex = _stage - 2000 + 1;
                if (tagIndex < statement.TagPrograms.Length)
                {
                    _stage = 2000 + tagIndex;
                    fiber.Push(ExpressionFrame.Create(fiber._session, statement.TagPrograms[tagIndex], 0));
                    return FrameSignal.Running;
                }

                return DispatchPublishedMessage(fiber);
            }

            private FrameSignal DispatchPublishedMessage(Fiber fiber)
            {
                var session = fiber._session;
                var message = _publishMessage;
                if (message is not null)
                {
                    var tags = _publishTags?.ToArray() ?? [];
                    session.PublishMessage(statement.PublishKind, tags.Count == 0 ? message : message.WithTags(tags));
                }

                fiber.Complete(BytecodeVmValue.Nothing);
                return FrameSignal.Completed;
            }

            private FrameSignal RunIf(Fiber fiber)
            {
                if (_stage == 0)
                {
                    if (!fiber.TryConsumeInstruction("Statement execution budget exhausted."))
                    {
                        return FrameSignal.Paused;
                    }

                    if (statement.ExpressionProgram is null || statement.ThenProgram is null)
                    {
                        return CompleteFailure(fiber);
                    }

                    _stage = 1;
                    fiber.Push(ExpressionFrame.Create(fiber._session, statement.ExpressionProgram, 0));
                    return FrameSignal.Running;
                }

                if (_stage == 1)
                {
                    if (!fiber.TryTakeResult(out var condition, out var success) || !success)
                    {
                        return CompleteFailure(fiber);
                    }

                    var branch = condition.IsTrue()
                        ? statement.ThenProgram
                        : statement.ElseProgram;
                    if (branch is null)
                    {
                        fiber.Complete(BytecodeVmValue.Nothing);
                        return FrameSignal.Completed;
                    }

                    _stage = 2;
                    fiber.Push(new StatementProgramFrame(branch));
                    return FrameSignal.Running;
                }

                if (!fiber.TryTakeResult(out _, out var branchSuccess) || !branchSuccess)
                {
                    return CompleteFailure(fiber);
                }

                fiber.Complete(BytecodeVmValue.Nothing);
                return FrameSignal.Completed;
            }

            private FrameSignal RunForRange(Fiber fiber)
            {
                var session = fiber._session;
                var source = statement.IterationSource;
                if (_stage == 0)
                {
                    if (!fiber.TryConsumeInstruction("Statement execution budget exhausted."))
                    {
                        return FrameSignal.Paused;
                    }

                    if (source?.RangeFromProgram is null || source.RangeToProgram is null)
                    {
                        return CompleteFailure(fiber);
                    }

                    _stage = 1;
                    fiber.Push(ExpressionFrame.Create(fiber._session, source.RangeFromProgram, 0));
                    return FrameSignal.Running;
                }

                if (_stage == 1)
                {
                    if (!fiber.TryTakeResult(out _first, out var success) || !success ||
                        !_first.TryGetRangeInteger(out _rangeFrom))
                    {
                        return CompleteFailure(fiber);
                    }

                    _stage = 2;
                    fiber.Push(ExpressionFrame.Create(fiber._session, source!.RangeToProgram!, 0));
                    return FrameSignal.Running;
                }

                if (_stage == 2)
                {
                    if (!fiber.TryTakeResult(out _second, out var success) || !success ||
                        !_second.TryGetRangeInteger(out _rangeTo))
                    {
                        return CompleteFailure(fiber);
                    }

                    if (source!.RangeStepProgram is not null)
                    {
                        _stage = 3;
                        fiber.Push(ExpressionFrame.Create(fiber._session, source.RangeStepProgram, 0));
                        return FrameSignal.Running;
                    }

                    return InitializeRangeLoop(fiber);
                }

                if (_stage == 3)
                {
                    if (!fiber.TryTakeResult(out var stepValue, out var success) || !success ||
                        !stepValue.TryGetRangeInteger(out _rangeStep))
                    {
                        return CompleteFailure(fiber);
                    }

                    return InitializeRangeLoop(fiber);
                }

                if (_stage == 5)
                {
                    if (!fiber.TryTakeResult(out _, out var bodySuccess) || !bodySuccess)
                    {
                        return CompleteFailure(fiber);
                    }

                    if (_step > 0)
                    {
                        if (long.MaxValue - _current < _step)
                        {
                            fiber.Complete(BytecodeVmValue.Nothing);
                            return FrameSignal.Completed;
                        }
                    }
                    else if (long.MinValue - _current > _step)
                    {
                        fiber.Complete(BytecodeVmValue.Nothing);
                        return FrameSignal.Completed;
                    }

                    _current += _step;
                    _stage = 4;
                }

                if (_step > 0 ? _current > _to : _current < _to)
                {
                    fiber.Complete(BytecodeVmValue.Nothing);
                    return FrameSignal.Completed;
                }

                if (!fiber.TryConsumeInstruction("Loop iteration budget exhausted."))
                {
                    return FrameSignal.Paused;
                }

                if (!session._runtimeBudget.TryConsumeLoopIteration("Loop iteration budget exhausted."))
                {
                    fiber.Halt();
                    return FrameSignal.Paused;
                }

                if (string.IsNullOrEmpty(statement.Name) || statement.BodyProgram is null)
                {
                    return CompleteFailure(fiber);
                }

                _stage = 5;
                fiber.Push(new LoopIterationFrame(statement.Name!, BytecodeVmValue.Integer(_current), statement.BodyProgram));
                return FrameSignal.Running;
            }

            private FrameSignal InitializeRangeLoop(Fiber fiber)
            {
                var session = fiber._session;
                var from = _rangeFrom;
                var to = _rangeTo;
                var step = _rangeStep;
                if (step == 0)
                {
                    fiber.Complete(BytecodeVmValue.Nothing);
                    return FrameSignal.Completed;
                }

                var length = GesRuntimeLimitUtilities.GetRangeLength(from, to, step);
                if (!session._runtimeBudget.TryCheckRangeLength(length, "For loop range would enumerate more range items than allowed."))
                {
                    fiber.Complete(BytecodeVmValue.Nothing);
                    return FrameSignal.Completed;
                }

                _current = from;
                _to = to;
                _step = step;
                _stage = 4;
                return FrameSignal.Running;
            }

            private FrameSignal RunForCollection(Fiber fiber)
            {
                var session = fiber._session;
                if (_stage == 0)
                {
                    if (!fiber.TryConsumeInstruction("Statement execution budget exhausted."))
                    {
                        return FrameSignal.Paused;
                    }

                    var source = statement.IterationSource;
                    if (source?.CollectionProgram is null)
                    {
                        return CompleteFailure(fiber);
                    }

                    _stage = 1;
                    fiber.Push(ExpressionFrame.Create(fiber._session, source.CollectionProgram, 0));
                    return FrameSignal.Running;
                }

                if (_stage == 1)
                {
                    if (!fiber.TryTakeResult(out var sourceValue, out var success) || !success)
                    {
                        return CompleteFailure(fiber);
                    }

                    var boxed = sourceValue.ToGameEventScriptValue();
                    if (GesRuntimeLimitUtilities.TryGetRangeLength(boxed, out var length) &&
                        !session._runtimeBudget.TryCheckRangeLength(length, "Iteration source would enumerate more range items than allowed."))
                    {
                        fiber.Complete(BytecodeVmValue.Nothing);
                        return FrameSignal.Completed;
                    }

                    _collectionItems = boxed.AsEnumerable().ToArray();
                    _collectionIndex = 0;
                    _stage = 2;
                }

                if (_stage == 3)
                {
                    if (!fiber.TryTakeResult(out _, out var bodySuccess) || !bodySuccess)
                    {
                        return CompleteFailure(fiber);
                    }

                    _stage = 2;
                }

                if (_collectionItems is null || _collectionIndex >= _collectionItems.Length)
                {
                    fiber.Complete(BytecodeVmValue.Nothing);
                    return FrameSignal.Completed;
                }

                if (!fiber.TryConsumeInstruction("Loop iteration budget exhausted."))
                {
                    return FrameSignal.Paused;
                }

                if (!session._runtimeBudget.TryConsumeLoopIteration("Loop iteration budget exhausted."))
                {
                    fiber.Halt();
                    return FrameSignal.Paused;
                }

                if (string.IsNullOrEmpty(statement.Name) || statement.BodyProgram is null)
                {
                    return CompleteFailure(fiber);
                }

                var item = BytecodeVmValue.FromGameEventScriptValue(_collectionItems[_collectionIndex++]);
                _stage = 3;
                fiber.Push(new LoopIterationFrame(statement.Name!, item, statement.BodyProgram));
                return FrameSignal.Running;
            }

            private FrameSignal RunExpression(Fiber fiber)
            {
                var session = fiber._session;
                if (_stage == 0)
                {
                    if (!fiber.TryConsumeInstruction("Statement execution budget exhausted."))
                    {
                        return FrameSignal.Paused;
                    }

                    if (statement.ExpressionProgram is null)
                    {
                        return CompleteFailure(fiber);
                    }

                    _stage = 1;
                    fiber.Push(ExpressionFrame.Create(fiber._session, statement.ExpressionProgram, 0));
                    return FrameSignal.Running;
                }

                if (!fiber.TryTakeResult(out var expressionValue, out var success) || !success)
                {
                    return CompleteFailure(fiber);
                }

                session.RecordExpressionStatementEvaluatedToNothing(statement.DiagnosticName, expressionValue);
                fiber.Complete(BytecodeVmValue.Nothing);
                return FrameSignal.Completed;
            }

            private FrameSignal RunSeededRandom(Fiber fiber)
            {
                var session = fiber._session;
                if (_stage == 0)
                {
                    if (!fiber.TryConsumeInstruction("Statement execution budget exhausted."))
                    {
                        return FrameSignal.Paused;
                    }

                    if (statement.ExpressionProgram is null || statement.BodyProgram is null)
                    {
                        return CompleteFailure(fiber);
                    }

                    _stage = 1;
                    fiber.Push(ExpressionFrame.Create(fiber._session, statement.ExpressionProgram, 0));
                    return FrameSignal.Running;
                }

                if (_stage == 1)
                {
                    if (!fiber.TryTakeResult(out var seed, out var success) || !success)
                    {
                        return CompleteFailure(fiber);
                    }

                    session.PushSeededRandomScope(seed.ToGameEventScriptValue());
                    _stage = 2;
                    fiber.Push(new SeededRandomBodyFrame(statement.BodyProgram!));
                    return FrameSignal.Running;
                }

                if (!fiber.TryTakeResult(out _, out var bodySuccess) || !bodySuccess)
                {
                    return CompleteFailure(fiber);
                }

                fiber.Complete(BytecodeVmValue.Nothing);
                return FrameSignal.Completed;
            }

            private static FrameSignal CompleteFailure(Fiber fiber)
            {
                fiber.Complete(BytecodeVmValue.Nothing, success: false);
                return FrameSignal.Completed;
            }
        }

        private sealed class LoopIterationFrame(
            string identifier,
            BytecodeVmValue item,
            GameEventScriptBytecodeStatementProgram body) : Frame
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
                    if (!session.Define(identifier, item))
                    {
                        fiber.Complete(BytecodeVmValue.Nothing, success: false);
                        return FrameSignal.Completed;
                    }

                    _stage = 1;
                    fiber.Push(new StatementProgramFrame(body));
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

        private sealed class SeededRandomBodyFrame(GameEventScriptBytecodeStatementProgram body) : Frame
        {
            private bool _pushed;
            private bool _waiting;

            public override FrameSignal Run(Fiber fiber)
            {
                if (!_pushed)
                {
                    _pushed = true;
                    _waiting = true;
                    fiber.Push(new StatementProgramFrame(body));
                    return FrameSignal.Running;
                }

                if (_waiting)
                {
                    if (!fiber.TryTakeResult(out _, out var success) || !success)
                    {
                        fiber.Complete(BytecodeVmValue.Nothing, success: false);
                        return FrameSignal.Completed;
                    }

                    _waiting = false;
                }

                fiber.Complete(BytecodeVmValue.Nothing);
                return FrameSignal.Completed;
            }

            public override void Exit(Fiber fiber)
            {
                fiber._session.PopSeededRandomScope();
            }
        }

        private sealed class LinearHelperExpressionFrame : Frame
        {
            private readonly int _entryAddress;
            private readonly int _temporaryBaseSlot;
            private LinearArgumentSource? _arguments;
            private List<LinearCallFrame>? _callFrames;
            private BytecodeVmValue[]? _previousValues;
            private bool[]? _previousAssignedSlots;
            private bool _previousSuppressLocalChangeTracking;
            private int _temporarySlotCount;
            private int _pc;
            private int _endAddress;
            private bool _initialized;

            public LinearHelperExpressionFrame(int entryAddress, int temporaryBaseSlot)
            {
                _entryAddress = entryAddress;
                _temporaryBaseSlot = temporaryBaseSlot;
                _pc = entryAddress;
            }

            public override FrameSignal Run(Fiber fiber)
            {
                var session = fiber._session;
                var code = session._compiledScript.LinearExecutable.Code;
                if (!_initialized)
                {
                    _initialized = true;
                    _endAddress = code.Count;
                    if ((uint)_entryAddress >= (uint)code.Count ||
                        _temporaryBaseSlot < 0 ||
                        _temporaryBaseSlot >= session._locals.Length)
                    {
                        fiber.Complete(BytecodeVmValue.Nothing, success: false);
                        return FrameSignal.Completed;
                    }

                    _temporarySlotCount = session._locals.Length - _temporaryBaseSlot;
                    _previousValues = ArrayPool<BytecodeVmValue>.Shared.Rent(_temporarySlotCount);
                    _previousAssignedSlots = ArrayPool<bool>.Shared.Rent(_temporarySlotCount);
                    for (var index = 0; index < _temporarySlotCount; index++)
                    {
                        var slot = _temporaryBaseSlot + index;
                        _previousValues[index] = session._locals[slot];
                        _previousAssignedSlots[index] = session._assignedSlots[slot];
                    }

                    _previousSuppressLocalChangeTracking = session._suppressLocalChangeTracking;
                    session._suppressLocalChangeTracking = true;
                }

                while (_pc < _endAddress)
                {
                    if (!fiber.TryConsumeInstruction("Linear expression helper execution budget exhausted."))
                    {
                        return FrameSignal.Paused;
                    }

                    var instruction = code[_pc];
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

                fiber.Complete(BytecodeVmValue.Nothing);
                return FrameSignal.Completed;
            }

            public override void Exit(Fiber fiber)
            {
                var session = fiber._session;
                session.UnwindLinearCallFrames(_callFrames);
                if (!_initialized)
                {
                    return;
                }

                session._suppressLocalChangeTracking = _previousSuppressLocalChangeTracking;
                if (_previousValues is not null && _previousAssignedSlots is not null)
                {
                    for (var index = 0; index < _temporarySlotCount; index++)
                    {
                        var slot = _temporaryBaseSlot + index;
                        session._locals[slot] = _previousValues[index];
                        session._assignedSlots[slot] = _previousAssignedSlots[index];
                    }

                    Array.Clear(_previousValues, 0, _temporarySlotCount);
                    ArrayPool<BytecodeVmValue>.Shared.Return(_previousValues);
                    Array.Clear(_previousAssignedSlots, 0, _temporarySlotCount);
                    ArrayPool<bool>.Shared.Return(_previousAssignedSlots);
                }
            }
        }

        private sealed class ExpressionFrame : Frame
        {
            private readonly GameEventScriptBytecodeExpressionProgram _program;
            private readonly int _stackBase;
            private GesLinearExpressionProgram? _linear;
            private BytecodeVmValue[]? _slots;
            private BytecodeVmValue[]? _operandBuffer;
            private int _pc;
            private bool _initialized;

            public static Frame Create(
                GesBytecodeVmExecutionSession session,
                GameEventScriptBytecodeExpressionProgram program,
                int stackBase)
            {
                if (stackBase == 0 &&
                    program.LinearTemporaryBaseSlot >= 0 &&
                    session.CanExecuteLinearEntryCached(program.LinearEntryAddress))
                {
                    return new LinearHelperExpressionFrame(program.LinearEntryAddress, program.LinearTemporaryBaseSlot);
                }

                return new ExpressionFrame(program, stackBase);
            }

            private ExpressionFrame(GameEventScriptBytecodeExpressionProgram program, int stackBase)
            {
                _program = program;
                _stackBase = stackBase;
            }

            public override FrameSignal Run(Fiber fiber)
            {
                var session = fiber._session;
                if (!_initialized)
                {
                    _initialized = true;
                    _linear = GesLinearExpressionProgram.GetOrCreate(_program);
                    _slots = ArrayPool<BytecodeVmValue>.Shared.Rent(_linear.MaxSlots);
                    Array.Clear(_slots, 0, _linear.MaxSlots);
                    _operandBuffer = _linear.MaxOperandCount > 0
                        ? ArrayPool<BytecodeVmValue>.Shared.Rent(_linear.MaxOperandCount)
                        : [];
                }

                if (_linear is null || _slots is null || _operandBuffer is null)
                {
                    fiber.Complete(BytecodeVmValue.Nothing, success: false);
                    return FrameSignal.Completed;
                }

                var instructions = _linear.Instructions;
                while (_pc < instructions.Length)
                {
                    if (!fiber.TryConsumeInstruction("Expression evaluation budget exhausted."))
                    {
                        return FrameSignal.Paused;
                    }

                    if (!session.TryExecuteCompatibilityLinearExpressionInstruction(
                            in instructions[_pc],
                            _slots,
                            _operandBuffer,
                            _stackBase,
                            ref _pc,
                            instructions.Length))
                    {
                        fiber.Complete(BytecodeVmValue.Nothing, success: false);
                        return FrameSignal.Completed;
                    }

                    if (session._halted)
                    {
                        fiber.Halt();
                        return FrameSignal.Paused;
                    }
                }

                fiber.Complete(_linear.ReturnSlot >= 0
                    ? ReadExpressionSlot(_slots, _linear.ReturnSlot)
                    : BytecodeVmValue.Nothing);
                return FrameSignal.Completed;
            }

            public override void Exit(Fiber fiber)
            {
                if (_linear is null)
                {
                    return;
                }

                if (_slots is not null)
                {
                    Array.Clear(_slots, 0, _linear.MaxSlots);
                    ArrayPool<BytecodeVmValue>.Shared.Return(_slots);
                    _slots = null;
                }

                if (_operandBuffer is not null && _linear.MaxOperandCount > 0)
                {
                    Array.Clear(_operandBuffer, 0, _linear.MaxOperandCount);
                    ArrayPool<BytecodeVmValue>.Shared.Return(_operandBuffer);
                    _operandBuffer = null;
                }
            }
        }
}
}
