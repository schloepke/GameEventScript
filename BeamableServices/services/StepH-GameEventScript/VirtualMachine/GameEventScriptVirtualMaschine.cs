#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections.Generic;
using System.Linq;
using StepH.GameEventScript.Api;
using StepH.GameEventScript.Extensions;
using StepH.GameEventScript.Runtime;
using static StepH.GameEventScript.Api.GameEventScriptBinaryBindKind;
using static StepH.GameEventScript.Api.GameEventScriptBytecodeOpCode;
using static StepH.GameEventScript.Api.GameEventScriptBytecodeTypeKind;
using static StepH.GameEventScript.VirtualMachine.GesVmState.StateValue;

namespace StepH.GameEventScript.VirtualMachine;

public class GameEventScriptVmException(string message) : GameEventScriptFatalRuntimeException(message);

public class GameEventScriptVirtualMaschine : IGameEventScriptModule
{
    private readonly GesVmState _vmState;

    public string ModuleName { get; }
    public IEnumerable<GameEventScriptMessageHandlerDescriptor> Handlers { get; }
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
        Handlers = (from bind in _vmState.Binary.BindTable.Entries.Where(x => x.Kind is MessageHandler or MessageNameHandler)
            let signature = GameEventScriptMessageSignature.Create(_vmState.FetchStringByPointer(bind.Name), bind.ArgumentNames.Select(_vmState.FetchStringByPointer))
            let matchArguments = bind.Kind == MessageHandler
            select new GameEventScriptMessageHandlerDescriptor(signature,
                (msg, session) => Invoke(msg, matchArguments, bind.EntryAddress, session),
                bind.RequiredTags.Select(_vmState.FetchStringByPointer).ToArray(),
                bind.ExcludedTags.Select(_vmState.FetchStringByPointer).ToArray(),
                matchArguments)).ToList();
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
        var handler = Handlers.FirstOrDefault(x => x.MatchArguments ? x.Signature.SignatureId == message.SignatureId : message.Name == x.Signature.Name);
        if (handler == null) return false;
        var init = handler.Invoke(message, session);
        while (!init.IsCompleted) init.RunSlice(int.MaxValue);
        return true;
    }

    private class Runner(GesVmState vmState, GameEventScriptSession session) : IGameEventScriptMessageInvocation, IGesVmStreamEntryEvaluator
    {
        public bool IsCompleted { get; private set; } = false;

        public bool TryEvaluateStreamEntry(ushort entryAddress, ushort itemSlot, ref GesVmValue item, GesVmValue[]? captures, ref GesVmValue result)
        {
            if (entryAddress >= vmState.CodeSegmentSize) return vmState.RaiseError($"Invalid stream helper entry address {entryAddress}.");
            var parentFrameLength = vmState.RegisterFrameLength;
            var resultSlot = parentFrameLength;
            vmState.ModifyLocalSlots(1);
            if (vmState.State != Processing) return false;

            vmState.ClearStage();
            if (captures is null)
            {
                var helperFrameLength = Math.Max(parentFrameLength, itemSlot + 1);
                var nothing = new GesVmValue();
                for (ushort i = 0; i < helperFrameLength; i++)
                {
                    if (i == itemSlot) vmState.StageValue(ref item);
                    else if (i < parentFrameLength) vmState.StageValue(ref vmState.Register(i));
                    else vmState.StageValue(ref nothing);
                }
            }
            else
            {
                var helperFrameLength = Math.Max(itemSlot + 1, captures.Length + 1);
                var nothing = new GesVmValue();
                for (ushort i = 0; i < helperFrameLength; i++)
                {
                    if (i == itemSlot)
                    {
                        vmState.StageValue(ref item);
                    }
                    else if (i > 0 && i <= captures.Length)
                    {
                        vmState.StageValue(ref captures[i - 1]);
                    }
                    else
                    {
                        vmState.StageValue(ref nothing);
                    }
                }
            }

            var baseCallStackPointer = vmState.CallStackPointer;
            if (!vmState.CallAddress(entryAddress, resultSlot)) return false;
            while (vmState.State == Processing && vmState.CallStackPointer > baseCallStackPointer)
            {
                RunSlice(1);
            }

            if (vmState.State != Processing || vmState.CallStackPointer != baseCallStackPointer) return false;
            result = vmState.Register(resultSlot);
            vmState.ModifyLocalSlots(-1);
            return vmState.State == Processing;
        }

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
                        case SlotLocals:
                            vmState.ModifyLocalSlots(instruction.Count);
                            break;
                        case Jump:
                            vmState.JumpAddress(instruction.TargetAddress);
                            break;
                        case JumpIfTrue:
                            if (vmState.IsRegisterTrue(instruction.ConditionSlot)) vmState.JumpAddress(instruction.TargetAddress);
                            break;
                        case JumpIfFalse:
                            if (vmState.IsRegisterFalse(instruction.ConditionSlot)) vmState.JumpAddress(instruction.TargetAddress);
                            break;
                        case JumpIfNotTrue:
                            if (vmState.IsRegisterNotTrue(instruction.ConditionSlot)) vmState.JumpAddress(instruction.TargetAddress);
                            break;
                        case JumpIfNothing:
                            if (vmState.IsRegisterNothing(instruction.ConditionSlot)) vmState.JumpAddress(instruction.TargetAddress);
                            break;

                        case Call:
                            vmState.CallAddress(instruction.TargetAddress, instruction.DestinationSlot, instruction.HasInstructionFlag(GameEventScriptInstructionFlag.NormalizeResultAsPredicate));
                            break;
                        case CallStandard:
                            vmState.GesVmCallStandard(instruction.DestinationSlot, instruction.SecondaryListIndex, instruction.ListIndex, instruction.HasInstructionFlag(GameEventScriptInstructionFlag.NormalizeResultAsPredicate));
                            break;
                        case CallExternal:
                            vmState.GesVmCallExternal(instruction.DestinationSlot, instruction.BindId, instruction.ListIndex, session, instruction.HasInstructionFlag(GameEventScriptInstructionFlag.NormalizeResultAsPredicate));
                            break;

                        case ReturnVoid:
                            vmState.ReturnVoid();
                            break;
                        case ReturnValue:
                            vmState.ReturnValue(instruction.XSlot);
                            break;

                        case EmitMessage:
                            vmState.GesVmPublishMessage(instruction.MessageDestination, vmState.Binary.Uint16ConstantTable.Resolve(instruction.ListIndex), false, session);
                            break;
                        case EmitMessageWithTags:
                            vmState.GesVmPublishMessageWithTags(instruction.MessageDestination, vmState.Binary.Uint16ConstantTable.Resolve(instruction.ListIndex),
                                vmState.Binary.Uint16ConstantTable.Resolve(instruction.SecondaryListIndex), false, session);
                            break;
                        case EmitMessageValue:
                            vmState.GesVmPublishMessageValue(ref vmState.Register(instruction.XSlot), false, session);
                            break;
                        case EmitMessageValueWithTags:
                            vmState.GesVmPublishMessageValueWithTags(ref vmState.Register(instruction.XSlot), vmState.Binary.Uint16ConstantTable.Resolve(instruction.ListIndex), false, session);
                            break;

                        case PublishMessage:
                            vmState.GesVmPublishMessage(instruction.MessageDestination, vmState.Binary.Uint16ConstantTable.Resolve(instruction.ListIndex), true, session);
                            break;
                        case PublishMessageWithTags:
                            vmState.GesVmPublishMessageWithTags(instruction.MessageDestination, vmState.Binary.Uint16ConstantTable.Resolve(instruction.ListIndex),
                                vmState.Binary.Uint16ConstantTable.Resolve(instruction.SecondaryListIndex), true, session);
                            break;
                        case PublishMessageValue:
                            vmState.GesVmPublishMessageValue(ref vmState.Register(instruction.XSlot), true, session);
                            break;
                        case PublishMessageValueWithTags:
                            vmState.GesVmPublishMessageValueWithTags(ref vmState.Register(instruction.XSlot), vmState.Binary.Uint16ConstantTable.Resolve(instruction.ListIndex), true, session);
                            break;

                        case Cast:
                            vmState.GesVmCast(instruction.DestinationSlot, vmState.Register(instruction.XSlot), instruction.TypeKind, session);
                            break;
                        case CastCustom:
                            vmState.GesVmCastCustom(instruction.DestinationSlot, vmState.Register(instruction.XSlot), instruction.SecondaryStringIndex);
                            break;
                        case CastUnit:
                            vmState.GesVmCastUnit(instruction.DestinationSlot, vmState.Register(instruction.XSlot), instruction.Unit);
                            break;
                        case CastNumeric:
                            vmState.GesVmCastNumeric(instruction.DestinationSlot, vmState.Register(instruction.XSlot));
                            break;

                        case CheckType:
                            vmState.GesVmCheckType(instruction.DestinationSlot, vmState.Register(instruction.XSlot), instruction.TypeKind);
                            break;
                        case CheckCustomType:
                            vmState.GesVmCheckCustomType(instruction.DestinationSlot, vmState.Register(instruction.XSlot), instruction.SecondaryStringIndex);
                            break;
                        case CheckUnit:
                            vmState.GesVmCheckUnit(instruction.DestinationSlot, vmState.Register(instruction.XSlot), instruction.Unit);
                            break;
                        case CheckNumeric:
                            vmState.GesVmCheckNumeric(instruction.DestinationSlot, vmState.Register(instruction.XSlot));
                            break;
                        case CheckInteger:
                            vmState.GesVmCheckInteger(instruction.DestinationSlot, vmState.Register(instruction.XSlot));
                            break;
                        case CheckFractional:
                            vmState.GesVmCheckFractional(instruction.DestinationSlot, vmState.Register(instruction.XSlot));
                            break;

                        case Move:
                            vmState.Register(instruction.DestinationSlot) = vmState.Register(instruction.XSlot);
                            break;
                        case MemberAccess:
                            vmState.GesVmMemberAccess(instruction.DestinationSlot, vmState.FetchStringByPointer(instruction.StringIndex), vmState.Register(instruction.YSlot));
                            break;
                        case IndexAccess:
                            vmState.GesVmIndexAccess(instruction.DestinationSlot, instruction.Index, vmState.Register(instruction.YSlot));
                            break;
                        case PropertyAccess:
                            vmState.GesVmPropertyAccess(instruction.DestinationSlot, vmState.Register(instruction.XSlot), vmState.Register(instruction.YSlot));
                            break;
                        case BindHandler:
                            vmState.BindHandler(instruction.DestinationSlot, vmState.Register(instruction.XSlot), vmState.Binary.Uint16ConstantTable.Resolve(instruction.ListIndex));
                            break;

                        case LoadNothing:
                            vmState.Register(instruction.DestinationSlot).SetNothing();
                            break;
                        case LoadTrue:
                            vmState.Register(instruction.DestinationSlot).SetBoolean(true);
                            break;
                        case LoadFalse:
                            vmState.Register(instruction.DestinationSlot).SetBoolean(false);
                            break;
                        case LoadInteger:
                            vmState.Register(instruction.DestinationSlot).SetInteger(instruction.I64, instruction.Unit);
                            break;
                        case LoadFloat:
                            vmState.Register(instruction.DestinationSlot).SetFloat(instruction.F64, instruction.Unit);
                            break;
                        case LoadPercentage:
                            vmState.Register(instruction.DestinationSlot).SetPercentage(instruction.F64);
                            break;
                        case LoadText:
                            vmState.SetTextPointer(instruction.DestinationSlot, instruction.StringIndex);
                            break;
                        case LoadTag:
                            vmState.SetTagPointer(instruction.DestinationSlot, instruction.StringIndex);
                            break;
                        case LoadHandler:
                            vmState.CreateMessageSignature(instruction.DestinationSlot, vmState.Binary.Uint16ConstantTable.Resolve(instruction.ListIndex));
                            break;
                        case LoadMessage:
                            vmState.CreateMessage(instruction.DestinationSlot, vmState.Binary.Uint16ConstantTable.Resolve(instruction.SecondaryListIndex), vmState.Binary.Uint16ConstantTable.Resolve(instruction.ListIndex));
                            break;

                        case StageRegister:
                            vmState.StageRegister(instruction.XSlot);
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
                            vmState.GesVmCreateDice(instruction.DestinationSlot, instruction.Count, instruction.ImmediateY, session);
                            break;
                        case CreateVector:
                            vmState.GesVmCreateVector(instruction.DestinationSlot, instruction.ImmediateX);
                            vmState.ClearStage();
                            break;
                        case CreatePoint:
                            vmState.GesVmCreatePoint(instruction.DestinationSlot, instruction.ImmediateX);
                            vmState.ClearStage();
                            break;
                        case CreateList:
                            vmState.GesVmCreateList(instruction.DestinationSlot);
                            vmState.ClearStage();
                            break;
                        case CreateMap:
                            vmState.GesVmCreateMap(instruction.DestinationSlot, instruction.SecondaryListIndex);
                            vmState.ClearStage();
                            break;
                        case CreateRange:
                            vmState.GesVmCreateRange(instruction.DestinationSlot, vmState.Register(instruction.XSlot), vmState.Register(instruction.YSlot));
                            break;
                        case CreateRangeWithStep:
                            vmState.GesVmCreateRange(instruction.DestinationSlot, vmState.Register(instruction.XSlot), vmState.Register(instruction.YSlot), vmState.Register(instruction.AU));
                            break;
                        case CreateRangeIterator:
                            vmState.GesVmCreateRangeStream(instruction.DestinationSlot, vmState.Register(instruction.XSlot), vmState.Register(instruction.YSlot), session);
                            break;
                        case CreateRangeIteratorWithStep:
                            vmState.GesVmCreateRangeStream(instruction.DestinationSlot, vmState.Register(instruction.XSlot), vmState.Register(instruction.YSlot), vmState.Register(instruction.AU), session);
                            break;
                        case CreateRangeIteratorShort:
                            if (!session.RuntimeBudget.TryCheckRangeLength(GameEventScriptRangeMath.GetLength(instruction.ImmediateX, instruction.ImmediateY, instruction.AS), "For loop range would enumerate more range items than allowed."))
                            {
                                vmState.Register(instruction.DestinationSlot).SetStream(new GesVmIntegerRangeStream(0, 0, 0));
                                break;
                            }

                            vmState.Register(instruction.DestinationSlot).SetStream(new GesVmIntegerRangeStream(instruction.ImmediateX, instruction.ImmediateY, instruction.AS));
                            break;
                        case CreateRecord:
                            vmState.CallRecordConstructor(instruction.BindId, instruction.DestinationSlot);
                            break;
                        case CreateExternalType:
                            vmState.GesVmCreateExternalType(instruction.DestinationSlot, instruction.BindId, instruction.ListIndex);
                            vmState.ClearStage();
                            break;

                        case HasValue:
                            vmState.Register(instruction.DestinationSlot).SetBoolean(vmState.Register(instruction.XSlot).HasValue);
                            break;
                        case IsEmpty:
                            vmState.Register(instruction.DestinationSlot).SetBoolean(!vmState.Register(instruction.XSlot).HasValue);
                            break;
                        case Default:
                            var a = vmState.Register(instruction.XSlot);
                            vmState.Register(instruction.DestinationSlot) = a.HasValue ? a : vmState.Register(instruction.YSlot);
                            break;

                        #endregion

                        #region Group 2 - boolean algebra, comparison, math and random

                        case Or:
                            vmState.GesVmOr(instruction.DestinationSlot, vmState.Register(instruction.XSlot), vmState.Register(instruction.YSlot));
                            break;
                        case And:
                            vmState.GesVmAnd(instruction.DestinationSlot, vmState.Register(instruction.XSlot), vmState.Register(instruction.YSlot));
                            break;
                        case Xor:
                            vmState.GesVmXor(instruction.DestinationSlot, vmState.Register(instruction.XSlot), vmState.Register(instruction.YSlot));
                            break;
                        case Implies:
                            vmState.GesVmImplies(instruction.DestinationSlot, vmState.Register(instruction.XSlot), vmState.Register(instruction.YSlot));
                            break;
                        case Not:
                            vmState.GesVmNot(instruction.DestinationSlot, vmState.Register(instruction.XSlot));
                            break;
                        case Equal:
                            vmState.GesVmEqual(instruction.DestinationSlot, in vmState.Register(instruction.XSlot), in vmState.Register(instruction.YSlot));
                            break;
                        case NotEqual:
                            vmState.GesVmNotEqual(instruction.DestinationSlot, in vmState.Register(instruction.XSlot), in vmState.Register(instruction.YSlot));
                            break;
                        case Less:
                            vmState.GesVmLess(instruction.DestinationSlot, in vmState.Register(instruction.XSlot), in vmState.Register(instruction.YSlot));
                            break;
                        case Greater:
                            vmState.GesVmGreater(instruction.DestinationSlot, in vmState.Register(instruction.XSlot), in vmState.Register(instruction.YSlot));
                            break;
                        case LessOrEqual:
                            vmState.GesVmLessOrEqual(instruction.DestinationSlot, in vmState.Register(instruction.XSlot), in vmState.Register(instruction.YSlot));
                            break;
                        case GreaterOrEqual:
                            vmState.GesVmGreaterOrEqual(instruction.DestinationSlot, in vmState.Register(instruction.XSlot), in vmState.Register(instruction.YSlot));
                            break;
                        case Add:
                            vmState.GesVmAdd(instruction.DestinationSlot, vmState.Register(instruction.XSlot), vmState.Register(instruction.YSlot));
                            break;
                        case Subtract:
                            vmState.GesVmSubtract(instruction.DestinationSlot, vmState.Register(instruction.XSlot), vmState.Register(instruction.YSlot));
                            break;
                        case Multiply:
                            vmState.GesVmMultiply(instruction.DestinationSlot, vmState.Register(instruction.XSlot), vmState.Register(instruction.YSlot));
                            break;
                        case Divide:
                            vmState.GesVmDivide(instruction.DestinationSlot, vmState.Register(instruction.XSlot), vmState.Register(instruction.YSlot));
                            break;
                        case Power:
                            vmState.GesVmPower(instruction.DestinationSlot, vmState.Register(instruction.XSlot), vmState.Register(instruction.YSlot));
                            break;
                        case IntegerDivide:
                            vmState.GesVmFloorDivide(instruction.DestinationSlot, vmState.Register(instruction.XSlot), vmState.Register(instruction.YSlot));
                            break;
                        case Modulo:
                            vmState.GesVmModulo(instruction.DestinationSlot, vmState.Register(instruction.XSlot), vmState.Register(instruction.YSlot));
                            break;
                        case Remainder:
                            vmState.GesVmRemainder(instruction.DestinationSlot, vmState.Register(instruction.XSlot), vmState.Register(instruction.YSlot));
                            break;
                        case Min:
                            vmState.GesVmMin(instruction.DestinationSlot, vmState.Register(instruction.XSlot), vmState.Register(instruction.YSlot));
                            break;
                        case Max:
                            vmState.GesVmMax(instruction.DestinationSlot, vmState.Register(instruction.XSlot), vmState.Register(instruction.YSlot));
                            break;
                        case Negate:
                            vmState.GesVmNegate(instruction.DestinationSlot, vmState.Register(instruction.XSlot));
                            break;
                        case Abs:
                            vmState.GesVmAbs(instruction.DestinationSlot, vmState.Register(instruction.XSlot));
                            break;
                        case LogN:
                            vmState.GesVmNaturalLog(instruction.DestinationSlot, vmState.Register(instruction.XSlot));
                            break;
                        case Chance:
                            vmState.GesVmChance(instruction.DestinationSlot, vmState.Register(instruction.XSlot));
                            break;
                        case Clamp:
                            vmState.GesVmClamp(instruction.DestinationSlot, vmState.Register(instruction.XSlot), vmState.Register(instruction.YSlot), vmState.Register(instruction.AU));
                            break;
                        case RandomTake:
                            vmState.GesVmRandom(instruction.DestinationSlot, vmState.Register(instruction.XSlot), vmState.Register(instruction.YSlot), vmState.RandomGenerator);
                            break;
                        case RandomTakeFloat:
                            vmState.GesVmRandomFloat(instruction.DestinationSlot, vmState.Register(instruction.XSlot), vmState.Register(instruction.YSlot), vmState.RandomGenerator);
                            break;
                        case RandomPush:
                            var seed = vmState.Register(instruction.XSlot);
                            vmState.PushRandom(seed.Kind == Integer ? new GesVmXoshiroRandom(seed.IntegerValue) : vmState.RandomGenerator);
                            break;
                        case RandomPushConstant:
                            vmState.PushRandom(new GesVmXoshiroRandom(instruction.I64));
                            break;
                        case RandomPop:
                            vmState.PopRandom();
                            break;
                        case Term:
                            vmState.GesVmTerm(instruction.DestinationSlot, vmState.Register(instruction.XSlot), vmState.Register(instruction.YSlot));
                            break;
                        case Exp:
                            vmState.GesVmExp(instruction.DestinationSlot, vmState.Register(instruction.XSlot));
                            break;
                        case Floor:
                            vmState.GesVmFloor(instruction.DestinationSlot, vmState.Register(instruction.XSlot));
                            break;
                        case Ceil:
                            vmState.GesVmCeil(instruction.DestinationSlot, vmState.Register(instruction.XSlot));
                            break;
                        case Truncate:
                            vmState.GesVmTruncate(instruction.DestinationSlot, vmState.Register(instruction.XSlot));
                            break;
                        case RoundHalfEven:
                            vmState.GesVmRoundHalfEven(instruction.DestinationSlot, vmState.Register(instruction.XSlot));
                            break;
                        case RoundHalfUp:
                            vmState.GesVmRoundHalfUp(instruction.DestinationSlot, vmState.Register(instruction.XSlot));
                            break;
                        case RoundHalfDown:
                            vmState.GesVmRoundHalfDown(instruction.DestinationSlot, vmState.Register(instruction.XSlot));
                            break;
                        case DegreeToRadians:
                            vmState.GesVmDegreeToRadians(instruction.DestinationSlot, vmState.Register(instruction.XSlot));
                            break;
                        case DegreeFromRadians:
                            vmState.GesVmDegreeFromRadians(instruction.DestinationSlot, vmState.Register(instruction.XSlot));
                            break;
                        case WrapDegree:
                            vmState.GesVmWrapDegree(instruction.DestinationSlot, vmState.Register(instruction.XSlot));
                            break;
                        case Sin:
                            vmState.GesVmSin(instruction.DestinationSlot, vmState.Register(instruction.XSlot));
                            break;
                        case Cos:
                            vmState.GesVmCos(instruction.DestinationSlot, vmState.Register(instruction.XSlot));
                            break;
                        case Tan:
                            vmState.GesVmTan(instruction.DestinationSlot, vmState.Register(instruction.XSlot));
                            break;
                        case Asin:
                            vmState.GesVmAsin(instruction.DestinationSlot, vmState.Register(instruction.XSlot));
                            break;
                        case Acos:
                            vmState.GesVmAcos(instruction.DestinationSlot, vmState.Register(instruction.XSlot));
                            break;
                        case Atan:
                            vmState.GesVmAtan(instruction.DestinationSlot, vmState.Register(instruction.XSlot));
                            break;
                        case Atan2:
                            vmState.GesVmAtan2(instruction.DestinationSlot, vmState.Register(instruction.XSlot), vmState.Register(instruction.YSlot));
                            break;
                        case Hypot2D:
                            vmState.GesVmHypot2D(instruction.DestinationSlot, vmState.Register(instruction.XSlot), vmState.Register(instruction.YSlot));
                            break;
                        case Hypot3D:
                            vmState.GesVmHypot3D(instruction.DestinationSlot, vmState.Register(instruction.XSlot), vmState.Register(instruction.YSlot), vmState.Register(instruction.AU));
                            break;
                        case Distance:
                            vmState.GesVmDistance(instruction.DestinationSlot, vmState.Register(instruction.XSlot), vmState.Register(instruction.YSlot));
                            break;
                        case Distance2D:
                            vmState.GesVmDistance2D(instruction.DestinationSlot, vmState.Register(instruction.XSlot), vmState.Register(instruction.YSlot), vmState.Register(instruction.AU), vmState.Register(instruction.BU));
                            break;
                        case Distance3D:
                            vmState.GesVmDistance3D(instruction.DestinationSlot, vmState.Register(instruction.XSlot), vmState.Register(instruction.YSlot), vmState.Register(instruction.AU), vmState.Register(instruction.BU), vmState.Register(instruction.CU), vmState.Register(instruction.DU));
                            break;
                        case DistanceSquared:
                            vmState.GesVmDistanceSquared(instruction.DestinationSlot, vmState.Register(instruction.XSlot), vmState.Register(instruction.YSlot));
                            break;
                        case DistanceSquared2D:
                            vmState.GesVmDistanceSquared2D(instruction.DestinationSlot, vmState.Register(instruction.XSlot), vmState.Register(instruction.YSlot), vmState.Register(instruction.AU), vmState.Register(instruction.BU));
                            break;
                        case DistanceSquared3D:
                            vmState.GesVmDistanceSquared3D(instruction.DestinationSlot, vmState.Register(instruction.XSlot), vmState.Register(instruction.YSlot), vmState.Register(instruction.AU), vmState.Register(instruction.BU), vmState.Register(instruction.CU), vmState.Register(instruction.DU));
                            break;
                        case LengthSquared:
                            vmState.GesVmLengthSquared(instruction.DestinationSlot, vmState.Register(instruction.XSlot));
                            break;
                        case LengthSquared2D:
                            vmState.GesVmLengthSquared2D(instruction.DestinationSlot, vmState.Register(instruction.XSlot), vmState.Register(instruction.YSlot));
                            break;
                        case LengthSquared3D:
                            vmState.GesVmLengthSquared3D(instruction.DestinationSlot, vmState.Register(instruction.XSlot), vmState.Register(instruction.YSlot), vmState.Register(instruction.AU));
                            break;
                        case Normalize:
                            vmState.GesVmNormalize(instruction.DestinationSlot, vmState.Register(instruction.XSlot));
                            break;
                        case Normalize2D:
                            vmState.GesVmNormalize2D(instruction.DestinationSlot, vmState.Register(instruction.XSlot), vmState.Register(instruction.YSlot));
                            break;
                        case Normalize3D:
                            vmState.GesVmNormalize3D(instruction.DestinationSlot, vmState.Register(instruction.XSlot), vmState.Register(instruction.YSlot), vmState.Register(instruction.AU));
                            break;
                        case Dot:
                            vmState.GesVmDot(instruction.DestinationSlot, vmState.Register(instruction.XSlot), vmState.Register(instruction.YSlot));
                            break;
                        case Dot2D:
                            vmState.GesVmDot2D(instruction.DestinationSlot, vmState.Register(instruction.XSlot), vmState.Register(instruction.YSlot), vmState.Register(instruction.AU), vmState.Register(instruction.BU));
                            break;
                        case Dot3D:
                            vmState.GesVmDot3D(instruction.DestinationSlot, vmState.Register(instruction.XSlot), vmState.Register(instruction.YSlot), vmState.Register(instruction.AU), vmState.Register(instruction.BU), vmState.Register(instruction.CU), vmState.Register(instruction.DU));
                            break;
                        case Cross:
                            vmState.GesVmCross(instruction.DestinationSlot, vmState.Register(instruction.XSlot), vmState.Register(instruction.YSlot));
                            break;
                        case Cross2D:
                            vmState.GesVmCross2D(instruction.DestinationSlot, vmState.Register(instruction.XSlot), vmState.Register(instruction.YSlot), vmState.Register(instruction.AU), vmState.Register(instruction.BU));
                            break;
                        case Cross3D:
                            vmState.GesVmCross3D(instruction.DestinationSlot, vmState.Register(instruction.XSlot), vmState.Register(instruction.YSlot), vmState.Register(instruction.AU), vmState.Register(instruction.BU), vmState.Register(instruction.CU), vmState.Register(instruction.DU));
                            break;
                        case AngleBetween:
                            vmState.GesVmAngleBetween(instruction.DestinationSlot, vmState.Register(instruction.XSlot), vmState.Register(instruction.YSlot));
                            break;
                        case AngleBetween2D:
                            vmState.GesVmAngleBetween2D(instruction.DestinationSlot, vmState.Register(instruction.XSlot), vmState.Register(instruction.YSlot), vmState.Register(instruction.AU), vmState.Register(instruction.BU));
                            break;
                        case AngleBetween3D:
                            vmState.GesVmAngleBetween3D(instruction.DestinationSlot, vmState.Register(instruction.XSlot), vmState.Register(instruction.YSlot), vmState.Register(instruction.AU), vmState.Register(instruction.BU), vmState.Register(instruction.CU), vmState.Register(instruction.DU));
                            break;

                        #endregion

                        #region Group 3 - text, collection, streams

                        case TakeFirst:
                            vmState.GesVmTakeFirst(instruction.DestinationSlot, vmState.Register(instruction.XSlot), instruction.ImmediateY);
                            break;
                        case DropFirst:
                            vmState.GesVmDropFirst(instruction.DestinationSlot, vmState.Register(instruction.XSlot), instruction.ImmediateY);
                            break;
                        case TakeLast:
                            vmState.GesVmTakeLast(instruction.DestinationSlot, vmState.Register(instruction.XSlot), instruction.ImmediateY);
                            break;
                        case DropLast:
                            vmState.GesVmDropLast(instruction.DestinationSlot, vmState.Register(instruction.XSlot), instruction.ImmediateY);
                            break;
                        case TakeHighest:
                            vmState.GesVmTakeHighest(instruction.DestinationSlot, vmState.Register(instruction.XSlot), instruction.ImmediateY);
                            break;
                        case TakeLowest:
                            vmState.GesVmTakeLowest(instruction.DestinationSlot, vmState.Register(instruction.XSlot), instruction.ImmediateY);
                            break;
                        case DropHighest:
                            vmState.GesVmDropHighest(instruction.DestinationSlot, vmState.Register(instruction.XSlot), instruction.ImmediateY);
                            break;
                        case DropLowest:
                            vmState.GesVmDropLowest(instruction.DestinationSlot, vmState.Register(instruction.XSlot), instruction.ImmediateY);
                            break;
                        case OneRandom:
                            vmState.GesVmOneRandom(instruction.DestinationSlot, vmState.Register(instruction.XSlot), vmState.RandomGenerator);
                            break;
                        case TakeRandom:
                            vmState.GesVmTakeRandom(instruction.DestinationSlot, vmState.Register(instruction.XSlot), instruction.ImmediateY, vmState.RandomGenerator);
                            break;
                        case OneWeighted:
                            vmState.GesVmOneWeighted(instruction.DestinationSlot, vmState.Register(instruction.XSlot), vmState.Register(instruction.YSlot), vmState.RandomGenerator);
                            break;
                        case TakeWeighted:
                            vmState.GesVmTakeWeighted(instruction.DestinationSlot, vmState.Register(instruction.XSlot), vmState.Register(instruction.AU), instruction.ImmediateY, vmState.RandomGenerator);
                            break;
                        case Count:
                            vmState.GesVmCount(instruction.DestinationSlot, vmState.Register(instruction.XSlot));
                            break;
                        case StartsWith:
                            vmState.GesVmStartsWith(instruction.DestinationSlot, vmState.Register(instruction.XSlot), vmState.Register(instruction.YSlot));
                            break;
                        case EndsWith:
                            vmState.GesVmEndsWith(instruction.DestinationSlot, vmState.Register(instruction.XSlot), vmState.Register(instruction.YSlot));
                            break;
                        case Contains:
                            vmState.GesVmContains(instruction.DestinationSlot, vmState.Register(instruction.XSlot), vmState.Register(instruction.YSlot));
                            break;
                        case ContainsAny:
                            vmState.GesVmContainsAnyAll(instruction.DestinationSlot, vmState.Register(instruction.XSlot), vmState.Register(instruction.YSlot), requireAll: false);
                            break;
                        case ContainsAll:
                            vmState.GesVmContainsAnyAll(instruction.DestinationSlot, vmState.Register(instruction.XSlot), vmState.Register(instruction.YSlot), requireAll: true);
                            break;
                        case ContainsValue:
                            vmState.GesVmContainsValue(instruction.DestinationSlot, vmState.Register(instruction.XSlot), vmState.Register(instruction.YSlot));
                            break;
                        case Union:
                            vmState.GesVmUnion(instruction.DestinationSlot, vmState.Register(instruction.XSlot), vmState.Register(instruction.YSlot));
                            break;
                        case Intersect:
                            vmState.GesVmIntersect(instruction.DestinationSlot, vmState.Register(instruction.XSlot), vmState.Register(instruction.YSlot));
                            break;
                        case Zip:
                            vmState.GesVmZip(instruction.DestinationSlot, vmState.Register(instruction.XSlot), vmState.Register(instruction.YSlot));
                            break;
                        case KeysOfMap:
                            vmState.GesVmKeys(instruction.DestinationSlot, vmState.Register(instruction.XSlot));
                            break;
                        case ValuesOfMap:
                            vmState.GesVmValues(instruction.DestinationSlot, vmState.Register(instruction.XSlot));
                            break;
                        case EntriesOfMap:
                            vmState.GesVmEntries(instruction.DestinationSlot, vmState.Register(instruction.XSlot));
                            break;
                        case GameEventScriptBytecodeOpCode.First:
                            vmState.GesVmFirst(instruction.DestinationSlot, vmState.Register(instruction.XSlot));
                            break;
                        case GameEventScriptBytecodeOpCode.Last:
                            vmState.GesVmLast(instruction.DestinationSlot, vmState.Register(instruction.XSlot));
                            break;
                        case GameEventScriptBytecodeOpCode.Single:
                            vmState.GesVmSingle(instruction.DestinationSlot, vmState.Register(instruction.XSlot));
                            break;
                        case StreamCreate:
                            vmState.GesVmStreamCreate(instruction.DestinationSlot, vmState.Register(instruction.XSlot));
                            break;
                        case StreamCreateOrJump:
                            vmState.GesVmStreamCreate(instruction.DestinationSlot, vmState.Register(instruction.XSlot));
                            if (vmState.IsRegisterNothing(instruction.DestinationSlot)) vmState.JumpAddress(instruction.TargetAddress);
                            break;
                        case StreamNext:
                        {
                            vmState.GesVmStreamNext(instruction.DestinationSlot, vmState.Register(instruction.XSlot), instruction.TargetAddress);
                            if (vmState.Register(instruction.DestinationSlot).Kind is not Nothing)
                            {
                                session.RuntimeBudget.TryConsumeLoopIteration("For loop iteration exceeds the configured limit.");
                            }
                            break;
                        }
                        case StreamClose:
                            vmState.GesVmStreamClose(instruction.XSlot);
                            break;
                        case StreamMap:
                            vmState.GesVmStreamMap(instruction.DestinationSlot, vmState.Register(instruction.XSlot), instruction.EntryAddress, instruction.AU, instruction.BU, this);
                            break;
                        case StreamFilter:
                            vmState.GesVmStreamFilter(instruction.DestinationSlot, vmState.Register(instruction.XSlot), instruction.EntryAddress, instruction.AU, instruction.BU, this);
                            break;
                        case StreamCollectList:
                            vmState.GesVmStreamCollectList(instruction.DestinationSlot, vmState.Register(instruction.XSlot));
                            break;
                        case StreamCollectMap:
                            vmState.GesVmStreamCollectMap(instruction.DestinationSlot, vmState.Register(instruction.XSlot), instruction.YSlot, instruction.AU, this);
                            break;
                        case StreamCollectMapValue:
                            vmState.GesVmStreamCollectMapValue(instruction.DestinationSlot, vmState.Register(instruction.XSlot), instruction.YSlot, instruction.AU, instruction.BU, this);
                            break;
                        case HasAny:
                            vmState.GesVmHasAnyAll(instruction.DestinationSlot, vmState.Register(instruction.XSlot), false);
                            break;
                        case HasAll:
                            vmState.GesVmHasAnyAll(instruction.DestinationSlot, vmState.Register(instruction.XSlot), true);
                            break;
                        case Distinct:
                            vmState.GesVmDistinct(instruction.DestinationSlot, vmState.Register(instruction.XSlot));
                            break;
                        case DistinctBy:
                            vmState.GesVmDistinctBy(instruction.DestinationSlot, vmState.Register(instruction.XSlot), instruction.YSlot, instruction.AU, this);
                            break;
                        case GroupBy:
                            vmState.GesVmGroupBy(instruction.DestinationSlot, vmState.Register(instruction.XSlot), instruction.YSlot, instruction.AU, this);
                            break;
                        case SortAscending:
                            vmState.GesVmSortAscending(instruction.DestinationSlot, vmState.Register(instruction.XSlot));
                            break;
                        case SortDescending:
                            vmState.GesVmSortDescending(instruction.DestinationSlot, vmState.Register(instruction.XSlot));
                            break;
                        case OrderByAscending:
                            vmState.GesVmOrderByAscending(instruction.DestinationSlot, vmState.Register(instruction.XSlot), instruction.YSlot, instruction.AU, this);
                            break;
                        case OrderByDescending:
                            vmState.GesVmOrderByDescending(instruction.DestinationSlot, vmState.Register(instruction.XSlot), instruction.YSlot, instruction.AU, this);
                            break;
                        case Reverse:
                            vmState.GesVmReverse(instruction.DestinationSlot, vmState.Register(instruction.XSlot));
                            break;
                        case Shuffle:
                            vmState.GesVmShuffle(instruction.DestinationSlot, vmState.Register(instruction.XSlot), vmState.RandomGenerator);
                            break;
                        case ListBuilderCreate:
                            vmState.GesVmCreateListBuilder(instruction.DestinationSlot);
                            break;
                        case ListBuilderAdd:
                            vmState.GesVmListBuilderAdd(vmState.Register(instruction.XSlot), in vmState.Register(instruction.YSlot));
                            break;
                        case ListBuilderFinish:
                            vmState.GesVmListBuilderFinish(instruction.DestinationSlot, in vmState.Register(instruction.XSlot));
                            break;
                        case HasPattern:
                            vmState.GesVmHasPattern(instruction.DestinationSlot, vmState.Register(instruction.XSlot), (GameEventScriptBytecodePatternKind)instruction.AU, instruction.ImmediateY, vmState.Register(instruction.BU));
                            break;
                        case TakePattern:
                            vmState.GesVmTakePattern(instruction.DestinationSlot, vmState.Register(instruction.XSlot), (GameEventScriptBytecodePatternKind)instruction.AU, instruction.ImmediateY, vmState.Register(instruction.BU));
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
