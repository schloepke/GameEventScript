// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

/// Shared implementation boundary for the separately packaged compiler.
@_spi(Compiler) public enum GameEventScriptCompilerSupport {
    public static func number(_ text: String, percentage: Bool = false) -> GesValue? {
        TextNumberCast.read(text, percentage: percentage, allowGrouping: false)
    }
    public static func castNumber(_ value: GesValue) -> GesValue { GesCasts.number(value) }
    public static func arithmetic(_ opcode: GameEventScriptBytecodeOpCode, _ left: GesValue, _ right: GesValue)
        -> GesValue?
    {
        switch opcode {
        case .add: return GesMath.add(left, right)
        case .subtract: return GesMath.add(left, right, subtract: true)
        case .multiply, .divide, .integerDivide, .modulo, .remainder, .power:
            return GesMath.arithmetic(opcode, left, right)
        default: return nil
        }
    }
    public static func quotedText(_ text: String) -> String? { GesLiteralParser.sourceText(text) }
    public static func sha256(_ bytes: [UInt8]) -> [UInt8] { GesSha256.digest(bytes) }
    public static func program(
        moduleName: String, programVersion: UInt64, requiredRegisterCount: UInt16,
        requiredCallStackDepth: UInt16, strings: [String], indexLists: [[UInt16]],
        bindings: [GameEventScriptBinding], code: [GameEventScriptBytecodeInstruction],
        debugSymbols: [GameEventScriptDebugSymbol]?, sourceMap: GameEventScriptSourceMap?,
        sourceArchive: [GameEventScriptSourceArchiveEntry]?
    ) throws -> GameEventScriptProgram {
        let result = GameEventScriptProgram(
            formatVersion: 1, moduleName: moduleName,
            programVersion: programVersion, requiredRegisterCount: requiredRegisterCount,
            requiredCallStackDepth: requiredCallStackDepth, stringConstants: strings,
            uint16IndexLists: indexLists, bindings: bindings, code: code, debugSymbols: debugSymbols,
            sourceMap: sourceMap, sourceArchive: sourceArchive, buildMetadata: nil, opaqueSections: [])
        try GameEventScriptProgramValidator.validate(result)
        return result
    }
}

extension GameEventScriptCompilerSupport {
    /// Logical operand positions: words 0...2, then payload words 3...6.
    public static func registerSlots(_ instruction: GameEventScriptBytecodeInstruction) -> [Int] {
        instruction.operands.enumerated().compactMap { position, operand in
            guard operand.isRegister else { return nil }
            switch operand {
            case .targetRegister: return 0
            case .rightRegister, .objectRegister, .itemRegister, .keyRegister, .indexRegister, .defaultRegister,
                .needleRegister, .toRegister, .minimumRegister:
                return 2
            case .valueRegister, .auxItemBindingRegister, .stepRegister, .maximumRegister, .auxARegister: return 3
            case .itemBindingRegister: return position >= 3 ? 3 : 2
            case .weightRegister: return instruction.opcode == .takeWeighted ? 3 : 2
            case .auxBRegister, .faceRegister: return 4
            case .auxCRegister: return 5
            case .auxDRegister: return 6
            default: return 1
            }
        }
    }
    public static func listSlots(_ instruction: GameEventScriptBytecodeInstruction, text: Bool) -> [Int] {
        instruction.operands.compactMap { operand in
            guard operand.isList && operand.isTextList == text else { return nil }
            switch operand {
            case .messageShapeList where instruction.opcode == .loadMessage, .keyNameList: return 1
            case .captureRegisterList: return 4
            case .tagRegisterList
            where instruction.opcode == .emitMessageWithTags || instruction.opcode == .publishMessageWithTags: return 1
            default: return 2
            }
        }
    }
    public static func textSlots(_ instruction: GameEventScriptBytecodeInstruction) -> [Int] {
        instruction.operands.compactMap { operand in operand.isString ? (operand == .customTypeName ? 2 : 1) : nil }
    }
}
