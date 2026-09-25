using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class StoryGraphNodeNaming
{
    static StoryGraphNodeNaming()
    {
        EditorApplication.delayCall += RepairOpenAssets;
    }

    private static void RepairOpenAssets()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;

        string[] guids = AssetDatabase.FindAssets("t:StoryGraph");
        int changed = 0;
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            changed += TryApplyAtPath(path);
        }

        if (changed == 0) return;

        AssetDatabase.SaveAssets();
        Debug.Log($"【StoryGraph】Project ビュー用のノード名を {changed} 件付けました。");
    }

    private const int MaxNameLength = 64;

    public static bool TryApply(BaseNode node)
    {
        if (node == null) return false;

        string next = Build(node);
        if (string.Equals(node.name, next, StringComparison.Ordinal)) return false;

        node.name = next;
        EditorUtility.SetDirty(node);
        return true;
    }

    public static int TryApplyAtPath(string assetPath)
    {
        if (string.IsNullOrEmpty(assetPath)) return 0;

        UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
        if (assets == null) return 0;

        int changed = 0;
        for (int i = 0; i < assets.Length; i++)
        {
            if (assets[i] is BaseNode node && TryApply(node))
            {
                changed++;
            }
        }

        return changed;
    }

    public static string Build(BaseNode node)
    {
        if (node == null) return "Node";

        string raw;
        if (node is LabelNode labelNode)
        {
            raw = labelNode.GetDisplayTitle();
        }
        else if (node is GotoNode gotoNode)
        {
            raw = gotoNode.GetDisplayTitle();
        }
        else if (node is VariableOperationNode variableNode)
        {
            raw = variableNode.GetDisplayTitle();
        }
        else if (node is BoolOperationNode boolNode)
        {
            raw = boolNode.GetDisplayTitle();
        }
        else if (node is ConditionNode conditionNode)
        {
            raw = conditionNode.GetDisplayTitle();
        }
        else if (node is ConditionBranchNode conditionBranchNode)
        {
            raw = conditionBranchNode.GetDisplayTitle();
        }
        else if (node is RandomBranchNode randomBranchNode)
        {
            raw = randomBranchNode.GetDisplayTitle();
        }
        else if (node is PlaybackSpeedNode speedNode)
        {
            raw = speedNode.GetDisplayTitle();
        }
        else if (node is TextNode textNode)
        {
            raw = textNode.GetDisplayTitle();
        }
        else if (node is SpawnPrefabNode spawnNode)
        {
            raw = spawnNode.GetDisplayTitle();
        }
        else if (node is SubGraphNode subGraphNode)
        {
            raw = subGraphNode.subGraph != null && !string.IsNullOrEmpty(subGraphNode.subGraph.name)
                ? subGraphNode.subGraph.name
                : "SubGraph";
        }
        else if (node is PlayClipNode playClipNode)
        {
            raw = FirstAssetName(playClipNode.clip, playClipNode.audioClip, "PlayClip");
        }
        else if (node is PlayImageNode playImageNode)
        {
            raw = FirstAssetName(playImageNode.image, playImageNode.audioClip, "PlayImage");
        }
        else if (node is ChoiceNode choiceNode)
        {
            raw = ChoiceLabel(choiceNode);
        }
        else
        {
            raw = ShortTypeName(node);
        }

        return Sanitize(raw, ShortTypeName(node));
    }

    private static string ChoiceLabel(ChoiceNode node)
    {
        if (node.choices == null || node.choices.Count == 0) return "Choice";

        List<string> parts = new List<string>(node.choices.Count);
        for (int i = 0; i < node.choices.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(node.choices[i])) continue;
            parts.Add(node.choices[i].Trim());
        }

        return parts.Count == 0 ? "Choice" : "Choice  " + string.Join(" / ", parts);
    }

    private static string FirstAssetName(UnityEngine.Object primary, UnityEngine.Object secondary, string fallback)
    {
        if (primary != null && !string.IsNullOrEmpty(primary.name)) return primary.name;
        if (secondary != null && !string.IsNullOrEmpty(secondary.name)) return secondary.name;
        return fallback;
    }

    private static string ShortTypeName(BaseNode node)
    {
        string typeName = node.GetType().Name;
        return typeName.EndsWith("Node", StringComparison.Ordinal)
            ? typeName.Substring(0, typeName.Length - 4)
            : typeName;
    }

    private static string Sanitize(string raw, string fallback)
    {
        if (string.IsNullOrWhiteSpace(raw)) raw = fallback;

        StringBuilder builder = new StringBuilder(raw.Length);
        bool pendingSpace = false;
        for (int i = 0; i < raw.Length; i++)
        {
            char c = raw[i];
            if (c == '/' || c == '\\')
            {
                c = '／';
            }
            else if (char.IsControl(c) || char.IsWhiteSpace(c))
            {
                pendingSpace = builder.Length > 0;
                continue;
            }

            if (pendingSpace)
            {
                builder.Append(' ');
                pendingSpace = false;
            }

            builder.Append(c);
            if (builder.Length >= MaxNameLength) break;
        }

        string sanitized = builder.ToString().Trim();
        if (string.IsNullOrEmpty(sanitized)) sanitized = string.IsNullOrEmpty(fallback) ? "Node" : fallback;
        return sanitized;
    }
}
