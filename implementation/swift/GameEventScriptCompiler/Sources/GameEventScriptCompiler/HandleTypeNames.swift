// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import GameEventScriptRuntime

// Static handle type names as the binder keeps them: `@Name`, `@Name~`, `@Name(:P)`, `@Name(:P)~` and unions joined
// with `|`, as GesHandleTypeNames of the C# compiler (specs/Language.md#handles, #projections).
enum GesHandleTypeNames {
    // Whether a declared or static type names a handle type or a union, which is only of handle types.
    static func isHandleType(_ typeName: String?) -> Bool {
        guard let typeName, let first = typeName.unicodeScalars.first else { return false }
        return first == "@" || typeName.unicodeScalars.contains("|")
    }

    // The alternatives of a type name in source order.
    static func alternatives(_ typeName: String) -> [String] { typeName.split(separator: "|", omittingEmptySubsequences: false).map(String.init) }

    // The name without `@` and projection and whether `~` requires a version.
    static func split(_ alternative: String) -> (name: String, versioned: Bool) {
        var scalars = Substring(alternative).unicodeScalars
        let versioned = scalars.last == "~"
        if versioned { scalars.removeLast() }
        if scalars.first == "@" { scalars.removeFirst() }
        if let open = scalars.firstIndex(of: "(") { scalars = scalars[..<open] }
        return (String(scalars), versioned)
    }

    // The projection an alternative reads through, without `:`, or "" for the full view.
    static func projection(_ alternative: String) -> String {
        let scalars = Substring(alternative).unicodeScalars
        guard let open = scalars.firstIndex(of: "(") else { return "" }
        var start = scalars.index(after: open)
        if start < scalars.endIndex, scalars[start] == ":" { start = scalars.index(after: start) }
        let close = scalars[open...].firstIndex(of: ")") ?? scalars.endIndex
        return String(scalars[start..<max(start, close)])
    }

    // Whether any alternative reads through a projection.
    static func hasProjection(_ typeName: String) -> Bool { typeName.unicodeScalars.contains("(") }

    // An alternative as written in source: a handle type as is, any other type with its `:` and its capitalized name,
    // such as `:Number` for the binder's `number`.
    static func sourceName(_ alternative: String) -> String {
        guard let first = alternative.first, first != "@" else { return alternative }
        return ":" + first.uppercased() + alternative.dropFirst()
    }

    // The alternatives' names without `@`, in source order.
    static func names(_ typeName: String) -> [String] { alternatives(typeName).map { split($0).name } }

    // Whether the alternatives share a root and one of them is a part-less composite, whose `id` is a root without a part.
    static func hasPartlessComposite(_ typeName: String, _ handleTypes: [String: GesHandleType]) -> Bool {
        guard commonRoot(typeName, handleTypes) != nil else { return false }
        return alternatives(typeName).contains { handleTypes[split($0).name]?.part == nil }
    }

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
