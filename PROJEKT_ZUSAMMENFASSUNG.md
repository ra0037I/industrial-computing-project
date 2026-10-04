# AR-Schaltschrank-App – Projektzusammenfassung

**Stand:** 04.10.2026
**Projekt:** `C:\Users\andre\unity\My project`
**Repository:** https://github.com/ra0037I/industrial-computing-project
**Technik:** Unity 6 (6000.6.0f1), URP, Vuforia Engine, Android, Google ML Kit

---

## 1. Ziel

Eine Android-AR-App für einen Schaltschrank-Verteiler:

1. **Schaltschrank erkennen.** Vuforia erkennt den realen Schrank über einen 3D-Scan (Model Target).
2. **Betriebsmittelkennzeichnungen (BMK) lesen.** Der Nutzer hält einen BMK-Sticker (z. B. `37T1`) in einen Rahmen in der Bildschirmmitte. Die App liest den Text per Texterkennung (OCR).
3. **Zusatzinfos anzeigen.** Zum erkannten BMK erscheinen Daten wie Typ, Funktion, Status und letzte Prüfung. Die Daten kommen vorerst aus einer Mock-Datenbank, später aus einem echten Webservice.

---

## 2. Was heute gemacht wurde

### 2.1 Vuforia Model Target aus dem 3D-Scan

- **Ausgangsdaten:** ein Photogrammetrie-Scan des Schaltschranks als FBX. Die Texturen sind in der Datei eingebettet.
- **Model Target Generator (MTG):**
  - Die FBX wurde importiert.
  - Einheiten/Maßstab, Up-Vector und Guide View wurden eingestellt.
  - Daraus wurde die Target-Datenbank generiert.
- **Unity-Einbindung:**
  1. Datenbank als `.unitypackage` importieren. Sie landet unter `Assets/StreamingAssets/Vuforia`.
  2. In der Szene das Objekt `GameObject → Vuforia Engine → Model Target` anlegen.
  3. Bei *Model Target Behaviour* die Datenbank und das Target auswählen.
- **Texturiertes Modell:** Die Model-Target-Vorschau in Unity ist **immer weiß**. Das ist normal, denn die Erkennung nutzt nur die Geometrie. Das texturierte Modell wurde deshalb zusätzlich als **OBJ** (mit `.mtl` und `.jpg`) importiert und als Kind-Objekt unter das Model Target gesetzt.
  - Position und Rotation stehen auf 0.
  - Die Skalierung ist deckungsgleich mit der Vorschau.

### 2.2 Plattformwechsel Windows → Android

- Über `File → Build Profiles → Android → Switch Platform` wurde auf Android umgestellt.
- Folgende Player Settings sind für Vuforia relevant:
  - Minimum API Level ≥ 24
  - Scripting Backend: IL2CPP
  - Target Architecture: ARM64
  - Graphics API: OpenGLES3 (nur nötig, falls Vulkan Probleme macht)

### 2.3 Behobene Fehler

| Fehler | Ursache | Lösung |
|---|---|---|
| `MissingReferenceException … Material has been destroyed` (Vuforia `VideoBackgroundTextureUpdater`) | Vuforia + URP im Editor; Material nach Plattformwechsel bzw. Play-Stop ungültig | Unity neu starten, *Reload Domain* aktivieren, ggf. Vuforia aktualisieren. Betrifft nur den Editor. |
| `CS0104: 'Image' is an ambiguous reference` | Sowohl `UnityEngine.UI.Image` als auch `Vuforia.Image` existieren | Alias `using Image = UnityEngine.UI.Image;` |
| `compileReleaseJavaWithJavac FAILED` – `package com.google.mlkit… does not exist` | ML-Kit-Abhängigkeit kam über `mainTemplate.gradle` nicht im Gradle-Projekt an | Editor-Skript `MlKitGradleDependency.cs` trägt die Abhängigkeit beim Build automatisch ein |
| `git push` rejected (fetch first) | GitHub-Repo wurde mit README angelegt; dadurch zwei getrennte Historien | `git pull origin main --allow-unrelated-histories` oder `git push --force` |

### 2.4 BMK-Erkennung per OCR

- Vuforia hat keine eigene Texterkennung mehr. Deshalb wird **Google ML Kit Text Recognition** verwendet. Es läuft auf dem Gerät und funktioniert offline.
- **Ablauf:**
  1. In der Bildschirmmitte wird ein Rahmen eingeblendet.
  2. Das Kamerabild wird aus Vuforia geholt (`CameraDevice.GetCameraImage`).
  3. Der Bereich unter dem Rahmen wird ausgeschnitten und aufrecht gedreht.
  4. Der Ausschnitt geht als JPEG an das Android-Plugin, ML Kit liefert den Text zurück.
  5. Der Text wird bereinigt und gegen die BMK-Liste geprüft. Typische Verwechslungen wie 1/I, 0/O oder 7/T werden korrigiert.
  6. Nach 2 gleichen Treffern hintereinander wird der Rahmen grün und ein Event wird ausgelöst.

### 2.5 Info-Panel mit Mock-Datenbank

- Zum erkannten BMK zeigt ein Panel am unteren Rand die Daten an.
- Ein farbiger Balken zeigt den Status:
  - **Grün:** OK
  - **Orange:** Wartung fällig oder prüfen
  - **Rot:** Störung oder BMK unbekannt
- Die Datenquelle ist austauschbar:
  - **Mock:** JSON-Datei im Projekt
  - **Rest:** `GET {URL}/{BMK}` liefert ein JSON im gleichen Format

### 2.6 BMKs aus dem Foto des Verteilers

