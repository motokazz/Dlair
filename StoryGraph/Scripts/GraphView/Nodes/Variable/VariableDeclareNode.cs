using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Video;

/// <summary>
/// 変数宣言ノードの1件。名前・型・初期値を持つ。
/// </summary>
[Serializable]
public class VariableDeclaration
{
    [Tooltip("他の変数ノードの一覧に出る名前")]
    public string key = "Variable 1";

    public StoryVariableType type = StoryVariableType.Int;

    public int defaultIntValue;
    public bool defaultBoolValue;
    public float defaultFloatValue;
    public string defaultStringValue = "";
    public VideoClip defaultVideoClip;
    public AudioClip defaultAudioClip;
}

/// <summary>
/// 変数宣言の一覧を持つノード。グローバル宣言とローカル宣言の両方。
/// </summary>
public interface IDeclarationList
{
    List<VariableDeclaration> Variables { get; set; }
    string DeclarationHelp { get; }
}

/// <summary>
/// 複数の変数をまとめて宣言する。
/// StoryVariableDatabase の変数と合わせて、変数ノードの選択肢になる。
/// グラフ開始時は、まだ値が無い変数にだけ初期値を入れる。
/// このノードを通過したときは、宣言した値を入れる。
/// ここで宣言した変数はセーブしない。StoryVariableDatabase にある同名変数は上書きしない。
/// </summary>
public class VariableDeclareNode : BaseNode, IDeclarationList
{
    public List<VariableDeclaration> variables = new List<VariableDeclaration>
    {
        new VariableDeclaration()
    };

    public List<VariableDeclaration> Variables
    {
        get => variables;
        set => variables = value;
    }

    public string DeclarationHelp => GetHelpText();

    /// <summary>
    /// このノードの宣言値を入れ、Next へ進む。データベースの変数は上書きしない。
    /// </summary>
    public override void Execute(StoryPlayer player)
    {
        ApplyDeclaredValues(player);
        if (player != null)
        {
            player.ContinueTo(this, "Next");
        }
    }

    /// <summary>
    /// グラフと、そこから辿れるサブグラフ・Goto 先にある宣言を反映する。
    /// </summary>
    public static void ApplyMissingInGraph(StoryGraph root)
    {
        ApplyMissingInGraph(root, new HashSet<StoryGraph>());
    }

    /// <summary>
    /// まだ GameManager に値が無いキーへ、宣言どおりの初期値を入れる。
    /// </summary>
    public void ApplyMissingDefaults()
    {
        if (variables == null) return;

        StoryLocalVariables.BeginGlobalWrite();
        try
        {
            for (int i = 0; i < variables.Count; i++)
            {
                ApplyMissing(variables[i]);
            }
        }
        finally
        {
            StoryLocalVariables.EndGlobalWrite();
        }
    }

    /// <summary>
    /// ノードタイトル用の変数名一覧。
    /// </summary>
    public string GetDisplayTitle()
    {
        if (variables == null || variables.Count == 0) return "Declare";

        List<string> parts = new List<string>(variables.Count);
        for (int i = 0; i < variables.Count; i++)
        {
            VariableDeclaration entry = variables[i];
            if (entry == null || string.IsNullOrWhiteSpace(entry.key)) continue;
            parts.Add(entry.key.Trim());
        }

        return parts.Count == 0 ? "Declare" : string.Join(", ", parts);
    }

    /// <summary>
    /// グラフ上の説明文。
    /// </summary>
    public string GetHelpText()
    {
        return "変数をまとめて宣言します。ここでの名前と Variable Database の変数が、他の変数ノードの一覧になります。Int、Bool、Float、String、VideoClip、AudioClip を宣言できます。見出しは区切りだけで、値は持ちません。左をドラッグすると並びを変えられ、SubGraph Input もこの順で出ます。グラフ開始時は、まだ値が無い変数にだけ初期値を入れます。このノードを通過したときは、宣言した値を入れます。データベースにある同名の変数は上書きしません。ここで宣言した変数はセーブしません。";
    }

