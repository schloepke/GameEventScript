// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using System.Text;
using GameEventScript.Api;

namespace GameEventScript.Generator;

// The schema set of a generation: the type that SchemaSet declares, or the one that UseSchemaSet names. Conversions, network
// bindings and document readers call it in place of the private helpers of the file.
internal sealed class GesSchemaSetReference(string? declared, string qualified)
{
    // The declared name, or null where another generation declares the type.
    internal string? Declared { get; } = declared;

    // The name that generated code calls it by: C# with global::, Swift as given.
    internal string Qualified { get; } = qualified;

    // Checks the options and gives the schema set of the generation, or null without one.
    internal static GesSchemaSetReference? From(GesSchemaGenerateOptions options)
    {
        var swift = options.Language == GesSchemaLanguage.Swift;
        var keywords = swift ? GesSwiftEmitter.Keywords : GesCSharpEmitter.Keywords;
        if (!swift && options.Imports.Count > 0)
            throw new GesGeneratorException(GesGeneratorException.Usage, "Imports concern Swift, whose generated files import modules; C# names types with qualified names.");
        foreach (var module in options.Imports)
            if (!Identifier(module, keywords)) throw new GesGeneratorException(GesGeneratorException.Usage, $"The module '{module}' is no identifier.");
        if (options.SchemaSet is not null && options.UseSchemaSet is not null)
            throw new GesGeneratorException(GesGeneratorException.Usage, "A generation either declares a schema set or uses the one of another generation, not both.");
        if (options.SchemaSet is { } name)
        {
            if (!Identifier(name, keywords)) throw new GesGeneratorException(GesGeneratorException.Usage, $"The schema set name '{name}' is no identifier.");
            var space = GesSchemaPlanner.Namespace(options.Namespace, keywords);
            return new GesSchemaSetReference(name, swift ? name : "global::" + string.Concat(space.ConvertAll(part => part + ".")) + name);
        }

        if (options.UseSchemaSet is not { } used) return null;
        var parts = used.Split('.');
        foreach (var part in parts)
            if (!Identifier(part, keywords)) throw new GesGeneratorException(GesGeneratorException.Usage, $"The schema set name '{used}' is no sequence of identifiers.");
        return new GesSchemaSetReference(null, swift ? used : "global::" + used);
    }

    // Adds the declared name to the top-level names of the generation, which no other type may take.
    internal void Reserve(HashSet<string> names)
    {
        if (Declared is { } name && !names.Add(name))
            throw new GesGeneratorException(GesGeneratorException.NameCollision, $"The schema set takes the name '{name}', which another type of the generation has.");
    }

    private static bool Identifier(string text, ISet<string> keywords)
    {
        var valid = text.Length > 0 && (text[0] is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or '_') && !keywords.Contains(text);
        foreach (var character in text) valid &= character is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or '_';
        return valid;
    }

    // FNV-1a 64 over the UTF-8 bytes of each source's name and text and each parameter's name and literal, each followed by a
    // zero byte, in the order of the generation.
    internal static ulong Hash(List<GesSchemaSource> sources, List<GesLayoutParameter> parameters)
    {
        var hash = 14695981039346656037UL;
        void Add(string text)
        {
            foreach (var value in Encoding.UTF8.GetBytes(text)) hash = unchecked((hash ^ value) * 1099511628211UL);
            hash = unchecked(hash * 1099511628211UL);
        }

        foreach (var source in sources)
        {
            Add(source.Name);
            Add(source.Text);
        }

        foreach (var parameter in parameters)
        {
            Add(parameter.Name);
            Add(parameter.Literal);
        }

        return hash;
    }

