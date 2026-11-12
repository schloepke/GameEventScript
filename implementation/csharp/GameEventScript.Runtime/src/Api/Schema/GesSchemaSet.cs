// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Text;
using GameEventScript.Runtime;
using GameEventScript.Syntax;
using Code = GameEventScript.Syntax.GesSchemaDiagnostic;

namespace GameEventScript.Api;

/// <summary>A named schema source text.</summary>
public readonly struct GesSchemaSource
{
    /// <summary>Creates a source.</summary>
    /// <param name="name">The name that errors report, such as a file name.</param>
    /// <param name="text">The source text.</param>
    public GesSchemaSource(string name, string text)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Text = text ?? throw new ArgumentNullException(nameof(text));
    }

    /// <summary>Gets the name that errors report.</summary>
    public string Name { get; }

    /// <summary>Gets the source text.</summary>
    public string Text { get; }
}

/// <summary>
/// Validated schema sources. A set is immutable and can be bound any number of times, also with different parameter
/// values within one session.
/// </summary>
public sealed class GesSchemaSet
{
    private const int MaximumNesting = 32;
    // A Flags field holds its members in one 64-bit mask.
    internal const int MaximumFlagCount = 64;

    private readonly List<GesSchemaSource> _sources;
    private readonly Dictionary<string, GesResolvedDeclaration> _declarations = new(StringComparer.Ordinal);
    private readonly List<GesResolvedDeclaration> _ordered = [];
    private readonly Dictionary<string, string> _tagSetSources = new(StringComparer.Ordinal);

    private GesSchemaSet(List<GesSchemaSource> sources) => _sources = sources;

    // The sources as given, which generated code embeds to convert to and from GES values.
    internal IReadOnlyList<GesSchemaSource> Sources => _sources;

    /// <summary>Reads and validates one or several sources. References resolve across all of them.</summary>
    /// <param name="sources">The sources.</param>
    /// <returns>The validated set.</returns>
    /// <exception cref="GesSchemaException">A source breaks a rule; the first error is reported.</exception>
    public static GesSchemaSet Parse(params GesSchemaSource[] sources)
    {
        if (sources is null) throw new ArgumentNullException(nameof(sources));
        var set = new GesSchemaSet(new List<GesSchemaSource>(sources));
        set.Load();
        return set;
    }

    /// <summary>Reads and validates one source named <c>schema.gess</c>.</summary>
    /// <param name="text">The source text.</param>
    /// <returns>The validated set.</returns>
    /// <exception cref="GesSchemaException">The source breaks a rule; the first error is reported.</exception>
    public static GesSchemaSet Parse(string text) => Parse(new GesSchemaSource("schema.gess", text));

    /// <summary>Gets the schemas, messages and message sets, in source order.</summary>
    public GesReadOnlyArray<GesSchema> Schemas { get; private set; }

    /// <summary>Gets a declaration by name.</summary>
    /// <param name="name">The name of a schema, message or message set.</param>
    /// <returns>Its description.</returns>
    /// <exception cref="GesSchemaException">The name is not declared.</exception>
    public GesSchema Schema(string name) => Declaration(name).Public;

    /// <summary>
    /// Checks a GES document against the schema of its header: a type name, or for a file name the one schema of the source
    /// with that name that no other schema of it refers to. <see cref="GesSchema.CheckDocument"/> ignores the header.
    /// </summary>
    /// <param name="text">The document text.</param>
    /// <param name="parameters">Parameter values; a bound whose parameter has no value is not checked.</param>
    /// <exception cref="GesSchemaException">The document is unreadable or breaks its schema; the code is a <c>document.*</c>
    /// code with its position, or a <c>schema.*</c> code when no schema is determined.</exception>
    public void CheckDocument(string text, GesSchemaParameters? parameters = null)
    {
        var syntax = ReadDocument(text);
        var schema = syntax.Schema == GesDocumentSchemaSyntax.Type ? Declaration(syntax.SchemaName)
            : syntax.Schema == GesDocumentSchemaSyntax.File ? RootSchema(syntax.SchemaName)
            : throw new GesSchemaException(Code.UnknownName, "", 0, 0, "The document names no schema.");
        Check(text, syntax, schema, parameters);
    }

    internal static GesDocumentSyntax ReadDocument(string text)
    {
        if (text is null) throw new ArgumentNullException(nameof(text));
        try
        {
            // Reads the values too, so that a form that constructs nothing is an error here as well.
            GesDocumentReading.Read(text, out _);
        }
        catch (GesDocumentException exception)
        {
            throw new GesSchemaException(exception.Code, "", exception.Line, exception.Column, GesDocumentDiagnostic.Explain(exception.Code, exception.LimitName, exception.Limit ?? 0));
        }

        return GesDocumentReader.Read(text);
    }

    internal static void Check(string text, GesDocumentSyntax syntax, GesResolvedDeclaration schema, GesSchemaParameters? parameters)
    {
        if (schema.Kind != GesSchemaKind.Schema) throw new GesSchemaException(Code.UnknownName, "", 0, 0, $"'{schema.Name}' is no schema.");
        if (schema.Abstract) throw new GesSchemaException(Code.InvalidAbstract, "", 0, 0, $"'{schema.Name}' is abstract and describes no document.");
        GesSchemaDocumentCheck.Check(text, syntax.Root!, schema, parameters);
    }

    // The one schema of a source that no other schema of it refers to.
    private GesResolvedDeclaration RootSchema(string source)
    {
        var referenced = new HashSet<GesResolvedDeclaration>();
        foreach (var declaration in _ordered)
        {
            if (declaration.Scope.SourceName != source || declaration.Kind != GesSchemaKind.Schema) continue;
            foreach (var field in declaration.Fields)
                foreach (var type in field.Alternatives) CollectReferences(type, referenced);
        }

        GesResolvedDeclaration? root = null;
        foreach (var declaration in _ordered)
        {
            if (declaration.Scope.SourceName != source || declaration.Kind != GesSchemaKind.Schema || declaration.Abstract || referenced.Contains(declaration)) continue;
            if (root is not null) throw new GesSchemaException(Code.UnknownName, "", 0, 0, $"The schema source '{source}' has more than one root schema.");
            root = declaration;
        }

        return root ?? throw new GesSchemaException(Code.UnknownName, "", 0, 0, $"The schema source '{source}' has no root schema.");
    }

    private static void CollectReferences(GesResolvedType type, HashSet<GesResolvedDeclaration> referenced)
    {
        if (type.Reference is { } reference) referenced.Add(reference);
        if (type.Element is { } element)
            foreach (var alternative in element) CollectReferences(alternative, referenced);
    }

    internal GesResolvedDeclaration Declaration(string root)
    {
        if (root is null) throw new ArgumentNullException(nameof(root));
        return _declarations.TryGetValue(root, out var declaration) ? declaration
            : throw new GesSchemaException(Code.UnknownName, "", 0, 0, $"'{root}' is not declared.");
    }

    // The name of the source that declares a schema, message, message set or tag set, which places its generated code in
    // split output.
    internal string SourceName(string name)
        => _declarations.TryGetValue(name, out var declaration) ? declaration.Scope.SourceName : _tagSetSources[name];

    internal GesResolvedDeclaration? FindSchema(string name)
        => _declarations.TryGetValue(name, out var declaration) && declaration.Kind == GesSchemaKind.Schema ? declaration : null;