    /// <summary>
    /// 追加行の初期名。同じノード内の名前と重ならないようにする。
    /// </summary>
    public static string NextDefaultKey(List<VariableDeclaration> list)
    {
        HashSet<string> used = new HashSet<string>();
        if (list != null)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] != null && !string.IsNullOrWhiteSpace(list[i].key))
                {
                    used.Add(list[i].key.Trim());
                }
            }
        }

        int n = 1;
        while (used.Contains("Variable " + n)) n++;
        return "Variable " + n;
    }

    /// <summary>
    /// 追加する見出しの初期名。同じノード内の名前と重ならないようにする。
    /// </summary>
    public static string NextHeadingKey(List<VariableDeclaration> list)
    {
        HashSet<string> used = new HashSet<string>();
        if (list != null)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] != null && !string.IsNullOrWhiteSpace(list[i].key))
                {
                    used.Add(list[i].key.Trim());
                }
            }
        }

        int n = 1;
        while (used.Contains("見出し " + n)) n++;
        return "見出し " + n;
    }

    /// <summary>
    /// グラフ内の宣言を、表示と同じ順で並べ替える。空の名前と重複名は表示に出ないので動かさない。
    /// </summary>
    public static bool MoveShown(StoryGraph graph, int fromShown, int toShown)
    {
        List<DeclarationSlot> slots = CollectSlots(graph);
        List<int> shown = new List<int>();
        HashSet<string> seen = new HashSet<string>();
        for (int i = 0; i < slots.Count; i++)
        {
            VariableDeclaration entry = slots[i].entry;
            if (entry == null || string.IsNullOrWhiteSpace(entry.key)) continue;
            if (!seen.Add(entry.key.Trim())) continue;
            shown.Add(i);
        }

        if (fromShown < 0 || toShown < 0 || fromShown >= shown.Count || toShown >= shown.Count || fromShown == toShown)
        {
            return false;
        }

        MoveSlots(slots, shown[fromShown], shown[toShown]);
        return true;
    }

    /// <summary>
    /// 1つの宣言リストの中で並べ替える。
    /// </summary>
    public static void MoveList(List<VariableDeclaration> list, int from, int to)
    {
        if (list == null || from == to) return;
        if (from < 0 || to < 0 || from >= list.Count || to >= list.Count) return;

        VariableDeclaration item = list[from];
        list.RemoveAt(from);
        list.Insert(to, item);
    }

    /// <summary>
    /// 並べ替えで書き換える宣言ノード。
    /// </summary>
    public static List<VariableDeclareNode> CollectDeclareNodes(StoryGraph graph)
    {
        List<VariableDeclareNode> nodes = new List<VariableDeclareNode>();
        if (graph == null || graph.nodes == null) return nodes;

        for (int i = 0; i < graph.nodes.Count; i++)
        {
            if (graph.nodes[i] is VariableDeclareNode declareNode)
            {
                nodes.Add(declareNode);
            }
        }

        return nodes;
    }

    private static void MoveSlots(List<DeclarationSlot> slots, int from, int to)
    {
        List<VariableDeclaration> items = new List<VariableDeclaration>(slots.Count);
        for (int i = 0; i < slots.Count; i++)
        {
            items.Add(slots[i].entry);
        }

        VariableDeclaration moved = items[from];
        items.RemoveAt(from);
        items.Insert(to, moved);
        for (int i = 0; i < slots.Count; i++)
        {
            slots[i].list[slots[i].index] = items[i];
        }
    }

    private static List<DeclarationSlot> CollectSlots(StoryGraph graph)
    {
        List<DeclarationSlot> slots = new List<DeclarationSlot>();
        if (graph == null || graph.nodes == null) return slots;

        for (int i = 0; i < graph.nodes.Count; i++)
        {
            List<VariableDeclaration> variables = null;
            if (graph.nodes[i] is VariableDeclareNode declareNode) variables = declareNode.variables;
            else if (graph.nodes[i] is LocalVariableDeclareNode localNode) variables = localNode.variables;
            if (variables == null) continue;

            for (int v = 0; v < variables.Count; v++)
            {
                if (variables[v] == null) continue;
                slots.Add(new DeclarationSlot
                {
                    list = variables,
                    index = v,
                    entry = variables[v]
                });
            }
        }

        return slots;
    }

    private class DeclarationSlot
    {
        public List<VariableDeclaration> list;
        public int index;
        public VariableDeclaration entry;
    }

    private static void ApplyMissingInGraph(StoryGraph graph, HashSet<StoryGraph> visited)
    {
        if (graph == null || graph.nodes == null || !visited.Add(graph)) return;

        for (int i = 0; i < graph.nodes.Count; i++)
        {
            if (graph.nodes[i] is VariableDeclareNode declareNode)
            {
                declareNode.ApplyMissingDefaults();
            }
        }

        for (int i = 0; i < graph.nodes.Count; i++)
        {
            BaseNode node = graph.nodes[i];
            if (StoryGraph.TryGetNestedGraph(node, out StoryGraph nested))
            {
                ApplyMissingInGraph(nested, visited);
            }
            else if (node is GotoNode gotoNode)
            {
                ApplyMissingInGraph(gotoNode.targetGraph, visited);
            }
        }
    }

    /// <summary>
    /// 通過時に宣言値を入れる。データベースにある名前は残す。
    /// </summary>
    public void ApplyDeclaredValues(StoryPlayer player)
    {
        if (variables == null) return;

        StoryLocalVariables.BeginGlobalWrite();
        try
        {
            for (int i = 0; i < variables.Count; i++)
            {
                ApplyDeclared(player, variables[i]);
            }
        }
        finally
        {
            StoryLocalVariables.EndGlobalWrite();
        }
    }

    private static void ApplyDeclared(StoryPlayer player, VariableDeclaration entry)
    {
        if (entry == null || string.IsNullOrWhiteSpace(entry.key)) return;

        string key = entry.key.Trim();
        if (entry.type == StoryVariableType.Heading) return;
        if (IsDatabaseKey(player, key)) return;

        WriteDeclared(entry, key);
    }

    private static bool IsDatabaseKey(StoryPlayer player, string key)
    {
        StoryVariableDatabase database = player != null ? player.variableDatabase : null;
        if (database == null || database.variables == null) return false;

        for (int i = 0; i < database.variables.Count; i++)
        {
            StoryVariableDefinition definition = database.variables[i];
            if (definition == null || string.IsNullOrWhiteSpace(definition.key)) continue;
            if (string.Equals(definition.key.Trim(), key, StringComparison.Ordinal)) return true;
        }

        return false;
    }

    private static void ApplyMissing(VariableDeclaration entry)
    {
        if (entry == null || string.IsNullOrWhiteSpace(entry.key)) return;

        string key = entry.key.Trim();
        switch (entry.type)
        {
            case StoryVariableType.Heading:
                return;
            case StoryVariableType.VideoClip:
                if (!GameManager.TryGetVideoClip(key, out _))
                {
                    GameManager.SetVideoClip(key, entry.defaultVideoClip);
                }
                return;
            case StoryVariableType.AudioClip:
                if (!GameManager.TryGetAudioClip(key, out _))
                {
                    GameManager.SetAudioClip(key, entry.defaultAudioClip);
                }
                return;
        }

        if (GameManager.TryGetDisplayText(key, out _)) return;

        WriteDeclared(entry, key);
    }

    private static void WriteDeclared(VariableDeclaration entry, string key)
    {
        switch (entry.type)
        {
            case StoryVariableType.Bool:
                GameManager.SetBool(key, entry.defaultBoolValue);
                break;
            case StoryVariableType.Float:
                GameManager.SetFloat(key, entry.defaultFloatValue);
                break;
            case StoryVariableType.String:
                GameManager.SetString(key, entry.defaultStringValue ?? "");
                break;
            case StoryVariableType.VideoClip:
                GameManager.SetVideoClip(key, entry.defaultVideoClip);
                break;
            case StoryVariableType.AudioClip:
                GameManager.SetAudioClip(key, entry.defaultAudioClip);
                break;
            case StoryVariableType.Heading:
                break;
            default:
                GameManager.SetParameter(key, entry.defaultIntValue);
                break;
        }
    }
}
