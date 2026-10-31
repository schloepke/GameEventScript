// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

/// Checks the rules of one source that need no other source, catalog or runtime: the spelling of names and labels,
/// repeated parameters, arguments, fields and bindings, shadowing, the forms of system endpoints and removed type names.
/// The diagnostics come in source order; at one position, shadowing follows the other rules, which keep their check
/// order.
@_spi(Syntax) public enum GesSyntaxValidator {
    static let invalidIdentifierCase = "validate.invalidIdentifierCase"
    static let invalidMessageCase = "validate.invalidMessageCase"
    static let invalidTypeConstructor = "validate.invalidTypeConstructor"
    static let duplicateVariable = "validate.duplicateVariable"
    static let shadowedVariable = "validate.shadowedVariable"
    static let duplicateDefinitionParameter = "validate.duplicateDefinitionParameter"
    static let duplicateHandlerParameter = "validate.duplicateHandlerParameter"
    static let duplicatePublishArgument = "validate.duplicatePublishArgument"

    /// The validation diagnostics of a source without syntax errors, in source order.
    @_spi(Syntax) public static func validate(_ tree: GesSyntaxTree) -> [GesSyntaxDiagnostic] { Walker(tree).run() }

    // A scope of the binding rules: a let binding repeats only within its own scope, and a binding shadows any visible one.
    final class Scope {
        let parent: Scope?
        private var names: Set<String> = []

        init(_ parent: Scope? = nil) { self.parent = parent }

        func declare(_ name: String) { names.insert(name) }

        func containsCurrent(_ name: String) -> Bool { names.contains(name) }

        func containsVisible(_ name: String) -> Bool {
            var scope: Scope? = self
            while let current = scope {
                if current.names.contains(name) { return true }
                scope = current.parent
            }
            return false
        }

        func containsInAncestor(_ name: String) -> Bool { parent?.containsVisible(name) == true }
    }

    typealias Anchor = (start: Int, end: Int)
    typealias Parameter = (node: GesSyntaxNode, name: Int, labeled: Bool, messageName: Bool)

    // Walks the declarations of one tree; the recursion over expressions stays in small functions.
    final class Walker {
        let tree: GesSyntaxTree
        let tokens: [GesToken]
        let roles: [GesSyntaxRole]
        var found: [(diagnostic: GesSyntaxDiagnostic, line: Int, column: Int, shadowing: Bool, order: Int)] = []
        var module = ""

        init(_ tree: GesSyntaxTree) {
            self.tree = tree
            tokens = tree.tokens
            roles = tree.roles
        }

        func run() -> [GesSyntaxDiagnostic] {
            for declaration in tree.root.children where declaration.kind == .moduleDeclaration { module = moduleName(declaration) }
            for declaration in tree.root.children {
                switch declaration.kind {
                case .recordDefinition: record(declaration)
                case .predicateDefinition: callable(declaration, "Predicate", .predicate)
                case .functionDefinition: callable(declaration, "Function", .function)
                case .eventHandler: handler(declaration)
                default: break
                }
            }
            found.sort {
                if $0.line != $1.line { return $0.line < $1.line }
                if $0.column != $1.column { return $0.column < $1.column }
                if $0.shadowing != $1.shadowing { return !$0.shadowing }
                return $0.order < $1.order
            }
            return found.map(\.diagnostic)
        }

        func moduleName(_ node: GesSyntaxNode) -> String {
            var name = ""
            for token in node.firstToken..<node.endToken where tokens[token].kind == .identifier || tokens[token].kind == .dot { name += tokens[token].text }
            return name
        }

        // MARK: Diagnostics

        func report(_ code: String, _ message: String, _ symbol: String?, _ kind: GesSyntaxSymbolKind, _ anchor: Anchor, shadowing: Bool = false) {
            // A node without a range of its own reports at the whole source.
            let anchor: Anchor = anchor.start < 0 ? (0, tokens.count > 1 ? tokens.count - 2 : 0) : anchor
            let start = tokens[anchor.start]
            let end = tokens[anchor.end]
            let diagnostic = GesSyntaxDiagnostic(
                code: code,
                message: message,
                line: start.line,
                column: start.column,
                endLine: end.endLine,
                endColumn: end.endColumn,
                moduleName: module,
                symbol: symbol?.isEmpty == false ? symbol : nil,
                symbolKind: kind
            )
            found.append((diagnostic, start.line, start.column, shadowing, found.count))
        }

