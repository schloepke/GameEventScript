// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

/// Independently selectable portable debug sections. All sections are enabled by default.
public struct GesDebugInfoOptions: OptionSet, Sendable {
    /// Bit mask selecting portable debug sections.
    public let rawValue: UInt8

    /// Creates an option mask; unsupported bits are rejected when compilation is requested.
    public init(rawValue: UInt8) { self.rawValue = rawValue }

    /// Retains register names and instruction lifetimes.
    public static let symbols = Self(rawValue: 1)
    /// Retains bytecode-to-source mappings.
    public static let sourceMap = Self(rawValue: 2)
    /// Embeds original source documents.
    public static let sourceArchive = Self(rawValue: 4)
    /// Enables all three portable debug sections.
    public static let all: Self = [.symbols, .sourceMap, .sourceArchive]
    /// Omits optional debug sections for size-sensitive Programs.
    public static let none: Self = []
}
