// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import Foundation

/// Reusable TextMate highlighting without a Compiler, Runtime, UI or terminal dependency. Matching uses Foundation internally.
public final class GameEventScriptSyntaxHighlighter: Sendable {
    /// The selected bundled grammar.
    public let language: GameEventScriptSyntaxLanguage
    /// Maximum UTF-16 input length per call; larger inputs return an incomplete result.
    public let maxInputLength: Int
    private let roots: [Int]
    private let rootScope: String

    /// Creates a highlighter with a cooperative 1000ms per-call matching budget.
    /// - Throws: `invalidInputLimit` if the bound is not positive. Foundation progress checks are not a hard real-time deadline.
    public init(language: GameEventScriptSyntaxLanguage = .ges, maxInputLength: Int = 262_144) throws {
        guard maxInputLength > 0 else { throw GameEventScriptHighlightingError.invalidInputLimit }
        self.language = language
        self.maxInputLength = maxInputLength
        roots = language == .ges ? Grammar.shared.source : Grammar.shared.assembly
        rootScope = language == .ges ? "source.gameeventscript" : "source.gameeventscript.assembler"
    }

    /// Creates an empty, immutable line-boundary state that editors may retain.
    public func createState() -> GameEventScriptHighlightState { .init(language: language, frames: []) }

    /// Highlights a complete document, preserving CR, LF, CRLF and all UTF-16 offsets. Invalid code remains highlightable.
    public func highlight(_ text: String) -> GameEventScriptHighlightResult { run(text, state: nil) }

    /// Highlights one line with an optional trailing CR, LF or CRLF; offsets are relative to this line.
    /// Supply the previous line's successful nextState and reprocess subsequent lines until text and state converge.
    /// - Throws: `expectedSingleLine` for interior line endings, or `incompatibleState` for a different grammar.
    public func highlightLine(_ text: String, state: GameEventScriptHighlightState? = nil) throws -> GameEventScriptHighlightResult {
        if let state, state.language != language { throw GameEventScriptHighlightingError.incompatibleState }
        let source = text as NSString
        let content = source.substring(to: Self.contentLength(source))
        if content.contains("\r") || content.contains("\n") { throw GameEventScriptHighlightingError.expectedSingleLine }
        return run(text, state: state)
    }

    private func run(_ text: String, state: GameEventScriptHighlightState?) -> GameEventScriptHighlightResult {
        guard text.utf16.count <= maxInputLength else { return .init(spans: [], nextState: nil) }
        let deadline = ProcessInfo.processInfo.systemUptime + 1.0
        let source = text as NSString
        var frames = state?.frames ?? []
        var spans: [GameEventScriptHighlightSpan] = []
        var start = 0
        repeat {
            var end = start
            while end < source.length && source.character(at: end) != 13 && source.character(at: end) != 10 { end += 1 }
            if end < source.length {
                let cr = source.character(at: end) == 13
                end += 1
                if cr && end < source.length && source.character(at: end) == 10 { end += 1 }
            }
            let text = source.substring(with: NSRange(location: start, length: end - start))
            guard line(text, offset: start, frames: &frames, spans: &spans, deadline: deadline) else { return .init(spans: [], nextState: nil) }
            start = end
        } while start < source.length
        return .init(spans: spans, nextState: .init(language: language, frames: frames))
    }

    private func line(_ text: String, offset: Int, frames: inout [Int], spans: inout [GameEventScriptHighlightSpan], deadline: Double) -> Bool {
        let source = text as NSString
        let length = Self.contentLength(source)
        let content = source.substring(to: length)
        var cache: [Int: CachedMatch] = [:]
        var expired = false

        func find(_ key: Int, _ regex: NSRegularExpression, _ position: Int) -> NSTextCheckingResult? {
            if let previous = cache[key], previous.match == nil || previous.match!.range.location >= position { return previous.match }
            var result: NSTextCheckingResult?
            regex.enumerateMatches(in: content, options: [.reportProgress, .withTransparentBounds, .withoutAnchoringBounds], range: NSRange(location: position, length: length - position)) { match, _, stop in
                if ProcessInfo.processInfo.systemUptime > deadline {
                    expired = true
                    stop.pointee = true
                } else if let match {
                    result = match
                    stop.pointee = true
                }
            }
            cache[key] = CachedMatch(match: result)
            return result
        }

        let rules = Grammar.shared.rules
        var position = 0
        while position <= length {
            if expired || ProcessInfo.processInfo.systemUptime > deadline || frames.count > 128 { return false }
            var scopes = context(frames)
            let parent = frames.last
            var match = parent.flatMap { find($0 * 2 + 1, rules[$0].end!, position) }
            var ruleID = parent ?? 0
            var ending = match != nil
            for id in parent.map({ rules[$0].children }) ?? roots {
                if let candidate = find(id * 2, rules[id].pattern, position),
                    match == nil || candidate.range.location < match!.range.location || candidate.range.location == match!.range.location && ending && rules[parent!].endLast
                {
                    match = candidate
                    ruleID = id
                    ending = false
                }
            }
            guard let match else {
                Self.add(&spans, offset + position, length - position, scopes)
                break
            }
            Self.add(&spans, offset + position, match.range.location - position, scopes)
            let rule = rules[ruleID]
            if ending {
                if !rule.contentName.isEmpty { scopes.removeLast() }
                Self.emit(&spans, offset, match, scopes, rule.endCaptures)
                frames.removeLast()
            } else {
                if match.range.length == 0 { return false }
                if !rule.name.isEmpty { scopes.append(rule.name) }
                Self.emit(&spans, offset, match, scopes, rule.end == nil ? rule.captures : rule.beginCaptures)
                if rule.end != nil { frames.append(ruleID) }
            }
            position = NSMaxRange(match.range)
        }
        Self.add(&spans, offset + length, source.length - length, context(frames))
        return !expired && ProcessInfo.processInfo.systemUptime <= deadline
    }

