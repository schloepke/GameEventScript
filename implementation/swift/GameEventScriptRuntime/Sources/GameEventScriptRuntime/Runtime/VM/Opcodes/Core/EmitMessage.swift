// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

extension GameEventScriptVirtualMachine {
    static func message(_ i: GameEventScriptBytecodeInstruction, _ s: GesVmState, _ c: GameEventScriptContext) throws {
        let valueMessage = [.emitMessageValue, .emitMessageValueWithTags, .publishMessageValue, .publishMessageValueWithTags].contains(i.opcode)
        var message: GameEventScriptMessage?
        if valueMessage {
            let value = s.value(Int(i.word1))
            message = value.messageValue ?? value.signatureValue?.createMessage([])
        } else {
            guard let signature = s.linked!.outbound[i.word0] else {
                s.fail("runtime.invalidMessageBinding")
                return
            }
            message = signature.createMessage(s.values(i.word2))
        }
        guard var message else { return }
        if [.emitMessageWithTags, .emitMessageValueWithTags, .publishMessageWithTags, .publishMessageValueWithTags].contains(i.opcode) {
            do { message = try tagged(message, s.values(valueMessage ? i.word2 : i.word1)) } catch {
                if valueMessage { throw error }
                return
            }
        }
        if [.publishMessage, .publishMessageWithTags, .publishMessageValue, .publishMessageValueWithTags].contains(i.opcode) { c.publish(message) } else { c.emit(message) }
    }
}
