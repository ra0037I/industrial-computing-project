using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using Image = UnityEngine.UI.Image;

// =====================================================================================
//  Datenmodell
// =====================================================================================

/// <summary>Zusatzinfos zu einem Betriebsmittel. Feldnamen = Schlüssel im JSON.</summary>
[Serializable]
public class BmkInfo
{
    public string bmk;
    public string bezeichnung;
    public string typ;
    public string hersteller;
    public string artikelnummer;
    public string einbauort;
    public string funktion;
    public string status;
    public string letztePruefung;
    public string hinweis;
    public string dokumentUrl;
}

[Serializable]
public class BmkInfoList
{
    public List<BmkInfo> eintraege = new List<BmkInfo>();
}

// =====================================================================================
//  Datenquellen – Mock jetzt, echte Datenbank/REST später austauschbar
// =====================================================================================

public interface IBmkInfoSource
{
    /// <summary>Liefert die Infos zum BMK oder null, wenn unbekannt.</summary>
    void Lookup(string bmk, Action<BmkInfo> done);
}

/// <summary>Liest alle Einträge aus einer JSON-Datei (TextAsset).</summary>
public class MockBmkInfoSource : IBmkInfoSource
{
    readonly Dictionary<string, BmkInfo> byBmk = new Dictionary<string, BmkInfo>();

    public MockBmkInfoSource(params TextAsset[] jsons)
    {
        if (jsons == null || jsons.Length == 0) { Debug.LogWarning("BmkInfo: Keine Mock-JSON gefunden."); return; }
        foreach (var json in jsons)
        {
            if (json == null) continue;
            var list = JsonUtility.FromJson<BmkInfoList>(json.text);
            if (list?.eintraege == null) { Debug.LogError("BmkInfo: Mock-JSON konnte nicht gelesen werden: " + json.name); continue; }
            foreach (var e in list.eintraege)
                if (!string.IsNullOrEmpty(e.bmk)) byBmk[e.bmk.ToUpperInvariant()] = e;
        }
        Debug.Log($"BmkInfo: {byBmk.Count} Mock-Einträge geladen ({BmkContext.Cabinet ?? "alle Schränke"}).");
    }

    public void Lookup(string bmk, Action<BmkInfo> done)
    {
        byBmk.TryGetValue(bmk.ToUpperInvariant(), out var info);
        done(info);
    }
}

/// <summary>
/// Holt die Infos von einem Webservice: GET {baseUrl}/{bmk} -> JSON im Format von BmkInfo.
/// Auf Android nur https verwenden (http wird standardmäßig blockiert).
/// </summary>
public class RestBmkInfoSource : IBmkInfoSource
{
    readonly MonoBehaviour host;
    readonly string baseUrl;

    public RestBmkInfoSource(MonoBehaviour host, string baseUrl)
    {
        this.host = host;
        this.baseUrl = baseUrl.TrimEnd('/');
    }

    public void Lookup(string bmk, Action<BmkInfo> done) => host.StartCoroutine(Get(bmk, done));

    IEnumerator Get(string bmk, Action<BmkInfo> done)
    {
        // Optional wird der erkannte Schrank mitgeschickt: GET {baseUrl}/{bmk}?schrank={TargetName}
        string url = baseUrl + "/" + UnityWebRequest.EscapeURL(bmk);
        if (!string.IsNullOrEmpty(BmkContext.Cabinet)) url += "?schrank=" + UnityWebRequest.EscapeURL(BmkContext.Cabinet);
        using (var req = UnityWebRequest.Get(url))
        {
            req.timeout = 5;
            yield return req.SendWebRequest();
            if (req.result == UnityWebRequest.Result.Success)
            {
                try { done(JsonUtility.FromJson<BmkInfo>(req.downloadHandler.text)); }
                catch (Exception e) { Debug.LogError("BmkInfo: Antwort ungültig. " + e.Message); done(null); }
            }
            else
            {
                Debug.LogWarning("BmkInfo: Abfrage fehlgeschlagen (" + req.error + ")");
                done(null);
            }
        }
    }
}

// =====================================================================================
//  Anzeige
// =====================================================================================

