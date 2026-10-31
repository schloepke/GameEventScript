// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Text;
using static GameEventScript.Syntax.GesTokenKind;
using NodeKind = GameEventScript.Syntax.GesSyntaxNodeKind;

namespace GameEventScript.Syntax;

// Checks the rules of one source that need no other source, catalog or runtime: the spelling of names and labels,
// repeated parameters, arguments, fields and bindings, shadowing, the forms of system endpoints and removed type names.
// The diagnostics come in source order; at one position, shadowing follows the other rules, which keep their check order.
internal static class GesSyntaxValidator
{
    internal const string InvalidIdentifierCase = "validate.invalidIdentifierCase";
    internal const string InvalidMessageCase = "validate.invalidMessageCase";
    internal const string InvalidTypeConstructor = "validate.invalidTypeConstructor";
    internal const string DuplicateVariable = "validate.duplicateVariable";
    internal const string ShadowedVariable = "validate.shadowedVariable";
    internal const string DuplicateDefinitionParameter = "validate.duplicateDefinitionParameter";
    internal const string DuplicateHandlerParameter = "validate.duplicateHandlerParameter";
    internal const string DuplicatePublishArgument = "validate.duplicatePublishArgument";

    // The tree must come from a source without syntax errors.
    internal static IReadOnlyList<GesSyntaxDiagnostic> Validate(GesSyntaxTree tree) => new Walker(tree).Run();

    // A scope of the binding rules: a let binding repeats only within its own scope, and a binding shadows any visible one.
    private sealed class Scope(Scope? parent = null)
    {
        private readonly HashSet<string> _names = new(StringComparer.Ordinal);

        internal Scope? Parent { get; } = parent;

        internal void Declare(string name) => _names.Add(name);

        internal bool ContainsCurrent(string name) => _names.Contains(name);

        internal bool ContainsVisible(string name)
        {
            for (var scope = this; scope is not null; scope = scope.Parent)
                if (scope._names.Contains(name)) return true;
            return false;
        }

        internal bool ContainsInAncestor(string name) => Parent?.ContainsVisible(name) == true;
    }

    private sealed class Walker(GesSyntaxTree tree)
    {
        private readonly IReadOnlyList<GesToken> _tokens = tree.Tokens;
        private readonly IReadOnlyList<GesSyntaxRole> _roles = tree.Roles;
        private readonly List<(GesSyntaxDiagnostic Diagnostic, int Line, int Column, bool Shadowing, int Order)> _found = [];
        private string _module = "UnknownModule";

        internal IReadOnlyList<GesSyntaxDiagnostic> Run()
        {
            foreach (var declaration in tree.Root.Children)
                if (declaration.Kind == NodeKind.ModuleDeclaration) _module = ModuleName(declaration);

            foreach (var declaration in tree.Root.Children)
            {
                switch (declaration.Kind)
                {
                    case NodeKind.RecordDefinition: Record(declaration); break;
                    case NodeKind.PredicateDefinition: Callable(declaration, "Predicate", GesSyntaxSymbolKind.Predicate); break;
                    case NodeKind.FunctionDefinition: Callable(declaration, "Function", GesSyntaxSymbolKind.Function); break;
                    case NodeKind.EventHandler: Handler(declaration); break;
                }
            }

            _found.Sort((left, right) =>
                left.Line != right.Line ? left.Line.CompareTo(right.Line) :
                left.Column != right.Column ? left.Column.CompareTo(right.Column) :
                left.Shadowing != right.Shadowing ? left.Shadowing.CompareTo(right.Shadowing) :
                left.Order.CompareTo(right.Order));
            var result = new GesSyntaxDiagnostic[_found.Count];
            for (var index = 0; index < result.Length; index++) result[index] = _found[index].Diagnostic;
            return result;
        }

        private string ModuleName(GesSyntaxNode node)
        {
            var name = new StringBuilder();
            for (var token = node.FirstToken; token < node.EndToken; token++)
                if (_tokens[token].Kind is Identifier or Dot) name.Append(_tokens[token].Text);
            return name.Length == 0 ? "UnknownModule" : name.ToString();
        }

        // Diagnostics ---------------------------------------------------------------------------------------------

        private void Report(string code, string message, string? symbol, GesSyntaxSymbolKind kind, (int Start, int End) anchor, bool shadowing = false)
        {
            // A node without a range of its own reports at the whole source.
            if (anchor.Start < 0) anchor = (0, _tokens.Count > 1 ? _tokens.Count - 2 : 0);
            var start = _tokens[anchor.Start];
            var end = _tokens[anchor.End];
            var diagnostic = new GesSyntaxDiagnostic(code, message, start.Line, start.Column, end.EndLine, end.EndColumn, _module, string.IsNullOrEmpty(symbol) ? null : symbol, kind);
            _found.Add((diagnostic, start.Line, start.Column, shadowing, _found.Count));
        }

