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

## 2. Portable Sprache und Runtime-Semantik festschreiben - DONE

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

## 3. C#-Kopplungen im portablen API/Core auflösen - DONE

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

### 3.2 Native Handler und Lebenszyklus - DONE

- Der portable Core verwendet `IGameEventScriptNativeMessageHandler`; der Host
  speichert keine C#-Delegates mehr.
- Sprachspezifische `Action<GameEventScriptMessage, GameEventScriptContext>`-
  Overloads und Adapter liegen ausschließlich im `CSharpBridge`.
- Der Host vergibt stabile, nicht wiederverwendete hostlokale Registrierungs-IDs
  für Programminstanzen und native Subscriptions.
- `GameEventScriptInstance` und `GameEventScriptSubscription` führen Detach bzw.
  Unsubscribe über Host plus Registrierungs-ID aus und halten keine
  `Func<bool>`-Closures.
- Idempotenz und Enqueue-Snapshot-Semantik bleiben erhalten: bereits
  eingereihte Handler laufen weiter, spätere Nachrichten sehen entfernte
  Registrierungen nicht mehr.
- JSON-Conformance verwendet einen manuellen portablen Handler; Native-only-
  Host, dynamische Registrierung und C#-Auto-Runner bleiben separat getestet.

### 3.3 Geordnete Message-Argumente - DONE

- `GameEventScriptMessageArgument` ist das portable immutable Name-/Wert-Paar;
  `GameEventScriptMessageArguments` und alle Core-Factorys konsumieren explizit
  geordnete Listen.
- Dictionary- und Tuple-Factorys sind aus dem Core entfernt. C#-Tuple-Helfer
  liegen im `CSharpBridge`; dessen Dictionary-Adapter verlangt eine bekannte
  Signatur und bindet in deren Parameterreihenfolge.
- Doppelte benannte Labels werden nach Normalisierung deterministisch
  abgelehnt. Wiederholtes `_` bleibt als positionale Argumentform erlaubt.
- Sämtliche Conformance-Nachrichten verwenden die geordnete Darstellung:

   ```json
   "args": [
     { "name": "target", "value": { "type": ":text", "value": "t1" } },
     { "name": "unit", "value": { "type": ":text", "value": "u1" } }
   ]
   ```
- Das gilt für Input, erwartete lokale/outbound Messages, verschachtelte
  Message-Werte und externe Emits. Der Conformance-Vergleich prüft Namen und
  Werte positionsgetreu.
- JSON-Conformance deckt unterschiedliche Reihenfolgen, leere Argumentlisten,
  doppelte normalisierte Namen und Signaturmatching ab.

### 3.4 Stabiler Fehlervertrag - DONE

- `GameEventScriptDiagnostic` definiert die gemeinsamen Phasen `parse`,
  `validate`, `compile`, `decode`, `link` und `runtime`, stabile ASCII-Codes und
  optionale Symbol-, Source-, Program-, Handler- und technische Felder.
- Parser, Validator und Compiler erzeugen direkt Diagnosen; der freie öffentliche
  String-Konstruktor und die alte separate Compile-Error-Oberfläche sind entfernt.
- `.gesb`-Fehler werden verlustfrei als `decode.<formatCode>` gespiegelt. Link-
  Exceptions tragen strukturierte Link-Codes und Referenzkontext.
- VM-/Native-Fehler werden nur auf dem Fehlerpfad materialisiert, über den
  Observer gemeldet und als `RuntimeError` plus erster Diagnose im Execution-
  Result sichtbar. Der fehlerhafte Handler wird resetet; der Dispatch-Snapshot
  läuft deterministisch weiter.
- JSON-Conformance verwendet ausschließlich Phase plus Code und optionale
  strukturierte Felder. `messageContains` wurde aus portablen Fehlererwartungen
  entfernt; Runtime-Diagnosen sind als geordnete Observer-Ereignisse prüfbar.
- Der normative Vertrag steht in `StepH-GameEventScript/PortableDiagnostics.md`.

## 4. `GameEventScriptProgram` als wirklich portables Datenmodell härten - DONE

Der `.gesb`-V1-Umbau hat einen großen Teil dieses Punkts bereits vorweggenommen.
Das Programmodell besitzt einen internen Konstruktor, seine Tabellen werden
defensiv kopiert und Reader, Writer sowie `Host.Load` verwenden den vollständigen
Validator. Die verbleibende Arbeit ist daher ein gezieltes Audit mit
Regressionstests statt eines weiteren großen Umbaus.

### 4.1 Programmodell und Ownership auditieren - DONE

- Der vollständige Program-Objektgraph enthält ausschließlich feste skalare
  Werte, Strings sowie immutable Segment-, Tabellen-, Entry- und Payloaddaten,
  die eine verlustfreie `.gesb`-Repräsentation besitzen.
- Delegates, Registries, CLR-Objekte, Runtime-Caches, VM-/Host-Referenzen,
  AST-Knoten und Compilerzustand liegen nachweislich außerhalb des Programs.
- Alle Sequenzen werden beim Aufbau defensiv kopiert. Der bisherige interne
  `UnsafeItems`-Arrayzugriff wurde entfernt; Core-Komponenten erhalten für
  Bulk-Reads nur noch `ReadOnlySpan<T>` oder immutable Slices.
- Linked Strings, Imports, ID-Indizes und andere Beschleunigungsstrukturen bleiben
  in `GesLinkedProgram` und werden nicht in das Program zurückgeschrieben.
- Der normative Daten-, Ownership- und Immutability-Vertrag steht in
  `StepH-GameEventScript/PortableProgramModel.md`.

### 4.2 Konstruktions- und Validierungsgrenzen absichern - DONE

- Compiler und Reader sind die einzigen öffentlichen Program-Erzeugungspfade;
  eine freie öffentliche Konstruktion aus Segmenten existiert nicht.
