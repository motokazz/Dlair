using System;
using System.Collections.Generic;
using UnityEditor;

public class StoryGraphAssetPostprocessor : AssetPostprocessor
{
    private static readonly HashSet<string> pendingPaths = new HashSet<string>();
    private static bool scheduled;

    private static void OnPostprocessAllAssets(
        string[] importedAssets,
        string[] deletedAssets,
        string[] movedAssets,
        string[] movedFromAssetPaths)
    {
        if (importedAssets == null) return;

        for (int i = 0; i < importedAssets.Length; i++)
        {
            string path = importedAssets[i];
            if (string.IsNullOrEmpty(path) || !path.EndsWith(".asset", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            pendingPaths.Add(path);
        }

        if (pendingPaths.Count == 0 || scheduled) return;

        scheduled = true;
        EditorApplication.delayCall += ProcessPending;
    }

    private static void ProcessPending()
    {
        scheduled = false;
        if (pendingPaths.Count == 0) return;

        string[] paths = new string[pendingPaths.Count];
        pendingPaths.CopyTo(paths);
        pendingPaths.Clear();

        bool dirty = false;
        for (int i = 0; i < paths.Length; i++)
        {
            StoryGraph graph = AssetDatabase.LoadAssetAtPath<StoryGraph>(paths[i]);
            if (graph == null) continue;
            if (StoryGraphGuidUtility.TryHandleImportedGraph(graph, paths[i]))
            {
                dirty = true;
            }
        }

        if (dirty)
        {
            AssetDatabase.SaveAssets();
        }
    }
}
