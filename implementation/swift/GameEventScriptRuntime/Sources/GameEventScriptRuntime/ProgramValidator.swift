// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

/// Complete, host-independent validation shared by reading, writing, and loading.
public enum GameEventScriptProgramValidator {
    /// Checks complete Program structure, references, debug data and resource metadata without resolving host imports.
    ///
    /// - Throws: `GameEventScriptProgramFormatError` with a stable classification on failure.
    public static func validate(_ p: GameEventScriptProgram) throws {
        if p.formatVersion != 1 { throw failure(.unsupportedFormatVersion) }
        if !GesNames.module(p.moduleName) { throw failure(.invalidProgram, 1) }
        if [p.stringConstants.count, p.uint16IndexLists.count, p.bindings.count, p.code.count].contains(where: {
            $0 > 65535
        }) {
            throw failure(.tooManyEntries)
        }
        var ids: Set<UInt64> = []
        var functions: Set<String> = []
        var predicates: Set<String> = []
        var signatures: Set<String> = []
        var maxRegisters: UInt16 = 0
        var maxDepth: UInt16 = 0
        for (index, binding) in p.bindings.enumerated() {
            let textIndices = [binding.name] + binding.argumentNames + binding.requiredTags + binding.excludedTags
            if textIndices.contains(where: { Int($0) >= p.stringConstants.count }) {
                throw failure(.invalidStringIndex, 4, index)
            }
            let name = p.stringConstants[Int(binding.name)]
            let valid: Bool
            switch binding.kind {
            case .messageHandler, .messageNameHandler, .outboundMessage: valid = GesNames.message(name)
            case .function, .predicate: valid = GesNames.plain(name)
            case .record, .externalType: valid = GesNames.type(name)
            case .extensionCall: valid = GesNames.extensionName(name)
            }
            if !valid { throw failure(.invalidProgram, 4, index) }
            var names: Set<String> = []
            for argument in binding.argumentNames {
                let text = p.stringConstants[Int(argument)]
                if text == "_" { continue }
                if !GesNames.identifier(text) || !names.insert(text).inserted {
                    throw failure(.invalidProgram, 4, index)
                }
            }
            for tag in binding.requiredTags + binding.excludedTags {
                if !GesNames.plain(p.stringConstants[Int(tag)]) { throw failure(.invalidProgram, 4, index) }
            }
            if binding.kind == .function || binding.kind == .predicate {
                let signature =
                    "\(binding.kind.rawValue):\(name)("
                    + binding.argumentNames.map { p.stringConstants[Int($0)] }.joined(separator: ",") + ")"
                if !signatures.insert(signature).inserted { throw failure(.invalidProgram, 4, index) }
                if binding.kind == .function { functions.insert(name) } else { predicates.insert(name) }
            }
            let key =
                binding.isHandler
                ? (UInt64(1) << 63) | (UInt64(binding.kind.rawValue) << 32) | (UInt64(binding.name) << 16)
                    | UInt64(binding.id)
                : (UInt64(binding.kind.rawValue) << 16) | UInt64(binding.id)
            if binding.id != .max, !ids.insert(key).inserted { throw failure(.duplicateBindingId, 4, index) }
            if binding.isExecutable {
                if binding.entryAddress == .max || Int(binding.entryAddress) >= p.code.count {
                    throw failure(.invalidEntryAddress, 4, index)
                }
            } else if binding.entryAddress != .max {
                throw failure(.invalidEntryAddress, 4, index)
            }
            if binding.isHandler {
                maxRegisters = max(maxRegisters, binding.requiredRegisterCount)
                maxDepth = max(maxDepth, binding.requiredCallStackDepth)
            }
        }
        if !functions.isDisjoint(with: predicates) { throw failure(.invalidProgram, 4) }
        if maxRegisters != p.requiredRegisterCount || maxDepth != p.requiredCallStackDepth {
            throw failure(.invalidResourceMetadata, 1)
        }
        if !p.stringConstants.contains(where: { GesText.scalarEqual($0, p.moduleName) }) {
            throw failure(.invalidStringIndex, 1)
        }
        for (index, instruction) in p.code.enumerated() {
            try validateInstruction(p, instruction, index, ids)
        }
        try validateDebug(p)
        try validateOpaque(p)
        try GesProgramResourceValidator.validate(p)
    }

