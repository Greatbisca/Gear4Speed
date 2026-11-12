using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenu : MonoBehaviour
{
    [SerializeField] string carSelectScene = "CarSelect";
    [SerializeField] string creditsScene = "Credits";
    [SerializeField] GameObject quitButton;   // hidden in the web build

    void Awake()
    {
        if (quitButton != null && Application.platform == RuntimePlatform.WebGLPlayer)
            quitButton.SetActive(false);
    }

    public void Play() => SceneManager.LoadScene(carSelectScene);

    public void Credits() => SceneManager.LoadScene(creditsScene);

    public void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}