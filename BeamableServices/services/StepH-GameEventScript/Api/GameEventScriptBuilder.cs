using System;
using System.Collections.Generic;
using System.IO;
using StepH.GameEventScript.Compiler;
using static StepH.GameEventScript.Api.GameEventScriptCompileErrorKind;
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
    private IGameEventScriptExternalTypeRegistry _externalTypeRegistry = GameEventScriptEmptyExternalTypeRegistry.Instance;

    /// <summary>
    /// Creates an instance of the GameEventScriptBuilder.
    /// </summary>
    /// <returns>A new instance of the GameEventScriptBuilder.</returns>
    public static GameEventScriptBuilder Create() => new();

    /// <summary>
    /// Enables or disables optimization before bytecode generation.
    /// </summary>
    /// <param name="enabled">A boolean value indicating whether optimization should be enabled. Defaults to true.</param>
    /// <returns>The current instance of <see cref="GameEventScriptBuilder"/> with the specified optimization setting applied.</returns>
    public GameEventScriptBuilder WithOptimization(bool enabled = true)
    {
        _options = new GameEventScriptCompileOptions
        {
            Optimize = enabled,
            EnableDebugInfo = _options.EnableDebugInfo
        };
        return this;
    }

    /// <summary>
    /// Enables or disables debug metadata generation for bytecode dumps and tooling.
    /// </summary>
    /// <param name="enabled">A boolean value indicating whether debug metadata should be generated. Defaults to true.</param>
    /// <returns>The current instance of <see cref="GameEventScriptBuilder"/> with the specified debug setting applied.</returns>
    public GameEventScriptBuilder WithDebugInfo(bool enabled = true)
    {
        _options = new GameEventScriptCompileOptions
        {
            Optimize = _options.Optimize,
            EnableDebugInfo = enabled
        };
        return this;
    }

    /// <summary>
    /// Configures external CLR-backed GameEventScript types that may be referenced by the compiled scripts.
    /// </summary>
    /// <param name="registry">The external type registry to use during compilation.</param>
    /// <returns>The current builder instance.</returns>
    public GameEventScriptBuilder WithExternalTypes(IGameEventScriptExternalTypeRegistry registry)
    {
        _externalTypeRegistry = registry ?? throw new ArgumentNullException(nameof(registry));
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
        _sources.Add(new SourceInput(text ?? throw new ArgumentNullException(nameof(text)), sourceName));
        return this;
    }

    /// <summary>
    /// Adds a GameEventScript source file by reading its content from the specified file path.
    /// </summary>
    /// <param name="path">The path to the script file to be added.</param>
    /// <returns>
    /// The current instance of <see cref="GameEventScriptBuilder"/> to allow method chaining.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when the provided <paramref name="path"/> is null.
    /// </exception>
    public GameEventScriptBuilder AddFile(string path)
    {
        _ = path ?? throw new ArgumentNullException(nameof(path));
        return AddScript(File.ReadAllText(path), path);
    }

    /// <summary>
    /// Compiles the configured GameEventScript sources into GameEventScript bytecode.
    /// </summary>
    /// <param name="options">Optional compilation options that specify settings for bytecode generation.</param>
    /// <returns>The generated GameEventScript bytecode.</returns>
    public GameEventScriptProgram Compile(GameEventScriptCompileOptions? options = null)
    {
        var compileOptions = options ?? _options;
        return GesCompiler.Compile(BuildModule(compileOptions));
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
            modules[index] = GesParser.Parse(source.Text, source.SourceName);
        }

        var errors = new GesValidationErrors();
        var typeDefinitions = BuildTypeDefinitionMap(modules, errors);
        var externalTypeDefinitions = _externalTypeRegistry.Types;
        var validationTypeDefinitions = BuildValidationTypeDefinitionMap(typeDefinitions, externalTypeDefinitions, modules, errors);
        var predicateDefinitions = BuildPredicateDefinitionMap(modules, errors);
        var functionDefinitions = BuildFunctionDefinitionMap(modules, errors);
        var callables = BuildCallableDefinitionMap(predicateDefinitions, functionDefinitions);
        var handlers = BuildHandlerMap(modules);

        foreach (var conflictName in predicateDefinitions.Keys)
        {
            if (!functionDefinitions.ContainsKey(conflictName))
            {
                continue;
            }

            var conflictModule = FindModuleWithCallable(modules, conflictName, true) ??
                                 FindModuleWithCallable(modules, conflictName, false);
            var conflictNode = FindCallableNode(conflictModule?.FunctionDefinitions, conflictName) ??
                               (ScriptNode?)FindCallableNode(conflictModule?.PredicateDefinitions, conflictName);
            errors.Add(conflictModule, $"Name '{conflictName}' is declared as both a predicate and a function", conflictName, GlobalDefinition, PredicateFunctionConflict, conflictNode);
        }

        foreach (var module in modules)
        {
            GesAstValidator.ValidateModule(module, callables, validationTypeDefinitions, compileOptions, errors);
        }

        errors.ThrowIfAny();

        var moduleResult = new GesSyntaxTreeModule(ResolveModuleName(modules), typeDefinitions, callables, handlers, externalTypeDefinitions);
        return compileOptions.Optimize ? GesAstOptimizer.Optimize(moduleResult) : moduleResult;
    }

    private sealed record SourceInput(string Text, string? SourceName);

    private static string ResolveModuleName(IReadOnlyList<ParsedScript> modules)
        => modules.Count switch
        {
            0 => "EmptyModule",
            1 => modules[0].ModuleName,
            _ => JoinModuleNames(modules)
        };

    private static string JoinModuleNames(IReadOnlyList<ParsedScript> modules)
    {
        var result = modules[0].ModuleName;
        for (var index = 1; index < modules.Count; index++)
        {
            result += "+" + modules[index].ModuleName;
        }

        return result;
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

    private static IReadOnlyDictionary<string, TypeDefinitionNode> BuildValidationTypeDefinitionMap(IReadOnlyDictionary<string, TypeDefinitionNode> scriptTypes, IReadOnlyDictionary<string, GameEventScriptExternalTypeDefinition> externalTypes,
        IReadOnlyList<ParsedScript> modules, GesValidationErrors errors)
    {
        if (externalTypes.Count == 0) return scriptTypes;
        var map = new Dictionary<string, TypeDefinitionNode>(scriptTypes, StringComparer.Ordinal);
        foreach (var externalType in externalTypes.Values)
        {
            if (map.ContainsKey(externalType.Name))
            {
                var module = FindModuleWithType(modules, externalType.Name);
                errors.Add(module, $"Type '{externalType.Name}' is defined both as a script record and an external type", externalType.Name, GameEventScriptSymbolKind.Type, DuplicateType);
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
                    errors.Add(module, $"Type '{typeDefinition.Name}' is defined more than once", typeDefinition.Name, GameEventScriptSymbolKind.Type, DuplicateType, typeDefinition);
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
                if (!map.TryAdd(predicateDefinition.Name, predicateDefinition)) errors.Add(module, $"Predicate '{predicateDefinition.Name}' is defined more than once", predicateDefinition.Name, Predicate, DuplicatePredicate, predicateDefinition);
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
                if (!map.TryAdd(functionDefinition.Name, functionDefinition)) errors.Add(module, $"Function '{functionDefinition.Name}' is defined more than once", functionDefinition.Name, Function, DuplicateFunction, functionDefinition);
            }
        }

        return map;
    }

    private static Dictionary<string, GesCallableDefinition> BuildCallableDefinitionMap(IReadOnlyDictionary<string, PredicateDefinitionNode> predicateDefinitions, IReadOnlyDictionary<string, FunctionDefinitionNode> functionDefinitions)
    {
        var map = new Dictionary<string, GesCallableDefinition>(StringComparer.Ordinal);

        foreach (var pair in predicateDefinitions)
        {
            map[pair.Key] = new GesCallableDefinition(pair.Key, pair.Value.ParameterList, pair.Value.Expression, PredicateCall, pair.Value.SourceRange);
        }

        foreach (var pair in functionDefinitions)
        {
            map[pair.Key] = new GesCallableDefinition(pair.Key, pair.Value.ParameterList, pair.Value.Expression, FunctionCall, pair.Value.SourceRange);
        }

        return map;
    }
}
