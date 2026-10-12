// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

namespace GameEventScript.Compiler.Worker;

internal static class Program
{
    private static int Main(string[] args)
    {
        if (args.Length == 2 && args[0] == "--source-nesting") return SourceNestingProbe.Run(args[1], 512 * 1024);
        if (args.Length == 2 && args[0] == "--probe") return Probe(args[1]);
        return 64;
    }

    private static int Probe(string mode)
    {
        switch (mode)
        {
            case "exit":
                Console.Write("{\"Status\":\"Passed\"}");
                return 23;
            case "hang":
                Thread.Sleep(Timeout.Infinite);
                return 0;
            case "stdout":
            case "stderr":
                var output = mode == "stdout" ? Console.Out : Console.Error;
                var chunk = new string('x', 4096);
                while (true) output.Write(chunk);
            case "missing":
                return 0;
            default:
                return 64;
        }
    }
}
