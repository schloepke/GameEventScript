<!-- Copyright 2026 Stephan Schlöpke -->
<!-- SPDX-License-Identifier: Apache-2.0 -->

# Game Event Script Monorepo migration

Dieses Dokument steuert den einmaligen Umzug von Game Event Script aus dem
PlasticSCM-Workspace von BattleClub in das eigenständige Git-Monorepo. Es hält
Entscheidungen, Reihenfolge, Abnahmen und offene Punkte fest. Die normative
Produkt- und Sprachspezifikation wurde beim mechanischen Umzug ohne inhaltliche
Neuinterpretation nach `specs` übernommen.

## Festgelegte Grundsätze

- Das bestehende leere GitHub-Repository wird das kanonische Zielrepository.
- Die AI-generierte Basisstruktur ist ausschließlich ein zu prüfender Entwurf.
- Die erste Monorepo-Implementierung ist C#. Swift folgt als erste neue
  Sprachimplementierung, Kotlin danach. C und C++ bleiben ohne vorbereitete
  Leerstruktur zurückgestellt, bis ein konkreter nativer Port begonnen wird.
- Der PlasticSCM-Workspace bleibt während der Migration unverändert die aktive
  Source of Truth.
- Der GitHub-Stand wird erst nach vollständiger lokaler Abnahme zur neuen Source
  of Truth.
- Es gibt keine dauerhafte Doppelpflege zwischen PlasticSCM und Git.
- Beamable ist weder Zielplattform noch Integration des Monorepos. Der neue
  portable C#-Build enthält keine Beamable-Abhängigkeit; ein Beamable-Service
  kann Game Event Script später wie jede andere externe Dependency verwenden.
- Unity verwendet zunächst die freigegebenen C#-DLLs. Unity-spezifische
  Editor-Integration, vorbereitete MonoBehaviours und ein distributierbares
  Unity-Package werden zuerst in einem echten Unity-Projekt entwickelt und erst
  danach als eigenständige Integration in das Monorepo übernommen.
- Die PlasticSCM-Historie wird nach Möglichkeit als echte Git-Historie
  übernommen. Ein bloßer History-Report ist nur die Rückfalllösung.
- Die Opcode- und Formatgenerierung bleibt zurückgestellt, bis das Monorepo
  steht und das nächste Bytecode-/Constant-Pool-Modell stabilisiert wurde.

## Temporärer Arbeitsbereich

Der lokale Migrationsbereich liegt unter:

```text
/Users/stephan/Projects/BattleClub/BeamableServices/services/_migration/
  review/
    generated-scaffold/
  github/
    target/
  plastic/
    export/
    imported-git/
    filtered-git/
  reports/
  scratch/
```

Die Unterordner dürfen eigenständige Git-Repositories enthalten. Der gesamte
Ordner `_migration` wird durch PlasticSCM ignoriert und niemals eingecheckt.
Insbesondere Fast-Export-Dateien, Marks-Dateien, ungefilterte Git-Objekte und
möglicherweise projektexterne Plastic-Inhalte dürfen nicht nach GitHub gelangen.
Der lokale Lese-Vault unter `services/.obsidian` ist reine Editor-Konfiguration:
Er wird von PlasticSCM ignoriert und aus Snapshot, Historienfilter, Git-Import
und künftigem Monorepo vollständig ausgeschlossen.

Nach erfolgreichem Cutover wird das Monorepo außerhalb des PlasticSCM-Workspace
in einem eigenen Workspace ausgecheckt. Anschließend wird `_migration`
vollständig entfernt. Dieses Dokument wird in das Monorepo als abgeschlossene
Migrationsaufzeichnung übernommen oder nach einer bewussten Abschlussentscheidung
archiviert.

## Phase 0 – Eingaben bereitstellen

- [x] `_migration` und die vorgesehenen Unterordner anlegen.
- [x] Sicherstellen, dass PlasticSCM `_migration` vollständig ignoriert.
- [x] Das GitHub-Repository nach `_migration/github/target` klonen.
- [x] Die AIR-generierte Basisstruktur unter
  `_migration/review/generated-scaffold` bereitstellen.
- [x] Keine der beiden Strukturen vor der Analyse zusammenführen.
- [x] GitHub-Remote, Default Branch, private Sichtbarkeit und vorhandene
  Initial-Commits dokumentieren.
- [x] Branch Protection vor dem finalen `main`-Cutover prüfen und festlegen.

## Phase 1 – Bestehendes Monorepo und Scaffold analysieren

