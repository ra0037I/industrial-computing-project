# AR-Schaltschrank-App („Wartungsassistent“) – Projektzusammenfassung

**Stand:** 18.10.2026
**Kurs:** Industrial Computing (MCI, Master)
**Repository:** https://github.com/ra0037I/industrial-computing-project
**Technik:** Unity 6 (6000.6.0f1), URP, Vuforia Engine 11.4.4, Android, Google ML Kit Text Recognition

---

## 1. Ziel

Eine Android-AR-App, die Wartungspersonal im Schaltschrank unterstützt:

1. **Schaltschrank erkennen.** Vuforia erkennt den realen Schrank anhand eines 3D-Scans (Model Target). Aktuell sind zwei Schränke hinterlegt: **Hauptverteiler** (`mainsCabinet`) und **Steuerschrank** (`controlCabinet`).
2. **Betriebsmittelkennzeichnungen (BMK) lesen.** Der Nutzer hält ein BMK-Schild (z. B. `37T1`) in einen Rahmen. Die App liest den Text per Texterkennung (OCR).
3. **Zusatzinfos anzeigen.** Zum BMK erscheinen Typ, Hersteller, Funktion, Status, letzte Prüfung usw. Die Daten kommen aktuell aus Mock-Dateien, später aus einem Webservice.

---

## 2. Aufbau der App

### 2.1 Szenen (Reihenfolge in der Build-Liste)

| Szene | Zweck |
|---|---|
| `Main` | Hauptmenü „Wartungsassistent“ mit drei Buttons. Ohne Kamerabild (Vuforia Behaviour deaktiviert). |
| `CabinetScanner` | Schaltschrank-Erkennung per Model Target, Popup mit Sprung zur BMK-Erkennung |
| `BmkScanner` | BMK-Erkennung per OCR und Info-Panel |
| `SessionRecorder` | Vuforia-Aufnahme (Session Recorder), um Testvideos für neue Schränke aufzuzeichnen |

### 2.2 Ablauf

```
Main ──► Schaltschrank Erkennung ──(Schrank erkannt → Popup)──► Betriebsmittel Scanner
  │                                                                  (nur BMKs dieses Schranks)
  ├────► Betriebsmittel Scanner (alle Schränke)
  └────► Vuforia Aufnahme Assistent
```

Der Zurück-Button führt immer zur Szene, aus der man gekommen ist (Szenenverlauf in `ChangeScene`).

---

## 3. Funktionen im Detail

### 3.1 Schaltschrank-Erkennung (`CabinetScanner`)

- **Model Targets:** Je Schrank ein Vuforia-Datensatz aus dem Model Target Generator (`Assets/StreamingAssets/Vuforia/{Schrank}.dat/.xml`). Grundlage sind Photogrammetrie-Scans (Scaniverse, OBJ).
- **Nur ein Datensatz gleichzeitig:** Vuforia kann immer nur einen Model-Target-Datensatz aktiv haben. `ModelTargetSwitcher` hält deshalb genau ein Target aktiv und wechselt automatisch zum nächsten, wenn das aktive einige Sekunden (eingestellt: 2 s) nicht `TRACKED` ist. `EXTENDED_TRACKED` zählt bewusst nicht als „im Bild“.
- **Popup (`CabinetFoundPopup`):** Erscheint bei `TRACKED` am unteren Rand. Der Text wird aus einer Vorlage zusammengebaut (`{name}`, `{count}`, `{target}`), d. h. für neue Schränke ist kein Eintrag im Inspector nötig. Der Button „Zur BMK-Erkennung“ übergibt den erkannten Schrank an den BMK-Scanner.
- **Texturierte Modelle:** Die OBJ-Modelle liegen als Kind-Objekte unter den Model Targets (nur zur Kontrolle im Editor, in der App ausgeblendet).

### 3.2 BMK-Erkennung (`BmkScanner`)

- **Texterkennung:** Google ML Kit (on-device, offline) über das Android-Plugin `BmkOcr.java`. Vuforia selbst hat keine Texterkennung.
- **Ablauf:**
  1. Rahmen in der Bildschirmmitte, Hinweis z. B. „Steuerschrank: BMK in den Rahmen halten“.
  2. Kamerabild von Vuforia holen (`CameraDevice.GetCameraImage`), Bereich unter dem Rahmen ausschneiden und aufrecht drehen.
  3. Ausschnitt als JPEG an ML Kit, Text zurück an Unity.
  4. Text bereinigen und gegen die BMK-Listen **aller** Schränke prüfen; typische Verwechslungen (1/I, 0/O, 7/T …) werden korrigiert.
  5. Nach 2 gleichen Treffern hintereinander wird der Rahmen grün, das Info-Panel erscheint.
- **Info-Panel (`BmkInfoPanel`):** Zeigt die Daten zum BMK mit farbigem Statusbalken:
  - **Grün:** OK · **Orange:** Wartung fällig / prüfen · **Rot:** Störung oder keine Daten
  - **Orange mit Hinweis „nicht in diesem Schrank“:** Das BMK gehört zu einem anderen Schrank (mit Angabe, zu welchem) oder ist im gewählten Schrank nicht verzeichnet.
