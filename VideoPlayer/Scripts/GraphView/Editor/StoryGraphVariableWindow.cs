using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class StoryGraphVariableWindow : EditorWindow
{
    [Serializable]
    private class RenameEdit
    {
        public string from = "";
        public string to = "";
        public int declareCount;
        public int referenceCount;
    }

    [SerializeField] private List<StoryGraph> targetGraphs = new List<StoryGraph>();
    [SerializeField] private List<RenameEdit> edits = new List<RenameEdit>();
    [SerializeField] private string bulkSuffix = "";

    private Vector2 nameScroll;
    private Vector2 previewScroll;
    private StoryGraphVariableUtility.Preview preview;
    private string statusMessage;
    private MessageType statusType = MessageType.Info;

    [MenuItem("Assets/Story Graph/変数名を変更...", true)]
    private static bool ValidateOpenFromSelection()
    {
        return StoryGraphVariableUtility.CollectSelectedGraphs().Count > 0;
    }

    [MenuItem("Assets/Story Graph/変数名を変更...")]
    private static void OpenFromSelection()
    {
        StoryGraphVariableWindow window = GetWindow<StoryGraphVariableWindow>("変数名の変更");
        window.SetTargetsFromSelection();
        window.SyncEdits(true);
        window.RefreshPreview();
        window.Show();
        window.Focus();
    }

    [MenuItem("Tools/Story Graph/変数名を変更...")]
    private static void Open()
    {
        StoryGraphVariableWindow window = GetWindow<StoryGraphVariableWindow>("変数名の変更");
        window.SetTargetsFromSelection();
        window.SyncEdits(true);
        window.RefreshPreview();
        window.Show();
        window.Focus();
    }

    private void OnEnable()
    {
        minSize = new Vector2(460f, 420f);
        if (targetGraphs == null) targetGraphs = new List<StoryGraph>();
        if (edits == null) edits = new List<RenameEdit>();
        SetTargetsFromSelection();
        SyncEdits(true);
        RefreshPreview();
    }

    private void OnSelectionChange()
    {
        if (!CaptureSelectedGraphs()) return;
        SyncEdits(true);
        RefreshPreview();
    }

    private void OnGUI()
    {
        EditorGUILayout.Space(6);
        EditorGUILayout.HelpBox(StoryGraphVariableUtility.Summary, MessageType.Info);
        DrawSelectedGraphs();

        EditorGUILayout.Space(6);
        DrawRenameRows();
        DrawBulkSuffix();

        EditorGUILayout.Space(6);
        DrawPreview();

        if (!string.IsNullOrEmpty(statusMessage))
        {
            EditorGUILayout.HelpBox(statusMessage, statusType);
        }

        int changeCount = preview != null ? preview.changes.Count : 0;
        using (new EditorGUI.DisabledScope(changeCount == 0))
        {
            if (GUILayout.Button($"変数名を {changeCount} 箇所変更", GUILayout.Height(32)))
            {
                ApplyNow();
            }
        }
    }

    private void DrawSelectedGraphs()
    {
        int count = targetGraphs != null ? targetGraphs.Count : 0;
        EditorGUILayout.LabelField($"対象 StoryGraph ({count})", EditorStyles.boldLabel);
        if (count == 0)
        {
            EditorGUILayout.HelpBox(
                "Project ウィンドウで変数名を変える StoryGraph を選択してください。",
                MessageType.Warning);
            return;
        }

        using (new EditorGUI.DisabledScope(true))
        {
            for (int i = 0; i < targetGraphs.Count; i++)
            {
                EditorGUILayout.ObjectField(targetGraphs[i], typeof(StoryGraph), false);
            }
        }
    }

    private void DrawRenameRows()
    {
        int count = edits != null ? edits.Count : 0;
        EditorGUILayout.LabelField($"変数名 ({count})", EditorStyles.boldLabel);
        nameScroll = EditorGUILayout.BeginScrollView(nameScroll, GUILayout.MinHeight(120));
        if (count == 0)
        {
            EditorGUILayout.LabelField("対象の StoryGraph に変数名があると、ここに出ます。");
        }
        else
        {
            EditorGUI.BeginChangeCheck();
            for (int i = 0; i < edits.Count; i++)
            {
                RenameEdit edit = edits[i];
                if (edit == null) continue;

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(
                    $"{edit.from}    宣言 {edit.declareCount} / 参照 {edit.referenceCount}",
                    GUILayout.MinWidth(220));
                GUILayout.Label("→", GUILayout.Width(18));
                edit.to = EditorGUILayout.TextField(edit.to);
                EditorGUILayout.EndHorizontal();
            }

            if (EditorGUI.EndChangeCheck())
            {
                RefreshPreview();
            }
        }

        EditorGUILayout.EndScrollView();
    }

    private void DrawBulkSuffix()
    {
        EditorGUILayout.BeginHorizontal();
        bulkSuffix = EditorGUILayout.TextField("一括サフィックス", bulkSuffix);
        using (new EditorGUI.DisabledScope(edits == null || edits.Count == 0 || string.IsNullOrWhiteSpace(bulkSuffix)))
        {
            if (GUILayout.Button("変更後へ入れる", GUILayout.Width(110)))
            {
                ApplyBulkSuffix();
            }
        }

        EditorGUILayout.EndHorizontal();
    }

    private void DrawPreview()
    {
        int changeCount = preview != null ? preview.changes.Count : 0;
        EditorGUILayout.LabelField($"変更箇所 ({changeCount})", EditorStyles.boldLabel);
        if (preview != null && preview.warnings.Count > 0)
        {
            EditorGUILayout.HelpBox(string.Join("\n", preview.warnings), MessageType.Warning);
        }

        previewScroll = EditorGUILayout.BeginScrollView(previewScroll, GUILayout.MinHeight(100));
        if (changeCount == 0)
        {
            EditorGUILayout.LabelField("変更後の名前が元と違う変数だけ、ここに出ます。");
        }
        else
        {
            for (int i = 0; i < preview.changes.Count; i++)
            {
                DrawChange(preview.changes[i]);
            }
        }

        EditorGUILayout.EndScrollView();
    }

    private static void DrawChange(StoryGraphVariableUtility.Occurrence item)
    {
        if (item == null) return;

        EditorGUILayout.BeginVertical("box");
        string path = item.graph != null ? AssetDatabase.GetAssetPath(item.graph) : "";
        EditorGUILayout.LabelField(StoryGraphVariableUtility.DescribeOccurrence(item), path);
        EditorGUILayout.LabelField($"{item.currentName}  →  {item.nextName}");
        EditorGUILayout.EndVertical();
    }

    private void ApplyBulkSuffix()
    {
        string suffix = string.IsNullOrWhiteSpace(bulkSuffix) ? "" : bulkSuffix.Trim();
        if (string.IsNullOrEmpty(suffix) || edits == null) return;

        for (int i = 0; i < edits.Count; i++)
        {
            RenameEdit edit = edits[i];
            if (edit == null) continue;
            edit.to = (edit.from ?? "") + suffix;
        }

        RefreshPreview();
    }

    private void ApplyNow()
    {
        if (preview == null || preview.changes.Count == 0)
        {
            SetStatus("変更する変数名がありません。", MessageType.Warning);
            return;
        }

        if (preview.warnings.Count > 0)
        {
            bool proceed = EditorUtility.DisplayDialog(
                "変数名の変更",
                "確認事項があります。このまま変更しますか？\n\n" + string.Join("\n", preview.warnings),
                "変更する",
                "中止");
            if (!proceed) return;
        }

        try
        {
            StoryGraphVariableUtility.ApplyResult result = StoryGraphVariableUtility.Apply(preview.changes);
            string message = $"【StoryGraph】変数名を {result.changed} 箇所変更しました。";
            if (result.warnings.Count > 0)
            {
                message += "\n" + string.Join("\n", result.warnings);
            }

            Debug.Log(message);
            SetStatus($"{result.changed} 箇所の変数名を変更しました。", MessageType.Info);
            SyncEdits(false);
            RefreshPreview();
        }
        catch (Exception ex)
        {
            Debug.LogError("【StoryGraph】変数名の変更に失敗しました: " + ex.Message);
            SetStatus(ex.Message, MessageType.Error);
        }
    }

    private void SetTargetsFromSelection()
    {
        targetGraphs = StoryGraphVariableUtility.CollectSelectedGraphs();
    }

    private bool CaptureSelectedGraphs()
    {
        List<StoryGraph> selected = StoryGraphVariableUtility.CollectSelectedGraphs();
        if (selected.Count == 0) return false;
        targetGraphs = selected;
        return true;
    }

    private void SyncEdits(bool preserveCustomTargets)
    {
        Dictionary<string, string> previous = new Dictionary<string, string>(StringComparer.Ordinal);
        if (preserveCustomTargets && edits != null)
        {
            for (int i = 0; i < edits.Count; i++)
            {
                RenameEdit edit = edits[i];
                if (edit == null || string.IsNullOrEmpty(edit.from)) continue;
                previous[edit.from] = edit.to ?? edit.from;
            }
        }

        List<StoryGraphVariableUtility.NameHit> hits = StoryGraphVariableUtility.CollectNames(targetGraphs);
        edits = new List<RenameEdit>(hits.Count);
        for (int i = 0; i < hits.Count; i++)
        {
            StoryGraphVariableUtility.NameHit hit = hits[i];
            string to = hit.name;
            if (preserveCustomTargets && previous.TryGetValue(hit.name, out string saved) && saved != null)
            {
                to = saved;
            }

            edits.Add(new RenameEdit
            {
                from = hit.name,
                to = to,
                declareCount = hit.declareCount,
                referenceCount = hit.referenceCount
            });
        }
    }

    private void RefreshPreview()
    {
        List<StoryGraphVariableUtility.RenameRule> rules = new List<StoryGraphVariableUtility.RenameRule>();
        if (edits != null)
        {
            for (int i = 0; i < edits.Count; i++)
            {
                RenameEdit edit = edits[i];
                if (edit == null) continue;
                rules.Add(new StoryGraphVariableUtility.RenameRule
                {
                    from = edit.from,
                    to = edit.to
                });
            }
        }

        preview = StoryGraphVariableUtility.BuildPreview(targetGraphs, rules);
        Repaint();
    }

    private void SetStatus(string message, MessageType type)
    {
        statusMessage = message;
        statusType = type;
    }
}
