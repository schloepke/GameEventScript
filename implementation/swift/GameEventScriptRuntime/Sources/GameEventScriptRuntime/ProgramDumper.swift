// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

/// Deterministic GESA presentation of a validated Program, including retained source/debug data.
/// Dumping neither executes code nor accesses files; it is outside the runtime hot path.
public enum GameEventScriptProgramDumper {
    /// Renders a validated Program as deterministic GESA text, optionally including instruction addresses. Performs no
    /// execution or file I/O.
    public static func dump(_ program: GameEventScriptProgram, includeInstructionAddresses: Bool = false) -> String { GesProgramDump(program).dump(addresses: includeInstructionAddresses) }
}

private final class GesProgramDump {
    let p: GameEventScriptProgram
    let separator = "// -------------------------------------------------------------------------------"
    var textLabels: [String] = [], bindLabels: [String] = [], listLabels: [String] = []
    var listRoles: [Int] = []
    var labels = Set<Int>(), codeNames: [Int: String] = [:], codeComments: [Int: String] = [:]
    var output = ""

    init(_ program: GameEventScriptProgram) {
        p = program
        var used = Set<String>()
        for (index, text) in p.stringConstants.enumerated() {
            let name = labelName(text)
            textLabels.append(unique("T_" + (name.isEmpty || name == "_" ? String(index) : name), used: &used))
        }
        used.removeAll()
        for (index, binding) in p.bindings.enumerated() {
            let name = labelName(text(binding.name))
            bindLabels.append(unique(prefix(binding.kind) + "_" + (name.isEmpty || name == "_" ? String(binding.id == .max ? index : Int(binding.id)) : name), used: &used))
            let address = Int(binding.entryAddress)
            if address < p.code.count {
                labels.insert(address)
                if binding.isExecutable && !name.isEmpty {
                    let codePrefix: String
                    switch binding.kind {
                    case .function: codePrefix = "function_"
                    case .predicate: codePrefix = "predicate_"
                    case .record: codePrefix = "record_"
                    default: codePrefix = ""
                    }
                    var candidate = codePrefix + name
                    var suffix = 1
                    while codeNames.values.contains(candidate) {
                        candidate = name + "_" + String(suffix)
                        suffix += 1
                    }
                    codeNames[address] = candidate
                    let kind = binding.isHandler ? "handler" : String(describing: binding.kind)
                    codeComments[address] = kind + " " + signature(binding, escaped: false, tags: false)
                }
            }
        }
        listLabels = p.uint16IndexLists.indices.map { "U16_" + String($0) }
        listRoles = .init(repeating: 0, count: listLabels.count)
        if !p.code.isEmpty { labels.insert(0) }
        for i in p.code {
            for part in i.operands {
                if part.isAddress, Int(i.address(part)) < p.code.count { labels.insert(Int(i.address(part))) }
                if part.isList {
                    let index = Int(i.list(part))
                    guard index < listLabels.count else { continue }
                    let name: String
                    switch part {
                    case .messageShapeList: name = "Shape"
                    case .argumentNameList: name = "Names"
                    case .keyNameList: name = "Keys"
                    case .argumentRegisterList: name = "Args"
                    case .itemRegisterList: name = "Items"
                    case .valueRegisterList: name = "Values"
                    case .captureRegisterList: name = "Captures"
                    default: name = "Tags"
                    }
                    listLabels[index] = name + "_" + String(index)
                    if listRoles[index] == 0 { listRoles[index] = part.isTextList ? 2 : 1 }
                }
            }
        }
        let named = codeNames.keys.sorted()
        used = Set(codeNames.values)
        for address in labels.sorted() where codeNames[address] == nil { if let owner = named.last(where: { $0 <= address }) { codeNames[address] = unique(codeNames[owner]! + "_" + String(address), used: &used, firstSuffix: 1) } }
    }

