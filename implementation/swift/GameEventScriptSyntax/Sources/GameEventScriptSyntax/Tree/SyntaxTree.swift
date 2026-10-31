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

/// A node of the lossless syntax tree. It spans the tokens `firstToken..<endToken` of its tree; its own tokens are those
/// not covered by a child.
@_spi(Syntax) public final class GesSyntaxNode: Sendable {
    @_spi(Syntax) public let kind: GesSyntaxNodeKind
    @_spi(Syntax) public let firstToken: Int
    @_spi(Syntax) public let endToken: Int
    @_spi(Syntax) public let children: [GesSyntaxNode]

    init(kind: GesSyntaxNodeKind, firstToken: Int, endToken: Int, children: [GesSyntaxNode]) {
        self.kind = kind
        self.firstToken = firstToken
        self.endToken = endToken
        self.children = children
    }
}

/// A source with its tokens and syntax nodes; its tokens, including their trivia, reproduce the source exactly.
@_spi(Syntax) public struct GesSyntaxTree: Sendable {
    @_spi(Syntax) public let text: String
    @_spi(Syntax) public let tokens: [GesToken]
    @_spi(Syntax) public let root: GesSyntaxNode
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

    @_spi(Syntax) public mutating func finishNode(at token: Int) {
        let (kind, firstToken, firstChild) = open.removeLast()
        let node = GesSyntaxNode(kind: kind, firstToken: firstToken, endToken: token, children: Array(children[firstChild...]))
        children.removeSubrange(firstChild...)
        children.append(node)
    }

    /// Closes the nodes a failed production left open, down to the depth it started at.
    @_spi(Syntax) public mutating func finish(to depth: Int, at token: Int) {
        while open.count > depth { finishNode(at: token) }
    }

    @_spi(Syntax) public mutating func finish(text: String, tokens: [GesToken]) -> GesSyntaxTree {
        finish(to: 0, at: tokens.count)
        let root = children.count == 1 && children[0].kind == .compilationUnit ? children[0] : GesSyntaxNode(kind: .compilationUnit, firstToken: 0, endToken: tokens.count, children: children)
        return .init(text: text, tokens: tokens, root: root)
    }
}
