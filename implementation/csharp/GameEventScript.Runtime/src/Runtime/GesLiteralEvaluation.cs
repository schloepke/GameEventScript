// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0
using System.Collections.Generic;
using GameEventScript.Api;
using GameEventScript.Runtime.Values;
using GameEventScript.Runtime.VM;
using static GameEventScript.Api.GameEventScriptBindingSegment;

namespace GameEventScript.Runtime;
// Parser-private nodes never escape into a public value or a Program.
internal sealed record GesLiteralNode(string Type, string[] Labels, GesValue[] Arguments, GameEventScriptBinaryBindEntry? Constructor = null)
{
    internal GesValue Value => new()
    {
        Kind = GameEventScriptBytecodeTypeKind.Custom,
        ObjectValue = this
    };
}

internal sealed class GesLiteralEvaluation
{
    private sealed record Operation(GesLiteralNode Node, int[] Inputs);
    private readonly List<Operation> _operations = new();
    private readonly List<GesValue> _results = new();
    private readonly GesVmState _state;
    private readonly GameEventScriptContext _context;
    private readonly ushort _destination;
    private int _next;
    private GesLiteralEvaluation(GesValue root, GesVmState state, GameEventScriptContext context, ushort destination)
    {
        _state = state;
        _context = context;
        _destination = destination;
        Add(root);
    }

    internal static void Start(GesValue root, GesVmState state, GameEventScriptContext context, ushort destination)
    {
        if (!ContainsNode(root))
        {
            state.SetValue(destination, root);
            return;
        }

        new GesLiteralEvaluation(root, state, context, destination).Run();
    }

    private static bool ContainsNode(GesValue value)
    {
        if (value.ObjectValue is GesLiteralNode)
            return true;
        if (value.ObjectValue is GesValue[] list)
        {
            foreach (var item in list)
                if (ContainsNode(item))
                    return true;
        }

        if (value.ObjectValue is GesValueMap map)
        {
            for (var i = 0; i < map.Length; i++)
                if (ContainsNode(map.ValueAt(i)))
                    return true;
        }

        return false;
    }

    private int Add(GesValue value)
    {
        GesLiteralNode node;
        if (value.ObjectValue is GesLiteralNode literal)
            node = literal;
        else if (value.ObjectValue is GesValue[] list && value.Kind == GameEventScriptBytecodeTypeKind.List)
            node = new("@list", [], list);
        else if (value.ObjectValue is GesValueMap map && value.Kind == GameEventScriptBytecodeTypeKind.Map)
        {
            var keys = new string[map.Length];
            var values = new GesValue[map.Length];
            for (var i = 0; i < map.Length; i++)
            {
                keys[i] = map.KeyAt(i);
                values[i] = map.ValueAt(i);
            }

            node = new("@map", keys, values);
        }
        else
            node = new("@value", [], [value]);
        var inputs = new int[node.Type == "@value" ? 0 : node.Arguments.Length];
        for (var i = 0; i < inputs.Length; i++)
            inputs[i] = Add(node.Arguments[i]);
        var index = _operations.Count;
        _operations.Add(new(node, inputs));
        _results.Add(default);
        return index;
    }

    internal void Resume(GesValue value)
    {
        _results[_next++] = value;
        Run();
    }

    private void Run()
    {
        while (_next < _operations.Count && !_context.RuntimeBudget.IsExhausted)
        {
            var operation = _operations[_next];
            var node = operation.Node;
            var args = new GesValue[operation.Inputs.Length];
            for (var i = 0; i < args.Length; i++)
                args[i] = _results[operation.Inputs[i]];
            if (node.Constructor is { } constructor)
            {
                _state.ClearStage();
                var unnamed = 0;
                foreach (var parameter in constructor.ArgumentNames)
                {
                    var label = _state.FetchStringByPointer(parameter);
                    var match = -1;
                    for (var i = label == "_" ? unnamed : 0; i < node.Labels.Length; i++)
                        if (node.Labels[i] == label)
                        {
                            match = i;
                            if (label == "_")
                                unnamed = i + 1;
                            break;
                        }

                    _state.StageValue(match < 0 ? default : args[match]);
                }

                if (_state.State == GesVmState.StateValue.Processing && _state.CallAddress(constructor.EntryAddress, _destination))
                    _state.CallStack[_state.CallStackPointer - 1].LiteralContinuation = this;
                return;
            }

            GesValue result;
            if (node.Type == "@value")
                result = node.Arguments[0];
            else if (node.Type == "@list")
            {
                result = new();
                result.SetList(args);
            }
            else if (node.Type == "@map")
            {
                var fields = new Dictionary<string, GesValue>(System.StringComparer.Ordinal);
                for (var i = 0; i < args.Length; i++)
                    fields[node.Labels[i]] = args[i];
                var keys = new string[fields.Count];
                var values = new GesValue[fields.Count];
                var index = 0;
                foreach (var field in fields)
                {
                    keys[index] = field.Key;
                    values[index++] = field.Value;
                }

                result = new();
                result.SetMap(new GesValueMap(keys, values, values.Length));
            }
            else if (node.Type.StartsWith("@message:", System.StringComparison.Ordinal))
            {
                var arguments = new GameEventScriptMessageArgument[args.Length];
                for (var i = 0; i < args.Length; i++)
                    arguments[i] = new(node.Labels[i], args[i]);
                result = GesValue.GesMessage(GameEventScriptMessage.Create(node.Type.Substring(9), arguments));
            }
            else
                result = GesDataConstruction.Create(node.Type, node.Labels, args, _state, _context);
            _results[_next++] = result;
        }

        if (_next == _operations.Count)
            _state.SetValue(_destination, _results[_results.Count - 1]);
    }
}
