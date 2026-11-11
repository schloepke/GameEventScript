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
            if (declaration.Scope.SourceName != source || declaration.Kind != GesSchemaKind.Schema || referenced.Contains(declaration)) continue;
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
        foreach (var file in files) file.DeclareNames(_declarations, _ordered, tagSets);
        foreach (var file in files) file.Resolve(_declarations);
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
            foreach (var type in field.Alternatives) CheckNesting(type, path, visited);
        path.RemoveAt(path.Count - 1);
    }

    private static void CheckNesting(GesResolvedType type, List<GesResolvedDeclaration> path, Dictionary<GesResolvedDeclaration, int> visited)
    {
        if (type.Reference is { } reference) CheckNesting(reference, path, visited);
        if (type.Element is { } element)
            foreach (var alternative in element) CheckNesting(alternative, path, visited);
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

    internal GesResolvedConstraints Constraints { get; init; } = new();

    internal GesResolvedAxis[]? Axes { get; init; }

    internal GesResolvedDeclaration? Reference { get; set; }

    internal string ReferenceName { get; init; } = "";

    // The named tag set a Tag takes its members from, or empty.
    internal string TagSet { get; init; } = "";

    internal int Offset { get; init; }
}

internal sealed class GesResolvedField
{
    internal string Name { get; init; } = "";

    internal bool Unlabeled { get; init; }

    internal GesResolvedType[] Alternatives { get; init; } = [];

    internal GesSchemaFieldMarker Marker { get; init; }

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

    internal string[] Tags { get; init; } = [];

    internal GesResolvedDeclaration[] Members { get; set; } = [];

    // The public description, which exists once all declarations are resolved.
    internal GesSchema Public { get; set; } = null!;
}

/// <summary>One source: its constants and parameters, which only its own declarations see.</summary>
internal sealed class GesFileScope
{
    private readonly GesSchemaSource _source;
    private readonly GesSchemaSourceSyntax _syntax;
    private readonly Dictionary<string, GesSchemaValueSyntax> _constants = new(StringComparer.Ordinal);
    private readonly List<GesResolvedDeclaration> _own = [];
    private Dictionary<string, GesSchemaDeclarationSyntax> _tagSets = new(StringComparer.Ordinal);

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
            var fields = new GesResolvedField[syntax.Fields.Length];
            for (var index = 0; index < fields.Length; index++)
            {
                var field = syntax.Fields[index];
                if (!names.Add(field.Name)) throw Error(Code.DuplicateName, field.Offset, $"Field '{field.Name}' is declared twice.");
                fields[index] = ResolveField(field, index, declaration.Kind, all);
            }

            declaration.Fields = fields;
        }
    }

    private GesResolvedField ResolveField(GesSchemaFieldSyntax field, int index, GesSchemaKind owner, Dictionary<string, GesResolvedDeclaration> all)
    {
        var alternatives = ResolveUnion(field.Alternatives, all);
        if (field.Marker == GesSchemaFieldMarker.Key)
        {
            var first = alternatives[0];
            var keyable = first.Constraints.Integer || (first.Kind == GesSchemaTypeKind.Tag && first.Tags is not null);
            if (owner != GesSchemaKind.Schema || index != 0 || alternatives.Length != 1 || !keyable || first.Constraints.Delta is not null)
                throw Error(Code.InvalidKey, field.Offset, "A key is the first field of a schema, an integer or a Tag with a set, without delta within or a union.");
        }

        if (field.Marker == GesSchemaFieldMarker.Optional && alternatives.Length > 1 && Array.Exists(alternatives, alternative => alternative.Kind == GesSchemaTypeKind.Nothing))
            throw Error(Code.InvalidUnion, field.Offset, "A union with Nothing already holds an absent value; it is not optional as well.");
        return new GesResolvedField { Name = field.Name, Unlabeled = field.Unlabeled, Alternatives = alternatives, Marker = field.Marker, Offset = field.Offset };
    }

    private GesResolvedType[] ResolveUnion(GesSchemaTypeSyntax[] alternatives, Dictionary<string, GesResolvedDeclaration> all)
    {
        var result = new GesResolvedType[alternatives.Length];
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < result.Length; index++)
        {
            var alternative = alternatives[index];
            result[index] = ResolveType(alternative, all);
            if (alternatives.Length == 1) continue;
            // The type name decides: a unit does not distinguish alternatives, and a named tag set is a Tag.
            var name = result[index].Kind == GesSchemaTypeKind.Reference ? ":" + alternative.Name : result[index].Kind.ToString();
            if (!seen.Add(name)) throw Error(Code.InvalidUnion, alternative.Offset, "A union repeats a type name; a unit does not distinguish alternatives.");
            if (alternative.Constraints.Delta is not null) throw Error(Code.InvalidUnion, alternative.Offset, "A union alternative has no delta within.");
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
        if (type.Tags is { } tags)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var tag in tags)
                if (!seen.Add(tag)) throw Error(Code.DuplicateName, type.Offset, $"Tag '#{tag}' is listed twice.");
            if (type.Kind == GesSchemaTypeKind.Flags && tags.Length > GesSchemaSet.MaximumFlagCount)
                throw Error(Code.TooWide, type.Offset, $"Flags have at most {GesSchemaSet.MaximumFlagCount} members.");
        }

        if (type.Max is <= 0) throw Error(Code.InvalidConstraint, type.Offset, "A maximum is positive.");
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
            Max = type.Max,
            Element = type.Element is { } element ? ResolveUnion(element, all) : null,
            Constraints = constraints,
            Axes = axes,
            Reference = reference,
            ReferenceName = type.Name,
            Offset = type.Offset
        };
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
            Lower = ResolveValue(constraints.Lower, unit, percentage),
            Upper = ResolveValue(constraints.Upper, unit, percentage),
            Step = ResolveValue(constraints.Step, unit, percentage),
            Delta = ResolveValue(constraints.Delta, unit, percentage)
        };
        GesSchemaBinder.CheckLiterals(this, resolved);
        return resolved;
    }

    private GesResolvedValue? ResolveValue(GesSchemaValueSyntax? value, GesQuantityUnit unit, bool percentage)
    {
        if (value is null) return null;
        if (value.Parameter.Length > 0)
        {
            if (!Parameters.TryGetValue(value.Parameter, out var parameter)) throw Error(Code.UnknownName, value.Offset, $"Parameter '{value.Parameter}' is not declared in this file.");
            if (parameter.Unit != unit || (parameter.Kind == GesSchemaTypeKind.Percentage) != percentage) throw Error(Code.UnitMismatch, value.Offset, $"Parameter '{value.Parameter}' has another kind or unit than its field.");
            return new GesResolvedValue { Parameter = value.Parameter, Offset = value.Offset };
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
            foreach (var type in field.Alternatives) Reach(type, declaration.Scope, seen, declarations, parameters);
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

    private static void Write(GesResolvedDeclaration declaration, StringBuilder builder)
    {
        switch (declaration.Kind)
        {
            case GesSchemaKind.Schema:
                builder.Append("schema ").Append(declaration.Name).Append('\n');
                foreach (var field in declaration.Fields) WriteField("field ", field, builder);
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
        builder.Append('\n');
    }

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
                builder.Append(type.Kind == GesSchemaTypeKind.Point ? "Point(" : "Vector(");
                builder.Append(type.Unit == GesQuantityUnit.UnitNone ? "none" : type.Unit.ToTypeName()).Append(')');
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