- [x] Verzeichnisstruktur, Buildsysteme und Workspace-Dateien beider Kandidaten
  inventarisieren.
- [x] Package-, Namespace-, Produkt- und Testkonventionen feststellen.
- [x] CI-, Release-, Versionierungs-, Formatting- und Lizenzkonfiguration prüfen.
- [x] Vorhandene Entscheidungen für Swift, Kotlin, C++, C#, Unity und gemeinsame
  Spezifikationen erfassen, ohne leere Sprachimplementierungen zu erzeugen.
- [x] Scaffold-Bestandteile als übernehmen, anpassen oder verwerfen klassifizieren.
- [x] Den konkreten Zielbaum und die anfänglichen C#-Projektgrenzen als Review-Ergebnis
  festschreiben.

### Analyse der bereitgestellten Eingaben

GitHub-Ziel:

- Remote: `git@github.com:schloepke/GameEventScript.git`
- Default Branch: `main`
- Der Branch ist clean und stimmt mit `origin/main` überein.
- Das Repository besitzt bereits genau einen Commit:
  `c8191b17a532aeb377779082f7699944b3168513` mit dem Betreff
  `Initial commit`.
- Dieser Commit enthält ausschließlich eine Apache-2.0-`LICENSE`. Sie
  unterscheidet sich von der im aktuellen Projekt mechanisch geprüften
  kanonischen Lizenz nur durch deren anfängliches LF-Byte. Beim Cutover muss die
  kanonische Projektdatei gewinnen.
- Sichtbarkeit und Branch Protection sind aus dem lokalen Clone noch nicht
  festgestellt.
- Der Initialcommit wird nicht ungeprüft mit einer unabhängigen Plastic-Historie
  gemergt. Empfohlen ist, seinen Hash im Migrationsreport zu bewahren, `main`
  später auf die geprüfte importierte Historie zu setzen und die kanonische
  Lizenz im expliziten Monorepo-Migrationscommit zu übernehmen.

AIR-Scaffold:

- `main` besitzt einen fachlichen Scaffold-Commit. Zusätzlich existieren 17
  `refs/air-checkpoints/...` mit synthetischen `Local History`-Commits; diese
  bilden keine zu übernehmende Produkthistorie.
- Der Arbeitsbaum ist nicht clean. AIR-Plan und `AGENTS.md` sind gestaged;
  `.cache`, Beispiele und ein kopierter Math-Test sind untracked. Zusätzlich
  liegen ignorierte Build- und IDE-Artefakte vor.
- Das Scaffold beschreibt nur Platzhalterparser und ein überholtes JSON-
  Conformance-Modell. Es enthält keine fachlich relevante Implementierung.
- Lizenz, Autoren, Repository-URL, API, Testframework und Paketmetadaten stimmen
  nicht mit dem aktuellen Projekt überein.

### Scaffold-Klassifikation

Als Konzept übernehmen:

- native Buildsysteme je Sprache statt eines gemeinsamen Meta-Buildsystems
- getrennte CI-Jobs je Plattform und Sprache
- C# als `netstandard2.1`-Library für die aktuelle Unity-Kompatibilität
- die gebaute C#-DLL als erster Unity-Verwendungsweg
- spätere native Veröffentlichungswege über NuGet, SwiftPM und Maven sowie für
  einen künftigen C- oder C++-Port über CMake und Conan
- Root-Kommandos für gezielte Build-, Test- und Pack-Abläufe

An den aktuellen Stand anpassen:

- `src/<language>` wird zugunsten der deutlicheren Trennung unter
  `implementation/<language>` nicht unverändert übernommen.
- `src/unity` wird nicht übernommen. Eine spätere Unity-Integration entsteht
  erst aus erprobtem Unity-Code und bleibt außerhalb der Sprachimplementierung.
- Root-Skripte dürfen anfangs nur C# voraussetzen. Weitere Toolchains werden
  erst mit ihrer Implementierung ergänzt und müssen weiterhin separat ausführbar
  bleiben.
- CI beginnt mit C#, Dokumentationskonsistenz, Markdown-Conformance, Binary-
  Fixtures sowie Performance-/Allokationsgates.
- Package- und Releasekonfiguration verwendet Apache-2.0, Stephan Schlöpke,
  `StepH.GameEventScript` und die tatsächliche GitHub-URL.
- Der gemeinsame Corpus besteht aus Conformance-Markdown, `.gesb`-Ressourcen
  und Cross-Language-Referenzen statt JSON-Einzelfällen.

Nicht übernehmen:

