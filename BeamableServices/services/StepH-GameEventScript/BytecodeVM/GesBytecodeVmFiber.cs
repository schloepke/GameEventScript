using System;
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
            private decimal _rangeFrom;
            private decimal _rangeTo;
            private decimal _rangeStep = 1m;
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
                    fiber.Push(new ExpressionFrame(statement.ExpressionProgram, 0));
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
                        fiber.Push(new ExpressionFrame(layout.ArgumentPrograms[0], 0));
                        return FrameSignal.Running;
                    }

                    if (statement.ExpressionProgram is null)
                    {
                        return CompleteFailure(fiber);
                    }

                    _stage = 1000;
                    fiber.Push(new ExpressionFrame(statement.ExpressionProgram, 0));
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
                    fiber.Push(new ExpressionFrame(publishLayout.ArgumentPrograms[argumentIndex], 0));
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
                fiber.Push(new ExpressionFrame(statement.TagPrograms[0], 0));
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
                    fiber.Push(new ExpressionFrame(statement.TagPrograms[tagIndex], 0));
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
                    fiber.Push(new ExpressionFrame(statement.ExpressionProgram, 0));
                    return FrameSignal.Running;
                }

                if (_stage == 1)
                {
                    if (!fiber.TryTakeResult(out var condition, out var success) || !success)
                    {
                        return CompleteFailure(fiber);
                    }

                    var branch = condition.AsBoolean() ? statement.ThenProgram : statement.ElseProgram;
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
                    fiber.Push(new ExpressionFrame(source.RangeFromProgram, 0));
                    return FrameSignal.Running;
                }

                if (_stage == 1)
                {
                    if (!fiber.TryTakeResult(out _first, out var success) || !success ||
                        !_first.TryGetFiniteNumber(out _rangeFrom))
                    {
                        return CompleteFailure(fiber);
                    }

                    _stage = 2;
                    fiber.Push(new ExpressionFrame(source!.RangeToProgram!, 0));
                    return FrameSignal.Running;
                }

                if (_stage == 2)
                {
                    if (!fiber.TryTakeResult(out _second, out var success) || !success ||
                        !_second.TryGetFiniteNumber(out _rangeTo))
                    {
                        return CompleteFailure(fiber);
                    }

                    if (source!.RangeStepProgram is not null)
                    {
                        _stage = 3;
                        fiber.Push(new ExpressionFrame(source.RangeStepProgram, 0));
                        return FrameSignal.Running;
                    }

                    return InitializeRangeLoop(fiber);
                }

                if (_stage == 3)
                {
                    if (!fiber.TryTakeResult(out var stepValue, out var success) || !success ||
                        !stepValue.TryGetFiniteNumber(out _rangeStep))
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
                var from = ToLongSaturated(_rangeFrom);
                var to = ToLongSaturated(_rangeTo);
                var step = ToLongSaturated(_rangeStep);
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
                    fiber.Push(new ExpressionFrame(source.CollectionProgram, 0));
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
                    fiber.Push(new ExpressionFrame(statement.ExpressionProgram, 0));
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
                    fiber.Push(new ExpressionFrame(statement.ExpressionProgram, 0));
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

        private sealed class ExpressionFrame : Frame
        {
            private readonly GameEventScriptBytecodeExpressionProgram _program;
            private readonly int _stackBase;
            private int _instructionIndex;
            private int _top;
            private bool _initialized;

            public ExpressionFrame(GameEventScriptBytecodeExpressionProgram program, int stackBase)
            {
                _program = program;
                _stackBase = stackBase;
                _top = stackBase;
            }

            public override FrameSignal Run(Fiber fiber)
            {
                var session = fiber._session;
                if (!_initialized)
                {
                    _initialized = true;
                    if (_stackBase + _program.MaxStackDepth > session._evaluationStack.Length)
                    {
                        fiber.Complete(BytecodeVmValue.Nothing, success: false);
                        return FrameSignal.Completed;
                    }
                }

                if (_instructionIndex >= _program.Instructions.Length)
                {
                    fiber.Complete(_top > _stackBase ? session._evaluationStack[_top - 1] : BytecodeVmValue.Nothing);
                    return FrameSignal.Completed;
                }

                if (!fiber.TryConsumeInstruction("Expression evaluation budget exhausted."))
                {
                    return FrameSignal.Paused;
                }

                ref readonly var instruction = ref _program.Instructions[_instructionIndex++];
                if (!ExecuteInstruction(fiber, in instruction))
                {
                    return FrameSignal.Completed;
                }

                if (session._halted)
                {
                    fiber.Halt();
                    return FrameSignal.Paused;
                }

                return FrameSignal.Running;
            }

            private bool ExecuteInstruction(Fiber fiber, in GameEventScriptBytecodeInstruction instruction)
            {
                var session = fiber._session;
                switch (instruction.OpCode)
                {
                    case GameEventScriptBytecodeOpCode.LoadConstant:
                        session._evaluationStack[_top++] = session.LoadConstant(instruction.ConstantIndex);
                        return true;

                    case GameEventScriptBytecodeOpCode.LoadSlot:
                        session._evaluationStack[_top++] = session.ResolveSlot(instruction.A);
                        return true;

                    case GameEventScriptBytecodeOpCode.Or:
                    case GameEventScriptBytecodeOpCode.Xor:
                    case GameEventScriptBytecodeOpCode.And:
                    case GameEventScriptBytecodeOpCode.Power:
                    case GameEventScriptBytecodeOpCode.Equal:
                    case GameEventScriptBytecodeOpCode.NotEqual:
                    case GameEventScriptBytecodeOpCode.Less:
                    case GameEventScriptBytecodeOpCode.Greater:
                    case GameEventScriptBytecodeOpCode.LessOrEqual:
                    case GameEventScriptBytecodeOpCode.GreaterOrEqual:
                    case GameEventScriptBytecodeOpCode.Add:
                    case GameEventScriptBytecodeOpCode.Subtract:
                    case GameEventScriptBytecodeOpCode.Multiply:
                    case GameEventScriptBytecodeOpCode.Divide:
                    case GameEventScriptBytecodeOpCode.IntegerDivide:
                    case GameEventScriptBytecodeOpCode.Modulo:
                    case GameEventScriptBytecodeOpCode.Remainder:
                    case GameEventScriptBytecodeOpCode.PrimitiveIntegerEqual:
                    case GameEventScriptBytecodeOpCode.PrimitiveIntegerNotEqual:
                    case GameEventScriptBytecodeOpCode.PrimitiveIntegerLess:
                    case GameEventScriptBytecodeOpCode.PrimitiveIntegerGreater:
                    case GameEventScriptBytecodeOpCode.PrimitiveIntegerLessOrEqual:
                    case GameEventScriptBytecodeOpCode.PrimitiveIntegerGreaterOrEqual:
                    case GameEventScriptBytecodeOpCode.PrimitiveIntegerAdd:
                    case GameEventScriptBytecodeOpCode.PrimitiveIntegerSubtract:
                    case GameEventScriptBytecodeOpCode.PrimitiveIntegerMultiply:
                    case GameEventScriptBytecodeOpCode.PrimitiveIntegerDivide:
                    case GameEventScriptBytecodeOpCode.PrimitiveIntegerFloorDivide:
                    case GameEventScriptBytecodeOpCode.PrimitiveIntegerModulo:
                    case GameEventScriptBytecodeOpCode.PrimitiveIntegerRemainder:
                    case GameEventScriptBytecodeOpCode.Default:
                    case GameEventScriptBytecodeOpCode.Contains:
                    case GameEventScriptBytecodeOpCode.ContainsValue:
                    case GameEventScriptBytecodeOpCode.StartsWith:
                    case GameEventScriptBytecodeOpCode.EndsWith:
                    case GameEventScriptBytecodeOpCode.Intersect:
                    case GameEventScriptBytecodeOpCode.Combine:
                    case GameEventScriptBytecodeOpCode.Except:
                    case GameEventScriptBytecodeOpCode.Zip:
                        var right = session._evaluationStack[--_top];
                        var left = session._evaluationStack[--_top];
                        session._evaluationStack[_top++] = session.EvaluateProgramBinary(instruction.OpCode, left, right);
                        return true;

                    case GameEventScriptBytecodeOpCode.Unary:
                        if (!session.TryEvaluateUnaryOperation(instruction.DiagnosticName, session._evaluationStack[_top - 1], out var unaryValue))
                        {
                            fiber.Complete(BytecodeVmValue.Nothing, success: false);
                            return false;
                        }

                        session._evaluationStack[_top - 1] = unaryValue;
                        return true;

                    case GameEventScriptBytecodeOpCode.Variadic:
                        _top -= instruction.A;
                        if (!session.TryEvaluateVariadicOperation(instruction.DiagnosticName, session._evaluationStack, _top, instruction.A, out var variadicValue))
                        {
                            fiber.Complete(BytecodeVmValue.Nothing, success: false);
                            return false;
                        }

                        session._evaluationStack[_top++] = variadicValue;
                        return true;

                    case GameEventScriptBytecodeOpCode.Clamp:
                        _top -= 3;
                        session._evaluationStack[_top] = EvaluateClamp(
                            session._evaluationStack[_top],
                            session._evaluationStack[_top + 1],
                            session._evaluationStack[_top + 2]);
                        _top++;
                        return true;

                    case GameEventScriptBytecodeOpCode.Random:
                        var to = session._evaluationStack[--_top];
                        var from = session._evaluationStack[--_top];
                        session._evaluationStack[_top++] = session.EvaluateRandomExpression(from, to);
                        return true;

                    case GameEventScriptBytecodeOpCode.Range:
                        _top -= instruction.A;
                        session._evaluationStack[_top] = EvaluateRangeExpression(
                            session._evaluationStack[_top],
                            session._evaluationStack[_top + 1],
                            instruction.A == 3 ? session._evaluationStack[_top + 2] : BytecodeVmValue.Integer(1));
                        _top++;
                        return true;

                    case GameEventScriptBytecodeOpCode.Dice:
                        session._evaluationStack[_top++] = session.EvaluateDiceExpression(instruction.A, instruction.B);
                        return true;

                    case GameEventScriptBytecodeOpCode.SeededRandom:
                        var seed = session._evaluationStack[--_top];
                        if (instruction.ExpressionProgram is null ||
                            !session.TryEvaluateSeededRandomExpression(seed, instruction.ExpressionProgram, _top, out var seededValue))
                        {
                            fiber.Complete(BytecodeVmValue.Nothing, success: false);
                            return false;
                        }

                        session._evaluationStack[_top++] = seededValue;
                        return true;

                    case GameEventScriptBytecodeOpCode.Cast:
                        session._evaluationStack[_top - 1] = session.EvaluateProgramCast(instruction.CastKind, session._evaluationStack[_top - 1]);
                        return true;

                    case GameEventScriptBytecodeOpCode.TypeConstructor:
                        _top -= instruction.A;
                        session._evaluationStack[_top] = session.EvaluateTypeConstructor(
                            instruction.DiagnosticName,
                            instruction.Names,
                            session._evaluationStack,
                            _top,
                            instruction.A);
                        _top++;
                        return true;

                    case GameEventScriptBytecodeOpCode.TypeCheck:
                        session._evaluationStack[_top - 1] = BytecodeVmValue.Boolean(IsValueOfType(
                            session._evaluationStack[_top - 1],
                            instruction.DiagnosticName));
                        return true;

                    case GameEventScriptBytecodeOpCode.RulePredicate:
                        var input = session._evaluationStack[--_top];
                        if (!session.TryEvaluateRulePredicate(in instruction, input, _top, out var predicateValue))
                        {
                            fiber.Complete(BytecodeVmValue.Nothing, success: false);
                            return false;
                        }

                        session._evaluationStack[_top++] = predicateValue;
                        return true;

                    case GameEventScriptBytecodeOpCode.Call:
                        _top -= instruction.A;
                        if (!session.TryEvaluateCallable(in instruction, session._evaluationStack, _top, instruction.A, out var callValue))
                        {
                            fiber.Complete(BytecodeVmValue.Nothing, success: false);
                            return false;
                        }

                        session._evaluationStack[_top++] = callValue;
                        return true;

                    case GameEventScriptBytecodeOpCode.MemberAccess:
                        session._evaluationStack[_top - 1] = EvaluateMemberAccess(session._evaluationStack[_top - 1], instruction.DiagnosticName);
                        return true;

                    case GameEventScriptBytecodeOpCode.IndexedAccess:
                        var selector = session._evaluationStack[--_top];
                        var target = session._evaluationStack[--_top];
                        session._evaluationStack[_top++] = EvaluateIndexedAccess(target, selector);
                        return true;

                    case GameEventScriptBytecodeOpCode.BuildList:
                        _top -= instruction.A;
                        session._evaluationStack[_top] = BuildListValue(session._evaluationStack, _top, instruction.A);
                        _top++;
                        return true;

                    case GameEventScriptBytecodeOpCode.BuildSequence:
                        _top -= instruction.A;
                        session._evaluationStack[_top] = BuildSequenceValue(session._evaluationStack, _top, instruction.A);
                        _top++;
                        return true;

                    case GameEventScriptBytecodeOpCode.BuildSet:
                        _top -= instruction.A;
                        session._evaluationStack[_top] = BuildSetValue(session._evaluationStack, _top, instruction.A);
                        _top++;
                        return true;

                    case GameEventScriptBytecodeOpCode.BuildDictionary:
                        _top -= instruction.A;
                        session._evaluationStack[_top] = BuildDictionaryValue(session._evaluationStack, _top, instruction.A, instruction.Names);
                        _top++;
                        return true;

                    case GameEventScriptBytecodeOpCode.BuildMessage:
                        _top -= instruction.A;
                        session._evaluationStack[_top] = BuildMessageValue(
                            session._evaluationStack,
                            _top,
                            instruction.A,
                            instruction.Names,
                            instruction.DiagnosticName,
                            instruction.DiagnosticArgumentName);
                        _top++;
                        return true;

                    case GameEventScriptBytecodeOpCode.BindHandler:
                        _top -= instruction.A + 1;
                        session._evaluationStack[_top] = BindHandlerValue(
                            session._evaluationStack[_top],
                            session._evaluationStack,
                            _top + 1,
                            instruction.A,
                            instruction.Names);
                        _top++;
                        return true;

                    case GameEventScriptBytecodeOpCode.CallExtension:
                        _top -= instruction.A;
                        if (!session.TryCallExtension(
                                instruction.DiagnosticName,
                                instruction.DiagnosticArgumentName,
                                instruction.Names,
                                instruction.B,
                                session._evaluationStack,
                                _top,
                                instruction.A,
                                out var extensionValue))
                        {
                            fiber.Complete(BytecodeVmValue.Nothing, success: false);
                            return false;
                        }

                        session._evaluationStack[_top++] = extensionValue;
                        return true;

                    case GameEventScriptBytecodeOpCode.Pipeline:
                        if (instruction.PipelineProgram is null ||
                            !session.TryExecutePipelineProgram(instruction.PipelineProgram, out var pipelineValue))
                        {
                            fiber.Complete(BytecodeVmValue.Nothing, success: false);
                            return false;
                        }

                        session._evaluationStack[_top++] = pipelineValue;
                        return true;

                    case GameEventScriptBytecodeOpCode.GeneratedCollection:
                        if (instruction.GeneratedCollectionProgram is null ||
                            !session.TryExecuteGeneratedCollectionProgram(instruction.GeneratedCollectionProgram, out var generatedValue))
                        {
                            fiber.Complete(BytecodeVmValue.Nothing, success: false);
                            return false;
                        }

                        session._evaluationStack[_top++] = generatedValue;
                        return true;

                    case GameEventScriptBytecodeOpCode.GuardedChoice:
                        if (instruction.GuardedChoiceProgram is null ||
                            !session.TryExecuteGuardedChoiceProgram(instruction.GuardedChoiceProgram, out var guardedValue))
                        {
                            fiber.Complete(BytecodeVmValue.Nothing, success: false);
                            return false;
                        }

                        session._evaluationStack[_top++] = guardedValue;
                        return true;

                    default:
                        fiber.Complete(BytecodeVmValue.Nothing, success: false);
                        return false;
                }
            }
        }
    }
}
