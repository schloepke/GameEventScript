#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using StepH.GameEventScript.RegisterVM;

namespace StepH.GameEventScript.Compiler;

public sealed partial class GseModuleBuilder
{
    private readonly List<GseParsedModule> _modules = [];
    private bool _optimize = true;

    public static GseModuleBuilder Create() => new();

    public GseModuleBuilder WithOptimization(bool enabled = true)
    {
        _optimize = enabled;
        return this;
    }

    public GseModuleBuilder AddScript(string text, string? sourceName = null)
    {
        _modules.Add(GseParser.Parse(text, sourceName));
        return this;
    }

    public GseModuleBuilder AddFile(string path)
    {
        _ = path ?? throw new ArgumentNullException(nameof(path));
        return AddScript(File.ReadAllText(path), path);
    }

    public GseModuleBuilder AddParsedModule(GseParsedModule module)
    {
        _modules.Add(module ?? throw new ArgumentNullException(nameof(module)));
        return this;
    }

    public GseModule Build()
    {
        var errors = new List<GseModuleBuildError>();
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
                GseSymbolKind.GlobalDefinition,
                GseModuleBuildErrorKind.RuleSelectConflict));
        }

        foreach (var module in _modules)
        {
            ValidateModule(module, callableDefinitions, typeDefinitions, errors);
        }

        if (errors.Count > 0)
        {
            throw new GameEventScriptModuleBuildException(errors);
        }

        var moduleResult = new GseModule(typeDefinitions, callableDefinitions, handlers, _modules.Count);
        return _optimize ? GseModuleOptimizer.Optimize(moduleResult) : moduleResult;
    }

    public RegisterCompiledGse Compile(RegisterVmCompilationOptions? options = null)
        => RegisterVmCompiler.Compile(Build(), options);

    private static GseModuleBuildError CreateError(
        GseParsedModule? module,
        string message,
        string symbol,
        GseSymbolKind symbolKind,
        GseModuleBuildErrorKind kind,
        int? line = null,
        int? column = null,
        int? endLine = null,
        int? endColumn = null)
    {
        var resolvedModuleName = module?.ModuleName ?? "UnknownModule";
        var resolvedSourceName = module?.SourceName ?? "UnknownSource";
        return new GseModuleBuildError(
            message,
            resolvedModuleName,
            symbol,
            symbolKind,
            kind,
            new GseSourceLocation(resolvedSourceName, line, column, endLine, endColumn, resolvedModuleName));
    }
}
