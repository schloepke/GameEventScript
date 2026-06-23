using System;
using System.Collections.Generic;
using StepH.GameEventScript.Api;
using StepH.GameEventScript.VirtualMachine;
using static StepH.GameEventScript.Api.GameEventScriptMessageSignature;
using static StepH.GameEventScript.Api.GameEventScriptValueFactory;

namespace StepH.GameEventScript.Runtime;

internal static class GesStandardExtensions
{
    public static bool IsStandardReference(GameEventScriptExtensionReference reference) => IsSeriesStandardReference(reference);

    public static bool TryInvoke(GameEventScriptExtensionReference reference, ReadOnlySpan<GameEventScriptValue> arguments, out GameEventScriptValue value)
    {
        value = GesNothing();
        if (!IsSeriesStandardReference(reference))
        {
            return false;
        }

        value = EvaluateSeries(reference, arguments);
        return true;
    }

    private static bool IsSeriesStandardReference(GameEventScriptExtensionReference reference) => reference.ExtensionName == "series" && reference.FunctionName switch
    {
        "fibonacci" or "factorial" => reference.ArgumentLabels.Count == 0,
        "natural" => IsNaturalSeriesSignature(reference.ArgumentLabels),
        _ => false
    };

    private static bool IsNaturalSeriesSignature(IReadOnlyList<string> labels) => labels.Count switch
    {
        0 => true,
        1 => IsUnlabeled(labels[0]) || IsLabel(labels[0], "start") || IsLabel(labels[0], "step"),
        _ => labels.Count == 2 && IsLabel(labels[0], "start") && IsLabel(labels[1], "step")
    };

    private static bool IsUnlabeled(string label) => string.Equals(NormalizeParameterName(label), UnlabeledParameterName, StringComparison.Ordinal);

    private static bool IsLabel(string label, string expected) => string.Equals(NormalizeParameterName(label), expected, StringComparison.Ordinal);

    private static GameEventScriptValue EvaluateSeries(GameEventScriptExtensionReference reference, ReadOnlySpan<GameEventScriptValue> arguments)
    {
        var series = reference.FunctionName switch
        {
            "fibonacci" => GesVmSeries.Fibonacci(),
            "factorial" => GesVmSeries.Factorial(),
            "natural" => EvaluateNaturalSeries(reference.ArgumentLabels, arguments),
            _ => null
        };

        return series is null ? GesNothing() : GesSeries(series);
    }

    private static GesVmSeries EvaluateNaturalSeries(IReadOnlyList<string> labels, ReadOnlySpan<GameEventScriptValue> arguments)
    {
        long start = 0;
        long step = 1;
        for (var index = 0; index < arguments.Length; index++)
        {
            var label = labels.Count > index ? labels[index] : UnlabeledParameterName;
            if (IsUnlabeled(label) || IsLabel(label, "start"))
            {
                start = arguments[index].Integer;
            }
            else if (IsLabel(label, "step"))
            {
                step = arguments[index].Integer;
            }
        }

        return GesVmSeries.Natural(start, step);
    }
}
