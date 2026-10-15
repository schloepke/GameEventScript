// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

extension GesVirtualMachine {
    func emitMessage(_ i: GesBytecodeInstruction, _ c: GesContext) throws {
        let valueMessage = [.emitMessageValue, .emitMessageValueWithTags, .publishMessageValue, .publishMessageValueWithTags].contains(i.opcode)
        var message: GameEventScriptMessage?
        if valueMessage {
            let value = self.value(Int(i.word1))
            message = value.messageValue ?? value.signatureValue?.makeMessage([])
        } else {
            guard let signature = linked!.outbound[i.word0] else {
                raiseError("runtime.invalidMessageBinding")
                return
            }
            message = signature.makeMessage(values(i.word2))
        }
        guard var message else { return }
        if [.emitMessageWithTags, .emitMessageValueWithTags, .publishMessageWithTags, .publishMessageValueWithTags].contains(i.opcode) {
            do { message = try Self.tagged(message, values(valueMessage ? i.word2 : i.word1)) } catch {
                if valueMessage { throw error }
                return
            }
        }
        if [.publishMessage, .publishMessageWithTags, .publishMessageValue, .publishMessageValueWithTags].contains(i.opcode) { c.publish(message) } else { c.emit(message) }
    }
}
