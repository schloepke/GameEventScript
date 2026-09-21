// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Text.Json;
using GameEventScript.Api;
using GameEventScript.Runtime.Values;

// Executable-only interoperability adapter. Parsing and formatting run through the VM.
internal static class NumberTextProbe
{
    internal static int Run(string input, string output)
    {
        using var requests = JsonDocument.Parse(File.ReadAllText(input));
        var program = GameEventScriptBuilder.Create().AddScript(
            "on Probe(value, text) { emit Result(formatted: value as :Text, number: text as :Number, percentage: text as :Percentage) }").Compile();
        var host = GameEventScriptHost.CreateBuilder().Build();
        var capture = new Capture();
        host.Subscribe("Result", ["formatted", "number", "percentage"], capture);
        host.Load(program);
        if (host.Start().State != GameEventScriptStartState.Ready) throw new InvalidOperationException("Probe host failed to start.");
        var responses = new List<object>();
        foreach (var row in requests.RootElement.EnumerateArray())
        {
            var kind = row.GetProperty("kind").GetString();
            var payload = row.GetProperty("payload").GetString()!;
            var unit = row.GetProperty("unit").GetString() switch
            {
                "none" => GameEventScriptBytecodeInstructionUnit.UnitNone,
                "m" => GameEventScriptBytecodeInstructionUnit.UnitMeter,
                "s" => GameEventScriptBytecodeInstructionUnit.UnitSecond,
                "degree" => GameEventScriptBytecodeInstructionUnit.UnitDegree,
                _ => throw new InvalidOperationException("Unknown unit.")
            };
            var value = kind == "integer" ? GesValue.GesInteger(long.Parse(payload, CultureInfo.InvariantCulture), unit)
                : DecodeFloat(kind!, payload, unit);
            var text = row.TryGetProperty("text", out var textInput) ? textInput.GetString()! : "nothing";
            capture.Message = null;
            capture.Count = 0;
            host.Receive(GameEventScriptMessage.Create("Probe", [new("value", value), new("text", GesValue.GesText(text))]));
            if (host.RunToCompletion().State != GameEventScriptExecutionState.Completed || capture.Count != 1 || capture.Message is not { } result)
                throw new InvalidOperationException("Probe did not produce exactly one result.");
            responses.Add(new { source = Describe(value), text = result.Arguments[0].AsText(), number = Describe(result.Arguments[1]), percentage = Describe(result.Arguments[2]) });
        }
        File.WriteAllText(output, JsonSerializer.Serialize(responses));
        return 0;
    }

    private static GesValue DecodeFloat(string kind, string payload, GameEventScriptBytecodeInstructionUnit unit)
    {
        var value = BitConverter.Int64BitsToDouble(unchecked((long)ulong.Parse(payload, NumberStyles.HexNumber, CultureInfo.InvariantCulture)));
        return kind switch
        {
            "float" => GesValue.GesFloat(value, unit),
            "percentage" when unit == GameEventScriptBytecodeInstructionUnit.UnitNone => GesValue.GesPercentage(value),
            _ => throw new InvalidOperationException("Unknown numeric input kind.")
        };
    }

    private static string Describe(GesValue value)
    {
        if (value.IsNothing) return "nothing";
        var unit = value.ValueUnit switch
        {
            GameEventScriptBytecodeInstructionUnit.UnitNone => "none",
            GameEventScriptBytecodeInstructionUnit.UnitMeter => "m",
            GameEventScriptBytecodeInstructionUnit.UnitSecond => "s",
            GameEventScriptBytecodeInstructionUnit.UnitDegree => "degree",
            _ => throw new InvalidOperationException("Unexpected result unit.")
        };
        if (value.ValueKind == GameEventScriptBytecodeTypeKind.Integer) return "integer:" + value.AsInteger().ToString(CultureInfo.InvariantCulture) + ":" + unit;
        var kind = value.ValueKind == GameEventScriptBytecodeTypeKind.Percentage ? "percentage" : value.ValueKind == GameEventScriptBytecodeTypeKind.Float ? "float" : throw new InvalidOperationException("Non-numeric result.");
        return kind + ":" + unchecked((ulong)BitConverter.DoubleToInt64Bits(value.AsNumber())).ToString("x16", CultureInfo.InvariantCulture) + ":" + unit;
    }

    private sealed class Capture : IGameEventScriptNativeMessageHandler
    {
        internal GameEventScriptMessage? Message;
        internal int Count;

        /// <inheritdoc />
        public void Handle(GameEventScriptMessage message, GameEventScriptContext context)
        {
            Message = message;
            Count++;
        }
    }
}