        func identifierCase(_ name: String, _ message: String, _ symbol: String, _ kind: GesSyntaxSymbolKind, _ anchor: Anchor) {
            if !GesNames.identifier(name) { report(invalidIdentifierCase, message, symbol, kind, anchor) }
        }

        func callableCase(_ name: String, _ message: String, _ symbol: String, _ kind: GesSyntaxSymbolKind, _ anchor: Anchor) {
            if !GesNames.plain(name) { report(invalidIdentifierCase, message, symbol, kind, anchor) }
        }

        func labelCase(_ name: String, _ message: String, _ symbol: String, _ kind: GesSyntaxSymbolKind, _ anchor: Anchor) {
            if !GesNames.plain(name) { report(invalidIdentifierCase, message, symbol, kind, anchor) }
        }

        func messageCase(_ name: String, _ message: String, _ symbol: String, _ kind: GesSyntaxSymbolKind, _ anchor: Anchor) {
            if !GesNames.type(name) { report(invalidMessageCase, message, symbol, kind, anchor) }
        }

        func shadow(_ name: String, _ anchor: Anchor) {
            report(shadowedVariable, "Binding '\(name)' shadows a binding from an enclosing scope", name, .variable, anchor, shadowing: true)
        }

        // Integer and float are numeric checks or casts, never type references.
        func removedType(_ typeName: String, _ anchor: Anchor) {
            let message: String? =
                switch typeName {
                case "optional": "Type ':optional' is not supported; use nothing to represent absence."
                case "set": "Type ':set' is not supported; use lists or key-only maps."
                case "uuid": "Type ':Uuid' is not supported; use ':Text' or ':Number' ids in scripts."
                case "Integer", "Float": "Type ':\(typeName)' is not a type; use 'as :Number' to convert or 'is integer' / 'is fractional' to check."
                case "ref": "Type ':ref' is not supported."
                default: nil
                }
            if let message { report(invalidTypeConstructor, message, typeName, .type, anchor) }
        }

        // MARK: Tree access

        func text(_ token: Int) -> String { tokens[token].text }

        // The tokens of a node from its first to its last.
        func span(_ node: GesSyntaxNode) -> Anchor { (node.firstToken, node.endToken - 1) }

        // The diagnostic range of an expression node.
        func range(_ node: GesSyntaxNode) -> Anchor { (node.rangeStart, node.rangeEnd) }

        func ownTokens(_ node: GesSyntaxNode, _ role: GesSyntaxRole) -> [Int] {
            var result: [Int] = []
            var child = 0
            for token in node.firstToken..<node.endToken {
                while child < node.children.count && node.children[child].endToken <= token { child += 1 }
                if child < node.children.count && node.children[child].firstToken <= token { continue }
                if roles[token] == role { result.append(token) }
            }
            return result
        }

        func ownToken(_ node: GesSyntaxNode, _ role: GesSyntaxRole) -> Int { ownTokens(node, role).first ?? -1 }

        func child(_ node: GesSyntaxNode, _ kind: GesSyntaxNodeKind) -> GesSyntaxNode? { node.children.first { $0.kind == kind } }

        static let nonExpressions: Set<GesSyntaxNodeKind> = [
            .typeReference, .argument, .tagClause, .choiceBranch, .collectionSelector, .mapEntry, .objectPattern, .objectPatternEntry, .dicePattern, .parameter, .parameterList,
            .block, .error,
        ]

        func isExpression(_ node: GesSyntaxNode) -> Bool { !Self.nonExpressions.contains(node.kind) }

