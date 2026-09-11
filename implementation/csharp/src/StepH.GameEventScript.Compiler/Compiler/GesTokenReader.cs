// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;

namespace StepH.GameEventScript.Compiler;

internal sealed class GesTokenReader
{
    private readonly GesLexer _lexer;
    private readonly List<GesToken> _lookahead = [];
    private GesToken _current;
    private GesToken _previous;
    private GesToken _firstToken;
    private GesToken _lastNonEofToken;
    private GesToken _eofToken;
    private bool _hasEofToken;

    public GesTokenReader(GesLexer lexer)
    {
        _lexer = lexer ?? throw new ArgumentNullException(nameof(lexer));
        _current = ReadToken();
        _previous = _current;
        _firstToken = _current;
        _lastNonEofToken = _current.Kind == GesTokenKind.EndOfFile ? _current : _current;
    }

    public GesToken Current => _current;

    public GesToken Previous => _previous;

    public GesToken FirstToken => _firstToken;

    public GesToken LastNonEofToken => _lastNonEofToken;

    public GesToken Advance()
    {
        var consumed = _current;
        _previous = consumed;
        if (_current.Kind != GesTokenKind.EndOfFile)
        {
            _current = TakeNextToken();
        }

        return consumed;
    }

    public GesToken Peek(int offset)
    {
        if (offset <= 0)
        {
            return _current;
        }

        while (_lookahead.Count < offset)
        {
            _lookahead.Add(ReadToken());
        }

        return _lookahead[offset - 1];
    }

    public GesToken PeekSignificant(int offset)
    {
        var seen = 0;
        var lookahead = 0;
        while (true)
        {
            var token = Peek(lookahead);
            if (token.Kind != GesTokenKind.NewLine)
            {
                if (seen == offset)
                {
                    return token;
                }

                seen++;
            }

            if (token.Kind == GesTokenKind.EndOfFile)
            {
                return token;
            }

            lookahead++;
        }
    }

    private GesToken TakeNextToken()
    {
        if (_lookahead.Count == 0)
        {
            return ReadToken();
        }

        var token = _lookahead[0];
        _lookahead.RemoveAt(0);
        return token;
    }

    private GesToken ReadToken()
    {
        if (_hasEofToken)
        {
            return _eofToken;
        }

        var token = _lexer.ReadNextToken();
        if (token.Kind == GesTokenKind.EndOfFile)
        {
            _eofToken = token;
            _hasEofToken = true;
        }
        else
        {
            _lastNonEofToken = token;
        }

        return token;
    }
}
