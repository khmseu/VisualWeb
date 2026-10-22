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
