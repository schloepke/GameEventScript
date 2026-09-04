using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace StepH.GameEventScript.Conformance;

/// <summary>
/// Represents a conformance corpus identity.
/// </summary>
public sealed class ConformanceCorpusIdentity
{
    internal ConformanceCorpusIdentity(int markdownFormatVersion, string sha256, int documentCount, int caseCount)
    {
        MarkdownFormatVersion = markdownFormatVersion;
        Sha256 = sha256;
        DocumentCount = documentCount;
        CaseCount = caseCount;
    }

    /// <summary>
    /// Gets the markdown format version.
    /// </summary>
    public int MarkdownFormatVersion { get; }
    /// <summary>
    /// Gets the sha256.
    /// </summary>
    public string Sha256 { get; }
    /// <summary>
    /// Gets the document count.
    /// </summary>
    public int DocumentCount { get; }
    /// <summary>
    /// Gets the case count.
    /// </summary>
    public int CaseCount { get; }
}

/// <summary>
/// Represents a conformance cross language result json writer.
/// </summary>
public static class ConformanceCrossLanguageResultJsonWriter
{
    private static readonly UTF8Encoding Utf8 = new(false, true);
    private static readonly byte[] HashDomain = Encoding.ASCII.GetBytes("GES-CONFORMANCE-CORPUS-V1\0");

    /// <summary>
    /// Performs the identify operation.
    /// </summary>
    /// <param name="documents">The documents value.</param>
    /// <returns>The result of the operation.</returns>
    public static ConformanceCorpusIdentity Identify(IReadOnlyList<ConformanceDocument> documents)
    {
        var ordered = ValidateAndOrderDocuments(documents, out var formatVersion, out var caseCount);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData(HashDomain);
        AppendUInt32(hash, checked((uint)ordered.Length));
        for (var index = 0; index < ordered.Length; index++)
        {
            var document = ordered[index];
            var suiteId = Utf8.GetBytes(document.SuiteId);
            AppendUInt32(hash, checked((uint)document.FormatVersion));
            AppendUInt32(hash, checked((uint)suiteId.Length));
            hash.AppendData(suiteId);
            AppendUInt32(hash, checked((uint)document.Source.Utf8Span.Length));
            hash.AppendData(document.Source.Utf8Span);
        }

        return new ConformanceCorpusIdentity(
            formatVersion,
            Hex(hash.GetHashAndReset()),
            ordered.Length,
            caseCount);
    }

    /// <summary>
    /// Converts this value to a text.
    /// </summary>
    /// <param name="documents">The documents value.</param>
    /// <param name="report">The report value.</param>
    /// <returns>The result of the operation.</returns>
    public static string ToText(IReadOnlyList<ConformanceDocument> documents, ConformanceRunReport report)
    {
        _ = report ?? throw new ArgumentNullException(nameof(report));
        var orderedDocuments = ValidateAndOrderDocuments(documents, out _, out var caseCount);
        var identity = Identify(orderedDocuments);
        var results = new Dictionary<string, ConformanceCaseResult>(StringComparer.Ordinal);
        for (var index = 0; index < report.Cases.Count; index++)
        {
            var result = report.Cases[index];
            if (!results.TryAdd(result.Id, result))
                throw new ArgumentException("The report contains duplicate case ID '" + result.Id + "'.", nameof(report));
        }
        if (results.Count != caseCount)
            throw new ArgumentException("The report does not contain exactly one result for every corpus case.", nameof(report));

        var entries = new List<Entry>(caseCount);
        for (var documentIndex = 0; documentIndex < orderedDocuments.Length; documentIndex++)
        {
            var document = orderedDocuments[documentIndex];
            for (var caseIndex = 0; caseIndex < document.Cases.Count; caseIndex++)
            {
                var testCase = document.Cases[caseIndex];
                if (!results.TryGetValue(testCase.FullId, out var result))
                    throw new ArgumentException("The report is missing case ID '" + testCase.FullId + "'.", nameof(report));
                if (result.Kind != testCase.Kind || result.Level != testCase.Level ||
                    !string.Equals(result.SuiteId, testCase.SuiteId, StringComparison.Ordinal) ||
                    !string.Equals(result.CaseId, testCase.Id, StringComparison.Ordinal))
                    throw new ArgumentException("The report metadata differs from corpus case '" + testCase.FullId + "'.", nameof(report));
                entries.Add(new Entry(testCase, result));
            }
        }
        entries.Sort((left, right) => StringComparer.Ordinal.Compare(left.TestCase.FullId, right.TestCase.FullId));

        var writer = new ConformanceCanonicalJsonWriter();
        writer.BeginObject();
        writer.Number("schemaVersion", 1);
        writer.Name("corpus");
        writer.BeginObject();
        writer.Number("markdownFormatVersion", identity.MarkdownFormatVersion);
        writer.String("sha256", identity.Sha256);
        writer.Number("documentCount", identity.DocumentCount);
        writer.Number("caseCount", identity.CaseCount);
        writer.EndObject();
        writer.Name("cases");
        writer.BeginArray();
        for (var index = 0; index < entries.Count; index++) WriteCase(writer, entries[index]);
        writer.EndArray();
        writer.EndObject();
        return writer.Complete();
    }

