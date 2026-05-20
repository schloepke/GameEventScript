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
                case ReserveSlots:
                    _vmState.AddLocalSlots(instruction.A_U16);
                    break;
                case ReleaseSlots:
                    _vmState.RemoveLocalSlots(instruction.A_U16);
                    break;
                case LoadNothing:
                    Register(instruction.Dest_U16).SetNothing();
                    break;
                case LoadTrue:
                    Register(instruction.Dest_U16).SetBoolean(true);
                    break;
                case LoadFalse:
                    Register(instruction.Dest_U16).SetBoolean(false);
                    break;
                case LoadInteger:
                    Register(instruction.Dest_U16).SetInteger(instruction.I64, DecodeNumericUnit(instruction.UnitAndFlags));
                    break;
                case LoadFloat:
                    Register(instruction.Dest_U16).SetFloat(instruction.F64, DecodeNumericUnit(instruction.UnitAndFlags));
                    break;
                case LoadPercentage:
                    Register(instruction.Dest_U16).SetPercentage(instruction.F64);
                    break;
                case LoadText:
                    Register(instruction.Dest_U16).SetStringPointer(instruction.A_U16);
                    break;
                case LoadTag:
                    Register(instruction.Dest_U16).SetTagPointer(instruction.A_U16);
                    break;
                case StageRegister:
                    _vmState.StageRegister(instruction.A_U16);
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
                    _vmState.StageTextConstant(instruction.C_U16);
                    break;
                case StageTag:
                    _vmState.StageTagConstant(instruction.C_U16);
                    break;
                case MoveSlot:
                    Register(instruction.Dest_U16) = Register(instruction.A_U16);
                    break;
                case Jump:
                    _vmState.JumpAddress(instruction.Target_U16);
                    break;
                case JumpIfTrue:
                    if (Register(instruction.Condition_U16).IsTrue) _vmState.JumpAddress(instruction.Target_U16);
                    break;
                case JumpIfFalse:
                    if (Register(instruction.Condition_U16).IsFalse) _vmState.JumpAddress(instruction.Target_U16);
                    break;
                case JumpIfNotTrue:
                    if (Register(instruction.Condition_U16).IsNotTrue) _vmState.JumpAddress(instruction.Target_U16);
                    break;
                case ReturnVoid:
                    _vmState.ReturnVoid();
                    break;
                case ReturnValue:
                    _vmState.ReturnValue(instruction.A_U16);
                    break;
                case Or:
                    Register(instruction.Dest_U16).VmOr(ref Register(instruction.A_U16), ref Register(instruction.B_U16));
                    break;
                case And:
                    Register(instruction.Dest_U16).VmAnd(ref Register(instruction.A_U16), ref Register(instruction.B_U16));
                    break;
                case Xor:
                    Register(instruction.Dest_U16).VmXor(ref Register(instruction.A_U16), ref Register(instruction.B_U16));
                    break;
                case Equal:
                    Register(instruction.Dest_U16).VmEqual(ref Register(instruction.A_U16), ref Register(instruction.B_U16));
                    break;
                case NotEqual:
                    Register(instruction.Dest_U16).VmNotEqual(ref Register(instruction.A_U16), ref Register(instruction.B_U16));
                    break;
                case ApproxEqual:
                    Register(instruction.Dest_U16).VmApproxEqual(ref Register(instruction.A_U16), ref Register(instruction.B_U16));
                    break;
                case Less:
                    Register(instruction.Dest_U16).VmLess(ref Register(instruction.A_U16), ref Register(instruction.B_U16));
                    break;
                case Greater:
                    Register(instruction.Dest_U16).VmGreater(ref Register(instruction.A_U16), ref Register(instruction.B_U16));
                    break;
                case LessOrEqual:
                    Register(instruction.Dest_U16).VmLessOrEqual(ref Register(instruction.A_U16), ref Register(instruction.B_U16));
                    break;
                case GreaterOrEqual:
                    Register(instruction.Dest_U16).VmGreaterOrEqual(ref Register(instruction.A_U16), ref Register(instruction.B_U16));
                    break;
                case Add:
                    Register(instruction.Dest_U16).VmAdd(ref Register(instruction.A_U16), ref Register(instruction.B_U16));
                    break;
                case Subtract:
                    Register(instruction.Dest_U16).VmSubtract(ref Register(instruction.A_U16), ref Register(instruction.B_U16));
                    break;
                case Multiply:
                    Register(instruction.Dest_U16).VmMultiply(ref Register(instruction.A_U16), ref Register(instruction.B_U16));
                    break;
                case Divide:
                    Register(instruction.Dest_U16).VmDivide(ref Register(instruction.A_U16), ref Register(instruction.B_U16));
                    break;
                case Power:
                    Register(instruction.Dest_U16).VmPower(ref Register(instruction.A_U16), ref Register(instruction.B_U16));
                    break;
                case Default:
                    Register(instruction.Dest_U16).VmDefault(ref Register(instruction.A_U16), ref Register(instruction.B_U16));
                    break;
                case IntEqual:
                    Register(instruction.Dest_U16).VmIntegerEqual(ref Register(instruction.A_U16), ref Register(instruction.B_U16));
                    break;
                case IntNotEqual:
                    Register(instruction.Dest_U16).VmIntegerNotEqual(ref Register(instruction.A_U16), ref Register(instruction.B_U16));
                    break;
                case IntLess:
                    Register(instruction.Dest_U16).VmIntegerLess(ref Register(instruction.A_U16), ref Register(instruction.B_U16));
                    break;
                case IntGreater:
                    Register(instruction.Dest_U16).VmIntegerGreater(ref Register(instruction.A_U16), ref Register(instruction.B_U16));
                    break;
                case IntLessOrEqual:
                    Register(instruction.Dest_U16).VmIntegerLessOrEqual(ref Register(instruction.A_U16), ref Register(instruction.B_U16));
                    break;
                case IntGreaterOrEqual:
                    Register(instruction.Dest_U16).VmIntegerGreaterOrEqual(ref Register(instruction.A_U16), ref Register(instruction.B_U16));
                    break;
                case IntAdd:
                    Register(instruction.Dest_U16).VmIntegerAdd(ref Register(instruction.A_U16), ref Register(instruction.B_U16));
                    break;
                case IntSubtract:
                    Register(instruction.Dest_U16).VmIntegerSubtract(ref Register(instruction.A_U16), ref Register(instruction.B_U16));
                    break;
                case IntMultiply:
                    Register(instruction.Dest_U16).VmIntegerMultiply(ref Register(instruction.A_U16), ref Register(instruction.B_U16));
                    break;
                case IntDivide:
                    Register(instruction.Dest_U16).VmIntegerDivide(ref Register(instruction.A_U16), ref Register(instruction.B_U16));
                    break;
                case IntFloorDivide:
                    Register(instruction.Dest_U16).VmIntegerFloorDivide(ref Register(instruction.A_U16), ref Register(instruction.B_U16));
                    break;
                case IntModulo:
                    Register(instruction.Dest_U16).VmIntegerModulo(ref Register(instruction.A_U16), ref Register(instruction.B_U16));
                    break;
                case IntRemainder:
                    Register(instruction.Dest_U16).VmIntegerRemainder(ref Register(instruction.A_U16), ref Register(instruction.B_U16));
                    break;
                case IntegerDivide:
                    Register(instruction.Dest_U16).VmIntegerDivide(ref Register(instruction.A_U16), ref Register(instruction.B_U16));
                    break;
                case Modulo:
                    Register(instruction.Dest_U16).VmModulo(ref Register(instruction.A_U16), ref Register(instruction.B_U16));
                    break;
                case Remainder:
                    Register(instruction.Dest_U16).VmRemainder(ref Register(instruction.A_U16), ref Register(instruction.B_U16));
                    break;
                case UnaryNegate:
                    Register(instruction.Dest_U16).VmNegate(ref Register(instruction.A_U16));
                    break;
                case UnaryNot:
                    Register(instruction.Dest_U16).VmNot(ref Register(instruction.A_U16));
                    break;
                case UnaryHasValue:
                    Register(instruction.Dest_U16).VmHasValue(ref Register(instruction.A_U16), ref binary.TextConstantTable);
                    break;
                case UnaryEmpty:
                    Register(instruction.Dest_U16).VmEmpty(ref Register(instruction.A_U16), ref binary.TextConstantTable);
                    break;
                case UnaryLength:
                    Register(instruction.Dest_U16).VmLength(ref Register(instruction.A_U16), ref binary.TextConstantTable);
                    break;
                case UnaryChance:
                    Register(instruction.Dest_U16).VmChance(ref Register(instruction.A_U16), ref _vmState);
                    break;
                case UnaryAbs:
                    Register(instruction.Dest_U16).VmAbs(ref Register(instruction.A_U16));
                    break;
                case UnaryNaturalLog:
                    Register(instruction.Dest_U16).VmNaturalLog(ref Register(instruction.A_U16));
                    break;
                case Clamp:
                    Register(instruction.Dest_U16).VmClamp(ref Register(instruction.A_U16), ref Register(instruction.B_U16), ref Register(instruction.C_U16));
                    break;
                case GameEventScriptBytecodeOpCode.Random:
                    Register(instruction.Dest_U16).VmRandom(ref Register(instruction.A_U16), ref Register(instruction.B_U16), _vmState.RandomGenerator);
                    break;
                case Dice:
                    Register(instruction.Dest_U16).VmDice(instruction.A_U16, instruction.B_U16, ref _vmState);
                    break;
                case RandomPush:
                    var seed = Register(instruction.A_U16);
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

                case UnaryKeys:
                    break;
                case UnaryValues:
                    break;
                case UnaryEntries:
                    break;

                case CastNothing:
                    break;
                case CastBoolean:
                    break;
                case CastInteger:
                    break;
                case CastFloat:
                    break;
                case CastNumber:
                    break;
                case CastPercentage:
                    break;
                case CastUnit:
                    break;
                case CastVector:
                    break;
                case CastPoint:
                    break;
                case CastUuid:
                    break;
                case CastCustom:
                    break;
                case CastSeries:
                    break;
                case CastEnvelope:
                    break;
                case CastRef:
                    break;
                case CastTag:
                    break;
                case CastText:
                    break;
                case CastList:
                    break;
                case CastRange:
                    break;
                case CastMessage:
                    break;
                case CastHandler:
                    break;
                case CastMap:
                    break;
                case CastDice:
                    break;
                
                case TypeCheckNothing:
                    break;
                case TypeCheckBoolean:
                    break;
                case TypeCheckInteger:
                    break;
                case TypeCheckFloat:
                    break;
                case TypeCheckPercentage:
                    break;
                case TypeCheckUnit:
                    break;
                case TypeCheckVector:
                    break;
                case TypeCheckPoint:
                    break;
                case TypeCheckUuid:
                    break;
                case TypeCheckTag:
                    break;
                case TypeCheckText:
                    break;
                case TypeCheckCustom:
                    break;
                case TypeCheckSeries:
                    break;
                case TypeCheckEnvelope:
                    break;
                case TypeCheckList:
                    break;
                case TypeCheckRange:
                    break;
                case TypeCheckMessage:
                    break;
                case TypeCheckHandler:
                    break;
                case TypeCheckRef:
                    break;
                case TypeCheckMap:
                    break;
                case TypeCheckDice:
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
                case BuildMessage:
                    break;
                case BindHandler:
                    break;
                case Variadic:
                    break;
                
                case EmitMessage:
                    VmPublishMessage(binary.Uint16ConstantTable.Resolve(instruction.A_U16), binary.Uint16ConstantTable.Resolve(instruction.B_U16), false, context);
                    break;
                case EmitMessageWithTags:
                    break;
                case PublishMessage:
                    VmPublishMessage(binary.Uint16ConstantTable.Resolve(instruction.A_U16), binary.Uint16ConstantTable.Resolve(instruction.B_U16), true, context);
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
                    Register(instruction.Dest_U16).SetObject(VmValue.VmValueKind.Iterator, new VmIntegerRangeIterator(instruction.A_U16, instruction.B_U16, instruction.C_U16));
                    break;
                case RangeIteratorWithStep:
                    break;
                case RangeIteratorShort:
                    Register(instruction.Dest_U16).SetObject(VmValue.VmValueKind.Iterator, new VmIntegerRangeIterator(instruction.A_U16, instruction.B_U16, instruction.C_U16));
                    break;
                case CollectionIterator:
                    break;
                case IteratorNext:
                    Register(instruction.Dest_U16).VmIteratorNext(ref Register(instruction.A_U16), instruction.B_U16, ref _vmState);
                    break;
                case IteratorClose:
                    Register(instruction.A_U16).VmIteratorClose();
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
                    _vmState.CallAddress(instruction.Target_U16, instruction.Dest_U16);
                    break;
                case CallPredicate:
                    _vmState.CallAddress(instruction.Target_U16, instruction.Dest_U16);
                    Register(instruction.Dest_U16).Kind = VmValue.VmValueKind.Boolean;
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
