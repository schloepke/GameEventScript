#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections.Generic;
using System.Linq;
using StepH.Flow.EventScript.Interpreter;
using StepH.Flow.EventScript.Linker;
using StepH.Flow.EventScript.Parser;
using StepH.Flow.EventScript.Types;
using static StepH.Flow.EventScript.Types.EventScriptValueFactory;

namespace StepH.Flow.EventScript.RegisterVM;

public static class RegisterEventScriptCompiler
{
    public static RegisterCompiledEventScript Compile(
        LinkedEventScriptModule module,
        RegisterEventScriptCompilationOptions? options = null)
    {
        _ = module ?? throw new ArgumentNullException(nameof(module));
        var compileOptions = options ?? new RegisterEventScriptCompilationOptions();
        var compatibilityRuntime = new CompiledEventScript(
            module,
            new EventScriptInterpreterCompilationOptions { EnableDiagnostics = compileOptions.EnableDiagnostics });
        var builder = new CompilerBuilder(module, compileOptions, compatibilityRuntime);
        return builder.Build();
    }

    private sealed class CompilerBuilder(
        LinkedEventScriptModule module,
        RegisterEventScriptCompilationOptions options,
        CompiledEventScript compatibilityRuntime)
    {
        private readonly Dictionary<string, int> _stringIndex = new(StringComparer.Ordinal);
        private readonly List<string> _stringPool = [];
        private readonly Dictionary<EventScriptValue, int> _constantIndex = new();
        private readonly List<EventScriptValue> _constantPool = [];
        private readonly Dictionary<string, int> _signatureIndex = new(StringComparer.Ordinal);
        private readonly List<string> _signatures = [];
        private readonly Dictionary<string, int> _namedArgumentLayoutIndex = new(StringComparer.Ordinal);
        private readonly List<IReadOnlyList<string>> _namedArgumentLayouts = [];
        private readonly Dictionary<string, int> _typeMetadataIndex = new(StringComparer.Ordinal);
        private readonly List<string> _typeMetadata = [];
        private readonly List<RegisterProgram> _programs = [];
        private readonly Dictionary<(string Message, int DeclarationOrder), int> _handlerProgramIndices = new();

        public RegisterCompiledEventScript Build()
        {
            CompileMetadata();
            CompileGlobalDefinitions();
            CompileHandlers();

            var handlers = BuildHandlers();
            var bytecode = new RegisterBytecodeModule(
                options,
                _stringPool.ToArray(),
                _constantPool.ToArray(),
                _signatures.ToArray(),
                _namedArgumentLayouts.ToArray(),
                _typeMetadata.ToArray(),
                _programs.ToArray());

            return new RegisterCompiledEventScript(options, bytecode, handlers, module.Callables, compatibilityRuntime);
        }

        private void CompileMetadata()
        {
            foreach (var typeName in module.TypeDefinitions.Keys.OrderBy(name => name, StringComparer.Ordinal))
            {
                AddTypeMetadata(typeName);
            }

            foreach (var callable in module.Callables.Values.OrderBy(callable => callable.Name, StringComparer.Ordinal))
            {
                AddString(callable.Name);
                AddSignature(EventScriptMessageSignature.CreateSignatureId(callable.Name, callable.Parameters));
            }
        }

        private void CompileGlobalDefinitions()
        {
            foreach (var callable in module.Callables.Values.OrderBy(callable => callable.Name, StringComparer.Ordinal))
            {
                CompileCallable(callable);
            }
        }

        private void CompileCallable(LinkedCallableDefinition callable)
        {
            var instructions = new List<RegisterInstruction>
            {
                new(RegisterOpCode.EvaluateExpression, AddString(GetExpressionDebugName(callable.Expression)), AllocateRegister()),
                new(RegisterOpCode.Return, 0)
            };
            _programs.Add(new RegisterProgram(
                $"{callable.Kind}:{callable.Name}",
                instructions.ToArray(),
                registerCount: Math.Max(1, instructions.Count),
                localCount: callable.Parameters.Count));
        }

        private void CompileHandlers()
        {
            foreach (var pair in module.Handlers.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            {
                for (var declarationOrder = 0; declarationOrder < pair.Value.Count; declarationOrder++)
                {
                    var handler = pair.Value[declarationOrder];
                    var programIndex = CompileStatements(
                        $"handler:{handler.Message}#{declarationOrder}",
                        handler.Statements,
                        createsScope: false);
                    _handlerProgramIndices[(handler.Message, declarationOrder)] = programIndex;
                    AddSignature(EventScriptMessageSignature.CreateSignatureId(handler.Message, handler.Parameters));
                }
            }
        }

        private int CompileStatements(string name, IReadOnlyList<StatementNode> statements, bool createsScope)
        {
            var instructions = new List<RegisterInstruction>();
            var localCount = 0;
            foreach (var statement in statements)
            {
                CompileStatement(statement, instructions, ref localCount);
            }

            instructions.Add(new RegisterInstruction(RegisterOpCode.Return));
            var programIndex = _programs.Count;
            _programs.Add(new RegisterProgram(
                name,
                instructions.ToArray(),
                registerCount: Math.Max(1, instructions.Count),
                localCount,
                createsScope));
            return programIndex;
        }

        private void CompileStatement(StatementNode statement, List<RegisterInstruction> instructions, ref int localCount)
        {
            switch (statement)
            {
                case PublishStatementNode publish:
                    AddNamedArgumentLayout(publish.MessageExpression);
                    instructions.Add(new RegisterInstruction(
                        RegisterOpCode.EvaluateExpression,
                        AddString(GetExpressionDebugName(publish.MessageExpression)),
                        AllocateRegister()));
                    instructions.Add(new RegisterInstruction(RegisterOpCode.Publish, instructions.Count - 1));
                    break;

                case LetStatementNode let:
                    var localSlot = localCount++;
                    instructions.Add(new RegisterInstruction(
                        RegisterOpCode.EvaluateExpression,
                        AddString(GetExpressionDebugName(let.Expression)),
                        AllocateRegister()));
                    instructions.Add(new RegisterInstruction(
                        RegisterOpCode.StoreLocal,
                        AddString(let.Identifier),
                        localSlot,
                        string.IsNullOrEmpty(let.DeclaredType) ? -1 : AddTypeMetadata(let.DeclaredType!)));
                    break;

                case IfStatementNode ifStatement:
                    var thenIndex = CompileStatements("if.then", ifStatement.ThenBody.Statements, ifStatement.ThenBody.IsBlock);
                    var elseIndex = ifStatement.ElseBody is null
                        ? -1
                        : CompileStatements("if.else", ifStatement.ElseBody.Statements, ifStatement.ElseBody.IsBlock);
                    instructions.Add(new RegisterInstruction(
                        RegisterOpCode.EvaluateExpression,
                        AddString(GetExpressionDebugName(ifStatement.Condition)),
                        AllocateRegister()));
                    instructions.Add(new RegisterInstruction(RegisterOpCode.JumpIfFalse, instructions.Count - 1, thenIndex, elseIndex));
                    break;

                case ForStatementNode forStatement:
                    var bodyIndex = CompileStatements("for.body", forStatement.Body.Statements, forStatement.Body.IsBlock);
                    instructions.Add(new RegisterInstruction(
                        RegisterOpCode.ForEach,
                        AddString(forStatement.Identifier),
                        AddString(GetIterationSourceDebugName(forStatement.Source)),
                        bodyIndex));
                    break;

                case SeededRandomStatementNode seededRandom:
                    var randomBodyIndex = CompileStatements("seededRandom.body", seededRandom.Body.Statements, seededRandom.Body.IsBlock);
                    instructions.Add(new RegisterInstruction(
                        RegisterOpCode.EvaluateExpression,
                        AddString(GetExpressionDebugName(seededRandom.SeedExpression)),
                        AllocateRegister()));
                    instructions.Add(new RegisterInstruction(RegisterOpCode.SeededRandom, instructions.Count - 1, randomBodyIndex));
                    break;

                case ExpressionStatementNode expressionStatement:
                    instructions.Add(new RegisterInstruction(
                        RegisterOpCode.EvaluateExpression,
                        AddString(GetExpressionDebugName(expressionStatement.Expression)),
                        AllocateRegister()));
                    break;
            }
        }

        private IReadOnlyDictionary<string, IReadOnlyList<RegisterCompiledEventScriptHandler>> BuildHandlers()
        {
            return compatibilityRuntime.Handlers.ToDictionary(
                pair => pair.Key,
                pair => (IReadOnlyList<RegisterCompiledEventScriptHandler>)pair.Value
                    .Select(handler => new RegisterCompiledEventScriptHandler(
                        handler.Message,
                        handler.Parameters,
                        handler.SignatureId,
                        handler.DeclarationOrder,
                        _handlerProgramIndices.TryGetValue((handler.Message, handler.DeclarationOrder), out var programIndex) ? programIndex : -1,
                        options.EnableDiagnostics,
                        handler,
                        handler.Statements,
                        RegisterVmFastPathAnalyzer.CreateHandlerPlan(handler.Parameters, handler.Statements, module.Callables)))
                    .ToArray(),
                StringComparer.Ordinal);
        }

        private int AllocateRegister() => 0;

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

        private int AddConstant(EventScriptValue value)
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

        private int AddNamedArgumentLayout(ExpressionNode expression)
        {
            if (expression is not MessageLiteralExpressionNode message)
            {
                return -1;
            }

            var orderedNames = message.Arguments.Select(argument => argument.Name).OrderBy(name => name, StringComparer.Ordinal).ToArray();
            var key = string.Join("\u001f", orderedNames);
            if (_namedArgumentLayoutIndex.TryGetValue(key, out var index))
            {
                return index;
            }

            index = _namedArgumentLayouts.Count;
            _namedArgumentLayouts.Add(orderedNames);
            _namedArgumentLayoutIndex[key] = index;
            foreach (var name in orderedNames)
            {
                AddString(name);
            }

            return index;
        }

        private string GetExpressionDebugName(ExpressionNode expression)
        {
            switch (expression)
            {
                case BooleanLiteralExpressionNode boolean:
                    AddConstant(EventScriptValueFactory.Boolean(boolean.Value));
                    return "literal:boolean";
                case IntegerLiteralExpressionNode integer:
                    AddConstant(Integer(integer.Value));
                    return "literal:integer";
                case DecimalLiteralExpressionNode decimalLiteral:
                    AddConstant(Decimal(decimalLiteral.Value));
                    return "literal:decimal";
                case PercentageLiteralExpressionNode percentage:
                    AddConstant(Percentage(percentage.PercentValue / 100m));
                    return "literal:percentage";
                case UnitDecimalLiteralExpressionNode unitDecimal:
                    AddConstant(Decimal(
                        unitDecimal.Value,
                        EventScriptDecimalUnits.TryParseTypeName(unitDecimal.UnitName, out var unit) ? unit : null));
                    return "literal:unitDecimal";
                case TextLiteralExpressionNode text:
                    AddConstant(Text(text.Value));
                    return "literal:text";
                case TagLiteralExpressionNode tag:
                    AddConstant(Tag(tag.Name));
                    return "literal:tag";
                case ListLiteralExpressionNode list:
                    foreach (var item in list.Items)
                    {
                        GetExpressionDebugName(item);
                    }

                    return "literal:list";
                case SetLiteralExpressionNode set:
                    foreach (var item in set.Items)
                    {
                        GetExpressionDebugName(item);
                    }

                    return "literal:set";
                case DictionaryLiteralExpressionNode dictionary:
                    foreach (var entry in dictionary.Entries)
                    {
                        AddString(entry.Key);
                        GetExpressionDebugName(entry.Value);
                    }

                    return "literal:dictionary";
                case IdentifierExpressionNode identifier:
                    AddString(identifier.Name);
                    return "identifier";
                case MessageLiteralExpressionNode message:
                    AddString(message.Message);
                    AddNamedArgumentLayout(message);
                    return "message";
                case HandlerLiteralExpressionNode handler:
                    AddString(handler.Message);
                    foreach (var parameter in handler.Parameters)
                    {
                        AddString(parameter);
                    }

                    return "handler";
                case HandlerBindExpressionNode handlerBind:
                    GetExpressionDebugName(handlerBind.CalleeExpression);
                    foreach (var argument in handlerBind.Arguments)
                    {
                        AddString(argument.Name);
                        GetExpressionDebugName(argument.Expression);
                    }

                    return "handlerBind";
                case CallExpressionNode call:
                    AddString(call.Name);
                    return "call";
                case BinaryExpressionNode binary:
                    AddString(binary.Operator);
                    return "binary";
                case CollectionAccessExpressionNode:
                    return "collectionAccess";
                case MemberAccessExpressionNode member:
                    AddString(member.Member);
                    return "memberAccess";
                case TypeCastExpressionNode typeCast:
                    AddTypeMetadata(typeCast.TypeName);
                    return "cast";
                case TypeCheckExpressionNode typeCheck:
                    AddTypeMetadata(typeCheck.TypeName);
                    return "typeCheck";
                case RulePredicateExpressionNode rulePredicate:
                    AddString(rulePredicate.RuleName);
                    return "rulePredicate";
                case RangeExpressionNode:
                    return "range";
                case DiceExpressionNode dice:
                    return $"dice:{dice.DiceCount}d{dice.SideCount}";
                default:
                    return expression.GetType().Name;
            }
        }

        private string GetIterationSourceDebugName(IterationSourceNode source)
            => source switch
            {
                CollectionIterationSourceNode collection => $"collection:{GetExpressionDebugName(collection.Expression)}",
                RangeIterationSourceNode range => $"range:{GetExpressionDebugName(range.RangeExpression)}",
                _ => source.GetType().Name
            };
    }
}
