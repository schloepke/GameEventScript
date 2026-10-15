// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

extension GesVirtualMachine {
    func executeCallExternal(_ i: GesBytecodeInstruction, _ c: GesContext) throws {
        let destination = Int(i.word0)
        let registers = list(i.word2)
        guard let binding = linked!.extensionBindings[i.word1], let function = linked!.extensions[i.word1], binding.argumentNames.count == registers.count else {
            setNothing(destination)
            raiseError("runtime.invalidExtensionBinding")
            return
        }
        let call = extensionCall
        let boundary = c.random.markBoundary()
        call.begin(c, .init(state: self, indexes: registers))
        defer {
            let fault = c.endBoundary(boundary)
            if error == nil { if fault == .boundaryUnderflow { raiseError("runtime.randomStackUnderflow") } else if fault == .unbalanced { raiseError("runtime.randomScopeImbalance") } }
            call.end()
        }
        do { try function.invoke(call) } catch let fault as GesExtensionFaultError {
            raiseError(fault.diagnostic)
            throw fault
        } catch let fault as GesRuntimeError {
            raiseError(fault.diagnostic)
            throw fault
        } catch {
            setNothing(destination)
            raiseError("runtime.extensionCallFailed", symbol: text(binding.name), error: error)
            return
        }
        setValue(destination, i.normalizeResultAsPredicate && call.result.kind != .boolean ? .nothing : call.result)
    }
}
