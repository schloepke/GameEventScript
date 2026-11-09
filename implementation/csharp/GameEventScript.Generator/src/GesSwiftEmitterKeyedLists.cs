// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;

namespace GameEventScript.Generator;

// Keyed lists in deltas of generated Swift types: the key order of their elements and the sections of their delta.
internal static partial class GesSwiftEmitter
{
    // The key of an element of a keyed list as an unsigned order: a signed integer without range has its sign bit flipped.
    private static void KeyOrder(GesSchemaPlanner.Code code, GesPlanType type)
    {
        var key = type.Fields[0];
        var flip = key.Value.Encoding.Kind == GesLayoutKind.Integer && !key.Value.Encoding.Range ? " ^ 0x8000_0000_0000_0000" : "";
        code.Blank();
        code.Open("func __keyOrder(_ paths: [String], _ at: Int) throws(GesWireError) -> UInt64 {");
        code.Line($"try Self.__wire0({Target(key.Base)}, paths, at){flip}");
        code.Close();
        code.Blank();
        code.Open("static func __readKeyOrder(_ reader: inout GesBitReader, _ paths: [String], _ at: Int) throws(GesWireError) -> UInt64 {");
        code.Line($"try Self.__read0(&reader, paths, at){flip}");
        code.Close();
    }

