using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using StepH.GameEventScript.Api;
using StepH.GameEventScript.Runtime;
using StepH.GameEventScript.Types;
using static StepH.GameEventScript.Api.GameEventScriptBinaryBindTable;

namespace StepH.GameEventScript.BytecodeVM;

internal sealed class GesBinaryVmRunner
{
    private readonly GameEventScriptBinary _binary;
    private readonly GameEventScriptBytecodeInstruction[] _code;
    private readonly int _frameSlotCount;

    public GesBinaryVmRunner(GameEventScriptBinary binary)
    {
        _binary = binary;
        _code = binary.InstructionTable ?? [];
        _frameSlotCount = Math.Max(1, ComputeFrameSlotCount(_code));
    }

    public bool RunHandler(GameEventScriptMessage message, GameEventScriptContext context)
    {
        if (!TryLookupHandler(message, out var handler))
        {
            return false;
        }

        var state = new GesBinaryVmRunState(_binary, _code, _frameSlotCount, context, handler, message);
        return state.Run();
    }

    private bool TryLookupHandler(GameEventScriptMessage message, out GameEventScriptBinaryBindEntry handler)
    {
        foreach (var entry in _binary.BindTable.Entries)
        {
            if (entry.Kind != GameEventScriptBinaryBindKind.MessageHandler ||
                !string.Equals(_binary.TextConstantTable.Resolve(entry.Name), message.Name, StringComparison.Ordinal) ||
                entry.ArgumentNames.Count != message.Arguments.SignatureLabels.Count)
            {
                continue;
            }

            var matches = true;
            for (var index = 0; index < entry.ArgumentNames.Count; index++)
            {
                if (!string.Equals(
                        _binary.TextConstantTable.Resolve(entry.ArgumentNames[index]),
                        message.Arguments.SignatureLabels[index],
                        StringComparison.Ordinal))
                {
                    matches = false;
                    break;
                }
            }

            if (!matches)
            {
                continue;
            }

            handler = entry;
            return true;
        }

        handler = default;
        return false;
    }

    private static int ComputeFrameSlotCount(ReadOnlySpan<GameEventScriptBytecodeInstruction> code)
    {
        var maxSlot = 0;
        foreach (var instruction in code)
        {
            maxSlot = Math.Max(maxSlot, instruction.DestinationSlot);
            switch (instruction.OpCode)
            {
                case GameEventScriptBytecodeOpCode.MoveSlot:
                case GameEventScriptBytecodeOpCode.ReturnValue:
                case GameEventScriptBytecodeOpCode.Cast:
                case GameEventScriptBytecodeOpCode.CastCustom:
                case GameEventScriptBytecodeOpCode.CastUnit:
                case GameEventScriptBytecodeOpCode.CastNumeric:
                case GameEventScriptBytecodeOpCode.TypeCheck:
                case GameEventScriptBytecodeOpCode.TypeCheckNumeric:
                case GameEventScriptBytecodeOpCode.TypeCheckCustom:
                case GameEventScriptBytecodeOpCode.CheckUnit:
                    maxSlot = Math.Max(maxSlot, instruction.XSlot);
                    break;

                case GameEventScriptBytecodeOpCode.SlotLocals:
                    if (instruction.Count > 0)
                    {
                        maxSlot = Math.Max(maxSlot, instruction.Count - 1);
                    }

                    break;

                case GameEventScriptBytecodeOpCode.Add:
                case GameEventScriptBytecodeOpCode.Subtract:
                case GameEventScriptBytecodeOpCode.Multiply:
                case GameEventScriptBytecodeOpCode.Divide:
                case GameEventScriptBytecodeOpCode.Equal:
                case GameEventScriptBytecodeOpCode.NotEqual:
                case GameEventScriptBytecodeOpCode.Greater:
                case GameEventScriptBytecodeOpCode.GreaterOrEqual:
                case GameEventScriptBytecodeOpCode.Less:
                case GameEventScriptBytecodeOpCode.LessOrEqual:
                case GameEventScriptBytecodeOpCode.Min:
                case GameEventScriptBytecodeOpCode.Max:
                case GameEventScriptBytecodeOpCode.IntAdd:
                case GameEventScriptBytecodeOpCode.IntSubtract:
                case GameEventScriptBytecodeOpCode.IntMultiply:
                case GameEventScriptBytecodeOpCode.IntDivide:
                case GameEventScriptBytecodeOpCode.IntGreater:
                case GameEventScriptBytecodeOpCode.IntGreaterOrEqual:
                case GameEventScriptBytecodeOpCode.IntLess:
                case GameEventScriptBytecodeOpCode.IntLessOrEqual:
                case GameEventScriptBytecodeOpCode.IntEqual:
                case GameEventScriptBytecodeOpCode.IntNotEqual:
                    maxSlot = Math.Max(maxSlot, Math.Max(instruction.XSlot, instruction.YSlot));
                    break;

                case GameEventScriptBytecodeOpCode.JumpIfTrue:
                case GameEventScriptBytecodeOpCode.JumpIfFalse:
                case GameEventScriptBytecodeOpCode.JumpIfNotTrue:
                    maxSlot = Math.Max(maxSlot, instruction.ConditionSlot);
                    break;
            }
        }

        return checked(maxSlot + 1);
    }
}

