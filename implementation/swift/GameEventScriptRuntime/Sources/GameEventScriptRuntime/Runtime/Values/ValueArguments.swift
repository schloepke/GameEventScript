// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

/// Ordered callback arguments, optionally borrowed from an active VM register frame.
public struct GesValueArguments {
    private let values: [GesValue]
    private let state: GesVirtualMachine?
    private let indexes: [UInt16]
    /// Number of arguments in the current view.
    public var count: Int { state == nil ? values.count : indexes.count }

    /// Creates an owned argument view over the supplied values.
    public init(_ values: [GesValue] = []) {
        self.values = values
        state = nil
        indexes = []
    }

    init(state: GesVirtualMachine, indexes: [UInt16]) {
        self.state = state
        self.indexes = indexes
        values = []
    }

    /// Reads a zero-based argument; returns Nothing for an invalid index.
    public subscript(index: Int) -> GesValue {
        guard index >= 0 && index < count else { return .nothing }
        return state.map { $0.value(Int(indexes[index])) } ?? values[index]
    }
}
