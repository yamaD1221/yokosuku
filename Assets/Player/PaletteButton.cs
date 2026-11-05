using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// パレットのアイテムをドラッグして、ステージ上に配置するクラス
// ドラッグ中は半透明の画像をマウスに追従させる
public class PaletteButton : MonoBehaviour,
    IBeginDragHandler, IDragHandler, IEndDragHandler
{
    // アイテムの生成や配置を管理するLevelEditorへの参照
    private LevelEditor editor;

    // パレット内で選択されたアイテムの番号
    private int index;

    // ドラッグ中にマウスについてくる半透明の画像
    private GameObject ghost;

    // このボタンが操作するLevelEditorとアイテム番号を設定する
    // e：LevelEditorへの参照
    // i：配置するアイテムの番号
    public void Init(LevelEditor e, int i)
    {
        editor = e;
        index = i;
    }

    // ドラッグを開始した瞬間に呼び出される
    public void OnBeginDrag(PointerEventData e)
    {
        // このボタンが所属しているCanvasを取得する
        var canvas = GetComponentInParent<Canvas>();

        // 元のパレットボタンのImageを取得する
        var src = GetComponent<Image>();

        // ドラッグ中に表示する画像を生成する
        // RectTransform：UIの位置やサイズを管理する
        // Image：アイテムの画像を表示する
        // CanvasGroup：Raycastの反応などを制御する
        ghost = new GameObject(
            "DragGhost",
            typeof(RectTransform),
            typeof(Image),
            typeof(CanvasGroup)
        );

        // 生成した画像をCanvasの子オブジェクトにする
        ghost.transform.SetParent(canvas.transform, false);

        // 元のボタンと同じ大きさに設定する
        ((RectTransform)ghost.transform).sizeDelta =
            ((RectTransform)transform).sizeDelta;

        // 生成した画像のImageを取得する
        var img = ghost.GetComponent<Image>();

        // 元のボタンと同じスプライトを設定する
        img.sprite = src.sprite;

        // 元の画像と同じ色を設定し、透明度を70%にする
        // ドラッグ中の画像だと分かるように半透明にする
        img.color = new Color(
            src.color.r,
            src.color.g,
            src.color.b,
            0.7f
        );

        // 画像の縦横比を維持する
        img.preserveAspect = true;

        // ドラッグ中の画像がUIのRaycastを遮らないようにする
        // これにより、ドロップ先の判定を正常に行える
        ghost.GetComponent<CanvasGroup>().blocksRaycasts = false;

        // ドラッグ開始位置に画像を配置する
        ghost.transform.position = e.position;
    }

    // ドラッグしている間、毎フレーム呼び出される
    public void OnDrag(PointerEventData e)
    {
        // ドラッグ中の画像が存在する場合、
        // マウスの現在位置に追従させる
        if (ghost != null)
            ghost.transform.position = e.position;
    }

    // ドラッグを終了した瞬間に呼び出される
    public void OnEndDrag(PointerEventData e)
    {
        // ドラッグ中に表示していた半透明の画像を削除する
        if (ghost != null)
            Destroy(ghost);

        // ドラッグを終了した場所がUIの上なら配置しない
        // UI上にドロップした場合はキャンセル扱いにする
        if (e.pointerCurrentRaycast.gameObject != null)
            return;

        // マウスのスクリーン座標をワールド座標に変換する
        // これにより、ステージ上の配置位置を取得できる
        Vector2 world = Camera.main.ScreenToWorldPoint(e.position);

        // LevelEditorにアイテム番号と配置位置を渡して、
        // ステージ上にアイテムを生成・配置する
        editor.SpawnFromPalette(index, world);
    }
}