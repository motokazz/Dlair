using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.Video;

/// <summary>
/// 数値変数と再生速度へ適用する演算。代入、加減、乗除、剰余。
/// </summary>
public enum VariableOperation
{
    Set,        // = (代入)
    Add,        // += (加算)
    Subtract,   // -= (減算)
    Multiply,   // *= (乗算)
    Divide,     // /= (除算)
    Modulo      // %= (剰余)
}

/// <summary>
/// ストーリー変数の実行中の値を持つ。データベースの初期値で始め、変更を通知し、セーブ用に出し入れする。
/// </summary>
public static class GameManager
{
    // 変数テーブル (キーと数値)
    private static Dictionary<string, int> intParams = new Dictionary<string, int>();
    private static Dictionary<string, bool> boolParams = new Dictionary<string, bool>();
    private static Dictionary<string, float> floatParams = new Dictionary<string, float>();
    private static Dictionary<string, string> stringParams = new Dictionary<string, string>();
    private static Dictionary<string, VideoClip> videoParams = new Dictionary<string, VideoClip>();
    private static Dictionary<string, AudioClip> audioParams = new Dictionary<string, AudioClip>();

    // 変数変更時のコールバックイベント
    public static event Action<string> OnVariableChanged;

    /// <summary>
    /// ローカル変数を書いたあとに、変更通知だけ出す。
    /// </summary>
    public static void NotifyChanged(string key)
    {
        if (string.IsNullOrEmpty(key)) return;
        OnVariableChanged?.Invoke(key);
    }

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
                case StoryVariableType.VideoClip:
                    videoParams[def.key] = def.defaultVideoClip;
                    break;
                case StoryVariableType.AudioClip:
                    audioParams[def.key] = def.defaultAudioClip;
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
        if (StoryLocalVariables.TryGetInt(key, out int local)) return local;
        if (intParams.TryGetValue(key, out int value)) return value;
        return defaultValue;
    }

    // パラメーターを直接設定する
    public static void SetParameter(string key, int value)
    {
        if (string.IsNullOrEmpty(key)) return;
        if (StoryLocalVariables.TrySetInt(key, value))
        {
            OnVariableChanged?.Invoke(key);
            return;
        }
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

    // 整数変数へ演算を適用する。計算は小数で行い、結果の小数点以下は切り捨てる。
    public static void ApplyOperation(string key, VariableOperation op, int operand)
    {
        ApplyOperationToInt(key, op, operand);
    }

    /// <summary>
    /// 整数変数へ小数の演算を適用する。計算は切り捨てず、格納時に小数点以下を切り捨てる。
    /// </summary>
    public static void ApplyOperationToInt(string key, VariableOperation op, float operand)
    {
        if (string.IsNullOrEmpty(key)) return;

        float current = GetParameter(key, 0);
        if (!TryCompute(key, current, op, operand, out float result)) return;
        SetParameter(key, TruncateToInt(result));
    }

    /// <summary>
    /// Float 変数へ演算を適用する。割り算は切り捨てない。
    /// </summary>
    public static void ApplyOperation(string key, VariableOperation op, float operand)
    {
        if (string.IsNullOrEmpty(key)) return;

        float current = GetFloat(key, 0f);
        if (!TryCompute(key, current, op, operand, out float result)) return;
        SetFloat(key, result);
    }

    static bool TryCompute(string key, float current, VariableOperation op, float operand, out float result)
    {
        result = current;
        switch (op)
        {
            case VariableOperation.Set:
                result = operand;
                return true;
            case VariableOperation.Add:
                result = current + operand;
                return true;
            case VariableOperation.Subtract:
                result = current - operand;
                return true;
            case VariableOperation.Multiply:
                result = current * operand;
                return true;
            case VariableOperation.Divide:
                if (operand != 0f)
                {
                    result = current / operand;
                    return true;
                }
                Debug.LogWarning($"【GameManager】ゼロ除算が試みられました: {key} / 0");
                return false;
            case VariableOperation.Modulo:
                if (operand != 0f)
                {
                    result = current % operand;
                    return true;
                }
                Debug.LogWarning($"【GameManager】ゼロ剰余が試みられました: {key} % 0");
                return false;
            default:
                return false;
        }
    }

    static int TruncateToInt(float value)
    {
        if (float.IsNaN(value) || float.IsInfinity(value)) return 0;
        if (value >= int.MaxValue) return int.MaxValue;
        if (value <= int.MinValue) return int.MinValue;
        return (int)value;
    }
    #endregion

    #region Other Types Support
    public static bool GetBool(string key, bool defaultValue = false)
    {
        if (string.IsNullOrEmpty(key)) return defaultValue;
        if (StoryLocalVariables.TryGetBool(key, out bool local)) return local;
        if (boolParams.TryGetValue(key, out bool value)) return value;
        return defaultValue;
    }

    public static bool HasBool(string key)
    {
        if (StoryLocalVariables.Owns(key)) return StoryLocalVariables.IsType(key, StoryVariableType.Bool);
        return !string.IsNullOrEmpty(key) && boolParams.ContainsKey(key);
    }

    public static bool HasFloat(string key)
    {
        if (StoryLocalVariables.Owns(key)) return StoryLocalVariables.IsType(key, StoryVariableType.Float);
        return !string.IsNullOrEmpty(key) && floatParams.ContainsKey(key);
    }

    public static void SetBool(string key, bool value)
    {
        if (string.IsNullOrEmpty(key)) return;
        if (StoryLocalVariables.TrySetBool(key, value))
        {
            OnVariableChanged?.Invoke(key);
            return;
        }
        boolParams[key] = value;
        Debug.Log($"【GameManager】 '{key}' を {(value ? "On" : "Off")} に設定しました。");
        OnVariableChanged?.Invoke(key);
    }

    public static void ToggleBool(string key)
    {
        SetBool(key, !GetBool(key));
    }

    public static float GetFloat(string key, float defaultValue = 0f)
    {
        if (string.IsNullOrEmpty(key)) return defaultValue;
        if (StoryLocalVariables.TryGetFloat(key, out float local)) return local;
        if (floatParams.TryGetValue(key, out float value)) return value;
        return defaultValue;
    }

    public static bool TryGetNumeric(string key, out float value)
    {
        value = 0f;
        if (string.IsNullOrEmpty(key)) return false;
        if (StoryLocalVariables.Owns(key))
        {
            StoryLocalVariables.TryGetNumeric(key, out value);
            return true;
        }
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
        if (boolParams.TryGetValue(key, out bool boolValue))
        {
            value = boolValue ? 1f : 0f;
            return true;
        }
        return false;
    }

    public static void SetFloat(string key, float value)
    {
        if (string.IsNullOrEmpty(key)) return;
        if (StoryLocalVariables.TrySetFloat(key, value))
        {
            OnVariableChanged?.Invoke(key);
            return;
        }
        floatParams[key] = value;
        OnVariableChanged?.Invoke(key);
    }

    public static string GetString(string key, string defaultValue = "")
    {
        if (string.IsNullOrEmpty(key)) return defaultValue;
        if (StoryLocalVariables.TryGetString(key, out string local)) return local;
        if (stringParams.TryGetValue(key, out string value)) return value;
        return defaultValue;
    }

    /// <summary>
    /// 実行時点の変数値を表示用の文字列にする。未登録のキーは false。
    /// </summary>
    public static bool TryGetDisplayText(string key, out string text)
    {
        text = "";
        if (string.IsNullOrEmpty(key)) return false;
        if (StoryLocalVariables.TryGetDisplayText(key, out text)) return true;

        if (stringParams.TryGetValue(key, out string stringValue))
        {
            text = stringValue ?? "";
            return true;
        }

        if (boolParams.TryGetValue(key, out bool boolValue))
        {
            text = boolValue ? "On" : "Off";
            return true;
        }

        if (floatParams.TryGetValue(key, out float floatValue))
        {
            text = floatValue.ToString("0.########", CultureInfo.InvariantCulture);
            return true;
        }

        if (intParams.TryGetValue(key, out int intValue))
        {
            text = intValue.ToString(CultureInfo.InvariantCulture);
            return true;
        }

        if (videoParams.TryGetValue(key, out VideoClip videoClip))
        {
            text = videoClip != null ? videoClip.name : "";
            return true;
        }

        if (audioParams.TryGetValue(key, out AudioClip audioClip))
        {
            text = audioClip != null ? audioClip.name : "";
            return true;
        }

        return false;
    }

    public static void SetString(string key, string value)
    {
        if (string.IsNullOrEmpty(key)) return;
        if (StoryLocalVariables.TrySetString(key, value))
        {
            OnVariableChanged?.Invoke(key);
            return;
        }
        stringParams[key] = value;
        OnVariableChanged?.Invoke(key);
    }

    /// <summary>
    /// VideoClip 変数を書く。セーブしない。
    /// </summary>
    public static void SetVideoClip(string key, VideoClip value)
    {
        if (string.IsNullOrEmpty(key)) return;
        if (StoryLocalVariables.TrySetVideo(key, value))
        {
            OnVariableChanged?.Invoke(key);
            return;
        }
        videoParams[key] = value;
        OnVariableChanged?.Invoke(key);
    }

    /// <summary>
    /// キーがあれば、null のクリップでも true。
    /// </summary>
    public static bool TryGetVideoClip(string key, out VideoClip value)
    {
        value = null;
        if (string.IsNullOrEmpty(key)) return false;
        if (StoryLocalVariables.TryGetVideo(key, out value)) return true;
        return videoParams.TryGetValue(key, out value);
    }

    public static VideoClip GetVideoClip(string key)
    {
        TryGetVideoClip(key, out VideoClip value);
        return value;
    }

    /// <summary>
    /// AudioClip 変数を書く。セーブしない。
    /// </summary>
    public static void SetAudioClip(string key, AudioClip value)
    {
        if (string.IsNullOrEmpty(key)) return;
        if (StoryLocalVariables.TrySetAudio(key, value))
        {
            OnVariableChanged?.Invoke(key);
            return;
        }
        audioParams[key] = value;
        OnVariableChanged?.Invoke(key);
    }

    /// <summary>
    /// キーがあれば、null のクリップでも true。
    /// </summary>
    public static bool TryGetAudioClip(string key, out AudioClip value)
    {
        value = null;
        if (string.IsNullOrEmpty(key)) return false;
        if (StoryLocalVariables.TryGetAudio(key, out value)) return true;
        return audioParams.TryGetValue(key, out value);
    }

    public static AudioClip GetAudioClip(string key)
    {
        TryGetAudioClip(key, out AudioClip value);
        return value;
    }
    #endregion

    #region Save & Load System
    /// <summary>
    /// 変数テーブルを JSON に書き出すための一覧。
    /// </summary>
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
        MergeSaveData(data);
        Debug.Log("【GameManager】セーブデータを復元しました。");
    }

    // 既にある値は残し、セーブにあるキーだけ上書きする
    public static void MergeSaveData(SaveData data)
    {
        if (data == null) return;

        if (data.intEntries != null)
            foreach (var e in data.intEntries)
                if (!string.IsNullOrEmpty(e.key)) intParams[e.key] = e.value;
        if (data.boolEntries != null)
            foreach (var e in data.boolEntries)
                if (!string.IsNullOrEmpty(e.key)) boolParams[e.key] = e.value;
        if (data.floatEntries != null)
            foreach (var e in data.floatEntries)
                if (!string.IsNullOrEmpty(e.key)) floatParams[e.key] = e.value;
        if (data.stringEntries != null)
            foreach (var e in data.stringEntries)
                if (!string.IsNullOrEmpty(e.key)) stringParams[e.key] = e.value;
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
    #endregion

    // すべての変数をリセット
    public static void ResetAll()
    {
        intParams.Clear();
        boolParams.Clear();
        floatParams.Clear();
        stringParams.Clear();
        videoParams.Clear();
        audioParams.Clear();
        StoryLocalVariables.Clear();
    }
}