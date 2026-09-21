#!/usr/bin/env ruby
# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0

# Read baseline references, never measured/received results or regression limits.
# Uses only Ruby's standard library; it does not run or update benchmarks.
require 'yaml'
require 'bigdecimal'
require 'pathname'
require 'digest'
require 'fileutils'

ROOT = Pathname.new(__dir__).parent.realpath
OUTPUT = ROOT.join('artifacts/conformance/PerformanceMatrix.md')
PROFILES = {
  'C#' => 'csharp-dotnet-release-macos-arm64',
  'Swift' => 'swift-6.4-release-macos26-arm64-m3max'
}.freeze

def yaml(text)
  YAML.safe_load(text)
end

def read_cases(path)
  text = path.read
  front = text.match(/\A---\n(.*?)\n---\n/m)
  raise "Missing frontmatter: #{path}" unless front
  defaults = yaml(front[1])
  cases = []
  title = nil
  blocks = []
  fence = nil
  payload = []
  finish = lambda do
    next unless title
    metadata = blocks.find { |block| block['gesBlock'] == 'case' }
    raise "Missing case metadata: #{path}: #{title}" unless metadata
    next unless metadata.fetch('kind', defaults['kind']) == 'performance'
    expected = blocks.find { |block| block['gesBlock'] == 'expect' }
    iterations = metadata.fetch('performance').fetch('iterations')
    raise "Invalid iterations: #{title}" unless iterations.is_a?(Integer) && iterations.positive?
    profiles = expected.fetch('performance').fetch('profiles')
    PROFILES.each_value { |profile| profiles.fetch(profile).fetch('metrics') }
    cases << { title: title, suite: defaults.fetch('suiteId'), id: metadata.fetch('id'),
               path: path, iterations: iterations, profiles: profiles }
  end
  text[front.end(0)..-1].each_line do |line|
    if fence
      if line.strip == '```'
        blocks << yaml(payload.join) if fence == 'yaml'
        fence = nil
      else
        payload << line
      end
    elsif line.start_with?('```')
      fence = line.strip.delete_prefix('```')
      payload = []
    elsif line.start_with?('## Test: ')
      finish.call
      title = line.delete_prefix('## Test: ').strip
      blocks = []
    end
  end
  raise "Unclosed fence: #{path}" if fence
  finish.call
  cases
end

def metric(test, language, id, quantity)
  entry = test[:profiles].fetch(PROFILES.fetch(language)).fetch('metrics')[id]
  return nil unless entry
  unit = entry.fetch('unit')
  factors = quantity == :time ? { 'ms' => 1 } : { 'B' => 1, 'KiB' => 1024 }
  factor = factors.fetch(unit) { raise "Unsupported #{quantity} unit: #{unit}" }
  value = BigDecimal(entry.fetch('reference').to_s) * factor
  raise "Invalid reference: #{test[:id]}/#{id}" unless value.finite? && value >= 0
  value
end

def display(value, digits)
  return '—' unless value
  return '0' if value.zero?
  format("%.#{digits}f", value)
end

def source_link(test)
  # Corpus test titles currently contain ASCII letters, digits, spaces and hyphens.
  anchor = 'test-' + test[:title].downcase.gsub(/[^a-z0-9 -]/, '').tr(' ', '-')
  relative = test[:path].relative_path_from(OUTPUT.dirname)
  label = test[:suite].delete_prefix('performance.') + ' · ' + test[:title]
  "[#{label.gsub('|', '\\|')}](#{relative}##{anchor})"
end

