// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using GameEventScript.Runtime.Values;
using static GameEventScript.Api.GameEventScriptBytecodeTypeKind;

namespace GameEventScript.Runtime.VM;

internal static class GesVmRegisterCollections
{
    internal static void GesVmCreateList(this GesVirtualMachine vm, ushort destinationRegister)
    {
        var list = new GesValue[vm.StageLength];
        for (ushort i = 0; i < vm.StageLength; i++) list[i] = vm.RegisterStaged(i);
        vm.SetList(destinationRegister, list);
    }

    internal static void GesVmCreateMap(this GesVirtualMachine vm, ushort destinationRegister, ushort keyNamesIndex)
    {
        var layout = vm.ActiveProgram!.MapLayout(keyNamesIndex);
        if (layout.InputCount != vm.StageLength)
        {
            vm.SetNothing(destinationRegister);
            return;
        }

        var values = new GesValue[layout.Keys.Length];
        for (var i = 0; i < values.Length; i++) values[i] = vm.RegisterStaged((ushort)layout.Sources[i]);
        vm.SetMap(destinationRegister, new GesValueMap(layout.Keys, values));
    }

    internal static void GesVmCreateListBuilder(this GesVirtualMachine vm, ushort destinationRegister)
    {
        vm.CreateListBuilder(destinationRegister);
    }

    internal static void GesVmListBuilderAdd(this GesVirtualMachine vm, in GesValue listBuilder, in GesValue value, GesRuntimeBudget budget)
    {
        if (listBuilder.Kind is ListBuilder && listBuilder.ObjectValue is GesVmListBuilder builder && budget.CheckGeneratedCollectionItemCountWithinLimit((long)builder.Count + 1)) builder.Add(value);
    }

    internal static void GesVmListBuilderFinish(this GesVirtualMachine vm, ushort destinationRegister, in GesValue listBuilder)
    {
        if (listBuilder.Kind is ListBuilder && listBuilder.ObjectValue is GesVmListBuilder builder)
        {
            vm.SetList(destinationRegister, builder.ToList());
            return;
        }

        vm.SetNothing(destinationRegister);
    }

    internal static void GesVmCreateMapBuilder(this GesVirtualMachine vm, ushort destinationRegister)
    {
        vm.CreateMapBuilder(destinationRegister);
    }

    internal static void GesVmMapBuilderAdd(this GesVirtualMachine vm, in GesValue mapBuilder, in GesValue keyValue, in GesValue value, GesRuntimeBudget budget)
    {
        if (mapBuilder.Kind is not MapBuilder || mapBuilder.ObjectValue is not GesVmMapBuilder builder) return;
        var key = keyValue.Kind switch
        {
            Text or Tag => keyValue.TextValue,
            Nothing => string.Empty,
            _ => keyValue.ToText
        };
        if (key.Length == 0) return;
        builder.Set(key, value, budget);
    }

    internal static void GesVmMapBuilderFinish(this GesVirtualMachine vm, ushort destinationRegister, in GesValue mapBuilder)
    {
        if (mapBuilder.Kind is MapBuilder && mapBuilder.ObjectValue is GesVmMapBuilder builder)
        {
            vm.SetMap(destinationRegister, builder.ToMap());
            return;
        }

        vm.SetNothing(destinationRegister);
    }

    internal static void GesVmCreateDistinctBuilder(this GesVirtualMachine vm, ushort destinationRegister)
    {
        vm.CreateDistinctBuilder(destinationRegister);
    }

    internal static void GesVmDistinctBuilderAdd(this GesVirtualMachine vm, in GesValue distinctBuilder, in GesValue keyValue, in GesValue value, GesRuntimeBudget budget)
    {
        if (distinctBuilder.Kind is DistinctBuilder && distinctBuilder.ObjectValue is GesVmDistinctBuilder builder) builder.Add(in keyValue, in value, budget);
    }

    internal static void GesVmDistinctBuilderFinish(this GesVirtualMachine vm, ushort destinationRegister, in GesValue distinctBuilder)
    {
        if (distinctBuilder.Kind is DistinctBuilder && distinctBuilder.ObjectValue is GesVmDistinctBuilder builder)
        {
            vm.SetList(destinationRegister, builder.ToList());
            return;
        }

        vm.SetNothing(destinationRegister);
    }

    internal static void GesVmCreateGroupBuilder(this GesVirtualMachine vm, ushort destinationRegister)
    {
        vm.CreateGroupBuilder(destinationRegister);
    }

    internal static void GesVmGroupBuilderAdd(this GesVirtualMachine vm, in GesValue groupBuilder, in GesValue keyValue, in GesValue value, GesRuntimeBudget budget)
    {
        if (groupBuilder.Kind is not GroupBuilder || groupBuilder.ObjectValue is not GesVmGroupBuilder builder) return;
        var key = keyValue.Kind is Text or Tag ? keyValue.TextValue : keyValue.ToText;
        builder.Add(key, value, budget);
    }

    internal static void GesVmGroupBuilderFinish(this GesVirtualMachine vm, ushort destinationRegister, in GesValue groupBuilder)
    {
        if (groupBuilder.Kind is GroupBuilder && groupBuilder.ObjectValue is GesVmGroupBuilder builder)
        {
            var result = builder.ToMapValue();
            vm.SetValue(destinationRegister, in result);
            return;
        }

        vm.SetNothing(destinationRegister);
    }

    internal static void GesVmCreateOrderBuilder(this GesVirtualMachine vm, ushort destinationRegister)
    {
        vm.CreateOrderBuilder(destinationRegister);
    }

    internal static void GesVmOrderBuilderAdd(this GesVirtualMachine vm, in GesValue orderBuilder, in GesValue keyValue, in GesValue value, GesRuntimeBudget budget)
    {
        if (orderBuilder.Kind is OrderBuilder && orderBuilder.ObjectValue is GesVmOrderBuilder builder) builder.Add(in keyValue, in value, budget);
    }

    internal static void GesVmOrderBuilderFinishAscending(this GesVirtualMachine vm, ushort destinationRegister, in GesValue orderBuilder)
    {
        if (orderBuilder.Kind is OrderBuilder && orderBuilder.ObjectValue is GesVmOrderBuilder builder && builder.ToList(descending: false) is { } list)
        {
            vm.SetList(destinationRegister, list);
            return;
        }

        vm.SetNothing(destinationRegister);
    }

    internal static void GesVmOrderBuilderFinishDescending(this GesVirtualMachine vm, ushort destinationRegister, in GesValue orderBuilder)
    {
        if (orderBuilder.Kind is OrderBuilder && orderBuilder.ObjectValue is GesVmOrderBuilder builder && builder.ToList(descending: true) is { } list)
        {
            vm.SetList(destinationRegister, list);
            return;
        }

        vm.SetNothing(destinationRegister);
    }
}
