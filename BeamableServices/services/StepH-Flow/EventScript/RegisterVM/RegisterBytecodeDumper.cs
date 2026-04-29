#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System.Globalization;
using System.Text;

namespace StepH.Flow.EventScript.RegisterVM;

public static class RegisterBytecodeDumper
{
    public static string ToDebugText(RegisterCompiledEventScript compiledScript)
        => ToDebugText(compiledScript.BytecodeModule);

    internal static string ToDebugText(RegisterBytecodeModule module)
    {
        var builder = new StringBuilder();
        builder.AppendLine("registervm bytecode v1");
        builder.Append("diagnostics: ").AppendLine(module.Options.EnableDiagnostics ? "on" : "off");
        AppendPool(builder, "strings", module.StringPool);
        AppendPool(builder, "signatures", module.Signatures);
        AppendPool(builder, "types", module.TypeMetadata);

        builder.Append("constants[").Append(module.ConstantPool.Count.ToString(CultureInfo.InvariantCulture)).AppendLine("]");
        for (var i = 0; i < module.ConstantPool.Count; i++)
        {
            builder.Append("  #").Append(i.ToString(CultureInfo.InvariantCulture)).Append(": ")
                .AppendLine(module.ConstantPool[i].ToString());
        }

        builder.Append("namedArgumentLayouts[").Append(module.NamedArgumentLayouts.Count.ToString(CultureInfo.InvariantCulture)).AppendLine("]");
        for (var i = 0; i < module.NamedArgumentLayouts.Count; i++)
        {
            builder.Append("  #").Append(i.ToString(CultureInfo.InvariantCulture)).Append(": ")
                .AppendLine(string.Join(", ", module.NamedArgumentLayouts[i]));
        }

        builder.Append("programs[").Append(module.Programs.Count.ToString(CultureInfo.InvariantCulture)).AppendLine("]");
        for (var programIndex = 0; programIndex < module.Programs.Count; programIndex++)
        {
            var program = module.Programs[programIndex];
            builder.Append("program #").Append(programIndex.ToString(CultureInfo.InvariantCulture))
                .Append(' ').Append(program.Name)
                .Append(" registers=").Append(program.RegisterCount.ToString(CultureInfo.InvariantCulture))
                .Append(" locals=").Append(program.LocalCount.ToString(CultureInfo.InvariantCulture))
                .Append(" scope=").Append(program.CreatesScope ? "block" : "shared")
                .AppendLine();
            for (var instructionIndex = 0; instructionIndex < program.Instructions.Count; instructionIndex++)
            {
                var instruction = program.Instructions[instructionIndex];
                builder.Append("  ")
                    .Append(instructionIndex.ToString("0000", CultureInfo.InvariantCulture))
                    .Append(' ')
                    .Append(instruction.OpCode);
                AppendInstructionOperands(builder, module, instruction);
                builder
                    .AppendLine();
            }
        }

        return builder.ToString();
    }

