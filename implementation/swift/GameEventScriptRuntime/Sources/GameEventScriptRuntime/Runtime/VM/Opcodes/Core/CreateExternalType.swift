// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

extension GesCallbacks {
    static func constructor(_ i: GameEventScriptBytecodeInstruction, _ s: GesVmState, _ c: GameEventScriptContext) throws {
        let destination = Int(i.word0)
        guard let binding = s.linked!.constructorBindings[i.word1], let constructor = s.linked!.constructors[i.word1] else {
            s.setNothing(destination)
            s.fail("runtime.invalidExternalTypeBinding")
            return
        }
        let labels = s.list(i.word2).map(s.text)
        guard labels.count == s.stageLength, labels.count == binding.argumentNames.count, labels.count == constructor.definition.parameters.count, !labels.contains("_") else {
            s.setNothing(destination)
            return
        }
        var arguments: [GesValue] = []
        for parameter in constructor.definition.parameters {
            guard let index = labels.firstIndex(of: parameter.name) else {
                s.setNothing(destination)
                return
            }
            let value = try convert(s.staged(index), to: parameter.typeName, c)
            if value.isNothing {
                s.setNothing(destination)
                return
            }
            arguments.append(GesExternalNames.coerce(value, kind: parameter.kind, unit: parameter.unit))
        }
        let call = s.constructorCall
        call.begin(arguments: .init(arguments), typeName: constructor.definition.typeName)
        defer { call.end() }
        do { try constructor.invoke(call) } catch let fault as GameEventScriptExtensionFault { throw fault } catch let fault as GesRuntimeError { throw fault } catch {
            s.setNothing(destination)
            s.fail("runtime.externalConstructorFailed", symbol: constructor.definition.typeName, error: error)
            return
        }
        s.setValue(destination, call.result)
    }
}
