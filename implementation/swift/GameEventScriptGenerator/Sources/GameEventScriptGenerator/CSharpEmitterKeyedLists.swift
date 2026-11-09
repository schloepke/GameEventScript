// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

// Keyed lists in deltas of generated C# types: the key order of their elements and the sections of their delta.
extension GesCSharpEmitter {
    // The key of an element of a keyed list as an unsigned order: a signed integer without range has its sign bit flipped.
    static func keyOrder(_ code: inout Code, _ type: GesPlanType, _ selfModifier: String) {
        let key = type.fields[0]
        let flip = key.value.encoding.kind == .integer && !key.value.encoding.range ? " ^ 0x8000000000000000UL" : ""
        code.blank()
        code.line("internal \(selfModifier)ulong __KeyOrder(string[] paths, int at) => __Wire0(\(key.base), paths, at)\(flip);")
        code.blank()
        code.line("internal static ulong __ReadKeyOrder(ref GesBitReader reader, string[] paths, int at) => __Read0(ref reader, paths, at)\(flip);")
    }

    // The delta of a keyed list: unchanged, whole when the keys of one side do not ascend strictly, and otherwise its removed,
    // added and changed elements, each section a merge of both lists by key. Counts are set once a section is written.
    static func keyedHelpers(_ code: inout Code, _ value: GesPlanValue, _ owner: String, _ global: String) {
        for alternative in value.alternatives { keyedHelpers(&code, alternative, owner, global) }
        if !GesSchemaPlanner.keyed(value) { return }
        let encoding = value.encoding
        let element = value.element!
        let node = String(value.node)
        let name = elementName(value)
        let at = "at + \(element.node + 1)"
        let width = String(encoding.width)
        let positionWidth = String(encoding.positionWidth)
        let max = String(encoding.maxCount)
        let pool = "__\(value.base)Pool"

        code.blank()
        code.line("private static bool __Ascending\(node)(List<\(name)> list, string[] paths, int at)")
        code.open("{")
        code.line("var previous = 0UL;")
        code.line("for (var position = 0; position < list.Count; position++)")
        code.open("{")
        code.line("var order = list[position].__KeyOrder(paths, \(at));")
        code.line("if (position > 0 && order <= previous) return false;")
        code.line("previous = order;")
        code.close()
        code.line("return true;")
        code.close()

        code.blank()
        code.line("private bool __WriteKeyed\(node)(\(owner) baseline, ref GesBitWriter writer, string[] paths, int at)")
        code.open("{")
        code.line("var list = \(value.base);")
        code.line("var held = baseline.\(value.base);")
        code.line("if (list.Count > \(max) || held.Count > \(max)) \(fail("TooLong", value))")
        code.line("var listAscending = __Ascending\(node)(list, paths, at);")
        code.line("var heldAscending = __Ascending\(node)(held, paths, at);")
        code.line("if (!listAscending || !heldAscending)")
        code.open("{")
        code.line("if (__Same\(node)(baseline, paths, at)) return false;")
        code.line("writer.Write(1, 1);")
        write(&code, value, "list")
        code.line("return true;")
        code.close()
        code.blank()
        code.line("var entry = writer.BitPosition;")
        code.line("writer.Write(0, 1);")
        code.line("var countBit = writer.BitPosition;")
        code.line("writer.Write(0, \(width));")
        code.line("var removed = 0UL;")
        code.line("var other = 0;")
        code.line("for (var position = 0; position < held.Count; position++)")
        code.open("{")
        match(&code, "held", "list", at)
        code.line("if (kept) continue;")
        code.line("writer.Write((ulong)position, \(positionWidth));")
        code.line("removed++;")
        code.close()
        code.line("__SetCount(ref writer, countBit, removed, \(width));")
        code.blank()
        code.line("countBit = writer.BitPosition;")
        code.line("writer.Write(0, \(width));")
        code.line("var added = 0UL;")
        code.line("other = 0;")
        code.line("for (var position = 0; position < list.Count; position++)")
        code.open("{")
        match(&code, "list", "held", at)
        code.line("if (kept) continue;")
        code.line("list[position].__WriteFields(ref writer, paths, \(at));")
        code.line("added++;")
        code.close()
        code.line("__SetCount(ref writer, countBit, added, \(width));")
        code.blank()
        code.line("countBit = writer.BitPosition;")
        code.line("writer.Write(0, \(width));")
        code.line("var changed = 0UL;")
        code.line("other = 0;")
        code.line("for (var position = 0; position < held.Count; position++)")
        code.open("{")
        match(&code, "held", "list", at)
        code.line("if (!kept) continue;")
        code.line("var start = writer.BitPosition;")
        code.line("writer.Write((ulong)position, \(positionWidth));")
        code.line("if (list[other].__WriteDelta(held[position], ref writer, paths, \(at), true)) changed++;")
        code.line("else writer.Rewind(start);")
        code.close()
        code.line("__SetCount(ref writer, countBit, changed, \(width));")
        code.line("if (removed > 0 || added > 0 || changed > 0) return true;")
        code.line("writer.Rewind(entry);")
        code.line("return false;")
        code.close()

        // Checks run in the wire order of the reference decoder: the baseline's order at the form bit, the positions as
        // they are read, an added key that the baseline holds at that element. A first pass validates the added elements;
        // the merge then reads them again where they belong.
        code.blank()
        code.line("private void __ReadKeyed\(node)(\(owner) baseline, ref GesBitReader reader, string[] paths, int at)")
        code.open("{")
        code.line("var held = baseline.\(value.base);")
        code.line("if (reader.Read(1, \(path(value))) == 1)")
        code.open("{")
        read(&code, value, value.base)
        code.line("var listAscending = __Ascending\(node)(\(value.base), paths, at);")
        code.line("var heldAscending = __Ascending\(node)(held, paths, at);")
        code.line("if (listAscending && heldAscending) \(fail("InvalidDelta", value))")
        code.line("if (__Same\(node)(baseline, paths, at)) \(fail("InvalidDelta", value))")
        code.line("return;")
        code.close()
        code.blank()
        code.line("if (!__Ascending\(node)(held, paths, at)) \(fail("InvalidDelta", value))")
        code.line("var baselineCount = (ulong)held.Count;")
        count(&code, value, "removedLeft", width, max)
        code.line("var removedCursor = reader.BitPosition;")
        code.line("for (ulong item = 0, previous = 0; item < removedLeft; item++)")
        code.open("{")
        code.line("var position = reader.Read(\(positionWidth), \(path(value)));")
        code.line("if (position >= baselineCount || (item > 0 && position <= previous)) \(fail("InvalidDelta", value))")
        code.line("previous = position;")
        code.close()
        count(&code, value, "addedLeft", width, max)
        code.line("var addedCursor = reader.BitPosition;")
        code.line("var other = 0;")
        code.line("for (ulong item = 0, previous = 0; item < addedLeft; item++)")
        code.open("{")
        code.line("var start = reader.BitPosition;")
        code.line("var order = \(global)\(name).__ReadKeyOrder(ref reader, paths, \(at));")
        code.line("if (item > 0 && order <= previous) \(fail("InvalidDelta", value))")
        code.line("previous = order;")
        code.line("for (; other < held.Count; other++)")
        code.open("{")
        code.line("var next = held[other].__KeyOrder(paths, \(at));")
        code.line("if (next == order) \(fail("InvalidDelta", value))")
        code.line("if (next > order) break;")
        code.close()
        code.line("reader.Seek(start);")
        if element.isClass {
            code.line("if (\(pool).Count == 0) \(pool).Add(new \(name)());")
            code.line("\(pool)[0].__ReadFields(ref reader, paths, \(at));")
        } else {
            code.line("var scratch = new \(name)();")
            code.line("scratch.__ReadFields(ref reader, paths, \(at));")
        }

        code.close()
        count(&code, value, "changedLeft", width, max)
        code.line("var changedCursor = reader.BitPosition;")
        code.line("if (removedLeft == 0 && addedLeft == 0 && changedLeft == 0) \(fail("InvalidDelta", value))")
        code.line("if (baselineCount - removedLeft + addedLeft > \(max)) \(fail("InvalidDelta", value))")
        code.blank()
        code.line("var list = \(value.base);")
        code.line("list.Clear();")
        code.line("var nextRemoved = __NextPosition(ref reader, ref removedCursor, ref removedLeft, \(positionWidth), \(path(value)));")
        code.line("var nextChanged = __NextPosition(ref reader, ref changedCursor, ref changedLeft, \(positionWidth), \(path(value)));")
        code.line("var addedOrder = 0UL;")
        code.line("if (addedLeft > 0)")
        code.open("{")
        code.line("reader.Seek(addedCursor);")
        code.line("addedOrder = \(global)\(name).__ReadKeyOrder(ref reader, paths, \(at));")
        code.close()
        code.line("for (var position = 0UL; position < baselineCount; position++)")
        code.open("{")
        code.line("var element = held[(int)position];")
        code.line("var order = element.__KeyOrder(paths, \(at));")
        code.line("while (addedLeft > 0 && addedOrder < order)")
        code.open("{")
        readAdded(&code, value, name, at, pool, global)
        code.close()
        code.line("if (position == nextRemoved)")
        code.open("{")
        code.line("if (position == nextChanged) \(fail("InvalidDelta", value))")
        code.line("nextRemoved = __NextPosition(ref reader, ref removedCursor, ref removedLeft, \(positionWidth), \(path(value)));")
        code.close()
        code.line("else if (position == nextChanged)")
        code.open("{")
        slot(&code, element, name, pool)
        code.line("reader.Seek(changedCursor);")
        code.line("if (!item.__ReadDelta(element, ref reader, paths, \(at), true)) \(fail("InvalidDelta", element))")
        code.line("changedCursor = reader.BitPosition;")
        code.line("list.Add(item);")
        code.line("var before = nextChanged;")
        code.line("nextChanged = __NextPosition(ref reader, ref changedCursor, ref changedLeft, \(positionWidth), \(path(value)));")
        code.line("if (nextChanged <= before) \(fail("InvalidDelta", value))")
        code.close()
        code.line("else")
        code.open("{")
        if element.isClass {
            slot(&code, element, name, pool)
            code.line("item.CopyFrom(element);")
            code.line("list.Add(item);")
        } else {
            code.line("list.Add(element);")
        }
        code.close()
        code.close()
        code.blank()
        code.line("// A changed position beyond the baseline was never reached.")
        code.line("if (nextChanged != ulong.MaxValue) \(fail("InvalidDelta", value))")
        code.line("while (addedLeft > 0)")
        code.open("{")
        readAdded(&code, value, name, at, pool, global)
        code.close()
        code.line("reader.Seek(changedCursor);")
        code.close()
    }

