using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class ObjectTracker
{
    // 기준 오브젝트 저장 (trackId와 objectId 포함)
    private Dictionary<int, ExperimentObject> referenceObjects = new Dictionary<int, ExperimentObject>();

    // 기준 오브젝트 중 하나라도 사라지면 호출
    public Action OnReferenceLost;

    /// <summary>
    /// Update 호출 시,
    /// - referenceObjects가 없으면 처음 들어온 탐지로 reference 설정
    /// - referenceObjects가 있으면 bbox만 갱신, 새 객체는 무시
    /// </summary>
    public List<ExperimentObject> Update(List<Detection> detections)
    {
        // 1. Reference가 없으면 처음 들어온 탐지로 설정
        if (referenceObjects.Count == 0 && detections.Count > 0)
        {
            int nextObjectId = 0;
            int nextTrackId = 0;
            foreach (var d in detections)
            {
                var obj = new ExperimentObject
                {
                    objectId = nextObjectId++,
                    trackId = nextTrackId++,
                    label = d.label,
                    bbox = ToRect(d)
                };
                referenceObjects[obj.objectId] = obj;
            }
            return referenceObjects.Values.ToList();
        }

        // 2. Reference가 이미 있으면 bbox 갱신
        HashSet<int> detectedIds = new HashSet<int>();

        foreach (var d in detections)
        {
            Rect r = ToRect(d);

            // 기존 reference 중 가장 IoU가 높은 것과 매칭
            ExperimentObject best = null;
            float bestIoU = 0f;

            foreach (var o in referenceObjects.Values)
            {
                float iou = IoU(o.bbox, r);
                if (iou > bestIoU)
                {
                    bestIoU = iou;
                    best = o;
                }
            }

            if (best != null && bestIoU > 0.3f)
            {
                best.bbox = r;
                best.label = d.label;
                detectedIds.Add(best.objectId);
            }
        }

        // 3. 기준 오브젝트 중 하나라도 없으면 이벤트 호출
        bool lostAny = referenceObjects.Keys.Except(detectedIds).Any();
        if (lostAny)
        {
            OnReferenceLost?.Invoke();
        }

        return referenceObjects.Values.ToList();
    }

    Rect ToRect(Detection d)
    {
        return new Rect(
            d.cx - d.w / 2f,
            d.cy - d.h / 2f,
            d.w,
            d.h
        );
    }

    float IoU(Rect a, Rect b)
    {
        float x1 = Mathf.Max(a.xMin, b.xMin);
        float y1 = Mathf.Max(a.yMin, b.yMin);
        float x2 = Mathf.Min(a.xMax, b.xMax);
        float y2 = Mathf.Min(a.yMax, b.yMax);
        float inter = Mathf.Max(0, x2 - x1) * Mathf.Max(0, y2 - y1);
        float uni = a.width * a.height + b.width * b.height - inter;
        return uni > 0 ? inter / uni : 0;
    }

    public void Reset()
    {
        referenceObjects.Clear();
    }
}