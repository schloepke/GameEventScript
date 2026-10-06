// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using static GameEventScript.Compiler.GesTokenKind;

namespace GameEventScript.Compiler;

internal sealed partial class GesParser
{
    private ExpressionNode ParseAssemblyOrExpression(string output, bool predicate = false, bool allowSend = false)
    {
        if (!MatchWord("asm")) return ParseExpression();
        var block = ParseAssemblyBlock(output) with { Predicate = predicate, AllowSend = allowSend };
        return WithRange(new AssemblyExpressionNode(block), block);
    }

    private AssemblyBlockNode ParseAssemblyBlock(string? output)
    {
        var start = Previous;
        SkipNewLines();
        Expect(LeftBrace);
        SkipStatementSeparators();
        var declarations = new List<AssemblyDeclaration>();
        var lines = new List<AssemblyLine>();
        while (!Is(RightBrace))
        {
            var token = Current;
            var exported = Match(Let);
            if (exported || Match(Dot))
            {
                if (!exported) ExpectWord("register");
                if (lines.Count != 0 || (exported && output is not null))
                    throw new GameEventScriptParseException("Assembly declarations must precede instructions; only standalone blocks export bindings.", token);
                do declarations.Add(WithRange(new AssemblyDeclaration(ExpectIdentifier(), exported), token)); while (Match(Comma));
            }
            else if (Match(Emit) || Match(Publish))
            {
                var kind = Previous.Kind == Emit ? PublishStatementKind.Emit : PublishStatementKind.Publish;
                var message = ParsePublishMessageExpression();
                var operands = new List<ExpressionNode> { message };
                operands.AddRange(ParseOptionalTagExpressions());
                lines.Add(WithRange(new AssemblyLine(kind == PublishStatementKind.Emit ? "emit" : "publish", false, operands), token));
            }
            else if (Is(Identifier))
            {
                var name = Advance().Text;
                Expect(Colon);
                lines.Add(WithRange(new AssemblyLine(name, true, []), token));
            }
            else
            {
                var name = Expect(Message).Text;
                var operands = new List<ExpressionNode>();
                if (!Is(NewLine) && !Is(Semicolon) && !Is(RightBrace))
                {
                    do operands.Add(ParseAssemblyOperand(AssemblyMessageOperand(name, operands.Count))); while (Match(Comma));
                }
                lines.Add(WithRange(new AssemblyLine(name, false, operands), token));
            }
            RequireStatementSeparatorOrClosing(RightBrace);
            SkipStatementSeparators();
        }
        Expect(RightBrace);
        return WithRange(new AssemblyBlockNode(output, declarations, lines, AllowSend: output is null), start);
    }

    private static bool AssemblyMessageOperand(string opcode, int index)
        => opcode switch
        {
            "LoadMessage" or "EmitInstant" or "PublishInstant" => index == 1,
            "EmitAfter" or "PublishAfter" => index == 2,
            "EmitMessage" or "EmitMessageWithTags" or "PublishMessage" or "PublishMessageWithTags" => index == 0,
            _ => false
        };

    private ExpressionNode ParseAssemblyOperand(bool message)
    {
        if (message && Is(Message)) return ParseMessageLiteralExpressionCore();
        if (Is(OperatorMinus) && _reader.Peek(1).Kind == Float && _reader.Peek(1).Text == "9223372036854775808")
        {
            var start = Advance();
            Advance();
            return WithRange(new IntegerLiteralExpressionNode(long.MinValue), start);
        }
        if (Is(TypeName) && _reader.Peek(1).Kind != LeftParen)
        {
            var token = Advance();
            return WithRange(new TextLiteralExpressionNode(token.Text.TrimStart(':')), token);
        }
        // Reuse literal decoding and its exact Int64/units rules; reject compound
        // expressions during assembly validation, before lowering any instruction.
        var operand = ParseExpression();
        return operand switch
        {
            UnaryExpressionNode { Operator: GesUnaryOperator.Negate, Operand: IntegerLiteralExpressionNode integer } when integer.Value != long.MinValue => WithRange(new IntegerLiteralExpressionNode(-integer.Value), operand),
            UnaryExpressionNode { Operator: GesUnaryOperator.Negate, Operand: FloatLiteralExpressionNode number } => WithRange(new FloatLiteralExpressionNode(-number.Value), operand),
            UnaryExpressionNode { Operator: GesUnaryOperator.Negate, Operand: UnitIntegerLiteralExpressionNode integer } when integer.Value != long.MinValue
                => WithRange(new UnitIntegerLiteralExpressionNode(-integer.Value, integer.UnitName), operand),
            UnaryExpressionNode { Operator: GesUnaryOperator.Negate, Operand: UnitFloatLiteralExpressionNode number }
                => WithRange(new UnitFloatLiteralExpressionNode(-number.Value, number.UnitName), operand),
            UnaryExpressionNode { Operator: GesUnaryOperator.Negate, Operand: PercentageLiteralExpressionNode percentage }
                => WithRange(new PercentageLiteralExpressionNode(-percentage.RatioValue), operand),
            _ => operand
        };
    }
}
