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
using static StepH.GameEventScript.BytecodeExecutor.VmState.StateValue;

namespace StepH.GameEventScript.BytecodeExecutor;

public class GameEventScriptVmException(string message) : GameEventScriptFatalRuntimeException(message);

public class GameEventScriptVirtualMaschine : IGameEventScriptModule
{
    private readonly VmState _vmState;
    private IGameEventScriptExternalTypeRegistry _externalTypeRegistry = GameEventScriptEmptyExternalTypeRegistry.Instance;

    public string ModuleName { get; }
    public IEnumerable<GameEventScriptMessageHandlerDescriptor> Handlers { get; }
    public string? DebugScriptSource { get; set; }

    public string DumpState(string? scriptSource = null, bool includeInstructionAddresses = true)
        => _vmState.Dump(includeInstructionAddresses, scriptSource ?? DebugScriptSource);

    public void Bind(IGameEventScriptExtensionRegistry extensionRegistry, IGameEventScriptExternalTypeRegistry typeRegistry)
    {
        _externalTypeRegistry = typeRegistry ?? GameEventScriptEmptyExternalTypeRegistry.Instance;
    }
    
    public static GameEventScriptVirtualMaschine Create(GameEventScriptBinary binary, ushort registerSize, ushort stackSize)
    {
        return new GameEventScriptVirtualMaschine(binary, registerSize, stackSize);
    }

