using UnityEngine;

[RequireComponent(typeof(Camera))]
public class CarCameraFollow : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CarController target;

    [Header("Position")]
    [Min(0f)]
    [SerializeField] private float rightOffset = 8f;
    [Min(0f)]
    [SerializeField] private float behindOffset = 6f;
    [Min(0f)]
    [SerializeField] private float heightOffset = 8f;

    [Header("Look")]
    [SerializeField] private float lookAhead = 3f;
    [SerializeField] private float lookHeight = 0.5f;

    [Header("Smoothing")]
    [Min(0f)]
    [SerializeField] private float positionSmoothTime = 0.15f;
    [Min(0f)]
    [SerializeField] private float rotationSmoothSpeed = 8f;

    private Vector3 positionVelocity;

    private void Awake()
    {
        if (target == null)
            target = FindFirstObjectByType<CarController>();
    }

    private void OnValidate()
    {
        rightOffset = Mathf.Max(0f, rightOffset);
        behindOffset = Mathf.Max(0f, behindOffset);
        heightOffset = Mathf.Max(0f, heightOffset);
        positionSmoothTime = Mathf.Max(0f, positionSmoothTime);
        rotationSmoothSpeed = Mathf.Max(0f, rotationSmoothSpeed);
    }

    private void LateUpdate()
    {
        if (target == null)
            return;

        Transform carTransform = target.transform;
        Vector3 desiredPosition =
            carTransform.position +
            carTransform.right * rightOffset -
            carTransform.forward * behindOffset +
            Vector3.up * heightOffset;

        transform.position = Vector3.SmoothDamp(
            transform.position,
            desiredPosition,
            ref positionVelocity,
            positionSmoothTime
        );

        Vector3 lookTarget =
            carTransform.position +
            carTransform.forward * lookAhead +
            Vector3.up * lookHeight;
        Vector3 lookDirection = lookTarget - transform.position;

        if (lookDirection.sqrMagnitude < 0.001f)
            return;

        Quaternion desiredRotation =
            Quaternion.LookRotation(lookDirection.normalized, Vector3.up);
        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            desiredRotation,
            rotationSmoothSpeed * Time.deltaTime
        );
    }
}
