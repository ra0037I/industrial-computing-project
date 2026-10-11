#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using Vuforia;

/// <summary>
/// Nur im Editor, nur im Vuforia-Play-Mode "Simulator":
/// Stellt den simulierten Schaltschrank beim Start so vor die Kamera,
/// dass man direkt in der Guide-View-Position steht (ca. 2,2 m frontal, leicht erhöht).
/// Taste R = Schrank erneut vor die Kamera setzen.
/// Hängt sich automatisch an jedes Model Target, kein Setup in der Szene nötig.
/// Ablage: Assets/Scripts/SimulatorStartPose.cs
/// </summary>
public class SimulatorStartPose : MonoBehaviour
{
    // Guide View aus Assets/StreamingAssets/Vuforia/mainsCabinet.xml (Target-Koordinaten, Meter)
    public Vector3 guideViewPosition = new Vector3(-0.0275347f, 0.687597f, 0.121474f);
    // Mitte der Bounding Box des Scans (aus mBBoxMin/mBBoxMax)
    public Vector3 lookAtPoint = new Vector3(-0.17876f, -0.06082f, -1.96199f);
    [Tooltip("Zusätzlicher Abstand zur Guide View in Metern (Test: Erkennung ab ca. 0,8 m weiter hinten).")]
    public float extraDistance = 0.8f;
    public float searchTimeout = 10f;

    Transform simulated;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install()
    {
        SceneManager.sceneLoaded += (s, m) => Attach();
        Attach();
    }

    static void Attach()
    {
        if (!VuforiaRuntimeUtilities.IsSimulatorPlayMode()) return;
        foreach (var mt in Object.FindObjectsByType<ModelTargetBehaviour>())
            if (mt.GetComponent<SimulatorStartPose>() == null)
                mt.gameObject.AddComponent<SimulatorStartPose>();
    }

    IEnumerator Start()
    {
        float t = 0f;
        while (simulated == null && t < searchTimeout)
        {
            simulated = FindSimulated();
            if (simulated == null) { t += Time.unscaledDeltaTime; yield return null; }
        }

        if (simulated == null)
        {
            var names = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include)
                .Where(x => x.parent == null).Select(x => x.name);
            Debug.LogWarning("[SimulatorStartPose] Kein '(simulated)'-Objekt gefunden. Root-Objekte: " + string.Join(", ", names));
            yield break;
        }

        // Ein paar Frames warten, bis der Simulator die Kamera gesetzt hat
        for (int i = 0; i < 5; i++) yield return null;
        MakeVisible();
        Place();
        Diagnose();
        for (int i = 0; i < 6; i++)
        {
            yield return new WaitForSeconds(0.5f);
            var cam = Camera.main.transform;
            var mtb = GetComponent<ModelTargetBehaviour>();
            Debug.Log($"[SimulatorStartPose] t={i * 0.5f + 0.5f}s Kamera pos={cam.position} rot={cam.eulerAngles} | sim pos={simulated.position} | Status={mtb.TargetStatus.Status}/{mtb.TargetStatus.StatusInfo}");
        }
    }

    void Diagnose()
    {
        var cam = Camera.main;
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"[SimulatorStartPose] DIAGNOSE Kamera='{cam.name}' cullingMask={cam.cullingMask} near={cam.nearClipPlane} far={cam.farClipPlane} fov={cam.fieldOfView}");
        sb.AppendLine($"  sim='{simulated.name}' parent={(simulated.parent ? simulated.parent.name : "-")} scale={simulated.lossyScale} active={simulated.gameObject.activeInHierarchy} layer={LayerMask.LayerToName(simulated.gameObject.layer)}");
        foreach (var r in simulated.GetComponentsInChildren<Renderer>(true))
            sb.AppendLine($"  Renderer '{r.name}' enabled={r.enabled} active={r.gameObject.activeInHierarchy} layer={LayerMask.LayerToName(r.gameObject.layer)} bounds.center={r.bounds.center} size={r.bounds.size}");
        foreach (Transform c in simulated)
            sb.AppendLine($"  Kind '{c.name}' localPos={c.localPosition} localScale={c.localScale}");
        var mtb = GetComponent<ModelTargetBehaviour>();
        sb.AppendLine($"  ModelTarget '{name}' pos={transform.position} scale={transform.lossyScale} Status={mtb.TargetStatus.Status}");
        Debug.Log(sb.ToString());
    }

    void Update()
    {
        var kb = UnityEngine.InputSystem.Keyboard.current;
        if (simulated != null && kb != null && kb.rKey.wasPressedThisFrame) Place();
    }

    // Bei ausgeschalteter Model-Target-Vorschau ist auch das simulierte Modell deaktiviert,
    // dann kann der Simulator nichts zeigen und nichts erkennen -> im Simulator einschalten.
    void MakeVisible()
    {
        foreach (var t in simulated.GetComponentsInChildren<Transform>(true))
            if (!t.gameObject.activeSelf) t.gameObject.SetActive(true);
        foreach (var r in simulated.GetComponentsInChildren<Renderer>(true))
            r.enabled = true;
    }

    Transform FindSimulated()
    {
        var all = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include)
            .Where(x => x.name.EndsWith(" (simulated)")).ToList();
        if (all.Count == 0) return null;
        var mt = GetComponent<ModelTargetBehaviour>();
        return all.FirstOrDefault(x => x.name.StartsWith(name) || (mt != null && x.name.StartsWith(mt.TargetName)))
               ?? all[0];
    }

    void Place()
    {
        var cam = Camera.main != null ? Camera.main.transform : null;
        if (cam == null) { Debug.LogWarning("[SimulatorStartPose] Keine Main Camera gefunden."); return; }

        var dir = (lookAtPoint - guideViewPosition).normalized;
        var gvRot = Quaternion.LookRotation(dir, Vector3.up);
        var gvPos = guideViewPosition - dir * extraDistance;
        Matrix4x4 guideView = Matrix4x4.TRS(gvPos, gvRot, Vector3.one);
        Matrix4x4 camPose = Matrix4x4.TRS(cam.position, cam.rotation, Vector3.one);
        Matrix4x4 target = camPose * guideView.inverse;

        Debug.Log($"[SimulatorStartPose] Kamera pos={cam.position} rot={cam.eulerAngles}; '{simulated.name}' vorher pos={simulated.position}");
        simulated.SetPositionAndRotation(target.GetPosition(), target.rotation);
        Debug.Log($"[SimulatorStartPose] '{simulated.name}' jetzt pos={simulated.position} rot={simulated.eulerAngles}");
    }
}
#endif
