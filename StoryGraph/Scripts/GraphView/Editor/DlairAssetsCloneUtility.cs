using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// データベースフォルダ直下のコピー元から、コピー先へ StoryGraph / Voices / UI / Videos をまとめて複製する。
/// StoryGraph は複製ツールと同じ GUID 振り直しと参照のつなぎ替えを行い、その後にアセット差し替えと変数名の置換をする。
/// </summary>
public static class DlairAssetsCloneUtility
{
    public const string Summary =
        "データベースフォルダの直下にあるコピー元フォルダから、コピー先フォルダへデータをまとめて複製します。\n" +
        "コピー先に StoryGraph、Voices、UI、Videos が無いときは作成します。フォルダ名の大文字小文字は区別しません。\n" +
        "StoryGraph は、ファイル名を「_」で区切った後半をコピー先フォルダ名に変えて、コピー先の StoryGraph フォルダへ複製します。同名があるときは上書きします。\n" +
        "ノード GUID の振り直しと、今回複製する StoryGraph 同士の参照のつなぎ替えは、複製ツールと同じです。今回の複製に含まれないグラフへのリンクはそのまま残します。\n" +
        "Voices、UI、Videos と、StoryGraph 以外のファイルは、コピー先に同じ名前が無いときだけコピーします。あるときは何もしません。\n" +
        "複製した StoryGraph の Prefab、画像、動画、音声などは、参照元がデータベースフォルダの中にあるときだけ、コピー先フォルダ内の同名または部分一致アセットへ差し替えます。データベースの外への参照はそのまま残します。\n" +
        "変数名にコピー元フォルダ名が含まれるときは、その部分をコピー先フォルダ名に変えます。大文字小文字は、一致した箇所に合わせます。";

    public static readonly string[] CategoryFolderNames = { "StoryGraph", "Voices", "UI", "Videos" };

    /// <summary>
    /// 実行前に出す、複製・コピー・変数名の一覧。
    /// </summary>
    public class Preview
    {
        public readonly List<string> notes = new List<string>();
        public readonly List<PlannedFile> files = new List<PlannedFile>();
        public readonly List<PlannedVariable> variables = new List<PlannedVariable>();
        public readonly List<string> warnings = new List<string>();
    }

    /// <summary>
    /// ファイル1件の予定。ラベルは複製、上書き、コピー、スキップのいずれか。
    /// </summary>
    public class PlannedFile
    {
        public string label;
        public string detail;
    }

    /// <summary>
    /// 変数名1種類の置換予定と、グラフ内の箇所数。
    /// </summary>
    public class PlannedVariable
    {
        public string from;
        public string to;
        public int count;
    }

    /// <summary>
    /// 一括クローンの実行結果。
    /// </summary>
    public class Result
    {
        public int graphsNew;
        public int graphsOverwritten;
        public int filesCopied;
        public int filesSkipped;
        public int assetsReplaced;
        public int unmatchedAssets;
        public int variablesChanged;
        public readonly List<string> notes = new List<string>();
        public readonly List<string> warnings = new List<string>();
        public readonly List<string> details = new List<string>();
    }

    private class GraphOp
    {
        public StoryGraph source;
        public string sourcePath;
        public string destinationPath;
        public bool overwrite;
    }

    private class FileOp
    {
        public string sourcePath;
        public string destinationPath;
        public string fileName;
        public bool skip;
        public string label;
        public string detail;
        public string categoryPath;
    }

    private class Plan
    {
        public string destinationRoot;
        public string sourceFolderName;
        public string destinationFolderName;
        public readonly List<GraphOp> graphs = new List<GraphOp>();
        public readonly List<FileOp> files = new List<FileOp>();
        public readonly List<StoryGraphVariableUtility.RenameRule> rules = new List<StoryGraphVariableUtility.RenameRule>();
        public readonly List<StoryGraph> sourceGraphs = new List<StoryGraph>();
        public readonly List<string> notes = new List<string>();
        public readonly List<string> warnings = new List<string>();
    }

    /// <summary>
    /// データベースフォルダの直下にあるフォルダを返す。
    /// </summary>
    public static List<string> ListChildFolders(string databaseFolder)
    {
        List<string> folders = new List<string>();
        string normalized = NormalizeFolderPath(databaseFolder);
        if (string.IsNullOrEmpty(normalized) || !AssetDatabase.IsValidFolder(normalized)) return folders;

        string[] children = AssetDatabase.GetSubFolders(normalized);
        for (int i = 0; i < children.Length; i++)
        {
            if (string.IsNullOrEmpty(children[i])) continue;
            folders.Add(children[i].Replace('\\', '/').TrimEnd('/'));
        }

        folders.Sort(StringComparer.OrdinalIgnoreCase);
        return folders;
    }

