## Ergebnis

Deine drei Punkte sind richtig, reichen aber noch nicht ganz. Vor allem fehlen sprachneutrale Verträge, ohne die Swift, Kotlin, C++ und C# trotz identischer JSON-Tests unterschiedlich reagieren könnten.

Wichtig: Der physische Umzug ins Monorepo muss nicht warten, bis alle Punkte fertig sind. Sinnvoll ist, zuerst Struktur und gemeinsame Verträge anzulegen und die weitere Portabilitätsarbeit anschließend direkt im Monorepo durchzuführen.

Ich habe nur analysiert und nichts geändert.

## 1. `.gesb`-Binary vollständig definieren - DONE

Aktuell ist `GameEventScriptProgram` eine gute In-Memory-Vorbereitung, aber noch kein vollständig spezifiziertes Dateiformat. Siehe [GameEventScriptProgram.cs](/Users/stephan/Projects/BattleClub/BeamableServices/services/StepH-GameEventScript/Api/GameEventScriptProgram.cs) und [BytecodeSpec.md](/Users/stephan/Projects/BattleClub/BeamableServices/services/StepH-GameEventScript/BytecodeSpec.md).

Benötigt werden:

- `GameEventScriptProgramWriter`
- `GameEventScriptProgramReader`
- vollständiger `GameEventScriptProgramValidator`
- explizite Endianness
- exaktes Layout aller Sections
- Offsets, Counts und Alignment
- UTF-8-Encoding und ungültige UTF-8-Behandlung
- Instruktionsencoding unabhängig vom CLR-`StructLayout`
- IEEE-754-Encoding für Floats
- maximale Datei-, Tabellen-, String- und Codegrößen
- Regeln für unbekannte Versionen, Flags, Opcodes und Sections
- deterministische/canonical Serialisierung
- `FileSize` korrekt setzen und validieren
- Entscheidung über Checksumme oder Content-Hash
- Debug-Segment entweder implementieren oder das momentan nur gesetzte Debug-Flag vorerst entfernen

Der Validator muss mindestens prüfen:

- Magic, Version, Header und Dateigröße
- alle Tabellen-, Slice- und Stringgrenzen
- Bind-Arten und IDs
- Entry-, Jump- und Call-Adressen
- Opcode und Operandenkombinationen
- Call-Graph weiterhin azyklisch
- `RequiredRegisterCount` und `RequiredCallStackDepth`
- External-, Extension-, Record- und Outbound-Referenzen
- keine gewöhnlichen `IndexOutOfRangeException` aus beschädigten Dateien

Dazu gehören Binary-Conformance-Fixtures:

- Source → erwartete `.gesb`-Bytes
- Program → Write → Read → gleiches Program
- Read → Write → byte-identische kanonische Datei
- abgeschnittene und manipulierte Dateien
- ungültige Tabellenreferenzen und Sprünge
- unbekannte Versionen und Flags
- dieselbe `.gesb`-Datei wird von allen Runtime-Implementierungen ausgeführt

Vorher muss entschieden werden, ob verschiedene Compiler byte-identische Programme erzeugen müssen. Meine Empfehlung:

- Reader/Writer müssen kanonisch und byte-identisch sein.
- Runtime-Ergebnisse müssen identisch sein.
- Compiler müssen nicht zwingend dieselbe Registerbelegung erzeugen, solange Semantik und Ressourcenlimits stimmen.

Sonst würden spätere Register- und Liveness-Optimierungen unnötig eingeschränkt.

## 2. Portable Sprache und Runtime-Semantik festschreiben

Mehrere aktuelle Verhaltensweisen hängen implizit an .NET.

### Text und Unicode

Lexer und Validator verwenden unter anderem `char.IsLetter`, `char.IsLower` und `char.IsWhiteSpace`. Swift verwendet bei Strings standardmäßig Grapheme, Kotlin UTF-16 und C++ häufig UTF-8-Bytes.

Festzulegen sind:

