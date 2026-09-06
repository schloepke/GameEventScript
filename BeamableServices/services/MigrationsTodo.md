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
- Die erste Monorepo-Implementierung ist C#. Swift, Kotlin und C++ werden erst
  angelegt, wenn an der jeweiligen Portierung gearbeitet wird.
- Der PlasticSCM-Workspace bleibt während der Migration unverändert die aktive
  Source of Truth.
- Der GitHub-Stand wird erst nach vollständiger lokaler Abnahme zur neuen Source
  of Truth.
- Es gibt keine dauerhafte Doppelpflege zwischen PlasticSCM und Git.
- Beamable- und Unity-Abhängigkeiten werden nicht vorab in der von Beamable
  kontrollierten Struktur entfernt. Die Trennung erfolgt in den neuen
  Monorepo-Projekten.
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

Nach erfolgreichem Cutover wird das Monorepo außerhalb des PlasticSCM-Workspace
in einem eigenen Workspace ausgecheckt. Anschließend wird `_migration`
vollständig entfernt. Dieses Dokument wird in das Monorepo als abgeschlossene
Migrationsaufzeichnung übernommen oder nach einer bewussten Abschlussentscheidung
archiviert.

## Phase 0 – Eingaben bereitstellen

- [x] `_migration` und die vorgesehenen Unterordner anlegen.
- [ ] Sicherstellen, dass PlasticSCM `_migration` vollständig ignoriert.
- [ ] Das leere GitHub-Repository nach `_migration/github/target` klonen.
- [ ] Die AI-generierte Basisstruktur unter
  `_migration/review/generated-scaffold` bereitstellen.
- [ ] Keine der beiden Strukturen vor der Analyse zusammenführen.
- [ ] GitHub-Remote, Default Branch, Sichtbarkeit, Branch Protection und
  vorhandene Initial-Commits dokumentieren.

## Phase 1 – Bestehendes Monorepo und Scaffold analysieren

- [ ] Verzeichnisstruktur, Buildsysteme und Workspace-Dateien beider Kandidaten
  inventarisieren.
- [ ] Package-, Namespace-, Produkt- und Testkonventionen feststellen.
- [ ] CI-, Release-, Versionierungs-, Formatting- und Lizenzkonfiguration prüfen.
- [ ] Vorhandene Entscheidungen für Swift, Kotlin, C++, C#, Unity und gemeinsame
  Spezifikationen erfassen, ohne leere Sprachimplementierungen zu erzeugen.
- [ ] Scaffold-Bestandteile als übernehmen, anpassen oder verwerfen klassifizieren.
- [ ] Den konkreten Zielbaum und die C#-Projektgrenzen als Review-Ergebnis
  festschreiben.

Abnahme:

- [ ] Es gibt genau einen freigegebenen Zielbaum.
- [ ] Jeder zu übernehmende Scaffold-Bestandteil hat eine begründete Entscheidung.
- [ ] Keine Migration oder Historienänderung wurde während der Analyse ausgeführt.

## Phase 2 – PlasticSCM-Quelle und Historie inventarisieren

- [ ] Plastic-Repository-Spec, Server, Workspace, Branch und aktuelles Changeset
  ermitteln.
- [ ] Sicherstellen, dass alle relevanten Änderungen eingecheckt sind.
- [ ] Frühere Namen, Pfade und Moves von Game Event Script ermitteln.
- [ ] Relevante Branches, Mergebeziehungen, Labels, Autoren und Zeitstempel
  inventarisieren.
- [ ] Einen maschinenlesbaren Changeset- und Pfad-History-Report erzeugen.
- [ ] Einen unveränderlichen Snapshot des aktuellen GES-Quellstands samt
  SHA-256-Manifest sichern.
- [ ] Prüfen, ob Xlinks, LFS-artige Inhalte, große Binärdateien oder nicht
  exportierbare Plastic-Konstrukte betroffen sind.

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
  `implementations/csharp` verschieben.
- [ ] Bestehende Inhalte möglichst mit echten Git-Moves und ohne fachliche
  Änderungen umordnen.
- [ ] Relative Links, Fixture-Auflösung, Testdatenpfade und Buildpfade anpassen.
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

## Phase 8 – Beamable- und Unity-Integrationen

- [ ] Feststellen, wie Beamable externe Assemblies oder Packages dauerhaft und
  regenerationssicher konsumiert.
- [ ] Einen schmalen Beamable-Adapter außerhalb des portablen Core aufbauen.
- [ ] Beamable Service Build, lokales Tooling und Deployment mit den neuen
  Assemblies prüfen.
- [ ] Unity-Integration als DLL-Consumer beziehungsweise Package aufbauen.
- [ ] Unity-Compile- und PlayMode-Smoke-Test ergänzen.
- [ ] IL2CPP-, AOT- und Trimming-Verhalten prüfen.
- [ ] Reflection-basierte Registries bei Bedarf durch manuelle oder später
  generierte Registries ergänzen.

Abnahme:

- [ ] Beamable-Regeneration überschreibt keine Game-Event-Script-Quellen oder
  Integrationskonfiguration.
- [ ] Beamable und Unity konsumieren dieselben freigegebenen C#-Artefakte.
- [ ] Keine Beamable- oder Unity-Abhängigkeit ist zurück in den Core gelangt.

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
- Swift-, Kotlin- und C++-Implementierungen
- zusätzliche sprachspezifische Benchmark-Harnesses
- Signing und Compression für zukünftige `.gesb`-Versionen
