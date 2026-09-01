#pragma warning disable CS1591 // Public architecture is documented in HostArchitecture.md.

using System;
using System.Collections.Generic;
using StepH.GameEventScript.Runtime;
using StepH.GameEventScript.Runtime.VM;
using static StepH.GameEventScript.Runtime.VM.GesVmState.StateValue;

namespace StepH.GameEventScript.Api;

/// <summary>
/// Serial message host for native handlers and optional GameEventScript programs.
/// The portable core performs no synchronization and creates no worker threads.
/// </summary>
public sealed class GameEventScriptHost
{
    private const int NormalPriority = 0;
    private const int DefaultRegisterLimit = 512;
    private readonly GameEventScriptRandomGenerator _random;
    private readonly IGameEventScriptRuntimeObserver? _observer;
    private readonly IGameEventScriptExtensionRegistry _extensionRegistry;
    private readonly IGameEventScriptExternalTypeRegistry _externalTypeRegistry;
    private readonly GameEventScriptRuntimeLimits _limits;
    private readonly IGameEventScriptPublishSink? _publishSink;
    private readonly Dictionary<string, SubscriptionEntry[]> _exact = new(StringComparer.Ordinal);
    private readonly Dictionary<string, SubscriptionEntry[]> _byName = new(StringComparer.Ordinal);
    private readonly MessageRingQueue _queue = new(16);
    private readonly GameEventScriptContext _context;
    private GesVmState? _vmState;
    private PendingMessage _activeMessage;
    private SubscriptionEntry? _activeHandler;
    private bool _hasActiveMessage;
    private bool _scriptHandlerActive;
    private GameEventScriptInstance? _instances;
    private SubscriptionEntry? _nativeSubscriptions;
    private long _nextRegistrationId;
    private long _nextRegistrationOrder;
    private int _stepExecutedOpcodes;
    private int _stepProcessedMessages;
    private int _stepEmittedMessages;
    private int _stepPublishedMessages;
    private GameEventScriptDiagnostic? _stepRuntimeDiagnostic;
    private int _pendingVmWarmupCapacity;

    internal GameEventScriptHost(
        GameEventScriptRandomGenerator random,
        IGameEventScriptRuntimeObserver? observer,
        IGameEventScriptExtensionRegistry? extensionRegistry,
        IGameEventScriptExternalTypeRegistry? externalTypeRegistry,
        GameEventScriptRuntimeLimits? limits,
        IGameEventScriptPublishSink? publishSink)
    {
        _random = random ?? throw new ArgumentNullException(nameof(random));
        _observer = observer;
        _extensionRegistry = extensionRegistry ?? GameEventScriptEmptyExtensionRegistry.Instance;
        _externalTypeRegistry = externalTypeRegistry ?? GameEventScriptEmptyExternalTypeRegistry.Instance;
        _limits = limits ?? GameEventScriptRuntimeLimits.Default;
        _publishSink = publishSink;
        _context = new GameEventScriptContext(this, _random, _limits, _extensionRegistry, _observer);
    }

    public static GameEventScriptHostBuilder CreateBuilder() => new();
    public int PendingMessageCount => _queue.Count;
    public bool IsIdle => !_hasActiveMessage && _queue.Count == 0;
    internal GesVmState? VmState => _vmState;

