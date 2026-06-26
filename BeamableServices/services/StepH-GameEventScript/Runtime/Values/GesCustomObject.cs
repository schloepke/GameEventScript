namespace StepH.GameEventScript.Runtime.Values;

internal sealed class GesCustomObject(string typeName, GesValueMap map)
{
    internal string TypeName { get; } = typeName;
    internal GesValueMap Map { get; } = map;
    internal int Length => Map.Length;
}