        private void IdentifierCase(string name, string message, string symbol, GesSyntaxSymbolKind kind, (int, int) anchor)
        {
            if (!GesText.IsIdentifier(name)) Report(InvalidIdentifierCase, message, symbol, kind, anchor);
        }

        private void CallableCase(string name, string message, string symbol, GesSyntaxSymbolKind kind, (int, int) anchor)
        {
            if (!GesText.IsCallableName(name)) Report(InvalidIdentifierCase, message, symbol, kind, anchor);
        }

        private void LabelCase(string name, string message, string symbol, GesSyntaxSymbolKind kind, (int, int) anchor)
        {
            if (!GesText.IsArgumentLabel(name)) Report(InvalidIdentifierCase, message, symbol, kind, anchor);
        }

        private void MessageCase(string name, string message, string symbol, GesSyntaxSymbolKind kind, (int, int) anchor)
        {
            if (!GesText.IsMessageName(name)) Report(InvalidMessageCase, message, symbol, kind, anchor);
        }

        private void Shadow(string name, (int, int) anchor)
            => Report(ShadowedVariable, $"Binding '{name}' shadows a binding from an enclosing scope", name, GesSyntaxSymbolKind.Variable, anchor, shadowing: true);

        private void RemovedType(string typeName, (int, int) anchor)
        {
            // Integer and float are numeric checks or casts, never type references.
            var message = typeName switch
            {
                "optional" => "Type ':optional' is not supported; use nothing to represent absence.",
                "set" => "Type ':set' is not supported; use lists or key-only maps.",
                "uuid" => "Type ':Uuid' is not supported; use ':Text' or ':Number' ids in scripts.",
                "Integer" or "Float" => $"Type ':{typeName}' is not a type; use 'as :Number' to convert or 'is integer' / 'is fractional' to check.",
                "ref" => "Type ':ref' is not supported.",
                _ => null,
            };
            if (message is not null) Report(InvalidTypeConstructor, message, typeName, GesSyntaxSymbolKind.Type, anchor);
        }

        // Tree access ---------------------------------------------------------------------------------------------

        private string Text(int token) => _tokens[token].Text;

        // The tokens of a node from its first to its last.
        private static (int, int) Span(GesSyntaxNode node) => (node.FirstToken, node.EndToken - 1);

        // The diagnostic range of an expression node.
        private static (int, int) Range(GesSyntaxNode node) => (node.RangeStart, node.RangeEnd);

        private int OwnToken(GesSyntaxNode node, GesSyntaxRole role)
        {
            var child = 0;
            for (var token = node.FirstToken; token < node.EndToken; token++)
            {
                while (child < node.Children.Count && node.Children[child].EndToken <= token) child++;
                if (child < node.Children.Count && node.Children[child].FirstToken <= token) continue;
                if (_roles[token] == role) return token;
            }

            return -1;
        }

        private List<int> OwnTokens(GesSyntaxNode node, GesSyntaxRole role)
        {
            var result = new List<int>();
            var child = 0;
            for (var token = node.FirstToken; token < node.EndToken; token++)
            {
                while (child < node.Children.Count && node.Children[child].EndToken <= token) child++;
                if (child < node.Children.Count && node.Children[child].FirstToken <= token) continue;
                if (_roles[token] == role) result.Add(token);
            }

            return result;
        }

        private static GesSyntaxNode? Child(GesSyntaxNode node, NodeKind kind)
        {
            foreach (var child in node.Children)
                if (child.Kind == kind) return child;
            return null;
        }

        private static bool IsExpression(GesSyntaxNode node)
            => node.Kind is not (NodeKind.TypeReference or NodeKind.Argument or NodeKind.TagClause or NodeKind.ChoiceBranch or NodeKind.CollectionSelector or NodeKind.MapEntry or
                NodeKind.ObjectPattern or NodeKind.ObjectPatternEntry or NodeKind.DicePattern or NodeKind.Parameter or NodeKind.ParameterList or NodeKind.Block or NodeKind.Error);

        // Declarations --------------------------------------------------------------------------------------------

        private void Record(GesSyntaxNode node)
        {
            var typeName = GesText.CanonicalTypeName(Text(Child(node, NodeKind.TypeReference)!.FirstToken)[1..]);
            RemovedType(typeName, Span(node));
            var fields = new List<(GesSyntaxNode Node, string Name)>();
            var scope = new Scope();
            foreach (var field in node.Children)
            {
                if (field.Kind != NodeKind.RecordField) continue;
                var nameToken = OwnToken(field, GesSyntaxRole.Field);
                if (nameToken < 0) nameToken = OwnToken(field, GesSyntaxRole.UnlabeledField);
                fields.Add((field, Text(nameToken)));
                scope.Declare(Text(nameToken));
            }

            for (var index = 0; index < fields.Count; index++)
            {
                var (field, name) = fields[index];
                LabelCase(name, "Field names must start with a lowercase ASCII letter and contain only ASCII letters or digits", name, GesSyntaxSymbolKind.Variable, Span(field));
                for (var earlier = 0; earlier < index; earlier++)
                {
                    if (fields[earlier].Name != name) continue;
                    Report(DuplicateVariable, $"Record ':{typeName}' declares field '{name}' more than once", name, GesSyntaxSymbolKind.Variable, Span(field));
                    break;
                }

                Elements(field, scope, validate: true, shadow: true);
            }
        }

