using System;
using UnityEngine;

[Serializable]
public class ExperimentObject
{
    public int objectId;          // P300에서 쓰는 고정 ID
    public int trackId;           // tracker 내부 ID (변해도 됨)
    public string label;
    public Rect bbox;             // screen space
}