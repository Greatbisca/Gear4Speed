using UnityEngine;

public static class CarFit
{
    // Centres the car on the origin with its wheels on y = 0.
    public static void CenterAtOrigin(GameObject car)
    {
        var rends = car.GetComponentsInChildren<Renderer>();
        if (rends.Length == 0) return;

        Bounds b = rends[0].bounds;
        for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);

        car.transform.position += new Vector3(-b.center.x, -b.min.y, -b.center.z);
    }

    // Scales the car so its largest dimension is targetSize metres, then centres it.
    public static void Fit(GameObject car, float targetSize = 4.5f)
    {
        var rends = car.GetComponentsInChildren<Renderer>();
        if (rends.Length == 0) return;

        Bounds b = rends[0].bounds;
        for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);

        float largest = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
        if (largest > 0.0001f)
            car.transform.localScale *= targetSize / largest;

        CenterAtOrigin(car);
    }
}