using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// CSV から StoryVariableDatabase を作るウィンドウ。
/// 説明文は StoryVariableDatabaseUtility.Summary を表示する。
/// </summary>
public class StoryVariableDatabaseWindow : EditorWindow
{
    private const string DefaultAssetName = "StoryVariableDatabase";
    private const string DefaultFolder = "Assets/RawData/Main/StoryGraph";

    [SerializeField] private TextAsset csvAsset;
    [SerializeField] private string externalCsvPath = "";
    [SerializeField] private StoryVariableDatabase targetDatabase;
    [SerializeField] private DefaultAsset destinationFolder;
    [SerializeField] private string assetName = DefaultAssetName;

    private Vector2 previewScroll;
    private StoryVariableDatabaseUtility.ParseResult parseResult;
    private string parsedSourceKey = "";
    private string statusMessage;
    private MessageType statusType = MessageType.Info;

    [MenuItem("Assets/Story/CSVから変数データベースを作る...", true)]
    private static bool ValidateOpenFromSelection()
    {
        return IsCsvObject(Selection.activeObject) || Selection.activeObject is StoryVariableDatabase;
    }

    [MenuItem("Assets/Story/CSVから変数データベースを作る...")]
    private static void OpenFromSelection()
    {
        StoryVariableDatabaseWindow window = GetWindow<StoryVariableDatabaseWindow>("変数データベース");
        window.ApplySelection(Selection.activeObject);
        window.Show();
        window.Focus();
    }

    [MenuItem("Tools/Story/CSVから変数データベースを作る...")]
    private static void Open()
    {
        StoryVariableDatabaseWindow window = GetWindow<StoryVariableDatabaseWindow>("変数データベース");
        window.ApplySelection(Selection.activeObject);
        window.Show();
        window.Focus();
    }

    private void OnEnable()
    {
        minSize = new Vector2(480f, 420f);
        if (csvAsset == null && string.IsNullOrEmpty(externalCsvPath))
        {
            TryAssignCsv(Selection.activeObject);
        }

        if (targetDatabase == null && Selection.activeObject is StoryVariableDatabase selected)
        {
            targetDatabase = selected;
        }

        if (destinationFolder == null) GuessFolder();
        if (string.IsNullOrEmpty(assetName)) assetName = DefaultAssetName;
        InvalidatePreview();
    }

    private void OnSelectionChange()
    {
        if (ApplySelection(Selection.activeObject)) Repaint();
    }

    private void OnGUI()
    {
        EditorGUILayout.Space(6);
        EditorGUILayout.HelpBox(StoryVariableDatabaseUtility.Summary, MessageType.Info);
        DrawCsvField();
        DrawTargetField();

        EditorGUILayout.Space(6);
        RefreshPreview();
        DrawPreview();

        if (!string.IsNullOrEmpty(statusMessage))
        {
            EditorGUILayout.HelpBox(statusMessage, statusType);
        }

        DrawApplyButton();
    }

    private void DrawCsvField()
    {
        EditorGUI.BeginChangeCheck();
        TextAsset nextAsset = (TextAsset)EditorGUILayout.ObjectField("CSV", csvAsset, typeof(TextAsset), false);
        if (EditorGUI.EndChangeCheck())
        {
            AssignCsv(nextAsset, "");
        }

        EditorGUILayout.BeginHorizontal();
        if (!string.IsNullOrEmpty(externalCsvPath) && csvAsset == null)
        {
            EditorGUILayout.LabelField(externalCsvPath);
        }
        else
        {
            GUILayout.FlexibleSpace();
        }

        if (GUILayout.Button("CSVを選ぶ...", GUILayout.Width(110)))
        {
            PickCsvFile();
        }

        if (GUILayout.Button("読み直す", GUILayout.Width(72)))
        {
            InvalidatePreview();
            SetStatus(null, MessageType.Info);
        }

        EditorGUILayout.EndHorizontal();
    }

