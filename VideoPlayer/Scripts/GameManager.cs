using System;
using System.Collections.Generic;
using UnityEngine;

public enum VariableOperation
{
    Set,        // = (代入)
    Add,        // += (加算)
    Subtract,   // -= (減算)
    Multiply,   // *= (乗算)
    Divide,     // /= (除算)
    Modulo      // %= (剰余)
}

public static class GameManager
{
    // 変数テーブル (キーと数値)
    private static Dictionary<string, int> intParams = new Dictionary<string, int>();
    private static Dictionary<string, bool> boolParams = new Dictionary<string, bool>();
    private static Dictionary<string, float> floatParams = new Dictionary<string, float>();
    private static Dictionary<string, string> stringParams = new Dictionary<string, string>();

    // 変数変更時のコールバックイベント
    public static event Action<string> OnVariableChanged;

    #region Database Initialization
    // データベースから初期値を読み込んで初期化
    public static void InitializeFromDatabase(StoryVariableDatabase database)
    {
        if (database == null) return;

        ResetAll();

        foreach (var def in database.variables)
        {
            if (string.IsNullOrEmpty(def.key)) continue;

            switch (def.type)
            {
                case StoryVariableType.Int:
                    intParams[def.key] = def.defaultIntValue;
                    break;
                case StoryVariableType.Bool:
                    boolParams[def.key] = def.defaultBoolValue;
                    break;
                case StoryVariableType.Float:
                    floatParams[def.key] = def.defaultFloatValue;
                    break;
                case StoryVariableType.String:
                    stringParams[def.key] = def.defaultStringValue;
                    break;
            }
        }
        Debug.Log($"【GameManager】データベースから {database.variables.Count} 個の変数を初期化しました。");
    }
    #endregion

    #region Integer Operations (既存互換 & 演算)
    // パラメーターの値を取得する (既存互換)
    public static int GetParameter(string key, int defaultValue = 0)
    {
        if (string.IsNullOrEmpty(key)) return defaultValue;
        if (intParams.TryGetValue(key, out int value)) return value;
        return defaultValue;
    }

    // パラメーターを直接設定する
    public static void SetParameter(string key, int value)
    {
        if (string.IsNullOrEmpty(key)) return;
        intParams[key] = value;
        Debug.Log($"【GameManager】 '{key}' を {value} に設定しました。");
        OnVariableChanged?.Invoke(key);
    }

    // パラメーターを加算する (既存互換)
    public static void AddParameter(string key, int value)
    {
        if (string.IsNullOrEmpty(key)) return;
        int current = GetParameter(key, 0);
        SetParameter(key, current + value);
    }

    // 演算子を用いた計算を適用する
    public static void ApplyOperation(string key, VariableOperation op, int operand)
    {
        if (string.IsNullOrEmpty(key)) return;

        int current = GetParameter(key, 0);
        int result = current;

        switch (op)
        {
            case VariableOperation.Set:
                result = operand;
                break;
            case VariableOperation.Add:
                result = current + operand;
                break;
            case VariableOperation.Subtract:
                result = current - operand;
                break;
            case VariableOperation.Multiply:
                result = current * operand;
                break;
            case VariableOperation.Divide:
                if (operand != 0) result = current / operand;
                else Debug.LogWarning($"【GameManager】ゼロ除算が試みられました: {key} / 0");
                break;
            case VariableOperation.Modulo:
                if (operand != 0) result = current % operand;
                else Debug.LogWarning($"【GameManager】ゼロ剰余が試みられました: {key} % 0");
                break;
        }

        SetParameter(key, result);
    }
    #endregion

    #region Other Types Support
    public static bool GetBool(string key, bool defaultValue = false)
    {
        if (string.IsNullOrEmpty(key)) return defaultValue;
        if (boolParams.TryGetValue(key, out bool value)) return value;
        return defaultValue;
    }

    public static void SetBool(string key, bool value)
    {
        if (string.IsNullOrEmpty(key)) return;
        boolParams[key] = value;
        OnVariableChanged?.Invoke(key);
    }

    public static float GetFloat(string key, float defaultValue = 0f)
    {
        if (string.IsNullOrEmpty(key)) return defaultValue;
        if (floatParams.TryGetValue(key, out float value)) return value;
        return defaultValue;
    }

