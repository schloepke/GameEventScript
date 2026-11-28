// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using System.Globalization;
using GameEventScript.Api;

namespace GameEventScript.Generator;

// The model type of a handle type: a readonly struct `<Name>Handle` that holds the id, or the root and part of a type with a part,
// and the version where the type has one: an attached type's is its root's, a keyed type holds its root without a version and its own. Equality, hashing and order use the identity only; the constructor checks the declared
// range or length. It converts to and from GesHandle, whose text form it writes.
internal static partial class GesCSharpModelEmitter
{
    private const string ValueType = "global::GameEventScript.Runtime.Values.GesValue";

    // Also the wire code's handle type, which needs only the Runtime; `handles` holds the root of a type with a part.
    internal static void Handle(GesSchemaPlanner.Code code, List<GesModelHandle> handles, GesModelHandle handle, string access)
    {
        var name = GesModelPlanner.HandleName(handle.Name);
        var withPart = handle.HasPart;
        var keyed = handle.IsKeyed;
        // A version of its own: every type except one attached to a root with a part, which carries its root's.
        var own = !withPart || keyed;
        var root = withPart ? handles.Find(candidate => candidate.Name == handle.Root)! : null;
        var version = VersionType(handle.Version);
        var describe = handle.Version switch
        {
            GesHandleVersionKind.Revision => ", with its revision",
            GesHandleVersionKind.GesHash => ", with its content hash",
            GesHandleVersionKind.Sha1 => ", with its SHA-1 version",
            GesHandleVersionKind.Sha256 => ", with its SHA-256 version",
            _ => ""
        };
        var of = handle.State.Length > 0 ? $"an object of <c>:{handle.State}</c> outside the host" : "an object without state outside the host";
        var rooted = !withPart ? "" : keyed ? ", keyed by a <c>@" + handle.Root + "</c>" : ", a part of a <c>@" + handle.Root + "</c>";
        code.Line($"/// <summary>A handle <c>@{handle.Name}</c>: the identity of {of}{rooted}{describe}.");
        code.Line("/// Equality, hashing and order use the identity and ignore the version.</summary>");
        code.Line($"{access} readonly struct {name} : IEquatable<{name}>, IComparable<{name}>");
        code.Open("{");
        var key = handle.Key;
        var keyType = key.IsText ? "string" : "long";
        var keyName = withPart ? "part" : "id";
        var keyMember = withPart ? "Part" : "Id";
        code.Line("/// <summary>Creates a handle.</summary>");
        if (withPart) code.Line(keyed ? "/// <param name=\"root\">The root, whose version the handle drops.</param>" : "/// <param name=\"root\">The root, whose version is the handle's.</param>");
        code.Line($"/// <param name=\"{keyName}\">The {keyName}, {KeyDescription(key)}.</param>");
        if (version is not null && own) code.Line("/// <param name=\"version\">The version, or null for none.</param>");
        if (key.IsText) code.Line($"/// <exception cref=\"ArgumentNullException\">Thrown when <paramref name=\"{keyName}\"/> is null.</exception>");
        code.Line($"/// <exception cref=\"ArgumentOutOfRangeException\">Thrown when the {keyName} is out of its declared {(key.IsText ? "length" : "range")}.</exception>");
        var parameters = withPart ? $"{GesModelPlanner.HandleName(root!.Name)} root, {keyType} part" + (keyed && version is not null ? $", {version}? version = null" : "") : version is null ? $"{keyType} id" : $"{keyType} id, {version}? version = null";
        code.Line($"public {name}({parameters})");
        code.Open("{");
        if (key.IsText)
        {
            code.Line($"if ({keyName} is null) throw new ArgumentNullException(nameof({keyName}));");
            code.Line($"if (global::System.Text.Encoding.UTF8.GetByteCount({keyName}) > {Number(key.MaxLength)}) throw new ArgumentOutOfRangeException(nameof({keyName}));");
        }
        else code.Line($"if ({keyName} < {Number(key.Minimum)} || {keyName} > {Number(key.Maximum)}) throw new ArgumentOutOfRangeException(nameof({keyName}));");
        if (withPart) code.Line(keyed && root!.Version != GesHandleVersionKind.None ? "Root = root.WithoutVersion();" : "Root = root;");
        code.Line($"{keyMember} = {keyName};");
        if (version is not null && own) code.Line("Version = version;");
        code.Close();
        code.Blank();
        if (withPart)
        {
            code.Line("/// <summary>Gets the root.</summary>");
            code.Line($"public {GesModelPlanner.HandleName(root!.Name)} Root {{ get; }}");
            code.Blank();
            code.Line("/// <summary>Gets the part of the root's object.</summary>");
            code.Line($"public {keyType} Part {{ get; }}");
            if (version is not null)
            {
                code.Blank();
                if (keyed)
                {
                    code.Line("/// <summary>Gets the version of its own, or null for none.</summary>");
                    code.Line($"public {version}? Version {{ get; }}");
                }
                else
                {
                    code.Line("/// <summary>Gets the version: the root's, or null for none.</summary>");
                    code.Line($"public {version}? Version => Root.Version;");
                }
            }
        }
        else
        {
            code.Line("/// <summary>Gets the id.</summary>");
            code.Line($"public {keyType} Id {{ get; }}");
            if (version is not null)
            {
                code.Blank();
                code.Line("/// <summary>Gets the version, or null for none.</summary>");
                code.Line($"public {version}? Version {{ get; }}");
            }
        }

        if (version is not null)
        {
            code.Blank();
            code.Line("/// <summary>Returns the handle without its version.</summary>");
            code.Line("/// <returns>The handle with the same identity and no version.</returns>");
            code.Line($"public {name} WithoutVersion() => new {name}({(!withPart ? "Id" : keyed ? "Root, Part" : "Root.WithoutVersion(), Part")});");
        }

        var compare = key.IsText ? $"CompareScalars({keyMember}, other.{keyMember})" : $"{keyMember}.CompareTo(other.{keyMember})";
        var equal = key.IsText ? $"string.Equals({keyMember}, other.{keyMember}, StringComparison.Ordinal)" : $"{keyMember} == other.{keyMember}";
        var hash = key.IsText ? $"({keyMember} is null ? 0 : StringComparer.Ordinal.GetHashCode({keyMember}))" : $"{keyMember}.GetHashCode()";
        code.Blank();
        code.Line("/// <summary>Compares the identity.</summary>");
        code.Line("/// <param name=\"other\">The other handle.</param>");
        code.Line("/// <returns>Whether both name the same object.</returns>");
        code.Line($"public bool Equals({name} other) => {(withPart ? "Root.Equals(other.Root) && " : "")}{equal};");
        code.Blank();
        code.Line("/// <inheritdoc />");
        code.Line($"public override bool Equals(object? obj) => obj is {name} other && Equals(other);");
        code.Blank();
        code.Line("/// <inheritdoc />");
        code.Line($"public override int GetHashCode() => {(withPart ? $"unchecked((Root.GetHashCode() * 397) ^ {hash})" : hash)};");
        code.Blank();
        code.Line(withPart ? "/// <summary>Orders by identity: the root, then the part, a Text by Unicode scalars.</summary>" : "/// <summary>Orders by identity: the id, a Text by Unicode scalars.</summary>");
        code.Line("/// <param name=\"other\">The other handle.</param>");
        code.Line("/// <returns>Less than zero, zero or greater than zero as this handle orders before, with or after the other.</returns>");
        if (withPart)
        {
            code.Line($"public int CompareTo({name} other)");
            code.Open("{");
            code.Line("var root = Root.CompareTo(other.Root);");
            code.Line($"return root != 0 ? root : {compare};");
            code.Close();
        }
        else code.Line($"public int CompareTo({name} other) => {compare};");
        code.Blank();
        code.Line("/// <summary>Compares the identity of two handles.</summary>");
        code.Line("/// <param name=\"left\">The first handle.</param>");
        code.Line("/// <param name=\"right\">The second handle.</param>");
        code.Line("/// <returns>Whether both name the same object.</returns>");
        code.Line($"public static bool operator ==({name} left, {name} right) => left.Equals(right);");
        code.Blank();
        code.Line("/// <summary>Compares the identity of two handles.</summary>");
        code.Line("/// <param name=\"left\">The first handle.</param>");
        code.Line("/// <param name=\"right\">The second handle.</param>");
        code.Line("/// <returns>Whether they name different objects.</returns>");
        code.Line($"public static bool operator !=({name} left, {name} right) => !left.Equals(right);");
        code.Blank();
        code.Line("/// <summary>Writes the text form, such as <c>@Drone:7~120</c>.</summary>");
        code.Line("/// <returns>The text form.</returns>");
        code.Line("public override string ToString() => ToHandle().ToString();");
        code.Blank();
        code.Line("/// <summary>Converts to a runtime handle.</summary>");
        code.Line("/// <returns>The runtime handle.</returns>");
        if (withPart && keyed) code.Line($"public GesHandle ToHandle() => GesHandle.CreateKeyed({GesSchemaPlanner.Literal(handle.Name, false)}, Root.ToHandle(), {KeyValue(key, "Part")}{(version is null ? "" : ", " + VersionValue(handle.Version))});");
        else if (withPart) code.Line($"public GesHandle ToHandle() => GesHandle.CreateAttached({GesSchemaPlanner.Literal(handle.Name, false)}, Root.ToHandle(), {KeyValue(key, "Part")});");
        else if (version is null) code.Line($"public GesHandle ToHandle() => GesHandle.Create({GesSchemaPlanner.Literal(handle.Name, false)}, {KeyValue(key, "Id")});");
        else code.Line($"public GesHandle ToHandle() => GesHandle.Create({GesSchemaPlanner.Literal(handle.Name, false)}, {KeyValue(key, "Id")}, {VersionValue(handle.Version)});");
        if (own && version is not null)
        {
            if (handle.Version != GesHandleVersionKind.Revision)
            {
                code.Blank();
                code.Line($"private static byte[] Bytes({version} version)");
                code.Open("{");
                code.Line($"var bytes = new byte[{version}.Length];");
                code.Line("version.CopyTo(bytes);");
                code.Line("return bytes;");
                code.Close();
            }
        }

        code.Blank();
        code.Line("/// <summary>Converts a runtime handle of this type.</summary>");
        code.Line("/// <param name=\"handle\">The runtime handle.</param>");
        code.Line("/// <returns>The handle.</returns>");
        code.Line("/// <exception cref=\"ArgumentNullException\">Thrown when <paramref name=\"handle\"/> is null.</exception>");
        code.Line($"/// <exception cref=\"ArgumentException\">Thrown when the handle is of another type or out of its declared form.</exception>");
        code.Line($"public static {name} FromHandle(GesHandle handle)");
        code.Open("{");
        code.Line("if (handle is null) throw new ArgumentNullException(nameof(handle));");
        code.Line($"return TryFrom(handle, out var result) ? result : throw new ArgumentException(\"The handle is no @{handle.Name}.\", nameof(handle));");
        code.Close();
        code.Blank();
        code.Line($"internal static bool TryFrom(GesHandle handle, out {name} result)");
        code.Open("{");
        code.Line("result = default;");
        code.Line($"if (handle.TypeName != {GesSchemaPlanner.Literal(handle.Name, false)} || {(withPart ? "!" : "")}handle.HasPart{(withPart ? $" || {(keyed ? "!" : "")}handle.IsKeyed" : "")}) return false;");
        if (withPart)
        {
            var rootName = GesModelPlanner.HandleName(root!.Name);
            var rootHandle = root.Version == GesHandleVersionKind.None || keyed ? $"GesHandle.Create({GesSchemaPlanner.Literal(root.Name, false)}, handle.Id)" : $"GesHandle.Create({GesSchemaPlanner.Literal(root.Name, false)}, handle.Id, handle.Version)";
            code.Line($"if (!{rootName}.TryFrom({rootHandle}, out var root)) return false;");
            KeyRead(code, key, "handle.Part", "part");
            if (keyed && version is not null)
            {
                VersionRead(code, handle.Version);
                code.Line($"result = new {name}(root, part, version);");
            }
            else code.Line($"result = new {name}(root, part);");
        }
        else
        {
            KeyRead(code, key, "handle.Id", "id");
            if (version is null) code.Line($"result = new {name}(id);");
            else
            {
                VersionRead(code, handle.Version);
                code.Line($"result = new {name}(id, version);");
            }
        }

        code.Line("return true;");
        code.Close();
        if (key.IsText) CompareScalars(code);
        code.Blank();
        code.Line("// A GES handle value of this type; a field without a version drops it, one with a version requires it.");
        code.Line($"internal static bool Read(in {ValueType} value, bool versioned, out {name} result)");
        code.Open("{");
        code.Line("result = default;");
        code.Line("if (value.Handle is not { } handle || !TryFrom(handle, out result)) return false;");
        if (version is null) code.Line("return true;");
        else
        {
            code.Line("if (!versioned) result = result.WithoutVersion();");
            code.Line("return !versioned || handle.HasVersion;");
        }

        code.Close();
        code.Blank();
        code.Line("// The GES value of the handle; a field without a version drops it.");
        code.Line($"internal {ValueType} ToValue(bool versioned) => {ValueType}.GesHandle({(version is null ? "ToHandle()" : "(versioned ? this : WithoutVersion()).ToHandle()")});");
        code.Close();
    }

