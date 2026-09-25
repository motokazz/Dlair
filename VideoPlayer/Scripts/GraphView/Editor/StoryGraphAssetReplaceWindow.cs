using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class StoryGraphAssetReplaceWindow : EditorWindow
{
    [SerializeField] private StoryGraph sourceGraph;
    [SerializeField] private DefaultAsset replacementFolder;
    [SerializeField] private bool includeRelatedGraphs = true;
    [SerializeField] private bool includeSubfolders = true;
    [SerializeField] private StoryGraphAssetReplaceUtility.MatchMode matchMode =
        StoryGraphAssetReplaceUtility.MatchMode.ExactOrPartial;

    private Vector2 replacementScroll;
    private Vector2 unmatchedScroll;
    private StoryGraphAssetReplaceUtility.Preview preview;
    private string statusMessage;
    private MessageType statusType = MessageType.Info;

    [MenuItem("Assets/Story Graph/アセットを差し替え...", true)]
    private static bool ValidateOpenFromSelection()
    {
        return Selection.activeObject is StoryGraph;
    }

    [MenuItem("Assets/Story Graph/アセットを差し替え...")]
    private static void OpenFromSelection()
    {
        StoryGraphAssetReplaceWindow window = GetWindow<StoryGraphAssetReplaceWindow>("アセット差し替え");
        window.sourceGraph = Selection.activeObject as StoryGraph;
        window.RefreshPreview();
        window.Show();
        window.Focus();
    }

    [MenuItem("Tools/Story Graph/アセットを差し替え...")]
    private static void Open()
    {
        StoryGraphAssetReplaceWindow window = GetWindow<StoryGraphAssetReplaceWindow>("アセット差し替え");
        if (window.sourceGraph == null && Selection.activeObject is StoryGraph selected)
        {
            window.sourceGraph = selected;
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
            "指定した StoryGraph 内の Prefab / Image / VideoClip / AudioClip などを、" +
            "指定フォルダ内の同名または部分一致アセットに差し替えます。\n" +
            "型が合うものだけが対象です。実行前にプレビューで確認できます。",
            MessageType.Info);

        EditorGUI.BeginChangeCheck();
        sourceGraph = (StoryGraph)EditorGUILayout.ObjectField("対象 Graph", sourceGraph, typeof(StoryGraph), false);
        DrawFolderField();
        includeRelatedGraphs = EditorGUILayout.Toggle("関連グラフも含む", includeRelatedGraphs);
        includeSubfolders = EditorGUILayout.Toggle("サブフォルダも含む", includeSubfolders);
        int modeIndex = matchMode == StoryGraphAssetReplaceUtility.MatchMode.ExactOnly ? 0 : 1;
        modeIndex = EditorGUILayout.Popup("一致条件", modeIndex, new[] { "完全一致のみ", "完全一致 + 部分一致" });
        matchMode = modeIndex == 0
            ? StoryGraphAssetReplaceUtility.MatchMode.ExactOnly
            : StoryGraphAssetReplaceUtility.MatchMode.ExactOrPartial;
        if (EditorGUI.EndChangeCheck())
        {
            RefreshPreview();
        }

        EditorGUILayout.Space(6);
        DrawPreview();

        if (!string.IsNullOrEmpty(statusMessage))
        {
            EditorGUILayout.HelpBox(statusMessage, statusType);
        }

        EditorGUILayout.BeginHorizontal();
        using (new EditorGUI.DisabledScope(preview == null || preview.replacements.Count == 0))
        {
            if (GUILayout.Button("すべて選択"))
            {
                SetAllEnabled(true);
            }

            if (GUILayout.Button("すべて解除"))
            {
                SetAllEnabled(false);
            }
        }

        GUILayout.FlexibleSpace();
        int selectedCount = CountEnabled();
        using (new EditorGUI.DisabledScope(selectedCount == 0))
        {
            if (GUILayout.Button($"選択中 {selectedCount} 件を差し替え", GUILayout.Height(28), GUILayout.Width(220)))
            {
                ApplyNow();
            }
        }

        EditorGUILayout.EndHorizontal();
    }

    private void DrawFolderField()
    {
        EditorGUILayout.BeginHorizontal();
        replacementFolder = (DefaultAsset)EditorGUILayout.ObjectField(
            "差し替えフォルダ",
            replacementFolder,
            typeof(DefaultAsset),
            false);

        if (GUILayout.Button("選択...", GUILayout.Width(72)))
        {
            string start = Application.dataPath;
            if (replacementFolder != null)
            {
                string current = AssetDatabase.GetAssetPath(replacementFolder);
                if (!string.IsNullOrEmpty(current))
                {
                    start = current;
                }
            }

            string abs = EditorUtility.OpenFolderPanel("差し替えフォルダ", start, "");
            if (!string.IsNullOrEmpty(abs))
            {
                TrySetFolderFromAbsolute(abs);
            }
        }

        EditorGUILayout.EndHorizontal();

        if (replacementFolder != null && !AssetDatabase.IsValidFolder(AssetDatabase.GetAssetPath(replacementFolder)))
        {
            EditorGUILayout.HelpBox("差し替え先はフォルダを指定してください。", MessageType.Warning);
        }
    }

    private void DrawPreview()
    {
        int replaceCount = preview != null ? preview.replacements.Count : 0;
        int unmatchedCount = preview != null ? preview.unmatched.Count : 0;
        int candidateCount = preview != null ? preview.candidateCount : 0;

        EditorGUILayout.LabelField(
            $"差し替え候補 {replaceCount} 件 / 未一致 {unmatchedCount} 件 / フォルダ内アセット {candidateCount} 件",
            EditorStyles.boldLabel);

        if (preview != null && preview.warnings.Count > 0)
        {
            EditorGUILayout.HelpBox(string.Join("\n", preview.warnings), MessageType.Warning);
        }

        replacementScroll = EditorGUILayout.BeginScrollView(replacementScroll, GUILayout.MinHeight(180));
        if (replaceCount == 0)
        {
            EditorGUILayout.LabelField("Graph とフォルダを指定すると、差し替え一覧が出ます。");
        }
        else
        {
            for (int i = 0; i < preview.replacements.Count; i++)
            {
                DrawReplacementRow(preview.replacements[i]);
            }
        }

        EditorGUILayout.EndScrollView();

        if (unmatchedCount == 0) return;

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField($"未一致 ({unmatchedCount})", EditorStyles.boldLabel);
        unmatchedScroll = EditorGUILayout.BeginScrollView(unmatchedScroll, GUILayout.MinHeight(80), GUILayout.MaxHeight(160));
        for (int i = 0; i < preview.unmatched.Count; i++)
        {
            StoryGraphAssetReplaceUtility.Unmatched item = preview.unmatched[i];
            string nodeName = StoryGraphAssetReplaceUtility.DescribeNode(item.node);
            string graphName = item.graph != null ? item.graph.name : "?";
            string currentName = item.current != null ? item.current.name : "(null)";
            EditorGUILayout.LabelField($"{graphName} / {nodeName} / {item.fieldLabel}", currentName);
        }

        EditorGUILayout.EndScrollView();
    }

    private static void DrawReplacementRow(StoryGraphAssetReplaceUtility.Replacement item)
    {
        if (item == null) return;

        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.BeginHorizontal();
        item.enabled = EditorGUILayout.Toggle(item.enabled, GUILayout.Width(18));
        string graphName = item.graph != null ? item.graph.name : "?";
        string nodeName = StoryGraphAssetReplaceUtility.DescribeNode(item.node);
        EditorGUILayout.LabelField($"{graphName} / {nodeName} / {item.fieldLabel}", EditorStyles.boldLabel);
        GUILayout.Label(item.matchKind, GUILayout.Width(90));
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.ObjectField(item.current, typeof(UnityEngine.Object), false);
        GUILayout.Label("→", GUILayout.Width(16));
        EditorGUILayout.ObjectField(item.next, typeof(UnityEngine.Object), false);
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();
    }

    private void ApplyNow()
    {
        if (preview == null)
        {
            SetStatus("プレビューがありません。", MessageType.Error);
            return;
        }

        try
        {
            StoryGraphAssetReplaceUtility.ApplyResult result = StoryGraphAssetReplaceUtility.Apply(preview.replacements);
            string message = $"【StoryGraph】アセットを {result.changed} 件差し替えました。";
            if (result.warnings.Count > 0)
            {
                message += "\n" + string.Join("\n", result.warnings);
            }

            Debug.Log(message);
            SetStatus($"{result.changed} 件を差し替えました。", MessageType.Info);
            RefreshPreview();
        }
        catch (Exception ex)
        {
            Debug.LogError("【StoryGraph】アセット差し替えに失敗しました: " + ex.Message);
            SetStatus(ex.Message, MessageType.Error);
        }
    }

    private void RefreshPreview()
    {
        string folderPath = replacementFolder != null ? AssetDatabase.GetAssetPath(replacementFolder) : null;
        if (sourceGraph == null || string.IsNullOrEmpty(folderPath) || !AssetDatabase.IsValidFolder(folderPath))
        {
            preview = new StoryGraphAssetReplaceUtility.Preview();
            Repaint();
            return;
        }

        preview = StoryGraphAssetReplaceUtility.BuildPreview(
            sourceGraph,
            folderPath,
            includeRelatedGraphs,
            includeSubfolders,
            matchMode);
        Repaint();
    }

    private int CountEnabled()
    {
        if (preview == null) return 0;
        int count = 0;
        for (int i = 0; i < preview.replacements.Count; i++)
        {
            if (preview.replacements[i] != null && preview.replacements[i].enabled) count++;
        }

        return count;
    }

    private void SetAllEnabled(bool enabled)
    {
        if (preview == null) return;
        for (int i = 0; i < preview.replacements.Count; i++)
        {
            if (preview.replacements[i] != null)
            {
                preview.replacements[i].enabled = enabled;
            }
        }
    }

    private void TrySetFolderFromAbsolute(string absolutePath)
    {
        string dataPath = Application.dataPath.Replace('\\', '/');
        string abs = absolutePath.Replace('\\', '/');
        if (!abs.StartsWith(dataPath, StringComparison.OrdinalIgnoreCase))
        {
            SetStatus("差し替えフォルダはプロジェクトの Assets 以下である必要があります。", MessageType.Error);
            return;
        }

        string relative = "Assets" + abs.Substring(dataPath.Length);
        if (!AssetDatabase.IsValidFolder(relative))
        {
            SetStatus("指定フォルダを Assets から参照できません。", MessageType.Error);
            return;
        }

        replacementFolder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(relative);
        SetStatus(null, MessageType.Info);
        RefreshPreview();
    }

    private void SetStatus(string message, MessageType type)
    {
        statusMessage = message;
        statusType = type;
    }
}
