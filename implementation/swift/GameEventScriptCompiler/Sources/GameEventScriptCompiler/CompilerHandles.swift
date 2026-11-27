// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

@_spi(Compiler) import GameEventScriptRuntime

// Lowering of handle types: parameter and field checks, `is @Name`, identity members and state reads, as
// GesCompilerHandles of the C# compiler (specs/Language.md#handles, specs/Bytecode.md#handles).
extension GesCompiler {
    // A parameter or field of a handle type keeps a matching handle and turns any other value into nothing.
    func handleTypeCast(_ d: Int, _ value: Int, _ typeName: String, _ r: GesRoutine) {
        let alternatives = GesHandleTypeNames.alternatives(typeName)
        if alternatives.count == 1 {
            namedHandleTypes.insert(GesHandleTypeNames.split(typeName).name)
            r.emit(.castCustom, d, value, builder.text(typeName))
            return
        }
        let matches = r.temporary()
        if GesHandleTypeNames.hasProjection(typeName) {
            // Each alternative gives its own view, so a matching value casts to the alternative it matched.
            var labels: [Int] = []
            for alternative in alternatives {
                namedHandleTypes.insert(GesHandleTypeNames.split(alternative).name)
                r.emit(.checkCustomType, matches, value, builder.text(alternative))
                labels.append(r.emit(.jumpIfTrue, 0, matches))
            }
            r.emit(.loadNothing, d)
            var ends = [r.emit(.jump)]
            for (index, alternative) in alternatives.enumerated() {
                r.mark([labels[index]])
                r.emit(.castCustom, d, value, builder.text(alternative))
                if index < alternatives.count - 1 { ends.append(r.emit(.jump)) }
            }
            r.mark(ends)
            return
        }
        var keep: [Int] = []
        for alternative in alternatives {
            namedHandleTypes.insert(GesHandleTypeNames.split(alternative).name)
            r.emit(.checkCustomType, matches, value, builder.text(alternative))
            keep.append(r.emit(.jumpIfTrue, 0, matches))
        }
        r.emit(.loadNothing, d)
        let done = r.emit(.jump)
        r.mark(keep)
        if d != value { r.emit(.move, d, value) }
        r.mark([done])
    }

    // `@X[:count]` and the other store reads, with a root those of one root's parts (specs/Language.md#stores). Reads of
    // values or entries read states, so the type's binding needs a resolver at the first of them.
    func storeReadExpression(_ e: GesExpression, _ d: Int, _ r: GesRoutine, _ scope: GesScope) throws {
        guard case .storeRead(let typeName, let rootExpression, let read) = e.kind else { preconditionFailure() }
        let root = try rootExpression.map { try expression($0, r, scope) }
        let opcode: GesBytecodeOpCode
        switch read {
        case .count: opcode = root == nil ? .countStore : .countParts
        case .keys: opcode = root == nil ? .keysOfStore : .keysOfParts
        case .values: opcode = root == nil ? .valuesOfStore : .valuesOfParts
        case .entries: opcode = root == nil ? .entriesOfStore : .entriesOfParts
        }
        storeRead(opcode, d, root, typeName, r)
    }

    // An assembled or source store read; an assembled one names its type like a source one when the module declares it.
    func storeRead(_ opcode: GesBytecodeOpCode, _ d: Int, _ root: Int?, _ typeName: String, _ r: GesRoutine) {
        let name = String(typeName.dropFirst())
        if typeName.hasPrefix("@"), handleTypes[name] != nil { namedHandleTypes.insert(name) }
        var payload: UInt64 = 0
        if [.valuesOfStore, .entriesOfStore, .valuesOfParts, .entriesOfParts].contains(opcode) {
            builder.handleStateReads.append([name])
            payload = UInt64(builder.handleStateReads.count)
        }
        r.emit(opcode, d, root ?? 0, builder.text(typeName), payload: payload)
    }

    // `is @Name` and `is @Name~` check the handle type and, with `~`, a carried version.
    func handleTypeCheck(_ d: Int, _ value: Int, _ typeName: String, _ r: GesRoutine) {
        namedHandleTypes.insert(GesHandleTypeNames.split(typeName).name)
        r.emit(.checkCustomType, d, value, builder.text(typeName))
    }

