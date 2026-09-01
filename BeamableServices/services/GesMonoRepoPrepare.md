## Ergebnis

Deine drei Punkte sind richtig, reichen aber noch nicht ganz. Vor allem fehlen sprachneutrale Verträge, ohne die Swift, Kotlin, C++ und C# trotz identischer JSON-Tests unterschiedlich reagieren könnten.

Wichtig: Der physische Umzug ins Monorepo muss nicht warten, bis alle Punkte fertig sind. Sinnvoll ist, zuerst Struktur und gemeinsame Verträge anzulegen und die weitere Portabilitätsarbeit anschließend direkt im Monorepo durchzuführen.

## 1. `.gesb`-Binary vollständig definieren - DONE

`.gesb` V1 ist spezifiziert und implementiert. Das normative Containerformat steht
in [GesbFormatV1.md](StepH-GameEventScript/GesbFormatV1.md); Opcode-Semantik und
Operandenformen stehen weiterhin in
[BytecodeSpec.md](StepH-GameEventScript/BytecodeSpec.md) und
[BytecodeOpcodeShape.md](StepH-GameEventScript/BytecodeOpcodeShape.md).

Abgeschlossen sind:

- kanonischer Little-Endian-Writer und begrenzter byteorientierter Reader
- gemeinsamer vollständiger Validator für Reader, Writer und `Host.Load`
- festes Datei- und Section-Framing ohne CLR-Layout-Abhängigkeit
- striktes UTF-8, IEEE-754-Binary64 und overflow-sichere Größenlimits
- stabile Formatfehler für ungültige Header, Sections, Referenzen, Operanden,
  Sprünge, Calls, Ressourcenmetadaten und Debugdaten
- azyklische Call-Graph-Validierung auch für geladene, nicht vertrauenswürdige
  Programme
- unabhängige optionale Segmente für DebugSymbols, SourceMap, SourceArchive und
  BuildMetadata
- Retention unbekannter optionaler Sections sowie kanonisches erneutes Schreiben
- Golden-, Invalid-, Retention-, Unicode-, Runtime- und JSON-Roundtrip-Tests

Festgelegte Abgrenzungen:

- V1 enthält keine Datei-Checksumme und keine Authentizitätsgarantie. Der
  Security-ID-Bereich ist reserviert; Signaturen, Schlüssel und Trust Policy sind
  getrennte spätere Themen.
- Bekannte V1-Sections sind unkomprimiert. Codec-Bits sind reserviert und
  Kompression bleibt eine spätere Erweiterung.
- Kanonische unkomprimierte Programs schreiben sich byteidentisch erneut.
  Verschiedene Compiler dürfen jedoch unterschiedlichen semantisch gleichwertigen
  Bytecode und unterschiedliche Registerbelegungen erzeugen.

Noch fehlende sprachübergreifende `.gesb`-Fixtures werden unter Punkt 5 geführt,
weil ihre Ausführung erst gemeinsam mit den Runtime-Ports geprüft werden kann.

## 2. Portable Sprache und Runtime-Semantik festschreiben

Mehrere aktuelle Verhaltensweisen hängen implizit an .NET.

### 2.1 Text und Unicode - DONE

Der sprachneutrale Vertrag ist in
[PortableTextSemantics.md](StepH-GameEventScript/PortableTextSemantics.md)
festgeschrieben und in Compiler, Runtime, Host-API und `.gesb`-Validator
umgesetzt:

- Source ist striktes UTF-8; ein initiales BOM wird entfernt.
- Namen und Tags verwenden explizite ASCII-Grammatiken.
- Portable Whitespace-/Newline-Zeichen sind fest definiert.
- Textlänge, Indexierung und Iteration verwenden Unicode Scalars bei
  weiterhin 1-basierten Indizes.
- Textgleichheit ist exakt, Sortierung lexikografisch nach Unicode Scalar und
  es findet keine Normalisierung statt.
- Compilerzeilen/-spalten sind 1-basiert und Scalar-basiert; SourceMaps bleiben
  UTF-8-Byte-basiert.
- JSON-Conformance deckt Supplementary-Plane-Zeichen, kombinierende Zeichen,
  Scalar-Sortierung und unzulässige Unicode-Namen/Whitespace ab.

### 2.2 Zahlen - DONE

