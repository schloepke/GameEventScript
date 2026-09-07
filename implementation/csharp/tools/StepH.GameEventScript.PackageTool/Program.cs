// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System.IO.Compression;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace StepH.GameEventScript.PackageTool;

internal static class Program
{
    private const string CorePackage = "StepH.GameEventScript";
    private static readonly DateTimeOffset CanonicalTimestamp = new(2000, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly UTF8Encoding Utf8 = new(false, true);
    private static readonly PackageDefinition[] Packages =
    [
        new(CorePackage, null),
        new("StepH.GameEventScript.CSharpBridge", CorePackage),
        new("StepH.GameEventScript.Conformance", CorePackage)
    ];

    private static int Main(string[] arguments)
    {
        try
        {
            if (arguments.Length != 4 || arguments[0] != "prepare")
                throw new ArgumentException("Usage: prepare <package-directory> <version> <repository-root>");

            var packageDirectory = Path.GetFullPath(arguments[1]);
            var version = arguments[2];
            var repositoryRoot = Path.GetFullPath(arguments[3]);
            foreach (var package in Packages)
            {
                Normalize(Path.Combine(packageDirectory, $"{package.Id}.{version}.nupkg"));
                Normalize(Path.Combine(packageDirectory, $"{package.Id}.{version}.snupkg"));
            }

            foreach (var package in Packages) Verify(packageDirectory, version, repositoryRoot, package);
            Console.WriteLine($"Prepared and verified {Packages.Length} NuGet packages and {Packages.Length} symbol packages.");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception.Message);
            return 1;
        }
    }

    private static void Normalize(string packagePath)
    {
        if (!File.Exists(packagePath)) throw new FileNotFoundException("Expected package was not produced.", packagePath);
        var entries = ReadEntries(packagePath);
        if (entries.Any(entry => string.Equals(entry.Name, ".signature.p7s", StringComparison.OrdinalIgnoreCase)))
            throw new InvalidDataException($"Signed package '{packagePath}' cannot be normalized. Signing must happen after canonical packaging.");

        var coreProperties = entries.Where(entry => entry.Name.StartsWith("package/services/metadata/core-properties/", StringComparison.Ordinal) && entry.Name.EndsWith(".psmdcp", StringComparison.Ordinal)).ToArray();
        if (coreProperties.Length != 1) throw new InvalidDataException($"Package '{packagePath}' must contain exactly one NuGet core-properties part.");

        var oldCorePropertiesName = coreProperties[0].Name;
        const string canonicalCorePropertiesName = "package/services/metadata/core-properties/core.psmdcp";
        for (var index = 0; index < entries.Count; index++)
        {
            var entry = entries[index];
            if (entry.Name == oldCorePropertiesName) entries[index] = entry with { Name = canonicalCorePropertiesName };
            else if (entry.Name == "_rels/.rels") entries[index] = entry with { Content = NormalizeRelationships(entry.Content) };
        }

        if (entries.Select(entry => entry.Name).Distinct(StringComparer.Ordinal).Count() != entries.Count)
            throw new InvalidDataException($"Canonicalization would create duplicate entries in '{packagePath}'.");

        var temporaryPath = packagePath + ".canonical.tmp";
        if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        try
        {
            using (var output = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: false, entryNameEncoding: Utf8))
            {
                foreach (var source in entries.OrderBy(entry => entry.Name, StringComparer.Ordinal))
                {
                    var destination = archive.CreateEntry(source.Name, CompressionLevel.Optimal);
                    destination.LastWriteTime = CanonicalTimestamp;
                    using var stream = destination.Open();
                    stream.Write(source.Content);
                }
            }

            File.Move(temporaryPath, packagePath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }
    }

