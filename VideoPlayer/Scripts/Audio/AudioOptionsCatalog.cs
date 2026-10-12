using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

public enum AudioOptionKind
{
    VolumeSlider = 0,
}

[Serializable]
public class AudioOptionEntry
{
    [Tooltip("保存キー。後から表示名を変えても、この ID が同じなら音量設定は残ります。")]
    public string id = "BGM";

    [Tooltip("スライダー横に出す名前。")]
    public string label = "BGM";

    public AudioOptionKind kind = AudioOptionKind.VolumeSlider;

    [Tooltip("AudioMixer のグループ名。")]
    public string mixerGroup = "BGM";

    [Tooltip("公開パラメータ名。空なら ID + Volume になります。")]
    public string exposedParameter = "";

    [Range(0f, 1f)]
    [Tooltip("プレイヤーがまだ音量を保存していないときのスライダー位置。1 が 100% です。")]
    public float defaultVolume = 1f;

    [Tooltip("スライダー 100% のときのグループ音量(dB)。同期ボタンでミキサーの現在値を取り込みます。")]
    public float authoredDb = 0f;

    public string ParameterName =>
        string.IsNullOrEmpty(exposedParameter) ? id + "Volume" : exposedParameter;
}

[CreateAssetMenu(fileName = "AudioOptionsCatalog", menuName = "Dlair/Audio Options Catalog")]
public class AudioOptionsCatalog : ScriptableObject
{
    public AudioMixer mixer;
    public List<AudioOptionEntry> options = new List<AudioOptionEntry>();

    public AudioOptionEntry Find(string id)
    {
        if (options == null || string.IsNullOrEmpty(id)) return null;

        for (int i = 0; i < options.Count; i++)
        {
            AudioOptionEntry entry = options[i];
            if (entry != null && entry.id == id)
                return entry;
        }

        return null;
    }
}