        // Calls `visit` with each element of a node in source order: a child node, or an own token.
        func elements(_ node: GesSyntaxNode, from first: Int? = nil, before limit: Int? = nil, _ visit: (GesSyntaxNode?, Int) -> Void) {
            var child = 0
            var token = node.firstToken
            let end = min(limit ?? node.endToken, node.endToken)
            while token < end {
                if child < node.children.count && node.children[child].firstToken == token {
                    let element = node.children[child]
                    child += 1
                    if first.map({ element.firstToken > $0 }) ?? true { visit(element, token) }
                    token = element.endToken
                    continue
                }
                if first.map({ token > $0 }) ?? true { visit(nil, token) }
                token += 1
            }
        }

        // MARK: Declarations

        func record(_ node: GesSyntaxNode) {
            let typeName = GesSyntaxValidator.canonicalTypeName(String(text(child(node, .typeReference)!.firstToken).dropFirst()))
            removedType(typeName, span(node))
            var fields: [(node: GesSyntaxNode, name: String)] = []
            let scope = Scope()
            for field in node.children where field.kind == .recordField {
                var nameToken = ownToken(field, .field)
                if nameToken < 0 { nameToken = ownToken(field, .unlabeledField) }
                fields.append((field, text(nameToken)))
                scope.declare(text(nameToken))
            }
            for (index, (field, name)) in fields.enumerated() {
                labelCase(name, "Field names must start with a lowercase ASCII letter and contain only ASCII letters or digits", name, .variable, span(field))
                if fields[..<index].contains(where: { $0.name == name }) {
                    report(duplicateVariable, "Record ':\(typeName)' declares field '\(name)' more than once", name, .variable, span(field))
                }
                elements(field, scope, validate: true, shadow: true)
            }
        }

        func parameters(_ list: GesSyntaxNode?) -> [Parameter] {
            guard let list else { return [] }
            return list.children.filter { $0.kind == .parameter }.map { parameter in
                let name = ownToken(parameter, .parameter)
                return name >= 0 ? (parameter, name, true, false) : (parameter, ownToken(parameter, .unlabeledParameter), false, false)
            }
        }

        func checkParameters(_ parameters: [Parameter], _ kind: String, _ name: String, _ symbolKind: GesSyntaxSymbolKind, _ duplicateCode: String) {
            for parameter in parameters {
                let local = text(parameter.name)
                let anchor = parameter.messageName ? (parameter.name, parameter.node.endToken - 1) : span(parameter.node)
                identifierCase(local, "\(kind) '\(name)' declares an invalid parameter name '\(local)'", name, symbolKind, anchor)
                if parameter.labeled { labelCase(local, "\(kind) '\(name)' declares an invalid external parameter label '\(local)'", name, symbolKind, anchor) }
            }
            var seen: Set<String> = []
            var reported: Set<String> = []
            for parameter in parameters {
                let local = text(parameter.name)
                if seen.insert(local).inserted || !reported.insert(local).inserted { continue }
                report(duplicateCode, "\(kind) '\(name)' declares parameter '\(local)' more than once", name, symbolKind, parameter.messageName ? (parameter.name, parameter.node.endToken - 1) : span(parameter.node))
            }
        }

        func callable(_ node: GesSyntaxNode, _ kind: String, _ symbolKind: GesSyntaxSymbolKind) {
            let name = text(ownToken(node, .callable))
            callableCase(name, "\(kind) names must start with a lowercase ASCII letter and contain only ASCII letters or digits", name, symbolKind, span(node))
            let parameters = self.parameters(child(node, .parameterList))
            checkParameters(parameters, kind, name, symbolKind, duplicateDefinitionParameter)
            let scope = Scope()
            for parameter in parameters { scope.declare(text(parameter.name)) }
            elements(node, scope, validate: true, shadow: true)
        }

