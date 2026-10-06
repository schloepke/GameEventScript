// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using GameEventScript.Api;
using GameEventScript.Runtime;

namespace GameEventScript.Compiler;

internal static partial class GesCompiler
{
    private sealed partial class BinaryCompiler
    {
        private static bool IsAssemblyScalar(ExpressionNode operand)
            => operand is IntegerLiteralExpressionNode or FloatLiteralExpressionNode or BooleanLiteralExpressionNode or NothingLiteralExpressionNode
                or TextLiteralExpressionNode or TagLiteralExpressionNode or UnitIntegerLiteralExpressionNode or UnitFloatLiteralExpressionNode or PercentageLiteralExpressionNode;

        private static string AssemblyName(ExpressionNode operand)
            => operand is IdentifierExpressionNode name ? name.Name : throw AssemblyFailure("Expected a register or label name.");

        private static string AssemblyText(ExpressionNode operand)
            => operand switch
            {
                TextLiteralExpressionNode text => text.Value,
                TagLiteralExpressionNode tag => tag.Name,
                _ => throw AssemblyFailure("Expected literal text or tag.")
            };

        private static long AssemblyInteger(ExpressionNode operand, long minimum, long maximum)
        {
            var value = operand switch
            {
                IntegerLiteralExpressionNode integer => integer.Value,
                _ => throw AssemblyFailure("Expected an exact integer literal.")
            };
            if (value < minimum || value > maximum) throw AssemblyFailure("Assembly immediate is out of range.");
            return value;
        }

        private static double AssemblyNumber(ExpressionNode operand)
            => operand switch
            {
                IntegerLiteralExpressionNode integer => integer.Value,
                FloatLiteralExpressionNode number => number.Value,
                _ => throw AssemblyFailure("Expected a numeric literal.")
            };

        private static GameEventScriptCompileException AssemblyFailure(string message, string? symbol = null)
            => CompileFailure("compile.invalidAssembly", message, symbol);

