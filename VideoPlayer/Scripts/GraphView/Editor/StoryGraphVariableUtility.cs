using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Project ウィンドウで選択している StoryGraph だけを対象に、ノードへ保存された変数名を変える。
/// 選択したグラフが参照している別の StoryGraph は対象に含めない。
/// </summary>
public static class StoryGraphVariableUtility
{
    public const string Summary =
        "処理対象は、Project ウィンドウで選択している StoryGraph です。\n" +
        "ノードに保存された変数名を、指定した名前へ変えます。対象は変数宣言の名前と、条件・演算・Bool・変数テキスト・条件分岐が参照する変数名です。\n" +
        "選択したグラフが参照している別の StoryGraph、StoryVariableDatabase、分岐ラベル、本文は対象外です。\n" +
        "複製ツールは変数名をそのまま写します。複製したグラフの変数名は、このツールで別名にできます。\n" +
        "実行前にプレビューで確認できます。";

    public class RenameRule
    {
        public string from;
        public string to;
    }

    public class NameHit
    {
        public string name;
        public int declareCount;
        public int referenceCount;
    }

    public class Occurrence
    {
        public StoryGraph graph;
        public BaseNode node;
        public string propertyPath;
        public string fieldLabel;
        public string currentName;
        public string nextName;
    }

    public class Preview
    {
        public readonly List<Occurrence> changes = new List<Occurrence>();
        public readonly List<string> warnings = new List<string>();
    }

    public class ApplyResult
    {
        public int changed;
        public readonly List<string> warnings = new List<string>();
    }

    private class Site
    {
        public StoryGraph graph;
        public BaseNode node;
        public string propertyPath;
        public bool declaration;
        public string name;
    }

    /// <summary>
    /// Project ウィンドウで選択中の StoryGraph を返す。フォルダ配下やリンク先は展開しない。
    /// </summary>
    public static List<StoryGraph> CollectSelectedGraphs()
    {
        List<StoryGraph> graphs = new List<StoryGraph>();
        StoryGraph[] selected = Selection.GetFiltered<StoryGraph>(SelectionMode.Assets);
        if (selected == null || selected.Length == 0) return graphs;

        HashSet<StoryGraph> seen = new HashSet<StoryGraph>();
        for (int i = 0; i < selected.Length; i++)
        {
            StoryGraph graph = selected[i];
            if (graph == null || !seen.Add(graph)) continue;
            graphs.Add(graph);
        }

        return graphs;
    }

    /// <summary>
    /// 渡された StoryGraph に出てくる変数名を、宣言数と参照数つきで返す。
    /// </summary>
    public static List<NameHit> CollectNames(IList<StoryGraph> graphs)
    {
        List<Site> sites = CollectSites(graphs);
        Dictionary<string, NameHit> map = new Dictionary<string, NameHit>(StringComparer.Ordinal);
        for (int i = 0; i < sites.Count; i++)
        {
            Site site = sites[i];
            if (!map.TryGetValue(site.name, out NameHit hit))
            {
                hit = new NameHit { name = site.name };
                map.Add(site.name, hit);
            }

            if (site.declaration) hit.declareCount++;
            else hit.referenceCount++;
        }

        List<NameHit> result = new List<NameHit>(map.Values);
        result.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        return result;
    }

    public static Preview BuildPreview(IList<StoryGraph> graphs, IList<RenameRule> rules)
    {
        Preview preview = new Preview();
        List<StoryGraph> targets = DistinctGraphs(graphs);
        if (targets.Count == 0)
        {
            preview.warnings.Add("Project ウィンドウで StoryGraph を選択してください。");
            return preview;
        }

        List<Site> sites = CollectSites(targets);
        if (sites.Count == 0)
        {
            preview.warnings.Add("選択した StoryGraph に変数名がありません。");
            return preview;
        }

        Dictionary<string, string> rename = BuildRenameMap(rules, preview.warnings);
        if (rename.Count == 0) return preview;

        HashSet<string> present = new HashSet<string>(StringComparer.Ordinal);
        HashSet<string> declared = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < sites.Count; i++)
        {
            present.Add(sites[i].name);
            if (sites[i].declaration) declared.Add(sites[i].name);
        }

