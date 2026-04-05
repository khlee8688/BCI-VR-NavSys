using UnityEngine;
using Unity.Robotics.ROSTCPConnector;
using RosMessageTypes.Geometry;

public class JoystickRobotController : MonoBehaviour
{
    [SerializeField] RobotController robotController;
    [SerializeField] float deadzone = 0.2f;

    [Header("Speed")]
    [SerializeField] float linearSpeed = 0.1f;
    [SerializeField] float angularSpeed = 0.3f;

    [Header("Toggle")]
    [SerializeField] bool manualControlEnabled = false;

    void Update()
    {
        if (!manualControlEnabled) return;

        Vector2 rightStick = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, OVRInput.Controller.RTouch);

        float linear = 0f;
        float angular = 0f;

        if (Mathf.Abs(rightStick.y) > deadzone)
            linear = rightStick.y * linearSpeed;

        if (Mathf.Abs(rightStick.x) > deadzone)
            angular = -rightStick.x * angularSpeed;

        if (linear != 0f || angular != 0f)
            robotController.PublishMoveRaw(linear, angular);
        else
            robotController.Stop();
    }

    public void SetManualControl(bool enabled)
    {
        manualControlEnabled = enabled;
    }
}