def table(cases, quantity)
  time = quantity == :time
  suffix = time ? 'elapsed' : 'allocated'
  unit = time ? 'ms' : 'KiB'
  run_unit = time ? 'µs/iteration' : 'B/iteration'
  headers = ['Case', 'Iterations']
  ['Compile¹', 'Load', 'Run'].each do |phase|
    PROFILES.each_key { |language| headers << "#{language} #{phase} (#{phase == 'Run' ? run_unit : unit})" }
  end
  lines = ['| ' + headers.join(' | ') + ' |', '| --- | ' + (['---:'] * (headers.length - 1)).join(' | ') + ' |']
  cases.each do |test|
    cells = [source_link(test), test[:iterations].to_s]
    ['compile', 'program-load', 'run'].each do |phase|
      PROFILES.each_key do |language|
        value = metric(test, language, "#{phase}.#{suffix}", quantity)
        if value
          value = if phase == 'run'
                    value * (time ? 1000 : 1) / test[:iterations]
                  else
                    value / (time ? 1 : 1024)
                  end
        end
        cells << display(value, time && phase != 'run' ? 4 : 3)
      end
    end
    lines << '| ' + cells.join(' | ') + ' |'
  end
  lines.join("\n")
end

raise 'Usage: ruby --disable-gems scripts/write-performance-matrix.rb' unless ARGV.empty?
paths = ROOT.join('conformance/suites/performance').glob('*.md').sort
cases = paths.flat_map { |path| read_cases(path) }
raise 'No performance cases found' if cases.empty?
identities = cases.map { |test| [test[:suite], test[:id]] }
raise 'Duplicate performance case IDs' unless identities.uniq.length == identities.length

text = <<~MARKDOWN
  # Performance baseline matrix

  #{cases.length} shared performance cases; references from the source Markdown, not
  a fresh benchmark run or the maximum allowed regression budgets. Case links open
  the owning workload. **— means no reference is recorded; 0 means a zero reference.**
  Bytecode snapshot cases are excluded.

  - **C#:** `#{PROFILES['C#']}`. The profile identifies Release/macOS/arm64;
    its ID does not pin a CPU or .NET version. The allocation-only suites also have
    matching `csharp-dotnet-release-managed` references.
  - **Swift:** `#{PROFILES['Swift']}`; Apple M3 Max, macOS 26.6.2,
    Swift 6.4, Release. See the [measurement guide](../../implementation/swift/Performance.md).

  These are implementation regression baselines with different sampling and
  instrumentation. Side-by-side values help locate costs; they do not establish
  a controlled language speed ranking. C# has no elapsed-time references for
  the low-allocation and text-conversion suites yet.

  ¹ `compile.*` is shown as defined by each profile: **C# includes AST build,
  binary build and Host.Load**; **Swift includes compilation and any requested
  binary roundtrip, but excludes Host creation/linking/initialization**, which
  belong to Swift's load measurement. The load columns also have different scopes.
  Compile and load columns must therefore not be summed indiscriminately.

  Run values are normalized from `run.elapsed` / `run.allocated` by the declared
  iteration count. One iteration executes all Steps. This avoids the extra rounding
  in older C# per-invocation references and makes the 1000/2000 pairs readable.
  Compile/load are per operation. Display rounding does not change any baseline.

  ## Times

  Compile/load: milliseconds. Run: microseconds per iteration. C# selects the
  fastest of three samples; Swift uses medians of five samples across three
  calibration processes. Swift timing includes allocation instrumentation overhead.

  #{table(cases, :time)}

  ## Allocations

  Compile/load: KiB (1024 bytes). Run: bytes per iteration. C# counts managed
  allocations on the calling thread; Swift counts requested libmalloc bytes on
  that thread. These are cumulative allocated bytes, including freed objects,
  not retained or peak memory. A zero here applies to the measured scope.

  #{table(cases, :allocation)}

  ## Refresh and source identity

  ```bash
  ruby --disable-gems scripts/write-performance-matrix.rb
  ```

  The command reads the current corpus and writes only this generated report under
  `artifacts`. It runs no benchmarks and changes no references. Source SHA-256:

MARKDOWN
paths.each do |path|
  relative = path.relative_path_from(OUTPUT.dirname)
  text << "- [#{path.basename}](#{relative}): `#{Digest::SHA256.file(path).hexdigest.upcase}`\n"
end
FileUtils.mkdir_p(OUTPUT.dirname)
OUTPUT.write(text)
puts "Wrote #{cases.length} cases to #{OUTPUT.relative_path_from(ROOT)}"
