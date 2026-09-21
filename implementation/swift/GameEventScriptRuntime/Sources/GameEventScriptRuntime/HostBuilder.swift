// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

/// Mutable single-caller configuration. Each build creates an independent Host and private random stream.
public final class GameEventScriptHostBuilder {
    private var seed: Int64?
    private var sequence: [Double] = []
    private var limits = GameEventScriptRuntimeLimits()
    private var observer: (any GameEventScriptRuntimeObserver)?
    private var extensions: (any GameEventScriptExtensionRegistry)?
    private var externalTypes: (any GameEventScriptExternalTypeRegistry)?
    private var sink: (any GameEventScriptPublishSink)?

    /// Creates a builder with default limits and no native registries, observer or publish sink.
    public init() {}

    /// Selects deterministic seeding and clears any configured start sequence. Returns this builder.
    @discardableResult public func withRandomSeed(_ value: Int64) -> Self {
        seed = value
        sequence = []
        return self
    }

    /// Mixes nonempty entropy into a deterministic seed and clears the start sequence. Returns this builder.
    ///
    /// - Throws: An API error for empty entropy.
    @discardableResult public func withRandomEntropy(_ bytes: [UInt8]) throws -> Self {
        seed = try GameEventScriptRandomGenerator.seedFromEntropy(bytes)
        sequence = []
        return self
    }

    /// Copies initial draws, then uses a private PRNG seeded by `fallbackSeed` or fresh entropy. Returns this builder.
    @discardableResult public func withRandomSequence(_ values: [Double], fallbackSeed: Int64? = nil) -> Self {
        sequence = values
        seed = fallbackSeed
        return self
    }

    /// Sets host execution and resource limits. Returns this builder.
    @discardableResult public func withRuntimeLimits(_ value: GameEventScriptRuntimeLimits) -> Self {
        limits = value
        return self
    }

    /// Sets the synchronous observer for emissions, dispatch and failures. Returns this builder.
    @discardableResult public func withRuntimeObserver(_ value: any GameEventScriptRuntimeObserver) -> Self {
        observer = value
        return self
    }

    /// Sets the runtime extension resolver used while linking Programs. Returns this builder.
    @discardableResult public func withRegistry(_ value: any GameEventScriptExtensionRegistry) -> Self {
        extensions = value
        return self
    }

    /// Sets the runtime external-constructor resolver. Returns this builder.
    @discardableResult public func withExternalTypeRegistry(_ value: any GameEventScriptExternalTypeRegistry) -> Self {
        externalTypes = value
        return self
    }

    /// Sets the optional synchronous outbound publish sink. Returns this builder.
    @discardableResult public func withPublishSink(_ value: any GameEventScriptPublishSink) -> Self {
        sink = value
        return self
    }

    /// Creates an independent loading host without executing initialization. Load all initial Programs, then call
    /// `start()`.
    ///
    /// - Throws: An API error for invalid host configuration.
    public func build() throws -> GameEventScriptHost { try .init(seed: seed, sequence: sequence, limits: limits, observer: observer, extensions: extensions, externalTypes: externalTypes, publishSink: sink) }
}
