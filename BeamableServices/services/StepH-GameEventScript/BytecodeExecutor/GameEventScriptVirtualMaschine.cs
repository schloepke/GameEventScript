#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using StepH.GameEventScript.Api;
using StepH.GameEventScript.Types;
using static StepH.GameEventScript.Api.GameEventScriptBytecodeOpCode;
using static StepH.GameEventScript.BytecodeExecutor.VmRegisterUnitCalculation;

namespace StepH.GameEventScript.BytecodeExecutor;

public class GameEventScriptVirtualMaschine(GameEventScriptBinary binary, ushort registerSize, ushort stackSize)
{
    private VmState _vmState = new(binary, registerSize, stackSize);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ref VmValue Register(ushort index) => ref _vmState.RegisterSlots[index + _vmState.RegisterFrameStart];

    public bool ExecuteMessage(GameEventScriptMessage message, GameEventScriptContext context)
    {
        if (!_vmState.PrepareMessage(message, context)) return false;
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
                case LoadNothing:
                    Register(instruction.DestinationSlot).SetNothing();
                    break;
                case LoadTrue:
                    Register(instruction.DestinationSlot).SetBoolean(true);
                    break;
                case LoadFalse:
                    Register(instruction.DestinationSlot).SetBoolean(false);
                    break;
                case LoadInteger:
                    Register(instruction.DestinationSlot).SetInteger(instruction.I64, DecodeNumericUnit(instruction.UnitAndFlags));
                    break;
                case LoadFloat:
                    Register(instruction.DestinationSlot).SetFloat(instruction.F64, DecodeNumericUnit(instruction.UnitAndFlags));
                    break;
                case LoadPercentage:
                    Register(instruction.DestinationSlot).SetPercentage(instruction.F64);
                    break;
                case LoadText:
                    Register(instruction.DestinationSlot).SetStringPointer(instruction.StringIndex);
                    break;
                case LoadTag:
                    Register(instruction.DestinationSlot).SetTagPointer(instruction.StringIndex);
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
                case MoveSlot:
                    Register(instruction.DestinationSlot) = Register(instruction.XSlot);
                    break;
                case Jump:
                    _vmState.JumpAddress(instruction.TargetAddress);
                    break;
                case JumpIfTrue:
                    if (Register(instruction.ConditionSlot).IsTrue) _vmState.JumpAddress(instruction.TargetAddress);
                    break;
                case JumpIfFalse:
                    if (Register(instruction.ConditionSlot).IsFalse) _vmState.JumpAddress(instruction.TargetAddress);
                    break;
                case JumpIfNotTrue:
                    if (Register(instruction.ConditionSlot).IsNotTrue) _vmState.JumpAddress(instruction.TargetAddress);
                    break;
                case ReturnVoid:
                    _vmState.ReturnVoid();
                    break;
                case ReturnValue:
                    _vmState.ReturnValue(instruction.XSlot);
                    break;
                case Or:
                    Register(instruction.DestinationSlot).VmOr(ref Register(instruction.XSlot), ref Register(instruction.YSlot));
                    break;
                case And:
                    Register(instruction.DestinationSlot).VmAnd(ref Register(instruction.XSlot), ref Register(instruction.YSlot));
                    break;
                case Xor:
                    Register(instruction.DestinationSlot).VmXor(ref Register(instruction.XSlot), ref Register(instruction.YSlot));
                    break;
                case Equal:
                    Register(instruction.DestinationSlot).VmEqual(ref Register(instruction.XSlot), ref Register(instruction.YSlot));
                    break;
                case NotEqual:
                    Register(instruction.DestinationSlot).VmNotEqual(ref Register(instruction.XSlot), ref Register(instruction.YSlot));
                    break;
                case ApproxEqual:
                    Register(instruction.DestinationSlot).VmApproxEqual(ref Register(instruction.XSlot), ref Register(instruction.YSlot));
                    break;
                case Less:
                    Register(instruction.DestinationSlot).VmLess(ref Register(instruction.XSlot), ref Register(instruction.YSlot));
                    break;
                case Greater:
                    Register(instruction.DestinationSlot).VmGreater(ref Register(instruction.XSlot), ref Register(instruction.YSlot));
                    break;
                case LessOrEqual:
                    Register(instruction.DestinationSlot).VmLessOrEqual(ref Register(instruction.XSlot), ref Register(instruction.YSlot));
                    break;
                case GreaterOrEqual:
                    Register(instruction.DestinationSlot).VmGreaterOrEqual(ref Register(instruction.XSlot), ref Register(instruction.YSlot));
                    break;
                case Add:
                    Register(instruction.DestinationSlot).VmAdd(ref Register(instruction.XSlot), ref Register(instruction.YSlot));
                    break;
                case Subtract:
                    Register(instruction.DestinationSlot).VmSubtract(ref Register(instruction.XSlot), ref Register(instruction.YSlot));
                    break;
                case Multiply:
                    Register(instruction.DestinationSlot).VmMultiply(ref Register(instruction.XSlot), ref Register(instruction.YSlot));
                    break;
                case Divide:
                    Register(instruction.DestinationSlot).VmDivide(ref Register(instruction.XSlot), ref Register(instruction.YSlot));
                    break;
                case Power:
                    Register(instruction.DestinationSlot).VmPower(ref Register(instruction.XSlot), ref Register(instruction.YSlot));
                    break;
                case Default:
                    Register(instruction.DestinationSlot).VmDefault(ref Register(instruction.XSlot), ref Register(instruction.YSlot));
                    break;
                case IntEqual:
                    Register(instruction.DestinationSlot).VmIntegerEqual(ref Register(instruction.XSlot), ref Register(instruction.YSlot));
                    break;
                case IntNotEqual:
                    Register(instruction.DestinationSlot).VmIntegerNotEqual(ref Register(instruction.XSlot), ref Register(instruction.YSlot));
                    break;
                case IntLess:
                    Register(instruction.DestinationSlot).VmIntegerLess(ref Register(instruction.XSlot), ref Register(instruction.YSlot));
                    break;
                case IntGreater:
                    Register(instruction.DestinationSlot).VmIntegerGreater(ref Register(instruction.XSlot), ref Register(instruction.YSlot));
                    break;
                case IntLessOrEqual:
                    Register(instruction.DestinationSlot).VmIntegerLessOrEqual(ref Register(instruction.XSlot), ref Register(instruction.YSlot));
                    break;
                case IntGreaterOrEqual:
                    Register(instruction.DestinationSlot).VmIntegerGreaterOrEqual(ref Register(instruction.XSlot), ref Register(instruction.YSlot));
                    break;
                case IntAdd:
                    Register(instruction.DestinationSlot).VmIntegerAdd(ref Register(instruction.XSlot), ref Register(instruction.YSlot));
                    break;
                case IntSubtract:
                    Register(instruction.DestinationSlot).VmIntegerSubtract(ref Register(instruction.XSlot), ref Register(instruction.YSlot));
                    break;
                case IntMultiply:
                    Register(instruction.DestinationSlot).VmIntegerMultiply(ref Register(instruction.XSlot), ref Register(instruction.YSlot));
                    break;
                case IntDivide:
                    Register(instruction.DestinationSlot).VmIntegerDivide(ref Register(instruction.XSlot), ref Register(instruction.YSlot));
                    break;
                case IntFloorDivide:
                    Register(instruction.DestinationSlot).VmIntegerFloorDivide(ref Register(instruction.XSlot), ref Register(instruction.YSlot));
                    break;
                case IntModulo:
                    Register(instruction.DestinationSlot).VmIntegerModulo(ref Register(instruction.XSlot), ref Register(instruction.YSlot));
                    break;
                case IntRemainder:
                    Register(instruction.DestinationSlot).VmIntegerRemainder(ref Register(instruction.XSlot), ref Register(instruction.YSlot));
                    break;
                case IntegerDivide:
                    Register(instruction.DestinationSlot).VmIntegerDivide(ref Register(instruction.XSlot), ref Register(instruction.YSlot));
                    break;
                case Modulo:
                    Register(instruction.DestinationSlot).VmModulo(ref Register(instruction.XSlot), ref Register(instruction.YSlot));
                    break;
                case Remainder:
                    Register(instruction.DestinationSlot).VmRemainder(ref Register(instruction.XSlot), ref Register(instruction.YSlot));
                    break;
                case UnaryNegate:
                    Register(instruction.DestinationSlot).VmNegate(ref Register(instruction.XSlot));
                    break;
                case UnaryNot:
                    Register(instruction.DestinationSlot).VmNot(ref Register(instruction.XSlot));
                    break;
                case UnaryHasValue:
                    Register(instruction.DestinationSlot).VmHasValue(ref Register(instruction.XSlot), ref binary.TextConstantTable);
                    break;
                case UnaryEmpty:
                    Register(instruction.DestinationSlot).VmEmpty(ref Register(instruction.XSlot), ref binary.TextConstantTable);
                    break;
                case UnaryLength:
                    Register(instruction.DestinationSlot).VmLength(ref Register(instruction.XSlot), ref binary.TextConstantTable);
                    break;
                case UnaryChance:
                    Register(instruction.DestinationSlot).VmChance(ref Register(instruction.XSlot), ref _vmState);
                    break;
                case UnaryAbs:
                    Register(instruction.DestinationSlot).VmAbs(ref Register(instruction.XSlot));
                    break;
                case UnaryNaturalLog:
                    Register(instruction.DestinationSlot).VmNaturalLog(ref Register(instruction.XSlot));
                    break;
                case Clamp:
                    Register(instruction.DestinationSlot).VmClamp(ref Register(instruction.XSlot), ref Register(instruction.YSlot), ref Register(instruction.AU));
                    break;
                case GameEventScriptBytecodeOpCode.Random:
                    Register(instruction.DestinationSlot).VmRandom(ref Register(instruction.XSlot), ref Register(instruction.YSlot), _vmState.RandomGenerator);
                    break;
                case Dice:
                    Register(instruction.DestinationSlot).VmDice(instruction.Count, instruction.ImmediateY, ref _vmState);
                    break;
                case RandomPush:
                    var seed = Register(instruction.XSlot);
                    _vmState.PushRandom(seed.Kind == VmValue.VmValueKind.Integer ? GameEventScriptRandomGenerator.FromSeed((int)seed.IntegerValue) : _vmState.RandomGenerator);
                    break;
                case RandomPushConstant:
                    _vmState.PushRandom(GameEventScriptRandomGenerator.FromSeed((int)instruction.I64));
                    break;
                case RandomPop:
                    _vmState.PopRandom();
                    break;
                case GameEventScriptBytecodeOpCode.Range:
                    break;

                case Implies:
                    break;
                
                case RangeWithStep:
                    break;
                case Contains:
                    break;
                case ContainsValue:
                    break;
                case StartsWith:
                    break;
                case EndsWith:
                    break;
                case Intersect:
                    break;
                case Combine:
                    break;
                case Except:
                    break;
                case Zip:
                    break;
                case Min:
                    break;
                case Max:
                    break;

                case UnaryKeys:
                    break;
                case UnaryValues:
                    break;
                case UnaryEntries:
                    break;

                case Cast:
                    break;
                case CastCustom:
                    break;
                case CastUnit:
                    break;

                case TypeCheck:
                    break;
                case TypeCheckCustom:
                    break;
                case CheckUnit:
                    break;
                case LoadHandler:
                    break;
                
                case TypeConstructor:
                    break;
                case MemberAccess:
                    break;
                case IndexedAccess:
                    break;
                
                case BuildList:
                    break;
                case BuildMap:
                    break;
                case LoadMessage:
                    break;
                case BindHandler:
                    break;
                
                case EmitMessage:
                    VmPublishMessage(binary.Uint16ConstantTable.Resolve(instruction.MessageDestination), binary.Uint16ConstantTable.Resolve(instruction.ListIndex), false, context);
                    break;
                case EmitMessageWithTags:
                    break;
                case PublishMessage:
                    VmPublishMessage(binary.Uint16ConstantTable.Resolve(instruction.MessageDestination), binary.Uint16ConstantTable.Resolve(instruction.ListIndex), true, context);
                    break;
                case PublishMessageWithTags:
                    break;
                case EmitMessageValue:
                    break;
                case EmitMessageValueWithTags:
                    break;
                case PublishMessageValue:
                    break;
                case PublishMessageValueWithTags:
                    break;
                
                case RangeIterator:
                    Register(instruction.DestinationSlot).SetObject(VmValue.VmValueKind.Iterator, new VmIntegerRangeIterator(instruction.XSlot, instruction.YSlot, instruction.AU));
                    break;
                case RangeIteratorWithStep:
                    break;
                case RangeIteratorShort:
                    Register(instruction.DestinationSlot).SetObject(VmValue.VmValueKind.Iterator, new VmIntegerRangeIterator(instruction.ImmediateX, instruction.ImmediateY, instruction.AS));
                    break;
                case CollectionIterator:
                    break;
                case IteratorNext:
                    Register(instruction.DestinationSlot).VmIteratorNext(ref Register(instruction.XSlot), instruction.TargetAddress, ref _vmState);
                    break;
                case IteratorClose:
                    Register(instruction.XSlot).VmIteratorClose();
                    break;
                case CollectionBuilderList:
                    break;
                case CollectionBuilderAdd:
                    break;
                case CollectionBuilderFinish:
                    break;
                case IteratorReduce:
                    break;
                case IteratorReduceOrDefault:
                    break;
                case IteratorFold:
                    break;
                case SeriesTerm:
                    break;
                case SeriesTake:
                    break;
                case SeriesDrop:
                    break;
                case Call:
                    _vmState.CallAddress(instruction.TargetAddress, instruction.DestinationSlot);
                    break;
                case CallPredicate:
                    _vmState.CallAddress(instruction.TargetAddress, instruction.DestinationSlot);
                    Register(instruction.DestinationSlot).Kind = VmValue.VmValueKind.Boolean;
                    break;
                case CallStandard:
                    break;
                case CallStandardPredicate:
                    break;
                case CallExternal:
                    break;
                case CallExternalPredicate:
                    break;
                case PipelineIterator:
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


    private bool VmPublishMessage(ReadOnlySpan<ushort> shape, ReadOnlySpan<ushort> argumentSlots, bool publish, GameEventScriptContext context)
    {
        if (shape.Length == 0 || argumentSlots.Length != shape.Length - 1) return false;
        var messageName = binary.TextConstantTable.Resolve(shape[0]);
        var pairs = new KeyValuePair<string, GameEventScriptValue>[argumentSlots.Length];
        for (var index = 0; index < argumentSlots.Length; index++)
        {
            pairs[index] = new KeyValuePair<string, GameEventScriptValue>(
                binary.TextConstantTable.Resolve(shape[index + 1]),
                Register(argumentSlots[index]).ToGameEventScriptValue());
        }

        var message = GameEventScriptMessage.Create(messageName, GameEventScriptNamedArguments.CreateOrdered(pairs));
        return publish ? context.Publish(message) : context.Emit(message);
    }
}
