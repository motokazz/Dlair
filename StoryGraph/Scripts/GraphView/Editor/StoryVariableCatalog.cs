using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
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
        return Collect(context, null);
    }

    /// <summary>
    /// type を指定するとその型だけ返す。指定しないときは VideoClip と AudioClip を除く。
    /// </summary>
    public static List<string> Collect(VisualElement context, StoryVariableType? type)
    {
        List<string> keys = new List<string>();
        HashSet<string> seen = new HashSet<string>();
        AddDatabases(keys, seen, type);

        StoryGraphView view = context != null ? context.GetFirstAncestorOfType<StoryGraphView>() : null;
        StoryGraph current = view != null ? view.CurrentGraph : null;
        if (view != null)
        {
            AddFromView(view, keys, seen, type);
        }

        string[] graphGuids = AssetDatabase.FindAssets("t:StoryGraph");
        for (int i = 0; i < graphGuids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(graphGuids[i]);
            StoryGraph graph = AssetDatabase.LoadAssetAtPath<StoryGraph>(path);
            if (graph == null || graph == current) continue;
            AddFromGraph(graph, keys, seen, type);
        }

        return keys;
    }

    static Dictionary<string, StoryVariableType> cachedTypes;
    static EntityId cachedGraphId;
    static double cachedAt = double.MinValue;

    /// <summary>
    /// 変数名の型。データベースを先に見て、なければ開いているグラフ、他グラフの宣言の順。
    /// </summary>
    public static bool TryGetType(VisualElement context, string key, out StoryVariableType type)
    {
        type = StoryVariableType.Int;
        if (string.IsNullOrWhiteSpace(key)) return false;

        StoryGraphView view = context != null ? context.GetFirstAncestorOfType<StoryGraphView>() : null;
        StoryGraph current = view != null ? view.CurrentGraph : null;
        EntityId graphId = current != null ? current.GetEntityId() : default;
        Dictionary<string, StoryVariableType> types = TypeMap(view, current, graphId);
        return types.TryGetValue(key.Trim(), out type);
    }

    /// <summary>
    /// 変数名の型。データベースを先に見て、なければ各グラフの宣言を見る。
    /// </summary>
    public static bool TryGetType(string key, out StoryVariableType type)
    {
        return TryGetType(null, key, out type);
    }

    static Dictionary<string, StoryVariableType> TypeMap(StoryGraphView view, StoryGraph current, EntityId graphId)
    {
        double now = EditorApplication.timeSinceStartup;
        if (cachedTypes != null && cachedGraphId.Equals(graphId) && now - cachedAt < 1d)
        {
            return cachedTypes;
        }

        Dictionary<string, StoryVariableType> types = new Dictionary<string, StoryVariableType>();
        AddDatabaseTypes(types);
        if (view != null) AddViewTypes(view, types);

        string[] graphGuids = AssetDatabase.FindAssets("t:StoryGraph");
        for (int i = 0; i < graphGuids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(graphGuids[i]);
            StoryGraph graph = AssetDatabase.LoadAssetAtPath<StoryGraph>(path);
            if (graph == null || graph == current) continue;
            AddGraphTypes(graph, types);
        }

        if (view != null) AddLocalViewTypes(view, types);

        cachedTypes = types;
        cachedGraphId = graphId;
        cachedAt = now;
        return types;
    }

    static void AddDatabaseTypes(Dictionary<string, StoryVariableType> types)
    {
        string[] guids = AssetDatabase.FindAssets("t:StoryVariableDatabase");
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            StoryVariableDatabase database = AssetDatabase.LoadAssetAtPath<StoryVariableDatabase>(path);
            if (database == null || database.variables == null) continue;

            for (int k = 0; k < database.variables.Count; k++)
            {
                StoryVariableDefinition definition = database.variables[k];
                if (definition == null) continue;
                AddType(types, definition.key, definition.type);
            }
        }
    }

    static void AddViewTypes(StoryGraphView view, Dictionary<string, StoryVariableType> types)
    {
        foreach (GraphElement element in view.graphElements)
        {
            if (element is not StoryNodeUI nodeUI || nodeUI.data is not VariableDeclareNode declareNode) continue;
            AddDeclarationTypes(declareNode, types);
        }
    }

    static void AddGraphTypes(StoryGraph graph, Dictionary<string, StoryVariableType> types)
    {
        if (graph?.nodes == null) return;

        for (int i = 0; i < graph.nodes.Count; i++)
        {
            if (graph.nodes[i] is VariableDeclareNode declareNode)
            {
                AddDeclarationTypes(declareNode, types);
            }
        }
    }

    static void AddDeclarationTypes(VariableDeclareNode declareNode, Dictionary<string, StoryVariableType> types)
    {
        if (declareNode?.variables == null) return;

        for (int i = 0; i < declareNode.variables.Count; i++)
        {
            VariableDeclaration entry = declareNode.variables[i];
            if (entry == null) continue;
            AddType(types, entry.key, entry.type);
        }
    }

    static void AddType(Dictionary<string, StoryVariableType> types, string key, StoryVariableType type)
    {
        if (type == StoryVariableType.Heading || string.IsNullOrWhiteSpace(key)) return;
        string trimmed = key.Trim();
        if (!types.ContainsKey(trimmed)) types.Add(trimmed, type);
    }

    private static void AddDatabases(List<string> keys, HashSet<string> seen, StoryVariableType? type)
    {
        string[] guids = AssetDatabase.FindAssets("t:StoryVariableDatabase");
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            StoryVariableDatabase database = AssetDatabase.LoadAssetAtPath<StoryVariableDatabase>(path);
            if (database == null || database.variables == null) continue;

            for (int k = 0; k < database.variables.Count; k++)
            {
                StoryVariableDefinition definition = database.variables[k];
                if (definition == null || !Include(definition.type, type)) continue;
                AddKey(keys, seen, definition.key);
            }
        }
    }

    private static void AddFromView(StoryGraphView view, List<string> keys, HashSet<string> seen, StoryVariableType? type)
    {
        foreach (GraphElement element in view.graphElements)
        {
            if (element is not StoryNodeUI nodeUI) continue;
            if (nodeUI.data is VariableDeclareNode declareNode)
            {
                AddDeclarations(declareNode, keys, seen, type);
            }
            else if (nodeUI.data is LocalVariableDeclareNode localNode)
            {
                AddDeclarationList(localNode.variables, keys, seen, type);
            }
        }
    }

    private static void AddFromGraph(StoryGraph graph, List<string> keys, HashSet<string> seen, StoryVariableType? type)
    {
        if (graph?.nodes == null) return;

        for (int i = 0; i < graph.nodes.Count; i++)
        {
            if (graph.nodes[i] is VariableDeclareNode declareNode)
            {
                AddDeclarations(declareNode, keys, seen, type);
            }
        }
    }

    private static void AddDeclarations(VariableDeclareNode declareNode, List<string> keys, HashSet<string> seen, StoryVariableType? type)
    {
        AddDeclarationList(declareNode?.variables, keys, seen, type);
    }

    private static void AddDeclarationList(List<VariableDeclaration> variables, List<string> keys, HashSet<string> seen, StoryVariableType? type)
    {
        if (variables == null) return;

        for (int i = 0; i < variables.Count; i++)
        {
            VariableDeclaration entry = variables[i];
            if (entry == null || !Include(entry.type, type)) continue;
            AddKey(keys, seen, entry.key);
        }
    }

    static void AddLocalViewTypes(StoryGraphView view, Dictionary<string, StoryVariableType> types)
    {
        foreach (GraphElement element in view.graphElements)
        {
            if (element is not StoryNodeUI nodeUI || nodeUI.data is not LocalVariableDeclareNode localNode) continue;
            if (localNode.variables == null) continue;

            for (int i = 0; i < localNode.variables.Count; i++)
            {
                VariableDeclaration entry = localNode.variables[i];
                if (entry == null || entry.type == StoryVariableType.Heading || string.IsNullOrWhiteSpace(entry.key)) continue;
                types[entry.key.Trim()] = entry.type;
            }
        }
    }

    private static bool Include(StoryVariableType actual, StoryVariableType? type)
    {
        if (actual == StoryVariableType.Heading) return false;
        if (type.HasValue) return actual == type.Value;
        return actual != StoryVariableType.VideoClip && actual != StoryVariableType.AudioClip;
    }

    private static void AddKey(List<string> keys, HashSet<string> seen, string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return;

        string trimmed = key.Trim();
        if (!seen.Add(trimmed)) return;
        keys.Add(trimmed);
    }
}