        func handler(_ node: GesSyntaxNode) {
            let message = text(ownToken(node, .messageName))
            let asParameter = child(node, .parameter)
            let parameters = asParameter.map { [($0, ownToken($0, .parameter), false, true)] } ?? self.parameters(child(node, .parameterList))
            if message == "initialization" {
                if asParameter != nil || !parameters.isEmpty {
                    report(invalidMessageCase, "System endpoint 'initialization' must use parameterless syntax", message, .handler, span(node))
                } else if child(node, .handlerTagFilter) != nil {
                    report(invalidMessageCase, "System endpoint 'initialization' cannot use tag filters", message, .handler, span(node))
                }
            } else if message == "undeliverable" {
                if asParameter == nil { report(invalidMessageCase, "System endpoint 'undeliverable' must use 'as message' syntax", message, .handler, span(node)) }
            } else {
                messageCase(message, "Handler message names must start uppercase and contain only ASCII letters or digits", message, .handler, span(node))
            }
            checkParameters(parameters, "Handler", message, .handler, duplicateHandlerParameter)
            let validation = Scope()
            let shadow = Scope()
            for parameter in parameters {
                validation.declare(text(parameter.name))
                shadow.declare(text(parameter.name))
            }
            statements(child(node, .block)!, validation, shadow)
        }

        // MARK: Statements

        func statements(_ block: GesSyntaxNode, _ validation: Scope, _ shadow: Scope) {
            for statement in block.children { self.statement(statement, validation, shadow) }
        }

        // A body, braced or a single statement, opens a scope of its own.
        func body(_ body: GesSyntaxNode, _ validation: Scope, _ shadow: Scope) {
            if body.kind == .block {
                statements(body, Scope(validation), Scope(shadow))
            } else {
                statement(body, Scope(validation), Scope(shadow))
            }
        }

        func statement(_ node: GesSyntaxNode, _ validation: Scope, _ shadow: Scope) {
            switch node.kind {
            case .emitStatement, .publishStatement, .expressionStatement: elements(node, shadow, validate: true, shadow: true)
            case .assemblyBlock: assembly(node, validation, shadow)
            case .letStatement:
                elements(node, shadow, validate: true, shadow: true)
                binding(text(ownToken(node, .letBinding)), span(node), validation, shadow)
            case .ifStatement: condition(node, validation, shadow)
            case .forStatement: loop(node, validation, shadow, nested: false)
            case .seededRandomStatement:
                // The seed comes before the body, which is the last element.
                let body = node.children[node.children.count - 1]
                elements(node, before: body.firstToken) { element, token in
                    if let element {
                        if self.isExpression(element) { self.expression(element, shadow, validate: true, shadow: true) }
                    } else {
                        self.token(token)
                    }
                }
                self.body(body, validation, shadow)
            default: break
            }
        }

        // A let binding: its name, a repetition within its scope and the shadowing of an enclosing binding.
        func binding(_ name: String, _ anchor: Anchor, _ validation: Scope, _ shadow: Scope) {
            identifierCase(name, "Variable '\(name)' must start lowercase, contain ASCII letters or digits, and may use one canonical final _index suffix", name, .variable, anchor)
            if validation.containsCurrent(name) {
                report(duplicateVariable, "Variable '\(name)' is already declared in the current scope", name, .variable, anchor)
            } else {
                validation.declare(name)
            }
            if shadow.containsInAncestor(name) { self.shadow(name, anchor) }
            shadow.declare(name)
        }

        func assembly(_ node: GesSyntaxNode, _ validation: Scope, _ shadow: Scope) {
            elements(node, shadow, validate: true, shadow: false)
            for token in node.firstToken..<node.endToken where roles[token] == .assemblyDeclaration {
                var line = token
                while tokens[line].kind != .let && tokens[line].kind != .dot { line -= 1 }
                let name = text(token)
                let anchor = (line, token)
                if shadow.containsVisible(name) { self.shadow(name, anchor) }
                guard tokens[line].kind == .let else { continue }
                shadow.declare(name)
                identifierCase(name, "Variable '\(name)' must start lowercase, contain ASCII letters or digits, and may use one canonical final _index suffix", name, .variable, anchor)
                if validation.containsCurrent(name) {
                    report(duplicateVariable, "Variable '\(name)' is already declared in the current scope", name, .variable, anchor)
                } else {
                    validation.declare(name)
                }
            }
        }