- Neben dem Reader validiert nun auch der Compiler das vollständig materialisierte
  Ergebnis mit dem gemeinsamen Programvalidator, bevor es zurückgegeben wird.
- Der Writer validiert vor Größenberechnung oder Ausgabe und schreibt daher keine
  Bytes eines ungültigen Programs.
- `Host.Load` validiert erneut, bevor Ressourcen geprüft, Bindings aufgelöst,
  VM-Speicher vorbereitet, Handler registriert oder Initialisierung eingereiht
  werden. Ein Fehler lässt den Host unverändert.
- Die absichtlich wiederholten Trust-Boundary-Prüfungen sind normativ in
  `PortableProgramModel.md` und `GesbFormatV1.md` festgehalten.

### 4.3 Encoding-Unabhängigkeit absichern - DONE

- `.gesb` wird ausschließlich aus seinen normativ spezifizierten numerischen
  Feldern kanonisch Little Endian gelesen und geschrieben.
- Die C#-Instruction verwendet keine überlagerten CLR-Felder mehr. Drei rohe
  `u16`-Wörter und ein `u64`-Payload bilden die Speicherung; Register-, Index-,
  Signed-, Aux-, Integer- und Float-Sichten sind numerische Properties mit
  Casts, Shifts, Masken und exakter Binary64-Bitkonvertierung.
- Damit sind insbesondere `AU/BU/CU/DU`, `I64` und `F64` unabhängig von nativer
  Endianness. Das weiterhin 16 Byte große sequenzielle C#-Struct ist nur eine
  Runtimeoptimierung und keine Formatdefinition.
- Alle im Program codierten enumähnlichen Werte besitzen explizite numerische
  IDs; Deklarationsreihenfolge und Sprach-Ordinals sind ohne Bedeutung.
- Das Program enthält keine Plattform-Hashwerte. Die einzige persistierte
  Hashart ist das normativ definierte SHA-256 der SourceMap.
- Der Vertrag ist in `PortableProgramModel.md`, `GesbFormatV1.md` und
  `BytecodeOpcodeShape.md` festgehalten.

### 4.4 Regressionstests und Abschluss - DONE

- Ein Programmodell-Test mutiert nach der Konstruktion sämtliche übergebenen
  Top-Level- und verschachtelten Sequenzen. String-/Index-Pools, Bindargumente
  und Tags, Code, Debugsymbole, SourceMap-Hashes und Line-Offsets, SourceArchive
  sowie opaque Payloads bleiben unverändert.
- Ein Reflection-Test schützt den fehlenden öffentlichen Program-Konstruktor.
- Ein vollständiger `Compile -> Write -> Read -> Write -> Host.Load -> Execute`-
  Test sichert kanonische Bytes und identisches Runtimeverhalten gemeinsam ab.
- Numerische Word-Aliase, Signed-Sichten, Aux-Payload-Reihenfolge, `I64`, exakte
  Binary64-Bits und die kompakte 16-Byte-C#-Instruction sind explizit getestet.
- Golden Fixtures und der Public-API-Snapshot schützen zusätzlich sämtliche
  kanonischen Bytes und expliziten Enum-/Opcode-IDs.
- Der Abschluss ist in `PortableProgramModel.md` und `AGENTS.md` festgehalten.

## 5. Portable Conformance ausbauen

Es gibt derzeit 34 JSON-Spec-Dateien mit 956 Fällen, davon 839 `scriptApi`-
Fälle. Die Infrastruktur ist bereits eine gute semantische Basis, ihr Format,
Runner und Ergebnisweg sind aber noch C#- und MSTest-zentriert.

Insbesondere fehlen ein formales Schema, explizite stabile Case-IDs,
Capabilities, portable Skip-Regeln und ein sprachneutraler Ergebnisvertrag. Die
heutige abgeleitete ID `SuiteName/Test.Name` ist nicht stabil und bereits ein
Anzeigename kommt innerhalb derselben Suite doppelt vor. Der C#-Reader
akzeptiert außerdem Kommentare, Trailing Commas und Property-Namen unabhängig
von Groß-/Kleinschreibung; andere JSON-Implementierungen müssen dieses Verhalten
nicht teilen.

Als zukünftiges Autorenformat ist Markdown vorgesehen. YAML-Frontmatter enthält
die Suite-Einstellungen und Defaults. Ein `## Test: ...` leitet einen Testfall
ein; der Fall endet am nächsten solchen H2-Heading oder am Dateiende. Normaler
GES-Source steht in `ges`-Codeblöcken, Testmetadaten und Erwartungen verwenden
ein eingeschränktes YAML-Profil, und geordnete Testschritte werden als klar
definierte Markdown-Tabellen geschrieben. Freie Prosa, Notes und sonstige
Markdown-Inhalte bleiben nichtnormativ.

Semantische YAML-Blöcke verwenden ausschließlich den normalen Fence-Info-String
`yaml`, damit verbreitete Markdown-Highlighter sie zuverlässig erkennen. Das
Pflichtfeld `gesBlock` im Root-Mapping unterscheidet `case` von `expect`;
zusätzliche Fence-Tokens wie `yaml ges-case` gehören nicht zum Format.

Ein optionaler reservierter Abschnitt `## Fixtures` darf Werte und Matrizen zur
besseren Lesbarkeit dokumentieren, wird in V1 aber vollständig vom Runner
ignoriert. Falls später parameterisierte beziehungsweise Matrix-Tests daraus
erzeugt werden sollen, benötigen sie ein neues explizites und versioniertes
Konstrukt. Eine spätere Formatversion darf vorhandene dokumentierende Fixtures
nicht still in ausführbare Semantik umdeuten.

Das Autorenformat darf den sprachneutralen semantischen Vertrag nicht mit einem
bestimmten Testframework oder einer Implementierungssprache koppeln. Ein
normalisiertes maschinenlesbares Testmodell und Ergebnisformat bleiben
erforderlich.

