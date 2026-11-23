// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using GameEventScript.Api;
using GameEventScript.Compiler;

namespace GameEventScript.Tests.Native.Compiler;

// The reduction of tag alternatives must not compare every pair of groups when many groups share a tag.
// Conformance pins which groups remain; the cost is an implementation property.
[TestClass]
public sealed class GesTagAlternativeReductionTests
{
    private const int GroupCount = 8_000;

    [TestMethod]
    public void GroupsSharingTheirFirstTagAreReducedWithoutComparingEveryPair()
    {
        var left = Compile(index => $"#common | #t{index}", out var leftProgram);
        var right = Compile(index => $"#t{index} | #common", out var rightProgram);
        Assert.AreEqual(GroupCount, Alternatives(leftProgram));
        Assert.AreEqual(GroupCount, Alternatives(rightProgram));
        // Before, the shared first tag listed every group as a candidate of every other: about 2 GB against 8.5 MB.
        Assert.IsLessThan(right * 2, left, $"left {left} bytes, right {right} bytes");
    }

    [TestMethod]
    public void RepeatedAndNestedGroupsAreReducedWithoutComparingEveryPair()
    {
        var unique = Compile(index => $"#t{index} | #u{index}", out _);
        // Every group repeats the first, or contains it and another tag.
        var repeated = Compile(index => index % 2 == 0 ? "#a | #b" : $"#a | #b | #t{index}", out var program);
        Assert.AreEqual(1, Alternatives(program));
        Assert.IsLessThan(unique * 2, repeated, $"repeated {repeated} bytes, unique {unique} bytes");
    }

    private static long Compile(Func<int, string> group, out GameEventScriptProgram program)
    {
        var source = "module tags\non Probe() matching " + string.Join(", ", Enumerable.Range(0, GroupCount).Select(group)) + " {}\n";
        GameEventScriptProgram? compiled = null;
        var bytes = AllocationMeasurement.MeasureBytes(() => compiled = new GesProgramBuilder().AddScript(source).WithDebugInfo(GesDebugInfoOptions.None).Compile());
        program = compiled!;
        return bytes;
    }

    private static int Alternatives(GameEventScriptProgram program) => program.Bindings.Entries.Single(entry => entry.RequiredTagAlternatives.Count > 0).RequiredTagAlternatives.Count;
}
