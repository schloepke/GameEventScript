// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using GameEventScript.Api;

internal static class CompilerObservations
{
    internal static object Diagnostic(GesDiagnostic diagnostic) => new
    {
        phase = diagnostic.Phase.ToString().ToLowerInvariant(),
        code = diagnostic.Code,
        symbol = diagnostic.Symbol,
        symbolKind = diagnostic.SymbolKind == GesSymbolKind.Unknown ? null : char.ToLowerInvariant(diagnostic.SymbolKind.ToString()[0]) + diagnostic.SymbolKind.ToString()[1..],
        sourceName = diagnostic.SourceLocation?.SourceName,
        line = diagnostic.SourceLocation?.Line,
        column = diagnostic.SourceLocation?.Column,
        endLine = diagnostic.SourceLocation?.EndLine,
        endColumn = diagnostic.SourceLocation?.EndColumn,
        programName = diagnostic.ProgramName,
        handlerName = diagnostic.HandlerName
    };

    internal static object Success(string kind, GameEventScriptProgram program)
    {
        if (kind == "bytecodeSnapshot") return new { status = "ok", dump = program.Dump() };
        if (kind == "compileError") return new { status = "ok" };
        if (kind == "bytecode")
        {
            var counts = new Dictionary<string, ulong>(StringComparer.Ordinal);
            for (var index = 0; index < program.Code.Count; index++)
            {
                var name = program.Code[index].OpCode.ToString();
                counts[name] = counts.GetValueOrDefault(name) + 1;
            }
            return new { status = "ok", opcodes = counts };
        }
        var signatures = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var handlers = new List<object>();
        foreach (var bind in program.Bindings.Entries)
        {
            if (bind.Kind is not (GesBinaryBindKind.MessageHandler or GesBinaryBindKind.MessageNameHandler)) continue;
            var name = program.StringConstants.Resolve(bind.Name);
            var parameters = bind.ArgumentNames.Select(argument => program.StringConstants.Resolve(argument)).ToArray();
            var signature = GesMessageSignature.CreateSignatureId(name, parameters);
            if (!signatures.TryGetValue(name, out var values)) signatures[name] = values = new List<string>();
            values.Add(signature);
            handlers.Add(new { name, signatureId = signature, requiredRegisterCount = bind.RequiredRegisterCount, requiredCallStackDepth = bind.RequiredCallStackDepth });
        }
        return new
        {
            status = "ok",
            metadata = new
            {
                messageDefinitions = signatures.Select(pair => new { name = pair.Key, count = pair.Value.Count, signatureIds = pair.Value }).ToArray(),
                programResources = new { requiredRegisterCount = program.RequiredRegisterCount, requiredCallStackDepth = program.RequiredCallStackDepth },
                handlerResources = handlers
            }
        };
    }
}