Die Umsetzung erfolgt bewusst vor der Ergänzung weiterer Conformance-Fälle.
Neue Semantik soll nicht mehr in das auslaufende JSON-Autorenformat eingebaut
werden. Die bestehende Suite wird zunächst ohne fachliche Änderungen in das
neue Format überführt; Erweiterungen folgen erst nach nachgewiesener Parität.

### 5.1 Markdown- und Runner-Vertrag normativ spezifizieren - DONE

Der Autorenvertrag steht normativ in
[ConformanceMarkdownV1.md](StepH-GameEventScript/ConformanceMarkdownV1.md), der
Ausführungs-, Capability-, Ergebnis-, Report- und Received-Vertrag in
[ConformanceRunnerV1.md](StepH-GameEventScript/ConformanceRunnerV1.md).

Festgelegt sind insbesondere der bewusst kleine Markdown-Strukturscanner, das
strikte YAML-Subset, stabile Suite-/Case-IDs, Vererbung, Source-/Programmgruppierung,
geordnete Step-Tabellen, portable Message-/Value-Formen und die Schemas aller
V1-Testarten. Fixtures bleiben ausdrücklich reine Dokumentation. Binary64 ist
standardmäßig bitexakt; ULP-Toleranz muss explizit gewählt werden.

Der Runner-Vertrag trennt Parser und Ausführung, definiert Core- gegenüber
optionalen Capabilities, Status/Fehlerklassifikation, Einzel-/Dokument-/Corpus-
Ausführung, Performanceprofile und das kanonische JSON-Ergebnis. Markdown-Report
und Received-Ausgabe werden aus strukturierten Ergebnissen erzeugt; Received darf
nur Performance-Referenzen und `gesa`-Payloads ersetzen und überschreibt nie die
Quelldatei.

- `ConformanceMarkdownV1.md` als normative Beschreibung des Autorenformats
  anlegen.
- UTF-8, Newlines, YAML-Frontmatter, Heading-, Codeblock- und Tabellengrammatik
  exakt festlegen, ohne einen vollständigen Markdown-AST zum Teil der Semantik
  zu machen.
- `## Test: ...` als Testanfang und das nächste solche Heading beziehungsweise
  das Dateiende als Testende definieren.
- `ges`-Blöcke als Source, eingeschränktes YAML als Testmetadaten und
  Expectations sowie Tabellen als geordnete Steps definieren.
- `## Fixtures` als optionalen, vollständig nichtnormativen und vom V1-Parser
  ignorierten Dokumentationsabschnitt festlegen. Eine spätere ausführbare
  Matrixfunktion benötigt ein neues explizites, versioniertes Konstrukt.
- Ein portables YAML-1.2-Subset ohne Anchors, Aliases, Merge Keys,
  benutzerdefinierte Tags, Directives, Block-Scalars, implizite Datumswerte,
  komplexe Keys, doppelte Mapping-Keys oder mehrere Dokumente definieren.
- Stabile `suiteId` und Case-ID von veränderbaren Titeln und Anzeigenamen
  trennen; Kategorien, Level, Tags und `requires`-Capabilities explizit
  modellieren.
- Vererbung und Overrides zwischen Suite-Frontmatter und Testmetadaten normativ
  festlegen.
- Required-/Optional-Felder je Testart und das normalisierte immutable
  Conformance-Dokumentmodell definieren.
- Vergleichsmodi für Binary64-Werte festlegen: exakt als Standard und explizite
  ULP-Toleranz nur für die Operationen, die sie benötigen.
- Performance-Workload, Korrektheit und Performance-Baseline getrennt
  modellieren. Zeiten werden mit expliziter Zeiteinheit und Allokationen in
  `KiB` angegeben; ein `KiB` entspricht exakt 1024 Bytes.
- `bytecodeSnapshot` als eigene Testart mit normalem `ges`-Source und einem
  erwarteten `gesa`-Block im Format „Game Event Script Assembler“ definieren.
- `ConformanceRunnerV1.md` für Testarten, Capabilities, Skip-Regeln,
  Ausführungsreihenfolge, Einzel-/Gesamtausführung, Reports und Ergebnisformat
  anlegen.
- Ungültige Daten, unbekannte Testarten und fehlende Core-Capabilities als
  Fehler behandeln; nur fehlende optionale Capabilities dürfen einen Skip
  erzeugen.
- Conformance-Daten und -Ergebnisse klar von zukünftigem Produkt-/Wire-JSON
  trennen.

### 5.2 Portables Conformance-Package, Parser und Validator implementieren - DONE

Der isolierte Ordner und Namespace `StepH.GameEventScript.Conformance` enthält
nun eine öffentliche synchrone Byte-/Text-Parser-API, konfigurierbare Limits,
stabile strukturierte Diagnostics und ein vollständig normalisiertes immutable
Dokumentmodell. Host, VM, Runtime, Compiler und übrige Core-Bereiche verweisen
nicht zurück auf das Conformance-Package, sodass es im Monorepo mechanisch in
ein optionales Modul verschoben werden kann.

Der Parser implementiert einen eigenen strukturellen Markdown-Scanner sowie nur
das normative YAML-Subset, ohne CommonMark-/YAML-Abhängigkeit. Er validiert
geschlossene V1-Schemas, Vererbung, IDs, Capabilities, Kardinalitäten,
Source-/Programmgruppierung, Steps und alle Testarten. Semantische Block-,
Payload-, Tabellen-, Test-, Performance-Reference- und `gesa`-Ranges bleiben als
UTF-8-Bytebereiche für den späteren Received-Writer erhalten.

