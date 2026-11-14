// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

/// Register lists shared by many instructions are summarized once per validation: their largest register for frame checks
/// and, for long lists, a membership set. Untrusted Programs may reference one long list from every instruction, so
/// scanning it per reference would make validation quadratic (specs/ProgramModel.md).
final class GesProgramListMembers {
    private static let setThreshold = 8
    private let lists: GesUInt16IndexListSegment
    private var maxima: [Int?]
    private var sets: [Set<UInt16>?]

    init(_ program: GameEventScriptProgram) {
        lists = program.uint16IndexLists
        maxima = Array(repeating: nil, count: lists.count)
        sets = Array(repeating: nil, count: lists.count)
    }

    /// The largest register in a list, or -1 for an empty list.
    func max(_ listIndex: Int) -> Int {
        if let known = maxima[listIndex] { return known }
        var value = -1
        for register in lists[listIndex] where Int(register) > value { value = Int(register) }
        maxima[listIndex] = value
        return value
    }

    func contains(_ listIndex: Int, _ register: UInt16) -> Bool {
        let list = lists[listIndex]
        if list.count <= Self.setThreshold { return list.contains(register) }
        if let set = sets[listIndex] { return set.contains(register) }
        var set = Set<UInt16>()
        for element in list { set.insert(element) }
        sets[listIndex] = set
        return set.contains(register)
    }
}
