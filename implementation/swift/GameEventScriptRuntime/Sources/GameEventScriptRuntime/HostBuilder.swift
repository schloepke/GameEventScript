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
    public init() {}
    @discardableResult public func withRandomSeed(_ value: Int64) -> Self {
        seed = value
        sequence = []
        return self
    }
    @discardableResult public func withRandomEntropy(_ bytes: [UInt8]) throws -> Self {
        seed = try GameEventScriptRandomGenerator.seedFromEntropy(bytes)
        sequence = []
        return self
    }
    @discardableResult public func withRandomSequence(_ values: [Double], fallbackSeed: Int64? = nil) -> Self {
        sequence = values
        seed = fallbackSeed
        return self
    }
    @discardableResult public func withRuntimeLimits(_ value: GameEventScriptRuntimeLimits) -> Self {
        limits = value
        return self
    }
    @discardableResult public func withRuntimeObserver(_ value: any GameEventScriptRuntimeObserver) -> Self {
        observer = value
        return self
    }
    @discardableResult public func withRegistry(_ value: any GameEventScriptExtensionRegistry) -> Self {
        extensions = value
        return self
    }
    @discardableResult public func withExternalTypeRegistry(_ value: any GameEventScriptExternalTypeRegistry) -> Self {
        externalTypes = value
        return self
    }
    @discardableResult public func withPublishSink(_ value: any GameEventScriptPublishSink) -> Self {
        sink = value
        return self
    }
    public func build() throws -> GameEventScriptHost {
        try .init(
            seed: seed, sequence: sequence, limits: limits, observer: observer, extensions: extensions,
            externalTypes: externalTypes, publishSink: sink)
    }
}