- Platzhalterparser und Platzhaltertests aller vier Sprachen
- die Scaffold-JSON-Schemas und beiden künstlichen JSON-Cases
- leere Swift-, Kotlin-, C-, C++- und native Unity-Implementierungen
- `refs/air-checkpoints`, `.air`, `.cache`, IDE-Dateien und Buildausgaben
- MIT-Lizenzangaben, generische Autoren und falsche Repository-URLs
- ein `build-all`/`test-all`, das schon vor vorhandenen Ports alle vier
  Toolchains zwingend voraussetzt
- den Scaffold-Commit selbst; relevante Konzepte werden später bewusst in der
  importierten Plastic-Historie neu umgesetzt

### Vorgeschlagener Zielbaum zur Freigabe

```text
/
  .github/
    workflows/
  specs/
    README.md
    ... normative Sprach-, API-, Host-, Bytecode- und Formatspezifikationen
  docs/
    guide/
    history/
  conformance/
    suites/
    fixtures/
    cross-language/
  implementation/
    csharp/
      src/
        StepH.GameEventScript/
      tests/
        StepH.GameEventScript.Tests/
  examples/
    scripts/
    csharp/
    unity/
  scripts/
  .editorconfig
  .gitignore
  AGENTS.md
  LICENSE
  LICENSING.md
  README.md
```

`benchmarks`, `tools`, `implementation/swift`, `implementation/kotlin`, native
Implementierungen und Unity-Integration werden erst angelegt, wenn sie eigenen
Inhalt besitzen. Performance-Cases verbleiben zunächst im gemeinsamen
Conformance-Corpus; die C#-Messadapter gehören zur C#-Testimplementierung.
Generierte Reports und Received-Dateien sind Artefakte, während freigegebene
Cross-Language-Referenzen versioniert werden.

Ausführbare Beispiele liegen unter `examples`, damit sie unabhängig von der
Dokumentation gebaut und getestet werden können. Kurze erklärende Ausschnitte
bleiben direkt unter `docs/guide`; Guides dürfen auf die ausführbaren Beispiele
verweisen.

Es gibt keinen eingecheckten Sammelordner `packages` für erzeugte
Release-Pakete. Package-Manifeste und Publishing-Konfiguration bleiben bei der
jeweiligen Implementierung. Lokale und in CI erzeugte DLLs, `.nupkg`, JARs,
Unity-Archive und native Pakete werden ausschließlich unter einem ignorierten
`artifacts/`-Arbeitsverzeichnis gestaged und anschließend in die zuständige
Registry beziehungsweise ein GitHub Release veröffentlicht. Ein separates
Packaging-Verzeichnis wird nur eingeführt, wenn ein konkretes Package wie eine
Unity-UPM-Distribution eigene versionierte Quelldateien benötigt.

### Künftiger Publisher-Namespace

Es wird während der Migration noch kein endgültiger Publisher-Namespace
festgelegt. Vor der ersten öffentlichen Package-Veröffentlichung soll bevorzugt
eine eigene, ausschließlich GES zugeordnete Domain registriert werden. Domain,
Maven-Namespace, Package-Publisher und eine mögliche GitHub-Organisation sollen
so gewählt und dokumentiert werden, dass sie später gemeinsam an eine
eigenständige Open-Source-Organisation oder Foundation übertragen werden können.

Als derzeit kontrollierte Ausweichmöglichkeiten stehen zur Verfügung:

- `step-h.com` ist der gamingorientierten Marke StepH zugeordnet.
- `schloepke.de` ist der persönlichen beziehungsweise Enterprise-orientierten
  Tätigkeit zugeordnet.

`jbasics.org` und der möglicherweise noch zugeordnete Maven-Namespace
`org.jbasics` gehören zu einem unabhängigen, veralteten Altprojekt ohne Bezug zu
GES. Sie sind keine Veröffentlichungsoption und liegen vollständig außerhalb
dieser Migration; vorhandene Berechtigungen werden weder geprüft noch verändert.

Diese Domains begründen keine Vorentscheidung für die öffentlichen
GES-Koordinaten. Falls keine eigene GES-Domain verwendet wird, kommen für Maven
Central weiterhin `com.step-h...`, `de.schloepke...` oder als GitHub-gebundene
Alternative `io.github.schloepke...` infrage. Ein Bindestrich ist in einem
Maven-`groupId` zulässig; der Kotlin-/Java-Package-Name muss nicht identisch mit
dem Maven-`groupId` sein.