    func dump(addresses: Bool) -> String {
        output = separator + "\n//  Module: " + p.moduleName + "\n//  Type: Game Event Script Assembler\n//  Format version: " + String(p.formatVersion) + ".0\n" + separator + "\n\n"
        output += ".gesb " + String(p.formatVersion) + "\n.module " + quote(p.moduleName) + "\n.program-version " + String(p.programVersion) + "\n"
        for source in p.sourceArchive ?? [] {
            let name = "Source: " + source.sourceName
            region(name, segment: "source " + quote(source.sourceName))
            let value = normalizeLines(source.text)
            output += value
            if !value.hasSuffix("\n") { output += "\n" }
            endRegion(name)
        }
        region("Text", segment: "text")
        for index in p.stringConstants.indices { output += aligned(textLabels[index]) + ".text " + quote(p.stringConstants[index]) + "\n" }
        endRegion("Text")
        region("Lists", segment: "lists")
        for (index, values) in p.uint16IndexLists.enumerated() {
            let role = listRoles[index]
            let rendered = values.map { role == 2 ? textLabels[Int($0)] : (role == 1 ? "r" : "") + String($0) }
            output += aligned(listLabels[index]) + (role == 2 ? ".texts" : role == 1 ? ".registers" : ".u16") + " [" + rendered.joined(separator: ", ") + "]"
            if role == 2 && !values.isEmpty { output += " // " + values.map { quote(text($0)) }.joined(separator: ", ") }
            output += "\n"
        }
        endRegion("Lists")
        region("Bindings", segment: "bind")
        for (index, binding) in p.bindings.enumerated() {
            output += aligned(bindLabels[index]) + ".bind " + enumName(binding.kind) + " id=" + (binding.id == .max ? "none" : String(binding.id)) + " name=" + textLabels[Int(binding.name)] + " args=" + texts(binding.argumentNames)
            if binding.entryAddress != .max { output += " entry=" + code(binding.entryAddress) }
            if !binding.requiredTags.isEmpty { output += " requiredTags=" + texts(binding.requiredTags) }
            if !binding.excludedTags.isEmpty { output += " excludedTags=" + texts(binding.excludedTags) }
            output += " // \"" + signature(binding, escaped: true, tags: true) + "\"\n"
        }
        endRegion("Bindings")
        region("Code", segment: "code")
        var previousSource: UInt32?
        var previousLine = -1
        var generated = false
        for (address, i) in p.code.enumerated() {
            let named = codeComments[address] != nil
            let hasLabel = labels.contains(address)
            let inline = hasLabel && !named && !addresses
            if named && address > 0 { blank() }
            if let mapping = p.sourceMap?.entries.first(where: { address >= $0.codeStart && address < UInt64($0.codeStart) + UInt64($0.codeLength) }), let source = p.sourceMap?.sources.first(where: { $0.sourceID == mapping.sourceID }) {
                generated = false
                let line = (source.lineStartByteOffsets.lastIndex(where: { $0 <= mapping.sourceStartByteOffset }) ?? 0)
                if previousSource != mapping.sourceID || previousLine != line {
                    previousSource = mapping.sourceID
                    previousLine = line
                    blank()
                    var content = ""
                    if let archive = p.sourceArchive?.first(where: { $0.sourceID == mapping.sourceID }) {
                        let lines = normalizeLines(archive.text).split(separator: "\n", omittingEmptySubsequences: false)
                        if line < lines.count { content = String(lines[line]) }
                    }
                    output += ".source-line " + quote(source.sourceName) + " " + String(line + 1) + " | " + content + "\n"
                }
            } else if !generated {
                output += "// compiler-generated\n"
                generated = true
                previousSource = nil
                previousLine = -1
            }
            if hasLabel && !inline { output += aligned(code(UInt16(address))) + (codeComments[address].map { " // " + $0 } ?? "") + "\n" }
            if addresses {
                let number = String(address)
                output += "\t\t@" + String(repeating: "0", count: max(0, 4 - number.count)) + number + "\t\t"
            } else {
                output += inline ? aligned(code(UInt16(address))) : String(repeating: "\t", count: 5)
            }
            output += enumName(i.opcode)
            let parts = i.operands
            let operands = parts.enumerated().map { operand(i, $0.element, $0.offset, address) }.filter { !$0.isEmpty }
            if !operands.isEmpty { output += " " + operands.joined(separator: ", ") }
            var flags: [String] = []
            if i.normalizeResultAsPredicate { flags.append("NormalizeResultAsPredicate") }
            if i.unitAndFlags & 0x40 != 0 { flags.append("WithTags") }
            if i.unitAndFlags & 0x80 != 0 { flags.append("Indirect") }
            if !flags.isEmpty { output += " flags=" + flags.joined(separator: ", ") }
            let comments = parts.compactMap { comment(i, $0) }
            if !comments.isEmpty { output += " // " + comments.joined(separator: ", ") }
            output += "\n"
        }
        endRegion("Code")
        return output
    }

