#!/usr/bin/env python3
# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0
"""Build and measure native C# collection opcodes against explicit ASM loops."""
import argparse
import os
from pathlib import Path
import subprocess
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--prepare-only', action='store_true', help='Build the harness without running timings.')
    args = parser.parse_args()
    output = ROOT / 'artifacts' / 'inline-assembly-benchmark'
    output.mkdir(parents=True, exist_ok=True)
    project = ET.Element('Project', Sdk='Microsoft.NET.Sdk')
    properties = ET.SubElement(project, 'PropertyGroup')
    for name, value in {'OutputType': 'Exe', 'TargetFramework': 'net8.0', 'ImplicitUsings': 'enable',
                        'Nullable': 'enable', 'EnableDefaultCompileItems': 'false',
                        'AssemblyName': 'InlineAssemblyBenchmark'}.items():
        ET.SubElement(properties, name).text = value
    items = ET.SubElement(project, 'ItemGroup')
    ET.SubElement(items, 'Compile', Include=str(ROOT / 'scripts' / 'inline-assembly-benchmark.cs'))
    for module in ['Runtime', 'Compiler', 'CSharpBridge']:
        name = 'GameEventScript.' + module
        path = ROOT / 'implementation' / 'csharp' / name / 'src' / (name + '.csproj')
        ET.SubElement(items, 'ProjectReference', Include=str(path))
    project_path = output / 'InlineAssemblyBenchmark.csproj'
    ET.indent(project)
    ET.ElementTree(project).write(project_path, encoding='unicode')
    subprocess.run(['dotnet', 'build', str(project_path), '--configuration', 'Release',
                    '--artifacts-path', str(output / 'build')], cwd=ROOT, check=True)
    if args.prepare_only:
        return
    dlls = list((output / 'build' / 'bin').glob('*/release/InlineAssemblyBenchmark.dll'))
    if len(dlls) != 1:
        raise RuntimeError(f'Expected one harness executable, found {len(dlls)}')
    environment = dict(os.environ, DOTNET_TieredCompilation='0')
    subprocess.run(['dotnet', str(dlls[0]), str(ROOT), str(output / 'results.json')],
                   cwd=ROOT, env=environment, check=True)


if __name__ == '__main__':
    main()
