using System.Collections;
using System.Collections.Generic;
using LSL;
using LSL4Unity.Utils;
using UnityEngine;

public class StimulusSender : MonoBehaviour
{
    private string StreamName;
    string StreamType = "Markers";

    #region LSL4Unity_outlet
    int ChannelCount = 1;
    private StreamOutlet stimulationOutlet;
    #endregion

    public virtual bool open(string streamName)
    {
        StreamName = streamName;
        if (!StreamName.Equals(""))
            SetupStimulusOutlet();
        else
        {
            Debug.LogError("Object must specify a name for resolver to lookup a stream");
            this.enabled = false;
            return false;
        }
        return true;
    }

    public virtual void close()
    {
        if (stimulationOutlet != null)
        {
            stimulationOutlet.Dispose();
            stimulationOutlet = null;
            Debug.Log("StimulusSender closed.");
        }
    }

    private void SetupStimulusOutlet()
    {
        string streamName = StreamName + "_Stimulations";
        string uniqueSourceId = gameObject.GetInstanceID().ToString();

        StreamInfo streamInfo_stimulation = new StreamInfo(streamName, StreamType, ChannelCount, 1.0, channel_format_t.cf_float32, uniqueSourceId);
        stimulationOutlet = new StreamOutlet(streamInfo_stimulation);
    }

    public void SendStimulation(string markerValue)
    {
        float[] marker = new float[1]{ 12.1f };
        if (stimulationOutlet != null)
        {
            stimulationOutlet.push_sample(marker);
            Debug.Log("Sent data: " + markerValue);
        }
    }
}