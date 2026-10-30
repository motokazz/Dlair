using System.IO;
using UnityEngine;

/// <summary>
/// 進行と設定を JSON で出し入れする。マスターの初期値はそのまま使い、プレイヤーが変えた値だけを SaveData へ書く。
/// </summary>
public static class GameSave
{
    const string LegacyStoryPrefsKey = "Story_SaveData_Slot0";

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
        GameManager.OnVariableChanged += _ => SaveStory();
        Debug.Log($"【Save】セーブ先: {SaveDataPaths.Root}");
    }

    public static void ApplyStoryDatabase(StoryVariableDatabase database)
    {
        if (database == null)
            return;

        storyDatabase = database;
        GameManager.SaveData overlay = GameManager.ExportSaveData();
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
    }

    public static void SaveStory()
    {
        if (suppressStorySave)
            return;

        JsonSaveFile.Write(SaveDataPaths.StoryFile, GameManager.ToJson(true));
    }

    public static void ClearStory(StoryVariableDatabase database = null)
    {
        StoryVariableDatabase source = ResolveDatabase(database);
        suppressStorySave = true;
        try
        {
            JsonSaveFile.Delete(SaveDataPaths.StoryFile);
            if (source != null)
            {
                storyDatabase = source;
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
        suppressStorySave = true;
        try
        {
            JsonSaveFile.DeleteDirectory(SaveDataPaths.Root);
            GameSettings.ClearMemory();
            StoryVariableDatabase source = ResolveDatabase(database);
            if (source != null)
            {
                storyDatabase = source;
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
}
