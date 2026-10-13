// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

/// Synchronous portable extension callback.
public protocol GameEventScriptExtensionFunction {
    /// Reads borrowed arguments and writes the result through the call object. Do not retain the call or its argument
    /// view after returning. Thrown faults are classified at the runtime boundary.
    func invoke(_ call: GesExtensionCall) throws
}
