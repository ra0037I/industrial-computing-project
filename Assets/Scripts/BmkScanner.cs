using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Vuforia;
using Image = UnityEngine.UI.Image;

/// <summary>
/// Blendet einen Rahmen in der Bildschirmmitte ein und liest per OCR (Google ML Kit, Android)
/// das Betriebsmittelkennzeichen (BMK) aus, das sich im Rahmen befindet.
///
/// Einrichtung: Auf ein leeres GameObject in der Szene legen (z. B. "BmkScanner").
/// Der Rahmen, die Anzeige und die Debug-Vorschau werden automatisch erzeugt.
/// Benötigt auf Android das Plugin BmkOcr.java (Assets/Plugins/Android) + ML-Kit-Abhängigkeit.
/// </summary>
public class BmkScanner : MonoBehaviour
{
    public enum RotationMode { Auto, Deg0, Deg90, Deg180, Deg270 }
    [Serializable] public class StringEvent : UnityEvent<string> { }

    [Header("Rahmen")]
    [Tooltip("Breite des Rahmens als Anteil der Bildschirmbreite.")]
    [Range(0.2f, 0.95f)] public float frameWidthFraction = 0.6f;
    [Tooltip("Seitenverhältnis Breite : Höhe des Rahmens (Sticker ca. 2,5 : 1).")]
    public float frameAspect = 2.5f;
    public float frameThickness = 6f;
    public Color frameColorIdle = new Color(1f, 1f, 1f, 0.9f);
    public Color frameColorFound = new Color(0.2f, 0.9f, 0.3f, 1f);
    public string hintText = "BMK in den Rahmen halten";

    [Header("Erkennung")]
    [Tooltip("Sekunden zwischen zwei OCR-Durchläufen.")]
    public float scanInterval = 0.4f;
    [Tooltip("So oft hintereinander muss dasselbe BMK erkannt werden, bevor es gilt.")]
    public int requiredMatches = 2;
    [Tooltip("Regex für gültige BMKs. Standard: Zahl + Buchstaben + Zahl, z. B. 37T1.")]
    public string bmkPattern = @"^\d+[A-Z]+\d+$";
    [Tooltip("Fallback, falls unter Resources/BmkData keine bmk_liste.txt liegt. Normalerweise wird die Liste " +
             "des erkannten Schranks automatisch geladen (siehe BmkContext).")]
    public TextAsset validBmkList;
    [Tooltip("Pause nach einem Treffer, bevor wieder gescannt wird (Sekunden).")]
    public float cooldownAfterHit = 2f;

    [Header("Kamerabild")]
    [Tooltip("Drehung des Kamerabilds für die OCR. Auto passt für Hochformat meist. Mit der Debug-Vorschau prüfen.")]
    public RotationMode rotation = RotationMode.Auto;
    [Tooltip("Zeigt unten links den Ausschnitt, den die OCR bekommt (Ausrichtung/Schärfe prüfen).")]
    public bool showDebugPreview = true;

    [Header("Ereignisse")]
    public StringEvent onBmkRecognized = new StringEvent();

    [Header("Nur Editor (Test ohne Handy)")]
    [Tooltip("Im Editor gibt es kein ML Kit. Dieser Text wird stattdessen als OCR-Ergebnis verwendet.")]
    public string editorSimulatedText = "";

    /// <summary>Zuletzt bestätigtes BMK.</summary>
    public string LastBmk { get; private set; }

    static readonly PixelFormat[] FormatCandidates = { PixelFormat.RGB888, PixelFormat.RGBA8888, PixelFormat.GRAYSCALE };

    // Zeichen, die OCR häufig verwechselt (nur für den Abgleich mit der BMK-Liste)
    static readonly string[] ConfusablePairs = { "0O", "0D", "1I", "1L", "5S", "8B", "2Z", "6G", "7T" };

    PixelFormat activeFormat;
    bool formatReady;
    bool busy;
    float busySince;
    float nextScanTime;
    float cooldownUntil;
    float flashUntil;
    string lastCandidate;
    int matchCount;

    Regex regex;
    HashSet<string> validSet;

    RectTransform frameRoot;
    readonly List<Image> edges = new List<Image>();
    Text label;
    RawImage preview;
    Texture2D cropTex;

#if UNITY_ANDROID && !UNITY_EDITOR
    AndroidJavaClass ocr;
#endif

