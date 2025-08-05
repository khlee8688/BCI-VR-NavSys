using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GazeUI : MonoBehaviour
{
    GameObject cursor;
    GameObject target;
    Renderer targetRender;
    Color originalTargetColor;
    public GameObject highlightEffect;
    GameObject highlightEffectObject;

    // Start is called before the first frame update
    void Start()
    {
        cursor = GameObject.Find("Gaze Cursor");
        target = GameObject.Find("Target");
        targetRender = target.GetComponent<Renderer>();
        originalTargetColor = targetRender.material.color;
        highlightEffectObject = null;
    }

    // Update is called once per frame
    void Update()
    {
        var mainCamera = Camera.main;
        var cameraPos = mainCamera.transform.position;
        var cameraRot = mainCamera.transform.rotation;
        var cursorLocalPos = 2f * Vector3.forward;

        cursor.transform.position = cameraPos + cameraRot * cursorLocalPos;

        var cursorPos = cursor.transform.position;
        var ray = new Ray(cameraPos, cursorPos - cameraPos);

        RaycastHit hit;
        if(Physics.Raycast(ray, out hit))
        {
            if(hit.collider.gameObject == target)
            {
                targetRender.material.color = originalTargetColor * 2f;
                if(highlightEffectObject == null)
                {
                    var targetPos = target.transform.position;
                    var targetRot = target.transform.rotation;
                    highlightEffectObject = Instantiate(highlightEffect, targetPos, targetRot);
                }
            }
        }
        else
        {
            targetRender.material.color = originalTargetColor;
            if( highlightEffectObject != null)
            {
                Destroy(highlightEffectObject);
                highlightEffectObject = null;
            }
        }
    }
}
