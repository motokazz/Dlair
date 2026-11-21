using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// データベースフォルダ直下のコピー元から、コピー先へデータを一括で複製するウィンドウ。
/// </summary>
public class DlairAssetsCloneWindow : EditorWindow
{
    [SerializeField] private DefaultAsset databaseFolder;
    [SerializeField] private string sourceName = "";
    [SerializeField] private string destinationName = "";

    private readonly List<string> childFolders = new List<string>();
    private DlairAssetsCloneUtility.Preview preview;
    private Vector2 messageScroll;
    private string statusMessage;
    private MessageType statusType = MessageType.Info;

    [MenuItem("Tools/Story Graph/データを一括クローン...")]
    private static void OpenFromTools()
    {
        Open();
    }

    [MenuItem("Assets/Story Graph/データを一括クローン...")]
    private static void OpenFromAssets()
    {
        Open();
    }

    private static void Open()
    {
        DlairAssetsCloneWindow window = GetWindow<DlairAssetsCloneWindow>("一括クローン");
        window.minSize = new Vector2(520f, 460f);
        window.GuessFromSelection();
        window.RefreshChildren();
        window.RefreshPreview();
        window.Show();
        window.Focus();
    }

    private void OnEnable()
    {
        minSize = new Vector2(520f, 460f);
        RefreshChildren();
        RefreshPreview();
    }

