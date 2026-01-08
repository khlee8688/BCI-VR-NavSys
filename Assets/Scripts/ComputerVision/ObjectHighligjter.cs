using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class ObjectHighlighter : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private RectTransform canvasRoot;
    [SerializeField] private Sprite borderSprite;
    [SerializeField] private Font font;

    [Header("Style")]
    [SerializeField] private Color normalColor = Color.clear;
    [SerializeField] private Color highlightColor = Color.red;

    private Dictionary<int, GameObject> boxMap = new Dictionary<int, GameObject>();

    public void UpdateObjects(List<ExperimentObject> objects)
    {
        // ÀüºÎ ¼û±è
        foreach (var kv in boxMap)
            kv.Value.SetActive(false);

        foreach (var obj in objects)
        {
            if (!boxMap.TryGetValue(obj.objectId, out var box))
            {
                box = CreateBox();
                boxMap[obj.objectId] = box;
            }

            UpdateBoxTransform(box, obj);
            SetBoxColor(box, normalColor, false);
            box.SetActive(true);
        }
    }

    public void Highlight(int objectId)
    {
        foreach (var kv in boxMap)
        {
            bool active = kv.Key == objectId;
            SetBoxColor(kv.Value, active ? highlightColor : normalColor, active);
        }
    }

    public void ClearHighlight()
    {
        foreach (var kv in boxMap)
            SetBoxColor(kv.Value, normalColor, false);
    }

    public void ClearAll()
    {
        foreach (var kv in boxMap)
            kv.Value.SetActive(false);
    }


    void UpdateBoxTransform(GameObject box, ExperimentObject obj)
    {
        RectTransform rt = box.GetComponent<RectTransform>();
        Rect canvasRect = canvasRoot.rect;

        float x = obj.bbox.center.x - canvasRect.width / 2f;
        float y = canvasRect.height / 2f - obj.bbox.center.y;

        rt.anchoredPosition = new Vector2(x, y);
        rt.sizeDelta = obj.bbox.size;

        Text label = box.GetComponentInChildren<Text>();
        label.text = $"{obj.label} ({obj.objectId})";
    }

    void SetBoxColor(GameObject box, Color color, bool showLabel)
    {
        if (!box.activeSelf) return;

        var img = box.GetComponent<Image>();
        var txt = box.GetComponentInChildren<Text>();

        if (img != null) img.color = color;
        if (txt != null)
        {
            txt.enabled = showLabel;
            txt.color = highlightColor;
        }
    }

    GameObject CreateBox()
    {
        var panel = new GameObject("ObjectBox");
        panel.transform.SetParent(canvasRoot, false);

        panel.AddComponent<CanvasRenderer>();

        var img = panel.AddComponent<Image>();
        img.sprite = borderSprite;
        img.type = Image.Type.Sliced;
        img.color = normalColor;

        var rt = panel.GetComponent<RectTransform>();
        rt.pivot = new Vector2(0.5f, 0.5f);

        var textGO = new GameObject("Label");
        textGO.transform.SetParent(panel.transform, false);

        textGO.AddComponent<CanvasRenderer>();
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

        return panel;
    }
}