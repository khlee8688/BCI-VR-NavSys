using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RosTestCode : MonoBehaviour
{
    [SerializeField] RobotController robotController;
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        StartCoroutine(MoveRobot());
    }

    IEnumerator MoveRobot()
    {
        robotController.MoveForward();
        yield return null;
    }
}
