using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using StepH.GameEventScript.Api;
using static StepH.GameEventScript.Api.GameEventScriptBytecodeTypeKind;

namespace StepH.GameEventScript.BytecodeExecutor;

internal static class VmRegisterCollectionOperators
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmLength(ref this VmValue dst, ref VmValue a, ref GameEventScriptTextTable textTable)
    {
        switch (a.Kind)
        {
            case Text when a.IsStoragePointer:
                dst.SetInteger(textTable.Resolve((ushort)a.IntegerValue).Length);
                break;
            case Text when a is { IsStorageObject: true, ObjectValue: string text }:
                dst.SetInteger(text.Length);
                break;
            case List or Map or Dice or GameEventScriptBytecodeTypeKind.Range:
                dst.SetInteger(a.IntegerValue);
                break;
            case Nothing:
                dst.SetInteger(0);
                break;
            default:
                dst.SetNothing();
                break;
        }
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmStartsWith(ref this VmValue dst, ref VmValue a, ref VmValue b, ref GameEventScriptTextTable textTable)
    {
        switch (a.Kind)
        {
            case Text when a.IsStoragePointer && b.Kind is Text && b.IsStoragePointer:
                dst.SetBoolean(textTable.Resolve((ushort)a.IntegerValue).StartsWith(textTable.Resolve((ushort)b.IntegerValue)));
                break;
            case Text when a.IsStoragePointer && b.Kind is Text && b is { IsStorageObject: true, ObjectValue: string startsWith }:
                dst.SetBoolean(textTable.Resolve((ushort)a.IntegerValue).StartsWith(startsWith));
                break;
            case Text when a is { IsStorageObject: true, ObjectValue: string text } && b.Kind is Text && b.IsStoragePointer:
                dst.SetBoolean(text.StartsWith(textTable.Resolve((ushort)b.IntegerValue)));
                break;
            case Text when a is { IsStorageObject: true, ObjectValue: string text } && b.Kind is Text && b is { IsStorageObject: true, ObjectValue: string startsWith }:
                dst.SetBoolean(text.StartsWith(startsWith));
                break;
            case List or Dice or GameEventScriptBytecodeTypeKind.Range:
                dst.SetBoolean(false);
                break;
            default:
                dst.SetBoolean(false);
                break;
        }
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmEndsWith(ref this VmValue dst, ref VmValue a, ref VmValue b, ref GameEventScriptTextTable textTable)
    {
        switch (a.Kind)
        {
            case Text when a.IsStoragePointer && b.Kind is Text && b.IsStoragePointer:
                dst.SetBoolean(textTable.Resolve((ushort)a.IntegerValue).EndsWith(textTable.Resolve((ushort)b.IntegerValue)));
                break;
            case Text when a.IsStoragePointer && b.Kind is Text && b is { IsStorageObject: true, ObjectValue: string startsWith }:
                dst.SetBoolean(textTable.Resolve((ushort)a.IntegerValue).EndsWith(startsWith));
                break;
            case Text when a is { IsStorageObject: true, ObjectValue: string text } && b.Kind is Text && b.IsStoragePointer:
                dst.SetBoolean(text.EndsWith(textTable.Resolve((ushort)b.IntegerValue)));
                break;
            case Text when a is { IsStorageObject: true, ObjectValue: string text } && b.Kind is Text && b is { IsStorageObject: true, ObjectValue: string startsWith }:
                dst.SetBoolean(text.EndsWith(startsWith));
                break;
            case List or Dice or GameEventScriptBytecodeTypeKind.Range:
                dst.SetBoolean(false);
                break;
            default:
                dst.SetBoolean(false);
                break;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmContains(ref this VmValue dst, ref VmValue a, ref VmValue b, ref GameEventScriptTextTable textTable)
    {
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmContainsValue(ref this VmValue dst, ref VmValue a, ref VmValue b, ref GameEventScriptTextTable textTable)
    {
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmIntersect(ref this VmValue dst, ref VmValue a, ref VmValue b, ref GameEventScriptTextTable textTable)
    {
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmCombine(ref this VmValue dst, ref VmValue a, ref VmValue b, ref GameEventScriptTextTable textTable)
    {
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmExcept(ref this VmValue dst, ref VmValue a, ref VmValue b, ref GameEventScriptTextTable textTable)
    {
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmZip(ref this VmValue dst, ref VmValue a, ref VmValue b, ref GameEventScriptTextTable textTable)
    {
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmValues(ref this VmValue dst, ref VmValue a)
    {
        switch (a.Kind)
        {
            case Map when a.ObjectValue is VmMapObject map:
                dst.SetList(map.ValueList);
                break;
            default:
                dst.SetNothing();
                break;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmKeys(ref this VmValue dst, ref VmValue a)
    {
        switch (a.Kind)
        {
            case Map when a.ObjectValue is VmMapObject map:
                dst.SetList(map.KeyList);
                break;
            default:
                dst.SetNothing();
                break;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmEntries(ref this VmValue dst, ref VmValue a)
    {
        switch (a.Kind)
        {
            case Map when a.ObjectValue is VmMapObject map:
                dst.SetList(map.EntryList);
                break;
            default:
                dst.SetNothing();
                break;
        }
    }
    
}