    // ------------------------------------------------------------------ Lebenszyklus

    void Start()
    {
        regex = new Regex(bmkPattern);
        if (!string.IsNullOrEmpty(BmkContext.CabinetDisplayName))
            hintText = BmkContext.CabinetDisplayName + ": " + hintText;
        BuildValidSet();
        BuildOverlay();

#if UNITY_ANDROID && !UNITY_EDITOR
        try { ocr = new AndroidJavaClass("com.bmk.ocr.BmkOcr"); }
        catch (Exception e) { Debug.LogError("BmkScanner: Android-Plugin BmkOcr nicht gefunden. " + e.Message); }
#endif

        VuforiaApplication.Instance.OnVuforiaStarted += OnVuforiaStarted;
        if (VuforiaApplication.Instance.IsRunning) OnVuforiaStarted();
    }

    void OnDestroy()
    {
        if (VuforiaApplication.Instance != null)
            VuforiaApplication.Instance.OnVuforiaStarted -= OnVuforiaStarted;
        if (cropTex != null) Destroy(cropTex);
    }

    void OnVuforiaStarted()
    {
        foreach (var f in FormatCandidates)
        {
            if (VuforiaBehaviour.Instance.CameraDevice.SetFrameFormat(f, true))
            {
                activeFormat = f;
                formatReady = true;
                Debug.Log("BmkScanner: Kamerabild-Format " + f);
                return;
            }
        }
        Debug.LogWarning("BmkScanner: Kein Kamerabild-Format verfügbar.");
    }

    void Update()
    {
        LayoutOverlay();

        if (busy && Time.time - busySince > 3f) busy = false; // Sicherheit, falls kein Rückruf kommt
        if (!formatReady || busy || Time.time < nextScanTime || Time.time < cooldownUntil) return;
        nextScanTime = Time.time + scanInterval;

        var img = VuforiaBehaviour.Instance.CameraDevice.GetCameraImage(activeFormat);
        if (img == null || img.Pixels == null || img.Width <= 0 || img.Height <= 0) return;

        var pixels = CropAndRotate(img, out int ow, out int oh);
        if (pixels == null) return;

        if (cropTex == null || cropTex.width != ow || cropTex.height != oh)
        {
            if (cropTex != null) Destroy(cropTex);
            cropTex = new Texture2D(ow, oh, TextureFormat.RGB24, false);
        }
        cropTex.SetPixels32(pixels);
        cropTex.Apply();

        if (preview != null)
        {
            preview.gameObject.SetActive(showDebugPreview);
            preview.texture = cropTex;
            float w = Mathf.Min(320f, Screen.width * 0.45f);
            preview.rectTransform.sizeDelta = new Vector2(w, w * oh / (float)ow);
        }

        SendToOcr();
    }

    // ------------------------------------------------------------------ Bild ausschneiden

    int GetRotation()
    {
        switch (rotation)
        {
            case RotationMode.Deg0: return 0;
            case RotationMode.Deg90: return 90;
            case RotationMode.Deg180: return 180;
            case RotationMode.Deg270: return 270;
        }
        switch (Screen.orientation)
        {
            case ScreenOrientation.Portrait: return 90;
            case ScreenOrientation.PortraitUpsideDown: return 270;
            case ScreenOrientation.LandscapeRight: return 180;
            default: return 0;
        }
    }

