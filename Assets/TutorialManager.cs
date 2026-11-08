
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.InputSystem;

public class TutorialManager : MonoBehaviour
{
    [System.Serializable]
    public class TutorialFrame
    {
        [TextArea(2, 5)]
        public string text;

        [Header("Text Position")]
        public Vector2 textPosition;

        [Header("Arrow")]
        public bool showArrow = true;
        public Vector2 arrowPosition;
        public float arrowRotation;
    }

    [Header("UI References")]
    public TMP_Text tutorialText;
    public RectTransform textTransform;
    public RectTransform arrowTransform;
    public GameObject arrowObject;

    [Header("Tutorial Frames")]
    public TutorialFrame[] frames;

    private int currentFrame = 0;
    private bool tutorialFinished = false;

    void Start()
    {
        ShowFrame();
    }

    void Update()
    {
        if (tutorialFinished)
            return;

        if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
            {
                NextFrame();
            }
    }

    void ShowFrame()
    {
        if (currentFrame >= frames.Length)
        {
            FinishTutorial();
            return;
        }

        TutorialFrame frame = frames[currentFrame];

        // Update text
        tutorialText.text = frame.text;
        textTransform.anchoredPosition = frame.textPosition;

        // Update arrow
        arrowObject.SetActive(frame.showArrow);

        if (frame.showArrow)
        {
            arrowTransform.anchoredPosition = frame.arrowPosition;
            arrowTransform.localRotation =
                Quaternion.Euler(0, 0, frame.arrowRotation);
        }
    }

    void NextFrame()
    {
        currentFrame++;
        ShowFrame();
    }

    void FinishTutorial()
    {
        tutorialFinished = true;

        tutorialText.gameObject.SetActive(false);
        arrowObject.SetActive(false);

        Debug.Log("Tutorial finished!");

        UnityEngine.SceneManagement.SceneManager.LoadScene("Main Scene");
    }
}
