// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using GameEventScript.Api;

namespace GameEventScript.Compiler;

internal static partial class GesCompiler
{
    private sealed partial class BinaryCompiler
    {
        private string? AssemblySymbolicSignature(AssemblyLine line)
        {
            if (line.Name is "emit" or "publish") return new string('e', line.Operands.Count);
            if (AssemblySend(line) is { } send)
            {
                var expected = (send.Result ? 2 : 1) + (send.Delayed ? 1 : 0) + (send.Tags ? 1 : 0);
                if (line.Operands.Count != expected) throw AssemblyFailure("Incorrect symbolic send operands.", line.Name);
                return (send.Result ? "w" : "") + new string('e', expected - (send.Result ? 1 : 0));
            }
            if (line.Name is not ("Call" or "CallExternal" or "CreateList" or "CreateMap" or "CreateRecord" or "CreateExternalType" or "CreateVector" or "CreatePoint"
                or "LoadMessage" or "LoadHandler" or "BindHandler" or "ConstructData")) return null;
            if (line.Operands.Count != 2) throw AssemblyFailure("Symbolic instruction requires a destination and a symbolic value.", line.Name);
            var value = line.Operands[1];
            var valid = line.Name switch
            {
                "Call" or "BindHandler" => value is CallExpressionNode,
                "CallExternal" => value is ExtensionCallExpressionNode,
                "CreateList" => value is ListLiteralExpressionNode,
                "CreateMap" => value is MapLiteralExpressionNode,
                "LoadMessage" => value is MessageLiteralExpressionNode,
                "LoadHandler" => value is HandlerLiteralExpressionNode,
                "CreateVector" => value is TypeConstructorExpressionNode { TypeName: "vector" } vector && GetSpatialConstructorStageShape(vector.Arguments) is not null,
                "CreatePoint" => value is TypeConstructorExpressionNode { TypeName: "point" } point && GetSpatialConstructorStageShape(point.Arguments) is not null,
                "CreateRecord" => value is TypeConstructorExpressionNode record && module.TypeDefinitions.ContainsKey(record.TypeName),
                "CreateExternalType" => value is TypeConstructorExpressionNode external && !module.TypeDefinitions.ContainsKey(external.TypeName)
                    && module.ExternalTypeDefinitions.Resolve(external.TypeName) is not null,
                "ConstructData" => value is TypeConstructorExpressionNode data && IsAssemblyDataConstructor(data),
                _ => false
            };
            if (!valid) throw AssemblyFailure("Operand does not match the symbolic instruction.", line.Name);
            return "we";
        }

        private static bool IsAssemblyDataConstructor(TypeConstructorExpressionNode constructor)
            => constructor.TypeName is "record" or "series" || constructor.TypeName is "number" or "range" or "message" or "nothing" or "percentage" or "boolean" or "text" or "tag" or "list" or "map" or "dice" or "handler"
                && (constructor.Arguments.Count != 1 || constructor.Arguments[0].Label is not null);

        private static (bool Publish, bool Result, bool Delayed, bool Tags)? AssemblySend(AssemblyLine line)
        {
            var name = line.Name;
            if (name is "EmitInstant" or "EmitAfter" or "PublishInstant" or "PublishAfter")
            {
                var delayed = name.EndsWith("After", StringComparison.Ordinal);
                return (name.StartsWith("Publish", StringComparison.Ordinal), true, delayed, line.Operands.Count == (delayed ? 4 : 3));
            }
            if (name is "EmitMessage" or "EmitMessageWithTags" or "EmitMessageValue" or "EmitMessageValueWithTags" or "PublishMessage" or "PublishMessageWithTags" or "PublishMessageValue" or "PublishMessageValueWithTags")
                return (name.StartsWith("Publish", StringComparison.Ordinal), false, false, name.EndsWith("WithTags", StringComparison.Ordinal));
            return null;
        }

        private static IEnumerable<ExpressionNode> AssemblySymbolicReads(ExpressionNode expression, bool handlerTarget = false)
        {
            if (handlerTarget && expression is CallExpressionNode target) yield return new IdentifierExpressionNode(target.Name);
            IReadOnlyList<ExpressionNode> operands;
            switch (expression)
            {
                case CallExpressionNode call: operands = call.Arguments; break;
                case ExtensionCallExpressionNode call: operands = call.ArgumentList.Expressions; break;
                case TypeConstructorExpressionNode constructor: operands = constructor.ArgumentList.Expressions; break;
                case MessageLiteralExpressionNode message: operands = message.ArgumentList.Expressions; break;
                case HandlerLiteralExpressionNode: yield break;
                case ListLiteralExpressionNode list: operands = list.Items; break;
                case MapLiteralExpressionNode map:
                    var values = new List<ExpressionNode>();
                    foreach (var entry in map.Entries) values.Add(entry.Value);
                    operands = values;
                    break;
                default: operands = new[] { expression }; break;
            }
            foreach (var operand in operands)
            {
                if (operand is not IdentifierExpressionNode && !IsAssemblyScalar(operand))
                    throw AssemblyFailure("Symbolic arguments must be register names or scalar literals.");
                yield return operand;
            }
        }

        private void EmitAssemblySymbolic(AssemblyLine line, GesRegisterRef[] registers, LoweringContext context)
        {
            if (AssemblySend(line) is { } send)
            {
                var offset = send.Result ? 1 : 0;
                var delay = send.Delayed ? line.Operands[offset++] : null;
                var message = line.Operands[offset++];
                IReadOnlyList<ExpressionNode> tags = [];
                if (send.Tags) tags = line.Operands[offset] is ListLiteralExpressionNode list ? list.Items : throw AssemblyFailure("Send tags require a literal list of registers or tags.");
                if (!send.Result && (line.Name.Contains("Value", StringComparison.Ordinal) ? message is not IdentifierExpressionNode : message is not MessageLiteralExpressionNode))
                    throw AssemblyFailure("Message opcode and symbolic operand disagree.", line.Name);
                var kind = send.Publish ? PublishStatementKind.Publish : PublishStatementKind.Emit;
                if (send.Result) EmitSend(registers[0], new SendExpressionNode(kind, message, tags, delay), context, new ExpressionState(context.RegisterCount));
                else EmitPublish(new PublishStatementNode(kind, message, tags), context);
                return;
            }
            if (line.Name == "Call")
            {
                EmitCallInto((CallExpressionNode)line.Operands[1], registers[0], context, new ExpressionState(context.RegisterCount));
                return;
            }
            if (line.Name == "BindHandler")
            {
                EmitHandlerBindCallInto((CallExpressionNode)line.Operands[1], registers[0], context, new ExpressionState(context.RegisterCount));
                return;
            }
            if (line.Name is "emit" or "publish")
            {
                var tags = new List<ExpressionNode>();
                for (var index = 1; index < line.Operands.Count; index++) tags.Add(line.Operands[index]);
                EmitPublish(new PublishStatementNode(line.Name == "emit" ? PublishStatementKind.Emit : PublishStatementKind.Publish, line.Operands[0], tags), context);
                return;
            }
            EmitExpressionToRegister(line.Operands[1], registers[0], context, new ExpressionState(context.RegisterCount));
        }
    }
}
