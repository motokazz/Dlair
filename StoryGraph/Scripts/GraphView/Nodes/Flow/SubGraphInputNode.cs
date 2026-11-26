using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Video;

/// <summary>
/// SubGraph Input ノードに並ぶ入力1件。参照先の変数宣言に対応する。
/// </summary>
[Serializable]
public class SubGraphInputValue
{
    public string key = "";

    public StoryVariableType type = StoryVariableType.Int;

    public int intValue;
    public bool boolValue;
    public float floatValue;
    public string stringValue = "";
    public VideoClip videoClip;
    public AudioClip audioClip;
}

/// <summary>
/// 別の StoryGraph に入る前に、参照先の変数宣言へ入力値を書く。
/// 入り方と戻り方は SubGraph ノードと同じ。SubGraph ノード自体は変えない。
/// </summary>
public class SubGraphInputNode : BaseNode
{
    public StoryGraph subGraph;

    public List<SubGraphInputValue> inputs = new List<SubGraphInputValue>();

    /// <summary>
    /// 入力値を変数へ書いてから、サブグラフに入る。
    /// </summary>
    public override void Execute(StoryPlayer player)
    {
        ApplyInputs();
        if (player != null)
        {
            player.EnterSubGraph(subGraph, this);
        }
    }

    /// <summary>
    /// ノードタイトル用。参照先のグラフ名を返す。
    /// </summary>
    public string GetDisplayTitle()
    {
        if (subGraph != null && !string.IsNullOrEmpty(subGraph.name))
        {
            return subGraph.name;
        }

        return "SubGraphInput";
    }

    /// <summary>
    /// いまの変数宣言に入力一覧を合わせる。同じ名前の設定値は残す。
    /// 一覧が変わったときだけ true。
    /// </summary>
    public bool SyncInputs()
    {
        List<SubGraphInputValue> resolved = ResolveInputs();
        if (SameInputs(inputs, resolved)) return false;

        inputs = resolved;
        return true;
    }

    /// <summary>
    /// 解決した入力値を変数へ書く。戻るときに戻さない。
    /// 参照先のローカル宣言は、そのグラフのローカル変数へ書く。グローバル宣言はグローバル変数へ書く。
    /// </summary>
    public void ApplyInputs()
    {
        List<SubGraphInputValue> resolved = ResolveInputs();
        StoryLocalVariables.BeginGlobalWrite();
        try
        {
            for (int i = 0; i < resolved.Count; i++)
            {
                SubGraphInputValue entry = resolved[i];
                if (entry == null || entry.type == StoryVariableType.Heading) continue;

                StoryLocalVariables.TrySetDeclared(
                    subGraph,
                    entry.key,
                    entry.type,
                    entry.intValue,
                    entry.boolValue,
                    entry.floatValue,
                    entry.stringValue,
                    entry.videoClip,
                    entry.audioClip);

                if (HasGlobalDeclaration(subGraph, entry.key, entry.type))
                {
                    WriteInput(entry);
                }
            }
        }
        finally
        {
            StoryLocalVariables.EndGlobalWrite();
        }
    }

    /// <summary>
    /// 参照先グラフの変数宣言に、このノードの設定値を重ねる。
    /// 宣言が無いときは空。アセットは書き換えない。
    /// </summary>
    public List<SubGraphInputValue> ResolveInputs()
    {
        List<SubGraphInputValue> resolved = new List<SubGraphInputValue>();
        List<VariableDeclaration> declarations = CollectDeclarations(subGraph);
        for (int i = 0; i < declarations.Count; i++)
        {
            VariableDeclaration declaration = declarations[i];
            string key = declaration.key.Trim();
            SubGraphInputValue saved = FindInput(key);
            if (saved != null && saved.type == declaration.type)
            {
                resolved.Add(CopyInput(saved, key));
            }
            else
            {
                resolved.Add(FromDeclaration(declaration, key));
            }
        }

        return resolved;
    }

    private static List<VariableDeclaration> CollectDeclarations(StoryGraph graph)
    {
        List<VariableDeclaration> declarations = new List<VariableDeclaration>();
        if (graph == null || graph.nodes == null) return declarations;

        HashSet<string> seen = new HashSet<string>();
        for (int i = 0; i < graph.nodes.Count; i++)
        {
            List<VariableDeclaration> variables = DeclarationsOn(graph.nodes[i]);
            if (variables == null) continue;

            for (int v = 0; v < variables.Count; v++)
            {
                VariableDeclaration entry = variables[v];
                if (entry == null || string.IsNullOrWhiteSpace(entry.key)) continue;

                string key = entry.key.Trim();
                if (!seen.Add(key)) continue;
                declarations.Add(entry);
            }
        }

        return declarations;
    }

