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

/// <summary>A named root of a store and the root-relative path of its root file (specs/Documents.md#reading-a-store).</summary>
/// <param name="Name">The root's name, which the host's file source and every origin carry.</param>
/// <param name="Path">The path of the root file, relative to the root.</param>
public sealed record GesStoreRoot(string Name, string Path);

/// <summary>
/// The files of a store's roots. GES reads no files itself: it asks for the text of each file a store reaches.
/// </summary>
public interface IGesStoreFiles
{
    /// <summary>Returns the text of a file of a root.</summary>
    /// <param name="root">The root's name.</param>
    /// <param name="path">The root-relative path, segments joined by <c>/</c>, without <c>.</c> or <c>..</c> segments.</param>
    /// <returns>The file's text, or <see langword="null"/> when the root has no such file (<c>store.importMissing</c>).</returns>
    string? Read(string root, string path);
}

/// <summary>
/// A root of a store read from versioned sources (specs/Documents.md#reading-from-handles): a root handle, whose id names the
/// source logically, and the path of the root file within it.
/// </summary>
/// <param name="Handle">The root handle, which carries no part, optionally with a version or a ref.</param>
/// <param name="Path">The path of the root file, relative to the root.</param>
public sealed record GesStoreSourceRoot(GesHandle Handle, string Path);

/// <summary>A file a <see cref="IGesStoreSource"/> answers: its text, or missing, and optionally the version of its root it comes from.</summary>
public readonly struct GesStoreFile
{
    /// <summary>Creates an answer.</summary>
    /// <param name="text">The file's text, or <see langword="null"/> when the root has no such file.</param>
    /// <param name="version">The version of the root the file comes from, an integer Number or 20 or 32 Bytes; Nothing for none.</param>
    public GesStoreFile(string? text, GesValue version = default)
    {
        Text = text;
        Version = version;
    }

    /// <summary>Gets the answer of a file the root does not have, without a version.</summary>
    public static GesStoreFile Missing => default;

    /// <summary>Gets the file's text, or <see langword="null"/> when the root has no such file (<c>store.importMissing</c>).</summary>
    public string? Text { get; }

    /// <summary>Gets the version of the root the file comes from, or Nothing.</summary>
    public GesValue Version { get; }
}

/// <summary>
/// The versioned sources of a store's roots, such as repositories at a commit. GES reads no files itself: it asks for the file of
/// each root handle and root-relative path a store reaches.
/// </summary>
public interface IGesStoreSource
{
    /// <summary>Returns a file of a root.</summary>
    /// <param name="root">
    /// The root handle: as given while the root has no known version, a ref included, afterwards with the version of the root's first
    /// answer.
    /// </param>
    /// <param name="path">The root-relative path, segments joined by <c>/</c>, without <c>.</c> or <c>..</c> segments, at most 256 UTF-8 bytes.</param>
    /// <returns>The file, or <see cref="GesStoreFile.Missing"/>; an unknown ref answers missing.</returns>
    GesStoreFile Read(GesHandle root, string path);
}

/// <summary>One object of a store: its id, its handle, its checked state and where it is declared.</summary>
public sealed class GesStoreEntry
{
    internal GesStoreEntry(string id, GesHandle handle, GesValue value, GesSourceLocation origin)
    {
        Id = id;
        Handle = handle;
        Value = value;
        Origin = origin;
    }

    /// <summary>Gets the entry's id: its module name, a <c>.</c> and its key, or only its key at the root.</summary>
    public string Id { get; }

    /// <summary>Gets the entry's handle, which carries the content hash of <see cref="Value"/> for a type <c>versioned by hash(#ges)</c>.</summary>
    public GesHandle Handle { get; }

    /// <summary>Gets the state: a Record of the state type with the schema's defaults applied, as a resolver's state.</summary>
    public GesValue Value { get; }

    /// <summary>
    /// Gets the position of the entry's key: <see cref="GesSourceLocation.ModuleName"/> is the root, <see cref="GesSourceLocation.SourceName"/> the
    /// root-relative path of the file, with line and column.
    /// </summary>
    public GesSourceLocation Origin { get; }
}

