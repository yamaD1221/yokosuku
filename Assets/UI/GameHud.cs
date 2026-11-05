using UnityEngine;
using UnityEngine.UI;

// 画面上にモードと縛り条件を表示する
public class GameHud : MonoBehaviour
{
    [SerializeField] private Font font;     // 空なら標準フォント

    private Transform root;
    private Image modeBg;
    private Text modeText;
    private GameObject constraintPanel;
    private Text constraintText;

    // モードごとの背景色
    private static readonly Color EditColor = new Color(0.12f, 0.30f, 0.62f, 0.9f);
    private static readonly Color PlayColor = new Color(0.12f, 0.50f, 0.25f, 0.9f);
    private static readonly Color PanelColor = new Color(0f, 0f, 0f, 0.6f);

    void Awake()
    {
        // フォントが設定されていなければ標準フォントを使用する
        if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        // HUDの画面と各表示部分を作成する
        BuildCanvas();
        BuildModeBanner();
        BuildConstraintPanel();
    }

    void Update()
    {
        // 現在のモードを取得する
        bool editing = LevelEditor.IsEditing;

        // モードに合わせて背景色と表示文字を変更する
        modeBg.color = editing ? EditColor : PlayColor;
        modeText.text = editing
            ? "編集モード     Tab: プレイ開始"
            : "プレイ中     Tab: 編集に戻る";

        // 挑戦記録を取得する
        var rec = RunRecorder.Instance;
        bool hasRules = rec != null && rec.HasRules;

        // 縛り条件がある場合だけパネルを表示する
        constraintPanel.SetActive(hasRules);

        if (hasRules)
        {
            // 縛り条件の内容を表示する
            constraintText.text = rec.BuildConstraintText().TrimEnd();

            // 文字の大きさに合わせてパネルのサイズを変更する
            var prt = (RectTransform)constraintPanel.transform;
            prt.sizeDelta = new Vector2(constraintText.preferredWidth + 28f, constraintText.preferredHeight + 28f);
        }
    }

    // 画面の部品を作る

    void BuildCanvas()
    {
        // HUD用のCanvasを作成する
        var go = new GameObject("HudCanvas", typeof(Canvas), typeof(CanvasScaler));
        go.transform.SetParent(transform, false);

        // 画面全体に重ねて表示する
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;

        // 画面サイズに合わせてUIを拡大・縮小する
        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        root = go.transform;
    }

    void BuildModeBanner()
    {
        // 画面上部中央にモード表示用の背景を作成する
        var rt = MakePanel("ModeBanner", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -16f), new Vector2(560f, 56f), EditColor);
        modeBg = rt.GetComponent<Image>();

        // モード名と操作方法を表示する
        modeText = MakeText(rt, "Text", 30, TextAnchor.MiddleCenter, Color.white);
        Stretch(modeText.rectTransform, 0f);
    }

    void BuildConstraintPanel()
    {
        // 画面右上に縛り条件表示用の背景を作成する
        var rt = MakePanel("ConstraintPanel", new Vector2(1f, 1f), new Vector2(1f, 1f),
            new Vector2(-16f, -16f), new Vector2(780f, 230f), PanelColor);
        constraintPanel = rt.gameObject;

        // 縛り条件を複数行で表示する
        constraintText = MakeText(rt, "Text", 28, TextAnchor.UpperLeft, Color.white);
        constraintText.supportRichText = true;
        Stretch(constraintText.rectTransform, 14f);
    }

    // 部品を作る道具

    RectTransform MakePanel(string name, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size, Color color)
    {
        // 背景用のオブジェクトを作成する
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(root, false);

        // 表示位置とサイズを設定する
        var rt = (RectTransform)go.transform;
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = pivot;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;

        // 背景色を設定し、マウス操作を妨げないようにする
        var img = go.GetComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return rt;
    }

    Text MakeText(Transform parent, string name, int size, TextAnchor align, Color color)
    {
        // 文字表示用のオブジェクトを作成する
        var go = new GameObject(name, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);

        // 文字のフォントや大きさ、色を設定する
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
        // 親の範囲いっぱいに広げ、余白を設定する
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(pad, pad);
        rt.offsetMax = new Vector2(-pad, -pad);
    }
}