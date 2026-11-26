// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

@_spi(Generator) import GameEventScriptRuntime

// The model type of a handle type: a readonly struct `<Name>Handle` that holds the id, or a composite's root and part, and the
// version where the type has one. Equality, hashing and order use the identity only; the constructor checks the declared
// range or length. It converts to and from GesHandle, whose text form it writes.
extension GesCSharpModelEmitter {
    private static let valueType = "global::GameEventScript.Runtime.Values.GesValue"

    // Also the wire code's handle type, which needs only the Runtime; `handles` holds the composite's root.
    static func handle(_ code: inout Code, _ handles: [GesModelHandle], _ handle: GesModelHandle, _ access: String) {
        let name = GesModelPlanner.handleName(handle.name)
        let root = handle.isComposite ? handles.first { $0.name == handle.root }! : nil
        let version = versionType(handle.version)
        let describe =
            switch handle.version {
            case .revision: ", with its revision"
            case .contentHash: ", with its content hash"
            case .sha1: ", with its SHA-1 version"
            case .sha256: ", with its SHA-256 version"
            case .none: ""
            }
        code.line("/// <summary>A handle <c>@\(handle.name)</c>: the identity of an object of <c>:\(handle.state)</c> outside the host\(handle.isComposite ? ", a part of a <c>@" + handle.root! + "</c>" : "")\(describe).")
        code.line("/// Equality, hashing and order use the identity and ignore the version.</summary>")
        code.line("\(access) readonly struct \(name) : IEquatable<\(name)>, IComparable<\(name)>")
        code.open("{")
        let key = handle.key
        let keyType = key.isText ? "string" : "long"
        let keyName = handle.isComposite ? "part" : "id"
        let keyMember = handle.isComposite ? "Part" : "Id"
        code.line("/// <summary>Creates a handle.</summary>")
        if handle.isComposite { code.line("/// <param name=\"root\">The root, whose version is the handle's.</param>") }
        code.line("/// <param name=\"\(keyName)\">The \(keyName), \(keyDescription(key)).</param>")
        if version != nil && !handle.isComposite { code.line("/// <param name=\"version\">The version, or null for none.</param>") }
        if key.isText { code.line("/// <exception cref=\"ArgumentNullException\">Thrown when <paramref name=\"\(keyName)\"/> is null.</exception>") }
        code.line("/// <exception cref=\"ArgumentOutOfRangeException\">Thrown when the \(keyName) is out of its declared \(key.isText ? "length" : "range").</exception>")
        let parameters =
            if handle.isComposite { "\(GesModelPlanner.handleName(root!.name)) root, \(keyType) part" } else if let version { "\(keyType) id, \(version)? version = null" } else { "\(keyType) id" }
        code.line("public \(name)(\(parameters))")
        code.open("{")
        if key.isText {
            code.line("if (\(keyName) is null) throw new ArgumentNullException(nameof(\(keyName)));")
            code.line("if (global::System.Text.Encoding.UTF8.GetByteCount(\(keyName)) > \(number(key.maxLength))) throw new ArgumentOutOfRangeException(nameof(\(keyName)));")
        } else {
            code.line("if (\(keyName) < \(number(key.minimum)) || \(keyName) > \(number(key.maximum))) throw new ArgumentOutOfRangeException(nameof(\(keyName)));")
        }
        if handle.isComposite { code.line("Root = root;") }
        code.line("\(keyMember) = \(keyName);")
        if version != nil && !handle.isComposite { code.line("Version = version;") }
        code.close()
        code.blank()
        if handle.isComposite {
            code.line("/// <summary>Gets the root.</summary>")
            code.line("public \(GesModelPlanner.handleName(root!.name)) Root { get; }")
            code.blank()
            code.line("/// <summary>Gets the part of the root's object.</summary>")
            code.line("public \(keyType) Part { get; }")
            if let version {
                code.blank()
                code.line("/// <summary>Gets the version: the root's, or null for none.</summary>")
                code.line("public \(version)? Version => Root.Version;")
            }
        } else {
            code.line("/// <summary>Gets the id.</summary>")
            code.line("public \(keyType) Id { get; }")
            if let version {
                code.blank()
                code.line("/// <summary>Gets the version, or null for none.</summary>")
                code.line("public \(version)? Version { get; }")
            }
        }

        if version != nil {
            code.blank()
            code.line("/// <summary>Returns the handle without its version.</summary>")
            code.line("/// <returns>The handle with the same identity and no version.</returns>")
            code.line("public \(name) WithoutVersion() => new \(name)(\(handle.isComposite ? "Root.WithoutVersion(), Part" : "Id"));")
        }

        let compare = key.isText ? "CompareScalars(\(keyMember), other.\(keyMember))" : "\(keyMember).CompareTo(other.\(keyMember))"
        let equal = key.isText ? "string.Equals(\(keyMember), other.\(keyMember), StringComparison.Ordinal)" : "\(keyMember) == other.\(keyMember)"
        let hash = key.isText ? "(\(keyMember) is null ? 0 : StringComparer.Ordinal.GetHashCode(\(keyMember)))" : "\(keyMember).GetHashCode()"
        code.blank()
        code.line("/// <summary>Compares the identity.</summary>")
        code.line("/// <param name=\"other\">The other handle.</param>")
        code.line("/// <returns>Whether both name the same object.</returns>")
        code.line("public bool Equals(\(name) other) => \(handle.isComposite ? "Root.Equals(other.Root) && " : "")\(equal);")
        code.blank()
        code.line("/// <inheritdoc />")
        code.line("public override bool Equals(object? obj) => obj is \(name) other && Equals(other);")
        code.blank()
        code.line("/// <inheritdoc />")
        code.line("public override int GetHashCode() => \(handle.isComposite ? "unchecked((Root.GetHashCode() * 397) ^ \(hash))" : hash);")
        code.blank()
        code.line("/// <summary>Orders by identity: the id, a Text by Unicode scalars.</summary>")
        code.line("/// <param name=\"other\">The other handle.</param>")
        code.line("/// <returns>Less than zero, zero or greater than zero as this handle orders before, with or after the other.</returns>")
        if handle.isComposite {
            code.line("public int CompareTo(\(name) other)")
            code.open("{")
            code.line("var root = Root.CompareTo(other.Root);")
            code.line("return root != 0 ? root : \(compare);")
            code.close()
        } else {
            code.line("public int CompareTo(\(name) other) => \(compare);")
        }
        code.blank()
        code.line("/// <summary>Compares the identity of two handles.</summary>")
        code.line("/// <param name=\"left\">The first handle.</param>")
        code.line("/// <param name=\"right\">The second handle.</param>")
        code.line("/// <returns>Whether both name the same object.</returns>")
        code.line("public static bool operator ==(\(name) left, \(name) right) => left.Equals(right);")
        code.blank()
        code.line("/// <summary>Compares the identity of two handles.</summary>")
        code.line("/// <param name=\"left\">The first handle.</param>")
        code.line("/// <param name=\"right\">The second handle.</param>")
        code.line("/// <returns>Whether they name different objects.</returns>")
        code.line("public static bool operator !=(\(name) left, \(name) right) => !left.Equals(right);")
        code.blank()
        code.line("/// <summary>Writes the text form, such as <c>@Drone:7~120</c>.</summary>")
        code.line("/// <returns>The text form.</returns>")
        code.line("public override string ToString() => ToHandle().ToString();")
        code.blank()
        code.line("/// <summary>Converts to a runtime handle.</summary>")
        code.line("/// <returns>The runtime handle.</returns>")
        let literalName = GesSchemaPlanner.literal(handle.name, swift: false)
        if handle.isComposite {
            code.line("public GesHandle ToHandle() => GesHandle.CreateComposite(\(literalName), Root.ToHandle(), \(keyValue(key, "Part")));")
        } else if let version {
            code.line("public GesHandle ToHandle() => GesHandle.Create(\(literalName), \(keyValue(key, "Id")), \(versionValue(handle.version)));")
            if handle.version != .revision {
                code.blank()
                code.line("private static byte[] Bytes(\(version) version)")
                code.open("{")
                code.line("var bytes = new byte[\(version).Length];")
                code.line("version.CopyTo(bytes);")
                code.line("return bytes;")
                code.close()
            }
        } else {
            code.line("public GesHandle ToHandle() => GesHandle.Create(\(literalName), \(keyValue(key, "Id")));")
        }

        code.blank()
        code.line("/// <summary>Converts a runtime handle of this type.</summary>")
        code.line("/// <param name=\"handle\">The runtime handle.</param>")
        code.line("/// <returns>The handle.</returns>")
        code.line("/// <exception cref=\"ArgumentNullException\">Thrown when <paramref name=\"handle\"/> is null.</exception>")
        code.line("/// <exception cref=\"ArgumentException\">Thrown when the handle is of another type or out of its declared form.</exception>")
        code.line("public static \(name) FromHandle(GesHandle handle)")
        code.open("{")
        code.line("if (handle is null) throw new ArgumentNullException(nameof(handle));")
        code.line("return TryFrom(handle, out var result) ? result : throw new ArgumentException(\"The handle is no @\(handle.name).\", nameof(handle));")
        code.close()
        code.blank()
        code.line("internal static bool TryFrom(GesHandle handle, out \(name) result)")
        code.open("{")
        code.line("result = default;")
        code.line("if (handle.TypeName != \(literalName) || handle.IsComposite != \(handle.isComposite ? "true" : "false")) return false;")
        if let root {
            let rootName = GesModelPlanner.handleName(root.name)
            let rootLiteral = GesSchemaPlanner.literal(root.name, swift: false)
            let rootHandle = root.version == .none ? "GesHandle.Create(\(rootLiteral), handle.Id)" : "GesHandle.Create(\(rootLiteral), handle.Id, handle.Version)"
            code.line("if (!\(rootName).TryFrom(\(rootHandle), out var root)) return false;")
            keyRead(&code, key, "handle.Part", "part")
            code.line("result = new \(name)(root, part);")
        } else {
            keyRead(&code, key, "handle.Id", "id")
            if version == nil {
                code.line("result = new \(name)(id);")
            } else {
                versionRead(&code, handle.version)
                code.line("result = new \(name)(id, version);")
            }
        }

        code.line("return true;")
        code.close()
        if key.isText { compareScalars(&code) }
        code.blank()
        code.line("// A GES handle value of this type; a field without a version drops it, one with a version requires it.")
        code.line("internal static bool Read(in \(valueType) value, bool versioned, out \(name) result)")
        code.open("{")
        code.line("result = default;")
        code.line("if (value.Handle is not { } handle || !TryFrom(handle, out result)) return false;")
        if version == nil {
            code.line("return true;")
        } else {
            code.line("if (!versioned) result = result.WithoutVersion();")
            code.line("return !versioned || handle.HasVersion;")
        }

        code.close()
        code.blank()
        code.line("// The GES value of the handle; a field without a version drops it.")
        code.line("internal \(valueType) ToValue(bool versioned) => \(valueType).GesHandle(\(version == nil ? "ToHandle()" : "(versioned ? this : WithoutVersion()).ToHandle()"));")
        code.close()
    }

