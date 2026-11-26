using UnityEngine;
#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
#endif

/// <summary>
/// 指定した StoryGraph の Label へ移動する。呼び出しは Button の OnClick などの Event から行う。
/// 説明は StoryGotoButton.Summary。
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("UI/Story Goto Button")]
public class StoryGotoButton : MonoBehaviour
{
    public const string Summary =
        "Goto を呼ぶと、指定した StoryGraph の Label へすぐに移動します。\n" +
        "ボタンのクリックには自動では登録しません。Button の OnClick など、Event から Goto を指定します。\n" +
        "Unlock と併用するときは、Lock がボタンを押せなくします。押したときの Event に、この Goto を登録します。\n" +
        "StoryGraph を割り当て、そのグラフにある Label を一覧から選びます。\n" +
        "Label の行先は識別子で保持するので、表示名を変えても維持されます。\n" +
        "StoryPlayer が空のときは、シーン内の StoryPlayer を見つけて設定します。";

    [Header("参照")]
    [Tooltip("空ならシーン内の StoryPlayer を使います")]
    [SerializeField] private StoryPlayer storyPlayer;

    [Header("行先")]
    [Tooltip("このグラフにある Label へ移動します")]
    [SerializeField] private StoryGraph targetGraph;

    [SerializeField] private string targetLabelGuid;

    /// <summary>
    /// 割り当てた Label へ移動する。Button の OnClick など、Event から呼ぶ。
    /// </summary>
    public void Goto()
    {
        EnsurePlayer();
        if (storyPlayer == null)
        {
            Debug.LogError("【GotoButton】StoryPlayer が見つかりません。");
            return;
        }

        if (targetGraph == null)
        {
            Debug.LogError("【GotoButton】StoryGraph が未設定です。");
            return;
        }

        if (StoryVisitButtonEntry.IsStartTarget(targetLabelGuid))
        {
            if (!storyPlayer.JumpToStart(targetGraph))
                Debug.LogError("【GotoButton】Start ノードが見つかりません。StoryGraph を確認してください。");
            return;
        }

        LabelNode label = targetGraph.FindLabel(targetLabelGuid);
        if (label == null)
        {
            Debug.LogError("【GotoButton】行先ラベルが見つかりません。StoryGraph と Label を確認してください。");
            return;
        }

        if (!storyPlayer.JumpFromOutside(label.GetDisplayName(), targetGraph))
        {
            Debug.LogWarning($"【GotoButton】行先 '{label.GetDisplayName()}' へジャンプできませんでした。");
        }
    }

    /// <summary>
    /// 行先を書き換える。クリックへの登録はしない。
    /// </summary>
    public void SetTarget(StoryGraph graph, string labelGuid)
    {
        targetGraph = graph;
        targetLabelGuid = labelGuid ?? "";
    }

    /// <summary>
    /// いまの行先が、指定したグラフと Label か。
    /// </summary>
    public bool MatchesTarget(StoryGraph graph, string labelGuid)
    {
        return targetGraph == graph && (targetLabelGuid ?? "") == (labelGuid ?? "");
    }

    private void EnsurePlayer()
    {
        if (storyPlayer != null) return;
#if UNITY_2023_1_OR_NEWER
        storyPlayer = FindFirstObjectByType<StoryPlayer>();
#else
        storyPlayer = FindObjectOfType<StoryPlayer>();
#endif
    }
}

#if UNITY_EDITOR
/// <summary>
/// StoryGotoButton のインスペクタ。説明と、割り当てた StoryGraph の Label 一覧を出す。複数選択に対応する。
/// </summary>
[CanEditMultipleObjects]
[CustomEditor(typeof(StoryGotoButton))]
public class StoryGotoButtonEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        AssignPlayerIfEmpty();
        EditorGUILayout.HelpBox(StoryGotoButton.Summary, MessageType.Info);

        SerializedProperty property = serializedObject.GetIterator();
        property.NextVisible(true);
        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.PropertyField(property);
        }

        while (property.NextVisible(false))
        {
            if (property.name == "targetLabelGuid")
            {
                DrawLabelPopup(property);
                continue;
            }

            EditorGUILayout.PropertyField(property, true);
        }

        serializedObject.ApplyModifiedProperties();
    }

    /// <summary>
    /// StoryPlayer が空なら、開いているシーンから見つけて入れる。プレハブ資産には書かない。
    /// </summary>
    private void AssignPlayerIfEmpty()
    {
        SerializedProperty playerProperty = serializedObject.FindProperty("storyPlayer");
        if (playerProperty == null || playerProperty.hasMultipleDifferentValues || playerProperty.objectReferenceValue != null)
            return;

        UnityEngine.Object[] targets = serializedObject.targetObjects;
        for (int i = 0; i < targets.Length; i++)
        {
            if (EditorUtility.IsPersistent(targets[i])) return;
            if (targets[i] is not Component component) return;
            if (!component.gameObject.scene.IsValid()) return;
            if (UnityEditor.SceneManagement.PrefabStageUtility.GetPrefabStage(component.gameObject) != null) return;
        }

        StoryPlayer found = FindScenePlayer();
        if (found == null) return;
        playerProperty.objectReferenceValue = found;
    }

    private static StoryPlayer FindScenePlayer()
    {
#if UNITY_2023_1_OR_NEWER
        return UnityEngine.Object.FindFirstObjectByType<StoryPlayer>();
#else
        return UnityEngine.Object.FindObjectOfType<StoryPlayer>();
#endif
    }

    private void DrawLabelPopup(SerializedProperty guidProperty)
    {
        SerializedProperty graphProperty = serializedObject.FindProperty("targetGraph");
        if (graphProperty != null && graphProperty.hasMultipleDifferentValues)
        {
            EditorGUILayout.PropertyField(guidProperty, new GUIContent("行先ラベル"));
            EditorGUILayout.HelpBox("StoryGraph が揃うと、その Label から選べます。", MessageType.Info);
            return;
        }

        StoryGraph graph = graphProperty != null
            ? graphProperty.objectReferenceValue as StoryGraph
            : null;

        string current = guidProperty.stringValue ?? "";
        List<string> guids = new List<string>();
        List<string> labels = new List<string>();
        StoryVisitButtonEntry.CollectLabelChoices(graph, guids, labels);

        if (guids.Count == 0)
        {
            EditorGUILayout.HelpBox("StoryGraph を指定すると、その中の Label から選べます。", MessageType.Info);
            return;
        }

        bool mixed = guidProperty.hasMultipleDifferentValues;
        if (!mixed)
        {
            if (string.IsNullOrEmpty(current))
            {
                guids.Insert(0, "");
                labels.Insert(0, "（未選択）");
            }
            else if (!guids.Contains(current))
            {
                string shortGuid = current.Length > 8 ? current.Substring(0, 8) : current;
                guids.Insert(0, current);
                labels.Insert(0, "(欠損) " + shortGuid);
            }
        }

        int index = 0;
        if (!mixed)
        {
            for (int i = 0; i < guids.Count; i++)
            {
                if (guids[i] == current)
                {
                    index = i;
                    break;
                }
            }
        }

        EditorGUI.showMixedValue = mixed;
        int next;
        bool changed;
        try
        {
            EditorGUI.BeginChangeCheck();
            next = EditorGUILayout.Popup(
                new GUIContent("行先ラベル", "Goto を呼んだときに移動する Label。"),
                index,
                labels.ToArray());
            changed = EditorGUI.EndChangeCheck();
        }
        finally
        {
            EditorGUI.showMixedValue = false;
        }

        if (changed && next >= 0 && next < guids.Count)
            guidProperty.stringValue = guids[next];
    }
}
#endif
