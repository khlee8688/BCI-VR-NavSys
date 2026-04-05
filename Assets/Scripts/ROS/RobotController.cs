using UnityEngine;
using Unity.Robotics.ROSTCPConnector;
using RosMessageTypes.Geometry;
using RosMessageTypes.Std;
using System;

public class RobotController : MonoBehaviour
{
    public static event Action OnArrived;

    private ROSConnection ros;

    [Header("ROS Topics")]
    public string controlTopicName = "/cmd_vel";
    public string depthTopicName = "/safety_reset";
    public string depthStateTopicName = "/safety_state";

    BoolMsg resetMsg = new BoolMsg(true);

    // volatile: 여러 스레드에서 동시에 접근할 때 캐싱 방지
    private volatile bool arrivedFlag = false;

    void Start()
    {
        ros = ROSConnection.GetOrCreateInstance();

        // 에러 방지: 토픽 이름이 비어있으면 등록 안 함
        if (!string.IsNullOrEmpty(controlTopicName))
            ros.RegisterPublisher<TwistMsg>(controlTopicName);

        if (!string.IsNullOrEmpty(depthTopicName))
            ros.RegisterPublisher<BoolMsg>(depthTopicName);

        if (!string.IsNullOrEmpty(depthStateTopicName))
            ros.Subscribe<StringMsg>(depthStateTopicName, OnSafetyState);
    }

    void Update()
    {
        if (arrivedFlag)
        {
            arrivedFlag = false;
            Debug.Log("[RobotController] Handling Arrival on Main Thread");
            OnArrived?.Invoke();
        }
    }

    // 이 함수는 백그라운드 스레드에서 호출될 수 있음
    void OnSafetyState(StringMsg msg)
    {
        // msg 자체가 null인 경우 방지
        if (msg == null) return;

        if (msg.data == "ARRIVED")
        {
            // 백그라운드 스레드에서 UI 접근 절대 금지. 플래그만 세움.
            arrivedFlag = true;
        }
    }

    public void PublishReset()
    {
        ros.Publish(depthTopicName, resetMsg);
    }

    void PublishMove(float linear, float angular)
    {
        if (ros.HasConnectionError)
        {
            Debug.LogWarning("[RobotController] ROS connection error, skipping");
            return;
        }

        TwistMsg twist = new TwistMsg
        {
            linear = new Vector3Msg(linear, 0, 0),
            angular = new Vector3Msg(0, 0, angular)
        };
        ros.Publish(controlTopicName, twist);
    }

    public void PublishMoveRaw(float linear, float angular)
    {
        if (ros.HasConnectionError) return;

        TwistMsg twist = new TwistMsg
        {
            linear = new Vector3Msg(linear, 0, 0),
            angular = new Vector3Msg(0, 0, angular)
        };
        ros.Publish(controlTopicName, twist);
    }

    public void MoveForward() => PublishMove(0.05f, 0f);
    public void TurnLeft() => PublishMove(0f, 0.1f);
    public void TurnRight() => PublishMove(0f, -0.1f);
    public void Stop() => PublishMove(0f, 0f);
}