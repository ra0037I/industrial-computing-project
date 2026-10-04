#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Play im Editor startet immer mit der ersten Szene der Build-Liste (z. B. "Main"),
/// egal welche Szene gerade geöffnet ist. Nach dem Stoppen bist du wieder in deiner Szene.
/// Ein-/Ausschalten über das Menü: Tools → Immer mit Main-Szene starten
/// Ablage: Assets/Editor/PlayFromMainScene.cs
/// </summary>
[InitializeOnLoad]
public static class PlayFromMainScene
{
    const string MenuPath = "Tools/Immer mit Main-Szene starten";
    const string PrefKey = "PlayFromMainScene.Enabled";

    static PlayFromMainScene()
    {
        EditorApplication.delayCall += Apply;
        EditorBuildSettings.sceneListChanged += Apply;
    }

    static bool Enabled
    {
        get => EditorPrefs.GetBool(PrefKey, true);
        set => EditorPrefs.SetBool(PrefKey, value);
    }

    [MenuItem(MenuPath)]
    static void Toggle()
    {
        Enabled = !Enabled;
        Apply();
    }

    [MenuItem(MenuPath, true)]
    static bool ToggleValidate()
    {
        Menu.SetChecked(MenuPath, Enabled);
        return true;
    }

    static void Apply()
    {
        if (!Enabled)
        {
            EditorSceneManager.playModeStartScene = null;
            return;
        }

        foreach (var s in EditorBuildSettings.scenes)
        {
            if (!s.enabled) continue;
            var scene = AssetDatabase.LoadAssetAtPath<SceneAsset>(s.path);
            if (scene != null)
            {
                EditorSceneManager.playModeStartScene = scene;
                return;
            }
        }

        EditorSceneManager.playModeStartScene = null;
        Debug.LogWarning("PlayFromMainScene: Keine aktive Szene in der Build-Liste gefunden.");
    }
}
#endif