Case- und Expectation-Metadaten stehen in gewöhnlichen `yaml`-Fences und werden
über das verpflichtende Root-Feld `gesBlock: case` beziehungsweise
`gesBlock: expect` klassifiziert. YAML außerhalb eines Tests und unter
`## Fixtures` bleibt Dokumentation; die frühere Zusatzsyntax im Fence wird nicht
akzeptiert.

Native Bootstrap-Tests und physische Golden-/Invalid-Fixtures decken UTF-8/BOM,
alle Newlineformen, YAML-Flow-/Blockformen, Fixtures, Vererbung, typisierte
Expectations, Tabellen, Limits, Immutability und stabile Fehlercodes ab. Die
öffentliche API-Snapshot-Erwartung enthält die neue Modulgrenze.

Abnahme: 1100/1100 Nicht-Performance-Tests, 27/27 native Parser-Bootstrap-Tests
und der Zero-Allocation-Hot-Path-Test sind erfolgreich. Der zuvor bestätigte
JSON-Performance-Referenzlauf bleibt von dieser Parseränderung unberührt.

- Vorerst den Ordner und Namespace `StepH.GameEventScript.Conformance` innerhalb
  des bestehenden Core-Projekts verwenden. Host, VM, Compiler und andere
  Core-Bereiche dürfen nicht zurück auf Conformance verweisen.
- Die öffentliche API so schneiden, dass der komplette Bereich im Monorepo
  später mechanisch in ein eigenes optionales Modul verschoben werden kann.
- Einen eigenen deterministischen Markdown-Strukturscanner statt eines
  vollständigen CommonMark-Parsers implementieren.
- Einen eigenen Parser ausschließlich für das normative YAML-Subset
  implementieren; keine vollständige YAML-Implementierung und keine neue
  Produktionsabhängigkeit einführen.
- Frontmatter, Testgrenzen, GES-Codeblöcke, YAML-Blöcke und Step-Tabellen in ein
  normalisiertes immutable Dokumentmodell binden.
- SourceRanges der semantischen Blöcke erhalten, damit ein späterer
  Received-Writer gezielt Performancewerte und `gesa`-Snapshots ersetzen kann,
  ohne Prosa oder übrige Formatierung neu zu schreiben.
- Byte-/textorientierte synchrone APIs ohne File-, Thread- oder Async-Abhängigkeit
  anbieten. Filesystemzugriff bleibt Aufgabe des Aufrufers.
- Stabile Parser- und Validation-Diagnostics mit Sourcepositionen sowie Limits
  für Dokumentgröße, YAML-Tiefe, Testanzahl, Sourcegröße, Tabellenzeilen und
  Scalar-Längen definieren.
- Öffentliche Parser-APIs und Modelle bereitstellen, damit Nutzer denselben
  Mechanismus für eigene Script-Selftests verwenden können; interne Markdown-
  und YAML-Zwischenbäume bleiben nicht öffentlich.
- Golden- und Invalid-Fixtures für Markdown, YAML, Tabellen, Vererbung, IDs,
  Limits und Diagnostics anlegen. Der Parser selbst behält dafür native
  Bootstrap-Tests.

### 5.3 Runner vom Autorenformat und MSTest entkoppeln - DONE

`StepH.GameEventScript.Conformance.ConformanceRunner` ist nun eine öffentliche,
synchrone, filelose Ausführungsschicht ausschließlich über dem normalisierten
Dokumentmodell. `RunCase`, `RunDocument` und `RunCorpus` verwenden dieselbe
Execution-Pipeline; Dokument- und Corpus-Reihenfolge bleiben stabil und ein
optionaler Result-Sink erhält jedes immutable Case-Ergebnis genau einmal.

Das explizite Runner-Environment enthält Runner-/Implementierungsidentität,
sortierte Capabilities, optionale portable Extension-/External-Type-Registries
sowie Performanceprofil und Measurement-Provider. Core- und optionale
Capabilities führen normgerecht zu `error` beziehungsweise `skipped`.
Corpus-Duplikate und Runnerlimits werden vor der ersten Ausführung geprüft.

Alle acht Testarten werden ausgeführt: Script-API einschließlich Initialisierung,
Frames, nativen Handlern und Local/Outbound-Beobachtung; Compile- und
Loadfehler; Message-API; Compile-Metadaten; Opcode-Constraints;
Bytecode-Snapshots sowie Performance nach vorheriger Korrektheitsprüfung.
Binary64-Vergleich unterstützt exakte Bits und ULP, Performancegrenzen verwenden
den kleinsten angegebenen Bound. Ergebnisobjekte enthalten Status, stabile
Codes, Mismatches, Diagnostics, Runtime-Limits, Snapshottext und Messwerte.

MSTest ist nur noch ein kleiner Adapter für Case-Discovery, Anzeige und die
Abbildung des strukturierten Status auf Framework-Assertions. Kanonische JSON-,
Markdown-Report- und Received-Writer bleiben wie vorgesehen Gegenstand von 5.4.

Abnahme: 7/7 fokussierte Runner-/Adapter-Tests und 1107/1107 Nicht-Performance-Tests
sind erfolgreich; der bestehende Zero-Allocation-Hot-Path und der bestätigte
JSON-Performance-Referenzlauf bleiben separat verifiziert. Die öffentliche
API-Snapshot-Erwartung enthält die Runner-Grenze.

- Den Runner ausschließlich gegen das normalisierte Conformance-Dokumentmodell
  implementieren; Markdown-Parsing und Testausführung bleiben getrennte Phasen.
- Alle heute vorhandenen Testarten `scriptApi`, `compileError`, `loadError`,
  `messageApi`, `compileMetadata`, `bytecode` und `performance` sowie die neue
  Testart `bytecodeSnapshot` abbilden.
- MSTest-Assertions aus dem Runner entfernen und MSTest zu einem dünnen Adapter
  für Discovery und Anzeige machen.
