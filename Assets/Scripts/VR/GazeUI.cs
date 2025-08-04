using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GazeUI : MonoBehaviour
{
    [SerializeField] GameObject cursor;
    // Start is called before the first frame update
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {
        var mainCamera = Camera.main;
        var cameraPos = mainCamera.transform.position;
        var cameraRot = mainCamera.transform.rotation;
        var cursorLocalPos = 2f * Vector3.forward;

        cursor.transform.position = cameraPos + cameraRot * cursorLocalPos;
    }
}