Der sprachneutrale Vertrag ist in
[PortableNumberSemantics.md](StepH-GameEventScript/PortableNumberSemantics.md)
festgeschrieben und in Compiler, VM, Value-Modell sowie JSON-Conformance
umgesetzt:

- Int64-Operationen erkennen Überlauf ohne CLR-Checked-Kontext und wechseln bei
  Überlauf definiert auf Binary64; exakte Werte oberhalb `2^53` bleiben integer.
- Float-zu-Integer-Konvertierung nutzt explizite `2^63`-Grenzen, Truncation und
  Sättigung.
- NaN, Infinity, negative Null und alle Rundungsmodi sind festgelegt.
- `div`, `mod` und `rem` besitzen definierte Regeln für negative Operanden.
- Transzendente Funktionen verwenden die Plattform-Binary64-Mathematik mit
  konfigurierbarer ULP-Toleranz in JSON-Tests; Runtime-Gleichheit bleibt bei zwei
  ULPs.
- Conformance-Floats verwenden kürzeste Roundtrip-Dezimaldarstellung mit
  kanonischem Exponenten statt eines C#-spezifischen festen Formats.
- Grenzfälle liegen sowohl als direkte Low-Level-Tests als auch als portable
  JSON-Conformance vor.

### 2.3 Determinismus - DONE

Der sprachneutrale Vertrag ist in
[PortableDeterminismSemantics.md](StepH-GameEventScript/PortableDeterminismSemantics.md)
festgeschrieben und durch Low-Level- sowie JSON-Conformance-Tests abgesichert:

- `:sort` und `:order by` sind aufsteigend wie absteigend stabil; gleiche
  Schlüssel behalten ihre Quellreihenfolge.
- Maps und Records verwenden Unicode-Scalar-Keyorder; doppelte Map-Keys sind
  deterministisch last-entry-wins.
- Top-Level-Numeric-Coercion und strikte strukturelle Gleichheit sind getrennt;
  Cross-Kind-, Dice-, List-, Map- und Record-Fälle liegen in JSON vor.
- Range-Länge, Richtung, Zero-Step, Int64-Grenzen und nicht fortschreitende
  Binary64-Iteratoren sind festgelegt. Iteratoren stoppen anhand der
  vorab berechneten Länge und können an Zahlengrenzen nicht weiterwrappen.
- Gleich priorisierte Handler laufen in Registrierungsreihenfolge; bei mehreren
  Programs entspricht sie der Load-Reihenfolge. Dispatch-Snapshots bleiben
  unverändert.

### 2.4 Random - DONE

Der sprachneutrale Random-Vertrag ist ebenfalls in
[PortableDeterminismSemantics.md](StepH-GameEventScript/PortableDeterminismSemantics.md)
festgeschrieben und durch Low-Level- sowie JSON-Conformance-Tests abgesichert:

- SplitMix64 initialisiert den xoshiro256**-Zustand aus dem vollständigen
  signed Int64 Seed; Zustandsübergang und Wrapping-Operationen sind normativ.
- Raw-UInt64-, rejection-sampled Integer- und exakte Binary64-Bitvektoren
  decken Seed `0`, `1`, `-1`, `Int64.MinValue`, `Int64.MaxValue` und Seeds
  oberhalb von 32 Bit ab.
- Der vollständige Int64-Bereich, vertauschte und identische Grenzen sowie deren
  exakter Stream-Verbrauch sind definiert und getestet.
- Verschachtelte `random with`-Scopes besitzen getrennte Generatoren und setzen
  die jeweils äußere Sequenz anschließend exakt fort.
- Die Float-API heißt `NextFloat`: Sie skaliert eine `[0,1)`-Quelle auf die
  geordneten Bounds. Binary64-Rundung darf dennoch den oberen Bound erzeugen;
  auch dieser Fall ist mit einem exakten Bitmuster abgesichert.
- NaN- und identische Bounds verbrauchen weder einen PRNG- noch einen
  `FromSequence`-Wert.

## 3. C#-Kopplungen im portablen API/Core auflösen

