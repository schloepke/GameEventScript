// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using StepH.GameEventScript.Compiler;
using StepH.GameEventScript.Runtime;
using static StepH.GameEventScript.Api.GameEventScriptDiagnosticCodes;
using static StepH.GameEventScript.Api.GameEventScriptSymbolKind;
using static StepH.GameEventScript.Compiler.GameEventScriptCallableKind;

namespace StepH.GameEventScript.Api;

/// <summary>
/// Provides functionality to compile GameEventScript source files into bytecode.
/// </summary>
public sealed class GameEventScriptBuilder
{
    private readonly List<SourceInput> _sources = [];
    private GameEventScriptCompileOptions _options = new();
    private IGameEventScriptExternalTypeCatalog _externalTypeCatalog = GameEventScriptEmptyExternalTypeCatalog.Instance;

    /// <summary>
    /// Creates an instance of the GameEventScriptBuilder.
    /// </summary>
    /// <returns>A new instance of the GameEventScriptBuilder.</returns>
    public static GameEventScriptBuilder Create() => new();

    /// <summary>
    /// Selects the portable debug metadata generated for bytecode dumps and tooling.
    /// </summary>
    /// <param name="options">The independently selectable debug sections. Defaults to all debug sections.</param>
    /// <returns>The current instance of <see cref="GameEventScriptBuilder"/> with the specified debug setting applied.</returns>
    public GameEventScriptBuilder WithDebugInfo(GameEventScriptDebugInfoOptions options = GameEventScriptDebugInfoOptions.All)
    {
        _options = new GameEventScriptCompileOptions
        {
            DebugInfo = options,
            ProgramVersion = _options.ProgramVersion
        };
        return this;
    }

    /// <summary>Sets the stable application-defined program version stored in binary metadata.</summary>
    /// <param name="version">The version, or zero for development/unspecified programs.</param>
    /// <returns>The current builder instance.</returns>
    public GameEventScriptBuilder WithProgramVersion(ulong version)
    {
        _options = new GameEventScriptCompileOptions
        {
            DebugInfo = _options.DebugInfo,
            ProgramVersion = version
        };
        return this;
    }

    /// <summary>
    /// Configures the declarative external GameEventScript types that may be referenced by compiled scripts.
    /// </summary>
    /// <param name="catalog">The portable external type catalog to use during compilation.</param>
    /// <returns>The current builder instance.</returns>
    public GameEventScriptBuilder WithExternalTypeCatalog(IGameEventScriptExternalTypeCatalog catalog)
    {
        _externalTypeCatalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        return this;
    }

    /// <summary>
    /// Adds a GameEventScript source string to the builder.
    /// </summary>
    /// <param name="text">The content of the script to be added.</param>
    /// <param name="sourceName">
    /// An optional string representing the source name of the script (e.g., file name).
    /// </param>
    /// <returns>
    /// The current instance of <see cref="GameEventScriptBuilder"/> to allow method chaining.
    /// </returns>
    public GameEventScriptBuilder AddScript(string text, string? sourceName = null)
    {
        var source = GameEventScriptText.PrepareSource(text ?? throw new ArgumentNullException(nameof(text)), nameof(text));
        _sources.Add(new SourceInput(source, sourceName));
        return this;
    }

    /// <summary>
    /// Compiles the configured GameEventScript sources into GameEventScript bytecode.
    /// </summary>
    /// <param name="options">Optional compilation options that specify settings for bytecode generation.</param>
    /// <returns>The generated GameEventScript bytecode.</returns>
    public GameEventScriptProgram Compile(GameEventScriptCompileOptions? options = null)
    {
        var compileOptions = options ?? _options;
        return GesCompiler.Compile(BuildModule(compileOptions), compileOptions);
    }