- Source-Encoding, wahrscheinlich UTF-8
- erlaubte Zeichen in Namen und Tags
- entweder ASCII-Namen oder eine genau definierte Unicode-Version
- erlaubte Whitespace- und Newline-Zeichen
- Einheit für Textlänge und Textindex:
    - UTF-8-Byte
    - Unicode Scalar
    - UTF-16 Code Unit
    - Grapheme Cluster
- Stringvergleich und Sortierreihenfolge
- Normalisierung, etwa keine Normalisierung oder NFC
- Einheit für Compiler-Zeile und Spalte

Ohne diese Regeln entstehen garantiert Unterschiede zwischen Swift und C#.

### Zahlen

Festzulegen sind:

- Integer-Overflow
- Float-zu-Integer-Konvertierung
- `NaN`, Infinity und negative Null
- Rundungsregeln
- Division und Modulo mit negativen Zahlen
- Verhalten transzendenter Funktionen
- Vergleichstoleranz beziehungsweise ULP-Toleranz in Conformance-Tests
- kanonische JSON-Floatdarstellung

Die jetzige C#-Formatierung mit einem festen `ToString`-Format ist kein ausreichender sprachneutraler Vertrag.

### Determinismus

Zusätzliche Verträge:

- bekannte Testvektoren für den Random Generator
- Sortierstabilität
- Map-/Record-Reihenfolge
- Gleichheit unterschiedlicher Value-Arten
- Iterator- und Range-Grenzfälle
- Reihenfolge gleich priorisierter Handler

Der PRNG braucht bekannte Ausgabesequenzen und nicht nur den Nachweis, dass zwei C#-Instanzen mit demselben Seed gleich laufen.

## 3. C#-Kopplungen im portablen API/Core auflösen

Nicht jedes C#-Sprachkonstrukt muss entfernt werden. `readonly struct`, `ref`, `init`, `IEnumerable` oder `AggressiveInlining` dürfen interne C#-Implementierungsdetails bleiben. Entscheidend ist, dass sie keinen sprachübergreifenden Vertrag definieren.

Tatsächliche Kopplungen sind dagegen:

### External Types

[GameEventScriptExternalTypes.cs](/Users/stephan/Projects/BattleClub/BeamableServices/services/StepH-GameEventScript/Api/GameEventScriptExternalTypes.cs) enthält im portablen API `object` und `Func<object, GesValue>`. Compilerbeschreibung und ausführbare CLR-Bindings sind vermischt.

Benötigte Trennung:

- portable External-Type-Beschreibung:
    - Name
    - Felder
    - Konstruktoren
    - Argumentsignaturen
- hostgebundene Runtime-Implementierung
- C#-`object`, Reflection, Attributes und Converter ausschließlich im `CSharpBridge`
- portable manuelle Registry für Conformance-Tests

Der Compiler sollte nur die deklarative Typbeschreibung benötigen, nicht die CLR-Ausführung.

### Native Handler und Lebenszyklus

Der Host verwendet direkt `Action<...>`, während Instance und Subscription intern `Func<bool>`-Closures halten.

Besser portierbar wären:

- portable Handler-Schnittstelle beziehungsweise Handler-Referenz
- Registrierungs-ID statt gespeicherter Detach-Closure
- `Instance.Detach()` delegiert über Host plus ID
- komfortable `Action`-Overloads nur als C#-Adapter

Das ist kein unmittelbarer Semantikfehler, erleichtert aber C++ und reduziert versteckte Closure-Allokationen.

### Geordnete Message-Argumente

Das ist ein konkreter Blocker: `GameEventScriptMessageArguments.Create(IReadOnlyDictionary...)` übernimmt die Dictionary-Iteration als Signaturreihenfolge.

Auch die JSON-Conformance verwendet aktuell JSON-Objekte und deren Property-Reihenfolge. Das ist nicht als portabler Vertrag geeignet.

Benötigt:

```
"args": [
  { "name": "target", "value": ... },
  { "name": "unit", "value": ... }
]
```

Dictionary-/Map-Convenience kann im jeweiligen Sprachadapter bleiben, darf aber keine Signaturreihenfolge festlegen.

### Fehlervertrag

Compiler-, Decoder-, Linker- und Runtime-Fehler benötigen stabile Codes:

- Fehlerphase
- Fehlercode
- Symbolart
- Source Range
- optional technische Details

Die Conformance sollte Fehlercodes prüfen, nicht englische `messageContains`-Texte.

## 4. `GameEventScriptProgram` als wirklich portables Datenmodell härten

Die Regel in `AGENTS.md` ist richtig. Zusätzlich sollte sichergestellt werden:

- keine Delegates
- keine Registries
- keine CLR-Objekte
- keine Runtime-Caches
- keine VM-Referenzen
- keine AST-Knoten
- keine platformabhängigen Hashwerte
- keine Abhängigkeit von C#-Struct-Memory-Layout
- alle Listen und Tabellen immutable
- alle enumähnlichen Werte numerisch spezifiziert
- jedes Program kann vollständig nach `.gesb` geschrieben werden

Der öffentliche Konstruktor erlaubt momentan prinzipiell beliebige ungültige Programs. Entweder:

- Konstruktion nur intern beziehungsweise über Reader/Compiler, oder
- jeder `Host.Load` führt zwingend den vollständigen Validator aus.

Für untrusted `.gesb` sollte Letzteres ohnehin gelten.

## 5. JSON-Conformance ausbauen

Es gibt derzeit 34 JSON-Spec-Dateien und zusätzlich 65 eigenständige MSTest-Methoden. Die JSON-Infrastruktur ist bereits eine gute Basis, ist aber momentan selbst noch C#-zentriert.

Benötigt werden:

- formales JSON Schema
- Schema-Versionierung
- stabile eindeutige Case-IDs
- Kategorien beziehungsweise Capability-Tags
- klare Required-/Optional-Felder pro Testart
- sprachneutraler Runner-Ein-/Ausgabevertrag
- maschinenlesbare Ergebnisdatei
- klare Skip-Regeln
- portable Testregistries für Extensions und External Types
- Observer-Trace-Format
- Publish-Result-Format
- Float-Toleranzen
- Binary-Fixture-Unterstützung
- Trennung zwischen Conformance-JSON und zukünftigem Produkt-/Wire-JSON

### In JSON zu verschieben

- Message-Normalisierung und Signaturen
- geordnete Argumente
- Value-Semantik und Value-Kinds
- Lists, Maps, Records, Dice, Range, Vector und Point
- Random-Known-Answer-Tests
- Compiler-Metadaten
- `.gesb` Read/Write/Validation
- Direct- und Indirect-Call-Cycles
- Native-only Host
- mehrere Hosts mit demselben Program
- mehrere Programs in einem Host
- VM-Reset zwischen Handlern
- Receive/Emit/Publish
- Publish ohne Sink, mit Annahme, Ablehnung und Exception
- Observer-Ereignisse
- Load/Detach/Subscribe/Unsubscribe während Dispatch
- Snapshot-Semantik
- Initialization-Reihenfolge
- Queue-Limits
- Runtime-Limits pro Handler
- Pause/Resume mit Frame-Budget
- External-Type-Semantik ohne Reflection

### Sprachspezifisch zu behalten

- C# Public-API-Snapshot
- Reflection- und Attribute-Tests
- CLR-Konvertierungen
- C# Auto-Runner und Threading
- interne Builder-/Rewriter-Tests
- konkrete Struct-Layouts
- C#-Allokationsmessungen
- sprachspezifische Benchmarks

Die Semantik dieser Tests sollte trotzdem möglichst durch JSON abgedeckt werden; der C#-Test prüft danach nur noch die Implementierungsbesonderheit.

## 6. Öffentliche sprachneutrale API spezifizieren

Der C# API-Snapshot reicht nicht als Vorlage für andere Sprachen.

Benötigt wird ein normatives API-Dokument für:

- Program
- Host
- Context
- Instance
- Subscription
- Message
- Value
- RuntimeLimits
- ExecutionResult
- PublishResult
- PublishSink
- Observer
- Extension Registry
- External Type Catalog und Runtime Bindings

Dabei ausdrücklich festhalten:

