// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using System.Globalization;
using GameEventScript.Api;

namespace GameEventScript.Generator;

// The Swift model type of a handle type: a struct `<Name>Handle` that holds the id, or the root and part of a type with a part,
// and the version where the type has one: an attached type's is its root's, a keyed type holds its root without a version and
// its own. Equality, hashing and order use the identity only, a Text id by its Unicode scalars rather
// than by String's canonical equivalence. Like the C# struct, the public initializers throw GesApiError for an id or part
// outside the declared range or length and for a runtime handle of another form, and none creates a default handle; the
// generated code builds handles it already checked through internal unchecked initializers. It converts to and from
// GesHandle, whose canonical text form it writes and reads as Codable. Swift's synthesized equality and hashing of a
// Dictionary already compare and hash entries independent of order, so models need no counterpart of the C# MapEquals and
// MapHash helpers.
internal static partial class GesSwiftModelEmitter
{
    // Also the wire code's handle type, which needs only the Runtime; `handles` holds the root of a type with a part.
    internal static void Handle(GesSchemaPlanner.Code code, List<GesModelHandle> handles, GesModelHandle handle, string access)
    {
        var name = GesModelPlanner.HandleName(handle.Name);
        var typeName = GesSchemaPlanner.Literal(handle.Name, true);
        var withPart = handle.HasPart;
        var keyed = handle.IsKeyed;
        // A version of its own: every type except one attached to a root with a part, which carries its root's.
        var own = !withPart || keyed;
        var root = withPart ? handles.Find(candidate => candidate.Name == handle.Root)! : null;
        var rootName = root is null ? "" : GesModelPlanner.HandleName(root.Name);
        var version = VersionType(handle.Version);
        var describe = handle.Version switch
        {
            GesHandleVersionKind.Revision => ", with its revision",
            GesHandleVersionKind.GesHash => ", with its content hash",
            GesHandleVersionKind.Sha1 => ", with its SHA-1 version",
            GesHandleVersionKind.Sha256 => ", with its SHA-256 version",
            _ => ""
        };
        var key = handle.Key;
        var keyType = key.IsText ? "String" : "Int64";
        var keyName = withPart ? "part" : "id";
        var of = handle.State.Length > 0 ? $"an object of `:{handle.State}` outside the host" : "an object without state outside the host";
        var rooted = !withPart ? "" : keyed ? ", keyed by a `@" + handle.Root + "`" : ", a part of a `@" + handle.Root + "`";
        code.Line($"/// A handle `@{handle.Name}`: the identity of {of}{rooted}{describe}.");
        code.Line($"/// Equality, hashing and order use the identity and ignore the version{(key.IsText ? "; a Text compares by Unicode scalars" : "")}.");
        code.Open($"{access}struct {name}: Hashable, Comparable, Sendable, Codable, CustomStringConvertible {{");
        if (withPart)
        {
            code.Line(keyed ? "/// The root, without a version." : "/// The root, whose version is the handle's.");
            code.Line($"{access}let root: {rootName}");
            code.Blank();
            code.Line(keyed ? "/// The part, which with the root's id names the object." : "/// The part of the root's object.");
            code.Line($"{access}let part: {keyType}");
            if (version is not null)
            {
                code.Blank();
                if (keyed)
                {
                    code.Line("/// The version of its own, or nil for none.");
                    code.Line($"{access}let version: {version}?");
                }
                else
                {
                    code.Line("/// The version: the root's, or nil for none.");
                    code.Line($"{access}var version: {version}? {{ root.version }}");
                }
            }
        }
        else
        {
            code.Line("/// The id.");
            code.Line($"{access}let id: {keyType}");
            if (version is not null)
            {
                code.Blank();
                code.Line("/// The version, or nil for none.");
                code.Line($"{access}let version: {version}?");
            }
        }

        code.Blank();
        code.Line("/// Creates a handle.");
        code.Line("///");
        code.Line("/// - Parameters:");
        if (withPart) code.Line(keyed ? "///   - root: The root, whose version the handle drops." : "///   - root: The root, whose version is the handle's.");
        code.Line($"///   - {keyName}: The {keyName}, {KeyDescription(key)}.");
        if (version is not null && own) code.Line("///   - version: The version, or nil for none.");
        code.Line($"/// - Throws: `GesApiError.invalidArgument` when the {keyName} is out of its declared {(key.IsText ? "length" : "range")}.");
        var keyedVersion = keyed && version is not null;
        var parameters = withPart ? $"root: {rootName}, part: {keyType}" + (keyedVersion ? $", version: {version}? = nil" : "") : version is null ? $"id: {keyType}" : $"id: {keyType}, version: {version}? = nil";
        code.Open($"{access}init({parameters}) throws(GesApiError) {{");
        var bound = GesSchemaPlanner.Literal($"The {keyName} of a @{handle.Name} is out of its declared {(key.IsText ? "length" : "range")}.", true);
        code.Line(key.IsText
            ? $"guard {keyName}.utf8.count <= {Number(key.MaxLength)} else {{ throw .invalidArgument({bound}) }}"
            : $"guard {keyName} >= {Number(key.Minimum)} && {keyName} <= {Number(key.Maximum)} else {{ throw .invalidArgument({bound}) }}");
        var keyedRoot = keyed && root!.Version != GesHandleVersionKind.None ? "root.withoutVersion()" : "root";
        code.Line(withPart ? $"self.init(uncheckedRoot: {keyedRoot}, part: part{(keyedVersion ? ", version: version" : "")})" : version is null ? "self.init(uncheckedId: id)" : "self.init(uncheckedId: id, version: version)");
        code.Close();
        code.Blank();
        code.Line($"// A handle whose {keyName} the generated code already checked.");
        var uncheckedParameters = withPart ? $"uncheckedRoot root: {rootName}, part: {keyType}" + (keyedVersion ? $", version: {version}? = nil" : "") : version is null ? $"uncheckedId id: {keyType}" : $"uncheckedId id: {keyType}, version: {version}? = nil";
        code.Open($"init({uncheckedParameters}) {{");
        if (withPart) code.Line("self.root = root");
        code.Line($"self.{keyName} = {keyName}");
        if (version is not null && own) code.Line("self.version = version");
        code.Close();
        if (version is not null)
        {
            code.Blank();
            code.Line("/// Returns the handle with the same identity and no version.");
            code.Line($"{access}func withoutVersion() -> {name} {{ {name}({(!withPart ? "uncheckedId: id" : keyed ? "uncheckedRoot: root, part: part" : "uncheckedRoot: root.withoutVersion(), part: part")}) }}");
        }

        var equal = key.IsText ? $"left.{keyName}.utf8.elementsEqual(right.{keyName}.utf8)" : $"left.{keyName} == right.{keyName}";
        var less = key.IsText ? $"left.{keyName}.utf8.lexicographicallyPrecedes(right.{keyName}.utf8)" : $"left.{keyName} < right.{keyName}";
        code.Blank();
        code.Line("/// Compares the identity: whether both handles name the same object.");
        code.Line($"{access}static func == (left: {name}, right: {name}) -> Bool {{ {(withPart ? "left.root == right.root && " : "")}{equal} }}");
        code.Blank();
        code.Line("/// Hashes the identity.");
        code.Open($"{access}func hash(into hasher: inout Hasher) {{");
        if (withPart) code.Line("hasher.combine(root)");
        if (key.IsText)
        {
            code.Line($"for byte in {keyName}.utf8 {{ hasher.combine(byte) }}");
            code.Line("// No UTF-8 byte is 0xFF, so it ends the text.");
            code.Line("hasher.combine(0xFF as UInt8)");
        }
        else code.Line($"hasher.combine({keyName})");
        code.Close();
        code.Blank();
        code.Line(withPart ? "/// Orders by identity: the root, then the part, a Text by Unicode scalars." : "/// Orders by identity: the id, a Text by Unicode scalars.");
        code.Line(withPart
            ? $"{access}static func < (left: {name}, right: {name}) -> Bool {{ left.root != right.root ? left.root < right.root : {less} }}"
            : $"{access}static func < (left: {name}, right: {name}) -> Bool {{ {less} }}");
        code.Blank();
        code.Line($"/// The text form, such as `{Example(handle, root)}`.");
        code.Line($"{access}var description: String {{ toHandle().description }}");
        code.Blank();
        code.Line("// Cannot throw: the type name is valid and the id, part and version have valid kinds.");
        code.Line("/// Converts to a runtime handle.");
        if (withPart && keyed) code.Line($"{access}func toHandle() -> GesHandle {{ try! GesHandle(typeName: {typeName}, keyedBy: root.toHandle(), part: {KeyValue(key, "part")}{(version is null ? "" : ", version: " + VersionValue(handle.Version))}) }}");
        else if (withPart) code.Line($"{access}func toHandle() -> GesHandle {{ try! GesHandle(typeName: {typeName}, attachedTo: root.toHandle(), part: {KeyValue(key, "part")}) }}");
        else if (version is null) code.Line($"{access}func toHandle() -> GesHandle {{ try! GesHandle(typeName: {typeName}, id: {KeyValue(key, "id")}) }}");
        else code.Line($"{access}func toHandle() -> GesHandle {{ try! GesHandle(typeName: {typeName}, id: {KeyValue(key, "id")}, version: {VersionValue(handle.Version)}) }}");
        code.Blank();
        code.Line("/// Converts a runtime handle of this type.");
        code.Line("///");
        code.Line("/// - Throws: `GesApiError.invalidArgument` for a handle of another type or out of its declared form.");
        code.Open($"{access}init(_ handle: GesHandle) throws(GesApiError) {{");
        code.Line($"guard let value = {name}(matching: handle) else {{ throw .invalidArgument({GesSchemaPlanner.Literal("The handle is no @" + handle.Name + ".", true)}) }}");
        code.Line("self = value");
        code.Close();
        code.Blank();
        code.Line("// A runtime handle of this type, or nil for a handle of another type or out of its declared form.");
        code.Open("init?(matching handle: GesHandle) {");
        // A text form with a part but without a version reads as attached, so only a version marks the form.
        code.Line($"guard handle.typeName == {typeName}, {(withPart ? "" : "!")}handle.hasPart{(withPart ? $", !handle.hasVersion || {(keyed ? "" : "!")}handle.isKeyed" : "")} else {{ return nil }}");
        if (withPart)
        {
            var rootHandle = root!.Version == GesHandleVersionKind.None || keyed
                ? $"GesHandle(typeName: {GesSchemaPlanner.Literal(root.Name, true)}, id: handle.id)"
                : $"GesHandle(typeName: {GesSchemaPlanner.Literal(root.Name, true)}, id: handle.id, version: handle.version)";
            code.Line($"guard let rootHandle = try? {rootHandle}, let root = {rootName}(matching: rootHandle) else {{ return nil }}");
            KeyRead(code, key, "handle.part", "part");
            if (keyedVersion)
            {
                VersionRead(code, handle.Version);
                code.Line("self.init(uncheckedRoot: root, part: part, version: version)");
            }
            else code.Line("self.init(uncheckedRoot: root, part: part)");
        }
        else
        {
            KeyRead(code, key, "handle.id", "id");
            if (version is null) code.Line("self.init(uncheckedId: id)");
            else
            {
                VersionRead(code, handle.Version);
                code.Line("self.init(uncheckedId: id, version: version)");
            }
        }

        code.Close();
        code.Blank();
        code.Line("/// Reads the text form.");
        code.Line("///");
        code.Line($"/// - Throws: `DecodingError.dataCorrupted` for text that is no handle `@{handle.Name}` of the declared form.");
        code.Open($"{access}init(from decoder: any Decoder) throws {{");
        code.Line("let container = try decoder.singleValueContainer()");
        code.Line("let text = try container.decode(String.self)");
        code.Line($"guard let handle = try? GesHandle(parsing: text), let value = {name}(matching: handle) else {{ throw DecodingError.dataCorruptedError(in: container, debugDescription: {GesSchemaPlanner.Literal("The text is no @" + handle.Name + ".", true)}) }}");
        code.Line("self = value");
        code.Close();
        code.Blank();
        code.Line("/// Writes the text form.");
        code.Open($"{access}func encode(to encoder: any Encoder) throws {{");
        code.Line("var container = encoder.singleValueContainer()");
        code.Line("try container.encode(description)");
        code.Close();
        code.Blank();
        code.Line("// A GES handle value of this type; a field without a version drops it, one with a version requires it.");
        code.Open($"static func read(_ value: GesValue, versioned: Bool) -> {name}? {{");
        code.Line($"guard let handle = value.handleValue, let result = {name}(matching: handle) else {{ return nil }}");
        if (version is null) code.Line("return result");
        else
        {
            code.Line("if !versioned { return result.withoutVersion() }");
            code.Line("return handle.hasVersion ? result : nil");
        }

        code.Close();
        code.Blank();
        code.Line("// The GES value of the handle; a field without a version drops it.");
        code.Line($"func toValue(versioned: Bool) -> GesValue {{ .handle({(version is null ? "toHandle()" : "(versioned ? self : withoutVersion()).toHandle()")}) }}");
        code.Close();
    }