    private static func versionType(_ kind: GesHandleVersionKind) -> String? {
        switch kind {
        case .revision: "long"
        case .sha1: "GesHash20"
        case .sha256, .contentHash: "GesHash32"
        case .none: nil
        }
    }

    private static func versionValue(_ kind: GesHandleVersionKind) -> String {
        kind == .revision
            ? "Version is { } version ? \(valueType).GesInteger(version) : \(valueType).GesNothing()"
            : "Version is { } version ? \(valueType).GesBytes(Bytes(version)) : \(valueType).GesNothing()"
    }

    private static func versionRead(_ code: inout Code, _ kind: GesHandleVersionKind) {
        let type = versionType(kind)!
        code.line("\(type)? version = null;")
        if kind == .revision {
            code.line("if (handle.Version.ValueKind == GesBytecodeTypeKind.Integer) version = handle.Version.AsInteger();")
        } else {
            code.line("if (handle.Version.ValueKind == GesBytecodeTypeKind.Bytes && handle.Version.AsBytes().Length == \(type).Length) version = \(type).FromBytes(handle.Version.AsBytes());")
        }
        code.line("else if (!handle.Version.IsNothing) return false;")
    }

    private static func keyValue(_ key: GesHandleKey, _ member: String) -> String {
        key.isText ? "\(valueType).GesText(\(member) ?? \"\")" : "\(valueType).GesInteger(\(member))"
    }

