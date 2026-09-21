// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using GameEventScript.Api;

namespace GameEventScript.Compiler;

internal sealed partial class GesBinaryBuilder
{
    private GameEventScriptSourceLocation? _currentSourceRange;

    internal GameEventScriptSourceLocation? CurrentSourceRange => _currentSourceRange;

    public GesBinaryBuilder SetSourceRange(GameEventScriptSourceLocation? sourceRange)
    {
        _currentSourceRange = sourceRange;
        return this;
    }

    public GesBinaryBuilder ClearSourceRange()
        => SetSourceRange(null);

    public GesBinarySourceRangeScope SourceRange(GameEventScriptSourceLocation? sourceRange)
        => new(this, sourceRange);

    internal sealed class GesBinarySourceRangeScope : IDisposable
    {
        private readonly GesBinaryBuilder _builder;
        private readonly GameEventScriptSourceLocation? _previousSourceRange;
        private bool _disposed;

        internal GesBinarySourceRangeScope(GesBinaryBuilder builder, GameEventScriptSourceLocation? sourceRange)
        {
            _builder = builder;
            _previousSourceRange = builder._currentSourceRange;
            builder._currentSourceRange = sourceRange;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _builder._currentSourceRange = _previousSourceRange;
        }
    }
}