    private void DrawTargetField()
    {
        EditorGUILayout.Space(4);
        EditorGUILayout.BeginHorizontal();
        EditorGUI.BeginChangeCheck();
        targetDatabase = (StoryVariableDatabase)EditorGUILayout.ObjectField(
            "上書きするデータベース",
            targetDatabase,
            typeof(StoryVariableDatabase),
            false);
        if (EditorGUI.EndChangeCheck()) SetStatus(null, MessageType.Info);

        using (new EditorGUI.DisabledScope(targetDatabase == null))
        {
            if (GUILayout.Button("外す", GUILayout.Width(48)))
            {
                targetDatabase = null;
                GuessFolder();
                SetStatus(null, MessageType.Info);
            }
        }

        EditorGUILayout.EndHorizontal();

        if (targetDatabase != null)
        {
            EditorGUILayout.LabelField("CSV の内容で、このアセットの変数一覧を置き換えます。");
            return;
        }

        EditorGUILayout.LabelField("CSV の内容で、新しいアセットを作ります。");
        EditorGUI.BeginChangeCheck();
        DrawDestinationFolderField();
        assetName = EditorGUILayout.TextField("アセット名", assetName);
        if (EditorGUI.EndChangeCheck()) SetStatus(null, MessageType.Info);
    }

    private void DrawDestinationFolderField()
    {
        EditorGUILayout.BeginHorizontal();
        destinationFolder = (DefaultAsset)EditorGUILayout.ObjectField(
            "作成先フォルダ",
            destinationFolder,
            typeof(DefaultAsset),
            false);
        if (GUILayout.Button("選択...", GUILayout.Width(72)))
        {
            string start = Application.dataPath;
            if (destinationFolder != null)
            {
                string current = AssetDatabase.GetAssetPath(destinationFolder);
                if (!string.IsNullOrEmpty(current)) start = current;
            }

            string absolute = EditorUtility.OpenFolderPanel("作成先フォルダ", start, "");
            if (!string.IsNullOrEmpty(absolute)) TrySetDestinationFromAbsolute(absolute);
        }

        EditorGUILayout.EndHorizontal();
    }

    private void DrawPreview()
    {
        int count = parseResult != null ? parseResult.variables.Count : 0;
        EditorGUILayout.LabelField($"読み込み結果 ({count})", EditorStyles.boldLabel);
        if (parseResult != null && !string.IsNullOrEmpty(parseResult.error))
        {
            EditorGUILayout.HelpBox(parseResult.error, MessageType.Error);
        }

        DrawWarnings(parseResult != null ? parseResult.warnings : null);

        previewScroll = EditorGUILayout.BeginScrollView(previewScroll, GUILayout.MinHeight(140));
        if (count == 0)
        {
            EditorGUILayout.LabelField("CSV を指定すると、変数がここに出ます。");
        }
        else
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("変数名", EditorStyles.boldLabel, GUILayout.Width(160));
            EditorGUILayout.LabelField("型", EditorStyles.boldLabel, GUILayout.Width(64));
            EditorGUILayout.LabelField("初期値", EditorStyles.boldLabel, GUILayout.Width(100));
            EditorGUILayout.LabelField("説明", EditorStyles.boldLabel);
            EditorGUILayout.EndHorizontal();

            for (int i = 0; i < parseResult.variables.Count; i++)
            {
                DrawVariable(parseResult.variables[i]);
            }
        }

