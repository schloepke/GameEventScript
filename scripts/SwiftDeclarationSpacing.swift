// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import Foundation
import SwiftParser
import SwiftSyntax

// Uses declaration boundaries rather than matching source lines: comments,
// attributes, nested declarations and multiline/raw strings remain attached.
private final class DeclarationSpacing: SyntaxVisitor {
    struct Edit {
        let range: Range<Int>
        let replacement: [UInt8]
    }

    let bytes: [UInt8]
    var edits: [Edit] = []

    init(_ source: String) {
        bytes = Array(source.utf8)
        super.init(viewMode: .sourceAccurate)
    }

    override func visit(_ node: MemberBlockItemListSyntax) -> SyntaxVisitorContinueKind {
        separate(node.map { (Syntax($0), needsSeparation(Syntax($0.decl))) })
        return .visitChildren
    }

    override func visit(_ node: CodeBlockItemListSyntax) -> SyntaxVisitorContinueKind {
        separate(node.map { (Syntax($0), needsSeparation(Syntax($0.item))) })
        return .visitChildren
    }

    private func needsSeparation(_ node: Syntax) -> Bool {
        node.is(FunctionDeclSyntax.self) || node.is(InitializerDeclSyntax.self) || node.is(DeinitializerDeclSyntax.self) || node.is(SubscriptDeclSyntax.self) || node.is(ClassDeclSyntax.self) || node.is(StructDeclSyntax.self)
            || node.is(EnumDeclSyntax.self) || node.is(ProtocolDeclSyntax.self) || node.is(ActorDeclSyntax.self) || node.is(ExtensionDeclSyntax.self) || node.is(MacroDeclSyntax.self)
    }

    private func whitespace(_ byte: UInt8) -> Bool { byte == 10 || byte == 13 || byte == 32 || byte == 9 }

    private func separate(_ items: [(Syntax, Bool)]) {
        for index in items.indices.dropFirst() where items[index - 1].1 || items[index].1 {
            // The previous item's trailing trivia may own a same-line comment;
            // start after it and before the next item's documentation/attributes.
            var start = items[index - 1].0.endPosition.utf8Offset
            let limit = items[index].0.positionAfterSkippingLeadingTrivia.utf8Offset
            var end = start
            while start > 0 && whitespace(bytes[start - 1]) { start -= 1 }
            while end < limit && whitespace(bytes[end]) { end += 1 }
            let gap = Array(bytes[start..<end])
            let indentStart = gap.lastIndex(of: 10).map { $0 + 1 } ?? gap.count
            let replacement: [UInt8] = [10, 10] + gap[indentStart...]
            if gap != replacement { edits.append(Edit(range: start..<end, replacement: replacement)) }
        }
    }
}

private func process(_ path: String, fix: Bool) throws -> Bool {
    let source = try String(contentsOfFile: path, encoding: .utf8)
    let tree = Parser.parse(source: source)
    guard !tree.hasError else { throw SpacingError.invalidSyntax(path) }
    let visitor = DeclarationSpacing(source)
    visitor.walk(tree)
    if visitor.edits.isEmpty { return false }
    if fix {
        var bytes = visitor.bytes
        for edit in visitor.edits.sorted(by: { $0.range.lowerBound > $1.range.lowerBound }) { bytes.replaceSubrange(edit.range, with: edit.replacement) }
        let formatted = String(decoding: bytes, as: UTF8.self)
        let checked = Parser.parse(source: formatted)
        guard !checked.hasError, tree.tokens(viewMode: .sourceAccurate).map(\.text) == checked.tokens(viewMode: .sourceAccurate).map(\.text) else { throw SpacingError.changedTokens(path) }
        try formatted.write(toFile: path, atomically: true, encoding: .utf8)
    } else {
        for edit in visitor.edits {
            let line = visitor.bytes[..<edit.range.lowerBound].filter { $0 == 10 }.count + 1
            print("\(path):\(line):1: error: require exactly one blank line between declarations")
        }
    }
    return true
}

private enum SpacingError: Error {
    case invalidSyntax(String)
    case changedTokens(String)
}

let arguments = Array(CommandLine.arguments.dropFirst())
let fix = arguments.first == "--fix"
let paths = fix ? Array(arguments.dropFirst()) : arguments
var failed = false
for path in paths {
    do { if try process(path, fix: fix), !fix { failed = true } } catch {
        fputs("Swift declaration spacing: \(error)\n", stderr)
        failed = true
    }
}
exit(failed ? 1 : 0)