    /// <summary>
    /// Converts this value to an array.
    /// </summary>
    /// <param name="documents">The documents value.</param>
    /// <param name="report">The report value.</param>
    /// <returns>The result of the operation.</returns>
    public static byte[] ToArray(IReadOnlyList<ConformanceDocument> documents, ConformanceRunReport report)
        => Utf8.GetBytes(ToText(documents, report));

    private static void WriteCase(ConformanceCanonicalJsonWriter writer, Entry entry)
    {
        writer.BeginObject();
        writer.String("id", entry.TestCase.FullId);
        writer.String("kind", ConformanceResultJsonWriter.Name(entry.TestCase.Kind));
        writer.String("level", ConformanceResultJsonWriter.Name(entry.TestCase.Level));
        writer.Name("requires");
        writer.BeginObject();
        writer.Name("core");
        WriteSortedStrings(writer, entry.TestCase.Requires.Core);
        writer.Name("optional");
        WriteSortedStrings(writer, entry.TestCase.Requires.Optional);
        writer.EndObject();
        writer.String("status", ConformanceResultJsonWriter.Name(entry.Result.Status));
        writer.String("code", entry.Result.Code);
        writer.EndObject();
    }

    private static void WriteSortedStrings(ConformanceCanonicalJsonWriter writer, IReadOnlyList<string> values)
    {
        var copy = new string[values.Count];
        for (var index = 0; index < values.Count; index++) copy[index] = values[index];
        Array.Sort(copy, StringComparer.Ordinal);
        writer.BeginArray();
        for (var index = 0; index < copy.Length; index++) writer.StringValue(copy[index]);
        writer.EndArray();
    }

    private static ConformanceDocument[] ValidateAndOrderDocuments(IReadOnlyList<ConformanceDocument> documents, out int formatVersion, out int caseCount)
    {
        _ = documents ?? throw new ArgumentNullException(nameof(documents));
        if (documents.Count == 0) throw new ArgumentException("A cross-language corpus must contain at least one document.", nameof(documents));
        var ordered = new ConformanceDocument[documents.Count];
        var suiteIds = new HashSet<string>(StringComparer.Ordinal);
        var caseIds = new HashSet<string>(StringComparer.Ordinal);
        formatVersion = -1;
        caseCount = 0;
        for (var index = 0; index < documents.Count; index++)
        {
            var document = documents[index] ?? throw new ArgumentException("The corpus contains a null document.", nameof(documents));
            if (!suiteIds.Add(document.SuiteId))
                throw new ArgumentException("The corpus contains duplicate suite ID '" + document.SuiteId + "'.", nameof(documents));
            if (formatVersion < 0) formatVersion = document.FormatVersion;
            else if (formatVersion != document.FormatVersion)
                throw new ArgumentException("All corpus documents must use the same Markdown format version.", nameof(documents));
            for (var caseIndex = 0; caseIndex < document.Cases.Count; caseIndex++)
            {
                if (!caseIds.Add(document.Cases[caseIndex].FullId))
                    throw new ArgumentException("The corpus contains duplicate case ID '" + document.Cases[caseIndex].FullId + "'.", nameof(documents));
                caseCount = checked(caseCount + 1);
            }
            ordered[index] = document;
        }
        Array.Sort(ordered, (left, right) => StringComparer.Ordinal.Compare(left.SuiteId, right.SuiteId));
        return ordered;
    }

    private static void AppendUInt32(IncrementalHash hash, uint value)
    {
        Span<byte> bytes = stackalloc byte[4];
        bytes[0] = (byte)value;
        bytes[1] = (byte)(value >> 8);
        bytes[2] = (byte)(value >> 16);
        bytes[3] = (byte)(value >> 24);
        hash.AppendData(bytes);
    }

    private static string Hex(byte[] bytes)
    {
        const string digits = "0123456789ABCDEF";
        var result = new char[bytes.Length * 2];
        for (var index = 0; index < bytes.Length; index++)
        {
            result[index * 2] = digits[bytes[index] >> 4];
            result[index * 2 + 1] = digits[bytes[index] & 0x0f];
        }
        return new string(result);
    }

    private readonly struct Entry
    {
        internal Entry(ConformanceCase testCase, ConformanceCaseResult result)
        {
            TestCase = testCase;
            Result = result;
        }

        internal ConformanceCase TestCase { get; }
        internal ConformanceCaseResult Result { get; }
    }
}
