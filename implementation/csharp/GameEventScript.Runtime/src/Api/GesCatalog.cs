// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Text;
using GameEventScript.Runtime;
using GameEventScript.Runtime.Values;
using GameEventScript.Runtime.Vm;
using GameEventScript.Syntax;

namespace GameEventScript.Api;

/// <summary>A named root of a catalog and the root-relative path of its root file (specs/Documents.md#reading-a-catalog).</summary>
/// <param name="Name">The root's name, which the host's file source and every origin carry.</param>
/// <param name="Path">The path of the root file, relative to the root.</param>
public sealed record GesCatalogRoot(string Name, string Path);

/// <summary>
/// The files of a catalog's roots. GES reads no files itself: it asks for the text of each file a catalog reaches.
/// </summary>
public interface IGesCatalogFiles
{
    /// <summary>Returns the text of a file of a root.</summary>
    /// <param name="root">The root's name.</param>
    /// <param name="path">The root-relative path, segments joined by <c>/</c>, without <c>.</c> or <c>..</c> segments.</param>
    /// <returns>The file's text, or <see langword="null"/> when the root has no such file (<c>catalog.importMissing</c>).</returns>
    string? Read(string root, string path);
}

/// <summary>One object of a catalog: its id, its handle, its checked state and where it is declared.</summary>
public sealed class GesCatalogEntry
{
    internal GesCatalogEntry(string id, GesHandle handle, GesValue value, GesSourceLocation origin)
    {
        Id = id;
        Handle = handle;
        Value = value;
        Origin = origin;
    }

    /// <summary>Gets the entry's id: its module name, a <c>.</c> and its key, or only its key at the root.</summary>
    public string Id { get; }

    /// <summary>Gets the entry's handle, which carries the content hash of <see cref="Value"/> for a <c>:ContentHash</c> type.</summary>
    public GesHandle Handle { get; }

    /// <summary>Gets the state: a Record of the state type with the schema's defaults applied, as a resolver's state.</summary>
    public GesValue Value { get; }

    /// <summary>
    /// Gets the position of the entry's key: <see cref="GesSourceLocation.ModuleName"/> is the root, <see cref="GesSourceLocation.SourceName"/> the
    /// root-relative path of the file, with line and column.
    /// </summary>
    public GesSourceLocation Origin { get; }
}

/// <summary>One error of reading a catalog (specs/Documents.md#reading-a-catalog).</summary>
public sealed class GesCatalogDiagnostic
{
    internal GesCatalogDiagnostic(string code, string message, GesSourceLocation location, string fieldPath, GesSourceLocation? other, string? suggestion)
    {
        Code = code;
        Message = message;
        Location = location;
        FieldPath = fieldPath;
        Other = other;
        Suggestion = suggestion;
    }

    /// <summary>Gets the stable code, a <c>catalog.*</c> or <c>document.*</c> code.</summary>
    public string Code { get; }

    /// <summary>Gets a message for people, which is not part of the contract.</summary>
    public string Message { get; }

    /// <summary>
    /// Gets the position: <see cref="GesSourceLocation.ModuleName"/> is the root, <see cref="GesSourceLocation.SourceName"/> the root-relative path,
    /// and line and column, which a missing root file has none of.
    /// </summary>
    public GesSourceLocation Location { get; }

    /// <summary>Gets the field path of a schema error, such as <c>weapons[].range</c>; otherwise empty.</summary>
    public string FieldPath { get; }

    /// <summary>Gets the second position of an error, the first key of a <c>catalog.duplicateId</c>; otherwise <see langword="null"/>.</summary>
    public GesSourceLocation? Other { get; }

    /// <summary>Gets the suggested id of a <c>catalog.unknownReference</c>, or <see langword="null"/> when no id is close.</summary>
    public string? Suggestion { get; }

    /// <summary>Returns the position, the code and the message.</summary>
    /// <returns>The diagnostic as text.</returns>
    public override string ToString() => $"{Location}: {Code}: {Message}";
}

/// <summary>A catalog could not be read; <see cref="Diagnostics"/> holds every error found.</summary>
public sealed class GesCatalogException : Exception
{
    /// <summary>The code of a file the host does not have.</summary>
    public const string ImportMissing = "catalog.importMissing";

    /// <summary>The code of an import path that steps above its root.</summary>
    public const string ImportOutsideRoot = "catalog.importOutsideRoot";

    /// <summary>The code of an import of a file on its own import chain.</summary>
    public const string ImportCycle = "catalog.importCycle";

    /// <summary>The code of an imported file of the wrong kind.</summary>
    public const string ImportKind = "catalog.importKind";

    /// <summary>The code of an imported file of another handle or state type.</summary>
    public const string ImportType = "catalog.importType";

    /// <summary>The code of an id longer than the id's <c>max</c>.</summary>
    public const string InvalidId = "catalog.invalidId";

    /// <summary>The code of a repeated id.</summary>
    public const string DuplicateId = "catalog.duplicateId";

    /// <summary>The code of a reference or handle that names no entry of a known catalog.</summary>
    public const string UnknownReference = "catalog.unknownReference";

    /// <summary>The code of a one-segment reference that names both a module entry and a root entry.</summary>
    public const string AmbiguousReference = "catalog.ambiguousReference";

    /// <summary>The code of a pin to a type without content hashes or without a known catalog.</summary>
    public const string InvalidPin = "catalog.invalidPin";

    /// <summary>The code of pinned references that form a cycle.</summary>
    public const string DependencyCycle = "catalog.dependencyCycle";

