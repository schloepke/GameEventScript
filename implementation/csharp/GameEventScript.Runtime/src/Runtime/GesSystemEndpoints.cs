// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using GameEventScript.Api;

namespace GameEventScript.Runtime;

internal static class GesSystemEndpoints
{
    public const string InitializationName = "initialization";
    public const string UndeliverableName = "undeliverable";
    public const string MessageArgumentName = "message";

    public static readonly string InitializationSignatureId = GesMessageSignature.CreateSignatureId(InitializationName, []);

    public static readonly string UndeliverableSignatureId = GesMessageSignature.CreateSignatureId(UndeliverableName, [MessageArgumentName]);

    public static bool IsInitializationName(string? name)
        => string.Equals(name, InitializationName, System.StringComparison.Ordinal);

    public static bool IsUndeliverableName(string? name)
        => string.Equals(name, UndeliverableName, System.StringComparison.Ordinal);

    public static bool IsSystemEndpointName(string? name)
        => IsInitializationName(name) || IsUndeliverableName(name);

    public static GameEventScriptMessage CreateInitializationMessage()
        => GameEventScriptMessage.CreatePrecomputed(InitializationName, GesMessageArguments.Empty, InitializationSignatureId);

}