    private void Load()
    {
        var files = new List<GesFileScope>();
        foreach (var source in _sources)
        {
            var syntax = GesSchemaSourceReader.Read(source.Text);
            if (syntax.Error is { } error) throw new GesSchemaException(error.Code, source.Name, error.Line, error.Column, error.Message);
            files.Add(new GesFileScope(source, syntax));
        }

        var tagSets = new Dictionary<string, GesSchemaDeclarationSyntax>(StringComparer.Ordinal);
        foreach (var file in files)
        {
            file.DeclareNames(_declarations, _ordered, tagSets);
            foreach (var name in tagSets.Keys) _tagSetSources.TryAdd(name, file.SourceName);
        }
        foreach (var file in files) file.Resolve(_declarations);
        var composed = new HashSet<GesResolvedDeclaration>();
        foreach (var declaration in _ordered)
            if (declaration.Kind == GesSchemaKind.Schema) Compose(declaration, [], composed);
        var visited = new Dictionary<GesResolvedDeclaration, int>();
        foreach (var declaration in _ordered) CheckNesting(declaration, [], visited);
        foreach (var declaration in _ordered)
            if (declaration.Syntax.WireGroup is not null) GesSchemaBinder.CheckEncodable(declaration);
        var schemas = new List<GesSchema>();
        foreach (var declaration in _ordered)
        {
            declaration.Public = new GesSchema(declaration);
            schemas.Add(declaration.Public);
        }

        foreach (var schema in schemas) schema.Complete();
        Schemas = new GesReadOnlyArray<GesSchema>(schemas);
    }

    // Composes the schemas a schema takes before it, so that it copies their complete fields; `with` must not form a cycle.
    private void Compose(GesResolvedDeclaration declaration, List<GesResolvedDeclaration> path, HashSet<GesResolvedDeclaration> composed)
    {
        if (composed.Contains(declaration)) return;
        path.Add(declaration);
        foreach (var with in declaration.Syntax.Withs)
        {
            if (!_declarations.TryGetValue(with.Name, out var source) || source.Kind != GesSchemaKind.Schema)
                throw declaration.Scope.Error(Code.UnknownName, with.Offset, $"Schema '{with.Name}' is not declared.");
            if (path.Contains(source)) throw declaration.Scope.Error(Code.Cycle, with.Offset, $"'{declaration.Name}' takes the fields of '{with.Name}', which takes its own.");
            Compose(source, path, composed);
        }

        path.RemoveAt(path.Count - 1);
        declaration.Scope.Compose(declaration, _declarations);
        composed.Add(declaration);
    }

    // Reference cycles and too deep nesting. A declaration already finished at this depth or deeper holds no error, so
    // the check stays polynomial however often schemas refer to each other.
    private static void CheckNesting(GesResolvedDeclaration declaration, List<GesResolvedDeclaration> path, Dictionary<GesResolvedDeclaration, int> visited)
    {
        if (declaration.Kind != GesSchemaKind.Schema) return;
        if (path.Contains(declaration)) throw declaration.Scope.Error(Code.Cycle, declaration.Offset, $"Schema '{declaration.Name}' refers to itself.");
        if (path.Count >= MaximumNesting) throw declaration.Scope.Error(Code.Cycle, declaration.Offset, $"Schema '{declaration.Name}' is nested more than {MaximumNesting} levels deep.");
        if (visited.TryGetValue(declaration, out var depth) && depth >= path.Count) return;
        visited[declaration] = path.Count;
        path.Add(declaration);
        foreach (var field in declaration.Fields)
            foreach (var type in field.Alternatives) CheckNesting(type, declaration, path, visited);
        path.RemoveAt(path.Count - 1);
    }

    private static void CheckNesting(GesResolvedType type, GesResolvedDeclaration holder, List<GesResolvedDeclaration> path, Dictionary<GesResolvedDeclaration, int> visited)
    {
        if (type.Reference is { } reference) CheckNesting(reference, path, visited);
        // A List with depth in the schema it holds is a limited recursion; where `with` copies it into another schema, it is
        // an ordinary List of the declaring schema.
        if (type.Recursion == holder) return;
        if (type.Element is { } element)
            foreach (var alternative in element) CheckNesting(alternative, holder, path, visited);
    }
}

/// <summary>A bound, step or delta range after constants are replaced: a number or a parameter.</summary>
internal sealed class GesResolvedValue
{
    internal GesSchemaDecimal Value { get; init; }

    internal string Parameter { get; init; } = "";

    internal int Offset { get; init; }

    internal bool IsParameter => Parameter.Length > 0;
}

/// <summary>A constant of a schema: its kind, Number, Percentage or Quantity, its unit and its exact value.</summary>
internal sealed class GesResolvedLet
{
    internal string Name { get; init; } = "";

    internal GesSchemaTypeKind Kind { get; init; }

    internal GesQuantityUnit Unit { get; init; }

    internal GesSchemaDecimal Value { get; init; }

    // A whole unitless value within Int64; any other is a binary64 in base units.
    internal bool Integer { get; set; }

    internal int Offset { get; init; }
}

internal sealed class GesResolvedConstraints
{
    internal bool Integer { get; init; }

    internal bool Wrapping { get; init; }

    internal GesResolvedValue? Lower { get; init; }

    internal GesResolvedValue? Upper { get; init; }

    internal GesResolvedValue? Step { get; init; }

    internal GesResolvedValue? Delta { get; init; }

    internal bool IsEmpty => !Integer && Lower is null && Step is null && Delta is null;
}

internal sealed class GesResolvedAxis
{
    internal char Axis { get; init; }

    internal GesResolvedConstraints Constraints { get; init; } = new();
}

internal sealed class GesResolvedType
{
    internal GesSchemaTypeKind Kind { get; init; }

    internal GesQuantityUnit Unit { get; init; }

    internal string[]? Tags { get; init; }

    internal long? Max { get; init; }

    internal GesResolvedType[]? Element { get; init; }

    // The `depth` of a List whose element is the schema that declares it: how many records of it nest; 0 without one.
    internal int Depth { get; init; }

    // The schema a List with depth holds itself through: its element, alone or as the one schema alternative of a union.
    internal GesResolvedDeclaration? Recursion => Depth > 0 ? Array.Find(Element!, alternative => alternative.Kind == GesSchemaTypeKind.Reference)?.Reference : null;

    internal GesResolvedConstraints Constraints { get; init; } = new();

    internal GesResolvedAxis[]? Axes { get; init; }

    internal GesResolvedDeclaration? Reference { get; set; }

    internal string ReferenceName { get; init; } = "";

    // The named tag set a Tag takes its members from, or empty.
    internal string TagSet { get; init; } = "";

    // The label of a union alternative, or empty.
    internal string Label { get; set; } = "";

    internal int Offset { get; init; }
}

internal sealed class GesResolvedField
{
    internal string Name { get; init; } = "";

    internal bool Unlabeled { get; init; }

    internal GesResolvedType[] Alternatives { get; init; } = [];

    internal GesSchemaFieldMarker Marker { get; init; }

    internal GesResolvedDefault? Default { get; set; }

    internal int Offset { get; init; }

    // The file that declares the field, whose constants, parameters and positions it uses, also where `with` takes it.
    internal GesFileScope Scope { get; init; } = null!;
}

