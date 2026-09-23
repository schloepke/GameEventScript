// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using PrettyPrompt;
using PrettyPrompt.Consoles;
using PrettyPrompt.Highlighting;

namespace GameEventScript.Tool;

internal sealed class RunPrompt : IDisposable
{
    private readonly bool _captureControlC = Console.TreatControlCAsInput;
    private readonly Prompt _prompt;
    private readonly RunHistory _history;

    internal RunPrompt(RunHistory history)
    {
        _history = history;
        _prompt = new(callbacks: new Callbacks(history), console: new ErrorConsole(), configuration: Configuration());
    }

    private static PromptConfiguration Configuration()
    {
        // Ctrl+N has its own control character; modified Enter may arrive as ordinary Enter.
        var newLine = new KeyPressPatterns(new(ConsoleModifiers.Control, ConsoleKey.N), new(ConsoleModifiers.Shift, ConsoleKey.Enter), new(ConsoleModifiers.Alt, ConsoleKey.Enter));
        return new PromptConfiguration(keyBindings: new KeyBindings(newLine: newLine, historyPrevious: new KeyPressPatterns(), historyNext: new KeyPressPatterns()), prompt: "ges> ", tabSize: ToolTextDisplay.TabSize);
    }

    internal string ReadLine()
    {
        while (true)
        {
            _history.Begin();
            var response = _prompt.ReadLineAsync().GetAwaiter().GetResult();
            if (response.IsSuccess) return response.Text;
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _prompt.DisposeAsync().AsTask().GetAwaiter().GetResult();
        Console.TreatControlCAsInput = _captureControlC;
    }

    private sealed class ErrorConsole : SystemConsole, IConsole
    {
        private readonly RunKeyReader _keys = new(() => Console.KeyAvailable, Console.ReadKey);

        bool IConsole.KeyAvailable => _keys.KeyAvailable;

        /// <inheritdoc />
        public override ConsoleKeyInfo ReadKey(bool intercept) => _keys.ReadKey(intercept);

        /// <inheritdoc />
        public override void Write(ReadOnlySpan<char> value) => Console.Error.Write(value);

        /// <inheritdoc />
        public override void WriteLine(ReadOnlySpan<char> value) => Console.Error.WriteLine(value);

        /// <inheritdoc />
        public override void Clear() => Console.Error.Write("\u001b[2J\u001b[H");
    }

    private sealed class Callbacks(RunHistory history) : PromptCallbacks
    {
        /// <inheritdoc />
        protected override Task<(string Text, int Caret)> FormatInput(string text, int caret, KeyPress keyPress, CancellationToken cancellationToken)
        {
            if (keyPress.ConsoleKeyInfo is { Modifiers: 0, Key: ConsoleKey.UpArrow or ConsoleKey.DownArrow } key)
            {
                text = history.Move(text, key.Key == ConsoleKey.UpArrow);
                caret = text.Length;
            }
            return Task.FromResult((text, caret));
        }

        /// <inheritdoc />
        protected override Task<IReadOnlyCollection<FormatSpan>> HighlightCallbackAsync(string text, CancellationToken cancellationToken)
        {
            IReadOnlyCollection<FormatSpan> spans = RunHighlighting.Highlight(text).Select(span => new FormatSpan(span.Start, span.Length, Color(span.Color))).ToArray();
            return Task.FromResult(spans);
        }

        /// <inheritdoc />
        protected override IEnumerable<(KeyPressPattern Pattern, KeyPressCallbackAsync Callback)> GetKeyPressCallbacks()
        {
            yield return (new KeyPressPattern(ConsoleModifiers.Control, ConsoleKey.D), (text, _, _) => Task.FromResult<KeyPressCallbackResult?>(text.Length == 0 ? new KeyPressCallbackResult(":quit", null) : null));
        }

        private static AnsiColor Color(int color) => color switch
        {
            32 => AnsiColor.Green,
            33 => AnsiColor.Yellow,
            34 => AnsiColor.Blue,
            35 => AnsiColor.Magenta,
            36 => AnsiColor.Cyan,
            _ => AnsiColor.BrightBlack
        };
    }
}
