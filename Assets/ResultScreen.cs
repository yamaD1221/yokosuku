using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

// 結果画面。表示とボタンだけを担当し、星の計算や状態の切り替えは行わない
public class ResultScreen : MonoBehaviour
{
    [SerializeField] private Font font;

    public event Action RetryClicked;
    public event Action NextClicked;
    public event Action QuitClicked;

    private GameObject screenRoot;
    private Text titleText, starText, rulesText, statsText;

    public bool IsVisible => screenRoot != null && screenRoot.activeSelf;
    public void Hide() { if (screenRoot != null) screenRoot.SetActive(false); }

    void Awake()
    {
        if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        Build();
        screenRoot.SetActive(false);
    }

    public void Show(bool cleared, StarResult r, RunRecorder rec)
    {
        titleText.text = cleared ? "CLEAR!" : "FAILED";
        titleText.color = cleared ? new Color(1f, 0.85f, 0.3f) : new Color(1f, 0.45f, 0.45f);

        string stars = "";
        for (int i = 0; i < 3; i++)
            stars += i < r.Stars ? "<color=#ffd54a>★</color>" : "<color=#555555>☆</color>";
        starText.text = stars;

        var s = rec.Stats;
        rulesText.text =
            Line("★1", rec.StarOneRule, s, r.StarOne, cleared ? null : "ボスを倒せなかった") + "\n" +
            Line("★2", rec.StarTwoRule, s, r.StarTwo, null) + "\n" +
            Line("★3", rec.StarThreeRule, s, r.StarThree, null) + "\n" +
            "<color=#aaaaaa>※星は、★1から順に満たした分だけ付きます</color>";

        statsText.text =
            $"ナイフ使用数 {s.KnifeThrown}     近接攻撃 {s.MeleeCount}     着地 {s.Landings}\n" +
            $"死亡 {s.Deaths}     {(cleared ? $"クリアタイム {s.ElapsedTime:F1}秒" : $"経過 {s.ElapsedTime:F1}秒")}";

        screenRoot.SetActive(true);
    }

    static string Line(string label, ChallengeRule rule, RunStats s, bool met, string note)
    {
        if (rule == null) return "";
        string mark = met ? "✓" : "×";
        string color = met ? "#9be77a" : "#ff6666";
        string progress = note ?? rule.Progress(s);
        return $"<color={color}>{mark}  {label}  {rule.Title}   {progress}</color>";
    }

    // ---------- 画面の部品を作る ----------
    void Build()
    {
        var canvasGo = new GameObject("ResultCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        // 画面全体を暗くして、下のステージ操作を受け付けなくする
        screenRoot = new GameObject("Dim", typeof(RectTransform), typeof(Image));
        screenRoot.transform.SetParent(canvasGo.transform, false);
        Stretch((RectTransform)screenRoot.transform);
        screenRoot.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.7f);

        // 中央のパネル
        var panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(screenRoot.transform, false);
        var prt = (RectTransform)panel.transform;
        prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 0.5f);
        prt.pivot = new Vector2(0.5f, 0.5f);
        prt.sizeDelta = new Vector2(1100f, 820f);
        panel.GetComponent<Image>().color = new Color(0.1f, 0.1f, 0.14f, 0.97f);

        titleText = MakeText(panel.transform, 96, TextAnchor.MiddleCenter, new Vector2(0f, -24f), new Vector2(1100f, 120f));
        titleText.fontStyle = FontStyle.Bold;
        starText = MakeText(panel.transform, 110, TextAnchor.MiddleCenter, new Vector2(0f, -150f), new Vector2(1100f, 130f));
        rulesText = MakeText(panel.transform, 34, TextAnchor.UpperLeft, new Vector2(70f, -310f), new Vector2(1000f, 190f));
        statsText = MakeText(panel.transform, 30, TextAnchor.UpperLeft, new Vector2(70f, -520f), new Vector2(1000f, 100f));

        MakeButton(panel.transform, "もう一度", new Vector2(50f, -690f), () => RetryClicked?.Invoke());
        MakeButton(panel.transform, "次へ", new Vector2(400f, -690f), () => NextClicked?.Invoke());
        MakeButton(panel.transform, "やめる", new Vector2(750f, -690f), () => QuitClicked?.Invoke());
    }

    Text MakeText(Transform parent, int size, TextAnchor align, Vector2 pos, Vector2 boxSize)
    {
        var go = new GameObject("Text", typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        PlaceTopLeft((RectTransform)go.transform, pos, boxSize);

        var t = go.GetComponent<Text>();
        t.font = font;
        t.fontSize = size;
        t.alignment = align;
        t.color = Color.white;
        t.supportRichText = true;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        return t;
    }

    void MakeButton(Transform parent, string label, Vector2 pos, UnityAction onClick)
    {
        var go = new GameObject("Btn_" + label, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        PlaceTopLeft((RectTransform)go.transform, pos, new Vector2(300f, 90f));

        var img = go.GetComponent<Image>();
        img.color = new Color(1f, 1f, 1f, 0.22f);
        var btn = go.GetComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(onClick);

        var t = MakeText(go.transform, 40, TextAnchor.MiddleCenter, Vector2.zero, Vector2.zero);
        Stretch(t.rectTransform);
        t.text = label;
    }

    static void PlaceTopLeft(RectTransform rt, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}