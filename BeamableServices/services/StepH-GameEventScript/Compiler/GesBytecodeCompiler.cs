#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections.Generic;
using System.Linq;
using StepH.GameEventScript.Api;
using StepH.GameEventScript.Runtime;
using StepH.GameEventScript.Types;

namespace StepH.GameEventScript.Compiler;

internal static class GesBytecodeCompiler
{
    public static GameEventScriptCompiled Compile(GesModule module, GameEventScriptCompileOptions? options = null)
    {
        _ = module ?? throw new ArgumentNullException(nameof(module));
        var compileOptions = options ?? new GameEventScriptCompileOptions();
        var builder = new CompilerBuilder(module, compileOptions);
        return builder.Build();
    }

    private sealed class CompilerBuilder(GesModule module, GameEventScriptCompileOptions options)
    {
        private readonly Dictionary<string, int> _stringIndex = new(StringComparer.Ordinal);
        private readonly List<string> _stringPool = [];
        private readonly Dictionary<GameEventScriptValue, int> _constantIndex = new(ConstantPoolValueComparer.Instance);
        private readonly List<GameEventScriptValue> _constantPool = [];
        private readonly Dictionary<string, int> _signatureIndex = new(StringComparer.Ordinal);
        private readonly List<string> _signatures = [];
        private readonly Dictionary<string, int> _externalReferenceIndex = new(StringComparer.Ordinal);
        private readonly List<GameEventScriptExtensionReference> _externalReferences = [];
        private readonly Dictionary<string, int> _namedArgumentLayoutIndex = new(StringComparer.Ordinal);
        private readonly List<IReadOnlyList<string>> _namedArgumentLayouts = [];
        private readonly Dictionary<string, int> _typeMetadataIndex = new(StringComparer.Ordinal);
        private readonly List<string> _typeMetadata = [];

        public GameEventScriptCompiled Build()
        {
            CollectSourceMetadata();

            var typeDefinitions = GesBytecodeLowerer.CompileTypeDefinitions(
                module.Callables,
                module.TypeDefinitions,
                AddExternalReference,
                AddConstant);

            var callables = GesBytecodeLowerer.CompileCallableDefinitions(
                module.Callables,
                module.TypeDefinitions,
                AddExternalReference,
                AddConstant);

            var handlers = BuildHandlers();
            CollectBytecodeMetadata(callables, handlers, typeDefinitions);
            var maxStackDepth = Math.Max(
                GetMaxStackDepth(handlers),
                Math.Max(GetMaxStackDepth(callables), GetMaxStackDepth(typeDefinitions)));

            return new GameEventScriptCompiled(
                options,
                _stringPool.ToArray(),
                _constantPool.ToArray(),
                _signatures.ToArray(),
                _externalReferences.ToArray(),
                _namedArgumentLayouts.ToArray(),
                _typeMetadata.ToArray(),
                callables,
                handlers,
                typeDefinitions,
                maxStackDepth);
        }

        private IReadOnlyDictionary<string, IReadOnlyList<GameEventScriptBytecodeHandler>> BuildHandlers()
            => module.Handlers.ToDictionary(
                pair => pair.Key,
                pair => (IReadOnlyList<GameEventScriptBytecodeHandler>)pair.Value
                    .Select((handler, index) =>
                    {
                        var signatureId = GameEventScriptMessageSignature.CreateSignatureId(pair.Key, handler.SignatureLabels);
                        return new GameEventScriptBytecodeHandler(
                            pair.Key,
                            handler.Parameters,
                            handler.SignatureLabels,
                            signatureId,
                            index,
                            GesBytecodeLowerer.CompileHandlerPlan(
                                pair.Key,
                                index,
                                handler.Parameters,
                                handler.Statements,
                                module.Callables,
                                module.TypeDefinitions,
                                AddExternalReference,
                                AddConstant));
                    })
                    .ToArray(),
                StringComparer.Ordinal);

