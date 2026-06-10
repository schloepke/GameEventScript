#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections.Generic;
using System.Linq;
using StepH.GameEventScript.Api;
using StepH.GameEventScript.Extensions;
using StepH.GameEventScript.Runtime;
using StepH.GameEventScript.Types;
using static StepH.GameEventScript.Api.GameEventScriptBinaryBindKind;
using static StepH.GameEventScript.Api.GameEventScriptBytecodeOpCode;
using static StepH.GameEventScript.Api.GameEventScriptBytecodeTypeKind;
using static StepH.GameEventScript.VirtualMachine.GesVmState.StateValue;

namespace StepH.GameEventScript.VirtualMachine;

public class GameEventScriptVmException(string message) : GameEventScriptFatalRuntimeException(message);

public class GameEventScriptVirtualMaschine : IGameEventScriptModule
{
    private readonly GesVmState _vmState;
    private IGameEventScriptExternalTypeRegistry _externalTypeRegistry = GameEventScriptEmptyExternalTypeRegistry.Instance;

    public string ModuleName { get; }
    public IEnumerable<GameEventScriptMessageHandlerDescriptor> Handlers { get; }
    public string? DebugScriptSource { get; set; }

    public string DumpState(string? scriptSource = null, bool includeInstructionAddresses = true)
        => _vmState.Dump(includeInstructionAddresses, scriptSource ?? DebugScriptSource);

    public void Bind(IGameEventScriptExtensionRegistry extensionRegistry, IGameEventScriptExternalTypeRegistry typeRegistry)
    {
        _externalTypeRegistry = typeRegistry ?? GameEventScriptEmptyExternalTypeRegistry.Instance;
        foreach (var bind in _vmState.Binary.BindTable.Entries)
        {
            if (bind.Kind is not ExternalType) continue;
            var typeName = _vmState.Binary.TextConstantTable.Resolve(bind.Name);
            var argumentLabels = bind.ArgumentNames.Select(_vmState.Binary.TextConstantTable.Resolve);
            var reference = new GameEventScriptExternalTypeConstructorReference(typeName, argumentLabels);
            if (!_externalTypeRegistry.TryResolve(reference, out _))
            {
                throw new GameEventScriptDynamicLinkException(
                    $"GameEventScript external type constructor ':{reference.SignatureId}' was not dynamically bound.");
            }
        }
    }
    
    public static GameEventScriptVirtualMaschine Create(GameEventScriptBinary binary, ushort registerSize, ushort stackSize)
    {
        return new GameEventScriptVirtualMaschine(binary, registerSize, stackSize);
    }

    private GameEventScriptVirtualMaschine(GameEventScriptBinary binary, ushort registerSize, ushort stackSize)
    {
        ModuleName = binary.ModuleName;
        _vmState = new GesVmState(binary, registerSize, stackSize);
        Handlers = (from bind in binary.BindTable.Entries.Where(x => x.Kind is MessageHandler or MessageNameHandler)
            let signature = GameEventScriptMessageSignature.Create(binary.TextConstantTable.Resolve(bind.Name), bind.ArgumentNames.Select(binary.TextConstantTable.Resolve))
            let matchArguments = bind.Kind == MessageHandler
            select new GameEventScriptMessageHandlerDescriptor(signature,
                (msg, session) => Invoke(msg, matchArguments, bind.EntryAddress, session),
                bind.RequiredTags.Select(binary.TextConstantTable.Resolve).ToArray(),
                bind.ExcludedTags.Select(binary.TextConstantTable.Resolve).ToArray(),
                matchArguments)).ToList();
    }

    private IGameEventScriptMessageInvocation Invoke(GameEventScriptMessage message, bool matchArguments, ushort entryAddress, GameEventScriptSession session)
    {
        if (_vmState.State is Processing) throw new GameEventScriptVmException("Virtual machine is already processing another message");
        if (_vmState.State != Ready) _vmState.Reset();
        if (!_vmState.PrepareStateForMessage(message, matchArguments, entryAddress, session)) _vmState.RaiseError("Failed to prepare state for message");
        return new Runner(_vmState, session, _externalTypeRegistry);
    }