internal sealed class GesBinaryVmRunState
{
    private readonly GameEventScriptBinary _binary;
    private readonly GameEventScriptBytecodeInstruction[] _code;
    private readonly GameEventScriptContext _context;
    private readonly GameEventScriptBinaryBindEntry _handler;
    private readonly GameEventScriptMessage _message;
    private readonly GesBinaryVmValue[] _slots;
    private int _pc;
    private bool _running;

    public GesBinaryVmRunState(
        GameEventScriptBinary binary,
        GameEventScriptBytecodeInstruction[] code,
        int frameSlotCount,
        GameEventScriptContext context,
        GameEventScriptBinaryBindEntry handler,
        GameEventScriptMessage message)
    {
        _binary = binary;
        _code = code;
        _context = context;
        _handler = handler;
        _message = message;
        _slots = new GesBinaryVmValue[Math.Max(1, frameSlotCount)];
        _pc = checked((int)handler.EntryAddress);
        BindHandlerArguments();
    }

    public bool Run()
    {
        if ((uint)_pc >= (uint)_code.Length)
        {
            return false;
        }

        _running = true;
        while (_running && (uint)_pc < (uint)_code.Length)
        {
            var instruction = _code[_pc++];
            switch (instruction.OpCode)
            {
                case GameEventScriptBytecodeOpCode.Nop:
                case GameEventScriptBytecodeOpCode.SlotLocals:
                    break;

                case GameEventScriptBytecodeOpCode.MoveSlot:
                    Set(instruction.DestinationSlot, Get(instruction.XSlot));
                    break;

                case GameEventScriptBytecodeOpCode.LoadNothing:
                    Set(instruction.DestinationSlot, GesBinaryVmValue.Nothing);
                    break;

                case GameEventScriptBytecodeOpCode.LoadTrue:
                    Set(instruction.DestinationSlot, GesBinaryVmValue.Boolean(true));
                    break;

                case GameEventScriptBytecodeOpCode.LoadFalse:
                    Set(instruction.DestinationSlot, GesBinaryVmValue.Boolean(false));
                    break;

                case GameEventScriptBytecodeOpCode.LoadInteger:
                    Set(instruction.DestinationSlot, GesBinaryVmValue.Integer(instruction.I64, DecodeUnit(instruction.UnitAndFlags)));
                    break;

                case GameEventScriptBytecodeOpCode.LoadFloat:
                    Set(instruction.DestinationSlot, GesBinaryVmValue.Float(instruction.F64, DecodeUnit(instruction.UnitAndFlags)));
                    break;

                case GameEventScriptBytecodeOpCode.LoadPercentage:
                    Set(instruction.DestinationSlot, GesBinaryVmValue.Percentage(instruction.F64));
                    break;

                case GameEventScriptBytecodeOpCode.LoadText:
                    Set(instruction.DestinationSlot, GesBinaryVmValue.Text(_binary.TextConstantTable.Resolve(checked((ushort)instruction.StringIndex))));
                    break;

                case GameEventScriptBytecodeOpCode.LoadTag:
                    Set(instruction.DestinationSlot, GesBinaryVmValue.Tag(_binary.TextConstantTable.Resolve(checked((ushort)instruction.StringIndex))));
                    break;

                case GameEventScriptBytecodeOpCode.Cast:
                    Set(instruction.DestinationSlot, CastValue(Get(instruction.XSlot), (GameEventScriptBytecodeTypeKind)instruction.TypeOperand));
                    break;

                case GameEventScriptBytecodeOpCode.CastCustom:
                    Set(instruction.DestinationSlot, CastCustomValue(Get(instruction.XSlot)));
                    break;

                case GameEventScriptBytecodeOpCode.CastUnit:
                    Set(instruction.DestinationSlot, CastUnit(Get(instruction.XSlot), DecodeUnit(instruction.UnitAndFlags)));
                    break;

                case GameEventScriptBytecodeOpCode.CastNumeric:
                    Set(instruction.DestinationSlot, CastNumeric(Get(instruction.XSlot)));
                    break;

                case GameEventScriptBytecodeOpCode.TypeCheck:
                    Set(instruction.DestinationSlot, GesBinaryVmValue.Boolean(IsValueOfType(Get(instruction.XSlot), (GameEventScriptBytecodeTypeKind)instruction.TypeOperand)));
                    break;

                case GameEventScriptBytecodeOpCode.TypeCheckNumeric:
                    Set(instruction.DestinationSlot, GesBinaryVmValue.Boolean(IsValueNumeric(Get(instruction.XSlot))));
                    break;

                case GameEventScriptBytecodeOpCode.TypeCheckCustom:
                    Set(instruction.DestinationSlot, GesBinaryVmValue.Boolean(IsValueOfCustomType(Get(instruction.XSlot), instruction.TypeOperand)));
                    break;

                case GameEventScriptBytecodeOpCode.CheckUnit:
                    Set(instruction.DestinationSlot, GesBinaryVmValue.Boolean(IsValueOfUnit(Get(instruction.XSlot), DecodeUnit(instruction.UnitAndFlags))));
                    break;

                case GameEventScriptBytecodeOpCode.IntAdd:
                case GameEventScriptBytecodeOpCode.IntSubtract:
                case GameEventScriptBytecodeOpCode.IntMultiply:
                case GameEventScriptBytecodeOpCode.IntDivide:
                case GameEventScriptBytecodeOpCode.IntEqual:
                case GameEventScriptBytecodeOpCode.IntNotEqual:
                case GameEventScriptBytecodeOpCode.IntGreater:
                case GameEventScriptBytecodeOpCode.IntGreaterOrEqual:
                case GameEventScriptBytecodeOpCode.IntLess:
                case GameEventScriptBytecodeOpCode.IntLessOrEqual:
                    Set(instruction.DestinationSlot, EvaluatePrimitiveInteger(instruction.OpCode, Get(instruction.XSlot), Get(instruction.YSlot)));
                    break;

                case GameEventScriptBytecodeOpCode.Add:
                case GameEventScriptBytecodeOpCode.Subtract:
                case GameEventScriptBytecodeOpCode.Multiply:
                case GameEventScriptBytecodeOpCode.Divide:
                case GameEventScriptBytecodeOpCode.Equal:
                case GameEventScriptBytecodeOpCode.NotEqual:
                case GameEventScriptBytecodeOpCode.Greater:
                case GameEventScriptBytecodeOpCode.GreaterOrEqual:
                case GameEventScriptBytecodeOpCode.Less:
                case GameEventScriptBytecodeOpCode.LessOrEqual:
                case GameEventScriptBytecodeOpCode.Min:
                case GameEventScriptBytecodeOpCode.Max:
                    Set(instruction.DestinationSlot, EvaluateGenericBinary(instruction.OpCode, Get(instruction.XSlot), Get(instruction.YSlot)));
                    break;

                case GameEventScriptBytecodeOpCode.Jump:
                    _pc = instruction.TargetAddress;
                    break;

                case GameEventScriptBytecodeOpCode.JumpIfTrue:
                    if (Get(instruction.ConditionSlot).IsTrue())
                    {
                        _pc = instruction.TargetAddress;
                    }

                    break;

                case GameEventScriptBytecodeOpCode.JumpIfFalse:
                    if (Get(instruction.ConditionSlot).IsFalse())
                    {
                        _pc = instruction.TargetAddress;
                    }

                    break;

                case GameEventScriptBytecodeOpCode.JumpIfNotTrue:
                    if (!Get(instruction.ConditionSlot).IsTrue())
                    {
                        _pc = instruction.TargetAddress;
                    }

                    break;

                case GameEventScriptBytecodeOpCode.EmitMessage:
                    if (!PublishMessage(instruction, publish: false))
                    {
                        return false;
                    }

                    break;

                case GameEventScriptBytecodeOpCode.PublishMessage:
                    if (!PublishMessage(instruction, publish: true))
                    {
                        return false;
                    }

                    break;

                case GameEventScriptBytecodeOpCode.ReturnVoid:
                    _running = false;
                    break;

                case GameEventScriptBytecodeOpCode.ReturnValue:
                    _running = false;
                    break;

                default:
                    return false;
            }
        }

        return true;
    }