        private int AddExternalReference(GameEventScriptExtensionReference reference)
        {
            if (GesStandardExtensions.IsStandardReference(reference))
            {
                return -1;
            }

            if (_externalReferenceIndex.TryGetValue(reference.SignatureId, out var existing))
            {
                return existing;
            }

            var index = _externalReferences.Count;
            _externalReferences.Add(reference);
            _externalReferenceIndex[reference.SignatureId] = index;
            AddString(reference.ExtensionName);
            AddString(reference.FunctionName);
            foreach (var label in reference.ArgumentLabels)
            {
                AddString(label);
            }

            return index;
        }

        private int AddString(string value)
        {
            if (_stringIndex.TryGetValue(value, out var index))
            {
                return index;
            }

            index = _stringPool.Count;
            _stringPool.Add(value);
            _stringIndex[value] = index;
            return index;
        }

        private int AddConstant(GameEventScriptValue value)
        {
            if (_constantIndex.TryGetValue(value, out var index))
            {
                return index;
            }

            index = _constantPool.Count;
            _constantPool.Add(value);
            _constantIndex[value] = index;
            return index;
        }

        private int AddSignature(string value)
        {
            if (_signatureIndex.TryGetValue(value, out var index))
            {
                return index;
            }

            index = _signatures.Count;
            _signatures.Add(value);
            _signatureIndex[value] = index;
            return index;
        }

        private int AddTypeMetadata(string value)
        {
            if (_typeMetadataIndex.TryGetValue(value, out var index))
            {
                return index;
            }

            index = _typeMetadata.Count;
            _typeMetadata.Add(value);
            _typeMetadataIndex[value] = index;
            AddString(value);
            return index;
        }

        private int AddNamedArgumentLayout(IReadOnlyList<string> orderedNames)
        {
            var key = string.Join("\u001f", orderedNames);
            if (_namedArgumentLayoutIndex.TryGetValue(key, out var index))
            {
                return index;
            }

            index = _namedArgumentLayouts.Count;
            _namedArgumentLayouts.Add(orderedNames.ToArray());
            _namedArgumentLayoutIndex[key] = index;
            foreach (var name in orderedNames)
            {
                AddString(name);
            }

            return index;
        }

        private static int GetMaxStackDepth(IReadOnlyDictionary<string, IReadOnlyList<GameEventScriptBytecodeHandler>> handlers)
            => handlers.Count == 0
                ? 1
                : handlers.Values.SelectMany(group => group).Select(handler => handler.ExecutionPlan.MaxStackDepth).DefaultIfEmpty(1).Max();

        private static int GetMaxStackDepth(IReadOnlyDictionary<string, GameEventScriptBytecodeCallable> callables)
            => callables.Count == 0
                ? 1
                : callables.Values.Select(callable => callable.ExpressionProgram.MaxStackDepth).DefaultIfEmpty(1).Max();

        private static int GetMaxStackDepth(IReadOnlyDictionary<string, GameEventScriptBytecodeTypeDefinition> typeDefinitions)
            => typeDefinitions.Count == 0
                ? 1
                : typeDefinitions.Values.SelectMany(type => type.Fields).SelectMany(field => new[]
                {
                    field.MinimumProgram,
                    field.MaximumProgram,
                    field.ComputedProgram
                }).Where(program => program is not null).Select(program => program!.MaxStackDepth).DefaultIfEmpty(1).Max();

