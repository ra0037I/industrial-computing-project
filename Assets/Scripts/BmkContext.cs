using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

/// <summary>
/// Gibt den erkannten Schaltschrank an den BMK-Scanner weiter und lädt die passenden BMK-Daten.
///
/// Datenablage (per Namenskonvention, kein Inspector-Setup nötig):
///   Assets/Resources/BmkData/schraenke.txt                – Liste der Schränke: TargetName;Anzeigename
///   Assets/Resources/BmkData/{TargetName}/bmk_liste.txt   – gültige BMKs des Schranks, eins pro Zeile
///   Assets/Resources/BmkData/{TargetName}/bmk_mock.json   – Mock-Daten im Format von BmkInfoList
/// {TargetName} = Name des Vuforia Model Targets, z. B. mainsCabinet oder controlCabinet.
///
/// Ist kein Schrank gesetzt (BMK-Scanner direkt aus dem Menü), gelten die Daten ALLER Schränke.
/// Ablage: Assets/Scripts/BmkContext.cs
/// </summary>
public static class BmkContext
{
    public const string ResourcesRoot = "BmkData";
    const string IndexFile = "schraenke";

    /// <summary>Target-Name des erkannten Schranks oder null (= alle Schränke).</summary>
    public static string Cabinet { get; private set; }

    /// <summary>Anzeigename des erkannten Schranks, z. B. "Hauptverteiler".</summary>
    public static string CabinetDisplayName { get; private set; }

    static Dictionary<string, string> cabinets;     // TargetName -> Anzeigename (Reihenfolge aus schraenke.txt)
    static Dictionary<string, string> bmkToCabinet; // BMK -> TargetName

    public static void SetCabinet(string targetName, string displayName = null)
    {
        Cabinet = string.IsNullOrEmpty(targetName) ? null : targetName;
        CabinetDisplayName = Cabinet == null ? null
            : !string.IsNullOrEmpty(displayName) ? displayName : DisplayName(Cabinet);
    }

    public static void Clear() => SetCabinet(null);

    /// <summary>Alle Schränke aus schraenke.txt (TargetName -> Anzeigename).</summary>
    public static IReadOnlyDictionary<string, string> Cabinets
    {
        get
        {
            if (cabinets != null) return cabinets;
            cabinets = new Dictionary<string, string>();
            var index = Resources.Load<TextAsset>($"{ResourcesRoot}/{IndexFile}");
            if (index == null) { Debug.LogWarning($"BmkContext: {ResourcesRoot}/{IndexFile}.txt fehlt."); return cabinets; }
            foreach (var raw in index.text.Split('\n'))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#")) continue;
                var parts = line.Split(';');
                string target = parts[0].Trim();
                cabinets[target] = parts.Length > 1 && parts[1].Trim().Length > 0 ? parts[1].Trim() : target;
            }
            return cabinets;
        }
    }

    /// <summary>Anzeigename zu einem TargetName (Fallback: der TargetName selbst).</summary>
    public static string DisplayName(string targetName) =>
        targetName != null && Cabinets.TryGetValue(targetName, out var n) ? n : targetName;

    /// <summary>Datei (ohne Endung, z. B. "bmk_mock") des aktuellen Schranks bzw. aller Schränke.</summary>
    public static TextAsset[] Load(string fileName)
    {
        if (Cabinet != null)
        {
            var asset = LoadFor(Cabinet, fileName);
            if (asset != null) return new[] { asset };
            Debug.LogWarning($"BmkContext: Keine Datei {ResourcesRoot}/{Cabinet}/{fileName} – verwende alle Schränke.");
        }
        return LoadAll(fileName);
    }

    /// <summary>Datei aller Schränke (unabhängig vom aktuellen Schrank).</summary>
    public static TextAsset[] LoadAll(string fileName) =>
        Cabinets.Keys.Select(c => LoadFor(c, fileName)).Where(a => a != null).ToArray();

    static TextAsset LoadFor(string cabinet, string fileName) =>
        Resources.Load<TextAsset>($"{ResourcesRoot}/{cabinet}/{fileName}");

    /// <summary>
    /// In welchem Schrank liegt das BMK (laut bmk_liste.txt)? null = in keiner Liste.
    /// </summary>
    public static string CabinetOf(string bmk)
    {
        if (bmkToCabinet == null)
        {
            bmkToCabinet = new Dictionary<string, string>();
            foreach (var cab in Cabinets.Keys)
            {
                var list = LoadFor(cab, "bmk_liste");
                if (list == null) continue;
                foreach (var line in list.text.Split('\n'))
                {
                    string c = Normalize(line);
                    if (c.Length > 0 && !bmkToCabinet.ContainsKey(c)) bmkToCabinet[c] = cab;
                }
            }
        }
        return bmkToCabinet.TryGetValue(Normalize(bmk), out var result) ? result : null;
    }

    /// <summary>Anzahl der BMKs in der bmk_liste.txt eines Schranks (0 = keine Liste).</summary>
    public static int BmkCount(string targetName)
    {
        if (targetName == null) return 0;
        CabinetOf(""); // Zuordnungstabelle aufbauen
        return bmkToCabinet.Values.Count(c => c == targetName);
    }

    /// <summary>Hat der aktuelle Schrank eine BMK-Liste? (nur dann ist die Zuordnungsprüfung sinnvoll)</summary>
    public static bool CurrentCabinetHasList => Cabinet != null && LoadFor(Cabinet, "bmk_liste") != null;

    /// <summary>Nur Großbuchstaben und Ziffern, wie im BmkScanner.</summary>
    public static string Normalize(string s)
    {
        var sb = new StringBuilder();
        foreach (char ch in (s ?? "").ToUpperInvariant())
            if ((ch >= 'A' && ch <= 'Z') || (ch >= '0' && ch <= '9')) sb.Append(ch);
        return sb.ToString();
    }

    // Bei jedem Play-Start im Editor zurücksetzen
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetOnPlay()
    {
        Clear();
        cabinets = null;
        bmkToCabinet = null;
    }
}
