// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;

namespace GameEventScript.SyntaxHighlighter;

/// <summary>Reusable GES/GESA TextMate highlighting without a Compiler, Runtime, UI or terminal dependency.</summary>
public sealed class GameEventScriptSyntaxHighlighter
{
    private static readonly GrammarRule[] Rules = EmbeddedGrammars.CreateRules();
    /// <summary>The selected bundled grammar.</summary>
    public GameEventScriptSyntaxLanguage Language { get; }
    /// <summary>Maximum UTF-16 input length for one call; larger inputs return an incomplete result.</summary>
    public int MaxInputLength { get; }
    private readonly int[] _roots;
    private readonly string _rootScope;

    /// <summary>Creates a highlighter. Matching has a cooperative 1000ms per-call budget, plus a 100ms per-regex bound.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The language is unknown or maxInputLength is not positive.</exception>
    public GameEventScriptSyntaxHighlighter(GameEventScriptSyntaxLanguage language = GameEventScriptSyntaxLanguage.Ges, int maxInputLength = 262144)
    {
        if (language is not (GameEventScriptSyntaxLanguage.Ges or GameEventScriptSyntaxLanguage.Gesa)) throw new ArgumentOutOfRangeException(nameof(language));
        if (maxInputLength <= 0) throw new ArgumentOutOfRangeException(nameof(maxInputLength));
        Language = language;
        MaxInputLength = maxInputLength;
        _roots = language == GameEventScriptSyntaxLanguage.Ges ? EmbeddedGrammars.Source : EmbeddedGrammars.Assembly;
        _rootScope = language == GameEventScriptSyntaxLanguage.Ges ? "source.gameeventscript" : "source.gameeventscript.assembler";
    }

    /// <summary>Creates an empty line-boundary state. States are immutable and can be retained by editors.</summary>
    public GameEventScriptHighlightState CreateState() => new(Language, []);

    /// <summary>Highlights a complete document, preserving CR, LF, CRLF and all UTF-16 offsets. Invalid source remains highlightable.</summary>
    /// <exception cref="ArgumentNullException">text is null.</exception>
    public GameEventScriptHighlightResult Highlight(string text) => Run(text, null, false);

    /// <summary>Highlights one line with an optional trailing CR, LF or CRLF. Offsets are relative to this line.</summary>
    /// <remarks>Supply the previous line's successful NextState. Reprocess subsequent lines until text and state converge.</remarks>
    /// <exception cref="ArgumentException">The text contains an interior line ending, or the state belongs to another language.</exception>
    /// <exception cref="ArgumentNullException">text is null.</exception>
    public GameEventScriptHighlightResult HighlightLine(string text, GameEventScriptHighlightState? state = null) => Run(text, state, true);

    private GameEventScriptHighlightResult Run(string text, GameEventScriptHighlightState? state, bool singleLine)
    {
        if (text is null) throw new ArgumentNullException(nameof(text));
        if (state is not null && state.Language != Language) throw new ArgumentException("State belongs to another language.", nameof(state));
        if (singleLine && text.Substring(0, ContentLength(text)).IndexOfAny(['\r', '\n']) >= 0) throw new ArgumentException("Expected one line.", nameof(text));
        if (text.Length > MaxInputLength) return new([], null);
        var clock = Stopwatch.StartNew();
        var frames = new List<int>(state?.Frames ?? []);
        var spans = new List<GameEventScriptHighlightSpan>();
        try
        {
            var start = 0;
            do
            {
                var end = start;
                while (end < text.Length && text[end] is not ('\r' or '\n')) end++;
                if (end < text.Length && text[end++] == '\r' && end < text.Length && text[end] == '\n') end++;
                var line = text.Substring(start, end - start);
                if (!Line(line, start, frames, spans, clock)) return new([], null);
                start = end;
            } while (start < text.Length);
            return new(spans, new(Language, frames));
        }
        catch (RegexMatchTimeoutException) { return new([], null); }
    }

