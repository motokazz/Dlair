using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class StoryGraphCloneWindow : EditorWindow
{
    [SerializeField] private StoryGraph sourceGraph;
    [SerializeField] private DefaultAsset destinationFolder;
    [SerializeField] private string nameSuffix = "_Clone";

    private Vector2 previewScroll;
    private List<StoryGraph> previewGraphs = new List<StoryGraph>();
    private string statusMessage;
    private MessageType statusType = MessageType.Info;

    [MenuItem("Assets/Story Graph/関連グラフごと複製...", true)]
    private static bool ValidateOpenFromSelection()
    {
        return Selection.activeObject is StoryGraph;
    }

    [MenuItem("Assets/Story Graph/関連グラフごと複製...")]
    private static void OpenFromSelection()
    {
        StoryGraphCloneWindow window = GetWindow<StoryGraphCloneWindow>("グラフ複製");
        window.sourceGraph = Selection.activeObject as StoryGraph;
        window.GuessDestinationFromSource();
        window.RefreshPreview();
        window.Show();
        window.Focus();
    }

    [MenuItem("Tools/Story Graph/関連グラフごと複製...")]
    private static void Open()
    {
        StoryGraphCloneWindow window = GetWindow<StoryGraphCloneWindow>("グラフ複製");
        if (window.sourceGraph == null && Selection.activeObject is StoryGraph selected)
        {
            window.sourceGraph = selected;
            window.GuessDestinationFromSource();
        }

        window.RefreshPreview();
        window.Show();
        window.Focus();
    }

    private void OnEnable()
    {
        RefreshPreview();
    }

    private void OnGUI()
    {
        EditorGUILayout.Space(6);
        EditorGUILayout.HelpBox(
            "指定した StoryGraph と、そこから辿れる SubGraph / Goto 先の関連グラフをすべて複製します。\n" +
            "複製後はサブグラフ参照と Goto 先が新しいグラフを指すので、元のグラフとは独立して同じ挙動を再現できます。\n" +
            "動画・画像などのメディアアセット自体は共有したままです。",
            MessageType.Info);

        EditorGUI.BeginChangeCheck();
        sourceGraph = (StoryGraph)EditorGUILayout.ObjectField("複製元 Graph", sourceGraph, typeof(StoryGraph), false);
        DrawDestinationFolderField();
        nameSuffix = EditorGUILayout.TextField("ファイル名サフィックス", nameSuffix);
        if (EditorGUI.EndChangeCheck())
        {
            RefreshPreview();
        }

        EditorGUILayout.Space(8);
        DrawPreview();

        if (!string.IsNullOrEmpty(statusMessage))
        {
            EditorGUILayout.HelpBox(statusMessage, statusType);
        }

        string folderPath = destinationFolder != null ? AssetDatabase.GetAssetPath(destinationFolder) : null;
        bool canDuplicate = sourceGraph != null && !string.IsNullOrEmpty(folderPath) && AssetDatabase.IsValidFolder(folderPath);
        using (new EditorGUI.DisabledScope(!canDuplicate))
        {
            if (GUILayout.Button("指定フォルダへ複製", GUILayout.Height(32)))
            {
                DuplicateNow();
            }
        }
    }

    private void DrawDestinationFolderField()
    {
        EditorGUILayout.BeginHorizontal();
        destinationFolder = (DefaultAsset)EditorGUILayout.ObjectField(
            "複製先フォルダ",
            destinationFolder,
            typeof(DefaultAsset),
            false);

        if (GUILayout.Button("選択...", GUILayout.Width(72)))
        {
            string start = Application.dataPath;
            if (destinationFolder != null)
            {
                string current = AssetDatabase.GetAssetPath(destinationFolder);
                if (!string.IsNullOrEmpty(current))
                {
                    start = current;
                }
            }

            string abs = EditorUtility.OpenFolderPanel("複製先フォルダ", start, "");
            if (!string.IsNullOrEmpty(abs))
            {
                TrySetDestinationFromAbsolute(abs);
            }
        }

        EditorGUILayout.EndHorizontal();

        if (destinationFolder != null && !AssetDatabase.IsValidFolder(AssetDatabase.GetAssetPath(destinationFolder)))
        {
            EditorGUILayout.HelpBox("複製先はフォルダを指定してください。", MessageType.Warning);
        }
    }

    private void DrawPreview()
    {
        int count = previewGraphs != null ? previewGraphs.Count : 0;
        EditorGUILayout.LabelField($"複製対象 ({count} 件)", EditorStyles.boldLabel);
        previewScroll = EditorGUILayout.BeginScrollView(previewScroll, GUILayout.MinHeight(120));
        if (count == 0)
        {
            EditorGUILayout.LabelField("複製元 Graph を指定すると、関連グラフ一覧が出ます。");
        }
        else
        {
            for (int i = 0; i < previewGraphs.Count; i++)
            {
                StoryGraph graph = previewGraphs[i];
                if (graph == null) continue;
                string path = AssetDatabase.GetAssetPath(graph);
                string label = i == 0 ? "ルート  " : "関連    ";
                EditorGUILayout.LabelField(label + graph.name, path);
            }
        }

        EditorGUILayout.EndScrollView();
    }

    private void DuplicateNow()
    {
        string folderPath = destinationFolder != null ? AssetDatabase.GetAssetPath(destinationFolder) : null;
        if (sourceGraph == null)
        {
            SetStatus("複製元 Graph を指定してください。", MessageType.Error);
            return;
        }

        if (string.IsNullOrEmpty(folderPath) || !AssetDatabase.IsValidFolder(folderPath))
        {
            SetStatus("複製先フォルダを指定してください。", MessageType.Error);
            return;
        }

        try
        {
            EditorUtility.DisplayProgressBar("StoryGraph 複製", "関連グラフを複製しています…", 0.4f);
            StoryGraphCloneUtility.Result result = StoryGraphCloneUtility.DuplicateTree(sourceGraph, folderPath, nameSuffix);
            Selection.activeObject = result.rootCopy;
            EditorGUIUtility.PingObject(result.rootCopy);

            string message = $"【StoryGraph】{result.copies.Count} 件を '{folderPath}' に複製しました。ルート: {result.rootCopy.name}";
            if (result.warnings.Count > 0)
            {
                message += "\n" + string.Join("\n", result.warnings);
            }

            Debug.Log(message);
            SetStatus($"{result.copies.Count} 件を複製しました。", MessageType.Info);
            RefreshPreview();
        }
        catch (Exception ex)
        {
            Debug.LogError("【StoryGraph】複製に失敗しました: " + ex.Message);
            SetStatus(ex.Message, MessageType.Error);
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    private void RefreshPreview()
    {
        previewGraphs = sourceGraph != null
            ? StoryGraphCloneUtility.CollectRelatedGraphs(sourceGraph)
            : new List<StoryGraph>();
        Repaint();
    }

    private void GuessDestinationFromSource()
    {
        if (sourceGraph == null) return;
        string path = AssetDatabase.GetAssetPath(sourceGraph);
        if (string.IsNullOrEmpty(path)) return;
        string folder = System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/');
        if (string.IsNullOrEmpty(folder)) return;
        destinationFolder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(folder);
    }

    private void TrySetDestinationFromAbsolute(string absolutePath)
    {
        string dataPath = Application.dataPath.Replace('\\', '/');
        string abs = absolutePath.Replace('\\', '/');
        if (!abs.StartsWith(dataPath, StringComparison.OrdinalIgnoreCase))
        {
            SetStatus("複製先はプロジェクトの Assets 以下である必要があります。", MessageType.Error);
            return;
        }

        string relative = "Assets" + abs.Substring(dataPath.Length);
        if (!AssetDatabase.IsValidFolder(relative))
        {
            SetStatus("指定フォルダを Assets から参照できません。", MessageType.Error);
            return;
        }

        destinationFolder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(relative);
        SetStatus(null, MessageType.Info);
    }

    private void SetStatus(string message, MessageType type)
    {
        statusMessage = message;
        statusType = type;
    }
}