/// <summary>The default of a field: an exact number in base units, a Boolean, a tag, or the name of a sibling field.</summary>
internal sealed class GesResolvedDefault
{
    internal GesSchemaDefaultKind Kind { get; init; }

    internal GesSchemaDecimal Value { get; init; }

    internal bool Boolean { get; init; }

    // The tag without '#', or the sibling field.
    internal string Name { get; init; } = "";

    internal int Offset { get; init; }
}

internal sealed class GesResolvedDeclaration
{
    internal GesSchemaKind Kind { get; init; }

    internal string Name { get; init; } = "";

    internal int Offset { get; init; }

    internal GesFileScope Scope { get; init; } = null!;

    internal GesSchemaDeclarationSyntax Syntax { get; init; } = null!;

    internal GesResolvedField[] Fields { get; set; } = [];

    internal GesResolvedLet[] Lets { get; set; } = [];

    // The schemas it takes with `with`, in declared order, each with its `with`.
    internal (GesResolvedDeclaration Schema, GesSchemaWithSyntax With)[] Takes { get; set; } = [];

    internal bool Abstract => Syntax.Abstract;

    internal string[] Tags { get; init; } = [];

    internal GesResolvedDeclaration[] Members { get; set; } = [];

    // The fields written `computed by`, own and taken, in the order their expressions may read each other.
    internal GesResolvedComputed[] Computed { get; set; } = [];

    // The public description, which exists once all declarations are resolved.
    internal GesSchema Public { get; set; } = null!;
}

/// <summary>One source: its constants and parameters, which only its own declarations see.</summary>
internal sealed partial class GesFileScope
{
    private readonly GesSchemaSource _source;
    private readonly GesSchemaSourceSyntax _syntax;
    private readonly Dictionary<string, GesSchemaValueSyntax> _constants = new(StringComparer.Ordinal);
    private readonly List<GesResolvedDeclaration> _own = [];
    private Dictionary<string, GesSchemaDeclarationSyntax> _tagSets = new(StringComparer.Ordinal);

    // The lets of the schema being resolved, which its fields can name.
    private readonly Dictionary<string, GesResolvedLet> _lets = new(StringComparer.Ordinal);

    internal GesFileScope(GesSchemaSource source, GesSchemaSourceSyntax syntax)
    {
        _source = source;
        _syntax = syntax;
    }

    internal Dictionary<string, GesSchemaTypeSyntax> Parameters { get; } = new(StringComparer.Ordinal);

    internal string SourceName => _source.Name;

    internal GesSchemaException Error(string code, int offset, string message)
    {
        var (line, column) = GesDocumentSyntax.Location(_syntax.Text, offset);
        return new GesSchemaException(code, _source.Name, line, column, message);
    }

    internal void DeclareNames(Dictionary<string, GesResolvedDeclaration> all, List<GesResolvedDeclaration> ordered, Dictionary<string, GesSchemaDeclarationSyntax> tagSets)
    {
        _tagSets = tagSets;
        foreach (var declaration in _syntax.Declarations)
        {
            switch (declaration.Kind)
            {
                case GesSchemaDeclarationKind.TagSet:
                {
                    if (GesSchemaTypeNames.IsBuiltIn(declaration.Name))
                        throw Error(Code.DuplicateName, declaration.Offset, $"'{declaration.Name}' is a built-in type.");
                    if (all.ContainsKey(declaration.Name) || !tagSets.TryAdd(declaration.Name, declaration))
                        throw Error(Code.DuplicateName, declaration.Offset, $"'{declaration.Name}' is declared twice.");
                    var members = new HashSet<string>(StringComparer.Ordinal);
                    foreach (var tag in declaration.Tags)
                        if (!members.Add(tag)) throw Error(Code.DuplicateName, declaration.Offset, $"Tag '#{tag}' is listed twice.");
                    break;
                }
                case GesSchemaDeclarationKind.Constant:
                    if (!_constants.TryAdd(declaration.Name, declaration.Value!)) throw Error(Code.DuplicateName, declaration.Offset, $"Constant '{declaration.Name}' is declared twice.");
                    break;
                case GesSchemaDeclarationKind.Parameter:
                    if (!Parameters.TryAdd(declaration.Name, declaration.Type!)) throw Error(Code.DuplicateName, declaration.Offset, $"Parameter '{declaration.Name}' is declared twice.");
                    break;
                default:
                    var resolved = new GesResolvedDeclaration
                    {
                        Kind = declaration.Kind switch
                        {
                            GesSchemaDeclarationKind.Schema => GesSchemaKind.Schema,
                            GesSchemaDeclarationKind.Message => GesSchemaKind.Message,
                            _ => GesSchemaKind.Messages
                        },
                        Name = declaration.Name,
                        Offset = declaration.Offset,
                        Scope = this,
                        Syntax = declaration,
                        Tags = declaration.Tags
                    };
                    if (GesSchemaTypeNames.IsBuiltIn(declaration.Name))
                        throw Error(Code.DuplicateName, declaration.Offset, $"'{declaration.Name}' is a built-in type.");
                    if (tagSets.ContainsKey(declaration.Name) || !all.TryAdd(declaration.Name, resolved))
                        throw Error(Code.DuplicateName, declaration.Offset, $"'{declaration.Name}' is declared twice.");
                    ordered.Add(resolved);
                    _own.Add(resolved);
                    break;
            }
        }
    }

    internal void Resolve(Dictionary<string, GesResolvedDeclaration> all)
    {
        foreach (var declaration in _own)
        {
            var syntax = declaration.Syntax;
            if (declaration.Kind == GesSchemaKind.Messages)
            {
                var seen = new HashSet<string>(StringComparer.Ordinal);
                var members = new GesResolvedDeclaration[syntax.Members.Length];
                for (var index = 0; index < members.Length; index++)
                {
                    var name = syntax.Members[index];
                    if (!seen.Add(name)) throw Error(Code.DuplicateName, syntax.MemberOffsets[index], $"Message '{name}' is listed twice.");
                    if (!all.TryGetValue(name, out var member) || member.Kind != GesSchemaKind.Message) throw Error(Code.UnknownName, syntax.MemberOffsets[index], $"Message '{name}' is not declared.");
                    members[index] = member;
                }

                declaration.Members = members;
                continue;
            }

            var names = new HashSet<string>(StringComparer.Ordinal);
            var tags = new HashSet<string>(StringComparer.Ordinal);
            foreach (var tag in syntax.Tags)
                if (!tags.Add(tag)) throw Error(Code.DuplicateName, syntax.Offset, $"Tag '#{tag}' is listed twice.");
            declaration.Lets = ResolveLets(syntax, names);
            var fields = new GesResolvedField[syntax.Fields.Length];
            var recursive = false;
            for (var index = 0; index < fields.Length; index++)
            {
                var field = syntax.Fields[index];
                if (!names.Add(field.Name)) throw Error(Code.DuplicateName, field.Offset, $"'{field.Name}' is declared twice in '{declaration.Name}'.");
                fields[index] = ResolveField(field, index, declaration.Kind, all);
                CheckDepth(field.Alternatives, fields[index].Alternatives, declaration, top: true);
                if (fields[index].Alternatives[0].Depth == 0) continue;
                if (recursive) throw Error(Code.InvalidConstraint, field.Alternatives[0].DepthOffset, "A schema holds at most one List with depth.");
                recursive = true;
            }

            declaration.Fields = fields;
        }
    }

