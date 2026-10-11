// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using GameEventScript.Api;

internal static class NativeHandlers
{
    internal static void Validate(JsonElement handlers)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var definition in handlers.EnumerateArray())
        {
            RuntimeValues.Fields(definition, "id", "message", "parameters", "messageName", "priority", "initiallySubscribed", "throw", "faultCode", "faultContext", "emit", "actions");
            var id = definition.GetProperty("id").GetString();
            if (string.IsNullOrEmpty(id) || !ids.Add(id) || string.IsNullOrEmpty(definition.GetProperty("message").GetString())) throw new ArgumentException("Invalid native handler identity.");
            var parameters = definition.GetProperty("parameters").EnumerateArray().Select(p => p.GetString() ?? throw new ArgumentException("Invalid parameter.")).ToArray();
            if (definition.GetProperty("messageName").GetBoolean() && parameters.Length != 0) throw new ArgumentException("Name-only subscription has parameters.");
            _ = definition.GetProperty("priority").GetInt32();
            _ = definition.GetProperty("initiallySubscribed").GetBoolean();
            var throws = definition.GetProperty("throw").GetBoolean();
            var code = definition.GetProperty("faultCode").GetString();
            if (code is not null && (string.IsNullOrWhiteSpace(code) || code.StartsWith("runtime.", StringComparison.Ordinal) || throws)) throw new ArgumentException("Invalid fault code.");
            var context = definition.GetProperty("faultContext");
            RuntimeValues.Fields(context, "programName", "handlerName");
            foreach (var field in context.EnumerateObject())
                if (field.Value.GetString() is not null && code is null) throw new ArgumentException("Fault context without code.");
            foreach (var emit in definition.GetProperty("emit").EnumerateArray())
            {
                RuntimeValues.Fields(emit, "message", "forwardArguments");
                var message = RuntimeValues.DecodeMessage(emit.GetProperty("message"));
                if (message.Tags.Count != 0 || (emit.GetProperty("forwardArguments").GetBoolean() && message.Arguments.Count != 0)) throw new ArgumentException("Invalid native emit.");
            }
        }
    }

    internal static IGameEventScriptNativeMessageHandler Create(JsonElement definition, HostScenario scenario) => new Handler(definition, scenario);

    private sealed class Handler(JsonElement definition, HostScenario scenario) : IGameEventScriptNativeMessageHandler
    {
        /// <summary>Performs only the declared fixture fault or ordered native emissions.</summary>
        public void Handle(GameEventScriptMessage message, GameEventScriptContext context)
        {
            if (definition.GetProperty("throw").GetBoolean()) throw new InvalidOperationException("Configured native handler failure.");
            if (definition.GetProperty("faultCode").GetString() is { } code)
            {
                var fault = definition.GetProperty("faultContext");
                throw new NativeFault(new GameEventScriptDiagnostic(GameEventScriptDiagnosticPhase.Runtime, code, "Configured native handler fault.",
                    ProgramName: fault.GetProperty("programName").GetString(), HandlerName: fault.GetProperty("handlerName").GetString()));
            }
            scenario.Apply("native/" + definition.GetProperty("id").GetString(), definition.GetProperty("actions"));
            foreach (var emit in definition.GetProperty("emit").EnumerateArray())
            {
                var output = RuntimeValues.DecodeMessage(emit.GetProperty("message"));
                if (emit.GetProperty("forwardArguments").GetBoolean())
                {
                    var arguments = new GameEventScriptMessageArgument[message.Arguments.Count];
                    for (var index = 0; index < arguments.Length; index++) arguments[index] = new GameEventScriptMessageArgument(message.Arguments.NameAt(index), message.Arguments.ValueAt(index));
                    output = GameEventScriptMessage.Create(output.Name, arguments);
                }
                context.Emit(output);
            }
        }
    }

    private sealed class NativeFault(GameEventScriptDiagnostic diagnostic) : GameEventScriptFatalRuntimeException(diagnostic);
}
