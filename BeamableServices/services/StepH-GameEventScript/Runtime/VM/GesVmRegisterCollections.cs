using static StepH.GameEventScript.Api.GameEventScriptBytecodeTypeKind;
using StepH.GameEventScript.Runtime.Values;

namespace StepH.GameEventScript.Runtime.VM;

internal static class GesVmRegisterCollections
{
    internal static void GesVmCreateList(this GesVmState vmState, ushort destinationRegister)
    {
        var list = new GesValue[vmState.StageLength];
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

        var map = new GesVmMapBuilder(vmState.StageLength);
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

    internal static void GesVmListBuilderAdd(this GesVmState vmState, in GesValue listBuilder, in GesValue value)
    {
        if (listBuilder.Kind is ListBuilder && listBuilder.ObjectValue is GesVmListBuilder builder) builder.Add(value);
    }

    internal static void GesVmListBuilderFinish(this GesVmState vmState, ushort destinationRegister, in GesValue listBuilder)
    {
        if (listBuilder.Kind is ListBuilder && listBuilder.ObjectValue is GesVmListBuilder builder)
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

    internal static void GesVmMapBuilderAdd(this GesVmState vmState, in GesValue mapBuilder, in GesValue keyValue, in GesValue value)
    {
        if (mapBuilder.Kind is not MapBuilder || mapBuilder.ObjectValue is not GesVmMapBuilder builder) return;
        var key = keyValue.Kind switch
        {
            Text or Tag => keyValue.TextValue,
            Nothing => string.Empty,
            _ => keyValue.ToText
        };
        if (key.Length == 0) return;
        builder.Set(key, value);
    }

    internal static void GesVmMapBuilderFinish(this GesVmState vmState, ushort destinationRegister, in GesValue mapBuilder)
    {
        if (mapBuilder.Kind is MapBuilder && mapBuilder.ObjectValue is GesVmMapBuilder builder)
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

    internal static void GesVmDistinctBuilderAdd(this GesVmState vmState, in GesValue distinctBuilder, in GesValue keyValue, in GesValue value)
    {
        if (distinctBuilder.Kind is DistinctBuilder && distinctBuilder.ObjectValue is GesVmDistinctBuilder builder) builder.Add(in keyValue, in value);
    }

    internal static void GesVmDistinctBuilderFinish(this GesVmState vmState, ushort destinationRegister, in GesValue distinctBuilder)
    {
        if (distinctBuilder.Kind is DistinctBuilder && distinctBuilder.ObjectValue is GesVmDistinctBuilder builder)
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

    internal static void GesVmGroupBuilderAdd(this GesVmState vmState, in GesValue groupBuilder, in GesValue keyValue, in GesValue value)
    {
        if (groupBuilder.Kind is not GroupBuilder || groupBuilder.ObjectValue is not GesVmGroupBuilder builder) return;
        var key = keyValue.Kind is Text or Tag ? keyValue.TextValue : keyValue.ToText;
        builder.Add(key, value);
    }

    internal static void GesVmGroupBuilderFinish(this GesVmState vmState, ushort destinationRegister, in GesValue groupBuilder)
    {
        if (groupBuilder.Kind is GroupBuilder && groupBuilder.ObjectValue is GesVmGroupBuilder builder)
        {
            var result = new GesValue();
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

    internal static void GesVmOrderBuilderAdd(this GesVmState vmState, in GesValue orderBuilder, in GesValue keyValue, in GesValue value)
    {
        if (orderBuilder.Kind is OrderBuilder && orderBuilder.ObjectValue is GesVmOrderBuilder builder) builder.Add(in keyValue, in value);
    }

    internal static void GesVmOrderBuilderFinishAscending(this GesVmState vmState, ushort destinationRegister, in GesValue orderBuilder)
    {
        if (orderBuilder.Kind is OrderBuilder && orderBuilder.ObjectValue is GesVmOrderBuilder builder && builder.TryToList(descending: false, out var list))
        {
            vmState.SetList(destinationRegister, list);
            return;
        }

        vmState.SetNothing(destinationRegister);
    }

    internal static void GesVmOrderBuilderFinishDescending(this GesVmState vmState, ushort destinationRegister, in GesValue orderBuilder)
    {
        if (orderBuilder.Kind is OrderBuilder && orderBuilder.ObjectValue is GesVmOrderBuilder builder && builder.TryToList(descending: true, out var list))
        {
            vmState.SetList(destinationRegister, list);
            return;
        }

        vmState.SetNothing(destinationRegister);
    }
}