    // Copies the fields that `with` takes in place, after every schema it takes is composed, then resolves the defaults,
    // which may name a taken field. The taken fields keep their file and their defaults, unless `all optional` drops them.
    internal void Compose(GesResolvedDeclaration declaration, Dictionary<string, GesResolvedDeclaration> all)
    {
        var syntax = declaration.Syntax;
        var own = declaration.Fields;
        var merged = new List<GesResolvedField>();
        var origins = new List<int>();
        var takes = new List<(GesResolvedDeclaration, GesSchemaWithSyntax)>();
        for (var index = 0; index <= own.Length; index++)
        {
            foreach (var with in syntax.Withs)
            {
                if (with.Position != index) continue;
                var source = all[with.Name];
                takes.Add((source, with));
                for (var excluded = 0; excluded < with.Without.Length; excluded++)
                    if (!Array.Exists(source.Fields, field => field.Name == with.Without[excluded]) && !Array.Exists(source.Computed, field => field.Name == with.Without[excluded]))
                        throw Error(Code.UnknownName, with.WithoutOffsets[excluded], $"'{with.Without[excluded]}' is no field of '{source.Name}'.");
                foreach (var field in source.Fields)
                {
                    if (Array.IndexOf(with.Without, field.Name) >= 0) continue;
                    var marker = field.Marker;
                    if (with.AllOptional)
                        marker = field.Alternatives.Length > 1 && Array.Exists(field.Alternatives, alternative => alternative.Kind == GesSchemaTypeKind.Nothing) ? GesSchemaFieldMarker.None : GesSchemaFieldMarker.Optional;
                    merged.Add(new GesResolvedField
                    {
                        Name = field.Name,
                        Unlabeled = field.Unlabeled,
                        Alternatives = field.Alternatives,
                        Marker = marker,
                        Default = with.AllOptional ? null : field.Default,
                        Offset = field.Offset,
                        Scope = field.Scope
                    });
                    origins.Add(with.Offset);
                }
            }

            if (index == own.Length) break;
            merged.Add(own[index]);
            origins.Add(-1);
        }

        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var let in declaration.Lets) names.Add(let.Name);
        for (var index = 0; index < merged.Count; index++)
        {
            var field = merged[index];
            var offset = origins[index] >= 0 ? origins[index] : field.Offset;
            if (!names.Add(field.Name)) throw Error(Code.DuplicateName, offset, $"'{field.Name}' is declared twice in '{declaration.Name}'.");
            if (field.Marker == GesSchemaFieldMarker.Key && index != 0)
                throw Error(Code.InvalidKey, offset, "A key is the first field of a schema, an integer or a Tag with a set, without delta within or a union.");
        }

