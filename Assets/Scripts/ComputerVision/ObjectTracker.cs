using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class ObjectTracker
{
    int nextObjectId = 0;
    List<ExperimentObject> objects = new();

    public List<ExperimentObject> Update(List<Detection> detections)
    {
        foreach (var obj in objects)
            obj.trackId = -1;

        foreach (var d in detections)
        {
            Rect r = ToRect(d);
            ExperimentObject best = null;
            float bestIoU = 0;

            foreach (var o in objects)
            {
                float iou = IoU(o.bbox, r);
                if (iou > bestIoU)
                {
                    bestIoU = iou;
                    best = o;
                }
            }

            if (bestIoU > 0.3f)
            {
                best.bbox = r;
                best.label = d.label;
            }
            else
            {
                objects.Add(new ExperimentObject
                {
                    objectId = nextObjectId++,
                    label = d.label,
                    bbox = r
                });
            }
        }

        return objects;
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
}