    // `h.id`, `h.version` and `h.root` read the identity; any other member of a statically typed handle reads its
    // state, which the binding of each possible type locates.
    func handleMemberAccess(_ target: GesExpression, _ member: String, _ d: Int, _ value: Int, _ r: GesRoutine, _ scope: GesScope) throws {
        guard let typeName = staticTypeName(target, scope), GesHandleTypeNames.isHandleType(typeName) else {
            r.emit(.memberAccess, d, builder.text(member), value)
            return
        }
        switch member {
        case "id":
            // A part-less composite holds its root's id as its own, so only `@id` reads its identity as a root without a
            // part; types whose composites all have a part keep the plain member.
            r.emit(.memberAccess, d, builder.text(GesHandleTypeNames.hasPartlessComposite(typeName, handleTypes) ? "@id" : "id"), value)
        case "version":
            r.emit(.memberAccess, d, builder.text(member), value)
        case "root":
            if let root = GesHandleTypeNames.commonRoot(typeName, handleTypes) {
                namedHandleTypes.insert(root.name)
                r.emit(.memberAccess, d, builder.text("@" + root.name), value)
            } else {
                r.emit(.memberAccess, d, builder.text(member), value)
            }
        default:
            let names = GesHandleTypeNames.names(typeName)
            for name in names { namedHandleTypes.insert(name) }
            var stateTarget = value
            // A parameter already holds its view; a handle from elsewhere, such as a Record field a message delivered,
            // takes the static view first, so the read never sees more than the type allows.
            if GesHandleTypeNames.hasProjection(typeName) && !target.isName {
                stateTarget = r.temporary()
                handleTypeCast(stateTarget, value, typeName, r)
            }
            builder.handleStateReads.append(names)
            r.emit(.memberAccess, d, builder.text(member), stateTarget, payload: UInt64(builder.handleStateReads.count))
        }
    }

    // `h as :State` and `h as :P` of a statically typed handle read the state of the alternatives with that state schema
    // or projection. `c as :P` of any other value projects a Record with a projection of a handle type's state, which
    // the Program names so that the host links the schema it was compiled against.
    func noteHandleStateCast(_ value: GesExpression, _ type: String, _ scope: GesScope) {
        if let typeName = staticTypeName(value, scope), GesHandleTypeNames.isHandleType(typeName) {
            for name in GesHandleTypeNames.names(typeName) {
                if let handle = handleTypes[name], handle.stateName == type || handle.findProjection(type) != nil { namedHandleTypes.insert(name) }
            }
            return
        }
        for name in handleTypeOrder where handleTypes[name]?.findProjection(type) != nil {
            namedHandleTypes.insert(name)
            return
        }
    }

    // The static type of an expression as far as handle lowering needs it: bindings with a declared type, record
    // fields, `h.root` of composites and casts.
    func staticTypeName(_ e: GesExpression, _ scope: GesScope) -> String? {
        switch e.kind {
        case .name(let name): return scope.type(name)
        case .cast(_, let type): return type
        case .member(let target, let member):
            guard let targetType = staticTypeName(target, scope) else { return nil }
            if GesHandleTypeNames.isHandleType(targetType) {
                return member == "root" ? GesHandleTypeNames.commonRoot(targetType, handleTypes).map { "@" + $0.name } : nil
            }
            return records[targetType]?.fields.first { $0.name == member }?.type
        default: return nil
        }
    }
}

extension GesBytecodeOpCode {
    // The store reads `countStore` through `entriesOfParts`.
    fileprivate var isStoreReadOpcode: Bool { rawValue >= Self.countStore.rawValue && rawValue <= Self.entriesOfParts.rawValue }
}

extension GesExpression {
    // Whether the expression is a bare name, such as a parameter.
    fileprivate var isName: Bool {
        if case .name = kind { return true }
        return false
    }
}

extension GesBinaryBuilder {
    // Handle type imports follow all other bindings in ordinal name order, each with its hash as four raw words, least
    // significant first, and the address of the first state read or the sentinel (specs/BinaryFormat.md#bindings).
    // State reads carry the 1-based index of their type list in the payload until here; this clears it.
    func handleTypeBinds(_ code: inout [GesBytecodeInstruction]) -> [(name: String, words: [UInt16], entryAddress: UInt16)] {
        guard !handleTypes.isEmpty || !handleStateReads.isEmpty else { return [] }
        var firstReads: [String: UInt16] = [:]
        for address in code.indices {
            let instruction = code[address]
            if (instruction.opcode == .memberAccess || instruction.opcode.isStoreReadOpcode) && instruction.payload != 0 {
                for name in handleStateReads[Int(instruction.payload) - 1] where firstReads[name] == nil { firstReads[name] = UInt16(address) }
                code[address] = .init(opcode: instruction.opcode, unitAndFlags: instruction.unitAndFlags, word0: instruction.word0, word1: instruction.word1, word2: instruction.word2, payload: 0)
            } else if instruction.opcode == .castCustom && Int(instruction.word2) < strings.count {
                let castName = strings[Int(instruction.word2)]
                for (name, type) in handleTypes where (type.stateName == castName || type.projections.contains(castName)) && firstReads[name] == nil { firstReads[name] = UInt16(address) }
            }
        }
        return handleTypes.keys.sorted(by: GesBinaryBuilder.ordinal).map { name in
            let hash = handleTypes[name]!.hash
            let words = [UInt16(truncatingIfNeeded: hash), UInt16(truncatingIfNeeded: hash >> 16), UInt16(truncatingIfNeeded: hash >> 32), UInt16(truncatingIfNeeded: hash >> 48)]
            return (name, words, firstReads[name] ?? 0xFFFF)
        }
    }

    // Ordinal order of UTF-16 code units, as C# StringComparer.Ordinal sorts.
    static func ordinal(_ left: String, _ right: String) -> Bool { left.utf16.lexicographicallyPrecedes(right.utf16) }
}
