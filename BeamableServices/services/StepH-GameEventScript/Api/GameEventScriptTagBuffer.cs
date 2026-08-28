using System;

namespace StepH.GameEventScript.Api;

internal sealed class GameEventScriptTagBuffer
{
    private string[] _tags;
    private int _count;

    internal GameEventScriptTagBuffer(int capacity)
    {
        _tags = capacity <= 0 ? [] : new string[capacity];
    }

    internal int Count => _count;

    internal void Add(string? tag)
    {
        var normalized = GameEventScriptMessage.NormalizeTagName(tag);
        if (normalized.Length == 0)
        {
            return;
        }

        for (var index = 0; index < _count; index++)
        {
            if (string.Equals(_tags[index], normalized, StringComparison.Ordinal))
            {
                return;
            }
        }

        if (_count == _tags.Length)
        {
            var next = new string[_tags.Length == 0 ? 4 : _tags.Length << 1];
            Array.Copy(_tags, next, _tags.Length);
            _tags = next;
        }

        _tags[_count++] = normalized;
    }

    internal string[] ToArrayOrEmpty()
    {
        if (_count == 0)
        {
            return [];
        }

        if (_count == _tags.Length)
        {
            return _tags;
        }

        var result = new string[_count];
        Array.Copy(_tags, result, _count);
        return result;
    }
}
