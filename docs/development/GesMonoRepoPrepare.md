<!-- Copyright 2026 Stephan Schlöpke -->
<!-- SPDX-License-Identifier: Apache-2.0 -->

## Ergebnis

Deine drei Punkte sind richtig, reichen aber noch nicht ganz. Vor allem fehlen sprachneutrale Verträge, ohne die Swift, Kotlin, C++ und C# trotz identischer Conformance-Fälle unterschiedlich reagieren könnten.

Wichtig: Der physische Umzug ins Monorepo muss nicht warten, bis alle Punkte fertig sind. Sinnvoll ist, zuerst Struktur und gemeinsame Verträge anzulegen und die weitere Portabilitätsarbeit anschließend direkt im Monorepo durchzuführen.

## 1. `.gesb`-Binary vollständig definieren - DONE

`.gesb` V1 ist spezifiziert und implementiert. Das normative Containerformat steht
in [BinaryFormat.md](../../specs/BinaryFormat.md); Opcode-Semantik und
Operandenformen stehen weiterhin in
[Bytecode.md](../../specs/Bytecode.md).

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
- Golden-, Invalid-, Retention-, Unicode-, Runtime- und Conformance-Roundtrip-Tests

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
[Text.md](../../specs/Semantics/Text.md)
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
- Markdown-Conformance deckt Supplementary-Plane-Zeichen, kombinierende Zeichen,
  Scalar-Sortierung und unzulässige Unicode-Namen/Whitespace ab.

### 2.2 Zahlen - DONE

Der sprachneutrale Vertrag ist in
[Numbers.md](../../specs/Semantics/Numbers.md)
festgeschrieben und in Compiler, VM, Value-Modell sowie Markdown-Conformance
umgesetzt:

- Int64-Operationen erkennen Überlauf ohne CLR-Checked-Kontext und wechseln bei
  Überlauf definiert auf Binary64; exakte Werte oberhalb `2^53` bleiben integer.
- Float-zu-Integer-Konvertierung nutzt explizite `2^63`-Grenzen, Truncation und
  Sättigung.
- NaN, Infinity, negative Null und alle Rundungsmodi sind festgelegt.
- `div`, `mod` und `rem` besitzen definierte Regeln für negative Operanden.
- Transzendente Funktionen verwenden die Plattform-Binary64-Mathematik mit
  konfigurierbarer ULP-Toleranz in Conformance-Tests; Runtime-Gleichheit bleibt bei zwei
  ULPs.
- Conformance-Floats verwenden kürzeste Roundtrip-Dezimaldarstellung mit
  kanonischem Exponenten statt eines C#-spezifischen festen Formats.
- Grenzfälle liegen sowohl als direkte Low-Level-Tests als auch als portable
  Markdown-Conformance vor.

### 2.3 Determinismus - DONE

Der sprachneutrale Vertrag ist in
[Determinism.md](../../specs/Semantics/Determinism.md)
festgeschrieben und durch Low-Level- sowie Markdown-Conformance-Tests abgesichert:

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
[Determinism.md](../../specs/Semantics/Determinism.md)
festgeschrieben und durch Low-Level- sowie Markdown-Conformance-Tests abgesichert:

- SplitMix64 initialisiert den xoshiro256**-Zustand aus dem vollständigen
  signed Int64 Seed; Zustandsübergang und Wrapping-Operationen sind normativ.
- Raw-UInt64-, rejection-sampled Integer- und exakte Binary64-Bitvektoren
  decken Seed `0`, `1`, `-1`, `Int64.MinValue`, `Int64.MaxValue` und Seeds
  oberhalb von 32 Bit ab.
- Der vollständige Int64-Bereich, vertauschte und identische Grenzen sowie deren
  exakter Stream-Verbrauch sind definiert und getestet.
- Gültige verschachtelte `random with`-Scopes setzen die jeweils äußere Sequenz
  anschließend exakt fort. Dynamische, statisch unbekannte Seeds verlangen eine
  explizite Konvertierung mit `as :number`. Ergibt diese zur Laufzeit keinen
  unitlosen exakten Int64, arbeitet der Body ohne Diagnose auf einer Kopie des
  aktuellen Random-Zustands; beim Verlassen wird der Parent exakt restauriert.
- Die Float-API heißt `NextFloat`: Sie skaliert eine `[0,1)`-Quelle auf die
  geordneten Bounds. Binary64-Rundung darf dennoch den oberen Bound erzeugen;
  auch dieser Fall ist mit einem exakten Bitmuster abgesichert.
- NaN- und identische Bounds verbrauchen weder einen PRNG- noch einen
  `FromSequence`-Wert.
- Jeder Host erzeugt aus Seed beziehungsweise kopierter Startsequenz einen
  eigenen Generator; mutable Generatorinstanzen werden nicht zwischen Hosts
  geteilt. Nach Ende einer Startsequenz übernimmt der private PRNG, in
  Conformance mit festem Fallback-Seed.
- `MaxRandomScopeDepth` begrenzt die tatsächlich erlaubten Scopes. Ein zusätzlich
  reservierter Overpush-Gate-State schützt den letzten gültigen Stream, bis der
  bestehende Handler-/Extension-Marker aufgeräumt wird. VM-Code stoppt sofort;
  atomare Extensions und native Handler werden ohne Exceptions bei ihrer
  Rückkehr als `RuntimeLimitReached` beendet.

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
- Markdown-Conformance verwendet einen manuellen Katalog, eine manuelle Registry und
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
- Markdown-Conformance verwendet einen manuellen portablen Handler; Native-only-
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
- Markdown-Conformance deckt unterschiedliche Reihenfolgen, leere Argumentlisten,
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
- Markdown-Conformance verwendet ausschließlich Phase plus Code und optionale
  strukturierte Felder. `messageContains` wurde aus portablen Fehlererwartungen
  entfernt; Runtime-Diagnosen sind als geordnete Observer-Ereignisse prüfbar.