    func operand(_ i: GameEventScriptBytecodeInstruction, _ part: GesOperand, _ position: Int, _ address: Int) -> String {
        if part.isRegister {
            let register = i.register(part, position)
            let symbol = p.debugSymbols?.filter { $0.registerID == register && address >= $0.codeStart && address < UInt64($0.codeStart) + UInt64($0.codeLength) }.min { $0.codeLength < $1.codeLength }
            return "r" + String(register) + (symbol.map { "(" + $0.name + ")" } ?? "")
        }
        if part.isAddress { return code(i.address(part)) }
        if part.isList { return listLabels[Int(i.list(part))] }
        if part.isString { return textLabels[Int(part == .customTypeName ? i.word2 : i.word1)] }
        switch part {
        case .outboundMessage, .recordReference, .externalReference: return binding(i, part).map { bindLabels[$0] } ?? "Bind_none"
        case .localRegisterDelta, .diceCount, .componentCount, .fromImmediate: return "#" + String(i.signedWord1)
        case .diceSideCount, .countImmediate, .toImmediate: return "#" + String(i.signedWord2)
        case .indexImmediate: return "#" + String(i.word1)
        case .stepImmediate: return "#" + String(Int16(bitPattern: i.a))
        case .integerImmediate: return "#" + String(i.integer)
        case .floatImmediate: return floatImmediate(i.float)
        case .unit: return i.unit == .none ? "" : "unit:" + (i.unit == .meter ? "meter" : i.unit == .second ? "second" : "degree")
        case .typeKind: return GameEventScriptBytecodeTypeKind(rawValue: i.word2).map(enumName) ?? "Unknown"
        case .patternKind: return GameEventScriptBytecodePatternKind(rawValue: i.a).map(enumName) ?? "Unknown"
        case .seriesKind: return GameEventScriptBytecodeSeriesKind(rawValue: i.word2).map(enumName) ?? "Unknown"
        default: return ""
        }
    }

    private func floatImmediate(_ value: Double) -> String {
        let text = GesNumber.format(value)
        if value == 0 || !value.isFinite { return "#" + text }

        // Keep the runtime's shortest round-trip digits, with GESA's own notation threshold.
        let negative = value < 0
        let magnitude = negative ? String(text.dropFirst()) : text
        let parts = magnitude.split(separator: "e")
        let coefficient = parts[0].split(separator: ".", omittingEmptySubsequences: false)
        let digits = Array(coefficient.joined().utf8)
        var first = 0
        var last = digits.count - 1
        while digits[first] == 48 { first += 1 }
        while digits[last] == 48 { last -= 1 }
        let exponent = (parts.count == 2 ? Int(parts[1])! : 0) + coefficient[0].count - first - 1
        let significant = String(decoding: digits[first...last], as: UTF8.self)
        let formatted: String
        if (-4..<16).contains(exponent) {
            let point = exponent + 1
            if point <= 0 {
                formatted = "0." + String(repeating: "0", count: -point) + significant
            } else if point >= significant.count {
                formatted = significant + String(repeating: "0", count: point - significant.count)
            } else {
                formatted = significant.prefix(point) + "." + significant.dropFirst(point)
            }
        } else {
            let significand = significant.count == 1 ? significant : significant.prefix(1) + "." + significant.dropFirst()
            formatted = significand + "e" + String(exponent)
        }
        return (negative ? "#-" : "#") + formatted
    }

    func comment(_ i: GameEventScriptBytecodeInstruction, _ part: GesOperand) -> String? {
        if part.isString { return quote(text(part == .customTypeName ? i.word2 : i.word1)) }
        if part.isTextList {
            let values = p.uint16IndexLists[Int(i.list(part))]
            return values.isEmpty ? nil : values.map { quote(text($0)) }.joined(separator: ", ")
        }
        if [.outboundMessage, .recordReference, .externalReference].contains(part), let index = binding(i, part) { return "\"" + signature(p.bindings[index], escaped: true, tags: true) + "\"" }
        if part.isAddress && part != .jumpTarget { return codeComments[Int(i.address(part))] }
        return nil
    }

