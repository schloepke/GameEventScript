namespace StepH.GameEventScript.Runtime.VM;

internal sealed class GesVmCustomObject(string typeName, GesVmValueMap map)
{
    internal string TypeName { get; } = typeName;
    internal GesVmValueMap Map { get; } = map;
    internal int Length => Map.Length;
}