Nicht jedes C#-Sprachkonstrukt muss entfernt werden. `readonly struct`, `ref`,
`init`, `IEnumerable`, `AggressiveInlining` und auch das interne `object?`-Feld
der C#-Implementierung von `GesValue` dürfen Implementierungsdetails bleiben.
Entscheidend ist, dass sie keinen sprachübergreifenden Vertrag definieren und
keine beliebigen CLR-Objekte in die portable API oder Runtime abstrahiert werden.

### 3.1 External Types - DONE

Compilerbeschreibung und ausführbare Runtime-Bindings sind getrennt:

- `IGameEventScriptExternalTypeCatalog` enthält ausschließlich deklarative
  Typ-, Feld-, Konstruktor-, Parameter- und Signaturdaten.
- Der Compiler verwendet nur den Katalog; ausführbare Bindings werden weder zur
  Compilation benötigt noch im Program gespeichert.
- `IGameEventScriptExternalTypeRegistry` ist die getrennte hostgebundene
  Linker-Grenze für Konstruktorimporte bei `Host.Load`.
- `IGameEventScriptExternalValue` bildet Runtime-Werte und Feldzugriff ohne
  beliebige Plattformobjekte im portablen Core ab.
- CLR-Objekte, Reflection, Attributes, Reader, Converter und Aufrufe liegen im
  `CSharpBridge`. Der C#-Adapter kombiniert Katalog und Registry lediglich als
  Komfortoberfläche.
- JSON-Conformance verwendet einen manuellen Katalog, eine manuelle Registry und
  einen manuellen `aim`-Wert; C#-Reflection bleibt in separaten Bridge-Tests.

### 3.2 Native Handler und Lebenszyklus

Der Core-Host verwendet direkt `Action<GameEventScriptMessage,
GameEventScriptContext>`. `GameEventScriptInstance` und
`GameEventScriptSubscription` halten außerdem `Func<bool>`-Closures für Detach
und Unsubscribe.

Benötigte Schritte:

1. Eine portable Native-Handler-Schnittstelle beziehungsweise Handler-Referenz
   definieren und im Core-Host verwenden.
2. Bequeme `Action`-Overloads und deren Adapter in den `CSharpBridge`
   verschieben.
3. Hostseitige stabile Registrierungs-IDs für Programminstanzen und native
   Subscriptions einführen.
4. `GameEventScriptInstance.Detach()` und
   `GameEventScriptSubscription.Unsubscribe()` über Host plus
   Registrierungs-ID ausführen, ohne gespeicherte Closures.
5. Idempotenz und die bestehende Snapshot-Semantik beibehalten: bereits
   eingereihte Dispatch-Snapshots laufen weiter, spätere Nachrichten sehen die
   entfernte Registrierung nicht mehr.
6. API-Snapshot, Native-only-Host-Tests, dynamische Registrierung sowie den
   CSharpBridge-Auto-Runner entsprechend migrieren.

### 3.3 Geordnete Message-Argumente

Die interne Message-Repräsentation ist bereits geordnet. Der portable Vertrag
wird aber durch `GameEventScriptMessageArguments.Create(IReadOnlyDictionary...)`
und JSON-`args` als Objekt verletzt, weil dabei Map- beziehungsweise
Property-Iteration die Signaturreihenfolge bestimmt.

Benötigte Schritte:

1. Eine explizit geordnete portable Argumentdarstellung für die öffentliche
   Core-API festlegen. Argumentname und Wert bleiben ein geordnetes Paar; die
   Reihenfolge ist Teil der Message-Signatur.
2. Die namebasierte Dictionary-Factory aus dem Core entfernen. Ein
   Dictionary-Adapter darf nur zusammen mit einer bereits bekannten Signatur
   Werte nach deren Parameterreihenfolge binden.
3. C#-Tuple- und Dictionary-Komfortoberflächen bei Bedarf im `CSharpBridge`
   anbieten, ohne einen eigenen Reihenfolgevertrag zu erzeugen.
4. Doppelte normalisierte Argumentnamen und andere mehrdeutige Shapes
   portabel und deterministisch ablehnen.
5. Alle JSON-Conformance-Messages auf eine geordnete Darstellung migrieren:

   ```json
   "args": [
     { "name": "target", "value": { "type": ":text", "value": "t1" } },
     { "name": "unit", "value": { "type": ":text", "value": "u1" } }
   ]
   ```

6. Input, erwartete lokale und outbound Messages, verschachtelte Message-Werte
   sowie externe Emits einheitlich umstellen.
