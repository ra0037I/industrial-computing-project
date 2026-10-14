using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Video;
using Vuforia;

/// <summary>
/// Legt pro Szene fest, welches Video Vuforia im Editor (Play Mode "Recording") abspielt.
/// Einfach in jeder Szene auf ein GameObject legen und das Video zuweisen.
/// Die Start-Szene wird schon vor Play von SceneRecordingPlayMode (Editor) gesetzt,
/// diese Komponente wechselt das Video bei Szenenwechseln während Play.
/// </summary>
[DefaultExecutionOrder(-10000)]
public class SceneRecording : MonoBehaviour
{
    [Tooltip("Video (mp4), das in dieser Szene im Editor abgespielt wird.")]
    public VideoClip video;

    [SerializeField, HideInInspector] string videoPath;

#if UNITY_EDITOR
    void OnValidate()
    {
        videoPath = video != null ? UnityEditor.AssetDatabase.GetAssetPath(video) : "";
    }
#endif

    void Awake()
    {
        if (!Application.isEditor || !VuforiaRuntimeUtilities.IsRecordingPlayMode() || string.IsNullOrEmpty(videoPath))
            return;

        var playMode = VuforiaConfiguration.Instance.PlayMode;
        if (playMode.SequencePath == videoPath)
        {
            // Nach dem Neuladen (siehe unten) ist Vuforia noch deinitialisiert
            if (!VuforiaApplication.Instance.IsInitialized)
                VuforiaApplication.Instance.Initialize();
            return;
        }

        // Vuforia lädt das Video nur bei der Initialisierung. Deinit + Init im selben Frame
        // lässt den Kamera-Start scheitern, daher wie im Vuforia-Sample (EditorRecordingPlayer):
        // Deinit, Pfad setzen, Szene neu laden.
        if (VuforiaApplication.Instance.IsInitialized)
            VuforiaApplication.Instance.Deinit();
        playMode.SequencePath = videoPath;

        // VuforiaBehaviour dieser (verworfenen) Szene nicht mehr starten lassen
        var vuforiaBehaviour = FindAnyObjectByType<VuforiaBehaviour>();
        if (vuforiaBehaviour != null)
            vuforiaBehaviour.gameObject.SetActive(false);

        SceneManager.LoadScene(gameObject.scene.path);
    }
}