- Der normative Vertrag steht in `../../specs/Diagnostics.md`.

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
  `../../specs/ProgramModel.md`.

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
  `../../specs/ProgramModel.md` und `../../specs/BinaryFormat.md` festgehalten.

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
- Der Vertrag ist in `../../specs/ProgramModel.md`, `../../specs/BinaryFormat.md` und
  `../../specs/Bytecode.md` festgehalten.

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
- Der Abschluss ist in `../../specs/ProgramModel.md` und `AGENTS.md` festgehalten.

## 5. Portable Conformance ausbauen - DONE

Der gemeinsame sprachneutrale Corpus liegt ausschließlich unter `conformance`.
YAML-Frontmatter enthält Suite-Einstellungen und Defaults; jedes H2 leitet einen
Fall ein. GES-Source steht in `ges`-Codeblöcken, Testmetadaten und Erwartungen
verwenden das normativ eingeschränkte YAML-Profil und geordnete Schritte werden
als definierte Markdown-Tabellen geschrieben. Freie Prosa, Notes und sonstige
Markdown-Inhalte bleiben nichtnormativ.

Semantische YAML-Blöcke verwenden ausschließlich den normalen Fence-Info-String
`yaml`, damit verbreitete Markdown-Highlighter sie zuverlässig erkennen. Das
Pflichtfeld `gesBlock` im Root-Mapping unterscheidet `case` von `expect`;
zusätzliche Fence-Tokens wie `yaml ges-case` gehören nicht zum Format.

Ein optionaler Abschnitt `## Fixtures` darf Werte und Matrizen zur
besseren Lesbarkeit dokumentieren, wird in V1 aber vollständig vom Runner
ignoriert. Falls später parameterisierte beziehungsweise Matrix-Tests daraus
erzeugt werden sollen, benötigen sie ein neues explizites und versioniertes
Konstrukt. Eine spätere Formatversion darf vorhandene dokumentierende Fixtures
nicht still in ausführbare Semantik umdeuten.

Der öffentliche synchrone Parser und Runner sind von Dateisystem, Netzwerk,
MSTest und C#-spezifischen Testmodellen unabhängig. Stabile Suite-/Case-IDs,
Capabilities, Skip-Regeln, Ergebnisse, Reports, Performance-Referenzen und
Received-Updates besitzen einen normativen portablen Vertrag. Neue Semantik wird
ausschließlich im Markdown-Corpus ergänzt.

### 5.1 Markdown- und Runner-Vertrag normativ spezifizieren - DONE

Der Autorenvertrag steht normativ in
[MarkdownFormat.md](../../specs/Conformance/MarkdownFormat.md), der
Ausführungs-, Capability-, Ergebnis-, Report- und Received-Vertrag in
[Runner.md](../../specs/Conformance/Runner.md).

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

- `../../specs/Conformance/MarkdownFormat.md` als normative Beschreibung des Autorenformats
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
- `../../specs/Conformance/Runner.md` für Testarten, Capabilities, Skip-Regeln,
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

Alle elf Testarten werden ausgeführt: Script-API einschließlich Initialisierung,
Frames, nativen Handlern und Local/Outbound-Beobachtung; Compile- und
Loadfehler; Message-, Value- und External-Type-API; Compile-Metadaten;
Opcode-Constraints; Bytecode-Snapshots; portable Program-Binaries sowie
Performance nach vorheriger Korrektheitsprüfung.
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

### 5.5 Vertikalen Markdown-Vertrag absichern - DONE

Der portable Parser und Runner decken jede Testart vertikal ab: `scriptApi`,
`compileError`, `loadError`, `messageApi`, `compileMetadata`, `bytecode`,
`performance` und `bytecodeSnapshot`. Frontmatter-Vererbung, lokale Overrides,
Binary-Roundtrip, benannte Sourceinputs, Steps, Diagnosen, Ressourcenmetadaten,
Opcodebedingungen, Performanceprofile und exaktes GESA werden gemeinsam
geprüft. Neue Conformance-Fälle werden ausschließlich als Markdown angelegt.

### 5.6 Normativen Markdown-Corpus materialisieren - DONE

Alle Cases besitzen explizite, vom Anzeigetitel unabhängige IDs und ein
aufgelöstes `atomic`/`scenario`-Level. Sourceinputs, getrennte Programs, Steps,
native Handler, Runtime-Limits, Diagnosen, Random-Sequenzen,
Binary64-Vergleich und fachliche Expectations liegen vollständig im
Markdownmodell vor. Maps verwenden die normative geordnete Entry-Form und
numerische Werte die definierte shortest-roundtrip Binary64-Darstellung.

Die Performancefälle enthalten portable KiB-/Millisekunden-Baselines und
jeweils einen eigenen `bytecodeSnapshot`. Suite-/Case-Eindeutigkeit sowie die
vollständige Ausführung werden durch die generische Corpus-Testklasse geprüft.

### 5.7 Einheitliche Conformance-Infrastruktur herstellen - DONE

Das Testprojekt verwendet ausschließlich das normative Markdownmodell und die
generische Discovery. Die weiterhin benötigten C#-Extension- und
External-Type-Bindings liegen in der C#-Testumgebung und haben keine
Abhängigkeit von einem weiteren Autorenformat.

Die fünf Performancefälle werden durch einen expliziten, nicht-parallelen
C#-Performanceadapter gemessen. Er verwendet die im jeweiligen Markdownfall
enthaltenen Profilgrenzen und erzeugt unter `Conformance/Received/performance`
einen kanonischen JSON-Report, einen Markdown-Gesamtbericht und eine
suite-benannte `.received.md`. Bytecode-Snapshot-Cases erzeugen analog
Approval-Dateien unter `Received/snapshots`. Kein Adapter überschreibt eine
normative Suite automatisch.

Die unabhängig ausgeführten Case-Ergebnisse werden threadsicher gesammelt und
ohne erneute fachliche Ausführung als `Received/ConformanceResults.json` und
`Received/ConformanceReport.md` geschrieben. Der Identitätstest prüft
Fallzahlen sowie eindeutige Suite- und Case-IDs.

### 5.8 Conformance-Environment und fehlende Semantik ausbauen - DONE

