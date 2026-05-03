using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using StepH.GameEventScript.RegisterVM;

namespace StepH.GameEventScript.Compiler;

/// <summary>
/// Provides functionality to construct and compile game event script modules.
/// Allows the addition of scripts, script files, parsed modules, and
/// supports enabling or disabling optimization during the module-building process.
/// </summary>
public sealed class GameEventScriptModuleBuilder
{
    private readonly List<ParsedModule> _modules = [];
    private bool _optimize = true;

    /// <summary>
    /// Creates an instance of the GameEventScriptModuleBuilder.
    /// </summary>
    /// <returns>A new instance of the GameEventScriptModuleBuilder.</returns>
    public static GameEventScriptModuleBuilder Create() => new();

    /// <summary>
    /// Enables or disables optimization for the module-building process.
    /// </summary>
    /// <param name="enabled">A boolean value indicating whether optimization should be enabled. Defaults to true.</param>
    /// <returns>The current instance of <see cref="GameEventScriptModuleBuilder"/> with the specified optimization setting applied.</returns>
    public GameEventScriptModuleBuilder WithOptimization(bool enabled = true)
    {
        _optimize = enabled;
        return this;
    }

    /// <summary>
    /// Adds a script to the module builder.
    /// </summary>
    /// <param name="text">The content of the script to be added.</param>
    /// <param name="sourceName">
    /// An optional string representing the source name of the script (e.g., file name).
    /// </param>
    /// <returns>
    /// The current instance of <see cref="GameEventScriptModuleBuilder"/> to allow method chaining.
    /// </returns>
    public GameEventScriptModuleBuilder AddScript(string text, string? sourceName = null)
    {
        _modules.Add(GesParser.Parse(text, sourceName));
        return this;
    }

    /// <summary>
    /// Adds a script file to the module builder by reading its content from the specified file path.
    /// </summary>
    /// <param name="path">The path to the script file to be added.</param>
    /// <returns>
    /// The current instance of <see cref="GameEventScriptModuleBuilder"/> to allow method chaining.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when the provided <paramref name="path"/> is null.
    /// </exception>
    public GameEventScriptModuleBuilder AddFile(string path)
    {
        _ = path ?? throw new ArgumentNullException(nameof(path));
        return AddScript(File.ReadAllText(path), path);
    }

    /// <summary>
    /// Builds and returns a new instance of the <see cref="GameEventScriptModule"/> class
    /// based on the currently configured modules and their definitions.
    /// </summary>
    /// <returns>A compiled <see cref="GameEventScriptModule"/> instance.</returns>
    /// <exception cref="GameEventScriptModuleBuildException">
    /// Thrown when errors are encountered during the build process.
    /// </exception>
    public GameEventScriptModule Build()
    {
        var errors = new GesValidationErrors();
        var typeDefinitions = BuildTypeDefinitionMap(_modules, errors);
        var ruleDefinitions = BuildRuleDefinitionMap(_modules, errors);
        var selectDefinitions = BuildSelectDefinitionMap(_modules, errors);
        var callables = BuildCallableDefinitionMap(ruleDefinitions, selectDefinitions);
        var handlers = BuildHandlerMap(_modules)
            .ToDictionary(pair => pair.Key, pair => (IReadOnlyList<EventHandlerNode>)pair.Value, StringComparer.Ordinal);

        foreach (var conflictName in ruleDefinitions.Keys.Where(selectDefinitions.ContainsKey))
        {
            var conflictModule = _modules.FirstOrDefault(module => module.SelectDefinitions.Any(select => string.Equals(select.Name, conflictName, StringComparison.Ordinal))) ??
                                 _modules.FirstOrDefault(module => module.RuleDefinitions.Any(rule => string.Equals(rule.Name, conflictName, StringComparison.Ordinal)));
            var conflictNode = conflictModule?.SelectDefinitions.FirstOrDefault(select => string.Equals(select.Name, conflictName, StringComparison.Ordinal)) ??
                               (ScriptNode?)conflictModule?.RuleDefinitions.FirstOrDefault(rule => string.Equals(rule.Name, conflictName, StringComparison.Ordinal));

            errors.Add(
                conflictModule,
                $"Name '{conflictName}' is declared as both a rule and a select",
                conflictName,
                GameEventScriptSymbolKind.GlobalDefinition,
                GameEventScriptModuleBuildErrorKind.RuleSelectConflict,
                conflictNode);
        }

        foreach (var module in _modules)
        {
            GesValidator.ValidateModule(module, callables, typeDefinitions, errors);
        }

        errors.ThrowIfAny();

        var moduleResult = new GameEventScriptModule(typeDefinitions, callables, handlers);
        return _optimize ? GesOptimizer.Optimize(moduleResult) : moduleResult;
    }

    /// <summary>
    /// Compiles the game event script module into a final executable form.
    /// </summary>
    /// <param name="options">Optional compilation options that specify settings for the compilation process.</param>
    /// <returns>A compiled game event script that can be executed within the scripting environment.</returns>
    public CompiledGameEventScript Compile(GameEventScriptCompilationOptions? options = null) => RegisterVmCompiler.Compile(Build(), options);

    private static Dictionary<string, List<EventHandlerNode>> BuildHandlerMap(IReadOnlyList<ParsedModule> modules)
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

    private static Dictionary<string, TypeDefinitionNode> BuildTypeDefinitionMap(IReadOnlyList<ParsedModule> modules, GesValidationErrors errors)
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
                        GameEventScriptModuleBuildErrorKind.DuplicateType,
                        typeDefinition);
                }
            }
        }

        return map;
    }

    private static Dictionary<string, RuleDefinitionNode> BuildRuleDefinitionMap(IReadOnlyList<ParsedModule> modules, GesValidationErrors errors)
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
                        GameEventScriptModuleBuildErrorKind.DuplicateRule,
                        ruleDefinition);
                }
            }
        }

        return map;
    }

    private static Dictionary<string, SelectDefinitionNode> BuildSelectDefinitionMap(IReadOnlyList<ParsedModule> modules, GesValidationErrors errors)
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
                        GameEventScriptModuleBuildErrorKind.DuplicateSelect,
                        selectDefinition);
                }
            }
        }

        return map;
    }

    private static Dictionary<string, GameEventScriptCallableDefinition> BuildCallableDefinitionMap(
        IReadOnlyDictionary<string, RuleDefinitionNode> ruleDefinitions,
        IReadOnlyDictionary<string, SelectDefinitionNode> selectDefinitions)
    {
        var map = new Dictionary<string, GameEventScriptCallableDefinition>(StringComparer.Ordinal);

        foreach (var pair in ruleDefinitions)
        {
            map[pair.Key] = new GameEventScriptCallableDefinition(
                pair.Key,
                pair.Value.ParameterList.ToArray(),
                pair.Value.Expression,
                GameEventScriptCallableKind.Rule,
                pair.Value.SourceRange);
        }

        foreach (var pair in selectDefinitions)
        {
            map[pair.Key] = new GameEventScriptCallableDefinition(
                pair.Key,
                pair.Value.ParameterList.ToArray(),
                pair.Value.Expression,
                GameEventScriptCallableKind.Select,
                pair.Value.SourceRange);
        }

        return map;
    }
}
