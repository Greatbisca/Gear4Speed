using UnityEngine;

public class FramePacing : MonoBehaviour
{
    void Awake()
    {
        QualitySettings.vSyncCount = 1;      // sincroniza com o ecrã
        Application.targetFrameRate = -1;
    }
}