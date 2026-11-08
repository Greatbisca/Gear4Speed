using UnityEngine;
using UnityEngine.UI;

public class AudioOptions : MonoBehaviour
{
    [SerializeField] Slider masterSlider;
    [SerializeField] Slider musicSlider;

    void OnEnable()
    {
        Setup(masterSlider, GameAudio.Master, v => GameAudio.Master = v);
        Setup(musicSlider,  GameAudio.Music,  v => GameAudio.Music  = v);
    }

    void OnDisable()
    {
        if (masterSlider != null) masterSlider.onValueChanged.RemoveAllListeners();
        if (musicSlider != null)  musicSlider.onValueChanged.RemoveAllListeners();
    }

    static void Setup(Slider s, float value, UnityEngine.Events.UnityAction<float> onChange)
    {
        if (s == null) return;
        s.minValue = 0f;
        s.maxValue = 1f;
        s.SetValueWithoutNotify(value);
        s.onValueChanged.RemoveAllListeners();
        s.onValueChanged.AddListener(onChange);
    }
}