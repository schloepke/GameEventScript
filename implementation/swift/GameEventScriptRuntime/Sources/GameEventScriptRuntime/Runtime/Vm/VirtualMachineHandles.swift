// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

private let compositeIdKeys = ["part", "root"]
private let rootIdKeys = ["root"]

// Handle members, type checks and lazy state reads (specs/Language.md#handles, specs/HostRuntime.md#handles).
extension GesVirtualMachine {
    // Keys of the per-invocation state cache: type and identity as handle equality has them, the carried version and the
    // projection.
    struct HandleStateKey: Hashable {
        let handle: GesHandle

        static func == (left: Self, right: Self) -> Bool {
            left.handle == right.handle && GesHandleStates.versionEquals(left.handle.version, right.handle.version) && left.handle.projection.utf8.elementsEqual(right.handle.projection.utf8)
        }

        func hash(into hasher: inout Hasher) { handle.hash(into: &hasher) }
    }

    func clearHandleStates() {
        if !handleStates.isEmpty { handleStates.removeAll(keepingCapacity: true) }
        if !handleLists.isEmpty { handleLists.removeAll(keepingCapacity: true) }
        handleListsCollected = false
        clearStoreHandles()
        handleContext = nil
    }

    // The parts of a handle type text `@Name`, `@Name(:P)`, `@Name~` or `@Name(:P)~`, located without allocating.
    struct HandleTypeText {
        let text: String.UTF8View
        let nameLength: Int
        let projectionStart: Int
        let projectionLength: Int
        let versioned: Bool

        init(_ typeName: String) {
            let utf8 = typeName.utf8
            text = utf8
            versioned = utf8.last == UInt8(ascii: "~")
            let end = utf8.count - (versioned ? 1 : 0)
            let open = utf8.firstIndex(of: UInt8(ascii: "(")).map { utf8.distance(from: utf8.startIndex, to: $0) } ?? -1
            if open < 0 || open >= end {
                nameLength = end - 1
                projectionStart = 0
                projectionLength = 0
            } else {
                nameLength = open - 1
                projectionStart = open + 2
                projectionLength = end - 1 - projectionStart
            }
        }

        var projected: Bool { projectionLength > 0 }

        func namesType(_ typeName: String) -> Bool { typeName.utf8.count == nameLength && typeName.utf8.elementsEqual(slice(1, nameLength)) }

        func namesProjection(_ projection: String) -> Bool { projection.utf8.count == projectionLength && projection.utf8.elementsEqual(slice(projectionStart, projectionLength)) }

        var projection: String { String(decoding: slice(projectionStart, projectionLength), as: UTF8.self) }

        private func slice(_ start: Int, _ length: Int) -> Substring.UTF8View {
            let from = text.index(text.startIndex, offsetBy: start)
            return Substring(text[from..<text.index(from, offsetBy: length)]).utf8
        }
    }

    // `@Name` matches a handle of that type with the full view, `@Name(:P)` one whose view is `P` or can narrow to it,
    // and `~` one that also carries a version.
    func isHandleType(_ value: GesValue, _ typeName: String) -> Bool {
        guard let handle = value.handleValue else { return false }
        let text = HandleTypeText(typeName)
        if !text.namesType(handle.typeName) || text.versioned && !handle.hasVersion { return false }
        if !text.projected { return handle.projection.isEmpty }
        return handle.projection.isEmpty || text.namesProjection(handle.projection) || canNarrow(handle, text.projection)
    }

    // Whether the host's type knows the handle's view and the projection, and the projection narrows the view.
    private func canNarrow(_ handle: GesHandle, _ projection: String) -> Bool {
        guard let binding = handleBinding(handle), let view = binding.type.findProjection(handle.projection), let target = binding.type.findProjection(projection) else { return false }
        return target.declaration.narrows(view.declaration)
    }

    private func handleBinding(_ handle: GesHandle) -> GesHandleBinding? { linked!.handleResolvers?[handle.typeName] }

    // A handle passing to a parameter or field of a projected type: an unpinned view drops the version, a pinned one
    // carries the projection's version (specs/Language.md#projections). Nothing when the version cannot be computed.
    func castHandleView(_ destination: Int, _ value: GesValue, _ handle: GesHandle, _ text: HandleTypeText) throws {
        if text.namesProjection(handle.projection) {
            setValue(destination, value)
            return
        }

        let projection = text.projection
        var narrowed: GesHandle?
        if !text.versioned {
            narrowed = handle.withView(.nothing, projection)
        } else if let binding = handleBinding(handle), let target = binding.type.findProjection(projection) {
            if binding.type.version != .contentHash {
                narrowed = handle.withView(handle.version, projection)
            } else {
                let wider = try resolveHandleState(handle)
                if !wider.state.isNothing {
                    let state = try GesHandleStates.narrow(binding.type, handle.withView(.nothing, projection), target, wider)
                    let result = handle.withView(state.version, projection)
                    let key = HandleStateKey(handle: result)
                    if handleStates[key] == nil { handleStates[key] = state }
                    narrowed = result
                }
            }
        }

        if let narrowed { setValue(destination, .handle(narrowed)) } else { setNothing(destination) }
    }

