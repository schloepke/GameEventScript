// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;

namespace GameEventScript.Compiler;

internal sealed record GesAssemblyExpressionNode(GesAssemblyBlockNode Block) : GesExpressionNode;
internal sealed record GesAssemblyStatementNode(GesAssemblyBlockNode Block) : GesStatementNode;
internal sealed record GesAssemblyBlockNode(string? Output, IReadOnlyList<GesAssemblyDeclaration> Declarations, IReadOnlyList<GesAssemblyLine> Lines, bool Predicate = false, bool AllowSend = false) : GesScriptNode;
internal sealed record GesAssemblyDeclaration(string Name, bool Export) : GesScriptNode;
internal sealed record GesAssemblyLine(string Name, bool IsLabel, IReadOnlyList<GesExpressionNode> Operands) : GesScriptNode;
