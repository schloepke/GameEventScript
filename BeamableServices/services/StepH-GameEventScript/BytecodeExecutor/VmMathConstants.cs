using System.Runtime.CompilerServices;

namespace StepH.GameEventScript.BytecodeExecutor;

internal static class VmMathConstants
{
    internal const double GesPi = 3.1415926535897932384626433833d;
    internal const double GesEulerNumber = 2.7182818284590452353602874714d;
    internal const double GesTau = 6.2831853071795864769252867666d;
    internal const double GesPhi = 1.6180339887498948482045868344d;
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static double ResolveNumericTagValue(string? tag) => tag switch
    {
        "infinity" => double.PositiveInfinity,
        "negativeinfinity" => double.NegativeInfinity,
        "pi" => GesPi,
        "e" => GesEulerNumber,
        "tau" => GesTau,
        "phi" => GesPhi,
        _ => double.NaN
    };

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool IsNumericTag(string? tag) => tag is "infinity" or "negativeinfinity" or "pi" or "e" or "tau" or "phi";
}