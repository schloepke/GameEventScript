// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

/// Host-specific resolution; this object is never retained by the portable Program.
final class GesLinkedProgram {
    struct Handler {
        let signature: GameEventScriptMessageSignature
        let binding: GameEventScriptBinding
        let requiredTags: [String]
        let excludedTags: [String]
    }
    let program: GameEventScriptProgram
    var handlers: [Handler] = []
    var records: [UInt16: GameEventScriptBinding] = [:]
    var recordsByName: [String: GameEventScriptBinding] = [:]
    var extensions: [UInt16: any GameEventScriptExtensionFunction] = [:]
    var extensionBindings: [UInt16: GameEventScriptBinding] = [:]
    var constructors: [UInt16: any GameEventScriptExternalTypeConstructor] = [:]
    var constructorBindings: [UInt16: GameEventScriptBinding] = [:]
    var outbound: [UInt16: GameEventScriptMessageSignature] = [:]
    init(
        _ program: GameEventScriptProgram, extensions: (any GameEventScriptExtensionRegistry)?,
        types: (any GameEventScriptExternalTypeRegistry)?
    ) throws {
        self.program = program
        for binding in program.bindings {
            let name = program.stringConstants[Int(binding.name)]
            let labels = binding.argumentNames.map { program.stringConstants[Int($0)] }
            switch binding.kind {
            case .messageHandler, .messageNameHandler:
                handlers.append(
                    try .init(
                        signature: .init(name: name, parameters: labels), binding: binding,
                        requiredTags: binding.requiredTags.map { program.stringConstants[Int($0)] },
                        excludedTags: binding.excludedTags.map { program.stringConstants[Int($0)] }))
            case .record:
                if binding.id != .max { records[binding.id] = binding }
                recordsByName[name] = binding
            case .outboundMessage:
                if binding.id != .max { outbound[binding.id] = try .init(name: name, parameters: labels) }
            case .extensionCall:
                if binding.id == .max { continue }
                let parts = name.split(separator: ".")
                let reference = try GameEventScriptExtensionReference(
                    extensionName: String(parts[0]), functionName: String(parts[1]), argumentLabels: labels)
                guard let function = try extensions?.resolve(reference) else {
                    throw GameEventScriptDynamicLinkError(
                        code: "link.missingExtension", program: program.moduleName, symbol: reference.signatureID)
                }
                self.extensions[binding.id] = function
                extensionBindings[binding.id] = binding
            case .externalType:
                if binding.id == .max { continue }
                let reference = try GameEventScriptExternalTypeConstructorReference(
                    typeName: name, argumentLabels: labels)
                guard let constructor = try types?.resolve(reference) else {
                    throw GameEventScriptDynamicLinkError(
                        code: "link.missingExternalTypeConstructor", program: program.moduleName,
                        symbol: reference.signatureID)
                }
                if constructor.definition.signatureID != reference.signatureID {
                    throw GameEventScriptDynamicLinkError(
                        code: "link.mismatchedExternalTypeConstructor", program: program.moduleName,
                        symbol: reference.signatureID)
                }
                constructors[binding.id] = constructor
                constructorBindings[binding.id] = binding
            default: break
            }
        }
    }
}
