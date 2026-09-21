// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System.Reflection;
using GameEventScript.Conformance;

namespace GameEventScript.Tests.Conformance;

internal static class ConformanceMSTestAdapter
{
    internal static IEnumerable<object[]> Cases(ConformanceDocument document)
    {
        for (var index = 0; index < document.Cases.Count; index++)
            yield return [document, document.Cases[index].Id];
    }

    internal static string DisplayName(MethodInfo method, object[] data)
        => data is [ConformanceDocument document, string caseId]
            ? Find(document, caseId).FullId
            : method.Name;

    internal static void AssertPassed(ConformanceCaseResult result, TestContext context)
    {
        context.WriteLine(result.Id + ": " + result.Status + " (" + result.Code + ")");
        for (var index = 0; index < result.Mismatches.Count; index++)
        {
            var mismatch = result.Mismatches[index];
            context.WriteLine(mismatch.Path + ": expected " + mismatch.Expected + ", actual " + mismatch.Actual);
        }

        if (result.Status == ConformanceCaseStatus.Skipped) Assert.Inconclusive(result.Code + ": " + string.Join(", ", result.MissingCapabilities));
        Assert.AreEqual(ConformanceCaseStatus.Passed, result.Status, result.Code);
    }

    private static ConformanceCase Find(ConformanceDocument document, string caseId)
    {
        for (var index = 0; index < document.Cases.Count; index++)
            if (string.Equals(document.Cases[index].Id, caseId, StringComparison.Ordinal)) return document.Cases[index];
        throw new ArgumentException("Unknown conformance case.", nameof(caseId));
    }
}
