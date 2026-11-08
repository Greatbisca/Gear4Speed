using System;
using UnityEngine;

public static class GameAudio
{
    const string MasterKey = "volume";        // same key the pause menu already uses
    const string MusicKey  = "musicVolume";

    public static event Action Changed;

    public static float Master
    {
        get => PlayerPrefs.GetFloat(MasterKey, 1f);
        set
        {
            value = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(MasterKey, value);
            AudioListener.volume = value;
            Changed?.Invoke();
        }
    }

    public static float Music
    {
        get => PlayerPrefs.GetFloat(MusicKey, 0.7f);
        set
        {
            PlayerPrefs.SetFloat(MusicKey, Mathf.Clamp01(value));
            Changed?.Invoke();
        }
    }

    // apply the saved master volume as soon as the game starts, in any scene
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Init() => AudioListener.volume = Master;
}