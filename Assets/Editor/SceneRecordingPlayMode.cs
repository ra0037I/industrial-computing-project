#if UNITY_EDITOR
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using Vuforia;

/// <summary>
/// Setzt vor dem Play-Start das Video der Start-Szene (aus deren SceneRecording-Komponente),
/// damit Vuforia direkt damit initialisiert. Nach dem Stoppen wird der ursprüngliche Pfad
/// wiederhergestellt, sodass VuforiaConfiguration.asset unverändert bleibt.
/// Szenenwechsel während Play übernimmt SceneRecording selbst.
/// Ablage: Assets/Editor/SceneRecordingPlayMode.cs
/// </summary>
[InitializeOnLoad]
public static class SceneRecordingPlayMode
{
    const string OriginalPathKey = "SceneRecordingPlayMode.OriginalPath";
    static readonly Regex VideoPathRegex = new Regex(@"^\s*videoPath:\s*(\S+)\s*$", RegexOptions.Multiline);

    static SceneRecordingPlayMode()
    {
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingEditMode)
            ApplyStartSceneVideo();
        else if (state == PlayModeStateChange.EnteredEditMode)
            RestoreOriginalPath();
    }

    static void ApplyStartSceneVideo()
    {
        if (VuforiaConfiguration.Instance.PlayMode.PlayModeType != PlayModeType.RECORDING)
            return;

        // Immer merken, da auch Szenenwechsel den Pfad ändern können.
        // SessionState überlebt den Domain Reload beim Play-Start
        SessionState.SetString(OriginalPathKey, VuforiaConfiguration.Instance.PlayMode.SequencePath);

        var startScene = EditorSceneManager.playModeStartScene;
        string scenePath = startScene != null ? AssetDatabase.GetAssetPath(startScene) : EditorSceneManager.GetActiveScene().path;
        if (string.IsNullOrEmpty(scenePath) || !File.Exists(scenePath))
            return;

        var match = VideoPathRegex.Match(File.ReadAllText(scenePath));
        if (match.Success)
            VuforiaConfiguration.Instance.PlayMode.SequencePath = match.Groups[1].Value;
    }

    static void RestoreOriginalPath()
    {
        string original = SessionState.GetString(OriginalPathKey, null);
        if (original == null)
            return;

        VuforiaConfiguration.Instance.PlayMode.SequencePath = original;
        SessionState.EraseString(OriginalPathKey);
    }
}
#endif
