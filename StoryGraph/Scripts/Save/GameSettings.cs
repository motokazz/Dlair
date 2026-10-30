using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// プレイヤーが変えた設定値。初期値は AudioOptionsCatalog などのマスターデータ側に置く。
/// </summary>
public static class GameSettings
{
    public static event Action Cleared;

    static readonly Dictionary<string, int> ints = new Dictionary<string, int>();
    static readonly Dictionary<string, bool> bools = new Dictionary<string, bool>();
    static readonly Dictionary<string, float> floats = new Dictionary<string, float>();
    static readonly Dictionary<string, string> strings = new Dictionary<string, string>();
    static bool loaded;

    /// <summary>
    /// options.json に書く設定の一覧。
    /// </summary>
    [Serializable]
    class SaveData
    {
        public int version = 1;
        public List<IntEntry> ints = new List<IntEntry>();
        public List<BoolEntry> bools = new List<BoolEntry>();
        public List<FloatEntry> floats = new List<FloatEntry>();
        public List<StringEntry> strings = new List<StringEntry>();
    }

    [Serializable] struct IntEntry { public string key; public int value; }
    [Serializable] struct BoolEntry { public string key; public bool value; }
    [Serializable] struct FloatEntry { public string key; public float value; }
    [Serializable] struct StringEntry { public string key; public string value; }

    public static void Load()
    {
        if (loaded)
            return;

        loaded = true;
        if (!JsonSaveFile.TryRead(SaveDataPaths.OptionsFile, out string json))
            return;

        SaveData data = JsonUtility.FromJson<SaveData>(json);
        if (data == null)
            return;

        Read(data.ints, ints, entry => entry.key, entry => entry.value);
        Read(data.bools, bools, entry => entry.key, entry => entry.value);
        Read(data.floats, floats, entry => entry.key, entry => entry.value);
        Read(data.strings, strings, entry => entry.key, entry => entry.value);
    }

    public static void Save()
    {
        var data = new SaveData();
        Write(ints, data.ints, (key, value) => new IntEntry { key = key, value = value });
        Write(bools, data.bools, (key, value) => new BoolEntry { key = key, value = value });
        Write(floats, data.floats, (key, value) => new FloatEntry { key = key, value = value });
        Write(strings, data.strings, (key, value) => new StringEntry { key = key, value = value });
        JsonSaveFile.Write(SaveDataPaths.OptionsFile, JsonUtility.ToJson(data, true));
    }

    public static void Clear()
    {
        ClearMemory();
        JsonSaveFile.Delete(SaveDataPaths.OptionsFile);
        Cleared?.Invoke();
    }

    public static void ClearMemory()
    {
        ints.Clear();
        bools.Clear();
        floats.Clear();
        strings.Clear();
        loaded = true;
    }

    public static void NotifyCleared()
    {
        Cleared?.Invoke();
    }

    public static bool HasFloat(string key)
    {
        return !string.IsNullOrEmpty(key) && floats.ContainsKey(key);
    }

    public static float GetFloat(string key, float defaultValue = 0f)
    {
        if (string.IsNullOrEmpty(key)) return defaultValue;
        return floats.TryGetValue(key, out float value) ? value : defaultValue;
    }

    public static void SetFloat(string key, float value, bool save = true)
    {
        if (string.IsNullOrEmpty(key)) return;
        if (floats.TryGetValue(key, out float current) && Mathf.Approximately(current, value))
            return;

        floats[key] = value;
        if (save)
            Save();
    }

    public static bool GetBool(string key, bool defaultValue = false)
    {
        if (string.IsNullOrEmpty(key)) return defaultValue;
        return bools.TryGetValue(key, out bool value) ? value : defaultValue;
    }

    public static void SetBool(string key, bool value, bool save = true)
    {
        if (string.IsNullOrEmpty(key)) return;
        if (bools.TryGetValue(key, out bool current) && current == value)
            return;

        bools[key] = value;
        if (save)
            Save();
    }

    public static int GetInt(string key, int defaultValue = 0)
    {
        if (string.IsNullOrEmpty(key)) return defaultValue;
        return ints.TryGetValue(key, out int value) ? value : defaultValue;
    }

    public static void SetInt(string key, int value, bool save = true)
    {
        if (string.IsNullOrEmpty(key)) return;
        if (ints.TryGetValue(key, out int current) && current == value)
            return;

        ints[key] = value;
        if (save)
            Save();
    }

    public static string GetString(string key, string defaultValue = "")
    {
        if (string.IsNullOrEmpty(key)) return defaultValue;
        return strings.TryGetValue(key, out string value) ? value : defaultValue;
    }

    public static void SetString(string key, string value, bool save = true)
    {
        if (string.IsNullOrEmpty(key)) return;
        if (strings.TryGetValue(key, out string current) && current == value)
            return;

        strings[key] = value ?? "";
        if (save)
            Save();
    }

    static void Read<TEntry, TValue>(
        List<TEntry> entries,
        Dictionary<string, TValue> destination,
        Func<TEntry, string> key,
        Func<TEntry, TValue> value)
    {
        if (entries == null)
            return;

        for (int i = 0; i < entries.Count; i++)
        {
            string entryKey = key(entries[i]);
            if (!string.IsNullOrEmpty(entryKey))
                destination[entryKey] = value(entries[i]);
        }
    }

    static void Write<TValue, TEntry>(
        Dictionary<string, TValue> source,
        List<TEntry> destination,
        Func<string, TValue, TEntry> create)
    {
        var keys = new List<string>(source.Keys);
        keys.Sort(StringComparer.Ordinal);
        for (int i = 0; i < keys.Count; i++)
            destination.Add(create(keys[i], source[keys[i]]));
    }
}
