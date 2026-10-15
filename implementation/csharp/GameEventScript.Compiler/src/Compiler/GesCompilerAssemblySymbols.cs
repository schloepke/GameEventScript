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
        private string? AssemblySymbolicSignature(GesAssemblyLine line)
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
                "Call" or "BindHandler" => value is GesCallExpressionNode,
                "CallExternal" => value is GesExtensionCallExpressionNode,
                "CreateList" => value is GesListLiteralExpressionNode,
                "CreateMap" => value is GesMapLiteralExpressionNode,
                "LoadMessage" => value is GesMessageLiteralExpressionNode,
                "LoadHandler" => value is GesHandlerLiteralExpressionNode,
                "CreateVector" => value is GesTypeConstructorExpressionNode { TypeName: "vector" } vector && GetSpatialConstructorStageShape(vector.Arguments) is not null,
                "CreatePoint" => value is GesTypeConstructorExpressionNode { TypeName: "point" } point && GetSpatialConstructorStageShape(point.Arguments) is not null,
                "CreateRecord" => value is GesTypeConstructorExpressionNode record && module.TypeDefinitions.ContainsKey(record.TypeName),
                "CreateExternalType" => value is GesTypeConstructorExpressionNode external && !module.TypeDefinitions.ContainsKey(external.TypeName)
                    && module.ExternalTypeDefinitions.Resolve(external.TypeName) is not null,
                "ConstructData" => value is GesTypeConstructorExpressionNode data && IsAssemblyDataConstructor(data),
                _ => false
            };
            if (!valid) throw AssemblyFailure("Operand does not match the symbolic instruction.", line.Name);
            return "we";
        }

        private static bool IsAssemblyDataConstructor(GesTypeConstructorExpressionNode constructor)
            => constructor.TypeName is "record" or "series" || constructor.TypeName is "number" or "range" or "message" or "nothing" or "percentage" or "boolean" or "text" or "tag" or "list" or "map" or "dice" or "handler"
                && (constructor.Arguments.Count != 1 || constructor.Arguments[0].Label is not null);

        private static (bool Publish, bool Result, bool Delayed, bool Tags)? AssemblySend(GesAssemblyLine line)
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

        private static IEnumerable<GesExpressionNode> AssemblySymbolicReads(GesExpressionNode expression, bool handlerTarget = false)
        {
            if (handlerTarget && expression is GesCallExpressionNode target) yield return new GesIdentifierExpressionNode(target.Name);
            IReadOnlyList<GesExpressionNode> operands;
            switch (expression)
            {
                case GesCallExpressionNode call: operands = call.Arguments; break;
                case GesExtensionCallExpressionNode call: operands = call.ArgumentList.Expressions; break;
                case GesTypeConstructorExpressionNode constructor: operands = constructor.ArgumentList.Expressions; break;
                case GesMessageLiteralExpressionNode message: operands = message.ArgumentList.Expressions; break;
                case GesHandlerLiteralExpressionNode: yield break;
                case GesListLiteralExpressionNode list: operands = list.Items; break;
                case GesMapLiteralExpressionNode map:
                    var values = new List<GesExpressionNode>();
                    foreach (var entry in map.Entries) values.Add(entry.Value);
                    operands = values;
                    break;
                default: operands = new[] { expression }; break;
            }
            foreach (var operand in operands)
            {
                if (operand is not GesIdentifierExpressionNode && !IsAssemblyScalar(operand))
                    throw AssemblyFailure("Symbolic arguments must be register names or scalar literals.");
                yield return operand;
            }
        }

        private void EmitAssemblySymbolic(GesAssemblyLine line, GesRegisterRef[] registers, LoweringContext context)
        {
            if (AssemblySend(line) is { } send)
            {
                var offset = send.Result ? 1 : 0;
                var delay = send.Delayed ? line.Operands[offset++] : null;
                var message = line.Operands[offset++];
                IReadOnlyList<GesExpressionNode> tags = [];
                if (send.Tags) tags = line.Operands[offset] is GesListLiteralExpressionNode list ? list.Items : throw AssemblyFailure("Send tags require a literal list of registers or tags.");
                if (!send.Result && (line.Name.Contains("Value", StringComparison.Ordinal) ? message is not GesIdentifierExpressionNode : message is not GesMessageLiteralExpressionNode))
                    throw AssemblyFailure("Message opcode and symbolic operand disagree.", line.Name);
                var kind = send.Publish ? GesPublishStatementKind.Publish : GesPublishStatementKind.Emit;
                if (send.Result) EmitSend(registers[0], new GesSendExpressionNode(kind, message, tags, delay), context, new ExpressionState(context.RegisterCount));
                else EmitPublish(new GesPublishStatementNode(kind, message, tags), context);
                return;
            }
            if (line.Name == "Call")
            {
                EmitCallInto((GesCallExpressionNode)line.Operands[1], registers[0], context, new ExpressionState(context.RegisterCount));
                return;
            }
            if (line.Name == "BindHandler")
            {
                EmitHandlerBindCallInto((GesCallExpressionNode)line.Operands[1], registers[0], context, new ExpressionState(context.RegisterCount));
                return;
            }
            if (line.Name is "emit" or "publish")
            {
                var tags = new List<GesExpressionNode>();
                for (var index = 1; index < line.Operands.Count; index++) tags.Add(line.Operands[index]);
                EmitPublish(new GesPublishStatementNode(line.Name == "emit" ? GesPublishStatementKind.Emit : GesPublishStatementKind.Publish, line.Operands[0], tags), context);
                return;
            }
            EmitExpressionToRegister(line.Operands[1], registers[0], context, new ExpressionState(context.RegisterCount));
        }
    }
}
