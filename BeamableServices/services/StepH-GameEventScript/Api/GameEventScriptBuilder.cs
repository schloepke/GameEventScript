using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using StepH.GameEventScript.Compiler;

namespace StepH.GameEventScript.Api;

/// <summary>
/// Provides functionality to compile GameEventScript source files into bytecode.
/// </summary>
public sealed class GameEventScriptBuilder
{
    private readonly List<SourceInput> _sources = [];
    private GameEventScriptCompileOptions _options = new();

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
            EnableDiagnostics = _options.EnableDiagnostics
        };
        return this;
    }

    /// <summary>
    /// Enables or disables diagnostic information generation during the compilation process in order add trace generation into the compiled code.
    /// </summary>
    /// <param name="enabled">A boolean value indicating whether diagnostic information should be enabled. Defaults to true.</param>
    /// <returns>The current instance of <see cref="GameEventScriptBuilder"/> with the specified diagnostic setting applied.</returns>
    public GameEventScriptBuilder WithEnableDiagnostic(bool enabled = true)
    {
        _options = new GameEventScriptCompileOptions
        {
            Optimize = _options.Optimize,
            EnableDiagnostics = enabled
        };
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
    public GameEventScriptCompiled Compile(GameEventScriptCompileOptions? options = null)
    {
        var compileOptions = options ?? _options;
        return GesBytecodeCompiler.Compile(BuildModule(compileOptions), compileOptions);
    }

    /// <summary>
    /// Builds and returns a new internal module model based on the configured sources.
    /// </summary>
    /// <returns>A built <see cref="GseModule"/> instance.</returns>
    /// <exception cref="GameEventScriptCompileException">
    /// Thrown when errors are encountered during the build process.
    /// </exception>
    internal GseModule BuildModule(GameEventScriptCompileOptions? options = null)
    {
        var compileOptions = options ?? _options;
        var modules = _sources
            .Select(source => GesParser.Parse(source.Text, source.SourceName, compileOptions))
            .ToArray();
        var errors = new GesValidationErrors();
        var typeDefinitions = BuildTypeDefinitionMap(modules, errors);
        var ruleDefinitions = BuildRuleDefinitionMap(modules, errors);
        var selectDefinitions = BuildSelectDefinitionMap(modules, errors);
        var callables = BuildCallableDefinitionMap(ruleDefinitions, selectDefinitions);
        var handlers = BuildHandlerMap(modules)
            .ToDictionary(pair => pair.Key, pair => (IReadOnlyList<EventHandlerNode>)pair.Value, StringComparer.Ordinal);

        foreach (var conflictName in ruleDefinitions.Keys.Where(selectDefinitions.ContainsKey))
        {
            var conflictModule = modules.FirstOrDefault(module => module.SelectDefinitions.Any(select => string.Equals(select.Name, conflictName, StringComparison.Ordinal))) ??
                                 modules.FirstOrDefault(module => module.RuleDefinitions.Any(rule => string.Equals(rule.Name, conflictName, StringComparison.Ordinal)));
            var conflictNode = conflictModule?.SelectDefinitions.FirstOrDefault(select => string.Equals(select.Name, conflictName, StringComparison.Ordinal)) ??
                               (ScriptNode?)conflictModule?.RuleDefinitions.FirstOrDefault(rule => string.Equals(rule.Name, conflictName, StringComparison.Ordinal));

            errors.Add(
                conflictModule,
                $"Name '{conflictName}' is declared as both a rule and a select",
                conflictName,
                GameEventScriptSymbolKind.GlobalDefinition,
                GameEventScriptCompileErrorKind.RuleSelectConflict,
                conflictNode);
        }

        foreach (var module in modules)
        {
            GesValidator.ValidateModule(module, callables, typeDefinitions, compileOptions, errors);
        }

        errors.ThrowIfAny();

        var moduleResult = new GseModule(typeDefinitions, callables, handlers);
        return compileOptions.Optimize ? GesOptimizer.Optimize(moduleResult, compileOptions) : moduleResult;
    }

    private sealed record SourceInput(string Text, string? SourceName);

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

    private static Dictionary<string, TypeDefinitionNode> BuildTypeDefinitionMap(IReadOnlyList<ParsedScript> modules, GesValidationErrors errors)
    {
        var map = new Dictionary<string, TypeDefinitionNode>(StringComparer.Ordinal);
        foreach (var module in modules)
        {
            foreach (var typeDefinition in module.TypeDefinitions)
            {
                if (!map.TryAdd(typeDefinition.Name, typeDefinition))
                {
                    errors.Add(
                        module,
                        $"Type '{typeDefinition.Name}' is defined more than once",
                        typeDefinition.Name,
                        GameEventScriptSymbolKind.Type,
                        GameEventScriptCompileErrorKind.DuplicateType,
                        typeDefinition);
                }
            }
        }

        return map;
    }

    private static Dictionary<string, RuleDefinitionNode> BuildRuleDefinitionMap(IReadOnlyList<ParsedScript> modules, GesValidationErrors errors)
    {
        var map = new Dictionary<string, RuleDefinitionNode>(StringComparer.Ordinal);
        foreach (var module in modules)
        {
            foreach (var ruleDefinition in module.RuleDefinitions)
            {
                if (!map.TryAdd(ruleDefinition.Name, ruleDefinition))
                {
                    errors.Add(
                        module,
                        $"Rule '{ruleDefinition.Name}' is defined more than once",
                        ruleDefinition.Name,
                        GameEventScriptSymbolKind.Rule,
                        GameEventScriptCompileErrorKind.DuplicateRule,
                        ruleDefinition);
                }
            }
        }

        return map;
    }

    private static Dictionary<string, SelectDefinitionNode> BuildSelectDefinitionMap(IReadOnlyList<ParsedScript> modules, GesValidationErrors errors)
    {
        var map = new Dictionary<string, SelectDefinitionNode>(StringComparer.Ordinal);
        foreach (var module in modules)
        {
            foreach (var selectDefinition in module.SelectDefinitions)
            {
                if (!map.TryAdd(selectDefinition.Name, selectDefinition))
                {
                    errors.Add(
                        module,
                        $"Select '{selectDefinition.Name}' is defined more than once",
                        selectDefinition.Name,
                        GameEventScriptSymbolKind.Select,
                        GameEventScriptCompileErrorKind.DuplicateSelect,
                        selectDefinition);
                }
            }
        }

        return map;
    }

    private static Dictionary<string, GseCallableDefinition> BuildCallableDefinitionMap(IReadOnlyDictionary<string, RuleDefinitionNode> ruleDefinitions,
        IReadOnlyDictionary<string, SelectDefinitionNode> selectDefinitions)
    {
        var map = new Dictionary<string, GseCallableDefinition>(StringComparer.Ordinal);

        foreach (var pair in ruleDefinitions)
        {
            map[pair.Key] = new GseCallableDefinition(
                pair.Key,
                pair.Value.ParameterList.ToArray(),
                pair.Value.Expression,
                GameEventScriptCallableKind.Rule,
                pair.Value.SourceRange);
        }

        foreach (var pair in selectDefinitions)
        {
            map[pair.Key] = new GseCallableDefinition(
                pair.Key,
                pair.Value.ParameterList.ToArray(),
                pair.Value.Expression,
                GameEventScriptCallableKind.Select,
                pair.Value.SourceRange);
        }

        return map;
    }
}