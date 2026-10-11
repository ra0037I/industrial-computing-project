using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Vuforia;
using Image = UnityEngine.UI.Image;

/// <summary>
/// Zeigt ein Popup am unteren Bildschirmrand, sobald das Model Target (Schaltschrank) erkannt wird.
/// Der Button wechselt in die BMK-Scanner-Szene. Optik analog zum BmkInfoPanel.
/// Auf das ModelTarget oder ein beliebiges GameObject in der Szene legen; die UI wird zur Laufzeit erzeugt.
/// Ablage: Assets/Scripts/CabinetFoundPopup.cs
/// </summary>
public class CabinetFoundPopup : MonoBehaviour
{
    [Tooltip("Model Target, auf das reagiert wird. Leer = automatisch suchen.")]
    public ObserverBehaviour target;

    [Header("Inhalt")]
    public string title = "Schaltschrank erkannt";
    [TextArea] public string message = "Hauptverteiler (mainsCabinet)\nBetriebsmittel können jetzt per Texterkennung (BMK) identifiziert werden.";
    public string buttonText = "Zur BMK-Erkennung";

    [Header("Szenenwechsel")]
    [Tooltip("Name der Szene in der Build-Liste.")]
    public string targetScene = "BmkScanner";

    [Header("Verhalten")]
    [Tooltip("Popup bleibt so viele Sekunden stehen, nachdem der Schrank aus dem Bild ist (damit man noch tippen kann).")]
    public float hideDelaySeconds = 3f;
    [Tooltip("Auch bei EXTENDED_TRACKED (Schrank kurz aus dem Bild) sichtbar lassen.")]
    public bool keepWhileExtendedTracked = true;

    [Header("Farben")]
    public Color panelColor = new Color(0.08f, 0.09f, 0.11f, 0.88f);
    public Color accentColor = new Color(0.25f, 0.85f, 0.4f, 1f);     // grün = erkannt
    public Color buttonColor = new Color(1f, 0.82f, 0.2f, 1f);        // gelb wie die BMK-Sticker

    ObserverBehaviour observer;
    GameObject panel;
    Text titleText, bodyText, buttonLabel;
    bool visible;
    float hideAt = -1f;
    bool loading;

    void Awake()
    {
        // Entweder direkt auf dem ModelTarget, sonst das zugewiesene bzw. erste Model Target der Szene nehmen
        observer = target != null ? target : GetComponent<ObserverBehaviour>();
        if (observer == null) observer = FindFirstObjectByType<ModelTargetBehaviour>();
        if (observer == null)
        {
            Debug.LogError("CabinetFoundPopup: Kein Model Target in der Szene gefunden.");
            enabled = false;
            return;
        }
        observer.OnTargetStatusChanged += OnStatusChanged;
        BuildUi();
        EnsureEventSystem();
    }

    void OnDestroy()
    {
        if (observer != null) observer.OnTargetStatusChanged -= OnStatusChanged;
    }

    void OnStatusChanged(ObserverBehaviour b, TargetStatus status)
    {
        bool found = status.Status == Status.TRACKED ||
                     (keepWhileExtendedTracked && status.Status == Status.EXTENDED_TRACKED && visible);
        if (found) { hideAt = -1f; SetVisible(true); }
        else if (visible && hideAt < 0f) hideAt = Time.time + hideDelaySeconds;
    }

    void Update()
    {
        if (panel == null) return;

        float w = Mathf.Min(Screen.width, Screen.height * 1.2f);
        titleText.fontSize = Mathf.RoundToInt(Mathf.Clamp(w * 0.055f, 22, 72));
        bodyText.fontSize = Mathf.RoundToInt(Mathf.Clamp(w * 0.038f, 16, 48));
        buttonLabel.fontSize = Mathf.RoundToInt(Mathf.Clamp(w * 0.045f, 18, 56));

        if (visible && hideAt > 0f && Time.time > hideAt) { hideAt = -1f; SetVisible(false); }
    }

    void SetVisible(bool v)
    {
        visible = v;
        panel.SetActive(v);
    }