    // The delta of a keyed list, as in the C# output.
    private static void KeyedHelpers(GesSchemaPlanner.Code code, GesPlanValue value, string owner)
    {
        foreach (var alternative in value.Alternatives) KeyedHelpers(code, alternative, owner);
        if (!GesSchemaPlanner.Keyed(value)) return;
        var encoding = value.Encoding;
        var element = value.Element!;
        var node = N(value.Node);
        var name = ElementName(value);
        var at = $"at + {N(element.Node + 1)}";
        var width = encoding.Width.ToString(CultureInfo.InvariantCulture);
        var positionWidth = encoding.PositionWidth.ToString(CultureInfo.InvariantCulture);
        var max = encoding.MaxCount.ToString(CultureInfo.InvariantCulture);
        var target = Target(value.Base);
        var pool = $"self.__{GesSchemaPlanner.Camel(value.Base)}Pool";

        code.Blank();
        code.Open($"private static func __ascending{node}(_ list: [{name}], _ paths: [String], _ at: Int) throws(GesWireError) -> Bool {{");
        code.Line("var previous: UInt64 = 0");
        code.Open("for position in 0..<list.count {");
        code.Line($"let order = try list[position].__keyOrder(paths, {at})");
        code.Line("if position > 0 && order <= previous { return false }");
        code.Line("previous = order");
        code.Close();
        code.Line("return true");
        code.Close();

        code.Blank();
        code.Open($"private func __writeKeyed{node}(_ baseline: {owner}, _ writer: inout GesBitWriter, _ paths: [String], _ at: Int) throws(GesWireError) -> Bool {{");
        code.Line($"let list = {target}");
        code.Line($"let held = {Other("baseline", value.Base)}");
        code.Line($"if list.count > {max} || held.count > {max} {{ {Throw("tooLong", value)} }}");
        code.Line($"let listAscending = try Self.__ascending{node}(list, paths, at)");
        code.Line($"let heldAscending = try Self.__ascending{node}(held, paths, at)");
        code.Open("if !listAscending || !heldAscending {");
        code.Line($"if try __same{node}(baseline, paths, at) {{ return false }}");
        code.Line("try writer.write(1, width: 1)");
        Write(code, value, "list");
        code.Line("return true");
        code.Close();
        code.Line("let entry = writer.bitPosition");
        code.Line("try writer.write(0, width: 1)");
        code.Line("var countBit = writer.bitPosition");
        code.Line($"try writer.write(0, width: {width})");
        code.Line("var removed: UInt64 = 0");
        code.Line("var other = 0");
        code.Open("for position in 0..<held.count {");
        Match(code, "held", "list", at);
        code.Line("if kept { continue }");
        code.Line($"try writer.write(UInt64(position), width: {positionWidth})");
        code.Line("removed += 1");
        code.Close();
        code.Line($"Self.__setCount(&writer, countBit, removed, {width})");
        code.Line("countBit = writer.bitPosition");
        code.Line($"try writer.write(0, width: {width})");
        code.Line("var added: UInt64 = 0");
        code.Line("other = 0");
        code.Open("for position in 0..<list.count {");
        Match(code, "list", "held", at);
        code.Line("if kept { continue }");
        code.Line($"try list[position].__writeFields(&writer, paths, {at})");
        code.Line("added += 1");
        code.Close();
        code.Line($"Self.__setCount(&writer, countBit, added, {width})");
        code.Line("countBit = writer.bitPosition");
        code.Line($"try writer.write(0, width: {width})");
        code.Line("var changed: UInt64 = 0");
        code.Line("other = 0");
        code.Open("for position in 0..<held.count {");
        Match(code, "held", "list", at);
        code.Line("if !kept { continue }");
        code.Line("let start = writer.bitPosition");
        code.Line($"try writer.write(UInt64(position), width: {positionWidth})");
        code.Open($"if try list[other].__writeDelta(held[position], &writer, paths, {at}, true) {{");
        code.Line("changed += 1");
        code.Close("} else {");
        code.Line("    writer.rewind(to: start)");
        code.Line("}");
        code.Close();
        code.Line($"Self.__setCount(&writer, countBit, changed, {width})");
        code.Line("if removed > 0 || added > 0 || changed > 0 { return true }");
        code.Line("writer.rewind(to: entry)");
        code.Line("return false");
        code.Close();

        code.Blank();
        code.Open($"private func __readKeyed{node}(_ baseline: {owner}, _ reader: inout GesBitReader, _ paths: [String], _ at: Int) throws(GesWireError) {{");
        code.Line($"let held = {Other("baseline", value.Base)}");
        code.Open($"if try reader.read(1, path: {Path(value)}) == 1 {{");
        Read(code, value, target);
        code.Line($"let listAscending = try Self.__ascending{node}({target}, paths, at)");
        code.Line($"let heldAscending = try Self.__ascending{node}(held, paths, at)");
        code.Line($"if listAscending && heldAscending {{ {Throw("invalidDelta", value)} }}");
        code.Line($"if try __same{node}(baseline, paths, at) {{ {Throw("invalidDelta", value)} }}");
        code.Line("return");
        code.Close();
        code.Line($"if !(try Self.__ascending{node}(held, paths, at)) {{ {Throw("invalidDelta", value)} }}");
        code.Line("let baselineCount = UInt64(held.count)");
        Count(code, value, "removedLeft", width, max);
        code.Line("var removedCursor = reader.bitPosition");
        code.Open("do {");
        code.Line("var previous: UInt64 = 0");
        code.Open("for item in 0..<removedLeft {");
        code.Line($"let position = try reader.read({positionWidth}, path: {Path(value)})");
        code.Line($"if position >= baselineCount || (item > 0 && position <= previous) {{ {Throw("invalidDelta", value)} }}");
        code.Line("previous = position");
        code.Close();
        code.Close();
        Count(code, value, "addedLeft", width, max);
        code.Line("var addedCursor = reader.bitPosition");
        code.Open("do {");
        code.Line("var other = 0");
        code.Line("var previous: UInt64 = 0");
        code.Open("for item in 0..<addedLeft {");
        code.Line("let start = reader.bitPosition");
        code.Line($"let order = try {name}.__readKeyOrder(&reader, paths, {at})");
        code.Line($"if item > 0 && order <= previous {{ {Throw("invalidDelta", value)} }}");
        code.Line("previous = order");
        code.Open("while other < held.count {");
        code.Line($"let next = try held[other].__keyOrder(paths, {at})");
        code.Line($"if next == order {{ {Throw("invalidDelta", value)} }}");
        code.Line("if next > order { break }");
        code.Line("other += 1");
        code.Close();
        code.Line("reader.seek(to: start)");
        if (element.IsClass)
        {
            code.Line($"if {pool}.isEmpty {{ {pool}.append({name}(__owner: ObjectIdentifier(self))) }}");
            code.Line($"try {pool}[0].__readFields(&reader, paths, {at})");
        }
        else
        {
            code.Line($"var scratch = {name}()");
            code.Line($"try scratch.__readFields(&reader, paths, {at})");
        }

        code.Close();
        code.Close();
        Count(code, value, "changedLeft", width, max);
        code.Line("var changedCursor = reader.bitPosition");
        code.Line($"if removedLeft == 0 && addedLeft == 0 && changedLeft == 0 {{ {Throw("invalidDelta", value)} }}");
        code.Line($"if baselineCount - removedLeft + addedLeft > {max} {{ {Throw("invalidDelta", value)} }}");
        code.Line($"{target}.removeAll(keepingCapacity: true)");
        code.Line($"var nextRemoved = try Self.__nextPosition(&reader, &removedCursor, &removedLeft, {positionWidth}, {Path(value)})");
        code.Line($"var nextChanged = try Self.__nextPosition(&reader, &changedCursor, &changedLeft, {positionWidth}, {Path(value)})");
        code.Line("var addedOrder: UInt64 = 0");
        code.Open("if addedLeft > 0 {");
        code.Line("reader.seek(to: addedCursor)");
        code.Line($"addedOrder = try {name}.__readKeyOrder(&reader, paths, {at})");
        code.Close();
        code.Open("for position in 0..<baselineCount {");
        code.Line("let element = held[Int(position)]");
        code.Line($"let order = try element.__keyOrder(paths, {at})");
        code.Open("while addedLeft > 0 && addedOrder < order {");
        ReadAdded(code, value, name, at, target, pool);
        code.Close();
        code.Open("if position == nextRemoved {");
        code.Line($"if position == nextChanged {{ {Throw("invalidDelta", value)} }}");
        code.Line($"nextRemoved = try Self.__nextPosition(&reader, &removedCursor, &removedLeft, {positionWidth}, {Path(value)})");
        code.Close("} else if position == nextChanged {");
        code.Indent();
        Slot(code, element, name, target, pool);
        code.Line("reader.seek(to: changedCursor)");
        code.Line($"if !(try item.__readDelta(element, &reader, paths, {at}, true)) {{ {Throw("invalidDelta", element)} }}");
        code.Line("changedCursor = reader.bitPosition");
        code.Line($"{target}.append(item)");
        code.Line("let before = nextChanged");
        code.Line($"nextChanged = try Self.__nextPosition(&reader, &changedCursor, &changedLeft, {positionWidth}, {Path(value)})");
        code.Line($"if nextChanged <= before {{ {Throw("invalidDelta", value)} }}");
        code.Close("} else {");
        code.Indent();
        if (element.IsClass)
        {
            Slot(code, element, name, target, pool);
            code.Line("item.copy(from: element)");
            code.Line($"{target}.append(item)");
        }
        else code.Line($"{target}.append(element)");
        code.Close("}");
        code.Close();
        code.Line("// A changed position beyond the baseline was never reached.");
        code.Line($"if nextChanged != UInt64.max {{ {Throw("invalidDelta", value)} }}");
        code.Open("while addedLeft > 0 {");
        ReadAdded(code, value, name, at, target, pool);
        code.Close();
        code.Line("reader.seek(to: changedCursor)");
        code.Close();
    }

