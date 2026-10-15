// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using GameEventScript.Api;
using GameEventScript.Runtime;
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
                        ModifyLocalRegisters(instruction.Count);
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
                        this.GesVmCreateSeries(instruction.DestinationRegister, (GesBytecodeSeriesKind)instruction.TypeOperand);
                        break;
                    case CallExternal:
                        this.GesVmCallExternal(instruction.DestinationRegister, instruction.BindId, instruction.ListIndex, context, instruction.HasInstructionFlag(GesInstructionFlag.NormalizeResultAsPredicate));
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
                        SetBoolean(instruction.DestinationRegister, this.Send(instruction, context));
                        break;
                    case EmitMessage:
                        this.GesVmPublishMessage(instruction.MessageDestination, Program.UInt16IndexLists.Resolve(instruction.ListIndex), false, context);
                        break;
                    case EmitMessageWithTags:
                        this.GesVmPublishMessageWithTags(instruction.MessageDestination, Program.UInt16IndexLists.Resolve(instruction.ListIndex),
                            Program.UInt16IndexLists.Resolve(instruction.SecondaryListIndex), false, context);
                        break;
                    case EmitMessageValue:
                        this.GesVmPublishMessageValue(in Register(instruction.XRegister), false, context);
                        break;
                    case EmitMessageValueWithTags:
                        this.GesVmPublishMessageValueWithTags(in Register(instruction.XRegister), Program.UInt16IndexLists.Resolve(instruction.ListIndex), false, context);
                        break;

                    case PublishMessage:
                        this.GesVmPublishMessage(instruction.MessageDestination, Program.UInt16IndexLists.Resolve(instruction.ListIndex), true, context);
                        break;
                    case PublishMessageWithTags:
                        this.GesVmPublishMessageWithTags(instruction.MessageDestination, Program.UInt16IndexLists.Resolve(instruction.ListIndex),
                            Program.UInt16IndexLists.Resolve(instruction.SecondaryListIndex), true, context);
                        break;
                    case PublishMessageValue:
                        this.GesVmPublishMessageValue(in Register(instruction.XRegister), true, context);
                        break;
                    case PublishMessageValueWithTags:
                        this.GesVmPublishMessageValueWithTags(in Register(instruction.XRegister), Program.UInt16IndexLists.Resolve(instruction.ListIndex), true, context);
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
                        this.GesVmCast(instruction.DestinationRegister, Register(instruction.XRegister), instruction.TypeKind, context);
                        break;
                    case CastCustom:
                        this.GesVmCastCustom(instruction.DestinationRegister, Register(instruction.XRegister), instruction.SecondaryStringIndex);
                        break;
                    case CastUnit:
                        this.GesVmCastUnit(instruction.DestinationRegister, Register(instruction.XRegister), instruction.Unit);
                        break;
                    case CastNumeric:
                        this.GesVmCastNumeric(instruction.DestinationRegister, Register(instruction.XRegister));
                        break;

                    case CheckType:
                        this.GesVmCheckType(instruction.DestinationRegister, Register(instruction.XRegister), instruction.TypeKind);
                        break;
                    case CheckCustomType:
                        this.GesVmCheckCustomType(instruction.DestinationRegister, Register(instruction.XRegister), instruction.SecondaryStringIndex);
                        break;
                    case CheckUnit:
                        this.GesVmCheckUnit(instruction.DestinationRegister, Register(instruction.XRegister), instruction.Unit);
                        break;
                    case CheckNumeric:
                        this.GesVmCheckNumeric(instruction.DestinationRegister, Register(instruction.XRegister));
                        break;
                    case CheckInteger:
                        this.GesVmCheckInteger(instruction.DestinationRegister, Register(instruction.XRegister));
                        break;
                    case CheckFractional:
                        this.GesVmCheckFractional(instruction.DestinationRegister, Register(instruction.XRegister));
                        break;

                    case Move:
                        if ((instruction.UnitAndFlags & 0x40) != 0) MoveNumeric(instruction.DestinationRegister, instruction.XRegister);
                        else SetValue(instruction.DestinationRegister, in Register(instruction.XRegister));
                        break;
                    case MemberAccess:
                        this.GesVmMemberAccess(instruction.DestinationRegister, FetchStringByPointer(instruction.StringIndex), Register(instruction.YRegister));
                        break;
                    case IndexAccess:
                        this.GesVmIndexAccess(instruction.DestinationRegister, instruction.Index, Register(instruction.YRegister));
                        break;
                    case PropertyAccess:
                        this.GesVmPropertyAccess(instruction.DestinationRegister, Register(instruction.XRegister), Register(instruction.YRegister));
                        break;
                    case BindHandler:
                        this.BindHandler(instruction.DestinationRegister, Register(instruction.XRegister), Program.UInt16IndexLists.Resolve(instruction.ListIndex));
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
                        SetTextPointer(instruction.DestinationRegister, instruction.StringIndex);
                        break;
                    case LoadTag:
                        SetTagPointer(instruction.DestinationRegister, instruction.StringIndex);
                        break;
                    case LoadHandler:
                        this.CreateMessageSignature(instruction.DestinationRegister, Program.UInt16IndexLists.Resolve(instruction.ListIndex));
                        break;
                    case LoadMessage:
                        this.CreateMessage(instruction.DestinationRegister, Program.UInt16IndexLists.Resolve(instruction.SecondaryListIndex), Program.UInt16IndexLists.Resolve(instruction.ListIndex));
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
                        this.GesVmCreateDice(instruction.DestinationRegister, instruction.Count, instruction.ImmediateY, context);
                        break;
                    case CreateVector:
                        this.GesVmCreateVector(instruction.DestinationRegister, instruction.ImmediateX);
                        ClearStage();
                        break;
                    case CreatePoint:
                        this.GesVmCreatePoint(instruction.DestinationRegister, instruction.ImmediateX);
                        ClearStage();
                        break;
                    case CreateList:
                        this.GesVmCreateList(instruction.DestinationRegister);
                        ClearStage();
                        break;
                    case CreateMap:
                        this.GesVmCreateMap(instruction.DestinationRegister, instruction.SecondaryListIndex);
                        ClearStage();
                        break;
                    case CreateRange:
                        this.GesVmCreateRange(instruction.DestinationRegister, Register(instruction.XRegister), Register(instruction.YRegister));
                        break;
                    case CreateRangeWithStep:
                        this.GesVmCreateRange(instruction.DestinationRegister, Register(instruction.XRegister), Register(instruction.YRegister), Register(instruction.AU));
                        break;
                    case CreateRangeIterator:
                        this.GesVmCreateRangeIterator(instruction.DestinationRegister, Register(instruction.XRegister), Register(instruction.YRegister), context);
                        break;
                    case CreateRangeIteratorWithStep:
                        this.GesVmCreateRangeIterator(instruction.DestinationRegister, Register(instruction.XRegister), Register(instruction.YRegister), Register(instruction.AU), context);
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
                        this.GesVmCreateRecordValue(instruction.DestinationRegister, Register(instruction.XRegister), instruction.SecondaryStringIndex);
                        break;
                    case CreateExternalType:
                        this.GesVmCreateExternalType(instruction.DestinationRegister, instruction.BindId, instruction.ListIndex);
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
                        this.GesVmOr(instruction.DestinationRegister, Register(instruction.XRegister), Register(instruction.YRegister));
                        break;
                    case And:
                        this.GesVmAnd(instruction.DestinationRegister, Register(instruction.XRegister), Register(instruction.YRegister));
                        break;
                    case Xor:
                        this.GesVmXor(instruction.DestinationRegister, Register(instruction.XRegister), Register(instruction.YRegister));
                        break;
                    case Implies:
                        this.GesVmImplies(instruction.DestinationRegister, Register(instruction.XRegister), Register(instruction.YRegister));
                        break;
                    case Not:
                        this.GesVmNot(instruction.DestinationRegister, Register(instruction.XRegister));
                        break;
                    case Equal:
                        this.GesVmEqual(instruction.DestinationRegister, in Register(instruction.XRegister), in Register(instruction.YRegister));
                        break;
                    case NotEqual:
                        this.GesVmNotEqual(instruction.DestinationRegister, in Register(instruction.XRegister), in Register(instruction.YRegister));
                        break;
                    case Less:
                        this.GesVmLess(instruction.DestinationRegister, in Register(instruction.XRegister), in Register(instruction.YRegister));
                        break;
                    case Greater:
                        this.GesVmGreater(instruction.DestinationRegister, in Register(instruction.XRegister), in Register(instruction.YRegister));
                        break;
                    case LessOrEqual:
                        this.GesVmLessOrEqual(instruction.DestinationRegister, in Register(instruction.XRegister), in Register(instruction.YRegister));
                        break;
                    case GreaterOrEqual:
                        this.GesVmGreaterOrEqual(instruction.DestinationRegister, in Register(instruction.XRegister), in Register(instruction.YRegister));
                        break;
                    case Add:
                        if ((instruction.UnitAndFlags & 0x40) != 0)
                            AddNumeric(instruction.DestinationRegister, instruction.XRegister, instruction.YRegister);
                        else this.AddRegisters(instruction.DestinationRegister, instruction.XRegister, instruction.YRegister);
                        break;
                    case Subtract:
                        if ((instruction.UnitAndFlags & 0x40) != 0) SubtractNumeric(instruction.DestinationRegister, instruction.XRegister, instruction.YRegister);
                        else this.GesVmSubtract(instruction.DestinationRegister, Register(instruction.XRegister), Register(instruction.YRegister));
                        break;
                    case Multiply:
                        if ((instruction.UnitAndFlags & 0x40) != 0) MultiplyNumeric(instruction.DestinationRegister, instruction.XRegister, instruction.YRegister);
                        else this.MultiplyRegisters(instruction.DestinationRegister, instruction.XRegister, instruction.YRegister);
                        break;
                    case Divide:
                        if ((instruction.UnitAndFlags & 0x40) != 0) DivideNumeric(instruction.DestinationRegister, instruction.XRegister, instruction.YRegister);
                        else this.GesVmDivide(instruction.DestinationRegister, Register(instruction.XRegister), Register(instruction.YRegister));
                        break;
                    case Power:
                        if ((instruction.UnitAndFlags & 0x40) != 0) PowerNumeric(instruction.DestinationRegister, instruction.XRegister, instruction.YRegister);
                        else this.GesVmPower(instruction.DestinationRegister, Register(instruction.XRegister), Register(instruction.YRegister));
                        break;
                    case IntegerDivide:
                        if ((instruction.UnitAndFlags & 0x40) != 0) FloorDivideNumeric(instruction.DestinationRegister, instruction.XRegister, instruction.YRegister);
                        else this.GesVmFloorDivide(instruction.DestinationRegister, Register(instruction.XRegister), Register(instruction.YRegister));
                        break;
                    case Modulo:
                        if ((instruction.UnitAndFlags & 0x40) != 0) ModuloNumeric(instruction.DestinationRegister, instruction.XRegister, instruction.YRegister);
                        else this.ModuloRegisters(instruction.DestinationRegister, instruction.XRegister, instruction.YRegister);
                        break;
                    case Remainder:
                        if ((instruction.UnitAndFlags & 0x40) != 0) RemainderNumeric(instruction.DestinationRegister, instruction.XRegister, instruction.YRegister);
                        else this.GesVmRemainder(instruction.DestinationRegister, Register(instruction.XRegister), Register(instruction.YRegister));
                        break;
                    case Min:
                        if ((instruction.UnitAndFlags & 0x40) != 0) MinNumeric(instruction.DestinationRegister, instruction.XRegister, instruction.YRegister);
                        else this.GesVmMin(instruction.DestinationRegister, Register(instruction.XRegister), Register(instruction.YRegister));
                        break;
                    case Max:
                        if ((instruction.UnitAndFlags & 0x40) != 0) MaxNumeric(instruction.DestinationRegister, instruction.XRegister, instruction.YRegister);
                        else this.GesVmMax(instruction.DestinationRegister, Register(instruction.XRegister), Register(instruction.YRegister));
                        break;
                    case Negate:
                        if ((instruction.UnitAndFlags & 0x40) != 0) NegateNumeric(instruction.DestinationRegister, instruction.XRegister);
                        else this.GesVmNegate(instruction.DestinationRegister, Register(instruction.XRegister));
                        break;
                    case Abs:
                        if ((instruction.UnitAndFlags & 0x40) != 0) AbsNumeric(instruction.DestinationRegister, instruction.XRegister);
                        else this.GesVmAbs(instruction.DestinationRegister, Register(instruction.XRegister));
                        break;
                    case LogN:
                        this.GesVmNaturalLog(instruction.DestinationRegister, Register(instruction.XRegister));
                        break;
                    case Chance:
                        this.GesVmChance(instruction.DestinationRegister, Register(instruction.XRegister));
                        break;
                    case Clamp:
                        if ((instruction.UnitAndFlags & 0x40) != 0) ClampNumeric(instruction.DestinationRegister, instruction.XRegister, instruction.YRegister, instruction.AU);
                        else this.GesVmClamp(instruction.DestinationRegister, Register(instruction.XRegister), Register(instruction.YRegister), Register(instruction.AU));
                        break;
                    case RandomTake:
                        this.GesVmRandom(instruction.DestinationRegister, Register(instruction.XRegister), Register(instruction.YRegister), RandomGenerator);
                        break;
                    case RandomTakeFloat:
                        this.GesVmRandomFloat(instruction.DestinationRegister, Register(instruction.XRegister), Register(instruction.YRegister), RandomGenerator);
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
                        this.GesVmTerm(instruction.DestinationRegister, Register(instruction.XRegister), Register(instruction.YRegister));
                        break;
                    case Exp:
                        this.GesVmExp(instruction.DestinationRegister, Register(instruction.XRegister));
                        break;
                    case Floor:
                        this.GesVmFloor(instruction.DestinationRegister, Register(instruction.XRegister));
                        break;
                    case Ceil:
                        this.GesVmCeil(instruction.DestinationRegister, Register(instruction.XRegister));
                        break;
                    case Truncate:
                        this.GesVmTruncate(instruction.DestinationRegister, Register(instruction.XRegister));
                        break;
                    case RoundHalfEven:
                        this.GesVmRoundHalfEven(instruction.DestinationRegister, Register(instruction.XRegister));
                        break;
                    case RoundHalfUp:
                        this.GesVmRoundHalfUp(instruction.DestinationRegister, Register(instruction.XRegister));
                        break;
                    case RoundHalfDown:
                        this.GesVmRoundHalfDown(instruction.DestinationRegister, Register(instruction.XRegister));
                        break;
                    case DegreeToRadians:
                        this.GesVmDegreeToRadians(instruction.DestinationRegister, Register(instruction.XRegister));
                        break;
                    case DegreeFromRadians:
                        this.GesVmDegreeFromRadians(instruction.DestinationRegister, Register(instruction.XRegister));
                        break;
                    case WrapDegree:
                        this.GesVmWrapDegree(instruction.DestinationRegister, Register(instruction.XRegister));
                        break;
                    case Sin:
                        this.GesVmSin(instruction.DestinationRegister, Register(instruction.XRegister));
                        break;
                    case Cos:
                        this.GesVmCos(instruction.DestinationRegister, Register(instruction.XRegister));
                        break;
                    case Tan:
                        this.GesVmTan(instruction.DestinationRegister, Register(instruction.XRegister));
                        break;
                    case Asin:
                        this.GesVmAsin(instruction.DestinationRegister, Register(instruction.XRegister));
                        break;
                    case Acos:
                        this.GesVmAcos(instruction.DestinationRegister, Register(instruction.XRegister));
                        break;
                    case Atan:
                        this.GesVmAtan(instruction.DestinationRegister, Register(instruction.XRegister));
                        break;
                    case Atan2:
                        this.GesVmAtan2(instruction.DestinationRegister, Register(instruction.XRegister), Register(instruction.YRegister));
                        break;
                    case Hypot2D:
                        this.GesVmHypot2D(instruction.DestinationRegister, Register(instruction.XRegister), Register(instruction.YRegister));
                        break;
                    case Hypot3D:
                        this.GesVmHypot3D(instruction.DestinationRegister, Register(instruction.XRegister), Register(instruction.YRegister), Register(instruction.AU));
                        break;
                    case Distance:
                        this.GesVmDistance(instruction.DestinationRegister, Register(instruction.XRegister), Register(instruction.YRegister));
                        break;
                    case Distance2D:
                        this.GesVmDistance2D(instruction.DestinationRegister, Register(instruction.XRegister), Register(instruction.YRegister), Register(instruction.AU), Register(instruction.BU));
                        break;
                    case Distance3D:
                        this.GesVmDistance3D(instruction.DestinationRegister, Register(instruction.XRegister), Register(instruction.YRegister), Register(instruction.AU), Register(instruction.BU),
                            Register(instruction.CU), Register(instruction.DU));
                        break;
                    case DistanceSquared:
                        this.GesVmDistanceSquared(instruction.DestinationRegister, Register(instruction.XRegister), Register(instruction.YRegister));
                        break;
                    case DistanceSquared2D:
                        this.GesVmDistanceSquared2D(instruction.DestinationRegister, Register(instruction.XRegister), Register(instruction.YRegister), Register(instruction.AU), Register(instruction.BU));
                        break;
                    case DistanceSquared3D:
                        this.GesVmDistanceSquared3D(instruction.DestinationRegister, Register(instruction.XRegister), Register(instruction.YRegister), Register(instruction.AU), Register(instruction.BU),
                            Register(instruction.CU), Register(instruction.DU));
                        break;
                    case LengthSquared:
                        this.GesVmLengthSquared(instruction.DestinationRegister, Register(instruction.XRegister));
                        break;
                    case LengthSquared2D:
                        this.GesVmLengthSquared2D(instruction.DestinationRegister, Register(instruction.XRegister), Register(instruction.YRegister));
                        break;
                    case LengthSquared3D:
                        this.GesVmLengthSquared3D(instruction.DestinationRegister, Register(instruction.XRegister), Register(instruction.YRegister), Register(instruction.AU));
                        break;
                    case Normalize:
                        this.GesVmNormalize(instruction.DestinationRegister, Register(instruction.XRegister));
                        break;
                    case Normalize2D:
                        this.GesVmNormalize2D(instruction.DestinationRegister, Register(instruction.XRegister), Register(instruction.YRegister));
                        break;
                    case Normalize3D:
                        this.GesVmNormalize3D(instruction.DestinationRegister, Register(instruction.XRegister), Register(instruction.YRegister), Register(instruction.AU));
                        break;
                    case Dot:
                        this.GesVmDot(instruction.DestinationRegister, Register(instruction.XRegister), Register(instruction.YRegister));
                        break;
                    case Dot2D:
                        this.GesVmDot2D(instruction.DestinationRegister, Register(instruction.XRegister), Register(instruction.YRegister), Register(instruction.AU), Register(instruction.BU));
                        break;
                    case Dot3D:
                        this.GesVmDot3D(instruction.DestinationRegister, Register(instruction.XRegister), Register(instruction.YRegister), Register(instruction.AU), Register(instruction.BU),
                            Register(instruction.CU), Register(instruction.DU));
                        break;
                    case Cross:
                        this.GesVmCross(instruction.DestinationRegister, Register(instruction.XRegister), Register(instruction.YRegister));
                        break;
                    case Cross2D:
                        this.GesVmCross2D(instruction.DestinationRegister, Register(instruction.XRegister), Register(instruction.YRegister), Register(instruction.AU), Register(instruction.BU));
                        break;
                    case Cross3D:
                        this.GesVmCross3D(instruction.DestinationRegister, Register(instruction.XRegister), Register(instruction.YRegister), Register(instruction.AU), Register(instruction.BU),
                            Register(instruction.CU), Register(instruction.DU));
                        break;
                    case AngleBetween:
                        this.GesVmAngleBetween(instruction.DestinationRegister, Register(instruction.XRegister), Register(instruction.YRegister));
                        break;
                    case AngleBetween2D:
                        this.GesVmAngleBetween2D(instruction.DestinationRegister, Register(instruction.XRegister), Register(instruction.YRegister), Register(instruction.AU), Register(instruction.BU));
                        break;
                    case AngleBetween3D:
                        this.GesVmAngleBetween3D(instruction.DestinationRegister, Register(instruction.XRegister), Register(instruction.YRegister), Register(instruction.AU), Register(instruction.BU),
                            Register(instruction.CU), Register(instruction.DU));
                        break;

                    #endregion

                    #region Group 3 - text, collection, iterators

                    case TakeFirst:
                        this.GesVmTakeFirst(instruction.DestinationRegister, Register(instruction.XRegister), instruction.ImmediateY);
                        break;
                    case DropFirst:
                        this.GesVmDropFirst(instruction.DestinationRegister, Register(instruction.XRegister), instruction.ImmediateY);
                        break;
                    case TakeLast:
                        this.GesVmTakeLast(instruction.DestinationRegister, Register(instruction.XRegister), instruction.ImmediateY);
                        break;
                    case DropLast:
                        this.GesVmDropLast(instruction.DestinationRegister, Register(instruction.XRegister), instruction.ImmediateY);
                        break;
                    case TakeHighest:
                        this.GesVmTakeHighest(instruction.DestinationRegister, Register(instruction.XRegister), instruction.ImmediateY);
                        break;
                    case TakeLowest:
                        this.GesVmTakeLowest(instruction.DestinationRegister, Register(instruction.XRegister), instruction.ImmediateY);
                        break;
                    case DropHighest:
                        this.GesVmDropHighest(instruction.DestinationRegister, Register(instruction.XRegister), instruction.ImmediateY);
                        break;
                    case DropLowest:
                        this.GesVmDropLowest(instruction.DestinationRegister, Register(instruction.XRegister), instruction.ImmediateY);
                        break;
                    case OneRandom:
                        this.GesVmOneRandom(instruction.DestinationRegister, Register(instruction.XRegister), RandomGenerator);
                        break;
                    case TakeRandom:
                        this.GesVmTakeRandom(instruction.DestinationRegister, Register(instruction.XRegister), instruction.ImmediateY, RandomGenerator);
                        break;
                    case OneWeighted:
                        this.GesVmOneWeighted(instruction.DestinationRegister, Register(instruction.XRegister), Register(instruction.YRegister), RandomGenerator);
                        break;
                    case TakeWeighted:
                        this.GesVmTakeWeighted(instruction.DestinationRegister, Register(instruction.XRegister), Register(instruction.AU), instruction.ImmediateY, RandomGenerator);
                        break;
                    case Count:
                        this.GesVmCount(instruction.DestinationRegister, Register(instruction.XRegister));
                        break;
                    case StartsWith:
                        this.GesVmStartsWith(instruction.DestinationRegister, Register(instruction.XRegister), Register(instruction.YRegister));
                        break;
                    case EndsWith:
                        this.GesVmEndsWith(instruction.DestinationRegister, Register(instruction.XRegister), Register(instruction.YRegister));
                        break;
                    case Contains:
                        this.GesVmContains(instruction.DestinationRegister, Register(instruction.XRegister), Register(instruction.YRegister));
                        break;
                    case ContainsAny:
                        this.GesVmContainsAnyAll(instruction.DestinationRegister, Register(instruction.XRegister), Register(instruction.YRegister), requireAll: false);
                        break;
                    case ContainsAll:
                        this.GesVmContainsAnyAll(instruction.DestinationRegister, Register(instruction.XRegister), Register(instruction.YRegister), requireAll: true);
                        break;
                    case ContainsValue:
                        this.GesVmContainsValue(instruction.DestinationRegister, Register(instruction.XRegister), Register(instruction.YRegister));
                        break;
                    case Union:
                        this.GesVmUnion(instruction.DestinationRegister, Register(instruction.XRegister), Register(instruction.YRegister));
                        break;
                    case Intersect:
                        this.GesVmIntersect(instruction.DestinationRegister, Register(instruction.XRegister), Register(instruction.YRegister));
                        break;
                    case Zip:
                        this.GesVmZip(instruction.DestinationRegister, Register(instruction.XRegister), Register(instruction.YRegister));
                        break;
                    case KeysOfMap:
                        this.GesVmKeys(instruction.DestinationRegister, Register(instruction.XRegister));
                        break;
                    case ValuesOfMap:
                        this.GesVmValues(instruction.DestinationRegister, Register(instruction.XRegister));
                        break;
                    case EntriesOfMap:
                        this.GesVmEntries(instruction.DestinationRegister, Register(instruction.XRegister));
                        break;
                    case GesBytecodeOpCode.First:
                        this.GesVmFirst(instruction.DestinationRegister, Register(instruction.XRegister));
                        break;
                    case GesBytecodeOpCode.Last:
                        this.GesVmLast(instruction.DestinationRegister, Register(instruction.XRegister));
                        break;
                    case GesBytecodeOpCode.Single:
                        this.GesVmSingle(instruction.DestinationRegister, Register(instruction.XRegister));
                        break;
                    case Cartesian:
                        this.GesVmCartesian(instruction.DestinationRegister, Register(instruction.XRegister), Register(instruction.YRegister), context.RuntimeBudget);
                        break;
                    case IteratorCreate:
                        this.GesVmIteratorCreate(instruction, context.RuntimeBudget);
                        break;
                    case IteratorCreateOrJump:
                        this.GesVmIteratorCreate(instruction, context.RuntimeBudget);
                        if (IsRegisterNothing(instruction.DestinationRegister)) JumpAddress(instruction.TargetAddress);
                        break;
                    case IteratorNext:
                    {
                        if (instruction.UnitAndFlags != 0 ? this.GesVmIteratorNextComponents(instruction) : this.GesVmIteratorNext(instruction.DestinationRegister, Register(instruction.XRegister), instruction.TargetAddress))
                        {
                            context.RuntimeBudget.ConsumeLoopIterationIfAvailable("For loop iteration exceeds the configured limit.");
                        }

                        break;
                    }
                    case IteratorClose:
                        this.GesVmIteratorClose(instruction.XRegister);
                        break;
                    case HasAny:
                        this.GesVmHasAnyAll(instruction.DestinationRegister, Register(instruction.XRegister), false);
                        break;
                    case HasAll:
                        this.GesVmHasAnyAll(instruction.DestinationRegister, Register(instruction.XRegister), true);
                        break;
                    case Distinct:
                        this.GesVmDistinct(instruction.DestinationRegister, Register(instruction.XRegister));
                        break;
                    case SortAscending:
                        this.GesVmSortAscending(instruction.DestinationRegister, Register(instruction.XRegister));
                        break;
                    case SortDescending:
                        this.GesVmSortDescending(instruction.DestinationRegister, Register(instruction.XRegister));
                        break;
                    case Reverse:
                        this.GesVmReverse(instruction.DestinationRegister, Register(instruction.XRegister));
                        break;
                    case Shuffle:
                        this.GesVmShuffle(instruction.DestinationRegister, Register(instruction.XRegister), RandomGenerator);
                        break;
                    case ListBuilderCreate:
                        this.GesVmCreateListBuilder(instruction.DestinationRegister);
                        break;
                    case ListBuilderAdd:
                        this.GesVmListBuilderAdd(Register(instruction.XRegister), in Register(instruction.YRegister), context.RuntimeBudget);
                        break;
                    case ListBuilderFinish:
                        this.GesVmListBuilderFinish(instruction.DestinationRegister, in Register(instruction.XRegister));
                        break;
                    case MapBuilderCreate:
                        this.GesVmCreateMapBuilder(instruction.DestinationRegister);
                        break;
                    case MapBuilderAdd:
                        this.GesVmMapBuilderAdd(Register(instruction.XRegister), in Register(instruction.YRegister), in Register(instruction.AU), context.RuntimeBudget);
                        break;
                    case MapBuilderFinish:
                        this.GesVmMapBuilderFinish(instruction.DestinationRegister, in Register(instruction.XRegister));
                        break;
                    case DistinctBuilderCreate:
                        this.GesVmCreateDistinctBuilder(instruction.DestinationRegister);
                        break;
                    case DistinctBuilderAdd:
                        this.GesVmDistinctBuilderAdd(Register(instruction.XRegister), in Register(instruction.YRegister), in Register(instruction.AU), context.RuntimeBudget);
                        break;
                    case DistinctBuilderFinish:
                        this.GesVmDistinctBuilderFinish(instruction.DestinationRegister, in Register(instruction.XRegister));
                        break;
                    case GroupBuilderCreate:
                        this.GesVmCreateGroupBuilder(instruction.DestinationRegister);
                        break;
                    case GroupBuilderAdd:
                        this.GesVmGroupBuilderAdd(Register(instruction.XRegister), in Register(instruction.YRegister), in Register(instruction.AU), context.RuntimeBudget);
                        break;
                    case GroupBuilderFinish:
                        this.GesVmGroupBuilderFinish(instruction.DestinationRegister, in Register(instruction.XRegister));
                        break;
                    case OrderBuilderCreate:
                        this.GesVmCreateOrderBuilder(instruction.DestinationRegister);
                        break;
                    case OrderBuilderAdd:
                        this.GesVmOrderBuilderAdd(Register(instruction.XRegister), in Register(instruction.YRegister), in Register(instruction.AU), context.RuntimeBudget);
                        break;
                    case OrderBuilderFinishAscending:
                        this.GesVmOrderBuilderFinishAscending(instruction.DestinationRegister, in Register(instruction.XRegister));
                        break;
                    case OrderBuilderFinishDescending:
                        this.GesVmOrderBuilderFinishDescending(instruction.DestinationRegister, in Register(instruction.XRegister));
                        break;
                    case HasPattern:
                        this.GesVmHasPattern(instruction.DestinationRegister, Register(instruction.XRegister), (GesBytecodePatternKind)instruction.AU, instruction.ImmediateY, Register(instruction.BU));
                        break;
                    case TakePattern:
                        this.GesVmTakePattern(instruction.DestinationRegister, Register(instruction.XRegister), (GesBytecodePatternKind)instruction.AU, instruction.ImmediateY, Register(instruction.BU));
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
