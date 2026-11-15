// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System.Text;
using GameEventScript.Api;

namespace GameEventScript.Compiler.Worker;

// Compiles a valid acyclic chain of functions whose result types the validator derives through every call, for a
// predicate result, a local binding and a send delay, on a bounded native stack.
internal static class CallChainProbe
{
    private const int ChainLength = 5000;

    internal static int Run(int stackBytes)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try { new GesProgramBuilder().AddScript(Source(), "chain.ges").Compile(); }
            catch (Exception exception) { failure = exception; }
        }, stackBytes);
        thread.Start();
        thread.Join();
        if (failure is not null)
        {
            Console.Error.WriteLine(failure);
            return 70;
        }

        Console.Write("completed:call-chain");
        return 0;
    }

    private static string Source()
    {
        var source = new StringBuilder("function c0() be true\nfunction d0() be 1s\n");
        for (var i = 1; i < ChainLength; i++)
            source.Append("function c").Append(i).Append("() be c").Append(i - 1).Append("()\nfunction d").Append(i).Append("() be d").Append(i - 1).Append("()\n");
        var last = ChainLength - 1;
        source.Append("predicate check() be c").Append(last).Append("()\n");
        source.Append("on Start {\n  let value be c").Append(last).Append("()\n  emit after d").Append(last).Append("() Done(value: value, check: check())\n}\n");
        return source.ToString();
    }
}
