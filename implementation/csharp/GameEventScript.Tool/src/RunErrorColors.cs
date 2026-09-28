// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System.Text;

namespace GameEventScript.Tool;

// Scope coloring to this invocation; source dumps and ordinary output remain untouched.
internal sealed class RunErrorColors : TextWriter
{
    private readonly TextWriter _original = Console.Error;
    internal bool Enabled { get; set; }
    internal RunErrorColors() => Console.SetError(this);
    /// <inheritdoc />
    public override Encoding Encoding => _original.Encoding;
    /// <inheritdoc />
    public override void Write(char value) => _original.Write(value);
    /// <inheritdoc />
    public override void Write(string? value) => _original.Write(value);
    /// <inheritdoc />
    public override void Write(ReadOnlySpan<char> value) => _original.Write(value);
    /// <inheritdoc />
    public override void WriteLine(string? value)
    {
        var diagnostic = value is not null && (value.StartsWith("error ", StringComparison.Ordinal) || value.Contains(": error ", StringComparison.Ordinal));
        _original.WriteLine(Enabled && diagnostic ? RunHighlighting.Paint(value!, 31) : value);
    }
    /// <inheritdoc />
    public override void Flush() => _original.Flush();
    /// <inheritdoc />
    protected override void Dispose(bool disposing) { Console.SetError(_original); base.Dispose(disposing); }
}
