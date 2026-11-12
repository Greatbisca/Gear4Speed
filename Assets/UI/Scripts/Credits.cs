using UnityEngine;
using UnityEngine.SceneManagement;

public class CreditsMenu : MonoBehaviour
{
    [SerializeField] string mainMenuScene = "MainMenu";

    public void Back() => SceneManager.LoadScene(mainMenuScene);
}