        private void EmitAssembly(AssemblyBlockNode block, GesRegisterRef? result, LoweringContext outer)
        {
            var context = outer.CreateChild();
            var writable = new HashSet<string>(StringComparer.Ordinal);
            var outputs = new HashSet<string>(StringComparer.Ordinal);
            var locals = new HashSet<string>(StringComparer.Ordinal);
            if (block.Output is { } output)
            {
                if (outer.Contains(output) && outer.Require(output).Id != result!.Value.Id)
                    throw AssemblyFailure("Assembly result cannot shadow an input binding.", output);
                writable.Add(output);
                outputs.Add(output);
                context.DeclareExisting(output, result!.Value);
            }
            foreach (var declaration in block.Declarations)
            {
                if (!GameEventScriptText.IsVariableName(declaration.Name)) throw AssemblyFailure("Invalid assembly register name.", declaration.Name);
                if (!writable.Add(declaration.Name) || outer.Contains(declaration.Name))
                    throw AssemblyFailure("Assembly bindings cannot shadow existing bindings.", declaration.Name);
                if (declaration.Export) outputs.Add(declaration.Name);
                else locals.Add(declaration.Name);
                context.Declare(declaration.Name);
            }

            var labels = new Dictionary<string, GesLabelRef>(StringComparer.Ordinal);
            var targets = new Dictionary<string, int>(StringComparer.Ordinal);
            for (var index = 0; index < block.Lines.Count; index++)
            {
                var line = block.Lines[index];
                if (!line.IsLabel) continue;
                if (!GameEventScriptText.IsVariableName(line.Name)) throw AssemblyFailure("Invalid assembly label.", line.Name);
                if (!targets.TryAdd(line.Name, index)) throw AssemblyFailure("Duplicate assembly label.", line.Name);
                labels.Add(line.Name, _builder.AddLabel(line.Name));
            }

            // Resolve all operands even on unreachable paths. Pinned storage is
            // deliberate: interval-only temporary allocation is not valid for
            // arbitrary assembly back edges.
            var signatures = new string[block.Lines.Count];
            for (var index = 0; index < block.Lines.Count; index++)
            {
                var line = block.Lines[index];
                if (!block.AllowSend && (line.Name is "emit" or "publish" || line.Name.StartsWith("Emit", StringComparison.Ordinal) || line.Name.StartsWith("Publish", StringComparison.Ordinal)))
                    throw AssemblyFailure("Expression assembly cannot send messages.", line.Name);
                var signature = line.IsLabel ? "" : AssemblySymbolicSignature(line) ?? AssemblySpecialSignature(line) ?? AssemblySignature(line.Name);
                signatures[index] = signature;
                if (signature.Length != line.Operands.Count) throw AssemblyFailure("Invalid assembly operand count.", line.Name);
                for (var operandIndex = 0; operandIndex < signature.Length; operandIndex++)
                {
                    var operand = line.Operands[operandIndex];
                    switch (signature[operandIndex])
                    {
                        case 'w':
                            if (IsAssemblyResource(AssemblyResultKind(line, new Dictionary<string, string>())) && !locals.Contains(AssemblyName(operand)))
                                throw AssemblyFailure("Internal resources require a block-local temporary.", AssemblyName(operand));
                            if (!writable.Contains(AssemblyName(operand))) throw AssemblyFailure("Outer bindings are read-only.", AssemblyName(operand));
                            break;
                        case 'e':
                            foreach (var read in AssemblySymbolicReads(operand, line.Name == "BindHandler"))
                                if (read is IdentifierExpressionNode symbolicName) context.Require(symbolicName.Name);
                            break;
                        case 'r':
                            if (AssemblyExpectedResource(line.Name, operandIndex) is not null && operand is not IdentifierExpressionNode)
                                throw AssemblyFailure("Internal resource operands must name a temporary.");
                            if (operand is IdentifierExpressionNode name) context.Require(name.Name);
                            else if (!IsAssemblyScalar(operand))
                                throw AssemblyFailure("Assembly register operands accept only names or scalar literals.");
                            break;
                        case 'l':
                            if (!targets.ContainsKey(AssemblyName(operand))) throw AssemblyFailure("Unknown assembly label.", AssemblyName(operand));
                            break;
                        case 'i': AssemblyInteger(operand, long.MinValue, long.MaxValue); break;
                        case 's': AssemblyInteger(operand, short.MinValue, short.MaxValue); break;
                        case 'u': AssemblyInteger(operand, ushort.MinValue, ushort.MaxValue); break;
                        case 'f': AssemblyNumber(operand); break;
                        case 't': AssemblyText(operand); break;
                        case 'y': AssemblyType(operand); break;
                        case 'q': AssemblyUnit(operand); break;
                        case 'p': AssemblyPattern(operand); break;
                        case 'z': AssemblySeries(operand); break;
                    }
                }
            }

            ValidateAssemblyFlow(block, signatures, targets, writable, outputs, locals);
            _builder.PreserveCurrentRoutine();
            for (var index = 0; index < block.Lines.Count; index++)
            {
                var line = block.Lines[index];
                using var source = _builder.SourceRange(line.SourceRange);
                if (line.IsLabel)
                {
                    _builder.MarkLabel(labels[line.Name]);
                    continue;
                }
                var registers = new GesRegisterRef[line.Operands.Count];
                for (var operandIndex = 0; operandIndex < line.Operands.Count; operandIndex++)
                {
                    if (signatures[index][operandIndex] is not ('w' or 'r')) continue;
                    var operand = line.Operands[operandIndex];
                    if (operand is IdentifierExpressionNode name) registers[operandIndex] = context.Require(name.Name);
                    else
                    {
                        var register = _builder.AddCompilerRegister("assembly_literal");
                        EmitExpressionToRegister(operand, register, context, new ExpressionState(context.RegisterCount));
                        registers[operandIndex] = register;
                    }
                }
                if (AssemblySymbolicSignature(line) is not null) EmitAssemblySymbolic(line, registers, context);
                else if (!EmitAssemblySpecial(line, registers)) EmitAssemblyInstruction(line, registers, labels);
            }
            foreach (var declaration in block.Declarations)
                if (declaration.Export) outer.DeclareExisting(declaration.Name, context.Require(declaration.Name));
        }

