using UnityEngine;
using Unity.Robotics.ROSTCPConnector;
using RosMessageTypes.Geometry;

public class WASDPublisher : MonoBehaviour
{
    private ROSConnection ros;
    public string topicName = "/cmd_vel";

    private float linearVelocity = 0.0f;
    private float angularVelocity = 0.0f;

    void Start()
    {
        ros = ROSConnection.GetOrCreateInstance();
        ros.RegisterPublisher<TwistMsg>(topicName);
    }

    void Update()
    {
        if (Input.GetKey(KeyCode.W)) linearVelocity += 0.05f;
        if (Input.GetKey(KeyCode.X)) linearVelocity -= 0.05f;
        if (Input.GetKey(KeyCode.A)) angularVelocity += 0.1f;
        if (Input.GetKey(KeyCode.D)) angularVelocity -= 0.1f;
        if (Input.GetKey(KeyCode.S))
        {
            linearVelocity = 0;
            angularVelocity = 0;
        }
        // 속도 제한
        linearVelocity = Mathf.Clamp(linearVelocity, -0.05f, 0.05f);
        angularVelocity = Mathf.Clamp(angularVelocity, -0.1f, 0.1f);

        // Twist 메시지 생성
        TwistMsg twist = new TwistMsg();
        twist.linear = new Vector3Msg(linearVelocity, 0, 0);
        twist.angular = new Vector3Msg(0, 0, angularVelocity);

        // 퍼블리시
        ros.Publish(topicName, twist);
    }
}