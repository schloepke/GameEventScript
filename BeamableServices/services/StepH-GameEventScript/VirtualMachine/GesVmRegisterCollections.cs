using static StepH.GameEventScript.Api.GameEventScriptBytecodeTypeKind;

namespace StepH.GameEventScript.VirtualMachine;

internal static class GesVmRegisterCollections
{
    internal static void GesVmCreateList(this GesVmState vmState, ushort destinationRegister)
    {
        var list = vmState.CreateList(vmState.StageLength);
        for (ushort i = 0; i < vmState.StageLength; i++) list[i] = vmState.RegisterStaged(i);
        vmState.SetList(destinationRegister, list);
    }

    internal static void GesVmCreateMap(this GesVmState vmState, ushort destinationRegister, ushort keyNamesIndex)
    {
        var keyNames = vmState.Binary.Uint16ConstantTable.Resolve(keyNamesIndex);
        if (keyNames.Length != vmState.StageLength)
        {
            vmState.SetNothing(destinationRegister);
            return;
        }

        var map = new GesVmValueMapBuilder(vmState, vmState.StageLength);
        for (ushort i = 0; i < vmState.StageLength; i++)
        {
            map.Set(vmState.Binary.TextConstantTable.Resolve(keyNames[i]), vmState.RegisterStaged(i));
        }

        vmState.SetMap(destinationRegister, map.ToMap());
    }

    internal static void GesVmCreateListBuilder(this GesVmState vmState, ushort destinationRegister)
    {
        vmState.CreateListBuilder(destinationRegister);
    }

    internal static void GesVmListBuilderAdd(this GesVmState vmState, in GesVmValue listBuilder, in GesVmValue value)
    {
        if (listBuilder.Kind is ListBuilder && listBuilder.ObjectValue is GesVmValueListBuilder builder) builder.Add(value);
    }

    internal static void GesVmListBuilderFinish(this GesVmState vmState, ushort destinationRegister, in GesVmValue listBuilder)
    {
        if (listBuilder.Kind is ListBuilder && listBuilder.ObjectValue is GesVmValueListBuilder builder)
        {
            vmState.SetList(destinationRegister, builder.ToList());
            return;
        }

        vmState.SetNothing(destinationRegister);
    }
}
