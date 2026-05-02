#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections.Generic;
using System.Linq;
using StepH.GameEventScript.Parser;

namespace StepH.GameEventScript.Linker;

public sealed partial class EventScriptLinkBuilder
{
    private readonly List<EventScriptModule> _modules = [];

    public static LinkedEventScriptModule LinkModules(params EventScriptModule[] modules)
    {
        var builder = new EventScriptLinkBuilder();
        foreach (var module in modules)
        {
            builder.AddModule(module);
        }

        return builder.Link();
    }

    public static LinkedEventScriptModule LinkModules(IEnumerable<EventScriptModule> modules)
    {
        var builder = new EventScriptLinkBuilder();
        foreach (var module in modules)
        {
            builder.AddModule(module);
        }

        return builder.Link();
    }

    public EventScriptLinkBuilder AddModule(EventScriptModule eventScriptModule)
    {
        _modules.Add(eventScriptModule ?? throw new ArgumentNullException(nameof(eventScriptModule)));
        return this;
    }

    public LinkedEventScriptModule Link()
    {
        var errors = new List<EventScriptLinkageError>();
        var typeDefinitions = EventScriptLinkBuilder.BuildTypeDefinitionMap(_modules, errors);
        var ruleDefinitions = EventScriptLinkBuilder.BuildRuleDefinitionMap(_modules, errors);
        var selectDefinitions = EventScriptLinkBuilder.BuildSelectDefinitionMap(_modules, errors);
        var callableDefinitions = EventScriptLinkBuilder.BuildCallableDefinitionMap(ruleDefinitions, selectDefinitions);
        var handlers = EventScriptLinkBuilder.BuildHandlerMap(_modules, errors)
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
                EventScriptSymbolKind.GlobalDefinition,
                EventScriptLinkageErrorKind.RuleSelectConflict));
        }

        foreach (var module in _modules)
        {
            EventScriptLinkBuilder.ValidateModule(module, callableDefinitions, typeDefinitions, errors);
        }

        if (errors.Count > 0)
        {
            throw new EventScriptLinkageException(errors);
        }

        var linkedModule = new LinkedEventScriptModule(
            typeDefinitions,
            callableDefinitions,
            handlers,
            _modules.Count);

        return EventScriptLinkOptimizer.Optimize(linkedModule);
    }

    private static EventScriptLinkageError CreateError(
        EventScriptModule module,
        string message,
        string symbol,
        EventScriptSymbolKind symbolKind,
        EventScriptLinkageErrorKind kind,
        int? line = null,
        int? column = null,
        int? endLine = null,
        int? endColumn = null)
    {
        var resolvedModuleName = module.ModuleName;
        var resolvedSourceName = module.SourceName;
        return new EventScriptLinkageError(
            message,
            resolvedModuleName,
            symbol,
            symbolKind,
            kind,
            new EventScriptSourceLocation(resolvedSourceName, line, column, endLine, endColumn, resolvedModuleName));
    }
}
