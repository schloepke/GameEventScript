// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System.Text;
using System.Text.Json;
using GameEventScript.Api;
using GameEventScript.Runtime.Values;

namespace GameEventScript.Tool;

internal sealed class RunObserver(bool verbose, bool color, bool interactive) : IGameEventScriptRuntimeObserver
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
    public void MessageEmitted(GameEventScriptMessage message, bool accepted)
    {
        if (verbose) WriteTrace($"emit {Format(message)}{(accepted ? string.Empty : " [not queued]")}");
    }

    /// <inheritdoc />
    public void MessagePublished(GameEventScriptMessage message, GameEventScriptPublishResult result)
    {
        if (verbose) WriteTrace($"publish {Format(message)}{(result.LocalAccepted ? string.Empty : " [not queued]")}");
    }

    /// <inheritdoc />
    public void DispatchStarted(GameEventScriptMessage message, string dispatchSignatureId)
    {
        if (verbose) WriteTrace($"dispatch {dispatchSignatureId} <- {Format(message)}");
    }

    /// <inheritdoc />
    public void DispatchCompleted(GameEventScriptMessage message, string dispatchSignatureId) { }

    /// <inheritdoc />
    public void RuntimeLimitReached(string limitName, string detail, int limit)
    {
        Failed = true;
        Write(Console.Error, $"error cli.runtimeLimit: {limitName}={limit}: {detail}");
    }

    /// <inheritdoc />
    public void RuntimeError(GameEventScriptDiagnostic diagnostic)
    {
        Failed = true;
        try { WriteDiagnostic(diagnostic, "runtime"); }
        catch (IOException exception) { OutputError ??= exception; }
    }

    internal static void WriteDiagnostic(GameEventScriptDiagnostic diagnostic, string fallback)
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
                text.Append(message.Arguments.KindAt(index) == GameEventScriptBytecodeTypeKind.Text ? JsonSerializer.Serialize(value.AsText()) : value.ToString());
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
        GameEventScriptBytecodeTypeKind.Text => value.ToString(),
        GameEventScriptBytecodeTypeKind.Integer or GameEventScriptBytecodeTypeKind.Float or GameEventScriptBytecodeTypeKind.Percentage => RunHighlighting.Paint(value.ToString(), 34),
        _ => RunHighlighting.Render(value.ToString())
    };

    private sealed class ConsoleHandler(RunObserver observer, bool error) : IGameEventScriptNativeMessageHandler
    {
        /// <inheritdoc />
        public void Handle(GameEventScriptMessage message, GameEventScriptContext context) => observer.WriteConsole(message, error);
    }

    private sealed class ErrorCodeHandler(RunObserver observer) : IGameEventScriptNativeMessageHandler
    {
        /// <inheritdoc />
        public void Handle(GameEventScriptMessage message, GameEventScriptContext context)
        {
            if (message.Arguments.Count != 1 || message.Arguments.NameAt(0) is not ("code" or "_")) throw InvalidCode();
            var value = message.Arguments[0];
            if (value.ValueKind == GameEventScriptBytecodeTypeKind.Nothing)
            {
                observer.ScriptExitCode = 0;
                return;
            }
            if (value.ValueKind is not (GameEventScriptBytecodeTypeKind.Integer or GameEventScriptBytecodeTypeKind.Float)
                || value.ValueUnit != GameEventScriptBytecodeInstructionUnit.UnitNone) throw InvalidCode();
            var number = value.AsNumber();
            if (!double.IsFinite(number) || number < 0 || number > 255 || number != Math.Truncate(number)) throw InvalidCode();
            observer.ScriptExitCode = (int)number;
        }

        private static GameEventScriptExtensionFaultException InvalidCode() => new("cli.errorCodeArgument", "ErrorCode requires one code argument: a unitless integer from 0 to 255, or nothing.");
    }
}
