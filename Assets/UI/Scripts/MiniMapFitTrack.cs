using UnityEngine;
using UnityEngine.Splines;

[RequireComponent(typeof(Camera))]
public class MinimapFitTrack : MonoBehaviour
{
    public RaceSplinePath track;                 // drag RaceSplicePath here
    [Range(1f, 1.5f)] public float margin = 1.1f;
    public float height = 300f;                  // camera height above the track
    public bool roundMap = true;                 // true = the minimap is a circle

    void Start()
    {
        if (track == null) track = FindFirstObjectByType<RaceSplinePath>();
        Fit();
    }

    [ContextMenu("Fit now")]
    public void Fit()
    {
        if (track == null || track.segments == null) return;

        bool any = false;
        Bounds b = new Bounds();
        foreach (var seg in track.segments)
        {
            if (seg == null) continue;
            var c = seg.Spline != null ? seg.Spline : seg.GetComponent<SplineContainer>();
            if (c == null) continue;

            for (int i = 0; i <= 24; i++)
            {
                Vector3 p = c.transform.TransformPoint(
                    SplineUtility.EvaluatePosition(c.Spline, i / 24f));
                if (!any) { b = new Bounds(p, Vector3.zero); any = true; }
                else b.Encapsulate(p);
            }
        }
        if (!any) return;

        var cam = GetComponent<Camera>();
        cam.orthographic = true;

        // a round map needs the corners of the box to fit inside the circle
        float half = roundMap
            ? new Vector2(b.extents.x, b.extents.z).magnitude
            : Mathf.Max(b.extents.x, b.extents.z);
        cam.orthographicSize = half * margin;

        transform.position = new Vector3(b.center.x, b.center.y + height, b.center.z);
        transform.rotation = Quaternion.Euler(90f, 0f, 0f);
    }
}