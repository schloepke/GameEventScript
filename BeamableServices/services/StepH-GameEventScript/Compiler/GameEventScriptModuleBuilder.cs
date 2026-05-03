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
public sealed partial class GameEventScriptModuleBuilder
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
        var errors = new List<GameEventScriptModuleBuildError>();
        var typeDefinitions = BuildTypeDefinitionMap(_modules, errors);
        var ruleDefinitions = BuildRuleDefinitionMap(_modules, errors);
        var selectDefinitions = BuildSelectDefinitionMap(_modules, errors);
        var callableDefinitions = BuildCallableDefinitionMap(ruleDefinitions, selectDefinitions);
        var handlers = BuildHandlerMap(_modules, errors)
            .ToDictionary(
                pair => pair.Key,
                pair => (IReadOnlyList<EventHandlerNode>)pair.Value.AsReadOnly(),
                StringComparer.Ordinal);

        foreach (var ruleName in ruleDefinitions.Keys.Where(selectDefinitions.ContainsKey))
        {
            var conflictModule = _modules.FirstOrDefault(module =>
                module.RuleDefinitions.Any(rule => rule.Name == ruleName) ||
                module.SelectDefinitions.Any(select => select.Name == ruleName));
            errors.Add(CreateError(
                conflictModule,
                $"Global definition '{ruleName}' is defined as both rule and select",
                ruleName,
                GameEventScriptSymbolKind.GlobalDefinition,
                GameEventScriptModuleBuildErrorKind.RuleSelectConflict));
        }

        foreach (var module in _modules)
        {
            ValidateModule(module, callableDefinitions, typeDefinitions, errors);
        }

        if (errors.Count > 0)
        {
            throw new GameEventScriptModuleBuildException(errors);
        }

        var moduleResult = new GameEventScriptModule(typeDefinitions, callableDefinitions, handlers);
        return _optimize ? GesOptimizer.Optimize(moduleResult) : moduleResult;
    }

    /// <summary>
    /// Compiles the game event script module into a final executable form.
    /// </summary>
    /// <param name="options">Optional compilation options that specify settings for the compilation process.</param>
    /// <returns>A compiled game event script that can be executed within the scripting environment.</returns>
    public CompiledGameEventScript Compile(GameEventScriptCompilationOptions? options = null) => RegisterVmCompiler.Compile(Build(), options);

    private static GameEventScriptModuleBuildError CreateError(
        ParsedModule? module,
        string message,
        string symbol,
        GameEventScriptSymbolKind symbolKind,
        GameEventScriptModuleBuildErrorKind kind,
        int? line = null,
        int? column = null,
        int? endLine = null,
        int? endColumn = null)
    {
        var resolvedModuleName = module?.ModuleName ?? "UnknownModule";
        var resolvedSourceName = module?.SourceName ?? "UnknownSource";
        return new GameEventScriptModuleBuildError(
            message,
            resolvedModuleName,
            symbol,
            symbolKind,
            kind,
            new GameEventScriptSourceLocation(resolvedSourceName, line, column, endLine, endColumn, resolvedModuleName));
    }
}
