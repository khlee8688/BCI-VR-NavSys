using UnityEngine;
using Unity.Robotics.ROSTCPConnector;
using RosMessageTypes.Geometry;

public class WASDPublisher : MonoBehaviour
{
    ROSConnection ros;
    public string topicName = "/cmd_vel";
    public float linearSpeed = 0.5f;
    public float angularSpeed = 1.0f;

    void Start()
    {
        ros = ROSConnection.GetOrCreateInstance();
        ros.RegisterPublisher<TwistMsg>(topicName);
    }

    void Update()
    {
        Vector3 linear = Vector3.zero;
        Vector3 angular = Vector3.zero;

        if (Input.GetKey(KeyCode.W))
            linear.z += linearSpeed;
        if (Input.GetKey(KeyCode.S))
            linear.z -= linearSpeed;
        if (Input.GetKey(KeyCode.A))
            angular.y += angularSpeed;
        if (Input.GetKey(KeyCode.D))
            angular.y -= angularSpeed;

        TwistMsg twist = new TwistMsg
        {
            linear = new Vector3Msg(linear.x, linear.y, linear.z),
            angular = new Vector3Msg(angular.x, angular.y, angular.z)
        };

        ros.Publish(topicName, twist);
    }
}