// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

/// An immutable portable message with ordered arguments and delivery-tag metadata.
public struct GameEventScriptMessage: Hashable, CustomStringConvertible {
    /// The normalized message name.
    public let name: String

    /// The immutable arguments in signature order.
    public let arguments: GameEventScriptMessageArguments

    /// Unique normalized delivery tags in first-occurrence order.
    public let tags: [String]

    /// The normalized name and ordered labels, excluding delivery tags.
    public let signatureId: String

    /// Creates a concrete message, normalizing names and tags and validating argument labels.
    public init(name: String, arguments: [GameEventScriptMessageArgument] = [], tags: [String] = []) throws {
        let normalizedName = try GameEventScriptMessageSignature.normalizeMessageName(name)
        guard !normalizedName.isEmpty else { throw GameEventScriptMessageError.emptyMessageName }
        let orderedArguments = try GameEventScriptMessageArguments(arguments)
        let signatureId = normalizedName + "(" + orderedArguments.signatureLabels.joined(separator: ",") + ")"
        self.init(normalizedName: normalizedName, arguments: orderedArguments, signatureId: signatureId, normalizedTags: try Self.normalizeTags(tags))
    }

    /// Tests for a normalized delivery tag. A leading `#`, surrounding spaces, and tabs are accepted.
    public func hasTag(_ tag: String) throws -> Bool {
        let normalized = try Self.normalizeTagName(tag)
        return !normalized.isEmpty && tags.contains { GesText.scalarEqual($0, normalized) }
    }

    /// Creates a value with additional tags merged in first-occurrence order.
    public func withTags(_ tags: [String]) throws -> GameEventScriptMessage {
        var merged = self.tags
        for tag in try Self.normalizeTags(tags) where !merged.contains(tag) { merged.append(tag) }
        return GameEventScriptMessage(normalizedName: name, arguments: arguments, signatureId: signatureId, normalizedTags: merged)
    }

    /// Formats the message name, arguments, and optional delivery tags.
    public var description: String {
        let message = arguments.isEmpty ? name : name + "(" + arguments.description + ")"
        return tags.isEmpty ? message : message + " with " + tags.map { "#" + $0 }.joined(separator: ", ")
    }

    /// Compares signature, argument values, and ordered delivery tags.
    public static func == (lhs: Self, rhs: Self) -> Bool { GesText.scalarEqual(lhs.signatureId, rhs.signatureId) && lhs.arguments == rhs.arguments && lhs.tags == rhs.tags }

    /// Hashes the message using the same signature, argument, and tag equality semantics.
    public func hash(into hasher: inout Hasher) {
        GesText.hashScalars(signatureId, into: &hasher)
        hasher.combine(arguments)
        for tag in tags { GesText.hashScalars(tag, into: &hasher) }
    }

    init(normalizedName: String, arguments: GameEventScriptMessageArguments, signatureId: String, normalizedTags: [String]) {
        name = normalizedName
        self.arguments = arguments
        self.signatureId = signatureId
        tags = normalizedTags
    }

    private static func normalizeTagName(_ tag: String) throws -> String {
        var normalized = MessageNames.trim(tag)
        if normalized.hasPrefix("#") { normalized.removeFirst() }
        guard !normalized.isEmpty else { return "" }
        guard GesText.isLowerName(normalized) else { throw GameEventScriptMessageError.invalidTag }
        return normalized
    }

    private static func normalizeTags(_ tags: [String]) throws -> [String] {
        var normalized: [String] = []
        normalized.reserveCapacity(tags.count)
        for tag in tags {
            let name = try normalizeTagName(tag)
            if !name.isEmpty && !normalized.contains(name) { normalized.append(name) }
        }
        return normalized
    }
}
