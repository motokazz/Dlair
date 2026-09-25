using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class StoryGraphAssetReplaceUtility
{
    public enum MatchMode
    {
        ExactOnly,
        ExactOrPartial
    }

    public class Candidate
    {
        public UnityEngine.Object asset;
        public string path;
        public string name;
        public Type type;
    }

    public class Replacement
    {
        public StoryGraph graph;
        public BaseNode node;
        public string propertyPath;
        public string fieldLabel;
        public UnityEngine.Object current;
        public UnityEngine.Object next;
        public string currentPath;
        public string nextPath;
        public string matchKind;
        public int score;
        public bool enabled = true;
    }

    public class Unmatched
    {
        public StoryGraph graph;
        public BaseNode node;
        public string fieldLabel;
        public UnityEngine.Object current;
        public string currentPath;
    }

    public class Preview
    {
        public readonly List<Replacement> replacements = new List<Replacement>();
        public readonly List<Unmatched> unmatched = new List<Unmatched>();
        public readonly List<string> warnings = new List<string>();
        public int candidateCount;
    }

    public class ApplyResult
    {
        public int changed;
        public readonly List<string> warnings = new List<string>();
    }

    public static Preview BuildPreview(
        StoryGraph source,
        string folderPath,
        bool includeRelatedGraphs,
        bool includeSubfolders,
        MatchMode matchMode)
    {
        Preview preview = new Preview();
        if (source == null)
        {
            preview.warnings.Add("対象 Graph が指定されていません。");
            return preview;
        }

        folderPath = NormalizeFolderPath(folderPath);
        if (string.IsNullOrEmpty(folderPath) || !AssetDatabase.IsValidFolder(folderPath))
        {
            preview.warnings.Add("差し替え先フォルダが Assets 以下にありません。");
            return preview;
        }

        List<StoryGraph> graphs = includeRelatedGraphs
            ? StoryGraphCloneUtility.CollectRelatedGraphs(source)
            : new List<StoryGraph> { source };

        List<Candidate> candidates = CollectFolderAssets(folderPath, includeSubfolders);
        preview.candidateCount = candidates.Count;
        if (candidates.Count == 0)
        {
            preview.warnings.Add("指定フォルダに差し替え候補のアセットがありません。");
        }

        HashSet<string> seen = new HashSet<string>();
        for (int i = 0; i < graphs.Count; i++)
        {
            CollectFromGraph(graphs[i], folderPath, candidates, matchMode, preview, seen);
        }

        return preview;
    }

    public static ApplyResult Apply(IList<Replacement> replacements)
    {
        ApplyResult result = new ApplyResult();
        if (replacements == null || replacements.Count == 0) return result;

        Undo.SetCurrentGroupName("StoryGraph アセット差し替え");
        int undoGroup = Undo.GetCurrentGroup();

        for (int i = 0; i < replacements.Count; i++)
        {
            Replacement item = replacements[i];
            if (item == null || !item.enabled || item.node == null || item.next == null) continue;
            if (item.current == item.next) continue;

            Undo.RecordObject(item.node, "アセット差し替え");
            using (SerializedObject serialized = new SerializedObject(item.node))
            {
                SerializedProperty property = serialized.FindProperty(item.propertyPath);
                if (property == null || property.propertyType != SerializedPropertyType.ObjectReference)
                {
                    result.warnings.Add($"プロパティを更新できません: {DescribeNode(item.node)} / {item.fieldLabel}");
                    continue;
                }

                property.objectReferenceValue = item.next;
                serialized.ApplyModifiedProperties();
            }

            EditorUtility.SetDirty(item.node);
            if (item.graph != null)
            {
                EditorUtility.SetDirty(item.graph);
            }

            result.changed++;
        }

        Undo.CollapseUndoOperations(undoGroup);
        AssetDatabase.SaveAssets();
        ReloadOpenGraphWindows(replacements);
        return result;
    }

    private static void ReloadOpenGraphWindows(IList<Replacement> replacements)
    {
        HashSet<StoryGraph> affected = new HashSet<StoryGraph>();
        for (int i = 0; i < replacements.Count; i++)
        {
            Replacement item = replacements[i];
            if (item == null || !item.enabled || item.graph == null) continue;
            affected.Add(item.graph);
        }

        if (affected.Count == 0) return;

        StoryGraphWindow[] windows = Resources.FindObjectsOfTypeAll<StoryGraphWindow>();
        for (int i = 0; i < windows.Length; i++)
        {
            StoryGraphWindow window = windows[i];
            if (window == null || window.currentGraph == null) continue;
            if (!affected.Contains(window.currentGraph)) continue;
            window.ReloadFromAsset();
        }
    }

    private static void CollectFromGraph(
        StoryGraph graph,
        string folderPath,
        List<Candidate> candidates,
        MatchMode matchMode,
        Preview preview,
        HashSet<string> seen)
    {
        if (graph?.nodes == null) return;

        for (int n = 0; n < graph.nodes.Count; n++)
        {
            BaseNode node = graph.nodes[n];
            if (node == null) continue;

            using (SerializedObject serialized = new SerializedObject(node))
            {
                SerializedProperty iterator = serialized.GetIterator();
                while (iterator.Next(true))
                {
                    if (iterator.propertyType != SerializedPropertyType.ObjectReference) continue;
                    UnityEngine.Object current = iterator.objectReferenceValue;
                    if (!IsReplaceableAsset(current)) continue;

                    string key = graph.name + "|" + node.guid + "|" + iterator.propertyPath;
                    if (!seen.Add(key)) continue;

                    string currentPath = AssetDatabase.GetAssetPath(current);
                    if (IsPathInsideFolder(currentPath, folderPath)) continue;

                    Candidate match = FindBestMatch(current, currentPath, candidates, matchMode, out string matchKind, out int score);
                    if (match == null || match.asset == current)
                    {
                        preview.unmatched.Add(new Unmatched
                        {
                            graph = graph,
                            node = node,
                            fieldLabel = PrettyFieldName(iterator),
                            current = current,
                            currentPath = currentPath
                        });
                        continue;
                    }

                    preview.replacements.Add(new Replacement
                    {
                        graph = graph,
                        node = node,
                        propertyPath = iterator.propertyPath,
                        fieldLabel = PrettyFieldName(iterator),
                        current = current,
                        next = match.asset,
                        currentPath = currentPath,
                        nextPath = match.path,
                        matchKind = matchKind,
                        score = score,
                        enabled = true
                    });
                }
            }
        }
    }

    private static Candidate FindBestMatch(
        UnityEngine.Object current,
        string currentPath,
        List<Candidate> candidates,
        MatchMode matchMode,
        out string matchKind,
        out int score)
    {
        matchKind = "";
        score = 0;
        Candidate best = null;
        int bestScore = 0;
        int ties = 0;
        string sourceName = GetAssetName(current, currentPath);
        Type sourceType = current.GetType();

        for (int i = 0; i < candidates.Count; i++)
        {
            Candidate candidate = candidates[i];
            if (candidate?.asset == null) continue;
            if (candidate.asset == current) continue;
            if (!IsTypeCompatible(sourceType, candidate.asset)) continue;

            int currentScore = ScoreMatch(sourceName, candidate.name, matchMode, out string kind);
            if (currentScore <= 0) continue;

            if (currentScore > bestScore)
            {
                best = candidate;
                bestScore = currentScore;
                matchKind = kind;
                ties = 1;
            }
            else if (currentScore == bestScore)
            {
                ties++;
            }
        }

        score = bestScore;
        if (best != null && ties > 1)
        {
            matchKind += "（同点あり）";
        }

        return best;
    }

    private static int ScoreMatch(string sourceName, string candidateName, MatchMode matchMode, out string kind)
    {
        kind = "";
        if (string.IsNullOrEmpty(sourceName) || string.IsNullOrEmpty(candidateName)) return 0;

        if (string.Equals(sourceName, candidateName, StringComparison.OrdinalIgnoreCase))
        {
            kind = "完全一致";
            return 100000;
        }

        if (matchMode == MatchMode.ExactOnly) return 0;

        int contains = ScoreContains(sourceName, candidateName);
        if (contains > 0)
        {
            kind = "部分一致";
            return contains;
        }

        int prefix = LongestCommonPrefixLength(sourceName, candidateName);
        int minLen = Mathf.Min(sourceName.Length, candidateName.Length);
        int required = Mathf.Max(4, minLen / 2);
        if (prefix >= required)
        {
            kind = "部分一致";
            return 1000 + prefix * 10 - Mathf.Abs(sourceName.Length - candidateName.Length);
        }

        return 0;
    }

    private static int ScoreContains(string sourceName, string candidateName)
    {
        if (sourceName.Length >= 2 && candidateName.IndexOf(sourceName, StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return 50000 + sourceName.Length * 10 - (candidateName.Length - sourceName.Length);
        }

        if (candidateName.Length >= 3 && sourceName.IndexOf(candidateName, StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return 40000 + candidateName.Length * 10 - (sourceName.Length - candidateName.Length);
        }

        return 0;
    }

    private static int LongestCommonPrefixLength(string a, string b)
    {
        int max = Mathf.Min(a.Length, b.Length);
        int i = 0;
        while (i < max && char.ToLowerInvariant(a[i]) == char.ToLowerInvariant(b[i]))
        {
            i++;
        }

        return i;
    }

    private static bool IsTypeCompatible(Type sourceType, UnityEngine.Object candidate)
    {
        if (sourceType == null || candidate == null) return false;
        if (sourceType.IsInstanceOfType(candidate)) return true;

        if (typeof(Texture).IsAssignableFrom(sourceType) && candidate is Texture)
        {
            return true;
        }

        if (sourceType == typeof(Sprite) && candidate is Texture2D)
        {
            return false;
        }

        return false;
    }

    private static bool IsReplaceableAsset(UnityEngine.Object value)
    {
        if (value == null) return false;
        if (!IsSupportedAssetType(value)) return false;
        if (!EditorUtility.IsPersistent(value)) return false;

        string path = AssetDatabase.GetAssetPath(value);
        return !string.IsNullOrEmpty(path);
    }

    private static bool IsSupportedAssetType(UnityEngine.Object value)
    {
        if (value == null) return false;
        if (value is StoryGraph || value is BaseNode || value is MonoScript || value is DefaultAsset)
        {
            return false;
        }

        return value is GameObject
            || value is Texture
            || value is Sprite
            || value is AudioClip
            || value is Material
            || value is AnimationClip
            || value is RuntimeAnimatorController
            || value is Font
            || value is UnityEngine.Video.VideoClip
            || value is TMPro.TMP_FontAsset;
    }

    private static List<Candidate> CollectFolderAssets(string folderPath, bool includeSubfolders)
    {
        List<Candidate> list = new List<Candidate>();
        string[] guids = AssetDatabase.FindAssets("", new[] { folderPath });
        HashSet<string> seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        HashSet<EntityId> seenIds = new HashSet<EntityId>();

        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            if (string.IsNullOrEmpty(path) || !seenPaths.Add(path)) continue;
            if (!includeSubfolders && !IsDirectChild(folderPath, path)) continue;
            if (AssetDatabase.IsValidFolder(path)) continue;

            UnityEngine.Object main = AssetDatabase.LoadMainAssetAtPath(path);
            TryAddCandidate(list, main, path, seenIds);

            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
            if (assets == null) continue;
            for (int a = 0; a < assets.Length; a++)
            {
                UnityEngine.Object asset = assets[a];
                if (asset is Sprite || asset is Texture2D || asset is AudioClip || asset is UnityEngine.Video.VideoClip)
                {
                    TryAddCandidate(list, asset, path, seenIds);
                }
            }
        }

        return list;
    }

    private static void TryAddCandidate(List<Candidate> list, UnityEngine.Object asset, string path, HashSet<EntityId> seenIds)
    {
        if (!IsReplaceableAsset(asset)) return;
        if (!seenIds.Add(asset.GetEntityId())) return;
        list.Add(new Candidate
        {
            asset = asset,
            path = path,
            name = GetAssetName(asset, path),
            type = asset.GetType()
        });
    }

    private static bool IsPathInsideFolder(string assetPath, string folderPath)
    {
        if (string.IsNullOrEmpty(assetPath) || string.IsNullOrEmpty(folderPath)) return false;
        string folder = folderPath.TrimEnd('/') + "/";
        return assetPath.Replace('\\', '/').StartsWith(folder, StringComparison.OrdinalIgnoreCase)
            || string.Equals(Path.GetDirectoryName(assetPath)?.Replace('\\', '/'), folderPath.TrimEnd('/'), StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsDirectChild(string folderPath, string assetPath)
    {
        string dir = Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
        return string.Equals(dir, folderPath.TrimEnd('/'), StringComparison.OrdinalIgnoreCase);
    }

    private static string GetAssetName(UnityEngine.Object asset, string path)
    {
        if (asset != null && AssetDatabase.IsSubAsset(asset) && !string.IsNullOrEmpty(asset.name))
        {
            return asset.name;
        }

        if (!string.IsNullOrEmpty(path))
        {
            string file = Path.GetFileNameWithoutExtension(path);
            if (!string.IsNullOrEmpty(file)) return file;
        }

        return asset != null ? asset.name : "";
    }

    private static string PrettyFieldName(SerializedProperty property)
    {
        if (property == null) return "";
        if (!string.IsNullOrEmpty(property.displayName)) return property.displayName;
        return property.name;
    }

    public static string DescribeNode(BaseNode node)
    {
        if (node == null) return "(null)";
        string typeName = node.GetType().Name.Replace("Node", "");
        return typeName;
    }

    public static string NormalizeFolderPath(string folder)
    {
        if (string.IsNullOrWhiteSpace(folder)) return null;
        string normalized = folder.Replace('\\', '/').TrimEnd('/');
        if (normalized == "Assets" || normalized.StartsWith("Assets/", StringComparison.Ordinal))
        {
            return normalized;
        }

        string dataPath = Application.dataPath.Replace('\\', '/');
        if (normalized.StartsWith(dataPath, StringComparison.OrdinalIgnoreCase))
        {
            return "Assets" + normalized.Substring(dataPath.Length);
        }

        return null;
    }
}
