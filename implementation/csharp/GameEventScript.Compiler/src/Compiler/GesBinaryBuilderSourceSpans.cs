// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using GameEventScript.Api;

namespace GameEventScript.Compiler;

internal sealed partial class GesBinaryBuilder
{
    private GesSourceLocation? _currentSourceRange;

    internal GesSourceLocation? CurrentSourceRange => _currentSourceRange;

    public GesBinaryBuilder SetSourceRange(GesSourceLocation? sourceRange)
    {
        _currentSourceRange = sourceRange;
        return this;
    }

    public GesBinaryBuilder ClearSourceRange()
        => SetSourceRange(null);

    public GesBinarySourceRangeScope SourceRange(GesSourceLocation? sourceRange)
        => new(this, sourceRange);

    internal sealed class GesBinarySourceRangeScope : IDisposable
    {
        private readonly GesBinaryBuilder _builder;
        private readonly GesSourceLocation? _previousSourceRange;
        private bool _disposed;

        internal GesBinarySourceRangeScope(GesBinaryBuilder builder, GesSourceLocation? sourceRange)
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
