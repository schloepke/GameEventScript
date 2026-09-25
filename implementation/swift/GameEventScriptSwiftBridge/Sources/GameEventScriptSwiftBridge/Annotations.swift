// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import GameEventScriptRuntime

/// Generates `static func createGesType() throws -> GameEventScriptSwiftType<Self>` on a struct or final class.
/// Only members marked with `@GesField` or `@GesConstruct` are exposed. The optional name defaults to the Swift type name.
/// Each factory call creates a distinct descriptor: retain and reuse it for wrapping, unwrapping and registration.
/// Declaration validation errors are reported during compilation; portable name/catalog errors throw when creating the descriptor.
@attached(member, names: named(createGesType))
public macro GesType(_ name: String? = nil) = #externalMacro(module: "GameEventScriptSwiftBridgeMacros", type: "GesTypeMacro")

/// Exposes one stored or computed instance property of an enclosing `@GesType`.
/// The name defaults to the property name. Standard Swift scalar/collection types are inferred from explicit type annotations;
/// aliases, custom convertible types and GesValue require `typeName`. A numeric `unit` explicitly maps native unitless numbers
/// to a GES quantity; it cannot be combined with `typeName`. This marker never adds a setter or reads a property during registration.
@attached(peer)
public macro GesField(_ name: String? = nil, typeName: String? = nil, unit: GesUnit = .none) = #externalMacro(module: "GameEventScriptSwiftBridgeMacros", type: "GesFieldMacro")

/// Exposes a synchronous, nonfailable initializer or static factory of an enclosing `@GesType`.
/// Parameters map to annotated fields by external label, then internal name, and inherit their field declarations.
/// Arguments use strict Swift conversion; declared quantity units are checked and removed before native numeric conversion.
/// Throwing constructors retain normal external-callback error classification. Unmarked constructors remain inaccessible to scripts.
@attached(peer)
public macro GesConstruct() = #externalMacro(module: "GameEventScriptSwiftBridgeMacros", type: "GesConstructMacro")