        private List<(GesSyntaxNode Node, int Name, bool Labeled, bool MessageName)> Parameters(GesSyntaxNode? list)
        {
            var result = new List<(GesSyntaxNode, int, bool, bool)>();
            if (list is null) return result;
            foreach (var parameter in list.Children)
            {
                if (parameter.Kind != NodeKind.Parameter) continue;
                var name = OwnToken(parameter, GesSyntaxRole.Parameter);
                if (name >= 0) result.Add((parameter, name, true, false));
                else result.Add((parameter, OwnToken(parameter, GesSyntaxRole.UnlabeledParameter), false, false));
            }

            return result;
        }

        private void CheckParameters(List<(GesSyntaxNode Node, int Name, bool Labeled, bool MessageName)> parameters, string kind, string name, GesSyntaxSymbolKind symbolKind, string duplicateCode)
        {
            foreach (var (parameter, nameToken, labeled, messageName) in parameters)
            {
                var local = Text(nameToken);
                var anchor = messageName ? (nameToken, parameter.EndToken - 1) : Span(parameter);
                IdentifierCase(local, $"{kind} '{name}' declares an invalid parameter name '{local}'", name, symbolKind, anchor);
                if (labeled) LabelCase(local, $"{kind} '{name}' declares an invalid external parameter label '{local}'", name, symbolKind, anchor);
            }

            var seen = new HashSet<string>(StringComparer.Ordinal);
            var reported = new HashSet<string>(StringComparer.Ordinal);
            foreach (var (parameter, nameToken, _, messageName) in parameters)
            {
                var local = Text(nameToken);
                if (seen.Add(local) || !reported.Add(local)) continue;
                Report(duplicateCode, $"{kind} '{name}' declares parameter '{local}' more than once", name, symbolKind, messageName ? (nameToken, parameter.EndToken - 1) : Span(parameter));
            }
        }

        private void Callable(GesSyntaxNode node, string kind, GesSyntaxSymbolKind symbolKind)
        {
            var name = Text(OwnToken(node, GesSyntaxRole.Callable));
            CallableCase(name, $"{kind} names must start with a lowercase ASCII letter and contain only ASCII letters or digits", name, symbolKind, Span(node));
            var parameters = Parameters(Child(node, NodeKind.ParameterList));
            CheckParameters(parameters, kind, name, symbolKind, DuplicateDefinitionParameter);
            var scope = new Scope();
            foreach (var parameter in parameters) scope.Declare(Text(parameter.Name));
            Elements(node, scope, validate: true, shadow: true);
        }

        private void Handler(GesSyntaxNode node)
        {
            var message = Text(OwnToken(node, GesSyntaxRole.MessageName));
            var asParameter = Child(node, NodeKind.Parameter);
            var parameters = asParameter is null ? Parameters(Child(node, NodeKind.ParameterList)) : [(asParameter, OwnToken(asParameter, GesSyntaxRole.Parameter), false, true)];
            var hasTags = Child(node, NodeKind.HandlerTagFilter) is not null;
            if (message == "initialization")
            {
                if (asParameter is not null || parameters.Count != 0)
                    Report(InvalidMessageCase, "System endpoint 'initialization' must use parameterless syntax", message, GesSyntaxSymbolKind.Handler, Span(node));
                else if (hasTags)
                    Report(InvalidMessageCase, "System endpoint 'initialization' cannot use tag filters", message, GesSyntaxSymbolKind.Handler, Span(node));
            }
            else if (message == "undeliverable")
            {
                if (asParameter is null) Report(InvalidMessageCase, "System endpoint 'undeliverable' must use 'as message' syntax", message, GesSyntaxSymbolKind.Handler, Span(node));
            }
            else
            {
                MessageCase(message, "Handler message names must start uppercase and contain only ASCII letters or digits", message, GesSyntaxSymbolKind.Handler, Span(node));
            }

            CheckParameters(parameters, "Handler", message, GesSyntaxSymbolKind.Handler, DuplicateHandlerParameter);
            var validationScope = new Scope();
            var shadowScope = new Scope();
            foreach (var parameter in parameters)
            {
                validationScope.Declare(Text(parameter.Name));
                shadowScope.Declare(Text(parameter.Name));
            }

            Statements(Child(node, NodeKind.Block)!, validationScope, shadowScope);
        }

        // Statements ----------------------------------------------------------------------------------------------

        private void Statements(GesSyntaxNode block, Scope validation, Scope shadow)
        {
            foreach (var statement in block.Children) Statement(statement, validation, shadow);
        }