Ein gemeinsamer Root-Ordner `extensions` wird vorerst nicht angelegt.
Sprachspezifische Extensions bleiben bei ihrer Implementierung. Eine künftige
portable Standard-Extension-Bibliothek erhält zuerst normative Verträge unter
`specs` und gemeinsame Fälle unter `conformance`; ihre Implementierungen folgen
danach in den jeweiligen Sprachpfaden.

### Namens- und Produktstrategie nach dem Cutover

Die Abkürzung `GES` sowie `.ges`, `.gesb`, `.gesa` und bestehende neutrale
Compiler-IDs eignen sich sowohl für **Game Event Script** als auch für eine
spätere anders positionierte Bedeutung wie **Guided Event Script**. Der
Historienimport und der mechanische Move benennen dennoch keine öffentlichen
Symbole um.

Nach dem vollständig grünen C#-Cutover wird in einem separaten, überprüfbaren
Änderungssatz entschieden, welche öffentlichen Typen, Namespaces, Assemblies,
Package-IDs und Dokumenttitel auf die neutrale Kurzform `Ges` beziehungsweise
`GES` umgestellt werden. Diese Entscheidung muss vor der ersten öffentlichen
Registry-Veröffentlichung fallen, weil Package-Koordinaten dauerhaft und nur
schwer rebrandbar sind. Die jeweilige Schreibweise darf sprachidiomatisch sein,
die fachliche Identität und das Binary-Format müssen jedoch gleich bleiben.

Für den lokalen, noch unveröffentlichten C#-Cutover ist festgelegt:

- Core, Compiler und Runtime bleiben gemeinsam in `StepH.GameEventScript`;
- die ergänzenden Packages heißen vorläufig
  `StepH.GameEventScript.CSharpBridge` und
  `StepH.GameEventScript.Conformance`;
- die breit unterstützte `GameEventScript.sln` ist der gemeinsame
  C#-Workspace; eine `.slnx` ist derzeit nicht erforderlich.

Vor der ersten öffentlichen Veröffentlichung noch festzulegen:

- neutrale `Ges`-/`GES`-Namen und Registry-Koordinaten vor der ersten
  Veröffentlichung
- eigene GES-Domain sowie ein auf eine spätere Open-Source-Organisation oder
  Foundation übertragbarer Publisher-Namespace
- GitHub-Sichtbarkeit und Branch-Protection-Regeln

Abnahme:

- [x] Es gibt genau einen freigegebenen Zielbaum.
- [x] Jeder zu übernehmende Scaffold-Bestandteil hat eine begründete Entscheidung.
- [x] Keine Migration oder Historienänderung wurde während der Analyse ausgeführt.

## Phase 2 – PlasticSCM-Quelle und Historie inventarisieren

- [x] Plastic-Repository-Spec, Server, Workspace, Branch und aktuelles Changeset
  ermitteln.
- [x] Sicherstellen, dass alle relevanten Änderungen einschließlich dieses
  aktualisierten Migrationsreviews eingecheckt sind.
- [x] Frühere Namen, Pfade und Moves von Game Event Script ermitteln.
- [x] Relevante Branches, Mergebeziehungen, Labels, Autoren und Zeitstempel
  inventarisieren.
- [x] Einen maschinenlesbaren Changeset- und Pfad-History-Report erzeugen.
- [x] Einen unveränderlichen Snapshot des aktuellen GES-Quellstands samt
  SHA-256-Manifest sichern.
- [x] Prüfen, ob Xlinks, LFS-artige Inhalte, große Binärdateien oder nicht
  exportierbare Plastic-Konstrukte betroffen sind.
- [x] Bestätigen, dass `services/.obsidian` weder im Plastic-Ausgangspunkt noch
  in einem Snapshot- oder Exportmanifest enthalten ist.

Festgestellter Ausgangspunkt:

```text
Workspace:  Battle Club Main
Workspace:  /Users/stephan/Projects/BattleClub
Repository: Battle Club/Battle Club@4324069@cloud
Branch:     /main/refactor/either-to-result-and-server-logging
Changeset:  667 (`4e0e0ea1-1d16-48ff-81a2-3da37fb2006e`)
Status:     Changeset 667 ist der eingecheckte, cleane Export-Baseline
Client:     cm 11.0.16.10042
```

Der Baseline-Changeset trägt den Kommentar
`GameEventScript: Prepare Game Event Script monorepo migration`. Sein
kontrollierter GES-Snapshot umfasst 289 Dateien und besitzt den Manifest-Hash
`5035dfbce95220feb9d419e125c0bb4de0c8fd0ea28222d5b9a68e211c63a6a4`.

Abnahme:

