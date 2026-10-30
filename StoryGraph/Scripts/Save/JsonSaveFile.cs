using System;
using System.IO;
using UnityEngine;

/// <summary>
/// セーブ用 JSON ファイルの読み書きと削除。
/// </summary>
public static class JsonSaveFile
{
    public static bool Exists(string path)
    {
        return !string.IsNullOrEmpty(path) && File.Exists(path);
    }

    public static bool TryRead(string path, out string json)
    {
        json = null;
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
            return false;

        try
        {
            json = File.ReadAllText(path);
            return !string.IsNullOrEmpty(json);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"【Save】読み込みに失敗しました: {path}\n{e.Message}");
            return false;
        }
    }

    public static bool Write(string path, string json)
    {
        if (string.IsNullOrEmpty(path))
            return false;

        try
        {
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            string tempPath = path + ".tmp";
            File.WriteAllText(tempPath, json ?? "");
            File.Copy(tempPath, path, true);
            File.Delete(tempPath);
            return true;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"【Save】書き込みに失敗しました: {path}\n{e.Message}");
            return false;
        }
    }

    public static void Delete(string path)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
            return;

        try
        {
            File.Delete(path);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"【Save】削除に失敗しました: {path}\n{e.Message}");
        }
    }

    public static void DeleteDirectory(string path)
    {
        if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
            return;

        try
        {
            Directory.Delete(path, true);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"【Save】フォルダの削除に失敗しました: {path}\n{e.Message}");
        }
    }
}