    // Reads a member of a handle: the identity members need no state, every other member reads the resolved state.
    func readHandleMember(_ destination: Int, _ key: String, _ handle: GesHandle) throws {
        switch key {
        case "id":
            if handle.isComposite { setMap(destination, GesValueMap(canonicalKeys: compositeIdKeys, values: [handle.part, handle.id])) } else { setValue(destination, handle.id) }
            return
        case "version":
            setValue(destination, handle.version)
            return
        // Without a static type the root type is unknown, so `root` gives nothing and never reads state.
        case "root":
            setNothing(destination)
            return
        // `@id` is `id` of a statically composite type of which one may be part-less: such a handle holds only its root's
        // id, so its identity is the root alone, without a part.
        case "@id":
            if handle.isComposite {
                setMap(destination, GesValueMap(canonicalKeys: compositeIdKeys, values: [handle.part, handle.id]))
            } else {
                setMap(destination, GesValueMap(canonicalKeys: rootIdKeys, values: [handle.id]))
            }
            return
        default:
            break
        }

        // `@Root` names the root type of a composite: a composite with a part keeps its root's identity and version, a
        // part-less one, which holds its root's id as its own, gives the root without a version.
        if key.unicodeScalars.count > 1 && key.unicodeScalars.first == "@" {
            let version = handle.isComposite ? handle.version : .nothing
            setValue(destination, .handle(GesHandle.fromWire(typeName: String(key.unicodeScalars.dropFirst()), id: handle.id, version: version, part: .nothing, isComposite: false)))
            return
        }

        let state = try resolveHandleState(handle).state
        if state.kind == .record, let field = state.asMap?.get(key) {
            setValue(destination, field)
            return
        }
        setNothing(destination)
    }

    // `h as :State` gives the whole state of a handle with the full view, `h as :P` the state of a projection that is the
    // handle's view or narrows it; anything else is nothing.
    func tryCastHandleState(_ destination: Int, _ handle: GesHandle, _ typeName: String) throws -> Bool {
        guard let binding = handleBinding(handle) else { return false }
        let type = binding.type
        if handle.projection.isEmpty && type.stateName == typeName {
            setValue(destination, try resolveHandleState(handle).state)
            return true
        }

        guard let target = type.findProjection(typeName) else { return false }
        let same = handle.projection == typeName
        if !same && !handle.projection.isEmpty {
            guard let view = type.findProjection(handle.projection), target.declaration.narrows(view.declaration) else { return false }
        }

        let wider = try resolveHandleState(handle)
        setValue(destination, same || wider.state.isNothing ? wider.state : try GesSchemaValues.project(target.declaration, wider.state))
        return true
    }

    // `c as :P` of a Record of a schema the projection P narrows: the Record of P with the taken fields, read along their
    // paths (specs/Language.md#projections). The projection comes from a handle type of the host whose state P narrows.
    func tryProjectRecord(_ destination: Int, _ value: GesValue, _ recordName: String, _ typeName: String) throws -> Bool {
        guard let resolvers = linked!.handleResolvers else { return false }
        for binding in resolvers.values {
            guard let target = binding.type.findProjection(typeName) else { continue }
            var source = target.declaration.source
            while let current = source {
                if current.name == recordName {
                    let projected: GesValue
                    do {
                        projected = try GesSchemaValues.project(target.declaration, value)
                    } catch is GesWireError {
                        projected = .nothing
                    }
                    setValue(destination, projected)
                    return true
                }
                source = current.source
            }
            return false
        }
        return false
    }

