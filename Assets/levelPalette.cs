using UnityEngine;
using UnityEngine.UI;

public class LevelPalette : MonoBehaviour
{
    [SerializeField] private LevelEditor editor;
    [SerializeField] private RectTransform slotContainer;   // PaletteBar
    [SerializeField] private GameObject bar;                // PaletteBar
    [SerializeField] private float slotSize = 80f;

    void OnEnable() { LevelEditor.EditingChanged += OnEditingChanged; }
    void OnDisable() { LevelEditor.EditingChanged -= OnEditingChanged; }

    void Start()
    {
        for (int i = 0; i < editor.ItemCount; i++) CreateSlot(i);
        if (bar != null) bar.SetActive(LevelEditor.IsEditing);
    }

    void OnEditingChanged(bool editing)
    {
        if (bar != null) bar.SetActive(editing);
    }

    void CreateSlot(int index)
    {
        var go = new GameObject($"Slot_{editor.GetItemId(index)}",
            typeof(RectTransform), typeof(Image), typeof(PaletteButton));
        go.transform.SetParent(slotContainer, false);
        ((RectTransform)go.transform).sizeDelta = new Vector2(slotSize, slotSize);

        editor.GetItemIcon(index, out var sprite, out var color);
        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.preserveAspect = true;

        go.GetComponent<PaletteButton>().Init(editor, index);
    }
}