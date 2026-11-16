// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

extension GesVirtualMachine {
    func emitOrPublishMessage(_ i: GesBytecodeInstruction, _ c: GesContext) throws {
        let valueMessage = [.emitMessageValue, .emitMessageValueWithTags, .publishMessageValue, .publishMessageValueWithTags].contains(i.opcode)
        var message: GameEventScriptMessage?
        if valueMessage {
            message = value(Int(i.word1)).messageValue
        } else {
            // As in C#, an outbound binding the linked Program lacks sends nothing; validation keeps it unreachable.
            guard let signature = linked!.outbound[i.word0] else { return }
            message = signature.makeMessage(values(i.word2))
        }
        guard var message else { return }
        if [.emitMessageWithTags, .emitMessageValueWithTags, .publishMessageWithTags, .publishMessageValueWithTags].contains(i.opcode) {
            guard let taggedMessage = try? Self.tagged(message, values(valueMessage ? i.word2 : i.word1)) else { return }
            message = taggedMessage
        }
        if [.publishMessage, .publishMessageWithTags, .publishMessageValue, .publishMessageValueWithTags].contains(i.opcode) { c.publish(message) } else { c.emit(message) }
    }
}
