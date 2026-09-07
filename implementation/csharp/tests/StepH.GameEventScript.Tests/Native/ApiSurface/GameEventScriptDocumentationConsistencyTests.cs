// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
using StepH.GameEventScript.Api;
using StepH.GameEventScript.Conformance;
using StepH.GameEventScript.CSharpBridge;

namespace StepH_GameEventScript_Tests.Native.ApiSurface;

/// <summary>
/// Guards the normative documentation against drift from the reference implementation.
/// </summary>
[TestClass]
public sealed class GameEventScriptDocumentationConsistencyTests
{
    /// <summary>
    /// Verifies that the public API ownership map remains exhaustive for the approved reference surface domains.
    /// </summary>
    [TestMethod]
    public void PublicApiOwnershipMapCoversEveryReferenceSurfaceDomain()
    {
        var publicApi = File.ReadAllText(Path.Combine(TestRepositoryPaths.SpecificationsDirectory, "PublicApi.md"));
        var mapStart = publicApi.IndexOf("## Completeness map", StringComparison.Ordinal);
        Assert.IsGreaterThanOrEqualTo(0, mapStart, "PublicApi.md must contain the completeness map.");
        var map = publicApi[mapStart..];
        var actualFamilies = Regex.Matches(map, @"^\|\s*(?<family>[^|]+?)\s*\|\s*(?<owner>[^|]+?)\s*\|$", RegexOptions.Multiline)
            .Select(match => match.Groups["family"].Value.Trim())
            .Where(family => family != "Public family" && !family.StartsWith("---", StringComparison.Ordinal))
            .ToArray();
        var expectedFamilies = new[]
        {
            "Builder, CompileOptions, CompileException, manager facade",
            "Program, all segment/entry/view types, instruction and ID types",
            "BinaryFormat constants, Reader, read options/limits/retention, Writer, Validator, format error, Dumper",
            "Message, MessageArgument(s), MessageSignature",
            "Value, ValueSlice, ValueArguments, ValueMap, integer/float range",
            "HostBuilder, RuntimeLimits, Host, Instance, Subscription",
            "Context, NativeMessageHandler, PublishSink/Result, Observer, ExecutionResult/State",
            "RandomGenerator",
            "ExtensionReference, ExtensionRegistry/Function, ExtensionCall",
            "External definitions/catalog, constructor reference/registry/call, ExternalValue",
            "Diagnostic, codes/phase/symbol/location and failure transports",
            "Conformance parser, limits/diagnostics, Document/Case and normalized nested models",
            "Environment/options/limits, resolver/provider/sink, Runner, results/report/summary",
            "Result, Markdown, Received, and CrossLanguage writers plus CorpusIdentity",
            "CSharpBridge reflection, delegate, dictionary, and automatic-runner adapters"
        };
        CollectionAssert.AreEqual(expectedFamilies, actualFamilies, "PublicApi.md ownership families changed without updating the consistency gate.");

        var actualNamespaces = new[] { typeof(GameEventScriptProgram).Assembly, typeof(GameEventScriptCSharpHostRunner).Assembly, typeof(ConformanceRunner).Assembly }
            .SelectMany(assembly => assembly.GetExportedTypes())
            .Select(type => type.Namespace ?? string.Empty)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        var expectedNamespaces = new[]
        {
            "StepH.GameEventScript",
            "StepH.GameEventScript.Api",
            "StepH.GameEventScript.CSharpBridge",
            "StepH.GameEventScript.Conformance",
            "StepH.GameEventScript.Runtime.Values"
        };
        CollectionAssert.AreEqual(expectedNamespaces, actualNamespaces, "A new public namespace requires an explicit PublicApi.md ownership decision.");
    }

