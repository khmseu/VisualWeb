# Kontextprotokoll

## Zweck und Abdeckung

Dieses Protokoll bewahrt dokumentierte Ziele, Motive, Alternativen,
Widersprueche und Richtungswechsel fuer VisualWeb. Es ist kein Rohtranskript
und enthaelt keine privaten internen Modellueberlegungen. Die aktuelle Sicht
steht im [Wissensstand](./Wissensstand.md).

- Projektordner: `/bigdata/KAI/projects/VisualWeb`.
- Erste in diesem Turn nachweislich persistierte Dokumentation: 2026-10-22,
  Europe/Berlin (UTC+02:00). Ein aelterer Export behauptet bereits eine
  Initialisierung; die Dateien waren beim zweimaligen Lesen hier nicht vorhanden.
- Belegbarer Ereigniszeitraum: 2026-10-04 bis 2026-10-22.
- KP-001 bis KP-014 sind nachtraeglich rekonstruierte Themenabschnitte.
  Sie stehen grob nach Beginn des Themas; ueberlappende Zeitraeume sind keine
  behauptete strikte Gespraechsreihenfolge.
- KP-015 dokumentiert den aktuellen Auftrag.
- Luecken: lange Tool-/Implementierungsteile nur gezielt gelesen, keine
  vollstaendige Rekonstruktion aller Sitzungen oder GitHub-Kommentare; keine Desktop-
  Verifikation oder erneute Tests; keine privaten Customization-Dateien importiert.

Statusbegriffe: **Nutzerangabe**, **Belegt**, **Vorschlag**, **Ableitung**,
**Offen**. Abstimmung getrennt: **beschlossen**, **vorgeschlagen**,
**verworfen**, **ersetzt**, **offen**. Eine gemergte PR beweist eine
Repositoryaenderung, nicht automatisch eine im Chat dokumentierte Freigabe.

## Quellenverzeichnis

Die Sessions-Dateien sind lokale spaetere Exporte mit Session-ID und
datierter Sprecherzuordnung, nicht neu erzeugte Originaltranskripte. Alle
24 Dateien wurden fuer datierte Nutzerpassagen erschlossen; die unten
angegebenen Antwortbereiche wurden gezielt gelesen. Eingebettete Agenten-
Auftraege unter einem User-Heading werden nicht als neue Nutzerentscheidungen
gewertet. Export und Session-Store derselben Unterhaltung sind keine
unabhaengigen Bestaetigungen.

