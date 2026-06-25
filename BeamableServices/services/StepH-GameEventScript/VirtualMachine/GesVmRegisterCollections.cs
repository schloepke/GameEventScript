using static StepH.GameEventScript.Api.GameEventScriptBytecodeTypeKind;

namespace StepH.GameEventScript.VirtualMachine;

internal static class GesVmRegisterCollections
{
    internal static void GesVmCreateList(this GesVmState vmState, ushort destinationRegister)
    {
        var list = new GesVmValue[vmState.StageLength];
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

        var map = new GesVmValueMapBuilder(vmState.StageLength);
        for (ushort i = 0; i < vmState.StageLength; i++)
        {
            map.Set(vmState.FetchStringByPointer(keyNames[i]), vmState.RegisterStaged(i));
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

    internal static void GesVmCreateMapBuilder(this GesVmState vmState, ushort destinationRegister)
    {
        vmState.CreateMapBuilder(destinationRegister);
    }

    internal static void GesVmMapBuilderAdd(this GesVmState vmState, in GesVmValue mapBuilder, in GesVmValue keyValue, in GesVmValue value)
    {
        if (mapBuilder.Kind is not MapBuilder || mapBuilder.ObjectValue is not GesVmValueMapBuilder builder) return;
        var key = keyValue.Kind switch
        {
            Text or Tag => keyValue.TextValue,
            Nothing => string.Empty,
            _ => keyValue.ToText
        };
        if (key.Length == 0) return;
        builder.Set(key, value);
    }

    internal static void GesVmMapBuilderFinish(this GesVmState vmState, ushort destinationRegister, in GesVmValue mapBuilder)
    {
        if (mapBuilder.Kind is MapBuilder && mapBuilder.ObjectValue is GesVmValueMapBuilder builder)
        {
            vmState.SetMap(destinationRegister, builder.ToMap());
            return;
        }

        vmState.SetNothing(destinationRegister);
    }

    internal static void GesVmCreateDistinctBuilder(this GesVmState vmState, ushort destinationRegister)
    {
        vmState.CreateDistinctBuilder(destinationRegister);
    }

    internal static void GesVmDistinctBuilderAdd(this GesVmState vmState, in GesVmValue distinctBuilder, in GesVmValue keyValue, in GesVmValue value)
    {
        if (distinctBuilder.Kind is DistinctBuilder && distinctBuilder.ObjectValue is GesVmValueDistinctBuilder builder) builder.Add(in keyValue, in value);
    }

    internal static void GesVmDistinctBuilderFinish(this GesVmState vmState, ushort destinationRegister, in GesVmValue distinctBuilder)
    {
        if (distinctBuilder.Kind is DistinctBuilder && distinctBuilder.ObjectValue is GesVmValueDistinctBuilder builder)
        {
            vmState.SetList(destinationRegister, builder.ToList());
            return;
        }

        vmState.SetNothing(destinationRegister);
    }

    internal static void GesVmCreateGroupBuilder(this GesVmState vmState, ushort destinationRegister)
    {
        vmState.CreateGroupBuilder(destinationRegister);
    }

    internal static void GesVmGroupBuilderAdd(this GesVmState vmState, in GesVmValue groupBuilder, in GesVmValue keyValue, in GesVmValue value)
    {
        if (groupBuilder.Kind is not GroupBuilder || groupBuilder.ObjectValue is not GesVmValueGroupBuilder builder) return;
        var key = keyValue.Kind is Text or Tag ? keyValue.TextValue : keyValue.ToText;
        builder.Add(key, value);
    }

    internal static void GesVmGroupBuilderFinish(this GesVmState vmState, ushort destinationRegister, in GesVmValue groupBuilder)
    {
        if (groupBuilder.Kind is GroupBuilder && groupBuilder.ObjectValue is GesVmValueGroupBuilder builder)
        {
            var result = new GesVmValue();
            builder.WriteTo(ref result);
            vmState.SetValue(destinationRegister, in result);
            return;
        }

        vmState.SetNothing(destinationRegister);
    }

    internal static void GesVmCreateOrderBuilder(this GesVmState vmState, ushort destinationRegister)
    {
        vmState.CreateOrderBuilder(destinationRegister);
    }

    internal static void GesVmOrderBuilderAdd(this GesVmState vmState, in GesVmValue orderBuilder, in GesVmValue keyValue, in GesVmValue value)
    {
        if (orderBuilder.Kind is OrderBuilder && orderBuilder.ObjectValue is GesVmValueOrderBuilder builder) builder.Add(in keyValue, in value);
    }

    internal static void GesVmOrderBuilderFinishAscending(this GesVmState vmState, ushort destinationRegister, in GesVmValue orderBuilder)
    {
        if (orderBuilder.Kind is OrderBuilder && orderBuilder.ObjectValue is GesVmValueOrderBuilder builder && builder.TryToList(descending: false, out var list))
        {
            vmState.SetList(destinationRegister, list);
            return;
        }

        vmState.SetNothing(destinationRegister);
    }

    internal static void GesVmOrderBuilderFinishDescending(this GesVmState vmState, ushort destinationRegister, in GesVmValue orderBuilder)
    {
        if (orderBuilder.Kind is OrderBuilder && orderBuilder.ObjectValue is GesVmValueOrderBuilder builder && builder.TryToList(descending: true, out var list))
        {
            vmState.SetList(destinationRegister, list);
            return;
        }

        vmState.SetNothing(destinationRegister);
    }
}