        private static void ValidateAssemblyFlow(
            AssemblyBlockNode block,
            string[] signatures,
            IReadOnlyDictionary<string, int> labels,
            ISet<string> writable,
            ISet<string> outputs,
            ISet<string> locals)
        {
            var states = new Dictionary<string, string>?[block.Lines.Count + 1];
            states[0] = new(StringComparer.Ordinal);
            var randomDepth = new int?[block.Lines.Count + 1];
            randomDepth[0] = 0;
            var pending = new Queue<int>();
            pending.Enqueue(0);
            while (pending.Count > 0)
            {
                var index = pending.Dequeue();
                var state = new Dictionary<string, string>(states[index]!, StringComparer.Ordinal);
                if (index == block.Lines.Count) continue;
                var line = block.Lines[index];
                var signature = signatures[index];
                var depth = randomDepth[index]!.Value;
                if (line.Name is "RandomPush" or "RandomPushConstant") depth++;
                if (line.Name == "RandomPop" && --depth < 0) throw AssemblyFailure("Assembly cannot pop an outer random scope.");
                // Delay read diagnostics until the fixed point: early paths can
                // carry more initialization than a later loop/join predecessor.
                for (var operandIndex = 0; operandIndex < signature.Length; operandIndex++)
                {
                    if (signature[operandIndex] != 'w') continue;
                    var destination = AssemblyName(line.Operands[operandIndex]);
                    state[destination] = AssemblyResultKind(line, state);
                }
                if (line.Name == "IteratorClose") state[AssemblyName(line.Operands[0])] = "closed";
                if (line.Name.Contains("BuilderFinish", StringComparison.Ordinal)) state[AssemblyName(line.Operands[1])] = "closed";
                if (line.Name == "IteratorCreateOrJump")
                {
                    Merge(index + 1, true);
                    state[AssemblyName(line.Operands[0])] = "value";
                    Merge(labels[AssemblyName(line.Operands[2])], true);
                    continue;
                }
                Merge(index + 1, line.Name != "Jump");
                for (var operandIndex = 0; operandIndex < signature.Length; operandIndex++)
                    if (signature[operandIndex] == 'l') Merge(labels[AssemblyName(line.Operands[operandIndex])], true);

                void Merge(int target, bool enabled)
                {
                    if (!enabled) return;
                    if (randomDepth[target] is { } knownDepth && knownDepth != depth) throw AssemblyFailure("Random scopes must balance at control-flow joins.");
                    randomDepth[target] = depth;
                    if (states[target] is not { } previous)
                    {
                        states[target] = new(state, StringComparer.Ordinal);
                        pending.Enqueue(target);
                        return;
                    }
                    foreach (var entry in state)
                        if (IsAssemblyResource(entry.Value) && !previous.ContainsKey(entry.Key))
                            throw AssemblyFailure("Resource is not live on every incoming path.", entry.Key);
                    var changed = false;
                    foreach (var name in new List<string>(previous.Keys))
                    {
                        if (!state.TryGetValue(name, out var kind))
                        {
                            if (IsAssemblyResource(previous[name])) throw AssemblyFailure("Resource is not live on every incoming path.", name);
                            previous.Remove(name);
                            changed = true;
                        }
                        else if (previous[name] != kind)
                        {
                            if (IsAssemblyResource(previous[name]) || IsAssemblyResource(kind))
                                throw AssemblyFailure("Incompatible resource states at assembly join.", name);
                            var merged = previous[name] == "closed" || kind == "closed" ? "closed" : "value";
                            if (previous[name] != merged) { previous[name] = merged; changed = true; }
                        }
                    }
                    if (changed) pending.Enqueue(target);
                }
            }
            for (var index = 0; index < block.Lines.Count; index++)
            {
                if (states[index] is not { } state) continue;
                var line = block.Lines[index];
                var signature = signatures[index];
                for (var operandIndex = 0; operandIndex < signature.Length; operandIndex++)
                {
                    if (signature[operandIndex] is not ('r' or 'e')) continue;
                    var reads = signature[operandIndex] == 'e' ? AssemblySymbolicReads(line.Operands[operandIndex], line.Name == "BindHandler") : new[] { line.Operands[operandIndex] };
                    foreach (var read in reads)
                    {
                    if (read is not IdentifierExpressionNode name) continue;
                    if (writable.Contains(name.Name) && !state.ContainsKey(name.Name))
                        throw AssemblyFailure("Assembly register is not initialized on every incoming path.", name.Name);
                    if (state.TryGetValue(name.Name, out var kind))
                    {
                        if (kind == "closed") throw AssemblyFailure("Assembly resource is already closed.", name.Name);
                        if (IsAssemblyResource(kind) && !AssemblyAcceptsResource(line.Name, operandIndex, kind))
                            throw AssemblyFailure("Internal assembly values cannot escape or be copied.", name.Name);
                    }
                    var expected = AssemblyExpectedResource(line.Name, operandIndex);
                    if (expected is not null && (!state.TryGetValue(name.Name, out var actual) || expected != actual))
                        throw AssemblyFailure("Assembly resource has the wrong lifecycle state.", name.Name);
                    }
                }
                for (var operandIndex = 0; operandIndex < signature.Length; operandIndex++)
                {
                    if (signature[operandIndex] != 'w') continue;
                    var name = AssemblyName(line.Operands[operandIndex]);
                    if (state.TryGetValue(name, out var old) && IsAssemblyResource(old))
                        throw AssemblyFailure("Cannot overwrite a live assembly resource.", name);
                    if (IsAssemblyResource(AssemblyResultKind(line, state)) && !locals.Contains(name))
                        throw AssemblyFailure("Internal resources require a block-local temporary.", name);
                }
            }
            if (states[^1] is { } exit)
            {
                if (randomDepth[^1] != 0) throw AssemblyFailure("Assembly random scopes must balance at exit.");
                foreach (var entry in exit)
                    if (IsAssemblyResource(entry.Value)) throw AssemblyFailure("Assembly resource is still live at block exit.", entry.Key);
                if (block.Predicate && exit.TryGetValue(block.Output!, out var resultKind) && resultKind != "boolean")
                    throw CompileFailure("compile.invalidAssembly", "Assembly predicate must return Boolean or Nothing.", block.Output);
                foreach (var output in outputs)
                    if (!exit.ContainsKey(output)) throw AssemblyFailure("Assembly output is not initialized on every exit.", output);
            }
        }

