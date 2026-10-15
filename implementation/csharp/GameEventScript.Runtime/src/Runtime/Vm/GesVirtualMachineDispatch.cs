// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using GameEventScript.Api;
using GameEventScript.Runtime.Values;
using static GameEventScript.Api.GesBytecodeOpCode;
using static GameEventScript.Api.GesBytecodeTypeKind;
using static GameEventScript.Runtime.Vm.GesVirtualMachine.StateValue;

namespace GameEventScript.Runtime.Vm;

internal sealed class GesVmException(GesDiagnostic diagnostic, Exception? innerException = null)
    : GesFatalRuntimeException(diagnostic, innerException);

internal partial class GesVirtualMachine
{
    internal void Begin(GesLinkedProgram program, GameEventScriptMessage message, bool matchArguments, ushort entryAddress, string handlerName, GesContext context)
    {
        if (State is Processing)
            throw new GesVmException(new GesDiagnostic(GesDiagnosticPhase.Runtime, GesDiagnosticCodes.RuntimeVmStateConflict, "Virtual machine is already processing another message."));
        if (State != Ready) Reset();
        if (!PrepareStateForMessage(program, message, matchArguments, entryAddress, handlerName, context) && ErrorDiagnostic is null)
            RaiseError(GesDiagnosticCodes.RuntimePreparationFailed, "Failed to prepare state for message.");
    }