- [x] Der aktuelle Plastic-Stand ist reproduzierbar identifiziert.
- [x] Alle historischen Pfade, die beim späteren Filtern berücksichtigt werden
  müssen, sind bekannt.
- [x] Der History-Report kann unabhängig vom Plastic-GUI gelesen werden.

## Phase 3 – Vollständigen Plastic→Git-Export erproben

- [x] `cm fast-export ... --nodata` zunächst gegen ein temporäres Ziel ausführen.
- [x] Exportwarnungen, Branchabbildung und erwartete Größe bewerten.
- [x] Den vollständigen Fast Export ausschließlich unter `_migration/plastic`
  erzeugen.
- [x] Den Export in ein neues lokales Git-Zwischenrepository importieren.
- [x] Exportdatei, Marks-Dateien und relevante Toolversionen mit Prüfsummen
  protokollieren.
- [x] Noch keine Verbindung oder Push-Operation zum GitHub-Ziel ausführen.

Abnahme:

- [x] Anzahl und Identität der exportierten Änderungen sind plausibel.
- [x] Autoren, Zeitstempel und Kommentare sind erhalten oder Abweichungen sind
  ausdrücklich dokumentiert.
- [x] Branches, Mergebeziehungen und Labels sind geprüft.
- [x] Der importierte Git-Tree des letzten relevanten Changesets stimmt mit dem
  gesicherten Plastic-Snapshot überein.

## Phase 4 – GES-Historie sicher extrahieren

- [x] Den vollständigen importierten Git-Bestand ausschließlich in einer neuen
  Kopie filtern.
- [x] Alle aktuellen und historischen GES-Pfade einschließen.
- [x] Nicht zu GES gehörende BattleClub-, Beamable- und Unity-Inhalte entfernen.
- [x] `.obsidian` und andere lokale Editor-/Vault-Metadaten unabhängig von
  ihrem historischen Trackingzustand aus allen veröffentlichbaren Refs entfernen.
- [x] Leere oder nur fachfremde Commits nach einer festgelegten Policy behandeln.
- [x] Plastic-Changesets soweit möglich auf resultierende Git-Commits abbilden.
- [x] Erreichbare Git-Objekte, große Dateien, Zugangsdaten und vertrauliche
  Inhalte prüfen.
- [x] Das gefilterte Repository neu klonen oder bereinigen, damit nicht
  erreichbare Objekte des vollständigen Exports nicht mitgeführt werden.

Abnahme:

- [x] Der aktuelle gefilterte Tree ist inhaltlich identisch zum GES-Snapshot.
- [x] Die relevante Datei- und Commit-Historie ist nachvollziehbar.
- [x] Kein fachfremder oder vertraulicher Inhalt ist über irgendeinen zu
  veröffentlichenden Ref erreichbar.
- [x] Der vollständige ungefilterte Export bleibt ausschließlich lokal/offline.

## Phase 5 – Historie mit dem GitHub-Ziel verbinden

- [x] Prüfen, ob das GitHub-Ziel wirklich leer ist oder Initial-Commits besitzt.
- [x] Bei einem leeren Ziel die gefilterte Historie als Ausgangshistorie verwenden.
- [x] Vorhandene sinnvolle Ziel-Commits nur bewusst übernehmen; keine ungeprüfte
  `--allow-unrelated-histories`-Zusammenführung durchführen.
- [x] Default Branch und Commit-/Tag-Namenskonvention festlegen.
- [x] Vor dem ersten Push einen lokalen Backup-Tag beziehungsweise ein Bundle
  des gefilterten Ausgangsstands erzeugen.

Der Remote-Initialcommit `c8191b17a532aeb377779082f7699944b3168513` wurde
nicht gemergt und bleibt in einem geprüften lokalen Bundle erhalten. `main` ist
der künftige Default Branch. Historische Tags bleiben unverändert; neue
öffentliche Releases verwenden nach Festlegung der Produktkoordinaten
`vMAJOR.MINOR.PATCH`. Der importierte Stand liegt bis zur Freigabe ausschließlich
lokal auf `migration/plastic-import`.

Abnahme:

- [x] Der lokale Zielstand enthält ausschließlich die freigegebene Historie.
- [x] Der letzte importierte Commit repräsentiert den unveränderten Plastic-Stand.
- [x] Noch wurde kein struktureller oder fachlicher Umbau mit dem Import vermischt.

## Phase 6 – Mechanischer Monorepo-Umzug

- [x] Gemeinsame Spezifikationen, Conformance-Suites, Fixtures, Reports und
  Benchmarks an ihre freigegebenen Monorepo-Orte verschieben.