/// <summary>One error of reading a store (specs/Documents.md#reading-a-store).</summary>
public sealed class GesStoreDiagnostic
{
    internal GesStoreDiagnostic(string code, string message, GesSourceLocation location, string fieldPath, GesSourceLocation? other, string? suggestion)
    {
        Code = code;
        Message = message;
        Location = location;
        FieldPath = fieldPath;
        Other = other;
        Suggestion = suggestion;
    }

    /// <summary>Gets the stable code, a <c>store.*</c> or <c>document.*</c> code.</summary>
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

    /// <summary>Gets the second position of an error, the first key of a <c>store.duplicateId</c>; otherwise <see langword="null"/>.</summary>
    public GesSourceLocation? Other { get; }

    /// <summary>Gets the suggested id of a <c>store.unknownReference</c>, or <see langword="null"/> when no id is close.</summary>
    public string? Suggestion { get; }

    /// <summary>Returns the position, the code and the message.</summary>
    /// <returns>The diagnostic as text.</returns>
    public override string ToString() => $"{Location}: {Code}: {Message}";
}

/// <summary>A store could not be read; <see cref="Diagnostics"/> holds every error found.</summary>
public sealed class GesStoreException : Exception
{
    /// <summary>The code of a file the host does not have.</summary>
    public const string ImportMissing = "store.importMissing";

    /// <summary>The code of an import path that steps above its root.</summary>
    public const string ImportOutsideRoot = "store.importOutsideRoot";

    /// <summary>The code of an import of a file on its own import chain.</summary>
    public const string ImportCycle = "store.importCycle";

    /// <summary>The code of an imported file of the wrong kind.</summary>
    public const string ImportKind = "store.importKind";

    /// <summary>The code of an imported file of another handle or state type.</summary>
    public const string ImportType = "store.importType";

    /// <summary>The code of an id longer than the id's <c>max</c>.</summary>
    public const string InvalidId = "store.invalidId";

    /// <summary>The code of a repeated id.</summary>
    public const string DuplicateId = "store.duplicateId";

    /// <summary>The code of a reference or handle that names no entry of a known store.</summary>
    public const string UnknownReference = "store.unknownReference";

    /// <summary>The code of a one-segment reference that names both a module entry and a root entry.</summary>
    public const string AmbiguousReference = "store.ambiguousReference";

    /// <summary>The code of a pin to a type without content hashes or without a known store.</summary>
    public const string InvalidPin = "store.invalidPin";

    /// <summary>The code of pinned references that form a cycle.</summary>
    public const string DependencyCycle = "store.dependencyCycle";

    internal GesStoreException(IReadOnlyList<GesStoreDiagnostic> diagnostics)
        : base(diagnostics.Count == 1 ? diagnostics[0].ToString() : $"{diagnostics[0]} (and {diagnostics.Count - 1} more errors)")
        => Diagnostics = diagnostics;

    /// <summary>Gets every error, ordered by file in reading order and by position within a file.</summary>
    public IReadOnlyList<GesStoreDiagnostic> Diagnostics { get; }
}

/// <summary>
/// The entries of one handle type read from store documents (specs/Documents.md#stores): every object under a stable
/// Text id, with references between entries replaced by handles and every state checked against the state schema. A store
/// is the handle resolver of its type (specs/Documents.md#stores-as-resolvers).
/// </summary>
public sealed class GesStore : IGesHandleResolver
{
    private readonly GesStoreEntry[] _entries;
    private readonly GesHandle[] _sources;

    private GesStore(GesHandleType type, GesStoreEntry[] entries, GesHandle[] sources)
    {
        Type = type;
        _entries = entries;
        _sources = sources;
    }

    /// <summary>Gets the handle type of every entry.</summary>
    public GesHandleType Type { get; }

    /// <summary>
    /// Gets the root handles a store read from handles was read from, each with the version it was read at, in the given order;
    /// empty for a store read from named roots.
    /// </summary>
    public IReadOnlyList<GesHandle> Sources => _sources;

    /// <summary>Gets every entry, sorted by id in ordinal order.</summary>
    public IReadOnlyList<GesStoreEntry> Entries => _entries;