    /// <summary>
    /// Schneidet den Bereich unter dem Rahmen aus dem Kamerabild aus und dreht ihn aufrecht.
    /// Kamerabild-Puffer: Zeile 0 = oben. Ausgabe: für Texture2D (Zeile 0 = unten).
    /// </summary>
    Color32[] CropAndRotate(Vuforia.Image img, out int ow, out int oh)
    {
        ow = oh = 0;
        int W = img.Width, H = img.Height;
        int bpp = activeFormat == PixelFormat.RGB888 ? 3 : activeFormat == PixelFormat.RGBA8888 ? 4 : 1;
        int stride = img.Stride > 0 ? img.Stride : W * bpp;
        byte[] px = img.Pixels;
        if (px.Length < stride * (H - 1) + W * bpp) return null;

        int rot = GetRotation();
        bool swap = rot == 90 || rot == 270;

        // Das Videobild füllt den Bildschirm (aspect fill) -> Skalierung Bildschirm <-> Kamerabild
        float dispW = swap ? H : W, dispH = swap ? W : H;
        float s = Mathf.Max(Screen.width / dispW, Screen.height / dispH);

        float fw = frameRoot.sizeDelta.x, fh = frameRoot.sizeDelta.y;
        int cw = Mathf.Clamp(Mathf.RoundToInt((swap ? fh : fw) / s), 8, W);
        int ch = Mathf.Clamp(Mathf.RoundToInt((swap ? fw : fh) / s), 8, H);
        int cx = (W - cw) / 2, cy = (H - ch) / 2;

        ow = swap ? ch : cw;
        oh = swap ? cw : ch;
        var result = new Color32[ow * oh];

        for (int y = 0; y < oh; y++)
        {
            for (int x = 0; x < ow; x++)
            {
                int sx, sy;
                switch (rot)
                {
                    case 90: sx = y; sy = ch - 1 - x; break;
                    case 180: sx = cw - 1 - x; sy = ch - 1 - y; break;
                    case 270: sx = cw - 1 - y; sy = x; break;
                    default: sx = x; sy = y; break;
                }
                int i = (cy + sy) * stride + (cx + sx) * bpp;
                Color32 c = bpp == 1
                    ? new Color32(px[i], px[i], px[i], 255)
                    : new Color32(px[i], px[i + 1], px[i + 2], 255);
                result[(oh - 1 - y) * ow + x] = c;
            }
        }
        return result;
    }

    // ------------------------------------------------------------------ OCR