    private void BindHandlerArguments()
    {
        for (var parameterIndex = 0; parameterIndex < _handler.ArgumentNames.Count && parameterIndex < _slots.Length; parameterIndex++)
        {
            var name = _binary.TextConstantTable.Resolve(_handler.ArgumentNames[parameterIndex]);
            if (_message.Arguments.TryGetValue(name, out var value))
            {
                Set(parameterIndex, GesBinaryVmValue.FromGameEventScriptValue(value));
            }
        }
    }

    private bool PublishMessage(GameEventScriptBytecodeInstruction instruction, bool publish)
    {
        var shape = _binary.Uint16ConstantTable.Resolve(instruction.MessageDestination);
        var argumentSlots = _binary.Uint16ConstantTable.Resolve(instruction.ListIndex);
        if (shape.Length == 0 || argumentSlots.Length != shape.Length - 1)
        {
            return false;
        }

        var messageName = _binary.TextConstantTable.Resolve(shape[0]);
        var pairs = new KeyValuePair<string, GameEventScriptValue>[argumentSlots.Length];
        for (var index = 0; index < argumentSlots.Length; index++)
        {
            pairs[index] = new KeyValuePair<string, GameEventScriptValue>(
                _binary.TextConstantTable.Resolve(shape[index + 1]),
                Get(argumentSlots[index]).ToGameEventScriptValue());
        }

        var message = GameEventScriptMessage.Create(messageName, GameEventScriptNamedArguments.CreateOrdered(pairs));
        return publish
            ? _context.Publish(message)
            : _context.Emit(message);
    }

