using UnityEngine;

public class SetLayerRecursive : MonoBehaviour
{
    public string layerName = "Track";

    [ContextMenu("Apply")]
    void Apply()
    {
        int l = LayerMask.NameToLayer(layerName);
        if (l < 0) { Debug.LogWarning("Create the layer first."); return; }
        foreach (var t in GetComponentsInChildren<Transform>(true))
            t.gameObject.layer = l;
        Debug.Log("Layer set on " + name + " and all its children.");
    }
}