    /// <summary>Wechselt in die BMK-Scanner-Szene. Kann auch von anderen Buttons aufgerufen werden.</summary>
    public void OpenBmkScanner()
    {
        if (loading) return;
        if (!Application.CanStreamedLevelBeLoaded(targetScene))
        {
            Debug.LogError($"CabinetFoundPopup: Szene '{targetScene}' ist nicht in der Build-Liste.");
            return;
        }
        loading = true;
        buttonLabel.text = "Lade …";
        ChangeScene.Load(targetScene);
    }

    // ------------------------------------------------------------------ UI-Aufbau

    void BuildUi()
    {
        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        // Eigenes Root-Objekt (nicht unter dem ModelTarget), damit Vuforia das Canvas
        // nicht mit dem Target ein-/ausblendet.
        var canvasGo = new GameObject("CabinetFoundCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 110;

        panel = new GameObject("CabinetPanel", typeof(RectTransform), typeof(Image),
                               typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        panel.transform.SetParent(canvasGo.transform, false);
        var bg = panel.GetComponent<Image>();
        bg.color = panelColor;
        bg.raycastTarget = true; // Tipps auf das Panel nicht an die AR-Szene durchreichen

        var rt = (RectTransform)panel.transform;
        rt.anchorMin = new Vector2(0, 0);
        rt.anchorMax = new Vector2(1, 0);
        rt.pivot = new Vector2(0.5f, 0);
        rt.offsetMin = new Vector2(24, 24);
        rt.offsetMax = new Vector2(-24, 0);
        rt.anchoredPosition = new Vector2(0, 24);

        var layout = panel.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(32, 32, 28, 32);
        layout.spacing = 16;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandHeight = false;
        panel.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var accent = new GameObject("Accent", typeof(RectTransform), typeof(Image), typeof(LayoutElement)).GetComponent<Image>();
        accent.transform.SetParent(panel.transform, false);
        accent.color = accentColor;
        accent.raycastTarget = false;
        accent.GetComponent<LayoutElement>().preferredHeight = 8;

        titleText = MakeText("Title", font, FontStyle.Bold, panel.transform);
        titleText.text = title;
        bodyText = MakeText("Body", font, FontStyle.Normal, panel.transform);
        bodyText.text = message;
        bodyText.lineSpacing = 1.15f;
        bodyText.color = new Color(0.9f, 0.92f, 0.95f);

        // Button
        var btnGo = new GameObject("BmkButton", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        btnGo.transform.SetParent(panel.transform, false);
        btnGo.GetComponent<Image>().color = buttonColor;
        btnGo.GetComponent<LayoutElement>().minHeight = 110;
        var btn = btnGo.GetComponent<Button>();
        var colors = btn.colors;
        colors.pressedColor = new Color(0.8f, 0.8f, 0.8f);
        btn.colors = colors;
        btn.onClick.AddListener(OpenBmkScanner);

        buttonLabel = MakeText("Label", font, FontStyle.Bold, btnGo.transform);
        buttonLabel.text = buttonText;
        buttonLabel.color = new Color(0.1f, 0.1f, 0.1f);
        buttonLabel.alignment = TextAnchor.MiddleCenter;
        var lrt = (RectTransform)buttonLabel.transform;
        lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
        lrt.offsetMin = Vector2.zero; lrt.offsetMax = Vector2.zero;

        panel.SetActive(false);
    }

    static Text MakeText(string name, Font font, FontStyle style, Transform parent)
    {
        var t = new GameObject(name, typeof(RectTransform), typeof(Text)).GetComponent<Text>();
        t.transform.SetParent(parent, false);
        t.font = font;
        t.fontStyle = style;
        t.color = Color.white;
        t.supportRichText = true;
        t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        return t;
    }

    static void EnsureEventSystem()
    {
        if (EventSystem.current != null || FindFirstObjectByType<EventSystem>() != null) return;
        new GameObject("EventSystem", typeof(EventSystem),
                       typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
    }
}
