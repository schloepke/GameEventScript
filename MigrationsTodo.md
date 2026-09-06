<!-- Copyright 2026 Stephan Schlöpke -->
<!-- SPDX-License-Identifier: Apache-2.0 -->

# Game Event Script Monorepo migration

Dieses Dokument steuert den einmaligen Umzug von Game Event Script aus dem
PlasticSCM-Workspace von BattleClub in das eigenständige Git-Monorepo. Es hält
Entscheidungen, Reihenfolge, Abnahmen und offene Punkte fest. Die normative
Produkt- und Sprachspezifikation verbleibt bis zum Cutover unter
`StepH-GameEventScript/Documentation/Specification` und wird beim Umzug ohne
inhaltliche Neuinterpretation übernommen.

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
- [ ] GitHub-Remote, Default Branch, Sichtbarkeit, Branch Protection und
  vorhandene Initial-Commits dokumentieren.

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

Noch festzulegen:

- ob Core/Compiler/Runtime zunächst gemeinsam in `StepH.GameEventScript`
  bleiben; empfohlen ist ja
- exakte öffentliche Package-Namen für `CSharpBridge` und Conformance
- neutrale `Ges`-/`GES`-Namen und Registry-Koordinaten vor der ersten
  Veröffentlichung
- eigene GES-Domain sowie ein auf eine spätere Open-Source-Organisation oder
  Foundation übertragbarer Publisher-Namespace
- Form der Solution (`.sln` oder `.slnx`) und gemeinsame MSBuild-Konfiguration
- GitHub-Sichtbarkeit und Branch-Protection-Regeln

Abnahme:

- [x] Es gibt genau einen freigegebenen Zielbaum.
- [x] Jeder zu übernehmende Scaffold-Bestandteil hat eine begründete Entscheidung.
- [x] Keine Migration oder Historienänderung wurde während der Analyse ausgeführt.

## Phase 2 – PlasticSCM-Quelle und Historie inventarisieren

- [x] Plastic-Repository-Spec, Server, Workspace, Branch und aktuelles Changeset
  ermitteln.
- [ ] Sicherstellen, dass alle relevanten Änderungen einschließlich dieses
  aktualisierten Migrationsreviews eingecheckt sind.
- [ ] Frühere Namen, Pfade und Moves von Game Event Script ermitteln.
- [ ] Relevante Branches, Mergebeziehungen, Labels, Autoren und Zeitstempel
  inventarisieren.
- [ ] Einen maschinenlesbaren Changeset- und Pfad-History-Report erzeugen.
- [ ] Einen unveränderlichen Snapshot des aktuellen GES-Quellstands samt
  SHA-256-Manifest sichern.
- [ ] Prüfen, ob Xlinks, LFS-artige Inhalte, große Binärdateien oder nicht
  exportierbare Plastic-Konstrukte betroffen sind.
- [ ] Bestätigen, dass `services/.obsidian` weder im Plastic-Ausgangspunkt noch
  in einem Snapshot- oder Exportmanifest enthalten ist.

Festgestellter Ausgangspunkt:

```text
Workspace:  Battle Club Main
Workspace:  /Users/stephan/Projects/BattleClub
Repository: Battle Club/Battle Club@4324069@cloud
Branch:     /main/refactor/either-to-result-and-server-logging
Changeset:  666
Status:     Changeset 666 war vor Aktualisierung dieses Dokuments clean
Client:     cm 11.0.16.10042
```

Die vorliegende Fortschritts- und Reviewaktualisierung ist nach der gemeinsamen
Freigabe noch als letzter Plastic-Migrationsplan-Changeset einzuchecken. Erst
dieser nachfolgende Changeset bildet den endgültigen Export-Ausgangspunkt.

Abnahme:

- [ ] Der aktuelle Plastic-Stand ist reproduzierbar identifiziert.
- [ ] Alle historischen Pfade, die beim späteren Filtern berücksichtigt werden
  müssen, sind bekannt.
- [ ] Der History-Report kann unabhängig vom Plastic-GUI gelesen werden.

## Phase 3 – Vollständigen Plastic→Git-Export erproben

- [ ] `cm fast-export ... --nodata` zunächst gegen ein temporäres Ziel ausführen.
- [ ] Exportwarnungen, Branchabbildung und erwartete Größe bewerten.
- [ ] Den vollständigen Fast Export ausschließlich unter `_migration/plastic`
  erzeugen.