    private static GesBinaryVmValue CastValue(GesBinaryVmValue value, GameEventScriptBytecodeTypeKind typeKind)
        => typeKind switch
        {
            GameEventScriptBytecodeTypeKind.Integer => GesBinaryVmValue.Integer(value.AsInteger()),
            GameEventScriptBytecodeTypeKind.Float => GesBinaryVmValue.Float(value.AsNumber()),
            GameEventScriptBytecodeTypeKind.Boolean => GesBinaryVmValue.Boolean(value.IsTrue()),
            GameEventScriptBytecodeTypeKind.Text => GesBinaryVmValue.Text(value.ToGameEventScriptValue().AsText()),
            _ => GesBinaryVmValue.FromGameEventScriptValue(value.ToGameEventScriptValue())
        };

    private static GesBinaryVmValue CastCustomValue(GesBinaryVmValue value)
        => GesBinaryVmValue.FromGameEventScriptValue(value.ToGameEventScriptValue());

    private static bool IsValueOfType(GesBinaryVmValue value, GameEventScriptBytecodeTypeKind typeKind)
        => typeKind switch
        {
            GameEventScriptBytecodeTypeKind.Nothing => value.Kind == GesBinaryVmValueKind.Nothing,
            GameEventScriptBytecodeTypeKind.Integer => value.Kind == GesBinaryVmValueKind.Integer,
            GameEventScriptBytecodeTypeKind.Float => value.Kind is GesBinaryVmValueKind.Integer or GesBinaryVmValueKind.Float or GesBinaryVmValueKind.Percentage,
            GameEventScriptBytecodeTypeKind.Boolean => value.Kind == GesBinaryVmValueKind.Boolean,
            GameEventScriptBytecodeTypeKind.Text => value.Kind == GesBinaryVmValueKind.Text,
            GameEventScriptBytecodeTypeKind.Tag => value.Kind == GesBinaryVmValueKind.Tag,
            GameEventScriptBytecodeTypeKind.Percentage => value.Kind == GesBinaryVmValueKind.Percentage,
            _ => false
        };

