using System.IO;
using UnityEngine;

public static class SaveDataPaths
{
    public const string FolderName = "SaveData";
    public const string StoryFileName = "story.json";
    public const string OptionsFileName = "options.json";

    public static string Root => Path.Combine(Application.persistentDataPath, FolderName);

    public static string StoryFile => Path.Combine(Root, StoryFileName);

    public static string OptionsFile => Path.Combine(Root, OptionsFileName);
}
