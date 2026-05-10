#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace StepH.GameEventScript.Api;

internal sealed class GameEventScriptBytecodeInstructionJsonConverter : JsonConverter<GameEventScriptBytecodeInstruction>
{
    private const string OpcodePropertyName = "Opcode";
    private const string FlagsPropertyName = "Flags";
    private const string DstPropertyName = "Dst";
    private const string ParameterPropertyName = "Parameter";

    public override GameEventScriptBytecodeInstruction Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException("A bytecode instruction must be a JSON object.");
        }

        GameEventScriptBytecodeOpCode? opcode = null;
        byte? flags = null;
        ushort? dst = null;
        ulong? parameter = null;

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
            {
                break;
            }

            if (reader.TokenType != JsonTokenType.PropertyName)
            {
                throw new JsonException("A bytecode instruction property name was expected.");
            }

            var propertyName = reader.GetString();
            if (!reader.Read())
            {
                throw new JsonException("A bytecode instruction property value was expected.");
            }

            if (IsProperty(propertyName, OpcodePropertyName, "OpCode", "opCode", "opcode"))
            {
                opcode = ReadOpcode(ref reader);
            }
            else if (IsProperty(propertyName, FlagsPropertyName, "UnitAndFlags", "unitAndFlags", "flags"))
            {
                flags = checked((byte)ReadUnsigned(ref reader, FlagsPropertyName, byte.MaxValue));
            }
            else if (IsProperty(propertyName, DstPropertyName, "Dest", "Dest_U16", "dest", "dst"))
            {
                dst = checked((ushort)ReadUnsigned(ref reader, DstPropertyName, ushort.MaxValue));
            }
            else if (IsProperty(propertyName, ParameterPropertyName, "Payload", "U64", "parameter", "payload", "u64"))
            {
                parameter = ReadUnsigned(ref reader, ParameterPropertyName, ulong.MaxValue);
            }
            else
            {
                reader.Skip();
            }
        }

        if (opcode is null)
        {
            throw new JsonException("A bytecode instruction requires an Opcode property.");
        }

        if (flags is null)
        {
            throw new JsonException("A bytecode instruction requires a Flags property.");
        }

        if (dst is null)
        {
            throw new JsonException("A bytecode instruction requires a Dst property.");
        }

        if (parameter is null)
        {
            throw new JsonException("A bytecode instruction requires a Parameter property.");
        }

        var instruction = new GameEventScriptBytecodeInstruction(opcode.Value, dst.Value, unitAndFlags: flags.Value);
        instruction.U64 = parameter.Value;
        return instruction;
    }

    public override void Write(Utf8JsonWriter writer, GameEventScriptBytecodeInstruction value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString(OpcodePropertyName, FormatOpcode(value.OpCode));
        writer.WriteString(FlagsPropertyName, FormatHex(value.UnitAndFlags, 2));
        writer.WriteString(DstPropertyName, FormatHex(value.Dest_U16, 4));
        writer.WriteString(ParameterPropertyName, FormatHex(value.U64, 16));
        writer.WriteEndObject();
    }

    private static string FormatOpcode(GameEventScriptBytecodeOpCode opcode)
        => Enum.IsDefined(typeof(GameEventScriptBytecodeOpCode), opcode)
            ? opcode.ToString()
            : FormatHex((byte)opcode, 2);

    private static GameEventScriptBytecodeOpCode ReadOpcode(ref Utf8JsonReader reader)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.Number:
                if (reader.TryGetByte(out var numericOpcode))
                {
                    return (GameEventScriptBytecodeOpCode)numericOpcode;
                }

                break;
            case JsonTokenType.String:
                var text = reader.GetString();
                if (string.IsNullOrWhiteSpace(text))
                {
                    break;
                }

                text = text.Trim();
                if (Enum.TryParse<GameEventScriptBytecodeOpCode>(text, ignoreCase: true, out var namedOpcode))
                {
                    return namedOpcode;
                }

                var parsed = ParseUnsigned(text, byte.MaxValue, OpcodePropertyName);
                return (GameEventScriptBytecodeOpCode)checked((byte)parsed);
        }

        throw new JsonException("Opcode must be an opcode name, byte number, or byte hex string.");
    }

    private static ulong ReadUnsigned(ref Utf8JsonReader reader, string propertyName, ulong maxValue)
    {
        ulong value;
        switch (reader.TokenType)
        {
            case JsonTokenType.Number:
                if (!reader.TryGetUInt64(out value))
                {
                    throw new JsonException(propertyName + " must be an unsigned integer.");
                }

                break;
            case JsonTokenType.String:
                value = ParseUnsigned(reader.GetString(), maxValue, propertyName);
                break;
            default:
                throw new JsonException(propertyName + " must be an unsigned integer or hex string.");
        }

        if (value > maxValue)
        {
            throw new JsonException(propertyName + " exceeds 0x" + maxValue.ToString("X", CultureInfo.InvariantCulture) + ".");
        }

        return value;
    }

    private static ulong ParseUnsigned(string? text, ulong maxValue, string propertyName)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new JsonException(propertyName + " must not be empty.");
        }

        text = text.Trim();
        var style = NumberStyles.Integer;
        var digits = text;
        if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            style = NumberStyles.AllowHexSpecifier;
            digits = text[2..];
        }

        if (digits.Length == 0 ||
            !ulong.TryParse(digits, style, CultureInfo.InvariantCulture, out var value) ||
            value > maxValue)
        {
            throw new JsonException(propertyName + " has invalid unsigned value '" + text + "'.");
        }

        return value;
    }

    private static bool IsProperty(string? actual, params string[] expected)
    {
        if (actual is null)
        {
            return false;
        }

        foreach (var candidate in expected)
        {
            if (string.Equals(actual, candidate, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static string FormatHex(byte value, int width) => "0x" + value.ToString("X" + width.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);

    private static string FormatHex(ushort value, int width) => "0x" + value.ToString("X" + width.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);

    private static string FormatHex(ulong value, int width) => "0x" + value.ToString("X" + width.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
}
