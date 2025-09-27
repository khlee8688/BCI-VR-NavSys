using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ConnectionTest : MonoBehaviour
{
    [SerializeField] string StreamName = "LSLExample";
    float speed = .1f;
    [SerializeField] StimulusReceiver theReceiver;
    [SerializeField] StimulusSender theSender;
    bool r_checker, s_checker;

    // Start is called before the first frame update
    void Start()
    {
        Debug.Log("check");
        r_checker = theReceiver.open(StreamName);
        s_checker = theSender.open(StreamName);
        if (r_checker && s_checker)
        {
            StartCoroutine(ConnectionChecker());
            Debug.Log("OpenVIBE Connected!!");
        }
        else
            Debug.Log("r: " + r_checker + "\ns: " + s_checker);
    }

    IEnumerator ConnectionChecker()
    {
        while (true)
        {
            theSender.SendStimulation("Object1");
            string rd = theReceiver.ReceiveStimulation();
            Debug.Log(rd);
            yield return new WaitForSeconds(speed);
        }
    }
}
