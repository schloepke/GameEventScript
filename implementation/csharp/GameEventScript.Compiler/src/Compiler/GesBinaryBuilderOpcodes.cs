// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using GameEventScript.Api;

namespace GameEventScript.Compiler;

[SuppressMessage("ReSharper", "UnusedMember.Global")]
[SuppressMessage("ReSharper", "UnusedMethodReturnValue.Global")]
[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
internal sealed partial class GesBinaryBuilder
{
    public GesBinaryBuilder Nop() => AddOpcode(GesBytecodeOpCode.Nop);

    public GesBinaryBuilder Jump(GesLabelRef target) => AddOpcode(GesBytecodeOpCode.Jump, y: GesOperand.Label(target));

    public GesBinaryBuilder JumpIfTrue(GesRegisterRef condition, GesLabelRef target) => AddOpcode(GesBytecodeOpCode.JumpIfTrue, x: GesOperand.Register(condition), y: GesOperand.Label(target));

    public GesBinaryBuilder JumpIfFalse(GesRegisterRef condition, GesLabelRef target) => AddOpcode(GesBytecodeOpCode.JumpIfFalse, x: GesOperand.Register(condition), y: GesOperand.Label(target));

    public GesBinaryBuilder JumpIfNotTrue(GesRegisterRef condition, GesLabelRef target) => AddOpcode(GesBytecodeOpCode.JumpIfNotTrue, x: GesOperand.Register(condition), y: GesOperand.Label(target));

    public GesBinaryBuilder JumpIfNothing(GesRegisterRef condition, GesLabelRef target) => AddOpcode(GesBytecodeOpCode.JumpIfNothing, x: GesOperand.Register(condition), y: GesOperand.Label(target));

    public GesBinaryBuilder Call(GesRegisterRef destination, GesLabelRef entryAddress, GesInstructionFlag flags = GesInstructionFlag.None)
        => AddOpcode(GesBytecodeOpCode.Call, flags: flags, dst: GesOperand.Register(destination), y: GesOperand.Label(entryAddress));

    public GesBinaryBuilder CreateSeries(GesRegisterRef destination, GesBytecodeSeriesKind seriesKind) => AddOpcode(GesBytecodeOpCode.CreateSeries, dst: GesOperand.Register(destination), y: GesOperand.U16((ushort)seriesKind));

    public GesBinaryBuilder CallExternal(GesRegisterRef destination, GesBindRef bind, IReadOnlyList<GesRegisterRef> arguments, GesInstructionFlag flags = GesInstructionFlag.None)
        => AddOpcode(GesBytecodeOpCode.CallExternal, flags: flags, dst: GesOperand.Register(destination), x: GesOperand.Bind(bind), y: GesOperand.RegisterList(arguments));

    public GesBinaryBuilder ReturnVoid() => AddOpcode(GesBytecodeOpCode.ReturnVoid);

    public GesBinaryBuilder ReturnValue(GesRegisterRef value) => AddOpcode(GesBytecodeOpCode.ReturnValue, x: GesOperand.Register(value));

    public GesBinaryBuilder EmitMessage(GesBindRef outboundMessage, IReadOnlyList<GesRegisterRef> arguments) => AddOpcode(GesBytecodeOpCode.EmitMessage, dst: GesOperand.Bind(outboundMessage), y: GesOperand.RegisterList(arguments));

    public GesBinaryBuilder EmitMessageWithTags(GesBindRef outboundMessage, IReadOnlyList<GesRegisterRef> arguments, IReadOnlyList<GesRegisterRef> tags)
        => AddOpcode(GesBytecodeOpCode.EmitMessageWithTags, dst: GesOperand.Bind(outboundMessage), y: GesOperand.RegisterList(arguments), secondaryList: GesOperand.RegisterList(tags));

    public GesBinaryBuilder EmitMessageValue(GesRegisterRef message) => AddOpcode(GesBytecodeOpCode.EmitMessageValue, x: GesOperand.Register(message));

    public GesBinaryBuilder EmitMessageValueWithTags(GesRegisterRef message, IReadOnlyList<GesRegisterRef> tags) => AddOpcode(GesBytecodeOpCode.EmitMessageValueWithTags, x: GesOperand.Register(message), y: GesOperand.RegisterList(tags));

    public GesBinaryBuilder PublishMessage(GesBindRef outboundMessage, IReadOnlyList<GesRegisterRef> arguments) => AddOpcode(GesBytecodeOpCode.PublishMessage, dst: GesOperand.Bind(outboundMessage), y: GesOperand.RegisterList(arguments));

    public GesBinaryBuilder PublishMessageWithTags(GesBindRef outboundMessage, IReadOnlyList<GesRegisterRef> arguments, IReadOnlyList<GesRegisterRef> tags)
        => AddOpcode(GesBytecodeOpCode.PublishMessageWithTags, dst: GesOperand.Bind(outboundMessage), y: GesOperand.RegisterList(arguments), secondaryList: GesOperand.RegisterList(tags));

    public GesBinaryBuilder PublishMessageValue(GesRegisterRef message) => AddOpcode(GesBytecodeOpCode.PublishMessageValue, x: GesOperand.Register(message));

    public GesBinaryBuilder PublishMessageValueWithTags(GesRegisterRef message, IReadOnlyList<GesRegisterRef> tags) => AddOpcode(GesBytecodeOpCode.PublishMessageValueWithTags, x: GesOperand.Register(message), y: GesOperand.RegisterList(tags));

    public GesBinaryBuilder Cast(GesRegisterRef destination, GesRegisterRef source, GesBytecodeTypeKind type) => AddOpcode(GesBytecodeOpCode.Cast, dst: GesOperand.Register(destination), x: GesOperand.Register(source), y: GesOperand.Type(type));

    public GesBinaryBuilder CastCustom(GesRegisterRef destination, GesRegisterRef source, string typeName)
        => AddOpcode(GesBytecodeOpCode.CastCustom, dst: GesOperand.Register(destination), x: GesOperand.Register(source), y: GesOperand.Text(typeName));

    public GesBinaryBuilder CastUnit(GesRegisterRef destination, GesRegisterRef source, GesQuantityUnit unit) => AddOpcode(GesBytecodeOpCode.CastUnit, unit, dst: GesOperand.Register(destination), x: GesOperand.Register(source));

    public GesBinaryBuilder ParseLiteral(GesRegisterRef destination, GesRegisterRef source, bool strict = false)
        => AddOpcode(GesBytecodeOpCode.ParseLiteral, flags: strict ? GesInstructionFlag.Strict : GesInstructionFlag.None, dst: GesOperand.Register(destination), x: GesOperand.Register(source));

    public GesBinaryBuilder CastNumeric(GesRegisterRef destination, GesRegisterRef source) => AddOpcode(GesBytecodeOpCode.CastNumeric, dst: GesOperand.Register(destination), x: GesOperand.Register(source));

    public GesBinaryBuilder CheckType(GesRegisterRef destination, GesRegisterRef source, GesBytecodeTypeKind type)
        => AddOpcode(GesBytecodeOpCode.CheckType, dst: GesOperand.Register(destination), x: GesOperand.Register(source), y: GesOperand.Type(type));

    public GesBinaryBuilder CheckCustomType(GesRegisterRef destination, GesRegisterRef source, string typeName)
        => AddOpcode(GesBytecodeOpCode.CheckCustomType, dst: GesOperand.Register(destination), x: GesOperand.Register(source), y: GesOperand.Text(typeName));

    public GesBinaryBuilder CheckUnit(GesRegisterRef destination, GesRegisterRef source, GesQuantityUnit unit) => AddOpcode(GesBytecodeOpCode.CheckUnit, unit, dst: GesOperand.Register(destination), x: GesOperand.Register(source));

    public GesBinaryBuilder CheckNumeric(GesRegisterRef destination, GesRegisterRef source) => AddOpcode(GesBytecodeOpCode.CheckNumeric, dst: GesOperand.Register(destination), x: GesOperand.Register(source));

    public GesBinaryBuilder CheckInteger(GesRegisterRef destination, GesRegisterRef source) => AddOpcode(GesBytecodeOpCode.CheckInteger, dst: GesOperand.Register(destination), x: GesOperand.Register(source));

    public GesBinaryBuilder CheckFractional(GesRegisterRef destination, GesRegisterRef source) => AddOpcode(GesBytecodeOpCode.CheckFractional, dst: GesOperand.Register(destination), x: GesOperand.Register(source));

    public GesBinaryBuilder Move(GesRegisterRef destination, GesRegisterRef source) => AddOpcode(GesBytecodeOpCode.Move, dst: GesOperand.Register(destination), x: GesOperand.Register(source));

    public GesBinaryBuilder MemberAccess(GesRegisterRef destination, string memberName, GesRegisterRef target)
        => AddOpcode(GesBytecodeOpCode.MemberAccess, dst: GesOperand.Register(destination), x: GesOperand.Text(memberName), y: GesOperand.Register(target));

    public GesBinaryBuilder IndexAccess(GesRegisterRef destination, ushort index, GesRegisterRef target) => AddOpcode(GesBytecodeOpCode.IndexAccess, dst: GesOperand.Register(destination), x: GesOperand.U16(index), y: GesOperand.Register(target));

    public GesBinaryBuilder PropertyAccess(GesRegisterRef destination, GesRegisterRef property, GesRegisterRef target)
        => AddOpcode(GesBytecodeOpCode.PropertyAccess, dst: GesOperand.Register(destination), x: GesOperand.Register(property), y: GesOperand.Register(target));

    public GesBinaryBuilder BindHandler(GesRegisterRef destination, GesRegisterRef handler, IReadOnlyList<GesRegisterRef> arguments)
        => AddOpcode(GesBytecodeOpCode.BindHandler, dst: GesOperand.Register(destination), x: GesOperand.Register(handler), y: GesOperand.RegisterList(arguments));

    public GesBinaryBuilder LoadNothing(GesRegisterRef destination) => AddOpcode(GesBytecodeOpCode.LoadNothing, dst: GesOperand.Register(destination));

    public GesBinaryBuilder LoadTrue(GesRegisterRef destination) => AddOpcode(GesBytecodeOpCode.LoadTrue, dst: GesOperand.Register(destination));

    public GesBinaryBuilder LoadFalse(GesRegisterRef destination) => AddOpcode(GesBytecodeOpCode.LoadFalse, dst: GesOperand.Register(destination));

    public GesBinaryBuilder LoadInteger(GesRegisterRef destination, long value, GesQuantityUnit unit = GesQuantityUnit.UnitNone) => AddOpcode(GesBytecodeOpCode.LoadInteger, unit, dst: GesOperand.Register(destination), i64: value);

    public GesBinaryBuilder LoadFloat(GesRegisterRef destination, double value, GesQuantityUnit unit = GesQuantityUnit.UnitNone) => AddOpcode(GesBytecodeOpCode.LoadFloat, unit, dst: GesOperand.Register(destination), f64: value);

    public GesBinaryBuilder LoadPercentage(GesRegisterRef destination, double ratio) => AddOpcode(GesBytecodeOpCode.LoadPercentage, dst: GesOperand.Register(destination), f64: ratio);

    public GesBinaryBuilder LoadText(GesRegisterRef destination, string text) => AddOpcode(GesBytecodeOpCode.LoadText, dst: GesOperand.Register(destination), x: GesOperand.Text(text));

    public GesBinaryBuilder LoadTag(GesRegisterRef destination, string tag) => AddOpcode(GesBytecodeOpCode.LoadTag, dst: GesOperand.Register(destination), x: GesOperand.Text(tag));

    public GesBinaryBuilder LoadHandler(GesRegisterRef destination, IReadOnlyList<string> shape) => AddOpcode(GesBytecodeOpCode.LoadHandler, dst: GesOperand.Register(destination), y: GesOperand.TextList(shape));

    public GesBinaryBuilder LoadMessage(GesRegisterRef destination, IReadOnlyList<string> shape, IReadOnlyList<GesRegisterRef> arguments)
        => AddOpcode(GesBytecodeOpCode.LoadMessage, dst: GesOperand.Register(destination), x: GesOperand.TextList(shape), y: GesOperand.RegisterList(arguments));

    public GesBinaryBuilder StageRegister(GesRegisterRef source) => AddOpcode(GesBytecodeOpCode.StageRegister, x: GesOperand.Register(source));

    public GesBinaryBuilder StageNothing() => AddOpcode(GesBytecodeOpCode.StageNothing);

    public GesBinaryBuilder StageInteger(long value, GesQuantityUnit unit = GesQuantityUnit.UnitNone) => AddOpcode(GesBytecodeOpCode.StageInteger, unit, i64: value);

    public GesBinaryBuilder StageFloat(double value, GesQuantityUnit unit = GesQuantityUnit.UnitNone) => AddOpcode(GesBytecodeOpCode.StageFloat, unit, f64: value);

    public GesBinaryBuilder StageText(string text) => AddOpcode(GesBytecodeOpCode.StageText, x: GesOperand.Text(text));

    public GesBinaryBuilder StageTag(string tag) => AddOpcode(GesBytecodeOpCode.StageTag, x: GesOperand.Text(tag));

    public GesBinaryBuilder StagePercentage(double ratio) => AddOpcode(GesBytecodeOpCode.StagePercentage, f64: ratio);

    public GesBinaryBuilder CreateDice(GesRegisterRef destination, short count, short sideCount) => AddOpcode(GesBytecodeOpCode.CreateDice, dst: GesOperand.Register(destination), y: GesOperand.I16(sideCount), count: count);

    public GesBinaryBuilder CreateVector(GesRegisterRef destination, short componentCount) => AddOpcode(GesBytecodeOpCode.CreateVector, dst: GesOperand.Register(destination), x: GesOperand.I16(componentCount));

    public GesBinaryBuilder CreatePoint(GesRegisterRef destination, short componentCount) => AddOpcode(GesBytecodeOpCode.CreatePoint, dst: GesOperand.Register(destination), x: GesOperand.I16(componentCount));

    public GesBinaryBuilder CreateList(GesRegisterRef destination) => AddOpcode(GesBytecodeOpCode.CreateList, dst: GesOperand.Register(destination));

    public GesBinaryBuilder CreateMap(GesRegisterRef destination, IReadOnlyList<string> keyNames) => AddOpcode(GesBytecodeOpCode.CreateMap, dst: GesOperand.Register(destination), secondaryList: GesOperand.TextList(keyNames));

    public GesBinaryBuilder CreateRange(GesRegisterRef destination, GesRegisterRef from, GesRegisterRef to) => AddOpcode(GesBytecodeOpCode.CreateRange, dst: GesOperand.Register(destination), x: GesOperand.Register(from), y: GesOperand.Register(to));

    public GesBinaryBuilder CreateRangeWithStep(GesRegisterRef destination, GesRegisterRef from, GesRegisterRef to, GesRegisterRef step)
        => AddOpcode(GesBytecodeOpCode.CreateRangeWithStep, dst: GesOperand.Register(destination), x: GesOperand.Register(from), y: GesOperand.Register(to), a: GesOperand.Register(step));

    public GesBinaryBuilder CreateRangeIterator(GesRegisterRef destination, GesRegisterRef from, GesRegisterRef to)
        => AddOpcode(GesBytecodeOpCode.CreateRangeIterator, dst: GesOperand.Register(destination), x: GesOperand.Register(from), y: GesOperand.Register(to));

    public GesBinaryBuilder CreateRangeIteratorWithStep(GesRegisterRef destination, GesRegisterRef from, GesRegisterRef to, GesRegisterRef step)
        => AddOpcode(GesBytecodeOpCode.CreateRangeIteratorWithStep, dst: GesOperand.Register(destination), x: GesOperand.Register(from), y: GesOperand.Register(to), a: GesOperand.Register(step));

    public GesBinaryBuilder CreateRangeIteratorShort(GesRegisterRef destination, short from, short to, short step)
        => AddOpcode(GesBytecodeOpCode.CreateRangeIteratorShort, dst: GesOperand.Register(destination), x: GesOperand.I16(from), y: GesOperand.I16(to), a: GesOperand.I16(step));

    public GesBinaryBuilder CreateRecord(GesRegisterRef destination, GesBindRef record) => AddOpcode(GesBytecodeOpCode.CreateRecord, dst: GesOperand.Register(destination), x: GesOperand.Bind(record));

    public GesBinaryBuilder CreateRecordValue(GesRegisterRef destination, GesRegisterRef map, string typeName)
        => AddOpcode(GesBytecodeOpCode.CreateRecordValue, dst: GesOperand.Register(destination), x: GesOperand.Register(map), y: GesOperand.Text(typeName));

    public GesBinaryBuilder CreateExternalType(GesRegisterRef destination, GesBindRef externalType, IReadOnlyList<string> argumentNames)
        => AddOpcode(GesBytecodeOpCode.CreateExternalType, dst: GesOperand.Register(destination), x: GesOperand.Bind(externalType), y: GesOperand.TextList(argumentNames));

    public GesBinaryBuilder HasValue(GesRegisterRef destination, GesRegisterRef operand) => UnaryOpcode(GesBytecodeOpCode.HasValue, destination, operand);

    public GesBinaryBuilder IsEmpty(GesRegisterRef destination, GesRegisterRef operand) => UnaryOpcode(GesBytecodeOpCode.IsEmpty, destination, operand);

    public GesBinaryBuilder Default(GesRegisterRef destination, GesRegisterRef value, GesRegisterRef defaultValue) => BinaryOpcode(GesBytecodeOpCode.Default, destination, value, defaultValue);

    public GesBinaryBuilder Or(GesRegisterRef destination, GesRegisterRef left, GesRegisterRef right) => BinaryOpcode(GesBytecodeOpCode.Or, destination, left, right);

    public GesBinaryBuilder And(GesRegisterRef destination, GesRegisterRef left, GesRegisterRef right) => BinaryOpcode(GesBytecodeOpCode.And, destination, left, right);

    public GesBinaryBuilder Xor(GesRegisterRef destination, GesRegisterRef left, GesRegisterRef right) => BinaryOpcode(GesBytecodeOpCode.Xor, destination, left, right);

    public GesBinaryBuilder Implies(GesRegisterRef destination, GesRegisterRef left, GesRegisterRef right) => BinaryOpcode(GesBytecodeOpCode.Implies, destination, left, right);

    public GesBinaryBuilder Not(GesRegisterRef destination, GesRegisterRef operand) => UnaryOpcode(GesBytecodeOpCode.Not, destination, operand);

    public GesBinaryBuilder Equal(GesRegisterRef destination, GesRegisterRef left, GesRegisterRef right) => BinaryOpcode(GesBytecodeOpCode.Equal, destination, left, right);

    public GesBinaryBuilder NotEqual(GesRegisterRef destination, GesRegisterRef left, GesRegisterRef right) => BinaryOpcode(GesBytecodeOpCode.NotEqual, destination, left, right);

    public GesBinaryBuilder Less(GesRegisterRef destination, GesRegisterRef left, GesRegisterRef right) => BinaryOpcode(GesBytecodeOpCode.Less, destination, left, right);

    public GesBinaryBuilder Greater(GesRegisterRef destination, GesRegisterRef left, GesRegisterRef right) => BinaryOpcode(GesBytecodeOpCode.Greater, destination, left, right);

    public GesBinaryBuilder LessOrEqual(GesRegisterRef destination, GesRegisterRef left, GesRegisterRef right) => BinaryOpcode(GesBytecodeOpCode.LessOrEqual, destination, left, right);

    public GesBinaryBuilder GreaterOrEqual(GesRegisterRef destination, GesRegisterRef left, GesRegisterRef right) => BinaryOpcode(GesBytecodeOpCode.GreaterOrEqual, destination, left, right);

    public GesBinaryBuilder NumericBinary(GesBytecodeOpCode opcode, GesRegisterRef destination, GesRegisterRef left, GesRegisterRef right)
        => AddOpcode(opcode, flags: GesInstructionFlag.Numeric, dst: GesOperand.Register(destination), x: GesOperand.Register(left), y: GesOperand.Register(right));

    public GesBinaryBuilder NumericUnary(GesBytecodeOpCode opcode, GesRegisterRef destination, GesRegisterRef operand) => AddOpcode(opcode, flags: GesInstructionFlag.Numeric, dst: GesOperand.Register(destination), x: GesOperand.Register(operand));

    public GesBinaryBuilder NumericClamp(GesRegisterRef destination, GesRegisterRef value, GesRegisterRef minimum, GesRegisterRef maximum)
        => AddOpcode(GesBytecodeOpCode.Clamp, flags: GesInstructionFlag.Numeric, dst: GesOperand.Register(destination), x: GesOperand.Register(value), y: GesOperand.Register(minimum), a: GesOperand.Register(maximum));

    public GesBinaryBuilder Add(GesRegisterRef destination, GesRegisterRef left, GesRegisterRef right) => BinaryOpcode(GesBytecodeOpCode.Add, destination, left, right);

    public GesBinaryBuilder Subtract(GesRegisterRef destination, GesRegisterRef left, GesRegisterRef right) => BinaryOpcode(GesBytecodeOpCode.Subtract, destination, left, right);

    public GesBinaryBuilder Multiply(GesRegisterRef destination, GesRegisterRef left, GesRegisterRef right) => BinaryOpcode(GesBytecodeOpCode.Multiply, destination, left, right);

    public GesBinaryBuilder Divide(GesRegisterRef destination, GesRegisterRef left, GesRegisterRef right) => BinaryOpcode(GesBytecodeOpCode.Divide, destination, left, right);

    public GesBinaryBuilder Power(GesRegisterRef destination, GesRegisterRef left, GesRegisterRef right) => BinaryOpcode(GesBytecodeOpCode.Power, destination, left, right);

    public GesBinaryBuilder IntegerDivide(GesRegisterRef destination, GesRegisterRef left, GesRegisterRef right) => BinaryOpcode(GesBytecodeOpCode.IntegerDivide, destination, left, right);

    public GesBinaryBuilder Modulo(GesRegisterRef destination, GesRegisterRef left, GesRegisterRef right) => BinaryOpcode(GesBytecodeOpCode.Modulo, destination, left, right);

    public GesBinaryBuilder Remainder(GesRegisterRef destination, GesRegisterRef left, GesRegisterRef right) => BinaryOpcode(GesBytecodeOpCode.Remainder, destination, left, right);

    public GesBinaryBuilder Min(GesRegisterRef destination, GesRegisterRef left, GesRegisterRef right) => BinaryOpcode(GesBytecodeOpCode.Min, destination, left, right);

    public GesBinaryBuilder Max(GesRegisterRef destination, GesRegisterRef left, GesRegisterRef right) => BinaryOpcode(GesBytecodeOpCode.Max, destination, left, right);

    public GesBinaryBuilder Negate(GesRegisterRef destination, GesRegisterRef operand) => UnaryOpcode(GesBytecodeOpCode.Negate, destination, operand);

    public GesBinaryBuilder Abs(GesRegisterRef destination, GesRegisterRef operand) => UnaryOpcode(GesBytecodeOpCode.Abs, destination, operand);

    public GesBinaryBuilder LogN(GesRegisterRef destination, GesRegisterRef operand) => UnaryOpcode(GesBytecodeOpCode.LogN, destination, operand);

    public GesBinaryBuilder Chance(GesRegisterRef destination, GesRegisterRef operand) => UnaryOpcode(GesBytecodeOpCode.Chance, destination, operand);

    public GesBinaryBuilder Exp(GesRegisterRef destination, GesRegisterRef operand) => UnaryOpcode(GesBytecodeOpCode.Exp, destination, operand);

    public GesBinaryBuilder Floor(GesRegisterRef destination, GesRegisterRef operand) => UnaryOpcode(GesBytecodeOpCode.Floor, destination, operand);

    public GesBinaryBuilder Ceil(GesRegisterRef destination, GesRegisterRef operand) => UnaryOpcode(GesBytecodeOpCode.Ceil, destination, operand);

    public GesBinaryBuilder Truncate(GesRegisterRef destination, GesRegisterRef operand) => UnaryOpcode(GesBytecodeOpCode.Truncate, destination, operand);

    public GesBinaryBuilder RoundHalfEven(GesRegisterRef destination, GesRegisterRef operand) => UnaryOpcode(GesBytecodeOpCode.RoundHalfEven, destination, operand);

    public GesBinaryBuilder RoundHalfUp(GesRegisterRef destination, GesRegisterRef operand) => UnaryOpcode(GesBytecodeOpCode.RoundHalfUp, destination, operand);

    public GesBinaryBuilder RoundHalfDown(GesRegisterRef destination, GesRegisterRef operand) => UnaryOpcode(GesBytecodeOpCode.RoundHalfDown, destination, operand);

    public GesBinaryBuilder DegreeToRadians(GesRegisterRef destination, GesRegisterRef operand) => UnaryOpcode(GesBytecodeOpCode.DegreeToRadians, destination, operand);

    public GesBinaryBuilder DegreeFromRadians(GesRegisterRef destination, GesRegisterRef operand) => UnaryOpcode(GesBytecodeOpCode.DegreeFromRadians, destination, operand);

    public GesBinaryBuilder WrapDegree(GesRegisterRef destination, GesRegisterRef operand) => UnaryOpcode(GesBytecodeOpCode.WrapDegree, destination, operand);

    public GesBinaryBuilder Sin(GesRegisterRef destination, GesRegisterRef operand) => UnaryOpcode(GesBytecodeOpCode.Sin, destination, operand);

    public GesBinaryBuilder Cos(GesRegisterRef destination, GesRegisterRef operand) => UnaryOpcode(GesBytecodeOpCode.Cos, destination, operand);

    public GesBinaryBuilder Tan(GesRegisterRef destination, GesRegisterRef operand) => UnaryOpcode(GesBytecodeOpCode.Tan, destination, operand);

    public GesBinaryBuilder Asin(GesRegisterRef destination, GesRegisterRef operand) => UnaryOpcode(GesBytecodeOpCode.Asin, destination, operand);

    public GesBinaryBuilder Acos(GesRegisterRef destination, GesRegisterRef operand) => UnaryOpcode(GesBytecodeOpCode.Acos, destination, operand);

    public GesBinaryBuilder Atan(GesRegisterRef destination, GesRegisterRef operand) => UnaryOpcode(GesBytecodeOpCode.Atan, destination, operand);

    public GesBinaryBuilder Atan2(GesRegisterRef destination, GesRegisterRef y, GesRegisterRef x) => BinaryOpcode(GesBytecodeOpCode.Atan2, destination, y, x);

    public GesBinaryBuilder Hypot2D(GesRegisterRef destination, GesRegisterRef x, GesRegisterRef y) => BinaryOpcode(GesBytecodeOpCode.Hypot2D, destination, x, y);

    public GesBinaryBuilder Hypot3D(GesRegisterRef destination, GesRegisterRef x, GesRegisterRef y, GesRegisterRef z)
        => AddOpcode(GesBytecodeOpCode.Hypot3D, dst: GesOperand.Register(destination), x: GesOperand.Register(x), y: GesOperand.Register(y), a: GesOperand.Register(z));

    public GesBinaryBuilder Distance(GesRegisterRef destination, GesRegisterRef left, GesRegisterRef right) => BinaryOpcode(GesBytecodeOpCode.Distance, destination, left, right);

    public GesBinaryBuilder Distance2D(GesRegisterRef destination, GesRegisterRef x1, GesRegisterRef y1, GesRegisterRef x2, GesRegisterRef y2)
        => AddOpcode(GesBytecodeOpCode.Distance2D, dst: GesOperand.Register(destination), x: GesOperand.Register(x1), y: GesOperand.Register(y1), a: GesOperand.Register(x2), b: GesOperand.Register(y2));

    public GesBinaryBuilder Distance3D(GesRegisterRef destination, GesRegisterRef x1, GesRegisterRef y1, GesRegisterRef z1, GesRegisterRef x2, GesRegisterRef y2, GesRegisterRef z2) => AddOpcode(
            GesBytecodeOpCode.Distance3D, dst: GesOperand.Register(destination), x: GesOperand.Register(x1), y: GesOperand.Register(y1), a: GesOperand.Register(z1),
            b: GesOperand.Register(x2), c: GesOperand.Register(y2), d: GesOperand.Register(z2));

    public GesBinaryBuilder DistanceSquared(GesRegisterRef destination, GesRegisterRef left, GesRegisterRef right) => BinaryOpcode(GesBytecodeOpCode.DistanceSquared, destination, left, right);

    public GesBinaryBuilder DistanceSquared2D(GesRegisterRef destination, GesRegisterRef x1, GesRegisterRef y1, GesRegisterRef x2, GesRegisterRef y2)
        => AddOpcode(GesBytecodeOpCode.DistanceSquared2D, dst: GesOperand.Register(destination), x: GesOperand.Register(x1), y: GesOperand.Register(y1), a: GesOperand.Register(x2), b: GesOperand.Register(y2));

    public GesBinaryBuilder DistanceSquared3D(GesRegisterRef destination, GesRegisterRef x1, GesRegisterRef y1, GesRegisterRef z1, GesRegisterRef x2, GesRegisterRef y2, GesRegisterRef z2) => AddOpcode(
            GesBytecodeOpCode.DistanceSquared3D, dst: GesOperand.Register(destination), x: GesOperand.Register(x1), y: GesOperand.Register(y1), a: GesOperand.Register(z1),
            b: GesOperand.Register(x2), c: GesOperand.Register(y2), d: GesOperand.Register(z2));

    public GesBinaryBuilder LengthSquared(GesRegisterRef destination, GesRegisterRef operand) => UnaryOpcode(GesBytecodeOpCode.LengthSquared, destination, operand);

    public GesBinaryBuilder LengthSquared2D(GesRegisterRef destination, GesRegisterRef x, GesRegisterRef y) => BinaryOpcode(GesBytecodeOpCode.LengthSquared2D, destination, x, y);

    public GesBinaryBuilder LengthSquared3D(GesRegisterRef destination, GesRegisterRef x, GesRegisterRef y, GesRegisterRef z)
        => AddOpcode(GesBytecodeOpCode.LengthSquared3D, dst: GesOperand.Register(destination), x: GesOperand.Register(x), y: GesOperand.Register(y), a: GesOperand.Register(z));

    public GesBinaryBuilder Normalize(GesRegisterRef destination, GesRegisterRef operand) => UnaryOpcode(GesBytecodeOpCode.Normalize, destination, operand);

    public GesBinaryBuilder Normalize2D(GesRegisterRef destination, GesRegisterRef x, GesRegisterRef y) => BinaryOpcode(GesBytecodeOpCode.Normalize2D, destination, x, y);

    public GesBinaryBuilder Normalize3D(GesRegisterRef destination, GesRegisterRef x, GesRegisterRef y, GesRegisterRef z)
        => AddOpcode(GesBytecodeOpCode.Normalize3D, dst: GesOperand.Register(destination), x: GesOperand.Register(x), y: GesOperand.Register(y), a: GesOperand.Register(z));

    public GesBinaryBuilder Dot(GesRegisterRef destination, GesRegisterRef left, GesRegisterRef right) => BinaryOpcode(GesBytecodeOpCode.Dot, destination, left, right);

    public GesBinaryBuilder Dot2D(GesRegisterRef destination, GesRegisterRef x1, GesRegisterRef y1, GesRegisterRef x2, GesRegisterRef y2)
        => AddOpcode(GesBytecodeOpCode.Dot2D, dst: GesOperand.Register(destination), x: GesOperand.Register(x1), y: GesOperand.Register(y1), a: GesOperand.Register(x2), b: GesOperand.Register(y2));

    public GesBinaryBuilder Dot3D(GesRegisterRef destination, GesRegisterRef x1, GesRegisterRef y1, GesRegisterRef z1, GesRegisterRef x2, GesRegisterRef y2, GesRegisterRef z2) => AddOpcode(
            GesBytecodeOpCode.Dot3D, dst: GesOperand.Register(destination), x: GesOperand.Register(x1), y: GesOperand.Register(y1), a: GesOperand.Register(z1),
            b: GesOperand.Register(x2), c: GesOperand.Register(y2), d: GesOperand.Register(z2));

    public GesBinaryBuilder Cross(GesRegisterRef destination, GesRegisterRef left, GesRegisterRef right) => BinaryOpcode(GesBytecodeOpCode.Cross, destination, left, right);

    public GesBinaryBuilder Cross2D(GesRegisterRef destination, GesRegisterRef x1, GesRegisterRef y1, GesRegisterRef x2, GesRegisterRef y2)
        => AddOpcode(GesBytecodeOpCode.Cross2D, dst: GesOperand.Register(destination), x: GesOperand.Register(x1), y: GesOperand.Register(y1), a: GesOperand.Register(x2), b: GesOperand.Register(y2));

    public GesBinaryBuilder Cross3D(GesRegisterRef destination, GesRegisterRef x1, GesRegisterRef y1, GesRegisterRef z1, GesRegisterRef x2, GesRegisterRef y2, GesRegisterRef z2) => AddOpcode(
            GesBytecodeOpCode.Cross3D, dst: GesOperand.Register(destination), x: GesOperand.Register(x1), y: GesOperand.Register(y1), a: GesOperand.Register(z1),
            b: GesOperand.Register(x2), c: GesOperand.Register(y2), d: GesOperand.Register(z2));

    public GesBinaryBuilder AngleBetween(GesRegisterRef destination, GesRegisterRef left, GesRegisterRef right) => BinaryOpcode(GesBytecodeOpCode.AngleBetween, destination, left, right);

    public GesBinaryBuilder AngleBetween2D(GesRegisterRef destination, GesRegisterRef x1, GesRegisterRef y1, GesRegisterRef x2, GesRegisterRef y2)
        => AddOpcode(GesBytecodeOpCode.AngleBetween2D, dst: GesOperand.Register(destination), x: GesOperand.Register(x1), y: GesOperand.Register(y1), a: GesOperand.Register(x2), b: GesOperand.Register(y2));

    public GesBinaryBuilder AngleBetween3D(GesRegisterRef destination, GesRegisterRef x1, GesRegisterRef y1, GesRegisterRef z1, GesRegisterRef x2, GesRegisterRef y2, GesRegisterRef z2) => AddOpcode(
            GesBytecodeOpCode.AngleBetween3D, dst: GesOperand.Register(destination), x: GesOperand.Register(x1), y: GesOperand.Register(y1), a: GesOperand.Register(z1),
            b: GesOperand.Register(x2), c: GesOperand.Register(y2), d: GesOperand.Register(z2));

    public GesBinaryBuilder Clamp(GesRegisterRef destination, GesRegisterRef value, GesRegisterRef minimum, GesRegisterRef maximum)
        => AddOpcode(GesBytecodeOpCode.Clamp, dst: GesOperand.Register(destination), x: GesOperand.Register(value), y: GesOperand.Register(minimum), a: GesOperand.Register(maximum));

    public GesBinaryBuilder RandomTake(GesRegisterRef destination, GesRegisterRef from, GesRegisterRef to) => BinaryOpcode(GesBytecodeOpCode.RandomTake, destination, from, to);

    public GesBinaryBuilder RandomTakeFloat(GesRegisterRef destination, GesRegisterRef from, GesRegisterRef to) => BinaryOpcode(GesBytecodeOpCode.RandomTakeFloat, destination, from, to);

    public GesBinaryBuilder RandomPush(GesRegisterRef seed) => AddOpcode(GesBytecodeOpCode.RandomPush, x: GesOperand.Register(seed));

    public GesBinaryBuilder RandomPushConstant(long seed) => AddOpcode(GesBytecodeOpCode.RandomPushConstant, i64: seed);

    public GesBinaryBuilder RandomPop() => AddOpcode(GesBytecodeOpCode.RandomPop);

    public GesBinaryBuilder Term(GesRegisterRef destination, GesRegisterRef series, GesRegisterRef index) => BinaryOpcode(GesBytecodeOpCode.Term, destination, series, index);

    public GesBinaryBuilder TakeFirst(GesRegisterRef destination, GesRegisterRef source, short count) => CountOpcode(GesBytecodeOpCode.TakeFirst, destination, source, count);

    public GesBinaryBuilder DropFirst(GesRegisterRef destination, GesRegisterRef source, short count) => CountOpcode(GesBytecodeOpCode.DropFirst, destination, source, count);

    public GesBinaryBuilder TakeLast(GesRegisterRef destination, GesRegisterRef source, short count) => CountOpcode(GesBytecodeOpCode.TakeLast, destination, source, count);

    public GesBinaryBuilder DropLast(GesRegisterRef destination, GesRegisterRef source, short count) => CountOpcode(GesBytecodeOpCode.DropLast, destination, source, count);

    public GesBinaryBuilder TakeHighest(GesRegisterRef destination, GesRegisterRef source, short count) => CountOpcode(GesBytecodeOpCode.TakeHighest, destination, source, count);

    public GesBinaryBuilder TakeLowest(GesRegisterRef destination, GesRegisterRef source, short count) => CountOpcode(GesBytecodeOpCode.TakeLowest, destination, source, count);

    public GesBinaryBuilder DropHighest(GesRegisterRef destination, GesRegisterRef source, short count) => CountOpcode(GesBytecodeOpCode.DropHighest, destination, source, count);

    public GesBinaryBuilder DropLowest(GesRegisterRef destination, GesRegisterRef source, short count) => CountOpcode(GesBytecodeOpCode.DropLowest, destination, source, count);

    public GesBinaryBuilder OneRandom(GesRegisterRef destination, GesRegisterRef source) => UnaryOpcode(GesBytecodeOpCode.OneRandom, destination, source);

    public GesBinaryBuilder TakeRandom(GesRegisterRef destination, GesRegisterRef source, short count) => CountOpcode(GesBytecodeOpCode.TakeRandom, destination, source, count);

    public GesBinaryBuilder OneWeighted(GesRegisterRef destination, GesRegisterRef items, GesRegisterRef weights) => BinaryOpcode(GesBytecodeOpCode.OneWeighted, destination, items, weights);

    public GesBinaryBuilder TakeWeighted(GesRegisterRef destination, GesRegisterRef items, GesRegisterRef weights, short count)
        => AddOpcode(GesBytecodeOpCode.TakeWeighted, dst: GesOperand.Register(destination), x: GesOperand.Register(items), y: GesOperand.I16(count), a: GesOperand.Register(weights));

    public GesBinaryBuilder Count(GesRegisterRef destination, GesRegisterRef source) => UnaryOpcode(GesBytecodeOpCode.Count, destination, source);

    public GesBinaryBuilder StartsWith(GesRegisterRef destination, GesRegisterRef left, GesRegisterRef right) => BinaryOpcode(GesBytecodeOpCode.StartsWith, destination, left, right);

    public GesBinaryBuilder EndsWith(GesRegisterRef destination, GesRegisterRef left, GesRegisterRef right) => BinaryOpcode(GesBytecodeOpCode.EndsWith, destination, left, right);

    public GesBinaryBuilder Contains(GesRegisterRef destination, GesRegisterRef left, GesRegisterRef right) => BinaryOpcode(GesBytecodeOpCode.Contains, destination, left, right);

    public GesBinaryBuilder ContainsAny(GesRegisterRef destination, GesRegisterRef left, GesRegisterRef right) => BinaryOpcode(GesBytecodeOpCode.ContainsAny, destination, left, right);

    public GesBinaryBuilder ContainsAll(GesRegisterRef destination, GesRegisterRef left, GesRegisterRef right) => BinaryOpcode(GesBytecodeOpCode.ContainsAll, destination, left, right);

    public GesBinaryBuilder HasAny(GesRegisterRef destination, GesRegisterRef source) => UnaryOpcode(GesBytecodeOpCode.HasAny, destination, source);

    public GesBinaryBuilder HasAll(GesRegisterRef destination, GesRegisterRef source) => UnaryOpcode(GesBytecodeOpCode.HasAll, destination, source);

    public GesBinaryBuilder ContainsValue(GesRegisterRef destination, GesRegisterRef left, GesRegisterRef right) => BinaryOpcode(GesBytecodeOpCode.ContainsValue, destination, left, right);

    public GesBinaryBuilder Union(GesRegisterRef destination, GesRegisterRef left, GesRegisterRef right) => BinaryOpcode(GesBytecodeOpCode.Union, destination, left, right);

    public GesBinaryBuilder Intersect(GesRegisterRef destination, GesRegisterRef left, GesRegisterRef right) => BinaryOpcode(GesBytecodeOpCode.Intersect, destination, left, right);

    public GesBinaryBuilder Zip(GesRegisterRef destination, GesRegisterRef left, GesRegisterRef right) => BinaryOpcode(GesBytecodeOpCode.Zip, destination, left, right);

    public GesBinaryBuilder KeysOfMap(GesRegisterRef destination, GesRegisterRef operand) => UnaryOpcode(GesBytecodeOpCode.KeysOfMap, destination, operand);

    public GesBinaryBuilder ValuesOfMap(GesRegisterRef destination, GesRegisterRef operand) => UnaryOpcode(GesBytecodeOpCode.ValuesOfMap, destination, operand);

    public GesBinaryBuilder EntriesOfMap(GesRegisterRef destination, GesRegisterRef operand) => UnaryOpcode(GesBytecodeOpCode.EntriesOfMap, destination, operand);

    public GesBinaryBuilder First(GesRegisterRef destination, GesRegisterRef operand) => UnaryOpcode(GesBytecodeOpCode.First, destination, operand);

    public GesBinaryBuilder Last(GesRegisterRef destination, GesRegisterRef operand) => UnaryOpcode(GesBytecodeOpCode.Last, destination, operand);

    public GesBinaryBuilder Single(GesRegisterRef destination, GesRegisterRef operand) => UnaryOpcode(GesBytecodeOpCode.Single, destination, operand);

    public GesBinaryBuilder IteratorCreate(GesRegisterRef destination, GesRegisterRef collection) => UnaryOpcode(GesBytecodeOpCode.IteratorCreate, destination, collection);

    public GesBinaryBuilder IteratorCreateOrJump(GesRegisterRef destination, GesRegisterRef collection, GesLabelRef failureTarget)
        => AddOpcode(GesBytecodeOpCode.IteratorCreateOrJump, dst: GesOperand.Register(destination), x: GesOperand.Register(collection), y: GesOperand.Label(failureTarget));

    public GesBinaryBuilder IteratorNext(GesRegisterRef destination, GesRegisterRef iterator, GesLabelRef exhaustedTarget)
        => AddOpcode(GesBytecodeOpCode.IteratorNext, dst: GesOperand.Register(destination), x: GesOperand.Register(iterator), y: GesOperand.Label(exhaustedTarget));

    public GesBinaryBuilder IteratorCreateSources(GesRegisterRef destination, IReadOnlyList<GesRegisterRef> sources, byte mode, GesLabelRef failureTarget)
        => AddOpcode(GesBytecodeOpCode.IteratorCreateOrJump, flags: (GesInstructionFlag)(mode << 5), dst: GesOperand.Register(destination),
            x: GesOperand.RegisterList(sources), y: GesOperand.Label(failureTarget));

    public GesBinaryBuilder IteratorCreateEntries(GesRegisterRef destination, GesRegisterRef source, GesLabelRef failureTarget)
        => AddOpcode(GesBytecodeOpCode.IteratorCreateOrJump, flags: (GesInstructionFlag)(6 << 5), dst: GesOperand.Register(destination),
            x: GesOperand.Register(source), y: GesOperand.Label(failureTarget));

    public GesBinaryBuilder IteratorNextComponents(IReadOnlyList<GesRegisterRef> destinations, GesRegisterRef iterator, GesLabelRef exhaustedTarget)
        => AddOpcode(GesBytecodeOpCode.IteratorNext, flags: (GesInstructionFlag)32, dst: GesOperand.RegisterList(destinations),
            x: GesOperand.Register(iterator), y: GesOperand.Label(exhaustedTarget));

    public GesBinaryBuilder Cartesian(GesRegisterRef destination, GesRegisterRef left, GesRegisterRef right)
        => AddOpcode(GesBytecodeOpCode.Cartesian, dst: GesOperand.Register(destination), x: GesOperand.Register(left), y: GesOperand.Register(right));

    public GesBinaryBuilder IteratorClose(GesRegisterRef iterator) => AddOpcode(GesBytecodeOpCode.IteratorClose, x: GesOperand.Register(iterator));

    public GesBinaryBuilder Distinct(GesRegisterRef destination, GesRegisterRef source) => UnaryOpcode(GesBytecodeOpCode.Distinct, destination, source);

    public GesBinaryBuilder SortAscending(GesRegisterRef destination, GesRegisterRef source) => UnaryOpcode(GesBytecodeOpCode.SortAscending, destination, source);

    public GesBinaryBuilder SortDescending(GesRegisterRef destination, GesRegisterRef source) => UnaryOpcode(GesBytecodeOpCode.SortDescending, destination, source);

    public GesBinaryBuilder Reverse(GesRegisterRef destination, GesRegisterRef source) => UnaryOpcode(GesBytecodeOpCode.Reverse, destination, source);

    public GesBinaryBuilder Shuffle(GesRegisterRef destination, GesRegisterRef source) => UnaryOpcode(GesBytecodeOpCode.Shuffle, destination, source);

    public GesBinaryBuilder ListBuilderCreate(GesRegisterRef destination) => AddOpcode(GesBytecodeOpCode.ListBuilderCreate, dst: GesOperand.Register(destination));

    public GesBinaryBuilder ListBuilderAdd(GesRegisterRef builder, GesRegisterRef item) => AddOpcode(GesBytecodeOpCode.ListBuilderAdd, x: GesOperand.Register(builder), y: GesOperand.Register(item));

    public GesBinaryBuilder ListBuilderFinish(GesRegisterRef destination, GesRegisterRef builder) => AddOpcode(GesBytecodeOpCode.ListBuilderFinish, dst: GesOperand.Register(destination), x: GesOperand.Register(builder));

    public GesBinaryBuilder MapBuilderCreate(GesRegisterRef destination) => AddOpcode(GesBytecodeOpCode.MapBuilderCreate, dst: GesOperand.Register(destination));

    public GesBinaryBuilder MapBuilderAdd(GesRegisterRef builder, GesRegisterRef key, GesRegisterRef value) => AddOpcode(GesBytecodeOpCode.MapBuilderAdd, x: GesOperand.Register(builder), y: GesOperand.Register(key), a: GesOperand.Register(value));

    public GesBinaryBuilder MapBuilderFinish(GesRegisterRef destination, GesRegisterRef builder) => AddOpcode(GesBytecodeOpCode.MapBuilderFinish, dst: GesOperand.Register(destination), x: GesOperand.Register(builder));

    public GesBinaryBuilder DistinctBuilderCreate(GesRegisterRef destination) => AddOpcode(GesBytecodeOpCode.DistinctBuilderCreate, dst: GesOperand.Register(destination));

    public GesBinaryBuilder DistinctBuilderAdd(GesRegisterRef builder, GesRegisterRef key, GesRegisterRef value)
        => AddOpcode(GesBytecodeOpCode.DistinctBuilderAdd, x: GesOperand.Register(builder), y: GesOperand.Register(key), a: GesOperand.Register(value));

    public GesBinaryBuilder DistinctBuilderFinish(GesRegisterRef destination, GesRegisterRef builder) => AddOpcode(GesBytecodeOpCode.DistinctBuilderFinish, dst: GesOperand.Register(destination), x: GesOperand.Register(builder));

    public GesBinaryBuilder GroupBuilderCreate(GesRegisterRef destination) => AddOpcode(GesBytecodeOpCode.GroupBuilderCreate, dst: GesOperand.Register(destination));

    public GesBinaryBuilder GroupBuilderAdd(GesRegisterRef builder, GesRegisterRef key, GesRegisterRef value)
        => AddOpcode(GesBytecodeOpCode.GroupBuilderAdd, x: GesOperand.Register(builder), y: GesOperand.Register(key), a: GesOperand.Register(value));

    public GesBinaryBuilder GroupBuilderFinish(GesRegisterRef destination, GesRegisterRef builder) => AddOpcode(GesBytecodeOpCode.GroupBuilderFinish, dst: GesOperand.Register(destination), x: GesOperand.Register(builder));

    public GesBinaryBuilder OrderBuilderCreate(GesRegisterRef destination) => AddOpcode(GesBytecodeOpCode.OrderBuilderCreate, dst: GesOperand.Register(destination));

    public GesBinaryBuilder OrderBuilderAdd(GesRegisterRef builder, GesRegisterRef key, GesRegisterRef value)
        => AddOpcode(GesBytecodeOpCode.OrderBuilderAdd, x: GesOperand.Register(builder), y: GesOperand.Register(key), a: GesOperand.Register(value));

    public GesBinaryBuilder OrderBuilderFinishAscending(GesRegisterRef destination, GesRegisterRef builder) => AddOpcode(GesBytecodeOpCode.OrderBuilderFinishAscending, dst: GesOperand.Register(destination), x: GesOperand.Register(builder));

    public GesBinaryBuilder OrderBuilderFinishDescending(GesRegisterRef destination, GesRegisterRef builder) => AddOpcode(GesBytecodeOpCode.OrderBuilderFinishDescending, dst: GesOperand.Register(destination), x: GesOperand.Register(builder));

    public GesBinaryBuilder HasPattern(GesRegisterRef destination, GesRegisterRef source, GesBytecodePatternKind pattern, short count = 0, GesRegisterRef? face = null)
        => PatternOpcode(GesBytecodeOpCode.HasPattern, destination, source, pattern, count, face);

    public GesBinaryBuilder TakePattern(GesRegisterRef destination, GesRegisterRef source, GesBytecodePatternKind pattern, short count = 0, GesRegisterRef? face = null)
        => PatternOpcode(GesBytecodeOpCode.TakePattern, destination, source, pattern, count, face);

    public GesBinaryBuilder LoadBoolean(GesRegisterRef destination, bool value) => value ? LoadTrue(destination) : LoadFalse(destination);

    public GesBinaryBuilder StageBoolean(bool value) => AddOpcode(value ? GesBytecodeOpCode.StageTrue : GesBytecodeOpCode.StageFalse);

    private GesBinaryBuilder UnaryOpcode(GesBytecodeOpCode opcode, GesRegisterRef destination, GesRegisterRef operand) => AddOpcode(opcode, dst: GesOperand.Register(destination), x: GesOperand.Register(operand));

    private GesBinaryBuilder BinaryOpcode(GesBytecodeOpCode opcode, GesRegisterRef destination, GesRegisterRef left, GesRegisterRef right)
        => AddOpcode(opcode, dst: GesOperand.Register(destination), x: GesOperand.Register(left), y: GesOperand.Register(right));

    private GesBinaryBuilder CountOpcode(GesBytecodeOpCode opcode, GesRegisterRef destination, GesRegisterRef source, short count) => AddOpcode(opcode, dst: GesOperand.Register(destination), x: GesOperand.Register(source), y: GesOperand.I16(count));

    private GesBinaryBuilder SelectorOpcode(GesBytecodeOpCode opcode, GesRegisterRef destination, GesRegisterRef source, GesRegisterRef itemBinding, GesLabelRef keyEntry)
        => AddOpcode(opcode, dst: GesOperand.Register(destination), x: GesOperand.Register(source), y: GesOperand.Register(itemBinding), a: GesOperand.Label(keyEntry));

    private GesBinaryBuilder PatternOpcode(GesBytecodeOpCode opcode, GesRegisterRef destination, GesRegisterRef source, GesBytecodePatternKind pattern, short count, GesRegisterRef? face)
        => AddOpcode(opcode, dst: GesOperand.Register(destination), x: GesOperand.Register(source), y: GesOperand.I16(count), a: GesOperand.U16((ushort)pattern), b: face.HasValue ? GesOperand.Register(face.Value) : default);
}