- [x] Die C#-Implementierung und ihre nativen Tests nach
  `implementation/csharp` verschieben.
- [x] Bestehende Inhalte möglichst mit echten Git-Moves und ohne fachliche
  Änderungen umordnen.
- [x] Relative Links, Fixture-Auflösung, Testdatenpfade und Buildpfade anpassen.
- [x] `.obsidian/` in der Monorepo-`.gitignore` verankern und keine lokale
  Vault-Konfiguration migrieren.
- [x] Den mechanischen Umzug als eigenen Commit abschließen.

Abnahme:

- [x] Alle Markdown- und lokalen Dokumentationslinks lösen auf.
- [x] Corpus-Identität und kanonische `.gesb`-/GESA-Artefakte sind unverändert.
- [x] Unterschiede des Migrationscommits bestehen ausschließlich aus Moves und
  notwendigen Pfadanpassungen.

## Phase 7 – C#-Projekt- und Package-Schnitt herstellen

- [x] Portable Core-, Compiler- und Runtime-Assembly ohne Beamable und Unity
  aufbauen.
- [x] `CSharpBridge` als eigene C#-spezifische Assembly beziehungsweise eigenes
  Package abtrennen.
- [x] Conformance-Parser/-Runner und native Testadapter entsprechend der
  spezifizierten Modulgrenzen schneiden.
- [x] Unnötige Package-Abhängigkeiten entfernen; insbesondere die tatsächliche
  Notwendigkeit von `System.Text.Json` je Assembly prüfen.
- [x] Namespace-, Assembly-, Package- und XML-Dokumentationsoberflächen prüfen.
- [x] Reproduzierbare lokale Build- und Testeinstiege bereitstellen.
- [x] Den Projekt-/Package-Umbau getrennt vom mechanischen Move committen.

Phase 7 erzeugt drei getrennte `netstandard2.1`-Assemblies und vorläufige
NuGet-Package-IDs: `StepH.GameEventScript`,
`StepH.GameEventScript.CSharpBridge` und
`StepH.GameEventScript.Conformance`. Bridge und Conformance hängen jeweils nur
vom Core ab; der Core besitzt keine externen Package-, Beamable- oder
Unity-Abhängigkeiten. Die gemeinsame C#-Solution und die Root-Skripte bilden
Build, Test, Performanceprüfung, Formatprüfung und lokalen Pack-Dry-Run ab.

Der Abschlusslauf umfasst 1.167 erfolgreiche Nicht-Performance-Ausführungen,
sechs erfolgreiche Performance-/Allokationsausführungen und einen fehlerfreien
Roslyn-Formatcheck. Die drei lokalen NuGet-Pakete wurden ausschließlich unter
dem ignorierten `artifacts/csharp/packages` erzeugt und ihr jeweiliger
Core-Abhängigkeitsgraph geprüft.

Abnahme:

- [x] Der portable C#-Core baut ohne Beamable- und Unity-Abhängigkeiten.
- [x] Die komplette Nicht-Performance-Suite besteht.
- [x] Alle Markdown-Conformance-Fälle bestehen.
- [x] Public-API- und Cross-Language-Snapshots stimmen.
- [x] `.gesb`-Golden-/Invalid-/Roundtrip-Abnahmen bestehen.
- [x] Performance- und Zero-Allocation-Hot-Path-Abnahmen bestehen.
- [x] `dotnet format --verify-no-changes` besteht für alle C#-Projekte.

## Phase 8 – C#-Distribution vorbereiten

- [x] Reproduzierbare C#-DLL- und NuGet-Artefakte ausschließlich unter dem
  ignorierten lokalen `artifacts/`-Pfad erzeugen.
- [x] Package-Inhalt, Lizenz, README, XML-Dokumentation, Symbole und
  Abhängigkeitsgraph prüfen.
- [x] Einen GitHub-Actions-Releaseweg mit kurzlebiger NuGet-Authentifizierung
  vorbereiten, ohne während der Migration ein öffentliches Release auszulösen.
- [x] Die DLLs als ersten dokumentierten Unity-Verwendungsweg prüfen; noch keine
  vorweggenommene Editor- oder MonoBehaviour-Abstraktion in den Core aufnehmen.
- [x] Die spätere SwiftPM- und Maven-Struktur dokumentieren, aber weder leere
  Ports noch funktionslose Package-Gerüste erzeugen.

Abnahme:

- [x] Ein lokaler Dry Run erzeugt dieselben veröffentlichbaren C#-Artefakte wie
  CI.
