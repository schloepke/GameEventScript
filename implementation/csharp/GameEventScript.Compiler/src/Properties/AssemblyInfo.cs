// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("GameEventScript.Compiler.Tests")]
[assembly: InternalsVisibleTo("GameEventScript.Conformance.Tests")]

// Internal measurement harness needs separate AST and bytecode compilation scopes.
[assembly: InternalsVisibleTo("GameEventScript.ConformanceAdapter")]
