// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Text;

namespace GameEventScript.Conformance;

/// <summary>
/// Represents a conformance received writer codes.
/// </summary>
public static class ConformanceReceivedWriterCodes
{
    /// <summary>
    /// Defines the invalid report value.
    /// </summary>
    public const string InvalidReport = "conformance.received.invalidReport";
    /// <summary>
    /// Defines the missing case value.
    /// </summary>
    public const string MissingCase = "conformance.received.missingCase";
    /// <summary>
    /// Defines the missing range value.
    /// </summary>
    public const string MissingRange = "conformance.received.missingRange";
    /// <summary>
    /// Defines the stale range value.
    /// </summary>
    public const string StaleRange = "conformance.received.staleRange";
    /// <summary>
    /// Defines the overlapping range value.
    /// </summary>
    public const string OverlappingRange = "conformance.received.overlappingRange";
}

/// <summary>
/// Represents a conformance received write exception.
/// </summary>
public sealed class ConformanceReceivedWriteException : Exception
{
    internal ConformanceReceivedWriteException(string code, string message) : base(message) => Code = code;
    /// <summary>
    /// Gets the code.
    /// </summary>
    public string Code { get; }
}

/// <summary>
/// Represents a conformance received markdown writer.
/// </summary>
public static class ConformanceReceivedMarkdownWriter
{
    private static readonly UTF8Encoding Utf8 = new(false, true);

    /// <summary>
    /// Converts this value to an array.
    /// </summary>
    /// <param name="document">The document value.</param>
    /// <param name="report">The report value.</param>
    /// <returns>The result of the operation.</returns>
    public static byte[] ToArray(ConformanceDocument document, ConformanceRunReport report)
    {
        _ = document ?? throw new ArgumentNullException(nameof(document));
        _ = report ?? throw new ArgumentNullException(nameof(report));
        var replacements = Collect(document, report);
        var original = Copy(document.Source.Utf8Bytes);
        if (replacements.Count == 0) return original;
        replacements.Sort(static (left, right) => left.Offset.CompareTo(right.Offset));
        for (var index = 1; index < replacements.Count; index++)
            if (replacements[index - 1].Offset + replacements[index - 1].Length > replacements[index].Offset)
                throw Error(ConformanceReceivedWriterCodes.OverlappingRange, "Received-output replacement ranges overlap.");

        long length = original.Length;
        for (var index = 0; index < replacements.Count; index++) length += replacements[index].Bytes.Length - replacements[index].Length;
        if (length > int.MaxValue) throw Error(ConformanceReceivedWriterCodes.InvalidReport, "The received document exceeds the supported byte size.");
        var output = new byte[(int)length];
        var inputOffset = 0;
        var outputOffset = 0;
        for (var index = 0; index < replacements.Count; index++)
        {
            var replacement = replacements[index];
            var prefix = replacement.Offset - inputOffset;
            Array.Copy(original, inputOffset, output, outputOffset, prefix);
            outputOffset += prefix;
            Array.Copy(replacement.Bytes, 0, output, outputOffset, replacement.Bytes.Length);
            outputOffset += replacement.Bytes.Length;
            inputOffset = replacement.Offset + replacement.Length;
        }
        Array.Copy(original, inputOffset, output, outputOffset, original.Length - inputOffset);
        return output;
    }

    /// <summary>
    /// Converts this value to a text.
    /// </summary>
    /// <param name="document">The document value.</param>
    /// <param name="report">The report value.</param>
    /// <returns>The result of the operation.</returns>
    public static string ToText(ConformanceDocument document, ConformanceRunReport report)
    {
        var bytes = ToArray(document, report);
        var offset = bytes.Length >= 3 && bytes[0] == 0xef && bytes[1] == 0xbb && bytes[2] == 0xbf ? 3 : 0;
        return Utf8.GetString(bytes, offset, bytes.Length - offset);
    }

    private static List<Replacement> Collect(ConformanceDocument document, ConformanceRunReport report)
    {
        var replacements = new List<Replacement>();
        var caseResults = new Dictionary<string, ConformanceCaseResult>(StringComparer.Ordinal);
        for (var index = 0; index < report.Cases.Count; index++)
        {
            var result = report.Cases[index];
            if (!string.Equals(result.SuiteId, document.SuiteId, StringComparison.Ordinal)) continue;
            if (!caseResults.TryAdd(result.CaseId, result)) throw Error(ConformanceReceivedWriterCodes.InvalidReport, "The report contains duplicate case results for this suite.");
        }
        if (document.Cases.Count > 0 && caseResults.Count == 0)
            throw Error(ConformanceReceivedWriterCodes.InvalidReport, "The report contains no results for the source suite.");

        for (var caseIndex = 0; caseIndex < document.Cases.Count; caseIndex++)
        {
            var testCase = document.Cases[caseIndex];
            if (!caseResults.TryGetValue(testCase.Id, out var result)) continue;
            if (!string.Equals(result.Id, testCase.FullId, StringComparison.Ordinal) ||
                !string.Equals(result.Title, testCase.Title, StringComparison.Ordinal) || result.Kind != testCase.Kind || result.Level != testCase.Level)
                throw Error(ConformanceReceivedWriterCodes.InvalidReport, "A report case does not match the source document metadata.");
            CollectPerformance(document, testCase, result, replacements);
            CollectAssembler(document, testCase, result, replacements);
        }

        foreach (var pair in caseResults)
        {
            var found = false;
            for (var index = 0; index < document.Cases.Count; index++) if (string.Equals(document.Cases[index].Id, pair.Key, StringComparison.Ordinal)) { found = true; break; }
            if (!found) throw Error(ConformanceReceivedWriterCodes.MissingCase, "The report references a case that is absent from the source document.");
        }
        return replacements;
    }