    private static func keyRead(_ code: inout Code, _ key: GesHandleKey, _ expression: String, _ name: String) {
        if key.isText {
            code.line("if (\(expression).ValueKind != GesBytecodeTypeKind.Text) return false;")
            code.line("var \(name) = \(expression).AsText();")
            code.line("if (global::System.Text.Encoding.UTF8.GetByteCount(\(name)) > \(number(key.maxLength))) return false;")
        } else {
            code.line("if (\(expression).ValueKind != GesBytecodeTypeKind.Integer) return false;")
            code.line("var \(name) = \(expression).AsInteger();")
            code.line("if (\(name) < \(number(key.minimum)) || \(name) > \(number(key.maximum))) return false;")
        }
    }

    private static func keyDescription(_ key: GesHandleKey) -> String {
        key.isText ? "a Text of at most \(number(key.maxLength)) UTF-8 bytes" : "an integer between \(number(key.minimum)) and \(number(key.maximum))"
    }

    private static func number(_ value: Int64) -> String {
        value == Int64.min ? "long.MinValue" : String(value) + (value > Int64(Int32.max) || value < Int64(Int32.min) ? "L" : "")
    }

    // Orders two texts by Unicode scalars, as GES orders text ids.
    private static func compareScalars(_ code: inout Code) {
        code.blank()
        code.line("// Orders two texts by Unicode scalars, as GES orders text ids.")
        code.line("private static int CompareScalars(string? left, string? right)")
        code.open("{")
        code.line("if (left is null || right is null) return left is null ? right is null ? 0 : -1 : 1;")
        code.line("var length = Math.Min(left.Length, right.Length);")
        code.line("for (var index = 0; index < length; index++)")
        code.open("{")
        code.line("int a = left[index], b = right[index];")
        code.line("if (a == b) continue;")
        code.line("// A surrogate encodes a scalar above every UTF-16 unit from U+E000.")
        code.line("a = a >= 0xE000 ? a - 0x800 : a >= 0xD800 ? a + 0x2000 : a;")
        code.line("b = b >= 0xE000 ? b - 0x800 : b >= 0xD800 ? b + 0x2000 : b;")
        code.line("return a - b;")
        code.close()
        code.line("return left.Length - right.Length;")
        code.close()
    }

