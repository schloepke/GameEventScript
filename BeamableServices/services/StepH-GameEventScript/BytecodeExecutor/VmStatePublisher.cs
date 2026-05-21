using System;
using System.Collections.Generic;
using StepH.GameEventScript.Api;
using StepH.GameEventScript.Runtime;
using StepH.GameEventScript.Types;

namespace StepH.GameEventScript.BytecodeExecutor;

internal static class VmStatePublisher
{
    
    private static bool VmPublishMessage(ref this VmState vmState, ReadOnlySpan<ushort> shape, ReadOnlySpan<ushort> argumentSlots, bool publish, GameEventScriptSession context)
    {
        if (shape.Length == 0 || argumentSlots.Length != shape.Length - 1) return false;
        var messageName = vmState.Binary.TextConstantTable.Resolve(shape[0]);
        var pairs = new KeyValuePair<string, GameEventScriptValue>[argumentSlots.Length];
        for (var index = 0; index < argumentSlots.Length; index++)
        {
            pairs[index] = new KeyValuePair<string, GameEventScriptValue>(
                vmState.Binary.TextConstantTable.Resolve(shape[index + 1]),
                vmState.Register(argumentSlots[index]).ToGameEventScriptValue());
        }

        var message = GameEventScriptMessage.Create(messageName, GameEventScriptNamedArguments.CreateOrdered(pairs));
        return publish ? context.Publish(message) : context.Emit(message);
    }


}