Das portable Environment, Host-Szenarien und die öffentlichen sprachneutralen
API-Verträge sind jetzt ausdrückbar und getestet:

- Der Corpus umfasst 1.029 Fälle in 74 Suites: 1.022 semantische Fälle und
  sieben einzeln ausführbare GESA-Snapshots.
- Die verbleibenden 84 C#-Testmethoden sind in `NativeTestRetention.md` nach
  Retention-Klasse begründet: 38 bootstrappen die Conformance-Infrastruktur,
  46 prüfen absichtlich sprach- oder implementationsspezifisches Verhalten.
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
- `../../specs/Conformance/Coverage.md` ordnet das portable
  Verhalten stabilen Case-IDs zu.

### 5.9 `.gesb`-Fixtures und Manifest ergänzen - DONE

`program.binary-format` ist das ausführbare portable Manifest für 13 immutable
`.gesb`-V1-Ressourcen. Jeder Fall enthält stabile Fixture- und Resource-IDs,
Paketpfad, Source/Compileroptionen, Compiler-ID/-Version, ProgramVersion,
SHA-256, optionale Ableitung sowie genaue Read-, Validation-, Rewrite- und
gegebenenfalls Runtime-Erwartungen.

Vier gültige Fixtures decken kanonische Runtime-/Debug-Programme, opaque
optionale Sections und nichtkanonische Section-Reihenfolge ab. Neun gezielt
beschädigte Fixtures decken Magic, Truncation, fehlende/doppelte Sections,
UTF-8, unbekannte Required Sections, reservierte Kompression, ungültige
Referenzen und einen aus gültigem Source nicht erzeugbaren indirekten
Call-Zyklus ab. Der stabile Zyklusfall ist
`program.binary-format/invalid-indirect-call-cycle`.

Der neue öffentliche `programBinary`-Falltyp unterscheidet strukturelle
`readError`- von semantischen `validationError`-Ergebnissen. Der Runner prüft
die Ressourcenidentität vor dem Reader, validiert ProgramVersion und optionale
Metadaten, schreibt gültige Programme vollständig neu und kann sie anschließend
über die normale Host-Step-Pipeline ausführen. BuildMetadata bleibt reine
Provenienz; die portable Runtime hängt nicht davon ab.

`IConformanceResourceResolver` erhält nur Resource-ID und hartes
`MaxResourceBytes`; der portable Parser und Runner interpretieren niemals
Pfade und führen kein Datei- oder Netzwerk-I/O aus. Der C#-Adapter löst die im
Manifest validierten relativen Pfade innerhalb des Fixture-Roots auf. Spätere
Swift-, Kotlin- und C++-Runner verwenden exakt dieselben Dateien und IDs statt
sprachspezifischer Kopien; ihre gemeinsame Ausführung wird unter 5.10
abgenommen.

Redundante native Read-/Validation-/Roundtrip-/Runtime-Tests wurden entfernt.
Die verbleibenden 16 `.gesb`-/Program-Tests prüfen ausschließlich direkte
C#-Implementierungsdetails und bewusst getrennte Retention-/Limitpfade.

Abnahme:

```text
1029/1029 Markdown-Conformance-Fälle bestanden
1125/1125 Nicht-Performance-Testausführungen bestanden
6/6 Performance-/Allokationstests im Bestätigungslauf bestanden
```

### 5.10 Cross-Language-Abnahme vorbereiten - DONE

Erledigt:

- `../../specs/Conformance/CrossLanguageAcceptance.md` definiert den bytegenauen Corpus-Fingerprint,
  die Cross-Language-Vergleichsregeln und die vollständige Capability-Policy.
- `ConformanceCrossLanguageResultJsonWriter` erzeugt aus einem vollständigen
  Corpus-Report eine kleine kanonische Projektion. Sie enthält ausschließlich
  Corpus-Identität, stabile Case-ID, Kind, Level, Core-/Optional-Anforderungen,
  Status und stabilen Code und ist unabhängig von Framework und Discovery Order.
- C# erzeugt ohne zweiten Corpus-Lauf einen Received-Kandidaten und vergleicht
  ihn bytegenau mit `CrossLanguage/CSharpReferenceResults.json`.
- Die gemeinsamen Valid-/Invalid-Parser-Fixtures besitzen ein simples
  `manifest.tsv` mit exakten SHA-256-Werten und erwarteter normalisierter
  Identität beziehungsweise stabilem ersten Fehlercode.
- `CapabilityMatrix.md` trennt Core von optionalen Fähigkeiten. Fehlender Core
  bleibt ein nicht akzeptierter Error; nur ehrlich fehlende optionale
  Capabilities dürfen die genau betroffenen Fälle überspringen.
- Performance-Korrektheit und Bytecode-Snapshots bleiben Teil des gemeinsamen
  Corpus. Sprach-/plattformabhängige Messwerte und BuildMetadata sind nicht Teil
  der kompakten semantischen Projektion.

Abnahme:

```text
Corpus-Fingerprint BB8FFCBBD98EBF330D6ADBBBFFA7DACFEF885CD9B40DAB44231D329DCE98B856
1029/1029 Markdown-Conformance-Fälle bestanden
1128/1128 Nicht-Performance-Testausführungen bestanden
6/6 Performance-/Allokationstests im Bestätigungslauf bestanden
```

## 6. Normative Dokumentation und öffentliche sprachneutrale API - DONE

Vor der eigentlichen API-Spezifikation werden Sourcecode und Dokumentation auf eine eindeutige, für die Sprachports geeignete Grundlage gebracht. Die neue normative Dokumentation beschreibt ausschließlich den gültigen Ist- und Sollvertrag; Entwicklungshistorie, Migrationen und ersetzte Architekturen gehören nicht hinein. `GameEventScript.Memory.md` bleibt außerhalb der normativen Dokumentation als historische Wissensquelle erhalten.

### 6.1 Formatierungsvertrag festlegen - DONE

Erledigt:

