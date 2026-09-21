// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System.Reflection;

namespace GameEventScript.Tool;

internal static class Program
{
    private const string HelpText = """
Game Event Script CLI

Usage:
  dotnet ges [--help | --version]
  dotnet ges compile <source.ges> [more.ges ...] [-o <output.gesb>] [--no-debug] [-v | -q]
  dotnet ges check <source.ges> [more.ges ...] [-v | -q]
  dotnet ges run <source.ges> [more.ges ...] [--scenario <scenario.ges>] [--seed <integer>]
  dotnet ges run <program.gesb> [more.gesb ...] [--args <text> ...] [--seed <integer>]
  dotnet ges run <program files ...> [--color] -- <text> ...
  dotnet ges run --interactive [--color] [program files ...]
  dotnet ges dump <program.gesb> [-o <output.gesa>] [--addresses]

Commands:
  compile     Compile UTF-8 source files together to .gesb. Use 'dotnet ges compile --help' for options.
  check       Validate source files without writing a binary. Use 'dotnet ges check --help' for options.
  run         Run Main(args), an event scenario, or the event console. Use 'dotnet ges run --help' for options.
  dump        Dump a .gesb file as GESA text. Use 'dotnet ges dump --help' for options.

Options:
  -h, --help  Show this help.
  --version   Show the tool version.
""";

    private static int Main(string[] arguments)
    {
        if (arguments is ["compile", ..]) return CompileCommand.Run(arguments[1..]);
        if (arguments is ["check", ..]) return CompileCommand.Run(arguments[1..], checkOnly: true);
        if (arguments is ["run", ..]) return RunCommand.Run(arguments[1..]);
        if (arguments is ["dump", ..]) return DumpCommand.Run(arguments[1..]);

        if (arguments.Length == 0 || arguments is ["--help"] or ["-h"])
        {
            Console.WriteLine(HelpText);
            return 0;
        }

        if (arguments is ["--version"])
        {
            var version = typeof(Program).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion;
            Console.WriteLine("dotnet ges " + version);
            return 0;
        }

        Console.Error.WriteLine("Unknown command or arguments. Run 'dotnet ges --help' for usage.");
        return 2;
    }
}