    private static string? VersionType(GesHandleVersionKind kind) => kind switch
    {
        GesHandleVersionKind.Revision => "Int64",
        GesHandleVersionKind.Sha1 => "GesHash20",
        GesHandleVersionKind.Sha256 or GesHandleVersionKind.GesHash => "GesHash32",
        _ => null
    };

    private static string VersionValue(GesHandleVersionKind kind) => kind == GesHandleVersionKind.Revision
        ? "version.map { GesValue.integer($0) } ?? .nothing"
        : "version.map { GesValue.bytes($0.bytes) } ?? .nothing";

    private static void VersionRead(GesSchemaPlanner.Code code, GesHandleVersionKind kind)
    {
        var type = VersionType(kind)!;
        code.Line($"let version: {type}?");
        if (kind == GesHandleVersionKind.Revision)
            code.Line("if let revision = handle.version.integerValue { version = revision }");
        else
            code.Line($"if let bytes = handle.version.bytesValue, bytes.count == {type}.length {{ version = {type}(bytes: bytes) }}");
        code.Line("else if handle.version.isNothing { version = nil }");
        code.Line("else { return nil }");
    }

    private static string KeyValue(GesHandleKey key, string member) => key.IsText ? $".text({member})" : $".integer({member})";

    private static void KeyRead(GesSchemaPlanner.Code code, GesHandleKey key, string expression, string name)
    {
        code.Line(key.IsText
            ? $"guard {expression}.valueKind == .text, let {name} = {expression}.textValue, {name}.utf8.count <= {Number(key.MaxLength)} else {{ return nil }}"
            : $"guard let {name} = {expression}.integerValue, {name} >= {Number(key.Minimum)} && {name} <= {Number(key.Maximum)} else {{ return nil }}");
    }

