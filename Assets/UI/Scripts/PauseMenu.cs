using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class PauseMenu : MonoBehaviour
{
    public static bool IsPaused { get; private set; }

    [Header("Panels")]
    [SerializeField] GameObject pausePanel;      // overlay + top bar + title
    [SerializeField] GameObject optionsPanel;

    [Header("Options")]
    [SerializeField] Slider volumeSlider;
    [SerializeField] Toggle fullscreenToggle;

    [Header("Freeze these while paused (drag the components, not the objects)")]
    [SerializeField] Behaviour[] disableWhilePaused;

    [Header("Scenes")]
    [SerializeField] string menuScene = "MainMenu";

    const string VolumeKey = "volume";

    void Awake()
    {
        IsPaused = false;
        Time.timeScale = 1f;
        AudioListener.pause = false;

        if (pausePanel != null) pausePanel.SetActive(false);
        if (optionsPanel != null) optionsPanel.SetActive(false);

        float v = PlayerPrefs.GetFloat(VolumeKey, 1f);
        AudioListener.volume = v;
        if (volumeSlider != null)
        {
            volumeSlider.minValue = 0f;
            volumeSlider.maxValue = 1f;
            volumeSlider.SetValueWithoutNotify(v);
            volumeSlider.onValueChanged.AddListener(SetVolume);
        }
        if (fullscreenToggle != null)
        {
            fullscreenToggle.SetIsOnWithoutNotify(Screen.fullScreen);
            fullscreenToggle.onValueChanged.AddListener(SetFullscreen);
        }
    }

    void OnDestroy()
    {
        // never leave the game frozen if this scene is unloaded while paused
        IsPaused = false;
        Time.timeScale = 1f;
        AudioListener.pause = false;
    }

    void Update()
    {
        if (RaceManager.Countdown || RaceManager.Finished) return;
        bool toggle;
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        toggle = kb != null && (kb.escapeKey.wasPressedThisFrame || kb.pKey.wasPressedThisFrame);
#else
        toggle = Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.P);
#endif
        if (!toggle) return;

        if (!IsPaused) Pause();
        else if (optionsPanel != null && optionsPanel.activeSelf) CloseOptions();
        else Resume();
    }

    void SetFrozen(bool frozen)
    {
        if (disableWhilePaused == null) return;
        foreach (var b in disableWhilePaused)
            if (b != null) b.enabled = !frozen;
    }

    public void Pause()
    {
        Debug.Log("PAUSE called");
        IsPaused = true;
        Time.timeScale = 0f;
        AudioListener.pause = true;     // engine sounds stop
        SetFrozen(true);
        if (pausePanel != null) pausePanel.SetActive(true);
        if (optionsPanel != null) optionsPanel.SetActive(false);
    }

    public void Resume()
    {
        IsPaused = false;
        Time.timeScale = 1f;
        AudioListener.pause = false;
        SetFrozen(false);
        if (pausePanel != null) pausePanel.SetActive(false);
        if (optionsPanel != null) optionsPanel.SetActive(false);
    }

    public void Restart()
    {
        Resume();   // unfreeze first
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);   // same car: the selection is kept
    }

    public void QuitToMenu()
    {
        Resume();
        SceneManager.LoadScene(menuScene);
    }

    public void OpenOptions()
    {
        pausePanel.SetActive(false);
        optionsPanel.SetActive(true);
    }

    public void CloseOptions()
    {
        optionsPanel.SetActive(false);
        pausePanel.SetActive(true);
    }

    void SetVolume(float v)
    {
        AudioListener.volume = v;
        PlayerPrefs.SetFloat(VolumeKey, v);
    }

    void SetFullscreen(bool on) => Screen.fullScreen = on;
}