    private static string? VersionType(GesHandleVersionKind kind) => kind switch
    {
        GesHandleVersionKind.Revision => "long",
        GesHandleVersionKind.Sha1 => "GesHash20",
        GesHandleVersionKind.Sha256 or GesHandleVersionKind.GesHash => "GesHash32",
        _ => null
    };

    private static string VersionValue(GesHandleVersionKind kind) => kind == GesHandleVersionKind.Revision
        ? $"Version is {{ }} version ? {ValueType}.GesInteger(version) : {ValueType}.GesNothing()"
        : $"Version is {{ }} version ? {ValueType}.GesBytes(Bytes(version)) : {ValueType}.GesNothing()";

    private static void VersionRead(GesSchemaPlanner.Code code, GesHandleVersionKind kind)
    {
        var type = VersionType(kind)!;
        code.Line($"{type}? version = null;");
        if (kind == GesHandleVersionKind.Revision)
            code.Line("if (handle.Version.ValueKind == GesBytecodeTypeKind.Integer) version = handle.Version.AsInteger();");
        else
            code.Line($"if (handle.Version.ValueKind == GesBytecodeTypeKind.Bytes && handle.Version.AsBytes().Length == {type}.Length) version = {type}.FromBytes(handle.Version.AsBytes());");
        code.Line("else if (!handle.Version.IsNothing) return false;");
    }