    private static GesBinaryVmValue CastNumeric(GesBinaryVmValue value)
    {
        var boxed = value.ToGameEventScriptValue();
        var unit = GameEventScriptValue.TryGetNumericUnit(boxed, out var numericUnit)
            ? numericUnit
            : (GameEventScriptNumericUnit?)null;

        if (!GesValueOperations.TryCoerceNumericForOperation(boxed, out var number))
        {
            return GesBinaryVmValue.Nothing;
        }

        if (number.IsNaN)
        {
            return GesBinaryVmValue.Float(double.NaN);
        }

        if (number.IsPositiveInfinity)
        {
            return GesBinaryVmValue.Float(double.PositiveInfinity);
        }

        if (number.IsNegativeInfinity)
        {
            return GesBinaryVmValue.Float(double.NegativeInfinity);
        }

        return number.Value >= long.MinValue &&
               number.Value <= long.MaxValue &&
               number.Value == Math.Truncate(number.Value)
            ? GesBinaryVmValue.Integer((long)number.Value, unit)
            : GesBinaryVmValue.Float(number.Value, unit);
    }

    private static bool IsValueNumeric(GesBinaryVmValue value)
    {
        if (value.Kind is GesBinaryVmValueKind.Integer or GesBinaryVmValueKind.Float or GesBinaryVmValueKind.Percentage)
        {
            return true;
        }

        return GesValueOperations.TryCoerceNumericForOperation(value.ToGameEventScriptValue(), out _);
    }

    private bool IsValueOfCustomType(GesBinaryVmValue value, ushort customTypeNameIndex)
        => value.ToGameEventScriptValue().TryGetCustomTypeName(out var customTypeName) &&
           string.Equals(customTypeName, _binary.TextConstantTable.Resolve(customTypeNameIndex), StringComparison.Ordinal);

    private static GesBinaryVmValue CastUnit(GesBinaryVmValue value, GameEventScriptNumericUnit? unit)
        => unit.HasValue
            ? value.Kind switch
            {
                GesBinaryVmValueKind.Integer => GesBinaryVmValue.Integer(value.IntegerValue, unit),
                GesBinaryVmValueKind.Float => GesBinaryVmValue.Float(value.NumberValue, unit),
                _ => GesBinaryVmValue.Nothing
            }
            : GesBinaryVmValue.Nothing;

    private static bool IsValueOfUnit(GesBinaryVmValue value, GameEventScriptNumericUnit? unit)
        => unit.HasValue &&
           value.Kind is GesBinaryVmValueKind.Integer or GesBinaryVmValueKind.Float &&
           value.Unit == unit.Value;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private GesBinaryVmValue Get(int slot)
        => (uint)slot < (uint)_slots.Length ? _slots[slot] : GesBinaryVmValue.Nothing;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Set(int slot, GesBinaryVmValue value)
    {
        if ((uint)slot < (uint)_slots.Length)
        {
            _slots[slot] = value;
        }
    }

