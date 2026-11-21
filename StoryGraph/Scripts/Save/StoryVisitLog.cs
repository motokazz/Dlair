using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 再生済みラベル。グラフ名とラベル名の組。
/// </summary>
[Serializable]
public class StoryVisitEntry
{
    public string graphName;
    public string labelName;
}

/// <summary>
/// 再生したラベルを、グラフのアセット名とラベル名の組で残す。
/// 説明は StoryVisitLog.Summary。
/// </summary>
public static class StoryVisitLog
{
    public const string Summary =
        "ラベルに到達すると、再生中の StoryGraph のアセット名とラベル名を1件として StoryGotoUnlocks.json に足します。\n" +
        "同じラベル名でも、グラフのアセット名が違えば別の記録です。\n" +
        "参照は StoryVisitLog.IsVisited、または StoryGraph.IsLabelVisited です。変数データベースでキーを見るのと同じように、グラフを指定してラベル名を見ます。\n" +
        "進行データのクリアと、セーブデータの全消去で消えます。";

    public static event Action OnChanged;

    static readonly List<StoryVisitEntry> entries = new List<StoryVisitEntry>();
    static readonly Dictionary<string, HashSet<string>> index = new Dictionary<string, HashSet<string>>();
    static bool loaded;

    public static bool IsVisited(StoryGraph graph, string labelName)
    {
        if (graph == null)
            return false;

        return IsVisited(graph.name, labelName);
    }

    public static bool IsVisited(string graphName, string labelName)
    {
        Load();
        if (string.IsNullOrEmpty(graphName) || string.IsNullOrWhiteSpace(labelName))
            return false;

        return index.TryGetValue(graphName, out HashSet<string> labels) && labels.Contains(labelName.Trim());
    }

    public static void Record(StoryGraph graph, string labelName)
    {
        if (!Application.isPlaying || graph == null)
            return;

        Record(graph.name, labelName);
    }

    public static void Record(string graphName, string labelName)
    {
        if (!Application.isPlaying || string.IsNullOrEmpty(graphName) || string.IsNullOrWhiteSpace(labelName))
            return;

        Load();
        labelName = labelName.Trim();
        if (!TryAdd(graphName, labelName))
            return;

        Save();
        OnChanged?.Invoke();
    }

    public static void Load()
    {
        if (loaded)
            return;

        loaded = true;
        entries.Clear();
        index.Clear();

        if (!JsonSaveFile.TryRead(SaveDataPaths.VisitsFile, out string json))
            return;

        StoryVisitSaveData data = JsonUtility.FromJson<StoryVisitSaveData>(json);
        if (data?.entries == null)
            return;

        for (int i = 0; i < data.entries.Count; i++)
        {
            StoryVisitEntry entry = data.entries[i];
            if (entry == null || string.IsNullOrEmpty(entry.graphName) || string.IsNullOrWhiteSpace(entry.labelName))
                continue;

            TryAdd(entry.graphName, entry.labelName.Trim());
        }
    }

    public static void Clear()
    {
        entries.Clear();
        index.Clear();
        loaded = true;
        JsonSaveFile.Delete(SaveDataPaths.VisitsFile);
        OnChanged?.Invoke();
    }

    static bool TryAdd(string graphName, string labelName)
    {
        if (!index.TryGetValue(graphName, out HashSet<string> labels))
        {
            labels = new HashSet<string>();
            index.Add(graphName, labels);
        }

        if (!labels.Add(labelName))
            return false;

        entries.Add(new StoryVisitEntry { graphName = graphName, labelName = labelName });
        return true;
    }

    static void Save()
    {
        StoryVisitSaveData data = new StoryVisitSaveData { entries = entries };
        JsonSaveFile.Write(SaveDataPaths.VisitsFile, JsonUtility.ToJson(data, true));
    }

    [Serializable]
    class StoryVisitSaveData
    {
        public List<StoryVisitEntry> entries = new List<StoryVisitEntry>();
    }
}
