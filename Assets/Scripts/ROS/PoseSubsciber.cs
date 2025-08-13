using RosMessageTypes.Geometry;
using Unity.Robotics.ROSTCPConnector;
using UnityEngine;

public class PoseSubsciber : MonoBehaviour
{
    [SerializeField] private string topicName = "pose";
    [SerializeField] private float publishFrequency = 0.1f;

    [SerializeField] private QuaternionMsg robotOrientation;
    private Quaternion robotQuaternion;

    ROSConnection ros;
    private float timeElapsed = 0.0f;

    void Start()
    {
        ros = ROSConnection.GetOrCreateInstance();
        ros.Subscribe<PoseMsg>(topicName, PoseCallBack);
    }

    private void PoseCallBack(PoseMsg msg)
    {
        robotOrientation = msg.orientation;
        robotQuaternion = new Quaternion((float)robotOrientation.x, (float)robotOrientation.y, (float)robotOrientation.z, (float)robotOrientation.w);

        Debug.Log(robotQuaternion);
    }

    public Quaternion GetRobotQuaternion()
    {
        return robotQuaternion;
    }
}