    private GameEventScriptVirtualMaschine(GameEventScriptBinary binary, ushort registerSize, ushort stackSize)
    {
        ModuleName = binary.ModuleName;
        _vmState = new VmState(binary, registerSize, stackSize);
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

    private class Runner(VmState vmState, GameEventScriptSession session, IGameEventScriptExternalTypeRegistry externalTypeRegistry) : IGameEventScriptMessageInvocation, IVmStreamEntryEvaluator
    {
        public bool IsCompleted { get; private set; } = false;

        public bool TryEvaluateStreamEntry(ushort entryAddress, ushort itemSlot, ref VmValue item, VmValue[]? captures, ref VmValue result)
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
                while (!IsCompleted && vmState.State == Processing && opcodesExecuted < maxSteps)
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
                            vmState.Register(instruction.DestinationSlot).VmCallStandard(instruction.SecondaryListIndex, instruction.ListIndex, instruction.HasInstructionFlag(GameEventScriptInstructionFlag.NormalizeResultAsPredicate));
                            break;
                        case CallExternal:
                            vmState.Register(instruction.DestinationSlot).VmCallExternal(instruction.BindId, instruction.ListIndex, session, instruction.HasInstructionFlag(GameEventScriptInstructionFlag.NormalizeResultAsPredicate));
                            break;

                        case ReturnVoid:
                            vmState.ReturnVoid();
                            break;
                        case ReturnValue:
                            vmState.ReturnValue(instruction.XSlot);
                            break;

                        case EmitMessage:
                            vmState.VmPublishMessage(instruction.MessageDestination, vmState.Binary.Uint16ConstantTable.Resolve(instruction.ListIndex), false, session);
                            break;
                        case EmitMessageWithTags:
                            vmState.VmPublishMessageWithTags(instruction.MessageDestination, vmState.Binary.Uint16ConstantTable.Resolve(instruction.ListIndex),
                                vmState.Binary.Uint16ConstantTable.Resolve(instruction.SecondaryListIndex), false, session);
                            break;
                        case EmitMessageValue:
                            vmState.VmPublishMessageValue(ref vmState.Register(instruction.XSlot), false, session);
                            break;
                        case EmitMessageValueWithTags:
                            vmState.VmPublishMessageValueWithTags(ref vmState.Register(instruction.XSlot), vmState.Binary.Uint16ConstantTable.Resolve(instruction.ListIndex), false, session);
                            break;

                        case PublishMessage:
                            vmState.VmPublishMessage(instruction.MessageDestination, vmState.Binary.Uint16ConstantTable.Resolve(instruction.ListIndex), true, session);
                            break;
                        case PublishMessageWithTags:
                            vmState.VmPublishMessageWithTags(instruction.MessageDestination, vmState.Binary.Uint16ConstantTable.Resolve(instruction.ListIndex),
                                vmState.Binary.Uint16ConstantTable.Resolve(instruction.SecondaryListIndex), true, session);
                            break;
                        case PublishMessageValue:
                            vmState.VmPublishMessageValue(ref vmState.Register(instruction.XSlot), true, session);
                            break;
                        case PublishMessageValueWithTags:
                            vmState.VmPublishMessageValueWithTags(ref vmState.Register(instruction.XSlot), vmState.Binary.Uint16ConstantTable.Resolve(instruction.ListIndex), true, session);
                            break;

                        case Cast:
                            vmState.Register(instruction.DestinationSlot).VmCast(ref vmState.Register(instruction.XSlot), instruction.TypeKind);
                            break;
                        case CastCustom:
                            vmState.Register(instruction.DestinationSlot).VmCastCustom(ref vmState.Register(instruction.XSlot), instruction.SecondaryStringIndex, instruction.DestinationSlot);
                            break;
                        case CastUnit:
                            vmState.Register(instruction.DestinationSlot).VmCastUnit(ref vmState.Register(instruction.XSlot), instruction.Unit);
                            break;
                        case CastNumeric:
                            vmState.Register(instruction.DestinationSlot).VmCastNumeric(ref vmState.Register(instruction.XSlot));
                            break;

                        case CheckType:
                            vmState.Register(instruction.DestinationSlot).VmCheckType(ref vmState.Register(instruction.XSlot), instruction.TypeKind);
                            break;
                        case CheckCustomType:
                            vmState.Register(instruction.DestinationSlot).VmCheckCustomType(ref vmState.Register(instruction.XSlot), instruction.SecondaryStringIndex);
                            break;
                        case CheckUnit:
                            vmState.Register(instruction.DestinationSlot).VmCheckUnit(ref vmState.Register(instruction.XSlot), instruction.Unit);
                            break;
                        case CheckNumeric:
                            vmState.Register(instruction.DestinationSlot).VmCheckNumeric(ref vmState.Register(instruction.XSlot));
                            break;
                        case CheckInteger:
                            vmState.Register(instruction.DestinationSlot).VmCheckInteger(ref vmState.Register(instruction.XSlot));
                            break;
                        case CheckFractional:
                            vmState.Register(instruction.DestinationSlot).VmCheckFractional(ref vmState.Register(instruction.XSlot));
                            break;

                        case MoveSlot:
                            vmState.Register(instruction.DestinationSlot) = vmState.Register(instruction.XSlot);
                            break;
                        case MemberAccess:
                            vmState.Register(instruction.DestinationSlot).VmMemberAccess(instruction.StringIndex, ref vmState.Register(instruction.YSlot));
                            break;
                        case IndexAccess:
                            vmState.Register(instruction.DestinationSlot).VmIndexAccess(instruction.Index, ref vmState.Register(instruction.YSlot));
                            break;
                        case PropertyAccess:
                            vmState.Register(instruction.DestinationSlot).VmPropertyAccess(ref vmState.Register(instruction.XSlot), ref vmState.Register(instruction.YSlot));
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
                            vmState.Register(instruction.DestinationSlot).VmCreateDice(instruction.Count, instruction.ImmediateY, vmState, session);
                            break;
                        case CreateVector:
                            vmState.Register(instruction.DestinationSlot).VmCreateVector(instruction.ImmediateX, vmState);
                            vmState.ClearStage();
                            break;
                        case CreatePoint:
                            vmState.Register(instruction.DestinationSlot).VmCreatePoint(instruction.ImmediateX, vmState);
                            vmState.ClearStage();
                            break;
                        case CreateList:
                            vmState.Register(instruction.DestinationSlot).VmCreateList();
                            vmState.ClearStage();
                            break;
                        case CreateMap:
                            vmState.Register(instruction.DestinationSlot).VmCreateMap(instruction.SecondaryListIndex);
                            vmState.ClearStage();
                            break;
                        case CreateRange:
                            vmState.Register(instruction.DestinationSlot).VmCreateRange(ref vmState.Register(instruction.XSlot), ref vmState.Register(instruction.YSlot));
                            break;
                        case CreateRangeWithStep:
                            vmState.Register(instruction.DestinationSlot).VmCreateRange(ref vmState.Register(instruction.XSlot), ref vmState.Register(instruction.YSlot), ref vmState.Register(instruction.AU));
                            break;
                        case CreateRangeIterator:
                            vmState.Register(instruction.DestinationSlot).VmCreateRangeStream(ref vmState.Register(instruction.XSlot), ref vmState.Register(instruction.YSlot));
                            break;
                        case CreateRangeIteratorWithStep:
                            vmState.Register(instruction.DestinationSlot).VmCreateRangeStream(ref vmState.Register(instruction.XSlot), ref vmState.Register(instruction.YSlot), ref vmState.Register(instruction.AU));
                            break;
                        case CreateRangeIteratorShort:
                            vmState.Register(instruction.DestinationSlot).SetStream(new VmIntegerRangeStream(instruction.ImmediateX, instruction.ImmediateY, instruction.AS));
                            break;
                        case CreateRecord:
                            vmState.CallRecordConstructor(instruction.BindId, instruction.DestinationSlot);
                            break;
                        case CreateExternalType:
                            vmState.Register(instruction.DestinationSlot).VmCreateExternalType(instruction.BindId, instruction.ListIndex, externalTypeRegistry);
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
                            vmState.Register(instruction.DestinationSlot).VmOr(ref vmState.Register(instruction.XSlot), ref vmState.Register(instruction.YSlot));
                            break;
                        case And:
                            vmState.Register(instruction.DestinationSlot).VmAnd(ref vmState.Register(instruction.XSlot), ref vmState.Register(instruction.YSlot));
                            break;
                        case Xor:
                            vmState.Register(instruction.DestinationSlot).VmXor(ref vmState.Register(instruction.XSlot), ref vmState.Register(instruction.YSlot));
                            break;
                        case Implies:
                            vmState.Register(instruction.DestinationSlot).VmImplies(ref vmState.Register(instruction.XSlot), ref vmState.Register(instruction.YSlot));
                            break;
                        case Not:
                            vmState.Register(instruction.DestinationSlot).VmNot(ref vmState.Register(instruction.XSlot));
                            break;
                        case Equal:
                            vmState.Register(instruction.DestinationSlot).VmEqual(ref vmState.Register(instruction.XSlot), ref vmState.Register(instruction.YSlot));
                            break;
                        case NotEqual:
                            vmState.Register(instruction.DestinationSlot).VmNotEqual(ref vmState.Register(instruction.XSlot), ref vmState.Register(instruction.YSlot));
                            break;
                        case Less:
                            vmState.Register(instruction.DestinationSlot).VmLess(ref vmState.Register(instruction.XSlot), ref vmState.Register(instruction.YSlot));
                            break;
                        case Greater:
                            vmState.Register(instruction.DestinationSlot).VmGreater(ref vmState.Register(instruction.XSlot), ref vmState.Register(instruction.YSlot));
                            break;
                        case LessOrEqual:
                            vmState.Register(instruction.DestinationSlot).VmLessOrEqual(ref vmState.Register(instruction.XSlot), ref vmState.Register(instruction.YSlot));
                            break;
                        case GreaterOrEqual:
                            vmState.Register(instruction.DestinationSlot).VmGreaterOrEqual(ref vmState.Register(instruction.XSlot), ref vmState.Register(instruction.YSlot));
                            break;
                        case Add:
                            vmState.Register(instruction.DestinationSlot).VmAdd(ref vmState.Register(instruction.XSlot), ref vmState.Register(instruction.YSlot), ref vmState.Binary.TextConstantTable);
                            break;
                        case Subtract:
                            vmState.Register(instruction.DestinationSlot).VmSubtract(ref vmState.Register(instruction.XSlot), ref vmState.Register(instruction.YSlot), ref vmState.Binary.TextConstantTable);
                            break;
                        case Multiply:
                            vmState.Register(instruction.DestinationSlot).VmMultiply(ref vmState.Register(instruction.XSlot), ref vmState.Register(instruction.YSlot), ref vmState.Binary.TextConstantTable);
                            break;
                        case Divide:
                            vmState.Register(instruction.DestinationSlot).VmDivide(ref vmState.Register(instruction.XSlot), ref vmState.Register(instruction.YSlot), ref vmState.Binary.TextConstantTable);
                            break;
                        case Power:
                            vmState.Register(instruction.DestinationSlot).VmPower(ref vmState.Register(instruction.XSlot), ref vmState.Register(instruction.YSlot), ref vmState.Binary.TextConstantTable);
                            break;
                        case IntegerDivide:
                            vmState.Register(instruction.DestinationSlot).VmFloorDivide(ref vmState.Register(instruction.XSlot), ref vmState.Register(instruction.YSlot), ref vmState.Binary.TextConstantTable);
                            break;
                        case Modulo:
                            vmState.Register(instruction.DestinationSlot).VmModulo(ref vmState.Register(instruction.XSlot), ref vmState.Register(instruction.YSlot), ref vmState.Binary.TextConstantTable);
                            break;
                        case Remainder:
                            vmState.Register(instruction.DestinationSlot).VmRemainder(ref vmState.Register(instruction.XSlot), ref vmState.Register(instruction.YSlot), ref vmState.Binary.TextConstantTable);
                            break;
                        case Min:
                            vmState.Register(instruction.DestinationSlot).VmMin(ref vmState.Register(instruction.XSlot), ref vmState.Register(instruction.YSlot), ref vmState.Binary.TextConstantTable);
                            break;
                        case Max:
                            vmState.Register(instruction.DestinationSlot).VmMax(ref vmState.Register(instruction.XSlot), ref vmState.Register(instruction.YSlot), ref vmState.Binary.TextConstantTable);
                            break;
                        case Negate:
                            vmState.Register(instruction.DestinationSlot).VmNegate(ref vmState.Register(instruction.XSlot), ref vmState.Binary.TextConstantTable);
                            break;
                        case Abs:
                            vmState.Register(instruction.DestinationSlot).VmAbs(ref vmState.Register(instruction.XSlot), ref vmState.Binary.TextConstantTable);
                            break;
                        case LogN:
                            vmState.Register(instruction.DestinationSlot).VmNaturalLog(ref vmState.Register(instruction.XSlot), ref vmState.Binary.TextConstantTable);
                            break;
                        case Chance:
                            vmState.Register(instruction.DestinationSlot).VmChance(ref vmState.Register(instruction.XSlot));
                            break;
                        case Clamp:
                            vmState.Register(instruction.DestinationSlot).VmClamp(ref vmState.Register(instruction.XSlot), ref vmState.Register(instruction.YSlot), ref vmState.Register(instruction.AU), ref vmState.Binary.TextConstantTable);
                            break;
                        case RandomTake:
                            vmState.Register(instruction.DestinationSlot).VmRandom(ref vmState.Register(instruction.XSlot), ref vmState.Register(instruction.YSlot), vmState.RandomGenerator, ref vmState.Binary.TextConstantTable);
                            break;
                        case RandomPush:
                            var seed = vmState.Register(instruction.XSlot);
                            vmState.PushRandom(seed.Kind == Integer ? GameEventScriptRandomGenerator.FromSeed((int)seed.IntegerValue) : vmState.RandomGenerator);
                            break;
                        case RandomPushConstant:
                            vmState.PushRandom(GameEventScriptRandomGenerator.FromSeed((int)instruction.I64));
                            break;
                        case RandomPop:
                            vmState.PopRandom();
                            break;
                        case Term:
                            vmState.Register(instruction.DestinationSlot).VmTerm(ref vmState.Register(instruction.XSlot), ref vmState.Register(instruction.YSlot));
                            break;

                        #endregion

                        #region Group 3 - text, collection, streams

                        case TakeFirst:
                            vmState.Register(instruction.DestinationSlot).VmTakeFirst(ref vmState.Register(instruction.XSlot), instruction.ImmediateY);
                            break;
                        case DropFirst:
                            vmState.Register(instruction.DestinationSlot).VmDropFirst(ref vmState.Register(instruction.XSlot), instruction.ImmediateY);
                            break;
                        case TakeLast:
                            vmState.Register(instruction.DestinationSlot).VmTakeLast(ref vmState.Register(instruction.XSlot), instruction.ImmediateY);
                            break;
                        case DropLast:
                            vmState.Register(instruction.DestinationSlot).VmDropLast(ref vmState.Register(instruction.XSlot), instruction.ImmediateY);
                            break;
                        case TakeHighest:
                            vmState.Register(instruction.DestinationSlot).VmTakeHighest(ref vmState.Register(instruction.XSlot), instruction.ImmediateY);
                            break;
                        case TakeLowest:
                            vmState.Register(instruction.DestinationSlot).VmTakeLowest(ref vmState.Register(instruction.XSlot), instruction.ImmediateY);
                            break;
                        case DropHighest:
                            vmState.Register(instruction.DestinationSlot).VmDropHighest(ref vmState.Register(instruction.XSlot), instruction.ImmediateY);
                            break;
                        case DropLowest:
                            vmState.Register(instruction.DestinationSlot).VmDropLowest(ref vmState.Register(instruction.XSlot), instruction.ImmediateY);
                            break;
                        case OneRandom:
                            vmState.Register(instruction.DestinationSlot).VmOneRandom(ref vmState.Register(instruction.XSlot), vmState.RandomGenerator);
                            break;
                        case TakeRandom:
                            vmState.Register(instruction.DestinationSlot).VmTakeRandom(ref vmState.Register(instruction.XSlot), instruction.ImmediateY, vmState.RandomGenerator);
                            break;
                        case Length:
                            vmState.Register(instruction.DestinationSlot).VmLength(ref vmState.Register(instruction.XSlot), ref vmState.Binary.TextConstantTable);
                            break;
                        case StartsWith:
                            vmState.Register(instruction.DestinationSlot).VmStartsWith(ref vmState.Register(instruction.XSlot), ref vmState.Register(instruction.YSlot), ref vmState.Binary.TextConstantTable);
                            break;
                        case EndsWith:
                            vmState.Register(instruction.DestinationSlot).VmEndsWith(ref vmState.Register(instruction.XSlot), ref vmState.Register(instruction.YSlot), ref vmState.Binary.TextConstantTable);
                            break;
                        case Contains:
                            vmState.Register(instruction.DestinationSlot).VmContains(ref vmState.Register(instruction.XSlot), ref vmState.Register(instruction.YSlot), ref vmState.Binary.TextConstantTable);
                            break;
                        case ContainsAny:
                            vmState.Register(instruction.DestinationSlot).VmContainsAny(ref vmState.Register(instruction.XSlot), ref vmState.Register(instruction.YSlot), ref vmState.Binary.TextConstantTable);
                            break;
                        case ContainsAll:
                            vmState.Register(instruction.DestinationSlot).VmContainsAll(ref vmState.Register(instruction.XSlot), ref vmState.Register(instruction.YSlot), ref vmState.Binary.TextConstantTable);
                            break;
                        case ContainsValue:
                            vmState.Register(instruction.DestinationSlot).VmContainsValue(ref vmState.Register(instruction.XSlot), ref vmState.Register(instruction.YSlot), ref vmState.Binary.TextConstantTable);
                            break;
                        case Union:
                            vmState.Register(instruction.DestinationSlot).VmUnion(ref vmState.Register(instruction.XSlot), ref vmState.Register(instruction.YSlot), ref vmState.Binary.TextConstantTable);
                            break;
                        case Intersect:
                            vmState.Register(instruction.DestinationSlot).VmIntersect(ref vmState.Register(instruction.XSlot), ref vmState.Register(instruction.YSlot), ref vmState.Binary.TextConstantTable);
                            break;
                        case Zip:
                            vmState.Register(instruction.DestinationSlot).VmZip(ref vmState.Register(instruction.XSlot), ref vmState.Register(instruction.YSlot), ref vmState.Binary.TextConstantTable);
                            break;
                        case KeysOfMap:
                            vmState.Register(instruction.DestinationSlot).VmKeys(ref vmState.Register(instruction.XSlot));
                            break;
                        case ValuesOfMap:
                            vmState.Register(instruction.DestinationSlot).VmValues(ref vmState.Register(instruction.XSlot));
                            break;
                        case EntriesOfMap:
                            vmState.Register(instruction.DestinationSlot).VmEntries(ref vmState.Register(instruction.XSlot));
                            break;
                        case GameEventScriptBytecodeOpCode.First:
                            vmState.Register(instruction.DestinationSlot).VmFirst(ref vmState.Register(instruction.XSlot));
                            break;
                        case GameEventScriptBytecodeOpCode.Last:
                            vmState.Register(instruction.DestinationSlot).VmLast(ref vmState.Register(instruction.XSlot));
                            break;
                        case GameEventScriptBytecodeOpCode.Single:
                            vmState.Register(instruction.DestinationSlot).VmSingle(ref vmState.Register(instruction.XSlot));
                            break;
                        case StreamCreate:
                            vmState.Register(instruction.DestinationSlot).VmStreamCreate(ref vmState.Register(instruction.XSlot));
                            break;
                        case StreamNext:
                            vmState.Register(instruction.DestinationSlot).VmStreamNext(ref vmState.Register(instruction.XSlot), instruction.TargetAddress);
                            break;
                        case StreamClose:
                            vmState.Register(instruction.XSlot).VmStreamClose();
                            break;
                        case StreamMap:
                            vmState.Register(instruction.DestinationSlot).VmStreamMap(ref vmState.Register(instruction.XSlot), instruction.EntryAddress, instruction.AU, instruction.BU, this);
                            break;
                        case StreamFilter:
                            vmState.Register(instruction.DestinationSlot).VmStreamFilter(ref vmState.Register(instruction.XSlot), instruction.EntryAddress, instruction.AU, instruction.BU, this);
                            break;
                        case StreamCount:
                            vmState.Register(instruction.DestinationSlot).VmStreamCount(ref vmState.Register(instruction.XSlot), instruction.DestinationSlot);
                            break;
                        case StreamSum:
                            vmState.Register(instruction.DestinationSlot).VmStreamSum(ref vmState.Register(instruction.XSlot));
                            break;
                        case StreamAverage:
                            vmState.Register(instruction.DestinationSlot).VmStreamAverage(ref vmState.Register(instruction.XSlot));
                            break;
                        case StreamMin:
                        {
                            var dst = vmState.CreateNothing();
                            VmRegisterStreamTerminals.VmStreamMin(ref dst, ref vmState.Register(instruction.XSlot), instruction.YSlot, instruction.AU, this);
                            vmState.Register(instruction.DestinationSlot) = dst;
                            break;
                        }
                        case StreamMax:
                        {
                            var dst = vmState.CreateNothing();
                            VmRegisterStreamTerminals.VmStreamMax(ref dst, ref vmState.Register(instruction.XSlot), instruction.YSlot, instruction.AU, this);
                            vmState.Register(instruction.DestinationSlot) = dst;
                            break;
                        }
                        case StreamOneWeighted:
                        {
                            var dst = vmState.CreateNothing();
                            VmRegisterStreamTerminals.VmStreamOneWeighted(ref dst, ref vmState.Register(instruction.XSlot), instruction.AU, instruction.BU, instruction.CU, this, vmState.RandomGenerator);
                            vmState.Register(instruction.DestinationSlot) = dst;
                            break;
                        }
                        case StreamTakeWeighted:
                        {
                            var dst = vmState.CreateNothing();
                            VmRegisterStreamTerminals.VmStreamTakeWeighted(ref dst, ref vmState.Register(instruction.XSlot), instruction.ImmediateY, instruction.AU, instruction.BU, instruction.CU, this, vmState.RandomGenerator);
                            vmState.Register(instruction.DestinationSlot) = dst;
                            break;
                        }
                        case StreamCollectList:
                            vmState.Register(instruction.DestinationSlot).VmStreamCollectList(ref vmState.Register(instruction.XSlot));
                            break;
                        case StreamCollectMap:
                            vmState.Register(instruction.DestinationSlot).VmStreamCollectMap(ref vmState.Register(instruction.XSlot), instruction.YSlot, instruction.AU, this);
                            break;
                        case StreamCollectMapValue:
                            vmState.Register(instruction.DestinationSlot).VmStreamCollectMapValue(ref vmState.Register(instruction.XSlot), instruction.YSlot, instruction.AU, instruction.BU, this);
                            break;
                        case HasAny:
                            vmState.Register(instruction.DestinationSlot).VmHasAny(ref vmState.Register(instruction.XSlot));
                            break;
                        case HasAll:
                            vmState.Register(instruction.DestinationSlot).VmHasAll(ref vmState.Register(instruction.XSlot));
                            break;
                        case Distinct:
                            vmState.Register(instruction.DestinationSlot).VmDistinct(ref vmState.Register(instruction.XSlot));
                            break;
                        case DistinctBy:
                            vmState.Register(instruction.DestinationSlot).VmDistinctBy(ref vmState.Register(instruction.XSlot), instruction.YSlot, instruction.AU, this, instruction.DestinationSlot);
                            break;
                        case GroupBy:
                            vmState.Register(instruction.DestinationSlot).VmGroupBy(ref vmState.Register(instruction.XSlot), instruction.YSlot, instruction.AU, this, instruction.DestinationSlot);
                            break;
                        case SortAscending:
                            vmState.Register(instruction.DestinationSlot).VmSortAscending(ref vmState.Register(instruction.XSlot));
                            break;
                        case SortDescending:
                            vmState.Register(instruction.DestinationSlot).VmSortDescending(ref vmState.Register(instruction.XSlot));
                            break;
                        case OrderByAscending:
                            vmState.Register(instruction.DestinationSlot).VmOrderByAscending(ref vmState.Register(instruction.XSlot), instruction.YSlot, instruction.AU, this, instruction.DestinationSlot);
                            break;
                        case OrderByDescending:
                            vmState.Register(instruction.DestinationSlot).VmOrderByDescending(ref vmState.Register(instruction.XSlot), instruction.YSlot, instruction.AU, this, instruction.DestinationSlot);
                            break;
                        case Reverse:
                            vmState.Register(instruction.DestinationSlot).VmReverse(ref vmState.Register(instruction.XSlot));
                            break;
                        case Shuffle:
                            vmState.Register(instruction.DestinationSlot).VmShuffle(ref vmState.Register(instruction.XSlot), vmState.RandomGenerator);
                            break;
                        case ListBuilderCreate:
                            vmState.Register(instruction.DestinationSlot).VmCreateListBuilder();
                            break;
                        case ListBuilderAdd:
                            vmState.Register(instruction.XSlot).VmListBuilderAdd(ref vmState.Register(instruction.YSlot));
                            break;
                        case ListBuilderFinish:
                            vmState.Register(instruction.DestinationSlot).VmListBuilderFinish(ref vmState.Register(instruction.XSlot));
                            break;
                        #endregion

                        #region Group 4 - pipeline terminals and transforms

                        case PipelineDicePatternCountAny:
                            // FIXME: creating the real custom type here
                            break;
                        case PipelineDicePatternCountFace:
                            // FIXME: creating the real custom type here
                            break;
                        case PipelineDicePatternFullHouse:
                            // FIXME: creating the real custom type here
                            break;
                        case PipelineDicePatternStraight:
                            // FIXME: creating the real custom type here
                            break;
                        case PipelineTakePatternCountAny:
                            // FIXME: creating the real custom type here
                            break;
                        case PipelineTakePatternCountFace:
                            // FIXME: creating the real custom type here
                            break;
                        case PipelineTakePatternFullHouse:
                            // FIXME: creating the real custom type here
                            break;
                        case PipelineTakePatternStraight:
                            // FIXME: creating the real custom type here
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
