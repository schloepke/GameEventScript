// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using GameEventScript.Api;

namespace GameEventScript.Generator;

/// <summary>
/// Generates code and layouts from schemas at build time. Every type reached from the roots is bound once with the given
/// parameters, so a type that several roots share appears once. The generator belongs to build tools and is never part of
/// a game.
/// </summary>
public static class GesSchemaGenerator
{
    /// <summary>
    /// Writes the language-neutral layout of the roots as canonical JSON, the input of every code emitter. Every port writes
    /// identical text for identical schemas and parameters.
    /// </summary>
    /// <param name="set">The schemas.</param>
    /// <param name="roots">The schemas, messages or message sets to generate, in the order the output names them.</param>
    /// <param name="parameters">A value for every parameter the roots reach.</param>
    /// <returns>The JSON text, ending with a line break.</returns>
    /// <exception cref="ArgumentException">A root is not declared.</exception>
    /// <exception cref="GesGeneratorException">No root is given.</exception>
    /// <exception cref="GesSchemaException">A root cannot be bound, for example because a parameter has no value.</exception>
    public static string Layout(GesSchemaSet set, IReadOnlyList<string> roots, GesSchemaParameters parameters)
    {
        if (set is null) throw new ArgumentNullException(nameof(set));
        if (roots is null) throw new ArgumentNullException(nameof(roots));
        if (parameters is null) throw new ArgumentNullException(nameof(parameters));
        return GesSchemaLayoutJson.Write(GesSchemaLayoutBuilder.Build(set, roots, parameters));
    }

    /// <summary>
    /// Generates code for the roots and every type they reach: a type per schema that writes and reads its records with
    /// the bytes, error codes and paths of <see cref="GesSchemaWriter"/> and <see cref="GesSchemaReader"/>. Every port
    /// writes identical text for identical schemas, parameters and options.
    /// </summary>
    /// <param name="set">The schemas.</param>
    /// <param name="roots">The schemas to generate, in the order the header names them.</param>
    /// <param name="parameters">A value for every parameter the roots reach; they become constants of the code.</param>
    /// <param name="options">The language, namespace and visibility.</param>
    /// <returns>The source text, ending with a line break.</returns>
    /// <exception cref="ArgumentException">A root is not declared.</exception>
    /// <exception cref="GesSchemaException">A root cannot be bound, for example because a parameter has no value.</exception>
    /// <exception cref="GesGeneratorException">No root is given, the namespace is invalid, two generated names collide, or
    /// a reached type cannot be generated yet.</exception>
    public static string Generate(GesSchemaSet set, IReadOnlyList<string> roots, GesSchemaParameters parameters, GesSchemaGenerateOptions options)
    {
        if (set is null) throw new ArgumentNullException(nameof(set));
        if (roots is null) throw new ArgumentNullException(nameof(roots));
        if (parameters is null) throw new ArgumentNullException(nameof(parameters));
        if (options is null) throw new ArgumentNullException(nameof(options));
        if (options.Split != GesSchemaSplit.None) throw new GesGeneratorException(GesGeneratorException.Usage, "Split code consists of several files; GenerateFiles returns them.");
        return GesCodeFiles.Single(Wire(set, roots, parameters, options, out _));
    }