    static func failure(_ code: GameEventScriptProgramFormatErrorCode, _ section: UInt16? = nil, _ entry: Int? = nil)
        -> GameEventScriptProgramFormatError
    {
        .init(code, sectionType: section, entryIndex: entry)
    }

    static func validateInstruction(
        _ p: GameEventScriptProgram, _ i: GameEventScriptBytecodeInstruction, _ index: Int, _ ids: Set<UInt64>
    ) throws {
        if i.unitAndFlags & 0x1f > 3 || i.unitAndFlags & 0xc0 != 0 { throw failure(.invalidOperand, 16, index) }
        if i.opcode == .parseLiteral && (i.unitAndFlags != 0 || i.word2 != 0 || i.payload != 0) {
            throw failure(.invalidOperand, 16, index)
        }
        for (position, operand) in i.operands.enumerated() {
            if operand.isRegister {
                if i.register(operand, position) == .max { throw failure(.invalidOperand, 16, index) }
            } else if operand.isAddress {
                if Int(i.address(operand)) >= p.code.count { throw failure(.invalidJumpAddress, 16, index) }
            } else if operand.isString {
                let stringIndex = operand == .customTypeName ? i.word2 : i.word1
                if Int(stringIndex) >= p.stringConstants.count { throw failure(.invalidStringIndex, 16, index) }
                let text = p.stringConstants[Int(stringIndex)]
                if operand == .tag && !GesNames.plain(text) || operand == .customTypeName && !GesNames.type(text) {
                    throw failure(.invalidOperand, 16, index)
                }
            } else if operand.isList {
                let listIndex = Int(i.list(operand))
                if listIndex >= p.uint16IndexLists.count { throw failure(.invalidListIndex, 16, index) }
                var seen: Set<String> = []
                for (element, value) in p.uint16IndexLists[listIndex].enumerated() {
                    if !operand.isTextList {
                        if value == .max { throw failure(.invalidOperand, 16, index) }
                        continue
                    }
                    if Int(value) >= p.stringConstants.count { throw failure(.invalidStringIndex, 16, index) }
                    let text = p.stringConstants[Int(value)]
                    if operand == .messageShapeList && element == 0 {
                        if !GesNames.message(text) { throw failure(.invalidOperand, 16, index) }
                        continue
                    }
                    if text != "_" && !GesNames.plain(text) { throw failure(.invalidOperand, 16, index) }
                    if operand != .keyNameList && text != "_", !seen.insert(text).inserted {
                        throw failure(.invalidOperand, 16, index)
                    }
                }
            } else {
                switch operand {
                case .typeKind:
                    if GameEventScriptBytecodeTypeKind(rawValue: i.word2) == nil {
                        throw failure(.invalidOperand, 16, index)
                    }
                case .patternKind:
                    if GameEventScriptBytecodePatternKind(rawValue: i.a) == nil {
                        throw failure(.invalidOperand, 16, index)
                    }
                case .seriesKind:
                    if GameEventScriptBytecodeSeriesKind(rawValue: i.word2) == nil {
                        throw failure(.invalidOperand, 16, index)
                    }
                case .outboundMessage, .recordReference, .externalReference:
                    let kind: GameEventScriptBinaryBindKind =
                        operand == .outboundMessage
                        ? .outboundMessage
                        : operand == .recordReference
                            ? .record : i.opcode == .callExternal ? .extensionCall : .externalType
                    let id = operand == .outboundMessage ? i.word0 : i.word1
                    if !ids.contains(UInt64(kind.rawValue) << 16 | UInt64(id)) {
                        throw failure(.invalidOperand, 16, index)
                    }
                default: break
                }
            }
        }
    }

    static func validateFrame(
        _ p: GameEventScriptProgram, _ i: GameEventScriptBytecodeInstruction, _ index: Int, _ length: Int
    ) throws {
        for (position, operand) in i.operands.enumerated() {
            if operand.isRegister && Int(i.register(operand, position)) >= length {
                throw failure(.invalidOperand, 16, index)
            }
            if operand.isList && !operand.isTextList
                && p.uint16IndexLists[Int(i.list(operand))].contains(where: { Int($0) >= length })
            {
                throw failure(.invalidOperand, 16, index)
            }
        }
    }
}