        func condition(_ node: GesSyntaxNode, _ validation: Scope, _ shadow: Scope) {
            let conditionValidation = Scope(validation)
            let conditionShadow = Scope(shadow)
            for child in node.children {
                switch child.kind {
                case .ifCondition:
                    elements(child, conditionShadow, validate: true, shadow: true)
                    let binding = ownToken(child, .conditionBinding)
                    if binding >= 0 { self.binding(text(binding), span(child), conditionValidation, conditionShadow) }
                case .block: statements(child, conditionValidation, conditionShadow)
                case .elseClause:
                    let elseValidation = Scope(validation)
                    let elseShadow = Scope(shadow)
                    for body in child.children {
                        if body.kind == .block { statements(body, elseValidation, elseShadow) } else { statement(body, elseValidation, elseShadow) }
                    }
                default: statement(child, conditionValidation, conditionShadow)
                }
            }
        }

        func loop(_ node: GesSyntaxNode, _ validation: Scope, _ shadow: Scope, nested: Bool) {
            let names = ownTokens(node, .loopBinding)
            var start = node.firstToken
            // A nested clause ranges from the last line break after its `and`.
            if nested { while tokens[start + 1].kind == .newLine { start += 1 } }
            let anchor = (start, node.endToken - 1)
            var bodyNode: GesSyntaxNode?
            var afterSource = false
            elements(node) { element, token in
                if let element {
                    if afterSource {
                        bodyNode = element
                    } else if self.isExpression(element) {
                        self.expression(element, shadow, validate: true, shadow: true)
                        afterSource = true
                    }
                } else if self.roles[token] == .variable && !afterSource {
                    self.token(token)
                    afterSource = true
                }
            }
            let identifier = text(names[0])
            identifierCase(identifier, "Loop variable '\(identifier)' must start lowercase, contain ASCII letters or digits, and may use one canonical final _index suffix", identifier, .variable, anchor)
            let loopValidation = Scope(validation)
            loopValidation.declare(identifier)
            let loopShadow = binderScope(shadow, names, anchor)
            guard let bodyNode else { return }
            if bodyNode.kind == .forStatement { loop(bodyNode, loopValidation, loopShadow, nested: true) } else { body(bodyNode, loopValidation, loopShadow) }
        }

        // A binder scope: one name, or several whose first is the element and the rest its components.
        func binderScope(_ parent: Scope, _ names: [Int], _ anchor: Anchor) -> Scope {
            let child = Scope(parent)
            if names.count < 2 {
                let name = text(names[0])
                if parent.containsVisible(name) { shadow(name, anchor) }
                child.declare(name)
                return child
            }
            let first = text(names[0])
            for token in names {
                let name = text(token)
                if name != first && !GesNames.identifier(name) { report(invalidIdentifierCase, "Invalid component binding name", name, .variable, anchor, shadowing: true) }
                if parent.containsVisible(name) { shadow(name, anchor) }
                if child.containsVisible(name) && !parent.containsVisible(name) { report(duplicateVariable, "Bindings must be distinct", name, .variable, anchor, shadowing: true) }
                child.declare(name)
            }
            return child
        }

        func binderScope(_ parent: Scope, _ name: String, _ anchor: Anchor) -> Scope {
            let child = Scope(parent)
            if parent.containsVisible(name) { shadow(name, anchor) }
            child.declare(name)
            return child
        }

        // MARK: Expressions

        func token(_ token: Int) {
            guard roles[token] == .variable else { return }
            let name = text(token)
            identifierCase(name, "Identifier '\(name)' must start lowercase, contain ASCII letters or digits, and may use one canonical final _index suffix", name, .variable, (token, token))
        }

        // The expressions among a node's elements: its name tokens and its expression children.
        func elements(_ node: GesSyntaxNode, _ scope: Scope, validate: Bool, shadow: Bool, from first: Int? = nil, before limit: Int? = nil) {
            elements(node, from: first, before: limit) { element, token in
                if let element {
                    if element.kind == .tagClause || element.kind == .choiceBranch || element.kind == .argument {
                        self.elements(element, scope, validate: validate, shadow: shadow)
                    } else if self.isExpression(element) {
                        self.expression(element, scope, validate: validate, shadow: shadow)
                    }
                } else if validate {
                    self.token(token)
                }
            }
        }