    public static bool TryGetNumeric(string key, out float value)
    {
        value = 0f;
        if (string.IsNullOrEmpty(key)) return false;
        if (floatParams.TryGetValue(key, out float floatValue))
        {
            value = floatValue;
            return true;
        }
        if (intParams.TryGetValue(key, out int intValue))
        {
            value = intValue;
            return true;
        }
        return false;
    }

    public static void SetFloat(string key, float value)
    {
        if (string.IsNullOrEmpty(key)) return;
        floatParams[key] = value;
        OnVariableChanged?.Invoke(key);
    }

    public static string GetString(string key, string defaultValue = "")
    {
        if (string.IsNullOrEmpty(key)) return defaultValue;
        if (stringParams.TryGetValue(key, out string value)) return value;
        return defaultValue;
    }

    public static void SetString(string key, string value)
    {
        if (string.IsNullOrEmpty(key)) return;
        stringParams[key] = value;
        OnVariableChanged?.Invoke(key);
    }
    #endregion

    #region Save & Load System
    [Serializable]
    public class SaveData
    {
        public List<IntEntry> intEntries = new List<IntEntry>();
        public List<BoolEntry> boolEntries = new List<BoolEntry>();
        public List<FloatEntry> floatEntries = new List<FloatEntry>();
        public List<StringEntry> stringEntries = new List<StringEntry>();

        [Serializable] public struct IntEntry { public string key; public int value; }
        [Serializable] public struct BoolEntry { public string key; public bool value; }
        [Serializable] public struct FloatEntry { public string key; public float value; }
        [Serializable] public struct StringEntry { public string key; public string value; }
    }

    // 現在の状態をセーブデータ構造体にエクスポート
    public static SaveData ExportSaveData()
    {
        var data = new SaveData();
        foreach (var kvp in intParams) data.intEntries.Add(new SaveData.IntEntry { key = kvp.Key, value = kvp.Value });
        foreach (var kvp in boolParams) data.boolEntries.Add(new SaveData.BoolEntry { key = kvp.Key, value = kvp.Value });
        foreach (var kvp in floatParams) data.floatEntries.Add(new SaveData.FloatEntry { key = kvp.Key, value = kvp.Value });
        foreach (var kvp in stringParams) data.stringEntries.Add(new SaveData.StringEntry { key = kvp.Key, value = kvp.Value });
        return data;
    }

    // セーブデータ構造体から状態を復元
    public static void ImportSaveData(SaveData data)
    {
        if (data == null) return;
        ResetAll();

        if (data.intEntries != null)
            foreach (var e in data.intEntries) intParams[e.key] = e.value;
        if (data.boolEntries != null)
            foreach (var e in data.boolEntries) boolParams[e.key] = e.value;
        if (data.floatEntries != null)
            foreach (var e in data.floatEntries) floatParams[e.key] = e.value;
        if (data.stringEntries != null)
            foreach (var e in data.stringEntries) stringParams[e.key] = e.value;

        Debug.Log("【GameManager】セーブデータを復元しました。");
    }

    // JSON文字列として取得（セーブファイル作成用）
    public static string ToJson(bool prettyPrint = true)
    {
        return JsonUtility.ToJson(ExportSaveData(), prettyPrint);
    }

    // JSON文字列から復元（ロード用）
    public static void LoadFromJson(string json)
    {
        if (string.IsNullOrEmpty(json)) return;
        SaveData data = JsonUtility.FromJson<SaveData>(json);
        ImportSaveData(data);
    }

    // PlayerPrefsに保存
    public static void SaveToPlayerPrefs(string saveSlotKey = "Story_SaveData_Slot0")
    {
        string json = ToJson(false);
        PlayerPrefs.SetString(saveSlotKey, json);
        PlayerPrefs.Save();
        Debug.Log($"【GameManager】PlayerPrefsに保存しました ({saveSlotKey})");
    }

    // PlayerPrefsからロード
    public static bool LoadFromPlayerPrefs(string saveSlotKey = "Story_SaveData_Slot0")
    {
        if (PlayerPrefs.HasKey(saveSlotKey))
        {
            string json = PlayerPrefs.GetString(saveSlotKey);
            LoadFromJson(json);
            return true;
        }
        return false;
    }
    #endregion

    // すべての変数をリセット
    public static void ResetAll()
    {
        intParams.Clear();
        boolParams.Clear();
        floatParams.Clear();
        stringParams.Clear();
    }
}