#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using StepH.GameEventScript.Api;
using StepH.GameEventScript.Runtime;
using static StepH.GameEventScript.Api.GameEventScriptBytecodeOpCode;
using static StepH.GameEventScript.Api.GameEventScriptBytecodeTypeKind;
using static StepH.GameEventScript.BytecodeExecutor.VmRegisterUnitCalculation;

namespace StepH.GameEventScript.BytecodeExecutor;

public class GameEventScriptVirtualMaschine(GameEventScriptBinary binary, ushort registerSize, ushort stackSize)
{
    private VmState _vmState = new(binary, registerSize, stackSize);

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
                    _vmState.CallAddress(instruction.TargetAddress, instruction.DestinationSlot);
                    break;
                case CallPredicate:
                    _vmState.CallAddress(instruction.TargetAddress, instruction.DestinationSlot);
                    _vmState.Register(instruction.DestinationSlot).Kind = GameEventScriptBytecodeTypeKind.Boolean;
                    break;
                case CallStandard:
                    _vmState.Register(instruction.DestinationSlot).VmCallStandard(instruction.SecondaryListIndex, instruction.ListIndex, ref _vmState);
                    break;
                case CallStandardPredicate:
                    _vmState.Register(instruction.DestinationSlot).VmCallStandardPredicate(instruction.SecondaryListIndex, instruction.ListIndex, ref _vmState);
                    break;
                case CallExternal:
                    _vmState.Register(instruction.DestinationSlot).VmCallExternal(instruction.SecondaryListIndex, instruction.ListIndex, ref _vmState);
                    break;
                case CallExternalPredicate:
                    _vmState.Register(instruction.DestinationSlot).VmCallExternalPredicate(instruction.SecondaryListIndex, instruction.ListIndex, ref _vmState);
                    break;
                case ReturnVoid:
                    _vmState.ReturnVoid();
                    break;
                case ReturnValue:
                    _vmState.ReturnValue(instruction.XSlot);
                    break;
                case EmitMessage:
                    _vmState.VmPublishMessage(binary.Uint16ConstantTable.Resolve(instruction.MessageDestination), binary.Uint16ConstantTable.Resolve(instruction.ListIndex), false, session);
                    break;
                case EmitMessageWithTags:
                    _vmState.VmPublishMessageWithTags(binary.Uint16ConstantTable.Resolve(instruction.MessageDestination), binary.Uint16ConstantTable.Resolve(instruction.ListIndex),
                        binary.Uint16ConstantTable.Resolve(instruction.SecondaryListIndex), false, session);
                    break;
                case EmitMessageValue:
                    _vmState.VmPublishMessageValue(ref _vmState.Register(instruction.XSlot), false, session);
                    break;
                case EmitMessageValueWithTags:
                    _vmState.VmPublishMessageValueWithTags(ref _vmState.Register(instruction.XSlot), binary.Uint16ConstantTable.Resolve(instruction.ListIndex), false, session);
                    break;
                case PublishMessage:
                    _vmState.VmPublishMessage(binary.Uint16ConstantTable.Resolve(instruction.MessageDestination), binary.Uint16ConstantTable.Resolve(instruction.ListIndex), true, session);
                    break;
                case PublishMessageWithTags:
                    _vmState.VmPublishMessageWithTags(binary.Uint16ConstantTable.Resolve(instruction.MessageDestination), binary.Uint16ConstantTable.Resolve(instruction.ListIndex),
                        binary.Uint16ConstantTable.Resolve(instruction.SecondaryListIndex), false, session);
                    break;
                case PublishMessageValue:
                    _vmState.VmPublishMessageValue(ref _vmState.Register(instruction.XSlot), true, session);
                    break;
                case PublishMessageValueWithTags:
                    _vmState.VmPublishMessageValueWithTags(ref _vmState.Register(instruction.XSlot), binary.Uint16ConstantTable.Resolve(instruction.ListIndex), true, session);
                    break;
                case Cast:
                    _vmState.Register(instruction.DestinationSlot).VmCast(ref _vmState.Register(instruction.XSlot), instruction.TypeKind, ref binary.TextConstantTable);
                    break;
                case CastNumeric:
                    _vmState.Register(instruction.DestinationSlot).VmCastNumeric(ref _vmState.Register(instruction.XSlot), ref binary.TextConstantTable);
                    break;
                case CastCustom:
                    _vmState.Register(instruction.DestinationSlot).VmCastCustom(ref _vmState.Register(instruction.XSlot), instruction.SecondaryStringIndex, ref binary.TextConstantTable);
                    break;
                case CastUnit:
                    _vmState.Register(instruction.DestinationSlot).VmCastUnit(ref _vmState.Register(instruction.XSlot), (GameEventScriptBytecodeInstructionUnit)(instruction.UnitAndFlags & 0x1f));
                    break;
                case CheckType:
                    _vmState.Register(instruction.DestinationSlot).VmCheckType(ref _vmState.Register(instruction.XSlot), instruction.TypeKind);
                    break;
                case CheckNumeric:
                    _vmState.Register(instruction.DestinationSlot).VmCheckNumeric(ref _vmState.Register(instruction.XSlot), ref binary.TextConstantTable);
                    break;
                case CheckInteger:
                    _vmState.Register(instruction.DestinationSlot).VmCheckInteger(ref _vmState.Register(instruction.XSlot), ref binary.TextConstantTable);
                    break;
                case CheckFractional:
                    _vmState.Register(instruction.DestinationSlot).VmCheckFractional(ref _vmState.Register(instruction.XSlot), ref binary.TextConstantTable);
                    break;
                case CheckCustomType:
                    _vmState.Register(instruction.DestinationSlot).VmCheckCustomType(ref _vmState.Register(instruction.XSlot), instruction.SecondaryStringIndex, ref binary.TextConstantTable);
                    break;
                case CheckUnit:
                    _vmState.Register(instruction.DestinationSlot).VmCheckUnit(ref _vmState.Register(instruction.XSlot), (GameEventScriptBytecodeInstructionUnit)(instruction.UnitAndFlags & 0x1f));
                    break;
                case MoveSlot:
                    _vmState.Register(instruction.DestinationSlot) = _vmState.Register(instruction.XSlot);
                    break;
                case MemberAccess:
                    break;
                case IndexedAccess:
                    break;
                case BindHandler:
                    _vmState.Register(instruction.DestinationSlot).BindHandler(ref _vmState.Register(instruction.XSlot), binary.Uint16ConstantTable.Resolve(instruction.ListIndex), ref _vmState, session);
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
                    _vmState.Register(instruction.DestinationSlot).SetInteger(instruction.I64, DecodeNumericUnit(instruction.UnitAndFlags));
                    break;
                case LoadFloat:
                    _vmState.Register(instruction.DestinationSlot).SetFloat(instruction.F64, DecodeNumericUnit(instruction.UnitAndFlags));
                    break;
                case LoadPercentage:
                    _vmState.Register(instruction.DestinationSlot).SetPercentage(instruction.F64);
                    break;
                case LoadText:
                    _vmState.Register(instruction.DestinationSlot).SetStringPointer(instruction.StringIndex);
                    break;
                case LoadTag:
                    _vmState.Register(instruction.DestinationSlot).SetTagPointer(instruction.StringIndex);
                    break;
                case LoadHandler:
                    _vmState.Register(instruction.DestinationSlot).CreateMessageSignature(binary.Uint16ConstantTable.Resolve(instruction.ListIndex), ref _vmState, session);
                    break;
                case LoadMessage:
                    _vmState.Register(instruction.DestinationSlot).CreateMessage(binary.Uint16ConstantTable.Resolve(instruction.SecondaryListIndex), binary.Uint16ConstantTable.Resolve(instruction.ListIndex), ref _vmState, session);
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
                    _vmState.StageInteger(instruction.I64, DecodeNumericUnit(instruction.UnitAndFlags));
                    break;
                case StageFloat:
                    _vmState.StageFloat(instruction.F64, DecodeNumericUnit(instruction.UnitAndFlags));
                    break;
                case StagePercentage:
                    _vmState.StagePercentage(instruction.F64);
                    break;
                case StageText:
                    _vmState.StageTextConstant(instruction.StringIndex);
                    break;
                case StageTag:
                    _vmState.StageTagConstant(instruction.StringIndex);
                    break;
                case TypeConstructor:
                    break;
                case CreateVector:
                    _vmState.Register(instruction.DestinationSlot).VmCreateTripleFloat(instruction.ImmediateX, ref _vmState, Vector);
                    _vmState.ClearStage();
                    break;
                case CreatePoint:
                    _vmState.Register(instruction.DestinationSlot).VmCreateTripleFloat(instruction.ImmediateX, ref _vmState, Point);
                    _vmState.ClearStage();
                    break;
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
                case UnaryNot:
                    _vmState.Register(instruction.DestinationSlot).VmNot(ref _vmState.Register(instruction.XSlot));
                    break;
                case UnaryHasValue:
                    _vmState.Register(instruction.DestinationSlot).VmHasValue(ref _vmState.Register(instruction.XSlot), ref binary.TextConstantTable);
                    break;
                case UnaryEmpty:
                    _vmState.Register(instruction.DestinationSlot).VmEmpty(ref _vmState.Register(instruction.XSlot), ref binary.TextConstantTable);
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
                case Default:
                    _vmState.Register(instruction.DestinationSlot).VmDefault(ref _vmState.Register(instruction.XSlot), ref _vmState.Register(instruction.YSlot));
                    break;
                case Add:
                    _vmState.Register(instruction.DestinationSlot).VmAdd(ref _vmState.Register(instruction.XSlot), ref _vmState.Register(instruction.YSlot), ref binary.TextConstantTable);
                    break;
                case Subtract:
                    _vmState.Register(instruction.DestinationSlot).VmSubtract(ref _vmState.Register(instruction.XSlot), ref _vmState.Register(instruction.YSlot));
                    break;
                case Multiply:
                    _vmState.Register(instruction.DestinationSlot).VmMultiply(ref _vmState.Register(instruction.XSlot), ref _vmState.Register(instruction.YSlot));
                    break;
                case Divide:
                    _vmState.Register(instruction.DestinationSlot).VmDivide(ref _vmState.Register(instruction.XSlot), ref _vmState.Register(instruction.YSlot));
                    break;
                case Power:
                    _vmState.Register(instruction.DestinationSlot).VmPower(ref _vmState.Register(instruction.XSlot), ref _vmState.Register(instruction.YSlot));
                    break;
                case IntegerDivide:
                    _vmState.Register(instruction.DestinationSlot).VmIntegerDivide(ref _vmState.Register(instruction.XSlot), ref _vmState.Register(instruction.YSlot));
                    break;
                case Modulo:
                    _vmState.Register(instruction.DestinationSlot).VmModulo(ref _vmState.Register(instruction.XSlot), ref _vmState.Register(instruction.YSlot));
                    break;
                case Remainder:
                    _vmState.Register(instruction.DestinationSlot).VmRemainder(ref _vmState.Register(instruction.XSlot), ref _vmState.Register(instruction.YSlot));
                    break;
                case Min:
                    break;
                case Max:
                    break;
                case UnaryNegate:
                    _vmState.Register(instruction.DestinationSlot).VmNegate(ref _vmState.Register(instruction.XSlot));
                    break;
                case UnaryAbs:
                    _vmState.Register(instruction.DestinationSlot).VmAbs(ref _vmState.Register(instruction.XSlot));
                    break;
                case UnaryNaturalLog:
                    _vmState.Register(instruction.DestinationSlot).VmNaturalLog(ref _vmState.Register(instruction.XSlot));
                    break;
                case UnaryChance:
                    _vmState.Register(instruction.DestinationSlot).VmChance(ref _vmState.Register(instruction.XSlot), ref _vmState);
                    break;
                case Clamp:
                    _vmState.Register(instruction.DestinationSlot).VmClamp(ref _vmState.Register(instruction.XSlot), ref _vmState.Register(instruction.YSlot), ref _vmState.Register(instruction.AU));
                    break;
                case GameEventScriptBytecodeOpCode.Random:
                    _vmState.Register(instruction.DestinationSlot).VmRandom(ref _vmState.Register(instruction.XSlot), ref _vmState.Register(instruction.YSlot), _vmState.RandomGenerator);
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
                case UnaryLength:
                    _vmState.Register(instruction.DestinationSlot).VmLength(ref _vmState.Register(instruction.XSlot), ref binary.TextConstantTable);
                    break;
                case StartsWith:
                    break;
                case EndsWith:
                    break;
                case Contains:
                    break;
                case ContainsValue:
                    break;
                case Intersect:
                    break;
                case Combine:
                    break;
                case Except:
                    break;
                case Zip:
                    break;
                case UnaryKeys:
                    break;
                case UnaryValues:
                    break;
                case UnaryEntries:
                    break;
                case GameEventScriptBytecodeOpCode.Range:
                    break;
                case RangeWithStep:
                    break;
                case RangeIterator:
                    _vmState.Register(instruction.DestinationSlot).SetObject(Stream, new VmIntegerRangeStream(instruction.XSlot, instruction.YSlot, instruction.AU));
                    break;
                case RangeIteratorWithStep:
                    break;
                case RangeIteratorShort:
                    _vmState.Register(instruction.DestinationSlot).SetObject(Stream, new VmIntegerRangeStream(instruction.ImmediateX, instruction.ImmediateY, instruction.AS));
                    break;
                case CollectionIterator:
                    break;
                case StreamNext:
                    _vmState.Register(instruction.DestinationSlot).VmIteratorNext(ref _vmState.Register(instruction.XSlot), instruction.TargetAddress, ref _vmState);
                    break;
                case StreamClose:
                    _vmState.Register(instruction.XSlot).VmIteratorClose();
                    break;
                case StreamReduce:
                    break;
                case StreamReduceOrDefault:
                    break;
                case StreamFold:
                    break;
                case SeriesTerm:
                    break;
                case SeriesTake:
                    break;
                case SeriesDrop:
                    break;
                case PipelineIterator:
                    break;
                case BuildList:
                    break;
                case BuildMap:
                    break;
                case CollectionBuilderList:
                    break;
                case CollectionBuilderAdd:
                    break;
                case CollectionBuilderFinish:
                    break;
                case GameEventScriptBytecodeOpCode.Dice:
                    _vmState.Register(instruction.DestinationSlot).VmDice(instruction.Count, instruction.ImmediateY, ref _vmState);
                    break;
                case PipelineCollectList:
                    break;
                case PipelineFirst:
                    break;
                case PipelineLast:
                    break;
                case PipelineSingle:
                    break;
                case PipelineHasAny:
                    break;
                case PipelineHasAll:
                    break;
                case PipelineContainsSingle:
                    break;
                case PipelineContainsAny:
                    break;
                case PipelineContainsAll:
                    break;
                case PipelineMap:
                    break;
                case PipelineMapValue:
                    break;
                case PipelineDistinct:
                    break;
                case PipelineDistinctBy:
                    break;
                case PipelineGroupBy:
                    break;
                case PipelineReverse:
                    break;
                case PipelineSortAscending:
                    break;
                case PipelineSortDescending:
                    break;
                case PipelineOrderByAscending:
                    break;
                case PipelineOrderByDescending:
                    break;
                case PipelineTakeFirst:
                    break;
                case PipelineTakeLast:
                    break;
                case PipelineTakeHighest:
                    break;
                case PipelineTakeLowest:
                    break;
                case PipelineDropFirst:
                    break;
                case PipelineDropLast:
                    break;
                case PipelineDropHighest:
                    break;
                case PipelineDropLowest:
                    break;
                case PipelineShuffle:
                    break;
                case PipelineDraw:
                    break;
                case PipelineChoose:
                    break;
                case PipelineChooseRandom:
                    break;
                case PipelineChooseWeighted:
                    break;
                case PipelineDicePatternCountAny:
                    break;
                case PipelineDicePatternCountFace:
                    break;
                case PipelineDicePatternFullHouse:
                    break;
                case PipelineDicePatternStraight:
                    break;
                case PipelineTakePatternCountAny:
                    break;
                case PipelineTakePatternCountFace:
                    break;
                case PipelineTakePatternFullHouse:
                    break;
                case PipelineTakePatternStraight:
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(instruction.OpCode), instruction.OpCode, null);
            }
        }

        return true;
    }
}
