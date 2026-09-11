// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System.Reflection;
using System.Text;
using System.Xml.Linq;
using StepH.GameEventScript.Api;
using StepH.GameEventScript.Conformance;
using StepH.GameEventScript.CSharpBridge;

namespace StepH_GameEventScript_Tests.Native.ApiSurface;

[TestClass]
public sealed class GameEventScriptPublicApiSurfaceTests
{
    [TestMethod]
    public void PublicApiSurfaceMatchesApprovedSnapshot()
    {
        var actual = BuildPublicSurfaceSnapshot();
        var approvedPath = FindApprovedSnapshotPath();
        var receivedDirectory = Path.Combine(TestRepositoryPaths.Root, "artifacts", "csharp", "api");
        Directory.CreateDirectory(receivedDirectory);
        var receivedPath = Path.Combine(receivedDirectory, "PublicApiSurface.received.txt");

        File.WriteAllText(receivedPath, actual);

        var expected = File.ReadAllText(approvedPath);

        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void CompilerTypesStayInternal()
    {
        var leakedTypes = typeof(GameEventScriptBuilder)
            .Assembly
            .GetExportedTypes()
            .Where(type =>
                IsNamespace(type, "StepH.GameEventScript.Compiler"))
                .Select(type => type.FullName)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();

        Assert.HasCount(0, leakedTypes, "Public compiler types leaked:\n" + string.Join("\n", leakedTypes));
    }

    [TestMethod]
    public void PublicTypesAndDependenciesFollowAssemblyBoundaries()
    {
        var runtime = typeof(GameEventScriptProgram).Assembly;
        var compiler = typeof(GameEventScriptBuilder).Assembly;
        var bridge = typeof(GameEventScriptCSharpHostRunner).Assembly;
        var conformance = typeof(ConformanceRunner).Assembly;

        Assert.AreEqual("StepH.GameEventScript", runtime.GetName().Name);
        Assert.AreEqual("StepH.GameEventScript.Compiler", compiler.GetName().Name);
        Assert.AreEqual("StepH.GameEventScript.CSharpBridge", bridge.GetName().Name);
        Assert.AreEqual("StepH.GameEventScript.Conformance", conformance.GetName().Name);
        CollectionAssert.AreEquivalent(new[] { typeof(GameEventScriptBuilder), typeof(GameEventScriptCompileOptions), typeof(GameEventScriptCompileException) }, compiler.GetExportedTypes());
        Assert.HasCount(0, runtime.GetTypes().Where(type => IsNamespace(type, "StepH.GameEventScript.Compiler")));
        Assert.HasCount(0, runtime.GetExportedTypes().Where(type => IsNamespace(type, "StepH.GameEventScript.CSharpBridge") || IsNamespace(type, "StepH.GameEventScript.Conformance")));
        Assert.IsTrue(bridge.GetExportedTypes().All(type => IsNamespace(type, "StepH.GameEventScript.CSharpBridge")));
        Assert.IsTrue(conformance.GetExportedTypes().All(type => IsNamespace(type, "StepH.GameEventScript.Conformance")));

        AssertProductReferences(runtime);
        AssertProductReferences(compiler, "StepH.GameEventScript");
        AssertProductReferences(bridge, "StepH.GameEventScript");
        AssertProductReferences(conformance, "StepH.GameEventScript", "StepH.GameEventScript.Compiler");
    }

    /// <summary>
    /// Verifies that the XML-documentation build gate remains active, every exported type reaches the artifact, and handwritten library sources contain no pragma directives.
    /// </summary>
    [TestMethod]
    public void PublicApiDocumentationAndPragmasRemainComplete()
    {
        var undocumentedTypes = new List<string>();
        foreach (var assembly in PublicAssemblies())
        {
            var xmlPath = Path.ChangeExtension(assembly.Location, ".xml");
            Assert.IsTrue(File.Exists(xmlPath), $"The public XML documentation artifact for {assembly.GetName().Name} was not generated.");

            var documentedMembers = XDocument.Load(xmlPath)
                .Descendants("member")
                .Select(member => (string?)member.Attribute("name"))
                .Where(name => name is not null)
                .ToHashSet(StringComparer.Ordinal);

            undocumentedTypes.AddRange(assembly
                .GetExportedTypes()
                .Select(type => "T:" + (type.FullName ?? type.Name).Replace('+', '.'))
                .Where(typeId => !documentedMembers.Contains(typeId)));
        }

        undocumentedTypes.Sort(StringComparer.Ordinal);
        Assert.HasCount(0, undocumentedTypes, "Exported types without XML documentation:\n" + string.Join("\n", undocumentedTypes));

        foreach (var projectDirectory in TestRepositoryPaths.ProductProjectDirectories)
        {
            var projectPath = Directory.EnumerateFiles(projectDirectory, "*.csproj", SearchOption.TopDirectoryOnly).Single();
            var project = XDocument.Load(projectPath);
            Assert.AreEqual("true", project.Descendants("GenerateDocumentationFile").SingleOrDefault()?.Value, $"{Path.GetFileName(projectPath)} must generate public XML documentation.");

            var warningsAsErrors = project.Descendants("WarningsAsErrors").SingleOrDefault()?.Value ?? string.Empty;
            foreach (var diagnostic in new[] { "CS0419", "CS1570", "CS1572", "CS1573", "CS1574", "CS1580", "CS1581", "CS1584", "CS1587", "CS1591", "CS1658", "CS1711", "CS1712" })
                Assert.Contains(diagnostic, warningsAsErrors, $"XML documentation diagnostic {diagnostic} must remain a build error in {Path.GetFileName(projectPath)}.");
        }

        var pragmaDirectives = TestRepositoryPaths.ProductProjectDirectories.Append(TestRepositoryPaths.TestProjectDirectory)
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
        var output = new StringBuilder();
        foreach (var type in PublicAssemblies().SelectMany(assembly => assembly.GetExportedTypes()).OrderBy(type => type.FullName, StringComparer.Ordinal))
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

    private static void AssertProductReferences(Assembly assembly, params string[] expected)
    {
        var references = assembly.GetReferencedAssemblies().Select(reference => reference.Name!).ToArray();
        CollectionAssert.AreEquivalent(expected, references.Where(name => name.StartsWith("StepH.GameEventScript", StringComparison.Ordinal)).ToArray(), assembly.GetName().Name);
        Assert.IsFalse(references.Any(name => name.StartsWith("Beamable", StringComparison.Ordinal) || name.StartsWith("Unity", StringComparison.Ordinal)));
    }

    private static Assembly[] PublicAssemblies()
        => [typeof(GameEventScriptProgram).Assembly, typeof(GameEventScriptBuilder).Assembly, typeof(GameEventScriptCSharpHostRunner).Assembly, typeof(ConformanceRunner).Assembly];
}
