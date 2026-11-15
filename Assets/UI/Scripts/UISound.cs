using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[RequireComponent(typeof(AudioSource))]
public class UISound : MonoBehaviour
{
    public static UISound Instance { get; private set; }

    [Header("Clips")]
    [SerializeField] AudioClip selectClip;      // hover / keyboard selection
    [SerializeField] AudioClip clickClip;       // pressed

    [Header("Volume")]
    [Range(0f, 1f)] [SerializeField] float selectVolume = 0.5f;
    [Range(0f, 1f)] [SerializeField] float clickVolume = 1f;

    [Header("Buttons")]
    [Tooltip("Add the sounds to every Button and Toggle in each scene that loads")]
    [SerializeField] bool autoAttach = true;

    AudioSource src;
    float lastSelect;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }   // one is enough
        Instance = this;
        DontDestroyOnLoad(gameObject);

        src = GetComponent<AudioSource>();
        src.playOnAwake = false;
        src.spatialBlend = 0f;               // 2D
        src.ignoreListenerPause = true;      // still plays when the game is paused

        SceneManager.sceneLoaded += OnSceneLoaded;
        Attach();
    }

    void OnDestroy()
    {
        if (Instance != this) return;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        Instance = null;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode) { if (autoAttach) Attach(); }

    // put UIButtonSound on every Button and Toggle (including hidden panels)
    public static void Attach()
    {
        foreach (var s in FindObjectsByType<Selectable>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!(s is Button) && !(s is Toggle)) continue;
            if (s.GetComponent<UIButtonSound>() == null) s.gameObject.AddComponent<UIButtonSound>();
        }
    }

    public void PlaySelect()
    {
        if (selectClip == null || Time.unscaledTime - lastSelect < 0.06f) return;   // no machine-gun
        lastSelect = Time.unscaledTime;
        src.PlayOneShot(selectClip, selectVolume);
    }

    public void PlayClick()
    {
        if (clickClip != null) src.PlayOneShot(clickClip, clickVolume);
    }
}

// Added automatically to each button
public class UIButtonSound : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
                             IPointerClickHandler, ISelectHandler, ISubmitHandler
{
    Selectable sel;
    bool pointerOver;

    void Awake() => sel = GetComponent<Selectable>();

    bool Active => sel != null && sel.IsInteractable() && UISound.Instance != null;

    public void OnPointerEnter(PointerEventData e)
    {
        pointerOver = true;
        if (Active) UISound.Instance.PlaySelect();
    }

    public void OnPointerExit(PointerEventData e) => pointerOver = false;

    // keyboard selection only: a mouse click also selects, and we already played it on hover
    public void OnSelect(BaseEventData e)
    {
        if (!pointerOver && Active) UISound.Instance.PlaySelect();
    }

    public void OnPointerClick(PointerEventData e)
    {
        if (Active) UISound.Instance.PlayClick();
    }

    public void OnSubmit(BaseEventData e)    // Enter / Space on a selected button
    {
        if (Active) UISound.Instance.PlayClick();
    }
}