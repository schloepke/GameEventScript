#!/usr/bin/env python3
# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0

"""Regression controls for scripts/verify-port-names.py using disposable source trees."""

import importlib.util
from pathlib import Path
import sys
import tempfile
import unittest

sys.dont_write_bytecode = True

spec = importlib.util.spec_from_file_location("port_names", Path(__file__).with_name("verify-port-names.py"))
port_names = importlib.util.module_from_spec(spec)
spec.loader.exec_module(port_names)

OPCODES = """namespace GameEventScript.Api;
public enum GesBytecodeOpCode : byte
{
    // A comment with Add = 9 must not count.
    Add = 0x01,
    IntegerDivide = 0x02,
    LogN = 0x03,
}
"""


class Workspace:
    """A minimal repository layout with all four module roots."""

    def __init__(self):
        self._directory = tempfile.TemporaryDirectory()
        self.root = Path(self._directory.name)
        for _, csharp, swift in port_names.MODULES:
            (self.root / csharp).mkdir(parents=True)
            (self.root / swift).mkdir(parents=True)
        self.write("implementation/csharp/GameEventScript.Runtime/src/Api/OpCodes.cs", OPCODES)
        self.write("implementation/swift/GameEventScriptRuntime/Sources/GameEventScriptRuntime/OpCodes.swift",
                   "public enum GesBytecodeOpCode: UInt8 { case add = 1 }\n")

    def write(self, relative, text):
        path = self.root / relative
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(text, encoding="utf-8")

    def csharp(self, module, relative, text):
        root = dict((m, c) for m, c, _ in port_names.MODULES)[module]
        self.write(f"{root}/{relative}", text)

    def swift(self, module, relative, text):
        root = dict((m, s) for m, _, s in port_names.MODULES)[module]
        self.write(f"{root}/Target/{relative}", text)

    def findings(self):
        return port_names.collect(self.root)

    def close(self):
        self._directory.cleanup()