- [ ] Den Export in ein neues lokales Git-Zwischenrepository importieren.
- [ ] Exportdatei, Marks-Dateien und relevante Toolversionen mit Prüfsummen
  protokollieren.
- [ ] Noch keine Verbindung oder Push-Operation zum GitHub-Ziel ausführen.

Abnahme:

- [ ] Anzahl und Identität der exportierten Änderungen sind plausibel.
- [ ] Autoren, Zeitstempel und Kommentare sind erhalten oder Abweichungen sind
  ausdrücklich dokumentiert.
- [ ] Branches, Mergebeziehungen und Labels sind geprüft.
- [ ] Der importierte Git-Tree des letzten relevanten Changesets stimmt mit dem
  gesicherten Plastic-Snapshot überein.

## Phase 4 – GES-Historie sicher extrahieren

- [ ] Den vollständigen importierten Git-Bestand ausschließlich in einer neuen
  Kopie filtern.
- [ ] Alle aktuellen und historischen GES-Pfade einschließen.
- [ ] Nicht zu GES gehörende BattleClub-, Beamable- und Unity-Inhalte entfernen.
- [ ] `.obsidian` und andere lokale Editor-/Vault-Metadaten unabhängig von
  ihrem historischen Trackingzustand aus allen veröffentlichbaren Refs entfernen.
- [ ] Leere oder nur fachfremde Commits nach einer festgelegten Policy behandeln.
- [ ] Plastic-Changesets soweit möglich auf resultierende Git-Commits abbilden.
- [ ] Erreichbare Git-Objekte, große Dateien, Zugangsdaten und vertrauliche
  Inhalte prüfen.
- [ ] Das gefilterte Repository neu klonen oder bereinigen, damit nicht
  erreichbare Objekte des vollständigen Exports nicht mitgeführt werden.

Abnahme:

- [ ] Der aktuelle gefilterte Tree ist inhaltlich identisch zum GES-Snapshot.
- [ ] Die relevante Datei- und Commit-Historie ist nachvollziehbar.
- [ ] Kein fachfremder oder vertraulicher Inhalt ist über irgendeinen zu
  veröffentlichenden Ref erreichbar.
- [ ] Der vollständige ungefilterte Export bleibt ausschließlich lokal/offline.

## Phase 5 – Historie mit dem GitHub-Ziel verbinden

- [ ] Prüfen, ob das GitHub-Ziel wirklich leer ist oder Initial-Commits besitzt.
- [ ] Bei einem leeren Ziel die gefilterte Historie als Ausgangshistorie verwenden.
- [ ] Vorhandene sinnvolle Ziel-Commits nur bewusst übernehmen; keine ungeprüfte
  `--allow-unrelated-histories`-Zusammenführung durchführen.
- [ ] Default Branch und Commit-/Tag-Namenskonvention festlegen.
- [ ] Vor dem ersten Push einen lokalen Backup-Tag beziehungsweise ein Bundle
  des gefilterten Ausgangsstands erzeugen.

Abnahme:

- [ ] Der lokale Zielstand enthält ausschließlich die freigegebene Historie.
- [ ] Der letzte importierte Commit repräsentiert den unveränderten Plastic-Stand.
- [ ] Noch wurde kein struktureller oder fachlicher Umbau mit dem Import vermischt.

## Phase 6 – Mechanischer Monorepo-Umzug

- [ ] Gemeinsame Spezifikationen, Conformance-Suites, Fixtures, Reports und
  Benchmarks an ihre freigegebenen Monorepo-Orte verschieben.
- [ ] Die C#-Implementierung und ihre nativen Tests nach
  `implementation/csharp` verschieben.
- [ ] Bestehende Inhalte möglichst mit echten Git-Moves und ohne fachliche
  Änderungen umordnen.
- [ ] Relative Links, Fixture-Auflösung, Testdatenpfade und Buildpfade anpassen.
- [ ] `.obsidian/` in der Monorepo-`.gitignore` verankern und keine lokale
  Vault-Konfiguration migrieren.
- [ ] Den mechanischen Umzug als eigenen Commit abschließen.

Abnahme:

- [ ] Alle Markdown- und lokalen Dokumentationslinks lösen auf.
- [ ] Corpus-Identität und kanonische `.gesb`-/GESA-Artefakte sind unverändert.
- [ ] Unterschiede des Migrationscommits bestehen ausschließlich aus Moves und
  notwendigen Pfadanpassungen.

