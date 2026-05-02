#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections.Generic;
using System.Linq;
using StepH.GameEventScript.Parser;

namespace StepH.GameEventScript.Linker;

public sealed partial class GseLinkBuilder
{
    private readonly List<GseModule> _modules = [];

    public static LinkedGseModule LinkModules(params GseModule[] modules)
    {
        var builder = new GseLinkBuilder();
        foreach (var module in modules)
        {
            builder.AddModule(module);
        }

        return builder.Link();
    }

    public static LinkedGseModule LinkModules(IEnumerable<GseModule> modules)
    {
        var builder = new GseLinkBuilder();
        foreach (var module in modules)
        {
            builder.AddModule(module);
        }

        return builder.Link();
    }

    public GseLinkBuilder AddModule(GseModule eventScriptModule)
    {
        _modules.Add(eventScriptModule ?? throw new ArgumentNullException(nameof(eventScriptModule)));
        return this;
    }

    public LinkedGseModule Link()
    {
        var errors = new List<GseLinkageError>();
        var typeDefinitions = GseLinkBuilder.BuildTypeDefinitionMap(_modules, errors);
        var ruleDefinitions = GseLinkBuilder.BuildRuleDefinitionMap(_modules, errors);
        var selectDefinitions = GseLinkBuilder.BuildSelectDefinitionMap(_modules, errors);
        var callableDefinitions = GseLinkBuilder.BuildCallableDefinitionMap(ruleDefinitions, selectDefinitions);
        var handlers = GseLinkBuilder.BuildHandlerMap(_modules, errors)
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
                GseLinkageErrorKind.RuleSelectConflict));
        }

        foreach (var module in _modules)
        {
            GseLinkBuilder.ValidateModule(module, callableDefinitions, typeDefinitions, errors);
        }

        if (errors.Count > 0)
        {
            throw new GseLinkageException(errors);
        }

        var linkedModule = new LinkedGseModule(
            typeDefinitions,
            callableDefinitions,
            handlers,
            _modules.Count);

        return GseLinkOptimizer.Optimize(linkedModule);
    }

    private static GseLinkageError CreateError(
        GseModule module,
        string message,
        string symbol,
        GseSymbolKind symbolKind,
        GseLinkageErrorKind kind,
        int? line = null,
        int? column = null,
        int? endLine = null,
        int? endColumn = null)
    {
        var resolvedModuleName = module.ModuleName;
        var resolvedSourceName = module.SourceName;
        return new GseLinkageError(
            message,
            resolvedModuleName,
            symbol,
            symbolKind,
            kind,
            new GseSourceLocation(resolvedSourceName, line, column, endLine, endColumn, resolvedModuleName));
    }
}
