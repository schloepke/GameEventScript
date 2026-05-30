#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using StepH.GameEventScript.Api;
using StepH.GameEventScript.Runtime;
using static StepH.GameEventScript.Api.GameEventScriptBytecodeOpCode;
using static StepH.GameEventScript.Api.GameEventScriptBytecodeTypeKind;

namespace StepH.GameEventScript.BytecodeExecutor;

public class GameEventScriptVirtualMaschine(GameEventScriptBinary binary, ushort registerSize, ushort stackSize)
{
    private readonly VmState _vmState = new(binary, registerSize, stackSize);

    public void Initialize(GameEventScriptSession session)
    {
        _vmState.Reset();
        var message = GameEventScriptSystemEndpoints.CreateInitializationMessage();
        if (!_vmState.HasMessageHandler(message))
        {
            return;
        }

        ExecuteMessage(message, session);
        _vmState.Reset();
    }

    public bool ExecuteMessage(GameEventScriptMessage message, GameEventScriptSession session)
    {
        if (!_vmState.PrepareMessage(message, session)) return false;
        _vmState.State = VmState.StateValue.Running;
        while (_vmState.State == VmState.StateValue.Running)
        {
            var instruction = _vmState.FetchInstructionAndIncrementInstructionPointer();
            switch (instruction.OpCode)
            {
                #region Group 1 - control, calls, messages, types, values

                case Nop:
                    break;
                case SlotLocals:
                    _vmState.ModifyLocalSlots(instruction.Count);
                    break;
                case Jump:
                    _vmState.JumpAddress(instruction.TargetAddress);
                    break;
                case JumpIfTrue:
                    if (_vmState.Register(instruction.ConditionSlot).IsTrue) _vmState.JumpAddress(instruction.TargetAddress);
                    break;
                case JumpIfFalse:
                    if (_vmState.Register(instruction.ConditionSlot).IsFalse) _vmState.JumpAddress(instruction.TargetAddress);
                    break;
                case JumpIfNotTrue:
                    if (_vmState.Register(instruction.ConditionSlot).IsNotTrue) _vmState.JumpAddress(instruction.TargetAddress);
                    break;

                case Call:
                    _vmState.CallAddress(instruction.TargetAddress, instruction.DestinationSlot, instruction.HasInstructionFlag(GameEventScriptInstructionFlag.NormalizeResultAsPredicate));
                    break;
                case CallStandard:
                    _vmState.Register(instruction.DestinationSlot).VmCallStandard(instruction.SecondaryListIndex, instruction.ListIndex, instruction.HasInstructionFlag(GameEventScriptInstructionFlag.NormalizeResultAsPredicate));
                    break;
                case CallExternal:
                    _vmState.Register(instruction.DestinationSlot).VmCallExternal(instruction.SecondaryListIndex, instruction.ListIndex, session, instruction.HasInstructionFlag(GameEventScriptInstructionFlag.NormalizeResultAsPredicate));
                    break;

                case ReturnVoid:
                    _vmState.ReturnVoid();
                    break;
                case ReturnValue:
                    _vmState.ReturnValue(instruction.XSlot);
                    break;

                case EmitMessage:
                    _vmState.VmPublishMessage(instruction.MessageDestination, binary.Uint16ConstantTable.Resolve(instruction.ListIndex), false, session);
                    break;
                case EmitMessageWithTags:
                    _vmState.VmPublishMessageWithTags(instruction.MessageDestination, binary.Uint16ConstantTable.Resolve(instruction.ListIndex),
                        binary.Uint16ConstantTable.Resolve(instruction.SecondaryListIndex), false, session);
                    break;
                case EmitMessageValue:
                    _vmState.VmPublishMessageValue(ref _vmState.Register(instruction.XSlot), false, session);
                    break;
                case EmitMessageValueWithTags:
                    _vmState.VmPublishMessageValueWithTags(ref _vmState.Register(instruction.XSlot), binary.Uint16ConstantTable.Resolve(instruction.ListIndex), false, session);
                    break;

                case PublishMessage:
                    _vmState.VmPublishMessage(instruction.MessageDestination, binary.Uint16ConstantTable.Resolve(instruction.ListIndex), true, session);
                    break;
                case PublishMessageWithTags:
                    _vmState.VmPublishMessageWithTags(instruction.MessageDestination, binary.Uint16ConstantTable.Resolve(instruction.ListIndex),
                        binary.Uint16ConstantTable.Resolve(instruction.SecondaryListIndex), true, session);
                    break;
                case PublishMessageValue:
                    _vmState.VmPublishMessageValue(ref _vmState.Register(instruction.XSlot), true, session);
                    break;
                case PublishMessageValueWithTags:
                    _vmState.VmPublishMessageValueWithTags(ref _vmState.Register(instruction.XSlot), binary.Uint16ConstantTable.Resolve(instruction.ListIndex), true, session);
                    break;

                case Cast:
                    _vmState.Register(instruction.DestinationSlot).VmCast(ref _vmState.Register(instruction.XSlot), instruction.TypeKind);
                    break;
                case CastCustom:
                    _vmState.Register(instruction.DestinationSlot).VmCastCustom(ref _vmState.Register(instruction.XSlot), instruction.SecondaryStringIndex);
                    break;
                case CastUnit:
                    _vmState.Register(instruction.DestinationSlot).VmCastUnit(ref _vmState.Register(instruction.XSlot), instruction.Unit);
                    break;
                case CastNumeric:
                    _vmState.Register(instruction.DestinationSlot).VmCastNumeric(ref _vmState.Register(instruction.XSlot));
                    break;

                case CheckType:
                    _vmState.Register(instruction.DestinationSlot).VmCheckType(ref _vmState.Register(instruction.XSlot), instruction.TypeKind);
                    break;
                case CheckCustomType:
                    _vmState.Register(instruction.DestinationSlot).VmCheckCustomType(ref _vmState.Register(instruction.XSlot), instruction.SecondaryStringIndex);
                    break;
                case CheckUnit:
                    _vmState.Register(instruction.DestinationSlot).VmCheckUnit(ref _vmState.Register(instruction.XSlot), instruction.Unit);
                    break;
                case CheckNumeric:
                    _vmState.Register(instruction.DestinationSlot).VmCheckNumeric(ref _vmState.Register(instruction.XSlot));
                    break;
                case CheckInteger:
                    _vmState.Register(instruction.DestinationSlot).VmCheckInteger(ref _vmState.Register(instruction.XSlot));
                    break;
                case CheckFractional:
                    _vmState.Register(instruction.DestinationSlot).VmCheckFractional(ref _vmState.Register(instruction.XSlot));
                    break;

                case MoveSlot:
                    _vmState.Register(instruction.DestinationSlot) = _vmState.Register(instruction.XSlot);
                    break;
                case MemberAccess:
                    _vmState.Register(instruction.DestinationSlot).VmMemberAccess(instruction.XSlot, ref _vmState.Register(instruction.YSlot));
                    break;
                case IndexAccess:
                    _vmState.Register(instruction.DestinationSlot).VmIndexAccess(instruction.Index, ref _vmState.Register(instruction.YSlot));
                    break;
                case PropertyAccess:
                    _vmState.Register(instruction.DestinationSlot).VmPropertyAccess(ref _vmState.Register(instruction.XSlot), ref _vmState.Register(instruction.YSlot));
                    break;
                case BindHandler:
                    _vmState.Register(instruction.DestinationSlot).BindHandler(ref _vmState.Register(instruction.XSlot), binary.Uint16ConstantTable.Resolve(instruction.ListIndex));
                    break;

                case LoadNothing:
                    _vmState.Register(instruction.DestinationSlot).SetNothing();
                    break;
                case LoadTrue:
                    _vmState.Register(instruction.DestinationSlot).SetBoolean(true);
                    break;
                case LoadFalse:
                    _vmState.Register(instruction.DestinationSlot).SetBoolean(false);
                    break;
                case LoadInteger:
                    _vmState.Register(instruction.DestinationSlot).SetInteger(instruction.I64, instruction.Unit);
                    break;
                case LoadFloat:
                    _vmState.Register(instruction.DestinationSlot).SetFloat(instruction.F64, instruction.Unit);
                    break;
                case LoadPercentage:
                    _vmState.Register(instruction.DestinationSlot).SetPercentage(instruction.F64);
                    break;
                case LoadText:
                    _vmState.Register(instruction.DestinationSlot).SetTextPointer(instruction.StringIndex);
                    break;
                case LoadTag:
                    _vmState.Register(instruction.DestinationSlot).SetTagPointer(instruction.StringIndex);
                    break;
                case LoadHandler:
                    _vmState.Register(instruction.DestinationSlot).CreateMessageSignature(binary.Uint16ConstantTable.Resolve(instruction.ListIndex), _vmState, session);
                    break;
                case LoadMessage:
                    _vmState.Register(instruction.DestinationSlot).CreateMessage(binary.Uint16ConstantTable.Resolve(instruction.SecondaryListIndex), binary.Uint16ConstantTable.Resolve(instruction.ListIndex));
                    break;

                case StageRegister:
                    _vmState.StageRegister(instruction.XSlot);
                    break;
                case StageNothing:
                    _vmState.StageNothing();
                    break;
                case StageTrue:
                    _vmState.StageBoolean(true);
                    break;
                case StageFalse:
                    _vmState.StageBoolean(false);
                    break;
                case StageInteger:
                    _vmState.StageInteger(instruction.I64, instruction.Unit);
                    break;
                case StageFloat:
                    _vmState.StageFloat(instruction.F64, instruction.Unit);
                    break;
                case StageText:
                    _vmState.StageTextConstant(instruction.StringIndex);
                    break;
                case StageTag:
                    _vmState.StageTagConstant(instruction.StringIndex);
                    break;
                case StagePercentage:
                    _vmState.StagePercentage(instruction.F64);
                    break;

                case CreateDice:
                    _vmState.Register(instruction.DestinationSlot).VmCreateDice(instruction.Count, instruction.ImmediateY, _vmState);
                    break;
                case CreateVector:
                    _vmState.Register(instruction.DestinationSlot).VmCreateVector(instruction.ImmediateX, _vmState);
                    _vmState.ClearStage();
                    break;
                case CreatePoint:
                    _vmState.Register(instruction.DestinationSlot).VmCreatePoint(instruction.ImmediateX, _vmState);
                    _vmState.ClearStage();
                    break;
                case CreateList:
                    _vmState.Register(instruction.DestinationSlot).VmCreateList();
                    _vmState.ClearStage();
                    break;
                case CreateMap:
                    _vmState.Register(instruction.DestinationSlot).VmCreateMap(instruction.SecondaryListIndex);
                    _vmState.ClearStage();
                    break;
                case CreateRange:
                    _vmState.Register(instruction.DestinationSlot).VmCreateRange(ref _vmState.Register(instruction.XSlot), ref _vmState.Register(instruction.YSlot));
                    break;
                case CreateRangeWithStep:
                    _vmState.Register(instruction.DestinationSlot).VmCreateRange(ref _vmState.Register(instruction.XSlot), ref _vmState.Register(instruction.YSlot), ref _vmState.Register(instruction.AU));
                    break;
                case CreateRangeIterator:
                    _vmState.Register(instruction.DestinationSlot).SetStream(new VmIntegerRangeStream(instruction.XSlot, instruction.YSlot, 1));
                    break;
                case CreateRangeIteratorWithStep:
                    _vmState.Register(instruction.DestinationSlot).SetStream(new VmIntegerRangeStream(instruction.XSlot, instruction.YSlot, instruction.AU));
                    break;
                case CreateRangeIteratorShort:
                    _vmState.Register(instruction.DestinationSlot).SetStream(new VmIntegerRangeStream(instruction.ImmediateX, instruction.ImmediateY, instruction.AS));
                    break;
                case CreateRecord:
                    _vmState.Register(instruction.DestinationSlot).VmCreateRecord(instruction.StringIndex, instruction.ListIndex);
                    _vmState.ClearStage();
                    break;
                case CreateExternalType:
                    _vmState.Register(instruction.DestinationSlot).VmCreateExternalType(instruction.ExternalReferenceIndex, instruction.ListIndex);
                    _vmState.ClearStage();
                    break;

                case HasValue:
                    _vmState.Register(instruction.DestinationSlot).SetBoolean(_vmState.Register(instruction.XSlot).HasValue);
                    break;
                case IsEmpty:
                    _vmState.Register(instruction.DestinationSlot).SetBoolean(!_vmState.Register(instruction.XSlot).HasValue);
                    break;
                case Default:
                    var a = _vmState.Register(instruction.XSlot);
                    _vmState.Register(instruction.DestinationSlot) = a.HasValue ? a : _vmState.Register(instruction.YSlot);
                    break;

                #endregion

                #region Group 2 - boolean algebra, comparison, math and random

                case Or:
                    _vmState.Register(instruction.DestinationSlot).VmOr(ref _vmState.Register(instruction.XSlot), ref _vmState.Register(instruction.YSlot));
                    break;
                case And:
                    _vmState.Register(instruction.DestinationSlot).VmAnd(ref _vmState.Register(instruction.XSlot), ref _vmState.Register(instruction.YSlot));
                    break;
                case Xor:
                    _vmState.Register(instruction.DestinationSlot).VmXor(ref _vmState.Register(instruction.XSlot), ref _vmState.Register(instruction.YSlot));
                    break;
                case Implies:
                    _vmState.Register(instruction.DestinationSlot).VmImplies(ref _vmState.Register(instruction.XSlot), ref _vmState.Register(instruction.YSlot));
                    break;
                case Not:
                    _vmState.Register(instruction.DestinationSlot).VmNot(ref _vmState.Register(instruction.XSlot));
                    break;
                case Equal:
                    _vmState.Register(instruction.DestinationSlot).VmEqual(ref _vmState.Register(instruction.XSlot), ref _vmState.Register(instruction.YSlot));
                    break;
                case NotEqual:
                    _vmState.Register(instruction.DestinationSlot).VmNotEqual(ref _vmState.Register(instruction.XSlot), ref _vmState.Register(instruction.YSlot));
                    break;
                case ApproxEqual:
                    _vmState.Register(instruction.DestinationSlot).VmApproxEqual(ref _vmState.Register(instruction.XSlot), ref _vmState.Register(instruction.YSlot));
                    break;
                case Less:
                    _vmState.Register(instruction.DestinationSlot).VmLess(ref _vmState.Register(instruction.XSlot), ref _vmState.Register(instruction.YSlot));
                    break;
                case Greater:
                    _vmState.Register(instruction.DestinationSlot).VmGreater(ref _vmState.Register(instruction.XSlot), ref _vmState.Register(instruction.YSlot));
                    break;
                case LessOrEqual:
                    _vmState.Register(instruction.DestinationSlot).VmLessOrEqual(ref _vmState.Register(instruction.XSlot), ref _vmState.Register(instruction.YSlot));
                    break;
                case GreaterOrEqual:
                    _vmState.Register(instruction.DestinationSlot).VmGreaterOrEqual(ref _vmState.Register(instruction.XSlot), ref _vmState.Register(instruction.YSlot));
                    break;
                case Add:
                    _vmState.Register(instruction.DestinationSlot).VmAdd(ref _vmState.Register(instruction.XSlot), ref _vmState.Register(instruction.YSlot), ref binary.TextConstantTable);
                    break;
                case Subtract:
                    _vmState.Register(instruction.DestinationSlot).VmSubtract(ref _vmState.Register(instruction.XSlot), ref _vmState.Register(instruction.YSlot), ref binary.TextConstantTable);
                    break;
                case Multiply:
                    _vmState.Register(instruction.DestinationSlot).VmMultiply(ref _vmState.Register(instruction.XSlot), ref _vmState.Register(instruction.YSlot), ref binary.TextConstantTable);
                    break;
                case Divide:
                    _vmState.Register(instruction.DestinationSlot).VmDivide(ref _vmState.Register(instruction.XSlot), ref _vmState.Register(instruction.YSlot), ref binary.TextConstantTable);
                    break;
                case Power:
                    _vmState.Register(instruction.DestinationSlot).VmPower(ref _vmState.Register(instruction.XSlot), ref _vmState.Register(instruction.YSlot), ref binary.TextConstantTable);
                    break;
                case IntegerDivide:
                    _vmState.Register(instruction.DestinationSlot).VmFloorDivide(ref _vmState.Register(instruction.XSlot), ref _vmState.Register(instruction.YSlot), ref binary.TextConstantTable);
                    break;
                case Modulo:
                    _vmState.Register(instruction.DestinationSlot).VmModulo(ref _vmState.Register(instruction.XSlot), ref _vmState.Register(instruction.YSlot), ref binary.TextConstantTable);
                    break;
                case Remainder:
                    _vmState.Register(instruction.DestinationSlot).VmRemainder(ref _vmState.Register(instruction.XSlot), ref _vmState.Register(instruction.YSlot), ref binary.TextConstantTable);
                    break;
                case Min:
                    _vmState.Register(instruction.DestinationSlot).VmMin(ref _vmState.Register(instruction.XSlot), ref _vmState.Register(instruction.YSlot), ref binary.TextConstantTable);
                    break;
                case Max:
                    _vmState.Register(instruction.DestinationSlot).VmMax(ref _vmState.Register(instruction.XSlot), ref _vmState.Register(instruction.YSlot), ref binary.TextConstantTable);
                    break;
                case Negate:
                    _vmState.Register(instruction.DestinationSlot).VmNegate(ref _vmState.Register(instruction.XSlot), ref binary.TextConstantTable);
                    break;
                case Abs:
                    _vmState.Register(instruction.DestinationSlot).VmAbs(ref _vmState.Register(instruction.XSlot), ref binary.TextConstantTable);
                    break;
                case LogN:
                    _vmState.Register(instruction.DestinationSlot).VmNaturalLog(ref _vmState.Register(instruction.XSlot), ref binary.TextConstantTable);
                    break;
                case Chance:
                    _vmState.Register(instruction.DestinationSlot).VmChance(ref _vmState.Register(instruction.XSlot));
                    break;
                case Clamp:
                    _vmState.Register(instruction.DestinationSlot).VmClamp(ref _vmState.Register(instruction.XSlot), ref _vmState.Register(instruction.YSlot), ref _vmState.Register(instruction.AU), ref binary.TextConstantTable);
                    break;
                case RandomTake:
                    _vmState.Register(instruction.DestinationSlot).VmRandom(ref _vmState.Register(instruction.XSlot), ref _vmState.Register(instruction.YSlot), _vmState.RandomGenerator, ref binary.TextConstantTable);
                    break;
                case RandomPush:
                    var seed = _vmState.Register(instruction.XSlot);
                    _vmState.PushRandom(seed.Kind == Integer ? GameEventScriptRandomGenerator.FromSeed((int)seed.IntegerValue) : _vmState.RandomGenerator);
                    break;
                case RandomPushConstant:
                    _vmState.PushRandom(GameEventScriptRandomGenerator.FromSeed((int)instruction.I64));
                    break;
                case RandomPop:
                    _vmState.PopRandom();
                    break;
                case SeriesTerm:
                    // FIXME: creating the real custom type here
                    break;
                case SeriesTake:
                    // FIXME: creating the real custom type here
                    break;
                case SeriesDrop:
                    // FIXME: creating the real custom type here
                    break;

                #endregion

                #region Group 3 - text, collection, streams

                case Length:
                    _vmState.Register(instruction.DestinationSlot).VmLength(ref _vmState.Register(instruction.XSlot), ref binary.TextConstantTable);
                    break;
                case StartsWith:
                    _vmState.Register(instruction.DestinationSlot).VmStartsWith(ref _vmState.Register(instruction.XSlot), ref _vmState.Register(instruction.YSlot), ref binary.TextConstantTable);
                    break;
                case EndsWith:
                    _vmState.Register(instruction.DestinationSlot).VmEndsWith(ref _vmState.Register(instruction.XSlot), ref _vmState.Register(instruction.YSlot), ref binary.TextConstantTable);
                    break;
                case Contains:
                    _vmState.Register(instruction.DestinationSlot).VmContains(ref _vmState.Register(instruction.XSlot), ref _vmState.Register(instruction.YSlot), ref binary.TextConstantTable);
                    break;
                case ContainsValue:
                    _vmState.Register(instruction.DestinationSlot).VmContainsValue(ref _vmState.Register(instruction.XSlot), ref _vmState.Register(instruction.YSlot), ref binary.TextConstantTable);
                    break;
                case Union:
                    _vmState.Register(instruction.DestinationSlot).VmUnion(ref _vmState.Register(instruction.XSlot), ref _vmState.Register(instruction.YSlot), ref binary.TextConstantTable);
                    break;
                case Intersect:
                    _vmState.Register(instruction.DestinationSlot).VmIntersect(ref _vmState.Register(instruction.XSlot), ref _vmState.Register(instruction.YSlot), ref binary.TextConstantTable);
                    break;
                case Zip:
                    _vmState.Register(instruction.DestinationSlot).VmZip(ref _vmState.Register(instruction.XSlot), ref _vmState.Register(instruction.YSlot), ref binary.TextConstantTable);
                    break;
                case KeysOfMap:
                    _vmState.Register(instruction.DestinationSlot).VmKeys(ref _vmState.Register(instruction.XSlot));
                    break;
                case ValuesOfMap:
                    _vmState.Register(instruction.DestinationSlot).VmValues(ref _vmState.Register(instruction.XSlot));
                    break;
                case EntriesOfMap:
                    _vmState.Register(instruction.DestinationSlot).VmEntries(ref _vmState.Register(instruction.XSlot));
                    break;
                case StreamCreate:
                    _vmState.Register(instruction.DestinationSlot).VmStreamCreate(ref _vmState.Register(instruction.XSlot));
                    break;
                case StreamNext:
                    _vmState.Register(instruction.DestinationSlot).VmStreamNext(ref _vmState.Register(instruction.XSlot), instruction.TargetAddress);
                    break;
                case StreamClose:
                    _vmState.Register(instruction.XSlot).VmStreamClose();
                    break;
                case StreamReduce:
                    // FIXME: creating the real custom type here
                    break;
                case StreamReduceOrDefault:
                    // FIXME: creating the real custom type here
                    break;
                case StreamFold:
                    // FIXME: creating the real custom type here
                    break;
                case StreamCollectList:
                    // FIXME: creating the real custom type here
                    break;
                case StreamCollectMap:
                    // FIXME: creating the real custom type here
                    break;
                case StreamCollectMapValue:
                    // FIXME: creating the real custom type here
                    break;
                case StreamCollectFirst:
                    // FIXME: creating the real custom type here
                    break;
                case StreamCollectLast:
                    // FIXME: creating the real custom type here
                    break;
                case StreamCollectSingle:
                    // FIXME: creating the real custom type here
                    break;

                #endregion

                #region Group 4 - pipeline terminals and transforms

                case PipelineStream:
                    // FIXME: creating the real custom type here
                    break;
                case PipelineHasAny:
                    // FIXME: creating the real custom type here
                    break;
                case PipelineHasAll:
                    // FIXME: creating the real custom type here
                    break;
                case PipelineContainsSingle:
                    // FIXME: creating the real custom type here
                    break;
                case PipelineContainsAny:
                    // FIXME: creating the real custom type here
                    break;
                case PipelineContainsAll:
                    // FIXME: creating the real custom type here
                    break;
                case PipelineDistinct:
                    // FIXME: creating the real custom type here
                    break;
                case PipelineDistinctBy:
                    // FIXME: creating the real custom type here
                    break;
                case PipelineGroupBy:
                    // FIXME: creating the real custom type here
                    break;
                case PipelineReverse:
                    // FIXME: creating the real custom type here
                    break;
                case PipelineSortAscending:
                    // FIXME: creating the real custom type here
                    break;
                case PipelineSortDescending:
                    // FIXME: creating the real custom type here
                    break;
                case PipelineOrderByAscending:
                    // FIXME: creating the real custom type here
                    break;
                case PipelineOrderByDescending:
                    // FIXME: creating the real custom type here
                    break;
                case PipelineTakeFirst:
                    // FIXME: creating the real custom type here
                    break;
                case PipelineTakeLast:
                    // FIXME: creating the real custom type here
                    break;
                case PipelineTakeHighest:
                    // FIXME: creating the real custom type here
                    break;
                case PipelineTakeLowest:
                    // FIXME: creating the real custom type here
                    break;
                case PipelineDropFirst:
                    // FIXME: creating the real custom type here
                    break;
                case PipelineDropLast:
                    // FIXME: creating the real custom type here
                    break;
                case PipelineDropHighest:
                    // FIXME: creating the real custom type here
                    break;
                case PipelineDropLowest:
                    // FIXME: creating the real custom type here
                    break;
                case PipelineShuffle:
                    // FIXME: creating the real custom type here
                    break;
                case PipelineDraw:
                    // FIXME: creating the real custom type here
                    break;
                case PipelineChoose:
                    // FIXME: creating the real custom type here
                    break;
                case PipelineChooseRandom:
                    // FIXME: creating the real custom type here
                    break;
                case PipelineChooseWeighted:
                    // FIXME: creating the real custom type here
                    break;
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
                case PipelineListCreateBuilder:
                    _vmState.Register(instruction.DestinationSlot).VmCreateListBuilder();
                    break;
                case PipelineListBuilderAdd:
                    _vmState.Register(instruction.XSlot).VmListBuilderAdd(ref _vmState.Register(instruction.YSlot));
                    break;
                case PipelineListBuilderFinish:
                    _vmState.Register(instruction.DestinationSlot).VmListBuilderFinish(ref _vmState.Register(instruction.XSlot));
                    break;

                #endregion
                default:
                    _vmState.RaiseError("Illegal opcode " + nameof(instruction.OpCode) + ". Execution halted.");
                    break;
            }
        }

        return true;
    }
}