- [x] Keine generierte Release-Datei ist im Git-Index enthalten.
- [x] Der portable Core enthält keine Beamable- oder Unity-Abhängigkeit.
- [x] Die echte Unity-DLL-Abnahme bewusst in die nachgelagerte
  Produktintegration verschieben und dort separat verfolgen.

Der exakte Unity-DLL-Satz wird bereits durch ein externes, projektfreies
.NET-Consumer-Projekt geladen und ausgeführt. Die letzte Abnahme bleibt bewusst
offen, bis ein echtes Unity-Projekt den dort gewählten Scripting-Backend- und
API-Kompatibilitätsstand geprüft hat; eine gewöhnliche .NET-Ausführung wird
nicht fälschlich als Unity-Abnahme gewertet.

Umgesetzt sind ein gepinnter SDK-Build, deterministische Assemblies und portable
PDBs, kanonisch normalisierte unsignierte NuGet-/Symbolpakete, ein Paket- und
Direkt-DLL-Consumer sowie getrennte CI-, Performance- und Release-Candidate-
Workflows. Der Release-Candidate-Workflow publiziert nur bei explizitem
manuellem Schalter, freigeschalteter Repository-Variable und geschützter NuGet-
Environment über einen kurzlebigen OIDC-Key. Die bisherige Performance-Shell
hatte entgegen dem Profilnamen Debug-Binaries gebaut; sie verwendet jetzt
Release, und nur die dadurch reproduzierbar geänderten Allokationsreferenzen
wurden über den Received-Workflow neu bestätigt.

## Phase 9 – GitHub-Cutover

Der Cutover erfolgte am 7. September 2026 in das private Repository
`schloepke/GameEventScript`. Der unabhängige GitHub-Initialcommit
`c8191b17a532aeb377779082f7699944b3168513` wurde nicht mit der importierten
Historie vermischt. `main` wurde mit einem auf genau diesen Commit begrenzten
`--force-with-lease` auf den geprüften Importstand
`56d6f291da1b33e5a7daf34b3cc1333bdaa7bdcb` gesetzt. Der historische Tag
`EndOfOldVM` ist veröffentlicht; der ursprüngliche Initialcommit bleibt im
lokalen Phase-5-Bundle erhalten.

Die lokale Abnahme dieses Stands ergab:

- Release-Build mit null Warnungen und null Fehlern;
- unveränderte Roslyn-Formatierung;
- 1.167 von 1.167 erfolgreiche Nicht-Performance-Ausführungen;
- ein erfolgreiches Zero-Allocation-Hot-Path-Gate;
- sechs von sechs erfolgreiche Performance-/Allokationsausführungen;
- drei NuGet- und drei Symbolpakete mit erfolgreichen Package- und
  Direkt-DLL-Consumer-Prüfungen;
- zwei unabhängige, byteidentische kanonische Paketläufe.

Der erste `C# CI`-Lauf auf `main` (`34073167616`) bestätigte denselben Stand:
Formatierung, Nicht-Performance-Suite, Zero-Allocation-Gate, Release-Artefakte,
Paket-Reproduzierbarkeit und Artifact-Upload waren erfolgreich. Das Repository
verwendet standardmäßig nur lesende Workflow-Berechtigungen. Das Environment
`nuget` ist angelegt und `NUGET_PUBLISH_ENABLED=false` ist ausdrücklich gesetzt;
ohne freigegebene Package-IDs, Publisheridentität, `NUGET_USER` und externe
NuGet-Trusted-Publishing-Policy ist daher kein Publish möglich.

- [x] Vollständige lokale Abnahme des Zielrepositories protokollieren.
- [x] GitHub-Remote und Zielorganisation nochmals prüfen.
- [x] Freigegebene Branches und Tags pushen.
- [x] CI ausführen und Ergebnisse mit der lokalen Abnahme vergleichen.
- [x] Branch Protection und `C# CI / verify` als Required Check prüfen und die
  tarifbedingt nicht verfügbare Aktivierung als späteres Governance-Thema
  dokumentieren.
- [x] Den Releaseweg mit eigenem Environment und explizit deaktiviertem
  Publish-Schalter absichern.
- [x] GitHub zum einzigen aktiven Entwicklungsort erklären.
- [x] Im PlasticSCM-Workspace Game Event Script erst nach bestätigtem GitHub-
  und Integrationsbetrieb entfernen oder auf den benötigten Adapter reduzieren.

