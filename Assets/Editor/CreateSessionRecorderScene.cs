#if UNITY_EDITOR
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Menü: Tools → Session-Recorder-Szene erstellen
///
/// Legt die Szene Assets/Scenes/SessionRecorder.unity an mit
///  - AR Camera (Vuforia)
///  - Vuforia Session Recorder (fertige UI mit Aufnehmen / Stopp / Teilen)
///  - Back-Button, 1:1 aus der BmkScanner-Szene übernommen (ChangeScene.GoBack)
/// trägt sie in die Build-Liste ein und ergänzt im Menü (Main) einen Button
/// "Session aufnehmen".
/// Ablage: Assets/Editor/CreateSessionRecorderScene.cs
/// </summary>
public static class CreateSessionRecorderScene
{
    const string SceneName = "SessionRecorder";
    const string RecorderScene = "Assets/Scenes/" + SceneName + ".unity";
    const string BmkScene = "Assets/Scenes/BmkScanner.unity";
    const string MainScene = "Assets/Scenes/Main.unity";
    const string MenuButtonText = "Session\naufnehmen";

    [MenuItem("Tools/Session-Recorder-Szene erstellen")]
    static void Create()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        if (File.Exists(RecorderScene) &&
            !EditorUtility.DisplayDialog("Session Recorder", RecorderScene + " existiert bereits. Überschreiben?", "Überschreiben", "Abbrechen"))
            return;
        if (!File.Exists(BmkScene)) { Debug.LogError("Szene nicht gefunden: " + BmkScene); return; }

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // 1) AR Camera und Session Recorder über die Vuforia-Menüeinträge anlegen
        Selection.activeObject = null;
        if (!EditorApplication.ExecuteMenuItem("GameObject/Vuforia Engine/AR Camera")) { Fail("AR Camera"); return; }
        var arCamera = Object.FindFirstObjectByType<Vuforia.VuforiaBehaviour>()?.gameObject;
        if (arCamera == null) { Fail("AR Camera"); return; }

        Selection.activeObject = null;
        if (!EditorApplication.ExecuteMenuItem("GameObject/Vuforia Engine/Session Recorder")) { Fail("Session Recorder"); return; }
        Selection.activeObject = null;

        // 2) ChangeScene auf die ARCamera (wie im BmkScanner)
        var changeScene = arCamera.AddComponent<ChangeScene>();

        // 3) Canvas mit Back-Button und EventSystem aus BmkScanner kopieren
        var bmk = EditorSceneManager.OpenScene(BmkScene, OpenSceneMode.Additive);
        var roots = bmk.GetRootGameObjects();
        var srcCanvas = roots.FirstOrDefault(g => g.name == "Canvas");
        var srcEvents = roots.FirstOrDefault(g => g.name == "EventSystem");
        if (srcCanvas == null || srcCanvas.transform.Find("Button") == null)
        {
            EditorSceneManager.CloseScene(bmk, true);
            Debug.LogError("Im BmkScanner wurde kein Canvas/Button gefunden.");
            return;
        }

        var canvas = Object.Instantiate(srcCanvas);
        canvas.name = "Canvas";
        SceneManager.MoveGameObjectToScene(canvas, scene);
        foreach (var child in canvas.transform.Cast<Transform>().ToList())
            if (child.name != "Button") Object.DestroyImmediate(child.gameObject);   // nur den Back-Button behalten

        var back = canvas.transform.Find("Button").GetComponent<Button>();
        while (back.onClick.GetPersistentEventCount() > 0) UnityEventTools.RemovePersistentListener(back.onClick, 0);
        UnityEventTools.AddVoidPersistentListener(back.onClick, changeScene.GoBack);

        if (srcEvents != null)
        {
            var events = Object.Instantiate(srcEvents);
            events.name = "EventSystem";
            SceneManager.MoveGameObjectToScene(events, scene);
        }
        EditorSceneManager.CloseScene(bmk, true);

        // 4) Speichern, Build-Liste, Menü-Button
        EditorSceneManager.SaveScene(scene, RecorderScene);
        AddToBuildSettings(RecorderScene);
        AddMenuButton();

        EditorSceneManager.OpenScene(RecorderScene);
        Debug.Log("Session-Recorder-Szene erstellt: " + RecorderScene);
    }

    static void AddToBuildSettings(string path)
    {
        var list = EditorBuildSettings.scenes.ToList();
        if (list.Any(s => s.path == path)) return;
        list.Add(new EditorBuildSettingsScene(path, true));
        EditorBuildSettings.scenes = list.ToArray();
    }

    static void AddMenuButton()
    {
        var main = EditorSceneManager.OpenScene(MainScene, OpenSceneMode.Additive);
        var buttons = main.GetRootGameObjects()
            .SelectMany(g => g.GetComponentsInChildren<Button>(true))
            .Where(b => b.onClick.GetPersistentEventCount() > 0 && b.onClick.GetPersistentTarget(0) is ChangeScene)
            .ToList();

        bool exists = buttons.Any(b => Enumerable.Range(0, b.onClick.GetPersistentEventCount())
            .Any(i => b.onClick.GetPersistentMethodName(i) == "MoveToScene" && GetStringArg(b, i) == SceneName));

        if (!exists && buttons.Count > 0)
        {
            // unterhalb des untersten Buttons im gleichen Abstand einfügen
            var sorted = buttons.OrderBy(b => ((RectTransform)b.transform).anchoredPosition.y).ToList();
            var template = sorted[0];
            float step = sorted.Count > 1
                ? ((RectTransform)sorted[1].transform).anchoredPosition.y - ((RectTransform)sorted[0].transform).anchoredPosition.y
                : ((RectTransform)template.transform).sizeDelta.y + 40f;

            var copy = Object.Instantiate(template.gameObject, template.transform.parent);
            copy.name = "Button " + SceneName;
            var rt = (RectTransform)copy.transform;
            rt.anchoredPosition = ((RectTransform)template.transform).anchoredPosition - new Vector2(0, step);

            var btn = copy.GetComponent<Button>();
            var target = (ChangeScene)template.onClick.GetPersistentTarget(0);
            while (btn.onClick.GetPersistentEventCount() > 0) UnityEventTools.RemovePersistentListener(btn.onClick, 0);
            UnityEventTools.AddStringPersistentListener(btn.onClick, target.MoveToScene, SceneName);

            var tmp = copy.GetComponentInChildren<TMP_Text>(true);
            if (tmp != null) tmp.text = MenuButtonText;
            else { var t = copy.GetComponentInChildren<Text>(true); if (t != null) t.text = MenuButtonText; }

            EditorSceneManager.MarkSceneDirty(main);
            EditorSceneManager.SaveScene(main);
        }
        else if (buttons.Count == 0)
        {
            Debug.LogWarning("Im Menü (Main) wurde kein Button mit ChangeScene gefunden – Menü-Button bitte manuell anlegen.");
        }
        EditorSceneManager.CloseScene(main, true);
    }

    static string GetStringArg(Button b, int index)
    {
        var so = new SerializedObject(b);
        var p = so.FindProperty($"m_OnClick.m_PersistentCalls.m_Calls.Array.data[{index}].m_Arguments.m_StringArgument");
        return p != null ? p.stringValue : null;
    }

    static void Fail(string what) =>
        Debug.LogError($"Vuforia-Menüeintrag '{what}' konnte nicht ausgeführt werden (GameObject → Vuforia Engine → {what}).");
}
#endif