        for (var index = 0; index < merged.Count; index++)
            if (origins[index] >= 0 && merged[index].Default is { Kind: GesSchemaDefaultKind.Field } fallback && !merged.Exists(field => field.Name == fallback.Name))
                throw Error(Code.InvalidDefault, origins[index], $"'{merged[index].Name}' defaults to '{fallback.Name}', which 'without' leaves out.");
        declaration.Fields = merged.ToArray();
        declaration.Takes = takes.ToArray();
        declaration.Computed = ResolveComputed(declaration, ComputedEntries(declaration, all), names);
        _lets.Clear();
        foreach (var let in declaration.Lets) _lets[let.Name] = let;
        ResolveDefaults(declaration);
    }

    // The computed fields in written order: own ones, and those of a schema taken by `with` where the `with` stands, without
    // the names it leaves out; `all optional` makes taken numbers optional.
    private List<GesResolvedComputed> ComputedEntries(GesResolvedDeclaration declaration, Dictionary<string, GesResolvedDeclaration> all)
    {
        var entries = new List<GesResolvedComputed>();
        var syntax = declaration.Syntax;
        var withIndex = 0;
        var ownIndex = 0;
        while (withIndex < syntax.Withs.Length || ownIndex < syntax.Computed.Length)
        {
            if (ownIndex == syntax.Computed.Length || withIndex < syntax.Withs.Length && syntax.Withs[withIndex].Sequence < syntax.Computed[ownIndex].Sequence)
            {
                var with = syntax.Withs[withIndex++];
                foreach (var taken in all[with.Name].Computed)
                {
                    if (Array.IndexOf(with.Without, taken.Name) >= 0) continue;
                    entries.Add(new GesResolvedComputed
                    {
                        Name = taken.Name,
                        Kind = taken.Kind,
                        Unit = taken.Unit,
                        Optional = taken.Optional || with.AllOptional && taken.Kind != GesSchemaTypeKind.Boolean,
                        Offset = taken.Offset,
                        Scope = taken.Scope,
                        Syntax = taken.Syntax,
                        Lets = taken.Lets
                    });
                }

                continue;
            }

            var field = syntax.Computed[ownIndex++];
            entries.Add(new GesResolvedComputed
            {
                Name = field.Name,
                Kind = field.Alternatives[0].Kind,
                Unit = field.Alternatives[0].Unit,
                Optional = field.Marker == GesSchemaFieldMarker.Optional,
                Offset = field.Offset,
                Scope = this,
                Syntax = field,
                Lets = declaration.Lets
            });
        }

        return entries;
    }

    // A default is a number, a Boolean or a tag of the field's single type, or a sibling field of the same type whose value it
    // takes; chains resolve in dependency order and must not form a cycle.
    private void ResolveDefaults(GesResolvedDeclaration declaration)
    {
        var syntax = declaration.Syntax.Fields;
        for (var index = 0; index < syntax.Length; index++)
        {
            if (syntax[index].Default is not { } written) continue;
            var field = Array.Find(declaration.Fields, candidate => candidate.Name == syntax[index].Name && candidate.Scope == this && candidate.Offset == syntax[index].Offset)!;
            if (field.Marker == GesSchemaFieldMarker.Key) throw Error(Code.InvalidDefault, written.Offset, "A key takes no default.");
            var name = written.Value?.Name ?? "";
            if (name.Length > 0 && Array.Exists(declaration.Computed, candidate => candidate.Name == name))
                throw Error(Code.InvalidDefault, written.Offset, $"'{name}' is computed; a default names a stored field.");
            var sibling = name.Length > 0 ? Array.Find(declaration.Fields, candidate => candidate.Name == name) : null;
            if (sibling is not null)
            {
                var type = GesSchemaCanonical.DefaultType(field.Alternatives);
                if (type is null) throw Error(Code.InvalidDefault, written.Offset, $"The type of '{field.Name}' belongs to it alone, as a union, Flags or a closed Tag written in place does; a tag set can be shared.");
                if (type != GesSchemaCanonical.DefaultType(sibling.Alternatives)) throw Error(Code.InvalidDefault, written.Offset, $"'{name}' has another type than '{field.Name}'.");
                if ((sibling.Marker == GesSchemaFieldMarker.Optional) != (field.Marker == GesSchemaFieldMarker.Optional))
                    throw Error(Code.InvalidDefault, written.Offset, "A field and the field it defaults to are both optional or both not.");
                field.Default = new GesResolvedDefault { Kind = GesSchemaDefaultKind.Field, Name = name, Offset = written.Offset };
                continue;
            }

            if (name.Length > 0 && !_lets.ContainsKey(name)) throw Error(Code.UnknownName, written.Value!.Offset, $"'{name}' is neither a field nor a let of '{declaration.Name}'.");
            if (field.Marker == GesSchemaFieldMarker.Optional) throw Error(Code.InvalidDefault, written.Offset, "An optional field takes its default only from another optional field.");
            if (field.Alternatives.Length != 1) throw Error(Code.InvalidDefault, written.Offset, "A union takes its default only from another field.");
            field.Default = ResolveDefault(field.Alternatives[0], written);
        }

        foreach (var field in declaration.Fields)
        {
            var seen = new HashSet<GesResolvedField>();
            for (var current = field; current.Default is { Kind: GesSchemaDefaultKind.Field } fallback;)
            {
                if (!seen.Add(current)) throw Error(Code.Cycle, field.Default!.Offset, $"The defaults of '{field.Name}' form a cycle.");
                current = Array.Find(declaration.Fields, candidate => candidate.Name == fallback.Name)!;
            }
        }
    }

    private GesResolvedDefault ResolveDefault(GesResolvedType type, GesSchemaDefaultSyntax written)
    {
        switch (type.Kind)
        {
            case GesSchemaTypeKind.Boolean when written.Boolean is { } boolean:
                return new GesResolvedDefault { Kind = GesSchemaDefaultKind.Boolean, Boolean = boolean, Offset = written.Offset };
            case GesSchemaTypeKind.Tag when written.Tag is { } tag:
                if (type.Tags is { } tags && Array.IndexOf(tags, tag) < 0) throw Error(Code.InvalidDefault, written.Offset, $"'#{tag}' is no tag of the field.");
                if (type.Max is { } max && System.Text.Encoding.UTF8.GetByteCount(tag) > max) throw Error(Code.InvalidDefault, written.Offset, $"'#{tag}' is longer than {max} bytes.");
                return new GesResolvedDefault { Kind = GesSchemaDefaultKind.Tag, Name = tag, Offset = written.Offset };
            case GesSchemaTypeKind.Number or GesSchemaTypeKind.Percentage or GesSchemaTypeKind.Quantity when written.Value is { } value:
                var resolved = ResolveValue(value, type.Unit, type.Kind == GesSchemaTypeKind.Percentage, bound: false)!;
                GesSchemaBinder.CheckDefault(this, type.Constraints, resolved.Value, written.Offset, null);
                return new GesResolvedDefault { Kind = GesSchemaDefaultKind.Number, Value = resolved.Value, Offset = written.Offset };
            default:
                throw Error(Code.InvalidDefault, written.Offset, $"A {type.Kind} field takes no such default; a sibling field of its type can be one.");
        }
    }

    // The element of a recursive List: the declaring schema alone, or a union of it and scalars, which hold no schema and no
    // elements, so that the recursion has one path and every alternative but one keeps its size at every level.
    private static bool SelfElement(GesResolvedType[] element, GesResolvedDeclaration owner)
    {
        var self = 0;
        foreach (var alternative in element)
        {
            if (alternative.Kind == GesSchemaTypeKind.Reference && alternative.Reference == owner) self++;
            else if (alternative.Kind is GesSchemaTypeKind.Reference or GesSchemaTypeKind.List or GesSchemaTypeKind.Dice or GesSchemaTypeKind.Poly) return false;
        }

        return self == 1;
    }

    // `depth` stands only on a field's List of the schema that declares the field, which may then hold itself that many
    // levels deep; every other cycle stays an error.
    private void CheckDepth(GesSchemaTypeSyntax[] written, GesResolvedType[] resolved, GesResolvedDeclaration owner, bool top)
    {
        for (var index = 0; index < written.Length; index++)
        {
            var type = resolved[index];
            if (type.Depth > 0 && !(top && written.Length == 1 && type.Element is { } element && SelfElement(element, owner)))
                throw Error(Code.InvalidConstraint, written[index].DepthOffset, "depth stands on a field's List whose element is the schema that declares it, alone or as the one schema of a union of scalars.");
            if (type.Depth == 1) throw Error(Code.InvalidConstraint, written[index].DepthOffset, "A depth of 1 leaves the List always empty; it counts the records that nest, at least 2.");
            if (written[index].Element is { } elements) CheckDepth(elements, type.Element!, owner, top: false);
        }
    }

    // A let is a literal or a file constant. Its name differs from the fields and other lets of its schema and from the
    // parameters of its file, since a bound names either.
    private GesResolvedLet[] ResolveLets(GesSchemaDeclarationSyntax syntax, HashSet<string> names)
    {
        _lets.Clear();
        var lets = new GesResolvedLet[syntax.Lets.Length];
        for (var index = 0; index < lets.Length; index++)
        {
            var let = syntax.Lets[index];
            if (!names.Add(let.Name)) throw Error(Code.DuplicateName, let.Offset, $"'{let.Name}' is declared twice in '{syntax.Name}'.");
            if (Parameters.ContainsKey(let.Name)) throw Error(Code.DuplicateName, let.Offset, $"'{let.Name}' is a parameter of this file.");
            var literal = let.Value;
            if (literal.Constant.Length > 0 && !_constants.TryGetValue(literal.Constant, out literal!))
                throw Error(Code.UnknownName, let.Value.Offset, $"Constant '${let.Value.Constant}' is not declared in this file.");
            var kind = literal.IsPercent ? GesSchemaTypeKind.Percentage : literal.Unit != GesQuantityUnit.UnitNone ? GesSchemaTypeKind.Quantity : GesSchemaTypeKind.Number;
            var resolved = new GesResolvedLet { Name = let.Name, Kind = kind, Unit = literal.Unit, Value = literal.Value, Offset = let.Offset };
            resolved.Integer = GesSchemaBinder.CheckLet(this, resolved);
            lets[index] = resolved;
            _lets[let.Name] = resolved;
        }

        return lets;
    }

    private GesResolvedField ResolveField(GesSchemaFieldSyntax field, int index, GesSchemaKind owner, Dictionary<string, GesResolvedDeclaration> all)
    {
        var alternatives = ResolveUnion(field.Alternatives, all);
        if (field.Marker == GesSchemaFieldMarker.Key)
        {
            var first = alternatives[0];
            var keyable = first.Constraints.Integer || (first.Kind == GesSchemaTypeKind.Tag && first.Tags is not null);
            if (owner != GesSchemaKind.Schema || alternatives.Length != 1 || !keyable || first.Constraints.Delta is not null)
                throw Error(Code.InvalidKey, field.Offset, "A key is the first field of a schema, an integer or a Tag with a set, without delta within or a union.");
        }

        if (field.Marker == GesSchemaFieldMarker.Optional && alternatives.Length > 1 && Array.Exists(alternatives, alternative => alternative.Kind == GesSchemaTypeKind.Nothing))
            throw Error(Code.InvalidUnion, field.Offset, "A union with Nothing already holds an absent value; it is not optional as well.");
        return new GesResolvedField { Name = field.Name, Unlabeled = field.Unlabeled, Alternatives = alternatives, Marker = field.Marker, Offset = field.Offset, Scope = this };
    }

    private GesResolvedType[] ResolveUnion(GesSchemaTypeSyntax[] alternatives, Dictionary<string, GesResolvedDeclaration> all)
    {
        var result = new GesResolvedType[alternatives.Length];
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var labels = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < result.Length; index++)
        {
            var alternative = alternatives[index];
            result[index] = ResolveType(alternative, all);
            if (alternative.Label.Length > 0)
            {
                if (alternatives.Length == 1) throw Error(Code.InvalidUnion, alternative.LabelOffset, "A label names an alternative of a union.");
                if (result[index].Kind == GesSchemaTypeKind.Nothing) throw Error(Code.InvalidUnion, alternative.LabelOffset, "A :Nothing alternative holds no value to label.");
                result[index].Label = alternative.Label;
            }

            if (alternatives.Length == 1) continue;
            // The type name decides: a unit does not distinguish alternatives, and a named tag set is a Tag.
            var name = result[index].Kind == GesSchemaTypeKind.Reference ? ":" + alternative.Name : result[index].Kind.ToString();
            if (!seen.Add(name)) throw Error(Code.InvalidUnion, alternative.Offset, "A union repeats a type name; a unit does not distinguish alternatives.");
            if (alternative.Constraints.Delta is not null) throw Error(Code.InvalidUnion, alternative.Offset, "A union alternative has no delta within.");
            // Generated code names an alternative after its label or its type, so both must stay apart.
            var label = alternative.Label.Length > 0 ? char.ToUpperInvariant(alternative.Label[0]) + alternative.Label.Substring(1)
                : result[index].Kind == GesSchemaTypeKind.Reference ? alternative.Name : result[index].Kind.ToString();
            if (!labels.Add(label)) throw Error(Code.DuplicateName, alternative.Label.Length > 0 ? alternative.LabelOffset : alternative.Offset, $"Two alternatives are both named '{label}'.");
        }

        return result;
    }

    private GesResolvedType ResolveType(GesSchemaTypeSyntax type, Dictionary<string, GesResolvedDeclaration> all)
    {
        // A named tag set stands for a Tag with its members; only its name differs, which belongs to no hash.
        if (type.Kind == GesSchemaTypeKind.Reference && _tagSets.TryGetValue(type.Name, out var tagSet))
            return new GesResolvedType { Kind = GesSchemaTypeKind.Tag, Tags = tagSet.Tags, TagSet = tagSet.Name, Offset = type.Offset };
        GesResolvedDeclaration? reference = null;
        if (type.Kind == GesSchemaTypeKind.Reference && (!all.TryGetValue(type.Name, out reference) || reference.Kind != GesSchemaKind.Schema))
            throw Error(Code.UnknownName, type.Offset, $"Schema '{type.Name}' is not declared.");
        if (reference is { Abstract: true }) throw Error(Code.InvalidAbstract, type.Offset, $"'{type.Name}' is abstract; only 'with' takes its fields.");
        if (type.Tags is { } tags)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var tag in tags)
                if (!seen.Add(tag)) throw Error(Code.DuplicateName, type.Offset, $"Tag '#{tag}' is listed twice.");
            if (type.Kind == GesSchemaTypeKind.Flags && tags.Length > GesSchemaSet.MaximumFlagCount)
                throw Error(Code.TooWide, type.Offset, $"Flags have at most {GesSchemaSet.MaximumFlagCount} members.");
        }

        var max = type.MaxName.Length > 0 ? LetMax(type) : type.Max;
        if (max is <= 0) throw Error(Code.InvalidConstraint, type.Offset, "A maximum is positive.");
        if (type.Kind == GesSchemaTypeKind.Dice && (type.Constraints.Integer || type.Constraints.Wrapping || type.Constraints.Step is not null || type.Constraints.Delta is not null))
            throw Error(Code.InvalidConstraint, type.Offset, "A Dice has only a range.");
        GesResolvedAxis[]? axes = null;
        if (type.Axes is { } written)
        {
            axes = new GesResolvedAxis[written.Length];
            var seen = new HashSet<char>();
            for (var index = 0; index < axes.Length; index++)
            {
                if (!seen.Add(written[index].Axis)) throw Error(Code.DuplicateName, written[index].Offset, $"Axis '{written[index].Axis}' is declared twice.");
                axes[index] = new GesResolvedAxis { Axis = written[index].Axis, Constraints = ResolveConstraints(written[index].Constraints, type.Unit, false, written[index].Offset) };
            }

            Array.Sort(axes, (left, right) => left.Axis.CompareTo(right.Axis));
        }

        var constraints = ResolveConstraints(type.Constraints, type.Unit, type.Kind == GesSchemaTypeKind.Percentage, type.Offset);
        if (type.Kind == GesSchemaTypeKind.Dice) GesSchemaBinder.CheckDice(this, constraints, type.Offset, null);
        return new GesResolvedType
        {
            Kind = type.Kind,
            Unit = type.Unit,
            Tags = type.Tags,
            Max = max,
            Element = type.Element is { } element ? ResolveUnion(element, all) : null,
            Depth = type.Depth,
            Constraints = constraints,
            Axes = axes,
            Reference = reference,
            ReferenceName = type.Name,
            Offset = type.Offset
        };
    }

    // A maximum that names a let takes its whole unitless value, which has at most 12 digits like a written one.
    private long LetMax(GesSchemaTypeSyntax type)
    {
        if (!_lets.TryGetValue(type.MaxName, out var let)) throw Error(Code.UnknownName, type.MaxOffset, $"'{type.MaxName}' is no let of this schema.");
        if (let.Kind != GesSchemaTypeKind.Number) throw Error(Code.UnitMismatch, type.MaxOffset, $"Let '{type.MaxName}' has a unit; a maximum is a whole number.");
        if (!let.Integer || let.Value.Significand <= 0 || let.Value.Exponent + Digits(let.Value.Significand) > 12)
            throw Error(Code.InvalidConstraint, type.MaxOffset, $"Let '{type.MaxName}' is no positive whole number of at most 12 digits.");
        var value = let.Value.Significand;
        for (var exponent = 0; exponent < let.Value.Exponent; exponent++) value *= 10;
        return value;
    }

    private static int Digits(long value)
    {
        var digits = 1;
        while (value >= 10)
        {
            value /= 10;
            digits++;
        }

        return digits;
    }

    private GesResolvedConstraints ResolveConstraints(GesSchemaConstraintsSyntax constraints, GesQuantityUnit unit, bool percentage, int offset)
    {
        if (constraints.Integer && percentage) throw Error(Code.InvalidConstraint, offset, "A Percentage is not an integer.");
        if (constraints.Integer && constraints.Step is not null) throw Error(Code.InvalidConstraint, offset, "An integer field has no step.");
        if (constraints.Step is not null && constraints.Lower is null) throw Error(Code.InvalidConstraint, offset, "A step requires a range.");
        if (constraints.Delta is not null && constraints.Lower is null) throw Error(Code.InvalidConstraint, offset, "A delta range requires a range.");
        if (constraints.Delta is not null && !constraints.Integer && constraints.Step is null) throw Error(Code.InvalidConstraint, offset, "A delta range requires a step or integer.");
        var resolved = new GesResolvedConstraints
        {
            Integer = constraints.Integer,
            Wrapping = constraints.Wrapping,
            Lower = ResolveValue(constraints.Lower, unit, percentage, bound: true),
            Upper = ResolveValue(constraints.Upper, unit, percentage, bound: true),
            Step = ResolveValue(constraints.Step, unit, percentage, bound: false),
            Delta = ResolveValue(constraints.Delta, unit, percentage, bound: false)
        };
        GesSchemaBinder.CheckLiterals(this, resolved);
        return resolved;
    }

    // A name is a let of the schema and, only in a bound, a parameter of the file; a let is replaced by its value.
    private GesResolvedValue? ResolveValue(GesSchemaValueSyntax? value, GesQuantityUnit unit, bool percentage, bool bound)
    {
        if (value is null) return null;
        if (value.Name.Length > 0)
        {
            if (_lets.TryGetValue(value.Name, out var let))
            {
                if (let.Unit != unit || (let.Kind == GesSchemaTypeKind.Percentage) != percentage) throw Error(Code.UnitMismatch, value.Offset, $"Let '{value.Name}' has another kind or unit than its field.");
                return new GesResolvedValue { Value = let.Value, Offset = value.Offset };
            }

            if (!bound) throw Error(Code.UnknownName, value.Offset, $"'{value.Name}' is no let of this schema; a step and a delta range never name a parameter.");
            if (!Parameters.TryGetValue(value.Name, out var parameter)) throw Error(Code.UnknownName, value.Offset, $"'{value.Name}' is neither a let of this schema nor a parameter of this file.");
            if (parameter.Unit != unit || (parameter.Kind == GesSchemaTypeKind.Percentage) != percentage) throw Error(Code.UnitMismatch, value.Offset, $"Parameter '{value.Name}' has another kind or unit than its field.");
            return new GesResolvedValue { Parameter = value.Name, Offset = value.Offset };
        }

        var literal = value;
        if (value.Constant.Length > 0 && !_constants.TryGetValue(value.Constant, out literal!)) throw Error(Code.UnknownName, value.Offset, $"Constant '${value.Constant}' is not declared in this file.");
        if (literal.Unit != unit || literal.IsPercent != percentage) throw Error(Code.UnitMismatch, value.Offset, "A value has another unit than its field.");
        return new GesResolvedValue { Value = literal.Value, Offset = value.Offset };
    }
}