    // The state of the object through the handle's view, or nothing when it is missing or not visible; the first answer
    // of the invocation is reused, and a cached full state serves narrower views.
    func resolveHandleState(_ handle: GesHandle) throws -> GesHandleState {
        let key = HandleStateKey(handle: handle)
        if let cached = handleStates[key] { return cached }
        // Without a resolver, which only a read without a static type reaches, the object reads as missing.
        guard let binding = handleBinding(handle) else { return GesHandleState(state: .nothing, version: .nothing) }
        var projection: GesSchema?
        if !handle.projection.isEmpty {
            guard let found = binding.type.findProjection(handle.projection) else {
                throw GesRuntimeError(diagnostic: .init(phase: .runtime, code: GesDiagnosticCodes.runtimeInvalidHandleState, message: "@\(handle.typeName) has no projection :\(handle.projection).", symbol: handle.description))
            }
            projection = found
            if let wider = cachedFull(handle) {
                let narrowed = try GesHandleStates.narrow(binding.type, handle, found, wider)
                handleStates[key] = narrowed
                return narrowed
            }
        }

        if try !resolveBatch(binding, handle, projection) {
            cacheHandleState(handle, try GesHandleStates.resolve(binding, handleResolveCall, handle, projection, handleParty))
        }
        return handleStates[key]!
    }

    // A full state the invocation already read for the same object and version, or for the object without a version.
    private func cachedFull(_ handle: GesHandle) -> GesHandleState? {
        if let full = handleStates[HandleStateKey(handle: handle.withView(handle.version, ""))], !full.state.isNothing { return full }
        if handle.hasVersion, let full = handleStates[HandleStateKey(handle: handle.withView(.nothing, ""))], !full.state.isNothing { return full }
        return nil
    }

    func cacheHandleState(_ handle: GesHandle, _ resolved: GesHandleState) {
        handleStates[HandleStateKey(handle: handle)] = resolved
        if resolved.notVisible { handleContext?.recordHandleNotVisible(handle.description) }
        // A read without a version picks up the resolved one, so a later read of that version sees the same state.
        if !handle.hasVersion && !resolved.version.isNothing { addState(handle.withView(resolved.version, handle.projection), resolved) }
        if resolved.full.isNothing { return }
        let full = GesHandleState(state: resolved.full, version: resolved.fullVersion)
        addState(handle.withView(.nothing, ""), full)
        if !resolved.fullVersion.isNothing { addState(handle.withView(resolved.fullVersion, ""), full) }
    }

    private func addState(_ handle: GesHandle, _ state: GesHandleState) {
        let key = HandleStateKey(handle: handle)
        if handleStates[key] == nil { handleStates[key] = state }
    }

    // Resolves the read handle together with the other uncached handles of its type in a List of the message, with the
    // read's projection and version rule (specs/HostRuntime.md#handles). False when no such List has another handle.
    private func resolveBatch(_ binding: GesHandleBinding, _ handle: GesHandle, _ projection: GesSchema?) throws -> Bool {
        if !handleListsCollected { collectHandleLists() }
        if handleLists.isEmpty { return false }
        for list in handleLists {
            let found = list.contains { element in
                guard let element = element.handleValue else { return false }
                return element == handle && (!handle.hasVersion || GesHandleStates.versionEquals(element.version, handle.version))
            }
            if !found { continue }
            let batch = handleResolveBatch
            defer {
                batch.end()
                handleBatchSeen.removeAll(keepingCapacity: true)
            }
            _ = batch.add(handle, binding.type, projection, handleParty)
            handleBatchSeen.insert(HandleStateKey(handle: handle))
            for item in list {
                guard let element = item.handleValue, element.typeName == handle.typeName, element.isComposite == handle.isComposite else { continue }
                let other = element.withView(handle.hasVersion ? element.version : .nothing, handle.projection)
                let key = HandleStateKey(handle: other)
                if handleStates[key] != nil || !handleBatchSeen.insert(key).inserted { continue }
                _ = batch.add(other, binding.type, projection, handleParty)
            }

            if batch.count == 1 { return false }
            try GesHandleStates.resolveMany(binding, batch)
            for index in 0..<batch.count { cacheHandleState(batch[index].handle, try GesHandleStates.check(binding.type, batch[index])) }
            return true
        }
        return false
    }

    // Collects the Lists of the message's arguments, at any depth, that hold a handle.
    private func collectHandleLists() {
        handleListsCollected = true
        guard let message = processingMessage else { return }
        var pending: [GesValue] = []
        for argument in message.arguments.values where argument.mayHoldHandle { pending.append(argument) }
        while let value = pending.popLast() {
            switch value.kind {
            case .list:
                let items = value.asList
                var holds = false
                for item in items {
                    if item.handleValue != nil { holds = true } else if item.mayHoldHandle { pending.append(item) }
                }
                if holds { handleLists.append(items) }
            case .message:
                for argument in value.messageValue!.arguments.values where argument.mayHoldHandle { pending.append(argument) }
            case .map, .record:
                if let map = value.asMap {
                    for index in 0..<map.length where map.value(at: index).mayHoldHandle { pending.append(map.value(at: index)) }
                }
            default:
                break
            }
        }
    }
}
