// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using System.Linq;
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
                var message = NormalizeAssemblyOperand(ParsePublishMessageExpression());
                var operands = new List<ExpressionNode> { message };
                operands.AddRange(ParseOptionalTagExpressions().Select(NormalizeAssemblyOperand));
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
        if (message && Is(Message)) return NormalizeAssemblyOperand(ParseMessageLiteralExpressionCore());
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
        return NormalizeAssemblyOperand(ParseExpression());
    }

    private ExpressionNode NormalizeAssemblyOperand(ExpressionNode operand)
    {
        ArgumentListNode Arguments(ArgumentListNode arguments)
            => WithRange(new ArgumentListNode(arguments.Arguments.Select(argument => argument with { Expression = NormalizeAssemblyOperand(argument.Expression) }).ToArray()), arguments);

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
            CallExpressionNode call => WithRange(new CallExpressionNode(call.Name, Arguments(call.ArgumentList)), operand),
            ExtensionCallExpressionNode call => WithRange(new ExtensionCallExpressionNode(call.ExtensionName, call.FunctionName, Arguments(call.ArgumentList)), operand),
            TypeConstructorExpressionNode constructor => WithRange(new TypeConstructorExpressionNode(constructor.TypeName, Arguments(constructor.ArgumentList)), operand),
            MessageLiteralExpressionNode message => WithRange(new MessageLiteralExpressionNode(message.Message, Arguments(message.ArgumentList)), operand),
            ListLiteralExpressionNode list => WithRange(new ListLiteralExpressionNode(list.Items.Select(NormalizeAssemblyOperand).ToArray()), operand),
            MapLiteralExpressionNode map => WithRange(new MapLiteralExpressionNode(map.Entries.Select(entry => entry with { Value = NormalizeAssemblyOperand(entry.Value) }).ToArray()), operand),
            _ => operand
        };
    }
}
