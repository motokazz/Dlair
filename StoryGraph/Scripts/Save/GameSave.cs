using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// 説明は GameSave.Summary。
/// </summary>
public static class GameSave
{
    public const string Summary =
        "進行データは、指定した StoryVariableDatabase にある変数だけを story.json に書きます。\n" +
        "StoryGraph の変数宣言ノードで作った変数は、実行中は使いますが保存しません。\n" +
        "再生したラベルは、グラフのアセット名とラベル名の組で StoryGotoUnlocks.json に残します。";

    const string LegacyStoryPrefsKey = "Story_SaveData_Slot0";

    static readonly HashSet<string> databaseKeys = new HashSet<string>();
    static StoryVariableDatabase storyDatabase;
    static bool booted;
    static bool suppressStorySave;

    public static string DirectoryPath => SaveDataPaths.Root;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Boot()
    {
        if (booted)
            return;

        booted = true;
        GameSettings.Load();
        LoadStoryFromDisk();
        StoryVisitLog.Load();
        GameManager.OnVariableChanged += SaveStory;
        Debug.Log($"【Save】セーブ先: {SaveDataPaths.Root}");
    }

    public static void ApplyStoryDatabase(StoryVariableDatabase database)
    {
        if (database == null)
            return;

        RememberDatabase(database);
        GameManager.SaveData overlay = FilterToDatabase(GameManager.ExportSaveData());
        suppressStorySave = true;
        try
        {
            GameManager.InitializeFromDatabase(database);
            GameManager.MergeSaveData(overlay);
        }
        finally
        {
            suppressStorySave = false;
        }

        SaveStory();
    }

    public static void SaveStory(string changedKey = null)
    {
        if (suppressStorySave)
            return;

        if (storyDatabase != null && changedKey != null && !databaseKeys.Contains(changedKey))
            return;

        GameManager.SaveData data = GameManager.ExportSaveData();
        if (storyDatabase != null)
            data = FilterToDatabase(data);

        JsonSaveFile.Write(SaveDataPaths.StoryFile, JsonUtility.ToJson(data, true));
    }

    public static void ClearStory(StoryVariableDatabase database = null)
    {
        StoryVariableDatabase source = ResolveDatabase(database);
        StoryVisitLog.Clear();
        suppressStorySave = true;
        try
        {
            JsonSaveFile.Delete(SaveDataPaths.StoryFile);
            if (source != null)
            {
                RememberDatabase(source);
                GameManager.InitializeFromDatabase(source);
            }
            else
            {
                GameManager.ResetAll();
            }
        }
        finally
        {
            suppressStorySave = false;
        }

        Debug.Log("【Save】進行データを初期状態に戻しました。");
    }

    public static void ClearOptions()
    {
        GameSettings.Clear();
        AudioOptions.DeleteLegacyPlayerPrefs();
        AudioOptions.ApplyAll();
        Debug.Log("【Save】オプションデータを初期状態に戻しました。");
    }

    public static void DeleteAll(StoryVariableDatabase database = null)
    {
        StoryVisitLog.Clear();
        suppressStorySave = true;
        try
        {
            JsonSaveFile.DeleteDirectory(SaveDataPaths.Root);
            GameSettings.ClearMemory();
            StoryVariableDatabase source = ResolveDatabase(database);
            if (source != null)
            {
                RememberDatabase(source);
                GameManager.InitializeFromDatabase(source);
            }
            else
            {
                GameManager.ResetAll();
            }
        }
        finally
        {
            suppressStorySave = false;
        }

        AudioOptions.DeleteLegacyPlayerPrefs();
        GameSettings.NotifyCleared();
        AudioOptions.ApplyAll();
        Debug.Log($"【Save】セーブデータを削除しました: {SaveDataPaths.Root}");
    }

    public static void LogContents()
    {
        string root = SaveDataPaths.Root;
        Debug.Log($"【Save】セーブ先: {root}");

        if (!Directory.Exists(root))
        {
            Debug.Log("【Save】セーブデータはありません。");
            return;
        }

        string[] files = Directory.GetFiles(root);
        System.Array.Sort(files, System.StringComparer.Ordinal);
        bool any = false;
        for (int i = 0; i < files.Length; i++)
        {
            if (files[i].EndsWith(".tmp", System.StringComparison.OrdinalIgnoreCase))
                continue;

            any = true;
            string name = Path.GetFileName(files[i]);
            if (JsonSaveFile.TryRead(files[i], out string json))
                Debug.Log($"【Save】{name}\n{json}");
            else
                Debug.Log($"【Save】{name}\n（空、または読み込めません）");
        }

        if (!any)
            Debug.Log("【Save】セーブフォルダは空です。");
    }

    static void LoadStoryFromDisk()
    {
        suppressStorySave = true;
        try
        {
            if (JsonSaveFile.TryRead(SaveDataPaths.StoryFile, out string json))
            {
                GameManager.LoadFromJson(json);
                DeleteLegacyStoryPrefs();
                return;
            }

            if (!PlayerPrefs.HasKey(LegacyStoryPrefsKey))
                return;

            GameManager.LoadFromJson(PlayerPrefs.GetString(LegacyStoryPrefsKey));
            DeleteLegacyStoryPrefs();
        }
        finally
        {
            suppressStorySave = false;
        }

        SaveStory();
    }

    static void DeleteLegacyStoryPrefs()
    {
        if (!PlayerPrefs.HasKey(LegacyStoryPrefsKey))
            return;

        PlayerPrefs.DeleteKey(LegacyStoryPrefsKey);
        PlayerPrefs.Save();
    }

    static StoryVariableDatabase ResolveDatabase(StoryVariableDatabase preferred)
    {
        if (preferred != null)
            return preferred;
        if (storyDatabase != null)
            return storyDatabase;

        StoryPlayer player = UnityEngine.Object.FindFirstObjectByType<StoryPlayer>();
        return player != null ? player.variableDatabase : null;
    }

    static void RememberDatabase(StoryVariableDatabase database)
    {
        storyDatabase = database;
        databaseKeys.Clear();
        if (database == null)
            return;

        List<string> keys = database.GetAllKeys();
        for (int i = 0; i < keys.Count; i++)
            databaseKeys.Add(keys[i]);
    }

    static GameManager.SaveData FilterToDatabase(GameManager.SaveData source)
    {
        var filtered = new GameManager.SaveData();
        if (source == null)
            return filtered;

        Keep(source.intEntries, filtered.intEntries, entry => entry.key);
        Keep(source.boolEntries, filtered.boolEntries, entry => entry.key);
        Keep(source.floatEntries, filtered.floatEntries, entry => entry.key);
        Keep(source.stringEntries, filtered.stringEntries, entry => entry.key);
        return filtered;
    }

    static void Keep<T>(List<T> source, List<T> destination, Func<T, string> keyOf)
    {
        if (source == null)
            return;

        for (int i = 0; i < source.Count; i++)
        {
            string key = keyOf(source[i]);
            if (!string.IsNullOrEmpty(key) && databaseKeys.Contains(key))
                destination.Add(source[i]);
        }
    }
}