## Phase 7 – C#-Projekt- und Package-Schnitt herstellen

- [ ] Portable Core-, Compiler- und Runtime-Assembly ohne Beamable und Unity
  aufbauen.
- [ ] `CSharpBridge` als eigene C#-spezifische Assembly beziehungsweise eigenes
  Package abtrennen.
- [ ] Conformance-Parser/-Runner und native Testadapter entsprechend der
  spezifizierten Modulgrenzen schneiden.
- [ ] Unnötige Package-Abhängigkeiten entfernen; insbesondere die tatsächliche
  Notwendigkeit von `System.Text.Json` je Assembly prüfen.
- [ ] Namespace-, Assembly-, Package- und XML-Dokumentationsoberflächen prüfen.
- [ ] Reproduzierbare lokale Build- und Testeinstiege bereitstellen.
- [ ] Den Projekt-/Package-Umbau getrennt vom mechanischen Move committen.

Abnahme:

- [ ] Der portable C#-Core baut ohne Beamable- und Unity-Abhängigkeiten.
- [ ] Die komplette Nicht-Performance-Suite besteht.
- [ ] Alle Markdown-Conformance-Fälle bestehen.
- [ ] Public-API- und Cross-Language-Snapshots stimmen.
- [ ] `.gesb`-Golden-/Invalid-/Roundtrip-Abnahmen bestehen.
- [ ] Performance- und Zero-Allocation-Hot-Path-Abnahmen bestehen.
- [ ] `dotnet format --verify-no-changes` besteht für alle C#-Projekte.

## Phase 8 – C#-Distribution vorbereiten

- [ ] Reproduzierbare C#-DLL- und NuGet-Artefakte ausschließlich unter dem
  ignorierten lokalen `artifacts/`-Pfad erzeugen.
- [ ] Package-Inhalt, Lizenz, README, XML-Dokumentation, Symbole und
  Abhängigkeitsgraph prüfen.
- [ ] Einen GitHub-Actions-Releaseweg mit kurzlebiger NuGet-Authentifizierung
  vorbereiten, ohne während der Migration ein öffentliches Release auszulösen.
- [ ] Die DLLs als ersten dokumentierten Unity-Verwendungsweg prüfen; noch keine
  vorweggenommene Editor- oder MonoBehaviour-Abstraktion in den Core aufnehmen.
- [ ] Die spätere SwiftPM- und Maven-Struktur dokumentieren, aber weder leere
  Ports noch funktionslose Package-Gerüste erzeugen.

Abnahme:

- [ ] Ein lokaler Dry Run erzeugt dieselben veröffentlichbaren C#-Artefakte wie
  CI.
- [ ] Keine generierte Release-Datei ist im Git-Index enthalten.
- [ ] Der portable Core enthält keine Beamable- oder Unity-Abhängigkeit.
- [ ] Unity kann die freigegebenen C#-DLLs in einem externen Testprojekt laden.

## Phase 9 – GitHub-Cutover

- [ ] Vollständige lokale Abnahme des Zielrepositories protokollieren.
- [ ] GitHub-Remote und Zielorganisation nochmals prüfen.
- [ ] Freigegebene Branches und Tags pushen.
- [ ] CI ausführen und Ergebnisse mit der lokalen Abnahme vergleichen.
- [ ] Branch Protection, Required Checks und Releaseberechtigungen aktivieren.
- [ ] GitHub zum einzigen aktiven Entwicklungsort erklären.
- [ ] Im PlasticSCM-Workspace Game Event Script erst nach bestätigtem GitHub-
  und Integrationsbetrieb entfernen oder auf den benötigten Adapter reduzieren.

## Phase 10 – Lokale Migration abschließen

- [ ] Das GitHub-Monorepo außerhalb von BattleClub in einen eigenen Workspace
  klonen.
- [ ] Den neuen eigenständigen Workspace bauen und vollständig testen.
- [ ] Benötigte History-Reports, Checksummen und die Changeset→Commit-Zuordnung
  an einem dauerhaften, geeigneten Ort archivieren.
- [ ] Den ungefilterten Plastic-Export nicht veröffentlichen; dessen gewünschte
  Offline-Aufbewahrung oder sichere Löschung bewusst entscheiden.
- [ ] `_migration` aus dem PlasticSCM-Workspace entfernen.
- [ ] Dieses Todo abschließen und die weiterführende Roadmap im Monorepo
  fortsetzen.

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