    /// <summary>
    /// Builds and returns a new internal module model based on the configured sources.
    /// </summary>
    /// <returns>A built <see cref="GesSyntaxTreeModule"/> instance.</returns>
    /// <exception cref="GameEventScriptCompileException">
    /// Thrown when errors are encountered during the build process.
    /// </exception>
    internal GesSyntaxTreeModule BuildModule(GameEventScriptCompileOptions? options = null)
    {
        var compileOptions = options ?? _options;
        var modules = new ParsedScript[_sources.Count];
        for (var index = 0; index < _sources.Count; index++)
        {
            var source = _sources[index];
            modules[index] = GesParser.Parse(source.Text, source.SourceName, checked((uint)index));
        }

        var errors = new GesValidationErrors();
        var constants = BuildConstantDefinitionMap(modules, errors);
        var typeDefinitions = BuildTypeDefinitionMap(modules, errors);
        var validationTypeDefinitions = BuildValidationTypeDefinitionMap(typeDefinitions, _externalTypeCatalog, modules, errors);
        var predicateDefinitions = BuildPredicateDefinitionMap(modules, errors);
        var functionDefinitions = BuildFunctionDefinitionMap(modules, errors);
        var callables = BuildCallableDefinitionMap(predicateDefinitions, functionDefinitions);
        var handlers = BuildHandlerMap(modules);

        var predicateNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var definition in predicateDefinitions.Values) predicateNames.Add(definition.Name);
        var reportedConflictNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var definition in functionDefinitions.Values)
        {
            var conflictName = definition.Name;
            if (!predicateNames.Contains(conflictName) || !reportedConflictNames.Add(conflictName))
            {
                continue;
            }

            var conflictModule = FindModuleWithCallable(modules, conflictName, true) ??
                                 FindModuleWithCallable(modules, conflictName, false);
            var conflictNode = FindCallableNode(conflictModule?.FunctionDefinitions, conflictName) ??
                               (ScriptNode?)FindCallableNode(conflictModule?.PredicateDefinitions, conflictName);
            errors.Add(conflictModule, $"Name '{conflictName}' is declared as both a predicate and a function", conflictName, GlobalDefinition, ValidatePredicateFunctionConflict, conflictNode);
        }

        foreach (var module in modules)
        {
            GesAstValidator.ValidateModule(module, callables, validationTypeDefinitions, constants, compileOptions, errors);
        }

        errors.ThrowIfAny();

        var sourceDocuments = new GesSourceDocument[_sources.Count];
        for (var index = 0; index < _sources.Count; index++)
        {
            var source = _sources[index];
            sourceDocuments[index] = new GesSourceDocument(checked((uint)index), string.IsNullOrEmpty(source.SourceName) ? "UnknownSource" : source.SourceName!, source.Text);
        }
        var moduleResult = new GesSyntaxTreeModule(ResolveModuleName(modules, errors), constants, typeDefinitions, callables, handlers, _externalTypeCatalog, sourceDocuments);
        errors.ThrowIfAny();
        return GesAstOptimizer.Optimize(moduleResult);
    }

    private sealed record SourceInput(string Text, string? SourceName);

    private static string ResolveModuleName(IReadOnlyList<ParsedScript> modules, GesValidationErrors errors)
    {
        var result = string.Empty;
        for (var index = 0; index < modules.Count; index++)
        {
            var candidate = modules[index].ModuleName;
            if (string.IsNullOrEmpty(candidate)) continue;
            if (string.IsNullOrEmpty(result))
            {
                result = candidate;
                continue;
            }

            if (!string.Equals(result, candidate, StringComparison.Ordinal))
            {
                errors.Add(modules[index], $"Module '{candidate}' does not match module '{result}' declared by another source", candidate, GlobalDefinition, ValidateInvalidIdentifierCase);
            }
        }

        return result;
    }

    private static Dictionary<string, ExpressionNode> BuildConstantDefinitionMap(IReadOnlyList<ParsedScript> modules, GesValidationErrors errors)
    {
        var map = new Dictionary<string, ExpressionNode>(StringComparer.Ordinal);
        foreach (var module in modules)
        {
            foreach (var definition in module.ConstantDefinitions)
            {
                if (!map.TryAdd(definition.Name, definition.Value))
                {
                    errors.Add(module, $"Constant '${definition.Name}' is defined more than once", definition.Name, GlobalDefinition, ValidateDuplicateConstant, definition);
                }
            }
        }

        return map;
    }

    private static ParsedScript? FindModuleWithCallable(IReadOnlyList<ParsedScript> modules, string name, bool function)
    {
        for (var moduleIndex = 0; moduleIndex < modules.Count; moduleIndex++)
        {
            var module = modules[moduleIndex];
            var definitions = function
                ? (IReadOnlyList<ScriptNode>)module.FunctionDefinitions
                : module.PredicateDefinitions;
            if (FindCallableNode(definitions, name) is not null)
            {
                return module;
            }
        }

        return null;
    }

    private static ScriptNode? FindCallableNode(IReadOnlyList<ScriptNode>? definitions, string name)
    {
        if (definitions is null)
        {
            return null;
        }

        for (var index = 0; index < definitions.Count; index++)
        {
            var definition = definitions[index];
            var definitionName = definition switch
            {
                FunctionDefinitionNode function => function.Name,
                PredicateDefinitionNode predicate => predicate.Name,
                _ => null
            };
            if (string.Equals(definitionName, name, StringComparison.Ordinal))
            {
                return definition;
            }
        }

        return null;
    }

    private static Dictionary<string, IReadOnlyList<EventHandlerNode>> BuildHandlerMap(IReadOnlyList<ParsedScript> modules)
    {
        var map = new Dictionary<string, IReadOnlyList<EventHandlerNode>>(StringComparer.Ordinal);

        foreach (var module in modules)
        {
            foreach (var handler in module.Handlers)
            {
                if (!map.TryGetValue(handler.Message, out var existing))
                {
                    var newHandlers = new List<EventHandlerNode>();
                    newHandlers.Add(handler);
                    map[handler.Message] = newHandlers;
                    continue;
                }

                ((List<EventHandlerNode>)existing).Add(handler);
            }
        }

        return map;
    }

    private static IReadOnlyDictionary<string, TypeDefinitionNode> BuildValidationTypeDefinitionMap(IReadOnlyDictionary<string, TypeDefinitionNode> scriptTypes, IGameEventScriptExternalTypeCatalog externalTypes,
        IReadOnlyList<ParsedScript> modules, GesValidationErrors errors)
    {
        if (externalTypes.Types.Count == 0) return scriptTypes;
        var map = new Dictionary<string, TypeDefinitionNode>(scriptTypes, StringComparer.Ordinal);
        for (var externalTypeIndex = 0; externalTypeIndex < externalTypes.Types.Count; externalTypeIndex++)
        {
            var externalType = externalTypes.Types[externalTypeIndex];
            if (map.ContainsKey(externalType.Name))
            {
                var module = FindModuleWithType(modules, externalType.Name);
                errors.Add(module, $"Type '{externalType.Name}' is defined both as a script record and an external type", externalType.Name, GameEventScriptSymbolKind.Type, ValidateDuplicateType);
                continue;
            }

            var fields = new TypeFieldDefinitionNode[externalType.Fields.Count];
            for (var index = 0; index < fields.Length; index++)
            {
                var field = externalType.Fields[index];
                fields[index] = new TypeFieldDefinitionNode(field.Name, field.TypeName, null, null, null, field.Name);
            }

            map[externalType.Name] = new TypeDefinitionNode(externalType.Name, fields);
        }

        return map;
    }

    private static ParsedScript? FindModuleWithType(IReadOnlyList<ParsedScript> modules, string name)
    {
        for (var moduleIndex = 0; moduleIndex < modules.Count; moduleIndex++)
        {
            var module = modules[moduleIndex];
            for (var typeIndex = 0; typeIndex < module.TypeDefinitions.Count; typeIndex++)
            {
                if (string.Equals(module.TypeDefinitions[typeIndex].Name, name, StringComparison.Ordinal))
                {
                    return module;
                }
            }
        }

        return null;
    }

    private static Dictionary<string, TypeDefinitionNode> BuildTypeDefinitionMap(IReadOnlyList<ParsedScript> modules, GesValidationErrors errors)
    {
        var map = new Dictionary<string, TypeDefinitionNode>(StringComparer.Ordinal);
        foreach (var module in modules)
        {
            foreach (var typeDefinition in module.TypeDefinitions)
            {
                if (!map.TryAdd(typeDefinition.Name, typeDefinition))
                {
                    errors.Add(module, $"Type '{typeDefinition.Name}' is defined more than once", typeDefinition.Name, GameEventScriptSymbolKind.Type, ValidateDuplicateType, typeDefinition);
                }
            }
        }

        return map;
    }

    private static Dictionary<string, PredicateDefinitionNode> BuildPredicateDefinitionMap(IReadOnlyList<ParsedScript> modules, GesValidationErrors errors)
    {
        var map = new Dictionary<string, PredicateDefinitionNode>(StringComparer.Ordinal);
        foreach (var module in modules)
        {
            foreach (var predicateDefinition in module.PredicateDefinitions)
            {
                var signatureId = GesCallableSignatures.Create(predicateDefinition.Name, predicateDefinition.ParameterList);
                if (!map.TryAdd(signatureId, predicateDefinition))
                    errors.Add(module, $"Predicate signature '{signatureId}' is defined more than once", predicateDefinition.Name, Predicate, ValidateDuplicatePredicate, predicateDefinition);
            }
        }

        return map;
    }

    private static Dictionary<string, FunctionDefinitionNode> BuildFunctionDefinitionMap(IReadOnlyList<ParsedScript> modules, GesValidationErrors errors)
    {
        var map = new Dictionary<string, FunctionDefinitionNode>(StringComparer.Ordinal);
        foreach (var module in modules)
        {
            foreach (var functionDefinition in module.FunctionDefinitions)
            {
                var signatureId = GesCallableSignatures.Create(functionDefinition.Name, functionDefinition.ParameterList);
                if (!map.TryAdd(signatureId, functionDefinition)) errors.Add(module, $"Function signature '{signatureId}' is defined more than once", functionDefinition.Name, Function, ValidateDuplicateFunction, functionDefinition);
            }
        }

        return map;
    }

    private static Dictionary<string, GesCallableDefinition> BuildCallableDefinitionMap(IReadOnlyDictionary<string, PredicateDefinitionNode> predicateDefinitions, IReadOnlyDictionary<string, FunctionDefinitionNode> functionDefinitions)
    {
        var map = new Dictionary<string, GesCallableDefinition>(StringComparer.Ordinal);

        foreach (var pair in predicateDefinitions)
        {
            map[pair.Key] = new GesCallableDefinition(pair.Value.Name, pair.Value.ParameterList, pair.Value.Expression, PredicateCall, pair.Value.SourceRange);
        }

        foreach (var pair in functionDefinitions)
        {
            map.TryAdd(pair.Key, new GesCallableDefinition(pair.Value.Name, pair.Value.ParameterList, pair.Value.Expression, FunctionCall, pair.Value.SourceRange));
        }

        return map;
    }
}
