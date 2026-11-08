using UnityEngine;

// Invisible camera target: sits on the car and faces the direction it is travelling.
// Ignores the car's own rotation, so a tilted or rotated model can't affect the camera.
public class ChaseTarget : MonoBehaviour
{
    public Transform car;                              // drag the Player here
    [Tooltip("0 = locked to the road direction, higher = a little softer")]
    [Range(0f, 40f)] public float turnSmoothing = 20f;
    public float minMoveSpeed = 0.3f;                  // below this speed the heading is held
    [Tooltip("Only used before the car moves. Try 90 / -90 / 180 if the start is wrong.")]
    public float startYaw = 0f;

    Vector3 lastPos;
    bool hasHeading;

    void Start()
    {
        if (car == null) return;
        lastPos = car.position;
        transform.position = car.position;
        transform.rotation = Quaternion.Euler(0f, startYaw, 0f);
        Vector3 f = Vector3.ProjectOnPlane(car.forward, Vector3.up);
        if (f.sqrMagnitude > 0.05f)
            transform.rotation = Quaternion.LookRotation(f.normalized, Vector3.up);
    }

    void LateUpdate()
    {
        if (car == null) return;

        Vector3 move = car.position - lastPos;
        move.y = 0f;
        lastPos = car.position;
        transform.position = car.position;

        if (Time.deltaTime <= 0f) return;
        if (move.magnitude / Time.deltaTime < minMoveSpeed) return;

        Quaternion want = Quaternion.LookRotation(move.normalized, Vector3.up);
        transform.rotation = (turnSmoothing <= 0f || !hasHeading)
            ? want
            : Quaternion.Slerp(transform.rotation, want, 1f - Mathf.Exp(-turnSmoothing * Time.deltaTime));
        hasHeading = true;
    }
}