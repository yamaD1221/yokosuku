using UnityEngine;
using UnityEngine.UI;

// 編集モード中に表示するアイテムパレットを管理するクラス
// LevelEditorに登録されているアイテムをボタンとして画面に並べる
public class LevelPalette : MonoBehaviour
{
    // ステージ編集を管理するLevelEditorへの参照
    [SerializeField] private LevelEditor editor;

    // パレットのボタンを配置する親オブジェクト
    // InspectorでPaletteBarを設定する
    [SerializeField] private RectTransform slotContainer;

    // パレット全体の表示・非表示を切り替えるオブジェクト
    [SerializeField] private GameObject bar;

    // パレットの各ボタンの幅と高さ
    [SerializeField] private float slotSize = 80f;

    // オブジェクトが有効になったときに実行される
    void OnEnable()
    {
        // 編集モードの状態変更イベントを登録する
        // 編集モードが切り替わるとOnEditingChangedを呼び出す
        LevelEditor.EditingChanged += OnEditingChanged;
    }

    // オブジェクトが無効になったときに実行される
    void OnDisable()
    {
        // 編集モードの状態変更イベントを解除する
        // 不要なイベント呼び出しを防ぐ
        LevelEditor.EditingChanged -= OnEditingChanged;
    }

    // ゲーム開始時に1回実行される
    void Start()
    {
        // LevelEditorに登録されているアイテムの数だけボタンを生成する
        for (int i = 0; i < editor.ItemCount; i++)
        {
            CreateSlot(i);
        }

        // パレット全体の表示状態を現在の編集モードに合わせる
        // 編集中なら表示し、プレイ中なら非表示にする
        if (bar != null)
            bar.SetActive(LevelEditor.IsEditing);
    }

    // 編集モードの状態が変更されたときに呼び出される
    // editing：trueなら編集モード、falseならプレイモード
    void OnEditingChanged(bool editing)
    {
        // 編集モードのときだけパレットを表示する
        if (bar != null)
            bar.SetActive(editing);
    }

    // 指定された番号のアイテムをパレットのボタンとして生成する
    // index：LevelEditorに登録されているアイテムの番号
    void CreateSlot(int index)
    {
        // ボタン用のGameObjectを生成する
        // RectTransform：UIの位置やサイズを管理する
        // Image：アイテムの画像を表示する
        // PaletteButton：クリックやドラッグ操作を管理する
        var go = new GameObject(
            $"Slot_{editor.GetItemId(index)}",
            typeof(RectTransform),
            typeof(Image),
            typeof(PaletteButton)
        );

        // 生成したボタンをパレットの子オブジェクトにする
        go.transform.SetParent(slotContainer, false);

        // ボタンの幅と高さを設定する
        ((RectTransform)go.transform).sizeDelta =
            new Vector2(slotSize, slotSize);

        // LevelEditorからアイテムの画像と色を取得する
        // アイコンが未設定の場合はPrefabのスプライトが使用される
        editor.GetItemIcon(index, out var sprite, out var color);

        // Imageコンポーネントを取得する
        var img = go.GetComponent<Image>();

        // アイテムの画像を設定する
        img.sprite = sprite;

        // アイテムの画像の色を設定する
        img.color = color;

        // 画像の縦横比を維持して表示する
        img.preserveAspect = true;

        // PaletteButtonを初期化する
        // どのLevelEditorを操作するか、どのアイテムを選択するかを設定する
        go.GetComponent<PaletteButton>().Init(editor, index);
    }
}