/// <summary>The canonical form of a declaration and its hash.</summary>
internal static class GesSchemaCanonical
{
    internal const ulong FnvOffset = 14695981039346656037;
    private const ulong FnvPrime = 1099511628211;

    internal static ulong Hash(string text)
    {
        var hash = FnvOffset;
        foreach (var value in Encoding.UTF8.GetBytes(text)) hash = (hash ^ value) * FnvPrime;
        return hash;
    }

    internal static string Hex(ulong hash) => hash.ToString("x16", System.Globalization.CultureInfo.InvariantCulture);

    internal static string Form(GesResolvedDeclaration root)
    {
        var declarations = new List<GesResolvedDeclaration>();
        var parameters = new SortedDictionary<string, GesSchemaTypeSyntax>(StringComparer.Ordinal);
        Reach(root, declarations, parameters);
        var builder = new StringBuilder("gess1\n");
        foreach (var parameter in parameters) builder.Append("parameter ").Append(parameter.Key).Append(' ').Append(ParameterKind(parameter.Value)).Append('\n');
        foreach (var declaration in declarations) Write(declaration, builder);
        return builder.ToString();
    }

    internal static void Reach(GesResolvedDeclaration declaration, List<GesResolvedDeclaration> declarations, SortedDictionary<string, GesSchemaTypeSyntax> parameters)
        => Reach(declaration, new HashSet<GesResolvedDeclaration>(), declarations, parameters);

