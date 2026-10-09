// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using GameEventScript.Api;

namespace GameEventScript.Compiler;

internal static partial class GesCompiler
{
    private sealed partial class BinaryCompiler
    {
        private static string? AssemblyIteratorSignature(AssemblyLine line)
        {
            if (line.Name == "IteratorNext" && line.Operands.Count == 3 && line.Operands[0] is ListLiteralExpressionNode targets)
            {
                var names = new HashSet<string>(System.StringComparer.Ordinal);
                if (targets.Items.Count == 0) throw AssemblyFailure("Component targets cannot be empty.");
                foreach (var item in targets.Items)
                {
                    var name = AssemblyName(item);
                    if (!names.Add(name) || name == AssemblyName(line.Operands[1])) throw AssemblyFailure("Component targets must be distinct from each other and the iterator.", name);
                }
                return "Wrl";
            }
            var create = line.Name == "IteratorCreate" && line.Operands.Count == 3;
            var branch = line.Name == "IteratorCreateOrJump" && line.Operands.Count == 4;
            if (!create && !branch) return null;
            var mode = AssemblyIteratorMode(line.Operands[^1]);
            if (mode is >= 1 and <= 5)
            {
                if (line.Operands[1] is not ListLiteralExpressionNode { Items.Count: >= 2 }) throw AssemblyFailure("Compound iterators require at least two source registers.");
                return branch ? "welt" : "wet";
            }
            return branch ? "wrlt" : "wrt";
        }

        private static byte AssemblyIteratorMode(ExpressionNode operand)
            => AssemblyText(operand) switch
            {
                "normal" => 0,
                "union" => 1,
                "intersect" => 2,
                "difference" => 3,
                "lockstep" => 4,
                "cartesian" => 5,
                "entries" => 6,
                _ => throw AssemblyFailure("Unknown iterator mode.")
            };

        private static IEnumerable<ExpressionNode> AssemblyWriteOperands(char role, ExpressionNode operand)
        {
            if (role == 'w') yield return operand;
            else if (role == 'W')
                foreach (var item in ((ListLiteralExpressionNode)operand).Items) yield return item;
        }

        private void EmitAssemblyIterator(AssemblyLine line, GesRegisterRef[] registers, LoweringContext context, IReadOnlyDictionary<string, GesLabelRef> labels)
        {
            if (line.Name == "IteratorNext")
            {
                var items = ((ListLiteralExpressionNode)line.Operands[0]).Items;
                var targets = new GesRegisterRef[items.Count];
                for (var index = 0; index < targets.Length; index++) targets[index] = context.Require(AssemblyName(items[index]));
                _builder.IteratorNextComponents(targets, registers[1], labels[AssemblyName(line.Operands[2])]);
                return;
            }
            var mode = AssemblyIteratorMode(line.Operands[^1]);
            GesOperand sources;
            if (mode is >= 1 and <= 5)
            {
                var items = ((ListLiteralExpressionNode)line.Operands[1]).Items;
                var inputs = new GesRegisterRef[items.Count];
                for (var index = 0; index < inputs.Length; index++)
                {
                    if (items[index] is IdentifierExpressionNode name) inputs[index] = context.Require(name.Name);
                    else
                    {
                        inputs[index] = _builder.AddCompilerRegister("assembly_source");
                        EmitExpressionToRegister(items[index], inputs[index], context, new ExpressionState(context.RegisterCount));
                    }
                }
                sources = GesOperand.RegisterList(inputs);
            }
            else sources = GesOperand.Register(registers[1]);
            var branch = line.Name == "IteratorCreateOrJump";
            _builder.AddOpcode(branch ? GameEventScriptBytecodeOpCode.IteratorCreateOrJump : GameEventScriptBytecodeOpCode.IteratorCreate,
                flags: (GameEventScriptInstructionFlag)(mode << 5), dst: GesOperand.Register(registers[0]), x: sources,
                y: branch ? GesOperand.Label(labels[AssemblyName(line.Operands[2])]) : default);
        }
    }
}
