using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UIHighlighter : MonoBehaviour
{
    [Header("Button")]
    [SerializeField] Button arrowButton;
    [SerializeField] Button exitButton;

    [Header("Style")]
    [SerializeField] private Color normalColor = Color.clear;
    [SerializeField] private Color highlightColor = Color.red;

    List<Image> images;

    private void Start()
    {
        images = new List<Image>();
        images.Add(arrowButton.transform.Find("HighlightImage").GetComponent<Image>());
        images.Add(exitButton.transform.Find("HighlightImage").GetComponent<Image>());
    }

    public void UIHighlight(int objectId)
    {
        for (int i = 0; i < images.Count; i++)
        {
            images[i].color = normalColor;
        }

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