    /// <summary>Gets the entry with an id.</summary>
    /// <param name="id">The id.</param>
    /// <returns>The entry, or <see langword="null"/> when the store has none with that id.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="id"/> is <see langword="null"/>.</exception>
    public GesStoreEntry? this[string id]
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
    public IReadOnlyList<GesStoreEntry> Module(string name)
    {
        if (name is null) throw new ArgumentNullException(nameof(name));
        var prefix = name + ".";
        var start = LowerBound(prefix);
        var end = start;
        while (end < _entries.Length && _entries[end].Id.StartsWith(prefix, StringComparison.Ordinal)) end++;
        var result = new GesStoreEntry[end - start];
        Array.Copy(_entries, start, result, 0, result.Length);
        return result;
    }

    /// <summary>
    /// Returns the handles of every entry through a projection (specs/Schemas.md#projections), sorted by id, each carrying the
    /// projection and its version: the content hash of the projected state for a type <c>versioned by hash(#ges)</c>, the entry's version
    /// otherwise. The list is computed on each call.
    /// </summary>
    /// <param name="projection">A projection of the store's state, one of <see cref="GesHandleType.Projections"/>.</param>
    /// <returns>The projected handles, as a script receives them as <c>@Name(:P)~</c>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="projection"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="projection"/> is no projection of the store's state.</exception>
    public IReadOnlyList<GesHandle> Index(GesSchema projection)
    {
        if (projection is null) throw new ArgumentNullException(nameof(projection));
        if (!projection.Declaration.Narrows(Type.Resolved.State!))
            throw new ArgumentException($"':{projection.Name}' is no projection of '@{Type.Name}'.", nameof(projection));
        var result = new GesHandle[_entries.Length];
        var hashed = Type.Version == GesHandleVersionKind.GesHash;
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

    /// <summary>Reads a store of a handle type from its roots.</summary>
    /// <param name="type">The handle type, which has a state and a Text id and no root.</param>
    /// <param name="roots">The roots, read in this order.</param>
    /// <param name="files">The source of every file's text.</param>
    /// <param name="dependencies">The stores of other types whose handles entries may name or pin; none when <see langword="null"/>.</param>
    /// <returns>The store.</returns>
    /// <exception cref="ArgumentNullException">An argument other than <paramref name="dependencies"/>, a root or a dependency is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The type has no state, has a root or has no Text id, two roots share a name, or two stores share a type.</exception>
    /// <exception cref="GesStoreException">The store has errors; the exception lists every one.</exception>
    public static GesStore Read(GesHandleType type, IReadOnlyList<GesStoreRoot> roots, IGesStoreFiles files, IReadOnlyList<GesStore>? dependencies = null)
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

        return new Reading(type, files.Read, others).Run(roots, null);
    }

    /// <summary>
    /// Reads a store of a handle type from versioned sources (specs/Documents.md#reading-from-handles): each root is a root handle and
    /// the path of its root file, and every file is requested from the source by the root handle and the file's root-relative path.
    /// </summary>
    /// <param name="type">The handle type, which has a state and a Text id and no root.</param>
    /// <param name="roots">The roots, read in this order; a root without a version is pinned by the version of its first answer.</param>
    /// <param name="source">The source of every file.</param>
    /// <param name="dependencies">The stores of other types whose handles entries may name or pin; none when <see langword="null"/>.</param>
    /// <returns>The store, whose <see cref="Sources"/> are the root handles with the versions they were read at.</returns>
    /// <exception cref="ArgumentNullException">An argument other than <paramref name="dependencies"/>, a root or a dependency is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// The type has no state, has a root or has no Text id, a root handle carries a part, two roots have equal handles, or two stores
    /// share a type.
    /// </exception>
    /// <exception cref="GesHandleException">
    /// The source passed an invalid version (<c>runtime.handleResolveFailed</c>) or another version than the root's
    /// (<c>runtime.handleVersionMismatch</c>).
    /// </exception>
    /// <exception cref="GesStoreException">The store has errors; the exception lists every one.</exception>
    public static GesStore Read(GesHandleType type, IReadOnlyList<GesStoreSourceRoot> roots, IGesStoreSource source, IReadOnlyList<GesStore>? dependencies = null)
    {
        if (roots is null) throw new ArgumentNullException(nameof(roots));
        if (source is null) throw new ArgumentNullException(nameof(source));
        var others = Dependencies(type, dependencies);
        var sources = new HandleSources(source, roots.Count);
        var names = new GesStoreRoot[roots.Count];
        for (var index = 0; index < names.Length; index++)
        {
            var root = roots[index];
            if (root?.Handle is null || root.Path is null) throw new ArgumentNullException(nameof(roots));
            if (root.Handle.HasPart) throw new ArgumentException($"The root {root.Handle} carries a part.", nameof(roots));
            if (!sources.Add(root.Handle)) throw new ArgumentException($"The root {root.Handle} is given twice.", nameof(roots));
            names[index] = new GesStoreRoot(root.Handle.ToString(), root.Path);
        }

        return new Reading(type, sources.Read, others).Run(names, sources);
    }