- **Im Editor:** ML Kit läuft nur auf Android. Im Editor wird das OCR-Ergebnis über *Editor Simulated Text* simuliert.

### 3.3 Daten pro Schrank

Ablage per Namenskonvention, kein Inspector-Setup nötig (`BmkContext`):

```
Assets/Resources/BmkData/
  schraenke.txt                 TargetName;Anzeigename  (z. B. controlCabinet;Steuerschrank)
  mainsCabinet/   bmk_liste.txt, bmk_mock.json
  controlCabinet/ bmk_liste.txt, bmk_mock.json
```

- Kommt man über das Popup in den BMK-Scanner, gelten nur die Daten des erkannten Schranks; über das Hauptmenü die Daten aller Schränke.
- Datenquelle im Info-Panel umschaltbar: **Mock** (JSON) oder **Rest** (`GET {URL}/{BMK}?schrank={TargetName}`).

**Neuen Schrank hinzufügen:** Model Target in `CabinetScanner` anlegen → Zeile in `schraenke.txt` → Ordner mit `bmk_liste.txt` und `bmk_mock.json`. Mehr ist nicht nötig.

### 3.4 BMKs aus den Fotos (`pictures/`)

**Hauptverteiler (63 BMKs):**

| Ebene | BMKs |
|---|---|
| 1 – LS-Schalter | 1F1–1F9, 5F1–5F6, 6F1–6F6, 7F1–7F6, 100B1 (Thermostat) |
| 2 – Motorschutzschalter | 10Q1, 11Q1–11Q3, 12Q1–12Q4 |
| 3 – Schütze | 30Q1–37Q1, 110Q1, 110Q2, 110Q4, 110Q5, 161Q3, 161Q4, 162Q1 |
| 4 – Frequenzumrichter (Lenze) | 30T1–37T1 |
| 6 – Einspeisung | 1Q1 (Hauptschalter), 2F1, 2F2, 1X1 (Steckdose) |

**Steuerschrank (11 BMKs):**

| Ebene | BMKs |
|---|---|
| 1 – 24-V-Versorgung | 65T1 (SITOP PSU300S), 65Q1–65Q3, 66Q1 (SITOP PSE200U) |
| 2 – Steuerung/Netzwerk | 99K1 (SIMATIC ET 200SP), 90K1 (Industrial Ethernet Switch) |
| 3 – Schütze | 161Q1, 161Q2, 163Q3, 163Q4 |

Gerätetypen sind vom Foto abgelesen; Funktionen und Artikelnummern sind Beispieldaten.
Teststatus: **37T1, 163Q4** Störung · **33T1, 12Q3, 65T1** Wartung fällig · **5F4, 66Q1** ausgelöst – prüfen.

### 3.5 Testen im Editor mit Videos

- Vuforia *Play Mode* = **Recording**. Dafür sind **Vuforia-Session-Aufnahmen** nötig (MP4 mit Kamera- und Sensordaten, aufgenommen mit dem Session Recorder in der App). Normale Handyvideos funktionieren nicht.
- `SceneRecording` legt pro Szene fest, welches Video abgespielt wird (`Assets/Videos/cabinet.mp4`, `bmkScanner.mp4`). `SceneRecordingPlayMode` setzt das Video der Startszene schon vor dem Play-Start und stellt die Konfiguration danach wieder her.
- `PlayFromMainScene` startet Play immer mit der Main-Szene (`Tools → Immer mit Main-Szene starten`).
- Am Handy wird immer die echte Kamera verwendet; der Play Mode gilt nur im Editor.

---

## 4. Dateien

| Datei | Ablageort | Zweck |
|---|---|---|
| `ChangeScene.cs` | `Assets/Scripts/` | Szenenwechsel mit Verlauf (`MoveToScene`, `GoBack`) |
| `ModelTargetSwitcher.cs` | `Assets/Scripts/` | Wechselt zwischen mehreren Model Targets (nur eines aktiv) |
| `CabinetFoundPopup.cs` | `Assets/Scripts/` | Popup bei erkanntem Schrank, Übergabe an BMK-Scanner |
| `BmkContext.cs` | `Assets/Scripts/` | Gewählter Schrank, Laden der BMK-Daten, Zuordnung BMK → Schrank |
| `BmkScanner.cs` | `Assets/Scripts/` | Rahmen-Overlay, Bildausschnitt, OCR-Aufruf, BMK-Prüfung |
| `BmkInfoPanel.cs` | `Assets/Scripts/` | Datenmodell, Datenquellen (Mock/REST), Info-Panel, Hinweis „nicht in diesem Schrank“ |
| `SceneRecording.cs` | `Assets/Scripts/` | Video pro Szene im Editor (Play Mode Recording) |
| `BmkOcr.java` | `Assets/Plugins/Android/` | ML-Kit-Texterkennung, Rückgabe per `UnitySendMessage` |
| `mainTemplate.gradle` | `Assets/Plugins/Android/` | Custom Gradle Template |
| `MlKitGradleDependency.cs` | `Assets/Editor/` | Trägt die ML-Kit-Abhängigkeit beim Build automatisch ein |
| `PlayFromMainScene.cs` | `Assets/Editor/` | Play startet immer mit Main |
| `SceneRecordingPlayMode.cs` | `Assets/Editor/` | Video der Startszene vor Play setzen |
| `BmkData/**` | `Assets/Resources/` | BMK-Listen, Mockdaten, Schrankliste |
| `{Schrank}.dat/.xml` | `Assets/StreamingAssets/Vuforia/` | Vuforia-Datensätze der Model Targets |
| `*.obj/.mtl/.jpg` | `Assets/Resources/{Schrank}/` | Texturierte Scans |
| `*.mp4` | `Assets/Videos/` | Vuforia-Session-Aufnahmen für Editor-Tests |

