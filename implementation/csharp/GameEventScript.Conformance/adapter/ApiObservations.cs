// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using GameEventScript.Api;

internal static class ApiObservations
{
    internal static object Run(JsonElement test)
    {
        var input = test.GetProperty("api");
        if (test.GetProperty("kind").GetString() == "messageApi") return MessageApiObservations.Run(input);
        if (test.GetProperty("kind").GetString() == "valueApi") return ValueApiObservations.Run(input);
        if (test.GetProperty("kind").GetString() == "externalTypeApi")
        {
            var names = input.GetProperty("typeNames").EnumerateArray().Select(n => n.GetString()!).ToArray();
            var definitions = names.Select(name => new GameEventScriptExternalTypeDefinition(name, [], [])).ToArray();
            try
            {
                var catalog = new GameEventScriptExternalTypeCatalog(definitions);
                return new { status = "ok", externalType = new { typeCount = catalog.Types.Count } };
            }
            catch (ArgumentException) when (names.Distinct(StringComparer.Ordinal).Count() != names.Length)
            {
                return new { status = "ok", externalType = new { error = "duplicateTypeName" } };
            }
        }
        var program = AdapterCompiler.Compile(test);
        var builder = GameEventScriptHost.CreateBuilder().WithRegistry(new RuntimeFixtures()).WithRuntimeLimits(RuntimeObservations.CreateLimits(input.GetProperty("limits")));
        switch (input.GetProperty("externalTypeRegistry").GetString())
        {
            case "environment": builder.WithExternalTypeRegistry(GameEventScript.Tests.Conformance.GameEventScriptConformanceExternalTypes.Registry); break;
            case "mismatch": builder.WithExternalTypeRegistry(new MismatchRegistry()); break;
        }
        var host = builder.Build();
        try
        {
            host.Load(program);
            return new { status = "loaded", diagnostics = Array.Empty<object>() };
        }
        catch (GameEventScriptDynamicLinkException exception)
        {
            return new { status = "loadError", diagnostics = new[] { CompilerObservations.Diagnostic(exception.Diagnostic) } };
        }
    }

    private sealed class MismatchRegistry : IGameEventScriptExternalTypeRegistry
    {
        /// <summary>Supplies the deliberate mismatched-constructor fixture for link validation.</summary>
        public IGameEventScriptExternalTypeConstructor? Resolve(GameEventScriptExternalTypeConstructorReference reference) => new MismatchConstructor();
    }

    private sealed class MismatchConstructor : IGameEventScriptExternalTypeConstructor
    {
        /// <summary>Declares a different type and parameter shape from the compiler catalog.</summary>
        public GameEventScriptExternalTypeConstructorDefinition Definition { get; } = new("Mismatch", [new GameEventScriptExternalTypeParameterDefinition("value", GameEventScriptBytecodeTypeKind.Float)]);

        /// <summary>Returns Nothing if invoked; the load-error fixture never starts the host.</summary>
        public void Invoke(GesExternalTypeConstructorCall call) => call.SetNothing();
    }
}
