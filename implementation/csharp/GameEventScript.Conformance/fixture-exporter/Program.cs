// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;
using System.Text;
using GameEventScript.Api;
using GameEventScript.Conformance;
using GameEventScript.Tests.Conformance;

if (args.Length == 3 && args[0] == "--message-json")
{
    var input = System.Text.Json.JsonSerializer.Deserialize<string[]>(File.ReadAllText(args[1]))!;
    var normalized = input.Select(json => GameEventScriptMessageJson.Serialize(GameEventScriptMessageJson.Deserialize(json))).ToArray();
    File.WriteAllText(args[2], System.Text.Json.JsonSerializer.Serialize(normalized));
    return 0;
}

if (args.Length == 3 && args[0] == "--number-text") return NumberTextProbe.Run(args[1], args[2]);

if (args.Length != 2)
{
    Console.Error.WriteLine("Usage: runtime-fixture-exporter <corpus-directory> <artifacts-output-directory>");
    return 2;
}
var corpus = Path.GetFullPath(args[0]);
var output = Path.GetFullPath(args[1]);
if (!output.Split(Path.DirectorySeparatorChar).Contains("artifacts", StringComparer.Ordinal))
    throw new ArgumentException("Generated runtime fixtures must be written below artifacts.");
Directory.CreateDirectory(output);
var rows = new List<string> { "GES-RUNTIME-FIXTURES-V1" };
var count = 0;
foreach (var path in Directory.EnumerateFiles(corpus, "*.md", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
{
    var bytes = File.ReadAllBytes(path);
    var document = ConformanceMarkdownParser.Parse(bytes);
    foreach (var testCase in document.Cases)
    {
        if (testCase.Kind is not (ConformanceTestKind.ScriptApi or ConformanceTestKind.LoadError or ConformanceTestKind.Performance or ConformanceTestKind.BytecodeSnapshot or ConformanceTestKind.ProgramBinary)) continue;
        var groups = testCase.Sources.GroupBy(source => source.ProgramId, StringComparer.Ordinal).ToArray();
        if (groups.Length == 0)
            rows.Add(string.Join('\t', testCase.FullId, "", "", "", Convert.ToHexString(SHA256.HashData(bytes))));
        foreach (var group in groups)
        {
            var builder = GameEventScriptBuilder.Create().WithExternalTypeCatalog(GameEventScriptConformanceExternalTypes.Catalog);
            foreach (var source in group) builder.AddScript(source.Text, source.Name);
            var debug = GameEventScriptDebugInfoOptions.None;
            foreach (var option in testCase.Compile.DebugInfo)
                debug |= option switch
                {
                    "debugSymbols" => GameEventScriptDebugInfoOptions.DebugSymbols,
                    "sourceMap" => GameEventScriptDebugInfoOptions.SourceMap,
                    "sourceArchive" => GameEventScriptDebugInfoOptions.SourceArchive,
                    _ => GameEventScriptDebugInfoOptions.None
                };
            var program = builder.Compile(new GameEventScriptCompileOptions { DebugInfo = debug, ProgramVersion = testCase.BinaryFixture?.ProgramVersion ?? 0 });
            var binary = GameEventScriptProgramWriter.ToArray(program);
            var hash = Convert.ToHexString(SHA256.HashData(binary));
            var name = hash + ".gesb";
            File.WriteAllBytes(Path.Combine(output, name), binary);
            rows.Add(string.Join('\t', testCase.FullId, group.Key, name, hash, Convert.ToHexString(SHA256.HashData(bytes))));
            count++;
        }
    }
}
File.WriteAllText(Path.Combine(output, "manifest.tsv"), string.Join('\n', rows) + "\n", new UTF8Encoding(false));
Console.WriteLine($"Exported {count} Programs from shared Markdown; execution expectations remain in the corpus.");
return 0;