    public bool ExecuteMessage(GameEventScriptMessage message, GameEventScriptSession session)
    {
        var handler = Handlers.FirstOrDefault(x => x.MatchArguments ? x.Signature.SignatureId == message.SignatureId : message.Name == x.Signature.Name);
        if (handler == null) return false;
        var init = handler.Invoke(message, session);
        while (!init.IsCompleted) init.RunSlice(int.MaxValue);
        return true;
    }

    private class Runner(GesVmState vmState, GameEventScriptSession session, IGameEventScriptExternalTypeRegistry externalTypeRegistry) : IGameEventScriptMessageInvocation, IGesVmStreamEntryEvaluator
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
                var nothing = vmState.CreateNothing();
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
                var nothing = vmState.CreateNothing();
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
                            if (vmState.ConditionalRegister(instruction.ConditionSlot).IsTrue) vmState.JumpAddress(instruction.TargetAddress);
                            break;
                        case JumpIfFalse:
                            if (vmState.ConditionalRegister(instruction.ConditionSlot).IsFalse) vmState.JumpAddress(instruction.TargetAddress);
                            break;
                        case JumpIfNotTrue:
                            if (vmState.ConditionalRegister(instruction.ConditionSlot).IsNotTrue) vmState.JumpAddress(instruction.TargetAddress);
                            break;

                        case Call:
                            vmState.CallAddress(instruction.TargetAddress, instruction.DestinationSlot, instruction.HasInstructionFlag(GameEventScriptInstructionFlag.NormalizeResultAsPredicate));
                            break;
                        case CallStandard:
                            vmState.Register(instruction.DestinationSlot).GesVmCallStandard(instruction.SecondaryListIndex, instruction.ListIndex, instruction.HasInstructionFlag(GameEventScriptInstructionFlag.NormalizeResultAsPredicate));
                            break;
                        case CallExternal:
                            vmState.Register(instruction.DestinationSlot).GesVmCallExternal(instruction.BindId, instruction.ListIndex, session, instruction.HasInstructionFlag(GameEventScriptInstructionFlag.NormalizeResultAsPredicate));
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
                            vmState.Register(instruction.DestinationSlot).GesVmCast(ref vmState.Register(instruction.XSlot), instruction.TypeKind, session);
                            break;
                        case CastCustom:
                            vmState.Register(instruction.DestinationSlot).GesVmCastCustom(ref vmState.Register(instruction.XSlot), instruction.SecondaryStringIndex, instruction.DestinationSlot);
                            break;
                        case CastUnit:
                            vmState.Register(instruction.DestinationSlot).GesVmCastUnit(ref vmState.Register(instruction.XSlot), instruction.Unit);
                            break;
                        case CastNumeric:
                            vmState.Register(instruction.DestinationSlot).GesVmCastNumeric(ref vmState.Register(instruction.XSlot));
                            break;

                        case CheckType:
                            vmState.Register(instruction.DestinationSlot).GesVmCheckType(ref vmState.Register(instruction.XSlot), instruction.TypeKind);
                            break;
                        case CheckCustomType:
                            vmState.Register(instruction.DestinationSlot).GesVmCheckCustomType(ref vmState.Register(instruction.XSlot), instruction.SecondaryStringIndex);
                            break;
                        case CheckUnit:
                            vmState.Register(instruction.DestinationSlot).GesVmCheckUnit(ref vmState.Register(instruction.XSlot), instruction.Unit);
                            break;
                        case CheckNumeric:
                            vmState.Register(instruction.DestinationSlot).GesVmCheckNumeric(ref vmState.Register(instruction.XSlot));
                            break;
                        case CheckInteger:
                            vmState.Register(instruction.DestinationSlot).GesVmCheckInteger(ref vmState.Register(instruction.XSlot));
                            break;
                        case CheckFractional:
                            vmState.Register(instruction.DestinationSlot).GesVmCheckFractional(ref vmState.Register(instruction.XSlot));
                            break;

