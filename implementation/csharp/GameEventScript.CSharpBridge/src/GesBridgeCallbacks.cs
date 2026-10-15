// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Threading;
using GameEventScript.Api;
using GameEventScript.Runtime.Values;

namespace GameEventScript.CSharpBridge;

/// <summary>
/// A delegate retained once at subscription time; dispatch invokes it synchronously.
/// </summary>
/// <param name="handler">The synchronous handler, invoked with the message and the owning host context.</param>
public sealed class GesBridgeMessageHandler(Action<GameEventScriptMessage, GesContext> handler) : IGesNativeMessageHandler
{
    private readonly Action<GameEventScriptMessage, GesContext> _handler =
        handler ?? throw new ArgumentNullException(nameof(handler));

    /// <summary>
    /// Invokes the stored delegate synchronously, propagating its exception.
    /// </summary>
    /// <param name="message">The dispatched message.</param>
    /// <param name="context">The owning host context.</param>
    public void Handle(GameEventScriptMessage message, GesContext context)
        => _handler(message, context);
}

/// <summary>
/// A synchronous outbound publish delegate; exceptions retain the Runtime's classification.
/// </summary>
/// <param name="publish">Returns whether the sink accepted responsibility for the message.</param>
public sealed class GesBridgePublishSink(Func<GameEventScriptMessage, bool> publish) : IGesPublishSink
{
    private readonly Func<GameEventScriptMessage, bool> _publish =
        publish ?? throw new ArgumentNullException(nameof(publish));

    /// <summary>
    /// Invokes the stored delegate and returns its acceptance flag, propagating its exception.
    /// </summary>
    /// <param name="message">The outbound message.</param>
    /// <returns><see langword="true"/> when the delegate accepted the message.</returns>
    public bool Publish(GameEventScriptMessage message) => _publish(message);
}

/// <summary>
/// Provides delegate-based and automatic-runner conveniences that intentionally remain outside the portable Core.
/// </summary>
public static class GesBridgeHostExtensions
{
    /// <summary>
    /// Performs the subscribe operation.
    /// </summary>
    /// <param name="host">The host value.</param>
    /// <param name="message">The message value.</param>
    /// <param name="parameterNames">The parameter names value.</param>
    /// <param name="handler">The handler value.</param>
    /// <param name="priority">The priority value.</param>
    /// <returns>The result of the operation.</returns>
    public static GesSubscription Subscribe(this GameEventScriptHost host, string message, IReadOnlyCollection<string> parameterNames, Action<GameEventScriptMessage, GesContext> handler, int priority = 0)
        => RequireHost(host).Subscribe(
            message,
            parameterNames,
            new GesBridgeMessageHandler(handler),
            priority);

    /// <summary>
    /// Performs the subscribe operation.
    /// </summary>
    /// <param name="host">The host value.</param>
    /// <param name="signature">The signature value.</param>
    /// <param name="handler">The handler value.</param>
    /// <param name="priority">The priority value.</param>
    /// <returns>The result of the operation.</returns>
    public static GesSubscription Subscribe(this GameEventScriptHost host, GesMessageSignature signature, Action<GameEventScriptMessage, GesContext> handler, int priority = 0)
        => RequireHost(host).Subscribe(
            signature,
            new GesBridgeMessageHandler(handler),
            priority);

    /// <summary>
    /// Performs the subscribe operation.
    /// </summary>
    /// <param name="host">The host value.</param>
    /// <param name="signature">The signature value.</param>
    /// <param name="handler">The handler value.</param>
    /// <param name="matchingTags">The matching tags value.</param>
    /// <param name="withoutTags">The without tags value.</param>
    /// <param name="priority">The priority value.</param>
    /// <returns>The result of the operation.</returns>
    public static GesSubscription Subscribe(
        this GameEventScriptHost host,
        GesMessageSignature signature,
        Action<GameEventScriptMessage, GesContext> handler,
        IReadOnlyCollection<string>? matchingTags,
        IReadOnlyCollection<string>? withoutTags = null,
        int priority = 0)
        => RequireHost(host).Subscribe(
            signature,
            new GesBridgeMessageHandler(handler),
            matchingTags,
            withoutTags,
            priority);

    /// <summary>
    /// Performs the subscribe message name operation.
    /// </summary>
    /// <param name="host">The host value.</param>
    /// <param name="messageName">The message name value.</param>
    /// <param name="handler">The handler value.</param>
    /// <param name="matchingTags">The matching tags value.</param>
    /// <param name="withoutTags">The without tags value.</param>
    /// <param name="priority">The priority value.</param>
    /// <returns>The result of the operation.</returns>
    public static GesSubscription SubscribeMessageName(
        this GameEventScriptHost host,
        string messageName,
        Action<GameEventScriptMessage, GesContext> handler,
        IReadOnlyCollection<string>? matchingTags = null,
        IReadOnlyCollection<string>? withoutTags = null,
        int priority = 0)
        => RequireHost(host).SubscribeMessageName(
            messageName,
            new GesBridgeMessageHandler(handler),
            matchingTags,
            withoutTags,
            priority);

    /// <summary>
    /// Creates a thread-safe C# runner that automatically pumps existing work and later accepted messages on the shared dispatcher.
    /// </summary>
    /// <param name="host">The host whose access becomes owned by the runner.</param>
    /// <returns>The automatic host runner.</returns>
    /// <remarks>Direct host access must stop when ownership is transferred. Pending callbacks may start before this method returns.</remarks>
    public static GesBridgeHostRunner RunAutomatically(this GameEventScriptHost host)
        => GesBridgeHostRunner.Create(RequireHost(host));

