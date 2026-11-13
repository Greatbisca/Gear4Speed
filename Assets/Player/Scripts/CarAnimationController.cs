using UnityEngine;

public class CarAnimationController : MonoBehaviour
{
    [Header("Animação procedural sem rig")]
    [Tooltip("Inclinação lateral da carroçaria durante uma curva.")]
    [SerializeField] float turnRoll = 7f;
    [Tooltip("Rotação horizontal adicional durante uma curva.")]
    [SerializeField] float turnYaw = 2f;
    [Tooltip("Inclinação lateral durante um derrapagem.")]
    [SerializeField] float skidRoll = 10f;
    [Tooltip("Rotação horizontal durante um derrapagem.")]
    [SerializeField] float skidYaw = 12f;
    [SerializeField] float animationSmoothing = 10f;
    [SerializeField] float skidDuration = 0.6f;

    PlayerController player;
    RaceSplineFollower follower;
    CarSpawner spawner;
    Transform visual;
    Quaternion baseLocalRotation;
    float skidTimeRemaining;
    bool subscribed;

    void Awake()
    {
        player = GetComponent<PlayerController>();
        follower = GetComponent<RaceSplineFollower>();
        spawner = GetComponent<CarSpawner>();
    }

    void OnEnable()
    {
        SubscribeToGearbox();
    }

    void Start()
    {
        SubscribeToGearbox();
        RefreshVisual();
    }

    void OnDisable()
    {
        if (subscribed && player != null && player.gearbox != null)
            player.gearbox.Shifted -= OnShifted;
        subscribed = false;
        ResetVisual();
    }

    void Update()
    {
        RefreshVisual();
        if (visual == null)
            return;

        if (skidTimeRemaining > 0f)
            skidTimeRemaining -= Time.deltaTime;

        int direction = follower != null ? follower.CurrentTurnDirection : 0;
        bool inCorner = follower != null &&
                        follower.CurrentSegment != null &&
                        follower.CurrentSegment.IsCorner &&
                        direction != 0;

        float targetRoll = 0f;
        float targetYaw = 0f;

        if (skidTimeRemaining > 0f)
        {
            targetRoll = -skidRoll;
            targetYaw = skidYaw;
        }
        else if (inCorner)
        {
            targetRoll = -direction * turnRoll;
            targetYaw = direction * turnYaw;
        }

        Quaternion targetRotation =
            baseLocalRotation * Quaternion.Euler(0f, targetYaw, targetRoll);
        float blend = 1f - Mathf.Exp(-animationSmoothing * Time.deltaTime);
        visual.localRotation = Quaternion.Slerp(
            visual.localRotation, targetRotation, blend);
    }

    void SubscribeToGearbox()
    {
        if (subscribed)
            return;

        if (player == null)
            player = GetComponent<PlayerController>();

        if (player != null && player.gearbox != null)
        {
            player.gearbox.Shifted += OnShifted;
            subscribed = true;
        }
    }

    void OnShifted(ShiftZone zone)
    {
        if (zone == ShiftZone.Red)
            skidTimeRemaining = skidDuration;
    }

    void RefreshVisual()
    {
        if (visual != null || spawner == null || spawner.CurrentCar == null)
            return;

        visual = spawner.CurrentCar.transform;
        baseLocalRotation = visual.localRotation;
    }

    void ResetVisual()
    {
        if (visual != null)
            visual.localRotation = baseLocalRotation;
    }
}
