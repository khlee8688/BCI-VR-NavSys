using UnityEngine;
using System;

public class GazeStabilityDetector : MonoBehaviour
{
    public Camera cam;

    [Header("Lock 조건")]
    public float lockTime = 5f;
    public float lockAngle = 5f;

    [Header("Break 조건")]
    public float breakAngle = 15f;

    public event Action OnLocked;
    public event Action OnBroken;

    Vector3 referenceForward;
    float stableTimer;
    bool locked;

    void Update()
    {
        Vector3 currentForward = cam.transform.forward;

        if (!locked)
        {
            if (stableTimer == 0)
                referenceForward = currentForward;

            float angle = Vector3.Angle(referenceForward, currentForward);

            if (angle <= lockAngle)
            {
                stableTimer += Time.deltaTime;
                if (stableTimer >= lockTime)
                {
                    referenceForward = currentForward;
                    locked = true;
                    OnLocked?.Invoke();
                }
            }
            else
            {
                stableTimer = 0f;
            }
        }
        else
        {
            float angle = Vector3.Angle(referenceForward, currentForward);
            if (angle > breakAngle)
            {
                ResetState();
                OnBroken?.Invoke();
            }
        }
    }

    public void ResetState()
    {
        locked = false;
        stableTimer = 0f;
    }
}