    private static GesBinaryVmValue EvaluatePrimitiveInteger(GameEventScriptBytecodeOpCode opCode, GesBinaryVmValue left, GesBinaryVmValue right)
    {
        var a = left.AsInteger();
        var b = right.AsInteger();
        return opCode switch
        {
            GameEventScriptBytecodeOpCode.IntAdd => GesBinaryVmValue.Integer(a + b, left.Unit ?? right.Unit),
            GameEventScriptBytecodeOpCode.IntSubtract => GesBinaryVmValue.Integer(a - b, left.Unit),
            GameEventScriptBytecodeOpCode.IntMultiply => GesBinaryVmValue.Integer(a * b, left.Unit ?? right.Unit),
            GameEventScriptBytecodeOpCode.IntDivide => b == 0 ? GesBinaryVmValue.Nothing : GesBinaryVmValue.Integer(a / b, left.Unit),
            GameEventScriptBytecodeOpCode.IntEqual => GesBinaryVmValue.Boolean(a == b),
            GameEventScriptBytecodeOpCode.IntNotEqual => GesBinaryVmValue.Boolean(a != b),
            GameEventScriptBytecodeOpCode.IntGreater => GesBinaryVmValue.Boolean(a > b),
            GameEventScriptBytecodeOpCode.IntGreaterOrEqual => GesBinaryVmValue.Boolean(a >= b),
            GameEventScriptBytecodeOpCode.IntLess => GesBinaryVmValue.Boolean(a < b),
            GameEventScriptBytecodeOpCode.IntLessOrEqual => GesBinaryVmValue.Boolean(a <= b),
            _ => GesBinaryVmValue.Nothing
        };
    }

    private static GesBinaryVmValue EvaluateGenericBinary(GameEventScriptBytecodeOpCode opCode, GesBinaryVmValue left, GesBinaryVmValue right)
    {
        if (opCode is GameEventScriptBytecodeOpCode.Equal or GameEventScriptBytecodeOpCode.NotEqual)
        {
            var equal = Equals(left.ToGameEventScriptValue(), right.ToGameEventScriptValue());
            return GesBinaryVmValue.Boolean(opCode == GameEventScriptBytecodeOpCode.Equal ? equal : !equal);
        }

        if (opCode is GameEventScriptBytecodeOpCode.Min or GameEventScriptBytecodeOpCode.Max)
        {
            return GesBinaryVmValue.FromGameEventScriptValue(GesValueOperations.EvaluateMinMax(
                left.ToGameEventScriptValue(),
                right.ToGameEventScriptValue(),
                isMax: opCode == GameEventScriptBytecodeOpCode.Max));
        }

        if (left.Kind == GesBinaryVmValueKind.Integer &&
            right.Kind == GesBinaryVmValueKind.Integer)
        {
            return opCode switch
            {
                GameEventScriptBytecodeOpCode.Add or
                    GameEventScriptBytecodeOpCode.Subtract or
                    GameEventScriptBytecodeOpCode.Multiply or
                    GameEventScriptBytecodeOpCode.Divide or
                    GameEventScriptBytecodeOpCode.Greater or
                    GameEventScriptBytecodeOpCode.GreaterOrEqual or
                    GameEventScriptBytecodeOpCode.Less or
                    GameEventScriptBytecodeOpCode.LessOrEqual => EvaluatePrimitiveInteger(ToPrimitiveIntegerOpCode(opCode), left, right),
                _ => GesBinaryVmValue.Nothing
            };
        }

        if (!left.TryGetFiniteNumber(out var a) || !right.TryGetFiniteNumber(out var b))
        {
            return GesBinaryVmValue.Nothing;
        }

        return opCode switch
        {
            GameEventScriptBytecodeOpCode.Add => GesBinaryVmValue.Float(a + b, left.Unit ?? right.Unit),
            GameEventScriptBytecodeOpCode.Subtract => GesBinaryVmValue.Float(a - b, left.Unit),
            GameEventScriptBytecodeOpCode.Multiply => GesBinaryVmValue.Float(a * b, left.Unit ?? right.Unit),
            GameEventScriptBytecodeOpCode.Divide => b == 0d ? GesBinaryVmValue.Nothing : GesBinaryVmValue.Float(a / b, left.Unit),
            GameEventScriptBytecodeOpCode.Greater => GesBinaryVmValue.Boolean(a > b),
            GameEventScriptBytecodeOpCode.GreaterOrEqual => GesBinaryVmValue.Boolean(a >= b),
            GameEventScriptBytecodeOpCode.Less => GesBinaryVmValue.Boolean(a < b),
            GameEventScriptBytecodeOpCode.LessOrEqual => GesBinaryVmValue.Boolean(a <= b),
            _ => GesBinaryVmValue.Nothing
        };
    }

