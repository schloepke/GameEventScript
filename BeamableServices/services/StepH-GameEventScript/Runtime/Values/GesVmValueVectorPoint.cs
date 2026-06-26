namespace StepH.GameEventScript.Runtime.Values;

internal sealed class GesVmValueVectorPoint(double x, double y, double z)
{
    internal readonly double X = x;
    internal readonly double Y = y;
    internal readonly double Z = z;

    public bool TryGetComponent(string key, out double value)
    {
        switch (key)
        {
            case "x":
                value = X;
                return true;
            case "y":
                value = Y;
                return true;
            case "z":
                value = Z;
                return true;
            default:
                value = 0;
                return false;
        }
    }
}
