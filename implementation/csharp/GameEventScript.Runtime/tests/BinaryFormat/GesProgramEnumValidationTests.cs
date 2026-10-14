// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using GameEventScript.Api;

namespace GameEventScript.Tests.Native.BinaryFormat;

[TestClass]
public sealed class GesProgramEnumValidationTests
{
    [TestMethod]
    public void ValidityChecksMatchEveryRepresentableTransportValue()
    {
        Verify<GesBytecodeOpCode>(byte.MaxValue, GesProgramEnumValidation.IsDefined);
        Verify<GesBinaryBindKind>(byte.MaxValue, GesProgramEnumValidation.IsDefined);
        Verify<GesQuantityUnit>(byte.MaxValue, GesProgramEnumValidation.IsDefined);
        Verify<GesBytecodeTypeKind>(byte.MaxValue, GesProgramEnumValidation.IsDefined);
        Verify<GesBytecodePatternKind>(ushort.MaxValue, GesProgramEnumValidation.IsDefined);
        Verify<GesBytecodeSeriesKind>(ushort.MaxValue, GesProgramEnumValidation.IsDefined);
        Verify<GesDebugSymbolKind>(byte.MaxValue, GesProgramEnumValidation.IsDefined);
    }

    [TestMethod]
    [TestCategory("Performance")]
    [TestCategory("Allocation")]
    public void ValidityChecksAllocateNothingAfterFullCollection()
    {
        var expected = CountValidValues();
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var before = GC.GetAllocatedBytesForCurrentThread();
        var actual = CountValidValues();
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.AreEqual(expected, actual);
        Assert.AreEqual(0L, allocated);
    }

    private static void Verify<T>(int maximum, Func<T, bool> isDefined) where T : struct, Enum
    {
        var declared = Enum.GetValues<T>().ToHashSet();
        for (var raw = 0; raw <= maximum; raw++)
        {
            var value = (T)Enum.ToObject(typeof(T), raw);
            Assert.AreEqual(declared.Contains(value), isDefined(value), $"{typeof(T).Name}: 0x{raw:X4}");
        }
    }

    private static int CountValidValues()
    {
        var count = 0;
        for (var raw = 0; raw <= ushort.MaxValue; raw++)
        {
            if (GesProgramEnumValidation.IsDefined((GesBytecodePatternKind)raw)) count++;
            if (GesProgramEnumValidation.IsDefined((GesBytecodeSeriesKind)raw)) count++;
            if (raw > byte.MaxValue) continue;
            if (GesProgramEnumValidation.IsDefined((GesBytecodeOpCode)raw)) count++;
            if (GesProgramEnumValidation.IsDefined((GesBinaryBindKind)raw)) count++;
            if (GesProgramEnumValidation.IsDefined((GesQuantityUnit)raw)) count++;
            if (GesProgramEnumValidation.IsDefined((GesBytecodeTypeKind)raw)) count++;
            if (GesProgramEnumValidation.IsDefined((GesDebugSymbolKind)raw)) count++;
        }
        return count;
    }
}
