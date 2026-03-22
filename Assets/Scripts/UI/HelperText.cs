using TMPro;
using UnityEngine;
using System.Collections;

public class HelperText : MonoBehaviour
{
    [SerializeField] TMP_Text text;

    Coroutine hideCoroutine;

    public void Show(string message, float duration = 0f)
    {
        if (hideCoroutine != null)
        {
            StopCoroutine(hideCoroutine);
            hideCoroutine = null;
        }

        text.text = message;
        gameObject.SetActive(true);

        if (duration > 0f)
            hideCoroutine = StartCoroutine(HideAfter(duration));
    }

    public void Hide()
    {
        if (hideCoroutine != null)
        {
            StopCoroutine(hideCoroutine);
            hideCoroutine = null;
        }
        gameObject.SetActive(false);
    }

    IEnumerator HideAfter(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        gameObject.SetActive(false);
    }
}