- Eine öffentliche synchrone Runner-API mit explizitem Environment, Optionen,
  Capability-Menge und Result-Sink bereitstellen. Sie muss einzelne Cases sowie
  ein Dokument oder den gesamten Corpus ausführen können.
- Testframework-Adapter registrieren jeden H2-Case als separaten Test; die
  Gesamtausführung bleibt eine zusätzliche Runner-/Report-Funktion und ersetzt
  nicht die einzeln adressierbaren Tests.
- Pro Case `passed`, `failed`, `skipped` oder `error`, stabile ID, normativen
  Fehlercode, erwartete/tatsächliche Werte und optionale Diagnostics oder
  Debug-Dumps liefern.
- Runner-Implementierung, Runner-Version und unterstützte Capabilities
  maschinenlesbar ausgeben.
- Ein kanonisches sprachneutrales JSON-Ergebnisformat für CI und den Vergleich
  der Sprachports definieren. JSON ist hierbei Ergebnis- beziehungsweise
  Austauschformat, nicht mehr Autorenformat.
- Den strukturierten Gesamtbericht als einzige Grundlage für den in 5.4
  implementierten menschenlesbaren Markdown-Writer bereitstellen.
- Laufzeit- und Performancewerte außerhalb expliziter Performance-Expectations
  als nichtnormative Metadaten behandeln und Benchmarks von funktionaler
  Conformance trennen.

### 5.4 Reports, Performance-Baselines und Snapshot-Updates implementieren - DONE

Die portable, dateisystemfreie Writer-Schicht ist umgesetzt. Der
`ConformanceResultJsonWriter` erzeugt kanonisches UTF-8-JSON ohne BOM, mit LF,
stabiler Property-Reihenfolge und allen strukturierten Case-, Diagnose-,
Runtime-Limit- und Performanceergebnissen. Der
`ConformanceMarkdownReportWriter` erzeugt daraus einen lesbaren Gesamtbericht
mit Summary, Capabilities, Case-Tabelle, Performanceübersicht und Details zu
Failures/Errors.

Der `ConformanceReceivedMarkdownWriter` verwendet ausschließlich die beim
Parsen erfassten UTF-8-SourceRanges. Er ersetzt gemessene Performance-
`reference`-Token und tatsächliche GESA-Payloads, prüft Reportidentität,
Profile, Metriken und den alten Range-Inhalt gegen stale Inputs und kopiert alle
übrigen Bytes einschließlich BOM und Line Endings unverändert. Er liefert nur
Text beziehungsweise Bytes und überschreibt niemals eine Suite. Stabile
`conformance.received.*`-Fehler decken inkompatible Reports, fehlende/stale
Ranges und Überlappungen ab.

- Drei klar getrennte Artefakte vorsehen:
  - `ConformanceResults.json` als normatives maschinenlesbares Ergebnis,
  - `ConformanceReport.md` als menschenlesbaren Gesamtbericht,
  - `<Suite>.received.md` als optionalen Approval-Vorschlag mit aktualisierten
    dynamischen Expectations.
- Report- und Received-Writer bleiben filelos und liefern Text beziehungsweise
  Bytes. CLI, MSTest oder ein anderer Adapter entscheidet, ob und wohin sie
  geschrieben werden.
- Der Markdown-Gesamtbericht enthält Gesamtstatus, Case-Zahlen,
  Passed/Failed/Skipped/Error, Capability-Übersicht, Performanceübersicht und
  Details fehlgeschlagener Cases anhand stabiler IDs.
- Performanceparameter wie Iterationen, Warmup und Performanceprofil in den
  Testmetadaten halten; fachliche Output-Erwartungen und Performancewerte im
  Expectation-YAML getrennt abbilden.
- Performance-Metriken pro Test mit Reference/Maximum, Einheit und optionaler
  Regressionstoleranz definieren. Verbesserungen bestehen; nur Überschreitungen
  der erlaubten Grenze schlagen fehl.
- Performanceprofile explizit benennen, da Workload und Korrektheit portabel,
  Zeit- und Allokationsbaselines aber sprach-, Runtime- und plattformbezogen
  sein können.
- Für `bytecodeSnapshot` jeden GES-/GESA-Vergleich als eigenen H2-Test ausführen
  und den kanonischen Dumpertext nach normativer LF-Normalisierung exakt mit dem
  `gesa`-Expectation-Block vergleichen.
- Der Received-Writer ersetzt ausschließlich gemessene Performance-Referenzen
  beziehungsweise den betroffenen `gesa`-Block anhand ihrer SourceRanges.
  Toleranzen, Prosa, Tabellen, Kommentare und andere Testdaten bleiben
  byteinhaltlich unverändert.
- Niemals die Quell-Suite automatisch überschreiben. Eine `.received.md` wird
  nur als explizit zu prüfender Diff-/Copy-Vorschlag erzeugt.

Abnahme: sechs fokussierte Writer-Tests decken kanonisches JSON,
Markdown-Escaping und -Inhalte, Performance-/Failure-Ausgabe, bytegenaue
Performanceupdates mit BOM/CRLF, GESA-Updates sowie stale Reports ab.

### 5.5 Repräsentative vertikale Markdown-Migration durchführen - DONE

Der inzwischen entfernte vertikale Pilot lag als echte Markdown-Suite unter
einem temporären Pilotdokument, das nach der vollständigen Migration entfernt wurde.
Er übernimmt aus dem bisherigen JSON-Bestand je einen `scriptApi`-,
`compileError`-, `loadError`-, `messageApi`-, `compileMetadata`-, `bytecode`-
und `performance`-Fall sowie den bisherigen ersten Performance-Dump als
eigenständigen `bytecodeSnapshot`-Fall. Frontmatter-Vererbung, lokale
Overrides, Binary-Roundtrip, benannte Sourceinputs, Steps, Diagnosen,
Ressourcenmetadaten, Opcodebedingungen, Performanceprofil und exaktes GESA
werden dadurch gemeinsam abgedeckt.

