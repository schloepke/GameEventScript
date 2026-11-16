// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;

namespace GameEventScript.Generator;

// Keyed lists in deltas of generated C# types: the key order of their elements and the sections of their delta.
internal static partial class GesCSharpEmitter
{
    // The key of an element of a keyed list as an unsigned order: a signed integer without range has its sign bit flipped.
    private static void KeyOrder(GesSchemaPlanner.Code code, GesPlanType type, string self)
    {
        var key = type.Fields[0];
        var flip = key.Value.Encoding.Kind == GesLayoutKind.Integer && !key.Value.Encoding.Range ? " ^ 0x8000000000000000UL" : "";
        code.Blank();
        code.Line($"internal {self}ulong __KeyOrder(string[] paths, int at) => __Wire0({key.Base}, paths, at){flip};");
        code.Blank();
        code.Line($"internal static ulong __ReadKeyOrder(ref GesBitReader reader, string[] paths, int at) => __Read0(ref reader, paths, at){flip};");
    }

    // The delta of a keyed list: unchanged, whole when the keys of one side do not ascend strictly, and otherwise its removed,
    // added and changed elements, each section a merge of both lists by key. Counts are set once a section is written.
    private static void KeyedHelpers(GesSchemaPlanner.Code code, GesPlanValue value, string owner, string global)
    {
        foreach (var alternative in value.Alternatives) KeyedHelpers(code, alternative, owner, global);
        if (!GesSchemaPlanner.Keyed(value)) return;
        var encoding = value.Encoding;
        var element = value.Element!;
        var node = N(value.Node);
        var name = ElementName(value);
        var at = $"at + {N(element.Node + 1)}";
        var width = encoding.Width.ToString(CultureInfo.InvariantCulture);
        var positionWidth = encoding.PositionWidth.ToString(CultureInfo.InvariantCulture);
        var max = encoding.MaxCount.ToString(CultureInfo.InvariantCulture);
        var pool = $"__{value.Base}Pool";
        var next = NextLevel(element);
        var depth = value.Depth.ToString(CultureInfo.InvariantCulture);

        code.Blank();
        code.Line($"private static bool __Ascending{node}(List<{name}> list, string[] paths, int at)");
        code.Open("{");
        code.Line("var previous = 0UL;");
        code.Line("for (var position = 0; position < list.Count; position++)");
        code.Open("{");
        code.Line($"var order = list[position].__KeyOrder(paths, {at});");
        code.Line("if (position > 0 && order <= previous) return false;");
        code.Line("previous = order;");
        code.Close();
        code.Line("return true;");
        code.Close();

        code.Blank();
        code.Line($"private bool __WriteKeyed{node}({owner} baseline, ref GesBitWriter writer, string[] paths, int at{(value.Depth > 0 ? ", int level" : "")})");
        code.Open("{");
        code.Line($"var list = {value.Base};");
        code.Line($"var held = baseline.{value.Base};");
        if (value.Depth > 0)
        {
            // At its depth the List is no longer keyed: it holds nothing and never changes.
            code.Line($"if (level >= {depth})");
            code.Open("{");
            code.Line($"if (list.Count + held.Count > 0) throw new GesWireException(GesWireException.TooDeep, {Path(value)}, \"deeper than depth {depth}\");");
            code.Line("return false;");
            code.Close();
            code.Blank();
        }

        code.Line($"if (list.Count > {max} || held.Count > {max}) {Throw("TooLong", value)}");
        code.Line($"var listAscending = __Ascending{node}(list, paths, at);");
        code.Line($"var heldAscending = __Ascending{node}(held, paths, at);");
        code.Line("if (!listAscending || !heldAscending)");
        code.Open("{");
        code.Line($"if (__Same{node}(baseline, paths, at{Level(value)})) return false;");
        code.Line("writer.Write(1, 1);");
        Write(code, value, "list");
        code.Line("return true;");
        code.Close();
        code.Blank();
        code.Line("var entry = writer.BitPosition;");
        code.Line("writer.Write(0, 1);");
        code.Line("var countBit = writer.BitPosition;");
        code.Line($"writer.Write(0, {width});");
        code.Line("var removed = 0UL;");
        code.Line("var other = 0;");
        code.Line("for (var position = 0; position < held.Count; position++)");
        code.Open("{");
        Match(code, "held", "list", at);
        code.Line("if (kept) continue;");
        code.Line($"writer.Write((ulong)position, {positionWidth});");
        code.Line("removed++;");
        code.Close();
        code.Line($"__SetCount(ref writer, countBit, removed, {width});");
        code.Blank();
        code.Line("countBit = writer.BitPosition;");
        code.Line($"writer.Write(0, {width});");
        code.Line("var added = 0UL;");
        code.Line("other = 0;");
        code.Line("for (var position = 0; position < list.Count; position++)");
        code.Open("{");
        Match(code, "list", "held", at);
        code.Line("if (kept) continue;");
        code.Line($"list[position].__WriteFields(ref writer, paths, {at}{next});");
        code.Line("added++;");
        code.Close();
        code.Line($"__SetCount(ref writer, countBit, added, {width});");
        code.Blank();
        code.Line("countBit = writer.BitPosition;");
        code.Line($"writer.Write(0, {width});");
        code.Line("var changed = 0UL;");
        code.Line("other = 0;");
        code.Line("for (var position = 0; position < held.Count; position++)");
        code.Open("{");
        Match(code, "held", "list", at);
        code.Line("if (!kept) continue;");
        code.Line("var start = writer.BitPosition;");
        code.Line($"writer.Write((ulong)position, {positionWidth});");
        code.Line($"if (list[other].__WriteDelta(held[position], ref writer, paths, {at}, true{next})) changed++;");
        code.Line("else writer.Rewind(start);");
        code.Close();
        code.Line($"__SetCount(ref writer, countBit, changed, {width});");
        code.Line("if (removed > 0 || added > 0 || changed > 0) return true;");
        code.Line("writer.Rewind(entry);");
        code.Line("return false;");
        code.Close();

        // Checks run in the wire order of the reference decoder: the baseline's order at the form bit, the positions as
        // they are read, an added key that the baseline holds at that element. A first pass validates the added elements;
        // the merge then reads them again where they belong.
        code.Blank();
        code.Line($"private void __ReadKeyed{node}({owner} baseline, ref GesBitReader reader, string[] paths, int at{(value.Depth > 0 ? ", int level" : "")})");
        code.Open("{");
        code.Line($"var held = baseline.{value.Base};");
        if (value.Depth > 0)
        {
            // At its depth the List takes no bits, so a change there is none.
            code.Line($"if (level >= {depth})");
            code.Open("{");
            code.Line($"{value.Base}.Clear();");
            code.Line($"if (__Same{node}(baseline, paths, at, level)) {Throw("InvalidDelta", value)}");
            code.Line("return;");
            code.Close();
            code.Blank();
        }

        code.Line($"if (reader.Read(1, {Path(value)}) == 1)");
        code.Open("{");
        Read(code, value, value.Base);
        code.Line($"var listAscending = __Ascending{node}({value.Base}, paths, at);");
        code.Line($"var heldAscending = __Ascending{node}(held, paths, at);");
        code.Line($"if (listAscending && heldAscending) {Throw("InvalidDelta", value)}");
        code.Line($"if (__Same{node}(baseline, paths, at{Level(value)})) {Throw("InvalidDelta", value)}");
        code.Line("return;");
        code.Close();
        code.Blank();
        code.Line($"if (!__Ascending{node}(held, paths, at)) {Throw("InvalidDelta", value)}");
        code.Line("var baselineCount = (ulong)held.Count;");
        Count(code, value, "removedLeft", width, max);
        code.Line("var removedCursor = reader.BitPosition;");
        code.Line("for (ulong item = 0, previous = 0; item < removedLeft; item++)");
        code.Open("{");
        code.Line($"var position = reader.Read({positionWidth}, {Path(value)});");
        code.Line($"if (position >= baselineCount || (item > 0 && position <= previous)) {Throw("InvalidDelta", value)}");
        code.Line("previous = position;");
        code.Close();
        Count(code, value, "addedLeft", width, max);
        code.Line("var addedCursor = reader.BitPosition;");
        code.Line("var other = 0;");
        code.Line("for (ulong item = 0, previous = 0; item < addedLeft; item++)");
        code.Open("{");
        code.Line("var start = reader.BitPosition;");
        code.Line($"var order = {global}{name}.__ReadKeyOrder(ref reader, paths, {at});");
        code.Line($"if (item > 0 && order <= previous) {Throw("InvalidDelta", value)}");
        code.Line("previous = order;");
        code.Line("for (; other < held.Count; other++)");
        code.Open("{");
        code.Line($"var next = held[other].__KeyOrder(paths, {at});");
        code.Line($"if (next == order) {Throw("InvalidDelta", value)}");
        code.Line("if (next > order) break;");
        code.Close();
        code.Line("reader.Seek(start);");
        if (element.IsClass)
        {
            code.Line($"if ({pool}.Count == 0) {pool}.Add(new {name}());");
            code.Line($"{pool}[0].__ReadFields(ref reader, paths, {at}{next});");
        }
        else
        {
            code.Line($"var scratch = new {name}();");
            code.Line($"scratch.__ReadFields(ref reader, paths, {at}{next});");
        }

        code.Close();
        Count(code, value, "changedLeft", width, max);
        code.Line("var changedCursor = reader.BitPosition;");
        code.Line($"if (removedLeft == 0 && addedLeft == 0 && changedLeft == 0) {Throw("InvalidDelta", value)}");
        code.Line($"if (baselineCount - removedLeft + addedLeft > {max}) {Throw("InvalidDelta", value)}");
        code.Blank();
        code.Line($"var list = {value.Base};");
        code.Line("list.Clear();");
        code.Line($"var nextRemoved = __NextPosition(ref reader, ref removedCursor, ref removedLeft, {positionWidth}, {Path(value)});");
        code.Line($"var nextChanged = __NextPosition(ref reader, ref changedCursor, ref changedLeft, {positionWidth}, {Path(value)});");
        code.Line("var addedOrder = 0UL;");
        code.Line("if (addedLeft > 0)");
        code.Open("{");
        code.Line("reader.Seek(addedCursor);");
        code.Line($"addedOrder = {global}{name}.__ReadKeyOrder(ref reader, paths, {at});");
        code.Close();
        code.Line("for (var position = 0UL; position < baselineCount; position++)");
        code.Open("{");
        code.Line("var element = held[(int)position];");
        code.Line($"var order = element.__KeyOrder(paths, {at});");
        code.Line("while (addedLeft > 0 && addedOrder < order)");
        code.Open("{");
        ReadAdded(code, value, name, at, pool, global);
        code.Close();
        code.Line("if (position == nextRemoved)");
        code.Open("{");
        code.Line($"if (position == nextChanged) {Throw("InvalidDelta", value)}");
        code.Line($"nextRemoved = __NextPosition(ref reader, ref removedCursor, ref removedLeft, {positionWidth}, {Path(value)});");
        code.Close();
        code.Line("else if (position == nextChanged)");
        code.Open("{");
        Slot(code, element, name, pool);
        code.Line("reader.Seek(changedCursor);");
        code.Line($"if (!item.__ReadDelta(element, ref reader, paths, {at}, true{next})) {Throw("InvalidDelta", element)}");
        code.Line("changedCursor = reader.BitPosition;");
        code.Line("list.Add(item);");
        code.Line("var before = nextChanged;");
        code.Line($"nextChanged = __NextPosition(ref reader, ref changedCursor, ref changedLeft, {positionWidth}, {Path(value)});");
        code.Line($"if (nextChanged <= before) {Throw("InvalidDelta", value)}");
        code.Close();
        code.Line("else");
        code.Open("{");
        if (element.IsClass)
        {
            Slot(code, element, name, pool);
            code.Line("item.CopyFrom(element);");
            code.Line("list.Add(item);");
        }
        else code.Line("list.Add(element);");
        code.Close();
        code.Close();
        code.Blank();
        code.Line("// A changed position beyond the baseline was never reached.");
        code.Line($"if (nextChanged != ulong.MaxValue) {Throw("InvalidDelta", value)}");
        code.Line("while (addedLeft > 0)");
        code.Open("{");
        ReadAdded(code, value, name, at, pool, global);
        code.Close();
        code.Line("reader.Seek(changedCursor);");
        code.Close();
    }

