using LSL;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class StimulusReceiver : MonoBehaviour
{
    public string StreamName;
    ContinuousResolver resolver;

    double max_chunk_duration = 0.2;

    private StreamInlet inlet;

    private float[,] data_buffer;
    private double[] timestamp_buffer;

    public bool open(string streamName)
    {
        StreamName = streamName;
        if (!StreamName.Equals(""))
            resolver = new ContinuousResolver("name", StreamName);
        else
        {
            Debug.LogError("Object must specify a name for resolver to lookup a stream");
            this.enabled = false;
            return false;
        }
        StartCoroutine(ResolveExpectedStream());
        return true;
    }

    IEnumerator ResolveExpectedStream()
    {
        var results = resolver.results();
        while(results.Length == 0)
        {
            yield return new WaitForSeconds(.1f);
            results = resolver.results();
        }

        inlet = new StreamInlet(results[0]);

        int buf_samples = (int)Mathf.Ceil((float)(inlet.info().nominal_srate() * max_chunk_duration));
        int n_channels = inlet.info().channel_count();
        data_buffer = new float[buf_samples, n_channels];
        timestamp_buffer = new double[buf_samples];
    }

    public virtual string ReceiveStimulation()
    {
        string responseData = string.Empty;

        if (inlet != null)
        {
            int samples_returned = inlet.pull_chunk(data_buffer, timestamp_buffer);
            if (samples_returned > 0)
            {
                responseData = data_buffer[samples_returned - 1, 0].ToString();
            }
        }

        return responseData;
    }
}
