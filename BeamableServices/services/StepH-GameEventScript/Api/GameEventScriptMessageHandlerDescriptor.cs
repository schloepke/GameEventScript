#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections.Generic;

namespace StepH.GameEventScript.Api;

/// <summary>
/// Describes a message handler exported by a GameEventScript module.
/// </summary>
public sealed class GameEventScriptMessageHandlerDescriptor
{
    private readonly Func<GameEventScriptMessage, GameEventScriptSession, IGameEventScriptMessageInvocation> _invoke;

    public GameEventScriptMessageHandlerDescriptor(
        GameEventScriptMessageSignature signature,
        Action<GameEventScriptMessage, GameEventScriptSession> handler,
        IReadOnlyCollection<string>? requiredTags = null,
        IReadOnlyCollection<string>? excludedTags = null,
        bool matchArguments = true)
        : this(
            signature,
            (message, session) => new ActionMessageInvocation(handler, message, session),
            requiredTags,
            excludedTags,
            matchArguments)
    {
    }

    public GameEventScriptMessageHandlerDescriptor(
        GameEventScriptMessageSignature signature,
        Func<GameEventScriptMessage, GameEventScriptSession, IGameEventScriptMessageInvocation> invoke,
        IReadOnlyCollection<string>? requiredTags = null,
        IReadOnlyCollection<string>? excludedTags = null,
        bool matchArguments = true)
    {
        Signature = signature ?? throw new ArgumentNullException(nameof(signature));
        _invoke = invoke ?? throw new ArgumentNullException(nameof(invoke));
        RequiredTags = GameEventScriptMessage.NormalizeTags(requiredTags);
        ExcludedTags = GameEventScriptMessage.NormalizeTags(excludedTags);
        MatchArguments = matchArguments;
    }

    public GameEventScriptMessageSignature Signature { get; }

    public bool MatchArguments { get; }

    public string DispatchSignatureId => MatchArguments ? Signature.SignatureId : $"{Signature.Name}(*)";

    public IReadOnlyList<string> RequiredTags { get; }

    public IReadOnlyList<string> ExcludedTags { get; }

    public IGameEventScriptMessageInvocation Invoke(GameEventScriptMessage message, GameEventScriptSession session)
        => _invoke(message, session);

    private sealed class ActionMessageInvocation(
        Action<GameEventScriptMessage, GameEventScriptSession> handler,
        GameEventScriptMessage message,
        GameEventScriptSession session) : IGameEventScriptMessageInvocation
    {
        private bool _completed;

        public bool IsCompleted => _completed;

        public int RunSlice(int maxSteps)
        {
            if (_completed)
            {
                return 0;
            }

            handler(message, session);
            _completed = true;
            return 0;
        }
    }
}
