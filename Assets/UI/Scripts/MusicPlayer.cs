using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(AudioSource))]
public class MusicPlayer : MonoBehaviour
{
    static MusicPlayer instance;

    [SerializeField] AudioClip menuMusic;
    [Tooltip("Scenes where the music fades out (e.g. the race)")]
    [SerializeField] string[] quietScenes = { "Main Scene" };
    [SerializeField] float fadeSpeed = 1.5f;

    AudioSource source;
    float fade = 1f, fadeTarget = 1f;

    void Awake()
    {
        if (instance != null) { Destroy(gameObject); return; }   // already playing: keep the first one
        instance = this;
        DontDestroyOnLoad(gameObject);

        source = GetComponent<AudioSource>();
        source.clip = menuMusic;
        source.loop = true;
        source.playOnAwake = false;
        source.spatialBlend = 0f;              // 2D sound
        source.ignoreListenerPause = true;     // keeps playing if a menu pauses the game
        source.volume = GameAudio.Music;
        source.Play();

        SceneManager.sceneLoaded += OnSceneLoaded;
        GameAudio.Changed += Apply;
    }

    void OnDestroy()
    {
        if (instance != this) return;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        GameAudio.Changed -= Apply;
        instance = null;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        fadeTarget = System.Array.IndexOf(quietScenes, scene.name) >= 0 ? 0f : 1f;
        if (fadeTarget > 0f && !source.isPlaying) source.Play();
    }

    void Update()
    {
        fade = Mathf.MoveTowards(fade, fadeTarget, fadeSpeed * Time.unscaledDeltaTime);
        Apply();
        if (fade <= 0f && source.isPlaying) source.Pause();
        else if (fade > 0f && !source.isPlaying) source.UnPause();
    }

    void Apply() { if (source != null) source.volume = GameAudio.Music * fade; }
}