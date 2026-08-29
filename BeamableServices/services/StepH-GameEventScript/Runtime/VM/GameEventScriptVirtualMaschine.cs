#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections.Generic;
using StepH.GameEventScript.Api;
using StepH.GameEventScript.Runtime;
using static StepH.GameEventScript.Api.GameEventScriptBinaryBindKind;
using static StepH.GameEventScript.Api.GameEventScriptBytecodeOpCode;
using static StepH.GameEventScript.Api.GameEventScriptBytecodeTypeKind;
using static StepH.GameEventScript.Runtime.VM.GesVmState.StateValue;
using StepH.GameEventScript.Runtime.Values;

namespace StepH.GameEventScript.Runtime.VM;

internal class GameEventScriptVmException(string message) : GameEventScriptFatalRuntimeException(message);

internal class GameEventScriptVirtualMaschine : IGameEventScriptModule, IGameEventScriptDebugDumpModule
{
    private readonly GesVmState _vmState;
    private readonly GameEventScriptMessageHandlerDescriptor[] _handlers;

    public string ModuleName { get; }
    public IEnumerable<GameEventScriptMessageHandlerDescriptor> Handlers => _handlers;
    public string? DebugScriptSource { get; set; }

    public string DumpState(string? scriptSource = null, bool includeInstructionAddresses = true)
        => _vmState.Dump(includeInstructionAddresses, scriptSource ?? DebugScriptSource);

    public void Bind(IGameEventScriptExtensionRegistry extensionRegistry, IGameEventScriptExternalTypeRegistry typeRegistry)
    {
        _vmState.BindDynamicReferences(
            extensionRegistry ?? GameEventScriptEmptyExtensionRegistry.Instance,
            typeRegistry ?? GameEventScriptEmptyExternalTypeRegistry.Instance);
    }
    
    public static GameEventScriptVirtualMaschine Create(GameEventScriptBinary binary, ushort registerSize, ushort stackSize)
    {
        return new GameEventScriptVirtualMaschine(binary, registerSize, stackSize);
    }

    private GameEventScriptVirtualMaschine(GameEventScriptBinary binary, ushort registerSize, ushort stackSize)
    {
        ModuleName = binary.ModuleName;
        _vmState = new GesVmState(binary, registerSize, stackSize);
        _handlers = CreateHandlers();
    }

    private GameEventScriptMessageHandlerDescriptor[] CreateHandlers()
    {
        var entries = _vmState.Binary.BindTable.Entries;
        var handlerCount = 0;
        for (var index = 0; index < entries.Count; index++)
        {
            var kind = entries[index].Kind;
            if (kind is MessageHandler or MessageNameHandler)
            {
                handlerCount++;
            }
        }

        if (handlerCount == 0)
        {
            return [];
        }

        var handlers = new GameEventScriptMessageHandlerDescriptor[handlerCount];
        var handlerIndex = 0;
        for (var index = 0; index < entries.Count; index++)
        {
            var bind = entries[index];
            if (bind.Kind is not (MessageHandler or MessageNameHandler))
            {
                continue;
            }

            var name = _vmState.FetchStringByPointer(bind.Name);
            var argumentNames = ReadTextPointers(bind.ArgumentNames);
            var requiredTags = ReadTextPointers(bind.RequiredTags);
            var excludedTags = ReadTextPointers(bind.ExcludedTags);
            var matchArguments = bind.Kind == MessageHandler;
            var entryAddress = bind.EntryAddress;
            var signature = GameEventScriptMessageSignature.Create(name, argumentNames);
            handlers[handlerIndex++] = new GameEventScriptMessageHandlerDescriptor(
                signature,
                (msg, session) => Invoke(msg, matchArguments, entryAddress, session),
                requiredTags,
                excludedTags,
                matchArguments);
        }

        return handlers;
    }

    private string[] ReadTextPointers(IReadOnlyList<ushort> pointers)
    {
        if (pointers.Count == 0)
        {
            return [];
        }

        var values = new string[pointers.Count];
        for (var index = 0; index < pointers.Count; index++)
        {
            values[index] = _vmState.FetchStringByPointer(pointers[index]);
        }

        return values;
    }

    private IGameEventScriptMessageInvocation Invoke(GameEventScriptMessage message, bool matchArguments, ushort entryAddress, GameEventScriptSession session)
    {
        if (_vmState.State is Processing) throw new GameEventScriptVmException("Virtual machine is already processing another message");
        if (_vmState.State != Ready) _vmState.Reset();
        if (!_vmState.PrepareStateForMessage(message, matchArguments, entryAddress, session)) _vmState.RaiseError("Failed to prepare state for message");
        return new Runner(_vmState, session);
    }

    public bool ExecuteMessage(GameEventScriptMessage message, GameEventScriptSession session)
    {
        GameEventScriptMessageHandlerDescriptor? handler = null;
        for (var index = 0; index < _handlers.Length; index++)
        {
            var candidate = _handlers[index];
            if (candidate.MatchArguments
                    ? candidate.Signature.SignatureId == message.SignatureId
                    : message.Name == candidate.Signature.Name)
            {
                handler = candidate;
                break;
            }
        }

        if (handler is null) return false;
        var init = handler.Invoke(message, session);
        while (!init.IsCompleted) init.RunSlice(int.MaxValue);
        return true;
    }

    private class Runner(GesVmState vmState, GameEventScriptSession session) : IGameEventScriptMessageInvocation
    {
        public bool IsCompleted { get; private set; } = false;