    public GameEventScriptInstance Load(GameEventScriptProgram program, int priority = NormalPriority)
    {
        _ = program ?? throw new ArgumentNullException(nameof(program));
        GameEventScriptProgramValidator.Validate(program);
        var maxRegisterCount = Math.Min(
            ushort.MaxValue,
            _limits.MaxRegisterValues > 0 ? _limits.MaxRegisterValues : DefaultRegisterLimit);
        var maxCallStackDepth = Math.Min(ushort.MaxValue, Math.Max(0, _limits.MaxCallDepth));
        if (program.RequiredRegisterCount > maxRegisterCount)
        {
            throw LinkError(
                GameEventScriptDiagnosticCodes.LinkRequiredRegisterCountExceeded,
                $"Program requires {program.RequiredRegisterCount} registers but the host limit is {maxRegisterCount}.",
                program);
        }

        if (program.RequiredCallStackDepth > maxCallStackDepth)
        {
            throw LinkError(
                GameEventScriptDiagnosticCodes.LinkRequiredCallStackDepthExceeded,
                $"Program requires call-stack depth {program.RequiredCallStackDepth} but the host limit is {maxCallStackDepth}.",
                program);
        }

        var linked = new GesLinkedProgram(program, _extensionRegistry, _externalTypeRegistry);
        _vmState ??= new GesVmState((ushort)maxRegisterCount, (ushort)maxCallStackDepth);
        if (!_vmState.PrepareCapacity(linked))
            _pendingVmWarmupCapacity = Math.Max(_pendingVmWarmupCapacity, linked.RequiredRegisterCapacity);

        var registrationId = NextRegistrationId();
        var instance = new GameEventScriptInstance(this, registrationId, program, linked);
        var initialization = new List<SubscriptionEntry>();
        for (var index = 0; index < linked.Handlers.Length; index++)
        {
            var handler = linked.Handlers[index];
            var entry = SubscriptionEntry.ForScript(instance, handler, priority, _nextRegistrationOrder++);
            if (GameEventScriptSystemEndpoints.IsInitializationName(handler.Signature.Name))
                initialization.Add(entry);
            else
                Register(entry);
        }

        instance.NextRegistration = _instances;
        _instances = instance;
        if (initialization.Count > 0)
        {
            var message = GameEventScriptSystemEndpoints.CreateInitializationMessage();
            EnqueuePlan(new PendingMessage(message, Sort(initialization.ToArray()), []));
        }

        return instance;
    }

    private static GameEventScriptDynamicLinkException LinkError(string code, string message, GameEventScriptProgram program)
        => new(new GameEventScriptDiagnostic(
            GameEventScriptDiagnosticPhase.Link,
            code,
            message,
            ProgramName: program.ModuleName));

    public GameEventScriptSubscription Subscribe(
        string message,
        IReadOnlyCollection<string> parameterNames,
        IGameEventScriptNativeMessageHandler handler,
        int priority = NormalPriority)
        => Subscribe(GameEventScriptMessageSignature.Create(message, parameterNames), handler, null, null, priority);

    public GameEventScriptSubscription Subscribe(
        GameEventScriptMessageSignature signature,
        IGameEventScriptNativeMessageHandler handler,
        int priority = NormalPriority)
        => Subscribe(signature, handler, null, null, priority);

    public GameEventScriptSubscription Subscribe(
        GameEventScriptMessageSignature signature,
        IGameEventScriptNativeMessageHandler handler,
        IReadOnlyCollection<string>? matchingTags,
        IReadOnlyCollection<string>? withoutTags = null,
        int priority = NormalPriority)
    {
        _ = signature ?? throw new ArgumentNullException(nameof(signature));
        _ = handler ?? throw new ArgumentNullException(nameof(handler));
        var registrationId = NextRegistrationId();
        var entry = SubscriptionEntry.ForNative(registrationId, signature, handler,
            GameEventScriptMessage.NormalizeTags(matchingTags),
            GameEventScriptMessage.NormalizeTags(withoutTags),
            matchArguments: true, priority, _nextRegistrationOrder++);
        Register(entry);
        entry.NextNativeRegistration = _nativeSubscriptions;
        _nativeSubscriptions = entry;
        return new GameEventScriptSubscription(this, registrationId);
    }