    /// <summary>
    /// Generates the code of <see cref="Generate"/> as several files: one per declaration or per schema source as
    /// <see cref="GesSchemaGenerateOptions.Split"/> chooses, a shared file whose name begins with <c>_</c> for the namespace and
    /// the embedded schema sources, and last the manifest <c>.ges-generated.json</c>, which names the options, roots,
    /// parameters, hashes and files. Nested types stay in the file of their record. Every port writes identical files.
    /// </summary>
    /// <param name="set">The schemas.</param>
    /// <param name="roots">The schemas to generate, in the order the manifest names them.</param>
    /// <param name="parameters">A value for every parameter the roots reach; they become constants of the code.</param>
    /// <param name="options">The language, namespace, visibility and split.</param>
    /// <returns>The files ordered by name, and the manifest last.</returns>
    /// <exception cref="ArgumentException">A root is not declared.</exception>
    /// <exception cref="GesSchemaException">A root cannot be bound, for example because a parameter has no value.</exception>
    /// <exception cref="GesGeneratorException">No root or no split is given, the namespace is invalid, two generated names or
    /// two file names that differ only in ASCII case collide, or a reached type cannot be generated yet.</exception>
    public static IReadOnlyList<GesGeneratedFile> GenerateFiles(GesSchemaSet set, IReadOnlyList<string> roots, GesSchemaParameters parameters, GesSchemaGenerateOptions options)
    {
        if (set is null) throw new ArgumentNullException(nameof(set));
        if (roots is null) throw new ArgumentNullException(nameof(roots));
        if (parameters is null) throw new ArgumentNullException(nameof(parameters));
        if (options is null) throw new ArgumentNullException(nameof(options));
        var document = Wire(set, roots, parameters, options, out var layout);
        return GesCodeFiles.Split(document, options.Split, set.SourceName, files => Manifest(options, model: false, layout.Roots, root => (layout.Type(root).Hash, layout.Type(root).BindingHash), layout.Parameters, files));
    }

    private static GesCodeDocument Wire(GesSchemaSet set, IReadOnlyList<string> roots, GesSchemaParameters parameters, GesSchemaGenerateOptions options, out GesSchemaLayout layout)
    {
        if (options.Bindings) throw new GesGeneratorException(GesGeneratorException.Usage, "Bindings concern model code; wire code converts through Values.");
        if (options.Documents) throw new GesGeneratorException(GesGeneratorException.Usage, "Documents concern model code; wire code converts through Values.");
        // Wire records and models of one schema share its name, so they need different namespaces.
        if (options.Models is not null && options.Models == options.Namespace)
            throw new GesGeneratorException(GesGeneratorException.NameCollision, $"The models live in the namespace of the wire code ('{options.Models}'), where a wire record and the model of its schema would share a name; generate them into different namespaces.");
        // Swift declares each namespace component as an enum, so two generations of one module cannot share the first one.
        if (options.Language == GesSchemaLanguage.Swift && options.Models is { Length: > 0 } && options.Namespace.Length > 0
            && options.Models.Split('.')[0] == options.Namespace.Split('.')[0])
            throw new GesGeneratorException(GesGeneratorException.NameCollision, $"The Swift namespaces '{options.Namespace}' and '{options.Models}' of wire code and models start alike, so both would declare the enum '{options.Namespace.Split('.')[0]}'; give them different first components.");
        layout = GesSchemaLayoutBuilder.Build(set, roots, parameters);
        var types = GesSchemaPlanner.Plan(layout);
        // Packed records are model types over one word, which wire code declares itself unless it shares those of models.
        var packed = layout.Types.FindAll(type => type.PackedWidth > 0).ConvertAll(type => type.Name);
        var shared = packed.Count > 0 && options.Models is null ? GesModelLayoutBuilder.Build(set, packed) : null;
        // With models, wire records that a model root of any group reaches convert to their model directly.
        var modelRoots = new List<string>();
        if (options.Models is not null)
            foreach (var schema in set.Schemas)
                if (schema.ModelGroup is not null) modelRoots.Add(schema.Name);
        var models = modelRoots.Count > 0 ? GesModelLayoutBuilder.Build(set, modelRoots) : null;
        return options.Language == GesSchemaLanguage.CSharp ? GesCSharpEmitter.Emit(layout, types, options, shared, models) : GesSwiftEmitter.Emit(layout, types, options, shared, models);
    }

    /// <summary>
    /// Lists the wire roots of a group: the declarations marked <c>wire(group)</c>, or <c>wire</c> for the group
    /// <c>default</c>, in declaration order.
    /// </summary>
    /// <param name="set">The schemas.</param>
    /// <param name="group">The group.</param>
    /// <returns>The names, empty when the group has no root.</returns>
    public static IReadOnlyList<string> WireRoots(GesSchemaSet set, string group) => Roots(set, group, wire: true);