        func expression(_ node: GesSyntaxNode, _ scope: Scope, validate: Bool, shadow: Bool) {
            switch node.kind {
            case .assemblyBlock: if validate { elements(node, scope, validate: true, shadow: false) }
            case .callExpression, .messageExpression: call(node, scope, validate: validate, shadow: shadow)
            case .handlerLiteralExpression: if validate { handlerLiteral(text(ownToken(node, .messageName)), range(node), parameters(node), ranged: true) }
            case .typeConstructorExpression: typeConstructor(node, scope, validate: validate, shadow: shadow)
            case .typeOperationExpression: typeOperation(node, scope, validate: validate, shadow: shadow)
            case .memberAccessExpression:
                if validate {
                    let member = text(ownToken(node, .member))
                    labelCase(member, "Member name '\(member)' must start with a lowercase ASCII letter and contain only ASCII letters or digits", member, .variable, range(node))
                }
                elements(node, scope, validate: validate, shadow: shadow)
            case .collectionAccessExpression: collectionAccess(node, scope, validate: validate, shadow: shadow)
            case .mapLiteralExpression:
                for entry in node.children where entry.kind == .mapEntry {
                    if validate {
                        let key = text(ownToken(entry, .mapKey))
                        labelCase(key, "Map key '\(key)' must start with a lowercase ASCII letter and contain only ASCII letters or digits", key, .variable, range(node))
                    }
                    elements(entry, scope, validate: validate, shadow: shadow)
                }
            case .collectionFactoryExpression: generated(node, scope, validate: validate, shadow: shadow)
            case .seededRandomExpression: seededRandom(node, scope, validate: validate, shadow: shadow)
            default: elements(node, scope, validate: validate, shadow: shadow)
            }
        }

        // A call or message literal: its name, repeated labels, label spellings and the argument values.
        func call(_ node: GesSyntaxNode, _ scope: Scope, validate: Bool, shadow: Bool) {
            guard validate else { return elements(node, scope, validate: false, shadow: shadow) }
            if node.kind == .callExpression {
                let name = text(ownToken(node, .callTarget))
                callableCase(name, "Call target '\(name)' must use identifier casing (predicate/function names start lowercase)", name, .globalDefinition, range(node))
                arguments(node, name, .globalDefinition, { "Call argument label '\($0)' must use identifier casing" }, scope, shadow: shadow)
            } else {
                let message = text(ownToken(node, .messageName))
                messageCase(message, "Message literal '\(message)' must start uppercase and contain only ASCII letters or digits", message, .message, range(node))
                arguments(node, message, .message, { "Message literal '\(message)' declares invalid argument name '\($0)'" }, scope, shadow: shadow)
            }
        }

        func arguments(_ node: GesSyntaxNode, _ symbol: String, _ labelKind: GesSyntaxSymbolKind, _ labelMessage: (String) -> String, _ scope: Scope, shadow: Bool) {
            let arguments = node.children.filter { $0.kind == .argument }.map { ($0, ownToken($0, .argumentLabel)) }
            var seen: Set<String> = []
            var reported: Set<String> = []
            for (argument, label) in arguments where label >= 0 {
                if seen.insert(text(label)).inserted || !reported.insert(text(label)).inserted { continue }
                report(duplicatePublishArgument, "Named argument '\(text(label))' is declared more than once", symbol, .handler, span(argument))
            }
            elements(node) { element, token in
                guard let element else { return self.token(token) }
                if element.kind == .argument {
                    let label = self.ownToken(element, .argumentLabel)
                    if label >= 0 { self.labelCase(self.text(label), labelMessage(self.text(label)), symbol, labelKind, self.span(element)) }
                    self.elements(element, scope, validate: true, shadow: shadow)
                } else if element.kind == .tagClause {
                    self.elements(element, scope, validate: true, shadow: shadow)
                } else if self.isExpression(element) {
                    self.expression(element, scope, validate: true, shadow: shadow)
                }
            }
        }

