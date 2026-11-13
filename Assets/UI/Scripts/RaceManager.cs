using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[RequireComponent(typeof(AudioSource))]
public class RaceManager : MonoBehaviour
{
    public static bool Countdown { get; private set; }
    public static bool RaceActive { get; private set; }
    public static bool Finished { get; private set; }

    [Header("Track")]
    [SerializeField] RaceSplinePath track;       // drag RaceSplicePath here
    [SerializeField] int totalLaps = 3;

    [Header("Racers")]
    [SerializeField] Transform player;
    [SerializeField] Transform[] opponents;

    [Header("UI (legacy Text)")]
    [SerializeField] Text countdownText;
    [SerializeField] Text lapText;
    [SerializeField] Text timeText;
    [SerializeField] GameObject resultsPanel;
    [SerializeField] Text resultsTitle;
    [SerializeField] Text resultsDetail;

    [Header("Countdown")]
    [SerializeField] int startFrom = 3;
    [SerializeField] float stepTime = 1f;
    [SerializeField] float goTime = 0.8f;
    [SerializeField] Color countColor = new Color(1f, .67f, .27f);
    [SerializeField] Color goColor = new Color(.3f, .9f, .3f);

    [Header("Race audio")]
    [SerializeField] AudioClip countdownSound;
    [SerializeField] AudioClip raceMusic;
    [SerializeField] float raceMusicVolume = 0.7f;
    [SerializeField] float raceMusicDelay = 0.5f;
    [SerializeField] float raceMusicFadeIn = 1f;

    [Header("Scenes")]
    [SerializeField] string menuScene = "MainMenu";

    // how often the lap tracking looks at the cars (the track query is not free)
    const float TrackStep = 0.05f;

    class Racer
    {
        public Transform t;
        public float lastFrac, progress, finishTime;
        public bool finished;
    }

    readonly List<Racer> racers = new List<Racer>();
    Racer playerRacer;
    AudioSource raceAudio;
    AudioSource raceMusicAudio;
    float raceTime, nextTrack;
    int finishedCount;

    void Awake()
    {
        raceAudio = GetComponent<AudioSource>();
        raceAudio.playOnAwake = false;
        raceAudio.loop = false;
        raceAudio.spatialBlend = 0f;
        raceAudio.ignoreListenerPause = true;
        GameObject musicObject = new GameObject("Race Music Audio");
        musicObject.transform.SetParent(transform, false);
        raceMusicAudio = musicObject.AddComponent<AudioSource>();
        raceMusicAudio.playOnAwake = false;
        raceMusicAudio.loop = true;
        raceMusicAudio.spatialBlend = 0f;
        raceMusicAudio.ignoreListenerPause = true;
        Countdown = RaceActive = Finished = false;
        if (countdownText != null) countdownText.gameObject.SetActive(false);
        if (resultsPanel != null) resultsPanel.SetActive(false);
        if (track == null) track = FindFirstObjectByType<RaceSplinePath>();
    }

    void Start() => StartCoroutine(Run());

    void OnDestroy()
    {
        if (raceAudio != null) raceAudio.Stop();
        if (raceMusicAudio != null) raceMusicAudio.Stop();
        Countdown = RaceActive = Finished = false;
        Time.timeScale = 1f;
    }

    // ---------------- Race flow ----------------
    IEnumerator Run()
    {
        Time.timeScale = 0f;              // cars stand still during the countdown
        Countdown = true;
        yield return null;                // let the cars be placed on the grid first
        yield return null;
        Setup();
        PlayCountdownSound();

        for (int n = startFrom; n >= 1; n--)
        {
            Show(n.ToString(), countColor);
            yield return Pop(stepTime);
        }

        Show("GO!", goColor);
        Time.timeScale = 1f;
        Countdown = false;
        RaceActive = true;
        StartCoroutine(PlayRaceMusicDelayed());
        yield return Pop(goTime);
        if (countdownText != null) countdownText.gameObject.SetActive(false);
    }

    void PlayCountdownSound()
    {
        if (countdownSound == null) return;
        raceAudio.Stop();
        raceAudio.loop = false;
        raceAudio.clip = countdownSound;
        raceAudio.volume = 1f;
        raceAudio.Play();
    }

