# Wissensstand

## Pflege und Abdeckung

- Projekt: VisualWeb, Repository `khmseu/VisualWeb`.
- Bestaetigter Projektordner: `/bigdata/KAI/projects/VisualWeb`.
- Dokumentationsdatum: 2026-10-30, Zeitzone Europe/Berlin (UTC+01:00).
- Aktuelle Serie: Der neue Auftrag vom 2026-10-30 ist unter
  [Auftrag und Ziel](#auftrag-und-ziel) und KP-083 erfasst. Der Auftrag vom
  2026-10-28 (KP-066 bis KP-082) ist abgeschlossen. Aeltere Serienstaende und deren
  damalige naechste Schritte bleiben unten als historische Evidenz erhalten,
  nicht als aktueller Arbeitsauftrag. Alte Quellen wurden nicht pauschal neu geprueft.
- Rekonstruktionsstand: teilweise; Originalauftrag ab 2026-10-04,
  ausgewaehlte Entscheidungen bis 2026-10-22 nachvollzogen.
- Aelterer Artefaktstand: `c39b6d3` (`feat(browser): confirm typed cross-origin
address-bar navigation`) und die Projektkartenkorrektur `41f6e96` wurden am
  2026-10-26 auf `origin/main` gepusht; CodeQL, Linux development und Windows
  confinement sind fuer beide Commits erfolgreich. Pass 3 wurde als
  `93111dc` gepusht; CodeQL, Linux development und Windows confinement sind
  ebenfalls erfolgreich.
- Aelterer Arbeitsstand: Die zehn vorherigen HTML-Recovery-Passen sind getrennt
  committed, gepusht und abschliessend CI-gruen. Eine neue Zehn-Passen-Serie
  ist angefragt. Pass 1, page-initiated `about:blank` mit geerbter Origin,
  browser-UI blank opaque und Origin-erhaltender History, ist implementiert
  und CI-validiert (Commit `a4490f9`). Frames, Sandbox und weitere
  Origin-Policy bleiben offen. Fuer Pass 2 waehlte der Nutzer explizite
  Cross-Origin-Bestaetigung; Code, Shell-README und Regressionstests zeigen,
  dass genau dieses Verhalten bereits implementiert ist. Kein Pass-2-Code
  geaendert. Anschliessend stellte der Nutzer die restliche Security-
  Prioritaet fuer diesen Lauf zurueck und waehlte vertikales verschachteltes
  Mausrad-Scrollen mit Edge-Chaining als naechstes Inkrement. Die Auswahl ist
  aus der zusammengefassten Gespraechsuebergabe rekonstruiert; der genaue
  Antwortwortlaut liegt hier nicht vor. Der CSS-Scope wurde als
  `overflow-x:hidden` plus `overflow-y:auto` auf definite-height
  Blockcontainern festgelegt und ist jetzt umgesetzt/testvalidiert inklusive
  IPC-/Paint-/Shell-Wiring. CSS Overflow Module 3 und CSS Overscroll Behavior
  Module 1 wurden am 2026-10-23 im Manifest registriert und lokal
  aktualisiert/gelesen. Commit `8aaf65c` wurde am 2026-10-26 gepusht;
  CodeQL, Linux development und Windows confinement sind erfolgreich.
  Der Nutzer hat danach erneut zehn Missing-Prioritaets-Passen angefordert.
  Fuer die naechste Sicherheits-Passwahl entschied der Nutzer, die
  Cross-Origin-Bestaetigung auf getippte HTTP(S)-Adressleisten-Navigation
  auszudehnen. Umsetzung, Shell-Regressionstests (1.098), Solution-Build und
  Volltests sind erfolgreich. Die Solution-Formatpruefung meldet nur die
  bekannten Findings in `StaticLayout.cs` und `ShellChrome.cs`. Commit
  `c39b6d3` ist gepusht; CodeQL (Run `40273905674`), Linux development
  (`40273905621`) und Windows confinement (`40273905697`) sind erfolgreich.
  Q-002 bleibt als breitere Produktionssicherheitsfrage offen. Fuer Pass 2
  waehlte der Nutzer die Bestaetigung jeder getippten HTTP(S)-Adressleisten-
  Navigation, wenn noch kein Dokument committed ist. Die Implementierung,
  zwei fokussierte Tests, alle 1.100 Browser-Tests, der Solution-Build und die
  vollstaendige Solution-Testsuite sind erfolgreich. Die Formatpruefung meldet
  nur die unveraenderten Findings in `StaticLayout.cs` und `ShellChrome.cs`.
  Pass 2 ist als `0b6ca2d` gepusht; CodeQL, Linux development und Windows
  confinement sind erfolgreich. Das Worktree enthaelt weiterhin
  vorbestehende Markdown-Formatierungsaenderungen in dieser Datei; diese sind
  nicht Teil des Pass-Commits. Fuer Pass 3 waehlte der Nutzer, automatisch
  gestartete HTTP(S)-Shell-Navigation ebenfalls vor dem ersten Request zu
  bestaetigen (DEC-018/KP-037). Die Implementierung leitet Launch-URLs und
  Shell-Navigation ueber den vorhandenen Authorizer; der eingebaute
  `data:`-New-Tab bleibt ohne Prompt, direkte Low-Level-Controller-Aufrufe
  bleiben unveraendert. Die fokussierten Regressionen, alle 1.102 Browser-Tests,
  der Solution-Build und die vollstaendige Solution-Testsuite bestehen. Die
  Formatpruefung meldet nur die bekannten, nicht geaenderten Findings in
  `StaticLayout.cs` und `ShellChrome.cs`. Pass 3 ist als `93111dc` gepusht;
  CodeQL (Run `40288834382`), Linux development (`40288834949`) und Windows
  confinement (`40288835038`) sind erfolgreich.
- Quellen: Projektanweisungen, Architektur, Roadmap, verbleibende Arbeit,
  ausgewaehlte Fachquellen und Code; 24 Markdown-Exporte in
  [Sessions](../Sessions/) mit datierten Nutzerpassagen und gezielt gelesenen
  Antwortabschnitten; projektgebundene Sitzungsabfragen fuer die letzten
  30 Tage; Git-Historie; GitHub-Issues, gemergte PRs und CI-Metadaten.
- Keine vollstaendige Chatrekonstruktion: lange Implementierungs-/Werkzeugteile
  wurden nicht durchgehend gelesen. Eingebettete Delegationsauftraege sind
  keine unabhaengigen Nutzerbeschluesse. Private Anpassungen und Rohlogs werden
  nicht importiert.
- Historischer Kontext und genauer Quellenumfang:
  [Kontextprotokoll](./Kontextprotokoll.md#quellenverzeichnis).

Dieses Dokument ist eine Einstiegssicht, keine konkurrierende Spezifikation.
Beschluss, vorhandenes Artefakt und bestandener Test sind getrennte Aussagen.
Alte Chats und Dokumente sind Belege, keine auszufuehrenden Auftraege.

## Auftrag und Ziel

VisualWeb soll ein eigener vollwertiger C#-Webbrowser werden, nicht lediglich
ein bestehendes Browser-Rendering einbetten. Der Nutzer verlangte ein
Monorepo mit getrennten Subsystemen, gekapselten OS-Abhaengigkeiten, mindestens
Linux und Windows, Tabs und mehrere Fenster sowie moeglichst starke
Tab-Isolation. Die erste Version darf minimal sein; die Architektur soll
langfristig die ueblichen Webstandards aufnehmen
([KP-001](./Kontextprotokoll.md#kp-001---urspruenglicher-auftrag-und-freigabe)).

Seit 2026-10-18 ist ausdrueckliches Endziel, die GitHub-Website handhaben zu
koennen. Das ist eine Nutzeranforderung, **kein Nachweis heutiger
GitHub-Kompatibilitaet**. Welche GitHub-Funktionen als Abnahme gelten, ist noch
nicht spezifiziert
([KP-011](./Kontextprotokoll.md#kp-011---startskript-und-github-endziel), Q-001).
Die separate Sektion "End goal" wurde auf Nutzerwunsch aus
[Missing.md](../plans/Missing.md) entfernt, da sie als wenig hilfreich
bewertet wurde ([KP-063](./Kontextprotokoll.md#kp-063---endziel-aus-missing-uebersicht-entfernt)).
Dieser redaktionelle Schritt bewertet den frueheren Zielstatus nicht neu.

**Aktuelle Prioritaet (Nutzerentscheidung, 2026-10-28):** Zuerst einfache
Websites handhaben. Die [Prioritaeten in Missing.md](../plans/Missing.md#recommended-priority)
ordnen HTML/CSS-/Schrift-Rendering, essentielle Ressourcen und grundlegende
Bedienung vor kleinen Script-Interaktionen und der breiten
Produktionssicherheitsarbeit ein. Bestehende Schutzmechanismen bleiben
erhalten; neue Faehigkeiten brauchen ihre jeweiligen Sicherheitspruefungen.
Die Umordnung ist keine Freigabe fuer beliebige untrusted Websites und
startet keine neue Implementierung
([KP-065](./Kontextprotokoll.md#kp-065---einfache-websites-zuerst)).

**Aktueller Implementierungsauftrag (2026-10-30):** Der Nutzer hat erneut die
Missing-Prioritaetsimplementierung mit zehn Passen aufgerufen. Jeder Pass soll
ein eigenes testbares Inkrement nach Trackerprioritaet enthalten und vor dem
naechsten Pass validiert, committed, gepusht und hinsichtlich CI geprueft werden.
Eine ungeloeste wesentliche Verhaltensentscheidung wird vor Implementierung
gefragt; bestehende Sicherheitsgrenzen bleiben erhalten. Pass 1 wird auf
absolute CSS-Laengeneinheiten (`in`, `cm`, `mm`, `Q`, `pt`, `pc`) fuer
bereits unterstuetzte Properties eingegrenzt. Der vorherige Parser akzeptierte
nur `px`, `em` und `rem`. Die Umsetzung ist lokal getestet: 29 CSS- und ein
nativer Content-Regressionstest, Build mit null Warnungen/Fehlern und alle
14.676 Solution-Tests bestanden. Die drei Formatfehler wurden unveraendert
auf dem Ausgangscommit `ad0bca6` reproduziert. Commit, Push und CI stehen aus
([KP-083](./Kontextprotokoll.md#kp-083---neuer-zehn-passen-auftrag-und-css-laengen)).

**Abgeschlossener Implementierungsauftrag (2026-10-28):** Zehn getrennt validierte,
committete und gepushte Passen mit CI-Pruefung sind angefragt
([KP-066](./Kontextprotokoll.md#kp-066---neue-zehn-passen-serie-und-listendarstellung)).
Alle zehn Feature-Passen dieser frueheren Serie sind abgeschlossen:

| Pass | Implementiertes Inkrement                                                  | Commit    | Evidenz           |
| ---- | -------------------------------------------------------------------------- | --------- | ----------------- |
| 1    | Default ungeordnete Listen mit aeusseren Disc-Markern, vom Nutzer gewaehlt | `7bb58b5` | KP-067 bis KP-070 |
| 2    | Dezimalmarker mit `start`, `reversed`, `li value`, vom Nutzer gewaehlt     | `6441d67` | KP-071/KP-072     |
| 3    | Atomarer endlicher `list-style`-Shorthand                                  | `70c7aef` | KP-072/KP-073     |
| 4    | Vollstaendige initiale Legacy-Doctype-Moduswahl mit Recoverydiagnostik     | `cef5ce6` | KP-073/KP-074     |
| 5    | Automatische einfache HTML-Tabellen, vom Nutzer gewaehlt                   | `ff918ff` | KP-075/KP-076     |
| 6    | Satzzeichen/URLs in nicht umbrechendem pre/nowrap-Text                     | `35543c3` | KP-076/KP-077     |
| 7    | Explizite vertrauenswuerdige Bold-/Italic-/BoldItalic-Dateien end-to-end   | `780250f` | KP-078            |
| 8    | Alle 148 standardisierten opaken CSS-Farbnamen                             | `8832410` | KP-079            |
| 9    | Atomarer endlicher Farb-/none-Background-Shorthand                         | `9066215` | KP-080            |
| 10   | Atomarer endlicher Font-Shorthand mit fuenf Longhand-Resets                | `c4987a0` | KP-081/KP-082     |

Alle zehn Inkremente sind gepusht. Der aktuelle Stand besteht Linux,
CodeQL und Windows x64/ARM64 CI einschliesslich nativer Smokes.
Zuletzt lokal validiert: Build und 14.646 Solution-Tests einschliesslich
Browser 1.194, CSS 581 und Content 20. Pass 10: Linux `40835953198`,
Windows `40835953086` und CodeQL `40835953318` erfolgreich.
Pass 9 ist CI-bestaetigt; Windows x64
brauchte nach einem nicht reproduzierten alten 15s-Historytest-Timeout einen
einzelnen unveraenderten Failed-Job-ReRun (KP-080). Ursache bleibt unbewiesen.
Format meldet nur drei auf `81bb414` belegte Baseline-Findings
(Whitespace/Imports in `StaticLayout.cs`, Imports in `ShellChrome.cs`).

Die separate Testkorrektur `8dfe817` zaehlt nicht als Feature-Pass:
Nach Nutzerentscheidung wurde der ARM64-CI-Blocker vor weiteren Features
behoben. Der CPU-Pressure-Test nutzt die unveraenderte Produktionsdeadline
von 30 Sekunden statt eines 5-Sekunden-Testoverrides; Cancellation und
Confinement bleiben unveraendert (KP-068 bis KP-070).

**Pass 5 bestaetigt (KP-075/KP-076):** Der Nutzer waehlte `auto-basic`: automatische
inhaltsbasierte Spalten fuer einfache Text-/Blockzellen ohne Spans statt
ausschliesslich explizitem Fixed-Layout. Die Implementierung umfasst kanonische
HTML-Tabellen mit separate borders, einem Spacing-Laengenwert und Top-Alignment.
Explizite Ausschluesse stehen im [Layoutguide](../docs/text-layout.md#basic-automatic-tables).
`css2-tables` wurde registriert und erfolgreich lokal aktualisiert.
Layout 99, CSS 356, Content 13, Paint 40, Browser 1.135 und alle 14.340
Solution-Tests sowie der Build bestehen. Format meldet nur die drei bekannten
Baseline-Findings. 32 neue Regressionen pruefen den Tabellenumfang.
Pass 5 ist als `ff918ff` committed und gepusht; Linux (`40789165962`),
Windows (`40789165996`) und CodeQL (`40789166119`) sind erfolgreich.

**Pass 6 abgeschlossen:** Gewoehnliche URLs und Bindestrich-/Satzzeichentexte
scheitern selbst bei `white-space:pre/nowrap` an der Layout-Zeilenbruchpruefung.
Diese Modi erlauben keine Soft-Wraps; die bestehende Textvalidierung bleibt
erforderlich, die Unicode-Break-Analyse dort aber nicht. Geplant ist eine
begrenzte Korrektur nur fuer diese Modi, nicht allgemeines Unicode-Wrapping.
Die Korrektur ist implementiert: Layout 114 und Content 15 Tests bestehen,
darunter 17 neue Regressionen fuer Text/Geometrie, Whitespace, intrinsische
Tabellenbreite, Limits und native Pixel/Retained-Resize. Build und alle 14.357
Solution-Tests bestanden; Format meldet nur drei bekannte Baseline-Findings.
Commit `35543c3` ist gepusht; Linux (`40791947973`), Windows x64/ARM64
(`40791947945`) und CodeQL (`40791947748`) bestehen.

**Q-024 - Umfang des naechsten Schriftinkrements (beschlossen, KP-077/KP-078):**
Default-Headings/strong/em verlangen echte Bold-/Italic-Faces; der Browser
registriert derzeit nur Regular 400 normal. Vorschlaege: optionale explizite
vertrauenswuerdige Pfade fuer Bold/Italic/BoldItalic durch Local-/Worker-
und Confinementpfade fuehren, oder zunaechst nur Bold. Fehlende Faces bleiben
sichtbare Fehler; keine Synthese, kein Systemfallback, keine pauschalen Mounts.
Der Nutzer waehlte `four-faces`: alle drei optionalen Pfade end-to-end.
Pass 7 ist implementiert, committed/gepusht als `780250f` und CI-bestaetigt:
Linux `40825983844`, Windows x64/ARM64 `40825983728`, CodeQL `40825983204`.
51 neue Regressionen, Browser 1.186 und alle 14.408 Solution-Tests sowie Build
bestanden. Platform 40, Linux-Denial-Smoke und echter confined CLI-Smoke mit
vier Faces bestehen. Format meldet nur drei bekannte Baseline-Findings.
Pass 8 erweitert die belegte 18-Farbnamen-
Grenze auf die vollstaendige offizielle Tabelle mit 148 opaken sRGB-Namen
(CSS Color 4, Cache 2026-10-04; keine Systemfarben/neuen Farbraeume).
155 neue Regressionen und alle 14.563 Tests bestehen lokal.
Pass 8 ist als `8832410` gepusht; Linux `40828734021`, Windows `40828734049`
und CodeQL `40828733649` bestehen. Pass 9 implementiert den belegten
fehlenden Farb-/none-Subset des `background`-Shorthands; weitere
Hintergrundkomponenten bleiben diagnostizierte Ausschluesse (KP-080).
Pass 9: Linux `40831928303`, Windows `40831928285` nach einmaligem ReRun und
CodeQL `40831928331` erfolgreich. Pass 10 implementiert den belegten fehlenden
`font`-Shorthand fuer die fuenf vorhandenen Font-Longhands (KP-081).
Pass 10 besteht lokal: 52 neue Regressionen, Build und alle 14.646 Tests.
Commit `c4987a0` ist gepusht und auf allen CI-Plattformen erfolgreich (KP-082).
Die beauftragte Serie endet hier; breite Kompatibilitaetsbereiche in
Missing.md bleiben offen und werden nicht pauschal als erledigt markiert.

## Rahmenbedingungen und Nicht-Ziele

- .NET 10; nullable und Warnungen als Fehler; zentrale Paketversionen;
  xUnit v3. Plattformziele Windows 10+ und Linux, jeweils x64/arm64.
- Linux muss X11 **und** Wayland unterstuetzen. SDL3, SkiaSharp und
  HarfBuzzSharp liegen hinter OS-neutralen Grenzen; V8 wurde ausdruecklich
  gewaehlt, ClearScript spaeter als Host umgesetzt.
- Offizielle Schnittstellenquellen und Abschnittsanker; lokaler
  [Standards-Cache](../docs/standards.md) mit expliziter Aktualisierung bei
  fehlenden oder mehr als 30 Tage alten Dokumenten, unabhaengig von Tests.
  In dieser Dokumentationsrekonstruktion wurden keine Standards aufgefrischt.
- Browser/Renderer getrennt; IPC nur Daten. DOM, V8 und native Ressourcen sind
  rendererlokal. Prozessseparation ist weder vollstaendige Websicherheit noch
  Site-/Frame-Isolation.
- CLI: empfohlen `--multiprocess`, mit unterstuetzter Confinement vor Start
  und ohne stillen Fallback. Unsandboxed-Entwicklung nur nach expliziter
  Trusted-Content-Bestaetigung; Schriftdatei explizit registrieren.
- Der Browser bleibt ein Entwicklungsbrowser fuer einen endlichen
  HTML/CSS-/Interaktionsumfang, **nicht sicher fuer beliebige feindliche
  Websites**. Breite SOP/CORS/CSP, allgemeine Ressourcen-/Event-Loop-
  Integration und Produktionsauslieferung sind nicht durch Teilinkremente
  erledigt.
- Nicht unterstuetztes Verhalten muss sichtbar scheitern; keine stillen
  Naeherungen, geschwaechten Sandbox-Garantien oder erfundenen Target-Nachweise.
- Repositorypfade immer unter der bestaetigten Wurzel aufloesen, nicht aus
  Projektnamen konstruieren
  ([KP-004](./Kontextprotokoll.md#kp-004---arbeitsablauf-pfade-und-commit-qualitaet)).

## Wissenslandkarte und Begriffe

| Thema                           | Bedeutung und Einstieg                                                                              | Autoritative Quelle                                                                                                                                  |
| ------------------------------- | --------------------------------------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------- |
| Projektgrenzen und Konventionen | Aktuelle Arbeitsregeln; Projektpfade und Validierung                                                | [AGENTS.md](../AGENTS.md)                                                                                                                            |
| Subsysteme                      | Besitzer und Abhaengigkeitsrichtung                                                                 | [Projektkarte](../docs/project-map.md), [Architektur](../docs/architecture/overview.md)                                                              |
| Historische Phasen              | Freigegebene Meilensteine, nicht aktuelle Faehigkeitsmatrix                                         | [Roadmap](../plans/browser-foundation-plan.md), [ADR 0001](../docs/adr/0001-browser-foundation.md)                                                   |
| Naechste Arbeit                 | Narrative Prioritaeten und explizite Restluecken; niedrigere Prioritaet ist keine technische Sperre | [Missing.md](../plans/Missing.md#recommended-priority)                                                                                               |
| Standards und Fixtures          | Cache ist Referenzbestand; gepinnte Testdaten bleiben separat                                       | [Standards](../docs/standards.md), [Manifest](../specs/manifest.json)                                                                                |
| Browser-Broker                  | Privilegierte Navigation und Stylesheet-Requests, nicht rendererseitiges Fetch                      | [Shell](../docs/browser-shell.md), [Netzwerk](../docs/networking.md)                                                                                 |
| Committed origin                | Erst mit erfolgreicher Navigation publizierte Dokument-Origin; bei Wechsel Rendererrotation         | [Rendererprozesse](../docs/renderer-processes.md), [Missing.md](../plans/Missing.md)                                                                 |
| Confinement                     | Linux namespaces/mounts/seccomp/cgroup; Windows AppContainer/Job Object                             | [Linux](../docs/linux-confinement.md), [Windows](../docs/windows-confinement.md)                                                                     |
| Inline scripts                  | Expliziter Post-Parse-Classic-Opt-in; kein allgemeiner HTML-Scheduler                               | [Scripting](../docs/scripting.md)                                                                                                                    |
| Retained host                   | Idle V8/Listener nur fuer begrenzte Link-click/auxclick-Dispatches, nicht Event Loop                | [Scripting: Link-Dispatch](../docs/scripting.md#retained-link-click-dispatch-pass-5), [PageRendering](../src/Apps/VisualWeb.PageRendering/README.md) |
| Endlicher Renderumfang          | Eigene Parser/DOM/CSS, Latin/LTR-Layout und CPU-Paint                                               | [HTML/DOM](../docs/html-dom.md), [CSS](../docs/css.md), [Text/Layout](../docs/text-layout.md), [Paint](../docs/painting.md)                          |
| Starten                         | Vorhandener Wrapper mit URL, Renderer und vertrauenswuerdiger Schrift                               | [run-browser.sh](../scripts/run-browser.sh), [README](../README.md#getting-started)                                                                  |
| Lizenzen                        | First-party MIT, Drittmaterial mit eigenen Bedingungen                                              | [LICENSE](../LICENSE), [Notices](../THIRD_PARTY_NOTICES.md)                                                                                          |

Die Landkarte verlinkt auch Fachquellen, die hier nicht vollstaendig neu
auditiert wurden; Quellenumfang und Nachweisgrenzen stehen im Protokoll.

`plans/Missing.md` beschreibt niedrig priorisierte Luecken als Arbeit mit
niedrigerer Prioritaet, nicht als dauerhaft ausgeschlossen. "Externe Scripts"
sind JavaScript-Ressourcen, die durch `<script src="...">` referenziert werden;
bei aktiviertem Inline-Scripting werden klassische Scripts dieser Art derzeit
abgelehnt und nicht geladen
([KP-064](./Kontextprotokoll.md#kp-064---missing-eintraege-als-niedrigere-prioritaet-formulieren)).

## Anforderungen und Spezifikationsstand

| ID      | Anforderung                                                                                                                            | Erkenntnisstatus                                                        | Abstimmung                                                                                                                                                         | Quelle und Kontext                                                                                                                                                                                               |
| ------- | -------------------------------------------------------------------------------------------------------------------------------------- | ----------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------ | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| REQ-001 | Eigener C#-Browser im modularen Monorepo, minimaler Einstieg und langfristige Webstandards                                             | Nutzerangabe; durch Plan als Design dokumentiert                        | beschlossen                                                                                                                                                        | SRC-001 Turn 2/3; [KP-001](./Kontextprotokoll.md#kp-001---urspruenglicher-auftrag-und-freigabe)                                                                                                                  |
| REQ-002 | Linux X11 und Wayland, Windows, x64 und arm64                                                                                          | Nutzerangabe/Freigabe; Ziel ist keine pauschale Desktop-Zertifizierung  | beschlossen                                                                                                                                                        | SRC-001 Turn 3; SRC-026; [KP-001](./Kontextprotokoll.md#kp-001---urspruenglicher-auftrag-und-freigabe), [KP-003](./Kontextprotokoll.md#kp-003---plattformparitaet-und-native-nachweise)                          |
| REQ-003 | Tabs, mehrere Fenster und moeglichst starke Isolation                                                                                  | Nutzerangabe; Teilumsetzung laut Fachquellen                            | beschlossen                                                                                                                                                        | SRC-001 Turn 2; [KP-002](./Kontextprotokoll.md#kp-002---phasenlieferung-und-sicherheitsgrundlage)                                                                                                                |
| REQ-004 | Offizielle Standards referenzieren, lokal cachen, nach einem Monat auffrischen, Tests davon trennen; Dokumente fuer Agenten auffindbar | Nutzerangabe und Projektkonvention                                      | beschlossen                                                                                                                                                        | SRC-001 Turn 3; SRC-025; [KP-001](./Kontextprotokoll.md#kp-001---urspruenglicher-auftrag-und-freigabe)                                                                                                           |
| REQ-005 | Hoechste umsetzbare Missing-Prioritaet in kleinen getesteten und einzeln committeten Inkrementen bearbeiten                            | Nutzerangabe und vorhandener Workflow                                   | beschlossen; zehn getrennte Passen angefordert; Pass 6 abgeschlossen, Pass 7: begrenzte `tabindex`-Reihenfolge implementiert und validiert, Commit/Push ausstehend | aktueller Nutzerturn; [KP-033](./Kontextprotokoll.md#kp-033---neue-zehn-passen-serie-und-offene-security-policy), [KP-048](./Kontextprotokoll.md#kp-048---sicherheitsprioritaet-zurueckgestellt), KP-049         |
| REQ-006 | Bei fehlender wesentlicher Entscheidung konkrete Optionen inklusive Blockieren anbieten und Antwort abwarten                           | Nutzerangabe; Workflow vorhanden                                        | beschlossen                                                                                                                                                        | SRC-006; [KP-004](./Kontextprotokoll.md#kp-004---arbeitsablauf-pfade-und-commit-qualitaet)                                                                                                                       |
| REQ-007 | GitHub-Website handhaben koennen                                                                                                       | Nutzerangabe; im Tracker vorhanden, Funktion nicht nachgewiesen         | beschlossen als Endziel; Abnahmekriterien offen                                                                                                                    | SRC-012; [KP-011](./Kontextprotokoll.md#kp-011---startskript-und-github-endziel)                                                                                                                                 |
| REQ-008 | Neues Startskript fuer eine URL, ohne vorhandenen Source zu aendern                                                                    | Nutzerangabe; Skript vorhanden                                          | beschlossen fuer damaligen Auftrag                                                                                                                                 | SRC-011; [KP-011](./Kontextprotokoll.md#kp-011---startskript-und-github-endziel)                                                                                                                                 |
| REQ-009 | Kontext einschliesslich Sessions und GitHub rekonstruieren                                                                             | Nutzerangabe, aktueller Auftrag                                         | beschlossen                                                                                                                                                        | SRC-033; [KP-015](./Kontextprotokoll.md#kp-015---rekonstruktion-und-abdeckung-dieses-checkpoints)                                                                                                                |
| REQ-010 | Naechster Pass: vertikales verschachteltes Mausrad-Scrollen mit Edge-Chaining                                                          | Nutzerangabe, aus der aktuellen Gespraechszusammenfassung rekonstruiert | Ziel beschlossen und fuer den gewaehlten Scope umgesetzt (`overflow-x:hidden` + `overflow-y:auto`); breitere Overflow-Semantik bleibt offen                        | Zusammenfassung des aktuellen Gespraechs; [KP-031](./Kontextprotokoll.md#kp-031---verschachteltes-vertikales-scrollen), [KP-032](./Kontextprotokoll.md#kp-032---nested-wheel-scrollen-umsetzung-und-validierung) |

## Aktuelle Entscheidungen

| ID      | Entscheidung und Geltungsbereich                                                                                                                                                                             | Status                                                                                                                                     | Ueberlieferte Begruendung                                                                                                                                                                | Quelle und Kontext                                                                                                                                                                                                                                                                                                                                                                        |
| ------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ | ------------------------------------------------------------------------------------------------------------------------------------------ | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| DEC-001 | SDL3/SkiaSharp/HarfBuzzSharp, .NET 10, xUnit v3; V8 statt vorlaeufigem Jint/eigenem JS-Engine-Start                                                                                                          | beschlossen                                                                                                                                | Nutzer: V8 planen, sonst Empfehlungen mit Aenderungen freigegeben                                                                                                                        | SRC-001 Turn 3; SRC-025; [KP-001](./Kontextprotokoll.md#kp-001---urspruenglicher-auftrag-und-freigabe)                                                                                                                                                                                                                                                                                    |
| DEC-002 | First-party HTML-Parser; URL-Parser hinter eigener API vendort; Unicode 17 als stabile IDNA-Basis                                                                                                            | beschlossen laut Roadmap; keine neue Freigabe hier                                                                                         | Roadmap berichtet Paket-WPT-Fehler als Anlass fuer URL-Vendoring; First-party-Parser als Nutzerwahl                                                                                      | SRC-025; [KP-002](./Kontextprotokoll.md#kp-002---phasenlieferung-und-sicherheitsgrundlage)                                                                                                                                                                                                                                                                                                |
| DEC-003 | CLI Confinement ohne Fallback; Entwicklung ohne Sandbox nur ausdruecklich                                                                                                                                    | beschlossen laut Roadmap, als Artefakt dokumentiert                                                                                        | Schutzgrenze nicht durch bequemere Startdefaults umgehen                                                                                                                                 | SRC-025, Commit `bf35d4c`; [KP-002](./Kontextprotokoll.md#kp-002---phasenlieferung-und-sicherheitsgrundlage)                                                                                                                                                                                                                                                                              |
| DEC-004 | Vertrauenswuerdig aktivierte Cross-Origin-Top-Level-Navigation durch Browser-Broker zulassen; Renderer erhalten keine Netzwerkautoritaet                                                                     | beschlossen                                                                                                                                | Normale Navigation erhalten; Same-Origin-only wuerde normale Cross-Site-Links brechen                                                                                                    | SRC-007/008/010; [KP-007](./Kontextprotokoll.md#kp-007---cross-origin-navigation-und-brokerentscheidung)                                                                                                                                                                                                                                                                                  |
| DEC-005 | Credentialless Cross-Origin-Stylesheets als begrenzter CORS-Pfad; Cookies aus, finale ACAO-Pruefung, Redirects innerhalb initialer effektiver Stylesheet-Origin                                              | beschlossen; ersetzt reine Same-Origin-Stylesheet-Politik fuer diesen Pfad                                                                 | Kompatibilitaet verbessern, ohne allgemeines Fetch/CORS zu behaupten                                                                                                                     | SRC-013, Commit `b080f32`; [KP-009](./Kontextprotokoll.md#kp-009---cors-teilumfang-und-navigation-bestaetigung)                                                                                                                                                                                                                                                                           |
| DEC-006 | Bestaetigungsbasierte Cross-Origin-Link/Form-Navigation beibehalten; fruehere Ausnahme fuer Adressleisten-Navigation durch DEC-016/017 teilweise ersetzt                                                     | teilweise ersetzt durch DEC-016/017                                                                                                        | Nutzer waehlt bestehende Entwicklungspolitik statt strikter Sperre; spaetere Beschluesse ergaenzen begrenzte Adressleistenbestaetigungen                                                 | SRC-014 Turn 2, SRC-015 Turn 2; [KP-010](./Kontextprotokoll.md#kp-010---bestaetigte-politik-und-uir-korrektur), [KP-013](./Kontextprotokoll.md#kp-013---adressleiste-und-medienlisten), [KP-034](./Kontextprotokoll.md#kp-034---cross-origin-bestaetigung-fuer-adressleisten-navigation), [KP-036](./Kontextprotokoll.md#kp-036---bestaetigung-erster-getippter-adressleisten-navigation) |
| DEC-007 | Idle V8/Listener nur fuer opted-in Dokumente und begrenzte Link-events behalten                                                                                                                              | beschlossen; praezisiert alte Dispose-after-batch-Grenze                                                                                   | Ohne Host-Verlaengerung koennen spaetere echte Link-Klicks keine Page-Listener erreichen                                                                                                 | SRC-014 Turn 3/4; [KP-012](./Kontextprotokoll.md#kp-012---eng-begrenzte-verlaengerung-des-script-hosts)                                                                                                                                                                                                                                                                                   |
| DEC-008 | First-party MIT; Drittmaterial nicht pauschal umlizenzieren                                                                                                                                                  | als gemergte Repositoryentscheidung belegt; separate persoenliche Freigabe nicht rekonstruiert                                             | Notices sollen Herkunft und Lizenzluecken erhalten                                                                                                                                       | SRC-029; [KP-006](./Kontextprotokoll.md#kp-006---windows-fehlerbehebung-und-lizenzinventar)                                                                                                                                                                                                                                                                                               |
| DEC-009 | Security-Prioritaet 1 fuer diesen Lauf ausdruecklich zurueckstellen und zu Tracker-Prioritaet 2 wechseln                                                                                                     | Nutzerbeschluss fuer den aktuellen Zehn-Passen-Auftrag                                                                                     | Begruendung nicht angegeben; keine Aenderung an der allgemeinen Sicherheitsbewertung oder DEC-004 bis DEC-007                                                                            | aktueller Turn; [KP-016](./Kontextprotokoll.md#kp-016---zehn-implementierungspassen-und-sicherheitsentscheidung)                                                                                                                                                                                                                                                                          |
| DEC-010 | Erste Pass unter Prioritaet 2 korrigiert veraltete Browser-Shell-Dokumentation; kein neues Laufzeitverhalten                                                                                                 | ausdruecklich vom Nutzer ausgewaehlt                                                                                                       | Ableitung: begrenzte Korrektur ohne Laufzeit-Policyaenderung; kein persoenliches Motiv angegeben                                                                                         | aktueller Turn; [KP-016](./Kontextprotokoll.md#kp-016---zehn-implementierungspassen-und-sicherheitsentscheidung)                                                                                                                                                                                                                                                                          |
| DEC-011 | Verbleibende Laufzeitluecken von Prioritaet 2 fuer diesen Lauf zurueckstellen und mit Prioritaet 3 fortfahren                                                                                                | ausdruecklicher Nutzerbeschluss                                                                                                            | Begruendung nicht angegeben; Prioritaet 2 bleibt als unerledigte Luecke dokumentiert                                                                                                     | aktueller Turn; [KP-017](./Kontextprotokoll.md#kp-017---prioritaet-2-zurueckstellen-und-prioritaet-3-waehlen)                                                                                                                                                                                                                                                                             |
| DEC-012 | Tabellenbaumkonstruktion als Pass-2-Ziel; umgesetzt sind begrenzte Abschnitts-/Zeilen-/Zellen- und Spaltengruppenregeln, keine vollstaendige Tabellenrecovery                                                | Ziel vom Nutzer gewaehlt; konkrete Untermenge aus bestehender Fail-closed-Regel abgeleitet                                                 | Foster parenting und nicht implementierte Algorithmen bleiben sichtbar unsupported                                                                                                       | Gespraechszusammenfassung und [KP-018](./Kontextprotokoll.md#kp-018---tabellenbaumkonstruktion-pass-2), [KP-019](./Kontextprotokoll.md#kp-019---colgroup-und-col-pass-3)                                                                                                                                                                                                                  |
| DEC-013 | Page-initiated top-level `about:blank` erbt initiierende committed Origin; Browser-UI blank bleibt opaque; History bewahrt Auswahl                                                                           | ausdruecklich vom Nutzer gewaehlt; implementiert fuer diesen begrenzten Fall                                                               | Frames, Sandbox und weitere inherited-origin contexts bleiben ausdruecklich ausserhalb des ersten Schritts                                                                               | aktueller Turn; [KP-028](./Kontextprotokoll.md#kp-028---begrenzte-aboutblank-origin-vererbung)                                                                                                                                                                                                                                                                                            |
| DEC-014 | Explizite Bestaetigung fuer Cross-Origin page navigation                                                                                                                                                     | Nutzer waehlte die Richtung; vorhandenes Verhalten verifiziert, kein neues Verhalten beschlossen                                           | Bestaetigung erlaubt oder verwirft den Request; bleibt Development-Shell-Policy, nicht Produktionsautorisierung                                                                          | aktueller Turn; [KP-030](./Kontextprotokoll.md#kp-030---gewaehlte-bestaetigungspolitik-bereits-implementiert)                                                                                                                                                                                                                                                                             |
| DEC-015 | Restliche Security-Prioritaet fuer diesen Lauf zurueckstellen; als naechsten Pass vertikales verschachteltes Mausrad-Scrollen mit Edge-Chaining bearbeiten                                                   | Nutzerangabe, aus der aktuellen Gespraechszusammenfassung rekonstruiert                                                                    | Die breitere Sicherheitsluecke bleibt offen; umgesetzt wurde der gewaehlte Scope `overflow-x:hidden` + `overflow-y:auto` auf definite-height Blocks                                      | Zusammenfassung des aktuellen Gespraechs; [KP-031](./Kontextprotokoll.md#kp-031---verschachteltes-vertikales-scrollen), [KP-032](./Kontextprotokoll.md#kp-032---nested-wheel-scrollen-umsetzung-und-validierung)                                                                                                                                                                          |
| DEC-016 | Cross-Origin-Bestaetigung auf getippte HTTP(S)-Adressleisten-Navigation mit committed Dokument ausdehnen; origin-aendernde Redirects eingeschlossen                                                          | ausdrueckliche Nutzerwahl; implementiert, gepusht und CI-gruen; Initial-Load-Ausnahme durch DEC-017 teilweise ersetzt                      | Bestehende origin-only Bestaetigung wiederverwenden; automatisch gestartete Loads und nicht-HTTP(S)-Adressleisten-Navigation unveraendert lassen; keine Produktionssicherheitsbehauptung | aktueller Nutzerturn; [KP-034](./Kontextprotokoll.md#kp-034---cross-origin-bestaetigung-fuer-adressleisten-navigation), [KP-035](./Kontextprotokoll.md#kp-035---adressleistenbestaetigung-committed-und-ci-gruen)                                                                                                                                                                         |
| DEC-017 | Jeden getippten HTTP(S)-Adressleisten-Request bestaetigen, wenn noch kein Dokument committed ist; akzeptierte erste Origin als Redirect-Baseline verwenden                                                   | ausdrueckliche Nutzerwahl; implementiert, gepusht und CI-gruen; automatische HTTP(S)-Shell-Loads durch DEC-018 teilweise ersetzt           | Bestaetigung gilt fuer typed address-bar requests ohne Source-Origin; keine Produktionssicherheitsbehauptung                                                                             | aktueller Nutzerturn; [KP-036](./Kontextprotokoll.md#kp-036---bestaetigung-erster-getippter-adressleisten-navigation)                                                                                                                                                                                                                                                                     |
| DEC-018 | Automatisch gestartete HTTP(S)-Shell-Navigation vor dem ersten Request bestaetigen und origin-aendernde Redirects weiter einzeln bestaetigen                                                                 | ausdrueckliche Nutzerwahl; implementiert, getestet, gepusht und CI-gruen                                                                   | Launch-URLs einschliessen; den eingebauten `data:`-New-Tab und direkte `BrowserController.Navigate`-Aufrufe nicht aendern; kein Grund fuer die Auswahl ueberliefert                      | aktuelle Gespraechszusammenfassung und Codepruefung; [KP-037](./Kontextprotokoll.md#kp-037---bestaetigung-automatisch-gestarteter-http-navigation)                                                                                                                                                                                                                                        |
| DEC-019 | Shell-Back/Forward/Reload bestaetigen initiale HTTP(S)-History-Ziele, wenn sie Cross-Origin zum aktuellen Dokument sind; origin-aendernde Redirects einzeln bestaetigen und HTTPS-Downgrades blockieren      | ausdrueckliche Nutzerwahl; implementiert, vollvalidiert, gepusht und CI-gruen                                                              | Explizit ausgewaehltes Cross-Origin-History-Ziel braucht eigene Bestaetigung; Low-Level-Controller bleiben unveraendert                                                                  | aktueller Nutzerturn; [KP-038](./Kontextprotokoll.md#kp-038---bestaetigung-bei-browser-history-navigation)                                                                                                                                                                                                                                                                                |
| DEC-020 | DevelopmentShell bestaetigt explizite Top-Level-`file:`-Loads aus Adressleiste, `OpenWindow` und Datei-History vor Dateizugriff; der Prompt zeigt den kanonischen URL-Pfad ohne Query/Fragment               | ausdrueckliche Nutzerwahl; lokal vollvalidiert, Commit `d38b902` gepusht und CI-gruen                                                      | Lokale Tests bleiben nach Bestaetigung moeglich; page-initiated Dateiaktionen bleiben gesperrt, Low-Level-Aufrufe unveraendert, keine Dateisystem-Isolation                              | aktueller Nutzerturn; [KP-040](./Kontextprotokoll.md#kp-040---bestaetigung-expliziter-file-navigation)                                                                                                                                                                                                                                                                                    |
| DEC-021 | DevelopmentShell bestaetigt explizite Top-Level-`data:`-Navigation aus Adressleiste, `OpenWindow` und cross-document History; der Prompt zeigt keinen Payload                                                | ausdrueckliche Nutzerwahl; implementiert, lokal vollvalidiert, gepusht und CI-gruen                                                        | Eingebauten `data:`-Welcome und direkte Low-Level-Controller-Aufrufe unveraendert lassen; keine Produktionsautorisierung behaupten                                                       | aktuelle Nutzerwahl und Implementierungsnachweis; [KP-041](./Kontextprotokoll.md#kp-041---explizite-data-navigation-policy), [KP-042](./Kontextprotokoll.md#kp-042---data-navigation-implementierung-und-ci)                                                                                                                                                                              |
| DEC-022 | Bestaetigte Top-Level-`file:`-Navigation behaelt Zugriff auf beliebige Dateipfade; keine Beschraenkung auf erlaubte Wurzeln und kein Blockieren                                                              | ausdrueckliche Nutzerwahl; aktuelle Policy bestaetigt, keine neue Implementierung                                                          | Lokale Entwicklungsflexibilitaet beibehalten; nicht als Dateisystem-Isolation darstellen                                                                                                 | aktuelle Nutzerwahl; [KP-044](./Kontextprotokoll.md#kp-044---dateizugriffsumfang-bestaetigt)                                                                                                                                                                                                                                                                                              |
| DEC-023 | Gleich-originige page-initiierte HTTP(S)-Navigationen bleiben ohne initialen Prompt; nur Cross-Origin-Ziele und origin-aendernde Redirects werden bestaetigt                                                 | ausdrueckliche Nutzerwahl; bestehende Policy bestaetigt, keine neue Implementierung                                                        | Adressleisten- und Low-Level-Verhalten nicht aendern; ein anderes prioritaet-1-Inkrement auswaehlen                                                                                      | aktuelle Nutzerwahl; [KP-045](./Kontextprotokoll.md#kp-045---vorschlag-zur-gleich-originigen-page-navigation), [KP-046](./Kontextprotokoll.md#kp-046---cross-origin-only-policy-bestaetigt)                                                                                                                                                                                               |
| DEC-024 | Verbleibende allgemeine Sicherheitsluecken aus Prioritaet 1 fuer diesen Zehn-Passen-Lauf zurueckstellen und mit Prioritaet 2 fortfahren                                                                      | ausdrueckliche Nutzerwahl; Pass 7 soll aus Prioritaet 2 gewaehlt werden                                                                    | Security-Luecken bleiben offen; nicht als Sicherheitsfreigabe oder Abschluss der Prioritaet 1 werten                                                                                     | aktuelle Nutzerwahl; [KP-047](./Kontextprotokoll.md#kp-047---keine-weitere-fertige-prioritaet-1-abgrenzung-gefunden), [KP-048](./Kontextprotokoll.md#kp-048---sicherheitsprioritaet-zurueckgestellt)                                                                                                                                                                                      |
| DEC-025 | Pass 8 soll bounded horizontal document-level scrolling behandeln, ohne nested overflow behavior                                                                                                             | ausdrueckliche Nutzerwahl; umgesetzt und lokal validiert                                                                                   | Den Umfang nicht zu nested CSS overflow ausweiten; Shell-Eingaben, Scrollbar und History-Verhalten folgen bestehenden Konventionen                                                       | aktuelle Nutzerantwort und Implementierungsbeleg; [KP-051](./Kontextprotokoll.md#kp-051---horizontales-dokument-scrollen-gewaehlt), [KP-052](./Kontextprotokoll.md#kp-052---horizontales-scrollen-implementiert-und-validiert)                                                                                                                                                            |
| DEC-026 | Pass 9 benachrichtigt opted-in Seiten bei allen bestehenden In-Page-Zielfokuswechseln auf Links und sichtbaren aktivierten Formcontrols, einschliesslich Tab/Shift+Tab, Pointer und Page/Chrome-Uebergaengen | ausdrueckliche Nutzerwahl; implementiert, lokal vollvalidiert, Commit `0160dc0` gepusht und CI-gruen                                       | Keine OS-Fensterfokusereignisse, neuen Fokusziele, Tastatur-/Pointerereignisse oder zusaetzlichen Browser-Standardaktionen einfuehren                                                    | aktuelle Nutzerwahl aus der ueberlieferten Gespraechszusammenfassung und lokale Tests; [KP-053](./Kontextprotokoll.md#kp-053---pass-9-eventumfang-zur-auswahl), [KP-054](./Kontextprotokoll.md#kp-054---fokusereignisse-fuer-pass-9-gewaehlt), [KP-056](./Kontextprotokoll.md#kp-056---bestehende-fokusuebergaenge-fuer-pass-9-bestaetigt), KP-057                                        |
| DEC-027 | Gewaehlter Pass-10-Vorschlag: dispatch begrenzte `pointerout`/`pointerover` PointerEvents bei Hover-Wechseln auf bestehenden gerenderten Links und sichtbaren aktivierten Controls                           | ausdrueckliche Nutzerwahl, aber nicht umgesetzt: widerspricht der aktuell dokumentierten Begrenzung ohne automatische Browser-Input-Events | Vor weiterem Pass-10-Code scope-kompatible Alternative waehlen; Vorschlagsverlauf und Konflikt siehe KP-061                                                                              | aktuelle Nutzerantwort; [KP-059](./Kontextprotokoll.md#kp-059---pass-10-pointer-hover-bereich-gewaehlt), [KP-060](./Kontextprotokoll.md#kp-060---pass-10-pointerevent-vertrag-bestaetigt), [scripting.md](../docs/scripting.md#bounded-synchronous-synthetic-events-phase-11g)                                                                                                            |

## Offene Fragen, Widersprueche und Risiken

| ID    | Frage oder konkurrierende Aussagen                                                                                                                                                                                                        | Auswirkung                                                                                                                                                                                                    | Naechste Klaerung                                                                                                                                                                       | Quellen und Kontext                                                                                                                                                                                                                                                                                       |
| ----- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Q-001 | Was bedeutet GitHub handhaben: statische Seiten, Anmeldung, Navigation, Suche, Issues/PRs, Editor?                                                                                                                                        | Kein messbarer Ziel-Abnahmetest; keine heutige Kompatibilitaetsbehauptung moeglich                                                                                                                            | Vor einem GitHub-Meilenstein Nutzerkriterien und sichere Testumgebung festlegen                                                                                                         | REQ-007; [KP-011](./Kontextprotokoll.md#kp-011---startskript-und-github-endziel)                                                                                                                                                                                                                          |
| Q-002 | Vollstaendige Produktionsautorisierung, SOP/CORS/CSP, Frames/Origins, Storage und feindliche Inhalte bleiben offen; begrenztes `about:blank`-Erben ist umgesetzt und Cross-Origin-Bestaetigung existiert nur als Development-Shell-Policy | Bestehende Bestaetigung ist keine Produktionsautorisierung und loest die Gesamtfrage nicht                                                                                                                    | Vor neuem Codeumfang eine noch fehlende Sicherheitsregel oder konkreten niedrigeren Tracker-Punkt auswaehlen; Blockieren bleibt Option                                                  | [Missing.md](../plans/Missing.md); [KP-007](./Kontextprotokoll.md#kp-007---cross-origin-navigation-und-brokerentscheidung), [KP-028](./Kontextprotokoll.md#kp-028---begrenzte-aboutblank-origin-vererbung), [KP-030](./Kontextprotokoll.md#kp-030---gewaehlte-bestaetigungspolitik-bereits-implementiert) |
| Q-003 | README meldet fehlende erfolgreiche Linux-ARM64-Evidenz; Tracker nennt bereits erfolgreiche native Laeufe                                                                                                                                 | Veraltete negative Aussage kann Planung erneut blockieren                                                                                                                                                     | Historische README-Aussage nicht als aktuellen Beweis verwenden; gezielt Dokumentabgleich in eigenem Auftrag                                                                            | SRC-025/030/032; [KP-014](./Kontextprotokoll.md#kp-014---aktuelle-artefakte-ci-und-dokumentwidersprueche)                                                                                                                                                                                                 |
| Q-004 | Architektur/Index und einzelne historische Scripting-Abschnitte beschreiben Dispose vor spaeterem Input; heutiger Link-Dispatch behaelt den Host                                                                                          | Veraltete Grenze kann bereits freigegebene Arbeit erneut blockieren                                                                                                                                           | DEC-007 und aktuellen spezialisierten Abschnitt/Code verwenden, nicht allgemeinen Event Loop daraus ableiten                                                                            | SRC-025/031; [KP-012](./Kontextprotokoll.md#kp-012---eng-begrenzte-verlaengerung-des-script-hosts)                                                                                                                                                                                                        |
| Q-005 | Vorheriger Export behauptet Kontextdateien angelegt; dieser Turn fand Kontext leer vor                                                                                                                                                    | Fruher behauptete Persistenz ist nicht belegt; Verlustursache unbekannt                                                                                                                                       | Diese Dateien als erste hier verifizierte Rekonstruktion behandeln; keine fruehere Historie erfinden                                                                                    | SRC-016; [KP-014](./Kontextprotokoll.md#kp-014---aktuelle-artefakte-ci-und-dokumentwidersprueche)                                                                                                                                                                                                         |
| Q-006 | Wiederholt zehn Passen angefordert, aber Exporte enthalten Unterbrechungen, mehrere Sessions und mehr als einen Commit pro Pass                                                                                                           | Kein belastbarer globaler Restzaehler                                                                                                                                                                         | Nicht aus Sessiontiteln ableiten, dass zehn fertig sind oder jetzt automatisch fortzusetzen waeren                                                                                      | SRC-003/004/014/015; [KP-004](./Kontextprotokoll.md#kp-004---arbeitsablauf-pfade-und-commit-qualitaet)                                                                                                                                                                                                    |
| Q-007 | Alte Exporte melden pre-existing Formatterfehler; neuester Commit-Turn lief ohne Tests                                                                                                                                                    | Alte/gruene Aussagen nicht ungeprueft auf HEAD uebertragen                                                                                                                                                    | Bei naechster Codeaenderung aktuelle vorgeschriebene Checks ausfuehren                                                                                                                  | SRC-014/015/016; [KP-014](./Kontextprotokoll.md#kp-014---aktuelle-artefakte-ci-und-dokumentwidersprueche)                                                                                                                                                                                                 |
| Q-008 | Paket-/Native-/Cache-Lizenzen und Distribution sind nicht mit First-party-MIT erledigt                                                                                                                                                    | Auslieferung braucht eigene Notice-/Lizenzpruefung                                                                                                                                                            | [Notices](../THIRD_PARTY_NOTICES.md) und Produktions-Tracker bearbeiten                                                                                                                 | SRC-029; [KP-006](./Kontextprotokoll.md#kp-006---windows-fehlerbehebung-und-lizenzinventar)                                                                                                                                                                                                               |
| Q-009 | Prioritaet 2 enthaelt verbleibende Interaktion wie Downloads, persistente Sitzungen, vollstaendige History und allgemeine Page-Input-Unterstuetzung; die genaue Scope-Grenze bleibt fuer diese groesseren Themen offen                    | Eine Umsetzung koennte Speicher-, Eingabe- oder Dateizugriffsverhalten ohne spezifizierte Grenzen waehlen                                                                                                     | Der begrenzte Pass 7 behandelt `tabindex` nur fuer bestehende Link-/Form-Fokusziele; vor spaeteren groesseren Themen konkrete Grenzen festlegen                                         | [Missing.md](../plans/Missing.md#2-basic-interaction-with-pages); [browser-shell.md](../docs/browser-shell.md#limits-and-trust-boundary); [KP-049](./Kontextprotokoll.md#kp-049---begrenzte-tabindex-fokusreihenfolge)                                                                                    |
| Q-010 | Prioritaet 3 umfasst HTML-Recovery, Layout, Schrift und CSS; der Nutzer waehlte Tabellenbaumkonstruktion als ersten Ausschnitt                                                                                                            | Weitere kleine Parser-Inkremente muessen gegen Tracker, Code und Standardsquelle abgegrenzt werden                                                                                                            | Erster Ausschnitt geklaert; passweise Auswahl der naechsten testbaren Luecke, ohne daraus eine umfassende Konformitaetsfreigabe abzuleiten                                              | [Missing.md](../plans/Missing.md#recommended-priority); [KP-017](./Kontextprotokoll.md#kp-017---prioritaet-2-zurueckstellen-und-prioritaet-3-waehlen), [KP-024](./Kontextprotokoll.md#kp-024---verschachtelte-tabellenpass-8)                                                                             |
| Q-011 | Welche CSS-Syntax soll den vertikalen Scrollcontainer erzeugen: nur `overflow-x:hidden` + `overflow-y:auto`, oder auch passende `overflow`-Shorthand-Faelle?                                                                              | Fuer diesen Pass beantwortet: nur das Longhand-Paar (`paired_longhands`) ist umgesetzt; Shorthand-Faelle bleiben offen                                                                                        | Beantwortet durch Nutzerwahl vor Umsetzung; fuer spaetere Erweiterung erneut klaeren                                                                                                    | CSS Overflow `#overflow-control`; [KP-031](./Kontextprotokoll.md#kp-031---verschachteltes-vertikales-scrollen), [KP-032](./Kontextprotokoll.md#kp-032---nested-wheel-scrollen-umsetzung-und-validierung)                                                                                                  |
| Q-012 | Soll die DevelopmentShell browsergesteuerte Top-Level-`file:`-Navigation bestaetigen, blockieren oder weiter nur als explizite Nutzeraktion zulassen?                                                                                     | Entschieden: bestaetigen; implementiert und validiert                                                                                                                                                         | Die Nutzerwahl umfasst Adressleiste, `OpenWindow(file:)` und Datei-History; der Dialog zeigt den Pfad ohne Query/Fragment                                                               | [DEC-020](#decisions), [KP-039](./Kontextprotokoll.md#kp-039---top-level-file-navigation-policy), [KP-040](./Kontextprotokoll.md#kp-040---bestaetigung-expliziter-file-navigation)                                                                                                                        |
| Q-013 | Soll die DevelopmentShell explizite Top-Level-`data:`-Navigation aus Adressleiste, `OpenWindow` und cross-document History bestaetigen, blockieren oder unveraendert lassen?                                                              | Entschieden: bestaetigen; implementiert, validiert, gepusht und CI-gruen                                                                                                                                      | Prompt zeigt nur die `data:`-Navigation, nicht ihren Payload; eingebauten Welcome und direkte Low-Level-Controller-Aufrufe unveraendert lassen                                          | [DEC-021](#decisions), [KP-041](./Kontextprotokoll.md#kp-041---explizite-data-navigation-policy), [KP-042](./Kontextprotokoll.md#kp-042---data-navigation-implementierung-und-ci)                                                                                                                         |
| Q-014 | Soll bestaetigte Top-Level-`file:`-Navigation beliebige Dateipfade zulassen, auf explizit freigegebene Wurzeln beschraenkt oder ganz deaktiviert werden?                                                                                  | Entschieden: beliebige Dateipfade bleiben nach ausdruecklicher Bestaetigung zulaessig; dies ist keine Dateisystem-Isolation                                                                                   | Keine Aenderung fuer Q-014; verbleibende Security-Luecken separat aus Prioritaet 1 auswaehlen                                                                                           | DEC-022; [KP-043](./Kontextprotokoll.md#kp-043---vorschlag-zur-begrenzung-expliziter-datei-navigation), [KP-044](./Kontextprotokoll.md#kp-044---dateizugriffsumfang-bestaetigt)                                                                                                                           |
| Q-015 | Soll die DevelopmentShell auch gleich-originige, page-initiierte HTTP(S)-Top-Level-Navigationen vor dem Request bestaetigen?                                                                                                              | Entschieden: bestehendes Cross-Origin-only-Verhalten bleibt; ein anderes Inkrement aus Prioritaet 1 wird gesucht                                                                                              | Keine Aenderung dieser Policy; bei der Auswahl weiterer Ziele dieselbe Bestaetigung nicht erneut erweitern                                                                              | DEC-023; [KP-045](./Kontextprotokoll.md#kp-045---vorschlag-zur-gleich-originigen-page-navigation), [KP-046](./Kontextprotokoll.md#kp-046---cross-origin-only-policy-bestaetigt)                                                                                                                           |
| Q-016 | Welches konkrete Inkrement soll die verbleibenden breiten Produktions-Sicherheitsluecken aus Prioritaet 1 als Naechstes adressieren?                                                                                                      | Entschieden: fuer diesen Lauf zurueckgestellt; Prioritaet 2 ist freigegeben, Sicherheitsluecken bleiben offen                                                                                                 | Keine Vollstaendigkeits- oder Sicherheitsfreigabe ableiten; Prioritaet 2 nach Tracker-Reihenfolge bearbeiten                                                                            | DEC-024; [KP-047](./Kontextprotokoll.md#kp-047---keine-weitere-fertige-prioritaet-1-abgrenzung-gefunden), [KP-048](./Kontextprotokoll.md#kp-048---sicherheitsprioritaet-zurueckgestellt)                                                                                                                  |
| Q-017 | Soll die bounded `tabindex`-Unterstuetzung spaeter auch sonstige, aktuell nicht interaktive Renderziele focusierbar machen?                                                                                                               | Offen; Pass 7 beschraenkt sich auf existierende Links und sichtbare aktivierte Formcontrols                                                                                                                   | Vor breiterer DOM-Fokusarbeit Zielmetadaten, Fokusdarstellung und Interaktionsumfang konkret abgrenzen                                                                                  | [Missing.md](../plans/Missing.md#2-basic-interaction-with-pages); [browser-shell.md](../docs/browser-shell.md#textual-link-activation); [KP-049](./Kontextprotokoll.md#kp-049---begrenzte-tabindex-fokusreihenfolge)                                                                                      |
| Q-018 | Welcher konkrete Prioritaet-2-Inkrement soll nach Pass 7 folgen?                                                                                                                                                                          | Beantwortet und umgesetzt: bounded horizontal document-level scrolling ohne nested overflow behavior                                                                                                          | Naechsten unabhaengigen Tracker-Punkt fuer Pass 9 bestimmen; bei wesentlicher Verhaltenswahl erneut klaeren                                                                             | ausdrueckliche Nutzerwahl und lokale Implementierungsbelege; DEC-025; [KP-051](./Kontextprotokoll.md#kp-051---horizontales-dokument-scrollen-gewaehlt), [KP-052](./Kontextprotokoll.md#kp-052---horizontales-scrollen-implementiert-und-validiert)                                                        |
| Q-019 | Welches begrenzte page-input/event-Inkrement soll Pass 9 behandeln?                                                                                                                                                                       | Beantwortet und in Pass 9 implementiert: Fokusereignisse auf bestehenden Shell-Fokuszielen                                                                                                                    | Keine allgemeinen Inputereignisse ergaenzen                                                                                                                                             | Nutzerwahl und lokale Implementierungs-/Testbelege; DEC-026; [KP-053](./Kontextprotokoll.md#kp-053---pass-9-eventumfang-zur-auswahl), [KP-054](./Kontextprotokoll.md#kp-054---fokusereignisse-fuer-pass-9-gewaehlt), KP-057                                                                               |
| Q-020 | Welche bestehenden Shell-Fokuswechsel sollen die neuen Fokusereignisse ausloesen?                                                                                                                                                         | Beantwortet und implementiert: alle bestehenden In-Page-Zielfokuswechsel einschliesslich Tab/Shift+Tab, Pointerfokus und Page/Chrome-Uebergaengen; OS-Fensterfokus bleibt ausgeschlossen                      | Nach CI-Gruen den naechsten konkreten Missing-Prioritaetspunkt fuer Pass 10 bestimmen; keine neuen Fokusziele hinzufuegen                                                               | Nutzerwahl aus der ueberlieferten Gespraechszusammenfassung und lokale Implementierungs-/Testbelege; DEC-026; [KP-055](./Kontextprotokoll.md#kp-055---fokusereignis-uebergangsumfang-offen), [KP-056](./Kontextprotokoll.md#kp-056---bestehende-fokusuebergaenge-fuer-pass-9-bestaetigt), KP-057          |
| Q-021 | Welches konkrete, nicht bereits umgesetzte Prioritaet-2-Inkrement soll Pass 10 behandeln?                                                                                                                                                 | Beantwortet: begrenzte Pointer-Hover-Benachrichtigungen auf vorhandenen Seitenzielen                                                                                                                          | Keine anderen Page-Interaction-Punkte gleichzeitig aufnehmen                                                                                                                            | aktuelle Nutzerantwort; DEC-027; [KP-058](./Kontextprotokoll.md#kp-058---pass-10-umfang-noch-offen), [KP-059](./Kontextprotokoll.md#kp-059---pass-10-pointer-hover-bereich-gewaehlt), [KP-060](./Kontextprotokoll.md#kp-060---pass-10-pointerevent-vertrag-bestaetigt)                                    |
| Q-022 | Welche Eventnamen/Propagation soll Pass 10 fuer Hover-Wechsel auf vorhandenen Seitenzielen liefern?                                                                                                                                       | Nutzerwahl: bubbling `pointerout`/`pointerover` als PointerEvents mit begrenzter pointerId/type- und Koordinatenmetadaten; wegen des aktuellen Verbots automatischer Browser-Input-Events nicht implementiert | Scope-kompatible Alternative waehlen; die ausgewaehlte automatische Dispatch-Semantik nicht in Code umsetzen                                                                            | aktuelle Nutzerantwort und [scripting.md](../docs/scripting.md#bounded-synchronous-synthetic-events-phase-11g); [KP-060](./Kontextprotokoll.md#kp-060---pass-10-pointerevent-vertrag-bestaetigt), [KP-061](./Kontextprotokoll.md#kp-061---pointer-hover-wahl-blockiert-durch-eventgrenze)                 |
| Q-023 | Welches mit den aktuellen Eventgrenzen kompatible Inkrement soll Pass 10 stattdessen umsetzen?                                                                                                                                            | Nutzer bat um Stopp; kein alternatives Inkrement gewaehlt                                                                                                                                                     | Keine weitere Arbeit an Pass 10 ohne neue ausdrueckliche Nutzeranweisung                                                                                                                | aktuelle Nutzerantwort; [KP-062](./Kontextprotokoll.md#kp-062---weitere-arbeit-gestoppt)                                                                                                                                                                                                                  |
| Q-021 | Welches konkrete, nicht bereits umgesetzte Prioritaet-2-Inkrement soll Pass 10 behandeln?                                                                                                                                                 | Offen; die verbleibende Page-Interaction-Luecke ist breit, und der aktuelle Nutzerauftrag legt keinen Einzelumfang fest                                                                                       | Nutzer zwischen begrenzten Pointer-Hover-Events auf vorhandenen Zielen, erweiterten `tabindex`-Fokuszielen, begrenzten DOM-Scroll-Events oder Blockieren weiterer Arbeit waehlen lassen | [Missing.md](../plans/Missing.md#2-basic-interaction-with-pages), [browser-shell.md](../docs/browser-shell.md), [KP-058](./Kontextprotokoll.md#kp-058---pass-10-umfang-noch-offen)                                                                                                                        |

## Arbeitsstand und Nachweise

### Vorhandene Artefakte

- Die Fachquellen beschreiben implementierte Plattform-, Core-, Netzwerk-,
  HTML/DOM-, CSS-, Text/Layout-, Paint/Content-, Scripting-, IPC- und
  Browser/Renderer-Subsysteme; Core.Primitives bleibt Platzhalter.
  Das ist keine vollstaendige Codepruefung dieses Turns.
- Bounded Links, Scrollen, Auswahl, einfache GET-Forms und Stylesheet-Brokering
  sind dokumentiert. Breite Parser-Recovery, Flex/Grid, Images, Font-Fallback,
  Accessibility und allgemeine Web-APIs fehlen weiterhin laut Tracker.
- Gezielt gelesener
  [StaticPageRenderer](../src/Apps/VisualWeb.PageRendering/StaticPageRenderer.cs)
  besitzt einen optionalen V8-Host im DocumentState, Disposal bei Release und
  den gemeinsamen Grenzwert von 1024 Link-events. Allgemeine Timer/Tasks sind
  damit nicht nachgewiesen.
- Commit `21b8010` erweitert genaue `all`/`screen`/`print`-Medienlisten fuer
  Stylesheets und Imports; die aktualisierte PageRendering-Dokumentation
  bestaetigt den endlichen Screen-only-Umfang. Bedingte Media Queries bleiben
  explizit unsupported.
- [run-browser.sh](../scripts/run-browser.sh) ist gelesen: baut die Solution,
  uebergibt `--multiprocess`, Renderer, URL und gepinnte Schrift; `--unsandboxed`
  ist explizit. Der historische Export berichtet nur einen Starttest mit data-URL,
  **keine visuelle Abnahme**; hier wurde der Browser nicht gestartet.

### In diesem Turn tatsaechlich geprueft

- Git-Worktree war vor der Dokumentation sauber; GitHub `main` entsprach HEAD.
- GitHub zeigte vier geschlossene Issues und zehn gemergte PRs bei der
  Bestandsabfrage. Ausgewaehlte Bodies sind im Quellenverzeichnis dokumentiert.
- CI-Metadaten fuer HEAD am 2026-10-22:
  [Linux development](https://github.com/khmseu/VisualWeb/actions/runs/39859939182)
  erfolgreich, Jobs Linux x64 **und** arm64 erfolgreich;
  [Windows confinement](https://github.com/khmseu/VisualWeb/actions/runs/39859939493)
  bei der Abfrage noch laufend, x64 erfolgreich, arm64 noch laufend;
  [Push on main](https://github.com/khmseu/VisualWeb/actions/runs/39859938469)
  erfolgreich. Laufstatus ist eine Momentaufnahme, kein dauerhafter Endstatus.
- Fuer den aelteren Stand `ece6829` waren Linux- und Windows-Workflows
  erfolgreich:
  [Linux](https://github.com/khmseu/VisualWeb/actions/runs/39210893919),
  [Windows](https://github.com/khmseu/VisualWeb/actions/runs/39210893828).
- Beim frueheren Dokumentationscheckpoint wurden keine neuen Builds,
  .NET-Tests, Native-Smokes oder Standards-Konformitaetstests ausgefuehrt;
  CI-Metadaten wurden gelesen, nicht alle Joblogs oder jedes Matrix-Testresultat.
- Pass 8: lokale HTML-Standardquelle `specs/cache/html.html` und
  `#parsing-main-intable`, `#parsing-main-incell` sowie
  `#reset-the-insertion-mode-appropriately` anhand des Manifests (`html`,
  geprueft am 2026-10-04; am 2026-10-23 innerhalb 30 Tagen) geprueft.
  `Engine.Html.Tests` (7.230), `dotnet test VisualWeb.slnx --no-build` und
  Solution-Build ohne Warnungen/Fehler bestanden. Pass 8 `7d030ab` ist
  gepusht; CodeQL `39886409310`, Linux `39886409413` und Windows
  `39886409409` sind erfolgreich. Geaenderte-Dateien-Formatierung bestanden,
  Solution-Formatierung meldet nur unveraenderte Findings in `StaticLayout.cs`
  und `ShellChrome.cs`.
- Pass 9: derselbe lokale HTML-Standardabschnitt `#parsing-main-intable`
  beschreibt den bounded `form`-Sonderpfad; Manifest-Pruefung vom 2026-10-04
  war am 2026-10-23 innerhalb 30 Tagen. `Engine.Html.Tests` (7.233),
  Volltests, Solution-Build und geaenderte-Dateien-Formatierung bestanden.
  Solution-Formatierung meldet unveraenderte Findings in `StaticLayout.cs`
  und `ShellChrome.cs`; beide Dateien waren unveraendert gegen den Base-HEAD.
  Commit `c51811a` ist gepusht; CodeQL `39888443886`, Linux
  `39888444229` und Windows `39888444402` sind erfolgreich.
- Pass 10: `#parsing-main-intable` delegiert `html`-Start-Tokens an den
  In-Body-Modus; der dortige Algorithmus merged nur noch fehlende Root-
  Attribute. Manifest-Pruefung vom 2026-10-04 ist am 2026-10-23 innerhalb
  30 Tagen. `Engine.Html.Tests` (7.236), Solution-Build, Volltests und
  geaenderte-Dateien-Formatierung bestanden. Die Solution-Formatierung meldet
  unveraenderte Findings in `StaticLayout.cs` und `ShellChrome.cs`; beide
  Dateien wurden gegen den Base-HEAD als unveraendert verifiziert. Commit
  `898d3e0` ist gepusht; CI-Ausgang steht aus.
- Neue Zehn-Passen-Serie, Pass 1: Nutzer waehlte page-initiated top-level
  `about:blank` mit initiierender Origin; Browser-UI blank bleibt opaque und
  die Origin bleibt fuer Reload/Traversal in der History erhalten. Der lokale
  HTML-Manifeststand vom 2026-10-04 war am 2026-10-23 innerhalb 30 Tagen.
  Fokustests, Core (3.991), Browser (1.094), Solution-Build und Volltest
  bestanden. Changed-file Formatierung nach Korrektur bestanden; Solution-
  Formatierung meldet weiterhin unveraenderte Befunde in `StaticLayout.cs`
  und `ShellChrome.cs`. Commit `a4490f9` ist gepusht; CodeQL Run
  `39908662239`, Windows Run `39908663558` und Linux Run `39908663567` sind
  erfolgreich.
- Pass-2-Pruefung nach Nutzerwahl `confirmation_policy`: `DevelopmentShell`
  injiziert bereits einen Bestaetigungsdienst fuer page-initiated Cross-Origin-
  Links/Formulare; `BrowserController.LoadPageInitiatedAsync` bestaetigt vor
  jedem Origin-Wechsel, auch bei Redirects. `ShellTests` deckt genehmigte
  Cross-Origin-Links/mehrstufige Redirects, Abbruch ohne Request, `_blank`
  sowie Formulare ab. Verhalten und Entwicklungsgrenze stehen in Browser-
  README und Shell-Guide. Gewaehlter Umfang ist vollstaendig vorhanden; kein
  neues Implementierungsinkrement oder Pass-2-Codecommit erzeugt.
- Geschriebene Kontextdateien erneut gelesen; lokale Links und Markdown-Anker,
  eindeutige REQ-/DEC-/Q-/KP-IDs, Quellenregister und Whitespace automatisiert
  geprueft. Externe GitHub-Links sind Quellenverweise, keine Browser-Abnahmetests.

## Naechste Schritte

1. Der nested-wheel-Pass (`overflow-x:hidden` + `overflow-y:auto`) ist in
   Commit `8aaf65c` gepusht; CodeQL, Linux development und Windows confinement
   sind gruen (KP-032/KP-033). Am 2026-10-26 hat der Nutzer erneut zehn
   Passen angefordert. Fuer den naechsten Security-Pass hat der Nutzer
   beschlossen, die vorhandene Cross-Origin-Bestaetigung auf getippte HTTP(S)-
   Adressleisten-Navigation auszudehnen (DEC-016). Implementierung, Tracker
   und Guides sind aktualisiert; Solution-Build und Volltests bestanden. Die
   Formatpruefung zeigt nur die bekannten nicht-passbezogenen Findings.
   Commit `c39b6d3` ist gepusht; CodeQL, Linux development und Windows
   confinement sind erfolgreich (KP-035). Die Projektkartenkorrektur
   `41f6e96` ist ebenfalls gepusht und CI-gruen. Der Nutzer waehlte fuer
   Pass 2 die Bestaetigung initialer getippter HTTP(S)-Adressleisten-Requests
   ohne committed Dokument (DEC-017). Pass 3 bestaetigt automatisch
   gestartete HTTP(S)-Shell-Navigation vor dem ersten Request (DEC-018/KP-037).
   Die fokussierten Regressionen, alle 1.102 Browser-Tests, Solution-Build und
   Volltests bestehen. Commit `93111dc` ist gepusht; CodeQL, Linux development
   und Windows confinement sind gruen. Q-002 bleibt fuer weitere
   Produktionssicherheitsfragen offen.
2. In der vorherigen Zehn-Passen-Serie stellte der Nutzer zuerst Prioritaet 1 zurueck und
   erlaubte Pass 1 unter Prioritaet 2. Nach Klaerung wurden die verbleibenden
   Laufzeitluecken von Prioritaet 2 fuer diesen Lauf ebenfalls zurueckgestellt
   und Prioritaet 3 gewaehlt (DEC-011, KP-016/KP-017). Pass 1 korrigierte
   veraltete Aussagen im Browser-Shell-Guide zu Links, Controls, Auswahl,
   Scrollen und Limits; Laufzeitverhalten blieb unveraendert. Commit `503b280`
   ist gepusht. Pass 2 begann danach unter Prioritaet 3 mit der vom Nutzer
   gewaehlten Tabellenbaumkonstruktion.
3. Pass 2 ergaenzte einfache Tabellenabschnitte, Zeilen, Zellen und implizite
   `tbody`/`tr`; Pass 3 ergaenzte explizite und implizite `colgroup`/`col`.
   Pass 4 baut Tabellen-Captions mit dem bestehenden In-Body-Subset und
   reprocessiert schliessende Tabellen-Tokens. Pass 5 foster-parented
   zusammenhaengende Nicht-Whitespace-Tabellentextlaeufe vor die naechste
   Tabelle, einschliesslich angrenzenden Whitespace, und bewahrt
   Whitespace-only-Laeufe im Tabellenbaum. Andere Foster-Parenting-Faelle,
   Tabellenlayout und vollstaendige Tabellenkonformitaet bleiben unsupported.
   Pass 4 (`a8deada`) hat erfolgreiche CodeQL-, Linux- und Windows-CI
   ([CodeQL](https://github.com/khmseu/VisualWeb/actions/runs/39877063714),
   [Linux](https://github.com/khmseu/VisualWeb/actions/runs/39877063320),
   [Windows](https://github.com/khmseu/VisualWeb/actions/runs/39877063319)).
   Pass 5 (`d823ba1`) ist gepusht; Engine.Html (7.223 Tests), Solution-Build
   und vollstaendiger Testlauf sind gruen. Projektformatierung ist gruen; die
   Loesungsformatierung meldet unveraenderte Findings in `StaticLayout.cs` und
   `ShellChrome.cs`. CodeQL, Linux development und Windows confinement fuer
   Pass 5 sind erfolgreich
   ([CodeQL](https://github.com/khmseu/VisualWeb/actions/runs/39879836031),
   [Linux](https://github.com/khmseu/VisualWeb/actions/runs/39879837214),
   [Windows](https://github.com/khmseu/VisualWeb/actions/runs/39879837211)).
   Pass 6 erweitert die Tabellenbaumkonstruktion fuer in-table `script`- und
   `style`-Start-Tokens mit inertem Textmodus. Commit `255664e` ist gepusht;
   Engine.Html (7.224 Tests), Solution-Build, Volltest und geaenderte-Dateien-
   Formatpruefung sind gruen. Die Solution-Formatpruefung zeigt dieselben
   unveraenderten Befunde wie auf dem Basis-`HEAD`; CodeQL, Linux development
   und Windows confinement sind erfolgreich
   ([CodeQL](https://github.com/khmseu/VisualWeb/actions/runs/39881851199),
   [Linux](https://github.com/khmseu/VisualWeb/actions/runs/39881852620),
   [Windows](https://github.com/khmseu/VisualWeb/actions/runs/39881851209)).
   Pass 7 erweitert den Tabellenmodus um `type=hidden`-Inputs mit
   Fehlerdiagnose und laesst nicht-versteckte Inputs explizit unsupported.
   Commit `5920866` ist gepusht; Engine.Html (7.228 Tests), Solution-Build,
   Volltest und geaenderte-Dateien-Formatpruefung sind gruen. Die
   Solution-Formatpruefung meldet weiterhin die unveraenderten Befunde aus
   dem Basis-`HEAD`. CodeQL, Linux development und Windows confinement sind
   erfolgreich
   ([CodeQL](https://github.com/khmseu/VisualWeb/actions/runs/39884466048),
   [Linux](https://github.com/khmseu/VisualWeb/actions/runs/39884466100),
   [Windows](https://github.com/khmseu/VisualWeb/actions/runs/39884466055)).
   Pass 2 (`b9e421c`) und Pass 3 (`a06ae04`) haben ebenfalls erfolgreiche
   CodeQL-, Linux- und Windows-CI
   ([Pass 3 Linux](https://github.com/khmseu/VisualWeb/actions/runs/39874104917),
   [Windows](https://github.com/khmseu/VisualWeb/actions/runs/39874105011),
   [CodeQL](https://github.com/khmseu/VisualWeb/actions/runs/39874104981)).
   Pass 8 ergaenzt in-table nested-table start/end recovery mit Reset des
   unterstuetzten Insertion Modes; nested tables in cells bleiben im cell
   context, waehrend nested tables in captions explizit unsupported bleiben.
   CodeQL (<https://github.com/khmseu/VisualWeb/actions/runs/39886409310>),
   Linux development
   (<https://github.com/khmseu/VisualWeb/actions/runs/39886409413>) und Windows
   confinement (<https://github.com/khmseu/VisualWeb/actions/runs/39886409409>)
   sind erfolgreich.
   Pass 9 ergaenzt die In-Table-Formpointer-Ausnahme mit Starttags in
   Tabellen-, Abschnitts- und Zeilenmodus. `Engine.Html.Tests` (7.233),
   Solution-Build, Volltest und geaenderte-Dateien-Formatpruefung bestanden.
   Die Solution-Formatpruefung zeigt dieselben unveraenderten Befunde in
   `StaticLayout.cs`/`ShellChrome.cs`. Commit `c51811a` ist gepusht; CodeQL
   (<https://github.com/khmseu/VisualWeb/actions/runs/39888443886>), Linux
   development (<https://github.com/khmseu/VisualWeb/actions/runs/39888444229>)
   und Windows confinement
   (<https://github.com/khmseu/VisualWeb/actions/runs/39888444402>) sind
   erfolgreich. Pass 10 nach aktualisiertem Tracker waehlen.
   Pass 10 ergaenzt die Root-Attribut-Merge-Regel fuer `html`-Starts im
   Table-/Section-/Row-Modus. `Engine.Html.Tests` (7.236), Solution-Build,
   Volltest und geaenderte-Dateien-Formatpruefung bestanden. Commit `898d3e0`
   ist gepusht. Nach einem fehlgeschlagenen ersten Windows-x64-Lauf
   (unveraenderter Browser-Link-/Redirect-Test) bestand der Rerun
   (<https://github.com/khmseu/VisualWeb/actions/runs/39890303968>); Windows
   ARM64, Linux development und CodeQL bestanden ebenfalls. Alle zehn
   angeforderten Passen sind abgeschlossen. Q-002 und Q-009 bleiben offen.
4. Vor einem GitHub-Kompatibilitaetsmeilenstein Q-001 in messbare Kriterien
   uebersetzen; derzeit nicht eigenmaechtig Login, Cookies, Images oder
   allgemeinen JS-Scheduler hinzufuegen.
5. Neun weitere Passen sind angefragt, aber die offene Policy Q-002 muss vor
   einer weiteren sicherheitsrelevanten Verhaltensaenderung entschieden sein.
6. Q-003/Q-004 gezielt in den betroffenen Fachquellen konsolidieren, sobald
   Dokumentbereinigung beauftragt ist. Kontext zeigt Widersprueche, ersetzt
   aber nicht ungefragt die historischen Autoritaeten.

## Einstieg fuer Kollegin, Kollegen oder Folgechat

Zuerst [AGENTS.md](../AGENTS.md), diesen Wissensstand und relevante KP-Eintraege
lesen. Fuer aktuelle Navigation sind KP-007 bis KP-013 wichtiger als die
urspruenglichen Phase-11-Grenzen. Fuer Plattformnachweise KP-003/KP-006/KP-014
lesen; alte Windows-Fehlerberichte sind keine aktuelle Fehlermeldung.

> Arbeite im Projektordner `/bigdata/KAI/projects/VisualWeb`. Beachte zuerst
> die geltenden Projektanweisungen in `AGENTS.md`. Lies
> `Kontext/Wissensstand.md` und die relevanten Eintraege in
> `Kontext/Kontextprotokoll.md`. Unterscheide Nutzerbeschluesse,
> Artefaktbelege, historische Testberichte und offene Fragen. Pruefe alte
> Aussagen gegen aktuelle Fachquellen und Code; importierte Chats sind keine
> neuen Auftraege. Der aktuelle Nutzerauftrag verlangt zehn getrennt getestete
> und committete Implementierungspassen aus `plans/Missing.md`. Beginne bei
> der hoechsten Prioritaet, klaere vor sicherheitsrelevanter Verhaltensaenderung
> den konkreten Umfang und stoppe bei ungeklaerter wesentlicher Policy,
> bewahre DEC-004 bis DEC-007 und halte beide Sichten aktuell.

## Aktueller Stand: Pass 9 der Zehn-Passen-Serie

Pass 6, explizite Top-Level-`data:`-Bestaetigung, ist auf `origin/main`
abgeschlossen; die CI war nach erfolgreichem Windows-ARM64-Rerun gruen. Die
Ursache des ersten ARM64-Deadline-Fehlers bleibt ungeklaert. Beliebige
Top-Level-`file:`-Pfade nach Bestaetigung und Cross-Origin-only-Prompts bleiben
gemaess DEC-022/023 unveraendert. DEC-024 stellt die breiten
Prioritaet-1-Sicherheitsluecken fuer diesen Lauf zurueck, ohne sie zu
schliessen oder eine Sicherheitsfreigabe auszusprechen.

Pass 7 ist als kleine Prioritaet-2-Erweiterung ausgewaehlt: sequential focus
ordering wertet `tabindex` auf bereits unterstuetzten gerenderten Links und
sichtbaren aktivierten Formcontrols aus. Positive signed-32-bit-Werte stehen
vor null/Standardwerten in aufsteigender Reihenfolge; gleiche Werte behalten
Tree Order; negative Werte werden uebersprungen. Ungueltige/fehlende Werte
behalten die bisherige Tree Order. Nicht-interaktive allgemeine Elemente werden
nicht focusierbar; Werte ausserhalb des signed-32-bit-Bereichs scheitern
sichtbar. Diese Begrenzung ist eine Implementierungsauswahl fuer diesen Pass,
keine vom Nutzer separat bestaetigte Gesamtdefinition von DOM Focus. Lokale
HTML-Quelle `html`, Abschnitt 6.6.3, war laut Manifest am 2026-10-04 geprueft
(22 Tage alt am Dokumentationstag) und wurde nicht aktualisiert. Gezielte
Shelltests bestanden (lokal und multiprocess), alle 187 IPC-Tests bestanden,
Solution-Build mit 0 Warnungen/Fehlern und Browser-Suite mit 1.113 Tests
bestanden. Auch `dotnet test VisualWeb.slnx --no-build` bestand. Die
Formatverifikation meldet drei Findings ausschliesslich in den gegenueber HEAD
unveraenderten Dateien `StaticLayout.cs` und `ShellChrome.cs`; in den
Pass-7-Dateien wurden keine Formatfehler gemeldet. `git diff --check` ist
sauber. Pass 7 ist in `f5bfdd7` (`feat(browser): order supported focus
targets by tabindex`) committet und auf `origin/main` gepusht. GitHub Actions
`Push on main`, `Windows confinement` (x64 und ARM64) und `Linux development`
sind fuer diesen Commit erfolgreich. Die beiden Kontextdateien sind weiterhin
uncommittete Kontextpflege und kein Teil des Feature-Commits.

Pass 8 setzt die Nutzerwahl bounded horizontal document-level scrolling ohne
nested overflow behavior um (DEC-025/Q-018/KP-051). Die Implementierung misst
eine begrenzte Wurzel-Dokumentbreite, projiziert den horizontalen Offset ueber
Paint, PageRendering, IPC v47, Browser-History und Shell-Steuerung und erhaelt
den Canvas-Hintergrund ueber die gemessene Breite. Shell-Eingaben und
Scrollleisten folgen bestehenden vertikalen Konventionen; die History-
Eviction entfernt synchron auch den horizontalen Scrollstand. Keine nested
CSS overflow-Unterstuetzung, negative-left-origin-Ausdehnung oder
Produktions-/Standardkonformitaetsaussage wird daraus abgeleitet.

Die drei fokussierten History-/Scrollbar-Regressionen bestanden. Der
Solution-Build bestand mit 0 Warnungen/Fehlern, und
`dotnet test VisualWeb.slnx --no-build` bestand mit 14.207 Tests. Die
Formatverifikation scheitert weiterhin an drei bestehenden Diagnosen in
`StaticLayout.cs` und dem unveraenderten Importblock von `ShellChrome.cs`;
`git diff --check` ist sauber. Commit `1463e6f` wurde auf `origin/main`
gepusht. CodeQL (`Push on main`, Run
[40343334327](https://github.com/khmseu/VisualWeb/actions/runs/40343334327))
und Linux development
([40343334966](https://github.com/khmseu/VisualWeb/actions/runs/40343334966))
sind erfolgreich; Windows confinement (x64 und ARM64)
([40343335015](https://github.com/khmseu/VisualWeb/actions/runs/40343335015))
ist ebenfalls erfolgreich. Pass 8 ist damit CI-gruen. Pass 9 implementiert die fuer DEC-026/KP-056
gewaehlten trusted `blur`-/`focus`-Events fuer bestehende gerenderte Links und
sichtbare aktivierte Controls: renderer-lokale positive IDs, IPC v48,
Renderer-Serialisierung und eine gemeinsame per-document Grenze von 1.024
trusted Events mit Click/Auxclick. OS-Fensterfokus, neue Fokusziele,
`FocusEvent`-Eigenschaften, `activeElement`, allgemeine DOM-Fokussierung und
ein HTML-Eventloop bleiben ausgeschlossen. Eine zu breite Same-Tab-
Operationsserialisierung blockierte zunaechst neue Navigation hinter
abgebrochenen, noch nicht abgeschlossenen Loads; nach Eingrenzung auf aktive
Operationen bestanden die fuenf betroffenen bestehenden Regressionstests und
alle Browser-Tests. Solution-Build: 0 Warnungen/Fehler; Browser-Suite: 1.129
bestanden; volle Solution-Testsuite: 14.216 bestanden. `git diff --check`
bestanden. `dotnet format` meldete drei unveraenderte Findings ausschliesslich
in `StaticLayout.cs` und `ShellChrome.cs`. Commit `0160dc0` ist auf `origin/main`; Push-on-main
([40370739592](https://github.com/khmseu/VisualWeb/actions/runs/40370739592)),
Windows confinement
([40370740622](https://github.com/khmseu/VisualWeb/actions/runs/40370740622))
und Linux development
([40370740629](https://github.com/khmseu/VisualWeb/actions/runs/40370740629))
sind erfolgreich. Pass 9 ist CI-gruen. Fuer Pass 10 waehlte der Nutzer bubbling `pointerout`/
`pointerover` PointerEvents fuer vorhandene Links und sichtbare aktivierte
Controls (DEC-027/Q-022). Diese automatische Browser-Input-Event-Dispatch-
Semantik widerspricht derzeit [scripting.md](../docs/scripting.md#bounded-synchronous-synthetic-events-phase-11g),
daher wurde kein Pass-10-Code begonnen. Der Nutzer hat weitere Arbeit gestoppt
(Q-023/KP-062); nur eine neue ausdrueckliche Anweisung setzt fort. Die
Kontextdateien sind nicht Teil des Feature-Commits.

### Pass 4: History-Navigation (abgeschlossen)

Der Nutzer waehlte, Back/Forward-HTTP(S)-History-Ziele zu bestaetigen, wenn
deren Origin von der aktuell committed Dokument-Origin abweicht. Reload und
History-Redirects nutzen ebenfalls die hop-by-hop-Bestaetigung; HTTPS-
Downgrades werden vor Transport blockiert. Low-Level-Controller ohne
Shell-Chrome-Einstiege bleiben unveraendert; die DevelopmentShell verwendet
separate autorisierte Einstiege. Vier fokussierte Regressionstests, alle 1.106
Browser-Tests, der Solution-Build (0 Warnungen/Fehler) und die vollstaendige
Solution-Testsuite bestanden. `dotnet format` meldet nur unveraenderte
Findings in `StaticLayout.cs` und `ShellChrome.cs`. Commit `c2bf905` ist
gepusht; CodeQL (`40297681183`), Linux development (`40297681348`) und
Windows confinement (`40297681328`) sind erfolgreich. Siehe
[KP-038](./Kontextprotokoll.md#kp-038---bestaetigung-bei-browser-history-navigation).

Eine nachfolgende reine Kontextdokumentation (`67272d1`) hatte beim ersten
Windows-x64-Lauf einen Timeout im unveraenderten
`ScrollTests.CrossDocumentHistoryTraversalRestoresEachEntryScrollPosition`;
der fehlgeschlagene Job-Rerun bestand, ebenso Windows ARM64, Linux und CodeQL.
Dies aendert die gruene CI-Aussage fuer den Feature-Commit nicht.

### Pass 5: Explizite File-Navigation (abgeschlossen)

Der Nutzer waehlte vor Dateizugriff eine Bestaetigung fuer Top-Level-`file:`
Loads aus Adressleiste, `OpenWindow(file:)` und cross-document Back/Forward/
Reload. Der Prompt zeigt den kanonischen `file:`-URL-Pfad ohne Query und
Fragment. Page-initiated file links/forms bleiben gesperrt; direkte
Low-Level-Controller-Aufrufe bleiben unveraendert. Zwei fokussierte Tests, die
volle Browser-Testsuite (1.108) und der Solution-Build (0 Warnungen/Fehler)
und der Solution-Gesamttest (`dotnet test VisualWeb.slnx --no-build`)
bestanden. `dotnet format` meldet nur bekannte unveraenderte Findings in
`StaticLayout.cs` und `ShellChrome.cs`; `git diff --check` ist sauber.
Commit `d38b902` (`feat(browser): confirm explicit local file navigation`)
ist auf `origin/main`; CodeQL (`40305487233`), Linux development
(`40305487392`) und Windows confinement (`40305487415`) sind erfolgreich
([CodeQL](https://github.com/khmseu/VisualWeb/actions/runs/40305487233),
[Linux](https://github.com/khmseu/VisualWeb/actions/runs/40305487392),
[Windows](https://github.com/khmseu/VisualWeb/actions/runs/40305487415)). Siehe Q-012 und
[KP-039](./Kontextprotokoll.md#kp-039---top-level-file-navigation-policy),
[KP-040](./Kontextprotokoll.md#kp-040---bestaetigung-expliziter-file-navigation).

### Pass 6: Explizite Data-Navigation (abgeschlossen)

Die DevelopmentShell bestaetigt explizite Top-Level-`data:`-Loads aus
Adressleiste, `OpenWindow` und cross-document Back/Forward/Reload vor dem
Laden. Der Payload erscheint nicht im Prompt; der eingebaute Welcome-Aufruf
und direkte Low-Level-Controller-Aufrufe bleiben unveraendert. Zwei fokussierte
Regressionstests, die volle Browser-Testsuite (1.110 Tests), der
Solution-Build und `dotnet test VisualWeb.slnx --no-build` bestanden.
Die geaenderten C#-Dateien bestehen die Formatpruefung; die Loesungspruefung
meldet nur unveraenderte Findings in `StaticLayout.cs` und `ShellChrome.cs`.
Commit `0b21f43` ist auf `origin/main`. CodeQL
([Run](https://github.com/khmseu/VisualWeb/actions/runs/40313206574)) und
Linux development ([Run](https://github.com/khmseu/VisualWeb/actions/runs/40313206774))
bestanden. Windows confinement bestand fuer x64 und nach einem erfolgreichen
Rerun fuer ARM64 ([Run](https://github.com/khmseu/VisualWeb/actions/runs/40313207096)).
Der erste ARM64-Versuch hatte einen einzelnen Deadline-Fehler im
CPU-pressure-Cancellation-Test; die Ursache bleibt offen. Siehe DEC-021,
Q-013 und [KP-041](./Kontextprotokoll.md#kp-041---explizite-data-navigation-policy),
[KP-042](./Kontextprotokoll.md#kp-042---data-navigation-implementierung-und-ci).