        // A body, braced or a single statement, opens a scope of its own.
        private void Body(GesSyntaxNode body, Scope validation, Scope shadow)
        {
            if (body.Kind == NodeKind.Block) Statements(body, new Scope(validation), new Scope(shadow));
            else Statement(body, new Scope(validation), new Scope(shadow));
        }

        private void Statement(GesSyntaxNode node, Scope validation, Scope shadow)
        {
            switch (node.Kind)
            {
                case NodeKind.EmitStatement or NodeKind.PublishStatement or NodeKind.ExpressionStatement:
                    Elements(node, shadow, validate: true, shadow: true);
                    return;
                case NodeKind.AssemblyBlock:
                    Assembly(node, validation, shadow);
                    return;
                case NodeKind.LetStatement:
                    Elements(node, shadow, validate: true, shadow: true);
                    Binding(Text(OwnToken(node, GesSyntaxRole.LetBinding)), Span(node), validation, shadow);
                    return;
                case NodeKind.IfStatement:
                    If(node, validation, shadow);
                    return;
                case NodeKind.ForStatement:
                    For(node, validation, shadow, nested: false);
                    return;
                case NodeKind.SeededRandomStatement:
                    // The seed comes before the body, which is the last element.
                    var body = node.Children[node.Children.Count - 1];
                    Seed(node, body, shadow);
                    Body(body, validation, shadow);
                    return;
            }
        }

        // The seed of a seeded random statement: every element before its body.
        private void Seed(GesSyntaxNode node, GesSyntaxNode body, Scope shadow)
        {
            var child = 0;
            for (var token = node.FirstToken; token < body.FirstToken; token++)
            {
                if (child < node.Children.Count && node.Children[child].FirstToken == token)
                {
                    var seed = node.Children[child++];
                    if (IsExpression(seed)) Expression(seed, shadow, validate: true, shadow: true);
                    token = seed.EndToken - 1;
                    continue;
                }

                Token(token);
            }
        }

        // A let binding: its name, a repetition within its scope and the shadowing of an enclosing binding.
        private void Binding(string name, (int, int) anchor, Scope validation, Scope shadow)
        {
            IdentifierCase(name, $"Variable '{name}' must start lowercase, contain ASCII letters or digits, and may use one canonical final _index suffix", name, GesSyntaxSymbolKind.Variable, anchor);
            if (validation.ContainsCurrent(name))
                Report(DuplicateVariable, $"Variable '{name}' is already declared in the current scope", name, GesSyntaxSymbolKind.Variable, anchor);
            else validation.Declare(name);
            if (shadow.ContainsInAncestor(name)) Shadow(name, anchor);
            shadow.Declare(name);
        }

        private void Assembly(GesSyntaxNode node, Scope validation, Scope shadow)
        {
            Elements(node, shadow, validate: true, shadow: false);
            for (var token = node.FirstToken; token < node.EndToken; token++)
            {
                if (_roles[token] != GesSyntaxRole.AssemblyDeclaration) continue;
                var line = token;
                while (_tokens[line].Kind is not (Let or Dot)) line--;
                var name = Text(token);
                var anchor = (line, token);
                if (shadow.ContainsVisible(name)) Shadow(name, anchor);
                if (_tokens[line].Kind != Let) continue;
                shadow.Declare(name);
                IdentifierCase(name, $"Variable '{name}' must start lowercase, contain ASCII letters or digits, and may use one canonical final _index suffix", name, GesSyntaxSymbolKind.Variable, anchor);
                if (validation.ContainsCurrent(name))
                    Report(DuplicateVariable, $"Variable '{name}' is already declared in the current scope", name, GesSyntaxSymbolKind.Variable, anchor);
                else validation.Declare(name);
            }
        }

        private void If(GesSyntaxNode node, Scope validation, Scope shadow)
        {
            var conditionValidation = new Scope(validation);
            var conditionShadow = new Scope(shadow);
            foreach (var child in node.Children)
            {
                switch (child.Kind)
                {
                    case NodeKind.IfCondition:
                        Elements(child, conditionShadow, validate: true, shadow: true);
                        var binding = OwnToken(child, GesSyntaxRole.ConditionBinding);
                        if (binding >= 0) Binding(Text(binding), Span(child), conditionValidation, conditionShadow);
                        break;
                    case NodeKind.Block: Statements(child, conditionValidation, conditionShadow); break;
                    case NodeKind.ElseClause:
                        var elseValidation = new Scope(validation);
                        var elseShadow = new Scope(shadow);
                        foreach (var body in child.Children)
                        {
                            if (body.Kind == NodeKind.Block) Statements(body, elseValidation, elseShadow);
                            else Statement(body, elseValidation, elseShadow);
                        }

                        break;
                    default: Statement(child, conditionValidation, conditionShadow); break;
                }
            }
        }

