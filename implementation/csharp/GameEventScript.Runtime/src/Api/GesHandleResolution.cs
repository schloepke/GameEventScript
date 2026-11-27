// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using GameEventScript.Runtime.Values;

namespace GameEventScript.Api;

/// <summary>
/// Reads the state of the objects of one handle type for a host (specs/HostRuntime.md#handles). The host calls it
/// synchronously from script execution on its serial path, at most once per object and handler invocation.
/// </summary>
public interface IGesHandleResolver
{
    /// <summary>
    /// Answers one state read with exactly one of <see cref="GesHandleResolveCall.SetState(GesValue)"/>,
    /// <see cref="GesHandleResolveCall.SetDocument(string)"/>, <see cref="GesHandleResolveCall.SetMissing"/> and
    /// <see cref="GesHandleResolveCall.SetNotVisible"/>. The call must not be retained after returning. Throwing ends the
    /// read with <c>runtime.handleResolveFailed</c>.
    /// </summary>
    /// <param name="call">The reusable call holding the handle and receiving the answer.</param>
    void Resolve(GesHandleResolveCall call);

    /// <summary>
    /// Turns the ref a handle read from a document carries into the version it names. The default implementation is
    /// for types without refs: it throws, which <see cref="GameEventScriptHost.ResolveRef(GesHandle)"/> reports as
    /// <c>runtime.handleResolveFailed</c>.
    /// </summary>
    /// <param name="handle">A handle of this resolver's type that carries a ref.</param>
    /// <returns>The version: an integer Number, or Bytes of 20 or 32 bytes for a hash; Nothing when no such ref exists.</returns>
    /// <exception cref="InvalidOperationException">The default implementation: the type has no refs.</exception>
    GesValue ResolveRef(GesHandle handle) => throw new InvalidOperationException($"@{handle.TypeName} has no refs.");

    /// <summary>
    /// Answers the state reads of several handles of one type and projection from one List of the current message, the
    /// read handle first and the others in List order. Each call follows the rules of
    /// <see cref="Resolve(GesHandleResolveCall)"/>. The default implementation calls <see cref="Resolve"/> for each call
    /// in order. Throwing ends the read of the first handle with <c>runtime.handleResolveFailed</c>; the others stay
    /// unresolved.
    /// </summary>
    /// <param name="batch">The reusable batch of calls; valid only during this invocation.</param>
    void ResolveMany(GesHandleResolveBatch batch)
    {
        if (batch is null) throw new ArgumentNullException(nameof(batch));
        for (var index = 0; index < batch.Count; index++) Resolve(batch[index]);
    }
}

/// <summary>
/// The state reads a host asks a <see cref="IGesHandleResolver"/> to answer together
/// (<see cref="IGesHandleResolver.ResolveMany"/>). The host reuses the instance and its calls; they are valid only during
/// the synchronous invocation.
/// </summary>
public sealed class GesHandleResolveBatch
{
    private readonly List<GesHandleResolveCall> _calls = new();
    private int _count;

    internal GesHandleResolveBatch()
    {
    }

    /// <summary>Gets the number of calls, at least one during an invocation.</summary>
    public int Count => _count;

    /// <summary>Gets a call; the first is the read that started the batch.</summary>
    /// <param name="index">The zero-based index, less than <see cref="Count"/>.</param>
    /// <returns>The call.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is outside the batch.</exception>
    public GesHandleResolveCall this[int index] => (uint)index < (uint)_count ? _calls[index] : throw new ArgumentOutOfRangeException(nameof(index));

    internal GesHandleResolveCall Add(GesHandle handle, GesHandleType handleType, GesSchema? projection, int party)
    {
        if (_count == _calls.Count) _calls.Add(new GesHandleResolveCall());
        var call = _calls[_count++];
        call.Begin(handle, handleType, projection, party);
        return call;
    }

    internal void End()
    {
        for (var index = 0; index < _count; index++) _calls[index].End();
        _count = 0;
    }
}

/// <summary>
/// One state read a host asks a <see cref="IGesHandleResolver"/> to answer. The host reuses the instance; it is valid
/// only during the synchronous <see cref="IGesHandleResolver.Resolve"/> call.
/// </summary>
public sealed class GesHandleResolveCall
{
    internal enum AnswerKind : byte
    {
        None,
        State,
        Document,
        Missing,
        NotVisible,
        Several,
    }

    private GesHandle? _handle;
    private GesHandleType? _handleType;
    private GesSchema? _projection;
    private int _party;

    internal GesHandleResolveCall()
    {
    }

    /// <summary>Gets the handle whose state is read, with the version it carries, if any.</summary>
    /// <exception cref="InvalidOperationException">The call is used outside its resolver invocation.</exception>
    public GesHandle Handle => _handle ?? throw new InvalidOperationException("The resolve call is only valid during its resolver invocation.");

