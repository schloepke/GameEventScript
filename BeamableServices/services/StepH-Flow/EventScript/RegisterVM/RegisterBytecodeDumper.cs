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
                    .Append(instruction.OpCode)
                    .Append(" a=").Append(instruction.A.ToString(CultureInfo.InvariantCulture))
                    .Append(" b=").Append(instruction.B.ToString(CultureInfo.InvariantCulture))
                    .Append(" c=").Append(instruction.C.ToString(CultureInfo.InvariantCulture))
                    .Append(" d=").Append(instruction.D.ToString(CultureInfo.InvariantCulture))
                    .AppendLine();
            }
        }

        return builder.ToString();
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
