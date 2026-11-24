# Wissensstand

## Pflege und Abdeckung

- Projekt: VisualWeb, Repository `khmseu/VisualWeb`.
- Bestaetigter Projektordner: `/bigdata/KAI/projects/VisualWeb`.
- Dokumentationsdatum: 2026-11-24, Zeitzone Europe/Berlin (UTC+01:00).

## Aktueller Wissensstand

**HTML/DOM-Abschlussplanung (2026-11-24; KP-614 bis KP-625; Phases 1-3 committed, Phase 4 implementation active):**
Der Nutzer moechte die HTML-/DOM-Arbeit nach Funktionen priorisieren, die
gaengige Websites und Browser typischerweise benoetigen. Eng benachbarte
Features mit geringem Zusatzaufwand sollen mit aufgenommen werden; der Rest
soll als nachvollziehbare, spaeter fortsetzbare Liste geparkter Luecken
erhalten bleiben. Der fruehere gemeinsame WHATWG-HTML-/DOM-Auftrag steht in
[KP-523](./Kontextprotokoll.md#kp-523---whatwg-html-und-dom-als-neuer-schwerpunkt).
Der abgeschlossene [Template-Plan](../plans/whatwg-html-dom-completion-plan.md)
war nur ein begrenztes Inkrement. Das neue Vorhaben soll zuerst anhand der
bestehenden Implementierung und lokalen Tests nach Nutzungsrelevanz und
Aufwand klassifizieren. Der aktuelle Vorschlag gruppiert haeufige
Dokument-/Fragmentparsing-, DOM-Abfrage-/Mutations-/Markup-, Formular-,
Interaktions- und Scripting-Pfade in fruehe Umsetzungsphasen; kostenguenstige
Nachbarfeatures sollen waehrenddessen mitgenommen werden. Aufwendige oder
selten benoetigte Bereiche (z.B. Shadow DOM/Custom Elements, Media/Canvas,
vollstaendige Web IDL und weitere Ressourcensysteme) sollen mit Standards-
Referenz, Grund, Abhaengigkeiten und Wiederaufnahme-Hinweis in
`plans/html-dom-deferred.md` geparkt werden. Der Nutzer hat den
siebenphasigen Vorschlag genehmigt. Phase 1 hat die aktuelle Unterstuetzung
gegen Guides, Codepfade, lokale Testgruppen und Missing-Tracker geprueft.
Der Initialrang priorisiert (1) gewoehnliche Dokument-Tree-Construction/
Recovery, (2) Fragment-/Markup-Verhalten, (3) gaengige DOM-Abfragen/
Mutationen, (4) Formulare/Interaktionen und (5) Script-/Dokumentlebenszyklus;
kleine angrenzende Parser-/DOM-Erweiterungen koennen in den Umsetzungsphasen
mitgenommen werden. Fuer 12 Deferred-Kategorien sind Ist-Grenzen, Begruendung,
Wiederaufnahmebedingungen, wahrscheinliche Owner/Prerequisites und gepruefte
Code-/Testhinweise festgehalten. Der Register bleibt verfeinerbar und nicht
telemetriegestuetzt. [Roadmap](../plans/whatwg-html-dom-completion-roadmap.md)
und [Deferred-Register](../plans/html-dom-deferred.md) sind im
[Missing-Tracker](../plans/Missing.md#4-general-html-and-dom-compatibility)
und [HTML/DOM-Guide](../docs/html-dom.md) verlinkt. Phase 1 wurde als
`17f78884` (`docs(html-dom): rank common support gaps`) committed.
Phase 2 implementiert, dass In-Body-Dokumente und Body-Fragmente
Tabellenstruktur-Starttags `caption`, `col`, `colgroup`, `tbody`, `thead`,
`tfoot`, `tr`, `td` und `th` mit Parse-Fehler ignorieren; nicht zugeordnete
Struktur-Endtags laufen durch generische In-Body-Recovery. Andere Modi bleiben
explizit begrenzt. Tests und Review sind erfolgt; Solution-Build, Engine.Html.Tests, gesamte
Solution-Test-Suite und Formatverifikation bestehen. Phase 2 wurde als
`72b27f7a` (`fix(html): recover stray table structure tags`) committed.
Phase 3 implementiert script-facing `Element.innerHTML` fuer den explizit
unterstuetzten HTML-Subset: kontextbezogene Fragment-Parsing/Serialisierung,
Template-Content, begrenzte Traversierung/Ausgabe und atomare Kindersetzung
bei Fehlern. Serialisierte Ausgabe ist keine Sanitization; unstuetzte
Kontexte/Algorithmen scheitern explizit und Parser-Insertion bleibt inert.
Die Processing-Instruction-Serialisierung endet standardgemaess mit `?>`,
einschliesslich Leerraum vor dem Terminator bei leerer Datenfolge. Build,
betroffene Testprojekte, vollstaendige Solution-Tests und Formatverifikation
bestanden; ein erster paralleler Volltestlauf zeigte zwei isoliert bestandene
Timeouts, der nachfolgende Volltestlauf war erfolgreich. Review ist APPROVED.
Phase 3 wurde als `f0a502b5` (`feat(scripting): add bounded Element.innerHTML`)
committed. Phase 4 setzt als begrenzten naechsten Schritt auf enabled
`button type=button` und `input type=button`: opted-in pointer-, Enter- und
Space-Aktivierung dispatcht ein trusted plain `click` mit retained repaint,
ohne Submit- oder Navigationsdefault. Das folgt der bestehenden
browser-owned focus-target/Event-Pipeline; eventuelle IPC-Uebermittlung bleibt
data-only. Dies ist eine aus dem bestehenden `inert`-Control-Pfad abgeleitete
Priorisierung, keine volle PointerEvent-/Button-Aktivierungsimplementierung.
Implementiert mit rendererlokaler Zielvalidierung und data-only IPC v51;
Phase 4 bleibt aktiv. Details: [KP-624](./Kontextprotokoll.md#kp-624---phase-4-button-click-scope-gewaehlt)
und [KP-625](./Kontextprotokoll.md#kp-625---begrenzte-type-button-klickaktivierung).
Die Priorisierung wurde nach Codeeinsicht verfeinert: Foreign-Content erfordert
namespace-erhaltende DOM-Elemente/-Attribute und Tree-Construction-Regeln; da
`DomElement.NamespaceUri` auf HTML festgelegt ist, wird der Parserumfang nicht
approximiert und HDF-005 im Deferred-Register geparkt. Dies ist eine aus der
Nutzerprioritaet "guenstig ergaenzen, Rest verschieben" abgeleitete
Aufwandsentscheidung, keine separate Telemetrie- oder Konformitaetsaussage.
Der Support-Prozentsatz bleibt unveraendert. Siehe
[Phase-2-Abschlussbericht](../plans/whatwg-html-dom-completion-roadmap-phase-2-complete.md),
[KP-615](./Kontextprotokoll.md#kp-615---nutzerpriorisiert-haeufige-html-dom-features),
[KP-617](./Kontextprotokoll.md#kp-617---priorisierter-html-dom-plan-genehmigt)
bis [KP-621](./Kontextprotokoll.md#kp-621---phase-2-commit-und-phase-3-begonnen).
Phase 3 und die finale Validierung sind unter
[KP-622](./Kontextprotokoll.md#kp-622---bounded-innerhtml-implementiert-und-validiert)
dokumentiert; Commit und Phase 4 starten in
[KP-623](./Kontextprotokoll.md#kp-623---phase-3-commit-und-phase-4-begonnen),
mit der Begruendung zur Auswahl des naechsten Verhaltens in
[KP-624](./Kontextprotokoll.md#kp-624---phase-4-button-click-scope-gewaehlt).

**WHATWG-HTML/DOM-Auftrag (2026-11-20; KP-523 bis KP-532; inkrementelle Fortsetzung):**
Der Nutzer setzt HTML/DOM als Schwerpunkt und moechte zusammengehoerige
Features parallel bearbeiten. Beschlossen ist ein gestufter Template-Start:
Phase 1 fuehrt native template contents als `DocumentFragment` mit inertem
Template-Owner-Document sowie Adoptions-/Clone-Semantik ein; Phase 2 ergaenzt
einen begrenzten HTML-Parserumfang; Phase 3 prueft subsystemuebergreifende
Inertheit und Grenzen. Scriptseitiges `innerHTML`, Scriptbindings und
Vollkonformitaet sind ausgeschlossen. Spaetere Inkremente bleiben separat zu
waehlen. Die heuristische Ranking-Reihenfolge (URL ~80%, Encoding ~55%,
HTML/DOM je ~20%) wird durch die ausdrueckliche Nutzerprioritaet uebersteuert.
Die lokalen HTML-/DOM-Caches haben `checkedAt: 2026-11-07` und sind am
2026-11-20 frisch. Der begrenzte Plan ist mit `3a02cca` (native DOM),
`a752387` (Parser-Subset) und `5752d3b` (Inertheit/Grenztests) abgeschlossen.
Phase 2 baut Templates in Head/Body mit begrenztem In-Body-Inhalt und
verschachtelten Templates in getrennten inert-owner-Dokumenten;
Content-Fragmente zaehlen gegen `MaxNodes`, Scripts loesen keinen
Parser-Completion-Callback aus. Foreign Content und Table/Select-Template-
Kontexte bleiben unsupported. Phase 3 ergaenzt ausschliesslich
Regressionsabdeckung: exakte Template-`MaxDepth`-Grenze, Inertheit von Inhalt,
Styles und Scripts bei statischem/Prozess-Rendering einschliesslich retained
Resize sowie Clone-Abbruch waehrend des Preflights des Template-Inhalts.
Engine.Dom: 64 Tests; Engine.Html: 7.295; Browser: 1.396. Der abschliessende
`dotnet test VisualWeb.slnx --no-build`-Lauf besteht; Formatverifikation und
`git diff --check` bestehen. Ein vorheriger Volltestlauf hatte einen Timeout
im bestehenden Prozess-Lifecycle-Test; dessen isolierter Lauf (2/2) und der
anschliessende Volltest bestanden. Dies schliesst nur den genehmigten
Template-Plan ab, nicht die WHATWG-HTML/DOM-Vollkonformitaet. Siehe
[Plan](../plans/whatwg-html-dom-completion-plan.md),
[Phase 1](../plans/whatwg-html-dom-completion-phase-1-complete.md),
[Phase 2](../plans/whatwg-html-dom-completion-phase-2-complete.md),
[Phase 3](../plans/whatwg-html-dom-completion-phase-3-complete.md),
[Gesamtabschluss](../plans/whatwg-html-dom-completion-complete.md) und
[KP-528](./Kontextprotokoll.md#kp-528---template-plan-committed-und-abgeschlossen).

**HTML/DOM-Fragment Parsing (2026-11-20; KP-530/531; committed und gepusht):**
Der Nutzer waehlte fuer Fragment Parsing alle derzeit unterstuetzten
Dokument-Kontexte einschliesslich Table-, Select-, Template- und Text-Modi;
SVG/MathML, `noscript` und frameset/frame-Fragmentkontexte blieben in diesem
ersten Stand explizit unsupported; `noscript` wurde spaeter fuer
scripting-disabled Fragmentparser in KP-609 freigegeben.
Script-Bindings, `innerHTML` und Vollkonformitaet sind nicht Teil dieses
Inkrements. `HtmlParser.ParseFragment` liefert ein kontextdokument-eigenes
`DocumentFragment`, setzt die bestehenden begrenzten Einfuegemodi und
Texttokenizerzustaende um und uebernimmt den Form-Pointer aus der Kontext-
Ancestor-Kette. Das `html`-Fragment ignoriert `</html>` im AfterBody-Modus,
statt in den Dokumentmodus AfterAfterBody zu wechseln. Commit `bbeeff7` ist
auf `origin/main`. 32 fokussierte Faelle; Engine.Html 7.327/7.327; gesamter
Solution-Testlauf 17.323 bestanden und 2 bedingte Browserfaelle uebersprungen.
Solution-Build (0 Warnungen/Fehler), Formatverifikation und `git diff --check`
bestanden. Windows, CodeQL und Prebuild-CI bestehen; Linux x64/arm64 bestehen,
der Linux-Trunk-Job scheitert mit 28 Markdownlint-Befunden ausschliesslich in
unveraenderten Abschluss-/Plan-Dokumenten und `tests/Core.Tests/Data/README.md`.
Der gleiche Trunk-Job scheiterte auf dem unmittelbaren Basis-Commit; keine der
beanstandeten Dateien gehoert zu diesem Inkrement. Siehe
[HTML/DOM-Guide](../docs/html-dom.md), [Missing.md](../plans/Missing.md) und
[KP-530](./Kontextprotokoll.md#kp-530---fragment-parsing-umfang-beschlossen),
[KP-531](./Kontextprotokoll.md#kp-531---fragment-parsing-committed-und-ci-geprueft).

**HTML/DOM-Aktive Formatierung (2026-11-20; KP-532/534; furthest-block-Erweiterung committed und gepusht):**
Der Parser speichert Token-Attribut-Snapshots, setzt die ungeordnete
Drei-Eintraege-Noah's-Ark-Grenze um, rekonstruiert aktive Formatierung und
beachtet Template-/Caption-/Cell-Marker. Der Adoption-Agency-Zweig ohne
furthest block ist implementiert; begrenztes Furthest-Block-Reparenting
unterstuetzt Clone/Removal, Bookmark-Verschiebungen und offene-Elemente-Stack-
Reihenfolge. Nested anchors/nobr, Reparenting mit noetigem Table-Foster-
Parenting sowie Table-Text-Foster-Parenting mit noetiger Rekonstruktion scheitern
weiterhin explizit. Commit `b4b4a88` ist auf `origin/main`. Engine.Html
7.346/7.346, Solution-Build (0 Warnungen/Fehler), voller Solution-Testlauf
(17.342 bestanden, 2 bedingte Browserfaelle uebersprungen), Formatverifikation
und `git diff --check` bestehen. Der Read-only-Code-Review ist APPROVED; die
geforderten Kommentar-/Abdeckungsnits wurden behoben. Fuer `f748224` bestehen
Linux x64/arm64, Windows x64/arm64, CodeQL und Dev-Environment-Prebuild. Der
Linux-Trunk-Job meldet dieselben 28 Markdownlint-Befunde in unveraenderten
Plan-/Abschlussdokumenten und `tests/Core.Tests/Data/README.md` wie der direkte
Basislauf. Der Prebuild fuer `47bb853` ist inzwischen erfolgreich abgeschlossen
(Run `44664881257`, aktualisiert 2026-11-20T03:26:49Z; KP-543).
Der inkrementelle Parserumfang ist keine Vollkonformitaetsbehauptung. Siehe
[HTML/DOM-Guide](../docs/html-dom.md), [Missing.md](../plans/Missing.md) und
[KP-532](./Kontextprotokoll.md#kp-532---aktive-formatierung-implementiert-und-validiert).

**Nested Anchor/Nobr-Recovery (2026-11-20; KP-540/542; committed und gepusht):**
Commit `47bb853` ergaenzt nested-`a`/`nobr`-Starttag-Recovery und Caption-
Scope-Grenzen. Engine.Html.Tests (7.362), Solution-Build, Volltests (17.358
bestanden, 2 bedingte Browser-Skips), Formatverifikation und Diff-Check
bestanden. Windows x64/arm64 und CodeQL bestehen. Linux x64/arm64 bestehen;
der Linux-Workflow scheitert ausschliesslich an denselben 28 Trunk-
Markdownlint-Befunden in unveraenderten Dateien wie der Basislauf. Siehe
[KP-540](./Kontextprotokoll.md#kp-540---nested-anchornobr-inkrement-gepusht),
[KP-542](./Kontextprotokoll.md#kp-542---nested-formatting-ci-verifiziert) und
[KP-543](./Kontextprotokoll.md#kp-543---prebuild-abgeschlossen-und-naechster-html-schnitt-gestartet).
**HTML-Tabellen-Input-Foster-Parenting (2026-11-20; KP-543 bis KP-545; committed und gepusht):**
Commit `c82754a` leitet nicht versteckte `input`-Starttags in Table-,
TableBody- und Row-Modi durch die begrenzte In-Body-/Foster-Parent-Logik.
Zellen behalten normale In-Body-Einfuegung; versteckte Inputs bleiben direkte
Tabellenkinder. Andere Tokens mit Foster-Parent-Bedarf und Adoption-Agency-
Reparenting, das Table-Foster-Parenting erfordert, bleiben unsupported.
Sechs fokussierte Faelle bestehen; Engine.Html.Tests 7.365/7.365, Solution-
Build ohne Warnungen/Fehler, Solution-Volltests 17.361 bestanden mit 2
bedingten Browser-Skips, Formatverifikation und Diff-Check bestehen. Commit
`c82754a` ist auf `origin/main`. Linux, Windows, CodeQL und Prebuild laufen
nicht mehr: Windows x64/arm64, Linux x64/arm64, CodeQL und Prebuild bestehen;
der Linux-Workflow scheitert nur am Trunk-Job mit denselben unveraenderten
Markdownlint-Befunden wie die frueheren Basislaeufe. Details stehen in
[KP-546](./Kontextprotokoll.md#kp-546---ci-fuer-table-input-commit-gestartet).
Siehe [KP-545](./Kontextprotokoll.md#kp-545---table-input-foster-parenting-committed).

**HTML-Tabellen-`br`/`hr`-Foster-Parenting (2026-11-20; KP-547; lokal validiert):**
`br`- und `hr`-Starttags in Table-, TableBody- und Row-Modi laufen durch
In-Body-Verarbeitung bei temporaer aktiviertem, begrenztem Foster-Parenting;
Parsefehler werden diagnostiziert und der vorherige Foster-Parenting-Zustand
wird wiederhergestellt. Andere Foster-Parenting-Tokens bleiben unsupported.
Vier fokussierte Faelle, Engine.Html.Tests (7.369), Solution-Build (0
Warnungen/Fehler), zweiter Volltestlauf (17.365 bestanden, zwei bedingte
Browser-Skips), Formatverifikation und `git diff --check` bestehen. Der erste
Volltestlauf hatte waehrend paralleler Formatverifikation einen Timeout in
einem nicht betroffenen Engine.Scripting-URL-Test; dessen gezielter Retry und
der zweite Volltestlauf bestehen. Commit `776c3d7` ist erstellt und auf
`origin/main` verifiziert. Die Linux x64/arm64, Windows x64/arm64,
Push-on-main und Prebuild Jobs bestehen; der Linux-Workflow scheitert nur am
Trunk-Job mit 28 Markdownlint-Befunden in unveraenderten Plan- und
Core-Testdateien. Siehe
[KP-547](./Kontextprotokoll.md#kp-547---brhr-in-table-modes) und
[KP-548](./Kontextprotokoll.md#kp-548---brhr-commit-gepusht).

**HTML-Tabellen-`div`-Foster-Parenting (2026-11-20; KP-549/550; committed und gepusht):**
`div`-Start- und Endtags in Table-, TableBody- und Row-Modi laufen jetzt lokal
durch die bestehende In-Body-Verarbeitung bei begrenztem Foster-Parenting.
Drei neue Faelle belegen gefosterte Blockeinsetzung, Textreihenfolge und
Diagnostik; sie scheiterten vor der Aenderung explizit mit
`UnsupportedHtmlException`. Engine.Html.Tests 7.371/7.371, Solution-Build
(0 Warnungen/Fehler), Volltests (17.367 bestanden, zwei bedingte
Browser-Skips), Formatverifikation und Diff-Check bestehen. Code und
Dokumentation sind als `2461c70` committed und auf `origin/main` verifiziert;
weitere Tokens bleiben unsupported. Windows x64/arm64, Push-on-main und
Commit-Prebuild bestehen; Linux x64/arm64 bestehen, aber der Linux-Workflow
scheitert am Trunk-Job wegen unveraenderter Markdownlint-Befunde. Siehe
[KP-549](./Kontextprotokoll.md#kp-549---div-table-foster-parenting) und
[KP-550](./Kontextprotokoll.md#kp-550---div-commit-gepusht).

**HTML-Tabellen-`p`-Foster-Parenting (2026-11-20; KP-551/552; committed und gepusht):**
`p`-Start- und Endtags in denselben drei Tabellenmodi nutzen jetzt die
begrenzte In-Body-/Foster-Parent-Verarbeitung. Die drei neuen
Paragraph-Faelle scheiterten vor der Aenderung explizit; zusammen mit den
Div-Faellen bestehen nun sechs. Engine.Html.Tests 7.374/7.374,
Solution-Build (0 Warnungen/Fehler), Volltestlauf (17.370 bestanden, zwei
bedingte Browser-Skips), Formatverifikation und Diff-Check bestehen. Code und
Dokumentation sind als `7ff60cf` committed und auf `origin/main` verifiziert.
Windows confinement, Commit-Prebuild und Push-on-main bestehen; Linux x64/arm64
bestehen, aber der Linux-Workflow scheitert im Trunk-Job an unveraenderten
Markdownlint-Befunden in Plan- und Core-Testdateien. Siehe
[KP-551](./Kontextprotokoll.md#kp-551---p-table-foster-parenting) und
[KP-552](./Kontextprotokoll.md#kp-552---p-commit-gepusht).

**HTML-Tabellen-Heading-Foster-Parenting (2026-11-20; KP-553; committed und gepusht):**
`h1`-`h6`-Start- und Endtags in Table-, TableBody- und Row-Modi nutzen jetzt
die begrenzte In-Body-/Foster-Parent-Verarbeitung. 18 neue Faelle scheiterten
zunaechst explizit; zusammen mit den bisherigen Block-/Paragraph-Faellen
bestehen 24. Engine.Html.Tests (7.392), Solution-Build, Volltestlauf (17.388
bestanden, zwei bedingte Browser-Skips), Formatverifikation und Diff-Check
bestanden. Commit `7767e07` ist auf `origin/main` verifiziert. Windows confinement und
Push-on-main bestehen; Linux x64/arm64 bestehen, aber der Linux-Workflow
scheitert im Trunk-Job an den unveraenderten Markdownlint-Befunden.
Commit-Prebuild laeuft bei der letzten Abfrage noch. Siehe
[KP-553](./Kontextprotokoll.md#kp-553---heading-table-foster-parenting).

**HTML-Tabellen-`pre`/`listing`-Foster-Parenting (2026-11-20; KP-554/555; committed und gepusht):**
Start- und Endtags fuer `pre` und `listing` in Table-, TableBody- und
Row-Modi laufen durch dieselbe begrenzte In-Body-/Foster-Parent-Verarbeitung.
Die standardisierte Entfernung eines direkt folgenden LF bleibt aktiv.
Zeichen nach einem gefosterten Starttag werden gemaess In-Table-Regel in das
aktuelle Element eingefuegt, nicht als Geschwistertext vor die Tabelle. Sechs
fokussierte Faelle, Engine.Html.Tests (7.398), Solution-Build (0
Warnungen/Fehler), Volltestlauf (17.394 bestanden, zwei bedingte
Browser-Skips), Formatverifikation und Diff-Check bestehen. Commit `374c73d`
ist auf `origin/main` verifiziert. Windows confinement und Push-on-main
bestehen; Linux x64/arm64 bestehen, aber der Linux-Workflow scheitert im
Trunk-Job an den unveraenderten Markdownlint-Befunden. Commit-Prebuild
besteht. Andere Foster-Parenting-Tokens bleiben unsupported. Siehe
[KP-554](./Kontextprotokoll.md#kp-554---pre-listing-table-foster-parenting) und
[KP-555](./Kontextprotokoll.md#kp-555---pre-listing-commit-gepusht).

**Weitere Blockelemente im Tabellen-Foster-Parenting (2026-11-20; KP-556; lokal validiert):**
Die verbleibenden 23 generischen Blockelemente des In-Body-Teilumfangs
verwenden in Table-, TableBody- und Row-Modi dieselbe begrenzte
Foster-Parent-Verarbeitung. Die Regressionen durchlaufen alle drei Modi je
Element und pruefen Geschwisterreihenfolge und Diagnostik. Engine.Html.Tests
(7.421), Solution-Build (0 Warnungen/Fehler), Volltestlauf (17.417 bestanden,
zwei bedingte Browser-Skips), Formatverifikation und Diff-Check bestehen.
Commit `064ef98` ist auf `origin/main` verifiziert. Windows confinement und
Push-on-main bestehen; Linux x64/arm64 bestehen, aber der Linux-Workflow
scheitert im Trunk-Job an den unveraenderten Markdownlint-Befunden.
Commit-Prebuild laeuft bei der letzten Abfrage noch. Tokens ausserhalb der
unterstuetzten Blockelemente bleiben explizit unsupported.

**HTML-Tabellen-Listenelemente (2026-11-20; KP-558; lokal validiert):**
`li`, `dd` und `dt` starten und enden in Table-, TableBody- und Row-Modi
jetzt mit begrenzter In-Body-/Foster-Parent-Verarbeitung. Neun Szenarien,
Engine.Html.Tests (7.424), Solution-Build (0 Warnungen/Fehler),
Volltestlauf (17.420 bestanden, zwei bedingte Browser-Skips),
Formatverifikation und Diff-Check bestehen. Commit `9a4f7d7` ist auf
`origin/main` verifiziert; Linux, Windows, Push-on-main und Prebuild laufen
noch. Weitere foster-pflichtige Tokens bleiben unsupported.

**HTML-Tabellen-Button-Foster-Parenting (2026-11-20; KP-560; committed und gepusht):**
`button`-Start- und Endtags in Table-, TableBody- und Row-Modi nutzen dieselbe
begrenzte In-Body-/Foster-Parent-Verarbeitung. Die Regression prueft
Einordnung, Geschwistertext und Diagnostik in allen drei Modi. Engine.Html.Tests
(7.425), Solution-Build (0 Warnungen/Fehler), Volltestlauf (17.421 bestanden,
zwei bedingte Browser-Skips), Formatverifikation und Diff-Check bestehen.
Commit `fd35613` ist nach `origin/main` gepusht. Windows confinement und
Push-on-main bestehen; Linux x64/arm64 bestehen, aber der Linux-Workflow
scheitert im Trunk-Job an bekannten Markdownlint-Befunden in unveraenderten
Plan- und Core-Testdateien. Commit-Prebuild besteht. Siehe
[KP-560](./Kontextprotokoll.md#kp-560---button-table-foster-parenting)
und [KP-561](./Kontextprotokoll.md#kp-561---button-commit-gepusht).

**HTML-Tabellen-`img`-Foster-Parenting (2026-11-20; KP-562; committed und gepusht):**
`img`-Start- und Endtags in Table-, TableBody- und Row-Modi laufen durch die
begrenzte In-Body-/Foster-Parent-Verarbeitung. Die Regression prueft das
gefosterte Void-Element und beide Parsefehlerdiagnosen. Engine.Html.Tests
(7.426), Solution-Build (0 Warnungen/Fehler), Volltestlauf (17.422 bestanden,
zwei bedingte Browser-Skips), Formatverifikation und Diff-Check bestehen.
Commit `0c4c429` ist nach `origin/main` gepusht; seine CI-Jobs laufen bei der
letzten Abfrage bestanden fuer Windows, Push-on-main und Prebuild. Linux
x64/arm64 bestehen; Trunk scheitert an unveraenderten Markdownlint-Befunden.
Siehe
[KP-562](./Kontextprotokoll.md#kp-562---img-table-foster-parenting) und
[KP-563](./Kontextprotokoll.md#kp-563---img-commit-gepusht).

**HTML-Tabellen-`wbr`-Foster-Parenting (2026-11-20; KP-564; committed und gepusht):**
`wbr`-Start- und Endtags in Table-, TableBody- und Row-Modi folgen jetzt der
begrenzten In-Body-/Foster-Parent-Verarbeitung. Der Test prueft Reihenfolge und
Diagnostik in allen drei Modi. Engine.Html.Tests (7.427), Solution-Build
(0 Warnungen/Fehler), Volltestlauf (17.423 bestanden, zwei bedingte
Browser-Skips), Formatverifikation und Diff-Check bestehen. Commit `5c5f6b8`
ist nach `origin/main` gepusht. Windows confinement und Push-on-main bestehen;
Linux x64/arm64 bestehen, aber Trunk scheitert an unveraenderten
Markdownlint-Befunden. Commit-Prebuild laeuft bei der letzten Abfrage noch.
Siehe [KP-564](./Kontextprotokoll.md#kp-564---wbr-table-foster-parenting) und
[KP-565](./Kontextprotokoll.md#kp-565---wbr-commit-gepusht).

**HTML-Tabellen-Foster-Parenting weiterer Void-Elemente (2026-11-20; KP-567; committed und gepusht):**
Die elf in-body-Void-Tags `area`, `base`, `basefont`, `bgsound`, `embed`,
`keygen`, `link`, `meta`, `param`, `source` und `track` werden in Table-,
TableBody- und Row-Modi begrenzt mit In-Body-/Foster-Parent-Regeln verarbeitet.
Die Tests validieren alle Tags in allen drei Modi samt Baumordnung und
Endtagdiagnostik; es wird kein Ressourcenladen eingefuehrt. Engine.Html.Tests
(7.438), Solution-Build (0 Warnungen/Fehler), Volltestlauf (17.434 bestanden,
zwei bedingte Browser-Skips), Formatverifikation und Diff-Check bestehen.
Commit `ad3ea20` ist nach `origin/main` gepusht; CI-Jobs laufen bei der
letzten Abfrage. Siehe
[KP-567](./Kontextprotokoll.md#kp-567---remaining-void-table-foster-parenting)
und [KP-568](./Kontextprotokoll.md#kp-568---void-commit-gepusht).

**Weitere Table-Foster-Tokens (2026-11-20; KP-569 bis KP-577; committed und gepusht):**
Die begrenzte In-Body-/Foster-Parent-Verarbeitung in Table-, TableBody- und
Row-Modi umfasst seit KP-568 `button`, `img`, `wbr`, den `image`-Alias, elf
weitere Void-Tags, `textarea`, `title`, `iframe`, `noembed`, `noframes` sowie
`xmp`.
`textarea` erhaelt RCDATA mit Unterdrueckung eines initialen LF; `title` erhaelt RCDATA
ohne diese Unterdrueckung. Die drei letzten Elemente nutzen RAWTEXT; es werden
keine Browsing-Kontexte oder Ressourcenladungen hinzugefuegt. `xmp` erhaelt
ebenfalls RAWTEXT. Die fokussierten
Tests, Solution-Builds, Volltests und Formatverifikation bestehen; zuletzt
17.447 Solution-Tests plus zwei bedingte Skips und 7.451 Engine.Html-Tests.
Commits `fd35613`, `0c4c429`, `5c5f6b8`, `ad3ea20`, `dae8b59`, `995aada`,
`da64411`, `d3d89df` und `c2b39dd` sind auf `origin/main` gepusht. Bei
`995aada` bestanden Windows, Linux x64/arm64, CodeQL und Prebuild; Trunk
scheiterte mit Markdownlint-Befunden in unveraenderten Plan- und
Core-Testdateien. Bei `da64411` bestanden Windows, Linux x64/arm64, CodeQL
und Prebuild; Trunk meldete dieselben Befunde. Bei `d3d89df` bestanden Windows,
Linux x64/arm64 und CodeQL; Trunk meldete wieder dieselben Baseline-Befunde,
Prebuild lief zuletzt noch. Bei `c2b39dd` bestehen Windows confinement und
CodeQL; Linux development scheitert mit Markdownlint-Befunden in unveraenderten
Plan-Abschlussdokumenten und `tests/Core.Tests/Data/README.md`; Dev Environment
Prebuild besteht (Run `44713851177`).
Weitere foster-pflichtige Tokens bleiben explizit unsupported, bis ein
separates Inkrement sie abdeckt. Siehe
[KP-569](./Kontextprotokoll.md#kp-569---button-commit-gepusht) bis
[KP-577](./Kontextprotokoll.md#kp-577---xmp-table-modes).

**Table-Foster-Text-Routing (2026-11-20; KP-578; lokal validiert):**
Eine Pruefung von `specs/cache/html.html#parsing-main-intable` zeigte, dass
Zeichen nur dann im Table-Text-Modus gepuffert werden, wenn der (angepasste)
aktuelle Knoten `table`, `tbody`, `template`, `tfoot`, `thead` oder `tr` ist.
Nach einem gefosterten Nicht-Tabellen-Element muss In-Body-Verarbeitung den
Text als Kind dieses Elements einfuegen. Die vorherigen Tests und diese
Wissensnotiz erwarteten hier faelschlich Geschwistertext; KP-578 korrigiert
Implementierung, Tests und Support-Dokumentation. Auch die Fragment-Parsing-
Sonderregel fuer den angepassten aktuellen Knoten bleibt erhalten. Engine.Html
7.452/7.452, Solution-Build (0 Warnungen/Fehler), Volltests (17.448 bestanden,
zwei bedingte Skips), Formatverifikation und staged Diff-Check bestehen.
Commit `f4e2e5a` ist nach `origin/main` gepusht; Windows, Linux, CodeQL und
Prebuild-CI liefen bei letzter Abfrage noch. Die Linux-CI fuer den
vorherigen XMP-Commit scheitert nur an den unveraenderten Markdownlint-
Befunden. Die beiden Kontextdateien bleiben ausserhalb des Source-Commits.
Siehe
[KP-578](./Kontextprotokoll.md#kp-578---table-foster-text-routing).

**HTML-Template-Verarbeitung in Tabellenmodi (2026-11-20; KP-579; committed und gepusht):**
Template-Start- und Endtags in Table-, TableBody- und Row-Modi verwenden die
vorhandene In-Head-/Template-Verarbeitung; beim Schliessen wird der passende
Tabellenmodus wiederhergestellt. Das vorhandene native `Content`-Fragment bleibt
unveraendert. Engine.Html (7.453/7.453), Solution-Build (0 Warnungen/Fehler),
Formatverifikation und Diff-Check bestehen. Der Volltestlauf hatte einen
V8-Deadline-Fehler in `UrlBindingTests.UrlComponentSettersMatchPinnedControlCharacterWptCases`;
der isolierte Test besteht (1/1). Commit `f7e1f2f` ist nach `origin/main`
gepusht. Windows, Linux x64/arm64, CodeQL und Prebuild bestehen; Linux Trunk
scheitert an unveraenderten Markdownlint-Befunden. Die CI fuer `f4e2e5a` ist
abgeschlossen: Windows, CodeQL und Prebuild bestehen, Linux scheitert an
unveraenderten Markdownlint-Befunden. Die Kontextdateien
bleiben ausserhalb des Source-Commits. Die anschliessende Auswahl der
Tabellen-Insertion-Modes innerhalb des `InTemplate`-Modus ist in KP-580
umgesetzt.
Siehe [KP-579](./Kontextprotokoll.md#kp-579---template-table-modes).

**Template-Tabelleninhalt (2026-11-20; KP-580; committed und gepusht):**
Der Schnitt erweitert die Insertion-Mode-Auswahl innerhalb
von Template-Inhalt fuer die vorhandenen begrenzten Tabellenalgorithmen und
behebt die EOF-Schliessung offener Templates in diesen Modi. Engine.Html.Tests
bestehen mit 7.471 Tests, der Solution-Build mit null Warnungen/Fehlern und
die finale Formatpruefung. Der parallele Volltestlauf scheiterte an xUnit-
Discovery im Paint-Projekt, das isoliert und im seriellen Volltest besteht;
der serielle Volltest besteht mit 17.467 Tests und zwei bedingten Skips.
Commit `bbe5c07` ist gepusht; Linux x64/arm64, Windows, Push-on-main und
Prebuild bestehen. Linux Trunk scheitert an 28 Markdownlint-Befunden in
unveraenderten Dateien.
Der vermeintliche Konflikt mit Furthest-Block-Adoption entstand aus dem Lesen
eines veralteten Checkpoints: `b4b4a88` ist bereits in `HEAD` enthalten.
Siehe [KP-580](./Kontextprotokoll.md#kp-580---checkpoint-klaerung-und-template-tabelleninhalt).

**Span-Foster-Parenting in Tabellenmodi (2026-11-20; KP-581; abgeschlossen):**
`span`-Start und -Endtags nutzen nun begrenztes In-Body-Foster-Parenting in
Table-, ColumnGroup-, TableBody- und Row-Modi; Formatierungs-Reparatur fuer
andere Tags bleibt unsupported. Template-Tabelleninhalt ist abgedeckt.
Engine.Html.Tests (7.477), Solution-Build (0 Warnungen/Fehler),
Formatverifikation und der serielle Volltest (17.473 bestanden, zwei
bedingte Browser-Skips) bestehen.
Commit `95d78ac` ist gepusht; Linux x64/arm64, Windows und Push-on-main
bestehen. Linux Trunk meldet die bekannten 28 Markdownlint-Befunde in
unveraenderten Dateien; Prebuild `44798780949` ist ebenfalls erfolgreich.
Siehe [KP-581](./Kontextprotokoll.md#kp-581---span-foster-parenting-in-tabellenmodi).

**Plaintext-Foster-Parenting in Tabellenmodi (2026-11-20; KP-582; gepusht):**
`plaintext`-Starttags und unpassende Endtags werden aus Table-, ColumnGroup-,
TableBody- und Row-Modi foster-parented. Der Start schaltet auf PLAINTEXT und
macht den Rest des Dokuments zu literalem Text; Template-Tabelleninhalt ist
mitgetestet. Sechs fokussierte Faelle, Engine.Html.Tests (7.483), Solution-Build
(0 Warnungen/Fehler), Formatverifikation und serieller Volltest (17.479
bestanden, zwei bedingte Browser-Skips) bestehen. Commit `72fcea7` ist gepusht;
Linux x64/arm64, Windows x64/arm64 und Push-on-main bestehen. Linux Trunk
meldet dieselben 28 bestehenden Markdownlint-Befunde in unbeteiligten Dateien;
Prebuild `44802344668` ist erfolgreich. Siehe
[KP-582](./Kontextprotokoll.md#kp-582---plaintext-foster-parenting-in-tabellenmodi).

**Select in Template-Inhalt (2026-11-20; KP-583; validiert und gepusht):**
Ein `select`-Start in Template-Inhalt nutzt nun den vorhandenen begrenzten
In-Body-/Select-Parser gemaess `in template`. Der Regressionstest prueft
Options, inert template-contents OwnerDocument, Moduswiederherstellung nach
`</select>` und Body-Fortsetzung nach `</template>`; Template-Starts in Selects,
verschachtelte Selects und Foreign Content bleiben unsupported. Der fokussierte
Test und Engine.Html.Tests (7.483), Solution-Build (0 Warnungen/Fehler),
Formatverifikation und serieller Volltest (17.479 Tests; zwei bedingte
Browser-Skips) bestehen. Commit `db368e1` ist gepusht; Linux x64/arm64,
Windows x64/arm64 und CodeQL bestehen. Linux Trunk meldet 28 Markdownlint-
Befunde ausschliesslich in unveraenderten Plan-/Core-Testdateien. Prebuild
`44827582469` ist inzwischen erfolgreich abgeschlossen.
Die bereits gestaged Kontextdateien bleiben ausserhalb eines Source-Commits.
Siehe
[KP-583](./Kontextprotokoll.md#kp-583---select-verarbeitung-in-template-inhalt).

**Select-Foster-Parenting in Tabellenmodi (2026-11-20; KP-587; committed und gepusht):**
`select`-Starts und unmatched Endtags verwenden in Table-, TableBody-, Row-
und ColumnGroup-Modi den begrenzten Foster-Parent-/In-Body-Pfad; offene
Select-Elemente nutzen anschliessend die bestehenden Select-Token-Regeln.
Fuenf Regressionstests waren vor Implementierung rot. Engine.Html.Tests
besteht mit 7.500 Tests; Solution-Build ohne Warnungen/Fehler, Volltest
(17.496 bestanden, zwei bedingte Browser-Skips), Formatverifikation und
Diff-Check bestehen. Commit `7fd2224` ist gepusht. Windows confinement und
CodeQL bestehen; Linux development meldet 28 Markdownlint-Befunde in
unveraenderten Plan-/Core-Testdateien. Der Dev-Environment-Prebuild besteht.
Siehe
[KP-587](./Kontextprotokoll.md#kp-587---select-in-tabellenmodi).

**Phrasierungselemente in Tabellenmodi (2026-11-21; KP-591/592; lokal committed):**
Zwoelf gewoehnliche Elemente (`abbr`, `cite`, `data`, `dfn`, `kbd`, `mark`,
`q`, `samp`, `sub`, `sup`, `time` und `var`) nutzen den bestehenden begrenzten
Foster-Parent-/In-Body-Pfad fuer Start- und Endtags in vier Tabellenkontexten.
49 neue Faelle waren vor der Umsetzung rot; Engine.Html.Tests besteht mit
7.561 Tests, Solution-Build, Formatverifikation und Volltest (17.557 Tests,
zwei bedingte Browser-Skips) bestehen. Commit `87b779b` ist lokal und nicht
gepusht. Siehe
[KP-591](./Kontextprotokoll.md#kp-591---gewoehnliche-phrasierungselemente-in-tabellenmodi).

**Weitere gewoehnliche Tags in Tabellenmodi (2026-11-21; KP-592/593; lokal committed):**
Andere nicht-spezielle Namen, darunter unbekannte und Custom-Element-Namen,
nutzen denselben begrenzten Foster-Parent-/In-Body-Pfad in vier Tabellenmodi.
Custom-Element-Reaktionen und SVG bleiben unsupported. 13 neue Faelle waren
vorher rot; Engine.Html.Tests besteht mit 7.576 Tests, der Solution-Build, die
Formatverifikation und der spaetere Volltest bestehen. Commit `c41afaa` ist
lokal und nicht gepusht. Siehe
[KP-592](./Kontextprotokoll.md#kp-592---phrasierungselemente-committed-weitere-gewoehnliche-tags).

**Template-Formulare (2026-11-21; KP-593/594; lokal committed):**
Formularstarts in Template-Inhalt und Template-Kontextfragmenten ignorieren und
erhalten den aeusseren Formularzeiger. In-body-Endtags schliessen ein in-scope
Formular; in-table-Starts werden eingefuegt und gepoppt, ohne den Zeiger zu
setzen. Drei fokussierte Faelle waren vorher rot; Engine.Html.Tests besteht mit
7.581 Tests, Solution-Build, Formatverifikation und der spaetere
Solution-Volltest bestehen. DOM-Form-Owner- und Script-APIs bleiben offen;
Commit `2ec8853` ist lokal und nicht gepusht. Siehe
[KP-593](./Kontextprotokoll.md#kp-593---template-formulare-und-formularzeiger).

**Formularzeiger bei Endtags (2026-11-21; KP-594/595; lokal committed):**
Ausserhalb von Template-Inhalt loescht ein Formularendtag den Zeiger vor der
Scope-Pruefung. Nicht in-scope befindliche Formulare bleiben auf dem Stack;
spaetere Starts werden nicht mehr durch den veralteten Zeiger ignoriert.
Fuenf fokussierte Faelle waren vorher rot; Engine.Html.Tests besteht mit 7.586
Tests, Solution-Build und Formatverifikation bestehen. Der anschliessende
Solution-Volltest bestand mit zwei bedingten Browser-Skips; Commit `e6eb8b6`
ist lokal und nicht gepusht.
Siehe
[KP-594](./Kontextprotokoll.md#kp-594---formularzeiger-vor-scope-pruefung-loeschen)
und [KP-595](./Kontextprotokoll.md#kp-595---formatierung-bei-tabellentext-rekonstruieren).

**Tabellentext und Formatierung (2026-11-21; KP-595/596; lokal committed):**
Nicht-Whitespace-Tabellentext rekonstruiert bei Bedarf aktive Formatierung
ueber den bestehenden Foster-Einfuegepfad und fuegt den Text im
rekonstruierten aktuellen Knoten ein. Whitespace-only bleibt unveraendert.
Dokument-, Template- und Fragmenttests sowie die Knotengrenze sind abgedeckt;
Adoption-Reparenting mit Foster-Parenting bleibt unsupported. Vier neue
gezielte Tests scheiterten vorher; fuenf fokussierte Tests und 7.590 HTML-Tests
bestehen. Solution-Build und Formatpruefung bestehen; der anschliessende
Solution-Volltest bestand mit zwei bedingten Browser-Skips. Commit `c5efe9c`
ist lokal und nicht gepusht. Siehe
[KP-595](./Kontextprotokoll.md#kp-595---formatierung-bei-tabellentext-rekonstruieren)
und [KP-596](./Kontextprotokoll.md#kp-596---verschachtelte-tabellen-in-captions).

**Verschachtelte Tabellen in Captions (2026-11-21; KP-596/597; lokal committed):**
Tabellenstarts in Captions verwenden die bestehenden begrenzten In-body-Regeln;
nach Schliessen der inneren Tabelle wird der Caption-Modus wiederhergestellt.
Dies gilt auch fuer Template-Inhalt und Caption-Kontextfragmente. Drei gezielte
Faelle scheiterten vorher; vier fokussierte Faelle und 7.592 HTML-Tests
bestehen. Der Solution-Build besteht ohne Warnungen und Fehler; die
Formatpruefung besteht. Der anschliessend verstaerkte Fragmenttest besteht
separat; der Solution-Volltest bestand mit zwei bedingten Browser-Skips.
Commit `2fa79a1` ist lokal und nicht gepusht. Siehe
[KP-596](./Kontextprotokoll.md#kp-596---verschachtelte-tabellen-in-captions)
und [KP-597](./Kontextprotokoll.md#kp-597---adoption-mit-table-foster-parenting).

**Adoption mit Table-Foster-Parenting (2026-11-21; KP-597/598; lokal committed):**
Die begrenzte Adoption-Recovery fuegt ihre umgehaengten Knoten bei aktivem
Foster-Parenting ueber den bestehenden Foster-Einfuegepfad ein, wenn der
gemeinsame Vorfahr tabellenbezogen ist. Bei mehreren Blockelementen waehlt die
Recovery das erste spezielle Element unterhalb der Formatierung auf dem Stack.
Dokument-, Template- und Fragmentfaelle sowie innere Formatierung und Knotengrenzen sind abgedeckt; vollstaendige
HTML-Konformitaet bleibt offen. Fuenf neue Faelle scheiterten vorher; der
HTML-Testlauf besteht mit 7.598 Tests. Solution-Build und Formatpruefung
bestehen; der anschliessende Solution-Volltest bestand mit zwei bedingten
Browser-Skips. Commit `2ab446b` ist lokal und nicht gepusht. Siehe
[KP-597](./Kontextprotokoll.md#kp-597---adoption-mit-table-foster-parenting)
und [KP-598](./Kontextprotokoll.md#kp-598---fehlverschachtelte-form-endtags).

**Fehlverschachtelte Form-Endtags (2026-11-21; KP-598; lokal in Arbeit):**
Ausserhalb von Template-Inhalt prueft das Endtag den konkreten Formularzeiger
auf Scope, erzeugt implizite Endtags und entfernt nur dieses Formular aus dem
Stack. Weitere offene Nachfahren bleiben bestehen; Fehlverschachtelung
erzeugt eine Diagnose. Dokument- und Fragmenttests sowie Ruby-Endtags sind
abgedeckt. Adoption-Agency-Entfernung des Formularzeigers und DOM-Form-Owner-
APIs bleiben offen. Drei neue Faelle scheiterten vorher; 7.600
HTML-Tests bestehen. Solution-Build ohne Warnungen/Fehler und Formatpruefung
bestehen; der Solution-Volltest besteht mit 17.596 erfolgreichen Tests und
zwei bedingten Browser-Skips. Commit `d4c2c1c5` ist lokal und nicht gepusht.
Siehe [KP-598](./Kontextprotokoll.md#kp-598---fehlverschachtelte-form-endtags).

**Form-Zeiger nach implizitem Stack-Pop (2026-11-22; KP-599; lokal committed):**
Normale In-Body-Endtags koennen eine offene Form aus dem Stack entfernen,
ohne den getrennten Form-Zeiger zu loeschen. Weitere Form-Starts bleiben bis
zum naechsten Form-Endtag ignoriert. Dokument- und Fragmenttests decken Baum,
Diagnostik und Dokumentzugehoerigkeit ab. Adoption-Agency-Entfernung des
Form-Zeigers bleibt unsupported. Zwei neue Tests scheiterten vorher; 7.600
HTML-Tests bestehen. Solution-Build ohne Warnungen/Fehler, Volltest mit 17.596
Erfolgen und zwei bedingten Browser-Skips, Formatpruefung und `git diff --check`
bestehen. Kein Push angefordert. Siehe
[KP-599](./Kontextprotokoll.md#kp-599---formularzeiger-nach-implizitem-stack-pop).

**Head-/Body-Tags in Tabellenmodi (2026-11-22; KP-600; lokal committed):**
Body- und Head-Starts sowie unmatched Head-Endtags in Table-, TableBody- und
Row-Modi werden ueber den fostered In-Body-Pfad verarbeitet. So werden
Body-Attribute in das vorhandene Body-Element uebernommen; Head-Starts bleiben
ignoriert und Head-Endtags erhalten die In-Body-Unmatched-Endtag-Diagnostik.
Andere nicht implementierte Table-Recovery bleibt explizit unsupported.
Drei Regressionen scheiterten zuvor mit `UnsupportedHtmlException`; sie und
alle 7.603 Engine.Html-Tests bestehen. Solution-Build (null Warnungen/Fehler),
Volltest (17.599 Erfolge, zwei bedingte Browser-Skips),
Formatverifikation und `git diff --check` bestehen. Der Supportwert bleibt
unveraendert; kein Push angefordert. Siehe
[KP-600](./Kontextprotokoll.md#kp-600---head-und-body-tags-in-tabellenmodi).

**Template-Inhalt innerhalb Selects (2026-11-22; KP-601; lokal validiert):**
Template-Starts werden nun auch verarbeitet, wenn ein Select offen ist.
Waehrend ein Template im Stack liegt, leitet der Parser Tokens nicht mehr in
die Select-spezifische Verarbeitung des darunterliegenden Select-Elements;
Template-Inhalt folgt dem begrenzten In-Body-/Template-Verhalten. Beim
Schliessen des Templates leitet die Tokenverarbeitung wieder an das umgebende
Select-Element weiter. Das gilt auch fuer Select-Kontext-Fragmente. Dokument-
und Fragmentregressionen pruefen
einen Template-Inhalt mit `input`/`option` und eine nachfolgende Option im
Select. Die HTML-Schaetzung bleibt unveraendert. Engine.Html.Tests besteht mit
7.604 Tests; Solution-Build ohne Warnungen/Fehler und Volltest mit 17.600
Erfolgen und zwei bedingten Browser-Skips bestehen; Formatverifikation und
`git diff --check` bestehen. Andere Select-/Template-Algorithmen bleiben
unsupported; kein Push angefordert. Siehe
[KP-601](./Kontextprotokoll.md#kp-601---template-inhalt-innerhalb-selects).

**HTML-Tabellen-`br`/`hr`-Endtags (2026-11-22; KP-602; lokal umgesetzt):**
Endtags fuer `br` und `hr` in Table-, TableBody- und Row-Modi laufen jetzt
ueber fostered In-Body-Recovery. `</br>` wird wie ein `br`-Starttag verarbeitet
und vor der naechsten Tabelle eingefuegt; ein unmatched `</hr>` erzeugt kein
Element. Sechs Regressionen scheiterten vorher am expliziten unsupported
Foster-Parenting-Pfad und bestehen nach der Aenderung; alle 7.610
Engine.Html-Tests bestehen. Der Solution-Build besteht ohne Warnungen/Fehler;
der Volltest besteht mit 17.606 Tests und zwei bedingten Browser-Skips.
Formatverifikation und `git diff --check` bestehen. Andere Table-Tokens bleiben
unsupported; das Inkrement ist lokal und wird nicht gepusht. Siehe
[KP-602](./Kontextprotokoll.md#kp-602---brhr-endtags-in-tabellenmodi).

**Inerte Embedded-Tag-Recovery (2026-11-22; KP-603; lokal committed):**
`applet`, `marquee` und `object` nutzen jetzt begrenzte In-Body-Start-/Endtag-
Recovery einschliesslich Rekonstruktion aktiver Formatierung und Marker-
bereinigung. Table-, TableBody- und Row-Modi leiten beide Tag-Arten an den
fostered In-Body-Pfad. 18 Regressionen decken die Dokumentverarbeitung,
unmatched Endtags, Markerbereinigung und alle drei Tabellenmodi ab.
Engine.Html.Tests besteht mit 7.628 Tests; Solution-Build ohne Warnungen/Fehler,
Volltest mit 17.624 Erfolgen und zwei bedingten Browser-Skips,
Formatverifikation und `git diff --check` bestehen. Fragmentkontexte,
Pluginverhalten und externe Ressourcen bleiben unsupported. Dieser begrenzte
statische DOM-Pass ist als `616a950f` lokal committed und nicht gepusht. Siehe
[KP-603](./Kontextprotokoll.md#kp-603---inerte-embedded-tag-recovery).

**Generischer Tabellen-Endtag-Fallback (2026-11-22; KP-604; validiert):**
Nicht anderweitig behandelte Endtags in Table-, TableBody- und Row-Modi folgen
jetzt der generischen Foster-Parent-/In-Body-Regel des HTML-Standards, anstatt
fuer einige spezielle Namen mit `UnsupportedHtmlException` abzubrechen.
Unimplementierte Starttag-Algorithmen und Fremdinhalte bleiben unsupported.
Zwölf Regressionen fuer `head`, `script`, `style` und `svg` ueber alle drei
Modi bestehen; neun scheiterten vor der Korrektur. Engine.Html.Tests besteht
mit 7.640 Tests, Solution-Build ohne Warnungen/Fehler, Volltest mit 17.636
Erfolgen und zwei bedingten Browser-Skips; Formatverifikation und
`git diff --check` bestehen. Siehe
[KP-604](./Kontextprotokoll.md#kp-604---generischer-tabellen-endtag-fallback).

**Aktueller HTML-Pass: Quirks-Mode-Tabellenstart (2026-11-22; KP-607; abgeschlossen):**
Ein `table`-Starttag im In-Body-Modus schliesst einen in-scope Absatz nur
ausserhalb des Quirks-Modus; im Quirks-Modus bleibt der Absatz offen. Die drei
fokussierten No-Quirks/Limited-Quirks/Quirks-Faelle bestehen. Engine.Html.Tests
besteht mit 7.649 Tests; Debug-/Release-Builds, Volltestlaeufe (je 17.645
Erfolge und zwei bedingte Browser-Skips) und Formatverifikation bestehen.
Commit `332e510f` ist auf `origin/main`; Linux, Windows, CodeQL und Dev
Environment Prebuild CI bestehen. Die HTML-Supporteinschaetzung bleibt
unveraendert. Details und Provenienz: [KP-607](./Kontextprotokoll.md#kp-607---quirks-mode-tabellenstart).

**Aktueller HTML-Pass: `noscript`-Dokumentparsing (2026-11-23; KP-608; committed und gepusht):**
Der Dokumentparser behandelt `noscript` scripting-abhaengig: Ohne
Script-Completion-Callback gilt Scripting als deaktiviert; im Head wird der
eigene In-Head-Noscript-Modus verwendet, im Body normale In-Body-Verarbeitung.
Mit Callback wird RAWTEXT fuer `noscript` im Head und Body verwendet. Nach
`</head>` bleibt `noscript` im In-Body-Pfad. `noscript`-Fragmentkontexte
bleiben explizit unsupported; volle Scriptvorbereitung ist nicht Teil dieses
Inkrements. Fuenf Regressionen, Engine.Html.Tests (7.653), Debug-Solution-
Build (0 Warnungen/Fehler), Volltest (17.649 bestanden, zwei bedingte
Browser-Skips), Formatverifikation und Diff-Check bestehen. Commit
`dfad4bdb` ist auf `origin/main`. Linux development
([45089958935](https://github.com/khmseu/VisualWeb/actions/runs/45089958935)),
Windows confinement
([45089958705](https://github.com/khmseu/VisualWeb/actions/runs/45089958705)),
CodeQL
([45089958469](https://github.com/khmseu/VisualWeb/actions/runs/45089958469))
und Dev Environment Prebuilds
([45089958344](https://github.com/khmseu/VisualWeb/actions/runs/45089958344))
bestehen.
Die HTML-Supporteinschaetzung bleibt unveraendert. Siehe
[HTML/DOM-Guide](../docs/html-dom.md),
[Missing.md](../plans/Missing.md) und
[KP-608](./Kontextprotokoll.md#kp-608---noscript-dokumentparsing).

**HTML-Pass: `noscript`-Fragmentkontext (2026-11-23; KP-609; committed und gepusht):**
`HtmlParser.ParseFragment` akzeptiert nun `noscript` als Kontext. Da die
Fragment-API keinen scripting-enabled Modus exponiert, bleibt der Tokenizer im
standardgemaessen Data-Zustand fuer deaktiviertes Scripting; `noscript`-Inhalt
wird als begrenztes In-Body-Fragment geparst. Keine Scriptvorbereitung,
Scriptbindings oder `innerHTML`-Unterstuetzung. Die Regression scheiterte vor
der Aenderung am Unsupported-Kontext-Guard und besteht danach; Engine.Html
7.653/7.653, Solution-Build ohne Warnungen/Fehler, Volltests 17.649 bestanden
mit zwei bedingten Browser-Skips, Formatverifikation und Diff-Check bestehen.
Commit `7a236353` ist auf `origin/main`. Die CI-Workflows waren beim letzten
Check noch nicht in `gh run list` sichtbar; beim Folgecheck liefen Linux
development, Windows confinement, Dev Environment Prebuild und CodeQL. Alle
vier Laeufe bestehen: Linux
([45097056193](https://github.com/khmseu/VisualWeb/actions/runs/45097056193)),
Windows
([45097056239](https://github.com/khmseu/VisualWeb/actions/runs/45097056239)),
Prebuild
([45097055369](https://github.com/khmseu/VisualWeb/actions/runs/45097055369))
und CodeQL
([45097055350](https://github.com/khmseu/VisualWeb/actions/runs/45097055350)).
Der zuvor offene Dev-Environment-Prebuild fuer `a337155a` besteht ebenfalls
(45092434125). Die HTML-Supporteinschaetzung bleibt unveraendert. Siehe
[HTML/DOM-Guide](../docs/html-dom.md),
[Missing.md](../plans/Missing.md) und
[KP-609](./Kontextprotokoll.md#kp-609---noscript-fragmentkontext).

**Aktueller HTML-Pass: statische Frameset-Dokumente (2026-11-23; KP-610; validiert, gepusht und CI bestanden):**
Die Dokument-Tree-Construction unterstuetzt nun die Frameset-Modi fuer
verschachtelte `<frameset>`/`<frame>`-Knoten, `frameset-ok`-Gating,
`noframes`-RAWTEXT sowie die Modi nach dem Frameset. Frames erzeugen weiterhin
keine Browsing-Kontexte, laden keine Ressourcen und werden nicht gerendert;
Frameset-/Frame-Fragmentkontexte bleiben explizit unsupported. Die Auswahl
dieses konkreten Inkrements folgt dem dokumentierten Parser-Support-Gap in
[Missing.md](../plans/Missing.md); eine separate Nutzerentscheidung zu genau
diesem Verhalten ist nicht dokumentiert.
Der lokale HTML-Standardcache hatte `checkedAt: 2026-11-07` und lag am
Dokumentationsdatum innerhalb des 30-Tage-Fensters. Engine.Html.Tests besteht
mit 7.664 Tests, der Solution-Build mit null Warnungen/Fehlern und
Format-/Diff-Checks bestehen; der finale Solution-Volltest besteht ebenfalls
mit 17.660 Tests und zwei bedingten Browser-Prozess-Test-Skips.
Die grobe HTML-Supporteinschaetzung bleibt unveraendert.
Der zuvor offene Dev-Environment-Prebuild fuer Kontextcommit `c2a4f739`
(`45099518203`) ist inzwischen erfolgreich abgeschlossen. Implementierungscommit
`4738d14f` ist gepusht; Kontextcommit `cfc0881f` haelt die Resultate fest.
Linux development, Windows confinement, CodeQL und Dev Environment Prebuilds
bestehen fuer `cfc0881f`. Siehe
[HTML/DOM-Guide](../docs/html-dom.md),
[Missing.md](../plans/Missing.md) und
[KP-610](./Kontextprotokoll.md#kp-610---statische-frameset-dokumente).

**Vorheriger HTML/DOM-Pass: nativer Form-Owner (2026-11-23; KP-611; committed, gepusht und CI-geprueft):**
`DomFormControls.FormOwner` berechnet den aktuellen Besitzer fuer die sieben
eingebauten gelisteten Form-assoziierten Elemente aus Baum und Attributen.
Bei verbundenen Elementen mit `form`-Attribut entscheidet die erste ID im
Baum; fehlt sie oder ist sie kein `form`, ist der Besitzer null. In allen
anderen Faellen gilt das naechste Form-Element im Ahnenpfad.
Parser-insertierte Assoziation,
form-assoziierte Custom Elements und scriptseitige APIs bleiben unsupported.
Der Browser-Form-Snapshot blieb unveraendert. Auswahl und Umfang sind aus dem
expliziten Restpunkt in [Missing.md](../plans/Missing.md) abgeleitet, nicht
separat vom Nutzer bestaetigt. Der HTML-Cache hatte `checkedAt: 2026-11-07`
und war am Dokumentationsdatum frisch. Die 75 Engine.Dom-Tests, der
Solution-Build (0 Warnungen/Fehler), der Volltest (17.671 bestanden, 2
bedingte Browser-Skips) und Formatverifikation bestehen. Commit `0c87d7c4` ist
auf `origin/main`; Linux development, Windows confinement, CodeQL und Dev
Environment Prebuilds bestehen. Siehe [HTML/DOM-Guide](../docs/html-dom.md),
[Engine.Dom README](../src/Engine/Engine.Dom/README.md) und
[KP-611](./Kontextprotokoll.md#kp-611---nativer-form-owner).

**Vorheriger HTML-Pass: `noscript` in Tabellenmodi (2026-11-23; KP-612; committed, gepusht und CI-geprueft):**
`noscript`-Starttags in Table-, ColumnGroup-, TableBody- und Row-Modi verwenden
nun den bestehenden Foster-Parent-/In-Body-Pfad. Bei deaktiviertem Scripting
nutzen Endtags denselben Pfad und Markup wird normal verarbeitet; bei
aktiviertem Scripting beginnt der Start RAWTEXT und das passende Endtag kehrt
zum urspruenglichen Tabellenmodus zurueck. Frameset-/Frame-Fragmentkontexte
aus der frueheren ausdruecklichen
Fragmentumfangsentscheidung bleiben unveraendert unsupported.
Die Auswahl ist aus den verbleibenden explizit fehlenden Tabellen-Startregeln
in [Missing.md](../plans/Missing.md) abgeleitet, nicht separat vom Nutzer
bestaetigt. Der HTML-Cache hatte `checkedAt: 2026-11-07` und war am
Dokumentationsdatum frisch. Engine.Html.Tests besteht mit 7.669 Tests in
Debug und Release. Debug- und Release-Solution-Builds bestehen mit 0
Warnungen/Fehlern (vor der letzten reinen Testergaenzung). Der finale
sequenzielle Debug-Solutiontest besteht mit 17.676 Tests und zwei bedingt
uebersprungenen Browserfaellen; der Release-Solutiontest bestand zuvor mit
17.675 Tests und zwei bedingten Skips, vor der ColumnGroup-Regression.
Format- und Diff-Checks bestehen. Die grobe HTML-Schaetzung bleibt
unveraendert.
Der erste parallele Volltestlauf meldete vier Browser-Prozessfehler; diese
traten in den sequenziellen Wiederholungen nicht auf. Implementierungscommit
`e64ec0d9` ist auf `origin/main`. Linux development (Run
`45129422198`), Windows confinement (`45129422114`), CodeQL
(`45129421363`) und Dev Environment Prebuilds (`45129437476`) bestehen.
Siehe
[HTML/DOM-Guide](../docs/html-dom.md), [Missing.md](../plans/Missing.md) und
[KP-612](./Kontextprotokoll.md#kp-612---noscript-in-tabellenmodi).

**Aktueller HTML-Pass: `frame`/`frameset` in Tabellenmodi (2026-11-23; KP-613; committed, gepusht, CI teilweise bestanden):**
Starttags fuer `frame` und `frameset` in Table-, TableBody- und Row-Modi
verwenden jetzt das generische foster-parented In-Body-Fallback. ColumnGroup
schliesst `colgroup` und reprocessiert diese Tags in Table. Das erstellt weder
Browsing-Kontexte noch Frame-Loads oder Rendering; `frameset` folgt lediglich
den In-Body-/`frameset-ok`-Regeln. Die Auswahl ist aus den noch explizit
unsupported Table-Starttags in [Missing.md](../plans/Missing.md) abgeleitet,
nicht separat bestaetigt. Der lokale HTML-Cache hatte `checkedAt: 2026-11-07`
und ist frisch. Engine.Html.Tests besteht mit 7.671 Tests in Debug und Release.
Debug- und Release-Solution-Builds bestehen ohne Warnungen/Fehler; beide
sequenziellen vollstaendigen Solution-Testlaeufe bestehen mit 17.678 Tests und
zwei bedingt uebersprungenen Browserfaellen. Formatverifikation und
`git diff --check` bestehen. Die HTML-Schaetzung bleibt unveraendert. Commit
`5559aabf` ist auf `origin/main`; Linux development (`45133963363`), Windows
confinement (`45133963297`) und CodeQL (`45133962930`) bestehen; Dev
Environment Prebuilds (`45133963481`) laufen noch. Siehe
[HTML/DOM-Guide](../docs/html-dom.md),
[Missing.md](../plans/Missing.md) und
[KP-613](./Kontextprotokoll.md#kp-613---frameframeset-in-tabellenmodi).

**Embedded-Fragmentkontexte (2026-11-22; KP-605; validiert):**
`applet`, `marquee` und `object` sind jetzt zusaetzlich als statische
Fragmentkontexte fuer die vorhandene begrenzte In-Body-Verarbeitung zugelassen.
Je drei Regressionen pruefen Elementverschachtelung/Ownership und dass das
Kontextelement nicht auf dem Open-Elements-Stack liegt; alle sechs scheiterten
vorher am Unsupported-Kontext-Guard. Engine.Html.Tests besteht mit 7.646 Tests,
Debug-Volltest mit 17.642 Erfolgen und zwei bedingten Browser-Skips,
Release-Volltest mit denselben Ergebnissen, Debug-/Release-Builds und
Formatverifikation bestehen. Commit `38fbc16f` ist auf `origin/main`;
Linux, Windows und CodeQL CI sind erfolgreich. Der separate Dev-Environment-
Prebuild laeuft noch. Plugin-/Ressourcenverhalten bleibt unsupported. Siehe
[KP-605](./Kontextprotokoll.md#kp-605---embedded-fragmentkontexte).
CI-Details: [KP-606](./Kontextprotokoll.md#kp-606---commit-push-und-ci).

**Label-Foster-Parenting in Tabellenmodi (2026-11-21; KP-589/590; gepusht):**
`label`-Starts und Endtags nutzen den begrenzten Foster-Parent-/In-Body-Pfad
in Table-, TableBody-, Row- und ColumnGroup-Kontexten; unmatched Endtags
erhalten die bestehende Recovery-Diagnostik. Acht fokussierte Faelle waren
vorher rot und bestehen nach der Umsetzung; Engine.Html.Tests besteht mit
7.512 Tests, der Solution-Build ohne Warnungen/Fehler und die
Formatverifikation. Ein erster Volltest scheiterte mit zwei Browser-V8-
Deadlinefehlern bei gleichzeitiger Formatverifikation; der isolierte Nachtest
beider Methoden und der erneute Volltest (17.508 Tests, zwei bedingte Skips)
bestehen. Die Ursache der ersten Deadlinefehler ist nicht nachgewiesen. Commit
`9d64258` liegt inzwischen auf `origin/main`. Keine Form-APIs werden neu bereitgestellt.
Siehe [KP-589](./Kontextprotokoll.md#kp-589---label-in-tabellenmodi) und
[KP-590](./Kontextprotokoll.md#kp-590---label-inkrement-committed).

**Ruby-Foster-Parenting in Tabellenmodi (2026-11-21; KP-588; validiert und gepusht):**
`ruby`, `rb`, `rtc`, `rp` und `rt` nutzen mit Start- und Endtags den
vorhandenen begrenzten Foster-Parent-/In-Body-Pfad in Table-, ColumnGroup-,
TableBody- und Row-Modi. Vier Modus-Regressionen pruefen die Annotationen und
den bestehenden Ruby-Parserpfad; Ruby-spezifisches Styling/Layout bleibt
unsupported. Engine.Html.Tests (7.504), Solution-Build, Volltest (17.500
bestanden, zwei bedingte Browser-Skips), Formatverifikation und Diff-Check
bestehen. Commit `bca3299` enthaelt unbeabsichtigt auch zuvor gestagte
Kontextaenderungen und liegt auf `origin/main`. Windows, CodeQL und Prebuild
bestehen; Linux Trunk scheitert an 28 vorhandenen Markdownlint-Befunden in
unveraenderten Plan-/Core-Testdateien. Siehe
[KP-588](./Kontextprotokoll.md#kp-588---ruby-annotationen-in-tabellenmodi).

**Formatting-Elemente in Tabellenmodi (2026-11-20; KP-586; committed und gepusht):**
Formatierungselemente aus der vorhandenen `FormattingTags`-Menge nutzen nun
mit ihren Start- und Endtags in Table-, ColumnGroup-, TableBody- und Row-Modi
den begrenzten In-Body-/Foster-Parent-Pfad. Fuenf neue Modus-/Recoveryfaelle
waren vor der Umsetzung rot. Komplexes Adoption-Reparenting, das Table-Foster-
Parenting benoetigt, bleibt explizit unsupported. Engine.Html.Tests besteht
mit 7.495 Tests; Solution-Build (0 Warnungen/Fehler), Volltest (17.491
bestanden, zwei bedingte Browser-Skips), Formatverifikation und Diff-Check
bestehen. Commit `9620879` ist gepusht. Linux x64/arm64, Windows, CodeQL und
Prebuild bestehen; Linux Trunk scheitert ausschliesslich an vorhandenen
Markdownlint-Befunden in unveraenderten Plan-/Core-Testdateien. Siehe
[KP-586](./Kontextprotokoll.md#kp-586---formatting-elemente-in-tabellenmodi).

**Anchor-Foster-Parenting in Tabellenmodi (2026-11-20; KP-585; gepusht):**
`a`-Start-/Endtags nutzen in Table-, ColumnGroup-, TableBody- und Row-Modi
den begrenzten In-Body-/Foster-Parent-Pfad, einschliesslich der vorhandenen
Nested-Anchor-Recovery. Vier Modusfaelle, ein Nested-Anchor-Fall und ein
Negativfall fuer Adoption-Reparenting mit Table-Foster-Parenting pruefen den
Umfang; letzteres bleibt explizit unsupported. Engine.Html.Tests besteht mit
7.492 Tests, der Solution-Build mit 0 Warnungen/Fehlern und der serielle
Solution-Volltest mit 17.488 Tests und zwei bedingten Browser-Skips.
Formatverifikation und Diff-Check bestehen. Commit `38bbf1d` ist gepusht;
Linux x64/arm64, Windows, CodeQL und Prebuild bestehen. Nur Linux Trunk
scheitert an Markdownlint-Befunden in unveraenderten Plan-/Core-Testdateien;
keine geaenderte Datei ist betroffen. Der HTML-Supportwert bleibt unveraendert.
Siehe [KP-585](./Kontextprotokoll.md#kp-585---anchor-foster-parenting).

**Option/Optgroup-Foster-Parenting in Tabellenmodi (2026-11-20; KP-584; gepusht):**
`option`- und `optgroup`-Start-/Endtags nutzen in Table-, ColumnGroup-,
TableBody- und Row-Modi den vorhandenen begrenzten In-Body-/
Foster-Parent-Pfad. Vier Regressionstests decken diese Modi, verschachtelte
Option-Struktur und Foster-Diagnostik ab; der erste Testlauf schlug mit
`UnsupportedHtmlException` fehl. Die Engine.Html-Suite besteht mit 7.487
Tests, der Solution-Build mit 0 Warnungen/Fehlern und der serielle
Solution-Volltest mit 17.483 bestandenen Tests und zwei bedingten
Browser-Skips. Formatverifikation und Diff-Check bestehen. Commit `99ae4ca`
ist gepusht; Linux x64/arm64, Windows x64/arm64, CodeQL und Prebuild bestehen.
Nur Linux Trunk scheitert an denselben 28 Markdownlint-Befunden in
unveraenderten Plan-Dateien und `tests/Core.Tests/Data/README.md`; keine
geaenderte Datei ist betroffen.
Der grobe HTML-Wert bleibt ~20%; andere Foster-Parent-Algorithmen und
formatting-element-Reparenting mit Table-Foster-Parenting bleiben unsupported.
Der Dokumentations-Follow-up `3855f05` fuer KP-583 hat erfolgreiche Windows-,
CodeQL- und Prebuild-CI; Linux Trunk meldet 28 Markdownlint-Befunde in
unveraenderten Plan-/Core-Testdateien. Siehe
[KP-584](./Kontextprotokoll.md#kp-584---optionoptgroup-foster-parenting).

**URL-IDL-Legacy-Alias (2026-11-19; KP-509; Beschluss umgesetzt):** Der
Nutzer stimmte der minimalen Window-Global-Integration fuer
`[LegacyWindowAlias=webkitURL]` zu. Nur die explizit aktivierten
PageRendering-Seitenhosts setzen `window === globalThis` und
`webkitURL === URL`; Hosts ohne Opt-in und Hosts mit bloss gebundenem Dokument
bleiben unveraendert. Das vollstaendige `Window`-Interface, weitere globale
Browser-APIs und Window/load-Verhalten sind nicht implementiert. Belegt sind
der Solution-Build (0 Warnungen/Fehler), 392 Engine.Scripting-Tests und 1.375
VisualWeb.Browser-Tests; zwei CPU-Drucktests wurden bedingt uebersprungen.
Auch `dotnet test VisualWeb.slnx --no-build`, Formatverifikation und
`git diff --check` bestanden. Commit `83a42d3` enthaelt die Aenderung; er wurde
nicht gepusht. Der WHATWG-URL-Support bleibt ein dokumentierter Teilumfang,
keine Vollkonformitaetsbehauptung.

**URL-Fortsetzung nach Legacy-Alias (2026-11-19; KP-511):** Der genehmigte
Umfang fuer Core-URL und browserseitige `URL`/`URLSearchParams` einschliesslich
interface-spezifischem Web IDL ist umgesetzt und validiert, aber kein
Vollkonformitaetsnachweis. Der gezielte Folgecheck fand keinen neuen
spec-/WPT-belegten Produktionsfehler. Der lokale URL-Algorithmus fuer Blob-
Origins beschraenkt den Pfad-URL-Fallback explizit auf HTTP, HTTPS und FILE;
das bestehende opaque Verhalten fuer FTP, WS und WSS ist daher kein Defekt.
Als dokumentierte Restbereiche bleiben der Blob-URL-Store (File API), die
vollstaendige Dokument-Basis-URL-Algorithmik (insbesondere `srcdoc` und der
volle frozen-base lifecycle) sowie weitere
HTML-Linkeigenschaften und vollstaendiges Web IDL. Opt-in PageRendering-Hosts
stellen die begrenzten `document.URL`/`documentURI`, `Node.baseURI` und
`<a>`/`<area>`-URL-Komponenten aus KP-513 bis KP-515 bereit. Die groben
HTML-/DOM-Prozentwerte bleiben unveraenderte Heuristiken. Siehe Q-039.

**Standardsinventar-Pruefung (2026-11-19; KP-512):** Die Eintraege unter
„Implemented subsets“ wurden gegen die aktuellen Core-, Networking-, HTML/DOM-,
CSS-, Text/Layout-, Scripting- und Browser-Shell-Leitfaeden geprueft. In den
beschriebenen Umfaengen wurde kein veralteter Implementierungsanspruch
festgestellt; daher bleibt `docs/standards.md` unveraendert. Auch die groben
Prozentwerte wurden beibehalten: Der Audit lieferte keine standardisierte
Messmethode oder neue quantifizierbare Evidenz fuer eine Neuberechnung. Der
URL-Legacy-Alias aus `83a42d3` ist bereits korrekt im URL-Eintrag enthalten.

**DOM-Dokument-URL (2026-11-19; KP-513):** Opt-in PageRendering-Script-
Hosts erhalten jetzt die finale `LoadedPage.Url` als readonly
`document.URL` und `document.documentURI`. Plain Hosts ohne gebundene URL
bleiben unveraendert. `Node.baseURI`, HTML-`<base>`-Aufloesung und
Anchor/Area-URL-Reflection sind nicht Teil dieses Inkrements. Commit `cf94354`
enthaelt Code, Tests, Leitfaeden und Kontextnotiz. Die `dom`-Scopebeschreibung
wurde angepasst; die grobe Prozentheuristik blieb unveraendert. KP-514 erweitert
den Umfang anschliessend um eine begrenzte `Node.baseURI`-Bindung.

**DOM-Node.baseURI (2026-11-19; KP-514):** Opt-in PageRendering-Script-Hosts
binden `Node.baseURI` fuer Document- und Node-Facades ein. Der Getter
berechnet den aktuellen ersten HTML-`<base href>`-Kandidaten in Baumreihenfolge
gegen die finale Seiten-URL neu und wendet die Browser-Freigabe an; bei einem
nicht passenden freigegebenen Ziel faellt er auf die Seiten-URL zurueck.
Unterstuetzte href-Mutationen sind beim naechsten Zugriff sichtbar. Volle
frozen-base, inherited `about:blank`/`srcdoc`- und Anchor/Area-Algorithmen
blieben zu diesem Zeitpunkt offen.
Commit `8e131e8` enthaelt die Aenderung. Die DOM-Teilbereichsbeschreibung wurde
erweitert; der grobe Prozentwert blieb unveraendert.

**HTML-Hyperlink-URL-API (2026-11-19; KP-515):** Die HTML/DOM-Fortsetzung
ergaenzt fuer die explizit opt-in-faehigen PageRendering-Hosts eine begrenzte
`HTMLAnchorElement`-/`HTMLAreaElement`-URL-Oberflaeche: `href` als
USVString-Stringifier, `toString()`, readonly `origin` sowie `protocol`,
`username`, `password`, `host`, `hostname`, `port`, `pathname`, `search`
und `hash`.
Getter reparsen das aktuelle `href` gegen die bereits begrenzte dynamische
Dokumentbasis; beim Fehler gibt `href` den Rohwert und Komponenten geben ihre
standardgemaessen Leer-/Defaultwerte zurueck. Setters spiegeln gueltige
Core-URL-Komponentenmutation zurueck in das `href`-Attribut. Das vollstaendige
HTML-`set the url`-/frozen-base-Lebenszyklusmodell, weitere Linkattribute und
generiertes Web IDL bleiben ausserhalb des Umfangs. Der HTML- und DOM-
Prozentwert wurde mangels neuer Messgrundlage nicht geaendert.
**Verifikation vor Commit:** Solution-Build ohne Warnungen/Fehler, Engine.Scripting
(395 Tests), VisualWeb.Browser (1.381 Tests; zwei CPU-pressure-Faelle als
uebersprungen gemeldet), kompletter Solution-Testlauf, Formatverifikation und
`git diff --check` bestanden; auch der gezielte PageRendering-Hyperlink-Test
bestand. Siehe [KP-515](./Kontextprotokoll.md#kp-515---begrenzte-hyperlink-url-api-fuer-a-und-area).
Quellen: lokale [HTML HyperlinkElementUtils](../specs/cache/html.html#api-for-hyperlink-elements),
[Core URL](../docs/core.md#url) und [Standardsinventar](../docs/standards.md#implemented-subsets).

**HTML-Base-URL-Fortsetzung (KP-516; beschlossen):** Mit gezielten Tests
Kandidatenauswahl und URL-Aufloesung fuer `<base href>` zwischen Stylesheet-
Sammlung und scriptseitigem `Node.baseURI`/Hyperlink-Verhalten angleichen.
Die browserseitige Freigabepolicy bleibt unveraendert; vollstaendige
frozen/inherited-base-Semantik und weitere URL-Properties sind ausgeschlossen.
Die neue Regression belegte, dass scriptseitiges `Node.baseURI` zuvor
`data:`-/`javascript:`-Basen akzeptierte, waehrend Stylesheet-Ermittlung diese
verwarf; nun teilen beide Pfade dieselbe URL-Pruefung. Der fokussierte Test
bestand nach zuvor fehlschlagenden Faellen. Build, Formatpruefung, alle 1.383
Browser-Tests und 17.222 Solution-Tests bestanden; zwei CPU-pressure-Varianten
wurden als uebersprungen protokolliert.
Siehe [KP-516](./Kontextprotokoll.md#kp-516---html-base-url-aufloesung-angleichen).

**HTMLBaseElement.href (KP-517; begrenzte Bindung):** Geladene Hosts mit
Dokument-URL exponieren die Property nur auf HTML-`base`-Facades. Der Getter
parst das Attribut gegen die finale Seiten-URL (nicht die dynamische
Dokumentbasis), gibt bei Parsefehlern den Rohwert zurueck und liefert sonst
die serialisierte URL. Der Setter reflektiert USVString; wiederholte Reads
teilen das DOM-Textbudget. Frozen-base state und inherited-base-URL-Verhalten
bleiben deferred. Fokussierte Tests, 397 Engine.Scripting-Tests, Build und
Formatierung sowie alle 17.224 Solution-Tests bestehen. Ein erster parallel
belasteter Scripting-Lauf hatte einen Deadline-Timeout in einem URL-Control-
Case; Einzeltest und kompletter Folgelauf bestanden. Zwei CPU-pressure-
Varianten wurden als uebersprungen protokolliert. Details:
[KP-517](./Kontextprotokoll.md#kp-517---begrenzte-htmlbaseelementhref-bindung).
Commit `460cec9` enthaelt diese Aenderung; er wurde nicht gepusht.

**HTMLBaseElement.target (KP-518; bounded reflection):** Gebrandete HTML-
`base`-Facades reflektieren `target` als `DOMString`, auch ohne zugeordnete
Dokument-URL; ein fehlendes Attribut liest sich als leerer String. Dies
aktualisiert nicht die bereits erfassten Shell-Navigationsmetadaten. Die
grobe HTML-Coverage-Heuristik blieb unveraendert. 398 Engine.Scripting-Tests,
Solution-Build, Formatverifikation und 17.225 Solution-Tests bestehen; zwei
CPU-pressure-Varianten wurden uebersprungen. Details:
[KP-518](./Kontextprotokoll.md#kp-518---begrenzte-htmlbaseelementtarget-reflexion).
Commit `5bf30c1` enthaelt diese Aenderung; er wurde nicht gepusht.

**Hyperlink target (KP-519; DOMString-Reflexion):** Dokumentgebundene
Script-Hosts exponieren `target` auf `<a>` und `<area>` unabhaengig von einer
Dokument-URL. Fehlende Attribute lesen sich als leerer String; DOMString-
Codeeinheiten bleiben erhalten. Spaetere Skriptmutationen aktualisieren nicht
die bereits gesammelten Shell-Linkmetadaten. Die grobe HTML-Coverage-Heuristik
blieb unveraendert. 399 Engine.Scripting-Tests, Solution-Build,
Formatverifikation und 17.226 Solution-Tests bestehen; zwei CPU-pressure-
Varianten wurden uebersprungen. Details:
[KP-519](./Kontextprotokoll.md#kp-519---begrenzte-hyperlink-target-reflexion).
Commit `35a0890` enthaelt diese Aenderung; er wurde nicht gepusht.

**Inherited `about:blank`-Basis-URL umgesetzt (2026-11-19; KP-520):**
Page-created Blank-Dokumente tragen jetzt die effektive Basis-URL des
Initiators getrennt vom CSP-Override `LoadedPage.BaseUrl`. Renderer geben die
effektive Basis einschliesslich `<base href>` und begrenzter Parser-Script-
Mutationen lokal sowie ueber IPC v50 zurueck. Browsernavigation, Zielmodi,
Reload, Traversal und Same-Document-History behalten den Snapshot; UI-erzeugte
Blanks behalten opaque Origin und lokalen Fallback ohne Vererbung. Siehe
[KP-520](./Kontextprotokoll.md#kp-520---inherited-aboutblank-basis-url). Die
Implementierung ist lokal vollvalidiert. Feature-Commit `96d4305` enthaelt Code,
Tests und Leitfaeden; Kontext-Commit `ef4d3d9` enthaelt den Kontextnachtrag.
Beide Commits wurden nicht gepusht.

**GBK/gb18030-Streaming (2026-11-19; KP-521):** `WebTextDecoder` stuetzt jetzt
inkrementelles GBK/gb18030-Decoding gemaess dem gemeinsamen gb18030-Decoder,
einschliesslich 4-Byte-Sequenzen, Fehler-Reconsume, Flush und fatalem Reset.
Das folgt der Missing-Prioritaetsregel: Der begrenzte WHATWG-URL-Arbeitsvorrat
war bereits umgesetzt; Encoding (~55%) ist die naechste actionable Luecke nach
URL (~80%). Die grobe Encoding-Schaetzung bleibt unveraendert. Alle 4.236
Core-Tests bestehen in Debug und Release; Debug-/Release-Solution-Build und
Formatverifikation bestehen. Feature-Commit `b76f7e9` enthaelt Code, Tests und
Leitfaeden; er wurde nicht gepusht. Ein paralleler `dotnet test VisualWeb.slnx
--no-build`-Lauf hatte einen Deadline-Timeout im unveraenderten
`UrlComponentSettersMatchPinnedControlCharacterWptCases`; isoliert bestand der
Test und die gesamte Engine.Scripting-Suite (399). Browser (1.394) bestand mit
zwei bedingten CPU-pressure-Skips. Siehe [KP-521](./Kontextprotokoll.md#kp-521---incremental-gbkgb18030-textdecoder).

**ISO-2022-JP-TextDecoder-Streaming (2026-11-19; KP-522):** `WebTextDecoder`
bewahrt jetzt ISO-2022-JP-Modus, Escape-, Lead/Trail- und Output-Flag-Zustand
ueber Chunks. Nur der finale Flush verarbeitet End-of-Queue und die
spezifizierten Reconsume-Faelle; Fehler oder Flush setzen den Stream zurueck.
Der Encoding-Standard ist laut Manifest am 2026-11-07 geprueft worden. Die
grobe Encoding-Schaetzung bleibt ~55%. 53 fokussierte Decoderfaelle,
alle 4.260 Core-Tests in Debug und Release sowie Debug-/Release-
Solution-Builds bestehen; beide vollstaendigen Solution-Testlaeufe bestehen
ebenfalls. Die Browser-Suite hat 1.394 Tests und zwei bedingte
CPU-pressure-Skips. Formatverifikation und Diff-Check bestehen. Kein Native-
Smoke ist fuer Core.Encoding anwendbar. Feature-Commit `33bfe90` ist lokal;
`origin/main` steht auf `c20ba6a`; der Feature-Commit ist nicht gepusht. Siehe
[KP-522](./Kontextprotokoll.md#kp-522---iso-2022-jp-textdecoder-streaming).

**URL-Abschlusspruefung (2026-11-19; KP-507):** Der finale Offline-
Validierungslauf bestaetigt den aktuellen Stand, findet aber keine neue
Produktionsabweichung: `dotnet build VisualWeb.slnx` besteht mit 0 Warnungen
und Fehlern; Core.Tests (4.232) und Engine.Scripting.Tests (391) bestehen;
`dotnet test VisualWeb.slnx --no-build` besteht mit zwei bedingten Skips in
VisualWeb.Browser.Tests; Formatverifikation besteht. Der Arbeitsbaum war
sauber. Der URL-Support bleibt ein dokumentierter Teilumfang, keine
WHATWG-Vollkonformitaet. Bei Beginn dieses Arbeitsschritts lag `main` zwei
Commits vor `origin/main`; der Kontextnachtrag wird als dritter lokaler
Commit hinzukommen. Kein Push.

**URL-Web-IDL-Regressionsabdeckung (2026-11-19; KP-506):** Ergaenzt wurde
eine Regression fuer die bereits korrekte Receiver-vor-Argument-
Konvertierungsreihenfolge bei geborgten URLSearchParams-Operationen und
URL-Attributsettern. Ungueltige bzw. interface-fremde Receiver muessen mit
TypeError abgewiesen werden, ohne `Symbol.toPrimitive` der Argumente
aufzurufen. Die Regression und alle 391 Engine.Scripting-Tests bestehen;
Formatverifikation und Diff-Check bestehen. Commit `bb44ff5`
(`test(engine.scripting): verify URL receiver conversion order`) enthaelt
nur den Test. Bei Wiederaufnahme zeigte `origin/main` auf `08fcc31`, denselben
Stand wie `main`; nach dem Testcommit liegt `main` einen Commit voraus. Kein
Push durch diesen Arbeitsschritt; Vollkonformitaet bleibt unbelegt.

**URL-Web-IDL-Audit (2026-11-19; KP-505):** Die aktuelle
Argumentkonvertierungsreihenfolge ist fuer die geprueften Faelle korrekt:
Web-IDL prueft bei Instanzoperationen zuerst den Receiver, bevor es Argumente
konvertiert; Attributsetter pruefen ebenfalls den Receiver vor USVString-
Konvertierung. `UrlBindings` tut dies bereits. Die JavaScript-Record-
Konvertierung konvertiert den Property-Key vor dem Getterzugriff; auch das
entspricht der vorhandenen Bindung. Der URLSearchParams-Record-Konstruktor
behandelt nach USVString-Normalisierung kollidierende Keys bereits als Map-
Updates; `UrlSearchParamsRecordConversionIncludesEnumerableSymbolsAndNormalizesDuplicateKeys`
deckt dies ab. In diesem Audit ergab sich keine weitere Codeaenderung.
Quellen sind die lokalen Web-IDL-Algorithmen und der genannten Test; allgemeine
Web-IDL-Konformitaet bleibt unbelegt.

**Fortsetzung (2026-11-19; KP-504):** Die URLSearchParams-Sequenzbindung
akzeptierte primitive innere Paarwerte: `for...of` iterierte etwa einen
String in seine Zeichen, obwohl die Web-IDL-Konvertierung von `sequence<T>`
zuerst ein ECMAScript-Objekt verlangt. `UrlBindings` weist primitive Paare nun
mit TypeError zurueck; iterierbare String-Objekte bleiben gueltig. Die
Regression prueft beide Faelle und bestand nach vorherigem Fehlschlag.
Implementierung, Test und Scripting-Dokumentation sind als `7d9dc93`
(`fix(engine.scripting): reject primitive URL pairs`) committed.
Engine.Scripting.Tests (390), Solution-Build (0 Warnungen/Fehler),
Solution-Volltest (zwei bedingte CPU-Druck-Skips), Formatverifikation,
lokaler V8Smoke und Diff-Check bestehen. Bei Wiederaufnahme stand `main` auf
`0eb5bf0` gleich `origin/main`; die fruehere Angabe von vier Commits Vorsprung
war ueberholt. Nach dem Codecommit ist `main` einen Commit voraus, der
Arbeitsbaum sauber, kein Push erfolgt. Vollkonformitaet bleibt unbelegt.

**Fortsetzung (2026-11-19; KP-503):** Die URLSearchParams-Sequence-
Initialisierung konvertierte bei einem zu langen inneren Paar hoechstens drei
Werte und brach vor weiteren Web-IDL-USVString-Konvertierungen ab. Eine
Regression mit beobachtbarer `toString()`-Nebenwirkung beim vierten Wert
schlug zuerst fehl. Die Implementierung konvertiert nun weitere Elemente bis
zum dokumentierten 1.024-Element-Limit pro innerer Sequenz, bevor sie die
erforderliche Paarlaenge von zwei validiert; nur die ersten zwei konvertierten
Werte werden gespeichert. Commit `6550f5e` (`fix(engine.scripting): convert
URL sequence pairs fully`) ist lokal committed. Engine.Scripting.Tests (390),
Solution-Build (0 Warnungen/Fehler), Solution-Volltest (zwei bedingte
CPU-Druck-Skips) und Formatverifikation bestehen. Der Kontextnachtrag ist als
`843c9ad` committed. Die damalige Branch-Ahead-Angabe ist durch KP-504 als
ueberholt markiert. Vollkonformitaet bleibt unbelegt.

**Fortsetzung (2026-11-19; KP-502):** Beim weiteren URL-Web-IDL-Abgleich
wurde festgestellt, dass `URL.prototype` und `URLSearchParams.prototype` an den
interface constructor objects noch beschreibbar waren. Eine neue
Deskriptorregression schlug zuerst fehl; beide Properties sind jetzt explizit
nicht beschreibbar. Commit `534e7c0` (`fix(engine.scripting): lock URL
interface prototypes`) ist lokal committed. Engine.Scripting.Tests (389),
Solution-Build (0 Warnungen/Fehler), Solution-Volltest (zwei bedingte
CPU-Druck-Skips) und Formatverifikation bestehen. `main` liegt einen Commit
vor `origin/main`; kein Push in diesem Arbeitsschritt. Vollkonformitaet bleibt
unbelegt.

**Fortsetzung (2026-11-18; KP-501):** Eine weitere URL-spezifische Web-IDL-
Reflexionsabweichung wurde test-first geschlossen: JavaScript hatte Namen von
Operations- und Accessor-Funktionen aus den Descriptor-Objekten abgeleitet
(etwa `value`, `get` und `set`) statt aus den URL-/URLSearchParams-Membern.
Der Bootstrap weist den Konstruktoren, statischen und Instanzoperationen sowie
Attribut-Gettern/-Settern jetzt ihre vorgeschriebenen Namen zu; bestehende
Deskriptoren und Funktionslaengen bleiben erhalten. Die Regression wurde zuerst
rot bestaetigt. Commit `265e6b2` (`fix(engine.scripting): expose URL Web IDL
function names`) ist lokal committed. Engine.Scripting.Tests (389), der
Solution-Build (0 Warnungen/Fehler), Solution-Volltest (zwei bedingte
CPU-Druck-Skips) und Formatverifikation bestehen. Ein erster Volltestlauf
parallel zur Formatverifikation ueberschritt das 2-s-V8-Limit eines bestehenden
Settertests; der erneute Volltestlauf nach Ende der Formatverifikation bestand.
Der Codecommit blieb lokal; in diesem Arbeitsschritt erfolgte kein Push.
Die WHATWG-URL-Vollkonformitaet bleibt unbelegt.

**Aktueller Auftrag (2026-11-18):** Der Nutzer bittet um die vollstaendige
WHATWG-URL-Implementierung (REQ-012). Der geklaerte Umfang umfasst Core-
Algorithmen und browserseitige `URL`/`URLSearchParams`-Semantik einschliesslich
interface-spezifischem Web IDL; allgemeine Web-IDL-Generierung bleibt
ausgeschlossen (DEC-035, Q-038). Das Support-Inventar wurde in Phase 6 nach erneuter Offline-Evidenz
naeherungsweise auf ~75% WHATWG URL und ~12% Web IDL aktualisiert; beide Werte
sind grobe Umfangsschaetzungen und kein Konformitaetsgrad.
Core bietet die unveraenderliche `BrowserUrl`-Fassade und das mutable Modell
mit bidirektionaler Live-Linkage. Der genehmigte Sechs-Phasen-Plan steht in
[whatwg-url-completion-plan.md](../plans/whatwg-url-completion-plan.md).
Phase 1 ergaenzt jetzt drei gepinnte WPT-abgeleitete Datensaetze fuer
URL-encoded parsing (35 Faelle), URLSearchParams-Stringifizierung (12) und
Sequence-Konstruktion (1) sowie fixture-getriebene Tests und Provenienz.
Expected-count-Guards und die WPT-Source-Blob-Hashes wurden nach der
Reviewempfehlung ergaenzt. Der Reviewer genehmigte die Phase; der
Implementierungsagent meldet 27 fokussierte und 4.194 Core-Tests gruen. Die
ausgewaehlten Vektoren stimmen mit bestehendem Verhalten ueberein, daher gab es
keine Produktionscodeaenderung. Der Parent-Build, der Solution-Volltest und
Formatverifikation bestehen. Commit `affb369` (`test(core-url): add pinned
URLSearchParams vectors`) ist lokal auf `main`; Push ist nicht erfolgt. Auf
Nutzerwunsch wurde danach Phase 2 abgeschlossen (KP-470–474): Die vorhandenen
gepinnten URL-/Domainfixturefamilien (896 URL-, 87 toascii- und 2.670 Unicode-17
IDNA-Faelle) bestehen. Eine Regression bei relativen Referenzen mit Unicode-
Basis wurde test-first behoben. Code-Review, Core-Tests, Solution-Build,
Solution-Volltest und Formatverifikation bestehen. Phase 2 ist als
`e161d26` (`fix(core-url): select parser from URL base`) lokal committed und
review-approved. Phase 3 schloss belegte Core-Setter- und Origin-Luecken:
Protocol-Uebergang von gehosteter `file:` URL, ASCII-Ziffernpraefixe im
`WithPort`-/`WithHost`-State-Override, `file:`-Host-Parsing, blob-Origin-
Allowlist und Such-/Fragment-Delimiterreihenfolge. Der abschliessende Review
genehmigte die Aenderungen. Core-Tests, Solution-Build, Volltest und
Formatverifikation bestehen (KP-476). Phase 3 ist als `3b865d9`
(`fix(core-url): align URL setter state overrides`) lokal committed. Auf
Nutzervorgabe wurde Phase 4 begonnen (KP-477): ein mutierbares Core-URL-Modell
mit stabiler, bidirektional synchronisierter `searchParams`-Identitaet, ohne
den unveraenderlichen `BrowserUrl`-Vertrag zu lockern. Die Implementation und
direkt bezogene Core-Dokumentation sind fertig und review-approved (KP-478);
Core- und Solution-Tests, Build, Format und Diff-Check bestehen. Phase 4 ist
als `9541c97` (`feat(core-url): add live searchParams linkage`) lokal
committed. Die URL-Scopebeschreibung und Schaetzung in
[standards.md](../docs/standards.md#implemented-subsets) wurden danach als
`c85bdcf` aktualisiert: WHATWG URL ~70%; Web IDL bleibt fuer Phase 4
unveraendert. Phase 5 ergaenzt jetzt gebrandete JavaScript-`URL`- und
`URLSearchParams`-Facades, mutable Attribute und stabile Live-Linkage, statische
`parse()`/`canParse()`, begrenzte Iteration/Callbacks sowie interface-spezifische
Web-IDL-USVString-, Sequence- und Record-Konvertierung. Die private Bridge
uebertraegt nur Strings/Handles; allgemeine Web IDL, Netzwerkzugriff,
automatische Dokument-URL-Integration und Aktivierung von Page Scripts bleiben
ausgeschlossen. Phase 5 ist als `3de5e4d` (`feat(engine.scripting): add URL
browser interfaces`) committed; der Hook formatierte Kontext-/Phasenbericht
und meldete neun gepruefte Dateien ohne Probleme. Phase 6 bestaetigte offline
896 URL-, 87 toascii-, 2.670 Unicode-17-IDNA-, 35 URL-encoded-Parser-, 12
Stringifier- und 1 Sequence-Constructor-WPT-Vektoren. Die 4.223 Core- und 372
Engine.Scripting-Tests bestanden; Solution-Build, Solution-Volltest,
Formatverifikation und Diff-Check bestanden, mit zwei uebersprungenen
Prozessdrucktests. `standards.md` nennt jetzt ungefaehr 75% WHATWG URL und 12%
Web IDL; Core-, Scripting-, Architektur- und Projektkarten-Dokumentation
beschreiben die Schnittstellengrenzen. Phase 6 wurde als `5181b13`
(`docs(standards): record URL and Web IDL support`) committed. Der
genehmigte Sechs-Phasen-Plan ist abgeschlossen; der Gesamtbericht steht in
[whatwg-url-completion-complete.md](../plans/whatwg-url-completion-complete.md).
Im aktuellen lokalen Status zeigen `main` und `origin/main` beide auf
`5181b13`. Der Reflog markiert die Aktualisierung von `origin/main` um
06:30:17+01 als „update by push“; in diesem Agent-Turn wurde kein Push-Befehl
ausgefuehrt, die Ursache der Ref-Aktualisierung ist ungeklärt. Der
Gesamtabschlussbericht und der zugehoerige Kontextnachtrag wurden danach lokal
als `cb58090` committed. Bei der letzten Statuspruefung war `main` einen Commit
vor `origin/main`; fuer diesen Abschlusscommit wurde nicht gepusht.

**Abgrenzung des Abschlusses:** Der genehmigte Sechs-Phasen-Plan ist fertig,
aber dies bedeutet keine vollstaendige WHATWG-URL-Konformitaet. Das
Support-Inventar bewertet URL mit ~80% (Schaetzung). Dokumentierte Restluecken
sind vollstaendige Standard-/WPT-Abdeckung, breitere/automatische URL-Dokument-
Integration sowie allgemeine Web-IDL-Generierung, vollstaendige
Overload-Aufloesung und Konstruktor-/Prototypsemantik. Die zuvor offene Core-
Mutation weiterer URL-Komponenten wurde in KP-493 geschlossen. Die vorhandenen
URL-/IDNA-/URLSearchParams-Fixtures belegen Regressionen fuer die gepinnten
Vektoren, nicht die gesamte Spezifikation. Die Aussage, es gebe noch keine
Folgephase, galt vor der Nutzerfreigabe KP-488 und ist dadurch ersetzt.

**Fortsetzung (2026-11-18, ca. 20:45 +01; KP-493):** Auf den erneuerten
Vollstaendigkeitsauftrag wurde eine dokumentierte Core-Luecke geschlossen:
`MutableBrowserUrl` bietet jetzt auch mutable `Protocol`, `Username`,
`Password`, `Host`, `Hostname`, `Port`, `Pathname` und `Hash` ueber die
vorhandenen unveraenderlichen Setter. `Href`/`Search` behalten ihre bisherige
Live-Linkage-Semantik; die weiteren Setter bewahren die Identitaet und Tupel
des `SearchParams`-Objekts. Neue Tests decken Setterketten und ungueltige
No-ops ab. Fokussierte Tests (8), Core.Tests (4.231), Solution-Build (0
Warnungen/Fehler), Solution-Volltest und Formatverifikation bestehen. Die
Aenderungen sind als `81f9ce1` committed; `main` liegt einen Commit vor
`origin/main`. Dies vervollstaendigt nicht den WHATWG-URL-Standard; die
dokumentierten Vollkonformitaets- und Integrationsluecken bleiben offen.
In der Fortsetzung wurden gepinnte WPT-Faelle fuer das Entfernen von
URLSearchParams aus verknuepften URLs, ein zweites Fragezeichen als Query-Daten
und die unabhaengige Kopie eines URLSearchParams-Objekts ergaenzt.
Diese Abdeckungsgruppe ist als `6dd9534` (`test(core-url): pin URLSearchParams
edge cases`) committed. Danach wurden weitere gepinnte WPT-Faelle fuer
JavaScript-only USVString-Eingaben, `URL.parse()`/`URL.canParse()` und
Setterverhalten bei fuenf C0-Steuerzeichen auf zwei Schemen geprueft und
ergaenzt. Die Aenderungen stehen in `479823a`
(`test(scripting): cover URL WPT edge cases`). Danach kamen Regressionen fuer
Mutation waehrend URLSearchParams-Iteration gemaess dem gepinnten
`urlsearchparams-foreach.any.js` hinzu. Engine.Scripting.Tests (385),
Core.Tests (4.232), Solution-Build (0 Warnungen/Fehler), Solution-Volltest,
Formatverifikation und Diff-Check bestehen. Alle geprueften Faelle stimmen
mit dem vorhandenen Verhalten ueberein; Produktionscode musste nicht geaendert
werden. Die Iterationsregression wurde als `2b09fd6`
(`test(scripting): cover URL iterator mutations`) committed. Eine weitere
Regression fuer `URLSearchParams.has()`/`delete()` mit optionalem Wert folgt
den gepinnten Faellen, einschliesslich explizitem `undefined`; die vollstaendige
Scripting-Suite (386), Solution-Build und Formatverifikation bestehen; dieses
Inkrement steht in `8682577` (`test(scripting): cover optional URLSearchParams
values`). Zusaetzlich wurde der gepinnte `URL.toJSON()`-JSON-Serializationstest
ergaenzt und als `8ecc0c4` (`test(scripting): cover URL toJSON serialization`)
committed; Engine.Scripting.Tests (387) besteht. Der aktuelle Durchlauf
ergaenzt ausserdem den readonly-`searchParams`-Fall aus WPT und besteht mit 388
Engine.Scripting-Tests. Die Aenderung steht als `fb6175a`
(`test(scripting): verify URL searchParams readonly`). Ein WPT-Abgleich der
Anchor/Area-Setterdatei bestaetigt, dass sie die schon gepinnten Settervektoren
auf HTMLAnchorElement/HTMLAreaElement anwendet; diese HTML-Integration ist in
der Supportbeschreibung ausdruecklich weiter ausgegrenzt. Es wurde kein
Produktionsfehler nachgewiesen. Nach dem readonly-Test bestanden
Solution-Volltest (alle Tests; zwei vorgesehene CPU-Drucktests uebersprungen),
Solution-Build (0 Warnungen/Fehler), Formatverifikation und Diff-Check.
Die eigenstaendige WHATWG-URL-WPT-Abdeckung dieses Durchlaufs ist fuer die
lokal vorhandenen relevanten Testfamilien abgeglichen; vollstaendige
Standardkonformitaet bleibt nicht belegt.

**Neue Nutzeranfrage (2026-11-18 06:53 +01):** Der Nutzer fordert nun, WHATWG
URL vollstaendig zu implementieren; dies erweitert den abgeschlossenen
Sechs-Phasen-Umfang. Eine Read-only-Lueckenpruefung fand zusaetzlich konkrete
URL-Web-IDL-Abweichungen (Prototyp-Deskriptoren/Funktionslaengen und
Iteratorobjekte) sowie offene, umgebungsabhaengige Blob-Origin-/Dokument-
Integration. Der Nutzer genehmigte am 2026-11-18 um 07:11 +01 den erweiterten
Fuenf-Phasen-Plan [whatwg-url-full-conformance-plan.md](../plans/whatwg-url-full-conformance-plan.md).
Phase 1 korrigiert URL/Web-IDL-Deskriptoren, Function-`length` und optionale
Argumente bei gewahrtem Schutz des privaten Bridges; sie wurde als `e493157`
committed und review-approved. Phase 2 ersetzt Generatoren durch gebrandete
Web-IDL-Paar-Iteratoren mit gemeinsamem Iteratorprototyp, nativen
Iteratorresultaten und indexbasierter Live-Mutationssemantik. Engine.Scripting-
Tests (379), Solution-Build, Solution-Volltests (zwei erwartete Skips), V8Smoke,
Format und Diff-Check bestanden. Phase 2 ist uncommitted. Phase 3 pinnt
zusaetzlich 278 URL-Setter-WPT-Faelle und acht URLSearchParams-Sortiervektoren;
WPT-getriebene Korrekturen betreffen den Host-Setter, nicht-autoritaere
`//`-Pfadserialisierung und optionale Query-Encoding-Ueberschreibung. Phase 4
ergaenzt injizierte Blob-Entry-Origins und eine konservative URL-Anzeigefassung
mit Credential-Auslassung, Unicode-IDN und Bidi-Elision; Browser-Chrome
verwendet diese Fassung, waehrend Historie kanonisch bleibt. Die
Fixture-Provenienz steht in [Core-Testdaten](../tests/Core.Tests/Data/README.md).
Phase 5 aktualisiert die nachgewiesenen Teilbereiche in
[standards.md](../docs/standards.md): ungefaehr 80% URL, 30% Unicode-IDNA/Bidi
und 15% Web IDL; diese Werte sind Schaetzungen, keine Konformitaetsmessung.
Berichte: [Phase 1](../plans/whatwg-url-full-conformance-phase-1-complete.md),
[Phase 2](../plans/whatwg-url-full-conformance-phase-2-complete.md) und
[Gesamtabschluss](../plans/whatwg-url-full-conformance-complete.md). Der
Solution-Build besteht mit null Warnungen/Fehlern; der finale Volltestlauf
besteht mit 17.194 Tests und zwei uebersprungenen CPU-Drucktests. Core.Tests
enthaelt 4.229, Engine.Scripting.Tests 379 und VisualWeb.Browser.Tests 1.374
bestandene Tests. Formatverifikation und Diff-Check bestehen. Ein erster
Volltestlauf fand einen Blob-Anzeige-Slash-Mismatch; die Anzeige wurde
korrigiert und die komplette Validierung danach wiederholt. Die Erweiterung
wurde als `8e06187` (`feat(url): extend bounded WHATWG URL support`) committed.
Der Commit-Hook formatierte fuenf Dokumentationsdateien und meldete fuer alle
29 Dateien keine Probleme. WHATWG-URL-Vollkonformitaet wird nicht behauptet.

**Aktueller Missing-Lauf (2026-11-17; 10 Inkremente angefordert; KP-454–462):**
Der Nutzer hat zehn getrennte Missing-Prioritaets-Inkremente angefordert.
WHATWG URL (~65%) bleibt gemaess der dokumentierten WHATWG-only-Prioritaetsregel
vor Encoding (~55%). Pass 1 implementiert `BrowserUrl.WithHost()` als
unveraenderlichen Snapshot. Commit `87e1402` ist auf `main`; Linux development,
Windows confinement, CodeQL und Dev Environment Prebuilds bestehen. Pass 2
implementiert `BrowserUrl.WithPort()` und ist als `ede73f5` auf `main`;
dieselben vier Workflows bestehen. Pass 3 implementiert `BrowserUrl.WithPathname()`
als unveraenderlichen Snapshot, einschliesslich Pfad-Normalisierung, Beibehaltung
auch leerer Query-/Fragment-Delimiter und opaque-path no-op. Die URL-Quelle ist
lokal vorhanden und manifest-frisch (`checkedAt: 2026-11-16`); kein Refresh
war erforderlich. Ein Sentinel-Fragment erhaelt abschliessende Pfad-Leerzeichen
und C0-Steuerzeichen bis zur pathname-setter-Verarbeitung. Fuer Pass 3 bestehen
13 fokussierte Faelle, alle 4.137 Core-Tests, der Debug-Solution-Build ohne
Warnungen/Fehler und Formatverifikation. Der erste volle Solution-Testlauf
hatte einen Timeout im Browser-Prozess-Test
`FilteredNthQueriesAndStylesFeedTransactionalRetainedPaint`; beide Varianten
bestanden isoliert. Der wiederholte Volltest bestand mit 17.084 Tests und zwei
erwarteten Skips; auch der vorherige volle Lauf auf Basis-Commit `ede73f5`
bestand. Commit `89a0a0f` ist auf `main`; Linux development, Windows
confinement, CodeQL und Dev Environment Prebuilds bestehen. Pass 3 ist
abgeschlossen. Pass 4 implementiert `BrowserUrl.WithHostname()` als
unveraenderlichen Snapshot mit Erhalt des Ports; Eingaben, die einen Port
enthalten, ungueltige Hostnamen und opaque-path URLs bleiben unveraendert. Die
URL-Quelle ist manifest-frisch (`checkedAt: 2026-11-16`). Acht fokussierte
Faelle, alle 4.145 Core-Tests, Debug-Solution-Build ohne Warnungen/Fehler,
17.092 Solution-Tests mit zwei erwarteten Skips und Formatverifikation
bestehen. Commit `e347ab4` ist auf `main`; Linux development, Windows
confinement, CodeQL und Dev Environment Prebuilds bestehen. Pass 4 ist
abgeschlossen. Fuer Pass 5 wurde nach erneuter Pruefung der URL-Luecken die
naechsthoehere WHATWG Encoding-Luecke gewaehlt: URL-`searchParams`-Live-Linkage
ist nicht Teil der unveraenderlichen Facade, und die URL-Quelle spezifiziert
keinen nicht-UTF-8-Override als konformes Verhalten. `WebTextDecoder` bietet
nun den one-shot .NET-Kernteil mit Label, fatal-Modus und BOM-Option; Streaming
und Web-IDL-Bindings bleiben aus. Die Encoding-Quelle ist manifest-frisch
(`checkedAt: 2026-11-07`). Sieben fokussierte und alle 4.152 Core-Tests
bestehen; Debug- und Release-Solution-Builds bestehen ohne Warnungen/Fehler.
Debug- und Release-Volltests bestehen jeweils mit 17.099 Tests und zwei
erwarteten Skips; Formatverifikation besteht. Kein nativer Smoke ist fuer
Core.Encoding anwendbar. Pass 5 ist committed und gepusht (`a93cd3e`); Linux
development (`44127068505`), Windows confinement (`44127068447`) und CodeQL
(`44127068165`) sowie Dev Environment Prebuilds (`44127067713`) bestehen.
Pass 5 ist abgeschlossen. Pass 6 erweitert `WebTextDecoder` um inkrementelles
UTF-8-Decodieren mit `stream`-Option, Teilzeichen-/BOM-Zustand, finalem Flush
und Reset; andere Encodings weisen Streaming explizit zurueck. Zwoelf
fokussierte Decoder- und alle 4.157 Core-Tests bestehen. Debug-/Release-
Solution-Builds bestehen ohne Warnungen/Fehler; beide Volltests bestehen mit
17.104 Tests und zwei erwarteten Skips; Formatpruefung besteht. Kein nativer
Smoke ist fuer Core.Encoding anwendbar. Pass 6 ist lokal validiert, committed
und gepusht (`045a6b5`); Linux development (`44138002370`),
Windows confinement (`44138002057`), CodeQL (`44138001292`) und Dev Environment
Prebuilds (`44138001342`) bestehen. Pass 6 ist abgeschlossen. Details und Quellen:
Pass 7 erweitert `WebTextDecoder` um Chunk-Decodierung fuer stateless single-byte
Encodings, zusaetzlich zum UTF-8-Streaming. Vierzehn fokussierte und alle 4.159
Core-Tests bestehen; Debug-/Release-Builds, Volltests mit 17.106 Tests und zwei
erwarteten Skips sowie Formatpruefung bestehen. Kein nativer Smoke ist fuer
Core.Encoding anwendbar. Pass 7 ist committed und gepusht (`c1b4bbd`); Linux
development (`44147752385`), Windows confinement (`44147752645`), CodeQL
(`44147751479`) und Dev Environment Prebuilds (`44147751887`) bestehen. Der
erste Prebuild-Lauf scheiterte bei der Template-Generierung/-Uebertragung mit
einem Manifest-Generierungsfehler; der erneute Lauf war erfolgreich.
Pass 8 erweitert `WebTextDecoder` um UTF-16LE/BE-Streaming mit gespeichertem
Teilbyte-/Surrogatzustand, BOM-Verarbeitung, korrekter Reprozessierung und
einmaligem EOF-Fehler fuer kombinierte Restzustaende. Die lokale
Encoding-Quelle war laut Manifest frisch (`checkedAt: 2026-11-07`); der
gemeinsame UTF-16-Decoder und die LE/BE-Algorithmen wurden gelesen. Alle 25
fokussierten Decoder- und 4.170 Core-Tests, Debug-/Release-Builds, beide
Solution-Volltests mit 17.117 Tests plus zwei erwarteten Skips sowie
Formatverifikation bestehen. Kein nativer Smoke ist fuer Core.Encoding
anwendbar. Pass 8 ist committed und gepusht (`81ff4f7`); Linux development
(`44164112835`), Windows confinement (`44164112725`), Push on main
(`44164112361`) und Dev Environment Prebuilds (`44164112614`) bestehen.
Pass 9 erweitert `WebTextDecoder` um Streaming fuer die vorhandenen
paarbasierten Big5-, Shift_JIS- und EUC-KR-Decoder. Geteilte Lead-Bytes,
ASCII-Reprozessierung und ungueltige Nicht-ASCII-Trailbytes bleiben
encoding-spezifisch korrekt; auch Finalisierung und fataler Reset sind
abgedeckt. Alle 41 fokussierten Decoder- und 4.186 Core-Tests sowie
Debug-/Release-Builds, beide Solution-Volltests mit 17.133 Tests und zwei
erwarteten Skips sowie Formatverifikation bestehen. Kein nativer Smoke ist fuer
Core.Encoding anwendbar. Pass 9 ist mit `69a97e6` committed und gepusht; Linux
development (`44178691390`), Windows confinement (`44178691496`), CodeQL
(`44178690822`) und Dev Environment Prebuilds (`44178691129`) bestehen.
Pass 10 erweitert das Streaming auf EUC-JP und behaelt Lead-Byte/JIS0212-
Zustand chunkuebergreifend; der 3-Byte-Pfad, Katakana, Trailfehler, ASCII-
Reprozessierung und Finalisierung sind durch fuenf fokussierte Tests belegt.
Der lokale Standard ist laut Manifest frisch (`checkedAt: 2026-11-07`); die
EUC-JP-Decoder-Algorithmen wurden gelesen. Alle fuenf fokussierten, 4.191 Core-
und 17.138 Solution-Tests mit zwei erwarteten Skips bestehen; Debug-/Release-
Builds und Formatverifikation bestehen ebenfalls. Kein nativer Smoke ist
anwendbar. Pass 10 ist mit `ea43ce9` committed und gepusht; Linux development
(`44189271100`), Windows confinement (`44189271150`), CodeQL (`44189270313`)
und Dev Environment Prebuilds (`44189269907`) bestehen. KP-454–466 sowie
[Missing.md](../plans/Missing.md#third-standards-priority-run-2026-11-17).

**Vorheriger Standards-Pass (2026-11-17; KP-453):** Der damalige
Missing-Prioritaetslauf folgt der WHATWG-URL-Luecke, weil URL im aktuellen
Support-Inventar (~65%) vor Encoding (~55%) liegt. Nach den dokumentierten
Passes `href`, `toJSON`, `parse` und `protocol` wurden `username` und
`password` als naechstes begrenztes URL-API-Inkrement umgesetzt. `BrowserUrl`
liefert dafuer unveraenderliche Snapshots; Userinfo wird nach dem UTF-8-
Percent-Encoding-Set der URL Standard kodiert. URLs ohne Host und `file:`
bleiben unveraendert. Das ist lokaler Code-/Teststand, keine Aussage zu
Web-IDL oder Vollkonformitaet. Der lokale URL-Standard (`url.html`, Manifest-ID
`url`, checkedAt 2026-11-16) wurde ueber SpecCache erfolgreich bestaetigt.
Debug/Release Core- und Solution-Validierung sowie Formatverifikation bestehen.
Commit `35ed9cd` ist auf `main` gepusht. Linux development
(Run `44006688357`, x64/arm64 und Trunk), Windows confinement
(Run `44006688164`, x64/arm64) und CodeQL (Run `44006687798`) bestehen.

**Standards-Prioritaetsregel aktualisiert (2026-11-15):** Bei kuenftigen
standardsgetriebenen Feature-Auswahlen werden nur teilweise implementierte
WHATWG-Standards nach den Schaetzungen im Support-Inventar gerankt. Die damalige
~30%-Gleichstandsschaetzung wurde durch die Inventarpruefung vom 2026-11-16
ersetzt; die aktuellen Werte und Auswahl stehen oben sowie in
[standards.md](../docs/standards.md#support-status). Der bereits begonnene
CSS-Color-Pass 1 wurde durch die damalige Regel nicht rueckwirkend geaendert.
Siehe DEC-033 und KP-382.

**Neuer Missing-Prioritaetslauf (2026-11-15; Pass 10 ausgewaehlt):** Der
aktuelle Auftrag fordert zehn neue getrennte Inkremente. Pass 1, CSS-Color-4-
Ray-Trace-Mapping fuer encoded und lineare Display-P3-Werte, wurde vor der
Regelaenderung in KP-382 gewaehlt und davon nicht rueckwirkend geaendert;
Commit `f600fba` und CI sind gruen. Fuer Pass 2 wurde nach der WHATWG-only-
Regel die begrenzte Encoding-Luecke `TextEncoder.encode()` gewaehlt. Der
.NET-Kern `WebTextEncoder.Encode` liefert UTF-8-Bytes und ersetzt ungepaarte
Surrogate durch U+FFFD. Pass 2 besteht sechs fokussierte, 3.997 Core- und
16.905 Solution-Tests; Build und Formatverifikation bestehen. Commit
`81d1d6d` ist gepusht; Linux, Windows x64/arm64 und CodeQL-CI sind gruen.
Fuer Pass 3 wurde die WHATWG-Encoding-Luecke `TextEncoder.encodeInto()`
ausgewaehlt: der .NET-Kern schreibt nur vollstaendige UTF-8-Skalare, zaehlt
gelesene UTF-16-Code-Units und ersetzt ungepaarte Surrogate. Vier fokussierte,
4.001 Core- und 16.909 Solution-Tests bestehen; Build ohne Warnungen/Fehler
und Formatverifikation bestehen. Commit `957105e` ist auf `main` gepusht;
Linux, Windows x64/arm64 und CodeQL-CI sind gruen. Pass 3 ist abgeschlossen.
Fuer Pass 4 wurde aus dem Gleichstand bei ~30% die WHATWG-URL-Luecke
`URLSearchParams` gewaehlt: String-Initialisierung, `size`, `get()` und
`getAll()` als bounded Core-Teilmenge ohne Mutationen, URL-Verknuepfung oder
Web IDL. Vier fokussierte Tests, 4.005 Core-Tests und 16.913 Solution-Tests
bestehen; Build ohne Warnungen/Fehler und Formatverifikation bestehen. Pass 4
ist abgeschlossen. Commit `8af42a6` ist auf `main` gepusht; Linux, Windows
x64/arm64 und CodeQL-CI sind gruen. Fuer Pass 5 wurde aus dem weiterhin
gleichrangigen WHATWG-URL-Standard der distincte Mutationsschritt
`URLSearchParams.append()` gewaehlt. Fuenf fokussierte URLSearchParams-Tests,
4.006 Core-Tests und 16.914 Solution-Tests bestehen; der Build ist ohne
Warnungen/Fehler und die Formatverifikation besteht. Commit `6d4a1ec` ist auf
`main` gepusht; Linux, Windows x64/arm64 und CodeQL-CI sind gruen. Pass 5 ist
abgeschlossen. Fuer Pass 6 wurde im weiterhin gleichrangigen WHATWG-URL-
Standard die begrenzte `URLSearchParams.has(name)`-Operation ausgewaehlt.
Der optionale Wertfilter bleibt ausserhalb des Inkrements. Sechs fokussierte
URLSearchParams-Tests, 4.007 Core-Tests und 16.915 Solution-Tests bestehen;
der Build ist ohne Warnungen/Fehler und die Formatverifikation besteht.
Commit `9c7fa7e` ist gepusht; Linux, Windows x64/arm64 und CodeQL-CI sind
gruen. Pass 6 ist abgeschlossen; vier der zehn angeforderten Inkremente
bleiben. Fuer Pass 7 wurde der noch offene optionale Wertfilter von
`URLSearchParams.has(name, value)` als eigener, genaues Tuple-Matching-
Algorithmusschritt gewaehlt. Sieben fokussierte URLSearchParams-Tests,
4.008 Core-Tests und 16.916 Solution-Tests bestehen; Build und
Formatverifikation bestehen. Commit `eff5b13` ist gepusht; Linux, Windows
x64/arm64 und CodeQL-CI sind gruen. Pass 7 ist abgeschlossen; drei der zehn
angeforderten Inkremente bleiben. Siehe Q-037 und KP-379 bis KP-397.
Fuer Pass 8 wurde im weiterhin gleichrangigen WHATWG-URL-Standard
`URLSearchParams.delete(name)` gewaehlt: alle Tupel mit diesem normalisierten
Namen entfernen, andere geordnete Eintraege erhalten. Der grobe URL-
Abdeckungswert bleibt ~30%, da die verbleibenden URL- und SearchParams-Luecken
deutlich breiter sind als dieser Methodeninkrement. Die Implementierung ist
lokal validiert: acht fokussierte URLSearchParams-, 4.009 Core- und 16.917
Solution-Tests bestehen; Build und Formatverifikation bestehen.
Commit `27702ca` ist gepusht; Linux- (Run `43736476256`), Windows- (Run
`43736476224`) und CodeQL-CI (Run `43736475056`) sind gruen. Beim Push wurde
auch der zuvor unabhaengig erstellte Dokumentationscommit `f16c6d7`
mitveroeffentlicht. Pass 8 ist abgeschlossen; zwei der zehn Inkremente
bleiben.
Fuer Pass 9 wurde nach erneuter Gleichstandspruefung
`URLSearchParams.delete(name, value)` gewaehlt: alle Tupel entfernen, bei
denen sowohl der normalisierte Name als auch der normalisierte Wert
uebereinstimmen. `delete(name)` bleibt unveraendert. Die URL-Quelle ist frisch
(`checkedAt: 2026-11-07`); andere URL-Mutationen und URL-Objekt-Verknuepfung
bleiben offen. Neun fokussierte URLSearchParams-, 4.010 Core- und 16.918
Solution-Tests bestehen; Build und Formatverifikation bestehen. Commit
`d637bb7` ist gepusht; Linux- (Run `43739620711`), Windows- (Run
`43739621051`) und CodeQL-CI (Run `43739619909`) sind gruen. Pass 9 ist
abgeschlossen; ein Inkrement bleibt.
Fuer Pass 10, das letzte der zehn angeforderten Inkremente, wurde
`URLSearchParams.set(name, value)` gewaehlt: den ersten passenden Tuple-Wert
ersetzen und weitere gleichnamige Tupel entfernen, andernfalls ans Ende
anhaengen. URL und Encoding sind weiterhin bei ~30% gleichauf; die Wahl
beruht auf diesem klar abgegrenzten, lokal testbaren URL-Algorithmus.
Elf fokussierte URLSearchParams-, 4.012 Core- und 16.920 Solution-Tests
bestehen; Build und Formatverifikation bestehen. Commit/Push und CI fuer Pass
10 sind abgeschlossen: Commit `878dd06` ist auf `main`; Linux development
(Run `43742818336`), Windows confinement (Run `43742818364`) und CodeQL
(Run `43742818324`) bestehen. Der angeforderte Zehnerlauf ist abgeschlossen.

**Ressourcen-Testzeiten vor Einzeltest-Instrumentierung (2026-11-15):** In den vier
Konfigurationen Linux x64, Linux arm64, Windows x64 und Windows arm64 wurden
die gesamten Browser-Testprojekte in 1:17, 1:25, 2:19 und 2:11 ausgefuehrt;
die Script-Testprojekte in 8, 10, 10 und 10 Sekunden. Das sind Suite-Zeiten,
keine Einzeltestzeiten. Einzelzeiten wurden nur fuer die fuenffach wiederholte
`WindowsCpuPressureCancellationOrDeadlineRestartsOnlyAffectedRenderer`-Theory
ausgegeben: pro Fall (Deadline / Cancellation) x64 etwa 33.4 / 5.0 Sekunden,
arm64 etwa 33.8 / 3.4 Sekunden. Der dedizierte Fuenf-Durchlaeufe-Schritt
dauerte insgesamt 3:24 (x64) bzw. 3:20 (arm64). Fuer die uebrigen
Ressourcenfaelle liefern diese Logs keine individuellen Dauern; sie sind nur
im Browser-Suite-Gesamtwert enthalten. Siehe KP-409.

**Einzeltest-Timing implementiert und CI-gruen (2026-11-15):** Der Nutzer hat die Ausgabe der
Laufzeiten aller zuvor genannten Ressourcen-Tests, Push und anschliessende
Analyse der vier CI-Konfigurationen beauftragt. Die beiden Workflows verwenden
nun fuer schnelle, Browser- und Scripting-Tests den vorhandenen VSTest-
Konsolenlogger mit `verbosity=normal`; Testcode, Fristen, Auswahl und Wiederholungen
bleiben unveraendert. Elf ausgewaehlte Release-Browser-Testfaelle bestehen lokal
und geben Einzelzeiten aus; Windows-Faelle kehren auf Linux frueh zurueck und
zaehlen nicht als Windows-Nachweis. Commit `36f48fa` ist auf `main` gepusht;
Linux development `43754186972`, Windows confinement `43754186901` und CodeQL
`43754186829` bestehen. Alle 16.920 lokalen Solution-Tests und Formatverifikation
bestehen. Die vollstaendige Vergleichstabelle steht in KP-411: Linux-Deadline-
Faelle dauern 13/12 Sekunden (x64/arm64), Windows-Deadline-Faelle 34/33 Sekunden;
Cancellation 1 Sekunde/917 ms bzw. 4/3 Sekunden. Die fuenf zusaetzlichen Windows-
Wiederholungen kosten insgesamt 3:23/3:34 Wandzeit. Fristen (12 Sekunden Linux-
Test, 30 Sekunden Windows-Standard) plus Worker-Start/Erholung erklaeren den
gezielten Warteanteil; das ist keine Messung reiner Rechenleistung. Siehe KP-410
und KP-411.

**CPU-Deadline-Tests nur auf ausdruecklichen Wunsch (2026-11-15):** Der Nutzer
moechte die teuren CPU-Deadline-Faelle auf allen Plattformen aus normalen
Testlaeufen herausnehmen. Nur die beiden `cancel: false`-Zeilen in
`ProcessTests` sind jetzt xUnit-v3-Explicit-Tests; Cancellation, Queue-Abbruch,
Tab-Schliessen, Speicher- und Task-Limits bleiben im Standardlauf. Lokaler
Namensfilter ohne Opt-in ueberspringt genau die Deadline-Zeilen, waehrend
`-- xUnit.Explicit=only` nur diese ausfuehrt (auf Linux nativ verifiziert;
Windows-Zweig lokal nicht ausgefuehrt). Beide Workflows bieten das manuelle
Boolean-Eingabefeld `run_cpu_deadline_tests` mit Default `false`; bei `true`
kommt je Architektur ein Deadline-only-Schritt hinzu. Die fuenf normalen
Windows-Wiederholungen bleiben Cancellation-only. Build ohne Warnungen/Fehler
und Formatpruefung bestehen; Gesamtregression besteht mit 16.918 ausgefuehrten
Tests (Browser: 1.339), die beiden expliziten Zeilen werden uebersprungen.
Als `b33f944` committed. Aktuelle Push-CI auf `50c16b2` besteht auf allen vier
nativen Konfigurationen (Linux `43766088094`, Windows `43766088085`).
Die Deadline-Zeilen werden in regulaerer Suite und Windows-Wiederholungen
uebersprungen. Manueller Opt-in-Dispatch weiterhin nicht nachgewiesen.
Siehe [Testanleitung](../tests/README.md#opt-in-cpu-deadline-tests) und KP-412.

**Aktuelle Ressourcen-Testzeiten (2026-11-15):** Auf Nutzerwunsch wurde die
Vergleichstabelle aus den neuesten abgeschlossenen CI-Logs neu erstellt,
ohne alte Deadline-Messungen einzumischen. KP-413 enthaelt die vollstaendige
Tabelle und Job-Quellen auf `50c16b2`. CPU-Cancellation dauert Linux x64/arm64
1 Sekunde/937 ms, Windows x64/arm64 3/4 Sekunden. Beide Deadline-Faelle sind
Opt-in und wurden nicht ausgefuehrt; Skipped-Meldungen sind keine Messzeiten.
Die regulaeren Browser-Suiten dauern laut Runner insgesamt 1.3091/1.1296
Minuten auf Linux und 1.7635/1.7389 Minuten auf Windows (je x64/arm64).

**Windows-CI-Zeitanteile (2026-11-15):** Auf Nachfrage wurden dieselben
Job-Logs und Steps nach Wandzeit verglichen (KP-414). Gesamt Linux x64/arm64
3:56/3:20, Windows 5:55/7:13. Windows allein wiederholt Cancellation fuenfmal
(33/38 Sekunden). Zwei Multiprocess-CLI-Smokes brauchen Windows 34/84 Sekunden
gegen Linux 5/7 Sekunden; die Pfade sind nicht gleichwertig (Windows nativ
und confined, Linux dummy und explizit unsandboxed). SDK-Setup 27/42 gegen
12/7 Sekunden, Build 75/89 gegen 82/51 Sekunden. Browser-Suite-Step
107/106 gegen 80/69 Sekunden; kein einzelner Browser-Fall dauert in diesen
Logs mehr als sechs Sekunden. Der ARM64-Zusatz liegt vor allem ausserhalb
der Browser-Suite. Interne Ursachen wie Start/Staging, AppContainer,
Grafik oder Dateipruefung sind aus diesen Logs nicht isolierbar.

**Smoke-Phaseninstrumentierung abgeschlossen (2026-11-16):** Der Nutzer hat
Instrumentierung, Push und erneute CI-Auswertung beauftragt. Die Einstiege
sind `Program.cs` (`BrowserSmoke.Run` samt Wait-Schleifen),
`ProcessPageRenderer` (Start/Handshake/Exchange) und
`WindowsRendererSandbox.Start` (Profil, Deployment-/Runtime-Kopie, ACLs,
Job/Prozess) samt Worker-Disposal. Vorgesehen sind smoke-only aktivierte,
korrelierbare Host-Diagnosen auf stderr, ohne IPC-/Worker-stdout-, Deadline-,
Confinement- oder Render-Verhaltensaenderungen. Der konkrete Drei-Phasen-Plan
wurde am 2026-11-15 um 23:16 Europe/Berlin mit "yes" freigegeben.
Implementiert und nach Review-Korrektur freigegeben: Smoke-Phasen,
Renderer-Start/Hello/separate Render-Requests/Exchange/Disposal sowie Windows-
Profil, Kopier-, ACL-, Job-/Prozess- und Cleanup-Phasen. Ein versiegelter
`HostTimingBuffer` zeichnet maximal 4.096 Metadaten-Records ohne I/O oder
Callbacks auf; Export auf stderr erfolgt nach Cleanup, Ueberlauf und Writer-
Fehler sind explizite Exportfehler. Native Ownership und Semaphore-Freigaben
koennen daher nicht durch Writer-Callbacks unterbrochen werden.
Fokussiert 15 Browser-/9 Platform-Faelle sowie Solution-Build ohne
Warnungen/Fehler bestehen. Lokaler unsandboxed gescripteter Multiprocess-
Dummy-Smoke besteht mit korrelierten Zeiten; keine native Windows-Evidenz.
Gesamtregression besteht mit 16.931 Tests (1.351 Browser, 41 Platform),
beide expliziten CPU-Deadline-Zeilen bleiben ausgeschlossen. Formatpruefung
und Diff-Check bestehen. Review APPROVED; als `bfce5aa` auf `main` committed
und gepusht. Native CI besteht auf allen vier Konfigurationen: Windows
`43771950858`, Linux `43771950888`; CodeQL `43771950625` besteht ebenfalls.
Die beiden neuen Windows-Timing-/Ownership-Tests sind auf x64 und arm64
nativ bestanden. Bereits
vorliegende lokale Kontext-Aenderungen aus KP-413/KP-414 bleiben erhalten.
Siehe KP-415 und KP-416.

**Gemessene Windows-Ursache (2026-11-16):** KP-417 enthaelt die Phasentabelle
auf `bfce5aa`. Jeder Multiprocess-Smoke startet sechs Worker und kopiert
Deployment und Runtime fuer jeden erneut. Auf Windows arm64 liegen im
statischen Smoke 26.48 von 26.93 aufsummierten Startup-Sekunden in diesen
Kopien; im gescripteten Smoke 28.96 von 29.29 Sekunden. Die CLI braucht
34.05/40.64 Sekunden, waehrend die aufsummierten Render-Requests nur
1.16/4.50 Sekunden brauchen. Profil/Storage/ACL/Job sind vergleichsweise
klein (zusammen deutlich unter einer Sekunde). Auf x64 gibt es zusaetzlich
einzelne Cleanup-/Handshake-Ausreisser. Gemessen ist Zeit innerhalb der
Dateikopien, keine isolierte Datentraeger-/Antivirus-Ursache. Naechster
Performance-Untersuchungsbereich waere wiederholtes vertrauenswuerdiges
Staging; keine Lockerung von Origin-Wechseln, ACLs oder Limits beschlossen.

**Deployment-Inventar untersucht (2026-11-16):** Auf "continue investigation"
wurde der Kopierpfad weiter verfolgt (KP-418). `CopyTree` kopiert rekursiv alle
regulaeren Dateien ausser Reparse-Points; weder Architektur noch Symbole
werden gefiltert. Das lokal vorhandene portable Release-Deployment des
Renderers umfasst 91 Dateien, 756.130 MiB und 17 RID-Verzeichnisse,
davon 341.139 MiB `.pdb`-Dateien. Auch Release deklariert sechs native
HarfBuzz-/Skia-PDBs als `runtimeTargets` mit `assetType: native`; dies ist
also kein reiner Debug-Build-Effekt. Eine rechnerische Dateiauswahl
Root/sonstige Dateien plus nur `win-x64` bzw. `win-arm64` waere
170.775/164.104 MiB gross (77.41/78.30% weniger); ohne PDBs
56.842/52.875 MiB (92.48/93.01% weniger). Das sind lokale Linux-Build-
Inventare und hypothetische Auswahlen, keine gemessenen Windows-CI-
Deployments, getestete Publish-Profile oder Laufzeitverbesserungen.
Die nativen Logs zeigen zudem starke Streuung auch spaeterer Worker:
arm64 gescriptet etwa 0.370 bis 8.725 Sekunden fuer die App-Kopie.
Ein reiner erster Kaltstart erklaert dies nicht; Antivirus, Storage und
Host-Scheduling bleiben unbewiesen. Ein x64-Disposal von 4.269 Sekunden
ist noch nicht nach Prozessende, Profilabbau und Dateiloeschung aufgeteilt.
Naechster Vorschlag: natives Windows-Release-Inventar samt kopierten
Dateien/Bytes und getrennten Cleanup-Zeiten messen, danach RID-spezifisches
Packaging gegen dieselben Smokes pruefen. Symbolauslassung getrennt
bewerten; keine brittle Dateifreigabeliste im Sicherheitslauncher und
kein gemeinsam beschreibbares Staging als vorschnelle Optimierung.
Bis KP-418 keine Code-/Policy-Aenderung und kein neuer Commit/Push/CI-Lauf.
Am 2026-11-16 um 00:29 Europe/Berlin hat der Nutzer den vorgeschlagenen
Dreischritt mit "yes" freigegeben (KP-419): zuerst native Kopierinventare
und getrennte Cleanup-Zeiten, danach architekturspezifisches Packaging
mit beibehaltenen Symbolen, zuletzt native CI und Vergleich. Phase 1 ist
implementiert und im Review APPROVED (KP-420); Packaging und Confinement
bleiben unveraendert. Solution-Build, 43 Platform-Tests und Formatpruefung
bestehen. Der erste Browser-Projektlauf hatte einen V8-Deadline-Fehler
in einem unveraenderten InlineScript-Prozesstest bei parallel laufender
Formatpruefung (1.351 bestanden, 1 fehlgeschlagen). Beide Parameterzeilen
dieses Tests bestehen isoliert danach; keine Deadlines angepasst.
Die anschliessende volle Default-Solution besteht mit 16.934 Tests,
darunter alle 1.352 Browser- und 43 Platform-Faelle; beide expliziten
CPU-Deadline-Zeilen bleiben aus. Native Windows-Evidenz lag zum lokalen
Phasenabschluss noch nicht vor; inzwischen siehe KP-422 unten.
Am 2026-11-16 um 01:13 Europe/Berlin bestaetigt der Nutzer mit
"commit and continue" die Commit-/Fortsetzungsrueckgabe (KP-421).
Die Messbaseline ist als `3ae5140acbfd18da528153f84e656518b567bc27`
committed und auf `origin/main` gepusht. Der Hook formatierte das
chronologische Protokoll und bestand alle 15 Dateien. Baseline-CI:
Linux `43779251649` (x64/arm64 samt Trunk), CodeQL `43779250921` und
Windows `43779251698` (x64/arm64) bestanden. Phase 2 ist implementiert,
nach direktem Review/Korrektur lokal validiert (KP-423) und inzwischen
als `2e3e7feade939e4a72cbdd135221b1fe6c62ee75` committed und auf
`origin/main` gepusht (KP-425). Der Workflow behaelt portable Kontroll-Smokes/Tests bei
und fuegt RID-spezifische statische/gescriptete Kandidaten-Smokes hinzu.
SDK-Publish ist Framework-dependent, ohne Apphost, Trimming oder
Symbolentfernung; isolierte Build-/Publish-Ausgaben liegen unter Renderer-
`obj/`. Ein PowerShell-Pruefer kontrolliert reale Deps-/RID-/Framework-
Metadaten, native Dateien, kompatible generische `win`/`any`-Fallbacks,
PDB-SHA-256 und kleinere Dateimenge als die portable Kontrolle.
Cross-publiziert und erneut geprueft: x64 179000984 Bytes
(170.709 MiB), arm64 172006030 (164.038 MiB), Symbole behalten.
Gegen die lokale portable Linux-Release-Ausgabe (792859287 Bytes)
sind das 77.423/78.306% weniger App-Dateibytes, keine Windows-Zeiten.
Solution-Build (null Warnungen/Fehler), Format, Workflow-Lint und erneut
alle 16.934 Default-Tests bestehen. Am 2026-11-16 um 02:16
Europe/Berlin bestaetigt der Nutzer mit "commit and continue" die
Veroeffentlichung des Versuchs und native CI-Auswertung (KP-424).
Phase 3 ist abgeschlossen: Linux `43785419766`, CodeQL `43785419328`
und Windows `43785419848` sind erfolgreich. Windows arm64 bestand erst
im unveraenderten Job-Rerun; der erste Versuch hatte vor dem Publish
einen V8-Deadline-Fehler im bestehenden Test
`ExternalClassicScriptsPauseParsingAndUseTheEffectiveBase(process: True)`.
Keine Deadline/Test-/Confinement-Aenderung; Ursache bleibt offen.

**Nativer RID-Vergleich (2026-11-16, KP-425):** Auf x64 sinkt das
App-Deployment von 756.234 auf 170.712 MiB (-77.426%), auf arm64 von
756.213 auf 164.041 MiB (-78.308%). Je Kandidat 47 statt 91 Dateien;
die architekturpassenden nativen PDBs sind SHA-256-identisch mit den
Paketdateien. Runtime bleibt pro Worker 80.381/89.456 MiB.
Sechs App-plus-Runtime-Kopien je Smoke sinken von 4.9020 auf
1.4712 GiB (x64) bzw. 4.9551 auf 1.4853 GiB (arm64).
Das sind logische Dateilaengen, keine gemessenen physischen I/O.

| CLI-Wandzeit, Sekunden | Portable Kontrolle | RID-Kandidat | Beobachtete Aenderung |
| ---------------------- | -----------------: | -----------: | --------------------: |
| x64 statisch           |             18.254 |        8.744 |               -52.10% |
| x64 gescriptet         |             14.753 |       23.476 |               +59.12% |
| arm64 statisch         |             35.125 |       10.568 |               -69.91% |
| arm64 gescriptet       |             34.704 |       10.260 |               -70.44% |

Je Vergleich stammen Kontrolle und Kandidat aus demselben Job:
x64 Versuch 1, arm64 Versuch 2. Die App-Kopierzeiten sinken in allen
vier Faellen, aber x64 gescriptet wird insgesamt langsamer:
Runtime-Kopien 1.234 -> 5.512 Sekunden, Hello 2.955 -> 7.313.
Arm64-static hat trotz kleinerem Deployment 3.498 statt 1.346 Sekunden
Staging-Loeschung. Phasen sind Summen ueber Worker/Requests, CLI ist
Wandzeit; verschachtelte Phasen nicht addieren. Reihenfolge/Cache und
Host-Streuung verhindern einen garantierten Packaging-Speedup; keine
Antivirus-/Storage-Ursache bewiesen. Publish/Validierung kosten zusaetzlich
5/1 Sekunden (x64) bzw. 6/1 (arm64); inklusive beider zusaetzlicher
Kandidaten-Smokes ist das Experiment 41/31 Sekunden Schrittzeit.
Nur CI-Kandidat, keine Umstellung der Standardbereitstellung oder
Entfernung portabler Kontrollen beschlossen. Naechste Optionen:
RID-Publish gezielt uebernehmen oder weitere gezielte Diagnostik.
RID-Publish-Uebernahme bleibt offen. Der Nutzer hat inzwischen die
vorgeschlagene zusaetzliche Instrumentierung freigegeben (KP-427).

**Fortgesetzte Startup-Spur (2026-11-16, KP-426):** Auf "commit and
continue" wurden die KP-425-Ergebnisse als `5cede2c` committed
(zwei Dateien, Hook ohne Befund; bisher nicht gepusht) und der
bestehende Startup-Pfad lesend weiter untersucht. Im langsameren
x64-scripted-Kandidaten steigt nicht nur die Runtime-Kopie:
Worker 2 braucht 0.982 s Profil/Storage (portable: 0.006 s),
2.457 s App-Kopie (0.382), 4.008 s Runtime-Kopie (0.274)
und 3.429 s Hello (0.431). Das ist kein isolierter V8-/Script-Befund.
Der Worker bestaetigt Confinement und laedt HarfBuzz-/Skia-Fonts vor
Hello; das V8-Dokument und Script-Ausfuehrung beginnen erst nach der
ersten Render-Anforderung. Hello misst somit nicht reine IPC-Zeit.
Der Host liest stdout/stderr asynchron ueber synchron angelegte
Windows-Pipes; Pool-/Scheduling-Wartezeit ist eine offene Hypothese,
keine nachgewiesene Starvation. Das damalige Runtime-Timing trennte weder
hostfxr/shared-runtime/dotnet.exe noch Kopieren von Enumeration/Metadaten.
Naechster Vorschlag: diese Kopieranteile und den Worker-vor-Hello-Pfad
diagnostisch aufteilen, danach begrenzt und reihenfolgeausgeglichen
nativ vergleichen. Keine Code-/Policy-Aenderung oder neue CI aus
dieser lesenden Fortsetzung. Am 2026-11-16 um 03:11 Europe/Berlin
bestaetigt "commit and continue" die Frage nach dieser Instrumentierung
(KP-427). Die Erkenntnisse samt Freigabe sind als `2b3b8df` committed,
ebenso wie `5cede2c` noch nicht gepusht. Phase 1 (Runtime-Komponenten
und Kopier-/Metadatenanteile) ist inzwischen lokal implementiert,
getestet und direkt reviewed (KP-428). Worker-vor-Hello-Diagnostik und explizite
reihenfolgeausgeglichene native Vergleiche folgen als getrennte
Phasen mit Review-/Commit-Rueckgabe. Keine Freigabe fuer geaenderte
Deployment-Defaults, abgeschwaechte Schutzregeln oder laengere Deadlines.

**Phase 1 lokal abgeschlossen (2026-11-16, KP-428):** Unter dem
bestehenden Smoke-Opt-in bleiben `windows.copy-runtime` und Inventar v1
erhalten. Zusaetzlich gibt es pro `hostfxr`, `shared-runtime` und `dotnet`
drei feste Records unter `windows.copy-runtime.<Komponente>`:
Komponenten-Wandzeit, `.copy` fuer File.Copy und `.metadata` fuer
direkt gemessene benannte Dateisystem-Metadaten-/Enumerationsoperationen.
Keine Metadaten-Schaetzung durch Subtraktion; Pfad-/Buchhaltungs- und
sonstiger Traversierungs-/Instrumentierungsaufwand bleiben Restanteile.
Keine per-Datei-Records, Pfade oder neue Messarbeit bei ausgeschalteter
Diagnostik. Kopierreihenfolge, Reparse-Skips, Inventare, Fehlersignale
und verzogerte Vollbuffer-Fehlermeldung bleiben erhalten.
Vier neue Plattform-Tests decken echte synthetische Laufzeitbaeume,
Disabled-Vergleich, Teilfehler und Vollbuffer ab. Solution-Build
(null Warnungen/Fehler), alle 16.938 Default-Tests (47 Platform,
1.352 Browser) und Solution-Formatverifikation bestehen; beide
expliziten CPU-Deadline-Zeilen bleiben aus. Direktes Review ohne
blockierende Befunde. Die neuen Windows-nativen Assertions sind
inzwischen durch x64/arm64-CI belegt (KP-438); die damaligen lokalen
Linux-Tests waren kein solcher Nachweis.
Am 2026-11-16 um 03:44 Europe/Berlin bestaetigt der Nutzer mit
"commit and continue" die Commit-Rueckgabe und den Beginn von Phase 2
(KP-429). Die neun Phase-1-Dateien sind als `b2760ea` committed
(Hook: neun Dateien ohne Befund), bisher nicht gepusht. Phase 2
ist nun lokal implementiert, vollstaendig validiert und reviewed
(APPROVED, KP-431; verworfene Zwischenloesungen in KP-430).
Begrenzte Worker-Startup-Batches ueber stderr ergaenzen Host-Hello-
Empfang/Validierung ohne geaendertes stdout-IPC. Erfolgreicher Hello
wartet unter der bestehenden Exchange-Deadline auf einen terminalen
Erfassungszustand. Cleanup erfolgt ausserhalb des Renderer-Locks;
der optionale Diagnose-Drain-Join ist separat auf eine Sekunde
begrenzt und meldet `worker-drain-timeout`. Native Bereinigung bleibt
synchron und erhaelt dadurch keine neue globale Zeitgarantie.
Finaler Solution-Build: null Warnungen/Fehler; alle 16.959
Default-Tests in 14 Projekten bestanden, darunter 1.373 Browser
und 47 Platform. 34 fokussierte SmokeTiming-Faelle bestehen;
beide CPU-Deadline-Zeilen bleiben explizit. Solution-Format,
zwoelfdateiger Scoped-Trunk-Check und Diff-Check bestehen.
Native Windows-x64/arm64-Close-/Cancel-Assertions und die neuen
Laufzeitrecords sind inzwischen durch CI belegt (KP-438).
Am 2026-11-16 um 06:33
Europe/Berlin bestaetigt der Nutzer "commit and continue" (KP-432):
Phase 2 ist als `9e34f7b` committed (Hook: zwoelf Dateien ohne
Befund), danach beginnt Phase 3 mit expliziten
reihenfolgeausgeglichenen nativen Vergleichen (KP-433).
Die vier damaligen lokalen Commits sind inzwischen mit der
Vergleichsautomation veroeffentlicht (KP-437).
Die Vergleichsschleife ist implementiert: je nativer Architektur
acht CLI-Laeufe, fuer static/scripted portable-RID und RID-portable,
mit `smoke-compare v=1`-Markern und Abbruch bei Exit-/Diagnostikfehlern.
Mocks und drei echte harmlose native Subprozess-Stub-Faelle bestehen;
erneut bestehen Build, alle 16.959 Default-Tests und Solution-Format.
Der erste konfigurierte Trunk-Check verwarf oeffentliche Dispatch-
Inputs mit CKV_GHA_7 (KP-434). Das ist ohne Unterdrueckung behoben:
Der neue rein manuelle `windows-startup-comparison.yml` ohne Inputs
ruft den bestehenden Windows-Workflow mit internem false-default
`workflow_call`-Schalter auf. Bestehende Push-/PR-/Default-Dispatch-
Laeufe bleiben unveraendert. Strukturtest, finales direktes Review
(APPROVED) und konfigurierter Fuenfdatei-Trunk-/Diff-Check bestehen
(KP-435). Am 2026-11-16 um 08:00 Europe/Berlin bestaetigt
"commit and continue" ausdruecklich die Frage nach Commit, Push
und nativen Vergleichen samt Auswertung (KP-436).
Die Automation ist als `4838b52` committed (Hook: fuenf Dateien
ohne Befund) und zusammen mit den vier lokalen Commits auf main
gepusht. Der explizite Vergleichslauf `43820945522` besteht auf
diesem Stand: x64-Job `133143633852`, arm64-Job `133143634126`.
Normale Windows-CI `43820934692`, Linux-CI `43820934683` und
CodeQL `43820934173` bestehen ebenfalls (KP-438). Die normalen
Windows-Jobs ueberspringen die neuen Vergleiche tatsaechlich;
die manuellen Jobs fuehren sie erfolgreich aus.

**Native Ergebnisse (KP-438, Sekunden aeussere CLI-Wandzeit):**
Je Zelle Bereich aus den beiden Reihenfolgen, keine Statistik.

| Windows | Szenario |      Portable |           RID |
| ------- | -------- | ------------: | ------------: |
| x64     | static   | 13.416-17.524 |   7.824-8.130 |
| x64     | scripted | 14.804-17.148 |   9.021-9.122 |
| arm64   | static   | 35.962-38.793 | 10.434-12.195 |
| arm64   | scripted | 34.960-39.529 | 12.196-12.361 |

Static/RID hat in diesen Messungen auf beiden Architekturen die
niedrigsten CLI-Zeiten (KP-440). Static und scripted pruefen jedoch
verschiedene Funktionspfade; daraus folgt keine Freigabe, Script-
Abdeckung zu entfernen. RID-Paketierung ist fuer beide Pfade die
vorgeschlagene Optimierung, kein Ersatz von scripted durch static.

Alle acht gepaarten Vergleiche zeigen 38.4-73.1% geringere
CLI-Wandzeit fuer RID; 16 fehlerfreie Aufrufe, 96 Inventare und
80 vollstaendige Worker-Batches sind geprueft.
Die Wiederholung bedeutet sechs separate Staging-Vorgaenge je
CLI-Smoke-Aufruf, je einer pro Worker-Lifetime; acht explizite
Vergleichsaufrufe je Architektur ergeben 48, beide Architekturen
zusammen 96. Das ist nicht die Zahl aller Stagings im gesamten CI-Job
(Klarstellung KP-439). Auf ARM64 liegt
portable App-Staging bei 15.131-24.834 s je CLI, RID bei
0.652-1.592 s. Runtime-Ausreisser sind nun eingegrenzt:
x64 Worker 4 verbringt 8.489 s in Shared-Runtime-File.Copy-
Aufrufen, nur 0.010 s in deren Metadatenoperationen.
ARM64 hat zusaetzlich hostfxr-Metadaten-Ausreisser bis 2.255 s
summiert. x64-Cleanup zeigt separat bis 5.843 s fuer ein
Staging-Directory.Delete und 2.966 s fuer einen
DeleteAppContainerProfile-Aufruf. Worker-Confinement-Verifikation
bleibt etwa 3-4 ms je erfolgreichem Worker.

Belegt sind wiederholtes Deployment-Staging und variable
Cleanup-/Runtime-Operationen, keine Defender-/Storage-/Scheduling-
Ursache. Host-Hello bleibt 2.0-5.5 s summiert je CLI; unabhaengige
Worker-/Host-Uhren erlauben keine praezise Subtraktion zur
IPC-Latenz. Der sechste Worker schliesst weiterhin vor vollem
Handshake. Am 2026-11-16 um 08:36 Europe/Berlin gibt der Nutzer
RID als Default frei und erlaubt nuetzliche weitere Varianten als
explizite Langtests (KP-441). Das Ziel ersetzt den zuvor offenen
RID-Vorschlag; der Nutzer genehmigt am 2026-11-16 um 09:48
Europe/Berlin mit "yes" auch den konkreten Dreiphasenplan (KP-442).
Static und scripted bleiben beide im normalen
Umfang; keine Schutzabschwaechung ist beschlossen.
Ein Codespaces-Prebuild laeuft beim letzten Check noch separat.
Die drei Instrumentierungsschritte samt nativer Auswertung sind
abgeschlossen; Ergebnisnachtrag bleibt lokal uncommitted.

**Genehmigter Rollout (KP-441/KP-442):** Erst die normalen Windows-
CLI-Smokes auf validierte native RID-Publishes umstellen, dann deren
Browser-Test-Renderer-Fixtures konsistent umstellen, zuletzt portable
Kontrollen und Wiederholungen ausschliesslich im vorhandenen manuellen
Vergleich erhalten und Dokumentation/Validierung abschliessen.
Die Fixtures kopieren bisher portables Build-Output; ein normaler Test
startet auch den Apphost. Dessen Abdeckung muss bei DLL-only-Publish
explizit erhalten bleiben. Linux und allgemeine lokale Build-/CLI-
Vertraege sowie die bereits expliziten CPU-Deadline-Zeilen bleiben
unveraendert. Phase 1 ist lokal umgesetzt und reviewed (APPROVED,
KP-443): genau zwei automatische Multiprocess-Smokes verwenden das
validierte RID-Deployment; portable Duplikate entfallen, der manuelle
Acht-Aufrufe-Vergleich bleibt unveraendert. Solution-Build ohne
Warnungen/Fehler, alle 16.959 Default-Tests und Solution-Format
bestehen. Konfigurierte Linter fuer Workflow und beide Guides
bestehen ebenfalls. Neue native Windows-Ausfuehrung erst nach
Veroeffentlichung. Der Nutzer bestaetigt am 2026-11-16 um 10:13
Europe/Berlin mit "yes" Commit von Phase 1 und Fortsetzung mit
Phase 2 (KP-444). Phase 1 ist als `a70af56` committed
(Hook: fuenf Dateien ohne Befund), nicht gepusht.
Phase 2 ist lokal abgeschlossen und direkt reviewed (APPROVED,
KP-445/KP-446). `RendererFixtureSource` waehlt in Windows-CI das vorher
validierte RID-Publish; normale lokale Builds bleiben portabel.
Das SDK-Publish hat nun `UseAppHost=true`, sodass der unveraenderte
Apphost-Test weiter ausfuehrbar bleibt. Die Renderer-Fixture wird
nach Vorabpruefung exakt ersetzt; Peer bleibt separat. Release-
Browser-Tests: 1.373 bestanden; Solution-Build ohne Warnungen/Fehler,
alle 16.959 Default-Tests und Solution-Format bestanden. Konfigurierte
Linter fuer die fuenf Implementierungs-/Guide-Dateien ohne Befund.
Am 2026-11-16 um 11:11 Europe/Berlin genehmigt der Nutzer mit
"yes" den Commit von Phase 2 und Fortsetzung mit Phase 3 (KP-447).
Phase 2 ist als `b60e215` committed (Hook: sieben Dateien ohne
Befund). Phase 3 ist lokal dokumentiert und direkt reviewed
(APPROVED, KP-448): Portable/Balanced-Laeufe bleiben im vorhandenen
manuellen Vergleich, CPU-Deadline-Zeilen im separaten manuellen
Workflow. Keine neue Workflow-/Laufzeitaenderung in Phase 3.
Strukturpruefung und konfigurierte Dokumentations-Linter bestehen;
die 16.959 Tests sowie Build/Format wurden nach letzter Codeaenderung
in Phase 2 erfolgreich ausgefuehrt und nicht fuer reine Docs wiederholt.
Alle drei lokalen Rollout-Schritte sind damit umgesetzt. Am
2026-11-16 um 11:39 Europe/Berlin genehmigt der Nutzer mit "yes"
den Phase-3-Dokumentationscommit, Push aller drei Commits und
CI-Validierung (KP-449). Phase 3 ist als `c6d2af4` committed
(Hook: vier Dateien ohne Befund), alle drei Rollout-Commits sind
auf main gepusht (KP-450). Linux-CI `43851000841`, CodeQL
`43850999990` und Windows-CI `43851000660` bestehen (KP-452).
Windows x64 besteht nach einem unveraenderten gezielten Job-Rerun;
der initiale Fehler im lokalen Singleprocess-Shell-Test bleibt in
KP-451 dokumentiert und wurde nicht durch Code-/Timeoutaenderungen
umgangen. Der Test besteht im Rerun ebenfalls.
Neue native Logs belegen auf x64 und arm64 RID-Publish/Validierung,
Fixture-Auswahl vor allen Browser-Tests, Apphost-Test und beide
RID-Smokes; die manuellen Vergleiche sind skipped. 24 Smoke-Inventare
belegen jeweils 48 App-Dateien ohne andere RID-Assets, mit erhaltenen
Symbolen. Damit sind alle drei Rollout-Schritte veroeffentlicht und
auf beiden Windows-Zielen validiert. Der separate Codespaces-Prebuild
laeuft beim letzten Check noch; kein Test-/Confinement-Ergebnis daraus
abgeleitet. Ergebnisnachtraege bleiben lokal uncommitted; temporaere
CI-Artefakte sind entfernt. Keine weitere Implementierung ausstehend.
Kein optionaler Vergleichs-/Deadline-Dispatch beauftragt.

**Historische portable Kopierbaseline (2026-11-16, KP-422):** Vor
RID-Default wurde pro Worker ein 91-Dateien-App-Deployment von
756.234 MiB (x64) bzw. 756.213 MiB (arm64) kopiert. Andere RIDs stellten
585.355/592.026 MiB (77.404/78.288% der App); die ueberlappende
Symbol-Teilmenge betraegt auf beiden 341.143 MiB. Runtime/hostfxr/Host
kommen mit 80.381/89.456 MiB hinzu. Die sechs Worker der damaligen
portablen statischen/gescripteten Smokes meldeten identische Mengen:
4.902/4.955 GiB logische App-plus-Runtime-Dateibytes pro portablem
Smoke, ohne separat kopierte Fonts. Aktuelle RID-Zahlen siehe KP-452.
Keine Aussage ueber physische Datentraeger-I/O oder Laufzeit-Speedup.
Die damaligen Cleanup-Phasen ordneten den x64-scripted-Ausreisser konkret
der Staging-Loeschung zu: Worker 4 dort 3.625 Sekunden, waehrend
Profilabbau im ganzen Smoke nur 0.015 Sekunden summiert. Andererseits
lag x64-static dort bei 12.377 summierten Hello-Sekunden (Worker 3:
6.952 Sekunden); Packaging allein ist keine bewiesene Loesung fuer
Handshake-Streuung. Nativ bestanden auch Inventar-/Cleanup- und
Vollbuffer-Ownership-Tests auf beiden Windows-Architekturen.

**Aktueller Standards-Pass (2026-11-15; Pass 8 abgeschlossen):**
Pass 2 ergaenzt bounded CSS Color 4 `lch()`. Numerische/prozentuale
Lightness und Chroma, Hue-Winkel, `none` und Slash-Alpha werden geparst;
Prozent-Chroma bildet 100% auf 150 ab, negatives Chroma wird geklemmt und
Hue bei Chroma <= 0.0015 als powerless behandelt. LCH wird in Lab D50
umgerechnet und durch die vorhandene Bradford-/sRGB-Pipeline verarbeitet.
L-Endpunkte ergeben schwarz/weiss; andere Ergebnisse ausserhalb sRGB bleiben
Diagnosen. 19 fokussierte Faelle und alle 1.545 CSS-Tests bestehen.
Debug-/Release-Builds melden null Warnungen/Fehler; beide Volltestlaeufe
bestehen mit je 16.811 Tests. Formatverifikation und `git diff --check`
bestehen. CSS Color wird grob mit ~50% geschaetzt. Commit `ef8a24d` ist auf
`main` gepusht. Linux development (Run 43652149635), Windows confinement
(Run 43652149569) und CodeQL (Run 43652149331) sind erfolgreich. Pass 1
(`7d914ce`) hatte ebenfalls gruenes CI. Pass 3 implementiert bounded
CSS-Color-4-`oklab()`. CSS Color (~55%) bleibt die hoechste actionable
Teilspezifikation nach dem ausserhalb Phase 11w blockierten ECMAScript (~90%),
vor CSS Syntax (~35%). 18 fokussierte Faelle, alle 1.563 CSS-Tests,
Debug-/Release-Builds ohne Warnungen/Fehler und beide Volltests mit je 16.829
Tests bestehen. Formatverifikation und `git diff --check` bestehen. Commit
`bc1cf94` ist gepusht. Linux development (Run 43655547872), Windows
confinement (Run 43655547863) und Push on main/CodeQL (Run 43655547850) sind
gruen, einschliesslich x64/arm64-Pruefungen. Pass 4 implementiert bounded CSS-Color-4-`oklch()`; CSS Color (~60%) bleibt
vor CSS Syntax (~35%) die hoechste actionable Teilspezifikation, waehrend
ECMAScript (~90%) ausserhalb Phase 11w bleibt. 20 fokussierte Faelle, alle
1.583 CSS-Tests, Debug-/Release-Builds ohne Warnungen/Fehler und beide
Volltests mit je 16.849 Tests bestehen; Formatverifikation und
`git diff --check` bestehen. Commit `4e0779e` ist auf `main` gepusht.
Linux development (Run 43658994675), Windows confinement (Run 43658994676)
und CodeQL (Run 43658994696) sind erfolgreich. Nach Pass 4 bleiben sechs der
zehn gewuenschten Passes. Siehe
[KP-356](./Kontextprotokoll.md#kp-356---css-color-lab-function),
[KP-357](./Kontextprotokoll.md#kp-357---css-color-lch-selection) und
[KP-358](./Kontextprotokoll.md#kp-358---css-color-oklab-selection) sowie
[KP-359](./Kontextprotokoll.md#kp-359---pass-3-ci-and-oklch-selection) und
[KP-360](./Kontextprotokoll.md#kp-360---oklch-ci-bestaetigt).
Pass 5 waehlt begrenztes CSS-Color-5-`color-mix(in srgb, ...)`, weil damit
ein statischer Farbverbraucher fuer die bislang ungenutzte CSS-Color-4-
Interpolation entsteht. Exakt zwei Farben und explizites
`srgb` werden mit CSS Values 5 Prozentnormalisierung und CSS Color 4
premultipliziertem Alpha gemischt; Standard-Oklab und andere Mischraeume
bleiben Diagnosen. CSS Color 5/Values 5 wurden neu in das Manifest aufgenommen
und am 2026-11-15 geladen. 20 fokussierte und alle 1.603 CSS-Tests bestehen;
Debug-/Release-Builds sind warnungs-/fehlerfrei und beide Volltests bestehen
16.869 Tests; Format und `git diff --check` bestehen. Commit `c03d6bb` ist auf
`main` gepusht. Linux development (Run 43663310025), Windows confinement (Run 43663310066) und CodeQL (Run 43663309766) sind erfolgreich. Nach Pass 5
verbleiben fuenf der zehn gewuenschten Passes. Siehe
[KP-361](./Kontextprotokoll.md#kp-361---bounded-color-mix-selection) und
[KP-362](./Kontextprotokoll.md#kp-362---color-mix-lokale-validierung) sowie
[KP-363](./Kontextprotokoll.md#kp-363---color-mix-ci-bestaetigt).
Pass 6 setzt die CSS-Color-5-Vorgabe um, dass ein fehlender
`color-mix()`-Interpolationsraum Oklab bedeutet; `in oklab` wird ebenfalls
unterstuetzt. Die CSS-Color-4-Konvertierung und alpha-premultiplizierte
Interpolation arbeiten mit der bestehenden 8-bit-sRGB-Farbform. Ergebnisse
ausserhalb des sRGB-Gamuts bleiben Diagnosen. CSS Color 4 (`checkedAt`
2026-11-07), CSS Color 5 und CSS Values 5 (`checkedAt` 2026-11-15) sind
frisch im lokalen Cache. 26 fokussierte ColorMix-Tests, alle 1.609 CSS-Tests
sowie Debug-/Release-Solution-Builds ohne Warnungen/Fehler bestehen. Beide
Debug-/Release-Volltests bestehen 16.875 Tests, ebenso die Formatverifikation
und `git diff --check`. Ein erster Debug-Volltestlauf traf einen V8-Deadline-
Timeout in einem isolierten Prozessrenderer-Test; der isolierte Test und der
vollstaendige Debug-Wiederholungslauf bestanden. Commit `8df2c07` ist gepusht.
Windows confinement (Run 43668545852) und CodeQL (Run 43668545368) bestehen.
Linux-x64/-arm64-Builds, Tests und native Smokes bestehen; der Linux-Workflow
ist wegen des separaten Trunk-Checks fehlgeschlagen, dessen `check uploads` /
`check all`-Modus nicht mehr unterstuetzt wird. Vier angeforderte Inkremente
verbleiben. Siehe [KP-364](./Kontextprotokoll.md#kp-364---oklab-color-mix-selection),
[KP-365](./Kontextprotokoll.md#kp-365---oklab-color-mix-lokale-validierung)
und [KP-366](./Kontextprotokoll.md#kp-366---oklab-color-mix-commit-push-und-ci).
Pass 7 unterstuetzt zusaetzlich explizites `in srgb-linear` fuer zwei statische
Farben: transfer-dekodierte sRGB-Kanaele werden mit den bestehenden
normalisierten Gewichten und premultipliziertem Alpha linear gemischt und per
CSS-Transferfunktion in die 8-bit-sRGB-Ausgabe umgewandelt. 30 fokussierte
ColorMix-Faelle, alle 1.613 CSS-Tests, Debug-/Release-Builds ohne Warnungen/
Fehler und beide Volltests mit je 16.879 Tests bestehen; Formatverifikation
und `git diff --check` bestehen. Commit `7abf658` ist gepusht; Linux development
(Run 43672624715), Windows confinement (Run 43672624685) und CodeQL
(Run 43672624384) sind erfolgreich. Windows x64 bestand nach einem
fehlgeschlagenen ersten Versuch des langsamen Browsertests
`UpgradeInsecureRequestsUpgradesPageLinkAndRedirectBeforeTransport` im
Fehljob-Rerun; Windows ARM64 bestand ebenfalls. Es waren keine Codeaenderungen
noetig. Pass 7 ist abgeschlossen; drei angeforderte Inkremente verbleiben.
Pass 8 ergaenzt explizites `in lab` fuer die bestehende bounded
Zwei-Farben-`color-mix()`-Form: Umwandlung von 8-bit-sRGB ueber D50 Lab,
vorhandene Prozentnormalisierung und premultiplizierter Alpha, Rueckwandlung
mit der bestehenden sRGB-Gamutdiagnose. Andere Interpolationsraeume,
verschachtelte Mixe, fehlende Komponenten und Gamut-Mapping bleiben
unsupported. CSS Color 4 (`checkedAt` 2026-11-07) und CSS Color 5
(`checkedAt` 2026-11-15) sind frisch. Fuenf neue fokussierte Regressionen decken
neutrale/chromatische Lab-Mixe, Gewichte, Alpha und sRGB-Gamutdiagnosen ab;
die gueltigen Faelle schlugen vor Implementierung fehl. Alle 35 fokussierten
ColorMix-Tests und 1.618 CSS-Tests bestehen. Debug-/Release-Solution-Builds
haben null Warnungen/Fehler; beide Volltests bestehen 16.884 Tests;
Formatverifikation und `git diff --check` bestehen. Commit `ba54d25` ist
gepusht; Linux development (Run 43677500402), Windows confinement
(Run 43677500428) und CodeQL (Run 43677500007) sind erfolgreich, einschliesslich
Windows x64/ARM64. Pass 8 ist abgeschlossen; zwei angeforderte Inkremente
verbleiben. Pass 9 ergaenzt explizites `in xyz-d65` und den CSS-Color-4-Alias
`in xyz` fuer zwei statische Farben. Die linearen sRGB-Eingaben werden nach
XYZ D65 konvertiert, mit bestehenden Gewichten und premultipliziertem Alpha
gemischt und ueber die bestehende Gamutdiagnose zur 8-bit-sRGB-Ausgabe
zurueckgewandelt; Gamut-Mapping bleibt unsupported. `xyz-d50` bleibt in
`color-mix()` unsupported. CSS Color 4 (`checkedAt` 2026-11-07) und CSS Color 5
(`checkedAt` 2026-11-15) sind frisch. Die fuenf fokussierten XYZ-Faelle,
alle 41 color-mix-Faelle und 1.624 CSS-Tests bestehen; Debug-/Release-
Solution-Builds haben null Warnungen/Fehler; beide Volltests bestehen 16.890
Tests; Formatverifikation und Whitespacepruefung bestehen. Commit `ee1d18b`
ist gepusht; Linux Run
43681056081, Windows Run 43681056228 (x64/ARM64) und CodeQL Run 43681055444.
Pass 10 (letztes angefordertes Inkrement) implementiert `in xyz-d50` fuer
dieselbe begrenzte Zwei-Farbenform: sRGB-Eingaben werden ueber
Bradford-adaptiertes XYZ D50 gemischt und via bestehender D50-zu-sRGB-
Gamutpruefung ausgegeben. Fuenf fokussierte Regressionen, alle 45
`color-mix()`-Tests und 1.628 Engine.Css-Tests bestehen; Debug-/Release-
Solution-Builds haben null Warnungen/Fehler, beide Volltests bestehen 16.894
Tests, Formatverifikation und `git diff --check` bestehen. Commit, Push und
Remote-CI sind abgeschlossen: Commit `fd495ec` ist gepusht; Linux-Run
43684336479, Windows-Run 43684336496 (x64/ARM64) und CodeQL-Run 43684336395
sind erfolgreich. Damit ist der angeforderte Zehn-Pass-Auftrag abgeschlossen.
CSS Color 4 (`checkedAt` 2026-11-07) und CSS Color 5 (`checkedAt` 2026-11-15)
sind frisch. Siehe
[KP-367](./Kontextprotokoll.md#kp-367---linear-srgb-color-mix-als-pass-7-gewaehlt)
sowie [KP-370](./Kontextprotokoll.md#kp-370---lab-color-mix-als-pass-8-gewaehlt),
[KP-371](./Kontextprotokoll.md#kp-371---lab-color-mix-implementiert-und-lokal-validiert),
[KP-372](./Kontextprotokoll.md#kp-372---lab-color-mix-ci-bestaetigt) und
[KP-373](./Kontextprotokoll.md#kp-373---xyz-d65-color-mix-als-pass-9-gewaehlt),
[KP-374](./Kontextprotokoll.md#kp-374---xyz-d65-color-mix-implementiert-und-lokal-validiert),
[KP-375](./Kontextprotokoll.md#kp-375---xyz-d65-color-mix-ci-bestaetigt),
[KP-376](./Kontextprotokoll.md#kp-376---xyz-d50-color-mix-als-pass-10-gewaehlt)
und [KP-377](./Kontextprotokoll.md#kp-377---xyz-d50-color-mix-lokal-validiert)
sowie [KP-378](./Kontextprotokoll.md#kp-378---xyz-d50-color-mix-ci-bestaetigt)
und [Missing](../plans/Missing.md).

**Vorheriger Standards-Pass (2026-11-14; lokal validiert, gepusht, CI-gruen):**
Pass 10 ergaenzt bounded `color(prophoto-rgb ...)` mit der CSS-Color-
Transferkurve (Gamma 1,8 und linearer Toe), dem ProPhoto-zu-D50-XYZ-Matrixpfad
und bestehender Bradford-D50-zu-D65-/sRGB-Gamutkonversion. Vierzehn
fokussierte Tests, alle 1.511 CSS-Tests, Debug-/Release-Solution-Builds ohne
Warnungen/Fehler und beide Volltests (16.777) bestehen; Formatverifikation
und `git diff --check` bestehen. Feature-Commit `e9211b4` ist auf `main`
gepusht; Linux development (Run 43641784100), Windows confinement
(Run 43641784214) und CodeQL (Run 43641783907) sind erfolgreich. Zehn
angeforderte Inkremente sind lokal validiert und einzeln committed/gepusht;
Pass 10 ist der letzte. Siehe
[KP-355](./Kontextprotokoll.md#kp-355---css-color-prophoto-rgb-function),
[Missing](../plans/Missing.md) und die Guide-Dokumentation.

**Aktueller Standards-Pass (2026-11-14; gepusht, CI laeuft):**
Pass 9 ergaenzt `color(xyz-d50 ...)` mit der Bradford-D50-zu-D65-Anpassung
vor der bestehenden XYZ-D65-zu-linearem-sRGB-Konversion. Endliche
Zahlen-/Prozentkanaele bleiben ungeklemmt; Ergebnisse ausserhalb des sRGB-
Gamuts bleiben Diagnosen. Zwoelf fokussierte Tests, alle 1.497 CSS-Tests,
Debug-/Release-Solution-Builds ohne Warnungen/Fehler und beide Volltests
(16.763) bestehen; Formatverifikation und `git diff --check` bestehen.
Feature-Commit `10c7b37` ist gepusht; Linux development (Run 43637147140),
CodeQL (Run 43637147000) und Windows x64 bestehen. Windows ARM64 fiel in einem
unveraenderten External-Script-Browsertest durch ein ueberschrittenes
V8-Ausfuehrungsdeadline fehl. Der Windows-Workflow wurde erfolgreich
wiederholt; Windows x64/ARM64 sind gruen (Run 43637147165).
Der lokale CSS-Color-Cache ist laut Manifest am 2026-11-07 geprueft und frisch.
Zwei der zehn angeforderten Inkremente bleiben; jeder Pass soll separat
committed und gepusht werden. Siehe [KP-354](./Kontextprotokoll.md#kp-354---css-color-d50-xyz-function),
[Missing](../plans/Missing.md) und die Guide-Dokumentation.

**Aktueller Standards-Pass (2026-11-14; gepusht, CI-gruen):**
Pass 8 fuegt `color(xyz-d65 ...)` und das D65-Alias `color(xyz ...)` hinzu.
Endliche Zahlen-/Prozentkanaele werden nicht auf [0,1] begrenzt; nach direkter
XYZ-D65-zu-linearem-sRGB-Konversion werden nur Ausgaben im sRGB-Gamut
akzeptiert. Dreizehn fokussierte und alle 1.486 CSS-Tests bestehen. Debug- und
Release-Solution-Builds bestehen ohne Warnungen/Fehler; beide Volltests
bestehen mit 16.752 Tests. Formatverifikation und `git diff --check` bestehen.
Feature-Commit `efe9268` ist gepusht; Linux development (Run 43633273119),
Windows confinement (Run 43633272906) und CodeQL (Run 43633272613) bestehen.
Pass 8 ist abgeschlossen; zwei der zehn Inkremente bleiben. Der lokale
CSS-Color-Cache ist laut Manifest am 2026-11-07 geprueft und frisch. Siehe
[KP-353](./Kontextprotokoll.md#kp-353---css-color-d65-xyz-function),
[Missing](../plans/Missing.md) und die Guide-Dokumentation.

**Vorheriger Standards-Pass (2026-11-14; gepusht, CI-gruen):**
Pass 7 ergaenzt begrenztes `color(rec2020 ...)`. Die Eingabekanaele werden
mit dem Rec.-2020-Exponenten 2,4 linearisiert und ueber die D65-Konversionsmatrix
in lineares sRGB umgerechnet. Werte ausserhalb des sRGB-Gamuts bleiben
Diagnosen; die statische Ausgabe bleibt gerundetes 8-bit-sRGB. Dreizehn
fokussierte und alle 1.473 CSS-Tests bestehen. Debug-/Release-Solution-Builds
und beide Volltests (16.739) bestehen; Formatverifikation und `git diff --check`
bestehen. Feature-Commit `26f354f` ist gepusht; Linux development
(Run 43629055052), Windows confinement (Run 43629055107) und CodeQL
(Run 43629054825) bestehen. Pass 7 ist abgeschlossen; drei der zehn
Inkremente bleiben. Siehe
[KP-352](./Kontextprotokoll.md#kp-352---css-color-rec-2020-function).

Pass 6 fuegt begrenztes `color(a98-rgb ...)` hinzu. Die Kanaele werden mit dem
A98-Transferexponenten linearisiert und ueber die CSS-Color-D65-Matrizen in
den sRGB-Farbraum konvertiert. Ergebnisse ausserhalb des sRGB-Gamuts bleiben
Diagnosen; die Ausgabe ist gerundetes 8-bit-sRGB. Dreizehn fokussierte Tests
und alle 1.460 CSS-Tests bestehen. Debug-/Release-Solution-Builds bestehen ohne
Warnungen/Fehler; beide Volltests bestehen mit 16.726 Tests.
Formatverifikation und `git diff --check` bestehen. Feature-Commit `b0ef6e6`
ist gepusht; Linux development (Run 43625000259), Windows confinement
(Run 43625000194) und CodeQL (Run 43624999793) bestehen. Siehe
[KP-351](./Kontextprotokoll.md#kp-351---css-color-a98-rgb-function),
[Missing](../plans/Missing.md) und die Guide-Dokumentation.

**Vorheriger Standards-Pass (2026-11-14; gepusht, CI-gruen):**
Pass 5 ergaenzt lineares Display-P3: `color(display-p3-linear ...)` nutzt
dieselben Primaerfarben und denselben D65-Weisspunkt wie `display-p3`, aber
keine Eingangs-Gammakodierung. Ausserhalb des sRGB-Gamuts liegende Werte bleiben
Diagnosen, nicht gamut-gemappt. Dreizehn fokussierte Tests und alle 1.447
CSS-Tests bestehen. Debug-/Release-Solution-Builds bestehen mit null
Warnungen/Fehlern; beide Volltests bestehen mit 16.713 Tests. Formatverifikation
und `git diff --check` bestehen. Feature-Commit `cc550b8` ist gepusht; Linux
development (Run 43621051210), Windows confinement (Run 43621051186; x64/arm64)
und CodeQL (Run 43621051599) sind erfolgreich. Pass 5 ist abgeschlossen; fuenf
der zehn angeforderten Inkremente bleiben. Der lokale CSS-Color-Cache ist laut
Manifest am 2026-11-07 geprueft und frisch. Siehe
[KP-350](./Kontextprotokoll.md#kp-350---css-color-linear-display-p3-function).

**Vorheriger Standards-Pass (2026-11-14; gepusht, CI-gruen):**
Nach `color(srgb-linear ...)` wurde CSS Color weiter geprueft. `color(display-p3
...)` nimmt In-Gamut-Quellkanaele an und konvertiert ueber lineares Display-P3
in den sRGB-Farbraum; nur konvertierte Werte innerhalb des sRGB-Gamuts werden
angenommen. Andere Farbraeume und Gamut-Mapping bleiben unsupported. Die
Ausgabe bleibt gerundetes 8-bit-sRGB. Der lokale CSS-Color-Cache war laut
Manifest am 2026-11-07 geprueft und frisch. 14 fokussierte P3-Faelle, alle
1.435 CSS-Tests, Debug-/Release-Builds ohne Warnungen/Fehler, Debug-/Release-
Volltests (je 16.701), Formatverifikation und `git diff --check` bestanden;
Code-Review war approved. Feature-Commit `6ecc220` wurde mit den
zwischenzeitlich auf `main` veroeffentlichten Test-/CI-Aenderungen im Merge-
Commit `375cfb5` gepusht. Linux development (43617047610; x64, ARM64 und
Trunk), Windows confinement (43617047675; x64/arm64) und CodeQL (43617046891)
sind erfolgreich. Im Linux-Lauf war der upstream Test-Wartepunkt
`893a937` enthalten. Siehe
[KP-349](./Kontextprotokoll.md#kp-349---css-color-display-p3-function).

**Vorheriger Standards-Pass (2026-11-14; gepusht, CI-gruen):**
Nach `color(srgb ...)` wurde CSS Color weiter geprueft. Der noch
unsupported lineare sRGB-Farbraum hat einen bounded Transferfunktionspfad:
`color(srgb-linear ...)`. Der Parser akzeptiert In-Gamut-Zahlen/Prozente,
`none` und optionale Slash-Alpha; lineare RGB-Kanaele werden gemaess CSS-
Transferfunktion vor gerundetem 8-bit-sRGB-Ausgang kodiert. Andere
Farbraeume und Out-of-Gamut-Kanaele bleiben unsupported. Der lokale
CSS-Color-Cache war laut Manifest am 2026-11-07 geprueft und frisch.
18 fokussierte Faelle, der CSS-Gesamttestsatz (1.421), Debug-/Release-
Solution-Builds ohne Warnungen/Fehler, Debug-/Release-Volltests (je 16.687),
Formatverifikation und `git diff --check` bestanden. Code-Review war approved.
Commit `6f2cf3c` ist nach `main` gepusht. Linux development
(43612068563), Windows confinement (43612068566) und CodeQL (43612068784)
sind erfolgreich. Im Linux-ARM64-Job schlug der unveraenderte Browser-Shell-
Pixelfall beim Initialversuch und ersten Job-Rerun fehl; ein weiterer Rerun
bestand. Linux x64 und Trunk-Checks bestanden im ersten Lauf, Windows x64 und
ARM64 sowie CodeQL ebenfalls. Siehe
[KP-348](./Kontextprotokoll.md#kp-348---css-color-srgb-linear-function) und
[Missing](../plans/Missing.md).

**Vorheriger Standards-Pass (2026-11-14; gepusht, CI-gruen):**
Nach dem Unicode-Range-Pass wurde CSS Color (~40%) erneut geprueft. Die zuvor
unsupported `color()`-Syntax hatte eine begrenzte, umsetzbare In-Gamut-Luecke:
`color(srgb ...)`. `CssProperties` nimmt nun drei Zahlen-/Prozentkanaele,
`none` als null und optionale Slash-Alpha an; andere Farbraeume und
Out-of-Gamut-Kanaele bleiben Diagnosen. Die Ausgabe bleibt gerundetes 8-bit
sRGB. Der lokale CSS-Color-Cache war laut Manifest am 2026-11-07 geprueft und
frisch. Die 17 fokussierten Tests, der CSS-Gesamttestsatz (1.404), Debug-/Release-
Solution-Builds ohne Warnungen/Fehler, Debug-/Release-Volltests (je 16.670),
Formatverifikation und `git diff --check` bestanden. Commit `02c13ac` ist nach
`main` gepusht; Linux development (43608274646), Windows confinement auf
x64/arm64 (43608274684) und CodeQL (43608274473) sind erfolgreich.
Weitere Farbraeume, Systemfarben und Farbinterpolation bleiben unsupported.
Siehe [KP-347](./Kontextprotokoll.md#kp-347---css-color-srgb-function).

**Vorheriger Standards-Pass (2026-11-14; Commit gepusht, CI-gruen):**
Die CSS-Syntax-Teilabdeckung (~35%) wurde nach CSS Color (~40%) erneut
ausgewaehlt, weil der Tokenizer eine opt-in Unicode-Range-Erweiterung
explizit ausschloss. `CssOptions.EnableUnicodeRangeTokenExtension` gibt
hexadezimale, Wildcard- und Bindestrich-Bereiche als inklusive typisierte
Grenzen aus; standardmaessig bleibt sie ausgeschaltet. Das fuegt weder
`@font-face` noch Descriptorvalidierung, Fontloading oder -auswahl hinzu.
Der lokale CSS-Syntax-Cache war laut Manifest am 2026-11-07 geprueft und
frisch. CSS-Tests (1.387), Debug-/Release-Builds, Debug-/Release-Volltests
(je 16.653) und Formatverifikation bestanden. Commit `d125818` ist gepusht;
Linux development, Windows confinement und CodeQL sind erfolgreich. Ein
einmaliger Linux-ARM64-Pixelfehler im unveraenderten Browser-Shell-Test
bestand beim Workflow-Rerun; ein vorheriger Linux-Lauf auf dem Basiscommit
`62ed6b7` war ebenfalls erfolgreich. Siehe
[KP-346](./Kontextprotokoll.md#kp-346---css-syntax-unicode-range-token-pass).

**Vorheriger Standards-Pass (2026-11-14; lokal validiert, gepusht und CI-gruen):**
Aufgrund der festgelegten Auswahl nach der hoechsten noch umsetzbaren
Teilabdeckung wurden nach ECMAScript (~90%, verbleibende Punkte ausserhalb
Phase 11w) CSS Syntax und CSS Color (~35%, Gleichstand) betrachtet. CSS Color
wurde wegen der konkreten Luecke `hwb()` gewaehlt. Der CSS-Parser implementiert
nun HWB-Hue, numerische/prozentuale/`none`-Weiss- und Schwarzanteile,
optionale Slash-Alpha, den achromatischen Fall ab Summe 100 und statische
8-bit-sRGB-Ausgabe; Komma-Syntax bleibt ungueltig. Werte ausserhalb des
Nennbereichs bleiben gueltig. Andere Farbraeume, Interpolation,
Missing-Component-Erhaltung und hochpraezises Farbmanagement bleiben
unsupported. Der CSS-Color-Schaetzwert wurde in
[standards.md](../docs/standards.md#support-status) von ~35% auf ~40%
aktualisiert; CSS Syntax (~35%) ist danach die naechste hoechste
Teilabdeckung. Der lokale CSS-Color-Cache war laut Manifest am 2026-11-07
geprueft und somit nicht stale. CSS-Tests (1.377), Solution-Volltest
(16.643), Solution-Build (0 Warnungen/Fehler), Formatverifikation und
`git diff --check` bestanden. Commit `62ed6b7` ist auf `main` gepusht; Linux
development (Run 43597326583), Windows confinement (Run 43597326700) und
CodeQL (Run 43597326615) sind erfolgreich. Siehe
[KP-345](./Kontextprotokoll.md#kp-345---css-color-hwb-pass).

Am 2026-11-12 wurde der standardspriorisierte Pass gewaehlt, als ECMAScript mit
~85% die hoechste aktuelle Teilabdeckung hatte. Pass 1 implementiert opt-in
parser-blockierende Ausfuehrung aller klassischen Inline- und externen Scripts.
Externe Scripts werden am Parser-Haltepunkt ueber begrenztes
Request/Response-IPC im Browser abgerufen; die Single-Process-Variante rendert
auf einem stabilen eigenen Thread. CSP, HTTP(S)-Begrenzung, Cookies-off,
MIME-/Quellenlimits, Deadlines und Transaktionsverhalten bleiben aktiv.
Build (0 Warnungen/Fehler), alle 16.613 Solution-Tests, Format- und
Whitespacepruefung sowie lokale und required-confined V8Smoke-Laeufe bestanden.
Commit `1bcdf59` ist gepusht; Linux development, Windows confinement (x64 und
arm64) und CodeQL waren erfolgreich.
Siehe [KP-333](./Kontextprotokoll.md#kp-333---parser-blockierende-klassiker-implementiert-und-validiert)
und [KP-334](./Kontextprotokoll.md#kp-334---commit-push-und-ci-bestaetigt).

**Abgeschlossener Pass (2026-11-14):** Phase 11w fuegt opt-in importfreie Inline-
Module nach Parsing und `interactive`, vor `DOMContentLoaded` hinzu.
Top-Level-await darf nur innerhalb derselben Deadline/Cancellation und
Readiness-/DOM-/Microtaskbudgets abschliessen; Imports, externe Module,
Loader, Datei-/Netzwerkzugriff und weitere Modulplanung bleiben unsupported.
Classic- und Modulquellen teilen Anzahl- und Zeichenlimits. Fehler,
Abbruch und Timeout invalidieren den Kandidaten, nicht das committed Dokument.
Die Nutzerentscheidung Q-036 hebt nur den vorherigen Top-Level-await-Ausschluss
auf; sie erteilt keine Import- oder Netzwerkberechtigung. Der lokale
ECMAScript-Cache vom 2026-11-07 war juenger als 30 Tage.

Lokale Verifikation: Engine.Scripting 362 Tests, Browser 1.341 Tests,
Solution-Build ohne Warnungen/Fehler, Solution-Volltest 16.626 Tests,
Formatverifikation, `git diff --check` sowie V8Smoke lokal und unter
required-confined Bedingungen bestanden. Commit `a3611e0` ist gepusht. Linux
development ([Run 43553596121](https://github.com/khmseu/VisualWeb/actions/runs/43553596121)),
Windows confinement x64/arm64
([Run 43553596192](https://github.com/khmseu/VisualWeb/actions/runs/43553596192))
und CodeQL ([Run 43553596120](https://github.com/khmseu/VisualWeb/actions/runs/43553596120))
sind erfolgreich.

**Aktueller Pass (2026-11-14; committed, gepusht und CI-verifiziert):**
Die Rangfolge wurde erneut geprueft: ECMAScript bleibt mit ~85% vorne, aber
seine verbliebenen Tracker-Luecken erfordern Modul-Loader/Ressourcen oder
persistentere Event-Loop-Faehigkeiten ausserhalb der gebilligten Phase 11w.
HTML und DOM liegen bei ~20%; als naechster begrenzter HTML-Punkt wurde die
Regel umgesetzt, dass `defer` auf Inline-Modulen keinen Effekt hat und
ignoriert wird. Modul-`async`, Importe und externe Modulquellen bleiben
unsupported. Das ist die Anwendung der bestehenden Nutzer-Rangfolgeregel auf
die genehmigten Phasengrenzen, keine separate Nutzerwahl fuer Modulimporte.
Der lokale HTML-Cache vom 2026-11-07 war juenger als 30 Tage.

Lokale Verifikation: der Regressionstest lief fuer Local- und Process-Renderer
(2 Faelle), Browser-Tests 1.341, Solution-Build ohne Warnungen/Fehler,
Solution-Volltest 16.626, Formatverifikation, `git diff --check` sowie V8Smoke
lokal und unter required-confined Bedingungen bestanden. Featurecommit
`3a341bd` ist gepusht. Linux development, Windows confinement x64/arm64 und
CodeQL sind erfolgreich. Beim ersten Linux-Lauf fiel ein nicht geaenderter
Shell-Pixeltest auf ARM64 aus; der vollstaendige Linux-Rerun bestand. Siehe
[KP-339](./Kontextprotokoll.md#kp-339---html-module-defer-semantics-pass) und
[KP-340](./Kontextprotokoll.md#kp-340---commit-push-und-ci-bestaetigt).

**Nachtraegliche Schaetzungsaktualisierung (2026-11-14):** Auf Nutzerbitte
wurde die ECMAScript-Schaetzung in [standards.md](../docs/standards.md#support-status)
von ~85% auf ~90% neu bewertet. Browser-spezifische Globals, Timer, Worker und
der HTML-Event-Loop werden nicht als ECMAScript-Luecken gezaehlt; deaktivierte
Modulimport-Aufloesung sowie `SharedArrayBuffer`/`Atomics` bleiben wesentliche
Luecken. Die Schaetzung ist weiterhin grob und keine Konformitaetspruefung.
Siehe [KP-341](./Kontextprotokoll.md#kp-341---ecmascript-estimate-reassessment).
Die naechste Auswahl soll die Rangfolge erneut anhand von
[Missing.md](../plans/Missing.md) und den approximativen Prozentwerten
bestimmen, statt hier vorweggenommen zu werden. Siehe
[KP-335](./Kontextprotokoll.md#kp-335---ecmascript-module-umfang-offen),
[KP-336](./Kontextprotokoll.md#kp-336---begrenzte-top-level-await-auswertung-gewaehlt)
und [KP-337](./Kontextprotokoll.md#kp-337---importfreie-inline-module-implementiert),
[KP-338](./Kontextprotokoll.md#kp-338---commit-push-und-ci-bestaetigt).

**Workflowpraezisierung (2026-11-14):** Die Prioritaets-Skill-Anweisung
bestimmt Standardsquellen-Alter jetzt nur aus `specs/manifest.json`
(`checkedAt`, ersatzweise `fetchedAt`); fehlende Zeitstempel gelten als
veraltet und Dateisystem-mtime wird nicht herangezogen. Testauswahl und
Pflichtvalidierung verweisen auf [tests/README.md](../tests/README.md) und den
Validierungsblock in [AGENTS.md](../AGENTS.md#validation-commands-repository-root);
Release-Build/-Solutiontest und plattformspezifische CI-Smokes sind ebenfalls
benannt. Siehe [KP-342](./Kontextprotokoll.md#kp-342---reproducible-standards-freshness-and-validation-guidance).

**Trunk-Plugin-Pin (2026-11-14):** `.trunk/trunk.yaml` pinnt die Plugin-Quelle
auf den Head-Commit `3ce6333bf6eb9cf4faa912e04bb3d9776bd4c5b9` von
[trunk-io/plugins#1180](https://github.com/trunk-io/plugins/pull/1180). Der
Commit enthaelt die Pinact-v5-Long-Flag-Korrektur und das passende bekannte
Pinact-Minimum; die PR war bei Pruefung noch offen. Vorhandene Tool-Version-
Aenderungen in derselben Datei blieben erhalten. Der Trunk-CLI war nicht
installiert; SHA-Verifikation und `git diff --check` bestanden. Siehe
[KP-343](./Kontextprotokoll.md#kp-343---pin-trunk-plugins-to-pinact-v5-fix).

**Trunk-CI (2026-11-14):** Der Linux-Entwicklungsworkflow fuehrt Trunk jetzt
einmal als separaten Job mit `check-mode: all` aus, ueber `trunk-action` mit
fixiertem Action-SHA. Die Action bezieht CLI- und Plugin-Versionen aus
`.trunk/trunk.yaml`; dadurch gibt es keinen doppelten Lauf pro Linux-
Architektur. Lokal waren `actionlint`, `yamllint` und die Trunk-CLI nicht
installiert; der Workflow muss noch in CI verifiziert werden. Siehe
[KP-344](./Kontextprotokoll.md#kp-344---trunk-ci-installation).

Am 2026-11-12 wurde [docs/standards.md](../docs/standards.md) auf Wunsch des
Nutzers in den beiden Standards-Supporttabellen von Statusangaben auf
approximative Prozentwerte umgestellt. Die Werte sind grobe, in 5%-Schritten
gerundete Schaetzungen zur Breite der dokumentierten Feature-Unterstuetzung;
sie zaehlen weder normative Anforderungen noch Testfaelle und sind keine
Konformitaetswerte. Siehe [KP-323](./Kontextprotokoll.md#kp-323---approximative-standards-abdeckung).

Am 2026-11-12 wurde ein neuer Auftrag ueber zehn getrennte
Missing-Prioritaetsinkremente begonnen. Pass 1 implementiert den HTML-
Presentational-Hint `hr[width]`: HTML-Dimensionswerte einschliesslich null und
Prozent werden durch den vorhandenen Parser als CSS-`width` in die
Author-Hint-Kaskade uebernommen; eine eigene aggregierte UTF-16-Grenze schuetzt
den Attributscan. CSS (1.346), Layout (1.001), Solution-Build (0 Warnungen/
Fehler), Solution-Volltest (16.581), Formatverifikation und `git diff --check`
bestehen lokal. Featurecommit `23609d6` ist gepusht; Linux, Windows
confinement und CodeQL bestehen (Runs 43126817154, 43126816817 und
43126816471). Pass 1 ist abgeschlossen. Pass 2 ergaenzt `hr[align]` fuer
ASCII-case-insensitive `left`, `right` und `center` als physische
Horizontalmargen-Hinweise. CSS (1.348), Layout (1.002), Solution-Build
(0 Warnungen/Fehler), Solution-Volltest (16.584), Formatverifikation und
`git diff --check` bestehen. Featurecommit `d203532` ist gepusht; Linux,
Windows confinement und CodeQL bestehen (Runs 43130981709, 43130981534 und
43130981386). Auch der Dokumentationscommit `6b42d5d` besteht in Linux,
Windows confinement und Push on main (Runs 43128251676, 43128251643 und
43128250644). Pass 2 ist abgeschlossen; acht Inkremente bleiben. Der lokale
HTML-Standard wurde am 2026-11-07 geprueft; kein Cache-Refresh war
erforderlich. Siehe
[KP-305](./Kontextprotokoll.md#kp-305---pass-1-hr-width),
[KP-306](./Kontextprotokoll.md#kp-306---pass-1-commit-push-und-ci) und
[KP-307](./Kontextprotokoll.md#kp-307---pass-2-hr-align).

Pass 3 fuegt den HTML-Presentational-Hint fuer `hr[color]` und `hr[noshade]`
als vier CSS-Border-Side-Style-Longhands hinzu und mappt gueltige
`hr[color]`-Werte auf CSS `color`. Eine eigene aggregierte UTF-16-Grenze
begrenzt die Attributwerte; `hr[size]` bleibt unsupported. CSS (1.350),
Solution-Build (0 Warnungen/Fehler), Solution-Volltest (16.586),
Formatverifikation und `git diff --check` bestehen lokal. Commit `e84de9e`
ist gepusht; Linux, Windows confinement und CodeQL bestehen (Runs 43135088646,
43135089070 und 43135088626). Pass 3 ist abgeschlossen; sieben angeforderte
Inkremente bleiben. Siehe
[KP-308](./Kontextprotokoll.md#kp-308---pass-3-hr-color-und-noshade).

Pass 4 implementiert den HTML-Presentational-Hint `hr[size]`: mit
`color`/`noshade` setzt er alle vier Borderbreiten auf die Haelfte des
geparsten Werts; andernfalls setzt size 1 die untere Borderbreite auf 0 und
Werte groesser als 1 die Hoehe auf size-2 px. Ohne `color`/`noshade` erzeugt
size 0 keinen Hint; ungueltige Werte erzeugen immer keinen Size-Hint. Eine
eigene aggregierte UTF-16-Grenze begrenzt den Scan. CSS (1.352), Layout
(1.003), Solution-Build (0 Warnungen/Fehler), Solution-Volltest (16.589),
Formatverifikation und `git diff --check` bestehen lokal. Commit `91f7b7f`
ist gepusht; Linux, Windows confinement und CodeQL bestehen (Runs 43139094812,
43139094632 und 43139094157). Pass 4 ist abgeschlossen; sechs angeforderte
Inkremente bleiben. Siehe
[KP-309](./Kontextprotokoll.md#kp-309---pass-4-waehlt-hr-size) und
[KP-310](./Kontextprotokoll.md#kp-310---pass-4-hr-size-implementiert) sowie
[KP-311](./Kontextprotokoll.md#kp-311---pass-4-ci-gruen).

Pass 5 implementiert begrenztes Rendering von HTML `table[border]`.
Nichtnegative Integerwerte werden als presentational hints fuer alle vier
Tabellenrahmenbreiten verwendet; Parsefehler ergeben 1px. Nur bei nichtnull
oder ungueltigem Wert kommen die getrennten UA-Regeln fuer erhabene
Tabellenraender und 1px eingesenkte Zellraender hinzu. Der Attributscan hat ein
eigenes aggregiertes UTF-16-Limit; Autoren-CSS und UA-Kaskade bleiben getrennt.
CSS (1.360), Layout (1.004), Paint (90) und der Solution-Build (0 Warnungen/
Fehler), Solution-Volltest (16.599), Formatverifikation und `git diff --check`
bestehen. Commit `8ace1a1` ist gepusht; Linux, Windows confinement und CodeQL
sind gruen (Runs 43145540509, 43145540433 und 43145540214). Pass 5 ist
abgeschlossen; fuenf angeforderte Inkremente bleiben. Siehe
[KP-312](./Kontextprotokoll.md#kp-312---pass-5-table-border).

Pass 6 erweitert den HTML-Tree-Builder um drei belegte Select-Recoveryfaelle:
`hr` schliesst implizit offene Optionen/Gruppen und bleibt im Select, `input`
schliesst das Select und wird im Body-Modus erneut verarbeitet, und ein
verschachteltes `select` schliesst das aktuelle Select und wird ignoriert.
Weitere Select-Inhalte bleiben explizit unsupported. Engine.Html (7.274),
Solution-Build (0 Warnungen/Fehler), Solution-Volltest (16.602),
Formatverifikation und `git diff --check` bestehen lokal. Commit `cde2498` ist
gepusht; Linux x64/arm64, Windows x64/arm64 und CodeQL bestehen (Runs
43150699240, 43150699235 und 43150698566). Vier angeforderte Inkremente
verbleiben. Siehe
[KP-314](./Kontextprotokoll.md#kp-314---html-select-recovery-pass-6) und
[KP-315](./Kontextprotokoll.md#kp-315---pass-6-gepusht-und-ci-gruen).

Pass 7 unterstuetzt HTML-`option`-/`optgroup`-Starttags ausserhalb eines
Selects im normalen In-Body-Pfad. Ein neuer `option`-Start und ein
`optgroup`-Start schliessen ein gerade offenes `option` vor dem Einfuegen.
Engine.Html (7.274), Solution-Build (0 Warnungen/Fehler),
Solution-Volltest (16.602), Formatverifikation und `git diff --check`
bestehen lokal. Commit `6fa2b05` ist gepusht; Linux-, Windows- und Push-
Workflows bestehen (43153874263, 43153874333, 43153873946). Zwei
angeforderte Inkremente verbleiben. Siehe
[KP-316](./Kontextprotokoll.md#kp-316---html-option-optgroup-im-body-pass-7)
bis [KP-318](./Kontextprotokoll.md#kp-318---pass-7-ci-gruen).

Pass 8 implementiert die begrenzte RCDATA-Verarbeitung eines `textarea` im
Select-Kontext entsprechend dem aktuellen In-Body-Algorithmus; das Select
bleibt dabei offen. Engine.Html (7.275), Solution-Build (null Warnungen/
Fehler), Solution-Volltest (16.603), Formatverifikation und `git diff --check`
bestehen. Ein vorheriger Volltestlauf hatte einen V8-Deadlinefehler in einem
unveraenderten Scripting-Test; der isolierte Wiederholungslauf und der finale
Volltest bestanden. Commit `9630078` ist gepusht; Linux-, Windows- und Push-
Workflows sind erfolgreich (43157674488, 43157674486, 43157673570). Siehe
KP-319 und
[KP-320](./Kontextprotokoll.md#kp-320---pass-8-commit-und-push).

Pass 9 delegiert sonst nicht speziell behandelte Tokens im Select-Kontext an
den begrenzten In-Body-Algorithmus, statt sie pauschal abzulehnen. Tests
bestaetigen unterstuetzte Blockelemente in Optionen und Optgroups. Engine.Html
(7.274), Solution-Build, Volltest (16.403), Formatverifikation und
`git diff --check` bestehen. Commit `7eb81d6` ist gepusht; Linux, Windows und
Push on main sind erfolgreich (43159752712, 43159752772, 43159752378).
Ein angefordertes Inkrement verbleibt. Siehe
[KP-321](./Kontextprotokoll.md#kp-321---select-inhalte-mit-in-body-regeln-pass-9).

Pass 10 implementiert die begrenzten In-Body-Starttagregeln fuer Ruby-
Annotationen (`rb`, `rtc`, `rp`, `rt`) mit implied-end-tag-Behandlung und
Parsefehlern ausserhalb passender Kontexte. Ruby-spezifisches Styling/Layout
bleibt unsupported. Engine.Html (7.278), Solution-Build (0 Warnungen/Fehler),
Volltest (16.407), Formatverifikation und `git diff --check` bestehen.
Commit `876590e` ist gepusht; Linux, Windows und Push on main sind erfolgreich
(43164284447, 43164284462, 43164284241). Die 10 angeforderten Inkremente sind
abgeschlossen. WHATWG HTML bleibt ein breites, nur teilweise implementiertes
Ziel; die Nutzerprioritaet ist dadurch nicht vollstaendig erledigt. Siehe
[KP-322](./Kontextprotokoll.md#kp-322---ruby-annotationen-pass-10).

**Aktuelle Standardprioritaet (Nutzerangabe, 2026-11-12):** Vor jeder Auswahl
eines standardsgetriebenen Features wird in
[standards.md](../docs/standards.md#support-status) der Support-Eintrag mit dem
hoechsten Wert unter den teilweise unterstuetzten Standards ausgewaehlt;
Gleichstaende sind gleichrangig. Zum Zeitpunkt der Aenderung ist ECMAScript
(`ecmascript`) mit ~85% am hoechsten. Der Wert wird bei jeder Auswahl neu
geprueft und ist nur eine grobe Priorisierungsheuristik, keine
Konformitaetsaussage. Dies ersetzt die zuvor beschlossene feste
HTML-Top-Prioritaet aus
[KP-313](./Kontextprotokoll.md#kp-313---html-als-erste-prioritaet-und-pass-5-ci).
Danach bleibt die bisherige empfohlene Prioritaetsfolge in
[Missing.md](../plans/Missing.md#recommended-priority) bestehen.

**Beschlossene Richtung DEC-034 (Nutzerwahl, 2026-11-12; umgesetzt):** Der
damalige ECMAScript-Pass erweiterte die Roadmap um parser-blockierende
Ausfuehrung klassischer externer Skripte mit CSP-gesteuertem HTTP(S)-Abruf
einschliesslich Cross-Origin-Faellen. Die Umsetzung blieb browservermittelt und
bewahrte Request-, Budget-, Abbruch- und Transaktionsregeln; Commit und CI sind
in [KP-333](./Kontextprotokoll.md#kp-333---parser-blockierende-klassiker-implementiert-und-validiert)
und [KP-334](./Kontextprotokoll.md#kp-334---commit-push-und-ci-bestaetigt)
belegt. Diese Wahl entscheidet nicht ueber ECMAScript-Module; siehe Q-034.

**Beschlossene Umsetzung Q-031 (Nutzerwahl, 2026-11-12):** Parser-blockierende
Skriptquellen werden am Parser-Haltepunkt ueber Request/Response-IPC vom Browser
vermittelt. Renderer duerfen weiter keine Ressourcen abrufen. Die Erweiterung
muss Protokoll-Framing, Identitaet, Deadlines, Abbruch und Fehlerwiederherstellung
erhalten; siehe [KP-328](./Kontextprotokoll.md#kp-328---parser-pause-ipc-fuer-externe-skripte-gewaehlt).

**Beschlossene Umsetzung Q-033 (Nutzerwahl, 2026-11-12):** Parser-blockierende
Ausfuehrung gilt fuer alle klassischen Inline- und externen Skripte. Das folgt
der HTML-Parserreihenfolge; Inline-only-Seiten behalten nicht die bisherige
Post-Parse-Ausnahme. Bestehende Tests und direkt betroffene Erwartungen sind an
dieses Verhalten anzupassen; siehe
[KP-332](./Kontextprotokoll.md#kp-332---parserreihenfolge-fuer-alle-klassiker-beschlossen).

Am 2026-11-10 wurde ein neuer Auftrag ueber zehn getrennte
Missing-Prioritaetsinkremente erteilt. Pass 1 implementiert geerbtes CSS
`text-indent` fuer begrenzte First-Line-Einrueckung im normalen Blockfluss.
CSS (1.302) und Layout (924) fokussierte Tests, Solution-Build (0 Warnungen/
Fehler), Solution-Volltest (16.458), Formatverifikation und `git diff --check`
bestehen. Featurecommit `0dd3c9e` und Integrationscommit `a072c01` sind
gepusht; Linux, Windows confinement und CodeQL bestehen auf `a072c01`.
Pass 2 ergaenzt geerbtes CSS `text-align-last` fuer Schlusszeilen und Zeilen
vor erzwungenen Umbruechen. CSS (1.317), Layout (927), Solution-Build
(0 Warnungen/Fehler), Solution-Volltest (16.476), Formatverifikation und
`git diff --check` bestehen lokal. Commit `3887986` ist gepusht; Linux,
Windows confinement x64/arm64 und CodeQL bestehen.
Pass 3 implementiert geerbtes CSS Text `text-transform` mit `none`,
ASCII-`uppercase` und ASCII-`lowercase` vor dem Shaping, ohne DOM-Mutation oder
UTF-16-Laengenverschiebung. Nicht-ASCII-Buchstaben scheitern explizit;
`capitalize`, `full-width` und `full-size-kana` bleiben unsupported. CSS
(1.329), Layout (929), Solution-Build (0 Warnungen/Fehler), Solution-Volltest
(16.490), Formatverifikation und `git diff --check` bestehen lokal. Feature-
Commit `0200283` ist gepusht. Linux x64/ARM64 (Run 42925954320), Windows
confinement x64/ARM64 (Run 42925954248) und Push-on-main/CodeQL (Run 42925954109) sind erfolgreich. Sieben Inkremente bleiben.
Siehe [KP-297](./Kontextprotokoll.md#kp-297---pass-3-ascii-text-transform).
Pass 4 ergaenzt geerbtes CSS-Text-`word-spacing` mit Initialwert `normal` und
nichtnegativen unterstuetzten Laengen, die vor der Vererbung in absolute
Laengen aufgeloest werden. Layout addiert den Abstand auf kollabierte und
erhaltene ASCII-Leerzeichen vor dem Line-Fitting. Negative Laengen, Prozentwerte
und nicht-ASCII-Worttrenner bleiben unsupported. CSS (1.344), Layout (931), der
Solution-Build (0 Warnungen/Fehler), alle 16.507 Solution-Tests,
Formatverifikation und `git diff --check` bestehen. Featurecommit `2dd5026` ist
gepusht. Linux-Development (Run 42931828540), Windows confinement x64/arm64
(Run 42931828466) und CodeQL (Run 42931828462) sind erfolgreich. Der separate
Pass-4-Trackercommit `e2f3183` besteht ebenfalls den Windows-Workflow
(Run 42933239637). Pass 5 implementiert bounded CSS `white-space:pre-wrap`:
ASCII-Leerzeichen und Segment-Newlines bleiben erhalten, Umbrueche erfolgen
nach einer Leerzeichensequenz und Zeilenend-Leerzeichen haengen ausserhalb von
Ausrichtung und intrinsischer Breite. Gemischte Whitespace-Modi, Tabs/Form
Feeds, `break-spaces` und Glue-Nachbarschaft bleiben unsupported. CSS (1.344),
Layout (940), Solution-Build (0 Warnungen/Fehler), alle 16.516 Solution-Tests,
Formatverifikation und `git diff --check` bestehen lokal. Featurecommit
`23d1d91` ist nach `main` gepusht; Linux-Development (Run 42938502799),
Windows confinement x64/arm64 (Run 42938502824) und Push-on-main/CodeQL
(Run 42938502178) sind erfolgreich. Pass 5 ist abgeschlossen; fuenf
Inkremente bleiben. Siehe
[KP-299](./Kontextprotokoll.md#kp-299---pass-5-bounded-pre-wrap).
Pass 6 implementiert bounded CSS `white-space:break-spaces`: ASCII-Leerzeichen
und Segment-Newlines bleiben erhalten, mit Umbruchmoeglichkeiten nach jedem
Leerzeichen einschliesslich benachbarter Leerzeichen. Zeilenend-Leerzeichen
zaehlen zur Ausrichtung und automatischen Tabellen-Intrinsic-Breite. Gemischte
Whitespace-Modi, Tabs/Form-Feeds, Nicht-ASCII-Leerzeichen, Widget-Grenzen und
Glue-Nachbarschaft bleiben unsupported. Layout (946) und Solution-Build
(0 Warnungen/Fehler), Solution-Volltest (16.522), Formatverifikation und
`git diff --check` bestehen. Featurecommit `5b498da` ist gepusht; Linux
(Run 42943325472), Windows x64/arm64 (Run 42943325301) und CodeQL
(Run 42943325315) bestehen. Pass 6 ist abgeschlossen; vier Inkremente
verbleiben.
Siehe [KP-300](./Kontextprotokoll.md#kp-300---pass-6-bounded-break-spaces).
Siehe [KP-298](./Kontextprotokoll.md#kp-298---pass-4-bounded-word-spacing).
Siehe
[KP-296](./Kontextprotokoll.md#kp-296---pass-2-text-align-last).
Chronologischer Beleg und Auswahlgrund stehen in
[KP-295](./Kontextprotokoll.md#kp-295---neuer-zehnerauftrag-und-pass-1-text-indent).
Pass 7 erweitert `white-space:break-spaces` um Umbrueche an unterstuetzten
atomaren Inline-Formular-Steuerelementen; angrenzende erhaltene Leerzeichen
bleiben einzeln umbrechbar, exakte Fits bleiben ungebrochen. Layout (951),
Solution-Build (0 Warnungen/Fehler), Solution-Volltest (16.527),
Formatverifikation und `git diff --check` bestehen lokal. Featurecommit
`c69f482` ist gepusht; Linux (Run 42949667326), Windows x64/arm64 (Run 42949667251) und CodeQL (Run 42949666801) sind gruen. Pass 7 ist
abgeschlossen; drei angeforderte Inkremente verbleiben.
Siehe [KP-301](./Kontextprotokoll.md#kp-301---pass-7-break-spaces-an-atomic-widgets).
Im aktuellen Zehnerauftrag implementiert Pass 8 begrenzte HTML-`<wbr>`-
Umbruchmoeglichkeiten in Wrapping-Whitespace-Modi und bei der automatischen
Tabellen-Minimumsbreite.
Benachbarte gleich formatierte Texte bleiben beim Formen ungebrochen ein Lauf;
Glyphcluster werden an Quelltextknoten-Grenzen zugeordnet. `pre`/`nowrap`
unterdruecken die Moeglichkeit. Layout (967), fokussierte Tests (16),
Solution-Build (0 Warnungen/Fehler), Solution-Volltest (16.543),
Formatverifikation und `git diff --check` bestehen. Ein erster Volltestlauf
hatte einen Fehler im unabhaengigen CPU-throttled Browser-Prozesstest; dessen
beide Faelle und der wiederholte Volltest bestehen. Featurecommit `c4e01e1` ist
gepusht; Linux (Run 42957196183), Windows x64/arm64 (Run 42957196261) und
CodeQL (Run 42957195483) sind gruen. Pass 8 ist abgeschlossen; zwei
angeforderte Inkremente verbleiben.
Siehe [KP-302](./Kontextprotokoll.md#kp-302---pass-8-wbr-zeilenumbrueche).
Pass 9 ergaenzt begrenzte manuelle U+00AD-Umbruchmoeglichkeiten zwischen
ASCII-Buchstaben oder Ziffern in einem Textknoten. Das Zeichen wird ohne
Umbruch nicht geformt; beim gewaehlt gebrochenen Lauf wird U+00AD selbst als
bedingter sichtbarer Glyph geformt. `pre`/`nowrap` blenden es aus. Fuehrende,
abschliessende, wiederholte und nicht-ASCII-benachbarte weiche Trennzeichen
bleiben unsupported; allgemeine Unicode-Zeilenumbrueche und automatische
Silbentrennung ebenfalls. Layout (981), Text (18), fokussierte Layouttests
(24), natives Shaping (1), Solution-Build (0 Warnungen/Fehler),
Solution-Volltest (16.558), Formatverifikation und `git diff --check`
bestehen lokal. Featurecommit `6ae83a3` ist gepusht; Linux
(Run 42963750524), Windows confinement (Run 42963748885) und CodeQL
(Run 42963748532) bestehen. Pass 9 ist abgeschlossen; ein angefordertes
Inkrement verbleibt. Siehe
[KP-303](./Kontextprotokoll.md#kp-303---pass-9-weiche-trennzeichen).
Pass 10 ergaenzt begrenzte U+2010-HYPHEN-Umbruchmoeglichkeiten: innerhalb
eines Textknotens zwischen ASCII-Buchstaben oder Ziffern, mit Umbruch nach dem
sichtbaren Bindestrich nur vor einem ASCII-Buchstaben. `pre`/`nowrap` umbrechen
dort nicht. Fuehrende, abschliessende, wiederholte oder nicht-ASCII-benachbarte
U+2010 sowie andere Gedankenstriche, allgemeine Unicode-Zeilenumbrueche und
automatische Silbentrennung bleiben unsupported. Greedy-Fitting,
`<wbr>`-Interaktion, Tabellen-Minimumsbreite, Abbruch und gemeinsames
Arbeitsbudget bleiben erhalten. Layout (1.000), Text (19), fokussierte Layout-
tests (128), natives Shaping (1), Solution-Build (0 Warnungen/Fehler),
Solution-Volltest (16.578), Formatverifikation und `git diff --check` bestehen
lokal; die exakte Umbruchgrenze ist 17 erfolgreich gegen 16 mit Limitfehler.
Featurecommit `f0570c5` ist gepusht; Linux (Run 42967745855), Windows
confinement (Run 42967745894) und CodeQL (Run 42967745101) bestehen.
Pass 10 schliesst den angeforderten Zehnerauftrag ab. Siehe
[KP-304](./Kontextprotokoll.md#kp-304---pass-10-u2010-umbrueche).

Der zuvor dokumentierte Auftrag ueber zehn Missing-Prioritaetsinkremente ist
abgeschlossen. Nach der anschliessenden Bitte um Fortsetzung wurde ein weiteres
einzelnes Inkrement bearbeitet:
Pass 8 bildet `td`/`th`-`width` auf CSS `width` ab. Featurecommit
`5da1a7f` und Trackercommit `08bf613` sind gepusht; Linux, Windows x64/arm64
und CodeQL fuer beide Commits sind gruen (KP-273 und anschliessende
Workflow-Pruefung). Pass 9 ergaenzt `tr[height]` als begrenzte CSS-
Presentational-Hint mit Pixel-Minimumhoehen, Kaskadenueberschreibung,
UTF-16-Budget und expliziter Ablehnung prozentualer Zeilenhoehen. CSS (1.241),
Layout (915), alle 16.369 Solution-Tests, Solution-Build (0 Warnungen/Fehler)
und Formatverifikation bestehen. Commit `d27699f` ist gepusht; Linux, Windows
und CodeQL sind gruen (KP-277). Pass 9 ist abgeschlossen. Pass 10 ergaenzt
HTML `col[width]` als begrenzten CSS-Hinweis mit Pixel-/Prozent-Spurbreiten,
Autor-Kaskade und eigenem UTF-16-Limit. CSS (1.245), Layout (919) und
alle 16.377 Solution-Tests, Solution-Build (0 Warnungen/Fehler),
Formatverifikation und `git diff --check` bestehen. Commit `73c61c7` ist
gepusht; Linux, Windows und CodeQL sind gruen. Pass 10 und der Auftrag mit
zehn Inkrementen sind abgeschlossen (KP-279). Chronologische Evidenz steht in
[KP-271 bis KP-279](./Kontextprotokoll.md#kp-271---pass-8-waehlt-tdth-width).

Das neue Inkrement ergaenzt die Standard-UA-Darstellung von HTML `<hr>` und
deterministisches Malen des `inset`-Rahmens. Lokale CSS-/Paint-/Content-Tests,
Solution-Build, Formatverifikation und serieller Solution-Volltest (16.381)
bestehen. Ein erster paralleler Volltest hatte einen Timeout in einem
unveraenderten Browser-Navigationstest; der gezielte Test bestand isoliert auf
Basiscommit und Arbeitsstand, und der serielle Volltest bestand. Featurecommit
`3304add` ist gepusht; Linux, Windows x64/arm64 und CodeQL sind gruen (Runs
42714571836, 42714571820 und 42714571721). Siehe
[KP-280](./Kontextprotokoll.md#kp-280---fortsetzung-waehlt-sichtbare-html-horizontalregel).

Am 2026-11-10 begann ein neuer Auftrag ueber zehn separate Missing-
Prioritaetsinkremente. Pass 1 erweitert das deterministische Rahmenmalen um
`outset` mit umgekehrten Hell-/Dunkelseiten. Paint (73), Solution-Build,
Formatverifikation und alle 16.381 Solution-Tests bestehen; Commit `086e064`
ist gepusht und Linux, Windows x64/arm64 und CodeQL sind gruen (KP-281).
Pass 2 ergaenzt begrenzte quadratische CSS-`dashed`-Rahmensegmente. Paint (73)
und Content (106), Solution-Build, Formatpruefung und alle 16.381
serialen Solution-Tests bestehen. Ein veralteter Content-Test erwartete
zunaechst, dass `dashed` fehlschlaegt; seine Negativabdeckung wurde auf
weiterhin unsupported `dotted` umgestellt (KP-282). Commit `90b9474` ist
gepusht; Linux, Windows confinement einschliesslich x64/arm64 und CodeQL sind
gruen (Runs [42732340775](https://github.com/khmseu/VisualWeb/actions/runs/42732340775),
[42732340725](https://github.com/khmseu/VisualWeb/actions/runs/42732340725)
und [42732340478](https://github.com/khmseu/VisualWeb/actions/runs/42732340478)).
Pass 2 ist abgeschlossen; acht Inkremente verbleiben.
Pass 3 erweitert die physische Border-Painting-Untermenge um CSS `groove` und
`ridge`: zwei angrenzende Schattenbaender pro Seite, jeweils mit invertierter
Inset-/Outset-Reihenfolge. Paint (74), Solution-Build (0 Warnungen/Fehler),
Formatverifikation und 16.382 serielle Solution-Tests bestehen. Commit
`d835936` ist gepusht; Linux, Windows confinement x64/arm64 und CodeQL sind
gruen (Runs
[42737897730](https://github.com/khmseu/VisualWeb/actions/runs/42737897730),
[42737897888](https://github.com/khmseu/VisualWeb/actions/runs/42737897888)
und [42737896350](https://github.com/khmseu/VisualWeb/actions/runs/42737896350)).
`dotted` und `double` bleiben unsupported; Pass 3 ist abgeschlossen, sieben
Inkremente verbleiben
([KP-283](./Kontextprotokoll.md#kp-283---pass-3-css-groove--und-ridge-rahmen)).
Pass 4 ergaenzt CSS-`double`-Rahmen als zwei Linien von je einem Drittel der
Rahmenbreite, getrennt durch eine transparente Luecke von einem Drittel.
Paint (75), Solution-Build (0 Warnungen/Fehler), Formatverifikation und
16.383 serielle Solution-Tests bestehen lokal; `dotted` bleibt unsupported.
Commit `f85b76f` ist gepusht; Linux, Windows confinement x64/arm64 und CodeQL
CI bestehen. Pass 4 ist abgeschlossen; sechs Inkremente verbleiben
([KP-284](./Kontextprotokoll.md#kp-284---pass-4-css-double-rahmen)).

Pass 5 ergaenzt zentrierte, runde CSS-`dotted`-Rahmenpunkte mit einem
Durchmesser und Abstand von je einer Rahmenbreite. Jeder Punkt verbraucht das
Paint-Budget; eine zu kurze Seite fuer einen vollstaendigen Punkt wird explizit
abgewiesen. Paint (77), Content (106), Solution-Build, Formatverifikation und
16.385 serielle Solution-Tests bestehen lokal. Commit `b06b2fc` ist gepusht;
Linux, Windows confinement x64/arm64 und CodeQL CI bestehen. Pass 5 ist
abgeschlossen; fuenf Inkremente verbleiben
([KP-285](./Kontextprotokoll.md#kp-285---pass-5-css-dotted-rahmen)).

Pass 6 ergaenzt die nicht-vererbte CSS-Eigenschaft
`text-decoration-color` fuer Underline und Line-through. Sie folgt der
Farbsyntax/Kaskade, startet mit `currentColor` und bleibt am Element verankert,
das die Dekoration setzt; Nachfahrenfarben aendern sie nicht. Zu diesem
Zeitpunkt war der Shorthand noch line-only; Pass 7 erweitert Stil und Farbe.
CSS (1.247), Paint (79),
Solution-Build (0 Warnungen/Fehler), Formatverifikation und 16.388 serielle
Solution-Tests bestehen lokal. Commit `b85319f` ist gepusht; Linux, Windows
confinement x64/arm64 und CodeQL bestehen (Runs
[42763658777](https://github.com/khmseu/VisualWeb/actions/runs/42763658777),
[42763659027](https://github.com/khmseu/VisualWeb/actions/runs/42763659027),
[42763658072](https://github.com/khmseu/VisualWeb/actions/runs/42763658072)).
Pass 6 ist abgeschlossen; vier Inkremente verbleiben
([KP-286](./Kontextprotokoll.md#kp-286---pass-6-css-text-decoration-color)).
Pass 7 ergaenzt `text-decoration-style` mit `solid` und `double` sowie die
begrenzte Zeilen-/Stil-/Farbkomposition des `text-decoration`-Shorthands.
CSS (1.269), Paint (82), Solution-Build, Formatverifikation und 16.413
serielle Solution-Tests bestehen lokal. Commit `384e99b` ist gepusht; Linux
(Run 42769753973), Windows confinement (Run 42769753360) und CodeQL
(Run 42769753102) sind erfolgreich. Pass 7 ist abgeschlossen; drei
Inkremente verbleiben ([KP-287](./Kontextprotokoll.md#kp-287---pass-7-css-text-decoration-style),
[KP-288](./Kontextprotokoll.md#kp-288---pass-7-committed-and-ci-green)).
Pass 8 nimmt `overline` in die endliche CSS-Textdekoration auf. Paint platziert
die Linie anhand der registrierten OpenType-Cap-Height und verwendet die
Underline-Thickness als begrenzte UA-Metrik. CSS (1.272), Paint (84),
Solution-Build, Formatverifikation und der serielle Solution-Volltest
(16.418) bestehen. Der erste Volltestlauf scheiterte in einem unabhängigen
Browser-Cgroup-Cleanup-Test beim Lesen von `cpu.stat`; der vollstaendige
Wiederholungslauf bestand. Commit `266d245` ist gepusht; Linux, Windows
x64/arm64 und CodeQL bestehen (Runs
[42874627946](https://github.com/khmseu/VisualWeb/actions/runs/42874627946),
[42874627593](https://github.com/khmseu/VisualWeb/actions/runs/42874627593),
[42874629254](https://github.com/khmseu/VisualWeb/actions/runs/42874629254)).
Pass 8 ist abgeschlossen; zwei Inkremente verbleiben
([KP-289](./Kontextprotokoll.md#kp-289---pass-8-css-text-decoration-overline),
[KP-290](./Kontextprotokoll.md#kp-290---pass-8-committed-and-ci-green)).
Pass 9 ergaenzt die begrenzte CSS-Text-Decoration-Level-4-Eigenschaft
`text-decoration-thickness`: nicht geerbt, `auto` als Initialwert,
`from-font`, die benannten UA-Breiten und nichtnegative unterstuetzte
Laengen/Prozente. Der Shorthand setzt Thickness bei Auslassung auf `auto`
zurueck. Underline, Overline und Line-through tragen die ausgewaehlte Breite bis
zur vorab validierten Rasterung; explizite Null unterdrueckt die Ausgabe.
CSS (1.287), Paint (88), Solution-Build (0 Warnungen/Fehler), alle 16.437
Solution-Tests, Formatverifikation und `git diff --check` bestehen lokal.
Commit `98c6fa8` ist gepusht; Linux development, Windows confinement x64/arm64
und CodeQL bestehen. Pass 9 ist abgeschlossen; ein angefordertes Inkrement
bleibt. Siehe [KP-291](./Kontextprotokoll.md#kp-291---pass-9-css-text-decoration-thickness)
und [KP-292](./Kontextprotokoll.md#kp-292---pass-9-ci-gruen).
Pass 10 erweitert `text-decoration-line` und den Shorthand um Kombinationen
verschiedener unterstuetzter Linien. Duplikate und `none` mit sichtbaren Linien
bleiben ungueltig; `blink` ist weiterhin unsupported. CSS (1.291), Paint (89),
Solution-Build (0 Warnungen/Fehler), alle 16.442 Solution-Tests,
Formatverifikation und `git diff --check` bestehen lokal. Commit `002d38e` ist
gepusht; Linux development (42893005649), Windows confinement x64/arm64
(42893005685) und CodeQL (42893004754) sind erfolgreich. Der erste Windows-
x64-Lauf traf einen unabhaengigen Browser-Test-Timeout; der vollstaendige
Windows-Rerun bestand. Der Zehnerauftrag ist abgeschlossen. Siehe
[KP-293](./Kontextprotokoll.md#kp-293---pass-10-kombinierte-textdekorationen)
und [KP-294](./Kontextprotokoll.md#kp-294---pass-10-commit-push-und-ci-gruen).

- Abgeschlossener Auftrag: am 2026-11-01 wurden zehn getrennte
  Missing-Prioritaetsinkremente angefordert. Damit ist der fruehere Stopp aus
  KP-062 fuer neue, prioritaetsgerechte Arbeit aufgehoben; die zuvor gewaehlten
  PointerEvents bleiben wegen des dokumentierten Event-Grenzenkonflikts
  unveraendert. Pass 1 waehlt als kleinen Prioritaet-1-Schritt die vorhandene
  HTML-`div[align]`-Luecke ueber die CSS-Autoren-Presentational-Hint-Kaskade.
  Nach dem CI-gruenen Commit `87bdcdd` zeigte ein erneuter Abgleich mit dem
  lokalen HTML-Standard, dass `center`/`middle` ebenfalls fuer `div` gelten;
  die erste Aenderung hatte sie faelschlich ausgeschlossen. Die gekoppelte
  Korrektur besteht gezielte CSS- (1.093) und Content- (84) Tests,
  Solution-Build, Volltest (16.058) und Formatpruefung. Der Korrekturcommit
  `0cd05e6` ist gepusht; Linux, Windows und CodeQL bestehen. Damit ist Pass 1
  abgeschlossen. Pass 2 ist als begrenzte CSS-Unterstuetzung fuer das
  HTML-`center`-Element ausgewaehlt; es soll das vorhandene Block-Parsing mit
  Center-Hint und passenden Regressionen in CSS/Content ergaenzen. CSS
  (1.093), Content (85), Solution-Build, Volltest (16.059) und Formatpruefung
  bestehen. `f984eaf` ist gepusht; Linux, Windows x64/arm64 und CodeQL
  bestehen. Der erste Windows-x64-Lauf traf eine bekannte Duplicate-Request-
  Timing-Flake im unveraenderten Browser-Test; nur der fehlgeschlagene Job
  wurde erneut ausgefuehrt und bestand (KP-173/KP-175). Pass 2 ist
  abgeschlossen; acht Inkremente verbleiben. Pass 3 waehlt `table align=center`
  als begrenzten Praesentationshinweis auf vorhandenen Auto-Margen; Layout-
  Freigabe und Tabellengeometrie werden getestet. `left`/`right` Floats und
  sonstige Legacy-Tabellenattribute bleiben unsupported (KP-176). Die
  Implementierung ist als Commit `eac7974` gepusht. CSS-Cascade- (1.098),
  Layout- (784) und Content-Pixeltests (86) bestehen; Solution-Build
  (0 Warnungen/Fehler), Volltest (16.068) und Formatpruefung bestehen.
  Linux x64/arm64, Windows x64/arm64 und CodeQL bestehen; sieben Inkremente
  verbleiben (KP-177). Pass 4 ergaenzt die fehlenden 40px Inline-Margen von
  HTML-`figure` im horizontalen LTR-Subset. CSS-Cascade (1.099), Layout-
  Geometrie (785), Solution-Build, Volltest (16.070) und Formatpruefung
  bestehen. `c34ed4c` ist gepusht; Linux x64/arm64, Windows x64/arm64 und
  CodeQL bestehen (KP-178). Pass 5 ergaenzt den HTML-`address`-UA-Default
  `font-style:italic` mit normaler Kaskade und ohne Font-Synthese. CSS (1.100),
  Content (87), Solution-Build, Volltest (16.072) und Formatpruefung bestehen.
  `3d00c0f` ist gepusht; Linux x64/arm64, Windows x64/arm64 und CodeQL bestehen.
  Fuenf Inkremente verbleiben (KP-179). Pass 6 implementiert die begrenzte
  sichtbare Unterstreichung fuer CSS-`underline` und den HTML-`:link`-Default.
  `none` hebt Vorfahren-Dekoration nicht auf; weitere Linienwerte und
  Dekorationsstile bleiben unsupported. CSS (1.102), Paint (65), Content (88),
  Solution-Build und Formatpruefung bestehen; der geaenderte Volltest besteht
  auf Retry mit 16.077 Faellen, der Basis-Volltest `3d00c0f` mit 16.072. Der
  erste lokale Volltest und der erste Windows-x64-CI-Lauf trafen jeweils
  denselben Timeout im unveraenderten Browser-Navigationstest. Beide
  Volltest-Retries bestanden; Windows x64/arm64, Linux und CodeQL sind gruen.
  Keine Ursache ist nachgewiesen. `c6f13b1` ist gepusht; vier Inkremente
  verbleiben ([KP-180](./Kontextprotokoll.md#kp-180---pass-6-waehlt-begrenzte-textunterstreichung),
  [KP-181](./Kontextprotokoll.md#kp-181---pass-6-lokal-implementiert-und-validiert),
  [KP-182](./Kontextprotokoll.md#kp-182---pass-6-gepusht-und-ci-gruen)). Der
  Tracker-Abschluss `7fdf2bb` ist ebenfalls gepusht und CI-gruen. Pass 7 waehlt
  als naechsten kleinen Tabellen-Rendering-Schritt die HTML-`align`-Hints
  `left`/`right`/`center`/`middle`/`justify` auf `td` und `th`; Row-/Group-
  Alignment und vertikale Zellpositionierung bleiben ausserhalb. Der lokale
  HTML-Cache (ID `html`, `checkedAt` 2026-10-04) ist innerhalb der 30-Tage-
  Frist. `c3f64a5` ist lokal validiert und gepusht: CSS (1.116), Layout (798),
  Content (89), Solution-Build (0 Warnungen/Fehler), Volltest (16.105) und
  Formatpruefung bestehen. Linux x64/arm64, Windows x64/arm64 und CodeQL-CI
  bestehen (KP-184). Pass 8 implementiert das begrenzte `<font face>`-Hint:
  CSS-Familienlisten, Author-Presentational-Hint-Kaskade, Vererbung,
  Rekalkulation und aggregiertes rohes UTF-16-Budget. Die registrierte
  Font-Auswahl prueft explizit gelistete Familien in Reihenfolge; kein
  impliziter Fallback, Download oder Synthese. CSS (1.129), Content (91),
  Solution-Build (0 Warnungen/Fehler), Volltest (16.120) und Formatpruefung
  bestehen. `5b5379e` ist gepusht; Linux, Windows x64/arm64 und CodeQL-CI
  sind gruen. Danach verbleiben zwei Inkremente
  ([KP-185](./Kontextprotokoll.md#kp-185---pass-8-waehlt-begrenzten-html-font-face-hint),
  [KP-186](./Kontextprotokoll.md#kp-186---pass-8-implementiert-begrenzter-html-font-face-hint)).
  Nur explizit aufgefuehrte und registrierte Familien kommen in Betracht
  ([KP-187](./Kontextprotokoll.md#kp-187---pass-9-waehlt-begrenzte-html-colspan-tabellen)).
  Pass 9 implementiert nun horizontal begrenztes `colspan` mit gleichmaessig
  verteilten Intrinsic-Constraints, HTML-Fallback-/Obergrenzen, explizitem
  `rowspan`-Fehler, `MaxTableColumns` (100.000) und gemeinsamem
  `MaxTableColumnOperations` (10.000.000). Die getrennten Limits verfeinern den
  frueheren Vorschlag, das Spaltenlimit an `MaxBoxes` zu binden, weil Grid-
  Dimension und Track-Arbeit nicht Layoutboxen zaehlen
  ([KP-188](./Kontextprotokoll.md#kp-188---pass-9-implementiert-begrenzte-horizontale-colspan-geometrie)).
  Layout (807),
  Content (92), Solution-Build (0 Warnungen/Fehler), Formatpruefung und finaler
  Volltest (16.130) bestehen. Die Span-Gruppierung ist bedarfsgesteuert, sodass
  Tabellen ohne Spans keinen festen Bucket-Array-Overhead erhalten. `3e568b1`
  ist gepusht; Linux, Windows confinement und CodeQL-CI bestehen
  ([KP-189](./Kontextprotokoll.md#kp-189---pass-9-committet-gepusht-und-ci-gruen)).
  Pass 10 ist als eng begrenzte Ableitung aus Prioritaet 1 gewaehlt: Zeilen-
  und Zeilengruppen-`background-color` auf kanonischen Tabellen. Die lokale
  CSS-2.2-Quelle beschreibt deren Hintergrundebenen; Layout (807), Content
  (93), Solution-Build, Formatpruefung und Volltest (16.131) bestehen.
  `69c902a` ist gepusht; Linux, Windows confinement und CodeQL-CI sind gruen.
  Damit sind alle zehn getrennten Inkremente abgeschlossen; `rowspan`,
  Captions/Spalten, arbitrary tables und weitere Tabellenfeatures bleiben
  ausdruecklich offen
  ([KP-190](./Kontextprotokoll.md#kp-190---pass-10-waehlt-und-validiert-tabellenhintergrundebenen),
  [KP-191](./Kontextprotokoll.md#kp-191---pass-10-committet-gepusht-und-ci-gruen)).
  ([KP-183](./Kontextprotokoll.md#kp-183---pass-7-waehlt-tabellenzell-align-hints),
  [KP-184](./Kontextprotokoll.md#kp-184---pass-7-lokal-validiert-und-gepusht)).
  ([KP-169](./Kontextprotokoll.md#kp-169---neuer-auftrag-und-div-ausrichtung),
  [KP-173](./Kontextprotokoll.md#kp-173---div-zentrierungsregel-korrigieren),
  [KP-174](./Kontextprotokoll.md#kp-174---div-zentrierung-korrigiert-und-validiert),
  [KP-175](./Kontextprotokoll.md#kp-175---pass-2-waehlt-begrenzte-center-element-ausrichtung),
  [KP-176](./Kontextprotokoll.md#kp-176---pass-3-waehlt-table-aligncenter),
  [KP-177](./Kontextprotokoll.md#kp-177---pass-3-implementiert-und-gepusht),
  [KP-178](./Kontextprotokoll.md#kp-178---pass-4-waehlt-html-figure-raender),
  [KP-179](./Kontextprotokoll.md#kp-179---pass-5-waehlt-und-implementiert-address-schriftstil),
  [KP-180](./Kontextprotokoll.md#kp-180---pass-6-waehlt-begrenzte-textunterstreichung)).
- Aktueller Auftrag: Der Nutzer hat `/missing-priority-implementation` mit
  Argument `10` erneut aufgerufen; dieser Lauf umfasst zehn neue, jeweils
  getrennt zu validierende, zu committende und zu pushende Inkremente. Die
  vorherigen zehn Inkremente sind abgeschlossen und kein Ersatz fuer diesen
  neuen Lauf. Pass 1 waehlt begrenzte CSS-`line-through`-Dekoration plus die
  HTML-Defaults fuer `del`, `s` und `strike`, aufbauend auf dem vorhandenen
  Underline-Pfad. Pass 1 ist als `f7a83d5` committet und gepusht; Solution-
  Build (0 Warnungen), alle 16.141 Solution-Tests und Formatpruefung bestehen.
  Linux/Windows x64+arm64 und CodeQL-CI bestehen ebenfalls
  ([KP-194](./Kontextprotokoll.md#kp-194---pass-1-ci-gruen)). Pass 2 waehlt
  als naechsten kleinen Priority-1-Schritt den HTML-UA-Unterstrich fuer
  `ins` und `u`; Commit `de76758` ist gepusht. CSS (1.136), Content (98),
  Build (0 Warnungen), Volltest (16.145) und Formatpruefung bestehen lokal.
  Linux/Windows x64+arm64 und CodeQL-CI bestehen ebenfalls
  ([KP-197](./Kontextprotokoll.md#kp-197---pass-2-ci-gruen)).
  Pass 3 pruefte die fehlende HTML-UA-Hervorhebung fuer `mark`; die
  Content-Regression scheitert an der expliziten Layout-Grenze fuer Inline-
  Hintergruende. Eine CSS-only-Regel wuerde markierte Seiten fehlschlagen
  lassen; Implementierung und Tests wurden entfernt. Der Nutzer hat entschieden,
  zuerst begrenzte Inline-Hintergrundfragmentgeometrie umzusetzen; der
  `mark`-Default bleibt Integrationsregression vorgesehen
  ([KP-200](./Kontextprotokoll.md#kp-200---inline-hintergrundgeometrie-ausdruecklich-ausgewaehlt)).
  Der Umsetzungsumfang beschraenkt sich auf nichttransparente Vollfarben fuer
  texttragende, nicht ersetzte Inline-Elemente: eine zusammengefasste rechteckige
  Geometrie je Inline-Element und Zeile, mit begrenzter Fragmentarbeit und
  Paint vor dem Zeilentext. Inline-Margins/Padding/Borders, atomare Widgets,
  Line-Break-Elemente, nichtpositive Boxhoehen und Bild-/Gradient-/Clip-/Radius-
  Effekte bleiben ausdruecklich ausgeschlossen. `mark` wird erst nach Layout-/
  Paint-Regressions als End-to-End-Fall integriert (KP-201). Pass 3 ist lokal
  validiert: Build ohne Warnungen, 16.154 Solution-Tests und Formatpruefung
  bestehen. Commit `7bd6e99` ist gepusht; Linux/Windows x64+arm64 und CodeQL-CI
  bestehen ebenfalls (KP-202).
  Pass 4 erweitert Tabellen um positive `rowspan`-Werte und HTMLs
  `rowspan=0`-Semantik bis zum Ende der expliziten Zeilengruppe. Ueberlaeufe
  werden an der Gruppe begrenzt, Tabellenmodell-Ueberlappungen scheitern
  explizit, und erforderliche Mehrhoehe wird gleichmaessig auf betroffene
  Zeilen verteilt; die CSS-2.2-Regel laesst diese Verteilung dem UA. Diese
  Verhaltenswahl hat der Nutzer mit `include-zero` getroffen (KP-203).
  Implementierung, Begrenzung ueber `MaxTableRowSpanOperations`, Paint-
  Reihenfolge und Regressionen sind abgeschlossen. Layout (821), Paint (69),
  Solution-Build (0 Warnungen/Fehler), Volltest (16.163) und Formatpruefung
  bestehen lokal. Commit `a79f119` ist gepusht; Linux x64/arm64, Windows
  x64 und CodeQL bestehen. Der erste Windows-arm64-Lauf traf einen
  Duplicate-Request-Fehler im unabhaengigen Browser-Test
  `UpgradeInsecureRequestsUpgradesPageLinkAndRedirectBeforeTransport`; der
  isolierte arm64-Rerun bestand (KP-204/KP-205).
  Pass 5 implementiert die gewaehlte top-only Caption: eine HTML-Caption als
  `display:table-caption` oberhalb der Tabelle, Wrapperbreite gleich der
  Tabellen-Border-Edge und kein Einfluss auf intrinsische Spaltenbreiten.
  Caption und Tabellenbox behalten getrennte Paint-Geometrie; `caption-side`
  und bottom bleiben unsupported (DEC-028/KP-207/KP-208). CSS (1.139),
  Layout (823), Paint (70) und alle 16.168 Solution-Tests bestehen lokal;
  Solution-Build (0 Warnungen/Fehler), Formatpruefung und `git diff --check`
  bestehen ebenfalls. Commit `b0d0441` ist gepusht; Linux-Entwicklung,
  Windows-Confinement und Push-on-main-CI bestehen (Runs 41387361158,
  41387361276 und 41387360887). Die lokalen HTML- und CSS-2.2-Quellen waren
  am 2026-11-02 innerhalb der 30-Tage-Frist.
  Pass 6 implementiert inzwischen kanonische Column-/Column-Group-Displays,
  begrenzte HTML-`span`-Tracks und absolute CSS-Breitenconstraints fuer
  automatische Tabellen. Prozentbreiten und Spaltenhintergruende bleiben
  ausgeschlossen (Q-025/KP-209/KP-210/KP-211). CSS (1.140) und Layout (834)
  bestehen fokussiert; Solution-Build (0 Warnungen/Fehler) und Formatpruefung
  bestehen ebenfalls. Der Volltest besteht (15.786 Tests). Commit, Push und CI
  sind abgeschlossen: `c677ceb` ist gepusht; Linux-Entwicklung, Windows-
  Confinement und Push-on-main-CI sind gruen (Runs 41403577075, 41403577187
  und 41403576947).
  Pass 7 ist aus der verbleibenden Tabellenluecke abgeleitet: begrenzte solide
  `background-color`-Layer fuer `colgroup`/`col`, pro Herkunftszelle und mit
  vollstaendiger Spanning-Zellflaeche nach CSS 2.2. Die Implementierung und
  fokussierten Tests bestehen lokal (Layout 833, Paint 71), ebenso der
  Solution-Build, der Volltest (16.180 Faelle) und die Formatpruefung.
  Commit `879982f` ist gepusht; Linux x64/arm64, Windows x64/arm64 sowie
  Push-on-main-CI sind gruen (Runs 42340535912, 42340535879 und 42340535497;
  KP-212/KP-214). Pass 8 setzt die Nutzerwahl Q-026/KP-217 um:
  CSS-`vertical-align:middle` und `bottom` positionieren Tabellenzellinhalte
  nach endgueltiger Zeilen-/Row-Span-Hoehe einschliesslich Zwischenabstaenden.
  Baseline, Legacy-`valign` und andere Zellwerte bleiben ausgeschlossen.
  CSS (1.142), Layout (839), Content (99), Paint (71), Solution-Build ohne
  Warnungen/Fehler, Volltest (16.188) und Formatpruefung bestehen lokal.
  Commit `8edd05b` ist gepusht; Linux-Entwicklung, Windows-Confinement
  (x64/arm64) und CodeQL-CI sind nach bestandenem failed-job-only-Rerun gruen.
  Der erste Lauf des unveraenderten Windows-x64-Browsernavigationstests traf
  wiederholte Requests; der Retry bestand (Runs 42357015761, 42357015749,
  42357015436 und 42357742176; KP-219/KP-220).
  Pass 9 leitet begrenzte HTML-`valign`-Hints fuer `td`/`th` aus der
  verbleibenden Tabellenluecke ab: `top`, `middle` und `bottom` verwenden die
  vorhandene CSS-Kaskade und Zell-/Row-Span-Layoutlogik; Baseline sowie
  Zeilen-/Gruppenattribute bleiben ausgeschlossen. CSS (1.149), Layout (852),
  Solution-Build (0 Warnungen/Fehler), Volltest (16.208), Formatpruefung und
  Whitespace-Check bestehen lokal. Commit `ce09478` ist gepusht; Linux
  x64/arm64, Windows x64/arm64 und CodeQL-CI sind gruen (Runs 42362747599,
  42362747677 und 42362747202; KP-221/KP-222). Der CI-Nachtrag `177388f`
  ist ebenfalls gepusht und Linux-, Windows- (x64/arm64) und CodeQL-gruen
  (Runs 42363535903, 42363535693 und 42363535754). Der Nutzer waehlte fuer
  Pass 10 Baselineausrichtung einschliesslich Row-Spans; deren zusaetzliche
  Hoehe wird nach der bestehenden gleichmaessigen Row-Span-Defizitpolitik
  verteilt. Die Umsetzung und lokale Validierung sind dokumentiert in
  [KP-224](./Kontextprotokoll.md#kp-224---pass-10-baselineausrichtung-mit-row-spans).
  Commit `348ad31` ist gepusht; Linux x64/arm64, Windows x64/arm64 und
  CodeQL-CI sind gruen (Runs 42373689351, 42373689357 und 42373689317).
  Ein neuer Nutzerauftrag vom 2026-11-07 verlangt zehn weitere getrennte
  Missing-Prioritaetsinkremente. Pass 1 ergaenzt CSS-2.2-`caption-side:bottom`
  (`ef3247f`); Pass 2 setzt definitive `col`/`colgroup`-Prozentbreiten um
  (`dee2a86`); Pass 3 erweitert `border-spacing` auf beide Achsen (`9102c49`);
  Pass 4 implementiert den HTML-`cellspacing`-Hint (`146952f`); Pass 5 loest
  Prozentconstraints auf Auto-Tabellen begrenzt (`de132ad`). Diese fuenf
  Inkremente sind lokal validiert, gepusht und Linux-, Windows- sowie
  CodeQL-CI-gruen (KP-225 bis KP-230). Pass 6 implementiert aus der
  verbleibenden Tabellenluecke den HTML-`table[width]`-Hint anhand der
  aufgefrischten HTML-Dimensionsregeln. CSS (1.171), Layout (870),
  Solution-Build (0 Warnungen/Fehler), Volltest (16.249), Formatpruefung und
  Whitespace-Check bestehen lokal. `fb73991` ist gepusht; Linux
  (Run 42401385159), Windows-Confinement x64/arm64 (Run 42401385143) und
  CodeQL (Run 42401384901) bestehen. Nach Pass 6 verblieben vier Inkremente
  ([KP-231](./Kontextprotokoll.md#kp-231---pass-6-html-table-width-hint)).
  Pass 7 erweitert die table-row/group `align`-Presentational-Hints auf
  `tr`, `thead`, `tbody` und `tfoot`; `text-align` wird in Zellen vererbt.
  Die HTML-Regeln und die Begrenzung auf diese Layout-Subset-Erweiterung sind
  dokumentiert in [KP-233](./Kontextprotokoll.md#kp-233---pass-7-row-und-row-group-align-hints).
  CSS (1.174), Layout (891), der Solution-Build (0 Warnungen/Fehler), der
  Volltest (16.273), Formatpruefung und Whitespace-Check bestehen lokal.
  `ae7f1ae` ist gepusht; Linux (Run 42474057086), Windows confinement
  x64/arm64 (Run 42474057143) und Push-on-main/CodeQL (Run 42474056897)
  bestehen. Pass 7 ist abgeschlossen; drei Inkremente verbleiben
  ([KP-234](./Kontextprotokoll.md#kp-234---pass-7-gepusht-und-ci-gruen)).
  Pass 8 waehlt als naechsten Tabellen-Rendering-Schritt die von der lokalen
  HTML-Regel definierte `align=absmiddle`-Abbildung auf `text-align:center`
  fuer `td`, `th`, `tr`, `thead`, `tbody` und `tfoot`. CSS (1.177), Layout
  (898), Solution-Build (0 Warnungen/Fehler), Volltest (16.283), Formatpruefung
  und Whitespace-Check bestehen lokal. Commit `25e1ab4` ist gepusht; Linux
  (Run 42478735102), Windows confinement x64/arm64 (Run 42478735101) und
  Push-on-main/CodeQL (Run 42478735123) sind gruen. Pass 8 ist abgeschlossen;
  zwei Inkremente verbleiben
  ([KP-235](./Kontextprotokoll.md#kp-235---pass-8-waehlt-den-html-tabellenalias-absmiddle),
  [KP-236](./Kontextprotokoll.md#kp-236---pass-8-lokal-implementiert-und-validiert),
  [KP-237](./Kontextprotokoll.md#kp-237---pass-8-gepusht-und-ci-gruen)).
  Pass 9 implementiert die beschlossenen row/group-`valign`-Defaults fuer
  Tabellenzellen. CSS (1.183), Layout (908), Solution-Build (0 Warnungen/
  Fehler), Volltest (16.299), Formatpruefung und Whitespace-Check bestehen
  lokal. `dd42bec` ist gepusht; Linux development
  ([Run 42485850844](https://github.com/khmseu/VisualWeb/actions/runs/42485850844)),
  Windows confinement x64/arm64
  ([Run 42485850871](https://github.com/khmseu/VisualWeb/actions/runs/42485850871))
  und Push on main/CodeQL
  ([Run 42485850925](https://github.com/khmseu/VisualWeb/actions/runs/42485850925))
  bestehen. Ein Inkrement verbleibt (KP-238 bis KP-241).
  Pass 10 waehlt begrenzte HTML-`body[bgcolor]`-Unterstuetzung: vorhandener
  Legacy-Color-Parser und Body-Hintergrundpaint werden genutzt, mit eigener
  aggregierter Attributgrenze. CSS (1.191), Content (101), Solution-Build
  (0 Warnungen/Fehler), Volltest (16.308), Formatpruefung und Whitespace-Check
  bestehen lokal. `49ab268` ist gepusht; Linux development
  (Run 42490116966), Windows confinement x64/arm64 (Run 42490116900) und
  Push-on-main/CodeQL (Run 42490116932) bestehen. Der Zehn-Passen-Lauf ist
  abgeschlossen
  ([KP-242](./Kontextprotokoll.md#kp-242---pass-10-als-body-bgcolor-hinweis-abgegrenzt),
  KP-243, KP-244, KP-245,
  [KP-246](./Kontextprotokoll.md#kp-246---pass-10-ci-gruen-zehn-passen-lauf-abgeschlossen)).
- Neuer Auftrag: zehn weitere getrennte Inkremente aus
  [plans/Missing.md](../plans/Missing.md) auswaehlen, testen, committen,
  pushen und CI-validieren. Pass 1 implementiert HTML-`body[text]` als
  begrenzten Legacy-Farb-Hinweis auf `color`; fokussierte CSS- und
  Renderpixeltests, vollstaendige CSS-/Content-Projekte, Solution-Build und
  Formatpruefung bestehen. Der Solution-Volltest hat einen einzelnen V8-
  Deadline-Timeout im Browserprojekt, der isolierte Wiederholungslauf des
  betroffenen Tests besteht; eine Ursache ist nicht nachgewiesen. `a571e76`
  ist gepusht; Linux, Windows x64/arm64 und CodeQL-CI bestehen. Pass 1 ist
  abgeschlossen, neun weitere Inkremente verbleiben. Pass 2 implementiert
  den begrenzten HTML-`body[link]`-Legacy-Farb-Hinweis fuer statische `:link`-
  Elemente; `vlink`/`:visited` und `alink`/`:active` bleiben unsupported.
  CSS/Content, Solution-Build, 16.327 Solution-Tests und Formatpruefung
  bestehen lokal. `8716cd6` ist gepusht; Linux development
  (Run 42503046700), Windows confinement x64/arm64 (Run 42503046721) und
  Push-on-main/CodeQL (Run 42503046339) bestehen. Pass 2 ist abgeschlossen;
  acht Inkremente verbleiben
  ([KP-247](./Kontextprotokoll.md#kp-247---neuer-zehn-passen-lauf-und-pass-1-abgegrenzt),
  KP-248,
  [KP-249](./Kontextprotokoll.md#kp-249---pass-1-committet-gepusht-und-ci-gruen),
  [KP-250](./Kontextprotokoll.md#kp-250---pass-2-waehlt-begrenzten-html-body-link-hinweis),
  KP-251,
  [KP-252](./Kontextprotokoll.md#kp-252---pass-2-committet-gepusht-und-ci-gruen)).
  Pass 3 ist als begrenzte Darstellung fuer `<dir>` ausgewaehlt: direkte
  `li`-Kinder nutzen die vorhandene Aussenmarker-Geometrie samt HTML-UA-
  Block-/Einrueckungs-/Tiefenregeln. Das erweitert keine DOM-List-Owner-
  Semantik, geordnete Listen, Inside-Marker oder beliebige List-Items
  ([KP-253](./Kontextprotokoll.md#kp-253---pass-3-waehlt-begrenzte-dir-listenpraesentation)).
  Pass 3 ist lokal implementiert: CSS (1.209), Layout (909),
  Content (104), Solution-Build (0 Warnungen/Fehler), Volltest (16.330),
  Formatpruefung und Whitespace-Check bestehen. Ein erster Volltest traf zwei
  CPU-Druck-/Renderer-Scope-Fehler; die betroffenen Faelle bestanden isoliert,
  ein cgroup-Pfadfehler reproduzierte auf dem Basiscommit `1b749f6`, und der
  anschliessende Volltest bestand. Commit `1429526` ist gepusht; Linux
  (Run 42510288621), Windows x64/arm64 (Run 42510288595) und CodeQL
  (Run 42510288546) bestehen. Pass 3 ist abgeschlossen; sieben Inkremente
  verbleiben ([KP-254](./Kontextprotokoll.md#kp-254---pass-3-dir-listenpraesentation-implementiert-und-lokal-validiert),
  [KP-255](./Kontextprotokoll.md#kp-255---pass-3-committet-gepusht-und-ci-gruen)).
  Pass-4-Korrektur: `dd`-Einrueckung war bereits vor Commit `9a0f78c` im
  Stylesheet implementiert; dieser Commit fuegte sie versehentlich doppelt
  ein. Die Korrektur entfernt die Dublette und stellt die exakten CSS-Budgets
  wieder her. CSS-/Layouttests und Dokumentation bleiben als Abdeckung der
  vorhandenen Regel. Korrekturcommit `b8a8c83` ist gepusht; Linux, Windows und
  CodeQL sind erfolgreich. Es wurde kein Inkrement dadurch abgeschlossen.
  Pass 4 waehlt die belegte Luecke fuer HTML-`bgcolor` auf `table`, `thead`,
  `tbody`, `tfoot`, `tr`, `td` und `th`: WHATWG HTML `#tables-2` mappt diese
  Attribute ueber den Legacy-Farbparser auf `background-color`; der bisherige
  Code unterstuetzte nur `body[bgcolor]`. Der Umfang nutzt die bestehende
  Presentational-Hint-Kaskade und Tabellen-Hintergrundebenen, mit eigenem
  aggregiertem UTF-16-Budget; Bildattribute und andere Elemente bleiben
  unsupported (KP-260). CSS (1.216), Content (105), Solution-Build (0
  Warnungen/Fehler) und Formatverifikation bestehen. Der erste Volltest traf
  nur den unabhaengigen
  `ProcessTests.CpuThrottledExchangeCanBeCanceledOrTimedOutWithoutAffectingOtherTabs`
  cgroup-`cpu.stat`-Pfadfehler; beide Parameter bestehen isoliert. Browsercode
  und Testquelle sind gegenueber dem Pass-Basiscommit unveraendert; der gleiche
  fehlende Pfad wurde zuvor auf `1b749f6` reproduziert. Der Volltest-Retry
  besteht mit 16.339 Tests. Commit `dff4edd` ist gepusht; Linux development
  (Run 42524681927), Windows confinement (Run 42524681949) und CodeQL
  (Run 42524681823) bestehen. Pass 4 ist abgeschlossen; sechs angeforderte
  Inkremente verbleiben
  ([KP-256](./Kontextprotokoll.md#kp-256---pass-4-waehlt-die-html-einrueckung-von-dd),
  [KP-257](./Kontextprotokoll.md#kp-257---pass-4-dd-einrueckung-implementiert-und-lokal-validiert),
  [KP-258](./Kontextprotokoll.md#kp-258---pass-4-committet-gepusht-und-ci-gruen),
  [KP-259](./Kontextprotokoll.md#kp-259---korrektur-dd-einrueckung-war-bereits-implementiert),
  [KP-260](./Kontextprotokoll.md#kp-260---pass-4-waehlt-tabellen-bgcolor-hints),
  [KP-261](./Kontextprotokoll.md#kp-261---pass-4-lokal-validiert),
  [KP-262](./Kontextprotokoll.md#kp-262---pass-4-committet-gepusht-und-ci-gruen)).
  Pass 5 waehlt die HTML-`table[cellpadding]`-Praesentationsregel: ein
  erfolgreich als nichtnegative Ganzzahl geparster Tabellenwert setzt alle
  vier Paddingseiten zugehoeriger `td`/`th`-Elemente. Jede Tabellenangabe wird
  einmal gegen ein eigenes aggregiertes UTF-16-Limit geprueft; fuer
  verschachtelte Tabellen gilt der naechste Tabellenvorfahr. Andere
  Attributpositionen bleiben unsupported. CSS (1.225), Layout (912),
  Solution-Build (0 Warnungen/Fehler), Formatverifikation und der Volltest-
  Retry (16.350) bestehen. Der erste Volltest traf eine Deadline-Ueberschreitung
  in einem unveraenderten Scripting-Test; dessen isolierter Lauf sowie der
  Volltest-Retry bestehen. Commit `5770646` ist gepusht; Linux development
  (Run 42531254218), Windows confinement (Run 42531254221) und CodeQL
  (Run 42531253690) bestehen. Pass 5 ist abgeschlossen; fuenf Inkremente
  verbleiben ([KP-263](./Kontextprotokoll.md#kp-263---pass-5-waehlt-html-table-cellpadding),
  [KP-264](./Kontextprotokoll.md#kp-264---pass-5-lokal-implementiert-und-validiert),
  [KP-265](./Kontextprotokoll.md#kp-265---pass-5-committet-gepusht-und-ci-gruen)).
  Pass 6 waehlt und implementiert den HTML-Boolean-Hinweis `nowrap` fuer
  `td`/`th`: Attributpraesenz setzt `white-space:nowrap`; der Attributwert
  wird ignoriert, normale Autoren-CSS kann den Hinweis ueberschreiben, und
  `nowrap` an Tabelle, Zeilen und Gruppen bleibt unsupported. CSS (1.229),
  Layout (913), Solution-Build (0 Warnungen/Fehler), Volltest (16.355),
  Formatpruefung und Whitespace-Check bestehen. `e24bf1e` ist gepusht; Linux
  (Run 42536481559), Windows confinement (Run 42536481451) und Push-on-main/
  CodeQL (Run 42536481151) sind gruen. Pass 6 ist abgeschlossen; vier
  Inkremente verbleiben
  ([KP-266](./Kontextprotokoll.md#kp-266---pass-6-td-th-nowrap-lokal-validiert),
  [KP-267](./Kontextprotokoll.md#kp-267---pass-6-committet-gepusht-und-ci-gruen)).
  Pass 7 implementiert HTML-`td`/`th`-`height`-Hints: nichtnull
  Dimensionswerte setzen `height`; positive Pixelwerte wirken als
  Mindestzellhoehe. Prozentuale Zellhoehen scheitern explizit am Layout, da
  CSS 2.2 deren Zeilenhoehenalgorithmus nicht definiert. Andere
  Tabellen-/Zeilen-/Gruppen-`height`-Attribute bleiben unsupported. CSS
  (1.233), Layout (914), Solution-Build (0 Warnungen/Fehler), Volltest
  (16.360), Formatpruefung und Whitespace-Check bestehen. Commit, Push und CI
  sind abgeschlossen; `d572657` ist auf `main`; Linux (Run 42540838824),
  Windows confinement (Run 42540838782) und CodeQL (Run 42540838464) sind
  gruen. Pass 7 ist abgeschlossen; drei weitere Inkremente
  verbleiben ([KP-268](./Kontextprotokoll.md#kp-268---pass-7-waehlt-html-td-th-height),
  [KP-269](./Kontextprotokoll.md#kp-269---pass-7-lokal-validiert),
  [KP-270](./Kontextprotokoll.md#kp-270---pass-7-committet-gepusht-und-ci-gruen)).
  Pass 8 bildet nichtnull `td`/`th`-`width`-Dimensionen auf CSS `width` ab;
  positive Pixelwerte wirken in den vorhandenen intrinsischen Zellbreiten-
  constraints einschliesslich `colspan`-Verteilung. Prozentbreiten und
  `width`-Attribute anderer Tabellenelemente bleiben unsupported. CSS (1.237),
  Layout (915), Build, Formatpruefung und alle 16.365 Solution-Tests bestehen
  lokal. `5da1a7f` ist nach `main` gepusht; Linux (Run 42545600550), Windows
  confinement (Run 42545600498) und CodeQL (Run 42545600021) sind gruen.
  Pass 8 ist abgeschlossen; zwei Inkremente verbleiben
  ([KP-271](./Kontextprotokoll.md#kp-271---pass-8-waehlt-td-th-width),
  [KP-272](./Kontextprotokoll.md#kp-272---pass-8-lokal-validiert),
  [KP-273](./Kontextprotokoll.md#kp-273---pass-8-committet-gepusht-und-ci-gruen)).
- Vorheriger Auftrag: am 2026-10-31 zehn weitere getrennt validierte,
  committete, gepushte und CI-gepruefte Missing-Prioritaetsinkremente bearbeiten
  ([KP-106](./Kontextprotokoll.md#kp-106---erneuter-auftrag-und-erstes-textinkrement)).
  Ausgangspunkt war `1bf9b74` mit zwei damals ungeschobenen lokalen Commits.
  Pass 1 unterstuetzt begrenzte ASCII-E-Mail-Adressen im Textlayout; alle
  15.403 Solution-Tests, Build, Formatpruefung und lokaler V8-Smoke bestanden.
  Commit `fd11978` ist gepusht; Linux, Windows und CodeQL bestanden,
  einschliesslich x64 und arm64. Pass 2 unterstuetzt begrenzte negative
  Zahlen, Datumsangaben und Brueche sowie Umbrueche neben vorhandenen
  Inline-Controls; direkte GL/Control-Nachbarschaft in Umbruchmodi scheitert
  mangels Backtracking explizit
  ([KP-107](./Kontextprotokoll.md#kp-107---zahlen-und-datumsangaben-im-textlayout)).
  Build, Formatpruefung, lokaler V8-Smoke und alle 15.753 Solution-Tests
  bestehen, darunter 350 netto neue Faelle. Commit `e1c4ae2` ist gepusht;
  Linux, Windows und CodeQL bestanden, einschliesslich x64 und arm64. Pass 3
  bearbeitet fehlende HTML-UA-Schriftdefaults fuer `small`, `big`, `dfn` und
  `tt`. Build, Formatpruefung, lokale und sandboxed V8-Smokes, Linux-
  Sandbox-Smoke und alle 15.780 Solution-Tests bestehen, darunter 27 neue
  Faelle. Commit `e6eb02d` ist gepusht; Linux, Windows und CodeQL bestanden,
  einschliesslich x64 und arm64
  ([KP-108](./Kontextprotokoll.md#kp-108---html-schriftdefaults)). Pass 4
  ergaenzt die fehlende HTML-UA-Farbe fuer unbesuchte Links. Build,
  Formatpruefung, lokaler V8-Smoke und alle 15.806 Solution-Tests bestehen,
  darunter 26 neue Faelle. Commit `154a304` ist gepusht; Linux, Windows
  und CodeQL bestanden, einschliesslich x64 und arm64
  ([KP-109](./Kontextprotokoll.md#kp-109---standardfarbe-fuer-links),
  [KP-110](./Kontextprotokoll.md#kp-110---link-ci-und-auswahl-des-fuenften-inkrements)).
  Pass 5 implementiert den gewoehnlichen HTML-`hidden`-Zustand ueber die
  UA-Cascade und `display:none`; versteckte Controls bleiben nach den
  regulaeren Regeln fuer Formulardaten beruecksichtigt. Buttonbeschriftungen
  filtern nicht gerenderte Nachkommen, Fragmentziele beachten die
  DOM-Reihenfolge. Gerenderte Selects mit `display:none`-Nachkommen,
  sichtbares `hidden=until-found` und die
  spezielle `<embed hidden>`-Darstellung scheitern explizit. 139 fokussierte
  Faelle, Solution-Build, Formatpruefung und lokaler V8-Smoke bestanden fuer
  den ersten Stand; der Review fand fehlende Inline-Widget-Fragmentziele und
  doppelt belastete Fragment-ID-/Namensbudgets. Laut Implementierungsbericht
  wurden beide korrigiert; 134 fokussierte Faelle, Build, Formatpruefung und
  lokaler V8-Smoke bestehen fuer diesen Stand. Der Folge-Review bestaetigte
  beide Korrekturen, fand aber unzureichend begrenzte wiederholte
  Vorfahrenscans. Laut Implementierungsbericht merkt die Nachbesserung
  vollstaendige Vorfahrenketten je Durchlauf und begrenzt die Besuche
  gemeinsam; 55 fokussierte Browser- und 3 IPC-Faelle sowie Build,
  Formatpruefung und lokaler V8-Smoke bestehen. Der Folge-Review ist
  freigegeben; alle 15.907 Solution-Tests bestehen. Commit `37dc296` ist
  gepusht; Linux, Windows und CodeQL bestehen, einschliesslich x64 und arm64
  ([KP-110](./Kontextprotokoll.md#kp-110---link-ci-und-auswahl-des-fuenften-inkrements),
  [KP-111](./Kontextprotokoll.md#kp-111---hidden-darstellung-lokal-implementiert),
  [KP-112](./Kontextprotokoll.md#kp-112---review-der-fragmentgrenzen),
  [KP-113](./Kontextprotokoll.md#kp-113---korrektur-der-fragmentziele),
  [KP-114](./Kontextprotokoll.md#kp-114---folge-review-der-vorfahrenscans),
  [KP-115](./Kontextprotokoll.md#kp-115---begrenzte-vorfahrenscans),
  [KP-116](./Kontextprotokoll.md#kp-116---reviewfreigabe-fuer-fragmentziele),
  [KP-117](./Kontextprotokoll.md#kp-117---vollstaendige-hidden-validierung),
  [KP-118](./Kontextprotokoll.md#kp-118---hidden-commit-und-ci-start),
  [KP-119](./Kontextprotokoll.md#kp-119---hidden-linux-und-codeql-erfolgreich),
  [KP-120](./Kontextprotokoll.md#kp-120---hidden-windows-ci-erfolgreich)).
  Pass 6 ergaenzt die HTML-UA-Defaults fuer `listing`, `plaintext` und `xmp`
  ueber die vorhandenen `pre`-Regeln. Gezielte CSS-, Parser-, Layout-,
  native Content- und lokale/Prozess-Browser-Tests sowie Build, Format und
  lokaler V8-Smoke bestehen; die Review-Ergaenzungen bestehen fokussiert.
  Der abschliessende Volltest mit den Ergaenzungen besteht mit 15.943
  Faellen, ebenso die abschliessende Formatpruefung. Commit `e4d0b79` ist
  gepusht; Linux, Windows und CodeQL bestehen fuer diesen Commit.
  Windows x64 scheiterte zunaechst an einem Timeout in einem unveraenderten
  Shell-Test; Wiederholungen des aktuellen und des vorherigen Commits
  bestehen. Ein kuenstlich verzoegerter Baseline-Versuch scheiterte ebenfalls,
  jedoch mit anderem Deadline-Fehler; die genaue Ursache des urspruenglichen
  Windows-Timeouts bleibt offen. Alle temporaeren Codeaenderungen sind
  entfernt, gegen `e4d0b79` geprueft und der normale lokale Release-Test
  besteht
  ([KP-121](./Kontextprotokoll.md#kp-121---rohtextdarstellung-als-sechstes-inkrement),
  [KP-122](./Kontextprotokoll.md#kp-122---rohtextdefaults-implementiert),
  [KP-123](./Kontextprotokoll.md#kp-123---review-regressionen-ergaenzt),
  [KP-124](./Kontextprotokoll.md#kp-124---review-regressionen-validiert),
  [KP-125](./Kontextprotokoll.md#kp-125---formatpruefung-erfolgreich),
  [KP-126](./Kontextprotokoll.md#kp-126---rohtext-volltest-erfolgreich),
  [KP-127](./Kontextprotokoll.md#kp-127---rohtext-commit-und-ci-start),
  [KP-128](./Kontextprotokoll.md#kp-128---rohtext-codeql-erfolgreich),
  [KP-129](./Kontextprotokoll.md#kp-129---rohtext-linux-ci-erfolgreich),
  [KP-130](./Kontextprotokoll.md#kp-130---windows-x64-testfehler),
  [KP-131](./Kontextprotokoll.md#kp-131---windows-wiederholungen-erfolgreich),
  [KP-132](./Kontextprotokoll.md#kp-132---baseline-timingversuch-vorbereitet),
  [KP-133](./Kontextprotokoll.md#kp-133---baseline-timingversuch-beendet),
  [KP-134](./Kontextprotokoll.md#kp-134---pass-6-ci-gruen)).
  Pass 7 ergaenzt die fehlende HTML-UA-Blockdarstellung von `search`:
  Der Parser behandelt es bereits als Blockstart, Block- und Formular-
  Nachkommen nutzen nun das vorhandene Blocklayout. Fokussierte Tests, Build,
  Format und lokaler V8-Smoke bestehen; die Review-Ergaenzungen bestehen
  fokussiert, der abschliessende Build und alle 15.953 Solution-Tests
  bestehen. Commit `087954f` ist gepusht; Linux, Windows und CodeQL bestehen
  fuer genau diesen Stand
  ([KP-135](./Kontextprotokoll.md#kp-135---search-blockdefault-ausgewaehlt),
  [KP-136](./Kontextprotokoll.md#kp-136---search-blockdefault-implementiert),
  [KP-137](./Kontextprotokoll.md#kp-137---search-review-ergaenzt),
  [KP-138](./Kontextprotokoll.md#kp-138---search-volltest-gestartet),
  [KP-139](./Kontextprotokoll.md#kp-139---search-volltest-bestanden),
  [KP-140](./Kontextprotokoll.md#kp-140---search-commit-und-ci-start),
  [KP-141](./Kontextprotokoll.md#kp-141---search-ci-bestanden)).
  Pass 8 implementiert die HTML-Praesentationshinweise `align` auf Absaetzen
  und Ueberschriften. Fokussierte CSS-, Layout-, Content- und Browser-Tests
  bestehen; der unabhaengige Review ist freigegeben und Solution-Build,
  Gesamtformatierung und lokaler V8-Smoke bestehen. Im Volltest scheiterte ein
  unveraenderter Browser-Navigationstest; 15.986 Faelle bestanden. Fuenf
  fokussierte Wiederholungen bestehen. Auf der isolierten Baseline reproduzierte
  ein testseitiger 150ms-Transportverzug dieselbe Assertion; mit Return nur
  bei ausstehendem Dialog bestand der Versuch. Die genaue Ursache des
  urspruenglichen Fehlers bleibt offen. Die Volltest-Wiederholung bestand
  ebenfalls 15.986 Faelle und scheiterte am anderen, auf Baseline `37dc296`
  mit Verzoegerung und gleichem Deadline-Fehler reproduzierten Shell-Test.
  Beide Tests bestehen in drei anschliessenden fokussierten Wiederholungen.
  Ursachen der uninstrumentierten Fehler bleiben offen. Commit `257f0b1` ist
  gepusht; Linux, Windows und CodeQL bestehen fuer diesen Commit
  ([KP-142](./Kontextprotokoll.md#kp-142---absatz-und-ueberschriftenausrichtung-ausgewaehlt),
  [KP-143](./Kontextprotokoll.md#kp-143---ausrichtung-implementiert-und-reviewt),
  [KP-144](./Kontextprotokoll.md#kp-144---format-und-v8-smoke-bestanden),
  [KP-145](./Kontextprotokoll.md#kp-145---ausrichtung-volltest-laeuft),
  [KP-146](./Kontextprotokoll.md#kp-146---browser-volltestfehler),
  [KP-147](./Kontextprotokoll.md#kp-147---baselineversuch-vorbereitet),
  [KP-148](./Kontextprotokoll.md#kp-148---baseline-timingreproduktion),
  [KP-149](./Kontextprotokoll.md#kp-149---volltest-wiederholung-laeuft),
  [KP-150](./Kontextprotokoll.md#kp-150---zweiter-browser-volltestfehler),
  [KP-151](./Kontextprotokoll.md#kp-151---navigationstests-fokussiert-bestanden),
  [KP-152](./Kontextprotokoll.md#kp-152---ausrichtung-commit-und-ci-start),
  [KP-153](./Kontextprotokoll.md#kp-153---ausrichtung-ci-bestanden)).
  Pass 9 implementiert die HTML-Altfarbzuordnung von `font[color]` mit der
  eigenstaendigen Altfarbregel, der vorhandenen CSS-Namenstabelle und einer
  gemeinsamen Grenze fuer rohe Attributzeichen. Laut Implementierungsbericht
  bestehen alle 1.053 CSS-Faelle sowie fokussierte Content- und Browser-
  Tests; der direkte Review fand bisher keinen offenen Fehler. Der Solution-
  Build, die gesamte Formatpruefung, alle 78 Content-Tests und der lokale
  V8-Smoke bestehen. Im seriellen Volltest bestanden 16.009 Faelle; der
  unveraenderte UIR-Navigationstest scheiterte mit ausstehender Autorisierung; ein
  frueherer Fehler desselben Tests hatte eine andere Deadline-Meldung. Der
  unveraenderte Baseline-Test besteht; ein testseitiger 150ms-Verzug auf
  `257f0b1` reproduziert andere Fehler desselben Tests, nicht aber die genaue
  aktuelle Meldung. Drei aktuelle fokussierte Wiederholungen bestehen. Die
  Ursache des Volltestfehlers bleibt offen. Der abschliessende Build und
  die Formatpruefung bestehen; Commit `03025fb` ist gepusht, Linux, Windows
  und CodeQL bestehen fuer diesen Commit
  ([KP-154](./Kontextprotokoll.md#kp-154---altfarbhint-als-neuntes-inkrement),
  [KP-155](./Kontextprotokoll.md#kp-155---altfarbhint-implementiert),
  [KP-156](./Kontextprotokoll.md#kp-156---altfarbe-format-und-smoke-bestanden),
  [KP-157](./Kontextprotokoll.md#kp-157---uir-volltestfehler),
  [KP-158](./Kontextprotokoll.md#kp-158---uir-baseline-timingversuch),
  [KP-159](./Kontextprotokoll.md#kp-159---altfarbe-abschlusspruefung),
  [KP-160](./Kontextprotokoll.md#kp-160---altfarbe-commit-und-ci-start),
  [KP-161](./Kontextprotokoll.md#kp-161---altfarbe-ci-bestanden)).
  Fuer Pass 10 ist der bisher fehlende HTML-Hinweis `font[size]` ausgewaehlt.
  Er ist gegenueber der Farbaenderung ein eigener Groessen- und Layoutpfad:
  Die HTML-Regel verarbeitet vorzeichenbehaftete Altwerte und bildet sie
  auf vorhandene absolute CSS-Schriftgroessen ab. Nach dem Review bestehen
  alle 16.043 Solution-Tests (darunter 1.083 CSS-, 79 Content- und
  1.333 Browser-Faelle), der Solution-Build, die Formatpruefung und der
  lokale V8-Smoke.
  Beim direkten Review wurden zwei zeitabhaengige Cancellation-Tests entfernt;
  lange Eingaben und vorab abgebrochene Verarbeitung bleiben getestet, ein
  deterministischer Abbruch mitten im Scan ist nicht nachgewiesen.
  Commit `80e9dac` ist gepusht; Linux und Windows bestehen jeweils auf
  x64 und arm64, ebenso CodeQL. Damit sind alle zehn angeforderten
  Inkremente committed, gepusht und CI-gruen
  ([KP-162](./Kontextprotokoll.md#kp-162---altschriftgroesse-als-zehntes-inkrement),
  [KP-163](./Kontextprotokoll.md#kp-163---altschriftgroesse-implementiert-und-reviewt),
  [KP-164](./Kontextprotokoll.md#kp-164---altschriftgroesse-validierung-laeuft),
  [KP-165](./Kontextprotokoll.md#kp-165---altschriftgroesse-volltest-bestanden),
  [KP-166](./Kontextprotokoll.md#kp-166---altschriftgroesse-commit-und-ci-start),
  [KP-167](./Kontextprotokoll.md#kp-167---altschriftgroesse-linux-und-codeql-bestanden),
  [KP-168](./Kontextprotokoll.md#kp-168---altschriftgroesse-windows-und-auftrag-abgeschlossen)).
- Vorheriger Auftrag: am 2026-10-31 erneut zehn getrennt validierte, committed,
  gepushte und CI-gepruefte Missing-Prioritaetsinkremente bearbeiten
  ([KP-094](./Kontextprotokoll.md#kp-094---erneuter-zehn-passen-auftrag)).
  Pass 1 ist lokal und in CI validiert: das zuvor abgelehnte CSS-Listenmarker-
  Keyword `circle` erzeugt einen hohlen Marker durch CSS, Layout und Paint.
  Commit `58afc50` ist gepusht; Linux, Windows und CodeQL bestanden.
  Pass 2 implementiert die HTML-UA-Defaults fuer verschachtelte Listen mit den
  vorhandenen Kreis- und Quadratmarkern. Der Build und alle 15.045 Solution-
  Tests bestehen; die drei bereits auf `b47a5a0` reproduzierten
  Format-Baseline-Findings bestehen unveraendert. Die geaenderten C#-Dateien
  bestehen eine separate Formatpruefung. Commit `3189f7e` ist gepusht;
  Linux, Windows und CodeQL bestanden, einschliesslich x64 und arm64
  ([KP-095](./Kontextprotokoll.md#kp-095---ua-marker-fuer-verschachtelte-listen)).
  Pass 3 bearbeitet HTML-UA-Aussenabstaende fuer verschachtelte Listen:
  ausserhalb einer anderen Liste 1em, innerhalb null; direkte CSS-/Layout- und
  Renderingregressionen wurden zuerst angelegt und scheiterten wie erwartet.
  Der Build und alle 15.065 Solution-Tests bestehen; die geaenderten
  C#-Dateien bestehen die separate Formatpruefung. Der Gesamtformatter meldet
  nur die drei bekannten Baseline-Findings. Commit `67f296b` ist gepusht;
  Linux, Windows und CodeQL bestanden, einschliesslich x64 und arm64
  ([KP-096](./Kontextprotokoll.md#kp-096---ua-abstaende-fuer-verschachtelte-listen)).
  Pass 4 bearbeitet die vorhandenen Markerwerte als begrenzte HTML-`type`-
  Praesentationshinweise mit eigener Cascade-Prioritaet; unbekannte Werte
  bleiben explizite Layoutfehler. Test-first-Regressionsfaelle wurden vor der
  Umsetzung ausgefuehrt und scheiterten wie erwartet. Build, V8-Smoke und
  alle 15.097 Solution-Tests bestehen; der Gesamtformatter meldet nur drei
  auf `b47a5a0` reproduzierte Baseline-Findings. Zwei betreffen ungeaenderte
  Zeilen der geaenderten `StaticLayout.cs`; die anderen geaenderten C#-Dateien
  bestehen eine separate Formatpruefung. Commit `5beedb0` ist gepusht;
  Linux, Windows und CodeQL bestanden, einschliesslich x64 und arm64
  ([KP-097](./Kontextprotokoll.md#kp-097---html-listentypen-als-praesentationshinweise)).
  Pass 5 ergaenzt ASCII-Alphabetmarker fuer CSS `lower-alpha`/`upper-alpha`
  und ihre `latin`-Aliase sowie die exakten HTML-Hinweise `type=a`/`type=A`.
  Positive Ordinalwerte laufen bijektiv weiter; null und negative Werte
  verwenden Dezimalmarker. Build, lokaler V8-Smoke und alle 15.151 Solution-
  Tests bestehen. Der Gesamtformatter meldet nur drei auf `b47a5a0`
  reproduzierte Baseline-Findings, davon zwei in ungeaenderten Zeilen der
  geaenderten `StaticLayout.cs`; alle anderen geaenderten C#-Dateien bestehen
  die separate Formatpruefung. Commit `0e71eb6` ist gepusht; Linux, Windows
  und CodeQL bestanden, einschliesslich x64 und arm64
  ([KP-098](./Kontextprotokoll.md#kp-098---alphabetische-listenmarker)).
  Pass 6 ergaenzt die CSS-Marker `lower-roman`/`upper-roman` und die exakten
  HTML-Hinweise `type=i`/`type=I`; nur Werte 1 bis 3999 nutzen roemische
  Darstellung, sonst gilt der CSS-Decimal-Fallback. Erweiterte ueberstrichene
  Formen sind nicht implementiert. Die gezielten Regressionen bestehen,
  einschliesslich aller 3.999 Werte beider Schreibweisen gegen eine
  unabhaengige Stellenwerttabelle. Solution-Build, lokaler V8-Smoke und alle
  15.212 Solution-Tests bestehen; der Solutionformatter meldet nur die drei
  bekannten Baseline-Findings und die uebrigen geaenderten C#-Dateien bestehen
  die gezielte Formatpruefung. Commit `69f0610` ist gepusht; Linux, Windows
  und CodeQL bestanden, einschliesslich x64 und arm64
  ([KP-099](./Kontextprotokoll.md#kp-099---roemische-listenmarker)).
  Pass 7 ergaenzt CSS-`decimal-leading-zero` fuer vorhandene Listenmarker;
  die Mindestbreite zwei zaehlt ein Minuszeichen mit (`-1.`, aber `01.` und
  `00.`). HTML-`type`-Hinweise bleiben unveraendert. Gezielt getestete CSS-,
  Layout-, Paint-, native Pixel- und lokale/Prozess-Browserfaelle bestehen,
  ebenso Solution-Build, lokaler V8-Smoke und alle 15.247 Solution-Tests.
  Der Solutionformatter meldet nur die drei bekannten Baseline-Findings;
  die uebrigen geaenderten C#-Dateien bestehen die separate Formatpruefung.
  Commit `9e3fdd1` ist gepusht; Linux, Windows und CodeQL bestanden,
  einschliesslich x64 und arm64
  ([KP-100](./Kontextprotokoll.md#kp-100---nullaufgefuellte-dezimalmarker)).
  Pass 8 ergaenzt begrenztes `<menu>`-Listenlayout mit denselben UA-Block- und
  Paddingwerten, Markern und Budgets wie `<ul>`; verschachtelte Listen und
  `li`-Hinweise bleiben unterstuetzt. `dir`-Listenlayout und spezielle Toolbar-
  APIs bleiben ausgenommen. Build, lokaler V8-Smoke und alle 15.260 Solution-
  Tests bestehen, darunter 13 netto neue Faelle; der Gesamtformatter meldet nur
  die drei bekannten Baseline-Findings und die uebrigen geaenderten C#-Dateien
  bestehen die separate Formatpruefung. Commit `54b85f5` ist gepusht; Linux,
  Windows und CodeQL bestanden, einschliesslich x64 und arm64
  ([KP-102](./Kontextprotokoll.md#kp-102---menu-listenlayout)).
  Pass 9 erweitert die begrenzte Textformatierung: `.`, `,`, `:` und `;`
  zwischen ASCII-Buchstaben oder Ziffern in einem Textknoten bleiben auch bei
  Zeilenumbruch ungetrennt. Beispiele wie `example.com`, `1,000.25` und `12:30`
  scheitern nicht mehr; allgemeiner Unicode-Umbruch und andere Kontexte bleiben
  ausgenommen. Build, lokaler V8-Smoke und alle 15.308 Solution-Tests bestehen,
  darunter 48 neue Faelle; zwei zwischenzeitliche Deadlinetests bestanden
  einzeln und im letzten serialisierten Lauf. Der Gesamtformatter meldet nur
  die drei bekannten Baseline-Findings, geaenderte Testdateien bestehen separat.
  Commit `872507e` ist gepusht; Linux, Windows und CodeQL bestanden,
  einschliesslich x64 und arm64
  ([KP-103](./Kontextprotokoll.md#kp-103---ascii-infixtrennzeichen-im-text)).
  Pass 10 beseitigt die Layoutablehnung fuer `text-align:justify` mit
  begrenztem englischem Wortzwischenraum-Blocksatz. Nur weiche Umbruchzeilen
  mit positiven Restbreiten dehnen kollabierte ASCII-Leerzeichen; End- und
  Zwangsumbruchzeilen bleiben linksbuendig. Quellen, Glyphen- und Auswahl-
  geometrie bleiben erhalten; benoetigte NBSP-Dehnung und fehlende Leerzeichen-
  glyphen scheitern explizit. Build, lokaler V8-Smoke und alle 15.329
  Solution-Tests bestehen, darunter 21 netto neue Faelle. Auf den Folgeauftrag
  hin wurden die drei verbleibenden Formatdiagnosen in `StaticLayout.cs` und
  `ShellChrome.cs` behoben; vollstaendige Solution-Formatpruefung, Build,
  Layout- und Browser-Tests bestehen ([KP-105](./Kontextprotokoll.md#kp-105---formatdiagnosen-behoben)).
  Commit `05dffda` ist gepusht; Linux und Windows auf x64/arm64 sowie CodeQL
  bestanden. Damit ist die angeforderte Zehn-Passen-Serie abgeschlossen
  ([KP-104](./Kontextprotokoll.md#kp-104---begrenzter-wortzwischenraum-blocksatz)).
  Die am 2026-10-30 angeforderte Zehn-Passen-Serie (KP-083 bis KP-092) und
  der anschliessende Dokumentationsauftrag (KP-093) sind abgeschlossen.
  Aeltere Serienstaende und deren damalige naechste Schritte bleiben unten als
  historische Evidenz erhalten. Alte Quellen wurden nicht pauschal neu geprueft.
- Zusatzkorrektur am 2026-10-31: Die Problems-Pruefung meldete keine Fehler in
  [.trunk/trunk.yaml](../.trunk/trunk.yaml), aber ungueltige Markdown-Fragmente
  und anschliessend Tabellenalignment in diesem Wissensstand. Die Verweise und
  Tabellenabstaende wurden korrigiert; die erneute Problems-Pruefung meldet in
  beiden Dateien keine Fehler ([KP-101](./Kontextprotokoll.md#kp-101---markdown-diagnosen)).
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

**Prioritaetsergaenzung REQ-011 (Nutzeranweisung, 2026-10-31):** WebDriver
und/oder WebDriver BiDi werden als potenziell hilfreiches, prioritaetsuebergreifendes
Debugging-Werkzeug vorgeschlagen; Protokollwahl und Umfang bleiben offen.
WAI-ARIA und WebMCP sind explizite niedrigere Prioritaeten. Dies ist keine
Implementierungs- oder Konformitaetszusage; der Fokus auf einfache Websites und
die bestehenden Schutzgrenzen bleiben erhalten. Vor dem Commit bestanden
`git diff --check` und `dotnet test VisualWeb.slnx --no-build` mit 14.972 Tests
auf vorhandenen Buildausgaben; ein neues Feature wurde damit nicht validiert
([KP-093](./Kontextprotokoll.md#kp-093---webdriver-debugging-und-spaetere-standards)).

**Implementierungsauftrag vom 2026-10-30 (abgeschlossen):** Der Nutzer hat erneut die
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
auf dem Ausgangscommit `ad0bca6` reproduziert. Commit `9f8aa41` ist gepusht;
Linux, Windows x64/ARM64 und CodeQL sind erfolgreich
([KP-083](./Kontextprotokoll.md#kp-083---neuer-zehn-passen-auftrag-und-css-laengen)).

**Pass 2 (abgeschlossen):** Moderne `rgb()`/`rgba()`-Werte mit gemischten
Zahlen/Prozenten, optionalem Slash-Alpha und `none` als Null werden auf die
vorhandenen statischen 8-Bit-sRGB-Farbwerte abgebildet. Auch benachbarte
getrennte CSS-Tokens sind gueltig; Legacy-Kommasyntax, atomare Shorthands
und bestehende Budgets bleiben erhalten. Farbraeume und Interpolation werden
nicht erweitert. Der Build ist erfolgreich; 644 CSS-, 22 Content- und alle
14.711 Solution-Tests bestehen, einschliesslich 34 neuer CSS-Faelle und eines
nativen Renderingtests. Die Formatpruefung zeigt nur die drei auf `ad0bca6`
reproduzierten Baseline-Fehler. Commit `e110697` ist gepusht; Linux, Windows
x64/ARM64 und CodeQL sind erfolgreich
([KP-084](./Kontextprotokoll.md#kp-084---moderne-rgb-syntax)).

**Pass 3 (abgeschlossen):** `hsl()`/`hsla()` ist als weiterer haeufiger
Stylesheet-Blocker umgesetzt. Der finite Parser rechnet In-Gamut-Werte
einschliesslich Winkel, Komma-/moderner Syntax und Alpha in vorhandenes
8-Bit-sRGB um; Out-of-Gamut-Mapping bleibt explizit diagnostiziert. Der Build,
693 CSS-, 23 Content- und alle 14.761 Solution-Tests bestehen. Darunter sind
49 neue CSS-Faelle und ein nativer Rendering-/Resizefall. Die Formatpruefung
meldet nur die drei auf `ad0bca6` reproduzierten Baseline-Fehler; die geaenderten
C#-Dateien bestehen die separate Formatpruefung. Commit `c9e5f59` ist gepusht;
Linux, Windows x64/ARM64 und CodeQL bestehen
([KP-085](./Kontextprotokoll.md#kp-085---begrenzte-hsl-farben)).

**Pass 4 (abgeschlossen):** Der statische Selektor `:any-link` erkennt HTML-`a`
und `area` anhand eines vorhandenen `href`, auch bei leerem oder nicht
navigierbarem Wert. Er wird von Stylesheets und DOM-Abfragen geteilt;
Verlauf und Navigationsberechtigung bleiben unveraendert. Vor der Umsetzung
scheiterten 20 gezielte CSS-Faelle; danach bestehen 28 neue Regressionen,
CSS 717, Scripting 351, Content 24 und alle 14.789 Solution-Tests. Der
Solution-Build und der lokale V8-Smoke bestehen. Die Formatpruefung meldet
nur die drei auf `ad0bca6` reproduzierten Baseline-Fehler; geaenderte
C#-Dateien bestehen die separate Pruefung. Commit `d473814` ist gepusht;
Linux, Windows x64/ARM64 und CodeQL bestehen
([KP-086](./Kontextprotokoll.md#kp-086---statischer-any-link-selektor)).

**Pass 5 (abgeschlossen):** Verschachtelte CSS-`@media`-Gruppen unterstuetzen
nur leere oder kommaseparierte `all`-/`screen`-/`print`-Listen fuer den
bereits bestehenden Screen-Modus. Cascade-Reihenfolge, Syntaxdiagnosen,
gemeinsame Arbeitsbudgets und die bisherige Import-Sicherheitsgrenze bleiben
erhalten; Bedingungen und weitere Medientypen bleiben nicht unterstuetzt.
36 neue Regressionen, CSS 748, Content 25, Browser 1.198 und alle 14.825
Solution-Tests bestehen. Der Build hat null Warnungen/Fehler; geaenderte
C#-Dateien bestehen die separate Formatpruefung. Die Solution-Formatpruefung
meldet nur die drei auf `ad0bca6` reproduzierten Baseline-Findings.
Commit `9864bad` ist gepusht; Linux, Windows x64/ARM64 und CodeQL bestehen
([KP-087](./Kontextprotokoll.md#kp-087---begrenzte-screen-media-regeln)).

**Pass 6 (abgeschlossen):** Gewoehnliche ASCII-hyphenierte Woerter sind im
Wrapping-Modus umgesetzt: Weiche Umbrueche nach internen Bindestrichen
zwischen ASCII-Buchstaben eines Textknotens teilen nur bei Bedarf den Shaping-Lauf.
Wiederholte Analyse hat ein gemeinsames Arbeitsbudget; andere Interpunktions-
und Unicode-Umbrueche bleiben ausgeschlossen. 30 fokussierte Layout-Faelle,
ein nativer Pixel-/Resizefall und je ein lokaler und Prozess-Renderer-Fall
einschliesslich sichtbarer Textauswahl bestehen. Der Solution-Build sowie
alle 138 Layout-, 26 Content- und 1.200 Browser-Tests bestehen. Die
Solution-Formatpruefung meldet nur die drei auf `ad0bca6` reproduzierten
Baseline-Findings; die geaenderten Testdateien bestehen separat. Alle 14.852
Solution-Tests bestehen, darunter 27 neue Regressionen. Commit `e803d75` ist
gepusht; Linux, Windows und CodeQL bestehen
([KP-088](./Kontextprotokoll.md#kp-088---bindestrichumbrueche)).

**Pass 7 (abgeschlossen):** Das haeufige `text-decoration:none` blockierte
als unbekannte CSS-Property ansonsten darstellbare Seiten. Fuer
`text-decoration` und `text-decoration-line` sind jetzt ausschliesslich `none`
und die vorhandenen CSS-weiten Werte `initial`, `inherit`, `unset` und `revert`
umgesetzt; `revert-layer` bleibt ausgeschlossen. `none` erzeugt keine und
verhindert keine vom Vorfahren stammende Dekoration. Sichtbare Linien und
weitere Decoration-Longhands bleiben diagnostiziert; die UA unterstreicht Links
noch nicht. Nach erfolgreichem Standard-Download bestehen 33 fokussierte
CSS-Faelle, ein nativer Pixel-/Resizefall und je ein lokaler und
Prozess-Renderer-Fall.
Der Build sowie 781 CSS-, 27 Content- und 1.202 Browser-Tests bestehen;
alle 14.888 Solution-Tests einschliesslich 36 neuer Regressionen sind gruen.
Ein vorheriger Browser-Gesamtlauf hatte einen Fehlschlag bei 1.202 Tests;
der betroffene Test bestand danach isoliert und im Solution-Rerun. Ursache
noch unbewiesen. Die geaenderten C#-Dateien bestehen die separate
Formatpruefung; die Solution-Pruefung meldet die drei auf `ad0bca6`
reproduzierten Baseline-Findings. Commit `ae43073` ist gepusht; Linux, Windows
und CodeQL bestehen
([KP-089](./Kontextprotokoll.md#kp-089---css-text-decoration-none)).

**Pass 8 (abgeschlossen):** Als naechster einfacher Render-Blocker wurde
`list-style-type:square` einschliesslich des vorhandenen `list-style`-
Shorthands eingegrenzt. Der bestehende CSS-Parser diagnostiziert diesen Wert;
das lokale CSS-2.2-Listenreferenzdokument laesst das genaue Aussehen der
Markierung durch die UA festlegen. Ein ausgefuelltes Quadrat nutzt jetzt den
bestehenden Outside-Markerabstand und die aktuelle Textfarbe; andere
Markerarten und Legacy-HTML-`type` bleiben ausgeschlossen. Fokussierte
Cascade-, Layout-, Pixel- und lokale/Prozess-Renderer-Tests sowie der Build
bestehen. Vollstaendig bestehen 796 CSS-, 141 Layout-, 42 Paint-, 28
Content- und 1.204 Browser-Tests sowie alle 14.911 Solution-Tests einschliesslich
23 neuer Regressionen. Die Formatpruefung meldet nur die drei auf `ad0bca6`
reproduzierten Baseline-Findings; die uebrigen geaenderten C#-Dateien bestehen
die separate Pruefung. Commit `137e779` ist gepusht; Linux, Windows und CodeQL
bestehen
([KP-090](./Kontextprotokoll.md#kp-090---quadratische-css-listenmarker)).

**Pass 9 (abgeschlossen):** Im umbruchfaehigen Text blockierten zuvor auch
gewoehnliche ASCII-Woerter wie `and/or`. CSS Text empfiehlt fuer Browser-
Interoperabilitaet keinen Umbruch zwischen Solidus und folgendem Buchstaben;
ein lokaler Chromium-Vergleich bestaetigt dies fuer untersuchte Beispiele.
Umgesetzt ist ausschliesslich ein ungebrochener Solidus zwischen ASCII-Buchstaben
eines Textknotens; Zahlen, fuehrende/abschliessende, doppelte, Unicode- und
knotenuebergreifende Faelle bleiben ausgeschlossen. Der Build, 171 Layout-,
29 Content-, 1.206 Browser- und alle 14.944 Solution-Tests bestehen,
darunter 33 neue Regressionen. Die Formatpruefung meldet nur drei auf
`ad0bca6` reproduzierte Baseline-Findings; die geaenderten Testdateien bestehen
separat. Commit `72f3d45` ist gepusht; Linux, Windows x64/ARM64 und CodeQL
bestehen
([KP-091](./Kontextprotokoll.md#kp-091---ungebrochener-ascii-solidus)).

**Pass 10 (abgeschlossen):** Als naechster einfacher CSS-Blocker wurde `:link`
bearbeitet. Der gemeinsame Matcher unterstuetzte bereits `:any-link`, lehnte
`:link` aber zuvor explizit ab. Da kein Besuchsstatus an Engine.Css uebermittelt
wird, erlaubt die lokale Selectors-Referenz aus Datenschutzgruenden, alle
Links als unbesucht zu behandeln. `:link` teilt die bestehende HTML-`a`-/
`area`-`href`-Pruefung; `:visited` bleibt ausdruecklich nicht unterstuetzt.
Fokussierte Tests einschliesslich Fragment-History und Lifecycle-Mutation
bestehen. Der Build, lokale V8-Smoke, alle 816 CSS-, 352 Scripting-,
30 Content-, 1.212 Browser- und 14.972 Solution-Tests bestehen; 30 neue
Faelle ersetzen zwei alte Unsupported-Faelle. Die Formatpruefung meldet nur
die drei auf `ad0bca6` reproduzierten Baseline-Findings; geaenderte C#-Dateien
bestehen separat. Commit `1215063` ist gepusht; Linux, Windows x64/ARM64 und
CodeQL bestehen
([KP-092](./Kontextprotokoll.md#kp-092---datenschutzfreundlicher-link-selektor)).

**Serie abgeschlossen:** Alle zehn angefragten Inkremente sind getrennt
validiert, committed und auf `origin/main` gepusht; Linux, Windows und CodeQL
bestehen fuer jeden Feature-Commit. Die zehn Commits sind `9f8aa41`, `e110697`,
`c9e5f59`, `d473814`, `9864bad`, `e803d75`, `ae43073`, `137e779`, `72f3d45`
und `1215063` (KP-083 bis KP-092). Dies schliesst nicht die breiteren, weiter
offenen Prioritaeten in [Missing.md](../plans/Missing.md) ab.

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
niedrigerer Prioritaet, nicht als dauerhaft ausgeschlossen. Parser-blockierende
klassische Inline- und externe Scripts sind nun opt-in unterstuetzt; andere
Scheduling-Arten und vollstaendige HTML-Vorbereitung bleiben offen (KP-333).

## Anforderungen und Spezifikationsstand

| ID      | Anforderung                                                                                                                            | Erkenntnisstatus                                                                                                                                         | Abstimmung                                                                                                                                                                                          | Quelle und Kontext                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                 |
| ------- | -------------------------------------------------------------------------------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| REQ-001 | Eigener C#-Browser im modularen Monorepo, minimaler Einstieg und langfristige Webstandards                                             | Nutzerangabe; durch Plan als Design dokumentiert                                                                                                         | beschlossen                                                                                                                                                                                         | SRC-001 Turn 2/3; [KP-001](./Kontextprotokoll.md#kp-001---urspruenglicher-auftrag-und-freigabe)                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                    |
| REQ-002 | Linux X11 und Wayland, Windows, x64 und arm64                                                                                          | Nutzerangabe/Freigabe; Ziel ist keine pauschale Desktop-Zertifizierung                                                                                   | beschlossen                                                                                                                                                                                         | SRC-001 Turn 3; SRC-026; [KP-001](./Kontextprotokoll.md#kp-001---urspruenglicher-auftrag-und-freigabe), [KP-003](./Kontextprotokoll.md#kp-003---plattformparitaet-und-native-nachweise)                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                            |
| REQ-003 | Tabs, mehrere Fenster und moeglichst starke Isolation                                                                                  | Nutzerangabe; Teilumsetzung laut Fachquellen                                                                                                             | beschlossen                                                                                                                                                                                         | SRC-001 Turn 2; [KP-002](./Kontextprotokoll.md#kp-002---phasenlieferung-und-sicherheitsgrundlage)                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                  |
| REQ-004 | Offizielle Standards referenzieren, lokal cachen, nach einem Monat auffrischen, Tests davon trennen; Dokumente fuer Agenten auffindbar | Nutzerangabe und Projektkonvention                                                                                                                       | beschlossen                                                                                                                                                                                         | SRC-001 Turn 3; SRC-025; [KP-001](./Kontextprotokoll.md#kp-001---urspruenglicher-auftrag-und-freigabe)                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                             |
| REQ-005 | Hoechste umsetzbare Missing-Prioritaet in kleinen getesteten und einzeln committeten Inkrementen bearbeiten                            | Nutzerangabe und vorhandener Workflow                                                                                                                    | beschlossen; zehn getrennte Passen angefordert; Pass 1-6 implementiert, lokal validiert, committed, gepusht und CI-gruen; Pass 7 lokal validiert, Commit/Push/CI ausstehend; drei Passen verbleiben | aktueller Nutzerturn; [KP-225](./Kontextprotokoll.md#kp-225---zehn-passen-lauf-caption-side-als-erster-tabellenpass), [KP-226](./Kontextprotokoll.md#kp-226---prozentbreiten-fuer-definitive-tabellen), [KP-227](./Kontextprotokoll.md#kp-227---zweiachsiger-tabellenabstand), [KP-228](./Kontextprotokoll.md#kp-228---html-cellspacing-hinweis), [KP-229](./Kontextprotokoll.md#kp-229---pass-5-auto-tabellen-prozentbreiten-umfang), [KP-230](./Kontextprotokoll.md#kp-230---pass-5-auto-tabellen-prozentbreiten-implementiert), [KP-231](./Kontextprotokoll.md#kp-231---pass-6-html-table-width-hint), [KP-232](./Kontextprotokoll.md#kp-232---pass-6-committed-gepusht-und-ci-gruen), [KP-314](./Kontextprotokoll.md#kp-314---html-select-recovery-pass-6), [KP-315](./Kontextprotokoll.md#kp-315---pass-6-gepusht-und-ci-gruen), [KP-316](./Kontextprotokoll.md#kp-316---html-option-optgroup-im-body-pass-7) |
| REQ-006 | Bei fehlender wesentlicher Entscheidung konkrete Optionen inklusive Blockieren anbieten und Antwort abwarten                           | Nutzerangabe; Workflow vorhanden                                                                                                                         | beschlossen                                                                                                                                                                                         | SRC-006; [KP-004](./Kontextprotokoll.md#kp-004---arbeitsablauf-pfade-und-commit-qualitaet)                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                         |
| REQ-007 | GitHub-Website handhaben koennen                                                                                                       | Nutzerangabe; im Tracker vorhanden, Funktion nicht nachgewiesen                                                                                          | beschlossen als Endziel; Abnahmekriterien offen                                                                                                                                                     | SRC-012; [KP-011](./Kontextprotokoll.md#kp-011---startskript-und-github-endziel)                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                   |
| REQ-008 | Neues Startskript fuer eine URL, ohne vorhandenen Source zu aendern                                                                    | Nutzerangabe; Skript vorhanden                                                                                                                           | beschlossen fuer damaligen Auftrag                                                                                                                                                                  | SRC-011; [KP-011](./Kontextprotokoll.md#kp-011---startskript-und-github-endziel)                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                   |
| REQ-009 | Kontext einschliesslich Sessions und GitHub rekonstruieren                                                                             | Nutzerangabe, aktueller Auftrag                                                                                                                          | beschlossen                                                                                                                                                                                         | SRC-033; [KP-015](./Kontextprotokoll.md#kp-015---rekonstruktion-und-abdeckung-dieses-checkpoints)                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                  |
| REQ-010 | Naechster Pass: vertikales verschachteltes Mausrad-Scrollen mit Edge-Chaining                                                          | Nutzerangabe, aus der aktuellen Gespraechszusammenfassung rekonstruiert                                                                                  | Ziel beschlossen und fuer den gewaehlten Scope umgesetzt (`overflow-x:hidden` + `overflow-y:auto`); breitere Overflow-Semantik bleibt offen                                                         | Zusammenfassung des aktuellen Gespraechs; [KP-031](./Kontextprotokoll.md#kp-031---verschachteltes-vertikales-scrollen), [KP-032](./Kontextprotokoll.md#kp-032---nested-wheel-scrollen-umsetzung-und-validierung)                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                   |
| REQ-012 | WHATWG URL vollstaendig implementieren                                                                                                 | Ausdrueckliche Nutzerangabe; der Nutzer genehmigte Core-Algorithmen plus browserseitige URL-/URLSearchParams-Semantik und interface-spezifisches Web IDL | Sechs-Phasen-Plan beschlossen; Umsetzung beginnt phasenweise und macht keine unbewiesene Vollkonformitaetsbehauptung                                                                                | Nutzerturns 2026-11-18; [Plan](../plans/whatwg-url-completion-plan.md), [Core guide](../docs/core.md#url), [standards inventory](../docs/standards.md#implemented-subsets), [KP-467](./Kontextprotokoll.md#kp-467---whatwg-url-vollstaendigkeitsauftrag-und-offene-grenze), [KP-468](./Kontextprotokoll.md#kp-468---url-planumfang-genehmigt)                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                      |

## Aktuelle Entscheidungen

| ID      | Entscheidung und Geltungsbereich                                                                                                                                                                                      | Status                                                                                                                                                                 | Ueberlieferte Begruendung                                                                                                                                                                                   | Quelle und Kontext                                                                                                                                                                                                                                                                                                                                                                        |
| ------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| DEC-001 | SDL3/SkiaSharp/HarfBuzzSharp, .NET 10, xUnit v3; V8 statt vorlaeufigem Jint/eigenem JS-Engine-Start                                                                                                                   | beschlossen                                                                                                                                                            | Nutzer: V8 planen, sonst Empfehlungen mit Aenderungen freigegeben                                                                                                                                           | SRC-001 Turn 3; SRC-025; [KP-001](./Kontextprotokoll.md)                                                                                                                                                                                                                                                                                                                                  |
| DEC-002 | First-party HTML-Parser; URL-Parser hinter eigener API vendort; Unicode 17 als stabile IDNA-Basis                                                                                                                     | beschlossen laut Roadmap; keine neue Freigabe hier                                                                                                                     | Roadmap berichtet Paket-WPT-Fehler als Anlass fuer URL-Vendoring; First-party-Parser als Nutzerwahl                                                                                                         | SRC-025; [KP-002](./Kontextprotokoll.md)                                                                                                                                                                                                                                                                                                                                                  |
| DEC-003 | CLI Confinement ohne Fallback; Entwicklung ohne Sandbox nur ausdruecklich                                                                                                                                             | beschlossen laut Roadmap, als Artefakt dokumentiert                                                                                                                    | Schutzgrenze nicht durch bequemere Startdefaults umgehen                                                                                                                                                    | SRC-025, Commit `bf35d4c`; [KP-002](./Kontextprotokoll.md#kp-002---phasenlieferung-und-sicherheitsgrundlage)                                                                                                                                                                                                                                                                              |
| DEC-004 | Vertrauenswuerdig aktivierte Cross-Origin-Top-Level-Navigation durch Browser-Broker zulassen; Renderer erhalten keine Netzwerkautoritaet                                                                              | beschlossen                                                                                                                                                            | Normale Navigation erhalten; Same-Origin-only wuerde normale Cross-Site-Links brechen                                                                                                                       | SRC-007/008/010; [KP-007](./Kontextprotokoll.md#kp-007---cross-origin-navigation-und-brokerentscheidung)                                                                                                                                                                                                                                                                                  |
| DEC-005 | Credentialless Cross-Origin-Stylesheets als begrenzter CORS-Pfad; Cookies aus, finale ACAO-Pruefung, Redirects innerhalb initialer effektiver Stylesheet-Origin                                                       | beschlossen; ersetzt reine Same-Origin-Stylesheet-Politik fuer diesen Pfad                                                                                             | Kompatibilitaet verbessern, ohne allgemeines Fetch/CORS zu behaupten                                                                                                                                        | SRC-013, Commit `b080f32`; [KP-009](./Kontextprotokoll.md#kp-009---cors-teilumfang-und-navigation-bestaetigung)                                                                                                                                                                                                                                                                           |
| DEC-006 | Bestaetigungsbasierte Cross-Origin-Link/Form-Navigation beibehalten; fruehere Ausnahme fuer Adressleisten-Navigation durch DEC-016/017 teilweise ersetzt                                                              | teilweise ersetzt durch DEC-016/017                                                                                                                                    | Nutzer waehlt bestehende Entwicklungspolitik statt strikter Sperre; spaetere Beschluesse ergaenzen begrenzte Adressleistenbestaetigungen                                                                    | SRC-014 Turn 2, SRC-015 Turn 2; [KP-010](./Kontextprotokoll.md#kp-010---bestaetigte-politik-und-uir-korrektur), [KP-013](./Kontextprotokoll.md#kp-013---adressleiste-und-medienlisten), [KP-034](./Kontextprotokoll.md#kp-034---cross-origin-bestaetigung-fuer-adressleisten-navigation), [KP-036](./Kontextprotokoll.md#kp-036---bestaetigung-erster-getippter-adressleisten-navigation) |
| DEC-007 | Idle V8/Listener nur fuer opted-in Dokumente und begrenzte Link-events behalten                                                                                                                                       | beschlossen; praezisiert alte Dispose-after-batch-Grenze                                                                                                               | Ohne Host-Verlaengerung koennen spaetere echte Link-Klicks keine Page-Listener erreichen                                                                                                                    | SRC-014 Turn 3/4; [KP-012](./Kontextprotokoll.md#kp-012---eng-begrenzte-verlaengerung-des-script-hosts)                                                                                                                                                                                                                                                                                   |
| DEC-008 | First-party MIT; Drittmaterial nicht pauschal umlizenzieren                                                                                                                                                           | als gemergte Repositoryentscheidung belegt; separate persoenliche Freigabe nicht rekonstruiert                                                                         | Notices sollen Herkunft und Lizenzluecken erhalten                                                                                                                                                          | SRC-029; [KP-006](./Kontextprotokoll.md#kp-006---windows-fehlerbehebung-und-lizenzinventar)                                                                                                                                                                                                                                                                                               |
| DEC-009 | Security-Prioritaet 1 fuer diesen Lauf ausdruecklich zurueckstellen und zu Tracker-Prioritaet 2 wechseln                                                                                                              | Nutzerbeschluss fuer den aktuellen Zehn-Passen-Auftrag                                                                                                                 | Begruendung nicht angegeben; keine Aenderung an der allgemeinen Sicherheitsbewertung oder DEC-004 bis DEC-007                                                                                               | aktueller Turn; [KP-016](./Kontextprotokoll.md#kp-016---zehn-implementierungspassen-und-sicherheitsentscheidung)                                                                                                                                                                                                                                                                          |
| DEC-010 | Erste Pass unter Prioritaet 2 korrigiert veraltete Browser-Shell-Dokumentation; kein neues Laufzeitverhalten                                                                                                          | ausdruecklich vom Nutzer ausgewaehlt                                                                                                                                   | Ableitung: begrenzte Korrektur ohne Laufzeit-Policyaenderung; kein persoenliches Motiv angegeben                                                                                                            | aktueller Turn; [KP-016](./Kontextprotokoll.md#kp-016---zehn-implementierungspassen-und-sicherheitsentscheidung)                                                                                                                                                                                                                                                                          |
| DEC-011 | Verbleibende Laufzeitluecken von Prioritaet 2 fuer diesen Lauf zurueckstellen und mit Prioritaet 3 fortfahren                                                                                                         | ausdruecklicher Nutzerbeschluss                                                                                                                                        | Begruendung nicht angegeben; Prioritaet 2 bleibt als unerledigte Luecke dokumentiert                                                                                                                        | aktueller Turn; [KP-017](./Kontextprotokoll.md#kp-017---prioritaet-2-zurueckstellen-und-prioritaet-3-waehlen)                                                                                                                                                                                                                                                                             |
| DEC-012 | Tabellenbaumkonstruktion als Pass-2-Ziel; umgesetzt sind begrenzte Abschnitts-/Zeilen-/Zellen- und Spaltengruppenregeln, keine vollstaendige Tabellenrecovery                                                         | Ziel vom Nutzer gewaehlt; konkrete Untermenge aus bestehender Fail-closed-Regel abgeleitet                                                                             | Foster parenting und nicht implementierte Algorithmen bleiben sichtbar unsupported                                                                                                                          | Gespraechszusammenfassung und [KP-018](./Kontextprotokoll.md#kp-018---tabellenbaumkonstruktion-pass-2), [KP-019](./Kontextprotokoll.md#kp-019---colgroup-und-col-pass-3)                                                                                                                                                                                                                  |
| DEC-013 | Page-initiated top-level `about:blank` erbt initiierende committed Origin; Browser-UI blank bleibt opaque; History bewahrt Auswahl                                                                                    | ausdruecklich vom Nutzer gewaehlt; implementiert fuer diesen begrenzten Fall                                                                                           | Frames, Sandbox und weitere inherited-origin contexts bleiben ausdruecklich ausserhalb des ersten Schritts                                                                                                  | aktueller Turn; [KP-028](./Kontextprotokoll.md#kp-028---begrenzte-aboutblank-origin-vererbung)                                                                                                                                                                                                                                                                                            |
| DEC-014 | Explizite Bestaetigung fuer Cross-Origin page navigation                                                                                                                                                              | Nutzer waehlte die Richtung; vorhandenes Verhalten verifiziert, kein neues Verhalten beschlossen                                                                       | Bestaetigung erlaubt oder verwirft den Request; bleibt Development-Shell-Policy, nicht Produktionsautorisierung                                                                                             | aktueller Turn; [KP-030](./Kontextprotokoll.md#kp-030---gewaehlte-bestaetigungspolitik-bereits-implementiert)                                                                                                                                                                                                                                                                             |
| DEC-015 | Restliche Security-Prioritaet fuer diesen Lauf zurueckstellen; als naechsten Pass vertikales verschachteltes Mausrad-Scrollen mit Edge-Chaining bearbeiten                                                            | Nutzerangabe, aus der aktuellen Gespraechszusammenfassung rekonstruiert                                                                                                | Die breitere Sicherheitsluecke bleibt offen; umgesetzt wurde der gewaehlte Scope `overflow-x:hidden` + `overflow-y:auto` auf definite-height Blocks                                                         | Zusammenfassung des aktuellen Gespraechs; [KP-031](./Kontextprotokoll.md#kp-031---verschachteltes-vertikales-scrollen), [KP-032](./Kontextprotokoll.md#kp-032---nested-wheel-scrollen-umsetzung-und-validierung)                                                                                                                                                                          |
| DEC-016 | Cross-Origin-Bestaetigung auf getippte HTTP(S)-Adressleisten-Navigation mit committed Dokument ausdehnen; origin-aendernde Redirects eingeschlossen                                                                   | ausdrueckliche Nutzerwahl; implementiert, gepusht und CI-gruen; Initial-Load-Ausnahme durch DEC-017 teilweise ersetzt                                                  | Bestehende origin-only Bestaetigung wiederverwenden; automatisch gestartete Loads und nicht-HTTP(S)-Adressleisten-Navigation unveraendert lassen; keine Produktionssicherheitsbehauptung                    | aktueller Nutzerturn; [KP-034](./Kontextprotokoll.md#kp-034---cross-origin-bestaetigung-fuer-adressleisten-navigation), [KP-035](./Kontextprotokoll.md#kp-035---adressleistenbestaetigung-committed-und-ci-gruen)                                                                                                                                                                         |
| DEC-017 | Jeden getippten HTTP(S)-Adressleisten-Request bestaetigen, wenn noch kein Dokument committed ist; akzeptierte erste Origin als Redirect-Baseline verwenden                                                            | ausdrueckliche Nutzerwahl; implementiert, gepusht und CI-gruen; automatische HTTP(S)-Shell-Loads durch DEC-018 teilweise ersetzt                                       | Bestaetigung gilt fuer typed address-bar requests ohne Source-Origin; keine Produktionssicherheitsbehauptung                                                                                                | aktueller Nutzerturn; [KP-036](./Kontextprotokoll.md#kp-036---bestaetigung-erster-getippter-adressleisten-navigation)                                                                                                                                                                                                                                                                     |
| DEC-018 | Automatisch gestartete HTTP(S)-Shell-Navigation vor dem ersten Request bestaetigen und origin-aendernde Redirects weiter einzeln bestaetigen                                                                          | ausdrueckliche Nutzerwahl; implementiert, getestet, gepusht und CI-gruen                                                                                               | Launch-URLs einschliessen; den eingebauten `data:`-New-Tab und direkte `BrowserController.Navigate`-Aufrufe nicht aendern; kein Grund fuer die Auswahl ueberliefert                                         | aktuelle Gespraechszusammenfassung und Codepruefung; [KP-037](./Kontextprotokoll.md#kp-037---bestaetigung-automatisch-gestarteter-http-navigation)                                                                                                                                                                                                                                        |
| DEC-019 | Shell-Back/Forward/Reload bestaetigen initiale HTTP(S)-History-Ziele, wenn sie Cross-Origin zum aktuellen Dokument sind; origin-aendernde Redirects einzeln bestaetigen und HTTPS-Downgrades blockieren               | ausdrueckliche Nutzerwahl; implementiert, vollvalidiert, gepusht und CI-gruen                                                                                          | Explizit ausgewaehltes Cross-Origin-History-Ziel braucht eigene Bestaetigung; Low-Level-Controller bleiben unveraendert                                                                                     | aktueller Nutzerturn; [KP-038](./Kontextprotokoll.md#kp-038---bestaetigung-bei-browser-history-navigation)                                                                                                                                                                                                                                                                                |
| DEC-020 | DevelopmentShell bestaetigt explizite Top-Level-`file:`-Loads aus Adressleiste, `OpenWindow` und Datei-History vor Dateizugriff; der Prompt zeigt den kanonischen URL-Pfad ohne Query/Fragment                        | ausdrueckliche Nutzerwahl; lokal vollvalidiert, Commit `d38b902` gepusht und CI-gruen                                                                                  | Lokale Tests bleiben nach Bestaetigung moeglich; page-initiated Dateiaktionen bleiben gesperrt, Low-Level-Aufrufe unveraendert, keine Dateisystem-Isolation                                                 | aktueller Nutzerturn; [KP-040](./Kontextprotokoll.md#kp-040---bestaetigung-expliziter-file-navigation)                                                                                                                                                                                                                                                                                    |
| DEC-021 | DevelopmentShell bestaetigt explizite Top-Level-`data:`-Navigation aus Adressleiste, `OpenWindow` und cross-document History; der Prompt zeigt keinen Payload                                                         | ausdrueckliche Nutzerwahl; implementiert, lokal vollvalidiert, gepusht und CI-gruen                                                                                    | Eingebauten `data:`-Welcome und direkte Low-Level-Controller-Aufrufe unveraendert lassen; keine Produktionsautorisierung behaupten                                                                          | aktuelle Nutzerwahl und Implementierungsnachweis; [KP-041](./Kontextprotokoll.md#kp-041---explizite-data-navigation-policy), [KP-042](./Kontextprotokoll.md#kp-042---data-navigation-implementierung-und-ci)                                                                                                                                                                              |
| DEC-022 | Bestaetigte Top-Level-`file:`-Navigation behaelt Zugriff auf beliebige Dateipfade; keine Beschraenkung auf erlaubte Wurzeln und kein Blockieren                                                                       | ausdrueckliche Nutzerwahl; aktuelle Policy bestaetigt, keine neue Implementierung                                                                                      | Lokale Entwicklungsflexibilitaet beibehalten; nicht als Dateisystem-Isolation darstellen                                                                                                                    | aktuelle Nutzerwahl; [KP-044](./Kontextprotokoll.md#kp-044---dateizugriffsumfang-bestaetigt)                                                                                                                                                                                                                                                                                              |
| DEC-023 | Gleich-originige page-initiierte HTTP(S)-Navigationen bleiben ohne initialen Prompt; nur Cross-Origin-Ziele und origin-aendernde Redirects werden bestaetigt                                                          | ausdrueckliche Nutzerwahl; bestehende Policy bestaetigt, keine neue Implementierung                                                                                    | Adressleisten- und Low-Level-Verhalten nicht aendern; ein anderes prioritaet-1-Inkrement auswaehlen                                                                                                         | aktuelle Nutzerwahl; [KP-045](./Kontextprotokoll.md#kp-045---vorschlag-zur-gleich-originigen-page-navigation), [KP-046](./Kontextprotokoll.md#kp-046---cross-origin-only-policy-bestaetigt)                                                                                                                                                                                               |
| DEC-024 | Verbleibende allgemeine Sicherheitsluecken aus Prioritaet 1 fuer diesen Zehn-Passen-Lauf zurueckstellen und mit Prioritaet 2 fortfahren                                                                               | ausdrueckliche Nutzerwahl; Pass 7 soll aus Prioritaet 2 gewaehlt werden                                                                                                | Security-Luecken bleiben offen; nicht als Sicherheitsfreigabe oder Abschluss der Prioritaet 1 werten                                                                                                        | aktuelle Nutzerwahl; [KP-047](./Kontextprotokoll.md#kp-047---keine-weitere-fertige-prioritaet-1-abgrenzung-gefunden), [KP-048](./Kontextprotokoll.md#kp-048---sicherheitsprioritaet-zurueckgestellt)                                                                                                                                                                                      |
| DEC-025 | Pass 8 soll bounded horizontal document-level scrolling behandeln, ohne nested overflow behavior                                                                                                                      | ausdrueckliche Nutzerwahl; umgesetzt und lokal validiert                                                                                                               | Den Umfang nicht zu nested CSS overflow ausweiten; Shell-Eingaben, Scrollbar und History-Verhalten folgen bestehenden Konventionen                                                                          | aktuelle Nutzerantwort und Implementierungsbeleg; [KP-051](./Kontextprotokoll.md#kp-051---horizontales-dokument-scrollen-gewaehlt), [KP-052](./Kontextprotokoll.md#kp-052---horizontales-scrollen-implementiert-und-validiert)                                                                                                                                                            |
| DEC-026 | Pass 9 benachrichtigt opted-in Seiten bei allen bestehenden In-Page-Zielfokuswechseln auf Links und sichtbaren aktivierten Formcontrols, einschliesslich Tab/Shift+Tab, Pointer und Page/Chrome-Uebergaengen          | ausdrueckliche Nutzerwahl; implementiert, lokal vollvalidiert, Commit `0160dc0` gepusht und CI-gruen                                                                   | Keine OS-Fensterfokusereignisse, neuen Fokusziele, Tastatur-/Pointerereignisse oder zusaetzlichen Browser-Standardaktionen einfuehren                                                                       | aktuelle Nutzerwahl aus der ueberlieferten Gespraechszusammenfassung und lokale Tests; [KP-053](./Kontextprotokoll.md#kp-053---pass-9-eventumfang-zur-auswahl), [KP-054](./Kontextprotokoll.md#kp-054---fokusereignisse-fuer-pass-9-gewaehlt), [KP-056](./Kontextprotokoll.md#kp-056---bestehende-fokusuebergaenge-fuer-pass-9-bestaetigt), KP-057                                        |
| DEC-027 | Gewaehlter Pass-10-Vorschlag: dispatch begrenzte `pointerout`/`pointerover` PointerEvents bei Hover-Wechseln auf bestehenden gerenderten Links und sichtbaren aktivierten Controls                                    | ausdrueckliche Nutzerwahl, aber nicht umgesetzt: widerspricht der aktuell dokumentierten Begrenzung ohne automatische Browser-Input-Events                             | Vor weiterem Pass-10-Code scope-kompatible Alternative waehlen; Vorschlagsverlauf und Konflikt siehe KP-061                                                                                                 | aktuelle Nutzerantwort; [KP-059](./Kontextprotokoll.md#kp-059---pass-10-pointer-hover-bereich-gewaehlt), [KP-060](./Kontextprotokoll.md#kp-060---pass-10-pointerevent-vertrag-bestaetigt), [scripting.md](../docs/scripting.md#bounded-synchronous-synthetic-events-phase-11g)                                                                                                            |
| DEC-028 | Pass 5 unterstuetzt eine einzelne HTML-Tabellencaption als `display:table-caption` ueber der kanonischen Tabelle; Wrapperbreite entspricht der Border-Edge der Tabelle; kein `caption-side` und keine Bottom-Position | Historische Nutzerwahl fuer Pass 5; implementiert in `b0d0441`; spaetere Erweiterung durch DEC-029                                                                     | Der top-only-Umfang galt fuer Pass 5; die nachfolgende Backlog-Arbeit wird dadurch nicht ausgeschlossen                                                                                                     | Nutzerwahl zu Q-024 in aktueller Sitzung; [KP-207](./Kontextprotokoll.md#kp-207---pass-5-top-only-caption-beschlossen), CSS 2.2 `#model`/`#caption-position`; HTML `#the-caption-element`                                                                                                                                                                                                 |
| DEC-029 | Ein spaeterer Tabellenpass erweitert die bestehende erste HTML-Caption um CSS-2.2-`caption-side:top/bottom`; initial/inherited top bleibt, bottom folgt dem Tabellenbox-Wrapper und aendert keine Intrinsic-Spalten   | Aus dem neuen Nutzerauftrag und der Prioritaetenfolge abgeleitet; lokal implementiert und validiert, Commit/CI ausstehend                                              | DEC-028 war auf Pass 5 begrenzt; der aktuelle Tracker fuehrt Bottom-Captions weiter als offene Tabellenluecke, und CSS 2.2 legt `top`/`bottom` eindeutig fest                                               | Nutzerauftrag 2026-11-07, 14:18 Europe/Berlin; [Missing.md](../plans/Missing.md#5-general-css-layout-and-painting), CSS 2.2 `#caption-position` (Cache geprueft 2026-10-28); [KP-225](./Kontextprotokoll.md#kp-225---zehn-passen-lauf-caption-side-als-erster-tabellenpass)                                                                                                               |
| DEC-030 | Auto-width tables solve `col`/`colgroup` percentage minimum constraints against the final table width using bounded deterministic bisection; fail explicitly if no finite width satisfies them                        | Ausdrueckliche Nutzerwahl Q-028; implementiert, voll lokal validiert, Commit `de132ad` gepusht und Linux-/Windows-/CodeQL-CI gruen                                     | Share evenly any group percentage deficit; charge solver scans/iterations to shared table-column operation/cancellation budgets; no approximate fallback                                                    | Nutzerwahl 2026-11-07, 17:15 Europe/Berlin; CSS 2.2 `#auto-table-layout`; [KP-229](./Kontextprotokoll.md#kp-229---pass-5-auto-tabellen-prozentbreiten-umfang), [KP-230](./Kontextprotokoll.md#kp-230---pass-5-auto-tabellen-prozentbreiten-implementiert)                                                                                                                                 |
| DEC-031 | HTML `table[width]` maps nonzero dimension values to pixel or percentage CSS width hints; percentages resolve against the containing block, and zero/invalid inputs add no hint                                       | Aus REQ-005, Missing-Tracker und frisch gepruefter HTML-Regel abgeleitet; implementiert, lokal validiert, Commit `fb73991` gepusht und Linux-/Windows-/CodeQL-CI gruen | Preserve the HTML dimension parser; bound aggregate raw attributes; fail explicitly outside the finite numeric range; keep width attributes on other table parts unsupported                                | [KP-231](./Kontextprotokoll.md#kp-231---pass-6-html-table-width-hint), [KP-232](./Kontextprotokoll.md#kp-232---pass-6-committed-gepusht-und-ci-gruen), WHATWG HTML `#tables-2` and `#rules-for-parsing-dimension-values` (cache refreshed 2026-11-07)                                                                                                                                     |
| DEC-032 | Valid `valign` on `tr`/row groups supplies a default for direct child table cells; the row wins over its group, and cell CSS or a cell `valign` hint overrides it                                                     | Ausdrueckliche Nutzerwahl `cell-default`; implementiert, lokal validiert, `dd42bec` gepusht und Linux-/Windows-/CodeQL-CI gruen                                        | This is bounded HTML compatibility behavior, not general CSS inheritance; unsupported values/targets still fail explicitly                                                                                  | [KP-238](./Kontextprotokoll.md#kp-238---naechster-tabellenpass-row-group-valign-offen), [KP-239](./Kontextprotokoll.md#kp-239---row-group-valign-als-zell-default-gewaehlt), [KP-240](./Kontextprotokoll.md#kp-240---pass-9-implementiert-und-lokal-vollvalidiert), [KP-241](./Kontextprotokoll.md#kp-241---pass-9-gepusht-und-ci-gruen)                                                  |
| DEC-033 | At each next standards-driven feature selection, rank only partially implemented WHATWG standards by the highest current coverage estimate; treat ties equally and re-evaluate from `standards.md`                    | Durch Nutzerangabe vom 2026-11-15 auf WHATWG-Standards begrenzt; der bereits begonnene CSS-Color-Pass 1 wird nicht rueckwirkend geaendert                              | Schaetzungen sind nur eine grobe Rangfolgeheuristik; waehle tatsaechliche offene Tracker-Luecken, keine Konformitaet oder Abschluss aus dem Prozentwert ableiten                                            | Aktueller Nutzerturn; [Missing.md](../plans/Missing.md#recommended-priority), [standards.md](../docs/standards.md#support-status), [KP-324](./Kontextprotokoll.md#kp-324---standardsprioritaet-nach-hoechster-teilabdeckung), [KP-382](./Kontextprotokoll.md#kp-382---whatwg-standardsrangfolge)                                                                                          |
| DEC-034 | Implement parser-blocking execution for external classic scripts with CSP-governed HTTP(S) loading, including cross-origin cases                                                                                      | Nutzerwahl umgesetzt, committed, gepusht und durch Linux/Windows/CodeQL-CI bestaetigt                                                                                  | Browser-owned loading only; preserve opt-in script policy, CSP, request/resource limits, cancellation and transactional publication; modules, async/defer and persistent event-loop support remain deferred | [KP-326](./Kontextprotokoll.md#kp-326---parser-blockierende-externe-klassiker-gewaehlt), [KP-333](./Kontextprotokoll.md#kp-333---parser-blockierende-klassiker-implementiert-und-validiert), [KP-334](./Kontextprotokoll.md#kp-334---commit-push-und-ci-bestaetigt)                                                                                                                       |
| DEC-035 | Vollstaendige WHATWG-URL-Arbeit umfasst Core-Algorithmen und browserseitige `URL`/`URLSearchParams`-Semantik mit interface-spezifischem Web IDL; allgemeine Web-IDL-Generierung bleibt ausserhalb des Scopes          | Vom Nutzer am 2026-11-18 genehmigt; Umsetzung phasenweise nach dem genehmigten URL-Plan                                                                                | Beibehalten der unveraenderlichen `BrowserUrl`-Fassade; mutable URL/SearchParams-Linkage und Script-Bindings als getrennte, testbare Erweiterungen; Konformitaet nur in belegtem Umfang behaupten           | Nutzerbestaetigung; [Plan](../plans/whatwg-url-completion-plan.md), [KP-468](./Kontextprotokoll.md#kp-468---url-planumfang-genehmigt)                                                                                                                                                                                                                                                     |

## Offene Fragen, Widersprueche und Risiken

| ID    | Frage oder konkurrierende Aussagen                                                                                                                                                                                                        | Auswirkung                                                                                                                                                                                                    | Naechste Klaerung                                                                                                                                                                                                                     | Quellen und Kontext                                                                                                                                                                                                                                                                                                                                                                                                                |
| ----- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Q-001 | Was bedeutet GitHub handhaben: statische Seiten, Anmeldung, Navigation, Suche, Issues/PRs, Editor?                                                                                                                                        | Kein messbarer Ziel-Abnahmetest; keine heutige Kompatibilitaetsbehauptung moeglich                                                                                                                            | Vor einem GitHub-Meilenstein Nutzerkriterien und sichere Testumgebung festlegen                                                                                                                                                       | REQ-007; [KP-011](./Kontextprotokoll.md#kp-011---startskript-und-github-endziel)                                                                                                                                                                                                                                                                                                                                                   |
| Q-002 | Vollstaendige Produktionsautorisierung, SOP/CORS/CSP, Frames/Origins, Storage und feindliche Inhalte bleiben offen; begrenztes `about:blank`-Erben ist umgesetzt und Cross-Origin-Bestaetigung existiert nur als Development-Shell-Policy | Bestehende Bestaetigung ist keine Produktionsautorisierung und loest die Gesamtfrage nicht                                                                                                                    | Vor neuem Codeumfang eine noch fehlende Sicherheitsregel oder konkreten niedrigeren Tracker-Punkt auswaehlen; Blockieren bleibt Option                                                                                                | [Missing.md](../plans/Missing.md); [KP-007](./Kontextprotokoll.md#kp-007---cross-origin-navigation-und-brokerentscheidung), [KP-028](./Kontextprotokoll.md#kp-028---begrenzte-aboutblank-origin-vererbung), [KP-030](./Kontextprotokoll.md#kp-030---gewaehlte-bestaetigungspolitik-bereits-implementiert)                                                                                                                          |
| Q-003 | README meldet fehlende erfolgreiche Linux-ARM64-Evidenz; Tracker nennt bereits erfolgreiche native Laeufe                                                                                                                                 | Veraltete negative Aussage kann Planung erneut blockieren                                                                                                                                                     | Historische README-Aussage nicht als aktuellen Beweis verwenden; gezielt Dokumentabgleich in eigenem Auftrag                                                                                                                          | SRC-025/030/032; [KP-014](./Kontextprotokoll.md#kp-014---aktuelle-artefakte-ci-und-dokumentwidersprueche)                                                                                                                                                                                                                                                                                                                          |
| Q-004 | Architektur/Index und einzelne historische Scripting-Abschnitte beschreiben Dispose vor spaeterem Input; heutiger Link-Dispatch behaelt den Host                                                                                          | Veraltete Grenze kann bereits freigegebene Arbeit erneut blockieren                                                                                                                                           | DEC-007 und aktuellen spezialisierten Abschnitt/Code verwenden, nicht allgemeinen Event Loop daraus ableiten                                                                                                                          | SRC-025/031; [KP-012](./Kontextprotokoll.md#kp-012---eng-begrenzte-verlaengerung-des-script-hosts)                                                                                                                                                                                                                                                                                                                                 |
| Q-005 | Vorheriger Export behauptet Kontextdateien angelegt; dieser Turn fand Kontext leer vor                                                                                                                                                    | Fruher behauptete Persistenz ist nicht belegt; Verlustursache unbekannt                                                                                                                                       | Diese Dateien als erste hier verifizierte Rekonstruktion behandeln; keine fruehere Historie erfinden                                                                                                                                  | SRC-016; [KP-014](./Kontextprotokoll.md#kp-014---aktuelle-artefakte-ci-und-dokumentwidersprueche)                                                                                                                                                                                                                                                                                                                                  |
| Q-006 | Wiederholt zehn Passen angefordert, aber Exporte enthalten Unterbrechungen, mehrere Sessions und mehr als einen Commit pro Pass                                                                                                           | Kein belastbarer globaler Restzaehler                                                                                                                                                                         | Nicht aus Sessiontiteln ableiten, dass zehn fertig sind oder jetzt automatisch fortzusetzen waeren                                                                                                                                    | SRC-003/004/014/015; [KP-004](./Kontextprotokoll.md#kp-004---arbeitsablauf-pfade-und-commit-qualitaet)                                                                                                                                                                                                                                                                                                                             |
| Q-007 | Alte Exporte melden pre-existing Formatterfehler; neuester Commit-Turn lief ohne Tests                                                                                                                                                    | Alte/gruene Aussagen nicht ungeprueft auf HEAD uebertragen                                                                                                                                                    | Bei naechster Codeaenderung aktuelle vorgeschriebene Checks ausfuehren                                                                                                                                                                | SRC-014/015/016; [KP-014](./Kontextprotokoll.md#kp-014---aktuelle-artefakte-ci-und-dokumentwidersprueche)                                                                                                                                                                                                                                                                                                                          |
| Q-008 | Paket-/Native-/Cache-Lizenzen und Distribution sind nicht mit First-party-MIT erledigt                                                                                                                                                    | Auslieferung braucht eigene Notice-/Lizenzpruefung                                                                                                                                                            | [Notices](../THIRD_PARTY_NOTICES.md) und Produktions-Tracker bearbeiten                                                                                                                                                               | SRC-029; [KP-006](./Kontextprotokoll.md#kp-006---windows-fehlerbehebung-und-lizenzinventar)                                                                                                                                                                                                                                                                                                                                        |
| Q-009 | Prioritaet 2 enthaelt verbleibende Interaktion wie Downloads, persistente Sitzungen, vollstaendige History und allgemeine Page-Input-Unterstuetzung; die genaue Scope-Grenze bleibt fuer diese groesseren Themen offen                    | Eine Umsetzung koennte Speicher-, Eingabe- oder Dateizugriffsverhalten ohne spezifizierte Grenzen waehlen                                                                                                     | Der begrenzte Pass 7 behandelt `tabindex` nur fuer bestehende Link-/Form-Fokusziele; vor spaeteren groesseren Themen konkrete Grenzen festlegen                                                                                       | [Missing.md](../plans/Missing.md#2-basic-interaction-with-pages); [browser-shell.md](../docs/browser-shell.md#limits-and-trust-boundary); [KP-049](./Kontextprotokoll.md#kp-049---begrenzte-tabindex-fokusreihenfolge)                                                                                                                                                                                                             |
| Q-010 | Prioritaet 3 umfasst HTML-Recovery, Layout, Schrift und CSS; der Nutzer waehlte Tabellenbaumkonstruktion als ersten Ausschnitt                                                                                                            | Weitere kleine Parser-Inkremente muessen gegen Tracker, Code und Standardsquelle abgegrenzt werden                                                                                                            | Erster Ausschnitt geklaert; passweise Auswahl der naechsten testbaren Luecke, ohne daraus eine umfassende Konformitaetsfreigabe abzuleiten                                                                                            | [Missing.md](../plans/Missing.md#recommended-priority); [KP-017](./Kontextprotokoll.md#kp-017---prioritaet-2-zurueckstellen-und-prioritaet-3-waehlen), [KP-024](./Kontextprotokoll.md#kp-024---verschachtelte-tabellenpass-8)                                                                                                                                                                                                      |
| Q-011 | Welche CSS-Syntax soll den vertikalen Scrollcontainer erzeugen: nur `overflow-x:hidden` + `overflow-y:auto`, oder auch passende `overflow`-Shorthand-Faelle?                                                                              | Fuer diesen Pass beantwortet: nur das Longhand-Paar (`paired_longhands`) ist umgesetzt; Shorthand-Faelle bleiben offen                                                                                        | Beantwortet durch Nutzerwahl vor Umsetzung; fuer spaetere Erweiterung erneut klaeren                                                                                                                                                  | CSS Overflow `#overflow-control`; [KP-031](./Kontextprotokoll.md#kp-031---verschachteltes-vertikales-scrollen), [KP-032](./Kontextprotokoll.md#kp-032---nested-wheel-scrollen-umsetzung-und-validierung)                                                                                                                                                                                                                           |
| Q-012 | Soll die DevelopmentShell browsergesteuerte Top-Level-`file:`-Navigation bestaetigen, blockieren oder weiter nur als explizite Nutzeraktion zulassen?                                                                                     | Entschieden: bestaetigen; implementiert und validiert                                                                                                                                                         | Die Nutzerwahl umfasst Adressleiste, `OpenWindow(file:)` und Datei-History; der Dialog zeigt den Pfad ohne Query/Fragment                                                                                                             | [DEC-020](#aktuelle-entscheidungen), [KP-039](./Kontextprotokoll.md#kp-039---top-level-file-navigation-policy), [KP-040](./Kontextprotokoll.md#kp-040---bestaetigung-expliziter-file-navigation)                                                                                                                                                                                                                                   |
| Q-013 | Soll die DevelopmentShell explizite Top-Level-`data:`-Navigation aus Adressleiste, `OpenWindow` und cross-document History bestaetigen, blockieren oder unveraendert lassen?                                                              | Entschieden: bestaetigen; implementiert, validiert, gepusht und CI-gruen                                                                                                                                      | Prompt zeigt nur die `data:`-Navigation, nicht ihren Payload; eingebauten Welcome und direkte Low-Level-Controller-Aufrufe unveraendert lassen                                                                                        | [DEC-021](#aktuelle-entscheidungen), [KP-041](./Kontextprotokoll.md#kp-041---explizite-data-navigation-policy), [KP-042](./Kontextprotokoll.md#kp-042---data-navigation-implementierung-und-ci)                                                                                                                                                                                                                                    |
| Q-014 | Soll bestaetigte Top-Level-`file:`-Navigation beliebige Dateipfade zulassen, auf explizit freigegebene Wurzeln beschraenkt oder ganz deaktiviert werden?                                                                                  | Entschieden: beliebige Dateipfade bleiben nach ausdruecklicher Bestaetigung zulaessig; dies ist keine Dateisystem-Isolation                                                                                   | Keine Aenderung fuer Q-014; verbleibende Security-Luecken separat aus Prioritaet 1 auswaehlen                                                                                                                                         | DEC-022; [KP-043](./Kontextprotokoll.md#kp-043---vorschlag-zur-begrenzung-expliziter-datei-navigation), [KP-044](./Kontextprotokoll.md#kp-044---dateizugriffsumfang-bestaetigt)                                                                                                                                                                                                                                                    |
| Q-015 | Soll die DevelopmentShell auch gleich-originige, page-initiierte HTTP(S)-Top-Level-Navigationen vor dem Request bestaetigen?                                                                                                              | Entschieden: bestehendes Cross-Origin-only-Verhalten bleibt; ein anderes Inkrement aus Prioritaet 1 wird gesucht                                                                                              | Keine Aenderung dieser Policy; bei der Auswahl weiterer Ziele dieselbe Bestaetigung nicht erneut erweitern                                                                                                                            | DEC-023; [KP-045](./Kontextprotokoll.md#kp-045---vorschlag-zur-gleich-originigen-page-navigation), [KP-046](./Kontextprotokoll.md#kp-046---cross-origin-only-policy-bestaetigt)                                                                                                                                                                                                                                                    |
| Q-016 | Welches konkrete Inkrement soll die verbleibenden breiten Produktions-Sicherheitsluecken aus Prioritaet 1 als Naechstes adressieren?                                                                                                      | Entschieden: fuer diesen Lauf zurueckgestellt; Prioritaet 2 ist freigegeben, Sicherheitsluecken bleiben offen                                                                                                 | Keine Vollstaendigkeits- oder Sicherheitsfreigabe ableiten; Prioritaet 2 nach Tracker-Reihenfolge bearbeiten                                                                                                                          | DEC-024; [KP-047](./Kontextprotokoll.md#kp-047---keine-weitere-fertige-prioritaet-1-abgrenzung-gefunden), [KP-048](./Kontextprotokoll.md#kp-048---sicherheitsprioritaet-zurueckgestellt)                                                                                                                                                                                                                                           |
| Q-017 | Soll die bounded `tabindex`-Unterstuetzung spaeter auch sonstige, aktuell nicht interaktive Renderziele focusierbar machen?                                                                                                               | Offen; Pass 7 beschraenkt sich auf existierende Links und sichtbare aktivierte Formcontrols                                                                                                                   | Vor breiterer DOM-Fokusarbeit Zielmetadaten, Fokusdarstellung und Interaktionsumfang konkret abgrenzen                                                                                                                                | [Missing.md](../plans/Missing.md#2-basic-interaction-with-pages); [browser-shell.md](../docs/browser-shell.md#textual-link-activation); [KP-049](./Kontextprotokoll.md#kp-049---begrenzte-tabindex-fokusreihenfolge)                                                                                                                                                                                                               |
| Q-018 | Welcher konkrete Prioritaet-2-Inkrement soll nach Pass 7 folgen?                                                                                                                                                                          | Beantwortet und umgesetzt: bounded horizontal document-level scrolling ohne nested overflow behavior                                                                                                          | Naechsten unabhaengigen Tracker-Punkt fuer Pass 9 bestimmen; bei wesentlicher Verhaltenswahl erneut klaeren                                                                                                                           | ausdrueckliche Nutzerwahl und lokale Implementierungsbelege; DEC-025; [KP-051](./Kontextprotokoll.md#kp-051---horizontales-dokument-scrollen-gewaehlt), [KP-052](./Kontextprotokoll.md#kp-052---horizontales-scrollen-implementiert-und-validiert)                                                                                                                                                                                 |
| Q-019 | Welches begrenzte page-input/event-Inkrement soll Pass 9 behandeln?                                                                                                                                                                       | Beantwortet und in Pass 9 implementiert: Fokusereignisse auf bestehenden Shell-Fokuszielen                                                                                                                    | Keine allgemeinen Inputereignisse ergaenzen                                                                                                                                                                                           | Nutzerwahl und lokale Implementierungs-/Testbelege; DEC-026; [KP-053](./Kontextprotokoll.md#kp-053---pass-9-eventumfang-zur-auswahl), [KP-054](./Kontextprotokoll.md#kp-054---fokusereignisse-fuer-pass-9-gewaehlt), KP-057                                                                                                                                                                                                        |
| Q-020 | Welche bestehenden Shell-Fokuswechsel sollen die neuen Fokusereignisse ausloesen?                                                                                                                                                         | Beantwortet und implementiert: alle bestehenden In-Page-Zielfokuswechsel einschliesslich Tab/Shift+Tab, Pointerfokus und Page/Chrome-Uebergaengen; OS-Fensterfokus bleibt ausgeschlossen                      | Nach CI-Gruen den naechsten konkreten Missing-Prioritaetspunkt fuer Pass 10 bestimmen; keine neuen Fokusziele hinzufuegen                                                                                                             | Nutzerwahl aus der ueberlieferten Gespraechszusammenfassung und lokale Implementierungs-/Testbelege; DEC-026; [KP-055](./Kontextprotokoll.md#kp-055---fokusereignis-uebergangsumfang-offen), [KP-056](./Kontextprotokoll.md#kp-056---bestehende-fokusuebergaenge-fuer-pass-9-bestaetigt), KP-057                                                                                                                                   |
| Q-021 | Welches konkrete, nicht bereits umgesetzte Prioritaet-2-Inkrement soll Pass 10 behandeln?                                                                                                                                                 | Beantwortet: begrenzte Pointer-Hover-Benachrichtigungen auf vorhandenen Seitenzielen                                                                                                                          | Keine anderen Page-Interaction-Punkte gleichzeitig aufnehmen                                                                                                                                                                          | aktuelle Nutzerantwort; DEC-027; [KP-058](./Kontextprotokoll.md#kp-058---pass-10-umfang-noch-offen), [KP-059](./Kontextprotokoll.md#kp-059---pass-10-pointer-hover-bereich-gewaehlt), [KP-060](./Kontextprotokoll.md#kp-060---pass-10-pointerevent-vertrag-bestaetigt)                                                                                                                                                             |
| Q-022 | Welche Eventnamen/Propagation soll Pass 10 fuer Hover-Wechsel auf vorhandenen Seitenzielen liefern?                                                                                                                                       | Nutzerwahl: bubbling `pointerout`/`pointerover` als PointerEvents mit begrenzter pointerId/type- und Koordinatenmetadaten; wegen des aktuellen Verbots automatischer Browser-Input-Events nicht implementiert | Scope-kompatible Alternative waehlen; die ausgewaehlte automatische Dispatch-Semantik nicht in Code umsetzen                                                                                                                          | aktuelle Nutzerantwort und [scripting.md](../docs/scripting.md#bounded-synchronous-synthetic-events-phase-11g); [KP-060](./Kontextprotokoll.md#kp-060---pass-10-pointerevent-vertrag-bestaetigt), [KP-061](./Kontextprotokoll.md#kp-061---pointer-hover-wahl-blockiert-durch-eventgrenze)                                                                                                                                          |
| Q-023 | Welches mit den aktuellen Eventgrenzen kompatible Inkrement soll Pass 10 stattdessen umsetzen?                                                                                                                                            | Nutzer bat um Stopp; kein alternatives Inkrement gewaehlt                                                                                                                                                     | Keine weitere Arbeit an Pass 10 ohne neue ausdrueckliche Nutzeranweisung                                                                                                                                                              | aktuelle Nutzerantwort; [KP-062](./Kontextprotokoll.md#kp-062---weitere-arbeit-gestoppt)                                                                                                                                                                                                                                                                                                                                           |
| Q-024 | Soll Pass 5 eine HTML-`table-caption` nur oben oder zusaetzlich oben/unten mit `caption-side` unterstuetzen?                                                                                                                              | Fuer Pass 5 beantwortet und umgesetzt: Nutzer waehlte top-only; spaetere Bottom-Erweiterung ist separat in DEC-029 dokumentiert                                                                               | Keine offene Wahl fuer Pass 5; die spaetere Tabellenluecke ist ein eigener Backlog-Pass und keine Aenderung der damaligen Nutzerwahl                                                                                                  | ausdrueckliche Nutzerwahl in aktueller Sitzung; DEC-028/029; [KP-207](./Kontextprotokoll.md#kp-207---pass-5-top-only-caption-beschlossen), [KP-225](./Kontextprotokoll.md#kp-225---zehn-passen-lauf-caption-side-als-erster-tabellenpass); CSS 2.2 `#caption-position`                                                                                                                                                             |
| Q-021 | Welches konkrete, nicht bereits umgesetzte Prioritaet-2-Inkrement soll Pass 10 behandeln?                                                                                                                                                 | Offen; die verbleibende Page-Interaction-Luecke ist breit, und der aktuelle Nutzerauftrag legt keinen Einzelumfang fest                                                                                       | Nutzer zwischen begrenzten Pointer-Hover-Events auf vorhandenen Zielen, erweiterten `tabindex`-Fokuszielen, begrenzten DOM-Scroll-Events oder Blockieren weiterer Arbeit waehlen lassen                                               | [Missing.md](../plans/Missing.md#2-basic-interaction-with-pages), [browser-shell.md](../docs/browser-shell.md), [KP-058](./Kontextprotokoll.md#kp-058---pass-10-umfang-noch-offen)                                                                                                                                                                                                                                                 |
| Q-026 | Welchen vertikalen Tabellenzell-Ausrichtungsumfang soll Pass 8 aus Prioritaet 1 umsetzen?                                                                                                                                                 | Beantwortet und umgesetzt: Nutzer waehlt CSS `middle`/`bottom` einschliesslich Row-Spans; CSS `baseline` und HTML-`valign` blieben ausserhalb von Pass 8                                                      | `vertical-align:middle/bottom` im kanonischen Tabellenlayout inkl. mehrzeiliger Spans implementieren; keine Legacy-`valign`-Hints oder Baseline-Algorithmik in Pass 8 ausweiten                                                       | ausdrueckliche Nutzerwahl am 2026-11-07; [Missing.md](../plans/Missing.md#recommended-priority), [text-layout.md](../docs/text-layout.md#basic-automatic-tables), [KP-217](./Kontextprotokoll.md#kp-217---pass-8-vertical-cell-alignment-scope-decided)                                                                                                                                                                            |
| Q-027 | Soll der zehnte Inkrement-Pass Tabellenzellen-Baselineausrichtung nur fuer nicht-spannende Zellen oder auch fuer Row-Spans umsetzen?                                                                                                      | Beantwortet und umgesetzt: Nutzer waehlte Option 2, Baselineausrichtung einschliesslich Row-Spans; `348ad31` ist gepusht und CI-gruen                                                                         | CSS/HTML-`baseline` fuer Zellen implementiert; Baseline-Row-Spans nutzen die bestehende gleichmaessige Verteilung zusaetzlicher Row-Hoehe als lokale Policy. CSS 2.2 bestimmt diese Hoehenfolge nicht; UA-Topdefault bleibt erhalten. | Nutzerantwort 2026-11-07; [Missing.md](../plans/Missing.md#5-general-css-layout-and-painting), [text-layout.md](../docs/text-layout.md#basic-automatic-tables), [KP-224](./Kontextprotokoll.md#kp-224---pass-10-baselineausrichtung-mit-row-spans)                                                                                                                                                                                 |
| Q-028 | Soll Pass 5 automatische Tabellen mit Prozentbreiten auf `col`/`colgroup` unterstuetzen oder eine andere Tabellenluecke waehlen?                                                                                                          | Beantwortet und umgesetzt: Nutzer waehlte Option 1, begrenzter Solver mit explizitem Fehler bei unloesbaren Constraints; Commit `de132ad`, CI-gruen                                                           | Prozentconstraint-Solving fuer Auto-Tabellen; resultierende Algorithmik und Restgrenzen in DEC-030/KP-230                                                                                                                             | Nutzerantwort 2026-11-07, 17:15 Europe/Berlin; [Missing.md](../plans/Missing.md#5-general-css-layout-and-painting), [text-layout.md](../docs/text-layout.md#basic-automatic-tables), [CSS 2.2](../specs/cache/css2-tables.html#auto-table-layout), [KP-229](./Kontextprotokoll.md#kp-229---pass-5-auto-tabellen-prozentbreiten-umfang), [KP-230](./Kontextprotokoll.md#kp-230---pass-5-auto-tabellen-prozentbreiten-implementiert) |
| Q-029 | Soll `valign` auf Tabellenzeilen/-gruppen als Standardausrichtung ihrer Zellen wirken, obwohl CSS 2.2 `vertical-align` als nicht vererbt und nur auf table-cell/inline-level anwendbar definiert?                                         | Beantwortet: Zell-Default; naechste row/group gewinnt vor Gruppe, Zell-CSS/-Hint ueberschreibt                                                                                                                | Nur HTML-`valign`-Hints auf `tr`/row-group werden als Default auf enthaltene Zellen uebertragen; keine allgemeine CSS-Vererbung behaupten                                                                                             | Ausdrueckliche Nutzerwahl „cell-default“; HTML `#tables-2` und CSS 2.2 `#propdef-vertical-align`/`#height-layout`; [KP-238](./Kontextprotokoll.md#kp-238---naechster-tabellenpass-row-group-valign-offen), [KP-239](./Kontextprotokoll.md#kp-239---row-group-valign-zell-default-gewaehlt)                                                                                                                                         |
| Q-030 | Soll externe klassische `<script src>`-Quellen nur in einem begrenzten Post-Parse-Modell oder mit HTML-konformer parser-blockierender Reihenfolge geladen und ausgefuehrt werden, und welche Quellen duerfen geladen werden?              | Beantwortet und lokal umgesetzt: parser-blockierende Klassiker; CSP-gesteuertes HTTP(S) einschliesslich Cross-Origin gewaehlt                                                                                 | Weitere Ausdehnung auf Module, `async`/`defer` oder unbeschraenkte Ressourcen bedarf eigener Auswahl und bleibt deferred                                                                                                              | Ausdrueckliche Nutzerwahl; [Missing.md](../plans/Missing.md#6-a-persistent-browser-javascript-environment), [KP-326](./Kontextprotokoll.md#kp-326---parser-blockierende-externe-klassiker-gewaehlt), [KP-333](./Kontextprotokoll.md#kp-333---parser-blockierende-klassiker-implementiert-und-validiert)                                                                                                                            |
| Q-031 | Wie sollen parser-blockierende externe Skripte Browser-Ressourcenabrufe erhalten, ohne dem Renderer Netzwerkzugriff zu geben: vorab bereitgestellter begrenzter Snapshot oder Request/Response-IPC am Parser-Haltepunkt?                  | Beantwortet und lokal umgesetzt: Request/Response-IPC am Parser-Haltepunkt                                                                                                                                    | Keine Renderer-Netzwerkfaehigkeit; weitere Ressourcentypen nicht aus dieser Entscheidung ableiten                                                                                                                                     | Nutzerwahl `parser_pause_ipc`; [renderer-processes.md](../docs/renderer-processes.md#private-stream-protocol-v49), [KP-328](./Kontextprotokoll.md#kp-328---parser-pause-ipc-fuer-externe-skripte-gewaehlt), [KP-333](./Kontextprotokoll.md#kp-333---parser-blockierende-klassiker-implementiert-und-validiert)                                                                                                                     |
| Q-032 | Soll die parser-pausierende Skript-Broker-Unterstuetzung auch im Single-Process-Modus gelten, obwohl dort ein synchrones Netzwerk-Warten den nativen Shell-Thread blockieren wuerde?                                                      | Beantwortet und lokal umgesetzt: asynchroner Abruf, stabiler dedizierter Renderer-Thread                                                                                                                      | Keine UI-Blockierung oder Netzwerkaufrufe aus Renderer-Engine                                                                                                                                                                         | Nutzerwahl `async_single_process_architecture`; [browser-shell.md](../docs/browser-shell.md#navigation-and-page-policy), [KP-330](./Kontextprotokoll.md#kp-330---asynchrone-single-process-renderarchitektur-gewaehlt), [KP-333](./Kontextprotokoll.md#kp-333---parser-blockierende-klassiker-implementiert-und-validiert)                                                                                                         |
| Q-033 | Soll die neue Parser-Ausfuehrung auch Inline-Klassiker in Inline-only-Dokumenten vom bisherigen Post-Parse-Zeitpunkt auf Parserabschluss umstellen?                                                                                       | Beantwortet und umgesetzt: parser-blockierende HTML-Reihenfolge fuer alle klassischen Inline- und externen Skripte                                                                                            | Alle parser-inserted Klassiker am Parserposition ausfuehren; vollstaendige HTML-Vorbereitung bleibt deferred                                                                                                                          | Nutzerwahl nach Klaerung der HTML-Anforderungen; [HTML #script-processing-model](https://html.spec.whatwg.org/multipage/scripting.html#script-processing-model), [KP-332](./Kontextprotokoll.md#kp-332---parserreihenfolge-fuer-alle-klassiker-beschlossen), [KP-333](./Kontextprotokoll.md#kp-333---parser-blockierende-klassiker-implementiert-und-validiert)                                                                    |
| Q-034 | Welchen Umfang soll das naechste ECMAScript-Modulinkrement haben: importfreie Inline-Module als erster Schritt oder einen begrenzten statischen Abhaengigkeitsgraphen mit browservermitteltem Ressourcenabruf?                            | Beantwortet: importfreie Inline-Module; sie laufen nach Parsing vor DOMContentLoaded; Imports bleiben aus                                                                                                     | Keine Modulimporte oder Modulgraph-/Scheduling-Erweiterung; der fruehere Top-Level-await-Ausschluss wurde durch Q-036 ersetzt                                                                                                         | Nutzerwahl `inline_only`; [Missing.md](../plans/Missing.md#recommended-priority), [standards.md](../docs/standards.md#support-status), [KP-335](./Kontextprotokoll.md#kp-335---ecmascript-module-umfang-offen), [KP-336](./Kontextprotokoll.md#kp-336---begrenzte-top-level-await-auswertung-gewaehlt)                                                                                                                             |
| Q-035 | Wie soll die Implementierung Top-Level-await in Inline-Modulen zuverlaessig ausschliessen, wenn ClearScript es nativ zulaesst?                                                                                                            | Offen; die Modulhost-Ausfuehrung warf beim lokalen Probeaufruf keinen synchronen Fehler; async Modulevaluation kann readiness und Taskbudget ueberschreiten                                                   | Nutzer zwischen konservativem Verbot von `await`-Syntax im gesamten Modul, einem separaten begrenzten Async-Evaluationsdesign oder Blockieren weiterer Aktionen waehlen lassen                                                        | Lokaler ClearScript-7.5.1.1-Probe; [KP-335](./Kontextprotokoll.md#kp-335---ecmascript-module-umfang-offen), [ClearScript Top-Level Await](https://github.com/ClearFoundry/ClearScript/blob/master/docs/_posts/2022-2-4-top-level-await.md)                                                                                                                                                                                         |

| Q-036 | Soll der gewaehlte Modulumfang Top-Level-await ausschliessen oder begrenzt asynchron auswerten? | Beantwortet: Nutzer revidiert den Ausschluss und waehlt begrenzte asynchrone Auswertung unter Readiness-/Taskbudget | Keine unbeschraenkte Promise-Wartezeit und keine Import-/Netzwerkberechtigung; Cancellation, Fehler und Fristen muessen explizit abgedeckt sein | Nutzerwahl `support-bounded-async`; [KP-336](./Kontextprotokoll.md#kp-336---begrenzte-top-level-await-auswertung-gewaehlt), [KP-335](./Kontextprotokoll.md#kp-335---ecmascript-module-umfang-offen) |
| Q-037 | Welcher CSS-Color-4-Gamut-Mapping-Algorithmus soll fuer die naechste sRGB-Ausgabe-Erweiterung gewaehlt werden? | Beantwortet: Nutzer waehlt Ray Trace | In Pass 1 auf aus-gamutige `color(display-p3 ...)`-Ergebnisse anwenden; andere Farbquellen bleiben unveraendert | ausdrueckliche Nutzerwahl „3“, 2026-11-15 12:21 Europe/Berlin; [CSS Color 4](../specs/cache/css-color.html#GMA-Raytrace); [KP-380](./Kontextprotokoll.md#kp-380---ray-trace-gamut-mapping-gewaehlt) |
| Q-038 | Bedeutet „WHATWG URL vollstaendig“ die Core/.NET-Spezifikationsalgorithmen oder auch die browserseitige JavaScript-URL-/URLSearchParams-API mit Live-Linkage und Web-IDL-Verhalten? | Beantwortet: Nutzer genehmigte Core-Algorithmen sowie browserseitige URL-/URLSearchParams-Semantik und interface-spezifisches Web IDL | Allgemeine Web-IDL-Generierung bleibt ausgeschlossen; Umsetzung folgt dem genehmigten Sechs-Phasen-Plan | Nutzerbestaetigung 2026-11-18; [Plan](../plans/whatwg-url-completion-plan.md), [Core guide](../docs/core.md#url), [URL spec cache](../specs/cache/url.html), [KP-468](./Kontextprotokoll.md#kp-468---url-planumfang-genehmigt) |
| Q-039 | Soll die Fortsetzung ueber die genehmigte URL-/URLSearchParams-Oberflaeche hinaus in den File-API-Blob-URL-Store oder HTML/DOM-Dokument-URL-Integration gehen? | Teilweise beantwortet: Nutzer waehlt HTML/DOM-bezogene Folgearbeit; Blob-URL-Store bleibt ungewaehlt. Umgesetzt sind begrenzte PageRendering-Bindungen fuer readonly `document.URL`, `document.documentURI`, `Node.baseURI` und die URL-Komponenten von `<a>`/`<area>`. Die Nutzeranweisung vom 2026-11-19 erweitert das Ziel auf breitere HTML/DOM-Folgearbeit, waehlt aber noch kein erstes Inkrement | Keine Vollintegration oder Prozentsteigerung aus den begrenzten Teilumfaengen ableiten; mit Q-040 die Reichweite des neuen Auftrags klaeren | Nutzeranweisung „let's do the html/dom related changes next“ und „complete WHATWH html and dom“; KP-513 bis KP-515, KP-523, [HTML/DOM](../docs/html-dom.md), [HTML HyperlinkElementUtils](../specs/cache/html.html#api-for-hyperlink-elements), [DOM URL attributes](../specs/cache/dom.html#dom-document-url), [Support-Inventar](../docs/standards.md#implemented-subsets) |
| Q-040 | Was bedeutet der Auftrag, WHATWG HTML und DOM zu „vervollstaendigen“, und welches erste zusammengehoerige HTML-/DOM-Inkrement ist gemeint? | Beantwortet fuer den ersten mehrphasigen Template-Umfang: Nutzer genehmigte bounded Template-Contents, Parser-Subset und Inertheits-/Limitintegration. Ein vollstaendiger HTML/DOM-Abschlussendpunkt ist weiterhin nicht definiert | Phase 1-3 des [Plans](../plans/whatwg-html-dom-completion-plan.md) in separaten, getesteten Phasen ausfuehren; danach den naechsten Inkrementumfang anhand des aktualisierten Trackers waehlen | Ausdrueckliche Nutzerfreigabe 2026-11-19; KP-524; [HTML/DOM guide](../docs/html-dom.md), [approved plan](../plans/whatwg-html-dom-completion-plan.md) |
| Q-041 | Welchen Umfang soll die naechste HTML/DOM-Arbeit nach dem Template-Plan haben? | Beantwortet: Nutzer waehlt Option 2; bounded Fragment Parsing ist in KP-530/531 umgesetzt und in KP-605 auf `applet`, `marquee` und `object` erweitert | Urspruenglicher Umfang: Kontextdokument-eigenes `DocumentFragment` fuer die dokumentierten In-Body-, Table-, Select-, Template- und Text-Modi; SVG/MathML, frameset/frame, `noscript` sowie nicht unterstuetzte Algorithmen scheitern explizit. KP-609 erweitert `noscript` um den scripting-disabled Data-Zustand. Keine Script-Bindings oder `innerHTML` | Ausdrueckliche Nutzerwahl „2“; [KP-530](./Kontextprotokoll.md#kp-530---fragment-parsing-umfang-beschlossen), [KP-531](./Kontextprotokoll.md#kp-531---fragment-parsing-committed-und-ci-geprueft), [KP-605](./Kontextprotokoll.md#kp-605---embedded-fragmentkontexte), [KP-609](./Kontextprotokoll.md#kp-609---noscript-fragmentkontext), [HTML fragment parsing](../specs/cache/html.html#parsing-html-fragments) |

Q-035 ist durch die spaetere Nutzerentscheidung Q-036 beantwortet; der zuvor
festgehaltene Ausschluss von Top-Level-await ist ersetzt.

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
- HTML/DOM-Fortsetzung: `bbeeff7` fuehrt bounded Fragment Parsing fuer die
  bereits unterstuetzten Kontexte ein und ist gepusht; Linux Trunk meldete
  dieselben 28 Markdownlint-Probleme wie der unmittelbar vorherige Commit.
  `f748224` fuehrt die bounded aktive Formatierung und den no-furthest-block-
  Adoption-Agency-Zweig ein und ist gepusht; Engine.Html (7.339), Solution-
  Build, Volltests und Formatverifikation bestanden. Linux- und Windows-
  Plattformjobs sowie CodeQL sind erfolgreich; nur der bekannte Trunk-
  Markdownlint-Job scheitert. Der Dev-Environment-Prebuild `44657161646` ist
  laut GitHub erfolgreich abgeschlossen.
- Geschriebene Kontextdateien erneut gelesen; lokale Links und Markdown-Anker,
  eindeutige REQ-/DEC-/Q-/KP-IDs, Quellenregister und Whitespace automatisiert
  geprueft. Externe GitHub-Links sind Quellenverweise, keine Browser-Abnahmetests.

## Naechste Schritte

**Aktueller Einstieg (2026-11-20; KP-531/534/537):** Der Template-Plan ist mit
`3a02cca`, `a752387` und `5752d3b` abgeschlossen. Bounded Fragment Parsing ist
mit `bbeeff7` committed und gepusht; aktive Formatierung mit no-furthest-block
und bounded furthest-block-Reparenting ist mit `b4b4a88` committed und gepusht.
Linux x64/arm64, Windows x64/arm64 und CodeQL bestehen; Trunk scheitert mit
denselben 28 Markdownlint-Befunden in unveraenderten Plan-/Abschlussdokumenten
und `tests/Core.Tests/Data/README.md` wie der Basislauf. Der Dev-Environment-
Prebuild `44657161646` ist erfolgreich. Der grosse Nutzerauftrag bleibt
inkrementell: WHATWG-HTML/DOM-Vollkonformitaet ist kein abgeschlossener
Endpunkt. Die In-Body-Recovery fuer verschachtelte `<a>`- und `<nobr>`-Starttags
auf Basis des Adoption-Agency-Algorithmus ist implementiert; Review ist
APPROVED. Engine.Html (7.362), alle Solution-Tests (17.358 bestanden, zwei
bedingte Browser-Skips), Solution-Build (0 Warnungen/Fehler),
Formatverifikation und Diff-Check bestehen. Commit `47bb853` ist auf `main`
gepusht. Linux x64/arm64, Windows x64/arm64 und CodeQL bestehen. Der Linux-
Trunk-Job meldet weiterhin 28 Markdownlint-Befunde ausschliesslich in
unveraenderten WHATWG-Abschluss-/Plan-Dokumenten und
`tests/Core.Tests/Data/README.md`, ausserdem eine Trunk-Warnung zu nicht mehr
unterstuetztem Check-upload/`check-mode: all`. Der Dev-Environment-Prebuild
laeuft noch. Die
`caption`-Grenzen sind in beiden Scope-Pruefungen ergaenzt. Ein isolierter
Caption-Fall ist in der aktuellen Parser-Teilmenge nicht direkt konstruierbar:
Caption ausserhalb einer Tabelle wird abgelehnt, waehrend echte Tabellen-
Captions sowohl eine Tabellen-Scope-Grenze als auch einen Formatting-Marker
einfuehren. Der Review-Hinweis zur nested-anchor-Clone-Leakage wurde nach
Regressions- und Referenzpruefung zurueckgezogen; siehe KP-536 bis KP-539 und
[Missing.md](../plans/Missing.md#4-general-html-and-dom-compatibility).
Table-Foster-Parenting mit noetiger Rekonstruktion bleibt ein separater
spaeterer Schnitt. Die Parser-Algorithmen bleiben bounded; Foreign Content,
Script-Bindings, `innerHTML` und andere nicht implementierte Algorithmen
bleiben unsupported/deferred. Nach Abschluss des Prebuilds den naechsten
Tracker-Schnitt waehlen; die beiden Kontextdateien bleiben lokale, nicht im
Code-Commit enthaltene Aenderungen.

**Aktueller Einstieg (2026-11-07):** Ein neuer Zehn-Passen-Auftrag laeuft.
Pass 1 erweitert den in Pass 5 bewusst top-only gehaltenen Caption-Umfang als
separates Tabelleninkrement um CSS-2.2-`caption-side:bottom`. CSS und Layout,
Solution-Build (0 Warnungen/Fehler), Volltest (16.214) und Formatpruefung
bestehen lokal; Commit `ef3247f` ist gepusht, Linux, Windows und CodeQL-CI sind
gruen. Pass 2 erweitert Prozentbreiten von `col`/`colgroup` bei definitiver
Tabellenbreite; Layout (858), Build, Volltest (16.218) und Formatpruefung
bestehen lokal. Commit und Push stehen aus; siehe
[KP-226](./Kontextprotokoll.md#kp-226---prozentbreiten-fuer-definitive-tabellen).
Details zu Pass 1 in DEC-029 und
[KP-225](./Kontextprotokoll.md#kp-225---zehn-passen-lauf-caption-side-als-erster-tabellenpass).

**Historischer Einstieg (2026-11-01):** Im damaligen Zehn-Passen-Auftrag sind
acht Inkremente committed, gepusht und CI-gruen. Pass 5 zu gewoehnlichem
HTML-`hidden` ist nach Review und 15.907 erfolgreichen Solution-Tests als
`37dc296` gepusht; Linux, Windows und CodeQL bestehen. Pass 6 hat die
HTML-UA-Darstellung von `listing`, `plaintext` und `xmp` ergaenzt und
fokussiert geprueft. Die Review-Ergaenzungen und alle 15.943 Solution-Tests
bestehen. Commit `e4d0b79` ist gepusht; CodeQL, Linux-CI und der zweite
Windows-Versuch bestehen. Der kuenstlich verzoegerte Baseline-Test schlug mit
einem anderen Deadline-Fehler fehl; sein genauer Zusammenhang zum ersten
Windows-Fehler ist offen. Die temporaere Instrumentierung wurde entfernt;
der unveraenderte lokale Release-Test besteht. Pass 7 hat den UA-Blockdefault
fuer HTML `search` implementiert und fokussiert geprueft; die Review-
Ergaenzungen, der abschliessende Build und alle 15.953 Solution-Tests
bestehen. Commit `087954f` ist gepusht; Linux, Windows und CodeQL bestehen
fuer diesen Commit. Pass 8 soll die bereits geparsten HTML-`align`-Attribute
auf `p` und `h1` bis `h6` ueber vorhandene CSS-Praesentationshinweise und
Textausrichtung und ist fokussiert getestet. Review, Solution-Build,
Gesamtformatierung und lokaler V8-Smoke bestehen. Im Volltest schlug ein
unveraenderter Browser-Navigationstest fehl, waehrend 15.986 Faelle bestanden.
Fuenf fokussierte Wiederholungen bestehen. Ein verzoegerter Baseline-Versuch
reproduziert dieselbe Assertion; gezieltes Return nur bei Dialog besteht.
Die genaue Ursache des urspruenglichen Fehlers bleibt offen. Die Volltest-
Wiederholung bestand 15.986 Faelle und scheiterte am anderen, bereits auf
Baseline `37dc296` mit Transportverzug und gleichem Deadline-Fehler
reproduzierten Shell-Test. Beide Tests bestehen in drei fokussierten
Wiederholungen. Ursachen der uninstrumentierten Fehler bleiben offen; Commit
`257f0b1` ist gepusht; Linux, Windows und CodeQL bestehen fuer diesen Commit.
Als Naechstes wird Pass 9 anhand des Trackers bestimmt
([KP-135](./Kontextprotokoll.md#kp-135---search-blockdefault-ausgewaehlt),
[KP-136](./Kontextprotokoll.md#kp-136---search-blockdefault-implementiert),
[KP-137](./Kontextprotokoll.md#kp-137---search-review-ergaenzt),
[KP-138](./Kontextprotokoll.md#kp-138---search-volltest-gestartet),
[KP-139](./Kontextprotokoll.md#kp-139---search-volltest-bestanden),
[KP-140](./Kontextprotokoll.md#kp-140---search-commit-und-ci-start),
[KP-141](./Kontextprotokoll.md#kp-141---search-ci-bestanden),
[KP-142](./Kontextprotokoll.md#kp-142---absatz-und-ueberschriftenausrichtung-ausgewaehlt),
[KP-143](./Kontextprotokoll.md#kp-143---ausrichtung-implementiert-und-reviewt),
[KP-144](./Kontextprotokoll.md#kp-144---format-und-v8-smoke-bestanden),
[KP-145](./Kontextprotokoll.md#kp-145---ausrichtung-volltest-laeuft),
[KP-146](./Kontextprotokoll.md#kp-146---browser-volltestfehler),
[KP-147](./Kontextprotokoll.md#kp-147---baselineversuch-vorbereitet),
[KP-148](./Kontextprotokoll.md#kp-148---baseline-timingreproduktion),
[KP-149](./Kontextprotokoll.md#kp-149---volltest-wiederholung-laeuft),
[KP-150](./Kontextprotokoll.md#kp-150---zweiter-browser-volltestfehler),
[KP-151](./Kontextprotokoll.md#kp-151---navigationstests-fokussiert-bestanden),
[KP-152](./Kontextprotokoll.md#kp-152---ausrichtung-commit-und-ci-start),
[KP-153](./Kontextprotokoll.md#kp-153---ausrichtung-ci-bestanden)).
Der aktuelle Stand und die Grenzen stehen oben sowie in
[KP-121](./Kontextprotokoll.md#kp-121---rohtextdarstellung-als-sechstes-inkrement)
[KP-122](./Kontextprotokoll.md#kp-122---rohtextdefaults-implementiert)
[KP-123](./Kontextprotokoll.md#kp-123---review-regressionen-ergaenzt),
[KP-124](./Kontextprotokoll.md#kp-124---review-regressionen-validiert)
[KP-125](./Kontextprotokoll.md#kp-125---formatpruefung-erfolgreich)
[KP-126](./Kontextprotokoll.md#kp-126---rohtext-volltest-erfolgreich)
[KP-127](./Kontextprotokoll.md#kp-127---rohtext-commit-und-ci-start)
[KP-128](./Kontextprotokoll.md#kp-128---rohtext-codeql-erfolgreich)
[KP-129](./Kontextprotokoll.md#kp-129---rohtext-linux-ci-erfolgreich)
[KP-130](./Kontextprotokoll.md#kp-130---windows-x64-testfehler)
[KP-131](./Kontextprotokoll.md#kp-131---windows-wiederholungen-erfolgreich)
[KP-132](./Kontextprotokoll.md#kp-132---baseline-timingversuch-vorbereitet)
[KP-133](./Kontextprotokoll.md#kp-133---baseline-timingversuch-beendet)
und [KP-134](./Kontextprotokoll.md#kp-134---pass-6-ci-gruen).
Die folgenden aelteren Schritte und Passnummern beschreiben fruehere Auftraege;
sie sind keine zusaetzlichen aktuellen Arbeitsauftraege.

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

## Historischer Zwischenstand: Pass 9 der Zehn-Passen-Serie

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
