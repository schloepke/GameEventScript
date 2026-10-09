// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using GameEventScript.Api;

namespace GameEventScript.Compiler;

internal static partial class GesCompiler
{
    private sealed partial class BinaryCompiler
    {
        private static string? AssemblySpecialSignature(AssemblyLine line) => line.Name switch
        {
            "Add" or "Subtract" or "Multiply" or "Divide" or "Power" or "IntegerDivide" or "Modulo" or "Remainder" or "Min" or "Max" when line.Operands.Count == 4 => "wrrt",
            "Move" or "Negate" or "Abs" when line.Operands.Count == 3 => "wrt",
            "Clamp" when line.Operands.Count == 5 => "wrrrt",
            "LoadInteger" when line.Operands.Count == 3 => "wiq",
            "LoadFloat" when line.Operands.Count == 3 => "wfq",
            "Cast" or "CheckType" => "wry",
            "CastUnit" or "CheckUnit" => "wrq",
            "CreateSeries" => "wz",
            "RandomPush" => "r",
            "RandomPushConstant" => "i",
            "RandomPop" => "",
            "HasPattern" or "TakePattern" => line.Operands.Count == 5 ? "wrpsr" : "wrps",
            "SplitText" => line.Operands.Count == 2 ? "wr" : "wrr",
            _ => null
        };

        private static GameEventScriptBytecodeTypeKind AssemblyType(ExpressionNode operand)
        {
            var name = AssemblyText(operand);
            return name == "Number" ? GameEventScriptBytecodeTypeKind.Float : GetBytecodeTypeKind(name.ToLowerInvariant()) ?? throw AssemblyFailure("Unknown assembly type.", name);
        }

        private static GameEventScriptBytecodeInstructionUnit AssemblyUnit(ExpressionNode operand)
            => AssemblyText(operand) switch
            {
                "none" => GameEventScriptBytecodeInstructionUnit.UnitNone,
                "degree" => GameEventScriptBytecodeInstructionUnit.UnitDegree,
                "m" or "meter" => GameEventScriptBytecodeInstructionUnit.UnitMeter,
                "s" or "second" => GameEventScriptBytecodeInstructionUnit.UnitSecond,
                var name => throw AssemblyFailure("Unknown assembly unit.", name)
            };

        private static GameEventScriptBytecodeSeriesKind AssemblySeries(ExpressionNode operand)
            => AssemblyText(operand) switch
            {
                "fibonacci" => GameEventScriptBytecodeSeriesKind.Fibonacci,
                "factorial" => GameEventScriptBytecodeSeriesKind.Factorial,
                var name => throw AssemblyFailure("Unknown assembly series.", name)
            };

        private static GameEventScriptBytecodePatternKind AssemblyPattern(ExpressionNode operand)
            => AssemblyText(operand) switch
            {
                "countAny" => GameEventScriptBytecodePatternKind.CountAny,
                "countFace" => GameEventScriptBytecodePatternKind.CountFace,
                "fullHouse" => GameEventScriptBytecodePatternKind.FullHouse,
                "straight" => GameEventScriptBytecodePatternKind.Straight,
                var name => throw AssemblyFailure("Unknown assembly pattern.", name)
            };

        private bool EmitAssemblySpecial(AssemblyLine line, GesRegisterRef[] registers)
        {
            var args = line.Operands;
            switch (line.Name)
            {
                case "Add" or "Subtract" or "Multiply" or "Divide" or "Power" or "IntegerDivide" or "Modulo" or "Remainder" or "Min" or "Max" when args.Count == 4:
                    if (AssemblyText(args[3]) != "numeric") throw AssemblyFailure("Unknown arithmetic mode.");
                    _builder.NumericBinary(Enum.Parse<GameEventScriptBytecodeOpCode>(line.Name), registers[0], registers[1], registers[2]);
                    break;
                case "Move" or "Negate" or "Abs" when args.Count == 3:
                    if (AssemblyText(args[2]) != "numeric") throw AssemblyFailure("Unknown arithmetic mode.");
                    _builder.NumericUnary(Enum.Parse<GameEventScriptBytecodeOpCode>(line.Name), registers[0], registers[1]);
                    break;
                case "Clamp" when args.Count == 5:
                    if (AssemblyText(args[4]) != "numeric") throw AssemblyFailure("Unknown arithmetic mode.");
                    _builder.NumericClamp(registers[0], registers[1], registers[2], registers[3]);
                    break;
                case "LoadInteger" when args.Count == 3: _builder.LoadInteger(registers[0], AssemblyInteger(args[1], long.MinValue, long.MaxValue), AssemblyUnit(args[2])); break;
                case "LoadFloat" when args.Count == 3: _builder.LoadFloat(registers[0], AssemblyNumber(args[1]), AssemblyUnit(args[2])); break;
                case "Cast": _builder.Cast(registers[0], registers[1], AssemblyType(args[2])); break;
                case "CheckType": _builder.CheckType(registers[0], registers[1], AssemblyType(args[2])); break;
                case "CastUnit": _builder.CastUnit(registers[0], registers[1], AssemblyUnit(args[2])); break;
                case "CheckUnit": _builder.CheckUnit(registers[0], registers[1], AssemblyUnit(args[2])); break;
                case "CreateSeries": _builder.CreateSeries(registers[0], AssemblySeries(args[1])); break;
                case "RandomPush": _builder.RandomPush(registers[0]); break;
                case "RandomPushConstant": _builder.RandomPushConstant(AssemblyInteger(args[0], long.MinValue, long.MaxValue)); break;
                case "RandomPop": _builder.RandomPop(); break;
                case "HasPattern":
                case "TakePattern":
                    var pattern = AssemblyPattern(args[2]);
                    var count = (short)AssemblyInteger(args[3], 0, short.MaxValue);
                    if ((pattern == GameEventScriptBytecodePatternKind.CountFace) != (args.Count == 5)) throw AssemblyFailure("Only countFace takes a face operand.");
                    if (line.Name == "HasPattern") _builder.HasPattern(registers[0], registers[1], pattern, count, args.Count == 5 ? registers[4] : null);
                    else _builder.TakePattern(registers[0], registers[1], pattern, count, args.Count == 5 ? registers[4] : null);
                    break;
                case "SplitText":
                    _builder.AddOpcode(GameEventScriptBytecodeOpCode.SplitText, dst: GesOperand.Register(registers[0]), x: GesOperand.Register(registers[1]),
                        y: args.Count == 3 ? GesOperand.Register(registers[2]) : default, a: GesOperand.U16((ushort)(args.Count == 2 ? 1 : 0)));
                    break;
                default: return false;
            }
            return true;
        }
    }
}