Jeder H2-Fall wird einzeln vom portablen Parser und Runner ausgeführt. Ein
C#-Paritätstest lädt zusätzlich den zugehörigen bisherigen JSON-Fall, vergleicht
das normalisierte Modell und führt auch den alten Testpfad aus. Der
Performancepilot führt die fachliche Workload real aus und speist die neue
Messschnittstelle deterministisch mit den bestehenden C#-Referenzwerten; echte
Messung und Approval bleiben Aufgabe des expliziten Performance-Adapters. Ein
zusätzlicher Dokumentlauf prüft Gesamtstatus, JSON-/Markdown-Report und einen
byteidentischen Received-Write bei unveränderten Expectations.

Der Pilot bestätigte drei bereits normative, für den Migrator wichtige Details:
H2-Titel sind case-sensitiv, ein terminales Source-Newline wird durch eine
zusätzliche Leerzeile im Fence repräsentiert und portable `symbolKind`-Werte
verwenden lower camel case statt der bisherigen C#-Enum-Schreibweise. Es war
keine Erweiterung des Formats, Parsers oder Laufzeitmodells erforderlich. Neue
Conformance-Fälle werden ab diesem Schnitt als Markdown angelegt; JSON ist nur
noch temporäre Quelle für Schritt 5.6.

- Vor der Massenmigration mindestens einen vorhandenen Fall jeder heutigen
  Testart sowie einen `bytecodeSnapshot` nach Markdown übertragen.
- Dabei Frontmatter, per-Test-Overrides, GES-Source, Expectations, Steps,
  Compilerfehler, Loadfehler, Metadaten, Opcode-Erwartungen und Performanceinput
  real gegen Parser und Runner prüfen.
- Für jeden Pilotfall das normalisierte Modell und das Ausführungsergebnis mit
  dem bisherigen JSON-Fall vergleichen.
- Gefundene Lücken zuerst in Spezifikation, Parser oder Modell korrigieren und
  das Format erst danach für die vollständige Migration festschreiben.
- Ab diesem funktionsfähigen vertikalen Schnitt neue Conformance-Fälle nur noch
  als Markdown anlegen. Ein akuter Regressionstest darf vorher ausnahmsweise
  temporär noch als JSON entstehen.

### 5.6 Bestehende JSON-Suite deterministisch migrieren - DONE

Der deterministische Einmal-Migrator hat 34 Suites und alle 956 bisherigen
Fälle nach `Conformance/Suites` übertragen. Die lokalen IDs
`case-0001` usw. sind explizit materialisiert und unabhängig von den
Anzeigeüberschriften; der doppelte Titel im Message-Member-Test wurde als
`message handler member and index access` eindeutig gemacht. Jeder Fall trägt
sein aufgelöstes `atomic`/`scenario`-Level. Sourceinputs, getrennte Programs,
Steps, native Handler, Runtime-Limits, Diagnosen, Random-Sequenzen,
Binary64-Vergleich und sämtliche fachlichen Expectations liegen nun im
Markdownmodell vor.

Der Migrationslauf hat alle 956 Fälle nacheinander über den bisherigen und den
neuen Runner ausgeführt und gleiche erfolgreiche Ergebnisse bestätigt. Dabei
wurden alte Map-Objekte in die normative geordnete Entry-Form und numerische
Strings bedeutungsgleich in shortest-roundtrip Binary64 überführt. Zwei zuvor
implizite Negativformen sind nun explizit spezifiziert: eine ungeordnete
Message-Argument-Map ausschließlich für `invalidArgumentsShape` sowie
`{ any: true }` als Runtime-Limit-Wildcard. U+FEFF innerhalb eines GES-Fence ist
Sourceinhalt und kein Dokument-BOM. Außerdem wurden zwei bereits im neuen
Vergleicher sichtbare Paritätsfehler für leere Maps, Integer-/Float-Ranges und
nicht-finite Float-Einheiten korrigiert.

Die fünf Performancefälle enthalten je zwölf portable KiB-/Millisekunden-
Baselines aus dem bisherigen Referenzreport. Jeder Fall besitzt zusätzlich
einen eigenen, aus dem bisherigen kombinierten Referenzdump extrahierten
`bytecodeSnapshot`. Der aktive C#-Adapter entdeckt 961 H2-Fälle einzeln; ein
zusätzlicher Corpus-Test prüft Suite-/Case-Eindeutigkeit und führt den gesamten
Corpus aus. Nach bestätigter Parität wurden alle 34 JSON-Quelldateien entfernt.
Der danach tote Einmal-Migrator, die ignorierten Legacy-Testklassen und die alten
kombinierten Referenzdateien wurden geschlossen in Schritt 5.7 entfernt.

- Einen internen einmaligen JSON-zu-Markdown-Migrator auf Basis der bestehenden
  C#-Modelle bauen; er ist kein öffentliches API und keine dauerhafte
  Kompatibilitätsschicht.
- Allen 956 bestehenden Fällen stabile IDs zuweisen und den bereits vorhandenen
  doppelten Anzeigenamen ohne Ableitung der ID vom Titel bereinigen.
- `atomic`/`scenario` explizit materialisieren und nicht länger aus Dateipfaden
  ableiten.
- Deterministische, lesbare Markdown-, YAML- und Tabellenformatierung erzeugen.
- Suite für Suite migrieren und jeweils Fallzahl, Sources, Inputs,
  Expectations, Compileroptionen, Random-Sequenzen und Ausführungsergebnisse
  gegen den bisherigen Stand vergleichen.
- Die bisher zusammengefassten Performancefälle als einzelne H2-Tests mit
  eigener YAML-Baseline materialisieren; die Werte aus
  `PerformanceReport.reference.txt` in die jeweiligen Expectations überführen.