    private static GameEventScriptBytecodeOpCode ToPrimitiveIntegerOpCode(GameEventScriptBytecodeOpCode opCode)
        => opCode switch
        {
            GameEventScriptBytecodeOpCode.Add => GameEventScriptBytecodeOpCode.IntAdd,
            GameEventScriptBytecodeOpCode.Subtract => GameEventScriptBytecodeOpCode.IntSubtract,
            GameEventScriptBytecodeOpCode.Multiply => GameEventScriptBytecodeOpCode.IntMultiply,
            GameEventScriptBytecodeOpCode.Divide => GameEventScriptBytecodeOpCode.IntDivide,
            GameEventScriptBytecodeOpCode.Greater => GameEventScriptBytecodeOpCode.IntGreater,
            GameEventScriptBytecodeOpCode.GreaterOrEqual => GameEventScriptBytecodeOpCode.IntGreaterOrEqual,
            GameEventScriptBytecodeOpCode.Less => GameEventScriptBytecodeOpCode.IntLess,
            GameEventScriptBytecodeOpCode.LessOrEqual => GameEventScriptBytecodeOpCode.IntLessOrEqual,
            _ => opCode
        };

    private static GameEventScriptNumericUnit? DecodeUnit(byte unitAndFlags)
        => (GameEventScriptBytecodeInstructionUnit)(unitAndFlags & 0x1F) switch
        {
            GameEventScriptBytecodeInstructionUnit.UnitDegree => GameEventScriptNumericUnit.Degree,
            GameEventScriptBytecodeInstructionUnit.UnitMeter => GameEventScriptNumericUnit.Meter,
            GameEventScriptBytecodeInstructionUnit.UnitSecond => GameEventScriptNumericUnit.Second,
            _ => null
        };
}

