using UnityEngine;

public class CarVisual : MonoBehaviour
{
    [SerializeField] CarCatalog catalog;
    [SerializeField] float yOffset = -0.5f;   // Player sits 0.5 above the road, the car pivot is on the ground

    void Start()
    {
        if (catalog == null || catalog.cars.Length == 0) return;
        int i = Mathf.Clamp(GameSelection.CarIndex, 0, catalog.cars.Length - 1);
        var prefab = catalog.cars[i].prefab;
        if (prefab == null) return;

        var go = Instantiate(prefab, Vector3.zero, Quaternion.identity);
        CarFit.CenterAtOrigin(go);
        go.transform.SetParent(transform, false);
        go.transform.localPosition += new Vector3(0f, yOffset, 0f);

        var mr = GetComponent<MeshRenderer>();
        if (mr != null) mr.enabled = false;   // hide the placeholder cube
    }
}