    void SendToOcr()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (ocr == null) return;
        byte[] jpg = cropTex.EncodeToJPG(92);
        busy = true;
        busySince = Time.time;
        ocr.CallStatic("recognize", (sbyte[])(Array)jpg, gameObject.name, nameof(OnOcrResult));
#else
        if (!string.IsNullOrEmpty(editorSimulatedText)) OnOcrResult(editorSimulatedText);
#endif
    }

    /// <summary>Rückruf vom Android-Plugin (UnitySendMessage). Name nicht ändern.</summary>
    public void OnOcrResult(string text)
    {
        busy = false;
        if (Time.time < cooldownUntil) return;

        string candidate = ExtractBmk(text);
        if (candidate == null)
        {
            lastCandidate = null;
            matchCount = 0;
            return;
        }

        if (candidate == lastCandidate) matchCount++;
        else { lastCandidate = candidate; matchCount = 1; }

        if (matchCount >= requiredMatches)
        {
            LastBmk = candidate;
            lastCandidate = null;
            matchCount = 0;
            cooldownUntil = Time.time + cooldownAfterHit;
            flashUntil = Time.time + cooldownAfterHit;
            if (label != null) label.text = candidate;
            Debug.Log("BmkScanner: erkannt " + candidate);
            onBmkRecognized.Invoke(candidate);
        }
    }

    string ExtractBmk(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;

        var candidates = new List<string>();
        foreach (var line in raw.Split('\n'))
        {
            string up = line.ToUpperInvariant();
            candidates.Add(Clean(up)); // ganze Zeile ohne Leerzeichen, z. B. "37 T1" -> "37T1"
            foreach (var tok in up.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries))
                candidates.Add(Clean(tok));
        }

        foreach (var c in candidates)
        {
            if (c.Length < 2) continue;
            string m = Match(c);
            if (m != null) return m;
        }
        return null;
    }

    string Match(string c)
    {
        if (validSet == null) return regex.IsMatch(c) ? c : null;

        if (validSet.Contains(c)) return c;

        // Genau ein verwechselbares Zeichen abweichend und eindeutig -> korrigieren
        string best = null;
        int hits = 0;
        foreach (var v in validSet)
        {
            if (IsConfusableVariant(v, c)) { best = v; hits++; }
        }
        return hits == 1 ? best : null;
    }

    static bool IsConfusableVariant(string a, string b)
    {
        if (a.Length != b.Length) return false;
        int diff = -1;
        for (int i = 0; i < a.Length; i++)
        {
            if (a[i] == b[i]) continue;
            if (diff >= 0) return false;
            diff = i;
        }
        if (diff < 0) return false;
        foreach (var p in ConfusablePairs)
        {
            if ((p[0] == a[diff] && p[1] == b[diff]) || (p[1] == a[diff] && p[0] == b[diff])) return true;
        }
        return false;
    }

    static string Clean(string s)
    {
        var sb = new StringBuilder(s.Length);
        foreach (char ch in s)
            if ((ch >= 'A' && ch <= 'Z') || (ch >= '0' && ch <= '9')) sb.Append(ch);
        return sb.ToString();
    }

    void BuildValidSet()
    {
        // BMK-Listen ALLER Schränke: So werden auch BMKs aus einem anderen Schrank sicher erkannt,
        // und das Info-Panel kann melden, dass sie nicht zum gewählten Schrank gehören.
        var lists = BmkContext.LoadAll("bmk_liste");
        if (lists.Length == 0 && validBmkList != null) lists = new[] { validBmkList };
        if (lists.Length == 0) return;

        validSet = new HashSet<string>();
        foreach (var list in lists)
            foreach (var line in list.text.Split('\n'))
            {
                string c = Clean(line.ToUpperInvariant());
                if (c.Length > 0) validSet.Add(c);
            }
        Debug.Log($"BmkScanner: {validSet.Count} gültige BMKs aus {lists.Length} Liste(n) geladen.");
    }

    // ------------------------------------------------------------------ Overlay (UI)

    void BuildOverlay()
    {
        var canvasGo = new GameObject("BmkScannerOverlay", typeof(Canvas), typeof(CanvasScaler));
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        frameRoot = new GameObject("Frame", typeof(RectTransform)).GetComponent<RectTransform>();
        frameRoot.SetParent(canvasGo.transform, false);
        frameRoot.anchorMin = frameRoot.anchorMax = frameRoot.pivot = new Vector2(0.5f, 0.5f);

        // vier Kanten: oben, unten, links, rechts
        AddEdge(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1));
        AddEdge(new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0));
        AddEdge(new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0.5f));
        AddEdge(new Vector2(1, 0), new Vector2(1, 1), new Vector2(1, 0.5f));

        label = new GameObject("Label", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
        label.transform.SetParent(frameRoot, false);
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.alignment = TextAnchor.LowerCenter;
        label.color = Color.white;
        label.raycastTarget = false;
        label.horizontalOverflow = HorizontalWrapMode.Overflow;
        label.text = hintText;
        var lr = label.rectTransform;
        lr.anchorMin = new Vector2(0, 1);
        lr.anchorMax = new Vector2(1, 1);
        lr.pivot = new Vector2(0.5f, 0);
        lr.anchoredPosition = new Vector2(0, 16);
        var shadow = label.gameObject.AddComponent<Shadow>();
        shadow.effectDistance = new Vector2(2, -2);

        preview = new GameObject("DebugPreview", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
        preview.transform.SetParent(canvasGo.transform, false);
        preview.raycastTarget = false;
        var pr = preview.rectTransform;
        pr.anchorMin = pr.anchorMax = pr.pivot = Vector2.zero;
        pr.anchoredPosition = new Vector2(16, 16);
        preview.gameObject.SetActive(false);
    }

    void AddEdge(Vector2 min, Vector2 max, Vector2 pivot)
    {
        var img = new GameObject("Edge", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        img.transform.SetParent(frameRoot, false);
        img.raycastTarget = false;
        var rt = img.rectTransform;
        rt.anchorMin = min;
        rt.anchorMax = max;
        rt.pivot = pivot;
        edges.Add(img);
    }

    void LayoutOverlay()
    {
        if (frameRoot == null) return;

        float fw = Screen.width * frameWidthFraction;
        float fh = fw / Mathf.Max(0.1f, frameAspect);
        frameRoot.sizeDelta = new Vector2(fw, fh);

        bool found = Time.time < flashUntil;
        for (int i = 0; i < edges.Count; i++)
        {
            edges[i].color = found ? frameColorFound : frameColorIdle;
            // oben/unten: volle Breite, Dicke in y; links/rechts: volle Höhe, Dicke in x
            edges[i].rectTransform.sizeDelta = i < 2 ? new Vector2(0, frameThickness) : new Vector2(frameThickness, 0);
        }

        if (label != null)
        {
            label.fontSize = Mathf.RoundToInt(Mathf.Clamp(Screen.width * 0.045f, 18, 64));
            if (!found && label.text != hintText) label.text = hintText;
        }
    }
}