    /// <summary>
    /// Verifies that the documentation index owns every normative specification and that every local documentation link resolves.
    /// </summary>
    [TestMethod]
    public void NormativeDocumentationIsIndexedAndAllLocalLinksResolve()
    {
        var projectDirectory = TestRepositoryPaths.LibraryProjectDirectory;
        var documentationDirectory = TestRepositoryPaths.DocumentationDirectory;
        var specificationDirectory = TestRepositoryPaths.SpecificationsDirectory;
        var indexPath = Path.Combine(documentationDirectory, "README.md");
        var index = File.ReadAllText(indexPath);

        var normativeFiles = Directory.EnumerateFiles(specificationDirectory, "*.md", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(documentationDirectory, path).Replace(Path.DirectorySeparatorChar, '/'))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
        var indexedNormativeFiles = ExtractLocalLinkTargets(index)
            .Where(target => target.StartsWith("../specs/", StringComparison.Ordinal))
            .Select(RemoveFragment)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();

        CollectionAssert.AreEqual(normativeFiles, indexedNormativeFiles, "docs/README.md must link every normative specification exactly once.");

        var brokenLinks = new List<string>();
        foreach (var markdownPath in Directory.EnumerateFiles(documentationDirectory, "*.md", SearchOption.AllDirectories)
                     .Concat(Directory.EnumerateFiles(specificationDirectory, "*.md", SearchOption.AllDirectories)))
        {
            var markdown = File.ReadAllText(markdownPath);
            foreach (var target in ExtractLocalLinkTargets(markdown))
            {
                var pathPart = Uri.UnescapeDataString(RemoveFragment(target));
                if (pathPart.Length == 0) continue;
                var resolvedPath = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(markdownPath)!, pathPart));
                if (!File.Exists(resolvedPath)) brokenLinks.Add($"{Path.GetRelativePath(projectDirectory, markdownPath)} -> {target}");
            }
        }

        Assert.HasCount(0, brokenLinks, "Broken local documentation links:\n" + string.Join("\n", brokenLinks));

    }

    /// <summary>
    /// Verifies that the normative bytecode and binary numeric registries match the public reference enums exactly.
    /// </summary>
    [TestMethod]
    public void BytecodeAndBinaryNumericRegistriesMatchPublicEnums()
    {
        var specificationDirectory = TestRepositoryPaths.SpecificationsDirectory;
        var bytecode = File.ReadAllText(Path.Combine(specificationDirectory, "Bytecode.md"));
        var documentedOpcodes = Regex.Matches(bytecode, @"^\|\s*0x(?<id>[0-9A-Fa-f]{2})\s*\|\s*`(?<name>[A-Za-z0-9]+)`\s*\|", RegexOptions.Multiline)
            .Select(match => new KeyValuePair<string, ulong>(match.Groups["name"].Value, ulong.Parse(match.Groups["id"].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture)))
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        AssertEnumMatches<GameEventScriptBytecodeOpCode>(documentedOpcodes, "Bytecode.md opcode table");

        var binaryFormat = File.ReadAllText(Path.Combine(specificationDirectory, "BinaryFormat.md"));
        var documentedSections = Regex.Matches(binaryFormat, @"^\|\s*`0x(?<id>[0-9A-Fa-f]{4})`\s*\|\s*(?<name>[A-Za-z0-9]+(?:Segment|Section))\s*\|", RegexOptions.Multiline)
            .Select(match => new KeyValuePair<string, ulong>(match.Groups["name"].Value, ulong.Parse(match.Groups["id"].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture)))
            .ToDictionary(pair => NormalizeSectionName(pair.Key), pair => pair.Value, StringComparer.Ordinal);
        AssertEnumMatches<GameEventScriptSectionType>(documentedSections, "BinaryFormat.md section registry");

        var documentedFormatErrors = new Dictionary<string, ulong>(StringComparer.Ordinal);
        foreach (Match match in Regex.Matches(binaryFormat, @"^\|\s*(?<firstId>[0-9]+)\s*\|\s*(?<firstName>[A-Za-z0-9]+)\s*\|\s*(?<secondId>[0-9]+)\s*\|\s*(?<secondName>[A-Za-z0-9]+)\s*\|", RegexOptions.Multiline))
        {
            documentedFormatErrors.Add(match.Groups["firstName"].Value, ulong.Parse(match.Groups["firstId"].Value, CultureInfo.InvariantCulture));
            documentedFormatErrors.Add(match.Groups["secondName"].Value, ulong.Parse(match.Groups["secondId"].Value, CultureInfo.InvariantCulture));
        }

        AssertEnumMatches<GameEventScriptProgramFormatErrorCode>(documentedFormatErrors, "BinaryFormat.md format-error table");
    }

    /// <summary>
    /// Verifies that all public stable diagnostic-code constants are enumerated in their owning normative specifications.
    /// </summary>
    [TestMethod]
    public void StableDiagnosticCodesAreNormativelyEnumerated()
    {
        var specificationDirectory = TestRepositoryPaths.SpecificationsDirectory;
        var portableDiagnostics = File.ReadAllText(Path.Combine(specificationDirectory, "Diagnostics.md"));
        AssertConstantValuesAppear(typeof(GameEventScriptDiagnosticCodes), portableDiagnostics, "Diagnostics.md");

        var conformanceSpecification = string.Join("\n", Directory.EnumerateFiles(Path.Combine(specificationDirectory, "Conformance"), "*.md", SearchOption.TopDirectoryOnly)
            .OrderBy(path => path, StringComparer.Ordinal)
            .Select(File.ReadAllText));
        AssertConstantValuesAppear(typeof(ConformanceDiagnosticCodes), conformanceSpecification, "Conformance specifications");
        AssertConstantValuesAppear(typeof(ConformanceRunnerCodes), conformanceSpecification, "Conformance specifications");
        AssertConstantValuesAppear(typeof(ConformanceReceivedWriterCodes), conformanceSpecification, "Conformance specifications");
    }

    /// <summary>
    /// Verifies that every word token recognized by the lexer is represented by the normative grammar.
    /// </summary>
    [TestMethod]
    public void LexerWordVocabularyAppearsInNormativeGrammar()
    {
        var projectDirectory = TestRepositoryPaths.LibraryProjectDirectory;
        var lexer = File.ReadAllText(Path.Combine(projectDirectory, "Compiler", "GesLexer.cs"));
        var language = File.ReadAllText(Path.Combine(TestRepositoryPaths.SpecificationsDirectory, "Language.md"));
        var grammarStart = language.IndexOf("```bnf", StringComparison.Ordinal);
        var grammarEnd = grammarStart < 0 ? -1 : language.IndexOf("```", grammarStart + 6, StringComparison.Ordinal);
        Assert.IsGreaterThanOrEqualTo(0, grammarStart, "Language.md must contain the normative BNF grammar block.");
        Assert.IsGreaterThan(grammarStart, grammarEnd, "Language.md must terminate the normative BNF grammar block.");
        var grammar = language.Substring(grammarStart, grammarEnd - grammarStart);
        var lexerWords = Regex.Matches(lexer, @"IsWordAt\(start,\s*length,\s*""(?<word>[^""]+)""\)")
            .Select(match => match.Groups["word"].Value)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(word => word, StringComparer.Ordinal)
            .ToArray();
        var missingWords = lexerWords.Where(word => !grammar.Contains("'" + word + "'", StringComparison.Ordinal)).ToArray();

        Assert.HasCount(0, missingWords, "Lexer words missing from the normative grammar:\n" + string.Join("\n", missingWords));
    }

    private static IEnumerable<string> ExtractLocalLinkTargets(string markdown)
        => Regex.Matches(markdown, @"!?\[[^\]]*\]\((?<target>[^)\s]+)(?:\s+""[^""]*"")?\)")
            .Select(match => match.Groups["target"].Value)
            .Where(target => !target.StartsWith('#') && !target.Contains("://", StringComparison.Ordinal));

    private static string RemoveFragment(string target)
    {
        var fragment = target.IndexOf('#');
        return fragment < 0 ? target : target[..fragment];
    }

    private static string NormalizeSectionName(string name)
        => name switch
        {
            "ProgramMetadataSegment" => nameof(GameEventScriptSectionType.ProgramMetadata),
            "StringConstantSegment" => nameof(GameEventScriptSectionType.StringConstants),
            "UInt16IndexListSegment" => nameof(GameEventScriptSectionType.UInt16IndexLists),
            "BindingSegment" => nameof(GameEventScriptSectionType.Bindings),
            "CodeSegment" => nameof(GameEventScriptSectionType.Code),
            "DebugSymbolsSegment" => nameof(GameEventScriptSectionType.DebugSymbols),
            "SourceMapSegment" => nameof(GameEventScriptSectionType.SourceMap),
            "SourceArchiveSegment" => nameof(GameEventScriptSectionType.SourceArchive),
            "BuildMetadataSegment" => nameof(GameEventScriptSectionType.BuildMetadata),
            "ReservedSignatureSegment" => nameof(GameEventScriptSectionType.ReservedSignature),
            "NamedCustomSection" => nameof(GameEventScriptSectionType.NamedCustom),
            _ => name
        };

    private static void AssertEnumMatches<TEnum>(IReadOnlyDictionary<string, ulong> documented, string source) where TEnum : struct, Enum
    {
        var expected = Enum.GetValues<TEnum>().ToDictionary(value => value.ToString(), value => Convert.ToUInt64(value, CultureInfo.InvariantCulture), StringComparer.Ordinal);
        CollectionAssert.AreEquivalent(expected.Keys.ToArray(), documented.Keys.ToArray(), $"{source} names do not match {typeof(TEnum).Name}.");
        foreach (var pair in expected) Assert.AreEqual(pair.Value, documented[pair.Key], $"{source} assigns the wrong numeric value to {pair.Key}.");
    }

    private static void AssertConstantValuesAppear(Type constantsType, string specification, string source)
    {
        var failures = constantsType.GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.IsLiteral && field.FieldType == typeof(string))
            .Select(field => (Name: field.Name, Value: (string)field.GetRawConstantValue()!))
            .Select(entry => (entry.Name, entry.Value, Count: Regex.Matches(specification, Regex.Escape("`" + entry.Value + "`")).Count))
            .Where(entry => entry.Count == 0)
            .Select(entry => $"{entry.Name} = {entry.Value}")
            .ToArray();
        Assert.HasCount(0, failures, $"Stable codes missing from {source}:\n" + string.Join("\n", failures));
    }

}
