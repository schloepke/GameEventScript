namespace StepH.GameEventScript.VirtualMachine;

internal sealed class GesVmCustomObject(string typeName, GesVmValueMap map)
{
    internal string TypeName { get; } = typeName;
    internal GesVmValueMap Map { get; } = map;
    internal int Length => Map.Length;
}
