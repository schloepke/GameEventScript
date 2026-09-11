// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

namespace GameEventScript.Tests.Conformance;

[TestClass]
[DoNotParallelize]
public sealed class ConformanceAllocationMeasurementTests
{
    [TestMethod]
    [TestCategory("Performance")]
    [TestCategory("Allocation")]
    [DataRow(false)]
    [DataRow(true)]
    public void AllocationCounterDistinguishesReuseFromEscapingAllocations(bool allocate)
    {
        const int count = 1000;
        var retained = new byte[count][];
        var existing = new byte[32];
        Action action = () =>
        {
            for (var index = 0; index < retained.Length; index++) retained[index] = allocate ? new byte[32] : existing;
        };
        action();
        _ = ConformanceCSharpAllocationProvider.MeasureBytes(action);

        var measured = ConformanceCSharpAllocationProvider.MeasureBytes(action);

        if (allocate) Assert.IsGreaterThanOrEqualTo(count * 32L, measured.AllocatedBytes, "Escaping allocations must be visible to the workload counter.");
        else Assert.AreEqual(0L, measured.AllocatedBytes, "Reusing existing values and calling the measurement harness must not allocate.");
        GC.KeepAlive(retained);
    }
}