    /// <summary>
    /// Lists the model roots of a group: the schemas marked <c>model(group)</c>, or <c>model</c> for the group
    /// <c>default</c>, in declaration order.
    /// </summary>
    /// <param name="set">The schemas.</param>
    /// <param name="group">The group.</param>
    /// <returns>The names, empty when the group has no root.</returns>
    public static IReadOnlyList<string> ModelRoots(GesSchemaSet set, string group) => Roots(set, group, wire: false);

    /// <summary>
    /// Writes the language-neutral model layout of the roots as canonical JSON: every schema they reach, described without
    /// binding, since models are never encoded. Every port writes identical text.
    /// </summary>
    /// <param name="set">The schemas.</param>
    /// <param name="roots">The schemas to generate as models, in the order the output names them.</param>
    /// <returns>The JSON text, ending with a line break.</returns>
    /// <exception cref="GesSchemaException">A root is not declared.</exception>
    /// <exception cref="GesGeneratorException">No root is given, a root is no schema, or a reached type has no model type.</exception>
    public static string ModelLayout(GesSchemaSet set, IReadOnlyList<string> roots)
    {
        if (set is null) throw new ArgumentNullException(nameof(set));
        if (roots is null) throw new ArgumentNullException(nameof(roots));
        return GesModelLayoutJson.Write(GesModelLayoutBuilder.Build(set, roots));
    }

    /// <summary>
    /// Generates model types for the roots and every schema they reach: plain values with value equality that are never
    /// encoded, so they need no ranges, steps or parameter values and may use document-only types. Every port writes
    /// identical text for identical schemas and options.
    /// </summary>
    /// <param name="set">The schemas.</param>
    /// <param name="roots">The schemas to generate as models, in the order the header names them.</param>
    /// <param name="options">The language, namespace and visibility; <see cref="GesSchemaGenerateOptions.Values"/> adds plain
    /// GES values, and <see cref="GesSchemaGenerateOptions.CheckedExclusivity"/> concerns wire code and must stay off.</param>
    /// <returns>The source text, ending with a line break.</returns>
    /// <exception cref="GesSchemaException">A root is not declared.</exception>
    /// <exception cref="GesGeneratorException">No root is given, a root is no schema, an option concerns wire code, the
    /// namespace is invalid, two generated names collide, or a reached type has no model type.</exception>
    public static string GenerateModels(GesSchemaSet set, IReadOnlyList<string> roots, GesSchemaGenerateOptions options)
    {
        if (set is null) throw new ArgumentNullException(nameof(set));
        if (roots is null) throw new ArgumentNullException(nameof(roots));
        if (options is null) throw new ArgumentNullException(nameof(options));
        if (options.Split != GesSchemaSplit.None) throw new GesGeneratorException(GesGeneratorException.Usage, "Split code consists of several files; GenerateModelFiles returns them.");
        return GesCodeFiles.Single(Models(set, roots, options, out _));
    }

    /// <summary>
    /// Generates the model types of <see cref="GenerateModels"/> as several files: one per declaration or per schema source
    /// as <see cref="GesSchemaGenerateOptions.Split"/> chooses, with the protocol of a schema in its file, a shared file
    /// whose name begins with <c>_</c> for the namespace, the conversions and the helpers, and last the manifest
    /// <c>.ges-generated.json</c>. Every port writes identical files.
    /// </summary>
    /// <param name="set">The schemas.</param>
    /// <param name="roots">The schemas to generate as models, in the order the manifest names them.</param>
    /// <param name="options">The language, namespace, visibility, conversions and split.</param>
    /// <returns>The files ordered by name, and the manifest last.</returns>
    /// <exception cref="GesSchemaException">A root is not declared.</exception>
    /// <exception cref="GesGeneratorException">No root or no split is given, a root is no schema, an option concerns wire
    /// code, the namespace is invalid, two generated names or two file names that differ only in ASCII case collide, or a
    /// reached type has no model type.</exception>
    public static IReadOnlyList<GesGeneratedFile> GenerateModelFiles(GesSchemaSet set, IReadOnlyList<string> roots, GesSchemaGenerateOptions options)
    {
        if (set is null) throw new ArgumentNullException(nameof(set));
        if (roots is null) throw new ArgumentNullException(nameof(roots));
        if (options is null) throw new ArgumentNullException(nameof(options));
        var document = Models(set, roots, options, out var layout);
        return GesCodeFiles.Split(document, options.Split, set.SourceName, files => Manifest(options, model: true, layout.Roots, root => (layout.Type(root).Hash, null), [], files));
    }

