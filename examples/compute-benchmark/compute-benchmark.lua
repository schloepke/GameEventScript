-- Copyright 2026 Stephan Schlöpke
-- SPDX-License-Identifier: Apache-2.0
-- Lua counterpart of compute-benchmark.ges. No external modules required.
-- Run once: lua compute-benchmark.lua
-- Use an external tool such as hyperfine for timing; GC uses Lua defaults.
-- Tables stand in for immutable GES records/maps. Parsing below handles only
-- this benchmark's report, not the complete GES literal language. There is no
-- GES host, opcode budget, or message dispatch overhead in this Lua version.

local rounds, items, expected = 48, 128, 2069276181

local function score(item, batch)
    local acc = item + batch
    for iteration = 1, 16 do
        acc = (acc * 33 + item * 17 + batch * 13 + iteration * 7) % 1000003
    end
    return acc
end

local function candidate(id, value)
    return {
        id = id,
        score = value,
        metric = math.sqrt(value + 1),
        faction = id % 2 == 0 and "red" or "blue",
    }
end

local function report_to_text(report)
    return string.format("[count: %d, highest: %d, lowest: %d, total: %d]",
        report.count, report.highest, report.lowest, report.total)
end

local function parse_report(text)
    local count, highest, lowest, total = text:match(
        "^%[count: (%d+), highest: (%d+), lowest: (%d+), total: (%d+)%]$")
    assert(count, "Invalid report literal")
    return {
        count = tonumber(count),
        highest = tonumber(highest),
        lowest = tonumber(lowest),
        total = tonumber(total),
    }
end

local function compute(batch, checksum)
    local candidates = {}
    for item = 1, items do
        candidates[item] = candidate(item, score(item, batch))
    end

    local selected = {}
    for _, value in ipairs(candidates) do
        if value.score % 3 ~= 0 then
            selected[#selected + 1] = value
        end
    end

    -- Preserve selected, just as GES order produces a separate collection.
    local ordered = {}
    for index, value in ipairs(selected) do
        ordered[index] = value
    end
    table.sort(ordered, function(left, right)
        -- Original input order breaks ties, matching a stable descending sort.
        if left.score == right.score then
            return left.id < right.id
        end
        return left.score > right.score
    end)

    local scores = {}
    for index, value in ipairs(ordered) do
        scores[index] = value.score
    end
    local total = scores[1]
    for index = 2, #scores do
        total = total + scores[index]
    end

    local factions = {}
    for _, value in ipairs(ordered) do
        local group = factions[value.faction]
        if not group then
            group = {}
            factions[value.faction] = group
        end
        group[#group + 1] = value
    end
    local grouped_count = #factions.red + #factions.blue
    local distance_sum = 0
    for _, value in ipairs(ordered) do
        distance_sum = distance_sum + value.metric
    end
    local report = {
        count = #scores,
        total = total,
        highest = scores[1],
        lowest = scores[#scores],
    }

    local restored = parse_report(report_to_text(report))
    assert(restored.count == report.count and restored.total == report.total
        and restored.highest == report.highest and restored.lowest == report.lowest
        and grouped_count == report.count and distance_sum > 0,
        "Benchmark validation failed in batch " .. batch)
    return checksum + restored.total + restored.count + restored.highest + restored.lowest
end

local function benchmark()
    local checksum = 0
    -- Serial batches correspond to the FIFO Compute messages in the GES version.
    for batch = 1, rounds do
        checksum = compute(batch, checksum)
    end
    assert(checksum == expected, "Benchmark checksum mismatch: " .. checksum)
end

benchmark()
print(string.format("Benchmark OK: rounds=%d, candidates=%d, checksum=%d",
    rounds, rounds * items, expected))
