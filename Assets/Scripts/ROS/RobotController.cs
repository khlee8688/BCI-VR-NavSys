using UnityEngine;
using Unity.Robotics.ROSTCPConnector;
using RosMessageTypes.Geometry;

public class RobotController : MonoBehaviour
{
    private ROSConnection ros;
    public string topicName = "/cmd_vel";

    [SerializeField] float linearSpeed = 0.05f;
    [SerializeField] float angularSpeed = 0.1f;

    void Start()
    {
        ros = ROSConnection.GetOrCreateInstance();
        ros.RegisterPublisher<TwistMsg>(topicName);
    }

    void Update()
    {
        if (Input.anyKeyDown) Stop();
    }

    void Publish(float linear, float angular)
    {
        TwistMsg twist = new TwistMsg
        {
            linear = new Vector3Msg(linear, 0, 0),
            angular = new Vector3Msg(0, 0, angular)
        };
        ros.Publish(topicName, twist);
    }

    public void MoveForward()
    {
        Publish(linearSpeed, 0f);
    }

    public void TurnLeft()
    {
        Publish(0f, angularSpeed);
    }

    public void TurnRight()
    {
        Publish(0f, -angularSpeed);
    }

    public void Stop()
    {
        Publish(0f, 0f);
    }

    public float CalculateRotateTime(float angleRad)
    {
        return angleRad;
    }
}