extension GesOperand {
    var isRegister: Bool {
        self != .outboundMessage && (rawValue <= Self.auxDRegister.rawValue || self == .faceRegister)
    }
    var isAddress: Bool { (Self.jumpTarget.rawValue...Self.valueEntry.rawValue).contains(rawValue) }
    var isString: Bool {
        (Self.string.rawValue...Self.typeName.rawValue).contains(rawValue) && self != .typeKind && self != .patternKind
            && self != .seriesKind
    }
    var isList: Bool { (Self.messageShapeList.rawValue...Self.tagRegisterList.rawValue).contains(rawValue) }
    var isTextList: Bool { self == .messageShapeList || self == .argumentNameList || self == .keyNameList }
}

extension GameEventScriptBytecodeInstruction {
    var operands: [GesOperand] {
        if opcode == .hasPattern || opcode == .takePattern {
            if a == 1 { return opcode.operands + [.countImmediate, .faceRegister] }
            if a == 0 { return opcode.operands + [.countImmediate] }
        }
        return opcode.operands
    }
    func register(_ operand: GesOperand, _ position: Int) -> UInt16 {
        switch operand {
        case .targetRegister, .outboundMessage: word0
        case .rightRegister, .objectRegister, .itemRegister, .keyRegister, .indexRegister, .defaultRegister,
            .needleRegister, .toRegister, .minimumRegister:
            word2
        case .valueRegister, .auxItemBindingRegister, .stepRegister, .maximumRegister, .auxARegister: a
        case .itemBindingRegister: position >= 3 ? a : word2
        case .weightRegister: opcode == .takeWeighted ? a : word2
        case .auxBRegister, .faceRegister: b
        case .auxCRegister: c
        case .auxDRegister: d
        default: word1
        }
    }
    func address(_ operand: GesOperand) -> UInt16 {
        switch operand {
        case .projectionEntry, .keyEntry: a
        case .valueEntry: b
        default: word2
        }
    }
    func list(_ operand: GesOperand) -> UInt16 {
        switch operand {
        case .messageShapeList where opcode == .loadMessage: word1
        case .keyNameList: word1
        case .captureRegisterList: b
        case .tagRegisterList where opcode == .emitMessageWithTags || opcode == .publishMessageWithTags: word1
        default: word2
        }
    }
}

/// ASCII grammars used at the binary boundary; no Unicode normalization is applied.
enum GesNames {
    static func lower(_ c: UInt8) -> Bool { (97...122).contains(c) }
    static func upper(_ c: UInt8) -> Bool { (65...90).contains(c) }
    static func digit(_ c: UInt8) -> Bool { (48...57).contains(c) }
    static func alnum(_ c: UInt8) -> Bool { lower(c) || upper(c) || digit(c) }
    static func plain(_ text: String) -> Bool {
        let bytes = Array(text.utf8)
        return bytes.first.map(lower) == true && bytes.dropFirst().allSatisfy(alnum)
    }
    static func type(_ text: String) -> Bool {
        let bytes = Array(text.utf8)
        return bytes.first.map(upper) == true && bytes.dropFirst().allSatisfy(alnum)
    }
    static func message(_ text: String) -> Bool { type(text) || text == "initialization" || text == "undeliverable" }
    static func identifier(_ text: String) -> Bool {
        let parts = text.split(separator: "_", omittingEmptySubsequences: false)
        if parts.count == 1 { return plain(text) }
        guard parts.count == 2, plain(String(parts[0])), !parts[1].isEmpty else { return false }
        return (parts[1] == "0" || parts[1].utf8.first != 48) && parts[1].utf8.allSatisfy(digit)
    }
    static func module(_ text: String) -> Bool {
        if text.isEmpty { return false }
        return text.split(separator: ".", omittingEmptySubsequences: false).allSatisfy { part in
            part.utf8.first.map(lower) == true && part.utf8.allSatisfy { lower($0) || digit($0) }
        }
    }
    static func extensionName(_ text: String) -> Bool {
        let parts = text.split(separator: ".", omittingEmptySubsequences: false)
        return parts.count == 2 && parts.allSatisfy { plain(String($0)) }
    }
}
