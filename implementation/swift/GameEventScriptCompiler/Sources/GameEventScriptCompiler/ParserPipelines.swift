// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

extension GesParser {
    func bindingNames() throws -> [String] {
        var result = [try identifier()]
        while match(.comma) {
            newlines()
            result.append(try identifier())
        }
        return result
    }

    static let combinedSelectors: Set<String> = [":cartesian", ":lockstep", ":zip", ":union", ":intersect", ":difference"]

    static let pipelineSelectors: Set<String> = [
        ":entries", ":keys", ":values", ":filter", ":select", ":foreach", ":any", ":all", ":count", ":sum", ":average", ":min", ":max", ":highest", ":lowest", ":fold", ":reduce", ":map", ":first", ":last", ":single", ":choose", ":take",
        ":drop", ":draw", ":shuffle", ":reverse", ":sort", ":distinct", ":group", ":order", ":contains", ":has", ":term", ":split",
    ]

    var isCombinedSelector: Bool { currentKind == .selector && Self.combinedSelectors.contains(current.text) }

    var isPipelineSelector: Bool { currentKind.isSelector && peekKind() != .dot && (isCombinedSelector || Self.pipelineSelectors.contains(current.text)) }

    func pipelineSteps(_ base: GesExpression?) throws -> GesExpression {
        var result = base
        repeat {
            newlines()
            let start = current
            if isCombinedSelector {
                result = try combinedStep(result, start)
            } else if matchSelector(":split") {
                result = try splitStep(result, start)
            } else {
                result = try selectorStep(result, start)
            }
            newlines()
        } while isPipelineSelector
        return result!
    }

    func combinedStep(_ base: GesExpression?, _ start: GesToken) throws -> GesExpression {
        let operation = String(advance().text.dropFirst())
        newlines()
        var sources = base.map { [$0] } ?? []
        sources.append(try expression())
        while match(.comma) {
            newlines()
            sources.append(try expression())
        }
        guard sources.count >= 2 else { throw failure("Combined selectors require at least two sources.", start) }
        let result = node(.combined(operation == "zip" ? "lockstep" : operation, sources), start)
        result.depth = (sources.map(\.depth).max() ?? 0) + 1
        if result.depth > 32 { throw failure("Expression nesting exceeded.", start, code: "parse.sourceNestingExceeded") }
        return result
    }

    func splitStep(_ base: GesExpression?, _ start: GesToken) throws -> GesExpression {
        guard let input = base else { throw failure("Selector requires a source.", start) }
        newlines()
        try expect(.on)
        newlines()
        let whitespace = matchWord("whitespace")
        let delimiter = whitespace ? node(.literal(.nothing), previous) : try expression()
        return try ranged(.constructor(whitespace ? "__splitWhitespace" : "__split", whitespace ? [.init(label: "_", value: input)] : [.init(label: "_", value: input), .init(label: "_", value: delimiter)]), input)
    }

    func selectorStep(_ base: GesExpression?, _ start: GesToken) throws -> GesExpression {
        guard let input = base else { throw failure("Selector requires a source.", start) }
        return try selectorAccess(input, selector())
    }

    func selectorAccess(_ input: GesExpression, _ selector: GesSelector) throws -> GesExpression {
        let result = try combined(.selector(input, selector), input)
        // C# gives the object-pattern selector no range, which leaves the whole access without one.
        if selector.operation == "objectMatch" { return GesExpression(result.kind, GesAstOptimizer.noRange(result.location), depth: result.depth) }
        return result
    }
}