/// <summary>
/// Zeigt zu einem erkannten BMK ein Info-Panel am unteren Bildschirmrand.
/// Auf dasselbe GameObject wie den BmkScanner legen (oder irgendwo in die Szene) –
/// verbindet sich automatisch mit dem Scanner.
/// </summary>
public class BmkInfoPanel : MonoBehaviour
{
    public enum SourceMode { Mock, Rest }

    [Header("Datenquelle")]
    public SourceMode source = SourceMode.Mock;
    [Tooltip("Fallback, falls unter Resources/BmkData keine bmk_mock.json liegt. Normalerweise werden die " +
             "Daten des erkannten Schranks automatisch geladen (siehe BmkContext).")]
    public TextAsset mockJson;
    [Tooltip("Basis-URL des Webservice, z. B. https://server/api/bmk")]
    public string restBaseUrl = "https://example.com/api/bmk";

    [Header("Anzeige")]
    [Tooltip("Panel nach x Sekunden ausblenden. 0 = bleibt bis zum nächsten Scan.")]
    public float hideAfterSeconds = 0f;
    public Color panelColor = new Color(0.08f, 0.09f, 0.11f, 0.88f);
    public Color accentColor = new Color(1f, 0.82f, 0.2f, 1f); // gelb wie die Sticker
    public Color wrongCabinetColor = new Color(1f, 0.6f, 0.15f, 1f); // orange = BMK aus anderem Schrank

    IBmkInfoSource dataSource;
    GameObject panel;
    Text titleText;
    Text bodyText;
    Image accentBar;
    string shownBmk;
    float hideAt;

    void Start()
    {
        dataSource = source == SourceMode.Rest
            ? new RestBmkInfoSource(this, restBaseUrl)
            : new MockBmkInfoSource(MockFiles());

        BuildUi();

        var scanner = FindFirstObjectByType<BmkScanner>();
        if (scanner != null) scanner.onBmkRecognized.AddListener(Show);
        else Debug.LogWarning("BmkInfoPanel: Kein BmkScanner in der Szene gefunden.");
    }

    TextAsset[] MockFiles()
    {
        var files = BmkContext.Load("bmk_mock");
        return files.Length > 0 ? files : new[] { mockJson };
    }

    void Update()
    {
        if (panel == null) return;

        float w = Screen.width;
        titleText.fontSize = Mathf.RoundToInt(Mathf.Clamp(w * 0.055f, 22, 72));
        bodyText.fontSize = Mathf.RoundToInt(Mathf.Clamp(w * 0.038f, 16, 48));

        if (panel.activeSelf && hideAfterSeconds > 0 && Time.time > hideAt) Hide();
    }

    /// <summary>Infos zu einem BMK abrufen und anzeigen. Kann auch von eigenem Code aufgerufen werden.</summary>
    public void Show(string bmk)
    {
        if (string.IsNullOrEmpty(bmk)) return;
        hideAt = Time.time + hideAfterSeconds;
        if (bmk == shownBmk && panel.activeSelf) return; // gleiches BMK schon sichtbar
        shownBmk = bmk;

        // BMK gehört nicht zum erkannten Schrank? -> Hinweis statt Daten
        if (BmkContext.CurrentCabinetHasList)
        {
            string home = BmkContext.CabinetOf(bmk);
            if (home != BmkContext.Cabinet)
            {
                titleText.text = bmk + "  ·  nicht in diesem Schrank";
                bodyText.text = home != null
                    ? $"Dieses Betriebsmittel gehört nicht zum <b>{BmkContext.CabinetDisplayName}</b>,\n" +
                      $"sondern zum <b>{BmkContext.DisplayName(home)}</b>."
                    : $"Dieses Betriebsmittel ist im <b>{BmkContext.CabinetDisplayName}</b> nicht verzeichnet.";
                accentBar.color = wrongCabinetColor;
                panel.SetActive(true);
                return;
            }
        }

        titleText.text = bmk;
        bodyText.text = "Lade Daten …";
        accentBar.color = accentColor;
        panel.SetActive(true);

        dataSource.Lookup(bmk, info =>
        {
            if (shownBmk != bmk) return; // inzwischen anderes BMK gescannt
            if (info == null)
            {
                titleText.text = bmk;
                bodyText.text = "Keine Daten zu diesem Betriebsmittel gefunden.";
                accentBar.color = new Color(0.9f, 0.3f, 0.3f);
                return;
            }
            titleText.text = string.IsNullOrEmpty(info.bezeichnung) ? info.bmk : info.bmk + "  ·  " + info.bezeichnung;
            bodyText.text = Format(info);
            accentBar.color = StatusColor(info.status);
        });
    }

