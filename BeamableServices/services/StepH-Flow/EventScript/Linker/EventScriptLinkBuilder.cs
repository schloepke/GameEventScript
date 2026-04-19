#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections.Generic;
using System.Linq;
using StepH.Flow.EventScript.Parser;

namespace StepH.Flow.EventScript.Linker;

public sealed partial class EventScriptLinkBuilder
{
    private readonly List<EventScriptModule> _modules = [];

    public int SourceCount => _modules.Count;

    public static LinkedEventScriptModule LinkScripts(params string[] scripts)
    {
        var builder = new EventScriptLinkBuilder();
        foreach (var script in scripts)
        {
            builder.AddScript(script);
        }

        return builder.Link();
    }

    public static LinkedEventScriptModule LinkModules(params EventScriptModule[] modules)
    {
        var builder = new EventScriptLinkBuilder();
        foreach (var module in modules)
        {
            builder.AddModule(module);
        }

        return builder.Link();
    }

    public EventScriptLinkBuilder AddScript(string script, string? sourceName = null)
    {
        _modules.Add(EventScriptParser.Parse(script, sourceName));
        return this;
    }

    public EventScriptLinkBuilder AddModule(EventScriptModule eventScriptModule)
    {
        _modules.Add(eventScriptModule ?? throw new ArgumentNullException(nameof(eventScriptModule)));
        return this;
    }

    public LinkedEventScriptModule Link()
    {
        var errors = new List<EventScriptLinkageError>();
        var typeDefinitions = BuildTypeDefinitionMap(_modules, errors);
        var ruleDefinitions = BuildRuleDefinitionMap(_modules, errors);
        var selectDefinitions = BuildSelectDefinitionMap(_modules, errors);
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
                EventScriptSymbolKind.GlobalDefinition,
                EventScriptLinkageErrorKind.RuleSelectConflict));
        }

        foreach (var module in _modules)
        {
            ValidateModule(module, ruleDefinitions, selectDefinitions, errors);
        }

        if (errors.Count > 0)
        {
            throw new EventScriptLinkageException(errors);
        }

        return new LinkedEventScriptModule(
            typeDefinitions,
            ruleDefinitions,
            selectDefinitions,
            handlers,
            _modules.Count);
    }

    private static EventScriptLinkageError CreateError(
        EventScriptModule? module,
        string message,
        string symbol,
        EventScriptSymbolKind symbolKind,
        EventScriptLinkageErrorKind kind,
        int? line = null,
        int? column = null)
    {
        var resolvedModuleName = module?.ModuleName ?? "AnonymousModule_Unknown";
        var resolvedSourceName = module?.SourceName ?? "UnknownSource_Unknown";
        return new EventScriptLinkageError(
            message,
            resolvedModuleName,
            symbol,
            symbolKind,
            kind,
            new EventScriptSourceLocation(resolvedSourceName, line, column));
    }
}
