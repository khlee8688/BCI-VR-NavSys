using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections.Generic;

public class ControllerRaycaster : MonoBehaviour
{
    [SerializeField] Transform controllerTransform; // RightControllerAnchor 연결
    [SerializeField] Camera uiCamera;               // CenterEyeAnchor 카메라 연결
    [SerializeField] Canvas canvas;
    [SerializeField] float rayLength = 10f;
    [SerializeField] LineRenderer lineRenderer;     // 선택사항

    void Update()
    {
        // 오른쪽 트리거 눌렀을 때
        if (OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger, OVRInput.Controller.RTouch))
        {
            Debug.Log("Controller Triggered");
            TryClick();
        }

        // Ray 시각화 (LineRenderer 있을 때)
        if (lineRenderer != null)
        {
            lineRenderer.SetPosition(0, controllerTransform.position);
            lineRenderer.SetPosition(1, controllerTransform.position + controllerTransform.forward * rayLength);
        }
    }

    void TryClick()
    {
        Ray ray = new Ray(controllerTransform.position, controllerTransform.forward);

        // GraphicRaycaster로 UI 히트 체크
        PointerEventData ped = new PointerEventData(EventSystem.current);
        ped.position = uiCamera.WorldToScreenPoint(controllerTransform.position + controllerTransform.forward * 0.1f);

        var results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(ped, results);

        foreach (var result in results)
        {
            var handler = result.gameObject.GetComponent<BoxClickHandler>();
            if (handler != null)
            {
                handler.OnPointerClick(ped);
                return;
            }
        }
    }
}