    private static void Reach(GesResolvedDeclaration declaration, HashSet<GesResolvedDeclaration> seen, List<GesResolvedDeclaration> declarations, SortedDictionary<string, GesSchemaTypeSyntax> parameters)
    {
        if (!seen.Add(declaration)) return;
        declarations.Add(declaration);
        foreach (var field in declaration.Fields)
            foreach (var type in field.Alternatives) Reach(type, field.Scope, seen, declarations, parameters);
        foreach (var member in declaration.Members) Reach(member, seen, declarations, parameters);
    }

    private static void Reach(GesResolvedType type, GesFileScope scope, HashSet<GesResolvedDeclaration> seen, List<GesResolvedDeclaration> declarations, SortedDictionary<string, GesSchemaTypeSyntax> parameters)
    {
        ReachValues(type.Constraints, scope, parameters);
        if (type.Axes is { } axes)
            foreach (var axis in axes) ReachValues(axis.Constraints, scope, parameters);
        if (type.Element is { } element)
            foreach (var alternative in element) Reach(alternative, scope, seen, declarations, parameters);
        if (type.Reference is { } reference) Reach(reference, seen, declarations, parameters);
    }

    private static void ReachValues(GesResolvedConstraints constraints, GesFileScope scope, SortedDictionary<string, GesSchemaTypeSyntax> parameters)
    {
        foreach (var value in new[] { constraints.Lower, constraints.Upper })
            if (value is { IsParameter: true })
            {
                var declared = scope.Parameters[value.Parameter];
                if (parameters.TryGetValue(value.Parameter, out var other) && (other.Kind != declared.Kind || other.Unit != declared.Unit))
                    throw scope.Error(Code.DuplicateName, value.Offset, $"Parameter '{value.Parameter}' has another kind or unit in another file.");
                parameters[value.Parameter] = declared;
            }
    }

    internal static string ParameterKind(GesSchemaTypeSyntax type) => type.Kind switch
    {
        GesSchemaTypeKind.Percentage => "Percentage",
        GesSchemaTypeKind.Quantity => $"Quantity({type.Unit.ToTypeName()})",
        _ => "Number"
    };

    internal static string LetKind(GesResolvedLet let) => let.Kind switch
    {
        GesSchemaTypeKind.Percentage => "Percentage",
        GesSchemaTypeKind.Quantity => $"Quantity({let.Unit.ToTypeName()})",
        _ => "Number"
    };

    private static void Write(GesResolvedDeclaration declaration, StringBuilder builder)
    {
        switch (declaration.Kind)
        {
            case GesSchemaKind.Schema:
                builder.Append("schema ").Append(declaration.Name).Append('\n');
                foreach (var let in declaration.Lets) builder.Append("let ").Append(let.Name).Append(' ').Append(LetKind(let)).Append(' ').Append(let.Value.Canonical).Append('\n');
                foreach (var field in declaration.Fields) WriteField("field ", field, builder);
                foreach (var computed in declaration.Computed)
                {
                    builder.Append("computed ").Append(computed.Name).Append(' ').Append(computed.Kind switch
                    {
                        GesSchemaTypeKind.Quantity => "Quantity(" + computed.Unit.ToTypeName() + ")",
                        _ => computed.Kind.ToString()
                    });
                    if (computed.Optional) builder.Append(" optional");
                    builder.Append(' ');
                    GesComputedCanonical.Write(computed.Expression, builder);
                    builder.Append('\n');
                }

                break;
            case GesSchemaKind.Message:
                builder.Append("message ").Append(declaration.Name);
                if (declaration.Tags.Length > 0)
                {
                    builder.Append(" tags");
                    foreach (var tag in declaration.Tags) builder.Append(" #").Append(tag);
                }

                builder.Append('\n');
                foreach (var field in declaration.Fields) WriteField("argument ", field, builder);
                break;
            default:
                builder.Append("messages ").Append(declaration.Name).Append('\n');
                foreach (var member in declaration.Members) builder.Append("member ").Append(member.Name).Append('\n');
                break;
        }
    }

