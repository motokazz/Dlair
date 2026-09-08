using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class StoryGraphGuidUtility
{
    static StoryGraphGuidUtility()
    {
        EditorApplication.delayCall += StampMissingIdentities;
    }

    private static void StampMissingIdentities()
    {
        string[] assetGuids = AssetDatabase.FindAssets("t:StoryGraph");
        bool dirty = false;
        for (int i = 0; i < assetGuids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(assetGuids[i]);
            StoryGraph graph = AssetDatabase.LoadAssetAtPath<StoryGraph>(path);
            if (graph == null || !string.IsNullOrEmpty(graph.assetIdentity)) continue;

            graph.assetIdentity = assetGuids[i];
            EditorUtility.SetDirty(graph);
            dirty = true;
        }

        if (dirty)
        {
            AssetDatabase.SaveAssets();
        }
    }

    public static bool TryHandleImportedGraph(StoryGraph graph, string assetPath)
    {
        if (graph == null || string.IsNullOrEmpty(assetPath)) return false;

        string fileGuid = AssetDatabase.AssetPathToGUID(assetPath);
        if (string.IsNullOrEmpty(fileGuid)) return false;
        if (graph.assetIdentity == fileGuid) return false;

        bool duplicated = !string.IsNullOrEmpty(graph.assetIdentity);
        if (duplicated)
        {
            int remapped = RegenerateNodeGuids(graph, false);
            Debug.Log($"【StoryGraph】'{graph.name}' は複製されたため、ノード GUID を {remapped} 件振り直しました。");
        }

        graph.assetIdentity = fileGuid;
        EditorUtility.SetDirty(graph);
        return true;
    }

    public static int RegenerateNodeGuids(StoryGraph graph, bool recordUndo = true)
    {
        if (graph == null || graph.nodes == null) return 0;

        Dictionary<string, string> map = new Dictionary<string, string>();
        int count = 0;

        for (int i = 0; i < graph.nodes.Count; i++)
        {
            BaseNode node = graph.nodes[i];
            if (node == null) continue;

            string oldGuid = node.guid;
            string newGuid = Guid.NewGuid().ToString();
            if (!string.IsNullOrEmpty(oldGuid) && !map.ContainsKey(oldGuid))
            {
                map[oldGuid] = newGuid;
            }

            if (recordUndo)
            {
                Undo.RecordObject(node, "ノード GUID を再発行");
            }

            node.guid = newGuid;
            EditorUtility.SetDirty(node);
            count++;
        }

        if (graph.links != null)
        {
            for (int i = 0; i < graph.links.Count; i++)
            {
                StoryLinkData link = graph.links[i];
                if (link == null) continue;
                Remap(ref link.baseNodeGuid, map);
                Remap(ref link.targetNodeGuid, map);
            }
        }

        for (int i = 0; i < graph.nodes.Count; i++)
        {
            if (graph.nodes[i] is not GotoNode gotoNode) continue;
            if (string.IsNullOrEmpty(gotoNode.targetLabelGuid)) continue;
            if (!map.TryGetValue(gotoNode.targetLabelGuid, out string remapped)) continue;
            gotoNode.targetLabelGuid = remapped;
            EditorUtility.SetDirty(gotoNode);
        }

        EditorUtility.SetDirty(graph);
        return count;
    }

    private static void Remap(ref string guid, Dictionary<string, string> map)
    {
        if (string.IsNullOrEmpty(guid)) return;
        if (map.TryGetValue(guid, out string remapped))
        {
            guid = remapped;
        }
    }

    [MenuItem("Assets/Story Graph/ノード GUID を再発行", true)]
    private static bool ValidateRegenerateSelected()
    {
        return Selection.activeObject is StoryGraph;
    }

    [MenuItem("Assets/Story Graph/ノード GUID を再発行")]
    private static void RegenerateSelected()
    {
        UnityEngine.Object[] selected = Selection.objects;
        int graphs = 0;
        int nodes = 0;
        for (int i = 0; i < selected.Length; i++)
        {
            if (selected[i] is not StoryGraph graph) continue;
            Undo.RecordObject(graph, "ノード GUID を再発行");
            nodes += RegenerateNodeGuids(graph);
            graphs++;
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"【StoryGraph】{graphs} 件のグラフでノード GUID を {nodes} 件振り直しました。");
    }
}
