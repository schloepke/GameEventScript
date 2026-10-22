// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using GameEventScript.Api;

internal static class RuntimeRandom
{
    internal static void Validate(JsonElement configuration)
    {
        if (configuration.ValueKind != JsonValueKind.Object || configuration.EnumerateObject().Count() != 1)
            throw new ArgumentException("Exactly one random configuration required.");
        var field = configuration.EnumerateObject().Single();
        switch (field.Name)
        {
            case "seed":
                _ = Seed(field.Value);
                break;
            case "entropy":
                var text = field.Value.GetString();
                if (text is null || !Regex.IsMatch(text, @"\A(?:[0-9A-F]{2})+\z")) throw new ArgumentException("Invalid entropy encoding.");
                break;
            case "sequence":
                if (field.Value.ValueKind != JsonValueKind.Array || field.Value.GetArrayLength() == 0) throw new ArgumentException("Empty random sequence.");
                foreach (var item in field.Value.EnumerateArray())
                    if (!double.IsFinite(RuntimeValues.ParseNumber(item.GetString()!))) throw new ArgumentException("Random sequence requires finite values.");
                break;
            default:
                throw new ArgumentException("Unknown random configuration.");
        }
    }

    internal static void Configure(GesHostBuilder builder, JsonElement configuration)
    {
        if (configuration.TryGetProperty("seed", out var seed)) builder.WithRandomSeed(Seed(seed));
        else if (configuration.TryGetProperty("entropy", out var entropy)) builder.WithRandomEntropy(Convert.FromHexString(entropy.GetString()!));
        else builder.WithRandomSequence(configuration.GetProperty("sequence").EnumerateArray().Select(item => RuntimeValues.ParseNumber(item.GetString()!)).ToArray(), 0L);
    }

    private static long Seed(JsonElement value)
    {
        var text = value.GetString();
        if (text is null || !Regex.IsMatch(text, @"\A(?:0|-?[1-9][0-9]*)\z")) throw new ArgumentException("Invalid random seed.");
        return long.Parse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
    }
}