    private static GameEventScriptHost RequireHost(GameEventScriptHost? host)
        => host ?? throw new ArgumentNullException(nameof(host));
}

/// <summary>
/// C#-specific message construction conveniences. Portable code uses ordered
/// <see cref="GesMessageArgument"/> values directly.
/// </summary>
public static class GesBridgeMessage
{
    /// <summary>
    /// Creates a message from C# tuple arguments while preserving tuple order.
    /// </summary>
    /// <param name="name">The message name.</param>
    /// <param name="arguments">The ordered name/value pairs.</param>
    /// <returns>An immutable portable message.</returns>
    public static GameEventScriptMessage Create(string name, params (string name, GesValue value)[] arguments)
        => GameEventScriptMessage.Create(name, ToOrdered(arguments));

    /// <summary>
    /// Creates a tagged message from C# tuple arguments while preserving tuple order.
    /// </summary>
    /// <param name="name">The message name.</param>
    /// <param name="tags">Optional message tags.</param>
    /// <param name="arguments">The ordered name/value pairs.</param>
    /// <returns>An immutable portable message.</returns>
    public static GameEventScriptMessage Create(string name, IEnumerable<string>? tags, params (string name, GesValue value)[] arguments)
        => GameEventScriptMessage.Create(name, ToOrdered(arguments), tags);

    /// <summary>
    /// Creates a message by arranging dictionary values according to a known ordered signature.
    /// </summary>
    /// <param name="signature">The signature that supplies deterministic argument order.</param>
    /// <param name="arguments">Values keyed by each named signature label.</param>
    /// <param name="tags">Optional message tags.</param>
    /// <returns>An immutable portable message.</returns>
    /// <exception cref="ArgumentException">Thrown for missing, extra, or unlabeled signature arguments.</exception>
    public static GameEventScriptMessage Create(GesMessageSignature signature, IReadOnlyDictionary<string, GesValue> arguments, IEnumerable<string>? tags = null)
    {
        _ = signature ?? throw new ArgumentNullException(nameof(signature));
        _ = arguments ?? throw new ArgumentNullException(nameof(arguments));
        if (arguments.Count != signature.Parameters.Count)
        {
            throw new ArgumentException($"Message signature '{signature.SignatureId}' expects {signature.Parameters.Count} argument(s) but received {arguments.Count}.", nameof(arguments));
        }

        var ordered = new GesMessageArgument[signature.Parameters.Count];
        for (var index = 0; index < signature.Parameters.Count; index++)
        {
            var parameter = signature.Parameters[index];
            if (string.Equals(parameter, GesMessageSignature.UnlabeledParameterName, StringComparison.Ordinal))
            {
                throw new ArgumentException("Dictionary binding cannot represent unlabeled message parameters.", nameof(signature));
            }

            if (!arguments.TryGetValue(parameter, out var value))
            {
                throw new ArgumentException($"Message argument '{parameter}' required by signature '{signature.SignatureId}' is missing.", nameof(arguments));
            }

            ordered[index] = new GesMessageArgument(parameter, value);
        }

        return GameEventScriptMessage.Create(signature.Name, ordered, tags);
    }

    internal static GesMessageArgument[] ToOrdered((string name, GesValue value)[] arguments)
    {
        _ = arguments ?? throw new ArgumentNullException(nameof(arguments));
        var ordered = new GesMessageArgument[arguments.Length];
        for (var index = 0; index < arguments.Length; index++)
        {
            ordered[index] = new GesMessageArgument(arguments[index].name, arguments[index].value);
        }

        return ordered;
    }
}

/// <summary>
/// Provides C# tuple and dictionary conveniences for context messaging.
/// </summary>
public static class GesBridgeContextExtensions
{
    /// <summary>
    /// Performs the emit operation.
    /// </summary>
    /// <param name="context">The context value.</param>
    /// <param name="message">The message value.</param>
    /// <param name="arguments">The arguments value.</param>
    /// <returns>The result of the operation.</returns>
    public static bool Emit(this GesContext context, string message, params (string name, GesValue value)[] arguments)
        => RequireContext(context).Emit(GesBridgeMessage.Create(message, arguments));

    /// <summary>
    /// Performs the emit operation.
    /// </summary>
    /// <param name="context">The context value.</param>
    /// <param name="signature">The signature value.</param>
    /// <param name="arguments">The arguments value.</param>
    /// <returns>The result of the operation.</returns>
    public static bool Emit(this GesContext context, GesMessageSignature signature, IReadOnlyDictionary<string, GesValue> arguments)
        => RequireContext(context).Emit(GesBridgeMessage.Create(signature, arguments));

    /// <summary>
    /// Performs the publish operation.
    /// </summary>
    /// <param name="context">The context value.</param>
    /// <param name="message">The message value.</param>
    /// <param name="arguments">The arguments value.</param>
    /// <returns>The result of the operation.</returns>
    public static GesPublishResult Publish(this GesContext context, string message, params (string name, GesValue value)[] arguments)
        => RequireContext(context).Publish(GesBridgeMessage.Create(message, arguments));

    /// <summary>
    /// Performs the publish operation.
    /// </summary>
    /// <param name="context">The context value.</param>
    /// <param name="signature">The signature value.</param>
    /// <param name="arguments">The arguments value.</param>
    /// <returns>The result of the operation.</returns>
    public static GesPublishResult Publish(this GesContext context, GesMessageSignature signature, IReadOnlyDictionary<string, GesValue> arguments)
        => RequireContext(context).Publish(GesBridgeMessage.Create(signature, arguments));

    private static GesContext RequireContext(GesContext? context)
        => context ?? throw new ArgumentNullException(nameof(context));
}