- Ownership und Lebensdauer
- Copy- versus Reference-Semantik
- Immutability
- Nullability
- Threading und Serialisierung
- Reentrancy
- Idempotenz
- Callback-Reihenfolge
- Fehlerbehandlung
- Verhalten bei Observer- oder Sink-Exceptions

Danach bekommt jede Sprache eine idiomatische Abbildung. Die APIs müssen konzeptionell gleich sein, nicht Zeichen für Zeichen.

## 7. Opcode- und Formatdefinition zentralisieren

[BytecodeOpcodeShape.md](/Users/stephan/Projects/BattleClub/BeamableServices/services/StepH-GameEventScript/BytecodeOpcodeShape.md) und die C#-Enums duplizieren momentan Informationen manuell.

Für das Monorepo sollte es eine zentrale maschinenlesbare Definition geben, etwa YAML oder JSON:

- Opcode-ID
- Name
- Operanden
- Flags
- Einheiten
- gültige Kombinationen
- Bind-Kinds
- Value-Kinds
- Binary-Versionen

Daraus können generiert werden:

- C#-, Swift-, Kotlin- und C++-Enums
- Validator-Tabellen
- Opcode-Dokumentation
- Dumper-Metadaten

Das verhindert unbemerkte numerische Abweichungen.

## 8. Projekt- und Packaging-Abhängigkeiten bereinigen

Die portable C#-Library hängt momentan über das Projekt an Beamable und damit indirekt an Unity-Paketen, obwohl im Produktionscode keine Beamable-Verwendung gefunden wurde. Siehe [StepH-GameEventScript.csproj](/Users/stephan/Projects/BattleClub/BeamableServices/services/StepH-GameEventScript/StepH-GameEventScript.csproj).

Vor beziehungsweise während des Imports:

- `Beamable.Common` aus dem portablen Projekt entfernen
- Beamable-Integration als eigenes Adapter-/Packaging-Projekt
- prüfen, ob `System.Text.Json` im Core überhaupt gebraucht wird; derzeit offenbar nicht
- Core ohne Unity-Abhängigkeiten bauen
- CSharpBridge als klar getrenntes Package/Assembly
- Unity-Package definieren, das die C#-DLL konsumiert
- IL2CPP-, AOT- und Trimming-Verhalten prüfen
- Reflection-Registrierung für Unity gegebenenfalls durch manuelle oder generierte Registries ergänzen

## 9. Monorepo-Struktur

Empfohlene logische Struktur:

```
/spec
  language
  bytecode
  host
  api
  schemas

/conformance
  specs
  binaries
  malformed-binaries
  runner-contract

/implementations
  csharp
  swift
  kotlin
  cpp

/integrations
  unity
  beamable

/benchmarks
  workloads
  baselines

/tools
  opcode-generation
  binary-inspection
  conformance-runner
```

Wichtig:

- Specs und Fixtures existieren genau einmal.
- Keine Sprache bekommt eine eigene Kopie der JSON-Dateien.
- Alle Implementierungen referenzieren dieselbe Bytecode-Version.
- Buildartefakte und generierte Dateien werden klar getrennt.
- Import möglichst mit erhaltener Git-Historie.
- Lizenz, Package-Namen, Versionierung und Releaseprozess festlegen.

Die konkrete Einpassung in das bereits existierende Monorepo konnte ich nicht prüfen, weil es in diesem Workspace nicht vorliegt.

## 10. CI-Matrix

Das Monorepo braucht gemeinsame Gates:

- alle Sprachimplementierungen bauen
- JSON Schema validieren
- alle Conformance-Runner ausführen
- kanonische `.gesb`-Fixtures lesen
- Writer-Ergebnisse byteweise vergleichen
- Generated Opcode-Dateien auf Drift prüfen
- Compilerfehler und Source Ranges vergleichen
- Sanitizer für C++
- Swift/Kotlin/C# Unit Tests
- Unity Compile-/PlayMode-Smoke-Test
- Release-Artefakte reproduzierbar bauen

Zusätzlich empfehlenswert:

- Fuzzing des Binary Readers
- Differential Tests zwischen C# und neuen Implementierungen
- Property Tests für Writer/Reader
- Corpus für beschädigte `.gesb`-Dateien