        HashSet<string> staying = new HashSet<string>(present, StringComparer.Ordinal);
        foreach (string from in rename.Keys)
        {
            staying.Remove(from);
        }

        Dictionary<string, int> destinationCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        List<string> activeSources = new List<string>();
        foreach (KeyValuePair<string, string> pair in rename)
        {
            if (!present.Contains(pair.Key))
            {
                preview.warnings.Add($"変数名が見つかりません: {pair.Key}");
                continue;
            }

            activeSources.Add(pair.Key);
            if (!destinationCounts.ContainsKey(pair.Value)) destinationCounts[pair.Value] = 0;
            destinationCounts[pair.Value]++;

            if (staying.Contains(pair.Value))
            {
                preview.warnings.Add($"変更後の名前が、変更しない変数と重なります: {pair.Key} → {pair.Value}");
            }

            if (!declared.Contains(pair.Key))
            {
                preview.warnings.Add($"「{pair.Key}」は選択した StoryGraph 内に宣言がありません。StoryVariableDatabase は変更しません。");
            }
        }

        foreach (KeyValuePair<string, int> pair in destinationCounts)
        {
            if (pair.Value > 1)
            {
                preview.warnings.Add($"変更後の名前が複数の変数で同じです: {pair.Key}");
            }
        }

        HashSet<string> active = new HashSet<string>(activeSources, StringComparer.Ordinal);
        for (int i = 0; i < sites.Count; i++)
        {
            Site site = sites[i];
            if (!active.Contains(site.name)) continue;
            if (!rename.TryGetValue(site.name, out string next)) continue;

            preview.changes.Add(new Occurrence
            {
                graph = site.graph,
                node = site.node,
                propertyPath = site.propertyPath,
                fieldLabel = site.declaration ? "宣言" : "参照",
                currentName = site.name,
                nextName = next
            });
        }