    private static string KeyValue(GesHandleKey key, string member) => key.IsText ? $"{ValueType}.GesText({member} ?? \"\")" : $"{ValueType}.GesInteger({member})";

    private static void KeyRead(GesSchemaPlanner.Code code, GesHandleKey key, string expression, string name)
    {
        if (key.IsText)
        {
            code.Line($"if ({expression}.ValueKind != GesBytecodeTypeKind.Text) return false;");
            code.Line($"var {name} = {expression}.AsText();");
            code.Line($"if (global::System.Text.Encoding.UTF8.GetByteCount({name}) > {Number(key.MaxLength)}) return false;");
        }
        else
        {
            code.Line($"if ({expression}.ValueKind != GesBytecodeTypeKind.Integer) return false;");
            code.Line($"var {name} = {expression}.AsInteger();");
            code.Line($"if ({name} < {Number(key.Minimum)} || {name} > {Number(key.Maximum)}) return false;");
        }
    }

    private static string KeyDescription(GesHandleKey key)
        => key.IsText ? $"a Text of at most {Number(key.MaxLength)} UTF-8 bytes" : $"an integer between {Number(key.Minimum)} and {Number(key.Maximum)}";

    private static string Number(long value) => value == long.MinValue ? "long.MinValue" : value.ToString(CultureInfo.InvariantCulture) + (value is > int.MaxValue or < int.MinValue ? "L" : "");

