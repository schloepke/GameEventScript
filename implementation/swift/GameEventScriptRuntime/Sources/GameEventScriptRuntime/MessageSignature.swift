// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

/// A validation failure in the portable ordered message API.
public enum GameEventScriptMessageError: Error, Equatable {
    /// A message name does not follow the ASCII message name grammar.
    case invalidName
    /// An argument label does not follow the ASCII label grammar.
    case invalidParameterName
    /// A named argument occurs more than once after normalization.
    case duplicateArgumentName
    /// A delivery tag does not follow the ASCII tag grammar.
    case invalidTag
    /// A concrete message cannot have an empty name.
    case emptyMessageName
    /// The number of values does not match the signature's arity.
    case argumentCountMismatch
    /// No named argument matches the requested label.
    case missingArgument
}

/// An immutable message identity: its name and ordered external argument labels.
public struct GameEventScriptMessageSignature: Hashable, CustomStringConvertible, Sendable {
    /// The label assigned to a positional argument without an external name.
    public static let unlabeledParameterName = "_"

    /// An empty identity that cannot construct or match a concrete message.
    public static let empty = GameEventScriptMessageSignature(normalizedName: "", parameters: [])

    /// The normalized message name.
    public let name: String

    /// The normalized external labels, in declaration order.
    public let parameters: [String]

    /// The stable language-neutral signature spelling, such as `Done(value)`.
    public let signatureId: String

    /// Creates a signature, normalizing names and rejecting duplicate named labels.
    /// Empty message names represent an empty identity; positional `_` labels may repeat.
    public init(name: String?, parameters: [String?] = []) throws {
        let normalizedName = try Self.normalizeMessageName(name)
        var labels: [String] = []
        labels.reserveCapacity(parameters.count)
        for parameter in parameters {
            let label = try Self.normalizeParameterName(parameter)
            if label != Self.unlabeledParameterName && labels.contains(label) {
                throw GameEventScriptMessageError.duplicateArgumentName
            }
            labels.append(label)
        }
        self.init(normalizedName: normalizedName, parameters: labels)
    }

    /// Removes surrounding ASCII spaces and tabs and validates the message name.
    /// Nil and an empty result normalize to the empty identity.
    public static func normalizeMessageName(_ name: String?) throws -> String {
        let normalized = MessageNames.trim(name ?? "")
        if normalized.isEmpty || normalized == "initialization" || normalized == "undeliverable" {
            return normalized
        }
        guard GesText.isUpperName(normalized) else {
            throw GameEventScriptMessageError.invalidName
        }
        return normalized
    }

    /// Removes surrounding ASCII spaces and tabs and validates an external label.
    /// Nil, empty, and whitespace-only inputs normalize to `_`.
    public static func normalizeParameterName(_ name: String?) throws -> String {
        let normalized = MessageNames.trim(name ?? "")
        if normalized.isEmpty || normalized == unlabeledParameterName { return unlabeledParameterName }
        guard GesText.isLowerName(normalized) else {
            throw GameEventScriptMessageError.invalidParameterName
        }
        return normalized
    }

    /// Formats the normalized name and labels without constructing a signature.
    /// Label order and repeated positional labels are preserved.
    public static func createSignatureId(name: String?, parameterNames: [String?] = []) throws -> String {
        let normalizedName = try normalizeMessageName(name)
        let normalizedLabels = try parameterNames.map(normalizeParameterName)
        return normalizedName + "(" + normalizedLabels.joined(separator: ",") + ")"
    }

    /// Whether the concrete message has this name and ordered label identity.
    public func matches(_ message: GameEventScriptMessage) -> Bool {
        GesText.scalarEqual(signatureId, message.signatureId)
    }

    /// Binds ordered values to this signature, or returns nil for an empty name or wrong arity.
    public func createMessage(_ arguments: [GesValue]) -> GameEventScriptMessage? {
        guard !name.isEmpty, arguments.count == parameters.count else { return nil }
        let pairs = zip(parameters, arguments).map {
            GameEventScriptMessageArgument(normalizedName: $0.0, value: $0.1)
        }
        return GameEventScriptMessage(
            normalizedName: name,
            arguments: GameEventScriptMessageArguments(normalizedArguments: pairs),
            signatureId: signatureId,
            normalizedTags: []
        )
    }

    /// Binds ordered values to this signature, throwing when a concrete message cannot be created.
    public func withArguments(_ arguments: [GesValue]) throws -> GameEventScriptMessage {
        guard let message = createMessage(arguments) else {
            throw GameEventScriptMessageError.argumentCountMismatch
        }
        return message
    }

    /// The stable signature spelling.
    public var description: String { signatureId }

    /// Compares exact signature identities without Unicode normalization.
    public static func == (lhs: Self, rhs: Self) -> Bool {
        GesText.scalarEqual(lhs.signatureId, rhs.signatureId)
    }

    /// Hashes the signature identity; the numeric hash is process-local.
    public func hash(into hasher: inout Hasher) {
        GesText.hashScalars(signatureId, into: &hasher)
    }

    private init(normalizedName: String, parameters: [String]) {
        name = normalizedName
        self.parameters = parameters
        signatureId = normalizedName + "(" + parameters.joined(separator: ",") + ")"
    }
}

enum MessageNames {
    static func trim(_ input: String) -> String {
        let scalars = input.unicodeScalars
        var start = scalars.startIndex
        var end = scalars.endIndex
        while start < end && isSpace(scalars[start].value) { scalars.formIndex(after: &start) }
        while start < end {
            let previous = scalars.index(before: end)
            if !isSpace(scalars[previous].value) { break }
            end = previous
        }
        return String(scalars[start..<end])
    }

    private static func isSpace(_ scalar: UInt32) -> Bool { scalar == 32 || scalar == 9 }
}