    // Moves `other` to the first element of the other list whose key is not smaller, and tells whether it has the same key.
    private static void Match(GesSchemaPlanner.Code code, string list, string otherList, string at)
    {
        code.Line($"var order = {list}[position].__KeyOrder(paths, {at});");
        code.Line("var kept = false;");
        code.Line($"for (; other < {otherList}.Count; other++)");
        code.Open("{");
        code.Line($"var next = {otherList}[other].__KeyOrder(paths, {at});");
        code.Line("if (next < order) continue;");
        code.Line("kept = next == order;");
        code.Line("break;");
        code.Close();
    }

    private static void Count(GesSchemaPlanner.Code code, GesPlanValue value, string name, string width, string max)
    {
        code.Line($"var {name} = reader.Read({width}, {Path(value)});");
        code.Line($"if ({name} > {max}) {Throw("InvalidCount", value)}");
    }

    // The storage for the next element of the list: a pooled instance, or a new value.
    private static void Slot(GesSchemaPlanner.Code code, GesPlanValue element, string name, string pool)
    {
        if (!element.IsClass)
        {
            code.Line($"var item = new {name}();");
            return;
        }

        code.Line($"if (list.Count == {pool}.Count) {pool}.Add(new {name}());");
        code.Line($"var item = {pool}[list.Count];");
    }