        return preview;
    }

    public static ApplyResult Apply(IList<Occurrence> changes)
    {
        ApplyResult result = new ApplyResult();
        if (changes == null || changes.Count == 0) return result;

        Undo.SetCurrentGroupName("StoryGraph 変数名の変更");
        int undoGroup = Undo.GetCurrentGroup();
        HashSet<BaseNode> recorded = new HashSet<BaseNode>();
        HashSet<BaseNode> touched = new HashSet<BaseNode>();

        for (int i = 0; i < changes.Count; i++)
        {
            Occurrence item = changes[i];
            if (item == null || item.node == null || string.IsNullOrEmpty(item.propertyPath)) continue;
            if (string.IsNullOrWhiteSpace(item.nextName))
            {
                result.warnings.Add($"変更後の名前が空です: {DescribeOccurrence(item)}");
                continue;
            }

            if (recorded.Add(item.node))
            {
                Undo.RecordObject(item.node, "変数名の変更");
            }

            using (SerializedObject serialized = new SerializedObject(item.node))
            {
                SerializedProperty property = serialized.FindProperty(item.propertyPath);
                if (property == null || property.propertyType != SerializedPropertyType.String)
                {
                    result.warnings.Add($"プロパティを更新できません: {DescribeOccurrence(item)}");
                    continue;
                }

                if (!NamesEqual(property.stringValue, item.currentName))
                {
                    result.warnings.Add($"変数名が変わっていたためスキップしました: {DescribeOccurrence(item)}");
                    continue;
                }

                property.stringValue = item.nextName.Trim();
                serialized.ApplyModifiedProperties();
            }

            EditorUtility.SetDirty(item.node);
            if (item.graph != null)
            {
                EditorUtility.SetDirty(item.graph);
            }

            touched.Add(item.node);
            result.changed++;
        }

        foreach (BaseNode node in touched)
        {
            StoryGraphNodeNaming.TryApply(node);
        }

        Undo.CollapseUndoOperations(undoGroup);
        if (result.changed > 0)
        {
            AssetDatabase.SaveAssets();
        }

        return result;
    }

    public static string DescribeOccurrence(Occurrence item)
    {
        if (item == null) return "(null)";
        string graphName = item.graph != null ? item.graph.name : "?";
        string typeName = "?";
        if (item.node != null)
        {
            typeName = item.node.GetType().Name;
            if (typeName.EndsWith("Node", StringComparison.Ordinal))
            {
                typeName = typeName.Substring(0, typeName.Length - 4);
            }
        }

        return $"{graphName} / {typeName} / {item.fieldLabel}";
    }

    private static Dictionary<string, string> BuildRenameMap(IList<RenameRule> rules, List<string> warnings)
    {
        Dictionary<string, string> rename = new Dictionary<string, string>(StringComparer.Ordinal);
        if (rules == null) return rename;

        for (int i = 0; i < rules.Count; i++)
        {
            RenameRule rule = rules[i];
            if (rule == null) continue;

            string from = Normalize(rule.from);
            string to = Normalize(rule.to);
            if (string.IsNullOrEmpty(from)) continue;
            if (string.IsNullOrEmpty(to))
            {
                warnings.Add($"変更後の名前が空です: {from}");
                continue;
            }

            if (NamesEqual(from, to)) continue;
            if (rename.ContainsKey(from))
            {
                warnings.Add($"変数名の指定が重複しています: {from}");
            }

            rename[from] = to;
        }

        return rename;
    }

    private static List<Site> CollectSites(IList<StoryGraph> graphs)
    {
        List<Site> sites = new List<Site>();
        List<StoryGraph> targets = DistinctGraphs(graphs);
        for (int i = 0; i < targets.Count; i++)
        {
            CollectFromGraph(targets[i], sites);
        }

        return sites;
    }

    private static void CollectFromGraph(StoryGraph graph, List<Site> sites)
    {
        if (graph?.nodes == null) return;

        for (int i = 0; i < graph.nodes.Count; i++)
        {
            BaseNode node = graph.nodes[i];
            if (node == null) continue;
            CollectFromNode(graph, node, sites);
        }
    }

    private static void CollectFromNode(StoryGraph graph, BaseNode node, List<Site> sites)
    {
        using (SerializedObject serialized = new SerializedObject(node))
        {
            SerializedProperty variableName = serialized.FindProperty("variableName");
            AddSite(sites, graph, node, variableName, false);

            if (node is not VariableDeclareNode) return;

            SerializedProperty variables = serialized.FindProperty("variables");
            if (variables == null || !variables.isArray) return;

            for (int i = 0; i < variables.arraySize; i++)
            {
                SerializedProperty element = variables.GetArrayElementAtIndex(i);
                if (element == null) continue;
                AddSite(sites, graph, node, element.FindPropertyRelative("key"), true);
            }
        }
    }

    private static void AddSite(
        List<Site> sites,
        StoryGraph graph,
        BaseNode node,
        SerializedProperty property,
        bool declaration)
    {
        if (property == null || property.propertyType != SerializedPropertyType.String) return;
        if (string.IsNullOrWhiteSpace(property.stringValue)) return;

        sites.Add(new Site
        {
            graph = graph,
            node = node,
            propertyPath = property.propertyPath,
            declaration = declaration,
            name = property.stringValue.Trim()
        });
    }

    private static List<StoryGraph> DistinctGraphs(IList<StoryGraph> graphs)
    {
        List<StoryGraph> result = new List<StoryGraph>();
        if (graphs == null) return result;

        HashSet<StoryGraph> seen = new HashSet<StoryGraph>();
        for (int i = 0; i < graphs.Count; i++)
        {
            StoryGraph graph = graphs[i];
            if (graph == null || !seen.Add(graph)) continue;
            result.Add(graph);
        }

        return result;
    }

    private static bool NamesEqual(string a, string b)
    {
        return string.Equals(Normalize(a), Normalize(b), StringComparison.Ordinal);
    }

    private static string Normalize(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "" : value.Trim();
    }
}
