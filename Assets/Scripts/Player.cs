using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Player : MonoBehaviour
{
    private float initialYAngle;
    // Start is called before the first frame update
    void Start()
    {
        initialYAngle = 0;
    }

    // Update is called once per frame
    void Update()
    {
        var mainCamera = Camera.main;
        var cameraRot = mainCamera.transform.rotation;
    }
}