        private void For(GesSyntaxNode node, Scope validation, Scope shadow, bool nested)
        {
            var names = OwnTokens(node, GesSyntaxRole.LoopBinding);
            var start = node.FirstToken;
            // A nested clause ranges from the last line break after its `and`.
            if (nested)
                while (_tokens[start + 1].Kind == NewLine) start++;
            var anchor = (start, node.EndToken - 1);
            GesSyntaxNode? bodyNode = null;
            var child = 0;
            var afterSource = false;
            for (var token = node.FirstToken; token < node.EndToken; token++)
            {
                if (child < node.Children.Count && node.Children[child].FirstToken == token)
                {
                    var element = node.Children[child++];
                    token = element.EndToken - 1;
                    if (afterSource)
                    {
                        bodyNode = element;
                        continue;
                    }

                    if (element.Kind == NodeKind.RangeExpression || IsExpression(element))
                    {
                        Expression(element, shadow, validate: true, shadow: true);
                        afterSource = true;
                    }

                    continue;
                }

                if (_roles[token] == GesSyntaxRole.Variable && !afterSource)
                {
                    Token(token);
                    afterSource = true;
                }
            }

            var identifier = Text(names[0]);
            IdentifierCase(identifier, $"Loop variable '{identifier}' must start lowercase, contain ASCII letters or digits, and may use one canonical final _index suffix", identifier, GesSyntaxSymbolKind.Variable, anchor);
            var loopValidation = new Scope(validation);
            loopValidation.Declare(identifier);
            var loopShadow = BinderScope(shadow, names, anchor);
            if (bodyNode is null) return;
            if (bodyNode.Kind == NodeKind.ForStatement) For(bodyNode, loopValidation, loopShadow, nested: true);
            else Body(bodyNode, loopValidation, loopShadow);
        }

        // A binder scope: one name, or several whose first is the element and the rest its components.
        private Scope BinderScope(Scope parent, List<int> names, (int, int) anchor)
        {
            var child = new Scope(parent);
            if (names.Count < 2)
            {
                var name = Text(names[0]);
                if (parent.ContainsVisible(name)) Shadow(name, anchor);
                child.Declare(name);
                return child;
            }

            var first = Text(names[0]);
            for (var index = 0; index < names.Count; index++)
            {
                var name = Text(names[index]);
                if (name != first && !GesText.IsVariableName(name))
                    Report(InvalidIdentifierCase, "Invalid component binding name", name, GesSyntaxSymbolKind.Variable, anchor, shadowing: true);
                if (parent.ContainsVisible(name)) Shadow(name, anchor);
                if (child.ContainsVisible(name) && !parent.ContainsVisible(name))
                    Report(DuplicateVariable, "Bindings must be distinct", name, GesSyntaxSymbolKind.Variable, anchor, shadowing: true);
                child.Declare(name);
            }

            return child;
        }

        private Scope BinderScope(Scope parent, string name, (int, int) anchor)
        {
            var child = new Scope(parent);
            if (parent.ContainsVisible(name)) Shadow(name, anchor);
            child.Declare(name);
            return child;
        }

        // Expressions ---------------------------------------------------------------------------------------------

        private void Token(int token)
        {
            if (_roles[token] != GesSyntaxRole.Variable) return;
            var name = Text(token);
            IdentifierCase(name, $"Identifier '{name}' must start lowercase, contain ASCII letters or digits, and may use one canonical final _index suffix", name, GesSyntaxSymbolKind.Variable, (token, token));
        }

        // The expressions among a node's elements: its name tokens and its expression children.
        private void Elements(GesSyntaxNode node, Scope scope, bool validate, bool shadow)
        {
            var child = 0;
            for (var token = node.FirstToken; token < node.EndToken; token++)
            {
                if (child < node.Children.Count && node.Children[child].FirstToken == token)
                {
                    var element = node.Children[child++];
                    token = element.EndToken - 1;
                    if (element.Kind is NodeKind.TagClause or NodeKind.ChoiceBranch or NodeKind.Argument) Elements(element, scope, validate, shadow);
                    else if (IsExpression(element)) Expression(element, scope, validate, shadow);
                    continue;
                }

                if (validate) Token(token);
            }
        }