        // A handler literal of the `:Handler` data form has no parameter nodes; its diagnostics point at the names.
        func handlerLiteral(_ message: String, _ anchor: Anchor, _ parameters: [Parameter], ranged: Bool) {
            messageCase(message, "Handler literal '\(message)' must start uppercase and contain only ASCII letters or digits", message, .handler, anchor)
            for parameter in parameters {
                let name = text(parameter.name)
                labelCase(name, "Handler literal '\(message)' declares invalid parameter '\(name)'", message, .handler, ranged ? anchor : (parameter.name, parameter.name))
            }
            var seen: Set<String> = []
            var reported: Set<String> = []
            for parameter in parameters {
                let name = text(parameter.name)
                if seen.insert(name).inserted || !reported.insert(name).inserted { continue }
                report(duplicateHandlerParameter, "Handler literal '\(message)' declares parameter '\(name)' more than once", message, .handler, ranged ? span(parameter.node) : (parameter.name, parameter.name))
            }
        }

        func typeConstructor(_ node: GesSyntaxNode, _ scope: Scope, validate: Bool, shadow: Bool) {
            let typeName = GesSyntaxValidator.canonicalTypeName(String(text(node.firstToken).dropFirst()))
            let messageToken = ownToken(node, .messageName)
            if typeName == "handler" && messageToken >= 0 {
                guard validate else { return }
                handlerLiteral(text(messageToken), (messageToken, messageToken), ownTokens(node, .parameter).map { (node, $0, true, false) }, ranged: false)
                return
            }
            guard validate else { return elements(node, scope, validate: false, shadow: shadow) }
            arguments(node, typeName, .type, { "Type constructor ':\(typeName)' declares invalid argument label '\($0)'" }, scope, shadow: shadow)
        }

        func typeOperation(_ node: GesSyntaxNode, _ scope: Scope, validate: Bool, shadow: Bool) {
            if validate {
                let target = ownToken(node, .predicateTarget)
                if target >= 0 {
                    let name = text(target)
                    callableCase(name, "Predicate test target '\(name)' must use identifier casing (start lowercase)", name, .predicate, range(node))
                } else if let type = child(node, .typeReference) {
                    removedType(GesSyntaxValidator.canonicalTypeName(String(text(type.firstToken).dropFirst())), range(node))
                }
            }
            elements(node, scope, validate: validate, shadow: shadow)
        }

        func generated(_ node: GesSyntaxNode, _ scope: Scope, validate: Bool, shadow: Bool) {
            let name = text(ownToken(node, .generatedBinding))
            if validate { identifierCase(name, "Generated collection identifier '\(name)' must use identifier casing (start lowercase)", name, .variable, range(node)) }
            // The source comes before `where` or the projection; the binding scopes the rest.
            var inner: Scope?
            elements(node) { element, token in
                if let element {
                    if self.isExpression(element) { self.expression(element, inner ?? scope, validate: validate, shadow: shadow) }
                } else {
                    guard self.roles[token] == .variable else { return }
                    if validate { self.token(token) }
                }
                if inner == nil { inner = shadow ? self.binderScope(scope, name, self.range(node)) : Scope(scope) }
            }
        }

        func seededRandom(_ node: GesSyntaxNode, _ scope: Scope, validate: Bool, shadow: Bool) {
            // The body has a scope of its own for shadowing.
            var seeded = false
            elements(node) { element, token in
                if let element {
                    if self.isExpression(element) { self.expression(element, seeded ? Scope(scope) : scope, validate: validate, shadow: shadow) }
                    seeded = true
                } else if self.roles[token] == .variable {
                    if validate { self.token(token) }
                    seeded = true
                }
            }
        }

        func collectionAccess(_ node: GesSyntaxNode, _ scope: Scope, validate: Bool, shadow: Bool) {
            elements(node) { element, token in
                if let element {
                    if element.kind == .collectionSelector {
                        self.selectorStep(element, scope, validate: validate, shadow: shadow)
                    } else if self.isExpression(element) {
                        self.expression(element, scope, validate: validate, shadow: shadow)
                    }
                } else if validate {
                    self.token(token)
                }
            }
        }

        static let combined: Set<String> = [":cartesian", ":lockstep", ":zip", ":union", ":intersect", ":difference"]

        static let patterns: Set<String> = [":has", ":take", ":drop", ":draw", ":shuffle", ":reverse", ":sort"]