    private static void AppendInstructionOperands(StringBuilder builder, RegisterBytecodeModule module, RegisterInstruction instruction)
    {
        switch (instruction.OpCode)
        {
            case RegisterOpCode.LoadParameter:
                AppendStringOperand(builder, module, "parameter", instruction.A);
                AppendRegisterOperand(builder, "target", instruction.B);
                break;

            case RegisterOpCode.LoadConstant:
                AppendConstantOperand(builder, module, "constant", instruction.A);
                AppendRegisterOperand(builder, "target", instruction.B);
                break;

            case RegisterOpCode.EvaluateExpression:
                AppendStringOperand(builder, module, "expr", instruction.A);
                AppendRegisterOperand(builder, "target", instruction.B);
                break;

            case RegisterOpCode.StoreLocal:
                AppendStringOperand(builder, module, "name", instruction.A);
                AppendIndexOperand(builder, "local", instruction.B);
                AppendTypeOperand(builder, module, "type", instruction.C);
                break;

            case RegisterOpCode.Publish:
                AppendIndexOperand(builder, "exprInstr", instruction.A);
                break;

            case RegisterOpCode.JumpIfFalse:
                AppendIndexOperand(builder, "conditionInstr", instruction.A);
                AppendProgramOperand(builder, "then", instruction.B);
                AppendProgramOperand(builder, "else", instruction.C);
                break;

            case RegisterOpCode.Jump:
                AppendProgramOperand(builder, "target", instruction.A);
                break;

            case RegisterOpCode.ForEach:
                AppendStringOperand(builder, module, "item", instruction.A);
                AppendStringOperand(builder, module, "source", instruction.B);
                AppendProgramOperand(builder, "body", instruction.C);
                break;

            case RegisterOpCode.SeededRandom:
                AppendIndexOperand(builder, "seedInstr", instruction.A);
                AppendProgramOperand(builder, "body", instruction.B);
                break;

            case RegisterOpCode.Call:
                AppendStringOperand(builder, module, "callable", instruction.A);
                AppendRegisterOperand(builder, "target", instruction.B);
                AppendIndexOperand(builder, "argc", instruction.C);
                break;

            case RegisterOpCode.Return:
                AppendRegisterOperand(builder, "value", instruction.A);
                break;

            case RegisterOpCode.EnterScope:
            case RegisterOpCode.ExitScope:
                break;

            default:
                AppendRawOperands(builder, instruction);
                break;
        }
    }

    private static void AppendStringOperand(StringBuilder builder, RegisterBytecodeModule module, string name, int index)
    {
        if (index < 0)
        {
            return;
        }

        builder.Append(' ').Append(name).Append("=#").Append(index.ToString(CultureInfo.InvariantCulture));
        if ((uint)index < (uint)module.StringPool.Count)
        {
            builder.Append('(').Append(module.StringPool[index]).Append(')');
        }
    }

    private static void AppendTypeOperand(StringBuilder builder, RegisterBytecodeModule module, string name, int index)
    {
        if (index < 0)
        {
            return;
        }

        builder.Append(' ').Append(name).Append("=#").Append(index.ToString(CultureInfo.InvariantCulture));
        if ((uint)index < (uint)module.TypeMetadata.Count)
        {
            builder.Append('(').Append(module.TypeMetadata[index]).Append(')');
        }
    }

    private static void AppendConstantOperand(StringBuilder builder, RegisterBytecodeModule module, string name, int index)
    {
        if (index < 0)
        {
            return;
        }

        builder.Append(' ').Append(name).Append("=#").Append(index.ToString(CultureInfo.InvariantCulture));
        if ((uint)index < (uint)module.ConstantPool.Count)
        {
            builder.Append('(').Append(module.ConstantPool[index]).Append(')');
        }
    }

    private static void AppendRegisterOperand(StringBuilder builder, string name, int register)
    {
        if (register < 0)
        {
            return;
        }

        builder.Append(' ').Append(name).Append("=r").Append(register.ToString(CultureInfo.InvariantCulture));
    }

    private static void AppendProgramOperand(StringBuilder builder, string name, int index)
    {
        if (index < 0)
        {
            return;
        }

        builder.Append(' ').Append(name).Append("=program#").Append(index.ToString(CultureInfo.InvariantCulture));
    }

    private static void AppendIndexOperand(StringBuilder builder, string name, int index)
    {
        if (index < 0)
        {
            return;
        }

        builder.Append(' ').Append(name).Append('=').Append(index.ToString(CultureInfo.InvariantCulture));
    }

    private static void AppendRawOperands(StringBuilder builder, RegisterInstruction instruction)
    {
        AppendIndexOperand(builder, "a", instruction.A);
        AppendIndexOperand(builder, "b", instruction.B);
        AppendIndexOperand(builder, "c", instruction.C);
        AppendIndexOperand(builder, "d", instruction.D);
    }

    private static void AppendPool(StringBuilder builder, string name, System.Collections.Generic.IReadOnlyList<string> values)
    {
        builder.Append(name).Append('[').Append(values.Count.ToString(CultureInfo.InvariantCulture)).AppendLine("]");
        for (var i = 0; i < values.Count; i++)
        {
            builder.Append("  #").Append(i.ToString(CultureInfo.InvariantCulture)).Append(": ").AppendLine(values[i]);
        }
    }
}