        public int RunSlice(int maxSteps)
        {
            if (maxSteps <= 0) throw new ArgumentOutOfRangeException(nameof(maxSteps), "RunSlice requires a positive integer as max steps");
            var opcodesExecuted = 0;
            try
            {
                while (!IsCompleted && vmState.State == Processing && !session.RuntimeBudget.IsExhausted && opcodesExecuted < maxSteps)
                {
                    var instruction = vmState.FetchInstructionAndIncrementInstructionPointer();
                    switch (instruction.OpCode)
                    {
                        #region Group 1 - control, calls, messages, types, values

                        case Nop:
                            break;
                        case RegisterLocals:
                            vmState.ModifyLocalRegisters(instruction.Count);
                            break;
                        case Jump:
                            vmState.JumpAddress(instruction.TargetAddress);
                            break;
                        case JumpIfTrue:
                            if (vmState.IsRegisterTrue(instruction.ConditionRegister)) vmState.JumpAddress(instruction.TargetAddress);
                            break;
                        case JumpIfFalse:
                            if (vmState.IsRegisterFalse(instruction.ConditionRegister)) vmState.JumpAddress(instruction.TargetAddress);
                            break;
                        case JumpIfNotTrue:
                            if (vmState.IsRegisterNotTrue(instruction.ConditionRegister)) vmState.JumpAddress(instruction.TargetAddress);
                            break;
                        case JumpIfNothing:
                            if (vmState.IsRegisterNothing(instruction.ConditionRegister)) vmState.JumpAddress(instruction.TargetAddress);
                            break;

                        case Call:
                            vmState.CallAddress(instruction.TargetAddress, instruction.DestinationRegister, instruction.HasInstructionFlag(GameEventScriptInstructionFlag.NormalizeResultAsPredicate));
                            break;
                        case CreateSeries:
                            vmState.GesVmCreateSeries(instruction.DestinationRegister, (GameEventScriptBytecodeSeriesKind)instruction.TypeOperand);
                            break;
                        case CallExternal:
                            vmState.GesVmCallExternal(instruction.DestinationRegister, instruction.BindId, instruction.ListIndex, session, instruction.HasInstructionFlag(GameEventScriptInstructionFlag.NormalizeResultAsPredicate));
                            break;

                        case ReturnVoid:
                            vmState.ReturnVoid();
                            break;
                        case ReturnValue:
                            vmState.ReturnValue(instruction.XRegister);
                            break;

                        case EmitMessage:
                            vmState.GesVmPublishMessage(instruction.MessageDestination, vmState.Binary.Uint16ConstantTable.Resolve(instruction.ListIndex), false, session);
                            break;
                        case EmitMessageWithTags:
                            vmState.GesVmPublishMessageWithTags(instruction.MessageDestination, vmState.Binary.Uint16ConstantTable.Resolve(instruction.ListIndex),
                                vmState.Binary.Uint16ConstantTable.Resolve(instruction.SecondaryListIndex), false, session);
                            break;
                        case EmitMessageValue:
                            vmState.GesVmPublishMessageValue(in vmState.Register(instruction.XRegister), false, session);
                            break;
                        case EmitMessageValueWithTags:
                            vmState.GesVmPublishMessageValueWithTags(in vmState.Register(instruction.XRegister), vmState.Binary.Uint16ConstantTable.Resolve(instruction.ListIndex), false, session);
                            break;

                        case PublishMessage:
                            vmState.GesVmPublishMessage(instruction.MessageDestination, vmState.Binary.Uint16ConstantTable.Resolve(instruction.ListIndex), true, session);
                            break;
                        case PublishMessageWithTags:
                            vmState.GesVmPublishMessageWithTags(instruction.MessageDestination, vmState.Binary.Uint16ConstantTable.Resolve(instruction.ListIndex),
                                vmState.Binary.Uint16ConstantTable.Resolve(instruction.SecondaryListIndex), true, session);
                            break;
                        case PublishMessageValue:
                            vmState.GesVmPublishMessageValue(in vmState.Register(instruction.XRegister), true, session);
                            break;
                        case PublishMessageValueWithTags:
                            vmState.GesVmPublishMessageValueWithTags(in vmState.Register(instruction.XRegister), vmState.Binary.Uint16ConstantTable.Resolve(instruction.ListIndex), true, session);
                            break;

                        case Cast:
                            vmState.GesVmCast(instruction.DestinationRegister, vmState.Register(instruction.XRegister), instruction.TypeKind, session);
                            break;
                        case CastCustom:
                            vmState.GesVmCastCustom(instruction.DestinationRegister, vmState.Register(instruction.XRegister), instruction.SecondaryStringIndex);
                            break;
                        case CastUnit:
                            vmState.GesVmCastUnit(instruction.DestinationRegister, vmState.Register(instruction.XRegister), instruction.Unit);
                            break;
                        case CastNumeric:
                            vmState.GesVmCastNumeric(instruction.DestinationRegister, vmState.Register(instruction.XRegister));
                            break;

                        case CheckType:
                            vmState.GesVmCheckType(instruction.DestinationRegister, vmState.Register(instruction.XRegister), instruction.TypeKind);
                            break;
                        case CheckCustomType:
                            vmState.GesVmCheckCustomType(instruction.DestinationRegister, vmState.Register(instruction.XRegister), instruction.SecondaryStringIndex);
                            break;
                        case CheckUnit:
                            vmState.GesVmCheckUnit(instruction.DestinationRegister, vmState.Register(instruction.XRegister), instruction.Unit);
                            break;
                        case CheckNumeric:
                            vmState.GesVmCheckNumeric(instruction.DestinationRegister, vmState.Register(instruction.XRegister));
                            break;
                        case CheckInteger:
                            vmState.GesVmCheckInteger(instruction.DestinationRegister, vmState.Register(instruction.XRegister));
                            break;
                        case CheckFractional:
                            vmState.GesVmCheckFractional(instruction.DestinationRegister, vmState.Register(instruction.XRegister));
                            break;

                        case Move:
                            vmState.SetValue(instruction.DestinationRegister, in vmState.Register(instruction.XRegister));
                            break;
                        case MemberAccess:
                            vmState.GesVmMemberAccess(instruction.DestinationRegister, vmState.FetchStringByPointer(instruction.StringIndex), vmState.Register(instruction.YRegister));
                            break;
                        case IndexAccess:
                            vmState.GesVmIndexAccess(instruction.DestinationRegister, instruction.Index, vmState.Register(instruction.YRegister));
                            break;
                        case PropertyAccess:
                            vmState.GesVmPropertyAccess(instruction.DestinationRegister, vmState.Register(instruction.XRegister), vmState.Register(instruction.YRegister));
                            break;
                        case BindHandler:
                            vmState.BindHandler(instruction.DestinationRegister, vmState.Register(instruction.XRegister), vmState.Binary.Uint16ConstantTable.Resolve(instruction.ListIndex));
                            break;

                        case LoadNothing:
                            vmState.SetNothing(instruction.DestinationRegister);
                            break;
                        case LoadTrue:
                            vmState.SetBoolean(instruction.DestinationRegister, true);
                            break;
                        case LoadFalse:
                            vmState.SetBoolean(instruction.DestinationRegister, false);
                            break;
                        case LoadInteger:
                            vmState.SetInteger(instruction.DestinationRegister, instruction.I64, instruction.Unit);
                            break;
                        case LoadFloat:
                            vmState.SetFloat(instruction.DestinationRegister, instruction.F64, instruction.Unit);
                            break;
                        case LoadPercentage:
                            vmState.SetPercentage(instruction.DestinationRegister, instruction.F64);
                            break;
                        case LoadText:
                            vmState.SetTextPointer(instruction.DestinationRegister, instruction.StringIndex);
                            break;
                        case LoadTag:
                            vmState.SetTagPointer(instruction.DestinationRegister, instruction.StringIndex);
                            break;
                        case LoadHandler:
                            vmState.CreateMessageSignature(instruction.DestinationRegister, vmState.Binary.Uint16ConstantTable.Resolve(instruction.ListIndex));
                            break;
                        case LoadMessage:
                            vmState.CreateMessage(instruction.DestinationRegister, vmState.Binary.Uint16ConstantTable.Resolve(instruction.SecondaryListIndex), vmState.Binary.Uint16ConstantTable.Resolve(instruction.ListIndex));
                            break;

                        case StageRegister:
                            vmState.StageRegister(instruction.XRegister);
                            break;
                        case StageNothing:
                            vmState.StageNothing();
                            break;
                        case StageTrue:
                            vmState.StageBoolean(true);
                            break;
                        case StageFalse:
                            vmState.StageBoolean(false);
                            break;
                        case StageInteger:
                            vmState.StageInteger(instruction.I64, instruction.Unit);
                            break;
                        case StageFloat:
                            vmState.StageFloat(instruction.F64, instruction.Unit);
                            break;
                        case StageText:
                            vmState.StageTextConstant(instruction.StringIndex);
                            break;
                        case StageTag:
                            vmState.StageTagConstant(instruction.StringIndex);
                            break;
                        case StagePercentage:
                            vmState.StagePercentage(instruction.F64);
                            break;

                        case CreateDice:
                            vmState.GesVmCreateDice(instruction.DestinationRegister, instruction.Count, instruction.ImmediateY, session);
                            break;
                        case CreateVector:
                            vmState.GesVmCreateVector(instruction.DestinationRegister, instruction.ImmediateX);
                            vmState.ClearStage();
                            break;
                        case CreatePoint:
                            vmState.GesVmCreatePoint(instruction.DestinationRegister, instruction.ImmediateX);
                            vmState.ClearStage();
                            break;
                        case CreateList:
                            vmState.GesVmCreateList(instruction.DestinationRegister);
                            vmState.ClearStage();
                            break;
                        case CreateMap:
                            vmState.GesVmCreateMap(instruction.DestinationRegister, instruction.SecondaryListIndex);
                            vmState.ClearStage();
                            break;
                        case CreateRange:
                            vmState.GesVmCreateRange(instruction.DestinationRegister, vmState.Register(instruction.XRegister), vmState.Register(instruction.YRegister));
                            break;
                        case CreateRangeWithStep:
                            vmState.GesVmCreateRange(instruction.DestinationRegister, vmState.Register(instruction.XRegister), vmState.Register(instruction.YRegister), vmState.Register(instruction.AU));
                            break;
                        case CreateRangeIterator:
                            vmState.GesVmCreateRangeIterator(instruction.DestinationRegister, vmState.Register(instruction.XRegister), vmState.Register(instruction.YRegister), session);
                            break;
                        case CreateRangeIteratorWithStep:
                            vmState.GesVmCreateRangeIterator(instruction.DestinationRegister, vmState.Register(instruction.XRegister), vmState.Register(instruction.YRegister), vmState.Register(instruction.AU), session);
                            break;
                        case CreateRangeIteratorShort:
                            if (!session.RuntimeBudget.CheckRangeLengthWithinLimit(GameEventScriptRangeMath.GetLength(instruction.ImmediateX, instruction.ImmediateY, instruction.AS), "For loop range would enumerate more range items than allowed."))
                            {
                                vmState.SetIterator(instruction.DestinationRegister, new GesIntegerRangeIterator(0, 0, 0));
                                break;
                            }

                            vmState.SetIterator(instruction.DestinationRegister, new GesIntegerRangeIterator(instruction.ImmediateX, instruction.ImmediateY, instruction.AS));
                            break;
                        case CreateRecord:
                            vmState.CallRecordConstructor(instruction.BindId, instruction.DestinationRegister);
                            break;
                        case CreateRecordValue:
                            vmState.GesVmCreateRecordValue(instruction.DestinationRegister, vmState.Register(instruction.XRegister), instruction.SecondaryStringIndex);
                            break;
                        case CreateExternalType:
                            vmState.GesVmCreateExternalType(instruction.DestinationRegister, instruction.BindId, instruction.ListIndex);
                            vmState.ClearStage();
                            break;

                        case HasValue:
                            vmState.SetBoolean(instruction.DestinationRegister, vmState.Register(instruction.XRegister).HasValue);
                            break;
                        case IsEmpty:
                            vmState.SetBoolean(instruction.DestinationRegister, !vmState.Register(instruction.XRegister).HasValue);
                            break;
                        case Default:
                            var a = vmState.Register(instruction.XRegister);
                            if (a.HasValue)
                            {
                                vmState.SetValue(instruction.DestinationRegister, in a);
                            }
                            else
                            {
                                vmState.SetValue(instruction.DestinationRegister, in vmState.Register(instruction.YRegister));
                            }
                            break;

                        #endregion

                        #region Group 2 - boolean algebra, comparison, math and random

                        case Or:
                            vmState.GesVmOr(instruction.DestinationRegister, vmState.Register(instruction.XRegister), vmState.Register(instruction.YRegister));
                            break;
                        case And:
                            vmState.GesVmAnd(instruction.DestinationRegister, vmState.Register(instruction.XRegister), vmState.Register(instruction.YRegister));
                            break;
                        case Xor:
                            vmState.GesVmXor(instruction.DestinationRegister, vmState.Register(instruction.XRegister), vmState.Register(instruction.YRegister));
                            break;
                        case Implies:
                            vmState.GesVmImplies(instruction.DestinationRegister, vmState.Register(instruction.XRegister), vmState.Register(instruction.YRegister));
                            break;
                        case Not:
                            vmState.GesVmNot(instruction.DestinationRegister, vmState.Register(instruction.XRegister));
                            break;
                        case Equal:
                            vmState.GesVmEqual(instruction.DestinationRegister, in vmState.Register(instruction.XRegister), in vmState.Register(instruction.YRegister));
                            break;
                        case NotEqual:
                            vmState.GesVmNotEqual(instruction.DestinationRegister, in vmState.Register(instruction.XRegister), in vmState.Register(instruction.YRegister));
                            break;
                        case Less:
                            vmState.GesVmLess(instruction.DestinationRegister, in vmState.Register(instruction.XRegister), in vmState.Register(instruction.YRegister));
                            break;
                        case Greater:
                            vmState.GesVmGreater(instruction.DestinationRegister, in vmState.Register(instruction.XRegister), in vmState.Register(instruction.YRegister));
                            break;
                        case LessOrEqual:
                            vmState.GesVmLessOrEqual(instruction.DestinationRegister, in vmState.Register(instruction.XRegister), in vmState.Register(instruction.YRegister));
                            break;
                        case GreaterOrEqual:
                            vmState.GesVmGreaterOrEqual(instruction.DestinationRegister, in vmState.Register(instruction.XRegister), in vmState.Register(instruction.YRegister));
                            break;
                        case Add:
                            vmState.GesVmAdd(instruction.DestinationRegister, vmState.Register(instruction.XRegister), vmState.Register(instruction.YRegister));
                            break;
                        case Subtract:
                            vmState.GesVmSubtract(instruction.DestinationRegister, vmState.Register(instruction.XRegister), vmState.Register(instruction.YRegister));
                            break;
                        case Multiply:
                            vmState.GesVmMultiply(instruction.DestinationRegister, vmState.Register(instruction.XRegister), vmState.Register(instruction.YRegister));
                            break;
                        case Divide:
                            vmState.GesVmDivide(instruction.DestinationRegister, vmState.Register(instruction.XRegister), vmState.Register(instruction.YRegister));
                            break;
                        case Power:
                            vmState.GesVmPower(instruction.DestinationRegister, vmState.Register(instruction.XRegister), vmState.Register(instruction.YRegister));
                            break;
                        case IntegerDivide:
                            vmState.GesVmFloorDivide(instruction.DestinationRegister, vmState.Register(instruction.XRegister), vmState.Register(instruction.YRegister));
                            break;
                        case Modulo:
                            vmState.GesVmModulo(instruction.DestinationRegister, vmState.Register(instruction.XRegister), vmState.Register(instruction.YRegister));
                            break;
                        case Remainder:
                            vmState.GesVmRemainder(instruction.DestinationRegister, vmState.Register(instruction.XRegister), vmState.Register(instruction.YRegister));
                            break;
                        case Min:
                            vmState.GesVmMin(instruction.DestinationRegister, vmState.Register(instruction.XRegister), vmState.Register(instruction.YRegister));
                            break;
                        case Max:
                            vmState.GesVmMax(instruction.DestinationRegister, vmState.Register(instruction.XRegister), vmState.Register(instruction.YRegister));
                            break;
                        case Negate:
                            vmState.GesVmNegate(instruction.DestinationRegister, vmState.Register(instruction.XRegister));
                            break;
                        case Abs:
                            vmState.GesVmAbs(instruction.DestinationRegister, vmState.Register(instruction.XRegister));
                            break;
                        case LogN:
                            vmState.GesVmNaturalLog(instruction.DestinationRegister, vmState.Register(instruction.XRegister));
                            break;
                        case Chance:
                            vmState.GesVmChance(instruction.DestinationRegister, vmState.Register(instruction.XRegister));
                            break;
                        case Clamp:
                            vmState.GesVmClamp(instruction.DestinationRegister, vmState.Register(instruction.XRegister), vmState.Register(instruction.YRegister), vmState.Register(instruction.AU));
                            break;
                        case RandomTake:
                            vmState.GesVmRandom(instruction.DestinationRegister, vmState.Register(instruction.XRegister), vmState.Register(instruction.YRegister), vmState.RandomGenerator);
                            break;
                        case RandomTakeFloat:
                            vmState.GesVmRandomFloat(instruction.DestinationRegister, vmState.Register(instruction.XRegister), vmState.Register(instruction.YRegister), vmState.RandomGenerator);
                            break;
                        case RandomPush:
                            var seed = vmState.Register(instruction.XRegister);
                            vmState.PushRandom(seed.Kind == Integer ? GameEventScriptRandomGenerator.FromSeed(seed.IntegerValue) : vmState.RandomGenerator);
                            break;
                        case RandomPushConstant:
                            vmState.PushRandom(GameEventScriptRandomGenerator.FromSeed(instruction.I64));
                            break;
                        case RandomPop:
                            vmState.PopRandom();
                            break;
                        case Term:
                            vmState.GesVmTerm(instruction.DestinationRegister, vmState.Register(instruction.XRegister), vmState.Register(instruction.YRegister));
                            break;
                        case Exp:
                            vmState.GesVmExp(instruction.DestinationRegister, vmState.Register(instruction.XRegister));
                            break;
                        case Floor:
                            vmState.GesVmFloor(instruction.DestinationRegister, vmState.Register(instruction.XRegister));
                            break;
                        case Ceil:
                            vmState.GesVmCeil(instruction.DestinationRegister, vmState.Register(instruction.XRegister));
                            break;
                        case Truncate:
                            vmState.GesVmTruncate(instruction.DestinationRegister, vmState.Register(instruction.XRegister));
                            break;
                        case RoundHalfEven:
                            vmState.GesVmRoundHalfEven(instruction.DestinationRegister, vmState.Register(instruction.XRegister));
                            break;
                        case RoundHalfUp:
                            vmState.GesVmRoundHalfUp(instruction.DestinationRegister, vmState.Register(instruction.XRegister));
                            break;
                        case RoundHalfDown:
                            vmState.GesVmRoundHalfDown(instruction.DestinationRegister, vmState.Register(instruction.XRegister));
                            break;
                        case DegreeToRadians:
                            vmState.GesVmDegreeToRadians(instruction.DestinationRegister, vmState.Register(instruction.XRegister));
                            break;
                        case DegreeFromRadians:
                            vmState.GesVmDegreeFromRadians(instruction.DestinationRegister, vmState.Register(instruction.XRegister));
                            break;
                        case WrapDegree:
                            vmState.GesVmWrapDegree(instruction.DestinationRegister, vmState.Register(instruction.XRegister));
                            break;
                        case Sin:
                            vmState.GesVmSin(instruction.DestinationRegister, vmState.Register(instruction.XRegister));
                            break;
                        case Cos:
                            vmState.GesVmCos(instruction.DestinationRegister, vmState.Register(instruction.XRegister));
                            break;
                        case Tan:
                            vmState.GesVmTan(instruction.DestinationRegister, vmState.Register(instruction.XRegister));
                            break;
                        case Asin:
                            vmState.GesVmAsin(instruction.DestinationRegister, vmState.Register(instruction.XRegister));
                            break;
                        case Acos:
                            vmState.GesVmAcos(instruction.DestinationRegister, vmState.Register(instruction.XRegister));
                            break;
                        case Atan:
                            vmState.GesVmAtan(instruction.DestinationRegister, vmState.Register(instruction.XRegister));
                            break;
                        case Atan2:
                            vmState.GesVmAtan2(instruction.DestinationRegister, vmState.Register(instruction.XRegister), vmState.Register(instruction.YRegister));
                            break;
                        case Hypot2D:
                            vmState.GesVmHypot2D(instruction.DestinationRegister, vmState.Register(instruction.XRegister), vmState.Register(instruction.YRegister));
                            break;
                        case Hypot3D:
                            vmState.GesVmHypot3D(instruction.DestinationRegister, vmState.Register(instruction.XRegister), vmState.Register(instruction.YRegister), vmState.Register(instruction.AU));
                            break;
                        case Distance:
                            vmState.GesVmDistance(instruction.DestinationRegister, vmState.Register(instruction.XRegister), vmState.Register(instruction.YRegister));
                            break;
                        case Distance2D:
                            vmState.GesVmDistance2D(instruction.DestinationRegister, vmState.Register(instruction.XRegister), vmState.Register(instruction.YRegister), vmState.Register(instruction.AU), vmState.Register(instruction.BU));
                            break;
                        case Distance3D:
                            vmState.GesVmDistance3D(instruction.DestinationRegister, vmState.Register(instruction.XRegister), vmState.Register(instruction.YRegister), vmState.Register(instruction.AU), vmState.Register(instruction.BU), vmState.Register(instruction.CU), vmState.Register(instruction.DU));
                            break;
                        case DistanceSquared:
                            vmState.GesVmDistanceSquared(instruction.DestinationRegister, vmState.Register(instruction.XRegister), vmState.Register(instruction.YRegister));
                            break;
                        case DistanceSquared2D:
                            vmState.GesVmDistanceSquared2D(instruction.DestinationRegister, vmState.Register(instruction.XRegister), vmState.Register(instruction.YRegister), vmState.Register(instruction.AU), vmState.Register(instruction.BU));
                            break;
                        case DistanceSquared3D:
                            vmState.GesVmDistanceSquared3D(instruction.DestinationRegister, vmState.Register(instruction.XRegister), vmState.Register(instruction.YRegister), vmState.Register(instruction.AU), vmState.Register(instruction.BU), vmState.Register(instruction.CU), vmState.Register(instruction.DU));
                            break;
                        case LengthSquared:
                            vmState.GesVmLengthSquared(instruction.DestinationRegister, vmState.Register(instruction.XRegister));
                            break;
                        case LengthSquared2D:
                            vmState.GesVmLengthSquared2D(instruction.DestinationRegister, vmState.Register(instruction.XRegister), vmState.Register(instruction.YRegister));
                            break;
                        case LengthSquared3D:
                            vmState.GesVmLengthSquared3D(instruction.DestinationRegister, vmState.Register(instruction.XRegister), vmState.Register(instruction.YRegister), vmState.Register(instruction.AU));
                            break;
                        case Normalize:
                            vmState.GesVmNormalize(instruction.DestinationRegister, vmState.Register(instruction.XRegister));
                            break;
                        case Normalize2D:
                            vmState.GesVmNormalize2D(instruction.DestinationRegister, vmState.Register(instruction.XRegister), vmState.Register(instruction.YRegister));
                            break;
                        case Normalize3D:
                            vmState.GesVmNormalize3D(instruction.DestinationRegister, vmState.Register(instruction.XRegister), vmState.Register(instruction.YRegister), vmState.Register(instruction.AU));
                            break;
                        case Dot:
                            vmState.GesVmDot(instruction.DestinationRegister, vmState.Register(instruction.XRegister), vmState.Register(instruction.YRegister));
                            break;
                        case Dot2D:
                            vmState.GesVmDot2D(instruction.DestinationRegister, vmState.Register(instruction.XRegister), vmState.Register(instruction.YRegister), vmState.Register(instruction.AU), vmState.Register(instruction.BU));
                            break;
                        case Dot3D:
                            vmState.GesVmDot3D(instruction.DestinationRegister, vmState.Register(instruction.XRegister), vmState.Register(instruction.YRegister), vmState.Register(instruction.AU), vmState.Register(instruction.BU), vmState.Register(instruction.CU), vmState.Register(instruction.DU));
                            break;
                        case Cross:
                            vmState.GesVmCross(instruction.DestinationRegister, vmState.Register(instruction.XRegister), vmState.Register(instruction.YRegister));
                            break;
                        case Cross2D:
                            vmState.GesVmCross2D(instruction.DestinationRegister, vmState.Register(instruction.XRegister), vmState.Register(instruction.YRegister), vmState.Register(instruction.AU), vmState.Register(instruction.BU));
                            break;
                        case Cross3D:
                            vmState.GesVmCross3D(instruction.DestinationRegister, vmState.Register(instruction.XRegister), vmState.Register(instruction.YRegister), vmState.Register(instruction.AU), vmState.Register(instruction.BU), vmState.Register(instruction.CU), vmState.Register(instruction.DU));
                            break;
                        case AngleBetween:
                            vmState.GesVmAngleBetween(instruction.DestinationRegister, vmState.Register(instruction.XRegister), vmState.Register(instruction.YRegister));
                            break;
                        case AngleBetween2D:
                            vmState.GesVmAngleBetween2D(instruction.DestinationRegister, vmState.Register(instruction.XRegister), vmState.Register(instruction.YRegister), vmState.Register(instruction.AU), vmState.Register(instruction.BU));
                            break;
                        case AngleBetween3D:
                            vmState.GesVmAngleBetween3D(instruction.DestinationRegister, vmState.Register(instruction.XRegister), vmState.Register(instruction.YRegister), vmState.Register(instruction.AU), vmState.Register(instruction.BU), vmState.Register(instruction.CU), vmState.Register(instruction.DU));
                            break;

                        #endregion

                        #region Group 3 - text, collection, iterators

                        case TakeFirst:
                            vmState.GesVmTakeFirst(instruction.DestinationRegister, vmState.Register(instruction.XRegister), instruction.ImmediateY);
                            break;
                        case DropFirst:
                            vmState.GesVmDropFirst(instruction.DestinationRegister, vmState.Register(instruction.XRegister), instruction.ImmediateY);
                            break;
                        case TakeLast:
                            vmState.GesVmTakeLast(instruction.DestinationRegister, vmState.Register(instruction.XRegister), instruction.ImmediateY);
                            break;
                        case DropLast:
                            vmState.GesVmDropLast(instruction.DestinationRegister, vmState.Register(instruction.XRegister), instruction.ImmediateY);
                            break;
                        case TakeHighest:
                            vmState.GesVmTakeHighest(instruction.DestinationRegister, vmState.Register(instruction.XRegister), instruction.ImmediateY);
                            break;
                        case TakeLowest:
                            vmState.GesVmTakeLowest(instruction.DestinationRegister, vmState.Register(instruction.XRegister), instruction.ImmediateY);
                            break;
                        case DropHighest:
                            vmState.GesVmDropHighest(instruction.DestinationRegister, vmState.Register(instruction.XRegister), instruction.ImmediateY);
                            break;
                        case DropLowest:
                            vmState.GesVmDropLowest(instruction.DestinationRegister, vmState.Register(instruction.XRegister), instruction.ImmediateY);
                            break;
                        case OneRandom:
                            vmState.GesVmOneRandom(instruction.DestinationRegister, vmState.Register(instruction.XRegister), vmState.RandomGenerator);
                            break;
                        case TakeRandom:
                            vmState.GesVmTakeRandom(instruction.DestinationRegister, vmState.Register(instruction.XRegister), instruction.ImmediateY, vmState.RandomGenerator);
                            break;
                        case OneWeighted:
                            vmState.GesVmOneWeighted(instruction.DestinationRegister, vmState.Register(instruction.XRegister), vmState.Register(instruction.YRegister), vmState.RandomGenerator);
                            break;
                        case TakeWeighted:
                            vmState.GesVmTakeWeighted(instruction.DestinationRegister, vmState.Register(instruction.XRegister), vmState.Register(instruction.AU), instruction.ImmediateY, vmState.RandomGenerator);
                            break;
                        case Count:
                            vmState.GesVmCount(instruction.DestinationRegister, vmState.Register(instruction.XRegister));
                            break;
                        case StartsWith:
                            vmState.GesVmStartsWith(instruction.DestinationRegister, vmState.Register(instruction.XRegister), vmState.Register(instruction.YRegister));
                            break;
                        case EndsWith:
                            vmState.GesVmEndsWith(instruction.DestinationRegister, vmState.Register(instruction.XRegister), vmState.Register(instruction.YRegister));
                            break;
                        case Contains:
                            vmState.GesVmContains(instruction.DestinationRegister, vmState.Register(instruction.XRegister), vmState.Register(instruction.YRegister));
                            break;
                        case ContainsAny:
                            vmState.GesVmContainsAnyAll(instruction.DestinationRegister, vmState.Register(instruction.XRegister), vmState.Register(instruction.YRegister), requireAll: false);
                            break;
                        case ContainsAll:
                            vmState.GesVmContainsAnyAll(instruction.DestinationRegister, vmState.Register(instruction.XRegister), vmState.Register(instruction.YRegister), requireAll: true);
                            break;
                        case ContainsValue:
                            vmState.GesVmContainsValue(instruction.DestinationRegister, vmState.Register(instruction.XRegister), vmState.Register(instruction.YRegister));
                            break;
                        case Union:
                            vmState.GesVmUnion(instruction.DestinationRegister, vmState.Register(instruction.XRegister), vmState.Register(instruction.YRegister));
                            break;
                        case Intersect:
                            vmState.GesVmIntersect(instruction.DestinationRegister, vmState.Register(instruction.XRegister), vmState.Register(instruction.YRegister));
                            break;
                        case Zip:
                            vmState.GesVmZip(instruction.DestinationRegister, vmState.Register(instruction.XRegister), vmState.Register(instruction.YRegister));
                            break;
                        case KeysOfMap:
                            vmState.GesVmKeys(instruction.DestinationRegister, vmState.Register(instruction.XRegister));
                            break;
                        case ValuesOfMap:
                            vmState.GesVmValues(instruction.DestinationRegister, vmState.Register(instruction.XRegister));
                            break;
                        case EntriesOfMap:
                            vmState.GesVmEntries(instruction.DestinationRegister, vmState.Register(instruction.XRegister));
                            break;
                        case GameEventScriptBytecodeOpCode.First:
                            vmState.GesVmFirst(instruction.DestinationRegister, vmState.Register(instruction.XRegister));
                            break;
                        case GameEventScriptBytecodeOpCode.Last:
                            vmState.GesVmLast(instruction.DestinationRegister, vmState.Register(instruction.XRegister));
                            break;
                        case GameEventScriptBytecodeOpCode.Single:
                            vmState.GesVmSingle(instruction.DestinationRegister, vmState.Register(instruction.XRegister));
                            break;
                        case IteratorCreate:
                            vmState.GesVmIteratorCreate(instruction.DestinationRegister, vmState.Register(instruction.XRegister));
                            break;
                        case IteratorCreateOrJump:
                            vmState.GesVmIteratorCreate(instruction.DestinationRegister, vmState.Register(instruction.XRegister));
                            if (vmState.IsRegisterNothing(instruction.DestinationRegister)) vmState.JumpAddress(instruction.TargetAddress);
                            break;
                        case IteratorNext:
                        {
                            vmState.GesVmIteratorNext(instruction.DestinationRegister, vmState.Register(instruction.XRegister), instruction.TargetAddress);
                            if (vmState.Register(instruction.DestinationRegister).Kind is not Nothing)
                            {
                                session.RuntimeBudget.ConsumeLoopIterationIfAvailable("For loop iteration exceeds the configured limit.");
                            }
                            break;
                        }
                        case IteratorClose:
                            vmState.GesVmIteratorClose(instruction.XRegister);
                            break;
                        case HasAny:
                            vmState.GesVmHasAnyAll(instruction.DestinationRegister, vmState.Register(instruction.XRegister), false);
                            break;
                        case HasAll:
                            vmState.GesVmHasAnyAll(instruction.DestinationRegister, vmState.Register(instruction.XRegister), true);
                            break;
                        case Distinct:
                            vmState.GesVmDistinct(instruction.DestinationRegister, vmState.Register(instruction.XRegister));
                            break;
                        case SortAscending:
                            vmState.GesVmSortAscending(instruction.DestinationRegister, vmState.Register(instruction.XRegister));
                            break;
                        case SortDescending:
                            vmState.GesVmSortDescending(instruction.DestinationRegister, vmState.Register(instruction.XRegister));
                            break;
                        case Reverse:
                            vmState.GesVmReverse(instruction.DestinationRegister, vmState.Register(instruction.XRegister));
                            break;
                        case Shuffle:
                            vmState.GesVmShuffle(instruction.DestinationRegister, vmState.Register(instruction.XRegister), vmState.RandomGenerator);
                            break;
                        case ListBuilderCreate:
                            vmState.GesVmCreateListBuilder(instruction.DestinationRegister);
                            break;
                        case ListBuilderAdd:
                            vmState.GesVmListBuilderAdd(vmState.Register(instruction.XRegister), in vmState.Register(instruction.YRegister));
                            break;
                        case ListBuilderFinish:
                            vmState.GesVmListBuilderFinish(instruction.DestinationRegister, in vmState.Register(instruction.XRegister));
                            break;
                        case MapBuilderCreate:
                            vmState.GesVmCreateMapBuilder(instruction.DestinationRegister);
                            break;
                        case MapBuilderAdd:
                            vmState.GesVmMapBuilderAdd(vmState.Register(instruction.XRegister), in vmState.Register(instruction.YRegister), in vmState.Register(instruction.AU));
                            break;
                        case MapBuilderFinish:
                            vmState.GesVmMapBuilderFinish(instruction.DestinationRegister, in vmState.Register(instruction.XRegister));
                            break;
                        case DistinctBuilderCreate:
                            vmState.GesVmCreateDistinctBuilder(instruction.DestinationRegister);
                            break;
                        case DistinctBuilderAdd:
                            vmState.GesVmDistinctBuilderAdd(vmState.Register(instruction.XRegister), in vmState.Register(instruction.YRegister), in vmState.Register(instruction.AU));
                            break;
                        case DistinctBuilderFinish:
                            vmState.GesVmDistinctBuilderFinish(instruction.DestinationRegister, in vmState.Register(instruction.XRegister));
                            break;
                        case GroupBuilderCreate:
                            vmState.GesVmCreateGroupBuilder(instruction.DestinationRegister);
                            break;
                        case GroupBuilderAdd:
                            vmState.GesVmGroupBuilderAdd(vmState.Register(instruction.XRegister), in vmState.Register(instruction.YRegister), in vmState.Register(instruction.AU));
                            break;
                        case GroupBuilderFinish:
                            vmState.GesVmGroupBuilderFinish(instruction.DestinationRegister, in vmState.Register(instruction.XRegister));
                            break;
                        case OrderBuilderCreate:
                            vmState.GesVmCreateOrderBuilder(instruction.DestinationRegister);
                            break;
                        case OrderBuilderAdd:
                            vmState.GesVmOrderBuilderAdd(vmState.Register(instruction.XRegister), in vmState.Register(instruction.YRegister), in vmState.Register(instruction.AU));
                            break;
                        case OrderBuilderFinishAscending:
                            vmState.GesVmOrderBuilderFinishAscending(instruction.DestinationRegister, in vmState.Register(instruction.XRegister));
                            break;
                        case OrderBuilderFinishDescending:
                            vmState.GesVmOrderBuilderFinishDescending(instruction.DestinationRegister, in vmState.Register(instruction.XRegister));
                            break;
                        case HasPattern:
                            vmState.GesVmHasPattern(instruction.DestinationRegister, vmState.Register(instruction.XRegister), (GameEventScriptBytecodePatternKind)instruction.AU, instruction.ImmediateY, vmState.Register(instruction.BU));
                            break;
                        case TakePattern:
                            vmState.GesVmTakePattern(instruction.DestinationRegister, vmState.Register(instruction.XRegister), (GameEventScriptBytecodePatternKind)instruction.AU, instruction.ImmediateY, vmState.Register(instruction.BU));
                            break;

                        #endregion

                        default:
                            vmState.RaiseError("Illegal opcode " + nameof(instruction.OpCode) + ". Execution halted.");
                            break;
                    }

                    opcodesExecuted++;
                }
            }
            catch (Exception e)
            {
                vmState.RaiseError(e.Message);
            }

            if (vmState.State == Processing) return opcodesExecuted;
            IsCompleted = true;

            vmState.Reset();
            return opcodesExecuted;
        }
    }
}