- Den kombinierten `PerformanceBinaryDump.reference.gesa` in eigenständige
  `bytecodeSnapshot`-Cases mit jeweils einem erwarteten `gesa`-Block aufteilen.
- Während der mechanischen Migration keine fachlichen Erwartungen ändern.
  Korrekturen erfolgen danach separat, damit Parser- und Semantikänderungen
  unterscheidbar bleiben.
- Nach bestätigter Parität die jeweilige JSON-Quelldatei entfernen; JSON und
  Markdown werden nicht dauerhaft parallel gepflegt.

### 5.7 Alte JSON-Conformance-Infrastruktur entfernen - DONE

Die ehemaligen JSON-Modelle, Discovery, Value-Codecs, Runner, dateispezifischen
MSTest-Klassen, der Einmal-Migrator und der superseded Pilot sind entfernt. Im
Testprojekt bleibt nur noch das normalisierte portable Markdownmodell mit einer
generischen Discovery über alle 34 Suites und 961 einzeln adressierbaren H2-Cases.
Die weiterhin benötigten C#-Extension- und External-Type-Bindings liegen nun in
einer reinen C#-Testumgebung und haben keine Abhängigkeit mehr von einem alten
Autorenformat.

Die fünf Performancefälle werden zusätzlich durch einen expliziten,
nicht-parallelen C#-Performanceadapter einzeln gemessen. Er verwendet die im
jeweiligen Markdownfall enthaltenen Profilgrenzen und erzeugt unter
`Conformance/Received/performance` einen kanonischen JSON-Report,
einen Markdown-Gesamtbericht und eine suite-benannte `.received.md` mit den gemessenen
Approval-Werten. Die fünf separaten Bytecode-Snapshot-Cases erzeugen analog eine
aggregierte Approval-Datei unter `Received/snapshots`. Kein Adapter überschreibt
die normative Suite automatisch.

Die 961 einzeln ausgeführten Case-Ergebnisse werden threadsicher gesammelt und
am Ende der MSTest-Klasse ohne erneute fachliche Ausführung als
`Received/ConformanceResults.json` und `Received/ConformanceReport.md`
geschrieben. Der frühere zusätzliche `RunCorpus` im Identitäts-Test ist
entfallen; dieser prüft nur noch Fallzahlen sowie eindeutige Suite- und Case-IDs.
Der Gesamtbericht führt Performance-Cases mit ihrem fachlichen Ergebnis, aber
ohne die deterministischen Platzhaltermetriken; reale Messwerte stehen nur im
separaten expliziten Performancebericht.

Der C#-Adapter nimmt pro Compile-, Load- und Run-Metrik drei voneinander
unabhängige Samples und vergleicht den schnellsten vollständigen Lauf. So bleiben
die kleinen Zeitmessungen gegenüber Scheduler-Ausreißern stabil; Allokationen
werden weiterhin aus genau diesem vollständigen Sample berichtet.

Die vier kombinierten `PerformanceReport.*.txt`- und
`PerformanceBinaryDump.*.gesa`-Dateien sind entfernt. Testdefinitionen stammen
damit ausschließlich aus Markdown; maschinenlesbare Resultate ausschließlich
aus dem kanonischen Conformance-JSON-Writer.

- Nach vollständiger Migration JSON-Modelle, JSON-Discovery und den temporären
  Importer/Migrator entfernen.
- Dateispezifische MSTest-Methoden durch generische Markdown-Discovery ersetzen.
- Die alten kombinierten Performance-Report- und Binary-Dump-Referenzdateien
  entfernen, sobald ihre Werte und Snapshots vollständig in den einzelnen
  Markdown-Cases enthalten sind.
- Sicherstellen, dass ausschließlich das normative Markdownformat zur
  Testdefinition und ausschließlich das definierte Ergebnisformat zur
  maschinellen Ausgabe verwendet werden.
- Den vollständigen migrierten Corpus, API-Snapshot, Nicht-Performance-Suite und
  Performance-Referenz verifizieren, bevor neue fachliche Fälle hinzukommen.

### 5.8 Conformance-Environment und fehlende Semantik ausbauen - DONE

Das portable Environment, Host-Szenarien und die öffentlichen sprachneutralen
API-Verträge sind jetzt ausdrückbar und getestet:

- Der Corpus umfasst 1.016 Fälle in 73 Suites: 1.009 semantische Fälle und
  sieben einzeln ausführbare GESA-Snapshots.
- Gegenüber dem ursprünglichen Bestand wurden 66 von 156 C#-Testmethoden nach
  Markdown migriert oder als Duplikate entfernt. Die verbleibenden 91 Methoden
  sind in `NativeTestRetention.md` einzeln nach Retention-Klasse begründet.
- `valueApi` prüft Value-Kinds, Reader/Flags, Einheiten, Equality/Hash,
  Containerkopien, skalare Map-Sortierung, Last-entry-wins, Records, Ranges und
  Message-Werte ohne GES-Ausführung.
- `messageApi` prüft zusätzlich Signature-, Message- und Handler-Equality samt
  Equal-Hash-Invariante und das Binden geordneter Werte an Signaturen.
- Deklarative Host-Aktionen können ihr boolesches Ergebnis erwarten. Damit sind
  `Detach` und `Unsubscribe` inklusive Idempotenz und bereits eingereihter
  Snapshots portabel abgedeckt.
- `externalTypeApi` deckt den portablen Katalog ohne Reflection ab. Zwei neue
  GESA-Snapshots ersetzen die bisherigen C#-Tests für kompakte Bindings,
  Message-name-Handler und eingebetteten Source im Dumper.
- Die Teststruktur besitzt nur noch `Conformance` und `Native` als fachliche
  Wurzeln. `Conformance` enthält Markdown, Fixtures, Reports und direkt in
  seinem Root die C#-Runner-/Parser-Adapter, damit IDEs sie als Conformance
  anzeigen. `Native` enthält die verbleibenden C#-spezifischen Tests für
  API-Surface, Binary-Format, Compiler, Core, Runtime und CSharpBridge.
