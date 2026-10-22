# Wissensstand

## Pflege und Abdeckung

- Projekt: VisualWeb, Repository `khmseu/VisualWeb`.
- Bestaetigter Projektordner: `/bigdata/KAI/projects/VisualWeb`.
- Dokumentationsdatum: 2026-10-22, Zeitzone Europe/Berlin (UTC+02:00).
- Rekonstruktionsstand: teilweise; Originalauftrag ab 2026-10-04,
  ausgewaehlte Entscheidungen bis 2026-10-22 nachvollzogen.
- Artefaktstand: lokales HEAD und GitHub `main`
  `15e18d5d21f8b5c59594a1670f5dcd91246247c6`.
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

| Thema                           | Bedeutung und Einstieg                                                                      | Autoritative Quelle                                                                                                                                  |
| ------------------------------- | ------------------------------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------- |
| Projektgrenzen und Konventionen | Aktuelle Arbeitsregeln; Projektpfade und Validierung                                        | [AGENTS.md](../AGENTS.md)                                                                                                                            |
| Subsysteme                      | Besitzer und Abhaengigkeitsrichtung                                                         | [Projektkarte](../docs/project-map.md), [Architektur](../docs/architecture/overview.md)                                                              |
| Historische Phasen              | Freigegebene Meilensteine, nicht aktuelle Faehigkeitsmatrix                                 | [Roadmap](../plans/browser-foundation-plan.md), [ADR 0001](../docs/adr/0001-browser-foundation.md)                                                   |
| Naechste Arbeit                 | Narrative Prioritaeten und explizite Restluecken                                            | [Missing.md](../plans/Missing.md#recommended-priority)                                                                                               |
| Standards und Fixtures          | Cache ist Referenzbestand; gepinnte Testdaten bleiben separat                               | [Standards](../docs/standards.md), [Manifest](../specs/manifest.json)                                                                                |
| Browser-Broker                  | Privilegierte Navigation und Stylesheet-Requests, nicht rendererseitiges Fetch              | [Shell](../docs/browser-shell.md), [Netzwerk](../docs/networking.md)                                                                                 |
| Committed origin                | Erst mit erfolgreicher Navigation publizierte Dokument-Origin; bei Wechsel Rendererrotation | [Rendererprozesse](../docs/renderer-processes.md), [Missing.md](../plans/Missing.md)                                                                 |
| Confinement                     | Linux namespaces/mounts/seccomp/cgroup; Windows AppContainer/Job Object                     | [Linux](../docs/linux-confinement.md), [Windows](../docs/windows-confinement.md)                                                                     |
| Inline scripts                  | Expliziter Post-Parse-Classic-Opt-in; kein allgemeiner HTML-Scheduler                       | [Scripting](../docs/scripting.md)                                                                                                                    |
| Retained host                   | Idle V8/Listener nur fuer begrenzte Link-click/auxclick-Dispatches, nicht Event Loop        | [Scripting: Link-Dispatch](../docs/scripting.md#retained-link-click-dispatch-pass-5), [PageRendering](../src/Apps/VisualWeb.PageRendering/README.md) |
| Endlicher Renderumfang          | Eigene Parser/DOM/CSS, Latin/LTR-Layout und CPU-Paint                                       | [HTML/DOM](../docs/html-dom.md), [CSS](../docs/css.md), [Text/Layout](../docs/text-layout.md), [Paint](../docs/painting.md)                          |
| Starten                         | Vorhandener Wrapper mit URL, Renderer und vertrauenswuerdiger Schrift                       | [run-browser.sh](../scripts/run-browser.sh), [README](../README.md#getting-started)                                                                  |
| Lizenzen                        | First-party MIT, Drittmaterial mit eigenen Bedingungen                                      | [LICENSE](../LICENSE), [Notices](../THIRD_PARTY_NOTICES.md)                                                                                          |

Die Landkarte verlinkt auch Fachquellen, die hier nicht vollstaendig neu
auditiert wurden; Quellenumfang und Nachweisgrenzen stehen im Protokoll.

## Anforderungen und Spezifikationsstand

| ID      | Anforderung                                                                                                                            | Erkenntnisstatus                                                       | Abstimmung                                                                       | Quelle und Kontext                                                                                                                                                                      |
| ------- | -------------------------------------------------------------------------------------------------------------------------------------- | ---------------------------------------------------------------------- | -------------------------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| REQ-001 | Eigener C#-Browser im modularen Monorepo, minimaler Einstieg und langfristige Webstandards                                             | Nutzerangabe; durch Plan als Design dokumentiert                       | beschlossen                                                                      | SRC-001 Turn 2/3; [KP-001](./Kontextprotokoll.md#kp-001---urspruenglicher-auftrag-und-freigabe)                                                                                         |
| REQ-002 | Linux X11 und Wayland, Windows, x64 und arm64                                                                                          | Nutzerangabe/Freigabe; Ziel ist keine pauschale Desktop-Zertifizierung | beschlossen                                                                      | SRC-001 Turn 3; SRC-026; [KP-001](./Kontextprotokoll.md#kp-001---urspruenglicher-auftrag-und-freigabe), [KP-003](./Kontextprotokoll.md#kp-003---plattformparitaet-und-native-nachweise) |
| REQ-003 | Tabs, mehrere Fenster und moeglichst starke Isolation                                                                                  | Nutzerangabe; Teilumsetzung laut Fachquellen                           | beschlossen                                                                      | SRC-001 Turn 2; [KP-002](./Kontextprotokoll.md#kp-002---phasenlieferung-und-sicherheitsgrundlage)                                                                                       |
| REQ-004 | Offizielle Standards referenzieren, lokal cachen, nach einem Monat auffrischen, Tests davon trennen; Dokumente fuer Agenten auffindbar | Nutzerangabe und Projektkonvention                                     | beschlossen                                                                      | SRC-001 Turn 3; SRC-025; [KP-001](./Kontextprotokoll.md#kp-001---urspruenglicher-auftrag-und-freigabe)                                                                                  |
| REQ-005 | Hoechste umsetzbare Missing-Prioritaet in kleinen getesteten und einzeln committeten Inkrementen bearbeiten                            | Nutzerangabe und vorhandener Workflow                                  | beschlossen fuer angeforderte Implementierungspassen, nicht Auftrag dieses Turns | SRC-003/004/006; [KP-004](./Kontextprotokoll.md#kp-004---arbeitsablauf-pfade-und-commit-qualitaet)                                                                                      |
| REQ-006 | Bei fehlender wesentlicher Entscheidung konkrete Optionen inklusive Blockieren anbieten und Antwort abwarten                           | Nutzerangabe; Workflow vorhanden                                       | beschlossen                                                                      | SRC-006; [KP-004](./Kontextprotokoll.md#kp-004---arbeitsablauf-pfade-und-commit-qualitaet)                                                                                              |
| REQ-007 | GitHub-Website handhaben koennen                                                                                                       | Nutzerangabe; im Tracker vorhanden, Funktion nicht nachgewiesen        | beschlossen als Endziel; Abnahmekriterien offen                                  | SRC-012; [KP-011](./Kontextprotokoll.md#kp-011---startskript-und-github-endziel)                                                                                                        |
| REQ-008 | Neues Startskript fuer eine URL, ohne vorhandenen Source zu aendern                                                                    | Nutzerangabe; Skript vorhanden                                         | beschlossen fuer damaligen Auftrag                                               | SRC-011; [KP-011](./Kontextprotokoll.md#kp-011---startskript-und-github-endziel)                                                                                                        |
| REQ-009 | Kontext einschliesslich Sessions und GitHub rekonstruieren                                                                             | Nutzerangabe, aktueller Auftrag                                        | beschlossen                                                                      | SRC-033; [KP-015](./Kontextprotokoll.md#kp-015---rekonstruktion-und-abdeckung-dieses-checkpoints)                                                                                       |

## Aktuelle Entscheidungen

| ID      | Entscheidung und Geltungsbereich                                                                                                                                | Status                                                                                         | Ueberlieferte Begruendung                                                                             | Quelle und Kontext                                                                                                                                                                     |
| ------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| DEC-001 | SDL3/SkiaSharp/HarfBuzzSharp, .NET 10, xUnit v3; V8 statt vorlaeufigem Jint/eigenem JS-Engine-Start                                                             | beschlossen                                                                                    | Nutzer: V8 planen, sonst Empfehlungen mit Aenderungen freigegeben                                     | SRC-001 Turn 3; SRC-025; [KP-001](./Kontextprotokoll.md#kp-001---urspruenglicher-auftrag-und-freigabe)                                                                                 |
| DEC-002 | First-party HTML-Parser; URL-Parser hinter eigener API vendort; Unicode 17 als stabile IDNA-Basis                                                               | beschlossen laut Roadmap; keine neue Freigabe hier                                             | Roadmap berichtet Paket-WPT-Fehler als Anlass fuer URL-Vendoring; First-party-Parser als Nutzerwahl   | SRC-025; [KP-002](./Kontextprotokoll.md#kp-002---phasenlieferung-und-sicherheitsgrundlage)                                                                                             |
| DEC-003 | CLI Confinement ohne Fallback; Entwicklung ohne Sandbox nur ausdruecklich                                                                                       | beschlossen laut Roadmap, als Artefakt dokumentiert                                            | Schutzgrenze nicht durch bequemere Startdefaults umgehen                                              | SRC-025, Commit `bf35d4c`; [KP-002](./Kontextprotokoll.md#kp-002---phasenlieferung-und-sicherheitsgrundlage)                                                                           |
| DEC-004 | Vertrauenswuerdig aktivierte Cross-Origin-Top-Level-Navigation durch Browser-Broker zulassen; Renderer erhalten keine Netzwerkautoritaet                        | beschlossen                                                                                    | Normale Navigation erhalten; Same-Origin-only wuerde normale Cross-Site-Links brechen                 | SRC-007/008/010; [KP-007](./Kontextprotokoll.md#kp-007---cross-origin-navigation-und-brokerentscheidung)                                                                               |
| DEC-005 | Credentialless Cross-Origin-Stylesheets als begrenzter CORS-Pfad; Cookies aus, finale ACAO-Pruefung, Redirects innerhalb initialer effektiver Stylesheet-Origin | beschlossen; ersetzt reine Same-Origin-Stylesheet-Politik fuer diesen Pfad                     | Kompatibilitaet verbessern, ohne allgemeines Fetch/CORS zu behaupten                                  | SRC-013, Commit `b080f32`; [KP-009](./Kontextprotokoll.md#kp-009---cors-teilumfang-und-navigation-bestaetigung)                                                                        |
| DEC-006 | Bestaetigungsbasierte Cross-Origin-Link/Form-Navigation beibehalten; explizite Adressleiste bleibt uneingeschraenkt                                             | beschlossen                                                                                    | Nutzer waehlt bestehende Entwicklungspolitik statt strikter Sperre/zusaetzlicher Adressleistenabfrage | SRC-014 Turn 2, SRC-015 Turn 2; [KP-010](./Kontextprotokoll.md#kp-010---bestaetigte-politik-und-uir-korrektur), [KP-013](./Kontextprotokoll.md#kp-013---adressleiste-und-medienlisten) |
| DEC-007 | Idle V8/Listener nur fuer opted-in Dokumente und begrenzte Link-events behalten                                                                                 | beschlossen; praezisiert alte Dispose-after-batch-Grenze                                       | Ohne Host-Verlaengerung koennen spaetere echte Link-Klicks keine Page-Listener erreichen              | SRC-014 Turn 3/4; [KP-012](./Kontextprotokoll.md#kp-012---eng-begrenzte-verlaengerung-des-script-hosts)                                                                                |
| DEC-008 | First-party MIT; Drittmaterial nicht pauschal umlizenzieren                                                                                                     | als gemergte Repositoryentscheidung belegt; separate persoenliche Freigabe nicht rekonstruiert | Notices sollen Herkunft und Lizenzluecken erhalten                                                    | SRC-029; [KP-006](./Kontextprotokoll.md#kp-006---windows-fehlerbehebung-und-lizenzinventar)                                                                                            |

## Offene Fragen, Widersprueche und Risiken

| ID    | Frage oder konkurrierende Aussagen                                                                                                               | Auswirkung                                                                         | Naechste Klaerung                                                                                            | Quellen und Kontext                                                                                                        |
| ----- | ------------------------------------------------------------------------------------------------------------------------------------------------ | ---------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------ | -------------------------------------------------------------------------------------------------------------------------- |
| Q-001 | Was bedeutet GitHub handhaben: statische Seiten, Anmeldung, Navigation, Suche, Issues/PRs, Editor?                                               | Kein messbarer Ziel-Abnahmetest; keine heutige Kompatibilitaetsbehauptung moeglich | Vor einem GitHub-Meilenstein Nutzerkriterien und sichere Testumgebung festlegen                              | REQ-007; [KP-011](./Kontextprotokoll.md#kp-011---startskript-und-github-endziel)                                           |
| Q-002 | Vollstaendige Produktionsautorisation, SOP/CORS/CSP, Frames/Origins, Storage und feindliche Inhalte bleiben offen                                | CLI-Confinement und einzelne Headerpruefungen loesen nicht den Gesamtauftrag       | Prioritaet 1 nach Tracker; wesentliche neue Politik erneut entscheiden                                       | [Missing.md](../plans/Missing.md); [KP-007](./Kontextprotokoll.md#kp-007---cross-origin-navigation-und-brokerentscheidung) |
| Q-003 | README meldet fehlende erfolgreiche Linux-ARM64-Evidenz; Tracker nennt bereits erfolgreiche native Laeufe                                        | Veraltete negative Aussage kann Planung erneut blockieren                          | Historische README-Aussage nicht als aktuellen Beweis verwenden; gezielt Dokumentabgleich in eigenem Auftrag | SRC-025/030/032; [KP-014](./Kontextprotokoll.md#kp-014---aktuelle-artefakte-ci-und-dokumentwidersprueche)                  |
| Q-004 | Architektur/Index und einzelne historische Scripting-Abschnitte beschreiben Dispose vor spaeterem Input; heutiger Link-Dispatch behaelt den Host | Veraltete Grenze kann bereits freigegebene Arbeit erneut blockieren                | DEC-007 und aktuellen spezialisierten Abschnitt/Code verwenden, nicht allgemeinen Event Loop daraus ableiten | SRC-025/031; [KP-012](./Kontextprotokoll.md#kp-012---eng-begrenzte-verlaengerung-des-script-hosts)                         |
| Q-005 | Vorheriger Export behauptet Kontextdateien angelegt; dieser Turn fand Kontext leer vor                                                           | Fruher behauptete Persistenz ist nicht belegt; Verlustursache unbekannt            | Diese Dateien als erste hier verifizierte Rekonstruktion behandeln; keine fruehere Historie erfinden         | SRC-016; [KP-014](./Kontextprotokoll.md#kp-014---aktuelle-artefakte-ci-und-dokumentwidersprueche)                          |
| Q-006 | Wiederholt zehn Passen angefordert, aber Exporte enthalten Unterbrechungen, mehrere Sessions und mehr als einen Commit pro Pass                  | Kein belastbarer globaler Restzaehler                                              | Nicht aus Sessiontiteln ableiten, dass zehn fertig sind oder jetzt automatisch fortzusetzen waeren           | SRC-003/004/014/015; [KP-004](./Kontextprotokoll.md#kp-004---arbeitsablauf-pfade-und-commit-qualitaet)                     |
| Q-007 | Alte Exporte melden pre-existing Formatterfehler; neuester Commit-Turn lief ohne Tests                                                           | Alte/gruene Aussagen nicht ungeprueft auf HEAD uebertragen                         | Bei naechster Codeaenderung aktuelle vorgeschriebene Checks ausfuehren                                       | SRC-014/015/016; [KP-014](./Kontextprotokoll.md#kp-014---aktuelle-artefakte-ci-und-dokumentwidersprueche)                  |
| Q-008 | Paket-/Native-/Cache-Lizenzen und Distribution sind nicht mit First-party-MIT erledigt                                                           | Auslieferung braucht eigene Notice-/Lizenzpruefung                                 | [Notices](../THIRD_PARTY_NOTICES.md) und Produktions-Tracker bearbeiten                                      | SRC-029; [KP-006](./Kontextprotokoll.md#kp-006---windows-fehlerbehebung-und-lizenzinventar)                                |

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
- Keine neuen Builds, .NET-Tests, Native-Smokes oder Standards-Konformitaetstests:
  dieser Auftrag aendert nur die zwei Kontextdokumente. CI-Metadaten wurden
  gelesen, nicht alle Joblogs oder jedes Matrix-Testresultat.
- Geschriebene Kontextdateien erneut gelesen; lokale Links und Markdown-Anker,
  eindeutige REQ-/DEC-/Q-/KP-IDs, Quellenregister und Whitespace automatisiert
  geprueft. Externe GitHub-Links sind Quellenverweise, keine Browser-Abnahmetests.

## Naechste Schritte

1. Beide Sichten als Einstieg verwenden; unbelegte oder alte Aussagen vor
   Codearbeit an Fachquellen und aktuellem Code pruefen.
2. Bei naechstem Implementierungsauftrag die
   [Recommended priority](../plans/Missing.md#recommended-priority) anwenden.
   Bereits entschiedene Cross-Origin-/Adressleisten-/Host-Fragen nicht ohne
   neue Anforderungen erneut als Blocker behandeln.
3. Fuer einen konkret neuen Sicherheitsumfang eine echte Policy-Entscheidung
   einholen; sonst nur mit ausdruecklicher Freigabe zur naechsten Prioritaet
   uebergehen. Keine leeren Passen fuer einen Zahlenzaehler.
4. Vor einem GitHub-Kompatibilitaetsmeilenstein Q-001 in messbare Kriterien
   uebersetzen; derzeit nicht eigenmaechtig Login, Cookies, Images oder
   allgemeinen JS-Scheduler hinzufuegen.
5. Windows-arm64-Endstatus fuer HEAD bei Bedarf erneut abrufen; keine
   Vollmatrix-Erfolgsaussage aus dem laufenden Snapshot ableiten.
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
> neuen Auftraege. Der aktuelle gespeicherte Auftrag war Kontextrekonstruktion,
> nicht Fortsetzung der zehn Implementierungspassen. Bei einem neuen
> Implementierungsauftrag setze am hoechsten noch umsetzbaren Inkrement in
> `plans/Missing.md` an, bewahre DEC-004 bis DEC-007 und halte beide Sichten aktuell.
