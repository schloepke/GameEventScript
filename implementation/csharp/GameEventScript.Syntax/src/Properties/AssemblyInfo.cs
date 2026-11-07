// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("GameEventScript.Runtime")]
[assembly: InternalsVisibleTo("GameEventScript.Compiler")]
[assembly: InternalsVisibleTo("GameEventScript.CSharpBridge")]
// The CLI explains document error codes like the highlighter.
[assembly: InternalsVisibleTo("GameEventScript.Tool")]
[assembly: InternalsVisibleTo("GameEventScript.Syntax.Tests")]
[assembly: InternalsVisibleTo("GameEventScript.Runtime.Tests")]
[assembly: InternalsVisibleTo("GameEventScript.Compiler.Tests")]
[assembly: InternalsVisibleTo("GameEventScript.CSharpBridge.Tests")]
[assembly: InternalsVisibleTo("GameEventScript.ConformanceAdapter")]