- Alle normativen Suites folgen derselben lesbaren Gliederung aus Warnhinweis,
  Suite-Prosa, horizontal getrennten Testfällen, Test-Prosa und benannten
  Abschnitten für Case, Source, Steps, Expectation und GESA-Snapshot.
- Umfangreiche Matrizen sind in fachlich benannte Unter-Suites zerlegt. Keine
  einzelne normative Suite überschreitet 3.000 Zeilen; ein H2-Testblock wird
  dabei niemals zwischen Dateien geteilt.
- `ConformanceCoverage.md` ordnet das portable Verhalten stabilen Case-IDs zu.
  Ein indirekter zyklischer Callgraph bleibt bis 5.9 ein nativer
  Builder-/Validator-Test, weil er nicht aus gültigem Source erzeugt werden kann.

### 5.9 `.gesb`-Fixtures und Manifest ergänzen

- Gültige und gezielt beschädigte `.gesb`-Dateien einchecken.
- Eine ungültige Fixture mit indirektem zyklischem Callgraph und stabiler
  portabler Case-ID aufnehmen; danach den temporären Hinweis in
  `ConformanceCoverage.md` ersetzen.
- Ein Manifest mit Fixture-ID, relativem Pfad, Source, Compileroptionen,
  ProgramVersion, SHA-256 und erwarteten Read-, Validation- und
  Runtime-Ergebnissen führen.
- `.gesb` Read/Write/Validation einschließlich stabiler Formatfehler durch das
  portable Conformance-Format abdecken.
- Dieselben Fixture-Bytes in C#, Swift, Kotlin und C++ laden und mit identischen
  Runtime-Ergebnissen ausführen.
- Unterschiedliche BuildMetadata verschiedener Compiler zulassen; die Required
  Runtime-Segmente müssen semantisch identisch bleiben.
- Externe Fixture-Bytes ausschließlich über einen injizierten, begrenzten
  Resource-Resolver beziehen; Parser und Runner greifen nie selbst auf das
  Dateisystem oder Netzwerk zu.

### 5.10 Cross-Language-Abnahme vorbereiten

- C# erzeugt zunächst die Referenzergebnisse des gemeinsamen Markdown-Corpus.
- Jeder Port implementiert denselben Parser-, Dokumentmodell-, Runner- und
  Ergebnisvertrag und führt denselben Corpus aus.
- Gemeinsame Valid-/Invalid-Fixtures prüfen zusätzlich die identische
  Markdown-/YAML-Interpretation aller Parser.
- Eine Capability-Matrix zeigt implementierte optionale Bereiche und verbietet
  das Überspringen von Required-Core-Fällen.
- Ergebnisse werden über stabile Case-IDs statt Testframework-Namen verglichen.
- Performance-Workloads und deren Korrektheitsanteil gehören zum gemeinsamen
  Corpus; Performanceprofile und Baselines dürfen sprach- beziehungsweise
  plattformspezifisch sein.
- Bytecode-Snapshots bleiben für identische Compilerinputs und Optionen
  sprachübergreifend vergleichbar; Unterschiede in ausdrücklich
  sprachspezifischer BuildMetadata werden separat behandelt.

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
- ConformanceDocument, Suite und Case
- ConformanceParser, ParseResult, ParserLimits und ConformanceDiagnostic
- ConformanceRunner und ConformanceEnvironment
- CapabilitySet und ResourceResolver
- CaseResult, RunSummary und RunReport
- maschinenlesbarer ResultWriter und menschenlesbarer MarkdownReportWriter
- ReceivedWriter für Performance-Baselines und Bytecode-Snapshots

Die Conformance-Oberfläche gehört zunächst zum Namespace
`StepH.GameEventScript.Conformance` im bestehenden Core-Projekt. Ihre API muss
aber bereits so geschnitten sein, dass sie im Monorepo ohne konzeptionelle
Änderung in ein eigenes optionales Modul verschoben werden kann. Core-Host, VM,
Compiler und Programmodell dürfen nicht von Conformance abhängen; ausschließlich
Conformance hängt von den öffentlichen Core-APIs ab.

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

Für die Conformance-API zusätzlich ausdrücklich festhalten:

- Parser und Runner sind synchron, threadlos und besitzen keine File-, Netzwerk-
  oder Testframework-Abhängigkeit.
- Der Parser nimmt UTF-8-Bytes beziehungsweise Text entgegen und liefert nur ein
  vollständig validiertes immutable Dokument oder strukturierte Diagnostics.
- Markdown-/YAML-Zwischenbäume sind Implementierungsdetails und nicht Teil der
  öffentlichen API.
- ResourceResolver liefert externe Fixture-Bytes explizit und unter
  konfigurierbaren Limits; der Runner öffnet niemals selbst Pfade oder URLs.
- RunCase, RunDocument und RunCorpus sind getrennte Operationen. Ein
  Testframework kann jeden Case einzeln registrieren, während dieselben
  Ergebnisse zusätzlich zu einem Gesamtreport aggregiert werden können.
- ResultWriter und MarkdownReportWriter schreiben ausschließlich in vom
  Aufrufer bereitgestellte Ziele beziehungsweise Buffer.
- ReceivedWriter erzeugt nur einen Approval-Vorschlag und überschreibt niemals
  selbst das ursprüngliche Markdown-Dokument.
- Performanceprofile und Baselines dürfen sprach-/plattformbezogen sein;
  Workload, Korrektheit, Einheiten und Vergleichsregeln bleiben konzeptionell
  gleich.
- Öffentliche Modelle und Ergebnisse dürfen keine MSTest-, XCTest-, JUnit- oder
  sonstigen Framework-Typen enthalten.

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