                        case Move:
                            vmState.Register(instruction.DestinationSlot) = vmState.Register(instruction.XSlot);
                            break;
                        case MemberAccess:
                            vmState.Register(instruction.DestinationSlot).GesVmMemberAccess(instruction.StringIndex, ref vmState.Register(instruction.YSlot));
                            break;
                        case IndexAccess:
                            vmState.Register(instruction.DestinationSlot).GesVmIndexAccess(instruction.Index, ref vmState.Register(instruction.YSlot));
                            break;
                        case PropertyAccess:
                            vmState.Register(instruction.DestinationSlot).GesVmPropertyAccess(ref vmState.Register(instruction.XSlot), ref vmState.Register(instruction.YSlot));
                            break;
                        case BindHandler:
                            vmState.Register(instruction.DestinationSlot).BindHandler(ref vmState.Register(instruction.XSlot), vmState.Binary.Uint16ConstantTable.Resolve(instruction.ListIndex));
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
                            vmState.Register(instruction.DestinationSlot).SetTextPointer(instruction.StringIndex);
                            break;
                        case LoadTag:
                            vmState.Register(instruction.DestinationSlot).SetTagPointer(instruction.StringIndex);
                            break;
                        case LoadHandler:
                            vmState.Register(instruction.DestinationSlot).CreateMessageSignature(vmState.Binary.Uint16ConstantTable.Resolve(instruction.ListIndex), vmState, session);
                            break;
                        case LoadMessage:
                            vmState.Register(instruction.DestinationSlot).CreateMessage(vmState.Binary.Uint16ConstantTable.Resolve(instruction.SecondaryListIndex), vmState.Binary.Uint16ConstantTable.Resolve(instruction.ListIndex));
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
                            vmState.Register(instruction.DestinationSlot).GesVmCreateDice(instruction.Count, instruction.ImmediateY, vmState, session);
                            break;
                        case CreateVector:
                            vmState.Register(instruction.DestinationSlot).GesVmCreateVector(instruction.ImmediateX, vmState);
                            vmState.ClearStage();
                            break;
                        case CreatePoint:
                            vmState.Register(instruction.DestinationSlot).GesVmCreatePoint(instruction.ImmediateX, vmState);
                            vmState.ClearStage();
                            break;
                        case CreateList:
                            vmState.Register(instruction.DestinationSlot).GesVmCreateList();
                            vmState.ClearStage();
                            break;
                        case CreateMap:
                            vmState.Register(instruction.DestinationSlot).GesVmCreateMap(instruction.SecondaryListIndex);
                            vmState.ClearStage();
                            break;
                        case CreateRange:
                            vmState.Register(instruction.DestinationSlot).GesVmCreateRange(ref vmState.Register(instruction.XSlot), ref vmState.Register(instruction.YSlot));
                            break;
                        case CreateRangeWithStep:
                            vmState.Register(instruction.DestinationSlot).GesVmCreateRange(ref vmState.Register(instruction.XSlot), ref vmState.Register(instruction.YSlot), ref vmState.Register(instruction.AU));
                            break;
                        case CreateRangeIterator:
                            vmState.Register(instruction.DestinationSlot).GesVmCreateRangeStream(ref vmState.Register(instruction.XSlot), ref vmState.Register(instruction.YSlot), session);
                            break;
                        case CreateRangeIteratorWithStep:
                            vmState.Register(instruction.DestinationSlot).GesVmCreateRangeStream(ref vmState.Register(instruction.XSlot), ref vmState.Register(instruction.YSlot), ref vmState.Register(instruction.AU), session);
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
                            vmState.Register(instruction.DestinationSlot).GesVmCreateExternalType(instruction.BindId, instruction.ListIndex, externalTypeRegistry);
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
                            vmState.Register(instruction.DestinationSlot).GesVmOr(ref vmState.Register(instruction.XSlot), ref vmState.Register(instruction.YSlot));
                            break;
                        case And:
                            vmState.Register(instruction.DestinationSlot).GesVmAnd(ref vmState.Register(instruction.XSlot), ref vmState.Register(instruction.YSlot));
                            break;
                        case Xor:
                            vmState.Register(instruction.DestinationSlot).GesVmXor(ref vmState.Register(instruction.XSlot), ref vmState.Register(instruction.YSlot));
                            break;
                        case Implies:
                            vmState.Register(instruction.DestinationSlot).GesVmImplies(ref vmState.Register(instruction.XSlot), ref vmState.Register(instruction.YSlot));
                            break;
                        case Not:
                            vmState.Register(instruction.DestinationSlot).GesVmNot(ref vmState.Register(instruction.XSlot));
                            break;
                        case Equal:
                            vmState.Register(instruction.DestinationSlot).GesVmEqual(ref vmState.Register(instruction.XSlot), ref vmState.Register(instruction.YSlot));
                            break;
                        case NotEqual:
                            vmState.Register(instruction.DestinationSlot).GesVmNotEqual(ref vmState.Register(instruction.XSlot), ref vmState.Register(instruction.YSlot));
                            break;
                        case Less:
                            vmState.Register(instruction.DestinationSlot).GesVmLess(ref vmState.Register(instruction.XSlot), ref vmState.Register(instruction.YSlot));
                            break;
                        case Greater:
                            vmState.Register(instruction.DestinationSlot).GesVmGreater(ref vmState.Register(instruction.XSlot), ref vmState.Register(instruction.YSlot));
                            break;
                        case LessOrEqual:
                            vmState.Register(instruction.DestinationSlot).GesVmLessOrEqual(ref vmState.Register(instruction.XSlot), ref vmState.Register(instruction.YSlot));
                            break;
                        case GreaterOrEqual:
                            vmState.Register(instruction.DestinationSlot).GesVmGreaterOrEqual(ref vmState.Register(instruction.XSlot), ref vmState.Register(instruction.YSlot));
                            break;
                        case Add:
                            vmState.Register(instruction.DestinationSlot).GesVmAdd(ref vmState.Register(instruction.XSlot), ref vmState.Register(instruction.YSlot), ref vmState.Binary.TextConstantTable);
                            break;
                        case Subtract:
                            vmState.Register(instruction.DestinationSlot).GesVmSubtract(ref vmState.Register(instruction.XSlot), ref vmState.Register(instruction.YSlot), ref vmState.Binary.TextConstantTable);
                            break;
                        case Multiply:
                            vmState.Register(instruction.DestinationSlot).GesVmMultiply(ref vmState.Register(instruction.XSlot), ref vmState.Register(instruction.YSlot), ref vmState.Binary.TextConstantTable);
                            break;
                        case Divide:
                            vmState.Register(instruction.DestinationSlot).GesVmDivide(ref vmState.Register(instruction.XSlot), ref vmState.Register(instruction.YSlot), ref vmState.Binary.TextConstantTable);
                            break;
                        case Power:
                            vmState.Register(instruction.DestinationSlot).GesVmPower(ref vmState.Register(instruction.XSlot), ref vmState.Register(instruction.YSlot), ref vmState.Binary.TextConstantTable);
                            break;
                        case IntegerDivide:
                            vmState.Register(instruction.DestinationSlot).GesVmFloorDivide(ref vmState.Register(instruction.XSlot), ref vmState.Register(instruction.YSlot), ref vmState.Binary.TextConstantTable);
                            break;
                        case Modulo:
                            vmState.Register(instruction.DestinationSlot).GesVmModulo(ref vmState.Register(instruction.XSlot), ref vmState.Register(instruction.YSlot), ref vmState.Binary.TextConstantTable);
                            break;
                        case Remainder:
                            vmState.Register(instruction.DestinationSlot).GesVmRemainder(ref vmState.Register(instruction.XSlot), ref vmState.Register(instruction.YSlot), ref vmState.Binary.TextConstantTable);
                            break;
                        case Min:
                            vmState.Register(instruction.DestinationSlot).GesVmMin(ref vmState.Register(instruction.XSlot), ref vmState.Register(instruction.YSlot), ref vmState.Binary.TextConstantTable);
                            break;
                        case Max:
                            vmState.Register(instruction.DestinationSlot).GesVmMax(ref vmState.Register(instruction.XSlot), ref vmState.Register(instruction.YSlot), ref vmState.Binary.TextConstantTable);
                            break;
                        case Negate:
                            vmState.Register(instruction.DestinationSlot).GesVmNegate(ref vmState.Register(instruction.XSlot), ref vmState.Binary.TextConstantTable);
                            break;
                        case Abs:
                            vmState.Register(instruction.DestinationSlot).GesVmAbs(ref vmState.Register(instruction.XSlot), ref vmState.Binary.TextConstantTable);
                            break;
                        case LogN:
                            vmState.Register(instruction.DestinationSlot).GesVmNaturalLog(ref vmState.Register(instruction.XSlot), ref vmState.Binary.TextConstantTable);
                            break;
                        case Chance:
                            vmState.Register(instruction.DestinationSlot).GesVmChance(ref vmState.Register(instruction.XSlot));
                            break;
                        case Clamp:
                            vmState.Register(instruction.DestinationSlot).GesVmClamp(ref vmState.Register(instruction.XSlot), ref vmState.Register(instruction.YSlot), ref vmState.Register(instruction.AU), ref vmState.Binary.TextConstantTable);
                            break;
                        case RandomTake:
                            vmState.Register(instruction.DestinationSlot).GesVmRandom(ref vmState.Register(instruction.XSlot), ref vmState.Register(instruction.YSlot), vmState.RandomGenerator, ref vmState.Binary.TextConstantTable);
                            break;
                        case RandomTakeFloat:
                            vmState.Register(instruction.DestinationSlot).GesVmRandomFloat(ref vmState.Register(instruction.XSlot), ref vmState.Register(instruction.YSlot), vmState.RandomGenerator);
                            break;
                        case RandomPush:
                            var seed = vmState.Register(instruction.XSlot);
                            vmState.PushRandom(seed.Kind == Integer ? GameEventScriptRandomGenerator.FromSeed(seed.IntegerValue) : vmState.RandomGenerator);
                            break;
                        case RandomPushConstant:
                            vmState.PushRandom(GameEventScriptRandomGenerator.FromSeed(instruction.I64));
                            break;
                        case RandomPop:
                            vmState.PopRandom();
                            break;
                        case Term:
                            vmState.Register(instruction.DestinationSlot).GesVmTerm(ref vmState.Register(instruction.XSlot), ref vmState.Register(instruction.YSlot));
                            break;