    private static void WriteField(string prefix, GesResolvedField field, StringBuilder builder)
    {
        builder.Append(prefix);
        if (field.Unlabeled) builder.Append("_ ");
        builder.Append(field.Name).Append(' ');
        WriteUnion(field.Alternatives, builder);
        if (field.Marker == GesSchemaFieldMarker.Optional) builder.Append(" optional");
        else if (field.Marker == GesSchemaFieldMarker.Key) builder.Append(" key");
        if (field.Default is { } fallback)
        {
            builder.Append(" default ");
            builder.Append(fallback.Kind switch
            {
                GesSchemaDefaultKind.Number => fallback.Value.Canonical,
                GesSchemaDefaultKind.Boolean => fallback.Boolean ? "true" : "false",
                GesSchemaDefaultKind.Tag => "#" + fallback.Name,
                _ => "field " + fallback.Name
            });
        }

        builder.Append('\n');
    }

    // The canonical type of a field that a sibling may default to, without delta ranges, which constrain no value, and with
    // the tag set a Tag takes its members from, since generated code names the type after it. Null for a union, Flags, a
    // closed Tag written in place and a List of one of them: generated code gives each such field a type of its own.
    internal static string? DefaultType(GesResolvedType[] alternatives)
    {
        var builder = new StringBuilder();
        return DefaultType(alternatives, builder) ? builder.ToString() : null;
    }

    private static bool DefaultType(GesResolvedType[] alternatives, StringBuilder builder)
    {
        if (alternatives.Length != 1) return false;
        var type = alternatives[0];
        switch (type.Kind)
        {
            case GesSchemaTypeKind.Flags:
            case GesSchemaTypeKind.Tag when type.Tags is not null && type.TagSet.Length == 0:
                return false;
            case GesSchemaTypeKind.Tag when type.TagSet.Length > 0:
                builder.Append(':').Append(type.TagSet);
                return true;
            case GesSchemaTypeKind.List when type.Element is { } element:
                builder.Append("List(");
                if (!DefaultType(element, builder)) return false;
                builder.Append(')');
                if (type.Max is { } max) builder.Append(" max ").Append(max);
                if (type.Depth > 0) builder.Append(" depth ").Append(type.Depth);
                return true;
            case GesSchemaTypeKind.Number or GesSchemaTypeKind.Percentage or GesSchemaTypeKind.Quantity or GesSchemaTypeKind.Dice:
                WriteType(new GesResolvedType { Kind = type.Kind, Unit = type.Unit, Max = type.Max, Constraints = WithoutDelta(type.Constraints) }, builder);
                return true;
            case GesSchemaTypeKind.Point or GesSchemaTypeKind.Vector or GesSchemaTypeKind.Box or GesSchemaTypeKind.Poly:
                var axes = type.Axes is { } written ? Array.ConvertAll(written, axis => new GesResolvedAxis { Axis = axis.Axis, Constraints = WithoutDelta(axis.Constraints) }) : null;
                WriteType(new GesResolvedType { Kind = type.Kind, Unit = type.Unit, Max = type.Max, Axes = axes }, builder);
                return true;
            default:
                WriteType(type, builder);
                return true;
        }
    }

    private static GesResolvedConstraints WithoutDelta(GesResolvedConstraints constraints)
        => new() { Integer = constraints.Integer, Wrapping = constraints.Wrapping, Lower = constraints.Lower, Upper = constraints.Upper, Step = constraints.Step };

    private static void WriteUnion(GesResolvedType[] alternatives, StringBuilder builder)
    {
        for (var index = 0; index < alternatives.Length; index++)
        {
            if (index > 0) builder.Append(" | ");
            WriteType(alternatives[index], builder);
        }
    }

    private static void WriteType(GesResolvedType type, StringBuilder builder)
    {
        switch (type.Kind)
        {
            case GesSchemaTypeKind.Quantity:
                builder.Append("Quantity(").Append(type.Unit.ToTypeName()).Append(')');
                WriteConstraints(type.Constraints, builder);
                break;
            case GesSchemaTypeKind.Number:
                builder.Append("Number");
                WriteConstraints(type.Constraints, builder);
                break;
            case GesSchemaTypeKind.Percentage:
                builder.Append("Percentage");
                WriteConstraints(type.Constraints, builder);
                break;
            case GesSchemaTypeKind.Point:
            case GesSchemaTypeKind.Vector:
            case GesSchemaTypeKind.Box:
            case GesSchemaTypeKind.Poly:
                builder.Append(type.Kind.ToString()).Append('(');
                builder.Append(type.Unit == GesQuantityUnit.UnitNone ? "none" : type.Unit.ToTypeName()).Append(')');
                if (type.Max is { } polyMax) builder.Append(" max ").Append(polyMax);
                if (type.Axes is { } axes)
                {
                    builder.Append(" {");
                    for (var index = 0; index < axes.Length; index++)
                    {
                        if (index > 0) builder.Append(" ;");
                        builder.Append(' ').Append(axes[index].Axis);
                        WriteConstraints(axes[index].Constraints, builder);
                    }

                    builder.Append(" }");
                }

                break;
            case GesSchemaTypeKind.Tag:
                builder.Append("Tag");
                if (type.Tags is { } tags)
                {
                    builder.Append('(');
                    for (var index = 0; index < tags.Length; index++)
                    {
                        if (index > 0) builder.Append(' ');
                        builder.Append('#').Append(tags[index]);
                    }

                    builder.Append(')');
                }

                if (type.Max is { } tagMax) builder.Append(" max ").Append(tagMax);

                break;
            case GesSchemaTypeKind.Flags:
                builder.Append("Flags(");
                for (var index = 0; index < type.Tags!.Length; index++)
                {
                    if (index > 0) builder.Append(' ');
                    builder.Append('#').Append(type.Tags[index]);
                }

                builder.Append(')');
                break;
            case GesSchemaTypeKind.Text:
                builder.Append("Text");
                if (type.Max is { } textMax) builder.Append(" max ").Append(textMax);
                break;
            case GesSchemaTypeKind.Bytes:
                builder.Append("Bytes");
                if (type.Max is { } bytesMax) builder.Append(" max ").Append(bytesMax);
                break;
            case GesSchemaTypeKind.Dice:
                builder.Append("Dice");
                if (type.Max is { } diceMax) builder.Append(" max ").Append(diceMax);
                WriteConstraints(type.Constraints, builder);
                break;
            case GesSchemaTypeKind.List:
                builder.Append("List");
                if (type.Element is { } element)
                {
                    builder.Append('(');
                    WriteUnion(element, builder);
                    builder.Append(')');
                }

                if (type.Max is { } listMax) builder.Append(" max ").Append(listMax);
                if (type.Depth > 0) builder.Append(" depth ").Append(type.Depth);
                break;
            case GesSchemaTypeKind.Reference:
                builder.Append(type.ReferenceName);
                break;
            default:
                builder.Append(type.Kind.ToString());
                break;
        }
    }

    private static void WriteConstraints(GesResolvedConstraints constraints, StringBuilder builder)
    {
        if (constraints.Integer) builder.Append(" integer");
        if (constraints.Lower is { } lower)
        {
            builder.Append(constraints.Wrapping ? " wrapping between " : " between ");
            builder.Append(Value(lower)).Append(' ').Append(Value(constraints.Upper!));
        }

        if (constraints.Step is { } step) builder.Append(" step ").Append(Value(step));
        if (constraints.Delta is { } delta) builder.Append(" delta ").Append(Value(delta));
    }

    private static string Value(GesResolvedValue value) => value.IsParameter ? value.Parameter : value.Value.Canonical;
}
