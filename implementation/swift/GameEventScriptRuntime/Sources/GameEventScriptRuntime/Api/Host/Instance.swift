// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

/// A host-local loaded Program handle. Detachment is idempotent and does not revoke ordinary captured deliveries.
public final class GesProgramInstance {
    private weak var host: GameEventScriptHost?
    /// Stable registration identifier, unique within the owning host.
    public let registrationID: Int64
    let linked: GesLinkedProgram
    /// The immutable Program shared by this instance.
    public var program: GameEventScriptProgram { linked.program }
    /// Whether the owning host still registers this instance.
    public var isAttached: Bool { host?.isAttached(registrationID) ?? false }
    /// Nil while initialization is pending; retains its success or failure result after completion.
    public internal(set) var startResult: GesStartResult? { didSet { initializationFailed = startResult != nil && startResult?.state != .ready } }
    private(set) var initializationFailed = false

    init(host: GameEventScriptHost, id: Int64, linked: GesLinkedProgram) {
        self.host = host
        registrationID = id
        self.linked = linked
    }

    /// Removes this instance from future subscription snapshots. Returns true only when this call removed a
    /// registration.
    @discardableResult public func detach() -> Bool { host?.detach(registrationID) ?? false }
}