    /// <summary>Answers a handle of a known id with its entry's state and, for a type <c>versioned by hash(#ges)</c>, its content hash; any other as missing.</summary>
    /// <param name="call">The call holding the handle.</param>
    public void Resolve(GesHandleResolveCall call)
    {
        if (call is null) throw new ArgumentNullException(nameof(call));
        var handle = call.Handle;
        if (handle.HasPart || handle.Id.ValueKind != GesBytecodeTypeKind.Text || this[handle.Id.AsText()] is not { } entry)
        {
            call.SetMissing();
            return;
        }

        if (Type.Version == GesHandleVersionKind.GesHash) call.SetState(entry.Value, entry.Handle.Version);
        else call.SetState(entry.Value);
    }

    /// <summary>Lists the handle of every entry, sorted by id, without versions; a store's type has no root, so a root lists none.</summary>
    /// <param name="root">Ignored unless set: a store's type has no root, so nothing is attached to or keyed by one.</param>
    /// <returns>The entry handles, or an empty list for a root.</returns>
    public IReadOnlyList<GesHandle>? Handles(GesHandle? root)
    {
        if (root is not null) return Array.Empty<GesHandle>();
        var result = new GesHandle[_entries.Length];
        for (var index = 0; index < result.Length; index++) result[index] = _entries[index].Handle.WithView(default, "");
        return new GesReadOnlyArray<GesHandle>(result);
    }

