// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import GameEventScriptRuntime

/// Generates `static func createGesType() throws -> GesBridgeTypeBinding<Self>` on a struct or final class.
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
public macro GesField(_ name: String? = nil, typeName: String? = nil, unit: GesQuantityUnit = .none) = #externalMacro(module: "GameEventScriptSwiftBridgeMacros", type: "GesFieldMacro")

/// Exposes a synchronous, nonfailable initializer or static factory of an enclosing `@GesType`.
/// Parameters map to annotated fields by external label, then internal name, and inherit their field declarations.
/// Arguments use strict Swift conversion; declared quantity units are checked and removed before native numeric conversion.
/// Throwing constructors retain normal external-callback error classification. Unmarked constructors remain inaccessible to scripts.
@attached(peer)
public macro GesConstruct() = #externalMacro(module: "GameEventScriptSwiftBridgeMacros", type: "GesConstructMacro")

/// Generates `gesExtensions()` on an enum, struct or final class: the `GesBridgeExtension` list of its `@GesFunction` and
/// `@GesPredicate` methods in the extension namespace `namespace`, so that signature and implementation cannot drift apart.
/// Static methods yield `static func gesExtensions() throws -> [GesBridgeExtension]`; instance methods yield an instance
/// `func gesExtensions() throws -> [GesBridgeExtension]` with the static ones as well, whose closures retain the instance (a
/// struct is copied, a class referenced) for as long as the registry holds them. Arguments convert strictly through
/// `GesBridgeValueConvertible`, and an argument that does not convert, or another argument count, gives `nothing`, as GES
/// operations do for a domain failure; a thrown Swift error stays an extension fault.
@attached(member, names: named(gesExtensions))
public macro GesExtension(_ namespace: String) = #externalMacro(module: "GameEventScriptSwiftBridgeMacros", type: "GesExtensionMacro")

/// Exposes a method of an enclosing `@GesExtension` as `:namespace.name(...)`; the name defaults to the Swift name, and the
/// argument labels are the Swift labels, `_` for an unlabeled argument. `unit` is attached to a numeric or spatial result,
/// and `units` maps Swift parameter names to the units their arguments must have, which are checked and removed; Swift has
/// no attributes on parameters, so they are named here. A `GesValue` parameter receives the raw argument, and an `Optional`
/// result returns `nothing` for nil. GES has no purity contract for host calls, so scripts treat the function as effectful.
@attached(peer)
public macro GesFunction(_ name: String? = nil, unit: GesQuantityUnit = .none, units: [String: GesQuantityUnit] = [:]) =
    #externalMacro(module: "GameEventScriptSwiftBridgeMacros", type: "GesFunctionMacro")

/// Exposes a method of an enclosing `@GesExtension` with one parameter and a `Bool` or `Bool?` result as the predicate of
/// `value is :namespace.name`, registered with one unlabeled argument whatever its Swift label; nil gives `nothing`.
@attached(peer)
public macro GesPredicate(_ name: String? = nil, units: [String: GesQuantityUnit] = [:]) =
    #externalMacro(module: "GameEventScriptSwiftBridgeMacros", type: "GesFunctionMacro")
