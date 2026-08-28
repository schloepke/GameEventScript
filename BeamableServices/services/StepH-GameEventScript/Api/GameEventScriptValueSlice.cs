#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace StepH.GameEventScript.Api;

public readonly struct GameEventScriptValueSlice
{
    private readonly GameEventScriptValue[]? _values;

    public static readonly GameEventScriptValueSlice Empty = new([]);

    public GameEventScriptValueSlice(GameEventScriptValue[] values)
    {
        _values = values;
        Start = 0;
        Length = values.Length;
    }

    internal GameEventScriptValueSlice(GameEventScriptValue[] values, int start, int length)
    {
        _values = values;
        Start = start;
        Length = length;
    }

    public int Start { get; }

    public int Length { get; }

    public GameEventScriptValue this[int index] => _values![Start + index];
}