    private static void Match(GesSchemaPlanner.Code code, string list, string otherList, string at)
    {
        code.Line($"let order = try {list}[position].__keyOrder(paths, {at})");
        code.Line("var kept = false");
        code.Open($"while other < {otherList}.count {{");
        code.Line($"let next = try {otherList}[other].__keyOrder(paths, {at})");
        code.Open("if next >= order {");
        code.Line("kept = next == order");
        code.Line("break");
        code.Close();
        code.Line("other += 1");
        code.Close();
    }

    private static void Count(GesSchemaPlanner.Code code, GesPlanValue value, string name, string width, string max)
    {
        code.Line($"var {name} = try reader.read({width}, path: {Path(value)})");
        code.Line($"if {name} > {max} {{ {Throw("invalidCount", value)} }}");
    }

    private static void Slot(GesSchemaPlanner.Code code, GesPlanValue element, string name, string target, string pool)
    {
        if (!element.IsClass)
        {
            code.Line($"var item = {name}()");
            return;
        }

        code.Line($"if {target}.count == {pool}.count {{ {pool}.append({name}(__owner: ObjectIdentifier(self))) }}");
        code.Line($"let item = {pool}[{target}.count]");
    }

    private static void ReadAdded(GesSchemaPlanner.Code code, GesPlanValue value, string name, string at, string target, string pool)
    {
        Slot(code, value.Element!, name, target, pool);
        code.Line("reader.seek(to: addedCursor)");
        code.Line($"try item.__readFields(&reader, paths, {at})");
        code.Line("addedCursor = reader.bitPosition");
        code.Line($"{target}.append(item)");
        code.Line("addedLeft -= 1");
        code.Line($"if addedLeft > 0 {{ addedOrder = try {name}.__readKeyOrder(&reader, paths, {at}) }}");
    }

    private static void KeyedStatics(GesSchemaPlanner.Code code)
    {
        code.Blank();
        code.Open("private static func __setCount(_ writer: inout GesBitWriter, _ countBit: Int, _ count: UInt64, _ width: Int) {");
        code.Line("for bit in 0..<width where (count >> UInt64(bit)) & 1 != 0 { writer.setBit(countBit + bit) }");
        code.Close();
        code.Blank();
        code.Line("// The next position of a section, read at its cursor, or the largest number when the section has ended.");
        code.Open("private static func __nextPosition(_ reader: inout GesBitReader, _ cursor: inout Int, _ left: inout UInt64, _ width: Int, _ path: String) throws(GesWireError) -> UInt64 {");
        code.Line("if left == 0 { return .max }");
        code.Line("left -= 1");
        code.Line("reader.seek(to: cursor)");
        code.Line("let position = try reader.read(width, path: path)");
        code.Line("cursor = reader.bitPosition");
        code.Line("return position");
        code.Close();
    }
}
