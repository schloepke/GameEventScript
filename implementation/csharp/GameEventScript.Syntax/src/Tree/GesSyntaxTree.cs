// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;

namespace GameEventScript.Syntax;

/// <summary>Kinds of syntax nodes; tokens keep their own <see cref="GesTokenKind"/>.</summary>
internal enum GesSyntaxNodeKind : byte
{
    CompilationUnit,
    ModuleDeclaration,
    ConstantDefinition,
    RecordDefinition,
    RecordField,
    PredicateDefinition,
    FunctionDefinition,
    ParameterList,
    Parameter,
    TypeReference,
    EventHandler,
    HandlerTagFilter,
    Block,
    EmitStatement,
    PublishStatement,
    LetStatement,
    IfStatement,
    IfCondition,
    ElseClause,
    ForStatement,
    SeededRandomStatement,
    ExpressionStatement,
    AssemblyBlock,
    TagClause,
    SendExpression,
    ArgumentList,
    Argument,
    GuardedChoiceExpression,
    ChoiceBranch,
    BinaryExpression,
    UnaryExpression,
    BetweenExpression,
    MembershipExpression,
    TypeOperationExpression,
    DicePattern,
    PowerExpression,
    SeriesExpression,
    IntrinsicCallExpression,
    MemberAccessExpression,
    CollectionAccessExpression,
    CollectionSelector,
    ObjectPattern,
    ObjectPatternEntry,
    ParenthesizedExpression,
    LiteralExpression,
    NameExpression,
    MessageExpression,
    CallExpression,
    HandlerLiteralExpression,
    ListLiteralExpression,
    DiceLiteralExpression,
    CollectionFactoryExpression,
    GeneratedCollectionExpression,
    MapLiteralExpression,
    MapEntry,
    RandomExpression,
    RangeExpression,
    SeededRandomExpression,
    DiceExpression,
    ExtensionCallExpression,
    TypeConstructorExpression,
    ClampExpression,
    VariadicExpression,
    Error
}

/// <summary>
/// A node of the lossless syntax tree. It spans the tokens [<see cref="FirstToken"/>, <see cref="EndToken"/>) of its tree;
/// its own tokens are those not covered by a child.
/// </summary>
internal sealed class GesSyntaxNode(GesSyntaxNodeKind kind, int firstToken, int endToken, GesSyntaxNode[] children)
{
    internal GesSyntaxNodeKind Kind { get; } = kind;

    internal int FirstToken { get; } = firstToken;

    internal int EndToken { get; } = endToken;

    internal ChildList Children { get; } = new(children);

    // The tokens an expression node's diagnostics range over, which can differ from its own tokens: a parenthesized
    // expression ranges over its content and a member access over its target. -1 without such a range.
    internal int RangeStart { get; private set; } = -1;

    internal int RangeEnd { get; private set; } = -1;

    internal void SetRange(int start, int end)
    {
        RangeStart = start;
        RangeEnd = end;
    }

    /// <summary>
    /// The read-only children of a node. A struct with its own enumerator, so walking a tree neither boxes an enumerator per
    /// node nor calls through an interface.
    /// </summary>
    internal readonly struct ChildList(GesSyntaxNode[] nodes)
    {
        internal int Count => nodes.Length;

        internal GesSyntaxNode this[int index] => nodes[index];

        public Enumerator GetEnumerator() => new(nodes);

        public struct Enumerator(GesSyntaxNode[] nodes)
        {
            private int _index = -1;

            public readonly GesSyntaxNode Current => nodes[_index];

            public bool MoveNext() => ++_index < nodes.Length;
        }
    }
}

/// <summary>The kind of declaration a diagnostic's symbol names; the Compiler maps it to its diagnostic symbol kinds.</summary>
internal enum GesSyntaxSymbolKind : byte
{
    Unknown,
    Type,
    Predicate,
    Function,
    Handler,
    Message,
    Variable,
    GlobalDefinition
}

/// <summary>The role a name token plays, recorded while parsing for validation and highlighting.</summary>
internal enum GesSyntaxRole : byte
{
    None,
    Variable,
    LetBinding,
    ConditionBinding,
    LoopBinding,
    SelectorBinding,
    WeightBinding,
    FoldAccumulator,
    GeneratedBinding,
    AssemblyDeclaration,
    Parameter,
    UnlabeledParameter,
    Field,
    UnlabeledField,
    Callable,
    CallTarget,
    PredicateTarget,
    ArgumentLabel,
    Member,
    MapKey,
    MessageName,
    // An identifier the grammar reads as a contextual keyword, such as `between`.
    Keyword,
    Opcode,
    AssemblyLabel
}