    internal GesCatalogException(IReadOnlyList<GesCatalogDiagnostic> diagnostics)
        : base(diagnostics.Count == 1 ? diagnostics[0].ToString() : $"{diagnostics[0]} (and {diagnostics.Count - 1} more errors)")
        => Diagnostics = diagnostics;

    /// <summary>Gets every error, ordered by file in reading order and by position within a file.</summary>
    public IReadOnlyList<GesCatalogDiagnostic> Diagnostics { get; }
}

/// <summary>
/// The entries of one handle type read from catalog documents (specs/Documents.md#catalogs): every object under a stable
/// Text id, with references between entries replaced by handles and every state checked against the state schema. A catalog
/// is the handle resolver of its type (specs/Documents.md#catalogs-as-resolvers).
/// </summary>
public sealed class GesCatalog : IGesHandleResolver
{
    private readonly GesCatalogEntry[] _entries;
    private readonly GesHandle[] _sources;

    private GesCatalog(GesHandleType type, GesCatalogEntry[] entries, GesHandle[] sources)
    {
        Type = type;
        _entries = entries;
        _sources = sources;
    }

    /// <summary>Gets the handle type of every entry.</summary>
    public GesHandleType Type { get; }

    /// <summary>
    /// Gets the root handles a catalog read from handles was read from, each with the version it was read at, in the given order;
    /// empty for a catalog read from named roots.
    /// </summary>
    public IReadOnlyList<GesHandle> Sources => _sources;

    /// <summary>Gets every entry, sorted by id in ordinal order.</summary>
    public IReadOnlyList<GesCatalogEntry> Entries => _entries;

    /// <summary>Gets the entry with an id.</summary>
    /// <param name="id">The id.</param>
    /// <returns>The entry, or <see langword="null"/> when the catalog has none with that id.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="id"/> is <see langword="null"/>.</exception>
    public GesCatalogEntry? this[string id]
    {
        get
        {
            if (id is null) throw new ArgumentNullException(nameof(id));
            var index = LowerBound(id);
            return index < _entries.Length && string.Equals(_entries[index].Id, id, StringComparison.Ordinal) ? _entries[index] : null;
        }
    }

    /// <summary>Returns the entries of a module: those whose id starts with the module name and <c>.</c>, sorted by id.</summary>
    /// <param name="name">The module name, such as <c>drone</c>.</param>
    /// <returns>The module's entries; empty when it has none.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is <see langword="null"/>.</exception>
    public IReadOnlyList<GesCatalogEntry> Module(string name)
    {
        if (name is null) throw new ArgumentNullException(nameof(name));
        var prefix = name + ".";
        var start = LowerBound(prefix);
        var end = start;
        while (end < _entries.Length && _entries[end].Id.StartsWith(prefix, StringComparison.Ordinal)) end++;
        var result = new GesCatalogEntry[end - start];
        Array.Copy(_entries, start, result, 0, result.Length);
        return result;
    }

    /// <summary>
    /// Returns the handles of every entry through a projection (specs/Schemas.md#catalogs), sorted by id, each carrying the
    /// projection and its version: the content hash of the projected state for a <c>:ContentHash</c> type, the entry's version
    /// otherwise. The list is computed on each call.
    /// </summary>
    /// <param name="projection">A projection of the catalog's state, one of <see cref="GesHandleType.Projections"/>.</param>
    /// <returns>The projected handles, as a script receives them as <c>@Name(:P)~</c>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="projection"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="projection"/> is no projection of the catalog's state.</exception>
    public IReadOnlyList<GesHandle> Index(GesSchema projection)
    {
        if (projection is null) throw new ArgumentNullException(nameof(projection));
        if (!projection.Declaration.Narrows(Type.Resolved.State))
            throw new ArgumentException($"':{projection.Name}' is no projection of '@{Type.Name}'.", nameof(projection));
        var result = new GesHandle[_entries.Length];
        var hashed = Type.Version == GesHandleVersionKind.ContentHash;
        Span<byte> bytes = stackalloc byte[GesHash32.Length];
        for (var index = 0; index < result.Length; index++)
        {
            var entry = _entries[index];
            var version = entry.Handle.Version;
            if (hashed)
            {
                GesSchemaValues.ProjectedHash(Type, projection.Declaration, GesSchemaValues.Project(projection.Declaration, entry.Value)).CopyTo(bytes);
                version = GesValue.GesBytes(bytes);
            }

            result[index] = entry.Handle.WithView(version, projection.Name);
        }

        return new GesReadOnlyArray<GesHandle>(result);
    }

    /// <summary>Reads a catalog of a handle type from its roots.</summary>
    /// <param name="type">The handle type, whose id is Text and which is not composite.</param>
    /// <param name="roots">The roots, read in this order.</param>
    /// <param name="files">The source of every file's text.</param>
    /// <param name="dependencies">The catalogs of other types whose handles entries may name or pin; none when <see langword="null"/>.</param>
    /// <returns>The catalog.</returns>
    /// <exception cref="ArgumentNullException">An argument other than <paramref name="dependencies"/>, a root or a dependency is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The type is composite or has no Text id, two roots share a name, or two catalogs share a type.</exception>
    /// <exception cref="GesCatalogException">The catalog has errors; the exception lists every one.</exception>
    public static GesCatalog Read(GesHandleType type, IReadOnlyList<GesCatalogRoot> roots, IGesCatalogFiles files, IReadOnlyList<GesCatalog>? dependencies = null)
    {
        if (roots is null) throw new ArgumentNullException(nameof(roots));
        if (files is null) throw new ArgumentNullException(nameof(files));
        var others = Dependencies(type, dependencies);
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var root in roots)
        {
            if (root is null) throw new ArgumentNullException(nameof(roots));
            if (root.Name is null || root.Path is null) throw new ArgumentNullException(nameof(roots));
            if (!names.Add(root.Name)) throw new ArgumentException($"Two roots are named '{root.Name}'.", nameof(roots));
        }

