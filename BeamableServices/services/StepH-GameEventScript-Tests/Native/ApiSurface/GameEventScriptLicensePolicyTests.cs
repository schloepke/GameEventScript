// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;

namespace StepH_GameEventScript_Tests.Native.ApiSurface;

[TestClass]
public sealed class GameEventScriptLicensePolicyTests
{
    private const string CopyrightText = "Copyright 2026 Stephan Schlöpke";
    private const string SpdxText = "SPDX-License-Identifier: Apache-2.0";
    private const string OfficialApacheLicenseSha256 = "CFC7749B96F63BD31C3C42B5C471BF756814053E847C10F3EB003417BC523D30";

    [TestMethod]
    public void LicenseIsTheUnmodifiedOfficialApache20Text()
    {
        var workspace = FindWorkspaceDirectory();
        var project = Path.Combine(workspace, "StepH-GameEventScript");
        var licensePath = Path.Combine(project, "LICENSE");
        Assert.IsTrue(File.Exists(licensePath), "The source distribution requires a top-level LICENSE file.");

        var sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(licensePath)));
        Assert.AreEqual(OfficialApacheLicenseSha256, sha256, "LICENSE differs from the official Apache-2.0 text.");
        Assert.IsFalse(File.Exists(Path.Combine(project, "NOTICE")), "LICENSING.md records that no NOTICE attribution is currently required.");
    }

    [TestMethod]
    public void PackageMetadataIdentifiesLicenseAndCopyrightOwner()
    {
        var projectDirectory = Path.Combine(FindWorkspaceDirectory(), "StepH-GameEventScript");
        var project = XDocument.Load(Path.Combine(projectDirectory, "StepH-GameEventScript.csproj"));

        Assert.AreEqual("Stephan Schlöpke", SingleValue(project, "Authors"));
        Assert.AreEqual(CopyrightText, SingleValue(project, "Copyright"));
        Assert.AreEqual("Portable, deterministic Game Event Script compiler, binary format, serial message host, runtime, and conformance APIs.", SingleValue(project, "Description"));
        Assert.AreEqual("Apache-2.0", SingleValue(project, "PackageLicenseExpression"));
        Assert.AreEqual("README.md", SingleValue(project, "PackageReadmeFile"));

        AssertPackageFile(project, "LICENSE");
        AssertPackageFile(project, "README.md");
    }

    [TestMethod]
    public void HandwrittenFilesCarryTheCanonicalHeaderWhereTheirFormatAllowsIt()
    {
        var workspace = FindWorkspaceDirectory();
        var library = Path.Combine(workspace, "StepH-GameEventScript");
        var tests = Path.Combine(workspace, "StepH-GameEventScript-Tests");
        var failures = new List<string>();

        foreach (var path in EnumerateSourceFiles(library, "*.cs").Concat(EnumerateSourceFiles(tests, "*.cs")))
            RequirePrefix(path, $"// {CopyrightText}\n// {SpdxText}\n", failures);

        foreach (var path in EnumerateSourceFiles(library, "*.md"))
            RequirePrefix(path, $"<!-- {CopyrightText} -->\n<!-- {SpdxText} -->\n", failures);

        foreach (var path in EnumerateSourceFiles(tests, "*.md").Where(IsHeaderEligibleTestMarkdown))
            RequirePrefix(path, $"<!-- {CopyrightText} -->\n<!-- {SpdxText} -->\n", failures);

        RequirePrefix(Path.Combine(workspace, ".editorconfig"), $"# {CopyrightText}\n# {SpdxText}\n", failures);
        RequirePrefix(Path.Combine(library, ".gitignore"), $"# {CopyrightText}\n# {SpdxText}\n", failures);

        RequireNearStart(Path.Combine(library, "StepH-GameEventScript.csproj"), failures);
        RequireNearStart(Path.Combine(tests, "StepH-GameEventScript-Tests.csproj"), failures);
        var editors = Path.Combine(library, "Editors");
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
        if (!prefix.Contains($"<!-- {CopyrightText} -->\n<!-- {SpdxText} -->", StringComparison.Ordinal)) failures.Add(path + ": missing canonical XML header");
    }

    private static string SingleValue(XDocument document, string name)
        => document.Descendants(name).SingleOrDefault()?.Value ?? string.Empty;

    private static void AssertPackageFile(XDocument project, string fileName)
    {
        var item = project.Descendants("None").SingleOrDefault(element => string.Equals((string?)element.Attribute("Update"), fileName, StringComparison.Ordinal));
        Assert.IsNotNull(item, $"Package file '{fileName}' is not declared.");
        Assert.AreEqual("true", (string?)item.Attribute("Pack"));
        Assert.AreEqual("/", (string?)item.Attribute("PackagePath"));
    }

    private static bool HasDirectorySegment(string path, string segment)
        => path.Contains(Path.DirectorySeparatorChar + segment + Path.DirectorySeparatorChar, StringComparison.Ordinal);

    private static string FindWorkspaceDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "StepH-GameEventScript", "StepH-GameEventScript.csproj")) &&
                File.Exists(Path.Combine(directory.FullName, "StepH-GameEventScript-Tests", "StepH-GameEventScript-Tests.csproj")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the Game Event Script workspace.");
    }
}