    private static void ReadAdded(GesSchemaPlanner.Code code, GesPlanValue value, string name, string at, string pool, string global)
    {
        Slot(code, value.Element!, name, pool);
        code.Line("reader.Seek(addedCursor);");
        code.Line($"item.__ReadFields(ref reader, paths, {at}{NextLevel(value.Element!)});");
        code.Line("addedCursor = reader.BitPosition;");
        code.Line("list.Add(item);");
        code.Line($"if (--addedLeft > 0) addedOrder = {global}{name}.__ReadKeyOrder(ref reader, paths, {at});");
    }

    // The bit helpers of the keyed-list deltas of one type.
    private static void KeyedStatics(GesSchemaPlanner.Code code)
    {
        code.Blank();
        code.Line("private static void __SetCount(ref GesBitWriter writer, long countBit, ulong count, int width)");
        code.Open("{");
        code.Line("for (var bit = 0; bit < width; bit++)");
        code.Line("    if (((count >> bit) & 1) != 0) writer.SetBit(countBit + bit);");
        code.Close();
        code.Blank();
        code.Line("// The next position of a section, read at its cursor, or the largest number when the section has ended.");
        code.Line("private static ulong __NextPosition(ref GesBitReader reader, ref long cursor, ref ulong left, int width, string path)");
        code.Open("{");
        code.Line("if (left == 0) return ulong.MaxValue;");
        code.Line("left--;");
        code.Line("reader.Seek(cursor);");
        code.Line("var position = reader.Read(width, path);");
        code.Line("cursor = reader.BitPosition;");
        code.Line("return position;");
        code.Close();
    }
}
