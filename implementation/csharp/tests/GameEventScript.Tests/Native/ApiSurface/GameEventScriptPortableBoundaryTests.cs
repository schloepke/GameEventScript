// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System.Collections;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using GameEventScript.Api;
using GameEventScript.Conformance;

namespace GameEventScript.Tests.Native.ApiSurface;

/// <summary>Verifies platform-independent dependency and public-input boundaries for each portable library.</summary>
[TestClass]
public sealed class GameEventScriptPortableBoundaryTests
{
    /// <summary>Verifies that file-system operations remain outside portable libraries.</summary>
    /// <param name="assemblyType">A public type identifying the library to inspect.</param>
    [TestMethod]
    [DataRow(typeof(GameEventScriptProgram))]
    [DataRow(typeof(GameEventScriptBuilder))]
    [DataRow(typeof(ConformanceRunner))]
    public void PortableAssemblyHasNoFileSystemDependencies(Type assemblyType)
    {
        using var stream = File.OpenRead(assemblyType.Assembly.Location);
        using var assembly = new PEReader(stream);
        var metadata = assembly.GetMetadataReader();
        var forbidden = new HashSet<string>(StringComparer.Ordinal)
        {
            "System.IO.File", "System.IO.Directory", "System.IO.FileInfo", "System.IO.DirectoryInfo",
            "System.IO.FileStream", "System.IO.FileSystemInfo", "System.IO.FileSystemWatcher", "System.IO.DriveInfo"
        };
        var dependencies = metadata.TypeReferences
            .Select(handle => metadata.GetTypeReference(handle))
            .Select(type => metadata.GetString(type.Namespace) + "." + metadata.GetString(type.Name))
            .Where(forbidden.Contains)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.HasCount(0, dependencies, "File-system adapters belong outside portable libraries: " + string.Join(", ", dependencies));
    }

    /// <summary>Verifies that portable libraries acquire no ambient clock, random, or identifier sources.</summary>
    /// <param name="assemblyType">A public type identifying the library to inspect.</param>
    [TestMethod]
    [DataRow(typeof(GameEventScriptProgram))]
    [DataRow(typeof(GameEventScriptBuilder))]
    [DataRow(typeof(ConformanceRunner))]
    public void PortableAssemblyHasNoAmbientClockOrIdentifierDependencies(Type assemblyType)
    {
        using var stream = File.OpenRead(assemblyType.Assembly.Location);
        using var assembly = new PEReader(stream);
        var metadata = assembly.GetMetadataReader();
        var forbidden = new HashSet<string>(StringComparer.Ordinal)
        {
            "System.DateTime", "System.DateTimeOffset", "System.Guid", "System.Random"
        };
        var dependencies = metadata.TypeReferences
            .Select(handle => metadata.GetTypeReference(handle))
            .Select(type => metadata.GetString(type.Namespace) + "." + metadata.GetString(type.Name))
            .Where(forbidden.Contains)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.HasCount(0, dependencies, "Ambient clock and identifier sources belong outside portable libraries; entropy is acquired only through the platform seam: " + string.Join(", ", dependencies));
    }

    /// <summary>Verifies that unordered dictionary input adapters remain in CSharpBridge.</summary>
    /// <param name="assemblyType">A public type identifying the library to inspect.</param>
    [TestMethod]
    [DataRow(typeof(GameEventScriptProgram))]
    [DataRow(typeof(GameEventScriptBuilder))]
    [DataRow(typeof(ConformanceRunner))]
    public void PortablePublicInputsDoNotExposeUnorderedDictionaryAdapters(Type assemblyType)
    {
        var violations = assemblyType.Assembly.GetExportedTypes()
            .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly).Cast<MethodBase>()
                .Concat(type.GetConstructors(BindingFlags.Public | BindingFlags.Instance)))
            .SelectMany(method => method.GetParameters().Where(parameter => ContainsDictionary(parameter.ParameterType))
                .Select(parameter => $"{method.DeclaringType!.FullName}.{method.Name}({parameter.Name}: {parameter.ParameterType})"))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.HasCount(0, violations, "Unordered dictionary input adapters belong in CSharpBridge:\n" + string.Join("\n", violations));
    }

    private static bool ContainsDictionary(Type type)
    {
        if (type.HasElementType) return ContainsDictionary(type.GetElementType()!);
        if (typeof(IDictionary).IsAssignableFrom(type)) return true;
        if (!type.IsGenericType) return false;
        var definition = type.GetGenericTypeDefinition();
        return definition == typeof(IDictionary<,>) || definition == typeof(IReadOnlyDictionary<,>) || type.GetGenericArguments().Any(ContainsDictionary);
    }
}
