using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class ObjectHighlighter : MonoBehaviour
{
    [Header("UI")]
    public RectTransform canvasRoot;
    [SerializeField] private Sprite fillSprite;
    [SerializeField] private Sprite outlineSprite;
    [SerializeField] private Font font;

    [Header("Style")]
    [SerializeField] private Color normalFillColor = new Color(1, 1, 1, 0);
    [SerializeField] private Color highlightFillColor = new Color(1, 0, 0, 0.2f);
    [SerializeField] private Color normalOutlineColor = Color.white;
    [SerializeField] private Color highlightOutlineColor = Color.red;

    [SerializeField] private UIHighlighter uiHighlighter;

    class BoxUI
    {
        public GameObject root;
        public RectTransform rect;
        public Image fillImage;
        public Image outlineImage;
        public Text label;
        public BoxClickHandler clickHandler;
    }

    private Dictionary<int, BoxUI> boxMap = new();

    // ======================================================

    public void UpdateObjects(List<ExperimentObject> objects)
    {
        foreach (var kv in boxMap)
            kv.Value.root.SetActive(false);

        foreach (var obj in objects)
        {
            if (!boxMap.TryGetValue(obj.objectId, out var box))
            {
                box = CreateBox();
                boxMap[obj.objectId] = box;
            }

            UpdateBoxTransform(box, obj);
            SetBoxColor(box, normalFillColor, false);
            box.root.SetActive(true);
        }
    }

    public void Highlight(int objectId)
    {
        if (uiHighlighter != null &&
            (uiHighlighter.UIOnlyMode || objectId <= 2))
        {
            ClearHighlight();
            uiHighlighter.UIHighlight(objectId);
            return;
        }

        foreach (var kv in boxMap)
        {
            bool active = kv.Key == objectId;
            SetBoxColor(
                kv.Value,
                active ? highlightFillColor : normalFillColor,
                active
            );
        }
    }

    public void ClearHighlight()
    {
        foreach (var kv in boxMap)
            SetBoxColor(kv.Value, normalFillColor, false);

        if (uiHighlighter != null)
            uiHighlighter.Clear();
    }

    public void ClearAll()
    {
        foreach (var kv in boxMap)
            kv.Value.root.SetActive(false);
    }

    // ======================================================

    private void UpdateBoxTransform(BoxUI box, ExperimentObject obj)
    {
        box.rect.anchoredPosition = new Vector2(
            obj.bbox.center.x,
            -obj.bbox.center.y
        );

        box.rect.sizeDelta = obj.bbox.size;

        box.clickHandler.objectId = obj.objectId;
        box.label.text = $"{obj.label} ({obj.objectId})";
    }

    private void SetBoxColor(BoxUI box, Color fillColor, bool highlight)
    {
        if (!box.root.activeSelf) return;

        box.fillImage.color = fillColor;

        box.outlineImage.color = highlight
            ? highlightOutlineColor
            : normalOutlineColor;

        box.label.enabled = highlight;
        box.label.color = highlightOutlineColor;
    }

    private BoxUI CreateBox()
    {
        var panel = new GameObject("ObjectBox");
        panel.transform.SetParent(canvasRoot, false);

        var rect = panel.AddComponent<RectTransform>();
        rect.pivot = new Vector2(0.5f, 0.5f);

        // ================= Fill =================
        var fillGO = new GameObject("Fill");
        fillGO.transform.SetParent(panel.transform, false);

        var fillRect = fillGO.AddComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;

        var fillImg = fillGO.AddComponent<Image>();
        fillImg.sprite = fillSprite;
        fillImg.type = Image.Type.Sliced;
        fillImg.color = normalFillColor;
        fillImg.raycastTarget = false;

        // ================= Outline =================
        var outlineGO = new GameObject("Outline");
        outlineGO.transform.SetParent(panel.transform, false);

        var outlineRect = outlineGO.AddComponent<RectTransform>();
        outlineRect.anchorMin = Vector2.zero;
        outlineRect.anchorMax = Vector2.one;
        outlineRect.offsetMin = Vector2.zero;
        outlineRect.offsetMax = Vector2.zero;

        var outlineImg = outlineGO.AddComponent<Image>();
        outlineImg.sprite = outlineSprite;
        outlineImg.type = Image.Type.Sliced;
        outlineImg.color = normalOutlineColor;
        outlineImg.raycastTarget = true;

        // ================= Click =================
        var click = panel.AddComponent<BoxClickHandler>();

        // ================= Label =================
        var textGO = new GameObject("Label");
        textGO.transform.SetParent(panel.transform, false);

        var txt = textGO.AddComponent<Text>();
        txt.font = font;
        txt.alignment = TextAnchor.UpperLeft;
        txt.horizontalOverflow = HorizontalWrapMode.Overflow;
        txt.verticalOverflow = VerticalWrapMode.Overflow;
        txt.enabled = false;

        var trt = textGO.GetComponent<RectTransform>();
        trt.anchorMin = new Vector2(0, 1);
        trt.anchorMax = new Vector2(0, 1);
        trt.pivot = new Vector2(0, 1);
        trt.anchoredPosition = new Vector2(5, -5);
        trt.sizeDelta = new Vector2(200, 40);

        return new BoxUI
        {
            root = panel,
            rect = rect,
            fillImage = fillImg,
            outlineImage = outlineImg,
            label = txt,
            clickHandler = click
        };
    }

    public GameObject GetBoxFromObjectID(int objectId)
    {
        if (boxMap.TryGetValue(objectId, out var box))
            return box.root;

        return null;
    }

}