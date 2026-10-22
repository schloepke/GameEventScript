// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

/// Host implementation of one declared external constructor.
public protocol GesExternalTypeConstructor {
    /// Declarative constructor signature implemented by this callback.
    var definition: GesExternalTypeConstructorDefinition { get }

    /// Reads borrowed arguments and sets an external value or Nothing. Do not retain the call object; thrown faults are
    /// classified by the runtime.
    func invoke(_ call: GesExternalTypeConstructorCall) throws
}