    private func context(_ frames: [Int]) -> [String] {
        var scopes = [rootScope]
        for id in frames {
            let rule = Grammar.shared.rules[id]
            if !rule.name.isEmpty { scopes.append(rule.name) }
            if !rule.contentName.isEmpty { scopes.append(rule.contentName) }
        }
        return scopes
    }

    private static func emit(_ spans: inout [GameEventScriptHighlightSpan], _ offset: Int, _ match: NSTextCheckingResult, _ scopes: [String], _ captures: [CaptureRule]) {
        let groups = captures.compactMap { capture -> (NSRange, String, Int)? in
            guard capture.group < match.numberOfRanges else { return nil }
            let range = match.range(at: capture.group)
            return range.location == NSNotFound || range.length == 0 ? nil : (range, capture.scope, capture.group)
        }.sorted { $0.0.length != $1.0.length ? $0.0.length > $1.0.length : $0.2 < $1.2 }
        var boundaries: Set<Int> = [match.range.location, NSMaxRange(match.range)]
        for (range, _, _) in groups { boundaries.formUnion([range.location, NSMaxRange(range)]) }
        let points = boundaries.sorted()
        for (start, end) in zip(points, points.dropFirst()) {
            let names = scopes + groups.filter { NSLocationInRange(start, $0.0) }.map { $0.1 }
            add(&spans, offset + start, end - start, names)
        }
    }

    private static func add(_ spans: inout [GameEventScriptHighlightSpan], _ start: Int, _ length: Int, _ scopes: [String]) {
        guard length > 0 else { return }
        if let previous = spans.last, previous.start + previous.length == start, previous.scopes == scopes {
            spans[spans.count - 1] = .init(previous.start, previous.length + length, scopes)
        } else {
            spans.append(.init(start, length, scopes))
        }
    }

    private static func contentLength(_ text: NSString) -> Int {
        var length = text.length
        if length > 0 && text.character(at: length - 1) == 10 { length -= 1 }
        if length > 0 && text.character(at: length - 1) == 13 { length -= 1 }
        return length
    }
}

private struct CachedMatch { let match: NSTextCheckingResult? }

struct CaptureRule {
    let group: Int
    let scope: String
}

final class GrammarRule {
    let pattern: NSRegularExpression
    let end: NSRegularExpression?
    let name: String
    let contentName: String
    let captures: [CaptureRule]
    let beginCaptures: [CaptureRule]
    let endCaptures: [CaptureRule]
    let children: [Int]
    let endLast: Bool

    init(_ data: [String: Any]) {
        // Physical CR/LF lines are split before matching. Other Unicode separators
        // must remain ordinary content, matching .NET Regex's LF-only line semantics.
        let options: NSRegularExpression.Options = [.anchorsMatchLines, .useUnixLineSeparators]
        pattern = try! NSRegularExpression(pattern: data["pattern"] as! String, options: options)
        let ending = data["end"] as! String
        end = ending.isEmpty ? nil : try! NSRegularExpression(pattern: ending, options: options)
        name = data["name"] as! String
        contentName = data["contentName"] as! String

        func groups(_ key: String) -> [CaptureRule] { (data[key] as! [[Any]]).map { CaptureRule(group: $0[0] as! Int, scope: $0[1] as! String) } }

        captures = groups("captures")
        beginCaptures = groups("beginCaptures")
        endCaptures = groups("endCaptures")
        children = data["children"] as! [Int]
        endLast = data["endLast"] as! Bool
    }
}

// Immutable compiled rules. NSRegularExpression is immutable; matching state remains local to each call.
final class Grammar: @unchecked Sendable {
    static let shared = Grammar()
    let source: [Int]
    let assembly: [Int]
    let rules: [GrammarRule]

    private init() {
        let data = try! JSONSerialization.jsonObject(with: Data(EmbeddedGrammars.json.utf8)) as! [String: Any]
        source = data["source"] as! [Int]
        assembly = data["assembly"] as! [Int]
        rules = (data["rules"] as! [[String: Any]]).map(GrammarRule.init)
    }
}
