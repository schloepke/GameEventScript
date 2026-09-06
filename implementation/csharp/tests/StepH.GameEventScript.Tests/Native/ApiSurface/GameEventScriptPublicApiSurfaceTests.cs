// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System.Reflection;
using System.Text;
using System.Xml.Linq;
using StepH.GameEventScript.Api;

namespace StepH_GameEventScript_Tests.Native.ApiSurface;

[TestClass]
public sealed class GameEventScriptPublicApiSurfaceTests
{
    [TestMethod]
    public void PublicApiSurfaceMatchesApprovedSnapshot()
    {
        var actual = BuildPublicSurfaceSnapshot();
        var approvedPath = FindApprovedSnapshotPath();
        var receivedPath = Path.Combine(
            Path.GetDirectoryName(approvedPath) ?? throw new DirectoryNotFoundException("Approved snapshot directory was not found."),
            "PublicApiSurface.received.txt");

        File.WriteAllText(receivedPath, actual);

        var expected = File.ReadAllText(approvedPath);

        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void CompilerTypesStayInternal()
    {
        var leakedTypes = typeof(GameEventScriptProgram)
            .Assembly
            .GetExportedTypes()
            .Where(type =>
                IsNamespace(type, "StepH.GameEventScript.Compiler"))
                .Select(type => type.FullName)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();

        Assert.HasCount(0, leakedTypes, "Public compiler types leaked:\n" + string.Join("\n", leakedTypes));
    }

    /// <summary>
    /// Verifies that the XML-documentation build gate remains active, every exported type reaches the artifact, and handwritten library sources contain no pragma directives.
    /// </summary>
    [TestMethod]
    public void PublicApiDocumentationAndPragmasRemainComplete()
    {
        var assembly = typeof(GameEventScriptProgram).Assembly;
        var xmlPath = Path.ChangeExtension(assembly.Location, ".xml");
        Assert.IsTrue(File.Exists(xmlPath), "The public XML documentation artifact was not generated.");

        var documentation = XDocument.Load(xmlPath);
        var documentedMembers = documentation
            .Descendants("member")
            .Select(member => (string?)member.Attribute("name"))
            .Where(name => name is not null)
            .ToHashSet(StringComparer.Ordinal);

        var undocumentedTypes = assembly
            .GetExportedTypes()
            .Select(type => "T:" + (type.FullName ?? type.Name).Replace('+', '.'))
            .Where(typeId => !documentedMembers.Contains(typeId))
            .OrderBy(typeId => typeId, StringComparer.Ordinal)
            .ToArray();
        Assert.HasCount(0, undocumentedTypes, "Exported types without XML documentation:\n" + string.Join("\n", undocumentedTypes));

        var projectDirectory = TestRepositoryPaths.LibraryProjectDirectory;
        var project = XDocument.Load(Path.Combine(projectDirectory, "StepH-GameEventScript.csproj"));
        var generatedDocumentation = project.Descendants("GenerateDocumentationFile").SingleOrDefault()?.Value;
        Assert.AreEqual("true", generatedDocumentation, "The library must continue to generate its public XML documentation artifact.");

        var warningsAsErrors = project.Descendants("WarningsAsErrors").SingleOrDefault()?.Value ?? string.Empty;
        foreach (var diagnostic in new[] { "CS0419", "CS1570", "CS1572", "CS1573", "CS1574", "CS1580", "CS1581", "CS1584", "CS1587", "CS1591", "CS1658", "CS1711", "CS1712" })
        {
            Assert.Contains(diagnostic, warningsAsErrors, $"XML documentation diagnostic {diagnostic} must remain a build error.");
        }

        var pragmaDirectives = new[] { TestRepositoryPaths.LibraryProjectDirectory, TestRepositoryPaths.TestProjectDirectory }
            .SelectMany(sourceRoot => Directory.EnumerateFiles(sourceRoot, "*.cs", SearchOption.AllDirectories))
            .Where(path => !HasDirectorySegment(path, "bin") && !HasDirectorySegment(path, "obj"))
            .SelectMany(path => File.ReadLines(path).Select((line, index) => new { Path = path, Line = line, Number = index + 1 }))
            .Where(entry => entry.Line.TrimStart().StartsWith("#pragma", StringComparison.Ordinal))
            .Select(entry => $"{entry.Path}:{entry.Number}: {entry.Line.Trim()}")
            .ToArray();
        Assert.HasCount(0, pragmaDirectives, "Handwritten C# sources contain pragma directives:\n" + string.Join("\n", pragmaDirectives));
    }

    internal static string BuildPublicSurfaceSnapshot()
    {
        var assembly = typeof(GameEventScriptProgram).Assembly;
        var output = new StringBuilder();
        foreach (var type in assembly.GetExportedTypes().OrderBy(type => type.FullName, StringComparer.Ordinal))
        {
            output.AppendLine(FormatKind(type) + " " + (type.FullName ?? type.Name).Replace("+", "."));
            if (type.IsEnum)
            {
                foreach (var name in Enum.GetNames(type))
                {
                    output.AppendLine("  enum-field " + name + " = " + Convert.ToInt64(Enum.Parse(type, name), System.Globalization.CultureInfo.InvariantCulture));
                }

                continue;
            }

            var members = type
                .GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Where(member => member.MemberType != MemberTypes.NestedType)
                .Where(member => member is not MethodInfo method || !IsPropertyOrEventAccessor(method))
                .Select(FormatMember)
                .OrderBy(line => line, StringComparer.Ordinal);

            foreach (var member in members)
            {
                output.AppendLine(member);
            }
        }

        return output.ToString();
    }

    private static string FindApprovedSnapshotPath()
    {
        return Path.Combine(TestRepositoryPaths.TestProjectDirectory, "Native", "ApiSurface", "PublicApiSurface.approved.txt");
    }

    private static bool HasDirectorySegment(string path, string segment)
    {
        var separator = Path.DirectorySeparatorChar;
        return path.Contains(separator + segment + separator, StringComparison.Ordinal);
    }

    private static string FormatType(Type type)
    {
        if (type.IsGenericParameter)
        {
            return type.Name;
        }

        if (type.IsByRef)
        {
            return FormatType(type.GetElementType()!) + "&";
        }

        if (type.IsArray)
        {
            return FormatType(type.GetElementType()!) + "[]";
        }

        var nullable = Nullable.GetUnderlyingType(type);
        if (nullable is not null)
        {
            return FormatType(nullable) + "?";
        }

        if (!type.IsGenericType)
        {
            return (type.FullName ?? type.Name).Replace("+", ".");
        }

        var definition = type.GetGenericTypeDefinition();
        var name = (definition.FullName ?? definition.Name).Replace("+", ".");
        var tick = name.IndexOf('`');
        if (tick >= 0)
        {
            name = name[..tick];
        }

        return name + "<" + string.Join(", ", type.GetGenericArguments().Select(FormatType)) + ">";
    }

    private static string FormatKind(Type type)
    {
        if (type.IsEnum)
        {
            return "enum";
        }

        if (type.IsInterface)
        {
            return "interface";
        }

        if (type.IsValueType)
        {
            return "struct";
        }

        if (type.IsAbstract && type.IsSealed)
        {
            return "static class";
        }

        if (type.IsAbstract)
        {
            return "abstract class";
        }

        return type.IsSealed ? "sealed class" : "class";
    }

    private static string FormatMember(MemberInfo member)
    {
        switch (member)
        {
            case ConstructorInfo constructor:
                return "  ctor " + constructor.DeclaringType!.Name + "(" + FormatParameters(constructor.GetParameters()) + ")";
            case MethodInfo method:
                var genericArguments = method.IsGenericMethodDefinition
                    ? "<" + string.Join(", ", method.GetGenericArguments().Select(argument => argument.Name)) + ">"
                    : string.Empty;
                return "  method " + (method.IsStatic ? "static " : string.Empty) + FormatType(method.ReturnType) + " " + method.Name + genericArguments + "(" + FormatParameters(method.GetParameters()) + ")";
            case PropertyInfo property:
                var access = (property.CanRead ? "get" : string.Empty) + (property.CanWrite ? " set" : string.Empty);
                return "  property " + FormatType(property.PropertyType) + " " + property.Name + " { " + access.Trim() + " }";
            case FieldInfo field:
                return "  field " + (field.IsStatic ? "static " : string.Empty) + FormatType(field.FieldType) + " " + field.Name;
            case EventInfo @event:
                return "  event " + FormatType(@event.EventHandlerType!) + " " + @event.Name;
            default:
                return "  " + member.MemberType + " " + member.Name;
        }
    }

    private static string FormatParameters(ParameterInfo[] parameters)
        => string.Join(", ", parameters.Select(parameter => FormatType(parameter.ParameterType) + " " + parameter.Name));

    private static bool IsPropertyOrEventAccessor(MethodInfo method)
        => method.IsSpecialName &&
           (method.Name.StartsWith("get_", StringComparison.Ordinal) ||
            method.Name.StartsWith("set_", StringComparison.Ordinal) ||
            method.Name.StartsWith("add_", StringComparison.Ordinal) ||
            method.Name.StartsWith("remove_", StringComparison.Ordinal));

    private static bool IsNamespace(Type type, string namespacePrefix)
        => type.Namespace is { } typeNamespace &&
           (string.Equals(typeNamespace, namespacePrefix, StringComparison.Ordinal) ||
            typeNamespace.StartsWith(namespacePrefix + ".", StringComparison.Ordinal));
}
