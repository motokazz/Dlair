using System.Collections.Generic;
using UnityEngine;

public static class GameManager
{
    // パラメーターの名前と数値をペアで保存する辞書
    private static Dictionary<string, int> parameters = new Dictionary<string, int>();

    // パラメーターを加算する
    public static void AddParameter(string key, int value)
    {
        if (string.IsNullOrEmpty(key)) return;
        if (!parameters.ContainsKey(key)) parameters[key] = 0;

        parameters[key] += value;
        Debug.Log($"【GameManager】 '{key}' に {value} 加算されました。（現在値: {parameters[key]}）");
    }

    // パラメーターの値を取得する
    public static int GetParameter(string key)
    {
        if (string.IsNullOrEmpty(key)) return 0;
        if (parameters.TryGetValue(key, out int value)) return value;
        return 0; // まだ追加されていないパラメーターは0を返す
    }

    // タイトル画面に戻る時など、すべてリセットしたい時に呼ぶ
    public static void ResetAll()
    {
        parameters.Clear();
    }
}