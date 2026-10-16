// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;

namespace GameEventScript.Compiler;

internal static partial class GesCompiler
{
    private sealed partial class BinaryCompiler
    {
        // Explicit source signatures; no numeric opcodes or pool addresses enter source.
        private static string AssemblySignature(string opcode) => opcode switch
        {
            "Nop" => "",
            "RandomTake" or "RandomTakeFloat" or "PropertyAccess" or "CreateRange" or "CreateRangeIterator" or "Default" or "Or" or "And" or "Xor" or "Implies" or "Equal" or "NotEqual" or "Less" or "Greater" or "LessOrEqual" or
                "GreaterOrEqual" or "Add" or "Subtract" or "Multiply" or "Cartesian" or "Divide" or "Power" or "IntegerDivide" or "Modulo" or "Remainder" or "Min" or "Max" or "Atan2" or "Hypot2D" or "Distance" or "DistanceSquared" or
                "LengthSquared2D" or "Normalize2D" or "Dot" or "Cross" or "AngleBetween" or "Term" or "OneWeighted" or "StartsWith" or "EndsWith" or "Contains" or "ContainsAny" or "ContainsAll" or "ContainsValue" or "Union" or
                "Intersect" or "Zip" => "wrr",
            "LoadInteger" => "wi",
            "LoadFloat" or "LoadPercentage" => "wf",
            "Jump" => "l",
            "JumpIfTrue" or "JumpIfFalse" or "JumpIfNotTrue" or "JumpIfNothing" => "rl",
            "CastCustom" or "CheckCustomType" => "wrt",
            "ParseLiteral" or "CastNumeric" or "CheckNumeric" or "CheckInteger" or "CheckFractional" or "Move" or "HasValue" or "IsEmpty" or "Not" or "Negate" or "Abs" or "LogN" or "Chance" or "Exp" or "Floor" or "Ceil" or "Truncate" or
                "RoundHalfEven" or "RoundHalfUp" or "RoundHalfDown" or "DegreeToRadians" or "DegreeFromRadians" or "WrapDegree" or "Sin" or "Cos" or "Tan" or "Asin" or "Acos" or "Atan" or "LengthSquared" or "Normalize" or "OneRandom" or
                "Count" or "HasAny" or "HasAll" or "KeysOfMap" or "ValuesOfMap" or "EntriesOfMap" or "First" or "Last" or "Single" or "IteratorCreate" or "Distinct" or "SortAscending" or "SortDescending" or "Reverse" or "Shuffle" or
                "ListBuilderFinish" or "MapBuilderFinish" or "DistinctBuilderFinish" or "GroupBuilderFinish" or "OrderBuilderFinishAscending" or "OrderBuilderFinishDescending" => "wr",
            "MemberAccess" => "wtr",
            "IndexAccess" => "wur",
            "LoadNothing" or "LoadTrue" or "LoadFalse" or "ListBuilderCreate" or "MapBuilderCreate" or "DistinctBuilderCreate" or "GroupBuilderCreate" or "OrderBuilderCreate" => "w",
            "LoadText" or "LoadTag" => "wt",
            "CreateDice" => "wss",
            "CreateRangeWithStep" or "CreateRangeIteratorWithStep" or "Hypot3D" or "LengthSquared3D" or "Normalize3D" or "Clamp" => "wrrr",
            "CreateRangeIteratorShort" => "wsss",
            "Distance2D" or "DistanceSquared2D" or "Dot2D" or "Cross2D" or "AngleBetween2D" => "wrrrr",
            "Distance3D" or "DistanceSquared3D" or "Dot3D" or "Cross3D" or "AngleBetween3D" => "wrrrrrr",
            "TakeFirst" or "DropFirst" or "TakeLast" or "DropLast" or "TakeHighest" or "TakeLowest" or "DropHighest" or "DropLowest" or "TakeRandom" => "wrs",
            "TakeWeighted" => "wrrs",
            "IteratorCreateOrJump" or "IteratorNext" => "wrl",
            "IteratorClose" => "r",
            "ListBuilderAdd" => "rr",
            "MapBuilderAdd" or "DistinctBuilderAdd" or "GroupBuilderAdd" or "OrderBuilderAdd" => "rrr",
            _ => throw CompileFailure("compile.invalidAssembly", $"Unsupported assembly instruction '{opcode}'.", opcode)
        };

