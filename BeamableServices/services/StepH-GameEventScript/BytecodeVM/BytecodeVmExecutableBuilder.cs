#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections.Generic;
using System.Linq;
using StepH.GameEventScript.Api;
using StepH.GameEventScript.Compiler;
using StepH.GameEventScript.Runtime;

namespace StepH.GameEventScript.BytecodeVM;

internal static class BytecodeVmExecutableBuilder
{
    public static CompiledGameEventScript Build(GameEventScriptBytecode bytecode)
    {
        _ = bytecode ?? throw new ArgumentNullException(nameof(bytecode));
        var module = bytecode.SourceModule ?? throw new GameEventScriptCompilationException(
            "BytecodeVM cannot build an executable from detached bytecode yet. Compile bytecode with GameEventScriptBuilder so source metadata is available.");
        var handlers = BuildHandlers(module, bytecode, bytecode.Options);
        var typeDefinitions = BuildTypeDefinitions(module);
        return new CompiledGameEventScript(bytecode.Options, bytecode, handlers, module.Callables, typeDefinitions);
    }

    public static CompiledGameEventScript Build(
        GameEventScriptModule module,
        GameEventScriptCompilationOptions? options = null)
    {
        _ = module ?? throw new ArgumentNullException(nameof(module));
        var compileOptions = options ?? new GameEventScriptCompilationOptions();
        var bytecode = GesBytecodeCompiler.Compile(module, compileOptions);
        return Build(bytecode);
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<CompiledGameEventScriptHandler>> BuildHandlers(
        GameEventScriptModule module,
        GameEventScriptBytecode bytecode,
        GameEventScriptCompilationOptions options)
    {
        return module.Handlers.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlyList<CompiledGameEventScriptHandler>)pair.Value
                .Select((handler, index) => new CompiledGameEventScriptHandler(
                    pair.Key,
                    handler.Parameters,
                    handler.SignatureLabels,
                    GameEventScriptMessageSignature.CreateSignatureId(pair.Key, handler.SignatureLabels),
                    index,
                    FindProgramIndex(bytecode, $"handler:{pair.Key}#{index}"),
                    options.EnableDiagnostics,
                    handler.Statements,
                    BytecodeVmExecutionPlanBuilder.CompileHandlerPlan(
                        pair.Key,
                        index,
                        handler.Parameters,
                        handler.Statements,
                        module.Callables,
                        module.TypeDefinitions,
                        reference => ResolveExternalReference(bytecode, reference))))
                .ToArray(),
            StringComparer.Ordinal);
    }

    private static IReadOnlyDictionary<string, BytecodeVmTypeDefinition> BuildTypeDefinitions(GameEventScriptModule module)
    {
        return module.TypeDefinitions.ToDictionary(
            pair => pair.Key,
            pair => new BytecodeVmTypeDefinition(pair.Value),
            StringComparer.Ordinal);
    }

    private static int FindProgramIndex(GameEventScriptBytecode bytecode, string programName)
    {
        for (var index = 0; index < bytecode.Programs.Count; index++)
        {
            if (string.Equals(bytecode.Programs[index].Name, programName, StringComparison.Ordinal))
            {
                return index;
            }
        }

        return -1;
    }

    private static int ResolveExternalReference(GameEventScriptBytecode bytecode, GameEventScriptExtensionReference reference)
    {
        if (GameEventScriptStandardExtensions.IsStandardReference(reference))
        {
            return -1;
        }

        for (var index = 0; index < bytecode.ExternalReferences.Count; index++)
        {
            if (string.Equals(bytecode.ExternalReferences[index].SignatureId, reference.SignatureId, StringComparison.Ordinal))
            {
                return index;
            }
        }

        throw new InvalidOperationException($"BytecodeVM invariant failed: external reference '{reference.SignatureId}' was not emitted into bytecode.");
    }
}
