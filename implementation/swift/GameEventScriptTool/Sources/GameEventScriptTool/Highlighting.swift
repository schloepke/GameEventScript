// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import Foundation

// Presentation only. Highlight incomplete input without invoking the compiler or evaluating values.
final class Highlighting {
    struct Rule {
        let pattern: NSRegularExpression
        let color: Int
        let captures: [(Int, Int)]
    }
    let sourceRules: [Rule]
    let assemblyRules: [Rule]
    let sourceLiterals = try! NSRegularExpression(pattern: #"//[^\r\n]*|'(?:''|[^'])*'?|"(?:""|[^"])*"?"#)
    let assemblyLiterals = try! NSRegularExpression(pattern: #"//[^\r\n]*|"(?:\\.|[^"\\])*"?"#)
    let embedded: NSRegularExpression
    let sourceEnd: NSRegularExpression
    init() {
        sourceRules = Self.rules(EmbeddedGrammars.source, embedded: false)
        assemblyRules = Self.rules(EmbeddedGrammars.assembly, embedded: true)
        let root = Self.grammar(EmbeddedGrammars.assembly)
        let repository = root["repository"] as! [String: [String: Any]]
        let patterns = repository["embedded-source"]!["patterns"] as! [[String: Any]]
        embedded = Self.regex(
            "(?<sourceBlock>" + (patterns[0]["begin"] as! String) + ")|(?:" + (patterns[1]["begin"] as! String) + ")")
        sourceEnd = Self.regex(patterns[0]["end"] as! String)
    }
    static func paint(_ text: String, _ color: Int) -> String { "\u{1b}[\(color)m" + text + "\u{1b}[0m" }
    func render(_ text: String) -> String { render(text, literals: sourceLiterals, rules: sourceRules) }
    func renderAssembly(_ text: String) -> String {
        guard text.utf16.count < 262_144 else { return text }
        let source = text as NSString
        var output = ""
        var position = 0
        let end = source.length
        let deadline = ProcessInfo.processInfo.systemUptime + 0.1
        let regions = matches(embedded, text: text, deadline: deadline)
        let literals = matches(sourceLiterals, text: text, deadline: deadline)
        let boundaries = matches(sourceEnd, text: text, deadline: deadline)
        for match in regions where match.range.location >= position {
            let start = NSMaxRange(match.range)
            output += render(
                source.substring(with: NSRange(location: position, length: start - position)),
                literals: assemblyLiterals, rules: assemblyRules)
            let stop: Int
            if match.range(withName: "sourceBlock").location != NSNotFound {
                stop =
                    boundaries.first { boundary in
                        boundary.range.location >= start
                            && !literals.contains { literal in NSLocationInRange(boundary.range.location, literal.range)
                            }
                    }?.range.location ?? end
            } else {
                let range = source.range(of: "\n", range: NSRange(location: start, length: end - start))
                stop = range.location == NSNotFound ? end : range.location
            }
            output += render(source.substring(with: NSRange(location: start, length: stop - start)))
            position = stop
        }
        output += render(source.substring(from: position), literals: assemblyLiterals, rules: assemblyRules)
        return output
    }
    func render(_ text: String, literals: NSRegularExpression, rules: [Rule]) -> String {
        guard !text.isEmpty, text.utf16.count < 262_144 else { return text }
        let source = text as NSString
        var colors = [Int](repeating: 0, count: source.length)
        let deadline = ProcessInfo.processInfo.systemUptime + 0.1
        func fill(_ range: NSRange, _ color: Int, protect: Bool = true) {
            guard range.location != NSNotFound, range.length > 0 else { return }
            let indices = range.location..<NSMaxRange(range)
            if !protect || indices.allSatisfy({ colors[$0] == 0 }) { for index in indices { colors[index] = color } }
        }
        for match in matches(literals, text: text, deadline: deadline) {
            fill(match.range, source.substring(with: match.range).hasPrefix("//") ? 90 : 32)
        }
        for rule in rules {
            for match in matches(rule.pattern, text: text, deadline: deadline) {
                if rule.captures.isEmpty {
                    fill(match.range, rule.color)
                } else {
                    for (group, color) in rule.captures where group < match.numberOfRanges {
                        fill(match.range(at: group), color)
                    }
                }
            }
            if ProcessInfo.processInfo.systemUptime > deadline { return text }
        }
        var result = ""
        var start = 0
        while start < colors.count {
            var end = start + 1
            while end < colors.count && colors[end] == colors[start] { end += 1 }
            let part = source.substring(with: NSRange(location: start, length: end - start))
            result += colors[start] == 0 ? part : Self.paint(part, colors[start])
            start = end
        }
        return result
    }
    private func matches(_ regex: NSRegularExpression, text: String, deadline: Double) -> [NSTextCheckingResult] {
        var results: [NSTextCheckingResult] = []
        regex.enumerateMatches(in: text, options: [.reportProgress], range: NSRange(text.startIndex..., in: text)) {
            match, _, stop in
            if ProcessInfo.processInfo.systemUptime > deadline {
                stop.pointee = true
            } else if let match {
                results.append(match)
            }
        }
        return results
    }
    static func grammar(_ json: String) -> [String: Any] {
        try! JSONSerialization.jsonObject(with: Data(json.utf8)) as! [String: Any]
    }
    static func regex(_ pattern: String) -> NSRegularExpression {
        try! NSRegularExpression(pattern: pattern, options: [.anchorsMatchLines])
    }
    static func rules(_ json: String, embedded: Bool) -> [Rule] {
        let root = grammar(json)
        let repository = root["repository"] as! [String: [String: Any]]
        return (root["patterns"] as! [[String: String]]).flatMap { entry -> [Rule] in
            let name = String(entry["include"]!.dropFirst())
            return (repository[name]!["patterns"] as! [[String: Any]]).compactMap { pattern in
                let expression =
                    pattern["match"] as? String
                    ?? (embedded && pattern["beginCaptures"] != nil ? pattern["begin"] as? String : nil)
                guard let expression else { return nil }
                let groups =
                    (pattern[pattern["match"] == nil ? "beginCaptures" : "captures"] as? [String: [String: String]])
                    ?? [:]
                let captures = groups.map { (Int($0.key)!, scopeColor($0.value["name"]!)) }.sorted { $0.0 < $1.0 }
                return Rule(
                    pattern: regex(expression), color: scopeColor(pattern["name"] as? String ?? ""), captures: captures)
            }
        }
    }
    static func scopeColor(_ scope: String) -> Int {
        if scope.hasPrefix("comment") { return 90 }
        if scope.hasPrefix("string") { return 32 }
        if scope.hasPrefix("constant.numeric") || scope.hasPrefix("constant.language.numeric") { return 34 }
        if scope.hasPrefix("keyword") || scope.hasPrefix("constant.language.boolean") { return 35 }
        if scope.hasPrefix("entity") || scope.hasPrefix("support") { return 36 }
        if scope.hasPrefix("variable.other.constant") || scope.hasPrefix("variable.other.register") { return 33 }
        if scope.hasPrefix("variable.other.label") || scope.hasPrefix("constant.other.symbol") { return 36 }
        return 0
    }
}
