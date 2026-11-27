// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

private let storeEntryKeys = ["key", "value"]

// Store reads `@X[:count]`, `@X[:keys]`, `@X[:values]`, `@X[:entries]` and the same of one root's parts `@X(root)`
// (specs/Language.md#stores, specs/HostRuntime.md#stores).
extension GesVirtualMachine {
    enum StoreSelector {
        case count
        case keys
        case values
        case entries
    }

    // Keys of the per-invocation listing cache: the type and the root of the parts, absent for the whole store.
    struct StoreKey: Hashable {
        let type: String
        let root: GesHandle?
    }

    func clearStoreHandles() {
        if !storeHandles.isEmpty { storeHandles.removeAll(keepingCapacity: true) }
    }

    func readStore(_ destination: Int, _ typeText: String, _ selector: StoreSelector) throws {
        guard let binding = storeBinding(typeText) else {
            setNothing(destination)
            return
        }

        try writeStore(destination, binding, try listStoreHandles(binding, nil), selector)
    }

    func readStoreParts(_ destination: Int, _ typeText: String, _ rootValue: GesValue, _ selector: StoreSelector) throws {
        // Only a handle of the root type names a root; any other value, like a type without a resolver, reads nothing.
        guard let binding = storeBinding(typeText), binding.type.part != nil, let rootType = binding.type.root, let given = rootValue.handleValue, !given.isComposite,
            given.typeName.utf8.elementsEqual(rootType.name.utf8)
        else {
            setNothing(destination)
            return
        }

        try writeStore(destination, binding, try listStoreHandles(binding, given.withView(.nothing, "")), selector)
    }

    private func storeBinding(_ typeText: String) -> GesHandleBinding? {
        guard let resolvers = linked!.handleResolvers else { return nil }
        return resolvers[String(typeText.unicodeScalars.dropFirst())]
    }

    private func writeStore(_ destination: Int, _ binding: GesHandleBinding, _ handles: [GesHandle]?, _ selector: StoreSelector) throws {
        guard let handles else {
            setNothing(destination)
            return
        }

        switch selector {
        case .count:
            setInteger(destination, Int64(handles.count))
            return
        case .keys:
            setList(destination, handles.map { .handle($0) })
            return
        case .values, .entries:
            break
        }

        let states = try storeStates(binding, handles)
        if selector == .values {
            setList(destination, states)
            return
        }

        var entries: [GesValue] = []
        entries.reserveCapacity(handles.count)
        for index in 0..<handles.count {
            entries.append(.map(GesValueMap(canonicalKeys: storeEntryKeys, values: [.handle(handles[index]), states[index]])))
        }
        setList(destination, entries)
    }

    // The resolver's handles of the store, or of one root's parts, checked, without version or view and in key order; the
    // first answer of the invocation is reused.
    private func listStoreHandles(_ binding: GesHandleBinding, _ root: GesHandle?) throws -> [GesHandle]? {
        let type = binding.type
        let key = StoreKey(type: type.name, root: root)
        if let cached = storeHandles[key] { return cached }
        var handles: [GesHandle]?
        if let listed = try GesHandleStates.handles(binding, root) {
            let composite = type.part != nil
            var checked: [GesHandle] = []
            checked.reserveCapacity(listed.count)
            for handle in listed {
                if !handle.typeName.utf8.elementsEqual(type.name.utf8) || handle.isComposite != composite {
                    throw GesHandleStates.invalidHandles(type, "the handle \(handle) of another type")
                }
                if let root, GesHandle.fromWire(typeName: root.typeName, id: handle.id, version: .nothing, part: .nothing, isComposite: false) != root {
                    throw GesHandleStates.invalidHandles(type, "the handle \(handle) of another root")
                }
                checked.append(handle.withView(.nothing, ""))
            }

            checked.sort { GesHandle.compare($0, $1) < 0 }
            for index in checked.indices.dropFirst() where checked[index] == checked[index - 1] {
                throw GesHandleStates.invalidHandles(type, "the handle \(checked[index]) twice")
            }
            handles = checked
        }

        storeHandles[key] = .some(handles)
        return handles
    }

    // The full states of the handles, nothing for a missing or invisible one; uncached ones resolve in one batch.
    private func storeStates(_ binding: GesHandleBinding, _ handles: [GesHandle]) throws -> [GesValue] {
        let batch = handleResolveBatch
        do {
            defer { batch.end() }
            for handle in handles where handleStates[HandleStateKey(handle: handle)] == nil {
                _ = batch.add(handle, binding.type, nil, handleParty)
            }
            if batch.count > 0 {
                try GesHandleStates.resolveMany(binding, batch)
                for index in 0..<batch.count { cacheHandleState(batch[index].handle, try GesHandleStates.check(binding.type, batch[index])) }
            }
        }

        return handles.map { handleStates[HandleStateKey(handle: $0)]!.state }
    }
}