        private static bool IsAssemblyResource(string kind) => kind == "iterator" || kind.EndsWith("Builder", StringComparison.Ordinal);

        private static string? AssemblyExpectedResource(string opcode, int operand)
        {
            if ((opcode == "IteratorNext" && operand == 1) || (opcode == "IteratorClose" && operand == 0)) return "iterator";
            var builder = opcode.IndexOf("Builder", StringComparison.Ordinal);
            if (builder >= 0 && ((opcode.EndsWith("Add", StringComparison.Ordinal) && operand == 0) || (opcode.Contains("Finish", StringComparison.Ordinal) && operand == 1)))
                return opcode.Substring(0, builder + 7);
            return null;
        }

        private static bool AssemblyAcceptsResource(string opcode, int operand, string kind)
            => AssemblyExpectedResource(opcode, operand) == kind;

        private static bool AssemblyBooleanOperand(ExpressionNode operand, IReadOnlyDictionary<string, string> state)
            => operand is BooleanLiteralExpressionNode or NothingLiteralExpressionNode || operand is IdentifierExpressionNode name && state.TryGetValue(name.Name, out var kind) && kind == "boolean";

        private static string AssemblyResultKind(AssemblyLine line, IReadOnlyDictionary<string, string> state)
            => line.Name switch
            {
                "Move" when line.Operands[1] is BooleanLiteralExpressionNode or NothingLiteralExpressionNode => "boolean",
                "Not" when AssemblyBooleanOperand(line.Operands[1], state) => "boolean",
                "And" or "Or" or "Xor" or "Implies" when AssemblyBooleanOperand(line.Operands[1], state) && AssemblyBooleanOperand(line.Operands[2], state) => "boolean",
                "IteratorCreate" or "IteratorCreateOrJump" or "CreateRangeIterator" or "CreateRangeIteratorWithStep" or "CreateRangeIteratorShort" => "iterator",
                "ListBuilderCreate" => "ListBuilder",
                "MapBuilderCreate" => "MapBuilder",
                "DistinctBuilderCreate" => "DistinctBuilder",
                "GroupBuilderCreate" => "GroupBuilder",
                "OrderBuilderCreate" => "OrderBuilder",
                "LoadTrue" or "LoadFalse" or "LoadNothing" or "Equal" or "NotEqual" or "Less" or "Greater" or "LessOrEqual" or "GreaterOrEqual" or "HasValue" or "IsEmpty"
                    or "CheckInteger" or "CheckFractional" or "CheckNumeric" or "CheckType" or "CheckCustomType" or "CheckUnit" or "Contains" or "ContainsAny" or "ContainsAll"
                    or "HasAny" or "HasAll" or "HasPattern" or "StartsWith" or "EndsWith" => "boolean",
                "Move" when line.Operands[1] is IdentifierExpressionNode name && state.TryGetValue(name.Name, out var kind) => kind,
                _ => "value"
            };
    }
}