        private void Expression(GesSyntaxNode node, Scope scope, bool validate, bool shadow)
        {
            switch (node.Kind)
            {
                case NodeKind.AssemblyBlock:
                    if (validate) Elements(node, scope, validate: true, shadow: false);
                    return;
                case NodeKind.CallExpression:
                    if (validate)
                    {
                        var name = Text(OwnToken(node, GesSyntaxRole.CallTarget));
                        CallableCase(name, $"Call target '{name}' must use identifier casing (predicate/function names start lowercase)", name, GesSyntaxSymbolKind.GlobalDefinition, Range(node));
                        Arguments(node, name, GesSyntaxSymbolKind.GlobalDefinition, label => $"Call argument label '{label}' must use identifier casing", scope, shadow);
                    }
                    else Elements(node, scope, false, shadow);

                    return;
                case NodeKind.MessageExpression:
                    if (validate)
                    {
                        var message = Text(OwnToken(node, GesSyntaxRole.MessageName));
                        MessageCase(message, $"Message literal '{message}' must start uppercase and contain only ASCII letters or digits", message, GesSyntaxSymbolKind.Message, Range(node));
                        Arguments(node, message, GesSyntaxSymbolKind.Message, label => $"Message literal '{message}' declares invalid argument name '{label}'", scope, shadow);
                    }
                    else Elements(node, scope, false, shadow);

                    return;
                case NodeKind.HandlerLiteralExpression:
                    if (validate) HandlerLiteral(Text(OwnToken(node, GesSyntaxRole.MessageName)), Range(node), Parameters(node), ranged: true);
                    return;
                case NodeKind.TypeConstructorExpression:
                    TypeConstructor(node, scope, validate, shadow);
                    return;
                case NodeKind.TypeOperationExpression:
                    TypeOperation(node, scope, validate, shadow);
                    return;
                case NodeKind.MemberAccessExpression:
                    if (validate)
                    {
                        var member = Text(OwnToken(node, GesSyntaxRole.Member));
                        LabelCase(member, $"Member name '{member}' must start with a lowercase ASCII letter and contain only ASCII letters or digits", member, GesSyntaxSymbolKind.Variable, Range(node));
                    }

                    Elements(node, scope, validate, shadow);
                    return;
                case NodeKind.CollectionAccessExpression:
                    CollectionAccess(node, scope, validate, shadow);
                    return;
                case NodeKind.MapLiteralExpression:
                    foreach (var entry in node.Children)
                    {
                        if (entry.Kind != NodeKind.MapEntry) continue;
                        if (validate)
                        {
                            var key = Text(OwnToken(entry, GesSyntaxRole.MapKey));
                            LabelCase(key, $"Map key '{key}' must start with a lowercase ASCII letter and contain only ASCII letters or digits", key, GesSyntaxSymbolKind.Variable, Range(node));
                        }

                        Elements(entry, scope, validate, shadow);
                    }

                    return;
                case NodeKind.CollectionFactoryExpression:
                    Generated(node, scope, validate, shadow);
                    return;
                case NodeKind.SeededRandomExpression:
                    // The body has a scope of its own for shadowing.
                    var seeded = false;
                    var child = 0;
                    for (var token = node.FirstToken; token < node.EndToken; token++)
                    {
                        if (child < node.Children.Count && node.Children[child].FirstToken == token)
                        {
                            var element = node.Children[child++];
                            token = element.EndToken - 1;
                            if (IsExpression(element)) Expression(element, seeded ? new Scope(scope) : scope, validate, shadow);
                            seeded = true;
                            continue;
                        }

                        if (_roles[token] != GesSyntaxRole.Variable) continue;
                        if (validate) Token(token);
                        seeded = true;
                    }

                    return;
                default:
                    Elements(node, scope, validate, shadow);
                    return;
            }
        }

        // Call and message arguments: repeated labels, label spellings and the argument values.
        private void Arguments(GesSyntaxNode node, string symbol, GesSyntaxSymbolKind labelKind, Func<string, string> labelMessage, Scope scope, bool shadow)
        {
            var arguments = new List<(GesSyntaxNode Node, int Label)>();
            foreach (var child in node.Children)
                if (child.Kind == NodeKind.Argument) arguments.Add((child, OwnToken(child, GesSyntaxRole.ArgumentLabel)));
            DuplicateArguments(arguments, symbol);
            var argument = 0;
            var position = 0;
            for (var token = node.FirstToken; token < node.EndToken; token++)
            {
                if (position < node.Children.Count && node.Children[position].FirstToken == token)
                {
                    var element = node.Children[position++];
                    token = element.EndToken - 1;
                    if (element.Kind == NodeKind.Argument)
                    {
                        var label = arguments[argument++].Label;
                        if (label >= 0) LabelCase(Text(label), labelMessage(Text(label)), symbol, labelKind, Span(element));
                        Elements(element, scope, validate: true, shadow);
                    }
                    else if (element.Kind == NodeKind.TagClause) Elements(element, scope, validate: true, shadow);
                    else if (IsExpression(element)) Expression(element, scope, validate: true, shadow);
                    continue;
                }

                Token(token);
            }
        }

        private void DuplicateArguments(List<(GesSyntaxNode Node, int Label)> arguments, string symbol)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var reported = new HashSet<string>(StringComparer.Ordinal);
            foreach (var (argument, label) in arguments)
            {
                if (label < 0 || seen.Add(Text(label)) || !reported.Add(Text(label))) continue;
                Report(DuplicatePublishArgument, $"Named argument '{Text(label)}' is declared more than once", symbol, GesSyntaxSymbolKind.Handler, Span(argument));
            }
        }