    // Orders two texts by Unicode scalars, as GES orders text ids.
    private static void CompareScalars(GesSchemaPlanner.Code code)
    {
        code.Blank();
        code.Line("// Orders two texts by Unicode scalars, as GES orders text ids.");
        code.Line("private static int CompareScalars(string? left, string? right)");
        code.Open("{");
        code.Line("if (left is null || right is null) return left is null ? right is null ? 0 : -1 : 1;");
        code.Line("var length = Math.Min(left.Length, right.Length);");
        code.Line("for (var index = 0; index < length; index++)");
        code.Open("{");
        code.Line("int a = left[index], b = right[index];");
        code.Line("if (a == b) continue;");
        code.Line("// A surrogate encodes a scalar above every UTF-16 unit from U+E000.");
        code.Line("a = a >= 0xE000 ? a - 0x800 : a >= 0xD800 ? a + 0x2000 : a;");
        code.Line("b = b >= 0xE000 ? b - 0x800 : b >= 0xD800 ? b + 0x2000 : b;");
        code.Line("return a - b;");
        code.Close();
        code.Line("return left.Length - right.Length;");
        code.Close();
    }

    // The support helpers of maps with handle keys.
    private static void HandleHelpers(GesSchemaPlanner.Code code, bool conversions)
    {
        code.Blank();
        code.Line("/// <summary>Compares two maps by their keys and values, in any order.</summary>");
        code.Line("internal static bool MapEquals<TKey, TValue>(Dictionary<TKey, TValue>? left, Dictionary<TKey, TValue>? right, Func<TValue, TValue, bool> equals) where TKey : notnull");
        code.Open("{");
        code.Line("if (ReferenceEquals(left, right)) return true;");
        code.Line("if (left is null || right is null || left.Count != right.Count) return false;");
        code.Line("foreach (var entry in left)");
        code.Line("    if (!right.TryGetValue(entry.Key, out var other) || !equals(entry.Value, other)) return false;");
        code.Line("return true;");
        code.Close();
        code.Blank();
        code.Line("/// <summary>Hashes a map from its entries, in any order.</summary>");
        code.Line("internal static int MapHash<TKey, TValue>(Dictionary<TKey, TValue>? map, Func<TValue, int> hash) where TKey : notnull");
        code.Open("{");
        code.Line("if (map is null) return 0;");
        code.Line("unchecked");
        code.Open("{");
        code.Line("var result = 23;");
        code.Line("foreach (var entry in map) result += (entry.Key.GetHashCode() * 31) ^ hash(entry.Value);");
        code.Line("return result;");
        code.Close();
        code.Close();
        if (!conversions) return;
        code.Blank();
        code.Line("internal static GesValue HandleMap<TKey, TValue>(Dictionary<TKey, TValue> map, Func<TKey, GesValue> key, Func<TValue, GesValue> value) where TKey : notnull");
        code.Open("{");
        code.Line("var keys = new GesValue[map.Count];");
        code.Line("var values = new GesValue[map.Count];");
        code.Line("var index = 0;");
        code.Line("foreach (var entry in map)");
        code.Open("{");
        code.Line("keys[index] = key(entry.Key);");
        code.Line("values[index++] = value(entry.Value);");
        code.Close();
        code.Line("return GesValue.GesMapOfKeys(keys, values);");
        code.Close();
        code.Blank();
        code.Line("internal static bool HandleMap<TKey, TValue>(in GesValue value, Decoder<TKey> key, Decoder<TValue> element, out Dictionary<TKey, TValue> result) where TKey : notnull");
        code.Open("{");
        code.Line("result = null!;");
        code.Line("if (value.ValueKind != GesBytecodeTypeKind.Map || value.CustomTypeName is not null || value.MaterializedMap() is not { } map) return false;");
        code.Line("var entries = new Dictionary<TKey, TValue>(map.Length);");
        code.Line("for (var index = 0; index < map.Length; index++)");
        code.Open("{");
        code.Line("if (map.HandleKey(index) is not { } handle || !key(GesValue.GesHandle(handle), out var entryKey) || !element(map.ValueAt(index), out var entryValue) || !entries.TryAdd(entryKey, entryValue)) return false;");
        code.Close();
        code.Blank();
        code.Line("result = entries;");
        code.Line("return true;");
        code.Close();
    }
}
