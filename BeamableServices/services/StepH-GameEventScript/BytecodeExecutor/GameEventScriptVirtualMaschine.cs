#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Runtime.CompilerServices;
using StepH.GameEventScript.Api;
using StepH.GameEventScript.Types;
using static StepH.GameEventScript.BytecodeExecutor.VmRegisterArithmetic;
using static StepH.GameEventScript.BytecodeExecutor.VmRegisterCompare;

namespace StepH.GameEventScript.BytecodeExecutor;

public class GameEventScriptVirtualMaschine(GameEventScriptBinary binary, ushort globalRegisterCount, ushort maxLocalRegisterCount, ushort stackSize)
{
    private VmState _vmState = new(binary, stackSize, globalRegisterCount);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ref VmRegister Register(ushort index) => ref _vmState.GlobalRegisters[index];

    public bool ExecuteMessage(GameEventScriptMessage message)
    {
        if (!_vmState.PrepareMessage(message)) return false;
        _vmState.State = VmState.StateValue.Running;
        while (_vmState.State == VmState.StateValue.Running)
        {
            var instruction = _vmState.FetchInstructionAndIncrementInstructionPointer();
            switch (instruction.OpCode)
            {
                case GameEventScriptBytecodeOpCode.Nop:
                    break;
                case GameEventScriptBytecodeOpCode.LoadNothing:
                    Register(instruction.Dest_U16).SetNothing();
                    break;
                case GameEventScriptBytecodeOpCode.LoadTrue:
                    Register(instruction.Dest_U16).SetBoolean(true);
                    break;
                case GameEventScriptBytecodeOpCode.LoadFalse:
                    Register(instruction.Dest_U16).SetBoolean(false);
                    break;
                case GameEventScriptBytecodeOpCode.LoadInteger:
                    Register(instruction.Dest_U16).SetInteger(instruction.I64, (GameEventScriptNumericUnit)(instruction.UnitAndFlags & 0x1F));
                    break;
                case GameEventScriptBytecodeOpCode.LoadFloat:
                    Register(instruction.Dest_U16).SetFloat(instruction.F64, (GameEventScriptNumericUnit)(instruction.UnitAndFlags & 0x1F));
                    break;
                case GameEventScriptBytecodeOpCode.LoadText:
                    Register(instruction.Dest_U16).SetStringPointer(instruction.A_U16);
                    break;
                case GameEventScriptBytecodeOpCode.LoadTag:
                    Register(instruction.Dest_U16).SetTagPointer(instruction.A_U16);
                    break;
                case GameEventScriptBytecodeOpCode.MoveSlot:
                    Register(instruction.Dest_U16) = Register(instruction.A_U16);
                    break;
                case GameEventScriptBytecodeOpCode.BindParameter:
                    // FIXME: Needs correct implementation
                    throw new NotImplementedException();
                    break;
                case GameEventScriptBytecodeOpCode.Jump:
                    _vmState.JumpAddress(instruction.Target_U16);
                    break;
                case GameEventScriptBytecodeOpCode.JumpIfTrue:
                    if (Register(instruction.Condition_U16).IsTrue) _vmState.JumpAddress(instruction.Target_U16);
                    break;
                case GameEventScriptBytecodeOpCode.JumpIfFalse:
                    if (Register(instruction.Condition_U16).IsFalse) _vmState.JumpAddress(instruction.Target_U16);
                    break;
                case GameEventScriptBytecodeOpCode.JumpIfNotTrue:
                    if (Register(instruction.Condition_U16).IsNotTrue) _vmState.JumpAddress(instruction.Target_U16);
                    break;
                case GameEventScriptBytecodeOpCode.ReturnVoid:
                    _vmState.ReturnVoid();
                    break;
                case GameEventScriptBytecodeOpCode.ReturnValue:
                    _vmState.ReturnValue(instruction.A_U16);
                    break;
                case GameEventScriptBytecodeOpCode.Or:
                    Register(instruction.Dest_U16).SetBooleanOrNothing(Register(instruction.A_U16).VmOr(ref Register(instruction.B_U16)));
                    break;
                case GameEventScriptBytecodeOpCode.And:
                    Register(instruction.Dest_U16).SetBooleanOrNothing(Register(instruction.A_U16).VmAnd(ref Register(instruction.B_U16)));
                    break;
                case GameEventScriptBytecodeOpCode.Xor:
                    Register(instruction.Dest_U16).SetBooleanOrNothing(Register(instruction.A_U16).VmXor(ref Register(instruction.B_U16)));
                    break;
                case GameEventScriptBytecodeOpCode.Equal:
                    Register(instruction.Dest_U16).SetBooleanOrNothing(Register(instruction.A_U16).VmEqual(ref Register(instruction.B_U16)));
                    break;
                case GameEventScriptBytecodeOpCode.NotEqual:
                    Register(instruction.Dest_U16).SetBooleanOrNothing(Register(instruction.A_U16).VmNotEqual(ref Register(instruction.B_U16)));
                    break;
                case GameEventScriptBytecodeOpCode.ApproxEqual:
                    Register(instruction.Dest_U16).SetBooleanOrNothing(Register(instruction.A_U16).VmApproxEqual(ref Register(instruction.B_U16)));
                    break;
                case GameEventScriptBytecodeOpCode.Less:
                    Register(instruction.Dest_U16).SetBooleanOrNothing(Register(instruction.A_U16).VmLess(ref Register(instruction.B_U16)));
                    break;
                case GameEventScriptBytecodeOpCode.Greater:
                    Register(instruction.Dest_U16).SetBooleanOrNothing(Register(instruction.A_U16).VmGreater(ref Register(instruction.B_U16)));
                    break;
                case GameEventScriptBytecodeOpCode.LessOrEqual:
                    Register(instruction.Dest_U16).SetBooleanOrNothing(Register(instruction.A_U16).VmLessOrEqual(ref Register(instruction.B_U16)));
                    break;
                case GameEventScriptBytecodeOpCode.GreaterOrEqual:
                    Register(instruction.Dest_U16).SetBooleanOrNothing(Register(instruction.A_U16).VmGreaterOrEqual(ref Register(instruction.B_U16)));
                    break;
                case GameEventScriptBytecodeOpCode.Add:
                    Register(instruction.Dest_U16).SetFloatOrNothing(VmAdd(ref Register(instruction.A_U16), ref Register(instruction.B_U16)));
                    break;
                case GameEventScriptBytecodeOpCode.Subtract:
                    Register(instruction.Dest_U16).SetFloatOrNothing(VmSubtract(ref Register(instruction.A_U16), ref Register(instruction.B_U16)));
                    break;
                case GameEventScriptBytecodeOpCode.Multiply:
                    Register(instruction.Dest_U16).SetFloatOrNothing(VmMultiply(ref Register(instruction.A_U16), ref Register(instruction.B_U16)));
                    break;
                case GameEventScriptBytecodeOpCode.Divide:
                    Register(instruction.Dest_U16).SetFloatOrNothing(VmDivide(ref Register(instruction.A_U16), ref Register(instruction.B_U16)));
                    break;
                case GameEventScriptBytecodeOpCode.Power:
                    Register(instruction.Dest_U16).SetFloatOrNothing(VmPower(ref Register(instruction.A_U16), ref Register(instruction.B_U16)));
                    break;
                case GameEventScriptBytecodeOpCode.Default:
                    break;
                case GameEventScriptBytecodeOpCode.PrimitiveIntegerEqual:
                    Register(instruction.Dest_U16).SetBooleanOrNothing(Register(instruction.A_U16).VmIntegerEqual(ref Register(instruction.B_U16)));
                    break;
                case GameEventScriptBytecodeOpCode.PrimitiveIntegerNotEqual:
                    Register(instruction.Dest_U16).SetBooleanOrNothing(Register(instruction.A_U16).VmIntegerNotEqual(ref Register(instruction.B_U16)));
                    break;
                case GameEventScriptBytecodeOpCode.PrimitiveIntegerLess:
                    Register(instruction.Dest_U16).SetBooleanOrNothing(Register(instruction.A_U16).VmIntegerLess(ref Register(instruction.B_U16)));
                    break;
                case GameEventScriptBytecodeOpCode.PrimitiveIntegerGreater:
                    Register(instruction.Dest_U16).SetBooleanOrNothing(Register(instruction.A_U16).VmIntegerGreater(ref Register(instruction.B_U16)));
                    break;
                case GameEventScriptBytecodeOpCode.PrimitiveIntegerLessOrEqual:
                    Register(instruction.Dest_U16).SetBooleanOrNothing(Register(instruction.A_U16).VmIntegerLessOrEqual( ref Register(instruction.B_U16)));
                    break;
                case GameEventScriptBytecodeOpCode.PrimitiveIntegerGreaterOrEqual:
                    Register(instruction.Dest_U16).SetBooleanOrNothing(Register(instruction.A_U16).VmIntegerGreaterOrEqual( ref Register(instruction.B_U16)));
                    break;
                case GameEventScriptBytecodeOpCode.PrimitiveIntegerAdd:
                    Register(instruction.Dest_U16).SetIntegerOrNothing(Register(instruction.A_U16).VmIntegerAdd( ref Register(instruction.B_U16)));
                    break;
                case GameEventScriptBytecodeOpCode.PrimitiveIntegerSubtract:
                    Register(instruction.Dest_U16).SetIntegerOrNothing(Register(instruction.A_U16).VmIntegerSubtract( ref Register(instruction.B_U16)));
                    break;
                case GameEventScriptBytecodeOpCode.PrimitiveIntegerMultiply:
                    Register(instruction.Dest_U16).SetIntegerOrNothing(Register(instruction.A_U16).VmIntegerMultiply( ref Register(instruction.B_U16)));
                    break;
                case GameEventScriptBytecodeOpCode.PrimitiveIntegerDivide:
                    Register(instruction.Dest_U16).SetIntegerOrNothing(Register(instruction.A_U16).VmIntegerDivide( ref Register(instruction.B_U16)));
                    break;
                case GameEventScriptBytecodeOpCode.PrimitiveIntegerFloorDivide:
                    Register(instruction.Dest_U16).SetIntegerOrNothing(Register(instruction.A_U16).VmIntegerFloorDivide( ref Register(instruction.B_U16)));
                    break;
                case GameEventScriptBytecodeOpCode.PrimitiveIntegerModulo:
                    Register(instruction.Dest_U16).SetIntegerOrNothing(Register(instruction.A_U16).VmIntegerModulo( ref Register(instruction.B_U16)));
                    break;
                case GameEventScriptBytecodeOpCode.PrimitiveIntegerRemainder:
                    Register(instruction.Dest_U16).SetIntegerOrNothing(Register(instruction.A_U16).VmIntegerRemainder( ref Register(instruction.B_U16)));
                    break;
                case GameEventScriptBytecodeOpCode.IntegerDivide:
                    Register(instruction.Dest_U16).SetIntegerOrNothing(Register(instruction.A_U16).VmIntegerDivide( ref Register(instruction.B_U16)));
                    break;
                case GameEventScriptBytecodeOpCode.Modulo:
                    break;
                case GameEventScriptBytecodeOpCode.Remainder:
                    break;
                case GameEventScriptBytecodeOpCode.UnaryNegate:
                    break;
                case GameEventScriptBytecodeOpCode.UnaryNot:
                    break;
                case GameEventScriptBytecodeOpCode.UnaryHasValue:
                    break;
                case GameEventScriptBytecodeOpCode.UnaryEmpty:
                    break;
                case GameEventScriptBytecodeOpCode.UnaryLength:
                    break;
                case GameEventScriptBytecodeOpCode.UnaryChance:
                    break;
                case GameEventScriptBytecodeOpCode.UnaryAbs:
                    break;
                case GameEventScriptBytecodeOpCode.UnaryNaturalLog:
                    break;
                case GameEventScriptBytecodeOpCode.Clamp:
                    break;
                case GameEventScriptBytecodeOpCode.Random:
                    break;
                case GameEventScriptBytecodeOpCode.Dice:
                    break;
                case GameEventScriptBytecodeOpCode.RandomPush:
                    break;
                case GameEventScriptBytecodeOpCode.RandomPushConstant:
                    break;
                case GameEventScriptBytecodeOpCode.RandomPop:
                    break;
                case GameEventScriptBytecodeOpCode.Range:
                    break;
                case GameEventScriptBytecodeOpCode.RangeWithStep:
                    break;
                case GameEventScriptBytecodeOpCode.Contains:
                    break;
                case GameEventScriptBytecodeOpCode.ContainsValue:
                    break;
                case GameEventScriptBytecodeOpCode.StartsWith:
                    break;
                case GameEventScriptBytecodeOpCode.EndsWith:
                    break;
                case GameEventScriptBytecodeOpCode.Intersect:
                    break;
                case GameEventScriptBytecodeOpCode.Combine:
                    break;
                case GameEventScriptBytecodeOpCode.Except:
                    break;
                case GameEventScriptBytecodeOpCode.Zip:
                    break;
                case GameEventScriptBytecodeOpCode.UnaryKeys:
                    break;
                case GameEventScriptBytecodeOpCode.UnaryValues:
                    break;
                case GameEventScriptBytecodeOpCode.UnaryEntries:
                    break;
                case GameEventScriptBytecodeOpCode.ShortCircuitOr:
                    break;
                case GameEventScriptBytecodeOpCode.ShortCircuitAnd:
                    break;
                case GameEventScriptBytecodeOpCode.ShortCircuitImplies:
                    break;
                case GameEventScriptBytecodeOpCode.CastNothing:
                    break;
                case GameEventScriptBytecodeOpCode.CastBoolean:
                    break;
                case GameEventScriptBytecodeOpCode.CastInteger:
                    break;
                case GameEventScriptBytecodeOpCode.CastFloat:
                    break;
                case GameEventScriptBytecodeOpCode.CastNumber:
                    break;
                case GameEventScriptBytecodeOpCode.CastPercentage:
                    break;
                case GameEventScriptBytecodeOpCode.CastDegree:
                    break;
                case GameEventScriptBytecodeOpCode.CastMeter:
                    break;
                case GameEventScriptBytecodeOpCode.CastSecond:
                    break;
                case GameEventScriptBytecodeOpCode.CastVector:
                    break;
                case GameEventScriptBytecodeOpCode.CastPoint:
                    break;
                case GameEventScriptBytecodeOpCode.CastUuid:
                    break;
                case GameEventScriptBytecodeOpCode.CastOptional:
                    break;
                case GameEventScriptBytecodeOpCode.CastCustom:
                    break;
                case GameEventScriptBytecodeOpCode.CastSequence:
                    break;
                case GameEventScriptBytecodeOpCode.CastSeries:
                    break;
                case GameEventScriptBytecodeOpCode.CastEnvelope:
                    break;
                case GameEventScriptBytecodeOpCode.CastRef:
                    break;
                case GameEventScriptBytecodeOpCode.CastTag:
                    break;
                case GameEventScriptBytecodeOpCode.CastText:
                    break;
                case GameEventScriptBytecodeOpCode.CastList:
                    break;
                case GameEventScriptBytecodeOpCode.CastRange:
                    break;
                case GameEventScriptBytecodeOpCode.CastMessage:
                    break;
                case GameEventScriptBytecodeOpCode.CastHandler:
                    break;
                case GameEventScriptBytecodeOpCode.CastDictionary:
                    break;
                case GameEventScriptBytecodeOpCode.CastSet:
                    break;
                case GameEventScriptBytecodeOpCode.CastDice:
                    break;
                case GameEventScriptBytecodeOpCode.TypeCheckNothing:
                    break;
                case GameEventScriptBytecodeOpCode.TypeCheckBoolean:
                    break;
                case GameEventScriptBytecodeOpCode.TypeCheckInteger:
                    break;
                case GameEventScriptBytecodeOpCode.TypeCheckFloat:
                    break;
                case GameEventScriptBytecodeOpCode.TypeCheckPercentage:
                    break;
                case GameEventScriptBytecodeOpCode.TypeCheckDegree:
                    break;
                case GameEventScriptBytecodeOpCode.TypeCheckMeter:
                    break;
                case GameEventScriptBytecodeOpCode.TypeCheckSecond:
                    break;
                case GameEventScriptBytecodeOpCode.TypeCheckVector:
                    break;
                case GameEventScriptBytecodeOpCode.TypeCheckPoint:
                    break;
                case GameEventScriptBytecodeOpCode.TypeCheckUuid:
                    break;
                case GameEventScriptBytecodeOpCode.TypeCheckOptional:
                    break;
                case GameEventScriptBytecodeOpCode.TypeCheckTag:
                    break;
                case GameEventScriptBytecodeOpCode.TypeCheckText:
                    break;
                case GameEventScriptBytecodeOpCode.TypeCheckCustom:
                    break;
                case GameEventScriptBytecodeOpCode.TypeCheckSequence:
                    break;
                case GameEventScriptBytecodeOpCode.TypeCheckSeries:
                    break;
                case GameEventScriptBytecodeOpCode.TypeCheckEnvelope:
                    break;
                case GameEventScriptBytecodeOpCode.TypeCheckList:
                    break;
                case GameEventScriptBytecodeOpCode.TypeCheckRange:
                    break;
                case GameEventScriptBytecodeOpCode.TypeCheckMessage:
                    break;
                case GameEventScriptBytecodeOpCode.TypeCheckHandler:
                    break;
                case GameEventScriptBytecodeOpCode.TypeCheckRef:
                    break;
                case GameEventScriptBytecodeOpCode.TypeCheckDictionary:
                    break;
                case GameEventScriptBytecodeOpCode.TypeCheckSet:
                    break;
                case GameEventScriptBytecodeOpCode.TypeCheckDice:
                    break;
                case GameEventScriptBytecodeOpCode.LoadHandler:
                    break;
                case GameEventScriptBytecodeOpCode.TypeConstructor:
                    break;
                case GameEventScriptBytecodeOpCode.MemberAccess:
                    break;
                case GameEventScriptBytecodeOpCode.IndexedAccess:
                    break;
                case GameEventScriptBytecodeOpCode.BuildList:
                    break;
                case GameEventScriptBytecodeOpCode.BuildSequence:
                    break;
                case GameEventScriptBytecodeOpCode.BuildSet:
                    break;
                case GameEventScriptBytecodeOpCode.BuildDictionary:
                    break;
                case GameEventScriptBytecodeOpCode.BuildMessage:
                    break;
                case GameEventScriptBytecodeOpCode.BindHandler:
                    break;
                case GameEventScriptBytecodeOpCode.Variadic:
                    break;
                case GameEventScriptBytecodeOpCode.EnterScope:
                    break;
                case GameEventScriptBytecodeOpCode.ExitScope:
                    break;
                case GameEventScriptBytecodeOpCode.EmitMessage:
                    break;
                case GameEventScriptBytecodeOpCode.EmitMessageWithTags:
                    break;
                case GameEventScriptBytecodeOpCode.PublishMessage:
                    break;
                case GameEventScriptBytecodeOpCode.PublishMessageWithTags:
                    break;
                case GameEventScriptBytecodeOpCode.EmitMessageValue:
                    break;
                case GameEventScriptBytecodeOpCode.EmitMessageValueWithTags:
                    break;
                case GameEventScriptBytecodeOpCode.PublishMessageValue:
                    break;
                case GameEventScriptBytecodeOpCode.PublishMessageValueWithTags:
                    break;
                case GameEventScriptBytecodeOpCode.RangeIterator:
                    break;
                case GameEventScriptBytecodeOpCode.RangeIteratorWithStep:
                    break;
                case GameEventScriptBytecodeOpCode.RangeIteratorShort:
                    break;
                case GameEventScriptBytecodeOpCode.CollectionIterator:
                    break;
                case GameEventScriptBytecodeOpCode.IteratorNext:
                    break;
                case GameEventScriptBytecodeOpCode.IteratorClose:
                    break;
                case GameEventScriptBytecodeOpCode.CollectionBuilderList:
                    break;
                case GameEventScriptBytecodeOpCode.CollectionBuilderSet:
                    break;
                case GameEventScriptBytecodeOpCode.CollectionBuilderAdd:
                    break;
                case GameEventScriptBytecodeOpCode.CollectionBuilderFinish:
                    break;
                case GameEventScriptBytecodeOpCode.IteratorReduce:
                    break;
                case GameEventScriptBytecodeOpCode.IteratorReduceOrDefault:
                    break;
                case GameEventScriptBytecodeOpCode.IteratorFold:
                    break;
                case GameEventScriptBytecodeOpCode.SeriesTerm:
                    break;
                case GameEventScriptBytecodeOpCode.SeriesTake:
                    break;
                case GameEventScriptBytecodeOpCode.SeriesDrop:
                    break;
                case GameEventScriptBytecodeOpCode.Call:
                    break;
                case GameEventScriptBytecodeOpCode.CallPredicate:
                    break;
                case GameEventScriptBytecodeOpCode.CallStandard:
                    break;
                case GameEventScriptBytecodeOpCode.CallStandardPredicate:
                    break;
                case GameEventScriptBytecodeOpCode.CallExternal:
                    break;
                case GameEventScriptBytecodeOpCode.CallExternalPredicate:
                    break;
                case GameEventScriptBytecodeOpCode.PipelineIterator:
                    break;
                case GameEventScriptBytecodeOpCode.PipelineCollectList:
                    break;
                case GameEventScriptBytecodeOpCode.PipelineCollectSet:
                    break;
                case GameEventScriptBytecodeOpCode.PipelineFirst:
                    break;
                case GameEventScriptBytecodeOpCode.PipelineLast:
                    break;
                case GameEventScriptBytecodeOpCode.PipelineSingle:
                    break;
                case GameEventScriptBytecodeOpCode.PipelineHasAny:
                    break;
                case GameEventScriptBytecodeOpCode.PipelineHasAll:
                    break;
                case GameEventScriptBytecodeOpCode.PipelineContainsSingle:
                    break;
                case GameEventScriptBytecodeOpCode.PipelineContainsAny:
                    break;
                case GameEventScriptBytecodeOpCode.PipelineContainsAll:
                    break;
                case GameEventScriptBytecodeOpCode.PipelineDictionary:
                    break;
                case GameEventScriptBytecodeOpCode.PipelineDictionaryValue:
                    break;
                case GameEventScriptBytecodeOpCode.PipelineDistinct:
                    break;
                case GameEventScriptBytecodeOpCode.PipelineDistinctBy:
                    break;
                case GameEventScriptBytecodeOpCode.PipelineGroupBy:
                    break;
                case GameEventScriptBytecodeOpCode.PipelineReverse:
                    break;
                case GameEventScriptBytecodeOpCode.PipelineSortAscending:
                    break;
                case GameEventScriptBytecodeOpCode.PipelineSortDescending:
                    break;
                case GameEventScriptBytecodeOpCode.PipelineOrderByAscending:
                    break;
                case GameEventScriptBytecodeOpCode.PipelineOrderByDescending:
                    break;
                case GameEventScriptBytecodeOpCode.PipelineTakeFirst:
                    break;
                case GameEventScriptBytecodeOpCode.PipelineTakeLast:
                    break;
                case GameEventScriptBytecodeOpCode.PipelineTakeHighest:
                    break;
                case GameEventScriptBytecodeOpCode.PipelineTakeLowest:
                    break;
                case GameEventScriptBytecodeOpCode.PipelineDropFirst:
                    break;
                case GameEventScriptBytecodeOpCode.PipelineDropLast:
                    break;
                case GameEventScriptBytecodeOpCode.PipelineDropHighest:
                    break;
                case GameEventScriptBytecodeOpCode.PipelineDropLowest:
                    break;
                case GameEventScriptBytecodeOpCode.PipelineShuffle:
                    break;
                case GameEventScriptBytecodeOpCode.PipelineDraw:
                    break;
                case GameEventScriptBytecodeOpCode.PipelineChoose:
                    break;
                case GameEventScriptBytecodeOpCode.PipelineChooseRandom:
                    break;
                case GameEventScriptBytecodeOpCode.PipelineChooseWeighted:
                    break;
                case GameEventScriptBytecodeOpCode.PipelineDicePatternCountAny:
                    break;
                case GameEventScriptBytecodeOpCode.PipelineDicePatternCountFace:
                    break;
                case GameEventScriptBytecodeOpCode.PipelineDicePatternFullHouse:
                    break;
                case GameEventScriptBytecodeOpCode.PipelineDicePatternStraight:
                    break;
                case GameEventScriptBytecodeOpCode.PipelineTakePatternCountAny:
                    break;
                case GameEventScriptBytecodeOpCode.PipelineTakePatternCountFace:
                    break;
                case GameEventScriptBytecodeOpCode.PipelineTakePatternFullHouse:
                    break;
                case GameEventScriptBytecodeOpCode.PipelineTakePatternStraight:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        return true;
    }
}