    public GameEventScriptSubscription SubscribeMessageName(
        string messageName,
        IGameEventScriptNativeMessageHandler handler,
        IReadOnlyCollection<string>? matchingTags = null,
        IReadOnlyCollection<string>? withoutTags = null,
        int priority = NormalPriority)
    {
        if (string.IsNullOrWhiteSpace(messageName)) throw new ArgumentException("Message name must not be empty.", nameof(messageName));
        _ = handler ?? throw new ArgumentNullException(nameof(handler));
        var registrationId = NextRegistrationId();
        var entry = SubscriptionEntry.ForNative(registrationId, GameEventScriptMessageSignature.Create(messageName, []), handler,
            GameEventScriptMessage.NormalizeTags(matchingTags),
            GameEventScriptMessage.NormalizeTags(withoutTags),
            matchArguments: false, priority, _nextRegistrationOrder++);
        Register(entry);
        entry.NextNativeRegistration = _nativeSubscriptions;
        _nativeSubscriptions = entry;
        return new GameEventScriptSubscription(this, registrationId);
    }

    public bool Receive(GameEventScriptMessage message)
    {
        if (message.Name.Length == 0 || GameEventScriptSystemEndpoints.IsInitializationName(message.Name)) return false;
        return EnqueueMessage(message);
    }

    public GameEventScriptExecutionResult ExecuteFrame(int opcodeBudget)
    {
        if (opcodeBudget <= 0) throw new ArgumentOutOfRangeException(nameof(opcodeBudget), "Opcode budget must be greater than zero.");
        BeginStep();
        var remaining = opcodeBudget;
        var runtimeLimitReached = false;

        while (remaining > 0)
        {
            if (!_hasActiveMessage && !StartNextMessage()) break;

            if (_scriptHandlerActive)
            {
                var executed = GameEventScriptVirtualMachine.RunSlice(_vmState!, _context, remaining);
                _stepExecutedOpcodes += executed;
                remaining -= executed;
                if (_context.RuntimeBudget.IsExhausted)
                {
                    _vmState!.Reset();
                    CompleteActiveHandler();
                    runtimeLimitReached = true;
                    break;
                }

                if (_vmState!.State == Processing) break;
                if (_vmState.State == Error)
                {
                    RecordRuntimeError(_vmState.ErrorDiagnostic ?? CreateRuntimeDiagnostic(
                        GameEventScriptDiagnosticCodes.RuntimeUnhandledFailure,
                        "The VM entered an error state without a diagnostic."));
                    _vmState.Reset();
                    CompleteActiveHandler();
                    continue;
                }

                _vmState.Reset();
                CompleteActiveHandler();
                continue;
            }

            var next = _activeMessage.NextMatching();
            if (next is null)
            {
                _hasActiveMessage = false;
                _stepProcessedMessages++;
                if (_limits.MaxProcessedEventsPerRun > 0 &&
                    _stepProcessedMessages >= _limits.MaxProcessedEventsPerRun)
                {
                    _context.RecordRuntimeLimitReached(
                        nameof(GameEventScriptRuntimeLimits.MaxProcessedEventsPerRun),
                        "Message processing limit reached before the host became idle.",
                        _limits.MaxProcessedEventsPerRun);
                    runtimeLimitReached = true;
                    break;
                }
                continue;
            }

            StartHandler(next);
            if (next.NativeHandler is not null)
            {
                try { next.NativeHandler.Handle(_activeMessage.Message, _context); }
                catch (GameEventScriptFatalRuntimeException exception) { RecordRuntimeError(exception.Diagnostic); }
                catch (Exception exception)
                {
                    RecordRuntimeError(CreateRuntimeDiagnostic(
                        GameEventScriptDiagnosticCodes.RuntimeNativeHandlerFailure,
                        "Native message handler failed.",
                        exception.GetType().Name + ": " + exception.Message));
                }
                CompleteActiveHandler();
                continue;
            }

            try
            {
                GameEventScriptVirtualMachine.Begin(_vmState!, next.Instance!.LinkedProgram, _activeMessage.Message,
                    next.MatchArguments, next.EntryAddress, next.DispatchSignatureId, _context);
            }
            catch (GameEventScriptFatalRuntimeException exception)
            {
                RecordRuntimeError(exception.Diagnostic);
                _vmState!.Reset();
                CompleteActiveHandler();
                continue;
            }
            _scriptHandlerActive = true;
        }

        return CreateResult(runtimeLimitReached);
    }