Branch Protection und Repository-Rulesets sind für dieses private Repository im
aktuellen GitHub-Tarif nicht verfügbar; GitHub beantwortet beide
Konfigurationswege mit HTTP 403. Dieser Governance-Punkt bleibt sichtbar offen,
bis das Repository öffentlich wird oder der Account einen Tarif mit Schutz für
private Repositories verwendet. Seit dem Cutover ist GitHub die einzige aktive
Entwicklungsquelle. Der PlasticSCM-Stand bleibt bis zur Phase-10-Abnahme
unverändert und dient nicht mehr als paralleler Entwicklungsbranch.

## Phase 10 – Lokale Migration abschließen

- [x] Das GitHub-Monorepo außerhalb von BattleClub in einen eigenen Workspace
  klonen.
- [x] Den neuen eigenständigen Workspace bauen und vollständig testen.
- [x] Die Aufbewahrungsentscheidung für History-Reports, Checksummen und die
  Changeset→Commit-Zuordnung in PlasticSCM dokumentieren.
- [x] Den ungefilterten Plastic-Export nicht veröffentlichen und nach bewusster
  Entscheidung zusammen mit den lokalen Konvertierungsdaten löschen.
- [x] `_migration` aus dem PlasticSCM-Workspace entfernen.
- [x] Dieses Todo abschließen und die weiterführende Roadmap im Monorepo
  fortsetzen.

Die Clean-Checkout-Abnahme unter `/Users/stephan/Projects/GameEventScript`
bestand den Release-Build mit null Warnungen und Fehlern, die unveränderte
Roslyn-Formatierung, 1.167 Nicht-Performance-Ausführungen, das
Zero-Allocation-Hot-Path-Gate und sechs Performance-/Allokationsausführungen.
Der Release-Dry-Run erzeugte und konsumierte alle drei NuGet-, Symbol- und
DLL-Sätze ausschließlich im neuen Workspace. Zwei weitere unabhängige
Paketläufe waren byteidentisch.

PlasticSCM bleibt als vollständiges Archiv der alten BattleClub-Historie
erhalten. Vor dem Entfernen von `_migration` wurde ein Referenzbericht in
PlasticSCM eingecheckt und der zugehörige Changeset als Monorepo-Cutover
gelabelt. Eine zweite dauerhafte Kopie der privaten Exportstreams,
Konvertierungsrepositories und Zuordnungstabellen ist deshalb bewusst nicht
vorgesehen. Keine dieser Daten wurde in das GitHub-Repository übernommen.

Die noch ausstehende Entfernung des alten Beamable-Service und die Korrektur
seiner Unity-Verweise sind keine Monorepo-Migrationsarbeiten. Sie werden im
PlasticSCM-Dokument
`BeamableServices/services/GameEventScriptPlasticCleanup.md` getrennt verfolgt
und blockieren die weitere GES-Entwicklung nicht.

## Rückfalllösung für die Historie

Falls die vollständige Historienkonvertierung technisch nicht zuverlässig
abgenommen werden kann, wird sie nicht teilweise als scheinbar vollständige
Git-Historie veröffentlicht. Stattdessen enthält der initiale Git-Import:

- den byte- beziehungsweise inhaltsgeprüften aktuellen Plastic-Snapshot,
- einen maschinenlesbaren Changeset-/Pfad-History-Report,
- Repository-, Branch- und Changeset-Identität des Ursprungs,
- ein SHA-256-Manifest des migrierten Quellstands,
- eine klare Erklärung, dass PlasticSCM die autoritative Althistorie bleibt.

Das Plastic-Repository muss in diesem Fall langfristig read-only erhalten oder
als vollständiges, überprüft wiederherstellbares Archiv gesichert werden.

## Spätere Arbeit nach dem Cutover

Diese Themen gehören nicht in den mechanischen Umzug:

- Bytecode- und Constant-Pool-Neuentwurf
- Immediate-Operanden und kompaktere Instruction-Layouts
- zentrale Opcode-/Formatdefinition und Codegenerierung
- neutraler `Ges`-/`GES`-Naming-Pass vor der ersten öffentlichen
  Package-Veröffentlichung
- Swift-Implementierung als erster Sprachport und Kotlin als folgender Port
- Unity-Editorintegration, MonoBehaviours und Unity-Package nach ihrer Erprobung
  in einem echten Unity-Projekt
- C-Runtime und optionale dünne C++-Fassade, sobald Gaming-Verbreitung oder eine
  spätere Enterprise-/Embedded-Positionierung den nativen Port rechtfertigen
- zusätzliche sprachspezifische Benchmark-Harnesses
- Signing und Compression für zukünftige `.gesb`-Versionen