    private static string KeyDescription(GesHandleKey key)
        => key.IsText ? $"a Text of at most {Number(key.MaxLength)} UTF-8 bytes" : $"an integer between {Number(key.Minimum)} and {Number(key.Maximum)}";

    private static string Number(long value) => value == long.MinValue ? "Int64.min" : value.ToString(CultureInfo.InvariantCulture);

    // A text form for the documentation, such as `@Drone:7~120`.
    private static string Example(GesModelHandle handle, GesModelHandle? root)
    {
        var owner = root ?? handle;
        var text = "@" + handle.Name + ":" + (owner.Id.IsText ? "north" : "7");
        var revision = handle.Version == GesHandleVersionKind.Revision ? "~120" : "";
        if (!handle.HasPart) return text + revision;
        var part = "/" + (handle.Part.IsText ? "hangar" : "2");
        return handle.IsKeyed ? text + part + revision : text + revision + part;
    }

    // The support helpers of maps with handle keys.
    private static void HandleHelpers(GesSchemaPlanner.Code code)
    {
        code.Blank();
        code.Open("static func handleMap<K, V>(_ map: [K: V], _ key: (K) -> GesValue, _ value: (V) -> GesValue) -> GesValue {");
        code.Line("var keys: [GesValue] = []");
        code.Line("var values: [GesValue] = []");
        code.Line("keys.reserveCapacity(map.count)");
        code.Line("values.reserveCapacity(map.count)");
        code.Open("for (entryKey, entryValue) in map {");
        code.Line("keys.append(key(entryKey))");
        code.Line("values.append(value(entryValue))");
        code.Close();
        code.Line("return try! .map(keyValues: keys, values: values)");
        code.Close();
        code.Blank();
        code.Open("static func handleMap<K, V>(_ value: GesValue, _ key: (GesValue) -> K?, _ element: (GesValue) -> V?) -> [K: V]? {");
        code.Line("guard value.valueKind == .map, value.customTypeName == nil, let map = value.asMap else { return nil }");
        code.Line("var entries: [K: V] = [:]");
        code.Line("entries.reserveCapacity(map.length)");
        code.Open("for index in 0..<map.length {");
        code.Line("guard let handle = map.handleKey(at: index), let entryKey = key(.handle(handle)), let entryValue = element(map.value(at: index)), entries.updateValue(entryValue, forKey: entryKey) == nil else { return nil }");
        code.Close();
        code.Line("return entries");
        code.Close();
    }
}
