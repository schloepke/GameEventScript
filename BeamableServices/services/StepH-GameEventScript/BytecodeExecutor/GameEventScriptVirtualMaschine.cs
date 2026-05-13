#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Runtime.CompilerServices;
using StepH.GameEventScript.Api;
using StepH.GameEventScript.Types;
using static StepH.GameEventScript.Api.GameEventScriptBytecodeOpCode;
using static StepH.GameEventScript.BytecodeExecutor.VmRegisterArithmetic;

namespace StepH.GameEventScript.BytecodeExecutor;

public class GameEventScriptVirtualMaschine(GameEventScriptBinary binary, ushort registerSize, ushort stackSize)
{
    private VmState _vmState = new(binary, registerSize, stackSize);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ref VmRegister Register(ushort index) => ref _vmState.RegisterSlots[index + _vmState.RegisterFrameStart];

    public bool ExecuteMessage(GameEventScriptMessage message, GameEventScriptContext context)
    {
        if (!_vmState.PrepareMessage(message)) return false;
        _vmState.State = VmState.StateValue.Running;
        while (_vmState.State == VmState.StateValue.Running)
        {
            var instruction = _vmState.FetchInstructionAndIncrementInstructionPointer();
            switch (instruction.OpCode)
            {
                case Nop:
                    break;
                case ReserveSlots:
                    var slotCount = instruction.A_U16;
                    var requiredTotalSlots = _vmState.RegisterFrameStart + slotCount;
                    if (requiredTotalSlots > _vmState.RegisterSlots.Length)
                    {
                        // FIXME: Here we might want to let the register frame grow.
                        throw new OverflowException("Not enough slots in register frame.");
                    }
                    _vmState.RegisterFrameLength = slotCount;
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
                    Register(instruction.Dest_U16).SetInteger(instruction.I64, (GameEventScriptNumericUnit)(instruction.UnitAndFlags & 0x1F));
                    break;
                case LoadFloat:
                    Register(instruction.Dest_U16).SetFloat(instruction.F64, (GameEventScriptNumericUnit)(instruction.UnitAndFlags & 0x1F));
                    break;
                case LoadText:
                    Register(instruction.Dest_U16).SetStringPointer(instruction.A_U16);
                    break;
                case LoadTag:
                    Register(instruction.Dest_U16).SetTagPointer(instruction.A_U16);
                    break;
                case MoveSlot:
                    Register(instruction.Dest_U16) = Register(instruction.A_U16);
                    break;
                case BindParameter:
                    var argument = message.Arguments[instruction.A_U16];
                    switch (argument.Kind)
                    {
                        case GameEventScriptValueKind.Nothing:
                            Register(instruction.Dest_U16).SetNothing();
                            break;
                        case GameEventScriptValueKind.Tag:
                            throw new NotImplementedException();
                            break;
                        case GameEventScriptValueKind.Text:
                            throw new NotImplementedException();
                            break;
                        case GameEventScriptValueKind.Percentage:
                            Register(instruction.Dest_U16).SetFloat(argument.AsNumber(), GameEventScriptNumericUnit.Percentage);
                            break;
                        case GameEventScriptValueKind.Vector:
                            throw new NotImplementedException();
                            break;
                        case GameEventScriptValueKind.Point:
                            throw new NotImplementedException();
                            break;
                        case GameEventScriptValueKind.Float:
                            Register(instruction.Dest_U16).SetFloat(argument.AsNumber());
                            break;
                        case GameEventScriptValueKind.Integer:
                            Register(instruction.Dest_U16).SetInteger(argument.AsInteger());
                            break;
                        case GameEventScriptValueKind.Boolean:
                            Register(instruction.Dest_U16).SetBoolean(argument.AsBoolean());
                            break;
                        case GameEventScriptValueKind.Uuid:
                            throw new NotImplementedException();
                            break;
                        case GameEventScriptValueKind.Optional:
                            throw new NotImplementedException();
                            break;
                        case GameEventScriptValueKind.Sequence:
                            throw new NotImplementedException();
                            break;
                        case GameEventScriptValueKind.Series:
                            throw new NotImplementedException();
                            break;
                        case GameEventScriptValueKind.Range:
                            throw new NotImplementedException();
                            break;
                        case GameEventScriptValueKind.Message:
                            throw new NotImplementedException();
                            break;
                        case GameEventScriptValueKind.Handler:
                            throw new NotImplementedException();
                            break;
                        case GameEventScriptValueKind.Ref:
                            throw new NotImplementedException();
                            break;
                        case GameEventScriptValueKind.List:
                            throw new NotImplementedException();
                            break;
                        case GameEventScriptValueKind.Dictionary:
                            throw new NotImplementedException();
                            break;
                        case GameEventScriptValueKind.Set:
                            throw new NotImplementedException();
                            break;
                        case GameEventScriptValueKind.Dice:
                            throw new NotImplementedException();
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
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
                    Register(instruction.Dest_U16).SetBooleanOrNothing(Register(instruction.A_U16).VmOr(ref Register(instruction.B_U16)));
                    break;
                case And:
                    Register(instruction.Dest_U16).SetBooleanOrNothing(Register(instruction.A_U16).VmAnd(ref Register(instruction.B_U16)));
                    break;
                case Xor:
                    Register(instruction.Dest_U16).SetBooleanOrNothing(Register(instruction.A_U16).VmXor(ref Register(instruction.B_U16)));
                    break;
                case Equal:
                    Register(instruction.Dest_U16).SetBooleanOrNothing(Register(instruction.A_U16).VmEqual(ref Register(instruction.B_U16)));
                    break;
                case NotEqual:
                    Register(instruction.Dest_U16).SetBooleanOrNothing(Register(instruction.A_U16).VmNotEqual(ref Register(instruction.B_U16)));
                    break;
                case ApproxEqual:
                    Register(instruction.Dest_U16).SetBooleanOrNothing(Register(instruction.A_U16).VmApproxEqual(ref Register(instruction.B_U16)));
                    break;
                case Less:
                    Register(instruction.Dest_U16).SetBooleanOrNothing(Register(instruction.A_U16).VmLess(ref Register(instruction.B_U16)));
                    break;
                case Greater:
                    Register(instruction.Dest_U16).SetBooleanOrNothing(Register(instruction.A_U16).VmGreater(ref Register(instruction.B_U16)));
                    break;
                case LessOrEqual:
                    Register(instruction.Dest_U16).SetBooleanOrNothing(Register(instruction.A_U16).VmLessOrEqual(ref Register(instruction.B_U16)));
                    break;
                case GreaterOrEqual:
                    Register(instruction.Dest_U16).SetBooleanOrNothing(Register(instruction.A_U16).VmGreaterOrEqual(ref Register(instruction.B_U16)));
                    break;
                case Add:
                    Register(instruction.Dest_U16).SetFloatOrNothing(Register(instruction.A_U16).VmAdd(ref Register(instruction.B_U16)));
                    break;
                case Subtract:
                    Register(instruction.Dest_U16).SetFloatOrNothing(Register(instruction.A_U16).VmSubtract(ref Register(instruction.B_U16)));
                    break;
                case Multiply:
                    Register(instruction.Dest_U16).SetFloatOrNothing(Register(instruction.A_U16).VmMultiply(ref Register(instruction.B_U16)));
                    break;
                case Divide:
                    Register(instruction.Dest_U16).SetFloatOrNothing(Register(instruction.A_U16).VmDivide(ref Register(instruction.B_U16)));
                    break;
                case Power:
                    Register(instruction.Dest_U16).SetFloatOrNothing(Register(instruction.A_U16).VmPower(ref Register(instruction.B_U16)));
                    break;
                case Default:
                    break;
                case PrimitiveIntegerEqual:
                    Register(instruction.Dest_U16).SetBooleanOrNothing(Register(instruction.A_U16).VmIntegerEqual(ref Register(instruction.B_U16)));
                    break;
                case PrimitiveIntegerNotEqual:
                    Register(instruction.Dest_U16).SetBooleanOrNothing(Register(instruction.A_U16).VmIntegerNotEqual(ref Register(instruction.B_U16)));
                    break;
                case PrimitiveIntegerLess:
                    Register(instruction.Dest_U16).SetBooleanOrNothing(Register(instruction.A_U16).VmIntegerLess(ref Register(instruction.B_U16)));
                    break;
                case PrimitiveIntegerGreater:
                    Register(instruction.Dest_U16).SetBooleanOrNothing(Register(instruction.A_U16).VmIntegerGreater(ref Register(instruction.B_U16)));
                    break;
                case PrimitiveIntegerLessOrEqual:
                    Register(instruction.Dest_U16).SetBooleanOrNothing(Register(instruction.A_U16).VmIntegerLessOrEqual( ref Register(instruction.B_U16)));
                    break;
                case PrimitiveIntegerGreaterOrEqual:
                    Register(instruction.Dest_U16).SetBooleanOrNothing(Register(instruction.A_U16).VmIntegerGreaterOrEqual( ref Register(instruction.B_U16)));
                    break;
                case PrimitiveIntegerAdd:
                    Register(instruction.Dest_U16).SetIntegerOrNothing(Register(instruction.A_U16).VmIntegerAdd( ref Register(instruction.B_U16)));
                    break;
                case PrimitiveIntegerSubtract:
                    Register(instruction.Dest_U16).SetIntegerOrNothing(Register(instruction.A_U16).VmIntegerSubtract( ref Register(instruction.B_U16)));
                    break;
                case PrimitiveIntegerMultiply:
                    Register(instruction.Dest_U16).SetIntegerOrNothing(Register(instruction.A_U16).VmIntegerMultiply( ref Register(instruction.B_U16)));
                    break;
                case PrimitiveIntegerDivide:
                    Register(instruction.Dest_U16).SetIntegerOrNothing(Register(instruction.A_U16).VmIntegerDivide( ref Register(instruction.B_U16)));
                    break;
                case PrimitiveIntegerFloorDivide:
                    Register(instruction.Dest_U16).SetIntegerOrNothing(Register(instruction.A_U16).VmIntegerFloorDivide( ref Register(instruction.B_U16)));
                    break;
                case PrimitiveIntegerModulo:
                    Register(instruction.Dest_U16).SetIntegerOrNothing(Register(instruction.A_U16).VmIntegerModulo( ref Register(instruction.B_U16)));
                    break;
                case PrimitiveIntegerRemainder:
                    Register(instruction.Dest_U16).SetIntegerOrNothing(Register(instruction.A_U16).VmIntegerRemainder( ref Register(instruction.B_U16)));
                    break;
                case IntegerDivide:
                    Register(instruction.Dest_U16).SetIntegerOrNothing(Register(instruction.A_U16).VmIntegerDivide( ref Register(instruction.B_U16)));
                    break;
                case Modulo:
                    Register(instruction.Dest_U16).SetFloatOrNothing(Register(instruction.A_U16).VmModulo(ref Register(instruction.B_U16)));
                    break;
                case Remainder:
                    Register(instruction.Dest_U16).SetFloatOrNothing(Register(instruction.A_U16).VmRemainder(ref Register(instruction.B_U16)));
                    break;
                case UnaryNegate:
                    var a = Register(instruction.A_U16);
                    if(a.IsInteger)
                        Register(instruction.Dest_U16).SetIntegerOrNothing(Register(instruction.A_U16).VmIntegerNegate(), a.Unit);
                    else 
                        Register(instruction.Dest_U16).SetFloatOrNothing(Register(instruction.A_U16).VmNegate(), a.Unit);
                    break;
                case UnaryNot:
                    Register(instruction.Dest_U16).SetBooleanOrNothing(Register(instruction.A_U16).VmNot());
                    break;
                case UnaryHasValue:
                    break;
                case UnaryEmpty:
                    break;
                case UnaryLength:
                    break;
                case UnaryChance:
                    break;
                case UnaryAbs:
                    break;
                case UnaryNaturalLog:
                    break;
                case Clamp:
                    break;
                case GameEventScriptBytecodeOpCode.Random:
                    break;
                case Dice:
                    break;
                case RandomPush:
                    break;
                case RandomPushConstant:
                    break;
                case RandomPop:
                    break;
                case GameEventScriptBytecodeOpCode.Range:
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
                case ShortCircuitOr:
                    break;
                case ShortCircuitAnd:
                    break;
                case ShortCircuitImplies:
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
                case CastDegree:
                    break;
                case CastMeter:
                    break;
                case CastSecond:
                    break;
                case CastVector:
                    break;
                case CastPoint:
                    break;
                case CastUuid:
                    break;
                case CastOptional:
                    break;
                case CastCustom:
                    break;
                case CastSequence:
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
                case CastDictionary:
                    break;
                case CastSet:
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
                case TypeCheckDegree:
                    break;
                case TypeCheckMeter:
                    break;
                case TypeCheckSecond:
                    break;
                case TypeCheckVector:
                    break;
                case TypeCheckPoint:
                    break;
                case TypeCheckUuid:
                    break;
                case TypeCheckOptional:
                    break;
                case TypeCheckTag:
                    break;
                case TypeCheckText:
                    break;
                case TypeCheckCustom:
                    break;
                case TypeCheckSequence:
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
                case TypeCheckDictionary:
                    break;
                case TypeCheckSet:
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
                case BuildSequence:
                    break;
                case BuildSet:
                    break;
                case BuildDictionary:
                    break;
                case BuildMessage:
                    break;
                case BindHandler:
                    break;
                case Variadic:
                    break;
                case EnterScope:
                    break;
                case ExitScope:
                    break;
                case EmitMessage:
                    break;
                case EmitMessageWithTags:
                    break;
                case PublishMessage:
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
                    break;
                case RangeIteratorWithStep:
                    break;
                case RangeIteratorShort:
                    break;
                case CollectionIterator:
                    break;
                case IteratorNext:
                    break;
                case IteratorClose:
                    break;
                case CollectionBuilderList:
                    break;
                case CollectionBuilderSet:
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
                    break;
                case CallPredicate:
                    _vmState.CallAddress(instruction.Target_U16, instruction.Dest_U16);
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
                case PipelineCollectSet:
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
                case PipelineDictionary:
                    break;
                case PipelineDictionaryValue:
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
                    throw new ArgumentOutOfRangeException();
            }
        }

        return true;
    }
}