    /// <summary>Gets the handle type registered with the resolver, whose state schema the answer must fit.</summary>
    /// <exception cref="InvalidOperationException">The call is used outside its resolver invocation.</exception>
    public GesHandleType HandleType => _handleType ?? throw new InvalidOperationException("The resolve call is only valid during its resolver invocation.");

    /// <summary>
    /// Gets the projection the script reads through, or <see langword="null"/> for the full view. The answer may be the
    /// full state or the state of this projection or of a projection between them; the host narrows it.
    /// </summary>
    /// <exception cref="InvalidOperationException">The call is used outside its resolver invocation.</exception>
    public GesSchema? Projection => _handle is null ? throw new InvalidOperationException("The resolve call is only valid during its resolver invocation.") : _projection;

    /// <summary>Gets the party the read is for: 0 for the host itself, otherwise the host-local number of a network party.</summary>
    /// <exception cref="InvalidOperationException">The call is used outside its resolver invocation.</exception>
    public int Party => _handle is null ? throw new InvalidOperationException("The resolve call is only valid during its resolver invocation.") : _party;

    internal AnswerKind Answer { get; private set; }
    internal GesValue Record { get; private set; }
    internal string? Text { get; private set; }
    internal GesValue Version { get; private set; }

    /// <summary>Answers with the state as a Record of the state schema.</summary>
    /// <param name="record">The state Record.</param>
    public void SetState(GesValue record) => Set(AnswerKind.State, record, null, default);

    /// <summary>Answers with the state as a Record of the state schema and the version of the object it describes.</summary>
    /// <param name="record">The state Record.</param>
    /// <param name="version">The version: an integer Number for a revision, Bytes for a hash.</param>
    public void SetState(GesValue record, GesValue version) => Set(AnswerKind.State, record, null, version);

    /// <summary>Answers with the state as <c>.gesd</c> document text whose schema header names the state schema.</summary>
    /// <param name="text">The document text.</param>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is <see langword="null"/>.</exception>
    public void SetDocument(string text) => Set(AnswerKind.Document, default, text ?? throw new ArgumentNullException(nameof(text)), default);

    /// <summary>Answers with the state as <c>.gesd</c> document text and the version of the object it describes.</summary>
    /// <param name="text">The document text.</param>
    /// <param name="version">The version: an integer Number for a revision, Bytes for a hash.</param>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is <see langword="null"/>.</exception>
    public void SetDocument(string text, GesValue version) => Set(AnswerKind.Document, default, text ?? throw new ArgumentNullException(nameof(text)), version);

    /// <summary>Answers that the object does not exist: its state reads give <c>nothing</c>.</summary>
    public void SetMissing() => Set(AnswerKind.Missing, default, null, default);

    /// <summary>
    /// Answers that the asking party may not see the object: its state reads give <c>nothing</c>, as for a missing
    /// object, and the host reports <c>runtime.handleNotVisible</c> to its observer.
    /// </summary>
    public void SetNotVisible() => Set(AnswerKind.NotVisible, default, null, default);

    internal void Begin(GesHandle handle, GesHandleType handleType, GesSchema? projection, int party)
    {
        _handle = handle;
        _handleType = handleType;
        _projection = projection;
        _party = party;
        Answer = AnswerKind.None;
        Record = default;
        Text = null;
        Version = default;
    }

    internal void End()
    {
        _handle = null;
        _handleType = null;
        _projection = null;
        _party = 0;
        Record = default;
        Text = null;
        Version = default;
    }

    private void Set(AnswerKind answer, GesValue record, string? text, GesValue version)
    {
        if (_handle is null) throw new InvalidOperationException("The resolve call is only valid during its resolver invocation.");
        Answer = Answer == AnswerKind.None ? answer : AnswerKind.Several;
        Record = record;
        Text = text;
        Version = version;
    }
}

/// <summary>
/// Thrown by <see cref="GameEventScriptHost.ResolveRef"/> when a ref cannot be turned into a version.
/// </summary>
public sealed class GesHandleException : Exception
{
    /// <summary>Creates the exception.</summary>
    /// <param name="code">The stable code, such as <c>runtime.handleResolveFailed</c>.</param>
    /// <param name="symbol">The handle's text form.</param>
    /// <param name="message">An explanation for people; informative text.</param>
    /// <param name="innerException">The resolver's exception, if any.</param>
    public GesHandleException(string code, string symbol, string message, Exception? innerException = null) : base(message, innerException)
    {
        Code = code ?? throw new ArgumentNullException(nameof(code));
        Symbol = symbol ?? throw new ArgumentNullException(nameof(symbol));
    }

    /// <summary>Gets the stable code, <c>runtime.handleResolveFailed</c>.</summary>
    public string Code { get; }

    /// <summary>Gets the handle's text form.</summary>
    public string Symbol { get; }
}
