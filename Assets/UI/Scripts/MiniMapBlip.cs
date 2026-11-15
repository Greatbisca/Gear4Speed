using UnityEngine;

public class MinimapBlip : MonoBehaviour
{
    [Tooltip("An unlit material (Universal Render Pipeline/Unlit). Needed in builds.")]
    public Material material;
    public Color color = new Color(1f, .8f, .1f);
    public bool isPlayer = false;
    [Tooltip("Dot size as a fraction of the map's size")]
    public float size = 0.05f;
    public Camera minimapCamera;

    Transform dot, ring;

    void Start()
    {
        int layer = LayerMask.NameToLayer("MiniMap");
        if (layer < 0) { Debug.LogWarning("MinimapBlip: create a 'MiniMap' layer."); return; }

        if (minimapCamera == null)
        {
            var fit = FindFirstObjectByType<MinimapFitTrack>();
            if (fit != null) minimapCamera = fit.GetComponent<Camera>();
        }

        if (isPlayer) ring = Make(name + "_MapRing", Color.white, layer);
        dot = Make(name + "_MapDot", color, layer);
        Debug.Log("MinimapBlip created for " + name);
    }

    // standalone object (NOT a child of the car), always flat and upright
    Transform Make(string n, Color c, int layer)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = n;
        Destroy(go.GetComponent<Collider>());
        go.layer = layer;
        go.transform.rotation = Quaternion.identity;

        var r = go.GetComponent<Renderer>();
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        if (material != null) r.sharedMaterial = material;
        var mpb = new MaterialPropertyBlock();
        mpb.SetColor("_BaseColor", c);
        mpb.SetColor("_Color", c);
        r.SetPropertyBlock(mpb);
        return go.transform;
    }

    void OnDestroy()
    {
        if (dot != null) Destroy(dot.gameObject);
        if (ring != null) Destroy(ring.gameObject);
    }

    void LateUpdate()
    {
        if (dot == null) return;
        float ortho = minimapCamera != null ? minimapCamera.orthographicSize : 100f;

        float d = ortho * size * (isPlayer ? 1.4f : 1f);
        Vector3 p = transform.position;

        dot.rotation = Quaternion.identity;
        dot.localScale = new Vector3(d, 0.05f, d);
        dot.position = new Vector3(p.x, 80f, p.z);

        if (ring != null)
        {
            float rd = d * 1.5f;
            ring.rotation = Quaternion.identity;
            ring.localScale = new Vector3(rd, 0.05f, rd);
            ring.position = new Vector3(p.x, 70f, p.z);
        }
    }
}