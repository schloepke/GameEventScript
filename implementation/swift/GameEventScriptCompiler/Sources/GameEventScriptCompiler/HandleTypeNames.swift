// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import GameEventScriptRuntime

// Static handle type names as the binder keeps them: `@Name`, `@Name~` and unions joined with `|`, as
// GesHandleTypeNames of the C# compiler (specs/Language.md#handles).
enum GesHandleTypeNames {
    // Whether a declared or static type names a handle type or a union, which is only of handle types.
    static func isHandleType(_ typeName: String?) -> Bool {
        guard let typeName, let first = typeName.unicodeScalars.first else { return false }
        return first == "@" || typeName.unicodeScalars.contains("|")
    }

    // The alternatives of a type name in source order.
    static func alternatives(_ typeName: String) -> [String] { typeName.split(separator: "|", omittingEmptySubsequences: false).map(String.init) }

    // The name without `@` and whether `~` requires a version.
    static func split(_ alternative: String) -> (name: String, versioned: Bool) {
        var scalars = Substring(alternative).unicodeScalars
        let versioned = scalars.last == "~"
        if versioned { scalars.removeLast() }
        if scalars.first == "@" { scalars.removeFirst() }
        return (String(scalars), versioned)
    }

    // The alternatives' names without `@`, in source order.
    static func names(_ typeName: String) -> [String] { alternatives(typeName).map { split($0).name } }

    // The root type all alternatives share, or nil when one is no declared composite or the roots differ.
    static func commonRoot(_ typeName: String, _ handleTypes: [String: GesHandleType]) -> GesHandleType? {
        var common: GesHandleType?
        for alternative in alternatives(typeName) {
            guard let type = handleTypes[split(alternative).name], let root = type.root else { return nil }
            if let common, common.name != root.name { return nil }
            common = root
        }
        return common
    }
}