        // A handler literal of the `:Handler` data form has no parameter nodes; its diagnostics point at the names.
        private void HandlerLiteral(string message, (int, int) anchor, List<(GesSyntaxNode Node, int Name, bool Labeled, bool MessageName)> parameters, bool ranged)
        {
            MessageCase(message, $"Handler literal '{message}' must start uppercase and contain only ASCII letters or digits", message, GesSyntaxSymbolKind.Handler, anchor);
            foreach (var parameter in parameters)
            {
                var name = Text(parameter.Name);
                LabelCase(name, $"Handler literal '{message}' declares invalid parameter '{name}'", message, GesSyntaxSymbolKind.Handler, ranged ? anchor : (parameter.Name, parameter.Name));
            }

            var seen = new HashSet<string>(StringComparer.Ordinal);
            var reported = new HashSet<string>(StringComparer.Ordinal);
            foreach (var parameter in parameters)
            {
                var name = Text(parameter.Name);
                if (seen.Add(name) || !reported.Add(name)) continue;
                Report(DuplicateHandlerParameter, $"Handler literal '{message}' declares parameter '{name}' more than once", message, GesSyntaxSymbolKind.Handler, ranged ? Span(parameter.Node) : (parameter.Name, parameter.Name));
            }
        }

        private void TypeConstructor(GesSyntaxNode node, Scope scope, bool validate, bool shadow)
        {
            var typeName = GesText.CanonicalTypeName(Text(node.FirstToken)[1..]);
            var messageToken = OwnToken(node, GesSyntaxRole.MessageName);
            if (typeName == "handler" && messageToken >= 0)
            {
                if (!validate) return;
                var parameters = new List<(GesSyntaxNode, int, bool, bool)>();
                foreach (var name in OwnTokens(node, GesSyntaxRole.Parameter)) parameters.Add((node, name, true, false));
                HandlerLiteral(Text(messageToken), (messageToken, messageToken), parameters, ranged: false);
                return;
            }

            if (!validate)
            {
                Elements(node, scope, false, shadow);
                return;
            }

            Arguments(node, typeName, GesSyntaxSymbolKind.Type, label => $"Type constructor ':{typeName}' declares invalid argument label '{label}'", scope, shadow);
        }

        private void TypeOperation(GesSyntaxNode node, Scope scope, bool validate, bool shadow)
        {
            if (validate)
            {
                var target = OwnToken(node, GesSyntaxRole.PredicateTarget);
                if (target >= 0)
                {
                    var name = Text(target);
                    CallableCase(name, $"Predicate test target '{name}' must use identifier casing (start lowercase)", name, GesSyntaxSymbolKind.Predicate, Range(node));
                }
                else if (Child(node, NodeKind.TypeReference) is { } type)
                {
                    RemovedType(GesText.CanonicalTypeName(Text(type.FirstToken)[1..]), Range(node));
                }
            }

            Elements(node, scope, validate, shadow);
        }

        private void Generated(GesSyntaxNode node, Scope scope, bool validate, bool shadow)
        {
            var binding = OwnToken(node, GesSyntaxRole.GeneratedBinding);
            var name = Text(binding);
            if (validate) IdentifierCase(name, $"Generated collection identifier '{name}' must use identifier casing (start lowercase)", name, GesSyntaxSymbolKind.Variable, Range(node));
            // The source comes before `where` or the projection; the binding scopes the rest.
            var child = 0;
            Scope? inner = null;
            for (var token = node.FirstToken; token < node.EndToken; token++)
            {
                if (child < node.Children.Count && node.Children[child].FirstToken == token)
                {
                    var element = node.Children[child++];
                    token = element.EndToken - 1;
                    if (IsExpression(element)) Expression(element, inner ?? scope, validate, shadow);
                    inner ??= shadow ? BinderScope(scope, name, Range(node)) : new Scope(scope);
                    continue;
                }

                if (_roles[token] != GesSyntaxRole.Variable) continue;
                if (validate) Token(token);
                inner ??= shadow ? BinderScope(scope, name, Range(node)) : new Scope(scope);
            }
        }

        private void CollectionAccess(GesSyntaxNode node, Scope scope, bool validate, bool shadow)
        {
            var child = 0;
            for (var token = node.FirstToken; token < node.EndToken; token++)
            {
                if (child < node.Children.Count && node.Children[child].FirstToken == token)
                {
                    var element = node.Children[child++];
                    token = element.EndToken - 1;
                    if (element.Kind == NodeKind.CollectionSelector) SelectorStep(element, scope, validate, shadow);
                    else if (IsExpression(element)) Expression(element, scope, validate, shadow);
                    continue;
                }

                if (validate) Token(token);
            }
        }