        return new Reading(type, (root, path) => (files.Read(root, path), false), others).Run(roots, null);
    }

    /// <summary>
    /// Reads a catalog of a handle type from versioned sources (specs/Documents.md#reading-from-handles): each root is a handle of a
    /// catalog file type of <paramref name="type"/> whose part is the path of the root file, and every file is requested from the
    /// resolver as the composite handle of the same root with the file's root-relative path.
    /// </summary>
    /// <param name="type">The handle type, whose id is Text and which is not composite.</param>
    /// <param name="roots">The root handles, read in this order; a root without a version is pinned by the version of its first answer.</param>
    /// <param name="resolver">The resolver of the catalog file type, which answers each file with its document text.</param>
    /// <param name="dependencies">The catalogs of other types whose handles entries may name or pin; none when <see langword="null"/>.</param>
    /// <returns>The catalog, whose <see cref="Sources"/> are the roots with the versions they were read at.</returns>
    /// <exception cref="ArgumentNullException">An argument other than <paramref name="dependencies"/>, a root or a dependency is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// The type is composite or has no Text id, a root is no handle of a catalog file type of <paramref name="type"/> with a Text part, two
    /// roots are equal, or two catalogs share a type.
    /// </exception>
    /// <exception cref="GesHandleException">
    /// The resolver gave no answer or several, passed an invalid version, or failed to turn a ref into a version
    /// (<c>runtime.handleResolveFailed</c>), or passed another version than the root's (<c>runtime.handleVersionMismatch</c>).
    /// </exception>
    /// <exception cref="GesCatalogException">The catalog has errors; the exception lists every one.</exception>
    public static GesCatalog Read(GesHandleType type, IReadOnlyList<GesHandle> roots, IGesHandleResolver resolver, IReadOnlyList<GesCatalog>? dependencies = null)
    {
        if (roots is null) throw new ArgumentNullException(nameof(roots));
        if (resolver is null) throw new ArgumentNullException(nameof(resolver));
        var others = Dependencies(type, dependencies);
        GesHandleType? fileType = null;
        var sources = new HandleSources(resolver, roots.Count);
        foreach (var root in roots)
        {
            if (root is null) throw new ArgumentNullException(nameof(roots));
            if (!root.IsComposite || root.Part.ValueKind != GesBytecodeTypeKind.Text)
                throw new ArgumentException($"{root} is no handle of a catalog file of @{type.Name} with a Text part.", nameof(roots));
            if (fileType is null || fileType.Name != root.TypeName)
            {
                fileType = type.FileType(root.TypeName);
                if (fileType is null)
                    throw new ArgumentException($"@{root.TypeName} is no catalog file type of @{type.Name}.", nameof(roots));
            }

            if (!sources.Add(root, fileType)) throw new ArgumentException($"The root {root} is given twice.", nameof(roots));
        }

        var names = new GesCatalogRoot[roots.Count];
        for (var index = 0; index < names.Length; index++) names[index] = new GesCatalogRoot(roots[index].ToString(), roots[index].Part.AsText());
        return new Reading(type, sources.Read, others).Run(names, sources);
    }

    /// <summary>Answers a handle of a known id with its entry's state and, for a <c>:ContentHash</c> type, its content hash; any other as missing.</summary>
    /// <param name="call">The call holding the handle.</param>
    public void Resolve(GesHandleResolveCall call)
    {
        if (call is null) throw new ArgumentNullException(nameof(call));
        var handle = call.Handle;
        if (handle.IsComposite || handle.Id.ValueKind != GesBytecodeTypeKind.Text || this[handle.Id.AsText()] is not { } entry)
        {
            call.SetMissing();
            return;
        }

        if (Type.Version == GesHandleVersionKind.ContentHash) call.SetState(entry.Value, entry.Handle.Version);
        else call.SetState(entry.Value);
    }

    private static Dictionary<string, GesCatalog> Dependencies(GesHandleType type, IReadOnlyList<GesCatalog>? dependencies)
    {
        if (type is null) throw new ArgumentNullException(nameof(type));
        if (type.Root is not null) throw new ArgumentException($"@{type.Name} is composite; a catalog needs a type of its own.", nameof(type));
        if (type.Id is not { IsText: true }) throw new ArgumentException($"@{type.Name} has no Text id.", nameof(type));
        var others = new Dictionary<string, GesCatalog>(StringComparer.Ordinal);
        foreach (var dependency in dependencies ?? [])
        {
            if (dependency is null) throw new ArgumentNullException(nameof(dependencies));
            if (dependency.Type.Name == type.Name || !others.TryAdd(dependency.Type.Name, dependency))
                throw new ArgumentException($"More than one catalog of @{dependency.Type.Name}.", nameof(dependencies));
        }

        return others;
    }

    private int LowerBound(string id)
    {
        int low = 0, high = _entries.Length;
        while (low < high)
        {
            var middle = (low + high) >>> 1;
            if (string.CompareOrdinal(_entries[middle].Id, id) < 0) low = middle + 1;
            else high = middle;
        }

        return low;
    }

    // A file a catalog reached, numbered in reading order; a missing root file has no text.
    private sealed class SourceFile(int index, string root, string path, string? text)
    {
        internal int Index { get; } = index;

        internal string Root { get; } = root;

        internal string Path { get; } = path;

        internal string? Text { get; } = text;

        internal GesSourceLocation At(int offset)
        {
            if (Text is null || offset < 0) return new GesSourceLocation(Path, ModuleName: Root);
            var (line, column) = GesDocumentSyntax.Location(Text, offset);
            return new GesSourceLocation(Path, line, column, ModuleName: Root);
        }
    }

    // An entry before its check: the key in its catalog file and the value, written there or in an imported state document.
    private sealed class Pending(SourceFile keyFile, GesCatalogItem item, SourceFile valueFile, GesDocumentNode value)
    {
        internal SourceFile KeyFile { get; } = keyFile;

        internal GesCatalogItem Item { get; } = item;

        internal SourceFile ValueFile { get; } = valueFile;

        internal GesDocumentNode Value { get; } = value;

        internal string Id { get; } = item.Module.Length == 0 ? item.Key : item.Module + "." + item.Key;

        // The resolved references and handles of the value, in reading order.
        internal List<Link> Links { get; } = [];

        internal bool Broken { get; set; }

        // 0 unvisited, 1 on the current pin chain, 2 checked, 3 failed or skipped.
        internal int State { get; set; }

        internal GesCatalogEntry? Entry { get; set; }
    }

    // A reference or complete handle with its target: an own entry or one of a dependency. A key replaces the label at
    // `KeyIndex` of `Node`; otherwise `Node` itself is replaced.
    private sealed class Link(GesDocumentNode node, int keyIndex, int offset, bool pinned, Pending? own, GesCatalogEntry? other)
    {
        internal GesDocumentNode Node { get; } = node;

        internal int KeyIndex { get; } = keyIndex;

        internal int Offset { get; } = offset;

        internal bool Pinned { get; } = pinned;

        internal Pending? Own { get; } = own;

        internal GesCatalogEntry? Other { get; } = other;
    }

    // The text of a root's file, or null when the root has none; State is true for a state answer instead of a document.
    private delegate (string? Text, bool State) Fetch(string root, string path);

    // The roots of a catalog read from handles, by the text form of each root handle, with the version each is read at.
    private sealed class HandleSources(IGesHandleResolver resolver, int capacity)
    {
        private readonly List<Source> _ordered = new(capacity);
        private readonly Dictionary<string, Source> _byName = new(StringComparer.Ordinal);
        private readonly GesHandleResolveCall _call = new();

        internal bool Add(GesHandle root, GesHandleType fileType)
        {
            var source = new Source(root, fileType);
            if (!_byName.TryAdd(root.ToString(), source)) return false;
            _ordered.Add(source);
            return true;
        }

        // The roots with the versions they were read at, in the given order.
        internal GesHandle[] Read()
        {
            var result = new GesHandle[_ordered.Count];
            for (var index = 0; index < result.Length; index++)
            {
                var source = _ordered[index];
                result[index] = source.Version.IsNothing ? source.Handle : source.Handle.WithVersion(source.Version);
            }

            return result;
        }

        internal (string? Text, bool State) Read(string root, string path)
        {
            var source = _byName[root];
            var fileType = source.FileType;
            if (!source.Prepared)
            {
                source.Prepared = true;
                source.Version = source.Handle.Version;
                if (!source.Handle.Ref.IsNothing)
                {
                    var version = resolver.ResolveRef(source.Handle);
                    if (version.IsNothing) source.UnknownRef = true;
                    else if (!GesHandleStates.IsVersionOf(fileType.Version, version))
                        throw new GesHandleException(GesDiagnosticCodes.RuntimeHandleResolveFailed, root, $"The resolver of @{fileType.Name} returned no valid version.");
                    else source.Version = version;
                }
            }

            if (source.UnknownRef || Encoding.UTF8.GetByteCount(path) > fileType.Part!.Value.MaxLength) return (null, false);
            var handle = GesHandle.CreateComposite(fileType.Name, GesHandle.Create(fileType.Root!.Name, source.Handle.Id, source.Version), GesValue.GesText(path));
            GesHandleResolveCall.AnswerKind answer;
            string? text;
            GesValue passed;
            _call.Begin(handle, fileType, null, 0);
            try
            {
                resolver.Resolve(_call);
                answer = _call.Answer;
                text = _call.Text;
                passed = _call.Version;
            }
            finally
            {
                _call.End();
            }

            var symbol = handle.ToString();
            if (answer is GesHandleResolveCall.AnswerKind.None or GesHandleResolveCall.AnswerKind.Several)
                throw new GesHandleException(GesDiagnosticCodes.RuntimeHandleResolveFailed, symbol, $"The resolver of @{fileType.Name} must give exactly one answer.");
            if (!passed.IsNothing)
            {
                if (!GesHandleStates.IsVersionOf(fileType.Version, passed))
                    throw new GesHandleException(GesDiagnosticCodes.RuntimeHandleResolveFailed, symbol, $"The resolver of @{fileType.Name} passed no valid version of its type.");
                if (source.Version.IsNothing) source.Version = passed;
                else if (!GesHandleStates.VersionEquals(source.Version, passed))
                    throw new GesHandleException(GesDiagnosticCodes.RuntimeHandleVersionMismatch, symbol, $"The resolver of @{fileType.Name} delivered another version of {root}.");
            }

            return answer switch
            {
                GesHandleResolveCall.AnswerKind.Document => (text, false),
                GesHandleResolveCall.AnswerKind.State => (null, true),
                _ => (null, false),
            };
        }

        private sealed class Source(GesHandle handle, GesHandleType fileType)
        {
            internal GesHandle Handle { get; } = handle;

            internal GesHandleType FileType { get; } = fileType;

            internal bool Prepared { get; set; }

            internal bool UnknownRef { get; set; }

            internal GesValue Version { get; set; }
        }
    }

    private sealed class Reading(GesHandleType type, Fetch files, Dictionary<string, GesCatalog> dependencies)
    {
        private readonly List<(SourceFile File, int Offset, GesCatalogDiagnostic Diagnostic)> _diagnostics = [];
        private readonly List<SourceFile> _files = [];
        private readonly HashSet<string> _visited = new(StringComparer.Ordinal);
        private readonly HashSet<string> _chain = new(StringComparer.Ordinal);
        private readonly List<Pending> _pending = [];
        private readonly Dictionary<string, Pending> _ids = new(StringComparer.Ordinal);
        private string[] _sortedIds = [];

        internal GesCatalog Run(IReadOnlyList<GesCatalogRoot> roots, HandleSources? sources)
        {
            foreach (var root in roots)
            {
                var path = Combine("", root.Path);
                if (path is null)
                {
                    var file = AddFile(root.Name, root.Path, null);
                    Report(file, -1, GesCatalogException.ImportOutsideRoot, $"The root file path '{root.Path}' steps above the root.");
                    continue;
                }

                ReadCatalogFile(root.Name, path, null, -1);
            }

            if (_diagnostics.Count == 0) CheckIds();
            if (_diagnostics.Count == 0) CheckEntries();
            if (_diagnostics.Count > 0) throw new GesCatalogException(Sorted());
            var entries = new GesCatalogEntry[_pending.Count];
            for (var index = 0; index < entries.Length; index++) entries[index] = _pending[index].Entry!;
            Array.Sort(entries, (left, right) => string.CompareOrdinal(left.Id, right.Id));
            return new GesCatalog(type, entries, sources?.Read() ?? []);
        }

        // Step 1: a catalog file and, at each import, the imported file.
        private void ReadCatalogFile(string root, string path, SourceFile? importer, int importOffset)
        {
            var key = root + "\n" + path;
            if (_chain.Contains(key))
            {
                Report(importer!, importOffset, GesCatalogException.ImportCycle, $"'{path}' imports itself through its import chain.");
                return;
            }

            if (!_visited.Add(key)) return;
            var (text, state) = files(root, path);
            if (state)
            {
                if (importer is null) Report(AddFile(root, path, null), -1, GesCatalogException.ImportKind, $"'{path}' is an object's state, not a catalog file.");
                else Report(importer, importOffset, GesCatalogException.ImportKind, $"'{path}' is an object's state, not a catalog file.");
                return;
            }

            if (text is null)
            {
                if (importer is null) Report(AddFile(root, path, null), -1, GesCatalogException.ImportMissing, $"The root '{root}' has no file '{path}'.");
                else Report(importer, importOffset, GesCatalogException.ImportMissing, $"The root '{root}' has no file '{path}'.");
                return;
            }

            var file = AddFile(root, path, text);
            var syntax = GesDocumentReader.ReadCatalog(text);
            if (!syntax.IsCatalog)
            {
                if (importer is null) Report(file, syntax.HeaderOffset, GesCatalogException.ImportKind, $"'{path}' is no catalog file.");
                else Report(importer, importOffset, GesCatalogException.ImportKind, $"'{path}' is no catalog file.");
                return;
            }

            if (syntax.Diagnostic is { } diagnostic)
            {
                ReportDocument(file, diagnostic);
                return;
            }

            if (syntax.TypeName != type.Name)
            {
                var message = $"'{path}' is a catalog of @{syntax.TypeName}, not of @{type.Name}.";
                if (importer is null) Report(file, syntax.HeaderOffset, GesCatalogException.ImportType, message);
                else Report(importer, importOffset, GesCatalogException.ImportType, message);
                return;
            }

            _chain.Add(key);
            foreach (var item in syntax.Items)
            {
                switch (item.Kind)
                {
                    case GesCatalogItemKind.Import:
                        if (Imported(file, item) is { } imported) ReadCatalogFile(root, imported, file, item.PathOffset);
                        break;
                    case GesCatalogItemKind.Entry:
                        _pending.Add(new Pending(file, item, file, item.Value!));
                        break;
                    default:
                        ReadStateDocument(file, item);
                        break;
                }
            }

            _chain.Remove(key);
        }

        // The state document of `key: import 'path'`.
        private void ReadStateDocument(SourceFile file, GesCatalogItem item)
        {
            if (Imported(file, item) is not { } path) return;
            var (text, state) = files(file.Root, path);
            if (state)
            {
                Report(file, item.PathOffset, GesCatalogException.ImportKind, $"'{path}' is an object's state, not a state document.");
                return;
            }

            if (text is null)
            {
                Report(file, item.PathOffset, GesCatalogException.ImportMissing, $"The root '{file.Root}' has no file '{path}'.");
                return;
            }

            var syntax = GesDocumentReader.Read(text, references: true);
            if (GesDocumentReader.ReadCatalog(text).IsCatalog)
            {
                Report(file, item.PathOffset, GesCatalogException.ImportKind, $"'{path}' is a catalog file, not a state document.");
                return;
            }

            var document = AddFile(file.Root, path, text);
            if (syntax.Diagnostic is { } diagnostic)
            {
                ReportDocument(document, diagnostic);
                return;
            }

            if (syntax.Schema != GesDocumentSchemaSyntax.Type)
            {
                Report(file, item.PathOffset, GesCatalogException.ImportKind, $"'{path}' names no state type in a :Schema header.");
                return;
            }

            if (syntax.SchemaName != type.StateName)
            {
                Report(file, item.PathOffset, GesCatalogException.ImportType, $"'{path}' is a :{syntax.SchemaName} document, not a :{type.StateName} state.");
                return;
            }

            _pending.Add(new Pending(file, item, document, syntax.Root!));
        }

        private string? Imported(SourceFile file, GesCatalogItem item)
        {
            if (Combine(file.Path, item.Path) is { } path) return path;
            Report(file, item.PathOffset, GesCatalogException.ImportOutsideRoot, $"The import path '{item.Path}' steps above the root.");
            return null;
        }

        // Step 2: every id fits the type's id and occurs once.
        private void CheckIds()
        {
            foreach (var pending in _pending)
            {
                if (Encoding.UTF8.GetByteCount(pending.Id) > type.Id!.Value.MaxLength)
                {
                    Report(pending.KeyFile, pending.Item.KeyOffset, GesCatalogException.InvalidId, $"The id '{pending.Id}' is longer than {type.Id.Value.MaxLength} bytes.");
                    continue;
                }

                if (_ids.TryGetValue(pending.Id, out var first))
                {
                    var location = pending.KeyFile.At(pending.Item.KeyOffset);
                    _diagnostics.Add((pending.KeyFile, pending.Item.KeyOffset, new GesCatalogDiagnostic(GesCatalogException.DuplicateId,
                        $"The id '{pending.Id}' is already declared at {first.KeyFile.At(first.Item.KeyOffset)}.", location, "", first.KeyFile.At(first.Item.KeyOffset), null)));
                    continue;
                }

                _ids.Add(pending.Id, pending);
            }
        }

        // Step 3: references resolve, pinned entries are checked before the entries that pin them, every state is checked.
        private void CheckEntries()
        {
            _sortedIds = new string[_ids.Count];
            _ids.Keys.CopyTo(_sortedIds, 0);
            Array.Sort(_sortedIds, StringComparer.Ordinal);
            foreach (var pending in _pending) Collect(pending, pending.Value);
            var stack = new List<Pending>();
            foreach (var pending in _pending) Visit(pending, stack);
        }

        // Records the links of a value and reports the references that name nothing.
        private void Collect(Pending pending, GesDocumentNode node)
        {
            if (node.Kind == GesDocumentNodeKind.Reference) Resolve(pending, node, -1, node.Name, node.Start, node.Pinned);
            else if (node.Kind == GesDocumentNodeKind.Handle) ResolveHandle(pending, node, -1, node.Name, node.Start, node.Pinned);
            for (var index = 0; index < node.Children.Length; index++)
            {
                if (node.Kind == GesDocumentNodeKind.Map && node.IsHandleKey(index))
                {
                    var label = node.Labels[index];
                    var offset = node.KeyOffsets.Length > index ? node.KeyOffsets[index] : node.Start;
                    if (node.IsReferenceKey(index)) Resolve(pending, node, index, label.Substring(1), offset, false);
                    else ResolveHandle(pending, node, index, label, offset, false);
                }

                Collect(pending, node.Children[index]);
            }
        }

        private void Resolve(Pending pending, GesDocumentNode node, int keyIndex, string name, int offset, bool pinned)
        {
            Pending? target;
            if (name.IndexOf('.') >= 0 || pending.Item.Module.Length == 0)
            {
                _ids.TryGetValue(name, out target);
            }
            else
            {
                _ids.TryGetValue(pending.Item.Module + "." + name, out var inModule);
                _ids.TryGetValue(name, out var atRoot);
                if (inModule is not null && atRoot is not null)
                {
                    Fail(pending, offset, GesCatalogException.AmbiguousReference, $"@{name} names both {inModule.Id} and the root entry {atRoot.Id}.");
                    return;
                }

                target = inModule ?? atRoot;
            }

            if (target is null)
            {
                FailUnknown(pending, offset, "@" + name, name, pending.Item.Module, _sortedIds);
                return;
            }

            if (pinned && type.Version != GesHandleVersionKind.ContentHash)
            {
                Fail(pending, offset, GesCatalogException.InvalidPin, $"@{type.Name} has no content hash to pin @{name} to.");
                return;
            }

            pending.Links.Add(new Link(node, keyIndex, offset, pinned, target, null));
        }

        // A complete handle names an entry of its type's catalog when the catalog is known.
        private void ResolveHandle(Pending pending, GesDocumentNode node, int keyIndex, string text, int offset, bool pinned)
        {
            if (!GesHandle.TryRead(text, out var handle) || handle!.IsComposite)
            {
                if (pinned) Fail(pending, offset, GesCatalogException.InvalidPin, $"{text} names no catalog entry to pin to.");
                return;
            }

            var id = handle.Id.ValueKind == GesBytecodeTypeKind.Text ? handle.Id.AsText() : null;
            if (handle.TypeName == type.Name)
            {
                if (id is null || !_ids.TryGetValue(id, out var target))
                {
                    FailUnknown(pending, offset, text, id ?? "", "", _sortedIds);
                    return;
                }

                if (pinned && type.Version != GesHandleVersionKind.ContentHash)
                {
                    Fail(pending, offset, GesCatalogException.InvalidPin, $"@{type.Name} has no content hash to pin {text} to.");
                    return;
                }

                if (pinned) pending.Links.Add(new Link(node, keyIndex, offset, true, target, null));
                return;
            }

            if (!dependencies.TryGetValue(handle.TypeName, out var catalog))
            {
                if (pinned) Fail(pending, offset, GesCatalogException.InvalidPin, $"No catalog of @{handle.TypeName} is known to pin {text} to.");
                return;
            }

            if ((id is null ? null : catalog[id]) is not { } entry)
            {
                var ids = new string[catalog._entries.Length];
                for (var index = 0; index < ids.Length; index++) ids[index] = catalog._entries[index].Id;
                FailUnknown(pending, offset, text, id ?? "", "", ids);
                return;
            }

            if (pinned && catalog.Type.Version != GesHandleVersionKind.ContentHash)
            {
                Fail(pending, offset, GesCatalogException.InvalidPin, $"@{handle.TypeName} has no content hash to pin {text} to.");
                return;
            }

            if (pinned) pending.Links.Add(new Link(node, keyIndex, offset, true, null, entry));
        }

        private void Visit(Pending pending, List<Pending> stack)
        {
            if (pending.State != 0) return;
            pending.State = 1;
            stack.Add(pending);
            foreach (var link in pending.Links)
            {
                if (!link.Pinned || link.Own is not { } target) continue;
                if (target.State == 1)
                {
                    Report(pending.ValueFile, link.Offset, GesCatalogException.DependencyCycle, $"Pinning @{target.Id} closes a cycle of pinned references.");
                    pending.Broken = true;
                    break;
                }

                Visit(target, stack);
                if (target.State == 3)
                {
                    pending.Broken = true;
                    break;
                }
            }

            stack.RemoveAt(stack.Count - 1);
            pending.State = !pending.Broken && Check(pending) ? 2 : 3;
        }

        // Replaces the links by handles, builds the state and checks it against the state schema.
        private bool Check(Pending pending)
        {
            var replacements = new Dictionary<GesDocumentNode, string>();
            var keys = new Dictionary<(GesDocumentNode, int), string>();
            foreach (var link in pending.Links)
            {
                var handle = link.Other?.Handle ?? (link.Pinned ? link.Own!.Entry!.Handle : GesHandle.Create(type.Name, GesValue.GesText(link.Own!.Id)));
                if (link.KeyIndex < 0) replacements[link.Node] = handle.ToString();
                else keys[(link.Node, link.KeyIndex)] = handle.ToString();
            }

            var root = Replace(pending.Value, replacements, keys);
            var text = pending.ValueFile.Text!;
            var declaration = type.Resolved.State;
            try
            {
                var value = GesDocumentReading.Entries(text, root, type.StateName);
                if (GesSchemaDocumentCheck.Failing(text, root, declaration, null, out var chosen, pending.Value.Start) is { } failure)
                {
                    ReportException(pending.ValueFile, failure);
                    return false;
                }

                var state = GesSchemaValues.NormalizeState(declaration, GesSchemaValues.DocumentShapes(declaration, value, chosen), "", type.Declarations);
                var id = GesValue.GesText(pending.Id);
                GesHandle handle;
                if (type.Version == GesHandleVersionKind.ContentHash)
                {
                    var bytes = new byte[GesHash32.Length];
                    GesSchemaValues.ContentHash(type, state).CopyTo(bytes);
                    handle = GesHandle.Create(type.Name, id, GesValue.GesBytes(bytes));
                }
                else
                {
                    handle = GesHandle.Create(type.Name, id);
                }

                pending.Entry = new GesCatalogEntry(pending.Id, handle, state, pending.KeyFile.At(pending.Item.KeyOffset));
                return true;
            }
            catch (GesDocumentException exception)
            {
                ReportException(pending.ValueFile, exception);
                return false;
            }
            catch (GesWireException exception)
            {
                var location = pending.ValueFile.At(pending.Value.Start);
                _diagnostics.Add((pending.ValueFile, pending.Value.Start, new GesCatalogDiagnostic(exception.Code, exception.Message, location, exception.Path, null, null)));
                return false;
            }
        }

        private static GesDocumentNode Replace(GesDocumentNode node, Dictionary<GesDocumentNode, string> replacements, Dictionary<(GesDocumentNode, int), string> keys)
        {
            if (replacements.TryGetValue(node, out var text))
            {
                var handle = GesDocumentNode.Handle(text);
                handle.Start = node.Start;
                return handle;
            }

            if (node.Children.Length == 0) return node;
            string[]? labels = null;
            GesDocumentNode[]? children = null;
            for (var index = 0; index < node.Children.Length; index++)
            {
                if (keys.TryGetValue((node, index), out var key))
                {
                    labels ??= (string[])node.Labels.Clone();
                    labels[index] = key;
                }

                var child = Replace(node.Children[index], replacements, keys);
                if (ReferenceEquals(child, node.Children[index])) continue;
                children ??= (GesDocumentNode[])node.Children.Clone();
                children[index] = child;
            }

            return labels is null && children is null ? node : node.With(labels ?? node.Labels, children ?? node.Children);
        }

        // An unknown reference with the suggestion of specs/Documents.md#references.
        private void FailUnknown(Pending pending, int offset, string written, string id, string module, string[] ids)
        {
            var suggestion = Suggest(id, id.IndexOf('.') < 0 && module.Length > 0 ? module + "." + id : null, ids);
            var message = $"{written} names no entry." + (suggestion is null ? "" : $" Did you mean {Written(written, suggestion)}?");
            pending.Broken = true;
            _diagnostics.Add((pending.ValueFile, offset, new GesCatalogDiagnostic(GesCatalogException.UnknownReference, message, pending.ValueFile.At(offset), "", null, suggestion)));
        }

        // A suggested id in the form of the written reference: with the type when it named one.
        private static string Written(string written, string id)
        {
            var colon = written.IndexOf(':');
            return colon < 0 ? "@" + id : written.Substring(0, colon + 1) + id;
        }

        private static string? Suggest(string id, string? qualified, string[] ids)
        {
            var last = LastSegment(id);
            foreach (var candidate in ids)
                if (LastSegment(candidate) == last) return candidate;
            string? best = null;
            var bestDistance = 4;
            foreach (var candidate in ids)
            {
                var distance = Distance(id, candidate);
                if (qualified is not null) distance = Math.Min(distance, Distance(qualified, candidate));
                if (distance >= bestDistance) continue;
                best = candidate;
                bestDistance = distance;
            }

            return best;
        }

        private static string LastSegment(string id) => id.Substring(id.LastIndexOf('.') + 1);

        // The Levenshtein distance of two ids.
        private static int Distance(string left, string right)
        {
            var previous = new int[right.Length + 1];
            var current = new int[right.Length + 1];
            for (var j = 0; j <= right.Length; j++) previous[j] = j;
            for (var i = 1; i <= left.Length; i++)
            {
                current[0] = i;
                for (var j = 1; j <= right.Length; j++)
                {
                    var substitution = previous[j - 1] + (left[i - 1] == right[j - 1] ? 0 : 1);
                    current[j] = Math.Min(substitution, Math.Min(previous[j], current[j - 1]) + 1);
                }

                (previous, current) = (current, previous);
            }

            return previous[right.Length];
        }

        private void Fail(Pending pending, int offset, string code, string message)
        {
            pending.Broken = true;
            Report(pending.ValueFile, offset, code, message);
        }

        private SourceFile AddFile(string root, string path, string? text)
        {
            var file = new SourceFile(_files.Count, root, path, text);
            _files.Add(file);
            return file;
        }

        private void Report(SourceFile file, int offset, string code, string message)
            => _diagnostics.Add((file, offset, new GesCatalogDiagnostic(code, message, file.At(offset), "", null, null)));

        private void ReportDocument(SourceFile file, GesDocumentDiagnostic diagnostic)
        {
            var (line, column) = GesDocumentSyntax.Location(file.Text!, diagnostic.Start);
            int? limit = diagnostic.LimitName is null ? null : diagnostic.LimitValue;
            var exception = new GesDocumentException(diagnostic.Code, line, column, diagnostic.LimitName, limit);
            _diagnostics.Add((file, diagnostic.Start, new GesCatalogDiagnostic(diagnostic.Code, exception.Message, file.At(diagnostic.Start), "", null, null)));
        }

        // A document error, which carries its line and column; it sorts by them within its file.
        private void ReportException(SourceFile file, GesDocumentException exception)
        {
            var location = new GesSourceLocation(file.Path, exception.Line, exception.Column, ModuleName: file.Root);
            _diagnostics.Add((file, -1, new GesCatalogDiagnostic(exception.Code, exception.Message, location, exception.Path, null, null)));
        }

        private GesCatalogDiagnostic[] Sorted()
        {
            var sorted = _diagnostics.ToArray();
            var order = new int[sorted.Length];
            for (var index = 0; index < order.Length; index++) order[index] = index;
            Array.Sort(order, (left, right) =>
            {
                var a = sorted[left];
                var b = sorted[right];
                var compared = a.File.Index.CompareTo(b.File.Index);
                if (compared != 0) return compared;
                compared = (a.Diagnostic.Location.Line ?? 0).CompareTo(b.Diagnostic.Location.Line ?? 0);
                if (compared != 0) return compared;
                compared = (a.Diagnostic.Location.Column ?? 0).CompareTo(b.Diagnostic.Location.Column ?? 0);
                return compared != 0 ? compared : left.CompareTo(right);
            });
            var result = new GesCatalogDiagnostic[sorted.Length];
            for (var index = 0; index < result.Length; index++) result[index] = sorted[order[index]].Diagnostic;
            return result;
        }
    }

    // A root-relative import path (specs/Documents.md#import-paths), or null when it steps above the root.
    internal static string? Combine(string from, string path)
    {
        var segments = new List<string>();
        if (!path.StartsWith("/", StringComparison.Ordinal))
        {
            var directory = from.LastIndexOf('/');
            if (directory > 0) segments.AddRange(from.Substring(0, directory).Split('/'));
        }

        foreach (var segment in path.Split('/'))
        {
            if (segment.Length == 0 || segment == ".") continue;
            if (segment != "..")
            {
                segments.Add(segment);
                continue;
            }

            if (segments.Count == 0) return null;
            segments.RemoveAt(segments.Count - 1);
        }

        return string.Join("/", segments);
    }
}
