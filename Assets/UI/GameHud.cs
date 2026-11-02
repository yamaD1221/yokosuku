using UnityEngine;
using UnityEngine.UI;

// 画面の上に、モードの帯と、縛り条件の一覧を出す(画面の部品は、コードで作る)
public class GameHud : MonoBehaviour
{
    [SerializeField] private Font font;     // 空なら標準フォント

    private Transform root;
    private Image modeBg;
    private Text modeText;
    private GameObject constraintPanel;
    private Text constraintText;

    private static readonly Color EditColor = new Color(0.12f, 0.30f, 0.62f, 0.9f);
    private static readonly Color PlayColor = new Color(0.12f, 0.50f, 0.25f, 0.9f);
    private static readonly Color PanelColor = new Color(0f, 0f, 0f, 0.6f);

    void Awake()
    {
        if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        BuildCanvas();
        BuildModeBanner();

        BuildConstraintPanel();
    }

    void Update()
    {
        bool editing = LevelEditor.IsEditing;

        modeBg.color = editing ? EditColor : PlayColor;
        modeText.text = editing
            ? "編集モード     Tab: プレイ開始"
            : "プレイ中     Tab: 編集に戻る";

        var rec = RunRecorder.Instance;
        bool hasRules = rec != null && rec.HasRules;
        constraintPanel.SetActive(hasRules);
        if (hasRules)
        {
            constraintText.text = rec.BuildConstraintText().TrimEnd();

            // 文字の大きさに合わせて、枠の大きさを決める(余白を詰める)
            var prt = (RectTransform)constraintPanel.transform;
            prt.sizeDelta = new Vector2(constraintText.preferredWidth + 28f, constraintText.preferredHeight + 28f);
        }
    }

    // ---------- 画面の部品を作る ----------
    void BuildCanvas()
    {
        var go = new GameObject("HudCanvas", typeof(Canvas), typeof(CanvasScaler));
        go.transform.SetParent(transform, false);

        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;

        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        root = go.transform;
    }

    void BuildModeBanner()
    {
        var rt = MakePanel("ModeBanner", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -16f), new Vector2(560f, 56f), EditColor);
        modeBg = rt.GetComponent<Image>();

        modeText = MakeText(rt, "Text", 30, TextAnchor.MiddleCenter, Color.white);
        Stretch(modeText.rectTransform, 0f);
    }

    void BuildConstraintPanel()
    {
        var rt = MakePanel("ConstraintPanel", new Vector2(1f, 1f), new Vector2(1f, 1f),
            new Vector2(-16f, -16f), new Vector2(780f, 230f), PanelColor);
        constraintPanel = rt.gameObject;

        constraintText = MakeText(rt, "Text", 28, TextAnchor.UpperLeft, Color.white);
        constraintText.supportRichText = true;
        Stretch(constraintText.rectTransform, 14f);
    }

    // ---------- 部品を作る道具 ----------
    RectTransform MakePanel(string name, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(root, false);

        var rt = (RectTransform)go.transform;
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = pivot;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;

        var img = go.GetComponent<Image>();
        img.color = color;
        img.raycastTarget = false;          // 下のステージ操作を邪魔しない
        return rt;
    }

    Text MakeText(Transform parent, string name, int size, TextAnchor align, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);

        var t = go.GetComponent<Text>();
        t.font = font;
        t.fontSize = size;
        t.alignment = align;
        t.color = color;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        return t;
    }

    static void Stretch(RectTransform rt, float pad)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(pad, pad);
        rt.offsetMax = new Vector2(-pad, -pad);
    }
}