    private static void CollectPerformance(ConformanceDocument document, ConformanceCase testCase, ConformanceCaseResult result, List<Replacement> replacements)
    {
        if (result.Performance is null) return;
        var expectation = testCase.Expectation.Performance ?? throw Error(ConformanceReceivedWriterCodes.InvalidReport, "A performance result has no source expectation.");
        ConformancePerformanceProfile? profile = null;
        for (var index = 0; index < expectation.Profiles.Count; index++)
            if (string.Equals(expectation.Profiles[index].Id, result.Performance.ProfileId, StringComparison.Ordinal)) { profile = expectation.Profiles[index]; break; }
        if (profile is null) throw Error(ConformanceReceivedWriterCodes.InvalidReport, "The measured performance profile is absent from the source document.");
        if (profile.Metrics.Count != result.Performance.Metrics.Count) throw Error(ConformanceReceivedWriterCodes.InvalidReport, "The performance metric set differs between report and source document.");
        for (var resultIndex = 0; resultIndex < result.Performance.Metrics.Count; resultIndex++)
        {
            var measured = result.Performance.Metrics[resultIndex];
            ConformancePerformanceMetric? expected = null;
            for (var index = 0; index < profile.Metrics.Count; index++)
                if (string.Equals(profile.Metrics[index].Id, measured.Id, StringComparison.Ordinal)) { expected = profile.Metrics[index]; break; }
            if (expected is null || !string.Equals(expected.Reference, measured.Reference, StringComparison.Ordinal) || !string.Equals(expected.Unit, measured.Unit, StringComparison.Ordinal))
                throw Error(ConformanceReceivedWriterCodes.InvalidReport, "A performance result is stale or incompatible with the source expectation.");
            AddChecked(document, expected.ReferenceRange, expected.Reference, measured.Measured, replacements, preserveLineEndings: false);
        }
    }

    private static void CollectAssembler(ConformanceDocument document, ConformanceCase testCase, ConformanceCaseResult result, List<Replacement> replacements)
    {
        if (result.ActualAssembler is null) return;
        if (testCase.AssemblerPayloadRange is null || testCase.ExpectedAssembler is null)
            throw Error(ConformanceReceivedWriterCodes.MissingRange, "An assembler result has no GESA payload range in the source document.");
        AddChecked(document, testCase.AssemblerPayloadRange.Value, testCase.ExpectedAssembler, result.ActualAssembler, replacements, preserveLineEndings: true);
    }

    private static void AddChecked(ConformanceDocument document, ConformanceSourceRange range, string expectedLogical, string replacementLogical, List<Replacement> replacements, bool preserveLineEndings)
    {
        var bom = document.Source.HasByteOrderMark ? 3 : 0;
        var offset = checked(range.ByteOffset + bom);
        var source = document.Source.Utf8Bytes;
        if (range.ByteOffset < 0 || range.ByteLength < 0 || offset < bom || (long)offset + range.ByteLength > source.Count)
            throw Error(ConformanceReceivedWriterCodes.MissingRange, "A replacement range lies outside the source document.");
        var actual = new byte[range.ByteLength];
        for (var index = 0; index < actual.Length; index++) actual[index] = source[offset + index];
        var decoded = Utf8.GetString(actual);
        if (!string.Equals(NormalizeLf(decoded), NormalizeLf(expectedLogical), StringComparison.Ordinal))
            throw Error(ConformanceReceivedWriterCodes.StaleRange, "A replacement range does not contain its parsed expectation.");
        var replacement = preserveLineEndings ? ApplyLineEnding(NormalizeLf(replacementLogical), UnambiguousLineEnding(document.Source.Utf8Bytes)) : replacementLogical;
        replacements.Add(new Replacement(offset, range.ByteLength, Utf8.GetBytes(replacement)));
    }

    private static string ApplyLineEnding(string value, string lineEnding) => lineEnding == "\n" ? value : value.Replace("\n", lineEnding);
    private static string UnambiguousLineEnding(IReadOnlyList<byte> source)
    {
        var sawLf = false; var sawCrLf = false; var sawCr = false;
        for (var index = 0; index < source.Count; index++)
        {
            if (source[index] == 0x0d)
            {
                if (index + 1 < source.Count && source[index + 1] == 0x0a) { sawCrLf = true; index++; }
                else sawCr = true;
            }
            else if (source[index] == 0x0a) sawLf = true;
        }
        var count = (sawLf ? 1 : 0) + (sawCrLf ? 1 : 0) + (sawCr ? 1 : 0);
        if (count != 1) return "\n";
        return sawCrLf ? "\r\n" : sawCr ? "\r" : "\n";
    }
    private static string NormalizeLf(string value) => value.Replace("\r\n", "\n").Replace('\r', '\n');
    private static byte[] Copy(IReadOnlyList<byte> bytes) { var result = new byte[bytes.Count]; for (var index = 0; index < result.Length; index++) result[index] = bytes[index]; return result; }
    private static ConformanceReceivedWriteException Error(string code, string message) => new(code, message);

    private readonly struct Replacement
    {
        internal Replacement(int offset, int length, byte[] bytes) { Offset = offset; Length = length; Bytes = bytes; }
        internal int Offset { get; }
        internal int Length { get; }
        internal byte[] Bytes { get; }
    }
}
