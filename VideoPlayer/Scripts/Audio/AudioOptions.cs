using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

public static class AudioOptions
{
    const string LegacyPrefKeyPrefix = "AudioOption.";
    const string VolumeKeyPrefix = "audio:";
    const float SilentDb = -80f;
    const string CatalogResourcePath = "AudioOptionsCatalog";

    static AudioOptionsCatalog catalog;
    static readonly HashSet<string> reportedMissingParameters = new HashSet<string>();

    public static AudioOptionsCatalog Catalog
    {
        get
        {
            if (catalog == null)
                catalog = Resources.Load<AudioOptionsCatalog>(CatalogResourcePath);
            return catalog;
        }
    }

    public static void SetCatalog(AudioOptionsCatalog value)
    {
        catalog = value;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        GameSettings.Load();
        MigrateLegacyPlayerPrefs();
        ApplyAll();
        var host = new GameObject("AudioOptionsApplier");
        host.hideFlags = HideFlags.HideAndDontSave;
        Object.DontDestroyOnLoad(host);
        host.AddComponent<AudioOptionsApplier>();
    }

    public static float GetVolume(AudioOptionEntry entry)
    {
        if (entry == null || string.IsNullOrEmpty(entry.id))
            return 1f;

        return Mathf.Clamp01(GameSettings.GetFloat(VolumeKey(entry), entry.defaultVolume));
    }

    public static float GetVolumeById(string id)
    {
        AudioOptionsCatalog current = Catalog;
        if (current == null)
            return 1f;

        return GetVolume(current.Find(id));
    }

    public static event System.Action<string, float> VolumeChanged;

    public static void SetVolume(AudioOptionEntry entry, float linear)
    {
        if (entry == null || string.IsNullOrEmpty(entry.id))
            return;

        linear = Mathf.Clamp01(linear);
        GameSettings.SetFloat(VolumeKey(entry), linear);
        Apply(entry, linear);
        if (VolumeChanged != null)
            VolumeChanged(entry.id, linear);
    }

    public static void ApplyAll()
    {
        AudioOptionsCatalog current = Catalog;
        if (current == null || current.options == null)
            return;

        for (int i = 0; i < current.options.Count; i++)
        {
            AudioOptionEntry entry = current.options[i];
            if (entry == null || entry.kind != AudioOptionKind.VolumeSlider)
                continue;

            Apply(entry, GetVolume(entry));
        }
    }

    public static AudioMixerGroup FindGroupForOption(string optionId)
    {
        AudioOptionEntry entry = Catalog != null ? Catalog.Find(optionId) : null;
        if (entry == null)
            return null;

        return FindGroup(entry.mixerGroup);
    }

    public static AudioMixerGroup FindGroup(string groupName)
    {
        AudioOptionsCatalog current = Catalog;
        if (current == null || current.mixer == null || string.IsNullOrEmpty(groupName))
            return null;

        AudioMixerGroup[] groups = current.mixer.FindMatchingGroups(groupName);
        for (int i = 0; i < groups.Length; i++)
        {
            if (groups[i] != null && groups[i].name == groupName)
                return groups[i];
        }

        return null;
    }

    public static float LinearToDb(float linear)
    {
        if (linear <= 0.0001f)
            return SilentDb;

        return Mathf.Log10(linear) * 20f;
    }

    public static float DbToLinear(float db)
    {
        if (db <= SilentDb)
            return 0f;

        return Mathf.Pow(10f, db / 20f);
    }

    static void Apply(AudioOptionEntry entry, float userLinear)
    {
        AudioOptionsCatalog current = Catalog;
        if (current == null || current.mixer == null)
            return;

        string parameter = entry.ParameterName;
        if (string.IsNullOrEmpty(parameter))
            return;

        float authoredLinear = DbToLinear(entry.authoredDb);
        float finalLinear = Mathf.Clamp01(authoredLinear * Mathf.Clamp01(userLinear));
        if (current.mixer.SetFloat(parameter, LinearToDb(finalLinear)))
            return;

        if (reportedMissingParameters.Add(parameter))
            Debug.LogWarning("【AudioOptions】ミキサーに '" + parameter + "' が公開されていません。カタログの同期を実行してください。");
    }

    public static void DeleteLegacyPlayerPrefs()
    {
        AudioOptionsCatalog current = Catalog;
        if (current == null || current.options == null)
            return;

        for (int i = 0; i < current.options.Count; i++)
        {
            AudioOptionEntry entry = current.options[i];
            if (entry == null || string.IsNullOrEmpty(entry.id))
                continue;

            PlayerPrefs.DeleteKey(LegacyPrefKey(entry));
        }

        PlayerPrefs.Save();
    }

    static void MigrateLegacyPlayerPrefs()
    {
        AudioOptionsCatalog current = Catalog;
        if (current == null || current.options == null)
            return;

        bool changed = false;
        for (int i = 0; i < current.options.Count; i++)
        {
            AudioOptionEntry entry = current.options[i];
            if (entry == null || string.IsNullOrEmpty(entry.id))
                continue;

            string legacyKey = LegacyPrefKey(entry);
            if (!PlayerPrefs.HasKey(legacyKey))
                continue;

            string volumeKey = VolumeKey(entry);
            if (!GameSettings.HasFloat(volumeKey))
            {
                GameSettings.SetFloat(volumeKey, PlayerPrefs.GetFloat(legacyKey), false);
                changed = true;
            }

            PlayerPrefs.DeleteKey(legacyKey);
        }

        PlayerPrefs.Save();
        if (changed)
            GameSettings.Save();
    }

    static string VolumeKey(AudioOptionEntry entry)
    {
        return VolumeKeyPrefix + entry.id;
    }

    static string LegacyPrefKey(AudioOptionEntry entry)
    {
        return LegacyPrefKeyPrefix + entry.id;
    }
}

// シーン開始直後はミキサーがスナップショットで音量を戻すことがある。
// スライダー表示だけ保存値になって、実際の音が追いつかないのを防ぐ。
sealed class AudioOptionsApplier : MonoBehaviour
{
    const int ApplyFrames = 30;

    int frames;

    void LateUpdate()
    {
        AudioOptions.ApplyAll();
        frames++;
        if (frames >= ApplyFrames)
            Destroy(gameObject);
    }
}
