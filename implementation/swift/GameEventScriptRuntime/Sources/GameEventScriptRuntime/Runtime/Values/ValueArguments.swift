// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

/// Ordered callback arguments, optionally borrowed from an active VM register frame.
public struct GesValueArguments {
    private let values: [GesValue]
    private let state: GesVirtualMachine?
    private let indexes: [UInt16]
    // The extension call this view belongs to; a retained view reads as empty once that call has ended instead of
    // reading whatever a later handler left in the registers.
    private let generation: Int
    private var isCurrent: Bool { state.map { $0.extensionCallGeneration == generation } ?? true }
    /// Number of arguments in the current view; zero once the extension call that lent the view has ended.
    public var count: Int { state == nil ? values.count : isCurrent ? indexes.count : 0 }

    /// Creates an owned argument view over the supplied values.
    public init(_ values: [GesValue] = []) {
        self.values = values
        state = nil
        indexes = []
        generation = 0
    }

    init(state: GesVirtualMachine, indexes: [UInt16]) {
        self.state = state
        self.indexes = indexes
        generation = state.extensionCallGeneration
        values = []
    }

    /// Reads a zero-based argument; returns Nothing for an invalid index and for every index once the extension call
    /// that lent the view has ended.
    public subscript(index: Int) -> GesValue {
        guard index >= 0 && index < count else { return .nothing }
        return state.map { $0.value(Int(indexes[index])) } ?? values[index]
    }
}
