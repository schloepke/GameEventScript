// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using System.Linq;
using static GameEventScript.Compiler.GesTokenKind;

namespace GameEventScript.Compiler;

internal sealed partial class GesParser
{
    private GesExpressionNode ParseAssemblyOrExpression(string output, bool predicate = false, bool allowSend = false)
    {
        if (!MatchWord("asm")) return ParseExpression();
        var block = ParseAssemblyBlock(output) with { Predicate = predicate, AllowSend = allowSend };
        return WithRange(new GesAssemblyExpressionNode(block), block);
    }

    private GesAssemblyBlockNode ParseAssemblyBlock(string? output)
    {
        var start = Previous;
        SkipNewLines();
        Expect(LeftBrace);
        SkipStatementSeparators();
        var declarations = new List<GesAssemblyDeclaration>();
        var lines = new List<GesAssemblyLine>();
        while (!Is(RightBrace))
        {
            var token = Current;
            var exported = Match(Let);
            if (exported || Match(Dot))
            {
                if (!exported) ExpectWord("register");
                if (lines.Count != 0 || (exported && output is not null))
                    throw new GameEventScriptParseException("Assembly declarations must precede instructions; only standalone blocks export bindings.", token);
                do declarations.Add(WithRange(new GesAssemblyDeclaration(ExpectIdentifier(), exported), token)); while (Match(Comma));
            }
            else if (Match(Emit) || Match(Publish))
            {
                var kind = Previous.Kind == Emit ? GesPublishStatementKind.Emit : GesPublishStatementKind.Publish;
                var message = NormalizeAssemblyOperand(ParsePublishMessageExpression());
                var operands = new List<GesExpressionNode> { message };
                operands.AddRange(ParseOptionalTagExpressions().Select(NormalizeAssemblyOperand));
                lines.Add(WithRange(new GesAssemblyLine(kind == GesPublishStatementKind.Emit ? "emit" : "publish", false, operands), token));
            }
            else if (Is(Identifier))
            {
                var name = Advance().Text;
                Expect(Colon);
                lines.Add(WithRange(new GesAssemblyLine(name, true, []), token));
            }
            else
            {
                var name = Expect(Message).Text;
                var operands = new List<GesExpressionNode>();
                if (!Is(NewLine) && !Is(Semicolon) && !Is(RightBrace))
                {
                    do operands.Add(ParseAssemblyOperand(AssemblyMessageOperand(name, operands.Count))); while (Match(Comma));
                }
                lines.Add(WithRange(new GesAssemblyLine(name, false, operands), token));
            }
            RequireStatementSeparatorOrClosing(RightBrace);
            SkipStatementSeparators();
        }
        Expect(RightBrace);
        return WithRange(new GesAssemblyBlockNode(output, declarations, lines, AllowSend: output is null), start);
    }

    private static bool AssemblyMessageOperand(string opcode, int index)
        => opcode switch
        {
            "LoadMessage" or "EmitInstant" or "PublishInstant" => index == 1,
            "EmitAfter" or "PublishAfter" => index == 2,
            "EmitMessage" or "EmitMessageWithTags" or "PublishMessage" or "PublishMessageWithTags" => index == 0,
            _ => false
        };

    private GesExpressionNode ParseAssemblyOperand(bool message)
    {
        if (message && Is(Message)) return NormalizeAssemblyOperand(ParseMessageLiteralExpressionCore());
        if (Is(OperatorMinus) && _reader.Peek(1).Kind == Float && _reader.Peek(1).Text == "9223372036854775808")
        {
            var start = Advance();
            Advance();
            return WithRange(new GesIntegerLiteralExpressionNode(long.MinValue), start);
        }
        if (Is(TypeName) && _reader.Peek(1).Kind != LeftParen)
        {
            var token = Advance();
            return WithRange(new GesTextLiteralExpressionNode(token.Text.TrimStart(':')), token);
        }
        // Reuse literal decoding and its exact Int64/units rules; reject compound
        // expressions during assembly validation, before lowering any instruction.
        return NormalizeAssemblyOperand(ParseExpression());
    }

    private GesExpressionNode NormalizeAssemblyOperand(GesExpressionNode operand)
    {
        GesArgumentListNode Arguments(GesArgumentListNode arguments)
            => WithRange(new GesArgumentListNode(arguments.Arguments.Select(argument => argument with { Expression = NormalizeAssemblyOperand(argument.Expression) }).ToArray()), arguments);

        return operand switch
        {
            GesUnaryExpressionNode { Operator: GesUnaryOperator.Negate, Operand: GesIntegerLiteralExpressionNode integer } when integer.Value != long.MinValue => WithRange(new GesIntegerLiteralExpressionNode(-integer.Value), operand),
            GesUnaryExpressionNode { Operator: GesUnaryOperator.Negate, Operand: GesFloatLiteralExpressionNode number } => WithRange(new GesFloatLiteralExpressionNode(-number.Value), operand),
            GesUnaryExpressionNode { Operator: GesUnaryOperator.Negate, Operand: GesUnitIntegerLiteralExpressionNode integer } when integer.Value != long.MinValue
                => WithRange(new GesUnitIntegerLiteralExpressionNode(-integer.Value, integer.UnitName), operand),
            GesUnaryExpressionNode { Operator: GesUnaryOperator.Negate, Operand: GesUnitFloatLiteralExpressionNode number }
                => WithRange(new GesUnitFloatLiteralExpressionNode(-number.Value, number.UnitName), operand),
            GesUnaryExpressionNode { Operator: GesUnaryOperator.Negate, Operand: GesPercentageLiteralExpressionNode percentage }
                => WithRange(new GesPercentageLiteralExpressionNode(-percentage.RatioValue), operand),
            GesCallExpressionNode call => WithRange(new GesCallExpressionNode(call.Name, Arguments(call.ArgumentList)), operand),
            GesExtensionCallExpressionNode call => WithRange(new GesExtensionCallExpressionNode(call.ExtensionName, call.FunctionName, Arguments(call.ArgumentList)), operand),
            GesTypeConstructorExpressionNode constructor => WithRange(new GesTypeConstructorExpressionNode(constructor.TypeName, Arguments(constructor.ArgumentList)), operand),
            GesMessageLiteralExpressionNode message => WithRange(new GesMessageLiteralExpressionNode(message.Message, Arguments(message.ArgumentList)), operand),
            GesListLiteralExpressionNode list => WithRange(new GesListLiteralExpressionNode(list.Items.Select(NormalizeAssemblyOperand).ToArray()), operand),
            GesMapLiteralExpressionNode map => WithRange(new GesMapLiteralExpressionNode(map.Entries.Select(entry => entry with { Value = NormalizeAssemblyOperand(entry.Value) }).ToArray()), operand),
            _ => operand
        };
    }
}