                        #endregion

                        #region Group 3 - text, collection, streams

                        case TakeFirst:
                            vmState.Register(instruction.DestinationSlot).GesVmTakeFirst(ref vmState.Register(instruction.XSlot), instruction.ImmediateY);
                            break;
                        case DropFirst:
                            vmState.Register(instruction.DestinationSlot).GesVmDropFirst(ref vmState.Register(instruction.XSlot), instruction.ImmediateY);
                            break;
                        case TakeLast:
                            vmState.Register(instruction.DestinationSlot).GesVmTakeLast(ref vmState.Register(instruction.XSlot), instruction.ImmediateY);
                            break;
                        case DropLast:
                            vmState.Register(instruction.DestinationSlot).GesVmDropLast(ref vmState.Register(instruction.XSlot), instruction.ImmediateY);
                            break;
                        case TakeHighest:
                            vmState.Register(instruction.DestinationSlot).GesVmTakeHighest(ref vmState.Register(instruction.XSlot), instruction.ImmediateY);
                            break;
                        case TakeLowest:
                            vmState.Register(instruction.DestinationSlot).GesVmTakeLowest(ref vmState.Register(instruction.XSlot), instruction.ImmediateY);
                            break;
                        case DropHighest:
                            vmState.Register(instruction.DestinationSlot).GesVmDropHighest(ref vmState.Register(instruction.XSlot), instruction.ImmediateY);
                            break;
                        case DropLowest:
                            vmState.Register(instruction.DestinationSlot).GesVmDropLowest(ref vmState.Register(instruction.XSlot), instruction.ImmediateY);
                            break;
                        case OneRandom:
                            vmState.Register(instruction.DestinationSlot).GesVmOneRandom(ref vmState.Register(instruction.XSlot), vmState.RandomGenerator);
                            break;
                        case TakeRandom:
                            vmState.Register(instruction.DestinationSlot).GesVmTakeRandom(ref vmState.Register(instruction.XSlot), instruction.ImmediateY, vmState.RandomGenerator);
                            break;
                        case Length:
                            vmState.Register(instruction.DestinationSlot).GesVmLength(ref vmState.Register(instruction.XSlot), ref vmState.Binary.TextConstantTable);
                            break;
                        case StartsWith:
                            vmState.Register(instruction.DestinationSlot).GesVmStartsWith(ref vmState.Register(instruction.XSlot), ref vmState.Register(instruction.YSlot), ref vmState.Binary.TextConstantTable);
                            break;
                        case EndsWith:
                            vmState.Register(instruction.DestinationSlot).GesVmEndsWith(ref vmState.Register(instruction.XSlot), ref vmState.Register(instruction.YSlot), ref vmState.Binary.TextConstantTable);
                            break;
                        case Contains:
                            vmState.Register(instruction.DestinationSlot).GesVmContains(ref vmState.Register(instruction.XSlot), ref vmState.Register(instruction.YSlot), ref vmState.Binary.TextConstantTable);
                            break;
                        case ContainsAny:
                            vmState.Register(instruction.DestinationSlot).GesVmContainsAny(ref vmState.Register(instruction.XSlot), ref vmState.Register(instruction.YSlot), ref vmState.Binary.TextConstantTable);
                            break;
                        case ContainsAll:
                            vmState.Register(instruction.DestinationSlot).GesVmContainsAll(ref vmState.Register(instruction.XSlot), ref vmState.Register(instruction.YSlot), ref vmState.Binary.TextConstantTable);
                            break;
                        case ContainsValue:
                            vmState.Register(instruction.DestinationSlot).GesVmContainsValue(ref vmState.Register(instruction.XSlot), ref vmState.Register(instruction.YSlot), ref vmState.Binary.TextConstantTable);
                            break;
                        case Union:
                            vmState.Register(instruction.DestinationSlot).GesVmUnion(ref vmState.Register(instruction.XSlot), ref vmState.Register(instruction.YSlot), ref vmState.Binary.TextConstantTable);
                            break;
                        case Intersect:
                            vmState.Register(instruction.DestinationSlot).GesVmIntersect(ref vmState.Register(instruction.XSlot), ref vmState.Register(instruction.YSlot), ref vmState.Binary.TextConstantTable);
                            break;
                        case Zip:
                            vmState.Register(instruction.DestinationSlot).GesVmZip(ref vmState.Register(instruction.XSlot), ref vmState.Register(instruction.YSlot), ref vmState.Binary.TextConstantTable);
                            break;
                        case KeysOfMap:
                            vmState.Register(instruction.DestinationSlot).GesVmKeys(ref vmState.Register(instruction.XSlot));
                            break;
                        case ValuesOfMap:
                            vmState.Register(instruction.DestinationSlot).GesVmValues(ref vmState.Register(instruction.XSlot));
                            break;
                        case EntriesOfMap:
                            vmState.Register(instruction.DestinationSlot).GesVmEntries(ref vmState.Register(instruction.XSlot));
                            break;
                        case GameEventScriptBytecodeOpCode.First:
                            vmState.Register(instruction.DestinationSlot).GesVmFirst(ref vmState.Register(instruction.XSlot));
                            break;
                        case GameEventScriptBytecodeOpCode.Last:
                            vmState.Register(instruction.DestinationSlot).GesVmLast(ref vmState.Register(instruction.XSlot));
                            break;
                        case GameEventScriptBytecodeOpCode.Single:
                            vmState.Register(instruction.DestinationSlot).GesVmSingle(ref vmState.Register(instruction.XSlot));
                            break;
                        case StreamCreate:
                            vmState.Register(instruction.DestinationSlot).GesVmStreamCreate(ref vmState.Register(instruction.XSlot));
                            break;
                        case StreamNext:
                        {
                            vmState.Register(instruction.DestinationSlot).GesVmStreamNext(ref vmState.Register(instruction.XSlot), instruction.TargetAddress);
                            if (vmState.Register(instruction.DestinationSlot).Kind is not Nothing)
                            {
                                session.RuntimeBudget.TryConsumeLoopIteration("For loop iteration exceeds the configured limit.");
                            }
                            break;
                        }
                        case StreamClose:
                            vmState.Register(instruction.XSlot).GesVmStreamClose();
                            break;
                        case StreamMap:
                            vmState.Register(instruction.DestinationSlot).GesVmStreamMap(ref vmState.Register(instruction.XSlot), instruction.EntryAddress, instruction.AU, instruction.BU, this);
                            break;
                        case StreamFilter:
                            vmState.Register(instruction.DestinationSlot).GesVmStreamFilter(ref vmState.Register(instruction.XSlot), instruction.EntryAddress, instruction.AU, instruction.BU, this);
                            break;
                        case StreamCount:
                            vmState.Register(instruction.DestinationSlot).GesVmStreamCount(ref vmState.Register(instruction.XSlot), instruction.DestinationSlot);
                            break;
                        case StreamSum:
                            vmState.Register(instruction.DestinationSlot).GesVmStreamSum(ref vmState.Register(instruction.XSlot));
                            break;
                        case StreamAverage:
                            vmState.Register(instruction.DestinationSlot).GesVmStreamAverage(ref vmState.Register(instruction.XSlot));
                            break;
                        case StreamMin:
                        {
                            var dst = vmState.CreateNothing();
                            GesVmRegisterStreamTerminals.GesVmStreamMin(ref dst, ref vmState.Register(instruction.XSlot), instruction.YSlot, instruction.AU, this);
                            vmState.Register(instruction.DestinationSlot) = dst;
                            break;
                        }
                        case StreamMax:
                        {
                            var dst = vmState.CreateNothing();
                            GesVmRegisterStreamTerminals.GesVmStreamMax(ref dst, ref vmState.Register(instruction.XSlot), instruction.YSlot, instruction.AU, this);
                            vmState.Register(instruction.DestinationSlot) = dst;
                            break;
                        }
                        case StreamOneWeighted:
                        {
                            var dst = vmState.CreateNothing();
                            GesVmRegisterStreamTerminals.GesVmStreamOneWeighted(ref dst, ref vmState.Register(instruction.XSlot), instruction.AU, instruction.BU, instruction.CU, this, vmState.RandomGenerator);
                            vmState.Register(instruction.DestinationSlot) = dst;
                            break;
                        }
                        case StreamTakeWeighted:
                        {
                            var dst = vmState.CreateNothing();
                            GesVmRegisterStreamTerminals.GesVmStreamTakeWeighted(ref dst, ref vmState.Register(instruction.XSlot), instruction.ImmediateY, instruction.AU, instruction.BU, instruction.CU, this, vmState.RandomGenerator);
                            vmState.Register(instruction.DestinationSlot) = dst;
                            break;
                        }
                        case StreamCollectList:
                            vmState.Register(instruction.DestinationSlot).GesVmStreamCollectList(ref vmState.Register(instruction.XSlot));
                            break;
                        case StreamCollectMap:
                        {
                            var dst = vmState.CreateNothing();
                            dst.GesVmStreamCollectMap(ref vmState.Register(instruction.XSlot), instruction.YSlot, instruction.AU, this);
                            vmState.Register(instruction.DestinationSlot) = dst;
                            break;
                        }
                        case StreamCollectMapValue:
                        {
                            var dst = vmState.CreateNothing();
                            dst.GesVmStreamCollectMapValue(ref vmState.Register(instruction.XSlot), instruction.YSlot, instruction.AU, instruction.BU, this);
                            vmState.Register(instruction.DestinationSlot) = dst;
                            break;
                        }
                        case HasAny:
                            vmState.Register(instruction.DestinationSlot).GesVmHasAny(ref vmState.Register(instruction.XSlot));
                            break;
                        case HasAll:
                            vmState.Register(instruction.DestinationSlot).GesVmHasAll(ref vmState.Register(instruction.XSlot));
                            break;
                        case Distinct:
                            vmState.Register(instruction.DestinationSlot).GesVmDistinct(ref vmState.Register(instruction.XSlot));
                            break;
                        case DistinctBy:
                            vmState.Register(instruction.DestinationSlot).GesVmDistinctBy(ref vmState.Register(instruction.XSlot), instruction.YSlot, instruction.AU, this, instruction.DestinationSlot);
                            break;
                        case GroupBy:
                            vmState.Register(instruction.DestinationSlot).GesVmGroupBy(ref vmState.Register(instruction.XSlot), instruction.YSlot, instruction.AU, this, instruction.DestinationSlot);
                            break;
                        case SortAscending:
                            vmState.Register(instruction.DestinationSlot).GesVmSortAscending(ref vmState.Register(instruction.XSlot));
                            break;
                        case SortDescending:
                            vmState.Register(instruction.DestinationSlot).GesVmSortDescending(ref vmState.Register(instruction.XSlot));
                            break;
                        case OrderByAscending:
                            vmState.Register(instruction.DestinationSlot).GesVmOrderByAscending(ref vmState.Register(instruction.XSlot), instruction.YSlot, instruction.AU, this, instruction.DestinationSlot);
                            break;
                        case OrderByDescending:
                            vmState.Register(instruction.DestinationSlot).GesVmOrderByDescending(ref vmState.Register(instruction.XSlot), instruction.YSlot, instruction.AU, this, instruction.DestinationSlot);
                            break;
                        case Reverse:
                            vmState.Register(instruction.DestinationSlot).GesVmReverse(ref vmState.Register(instruction.XSlot));
                            break;
                        case Shuffle:
                            vmState.Register(instruction.DestinationSlot).GesVmShuffle(ref vmState.Register(instruction.XSlot), vmState.RandomGenerator);
                            break;
                        case ListBuilderCreate:
                            vmState.Register(instruction.DestinationSlot).GesVmCreateListBuilder();
                            break;
                        case ListBuilderAdd:
                            vmState.Register(instruction.XSlot).GesVmListBuilderAdd(ref vmState.Register(instruction.YSlot));
                            break;
                        case ListBuilderFinish:
                            vmState.Register(instruction.DestinationSlot).GesVmListBuilderFinish(ref vmState.Register(instruction.XSlot));
                            break;
                        case HasPattern:
                            vmState.Register(instruction.DestinationSlot).GesVmHasPattern(ref vmState.Register(instruction.XSlot), (GameEventScriptBytecodePatternKind)instruction.AU, instruction.ImmediateY, instruction.BU, this, instruction.DestinationSlot);
                            break;
                        case TakePattern:
                            vmState.Register(instruction.DestinationSlot).GesVmTakePattern(ref vmState.Register(instruction.XSlot), (GameEventScriptBytecodePatternKind)instruction.AU, instruction.ImmediateY, instruction.BU, this, instruction.DestinationSlot);
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