    internal static void WriteCSharp(GesSchemaPlanner.Code code, string name, string access, List<GesSchemaSource> sources, List<GesLayoutParameter> parameters, bool networks)
    {
        code.Blank();
        code.Line("/// <summary>");
        code.Line("/// The schema set of the generated code: the embedded schema sources, parsed once, the parameter values of the generation");
        code.Line("/// and the binding of schemas" + (networks ? ", networks" : "") + " and documents against them, which other generations share.");
        code.Line("/// </summary>");
        code.Line($"{access} static class {name}");
        code.Open("{");
        code.Line("private static GesSchemaSet? __set;");
        code.Blank();
        code.Line("/// <summary>Gets the FNV-1a 64 hash over the embedded sources and the parameter values.</summary>");
        code.Line($"public static ulong Hash => 0x{GesSchemaLayoutJson.Hex(Hash(sources, parameters))}UL;");
        code.Blank();
        code.Line("/// <summary>Gets the embedded schema sources, parsed on first access.</summary>");
        code.Line("/// <exception cref=\"GesSchemaException\">The runtime no longer parses the sources.</exception>");
        code.Line("public static GesSchemaSet Set => __set ??= GesSchemaSet.Parse(");
        code.Indent();
        for (var index = 0; index < sources.Count; index++)
        {
            var source = sources[index];
            code.Line($"new GesSchemaSource({GesSchemaPlanner.Literal(source.Name, false)}, string.Join(\"\\n\", new[]");
            code.Open("{");
            foreach (var line in source.Text.Split('\n')) code.Line(GesSchemaPlanner.Literal(line, false) + ",");
            code.Close(index + 1 < sources.Count ? "}))," : "})));");
        }

        code.Dedent();
        code.Blank();
        code.Line("/// <summary>Gets the parameter values of the generation, a new instance on each access.</summary>");
        var values = "new GesSchemaParameters()";
        foreach (var parameter in parameters) values += $".With({GesSchemaPlanner.Literal(parameter.Name, false)}, {GesSchemaPlanner.Literal(parameter.Literal, false)})";
        code.Line($"public static GesSchemaParameters Parameters => {values};");
        code.Blank();
        code.Line("/// <summary>Binds a schema with the parameter values and checks that it binds like the generated code.</summary>");
        code.Line("/// <param name=\"root\">The schema, message or message set.</param>");
        code.Line("/// <param name=\"bindingHash\">The binding hash of the generated code.</param>");
        code.Line("/// <returns>The bound schema.</returns>");
        code.Line("/// <exception cref=\"global::System.ArgumentException\">The root is not declared.</exception>");
        code.Line("/// <exception cref=\"GesSchemaException\">The sources no longer parse or the root no longer binds.</exception>");
        code.Line("/// <exception cref=\"global::System.InvalidOperationException\">The binding hash differs from <paramref name=\"bindingHash\"/>.</exception>");
        code.Line("public static GesBoundSchema Bind(string root, ulong bindingHash)");
        code.Open("{");
        code.Line("var bound = Set.Schema(root).Bind(Parameters);");
        code.Line("if (bound.BindingHash != bindingHash) throw new global::System.InvalidOperationException($\"The embedded schema sources no longer bind '{root}' like the generated code.\");");
        code.Line("return bound;");
        code.Close();
        code.Blank();
        code.Line("/// <summary>Binds a network with the parameter values and checks that it binds like the generated code.</summary>");
        code.Line("/// <param name=\"name\">The network.</param>");
        code.Line("/// <param name=\"hash\">The network hash of the generated code.</param>");
        code.Line("/// <returns>The bound network.</returns>");
        code.Line("/// <exception cref=\"global::System.ArgumentException\">The network is not declared.</exception>");
        code.Line("/// <exception cref=\"GesSchemaException\">The sources no longer parse or the network no longer binds.</exception>");
        code.Line("/// <exception cref=\"global::System.InvalidOperationException\">The network hash differs from <paramref name=\"hash\"/>.</exception>");
        code.Line("public static GesBoundNetwork BindNetwork(string name, ulong hash)");
        code.Open("{");
        code.Line("var bound = Set.Network(name).Bind(Parameters);");
        code.Line("if (bound.Hash != hash) throw new global::System.InvalidOperationException($\"The embedded schema sources no longer bind the network '{name}' like the generated code.\");");
        code.Line("return bound;");
        code.Close();
        code.Blank();
        code.Line("/// <summary>Reads GES document text and checks it against the embedded schema sources.</summary>");
        code.Line("/// <param name=\"text\">The document text.</param>");
        code.Line("/// <param name=\"roots\">The schemas the header may name.</param>");
        code.Line("/// <returns>The checked document.</returns>");
        code.Line("/// <exception cref=\"GesDocumentException\">The text is no document, names another schema or breaks its schema.</exception>");
        code.Line("/// <exception cref=\"GesSchemaException\">The runtime no longer parses the sources.</exception>");
        code.Line("public static GesSchemaDocument ReadDocument(string text, params string[] roots) => GesSchemaDocument.Read(Set, text, roots);");
        code.Close();
    }

