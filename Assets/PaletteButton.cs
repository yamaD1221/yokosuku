using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class PaletteButton : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    private LevelEditor editor;
    private int index;
    private GameObject ghost;

    public void Init(LevelEditor e, int i)
    {
        editor = e;
        index = i;
    }

    public void OnBeginDrag(PointerEventData e)
    {
        var canvas = GetComponentInParent<Canvas>();
        var src = GetComponent<Image>();

        // ドラッグ中についてくる半透明の画像
        ghost = new GameObject("DragGhost", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
        ghost.transform.SetParent(canvas.transform, false);
        ((RectTransform)ghost.transform).sizeDelta = ((RectTransform)transform).sizeDelta;

        var img = ghost.GetComponent<Image>();
        img.sprite = src.sprite;
        img.color = new Color(src.color.r, src.color.g, src.color.b, 0.7f);
        img.preserveAspect = true;
        ghost.GetComponent<CanvasGroup>().blocksRaycasts = false;

        ghost.transform.position = e.position;
    }

    public void OnDrag(PointerEventData e)
    {
        if (ghost != null) ghost.transform.position = e.position;
    }

    public void OnEndDrag(PointerEventData e)
    {
        if (ghost != null) Destroy(ghost);

        // UIの上で離したときは、置かずにキャンセル
        if (e.pointerCurrentRaycast.gameObject != null) return;

        Vector2 world = Camera.main.ScreenToWorldPoint(e.position);
        editor.SpawnFromPalette(index, world);
    }
}