    // The support helpers of maps with handle keys.
    static func handleHelpers(_ code: inout Code, _ conversions: Bool) {
        code.blank()
        code.line("/// <summary>Compares two maps by their keys and values, in any order.</summary>")
        code.line("internal static bool MapEquals<TKey, TValue>(Dictionary<TKey, TValue>? left, Dictionary<TKey, TValue>? right, Func<TValue, TValue, bool> equals) where TKey : notnull")
        code.open("{")
        code.line("if (ReferenceEquals(left, right)) return true;")
        code.line("if (left is null || right is null || left.Count != right.Count) return false;")
        code.line("foreach (var entry in left)")
        code.line("    if (!right.TryGetValue(entry.Key, out var other) || !equals(entry.Value, other)) return false;")
        code.line("return true;")
        code.close()
        code.blank()
        code.line("/// <summary>Hashes a map from its entries, in any order.</summary>")
        code.line("internal static int MapHash<TKey, TValue>(Dictionary<TKey, TValue>? map, Func<TValue, int> hash) where TKey : notnull")
        code.open("{")
        code.line("if (map is null) return 0;")
        code.line("unchecked")
        code.open("{")
        code.line("var result = 23;")
        code.line("foreach (var entry in map) result += (entry.Key.GetHashCode() * 31) ^ hash(entry.Value);")
        code.line("return result;")
        code.close()
        code.close()
        if !conversions { return }
        code.blank()
        code.line("internal static GesValue HandleMap<TKey, TValue>(Dictionary<TKey, TValue> map, Func<TKey, GesValue> key, Func<TValue, GesValue> value) where TKey : notnull")
        code.open("{")
        code.line("var keys = new GesValue[map.Count];")
        code.line("var values = new GesValue[map.Count];")
        code.line("var index = 0;")
        code.line("foreach (var entry in map)")
        code.open("{")
        code.line("keys[index] = key(entry.Key);")
        code.line("values[index++] = value(entry.Value);")
        code.close()
        code.line("return GesValue.GesMapOfKeys(keys, values);")
        code.close()
        code.blank()
        code.line("internal static bool HandleMap<TKey, TValue>(in GesValue value, Decoder<TKey> key, Decoder<TValue> element, out Dictionary<TKey, TValue> result) where TKey : notnull")
        code.open("{")
        code.line("result = null!;")
        code.line("if (value.ValueKind != GesBytecodeTypeKind.Map || value.CustomTypeName is not null || value.MaterializedMap() is not { } map) return false;")
        code.line("var entries = new Dictionary<TKey, TValue>(map.Length);")
        code.line("for (var index = 0; index < map.Length; index++)")
        code.open("{")
        code.line("if (map.HandleKey(index) is not { } handle || !key(GesValue.GesHandle(handle), out var entryKey) || !element(map.ValueAt(index), out var entryValue) || !entries.TryAdd(entryKey, entryValue)) return false;")
        code.close()
        code.blank()
        code.line("result = entries;")
        code.line("return true;")
        code.close()
    }
}
