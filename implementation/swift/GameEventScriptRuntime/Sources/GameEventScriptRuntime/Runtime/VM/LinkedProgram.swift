// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

/// Host-specific resolution; this object is never retained by the portable Program.
final class GesLinkedProgram {
    struct Handler {
        let signature: GesMessageSignature
        let binding: GesBindingSegment.Entry
        let requiredTags: [String]
        let excludedTags: [String]
    }

    var profiler: (any GesProgramProfiler)?
    let textValues: [GesValue]
    let program: GameEventScriptProgram
    var handlers: [Handler] = []
    var records: [UInt16: GesBindingSegment.Entry] = [:]
    var recordsByName: [String: GesBindingSegment.Entry] = [:]
    var extensions: [UInt16: any GesExtensionFunction] = [:]
    var extensionBindings: [UInt16: GesBindingSegment.Entry] = [:]
    var constructors: [UInt16: any GesExternalTypeConstructor] = [:]
    var constructorBindings: [UInt16: GesBindingSegment.Entry] = [:]
    var outbound: [UInt16: GesMessageSignature] = [:]
    private var mapLayouts: [GesMapLayout?]

    private var dataPositions: [UInt32: [Int]?] = [:]

    /// Argument positions of a constant data-construction type and label list, resolved on first use;
    /// nil when the shape is invalid and construction yields Nothing.
    func dataConstructionPositions(type: UInt16, labels: UInt16) -> [Int]? {
        let key = UInt32(type) << 16 | UInt32(labels)
        if let positions = dataPositions[key] { return positions }
        let positions = GesDataConstructionShape.positions(program.stringConstants[Int(type)], program.uint16IndexLists[Int(labels)].map { program.stringConstants[Int($0)] })
        dataPositions[key] = .some(positions)
        return positions
    }

    /// Canonical key order of a constant map-literal key list, computed on first use.
    func mapLayout(_ list: UInt16) -> GesMapLayout {
        if let layout = mapLayouts[Int(list)] { return layout }
        let layout = GesMapLayout(program.uint16IndexLists[Int(list)].map { program.stringConstants[Int($0)] })
        mapLayouts[Int(list)] = layout
        return layout
    }

    init(_ program: GameEventScriptProgram, extensions: (any GesExtensionRegistry)?, types: (any GesExternalTypeRegistry)?) throws {
        self.program = program
        textValues = program.stringConstants.map(GesValue.text)
        mapLayouts = .init(repeating: nil, count: program.uint16IndexLists.count)
        for binding in program.bindings {
            let name = program.stringConstants[Int(binding.name)]
            let labels = binding.argumentNames.map { program.stringConstants[Int($0)] }
            switch binding.kind {
            case .messageHandler, .messageNameHandler:
                handlers.append(
                    try .init(
                        signature: .init(name: name, parameters: labels),
                        binding: binding,
                        requiredTags: binding.requiredTags.map { program.stringConstants[Int($0)] },
                        excludedTags: binding.excludedTags.map { program.stringConstants[Int($0)] }
                    )
                )
            case .record:
                if binding.id != .max { records[binding.id] = binding }
                recordsByName[name] = binding
            case .outboundMessage: if binding.id != .max { outbound[binding.id] = try .init(name: name, parameters: labels) }
            case .extensionCall:
                if binding.id == .max { continue }
                let parts = name.split(separator: ".")
                let reference = try GesExtensionReference(extensionName: String(parts[0]), functionName: String(parts[1]), argumentLabels: labels)
                guard let function = try extensions?.resolve(reference) else { throw GesDynamicLinkError(code: "link.missingExtension", program: program.moduleName, symbol: reference.signatureID) }
                self.extensions[binding.id] = function
                extensionBindings[binding.id] = binding
            case .externalType:
                if binding.id == .max { continue }
                let reference = try GesExternalTypeConstructorReference(typeName: name, argumentLabels: labels)
                guard let constructor = try types?.resolve(reference) else { throw GesDynamicLinkError(code: "link.missingExternalTypeConstructor", program: program.moduleName, symbol: reference.signatureID) }
                if constructor.definition.signatureID != reference.signatureID { throw GesDynamicLinkError(code: "link.mismatchedExternalTypeConstructor", program: program.moduleName, symbol: reference.signatureID) }
                constructors[binding.id] = constructor
                constructorBindings[binding.id] = binding
            default: break
            }
        }
    }
}

/// Canonical GesValueMap order for a fixed key list: ascending scalar keys, last duplicate wins.
final class GesMapLayout {
    let inputCount: Int
    let keys: [String]
    /// Input (staged) position supplying each canonical entry.
    let sources: [Int]

    init(_ inputKeys: [String]) {
        inputCount = inputKeys.count
        let canonical = GesValueMap(inputKeys.enumerated().map { GesMapEntry(key: $0.element, value: .integer(Int64($0.offset))) })
        keys = canonical.entries.map(\.key)
        sources = canonical.entries.map { Int($0.value.integerValue!) }
    }
}
