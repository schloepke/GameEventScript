#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Globalization;
using static StepH.GameEventScript.Api.GameEventScriptValueFactory;

namespace StepH.GameEventScript.Types;

public sealed class GameEventScriptUuidValue : GameEventScriptValue
{
    public static GameEventScriptUuidValue Create(long high, long low)
        => new(high, low);

    public static GameEventScriptUuidValue Parse(string text)
        => TryParse(text, out var value)
            ? value
            : throw new FormatException("Value is not a canonical RFC UUID string.");

    public static bool TryParse(string? text, out GameEventScriptUuidValue value)
    {
        value = default!;
        var input = text?.Trim();
        if (input is not { Length: 36 } ||
            input[8] != '-' ||
            input[13] != '-' ||
            input[18] != '-' ||
            input[23] != '-')
        {
            return false;
        }

        ulong high = 0;
        ulong low = 0;
        var digitIndex = 0;
        for (var index = 0; index < input.Length; index++)
        {
            var ch = input[index];
            if (ch == '-')
            {
                continue;
            }

            var nibble = HexNibble(ch);
            if (nibble < 0)
            {
                return false;
            }

            if (digitIndex < 16)
            {
                high = (high << 4) | (uint)nibble;
            }
            else
            {
                low = (low << 4) | (uint)nibble;
            }

            digitIndex++;
        }

        if (digitIndex != 32)
        {
            return false;
        }

        value = new GameEventScriptUuidValue(unchecked((long)high), unchecked((long)low));
        return true;
    }

    private GameEventScriptUuidValue(long high, long low)
    {
        High = high;
        Low = low;
    }

    public override GameEventScriptValueKind Kind => GameEventScriptValueKind.Uuid;

    public long High { get; }

    public long Low { get; }

    public override string AsText() => ToCanonicalString();

    public override bool AsBoolean() => High != 0 || Low != 0;

    public override bool HasSemanticValue() => AsBoolean();

    public override bool IsSemanticallyEmpty() => !AsBoolean();

    internal override bool TryConvertToText(out GameEventScriptValue value)
    {
        value = GesText(AsText());
        return true;
    }

    private string ToCanonicalString()
    {
        var high = unchecked((ulong)High).ToString("x16", CultureInfo.InvariantCulture);
        var low = unchecked((ulong)Low).ToString("x16", CultureInfo.InvariantCulture);
        return string.Concat(
            high.Substring(0, 8),
            "-",
            high.Substring(8, 4),
            "-",
            high.Substring(12, 4),
            "-",
            low.Substring(0, 4),
            "-",
            low.Substring(4, 12));
    }

    private static int HexNibble(char ch)
        => ch switch
        {
            >= '0' and <= '9' => ch - '0',
            >= 'a' and <= 'f' => ch - 'a' + 10,
            >= 'A' and <= 'F' => ch - 'A' + 10,
            _ => -1
        };
}
