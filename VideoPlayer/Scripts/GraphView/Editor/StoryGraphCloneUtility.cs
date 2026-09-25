using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class StoryGraphCloneUtility
{
    public class Result
    {
        public StoryGraph rootCopy;
        public readonly List<StoryGraph> copies = new List<StoryGraph>();
        public readonly List<string> warnings = new List<string>();
    }

    public static List<StoryGraph> CollectRelatedGraphs(StoryGraph root)
    {
        List<StoryGraph> collected = new List<StoryGraph>();
        CollectRelatedGraphs(root, new HashSet<StoryGraph>(), collected);
        return collected;
    }

    public static Result DuplicateTree(StoryGraph source, string destinationFolder, string nameSuffix)
    {
        Result result = new Result();
        if (source == null)
        {
            throw new ArgumentException("複製元の StoryGraph が指定されていません。");
        }

        destinationFolder = NormalizeFolderPath(destinationFolder);
        if (string.IsNullOrEmpty(destinationFolder) || !AssetDatabase.IsValidFolder(destinationFolder))
        {
            throw new ArgumentException("複製先フォルダが Assets 以下にありません。");
        }

        List<StoryGraph> originals = CollectRelatedGraphs(source);
        if (originals.Count == 0)
        {
            throw new InvalidOperationException("複製対象のグラフが見つかりません。");
        }

        string suffix = nameSuffix ?? "";
        Dictionary<StoryGraph, string> destPaths = new Dictionary<StoryGraph, string>();
        HashSet<string> reserved = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < originals.Count; i++)
        {
            StoryGraph original = originals[i];
            string srcPath = AssetDatabase.GetAssetPath(original);
            if (string.IsNullOrEmpty(srcPath))
            {
                result.warnings.Add($"パスが無いグラフをスキップしました: {original.name}");
                continue;
            }

            string destPath = MakeDestinationPath(destinationFolder, original.name, suffix, reserved);
            destPaths[original] = destPath;
        }

        if (!destPaths.ContainsKey(source))
        {
            throw new InvalidOperationException("複製元グラフの出力パスを作れませんでした。");
        }

        bool previousSuppress = StoryGraphGuidUtility.SuppressImportHandling;
        StoryGraphGuidUtility.SuppressImportHandling = true;
        Dictionary<StoryGraph, StoryGraph> originalToCopy = new Dictionary<StoryGraph, StoryGraph>();
        List<string> createdPaths = new List<string>();

        try
        {
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (KeyValuePair<StoryGraph, string> pair in destPaths)
                {
                    string srcPath = AssetDatabase.GetAssetPath(pair.Key);
                    string destPath = pair.Value;
                    if (!AssetDatabase.CopyAsset(srcPath, destPath))
                    {
                        throw new InvalidOperationException($"コピーに失敗しました: {srcPath} → {destPath}");
                    }

                    createdPaths.Add(destPath);
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            foreach (KeyValuePair<StoryGraph, string> pair in destPaths)
            {
                AssetDatabase.ImportAsset(pair.Value, ImportAssetOptions.ForceSynchronousImport);
                StoryGraph copy = AssetDatabase.LoadAssetAtPath<StoryGraph>(pair.Value);
                if (copy == null)
                {
                    throw new InvalidOperationException($"複製したグラフを読み込めません: {pair.Value}");
                }

                originalToCopy[pair.Key] = copy;
                result.copies.Add(copy);
            }

            Dictionary<string, string> guidMap = AssignNewGuids(originalToCopy.Values, result);
            foreach (StoryGraph copy in originalToCopy.Values)
            {
                StoryGraphGuidUtility.RemapStoredGuids(copy, guidMap);
                RemapGraphObjectReferences(copy, originalToCopy);
                StampAssetIdentity(copy);
                EditorUtility.SetDirty(copy);
            }

            result.rootCopy = originalToCopy[source];
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
        catch
        {
            for (int i = 0; i < createdPaths.Count; i++)
            {
                if (!string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(createdPaths[i])))
                {
                    AssetDatabase.DeleteAsset(createdPaths[i]);
                }
            }

            throw;
        }
        finally
        {
            StoryGraphGuidUtility.SuppressImportHandling = previousSuppress;
        }

        return result;
    }

    private static void CollectRelatedGraphs(StoryGraph current, HashSet<StoryGraph> visited, List<StoryGraph> collected)
    {
        if (current == null || !visited.Add(current)) return;

        collected.Add(current);
        if (current.nodes == null) return;

        for (int i = 0; i < current.nodes.Count; i++)
        {
            foreach (StoryGraph referenced in EnumerateReferencedGraphs(current.nodes[i]))
            {
                CollectRelatedGraphs(referenced, visited, collected);
            }
        }
    }

    private static IEnumerable<StoryGraph> EnumerateReferencedGraphs(BaseNode node)
    {
        if (node == null) yield break;

        if (node is SubGraphNode subGraphNode && subGraphNode.subGraph != null)
        {
            yield return subGraphNode.subGraph;
        }

        if (node is GotoNode gotoNode && gotoNode.targetGraph != null)
        {
            yield return gotoNode.targetGraph;
        }

        using (SerializedObject serialized = new SerializedObject(node))
        {
            SerializedProperty iterator = serialized.GetIterator();
            while (iterator.Next(true))
            {
                if (iterator.propertyType != SerializedPropertyType.ObjectReference) continue;
                if (iterator.objectReferenceValue is not StoryGraph referenced || referenced == null) continue;
                yield return referenced;
            }
        }
    }

    private static Dictionary<string, string> AssignNewGuids(IEnumerable<StoryGraph> copies, Result result)
    {
        Dictionary<string, string> map = new Dictionary<string, string>();
        foreach (StoryGraph copy in copies)
        {
            if (copy?.nodes == null) continue;
            for (int i = 0; i < copy.nodes.Count; i++)
            {
                BaseNode node = copy.nodes[i];
                if (node == null) continue;

                string oldGuid = node.guid;
                string newGuid = Guid.NewGuid().ToString();
                if (!string.IsNullOrEmpty(oldGuid) && !map.ContainsKey(oldGuid))
                {
                    map[oldGuid] = newGuid;
                }
                else if (!string.IsNullOrEmpty(oldGuid))
                {
                    newGuid = map[oldGuid];
                    result?.warnings.Add($"ノード GUID が重複していたため共有しました: {copy.name}");
                }

                node.guid = newGuid;
                EditorUtility.SetDirty(node);
            }
        }

        return map;
    }

    private static void RemapGraphObjectReferences(StoryGraph copy, Dictionary<StoryGraph, StoryGraph> originalToCopy)
    {
        if (copy?.nodes == null) return;

        for (int i = 0; i < copy.nodes.Count; i++)
        {
            BaseNode node = copy.nodes[i];
            if (node == null) continue;

            if (node is SubGraphNode subGraphNode
                && subGraphNode.subGraph != null
                && originalToCopy.TryGetValue(subGraphNode.subGraph, out StoryGraph mappedSub)
                && mappedSub != subGraphNode.subGraph)
            {
                subGraphNode.subGraph = mappedSub;
                EditorUtility.SetDirty(subGraphNode);
            }

            if (node is GotoNode gotoNode
                && gotoNode.targetGraph != null
                && originalToCopy.TryGetValue(gotoNode.targetGraph, out StoryGraph mappedGoto)
                && mappedGoto != gotoNode.targetGraph)
            {
                gotoNode.targetGraph = mappedGoto;
                EditorUtility.SetDirty(gotoNode);
            }

            using (SerializedObject serialized = new SerializedObject(node))
            {
                SerializedProperty iterator = serialized.GetIterator();
                bool changed = false;
                while (iterator.Next(true))
                {
                    if (iterator.propertyType != SerializedPropertyType.ObjectReference) continue;
                    if (iterator.objectReferenceValue is not StoryGraph original) continue;
                    if (!originalToCopy.TryGetValue(original, out StoryGraph remapped)) continue;
                    if (remapped == original) continue;
                    iterator.objectReferenceValue = remapped;
                    changed = true;
                }

                if (!changed) continue;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            EditorUtility.SetDirty(node);
        }
    }

    private static void StampAssetIdentity(StoryGraph graph)
    {
        string path = AssetDatabase.GetAssetPath(graph);
        if (string.IsNullOrEmpty(path)) return;
        graph.assetIdentity = AssetDatabase.AssetPathToGUID(path);
        EditorUtility.SetDirty(graph);
    }

    private static string MakeDestinationPath(string folder, string graphName, string suffix, HashSet<string> reserved)
    {
        string safeName = SanitizeFileName(graphName);
        if (string.IsNullOrEmpty(safeName))
        {
            safeName = "StoryGraph";
        }

        string fileName = safeName + suffix;
        string destPath = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{fileName}.asset");
        int guard = 0;
        while (!reserved.Add(destPath) || AssetAlreadyExists(destPath))
        {
            guard++;
            destPath = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{fileName}_{guard}.asset");
            if (guard > 1000)
            {
                throw new InvalidOperationException($"出力ファイル名を決定できません: {fileName}");
            }
        }

        return destPath;
    }

    private static bool AssetAlreadyExists(string assetPath)
    {
        return !string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(assetPath));
    }

    private static string SanitizeFileName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "StoryGraph";
        char[] invalid = Path.GetInvalidFileNameChars();
        string trimmed = name.Trim();
        for (int i = 0; i < invalid.Length; i++)
        {
            trimmed = trimmed.Replace(invalid[i], '_');
        }

        return trimmed;
    }

    private static string NormalizeFolderPath(string folder)
    {
        if (string.IsNullOrWhiteSpace(folder)) return null;
        string normalized = folder.Replace('\\', '/').TrimEnd('/');
        if (normalized == "Assets" || normalized.StartsWith("Assets/", StringComparison.Ordinal))
        {
            return normalized;
        }

        string dataPath = Application.dataPath.Replace('\\', '/');
        string abs = normalized;
        if (abs.StartsWith(dataPath, StringComparison.OrdinalIgnoreCase))
        {
            return "Assets" + abs.Substring(dataPath.Length);
        }

        return null;
    }
}
