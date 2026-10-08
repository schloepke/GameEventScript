// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

extension GesParser {
    func bindingNames() throws -> [String] {
        var result = [try identifier()]
        while match(",") {
            newlines()
            result.append(try identifier())
        }
        return result
    }

    var isCombinedSelector: Bool { [":cartesian", ":lockstep", ":zip", ":union", ":intersect", ":difference"].contains(current.syntaxText) }

    var isPipelineSelector: Bool {
        current.kind == "selector" && peek().syntaxText != "."
            && (isCombinedSelector
                || [
                    ":entries", ":keys", ":values", ":filter", ":select", ":foreach", ":any", ":all", ":count", ":sum", ":average", ":min", ":max", ":highest", ":lowest", ":fold", ":reduce", ":map", ":first", ":last", ":single", ":choose", ":take",
                    ":drop", ":draw", ":shuffle", ":reverse", ":sort", ":distinct", ":group", ":order", ":contains", ":has", ":term", ":split",
                ].contains(current.syntaxText))
    }

    func pipelineSteps(_ base: GesExpression?) throws -> GesExpression {
        var result = base
        repeat {
            newlines()
            let start = current
            if isCombinedSelector {
                let operation = String(advance().text.dropFirst())
                newlines()
                var sources = result.map { [$0] } ?? []
                sources.append(try expression())
                while match(",") {
                    newlines()
                    sources.append(try expression())
                }
                guard sources.count >= 2 else { throw failure("Combined selectors require at least two sources.", start) }
                result = node(.combined(operation == "zip" ? "lockstep" : operation, sources), start)
                result!.depth = (sources.map(\.depth).max() ?? 0) + 1
                if result!.depth > 32 { throw failure("Expression nesting exceeded.", start, code: "parse.sourceNestingExceeded") }
            } else if match(":split") {
                guard let input = result else { throw failure("Selector requires a source.", start) }
                newlines()
                try expect("on")
                newlines()
                let whitespace = match("whitespace")
                let delimiter = whitespace ? node(.literal(.nothing), previous) : try expression()
                result = try combined(.constructor(whitespace ? "__splitWhitespace" : "__split", whitespace ? [.init(label: "_", value: input)] : [.init(label: "_", value: input), .init(label: "_", value: delimiter)]), input)
            } else {
                guard let input = result else { throw failure("Selector requires a source.", start) }
                result = try combined(.selector(input, selector()), input)
            }
            newlines()
        } while isPipelineSelector
        return result!
    }
}