        EditorGUILayout.EndScrollView();
    }

    private static void DrawVariable(StoryVariableDefinition definition)
    {
        if (definition == null) return;
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField(definition.key, GUILayout.Width(160));
        EditorGUILayout.LabelField(definition.type.ToString(), GUILayout.Width(64));
        EditorGUILayout.LabelField(StoryVariableDatabaseUtility.FormatDefault(definition), GUILayout.Width(100));
        EditorGUILayout.LabelField(definition.description ?? "");
        EditorGUILayout.EndHorizontal();
    }

    private static void DrawWarnings(List<string> warnings)
    {
        if (warnings == null || warnings.Count == 0) return;
        const int limit = 12;
        int shown = warnings.Count < limit ? warnings.Count : limit;
        List<string> lines = new List<string>();
        for (int i = 0; i < shown; i++) lines.Add(warnings[i]);
        if (warnings.Count > limit) lines.Add($"ほか {warnings.Count - limit} 件");
        EditorGUILayout.HelpBox(string.Join("\n", lines), MessageType.Warning);
    }

    private void DrawApplyButton()
    {
        int count = parseResult != null && string.IsNullOrEmpty(parseResult.error) ? parseResult.variables.Count : 0;
        string folderPath = FolderPath();
        bool canCreate = targetDatabase == null && !string.IsNullOrEmpty(folderPath) && AssetDatabase.IsValidFolder(folderPath);
        bool canReplace = targetDatabase != null;
        using (new EditorGUI.DisabledScope(count == 0 || (!canCreate && !canReplace)))
        {
            string label = targetDatabase != null
                ? $"{targetDatabase.name} の変数を {count} 件で置き換え"
                : $"新規 StoryVariableDatabase を作成（{count} 件）";
            if (GUILayout.Button(label, GUILayout.Height(32)))
            {
                ApplyNow();
            }
        }
    }

    private void ApplyNow()
    {
        if (parseResult == null || !string.IsNullOrEmpty(parseResult.error) || parseResult.variables.Count == 0)
        {
            SetStatus("書き込む変数がありません。", MessageType.Warning);
            return;
        }

        try
        {
            StoryVariableDatabase database;
            if (targetDatabase != null)
            {
                bool accepted = EditorUtility.DisplayDialog(
                    "変数データベースを置き換え",
                    $"{targetDatabase.name} の変数一覧を、CSV の {parseResult.variables.Count} 件で置き換えます。\nCSV に無い変数は削除されます。",
                    "置き換え",
                    "キャンセル");
                if (!accepted) return;

                StoryVariableDatabaseUtility.ReplaceVariables(targetDatabase, parseResult.variables);
                database = targetDatabase;
                SetStatus($"{database.name} の変数を {parseResult.variables.Count} 件で置き換えました。", MessageType.Info);
                Debug.Log($"【StoryVariableDatabase】{database.name} の変数を {parseResult.variables.Count} 件で置き換えました。");
            }
            else
            {
                database = StoryVariableDatabaseUtility.CreateAsset(FolderPath(), assetName, parseResult.variables);
                targetDatabase = database;
                SetStatus($"{database.name} を作成しました。変数は {parseResult.variables.Count} 件です。", MessageType.Info);
                Debug.Log($"【StoryVariableDatabase】{AssetDatabase.GetAssetPath(database)} を作成しました。");
            }

            EditorGUIUtility.PingObject(database);
            Selection.activeObject = database;
        }
        catch (Exception ex)
        {
            Debug.LogError("【StoryVariableDatabase】CSV からの作成に失敗しました: " + ex.Message);
            SetStatus(ex.Message, MessageType.Error);
        }
    }

    private void RefreshPreview()
    {
        string path = CurrentCsvPath();
        string key = SourceKey(path);
        if (key == parsedSourceKey && parseResult != null) return;
        if (key == parsedSourceKey && string.IsNullOrEmpty(path)) return;

        parsedSourceKey = key;
        parseResult = string.IsNullOrEmpty(path) ? null : StoryVariableDatabaseUtility.ParseFile(path);
    }

    private static string SourceKey(string path)
    {
        if (string.IsNullOrEmpty(path)) return "";
        string absolute = path;
        if (!Path.IsPathRooted(path))
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            absolute = Path.Combine(projectRoot, path);
        }

        if (!File.Exists(absolute)) return path + "|missing";
        return path + "|" + File.GetLastWriteTimeUtc(absolute).Ticks;
    }

    private void InvalidatePreview()
    {
        parsedSourceKey = "";
        parseResult = null;
    }

    private string CurrentCsvPath()
    {
        if (csvAsset != null)
        {
            string assetPath = AssetDatabase.GetAssetPath(csvAsset);
            if (!string.IsNullOrEmpty(assetPath)) return assetPath;
        }

        return externalCsvPath ?? "";
    }

    private bool ApplySelection(UnityEngine.Object selected)
    {
        bool changed = false;
        if (IsCsvObject(selected)) changed |= TryAssignCsv(selected);
        if (selected is StoryVariableDatabase database && targetDatabase != database)
        {
            targetDatabase = database;
            changed = true;
        }

        return changed;
    }

    private bool TryAssignCsv(UnityEngine.Object selected)
    {
        if (!IsCsvObject(selected)) return false;
        string path = AssetDatabase.GetAssetPath(selected);
        TextAsset asset = selected as TextAsset;
        if (asset == null) asset = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
        if (csvAsset == asset && string.IsNullOrEmpty(externalCsvPath) && asset != null) return false;
        if (asset == null && externalCsvPath == path) return false;
        AssignCsv(asset, asset != null ? "" : path);
        return true;
    }

    private void AssignCsv(TextAsset asset, string externalPath)
    {
        csvAsset = asset;
        externalCsvPath = externalPath ?? "";
        if (string.IsNullOrEmpty(assetName) || assetName == DefaultAssetName)
        {
            string fileName = CsvFileName();
            if (!string.IsNullOrEmpty(fileName)) assetName = fileName;
        }

        if (destinationFolder == null) GuessFolder();
        InvalidatePreview();
        SetStatus(null, MessageType.Info);
    }

    private void PickCsvFile()
    {
        string start = Application.dataPath;
        string current = CurrentCsvPath();
        if (!string.IsNullOrEmpty(current))
        {
            string directory = Path.GetDirectoryName(current);
            if (!string.IsNullOrEmpty(directory)) start = directory;
        }

        string absolute = EditorUtility.OpenFilePanel("CSV", start, "csv");
        if (string.IsNullOrEmpty(absolute)) return;
        SetCsvFromAbsolute(absolute);
    }

    private void SetCsvFromAbsolute(string absolutePath)
    {
        string dataPath = Application.dataPath.Replace('\\', '/');
        string absolute = absolutePath.Replace('\\', '/');
        if (absolute.StartsWith(dataPath, StringComparison.OrdinalIgnoreCase))
        {
            string relative = "Assets" + absolute.Substring(dataPath.Length);
            TextAsset asset = AssetDatabase.LoadAssetAtPath<TextAsset>(relative);
            AssignCsv(asset, asset != null ? "" : relative);
            return;
        }

        AssignCsv(null, absolute);
    }

    private void GuessFolder()
    {
        string path = "";
        if (csvAsset != null) path = AssetDatabase.GetAssetPath(csvAsset);
        else if (!string.IsNullOrEmpty(externalCsvPath) && externalCsvPath.StartsWith("Assets")) path = externalCsvPath;
        else if (targetDatabase != null) path = AssetDatabase.GetAssetPath(targetDatabase);

        string folder = "";
        if (!string.IsNullOrEmpty(path))
        {
            folder = Path.GetDirectoryName(path)?.Replace('\\', '/');
        }

        if (string.IsNullOrEmpty(folder) || !AssetDatabase.IsValidFolder(folder))
        {
            folder = AssetDatabase.IsValidFolder(DefaultFolder) ? DefaultFolder : "Assets";
        }

        destinationFolder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(folder);
    }

    private void TrySetDestinationFromAbsolute(string absolutePath)
    {
        string dataPath = Application.dataPath.Replace('\\', '/');
        string absolute = absolutePath.Replace('\\', '/');
        if (!absolute.StartsWith(dataPath, StringComparison.OrdinalIgnoreCase))
        {
            SetStatus("作成先はプロジェクトの Assets 以下である必要があります。", MessageType.Error);
            return;
        }

        string relative = "Assets" + absolute.Substring(dataPath.Length);
        if (!AssetDatabase.IsValidFolder(relative))
        {
            SetStatus("指定フォルダを Assets から参照できません。", MessageType.Error);
            return;
        }

        destinationFolder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(relative);
        SetStatus(null, MessageType.Info);
    }

    private string FolderPath()
    {
        if (destinationFolder == null) return "";
        string path = AssetDatabase.GetAssetPath(destinationFolder);
        if (string.IsNullOrEmpty(path) || !AssetDatabase.IsValidFolder(path)) return "";
        return path;
    }

    private string CsvFileName()
    {
        string path = CurrentCsvPath();
        if (string.IsNullOrEmpty(path)) return "";
        return Path.GetFileNameWithoutExtension(path);
    }

    private static bool IsCsvObject(UnityEngine.Object obj)
    {
        if (obj == null) return false;
        string path = AssetDatabase.GetAssetPath(obj);
        return !string.IsNullOrEmpty(path) && path.EndsWith(".csv", StringComparison.OrdinalIgnoreCase);
    }

    private void SetStatus(string message, MessageType type)
    {
        statusMessage = message;
        statusType = type;
    }
}
