// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;

namespace GameEventScript.Compiler;

internal sealed record AssemblyExpressionNode(AssemblyBlockNode Block) : ExpressionNode;
internal sealed record AssemblyStatementNode(AssemblyBlockNode Block) : StatementNode;
internal sealed record AssemblyBlockNode(string? Output, IReadOnlyList<AssemblyDeclaration> Declarations, IReadOnlyList<AssemblyLine> Lines, bool Predicate = false, bool AllowSend = false) : ScriptNode;
internal sealed record AssemblyDeclaration(string Name, bool Export) : ScriptNode;
internal sealed record AssemblyLine(string Name, bool IsLabel, IReadOnlyList<ExpressionNode> Operands) : ScriptNode;
