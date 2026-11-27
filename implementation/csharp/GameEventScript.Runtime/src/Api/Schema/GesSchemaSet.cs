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
    /// <exception cref="ArgumentNullException"><paramref name="name"/> or <paramref name="text"/> is <see langword="null"/>.</exception>
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
    // Schemas and message sets share one namespace; messages have their own.
    private readonly Dictionary<string, GesResolvedDeclaration> _declarations = new(StringComparer.Ordinal);
    private readonly Dictionary<string, GesResolvedDeclaration> _messages = new(StringComparer.Ordinal);
    private readonly List<GesResolvedDeclaration> _ordered = [];
    private readonly Dictionary<string, string> _tagSetSources = new(StringComparer.Ordinal);
    private readonly Dictionary<string, GesNetwork> _networks = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _networkSources = new(StringComparer.Ordinal);
    // Handle types, always written with `@`, have a namespace of their own.
    private readonly Dictionary<string, GesResolvedHandle> _handles = new(StringComparer.Ordinal);
    private readonly List<GesResolvedHandle> _orderedHandles = [];

    private GesSchemaSet(List<GesSchemaSource> sources) => _sources = sources;

    // The sources as given, which generated code embeds to convert to and from GES values.
    internal IReadOnlyList<GesSchemaSource> Sources => _sources;

    /// <summary>Reads and validates one or several sources. References resolve across all of them.</summary>
    /// <param name="sources">The sources.</param>
    /// <returns>The validated set.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="sources"/> is <see langword="null"/>.</exception>
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

    /// <summary>
    /// Gets a declaration by its exact name, compared ordinally. Messages and message sets have their own namespace:
    /// <c>:Name</c> selects a schema, <c>Name()</c> a message or message set, and a plain <c>Name</c> whichever of the two
    /// declares it.
    /// </summary>
    /// <param name="name">The name of a schema, message or message set, written <c>Name</c>, <c>:Name</c> or <c>Name()</c>.</param>
    /// <returns>Its description.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is <see langword="null"/>.</exception>
    /// <exception cref="GesSchemaException">The name is not declared (<c>schema.unknownName</c>), or a plain name declares
    /// both a schema or network and a message or message set (<c>schema.ambiguousName</c>).</exception>
    public GesSchema Schema(string name) => Declaration(name).Public;

    /// <summary>Gets the networks, in source order.</summary>
    public GesReadOnlyArray<GesNetwork> Networks { get; private set; }

    /// <summary>Gets a network by its exact name, compared ordinally.</summary>
    /// <param name="name">The network's name, without the leading <c>:</c>.</param>
    /// <returns>Its description.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is <see langword="null"/>.</exception>
    /// <exception cref="GesSchemaException">No network has that name (<c>schema.unknownName</c>).</exception>
    public GesNetwork Network(string name)
    {
        if (name is null) throw new ArgumentNullException(nameof(name));
        return _networks.TryGetValue(name, out var network) ? network
            : throw new GesSchemaException(Code.UnknownName, "", 0, 0, $"Network '{name}' is not declared.");
    }

    /// <summary>Gets the handle types, in source order.</summary>
    public GesReadOnlyArray<GesHandleType> HandleTypes { get; private set; }

    /// <summary>Gets a handle type by its exact name, compared ordinally.</summary>
    /// <param name="name">The handle type's name, with or without the leading <c>@</c>.</param>
    /// <returns>Its description.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is <see langword="null"/>.</exception>
    /// <exception cref="GesSchemaException">No handle type has that name (<c>schema.unknownName</c>).</exception>
    public GesHandleType HandleType(string name)
    {
        if (name is null) throw new ArgumentNullException(nameof(name));
        var plain = name.StartsWith("@", StringComparison.Ordinal) ? name.Substring(1) : name;
        return _handles.TryGetValue(plain, out var handle) ? handle.Public
            : throw new GesSchemaException(Code.UnknownName, "", 0, 0, $"Handle type '@{plain}' is not declared.");
    }

    // A handle type by its name without `@`, or null.
    internal GesResolvedHandle? FindHandle(string name) => _handles.TryGetValue(name, out var handle) ? handle : null;

    // The name of the source that declares a handle type.
    internal string HandleSourceName(string name) => _handles[name].Scope.SourceName;

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
        var schema = syntax.Schema == GesDocumentSchemaSyntax.Type ? TypeDeclaration(syntax.SchemaName)
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

    // The one schema of a source that no other schema of it refers to; a recursive schema's reference to itself does not count.
    private GesResolvedDeclaration RootSchema(string source)
    {
        var referenced = new HashSet<GesResolvedDeclaration>();
        foreach (var declaration in _ordered)
        {
            if (declaration.Scope.SourceName != source || declaration.Kind != GesSchemaKind.Schema) continue;
            foreach (var field in declaration.Fields)
                foreach (var type in field.Alternatives) CollectReferences(type, declaration, referenced);
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

    private static void CollectReferences(GesResolvedType type, GesResolvedDeclaration owner, HashSet<GesResolvedDeclaration> referenced)
    {
        if (type.Reference is { } reference && reference != owner) referenced.Add(reference);
        if (type.Element is { } element)
            foreach (var alternative in element) CollectReferences(alternative, owner, referenced);
    }

    // A root as an API or tool names it: `:Name` is a schema, `Name()` a message or message set and a plain name either,
    // when only one of the two namespaces declares it.
    internal GesResolvedDeclaration Declaration(string root)
    {
        if (root is null) throw new ArgumentNullException(nameof(root));
        if (root.StartsWith(":", StringComparison.Ordinal)) return TypeDeclaration(root.Substring(1));
        if (root.EndsWith("()", StringComparison.Ordinal))
        {
            var name = root.Substring(0, root.Length - 2);
            return _messages.TryGetValue(name, out var message) ? message
                : throw new GesSchemaException(Code.UnknownName, "", 0, 0, $"Message or message set '{name}' is not declared.");
        }

        var found = _declarations.TryGetValue(root, out var declaration);
        if (_messages.TryGetValue(root, out var other))
        {
            // A network shares the namespace of schemas, so it makes the plain name ambiguous too.
            var what = found ? "schema" : _networks.ContainsKey(root) ? "network" : null;
            var sent = other.Kind == GesSchemaKind.Message ? "message" : "message set";
            return what is null ? other : throw new GesSchemaException(Code.AmbiguousName, "", 0, 0, $"'{root}' names a {what} and a {sent}; write ':{root}' or '{root}()'.");
        }

        return found ? declaration! : throw new GesSchemaException(Code.UnknownName, "", 0, 0, $"'{root}' is not declared.");
    }

    // A schema, as a document header or a `:Name` names it; a message set named so gets a hint at its spelling.
    internal GesResolvedDeclaration TypeDeclaration(string name)
        => _declarations.TryGetValue(name, out var declaration) ? declaration
            : throw new GesSchemaException(Code.UnknownName, "", 0, 0, _messages.TryGetValue(name, out var set) && set.Kind == GesSchemaKind.Messages
                ? $"':{name}' is not declared; a message set is named without ':', as '{name}' or '{name}()'."
                : $"':{name}' is not declared.");

    // The name of the source that declares a schema, tag set or network, which places its generated code in split output.
    internal string SourceName(string name)
        => _declarations.TryGetValue(name, out var declaration) ? declaration.Scope.SourceName
            : _networkSources.TryGetValue(name, out var network) ? network
            : _tagSetSources.TryGetValue(name, out var tagSet) ? tagSet : MessageSourceName(name);

    // The name of the source that declares a message or message set.
    internal string MessageSourceName(string name) => _messages[name].Scope.SourceName;

    // Whether a plain name declares both a message or message set and a schema or network, so that a root needs `:` or `()`.
    internal bool NamesBoth(string name) => _messages.ContainsKey(name) && (_declarations.ContainsKey(name) || _networks.ContainsKey(name));

    // A declaration as a root names it: plainly, unless the name is also declared in the other namespace.
    internal string RootName(GesSchema schema)
        => !NamesBoth(schema.Name) ? schema.Name : schema.Kind == GesSchemaKind.Schema ? ":" + schema.Name : schema.Name + "()";

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
        var networkNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var file in files)
        {
            file.DeclareNames(_declarations, _messages, _ordered, tagSets, networkNames, _handles, _orderedHandles);
            foreach (var name in tagSets.Keys) _tagSetSources.TryAdd(name, file.SourceName);
        }
        foreach (var file in files) file.ResolveHandles(_declarations);
        var handleTypes = new List<GesHandleType>();
        foreach (var handle in _orderedHandles)
        {
            handle.Public = new GesHandleType(handle, _declarations);
            handleTypes.Add(handle.Public);
        }

        HandleTypes = new GesReadOnlyArray<GesHandleType>(handleTypes);
        foreach (var file in files) file.Resolve(_declarations, _messages);
        var composed = new HashSet<GesResolvedDeclaration>();
        foreach (var declaration in _ordered)
            if (declaration.Kind == GesSchemaKind.Schema) Compose(declaration, [], composed);
        var visited = new Dictionary<GesResolvedDeclaration, int>();
        foreach (var declaration in _ordered) CheckNesting(declaration, [], visited);
        foreach (var handle in _orderedHandles) handle.Scope.CheckHandleState(handle);
        foreach (var file in files) file.ResolveRecordDefaults();
        foreach (var declaration in _ordered)
            if (declaration.Packed) GesSchemaBinder.CheckPacked(declaration);
        foreach (var declaration in _ordered)
            if (declaration.Syntax.WireGroup is not null) GesSchemaBinder.CheckEncodable(declaration);
        var schemas = new List<GesSchema>();
        foreach (var declaration in _ordered)
        {
            declaration.Public = new GesSchema(declaration);
            schemas.Add(declaration.Public);
        }

        foreach (var schema in schemas) schema.Complete();
        foreach (var handleType in handleTypes) handleType.CompleteLinkHash();
        Schemas = new GesReadOnlyArray<GesSchema>(schemas);
        var networks = new List<GesNetwork>();
        foreach (var file in files)
        {
            var start = networks.Count;
            file.ResolveNetworks(_declarations, _messages, networks);
            for (var index = start; index < networks.Count; index++) _networkSources.Add(networks[index].Name, file.SourceName);
        }

        foreach (var network in networks) _networks.Add(network.Name, network);
        Networks = new GesReadOnlyArray<GesNetwork>(networks);
    }

    // Composes the schemas a schema takes before it, so that it copies their complete fields; `with` must not form a cycle.
    private void Compose(GesResolvedDeclaration declaration, List<GesResolvedDeclaration> path, HashSet<GesResolvedDeclaration> composed)
    {
        if (composed.Contains(declaration)) return;
        path.Add(declaration);
        if (declaration.IsProjection)
        {
            var syntax = declaration.Syntax;
            if (!_declarations.TryGetValue(syntax.State, out var source) || source.Kind != GesSchemaKind.Schema)
                throw declaration.Scope.Error(Code.InvalidProjection, syntax.StateOffset, $"The source of '{declaration.Name}', ':{syntax.State}', is no declared schema or projection.");
            if (source.Abstract) throw declaration.Scope.Error(Code.InvalidProjection, syntax.StateOffset, $"The source of '{declaration.Name}', ':{syntax.State}', is abstract.");
            if (source.Packed) throw declaration.Scope.Error(Code.InvalidProjection, syntax.StateOffset, $"The source of '{declaration.Name}', ':{syntax.State}', is packed.");
            if (path.Contains(source)) throw declaration.Scope.Error(Code.InvalidProjection, syntax.StateOffset, $"'{declaration.Name}' projects ':{syntax.State}', which projects it in turn.");
            Compose(source, path, composed);
            path.RemoveAt(path.Count - 1);
            declaration.Source = source;
            declaration.Scope.ComposeProjection(declaration);
            composed.Add(declaration);
            return;
        }

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
    // x, y, z, or the radius of a Disc.
    internal string Axis { get; init; } = "";

    // The order of the axes in a block: x, y, z, then the radius.
    internal static int Order(string axis) => axis switch { "x" => 0, "y" => 1, "z" => 2, _ => 3 };

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

    // The handle type of a handle, or the key type of a handle Map.
    internal GesResolvedHandle? Handle { get; init; }

    // A handle written `@Name~`, which carries its version.
    internal bool Versioned { get; init; }

    // The projection of a handle written `@Name(:P)`, which it reads; null without one.
    internal GesResolvedDeclaration? Projection { get; init; }

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

/// <summary>The default of a field: an exact number in base units, a Boolean, a tag, the name of a sibling field, a record
/// literal, or the empty Map or List.</summary>
internal sealed class GesResolvedDefault
{
    internal GesSchemaDefaultKind Kind { get; init; }

    // The fields a record literal names, which every copy of the field taken with `with` shares.
    internal GesResolvedRecord? Record { get; init; }

    internal GesResolvedRecordEntry[] Entries => Record?.Entries ?? [];

    internal GesSchemaDecimal Value { get; init; }

    internal bool Boolean { get; init; }

    // The tag without '#', or the sibling field.
    internal string Name { get; init; } = "";

    internal int Offset { get; init; }
}

// A field a record literal names, with its default-like value: a number, a Boolean, a tag, or again a record literal.
internal sealed class GesResolvedRecordEntry
{
    internal GesResolvedField Field { get; init; } = null!;

    internal GesResolvedDefault Value { get; init; } = null!;
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

    // A packed schema's machine word: the declared width or the smallest of 8, 16, 32 and 64 that holds its bits.
    internal bool Packed => Syntax.Packed;

    internal int PackedBits { get; set; }

    internal int PackedWidth { get; set; }

    internal string[] Tags { get; init; } = [];

    internal GesResolvedDeclaration[] Members { get; set; } = [];

    // The fields written `computed by`, own and taken, in the order their expressions may read each other.
    internal GesResolvedComputed[] Computed { get; set; } = [];

    // A projection's source and, per taken field, the source fields along its path, from the source's root.
    internal bool IsProjection => Syntax.Kind == GesSchemaDeclarationKind.Projection;

    internal GesResolvedDeclaration? Source { get; set; }

    internal GesResolvedField[][] Paths { get; set; } = [];

    // Whether this schema is a projection that narrows the other: the other is its source or a schema its source narrows.
    internal bool Narrows(GesResolvedDeclaration other)
    {
        for (var source = Source; source is not null; source = source.Source)
            if (source == other) return true;
        return false;
    }

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
    private readonly List<GesSchemaDeclarationSyntax> _networks = [];
    private readonly List<GesResolvedHandle> _ownHandles = [];
    private Dictionary<string, GesResolvedHandle> _handles = new(StringComparer.Ordinal);

    // The lets of the schema being resolved, which its fields can name.
    private readonly Dictionary<string, GesResolvedLet> _lets = new(StringComparer.Ordinal);

    // Record literals of fields, resolved once every schema is composed, since they name the fields of another schema.
    private readonly List<(GesResolvedRecord Record, GesResolvedDeclaration Schema, GesSchemaDefaultSyntax Syntax)> _records = [];

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

    internal void DeclareNames(Dictionary<string, GesResolvedDeclaration> all, Dictionary<string, GesResolvedDeclaration> messages, List<GesResolvedDeclaration> ordered, Dictionary<string, GesSchemaDeclarationSyntax> tagSets, HashSet<string> networks, Dictionary<string, GesResolvedHandle> handles, List<GesResolvedHandle> orderedHandles)
    {
        _tagSets = tagSets;
        _handles = handles;
        foreach (var declaration in _syntax.Declarations)
        {
            switch (declaration.Kind)
            {
                case GesSchemaDeclarationKind.TagSet:
                {
                    if (GesSchemaTypeNames.IsBuiltIn(declaration.Name))
                        throw Error(Code.DuplicateName, declaration.Offset, $"'{declaration.Name}' is a built-in type.");
                    if (all.ContainsKey(declaration.Name) || networks.Contains(declaration.Name) || !tagSets.TryAdd(declaration.Name, declaration))
                        throw Error(Code.DuplicateName, declaration.Offset, $"'{declaration.Name}' is declared twice.");
                    var members = new HashSet<string>(StringComparer.Ordinal);
                    foreach (var tag in declaration.Tags)
                        if (!members.Add(tag)) throw Error(Code.DuplicateName, declaration.Offset, $"Tag '#{tag}' is listed twice.");
                    break;
                }
                case GesSchemaDeclarationKind.Network:
                    if (GesSchemaTypeNames.IsBuiltIn(declaration.Name))
                        throw Error(Code.DuplicateName, declaration.Offset, $"'{declaration.Name}' is a built-in type.");
                    if (all.ContainsKey(declaration.Name) || tagSets.ContainsKey(declaration.Name) || !networks.Add(declaration.Name))
                        throw Error(Code.DuplicateName, declaration.Offset, $"'{declaration.Name}' is declared twice.");
                    _networks.Add(declaration);
                    break;
                case GesSchemaDeclarationKind.Handle:
                {
                    var handle = new GesResolvedHandle { Name = declaration.Name, Offset = declaration.Offset, Scope = this, Syntax = declaration };
                    if (!handles.TryAdd(declaration.Name, handle)) throw Error(Code.DuplicateName, declaration.Offset, $"Handle type '@{declaration.Name}' is declared twice.");
                    orderedHandles.Add(handle);
                    _ownHandles.Add(handle);
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
                            GesSchemaDeclarationKind.Schema or GesSchemaDeclarationKind.Projection => GesSchemaKind.Schema,
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
                    // Messages and message sets share one namespace; schemas, tag sets and networks the other.
                    if (resolved.Kind != GesSchemaKind.Schema
                            ? !messages.TryAdd(declaration.Name, resolved)
                            : tagSets.ContainsKey(declaration.Name) || networks.Contains(declaration.Name) || !all.TryAdd(declaration.Name, resolved))
                        throw Error(Code.DuplicateName, declaration.Offset, resolved.Kind == GesSchemaKind.Schema ? $"'{declaration.Name}' is declared twice." : $"Message or message set '{declaration.Name}' is declared twice.");
                    ordered.Add(resolved);
                    _own.Add(resolved);
                    break;
            }
        }
    }

    // A handle type's state is a declared, non-abstract schema, a composite's root a handle type that is not composite
    // itself; its id, part and revision are resolved like field types, without lets.
    internal void ResolveHandles(Dictionary<string, GesResolvedDeclaration> all)
    {
        _lets.Clear();
        foreach (var handle in _ownHandles)
        {
            var syntax = handle.Syntax;
            if (syntax.Catalog.Length > 0)
                handle.Catalog = CatalogOf(handle);
            else if (!all.TryGetValue(syntax.State, out var state) || state.Kind != GesSchemaKind.Schema)
                throw Error(Code.InvalidHandle, syntax.StateOffset, $"The state of '@{handle.Name}', ':{syntax.State}', is no declared schema.");
            else if (state.Abstract) throw Error(Code.InvalidHandle, syntax.StateOffset, $"The state of '@{handle.Name}', ':{syntax.State}', is abstract.");
            else handle.State = state;
            if (syntax.Root.Length > 0)
            {
                var root = Handle(syntax.Root, syntax.RootOffset);
                if (root.Syntax.Root.Length > 0)
                    throw Error(Code.InvalidHandle, syntax.RootOffset, $"'@{syntax.Root}' is composite itself; composites have one level.");
                handle.Root = root;
            }

            handle.Id = ResolveType(syntax.Id!, all);
            if (handle.Id.Kind == GesSchemaTypeKind.Number)
                foreach (var bound in new[] { handle.Id.Constraints.Lower!, handle.Id.Constraints.Upper! })
                    if (bound.IsParameter) throw Error(Code.InvalidHandle, bound.Offset, "A handle type's bounds are literals or constants, never parameters.");
                    else if (bound.Value.Exponent + Digits(Math.Abs(bound.Value.Significand)) > 18)
                        throw Error(Code.InvalidHandle, bound.Offset, "An integer id or part lies within 18 digits.");
            if (syntax.Revision is { } revision) handle.Revision = ResolveType(revision, all);
        }
    }

    // A catalog file type `:Catalog(@Unit)` is composite with a Text part, and its catalog's type is a declared handle type
    // with a Text id that is neither composite nor a catalog file type itself.
    private GesResolvedHandle CatalogOf(GesResolvedHandle handle)
    {
        var syntax = handle.Syntax;
        if (syntax.Root.Length == 0)
            throw Error(Code.InvalidHandle, syntax.CatalogOffset, $"The catalog file type '@{handle.Name}' is no composite; it names the files of a root.");
        if (syntax.Id!.Kind != GesSchemaTypeKind.Text)
            throw Error(Code.InvalidHandle, syntax.Id.Offset, $"The part of the catalog file type '@{handle.Name}' is a path, a :Text with max.");
        var target = Handle(syntax.Catalog, syntax.CatalogOffset);
        if (target.Syntax.Catalog.Length > 0 || target.Syntax.Root.Length > 0 || target.Syntax.Id!.Kind != GesSchemaTypeKind.Text)
            throw Error(Code.InvalidHandle, syntax.CatalogOffset, $"A catalog holds a handle type with a Text id that is not composite, unlike '@{syntax.Catalog}'.");
        target.Files.Add(handle);
        return target;
    }

    // A state field named `id`, `version` or `root` would collide with the handle's members; a catalog file type has no state.
    internal void CheckHandleState(GesResolvedHandle handle)
    {
        if (handle.Catalog is not null) return;
        foreach (var name in new[] { "id", "version", "root" })
            if (Array.Exists(handle.State.Fields, field => field.Name == name) || Array.Exists(handle.State.Computed, field => field.Name == name))
                throw Error(Code.InvalidHandle, handle.Syntax.StateOffset, $"The state ':{handle.State.Name}' of '@{handle.Name}' has a field '{name}', which collides with the handle's members.");
    }

    private GesResolvedHandle Handle(string name, int offset)
        => _handles.TryGetValue(name, out var handle) ? handle : throw Error(Code.UnknownName, offset, $"Handle type '@{name}' is not declared.");

    private GesResolvedHandle KeyHandle(string name, int offset)
    {
        var handle = Handle(name, offset);
        return handle.Syntax.Catalog.Length == 0 ? handle
            : throw Error(Code.InvalidHandle, offset, $"'@{name}' is a catalog file type, which only reading a catalog uses.");
    }

    internal void Resolve(Dictionary<string, GesResolvedDeclaration> all, Dictionary<string, GesResolvedDeclaration> messages)
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
                    if (!messages.TryGetValue(name, out var member) || member.Kind != GesSchemaKind.Message)
                        throw Error(Code.UnknownName, syntax.MemberOffsets[index], member is null ? $"Message '{name}' is not declared." : $"'{name}' is a message set; a set lists messages.");
                    members[index] = member;
                }

                declaration.Members = members;
            }

            var names = new HashSet<string>(StringComparer.Ordinal);
            // A set's parameter cannot be named `message`, which the set's value names the carried message by.
            if (declaration.Kind == GesSchemaKind.Messages) names.Add(GesSchemaValues.MessageLabel);
            var tags = new HashSet<string>(StringComparer.Ordinal);
            foreach (var tag in syntax.Tags)
                if (!tags.Add(tag)) throw Error(Code.DuplicateName, syntax.Offset, $"Tag '#{tag}' is listed twice.");
            declaration.Lets = ResolveLets(syntax, names);
            var fields = new GesResolvedField[syntax.Fields.Length];
            var recursive = false;
            for (var index = 0; index < fields.Length; index++)
            {
                var field = syntax.Fields[index];
                if (!names.Add(field.Name))
                    throw Error(Code.DuplicateName, field.Offset, declaration.Kind == GesSchemaKind.Messages && field.Name == GesSchemaValues.MessageLabel
                        ? $"A parameter of '{declaration.Name}' cannot be named '{GesSchemaValues.MessageLabel}', which names the carried message."
                        : $"'{field.Name}' is declared twice in '{declaration.Name}'.");
                fields[index] = ResolveField(field, index, declaration.Kind, all);
                CheckDepth(field.Alternatives, fields[index].Alternatives, declaration, top: true);
                if (fields[index].Alternatives[0].Depth == 0) continue;
                if (recursive) throw Error(Code.InvalidConstraint, field.Alternatives[0].DepthOffset, "A schema holds at most one List with depth.");
                recursive = true;
            }

            declaration.Fields = fields;
            if (declaration.Kind != GesSchemaKind.Schema) ResolveArgumentDefaults(declaration);
        }
    }

    // An argument default is a literal number or constant, a Boolean, a tag of the argument's single type or the empty List
    // or Map; it fills a missing argument in a lenient read, so a defaulted argument is required on the wire. A set's
    // parameters follow the same rules.
    private void ResolveArgumentDefaults(GesResolvedDeclaration declaration)
    {
        for (var index = 0; index < declaration.Fields.Length; index++)
        {
            if (declaration.Syntax.Fields[index].Default is not { } written) continue;
            var field = declaration.Fields[index];
            // `[:]` is the empty Map here, never a record literal.
            var emptyMap = written.Record is { Length: 0 } && field.Alternatives[0].Kind == GesSchemaTypeKind.Map;
            if (written.Value is { Name.Length: > 0 } || (written.Record is not null && !emptyMap))
                throw Error(Code.InvalidDefault, written.Offset, "An argument default is a literal or a constant.");
            if (field.Marker == GesSchemaFieldMarker.Optional) throw Error(Code.InvalidDefault, written.Offset, "An argument is optional or has a default, not both.");
            if (field.Alternatives.Length != 1) throw Error(Code.InvalidDefault, written.Offset, "A union argument takes no default.");
            field.Default = ResolveDefault(field.Alternatives[0], written);
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

    // Copies the fields a projection takes from its composed source along their paths (specs/Schemas.md#projections). A
    // field keeps its type, file and marker; a path through an optional Record makes it optional without default, a key
    // stays one only when taken bare, and a sibling default only when the sibling is taken bare too.
    internal void ComposeProjection(GesResolvedDeclaration declaration)
    {
        var source = declaration.Source!;
        var taken = declaration.Syntax.Taken;
        var fields = new GesResolvedField[taken.Length];
        var paths = new GesResolvedField[taken.Length][];
        var names = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < taken.Length; index++)
        {
            var entry = taken[index];
            if (!names.Add(entry.Name)) throw Error(Code.InvalidProjection, entry.Offset, $"'{entry.Name}' is taken twice by '{declaration.Name}'.");
            var holder = source;
            var path = new GesResolvedField[entry.Path.Length];
            var throughOptional = false;
            for (var segment = 0; segment < path.Length; segment++)
            {
                var name = entry.Path[segment];
                var offset = entry.PathOffsets[segment];
                var field = Array.Find(holder.Fields, candidate => candidate.Name == name);
                if (field is null)
                    throw Error(Code.InvalidProjection, offset, Array.Exists(holder.Computed, computed => computed.Name == name)
                        ? $"'{name}' of '{holder.Name}' is computed; a projection takes stored fields."
                        : $"'{name}' is no field of '{holder.Name}'.");
                path[segment] = field;
                if (segment == path.Length - 1) break;
                if (field.Alternatives is not [{ Kind: GesSchemaTypeKind.Reference, Reference: { } record }])
                    throw Error(Code.InvalidProjection, entry.PathOffsets[segment + 1], $"'{name}' of '{holder.Name}' is no Record, so a path cannot continue through it.");
                throughOptional |= field.Marker == GesSchemaFieldMarker.Optional;
                holder = record;
            }

            var last = path[path.Length - 1];
            if (Array.Exists(last.Alternatives, alternative => alternative.Depth > 0))
                throw Error(Code.InvalidProjection, entry.PathOffsets[path.Length - 1], $"'{last.Name}' is a recursive List, which a projection does not take.");
            var bare = path.Length == 1 && entry.Path[0] == entry.Name;
            var marker = last.Marker;
            if (marker == GesSchemaFieldMarker.Key)
            {
                if (!bare) marker = GesSchemaFieldMarker.None;
                else if (index != 0) throw Error(Code.InvalidKey, entry.Offset, "A key is the first field of a schema, an integer or a Tag with a set, without delta within or a union.");
            }

            if (throughOptional)
                marker = last.Alternatives.Length > 1 && Array.Exists(last.Alternatives, alternative => alternative.Kind == GesSchemaTypeKind.Nothing) ? GesSchemaFieldMarker.None : GesSchemaFieldMarker.Optional;
            fields[index] = new GesResolvedField
            {
                Name = entry.Name,
                Unlabeled = last.Unlabeled,
                Alternatives = last.Alternatives,
                Marker = marker,
                Default = throughOptional ? null : last.Default,
                Offset = last.Offset,
                Scope = last.Scope
            };
            paths[index] = path;
        }

        for (var index = 0; index < fields.Length; index++)
            if (fields[index].Default is { Kind: GesSchemaDefaultKind.Field } fallback
                && (paths[index].Length != 1 || !Array.Exists(taken, entry => entry.Name == fallback.Name && entry.Path is [var only] && only == fallback.Name)))
                throw Error(Code.InvalidDefault, taken[index].Offset, $"'{fields[index].Name}' defaults to '{fallback.Name}', which '{declaration.Name}' does not take under its name.");
        declaration.Fields = fields;
        declaration.Paths = paths;
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

    // A default is a number, a Boolean or a tag of the field's single type, a record literal of a schema, `[:]` of a Map, `[]`
    // of a List, or a sibling field of the same type whose value it
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
            case GesSchemaTypeKind.Reference when written.Record is not null:
                var record = new GesResolvedRecord();
                _records.Add((record, type.Reference!, written));
                return new GesResolvedDefault { Kind = GesSchemaDefaultKind.Record, Record = record, Offset = written.Offset };
            case GesSchemaTypeKind.Map when written.Record is { Length: 0 }:
            case GesSchemaTypeKind.List when written.EmptyList:
                return new GesResolvedDefault { Kind = GesSchemaDefaultKind.Empty, Offset = written.Offset };
            default:
                throw Error(Code.InvalidDefault, written.Offset, $"A {type.Kind} field takes no such default; a sibling field of its type can be one.");
        }
    }

    internal void ResolveRecordDefaults()
    {
        foreach (var (record, schema, written) in _records) record.Entries = ResolveRecord(schema, written);
    }

    // The fields a record literal names, in the field order of its schema. Each name is a stored field, at most once, whose
    // value is checked as its own default would be; a required field it leaves out has a default or accepts nothing.
    private GesResolvedRecordEntry[] ResolveRecord(GesResolvedDeclaration schema, GesSchemaDefaultSyntax written)
    {
        var entries = written.Record!;
        var resolved = new GesResolvedDefault[schema.Fields.Length];
        foreach (var entry in entries)
        {
            var index = Array.FindIndex(schema.Fields, field => field.Name == entry.Name);
            if (index < 0) throw Error(Code.InvalidDefault, entry.Offset, $"'{entry.Name}' is no stored field of '{schema.Name}'.");
            if (resolved[index] is not null) throw Error(Code.InvalidDefault, entry.Offset, $"The record literal names '{entry.Name}' twice.");
            var field = schema.Fields[index];
            var value = entry.Value;
            if (value.Value is { Name.Length: > 0 }) throw Error(Code.InvalidDefault, entry.Offset, "A record literal names no let, parameter or field.");
            if (field.Alternatives.Length != 1) throw Error(Code.InvalidDefault, entry.Offset, $"'{entry.Name}' is a union, which a record literal leaves out.");
            var type = field.Alternatives[0];
            if (type.Kind == GesSchemaTypeKind.Reference && value.Record is not null)
            {
                resolved[index] = new GesResolvedDefault { Kind = GesSchemaDefaultKind.Record, Record = new GesResolvedRecord { Entries = ResolveRecord(type.Reference!, value) }, Offset = entry.Offset };
                continue;
            }

            var takes = type.Kind switch
            {
                GesSchemaTypeKind.Boolean => value.Boolean is not null,
                GesSchemaTypeKind.Tag => value.Tag is not null,
                GesSchemaTypeKind.Number or GesSchemaTypeKind.Percentage or GesSchemaTypeKind.Quantity => value.Value is not null,
                _ => false
            };
            if (!takes)
                throw Error(Code.InvalidDefault, entry.Offset, $"'{entry.Name}' is a {type.Kind} field, which takes no such value in a record literal.");
            resolved[index] = ResolveDefault(type, new GesSchemaDefaultSyntax { Value = value.Value, Boolean = value.Boolean, Tag = value.Tag, Offset = entry.Offset });
        }

        var result = new List<GesResolvedRecordEntry>();
        for (var index = 0; index < resolved.Length; index++)
        {
            var field = schema.Fields[index];
            if (resolved[index] is { } value) result.Add(new GesResolvedRecordEntry { Field = field, Value = value });
            else if (field.Default is null && field.Marker != GesSchemaFieldMarker.Optional && !Array.Exists(field.Alternatives, alternative => alternative.Kind == GesSchemaTypeKind.Nothing))
                throw Error(Code.InvalidDefault, written.Offset, $"The record literal leaves out '{field.Name}' of '{schema.Name}', which has no default.");
        }

        return result.ToArray();
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
            // `@Name` and `@Name~` share their name, so one union never holds both.
            var name = result[index].Kind switch
            {
                GesSchemaTypeKind.Reference => ":" + alternative.Name,
                GesSchemaTypeKind.Handle => "@" + alternative.Name,
                _ => result[index].Kind.ToString()
            };
            if (!seen.Add(name))
                throw result[index].Kind == GesSchemaTypeKind.Handle && Array.Exists(result, other => other is not null && other != result[index] && other.Kind == GesSchemaTypeKind.Handle && other.Handle == result[index].Handle && other.Projection != result[index].Projection)
                    ? Error(Code.InvalidProjection, alternative.Offset, $"A union holds '@{alternative.Name}' with two projections or with and without one.")
                    : Error(Code.InvalidUnion, alternative.Offset, "A union repeats a type name; a unit does not distinguish alternatives.");
            if (alternative.Constraints.Delta is not null) throw Error(Code.InvalidUnion, alternative.Offset, "A union alternative has no delta within.");
            // Generated code names an alternative after its label or its type, so both must stay apart.
            var label = alternative.Label.Length > 0 ? char.ToUpperInvariant(alternative.Label[0]) + alternative.Label.Substring(1)
                : result[index].Kind is GesSchemaTypeKind.Reference or GesSchemaTypeKind.Handle ? alternative.Name : result[index].Kind.ToString();
            if (!labels.Add(label)) throw Error(Code.DuplicateName, alternative.Label.Length > 0 ? alternative.LabelOffset : alternative.Offset, $"Two alternatives are both named '{label}'.");
        }

        return result;
    }

    // `@Name(:P)` names a projection that narrows the state of a handle type that is neither composite nor a catalog file
    // type. Narrowing follows the declared sources by name, since projections are composed only later.
    private GesResolvedDeclaration HandleProjection(GesResolvedHandle handle, GesSchemaTypeSyntax type, Dictionary<string, GesResolvedDeclaration> all)
    {
        if (handle.Root is not null)
            throw Error(Code.InvalidProjection, type.ProjectionOffset, $"'@{type.Name}' is composite; a projection reads a handle type that is not.");
        if (!all.TryGetValue(type.Projection, out var projection) || !projection.IsProjection)
            throw Error(Code.InvalidProjection, type.ProjectionOffset, $"':{type.Projection}' is no declared projection.");
        var source = projection;
        for (var steps = 0; source.IsProjection && steps <= all.Count; steps++)
        {
            if (!all.TryGetValue(source.Syntax.State, out var next)) break;
            source = next;
            if (source == handle.State) return projection;
        }

        throw Error(Code.InvalidProjection, type.ProjectionOffset, $"':{type.Projection}' is no projection of '@{type.Name}', whose state is ':{handle.State.Name}'.");
    }

    private GesResolvedType ResolveType(GesSchemaTypeSyntax type, Dictionary<string, GesResolvedDeclaration> all)
    {
        if (type.Kind == GesSchemaTypeKind.Handle)
        {
            var handle = Handle(type.Name, type.Offset);
            if (handle.Syntax.Catalog.Length > 0)
                throw Error(Code.InvalidHandle, type.Offset, $"'@{type.Name}' is a catalog file type, which only reading a catalog uses.");
            if (type.Versioned && !handle.Versioned)
                throw Error(Code.InvalidHandle, type.Offset, $"'@{type.Name}' has no version, so '@{type.Name}~' does not exist.");
            var projection = type.Projection.Length > 0 ? HandleProjection(handle, type, all) : null;
            return new GesResolvedType { Kind = GesSchemaTypeKind.Handle, Handle = handle, Versioned = type.Versioned, Projection = projection, ReferenceName = type.Name, Offset = type.Offset };
        }

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
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < axes.Length; index++)
            {
                if (!seen.Add(written[index].Axis)) throw Error(Code.DuplicateName, written[index].Offset, $"Axis '{written[index].Axis}' is declared twice.");
                axes[index] = new GesResolvedAxis { Axis = written[index].Axis, Constraints = ResolveConstraints(written[index].Constraints, type.Unit, false, written[index].Offset) };
            }

            Array.Sort(axes, (left, right) => GesResolvedAxis.Order(left.Axis).CompareTo(GesResolvedAxis.Order(right.Axis)));
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
            Handle = type.Key.Length > 0 ? KeyHandle(type.Key, type.KeyOffset) : null,
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
        var parameters = new SortedDictionary<string, GesSchemaTypeSyntax>(StringComparer.Ordinal);
        var reach = new Reached([], parameters, []);
        Reach(root, reach);
        var builder = new StringBuilder("gess1\n");
        foreach (var parameter in parameters) builder.Append("parameter ").Append(parameter.Key).Append(' ').Append(ParameterKind(parameter.Value)).Append('\n');
        foreach (var declaration in reach.Order!)
        {
            if (declaration is GesResolvedHandle handle) handle.WriteCanonical(builder);
            else Write((GesResolvedDeclaration)declaration, builder);
        }

        return builder.ToString();
    }

    // The form a handle hash runs over: the handle type's declaration and, for a composite, its root's. A handle type has
    // no parameters and never reaches its state.
    internal static string HandleForm(GesResolvedHandle handle)
    {
        var builder = new StringBuilder("gess1\n");
        handle.WriteCanonical(builder);
        handle.Root?.WriteCanonical(builder);
        return builder.ToString();
    }

    // An id, part or revision type: `Number` with its constraints or `Text max n`.
    internal static void WriteHandleType(GesResolvedType type, StringBuilder builder) => WriteType(type, builder);

    internal static void Reach(GesResolvedDeclaration declaration, List<GesResolvedDeclaration> declarations, SortedDictionary<string, GesSchemaTypeSyntax> parameters)
        => Reach(declaration, new Reached(declarations, parameters, null));

    // The schemas, parameters and, for a canonical form, the declarations of schemas and handle types in reach order.
    private sealed class Reached(List<GesResolvedDeclaration> declarations, SortedDictionary<string, GesSchemaTypeSyntax> parameters, List<object>? order)
    {
        internal HashSet<object> Seen { get; } = [];

        internal List<GesResolvedDeclaration> Declarations { get; } = declarations;

        internal SortedDictionary<string, GesSchemaTypeSyntax> Parameters { get; } = parameters;

        internal List<object>? Order { get; } = order;
    }

    private static void Reach(GesResolvedDeclaration declaration, Reached reach)
    {
        if (!reach.Seen.Add(declaration)) return;
        reach.Declarations.Add(declaration);
        reach.Order?.Add(declaration);
        foreach (var field in declaration.Fields)
            foreach (var type in field.Alternatives) Reach(type, field.Scope, reach);
        foreach (var member in declaration.Members) Reach(member, reach);
    }

    // A handle type is written where it is first reached, followed by its root; its state is not reached.
    private static void Reach(GesResolvedHandle handle, Reached reach)
    {
        if (reach.Order is null || !reach.Seen.Add(handle)) return;
        reach.Order.Add(handle);
        if (handle.Root is { } root) Reach(root, reach);
    }

    private static void Reach(GesResolvedType type, GesFileScope scope, Reached reach)
    {
        ReachValues(type.Constraints, scope, reach.Parameters);
        if (type.Axes is { } axes)
            foreach (var axis in axes) ReachValues(axis.Constraints, scope, reach.Parameters);
        if (type.Handle is { } handle) Reach(handle, reach);
        if (type.Element is { } element)
            foreach (var alternative in element) Reach(alternative, scope, reach);
        if (type.Reference is { } reference) Reach(reference, reach);
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
                builder.Append("schema ").Append(declaration.Name);
                if (declaration.Packed) builder.Append(" packed ").Append(declaration.PackedWidth);
                builder.Append('\n');
                foreach (var let in declaration.Lets) builder.Append("let ").Append(let.Name).Append(' ').Append(LetKind(let)).Append(' ').Append(let.Value.Canonical).Append('\n');
                foreach (var field in declaration.Fields) WriteField("field ", field, builder);
                foreach (var computed in declaration.Computed)
                {
                    builder.Append("computed ").Append(computed.Name).Append(' ').Append(computed.Kind switch
                    {
                        GesSchemaTypeKind.Quantity => "Quantity(" + computed.Unit.ToTypeName() + ")",
                        _ when computed.IsSpatial && computed.Unit != GesQuantityUnit.UnitNone => computed.Kind + "(" + computed.Unit.ToTypeName() + ")",
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
                foreach (var field in declaration.Fields) WriteField("argument ", field, builder);
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
                GesSchemaDefaultKind.Record => RecordText(fallback),
                GesSchemaDefaultKind.Empty => field.Alternatives[0].Kind == GesSchemaTypeKind.Map ? "[:]" : "[]",
                _ => "field " + fallback.Name
            });
        }

        builder.Append('\n');
    }

    // `[:]`, or `[name: value, ...]` in the field order of the record's schema.
    private static string RecordText(GesResolvedDefault fallback)
    {
        if (fallback.Entries.Length == 0) return "[:]";
        var builder = new StringBuilder("[");
        for (var index = 0; index < fallback.Entries.Length; index++)
        {
            var entry = fallback.Entries[index];
            if (index > 0) builder.Append(", ");
            builder.Append(entry.Field.Name).Append(": ").Append(entry.Value.Kind switch
            {
                GesSchemaDefaultKind.Number => entry.Value.Value.Canonical,
                GesSchemaDefaultKind.Boolean => entry.Value.Boolean ? "true" : "false",
                GesSchemaDefaultKind.Tag => "#" + entry.Value.Name,
                _ => RecordText(entry.Value)
            });
        }

        return builder.Append(']').ToString();
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
            case GesSchemaTypeKind.Point or GesSchemaTypeKind.Vector or GesSchemaTypeKind.Box or GesSchemaTypeKind.Poly or GesSchemaTypeKind.Disc:
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
            case GesSchemaTypeKind.Disc:
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
            case GesSchemaTypeKind.Handle:
                builder.Append('@').Append(type.Handle!.Name);
                if (type.Projection is { } projected) builder.Append('(').Append(projected.Name).Append(')');
                if (type.Versioned) builder.Append('~');
                break;
            case GesSchemaTypeKind.Map when type.Handle is { } key:
                builder.Append("Map(@").Append(key.Name).Append(" -> ");
                WriteUnion(type.Element!, builder);
                builder.Append(')');
                if (type.Max is { } mapMax) builder.Append(" max ").Append(mapMax);
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
