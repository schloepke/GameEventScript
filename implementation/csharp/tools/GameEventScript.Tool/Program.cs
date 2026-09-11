// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System.Reflection;

namespace GameEventScript.Tool;

internal static class Program
{
    private const string HelpText = """
Game Event Script CLI

Usage:
  ges [--help | --version]
  ges compile <source.ges> [more.ges ...] [-o <output.gesb>] [--no-debug] [-v | -q]
  ges dump <program.gesb> [-o <output.gesa>] [--addresses]

Commands:
  compile     Compile UTF-8 source files together to .gesb. Use 'ges compile --help' for options.
  dump        Dump a .gesb file as GESA text. Use 'ges dump --help' for options.

Options:
  -h, --help  Show this help.
  --version   Show the tool version.
""";

    private static int Main(string[] arguments)
    {
        if (arguments is ["compile", ..]) return CompileCommand.Run(arguments[1..]);
        if (arguments is ["dump", ..]) return DumpCommand.Run(arguments[1..]);

        if (arguments.Length == 0 || arguments is ["--help"] or ["-h"])
        {
            Console.WriteLine(HelpText);
            return 0;
        }

        if (arguments is ["--version"])
        {
            var version = typeof(Program).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion;
            Console.WriteLine("ges " + version);
            return 0;
        }

        Console.Error.WriteLine("Unknown command or arguments. Run 'ges --help' for usage.");
        return 2;
    }
}