        private void CollectSourceMetadata()
        {
            foreach (var type in module.TypeDefinitions.Values.OrderBy(type => type.Name, StringComparer.Ordinal))
            {
                AddTypeMetadata(type.Name);
                foreach (var field in type.Fields)
                {
                    AddString(field.Name);
                    AddTypeMetadata(field.TypeName);
                }
            }

            foreach (var callable in module.Callables.Values.OrderBy(callable => callable.Name, StringComparer.Ordinal))
            {
                AddString(callable.Name);
                foreach (var parameter in callable.Parameters)
                {
                    AddString(parameter);
                }

                foreach (var label in callable.SignatureLabels)
                {
                    AddString(label);
                }

                AddSignature(GameEventScriptMessageSignature.CreateSignatureId(callable.Name, callable.SignatureLabels));
            }

            foreach (var pair in module.Handlers.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            {
                AddString(pair.Key);
                foreach (var handler in pair.Value)
                {
                    foreach (var parameter in handler.Parameters)
                    {
                        AddString(parameter);
                    }

                    foreach (var label in handler.SignatureLabels)
                    {
                        AddString(label);
                    }

                    AddSignature(GameEventScriptMessageSignature.CreateSignatureId(pair.Key, handler.SignatureLabels));
                }
            }
        }

        private void CollectBytecodeMetadata(
            IReadOnlyDictionary<string, GameEventScriptBytecodeCallable> callables,
            IReadOnlyDictionary<string, IReadOnlyList<GameEventScriptBytecodeHandler>> handlers,
            IReadOnlyDictionary<string, GameEventScriptBytecodeTypeDefinition> typeDefinitions)
        {
            foreach (var callable in callables.Values)
            {
                AddString(callable.Name);
                AddSignature(callable.SignatureId);
                CollectExpressionMetadata(callable.ExpressionProgram);
            }

            foreach (var handler in handlers.Values.SelectMany(group => group))
            {
                AddString(handler.Message);
                AddSignature(handler.SignatureId);
                CollectStatementMetadata(handler.ExecutionPlan.StatementProgram);
            }

            foreach (var type in typeDefinitions.Values)
            {
                AddTypeMetadata(type.Name);
                foreach (var field in type.Fields)
                {
                    AddString(field.Name);
                    AddTypeMetadata(field.TypeName);
                    CollectExpressionMetadata(field.MinimumProgram);
                    CollectExpressionMetadata(field.MaximumProgram);
                    CollectExpressionMetadata(field.ComputedProgram);
                }
            }
        }

        private void CollectStatementMetadata(GameEventScriptBytecodeStatementProgram? program)
        {
            if (program is null)
            {
                return;
            }

            foreach (var statement in program.Statements)
            {
                AddStringIfPresent(statement.Name);
                AddTypeIfPresent(statement.DeclaredType);
                AddStringIfPresent(statement.DiagnosticName);
                if (statement.PublishLayout is { } publishLayout)
                {
                    AddString(publishLayout.MessageName);
                    AddSignature(publishLayout.SignatureId);
                    AddNamedArgumentLayout(publishLayout.ArgumentNames);
                    foreach (var argumentProgram in publishLayout.ArgumentPrograms)
                    {
                        CollectExpressionMetadata(argumentProgram);
                    }
                }

                CollectExpressionMetadata(statement.ExpressionProgram);
                CollectStatementMetadata(statement.ThenProgram);
                CollectStatementMetadata(statement.ElseProgram);
                CollectStatementMetadata(statement.BodyProgram);
                CollectIterationSourceMetadata(statement.IterationSource);
            }
        }

        private void CollectExpressionMetadata(GameEventScriptBytecodeExpressionProgram? program)
        {
            if (program is null)
            {
                return;
            }

            foreach (var instruction in program.Instructions)
            {
                AddStringIfPresent(instruction.DiagnosticName);
                AddStringIfPresent(instruction.DiagnosticArgumentName);
                if (instruction.Names is { } names)
                {
                    AddNamedArgumentLayout(names);
                }

                CollectExpressionMetadata(instruction.ExpressionProgram);
                CollectPipelineMetadata(instruction.PipelineProgram);
                CollectGeneratedCollectionMetadata(instruction.GeneratedCollectionProgram);
                CollectGuardedChoiceMetadata(instruction.GuardedChoiceProgram);
            }
        }

        private void CollectPipelineMetadata(GameEventScriptBytecodePipelineProgram? program)
        {
            if (program is null)
            {
                return;
            }

            CollectExpressionMetadata(program.SourceProgram);
            foreach (var selector in program.PrefixSelectors)
            {
                CollectSelectorMetadata(selector);
            }

            CollectSelectorMetadata(program.TerminalSelector);
        }

        private void CollectSelectorMetadata(GameEventScriptBytecodeSelectorProgram selector)
        {
            AddStringIfPresent(selector.EdgeMode);
            AddStringIfPresent(selector.SecondaryMode);
            CollectExpressionMetadata(selector.ExpressionProgram);
            CollectExpressionMetadata(selector.SecondaryExpressionProgram);
            CollectDicePatternMetadata(selector.DicePattern);
            CollectObjectMatchPatternMetadata(selector.ObjectPattern);
        }

        private void CollectGeneratedCollectionMetadata(GameEventScriptBytecodeGeneratedCollectionProgram? program)
        {
            if (program is null)
            {
                return;
            }

            AddString(program.CollectionType);
            CollectIterationSourceMetadata(program.Source);
            CollectExpressionMetadata(program.PredicateProgram);
            CollectExpressionMetadata(program.ProjectionProgram);
        }

        private void CollectGuardedChoiceMetadata(GameEventScriptBytecodeGuardedChoiceProgram? program)
        {
            if (program is null)
            {
                return;
            }

            foreach (var valueProgram in program.ValuePrograms)
            {
                CollectExpressionMetadata(valueProgram);
            }

            foreach (var conditionProgram in program.ConditionPrograms)
            {
                CollectExpressionMetadata(conditionProgram);
            }

            CollectExpressionMetadata(program.OtherwiseProgram);
        }

        private void CollectIterationSourceMetadata(GameEventScriptBytecodeIterationSourceProgram? source)
        {
            if (source is null)
            {
                return;
            }

            CollectExpressionMetadata(source.CollectionProgram);
            CollectExpressionMetadata(source.RangeFromProgram);
            CollectExpressionMetadata(source.RangeToProgram);
            CollectExpressionMetadata(source.RangeStepProgram);
        }

        private void CollectDicePatternMetadata(GameEventScriptBytecodeDicePattern? pattern)
        {
            if (pattern is GameEventScriptBytecodeDiceCountPattern count)
            {
                CollectExpressionMetadata(count.FaceProgram);
            }
        }

        private void CollectObjectMatchPatternMetadata(GameEventScriptBytecodeObjectMatchPattern? pattern)
        {
            if (pattern is null)
            {
                return;
            }

            foreach (var entry in pattern.Entries)
            {
                AddString(entry.Key);
                switch (entry.Value)
                {
                    case GameEventScriptBytecodeObjectMatchExpressionValue expression:
                        CollectExpressionMetadata(expression.ExpressionProgram);
                        break;
                    case GameEventScriptBytecodeObjectMatchNestedValue nested:
                        CollectObjectMatchPatternMetadata(nested.Pattern);
                        break;
                }
            }
        }

        private void AddStringIfPresent(string? value)
        {
            if (!string.IsNullOrEmpty(value))
            {
                AddString(value);
            }
        }

        private void AddTypeIfPresent(string? value)
        {
            if (!string.IsNullOrEmpty(value))
            {
                AddTypeMetadata(value);
            }
        }

        private sealed class ConstantPoolValueComparer : IEqualityComparer<GameEventScriptValue>
        {
            public static readonly ConstantPoolValueComparer Instance = new();

            private ConstantPoolValueComparer()
            {
            }

            public bool Equals(GameEventScriptValue? x, GameEventScriptValue? y)
            {
                if (ReferenceEquals(x, y))
                {
                    return true;
                }

                if (x is null || y is null || x.Kind != y.Kind)
                {
                    return false;
                }

                return x.Equals(y);
            }

            public int GetHashCode(GameEventScriptValue obj)
            {
                var hash = new HashCode();
                hash.Add(obj.Kind);
                hash.Add(obj);
                return hash.ToHashCode();
            }
        }
    }
}