internal readonly struct GesBinaryVmValue
{
    private readonly object? _reference;

    private GesBinaryVmValue(GesBinaryVmValueKind kind, long integer, double number, object? reference, GameEventScriptNumericUnit? unit)
    {
        Kind = kind;
        IntegerValue = integer;
        NumberValue = number;
        _reference = reference;
        Unit = unit;
    }

    public GesBinaryVmValueKind Kind { get; }

    public long IntegerValue { get; }

    public double NumberValue { get; }

    public GameEventScriptNumericUnit? Unit { get; }

    public static GesBinaryVmValue Nothing { get; } = new(GesBinaryVmValueKind.Nothing, 0, 0d, null, null);

    public static GesBinaryVmValue Boolean(bool value) => new(GesBinaryVmValueKind.Boolean, value ? 1 : 0, value ? 1d : 0d, null, null);

    public static GesBinaryVmValue Integer(long value, GameEventScriptNumericUnit? unit = null) => new(GesBinaryVmValueKind.Integer, value, value, null, unit);

    public static GesBinaryVmValue Float(double value, GameEventScriptNumericUnit? unit = null) => new(GesBinaryVmValueKind.Float, 0, value, null, unit);

    public static GesBinaryVmValue Percentage(double ratio) => new(GesBinaryVmValueKind.Percentage, 0, ratio, null, null);

    public static GesBinaryVmValue Text(string value) => new(GesBinaryVmValueKind.Text, 0, 0d, value, null);

    public static GesBinaryVmValue Tag(string value) => new(GesBinaryVmValueKind.Tag, 0, 0d, value, null);

    public static GesBinaryVmValue Reference(GameEventScriptValue value) => new(GesBinaryVmValueKind.Reference, 0, 0d, value, null);

    public static GesBinaryVmValue FromGameEventScriptValue(GameEventScriptValue value)
        => value switch
        {
            GameEventScriptIntegerValue integer => Integer(integer.Value, integer.Unit),
            GameEventScriptFloatValue number => Float(number.AsNumber(), number.Unit),
            GameEventScriptPercentageValue percentage => Percentage(percentage.Ratio),
            GameEventScriptBooleanValue boolean => Boolean(boolean.Value),
            GameEventScriptTextValue text => Text(text.Value),
            GameEventScriptTagValue tag => Tag(tag.Value),
            _ when value.IsNothing() => Nothing,
            _ => Reference(value)
        };

    public long AsInteger()
        => Kind switch
        {
            GesBinaryVmValueKind.Integer => IntegerValue,
            GesBinaryVmValueKind.Float or GesBinaryVmValueKind.Percentage => GameEventScriptValue.ToIntegerSaturated(NumberValue),
            GesBinaryVmValueKind.Boolean => IntegerValue,
            GesBinaryVmValueKind.Reference when _reference is GameEventScriptValue value => value.AsInteger(),
            _ => 0
        };

    public double AsNumber()
        => Kind switch
        {
            GesBinaryVmValueKind.Integer => IntegerValue,
            GesBinaryVmValueKind.Float or GesBinaryVmValueKind.Percentage => NumberValue,
            GesBinaryVmValueKind.Boolean => NumberValue,
            GesBinaryVmValueKind.Reference when _reference is GameEventScriptValue value => value.AsNumber(),
            _ => 0d
        };

    public bool TryGetFiniteNumber(out double number)
    {
        number = AsNumber();
        return (Kind is GesBinaryVmValueKind.Integer or GesBinaryVmValueKind.Float or GesBinaryVmValueKind.Percentage or GesBinaryVmValueKind.Boolean) &&
               !double.IsNaN(number) &&
               !double.IsInfinity(number);
    }

    public bool IsTrue()
        => Kind switch
        {
            GesBinaryVmValueKind.Boolean => IntegerValue != 0,
            GesBinaryVmValueKind.Integer => IntegerValue != 0,
            GesBinaryVmValueKind.Float or GesBinaryVmValueKind.Percentage => NumberValue != 0d && !double.IsNaN(NumberValue),
            GesBinaryVmValueKind.Reference when _reference is GameEventScriptValue value => value.AsBoolean(),
            _ => false
        };

    public bool IsFalse()
        => Kind == GesBinaryVmValueKind.Boolean && IntegerValue == 0;

    public GameEventScriptValue ToGameEventScriptValue()
        => Kind switch
        {
            GesBinaryVmValueKind.Integer => GameEventScriptValueFactory.GesInteger(IntegerValue, Unit),
            GesBinaryVmValueKind.Float => GameEventScriptValueFactory.GesFloat(NumberValue, Unit),
            GesBinaryVmValueKind.Percentage => GameEventScriptValueFactory.GesPercentage(NumberValue),
            GesBinaryVmValueKind.Boolean => GameEventScriptValueFactory.GesBoolean(IntegerValue != 0),
            GesBinaryVmValueKind.Text => GameEventScriptValueFactory.GesText((string?)_reference ?? string.Empty),
            GesBinaryVmValueKind.Tag => GameEventScriptValueFactory.GesTag((string?)_reference ?? string.Empty),
            GesBinaryVmValueKind.Reference when _reference is GameEventScriptValue value => value,
            _ => GameEventScriptValueFactory.GesNothing()
        };
}

internal enum GesBinaryVmValueKind : byte
{
    Nothing,
    Boolean,
    Integer,
    Float,
    Percentage,
    Text,
    Tag,
    Reference
}