        func selectorStep(_ node: GesSyntaxNode, _ scope: Scope, validate: Bool, shadow: Bool) {
            let keyword = tokens[node.firstToken]
            let anchor = span(node)
            let bindings = ownTokens(node, .selectorBinding)
            let weights = ownTokens(node, .weightBinding)
            let operation = keyword.kind.isSelector ? keyword.text : ""
            if operation == ":split" || Self.combined.contains(operation) { return elements(node, scope, validate: validate, shadow: shadow) }
            if Self.patterns.contains(operation) {
                // Patterns are no references; their face values and object values only meet the shadowing rule.
                if shadow { patternValues(node, scope) }
                return
            }
            if operation == ":fold" || operation == ":reduce" { return fold(node, scope, validate: validate, shadow: shadow, bindings, anchor) }
            if bindings.isEmpty && weights.isEmpty { return elements(node, scope, validate: validate, shadow: shadow) }
            let weightStart = weights.first ?? node.endToken
            if let firstBinding = bindings.first {
                let first = text(firstBinding)
                if validate && operation != ":foreach" { identifierCase(first, "Selector identifier '\(first)' must use identifier casing (start lowercase)", first, .variable, anchor) }
                let inner = shadow ? binderScope(scope, bindings, anchor) : Scope(scope)
                elements(node, inner, validate: validate, shadow: shadow, from: bindings[bindings.count - 1], before: weightStart)
            }
            guard let firstWeight = weights.first else { return }
            // A weighted choice: the weight in a binding scope of its own.
            let weightName = text(firstWeight)
            if validate { identifierCase(weightName, "Selector weight identifier '\(weightName)' must use identifier casing (start lowercase)", weightName, .variable, anchor) }
            let weightScope = shadow ? binderScope(scope, weights, anchor) : Scope(scope)
            elements(node, weightScope, validate: validate, shadow: shadow, from: weights[weights.count - 1])
        }

        func fold(_ node: GesSyntaxNode, _ scope: Scope, validate: Bool, shadow: Bool, _ bindings: [Int], _ anchor: Anchor) {
            let accumulatorName = text(ownToken(node, .foldAccumulator))
            let elementName = text(bindings[0])
            if validate {
                identifierCase(accumulatorName, "Accumulator must use identifier casing", accumulatorName, .variable, anchor)
                identifierCase(elementName, "Element must use identifier casing", elementName, .variable, anchor)
            }
            // The seed comes before the bindings; the projection is in their scope.
            var foldScope = scope
            var bound = false
            elements(node) { element, token in
                if let element {
                    if self.isExpression(element) { self.expression(element, foldScope, validate: validate, shadow: shadow) }
                    return
                }
                if self.roles[token] == .selectorBinding && !bound {
                    bound = true
                    guard shadow else { return }
                    foldScope = bindings.count > 1 && elementName == accumulatorName ? self.binderScope(scope, bindings, anchor) : self.binderScope(scope, accumulatorName, anchor)
                    if accumulatorName == elementName {
                        self.report(duplicateVariable, "Fold bindings must be distinct", elementName, .variable, anchor, shadowing: true)
                    } else {
                        foldScope = self.binderScope(foldScope, bindings, anchor)
                    }
                    return
                }
                if validate { self.token(token) }
            }
        }

        func patternValues(_ node: GesSyntaxNode, _ scope: Scope) {
            for child in node.children {
                switch child.kind {
                case .dicePattern: elements(child, scope, validate: false, shadow: true)
                case .objectPatternEntry:
                    elements(child, scope, validate: false, shadow: true)
                    patternValues(child, scope)
                case .objectPattern: patternValues(child, scope)
                default: break
                }
            }
        }
    }

    // The canonical name of a source type name without its colon; built-in types are lowercase, such as `number`.
    static func canonicalTypeName(_ typeName: String) -> String {
        ["Record", "Nothing", "Boolean", "Number", "Percentage", "Vector", "Point", "Series", "Range", "Message", "Handler", "Tag", "Text", "List", "Map", "Dice"].contains(typeName)
            ? typeName.lowercased() : typeName
    }
}