## 11. Performance- und Allokationsvertrag

Performance-Conformance sollte nicht einfach Laufzeiten verschiedener CI-Maschinen vergleichen.

Stattdessen:

- gemeinsame JSON-Workloads
- native Benchmark-Harnesses je Sprache
- Message-Erzeugung, JSON-Decoding, Linking und VM-Ausführung getrennt messen
- Warmup eindeutig definieren
- Zero-allocation-Anforderungen für:
    - Queue-Dispatch
    - Handlerauswahl
    - VM-Resume
    - Frame-Result
- erlaubte Allokationen bei:
    - Program Load
    - Queue-Wachstum
    - Register-Wachstum
    - Output-Message-Erzeugung
- Speicher-Maxima und Wachstumsregeln dokumentieren
- Regression-Baselines pro Sprache und Plattform

C++ benötigt dabei eher Allocator-Instrumentierung, Swift Instruments/XCTest-Metriken, Kotlin JMH beziehungsweise Android-Benchmarks und C# BenchmarkDotNet/GC-Zähler.

## 12. Dokumentation konsolidieren

Vor Beginn der Ports sollten die Dokumente in drei Klassen geteilt werden:

- normative Spezifikation
- Implementierungsnotizen
- historisches Memory/Handoff

Normativ sollten sein:

- Sprachsemantik
- Bytecodeformat
- Opcodeformen
- Host-State-Machine
- API-Verantwortlichkeiten
- Conformance-Schema
- Fehlercodes

[GameEventScript.Memory.md](/Users/stephan/Projects/BattleClub/BeamableServices/services/StepH-GameEventScript/GameEventScript.Memory.md) sollte nicht als Portierungsvertrag dienen, sondern nur als Historie. Widersprüche zwischen Code, Memory und Specs müssen vor der jeweiligen Portierung aufgelöst werden.

## Empfohlene Reihenfolge

### Phase A – vor oder direkt beim Import

1. Zielumfang je Sprache festlegen: Compiler, Runtime oder beides.
2. Monorepo-Verzeichnisse und gemeinsame Spec-Ablage anlegen.
3. sprachneutrale Text-, Zahlen-, Fehler- und Ownership-Verträge festlegen.
4. External-Type-Metadaten von CLR-Bindings trennen.
5. JSON-Schema und Runner-Vertrag definieren.
6. Opcode-IDs und Binary-Version 1 einfrieren.
7. Beamable/Unity-Abhängigkeit aus dem Core lösen.

### Phase B – erstes gemeinsames Portierungs-Gate

8. `.gesb` Writer, Reader und Validator implementieren.
9. Golden- und Invalid-Binary-Fixtures ergänzen.
10. geordnete JSON-Argumentdarstellung einführen.
11. portable Registry-Fixtures für Extensions und External Types einführen.
12. portable C#-Tests in JSON überführen.
13. C# muss sämtliche neuen Fixtures bestehen.

### Phase C – erste zweite Runtime

14. kleinste Runtime, vermutlich Kotlin oder Swift, gegen bestehende `.gesb`-Fixtures implementieren.
15. Differential Tests gegen C#.
16. Host- und Runtime-Conformance vollständig herstellen.
17. Performance-/Allokationsmessung der zweiten Runtime.
18. danach weitere Sprachen und Compilerportierungen.

## Kein Monorepo-Blocker

Diese Backlog-Themen können weiterhin warten:

- bessere Liveness/Register-Wiederverwendung
- allgemeines `fold`/`reduce`
- Tables und Mutation Queue
- VM-nähere Extension-Optimierung
- weitere Message-/Emit-Allokationsoptimierung
- endgültiges Produkt-Wire-Messageformat

Das Conformance-JSON muss allerdings unabhängig davon jetzt stabilisiert werden. Es ist ein Testformat und sollte nicht mit dem späteren Netzwerkformat gekoppelt werden.

Der kritischste Pfad ist damit:

**portabler Semantikvertrag → External-Binding-Trennung → JSON-Schema → `.gesb` v1 + Validator → Golden Fixtures → erste zweite Runtime.**