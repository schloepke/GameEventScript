// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

@_spi(Compiler) import GameEventScriptRuntime

extension GesCompiler {
    func materialize(_ sourceBindings: [GameEventScriptBinding], _ sourceCode: [Instruction]) -> ([GameEventScriptBinding], [Instruction]) {
        let oldStrings = strings
        let oldLists = lists
        strings = []
        lists = []
        stringIDs = [:]
        listIDs = [:]

        func resolve(_ index: UInt16) -> UInt16 { UInt16(text(oldStrings[Int(index)])) }

        let order: [GameEventScriptBinaryBindKind: Int] = [.messageHandler: 0, .messageNameHandler: 0, .record: 1, .predicate: 2, .function: 3, .outboundMessage: 4, .extensionCall: 5, .externalType: 6]
        let ordered = sourceBindings.enumerated().sorted { a, b in
            let ak = order[a.element.kind]!
            let bk = order[b.element.kind]!
            return ak == bk ? a.offset < b.offset : ak < bk
        }.map(\.element)
        let bindings = ordered.map { binding in
            let args = binding.argumentNames.map(resolve)
            let required = binding.requiredTags.map(resolve)
            let excluded = binding.excludedTags.map(resolve)
            _ = list(args.map(Int.init))
            _ = list(required.map(Int.init))
            _ = list(excluded.map(Int.init))
            return GameEventScriptBinding(
                kind: binding.kind,
                name: resolve(binding.name),
                argumentNames: args,
                entryAddress: binding.entryAddress,
                id: binding.id,
                requiredTags: required,
                excludedTags: excluded,
                requiredRegisterCount: binding.requiredRegisterCount,
                requiredCallStackDepth: binding.requiredCallStackDepth
            )
        }
        let instructions = sourceCode.map { instruction in
            var fields = instruction.fields
            let texts = GameEventScriptCompilerSupport.textSlots(instruction)
            let textLists = GameEventScriptCompilerSupport.listSlots(instruction, text: true)
            let registerLists = GameEventScriptCompilerSupport.listSlots(instruction, text: false)
            // Tagged emission resolves its argument list before the secondary tag list.
            let order = instruction.opcode == .emitMessageWithTags || instruction.opcode == .publishMessageWithTags ? [0, 2, 1, 3, 4, 5, 6] : Array(0...6)
            for slot in order {
                if texts.contains(slot) {
                    fields[slot] = resolve(fields[slot])
                } else if textLists.contains(slot) {
                    fields[slot] = UInt16(list(oldLists[Int(fields[slot])].map { Int(resolve($0)) }))
                } else if registerLists.contains(slot) {
                    fields[slot] = UInt16(list(oldLists[Int(fields[slot])].map(Int.init)))
                }
            }
            return instruction.replacing(fields)
        }
        return (bindings, instructions)
    }
}