    public GameEventScriptExecutionResult RunToCompletion()
    {
        var totalOpcodes = 0;
        var totalProcessed = 0;
        var totalEmitted = 0;
        var totalPublished = 0;
        GameEventScriptExecutionResult step;
        do
        {
            step = ExecuteFrame(int.MaxValue);
            totalOpcodes += step.ExecutedOpcodes;
            totalProcessed += step.ProcessedMessages;
            totalEmitted += step.EmittedMessages;
            totalPublished += step.PublishedMessages;
        }
        while (step.State == GameEventScriptExecutionState.Paused &&
               (step.ExecutedOpcodes > 0 || step.ProcessedMessages > 0 || step.EmittedMessages > 0 || step.PublishedMessages > 0));

        return new GameEventScriptExecutionResult(step.State, totalOpcodes, totalProcessed, totalEmitted, totalPublished, step.Diagnostic);
    }

    internal bool EmitFromContext(GameEventScriptMessage message)
    {
        var accepted = EnqueueMessage(message);
        _stepEmittedMessages++;
        _observer?.MessageEmitted(message, accepted);
        return accepted;
    }

    internal GameEventScriptPublishResult PublishFromContext(GameEventScriptMessage message)
    {
        var localAccepted = EnqueueMessage(message);
        var attempted = _publishSink is not null;
        var outboundAccepted = false;
        if (_publishSink is not null)
        {
            try { outboundAccepted = _publishSink.Publish(message); }
            catch (Exception exception)
            {
                outboundAccepted = false;
                _observer?.RuntimeError(CreateRuntimeDiagnostic(
                    GameEventScriptDiagnosticCodes.RuntimePublishSinkFailure,
                    "Publish sink failed.",
                    exception.GetType().Name + ": " + exception.Message));
            }
        }

        var result = new GameEventScriptPublishResult(localAccepted, attempted, outboundAccepted);
        _stepPublishedMessages++;
        _observer?.MessagePublished(message, result);
        return result;
    }

    private void BeginStep()
    {
        _stepExecutedOpcodes = 0;
        _stepProcessedMessages = 0;
        _stepEmittedMessages = 0;
        _stepPublishedMessages = 0;
        _stepRuntimeDiagnostic = null;
    }

    private GameEventScriptExecutionResult CreateResult(bool runtimeLimitReached)
        => new(_stepRuntimeDiagnostic is not null
                ? GameEventScriptExecutionState.RuntimeError
                : runtimeLimitReached
                ? GameEventScriptExecutionState.RuntimeLimitReached
                : IsIdle ? GameEventScriptExecutionState.Completed : GameEventScriptExecutionState.Paused,
            _stepExecutedOpcodes, _stepProcessedMessages, _stepEmittedMessages, _stepPublishedMessages,
            _stepRuntimeDiagnostic);

    private void RecordRuntimeError(GameEventScriptDiagnostic diagnostic)
    {
        _stepRuntimeDiagnostic ??= diagnostic;
        _observer?.RuntimeError(diagnostic);
    }

    private GameEventScriptDiagnostic CreateRuntimeDiagnostic(string code, string message, string? technicalDetails = null)
        => new(
            GameEventScriptDiagnosticPhase.Runtime,
            code,
            message,
            ProgramName: _activeHandler?.Instance?.Program.ModuleName,
            HandlerName: _activeHandler?.DispatchSignatureId,
            TechnicalDetails: technicalDetails);

    private bool StartNextMessage()
    {
        if (!_queue.Dequeue(out _activeMessage)) return false;
        _hasActiveMessage = true;
        return true;
    }