    private static byte[] NormalizeRelationships(byte[] content)
    {
        var document = ParseXml(content);
        var relationships = document.Root?.Elements().Where(element => element.Name.LocalName == "Relationship").OrderBy(element => (string?)element.Attribute("Target"), StringComparer.Ordinal).ToArray()
            ?? throw new InvalidDataException("NuGet relationship document has no root element.");
        var customIndex = 0;
        foreach (var relationship in relationships)
        {
            var type = (string?)relationship.Attribute("Type") ?? string.Empty;
            var target = (string?)relationship.Attribute("Target") ?? string.Empty;
            if (type.EndsWith("/metadata/core-properties", StringComparison.Ordinal))
            {
                relationship.SetAttributeValue("Id", "RCoreProperties");
                relationship.SetAttributeValue("Target", "/package/services/metadata/core-properties/core.psmdcp");
            }
            else if (target.EndsWith(".nuspec", StringComparison.OrdinalIgnoreCase))
            {
                relationship.SetAttributeValue("Id", "RManifest");
            }
            else
            {
                relationship.SetAttributeValue("Id", $"R{customIndex++:D4}");
            }
        }

        return SerializeXml(document);
    }

    private static void Verify(string packageDirectory, string version, string repositoryRoot, PackageDefinition package)
    {
        var normalPath = Path.Combine(packageDirectory, $"{package.Id}.{version}.nupkg");
        var symbolPath = Path.Combine(packageDirectory, $"{package.Id}.{version}.snupkg");
        var normalEntries = VerifyCanonicalArchive(normalPath);
        var symbolEntries = VerifyCanonicalArchive(symbolPath);
        var nuspecEntry = RequiredEntry(normalEntries, package.Id + ".nuspec");
        VerifyManifest(ParseXml(nuspecEntry.Content), package, version, requireDistributionMetadata: true);
        VerifyManifest(ParseXml(RequiredEntry(symbolEntries, package.Id + ".nuspec").Content), package, version, requireDistributionMetadata: false);

        AssertBytesEqual(File.ReadAllBytes(Path.Combine(repositoryRoot, "LICENSE")), RequiredEntry(normalEntries, "LICENSE").Content, $"{package.Id} LICENSE");
        AssertBytesEqual(File.ReadAllBytes(Path.Combine(repositoryRoot, "README.md")), RequiredEntry(normalEntries, "README.md").Content, $"{package.Id} README");

        var libraryPrefix = "lib/netstandard2.1/" + package.Id;
        var assembly = RequiredEntry(normalEntries, libraryPrefix + ".dll").Content;
        _ = RequiredEntry(normalEntries, libraryPrefix + ".xml");
        var packagedAssemblies = normalEntries.Where(entry => entry.Name.StartsWith("lib/netstandard2.1/", StringComparison.Ordinal) && entry.Name.EndsWith(".dll", StringComparison.Ordinal)).ToArray();
        if (packagedAssemblies.Length != 1 || packagedAssemblies[0].Name != libraryPrefix + ".dll")
            throw new InvalidDataException($"Package '{package.Id}' must contain only its own runtime assembly.");
        VerifyAssemblyReferences(package.Id, assembly);

        var pdb = RequiredEntry(symbolEntries, libraryPrefix + ".pdb").Content;
        if (pdb.Length < 4 || pdb[0] != (byte)'B' || pdb[1] != (byte)'S' || pdb[2] != (byte)'J' || pdb[3] != (byte)'B')
            throw new InvalidDataException($"Symbol package for '{package.Id}' does not contain a portable PDB.");
    }

    private static List<PackageEntry> VerifyCanonicalArchive(string packagePath)
    {
        var entries = ReadEntries(packagePath);
        var names = entries.Select(entry => entry.Name).ToArray();
        if (!names.SequenceEqual(names.OrderBy(name => name, StringComparer.Ordinal), StringComparer.Ordinal))
            throw new InvalidDataException($"Package '{packagePath}' does not use canonical entry order.");
        if (entries.Any(entry => entry.Timestamp.DateTime != CanonicalTimestamp.DateTime))
            throw new InvalidDataException($"Package '{packagePath}' does not use the canonical ZIP timestamp.");
        if (!names.Contains("package/services/metadata/core-properties/core.psmdcp", StringComparer.Ordinal))
            throw new InvalidDataException($"Package '{packagePath}' does not use the canonical core-properties path.");
        if (names.Distinct(StringComparer.Ordinal).Count() != names.Length)
            throw new InvalidDataException($"Package '{packagePath}' contains duplicate ZIP entries.");
        return entries;
    }

