# Wartungsassistent – AR-App zur Schaltschrank- und BMK-Erkennung

Projektarbeit im Kurs **Industrial Computing** (MCI, Master) von:
- **Andreas Rofner**
- **Rene Stockinger**

Die Android-App unterstützt Wartungspersonal am Schaltschrank:

1. **Schaltschrank erkennen** – Vuforia erkennt den realen Schrank anhand eines 3D-Scans (Model Target).
2. **Betriebsmittelkennzeichnung (BMK) lesen** – ein BMK-Schild (z. B. `35T1`) wird in einen Rahmen gehalten und per Texterkennung gelesen.
3. **Informationen anzeigen** – zum BMK erscheinen Typ, Hersteller, Funktion, Status und letzte Prüfung.

Hinterlegt sind zwei Schränke: **Hauptverteiler** (`mainsCabinet`, 63 BMKs) und **Steuerschrank** (`controlCabinet`, 11 BMKs).

---

## Inhalt

1. [Voraussetzungen](#1-voraussetzungen)
2. [Projekt herunterladen](#2-projekt-herunterladen)
3. [Projekt in Unity öffnen](#3-projekt-in-unity-öffnen)
4. [Vuforia-Lizenz eintragen (Pflicht)](#4-vuforia-lizenz-eintragen-pflicht)
5. [Testen am PC](#5-testen-am-pc)
6. [Testen am Android-Smartphone](#6-testen-am-android-smartphone)
7. [Aufbau der App](#7-aufbau-der-app)
8. [Projektstruktur](#8-projektstruktur)
9. [Bekannte Einschränkungen](#9-bekannte-einschränkungen)

---

## 1. Voraussetzungen

| | Benötigt für |
|---|---|
| **Unity 6000.6.0f1** (über Unity Hub) | Öffnen und Testen des Projekts |
| **Git** und **Git LFS** | Herunterladen (Modelle, Videos und das Vuforia-Paket liegen in Git LFS) |
| **Vuforia-Developer-Account** (kostenlos) | Lizenzschlüssel, siehe [Abschnitt 4](#4-vuforia-lizenz-eintragen-pflicht) |
| Unity-Modul **Android Build Support** | nur für den Test am Smartphone |
| Android-Smartphone mit ARCore-Unterstützung | nur für den Test am Smartphone |

Eine Webcam ist **nicht** nötig – am PC wird mit aufgezeichneten Vuforia-Videos getestet.

---

## 2. Projekt herunterladen

```bash
git lfs install
git clone https://github.com/ra0037I/industrial-computing-project.git
```

---

## 3. Projekt in Unity öffnen

1. **Unity Hub → Add → Add project from disk** und den Ordner `industrial-computing-project` wählen.
2. Mit Editor-Version **6000.6.0f1** öffnen.
3. Der erste Import dauert einige Minuten. Das Vuforia-Paket wird dabei automatisch aus `Packages/com.ptc.vuforia.engine-11.4.4.tgz` installiert.
4. Falls Unity beim ersten Start nach der Vuforia-Lizenzvereinbarung (EULA) fragt: akzeptieren.

---

## 4. Vuforia-Lizenz eintragen (Pflicht)

Ohne gültigen Lizenzschlüssel lädt Vuforia die Schaltschrank-Datensätze nicht – das Bild bleibt schwarz bzw. es wird nichts erkannt.

**Schlüssel erzeugen:**

1. Auf <https://developer.vuforia.com> anmelden (Registrierung ist kostenlos).
2. **Plan & Licenses → Generate Basic License**, beliebigen Namen vergeben, bestätigen.
3. Die erzeugte Lizenz öffnen und den **License Key** kopieren (lange Zeichenkette).

**Schlüssel in Unity eintragen:**

1. Menü **Window → Vuforia Configuration** öffnen
   (alternativ im Project-Fenster `Assets/Resources/VuforiaConfiguration.asset` auswählen).
2. Im Inspector bei **App License Key** den Schlüssel einfügen.
3. Speichern (**Strg+S**).

---

## 5. Testen am PC

Im Editor wird die Smartphone-Kamera durch **Vuforia-Aufnahmen** ersetzt (Videos mit Kamera- und Bewegungsdaten). Diese wurden am Smartphone mit dem **Vuforia Aufnahme Assistent** der App am echten Schaltschrank aufgenommen. Das ist bereits fertig eingerichtet:

- *Vuforia Configuration → Play Mode* = **Recording**
- Jede AR-Szene spielt über die Komponente `SceneRecording` ihr eigenes Video ab:
  - `CabinetScanner` → `Assets/Videos/cabinet.mp4`
  - `BmkScanner` → `Assets/Videos/bmkScanner.mp4`
- **Play startet immer im Hauptmenü** (`Main`), egal welche Szene gerade geöffnet ist.
  Umschaltbar über **Tools → Immer mit Main-Szene starten**.

### 5.1 Schaltschrank-Erkennung

1. **Play** drücken. Das Hauptmenü erscheint (ohne Kamerabild).
2. **Schaltschrank Erkennung** wählen. Das Video `cabinet.mp4` startet.
3. Sobald der **Hauptverteiler** im Bild ist, erscheint unten ein Popup
   *„Schaltschrank erkannt – Hauptverteiler, 63 Betriebsmittel hinterlegt …“*.
4. Im weiteren Verlauf schwenkt das Video zum **Steuerschrank**. Die App wechselt automatisch das aktive Model Target (Vuforia kann nur eines gleichzeitig suchen. Siehe auch [Einschränkungen](#9-bekannte-einschränkungen)) und zeigt bei Erkennung *„Steuerschrank, 11 Betriebsmittel hinterlegt“*.
5. **Zur BMK-Erkennung** im Popup öffnet den Betriebsmittel-Scanner **für genau diesen Schrank**.

### 5.2 BMK-Erkennung

Die Texterkennung (Google ML Kit) läuft nur auf Android. **Im Editor wird das Leseergebnis simuliert:**

1. In die BMK-Erkennung wechseln (über das Popup aus 5.1 oder im Hauptmenü über **Betriebsmittel Scanner**).
2. In der Hierarchy das GameObject **BmkScanner** auswählen.
3. In der Komponente *Bmk Scanner* das Feld **Editor Simulated Text** setzen – das geht auch **während Play**. Der Wert wird wie ein gelesenes Schild behandelt.

Der Rahmen wird grün und unten erscheint das Info-Panel. Vorschläge zum Ausprobieren:

| BMK | Schrank | Ergebnis |
|---|---|---|
| `1F1` | Hauptverteiler | In Betrieb – OK (grün) |
| `33T1` | Hauptverteiler | Wartung fällig (orange) |
| `5F4` | Hauptverteiler | Ausgelöst – prüfen (orange) |
| `37T1` | Hauptverteiler | Störung (rot) |
| `65T1` | Steuerschrank | Wartung fällig (orange) |
| `163Q4` | Steuerschrank | Störung (rot) |

BMKs, die in keiner BMK-Liste stehen, werden nicht angenommen – der Rahmen bleibt weiß. Das verhindert Fehltreffer der Texterkennung.

**Schrank-Zuordnung testen:** Über *Schaltschrank Erkennung* den **Hauptverteiler** erkennen, ins Popup **Zur BMK-Erkennung** und dann `163Q4` eingeben. Es erscheint ein oranger Hinweis
*„163Q4 · nicht in diesem Schrank – gehört zum Steuerschrank“*.
Über das Hauptmenü (ohne gewählten Schrank) werden dagegen die BMKs aller Schränke akzeptiert.

### 5.3 Navigation

- Der Button **Zurück** führt jeweils zur Szene, aus der man gekommen ist (z. B. BMK-Erkennung → Schrank-Erkennung → Hauptmenü).
- **Vuforia Aufnahme Assistent** öffnet den Vuforia Session Recorder. Mit ihm wurden am Smartphone die Aufnahmen `cabinet.mp4` und `bmkScanner.mp4` erstellt, die beim Test am PC als Recording im Hintergrund laufen (siehe Abschnitt 5). Auf die gleiche Weise lassen sich Aufnahmen für neue Schränke erstellen. Er nimmt nur am Smartphone auf und ist im Editor nicht sinnvoll testbar.

---

## 6. Testen am Android-Smartphone

1. **File → Build Profiles → Android → Switch Platform**.
2. Smartphone per USB verbinden (USB-Debugging aktiv) und **Build And Run** wählen.
3. Beim ersten Start die Kamera-Berechtigung erlauben.

Am Smartphone wird immer die echte Kamera verwendet (die Videos gelten nur im Editor). Für die **Schrank-Erkennung** wird der reale Schaltschrank benötigt.

**BMK-Erkennung ohne echten Schrank:** Dafür können die Fotos aus dem Ordner `Pictures/` verwendet werden:

1. `Pictures/mainsCabinett.jpg` (Hauptverteiler) oder `Pictures/controlCabinett.jpg` (Steuerschrank) am PC-Bildschirm öffnen.
2. **Stark in das Foto hineinzoomen**, bis ein einzelnes gelbes BMK-Schild gut lesbar ist – in der Gesamtansicht sind die Schilder zu klein für die Texterkennung.
3. In der App **Betriebsmittel Scanner** öffnen und das Schild mit dem Smartphone in den Rahmen halten.
4. Nach kurzer Zeit wird der Rahmen grün und die Daten zum BMK erscheinen.

---

## 7. Aufbau der App

```
Hauptmenü „Wartungsassistent“
 ├─ Schaltschrank Erkennung ──(Schrank erkannt → Popup)──► Betriebsmittel Scanner (nur BMKs dieses Schranks)
 ├─ Betriebsmittel Scanner (BMKs aller Schränke)
 └─ Vuforia Aufnahme Assistent (Vuforia-Aufnahmen für den Test am PC erstellen)
```

| Szene | Inhalt |
|---|---|
| `Main` | Hauptmenü, kein Kamerabild |
| `CabinetScanner` | Model Targets beider Schränke, automatischer Wechsel, Popup |
| `BmkScanner` | Rahmen, Texterkennung, Info-Panel |
| `SessionRecorder` | Vuforia Session Recorder – damit wurden die Recordings in `Assets/Videos/` aufgenommen |

**Ablauf der BMK-Erkennung:** Kamerabild von Vuforia → Ausschnitt unter dem Rahmen → Google ML Kit (on-device, offline) → Text bereinigen und gegen die BMK-Listen prüfen (typische Verwechslungen wie 1/I, 0/O, 7/T werden korrigiert) → nach 2 gleichen Treffern Anzeige der Daten.

**Datenquelle:** aktuell Mock-Dateien (JSON). Im Info-Panel kann auf einen Webservice umgestellt werden (*Source* = Rest, Abfrage `GET {URL}/{BMK}?schrank={Schrank}`).

---

## 8. Projektstruktur

| Pfad | Inhalt |
|---|---|
| `Assets/Scenes/` | die vier Szenen |
| `Assets/Scripts/` | Programmlogik (siehe unten) |
| `Assets/Editor/` | Editor-Hilfen: Play startet in Main, Video pro Szene, ML-Kit-Abhängigkeit für den Android-Build |
| `Assets/Plugins/Android/BmkOcr.java` | Android-Plugin für die Texterkennung mit Google ML Kit |
| `Assets/StreamingAssets/Vuforia/` | Vuforia-Datensätze der beiden Schränke |
| `Assets/Resources/mainsCabinet/`, `controlCabinet/` | texturierte 3D-Scans (OBJ) |
| `Assets/Resources/BmkData/` | BMK-Listen und Mockdaten pro Schrank |
| `Assets/Videos/` | Vuforia-Aufnahmen für den Test am PC |
| `Pictures/` | Fotos der Schränke, die für Punkt 6 verwendet werden können |

| Skript | Aufgabe |
|---|---|
| `ChangeScene` | Szenenwechsel mit Verlauf (Zurück-Button) |
| `ModelTargetSwitcher` | hält genau ein Model Target aktiv und wechselt, solange keines erkannt wird |
| `CabinetFoundPopup` | Popup bei erkanntem Schrank, Übergabe des Schranks an die BMK-Erkennung |
| `BmkScanner` | Rahmen, Bildausschnitt, Texterkennung, Abgleich mit den BMK-Listen |
| `BmkInfoPanel` | Anzeige der Daten, Statusfarben, Hinweis „nicht in diesem Schrank“ |
| `BmkContext` | gewählter Schrank, Laden der Daten, Zuordnung BMK zum Schrank |
| `SceneRecording` | Vuforia-Video pro Szene im Editor |

### Daten pro Schrank

```
Assets/Resources/BmkData/
  schraenke.txt                  TargetName;Anzeigename
  mainsCabinet/   bmk_liste.txt  bmk_mock.json
  controlCabinet/ bmk_liste.txt  bmk_mock.json
```

**Neuen Schrank hinzufügen:** Model Target in `CabinetScanner` anlegen, eine Zeile in `schraenke.txt` ergänzen und einen Ordner mit `bmk_liste.txt` und `bmk_mock.json` anlegen. Popup-Text, Schrank-Zuordnung und Datenquelle passen sich automatisch an.

Die BMKs wurden von den Fotos in `Pictures/` abgelesen. Gerätetypen entsprechen den Fotos; Funktionen, Artikelnummern und Status sind Beispieldaten.

---

## 9. Bekannte Einschränkungen

- **Texterkennung nur auf Android.** Im Editor wird sie über *Editor Simulated Text* simuliert (siehe 5.2).
- **Ein Model Target zur Zeit.** Vuforia kann nur einen Model-Target-Datensatz gleichzeitig aktiv haben. Die App wechselt deshalb alle 2 Sekunden zwischen den Schränken, solange keiner erkannt wird (Skript `ModelTargetSwitcher`). Die Erkennung des zweiten Schranks kann dadurch kurz verzögert sein.
- **Mockdaten:** Die Infos zu den Betriebsmitteln sind Beispieldaten, eine Anbindung an echte Daten ist vorbereitet.
