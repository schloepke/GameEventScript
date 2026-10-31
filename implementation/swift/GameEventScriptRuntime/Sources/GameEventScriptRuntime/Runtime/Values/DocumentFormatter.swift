// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

@_spi(Syntax) import GameEventScriptSyntax

// The recommended document layout of specs/Documents.md. Only its round trip through GesLiteralParser is normative.
enum GesDocumentFormatter {
    static func print(_ document: GesDocument, _ format: GesDocumentFormat) -> String {
        var output = ""
        if let schema = document.schema {
            output += ":Schema(" + (schema.kind == .type ? ":" + schema.name : GesText.quoted(schema.name)) + ")\n"
        }
        let entries = document.entries
        for index in 0..<entries.length {
            let key = keyText(entries.key(at: index)) + ": "
            output += key
            write(&output, entries.value(at: index), level: 0, column: key.unicodeScalars.count, format, commas: false)
            output += "\n"
        }
        return output
    }

    static func format(_ value: GesValue, _ format: GesDocumentFormat) throws -> String {
        guard isPrintable(value) else { throw GesApiError.invalidArgument("The value has no readable literal text.") }
        var output = ""
        write(&output, value, level: 0, column: 0, format, commas: true)
        return output
    }

    // Values whose literal text reads back: no External values, positive Dice rolls and Record type names that
    // follow the TypeName grammar, at every level.
    static func isPrintable(_ value: GesValue) -> Bool {
        switch value.kind {
        case .list: return value.listValue!.allSatisfy(isPrintable)
        case .map: return value.asMap!.values.allSatisfy(isPrintable)
        case .record: return GesNames.type(value.customTypeName!) && value.asMap!.values.allSatisfy(isPrintable)
        case .dice: return value.diceRolls!.allSatisfy { $0 > 0 }
        case .message:
            let message = value.messageValue!
            return (0..<message.arguments.count).allSatisfy { isPrintable(message.arguments[$0]) }
        case .external: return false
        default: return true
        }
    }

    // Writes a value inline when it fits from the current column, otherwise a non-empty container in block form.
    // Documents separate block entries by line breaks; single values add commas so that `parse` reads them back.
    private static func write(_ output: inout String, _ value: GesValue, level: Int, column: Int, _ format: GesDocumentFormat, commas: Bool) {
        guard isBlockContainer(value) else {
            _ = inline(&output, value, format, stop: .max)
            return
        }
        // Measuring stops once the inline text cannot fit, so each nesting level renders at most about one line.
        let remaining = max(format.lineWidth, 1) - column
        var text = ""
        if remaining >= 0, inline(&text, value, format, stop: remaining) {
            output += text
            return
        }
        switch value.kind {
        case .list:
            let items = value.listValue!
            output += "[\n"
            for (index, item) in items.enumerated() {
                output += indent(level + 1, format)
                write(&output, item, level: level + 1, column: (level + 1) * spaces(format), format, commas: commas)
                output += commas && index < items.count - 1 ? ",\n" : "\n"
            }
            output += indent(level, format) + "]"
        case .map:
            writeMapBlock(&output, value.asMap!, level: level, format, commas: commas)
        default:
            output += ":Record(" + GesText.quoted(value.customTypeName!) + ", "
            writeMapBlock(&output, value.asMap!, level: level, format, commas: commas)
            output += ")"
        }
    }

    private static func writeMapBlock(_ output: inout String, _ map: GesValueMap, level: Int, _ format: GesDocumentFormat, commas: Bool) {
        output += "[\n"
        for index in 0..<map.length {
            let key = keyText(map.key(at: index)) + ": "
            output += indent(level + 1, format) + key
            write(&output, map.value(at: index), level: level + 1, column: (level + 1) * spaces(format) + key.unicodeScalars.count, format, commas: commas)
            output += commas && index < map.length - 1 ? ",\n" : "\n"
        }
        output += indent(level, format) + "]"
    }

    private static func isBlockContainer(_ value: GesValue) -> Bool {
        switch value.kind {
        case .list: !value.listValue!.isEmpty
        case .map, .record: value.asMap!.length > 0
        default: false
        }
    }

    // Appends the single-line form. With a finite stop, measuring ends early and returns false once the text exceeds
    // stop Unicode scalars or contains a line break, because it then cannot be printed inline.
    private static func inline(_ output: inout String, _ value: GesValue, _ format: GesDocumentFormat, stop: Int) -> Bool {
        let bounded = stop != .max
        switch value.kind {
        case .text:
            if bounded && value.textValue!.unicodeScalars.contains("\n") { return false }
            output += GesText.quoted(value.textValue!)
        case .integer where value.unit != .none, .float where value.unit != .none: output += format.prefixedUnits ? quantity(value) : value.toText
        case .list:
            output += "["
            for (index, item) in value.listValue!.enumerated() {
                if index > 0 { output += ", " }
                if !inline(&output, item, format, stop: stop) { return false }
            }
            output += "]"
        case .map:
            if !inlineMap(&output, value.asMap!, format, stop: stop) { return false }
        case .record:
            output += ":Record(" + GesText.quoted(value.customTypeName!) + ", "
            if !inlineMap(&output, value.asMap!, format, stop: stop) { return false }
            output += ")"
        case .message:
            let message = value.messageValue!
            output += ":Message(" + message.name + "("
            for index in 0..<message.arguments.count {
                if index > 0 { output += ", " }
                let label = message.arguments.nameAt(index)
                if label != "_" { output += label + ": " }
                if !inline(&output, message.arguments[index], format, stop: stop) { return false }
            }
            output += ")" + (message.tags.isEmpty ? "" : " with " + message.tags.map { "#" + $0 }.joined(separator: ", ")) + ")"
        default: output += value.toText
        }
        return !bounded || output.unicodeScalars.count <= stop
    }