- Die gemeinsame `.editorconfig` enthält ausschließlich Sections für `StepH-GameEventScript` und `StepH-GameEventScript-Tests`; andere Services im Workspace bleiben unbeeinflusst.
- `CodeStyle.md` definiert den verbindlichen Entwicklungsvertrag für handgeschriebenen C#-Source.
- Die maximale Zeilenlänge beträgt 250 Zeichen. Deklarationen, Aufrufe und Konstruktoren bleiben einzeilig, solange sie vollständig hineinpassen und lesbar bleiben; eine feste Argumentanzahl erzwingt keinen Umbruch.
- Notwendige mehrzeilige Parameter-, Argument-, Initializer- und Fluent-Chain-Formen sowie typische C#-Einrückungs-, Klammer-, Spacing- und `using`-Regeln sind festgelegt.
- Der Vertrag unterscheidet ausdrücklich zwischen Editor-/Roslyn-Regeln und der zusätzlich mechanisch zu prüfenden Zeilenlänge.
- `AGENTS.md` verweist auf diesen Vertrag, damit spätere Änderungen und Sprachport-Arbeiten dieselben Regeln verwenden.

Die vorhandenen C#-Dateien wurden in diesem Schritt bewusst noch nicht verändert; ihre mechanische Reformattierung ist Gegenstand von 6.2.

Abnahme:

```text
EditorConfig wurde von dotnet format für eine isolierte Produktionsdatei fehlerfrei geladen.
Ausgangsbestand für 6.2: 76 Produktions- und 10 Testzeilen sind länger als 250 Zeichen.
```

### 6.2 Sourcecode mechanisch reformattieren - DONE

Erledigt:

- Alle handgeschriebenen `.cs`-Dateien in `StepH-GameEventScript` und `StepH-GameEventScript-Tests` wurden mit Roslyn nach dem Vertrag aus 6.1 formatiert; dazu gehört auch die kanonische `using`-Reihenfolge.
- Unnötig fragmentierte Deklarationen wurden wieder zusammengezogen, wenn die vollständige Deklaration einschließlich Einrückung innerhalb von 250 Zeichen bleibt.
- Die zuvor vorhandenen 76 Produktions- und 10 Testzeilen über 250 Zeichen wurden kontrolliert und ohne fachliche Änderung umgebrochen. Eingebettete Markdown-Testdaten behalten dabei ihren exakten Laufzeitinhalt.
- Generierte Ausgaben, Golden Files, Markdown-/GESA-Snapshots und Binärfixtures wurden nicht als C#-Source formatiert.
- Beide Projekte bestehen `dotnet format --verify-no-changes`; keine handgeschriebene C#-Zeile überschreitet 250 Zeichen.
- API-Snapshot, vollständige Markdown-Conformance und alle übrigen Nicht-Performance-Tests sowie Performance-/Allokationsreferenzen sind unverändert erfolgreich.

Abnahme:

```text
1128/1128 non-performance test executions passed
6/6 performance/allocation tests passed
0 C# source lines exceed 250 characters
```

Die bereits bekannten XML-Dokumentationswarnungen für `GesValueMap` bleiben unverändert und gehören nicht zu diesem mechanischen Formatierungsschritt.

### 6.3 Normative Dokumentationsstruktur anlegen - DONE

Erledigt:

- Unter `StepH-GameEventScript/Documentation` wurde die eindeutige Zielstruktur angelegt:

```text
Documentation/
  README.md
  Specification/
    Language.md
    PublicApi.md
    HostRuntime.md
    ProgramModel.md
    Bytecode.md
    BinaryFormat.md
    AssemblerFormat.md
    Diagnostics.md
    Semantics/
      Text.md
      Numbers.md
      Determinism.md
    Conformance/
      MarkdownFormat.md
      Runner.md
      Environment.md
      CrossLanguageAcceptance.md
  Guide/
    README.md
```

- `Documentation/README.md` ist der zentrale Einstieg und ordnet jeden Vertragsbereich genau einem verantwortlichen Dokument zu; sämtliche aufgeführten Dokumente sind von dort erreichbar.
- Jedes Zieldokument besitzt bereits eine eindeutige Scope- und Abgrenzungsbeschreibung mit Links auf die jeweils zuständigen Nachbardokumente.
- Die neuen Spezifikationsdateien sind bis zur inhaltlichen Überführung ausdrücklich als `Structural draft` markiert und erheben noch keinen unvollständigen normativen Anspruch.
- `Guide/README.md` definiert Lernmaterial ausdrücklich als nicht normativ und verweist für exaktes Verhalten auf den Spezifikationsindex.
- Arbeitsdokumente, Testinventare, Backlogs, Handoffs, Editor-Dokumente und `GameEventScript.Memory.md` bleiben außerhalb der normativen Struktur.
- Sämtliche bisherigen technischen Quelldokumente bleiben bis 6.4 beziehungsweise 6.5 unverändert an ihrem bisherigen Ort; dieser Schritt hat keine Inhalte vorzeitig verschoben oder entfernt.
- Dateinamen, Verzeichnisse und relative Verlinkung sind bereits ohne konzeptionelle Umbenennung in das Monorepo übernehmbar.

Abnahme:

```text
17 documentation entry/specification/guide files created
All internal Documentation links resolve
All pre-existing source documents remain available for 6.4/6.5
```

### 6.4 Bestehende technische Spezifikationen überführen - DONE

Erledigt:

- Die technischen Verträge wurden an ihre endgültigen Orte unter `../../specs` überführt und dort als normativ markiert. `Language.md` und `PublicApi.md` bleiben bis 6.5 beziehungsweise 6.6 die einzigen strukturellen Entwürfe.
- Die bisherigen Bytecode-Dokumente wurden in `Specification/Bytecode.md` zusammengeführt. Das Dokument definiert Instruction-Layout, vollständige Opcode-IDs und Operandenformen, Flags, Units, Kontrollfluss, Calls, Validierung und Ausführungssemantik.
- `.gesb`-Container, Sections, Little-Endian-Encoding, Reader-Retention und Formatvalidierung liegen ausschließlich in `Specification/BinaryFormat.md`.
- `Specification/AssemblerFormat.md` wurde anhand des Dumpers und der Conformance-Snapshots als vollständiger Vertrag für das menschenlesbare `.gesa`-Format erstellt. Er umfasst Dokumentdirektiven, Segmente, Regions, Source-Zeilen, Labels, symbolische Register und sämtliche Operandformen.
- Host-State-Machine und Laufzeitverantwortlichkeiten liegen in `HostRuntime.md`, Program-Ownership und Serialisierbarkeit in `ProgramModel.md`, Diagnostics in `Diagnostics.md` und die portablen Detailsemantiken unter `Semantics`.
- Conformance-Autorenformat, Runner, feste Umgebung und Cross-Language-Abnahme wurden in die vier Conformance-Spezifikationen überführt.
- Alle aktiven Verweise wurden auf die endgültigen Pfade umgestellt. Die vollständig abgelösten technischen Root-Dokumente wurden entfernt; es gibt keine Redirect-, Legacy- oder Historienkapitel in den normativen Zieldokumenten.
- `GameEventScript.md` bleibt ausschließlich als Wissensquelle für 6.5 bestehen. `GameEventScript.Memory.md` bleibt als historische Aufzeichnung unverändert außerhalb der normativen Struktur.
- Punkt 7 bleibt für die spätere maschinenlesbare Opcode-Quelle verantwortlich. Die aktuelle manuelle Opcode-Tabelle ist vollständig und wurde mechanisch gegen den öffentlichen Enum geprüft.

Abnahme:

```text
17/17 documentation entry/specification/guide files present
All Documentation and active Markdown links resolve
199/199 opcode IDs match the public opcode enum
9/9 concrete .gesb runtime/debug/build section IDs match the public section enum
All GESA forms emitted by GameEventScriptProgramDumper are specified
All documentation code fences are balanced
1128/1128 non-performance test executions passed
```

### 6.5 Vollständige Sprachspezifikation erstellen - DONE

Erledigt:

- `../../specs/Language.md` definiert den aktuellen Sprachumfang normativ und ohne Implementierungs- oder Migrationshistorie. Parser, Compiler, Runtime-Semantik, alter Sprachtext und Markdown-Conformance wurden dafür gemeinsam abgeglichen.
- Die Spezifikation umfasst Compilation Units, Deklarationen, portable Lexik und Unicode-Verweise, Literale, Scopes, Callables, azyklische Calls, Handler und Dispatch, Statements, vollständige Operatorpräzedenz, Typen, Casts, Truth-Semantik, Werte, Records, Collections und Pipelines, Randomness, Dice, Series, Extensions sowie Fehler- und Limitverhalten.
- Detailverträge für Unicode, Zahlen, deterministische Reihenfolge und PRNG bleiben ausschließlich in den zuständigen Semantikdokumenten; Host-, Program-, Bytecode- und Binary-Verantwortlichkeiten sind klar abgegrenzt und verlinkt.
- Die korrigierte normative Grammatik steht direkt im Sprachdokument. Sie verwendet die von JetBrains Grammar-Kit verstandene erweiterte BNF-Ausdruckssyntax für korrektes IntelliJ-Highlighting, ohne Parsergenerierung oder PEG-Konfliktregeln zum Sprachvertrag zu machen. Sie beschreibt denselben Umfang wie die Prosa, einschließlich Intrinsics, Selectorformen und kontextabhängiger Message-/Handler-Literale.
- Die bereits lexikalisch erzwungene portable Typnamensyntax besitzt nun eine explizite Markdown-Conformance-Abdeckung für die Abgrenzung gegenüber Identifiern mit numerischem Suffix.
- Funktionen und Predicates werden über Name plus geordnete externe Labels aufgelöst; Typangaben und lokale Parameternamen erzeugen keine Overloads, und beide Callable-Arten dürfen keinen Namen teilen. Lexikalisches Shadowing sichtbarer Vorfahren ist für lokale, Loop-, Generator- und Selector-Bindings verboten, während `if`-/`else`-Geschwisterscopes denselben neuen Namen verwenden dürfen.
- Das frühere `GameEventScript.md` und die separate, abweichende `GameEventScript.bnf` wurden nach der Überführung entfernt. Lernmaterial bleibt ausdrücklich Aufgabe des getrennten `Guide`-Bereichs.

Abnahme:

```text
All Documentation links resolve
All Markdown code fences are balanced
Record type-name grammar has executable Markdown conformance coverage
1037/1037 Markdown conformance cases passed
1138/1138 non-performance test executions passed
```

### 6.6 Öffentliche portable API spezifizieren - DONE

Erledigt:

- `../../specs/PublicApi.md` ist nun der normative, sprachneutrale Vertrag für Compiler, Programmodell, Bytecode-Daten, `.gesb`-Codec, Messages, Values, Host, Context, Lifecycle, Randomness, Extensions, External Types und Diagnostics.
- Die Conformance-Oberfläche ist vollständig als getrennte konzeptionelle Modulgrenze beschrieben: Parser, immutable normalisiertes Modell, Environment, Capabilities, Resolver, Runner, Ergebnisse, Reports, Performance Provider, Received Writer und Cross-Language Writer.
- Für die öffentlichen Familien sind Verantwortung, Ownership, Copy-/Reference-Semantik, Immutability, Nullability, Argumentfehler, synchrone Ausführung, Threading, Reentrancy, Callback-Reihenfolge, Lifecycle-Idempotenz und relevante Allokationsanforderungen festgelegt.
- Core ist normativ synchron, threadlos und unsynchronisiert. Ein Host ist seriell, aber nicht thread-affin; Reflection, Delegates, Locks, Tasks, Filesystem-Helfer und Dictionary-Conveniences bleiben sprachspezifische Adapter.
- Publish-Sink-Exceptions werden vom Host in eine Runtime-Diagnostic überführt und beschädigen lokales Enqueue nicht. Observer und Conformance-Result-Sinks dürfen nicht werfen; ein Verstoß liegt ausdrücklich außerhalb der Host-/Runner-Recovery-Garantien.
- `GameEventScriptProgram` bleibt ausschließlich durch Compiler oder Reader öffentlich erzeugbar und transportiert nur `.gesb`-darstellbare Daten. Writer, Validator, Dumper und Reader besitzen getrennte, klar benannte Trust- und I/O-Grenzen.
- Die C#-, Swift-, Kotlin-, C++- und Unity-Abbildungen müssen semantisch gleich, aber nicht namens- oder typformgleich sein. Das derzeit colocated Conformance-Package kann ohne konzeptionelle API-Änderung in ein optionales Monorepo-Modul verschoben werden.
- `Documentation/README.md` führt nun alle Spezifikationen, einschließlich Public API, als normativ; es verbleibt kein struktureller Dokumententwurf.

