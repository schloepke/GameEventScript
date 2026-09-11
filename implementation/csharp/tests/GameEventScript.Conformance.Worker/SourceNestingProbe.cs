// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using GameEventScript.Api;

namespace GameEventScript.Conformance.Worker;

internal static class SourceNestingProbe
{
    internal static int Run(string mode, int stackBytes)
    {
        if (mode is not ("deep" or "boundary")) return 64;
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try { CompileExamples(mode == "deep"); }
            catch (Exception exception) { failure = exception; }
        }, stackBytes);
        thread.Start();
        thread.Join();
        if (failure is not null)
        {
            Console.Error.WriteLine(failure);
            return 70;
        }
        Console.Write("completed:" + mode);
        return 0;
    }

    private static void CompileExamples(bool deep)
    {
        var count = deep ? 5000 : 31;
        var expressions = new[]
        {
            new string('(', count) + "1" + new string(')', count),
            string.Concat(Enumerable.Repeat("not ", count)) + "true",
            string.Join(" ^ ", Enumerable.Repeat("1", count + 1)),
            string.Join(" -> ", Enumerable.Repeat("true", count + 1)),
            string.Join(" + ", Enumerable.Repeat("1", count + 1)),
            "input" + string.Concat(Enumerable.Repeat(".value", count)),
            string.Concat(Enumerable.Repeat("[", count)) + "1" + string.Concat(Enumerable.Repeat("]", count)),
            string.Concat(Enumerable.Repeat("[value: ", count)) + "1" + string.Concat(Enumerable.Repeat("]", count))
        };
        foreach (var expression in expressions) Compile("on Start(input) { let value be " + expression + "\n emit Done() }", deep);
        count = deep ? 5000 : 32;
        Compile("on Start { " + string.Concat(Enumerable.Repeat("if true { ", count)) + "emit Done()" + new string('}', count) + " }", deep);
        Compile("on Start { " + string.Concat(Enumerable.Repeat("if true ", count)) + "emit Done() }", deep);
        var pattern = string.Concat(Enumerable.Repeat("[value: ", deep ? 5000 : 30)) + "1" + new string(']', deep ? 5000 : 30);
        Compile("on Start(input) { let value be input[:has " + pattern + "]\n emit Done() }", deep);
        if (!deep)
            Compile("on Start { " + string.Concat(Enumerable.Repeat("if true { ", 32)) + "let value be " + expressions[0] + "\n emit Done()" + new string('}', 32) + " }", false);
        // A failure must not poison subsequent compilation on the same worker.
        Compile("on Start { emit Done() }", false);
    }

    private static void Compile(string source, bool reject)
    {
        try
        {
            GameEventScriptBuilder.Create().AddScript(source, "nesting.ges").Compile();
        }
        catch (GameEventScriptCompileException exception) when (reject)
        {
            var diagnostic = exception.Diagnostics.Single();
            if (diagnostic.Phase != GameEventScriptDiagnosticPhase.Parse || diagnostic.Code != GameEventScriptDiagnosticCodes.ParseSourceNestingExceeded || diagnostic.SourceLocation?.SourceName != "nesting.ges")
                throw new InvalidOperationException("Expected a structured source-nesting diagnostic.", exception);
            return;
        }
        if (reject) throw new InvalidOperationException("Excessive source nesting was accepted.");
    }
}
