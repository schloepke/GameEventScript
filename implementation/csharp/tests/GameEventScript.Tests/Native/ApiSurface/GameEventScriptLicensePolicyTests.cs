// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;

namespace GameEventScript.Tests.Native.ApiSurface;

[TestClass]
public sealed class GameEventScriptLicensePolicyTests
{
    private const string CopyrightText = "Copyright 2026 Stephan Schlöpke";
    private const string SpdxText = "SPDX-License-Identifier: Apache-2.0";
    private const string OfficialApacheLicenseSha256 = "CFC7749B96F63BD31C3C42B5C471BF756814053E847C10F3EB003417BC523D30";

    [TestMethod]
    public void LicenseIsTheUnmodifiedOfficialApache20Text()
    {
        var workspace = TestRepositoryPaths.Root;
        var licensePath = Path.Combine(workspace, "LICENSE");
        Assert.IsTrue(File.Exists(licensePath), "The source distribution requires a top-level LICENSE file.");

        var sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(licensePath)));
        Assert.AreEqual(OfficialApacheLicenseSha256, sha256, "LICENSE differs from the official Apache-2.0 text.");
        Assert.IsFalse(File.Exists(Path.Combine(workspace, "NOTICE")), "LICENSING.md records that no NOTICE attribution is currently required.");
    }

    [TestMethod]
    public void PackageMetadataIdentifiesLicenseAndCopyrightOwner()
    {
        var projects = new[]
        {
            (TestRepositoryPaths.LibraryProjectDirectory, "GameEventScript.Runtime", "Portable, deterministic Game Event Script runtime, serial message host, and Program binary codec. No source compiler dependency."),
            (TestRepositoryPaths.CompilerProjectDirectory, "GameEventScript.Compiler", "Portable Game Event Script source compiler. Depends on the separately consumable runtime."),
            (TestRepositoryPaths.CSharpBridgeProjectDirectory, "GameEventScript.CSharpBridge", "C# adapters for Game Event Script reflection, delegates, dictionaries, and automatic host execution."),
            (TestRepositoryPaths.ConformanceProjectDirectory, "GameEventScript.Conformance", "Portable Markdown parser, runner, and report writers for the Game Event Script conformance corpus.")
        };

        foreach (var (projectDirectory, packageId, description) in projects)
        {
            var project = XDocument.Load(Directory.EnumerateFiles(projectDirectory, "*.csproj", SearchOption.TopDirectoryOnly).Single());
            Assert.AreEqual(packageId, SingleValue(project, "PackageId"));
            Assert.AreEqual("0.1.0", SingleValue(project, "Version"));
            Assert.AreEqual("Stephan Schlöpke", SingleValue(project, "Authors"));
            Assert.AreEqual(CopyrightText, SingleValue(project, "Copyright"));
            Assert.AreEqual(description, SingleValue(project, "Description"));
            Assert.AreEqual("Apache-2.0", SingleValue(project, "PackageLicenseExpression"));
            Assert.AreEqual("README.md", SingleValue(project, "PackageReadmeFile"));
            Assert.AreEqual("https://github.com/schloepke/GameEventScript", SingleValue(project, "RepositoryUrl"));

            AssertPackageFile(project, "LICENSE");
            AssertPackageFile(project, "README.md");
            Assert.IsFalse(project.Descendants("PackageReference").Any(), $"{packageId} must not acquire an external package dependency.");
        }
    }

    [TestMethod]
    public void HandwrittenFilesCarryTheCanonicalHeaderWhereTheirFormatAllowsIt()
    {
        var workspace = TestRepositoryPaths.Root;
        var csharp = Path.Combine(workspace, "implementation", "csharp");
        var failures = new List<string>();

        foreach (var path in EnumerateSourceFiles(csharp, "*.cs"))
            RequirePrefix(path, $"// {CopyrightText}\n// {SpdxText}\n", failures);

        foreach (var path in EnumerateSourceFiles(csharp, "*.md")
                     .Concat(EnumerateSourceFiles(TestRepositoryPaths.SpecificationsDirectory, "*.md"))
                     .Concat(EnumerateSourceFiles(TestRepositoryPaths.DocumentationDirectory, "*.md"))
                     .Concat(EnumerateSourceFiles(TestRepositoryPaths.ConformanceDirectory, "*.md"))
                     .Concat(EnumerateSourceFiles(Path.Combine(workspace, "tools"), "*.md"))
                     .Where(IsHeaderEligibleTestMarkdown))
            RequirePrefix(path, $"<!-- {CopyrightText} -->\n<!-- {SpdxText} -->\n", failures);

        foreach (var path in new[] { "AGENTS.md", "BACKLOG.md", "LICENSING.md", "README.md" })
            RequirePrefix(Path.Combine(workspace, path), $"<!-- {CopyrightText} -->\n<!-- {SpdxText} -->\n", failures);

        RequirePrefix(Path.Combine(workspace, ".editorconfig"), $"# {CopyrightText}\n# {SpdxText}\n", failures);
        RequirePrefix(Path.Combine(workspace, ".gitignore"), $"# {CopyrightText}\n# {SpdxText}\n", failures);
        RequireNearStart(Path.Combine(workspace, "Directory.Build.props"), failures);
        RequireNearStart(Path.Combine(workspace, "GameEventScript.sln"), failures);

        foreach (var path in EnumerateSourceFiles(Path.Combine(workspace, "scripts"), "*.sh"))
            RequireNearStart(path, failures);

        foreach (var path in EnumerateSourceFiles(csharp, "*.csproj")) RequireNearStart(path, failures);
        foreach (var path in EnumerateSourceFiles(Path.Combine(workspace, ".github", "workflows"), "*.yml")) RequirePrefix(path, $"# {CopyrightText}\n# {SpdxText}\n", failures);
        var editors = Path.Combine(workspace, "tools", "editors");
        foreach (var pattern in new[] { "*.plist", "*.tmLanguage", "*.tmPreferences" })
            foreach (var path in EnumerateSourceFiles(editors, pattern))
                RequireNearStart(path, failures);

        Assert.HasCount(0, failures, "License header policy violations:\n" + string.Join("\n", failures));
    }

    private static IEnumerable<string> EnumerateSourceFiles(string root, string pattern)
        => Directory.EnumerateFiles(root, pattern, SearchOption.AllDirectories)
            .Where(path => !HasDirectorySegment(path, "bin") && !HasDirectorySegment(path, "obj") && !HasDirectorySegment(path, "TestResults"));

    private static bool IsHeaderEligibleTestMarkdown(string path)
        => !HasDirectorySegment(path, "Suites") &&
           !HasDirectorySegment(path, "Received") &&
           !HasDirectorySegment(path, "Valid") &&
           !HasDirectorySegment(path, "Invalid");

    private static void RequirePrefix(string path, string expected, ICollection<string> failures)
    {
        var text = File.ReadAllText(path, Encoding.UTF8).TrimStart('\uFEFF');
        if (!text.StartsWith(expected, StringComparison.Ordinal)) failures.Add(path + ": missing canonical leading header");
    }

    private static void RequireNearStart(string path, ICollection<string> failures)
    {
        var text = File.ReadAllText(path, Encoding.UTF8).TrimStart('\uFEFF');
        var prefix = text[..Math.Min(text.Length, 512)];
        var hasXmlHeader = prefix.Contains($"<!-- {CopyrightText} -->\n<!-- {SpdxText} -->", StringComparison.Ordinal);
        var hasLineHeader = prefix.Contains($"# {CopyrightText}\n# {SpdxText}", StringComparison.Ordinal);
        if (!hasXmlHeader && !hasLineHeader) failures.Add(path + ": missing canonical header near the start");
    }

    private static string SingleValue(XDocument document, string name)
        => document.Descendants(name).SingleOrDefault()?.Value ?? string.Empty;

    private static void AssertPackageFile(XDocument project, string fileName)
    {
        var item = project.Descendants("None").SingleOrDefault(element =>
            string.Equals((string?)element.Attribute("Link"), fileName, StringComparison.Ordinal) ||
            string.Equals((string?)element.Attribute("Update"), fileName, StringComparison.Ordinal));
        Assert.IsNotNull(item, $"Package file '{fileName}' is not declared.");
        Assert.AreEqual("true", (string?)item.Attribute("Pack"));
        Assert.AreEqual("/", (string?)item.Attribute("PackagePath"));
    }

    private static bool HasDirectorySegment(string path, string segment)
        => path.Contains(Path.DirectorySeparatorChar + segment + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

}
