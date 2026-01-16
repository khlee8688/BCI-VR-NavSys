using UnityEngine;
using UnityEngine.EventSystems;
using System;

public class BoxClickHandler : MonoBehaviour, IPointerClickHandler
{
    public int objectId;
    public static Action<int> OnBoxClicked;

    public void OnPointerClick(PointerEventData eventData)
    {
        OnBoxClicked?.Invoke(objectId);
    }
}