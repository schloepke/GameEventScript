// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System.Text;
using StepH.GameEventScript.Api;
using StepH.GameEventScript.CSharpBridge;

namespace StepH_GameEventScript_Tests.Native.CSharpBridge;

[TestClass]
public sealed class GameEventScriptCSharpBuilderAdapterTests
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void AddFileSnapshotsUtf8SourceAndPreservesSourceIdentity(bool withBom)
    {
        const string source = "module fileadapter\r\non Start { emit Done(value: 'Grüße 😀') }\r";
        var path = CreateSourcePath();
        try
        {
            File.WriteAllText(path, source, new UTF8Encoding(withBom, true));
            var builder = GameEventScriptBuilder.Create();
            Assert.AreSame(builder, builder.AddFile(path));
            File.Delete(path);

            var program = builder.Compile();
            Assert.AreEqual(source, program.SourceArchive!.Sources[0].ResolveText());
            Assert.AreEqual(path, program.SourceArchive.Sources[0].SourceName);
            Assert.AreEqual(path, program.SourceMap!.Sources[0].SourceName);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(path)!, true);
        }
    }

    [TestMethod]
    public void AddFileRejectsInvalidUtf8BeforeAddingSource()
    {
        var path = CreateSourcePath();
        try
        {
            File.WriteAllBytes(path, [0xC3, 0x28]);
            var builder = GameEventScriptBuilder.Create().AddScript("module fileadapter", "existing.ges");
            Assert.ThrowsExactly<DecoderFallbackException>(() => builder.AddFile(path));

            var program = builder.Compile();
            Assert.AreEqual(1, program.SourceArchive!.Sources.Length);
            Assert.AreEqual("existing.ges", program.SourceArchive.Sources[0].SourceName);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(path)!, true);
        }
    }

    private static string CreateSourcePath()
    {
        var directory = Path.Combine(TestRepositoryPaths.Root, "artifacts", "csharp", "tests", "file-adapter", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, "source.ges");
    }
}
