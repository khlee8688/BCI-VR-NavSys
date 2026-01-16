using LSL;
using LSL4Unity.Utils;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Profiling;

public class StimulusSender : MonoBehaviour
{
    private string StreamName;
    string StreamType = "Markers";
    string StreamType2= "Stream";
    int[] sample = new int[1];

    #region LSL4Unity_outlet
    int ChannelCount = 1;
    private StreamOutlet markerOutlet;
    private StreamOutlet signalOutlet; // marker stream만으로는 연결이 안 됨. signal stream이 무조건 필요.
    #endregion

    void Start()
    {
        string streamName = StreamName + "_Stimulations";

        var hash = new Hash128();
        hash.Append("P300Stimulus");
        hash.Append(gameObject.GetInstanceID());

        StreamInfo streamInfo_stimulation = new StreamInfo(streamName, StreamType, ChannelCount, LSL.LSL.IRREGULAR_RATE, channel_format_t.cf_int32, hash.ToString());
        markerOutlet = new StreamOutlet(streamInfo_stimulation);

        StreamInfo streamInfo_stream = new StreamInfo(streamName, StreamType2, ChannelCount, LSL.LSL.IRREGULAR_RATE, channel_format_t.cf_float32, hash.ToString());
        signalOutlet = new StreamOutlet(streamInfo_stream);
    }

    private void Update()
    {
        signalOutlet.push_sample(sample); // siganl stream의 sample frequency가 0이어도 실행이 안 되는 문제 해결을 위한 코드 (보낸 값은 사용 안 됨)
    }

    public void SendStimulation(byte instanceID)
    {
        sample[0] = instanceID;
        if (markerOutlet != null)
        {
            markerOutlet.push_sample(sample);
            // Debug.Log("Sent data: " + instanceID);
        }
    }
}