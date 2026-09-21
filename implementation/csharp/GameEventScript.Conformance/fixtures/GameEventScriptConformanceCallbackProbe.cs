// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using GameEventScript.Api;
using GameEventScript.Runtime.Values;

namespace GameEventScript.Tests.Conformance;

/// <summary>
/// Implements the fixed CallbackProbe fixture defined by the portable Conformance environment.
/// </summary>
internal static class GameEventScriptConformanceCallbackProbe
{
    internal static readonly GameEventScriptExternalTypeDefinition Definition = new(
        "CallbackProbe",
        [new GameEventScriptExternalTypeFieldDefinition("failure", GameEventScriptBytecodeTypeKind.Text),
            new GameEventScriptExternalTypeFieldDefinition("context", GameEventScriptBytecodeTypeKind.Text),
            new GameEventScriptExternalTypeFieldDefinition("value", GameEventScriptBytecodeTypeKind.Float)],
        [new GameEventScriptExternalTypeConstructorDefinition("CallbackProbe",
            [new GameEventScriptExternalTypeParameterDefinition("failure", GameEventScriptBytecodeTypeKind.Text), new GameEventScriptExternalTypeParameterDefinition("context", GameEventScriptBytecodeTypeKind.Text)])]);

    internal static readonly IGameEventScriptExternalTypeConstructor Constructor = new ProbeConstructor();

    private sealed class ProbeConstructor : IGameEventScriptExternalTypeConstructor
    {
        public GameEventScriptExternalTypeConstructorDefinition Definition => GameEventScriptConformanceCallbackProbe.Definition.Constructors[0];

        public void Invoke(GesExternalTypeConstructorCall call)
        {
            var failure = call.Arguments.GetAsText(0);
            var context = call.Arguments.GetAsText(1);
            if (context is not ("none" or "program" or "handler" or "both"))
            {
                call.SetNothing();
                return;
            }
            switch (failure)
            {
                case "constructorUnexpected":
                    throw new InvalidOperationException("Configured external constructor failure.");
                case "constructorDeclared":
                    throw DeclaredFault("CallbackProbe", context);
                case "fieldUnexpected":
                case "fieldDeclared":
                    call.SetExternalValue(new ProbeValue(failure, context));
                    return;
                default:
                    call.SetNothing();
                    return;
            }
        }
    }

    private sealed class ProbeValue(string failure, string context) : IGameEventScriptExternalValue
    {
        public GameEventScriptExternalTypeDefinition Definition => GameEventScriptConformanceCallbackProbe.Definition;

        public GesValue? GetField(string fieldName)
        {
            if (fieldName == "failure") return GesValue.GesText(failure);
            if (fieldName == "context") return GesValue.GesText(context);
            if (fieldName != "value") return null;
            if (failure == "fieldUnexpected") throw new InvalidOperationException("Configured external field failure.");
            throw DeclaredFault("CallbackProbe.value", context);
        }
    }

    private static GameEventScriptFatalRuntimeException DeclaredFault(string symbol, string context)
    {
        const string code = "test.callbackFault";
        const string message = "Configured external declared fault.";
        if (context == "none") return new GameEventScriptExtensionFaultException(code, message, symbol);
        return new ContextualFault(new GameEventScriptDiagnostic(GameEventScriptDiagnosticPhase.Runtime, code, message, Symbol: symbol,
            ProgramName: context is "program" or "both" ? "reported.program" : null,
            HandlerName: context is "handler" or "both" ? "Reported()" : null));
    }

    private sealed class ContextualFault(GameEventScriptDiagnostic diagnostic) : GameEventScriptFatalRuntimeException(diagnostic);
}