7. JSON-Conformance für unterschiedliche Argumentreihenfolgen, leere Argumente,
   doppelte Namen und Signaturmatching ergänzen.

### 3.4 Stabiler Fehlervertrag

`.gesb`-Formatfehler besitzen bereits stabile Codes. Compile-Fehler sind nur
teilweise strukturiert; einzelne Compilerpfade, Dynamic Linking und VM-Fehler
verwenden weiterhin freie englische Texte. Runtime-Fehler sind außerdem nicht
als strukturierte Ereignisse über Execution Result oder Observer sichtbar.

Benötigte Schritte:

1. Einen sprachneutralen Diagnosevertrag mit stabiler Fehlerphase und stabilem
   Fehlercode definieren. Mindestens `parse`, `validate`, `compile`, `decode`,
   `link` und `runtime` müssen eindeutig unterscheidbar sein.
2. Gemeinsame optionale Diagnosefelder für Symbol, Symbolart, Source Range,
   Program beziehungsweise Handler und technische Details festlegen. Freier
   Meldungstext bleibt nur eine menschenlesbare Ergänzung.
3. Alle Parser-, Validator- und Compilerfehler strukturiert erzeugen; generische
   `GameEventScriptCompileException(string)`-Pfade aus dem portablen Ablauf
   entfernen.
4. `GameEventScriptDynamicLinkException` um stabile Link-Codes und relevante
   Referenzinformationen erweitern.
5. VM- und Runtime-Fehler mit stabilen Codes versehen und über einen definierten
   Observer- beziehungsweise Execution-Vertrag sichtbar machen, ohne den
   Runtime-Hot-Path unnötig zu allozieren.
6. Exceptions als C#-Transportmechanismus behandeln; der portable Vertrag sind
   die Diagnosedaten, nicht die CLR-Exception-Hierarchie.
7. JSON-Conformance von `messageContains` auf Phase plus Fehlercode und, wo
   sinnvoll, Source-/Symbolfelder migrieren. Englischer Text darf nicht mehr
   über Bestehen oder Fehlschlagen eines portablen Tests entscheiden.

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
- Binary-Fixture-Unterstützung mit eingecheckten kanonischen `.gesb`-Dateien und
  sprachneutralem Manifest für Source, Compileroptionen, ProgramVersion und
  erwarteten Datei-Hash
- Trennung zwischen Conformance-JSON und zukünftigem Produkt-/Wire-JSON

### In JSON zu verschieben

- Message-Normalisierung und Signaturen
- geordnete Argumente
- Value-Semantik und Value-Kinds
- Lists, Maps, Records, Dice, Range, Vector und Point
- Random-Known-Answer-Tests
- Compiler-Metadaten
- `.gesb` Read/Write/Validation einschließlich beschädigter Fixtures und stabiler
  Formatfehler
- dieselben eingecheckten `.gesb`-Fixtures in C#, Swift, Kotlin und C++ laden und
  mit identischen Runtime-Ergebnissen ausführen
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
6. die bereits festgelegte Binary-Version 1 übernehmen und Opcode-IDs über eine
   zentrale maschinenlesbare Definition stabilisieren.
7. Beamable/Unity-Abhängigkeit aus dem Core lösen.

### Phase B – erstes gemeinsames Portierungs-Gate

8. kanonische Golden- und Invalid-`.gesb`-Fixtures samt sprachneutralem Manifest
   aus der bestehenden C#-Implementierung erzeugen und einchecken.
9. geordnete JSON-Argumentdarstellung einführen.
10. portable Registry-Fixtures für Extensions und External Types einführen.
11. portable C#-Tests in JSON überführen.
12. C# muss sämtliche neuen Fixtures bestehen.

### Phase C – erste zweite Runtime

13. kleinste Runtime, vermutlich Kotlin oder Swift, gegen bestehende `.gesb`-Fixtures implementieren.
14. Differential Tests gegen C#.
15. Host- und Runtime-Conformance vollständig herstellen.
16. Performance-/Allokationsmessung der zweiten Runtime.
17. danach weitere Sprachen und Compilerportierungen.

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

**portabler Semantikvertrag → External-Binding-Trennung → JSON-Schema und Runner-Vertrag → portable `.gesb`-Fixtures → erste zweite Runtime.**