Abnahme:

```text
PublicApi.md covers every portable public API family from the approved C# surface
Core <- Conformance dependency direction is explicit
All public API cross-references resolve
All specification documents are normative
1148/1148 non-performance test executions passed
6/6 explicit performance tests passed
```

### 6.7 Öffentliche XML-API-Dokumentation vervollständigen - DONE

Erledigt:

- Sämtliche öffentlich sichtbaren C#-Typen und Member in API, Runtime, `CSharpBridge` und Conformance besitzen XML-Dokumentation; der Compiler exportiert weiterhin keine öffentlichen Typen. Vorhandene fachliche Kommentare wurden bewahrt und zentrale Eingangs-APIs gegen `Specification/PublicApi.md` inhaltlich geschärft.
- Host, Context, Program-Codec, Lifecycle, Publish, Observer, C#-Auto-Runner sowie Conformance-Parser und -Runner dokumentieren die jeweils relevanten Ownership-, Lebensdauer-, Nullability-, Seiteneffekt-, Synchronitäts-, Reentrancy- und Fehlerverträge. Die XML-Kommentare bleiben die konkrete C#-Abbildung; die sprachneutrale Norm liegt weiterhin ausschließlich in `PublicApi.md`.
- Das Hauptprojekt erzeugt bei jedem Build `StepH.GameEventScript.xml`. Fehlende öffentliche Dokumentation sowie ungültige XML-Struktur, Parameter-, Typparameter- und `cref`-Angaben sind als Buildfehler konfiguriert statt unterdrückt zu werden.
- Alle handgeschriebenen `#pragma`-Direktiven wurden aus Produktions- und Testcode entfernt. Damit sind insbesondere die früher unterdrückten `GesValueMap`-Dokumentationswarnungen tatsächlich behoben.
- Der API-Surface-Test sichert das erzeugte XML-Artefakt und dessen Abdeckung aller exportierten Typen, die vollständige Build-Warnschranke und die Pragma-Freiheit beider handgeschriebener C#-Projektbäume mechanisch ab.
- `Receive(GameEventScriptMessage)` prüft den dokumentierten erforderlichen Parameter nun explizit und liefert bei `null` denselben synchronen Argumentfehler wie die Context-Messaging-Operationen.

Abnahme:

```text
1585 public XML documentation member entries generated
0 hand-written C# pragma directives
0 XML documentation warnings
1149/1149 non-performance test executions passed
6/6 performance/allocation tests passed
dotnet format --verify-no-changes passed for production and tests
```

### 6.8 Apache-2.0-Lizenz und Copyright-Header einführen - DONE

Erledigt:

- Der Rechteinhaber ist dauerhaft als natürliche Person `Stephan Schlöpke` festgelegt. Die kanonische UTF-8-Schreibweise mit `ö` wird nicht parallel durch eine ASCII-Identität ersetzt; eine aktuelle oder spätere freiberufliche Geschäftsbezeichnung ist bewusst nicht Teil des stabilen Copyright-Hinweises.
- `2026` ist das feste Erstveröffentlichungsjahr. Es wird weder beim Build noch bei späteren Routineänderungen automatisch fortgeschrieben oder in einen rollenden Zeitraum umgewandelt.
- `StepH-GameEventScript/LICENSE` enthält bytegenau den unveränderten offiziellen Apache-License-2.0-Text. `LICENSING.md` definiert Geltungsbereich, kanonische Kurzheader, Jahreskonvention, technisch begründete Ausnahmen und den Umgang mit künftigem Drittmaterial.
- Alle handgeschriebenen C#-Quellen in Produktion und Tests, reguläre Projekt-/Spezifikations-/Guide-/README-Markdowns, beide C#-Projektdateien, die GES-spezifische EditorConfig und Gitignore sowie XML-basierte TextMate-Dateien tragen passende Copyright- und SPDX-Header.
- Streng geparste Conformance-Suites und Parser-Fixtures, Golden-/Received-/Report-Dateien, JSON/TSV ohne Kommentarsyntax, `.ges`-Testinputs, `.gesb`-Fixtures, Build-/Testergebnisse und Betriebssystemmetadaten bleiben absichtlich unverändert. Dadurch wurden keine Corpus-Hashes, GESA-Snapshots oder deterministischen Binary-Fixtures aktualisiert.
- Im aktuellen Sourcebestand wurde kein vendortes Drittmaterial mit eigener Attributionspflicht identifiziert. Paketabhängigkeiten bleiben externe NuGet-Referenzen. Deshalb wird derzeit bewusst keine leere oder erfundene `NOTICE`-Datei erzeugt; künftiges Drittmaterial muss vor Aufnahme separat geprüft werden.
- `StepH-GameEventScript.csproj` veröffentlicht Autor, Copyright, Beschreibung, `PackageLicenseExpression=Apache-2.0` und Package-README. Das erzeugte NuGet-Paket enthält `LICENSE`, `README.md`, Assembly und XML-Dokumentation.
- Drei mechanische Lizenztests prüfen den offiziellen Lizenz-SHA-256, die bewusste `NOTICE`-Entscheidung, Paketmetadaten und Paketdateien sowie sämtliche vereinbarten Headerbereiche und Ausnahmen.

Abnahme:

```text
LICENSE SHA-256 cfc7749b96f63bd31c3c42b5c471bf756814053e847c10f3eb003417bc523d30 matches apache.org
NuGet package contains LICENSE, README.md, assembly, and XML documentation
All XML TextMate assets pass plutil validation
1152/1152 non-performance test executions passed
6/6 performance/allocation tests passed
dotnet format --verify-no-changes passed for production and tests
```

