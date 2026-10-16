using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Vuforia;

/// <summary>
/// Vuforia kann immer nur EINEN Model-Target-Datensatz gleichzeitig aktiv haben
/// ("ModelTracker: Failed to activate data set because another one is already active").
/// Dieses Skript hält genau ein Model Target aktiv und wechselt automatisch zum nächsten,
/// sobald das aktive einige Sekunden nicht mehr im Bild (TRACKED) ist.
/// Auf ein beliebiges GameObject in der Szene legen (z. B. ARCamera).
/// Ablage: Assets/Scripts/ModelTargetSwitcher.cs
/// </summary>
[DefaultExecutionOrder(-1000)] // vor dem Start von Vuforia die übrigen Targets abschalten
public class ModelTargetSwitcher : MonoBehaviour
{
    [Tooltip("Model Targets, zwischen denen gewechselt wird. Leer = alle Model Targets der Szene.")]
    public List<ModelTargetBehaviour> targets = new List<ModelTargetBehaviour>();

    [Tooltip("Sekunden ohne TRACKED, bis zum nächsten Model Target gewechselt wird.")]
    public float switchIntervalSeconds = 4f;

    [Tooltip("Automatisch wechseln. Aus = nur über Select() / Next(), z. B. per Button.")]
    public bool autoSwitch = true;

    public ModelTargetBehaviour Current => targets.Count > 0 ? targets[index] : null;

    int index;
    float nextSwitchAt;

    void Awake()
    {
        if (targets.Count == 0)
            targets = FindObjectsByType<ModelTargetBehaviour>(FindObjectsInactive.Include)
                        .OrderBy(t => t.transform.GetSiblingIndex()).ToList();
        targets.RemoveAll(t => t == null);

        if (targets.Count < 2)
        {
            enabled = false; // nichts zu wechseln
            return;
        }

        // Nur das erste aktiv lassen
        for (int i = 0; i < targets.Count; i++) targets[i].enabled = i == 0;
        index = 0;
        nextSwitchAt = Time.time + switchIntervalSeconds;
        Debug.Log($"ModelTargetSwitcher: {targets.Count} Model Targets, aktiv: {Current.TargetName}");
    }

    void Update()
    {
        if (!autoSwitch || Current == null) return;

        // Nur echtes TRACKED zählt. EXTENDED_TRACKED bleibt nach einer Erkennung dauerhaft stehen
        // (Vuforia schätzt die Position über die Gerätebewegung weiter), auch wenn der Schrank
        // längst nicht mehr im Bild ist – damit würde nie gewechselt.
        if (Current.TargetStatus.Status == Status.TRACKED)
        {
            nextSwitchAt = Time.time + switchIntervalSeconds; // im Bild -> dabei bleiben
            return;
        }

        if (Time.time >= nextSwitchAt) Next();
    }

    /// <summary>Zum nächsten Model Target wechseln.</summary>
    public void Next() => Select((index + 1) % targets.Count);

    /// <summary>Bestimmtes Model Target aktivieren (Index in der Liste), z. B. für Buttons.</summary>
    public void Select(int i)
    {
        if (targets.Count == 0 || i < 0 || i >= targets.Count) return;
        if (i == index && targets[i].enabled) return;

        targets[index].enabled = false;   // erst den aktiven Datensatz deaktivieren ...
        index = i;
        targets[index].enabled = true;    // ... dann den neuen aktivieren
        nextSwitchAt = Time.time + switchIntervalSeconds;
        Debug.Log($"ModelTargetSwitcher: aktiv jetzt {Current.TargetName}");
    }

    /// <summary>Model Target über den Namen (z. B. "controlCabinet") aktivieren.</summary>
    public void Select(string targetName)
    {
        int i = targets.FindIndex(t => t.TargetName == targetName);
        if (i >= 0) Select(i);
    }
}
