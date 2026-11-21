using System.IO;
using UnityEngine;

/// <summary>
/// セーブフォルダと、進行・設定・再生記録 JSON のパス。
/// </summary>
public static class SaveDataPaths
{
    public const string FolderName = "SaveData";
    public const string StoryFileName = "story.json";
    public const string OptionsFileName = "options.json";
    public const string VisitsFileName = "StoryGotoUnlocks.json";

    public static string Root => Path.Combine(Application.persistentDataPath, FolderName);

    public static string StoryFile => Path.Combine(Root, StoryFileName);

    public static string OptionsFile => Path.Combine(Root, OptionsFileName);

    public static string VisitsFile => Path.Combine(Root, VisitsFileName);
}