### 6.9 Gesamtkonsistenz und Vollständigkeit prüfen - DONE

Erledigt:

- `Documentation/README.md` indiziert alle 16 normativen
  Spezifikationsdokumente genau einmal. Sämtliche lokalen Links innerhalb der
  Dokumentationshierarchie werden mechanisch auf ein vorhandenes Ziel geprüft.
- Der bisher außerhalb liegende normative `ConformanceCoverage.md` wurde nach
  `../../specs/Conformance/Coverage.md` verschoben. Außerhalb
  der Dokumentationshierarchie verbleiben nur Projekt-/Lizenz-Einstiege,
  Entwicklungsregeln, Implementierungs- und Testinventare, Editorunterlagen,
  ausführbare Testdokumente, generierte Reports und die historische Memory-Datei.
- `PublicApi.md` legt nun ausdrücklich fest, dass jeder exportierte C#-Typ genau
  einer Completeness-Map-Zeile gehört und alle deklarierten Operationen die
  Zuordnung ihres Typs erben. API-Snapshot, erlaubte öffentliche Namespaces,
  Completeness Map und vollständige XML-Dokumentation bilden gemeinsam das
  dauerhafte Gate.
- Die kanonische Opcode-Tabelle wurde vollständig gegen alle 199 öffentlichen
  Opcode-Namen und -IDs geprüft. Die Section-Registry benennt nun auch die
  reservierten IDs `0x0040` und `0xFFFE` eindeutig und stimmt für alle 11
  öffentlichen Section-Werte mit der Implementierung überein. Alle 38
  `.gesb`-Formatfehler sind ebenfalls mechanisch abgeglichen.
- Sämtliche stabilen Parse-, Validate-, Compile-, Decode-, Link- und
  Runtime-Diagnostic-Codes sind in `Diagnostics.md` normativ auffindbar. Die
  zuvor nur verkürzt beschriebenen Conformance-Parser-, Runner-, Resource- und
  Received-Writer-Codes sind nun vollständig ausgeschrieben und werden gegen
  die öffentlichen Konstanten geprüft.
- Jedes vom Lexer erkannte reservierte ASCII-Wort muss als Terminal in der
  normativen BNF erscheinen. Parser/Compiler-Verhalten, `.gesb`-Fixtures,
  GESA-Snapshots, portable Semantik und Cross-Language-Projektion bleiben durch
  den vollständigen Markdown-Corpus ausführbar abgesichert.
- Fünf neue Dokumentations-Konsistenztests halten Index/Links, API-Ownership,
  Opcode-/Binary-Tabellen, Diagnostic-Codes und Lexer-/Grammatik-Vokabular
  dauerhaft synchron. Lizenz- und Headervertrag bleiben durch die drei Tests
  aus 6.8 Bestandteil derselben vollständigen Abnahme.

Abnahme:

```text
5/5 documentation consistency tests passed
1040/1040 Markdown conformance cases and the corpus/cross-language audit passed
1157/1157 non-performance test executions passed
6/6 performance/allocation tests passed
0 handwritten C# pragmas and 0 C# source lines over 250 characters
dotnet format --verify-no-changes passed for production and tests
```

### Ergänzende Sprach- und Transportnamensbereinigung - DONE

Nach Abschluss von Punkt 6 wurde die Namens- und Typoberfläche vor dem
Monorepo-Port nochmals vereinheitlicht:

- `constant $name be LITERAL` definiert programweite skalare
  Compile-Time-Konstanten. Sie werden vollständig inlined und erzeugen weder
  Bindings noch eigene `.gesb`-Daten oder Runtime-State.
- Die alte Form `let name as :Type be expression` ist entfernt. Casts stehen
  ausschließlich im Ausdruck: `let name be expression as :Type`.
- Source-Typen und Typkonstruktoren sind PascalCase; dazu gehört auch
  `:List[...]`. Selector- und Extension-Namen bleiben lowercase.
- Module verwenden dot-separierte lowercase Komponenten mit optionalen Ziffern.
  Allgemeine Namen dürfen Ziffern nach dem ersten Zeichen enthalten. Nur lokale
  Variablen dürfen zusätzlich den kanonischen Suffix `_number` tragen.
- Die Conformance-Transporttypen unterscheiden exakte Speicherung durch
  `:Number.int64`/`:Number.binary64`, entsprechende Quantity-/Range-Varianten
  und PascalCase für alle übrigen Kinds. `rangeKind` ist entfallen.
- Lexer, Parser, AST-Validierung, `.gesb`-Validierung, Debug-/GESA-Snapshots,
  Golden Binaries, Markdown-Conformance, TextMate-Bundles und normative
  Spezifikationen wurden gemeinsam aktualisiert.

Abnahme:

```text
1049/1049 Markdown conformance cases passed
1166/1166 non-performance test executions passed
5/5 explicit Markdown performance tests passed
1/1 zero-allocation hot-path test passed
Corpus-Fingerprint 87677320758639E5B213474857AD3E82E2E68A5F50A12063F762D6EAB6347637
dotnet format --verify-no-changes passed for production and tests
```

## 7. Monorepo-Migration und Packaging-/Integrationsschnitt - DONE

Das eigenständige GitHub-Monorepo ist die einzige aktive Source of Truth für
Game Event Script. Die übernommene PlasticSCM-Historie bleibt außerhalb dieses
Repositories als historischer Produktkontext erhalten. Beamable- und
Unity-Bereinigung am ehemaligen Standort sind Aufgaben des jeweiligen Produkts
und keine Abhängigkeit der portablen Library.

Die tatsächliche Monorepo-Struktur ist:

```text
/specs
/conformance
/implementation/csharp
  /src/StepH.GameEventScript
  /src/StepH.GameEventScript.CSharpBridge
  /src/StepH.GameEventScript.Conformance
  /tests
  /tools
/docs
/scripts
/tools/editors
/.github/workflows
```

- `specs` besitzt die normative sprachneutrale Dokumentation genau einmal.
- `conformance` besitzt den gemeinsamen Markdown-Corpus, `.gesb`- und
  Parser-Fixtures sowie die Cross-Language-Referenz genau einmal.