    IEnumerator PlayRaceMusicDelayed()
    {
        if (raceMusicAudio == null || raceMusic == null) yield break;
        raceMusicAudio.Stop();
        raceMusicAudio.volume = 0f;
        if (raceMusicDelay > 0f)
            yield return new WaitForSecondsRealtime(raceMusicDelay);

        raceMusicAudio.clip = raceMusic;
        raceMusicAudio.Play();

        float targetVolume = raceMusicVolume * GameAudio.Music;
        float elapsed = 0f;
        float fadeDuration = Mathf.Max(0f, raceMusicFadeIn);
        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            raceMusicAudio.volume = Mathf.Lerp(0f, targetVolume, elapsed / fadeDuration);
            yield return null;
        }

        raceMusicAudio.volume = targetVolume;
    }

    void Show(string s, Color c)
    {
        if (countdownText == null) return;
        countdownText.gameObject.SetActive(true);
        countdownText.text = s;
        countdownText.color = c;
    }

    // number "pops" from big to normal; uses unscaled time because the game is frozen
    IEnumerator Pop(float duration)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / .25f);
            if (countdownText != null)
                countdownText.rectTransform.localScale = Vector3.one * Mathf.Lerp(1.8f, 1f, 1f - (1f - k) * (1f - k));
            yield return null;
        }
    }

    // ---------------- Lap tracking ----------------
    void Setup()
    {
        racers.Clear();
        if (track == null || track.TotalLength <= 0f)
        {
            Debug.LogWarning("RaceManager: no RaceSplinePath (or it has no length).");
            return;
        }

        playerRacer = AddRacer(player);
        if (opponents != null) foreach (var o in opponents) AddRacer(o);
    }

    Racer AddRacer(Transform t)
    {
        if (t == null) return null;
        var r = new Racer { t = t };
        float f = Fraction(t.position);
        r.lastFrac = f;
        r.progress = f > 0.5f ? f - 1f : f;     // a car parked just behind the line starts below zero
        racers.Add(r);
        return r;
    }

    // 0..1 position along the whole track
    float Fraction(Vector3 worldPos) => track.GetClosestDistance(worldPos) / track.TotalLength;

    void Update()
    {
        if (!RaceActive || Finished || track == null || track.TotalLength <= 0f) return;

        raceTime += Time.deltaTime;

        if (Time.time >= nextTrack)
        {
            nextTrack = Time.time + TrackStep;
            foreach (var r in racers) Step(r);
        }

        if (playerRacer != null)
        {
            int lap = Mathf.Clamp(Mathf.FloorToInt(playerRacer.progress) + 1, 1, totalLaps);
            if (lapText != null) lapText.text = $"LAP {lap}/{totalLaps}";
        }
        if (timeText != null) timeText.text = FormatTime(raceTime);
    }

    void Step(Racer r)
    {
        if (r.finished || r.t == null) return;

        float f = Fraction(r.t.position);
        float d = f - r.lastFrac;
        if (d < -0.5f) d += 1f;            // crossed the start line going forward
        else if (d > 0.5f) d -= 1f;
        if (Mathf.Abs(d) > 0.05f) return;  // ignore jumps to a nearby stretch of track

        r.lastFrac = f;
        r.progress += d;

        if (r.progress >= totalLaps)
        {
            r.finished = true;
            r.finishTime = raceTime;
            finishedCount++;
            if (r == playerRacer) Finish(finishedCount);
        }
    }

    void Finish(int place)
    {
        Finished = true;
        RaceActive = false;
        Time.timeScale = 0f;

        if (resultsPanel != null) resultsPanel.SetActive(true);
        if (resultsTitle != null) resultsTitle.text = Ordinal(place) + " PLACE";
        if (resultsDetail != null) resultsDetail.text = "TIME  " + FormatTime(raceTime);
    }

    // ---------------- Helpers / buttons ----------------
    static string FormatTime(float t)
    {
        int m = (int)(t / 60f);
        float s = t - m * 60f;
        return $"{m:00}:{s:00.00}";
    }

    static string Ordinal(int n) => n == 1 ? "1ST" : n == 2 ? "2ND" : n == 3 ? "3RD" : n + "TH";

    public void Restart()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void QuitToMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(menuScene);
    }
}