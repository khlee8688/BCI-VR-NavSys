using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UIHighlighter : MonoBehaviour
{
    [Header("Button")]
    [SerializeField] Button arrowButton;

    [Header("UI Only Mode")]
    [SerializeField] Canvas targetCanvas; // Object_2~6 이 달린 Canvas

    [Header("Style")]
    [SerializeField] private Color normalColor = Color.clear;
    [SerializeField] private Color highlightColor = Color.red;
    public bool UIOnlyMode = false;

    List<Image> images;

    private void Start()
    {
        images = new List<Image>();

        // 기본 버튼들
        images.Add(arrowButton.transform.Find("HighlightImage").GetComponent<Image>());

        // UI Only Mode: Canvas child 이미지 추가
        if (UIOnlyMode && targetCanvas != null)
        {
            for (int i = 2; i <= 6; i++)
            {
                var img = targetCanvas
                    .transform
                    .Find($"Object_{i}/HighlightImage")
                    ?.GetComponent<Image>();

                if (img != null)
                    images.Add(img);
            }
        }
    }

    public void UIHighlight(int objectId)
    {
        for (int i = 0; i < images.Count; i++)
            images[i].color = normalColor;

        int idx = objectId - 1;
        if (idx >= 0 && idx < images.Count)
            images[idx].color = highlightColor;
    }

    public void Clear()
    {
        foreach (var img in images)
            img.color = normalColor;
    }
}