// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

/// Kinds of syntax nodes; tokens keep their own `GesTokenKind`. The cases match the C# `GesSyntaxNodeKind` names.
@_spi(Syntax) public enum GesSyntaxNodeKind: Sendable {
    case compilationUnit, moduleDeclaration, constantDefinition, recordDefinition, recordField, predicateDefinition, functionDefinition
    case parameterList, parameter, typeReference, eventHandler, handlerTagFilter, block
    case emitStatement, publishStatement, letStatement, ifStatement, ifCondition, elseClause, forStatement, seededRandomStatement, expressionStatement
    case assemblyBlock, tagClause, sendExpression, argumentList, argument
    case guardedChoiceExpression, choiceBranch, binaryExpression, unaryExpression, betweenExpression, membershipExpression, typeOperationExpression
    case dicePattern, powerExpression, seriesExpression, intrinsicCallExpression, memberAccessExpression, collectionAccessExpression, collectionSelector
    case objectPattern, objectPatternEntry, parenthesizedExpression, literalExpression, nameExpression, messageExpression, callExpression
    case handlerLiteralExpression, listLiteralExpression, diceLiteralExpression, collectionFactoryExpression, generatedCollectionExpression
    case mapLiteralExpression, mapEntry, randomExpression, rangeExpression, seededRandomExpression, diceExpression, extensionCallExpression
    case typeConstructorExpression, clampExpression, variadicExpression, error
}

/// The role a name token plays, recorded while parsing for validation and highlighting.
@_spi(Syntax) public enum GesSyntaxRole: UInt8, Sendable {
    /// A token that names nothing.
    case none
    /// A name expression.
    case variable
    /// The name of a let binding.
    case letBinding
    /// The name of an if-let binding.
    case conditionBinding
    /// A name a for loop binds.
    case loopBinding
    /// A name a selector binds.
    case selectorBinding
    /// A name a weighted choice binds.
    case weightBinding
    /// The accumulator of a fold or reduce.
    case foldAccumulator
    /// The element of a generated collection.
    case generatedBinding
    /// A register an assembly block declares.
    case assemblyDeclaration
    /// A parameter whose name is also its label.
    case parameter
    /// A parameter without a label.
    case unlabeledParameter
    /// A record field that is a labeled constructor parameter or computed.
    case field
    /// A record field that is an unlabeled constructor parameter.
    case unlabeledField
    /// The name of a predicate or function definition.
    case callable
    /// The name a call targets.
    case callTarget
    /// The predicate an `is` test names.
    case predicateTarget
    /// An argument label.
    case argumentLabel
    /// A member name.
    case member
    /// A map or object pattern key.
    case mapKey
    /// A message name of a handler, message literal or handler literal.
    case messageName
}

/// A node of the lossless syntax tree. It spans the tokens `firstToken..<endToken` of its tree; its own tokens are those
/// not covered by a child.
@_spi(Syntax) public final class GesSyntaxNode: Sendable {
    @_spi(Syntax) public let kind: GesSyntaxNodeKind
    @_spi(Syntax) public let firstToken: Int
    @_spi(Syntax) public let endToken: Int
    @_spi(Syntax) public let children: [GesSyntaxNode]
    /// The first token an expression node's diagnostics range over, which can differ from its own first token: a
    /// parenthesized expression ranges over its content and a member access over its target; -1 without a range.
    @_spi(Syntax) public let rangeStart: Int
    /// The last token of that range, or -1.
    @_spi(Syntax) public let rangeEnd: Int

    init(kind: GesSyntaxNodeKind, firstToken: Int, endToken: Int, children: [GesSyntaxNode], rangeStart: Int = -1, rangeEnd: Int = -1) {
        self.kind = kind
        self.firstToken = firstToken
        self.endToken = endToken
        self.children = children
        self.rangeStart = rangeStart
        self.rangeEnd = rangeEnd
    }
}

/// A source with its tokens and syntax nodes; its tokens, including their trivia, reproduce the source exactly.
@_spi(Syntax) public struct GesSyntaxTree: Sendable {
    @_spi(Syntax) public let text: String
    @_spi(Syntax) public let tokens: [GesToken]
    @_spi(Syntax) public let root: GesSyntaxNode
    /// The syntax errors of the source in source order; empty for a source without syntax errors.
    @_spi(Syntax) public let diagnostics: [GesSyntaxDiagnostic]
    /// The role of each token; `none` for tokens that name nothing.
    @_spi(Syntax) public let roles: [GesSyntaxRole]
}

/// Builds a syntax tree while a parser consumes tokens by index. A checkpoint taken before a token lets a later node
/// start there, so a node can wrap children that were parsed before its kind was known.
@_spi(Syntax) public struct GesSyntaxTreeBuilder {
    /// A position between tokens and children, taken before parsing something a later node may wrap.
    @_spi(Syntax) public struct Checkpoint: Sendable {
        let token: Int
        let child: Int
    }

    private var children: [GesSyntaxNode] = []
    private var open: [(kind: GesSyntaxNodeKind, firstToken: Int, firstChild: Int)] = []

    @_spi(Syntax) public init() {}

    @_spi(Syntax) public var depth: Int { open.count }

    @_spi(Syntax) public func mark(_ token: Int) -> Checkpoint { .init(token: token, child: children.count) }

    @_spi(Syntax) public mutating func startNode(_ kind: GesSyntaxNodeKind, at token: Int) { open.append((kind, token, children.count)) }

    @_spi(Syntax) public mutating func startNode(at checkpoint: Checkpoint, _ kind: GesSyntaxNodeKind) {
        precondition(checkpoint.child <= children.count && (open.last.map { checkpoint.child >= $0.firstChild } ?? true), "A checkpoint must lie inside the innermost open node.")
        open.append((kind, checkpoint.token, checkpoint.child))
    }

    @_spi(Syntax) public mutating func finishNode(at token: Int, range: (start: Int, end: Int) = (-1, -1)) {
        let (kind, firstToken, firstChild) = open.removeLast()
        let node = GesSyntaxNode(kind: kind, firstToken: firstToken, endToken: token, children: Array(children[firstChild...]), rangeStart: range.start, rangeEnd: range.end)
        children.removeSubrange(firstChild...)
        children.append(node)
    }

    /// Drops the nodes opened or completed since the checkpoint, taken at the given depth, so that its tokens can form one
    /// error node; the tokens themselves stay.
    @_spi(Syntax) public mutating func discard(to depth: Int, _ checkpoint: Checkpoint) {
        open.removeSubrange(depth...)
        children.removeSubrange(checkpoint.child...)
    }

    /// Closes the nodes a failed production left open, down to the depth it started at.
    @_spi(Syntax) public mutating func finish(to depth: Int, at token: Int) {
        while open.count > depth { finishNode(at: token) }
    }

    @_spi(Syntax) public mutating func finish(text: String, tokens: [GesToken], diagnostics: [GesSyntaxDiagnostic] = [], roles: [GesSyntaxRole] = []) -> GesSyntaxTree {
        finish(to: 0, at: tokens.count)
        let root = children.count == 1 && children[0].kind == .compilationUnit ? children[0] : GesSyntaxNode(kind: .compilationUnit, firstToken: 0, endToken: tokens.count, children: children)
        return .init(text: text, tokens: tokens, root: root, diagnostics: diagnostics, roles: roles.count == tokens.count ? roles : Array(repeating: .none, count: tokens.count))
    }
}