class PortNameTests(unittest.TestCase):
    def setUp(self):
        self.workspace = Workspace()
        self.addCleanup(self.workspace.close)

    def test_matching_ports_have_no_findings(self):
        self.workspace.csharp("Runtime", "Host.cs", "namespace GameEventScript.Api;\n\npublic sealed class GameEventScriptHost\n{\n    private sealed class Nested { }\n}\n")
        self.workspace.swift("Runtime", "Host.swift", "public final class GameEventScriptHost {\n    struct Nested {}\n}\n")
        self.assertEqual(set(), self.workspace.findings())

    def test_nested_private_and_extension_declarations_are_ignored(self):
        self.workspace.csharp("Runtime", "Value.cs", "namespace GameEventScript.Runtime;\n\ninternal struct GesValue\n{\n    internal enum Kind { A }\n}\n")
        self.workspace.swift("Runtime", "Value.swift", "struct GesValue {}\nprivate struct Helper {}\nfileprivate enum Other {}\nextension GesValue { enum Kind {} }\n")
        self.assertEqual(set(), self.workspace.findings())

    def test_block_namespace_types_are_top_level(self):
        self.workspace.csharp("Runtime", "Value.cs", "namespace GameEventScript.Runtime\n{\n    internal sealed class Helper { }\n}\n")
        findings = self.workspace.findings()
        self.assertIn(("prefix", "csharp", "Runtime", "Helper"), findings)
        self.assertIn(("only", "csharp", "Runtime", "Helper"), findings)

    def test_comments_do_not_declare_types(self):
        self.workspace.csharp("Runtime", "Doc.cs", "/*\nclass Hidden\n*/\n// class AlsoHidden\n")
        self.workspace.swift("Runtime", "Doc.swift", "/// struct Hidden\n/* class AlsoHidden */\n")
        self.assertEqual(set(), self.workspace.findings())

    def test_idiomatic_mappings_compare_equal(self):
        self.workspace.csharp("Runtime", "Api.cs", "namespace GameEventScript.Api;\n\n"
                              "public interface IGesClock { }\n"
                              "public sealed class GesCompileException : System.Exception { }\n"
                              "public sealed class GesTypeAttribute : System.Attribute { }\n")
        self.workspace.swift("Runtime", "Api.swift", "public protocol GesClock {}\n"
                             "public struct GesCompileError: Error {}\n"
                             "@attached(member) public macro GesType() = #externalMacro(module: \"M\", type: \"T\")\n")
        self.assertEqual(set(), self.workspace.findings())

    def test_single_port_type_is_reported_unless_platform_only(self):
        self.workspace.csharp("Runtime", "Only.cs", "namespace GameEventScript.Runtime;\n\ninternal sealed class GesOnlyCSharp { }\ninternal sealed class IsExternalInit { }\n")
        self.workspace.csharp("Compiler", "Fix.cs", "namespace System.Runtime.CompilerServices;\n\ninternal static class IsExternalInit { }\n")
        findings = self.workspace.findings()
        self.assertIn(("only", "csharp", "Runtime", "GesOnlyCSharp"), findings)
        self.assertNotIn(("only", "csharp", "Runtime", "IsExternalInit"), findings)
        self.assertNotIn(("prefix", "csharp", "Compiler", "IsExternalInit"), findings)

    def test_module_placement_is_part_of_parity(self):
        self.workspace.csharp("Runtime", "Options.cs", "namespace GameEventScript.Api;\n\npublic enum GesDebugInfoOptions { }\n")
        self.workspace.swift("Compiler", "Options.swift", "public struct GesDebugInfoOptions {}\n")
        findings = self.workspace.findings()
        self.assertIn(("only", "csharp", "Runtime", "GesDebugInfoOptions"), findings)
        self.assertIn(("only", "swift", "Compiler", "GesDebugInfoOptions"), findings)

    def test_prefix_rule(self):
        for name, expected in (("GameEventScriptHost", False), ("GameEventScriptProgram", False),
                               ("GameEventScriptMessage", False), ("GameEventScriptContext", True),
                               ("GesValue", False), ("IGesClock", False), ("IGameEventScriptClock", True),
                               ("Gesture", True), ("Ges", False), ("Helper", True), ("TextNumberCast", True)):
            with self.subTest(name=name):
                self.assertEqual(expected, port_names.prefix_violation(name))

    def test_abbreviation_rule(self):
        for name, expected in (("GesApiError", None), ("GameEventScriptAPIError", "API"), ("GesUInt16IndexList", None),
                               ("GesCSharpValue", None), ("IGesClock", None), ("VM", "VM"), ("GesVmState", None),
                               ("GesJSONCodec", "JSON"), ("GesSha256", None)):
            with self.subTest(name=name):
                self.assertEqual(expected, port_names.abbreviation(name))

    def test_abbreviated_folder_and_namespace_are_reported(self):
        self.workspace.csharp("Runtime", "Runtime/VM/GesVirtualMachine.cs", "namespace GameEventScript.Runtime.VM;\n\ninternal sealed class GesVirtualMachine { }\n")
        self.workspace.swift("Runtime", "Runtime/VM/VirtualMachine.swift", "final class GesVirtualMachine {}\n")
        findings = self.workspace.findings()
        self.assertIn(("abbreviation", "csharp", "Runtime", "folder VM"), findings)
        self.assertIn(("abbreviation", "csharp", "Runtime", "namespace GameEventScript.Runtime.VM"), findings)
        self.assertIn(("abbreviation", "swift", "Runtime", "folder VM"), findings)

    def test_csharp_handlers(self):
        self.workspace.csharp("Runtime", "Vm/Handlers.cs", "namespace GameEventScript.Runtime.Vm;\n\n"
                              "internal partial class GesVirtualMachine\n{\n"
                              "    private void ExecuteAdd(ushort d) { }\n"
                              "    private void ExecuteAddGeneral(ushort d) { }\n"
                              "    private void ExecuteIntegerDivide(ushort d) { }\n"
                              "    internal void ExecuteLogN(ushort d) { }\n"
                              "    private void ExecuteFloorDivide(ushort d) { }\n"
                              "    private void ExecuteAddition(ushort d) { }\n"
                              "}\n"
                              "internal static class GesVmRegisterMath\n{\n"
                              "    internal static void AddRegisters(this GesVirtualMachine vm, ushort d) { }\n"
                              "}\n")
        handlers = {f for f in self.workspace.findings() if f[0] == "handler"}
        self.assertEqual({
            ("handler", "csharp", "Runtime", "ExecuteLogN (not private)"),
            ("handler", "csharp", "Runtime", "ExecuteFloorDivide"),
            ("handler", "csharp", "Runtime", "ExecuteAddition"),
            ("handler", "csharp", "Runtime", "AddRegisters (extension method)"),
        }, handlers)

    def test_swift_handlers_and_call_sites(self):
        base = "implementation/swift/GameEventScriptRuntime/Sources/GameEventScriptRuntime"
        self.workspace.write(f"{base}/Runtime/Vm/Opcodes/Math/Add.swift",
                             "extension GesVirtualMachine {\n    func executeAdd(_ d: Int) { executeAddGeneral(d) }\n    func executeAddGeneral(_ d: Int) {}\n}\n")
        self.workspace.write(f"{base}/Runtime/Vm/VirtualMachineDispatch.swift",
                             "extension GesVirtualMachine {\n    func run() { executeAdd(1); executeLogN(2) }\n    func executeLogN(_ d: Int) {}\n    func executeFrame() {}\n}\n")
        self.workspace.write(f"{base}/Api/Host.swift", "func pump(vm: GesVirtualMachine) { vm.executeAdd(1) }\n")
        findings = {f for f in self.workspace.findings() if f[0].startswith("handler")}
        self.assertEqual({
            ("handler", "swift", "Runtime", "executeFrame"),
            ("handler-call", "swift", "Runtime", "executeAdd in Host.swift"),
        }, findings)

    def test_baseline_is_a_ratchet(self):
        self.workspace.csharp("Runtime", "Only.cs", "namespace GameEventScript.Runtime;\n\ninternal sealed class GesOnlyCSharp { }\n")
        baseline = self.workspace.root / "baseline.txt"
        port_names.write_baseline(baseline, self.workspace.findings())
        self.assertEqual(([], []), port_names.verify(self.workspace.root, baseline))

        self.workspace.swift("Runtime", "Extra.swift", "struct Helper {}\n")
        new, stale = port_names.verify(self.workspace.root, baseline)
        self.assertIn(("prefix", "swift", "Runtime", "Helper"), new)
        self.assertEqual([], stale)

        (self.workspace.root / "implementation/swift/GameEventScriptRuntime/Sources/Target/Extra.swift").unlink()
        self.workspace.swift("Runtime", "Only.swift", "struct GesOnlyCSharp {}\n")
        new, stale = port_names.verify(self.workspace.root, baseline)
        self.assertEqual([], new)
        self.assertEqual([("only", "csharp", "Runtime", "GesOnlyCSharp")], stale)

    def test_command_line_reports_failure(self):
        self.workspace.swift("Runtime", "Extra.swift", "struct Helper {}\n")
        baseline = self.workspace.root / "baseline.txt"
        port_names.write_baseline(baseline, set())
        self.assertEqual(1, port_names.main(["--root", str(self.workspace.root), "--baseline", str(baseline)]))
        port_names.main(["--root", str(self.workspace.root), "--baseline", str(baseline), "--update-baseline"])
        self.assertEqual(0, port_names.main(["--root", str(self.workspace.root), "--baseline", str(baseline)]))

    def test_malformed_baseline_is_rejected(self):
        baseline = self.workspace.root / "baseline.txt"
        baseline.write_text("only\tcsharp\tRuntime\n", encoding="utf-8")
        with self.assertRaises(RuntimeError):
            port_names.read_baseline(baseline)

    def test_repository_baseline_matches_sources(self):
        self.assertEqual(([], []), port_names.verify(port_names.ROOT, port_names.BASELINE))


if __name__ == "__main__":
    unittest.main()
