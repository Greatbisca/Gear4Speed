using System.Collections;
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class GearboxSounds : MonoBehaviour
{
    public Gearbox gearbox;

    [Header("Embraiagem")]
    public AudioClip clutchPress;

    [Header("Mudanças")]
    public AudioClip gearUp;
    public AudioClip gearDown;
    public AudioClip neutralToNeutral;   // ainda não usado

    [Header("Largar a embraiagem")]
    public AudioClip releaseNormal;      // zona amarela (ou sem mudança engatada): 1 som
    public AudioClip releasePerfect;     // zona verde: 1 som
    public AudioClip releaseBad1;        // zona vermelha: 2 sons
    public AudioClip releaseBad2;
    [Tooltip("Atraso do segundo som da largada má (0 = tocam juntos).")]
    public float badSecondDelay = 0.1f;

    [Range(0f, 1f)] public float volume = 1f;

    AudioSource source;

    void Awake()
    {
        source = GetComponent<AudioSource>();
        if (gearbox == null) gearbox = GetComponent<Gearbox>();

        source.playOnAwake = false;
        source.spatialBlend = 0f;   // sempre 2D
    }

    void OnEnable()
    {
        if (gearbox == null) return;
        gearbox.ClutchPressed += OnClutchPressed;
        gearbox.GearPicked += OnGearPicked;
        gearbox.Shifted += OnShifted;
    }

    void OnDisable()
    {
        if (gearbox == null) return;
        gearbox.ClutchPressed -= OnClutchPressed;
        gearbox.GearPicked -= OnGearPicked;
        gearbox.Shifted -= OnShifted;
    }

    void OnClutchPressed() => Play(clutchPress);

    void OnGearPicked(int dir) => Play(dir > 0 ? gearUp : gearDown);

    // Chama isto quando a mecânica de neutro para neutro existir
    public void PlayNeutralToNeutral() => Play(neutralToNeutral);

    void OnShifted(ShiftZone zone)
    {
        switch (zone)
        {
            case ShiftZone.Green:
                Play(releasePerfect);
                break;

            case ShiftZone.Red:
                Play(releaseBad1);
                if (badSecondDelay <= 0f) Play(releaseBad2);
                else StartCoroutine(PlayDelayed(releaseBad2, badSecondDelay));
                break;

            default:   // Yellow ou None
                Play(releaseNormal);
                break;
        }
    }

    IEnumerator PlayDelayed(AudioClip clip, float delay)
    {
        yield return new WaitForSeconds(delay);
        Play(clip);
    }

    void Play(AudioClip clip)
    {
        if (clip != null) source.PlayOneShot(clip, volume);
    }
}