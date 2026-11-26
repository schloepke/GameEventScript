// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

private let compositeIdKeys = ["part", "root"]

// Handle members, type checks and lazy state reads (specs/Language.md#handles, specs/HostRuntime.md#handles).
extension GesVirtualMachine {
    // Keys of the per-invocation state cache: type and identity as handle equality has them, and the carried version.
    struct HandleStateKey: Hashable {
        let handle: GesHandle

        static func == (left: Self, right: Self) -> Bool { left.handle == right.handle && GesHandleStates.versionEquals(left.handle.version, right.handle.version) }

        func hash(into hasher: inout Hasher) { handle.hash(into: &hasher) }
    }

    func clearHandleStates() { if !handleStates.isEmpty { handleStates.removeAll(keepingCapacity: true) } }

    // `@Name` matches a handle of that type, `@Name~` one that also carries a version.
    static func isHandleType(_ value: GesValue, _ typeName: String) -> Bool {
        guard let handle = value.handleValue else { return false }
        let versioned = typeName.unicodeScalars.last == "~"
        let name = typeName.unicodeScalars.dropFirst().dropLast(versioned ? 1 : 0)
        return handle.typeName.unicodeScalars.elementsEqual(name) && (!versioned || handle.hasVersion)
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
        default:
            break
        }

        // `@Root` names the root type of a composite, whose handle keeps the composite's identity root and version.
        if key.unicodeScalars.count > 1 && key.unicodeScalars.first == "@" {
            if !handle.isComposite {
                setNothing(destination)
                return
            }
            setValue(destination, .handle(GesHandle.fromWire(typeName: String(key.unicodeScalars.dropFirst()), id: handle.id, version: handle.version, part: .nothing, isComposite: false)))
            return
        }

        let state = try resolveHandleState(handle)
        if state.kind == .record, let field = state.asMap?.get(key) {
            setValue(destination, field)
            return
        }
        setNothing(destination)
    }

    // `h as :State`: the whole state of a handle whose type has that state schema, else nothing.
    func tryCastHandleState(_ destination: Int, _ handle: GesHandle, _ typeName: String) throws -> Bool {
        guard let binding = linked!.handleResolvers?[handle.typeName], binding.type.stateName == typeName else { return false }
        setValue(destination, try resolveHandleState(handle))
        return true
    }

    // The state Record of the object, or nothing when it is missing; the first answer of the invocation is reused.
    func resolveHandleState(_ handle: GesHandle) throws -> GesValue {
        let key = HandleStateKey(handle: handle)
        if let cached = handleStates[key] { return cached.state }
        let binding = linked!.handleResolvers?[handle.typeName]
        let resolved = try GesHandleStates.resolve(binding, handleResolveCall, handle)
        handleStates[key] = resolved
        // A read without a version picks up the resolved one, so a later read of that version sees the same state.
        if !handle.hasVersion && !resolved.version.isNothing, let versioned = try? handle.withVersion(resolved.version) {
            let versionedKey = HandleStateKey(handle: versioned)
            if handleStates[versionedKey] == nil { handleStates[versionedKey] = resolved }
        }
        return resolved.state
    }
}