    private void OnGUI()
    {
        const float footerHeight = 48f;
        Rect footer = new Rect(0f, position.height - footerHeight, position.width, footerHeight);
        Rect body = new Rect(0f, 0f, position.width, Mathf.Max(0f, position.height - footerHeight));

        GUILayout.BeginArea(body);
        EditorGUILayout.Space(6);
        DrawDatabaseField();
        DrawFolderPopups();
        messageScroll = EditorGUILayout.BeginScrollView(messageScroll, GUILayout.ExpandHeight(true));
        EditorGUILayout.HelpBox(DlairAssetsCloneUtility.Summary, MessageType.Info);
        DrawPreview();
        if (!string.IsNullOrEmpty(statusMessage))
        {
            EditorGUILayout.HelpBox(statusMessage, statusType);
        }

        EditorGUILayout.EndScrollView();
        GUILayout.EndArea();

        string sourcePath = ChildPath(sourceName);
        string destinationPath = ChildPath(destinationName);
        bool sameFolder = !string.IsNullOrEmpty(sourcePath)
            && string.Equals(sourcePath, destinationPath, StringComparison.OrdinalIgnoreCase);

        GUILayout.BeginArea(footer);
        using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(sourcePath) || string.IsNullOrEmpty(destinationPath) || sameFolder))
        {
            if (GUILayout.Button("一括クローンを実行", GUILayout.Height(32)))
            {
                ExecuteNow(sourcePath, destinationPath);
            }
        }

        GUILayout.EndArea();
    }

    private void DrawDatabaseField()
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUI.BeginChangeCheck();
        databaseFolder = (DefaultAsset)EditorGUILayout.ObjectField(
            "データベース",
            databaseFolder,
            typeof(DefaultAsset),
            false);
        bool databaseChanged = EditorGUI.EndChangeCheck();

        if (GUILayout.Button("選択...", GUILayout.Width(72)))
        {
            string start = Application.dataPath;
            string current = DatabasePath();
            if (!string.IsNullOrEmpty(current)) start = current;
            string absolute = EditorUtility.OpenFolderPanel("データベースフォルダ", start, "");
            if (!string.IsNullOrEmpty(absolute))
            {
                if (TryAssignDatabase(absolute)) databaseChanged = true;
            }
        }

        EditorGUILayout.EndHorizontal();

        string databasePath = DatabasePath();
        if (databaseFolder != null && (string.IsNullOrEmpty(databasePath) || !AssetDatabase.IsValidFolder(databasePath)))
        {
            EditorGUILayout.HelpBox("データベースはフォルダを指定してください。", MessageType.Warning);
        }

        if (!databaseChanged) return;
        RefreshChildren();
        PruneFolderNames();
        RefreshPreview();
    }

    private void DrawFolderPopups()
    {
        if (childFolders.Count == 0)
        {
            return;
        }

        EditorGUI.BeginChangeCheck();
        sourceName = DrawFolderPopup("コピー元", sourceName);
        destinationName = DrawFolderPopup("コピー先", destinationName);
        if (EditorGUI.EndChangeCheck())
        {
            RefreshPreview();
        }
    }

    private string DrawFolderPopup(string label, string currentName)
    {
        string[] labels = new string[childFolders.Count + 1];
        labels[0] = "（選択）";
        int selected = 0;
        for (int i = 0; i < childFolders.Count; i++)
        {
            string leaf = Path.GetFileName(childFolders[i].Replace('\\', '/'));
            labels[i + 1] = leaf;
            if (string.Equals(leaf, currentName, StringComparison.OrdinalIgnoreCase))
            {
                selected = i + 1;
            }
        }

        int next = EditorGUILayout.Popup(label, selected, labels);
        if (next <= 0) return "";
        return Path.GetFileName(childFolders[next - 1].Replace('\\', '/'));
    }

    private void DrawPreview()
    {
        if (preview == null)
        {
            string message = childFolders.Count == 0
                ? "データベースフォルダの直下に、コピー元とコピー先になるフォルダが必要です。"
                : "コピー元とコピー先を選ぶと、複製する一覧が出ます。";
            EditorGUILayout.HelpBox(message, MessageType.Info);
            return;
        }

        string sourcePath = ChildPath(sourceName);
        string destinationPath = ChildPath(destinationName);
        if (!string.IsNullOrEmpty(sourcePath)
            && string.Equals(sourcePath, destinationPath, StringComparison.OrdinalIgnoreCase))
        {
            EditorGUILayout.HelpBox("コピー元とコピー先が同じフォルダです。", MessageType.Warning);
        }

        if (preview.notes.Count > 0)
        {
            EditorGUILayout.HelpBox(string.Join("\n", preview.notes), MessageType.Info);
        }

        if (preview.warnings.Count > 0)
        {
            EditorGUILayout.HelpBox(string.Join("\n", preview.warnings), MessageType.Warning);
        }

        CountPreview(out int graphs, out int overwrite, out int copy, out int skip);
        EditorGUILayout.LabelField(
            $"StoryGraph {graphs} 件（上書き {overwrite}） / その他 コピー {copy} / スキップ {skip} / 変数 {preview.variables.Count} 種",
            EditorStyles.boldLabel);

        if (preview.files.Count == 0 && preview.variables.Count == 0)
        {
            EditorGUILayout.LabelField("複製するファイルがありません。");
        }

        for (int i = 0; i < preview.files.Count; i++)
        {
            DlairAssetsCloneUtility.PlannedFile file = preview.files[i];
            if (file == null) continue;
            EditorGUILayout.LabelField(file.label, file.detail);
        }

        for (int i = 0; i < preview.variables.Count; i++)
        {
            DlairAssetsCloneUtility.PlannedVariable variable = preview.variables[i];
            if (variable == null) continue;
            EditorGUILayout.LabelField("変数", $"{variable.from}  →  {variable.to}    {variable.count} 箇所");
        }
    }

    private void ExecuteNow(string sourcePath, string destinationPath)
    {
        CountPreview(out int graphs, out int overwrite, out int copy, out int skip);
        int variables = preview != null ? preview.variables.Count : 0;
        string confirm = "コピー元: " + sourceName + "\nコピー先: " + destinationName
            + "\nStoryGraph: " + graphs + " 件（上書き " + overwrite + "）"
            + "\nその他: コピー " + copy + " / スキップ " + skip
            + "\n変数名: " + variables + " 種";
        if (preview != null && preview.warnings.Count > 0)
        {
            confirm += "\n\n確認事項:\n" + JoinLimited(preview.warnings, 12);
        }

        confirm += "\n\nこの内容で実行しますか？";
        if (!EditorUtility.DisplayDialog("一括クローン", confirm, "実行する", "中止")) return;

        try
        {
            DlairAssetsCloneUtility.Result result = DlairAssetsCloneUtility.Execute(
                DatabasePath(),
                sourcePath,
                destinationPath,
                (message, progress) => EditorUtility.DisplayProgressBar("一括クローン", message, progress));

            string summary = DlairAssetsCloneUtility.FormatResult(result);
            string detail = result.details.Count > 0 ? "\n" + string.Join("\n", result.details) : "";
            Debug.Log("【Dlair】一括クローン\n" + summary + detail);
            statusMessage = summary;
            statusType = result.warnings.Count > 0 ? MessageType.Warning : MessageType.Info;

            DefaultAsset destination = AssetDatabase.LoadAssetAtPath<DefaultAsset>(destinationPath);
            if (destination != null)
            {
                Selection.activeObject = destination;
                EditorGUIUtility.PingObject(destination);
            }

            RefreshPreview();
        }
        catch (Exception ex)
        {
            Debug.LogError("【Dlair】一括クローンに失敗しました: " + ex.Message);
            statusMessage = ex.Message;
            statusType = MessageType.Error;
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    private void CountPreview(out int graphs, out int overwrite, out int copy, out int skip)
    {
        graphs = 0;
        overwrite = 0;
        copy = 0;
        skip = 0;
        if (preview == null) return;
        for (int i = 0; i < preview.files.Count; i++)
        {
            DlairAssetsCloneUtility.PlannedFile file = preview.files[i];
            if (file == null) continue;
            if (file.label == "上書き")
            {
                graphs++;
                overwrite++;
            }
            else if (file.label == "複製")
            {
                graphs++;
            }
            else if (file.label == "コピー")
            {
                copy++;
            }
            else if (file.label == "スキップ")
            {
                skip++;
            }
        }
    }

    private void RefreshChildren()
    {
        string databasePath = DatabasePath();
        if (!string.IsNullOrEmpty(databasePath)
            && DlairAssetsCloneUtility.FindChildFolder(databasePath, "StoryGraph") != null)
        {
            AssignDatabaseFolder(databasePath);
            databasePath = DatabasePath();
        }

        childFolders.Clear();
        childFolders.AddRange(DlairAssetsCloneUtility.ListChildFolders(databasePath));
    }

    private void RefreshPreview()
    {
        string databasePath = DatabasePath();
        string sourcePath = ChildPath(sourceName);
        string destinationPath = ChildPath(destinationName);
        if (string.IsNullOrEmpty(databasePath) || string.IsNullOrEmpty(sourcePath) || string.IsNullOrEmpty(destinationPath))
        {
            preview = null;
            Repaint();
            return;
        }

        preview = DlairAssetsCloneUtility.BuildPreview(databasePath, sourcePath, destinationPath);
        Repaint();
    }

    private void PruneFolderNames()
    {
        if (ChildPath(sourceName) == null) sourceName = "";
        if (ChildPath(destinationName) == null) destinationName = "";
    }

    private void GuessFromSelection()
    {
        if (databaseFolder != null) return;
        string path = AssetDatabase.GetAssetPath(Selection.activeObject);
        if (string.IsNullOrEmpty(path) || !AssetDatabase.IsValidFolder(path)) return;
        AssignDatabaseFolder(path);
    }

    private bool TryAssignDatabase(string absolutePath)
    {
        string dataPath = Application.dataPath.Replace('\\', '/');
        string absolute = absolutePath.Replace('\\', '/');
        if (!absolute.StartsWith(dataPath, StringComparison.OrdinalIgnoreCase))
        {
            SetStatus("データベースフォルダはプロジェクトの Assets 以下である必要があります。", MessageType.Error);
            return false;
        }

        string relative = "Assets" + absolute.Substring(dataPath.Length);
        if (!AssetDatabase.IsValidFolder(relative))
        {
            SetStatus("指定フォルダを Assets から参照できません。", MessageType.Error);
            return false;
        }

        AssignDatabaseFolder(relative);
        SetStatus(null, MessageType.Info);
        return true;
    }

    private void AssignDatabaseFolder(string folder)
    {
        folder = folder.Replace('\\', '/').TrimEnd('/');
        if (DlairAssetsCloneUtility.FindChildFolder(folder, "StoryGraph") != null)
        {
            string parent = Path.GetDirectoryName(folder)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(parent) && AssetDatabase.IsValidFolder(parent))
            {
                sourceName = Path.GetFileName(folder);
                folder = parent;
            }
        }

        databaseFolder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(folder);
    }

    private string DatabasePath()
    {
        if (databaseFolder == null) return null;
        string path = AssetDatabase.GetAssetPath(databaseFolder);
        if (string.IsNullOrEmpty(path) || !AssetDatabase.IsValidFolder(path)) return null;
        return path.Replace('\\', '/').TrimEnd('/');
    }

    private string ChildPath(string folderName)
    {
        if (string.IsNullOrEmpty(folderName)) return null;
        for (int i = 0; i < childFolders.Count; i++)
        {
            string child = childFolders[i].Replace('\\', '/').TrimEnd('/');
            if (string.Equals(Path.GetFileName(child), folderName, StringComparison.OrdinalIgnoreCase))
            {
                return child;
            }
        }

        return null;
    }

    private static string JoinLimited(List<string> lines, int limit)
    {
        if (lines == null || lines.Count == 0) return "";
        int count = Mathf.Min(limit, lines.Count);
        string text = string.Join("\n", lines.GetRange(0, count));
        if (lines.Count > limit) text += "\n…";
        return text;
    }

    private void SetStatus(string message, MessageType type)
    {
        statusMessage = message;
        statusType = type;
    }
}
