// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

/// A validation failure in the portable ordered message API.
public enum GesMessageError: Error, Equatable {
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