    // Moves `other` to the first element of the other list whose key is not smaller, and tells whether it has the same key.
    private static func match(_ code: inout Code, _ list: String, _ otherList: String, _ at: String) {
        code.line("var order = \(list)[position].__KeyOrder(paths, \(at));")
        code.line("var kept = false;")
        code.line("for (; other < \(otherList).Count; other++)")
        code.open("{")
        code.line("var next = \(otherList)[other].__KeyOrder(paths, \(at));")
        code.line("if (next < order) continue;")
        code.line("kept = next == order;")
        code.line("break;")
        code.close()
    }

    private static func count(_ code: inout Code, _ value: GesPlanValue, _ name: String, _ width: String, _ max: String) {
        code.line("var \(name) = reader.Read(\(width), \(path(value)));")
        code.line("if (\(name) > \(max)) \(fail("InvalidCount", value))")
    }

    // The storage for the next element of the list: a pooled instance, or a new value.
    private static func slot(_ code: inout Code, _ element: GesPlanValue, _ name: String, _ pool: String) {
        if !element.isClass {
            code.line("var item = new \(name)();")
            return
        }
        code.line("if (list.Count == \(pool).Count) \(pool).Add(new \(name)());")
        code.line("var item = \(pool)[list.Count];")
    }

    private static func readAdded(_ code: inout Code, _ value: GesPlanValue, _ name: String, _ at: String, _ pool: String, _ global: String) {
        slot(&code, value.element!, name, pool)
        code.line("reader.Seek(addedCursor);")
        code.line("item.__ReadFields(ref reader, paths, \(at));")
        code.line("addedCursor = reader.BitPosition;")
        code.line("list.Add(item);")
        code.line("if (--addedLeft > 0) addedOrder = \(global)\(name).__ReadKeyOrder(ref reader, paths, \(at));")
    }

    // The bit helpers of the keyed-list deltas of one type.
    static func keyedStatics(_ code: inout Code) {
        code.blank()
        code.line("private static void __SetCount(ref GesBitWriter writer, long countBit, ulong count, int width)")
        code.open("{")
        code.line("for (var bit = 0; bit < width; bit++)")
        code.line("    if (((count >> bit) & 1) != 0) writer.SetBit(countBit + bit);")
        code.close()
        code.blank()
        code.line("// The next position of a section, read at its cursor, or the largest number when the section has ended.")
        code.line("private static ulong __NextPosition(ref GesBitReader reader, ref long cursor, ref ulong left, int width, string path)")
        code.open("{")
        code.line("if (left == 0) return ulong.MaxValue;")
        code.line("left--;")
        code.line("reader.Seek(cursor);")
        code.line("var position = reader.Read(width, path);")
        code.line("cursor = reader.BitPosition;")
        code.line("return position;")
        code.close()
    }
}
