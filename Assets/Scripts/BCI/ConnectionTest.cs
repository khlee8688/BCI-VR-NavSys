using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ConnectionTest : MonoBehaviour
{
    [SerializeField] string StreamName = "LSLExample";
    float speed = .1f;
    [SerializeField] StimulusSender theSender;

    // Start is called before the first frame update
    void Start()
    {
        Debug.Log("check");
        StartCoroutine(ConnectionChecker());
    }

    IEnumerator ConnectionChecker()
    {
        int id = 1;

        while (true)
        {
            theSender.SendStimulation((byte)id);
            id++;

            if (id > 10) id = 1; // ¼øÈ¯
            yield return new WaitForSeconds(0.5f);
        }
    }
}