    private static GesCodeDocument Models(GesSchemaSet set, IReadOnlyList<string> roots, GesSchemaGenerateOptions options, out GesModelLayout layout)
    {
        if (options.CheckedExclusivity) throw new GesGeneratorException(GesGeneratorException.Usage, "Checked exclusivity concerns wire code, not models.");
        if (options.Models is not null) throw new GesGeneratorException(GesGeneratorException.Usage, "Models concern wire code, which shares their types; models do not name another namespace.");
        layout = GesModelLayoutBuilder.Build(set, roots);
        // Reading documents converts through the plain values.
        var effective = options.Documents && !options.Values ? new GesSchemaGenerateOptions { Language = options.Language, Namespace = options.Namespace, Access = options.Access, Values = true, Bindings = options.Bindings, Documents = true, Split = options.Split } : options;
        return effective.Language == GesSchemaLanguage.CSharp ? GesCSharpModelEmitter.Emit(layout, effective) : GesSwiftModelEmitter.Emit(layout, effective);
    }

    // The manifest of split output: the options, the roots with their hashes, the parameters and the files, so that a tool
    // knows which files of the directory it owns.
    private static string Manifest(GesSchemaGenerateOptions options, bool model, List<string> roots, Func<string, (ulong Hash, ulong? BindingHash)> hashes, List<GesLayoutParameter> parameters, List<string> files)
    {
        var json = new GesSchemaLayoutJson.Writer();
        json.BeginObject();
        json.Number("manifest", 1);
        json.String("code", model ? "model" : "wire");
        json.String("language", options.Language == GesSchemaLanguage.CSharp ? "csharp" : "swift");
        json.String("split", options.Split == GesSchemaSplit.Schema ? "schema" : "file");
        json.String("namespace", options.Namespace);
        json.String("access", options.Access == GesSchemaAccess.Public ? "public" : "internal");
        // Document reading includes the value conversions, so the manifest names what the files contain.
        json.Boolean("values", options.Values || options.Documents);
        json.Boolean("bindings", options.Bindings);
        if (options.Documents) json.Boolean("documents", true);
        json.Boolean("checkedExclusivity", options.CheckedExclusivity);
        json.BeginArray("roots");
        foreach (var root in roots)
        {
            var (hash, bindingHash) = hashes(root);
            json.BeginObject();
            json.String("name", root);
            json.String("hash", GesSchemaLayoutJson.Hex(hash));
            if (bindingHash is { } binding) json.String("bindingHash", GesSchemaLayoutJson.Hex(binding));
            json.EndObject();
        }

        json.EndArray();
        json.BeginArray("parameters");
        foreach (var parameter in parameters)
        {
            json.BeginObject();
            json.String("name", parameter.Name);
            json.String("type", parameter.Type);
            json.String("unit", parameter.Unit);
            json.String("value", parameter.Value);
            json.EndObject();
        }

        json.EndArray();
        json.Strings("files", files);
        json.EndObject();
        return json.ToString();
    }

    private static IReadOnlyList<string> Roots(GesSchemaSet set, string group, bool wire)
    {
        if (set is null) throw new ArgumentNullException(nameof(set));
        if (group is null) throw new ArgumentNullException(nameof(group));
        var roots = new List<string>();
        foreach (var schema in set.Schemas)
            if ((wire ? schema.WireGroup : schema.ModelGroup) == group) roots.Add(schema.Name);
        return roots;
    }
}
