using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;

public class MinimapTrackLine : MonoBehaviour
{
    public RaceSplinePath track;                 // drag RaceSplicePath here
    public Camera minimapCamera;                 // drag MinimapCamera here
    [Tooltip("Unlit material. MinimapBlipMat works.")]
    public Material material;
    public Color color = new Color(.85f, .9f, 1f);
    [Tooltip("Line width as a fraction of the map size")]
    public float width = 0.03f;
    public int samplesPerSegment = 12;

    LineRenderer line;

    void Start()
    {
        if (track == null) track = FindFirstObjectByType<RaceSplinePath>();
        if (minimapCamera == null) minimapCamera = GetComponent<Camera>();

        int layer = LayerMask.NameToLayer("MiniMap");
        if (layer < 0) { Debug.LogWarning("MinimapTrackLine: create a 'MiniMap' layer."); return; }

        var points = new List<Vector3>();
        foreach (var seg in track.segments)
        {
            if (seg == null) continue;
            var c = seg.Spline != null ? seg.Spline : seg.GetComponent<SplineContainer>();
            if (c == null) continue;
            for (int i = 0; i <= samplesPerSegment; i++)
            {
                Vector3 p = c.transform.TransformPoint(
                    SplineUtility.EvaluatePosition(c.Spline, i / (float)samplesPerSegment));
                points.Add(new Vector3(p.x, 60f, p.z));   // above the ground, below the blips
            }
        }
        if (points.Count < 2) return;

        var go = new GameObject("MinimapTrackLine") { layer = layer };
        line = go.AddComponent<LineRenderer>();
        line.positionCount = points.Count;
        line.SetPositions(points.ToArray());
        line.loop = true;
        line.alignment = LineAlignment.TransformZ;     // the line lies flat
        go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.numCornerVertices = 4;
        if (material != null) line.sharedMaterial = material;
        line.startColor = line.endColor = color;
        var mpb = new MaterialPropertyBlock();
        mpb.SetColor("_BaseColor", color);
        mpb.SetColor("_Color", color);
        line.SetPropertyBlock(mpb);
    }

    void LateUpdate()
    {
        if (line == null || minimapCamera == null) return;
        float w = minimapCamera.orthographicSize * width;
        line.startWidth = line.endWidth = w;
    }
}