    public void Hide()
    {
        if (panel != null) panel.SetActive(false);
        shownBmk = null;
    }

    static string Format(BmkInfo i)
    {
        var sb = new StringBuilder();
        Row(sb, "Typ", i.typ);
        Row(sb, "Hersteller", i.hersteller);
        Row(sb, "Artikelnr.", i.artikelnummer);
        Row(sb, "Einbauort", i.einbauort);
        Row(sb, "Funktion", i.funktion);
        Row(sb, "Status", i.status);
        Row(sb, "Letzte Prüfung", i.letztePruefung);
        Row(sb, "Hinweis", i.hinweis);
        Row(sb, "Dokument", i.dokumentUrl);
        return sb.ToString().TrimEnd();
    }

    static void Row(StringBuilder sb, string label, string value)
    {
        if (string.IsNullOrEmpty(value)) return;
        sb.Append("<b>").Append(label).Append(":</b>  ").Append(value).Append('\n');
    }

    static Color StatusColor(string status)
    {
        string s = (status ?? "").ToLowerInvariant();
        if (s.Contains("störung") || s.Contains("defekt") || s.Contains("gesperrt")) return new Color(0.9f, 0.3f, 0.3f);
        if (s.Contains("wartung") || s.Contains("fällig") || s.Contains("prüfen")) return new Color(1f, 0.6f, 0.15f);
        if (s.Contains("ok") || s.Contains("in betrieb")) return new Color(0.25f, 0.85f, 0.4f);
        return new Color(1f, 0.82f, 0.2f);
    }

    // ------------------------------------------------------------------ UI-Aufbau

    void BuildUi()
    {
        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        var canvasGo = new GameObject("BmkInfoCanvas", typeof(Canvas), typeof(CanvasScaler));
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 110; // über dem Scanner-Rahmen

        // Panel unten, Höhe passt sich dem Inhalt an
        panel = new GameObject("InfoPanel", typeof(RectTransform), typeof(Image),
                               typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        panel.transform.SetParent(canvasGo.transform, false);
        var bg = panel.GetComponent<Image>();
        bg.color = panelColor;
        bg.raycastTarget = false;

        var rt = (RectTransform)panel.transform;
        rt.anchorMin = new Vector2(0, 0);
        rt.anchorMax = new Vector2(1, 0);
        rt.pivot = new Vector2(0.5f, 0);
        rt.offsetMin = new Vector2(24, 24);
        rt.offsetMax = new Vector2(-24, 0);
        rt.anchoredPosition = new Vector2(0, 24);

        var layout = panel.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(32, 32, 28, 28);
        layout.spacing = 14;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandHeight = false;

        panel.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // farbiger Statusbalken oben
        accentBar = new GameObject("Accent", typeof(RectTransform), typeof(Image), typeof(LayoutElement)).GetComponent<Image>();
        accentBar.transform.SetParent(panel.transform, false);
        accentBar.raycastTarget = false;
        accentBar.GetComponent<LayoutElement>().preferredHeight = 8;

        titleText = MakeText("Title", font, FontStyle.Bold);
        bodyText = MakeText("Body", font, FontStyle.Normal);
        bodyText.lineSpacing = 1.15f;
        bodyText.color = new Color(0.9f, 0.92f, 0.95f);

        panel.SetActive(false);
    }

    Text MakeText(string name, Font font, FontStyle style)
    {
        var t = new GameObject(name, typeof(RectTransform), typeof(Text)).GetComponent<Text>();
        t.transform.SetParent(panel.transform, false);
        t.font = font;
        t.fontStyle = style;
        t.color = Color.white;
        t.supportRichText = true;
        t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        return t;
    }
}