    private static void VerifyManifest(XDocument document, PackageDefinition package, string version, bool requireDistributionMetadata)
    {
        var metadata = document.Descendants().Single(element => element.Name.LocalName == "metadata");
        string Value(string name) => metadata.Elements().Single(element => element.Name.LocalName == name).Value;
        if (Value("id") != package.Id) throw new InvalidDataException($"Unexpected package ID in manifest for '{package.Id}'.");
        if (Value("version") != version) throw new InvalidDataException($"Unexpected version in manifest for '{package.Id}'.");
        if (requireDistributionMetadata && Value("license") != "Apache-2.0") throw new InvalidDataException($"Unexpected license in manifest for '{package.Id}'.");
        if (requireDistributionMetadata && Value("readme") != "README.md") throw new InvalidDataException($"Unexpected README in manifest for '{package.Id}'.");
        var repository = metadata.Elements().Single(element => element.Name.LocalName == "repository");
        if (repository.Attribute("url")?.Value != "https://github.com/schloepke/GameEventScript" || repository.Attribute("type")?.Value != "git")
            throw new InvalidDataException($"Unexpected repository metadata in manifest for '{package.Id}'.");

        var dependencies = metadata.Descendants().Where(element => element.Name.LocalName == "dependency").ToArray();
        if (package.Dependency is null && dependencies.Length != 0) throw new InvalidDataException($"Core package '{package.Id}' must not have package dependencies.");
        if (package.Dependency is not null && (dependencies.Length != 1 || dependencies[0].Attribute("id")?.Value != package.Dependency || !(dependencies[0].Attribute("version")?.Value ?? string.Empty).Contains(version, StringComparison.Ordinal)))
            throw new InvalidDataException($"Package '{package.Id}' must depend only on '{package.Dependency}' version '{version}'.");
    }

    private static void VerifyAssemblyReferences(string packageId, byte[] assembly)
    {
        using var stream = new MemoryStream(assembly, writable: false);
        using var peReader = new PEReader(stream);
        var metadata = peReader.GetMetadataReader();
        foreach (var handle in metadata.AssemblyReferences)
        {
            var reference = metadata.GetAssemblyReference(handle);
            var name = metadata.GetString(reference.Name);
            if (name.StartsWith("Beamable", StringComparison.Ordinal) || name.StartsWith("Unity", StringComparison.Ordinal))
                throw new InvalidDataException($"Package assembly '{packageId}' unexpectedly references '{name}'.");
        }
    }

    private static List<PackageEntry> ReadEntries(string packagePath)
    {
        var entries = new List<PackageEntry>();
        using var input = new FileStream(packagePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var archive = new ZipArchive(input, ZipArchiveMode.Read, leaveOpen: false, entryNameEncoding: Utf8);
        foreach (var entry in archive.Entries)
        {
            using var source = entry.Open();
            using var content = new MemoryStream(checked((int)entry.Length));
            source.CopyTo(content);
            entries.Add(new PackageEntry(entry.FullName, content.ToArray(), entry.LastWriteTime));
        }

        return entries;
    }

    private static PackageEntry RequiredEntry(IEnumerable<PackageEntry> entries, string name)
        => entries.SingleOrDefault(entry => entry.Name == name) ?? throw new InvalidDataException($"Required package entry '{name}' is missing.");

    private static XDocument ParseXml(byte[] content)
    {
        using var stream = new MemoryStream(content, writable: false);
        using var reader = XmlReader.Create(stream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit });
        return XDocument.Load(reader, LoadOptions.None);
    }

    private static byte[] SerializeXml(XDocument document)
    {
        using var output = new MemoryStream();
        using (var writer = XmlWriter.Create(output, new XmlWriterSettings { Encoding = Utf8, Indent = false, NewLineChars = "\n", OmitXmlDeclaration = document.Declaration is null })) document.Save(writer);
        return output.ToArray();
    }

    private static void AssertBytesEqual(byte[] expected, byte[] actual, string name)
    {
        if (!expected.AsSpan().SequenceEqual(actual)) throw new InvalidDataException($"Packaged {name} differs from the repository file.");
    }

    private sealed record PackageDefinition(string Id, string? Dependency);
    private sealed record PackageEntry(string Name, byte[] Content, DateTimeOffset Timestamp);
}
