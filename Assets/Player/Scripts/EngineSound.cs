using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class EngineSound : MonoBehaviour
{
    [Header("Som")]
    public AudioClip engineLoop;
    [Range(0f, 1f)] public float volume = 0.6f;

    [Header("RPM (valores editáveis)")]
    public float idleRpm = 800f;
    public float maxRpm = 4000f;

    [Header("Pitch / volume")]
    public float idlePitch = 0.6f;
    public float maxPitch = 2f;
    [Range(0f, 1f)] public float idleVolumeFactor = 0.6f;   // volume em ralenti vs. no máximo
    [Tooltip("Maior = o pitch acompanha mais depressa.")]
    public float smoothing = 10f;

    [Header("Sem Gearbox (IA): RPM estimado pela velocidade")]
    [Tooltip("Velocidade (m/s) à qual o motor chega ao máximo na última mudança.")]
    public float topSpeed = 20f;
    [Tooltip("Mudanças falsas para o pitch subir e descer.")]
    public int fakeGears = 4;
    [Range(0f, 1f)] public float rpmAfterShift = 0.55f;     // fração do RPM ao entrar numa mudança nova

    [Header("3D")]
    public float minDistance = 2f;
    public float maxDistance = 30f;

    [Header("Diagnóstico (só leitura)")]
    public float rpmDebug;
    public bool usingGearbox;

    AudioSource source;
    Gearbox gearbox;
    ISpeedSource speedSource;
    float currentRpm;

    void Awake()
    {
        AudioSource existing = GetComponent<AudioSource>();
        GameObject audioObject = new GameObject("Engine Audio");
        audioObject.transform.SetParent(transform, false);
        source = audioObject.AddComponent<AudioSource>();
        CopyAudioSettings(existing, source);
        source.clip = engineLoop;
        source.loop = true;
        source.playOnAwake = false;
        source.spatialBlend = 1f;
        source.dopplerLevel = 0f;
        source.rolloffMode = AudioRolloffMode.Linear;
        source.minDistance = minDistance;
        source.maxDistance = maxDistance;

        existing.Stop();
        existing.playOnAwake = false;
        existing.clip = null;

        speedSource = GetComponentInParent<ISpeedSource>();

        // Player: vai buscar o Gearbox que o PlayerController já tem
        var player = GetComponentInParent<PlayerController>();
        gearbox = player != null ? player.gearbox : GetComponentInParent<Gearbox>();
        usingGearbox = gearbox != null;

        var stats = GetComponentInParent<CarStats>();
        if (stats != null && stats.engineSound != null)
        {
            engineLoop = stats.engineSound;
            maxRpm = stats.maxRpm;
        }
    }

    static void CopyAudioSettings(AudioSource from, AudioSource to)
    {
        to.outputAudioMixerGroup = from.outputAudioMixerGroup;
        to.mute = from.mute;
        to.bypassEffects = from.bypassEffects;
        to.bypassListenerEffects = from.bypassListenerEffects;
        to.bypassReverbZones = from.bypassReverbZones;
        to.priority = from.priority;
        to.panStereo = from.panStereo;
        to.spatialBlend = from.spatialBlend;
        to.reverbZoneMix = from.reverbZoneMix;
        to.dopplerLevel = from.dopplerLevel;
        to.rolloffMode = from.rolloffMode;
        to.minDistance = from.minDistance;
        to.maxDistance = from.maxDistance;
    }

    void Start()
    {
        currentRpm = idleRpm;
        if (engineLoop != null)
            source.Play();
    }

    public void Configure(CarStats stats)
    {
        if (stats == null) return;
        source.spatialBlend = 1f;
        source.dopplerLevel = 0f;
        source.rolloffMode = AudioRolloffMode.Linear;
        source.minDistance = minDistance;
        source.maxDistance = maxDistance;
        if (stats.engineSound != null)
        {
            engineLoop = stats.engineSound;
            source.clip = engineLoop;
            source.loop = true;
            if (!source.isPlaying && engineLoop != null)
                source.Play();
        }
        maxRpm = stats.maxRpm;
    }

    void Update()
    {
        float targetRpm = gearbox != null ? gearbox.Rpm : EstimateRpm();
        targetRpm = Mathf.Clamp(targetRpm, idleRpm, maxRpm);

        currentRpm = Mathf.Lerp(currentRpm, targetRpm, 1f - Mathf.Exp(-smoothing * Time.deltaTime));
        rpmDebug = currentRpm;

        float n = Mathf.InverseLerp(idleRpm, maxRpm, currentRpm);
        source.pitch = Mathf.Lerp(idlePitch, maxPitch, n);
        source.volume = volume * Mathf.Lerp(idleVolumeFactor, 1f, n);
    }

    // IA (ou qualquer objeto sem Gearbox): RPM falso a partir da velocidade
    float EstimateRpm()
    {
        float speed = speedSource != null ? Mathf.Abs(speedSource.CurrentSpeed) : 0f;
        int gears = Mathf.Max(1, fakeGears);
        float gearTop = topSpeed / gears;

        int gear = Mathf.Min(Mathf.FloorToInt(speed / gearTop), gears - 1);
        float within = Mathf.Clamp01((speed - gear * gearTop) / gearTop);
        float start = gear == 0 ? 0f : rpmAfterShift;

        return Mathf.Lerp(idleRpm, maxRpm, Mathf.Lerp(start, 1f, within));
    }
}