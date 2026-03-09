using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class ObjectFilter
{
    public int maxCount = 5;

    List<int> filterObjectIdList = new List<int>();
    bool locked = false;

    public List<ExperimentObject> Filter(List<ExperimentObject> objects)
    {
        var list = objects.ToList();

        if (!locked)
        {
            if (list.Count > maxCount)
            {
                list = RemoveOverlappingLargeOnes(list);
                list = SortByScreenRule(list);
                list = TrimToMax(list);
            }

            filterObjectIdList.Clear();
            foreach (var o in list)
                filterObjectIdList.Add(o.objectId);

            locked = true;
        }
        else
        {
            list = list
                .Where(o => filterObjectIdList.Contains(o.objectId))
                .ToList();
        }

        return list;
    }

    List<ExperimentObject> RemoveOverlappingLargeOnes(List<ExperimentObject> list)
    {
        var result = new List<ExperimentObject>();

        foreach (var o in list.OrderBy(o => o.bbox.width * o.bbox.height))
        {
            bool overlapped = result.Any(r => IoU(r.bbox, o.bbox) > 0.4f);
            if (!overlapped)
                result.Add(o);
        }

        return result;
    }

    List<ExperimentObject> SortByScreenRule(List<ExperimentObject> list)
    {
        return list
            .OrderBy(o => o.bbox.center.sqrMagnitude)
            .ToList();
    }

    List<ExperimentObject> TrimToMax(List<ExperimentObject> list)
    {
        return list.Count <= maxCount
            ? list
            : list.Take(maxCount).ToList();
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
        locked = false;
        filterObjectIdList.Clear();
    }
}
