// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using GameEventScript.Runtime;
using GameEventScript.Runtime.Vm;
using static GameEventScript.Runtime.Vm.GesVirtualMachine.StateValue;

namespace GameEventScript.Api;

/// <summary>One captured script or native subscription; dispatch order is descending priority, then registration order.</summary>
internal sealed class GesSubscriptionEntry
{
    private GesSubscriptionEntry(
        GesMessageSignature signature,
        string[] requiredTags,
        string[] excludedTags,
        bool matchArguments,
        int priority,
        long registrationOrder,
        long registrationId,
        IGesNativeMessageHandler? nativeHandler,
        GesProgramInstance? instance,
        ushort entryAddress)
    {
        Signature = signature;
        RequiredTags = requiredTags;
        ExcludedTags = excludedTags;
        MatchArguments = matchArguments;
        DispatchSignatureId = matchArguments ? signature.SignatureId : $"{signature.Name}(*)";
        Priority = priority;
        RegistrationOrder = registrationOrder;
        RegistrationId = registrationId;
        NativeHandler = nativeHandler;
        Instance = instance;
        EntryAddress = entryAddress;
    }

    internal GesMessageSignature Signature { get; }
    internal string[] RequiredTags { get; }
    internal string[] ExcludedTags { get; }
    internal bool MatchArguments { get; }
    internal int Priority { get; }
    internal long RegistrationOrder { get; }
    internal long RegistrationId { get; }
    internal IGesNativeMessageHandler? NativeHandler { get; }
    internal GesProgramInstance? Instance { get; }
    internal ushort EntryAddress { get; }
    internal GesSubscriptionEntry? NextNativeRegistration { get; set; }
    internal string DispatchSignatureId { get; }

    internal static GesSubscriptionEntry ForNative(
        long registrationId,
        GesMessageSignature signature,
        IGesNativeMessageHandler handler,
        IReadOnlyList<string> requiredTags,
        IReadOnlyList<string> excludedTags,
        bool matchArguments,
        int priority,
        long order)
        => new(signature, Copy(requiredTags), Copy(excludedTags), matchArguments, priority, order, registrationId, handler, null, 0);

    internal static GesSubscriptionEntry ForScript(GesProgramInstance instance, GesLinkedProgram.Handler handler, int priority, long order)
        => new(handler.Signature, handler.RequiredTags, handler.ExcludedTags, handler.MatchArguments, priority, order, 0, null, instance, handler.EntryAddress);

    internal bool Matches(GameEventScriptMessage message)
    {
        for (var index = 0; index < RequiredTags.Length; index++) if (!message.HasTag(RequiredTags[index])) return false;
        for (var index = 0; index < ExcludedTags.Length; index++) if (message.HasTag(ExcludedTags[index])) return false;
        return true;
    }

    private static string[] Copy(IReadOnlyList<string> source)
    {
        if (source.Count == 0) return [];
        var result = new string[source.Count];
        for (var index = 0; index < result.Length; index++) result[index] = source[index];
        return result;
    }

    internal static int Compare(GesSubscriptionEntry left, GesSubscriptionEntry right)
    {
        var priority = right.Priority.CompareTo(left.Priority);
        return priority != 0 ? priority : left.RegistrationOrder.CompareTo(right.RegistrationOrder);
    }
}