    internal int RunSlice(GesContext context, int maxSteps)
    {
        if (maxSteps <= 0) throw new ArgumentOutOfRangeException(nameof(maxSteps), "RunSlice requires a positive integer as max steps");
        var opcodesExecuted = 0;
        var profiler = ActiveProgram?.Profiler;
        const string executionLimitDetail = "Execution step limit reached.";
        var reservedSteps = context.RuntimeBudget.ReserveExecutionSlice(maxSteps, executionLimitDetail);
        try
        {
            while (true)
            {
                profiler?.PhaseStarting(GesProfilePhase.StateCheck);
                if (State != Processing) break;
                profiler?.PhaseStarting(GesProfilePhase.BudgetCheck);
                if (context.RuntimeBudget.IsExhausted) break;
                profiler?.PhaseStarting(GesProfilePhase.SliceCheck);
                if (opcodesExecuted >= reservedSteps) break;
                profiler?.PhaseStarting(GesProfilePhase.Fetch);
                var instruction = FetchInstructionAndIncrementInstructionPointer();
                profiler?.InstructionStarting(InstructionPointer - 1);
                switch (instruction.OpCode)
                {
                    #region Group 1 - control, calls, messages, types, values

                    case Nop:
                        break;
                    case RegisterLocals:
                        ModifyLocals(instruction.Count);
                        break;
                    case Jump:
                        JumpAddress(instruction.TargetAddress);
                        break;
                    case JumpIfTrue:
                        if (IsRegisterTrue(instruction.ConditionRegister)) JumpAddress(instruction.TargetAddress);
                        break;
                    case JumpIfFalse:
                        if (IsRegisterFalse(instruction.ConditionRegister)) JumpAddress(instruction.TargetAddress);
                        break;
                    case JumpIfNotTrue:
                        if (IsRegisterNotTrue(instruction.ConditionRegister)) JumpAddress(instruction.TargetAddress);
                        break;
                    case JumpIfNothing:
                        if (IsRegisterNothing(instruction.ConditionRegister)) JumpAddress(instruction.TargetAddress);
                        break;

                    case Call:
                        CallAddress(instruction.TargetAddress, instruction.DestinationRegister, instruction.HasInstructionFlag(GesInstructionFlag.NormalizeResultAsPredicate));
                        break;
                    case CreateSeries:
                        ExecuteCreateSeries(instruction.DestinationRegister, (GesBytecodeSeriesKind)instruction.TypeOperand);
                        break;
                    case CallExternal:
                        ExecuteCallExternal(instruction.DestinationRegister, instruction.BindId, instruction.ListIndex, context, instruction.HasInstructionFlag(GesInstructionFlag.NormalizeResultAsPredicate));
                        break;

                    case GesBytecodeOpCode.ReturnVoid:
                        ReturnVoid();
                        break;
                    case GesBytecodeOpCode.ReturnValue:
                        ReturnValue(instruction.XRegister);
                        break;

                    case EmitInstant:
                    case EmitAfter:
                    case PublishInstant:
                    case PublishAfter:
                        SetBoolean(instruction.DestinationRegister, Send(instruction, context));
                        break;
                    case EmitMessage:
                        EmitOrPublishMessage(instruction.MessageDestination, Program.UInt16IndexLists.Resolve(instruction.ListIndex), false, context);
                        break;
                    case EmitMessageWithTags:
                        EmitOrPublishMessageWithTags(instruction.MessageDestination, Program.UInt16IndexLists.Resolve(instruction.ListIndex),
                            Program.UInt16IndexLists.Resolve(instruction.SecondaryListIndex), false, context);
                        break;
                    case EmitMessageValue:
                        EmitOrPublishMessageValue(in Register(instruction.XRegister), false, context);
                        break;
                    case EmitMessageValueWithTags:
                        EmitOrPublishMessageValueWithTags(in Register(instruction.XRegister), Program.UInt16IndexLists.Resolve(instruction.ListIndex), false, context);
                        break;

                    case PublishMessage:
                        EmitOrPublishMessage(instruction.MessageDestination, Program.UInt16IndexLists.Resolve(instruction.ListIndex), true, context);
                        break;
                    case PublishMessageWithTags:
                        EmitOrPublishMessageWithTags(instruction.MessageDestination, Program.UInt16IndexLists.Resolve(instruction.ListIndex),
                            Program.UInt16IndexLists.Resolve(instruction.SecondaryListIndex), true, context);
                        break;
                    case PublishMessageValue:
                        EmitOrPublishMessageValue(in Register(instruction.XRegister), true, context);
                        break;
                    case PublishMessageValueWithTags:
                        EmitOrPublishMessageValueWithTags(in Register(instruction.XRegister), Program.UInt16IndexLists.Resolve(instruction.ListIndex), true, context);
                        break;

                    case ConstructData:
                    {
                        if (ActiveProgram!.DataConstructionPositions(instruction.StringIndex, instruction.AU) is not { } positions)
                        {
                            SetNothing(instruction.DestinationRegister);
                            break;
                        }

                        var registers = Program.UInt16IndexLists.Resolve(instruction.ListIndex);
                        var arguments = new GesValue[registers.Length];
                        for (var i = 0; i < arguments.Length; i++) arguments[i] = Register(registers[i]);
                        var constructed = GesDataConstruction.Create(FetchStringByPointer(instruction.StringIndex), positions, arguments, this, context);
                        SetValue(instruction.DestinationRegister, constructed);
                        break;
                    }
                    case SplitText:
                    {
                        var split = GesDataConstruction.Split(Register(instruction.XRegister), Register(instruction.YRegister), instruction.AU == 1);
                        SetValue(instruction.DestinationRegister, split);
                        break;
                    }
                    case ParseLiteral:
                    {
                        var parsed = GesLiteralParser.Parse(in Register(instruction.XRegister), context, this);
                        GesLiteralEvaluation.Start(parsed, this, context, instruction.DestinationRegister);
                        break;
                    }
                    case Cast:
                        ExecuteCast(instruction.DestinationRegister, Register(instruction.XRegister), instruction.TypeKind, context);
                        break;
                    case CastCustom:
                        ExecuteCastCustom(instruction.DestinationRegister, Register(instruction.XRegister), instruction.SecondaryStringIndex);
                        break;
                    case CastUnit:
                        ExecuteCastUnit(instruction.DestinationRegister, Register(instruction.XRegister), instruction.Unit);
                        break;
                    case CastNumeric:
                        ExecuteCastNumeric(instruction.DestinationRegister, Register(instruction.XRegister));
                        break;

                    case CheckType:
                        ExecuteCheckType(instruction.DestinationRegister, Register(instruction.XRegister), instruction.TypeKind);
                        break;
                    case CheckCustomType:
                        ExecuteCheckCustomType(instruction.DestinationRegister, Register(instruction.XRegister), instruction.SecondaryStringIndex);
                        break;
                    case CheckUnit:
                        ExecuteCheckUnit(instruction.DestinationRegister, Register(instruction.XRegister), instruction.Unit);
                        break;
                    case CheckNumeric:
                        ExecuteCheckNumeric(instruction.DestinationRegister, Register(instruction.XRegister));
                        break;
                    case CheckInteger:
                        ExecuteCheckInteger(instruction.DestinationRegister, Register(instruction.XRegister));
                        break;
                    case CheckFractional:
                        ExecuteCheckFractional(instruction.DestinationRegister, Register(instruction.XRegister));
                        break;

                    case Move:
                        if ((instruction.UnitAndFlags & 0x40) != 0) ExecuteMoveNumeric(instruction.DestinationRegister, instruction.XRegister);
                        else SetValue(instruction.DestinationRegister, in Register(instruction.XRegister));
                        break;
                    case MemberAccess:
                        ExecuteMemberAccess(instruction.DestinationRegister, FetchStringByPointer(instruction.StringIndex), Register(instruction.YRegister));
                        break;
                    case IndexAccess:
                        ExecuteIndexAccess(instruction.DestinationRegister, instruction.Index, Register(instruction.YRegister));
                        break;
                    case PropertyAccess:
                        ExecutePropertyAccess(instruction.DestinationRegister, Register(instruction.XRegister), Register(instruction.YRegister));
                        break;
                    case BindHandler:
                        ExecuteBindHandler(instruction.DestinationRegister, Register(instruction.XRegister), Program.UInt16IndexLists.Resolve(instruction.ListIndex));
                        break;

                    case LoadNothing:
                        SetNothing(instruction.DestinationRegister);
                        break;
                    case LoadTrue:
                        SetBoolean(instruction.DestinationRegister, true);
                        break;
                    case LoadFalse:
                        SetBoolean(instruction.DestinationRegister, false);
                        break;
                    case LoadInteger:
                        SetInteger(instruction.DestinationRegister, instruction.I64, instruction.Unit);
                        break;
                    case LoadFloat:
                        SetFloat(instruction.DestinationRegister, instruction.F64, instruction.Unit);
                        break;
                    case LoadPercentage:
                        SetPercentage(instruction.DestinationRegister, instruction.F64);
                        break;
                    case LoadText:
                        SetTextConstant(instruction.DestinationRegister, instruction.StringIndex);
                        break;
                    case LoadTag:
                        SetTagConstant(instruction.DestinationRegister, instruction.StringIndex);
                        break;
                    case LoadHandler:
                        ExecuteLoadHandler(instruction.DestinationRegister, Program.UInt16IndexLists.Resolve(instruction.ListIndex));
                        break;
                    case LoadMessage:
                        ExecuteLoadMessage(instruction.DestinationRegister, Program.UInt16IndexLists.Resolve(instruction.SecondaryListIndex), Program.UInt16IndexLists.Resolve(instruction.ListIndex));
                        break;

                    case GesBytecodeOpCode.StageRegister:
                        StageRegister(instruction.XRegister);
                        break;
                    case GesBytecodeOpCode.StageNothing:
                        StageNothing();
                        break;
                    case StageTrue:
                        StageBoolean(true);
                        break;
                    case StageFalse:
                        StageBoolean(false);
                        break;
                    case GesBytecodeOpCode.StageInteger:
                        StageInteger(instruction.I64, instruction.Unit);
                        break;
                    case GesBytecodeOpCode.StageFloat:
                        StageFloat(instruction.F64, instruction.Unit);
                        break;
                    case StageText:
                        StageTextConstant(instruction.StringIndex);
                        break;
                    case StageTag:
                        StageTagConstant(instruction.StringIndex);
                        break;
                    case GesBytecodeOpCode.StagePercentage:
                        StagePercentage(instruction.F64);
                        break;

                    case CreateDice:
                        ExecuteCreateDice(instruction.DestinationRegister, instruction.Count, instruction.ImmediateY, context);
                        break;
                    case CreateVector:
                        ExecuteCreateVector(instruction.DestinationRegister, instruction.ImmediateX);
                        ClearStage();
                        break;
                    case CreatePoint:
                        ExecuteCreatePoint(instruction.DestinationRegister, instruction.ImmediateX);
                        ClearStage();
                        break;
                    case CreateList:
                        ExecuteCreateList(instruction.DestinationRegister);
                        ClearStage();
                        break;
                    case CreateMap:
                        ExecuteCreateMap(instruction.DestinationRegister, instruction.SecondaryListIndex);
                        ClearStage();
                        break;
                    case CreateRange:
                        ExecuteCreateRange(instruction.DestinationRegister, Register(instruction.XRegister), Register(instruction.YRegister));
                        break;
                    case CreateRangeWithStep:
                        ExecuteCreateRangeWithStep(instruction.DestinationRegister, Register(instruction.XRegister), Register(instruction.YRegister), Register(instruction.AU));
                        break;
                    case CreateRangeIterator:
                        ExecuteCreateRangeIterator(instruction.DestinationRegister, Register(instruction.XRegister), Register(instruction.YRegister), context);
                        break;
                    case CreateRangeIteratorWithStep:
                        ExecuteCreateRangeIteratorWithStep(instruction.DestinationRegister, Register(instruction.XRegister), Register(instruction.YRegister), Register(instruction.AU), context);
                        break;
                    case CreateRangeIteratorShort:
                        if (!context.RuntimeBudget.CheckRangeLengthWithinLimit(GesRangeMath.GetLength(instruction.ImmediateX, instruction.ImmediateY, instruction.AS), "For loop range would enumerate more range items than allowed."))
                        {
                            SetIterator(instruction.DestinationRegister, new GesIntegerRangeIterator(0, 0, 0));
                            break;
                        }

                        SetIterator(instruction.DestinationRegister, new GesIntegerRangeIterator(instruction.ImmediateX, instruction.ImmediateY, instruction.AS));
                        break;
                    case CreateRecord:
                        CallRecordConstructor(instruction.BindId, instruction.DestinationRegister);
                        break;
                    case CreateRecordValue:
                        ExecuteCreateRecordValue(instruction.DestinationRegister, Register(instruction.XRegister), instruction.SecondaryStringIndex);
                        break;
                    case CreateExternalType:
                        ExecuteCreateExternalType(instruction.DestinationRegister, instruction.BindId, instruction.ListIndex);
                        ClearStage();
                        break;

                    case HasValue:
                        SetBoolean(instruction.DestinationRegister, Register(instruction.XRegister).HasValue);
                        break;
                    case IsEmpty:
                        SetBoolean(instruction.DestinationRegister, !Register(instruction.XRegister).HasValue);
                        break;
                    case Default:
                        var a = Register(instruction.XRegister);
                        if (a.HasValue)
                        {
                            SetValue(instruction.DestinationRegister, in a);
                        }
                        else
                        {
                            SetValue(instruction.DestinationRegister, in Register(instruction.YRegister));
                        }

                        break;

                    #endregion

                    #region Group 2 - boolean algebra, comparison, math and random

                    case Or:
                        ExecuteOr(instruction.DestinationRegister, Register(instruction.XRegister), Register(instruction.YRegister));
                        break;
                    case And:
                        ExecuteAnd(instruction.DestinationRegister, Register(instruction.XRegister), Register(instruction.YRegister));
                        break;
                    case Xor:
                        ExecuteXor(instruction.DestinationRegister, Register(instruction.XRegister), Register(instruction.YRegister));
                        break;
                    case Implies:
                        ExecuteImplies(instruction.DestinationRegister, Register(instruction.XRegister), Register(instruction.YRegister));
                        break;
                    case Not:
                        ExecuteNot(instruction.DestinationRegister, Register(instruction.XRegister));
                        break;
                    case Equal:
                        ExecuteEqual(instruction.DestinationRegister, in Register(instruction.XRegister), in Register(instruction.YRegister));
                        break;
                    case NotEqual:
                        ExecuteNotEqual(instruction.DestinationRegister, in Register(instruction.XRegister), in Register(instruction.YRegister));
                        break;
                    case Less:
                        ExecuteLess(instruction.DestinationRegister, in Register(instruction.XRegister), in Register(instruction.YRegister));
                        break;
                    case Greater:
                        ExecuteGreater(instruction.DestinationRegister, in Register(instruction.XRegister), in Register(instruction.YRegister));
                        break;
                    case LessOrEqual:
                        ExecuteLessOrEqual(instruction.DestinationRegister, in Register(instruction.XRegister), in Register(instruction.YRegister));
                        break;
                    case GreaterOrEqual:
                        ExecuteGreaterOrEqual(instruction.DestinationRegister, in Register(instruction.XRegister), in Register(instruction.YRegister));
                        break;
                    case Add:
                        if ((instruction.UnitAndFlags & 0x40) != 0)
                            ExecuteAddNumeric(instruction.DestinationRegister, instruction.XRegister, instruction.YRegister);
                        else ExecuteAdd(instruction.DestinationRegister, instruction.XRegister, instruction.YRegister);
                        break;
                    case Subtract:
                        if ((instruction.UnitAndFlags & 0x40) != 0) ExecuteSubtractNumeric(instruction.DestinationRegister, instruction.XRegister, instruction.YRegister);
                        else ExecuteSubtract(instruction.DestinationRegister, Register(instruction.XRegister), Register(instruction.YRegister));
                        break;
                    case Multiply:
                        if ((instruction.UnitAndFlags & 0x40) != 0) ExecuteMultiplyNumeric(instruction.DestinationRegister, instruction.XRegister, instruction.YRegister);
                        else ExecuteMultiply(instruction.DestinationRegister, instruction.XRegister, instruction.YRegister);
                        break;
                    case Divide:
                        if ((instruction.UnitAndFlags & 0x40) != 0) ExecuteDivideNumeric(instruction.DestinationRegister, instruction.XRegister, instruction.YRegister);
                        else ExecuteDivide(instruction.DestinationRegister, Register(instruction.XRegister), Register(instruction.YRegister));
                        break;
                    case Power:
                        if ((instruction.UnitAndFlags & 0x40) != 0) ExecutePowerNumeric(instruction.DestinationRegister, instruction.XRegister, instruction.YRegister);
                        else ExecutePower(instruction.DestinationRegister, Register(instruction.XRegister), Register(instruction.YRegister));
                        break;
                    case IntegerDivide:
                        if ((instruction.UnitAndFlags & 0x40) != 0) ExecuteIntegerDivideNumeric(instruction.DestinationRegister, instruction.XRegister, instruction.YRegister);
                        else ExecuteIntegerDivide(instruction.DestinationRegister, Register(instruction.XRegister), Register(instruction.YRegister));
                        break;
                    case Modulo:
                        if ((instruction.UnitAndFlags & 0x40) != 0) ExecuteModuloNumeric(instruction.DestinationRegister, instruction.XRegister, instruction.YRegister);
                        else ExecuteModulo(instruction.DestinationRegister, instruction.XRegister, instruction.YRegister);
                        break;
                    case Remainder:
                        if ((instruction.UnitAndFlags & 0x40) != 0) ExecuteRemainderNumeric(instruction.DestinationRegister, instruction.XRegister, instruction.YRegister);
                        else ExecuteRemainder(instruction.DestinationRegister, Register(instruction.XRegister), Register(instruction.YRegister));
                        break;
                    case Min:
                        if ((instruction.UnitAndFlags & 0x40) != 0) ExecuteMinNumeric(instruction.DestinationRegister, instruction.XRegister, instruction.YRegister);
                        else ExecuteMin(instruction.DestinationRegister, Register(instruction.XRegister), Register(instruction.YRegister));
                        break;
                    case Max:
                        if ((instruction.UnitAndFlags & 0x40) != 0) ExecuteMaxNumeric(instruction.DestinationRegister, instruction.XRegister, instruction.YRegister);
                        else ExecuteMax(instruction.DestinationRegister, Register(instruction.XRegister), Register(instruction.YRegister));
                        break;
                    case Negate:
                        if ((instruction.UnitAndFlags & 0x40) != 0) ExecuteNegateNumeric(instruction.DestinationRegister, instruction.XRegister);
                        else ExecuteNegate(instruction.DestinationRegister, Register(instruction.XRegister));
                        break;
                    case Abs:
                        if ((instruction.UnitAndFlags & 0x40) != 0) ExecuteAbsNumeric(instruction.DestinationRegister, instruction.XRegister);
                        else ExecuteAbs(instruction.DestinationRegister, Register(instruction.XRegister));
                        break;
                    case LogN:
                        ExecuteLogN(instruction.DestinationRegister, Register(instruction.XRegister));
                        break;
                    case Chance:
                        ExecuteChance(instruction.DestinationRegister, Register(instruction.XRegister));
                        break;
                    case Clamp:
                        if ((instruction.UnitAndFlags & 0x40) != 0) ExecuteClampNumeric(instruction.DestinationRegister, instruction.XRegister, instruction.YRegister, instruction.AU);
                        else ExecuteClamp(instruction.DestinationRegister, Register(instruction.XRegister), Register(instruction.YRegister), Register(instruction.AU));
                        break;
                    case RandomTake:
                        ExecuteRandomTake(instruction.DestinationRegister, Register(instruction.XRegister), Register(instruction.YRegister), RandomGenerator);
                        break;
                    case RandomTakeFloat:
                        ExecuteRandomTakeFloat(instruction.DestinationRegister, Register(instruction.XRegister), Register(instruction.YRegister), RandomGenerator);
                        break;
                    case RandomPush:
                        var seed = Register(instruction.XRegister);
                        var pushed = seed.Kind == Integer && seed.Unit == GesQuantityUnit.UnitNone
                            ? RandomGenerator.Push(seed.IntegerValue)
                            : RandomGenerator.Push();
                        if (!pushed) context.RecordRandomScopeLimitReached();
                        break;
                    case RandomPushConstant:
                        if (!RandomGenerator.Push(instruction.I64)) context.RecordRandomScopeLimitReached();
                        break;
                    case RandomPop:
                        if (!RandomGenerator.Pop() && !context.RuntimeBudget.IsExhausted)
                            RaiseError(GesDiagnosticCodes.RuntimeRandomStackUnderflow, "Random scope pop crossed the active handler boundary.");
                        break;
                    case Term:
                        ExecuteTerm(instruction.DestinationRegister, Register(instruction.XRegister), Register(instruction.YRegister));
                        break;
                    case Exp:
                        ExecuteExp(instruction.DestinationRegister, Register(instruction.XRegister));
                        break;
                    case Floor:
                        ExecuteFloor(instruction.DestinationRegister, Register(instruction.XRegister));
                        break;
                    case Ceil:
                        ExecuteCeil(instruction.DestinationRegister, Register(instruction.XRegister));
                        break;
                    case Truncate:
                        ExecuteTruncate(instruction.DestinationRegister, Register(instruction.XRegister));
                        break;
                    case RoundHalfEven:
                        ExecuteRoundHalfEven(instruction.DestinationRegister, Register(instruction.XRegister));
                        break;
                    case RoundHalfUp:
                        ExecuteRoundHalfUp(instruction.DestinationRegister, Register(instruction.XRegister));
                        break;
                    case RoundHalfDown:
                        ExecuteRoundHalfDown(instruction.DestinationRegister, Register(instruction.XRegister));
                        break;
                    case DegreeToRadians:
                        ExecuteDegreeToRadians(instruction.DestinationRegister, Register(instruction.XRegister));
                        break;
                    case DegreeFromRadians:
                        ExecuteDegreeFromRadians(instruction.DestinationRegister, Register(instruction.XRegister));
                        break;
                    case WrapDegree:
                        ExecuteWrapDegree(instruction.DestinationRegister, Register(instruction.XRegister));
                        break;
                    case Sin:
                        ExecuteSin(instruction.DestinationRegister, Register(instruction.XRegister));
                        break;
                    case Cos:
                        ExecuteCos(instruction.DestinationRegister, Register(instruction.XRegister));
                        break;
                    case Tan:
                        ExecuteTan(instruction.DestinationRegister, Register(instruction.XRegister));
                        break;
                    case Asin:
                        ExecuteAsin(instruction.DestinationRegister, Register(instruction.XRegister));
                        break;
                    case Acos:
                        ExecuteAcos(instruction.DestinationRegister, Register(instruction.XRegister));
                        break;
                    case Atan:
                        ExecuteAtan(instruction.DestinationRegister, Register(instruction.XRegister));
                        break;
                    case Atan2:
                        ExecuteAtan2(instruction.DestinationRegister, Register(instruction.XRegister), Register(instruction.YRegister));
                        break;
                    case Hypot2D:
                        ExecuteHypot2D(instruction.DestinationRegister, Register(instruction.XRegister), Register(instruction.YRegister));
                        break;
                    case Hypot3D:
                        ExecuteHypot3D(instruction.DestinationRegister, Register(instruction.XRegister), Register(instruction.YRegister), Register(instruction.AU));
                        break;
                    case Distance:
                        ExecuteDistance(instruction.DestinationRegister, Register(instruction.XRegister), Register(instruction.YRegister));
                        break;
                    case Distance2D:
                        ExecuteDistance2D(instruction.DestinationRegister, Register(instruction.XRegister), Register(instruction.YRegister), Register(instruction.AU), Register(instruction.BU));
                        break;
                    case Distance3D:
                        ExecuteDistance3D(instruction.DestinationRegister, Register(instruction.XRegister), Register(instruction.YRegister), Register(instruction.AU), Register(instruction.BU),
                            Register(instruction.CU), Register(instruction.DU));
                        break;
                    case DistanceSquared:
                        ExecuteDistanceSquared(instruction.DestinationRegister, Register(instruction.XRegister), Register(instruction.YRegister));
                        break;
                    case DistanceSquared2D:
                        ExecuteDistanceSquared2D(instruction.DestinationRegister, Register(instruction.XRegister), Register(instruction.YRegister), Register(instruction.AU), Register(instruction.BU));
                        break;
                    case DistanceSquared3D:
                        ExecuteDistanceSquared3D(instruction.DestinationRegister, Register(instruction.XRegister), Register(instruction.YRegister), Register(instruction.AU), Register(instruction.BU),
                            Register(instruction.CU), Register(instruction.DU));
                        break;
                    case LengthSquared:
                        ExecuteLengthSquared(instruction.DestinationRegister, Register(instruction.XRegister));
                        break;
                    case LengthSquared2D:
                        ExecuteLengthSquared2D(instruction.DestinationRegister, Register(instruction.XRegister), Register(instruction.YRegister));
                        break;
                    case LengthSquared3D:
                        ExecuteLengthSquared3D(instruction.DestinationRegister, Register(instruction.XRegister), Register(instruction.YRegister), Register(instruction.AU));
                        break;
                    case Normalize:
                        ExecuteNormalize(instruction.DestinationRegister, Register(instruction.XRegister));
                        break;
                    case Normalize2D:
                        ExecuteNormalize2D(instruction.DestinationRegister, Register(instruction.XRegister), Register(instruction.YRegister));
                        break;
                    case Normalize3D:
                        ExecuteNormalize3D(instruction.DestinationRegister, Register(instruction.XRegister), Register(instruction.YRegister), Register(instruction.AU));
                        break;
                    case Dot:
                        ExecuteDot(instruction.DestinationRegister, Register(instruction.XRegister), Register(instruction.YRegister));
                        break;
                    case Dot2D:
                        ExecuteDot2D(instruction.DestinationRegister, Register(instruction.XRegister), Register(instruction.YRegister), Register(instruction.AU), Register(instruction.BU));
                        break;
                    case Dot3D:
                        ExecuteDot3D(instruction.DestinationRegister, Register(instruction.XRegister), Register(instruction.YRegister), Register(instruction.AU), Register(instruction.BU),
                            Register(instruction.CU), Register(instruction.DU));
                        break;
                    case Cross:
                        ExecuteCross(instruction.DestinationRegister, Register(instruction.XRegister), Register(instruction.YRegister));
                        break;
                    case Cross2D:
                        ExecuteCross2D(instruction.DestinationRegister, Register(instruction.XRegister), Register(instruction.YRegister), Register(instruction.AU), Register(instruction.BU));
                        break;
                    case Cross3D:
                        ExecuteCross3D(instruction.DestinationRegister, Register(instruction.XRegister), Register(instruction.YRegister), Register(instruction.AU), Register(instruction.BU),
                            Register(instruction.CU), Register(instruction.DU));
                        break;
                    case AngleBetween:
                        ExecuteAngleBetween(instruction.DestinationRegister, Register(instruction.XRegister), Register(instruction.YRegister));
                        break;
                    case AngleBetween2D:
                        ExecuteAngleBetween2D(instruction.DestinationRegister, Register(instruction.XRegister), Register(instruction.YRegister), Register(instruction.AU), Register(instruction.BU));
                        break;
                    case AngleBetween3D:
                        ExecuteAngleBetween3D(instruction.DestinationRegister, Register(instruction.XRegister), Register(instruction.YRegister), Register(instruction.AU), Register(instruction.BU),
                            Register(instruction.CU), Register(instruction.DU));
                        break;

                    #endregion

                    #region Group 3 - text, collection, iterators

                    case TakeFirst:
                        ExecuteTakeFirst(instruction.DestinationRegister, Register(instruction.XRegister), instruction.ImmediateY);
                        break;
                    case DropFirst:
                        ExecuteDropFirst(instruction.DestinationRegister, Register(instruction.XRegister), instruction.ImmediateY);
                        break;
                    case TakeLast:
                        ExecuteTakeLast(instruction.DestinationRegister, Register(instruction.XRegister), instruction.ImmediateY);
                        break;
                    case DropLast:
                        ExecuteDropLast(instruction.DestinationRegister, Register(instruction.XRegister), instruction.ImmediateY);
                        break;
                    case TakeHighest:
                        ExecuteTakeHighest(instruction.DestinationRegister, Register(instruction.XRegister), instruction.ImmediateY);
                        break;
                    case TakeLowest:
                        ExecuteTakeLowest(instruction.DestinationRegister, Register(instruction.XRegister), instruction.ImmediateY);
                        break;
                    case DropHighest:
                        ExecuteDropHighest(instruction.DestinationRegister, Register(instruction.XRegister), instruction.ImmediateY);
                        break;
                    case DropLowest:
                        ExecuteDropLowest(instruction.DestinationRegister, Register(instruction.XRegister), instruction.ImmediateY);
                        break;
                    case OneRandom:
                        ExecuteOneRandom(instruction.DestinationRegister, Register(instruction.XRegister), RandomGenerator);
                        break;
                    case TakeRandom:
                        ExecuteTakeRandom(instruction.DestinationRegister, Register(instruction.XRegister), instruction.ImmediateY, RandomGenerator);
                        break;
                    case OneWeighted:
                        ExecuteOneWeighted(instruction.DestinationRegister, Register(instruction.XRegister), Register(instruction.YRegister), RandomGenerator);
                        break;
                    case TakeWeighted:
                        ExecuteTakeWeighted(instruction.DestinationRegister, Register(instruction.XRegister), Register(instruction.AU), instruction.ImmediateY, RandomGenerator);
                        break;
                    case Count:
                        ExecuteCount(instruction.DestinationRegister, Register(instruction.XRegister));
                        break;
                    case StartsWith:
                        ExecuteStartsWith(instruction.DestinationRegister, Register(instruction.XRegister), Register(instruction.YRegister));
                        break;
                    case EndsWith:
                        ExecuteEndsWith(instruction.DestinationRegister, Register(instruction.XRegister), Register(instruction.YRegister));
                        break;
                    case Contains:
                        ExecuteContains(instruction.DestinationRegister, Register(instruction.XRegister), Register(instruction.YRegister));
                        break;
                    case ContainsAny:
                        ExecuteContainsAny(instruction.DestinationRegister, Register(instruction.XRegister), Register(instruction.YRegister));
                        break;
                    case ContainsAll:
                        ExecuteContainsAll(instruction.DestinationRegister, Register(instruction.XRegister), Register(instruction.YRegister));
                        break;
                    case ContainsValue:
                        ExecuteContainsValue(instruction.DestinationRegister, Register(instruction.XRegister), Register(instruction.YRegister));
                        break;
                    case Union:
                        ExecuteUnion(instruction.DestinationRegister, Register(instruction.XRegister), Register(instruction.YRegister));
                        break;
                    case Intersect:
                        ExecuteIntersect(instruction.DestinationRegister, Register(instruction.XRegister), Register(instruction.YRegister));
                        break;
                    case Zip:
                        ExecuteZip(instruction.DestinationRegister, Register(instruction.XRegister), Register(instruction.YRegister));
                        break;
                    case KeysOfMap:
                        ExecuteKeysOfMap(instruction.DestinationRegister, Register(instruction.XRegister));
                        break;
                    case ValuesOfMap:
                        ExecuteValuesOfMap(instruction.DestinationRegister, Register(instruction.XRegister));
                        break;
                    case EntriesOfMap:
                        ExecuteEntriesOfMap(instruction.DestinationRegister, Register(instruction.XRegister));
                        break;
                    case GesBytecodeOpCode.First:
                        ExecuteFirst(instruction.DestinationRegister, Register(instruction.XRegister));
                        break;
                    case GesBytecodeOpCode.Last:
                        ExecuteLast(instruction.DestinationRegister, Register(instruction.XRegister));
                        break;
                    case GesBytecodeOpCode.Single:
                        ExecuteSingle(instruction.DestinationRegister, Register(instruction.XRegister));
                        break;
                    case Cartesian:
                        ExecuteCartesian(instruction.DestinationRegister, Register(instruction.XRegister), Register(instruction.YRegister), context.RuntimeBudget);
                        break;
                    case IteratorCreate:
                        ExecuteIteratorCreate(instruction, context.RuntimeBudget);
                        break;
                    case IteratorCreateOrJump:
                        ExecuteIteratorCreate(instruction, context.RuntimeBudget);
                        if (IsRegisterNothing(instruction.DestinationRegister)) JumpAddress(instruction.TargetAddress);
                        break;
                    case IteratorNext:
                    {
                        if (instruction.UnitAndFlags != 0 ? ExecuteIteratorNextComponents(instruction) : ExecuteIteratorNext(instruction.DestinationRegister, Register(instruction.XRegister), instruction.TargetAddress))
                        {
                            context.RuntimeBudget.ConsumeLoopIterationIfAvailable("For loop iteration exceeds the configured limit.");
                        }

                        break;
                    }
                    case IteratorClose:
                        ExecuteIteratorClose(instruction.XRegister);
                        break;
                    case HasAny:
                        ExecuteHasAny(instruction.DestinationRegister, Register(instruction.XRegister));
                        break;
                    case HasAll:
                        ExecuteHasAll(instruction.DestinationRegister, Register(instruction.XRegister));
                        break;
                    case Distinct:
                        ExecuteDistinct(instruction.DestinationRegister, Register(instruction.XRegister));
                        break;
                    case SortAscending:
                        ExecuteSortAscending(instruction.DestinationRegister, Register(instruction.XRegister));
                        break;
                    case SortDescending:
                        ExecuteSortDescending(instruction.DestinationRegister, Register(instruction.XRegister));
                        break;
                    case Reverse:
                        ExecuteReverse(instruction.DestinationRegister, Register(instruction.XRegister));
                        break;
                    case Shuffle:
                        ExecuteShuffle(instruction.DestinationRegister, Register(instruction.XRegister), RandomGenerator);
                        break;
                    case ListBuilderCreate:
                        ExecuteListBuilderCreate(instruction.DestinationRegister);
                        break;
                    case ListBuilderAdd:
                        ExecuteListBuilderAdd(Register(instruction.XRegister), in Register(instruction.YRegister), context.RuntimeBudget);
                        break;
                    case ListBuilderFinish:
                        ExecuteListBuilderFinish(instruction.DestinationRegister, in Register(instruction.XRegister));
                        break;
                    case MapBuilderCreate:
                        ExecuteMapBuilderCreate(instruction.DestinationRegister);
                        break;
                    case MapBuilderAdd:
                        ExecuteMapBuilderAdd(Register(instruction.XRegister), in Register(instruction.YRegister), in Register(instruction.AU), context.RuntimeBudget);
                        break;
                    case MapBuilderFinish:
                        ExecuteMapBuilderFinish(instruction.DestinationRegister, in Register(instruction.XRegister));
                        break;
                    case DistinctBuilderCreate:
                        ExecuteDistinctBuilderCreate(instruction.DestinationRegister);
                        break;
                    case DistinctBuilderAdd:
                        ExecuteDistinctBuilderAdd(Register(instruction.XRegister), in Register(instruction.YRegister), in Register(instruction.AU), context.RuntimeBudget);
                        break;
                    case DistinctBuilderFinish:
                        ExecuteDistinctBuilderFinish(instruction.DestinationRegister, in Register(instruction.XRegister));
                        break;
                    case GroupBuilderCreate:
                        ExecuteGroupBuilderCreate(instruction.DestinationRegister);
                        break;
                    case GroupBuilderAdd:
                        ExecuteGroupBuilderAdd(Register(instruction.XRegister), in Register(instruction.YRegister), in Register(instruction.AU), context.RuntimeBudget);
                        break;
                    case GroupBuilderFinish:
                        ExecuteGroupBuilderFinish(instruction.DestinationRegister, in Register(instruction.XRegister));
                        break;
                    case OrderBuilderCreate:
                        ExecuteOrderBuilderCreate(instruction.DestinationRegister);
                        break;
                    case OrderBuilderAdd:
                        ExecuteOrderBuilderAdd(Register(instruction.XRegister), in Register(instruction.YRegister), in Register(instruction.AU), context.RuntimeBudget);
                        break;
                    case OrderBuilderFinishAscending:
                        ExecuteOrderBuilderFinishAscending(instruction.DestinationRegister, in Register(instruction.XRegister));
                        break;
                    case OrderBuilderFinishDescending:
                        ExecuteOrderBuilderFinishDescending(instruction.DestinationRegister, in Register(instruction.XRegister));
                        break;
                    case HasPattern:
                        ExecuteHasPattern(instruction.DestinationRegister, Register(instruction.XRegister), (GesBytecodePatternKind)instruction.AU, instruction.ImmediateY, Register(instruction.BU));
                        break;
                    case TakePattern:
                        ExecuteTakePattern(instruction.DestinationRegister, Register(instruction.XRegister), (GesBytecodePatternKind)instruction.AU, instruction.ImmediateY, Register(instruction.BU));
                        break;

                    #endregion

                    default:
                        RaiseError(GesDiagnosticCodes.RuntimeIllegalOpcode,
                            "Illegal opcode " + instruction.OpCode + ". Execution halted.");
                        break;
                }

                profiler?.PhaseStarting(GesProfilePhase.Advance);
                opcodesExecuted++;
            }
        }
        catch (GesFatalRuntimeException exception)
        {
            // A GesExtensionFaultException or GesCallbackFailureException
            // already carries its own TechnicalDetails; only fall back to the CLR type
            // name for internal fatal signals that never set it.
            RaiseError(exception.Diagnostic, exception.Diagnostic.TechnicalDetails ?? exception.GetType().Name);
        }
        catch (Exception exception)
        {
            RaiseError(
                GesDiagnosticCodes.RuntimeUnhandledFailure,
                "Unhandled VM execution failure.",
                GesRuntimeExceptionText.Describe(exception));
        }

        finally { profiler?.FinishSlice(); }

        context.RuntimeBudget.CompleteExecutionSlice(opcodesExecuted, reservedSteps, State == Processing, executionLimitDetail);
        return opcodesExecuted;
    }
}