        private void EmitAssemblyInstruction(GesAssemblyLine line, GesRegisterRef[] registers, IReadOnlyDictionary<string, GesLabelRef> labels)
        {
            var operands = line.Operands;
            switch (line.Name)
            {
                case "LoadInteger": _builder.LoadInteger(registers[0], AssemblyInteger(operands[1], long.MinValue, long.MaxValue)); break;
                case "LoadFloat": _builder.LoadFloat(registers[0], AssemblyNumber(operands[1])); break;
                case "RandomTake": _builder.RandomTake(registers[0], registers[1], registers[2]); break;
                case "RandomTakeFloat": _builder.RandomTakeFloat(registers[0], registers[1], registers[2]); break;
                case "Nop": _builder.Nop(); break;
                case "Jump": _builder.Jump(labels[AssemblyName(operands[0])]); break;
                case "JumpIfTrue": _builder.JumpIfTrue(registers[0], labels[AssemblyName(operands[1])]); break;
                case "JumpIfFalse": _builder.JumpIfFalse(registers[0], labels[AssemblyName(operands[1])]); break;
                case "JumpIfNotTrue": _builder.JumpIfNotTrue(registers[0], labels[AssemblyName(operands[1])]); break;
                case "JumpIfNothing": _builder.JumpIfNothing(registers[0], labels[AssemblyName(operands[1])]); break;
                case "CastCustom": _builder.CastCustom(registers[0], registers[1], AssemblyText(operands[2])); break;
                case "ParseLiteral": _builder.ParseLiteral(registers[0], registers[1]); break;
                case "CastNumeric": _builder.CastNumeric(registers[0], registers[1]); break;
                case "CheckCustomType": _builder.CheckCustomType(registers[0], registers[1], AssemblyText(operands[2])); break;
                case "CheckNumeric": _builder.CheckNumeric(registers[0], registers[1]); break;
                case "CheckInteger": _builder.CheckInteger(registers[0], registers[1]); break;
                case "CheckFractional": _builder.CheckFractional(registers[0], registers[1]); break;
                case "Move": _builder.Move(registers[0], registers[1]); break;
                case "MemberAccess": _builder.MemberAccess(registers[0], AssemblyText(operands[1]), registers[2]); break;
                case "IndexAccess": _builder.IndexAccess(registers[0], (ushort)AssemblyInteger(operands[1], ushort.MinValue, ushort.MaxValue), registers[2]); break;
                case "PropertyAccess": _builder.PropertyAccess(registers[0], registers[1], registers[2]); break;
                case "LoadNothing": _builder.LoadNothing(registers[0]); break;
                case "LoadTrue": _builder.LoadTrue(registers[0]); break;
                case "LoadFalse": _builder.LoadFalse(registers[0]); break;
                case "LoadPercentage": _builder.LoadPercentage(registers[0], AssemblyNumber(operands[1])); break;
                case "LoadText": _builder.LoadText(registers[0], AssemblyText(operands[1])); break;
                case "LoadTag": _builder.LoadTag(registers[0], AssemblyText(operands[1])); break;
                case "CreateDice": _builder.CreateDice(registers[0], (short)AssemblyInteger(operands[1], short.MinValue, short.MaxValue), (short)AssemblyInteger(operands[2], short.MinValue, short.MaxValue)); break;
                case "CreateRange": _builder.CreateRange(registers[0], registers[1], registers[2]); break;
                case "CreateRangeWithStep": _builder.CreateRangeWithStep(registers[0], registers[1], registers[2], registers[3]); break;
                case "CreateRangeIterator": _builder.CreateRangeIterator(registers[0], registers[1], registers[2]); break;
                case "CreateRangeIteratorWithStep": _builder.CreateRangeIteratorWithStep(registers[0], registers[1], registers[2], registers[3]); break;
                case "CreateRangeIteratorShort":
                    _builder.CreateRangeIteratorShort(
                        registers[0],
                        (short)AssemblyInteger(operands[1], short.MinValue, short.MaxValue),
                        (short)AssemblyInteger(operands[2], short.MinValue, short.MaxValue),
                        (short)AssemblyInteger(operands[3], short.MinValue, short.MaxValue)
                    );
                    break;
                case "HasValue": _builder.HasValue(registers[0], registers[1]); break;
                case "IsEmpty": _builder.IsEmpty(registers[0], registers[1]); break;
                case "Default": _builder.Default(registers[0], registers[1], registers[2]); break;
                case "Or": _builder.Or(registers[0], registers[1], registers[2]); break;
                case "And": _builder.And(registers[0], registers[1], registers[2]); break;
                case "Xor": _builder.Xor(registers[0], registers[1], registers[2]); break;
                case "Implies": _builder.Implies(registers[0], registers[1], registers[2]); break;
                case "Not": _builder.Not(registers[0], registers[1]); break;
                case "Equal": _builder.Equal(registers[0], registers[1], registers[2]); break;
                case "NotEqual": _builder.NotEqual(registers[0], registers[1], registers[2]); break;
                case "Less": _builder.Less(registers[0], registers[1], registers[2]); break;
                case "Greater": _builder.Greater(registers[0], registers[1], registers[2]); break;
                case "LessOrEqual": _builder.LessOrEqual(registers[0], registers[1], registers[2]); break;
                case "GreaterOrEqual": _builder.GreaterOrEqual(registers[0], registers[1], registers[2]); break;
                case "Add": _builder.Add(registers[0], registers[1], registers[2]); break;
                case "Subtract": _builder.Subtract(registers[0], registers[1], registers[2]); break;
                case "Cartesian": _builder.Cartesian(registers[0], registers[1], registers[2]); break;
                case "Multiply": _builder.Multiply(registers[0], registers[1], registers[2]); break;
                case "Divide": _builder.Divide(registers[0], registers[1], registers[2]); break;
                case "Power": _builder.Power(registers[0], registers[1], registers[2]); break;
                case "IntegerDivide": _builder.IntegerDivide(registers[0], registers[1], registers[2]); break;
                case "Modulo": _builder.Modulo(registers[0], registers[1], registers[2]); break;
                case "Remainder": _builder.Remainder(registers[0], registers[1], registers[2]); break;
                case "Min": _builder.Min(registers[0], registers[1], registers[2]); break;
                case "Max": _builder.Max(registers[0], registers[1], registers[2]); break;
                case "Negate": _builder.Negate(registers[0], registers[1]); break;
                case "Abs": _builder.Abs(registers[0], registers[1]); break;
                case "LogN": _builder.LogN(registers[0], registers[1]); break;
                case "Chance": _builder.Chance(registers[0], registers[1]); break;
                case "Exp": _builder.Exp(registers[0], registers[1]); break;
                case "Floor": _builder.Floor(registers[0], registers[1]); break;
                case "Ceil": _builder.Ceil(registers[0], registers[1]); break;
                case "Truncate": _builder.Truncate(registers[0], registers[1]); break;
                case "RoundHalfEven": _builder.RoundHalfEven(registers[0], registers[1]); break;
                case "RoundHalfUp": _builder.RoundHalfUp(registers[0], registers[1]); break;
                case "RoundHalfDown": _builder.RoundHalfDown(registers[0], registers[1]); break;
                case "DegreeToRadians": _builder.DegreeToRadians(registers[0], registers[1]); break;
                case "DegreeFromRadians": _builder.DegreeFromRadians(registers[0], registers[1]); break;
                case "WrapDegree": _builder.WrapDegree(registers[0], registers[1]); break;
                case "Sin": _builder.Sin(registers[0], registers[1]); break;
                case "Cos": _builder.Cos(registers[0], registers[1]); break;
                case "Tan": _builder.Tan(registers[0], registers[1]); break;
                case "Asin": _builder.Asin(registers[0], registers[1]); break;
                case "Acos": _builder.Acos(registers[0], registers[1]); break;
                case "Atan": _builder.Atan(registers[0], registers[1]); break;
                case "Atan2": _builder.Atan2(registers[0], registers[1], registers[2]); break;
                case "Hypot2D": _builder.Hypot2D(registers[0], registers[1], registers[2]); break;
                case "Hypot3D": _builder.Hypot3D(registers[0], registers[1], registers[2], registers[3]); break;
                case "Distance": _builder.Distance(registers[0], registers[1], registers[2]); break;
                case "Distance2D": _builder.Distance2D(registers[0], registers[1], registers[2], registers[3], registers[4]); break;
                case "Distance3D": _builder.Distance3D(registers[0], registers[1], registers[2], registers[3], registers[4], registers[5], registers[6]); break;
                case "DistanceSquared": _builder.DistanceSquared(registers[0], registers[1], registers[2]); break;
                case "DistanceSquared2D": _builder.DistanceSquared2D(registers[0], registers[1], registers[2], registers[3], registers[4]); break;
                case "DistanceSquared3D": _builder.DistanceSquared3D(registers[0], registers[1], registers[2], registers[3], registers[4], registers[5], registers[6]); break;
                case "LengthSquared": _builder.LengthSquared(registers[0], registers[1]); break;
                case "LengthSquared2D": _builder.LengthSquared2D(registers[0], registers[1], registers[2]); break;
                case "LengthSquared3D": _builder.LengthSquared3D(registers[0], registers[1], registers[2], registers[3]); break;
                case "Normalize": _builder.Normalize(registers[0], registers[1]); break;
                case "Normalize2D": _builder.Normalize2D(registers[0], registers[1], registers[2]); break;
                case "Normalize3D": _builder.Normalize3D(registers[0], registers[1], registers[2], registers[3]); break;
                case "Dot": _builder.Dot(registers[0], registers[1], registers[2]); break;
                case "Dot2D": _builder.Dot2D(registers[0], registers[1], registers[2], registers[3], registers[4]); break;
                case "Dot3D": _builder.Dot3D(registers[0], registers[1], registers[2], registers[3], registers[4], registers[5], registers[6]); break;
                case "Cross": _builder.Cross(registers[0], registers[1], registers[2]); break;
                case "Cross2D": _builder.Cross2D(registers[0], registers[1], registers[2], registers[3], registers[4]); break;
                case "Cross3D": _builder.Cross3D(registers[0], registers[1], registers[2], registers[3], registers[4], registers[5], registers[6]); break;
                case "AngleBetween": _builder.AngleBetween(registers[0], registers[1], registers[2]); break;
                case "AngleBetween2D": _builder.AngleBetween2D(registers[0], registers[1], registers[2], registers[3], registers[4]); break;
                case "AngleBetween3D": _builder.AngleBetween3D(registers[0], registers[1], registers[2], registers[3], registers[4], registers[5], registers[6]); break;
                case "Clamp": _builder.Clamp(registers[0], registers[1], registers[2], registers[3]); break;
                case "Term": _builder.Term(registers[0], registers[1], registers[2]); break;
                case "TakeFirst": _builder.TakeFirst(registers[0], registers[1], (short)AssemblyInteger(operands[2], short.MinValue, short.MaxValue)); break;
                case "DropFirst": _builder.DropFirst(registers[0], registers[1], (short)AssemblyInteger(operands[2], short.MinValue, short.MaxValue)); break;
                case "TakeLast": _builder.TakeLast(registers[0], registers[1], (short)AssemblyInteger(operands[2], short.MinValue, short.MaxValue)); break;
                case "DropLast": _builder.DropLast(registers[0], registers[1], (short)AssemblyInteger(operands[2], short.MinValue, short.MaxValue)); break;
                case "TakeHighest": _builder.TakeHighest(registers[0], registers[1], (short)AssemblyInteger(operands[2], short.MinValue, short.MaxValue)); break;
                case "TakeLowest": _builder.TakeLowest(registers[0], registers[1], (short)AssemblyInteger(operands[2], short.MinValue, short.MaxValue)); break;
                case "DropHighest": _builder.DropHighest(registers[0], registers[1], (short)AssemblyInteger(operands[2], short.MinValue, short.MaxValue)); break;
                case "DropLowest": _builder.DropLowest(registers[0], registers[1], (short)AssemblyInteger(operands[2], short.MinValue, short.MaxValue)); break;
                case "OneRandom": _builder.OneRandom(registers[0], registers[1]); break;
                case "TakeRandom": _builder.TakeRandom(registers[0], registers[1], (short)AssemblyInteger(operands[2], short.MinValue, short.MaxValue)); break;
                case "OneWeighted": _builder.OneWeighted(registers[0], registers[1], registers[2]); break;
                case "TakeWeighted": _builder.TakeWeighted(registers[0], registers[1], registers[2], (short)AssemblyInteger(operands[3], short.MinValue, short.MaxValue)); break;
                case "Count": _builder.Count(registers[0], registers[1]); break;
                case "StartsWith": _builder.StartsWith(registers[0], registers[1], registers[2]); break;
                case "EndsWith": _builder.EndsWith(registers[0], registers[1], registers[2]); break;
                case "Contains": _builder.Contains(registers[0], registers[1], registers[2]); break;
                case "ContainsAny": _builder.ContainsAny(registers[0], registers[1], registers[2]); break;
                case "ContainsAll": _builder.ContainsAll(registers[0], registers[1], registers[2]); break;
                case "HasAny": _builder.HasAny(registers[0], registers[1]); break;
                case "HasAll": _builder.HasAll(registers[0], registers[1]); break;
                case "ContainsValue": _builder.ContainsValue(registers[0], registers[1], registers[2]); break;
                case "Union": _builder.Union(registers[0], registers[1], registers[2]); break;
                case "Intersect": _builder.Intersect(registers[0], registers[1], registers[2]); break;
                case "Zip": _builder.Zip(registers[0], registers[1], registers[2]); break;
                case "KeysOfMap": _builder.KeysOfMap(registers[0], registers[1]); break;
                case "ValuesOfMap": _builder.ValuesOfMap(registers[0], registers[1]); break;
                case "EntriesOfMap": _builder.EntriesOfMap(registers[0], registers[1]); break;
                case "First": _builder.First(registers[0], registers[1]); break;
                case "Last": _builder.Last(registers[0], registers[1]); break;
                case "Single": _builder.Single(registers[0], registers[1]); break;
                case "IteratorCreate": _builder.IteratorCreate(registers[0], registers[1]); break;
                case "IteratorCreateOrJump": _builder.IteratorCreateOrJump(registers[0], registers[1], labels[AssemblyName(operands[2])]); break;
                case "IteratorNext": _builder.IteratorNext(registers[0], registers[1], labels[AssemblyName(operands[2])]); break;
                case "IteratorClose": _builder.IteratorClose(registers[0]); break;
                case "Distinct": _builder.Distinct(registers[0], registers[1]); break;
                case "SortAscending": _builder.SortAscending(registers[0], registers[1]); break;
                case "SortDescending": _builder.SortDescending(registers[0], registers[1]); break;
                case "Reverse": _builder.Reverse(registers[0], registers[1]); break;
                case "Shuffle": _builder.Shuffle(registers[0], registers[1]); break;
                case "ListBuilderCreate": _builder.ListBuilderCreate(registers[0]); break;
                case "ListBuilderAdd": _builder.ListBuilderAdd(registers[0], registers[1]); break;
                case "ListBuilderFinish": _builder.ListBuilderFinish(registers[0], registers[1]); break;
                case "MapBuilderCreate": _builder.MapBuilderCreate(registers[0]); break;
                case "MapBuilderAdd": _builder.MapBuilderAdd(registers[0], registers[1], registers[2]); break;
                case "MapBuilderFinish": _builder.MapBuilderFinish(registers[0], registers[1]); break;
                case "DistinctBuilderCreate": _builder.DistinctBuilderCreate(registers[0]); break;
                case "DistinctBuilderAdd": _builder.DistinctBuilderAdd(registers[0], registers[1], registers[2]); break;
                case "DistinctBuilderFinish": _builder.DistinctBuilderFinish(registers[0], registers[1]); break;
                case "GroupBuilderCreate": _builder.GroupBuilderCreate(registers[0]); break;
                case "GroupBuilderAdd": _builder.GroupBuilderAdd(registers[0], registers[1], registers[2]); break;
                case "GroupBuilderFinish": _builder.GroupBuilderFinish(registers[0], registers[1]); break;
                case "OrderBuilderCreate": _builder.OrderBuilderCreate(registers[0]); break;
                case "OrderBuilderAdd": _builder.OrderBuilderAdd(registers[0], registers[1], registers[2]); break;
                case "OrderBuilderFinishAscending": _builder.OrderBuilderFinishAscending(registers[0], registers[1]); break;
                case "OrderBuilderFinishDescending": _builder.OrderBuilderFinishDescending(registers[0], registers[1]); break;
            }
        }
    }
}