    private void StartHandler(SubscriptionEntry handler)
    {
        _activeHandler = handler;
        _context.BeginHandler();
        _observer?.DispatchStarted(_activeMessage.Message, handler.DispatchSignatureId);
    }

    private void CompleteActiveHandler()
    {
        var handler = _activeHandler;
        _scriptHandlerActive = false;
        _activeHandler = null;
        if (_pendingVmWarmupCapacity > 0 && _vmState is not null && _vmState.PrepareCapacity(_pendingVmWarmupCapacity))
            _pendingVmWarmupCapacity = 0;
        if (handler is not null) _observer?.DispatchCompleted(_activeMessage.Message, handler.DispatchSignatureId);
    }

    private bool EnqueueMessage(GameEventScriptMessage message)
    {
        if (string.IsNullOrWhiteSpace(message.Name)) return false;
        var exact = Get(_exact, message.SignatureId);
        var names = Get(_byName, message.Name);
        if (!HasMatch(message, exact, names))
        {
            if (GameEventScriptSystemEndpoints.IsUndeliverableName(message.Name)) return false;
            exact = Get(_exact, GameEventScriptSystemEndpoints.UndeliverableSignatureId);
            names = Get(_byName, GameEventScriptSystemEndpoints.UndeliverableName);
            if (!HasMatch(message, exact, names)) return false;
        }

        return EnqueuePlan(new PendingMessage(message, exact, names));
    }

    private bool EnqueuePlan(PendingMessage message)
    {
        if (_limits.MaxQueuedMessagesPerRun > 0 && _queue.Count >= _limits.MaxQueuedMessagesPerRun)
        {
            _context.RecordRuntimeLimitReached(nameof(GameEventScriptRuntimeLimits.MaxQueuedMessagesPerRun),
                $"Message queue limit reached. Dropped '{message.Message.Name}'.", _limits.MaxQueuedMessagesPerRun);
            return false;
        }

        _queue.Enqueue(message);
        return true;
    }

    private void Register(SubscriptionEntry entry)
    {
        var index = entry.MatchArguments ? _exact : _byName;
        var key = entry.MatchArguments ? entry.Signature.SignatureId : entry.Signature.Name;
        index[key] = index.TryGetValue(key, out var entries) ? Insert(entries, entry) : [entry];
    }

    private bool Remove(SubscriptionEntry entry)
    {
        var index = entry.MatchArguments ? _exact : _byName;
        var key = entry.MatchArguments ? entry.Signature.SignatureId : entry.Signature.Name;
        if (!index.TryGetValue(key, out var entries)) return false;
        var updated = Remove(entries, entry);
        if (ReferenceEquals(updated, entries)) return false;
        if (updated.Length == 0) index.Remove(key); else index[key] = updated;
        return true;
    }

    internal bool IsInstanceAttached(long registrationId)
    {
        for (var current = _instances; current is not null; current = current.NextRegistration)
            if (current.RegistrationId == registrationId) return true;
        return false;
    }

    internal bool DetachInstance(long registrationId)
    {
        GameEventScriptInstance? previous = null;
        var current = _instances;
        while (current is not null && current.RegistrationId != registrationId)
        {
            previous = current;
            current = current.NextRegistration;
        }

        if (current is null) return false;
        if (previous is null) _instances = current.NextRegistration;
        else previous.NextRegistration = current.NextRegistration;
        current.NextRegistration = null;
        RemoveInstanceRegistrations(current);
        return true;
    }

    internal bool IsSubscriptionRegistered(long registrationId)
    {
        for (var current = _nativeSubscriptions; current is not null; current = current.NextNativeRegistration)
            if (current.RegistrationId == registrationId) return true;
        return false;
    }

    internal bool Unsubscribe(long registrationId)
    {
        SubscriptionEntry? previous = null;
        var current = _nativeSubscriptions;
        while (current is not null && current.RegistrationId != registrationId)
        {
            previous = current;
            current = current.NextNativeRegistration;
        }

        if (current is null) return false;
        if (previous is null) _nativeSubscriptions = current.NextNativeRegistration;
        else previous.NextNativeRegistration = current.NextNativeRegistration;
        current.NextNativeRegistration = null;
        return Remove(current);
    }