    private static func inlineMap(_ output: inout String, _ map: GesValueMap, _ format: GesDocumentFormat, stop: Int) -> Bool {
        if map.length == 0 {
            output += "[:]"
            return stop == .max || output.unicodeScalars.count <= stop
        }
        output += "["
        for index in 0..<map.length {
            if index > 0 { output += ", " }
            output += keyText(map.key(at: index)) + ": "
            if !inline(&output, map.value(at: index), format, stop: stop) { return false }
        }
        output += "]"
        return stop == .max || output.unicodeScalars.count <= stop
    }

    private static func keyText(_ key: String) -> String { GesText.isLowerName(key) ? key : GesText.quoted(key) }

    // Writes a scalar quantity with the exact prefixed spelling whose magnitude is closest to [1, 1000).
    private static func quantity(_ value: GesValue) -> String {
        let base = value.toText
        let number = value.kind == .integer ? String(value.integerValue!) : GesNumber.format(value.floatValue!)
        guard let (negative, digits, exponent) = decompose(number) else { return base }
        let lead = digits.count - 1 + exponent
        var best: (text: String, exponent: Int)?
        var distance = Self.distance(lead)
        for suffix in GesQuantityUnit.suffixes where suffix.unit == value.unit && suffix.factor == 1 && suffix.exponent != 0 {
            let candidate = Self.distance(lead - suffix.exponent)
            if candidate < distance {
                distance = candidate
                best = (suffix.text, suffix.exponent)
            }
        }
        guard let best else { return base }
        let text = decimal(negative, digits, exponent - best.exponent) + best.text
        guard let read = GesTextNumberCast.read(text, allowGrouping: false), read.kind == value.kind, read.unit == value.unit,
            value.kind == .integer ? read.integerValue == value.integerValue : read.floatValue!.bitPattern == value.floatValue!.bitPattern
        else { return base }
        return text
    }

    // Distance of a decimal leading-digit exponent from the magnitude interval [1, 1000).
    private static func distance(_ lead: Int) -> Int { lead < 0 ? -lead : lead > 2 ? lead - 2 : 0 }

    // Splits finite numeric text into sign, significant digits without leading or trailing zeros, and the exponent
    // of the last digit. Zero and infinities have no digits.
    private static func decompose(_ text: String) -> (Bool, [Character], Int)? {
        var body = Substring(text)
        let negative = body.hasPrefix("-")
        if negative { body = body.dropFirst() }
        var exponent = 0
        if let mark = body.firstIndex(where: { $0 == "e" || $0 == "E" }) {
            guard let value = Int(body[body.index(after: mark)...]) else { return nil }
            exponent = value
            body = body[..<mark]
        }
        var characters = Array(body)
        if let point = characters.firstIndex(of: ".") {
            exponent -= characters.count - point - 1
            characters.remove(at: point)
        }
        let start = characters.firstIndex(where: { $0 != "0" }) ?? characters.count
        var digits = Array(characters[start...])
        while digits.last == "0" {
            digits.removeLast()
            exponent += 1
        }
        guard !digits.isEmpty, digits.allSatisfy({ $0.isASCII && $0.isNumber }) else { return nil }
        return (negative, digits, exponent)
    }

    private static func decimal(_ negative: Bool, _ digits: [Character], _ exponent: Int) -> String {
        var output = negative ? "-" : ""
        let lead = digits.count - 1 + exponent
        if lead < -6 || lead >= 21 {
            output.append(digits[0])
            if digits.count > 1 { output += "." + String(digits[1...]) }
            return output + "e" + String(lead)
        }
        if exponent >= 0 { return output + String(digits) + String(repeating: "0", count: exponent) }
        let point = digits.count + exponent
        if point > 0 { return output + String(digits[..<point]) + "." + String(digits[point...]) }
        return output + "0." + String(repeating: "0", count: -point) + String(digits)
    }

    private static func indent(_ level: Int, _ format: GesDocumentFormat) -> String { String(repeating: " ", count: level * spaces(format)) }

    // The indentation within 0...GesDocumentFormat.maximumIndentation, so nesting cannot overflow a column.
    private static func spaces(_ format: GesDocumentFormat) -> Int { min(max(format.indentation, 0), GesDocumentFormat.maximumIndentation) }
}