Vom Foto des Verteilers wurden **63 BMKs** abgelesen. Daraus entstanden die Mockdaten und die Liste gültiger BMKs.

| Ebene | BMKs |
|---|---|
| 1 – LS-Schalter | 1F1–1F9, 5F1–5F6, 6F1–6F6, 7F1–7F6, 100B1 (Thermostat) |
| 2 – Motorschutzschalter | 10Q1, 11Q1–11Q3, 12Q1–12Q4 |
| 3 – Schütze | 30Q1–37Q1, 110Q1, 110Q2, 110Q4, 110Q5, 161Q3, 161Q4, 162Q1 |
| 4 – Frequenzumrichter (Lenze) | 30T1–37T1 |
| 6 – Einspeisung | 1Q1 (Hauptschalter), 2F1, 2F2, 1X1 (Steckdose) |

Für Tests sind folgende Status eingebaut:
- **37T1:** Störung
- **33T1** und **12Q3:** Wartung fällig
- **5F4:** Ausgelöst – prüfen

### 2.7 Git-Repository

- `.gitignore` schließt die erzeugten Ordner aus: `Library`, `Temp`, `Logs`, `UserSettings`, Builds sowie IDE-Dateien.
- `.gitattributes` legt große Binärdateien in **Git LFS** ab: FBX/OBJ, Texturen, Vuforia-`.dat`, `.unitypackage`, `.tgz`.
- Das Projekt wurde nach GitHub gepusht. Dabei gingen 36 LFS-Objekte mit ca. 149 MB hoch.

---

## 3. Erstellte Dateien

| Datei | Ablageort | Zweck |
|---|---|---|
| `BmkScanner.cs` | `Assets/Scripts/` | Rahmen-Overlay, Kamerabild ausschneiden/drehen, Aufruf der OCR, BMK-Prüfung (Regex/Liste), Bestätigung nach mehreren Treffern, Event `onBmkRecognized`, Debug-Vorschau |
| `BmkOcr.java` | `Assets/Plugins/Android/` | Android-Plugin: Texterkennung mit Google ML Kit, Rückgabe an Unity per `UnitySendMessage` |
| `MlKitGradleDependency.cs` | `Assets/Editor/` | Trägt beim Android-Build `com.google.mlkit:text-recognition:16.0.1` in die `build.gradle` von `unityLibrary` ein |
| `BmkInfoPanel.cs` | `Assets/Scripts/` | Datenmodell `BmkInfo`, Datenquellen (Mock/REST), Info-Panel-UI; verbindet sich automatisch mit dem Scanner |
| `bmk_mock.json` | `Assets/Data/` | Mock-Datenbank mit 63 Einträgen (Bezeichnung, Typ, Hersteller, Einbauort, Funktion, Status, Prüfdatum …) |
| `bmk_liste.txt` | `Assets/Data/` | Liste aller gültigen BMKs, eins pro Zeile; verbessert die OCR-Trefferquote |
| `PlayFromMainScene.cs` | `Assets/Editor/` | Play im Editor startet immer die erste Szene der Build-Liste (Main); umschaltbar über `Tools → Immer mit Main-Szene starten` |
| `.gitignore` | Projektwurzel | Schließt generierte Unity-Ordner und IDE-Dateien aus |
| `.gitattributes` | Projektwurzel | Zeilenenden für Textdateien, Git LFS für große Binärdateien |

---

## 4. Einstellungen in Unity

### Szene

- **AR Camera** (Vuforia) statt Main Camera
- **Model Target** mit der Scan-Datenbank; das texturierte OBJ liegt als Kind-Objekt darunter
- GameObject **BmkScanner** mit den Komponenten:
  - **BmkScanner**
    - *Valid Bmk List* = `bmk_liste.txt`
    - *Rotation* = Auto (bei Bedarf anpassen, Kontrolle über die Debug-Vorschau)
    - *Show Debug Preview* nach dem Einrichten ausschalten
    - *Editor Simulated Text* zum Testen im Editor, z. B. `37T1`
  - **BmkInfoPanel**
    - *Source* = Mock
    - *Mock Json* = `bmk_mock.json`

### Project Settings

| Bereich | Einstellung |
|---|---|
| Vuforia Configuration | App License Key eingetragen |
| Player → Android → Other Settings | Min. API Level ≥ 24, IL2CPP, ARM64 |
| Player → Android → Publishing Settings | *Custom Main Gradle Template* aktiv, ML-Kit-Zeile eingetragen (inzwischen durch `MlKitGradleDependency.cs` abgedeckt) |
| Editor → Enter Play Mode Settings | Reload Domain aktiv (gegen den Vuforia-Material-Fehler) |
| Editor → Version Control / Serialization | Visible Meta Files / Force Text |
| Build Profiles → Scene List | Main-Szene an erster Stelle |

---

## 5. Offene Punkte / nächste Schritte

- [ ] **BMK 100B1** (Thermostat) am echten Schrank prüfen; auf dem Foto war die Kennzeichnung nicht sicher lesbar.
- [ ] Prüfen, ob in der Reihe **110Q1–110Q5** tatsächlich **110Q3** fehlt.
- [ ] **Mockdaten durch echte Daten ersetzen**, z. B. aus Schaltplan oder EPLAN-Export; danach Webservice anbinden (*Source* = Rest).
- [ ] **Maßstab des Scans** mit dem realen Schrank abgleichen, falls die Model-Target-Erkennung unzuverlässig ist.
- [ ] OCR unter echten Bedingungen testen (Licht, Abstand, Blickwinkel) und *Scan Interval* bzw. *Required Matches* feinjustieren.
- [ ] Repository privat halten, weil der Vuforia-Lizenzschlüssel in `VuforiaConfiguration.asset` steht.
