namespace StepH.GameEventScript.Runtime.Values;

internal sealed class GesValueVectorPoint(double x, double y, double z)
{
    internal readonly double X = x;
    internal readonly double Y = y;
    internal readonly double Z = z;

    public double? GetComponent(string key)
    {
        return key switch
        {
            "x" => X,
            "y" => Y,
            "z" => Z,
            _ => null
        };
    }
}
