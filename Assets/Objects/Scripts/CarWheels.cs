using System.Collections.Generic;
using UnityEngine;

public class CarWheels : MonoBehaviour
{
    class Wheel
    {
        public Transform t;
        public bool front;
        public bool left;
        public float radius;
        public float angle;
        public Quaternion initialRot;
    }

    [Header("Nome do carro (vazio = nome do objeto)")]
    public string carName = "";

    [Header("Movimento")]
    [Tooltip("Vazio = pai, se existir")]
    public Transform motionSource;

    [Header("Rotação")]
    public Vector3 spinAxis = Vector3.right;
    public Vector3 steerAxis = Vector3.forward;
    public bool invertSpin = false;
    public bool invertLeftSide = false;

    [HideInInspector] public float steerAngle;   // preenchido pelo WheelSteering

    [Header("Diagnóstico (só leitura)")]
    public int wheelsFound;
    public float speedDebug;

    readonly List<Wheel> wheels = new List<Wheel>();
    ISpeedSource speedSource;
    Vector3 lastPos;

    public float WheelBase { get; private set; } = 2.5f;
    public float SignedSpeed { get; private set; }

    void Awake()
    {
        if (motionSource == null)
            motionSource = transform.parent != null ? transform.parent : transform;

        // PlayerController ou AIController (ambos implementam ISpeedSource)
        speedSource = GetComponentInParent<ISpeedSource>();
    }

    void Start()
    {
        // Corre depois do Instantiate: escala e rotação já estão aplicadas
        FindWheels();
        lastPos = motionSource.position;
    }

    void FindWheels()
    {
        wheels.Clear();

        string baseName = string.IsNullOrEmpty(carName) ? name : carName;
        baseName = baseName.Replace("(Clone)", "").Trim();
        string prefix = (baseName + " wheel").ToLowerInvariant();

        foreach (Transform child in GetComponentsInChildren<Transform>(true))
        {
            if (child == transform) continue;

            string n = child.name.ToLowerInvariant();
            if (!n.StartsWith(prefix)) continue;

            var w = new Wheel
            {
                t = child,
                front = n.Contains("front"),
                left = n.Contains("left"),
                initialRot = child.localRotation,
                radius = 0.3f
            };

            var r = child.GetComponentInChildren<Renderer>();
            if (r != null)
            {
                Vector3 e = r.bounds.extents;
                w.radius = Mathf.Max(0.05f, Mathf.Max(e.x, Mathf.Max(e.y, e.z)));
            }

            wheels.Add(w);
        }

        wheelsFound = wheels.Count;

        if (wheels.Count == 0)
        {
            Debug.LogWarning($"{name}: nenhuma roda com o prefixo \"{baseName} wheel\". Preenche Car Name.", this);
            return;
        }

        // distância entre eixos, medida no referencial da estrada (não do modelo rodado)
        float maxZ = float.MinValue, minZ = float.MaxValue;
        foreach (var w in wheels)
        {
            float z = motionSource.InverseTransformPoint(w.t.position).z;
            if (z > maxZ) maxZ = z;
            if (z < minZ) minZ = z;
        }
        WheelBase = Mathf.Max(0.5f, maxZ - minZ);
    }

    void LateUpdate()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f || wheels.Count == 0) return;

        Vector3 delta = motionSource.position - lastPos;
        lastPos = motionSource.position;

        SignedSpeed = speedSource != null
            ? speedSource.CurrentSpeed
            : Vector3.Dot(delta, motionSource.forward) / dt;
        speedDebug = SignedSpeed;

        foreach (var w in wheels)
        {
            float dir = invertSpin ? -1f : 1f;
            if (invertLeftSide && w.left) dir = -dir;

            w.angle = Mathf.Repeat(
                w.angle + (SignedSpeed / w.radius) * Mathf.Rad2Deg * dt * dir, 360f);

            Quaternion spin = Quaternion.AngleAxis(w.angle, spinAxis);
            Quaternion steer = w.front ? Quaternion.AngleAxis(steerAngle, steerAxis) : Quaternion.identity;

            w.t.localRotation = steer * w.initialRot * spin;
        }
    }
}