    /// <summary>
    /// 親フォルダの直下から、名前が一致するフォルダを探す。大文字小文字は区別しない。
    /// </summary>
    public static string FindChildFolder(string parentFolder, string folderName)
    {
        string parent = NormalizeFolderPath(parentFolder);
        if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(folderName)) return null;
        if (!AssetDatabase.IsValidFolder(parent)) return null;

        string[] children = AssetDatabase.GetSubFolders(parent);
        for (int i = 0; i < children.Length; i++)
        {
            string child = children[i].Replace('\\', '/').TrimEnd('/');
            if (string.Equals(Leaf(child), folderName, StringComparison.OrdinalIgnoreCase))
            {
                return child;
            }
        }

        return null;
    }

    /// <summary>
    /// 「_」区切りの後半を、コピー先フォルダ名に置き換えたファイル名を返す。区切りが無いときは元の名前。
    /// </summary>
    public static string BuildClonedGraphFileName(string sourceFileName, string destinationFolderName)
    {
        string name = sourceFileName ?? "";
        int split = name.LastIndexOf('_');
        if (split < 0 || split >= name.Length - 1) return name;
        return name.Substring(0, split + 1) + (destinationFolderName ?? "");
    }

    /// <summary>
    /// 変数名に含まれるコピー元フォルダ名を、コピー先フォルダ名へ置き換える。無ければ元の名前。
    /// </summary>
    public static string ReplaceFolderToken(string variableName, string sourceFolderName, string destinationFolderName)
    {
        if (string.IsNullOrEmpty(variableName) || string.IsNullOrEmpty(sourceFolderName)) return variableName;
        if (string.IsNullOrEmpty(destinationFolderName)) return variableName;

        StringBuilder builder = new StringBuilder();
        int index = 0;
        bool found = false;
        while (index < variableName.Length)
        {
            int hit = variableName.IndexOf(sourceFolderName, index, StringComparison.OrdinalIgnoreCase);
            if (hit < 0)
            {
                builder.Append(variableName, index, variableName.Length - index);
                break;
            }

            found = true;
            builder.Append(variableName, index, hit - index);
            string matched = variableName.Substring(hit, sourceFolderName.Length);
            builder.Append(AdaptCasing(destinationFolderName, matched));
            index = hit + sourceFolderName.Length;
        }

        return found ? builder.ToString() : variableName;
    }

    public static Preview BuildPreview(string databaseFolder, string sourceFolder, string destinationFolder)
    {
        Preview preview = new Preview();
        try
        {
            Validate(databaseFolder, sourceFolder, destinationFolder);
            Plan plan = CreatePlan(sourceFolder, destinationFolder);
            preview.notes.AddRange(plan.notes);
            preview.warnings.AddRange(plan.warnings);
            for (int i = 0; i < plan.graphs.Count; i++)
            {
                GraphOp graph = plan.graphs[i];
                preview.files.Add(new PlannedFile
                {
                    label = graph.overwrite ? "上書き" : "複製",
                    detail = Path.GetFileName(graph.sourcePath) + "  →  " + Path.GetFileName(graph.destinationPath)
                });
            }

            for (int i = 0; i < plan.files.Count; i++)
            {
                FileOp file = plan.files[i];
                preview.files.Add(new PlannedFile
                {
                    label = file.label,
                    detail = file.detail
                });
            }

            FillVariablePreview(plan, preview);
        }
        catch (ArgumentException ex)
        {
            preview.warnings.Add(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            preview.warnings.Add(ex.Message);
        }

        return preview;
    }

    public static Result Execute(
        string databaseFolder,
        string sourceFolder,
        string destinationFolder,
        Action<string, float> reportProgress = null)
    {
        Validate(databaseFolder, sourceFolder, destinationFolder);
        Result result = new Result();
        bool previousSuppress = StoryGraphGuidUtility.SuppressImportHandling;
        StoryGraphGuidUtility.SuppressImportHandling = true;
        try
        {
            Report(reportProgress, "フォルダを確認しています", 0.05f);
            EnsureDestinationFolders(destinationFolder, result.notes);
            Plan plan = CreatePlan(sourceFolder, destinationFolder);
            result.notes.AddRange(plan.notes);
            result.warnings.AddRange(plan.warnings);

            Report(reportProgress, "StoryGraph を複製しています", 0.2f);
            List<StoryGraph> copies = WriteGraphs(plan, result);
            List<string> copyPaths = new List<string>();
            for (int i = 0; i < copies.Count; i++)
            {
                string path = copies[i] != null ? AssetDatabase.GetAssetPath(copies[i]) : null;
                if (!string.IsNullOrEmpty(path)) copyPaths.Add(path);
            }

            Report(reportProgress, "その他のファイルをコピーしています", 0.55f);
            CopyOtherFiles(plan, result, reportProgress);
            copies = ReloadCopiedGraphs(copyPaths, result);

            Report(reportProgress, "参照を差し替えています", 0.78f);
            RelinkAssets(copies, plan.destinationRoot, databaseFolder, result);

            Report(reportProgress, "変数名を変えています", 0.9f);
            RenameVariables(copies, plan, result);

            AssetDatabase.SaveAssets();
            ReloadOpenWindows(copies);
            Report(reportProgress, "完了", 1f);
        }
        finally
        {
            StoryGraphGuidUtility.SuppressImportHandling = previousSuppress;
        }

        return result;
    }

    public static string FormatResult(Result result)
    {
        if (result == null) return "";
        StringBuilder builder = new StringBuilder();
        builder.Append("StoryGraph ").Append(result.graphsNew + result.graphsOverwritten)
            .Append(" 件（上書き ").Append(result.graphsOverwritten).AppendLine("）");
        builder.Append("その他のファイル コピー ").Append(result.filesCopied)
            .Append(" / スキップ ").Append(result.filesSkipped).AppendLine();
        builder.Append("アセット差し替え ").Append(result.assetsReplaced)
            .Append(" 件 / 未一致 ").Append(result.unmatchedAssets).AppendLine();
        builder.Append("変数名 ").Append(result.variablesChanged).Append(" 箇所");

        if (result.notes.Count > 0)
        {
            builder.AppendLine();
            for (int i = 0; i < result.notes.Count; i++)
            {
                builder.AppendLine(result.notes[i]);
            }
        }

        if (result.warnings.Count > 0)
        {
            builder.AppendLine();
            for (int i = 0; i < result.warnings.Count; i++)
            {
                builder.AppendLine(result.warnings[i]);
            }
        }

        return builder.ToString().TrimEnd();
    }

    private static void Validate(string databaseFolder, string sourceFolder, string destinationFolder)
    {
        if (EditorApplication.isPlaying)
        {
            throw new InvalidOperationException("再生中は一括クローンできません。");
        }

        databaseFolder = NormalizeFolderPath(databaseFolder);
        sourceFolder = NormalizeFolderPath(sourceFolder);
        destinationFolder = NormalizeFolderPath(destinationFolder);
        if (string.IsNullOrEmpty(databaseFolder) || !AssetDatabase.IsValidFolder(databaseFolder))
        {
            throw new ArgumentException("データベースフォルダが Assets 以下にありません。");
        }

        if (string.IsNullOrEmpty(sourceFolder) || !AssetDatabase.IsValidFolder(sourceFolder))
        {
            throw new ArgumentException("コピー元フォルダが Assets 以下にありません。");
        }

        if (string.IsNullOrEmpty(destinationFolder) || !AssetDatabase.IsValidFolder(destinationFolder))
        {
            throw new ArgumentException("コピー先フォルダが Assets 以下にありません。");
        }

        if (!IsDirectChild(databaseFolder, sourceFolder) || !IsDirectChild(databaseFolder, destinationFolder))
        {
            throw new ArgumentException("コピー元とコピー先は、データベースフォルダの直下を選んでください。");
        }

        if (string.Equals(sourceFolder, destinationFolder, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("コピー元とコピー先が同じフォルダです。");
        }
    }

    private static Plan CreatePlan(string sourceFolder, string destinationFolder)
    {
        Plan plan = new Plan();
        plan.destinationRoot = NormalizeFolderPath(destinationFolder);
        plan.sourceFolderName = Leaf(sourceFolder);
        plan.destinationFolderName = Leaf(destinationFolder);

        HashSet<string> reservedGraphNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int c = 0; c < CategoryFolderNames.Length; c++)
        {
            string canonical = CategoryFolderNames[c];
            string sourceCategory = FindChildFolder(sourceFolder, canonical);
            string destinationCategory = FindChildFolder(destinationFolder, canonical);
            bool destinationMissing = string.IsNullOrEmpty(destinationCategory);
            if (destinationMissing)
            {
                destinationCategory = plan.destinationRoot + "/" + canonical;
                plan.notes.Add(canonical + " フォルダを作成します。");
            }

            if (string.IsNullOrEmpty(sourceCategory))
            {
                plan.warnings.Add("コピー元に " + canonical + " フォルダがありません。");
                continue;
            }

            if (canonical == "StoryGraph")
            {
                AddGraphOps(plan, sourceCategory, destinationCategory, reservedGraphNames);
                AddFileOps(plan, sourceCategory, destinationCategory, reservedGraphNames);
            }
            else
            {
                AddFileOps(plan, sourceCategory, destinationCategory, null);
            }
        }

        for (int i = 0; i < plan.graphs.Count; i++)
        {
            if (plan.graphs[i].source != null) plan.sourceGraphs.Add(plan.graphs[i].source);
        }

        plan.rules.AddRange(BuildVariableRules(plan.sourceGraphs, plan.sourceFolderName, plan.destinationFolderName));
        return plan;
    }

    private static void AddGraphOps(
        Plan plan,
        string sourceCategory,
        string destinationCategory,
        HashSet<string> reservedGraphNames)
    {
        List<StoryGraph> graphs = CollectStoryGraphs(sourceCategory);
        Dictionary<string, string> reservedPaths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < graphs.Count; i++)
        {
            StoryGraph graph = graphs[i];
            string sourcePath = AssetDatabase.GetAssetPath(graph);
            if (string.IsNullOrEmpty(sourcePath))
            {
                plan.warnings.Add("パスが無い StoryGraph をスキップしました: " + graph.name);
                continue;
            }

            string sourceFileName = Path.GetFileNameWithoutExtension(sourcePath);
            string destFileName = SanitizeFileName(BuildClonedGraphFileName(sourceFileName, plan.destinationFolderName));
            string destinationPath = destinationCategory.TrimEnd('/') + "/" + destFileName + ".asset";
            if (reservedPaths.ContainsKey(destinationPath))
            {
                plan.warnings.Add("複製後の名前が重なるためスキップしました: " + Path.GetFileName(sourcePath)
                    + " → " + Path.GetFileName(destinationPath));
                continue;
            }

            if (AssetExists(destinationPath) && !IsStoryGraphAsset(destinationPath))
            {
                plan.warnings.Add("StoryGraph ではないため上書きしません: " + destinationPath);
                continue;
            }

            reservedPaths.Add(destinationPath, sourcePath);
            reservedGraphNames.Add(destFileName + ".asset");
            plan.graphs.Add(new GraphOp
            {
                source = graph,
                sourcePath = sourcePath,
                destinationPath = destinationPath,
                overwrite = AssetExists(destinationPath)
            });
        }
    }

    private static void AddFileOps(
        Plan plan,
        string sourceCategory,
        string destinationCategory,
        HashSet<string> reservedGraphNames)
    {
        List<string> sourcePaths = CollectAssetPaths(sourceCategory);
        HashSet<string> existingNames = CollectFileNames(destinationCategory);
        if (reservedGraphNames != null)
        {
            foreach (string reservedName in reservedGraphNames)
            {
                existingNames.Add(reservedName);
            }
        }

        for (int i = 0; i < sourcePaths.Count; i++)
        {
            string sourcePath = sourcePaths[i];
            if (IsStoryGraphAsset(sourcePath)) continue;

            string fileName = Path.GetFileName(sourcePath);
            if (string.IsNullOrEmpty(fileName)) continue;

            string prefix = sourceCategory.TrimEnd('/') + "/";
            if (!sourcePath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;
            string relative = sourcePath.Substring(prefix.Length);
            string destinationPath = destinationCategory.TrimEnd('/') + "/" + relative;
            bool skip = existingNames.Contains(fileName);
            if (!skip)
            {
                existingNames.Add(fileName);
            }

            plan.files.Add(new FileOp
            {
                sourcePath = sourcePath,
                destinationPath = destinationPath,
                fileName = fileName,
                skip = skip,
                categoryPath = destinationCategory,
                label = skip ? "スキップ" : "コピー",
                detail = skip ? relative + "（同名あり）" : relative
            });
        }
    }

    private static void FillVariablePreview(Plan plan, Preview preview)
    {
        if (plan.sourceGraphs.Count == 0 || plan.rules.Count == 0) return;

        StoryGraphVariableUtility.Preview variablePreview =
            StoryGraphVariableUtility.BuildPreview(plan.sourceGraphs, plan.rules);
        preview.warnings.AddRange(variablePreview.warnings);
        Dictionary<string, PlannedVariable> grouped = new Dictionary<string, PlannedVariable>(StringComparer.Ordinal);
        for (int i = 0; i < variablePreview.changes.Count; i++)
        {
            StoryGraphVariableUtility.Occurrence change = variablePreview.changes[i];
            if (change == null) continue;
            string key = change.currentName + "\n" + change.nextName;
            if (!grouped.TryGetValue(key, out PlannedVariable planned))
            {
                planned = new PlannedVariable
                {
                    from = change.currentName,
                    to = change.nextName
                };
                grouped.Add(key, planned);
                preview.variables.Add(planned);
            }

            planned.count++;
        }
    }

    private static List<StoryGraphVariableUtility.RenameRule> BuildVariableRules(
        IList<StoryGraph> graphs,
        string sourceFolderName,
        string destinationFolderName)
    {
        List<StoryGraphVariableUtility.RenameRule> rules = new List<StoryGraphVariableUtility.RenameRule>();
        List<StoryGraphVariableUtility.NameHit> hits = StoryGraphVariableUtility.CollectNames(graphs);
        for (int i = 0; i < hits.Count; i++)
        {
            string from = hits[i].name;
            string to = ReplaceFolderToken(from, sourceFolderName, destinationFolderName);
            if (string.Equals(from, to, StringComparison.Ordinal)) continue;
            rules.Add(new StoryGraphVariableUtility.RenameRule
            {
                from = from,
                to = to
            });
        }

        return rules;
    }

    private static void EnsureDestinationFolders(string destinationFolder, List<string> notes)
    {
        string root = NormalizeFolderPath(destinationFolder);
        for (int i = 0; i < CategoryFolderNames.Length; i++)
        {
            string canonical = CategoryFolderNames[i];
            if (!string.IsNullOrEmpty(FindChildFolder(root, canonical))) continue;
            EnsureFolderExists(root + "/" + canonical);
            notes.Add(canonical + " フォルダを作成しました。");
        }
    }

    private static List<StoryGraph> WriteGraphs(Plan plan, Result result)
    {
        List<StoryGraph> copies = new List<StoryGraph>();
        if (plan.graphs.Count == 0) return copies;

        List<string> createdPaths = new List<string>();
        List<GraphOp> written = new List<GraphOp>();
        bool editing = false;
        try
        {
            AssetDatabase.StartAssetEditing();
            editing = true;
            for (int i = 0; i < plan.graphs.Count; i++)
            {
                GraphOp op = plan.graphs[i];
                bool exists = AssetExists(op.destinationPath);
                if (exists && !IsStoryGraphAsset(op.destinationPath))
                {
                    result.warnings.Add("StoryGraph ではないため上書きしません: " + op.destinationPath);
                    continue;
                }

                if (exists)
                {
                    OverwriteAssetFile(op.sourcePath, op.destinationPath);
                    result.graphsOverwritten++;
                    result.details.Add("上書き  " + Path.GetFileName(op.sourcePath) + "  →  " + Path.GetFileName(op.destinationPath));
                }
                else
                {
                    EnsureFolderExists(ParentFolder(op.destinationPath));
                    if (!AssetDatabase.CopyAsset(op.sourcePath, op.destinationPath))
                    {
                        throw new InvalidOperationException("コピーに失敗しました: " + op.sourcePath + " → " + op.destinationPath);
                    }

                    createdPaths.Add(op.destinationPath);
                    result.graphsNew++;
                    result.details.Add("複製  " + Path.GetFileName(op.sourcePath) + "  →  " + Path.GetFileName(op.destinationPath));
                }

                written.Add(op);
            }
        }
        catch
        {
            if (editing)
            {
                AssetDatabase.StopAssetEditing();
                editing = false;
            }

            for (int i = 0; i < createdPaths.Count; i++)
            {
                if (AssetExists(createdPaths[i]))
                {
                    AssetDatabase.DeleteAsset(createdPaths[i]);
                }
            }

            throw;
        }
        finally
        {
            if (editing)
            {
                AssetDatabase.StopAssetEditing();
            }
        }

        Dictionary<StoryGraph, StoryGraph> originalToCopy = new Dictionary<StoryGraph, StoryGraph>();
        try
        {
            for (int i = 0; i < written.Count; i++)
            {
                GraphOp op = written[i];
                AssetDatabase.ImportAsset(op.destinationPath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                StoryGraph copy = AssetDatabase.LoadAssetAtPath<StoryGraph>(op.destinationPath);
                if (copy == null)
                {
                    throw new InvalidOperationException("複製したグラフを読み込めません: " + op.destinationPath);
                }

                if (copy == op.source)
                {
                    throw new InvalidOperationException("コピー元とコピー先が同じアセットです: " + op.destinationPath);
                }

                ApplyFileName(copy);
                originalToCopy.Add(op.source, copy);
                copies.Add(copy);
            }

            Dictionary<string, string> guidMap = AssignNewGuids(copies, result.warnings);
            for (int i = 0; i < copies.Count; i++)
            {
                StoryGraph copy = copies[i];
                StoryGraphGuidUtility.RemapStoredGuids(copy, guidMap);
                RemapGraphObjectReferences(copy, originalToCopy);
                ApplyFileName(copy);
                StampAssetIdentity(copy);
                EditorUtility.SetDirty(copy);
            }

            AssetDatabase.SaveAssets();
        }
        catch
        {
            for (int i = 0; i < createdPaths.Count; i++)
            {
                if (AssetExists(createdPaths[i]))
                {
                    AssetDatabase.DeleteAsset(createdPaths[i]);
                }
            }

            throw;
        }

        return copies;
    }

    private static void CopyOtherFiles(Plan plan, Result result, Action<string, float> reportProgress)
    {
        bool editing = false;
        Dictionary<string, HashSet<string>> namesByFolder = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        try
        {
            AssetDatabase.StartAssetEditing();
            editing = true;
            for (int i = 0; i < plan.files.Count; i++)
            {
                FileOp op = plan.files[i];
                float t = 0.55f + 0.2f * ((i + 1f) / Math.Max(1, plan.files.Count));
                Report(reportProgress, op.fileName, t);
                HashSet<string> names = NamesInFolder(namesByFolder, op.categoryPath);
                if (op.skip || names.Contains(op.fileName) || AssetExists(op.destinationPath))
                {
                    result.filesSkipped++;
                    names.Add(op.fileName);
                    continue;
                }

                EnsureFolderExists(ParentFolder(op.destinationPath));
                if (!AssetDatabase.CopyAsset(op.sourcePath, op.destinationPath))
                {
                    result.warnings.Add("コピーに失敗しました: " + op.sourcePath);
                    result.filesSkipped++;
                    continue;
                }

                names.Add(op.fileName);
                result.filesCopied++;
                result.details.Add("コピー  " + op.detail);
            }
        }
        finally
        {
            if (editing)
            {
                AssetDatabase.StopAssetEditing();
            }
        }

        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
    }

    private static List<StoryGraph> ReloadCopiedGraphs(List<string> paths, Result result)
    {
        List<StoryGraph> graphs = new List<StoryGraph>();
        for (int i = 0; i < paths.Count; i++)
        {
            StoryGraph graph = AssetDatabase.LoadAssetAtPath<StoryGraph>(paths[i]);
            if (graph == null)
            {
                result.warnings.Add("複製した StoryGraph を読み戻せません: " + paths[i]);
                continue;
            }

            graphs.Add(graph);
        }

        return graphs;
    }

    private static void RelinkAssets(List<StoryGraph> copies, string destinationRoot, string databaseFolder, Result result)
    {
        if (copies == null || copies.Count == 0) return;

        string database = NormalizeFolderPath(databaseFolder);
        StoryGraphAssetReplaceUtility.Preview preview = StoryGraphAssetReplaceUtility.BuildPreview(
            copies,
            destinationRoot,
            true,
            StoryGraphAssetReplaceUtility.MatchMode.ExactOrPartial);
        result.warnings.AddRange(preview.warnings);

        int keptOutside = 0;
        for (int i = 0; i < preview.replacements.Count; i++)
        {
            StoryGraphAssetReplaceUtility.Replacement replacement = preview.replacements[i];
            if (replacement == null || !replacement.enabled) continue;
            if (IsInside(replacement.currentPath, database)) continue;
            replacement.enabled = false;
            keptOutside++;
        }

        for (int i = 0; i < preview.unmatched.Count; i++)
        {
            StoryGraphAssetReplaceUtility.Unmatched unmatched = preview.unmatched[i];
            if (unmatched == null) continue;
            if (!IsInside(unmatched.currentPath, database))
            {
                keptOutside++;
                continue;
            }

            result.unmatchedAssets++;
            string graphName = unmatched.graph != null ? unmatched.graph.name : "?";
            string currentName = unmatched.current != null ? unmatched.current.name : "(null)";
            result.details.Add("未一致  " + graphName + " / " + unmatched.fieldLabel + " / " + currentName);
        }

        if (keptOutside > 0)
        {
            result.notes.Add("データベース外の参照 " + keptOutside + " 件は差し替えませんでした。");
        }

        StoryGraphAssetReplaceUtility.ApplyResult applied = StoryGraphAssetReplaceUtility.Apply(preview.replacements);
        result.assetsReplaced = applied.changed;
        result.warnings.AddRange(applied.warnings);
        for (int i = 0; i < preview.replacements.Count; i++)
        {
            StoryGraphAssetReplaceUtility.Replacement replacement = preview.replacements[i];
            if (replacement == null || !replacement.enabled) continue;
            string graphName = replacement.graph != null ? replacement.graph.name : "?";
            result.details.Add("差し替え  " + graphName + " / " + replacement.fieldLabel + "  "
                + replacement.currentPath + "  →  " + replacement.nextPath);
        }
    }

    private static void RenameVariables(List<StoryGraph> copies, Plan plan, Result result)
    {
        if (copies == null || copies.Count == 0 || plan.rules.Count == 0) return;

        StoryGraphVariableUtility.Preview preview = StoryGraphVariableUtility.BuildPreview(copies, plan.rules);
        result.warnings.AddRange(preview.warnings);
        StoryGraphVariableUtility.ApplyResult applied = StoryGraphVariableUtility.Apply(preview.changes);
        result.variablesChanged = applied.changed;
        result.warnings.AddRange(applied.warnings);

        Dictionary<string, int> counts = new Dictionary<string, int>(StringComparer.Ordinal);
        List<string> order = new List<string>();
        for (int i = 0; i < preview.changes.Count; i++)
        {
            StoryGraphVariableUtility.Occurrence change = preview.changes[i];
            if (change == null) continue;
            string key = change.currentName + "  →  " + change.nextName;
            if (!counts.ContainsKey(key))
            {
                counts[key] = 0;
                order.Add(key);
            }

            counts[key]++;
        }

        for (int i = 0; i < order.Count; i++)
        {
            result.details.Add("変数  " + order[i] + "  " + counts[order[i]] + " 箇所");
        }
    }

    private static void ReloadOpenWindows(IList<StoryGraph> graphs)
    {
        if (graphs == null || graphs.Count == 0) return;
        HashSet<StoryGraph> affected = new HashSet<StoryGraph>();
        for (int i = 0; i < graphs.Count; i++)
        {
            if (graphs[i] != null) affected.Add(graphs[i]);
        }

        StoryGraphWindow[] windows = Resources.FindObjectsOfTypeAll<StoryGraphWindow>();
        for (int i = 0; i < windows.Length; i++)
        {
            StoryGraphWindow window = windows[i];
            if (window == null || window.currentGraph == null) continue;
            if (!affected.Contains(window.currentGraph)) continue;
            window.ReloadFromAsset();
        }
    }

    private static Dictionary<string, string> AssignNewGuids(List<StoryGraph> copies, List<string> warnings)
    {
        Dictionary<string, string> map = new Dictionary<string, string>();
        for (int c = 0; c < copies.Count; c++)
        {
            StoryGraph copy = copies[c];
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
                    warnings?.Add("ノード GUID が重複していたため共有しました: " + copy.name);
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

    private static void ApplyFileName(StoryGraph graph)
    {
        string path = AssetDatabase.GetAssetPath(graph);
        if (string.IsNullOrEmpty(path)) return;
        string fileName = Path.GetFileNameWithoutExtension(path);
        if (string.IsNullOrEmpty(fileName) || string.Equals(graph.name, fileName, StringComparison.Ordinal)) return;
        graph.name = fileName;
        EditorUtility.SetDirty(graph);
    }

    private static void OverwriteAssetFile(string sourcePath, string destinationPath)
    {
        string sourceAbsolute = ToAbsolutePath(sourcePath);
        string destinationAbsolute = ToAbsolutePath(destinationPath);
        if (File.Exists(destinationAbsolute))
        {
            File.SetAttributes(destinationAbsolute, FileAttributes.Normal);
        }

        File.Copy(sourceAbsolute, destinationAbsolute, true);
    }

    private static List<StoryGraph> CollectStoryGraphs(string folder)
    {
        List<StoryGraph> graphs = new List<StoryGraph>();
        if (string.IsNullOrEmpty(folder) || !AssetDatabase.IsValidFolder(folder)) return graphs;

        string[] guids = AssetDatabase.FindAssets("t:StoryGraph", new[] { folder });
        HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            if (string.IsNullOrEmpty(path) || !seen.Add(path)) continue;
            if (!IsInside(path, folder)) continue;
            StoryGraph graph = AssetDatabase.LoadAssetAtPath<StoryGraph>(path);
            if (graph == null) continue;
            graphs.Add(graph);
        }

        graphs.Sort((a, b) => string.Compare(
            AssetDatabase.GetAssetPath(a),
            AssetDatabase.GetAssetPath(b),
            StringComparison.OrdinalIgnoreCase));
        return graphs;
    }

    private static List<string> CollectAssetPaths(string folder)
    {
        List<string> paths = new List<string>();
        if (string.IsNullOrEmpty(folder) || !AssetDatabase.IsValidFolder(folder)) return paths;

        string[] guids = AssetDatabase.FindAssets("", new[] { folder });
        HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            if (string.IsNullOrEmpty(path) || !seen.Add(path)) continue;
            if (AssetDatabase.IsValidFolder(path)) continue;
            if (!IsInside(path, folder)) continue;
            if (path.EndsWith(".meta", StringComparison.OrdinalIgnoreCase)) continue;
            paths.Add(path);
        }

        paths.Sort(StringComparer.OrdinalIgnoreCase);
        return paths;
    }

    private static HashSet<string> CollectFileNames(string folder)
    {
        HashSet<string> names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        List<string> paths = CollectAssetPaths(folder);
        for (int i = 0; i < paths.Count; i++)
        {
            string fileName = Path.GetFileName(paths[i]);
            if (!string.IsNullOrEmpty(fileName)) names.Add(fileName);
        }

        return names;
    }

    private static HashSet<string> NamesInFolder(Dictionary<string, HashSet<string>> namesByFolder, string folder)
    {
        string key = string.IsNullOrEmpty(folder) ? "" : folder;
        if (!namesByFolder.TryGetValue(key, out HashSet<string> names))
        {
            names = CollectFileNames(key);
            namesByFolder.Add(key, names);
        }

        return names;
    }

    private static bool IsStoryGraphAsset(string assetPath)
    {
        Type type = AssetDatabase.GetMainAssetTypeAtPath(assetPath);
        return type != null && typeof(StoryGraph).IsAssignableFrom(type);
    }

    private static bool AssetExists(string assetPath)
    {
        return !string.IsNullOrEmpty(assetPath) && !string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(assetPath));
    }

    private static bool IsInside(string assetPath, string folder)
    {
        if (string.IsNullOrEmpty(assetPath) || string.IsNullOrEmpty(folder)) return false;
        string prefix = folder.Replace('\\', '/').TrimEnd('/') + "/";
        return assetPath.Replace('\\', '/').StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsDirectChild(string parentFolder, string childFolder)
    {
        string parent = ParentFolder(childFolder);
        return string.Equals(parent, parentFolder.TrimEnd('/'), StringComparison.OrdinalIgnoreCase);
    }

    private static void EnsureFolderExists(string folderPath)
    {
        folderPath = string.IsNullOrEmpty(folderPath) ? null : folderPath.Replace('\\', '/').TrimEnd('/');
        if (string.IsNullOrEmpty(folderPath) || AssetDatabase.IsValidFolder(folderPath)) return;

        string parent = ParentFolder(folderPath);
        string name = Leaf(folderPath);
        if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(name))
        {
            throw new InvalidOperationException("フォルダを作れません: " + folderPath);
        }

        EnsureFolderExists(parent);
        if (AssetDatabase.IsValidFolder(folderPath)) return;
        string guid = AssetDatabase.CreateFolder(parent, name);
        if (string.IsNullOrEmpty(guid) && !AssetDatabase.IsValidFolder(folderPath))
        {
            throw new InvalidOperationException("フォルダを作れません: " + folderPath);
        }
    }

    private static string ParentFolder(string assetPath)
    {
        if (string.IsNullOrEmpty(assetPath)) return null;
        string parent = Path.GetDirectoryName(assetPath.Replace('\\', '/'));
        if (string.IsNullOrEmpty(parent)) return null;
        return parent.Replace('\\', '/');
    }

    private static string Leaf(string assetPath)
    {
        if (string.IsNullOrEmpty(assetPath)) return "";
        return Path.GetFileName(assetPath.Replace('\\', '/').TrimEnd('/'));
    }

    private static string ToAbsolutePath(string assetPath)
    {
        string relative = assetPath.Replace('\\', '/');
        const string assetsPrefix = "Assets/";
        if (relative.StartsWith(assetsPrefix, StringComparison.OrdinalIgnoreCase))
        {
            relative = relative.Substring(assetsPrefix.Length);
        }

        return Path.Combine(Application.dataPath, relative.Replace('/', Path.DirectorySeparatorChar));
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

    private static string AdaptCasing(string destinationFolderName, string matched)
    {
        if (string.IsNullOrEmpty(destinationFolderName)) return destinationFolderName;
        if (IsAllUpper(matched) && matched.Length > 1) return destinationFolderName.ToUpperInvariant();
        if (IsTitleCase(matched)) return Capitalize(destinationFolderName);
        return destinationFolderName;
    }

    private static bool IsAllUpper(string value)
    {
        bool hasLetter = false;
        for (int i = 0; i < value.Length; i++)
        {
            if (!char.IsLetter(value[i])) continue;
            hasLetter = true;
            if (!char.IsUpper(value[i])) return false;
        }

        return hasLetter;
    }

    private static bool IsTitleCase(string value)
    {
        if (string.IsNullOrEmpty(value) || !char.IsLetter(value[0]) || !char.IsUpper(value[0])) return false;
        for (int i = 1; i < value.Length; i++)
        {
            if (char.IsLetter(value[i]) && !char.IsLower(value[i])) return false;
        }

        return true;
    }

    private static string Capitalize(string value)
    {
        if (string.IsNullOrEmpty(value)) return value;
        string lower = value.ToLowerInvariant();
        if (lower.Length == 1) return lower.ToUpperInvariant();
        return char.ToUpperInvariant(lower[0]) + lower.Substring(1);
    }

    private static string NormalizeFolderPath(string folder)
    {
        string normalized = StoryGraphAssetReplaceUtility.NormalizeFolderPath(folder);
        if (string.IsNullOrEmpty(normalized)) return null;
        return normalized.TrimEnd('/');
    }

    private static void Report(Action<string, float> reportProgress, string message, float progress)
    {
        reportProgress?.Invoke(message, progress);
    }
}