    private bool Line(string text, int offset, List<int> frames, List<GameEventScriptHighlightSpan> spans, Stopwatch clock)
    {
        var length = ContentLength(text);
        var content = text.Substring(0, length);
        var cache = new Dictionary<int, Match>();
        Match Find(int key, Regex regex, int position)
        {
            if (!cache.TryGetValue(key, out var value) || value.Success && value.Index < position)
                cache[key] = value = regex.Match(content, position);
            return value;
        }
        var position = 0;
        while (position <= length)
        {
            if (clock.ElapsedMilliseconds > 1000 || frames.Count > 128) return false;
            var scopes = Context(frames);
            var parent = frames.Count == 0 ? -1 : frames[frames.Count - 1];
            Match? match = parent < 0 ? null : Find(parent * 2 + 1, Rules[parent].End!, position);
            if (match is not null && !match.Success) match = null;
            var ruleId = parent;
            var ending = match is not null;
            foreach (var id in parent < 0 ? _roots : Rules[parent].Children)
            {
                var candidate = Find(id * 2, Rules[id].Pattern, position);
                if (candidate.Success && (match is null || candidate.Index < match.Index || candidate.Index == match.Index && ending && Rules[parent].EndLast))
                {
                    match = candidate;
                    ruleId = id;
                    ending = false;
                }
            }
            if (match is null)
            {
                Add(spans, offset + position, length - position, scopes);
                break;
            }
            Add(spans, offset + position, match.Index - position, scopes);
            var rule = Rules[ruleId];
            if (ending)
            {
                if (rule.ContentName.Length != 0) scopes.RemoveAt(scopes.Count - 1);
                Emit(spans, offset, match, scopes, rule.EndCaptures);
                frames.RemoveAt(frames.Count - 1);
            }
            else
            {
                if (match.Length == 0) return false;
                if (rule.Name.Length != 0) scopes.Add(rule.Name);
                Emit(spans, offset, match, scopes, rule.End is null ? rule.Captures : rule.BeginCaptures);
                if (rule.End is not null) frames.Add(ruleId);
            }
            position = match.Index + match.Length;
        }
        Add(spans, offset + length, text.Length - length, Context(frames));
        return clock.ElapsedMilliseconds <= 1000;
    }

    private List<string> Context(List<int> frames)
    {
        var scopes = new List<string> { _rootScope };
        foreach (var id in frames)
        {
            if (Rules[id].Name.Length != 0) scopes.Add(Rules[id].Name);
            if (Rules[id].ContentName.Length != 0) scopes.Add(Rules[id].ContentName);
        }
        return scopes;
    }

    private static void Emit(List<GameEventScriptHighlightSpan> spans, int offset, Match match, List<string> scopes, CaptureRule[] captures)
    {
        var groups = captures.Where(c => c.Group < match.Groups.Count && match.Groups[c.Group].Success && match.Groups[c.Group].Length > 0)
            .Select(c => (Range: match.Groups[c.Group], c.Scope)).OrderByDescending(c => c.Range.Length).ToArray();
        var boundaries = new SortedSet<int> { match.Index, match.Index + match.Length };
        foreach (var group in groups)
        {
            boundaries.Add(group.Range.Index);
            boundaries.Add(group.Range.Index + group.Range.Length);
        }
        var points = boundaries.ToArray();
        for (var i = 0; i + 1 < points.Length; i++)
        {
            var names = new List<string>(scopes);
            foreach (var group in groups)
                if (group.Range.Index <= points[i] && points[i] < group.Range.Index + group.Range.Length) names.Add(group.Scope);
            Add(spans, offset + points[i], points[i + 1] - points[i], names);
        }
    }

    private static void Add(List<GameEventScriptHighlightSpan> spans, int start, int length, List<string> scopes)
    {
        if (length <= 0) return;
        if (spans.Count > 0)
        {
            var previous = spans[spans.Count - 1];
            if (previous.Start + previous.Length == start && previous.Scopes.SequenceEqual(scopes))
            {
                spans[spans.Count - 1] = new(previous.Start, previous.Length + length, scopes.ToArray());
                return;
            }
        }
        spans.Add(new(start, length, scopes.ToArray()));
    }

    private static int ContentLength(string text)
    {
        var length = text.Length;
        if (length > 0 && text[length - 1] == '\n') length--;
        if (length > 0 && text[length - 1] == '\r') length--;
        return length;
    }
}

internal sealed class CaptureRule(int group, string scope)
{
    internal int Group { get; } = group;
    internal string Scope { get; } = scope;
}

internal sealed class GrammarRule
{
    internal Regex Pattern { get; }
    internal Regex? End { get; }
    internal string Name { get; }
    internal string ContentName { get; }
    internal CaptureRule[] Captures { get; }
    internal CaptureRule[] BeginCaptures { get; }
    internal CaptureRule[] EndCaptures { get; }
    internal int[] Children { get; }
    internal bool EndLast { get; }

    internal GrammarRule(string pattern, string end, string name, string contentName, CaptureRule[] captures, CaptureRule[] beginCaptures, CaptureRule[] endCaptures, int[] children, bool endLast)
    {
        const RegexOptions options = RegexOptions.CultureInvariant | RegexOptions.Multiline;
        Pattern = new(pattern, options, TimeSpan.FromMilliseconds(100));
        End = end.Length == 0 ? null : new(end, options, TimeSpan.FromMilliseconds(100));
        Name = name;
        ContentName = contentName;
        Captures = captures;
        BeginCaptures = beginCaptures;
        EndCaptures = endCaptures;
        Children = children;
        EndLast = endLast;
    }
}