    private void RemoveInstanceRegistrations(GameEventScriptInstance instance)
    {
        var handlers = instance.LinkedProgram.Handlers;
        for (var handlerIndex = 0; handlerIndex < handlers.Length; handlerIndex++)
        {
            var handler = handlers[handlerIndex];
            if (GameEventScriptSystemEndpoints.IsInitializationName(handler.Signature.Name)) continue;
            var index = handler.MatchArguments ? _exact : _byName;
            var key = handler.MatchArguments ? handler.Signature.SignatureId : handler.Signature.Name;
            if (!index.TryGetValue(key, out var entries)) continue;
            var updated = Remove(entries, instance);
            if (ReferenceEquals(updated, entries)) continue;
            if (updated.Length == 0) index.Remove(key); else index[key] = updated;
        }
    }

    private long NextRegistrationId()
    {
        if (_nextRegistrationId == long.MaxValue)
            throw new InvalidOperationException("The host registration ID space is exhausted.");
        return ++_nextRegistrationId;
    }

    private static SubscriptionEntry[] Get(Dictionary<string, SubscriptionEntry[]> index, string key)
        => index.TryGetValue(key, out var entries) ? entries : [];

    private static bool HasMatch(GameEventScriptMessage message, SubscriptionEntry[] exact, SubscriptionEntry[] names)
    {
        for (var index = 0; index < exact.Length; index++) if (exact[index].Matches(message)) return true;
        for (var index = 0; index < names.Length; index++) if (names[index].Matches(message)) return true;
        return false;
    }

    private static SubscriptionEntry[] Insert(SubscriptionEntry[] entries, SubscriptionEntry entry)
    {
        var result = new SubscriptionEntry[entries.Length + 1];
        var target = entries.Length;
        for (var index = 0; index < entries.Length; index++)
            if (Compare(entry, entries[index]) < 0) { target = index; break; }
        Array.Copy(entries, 0, result, 0, target);
        result[target] = entry;
        Array.Copy(entries, target, result, target + 1, entries.Length - target);
        return result;
    }

    private static SubscriptionEntry[] Remove(SubscriptionEntry[] entries, SubscriptionEntry entry)
    {
        var index = Array.IndexOf(entries, entry);
        if (index < 0) return entries;
        if (entries.Length == 1) return [];
        var result = new SubscriptionEntry[entries.Length - 1];
        Array.Copy(entries, 0, result, 0, index);
        Array.Copy(entries, index + 1, result, index, entries.Length - index - 1);
        return result;
    }

    private static SubscriptionEntry[] Remove(SubscriptionEntry[] entries, GameEventScriptInstance instance)
    {
        var removedCount = 0;
        for (var index = 0; index < entries.Length; index++)
            if (ReferenceEquals(entries[index].Instance, instance)) removedCount++;
        if (removedCount == 0) return entries;
        if (removedCount == entries.Length) return [];
        var result = new SubscriptionEntry[entries.Length - removedCount];
        var target = 0;
        for (var index = 0; index < entries.Length; index++)
            if (!ReferenceEquals(entries[index].Instance, instance)) result[target++] = entries[index];
        return result;
    }

    private static SubscriptionEntry[] Sort(SubscriptionEntry[] entries)
    {
        Array.Sort(entries, Compare);
        return entries;
    }

    private static int Compare(SubscriptionEntry left, SubscriptionEntry right)
    {
        var priority = right.Priority.CompareTo(left.Priority);
        return priority != 0 ? priority : left.RegistrationOrder.CompareTo(right.RegistrationOrder);
    }