| ID      | Auffindbare Quelle                                                                                                                                                                                                                                                                                                           | Ereignis-/Versionsstand                         | Eingesehener Umfang und Grenze                                                                                                                           |
| ------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ----------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------- |
| SRC-001 | [Grundaufbau, Session 8cbb89ec](<../Sessions/add the recommended vscode extensions for c%23 to the workspace recommendations__copilot-8cbb89ec-a6f6-46ae-8355-b8e4ea22a661.md>)                                                                                                                                              | 2026-10-04 bis 2026-10-06                       | Nutzerpassagen aller Phasen; Zeilen 1-110 mit Originalplan/Freigabe; lange Phasenantworten nicht vollstaendig gelesen                                    |
| SRC-002 | Projektgebundener Session-Store `bb5acc8d-8588-4764-bb1c-856fad07c9f4`                                                                                                                                                                                                                                                       | 2026-10-06; dokumentiert 2026-10-22             | Turn 8 Originalziel/Gapanalyse, Turn 526 Fortsetzung, 614 Freigabe, 974/2421/3051/3889/4944/5983 Phasenfortsetzung; Antwortauszuege, keine ganze Sitzung |
| SRC-003 | [Zehn Passen, Session 5e0d365e](<../Sessions/do this ten times in a row___copilot-5e0d365e-74af-4c5e-91e0-597747fac528.md>)                                                                                                                                                                                                  | 2026-10-10/11                                   | Nutzerauftraege; Zeilen 180-235 mit Pfadbeschwerde, begrenzter Ursachenaussage und AGENTS-Aenderung                                                      |
| SRC-004 | [Workflow-Erstellung, Session 14b7ba79](<../Sessions/do this ten times in a row___copilot-14b7ba79-e27e-4a7b-b275-4c4cb2a76f9b.md>)                                                                                                                                                                                          | 2026-10-11                                      | Nutzerauftrag zehn Passen und Workflow; Anfang mit damaliger Prioritaetsinterpretation, nicht heutige Faehigkeitsmatrix                                  |
| SRC-005 | [Implementierung, Session 64ef7ee6](<../Sessions/_missing-priority-implementation 10__copilot-64ef7ee6-9076-42d6-90c6-953f1f6cb4ed.md>)                                                                                                                                                                                      | 2026-10-11                                      | Nutzerpassagen ueber wiederholt nicht reparierte Commit-Linefeeds; keine erneute Pruefung damaliger Git-Rewrites                                         |
| SRC-006 | [Entscheidungsoptionen, Session de977bdf](<../Sessions/adjust %23SKILL.mdto ask for user decisions when those missing is what would otherwise a blocker. In those cases, offer bl__copilot-de977bdf-f93e-4c94-bbde-58519e89310a.md>); [heutiger Projektworkflow](../.github/skills/missing-priority-implementation/SKILL.md) | 2026-10-11; Workflow bei HEAD                   | Export voll gelesen; aktueller Workflow gelesen; Historie nicht als neuer Ausfuehrungsauftrag                                                            |
| SRC-007 | [Navigation, Session d64473bd](<../Sessions/_missing-priority-implementation 10__copilot-d64473bd-e284-49de-9fee-285e1d8714ea.md>)                                                                                                                                                                                           | 2026-10-15                                      | Anfang mit Optionen, Auswahl 1 und Feststellung bereits vorhandenen Verhaltens; spaetere Passenteile nicht voll gelesen                                  |
| SRC-008 | [Brokerentscheidung, Session 09823d4b](<../Sessions/_missing-priority-implementation 10__copilot-09823d4b-175a-4d1d-b312-61a33f6887d9.md>)                                                                                                                                                                                   | 2026-10-15                                      | Nutzerpassagen und Zeilen 1310-1385 mit Optionen/Antwort 2; viele eingebettete Rechercheauftraege nicht als Freigaben gewertet                           |
| SRC-009 | [Commits, Session 1f8a6295](<../Sessions/_missing-priority-implementation 10__copilot-1f8a6295-78a4-4a6f-84a9-f30ce8862b8d.md>)                                                                                                                                                                                              | 2026-10-15/16                                   | Nutzerpassagen zu zehn Passen und Commit-Linefeed-Schaden; nicht alle Implementierungsergebnisse geprueft                                                |
| SRC-010 | [CSP/Navigation, Session 30b94849](<../Sessions/_missing-priority-implementation 10__copilot-30b94849-4f45-4ccc-bb33-426100edf56f.md>)                                                                                                                                                                                       | 2026-10-16 bis 2026-10-18                       | Nutzerpassagen, Zeilen 1-130 mit style-src-elem, Auswahl 1 und UIR; spaetere Meldungen nur erschlossen                                                   |
| SRC-011 | [Startskript, Session ed78a0d4](<../Sessions/Can you write a short script to start the app on a given URL, with all the necessary arguments, without changing any sou__copilot-ed78a0d4-daf4-4c5f-92ce-b96e42d461f8.md>); [Skript](../scripts/run-browser.sh)                                                                | 2026-10-18; Skript bei HEAD                     | Export und Skript voll gelesen; historischer Startbericht nicht hier wiederholt                                                                          |
| SRC-012 | [GitHub-Ziel, Session 4daa2c27](<../Sessions/Add to %23Missing.md the end goal that the browser should be able to handle the github website.__copilot-4daa2c27-3190-4e71-8556-2de9c2eccbca.md>)                                                                                                                              | Nutzer 2026-10-18T17:02:02.306Z                 | Datierte Nutzeranforderung erschlossen; mit aktuellem End-goal-Abschnitt abgeglichen                                                                     |
| SRC-013 | [CORS, Session b4f3d2c2](<../Sessions/_missing-priority-implementation 10__copilot-b4f3d2c2-c26f-461d-84cc-e86567a17244.md>)                                                                                                                                                                                                 | 2026-10-18                                      | Nutzerpassagen, Zeilen 1-100 mit Optionen/Antwort 1, Umsetzungsmeldung und Beginn Bestaetigungsarbeit                                                    |
| SRC-014 | [Retained events, Session f71c4f6a](<../Sessions/_missing-priority-implementation 10__copilot-f71c4f6a-320f-4c8d-8824-92c4fbc2e397.md>)                                                                                                                                                                                      | 2026-10-18/19                                   | Nutzerpassagen und Zeilen 70-190 mit UIR-Korrektur, Politik-, click- und Host-Freigabe; nicht gesamte Implementierung gelesen                            |
| SRC-015 | [Adressleiste/Media, Session 20819992](<../Sessions/_missing-priority-implementation 10__copilot-20819992-3582-4dc6-bd49-2251760835a3.md>)                                                                                                                                                                                   | 2026-10-19                                      | Export voll gelesen; unvollstaendige Fortsetzung endet bei Beginn Media-Listen-Inkrement                                                                 |
| SRC-016 | [Commit/behaupteter Kontext, Session 87c5fd1f](<../Sessions/commit staged files__copilot-87c5fd1f-44c6-4716-86c1-3d3960535cd1.md>)                                                                                                                                                                                           | 2026-10-22                                      | Export voll gelesen; Commit/keine Tests/behauptete Kontextanlage; Persistenz mit Dateisystem abgeglichen                                                 |
| SRC-017 | [SDK-Erkennung, Session 84fc76e4](<../Sessions/Given the following, it seems that the newest version is installed, but I still get the error message in the attached pi__copilot-84fc76e4-e6e4-4774-b37c-75b6110793fd.md>)                                                                                                   | 2026-10-10                                      | Export voll gelesen; keine privaten Profile oder Settings neu gelesen/importiert                                                                         |
| SRC-018 | [CI-Fehler, Session 8ed868a7](<../Sessions/Look for current CI failures, and fix them.__copilot-8ed868a7-3350-409d-8dd9-939cdb7b9fbc.md>)                                                                                                                                                                                    | 2026-10-11                                      | Nutzerpassagen zu CI, Data-Ignore und Issue-Erstellung; fachliche Fehlergeschichte anhand GitHub statt Rohlogs                                           |
| SRC-019 | [Commitbeschreibung, Session 2b492391](<../Sessions/commit staged files__copilot-2b492391-45cd-4f73-810a-772f6e08b94a.md>)                                                                                                                                                                                                   | 2026-10-10                                      | Nutzerpassagen: Beschreibung muss tatsaechlichem Diff entsprechen; keine weitere Git-Aenderung dieses Turns                                              |
| SRC-020 | [AGENTS-Diagnostik, Session d61b871f](../Sessions/_fix-customization-evaluation-diagnostics__copilot-d61b871f-c0dc-46a3-b74b-7009d612f903.md)                                                                                                                                                                                | 2026-10-11                                      | Nutzerpassagen erschlossen; Diagnostik nicht erneut ausgefuehrt                                                                                          |
| SRC-021 | [AGENTS-Diagnostik, Session 7fda37cc](../Sessions/_fix-customization-evaluation-diagnostics__copilot-7fda37cc-8b62-4148-9723-fbbdc9b89bc3.md)                                                                                                                                                                                | 2026-10-11                                      | Nutzerpassagen erschlossen; kein neuer Customization-Auftrag                                                                                             |
| SRC-022 | [AGENTS-Diagnostik, Session dcbcd2e3](../Sessions/_fix-customization-evaluation-diagnostics__copilot-dcbcd2e3-e1ef-4e87-a370-278ab1383f68.md)                                                                                                                                                                                | 2026-10-11                                      | Nutzerpassagen erschlossen; Details nicht rekonstruiert                                                                                                  |
| SRC-023 | [Workflow-Diagnostik, Session fd3df3ea](../Sessions/_fix-customization-evaluation-diagnostics__copilot-fd3df3ea-3e20-4a8c-8cc0-2574159eabc3.md) und [c63e546e](../Sessions/_fix-customization-evaluation-diagnostics__copilot-c63e546e-0027-434c-8db3-892e4f58e018.md)                                                       | 2026-10-11                                      | Nutzerpassagen erschlossen; heutigen Workflow separat gelesen                                                                                            |
| SRC-024 | [Migration, Session 96f045ff](../Sessions/_migrate-customizations__copilot-96f045ff-c44e-40c7-930b-7457e13aff41.md)                                                                                                                                                                                                          | 2026-10-10                                      | Im Inventar/Passagenextrakt erfasst; private Migration bewusst nicht in Projektwissen uebernommen                                                        |
| SRC-025 | [AGENTS](../AGENTS.md), [Index](../docs/README.md), [Architektur](../docs/architecture/overview.md), [Projektkarte](../docs/project-map.md), [Roadmap](../plans/browser-foundation-plan.md), [ADR](../docs/adr/0001-browser-foundation.md), [Standards](../docs/standards.md), [README](../README.md)                        | HEAD `15e18d5`, gelesen 2026-10-22              | Index/ADR/README gelesen; Architektur Anfang, Projektkarte Anfang, Roadmap Zeilen 1-100, Standards Anfang; keine gesamte Phase-11-Historie neu auditiert |
| SRC-026 | [Issue #6](https://github.com/khmseu/VisualWeb/issues/6), [Issue #8](https://github.com/khmseu/VisualWeb/issues/8), [PR #9](https://github.com/khmseu/VisualWeb/pull/9); PR #1-4/#7 im GitHub-Inventar                                                                                                                       | 2026-10-05 bis 2026-10-10                       | #6/#8 Bodies und leere Kommentare, #9 Body/Merge gelesen; #1-4/#7 Titel, Zustand und Mergezeit, nicht alle Diskussionen                                  |
| SRC-027 | [Issue #10](https://github.com/khmseu/VisualWeb/issues/10), [PR #11](https://github.com/khmseu/VisualWeb/pull/11)                                                                                                                                                                                                            | 2026-10-11/12                                   | Bodies und Merge-Metadaten gelesen; alte Fehlermeldung und spaeterer Fix getrennt                                                                        |
| SRC-028 | [PR #5](https://github.com/khmseu/VisualWeb/pull/5)                                                                                                                                                                                                                                                                          | gemergt 2026-10-08T17:15:16Z                    | Body und Merge gelesen; spaetere CORS-Weiterentwicklung in SRC-013                                                                                       |
| SRC-029 | [PR #12](https://github.com/khmseu/VisualWeb/pull/12), [README-Lizenzen](../README.md#licensing)                                                                                                                                                                                                                             | gemergt 2026-10-13T17:32:49Z                    | Body/Merge und README gelesen; Notices verlinkt, nicht vollstaendig neu geprueft                                                                         |
| SRC-030 | [Issue #14](https://github.com/khmseu/VisualWeb/issues/14), [PR #15](https://github.com/khmseu/VisualWeb/pull/15)                                                                                                                                                                                                            | 2026-10-19                                      | Bodies und Merge gelesen; Dokumentations-Nachweise auch im Tracker                                                                                       |
| SRC-031 | [Scripting](../docs/scripting.md#retained-link-click-dispatch-pass-5), [PageRendering README](../src/Apps/VisualWeb.PageRendering/README.md), [StaticPageRenderer](../src/Apps/VisualWeb.PageRendering/StaticPageRenderer.cs)                                                                                                | HEAD                                            | Scripting Zeilen 186-227/gezielte Suche, PageRendering Anfang, Code Zeilen 20-45/gezielte Suche; keine volle Event-Auditierung                           |
| SRC-032 | Git-Historie/`git show 21b8010`; [Missing](../plans/Missing.md); GitHub `main`/CI                                                                                                                                                                                                                                            | HEAD, Abfrage 2026-10-22                        | Fruehe Commitliste und Tracker-Commits, aktuelle Commitstatistik/-body; Tracker Anfang und Zeilen 285-404; CI-Run-/Jobstatus, keine gesamten Logs        |
| SRC-033 | Aktuelle Unterhaltung, Session `ca8c03c7-0154-413c-b784-761a7040d2fc`                                                                                                                                                                                                                                                        | Nutzerauftrag 2026-10-22 (Zeitangabe UTC+02:00) | Auftrag: `/kontextprotokoll`, Argument `rebuild, including stuff from the Sessions dir and from github`; diese Dateien sind dauerhafte Zusammenfassung   |

## Eintraege

### KP-001 - Urspruenglicher Auftrag und Freigabe

- Dokumentiert am: 2026-10-22.
- Ereignisdatum: 2026-10-04T13:39:03.781Z und 13:56:30.520Z.
- Erfassungsart: nachtraeglich rekonstruiert.
- Quellen: SRC-001 Turn 2/3, SRC-025 Roadmap/ADR.
- Bezug: REQ-001 bis REQ-004, DEC-001.

**Nutzerangabe:** Voller C#-Browser, getrennte Subprojekte, gekapselte
OS-Abhaengigkeiten, mindestens Linux/Windows, Tabs/Fenster und moeglichst
starke Isolation. Minimaler erster Stand, aber Architektur fuer uebliche
Webstandards. Motive explizit: Standards-Cache soll Netzwerkverkehr reduzieren;
Dokumentation soll fuer AI-Agenten auffindbar sein.

**Vorschlag des Assistenten:** zehn Grundphasen, SDL3/Skia/HarfBuzz, xUnit v3,
.NET 10 und x64/arm64; JS-Alternativen Jint, V8/ClearScript oder Eigenbau;
UI-Alternativen Silk.NET oder direkte OS-Bindings.

**Beschlossen:** Nutzer verlangt X11 und Wayland, V8 und offizielle
Referenzen/monatlichen Cache unabhaengig von Tests; sonst Empfehlungen
freigegeben. Jint/eigene JS-Engine sind damit nicht der gewaehlte Einstieg;
weitergehende persoenliche Ablehnungsgruende nicht ueberliefert.

**Korrektur:** Die damalige Aussage, der Ordner sei noch kein Git-Repository,
wurde vom Nutzer explizit berichtigt. Tool-Sandbox-/Netzwerkprobleme und das
spaetere Abschalten der damaligen Agent-Sandbox betreffen Entwicklungswerkzeuge,
nicht eine Erlaubnis fuer unsandboxed Browser-Renderer oder feindliche Inhalte.

**Folge:** Roadmap und ADR sind die bestehenden Autoritaeten; keine zweite
Planfreigabe durch diese Rekonstruktion.

### KP-002 - Phasenlieferung und Sicherheitsgrundlage

- Dokumentiert am: 2026-10-22.
- Ereigniszeitraum: 2026-10-04 bis 2026-10-07.
- Erfassungsart: nachtraeglich rekonstruiert.
- Quellen: SRC-001 Nutzerfortsetzungen, SRC-002 ausgewaehlte Turns,
  SRC-025 historische Phasen, SRC-032 Git.
- Bezug: REQ-001/003, DEC-002/003.

**Belegt als Historie:** Git nennt Foundation `2e7d191`, Plattform `cd89fc4`,
Core `4255cb9`, Netzwerk `029bedc`, HTML `3ea48e7`, CSS `0209bad`,
Layout `4b9c5e4`, Paint `2b3ae3c`, Shell `ec5dd3c`, Prozesse `9a2d24c`,
Linux-Confinement `1dadafa`, Ressourcen `57d482f`, Recovery `bb55650`.
Nutzer fordert im Export phasenweise Commit und Fortsetzung bis Phase 11.
Commitbezeichnungen beweisen nicht alle damaligen Testresultate.

**Roadmapbericht, nicht neu wiederholte Freigabe:** First-party HTML-Parser;
URL-Paket wurde nach WPT-Fehlern hinter eigener API als MIT-Source vendort;
Unicode 17 als stabiler IDNA-Stand. Spaetere Phasen nutzen begrenzte
V8/DOM-/Events statt allgemeinem HTML-Event-Loop. Urspruengliche
Subset-Ausschluesse sind historische Grenzen, nicht fuer immer fehlende Features.

**Nutzerfreigabe in SRC-002 Turn 614:** Origin-Identitaetsarbeit; spaetere
Fortsetzungen betreffen finale Dokument-Origins, opt-in Same-Origin-GET,
Bad Ports, Charset-Prescan und Selektoren. Gesamtfreigabe fuer alle
Produktions-Sicherheitsmechanismen ist daraus nicht abzuleiten.

**Artefaktfolge:** CLI-Confinement `bf35d4c`, Origin-Rendererrotation `8e6ca08`,
Session-HSTS `17ba3d0` und bounded Scroll/Links/Stylesheets folgen in Git.
Prozesswechsel bleibt nicht Site-/Frame-Isolation; Teilpolicies nicht
Produktionssicherheit. Detailgruende fuer jeden einzelnen Commit sind ohne
weitere Quellen nicht rekonstruiert.

### KP-003 - Plattformparitaet und native Nachweise

- Dokumentiert am: 2026-10-22.
- Ereigniszeitraum: 2026-10-05 bis 2026-10-10; aktuelle Nachweise KP-014.
- Erfassungsart: nachtraeglich rekonstruiert.
- Quellen: SRC-026, SRC-032.
- Bezug: REQ-002, Q-003.

**Nutzerangabe auf GitHub:** #6 fordert Windows-Paritaet mit Linux;
#8 ARM-Paritaet mit Intel. Begruendung ueber diese Paritaetsforderung hinaus
nicht ueberliefert.

**Belegt:** PR #1-3 betreffen Windows-Confinement/Parity/Smokes; PR #4 native
Linux-ARM64-CI; #7/#9 vollstaendige Windows-Solutiontests. #9 ist
2026-10-10T05:40:52Z gemergt, #7 05:42:18Z. Titel allein sind noch keine
erfolgreichen nativen Laeufe.

**Folge:** Veraltete lokale x64-only-Evidenz nicht als dauerhafte ARM-Luecke
behandeln. OS/Architektur-CI und echte Wayland/IME/high-DPI-Desktoppruefungen
bleiben verschiedene Nachweise.

### KP-004 - Arbeitsablauf, Pfade und Commit-Qualitaet

- Dokumentiert am: 2026-10-22.
- Ereigniszeitraum: 2026-10-10 bis 2026-10-16.
- Erfassungsart: nachtraeglich rekonstruiert.
- Quellen: SRC-003 bis SRC-006, SRC-009, SRC-019 bis SRC-023.
- Bezug: REQ-005/006, Q-006.

**Nutzerangabe:** Mehrfach zehnmal naechste hoechste Missing-Prioritaet
implementieren und jeweils committen. Spaeter fordert der Nutzer einen
wiederverwendbaren Skill/Workflow und Optionen inklusive Arbeitsblockade,
wenn eine fehlende Entscheidung sonst blockiert.

**Beschlossen/Artefakt:** Der heutige Workflow verlangt konkrete bereits
gepruefte Inkremente, getrennte Tests/Commits/Push/CI; fehlende materielle
Entscheidungen sind vor Implementierung abzufragen. Keine spekulativen oder
leeren Commits nur zum Erreichen einer Zahl.

**Nutzererfahrung:** Wiederholt falscher Browser-Projektpfad ausserhalb der
Repositorywurzel; Bitte, Wurzelanker in AGENTS aufzunehmen. Die damalige
Antwort nennt Pfadkonstruktion nur als wahrscheinliche Ursache, da konkrete
Fehlversuchslogs fehlten. Nicht nachtraeglich als bewiesene Root Cause darstellen.

**Nutzererfahrung:** Beschreibungen passten nicht zum Diff; mehrfach
Linefeed-Schaden in unpushed Commitmessages, einmal trotz vorheriger
Aufforderung weiter vorhanden. Konsequenz: Committexte am echten Diff
orientieren, Reparatur tatsaechlich verifizieren. Das ist keine aktuelle
Erlaubnis, Historie zu amendieren oder zu rewriten.

**Offen:** Kein globaler Restzaehler der wiederholten Zehnerauftraege
rekonstruiert; Sessions unterbrechen sich und Antworten enthalten auch
delegierte Arbeit. Der aktuelle Auftrag ist nur Kontextrekonstruktion.

### KP-005 - SDK-Erkennung und revidierte Diagnose

- Dokumentiert am: 2026-10-22.
- Ereignisdatum: 2026-10-10T21:15:48.073Z bis 21:31:04.320Z.
- Erfassungsart: nachtraeglich rekonstruiert.
- Quelle: SRC-017.

**Nutzerangabe:** Terminal meldet SDK 10.0.401, VS Code meldet weiterhin Fehler;
nach erster Empfehlung: weiterhin nicht funktionierend.

**Historische Korrektur des Assistenten:** Erste Empfehlung vermischte
Extension-Runtimepfad und SDK-Auswahl. Spaetere Antwort berichtet eine
unexpandierte quoted-Tilde im PATH und User-Level-Korrekturen. Berichteter
Nachweis: frische Login-Shell findet SDK 10.0.401; C# Doctor nach vollem
Neustart blieb ungeprueft.

**Grenze:** Keine heutigen privaten Shell-/Settings-Dateien eingesehen,
keine Machine-Fixes vorgenommen, keine dauerhafte VS-Code-Erfolgsaussage
aus dem Export. Fuer Folgearbeit Projekt-SDK-Pin und reale Prozessumgebung
getrennt pruefen.

### KP-006 - Windows-Fehlerbehebung und Lizenzinventar

- Dokumentiert am: 2026-10-22.
- Ereigniszeitraum: 2026-10-11 bis 2026-10-13.
- Erfassungsart: nachtraeglich rekonstruiert.
- Quellen: SRC-018, SRC-027, SRC-029.
- Bezug: DEC-008, Q-008.

**Nutzerauftraege im Export:** CI-Fehler suchen/reparieren, staged Diffs vor
Commit lesen; spaeter Issue fuer verbleibendes Windows-Problem erstellen.

**Belegt als damaliger Issue-Stand:** #10 dokumentiert CreateProcessW error 87
auf Windows x64/ARM64, mehrere erfolglose Vorversuche und native
Akzeptanzkriterien. Nicht required Sandbox-Attribute entfernen oder
unsandboxed retry als Abkuerzung.

**Spaetere Artefaktloesung:** PR #11 benennt vertauschte stdout/stderr-
CreatePipePair-Ausgaenge: non-inheritable Parent-Readhandles statt
Child-Writehandles in der Handle-Liste. Kind-zuerst-Ausgabeordnung repariert,
ABI-/Streamregressionen hinzugefuegt; AppContainer, private Pipes,
Read-only-Storage, Job-Zuordnung und Fail-closed erhalten. Merge
2026-10-12T06:58:02Z, `03ee760`. PR-Text meldet damals noch pending native
Approval; spaetere CI-Evidenz ist davon getrennt.

**Separates Repositorythema:** PR #12 fuehrt First-party-MIT und
Third-party-Notices ein, ohne Fremdmaterial umzuschreiben.
Lizenz-/Provenienz-/Distribution-Luecken werden offen genannt.
Persoenliche Lizenzmotivation oder ausdrueckliche Chat-Freigabe ist hier
nicht rekonstruiert; Merge und README sind Artefaktbelege.

### KP-007 - Cross-Origin-Navigation und Brokerentscheidung

- Dokumentiert am: 2026-10-22.
- Ereignisdaten: 2026-10-15T05:50:25.929Z und 15:25:42.918Z.
- Erfassungsart: nachtraeglich rekonstruiert.
- Quellen: SRC-007 Turn 2, SRC-008 Turn 43.
- Bezug: DEC-004, Q-002.

**Alternativen:** Same-Origin-only fuer Page-Navigation (staerkere Grenze,
bricht normale Websitewechsel); trusted user activation via Browser-Broker;
alle page-initiated Navigationen (naehere Browsersemantik, unsolicited requests
brauchen mehr Schutz); weitere Arbeit blockieren.

**Beschlossen:** Nutzer waehlt zuerst user-activated Cross-Origin-Links,
spaeter Option 2 trusted Broker-Navigation fuer Links/Forms. Adressleiste
bleibt separat, Renderer bekommen keine Netzautoritaet, Redirects werden
neu autorisiert. Begruendung in Optionen: normale Navigation erhalten ohne
allgemeine SOP/CORS-Behauptung.

**Unsicherheit:** Erste Session stellt danach fest, gewaehlte Linksemantik
sei bereits implementiert, und geht zu Anchor-Blockgeometrie ueber. Das
beweist nicht, dass die ganze Produktionsprioritaet fertig war.
Aktuelle Bestaetigungspolitik wird spaeter praezisiert, nicht daraus
eine generelle Freigabe fuer scriptinitiierte Requests ableiten.

### KP-008 - CSP-Teilpolicies und Erhalt der Navigation

- Dokumentiert am: 2026-10-22.
- Ereignisdatum: 2026-10-16T01:50:27.484Z; vorheriger Pass in derselben Sitzung.
- Erfassungsart: nachtraeglich rekonstruiert.
- Quellen: SRC-010 Zeilen 1-130, SRC-032.
- Bezug: DEC-004/006, Q-002/007.

**Historischer Implementierungsbericht:** `38a985c` fuehrt
style-src-elem fuer style elements, Links und Imports mit style-src/default-src-
Fallback ein; style attributes bleiben eigener Pfad.
Bericht nennt bestandene Browser-/Solutiontests und pre-existing
Formatterbefunde, hier nicht erneut ausgefuehrt.

**Alternativen und Beschluss:** Cross-Origin-Navigation erhalten,
auf Committed-Origin beschraenken oder blockieren. Nutzer waehlt 1:
erhalten. Folgeinkrement ist UIR nur fuer gebrokerte Stylesheets/Imports,
nicht eine stille Navigationssperre.

**Folge:** CSP- und UIR-Teilumsetzung nicht als allgemeine CSP/Fetch-Autorisation
bezeichnen. Spaterer navigation-specific UIR-Umfang steht in KP-010.

### KP-009 - CORS-Teilumfang und Navigation-Bestaetigung

- Dokumentiert am: 2026-10-22.
- Ereignisdatum: 2026-10-18T09:03:45.185Z; Bestaetigungsarbeit spaeter.
- Erfassungsart: nachtraeglich rekonstruiert.
- Quellen: SRC-013 Zeilen 1-100, SRC-028, SRC-032 Tracker.
- Bezug: DEC-005/006.
- Praezisiert: KP-007; ersetzt reine Same-Origin-Stylesheet-Grenze aus PR #5
  nur fuer den neuen Cross-Origin-CORS-Pfad.

**Ausgangslage:** PR #5 hatte Stylesheets absichtlich strenger als HTML auf
die finale Dokument-Origin beschraenkt, einschliesslich Redirects vor Transport.

**Alternativen:** bounded CORS-Stylesheets, vorhandene Same-Origin-Grenze
behalten und andere benannte Arbeit freigeben, oder blockieren.
Nutzer waehlt CORS-Option 1.

**Beschluss/berichtetes Artefakt `b080f32`:** Origin-Header der Seite,
genau eine passende finale ACAO-Antwort oder `*`, Cookies aus, Redirects
nur innerhalb initialer effektiver Stylesheet-Origin. Kompatibilitaet wird
gezielt erweitert; kein full Fetch/CORS.

**Nachfolgende Bestaetigung:** Antwort meldet Arbeit fuer Cross-Origin-
Page-Links/Forms. Die hier gelesenen Passagen enthalten keine separate
urspruengliche Nutzerantwort dafuer; spaetere explizite Freigabe zur
Beibehaltung steht in KP-010 und ist der belastbare Beschluss.
Heutiger Tracker beschreibt Prompts vor Transport und fuer jeden
origin-changing Redirect, auch Rueckkehr zu frueherer Origin;
Adresse bleibt bis Zustimmung unveraendert.

### KP-010 - Bestaetigte Politik und UIR-Korrektur

- Dokumentiert am: 2026-10-22.
- Ereignisdatum: 2026-10-18T17:51:52.054Z; Inkremente 2026-10-18.
- Erfassungsart: nachtraeglich rekonstruiert.
- Quellen: SRC-014 Zeilen 70-190, SRC-032 Git/Tracker.
- Bezug: DEC-006, Q-002.
- Praezisiert: KP-007/008/009.

**Belegt als Richtungswechsel:** `884bc5b` fuehrt insecure Page-Link-Upgrades
ein; `0b5ee5d` korrigiert den Umfang nach Standardspruefung auf
same-origin Links/Redirects. Cross-Origin-Links und Adressleistenrequests
erben die Dokument-UIR-Politik nicht; Forms behalten ihre dokumentierte
Cross-Origin-Ausnahme. Die erste zu breite Umsetzung ist kein weiter
gueltiger Beschluss.

**Alternativen und Beschluss:** Confirmation-based Navigation behalten,
Cross-Origin-Links/Forms sperren, oder blockieren. Nutzer waehlt 1:
bestehende Bestaetigungspolitik und bounded development work; allgemeine
Produktionsautorisation bleibt deferred.

**Artefaktfolge:** `9a335de` signalisiert UIR auf HTTP-Main-Document-Hops,
`d3d3922` auch HTTPS ohne Preloadliste; Subresources nicht.
Header-Signal ist keine URL-Umschreibung. `dc91197` wartet in einem
Form-Redirect-Test auf den echten Prompt vor Enter.

**Nachweisgrenze:** Export meldet zeitweise lokale flaky Process-Tests und
unabhaengige Windows-Timeouts. Daraus keine pauschale aktuelle Regression
oder Erfolgsaussage ableiten; KP-014 trennt spaetere CI.

### KP-011 - Startskript und GitHub-Endziel

- Dokumentiert am: 2026-10-22.
- Ereignisdaten: 2026-10-18T11:01:54.005Z und 17:02:02.306Z.
- Erfassungsart: nachtraeglich rekonstruiert.
- Quellen: SRC-011/012, SRC-032 Tracker-End-goal.
- Bezug: REQ-007/008, Q-001.

**Nutzerangabe:** Kurzes Startskript fuer eine gegebene URL mit allen
notwendigen Argumenten, ohne bestehende Source zu aendern.
Vorhandenes Skript baut zuerst, setzt empfohlenes confined Multiprocess,
Renderer und gepinnte Schrift; unsandboxed ist separater expliziter Opt-in.

**Historischer Bericht:** data-URL-Start ohne Fehler, nach 60 Sekunden
gestoppt und keine Restprozesse; keine visuelle Renderpruefung. Hier wurden
nur Export und Skript gelesen, nicht der Start wiederholt.

**Nutzerangabe:** Endziel in Missing aufnehmen, Browser soll GitHub-Website
handhaben koennen. Tracker enthaelt den Satz. Keine Funktionen oder
Abnahmekriterien vereinbart, insbesondere kein nachgewiesenes Login,
Issues/PRs, JS-App-Verhalten oder safe-untrusted browsing. Q-001 bleibt offen.

### KP-012 - Eng begrenzte Verlaengerung des Script-Hosts

- Dokumentiert am: 2026-10-22.
- Ereignisdaten: 2026-10-18T18:54:24.929Z und 22:05:59.435Z.
- Erfassungsart: nachtraeglich rekonstruiert.
- Quellen: SRC-014 Turn 3/4, SRC-031, SRC-032 Git.
- Bezug: DEC-007, Q-004.
- Praezisiert: fruehere Phase-11-Historie in KP-002, nicht deren Rohquelle
  stillschweigend umgeschrieben.

**Nutzerfreigabe:** Bounded Shell-to-page click-Inkrement statt Image-Loading
oder Blockieren. Danach eigene Lifecycle-Entscheidung: opted-in V8/Listener
idle behalten statt Clicks wegen bisheriger Dispose-after-batch-Grenze
zu deferieren. Nutzer waehlt jeweils 1.

**Ueberlieferter Grund:** Spaetere echte Klicks koennen Listener nur mit
verlaengerter Host-Lebensdauer erreichen; Runtime/Ressourcen-Lebensdauer wird
gezielt erweitert, nicht ein kompletter HTML-Event-Loop genehmigt.

**Artefakte:** `de5b8ef` retained click; `1468de0` fokussierte Links,
`102b463` Ctrl-click, `bc22600` cancelable auxclick. Aktuelle Fachquelle
beschreibt native plain Events, rendererlokale Identitaet, 1024 gemeinsame
click/auxclick-Events pro Dokument, Deadline/Budgets und Hostinvalidation
bei Fehlern bis Reload. Resize/Scroll rerun keine Scripts.

**Gezielt im Code belegt:** DocumentState.Host, Release/Dispose und 1024-
Grenze gelesen. Das ist kein Gesamt-Audit der Event-/IPC-Implementierung.

**Widerspruch:** Allgemeine/historische Architektur- und Scripting-Saetze
lassen Listener vor retained repaint enden bzw. nennen persistent host
deferred. Aktueller spezifischer Dispatch-Abschnitt und Code zeigen die
enge Ausnahme. Allgemeine Timer/Tasks/Input-/Formevents bleiben deferred.

### KP-013 - Adressleiste und Medienlisten

- Dokumentiert am: 2026-10-22.
- Ereignisdatum: Nutzer 2026-10-19T05:10:31.501Z; Commit 2026-10-22.
- Erfassungsart: nachtraeglich rekonstruiert.
- Quellen: SRC-015 Turn 2, SRC-016, SRC-031/032.
- Bezug: DEC-006, Q-006/007.
- Praezisiert: KP-010.

**Alternativen:** Adressleiste uneingeschraenkt behalten und naechste
Usability-Prioritaet, auch explizite Navigation bestaetigen oder blockieren.
Nutzer waehlt 1. Kein erneuter Blocker aus bereits entschiedener Politik.

**Artefaktentwicklung:** `71dbd33` ignoriert print-only Stylesheets im
Screen-Renderer; `ece6829` einfache Media Types in Imports; `21b8010`
comma-separated genaue all/screen/print-Listen ueber Links, eingebettete
Styles und Imports. Nicht passende Listen ignorieren, bedingte Media
Queries weiter ablehnen.

**Nachweisgrenze:** Export SRC-015 endet nach Beginn des Listeninkrements;
SRC-016 berichtet Commit der acht staged Dateien ohne neue Tests.
Git-Body/-Stat und PageRendering-README sind gelesen, nicht die gesamte
Codeaenderung oder alle neuen Testfaelle. Aktuelle Remote-CI in KP-014.

### KP-014 - Aktuelle Artefakte, CI und Dokumentwidersprueche

- Dokumentiert am: 2026-10-22.
- Ereignisse: Windows-Recovery 2026-10-19; aktueller Abfragestand 2026-10-22.
- Erfassungsart: nachtraeglich rekonstruiert und aktueller Artefaktabgleich.
- Quellen: SRC-016/025/030/031/032.
- Bezug: Q-003 bis Q-007.

**Historischer Fehler und Fix:** #14 beschreibt Windows-x64-CPU-pressure-
Recovery-Timeout nach Cancellation, nicht bestaetigte Ctrl-click-Regression.
PR #15 berichtet spaetere Reproduktion auch auf ARM64 und Testcontention:
nonparallel Process-Testcollection, Assertions fuer old-worker exit,
neue PID, exakte Pixel und Channel-Reuse; Deadline 5s und Cancellation
500ms bleiben. Windows-CI wiederholt Recovery-Faelle fuenfmal.
Merge `53853f8` am 2026-10-19T04:44:49Z; Produktion unveraendert laut PR.

**Aktuell abgefragt:** HEAD und GitHub main
`15e18d5d21f8b5c59594a1670f5dcd91246247c6`; vor Schreibbeginn sauberer
Worktree. Commit `15e18d5` ignoriert Sessions; lokale Exporte sind damit
nicht durch ihren Standort automatisch versioniert.

| Stand/Run                                                                                                                                                                                           | Gelesenes Ergebnis am 2026-10-22               | Grenze                                                                   |
| --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ---------------------------------------------- | ------------------------------------------------------------------------ |
| [HEAD Linux 39859939182](https://github.com/khmseu/VisualWeb/actions/runs/39859939182)                                                                                                              | completed success; Linux x64 und arm64 success | Jobmetadaten, keine ganzen Logs oder Desktop-Abnahme                     |
| [HEAD Windows 39859939493](https://github.com/khmseu/VisualWeb/actions/runs/39859939493)                                                                                                            | in_progress; x64 success, arm64 in_progress    | Kein finaler Vollmatrix-Erfolg bei Abfrage                               |
| [HEAD Push 39859938469](https://github.com/khmseu/VisualWeb/actions/runs/39859938469)                                                                                                               | completed success                              | Nicht gleichbedeutend mit allen Native-Tests                             |
| [ece6829 Linux 39210893919](https://github.com/khmseu/VisualWeb/actions/runs/39210893919) / [Windows 39210893828](https://github.com/khmseu/VisualWeb/actions/runs/39210893828)                     | beide completed success                        | Aelterer Stand, nicht HEAD                                               |
| Tracker-Nachweis `bc22600`: [Linux 39194869543](https://github.com/khmseu/VisualWeb/actions/runs/39194869543) / [Windows 39194869712](https://github.com/khmseu/VisualWeb/actions/runs/39194869712) | Tracker nennt erfolgreiche x64/arm64-Laeufe    | In diesem Turn Quelle gelesen, diese beiden Runs nicht separat abgefragt |

**Offene Widersprueche:** README meldet noch keine erfolgreiche
Linux-ARM64-Evidenz; neuere Tracker-/Runbefunde widersprechen dem.
Allgemeine Lifecycle-Doku beschreibt noch alte Host-Grenzen (KP-012).
Tracker cross-check date 2026-10-19 bedeutet nicht, dass alle Aussagen nach
Media-Listen-Commit neu getestet wurden.

**Persistenzkonflikt:** SRC-016 sagt am 2026-10-22T21:41:59.389Z,
Kontextdateien seien initialisiert/untracked. In diesem Turn war Kontext
bei zwei Reads leer und globale Dateisuche darunter fand keine Dateien.
Grund unbekannt; kein rekonstruierter Dateiinhalt vorhanden. Nicht
stillschweigend erfolgreiche fruehere Persistenz behaupten.

### KP-015 - Rekonstruktion und Abdeckung dieses Checkpoints

- Dokumentiert am: 2026-10-22, Europe/Berlin (UTC+02:00).
- Ereignisdatum: 2026-10-22, aktueller Turn.
- Erfassungsart: laufend dokumentiert.
- Quelle: SRC-033; Artefakte/Inventar SRC-001 bis SRC-032.
- Bezug: REQ-009, Q-001 bis Q-008.

**Nutzerauftrag:** Kontext neu aufbauen, einschliesslich Sessions-Verzeichnis
und GitHub. Keine Autorisierung fuer Commit/Push oder neue Browserfeatures.

**Ausgefuehrt:** Bestehende Kontextablage gesucht und zweimal leer gelesen;
die zwei komplementaeren Sichten nach Vorlagen aufgebaut. Alle 24 Exporte
inventarisiert und datierte Nutzerpassagen erschlossen, relevante Plan-/
Optionen-/Antwortbereiche gezielt gelesen; Projekt-Session-Store erst sieben,
dann 30 Tage mit exakter Repository-/cwd-Zuordnung abgefragt.
Ein nicht exportierter frueher Gap-/Origin-Kontext wurde auszugsweise ergaenzt.

**GitHub-Abdeckung:** Repository/main, vier Issues und zehn PRs im Inventar;
Bodies fuer Issues #6/#8/#10/#14 und PRs #5/#9/#11/#12/#15;
neueste ausgewaehlte Runs/Jobs. Keine Vollrekonstruktion aller PR-Reviews,
Kommentare, geloeschten Inhalte oder Workflowlogs. Git-Commitlisten und
ausgewaehlte Bodies dienen Artefaktdatierung, nicht erfundenen Motiven.

**Grenzen/Alternativen:** Keine Rohchatspiegelung, keine privaten
Customization-Migrationen, keine unbekannten Begruendungen ergaenzt.
Dateisystem-/Sitzungs-/GitHub-Evidenz statt alten Erfolgsmeldungen blind zu
glauben. Details ausserhalb gelesener Auszuege bleiben offen.

**Folge:** [Wissensstand](./Wissensstand.md) enthaelt Anforderungen,
Beschluesse, Quellenlandkarte, Widersprueche und einen Folgechat-Startprompt.
Lokale Exporte liegen ignored; portable Chronologie behaelt deshalb auch
Session-ID/Datum und wesentliche Zusammenfassung. Die beiden Kontextdateien
sind bewusst nicht committet oder publiziert. Dokumentation ist keine
GitHub-Kompatibilitaets-, Conformance- oder Sicherheitsabnahme.

**Persistenzpruefung:** Geschriebene Abschnitte erneut gelesen; automatisierte
Pruefung fuer lokale Links/Markdown-Anker, Whitespace, eindeutige IDs und
vollstaendiges Quellenregister bestanden. Drei Dateinamen mit `#` mussten
im Linkziel als `%23` kodiert werden; nach Korrektur bestanden die Linkchecks.
Keine Codeaenderungen oder neuen .NET-Testlaeufe.

### KP-016 - Zehn Implementierungspassen und Sicherheitsentscheidung

- Dokumentiert am: 2026-10-23, Europe/Berlin (UTC+02:00).
- Ereignisdatum: 2026-10-23, aktueller Turn.
- Erfassungsart: laufend dokumentiert.
- Quelle: ausdruecklicher Nutzerauftrag in diesem Turn; gelesene
  Projektanweisungen, `plans/Missing.md`, `Kontext/Wissensstand.md`,
  `docs/README.md`, `docs/architecture/overview.md`, `docs/project-map.md`,
  `plans/browser-foundation-plan.md` und `docs/adr/0001-browser-foundation.md`.
- Bezug: REQ-005, Q-002, DEC-004 bis DEC-007.

**Nutzerangabe:** Der Nutzer startete `/missing-priority-implementation`
mit Argument `10`, also zehn getrennt zu bearbeitende Passen, jeweils mit
Test, Tracker-Update, Commit und Push.

**Belegt:** Der Worktree war sauber auf `main`; lokales HEAD und
`origin/main` standen bei `a22c929`. Der Tracker empfiehlt Security als
Prioritaet 1. Er nennt weiterhin nicht abgeschlossene Produktionsautorisierung,
SOP/CORS/CSP, Frames/Origins und Storage. Q-002 und der Wissensstand halten
fest, dass ein neuer groesserer Sicherheitsumfang eine echte Policy-Entscheidung
benoetigt. Bestehende Beschluesse DEC-004 bis DEC-007 umfassen nur eng
begrenzte Browsernavigation, Stylesheet-CORS, bestaetigte page-initiated
Cross-Origin-Navigation und Link-Event-Hostlebensdauer; sie legen keine
allgemeine Produktionsautorisierung fest.

**Nutzerentscheidung:** Auf die Frage nach der Sicherheitsprioritaet antwortete
der Nutzer `defer_priority_1`. Das wird fuer diesen Zehn-Passen-Lauf als
ausdrueckliche Freigabe verstanden, Prioritaet 1 zurueckzustellen und mit
Prioritaet 2 fortzufahren. Eine Begruendung wurde nicht angegeben; die offene
allgemeine Sicherheitsfrage Q-002 und bestehende DEC-004 bis DEC-007 bleiben
unveraendert.

Der Nutzer waehlt als erste Pass die Korrektur des veralteten Browser-Shell-
Guides, damit dieser Links, Controls, Auswahl, Scrollen und Limits mit dem
aktuellen Stand beschreibt. Das ist ein Dokumentationsinkrement ohne
Laufzeitverhalten.

**Belegt:** In `docs/browser-shell.md` widersprechen die Limit-Abschnitte den
Kontroll-/Linkabschnitten: sie verneinen Formcontrols und Textauswahl; der
Scrollabschnitt nennt Scrollbars nicht implementiert. Die aktuelle
`src/Apps/VisualWeb.Browser/README.md`, `plans/Missing.md`, `ShellTests`,
`FormTests`, `ScrollTests` und `LinkTests` beschreiben bzw. testen diese
Funktionen bereits. Das benoetigt keinen neuen Laufzeit- oder Standardsumfang.

**Weitere offene Klaerung:** Prioritaet 2 hat bereits umfangreiche Links,
Scrolling, Auswahl und einfache GET-Formulare, nennt aber weiter Downloads,
persistente Sitzungen, vollstaendige History und allgemeine Page-Input-
Unterstuetzung als fehlend. Die Browser-README beschreibt aktuelle
Interaktionen; der allgemeine Shell-Guide enthaelt teilweise aeltere Aussagen
und limitiert die Roadmap insbesondere bei automatischen Input-Events. Vor
einer Umsetzung muss daher ein konkreter, roadmap-kompatibler Umfang mit dem
Nutzer gewaehlt werden.

**Status:** Pass 1 bleibt auf die gewaehlte Dokumentationskorrektur begrenzt.
`docs/browser-shell.md` und `plans/Missing.md` wurden angepasst und der Diff
geprueft; `git diff --check` bestanden. Kein Laufzeittest wurde ausgefuehrt,
da keine Codeaenderung oder dokumentationsspezifische Test-Suite betroffen ist.
Commit `503b280` wurde erstellt und nach `origin/main` gepusht. Linux
development und Push-on-main CI stehen erfolgreich; Windows confinement
war zunaechst rot: Windows ARM64 bestand, waehrend Windows x64 bei
`ProcessTests.WindowsCpuPressureCancellationOrDeadlineRestartsOnlyAffectedRenderer(cancel: False)`
einen Renderer-Startup/Render-Deadline-Timeout unter CPU-Druck meldete. Der
Windows confinement Lauf auf dem Basiscommit `a22c929` war erfolgreich; nach
Rerun des fehlgeschlagenen Jobs war Windows x64 erfolgreich. Kein Code wurde
zur Behebung geaendert. Pass 1 ist mit gruener abschliessender CI abgeschlossen.

**Pass 2-Pruefung:** Entsprechend dem Zehn-Passen-Auftrag wurde der
Projektstatus und die verpflichtenden Uebersichten erneut geprueft. Der
Tracker priorisiert weiter Interaktion, nennt Downloads, persistente Sitzungen
und vollstaendige History. Die aktuelle Browser-Implementierung hat nur
fluechtige `NavigationHistory`; eine Suche in Browser-Code und -Tests fand
keine Download- oder Session-Persistenz-Schnittstelle. Die Scripting-Regeln
verbieten automatische Input-/Load-Events im aktuellen Phase-11g-Umfang.
Welches dieser Verhaltensziele und welche Datenschutz-/Speicher- oder
Dateizugriffsgrenzen als Pass 2 gelten sollen, ist nicht festgelegt. Keine
Implementierung begonnen; vor Pass 2 wurde eine weitere Nutzerentscheidung
angefragt.

### KP-017 - Prioritaet 2 zurueckstellen und Prioritaet 3 waehlen

- Dokumentiert am: 2026-10-23, Europe/Berlin (UTC+02:00).
- Ereignisdatum: 2026-10-23, aktueller Turn.
- Erfassungsart: laufend dokumentiert.
- Quelle: ausdrueckliche Antwort des Nutzers in diesem Turn.
- Bezug: REQ-005, DEC-011, Q-009.

**Nutzerangabe:** Nach der Klaerung der offenen Scope-Frage zu Prioritaet 2
waehlte der Nutzer `defer_priority_2` und erlaubte fuer diesen Lauf den
Wechsel zu Prioritaet 3.

**Abstimmung:** Verbleibende Prioritaet-2-Laufzeitfeatures bleiben offen und
werden nicht umgesetzt oder als erledigt markiert. Keine Begruendung wurde
genannt. Das ist ein laufbezogener Reihenfolgebeschluss, keine Spezifikations-
aenderung fuer spaetere Arbeit.

**Naechster Schritt:** Unter Prioritaet 3 ein konkretes, bereits nicht
implementiertes Inkrement aus HTML-Recovery, Layout, Fonts oder CSS anhand
von Quellcode und Tests auswaehlen, bevor der Fachguide und das README
gelesen und ein etwaiger Standardsbedarf geprueft werden.

**Pass-2-Auswahlpruefung:** `HtmlParser` klassifiziert `table`, `tr`, `td`,
`th` und zugehoerige Elemente als explizit unsupported; `TreeTests` prueft
weiterhin, dass einfache Tabellen-Inputs fehlschlagen. Der HTML-Projekt-README
beschreibt diese Grenzen. CSS dokumentiert Flexbox/Grid als fehlend. Diese
Kandidaten unterscheiden sich deutlich: Tabellenbaumkonstruktion kann ohne
entsprechendes Layout noch keine Tabellenansicht liefern; Flexbox vergroessert
den Layoutumfang und benoetigt seine eigene exakt begrenzte Geometriespezifikation.
Der Nutzer hat bisher nur die Tracker-Prioritaet freigegeben, nicht diesen
konkreten Umfang. Diese offene Formulierung wurde durch die spaetere Auswahl
der Tabellenbaumkonstruktion als Pass-2-Ziel ueberholt; Details der
Unterstuetzungsgrenze blieben eine umsetzungsbezogene Ableitung.

### KP-018 - Tabellenbaumkonstruktion Pass 2

- Dokumentiert am: 2026-10-23, Europe/Berlin (UTC+02:00).
- Ereignisdatum: 2026-10-23, Fortsetzung nach dem vorigen Checkpoint.
- Erfassungsart: laufend dokumentiert.
- Quelle: vom Nutzer bereitgestellte Gespraechszusammenfassung zur Auswahl
  `table_tree_construction`; lokale HTML-Standardskopie, Code, Tests und
  Validierung dieser Fortsetzung.
- Bezug: DEC-012, Pass 2 des Zehn-Passen-Auftrags.

**Nutzerangabe:** Der Nutzer waehlte Tabellenbaumkonstruktion als naechstes
Prioritaet-3-Inkrement. Die genaue Algorithmen-Untermenge war damit nicht
festgelegt.

**Ableitung und Umsetzung:** Unter Beibehaltung der Projektregel, nicht
implementierte Algorithmen explizit scheitern zu lassen, wurde ein kleiner
statischer Umfang umgesetzt: table-/section-/row-/cell-Modi,
implizite `tbody`/`tr`, Zell-/Zeilen-/Abschnittsschluss, Whitespace,
Kommentare und Zellinhalt aus dem vorhandenen In-body-Subset. Foster
parenting, Caption-/Spaltenkonstruktion und Tabellenlayout bleiben sichtbar
unsupported. Die Auswahl des Bereichs ist Nutzerbeschluss; diese konkrete
Grenzziehung ist eine Ableitung, keine zusaetzliche Nutzerspezifikation.

**Belegte Quellen und Standards:** Die lokale WHATWG-HTML-Kopie aus
`specs/cache/html.html` war laut `specs/manifest.json` am 2026-10-04 abgerufen
und am 2026-10-23 weniger als 30 Tage alt. Gelesen wurden die Abschnitte
`#parsing-main-intable`, `#parsing-main-intabletext`,
`#parsing-main-intbody`, `#parsing-main-intr` und `#parsing-main-intd`;
kein Refresh war nach der 30-Tage-Regel erforderlich. Die konkreten Tests
belegen nur den gewaehlten Teilumfang, keine vollstaendige HTML-Konformitaet.

**Validierung:** `Engine.Html.Tests` (7.218 Tests) und `Engine.Content.Tests`
(7 Tests) bestanden; Solution-Build ohne Warnungen/Fehler und
`dotnet test VisualWeb.slnx --no-build` bestanden. Geaenderte C#-Projekte
bestanden `dotnet format` einzeln. Die loesungsweite Formatpruefung scheiterte
an unveraenderten Findings in `src/Engine/Engine.Layout/StaticLayout.cs` und
`src/Apps/VisualWeb.Browser/ShellChrome.cs`; keine dieser Dateien wurde
geaendert. Der erste loesungsweite Testlauf machte sichtbar, dass der
Content-Test nach Parserunterstuetzung weiterhin eine HTML-Parser-Ausnahme
erwartete; dieser gekoppelte Test wurde auf die weiterhin explizite
`UnsupportedLayoutException` umgestellt, und der erneute volle Testlauf war
gruen.

**Status / naechste Schritte:** Implementierung und lokale Validierung sind
abgeschlossen und als Commit `b9e421c` nach `origin/main` gepusht. Die
Remote-CI ist fuer Linux development
(https://github.com/khmseu/VisualWeb/actions/runs/39871989779), Windows
confinement (https://github.com/khmseu/VisualWeb/actions/runs/39871990125)
und CodeQL (https://github.com/khmseu/VisualWeb/actions/runs/39871989047)
erfolgreich; Pass 2 ist abgeschlossen. Danach gegen den aktualisierten
Tracker eine neue actionable Pass-3-Aenderung bestimmen; keine Tabellenansicht
oder breite Priority-3-Fertigstellung behaupten.

### KP-019 - Colgroup und Col Pass 3

- Dokumentiert am: 2026-10-23, Europe/Berlin (UTC+02:00).
- Ereignisdatum: 2026-10-23, Fortsetzung nach dem Abschluss von Pass 2.
- Erfassungsart: laufend dokumentiert.
- Quelle: aktualisierter `plans/Missing.md`, lokale HTML-Standardskopie,
  aktuelle Parser-/Testquellen und Validierung dieser Fortsetzung.
- Bezug: DEC-012, Pass 3 des Zehn-Passen-Auftrags.

**Arbeitsauswahl (Ableitung, kein neuer Nutzerbeschluss):** Nach dem
tabellenbezogenen Pass 2 zeigte der aktualisierte Tracker weiterhin explizit
unsupported `colgroup`/`col`. Als kleiner separater HTML-Tree-Builder-Schritt
wurde ihre Konstruktion ausgewaehlt; das Nutzerziel blieb Tabellenbaum-
Konstruktion, eine neue Einzelentscheidung wurde nicht eingeholt.

**Umsetzung:** `colgroup` und `col` werden in der In-Column-Group-
Einsetzungsart abgebildet, direkte `col`-Elemente erzeugen eine implizite
Spaltengruppe, und die Gruppen schliessen beim passenden Token-Reprocessing.
Whitespace und Kommentare bleiben im Baum. Foster parenting, Captions und
Tabellenlayout bleiben unsupported; es wird keine Tabellenansicht behauptet.

**Standardsquelle:** `specs/cache/html.html`, Abschnitte
`#parsing-main-intable` und `#parsing-main-incolgroup`; laut Manifest am
2026-10-04 abgerufen und am 2026-10-23 noch innerhalb der 30-Tage-Frist.

**Validierung:** `Engine.Html.Tests` (7.219 Tests), kompletter Solution-Build
ohne Warnungen/Fehler und `dotnet test VisualWeb.slnx --no-build` bestanden.
Projektbezogenes Format ist gruen; die globale Formatpruefung meldet dieselben
unveraenderten Findings in `StaticLayout.cs` und `ShellChrome.cs`. Commit
`a06ae04` wurde nach `origin/main` gepusht. Die CI ist fuer Linux development
(https://github.com/khmseu/VisualWeb/actions/runs/39874104917), Windows
confinement (https://github.com/khmseu/VisualWeb/actions/runs/39874105011)
und CodeQL (https://github.com/khmseu/VisualWeb/actions/runs/39874104981)
erfolgreich.

**Status / naechste Schritte:** Pass 3 ist abgeschlossen. Im aktualisierten
Tracker eine andere unabhaengige, testbare Pass-4-Luecke bestimmen; nicht
Tabellenlayout oder volle Tabellenkonformitaet ableiten.

### KP-020 - Tabellen-Captions Pass 4

- Dokumentiert am: 2026-10-23, Europe/Berlin.
- Ereignisdatum: 2026-10-23.
- Erfassungsart: laufend dokumentiert.
- Quelle: aktualisierter `plans/Missing.md`, `docs/html-dom.md`,
  `specs/cache/html.html`, Parser-/Testquellen und Validierung dieser Pass.
- Bezug: REQ-005, DEC-012, Pass 4 des Zehn-Passen-Auftrags; folgt auf KP-019.

**Auswahl (Ableitung, keine neue Einzelentscheidung):** Der Nutzer hatte
Tabellenbaumkonstruktion als Arbeitsfokus ausgewaehlt. Der aktualisierte Tracker
markierte Captions weiterhin als unsupported. Diese kleine, getrennt testbare
Caption-Insertion wurde als naechster Schritt innerhalb dieses Fokus gewaehlt;
das erweitert nicht die Freigabe auf Foster Parenting, Tabellenlayout oder volle
HTML-Konformitaet. CSS-Media-Queries waren zuvor nur eine moegliche Alternative,
nicht beschlossen.

**Umsetzung:** `HtmlParser` hat eine In-Caption-Insertion-Mode-Erweiterung.
Captions verwenden das bestehende In-Body-Subset; schliessende Tabellen-Tokens
beenden die Caption und werden in Tabellenmodus erneut verarbeitet. Einfache
verschachtelte Elemente funktionieren, waehrend verschachtelte Tabellen in
Captions, Foster Parenting und aktive Formatierungsrekonstruktion weiterhin
explizit unsupported sind. Dokumentation und Missing-Tracker grenzen dies von
Tabellenlayout ab.

**Standardsquelle:** `specs/cache/html.html`, `#parsing-main-intable` und
`#parsing-main-incaption`; Manifest-Pruefung am 2026-10-04, am 2026-10-23
innerhalb der 30-Tage-Frist. Die Offline-Tests bleiben ohne Netzwerkzugriff.

**Validierung:** `Engine.Html.Tests` (7.221 Tests), Solution-Build ohne
Warnungen/Fehler und `dotnet test VisualWeb.slnx --no-build` bestanden.
Projektbezogene Formatpruefung bestanden. Die Solution-Formatpruefung meldet
weiterhin vorhandene Findings in `src/Engine/Engine.Layout/StaticLayout.cs`
und `src/Apps/VisualWeb.Browser/ShellChrome.cs`; diese Dateien sind in diesem
Pass unveraendert. Commit `a8deada` wurde nach `origin/main` gepusht. CI ist
fuer CodeQL
(https://github.com/khmseu/VisualWeb/actions/runs/39877063714), Linux
development (https://github.com/khmseu/VisualWeb/actions/runs/39877063320) und
Windows confinement (https://github.com/khmseu/VisualWeb/actions/runs/39877063319)
erfolgreich.

**Status / naechste Schritte:** Pass 4 ist abgeschlossen. Pass 5 gegen den
aktualisierten Tracker bestimmen, ohne Tabellenlayout oder volle Konformitaet
abzuleiten.

### KP-021 - Text-Foster-Parenting Pass 5

- Dokumentiert am: 2026-10-23, Europe/Berlin.
- Ereignisdatum: 2026-10-23.
- Erfassungsart: abgeschlossen.
- Quelle: aktualisierter `plans/Missing.md`, `docs/html-dom.md`,
  `specs/cache/html.html`, Parser-/Testquellen und Validierung dieser Pass.
- Bezug: REQ-005, DEC-012, Pass 5 des Zehn-Passen-Auftrags; folgt auf KP-020.

**Auswahl (Ableitung, keine neue Einzelentscheidung):** Der Tracker benannte
Foster Parenting inklusive Nicht-Whitespace-Text in Tabellen weiter als Luecke.
Als abgegrenzter Schritt wurde nur die standardgemaesse Behandlung
zusammenhaengender Tabellen-Character-Tokens gewaehlt; Foster Parenting von
Element- oder anderen Tabellen-Tokens bleibt ausserhalb dieses Passes.

**Umsetzung:** `TableTextToken` puffert Character-Tokens bis zum naechsten
Nicht-Character-Token. Whitespace-only-Laeufe bleiben im Tabellenbaum;
gemischte/nichtleere Laeufe werden mit angrenzendem Whitespace vor die naechste
angehaengte Tabelle eingefuegt und mit unmittelbar vorhergehendem Text
zusammengefuehrt. Nicht-Whitespace-Inhalt in `colgroup` meldet den Parsefehler,
schliesst die Gruppe und wird nach den Tabellen-Textregeln erneut verarbeitet.
Andere Foster-Parenting-Algorithmen bleiben explizit unsupported.

**Standardsquelle:** `specs/cache/html.html`,
`#parsing-main-intabletext` und `#foster-parent`; Manifest-Pruefung am
2026-10-04, am 2026-10-23 innerhalb der 30-Tage-Frist. Tests bleiben offline.

**Validierung:** `Engine.Html.Tests` (7.223 Tests), Solution-Build ohne
Warnungen/Fehler und `dotnet test VisualWeb.slnx --no-build` bestanden.
Projektbezogene Formatpruefung bestanden. Die Solution-Formatpruefung meldet
weiterhin die vorhandenen Findings in `StaticLayout.cs` und `ShellChrome.cs`;
diese Dateien sind unveraendert. Commit `d823ba1` wurde nach `origin/main`
gepusht. CI ist fuer CodeQL
(https://github.com/khmseu/VisualWeb/actions/runs/39879836031), Linux
development (https://github.com/khmseu/VisualWeb/actions/runs/39879837214) und
Windows confinement (https://github.com/khmseu/VisualWeb/actions/runs/39879837211)
erfolgreich.

**Status / naechste Schritte:** Pass 5 ist abgeschlossen. Pass 6 erneut anhand
des aktualisierten Trackers waehlen.

### KP-022 - Script- und Style-Tags in Tabellen Pass 6

- Dokumentiert am: 2026-10-23, Europe/Berlin.
- Ereignisdatum: 2026-10-23.
- Erfassungsart: laufend dokumentiert; Pass 6 abgeschlossen.
- Quelle: aktualisierter `plans/Missing.md`, `docs/html-dom.md`,
  `specs/cache/html.html`, Parser-/Testquellen und Validierung dieses Pass.
- Bezug: REQ-005, DEC-012, Pass 6 des Zehn-Passen-Auftrags; folgt auf KP-021.

**Auswahl (Ableitung, keine neue Einzelentscheidung):** Der Tracker benennt
weiterhin unvollstaendige spezialisierte HTML-Algorithmen und grenzt bei
Tabellen die nicht implementierte allgemeine Foster-Parenting-Behandlung ab.
Als kleiner angrenzender Schritt wurden In-Table-Start-Tokens fuer `script`
und `style` gewaehlt: Der Standard leitet sie in die vorhandene
Raw-Text-/Script-Data-Behandlung um, ohne allgemeines Foster Parenting oder
Script-Ausfuehrung freizugeben.

**Umsetzung:** `TableToken` verwendet fuer `style` und `script` die vorhandene
`StartText`-Routine mit Raw-Text bzw. Script-Data. Der urspruengliche
Tabellen-Einfuegemodus wird nach dem Ende des Text-Elements wiederhergestellt.
Tests pruefen die DOM-Position und unveraenderten Textinhalt im Tabellen-,
Abschnitts- und Zeilenkontext. Andere Foster-Parenting-Faelle bleiben
explizit unsupported; Script-Inhalt bleibt inert.

**Standardsquelle:** `specs/cache/html.html`, `#parsing-main-intable` und
`#parsing-main-inhead`; Manifest-Pruefung am 2026-10-04, am 2026-10-23
innerhalb der 30-Tage-Frist. Tests bleiben offline.

**Validierung:** `Engine.Html.Tests` (7.224 Tests), Solution-Build ohne
Warnungen/Fehler und `dotnet test VisualWeb.slnx --no-build` bestanden.
Geaenderte-Dateien-Formatpruefung bestanden. Die Solution-Formatpruefung
meldet weiterhin Findings in `StaticLayout.cs` und `ShellChrome.cs`; beide
Dateien sind identisch zu `HEAD` und nicht Teil dieses Passes. Commit
`255664e` wurde nach `origin/main` gepusht; Linux-, Windows- und Push-CI
sind erfolgreich: CodeQL
(https://github.com/khmseu/VisualWeb/actions/runs/39881851199), Linux
development (https://github.com/khmseu/VisualWeb/actions/runs/39881852620) und
Windows confinement (https://github.com/khmseu/VisualWeb/actions/runs/39881851209).

**Status / naechste Schritte:** Pass 6 ist abgeschlossen. Pass 7 erneut anhand
des aktualisierten Trackers waehlen.

### KP-023 - Versteckte Inputs im Tabellenmodus Pass 7

- Dokumentiert am: 2026-10-23, Europe/Berlin.
- Ereignisdatum: 2026-10-23.
- Erfassungsart: laufend dokumentiert; Pass 7 abgeschlossen.
- Quelle: aktualisierter `plans/Missing.md`, `docs/html-dom.md`,
  `specs/cache/html.html`, Parser-/Testquellen und Validierung dieses Pass.
- Bezug: REQ-005, DEC-012, Pass 7 des Zehn-Passen-Auftrags; folgt auf KP-022.

**Auswahl (Ableitung, keine neue Einzelentscheidung):** Der Tracker fuehrt
weitere fehlende spezialisierte HTML-Algorithmen auf. Der In-Table-Algorithmus
enthaelt eine kleine Ausnahme ohne allgemeines Foster Parenting: `input`
mit `type=hidden` wird trotz Parsefehlers als void Element eingefuegt. Andere
Inputs, die den Alles-andere-/Foster-Parenting-Pfad benoetigen, bleiben
unsupported.

**Umsetzung:** Fuer `input` im Tabellenmodus wird ausschliesslich ein
ASCII-case-insensitives `type=hidden` akzeptiert. Der Parser protokolliert
`hidden-input-in-table` und fuegt das Element ohne Stack-Push in die aktuelle
Tabellenposition ein. Fehlender, nicht passender oder nicht-ASCII-passender
Typ bleibt explizit unsupported. Tests decken direkte Tabellen- und
Zeilenposition, voidness, Originalwert und Diagnose ab.

**Standardsquelle:** `specs/cache/html.html`, `#parsing-main-intable`;
Manifest-Pruefung am 2026-10-04, am 2026-10-23 innerhalb der 30-Tage-Frist.
Tests bleiben offline.

**Validierung:** `Engine.Html.Tests` (7.228 Tests), Solution-Build ohne
Warnungen/Fehler und `dotnet test VisualWeb.slnx --no-build` bestanden.
Geaenderte-Dateien-Formatpruefung bestanden. Die Solution-Formatpruefung
meldet weiterhin die unveraenderten Findings in `StaticLayout.cs` und
`ShellChrome.cs`; beide Dateien sind identisch zu `HEAD` und nicht Teil dieses
Passes. Commit `5920866` wurde nach `origin/main` gepusht. CodeQL
(https://github.com/khmseu/VisualWeb/actions/runs/39884466048), Linux
development (https://github.com/khmseu/VisualWeb/actions/runs/39884466100)
und Windows confinement
(https://github.com/khmseu/VisualWeb/actions/runs/39884466055) sind erfolgreich.

**Status / naechste Schritte:** Pass 7 ist abgeschlossen. Pass 8 aus dem
aktualisierten Tracker waehlen.

### KP-024 - Verschachtelte Tabellen Pass 8

- Dokumentiert am: 2026-10-23, Europe/Berlin.
- Ereignisdatum: 2026-10-23.
- Erfassungsart: laufend dokumentiert; Pass 8 abgeschlossen.
- Quelle: `plans/Missing.md`, HTML/DOM-Guide, Engine.Html README, lokaler
  WHATWG-HTML-Cache, Parser/Testquellen und aktuelle Validierung.
- Bezug: REQ-005, DEC-012, Q-010 und Pass 8 des Zehn-Passen-Auftrags; folgt
  auf KP-023.

**Auswahl (Ableitung, keine neue Einzelentscheidung):** Der Nutzer hatte fuer
Prioritaet 3 Tabellenbaumkonstruktion gewaehlt. Nach Pass 7 war nested-table
recovery die naechste klar begrenzte, sichtbar testbare Luecke im Parser.
Untersucht wurden die In-Table-Startregel, In-Cell-Startregel und das
Insertion-Mode-Reset im vorhandenen Standardcache. Der Umfang bleibt auf die
bereits implementierten Modi begrenzt; Caption-Nesting bleibt wie zuvor
explizit unsupported.

**Umsetzung:** Ein in-scope nested `table`-Start im Tabellenmodus schliesst
die aktuelle Tabelle, setzt den Modus aus dem verbleibenden Stack zurueck und
reprocessiert das Token. Tabellenstart innerhalb einer Zelle folgt dem
In-Body-Pfad, bleibt Kind der Zelle und stellt nach dem inneren
Tabellen-Endtag den Zellmodus wieder her. Das In-Cell-Schliess-Set enthaelt
deshalb kein `table`. Reset-Modi fuer Templates bleiben explizit unsupported.
Tests decken Geschwistertabellen nach Reprocessing und innere Tabellen in
Zellen samt anschliessendem Zelltext ab; der bestehende Unsupported-Test fuer
Caption-Nesting bleibt erhalten.

**Standardsquelle:** `specs/cache/html.html` mit den Abschnitten
`#parsing-main-intable`, `#parsing-main-incell` und
`#reset-the-insertion-mode-appropriately`; Manifest-Pruefung am 2026-10-04,
am 2026-10-23 innerhalb der 30-Tage-Frist. Keine Netzaktualisierung noetig;
Tests bleiben offline.

**Validierung:** `Engine.Html.Tests` (7.230), `dotnet test VisualWeb.slnx
--no-build` und Solution-Build ohne Warnungen/Fehler bestanden. Formatpruefung
der geaenderten C#-Dateien bestanden. Die Solution-Formatpruefung zeigt
weiterhin die nicht geaenderten Findings in `StaticLayout.cs` und
`ShellChrome.cs`. Commit `7d030ab` wurde am 2026-10-23 gepusht. CodeQL
(https://github.com/khmseu/VisualWeb/actions/runs/39886409310) und Linux
development (https://github.com/khmseu/VisualWeb/actions/runs/39886409413)
sowie Windows confinement
(https://github.com/khmseu/VisualWeb/actions/runs/39886409409) sind
erfolgreich.

**Status / naechste Schritte:** Pass 8 ist implementiert, lokal validiert,
committed, gepusht und CI-gruen. Pass 9 anhand des aktualisierten Trackers
waehlen.

### KP-025 - In-Table-Formpointer-Ausnahme Pass 9

- Dokumentiert am: 2026-10-23, Europe/Berlin.
- Ereignisdatum: 2026-10-23.
- Erfassungsart: laufend dokumentiert; Pass 9 abgeschlossen.
- Quelle: aktualisierter `plans/Missing.md`, HTML/DOM-Guide, Engine.Html
  README, `specs/cache/html.html`, Parser/Testquellen und Validierung.
- Bezug: REQ-005, DEC-012, Q-010 und Pass 9 des Zehn-Passen-Auftrags; folgt
  auf KP-024.

**Auswahl (Ableitung, keine neue Einzelentscheidung):** Prioritaet 3 und
Tabellenbaumkonstruktion waren bereits vom Nutzer gewaehlt. Der
In-Table-Algorithmus hat eine spezialisierte Form-Regel, die keine allgemeine
Foster-Parenting-Implementierung benoetigt und den vorhandenen Form-Pointer
nutzt. Der bisherige Parser warf bei Form-Start-Tags in Table/Section/Row-
Modi wegen des nicht implementierten Foster-Parenting-Standardpfads. Der
Umfang bleibt auf diese konkrete Ausnahme begrenzt.

**Umsetzung:** Jedes Form-Start-Token im Tabellenmodus erzeugt einen
Parsefehler; wenn der Form-Pointer leer ist, wird das Element an der aktuellen
Tabellenposition eingefuegt, der Pointer gesetzt und das Element direkt vom
Open-Element-Stack entfernt. Weitere Starts werden bei gesetztem Pointer
ignoriert. Form-End-Tokens verwenden die vorhandene In-Body-Behandlung:
Pointer wird geloescht, das aus dem Stack entfernte Element wird nicht
geschlossen und ein Unexpected-End-Tag-Diagnostikcode bleibt sichtbar. Tests
decken Tabellen-, Abschnitts- und Zeilenmodus, die Elternposition, Ignorieren
weiterer Starts und End-Tag-Diagnose ab.

**Standardsquelle:** `specs/cache/html.html#parsing-main-intable`; Manifest
`html` zuletzt geprueft am 2026-10-04, am 2026-10-23 innerhalb der
30-Tage-Frist. Kein Refresh erforderlich; Tests bleiben offline.

**Validierung:** `Engine.Html.Tests` (7.233), Solution-Build ohne
Warnungen/Fehler, `dotnet test VisualWeb.slnx --no-build` und geaenderte-
Dateien-Formatpruefung bestanden. Solution-Formatpruefung zeigt weiterhin
die Findings in `StaticLayout.cs` und `ShellChrome.cs`; beide Dateien sind
unveraendert gegen den HEAD-Basisstand. Commit `c51811a` wurde gepusht. CodeQL
(https://github.com/khmseu/VisualWeb/actions/runs/39888443886), Linux
development (https://github.com/khmseu/VisualWeb/actions/runs/39888444229)
und Windows confinement
(https://github.com/khmseu/VisualWeb/actions/runs/39888444402) sind
erfolgreich.

**Status / naechste Schritte:** Pass 9 ist lokal validiert, committed und
gepusht und CI-gruen. Pass 10 aus dem aktualisierten Tracker waehlen.

### KP-026 - HTML-Starttags in Tabellenmodi Pass 10

- Dokumentiert am: 2026-10-23, Europe/Berlin.
- Ereignisdatum: 2026-10-23.
- Erfassungsart: laufend dokumentiert; Pass 10 committed, gepusht und mit
  erfolgreicher abschliessender CI abgeschlossen.
- Quelle: aktualisierter `plans/Missing.md`, HTML/DOM-Guide, Engine.Html
  README, lokaler `html`-Standardcache, Parser/Testquellen und Validierung.
- Bezug: REQ-005, DEC-012, Q-010 und Pass 10 des Zehn-Passen-Auftrags; folgt
  auf KP-025.

**Auswahl (Ableitung, keine neue Einzelentscheidung):** Nach dem
Formpointer-Sonderfall war im In-Table-Algorithmus weiterhin das `html`
Start-Token unsupported, obwohl der In-Body-Zweig fuer dieses Token lediglich
Root-Attribute merged und keinen Baumknoten einfuegt. Das ist ein kleiner
table-recovery-Schritt ohne allgemeine Foster-Parenting-Implementierung; er
liegt innerhalb der bereits gewaehlteten Prioritaet 3.

**Umsetzung:** `html`-Start-Tokens im Table-Modus werden an die
vorhandene In-Body-Verarbeitung weitergeleitet. Damit bleiben bestehende
Root-Attributwerte erhalten und bislang fehlende Attribute werden ergaenzt.
Tests decken Table-, Section- und Row-Modus ab.

**Standardsquelle:** `specs/cache/html.html#parsing-main-intable`; Manifest
`html` zuletzt geprueft am 2026-10-04, am 2026-10-23 innerhalb der
30-Tage-Frist. Kein Refresh erforderlich; Tests bleiben offline.

**Validierung:** `Engine.Html.Tests` (7.236), Solution-Build ohne Warnungen/
Fehler, `dotnet test VisualWeb.slnx --no-build` und geaenderte-Dateien-
Formatpruefung bestanden. Die Solution-Formatpruefung meldet weiterhin
unveraenderte Findings in `StaticLayout.cs` und `ShellChrome.cs`, beide gegen
den Base-HEAD unveraendert. Commit `898d3e0` wurde gepusht. Bei der ersten
Run-Listen-Abfrage war noch kein CI-Lauf fuer diesen SHA sichtbar. Der erste
Windows-x64-Lauf scheiterte in einem unveraenderten Browser-Link-/Redirect-Test;
der angeforderte Rerun bestand. Windows ARM64, Linux development und CodeQL
waren ebenfalls erfolgreich. Der erfolgreiche Windows-Rerun ist
[Run 39890303968](https://github.com/khmseu/VisualWeb/actions/runs/39890303968).

**Status / naechste Schritte:** Alle zehn angeforderten Passen sind getrennt
validiert, committed, gepusht und abschliessend CI-gruen. Der Windows-x64-
Erstlauf-Fehler wurde durch erfolgreichen Rerun geklaert; es gab keine
Codeaenderung fuer diesen unabhängigen Browser-Test.

### KP-027 - Neue Zehn-Passen-Serie vor Sicherheitsumfang klaeren

- Dokumentiert am: 2026-10-23, Europe/Berlin.
- Ereignisdatum: 2026-10-23.
- Erfassungsart: laufende Entscheidungsklaerung; keine Codeaenderung.
- Quelle: ausdrueckliche erneute Skill-Anforderung mit Argument `10`;
  Arbeitsbaumstatus, Tracker, Architektur-/Ownership-Map, Roadmap,
  Foundation-ADR und bestehende Browser-/Networking-Guides.
- Bezug: REQ-005, Q-002; folgt auf KP-026.

**Nutzerangabe:** Nach Abschluss der vorigen zehn HTML-Passen wurde der
Implementierungsworkflow erneut fuer zehn Passen aufgerufen. Der Arbeitsbaum
war sauber auf `279c3e1` (`origin/main`).

**Belegt:** `plans/Missing.md` empfiehlt weiterhin Produktionssicherheit und
Isolation als Prioritaet 1. Es dokumentiert zugleich offene Grenzen bei
Origin-Auswahl, Top-Level-Anfragen, SOP/CORS/CSP, Frames und Storage.
`docs/browser-shell.md` und `docs/networking.md` grenzen die bestehenden
Confinement-, Bestatigungs- und Broker-Regeln ausdruecklich von einer
Produktionsautorisierung ab. Frueher wurde Prioritaet 1 nur fuer den damaligen
Zehn-Passen-Lauf zurueckgestellt (KP-016); das setzt keine neue Policy.

**Nutzerentscheidung:** Auf die erste Auswahlfrage waehlte der Nutzer
`origin-selection`, also die Richtung inherited/sandbox origin selection.

**Belegt:** Der aktuelle Shell hat top-level tabs/documents, aber keine
iframe/srcdoc, sandboxed document, opener-created browsing context oder
origin-aware history entry. URL-only origins, standalone `about:blank` opaque
identity, and final-document-origin publication already exist. The HTML cache
describes inherited origins only with a source origin (including `about:blank`)
and opaque origins when an origin sandbox flag is set
(`specs/cache/html.html#determining-the-origin`).

**Offen:** The chosen direction does not identify a currently supported
creator/sandbox context. Adding only page-initiated `about:blank` inheritance
would also require deciding navigation authorization, reload/history identity,
and how a creator document is retained; inventing a frame/sandbox model would
exceed the current renderer architecture. A narrower standalone `about:blank`
load with a fresh opaque origin does not implement the selected inherited/
sandbox behavior. A second, concrete scope decision is requested; no
security behavior or other priority is changed pending that answer.

### KP-028 - Begrenzte about:blank-Origin-Vererbung

- Dokumentiert am: 2026-10-23, Europe/Berlin.
- Ereignisdatum: 2026-10-23.
- Erfassungsart: beschlossene Umfangsklaerung und Implementierungsnachweis.
- Quelle: Nutzerentscheidung im aktuellen Gespraech, Code-/Dokumentdiff,
  gezielte Tests, Build, Volltest und Formatterlauf.
- Bezug: REQ-005, Q-002; konkretisiert und teilweise ersetzt KP-027.

**Nutzerentscheidung:** Der Nutzer waehlte fuer den ersten Schritt
page-initiated `about:blank` mit der Origin des initiierenden committed
Dokuments. Browser-UI-Aufrufe sollen weiterhin eine opaque Origin erhalten;
Reload und History-Traversal sollen die gewaehlte Origin bewahren. Frames und
Sandbox-Origin-Verhalten bleiben ausserhalb dieses Schritts.

**Belegt:** `BrowserUrl.IsAboutBlank` erkennt das HTML-Praedikat einschliesslich
erlaubter Query/Fragment. `LoadedPage.CreateAboutBlank` nimmt optional die
Creator-Origin entgegen; lokale Blank-Loads brauchen keinen Transport.
NavigationHistory speichert Origin-Metadaten pro Eintrag, auch bei Replace,
same-document commits, Forward-Truncation und begrenzter Eviction. Der
Controller stellt die gespeicherte Origin fuer Reload/Traversal wieder her.
Dokumentation und `plans/Missing.md` nennen andere inherited contexts und
Sandbox weiterhin unsupported.

**Validierung:** Fokussierte about:blank-Tests, Core-Tests (3.991),
Browser-Tests (1.094), Solution-Build (`--no-restore`, null Warnungen/Fehler)
und der Volltest `dotnet test VisualWeb.slnx --no-build` bestanden. Der
zunaechst gemeldete Whitespace-Befund in `OriginIsolationTests.cs` wurde mit
dem Projektformatierer korrigiert. Die anschliessende Loesungsformatpruefung
meldete nur noch die unveraenderten Findings in `StaticLayout.cs` und
`ShellChrome.cs`; beide Dateien sind nicht Teil dieser Aenderung.

**Status / naechste Schritte:** Pass 1 der neuen Zehn-Passen-Anforderung ist
implementiert und validiert; Commit, Push und CI-Auswertung stehen noch aus.
Nach Abschluss anhand des aktualisierten Trackers neu waehlen. Der beschlossene
Teilumfang klaert nicht die breitere Produktionsautorisierung, SOP/CORS/CSP,
Frames, Sandbox oder andere inherited origins.

### KP-029 - Pass 1 gepusht und CI bestaetigt

- Dokumentiert am: 2026-10-23, Europe/Berlin.
- Ereignisdatum: 2026-10-23.
- Erfassungsart: Commit-, Push- und CI-Nachweis.
- Quelle: Git-Commit `a4490f9`, `git push`, GitHub Actions fuer diesen Commit.
- Bezug: KP-028, REQ-005, Q-002.

**Belegt:** `a4490f9` (`feat(browser): inherit origins for page-initiated
about:blank`) ist auf `main` gepusht. CodeQL/Push-on-main
([Run 39908662239](https://github.com/khmseu/VisualWeb/actions/runs/39908662239)),
Windows confinement
([Run 39908663558](https://github.com/khmseu/VisualWeb/actions/runs/39908663558))
und Linux development
([Run 39908663567](https://github.com/khmseu/VisualWeb/actions/runs/39908663567))
sind erfolgreich.

**Status / naechste Schritte:** Pass 1 ist abgeschlossen. Neun weitere Passen
sind angefragt; vor Pass 2 bleibt die konkrete naechste Sicherheitsregel
ungeklaert. Die Auswahl muss zwischen einem begrenzten Top-Level-
Navigationspolicy-Inkrement, dem Zurueckstellen der Security-Prioritaet fuer
eine konkrete niedrigere Tracker-Prioritaet und dem Blockieren weiterer Arbeit
unterscheiden.

### KP-030 - Gewaehlte Bestaetigungspolitik bereits implementiert

- Dokumentiert am: 2026-10-23, Europe/Berlin.
- Ereignisdatum: 2026-10-23.
- Erfassungsart: Nutzerentscheidung und Verifikation vorhandener Umsetzung.
- Quelle: Antwort `confirmation_policy` auf die Pass-2-Auswahl; Browser-Shell-
  Code, README und gezielte bestehende Shell-Tests.
- Bezug: REQ-005, Q-002; folgt auf KP-029.

**Nutzerangabe:** Der Nutzer waehlte explizite Bestaetigung fuer
cross-origin page navigation.

**Belegt:** Diese Regel existiert bereits in `DevelopmentShell`: die
Entwicklungsoberflaeche bestaetigt page-initiated Links/Formulare vor dem
Transport und jeden origin-aendernden Redirect. Same-origin-Uebergaenge
werden ohne Nachfrage fortgesetzt; die Bestaetigung zeigt nur die Ziel-Origin.
Abbruch verweigert den Request. Tests decken Link plus mehrstufige
Cross-Origin-Redirects, Abbruch ohne Request, `_blank`-Tab und Cross-Origin-
Formularbestaetigung ab (`tests/VisualWeb.Browser.Tests/ShellTests.cs`).
README und Shell-Guide beschreiben dieses Verhalten. Es bleibt explizite
Entwicklungsbestaetigung, keine Produktionsautorisierung.

**Status / naechste Schritte:** Die gewaehlte Pass-2-Richtung benoetigt keine
Codeaenderung, weil sie bereits umgesetzt und getestet ist; es wurde kein
Pass-2-Code oder leerer Commit erzeugt. Vor einem weiteren Inkrement muss der
Nutzer eine noch fehlende Sicherheitsregel benennen, Prioritaet 1 fuer einen
konkreten niedrigeren Tracker-Punkt zurueckstellen oder weitere Arbeit
blockieren.

### KP-031 - Verschachteltes vertikales Scrollen

- Dokumentiert am: 2026-10-23, Europe/Berlin (UTC+02:00).
- Ereignisdatum: 2026-10-23 laut aktueller Gespraechszusammenfassung.
- Erfassungsart: rekonstruierte Nutzerentscheidung; Originalantwort im
  erhaltenen Verlauf nicht verfuegbar.
- Quelle: Zusammenfassung des aktuellen Gespraechs nach KP-030; erneute
  Pruefung von `plans/Missing.md`, Browser-/Layout-/Paint-Guides und Code.
- Bezug: REQ-005, REQ-010, DEC-015; folgt auf KP-030.

**Nutzerangabe (rekonstruiert):** Die restliche Security-Prioritaet wird fuer
diesen Lauf zurueckgestellt. Als naechstes Inkrement waehlte der Nutzer
vertikales verschachteltes Mausrad-Scrollen mit Edge-Chaining.

**Belegt:** `plans/Missing.md`, Browser-Shell- und Engine-Guides beschreiben
derzeit nur Dokument-Scrollen; Layout, Paint und IPC enthalten noch keine
allgemeine nested-overflow-Implementierung. Der aktuelle CSS-Overflow-
Standard wurde als `css-overflow` registriert und am 2026-10-23 lokal
aktualisiert. Gelesen wurden insbesondere CSS Overflow Module Level 3
§3.1 (`#overflow-control`) zu Achsen, Shorthand und Werten sowie
`#scrollable-overflow`, `#scroll-container` und `#scrollport`. Die CSS
Overscroll Behavior Module Level 1 wurde als `css-overscroll` registriert und
am selben Datum lokal aktualisiert; §3 (`#scroll-chaining-and-boundary-default-actions`)
definiert die Weitergabe an Vorfahren typischerweise an einer Scroll-Grenze,
weist aber ausdruecklich darauf hin, dass diese Working Draft nicht festlegt,
ob/wie eine UA Scroll-Chaining implementiert. Die lokale Cache-Aktualisierung
lief erfolgreich; Tests bleiben davon getrennt und offline.

**Offen:** Die author-facing CSS-Syntax fuer dieses Inkrement ist nicht
entschieden. Eine enge Option ist nur das Paar
`overflow-x:hidden`/`overflow-y:auto` auf hoehenbegrenzten Blockcontainern;
eine weitere Option umfasst passende `overflow`-Shorthand-Faelle und braucht
mehr Validierung fuer nicht unterstuetzte horizontale Scrollfaelle. Vor
Implementierung wird diese Auswirkung dem Nutzer zur Wahl gestellt.

**Status / naechste Schritte:** Scope-Wahl ist aus der Gespraechszusammenfassung
uebernommen, nicht als exaktes Zitat. Noch kein Laufzeitcode geaendert.
Manifest-Eintraege und Cache sind aktualisiert. Q-011 vor der kleinsten
offline-testbaren Implementierung mit Tests und Dokumentation klaeren.

### KP-032 - Nested-Wheel-Scrollen Umsetzung und Validierung

- Dokumentiert am: 2026-10-23, Europe/Berlin (UTC+02:00).
- Ereignisdatum: 2026-10-23.
- Erfassungsart: Implementierung, Tests und Dokumentationsabgleich.
- Quelle: Arbeitsbaum-Diff, Build-/Testausgaben, aktualisierte Tracker- und
  Guide-Dateien in diesem Turn.
- Bezug: REQ-005, REQ-010, DEC-015, Q-011; folgt auf KP-031.

**Nutzerangabe/Entscheidung:** Fuer diesen Pass wurde die enge CSS-Variante
gewaehlt (`paired_longhands`): nur `overflow-x:hidden` plus `overflow-y:auto`
auf definite-height Blockcontainern.

**Belegt:** Der Pass ist jetzt durchgaengig verdrahtet:

- CSS: `overflow-x`/`overflow-y` sind als Properties registriert/parsbar.
- Rendering/Paint: verschachtelte Clip-/Translate-Stacks ueber
  `PushScrollClip`/`PopScrollClip` und per-Container-Offsetzustand.
- IPC: Version 45 mit `ScrollOffsets` (Request) und `ScrollContainers`
  (Frame-Metadaten) inklusive Bounds-Validierung.
- Browser/Shell: Wheel verarbeitet zuerst verschachtelte Container und gibt
  Restdelta an Dokumentscroll weiter (Edge-Chaining).
- Tests: neue CSS- und Browser-Tests fuer die gewaehlte Teilmenge; IPC-Tests
  auf Protokollversion aktualisiert.
- Dokumentation/Tracker: `plans/Missing.md`, Browser-Shell-Guide und
  PageRendering-README beschreiben den begrenzten Umfang und verbleibende
  Luecken.

**Validierung:** Erfolgreich: `dotnet build VisualWeb.slnx --no-restore`,
gezielte Tests (`Engine.Css.Tests`, `Engine.Paint.Tests`, `Ipc.Tests`,
`VisualWeb.Browser.Tests`) und `dotnet test VisualWeb.slnx --no-build`.
`dotnet format VisualWeb.slnx --verify-no-changes --no-restore --exclude third_party`
meldet weiterhin unveraenderte, nicht-passbezogene Befunde in
`src/Engine/Engine.Layout/StaticLayout.cs` und
`src/Apps/VisualWeb.Browser/ShellChrome.cs`.

**Status / naechste Schritte:** Der ausgewaehlte nested-wheel-Teilumfang ist
implementiert und testgruen, aber noch nicht committed/gepusht. Danach den
naechsten hoechstprioren offenen Tracker-Schritt waehlen; allgemeine
Overflow-Semantik, horizontales Scrollen und DOM scroll events bleiben offen.

### KP-033 - Neue Zehn-Passen-Serie und offene Security-Policy

- Dokumentiert am: 2026-10-26, Europe/Berlin (UTC+01:00).
- Ereignisdatum: 2026-10-26.
- Erfassungsart: ausdruecklicher Nutzerauftrag, Artefakt-/CI-Abgleich und
  Aktualisierung des Arbeitsstands.
- Quelle: aktueller Nutzerturn `/missing-priority-implementation 10`,
  `plans/Missing.md`, Git-Status/Commit und GitHub Actions.
- Bezug: REQ-005, Q-002, KP-032; folgt auf KP-032.

**Nutzerangabe:** Der Nutzer hat erneut zehn Missing-Prioritaets-Passen
angefordert.

**Belegt:** Der zuvor gewaehlte nested-wheel-Pass wurde als Commit `8aaf65c`
auf `main` gepusht. Die zugehoerigen Workflows CodeQL, Linux development und
Windows confinement sind erfolgreich (GitHub Actions Run IDs
`40271027798`, `40271028243`, `40271028239`). Damit ist die Statusaussage in
KP-032, der Pass sei noch nicht committed/gepusht, ueberholt; dieser Eintrag
korrigiert sie transparent. Das Worktree enthaelt eine nicht-committete
Markdown-Formatierungsaenderung in `Kontext/Wissensstand.md`, die bereits vor
diesem Auftrag vorhanden war und nicht Teil eines neuen Codepasses ist.

**Ableitung / offen:** `plans/Missing.md` priorisiert weiterhin Security an
erster Stelle. Q-002 haelt die konkrete noch fehlende Produktions-/Navigations-
Policy offen. Die fruehere Auswahl, Security fuer den vorigen Lauf
zurueckzustellen, wird nicht als Freigabe fuer einen weiteren Sicherheits-
Verhaltenswechsel interpretiert. Daher ist vor Implementierung eine neue
konkrete Entscheidung erforderlich. Dieser Auftrag hat noch keinen neuen
Implementierungspass begonnen.

**Status / naechste Schritte:** Nutzerentscheidung fuer das naechste
Security-Verhalten abwarten; die Optionen sollen insbesondere die Erweiterung
der Cross-Origin-Bestaetigung auf Adressleisten-Navigation, eine restriktive
Same-Origin-Adressleistenpolitik, Zurueckstellung der Security-Prioritaet mit
konkreter niedrigerer Tracker-Wahl oder das Blockieren weiterer Aktionen
unterscheidbar machen. Keine der Optionen ist ohne Antwort beschlossen.

### KP-034 - Cross-Origin-Bestaetigung fuer Adressleisten-Navigation

- Dokumentiert am: 2026-10-26, Europe/Berlin (UTC+01:00).
- Ereignisdatum: 2026-10-26.
- Erfassungsart: ausdrueckliche Nutzerentscheidung; Implementierung in Arbeit.
- Quelle: Nutzerantwort `confirm-address-bar-cross-origin` und aktueller
  Browser-Shell-/Controller-Diff.
- Bezug: REQ-005, DEC-006, DEC-016, Q-002; folgt auf KP-033.

**Nutzerangabe:** Der Nutzer waehlte, die bestehende Cross-Origin-
Bestaetigung auf getippte Adressleisten-Navigation auszudehnen.

**Beschlossener Umfang (DEC-016):** Bei einem bestehenden committed Dokument
werden getippte HTTP(S)-Navigationen ueber denselben origin-only-
Bestaetigungspfad wie Page-Navigation geleitet. Initiale Ziel-Origin und alle
origin-aendernden Redirects werden vor dem jeweiligen Transport bestaetigt.
Initial-Loads, nicht-HTTP(S)-Adressleisten-Navigation und direkte
Controller-Aufrufe ohne Authorizer bleiben unveraendert. Die bestehende
Entwicklungsbestaetigung ist keine Produktionsautorisierung. Diese Auswahl
praezisiert DEC-006 und ersetzt teilweise dessen fruehere Aussage, dass
Adressleisten-Navigation uneingeschraenkt bleibt.

**Belegt:** `BrowserController.NavigateFromAddressBar` und die
Development-Shell-Return-Verarbeitung wurden angepasst. Drei fokussierte
Shell-Regressionstests bestanden: direkte Cross-Origin-Adresse mit Zustimmung,
Same-Origin-Einstieg mit Cross-Origin-Redirect sowie Abbruch ohne
Zielanforderung. Der gesamte `VisualWeb.Browser.Tests`-Lauf (1.098 Tests),
`dotnet build VisualWeb.slnx --no-restore` und
`dotnet test VisualWeb.slnx --no-build` bestanden.
`dotnet format VisualWeb.slnx --verify-no-changes --no-restore --exclude third_party`
meldet nur die bereits bekannten nicht-passbezogenen Findings in
`src/Engine/Engine.Layout/StaticLayout.cs` und
`src/Apps/VisualWeb.Browser/ShellChrome.cs`.

**Offen:** Q-002 bleibt fuer die breitere Produktionssicherheits-, SOP/CORS/
CSP-, Frame/Origin- und Storage-Policy bestehen. Das Worktree enthaelt
weiterhin die aus KP-033 bekannte nicht-committete Markdown-Aenderung in
`Kontext/Wissensstand.md`; vor dem Commit muss sie von den neuen, gezielt
zugeordneten Inhalten getrennt oder als passende Kontextdokumentation sauber
integriert werden.

**Status / naechste Schritte:** Pass 1 dieser neuen Zehn-Passen-Anforderung
ist lokal implementiert und validiert; Tracker und direkte Guides sind
aktualisiert. Commit, Push und CI stehen aus.

### KP-035 - Adressleistenbestaetigung committed und CI-gruen

- Dokumentiert am: 2026-10-26, Europe/Berlin (UTC+01:00).
- Ereignisdatum: 2026-10-26.
- Erfassungsart: Commit-, Worktree- und GitHub-Actions-Abgleich.
- Quelle: Commit `c39b6d3`, lokales Worktree, `docs/project-map.md` und GitHub
  Actions Runs `40273905674`, `40273905621`, `40273905697`.
- Bezug: REQ-005, DEC-016, Q-002; aktualisiert den Arbeitsstatus aus KP-034.

**Belegt:** Commit `c39b6d3` (`feat(browser): confirm typed cross-origin
address-bar navigation`) wurde nach `origin/main` gepusht. CodeQL
(`40273905674`), Linux development (`40273905621`) und Windows confinement
(`40273905697`) sind erfolgreich. Vor dem Push bestanden die fokussierten
Adressleisten-Regressionstests (3), alle 1.098 Browser-Tests, Solution-Build
und Solution-Volltests. Die loesungsweite Formatpruefung meldete nur die
unveraenderten, nicht geaenderten Findings in `StaticLayout.cs` und
`ShellChrome.cs`.

**Dokumentationskorrektur:** `docs/project-map.md` hatte noch ausgesagt, dass
Adressleisten-Navigation nicht bestaetigt wird. Diese Zusammenfassung wird an
DEC-016 angepasst. Ein Commit-Hook formatierte beim Commit zusaetzlich Tabellen
in `Kontext/Wissensstand.md`; die bereits zuvor vorhandene Markdown-Link-
Formatierungsaenderung in derselben Datei blieb unstaged und wird bewahrt.

**Offen / naechste Schritte:** Q-002 bleibt fuer die breitere
Produktionssicherheits-, SOP/CORS/CSP-, Frame/Origin- und Storage-Policy offen.
Vor der naechsten Umsetzung unter der Security-Prioritaet ist ein weiteres
konkretes, abgegrenztes Verhalten vom Nutzer zu waehlen; der Auftrag ueber
zehn Passen bleibt bestehen.

**Nachtrag:** Die Korrektur in `docs/project-map.md` wurde als Commit
`41f6e96` gepusht. CodeQL (`40274885609`), Linux development (`40274885456`)
und Windows confinement (`40274885395`) sind erfolgreich.

### KP-036 - Bestaetigung erster getippter Adressleisten-Navigation

- Dokumentiert am: 2026-10-26, Europe/Berlin (UTC+01:00).
- Ereignisdatum: 2026-10-26.
- Erfassungsart: ausdrueckliche Nutzerentscheidung und Code-/Testpruefung.
- Quelle: Nutzerantwort `confirm_initial_address_bar`, `plans/Missing.md`,
  Browser-Shell-Code und fokussierte Tests.
- Bezug: REQ-005, DEC-016, DEC-017, Q-002; folgt auf KP-035.

**Nutzerangabe:** Der Nutzer waehlte, die vorhandene Origin-Bestaetigung auch
auf typed HTTP(S) Adressleisten-Requests anzuwenden, wenn noch kein Dokument
committed ist.

**Beschlossener Umfang (DEC-017):** Ohne committed Dokument gibt es keine
Source-Origin zum Vergleich; deshalb wird jedes getippte HTTP(S)-Ziel vor dem
Transport bestaetigt. Nach Zustimmung gilt dessen Origin als Ausgangspunkt
fuer die bereits vorhandene hop-by-hop Redirect-Bestaetigung. Automatisch
gestartete Initial-Loads, nicht-HTTP(S)-Adressleisten-Navigation und direkte
Controller ohne konfigurierten Authorizer bleiben unveraendert. Die Regel
bleibt eine Development-Shell-Bestaetigung und ist keine
Produktionsautorisierung.

**Belegt:** `BrowserController` uebergibt bei fehlendem committed Dokument
keine Source-Origin; der Shell-Authorizer bestaetigt dadurch das erste
Navigationsziel und die Redirect-Kette bleibt an der genehmigten ersten
Origin verankert. Zwei fokussierte Shell-Tests bestanden: Freigabe mit
anschliessender Cross-Origin-Redirect-Bestaetigung sowie Abbruch vor dem
ersten Request. Alle 1.100 Browser-Tests, der Solution-Build und die
vollstaendige Solution-Testsuite bestanden. `dotnet format` meldet
unveraenderte Findings in `StaticLayout.cs` und `ShellChrome.cs`; sie liegen
ausserhalb dieses Diffs. `git diff --check` ist sauber. Formatpruefung, Commit,
Push und CI fuer diesen Pass waren zu diesem Zeitpunkt noch ausstehend.

**Nachtrag:** Pass 2 wurde als Commit `0b6ca2d` gepusht. CodeQL
(<https://github.com/khmseu/VisualWeb/actions/runs/40278866974>), Linux
development (<https://github.com/khmseu/VisualWeb/actions/runs/40278867244>)
und Windows confinement
(<https://github.com/khmseu/VisualWeb/actions/runs/40278867114>) sind
erfolgreich.

### KP-037 - Bestaetigung automatisch gestarteter HTTP-Navigation

- Dokumentiert am: 2026-10-26, Europe/Berlin (UTC+01:00).
- Ereignisdatum: 2026-10-26.
- Erfassungsart: ausdrueckliche Nutzerwahl laut aktueller
  Gespraechszusammenfassung sowie anschliessender Codepruefung.
- Quelle: aktuelle Gespraechszusammenfassung zum Pass 3 und Shell-Code.
- Bezug: REQ-005, DEC-016 bis DEC-018, Q-002; folgt auf KP-036.

**Nutzerangabe:** Fuer den naechsten Pass waehlte der Nutzer die Bestaetigung
automatisch gestarteter HTTP(S)-Loads. Die ueberlieferte Passbeschreibung
umfasst Launch-URLs und den Standard-New-Tab-Pfad; ein persoenliches Motiv
wurde nicht ueberliefert.

**Beschlossener Umfang (DEC-018):** Automatisch gestartete HTTP(S)-
Shell-Navigation wird vor dem ersten Transport bestaetigt, wenn kein
committed Dokument als Source-Origin vorhanden ist. Origin-aendernde
Redirects bleiben einzeln bestaetigungspflichtig. Launch-URLs ueber
`OpenWindow(url)` gehoeren dazu. Der eingebaute New-Tab-Welcome-Inhalt ist
`data:` und bleibt daher ohne HTTP(S)-Prompt; andere Nicht-HTTP(S)-
Shellnavigation und direkte `BrowserController.Navigate`-Aufrufe bleiben
unveraendert. Die bestaetigungsbasierte Development-Shell-Policy ist keine
Produktionsautorisierung. DEC-018 ersetzt die automatische Initial-Load-
Ausnahme aus DEC-016/017 nur fuer diese HTTP(S)-Shell-Loads.

**Belegt zum Dokumentationszeitpunkt:** `OpenWindow`, die Tastatur-
Adressleisten-Navigation und die New-Tab-Erzeugung verwenden den
confirmation-aware Controller-Einstieg; der direkte `Navigate`-Pfad bleibt
unveraendert. Fokussierte Shell-Tests fuer Freigabe/Redirect und Abbruch eines
HTTP(S)-Launches bestanden; die vollen Browser-Tests (1.102), der Solution-
Build und die vollstaendige Solution-Testsuite sind ebenfalls erfolgreich. Die
Formatpruefung meldet nur die bekannten, nicht geaenderten Findings in
`StaticLayout.cs` und `ShellChrome.cs`; `git diff --check` ist sauber.

**Offen:** Q-002 bleibt als breitere Produktionssicherheits-, SOP/CORS/CSP-,
Frame/Origin- und Storage-Policy bestehen. Der Worktree hat eine bereits
vorbestehende Markdown-Formatierungsaenderung in `Kontext/Wissensstand.md`;
sie ist von den neuen Pass-Aenderungen zu trennen.

**Status / naechste Schritte:** Pass 3 ist lokal implementiert und validiert;
Commit, Push und CI stehen aus. Fuer die naechste Pass-Auswahl sind Tracker
und verbleibende offene Policy erneut zu pruefen.

**Nachtrag:** Commit `93111dc` (`feat(browser): confirm automatic HTTP(S)
shell loads`) wurde am 2026-10-26 auf `origin/main` gepusht. CodeQL
(`40288834382`), Linux development (`40288834949`) und Windows confinement
(`40288835038`) sind erfolgreich. Die lokalen Browser-Tests (1.102),
Solution-Build und Volltests bestanden. Die Formatpruefung meldet weiterhin
nur die unveraenderten Findings in `StaticLayout.cs` und `ShellChrome.cs`.

### KP-038 - Bestaetigung bei Browser-History-Navigation

- Dokumentiert am: 2026-10-26, Europe/Berlin (UTC+01:00).
- Ereignisdatum: 2026-10-26.
- Erfassungsart: Codepruefung, ausdrueckliche Nutzerentscheidung,
  Implementierung, Validierung und CI-Abgleich.
- Quelle: `BrowserController.Back`, `Forward`, `Reload`, `Start` und
  `GetPageSource.LoadAsync`; Nutzerantwort `confirm_initial_cross_origin`;
  Commit `c2bf905`; `plans/Missing.md` Prioritaet 1.
- Bezug: REQ-005, Q-002, KP-037; folgt auf KP-037.

**Belegt vor Umsetzung:** Shell-Chrome routete `Back`, `Forward` und `Reload`
ueber Controller-Einstiege, die `Start` ohne `authorizeRedirects`,
`preventHttpsDowngrade` oder Source-Origin aufriefen. Origin-aendernde
Redirects wurden daher nicht bestaetigt; HTTPS-Downgrade-Schutz war nicht
aktiviert.

**Nutzerentscheidung (DEC-019):** Der Nutzer waehlte die Bestaetigung auch fuer
das initiale Back/Forward-History-Ziel, wenn es Cross-Origin zum aktuell
committed Dokument ist. Reload und origin-aendernde Redirects bleiben
hop-by-hop bestaetigungspflichtig; HTTPS-Downgrades werden blockiert.
Low-Level-Controller ohne Shell-Authorizer bleiben unveraendert. Ein Motiv
fuer die Wahl wurde nicht ueberliefert.

**Belegt, umgesetzt und lokal validiert:** Separate
`BackWithShellConfirmation`, `ForwardWithShellConfirmation` und
`ReloadWithShellConfirmation`-Einstiege halten direkte Controller-Aufrufe
unveraendert; `DevelopmentShell` routet seine Chrome-Aktionen ueber diese
Einstiege. Der gemeinsame History-Load-Pfad uebergibt Source-Origin,
Hop-Autorisierung und HTTPS-Downgrade-Schutz. Vier fokussierte
Regressionstests fuer Cross-Origin Back/Forward-Ziele, Cross-Origin-Reload-
Redirect sowie HTTP-Initialziel und Redirect-Downgrade aus HTTPS bestanden.
Der vollstaendige Browser-Lauf (1.106), `dotnet build VisualWeb.slnx` (0
Warnungen/Fehler) und `dotnet test VisualWeb.slnx --no-build` bestanden.
Ein frueherer Gesamtlauf mit einem Zwischenstand der Implementierung deckte
eine zu breite Aenderung direkter Controller-Aufrufe auf; getrennte
shell-spezifische Einstiege behoben sie und der anschliessende Gesamtlauf ist
gruen. `dotnet format VisualWeb.slnx --verify-no-changes --no-restore --exclude
third_party` meldet ausschliesslich bekannte unveraenderte Findings in
`StaticLayout.cs` (Whitespace/Imports) und `ShellChrome.cs` (Imports);
`git diff --check` ist sauber. Commit `c2bf905` (`feat(browser): authorize
shell history navigation`) wurde auf `origin/main` gepusht. CodeQL / Push on
main (`40297681183`), Linux development (`40297681348`) und Windows
confinement (`40297681328`) sind erfolgreich:
[CodeQL](https://github.com/khmseu/VisualWeb/actions/runs/40297681183),
[Linux](https://github.com/khmseu/VisualWeb/actions/runs/40297681348),
[Windows](https://github.com/khmseu/VisualWeb/actions/runs/40297681328).

**CI-Nachlauf zum Kontext-Commit `67272d1`:** Linux development und CodeQL
bestanden. Windows ARM64 bestand; der erste Windows-x64-Lauf scheiterte an
einem Timeout in `ScrollTests.CrossDocumentHistoryTraversalRestoresEachEntryScrollPosition(process: true)`.
Der fehlgeschlagene Windows-x64-Job wurde erneut ausgefuehrt und bestand.
Dies war ein transienter Testlauf-Fehler im unveraenderten Scrolltest; der
Feature-Commit `c2bf905` hatte bereits alle drei primaeren Workflows bestanden.

### KP-039 - Top-Level-File-Navigation-Policy

- Dokumentiert am: 2026-10-26, Europe/Berlin (UTC+01:00).
- Ereignisdatum: 2026-10-26.
- Erfassungsart: Codepruefung und damals offene Policy-Frage; spaetere
  Nutzerentscheidung ist in KP-040 festgehalten.
- Quelle: `plans/Missing.md` Prioritaet 1; `BrowserController.NavigateWithShellConfirmation`,
  `StartHistoryNavigation` und `DevelopmentShell.OpenWindow`.
- Bezug: REQ-005, Q-002, Q-012, KP-038; folgt auf KP-038.

**Belegt:** Die shell-spezifische Bestätigung greift nur fuer HTTP(S).
Explizite Top-Level-`file:`-Navigation aus der Adressleiste, einem
`OpenWindow(file:)`-Launch und Datei-History verwendet weiter den normalen
Load-Pfad. Page-initiated File-Links/-Forms bleiben separat blockiert.
Eine Origin-Bestaetigung wuerde fuer `file:` keinen ausreichenden Zieltext
anzeigen; der Datei-Pfad waere fuer eine nutzbare Entscheidung erforderlich.

**Bei der Ersterfassung offen:** Der Nutzer sollte entscheiden, ob Top-Level-
`file:`-Navigation in der DevelopmentShell vor dem Laden bestaetigt, generell
blockiert oder als explizite Browser-UI-/Launch-Aktion unveraendert zulaessig
bleibt. Ein Prompt oder Block beeinflusst absichtliche lokale Tests; eine
Prompt-Anzeige des Pfads legt lokale Pfad-Information offen. Alternativ kann
ein anderer konkreter Missing-Punkt gewaehlt werden oder weitere Aktionen
blockiert werden. Die Entscheidung folgte spaeter in KP-040.

**Status bei Ersterfassung:** Keine Pass-5-Implementierung begonnen;
Nutzerentscheidung ausstehend.

### KP-040 - Bestaetigung expliziter File-Navigation

- Dokumentiert am: 2026-10-26, Europe/Berlin (UTC+01:00).
- Ereignisdatum: 2026-10-26.
- Erfassungsart: ausdrueckliche Nutzerwahl und Implementierung.
- Quelle: Nutzerantwort auf die Policy-Auswahl im aktuellen Turn;
  `BrowserController`, `DevelopmentShell`, `ShellChrome` und
  `ShellTests.ShellLaunchAndFileHistoryLoadsRequireConfirmation`,
  `ShellTests.FileAddressBarNavigationWaitsForConfirmationAndShowsPathWithoutQueryOrFragment`.
- Bezug: REQ-005, DEC-020, Q-012, KP-039; setzt KP-039 fort.

**Nutzerentscheidung (DEC-020):** Der Nutzer waehlte `confirm_file_navigation`.
Explizite Top-Level-`file:`-Loads aus Adressleiste, `OpenWindow(file:)` und
Datei-History sollen vor dem Laden bestaetigt werden; der Prompt soll den
Datei-Pfad anzeigen. Ein Motiv fuer die Wahl wurde nicht mitgeteilt.

**Umsetzung:** Shell-aware Adressleisten- und History-Einstiege routen `file:`
ueber die bestaetigungsbasierte Ladeoperation. Da der bestehende Engine.Net
Autorisierungsloader nur HTTP(S) akzeptiert und File-Loads keine Redirects
haben, erfolgt die Nutzerautorisierung vor dem vorhandenen lokalen
`IPageSource.LoadAsync`-Pfad. Der Prompt zeigt den kanonischen `file:`-URL-Pfad;
Query und Fragment werden nicht angezeigt. Page-initiated File-Links/-Forms und
direkte Low-Level-Controller-Aufrufe bleiben unveraendert. Gleichdokumentige
Fragment-History laedt die Datei nicht erneut und fragt daher nicht.

**Bisherige Validierung:** Zwei fokussierte Browser-Regressionstests, die
vollstaendige Browser-Testsuite (1.108 Tests), `dotnet build VisualWeb.slnx`
(0 Warnungen/Fehler) und `dotnet test VisualWeb.slnx --no-build` bestanden.
`dotnet format VisualWeb.slnx --verify-no-changes --no-restore --exclude
third_party` meldet die zuvor bekannten, nicht geaenderten Import-/
Whitespace-Findings in `ShellChrome.cs` und `StaticLayout.cs`. `git diff
--check` ist sauber. Commit `d38b902` wurde nach `origin/main` gepusht;
CodeQL (`40305487233`), Linux development (`40305487392`) und Windows
confinement (`40305487415`) sind erfolgreich:
[CodeQL](https://github.com/khmseu/VisualWeb/actions/runs/40305487233),
[Linux](https://github.com/khmseu/VisualWeb/actions/runs/40305487392),
[Windows](https://github.com/khmseu/VisualWeb/actions/runs/40305487415).

### KP-041 - Explizite Data-Navigation-Policy

- Dokumentiert am: 2026-10-26, Europe/Berlin (UTC+01:00).
- Ereignisdatum: 2026-10-26.
- Erfassungsart: Code-/Trackerpruefung und ausdrueckliche Nutzerwahl.
- Quelle: `plans/Missing.md` Prioritaet 1; `docs/browser-shell.md`;
  `BrowserController.NavigateWithShellConfirmation`,
  `DevelopmentShell.OpenWindow`.
- Bezug: REQ-005, Q-002, Q-013, KP-040; folgt auf KP-040.

**Belegt:** Die shell-aware Adressleisten- und History-Einstiege routen
HTTP(S) sowie Top-Level-`file:` durch die Shell-Policy; explizite
Top-Level-`data:`-Navigation bleibt wie andere nicht-HTTP(S)-Navigation
unveraendert. Die eingebaute New-Tab-Welcome-Page ist ebenfalls eine
`data:`-URL. Page-initiated `data:`-Links und -Forms sind separat gesperrt.

**Vorschlag (nicht beschlossen):** Naechster Pass waere eine ausdrueckliche
Policy fuer browsergesteuerte `data:`-Navigation aus Adressleiste,
`OpenWindow(data:)` und cross-document History. Bei einer Bestaetigung sollte
der Dialog den Payload nicht anzeigen. Als Alternativen stehen Blockieren,
Beibehalten des aktuellen Verhaltens mit Auswahl eines anderen konkreten
Tracker-Inkrements oder das Pausieren weiterer Aktionen bis zur Klaerung
offen. Der eingebaute Welcome-Aufruf und direkte Low-Level-Controller-Aufrufe
bleiben bei einer Shell-Policy-Aenderung ausserhalb des Scopes.

**Nutzerentscheidung (DEC-021):** Nutzer waehlt `confirm_top_level_data`.
Explizite Top-Level-`data:`-Loads aus Adressleiste, `OpenWindow` und
cross-document History sollen bestaetigt werden. Der Dialog zeigt nicht den
Payload. Der eingebaute Welcome-Aufruf und direkte Low-Level-Controller-Aufrufe
bleiben unveraendert; ein Motiv wurde nicht mitgeteilt.

**Status:** Beschlossen; implementiert, lokal vollvalidiert, gepusht und CI-gruen.
Siehe Q-013 und [KP-042](./Kontextprotokoll.md#kp-042---data-navigation-implementierung-und-ci)
in [Wissensstand](./Wissensstand.md#offene-fragen-widersprueche-und-risiken).

### KP-042 - Data-Navigation-Implementierung und CI

- Dokumentiert am: 2026-10-26, Europe/Berlin (UTC+01:00).
- Ereignisdatum: 2026-10-26.
- Erfassungsart: Commit-, Test- und Actions-Pruefung.
- Quelle: Commit `0b21f43`; `plans/Missing.md`; Browser-Navigationstests;
  Actions CodeQL `40313206574`, Linux development `40313206774`,
  Windows confinement `40313207096`.
- Bezug: DEC-021, Q-013, KP-041; Pass 6 der Zehn-Passen-Serie.

**Belegt:** Explizite Top-Level-`data:`-Loads aus Adressleiste,
`OpenWindow` und cross-document Back/Forward/Reload werden vor dem Laden
bestaetigt. Der Payload wird nicht angezeigt; der eingebaute Welcome-Aufruf
und direkte Low-Level-Controller-Aufrufe bleiben unveraendert. Zwei fokussierte
Regressionstests, alle 1.110 Browser-Tests, `dotnet build VisualWeb.slnx`,
`dotnet test VisualWeb.slnx --no-build` und die Formatpruefung der geaenderten
C#-Dateien bestanden. Die Loesungs-Formatpruefung meldet nur unveraenderte
Findings in `StaticLayout.cs` und `ShellChrome.cs`.

**Belegt:** Commit `0b21f43` ist auf `origin/main`. CodeQL, Linux development
und Windows x64 bestanden. Der erste Windows-ARM64-Versuch scheiterte im
CPU-pressure-Cancellation-Test mit `RendererProcessException` wegen
ueberschrittenem Deadline, obwohl der Test Cancellation erwartete. Der
fehlgeschlagene ARM64-Job wurde erneut ausgefuehrt und bestand einschliesslich
des Tests, der vollstaendigen Solution-Tests und der Windows-Smokes. Die
Ursache des ersten Fehlschlags ist nicht ermittelt; die erfolgreiche
Wiederholung belegt keinen bestimmten Flake-Mechanismus.

**Status:** Pass 6 abgeschlossen; CI nach erfolgreichem Rerun gruen. Die
breitere Produktionsautorisierung bleibt offen. Der naechste konkrete
Dateizugriffsumfang ist als Q-014 offen.

### KP-043 - Vorschlag zur Begrenzung expliziter Datei-Navigation

- Dokumentiert am: 2026-10-26, Europe/Berlin (UTC+01:00).
- Ereignisdatum: 2026-10-26.
- Erfassungsart: Vorschlag auf Grundlage des Trackers und der aktuellen
  Shell-Dokumentation; Nutzerentscheidung steht aus.
- Quelle: [Missing.md](../plans/Missing.md#1-production-safe-browsing-and-stronger-isolation),
  [browser-shell.md](../docs/browser-shell.md), DEC-020 und Q-014.
- Bezug: DEC-020, KP-040, KP-042; nicht beschlossen.

**Belegt:** Top-Level-`file:`-Navigation aus Adressleiste, `OpenWindow` und
History wird vor Dateizugriff bestaetigt. Der Prompt zeigt den kanonischen
Pfad, aber diese Bestaetigung bietet laut Shell-Dokumentation keine
Dateisystem-Isolation.

**Vorschlag (offen):** Vor weiteren Datei-Sicherheitsarbeiten entscheiden,
ob bestaetigte Navigation beliebige Dateipfade behalten soll, auf explizit
freigegebene Wurzeln beschraenkt werden soll oder ganz deaktiviert werden
soll. Eine Beschraenkung reduziert moeglichen Zugriff, braucht aber eine
benutzerverstaendliche Wurzelwahl und kann lokale Entwicklungsablaeufe
einschraenken; Beibehalten bewahrt Nutzbarkeit, ist jedoch keine Isolation;
Deaktivieren nimmt die lokale Datei-Funktion weg.

**Status bei Dokumentation:** Nutzerwahl stand aus; diese Optionen waren ein
Vorschlag und keine Aenderung an DEC-020. **Spaeterer Stand:** Q-014 wurde
durch DEC-022 / KP-044 beantwortet; der Nutzer bestaetigte beliebige
Dateipfade nach Prompt und verwarf Wurzelbeschraenkung sowie Abschaltung fuer
diesen Scope.

### KP-044 - Dateizugriffsumfang bestaetigt

- Dokumentiert am: 2026-10-26, Europe/Berlin (UTC+01:00).
- Ereignisdatum: 2026-10-26.
- Erfassungsart: ausdrueckliche Nutzerwahl.
- Quelle: aktuelle Nutzerantwort zu Q-014.
- Bezug: DEC-020, KP-040, KP-043; DEC-022 bestaetigt die bestehende Policy.

**Nutzerangabe / Entscheidung (DEC-022):** Der Nutzer waehlte
`keep_confirmed_any_path`: Top-Level-`file:`-Navigation behaelt nach
ausdruecklicher Bestaetigung den Zugriff auf beliebige Dateipfade. Es werden
keine erlaubten Wurzeln eingefuehrt und Datei-Navigation wird nicht
abgeschaltet. Ein Motiv wurde nicht mitgeteilt.

**Folge:** Die bestehende DevelopmentShell-Policy bleibt unveraendert. Sie
erlaubt lokale Entwicklungsnavigation nach Bestaetigung, bietet aber keine
Dateisystem-Isolation. Q-014 ist beantwortet; Pass 7 soll ein anderes
konkretes Inkrement aus der weiterhin hoechsten Prioritaet waehlen.

### KP-045 - Vorschlag zur gleich-originigen Page-Navigation

- Dokumentiert am: 2026-10-26, Europe/Berlin (UTC+01:00).
- Ereignisdatum: 2026-10-26.
- Erfassungsart: Vorschlag vor Nutzerentscheidung; Code- und
  Shell-Policy-Pruefung.
- Quelle: [Missing.md](../plans/Missing.md#1-production-safe-browsing-and-stronger-isolation),
  [browser-shell.md](../docs/browser-shell.md),
  [BrowserController.cs](../src/Apps/VisualWeb.Browser/BrowserController.cs).
- Bezug: Q-002, Q-015, DEC-014, KP-030; naechste moegliche Pass-7-Policy.

**Belegt:** Die DevelopmentShell bestaetigt page-initiierte HTTP(S)-Ziele,
wenn deren Origin nicht mit der aktuell committed Source-Origin uebereinstimmt.
Gleich-originige Ziele laufen ohne initialen Prompt weiter; Redirects werden
weiter einzeln bestaetigt, wenn der naechste Hop die Origin wechselt. Direkte
Low-Level-Controller-Einstiege bleiben ohne Shell-Authorizer.

**Vorschlag (nicht beschlossen):** Als eng begrenzte Ergaenzung koennte die
DevelopmentShell auch den initialen gleich-originigen page-initiierte
HTTP(S)-Top-Level-Request bestaetigen, waehrend die aktuelle Redirect-Policy,
Address-Bar-Verhalten und Low-Level-Aufrufe unveraendert bleiben. Das erhoeht
die explizite Nutzerkontrolle, erzeugt aber Dialoge auch fuer gewoehnliche
gleich-originige Links/Formulare. Alternativ bleibt die bestehende
Cross-Origin-only-Policy bestehen und ein anderes prioritaet-1-Inkrement muss
ausgewaehlt werden.

**Status bei Dokumentation:** Nutzerentscheidung zu Q-015 stand aus; keine
Laufzeitaenderung war beschlossen. **Spaeterer Stand:** Der Nutzer bestaetigte
die bestehende Cross-Origin-only-Policy in DEC-023 / KP-046.

### KP-046 - Cross-Origin-only-Policy bestaetigt

- Dokumentiert am: 2026-10-26, Europe/Berlin (UTC+01:00).
- Ereignisdatum: 2026-10-26.
- Erfassungsart: ausdrueckliche Nutzerwahl.
- Quelle: aktuelle Nutzerantwort zu Q-015.
- Bezug: DEC-014, Q-002, Q-015, KP-030 und KP-045.

**Nutzerangabe / Entscheidung (DEC-023):** Der Nutzer waehlte
`keep_cross_origin_only`. Gleich-originige page-initiierte HTTP(S)-Ziele
behalten das aktuelle Verhalten ohne initialen Prompt. Cross-Origin-Ziele und
origin-aendernde Redirects behalten die bestehende bestaetigungsbasierte
Shell-Policy; Adressleisten- und direkte Low-Level-Controller-Aufrufe bleiben
unveraendert. Ein Motiv wurde nicht mitgeteilt.

**Folge:** Die vorgeschlagene Erweiterung fuer gleich-originige
Page-Navigation wird nicht umgesetzt. Q-015 ist beantwortet; der naechste
Pass muss einen anderen konkreten Punkt aus der weiterhin hoechsten
Sicherheitsprioritaet adressieren.

### KP-047 - Keine weitere fertige Prioritaet-1-Abgrenzung gefunden

- Dokumentiert am: 2026-10-26, Europe/Berlin (UTC+01:00).
- Ereignisdatum: 2026-10-26.
- Erfassungsart: Tracker-, Roadmap- und Implementierungspruefung; Vorschlag
  zur Entscheidung steht aus.
- Quelle: [Missing.md](../plans/Missing.md#1-production-safe-browsing-and-stronger-isolation),
  [browser-foundation-plan.md](../plans/browser-foundation-plan.md),
  [docs/architecture/overview.md](../docs/architecture/overview.md),
  [project-map.md](../docs/project-map.md), DEC-022 und DEC-023.
- Bezug: Q-002, Q-016, DEC-022, DEC-023 und KP-046; Pass-7-Auswahl.

**Belegt:** Die verbleibenden hoeher priorisierten Sicherheitsluecken umfassen
allgemeine Request-Autorisierung, volle Fetch CORS/CSP, nicht abgedeckte
Sandbox-/Origin-Kontexte, Storage-Partitionierung und Site-/Frame-Isolation.
Der genehmigte Roadmap-Text stellt umfassendere Fetch/CSP-Funktionen und
spaetere Site-Isolation als ausstehend dar. Die bereits abgegrenzten
DevelopmentShell-Prompts fuer HTTP(S), `file:` und `data:` wurden umgesetzt;
der Nutzer bestaetigte beliebige `file:`-Pfade nach Prompt und lehnte eine
Ausdehnung auf gleich-originige HTTP(S)-Page-Navigation ab.

**Ableitung:** Aus den geprueften Quellen ergibt sich derzeit kein weiteres
kleines, bereits spezifiziertes und roadmap-kompatibles Prioritaet-1-
Inkrement. Eine neue CSP-/CORS-/Sandbox-Policy ohne konkrete Nutzerabgrenzung
waere spekulativ; Site-/Frame-Isolation waere ein groesserer, spaeterer
Architekturschritt.

**Vorschlag (offen):** Fuer Pass 7 entweder eine konkrete begrenzte
Sicherheitsregel waehlen oder verbleibende Prioritaet 1 fuer diesen Auftrag
ausdruecklich zurueckstellen und den naechsten Tracker-Punkt unter Prioritaet 2
nehmen. Die zweite Option erhoeht den Funktionsumfang, laesst aber die
breiteren Produktions-Sicherheitsluecken offen.

**Status:** Q-016 offen; keine Implementierung oder Prioritaetsverschiebung
beschlossen.

### KP-048 - Sicherheitsprioritaet zurueckgestellt

- Dokumentiert am: 2026-10-26, Europe/Berlin (UTC+01:00).
- Ereignisdatum: 2026-10-26.
- Erfassungsart: ausdrueckliche Nutzerwahl nach Tracker- und Roadmap-Pruefung.
- Quelle: aktuelle Nutzerantwort zu Q-016.
- Bezug: Q-002, Q-016, DEC-024 und KP-047; Pass-7-Auswahl.

**Nutzerangabe / Entscheidung (DEC-024):** Der Nutzer waehlte
`defer_security_move_to_priority2`: die verbleibenden breiten
Prioritaet-1-Sicherheitsluecken werden fuer diesen Zehn-Passen-Lauf
zurueckgestellt; der naechste Pass soll nach Prioritaet 2 aus `Missing.md`
gewaehlt werden. Ein Motiv wurde nicht mitgeteilt.

**Folge:** Diese Auswahl ist eine Arbeitsreihenfolge, keine Aussage, dass
Prioritaet 1 abgeschlossen, die Anwendung sicher fuer untrusted content oder
die Sicherheitsluecken akzeptabel seien.

### KP-049 - Begrenzte tabindex-Fokusreihenfolge

- Dokumentiert am: 2026-10-26, Europe/Berlin (UTC+01:00).
- Ereignisdatum: 2026-10-26.
- Erfassungsart: Implementierungsauswahl und Artefaktpruefung fuer Pass 7;
  kein separat ueberlieferter Nutzerbeschluss zum genauen `tabindex`-Umfang.
- Quelle: [Missing.md](../plans/Missing.md#2-basic-interaction-with-pages),
  [browser-shell.md](../docs/browser-shell.md#textual-link-activation),
  [PageTabIndex.cs](../src/Apps/VisualWeb.PageRendering/PageTabIndex.cs),
  [BrowserController.cs](../src/Apps/VisualWeb.Browser/BrowserController.cs),
  [HTML-Manifest](../specs/manifest.json) (`html`, checked 2026-10-04),
  lokale HTML-Quelle `specs/cache/html.html`, Abschnitt 6.6.3.
- Bezug: DEC-024, Q-009, Q-017 und Pass 7.

**Belegt:** Der Guide fuehrte `tabindex` als deferred; bestehendes
Keyboard-Tabbing umfasste bereits gerenderte Links und sichtbare aktivierte
Formcontrols in Tree Order. Der lokale HTML-Standard beschreibt positive Werte
vor nichtpositiven Werten und negative Werte ausserhalb der normalen
sequential-focus scope. Die Manifestpruefung war zum Ereignisdatum 22 Tage
alt; daher war kein Spec-Cache-Refresh erforderlich.

**Ableitung / Implementierungsauswahl:** Pass 7 begrenzt die Erweiterung auf
die bestehenden Link-/Form-Fokusziele und fuehrt keine generic rendered targets,
DOM focus API oder Page-Fokusereignisse ein. Positive signed-32-bit-Integer
werden aufsteigend vor null/Standardwerten sortiert; Tree-Order-Ties bleiben
erhalten; negative Werte werden aus der Tab-Reihenfolge entfernt. Fehlende oder
ungueltige Attribute behalten die bisherige Tree Order. Ein Wert ausserhalb
des signed-32-bit-Bereichs scheitert sichtbar statt still ignoriert zu werden.
Diese Grenzen sind eine kleine, explizit dokumentierte Umsetzung des Trackers
und keine allgemeine Konformitaets- oder Nutzerfreigabe fuer DOM Focus. Die
unterstuetzten Browserziele werden ueber IPC v46 als Daten transportiert.

**Validierungsstand bei Erfassung:** Fokussierte Shelltests bestanden lokal und
im Multiprocess-Modus; ein Test deckt positive Gleichstandreihenfolge,
Zero-/Default-Reihenfolge, negative Auslassung und ungueltige Standardwerte
ab; ein weiterer bestaetigt sichtbaren Fehler bei Ueberlauf. Alle 187
`Ipc.Tests`, die Browser-Suite (1.113 Tests) und der Solution-Build (0
Warnungen/Fehler) bestanden. Auch `dotnet test VisualWeb.slnx --no-build`
bestand. Die Formatverifikation meldet drei Findings nur in den gegenueber
HEAD unveraenderten Dateien `StaticLayout.cs` und `ShellChrome.cs`; in den
Pass-7-Dateien wurden keine Formatfehler gemeldet. `git diff --check` ist
sauber. Commit `f5bfdd7` (`feat(browser): order supported focus targets by
tabindex`) ist auf `origin/main` gepusht. Die Workflows `Push on main`,
`Windows confinement` (x64 und ARM64) und `Linux development` sind erfolgreich:
[Push](https://github.com/khmseu/VisualWeb/actions/runs/40324104295),
[Windows](https://github.com/khmseu/VisualWeb/actions/runs/40324104669),
[Linux](https://github.com/khmseu/VisualWeb/actions/runs/40324104881).
Die beiden Kontextdateien bleiben ausserhalb des Feature-Commits.

### KP-050 - Naechster Prioritaet-2-Inkrement

- Dokumentiert am: 2026-10-26, Europe/Berlin (UTC+01:00).
- Ereignisdatum: 2026-10-26.
- Erfassungsart: offene Umfangsklaerung fuer die fortgesetzte Zehn-Passen-Serie.
- Quelle: aktuelle Session, naechster Turn nach Pass 7; Projekttracker
  [Missing.md](../plans/Missing.md#2-basic-interaction-with-pages).
- Bezug: DEC-024, Q-018 und Pass 7.

**Belegt:** Pass 7 ist abgeschlossen und CI-gruen. Prioritaet 2 enthaelt
mehrere noch fehlende, voneinander unabhaengige Bereiche; darunter horizontale
Scroll-Unterstuetzung, weitere `tabindex`-Ziele und breitere Funktionen wie
Downloads oder vollstaendige History. Der Tracker bestimmt nicht den
Verhaltensumfang dieser Inkremente. Eine gezielte Suche fand keine bestehende
`ScrollX`-Implementierung; Downloads sind im Shell-Guide als nicht
implementiert dokumentiert.

**Offen:** Welcher dieser Bereiche soll Pass 8 behandeln, und welcher
begrenzte Verhaltensumfang ist gemeint? Es wird keine Implementierungsauswahl
aus dem blossen bisherigen Vorrang fuer Prioritaet 2 abgeleitet. Vor Beginn
wurde die Nutzerwahl eingeholt; siehe KP-051.

### KP-051 - Horizontales Dokument-Scrollen gewaehlt

- Dokumentiert am: 2026-10-26, Europe/Berlin (UTC+01:00).
- Ereignisdatum: 2026-10-26.
- Erfassungsart: ausdrueckliche Nutzerwahl.
- Quelle: aktuelle Nutzerantwort auf die Pass-8-Umfangsklaerung.
- Bezug: DEC-025, Q-018 und Prioritaet 2 in `plans/Missing.md`.

**Nutzerangabe / Entscheidung (DEC-025):** Der Nutzer waehlte fuer Pass 8
bounded horizontal document-level scrolling, ausdruecklich ohne nested overflow
behavior.

**Zum Zeitpunkt der Auswahl offen; spaeter geklaert:** Konkrete
Shell-Eingaben, Scrollbar-Darstellung und Offsetverhalten bei Resize/
Navigation wurden gegen die bestehenden vertikalen Scroll-Konventionen
geprueft. Die Evidenz reichte fuer die technische Ausgestaltung; Details und
Implementierungsnachweise stehen in KP-052.

### KP-052 - Horizontales Scrollen implementiert und lokal validiert

- Dokumentiert am: 2026-10-26, Europe/Berlin (UTC+01:00).
- Ereignisdatum: 2026-10-26.
- Erfassungsart: laufend dokumentierter Artefakt- und Testabgleich.
- Quellen: aktueller Turn; Commit `1463e6f`; `plans/Missing.md`,
  Browser-/Paint-/IPC-Implementierung und fokussierte Regressionstests.
- Bezug: DEC-025, Q-018, KP-050/KP-051 und Pass 8 der Zehn-Passen-Serie.

**Belegt:** Die Shell-Konventionen fuer Scroll-Offsets, Pointer/Wheel-
Eingaben, Scrollleisten und History erlaubten die technische Ausgestaltung
ohne weitere wesentliche Nutzerwahl. Gemessen wird eine begrenzte rechte
Dokumentausdehnung aus unterstuetzten Layout-Geometrien; negative
Left-Origin-Ausdehnung und nested horizontal overflow bleiben ausserhalb.
Der Offset wird waehrend Paint angewandt, ohne den Layout-Viewport oder
Frame-Rahmen zu veraendern. Dokumentbreite, `DocumentX` und `ScrollX` laufen
durch PageRendering, IPC v47, Renderer und Browser-Controller. Die
Canvas-Hintergrundflaeche wird bis zur gemessenen Dokumentbreite erweitert.
History-Eintraege speichern den horizontalen Scrollstand parallel zum
vertikalen Stand; History-Eviction entfernt beide zusammen. Die Shell bietet
Horizontal-Scrollbar, horizontales Mausrad, Shift+Vertikalrad und
Pfeiltasten.

**Validierung:** Die drei fokussierten Regressionstests fuer Scrollbar-
Zuordnung bei beiden Achsen und History-Eviction bestanden. Der Build von
`VisualWeb.slnx` bestand mit 0 Warnungen/Fehlern. `dotnet test VisualWeb.slnx
--no-build` bestand mit 14.207 Tests, darunter 1.124 Browser- und 195
IPC-Tests. `git diff --check` war sauber. `dotnet format
VisualWeb.slnx --verify-no-changes --no-restore --exclude third_party`
scheiterte an drei Diagnosen: Leerraum-/Importformatierung in
`StaticLayout.cs` und Importreihenfolge in `ShellChrome.cs`; diese Befunde
waren nicht durch Pass 8 verursacht und wurden nicht als Nebenarbeit
geaendert.

**Persistenz und CI-Stand:** 24 feature-, test- und
projektdokumentationsbezogene Dateien wurden als `1463e6f` mit
Conventional-Commit-Body und Copilot-Co-Author-Trailer committet und nach
`origin/main` gepusht. Die Kontextdateien blieben bewusst uncommittet. Bei
dieser Eintragung sind CodeQL (Workflow-Anzeige `Push on main`,
[40343334327](https://github.com/khmseu/VisualWeb/actions/runs/40343334327))
und Linux development
([40343334966](https://github.com/khmseu/VisualWeb/actions/runs/40343334966))
erfolgreich. Windows confinement (x64 und ARM64)
([40343335015](https://github.com/khmseu/VisualWeb/actions/runs/40343335015))
ist ebenfalls erfolgreich. Damit ist die CI-Matrix fuer Pass 8 abgeschlossen.
Pass 9 ist noch nicht gewaehlt oder begonnen.

### KP-053 - Pass 9 Eventumfang zur Auswahl

- Dokumentiert am: 2026-10-26, Europe/Berlin (UTC+01:00).
- Ereignisdatum: 2026-10-26.
- Erfassungsart: laufende Kandidatenpruefung; Nutzerentscheidung offen.
- Quellen: aktueller Nutzerauftrag fuer zehn Passen; `plans/Missing.md`
  Abschnitt 2; `docs/browser-shell.md` und `docs/scripting.md`; aktuelle
  Shell-, renderer- und Event-Host-Code-/Testsuche.
- Bezug: REQ-005, DEC-024, Q-019 und Pass 9.

**Belegt:** Nach Pass 8 ist der naechste noch offene Prioritaet-2-Bereich
allgemeine Page-Interaktion. Tab/Shift+Tab traversiert bereits Browser-Chrome,
unterstuetzte Links und sichtbare aktivierte Formcontrols; aktivierte Links
dispatchen im opt-in Script-Modus begrenzt `click`/`auxclick`. Der Renderer
dispatcht keine allgemeinen Tastatur-/Mausereignisse und keine DOM-
Fokusereignisse; allgemeines Hit-Testing bleibt ebenfalls offen.
Phase-11g erlaubt keine automatischen Browserereignisse und Browser-Scripts
bleiben opt-in. Tracker und vorhandene APIs bestimmen nicht, ob ein naechster
Pass Fokus-, Tastatur- oder Pointerereignisse samt jeweiliger Ziel-/Timing-
Semantik einfuehren soll.

**Vorschlag, nicht beschlossen:** Eine einzelne bounded Ereignisfamilie als
Pass 9 waehlen (Fokusereignisse auf den bestehenden Shell-Fokuszielen,
Tastaturereignisse fuer bereits fokussierte unterstuetzte Ziele oder
Primary-button Mausereignisse an unterstuetzten Linkzielen); die jeweils
anderen Ereignisse, allgemeinen DOM-Hit-Tests und Standardaktionen ausserhalb
des gewaehlten Umfangs lassen. Die wesentliche Wahl wird vor Codebeginn
angefragt. Pass 9 ist nicht begonnen.

### KP-054 - Fokusereignisse fuer Pass 9 gewaehlt

- Dokumentiert am: 2026-10-26, Europe/Berlin (UTC+01:00).
- Ereignisdatum: 2026-10-26.
- Erfassungsart: ausdrueckliche Nutzerwahl.
- Quelle: aktuelle Nutzerantwort auf die strukturierte Pass-9-Umfangsfrage.
- Bezug: DEC-026, Q-019, KP-053 und Pass 9.

**Nutzerangabe / Entscheidung:** Der Nutzer waehlte die begrenzte Variante,
opt-in Seiten bei Fokuswechseln auf bereits unterstuetzten Links und
sichtbaren aktivierten Formcontrols zu benachrichtigen. Allgemeine Treffer-
ziele sowie Tastatur-/Pointerereignisse sind ausgeschlossen.

**Offen vor Implementierung:** Welche Event-Klassen/Flags und Uebergangs-
reihenfolge den vorhandenen Browser-Fokuszustaenden entsprechen, wird anhand
der lokalen DOM/HTML-Quellen sowie der bestehenden Lebenszyklus-/Shellregeln
festgelegt. Automatic browser events, neue Fokusziele und Defaultaktionen
werden nicht aus der Auswahl abgeleitet.

### KP-055 - Fokusereignis-Uebergangsumfang offen

- Dokumentiert am: 2026-10-26, Europe/Berlin (UTC+01:00).
- Ereignisdatum: 2026-10-26.
- Erfassungsart: Laufzeitpfad-Pruefung; Nutzerklaerung erforderlich.
- Quellen: `BrowserController.FocusPage`, `FocusLink`, `FocusControl`,
  `FocusPagePosition` und `DevelopmentShell.TraverseFocus`/
  `HandlePagePrimaryPointerPress`; PageRendering- und Process-Renderer-
  Ereignisdispatch; relevante lokale `html`- und `dom`-Quellen.
- Bezug: DEC-026, Q-020, KP-054 und Pass 9.

**Belegt:** Die browser-owned Fokuszustandsaenderungen sind synchron und
werden durch Tab-Traversierung, Pointeraktivierung, Clear/Selection und
Page-/Chrome-Uebergang aufgerufen. Opt-in Klickereignisse laufen dagegen
ueber eine asynchrone Rendereroperation und anschliessenden Retained-Repaint;
Pointer-/Controlaktionen koennen sofort weiterarbeiten. Ein vollstaendiger
Fokuswechselumfang muss diese Reihenfolge mit blur/focus verknuepfen, bevor
anschliessende Browseraktion oder Rendering entscheidet. System-/OS-
Fensterfokuswechsel besitzt im betrachteten Shellpfad keine gleichwertige
fokussierte Seitenelement-Abbildung.

**Zum Erfassungszeitpunkt offen; spaeter entschieden:** Eine schmale Pass-9-
Variante kann nur Tab/Shift+Tab-Uebergaenge behandeln; die breitere Variante
deckt alle bestehenden In-Page-Zielwechsel einschliesslich Pointeraktivierung
und Wechsel zur/von Browser-Chrome ab. Diese Wahl bestimmt die
Renderer-Serialisierung. Die spaetere Nutzerwahl ist in KP-056 festgehalten.

### KP-056 - Bestehende Fokusuebergaenge fuer Pass 9 bestaetigt

- Dokumentiert am: 2026-10-26, Europe/Berlin (UTC+01:00).
- Ereignisdatum: Datum der urspruenglichen Nutzerantwort unbekannt; die
  ueberlieferte Gespraechszusammenfassung ist Quelle dieser Eintragung.
- Erfassungsart: Nutzerwahl aus der vom Nutzer bereitgestellten
  Gespraechszusammenfassung rekonstruiert.
- Quelle: aktuelle Nutzer-Nachricht mit zusammengefasstem Verlauf; sie berichtet
  die Auswahl aller bestehenden In-Page-Fokuswechsel nach KP-055.
- Bezug: DEC-026, Q-020, KP-053 bis KP-055 und Pass 9.

**Nutzerangabe / Entscheidung:** Pass 9 umfasst Tab/Shift+Tab, bereits
unterstuetzte Links und sichtbare aktivierte Formcontrols, Pointerfokus sowie
Wechsel zwischen Seite und Browser-Chrome. Betriebssystem-Fensterfokus-
ereignisse und neue Fokusziele sind ausgeschlossen.

**Abgrenzung:** Dies ist keine Freigabe fuer allgemeine DOM-Fokussierung,
Pointer-/Keyboard-Events, HTML-Eventloop oder Browser-Standardaktionen ausser
der bestehenden Shell-Interaktion. Fokus-/Blur-Reihenfolge und asynchrone
Renderer-Serialisierung muessen diese ausgewaehlten Uebergaenge abdecken.

### KP-057 - Pass 9 Fokusereignisse implementiert und lokal validiert

- Dokumentiert am: 2026-10-26, Europe/Berlin (UTC+01:00).
- Ereignisdatum: 2026-10-26; exakte Uhrzeiten aus den einzelnen Befehlen sind
  fuer die Implementierungsereignisse nicht weiter relevant.
- Erfassungsart: Implementierungs-, Test-, Git- und CI-Belege dieser Sitzung.
- Quellen: `plans/Missing.md`, `BrowserController.cs`, `RendererProtocol.cs`,
  `StaticPageRenderer.cs`, `ProcessPageRenderer.cs`, `Program.cs`,
  `ClickDispatchTests.cs`, `ProtocolTests.cs`, `LinkClickEventTests.cs`,
  `docs/browser-shell.md`, `docs/renderer-processes.md`, `docs/scripting.md`,
  Commit `0160dc0` und GitHub Actions Runs
  [40370739592](https://github.com/khmseu/VisualWeb/actions/runs/40370739592),
  [40370740622](https://github.com/khmseu/VisualWeb/actions/runs/40370740622),
  [40370740629](https://github.com/khmseu/VisualWeb/actions/runs/40370740629).
- Bezug: DEC-026, Q-019, Q-020, KP-053 bis KP-056 und Pass 9.

**Belegt:** Opted-in Dokumente koennen auf Wechseln zwischen bereits
unterstuetzten gerenderten Links und sichtbaren aktivierten Controls trusted
`blur`-/`focus`-Events erhalten. Die Renderer erzeugen begrenzte positive
document-lokale Target-IDs; IPC v48 fuehrt nur diese opaque IDs, Dokument-ID,
Eventart und Script-Policy. Blur geht Focus voraus. Lokale und Process-Renderer
verarbeiten die Events auf ihrem Owning Thread. Fokus-/Blur teilen die
per-document-Grenze von 1.024 trusted Events mit Click/Auxclick. Die
Implementierung ergaenzt weder OS-Fensterfokus, neue Fokusziele,
`FocusEvent`-spezifische Eigenschaften, `activeElement`, allgemeine
DOM-Fokussierung noch einen HTML-Eventloop.

**Belegt / Korrektur waehrend Implementierung:** Die erste vollstaendige
Browser-Suite meldete fuenf Regressionen in Navigationstests. Die fuenf Tests
bestanden am Basiscommit `1463e6f`; die Ursache war eine zu breite
Same-Tab-Serialisierung im Browser-Pump, durch die ein abgebrochener, noch
nicht abgeschlossener Load spaetere Operationen blockierte. Die Serialisierung
ignoriert nun solche bereits abgebrochenen Operationen, behaelt aber die
Reihenfolge aktiver Fokusarbeit bei. Danach bestanden die fuenf gezielten Tests
und alle 1.129 Browser-Tests.

**Validiert:** `dotnet build VisualWeb.slnx` bestand mit null Warnungen und
Fehlern; `dotnet test VisualWeb.slnx --no-build` bestand mit 14.216 Tests;
`git diff --check` war sauber. `dotnet format VisualWeb.slnx
--verify-no-changes --no-restore --exclude third_party` meldete weiterhin drei
bestehende Diagnosen ausschliesslich in den nicht geaenderten
`StaticLayout.cs`- und `ShellChrome.cs`-Dateien. Commit `0160dc0` wurde nach dem Testlauf erstellt und auf `origin/main`
gepusht. Push-on-main
([40370739592](https://github.com/khmseu/VisualWeb/actions/runs/40370739592)),
Windows confinement
([40370740622](https://github.com/khmseu/VisualWeb/actions/runs/40370740622))
und Linux development
([40370740629](https://github.com/khmseu/VisualWeb/actions/runs/40370740629))
sind erfolgreich. Pass 9 ist damit CI-gruen. Naechster vorgesehener Schritt
ist, fuer Pass 10 einen konkreten noch offenen Punkt aus `plans/Missing.md`
zu waehlen; die Kontextdateien bleiben ausserhalb des Feature-Commits.

### KP-058 - Pass-10-Umfang noch offen

- Dokumentiert am: 2026-10-26, Europe/Berlin (UTC+01:00).
- Ereignisdatum: 2026-10-26.
- Erfassungsart: Nutzerauftrag aus dem bereitgestellten Verlauf und
  Implementierungs-/Trackerpruefung dieser Sitzung.
- Quelle: Nutzerauftrag, zehn getrennte Missing-Prioritaetspassen auszufuehren;
  `plans/Missing.md` Abschnitt 2 und `docs/browser-shell.md`.
- Bezug: Q-021; Pass 9 ist in KP-057 dokumentiert.

**Nutzerangabe:** Der Arbeitsauftrag umfasst zehn getrennt validierte und
committete Passen. Nach CI-gruenem Pass 9 ist Pass 10 noch Teil des Auftrags.

**Belegt:** Der Tracker beschreibt verbleibende allgemeine Seiteninteraktion,
Hit-Testing, weitere DOM-Events und nicht abgedeckte Fokusziele, legt aber
keinen naechsten konkreten Ereignis- oder Fokusumfang fest.

**Vorschlaege, nicht beschlossen:** (1) begrenzte trusted Pointer-Hover-Events
fuer bereits vorhandene gerenderte Ziele; (2) `tabindex`-/Fokusunterstuetzung
fuer bisher nicht interaktive Ziele (bestehende offene Q-017); (3) begrenzte
DOM-Scroll-Events fuer vorhandene Root-/Nested-Scrollaktionen; (4) bis zu einer
Entscheidung keine weiteren Aktionen. Diese Alternativen haben unterschiedliche
Hit-Testing-, Renderer-, Eventziel- und Zustandsfolgen. Kein Vorschlag ist als
Nutzerwahl oder Implementierungsfreigabe festgehalten; Pass 10 bleibt
unbegonnen, bis der Umfang entschieden ist.

### KP-059 - Pass-10-Pointer-Hover-Bereich gewaehlt

- Dokumentiert am: 2026-10-26, Europe/Berlin (UTC+01:00).
- Ereignisdatum: 2026-10-26.
- Erfassungsart: ausdrueckliche Nutzerwahl in dieser Sitzung.
- Quelle: Antwort auf die Auswahlfrage fuer Pass 10; vorausgehende Tracker-
  und Codebelege aus `plans/Missing.md`, `docs/browser-shell.md` und
  `DevelopmentShell.cs`.
- Bezug: DEC-027, Q-021, Q-022 und KP-058.

**Nutzerangabe / Entscheidung:** Pass 10 soll begrenzte Pointer-Hover-
Benachrichtigungen auf bestehenden Seitenzielen behandeln. Dies waehlt den
Funktionsbereich, bestimmt aber noch nicht, ob die Seite Mouse- oder
Pointer-Eventnamen, bubbling oder pointer-spezifische Daten erhaelt.

**Belegt:** Die Shell hat bereits browser-owned Hover-Hervorhebungen fuer
sichtbare Links und Select-Popup-Zeilen; allgemeine Hover-Events werden nicht
an die Seite dispatcht. Die Browser-/Scripting-Guides begrenzen bisherige
Click-/Focus-Events ausdruecklich auf plain Events ohne Pointer-Metadaten.
Diese vorhandenen Grenzen sind zu pruefen, bevor eine Event-Semantik
implementiert wird.

**Offen:** Vor Implementierung muss Q-022 klaeren, welche konkrete Eventfamilie
und Propagation auf welchen bestehenden Zieltypen geliefert wird und ob
`relatedTarget` oder Pointer-Metadaten ausgeschlossen bleiben. Keine
Eventnamen oder Standardssemantik aus der Bezeichnung des gewaehlten Bereichs
ableiten.

### KP-060 - Pass-10-PointerEvent-Vertrag bestaetigt

- Dokumentiert am: 2026-10-26, Europe/Berlin (UTC+01:00).
- Ereignisdatum: 2026-10-26.
- Erfassungsart: ausdrueckliche Nutzerwahl in dieser Sitzung.
- Quelle: strukturierte Antwort auf Q-022.
- Bezug: DEC-027, Q-021, Q-022 und KP-059.

**Nutzerangabe / Entscheidung:** Hover-Wechsel sollen bubbling
`pointerout`/`pointerover` PointerEvents erzeugen. Der Nutzer waehlte
PointerEvents mit pointerId/type und Koordinaten, statt plain Events oder
Mouse-Events. Zielmenge: bestehende gerenderte Links sowie sichtbare
aktivierte unterstuetzte Controls. Browser-owned Popup-/Listbox-Zeilen sind
keine Seitenziele.

**Umsetzungshinweis:** Das konkrete begrenzte Koordinaten-/Pointerdatenfeld-
Subset wird aus den vorhandenen Plattform-Mausdaten abgeleitet und in den
Subsystem-Guides explizit beschrieben; keine Freigabe fuer pointer capture,
weitere Pointer-Events oder andere DOM-Input-APIs wurde erteilt.

### KP-061 - Pointer-Hover-Wahl blockiert durch Eventgrenze

- Dokumentiert am: 2026-10-26, Europe/Berlin (UTC+01:00).
- Ereignisdatum: 2026-10-26.
- Erfassungsart: Abgleich der ausdruecklichen Nutzerwahl mit einer geltenden
  Projektbegrenzung.
- Quellen: Nutzerantwort zu Q-022; `docs/scripting.md`, Abschnitt
  "Bounded synchronous synthetic events (phase 11g)"; Phase-11g-Projektvorgabe.
- Bezug: DEC-027, Q-022, Q-023, KP-059 und KP-060.

**Nutzerangabe:** Der Nutzer waehlte bubbling `pointerout`/`pointerover`
PointerEvents mit pointerId/type- und Koordinatenmetadaten auf bestehenden
gerenderten Links und sichtbaren aktivierten Controls.

**Belegt / Grenze:** Die aktuelle Event-Roadmap sagt ausdruecklich, dass
automatische Browser-Input-Events nicht hinzugefuegt werden. Ein automatischer
Dispatch bei Pointer-Hover-Wechseln faellt darunter. Daher ist diese Nutzerwahl
als Vorschlag dokumentiert, aber nicht umgesetzt; es wurde kein Pass-10-Code
begonnen. Eine blosse Vereinfachung auf weniger Pointer-Metadaten wuerde den
Konflikt nicht loesen.

**Offen:** Der Nutzer muss ein anderes mit den aktuellen Grenzen kompatibles
Pass-10-Inkrement waehlen oder weitere Arbeit blockieren. Als unverbindlicher
Vorschlag kommt eine browser-owned Hover-Darstellung fuer bestehende sichtbare
Controls ohne Seiten-DOM-Events in Frage; andere Prioritaet-2-Punkte koennen
ebenfalls vorgeschlagen werden. Q-023 bleibt offen.

### KP-062 - Weitere Arbeit gestoppt

- Dokumentiert am: 2026-10-26, Europe/Berlin (UTC+01:00).
- Ereignisdatum: 2026-10-26.
- Erfassungsart: ausdrueckliche Nutzeranweisung.
- Quelle: aktuelle Nutzerantwort "stop".
- Bezug: Q-023, DEC-027, KP-061.

Der Nutzer bat nach der Bitte um eine scope-kompatible Alternative um Stopp.
Es wird kein alternatives Pass-10-Inkrement angenommen und keine weitere
Implementierung begonnen, bis der Nutzer eine neue ausdrueckliche Anweisung
erteilt.

### KP-063 - Endziel aus Missing-Uebersicht entfernt

- Dokumentiert am: 2026-10-26, Europe/Berlin (UTC+01:00).
- Ereignisdatum: 2026-10-26.
- Erfassungsart: ausdrueckliche Nutzeranweisung.
- Quelle: aktuelle Nutzeranweisung zu `plans/Missing.md`.
- Bezug: KP-011; redaktionelle Aenderung, keine neue Funktionsanforderung.

Der Nutzer bat darum, die knappe Sektion "End goal" aus `plans/Missing.md`
zu entfernen, weil sie weniger hilfreich als erwartet war. Die Sektion wurde
entfernt. Diese redaktionelle Aenderung allein widerruft oder bestaetigt nicht
die zuvor dokumentierte Nutzerangabe zum GitHub-Ziel; deren aktueller
Anforderungsstatus wurde in diesem Auftrag nicht neu bewertet.

### KP-064 - Missing-Eintraege als niedrigere Prioritaet formulieren

- Dokumentiert am: 2026-10-26, Europe/Berlin (UTC+01:00).
- Ereignisdatum: 2026-10-26.
- Erfassungsart: ausdrueckliche Nutzeranweisung und Codepruefung.
- Quelle: aktuelle Nutzeranweisung; `plans/Missing.md`,
  `InlinePageScripts.Collect` und `docs/scripting.md`.
- Bezug: KP-063; Aktualisierung der Darstellungsweise, keine neue technische
  Zusage.

Der Nutzer bat darum, Aussagen in `plans/Missing.md`, die Funktionen als
"deferred" bezeichnen, stattdessen als niedriger priorisierte Arbeit
darzustellen. Die zwei expliziten Deferred-Aussagen wurden als derzeit
unimplementierte, niedrig priorisierte Arbeit umformuliert. Das benennt eine
Prioritaet, keine technische Sperre.

Der Nutzer fragte ausserdem nach "external scripts". Hiermit sind Script-
Elemente gemeint, deren JavaScript als separate Ressource ueber `src` referenziert
wird, etwa `<script src="app.js"></script>`, im Gegensatz zu inline JavaScript
zwischen den Script-Tags. Belegt durch `InlinePageScripts.Collect`: Beim
aktivierten Inline-Scripting wird ein klassisches Script mit `src` abgelehnt;
die Ressource wird nicht geladen oder ausgefuehrt. Module sind eine separate,
ebenfalls nicht unterstuetzte Script-Art.

### KP-065 - Einfache Websites zuerst

- Dokumentiert am: 2026-10-28, Europe/Berlin (UTC+01:00).
- Ereignisdatum: 2026-10-28.
- Erfassungsart: ausdrueckliche Nutzeranweisung und Planaktualisierung.
- Quelle: aktuelle Nutzeranweisung zu `plans/Missing.md`.
- Bezug: KP-064; ersetzt die bisherige Reihenfolge unter "Recommended priority".

**Nutzerentscheidung:** Die empfohlenen Prioritaeten sollen zuerst einen
Browser hervorbringen, der einfache Websites handhaben kann.

**Umsetzung im Plan:** Dokumentorientierte, ueberwiegend statische Seiten
bilden den ersten Arbeitsfokus: HTML-Recovery, gewoehnliches Rendering und
Schriften, essentielle Ressourcen einschliesslich Bildern, grundlegende
Navigation/Bedienung, danach kleine Script-Interaktionen. Breite
Produktionssicherheit und weitergehende Standards/APIs haben niedrigere
Prioritaet. Diese Konkretisierung ist eine Planableitung aus dem Nutzerziel,
keine separat vereinbarte vollstaendige Abnahmedefinition.

**Grenze:** Bestehende Confinement-/Request-Schutzmechanismen bleiben erhalten;
neue Ressourcen und Script-Faehigkeiten muessen ihre Sicherheitspruefungen
mitbringen. Vollstaendige Produktionssicherheit ist kein vorgeschalteter
Blocker fuer jede Kompatibilitaetsverbesserung. Keine Codeimplementierung
oder neue Sicherheits-/Konformitaetszusage in diesem Auftrag.

### KP-066 - Neue Zehn-Passen-Serie und Listendarstellung

- Dokumentiert am: 2026-10-28, Europe/Berlin (UTC+01:00).
- Ereignisdatum: 2026-10-28.
- Erfassungsart: Nutzerauftrag, Codepruefung und offene Scope-Auswahl.
- Quelle: aktueller Aufruf von `missing-priority-implementation` mit Argument
  `10`; `StaticLayout.Check`, `CssStyleEngine` und Subsystem-Guides.
- Bezug: KP-065; ersetzt den vorherigen Implementierungsstopp nur fuer den
  neuen Auftrag.

**Nutzerauftrag:** Zehn getrennt getestete und committete Inkremente nach der
neuen Simple-Site-Prioritaet, jeweils mit Push und CI-Pruefung.

**Belegt:** Der Ausgangsstand ist `81bb414` mit sauberem Worktree.
Der HTML-Parser baut gewoehnliche Listen bereits auf. Die UA-Styles definieren
Blockdarstellung und Listeneinrueckung; `StaticLayout.Check` lehnt sichtbare
`ul`, `ol` und `li` trotzdem ab. Dadurch koennen gewoehnliche statische
Seiten mit Listen nicht gerendert werden. Markerlayout/-painting fehlt.

**Offen:** Fuer das erste Inkrement ist der Listenumfang zu klaeren:
default ungeordnete Listen mit aeusseren Disc-Markern, zusaetzlich geordnete
Listen mit Dezimalnummern, oder ein breiteres CSS-List-Style-Subset.
Kein Marker darf still weggelassen oder durch unzutreffende Geometrie
angenaehert werden. Noch keine Codeaenderung, kein Pass abgeschlossen.

**Baseline-Validierung:** Die Solution-Formatpruefung auf unveraendertem
`81bb414` scheitert mit Exitcode 2 an drei vorbestehenden Diagnosen:
Whitespace und Imports in `StaticLayout.cs`, Imports in `ShellChrome.cs`.

### KP-067 - Default-Unordered-Lists fuer Pass 1 gewaehlt

- Dokumentiert am: 2026-10-28, Europe/Berlin (UTC+01:00).
- Ereignisdatum: 2026-10-28.
- Erfassungsart: ausdrueckliche Nutzerwahl und laufende Implementierung.
- Quelle: strukturierte Antwort `unordered-default`; Code und Tests fuer CSS,
  Layout, Paint, Content und lokale/Prozess-Renderer.
- Bezug: KP-066; Pass 1 der neuen Zehn-Passen-Serie.

**Nutzerentscheidung:** Zuerst default ungeordnete Listen mit aeusseren
Disc-Markern, nicht zusaetzlich geordnete Listen oder breitere List-Styles.

**Implementierung in Validierung:** `display:list-item`, vererbte begrenzte
List-Style-Longhands und eigene Disc-Marker-Geometrie/-Paintcommands.
Verschachtelte/leere Items und Hidden/None-Verhalten werden getestet;
geordnete Listen und breitere Styles bleiben explizit nicht unterstuetzt.
Der offizielle Listentext `css2-lists` wurde im Manifest registriert und
erfolgreich lokal geladen/gelesen. Keine neue DOM-/IPC-Objektweitergabe.

**Lokale Validierung:** 18 neue fokussierte Regressionen bestanden,
einschliesslich nativer Pixel und lokaler/Prozess-Retained-Repaints.
Der Solution-Build bestand mit 0 Warnungen/Fehlern; die vollstaendige
Solution-Testsuite bestand mit 14.234 Tests (CSS 329, Layout 63, Paint 39,
Content 9, Browser 1.131). Die Solution-Formatpruefung meldet ausschliesslich
die drei auf `81bb414` reproduzierten, unveraenderten Findings in
`StaticLayout.cs` und `ShellChrome.cs`. Keine native Windows-/ARM-
Ausfuehrung auf diesem Host.

### KP-068 - Pass-1-Commit und ARM64-CI-Blocker

- Dokumentiert am: 2026-10-28.
- Quelle: Commit `7bb58b518b69e751e3126db2e364579eb9867a88`, GitHub-Actions-
  Lauf [Windows confinement 40752493869](https://github.com/khmseu/VisualWeb/actions/runs/40752493869)
  einschliesslich Versuch 2; Linux development 40752494000 und
  CodeQL 40752492918.
- Bezug: KP-067; Statuskorrektur nach Commit und CI-Pruefung.

**Belegt:** Pass 1 ist committed und auf `origin/main` gepusht. Linux,
CodeQL und Windows x64 bestanden, einschliesslich der x64-Sandbox-Smokes.
ARM64 baute erfolgreich, scheiterte aber im ersten Versuch beim initialen
Peer-Render des CPU-Pressure-Tests (ProcessTests.cs:226). Nach gezieltem
Retry des fehlgeschlagenen Jobs bestanden alle fuenf fokussierten
CPU-Pressure-Wiederholungen. Im anschliessenden Solution-Lauf scheiterte
derselbe Test bei der Recovery (ProcessTests.cs:248, cancel=true) erneut
an der fuenfsekundigen Renderer-Deadline. Die anderen 1.130 Browsertests
und alle weiteren Testprojekte bestanden.

**Einordnung:** Timingempfindlichkeit ist eine Ableitung aus wechselnden
Fehlerstellen und erfolgreichen Wiederholungen, kein bewiesener
Baseline-Fehler. Keine Deadline wurde erhoeht, keine Sicherheitsgarantie
abgeschwaecht und kein Test unterdrueckt. Native ARM64-Reproduktion ist auf
diesem Linux-Host nicht moeglich. CI bleibt rot; neun weitere Passen sind
noch nicht begonnen. Das weitere Vorgehen wird zur Entscheidung gestellt.

### KP-069 - ARM64-Blocker vor weiteren Passen untersuchen

- Dokumentiert am: 2026-10-28.
- Quelle: strukturierte Nutzerantwort `investigate`; `ProcessPageRenderer`,
  `ProcessTests` und privater `Renderer.TestPeer`.
- Bezug: KP-068.

**Nutzerentscheidung:** Den ARM64-CI-Fehler zuerst untersuchen und beheben,
statt mit offenem Fehler weitere Feature-Passen zu beginnen.

**Belegt:** Die Testueberschreibung von fuenf Sekunden gilt nicht nur fuer
die CPU-Endlosschleife, sondern auch fuer kaltes AppContainer-Staging,
Handshake und den Ersatzstart. Die Produktion verwendet 30 Sekunden.
Die beiden beobachteten Fehler liegen im normalen Initial-/Recovery-Render.

**Korrektur in Validierung:** Der CPU-Pressure-Test verwendet die vorhandene
Produktionsdeadline statt der fuenfsekundigen Testueberschreibung. Produktcode,
Confinement und 500-ms-Cancellation bleiben unveraendert. Die Endlosschleife
muss weiterhin durch die Deadline beendet werden; Exit, Peer-Isolation,
Ersatzprozess, Pixel und Wiederverwendung werden unveraendert geprueft.
Dies ist eine separate CI-Korrektur, kein zweiter Simple-Site-Feature-Pass.

**Lokale Validierung:** Solution-Build (0 Warnungen/Fehler), Browserprojekt
(1.131 Tests) und volle Solution (14.234 Tests) bestanden. Formatpruefung
meldet nur die drei in KP-066 belegten Baseline-Findings. Native
Windows-Validierung erfolgt nach Push in CI.

### KP-070 - CI-Korrektur nativ validiert und naechster Listenumfang

- Dokumentiert am: 2026-10-28.
- Quelle: Commit `8dfe817`; GitHub Actions
  [Windows 40758152591](https://github.com/khmseu/VisualWeb/actions/runs/40758152591),
  [Linux 40758152794](https://github.com/khmseu/VisualWeb/actions/runs/40758152794)
  und [CodeQL 40758152061](https://github.com/khmseu/VisualWeb/actions/runs/40758152061);
  erneute Pruefung von `StaticLayout.Check`, UA-Styles und Rendering-Guides.
- Bezug: KP-069, neuer Zehn-Passen-Auftrag KP-066.

**Belegt:** Alle drei CI-Laeufe bestanden. Windows x64 und ARM64 liefen
einschliesslich der wiederholten CPU-Pressure-Tests, vollen Solution und
nativen Smokes erfolgreich. Damit ist der CI-Blocker behoben und Pass 1
abgeschlossen. Die Testkorrektur zaehlt nicht als weiterer Feature-Pass.

**Naechster Blocker:** Default ungeordnete Listen sind geliefert, geordnete
Listen werden weiterhin vollstaendig abgelehnt. Die Standardsquelle
beschreibt Dezimalmarker und Nummerierung ueber `start`, `reversed` und
`li value`; der endliche Umfang des zweiten Inkrements ist noch offen.
Keine Pass-2-Codeaenderung. Neun Feature-Passen stehen aus.

### KP-071 - Dezimalnummerierung fuer Pass 2 gewaehlt

- Dokumentiert am: 2026-10-28.
- Quelle: strukturierte Nutzerantwort `decimal-numbering`; lokale Quellen
  `html` (ol/li und Integer-Mikrosyntax) und `css2-lists`; aktuelle
  CSS-/Layout-/Paint-/Content-/Browser-Implementierung und Tests.
- Bezug: KP-070; Pass 2 des neuen Zehn-Passen-Auftrags.

**Nutzerentscheidung:** Dezimalmarker einschliesslich `start`, `reversed`
und `li value`, nicht nur default aufsteigende Listen. Andere Markerarten
bleiben unimplementiert.

**Umsetzung in Validierung:** Eigene geformte Markerruns ohne synthetische
DOM-Textknoten; gemeinsame Paint-Glyph-Logik mit normalen Textfragmenten.
Direkte gerenderte List-Items werden einmal je Liste unter Node-Limits
nummeriert. Versteckte oder als Block formatierte Items zaehlen nicht;
Markerunterdrueckung per `none` laesst die Nummerierung bestehen.
Verschachtelte Listen haben unabhaengige Zaehler und UA-Style-Resets.
HTML-Integersyntax wird mit expliziter signed-32-bit-Grenze fuer Attribute
gelesen; automatische Fortschreibung verwendet long und laeuft nicht um.
Scans/Markerausgabe teilen Text-, Shaping-, Glyph-, Command- und
Cancellation-Budgets. Breitere List-Ownership, CSS-Counter und nichtdezimaler
Legacy-Typ bleiben sichtbar nicht unterstuetzt.

**Zwischenstand:** CSS 330, Layout 77, Paint 40 und Content 10 Tests bestanden.
Build, Browserprojekt und volle Solution sind in Validierung. Format meldet
nur die drei belegten Baseline-Findings. Noch kein Pass-2-Commit.

**Abschliessende lokale Validierung:** 19 neue Regressionen; Solution-Build
mit 0 Warnungen/Fehlern, Browserprojekt mit 1.133 und volle Solution mit
14.253 Tests bestanden. Keine neuen Formatfindings. Native Targets werden
nach Commit/Push durch CI validiert.

### KP-072 - Pass 2 gruen und List-Style-Shorthand fuer Pass 3

- Dokumentiert am: 2026-10-28.
- Quelle: Commit `6441d67`; CI
  [Linux 40767827687](https://github.com/khmseu/VisualWeb/actions/runs/40767827687),
  [Windows 40767827860](https://github.com/khmseu/VisualWeb/actions/runs/40767827860),
  [CodeQL 40767826172](https://github.com/khmseu/VisualWeb/actions/runs/40767826172);
  `CssProperties.Expand`, lokaler Standard `css2-lists` und aktuelle Tests.
- Bezug: KP-071, Pass 3 des Zehn-Passen-Auftrags.

**Belegt:** Pass 2 ist committed, gepusht und auf allen genannten CI-Targets
erfolgreich. Als naechster konkreter Common-CSS-Blocker war `list-style:none`
noch als unbekannte Property diagnostiziert; Layout verlangt null Diagnosen.

**Umsetzung:** Das Shorthand expandiert atomar zu den vorhandenen drei
Longhands mit disc/decimal/none, outside und image none. Ausgelassene Werte
werden zurueckgesetzt, `none`-Mehrdeutigkeit, CSS-wide-Keywords, Importance
und Deklarationsreihenfolge bleiben konsistent mit der Cascade. Nicht
unterstuetzte Werte/Images, doppelte Komponenten und gemischte Wide-Keywords
diagnostizieren die ganze Deklaration. Keine neue Layout-/Paint-Faehigkeit
und keine automatische Embedded-Style-Sammlung in Offline-Content.

**Validierung:** 20 neue Regressionen (19 CSS, eine native Content-Fixture),
CSS 349 und Content 11 Tests bestanden. Solution-Build bestand mit null
Warnungen/Fehlern; alle 14.273 Solution-Tests bestanden. Format meldet nur
die drei bekannten Baseline-Findings. Noch kein Pass-3-Commit/CI-Ergebnis.

### KP-073 - Pass 3 bestaetigt und Legacy-Doctypes fuer Pass 4

- Dokumentiert am: 2026-10-28.
- Quelle: Commit `70c7aef`; CI
  [Linux 40771152013](https://github.com/khmseu/VisualWeb/actions/runs/40771152013),
  [Windows 40771152048](https://github.com/khmseu/VisualWeb/actions/runs/40771152048),
  [CodeQL 40771152079](https://github.com/khmseu/VisualWeb/actions/runs/40771152079);
  `HtmlParser`, `DomDocumentMode`, lokaler HTML-Standard und aktuelle Tests.
- Bezug: KP-072, Pass 4 des neuen Zehn-Passen-Auftrags.

**Belegt:** Pass 3 ist gepusht und auf allen genannten Targets gruen.
Der naechste Parserblocker war die explizite Ablehnung von Legacy-Doctypes,
obwohl das native DOM alle drei Dokumentmodi bereits besitzt.

**Umsetzung in Validierung:** Die initiale HTML-Moduswahl verwendet die
vollstaendigen Identifierregeln, einschliesslich Force-Quirks-Vorrang,
ASCII-Vergleichen und fehlenden/leeren versus nichtleeren HTML-4.01-System-IDs.
Die 55 Quirks-Praefixe wurden per exaktem Tabellenvergleich gegen den lokalen
HTML-Cache geprueft: keine fehlenden/ueberzaehligen Eintraege, gleiche Reihenfolge.
Unbekannte IDs bleiben inert erhalten; nichtkonforme Doctypes melden
`invalid-doctype`, statt die Seite abzubrechen. Keine DTD-Requests, kein
srcdoc-/Fragmentmodus und keine neue Quirks-Layout-Zusage.

**Zwischenstand:** 35 neue Parser-Modus-/Recoveryfaelle und eine native
Content-Fixture bestanden; ein frueherer Unsupported-Fall wurde durch die
neue positive Abdeckung ersetzt. Build/volle Solution laufen. Format meldet
nur bekannte Baseline-Findings. Noch kein Pass-4-Commit.

**Abschliessende lokale Validierung:** HTML-Projekt 7.270 Tests, Content 12,
Solution-Build (0 Warnungen/Fehler) und volle Solution 14.308 Tests bestanden.
Format meldet ausschliesslich die belegten Baseline-Findings. CI nach Push
bleibt erforderlich.

### KP-074 - Vier Passen bestaetigt und Tabellenlayout-Auswahl

- Dokumentiert am: 2026-10-28.
- Quelle: Commit `cef5ce6`; CI
  [Linux 40774705097](https://github.com/khmseu/VisualWeb/actions/runs/40774705097),
  [Windows 40774705119](https://github.com/khmseu/VisualWeb/actions/runs/40774705119),
  [CodeQL 40774704995](https://github.com/khmseu/VisualWeb/actions/runs/40774704995);
  `StaticLayout.Check`, CSS-Propertyregistry/UA-Styles und Rendering-Guides.
- Bezug: KP-073; Pass-5-Auswahl des Zehn-Passen-Auftrags.

**Belegt:** Pass 4 ist gepusht; alle drei genannten CI-Laeufe bestanden.
Vier Feature-Passen abgeschlossen, sechs verbleiben. Der Parser baut einfache
Tabellen bereits, aber sichtbare Tabellen werden in Layout explizit abgelehnt.
Tabellen-Displaywerte und Sizing-Properties fehlen in CSS.

**Offene materielle Designentscheidung:** Ein erster Tabellenlayout-Umfang
kann inhaltsbasierte automatische Spaltenbreiten fuer einfache Tabellen
liefern oder zunaechst nur explizites Fixed-Layout. Erstes hilft
unveraenderten einfachen Webseiten unmittelbar, verlangt aber intrinsische
Text-/Zellmessung; zweites ist kleiner und deterministischer, hilft ohne
passende Autoren-CSS aber nicht. Spans, collapsed borders und allgemeinere
Tabellensemantik sollen nicht still angenaehert werden. Konkrete Auswahl
einschliesslich expliziter Blockieroption wird gestellt. Keine
Tabellenlayout-Implementierung begonnen.

### KP-075 - Automatische einfache Tabellen fuer Pass 5

- Dokumentiert am: 2026-10-29.
- Quelle: Nutzerwahl `auto-basic` im laufenden Gespraech; CSS-Registry,
  `TableLayout.cs`, Layout-/CSS-/Content-Tests und offizieller lokaler
  CSS-2.2-Tabellencache (`css2-tables`).
- Bezug: KP-074; Pass 5 des Zehn-Passen-Auftrags.

**Nutzerentscheidung, beschlossen:** Inhaltsbasierte automatische Spalten
fuer einfache Text-/Blockzellen ohne Spans. Fixed-only haette passende
Autoren-CSS verlangt und unveraenderte einfache Seiten weniger unterstuetzt.
Die angebotene Blockieroption wurde nicht gewaehlt.

**Implementiert, noch nicht committed:** Kanonische HTML-Tabellen erhalten
intrinsische Min-/Max-Messung, automatische Spaltenverteilung, separate
borders, einen Spacing-Wert und top-aligned Zellen. Zellhoehe ist ein Minimum;
Zellen gleichen sich der Zeilenhoehe an. Vorhandener Layout-/Paint-/Metadatenfluss
wird wiederverwendet; Messung und Anordnung teilen Budgets und Cancellation.
Spans, captions/columns, anonymous tables, nested tables/widgets, Prozentmasse
in Zellen, row/group backgrounds und weitere im Guide gelistete Faelle
scheitern explizit. Row-background-Rechtecke wuerden Spacing-Luecken falsch
fuellen; deshalb keine stille Naeherung. Keine Netzwerk-/IPC-/Confinementaenderung.

**Standardbeleg:** `css2-tables` registriert, SpecCache erfolgreich ausgefuehrt.
CSS 2.2 erlaubt UA-definierte automatische Breitenverteilung; die gewaehlte
endliche Policy ist kein Nachweis pixelgleicher allgemeiner Browserkonformitaet.

**Zwischenvalidierung:** Layout 98, CSS 356, Content 13 Tests bestanden.
Browser-Tabellen-Scroll-/Link-/Resizefaelle laufen noch. Build, volle Browser-/
Paint-/Solution-Suiten, Format und anschliessendes Commit/Push/CI stehen aus.

**Abschliessende lokale Validierung:** 32 neue Regressionen; Layout 99,
CSS 356, Content 13, Paint 40, Browser 1.135 und alle 14.340 Solution-Tests
bestanden. Build: null Warnungen/Fehler. Die erste Formatpruefung fand neue
Whitespace-Findings; diese wurden korrigiert. Die Wiederholung meldet nur
die drei auf `81bb414` belegten Baseline-Findings. Die ersten beiden
Browser-Tabellenfaelle erwarteten irrtuemlich das Entfernen eines offscreen
Links; der vorhandene Fokusvertrag behaelt Identitaet ohne sichtbare Rects.
Die Tests pruefen jetzt diesen Vertrag und wieder sichtbare Rects nach Resize,
ohne Produktionsverhalten zu veraendern. Commit/Push/CI bleiben erforderlich.
