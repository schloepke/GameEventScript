using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using StepH.GameEventScript.VirtualMachine;
using StepH.GameEventScript.Compiler;
using StepH.GameEventScript.Runtime;
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
    /// Enables or disables debug metadata generation for bytecode diagnostics, dumps, and tooling.
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
    /// Configures external CLR-backed GameEventScript types by scanning annotated CLR types.
    /// </summary>
    /// <param name="types">CLR types annotated with <see cref="GesTypeAttribute"/>.</param>
    /// <returns>The current builder instance.</returns>
    public GameEventScriptBuilder WithExternalTypes(params Type[] types)
        => WithExternalTypes(GameEventScriptExternalTypeRegistry.Create(types));

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
    public GameEventScriptBinary Compile(GameEventScriptCompileOptions? options = null)
    {
        var compileOptions = options ?? _options;
        return GesBinaryCompiler.Compile(BuildModule(compileOptions));
    }

    /// <summary>
    /// Compiles the configured GameEventScript sources into a bindable runtime module.
    /// </summary>
    /// <param name="options">Optional compilation options that specify settings for bytecode generation.</param>
    /// <returns>A bindable runtime module that exports the compiled message handlers.</returns>
    public IGameEventScriptModule CompileModule(GameEventScriptCompileOptions? options = null)
        => GameEventScriptVirtualMaschine.Create(Compile(options), 512, 128);

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
        var modules = _sources.Select(source => GesParser.Parse(source.Text, source.SourceName, compileOptions)).ToArray();
        var errors = new GesValidationErrors();
        var typeDefinitions = BuildTypeDefinitionMap(modules, errors);
        var externalTypeDefinitions = _externalTypeRegistry.Types;
        var validationTypeDefinitions = BuildValidationTypeDefinitionMap(typeDefinitions, externalTypeDefinitions, modules, errors);
        var predicateDefinitions = BuildPredicateDefinitionMap(modules, errors);
        var functionDefinitions = BuildFunctionDefinitionMap(modules, errors);
        var callables = BuildCallableDefinitionMap(predicateDefinitions, functionDefinitions);
        var handlers = BuildHandlerMap(modules).ToDictionary(pair => pair.Key, IReadOnlyList<EventHandlerNode> (pair) => pair.Value, StringComparer.Ordinal);

        foreach (var conflictName in predicateDefinitions.Keys.Where(functionDefinitions.ContainsKey))
        {
            var conflictModule = modules.FirstOrDefault(module => module.FunctionDefinitions.Any(function => string.Equals(function.Name, conflictName, StringComparison.Ordinal))) ??
                                 modules.FirstOrDefault(module => module.PredicateDefinitions.Any(predicate => string.Equals(predicate.Name, conflictName, StringComparison.Ordinal)));
            var conflictNode = conflictModule?.FunctionDefinitions.FirstOrDefault(function => string.Equals(function.Name, conflictName, StringComparison.Ordinal)) ??
                               (ScriptNode?)conflictModule?.PredicateDefinitions.FirstOrDefault(predicate => string.Equals(predicate.Name, conflictName, StringComparison.Ordinal));
            errors.Add(conflictModule, $"Name '{conflictName}' is declared as both a predicate and a function", conflictName, GlobalDefinition, PredicateFunctionConflict, conflictNode);
        }

        foreach (var module in modules)
        {
            GesAstValidator.ValidateModule(module, callables, validationTypeDefinitions, compileOptions, errors);
        }

        errors.ThrowIfAny();

        var moduleResult = new GesSyntaxTreeModule(ResolveModuleName(modules), typeDefinitions, callables, handlers, externalTypeDefinitions);
        return compileOptions.Optimize ? GesAstOptimizer.Optimize(moduleResult, compileOptions) : moduleResult;
    }

    private sealed record SourceInput(string Text, string? SourceName);

    private static string ResolveModuleName(IReadOnlyList<ParsedScript> modules)
        => modules.Count switch
        {
            0 => "EmptyModule",
            1 => modules[0].ModuleName,
            _ => string.Join("+", modules.Select(module => module.ModuleName))
        };

    private static Dictionary<string, List<EventHandlerNode>> BuildHandlerMap(IReadOnlyList<ParsedScript> modules)
    {
        var map = new Dictionary<string, List<EventHandlerNode>>(StringComparer.Ordinal);

        foreach (var module in modules)
        {
            foreach (var handler in module.Handlers)
            {
                if (!map.TryGetValue(handler.Message, out var handlers))
                {
                    handlers = [];
                    map[handler.Message] = handlers;
                }

                handlers.Add(handler);
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
                var module = modules.FirstOrDefault(parsedModule => parsedModule.TypeDefinitions.Any(type => string.Equals(type.Name, externalType.Name, StringComparison.Ordinal)));
                errors.Add(module, $"Type '{externalType.Name}' is defined both as a script record and an external type", externalType.Name, GameEventScriptSymbolKind.Type, DuplicateType);
                continue;
            }

            var fields = externalType.Fields.Select(field => new TypeFieldDefinitionNode(field.Name, field.TypeName, null, null, null, field.Name)).ToArray();
            map[externalType.Name] = new TypeDefinitionNode(externalType.Name, fields);
        }

        return map;
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
            map[pair.Key] = new GesCallableDefinition(pair.Key, pair.Value.ParameterList.ToArray(), pair.Value.Expression, PredicateCall, pair.Value.SourceRange);
        }

        foreach (var pair in functionDefinitions)
        {
            map[pair.Key] = new GesCallableDefinition(pair.Key, pair.Value.ParameterList.ToArray(), pair.Value.Expression, FunctionCall, pair.Value.SourceRange);
        }

        return map;
    }
}