- Die portable C#-Assembly enthält Core, Compiler, Host und Runtime ohne
  Beamable-, Unity-, Reflection-, Threading- oder JSON-Abhängigkeit.
- `CSharpBridge` kapselt Reflection, Delegates, CLR-Konvertierung und den
  optionalen automatischen Host-Runner in einer getrennten Assembly und einem
  getrennten Package.
- Der portable Conformance-Parser und -Runner bilden eine dritte, unabhängig
  verschiebbare Assembly und ein eigenes Package.
- Unity konsumiert zunächst die reproduzierbar gestagten C#-DLLs. Es existiert
  keine leere oder ungeprüfte Unity-/Beamable-Integration im Monorepo.
- Weitere Sprach- und Integrationsverzeichnisse entstehen erst mit einer realen
  Implementierung. Entsprechende Folgearbeiten stehen in
  [BACKLOG.md](../../BACKLOG.md).
- Build-, Test-, Pack-, Direct-DLL-, Package-Consumer- und
  Reproduzierbarkeitsabläufe sind durch Root-Skripte ausführbar; erzeugte
  Artefakte liegen ausschließlich im ignorierten `artifacts`-Verzeichnis.
- Apache-2.0-Lizenz, feste Copyright-Konvention, Package-Metadaten und der
  deaktivierte, explizit freizugebende NuGet-Publishweg sind festgelegt.

## 8. C#-CI-Matrix - DONE

Die aktuelle Referenzimplementierung besitzt drei getrennte GitHub-Workflows:

- `C# CI` läuft bei Push und Pull Request. Der Workflow stellt die gepinnten
  .NET-SDKs bereit, restauriert die Solution, prüft die Formatierung, führt alle
  Nicht-Performance-Tests und den separaten Zero-Allocation-Hot-Path-Gate aus,
  baut und konsumiert Release-Artefakte und prüft deren byteidentische
  Reproduzierbarkeit.
- Die Nicht-Performance-Suite parst und validiert den vollständigen gemeinsamen
  Markdown-Corpus, führt den C#-Conformance-Runner aus, liest kanonische und
  beschädigte `.gesb`-Fixtures, prüft Writer-Goldens, Compilerdiagnosen,
  Source-Ranges, Dokumentationskonsistenz und die öffentliche API.
- `C# Performance Gate` führt die profilgebundenen Performance- und
  Allokationstests bewusst manuell auf dem vorgesehenen selbst gehosteten
  macOS/ARM64-Runner aus und veröffentlicht die Reports als Artefakt.
- `C# Release Candidate` baut nach manueller Auslösung eine geprüfte
  Release-Version und veröffentlicht sie als Workflow-Artefakt. NuGet-Publishing
  bleibt zusätzlich durch Eingabe, Repositoryvariable, Environment und
  kurzlebige Zugangsdaten gesperrt.

Weitere CI-Erweiterungen sind ausschließlich in
[BACKLOG.md](../../BACKLOG.md) geführt und blockieren den abgeschlossenen
C#-Monorepo-Cutover nicht.

## 9. Performance- und Allokationsvertrag

Performance-Conformance sollte nicht einfach Laufzeiten verschiedener CI-Maschinen vergleichen.

Stattdessen:

- gemeinsame Markdown-Conformance-Workloads
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

## 10. Bytecode weiterentwickeln und Opcode-/Formatdefinition zentralisieren

Dieser Punkt wird ausdrücklich erst nach der Monorepo-Migration bearbeitet.
[Bytecode.md](../../specs/Bytecode.md)
und die C#-Enums duplizieren momentan Informationen manuell; die bestehende
mechanische Konsistenzprüfung ist dafür vorerst ausreichend.

Das aktuelle Opcodeformat ist stabil genug als Referenz, aber noch nicht als endgültige Grundlage eines sprachübergreifenden Generators beschlossen. Zuvor sollen im Monorepo insbesondere folgende Möglichkeiten untersucht und gemessen werden:

- I64- und Binary64-Konstanten in Constant Pools
- Immediate-Operanden für Formen wie `Add r0, r1, #12345`
- Unterscheidung von Register-, Immediate- und Constant-Pool-Operanden
- kompaktere feste oder gegebenenfalls variable Instruction-Layouts
- Adressbreiten, Constant-Pool-Indizes und Decoding-Kosten
- Auswirkungen auf `.gesb`, Validator, Dumper, VM-Hot-Path und Portierbarkeit

Erst nach dieser Stabilisierung wird das Autorenformat für eine zentrale
maschinenlesbare Definition festgelegt. JSON ist keine Vorentscheidung; eine
tabellarische Darstellung, TSV, YAML oder eine kleine deklarative DSL müssen
anhand des dann tatsächlichen Modells verglichen werden.

Die spätere zentrale Definition kann mindestens enthalten:

- Opcode-ID und Name
- erlaubte Operandenformen
- Flags, Einheiten und gültige Kombinationen
- Bind-, Value- und Section-Kinds
- relevante Binary- und Bytecodeversionen

Daraus können eingecheckte C#-, Swift-, Kotlin- und C++-Enums beziehungsweise
Deskriptortabellen, Validator-Metadaten, Dumper-Metadaten und Dokumentation
erzeugt werden. Der Generator ist ein Maintainer-Tool; normale IDE- und
Produkt-Builds konsumieren eingecheckte generierte Quellen und benötigen ihn
nicht. CI prüft Definition und generierte Ausgaben auf Drift.

## Weiteres Vorgehen

Monorepo-Cutover, Packaging-Schnitt und C#-CI sind abgeschlossen. Dieses
Arbeitsdokument setzt mit Punkt 9 und anschließend Punkt 10 fort. Sobald ein
dort ebenfalls genanntes Backlog-Thema aktiv bearbeitet wird, wird sein Eintrag
in [BACKLOG.md](../../BACKLOG.md) im selben Änderungssatz entfernt. Alle übrigen
Sprachport-, Integrations- und Optimierungsthemen werden ausschließlich im
Backlog geführt.