    private static Dictionary<string, GesStore> Dependencies(GesHandleType type, IReadOnlyList<GesStore>? dependencies)
    {
        if (type is null) throw new ArgumentNullException(nameof(type));
        if (!type.HasState) throw new ArgumentException($"@{type.Name} has no state; a store needs a type with one.", nameof(type));
        if (type.Root is not null) throw new ArgumentException($"@{type.Name} has a root; a store needs a type of its own.", nameof(type));
        if (type.Id is not { IsText: true }) throw new ArgumentException($"@{type.Name} has no Text id.", nameof(type));
        var others = new Dictionary<string, GesStore>(StringComparer.Ordinal);
        foreach (var dependency in dependencies ?? [])
        {
            if (dependency is null) throw new ArgumentNullException(nameof(dependencies));
            if (dependency.Type.Name == type.Name || !others.TryAdd(dependency.Type.Name, dependency))
                throw new ArgumentException($"More than one store of @{dependency.Type.Name}.", nameof(dependencies));
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

    // A file a store reached, numbered in reading order; a missing root file has no text.
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

    // An entry before its check: the key in its store file and the value, written there or in an imported state document.
    private sealed class Pending(SourceFile keyFile, GesStoreItem item, SourceFile valueFile, GesDocumentNode value)
    {
        internal SourceFile KeyFile { get; } = keyFile;

        internal GesStoreItem Item { get; } = item;

        internal SourceFile ValueFile { get; } = valueFile;

        internal GesDocumentNode Value { get; } = value;

        internal string Id { get; } = item.Module.Length == 0 ? item.Key : item.Module + "." + item.Key;

        // The resolved references and handles of the value, in reading order.
        internal List<Link> Links { get; } = [];

        internal bool Broken { get; set; }

        // 0 unvisited, 1 on the current pin chain, 2 checked, 3 failed or skipped.
        internal int State { get; set; }

        internal GesStoreEntry? Entry { get; set; }
    }

    // A reference or complete handle with its target: an own entry or one of a dependency. A key replaces the label at
    // `KeyIndex` of `Node`; otherwise `Node` itself is replaced.
    private sealed class Link(GesDocumentNode node, int keyIndex, int offset, bool pinned, Pending? own, GesStoreEntry? other)
    {
        internal GesDocumentNode Node { get; } = node;

        internal int KeyIndex { get; } = keyIndex;

        internal int Offset { get; } = offset;

        internal bool Pinned { get; } = pinned;

        internal Pending? Own { get; } = own;

        internal GesStoreEntry? Other { get; } = other;
    }

    // The text of a root's file, or null when the root has none.
    private delegate string? Fetch(string root, string path);

    // The roots of a store read from handles, by the text form of each root handle, with the version each is read at.
    private sealed class HandleSources(IGesStoreSource source, int capacity)
    {
        private const int MaxPathLength = 256;
        private readonly List<Source> _ordered = new(capacity);
        private readonly Dictionary<string, Source> _byName = new(StringComparer.Ordinal);

        internal bool Add(GesHandle root)
        {
            var entry = new Source(root);
            if (!_byName.TryAdd(root.ToString(), entry)) return false;
            _ordered.Add(entry);
            return true;
        }

        // The roots with the versions they were read at, in the given order.
        internal GesHandle[] Read()
        {
            var result = new GesHandle[_ordered.Count];
            for (var index = 0; index < result.Length; index++)
            {
                var entry = _ordered[index];
                result[index] = entry.Version.IsNothing ? entry.Handle : entry.Handle.WithVersion(entry.Version);
            }

            return result;
        }

        internal string? Read(string root, string path)
        {
            var entry = _byName[root];
            if (Encoding.UTF8.GetByteCount(path) > MaxPathLength) return null;
            var handle = entry.Version.IsNothing ? entry.Handle : entry.Handle.WithVersion(entry.Version);
            var file = source.Read(handle, path);
            var passed = file.Version;
            if (!passed.IsNothing)
            {
                var symbol = root + "/" + path;
                if (!IsVersion(passed))
                    throw new GesHandleException(GesDiagnosticCodes.RuntimeHandleResolveFailed, symbol, $"The source of {root} passed no valid version.");
                if (entry.Version.IsNothing) entry.Version = passed;
                else if (!GesHandleStates.VersionEquals(entry.Version, passed))
                    throw new GesHandleException(GesDiagnosticCodes.RuntimeHandleVersionMismatch, symbol, $"The source of {root} delivered another version.");
            }

            return file.Text;
        }

        private static bool IsVersion(GesValue version)
            => version.ValueKind == GesBytecodeTypeKind.Integer && version.Unit == GesQuantityUnit.UnitNone
                || version.ValueKind == GesBytecodeTypeKind.Bytes && version.AsBytes().Length is GesHash20.Length or GesHash32.Length;

        private sealed class Source(GesHandle handle)
        {
            internal GesHandle Handle { get; } = handle;

            internal GesValue Version { get; set; } = handle.Version;
        }
    }

    private sealed class Reading(GesHandleType type, Fetch files, Dictionary<string, GesStore> dependencies)
    {
        private readonly List<(SourceFile File, int Offset, GesStoreDiagnostic Diagnostic)> _diagnostics = [];
        private readonly List<SourceFile> _files = [];
        private readonly HashSet<string> _visited = new(StringComparer.Ordinal);
        private readonly HashSet<string> _chain = new(StringComparer.Ordinal);
        private readonly List<Pending> _pending = [];
        private readonly Dictionary<string, Pending> _ids = new(StringComparer.Ordinal);
        private string[] _sortedIds = [];

        internal GesStore Run(IReadOnlyList<GesStoreRoot> roots, HandleSources? sources)
        {
            foreach (var root in roots)
            {
                var path = Combine("", root.Path);
                if (path is null)
                {
                    var file = AddFile(root.Name, root.Path, null);
                    Report(file, -1, GesStoreException.ImportOutsideRoot, $"The root file path '{root.Path}' steps above the root.");
                    continue;
                }

                ReadStoreFile(root.Name, path, null, -1);
            }

            if (_diagnostics.Count == 0) CheckIds();
            if (_diagnostics.Count == 0) CheckEntries();
            if (_diagnostics.Count > 0) throw new GesStoreException(Sorted());
            var entries = new GesStoreEntry[_pending.Count];
            for (var index = 0; index < entries.Length; index++) entries[index] = _pending[index].Entry!;
            Array.Sort(entries, (left, right) => string.CompareOrdinal(left.Id, right.Id));
            return new GesStore(type, entries, sources?.Read() ?? []);
        }

        // Step 1: a store file and, at each import, the imported file.
        private void ReadStoreFile(string root, string path, SourceFile? importer, int importOffset)
        {
            var key = root + "\n" + path;
            if (_chain.Contains(key))
            {
                Report(importer!, importOffset, GesStoreException.ImportCycle, $"'{path}' imports itself through its import chain.");
                return;
            }

            if (!_visited.Add(key)) return;
            var text = files(root, path);

            if (text is null)
            {
                if (importer is null) Report(AddFile(root, path, null), -1, GesStoreException.ImportMissing, $"The root '{root}' has no file '{path}'.");
                else Report(importer, importOffset, GesStoreException.ImportMissing, $"The root '{root}' has no file '{path}'.");
                return;
            }

            var file = AddFile(root, path, text);
            var syntax = GesDocumentReader.ReadStore(text);
            if (!syntax.IsStore)
            {
                if (importer is null) Report(file, syntax.HeaderOffset, GesStoreException.ImportKind, $"'{path}' is no store file.");
                else Report(importer, importOffset, GesStoreException.ImportKind, $"'{path}' is no store file.");
                return;
            }

            if (syntax.Diagnostic is { } diagnostic)
            {
                ReportDocument(file, diagnostic);
                return;
            }

            if (syntax.TypeName != type.Name)
            {
                var message = $"'{path}' is a store of @{syntax.TypeName}, not of @{type.Name}.";
                if (importer is null) Report(file, syntax.HeaderOffset, GesStoreException.ImportType, message);
                else Report(importer, importOffset, GesStoreException.ImportType, message);
                return;
            }

            _chain.Add(key);
            foreach (var item in syntax.Items)
            {
                switch (item.Kind)
                {
                    case GesStoreItemKind.Import:
                        if (Imported(file, item) is { } imported) ReadStoreFile(root, imported, file, item.PathOffset);
                        break;
                    case GesStoreItemKind.Entry:
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
        private void ReadStateDocument(SourceFile file, GesStoreItem item)
        {
            if (Imported(file, item) is not { } path) return;
            var text = files(file.Root, path);

            if (text is null)
            {
                Report(file, item.PathOffset, GesStoreException.ImportMissing, $"The root '{file.Root}' has no file '{path}'.");
                return;
            }

            var syntax = GesDocumentReader.Read(text, references: true);
            if (GesDocumentReader.ReadStore(text).IsStore)
            {
                Report(file, item.PathOffset, GesStoreException.ImportKind, $"'{path}' is a store file, not a state document.");
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
                Report(file, item.PathOffset, GesStoreException.ImportKind, $"'{path}' names no state type in a :Schema header.");
                return;
            }

            if (syntax.SchemaName != type.StateName)
            {
                Report(file, item.PathOffset, GesStoreException.ImportType, $"'{path}' is a :{syntax.SchemaName} document, not a :{type.StateName} state.");
                return;
            }

            _pending.Add(new Pending(file, item, document, syntax.Root!));
        }

        private string? Imported(SourceFile file, GesStoreItem item)
        {
            if (Combine(file.Path, item.Path) is { } path) return path;
            Report(file, item.PathOffset, GesStoreException.ImportOutsideRoot, $"The import path '{item.Path}' steps above the root.");
            return null;
        }

        // Step 2: every id fits the type's id and occurs once.
        private void CheckIds()
        {
            foreach (var pending in _pending)
            {
                if (Encoding.UTF8.GetByteCount(pending.Id) > type.Id!.Value.MaxLength)
                {
                    Report(pending.KeyFile, pending.Item.KeyOffset, GesStoreException.InvalidId, $"The id '{pending.Id}' is longer than {type.Id.Value.MaxLength} bytes.");
                    continue;
                }

                if (_ids.TryGetValue(pending.Id, out var first))
                {
                    var location = pending.KeyFile.At(pending.Item.KeyOffset);
                    _diagnostics.Add((pending.KeyFile, pending.Item.KeyOffset, new GesStoreDiagnostic(GesStoreException.DuplicateId,
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
                    Fail(pending, offset, GesStoreException.AmbiguousReference, $"@{name} names both {inModule.Id} and the root entry {atRoot.Id}.");
                    return;
                }

                target = inModule ?? atRoot;
            }

            if (target is null)
            {
                FailUnknown(pending, offset, "@" + name, name, pending.Item.Module, _sortedIds);
                return;
            }

            if (pinned && type.Version != GesHandleVersionKind.GesHash)
            {
                Fail(pending, offset, GesStoreException.InvalidPin, $"@{type.Name} has no content hash to pin @{name} to.");
                return;
            }

            pending.Links.Add(new Link(node, keyIndex, offset, pinned, target, null));
        }

        // A complete handle names an entry of its type's store when the store is known.
        private void ResolveHandle(Pending pending, GesDocumentNode node, int keyIndex, string text, int offset, bool pinned)
        {
            if (!GesHandle.TryRead(text, out var handle) || handle!.HasPart)
            {
                if (pinned) Fail(pending, offset, GesStoreException.InvalidPin, $"{text} names no store entry to pin to.");
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

                if (pinned && type.Version != GesHandleVersionKind.GesHash)
                {
                    Fail(pending, offset, GesStoreException.InvalidPin, $"@{type.Name} has no content hash to pin {text} to.");
                    return;
                }

                if (pinned) pending.Links.Add(new Link(node, keyIndex, offset, true, target, null));
                return;
            }

            if (!dependencies.TryGetValue(handle.TypeName, out var store))
            {
                if (pinned) Fail(pending, offset, GesStoreException.InvalidPin, $"No store of @{handle.TypeName} is known to pin {text} to.");
                return;
            }

            if ((id is null ? null : store[id]) is not { } entry)
            {
                var ids = new string[store._entries.Length];
                for (var index = 0; index < ids.Length; index++) ids[index] = store._entries[index].Id;
                FailUnknown(pending, offset, text, id ?? "", "", ids);
                return;
            }

            if (pinned && store.Type.Version != GesHandleVersionKind.GesHash)
            {
                Fail(pending, offset, GesStoreException.InvalidPin, $"@{handle.TypeName} has no content hash to pin {text} to.");
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
                    Report(pending.ValueFile, link.Offset, GesStoreException.DependencyCycle, $"Pinning @{target.Id} closes a cycle of pinned references.");
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
            var declaration = type.Resolved.State!;
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
                if (type.Version == GesHandleVersionKind.GesHash)
                {
                    var bytes = new byte[GesHash32.Length];
                    GesSchemaValues.ContentHash(type, state).CopyTo(bytes);
                    handle = GesHandle.Create(type.Name, id, GesValue.GesBytes(bytes));
                }
                else
                {
                    handle = GesHandle.Create(type.Name, id);
                }

                pending.Entry = new GesStoreEntry(pending.Id, handle, state, pending.KeyFile.At(pending.Item.KeyOffset));
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
                _diagnostics.Add((pending.ValueFile, pending.Value.Start, new GesStoreDiagnostic(exception.Code, exception.Message, location, exception.Path, null, null)));
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
            _diagnostics.Add((pending.ValueFile, offset, new GesStoreDiagnostic(GesStoreException.UnknownReference, message, pending.ValueFile.At(offset), "", null, suggestion)));
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
            => _diagnostics.Add((file, offset, new GesStoreDiagnostic(code, message, file.At(offset), "", null, null)));

        private void ReportDocument(SourceFile file, GesDocumentDiagnostic diagnostic)
        {
            var (line, column) = GesDocumentSyntax.Location(file.Text!, diagnostic.Start);
            int? limit = diagnostic.LimitName is null ? null : diagnostic.LimitValue;
            var exception = new GesDocumentException(diagnostic.Code, line, column, diagnostic.LimitName, limit);
            _diagnostics.Add((file, diagnostic.Start, new GesStoreDiagnostic(diagnostic.Code, exception.Message, file.At(diagnostic.Start), "", null, null)));
        }

        // A document error, which carries its line and column; it sorts by them within its file.
        private void ReportException(SourceFile file, GesDocumentException exception)
        {
            var location = new GesSourceLocation(file.Path, exception.Line, exception.Column, ModuleName: file.Root);
            _diagnostics.Add((file, -1, new GesStoreDiagnostic(exception.Code, exception.Message, location, exception.Path, null, null)));
        }

        private GesStoreDiagnostic[] Sorted()
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
            var result = new GesStoreDiagnostic[sorted.Length];
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