        private void SelectorStep(GesSyntaxNode node, Scope scope, bool validate, bool shadow)
        {
            var keyword = _tokens[node.FirstToken];
            var anchor = Span(node);
            var bindings = OwnTokens(node, GesSyntaxRole.SelectorBinding);
            var weights = OwnTokens(node, GesSyntaxRole.WeightBinding);
            var accumulator = OwnToken(node, GesSyntaxRole.FoldAccumulator);
            var operation = keyword.Kind is Selector or SelectorAny or SelectorAll or SelectorFilter or SelectorHas or SelectorTake or SelectorDrop or SelectorCount or SelectorChoose or
                SelectorDraw or SelectorShuffle or SelectorReverse or SelectorSum or SelectorAverage or SelectorSelect or SelectorContains or SelectorSort
                ? keyword.Text
                : "";

            if (operation is ":split" || IsCombined(operation))
            {
                Elements(node, scope, validate, shadow);
                return;
            }

            if (operation is ":has" or ":take" or ":drop" or ":draw" or ":shuffle" or ":reverse" or ":sort")
            {
                // Patterns are no references; their face values and object values only meet the shadowing rule.
                if (shadow) PatternValues(node, scope);
                return;
            }

            if (operation is ":fold" or ":reduce")
            {
                var accumulatorName = Text(accumulator);
                var elementName = Text(bindings[0]);
                if (validate)
                {
                    IdentifierCase(accumulatorName, "Accumulator must use identifier casing", accumulatorName, GesSyntaxSymbolKind.Variable, anchor);
                    IdentifierCase(elementName, "Element must use identifier casing", elementName, GesSyntaxSymbolKind.Variable, anchor);
                }

                // The seed comes before the bindings; the projection is in their scope.
                var foldScope = scope;
                var bound = false;
                var position = 0;
                for (var token = node.FirstToken; token < node.EndToken; token++)
                {
                    if (position < node.Children.Count && node.Children[position].FirstToken == token)
                    {
                        var element = node.Children[position++];
                        token = element.EndToken - 1;
                        if (IsExpression(element)) Expression(element, foldScope, validate, shadow);
                        continue;
                    }

                    if (_roles[token] == GesSyntaxRole.SelectorBinding && !bound)
                    {
                        bound = true;
                        if (!shadow) continue;
                        foldScope = bindings.Count > 1 && elementName == accumulatorName ? BinderScope(scope, bindings, anchor) : BinderScope(scope, accumulatorName, anchor);
                        if (accumulatorName == elementName)
                            Report(DuplicateVariable, "Fold bindings must be distinct", elementName, GesSyntaxSymbolKind.Variable, anchor, shadowing: true);
                        else foldScope = BinderScope(foldScope, bindings, anchor);
                        continue;
                    }

                    if (validate) Token(token);
                }

                return;
            }

            if (bindings.Count == 0 && weights.Count == 0)
            {
                Elements(node, scope, validate, shadow);
                return;
            }

            var weightStart = weights.Count == 0 ? node.EndToken : weights[0];
            if (bindings.Count > 0)
            {
                var first = Text(bindings[0]);
                if (validate && operation != ":foreach")
                    IdentifierCase(first, $"Selector identifier '{first}' must use identifier casing (start lowercase)", first, GesSyntaxSymbolKind.Variable, anchor);
                var inner = shadow ? BinderScope(scope, bindings, anchor) : new Scope(scope);
                ElementsBetween(node, bindings[bindings.Count - 1], weightStart, inner, validate, shadow);
            }

            if (weights.Count == 0) return;

            // A weighted choice: the weight in a binding scope of its own.
            var weightName = Text(weights[0]);
            if (validate) IdentifierCase(weightName, $"Selector weight identifier '{weightName}' must use identifier casing (start lowercase)", weightName, GesSyntaxSymbolKind.Variable, anchor);
            var weightScope = shadow ? BinderScope(scope, weights, anchor) : new Scope(scope);
            ElementsAfter(node, weights[weights.Count - 1], weightScope, validate, shadow);
        }

        private static bool IsCombined(string operation) => operation is ":cartesian" or ":lockstep" or ":zip" or ":union" or ":intersect" or ":difference";

        private void ElementsAfter(GesSyntaxNode node, int after, Scope scope, bool validate, bool shadow) => ElementsBetween(node, after, node.EndToken, scope, validate, shadow);

        // The expressions among the elements after token `after` and before token `before`.
        private void ElementsBetween(GesSyntaxNode node, int after, int before, Scope scope, bool validate, bool shadow)
        {
            var child = 0;
            for (var token = node.FirstToken; token < Math.Min(before, node.EndToken); token++)
            {
                if (child < node.Children.Count && node.Children[child].FirstToken == token)
                {
                    var element = node.Children[child++];
                    token = element.EndToken - 1;
                    if (element.FirstToken > after && IsExpression(element)) Expression(element, scope, validate, shadow);
                    continue;
                }

                if (token > after && validate) Token(token);
            }
        }

        private void PatternValues(GesSyntaxNode node, Scope scope)
        {
            foreach (var child in node.Children)
            {
                switch (child.Kind)
                {
                    case NodeKind.DicePattern: Elements(child, scope, validate: false, shadow: true); break;
                    case NodeKind.ObjectPatternEntry:
                        Elements(child, scope, validate: false, shadow: true);
                        PatternValues(child, scope);
                        break;
                    case NodeKind.ObjectPattern: PatternValues(child, scope); break;
                }
            }
        }
    }
}
