using System;
using System.Collections.Generic;
using UnityEngine;

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
}

/// <summary>
/// 複数の変数をまとめて宣言する。
/// StoryVariableDatabase の変数と合わせて、変数ノードの選択肢になる。
/// 再生時は、まだ値が無い変数にだけ初期値を入れる。セーブ済みの値は上書きしない。
/// </summary>
public class VariableDeclareNode : BaseNode
{
    public List<VariableDeclaration> variables = new List<VariableDeclaration>
    {
        new VariableDeclaration()
    };

    /// <summary>
    /// このノードの未設定変数へ初期値を入れ、Next へ進む。
    /// </summary>
    public override void Execute(StoryPlayer player)
    {
        ApplyMissingDefaults();
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

        for (int i = 0; i < variables.Count; i++)
        {
            ApplyMissing(variables[i]);
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
        return "変数をまとめて宣言します。ここでの名前と Variable Database の変数が、他の変数ノードの一覧になります。再生時は、まだ値が無い変数にだけ初期値を入れます。";
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
            if (node is SubGraphNode subGraphNode)
            {
                ApplyMissingInGraph(subGraphNode.subGraph, visited);
            }
            else if (node is GotoNode gotoNode)
            {
                ApplyMissingInGraph(gotoNode.targetGraph, visited);
            }
        }
    }

    private static void ApplyMissing(VariableDeclaration entry)
    {
        if (entry == null || string.IsNullOrWhiteSpace(entry.key)) return;

        string key = entry.key.Trim();
        if (GameManager.TryGetDisplayText(key, out _)) return;

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
            default:
                GameManager.SetParameter(key, entry.defaultIntValue);
                break;
        }
    }
}
