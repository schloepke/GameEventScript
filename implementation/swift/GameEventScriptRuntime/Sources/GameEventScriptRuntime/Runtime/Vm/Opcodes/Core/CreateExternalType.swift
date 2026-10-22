// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

extension GesVirtualMachine {
    func executeCreateExternalType(_ i: GesBytecodeInstruction, _ c: GesContext) throws {
        let destination = Int(i.word0)
        guard let binding = linked!.constructorBindings[i.word1], let constructor = linked!.constructors[i.word1] else {
            setNothing(destination)
            raiseError("runtime.invalidExternalTypeBinding")
            return
        }
        let labels = list(i.word2).map(text)
        guard labels.count == stageLength, labels.count == binding.argumentNames.count, labels.count == constructor.definition.parameters.count, !labels.contains("_") else {
            setNothing(destination)
            return
        }
        var arguments: [GesValue] = []
        for parameter in constructor.definition.parameters {
            guard let index = labels.firstIndex(of: parameter.name) else {
                setNothing(destination)
                return
            }
            let value = try GesCallbacks.convert(staged(index), to: parameter.typeName, c)
            if value.isNothing {
                setNothing(destination)
                return
            }
            arguments.append(GesExternalTypeNames.coerce(value, kind: parameter.kind, unit: parameter.unit))
        }
        let call = constructorCall
        call.begin(arguments: .init(arguments), typeName: constructor.definition.typeName)
        defer { call.end() }
        do { try constructor.invoke(call) } catch let fault as GesExtensionFaultError { throw fault } catch let fault as GesRuntimeError { throw fault } catch {
            setNothing(destination)
            raiseError("runtime.externalConstructorFailed", symbol: constructor.definition.typeName, error: error)
            return
        }
        setValue(destination, call.result)
    }
}
