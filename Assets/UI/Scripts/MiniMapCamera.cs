using UnityEngine;

public class MinimapCamera : MonoBehaviour
{
    public Transform target;                 // drag the Player here
    public float height = 100f;
    public bool rotateWithTarget = true;     // true = the car always points up on the map

    void LateUpdate()
    {
        if (target == null) return;
        Vector3 p = target.position;
        transform.position = new Vector3(p.x, p.y + height, p.z);
        float yaw = rotateWithTarget ? target.eulerAngles.y : 0f;
        transform.rotation = Quaternion.Euler(90f, yaw, 0f);
    }
}