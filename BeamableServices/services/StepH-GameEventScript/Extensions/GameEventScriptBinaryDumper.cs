using System.Text;
using System.Globalization;
using StepH.GameEventScript.Api;

namespace StepH.GameEventScript.Extensions;

/// <summary>
/// Creates a readable dump of the portable GameEventScript binary model.
/// </summary>
public static class GameEventScriptBinaryDumper
{
    /// <summary>
    /// Dumps the binary header, tables, and instruction words.
    /// </summary>
    public static string Dump(this GameEventScriptBinary binary)
    {
        var builder = new StringBuilder();
        builder
            .AppendLine("// -------------------------------------------------------------------------------")
            .Append("//  ").Append(binary.ModuleName).AppendLine(".gesb")
            .AppendLine("//")
            .Append("//  Format version: ").Append(binary.Header.Version).AppendLine()
            .AppendLine("// -------------------------------------------------------------------------------")
            .AppendLine();
        AppendStringTable(builder, binary.TextConstantTable);
        AppendUInt16Table(builder, binary.Uint16ConstantTable);
        AppendBindTable(builder, binary.BindTable, binary.TextConstantTable);
        AppendInstructionTable(builder, binary.InstructionTable);
        return builder.ToString();
    }

    private static void AppendStringTable(StringBuilder builder, GameEventScriptTextTable binaryStringTable)
    {
        builder.AppendLine(".string-table").AppendLine();
        for (var i = 0; i < binaryStringTable.Slices.Length; i++)
        {
            var slice = binaryStringTable.Slices[i];
            builder.Append(".entry.").Append(i.ToString()).Append(" \"").Append(Escape(binaryStringTable.Resolve((ushort)i))).AppendLine("\"");
        }
        builder.AppendLine();
    }

    private static void AppendUInt16Table(StringBuilder builder, GameEventScriptUInt16Table table)
    {
        builder.AppendLine(".uint16-table").AppendLine();
        for (var i = 0; i < table.Slices.Length; i++)
        {
            var slice = table.Slices[i];
            var values = table.Resolve((ushort)i).ToArray();
            builder.Append(".list.").Append(i.ToString()).Append(" ").AppendLine(string.Join(", ", values));
        }
        builder.AppendLine();
    }

    private static void AppendBindTable(StringBuilder builder, GameEventScriptBinaryBindTable table, GameEventScriptTextTable stringTable)
    {
        builder.AppendLine(".bind-table").AppendLine();
        for (var i = 0; i < table.Entries.Count; i++)
        {
            var entry = table.Entries[i];
            builder
                .Append(".index ").AppendLine(i.ToString(CultureInfo.InvariantCulture))
                .Append(".id ").AppendLine(entry.Id.ToString(CultureInfo.InvariantCulture))
                .Append(".kind ").AppendLine(entry.Kind.ToString())
                .Append(".name ").Append(entry.Name.ToString(CultureInfo.InvariantCulture))
                .Append(" \"").Append(Escape(stringTable.Resolve(entry.Name))).AppendLine("\"")
                .Append(".entry ").AppendLine(entry.EntryAddress.ToString(CultureInfo.InvariantCulture))
                .Append(".arguments ").AppendLine(string.Join(", ", entry.ArgumentNames));
        }

        builder.AppendLine();
    }

    private static void AppendInstructionTable(StringBuilder builder, GameEventScriptBytecodeInstruction[] instructions)
    {
        builder.AppendLine(".instruction-table").AppendLine();
        for (var i = 0; i < instructions.Length; i++)
        {
            var instruction = instructions[i];
            builder
                .Append('@').Append(i.ToString("0000", CultureInfo.InvariantCulture))
                .Append(' ')
                .Append(instruction.OpCode)
                .Append(" flags=0x").Append(instruction.UnitAndFlags.ToString("X2", CultureInfo.InvariantCulture))
                .Append(" dst=0x").Append(instruction.DestinationSlot.ToString("X4", CultureInfo.InvariantCulture))
                .Append(" payload=0x").Append(instruction.Payload.ToString("X16", CultureInfo.InvariantCulture))
                .AppendLine();
        }
    }

private static string Escape(string value)
        => value.Replace("\\", "\\\\").Replace("\"", "\\\"");
}
