// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

extension GesCallbacks {
    static func extensionCall(_ i: GameEventScriptBytecodeInstruction, _ s: GesVmState, _ c: GameEventScriptContext) throws {
        let destination = Int(i.word0)
        let registers = s.list(i.word2)
        guard let binding = s.linked!.extensionBindings[i.word1], let function = s.linked!.extensions[i.word1], binding.argumentNames.count == registers.count else {
            s.setNothing(destination)
            s.fail("runtime.invalidExtensionBinding")
            return
        }
        let call = s.extensionCall
        let boundary = c.random.markBoundary()
        call.begin(c, .init(state: s, indexes: registers))
        defer {
            let fault = c.endBoundary(boundary)
            if s.error == nil { if fault == .boundaryUnderflow { s.fail("runtime.randomStackUnderflow") } else if fault == .unbalanced { s.fail("runtime.randomScopeImbalance") } }
            call.end()
        }
        do { try function.invoke(call) } catch let fault as GameEventScriptExtensionFault {
            s.fail(fault.diagnostic)
            throw fault
        } catch let fault as GesRuntimeError {
            s.fail(fault.diagnostic)
            throw fault
        } catch {
            s.setNothing(destination)
            s.fail("runtime.extensionCallFailed", symbol: s.text(binding.name), error: error)
            return
        }
        s.setValue(destination, i.normalizeResultAsPredicate && call.result.kind != .boolean ? .nothing : call.result)
    }
}
