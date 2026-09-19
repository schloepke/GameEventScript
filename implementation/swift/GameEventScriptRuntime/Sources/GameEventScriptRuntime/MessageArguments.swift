// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

/// One immutable value and its normalized external message argument label.
public struct GameEventScriptMessageArgument: Hashable {
    /// The external label, or `_` for a positional argument.
    public let name: String

    /// The argument value.
    public let value: GesValue

    /// Normalizes the supplied label and retains the value using Swift value semantics.
    public init(name: String? = nil, value: GesValue) throws {
        self.name = try GameEventScriptMessageSignature.normalizeParameterName(name)
        self.value = value
    }

    /// Compares the normalized label and value.
    public static func == (lhs: Self, rhs: Self) -> Bool {
        GesText.scalarEqual(lhs.name, rhs.name) && lhs.value == rhs.value
    }

    /// Hashes the normalized label and value using their equality semantics.
    public func hash(into hasher: inout Hasher) {
        GesText.hashScalars(name, into: &hasher)
        hasher.combine(value)
    }

    init(normalizedName: String, value: GesValue) {
        name = normalizedName
        self.value = value
    }
}

/// Immutable ordered message arguments. Named labels are unique; `_` may repeat.
public struct GameEventScriptMessageArguments: Sequence, Hashable, CustomStringConvertible {
    /// An empty ordered argument collection.
    public static var empty: Self { GameEventScriptMessageArguments(normalizedArguments: []) }

    private let arguments: [GameEventScriptMessageArgument]

    /// Copies an ordered argument list and validates named-label uniqueness.
    public init(_ arguments: [GameEventScriptMessageArgument] = []) throws {
        var names = Set<String>()
        for argument in arguments where argument.name != GameEventScriptMessageSignature.unlabeledParameterName {
            guard names.insert(argument.name).inserted else {
                throw GameEventScriptMessageError.duplicateArgumentName
            }
        }
        self.arguments = arguments
    }

    /// The number of positional and named arguments.
    public var count: Int { arguments.count }

    /// Whether the collection has no arguments.
    public var isEmpty: Bool { arguments.isEmpty }

    /// The signature labels in argument order.
    public var signatureLabels: [String] { arguments.map(\.name) }

    /// The labels in argument order, including repeated positional labels.
    public var keys: [String] { signatureLabels }

    /// The values in argument order.
    public var values: [GesValue] { arguments.map(\.value) }

    /// The value at an existing argument index.
    public subscript(index: Int) -> GesValue { arguments[index].value }

    /// The normalized label at an existing argument index.
    public func nameAt(_ index: Int) -> String { arguments[index].name }

    /// The value at an existing argument index.
    public func valueAt(_ index: Int) -> GesValue { arguments[index].value }

    /// The storage kind at an existing argument index.
    public func kindAt(_ index: Int) -> GesValueKind { arguments[index].value.kind }

    /// The unit at an existing argument index.
    public func unitAt(_ index: Int) -> GesUnit { arguments[index].value.unit }

    /// Whether the value at an existing index carries a value.
    public func hasValueAt(_ index: Int) -> Bool { arguments[index].value.hasValue }

    /// Whether the value at an existing index is nothing or scalar NaN.
    public func isNothingAt(_ index: Int) -> Bool { arguments[index].value.isNothing }

    /// Whether the value at an existing index is numeric.
    public func isNumericAt(_ index: Int) -> Bool { arguments[index].value.isNumeric }

    /// Converts the value at an existing index to a saturated signed 64-bit integer.
    public func getAsInteger(_ index: Int) -> Int64 { arguments[index].value.asInteger }

    /// Converts the value at an existing index to a binary64 number.
    public func getAsNumber(_ index: Int) -> Double { arguments[index].value.asNumber }

    /// Converts the value at an existing index to a Boolean.
    public func getAsBoolean(_ index: Int) -> Bool { arguments[index].value.asBoolean }

    /// Returns API text for an existing index, including the raw name of a Tag.
    public func getAsText(_ index: Int) -> String { arguments[index].value.asText }

    /// Converts a named argument to a saturated signed 64-bit integer.
    public func getAsInteger(_ name: String) throws -> Int64 { try value(named: name).asInteger }

    /// Converts a named argument to a binary64 number.
    public func getAsNumber(_ name: String) throws -> Double { try value(named: name).asNumber }

    /// Converts a named argument to a Boolean.
    public func getAsBoolean(_ name: String) throws -> Bool { try value(named: name).asBoolean }

    /// Returns API text for a named argument, including the raw name of a Tag.
    public func getAsText(_ name: String) throws -> String { try value(named: name).asText }

    /// Finds a normalized named label. Positional `_` arguments cannot be found by name.
    public func indexOf(_ name: String?) throws -> Int? {
        let normalized = try GameEventScriptMessageSignature.normalizeParameterName(name)
        guard normalized != GameEventScriptMessageSignature.unlabeledParameterName else { return nil }
        return arguments.firstIndex { GesText.scalarEqual($0.name, normalized) }
    }

    /// Whether a normalized named label is present.
    public func containsKey(_ name: String?) throws -> Bool { try indexOf(name) != nil }

    /// Finds the value for a named label, throwing when the label is absent.
    public func value(named name: String) throws -> GesValue {
        guard let index = try indexOf(name) else { throw GameEventScriptMessageError.missingArgument }
        return arguments[index].value
    }

    /// Iterates argument pairs in their original order.
    public func makeIterator() -> IndexingIterator<[GameEventScriptMessageArgument]> {
        arguments.makeIterator()
    }

    /// Formats ordered labels and language text values for message display.
    public var description: String {
        arguments.map { $0.name + ": " + $0.value.description }.joined(separator: ", ")
    }

    init(normalizedArguments: [GameEventScriptMessageArgument]) { arguments = normalizedArguments }
}
