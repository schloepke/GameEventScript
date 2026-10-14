// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

/// Role of the optional symbol identified by a diagnostic.
public enum GesSymbolKind: Sendable {
    /// No specific symbol category is available.
    case unknown
    /// A source or external type.
    case type
    /// A declared predicate.
    case predicate
    /// A declared function.
    case function
    /// A message handler.
    case handler
    /// A message signature or emission.
    case message
    /// A lexical variable binding.
    case variable
    /// A program-wide definition.
    case globalDefinition
}