/// <summary>
/// A source with its tokens and syntax nodes; its tokens, including their trivia, reproduce the source exactly. A tree
/// exists also for a source with syntax errors, which its diagnostics report in source order.
/// </summary>
internal sealed class GesSyntaxTree(string text, IReadOnlyList<GesToken> tokens, GesSyntaxNode root, IReadOnlyList<GesSyntaxDiagnostic>? diagnostics = null,
    IReadOnlyList<GesSyntaxRole>? roles = null)
{
    internal string Text { get; } = text;

    internal IReadOnlyList<GesToken> Tokens { get; } = tokens;

    // The role of each token; None for tokens that name nothing.
    internal IReadOnlyList<GesSyntaxRole> Roles { get; } = roles ?? [];

    internal GesSyntaxNode Root { get; } = root;

    internal IReadOnlyList<GesSyntaxDiagnostic> Diagnostics { get; } = diagnostics ?? [];

    /// <summary>
    /// The tokens of a tree, stored in blocks of fixed size. Growing adds a block and copies nothing, because sources vary
    /// too much in tokens per character for one up-front capacity: a growing list would copy its large tokens and keep the
    /// abandoned arrays as garbage.
    /// </summary>
    internal sealed class TokenList : IReadOnlyList<GesToken>
    {
        private const int BlockShift = 5;
        private const int BlockSize = 1 << BlockShift;
        private const int BlockMask = BlockSize - 1;
        private GesToken[][] _blocks = new GesToken[4][];

        public int Count { get; private set; }

        public GesToken this[int index]
        {
            get
            {
                if ((uint)index >= (uint)Count) throw new ArgumentOutOfRangeException(nameof(index));
                return _blocks[index >> BlockShift][index & BlockMask];
            }
        }

        internal void Add(GesToken token)
        {
            var block = Count >> BlockShift;
            if ((Count & BlockMask) == 0)
            {
                if (block == _blocks.Length) Array.Resize(ref _blocks, _blocks.Length * 2);
                _blocks[block] = new GesToken[BlockSize];
            }

            _blocks[block][Count & BlockMask] = token;
            Count++;
        }

        public IEnumerator<GesToken> GetEnumerator()
        {
            for (var index = 0; index < Count; index++) yield return _blocks[index >> BlockShift][index & BlockMask];
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
}

/// <summary>
/// A syntax error of one source, with the module name declared before it. Lines and columns are one-based; columns
/// count Unicode scalars, as token positions do.
/// </summary>
internal sealed record GesSyntaxDiagnostic(string Code, string Message, int Line, int Column, int EndLine, int EndColumn, string ModuleName,
    string? Symbol = null, GesSyntaxSymbolKind SymbolKind = GesSyntaxSymbolKind.Unknown)
{
    internal const string Syntax = "parse.syntax";
    internal const string SourceNestingExceeded = "parse.sourceNestingExceeded";
}

/// <summary>
/// Builds a syntax tree while a parser consumes tokens. A checkpoint taken before a token lets a later node start there,
/// so a node can wrap children that were parsed before its kind was known.
/// </summary>
internal sealed class GesSyntaxTreeBuilder
{
    private readonly GesSyntaxTree.TokenList _tokens = new();
    private readonly List<GesSyntaxRole> _roles;

    // A source has roughly one token per two to four characters; the one-byte roles may overestimate.
    internal GesSyntaxTreeBuilder(int sourceLength = 0) => _roles = new List<GesSyntaxRole>(sourceLength / 2 + 8);
    private readonly List<GesSyntaxNode> _children = [];
    private readonly List<(GesSyntaxNodeKind Kind, int FirstToken, int FirstChild)> _open = [];

    internal readonly record struct Checkpoint(int Token, int Child);

    internal int Depth => _open.Count;

    internal int TokenCount => _tokens.Count;

    internal GesToken TokenAt(int index) => _tokens[index];

    internal Checkpoint Mark() => new(_tokens.Count, _children.Count);

    internal void Token(GesToken token)
    {
        _tokens.Add(token);
        _roles.Add(GesSyntaxRole.None);
    }

    internal void SetRole(int token, GesSyntaxRole role) => _roles[token] = role;

    // Sets the diagnostic range of the node completed last.
    internal void SetLastRange(int start, int end) => _children[^1].SetRange(start, end);

    internal void StartNode(GesSyntaxNodeKind kind) => _open.Add((kind, _tokens.Count, _children.Count));

    internal void StartNodeAt(Checkpoint checkpoint, GesSyntaxNodeKind kind)
    {
        if (checkpoint.Child > _children.Count || (_open.Count > 0 && checkpoint.Child < _open[^1].FirstChild))
            throw new InvalidOperationException("A checkpoint must lie inside the innermost open node.");
        _open.Add((kind, checkpoint.Token, checkpoint.Child));
    }

    internal void FinishNode()
    {
        var (kind, firstToken, firstChild) = _open[^1];
        _open.RemoveAt(_open.Count - 1);
        var count = _children.Count - firstChild;
        var children = count == 0 ? Array.Empty<GesSyntaxNode>() : new GesSyntaxNode[count];
        _children.CopyTo(firstChild, children, 0, count);
        _children.RemoveRange(firstChild, count);
        _children.Add(new GesSyntaxNode(kind, firstToken, _tokens.Count, children));
    }

    // Drops the nodes opened or completed since the checkpoint, taken at the given depth, so that its tokens can form one
    // error node; the tokens themselves stay.
    internal void DiscardTo(int depth, Checkpoint checkpoint)
    {
        _open.RemoveRange(depth, _open.Count - depth);
        _children.RemoveRange(checkpoint.Child, _children.Count - checkpoint.Child);
    }

    // Closes the nodes a failed production left open, down to the depth it started at.
    internal void FinishTo(int depth)
    {
        while (_open.Count > depth) FinishNode();
    }

    internal GesSyntaxTree Finish(string text, IReadOnlyList<GesSyntaxDiagnostic>? diagnostics = null)
    {
        FinishTo(0);
        var root = _children.Count == 1 && _children[0].Kind == GesSyntaxNodeKind.CompilationUnit
            ? _children[0]
            : new GesSyntaxNode(GesSyntaxNodeKind.CompilationUnit, 0, _tokens.Count, _children.ToArray());
        // The end of file has no role.
        while (_roles.Count < _tokens.Count) _roles.Add(GesSyntaxRole.None);
        return new GesSyntaxTree(text, _tokens, root, diagnostics, _roles);
    }
}