    private static List<VariableDeclaration> DeclarationsOn(BaseNode node)
    {
        if (node is VariableDeclareNode declareNode) return declareNode.variables;
        if (node is LocalVariableDeclareNode localNode) return localNode.variables;
        return null;
    }

    private static bool HasGlobalDeclaration(StoryGraph graph, string key, StoryVariableType type)
    {
        if (graph == null || graph.nodes == null || string.IsNullOrWhiteSpace(key)) return false;

        string trimmed = key.Trim();
        for (int i = 0; i < graph.nodes.Count; i++)
        {
            if (graph.nodes[i] is not VariableDeclareNode declareNode || declareNode.variables == null) continue;

            for (int v = 0; v < declareNode.variables.Count; v++)
            {
                VariableDeclaration entry = declareNode.variables[v];
                if (entry == null || entry.type == StoryVariableType.Heading || entry.type != type) continue;
                if (string.IsNullOrWhiteSpace(entry.key)) continue;
                if (string.Equals(entry.key.Trim(), trimmed, StringComparison.Ordinal)) return true;
            }
        }

        return false;
    }

    private SubGraphInputValue FindInput(string key)
    {
        if (inputs == null) return null;

        for (int i = 0; i < inputs.Count; i++)
        {
            SubGraphInputValue entry = inputs[i];
            if (entry == null || string.IsNullOrWhiteSpace(entry.key)) continue;
            if (string.Equals(entry.key.Trim(), key, StringComparison.Ordinal)) return entry;
        }

        return null;
    }

    private static SubGraphInputValue CopyInput(SubGraphInputValue source, string key)
    {
        return new SubGraphInputValue
        {
            key = key,
            type = source.type,
            intValue = source.intValue,
            boolValue = source.boolValue,
            floatValue = source.floatValue,
            stringValue = source.stringValue ?? "",
            videoClip = source.videoClip,
            audioClip = source.audioClip
        };
    }

    private static SubGraphInputValue FromDeclaration(VariableDeclaration declaration, string key)
    {
        return new SubGraphInputValue
        {
            key = key,
            type = declaration.type,
            intValue = declaration.defaultIntValue,
            boolValue = declaration.defaultBoolValue,
            floatValue = declaration.defaultFloatValue,
            stringValue = declaration.defaultStringValue ?? "",
            videoClip = declaration.defaultVideoClip,
            audioClip = declaration.defaultAudioClip
        };
    }

    private static void WriteInput(SubGraphInputValue entry)
    {
        if (entry == null || string.IsNullOrWhiteSpace(entry.key)) return;
        if (entry.type == StoryVariableType.Heading) return;

        string key = entry.key.Trim();
        switch (entry.type)
        {
            case StoryVariableType.Bool:
                GameManager.SetBool(key, entry.boolValue);
                break;
            case StoryVariableType.Float:
                GameManager.SetFloat(key, entry.floatValue);
                break;
            case StoryVariableType.String:
                GameManager.SetString(key, entry.stringValue ?? "");
                break;
            case StoryVariableType.VideoClip:
                GameManager.SetVideoClip(key, entry.videoClip);
                break;
            case StoryVariableType.AudioClip:
                GameManager.SetAudioClip(key, entry.audioClip);
                break;
            default:
                GameManager.SetParameter(key, entry.intValue);
                break;
        }
    }

    private static bool SameInputs(List<SubGraphInputValue> current, List<SubGraphInputValue> resolved)
    {
        if (current == null || resolved == null) return false;
        if (current.Count != resolved.Count) return false;

        for (int i = 0; i < current.Count; i++)
        {
            if (!SameInput(current[i], resolved[i])) return false;
        }

        return true;
    }

    private static bool SameInput(SubGraphInputValue current, SubGraphInputValue resolved)
    {
        if (current == null || resolved == null) return false;
        if (!string.Equals(current.key, resolved.key, StringComparison.Ordinal)) return false;
        if (current.type != resolved.type) return false;

        switch (current.type)
        {
            case StoryVariableType.Bool:
                return current.boolValue == resolved.boolValue;
            case StoryVariableType.Float:
                return current.floatValue == resolved.floatValue;
            case StoryVariableType.String:
                return string.Equals(current.stringValue ?? "", resolved.stringValue ?? "", StringComparison.Ordinal);
            case StoryVariableType.VideoClip:
                return current.videoClip == resolved.videoClip;
            case StoryVariableType.AudioClip:
                return current.audioClip == resolved.audioClip;
            case StoryVariableType.Heading:
                return true;
            default:
                return current.intValue == resolved.intValue;
        }
    }
}
