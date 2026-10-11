using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Szenenwechsel mit Verlauf: MoveToScene merkt sich die aktuelle Szene,
/// GoBack kehrt zur Szene zurück, von der man gekommen ist
/// (z. B. BMK-Scanner -> Main oder BMK-Scanner -> CabinetScanner).
/// </summary>
public class ChangeScene : MonoBehaviour
{
    [Tooltip("Ziel für GoBack, wenn es keinen Verlauf gibt (z. B. Szene direkt im Editor gestartet).")]
    public string fallbackScene = "Main";

    static readonly Stack<string> history = new Stack<string>();

    /// <summary>Wechselt in die angegebene Szene und merkt sich die aktuelle.</summary>
    public void MoveToScene(string sceneName) => Load(sceneName);

    /// <summary>Zurück zur vorherigen Szene (ohne Parameter, für Back-Buttons).</summary>
    public void GoBack()
    {
        string target = history.Count > 0 ? history.Pop() : fallbackScene;
        SceneManager.LoadScene(target);
    }

    /// <summary>Statische Variante, z. B. für Skripte wie CabinetFoundPopup.</summary>
    public static void Load(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName)) { Debug.LogWarning("ChangeScene: Kein Szenenname angegeben."); return; }
        string current = SceneManager.GetActiveScene().name;
        if (current != sceneName) history.Push(current);
        SceneManager.LoadScene(sceneName);
    }

    // Verlauf bei jedem Play-Start im Editor leeren (statische Felder überleben sonst ohne Domain Reload)
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetHistory() => history.Clear();
}