---

## 5. Einstellungen

| Bereich | Einstellung |
|---|---|
| Vuforia Configuration | App License Key (lokal eintragen, **nicht committen**), Play Mode im Editor: Recording |
| Player → Android | Min. API Level ≥ 24, IL2CPP, ARM64 |
| Player → Android → Publishing | *Custom Main Gradle Template* aktiv |
| Editor → Enter Play Mode | Reload Domain aktiv (gegen Vuforia-Material-Fehler) |
| Version Control / Serialization | Visible Meta Files / Force Text |
| Build Profiles → Scene List | Main, CabinetScanner, BmkScanner, SessionRecorder |
| ARCamera | World Center Mode = Device |

---

## 6. Behobene Probleme

| Problem | Ursache | Lösung |
|---|---|---|
| Schrank wird nicht erkannt, Simulator schwarz | Vuforia-Lizenzschlüssel fehlte (`Failed to create ModelTargetObserver`) | Lizenzschlüssel in der Vuforia Configuration eintragen |
| Erkennung unmöglich | Model Target war 1000-fach skaliert | Skalierung 1; Position der Targets in der Szene ist bei *World Center Mode = Device* egal |
| Zweiter Schrank wird nie erkannt | `Failed to activate data set because another one is already active` | `ModelTargetSwitcher` |
| Switcher wechselt nie | `EXTENDED_TRACKED` bleibt nach Erkennung dauerhaft stehen | Nur `TRACKED` zählt |
| Video im Editor bleibt leer | VLC-Video ohne Vuforia-Daten (`Failed to open external camera`) | Aufnahme mit dem Vuforia Session Recorder |
| Texturiertes Modell überdeckt den echten Schrank | Kind-Objekt wird bei Erkennung eingeblendet | Kind-Objekt deaktivieren |
| `CS0104: 'Image' is an ambiguous reference` | `UnityEngine.UI.Image` und `Vuforia.Image` | `using Image = UnityEngine.UI.Image;` |
| ML-Kit-Paket beim Build nicht gefunden | Abhängigkeit kam nicht im Gradle-Projekt an | `MlKitGradleDependency.cs` |
| Szene nach Umbenennen nicht gefunden | Reine Groß-/Kleinschreibung erkennt Git unter Windows nicht | `git rm --cached` alter Name + `git add` neuer Name |

---

## 7. Git

- `.gitignore` schließt u. a. `Library`, `Temp`, `Logs`, `UserSettings`, Builds, IL2CPP-Backup-Ordner, `QCAR/`, `.utmp/`, Recovery-Szenen und `PerformanceTestRun*.json` aus.
- `.gitattributes`: große Binärdateien (OBJ, Texturen, `.dat`, `.mp4`, `.tgz` …) über **Git LFS**.
- `.meta`-Dateien immer mit einchecken (enthalten die GUIDs, über die Szenen auf Assets verweisen).
- `VuforiaConfiguration.asset` nur ohne Lizenzschlüssel committen.

---

## 8. Offene Punkte

- [ ] **controlCabinet neu generieren:** im Model Target Generator mit Modus wie beim mainsCabinet (nicht „car“) und das Modell senkrecht ausrichten (aktuell ca. 5° geneigt).
- [ ] Am echten Schrank prüfen: **BMK 100B1** (auf dem Foto schlecht lesbar) und ob **110Q3** tatsächlich fehlt.
- [ ] **Mockdaten durch echte Daten ersetzen** (z. B. Schaltplan/EPLAN-Export), danach Webservice anbinden (*Source* = Rest).
- [ ] OCR unter echten Bedingungen testen (Licht, Abstand, Winkel) und *Scan Interval* / *Required Matches* feinjustieren.
- [ ] Lizenzschlüssel aus der gemeinsamen Konfiguration herausnehmen (jeder trägt ihn lokal ein bzw. lädt ihn aus einer nicht versionierten Datei).