    func binding(_ i: GameEventScriptBytecodeInstruction, _ part: GesOperand) -> Int? {
        let kind: GameEventScriptBinaryBindKind = part == .outboundMessage ? .outboundMessage : part == .recordReference ? .record : i.opcode == .createExternalType ? .externalType : .extensionCall
        let id = part == .outboundMessage && !i.opcode.isResultSend ? i.word0 : i.word1
        return p.bindings.firstIndex { $0.kind == kind && $0.id == id }
    }

    func signature(_ binding: GameEventScriptBinding, escaped: Bool, tags: Bool) -> String {
        func resolve(_ id: UInt16) -> String { escaped ? escape(text(id)) : text(id) }

        var result = resolve(binding.name)
        if binding.kind == .messageNameHandler { result += " as message" } else { result += "(" + binding.argumentNames.map(resolve).joined(separator: ", ") + ")" }
        if tags {
            if !binding.requiredTags.isEmpty { result += " matching " + binding.requiredTags.map { "#" + resolve($0) }.joined(separator: ", ") }
            if !binding.excludedTags.isEmpty { result += " without " + binding.excludedTags.map { "#" + resolve($0) }.joined(separator: ", ") }
        }
        return result
    }

    func text(_ index: UInt16) -> String { p.stringConstants[Int(index)] }

    func texts(_ values: [UInt16]) -> String { "[" + values.map { textLabels[Int($0)] }.joined(separator: ", ") + "]" }

    func code(_ address: UInt16) -> String { address == .max ? "L_none" : codeNames[Int(address)] ?? "L_" + String(address) }

    func enumName<T>(_ value: T) -> String {
        let text = String(describing: value)
        return text.prefix(1).uppercased() + text.dropFirst()
    }

    func prefix(_ kind: GameEventScriptBinaryBindKind) -> String {
        switch kind {
        case .messageHandler, .messageNameHandler: "Handler"
        case .extensionCall: "Extension"
        case .outboundMessage: "Outbound"
        default: enumName(kind)
        }
    }

    func labelName(_ text: String) -> String {
        if text.unicodeScalars.allSatisfy({ $0.properties.isWhitespace }) { return "" }
        var output = ""
        for (index, scalar) in text.utf16.enumerated() {
            let letter = (65...90).contains(scalar) || (97...122).contains(scalar)
            let digit = (48...57).contains(scalar)
            if index == 0 && !letter && scalar != 95 {
                output += "_"
                if digit { output.append(Character(Unicode.Scalar(scalar)!)) }
            } else {
                output += letter || digit || scalar == 95 ? String(Unicode.Scalar(scalar)!) : "_"
            }
        }
        return output
    }

    func unique(_ name: String, used: inout Set<String>, firstSuffix: Int = 2) -> String {
        if used.insert(name).inserted { return name }
        var suffix = firstSuffix
        while !used.insert(name + "_" + String(suffix)).inserted { suffix += 1 }
        return name + "_" + String(suffix)
    }

    func escape(_ text: String) -> String { text.unicodeScalars.map { $0 == "\\" ? "\\\\" : $0 == "\"" ? "\\\"" : String($0) }.joined() }

    func quote(_ text: String) -> String { "\"" + escape(text) + "\"" }

    func aligned(_ label: String) -> String { label + ":" + String(repeating: "\t", count: max(1, (20 - label.utf16.count - 1 + 3) / 4)) }

    func blank() { if !output.isEmpty && !output.hasSuffix("\n\n") { output += "\n" } }

    func region(_ name: String, segment: String) {
        blank()
        output += separator + "\n.region " + quote(name) + "\n\n.segment " + segment + "\n\n"
    }

    func endRegion(_ name: String) {
        blank()
        output += ".region-end " + quote(name) + "\n" + separator + "\n"
    }

    func normalizeLines(_ text: String) -> String {
        var result = ""
        var carriage = false
        for scalar in text.unicodeScalars {
            if scalar == "\n" && carriage {
                carriage = false
                continue
            }
            carriage = scalar == "\r"
            result.unicodeScalars.append(carriage ? "\n" : scalar)
        }
        return result
    }
}