    internal static void WriteSwift(GesSchemaPlanner.Code code, string name, string access, List<GesSchemaSource> sources, List<GesLayoutParameter> parameters, bool networks)
    {
        code.Blank();
        code.Line("/// The schema set of the generated code: the embedded schema sources, parsed once, the parameter values of the generation");
        code.Line("/// and the binding of schemas" + (networks ? ", networks" : "") + " and documents against them, which other generations share.");
        code.Open($"{access}enum {name} {{");
        code.Line("/// A schema or network that no longer binds like the generated code.");
        code.Open($"{access}struct Mismatch: Error, Sendable {{");
        code.Line("/// The schema or network.");
        code.Line($"{access}let name: String");
        code.Close();
        code.Blank();
        code.Line("/// The FNV-1a 64 hash over the embedded sources and the parameter values.");
        code.Line($"{access}static let hash: UInt64 = 0x{GesSchemaLayoutJson.Hex(Hash(sources, parameters))}");
        code.Blank();
        code.Open("private static let __set = Result<GesSchemaSet, any Error> {");
        code.Open("try GesSchemaSet(sources: [");
        foreach (var source in sources)
        {
            code.Open($"GesSchemaSource(name: {GesSchemaPlanner.Literal(source.Name, true)}, text: [");
            foreach (var line in source.Text.Split('\n')) code.Line(GesSchemaPlanner.Literal(line, true) + ",");
            code.Close("].joined(separator: \"\\n\")),");
        }

        code.Close("])");
        code.Close();
        code.Blank();
        code.Line("/// The embedded schema sources, parsed on first access.");
        code.Line("/// - Throws: `GesSchemaError` when the runtime no longer parses the sources.");
        code.Open($"{access}static var set: GesSchemaSet {{");
        code.Line("get throws { try __set.get() }");
        code.Close();
        code.Blank();
        code.Line("/// The parameter values of the generation, a new instance on each call.");
        code.Line("/// - Throws: `GesSchemaError` when the runtime no longer reads a value.");
        code.Open($"{access}static func parameters() throws -> GesSchemaParameters {{");
        code.Line(parameters.Count == 0 ? "GesSchemaParameters()" : "let parameters = GesSchemaParameters()");
        foreach (var parameter in parameters) code.Line($"try parameters.with({GesSchemaPlanner.Literal(parameter.Name, true)}, literal: {GesSchemaPlanner.Literal(parameter.Literal, true)})");
        if (parameters.Count > 0) code.Line("return parameters");
        code.Close();
        code.Blank();
        code.Line("/// Binds a schema with the parameter values and checks that it binds like the generated code.");
        code.Line("/// - Parameters:");
        code.Line("///   - root: The schema, message or message set.");
        code.Line("///   - bindingHash: The binding hash of the generated code.");
        code.Line("/// - Returns: The bound schema.");
        code.Line("/// - Throws: `GesSchemaError` when the sources no longer parse or the root is not declared or no longer binds, and");
        code.Line("///   `Mismatch` when the binding hash differs.");
        code.Open($"{access}static func bind(_ root: String, bindingHash: UInt64) throws -> GesBoundSchema {{");
        code.Line("let bound = try set.schema(root).bind(parameters: parameters())");
        code.Line("if bound.bindingHash != bindingHash { throw Mismatch(name: root) }");
        code.Line("return bound");
        code.Close();
        code.Blank();
        code.Line("/// Binds a network with the parameter values and checks that it binds like the generated code.");
        code.Line("/// - Parameters:");
        code.Line("///   - name: The network.");
        code.Line("///   - hash: The network hash of the generated code.");
        code.Line("/// - Returns: The bound network.");
        code.Line("/// - Throws: `GesSchemaError` when the sources no longer parse or the network is not declared or no longer binds,");
        code.Line("///   and `Mismatch` when the network hash differs.");
        code.Open($"{access}static func bindNetwork(_ name: String, hash: UInt64) throws -> GesBoundNetwork {{");
        code.Line("let bound = try set.network(name).bind(parameters: parameters())");
        code.Line("if bound.hash != hash { throw Mismatch(name: name) }");
        code.Line("return bound");
        code.Close();
        code.Blank();
        code.Line("/// Reads GES document text and checks it against the embedded schema sources.");
        code.Line("/// - Parameters:");
        code.Line("///   - text: The document text.");
        code.Line("///   - roots: The schemas the header may name.");
        code.Line("/// - Returns: The checked document.");
        code.Line("/// - Throws: `GesDocumentError` when the text is no document, names another schema or breaks its schema, and");
        code.Line("///   `document.schemaMismatch` when the runtime no longer parses the sources.");
        code.Open($"{access}static func readDocument(_ text: String, roots: [String]) throws(GesDocumentError) -> GesSchemaDocument {{");
        code.Open("guard let set = try? __set.get() else {");
        code.Line("throw GesDocumentError(code: GesSchemaDocument.schemaMismatch, line: 1, column: 1, message: \"The embedded schema sources no longer parse.\")");
        code.Close();
        code.Line("return try GesSchemaDocument(text, schemas: set, expected: roots)");
        code.Close();
        code.Close();
    }
}
