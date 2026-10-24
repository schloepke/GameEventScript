// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System.Text;
using System.Text.Json;
using GameEventScript.Api;
using GameEventScript.Runtime.Values;

namespace GameEventScript.Tool;

internal sealed class RunObserver(bool verbose, bool color, bool interactive, RunClock clock) : IGesRuntimeObserver
{
    internal bool Failed { get; private set; }
    internal IOException? OutputError { get; private set; }
    internal int ScriptExitCode { get; private set; }

    internal void SubscribeConsoleHandlers(RunInventory inventory)
    {
        inventory.SubscribeMessageName("ConsoleOut", new ConsoleHandler(this, false));
        inventory.SubscribeMessageName("ConsoleErr", new ConsoleHandler(this, true));
        inventory.SubscribeMessageName("ErrorCode", new ErrorCodeHandler(this));
    }

    /// <inheritdoc />
    public void MessageEmitted(GameEventScriptMessage message, long id, bool accepted)
    {
        if (verbose) WriteTrace($"emit {TraceId(id)}{Format(message)}{(accepted ? string.Empty : " [not queued]")}");
    }

    /// <inheritdoc />
    public void MessagePublished(GameEventScriptMessage message, long id, GesPublishResult result)
    {
        if (verbose) WriteTrace($"publish {TraceId(id)}{Format(message)}{(result.LocalAccepted ? string.Empty : " [not queued]")}");
    }

    /// <inheritdoc />
    public void DispatchStarted(GameEventScriptMessage message, long id, string dispatchSignatureId)
    {
        if (verbose) WriteTrace($"dispatch #{id} {dispatchSignatureId} <- {Format(message)}");
    }

    /// <inheritdoc />
    public void MessageScheduled(GameEventScriptMessage message, long id, long deadline, bool publish)
    {
        if (verbose) WriteTrace($"schedule #{id} {(publish ? "publish" : "emit")} {Format(message)} at {clock.FormatDeadline(deadline)}");
    }

    /// <inheritdoc />
    public void MessageDiscarded(long id)
    {
        if (verbose) WriteTrace($"discard #{id}");
    }

    /// <inheritdoc />
    public void RuntimeLimitReached(string limitName, string detail, int limit)
    {
        Failed = true;
        Write(Console.Error, $"error cli.runtimeLimit: {limitName}={limit}: {detail}");
    }

    /// <inheritdoc />
    public void RuntimeError(GesDiagnostic diagnostic)
    {
        Failed = true;
        try { WriteDiagnostic(diagnostic, "runtime"); }
        catch (IOException exception) { OutputError ??= exception; }
    }

    internal static void WriteDiagnostic(GesDiagnostic diagnostic, string fallback)
    {
        var location = diagnostic.SourceLocation;
        var prefix = location?.SourceName ?? fallback;
        if (location?.Line is { } line) prefix += $"({line},{location.Column ?? 1})";
        var context = new List<string>();
        if (diagnostic.ProgramName is { } program) context.Add($"program={program}");
        if (diagnostic.HandlerName is { } handler) context.Add($"handler={handler}");
        if (diagnostic.Symbol is { } symbol) context.Add($"symbol={symbol}");
        var suffix = context.Count == 0 ? string.Empty : " [" + string.Join(", ", context) + "]";
        Console.Error.WriteLine($"{prefix}: error {diagnostic.Code}: {diagnostic.Message}{suffix}");
    }

    // Sends without an ID (0) show none.
    private static string TraceId(long id) => id == 0 ? string.Empty : $"#{id} ";

    private void WriteTrace(string text) => Write(Console.Error, color && interactive ? RunHighlighting.Paint(text, 33) : text);

    private void Write(TextWriter writer, string text)
    {
        if (OutputError is not null) return;
        try { writer.WriteLine(text); }
        catch (IOException exception) { OutputError = exception; }
    }

    private static string Format(GameEventScriptMessage message)
    {
        var text = new StringBuilder(message.Name);
        if (message.Arguments.Count > 0)
        {
            text.Append('(');
            for (var index = 0; index < message.Arguments.Count; index++)
            {
                if (index > 0) text.Append(", ");
                var value = message.Arguments[index];
                text.Append(message.Arguments.NameAt(index)).Append(": ");
                text.Append(message.Arguments.KindAt(index) == GesBytecodeTypeKind.Text ? JsonSerializer.Serialize(value.AsText(), ToolJsonContext.Default.String) : value.ToString());
            }
            text.Append(')');
        }
        if (message.Tags.Count > 0) text.Append(" with #").AppendJoin(", #", message.Tags);
        return text.ToString();
    }

    private void WriteConsole(GameEventScriptMessage message, bool error)
    {
        var values = new string[message.Arguments.Count];
        for (var index = 0; index < values.Length; index++)
        {
            var value = message.Arguments[index];
            values[index] = color && !error ? ColorValue(value) : value.ToString();
        }
        var text = string.Concat(values);
        if (color && error) text = RunHighlighting.Paint(text, 31);
        Write(error ? Console.Error : Console.Out, text);
    }

    private static string ColorValue(GesValue value) => value.ValueKind switch
    {
        GesBytecodeTypeKind.Text => value.ToString(),
        GesBytecodeTypeKind.Integer or GesBytecodeTypeKind.Float or GesBytecodeTypeKind.Percentage => RunHighlighting.Paint(value.ToString(), 34),
        _ => RunHighlighting.Render(value.ToString())
    };

    private sealed class ConsoleHandler(RunObserver observer, bool error) : IGesNativeMessageHandler
    {
        /// <inheritdoc />
        public void Handle(GameEventScriptMessage message, GesContext context) => observer.WriteConsole(message, error);
    }

    private sealed class ErrorCodeHandler(RunObserver observer) : IGesNativeMessageHandler
    {
        /// <inheritdoc />
        public void Handle(GameEventScriptMessage message, GesContext context)
        {
            if (message.Arguments.Count != 1 || message.Arguments.NameAt(0) is not ("code" or "_")) throw InvalidCode();
            var value = message.Arguments[0];
            if (value.ValueKind == GesBytecodeTypeKind.Nothing)
            {
                observer.ScriptExitCode = 0;
                return;
            }
            if (value.ValueKind is not (GesBytecodeTypeKind.Integer or GesBytecodeTypeKind.Float)
                || value.ValueUnit != GesQuantityUnit.UnitNone) throw InvalidCode();
            var number = value.AsNumber();
            if (!double.IsFinite(number) || number < 0 || number > 255 || number != Math.Truncate(number)) throw InvalidCode();
            observer.ScriptExitCode = (int)number;
        }

        private static GesExtensionFaultException InvalidCode() => new("cli.errorCodeArgument", "ErrorCode requires one code argument: a unitless integer from 0 to 255, or nothing.");
    }
}
