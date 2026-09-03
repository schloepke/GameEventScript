using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace StepH.GameEventScript.Conformance;

internal sealed class ConformanceCanonicalJsonWriter
{
    private readonly StringBuilder _text = new();
    private readonly List<bool> _first = new();
    private int _depth;
    private bool _afterName;

    internal void BeginObject() { BeforeValue(); _text.Append('{'); Open(); }
    internal void EndObject() { Close(); _text.Append('}'); }
    internal void BeginArray() { BeforeValue(); _text.Append('['); Open(); }
    internal void EndArray() { Close(); _text.Append(']'); }
    internal void Name(string name) { Next(); AppendString(name); _text.Append(": "); _afterName = true; }
    internal void String(string name, string value) { Name(name); StringValueRaw(value); _afterName = false; }
    internal void Number(string name, int value) { Name(name); _text.Append(value.ToString(CultureInfo.InvariantCulture)); _afterName = false; }
    internal void Number(string name, uint value) { Name(name); _text.Append(value.ToString(CultureInfo.InvariantCulture)); _afterName = false; }
    internal void Boolean(string name, bool value) { Name(name); _text.Append(value ? "true" : "false"); _afterName = false; }
    internal void Null(string name) { Name(name); _text.Append("null"); _afterName = false; }
    internal void StringValue(string value) { BeforeValue(); StringValueRaw(value); }
    internal string Complete() => _text.Append('\n').ToString();

    private void Open() { _depth++; _first.Add(true); }

    private void Close()
    {
        var hadValues = !_first[^1];
        _first.RemoveAt(_first.Count - 1);
        _depth--;
        if (hadValues) NewLine();
    }

    private void BeforeValue()
    {
        if (_afterName) { _afterName = false; return; }
        if (_first.Count == 0) return;
        Next();
    }

    private void Next()
    {
        if (_first[^1]) _first[^1] = false;
        else _text.Append(',');
        NewLine();
    }

    private void NewLine() { _text.Append('\n'); _text.Append(' ', _depth * 2); }
    private void StringValueRaw(string value) => AppendString(value);

    private void AppendString(string value)
    {
        _text.Append('"');
        for (var index = 0; index < value.Length; index++)
        {
            var c = value[index];
            switch (c)
            {
                case '"': _text.Append("\\\""); break;
                case '\\': _text.Append("\\\\"); break;
                case '\b': _text.Append("\\b"); break;
                case '\f': _text.Append("\\f"); break;
                case '\n': _text.Append("\\n"); break;
                case '\r': _text.Append("\\r"); break;
                case '\t': _text.Append("\\t"); break;
                default:
                    if (c < 0x20) _text.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                    else _text.Append(c);
                    break;
            }
        }
        _text.Append('"');
    }
}
