using UnityEngine;

public static class CarFit
{
    // Call on a car that has NO parent yet and no rotation.
    // Moves it so its visual centre is on the origin and its wheels touch y = 0.
    public static void CenterAtOrigin(GameObject car)
    {
        var rends = car.GetComponentsInChildren<Renderer>();
        if (rends.Length == 0) return;

        Bounds b = rends[0].bounds;
        for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);

        car.transform.position += new Vector3(-b.center.x, -b.min.y, -b.center.z);
    }
}