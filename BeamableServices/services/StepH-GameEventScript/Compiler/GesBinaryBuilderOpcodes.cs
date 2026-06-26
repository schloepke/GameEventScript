using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using StepH.GameEventScript.Api;

namespace StepH.GameEventScript.Compiler;

[SuppressMessage("ReSharper", "UnusedMember.Global")]
[SuppressMessage("ReSharper", "UnusedMethodReturnValue.Global")]
[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
internal sealed partial class GesBinaryBuilder
{
    public GesBinaryBuilder Nop()
        => AddOpcode(GameEventScriptBytecodeOpCode.Nop);

    public GesBinaryBuilder RegisterLocals(short count)
        => AddOpcode(GameEventScriptBytecodeOpCode.RegisterLocals, count: count);

    public GesBinaryBuilder Jump(GesLabelRef target)
        => AddOpcode(GameEventScriptBytecodeOpCode.Jump, y: GesOperand.Label(target));

    public GesBinaryBuilder JumpIfTrue(GesRegisterRef condition, GesLabelRef target)
        => AddOpcode(GameEventScriptBytecodeOpCode.JumpIfTrue, x: GesOperand.Register(condition), y: GesOperand.Label(target));

    public GesBinaryBuilder JumpIfFalse(GesRegisterRef condition, GesLabelRef target)
        => AddOpcode(GameEventScriptBytecodeOpCode.JumpIfFalse, x: GesOperand.Register(condition), y: GesOperand.Label(target));

    public GesBinaryBuilder JumpIfNotTrue(GesRegisterRef condition, GesLabelRef target)
        => AddOpcode(GameEventScriptBytecodeOpCode.JumpIfNotTrue, x: GesOperand.Register(condition), y: GesOperand.Label(target));

    public GesBinaryBuilder JumpIfNothing(GesRegisterRef condition, GesLabelRef target)
        => AddOpcode(GameEventScriptBytecodeOpCode.JumpIfNothing, x: GesOperand.Register(condition), y: GesOperand.Label(target));

    public GesBinaryBuilder Call(GesRegisterRef destination, GesLabelRef entryAddress, GameEventScriptInstructionFlag flags = GameEventScriptInstructionFlag.None)
        => AddOpcode(GameEventScriptBytecodeOpCode.Call, flags: flags, dst: GesOperand.Register(destination), y: GesOperand.Label(entryAddress));

    public GesBinaryBuilder CallStandard(GesRegisterRef destination, IReadOnlyList<string> shape, IReadOnlyList<GesRegisterRef> arguments, GameEventScriptInstructionFlag flags = GameEventScriptInstructionFlag.None)
        => AddOpcode(GameEventScriptBytecodeOpCode.CallStandard, flags: flags, dst: GesOperand.Register(destination), x: GesOperand.TextList(shape), y: GesOperand.RegisterList(arguments));

    public GesBinaryBuilder CallExternal(GesRegisterRef destination, GesBindRef bind, IReadOnlyList<GesRegisterRef> arguments, GameEventScriptInstructionFlag flags = GameEventScriptInstructionFlag.None)
        => AddOpcode(GameEventScriptBytecodeOpCode.CallExternal, flags: flags, dst: GesOperand.Register(destination), x: GesOperand.Bind(bind), y: GesOperand.RegisterList(arguments));

    public GesBinaryBuilder ReturnVoid()
        => AddOpcode(GameEventScriptBytecodeOpCode.ReturnVoid);

    public GesBinaryBuilder ReturnValue(GesRegisterRef value)
        => AddOpcode(GameEventScriptBytecodeOpCode.ReturnValue, x: GesOperand.Register(value));

    public GesBinaryBuilder EmitMessage(GesBindRef outboundMessage, IReadOnlyList<GesRegisterRef> arguments)
        => AddOpcode(GameEventScriptBytecodeOpCode.EmitMessage, dst: GesOperand.Bind(outboundMessage), y: GesOperand.RegisterList(arguments));

    public GesBinaryBuilder EmitMessageWithTags(GesBindRef outboundMessage, IReadOnlyList<GesRegisterRef> arguments, IReadOnlyList<GesRegisterRef> tags)
        => AddOpcode(GameEventScriptBytecodeOpCode.EmitMessageWithTags, dst: GesOperand.Bind(outboundMessage), y: GesOperand.RegisterList(arguments), secondaryList: GesOperand.RegisterList(tags));

    public GesBinaryBuilder EmitMessageValue(GesRegisterRef message)
        => AddOpcode(GameEventScriptBytecodeOpCode.EmitMessageValue, x: GesOperand.Register(message));

    public GesBinaryBuilder EmitMessageValueWithTags(GesRegisterRef message, IReadOnlyList<GesRegisterRef> tags)
        => AddOpcode(GameEventScriptBytecodeOpCode.EmitMessageValueWithTags, x: GesOperand.Register(message), y: GesOperand.RegisterList(tags));

    public GesBinaryBuilder PublishMessage(GesBindRef outboundMessage, IReadOnlyList<GesRegisterRef> arguments)
        => AddOpcode(GameEventScriptBytecodeOpCode.PublishMessage, dst: GesOperand.Bind(outboundMessage), y: GesOperand.RegisterList(arguments));

    public GesBinaryBuilder PublishMessageWithTags(GesBindRef outboundMessage, IReadOnlyList<GesRegisterRef> arguments, IReadOnlyList<GesRegisterRef> tags)
        => AddOpcode(GameEventScriptBytecodeOpCode.PublishMessageWithTags, dst: GesOperand.Bind(outboundMessage), y: GesOperand.RegisterList(arguments), secondaryList: GesOperand.RegisterList(tags));

    public GesBinaryBuilder PublishMessageValue(GesRegisterRef message)
        => AddOpcode(GameEventScriptBytecodeOpCode.PublishMessageValue, x: GesOperand.Register(message));

    public GesBinaryBuilder PublishMessageValueWithTags(GesRegisterRef message, IReadOnlyList<GesRegisterRef> tags)
        => AddOpcode(GameEventScriptBytecodeOpCode.PublishMessageValueWithTags, x: GesOperand.Register(message), y: GesOperand.RegisterList(tags));

    public GesBinaryBuilder Cast(GesRegisterRef destination, GesRegisterRef source, GameEventScriptBytecodeTypeKind type)
        => AddOpcode(GameEventScriptBytecodeOpCode.Cast, dst: GesOperand.Register(destination), x: GesOperand.Register(source), y: GesOperand.Type(type));

    public GesBinaryBuilder CastCustom(GesRegisterRef destination, GesRegisterRef source, string typeName)
        => AddOpcode(GameEventScriptBytecodeOpCode.CastCustom, dst: GesOperand.Register(destination), x: GesOperand.Register(source), y: GesOperand.Text(typeName));

    public GesBinaryBuilder CastUnit(GesRegisterRef destination, GesRegisterRef source, GameEventScriptBytecodeInstructionUnit unit)
        => AddOpcode(GameEventScriptBytecodeOpCode.CastUnit, unit, dst: GesOperand.Register(destination), x: GesOperand.Register(source));

    public GesBinaryBuilder CastNumeric(GesRegisterRef destination, GesRegisterRef source)
        => AddOpcode(GameEventScriptBytecodeOpCode.CastNumeric, dst: GesOperand.Register(destination), x: GesOperand.Register(source));

    public GesBinaryBuilder CheckType(GesRegisterRef destination, GesRegisterRef source, GameEventScriptBytecodeTypeKind type)
        => AddOpcode(GameEventScriptBytecodeOpCode.CheckType, dst: GesOperand.Register(destination), x: GesOperand.Register(source), y: GesOperand.Type(type));

    public GesBinaryBuilder CheckCustomType(GesRegisterRef destination, GesRegisterRef source, string typeName)
        => AddOpcode(GameEventScriptBytecodeOpCode.CheckCustomType, dst: GesOperand.Register(destination), x: GesOperand.Register(source), y: GesOperand.Text(typeName));

    public GesBinaryBuilder CheckUnit(GesRegisterRef destination, GesRegisterRef source, GameEventScriptBytecodeInstructionUnit unit)
        => AddOpcode(GameEventScriptBytecodeOpCode.CheckUnit, unit, dst: GesOperand.Register(destination), x: GesOperand.Register(source));

    public GesBinaryBuilder CheckNumeric(GesRegisterRef destination, GesRegisterRef source)
        => AddOpcode(GameEventScriptBytecodeOpCode.CheckNumeric, dst: GesOperand.Register(destination), x: GesOperand.Register(source));

    public GesBinaryBuilder CheckInteger(GesRegisterRef destination, GesRegisterRef source)
        => AddOpcode(GameEventScriptBytecodeOpCode.CheckInteger, dst: GesOperand.Register(destination), x: GesOperand.Register(source));

    public GesBinaryBuilder CheckFractional(GesRegisterRef destination, GesRegisterRef source)
        => AddOpcode(GameEventScriptBytecodeOpCode.CheckFractional, dst: GesOperand.Register(destination), x: GesOperand.Register(source));

    public GesBinaryBuilder Move(GesRegisterRef destination, GesRegisterRef source)
        => AddOpcode(GameEventScriptBytecodeOpCode.Move, dst: GesOperand.Register(destination), x: GesOperand.Register(source));

    public GesBinaryBuilder MemberAccess(GesRegisterRef destination, string memberName, GesRegisterRef target)
        => AddOpcode(GameEventScriptBytecodeOpCode.MemberAccess, dst: GesOperand.Register(destination), x: GesOperand.Text(memberName), y: GesOperand.Register(target));

    public GesBinaryBuilder IndexAccess(GesRegisterRef destination, ushort index, GesRegisterRef target)
        => AddOpcode(GameEventScriptBytecodeOpCode.IndexAccess, dst: GesOperand.Register(destination), x: GesOperand.U16(index), y: GesOperand.Register(target));

    public GesBinaryBuilder PropertyAccess(GesRegisterRef destination, GesRegisterRef property, GesRegisterRef target)
        => AddOpcode(GameEventScriptBytecodeOpCode.PropertyAccess, dst: GesOperand.Register(destination), x: GesOperand.Register(property), y: GesOperand.Register(target));

    public GesBinaryBuilder BindHandler(GesRegisterRef destination, GesRegisterRef handler, IReadOnlyList<GesRegisterRef> arguments)
        => AddOpcode(GameEventScriptBytecodeOpCode.BindHandler, dst: GesOperand.Register(destination), x: GesOperand.Register(handler), y: GesOperand.RegisterList(arguments));

    public GesBinaryBuilder LoadNothing(GesRegisterRef destination)
        => AddOpcode(GameEventScriptBytecodeOpCode.LoadNothing, dst: GesOperand.Register(destination));

    public GesBinaryBuilder LoadTrue(GesRegisterRef destination)
        => AddOpcode(GameEventScriptBytecodeOpCode.LoadTrue, dst: GesOperand.Register(destination));

    public GesBinaryBuilder LoadFalse(GesRegisterRef destination)
        => AddOpcode(GameEventScriptBytecodeOpCode.LoadFalse, dst: GesOperand.Register(destination));

    public GesBinaryBuilder LoadInteger(GesRegisterRef destination, long value, GameEventScriptBytecodeInstructionUnit unit = GameEventScriptBytecodeInstructionUnit.UnitNone)
        => AddOpcode(GameEventScriptBytecodeOpCode.LoadInteger, unit, dst: GesOperand.Register(destination), i64: value);

    public GesBinaryBuilder LoadFloat(GesRegisterRef destination, double value, GameEventScriptBytecodeInstructionUnit unit = GameEventScriptBytecodeInstructionUnit.UnitNone)
        => AddOpcode(GameEventScriptBytecodeOpCode.LoadFloat, unit, dst: GesOperand.Register(destination), f64: value);

    public GesBinaryBuilder LoadPercentage(GesRegisterRef destination, double ratio)
        => AddOpcode(GameEventScriptBytecodeOpCode.LoadPercentage, dst: GesOperand.Register(destination), f64: ratio);

    public GesBinaryBuilder LoadText(GesRegisterRef destination, string text)
        => AddOpcode(GameEventScriptBytecodeOpCode.LoadText, dst: GesOperand.Register(destination), x: GesOperand.Text(text));

    public GesBinaryBuilder LoadTag(GesRegisterRef destination, string tag)
        => AddOpcode(GameEventScriptBytecodeOpCode.LoadTag, dst: GesOperand.Register(destination), x: GesOperand.Text(tag));

    public GesBinaryBuilder LoadHandler(GesRegisterRef destination, IReadOnlyList<string> shape)
        => AddOpcode(GameEventScriptBytecodeOpCode.LoadHandler, dst: GesOperand.Register(destination), y: GesOperand.TextList(shape));

    public GesBinaryBuilder LoadMessage(GesRegisterRef destination, IReadOnlyList<string> shape, IReadOnlyList<GesRegisterRef> arguments)
        => AddOpcode(GameEventScriptBytecodeOpCode.LoadMessage, dst: GesOperand.Register(destination), x: GesOperand.TextList(shape), y: GesOperand.RegisterList(arguments));

    public GesBinaryBuilder StageRegister(GesRegisterRef source)
        => AddOpcode(GameEventScriptBytecodeOpCode.StageRegister, x: GesOperand.Register(source));

    public GesBinaryBuilder StageNothing()
        => AddOpcode(GameEventScriptBytecodeOpCode.StageNothing);

    public GesBinaryBuilder StageTrue()
        => AddOpcode(GameEventScriptBytecodeOpCode.StageTrue);

    public GesBinaryBuilder StageFalse()
        => AddOpcode(GameEventScriptBytecodeOpCode.StageFalse);

    public GesBinaryBuilder StageInteger(long value, GameEventScriptBytecodeInstructionUnit unit = GameEventScriptBytecodeInstructionUnit.UnitNone)
        => AddOpcode(GameEventScriptBytecodeOpCode.StageInteger, unit, i64: value);

    public GesBinaryBuilder StageFloat(double value, GameEventScriptBytecodeInstructionUnit unit = GameEventScriptBytecodeInstructionUnit.UnitNone)
        => AddOpcode(GameEventScriptBytecodeOpCode.StageFloat, unit, f64: value);

    public GesBinaryBuilder StageText(string text)
        => AddOpcode(GameEventScriptBytecodeOpCode.StageText, x: GesOperand.Text(text));

    public GesBinaryBuilder StageTag(string tag)
        => AddOpcode(GameEventScriptBytecodeOpCode.StageTag, x: GesOperand.Text(tag));

    public GesBinaryBuilder StagePercentage(double ratio)
        => AddOpcode(GameEventScriptBytecodeOpCode.StagePercentage, f64: ratio);

    public GesBinaryBuilder CreateDice(GesRegisterRef destination, short count, short sideCount)
        => AddOpcode(GameEventScriptBytecodeOpCode.CreateDice, dst: GesOperand.Register(destination), y: GesOperand.I16(sideCount), count: count);

    public GesBinaryBuilder CreateVector(GesRegisterRef destination, short componentCount)
        => AddOpcode(GameEventScriptBytecodeOpCode.CreateVector, dst: GesOperand.Register(destination), x: GesOperand.I16(componentCount));

    public GesBinaryBuilder CreatePoint(GesRegisterRef destination, short componentCount)
        => AddOpcode(GameEventScriptBytecodeOpCode.CreatePoint, dst: GesOperand.Register(destination), x: GesOperand.I16(componentCount));

    public GesBinaryBuilder CreateList(GesRegisterRef destination)
        => AddOpcode(GameEventScriptBytecodeOpCode.CreateList, dst: GesOperand.Register(destination));

    public GesBinaryBuilder CreateMap(GesRegisterRef destination, IReadOnlyList<string> keyNames)
        => AddOpcode(GameEventScriptBytecodeOpCode.CreateMap, dst: GesOperand.Register(destination), secondaryList: GesOperand.TextList(keyNames));

    public GesBinaryBuilder CreateRange(GesRegisterRef destination, GesRegisterRef from, GesRegisterRef to)
        => AddOpcode(GameEventScriptBytecodeOpCode.CreateRange, dst: GesOperand.Register(destination), x: GesOperand.Register(from), y: GesOperand.Register(to));

    public GesBinaryBuilder CreateRangeWithStep(GesRegisterRef destination, GesRegisterRef from, GesRegisterRef to, GesRegisterRef step)
        => AddOpcode(GameEventScriptBytecodeOpCode.CreateRangeWithStep, dst: GesOperand.Register(destination), x: GesOperand.Register(from), y: GesOperand.Register(to), a: GesOperand.Register(step));

    public GesBinaryBuilder CreateRangeIterator(GesRegisterRef destination, GesRegisterRef from, GesRegisterRef to)
        => AddOpcode(GameEventScriptBytecodeOpCode.CreateRangeIterator, dst: GesOperand.Register(destination), x: GesOperand.Register(from), y: GesOperand.Register(to));

    public GesBinaryBuilder CreateRangeIteratorWithStep(GesRegisterRef destination, GesRegisterRef from, GesRegisterRef to, GesRegisterRef step)
        => AddOpcode(GameEventScriptBytecodeOpCode.CreateRangeIteratorWithStep, dst: GesOperand.Register(destination), x: GesOperand.Register(from), y: GesOperand.Register(to), a: GesOperand.Register(step));

    public GesBinaryBuilder CreateRangeIteratorShort(GesRegisterRef destination, short from, short to, short step)
        => AddOpcode(GameEventScriptBytecodeOpCode.CreateRangeIteratorShort, dst: GesOperand.Register(destination), x: GesOperand.I16(from), y: GesOperand.I16(to), a: GesOperand.I16(step));

    public GesBinaryBuilder CreateRecord(GesRegisterRef destination, GesBindRef record)
        => AddOpcode(GameEventScriptBytecodeOpCode.CreateRecord, dst: GesOperand.Register(destination), x: GesOperand.Bind(record));

    public GesBinaryBuilder CreateRecordValue(GesRegisterRef destination, GesRegisterRef map, string typeName)
        => AddOpcode(GameEventScriptBytecodeOpCode.CreateRecordValue, dst: GesOperand.Register(destination), x: GesOperand.Register(map), y: GesOperand.Text(typeName));

    public GesBinaryBuilder CreateExternalType(GesRegisterRef destination, GesBindRef externalType, IReadOnlyList<string> argumentNames)
        => AddOpcode(GameEventScriptBytecodeOpCode.CreateExternalType, dst: GesOperand.Register(destination), x: GesOperand.Bind(externalType), y: GesOperand.TextList(argumentNames));

    public GesBinaryBuilder HasValue(GesRegisterRef destination, GesRegisterRef operand)
        => UnaryOpcode(GameEventScriptBytecodeOpCode.HasValue, destination, operand);

    public GesBinaryBuilder IsEmpty(GesRegisterRef destination, GesRegisterRef operand)
        => UnaryOpcode(GameEventScriptBytecodeOpCode.IsEmpty, destination, operand);

    public GesBinaryBuilder Default(GesRegisterRef destination, GesRegisterRef value, GesRegisterRef defaultValue)
        => BinaryOpcode(GameEventScriptBytecodeOpCode.Default, destination, value, defaultValue);

    public GesBinaryBuilder Or(GesRegisterRef destination, GesRegisterRef left, GesRegisterRef right)
        => BinaryOpcode(GameEventScriptBytecodeOpCode.Or, destination, left, right);

    public GesBinaryBuilder And(GesRegisterRef destination, GesRegisterRef left, GesRegisterRef right)
        => BinaryOpcode(GameEventScriptBytecodeOpCode.And, destination, left, right);

    public GesBinaryBuilder Xor(GesRegisterRef destination, GesRegisterRef left, GesRegisterRef right)
        => BinaryOpcode(GameEventScriptBytecodeOpCode.Xor, destination, left, right);

    public GesBinaryBuilder Implies(GesRegisterRef destination, GesRegisterRef left, GesRegisterRef right)
        => BinaryOpcode(GameEventScriptBytecodeOpCode.Implies, destination, left, right);

    public GesBinaryBuilder Not(GesRegisterRef destination, GesRegisterRef operand)
        => UnaryOpcode(GameEventScriptBytecodeOpCode.Not, destination, operand);

    public GesBinaryBuilder Equal(GesRegisterRef destination, GesRegisterRef left, GesRegisterRef right)
        => BinaryOpcode(GameEventScriptBytecodeOpCode.Equal, destination, left, right);

    public GesBinaryBuilder NotEqual(GesRegisterRef destination, GesRegisterRef left, GesRegisterRef right)
        => BinaryOpcode(GameEventScriptBytecodeOpCode.NotEqual, destination, left, right);

    public GesBinaryBuilder Less(GesRegisterRef destination, GesRegisterRef left, GesRegisterRef right)
        => BinaryOpcode(GameEventScriptBytecodeOpCode.Less, destination, left, right);

    public GesBinaryBuilder Greater(GesRegisterRef destination, GesRegisterRef left, GesRegisterRef right)
        => BinaryOpcode(GameEventScriptBytecodeOpCode.Greater, destination, left, right);

    public GesBinaryBuilder LessOrEqual(GesRegisterRef destination, GesRegisterRef left, GesRegisterRef right)
        => BinaryOpcode(GameEventScriptBytecodeOpCode.LessOrEqual, destination, left, right);

    public GesBinaryBuilder GreaterOrEqual(GesRegisterRef destination, GesRegisterRef left, GesRegisterRef right)
        => BinaryOpcode(GameEventScriptBytecodeOpCode.GreaterOrEqual, destination, left, right);

    public GesBinaryBuilder Add(GesRegisterRef destination, GesRegisterRef left, GesRegisterRef right)
        => BinaryOpcode(GameEventScriptBytecodeOpCode.Add, destination, left, right);

    public GesBinaryBuilder Subtract(GesRegisterRef destination, GesRegisterRef left, GesRegisterRef right)
        => BinaryOpcode(GameEventScriptBytecodeOpCode.Subtract, destination, left, right);

    public GesBinaryBuilder Multiply(GesRegisterRef destination, GesRegisterRef left, GesRegisterRef right)
        => BinaryOpcode(GameEventScriptBytecodeOpCode.Multiply, destination, left, right);

    public GesBinaryBuilder Divide(GesRegisterRef destination, GesRegisterRef left, GesRegisterRef right)
        => BinaryOpcode(GameEventScriptBytecodeOpCode.Divide, destination, left, right);

    public GesBinaryBuilder Power(GesRegisterRef destination, GesRegisterRef left, GesRegisterRef right)
        => BinaryOpcode(GameEventScriptBytecodeOpCode.Power, destination, left, right);

    public GesBinaryBuilder IntegerDivide(GesRegisterRef destination, GesRegisterRef left, GesRegisterRef right)
        => BinaryOpcode(GameEventScriptBytecodeOpCode.IntegerDivide, destination, left, right);

    public GesBinaryBuilder Modulo(GesRegisterRef destination, GesRegisterRef left, GesRegisterRef right)
        => BinaryOpcode(GameEventScriptBytecodeOpCode.Modulo, destination, left, right);

    public GesBinaryBuilder Remainder(GesRegisterRef destination, GesRegisterRef left, GesRegisterRef right)
        => BinaryOpcode(GameEventScriptBytecodeOpCode.Remainder, destination, left, right);

    public GesBinaryBuilder Min(GesRegisterRef destination, GesRegisterRef left, GesRegisterRef right)
        => BinaryOpcode(GameEventScriptBytecodeOpCode.Min, destination, left, right);

    public GesBinaryBuilder Max(GesRegisterRef destination, GesRegisterRef left, GesRegisterRef right)
        => BinaryOpcode(GameEventScriptBytecodeOpCode.Max, destination, left, right);

    public GesBinaryBuilder Negate(GesRegisterRef destination, GesRegisterRef operand)
        => UnaryOpcode(GameEventScriptBytecodeOpCode.Negate, destination, operand);

    public GesBinaryBuilder Abs(GesRegisterRef destination, GesRegisterRef operand)
        => UnaryOpcode(GameEventScriptBytecodeOpCode.Abs, destination, operand);

    public GesBinaryBuilder LogN(GesRegisterRef destination, GesRegisterRef operand)
        => UnaryOpcode(GameEventScriptBytecodeOpCode.LogN, destination, operand);

    public GesBinaryBuilder Chance(GesRegisterRef destination, GesRegisterRef operand)
        => UnaryOpcode(GameEventScriptBytecodeOpCode.Chance, destination, operand);

    public GesBinaryBuilder Exp(GesRegisterRef destination, GesRegisterRef operand)
        => UnaryOpcode(GameEventScriptBytecodeOpCode.Exp, destination, operand);

    public GesBinaryBuilder Floor(GesRegisterRef destination, GesRegisterRef operand)
        => UnaryOpcode(GameEventScriptBytecodeOpCode.Floor, destination, operand);

    public GesBinaryBuilder Ceil(GesRegisterRef destination, GesRegisterRef operand)
        => UnaryOpcode(GameEventScriptBytecodeOpCode.Ceil, destination, operand);

    public GesBinaryBuilder Truncate(GesRegisterRef destination, GesRegisterRef operand)
        => UnaryOpcode(GameEventScriptBytecodeOpCode.Truncate, destination, operand);

    public GesBinaryBuilder RoundHalfEven(GesRegisterRef destination, GesRegisterRef operand)
        => UnaryOpcode(GameEventScriptBytecodeOpCode.RoundHalfEven, destination, operand);

    public GesBinaryBuilder RoundHalfUp(GesRegisterRef destination, GesRegisterRef operand)
        => UnaryOpcode(GameEventScriptBytecodeOpCode.RoundHalfUp, destination, operand);

    public GesBinaryBuilder RoundHalfDown(GesRegisterRef destination, GesRegisterRef operand)
        => UnaryOpcode(GameEventScriptBytecodeOpCode.RoundHalfDown, destination, operand);

    public GesBinaryBuilder DegreeToRadians(GesRegisterRef destination, GesRegisterRef operand)
        => UnaryOpcode(GameEventScriptBytecodeOpCode.DegreeToRadians, destination, operand);

    public GesBinaryBuilder DegreeFromRadians(GesRegisterRef destination, GesRegisterRef operand)
        => UnaryOpcode(GameEventScriptBytecodeOpCode.DegreeFromRadians, destination, operand);

    public GesBinaryBuilder WrapDegree(GesRegisterRef destination, GesRegisterRef operand)
        => UnaryOpcode(GameEventScriptBytecodeOpCode.WrapDegree, destination, operand);

    public GesBinaryBuilder Sin(GesRegisterRef destination, GesRegisterRef operand)
        => UnaryOpcode(GameEventScriptBytecodeOpCode.Sin, destination, operand);

    public GesBinaryBuilder Cos(GesRegisterRef destination, GesRegisterRef operand)
        => UnaryOpcode(GameEventScriptBytecodeOpCode.Cos, destination, operand);

    public GesBinaryBuilder Tan(GesRegisterRef destination, GesRegisterRef operand)
        => UnaryOpcode(GameEventScriptBytecodeOpCode.Tan, destination, operand);

    public GesBinaryBuilder Asin(GesRegisterRef destination, GesRegisterRef operand)
        => UnaryOpcode(GameEventScriptBytecodeOpCode.Asin, destination, operand);

    public GesBinaryBuilder Acos(GesRegisterRef destination, GesRegisterRef operand)
        => UnaryOpcode(GameEventScriptBytecodeOpCode.Acos, destination, operand);

    public GesBinaryBuilder Atan(GesRegisterRef destination, GesRegisterRef operand)
        => UnaryOpcode(GameEventScriptBytecodeOpCode.Atan, destination, operand);

    public GesBinaryBuilder Atan2(GesRegisterRef destination, GesRegisterRef y, GesRegisterRef x)
        => BinaryOpcode(GameEventScriptBytecodeOpCode.Atan2, destination, y, x);

    public GesBinaryBuilder Hypot2D(GesRegisterRef destination, GesRegisterRef x, GesRegisterRef y)
        => BinaryOpcode(GameEventScriptBytecodeOpCode.Hypot2D, destination, x, y);

    public GesBinaryBuilder Hypot3D(GesRegisterRef destination, GesRegisterRef x, GesRegisterRef y, GesRegisterRef z)
        => AddOpcode(GameEventScriptBytecodeOpCode.Hypot3D, dst: GesOperand.Register(destination), x: GesOperand.Register(x), y: GesOperand.Register(y), a: GesOperand.Register(z));

    public GesBinaryBuilder Distance(GesRegisterRef destination, GesRegisterRef left, GesRegisterRef right)
        => BinaryOpcode(GameEventScriptBytecodeOpCode.Distance, destination, left, right);

    public GesBinaryBuilder Distance2D(GesRegisterRef destination, GesRegisterRef x1, GesRegisterRef y1, GesRegisterRef x2, GesRegisterRef y2)
        => AddOpcode(GameEventScriptBytecodeOpCode.Distance2D, dst: GesOperand.Register(destination), x: GesOperand.Register(x1), y: GesOperand.Register(y1), a: GesOperand.Register(x2), b: GesOperand.Register(y2));

    public GesBinaryBuilder Distance3D(GesRegisterRef destination, GesRegisterRef x1, GesRegisterRef y1, GesRegisterRef z1, GesRegisterRef x2, GesRegisterRef y2, GesRegisterRef z2)
        => AddOpcode(GameEventScriptBytecodeOpCode.Distance3D, dst: GesOperand.Register(destination), x: GesOperand.Register(x1), y: GesOperand.Register(y1), a: GesOperand.Register(z1), b: GesOperand.Register(x2), c: GesOperand.Register(y2), d: GesOperand.Register(z2));

    public GesBinaryBuilder DistanceSquared(GesRegisterRef destination, GesRegisterRef left, GesRegisterRef right)
        => BinaryOpcode(GameEventScriptBytecodeOpCode.DistanceSquared, destination, left, right);

    public GesBinaryBuilder DistanceSquared2D(GesRegisterRef destination, GesRegisterRef x1, GesRegisterRef y1, GesRegisterRef x2, GesRegisterRef y2)
        => AddOpcode(GameEventScriptBytecodeOpCode.DistanceSquared2D, dst: GesOperand.Register(destination), x: GesOperand.Register(x1), y: GesOperand.Register(y1), a: GesOperand.Register(x2), b: GesOperand.Register(y2));

    public GesBinaryBuilder DistanceSquared3D(GesRegisterRef destination, GesRegisterRef x1, GesRegisterRef y1, GesRegisterRef z1, GesRegisterRef x2, GesRegisterRef y2, GesRegisterRef z2)
        => AddOpcode(GameEventScriptBytecodeOpCode.DistanceSquared3D, dst: GesOperand.Register(destination), x: GesOperand.Register(x1), y: GesOperand.Register(y1), a: GesOperand.Register(z1), b: GesOperand.Register(x2), c: GesOperand.Register(y2), d: GesOperand.Register(z2));

    public GesBinaryBuilder LengthSquared(GesRegisterRef destination, GesRegisterRef operand)
        => UnaryOpcode(GameEventScriptBytecodeOpCode.LengthSquared, destination, operand);

    public GesBinaryBuilder LengthSquared2D(GesRegisterRef destination, GesRegisterRef x, GesRegisterRef y)
        => BinaryOpcode(GameEventScriptBytecodeOpCode.LengthSquared2D, destination, x, y);

    public GesBinaryBuilder LengthSquared3D(GesRegisterRef destination, GesRegisterRef x, GesRegisterRef y, GesRegisterRef z)
        => AddOpcode(GameEventScriptBytecodeOpCode.LengthSquared3D, dst: GesOperand.Register(destination), x: GesOperand.Register(x), y: GesOperand.Register(y), a: GesOperand.Register(z));

    public GesBinaryBuilder Normalize(GesRegisterRef destination, GesRegisterRef operand)
        => UnaryOpcode(GameEventScriptBytecodeOpCode.Normalize, destination, operand);

    public GesBinaryBuilder Normalize2D(GesRegisterRef destination, GesRegisterRef x, GesRegisterRef y)
        => BinaryOpcode(GameEventScriptBytecodeOpCode.Normalize2D, destination, x, y);

    public GesBinaryBuilder Normalize3D(GesRegisterRef destination, GesRegisterRef x, GesRegisterRef y, GesRegisterRef z)
        => AddOpcode(GameEventScriptBytecodeOpCode.Normalize3D, dst: GesOperand.Register(destination), x: GesOperand.Register(x), y: GesOperand.Register(y), a: GesOperand.Register(z));

    public GesBinaryBuilder Dot(GesRegisterRef destination, GesRegisterRef left, GesRegisterRef right)
        => BinaryOpcode(GameEventScriptBytecodeOpCode.Dot, destination, left, right);

    public GesBinaryBuilder Dot2D(GesRegisterRef destination, GesRegisterRef x1, GesRegisterRef y1, GesRegisterRef x2, GesRegisterRef y2)
        => AddOpcode(GameEventScriptBytecodeOpCode.Dot2D, dst: GesOperand.Register(destination), x: GesOperand.Register(x1), y: GesOperand.Register(y1), a: GesOperand.Register(x2), b: GesOperand.Register(y2));

    public GesBinaryBuilder Dot3D(GesRegisterRef destination, GesRegisterRef x1, GesRegisterRef y1, GesRegisterRef z1, GesRegisterRef x2, GesRegisterRef y2, GesRegisterRef z2)
        => AddOpcode(GameEventScriptBytecodeOpCode.Dot3D, dst: GesOperand.Register(destination), x: GesOperand.Register(x1), y: GesOperand.Register(y1), a: GesOperand.Register(z1), b: GesOperand.Register(x2), c: GesOperand.Register(y2), d: GesOperand.Register(z2));

    public GesBinaryBuilder Cross(GesRegisterRef destination, GesRegisterRef left, GesRegisterRef right)
        => BinaryOpcode(GameEventScriptBytecodeOpCode.Cross, destination, left, right);

    public GesBinaryBuilder Cross2D(GesRegisterRef destination, GesRegisterRef x1, GesRegisterRef y1, GesRegisterRef x2, GesRegisterRef y2)
        => AddOpcode(GameEventScriptBytecodeOpCode.Cross2D, dst: GesOperand.Register(destination), x: GesOperand.Register(x1), y: GesOperand.Register(y1), a: GesOperand.Register(x2), b: GesOperand.Register(y2));

    public GesBinaryBuilder Cross3D(GesRegisterRef destination, GesRegisterRef x1, GesRegisterRef y1, GesRegisterRef z1, GesRegisterRef x2, GesRegisterRef y2, GesRegisterRef z2)
        => AddOpcode(GameEventScriptBytecodeOpCode.Cross3D, dst: GesOperand.Register(destination), x: GesOperand.Register(x1), y: GesOperand.Register(y1), a: GesOperand.Register(z1), b: GesOperand.Register(x2), c: GesOperand.Register(y2), d: GesOperand.Register(z2));

    public GesBinaryBuilder AngleBetween(GesRegisterRef destination, GesRegisterRef left, GesRegisterRef right)
        => BinaryOpcode(GameEventScriptBytecodeOpCode.AngleBetween, destination, left, right);

    public GesBinaryBuilder AngleBetween2D(GesRegisterRef destination, GesRegisterRef x1, GesRegisterRef y1, GesRegisterRef x2, GesRegisterRef y2)
        => AddOpcode(GameEventScriptBytecodeOpCode.AngleBetween2D, dst: GesOperand.Register(destination), x: GesOperand.Register(x1), y: GesOperand.Register(y1), a: GesOperand.Register(x2), b: GesOperand.Register(y2));

    public GesBinaryBuilder AngleBetween3D(GesRegisterRef destination, GesRegisterRef x1, GesRegisterRef y1, GesRegisterRef z1, GesRegisterRef x2, GesRegisterRef y2, GesRegisterRef z2)
        => AddOpcode(GameEventScriptBytecodeOpCode.AngleBetween3D, dst: GesOperand.Register(destination), x: GesOperand.Register(x1), y: GesOperand.Register(y1), a: GesOperand.Register(z1), b: GesOperand.Register(x2), c: GesOperand.Register(y2), d: GesOperand.Register(z2));

    public GesBinaryBuilder Clamp(GesRegisterRef destination, GesRegisterRef value, GesRegisterRef minimum, GesRegisterRef maximum)
        => AddOpcode(GameEventScriptBytecodeOpCode.Clamp, dst: GesOperand.Register(destination), x: GesOperand.Register(value), y: GesOperand.Register(minimum), a: GesOperand.Register(maximum));

    public GesBinaryBuilder RandomTake(GesRegisterRef destination, GesRegisterRef from, GesRegisterRef to)
        => BinaryOpcode(GameEventScriptBytecodeOpCode.RandomTake, destination, from, to);

    public GesBinaryBuilder RandomTakeFloat(GesRegisterRef destination, GesRegisterRef from, GesRegisterRef to)
        => BinaryOpcode(GameEventScriptBytecodeOpCode.RandomTakeFloat, destination, from, to);

    public GesBinaryBuilder RandomPush(GesRegisterRef seed)
        => AddOpcode(GameEventScriptBytecodeOpCode.RandomPush, x: GesOperand.Register(seed));

    public GesBinaryBuilder RandomPushConstant(long seed)
        => AddOpcode(GameEventScriptBytecodeOpCode.RandomPushConstant, i64: seed);

    public GesBinaryBuilder RandomPop()
        => AddOpcode(GameEventScriptBytecodeOpCode.RandomPop);

    public GesBinaryBuilder Term(GesRegisterRef destination, GesRegisterRef series, GesRegisterRef index)
        => BinaryOpcode(GameEventScriptBytecodeOpCode.Term, destination, series, index);

    public GesBinaryBuilder TakeFirst(GesRegisterRef destination, GesRegisterRef source, short count)
        => CountOpcode(GameEventScriptBytecodeOpCode.TakeFirst, destination, source, count);

    public GesBinaryBuilder DropFirst(GesRegisterRef destination, GesRegisterRef source, short count)
        => CountOpcode(GameEventScriptBytecodeOpCode.DropFirst, destination, source, count);

    public GesBinaryBuilder TakeLast(GesRegisterRef destination, GesRegisterRef source, short count)
        => CountOpcode(GameEventScriptBytecodeOpCode.TakeLast, destination, source, count);

    public GesBinaryBuilder DropLast(GesRegisterRef destination, GesRegisterRef source, short count)
        => CountOpcode(GameEventScriptBytecodeOpCode.DropLast, destination, source, count);

    public GesBinaryBuilder TakeHighest(GesRegisterRef destination, GesRegisterRef source, short count)
        => CountOpcode(GameEventScriptBytecodeOpCode.TakeHighest, destination, source, count);

    public GesBinaryBuilder TakeLowest(GesRegisterRef destination, GesRegisterRef source, short count)
        => CountOpcode(GameEventScriptBytecodeOpCode.TakeLowest, destination, source, count);

    public GesBinaryBuilder DropHighest(GesRegisterRef destination, GesRegisterRef source, short count)
        => CountOpcode(GameEventScriptBytecodeOpCode.DropHighest, destination, source, count);

    public GesBinaryBuilder DropLowest(GesRegisterRef destination, GesRegisterRef source, short count)
        => CountOpcode(GameEventScriptBytecodeOpCode.DropLowest, destination, source, count);

    public GesBinaryBuilder OneRandom(GesRegisterRef destination, GesRegisterRef source)
        => UnaryOpcode(GameEventScriptBytecodeOpCode.OneRandom, destination, source);

    public GesBinaryBuilder TakeRandom(GesRegisterRef destination, GesRegisterRef source, short count)
        => CountOpcode(GameEventScriptBytecodeOpCode.TakeRandom, destination, source, count);

    public GesBinaryBuilder OneWeighted(GesRegisterRef destination, GesRegisterRef items, GesRegisterRef weights)
        => BinaryOpcode(GameEventScriptBytecodeOpCode.OneWeighted, destination, items, weights);

    public GesBinaryBuilder TakeWeighted(GesRegisterRef destination, GesRegisterRef items, GesRegisterRef weights, short count)
        => AddOpcode(GameEventScriptBytecodeOpCode.TakeWeighted, dst: GesOperand.Register(destination), x: GesOperand.Register(items), y: GesOperand.I16(count), a: GesOperand.Register(weights));

    public GesBinaryBuilder Count(GesRegisterRef destination, GesRegisterRef source)
        => UnaryOpcode(GameEventScriptBytecodeOpCode.Count, destination, source);

    public GesBinaryBuilder StartsWith(GesRegisterRef destination, GesRegisterRef left, GesRegisterRef right)
        => BinaryOpcode(GameEventScriptBytecodeOpCode.StartsWith, destination, left, right);

    public GesBinaryBuilder EndsWith(GesRegisterRef destination, GesRegisterRef left, GesRegisterRef right)
        => BinaryOpcode(GameEventScriptBytecodeOpCode.EndsWith, destination, left, right);

    public GesBinaryBuilder Contains(GesRegisterRef destination, GesRegisterRef left, GesRegisterRef right)
        => BinaryOpcode(GameEventScriptBytecodeOpCode.Contains, destination, left, right);

    public GesBinaryBuilder ContainsAny(GesRegisterRef destination, GesRegisterRef left, GesRegisterRef right)
        => BinaryOpcode(GameEventScriptBytecodeOpCode.ContainsAny, destination, left, right);

    public GesBinaryBuilder ContainsAll(GesRegisterRef destination, GesRegisterRef left, GesRegisterRef right)
        => BinaryOpcode(GameEventScriptBytecodeOpCode.ContainsAll, destination, left, right);

    public GesBinaryBuilder HasAny(GesRegisterRef destination, GesRegisterRef source)
        => UnaryOpcode(GameEventScriptBytecodeOpCode.HasAny, destination, source);

    public GesBinaryBuilder HasAll(GesRegisterRef destination, GesRegisterRef source)
        => UnaryOpcode(GameEventScriptBytecodeOpCode.HasAll, destination, source);

    public GesBinaryBuilder ContainsValue(GesRegisterRef destination, GesRegisterRef left, GesRegisterRef right)
        => BinaryOpcode(GameEventScriptBytecodeOpCode.ContainsValue, destination, left, right);

    public GesBinaryBuilder Union(GesRegisterRef destination, GesRegisterRef left, GesRegisterRef right)
        => BinaryOpcode(GameEventScriptBytecodeOpCode.Union, destination, left, right);

    public GesBinaryBuilder Intersect(GesRegisterRef destination, GesRegisterRef left, GesRegisterRef right)
        => BinaryOpcode(GameEventScriptBytecodeOpCode.Intersect, destination, left, right);

    public GesBinaryBuilder Zip(GesRegisterRef destination, GesRegisterRef left, GesRegisterRef right)
        => BinaryOpcode(GameEventScriptBytecodeOpCode.Zip, destination, left, right);

    public GesBinaryBuilder KeysOfMap(GesRegisterRef destination, GesRegisterRef operand)
        => UnaryOpcode(GameEventScriptBytecodeOpCode.KeysOfMap, destination, operand);

    public GesBinaryBuilder ValuesOfMap(GesRegisterRef destination, GesRegisterRef operand)
        => UnaryOpcode(GameEventScriptBytecodeOpCode.ValuesOfMap, destination, operand);

    public GesBinaryBuilder EntriesOfMap(GesRegisterRef destination, GesRegisterRef operand)
        => UnaryOpcode(GameEventScriptBytecodeOpCode.EntriesOfMap, destination, operand);

    public GesBinaryBuilder First(GesRegisterRef destination, GesRegisterRef operand)
        => UnaryOpcode(GameEventScriptBytecodeOpCode.First, destination, operand);

    public GesBinaryBuilder Last(GesRegisterRef destination, GesRegisterRef operand)
        => UnaryOpcode(GameEventScriptBytecodeOpCode.Last, destination, operand);

    public GesBinaryBuilder Single(GesRegisterRef destination, GesRegisterRef operand)
        => UnaryOpcode(GameEventScriptBytecodeOpCode.Single, destination, operand);

    public GesBinaryBuilder IteratorCreate(GesRegisterRef destination, GesRegisterRef collection)
        => UnaryOpcode(GameEventScriptBytecodeOpCode.IteratorCreate, destination, collection);

    public GesBinaryBuilder IteratorCreateOrJump(GesRegisterRef destination, GesRegisterRef collection, GesLabelRef failureTarget)
        => AddOpcode(GameEventScriptBytecodeOpCode.IteratorCreateOrJump, dst: GesOperand.Register(destination), x: GesOperand.Register(collection), y: GesOperand.Label(failureTarget));

    public GesBinaryBuilder IteratorNext(GesRegisterRef destination, GesRegisterRef iterator, GesLabelRef exhaustedTarget)
        => AddOpcode(GameEventScriptBytecodeOpCode.IteratorNext, dst: GesOperand.Register(destination), x: GesOperand.Register(iterator), y: GesOperand.Label(exhaustedTarget));

    public GesBinaryBuilder IteratorClose(GesRegisterRef iterator)
        => AddOpcode(GameEventScriptBytecodeOpCode.IteratorClose, x: GesOperand.Register(iterator));

    public GesBinaryBuilder Distinct(GesRegisterRef destination, GesRegisterRef source)
        => UnaryOpcode(GameEventScriptBytecodeOpCode.Distinct, destination, source);

    public GesBinaryBuilder SortAscending(GesRegisterRef destination, GesRegisterRef source)
        => UnaryOpcode(GameEventScriptBytecodeOpCode.SortAscending, destination, source);

    public GesBinaryBuilder SortDescending(GesRegisterRef destination, GesRegisterRef source)
        => UnaryOpcode(GameEventScriptBytecodeOpCode.SortDescending, destination, source);

    public GesBinaryBuilder Reverse(GesRegisterRef destination, GesRegisterRef source)
        => UnaryOpcode(GameEventScriptBytecodeOpCode.Reverse, destination, source);

    public GesBinaryBuilder Shuffle(GesRegisterRef destination, GesRegisterRef source)
        => UnaryOpcode(GameEventScriptBytecodeOpCode.Shuffle, destination, source);

    public GesBinaryBuilder ListBuilderCreate(GesRegisterRef destination)
        => AddOpcode(GameEventScriptBytecodeOpCode.ListBuilderCreate, dst: GesOperand.Register(destination));

    public GesBinaryBuilder ListBuilderAdd(GesRegisterRef builder, GesRegisterRef item)
        => AddOpcode(GameEventScriptBytecodeOpCode.ListBuilderAdd, x: GesOperand.Register(builder), y: GesOperand.Register(item));

    public GesBinaryBuilder ListBuilderFinish(GesRegisterRef destination, GesRegisterRef builder)
        => AddOpcode(GameEventScriptBytecodeOpCode.ListBuilderFinish, dst: GesOperand.Register(destination), x: GesOperand.Register(builder));

    public GesBinaryBuilder MapBuilderCreate(GesRegisterRef destination)
        => AddOpcode(GameEventScriptBytecodeOpCode.MapBuilderCreate, dst: GesOperand.Register(destination));

    public GesBinaryBuilder MapBuilderAdd(GesRegisterRef builder, GesRegisterRef key, GesRegisterRef value)
        => AddOpcode(GameEventScriptBytecodeOpCode.MapBuilderAdd, x: GesOperand.Register(builder), y: GesOperand.Register(key), a: GesOperand.Register(value));

    public GesBinaryBuilder MapBuilderFinish(GesRegisterRef destination, GesRegisterRef builder)
        => AddOpcode(GameEventScriptBytecodeOpCode.MapBuilderFinish, dst: GesOperand.Register(destination), x: GesOperand.Register(builder));

    public GesBinaryBuilder DistinctBuilderCreate(GesRegisterRef destination)
        => AddOpcode(GameEventScriptBytecodeOpCode.DistinctBuilderCreate, dst: GesOperand.Register(destination));

    public GesBinaryBuilder DistinctBuilderAdd(GesRegisterRef builder, GesRegisterRef key, GesRegisterRef value)
        => AddOpcode(GameEventScriptBytecodeOpCode.DistinctBuilderAdd, x: GesOperand.Register(builder), y: GesOperand.Register(key), a: GesOperand.Register(value));

    public GesBinaryBuilder DistinctBuilderFinish(GesRegisterRef destination, GesRegisterRef builder)
        => AddOpcode(GameEventScriptBytecodeOpCode.DistinctBuilderFinish, dst: GesOperand.Register(destination), x: GesOperand.Register(builder));

    public GesBinaryBuilder GroupBuilderCreate(GesRegisterRef destination)
        => AddOpcode(GameEventScriptBytecodeOpCode.GroupBuilderCreate, dst: GesOperand.Register(destination));

    public GesBinaryBuilder GroupBuilderAdd(GesRegisterRef builder, GesRegisterRef key, GesRegisterRef value)
        => AddOpcode(GameEventScriptBytecodeOpCode.GroupBuilderAdd, x: GesOperand.Register(builder), y: GesOperand.Register(key), a: GesOperand.Register(value));

    public GesBinaryBuilder GroupBuilderFinish(GesRegisterRef destination, GesRegisterRef builder)
        => AddOpcode(GameEventScriptBytecodeOpCode.GroupBuilderFinish, dst: GesOperand.Register(destination), x: GesOperand.Register(builder));

    public GesBinaryBuilder OrderBuilderCreate(GesRegisterRef destination)
        => AddOpcode(GameEventScriptBytecodeOpCode.OrderBuilderCreate, dst: GesOperand.Register(destination));

    public GesBinaryBuilder OrderBuilderAdd(GesRegisterRef builder, GesRegisterRef key, GesRegisterRef value)
        => AddOpcode(GameEventScriptBytecodeOpCode.OrderBuilderAdd, x: GesOperand.Register(builder), y: GesOperand.Register(key), a: GesOperand.Register(value));

    public GesBinaryBuilder OrderBuilderFinishAscending(GesRegisterRef destination, GesRegisterRef builder)
        => AddOpcode(GameEventScriptBytecodeOpCode.OrderBuilderFinishAscending, dst: GesOperand.Register(destination), x: GesOperand.Register(builder));

    public GesBinaryBuilder OrderBuilderFinishDescending(GesRegisterRef destination, GesRegisterRef builder)
        => AddOpcode(GameEventScriptBytecodeOpCode.OrderBuilderFinishDescending, dst: GesOperand.Register(destination), x: GesOperand.Register(builder));

    public GesBinaryBuilder HasPattern(GesRegisterRef destination, GesRegisterRef source, GameEventScriptBytecodePatternKind pattern, short count = 0, GesRegisterRef? face = null)
        => PatternOpcode(GameEventScriptBytecodeOpCode.HasPattern, destination, source, pattern, count, face);

    public GesBinaryBuilder TakePattern(GesRegisterRef destination, GesRegisterRef source, GameEventScriptBytecodePatternKind pattern, short count = 0, GesRegisterRef? face = null)
        => PatternOpcode(GameEventScriptBytecodeOpCode.TakePattern, destination, source, pattern, count, face);

    public GesBinaryBuilder LoadBoolean(GesRegisterRef destination, bool value)
        => value ? LoadTrue(destination) : LoadFalse(destination);

    public GesBinaryBuilder StageBoolean(bool value)
        => value ? StageTrue() : StageFalse();

    private GesBinaryBuilder UnaryOpcode(GameEventScriptBytecodeOpCode opcode, GesRegisterRef destination, GesRegisterRef operand)
        => AddOpcode(opcode, dst: GesOperand.Register(destination), x: GesOperand.Register(operand));

    private GesBinaryBuilder BinaryOpcode(GameEventScriptBytecodeOpCode opcode, GesRegisterRef destination, GesRegisterRef left, GesRegisterRef right)
        => AddOpcode(opcode, dst: GesOperand.Register(destination), x: GesOperand.Register(left), y: GesOperand.Register(right));

    private GesBinaryBuilder CountOpcode(GameEventScriptBytecodeOpCode opcode, GesRegisterRef destination, GesRegisterRef source, short count)
        => AddOpcode(opcode, dst: GesOperand.Register(destination), x: GesOperand.Register(source), y: GesOperand.I16(count));

    private GesBinaryBuilder SelectorOpcode(GameEventScriptBytecodeOpCode opcode, GesRegisterRef destination, GesRegisterRef source, GesRegisterRef itemBinding, GesLabelRef keyEntry)
        => AddOpcode(opcode, dst: GesOperand.Register(destination), x: GesOperand.Register(source), y: GesOperand.Register(itemBinding), a: GesOperand.Label(keyEntry));

    private GesBinaryBuilder PatternOpcode(GameEventScriptBytecodeOpCode opcode, GesRegisterRef destination, GesRegisterRef source, GameEventScriptBytecodePatternKind pattern, short count, GesRegisterRef? face)
        => AddOpcode(opcode, dst: GesOperand.Register(destination), x: GesOperand.Register(source), y: GesOperand.I16(count), a: GesOperand.U16((ushort)pattern), b: face.HasValue ? GesOperand.Register(face.Value) : default);
}
