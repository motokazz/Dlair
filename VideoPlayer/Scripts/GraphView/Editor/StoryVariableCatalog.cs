using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine.UIElements;

/// <summary>
/// 変数ノードのドロップダウンに出す名前。
/// StoryVariableDatabase と、グラフ上の変数宣言ノードから集める。
/// </summary>
public static class StoryVariableCatalog
{
    /// <summary>
    /// データベースの並びを先にし、開いているグラフの宣言、他グラフの宣言の順で返す。
    /// 同じ名前は1回だけ入れる。
    /// </summary>
    public static List<string> Collect(VisualElement context)
    {
        List<string> keys = new List<string>();
        HashSet<string> seen = new HashSet<string>();
        AddDatabases(keys, seen);

        StoryGraphView view = context != null ? context.GetFirstAncestorOfType<StoryGraphView>() : null;
        StoryGraph current = view != null ? view.CurrentGraph : null;
        if (view != null)
        {
            AddFromView(view, keys, seen);
        }

        string[] graphGuids = AssetDatabase.FindAssets("t:StoryGraph");
        for (int i = 0; i < graphGuids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(graphGuids[i]);
            StoryGraph graph = AssetDatabase.LoadAssetAtPath<StoryGraph>(path);
            if (graph == null || graph == current) continue;
            AddFromGraph(graph, keys, seen);
        }

        return keys;
    }

    private static void AddDatabases(List<string> keys, HashSet<string> seen)
    {
        string[] guids = AssetDatabase.FindAssets("t:StoryVariableDatabase");
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            StoryVariableDatabase database = AssetDatabase.LoadAssetAtPath<StoryVariableDatabase>(path);
            if (database == null) continue;

            List<string> databaseKeys = database.GetAllKeys();
            for (int k = 0; k < databaseKeys.Count; k++)
            {
                AddKey(keys, seen, databaseKeys[k]);
            }
        }
    }

    private static void AddFromView(StoryGraphView view, List<string> keys, HashSet<string> seen)
    {
        foreach (GraphElement element in view.graphElements)
        {
            if (element is not StoryNodeUI nodeUI || nodeUI.data is not VariableDeclareNode declareNode) continue;
            AddDeclarations(declareNode, keys, seen);
        }
    }

    private static void AddFromGraph(StoryGraph graph, List<string> keys, HashSet<string> seen)
    {
        if (graph?.nodes == null) return;

        for (int i = 0; i < graph.nodes.Count; i++)
        {
            if (graph.nodes[i] is VariableDeclareNode declareNode)
            {
                AddDeclarations(declareNode, keys, seen);
            }
        }
    }

    private static void AddDeclarations(VariableDeclareNode declareNode, List<string> keys, HashSet<string> seen)
    {
        if (declareNode?.variables == null) return;

        for (int i = 0; i < declareNode.variables.Count; i++)
        {
            VariableDeclaration entry = declareNode.variables[i];
            if (entry == null) continue;
            AddKey(keys, seen, entry.key);
        }
    }

    private static void AddKey(List<string> keys, HashSet<string> seen, string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return;

        string trimmed = key.Trim();
        if (!seen.Add(trimmed)) return;
        keys.Add(trimmed);
    }
}
