# Flugeffekte für School of Dragons

Ein BepInEx-Mod für **School of Dragons** (Spielversion 3.31.0 über den **Project-Edge**-Launcher).

Beim Fliegen auf dem eigenen Drachen ziehen Streifen und Partikel von den Flügelspitzen weg, wahlweise auch vom Schwanz.

- Es gibt sechs Effekt-Arten: Kondensstreifen, Regenbogen, Glitzer, Feuer, Sternenstaub und Herzen.
- Farben sind frei wählbar.
- Ein Editor im Spiel (Taste **F7**) stellt alles ein.
- Einstellungen lassen sich als Vorlagen speichern.

*English: a BepInEx 5 mod for School of Dragons 3.31.0 (Project Edge). It adds customizable wing-tip trails and particles while you fly your dragon, with an in-game editor on F7.*

## Installieren

1. Spiel und Launcher schließen.
2. `FlightEffects_0.1.1.spf` von den [Releases](https://github.com/patstar374/sod-flight-effects/releases/latest) herunterladen und in den Ordner `payloads` des Project-Edge-Launchers kopieren.
3. Den Launcher starten. Er erkennt das neue Paket und öffnet von selbst den **„Payload manager“**.
   - Falls nicht: beim Start **Shift** halten, dann **„More options...“** und **„Open payload manager“**.
4. In der Liste „Payloads“ auf **„Flight Effects“** klicken und mit **„Ok“** bestätigen. Der Launcher baut den Spiel-Client dabei einmal neu auf und startet das Spiel.
5. Im Spiel auf den Drachen steigen und losfliegen.
6. **F7** öffnet den Editor (am Laptop eventuell Fn + F7).
   - Vorlagen werden automatisch benannt, z. B. „Herzen 1“.
   - Alles wird gespeichert, auch über Updates hinweg, in `%APPDATA%\FlightEffects\presets.txt`.

Zum Ausschalten im Editor „Effekte an“ abhaken. Zum Entfernen im Payload manager „Flight Effects“ ausschalten und „Ok“ klicken.

## Was der Code macht – und was nicht

**Das macht der Mod:**

- Er läuft nur im eigenen Spiel. Andere Spieler sehen die Effekte nicht.
- Er hat keinen Netzwerkzugriff.
- Er verändert weder Spieldateien noch den Spielstand.
- Er hat keine Harmony-Patches und startet keine Hintergrundprozesse.

**Das liest er aus dem Spiel:**

- Welcher Drache gerade aktiv ist: `SanctuaryManager.pCurPetInstance`.
- Ob der Spieler im Sattel sitzt und fliegt: `SanctuaryPet.pIsMounted`, `AvAvatar.IsFlying()`.
- Per Reflection zwei private Felder des Drachens: `mIsAccelerating` für den Modus „nur beim Beschleunigen“ und `mRendererMap` (die eigenen Modelle des Drachens).

**So entstehen die Effekte:**

- An die Flügelspitzen-Knochen hängt er kleine Hilfsobjekte mit `TrailRenderer` und `ParticleSystem`.
- Die Flügelspitzen findet er im Skelett des jeweiligen Drachens. Das klappt bei jeder Drachenart.
- Texturen wie Streifen, Stern und Herz werden im Code erzeugt. Die Shader stammen aus dem Spiel.

**Solange die Maus über dem Editor-Fenster ist**, sperrt er Klicks fürs Spiel:

- Das Spiel-HUD (NGUI) pausiert über `UICamera.ignoreAllEvents`.
- Für die Spielprüfung „Maus über UI“ (`KAUI.mGlobalMouseOverItem`) setzt er ein verstecktes Platzhalter-Element.
- Avatar und Kamera reagieren dadurch nicht auf Klicks im Fenster.
- Beides wird zurückgesetzt, sobald die Maus das Fenster verlässt oder der Editor zugeht.
- Es gibt keine Texteingabe, weil das Spiel Tasten direkt liest. Vorlagen werden automatisch benannt.

**Diese Dateien schreibt er:**

- `BepInEx/config/de.httyd.flighteffects.cfg` im Spiel-Client, für Taste und An/Aus.
- `%APPDATA%\FlightEffects\presets.txt` für Einstellungen und Vorlagen, als lesbarer Text, die vorige Fassung als `.bak`.
  - Die Datei liegt außerhalb des Clients, weil der Launcher den Client bei jedem Mod-Update neu aufbaut.

## Aufbau

| Ordner | Inhalt |
|---|---|
| `FlightEffects/src/Core/` | Logik ohne Unity: Farben, Einstellungen, Aussehen je Effekt, Vorlagen-Datei, Suche der Flügel- und Schwanzspitzen, weiches Ein-/Ausblenden, Texturpixel |
| `FlightEffects/src/Unity/` | Anbindung ans Spiel: Streifen und Partikel (`FxRig`), Steuerung (`FlightFxController`), Editor (`EditorWindow`), Shader und Texturen, Zugriff auf Spielwerte (`GameAccess`) |
| `FlightEffects/src/Plugin.cs` | BepInEx-Einstieg und Config |
| `FlightEffects.Tests/` | xUnit-Tests für die Logik |
| `tools/package_spf.py` | baut das `.spf`-Paket: ein ZIP mit `payloadinfo` und `BepInEx/plugins/FlightEffects/FlightEffects.dll` |

## Selbst bauen

**Du brauchst:**

- das .NET SDK 10
- den Spiel-Client 3.31.0 mit BepInEx 5.4.21 (vom Project-Edge-Launcher installiert)

**Schritte:**

```
dotnet build FlightEffects -c Release -p:GameDir="<…>\launcher\clients\client-3.31.0"
dotnet test FlightEffects.Tests
python tools/package_spf.py FlightEffects/bin/Release/net48/FlightEffects.dll
```

- Der Build legt die DLL unter `FlightEffects/bin/Release/net48/FlightEffects.dll` ab. Er verweist auf die DLLs des Spiel-Clients, kopiert sie aber nicht mit.
- Die Tests brauchen das Spiel nicht.