    private sealed class SubscriptionEntry
    {
        private SubscriptionEntry(
            GameEventScriptMessageSignature signature,
            string[] requiredTags,
            string[] excludedTags,
            bool matchArguments,
            int priority,
            long registrationOrder,
            long registrationId,
            IGameEventScriptNativeMessageHandler? nativeHandler,
            GameEventScriptInstance? instance,
            ushort entryAddress)
        {
            Signature = signature;
            RequiredTags = requiredTags;
            ExcludedTags = excludedTags;
            MatchArguments = matchArguments;
            Priority = priority;
            RegistrationOrder = registrationOrder;
            RegistrationId = registrationId;
            NativeHandler = nativeHandler;
            Instance = instance;
            EntryAddress = entryAddress;
        }

        internal GameEventScriptMessageSignature Signature { get; }
        internal string[] RequiredTags { get; }
        internal string[] ExcludedTags { get; }
        internal bool MatchArguments { get; }
        internal int Priority { get; }
        internal long RegistrationOrder { get; }
        internal long RegistrationId { get; }
        internal IGameEventScriptNativeMessageHandler? NativeHandler { get; }
        internal GameEventScriptInstance? Instance { get; }
        internal ushort EntryAddress { get; }
        internal SubscriptionEntry? NextNativeRegistration { get; set; }
        internal string DispatchSignatureId => MatchArguments ? Signature.SignatureId : $"{Signature.Name}(*)";

        internal static SubscriptionEntry ForNative(
            long registrationId,
            GameEventScriptMessageSignature signature,
            IGameEventScriptNativeMessageHandler handler,
            IReadOnlyList<string> requiredTags,
            IReadOnlyList<string> excludedTags,
            bool matchArguments,
            int priority,
            long order)
            => new(signature, Copy(requiredTags), Copy(excludedTags), matchArguments, priority, order, registrationId, handler, null, 0);

        internal static SubscriptionEntry ForScript(GameEventScriptInstance instance, GesLinkedProgram.Handler handler, int priority, long order)
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
    }

    private struct PendingMessage
    {
        private readonly SubscriptionEntry[] _exact;
        private readonly SubscriptionEntry[] _names;
        private int _exactIndex;
        private int _nameIndex;

        internal PendingMessage(GameEventScriptMessage message, SubscriptionEntry[] exact, SubscriptionEntry[] names)
        {
            Message = message;
            _exact = exact;
            _names = names;
            _exactIndex = 0;
            _nameIndex = 0;
        }

        internal GameEventScriptMessage Message { get; }

        internal SubscriptionEntry? NextMatching()
        {
            while (_exactIndex < _exact.Length || _nameIndex < _names.Length)
            {
                SubscriptionEntry next;
                if (_exactIndex >= _exact.Length) next = _names[_nameIndex++];
                else if (_nameIndex >= _names.Length) next = _exact[_exactIndex++];
                else if (Compare(_exact[_exactIndex], _names[_nameIndex]) <= 0) next = _exact[_exactIndex++];
                else next = _names[_nameIndex++];
                if (next.Matches(Message)) return next;
            }

            return null;
        }
    }

    private sealed class MessageRingQueue
    {
        private PendingMessage[] _items;
        private int _head;
        private int _tail;

        internal MessageRingQueue(int capacity) => _items = new PendingMessage[Math.Max(1, capacity)];
        internal int Count { get; private set; }

        internal void Enqueue(PendingMessage item)
        {
            if (Count == _items.Length) Grow();
            _items[_tail] = item;
            _tail = (_tail + 1) % _items.Length;
            Count++;
        }

        internal bool Dequeue(out PendingMessage item)
        {
            if (Count == 0) { item = default; return false; }
            item = _items[_head];
            _items[_head] = default;
            _head = (_head + 1) % _items.Length;
            Count--;
            return true;
        }

        private void Grow()
        {
            var expanded = new PendingMessage[_items.Length * 2];
            for (var index = 0; index < Count; index++) expanded[index] = _items[(_head + index) % _items.Length];
            _items = expanded;
            _head = 0;
            _tail = Count;
        }
    }
}
