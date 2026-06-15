namespace StepH.GameEventScript.VirtualMachine;

internal static class GesVmMathConstants
{
    private const double GesPi = 3.1415926535897932384626433833d;
    private const double GesEulerNumber = 2.7182818284590452353602874714d;
    private const double GesTau = 6.2831853071795864769252867666d;
    private const double GesPhi = 1.6180339887498948482045868344d;

    private const string TrueTag = "true";
    private const string FalseTag = "false";
    private const string InfinityTag = "infinity";
    private const string NegativeInfinityTag = "negativeinfinity";
    private const string NumberPiTag = "pi";
    private const string NumberEulerTag = "e";
    private const string NumberTauTag = "tau";
    private const string NumberPhiTag = "phi";
    
    internal static double ResolveNumericTagValue(string? tag) => tag switch
    {
        TrueTag => 1,
        FalseTag => 0,
        InfinityTag => double.PositiveInfinity,
        NegativeInfinityTag => double.NegativeInfinity,
        NumberPiTag => GesPi,
        NumberEulerTag => GesEulerNumber,
        NumberTauTag => GesTau,
        NumberPhiTag => GesPhi,
        _ => double.NaN
    };

    internal static bool IsNumericTag(string? tag) => tag is TrueTag or FalseTag or InfinityTag or NegativeInfinityTag or NumberPiTag or NumberEulerTag or NumberTauTag or NumberPhiTag;
}