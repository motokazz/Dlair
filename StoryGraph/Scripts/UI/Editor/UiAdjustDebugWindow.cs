using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 実行中に選んだボタンを半透明で表示し、位置とサイズを外側のプレファブへ書き戻す。
/// カーソル属性は、選んだボタン1つを元のプレファブへ付ける。
/// </summary>
public class UiAdjustDebugWindow : EditorWindow
{
    const float DefaultAlpha = 0.5f;
    const float MinAlpha = 0.15f;
    const float MaxAlpha = 0.85f;
    const float PositionEpsilon = 0.05f;
    const float AnchorEpsilon = 0.0001f;

    [SerializeField] string editTargetId = "";
    [SerializeField] string editTargetPath = "";
    [SerializeField] bool debugVisible;
    [SerializeField] float debugAlpha = DefaultAlpha;

    GameObject editTarget;
    readonly Dictionary<EntityId, SavedVisual> visuals = new Dictionary<EntityId, SavedVisual>();
    readonly List<SnapshotEntry> snapshots = new List<SnapshotEntry>();
    readonly List<ButtonRow> rows = new List<ButtonRow>();
    readonly Dictionary<EntityId, string> attributeDrafts = new Dictionary<EntityId, string>();
    readonly List<GameObject> displayed = new List<GameObject>();
    static readonly Dictionary<EntityId, GameObject> clonePrefabCache = new Dictionary<EntityId, GameObject>();
    static readonly Dictionary<string, GameObject[]> prefabNameCache = new Dictionary<string, GameObject[]>();
    Vector2 scroll;
    Vector2 displayedScroll;
    bool displayedDirty = true;
    double nextDisplayedRefresh;
    string status = "";
    MessageType statusType = MessageType.Info;
    bool rowsDirty = true;
    bool applyingVisuals;
    bool visualQueued;
    bool writing;
    int changedCount;

    struct SavedVisual
    {
        public Color color;
        public Sprite sprite;
        public bool assignedSprite;
    }

    class ButtonRow
    {
        public GameObject gameObject;
        public string name;
        public string detail;
        public bool canWrite;
    }

    class RectSample
    {
        public string prefabPath;
        public string[] namePath;
        public int[] indexPath;
        public string name;
        public string rectId;
        public Vector2 anchorMin;
        public Vector2 anchorMax;
        public Vector2 pivot;
        public Vector2 sizeDelta;
        public Vector3 anchoredPosition3D;
        public Quaternion localRotation;
        public Vector3 localScale;
        public RectTransform rect;
        public RectTransform sourceRect;
        public bool sceneInstance;
        public string attributeName;
        public bool attributeChanged;
        public bool writeRect;
    }

    class SnapshotEntry
    {
        public RectTransform rect;
        public RectSnapshot shot;
    }

    struct RectSnapshot
    {
        public Vector2 anchorMin;
        public Vector2 anchorMax;
        public Vector2 pivot;
        public Vector2 sizeDelta;
        public Vector3 anchoredPosition3D;
        public Quaternion localRotation;
        public Vector3 localScale;
    }

    [MenuItem("Tools/UI/UI調整デバッグ")]
    static void Open()
    {
        UiAdjustDebugWindow window = GetWindow<UiAdjustDebugWindow>("UI調整デバッグ");
        window.minSize = new Vector2(440f, 520f);
        window.Show();
        window.Focus();
    }

    void OnEnable()
    {
        titleContent = new GUIContent("UI調整デバッグ");
        minSize = new Vector2(440f, 520f);
        debugAlpha = Mathf.Clamp(debugAlpha <= 0f ? DefaultAlpha : debugAlpha, MinAlpha, MaxAlpha);
        EditorApplication.playModeStateChanged += OnPlayMode;
        EditorApplication.hierarchyChanged += OnHierarchy;
        EditorApplication.update += OnEditorUpdate;
        rowsDirty = true;
        displayedDirty = true;
        if (debugVisible && EditorApplication.isPlaying)
            ApplyVisuals();
    }

    void OnDisable()
    {
        EditorApplication.playModeStateChanged -= OnPlayMode;
        EditorApplication.hierarchyChanged -= OnHierarchy;
        EditorApplication.update -= OnEditorUpdate;
        EditorApplication.delayCall -= ApplyQueuedVisuals;
        visualQueued = false;
        RestoreVisuals();
    }

    void OnPlayMode(PlayModeStateChange change)
    {
        rowsDirty = true;
        displayedDirty = true;
        if (change == PlayModeStateChange.EnteredPlayMode && debugVisible)
            ApplyVisuals();
        if (change == PlayModeStateChange.ExitingPlayMode)
            RestoreVisuals();
        Repaint();
    }

    void OnHierarchy()
    {
        rowsDirty = true;
        displayedDirty = true;
        QueueVisuals();
        Repaint();
    }

    void OnEditorUpdate()
    {
        if (EditorApplication.timeSinceStartup >= nextDisplayedRefresh)
        {
            nextDisplayedRefresh = EditorApplication.timeSinceStartup + 0.5d;
            displayedDirty = true;
            Repaint();
        }

        if (!EditorApplication.isPlaying)
        {
            if (clonePrefabCache.Count > 0) clonePrefabCache.Clear();
            if (prefabNameCache.Count > 0) prefabNameCache.Clear();
            if (changedCount != 0)
            {
                changedCount = 0;
                Repaint();
            }

            return;
        }

        if (debugVisible)
            ApplyVisuals();
        RefreshChangedCount();
    }

    void OnGUI()
    {
        if (displayedDirty)
        {
            RefreshDisplayed();
            displayedDirty = false;
        }

        if (ResolveTarget() == null && displayed.Count == 1)
            SetTarget(displayed[0]);

        if (rowsDirty)
        {
            RefreshRows();
            rowsDirty = false;
        }

        EditorGUILayout.Space(6f);
        EditorGUILayout.HelpBox(
            "表示中の Canvas か Panel を押して監視対象にします。カーソル属性を変えると、実行中は書き戻しが押せます。位置とサイズは、デバッグ表示を付けてから直すと書き戻しが押せます。",
            MessageType.Info);

        DrawTargetField();
        DrawDebugToggle();
        DrawRows();
        DrawStatus();
        DrawWriteButton();
    }

    void DrawTargetField()
    {
        GameObject current = ResolveTarget();
        EditorGUI.BeginChangeCheck();
        GameObject picked = (GameObject)EditorGUILayout.ObjectField("監視対象", current, typeof(GameObject), true);
        if (EditorGUI.EndChangeCheck())
        {
            if (picked != null && EditorUtility.IsPersistent(picked))
                SetStatus("シーンに置いてあるオブジェクトを選んでください。", MessageType.Warning);
            else
                SetTarget(picked);
        }

        EditorGUILayout.LabelField("監視対象", displayed.Count + " 件");
        displayedScroll = EditorGUILayout.BeginScrollView(displayedScroll, GUILayout.MaxHeight(140f));
        if (displayed.Count == 0)
            EditorGUILayout.LabelField("表示中の Canvas か Panel がありません。");

        for (int i = 0; i < displayed.Count; i++)
        {
            GameObject candidate = displayed[i];
            if (candidate == null) continue;
            bool selected = candidate == current;
            string label = selected ? DisplayLabel(candidate) + "（監視中）" : DisplayLabel(candidate);
            if (!GUILayout.Button(label, EditorStyles.miniButton)) continue;
            Selection.activeGameObject = candidate;
            EditorGUIUtility.PingObject(candidate);
            if (!selected)
                SetTarget(candidate);
        }

        EditorGUILayout.EndScrollView();
    }

    void DrawDebugToggle()
    {
        EditorGUI.BeginChangeCheck();
        debugVisible = EditorGUILayout.ToggleLeft("デバッグ表示", debugVisible);
        if (EditorGUI.EndChangeCheck())
        {
            if (debugVisible && EditorApplication.isPlaying)
                ApplyVisuals();
            else
                RestoreVisuals();
            rowsDirty = true;
        }

        EditorGUI.BeginChangeCheck();
        debugAlpha = EditorGUILayout.Slider("表示アルファ", debugAlpha, MinAlpha, MaxAlpha);
        if (EditorGUI.EndChangeCheck() && debugVisible && EditorApplication.isPlaying)
            ApplyVisuals();

        if (!EditorApplication.isPlaying)
            EditorGUILayout.HelpBox("ゲームを実行すると、デバッグ表示のボタンが半透明になります。", MessageType.None);
        else if (!debugVisible)
            EditorGUILayout.HelpBox("デバッグ表示を付けると、半透明になります。", MessageType.None);
        else if (changedCount == 0)
            EditorGUILayout.HelpBox("位置、サイズ、カーソル属性を変えると、書き戻しが押せます。", MessageType.None);
    }

    void DrawRows()
    {
        int writable = 0;
        for (int i = 0; i < rows.Count; i++)
        {
            if (rows[i].canWrite) writable++;
        }

        EditorGUILayout.LabelField("ボタン", rows.Count + " 件（書き戻せる " + writable + " 件）");
        EditorGUILayout.LabelField("各行の属性名を変えると、実行中は書き戻しが押せます。空にすると、カーソル属性を外します。", EditorStyles.miniLabel);
        scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.ExpandHeight(true));
        if (ResolveTarget() == null)
            EditorGUILayout.LabelField("監視対象を選んでください。");
        else if (rows.Count == 0)
            EditorGUILayout.LabelField("配下にボタンがありません。");

        for (int i = 0; i < rows.Count; i++)
        {
            ButtonRow row = rows[i];
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(row.name, EditorStyles.miniButton, GUILayout.MinWidth(140f), GUILayout.MaxWidth(240f)))
                {
                    if (row.gameObject != null)
                    {
                        Selection.activeGameObject = row.gameObject;
                        EditorGUIUtility.PingObject(row.gameObject);
                    }
                }

                string currentAttribute = AttributeName(row.gameObject);
                EntityId id = row.gameObject != null ? row.gameObject.GetEntityId() : default;
                string shown = currentAttribute;
                if (row.gameObject != null && attributeDrafts.TryGetValue(id, out string draft))
                    shown = draft;
                string edited = EditorGUILayout.TextField(shown, GUILayout.Width(110f));
                if (row.gameObject != null && edited != shown)
                {
                    attributeDrafts[id] = edited;
                    RefreshChangedCount();
                }
                string action = string.IsNullOrEmpty(edited) ? "削除" : "付与";
                if (GUILayout.Button(action, EditorStyles.miniButton, GUILayout.Width(44f)))
                    AssignCursorAttribute(row.gameObject, edited);

                EditorGUILayout.LabelField(row.detail, EditorStyles.miniLabel);
            }
        }

        EditorGUILayout.EndScrollView();
    }

    static string AttributeName(GameObject button)
    {
        if (button == null) return "";
        UiCursorAttribute attribute = button.GetComponent<UiCursorAttribute>();
        if (attribute == null || attribute.attributeName == null) return "";
        return attribute.attributeName;
    }

    void AssignCursorAttribute(GameObject buttonObject, string attributeName)
    {
        if (buttonObject == null)
        {
            SetStatus("ボタンを選んでください。", MessageType.Warning);
            return;
        }

        attributeName = attributeName == null ? "" : attributeName.Trim();
        Button button = buttonObject.GetComponent<Button>();
        RectSample sample;
        string reason;
        if (!TrySample(button, ResolveTarget(), out sample, out reason))
        {
            SetStatus(buttonObject.name + " : " + reason, MessageType.Warning);
            return;
        }

        if (AttributeName(buttonObject) == attributeName)
        {
            attributeDrafts.Remove(buttonObject.GetEntityId());
            rowsDirty = true;
            SetStatus(attributeName.Length == 0
                ? buttonObject.name + " にカーソル属性はありません。"
                : buttonObject.name + " はすでに「" + attributeName + "」です。", MessageType.Info);
            return;
        }

        if (attributeName.Length == 0)
            RemoveLiveAttribute(buttonObject);
        else
            SetLiveAttribute(buttonObject, attributeName);

        attributeDrafts.Remove(buttonObject.GetEntityId());
        rowsDirty = true;
        RefreshChangedCount();
        string next = attributeName.Length == 0
            ? buttonObject.name + " のカーソル属性を外しました。書き戻しでプレファブから消します。"
            : buttonObject.name + " の属性を「" + attributeName + "」にしました。書き戻しでプレファブへ書けます。";
        if (!EditorApplication.isPlaying)
            next += " ゲームの実行中にしてください。";
        SetStatus(next, MessageType.Info);
    }

    static void SetLiveAttribute(GameObject buttonObject, string attributeName)
    {
        UiCursorAttribute live = buttonObject.GetComponent<UiCursorAttribute>();
        if (live == null)
            live = buttonObject.AddComponent<UiCursorAttribute>();
        live.attributeName = attributeName;
    }

    static void RemoveLiveAttribute(GameObject buttonObject)
    {
        UiCursorAttribute live = buttonObject.GetComponent<UiCursorAttribute>();
        if (live == null) return;
        UnityEngine.Object.DestroyImmediate(live);
    }

    string IntendedAttribute(GameObject button)
    {
        if (button != null && attributeDrafts.TryGetValue(button.GetEntityId(), out string draft))
            return draft == null ? "" : draft.Trim();
        return AttributeName(button);
    }

    static string SourceAttributeName(RectTransform source)
    {
        if (source == null) return "";
        return AttributeName(source.gameObject);
    }

    void DrawStatus()
    {
        if (string.IsNullOrEmpty(status)) return;
        EditorGUILayout.HelpBox(status, statusType);
    }

    void DrawWriteButton()
    {
        bool canPress = EditorApplication.isPlaying && changedCount > 0 && !writing;
        using (new EditorGUI.DisabledScope(!canPress))
        {
            string label = changedCount > 0 ? "プレファブへ書き戻す (" + changedCount + ")" : "プレファブへ書き戻す";
            if (GUILayout.Button(label, GUILayout.Height(32f)))
                WriteBack();
        }
    }

    void RefreshChangedCount()
    {
        int next = 0;
        GameObject target = ResolveTarget();
        if (target != null)
        {
            Button[] buttons = target.GetComponentsInChildren<Button>(true);
            for (int i = 0; i < buttons.Length; i++)
            {
                RectTransform rect = buttons[i].transform as RectTransform;
                if (rect == null) continue;
                RememberSnapshot(rect);
                RectTransform source = CorrespondingRect(rect);
                if (source == null) continue;
                bool rectChanged = debugVisible && ChangedSinceSnapshot(rect);
                bool attributeChanged = IntendedAttribute(buttons[i].gameObject) != SourceAttributeName(source);
                if (!rectChanged && !attributeChanged) continue;
                next++;
            }
        }

        if (next == changedCount) return;
        changedCount = next;
        Repaint();
    }

    void SetTarget(GameObject target)
    {
        RestoreVisuals();
        editTarget = target;
        if (target == null)
        {
            editTargetId = "";
            editTargetPath = "";
        }
        else
        {
            editTargetId = GlobalObjectId.GetGlobalObjectIdSlow(target).ToString();
            editTargetPath = HierarchyPath(target);
        }

        snapshots.Clear();
        changedCount = 0;
        rowsDirty = true;
        SetStatus("", MessageType.Info);
        if (debugVisible && EditorApplication.isPlaying)
            ApplyVisuals();
    }

    GameObject ResolveTarget()
    {
        if (editTarget != null) return editTarget;
        if (!string.IsNullOrEmpty(editTargetId) && GlobalObjectId.TryParse(editTargetId, out GlobalObjectId id))
            editTarget = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(id) as GameObject;
        if (editTarget == null)
            editTarget = FindByHierarchyPath(editTargetPath);
        return editTarget;
    }

    void RefreshDisplayed()
    {
        displayed.Clear();
        Button[] buttons = UnityEngine.Object.FindObjectsByType<Button>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < buttons.Length; i++)
        {
            if (!IsShownButton(buttons[i])) continue;
            AddWatchParents(buttons[i].transform);
        }

        displayed.Sort(CompareDisplayed);
    }

    void AddWatchParents(Transform start)
    {
        Transform current = start.parent;
        while (current != null)
        {
            if (IsWatchParent(current.gameObject) && !displayed.Contains(current.gameObject))
                displayed.Add(current.gameObject);
            current = current.parent;
        }
    }

    static bool IsWatchParent(GameObject gameObject)
    {
        if (gameObject == null || !gameObject.activeInHierarchy) return false;
        if (gameObject.GetComponent<Canvas>() != null) return true;
        string name = gameObject.name;
        if (name.IndexOf("Canvas", StringComparison.OrdinalIgnoreCase) >= 0) return true;
        return name.IndexOf("Panel", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    static int CompareDisplayed(GameObject a, GameObject b)
    {
        if (a == null && b == null) return 0;
        if (a == null) return 1;
        if (b == null) return -1;
        return string.Compare(DisplayLabel(a), DisplayLabel(b), StringComparison.Ordinal);
    }

    static string DisplayLabel(GameObject gameObject)
    {
        Transform parent = gameObject.transform.parent;
        if (parent == null) return gameObject.name;
        return parent.name + "/" + gameObject.name;
    }

    static bool IsShownButton(Button button)
    {
        if (button == null || !button.gameObject.activeInHierarchy) return false;
        RectTransform rect = button.transform as RectTransform;
        if (rect == null) return false;
        return IsShown(button.gameObject);
    }

    static bool IsShown(GameObject gameObject)
    {
        if (gameObject == null || !gameObject.activeInHierarchy) return false;
        Canvas nearest = null;
        Transform current = gameObject.transform;
        while (current != null)
        {
            if (nearest == null)
            {
                Canvas canvas = current.GetComponent<Canvas>();
                if (canvas != null)
                    nearest = canvas;
            }

            CanvasGroup group = current.GetComponent<CanvasGroup>();
            if (group != null && group.alpha <= 0.001f)
                return false;
            current = current.parent;
        }

        return nearest != null && nearest.isActiveAndEnabled;
    }

    void RefreshRows()
    {
        rows.Clear();
        GameObject target = ResolveTarget();
        if (target == null) return;

        Button[] buttons = target.GetComponentsInChildren<Button>(true);
        for (int i = 0; i < buttons.Length; i++)
        {
            Button button = buttons[i];
            if (button == null) continue;
            ButtonRow row = new ButtonRow();
            row.gameObject = button.gameObject;
            row.name = button.gameObject.activeInHierarchy ? button.name : button.name + "（非表示）";
            string reason;
            RectSample sample;
            row.canWrite = TrySample(button, target, out sample, out reason);
            string attribute = AttributeName(button.gameObject);
            row.detail = attribute.Length == 0 ? reason : reason + " / " + attribute;
            rows.Add(row);
        }
    }

    void QueueVisuals()
    {
        if (visualQueued || applyingVisuals) return;
        if (!debugVisible || !EditorApplication.isPlaying) return;
        visualQueued = true;
        EditorApplication.delayCall += ApplyQueuedVisuals;
    }

    void ApplyQueuedVisuals()
    {
        visualQueued = false;
        if (this == null) return;
        ApplyVisuals();
        Repaint();
    }

    void ApplyVisuals()
    {
        if (applyingVisuals) return;
        if (!debugVisible || !EditorApplication.isPlaying) return;
        GameObject target = ResolveTarget();
        if (target == null) return;

        applyingVisuals = true;
        try
        {
            PruneVisuals();
            debugAlpha = Mathf.Clamp(debugAlpha, MinAlpha, MaxAlpha);
            Button[] buttons = target.GetComponentsInChildren<Button>(true);
            for (int i = 0; i < buttons.Length; i++)
            {
                RectTransform rect = buttons[i].transform as RectTransform;
                if (rect != null)
                    RememberSnapshot(rect);

                Image image = ButtonImage(buttons[i]);
                if (image == null) continue;
                EntityId id = image.GetEntityId();
                SavedVisual saved;
                if (!visuals.TryGetValue(id, out saved))
                {
                    saved.color = image.color;
                    saved.sprite = image.sprite;
                    saved.assignedSprite = image.sprite == null;
                    if (saved.assignedSprite)
                        image.sprite = WhiteSprite();
                    visuals[id] = saved;
                }
                else if (saved.assignedSprite && image.sprite == null)
                {
                    image.sprite = WhiteSprite();
                }

                Color next = saved.color;
                next.a = debugAlpha;
                if (image.color != next)
                    image.color = next;
            }
        }
        finally
        {
            applyingVisuals = false;
        }
    }

    void RestoreVisuals()
    {
        foreach (KeyValuePair<EntityId, SavedVisual> pair in visuals)
        {
            Image image = EditorUtility.EntityIdToObject(pair.Key) as Image;
            if (image == null) continue;
            if (pair.Value.assignedSprite)
                image.sprite = pair.Value.sprite;
            image.color = pair.Value.color;
        }

        visuals.Clear();
        snapshots.Clear();
    }

    void PruneVisuals()
    {
        List<EntityId> missing = null;
        foreach (KeyValuePair<EntityId, SavedVisual> pair in visuals)
        {
            if (EditorUtility.EntityIdToObject(pair.Key) != null) continue;
            if (missing == null) missing = new List<EntityId>();
            missing.Add(pair.Key);
        }

        if (missing == null) return;
        for (int i = 0; i < missing.Count; i++)
            visuals.Remove(missing[i]);
    }

    void RememberSnapshot(RectTransform rect)
    {
        if (rect == null || FindSnapshot(rect) != null) return;
        snapshots.Add(new SnapshotEntry { rect = rect, shot = Capture(rect) });
    }

    bool ChangedSinceSnapshot(RectTransform rect)
    {
        SnapshotEntry entry = FindSnapshot(rect);
        if (entry == null || entry.rect == null) return false;
        return !SameSnapshot(entry.shot, entry.rect);
    }

    SnapshotEntry FindSnapshot(RectTransform rect)
    {
        for (int i = snapshots.Count - 1; i >= 0; i--)
        {
            if (snapshots[i].rect == null)
            {
                snapshots.RemoveAt(i);
                continue;
            }

            if (snapshots[i].rect == rect) return snapshots[i];
        }

        return null;
    }

    void CommitSnapshots(List<RectSample> written)
    {
        for (int i = 0; i < written.Count; i++)
        {
            RectTransform rect = written[i].rect;
            if (rect == null) continue;
            SnapshotEntry entry = FindSnapshot(rect);
            if (entry == null)
            {
                snapshots.Add(new SnapshotEntry { rect = rect, shot = Capture(rect) });
                continue;
            }

            entry.shot = Capture(rect);
        }
    }

    static RectSnapshot Capture(RectTransform rect)
    {
        RectSnapshot shot = new RectSnapshot();
        shot.anchorMin = rect.anchorMin;
        shot.anchorMax = rect.anchorMax;
        shot.pivot = rect.pivot;
        shot.sizeDelta = rect.sizeDelta;
        shot.anchoredPosition3D = rect.anchoredPosition3D;
        shot.localRotation = rect.localRotation;
        shot.localScale = rect.localScale;
        return shot;
    }

    static bool SameSnapshot(RectSnapshot shot, RectTransform rect)
    {
        return Near(shot.anchorMin, rect.anchorMin, AnchorEpsilon)
            && Near(shot.anchorMax, rect.anchorMax, AnchorEpsilon)
            && Near(shot.pivot, rect.pivot, AnchorEpsilon)
            && Near(shot.sizeDelta, rect.sizeDelta, PositionEpsilon)
            && Near(shot.anchoredPosition3D, rect.anchoredPosition3D, PositionEpsilon)
            && Near(shot.localScale, rect.localScale, PositionEpsilon)
            && Quaternion.Angle(shot.localRotation, rect.localRotation) < PositionEpsilon;
    }

    void WriteBack()
    {
        if (writing) return;
        if (!EditorApplication.isPlaying) return;

        GameObject target = ResolveTarget();
        if (target == null)
        {
            SetStatus("監視対象を選んでください。", MessageType.Warning);
            return;
        }

        List<RectSample> samples = new List<RectSample>();
        List<string> skipped = new List<string>();
        Button[] buttons = target.GetComponentsInChildren<Button>(true);
        for (int i = 0; i < buttons.Length; i++)
        {
            Button button = buttons[i];
            if (button == null) continue;
            RectSample sample;
            string reason;
            if (!TrySample(button, target, out sample, out reason))
            {
                skipped.Add(button.name + " : " + reason);
                continue;
            }

            sample.attributeName = IntendedAttribute(button.gameObject);
            sample.attributeChanged = sample.attributeName != SourceAttributeName(sample.sourceRect);
            sample.writeRect = debugVisible && ChangedSinceSnapshot(sample.rect);
            if (!sample.writeRect && !sample.attributeChanged)
                continue;

            samples.Add(sample);
        }

        if (samples.Count == 0)
        {
            string empty = skipped.Count == 0
                ? "デバッグ表示のあとで、位置かサイズが変わったボタンがありません。"
                : "変わったボタンがありません。\n" + JoinLines(skipped);
            SetStatus(empty, skipped.Count == 0 ? MessageType.Info : MessageType.Warning);
            return;
        }

        string message = BuildConfirmMessage(samples, skipped);
        if (!EditorUtility.DisplayDialog("プレファブへ書き戻す", message, "書き戻す", "キャンセル"))
            return;

        for (int i = 0; i < samples.Count; i++)
        {
            RectSample sample = samples[i];
            if (!sample.attributeChanged || sample.rect == null) continue;
            if (string.IsNullOrEmpty(sample.attributeName))
                RemoveLiveAttribute(sample.rect.gameObject);
            else
                SetLiveAttribute(sample.rect.gameObject, sample.attributeName);
            attributeDrafts.Remove(sample.rect.gameObject.GetEntityId());
        }

        writing = true;
        try
        {
            Dictionary<string, List<RectSample>> byPrefab = GroupByPrefab(samples);
            List<string> writtenNames = new List<string>();
            List<RectSample> writtenSamples = new List<RectSample>();
            List<UiAdjustDebugSceneRevert.PendingRect> pending = new List<UiAdjustDebugSceneRevert.PendingRect>();
            List<string> errors = new List<string>();
            int unchanged = 0;
            string[] paths = new string[byPrefab.Count];
            byPrefab.Keys.CopyTo(paths, 0);

            try
            {
                for (int i = 0; i < paths.Length; i++)
                {
                    EditorUtility.DisplayProgressBar("プレファブへ書き戻す", Path.GetFileName(paths[i]), (float)i / paths.Length);
                    WritePrefab(paths[i], byPrefab[paths[i]], writtenNames, writtenSamples, pending, errors, ref unchanged);
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            if (writtenNames.Count > 0)
                AssetDatabase.SaveAssets();
            if (pending.Count > 0)
                UiAdjustDebugSceneRevert.Remember(pending);
            if (writtenSamples.Count > 0)
                CommitSnapshots(writtenSamples);

            ApplyVisuals();
            rowsDirty = true;
            SetStatus(BuildResultMessage(writtenNames, unchanged, errors, pending.Count), errors.Count > 0 ? MessageType.Warning : MessageType.Info);
        }
        finally
        {
            writing = false;
        }
    }

    static void WritePrefab(
        string path,
        List<RectSample> samples,
        List<string> writtenNames,
        List<RectSample> writtenSamples,
        List<UiAdjustDebugSceneRevert.PendingRect> pending,
        List<string> errors,
        ref int unchanged)
    {
        List<RectSample> changed = new List<RectSample>();
        try
        {
            for (int i = 0; i < samples.Count; i++)
            {
                RectSample sample = samples[i];
                if (sample.sourceRect == null)
                {
                    errors.Add(sample.name + " : プレファブ上に同じボタンが無い");
                    continue;
                }

                bool rectSame = !sample.writeRect || SameRect(sample.sourceRect, sample);
                bool attributeSame = sample.attributeName == SourceAttributeName(sample.sourceRect);
                if (rectSame && attributeSame)
                {
                    unchanged++;
                    RememberSceneRevert(pending, sample);
                    continue;
                }

                if (sample.writeRect && !SameRect(sample.sourceRect, sample))
                {
                    ApplySample(sample, sample.sourceRect);
                    EditorUtility.SetDirty(sample.sourceRect);
                }

                if (!attributeSame)
                    ApplySourceAttribute(sample);
                changed.Add(sample);
            }

            if (changed.Count == 0) return;

            GameObject prefabRoot = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefabRoot == null)
            {
                errors.Add(Path.GetFileName(path) + " : プレファブを開けない");
                return;
            }

            PrefabUtility.SavePrefabAsset(prefabRoot);
            string fileName = Path.GetFileName(path);
            for (int i = 0; i < changed.Count; i++)
            {
                writtenNames.Add(changed[i].name + " → " + fileName);
                writtenSamples.Add(changed[i]);
                RememberSceneRevert(pending, changed[i]);
            }
        }
        catch (Exception exception)
        {
            errors.Add(Path.GetFileName(path) + " : " + exception.Message);
        }
    }

    static void RememberSceneRevert(List<UiAdjustDebugSceneRevert.PendingRect> pending, RectSample sample)
    {
        if (!sample.sceneInstance || string.IsNullOrEmpty(sample.rectId)) return;
        pending.Add(new UiAdjustDebugSceneRevert.PendingRect { id = sample.rectId, name = sample.name });
    }

    static bool TrySample(Button button, GameObject editTarget, out RectSample sample, out string reason)
    {
        sample = null;
        reason = "";
        if (button == null)
        {
            reason = "ボタンが無い";
            return false;
        }

        RectTransform rect = button.transform as RectTransform;
        if (rect == null)
        {
            reason = "RectTransform が無い";
            return false;
        }

        if (!TryCorrespondingRect(rect, out RectTransform source, out reason))
            return false;

        string path = AssetDatabase.GetAssetPath(source);
        if (string.IsNullOrEmpty(path) || !path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
        {
            reason = "プレファブ資産ではない";
            return false;
        }

        if (PrefabUtility.IsPartOfImmutablePrefab(source))
        {
            reason = "編集できないプレファブ";
            return false;
        }

        sample = new RectSample();
        sample.prefabPath = path;
        sample.name = button.name;
        sample.rectId = IsSavedSceneObject(rect) ? GlobalObjectId.GetGlobalObjectIdSlow(rect).ToString() : "";
        sample.anchorMin = rect.anchorMin;
        sample.anchorMax = rect.anchorMax;
        sample.pivot = rect.pivot;
        sample.sizeDelta = rect.sizeDelta;
        sample.anchoredPosition3D = rect.anchoredPosition3D;
        sample.localRotation = rect.localRotation;
        sample.localScale = rect.localScale;
        sample.rect = rect;
        sample.sourceRect = source;
        sample.sceneInstance = sample.rectId.Length > 0;
        sample.attributeName = AttributeName(button.gameObject);
        sample.attributeChanged = sample.attributeName != SourceAttributeName(source);
        reason = Path.GetFileName(path);
        return true;
    }

    static RectTransform CorrespondingRect(RectTransform rect)
    {
        RectTransform source;
        string reason;
        TryCorrespondingRect(rect, out source, out reason);
        return source;
    }

    static bool TryCorrespondingRect(RectTransform rect, out RectTransform source, out string reason)
    {
        source = null;
        reason = "";
        RectTransform linked = LinkedPrefabRect(rect);
        if (linked != null)
        {
            source = linked;
            return true;
        }

        Transform clone = SpawnedRoot(rect);
        if (clone == null)
        {
            reason = "プレファブに無い";
            return false;
        }

        GameObject prefab = PrefabForClone(clone.gameObject, out reason);
        if (prefab == null) return false;

        Transform found = FindByNamePath(prefab.transform, NamePath(clone, rect));
        RectTransform foundRect = found as RectTransform;
        if (foundRect == null)
        {
            reason = "プレファブに同じボタンが無い";
            return false;
        }

        source = foundRect;
        return true;
    }

    static RectTransform LinkedPrefabRect(RectTransform rect)
    {
        RectTransform source = PrefabAssetRect(PrefabUtility.GetCorrespondingObjectFromSource(rect) as RectTransform);
        if (source != null) return source;
        source = PrefabAssetRect(PrefabUtility.GetCorrespondingObjectFromOriginalSource(rect) as RectTransform);
        if (source != null) return source;

        Transform current = rect.parent;
        while (current != null)
        {
            Transform sourceParent = PrefabUtility.GetCorrespondingObjectFromSource(current) as Transform;
            if (sourceParent == null)
                sourceParent = PrefabUtility.GetCorrespondingObjectFromOriginalSource(current) as Transform;
            sourceParent = PrefabAssetTransform(sourceParent);
            if (sourceParent != null)
            {
                Transform found = FindByNamePath(sourceParent, NamePath(current, rect));
                RectTransform foundRect = PrefabAssetRect(found as RectTransform);
                if (foundRect != null) return foundRect;
            }

            current = current.parent;
        }

        return null;
    }

    static RectTransform PrefabAssetRect(RectTransform rect)
    {
        if (rect == null) return null;
        string path = AssetDatabase.GetAssetPath(rect);
        if (string.IsNullOrEmpty(path) || !path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
            return null;
        return rect;
    }

    static Transform PrefabAssetTransform(Transform source)
    {
        if (source == null) return null;
        string path = AssetDatabase.GetAssetPath(source);
        if (string.IsNullOrEmpty(path) || !path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
            return null;
        return source;
    }

    static Transform SpawnedRoot(Transform node)
    {
        Transform current = node;
        while (current != null)
        {
            if (current.name.Contains("(Clone)")) return current;
            PooledObject pooled = current.GetComponent<PooledObject>();
            if (pooled != null && pooled.OriginPrefab != null) return current;
            current = current.parent;
        }

        return null;
    }

    static GameObject PrefabForClone(GameObject clone, out string reason)
    {
        reason = "";
        EntityId id = clone.GetEntityId();
        GameObject cached;
        if (clonePrefabCache.TryGetValue(id, out cached) && cached != null)
            return cached;

        PooledObject pooled = clone.GetComponent<PooledObject>();
        if (pooled != null && pooled.OriginPrefab != null)
        {
            clonePrefabCache[id] = pooled.OriginPrefab;
            return pooled.OriginPrefab;
        }

        string prefabName = StripCloneSuffix(clone.name);
        if (string.IsNullOrEmpty(prefabName))
        {
            reason = "プレファブに無い";
            return null;
        }

        GameObject instanceMatch = null;
        List<GameObject> playing = new List<GameObject>();
        CollectPlayingPrefabs(clone, prefabName, playing, ref instanceMatch);
        GameObject resolved = null;
        if (instanceMatch != null)
            resolved = instanceMatch;
        else if (playing.Count == 1)
            resolved = playing[0];
        else if (playing.Count > 1)
            resolved = PreferCurrentNode(playing, prefabName);

        if (resolved == null && playing.Count > 1)
            resolved = PickByHierarchy(playing, clone.transform);

        if (resolved == null && playing.Count == 0)
        {
            GameObject[] assets = FindPrefabsNamed(prefabName);
            if (assets.Length == 1)
                resolved = assets[0];
            else if (assets.Length > 1)
                resolved = PickByHierarchy(assets, clone.transform);
        }

        if (resolved == null)
        {
            reason = playing.Count > 1 || FindPrefabsNamed(prefabName).Length > 1
                ? "同名のプレファブが複数ある"
                : "元のプレファブが見つからない";
            return null;
        }

        clonePrefabCache[id] = resolved;
        return resolved;
    }

    static GameObject PreferCurrentNode(List<GameObject> playing, string prefabName)
    {
        StoryPlayer[] players = UnityEngine.Object.FindObjectsByType<StoryPlayer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < players.Length; i++)
        {
            GameObject prefab = NodePrefab(players[i].CurrentNode);
            if (prefab == null || StripCloneSuffix(prefab.name) != prefabName) continue;
            if (playing.Contains(prefab)) return prefab;
        }

        return null;
    }

    static void CollectPlayingPrefabs(GameObject clone, string prefabName, List<GameObject> matches, ref GameObject instanceMatch)
    {
        StoryPlayer[] players = UnityEngine.Object.FindObjectsByType<StoryPlayer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        HashSet<StoryGraph> visited = new HashSet<StoryGraph>();
        for (int i = 0; i < players.Length; i++)
        {
            ConsiderNode(players[i].CurrentNode, clone, prefabName, matches, ref instanceMatch);
            CollectGraph(players[i].graph, visited, clone, prefabName, matches, ref instanceMatch);
            CollectGraph(players[i].ActiveGraph, visited, clone, prefabName, matches, ref instanceMatch);
        }
    }

    static void CollectGraph(
        StoryGraph graph,
        HashSet<StoryGraph> visited,
        GameObject clone,
        string prefabName,
        List<GameObject> matches,
        ref GameObject instanceMatch)
    {
        if (graph == null || !visited.Add(graph) || graph.nodes == null) return;
        for (int i = 0; i < graph.nodes.Count; i++)
        {
            BaseNode node = graph.nodes[i];
            ConsiderNode(node, clone, prefabName, matches, ref instanceMatch);
            if (node is SubGraphNode subGraph)
                CollectGraph(subGraph.subGraph, visited, clone, prefabName, matches, ref instanceMatch);
            else if (node is GotoNode gotoNode)
                CollectGraph(gotoNode.targetGraph, visited, clone, prefabName, matches, ref instanceMatch);
            else if (node is ConditionMultiNode condition && condition.conditions != null)
            {
                for (int c = 0; c < condition.conditions.Count; c++)
                {
                    ConditionMultiEntry entry = condition.conditions[c];
                    if (entry == null) continue;
                    CollectGraph(entry.visitGraph, visited, clone, prefabName, matches, ref instanceMatch);
                }
            }
        }
    }

    static void ConsiderNode(BaseNode node, GameObject clone, string prefabName, List<GameObject> matches, ref GameObject instanceMatch)
    {
        GameObject prefab = NodePrefab(node);
        if (prefab == null) return;
        GameObject spawned = SpawnedInstance(node);
        if (spawned == clone)
            instanceMatch = prefab;
        if (StripCloneSuffix(prefab.name) != prefabName) return;
        if (!matches.Contains(prefab)) matches.Add(prefab);
    }

    static GameObject NodePrefab(BaseNode node)
    {
        ChoiceNode choice = node as ChoiceNode;
        if (choice != null) return choice.branchUIPrefab;
        TextNode text = node as TextNode;
        if (text != null) return text.textUIPrefab;
        VariableTextNode variableText = node as VariableTextNode;
        if (variableText != null) return variableText.textUIPrefab;
        SpawnPrefabNode spawn = node as SpawnPrefabNode;
        if (spawn != null) return spawn.prefab;
        return null;
    }

    static GameObject SpawnedInstance(BaseNode node)
    {
        if (node == null) return null;
        FieldInfo field = node.GetType().GetField("currentUIInstance", BindingFlags.Instance | BindingFlags.NonPublic);
        if (field == null) return null;
        return field.GetValue(node) as GameObject;
    }

    static GameObject PickByHierarchy(IList<GameObject> prefabs, Transform clone)
    {
        GameObject match = null;
        for (int i = 0; i < prefabs.Count; i++)
        {
            GameObject prefab = prefabs[i];
            if (prefab == null || !SameHierarchy(prefab.transform, clone)) continue;
            if (match != null && match != prefab) return null;
            match = prefab;
        }

        return match;
    }

    static bool SameHierarchy(Transform prefab, Transform instance)
    {
        if (prefab.childCount != instance.childCount) return false;
        for (int i = 0; i < prefab.childCount; i++)
        {
            Transform prefabChild = prefab.GetChild(i);
            Transform instanceChild = instance.GetChild(i);
            if (prefabChild.name != StripCloneSuffix(instanceChild.name)) return false;
            if (!SameHierarchy(prefabChild, instanceChild)) return false;
        }

        return true;
    }

    static GameObject[] FindPrefabsNamed(string prefabName)
    {
        GameObject[] cached;
        if (prefabNameCache.TryGetValue(prefabName, out cached)) return cached;

        List<GameObject> found = new List<GameObject>();
        string[] guids = AssetDatabase.FindAssets("t:Prefab " + prefabName);
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            if (!string.Equals(Path.GetFileNameWithoutExtension(path), prefabName, StringComparison.Ordinal))
                continue;
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null) found.Add(prefab);
        }

        cached = found.ToArray();
        prefabNameCache[prefabName] = cached;
        return cached;
    }

    static string StripCloneSuffix(string name)
    {
        if (string.IsNullOrEmpty(name)) return "";
        const string suffix = "(Clone)";
        while (name.EndsWith(suffix, StringComparison.Ordinal))
            name = name.Substring(0, name.Length - suffix.Length).TrimEnd();
        return name;
    }

    static bool IsSavedSceneObject(UnityEngine.Object target)
    {
        if (target == null) return false;
        GlobalObjectId id = GlobalObjectId.GetGlobalObjectIdSlow(target);
        // 2 はシーンに保存されているオブジェクト。実行中に作られたものは対象にしない。
        return id.identifierType == 2 && id.targetObjectId != 0;
    }

    static Dictionary<string, List<RectSample>> GroupByPrefab(List<RectSample> samples)
    {
        Dictionary<string, List<RectSample>> groups = new Dictionary<string, List<RectSample>>();
        for (int i = 0; i < samples.Count; i++)
        {
            RectSample sample = samples[i];
            List<RectSample> group;
            if (!groups.TryGetValue(sample.prefabPath, out group))
            {
                group = new List<RectSample>();
                groups.Add(sample.prefabPath, group);
            }

            group.Add(sample);
        }

        return groups;
    }

    static string BuildConfirmMessage(List<RectSample> samples, List<string> skipped)
    {
        List<string> lines = new List<string>();
        lines.Add("変わっている位置、サイズ、カーソル属性を、元のプレファブへ書き戻します。");
        lines.Add("属性が空のボタンは、カーソル属性を外します。");
        lines.Add("画像、色、マテリアルは変えません。");
        lines.Add("");
        AppendNames(lines, samples, 20);
        bool editsRoot = false;
        for (int i = 0; i < samples.Count; i++)
        {
            if (samples[i].sourceRect != null && samples[i].sourceRect.transform.parent == null)
                editsRoot = true;
        }

        if (editsRoot)
        {
            lines.Add("");
            lines.Add("ボタン自身のプレファブを書き換えるものがあります。そのプレファブの初期の位置とサイズが変わります。");
        }

        if (skipped.Count > 0)
        {
            lines.Add("");
            lines.Add("戻さない " + skipped.Count + " 件");
        }

        return string.Join("\n", lines.ToArray());
    }

    static void AppendNames(List<string> lines, List<RectSample> samples, int limit)
    {
        Dictionary<string, List<string>> byPrefab = new Dictionary<string, List<string>>();
        for (int i = 0; i < samples.Count; i++)
        {
            string file = Path.GetFileName(samples[i].prefabPath);
            List<string> names;
            if (!byPrefab.TryGetValue(file, out names))
            {
                names = new List<string>();
                byPrefab.Add(file, names);
            }

            string label = samples[i].name;
            if (samples[i].attributeChanged)
            {
                label += string.IsNullOrEmpty(samples[i].attributeName)
                    ? "（属性を外す）"
                    : "（属性 " + samples[i].attributeName + "）";
            }

            names.Add(label);
        }

        foreach (KeyValuePair<string, List<string>> pair in byPrefab)
        {
            lines.Add(pair.Key);
            int count = Mathf.Min(limit, pair.Value.Count);
            for (int i = 0; i < count; i++)
                lines.Add("・" + pair.Value[i]);
            if (pair.Value.Count > limit)
                lines.Add("・ほか " + (pair.Value.Count - limit) + " 件");
        }
    }

    static string BuildResultMessage(List<string> writtenNames, int unchanged, List<string> errors, int revertCount)
    {
        List<string> lines = new List<string>();
        if (writtenNames.Count == 0 && errors.Count == 0)
        {
            lines.Add(revertCount > 0
                ? "位置とサイズはプレファブと同じです。再生を止めると、シーンに残っていた位置の上書きを外します。"
                : "位置とサイズはプレファブと同じです。");
        }
        else if (writtenNames.Count > 0)
            lines.Add(writtenNames.Count + " 件をプレファブへ書き戻しました。再生を止めると、シーンに残っていた位置の上書きを外します。");

        if (unchanged > 0)
            lines.Add("プレファブと差が無い " + unchanged + " 件は、プレファブには書きません。");

        for (int i = 0; i < writtenNames.Count && i < 12; i++)
            lines.Add(writtenNames[i]);
        if (writtenNames.Count > 12)
            lines.Add("ほか " + (writtenNames.Count - 12) + " 件");

        for (int i = 0; i < errors.Count; i++)
            lines.Add(errors[i]);
        return string.Join("\n", lines.ToArray());
    }

    static string JoinLines(List<string> lines)
    {
        return string.Join("\n", lines.ToArray());
    }

    static void ApplySourceAttribute(RectSample sample)
    {
        GameObject target = sample.sourceRect.gameObject;
        UiCursorAttribute attribute = target.GetComponent<UiCursorAttribute>();
        if (string.IsNullOrEmpty(sample.attributeName))
        {
            if (attribute == null) return;
            UnityEngine.Object.DestroyImmediate(attribute, true);
            EditorUtility.SetDirty(target);
            return;
        }

        if (attribute == null)
            attribute = target.AddComponent<UiCursorAttribute>();
        attribute.attributeName = sample.attributeName;
        EditorUtility.SetDirty(attribute);
    }

    static void ApplySample(RectSample sample, RectTransform rect)
    {
        rect.anchorMin = sample.anchorMin;
        rect.anchorMax = sample.anchorMax;
        rect.pivot = sample.pivot;
        rect.sizeDelta = sample.sizeDelta;
        rect.anchoredPosition3D = sample.anchoredPosition3D;
        rect.localRotation = sample.localRotation;
        rect.localScale = sample.localScale;
    }

    static bool SameRect(RectTransform rect, RectSample sample)
    {
        return Near(rect.anchorMin, sample.anchorMin, AnchorEpsilon)
            && Near(rect.anchorMax, sample.anchorMax, AnchorEpsilon)
            && Near(rect.pivot, sample.pivot, AnchorEpsilon)
            && Near(rect.sizeDelta, sample.sizeDelta, PositionEpsilon)
            && Near(rect.anchoredPosition3D, sample.anchoredPosition3D, PositionEpsilon)
            && Near(rect.localScale, sample.localScale, PositionEpsilon)
            && Quaternion.Angle(rect.localRotation, sample.localRotation) < PositionEpsilon;
    }

    static bool Near(Vector2 a, Vector2 b, float epsilon)
    {
        return Mathf.Abs(a.x - b.x) < epsilon && Mathf.Abs(a.y - b.y) < epsilon;
    }

    static bool Near(Vector3 a, Vector3 b, float epsilon)
    {
        return Mathf.Abs(a.x - b.x) < epsilon
            && Mathf.Abs(a.y - b.y) < epsilon
            && Mathf.Abs(a.z - b.z) < epsilon;
    }

    static Image ButtonImage(Button button)
    {
        if (button == null) return null;
        Image graphic = button.targetGraphic as Image;
        if (graphic != null) return graphic;
        return button.GetComponent<Image>();
    }

    static int[] IndexPath(Transform root, Transform node)
    {
        List<int> indices = new List<int>();
        Transform current = node;
        while (current != null && current != root)
        {
            indices.Add(current.GetSiblingIndex());
            current = current.parent;
        }

        if (current != root) return null;
        indices.Reverse();
        return indices.ToArray();
    }

    static string[] NamePath(Transform root, Transform node)
    {
        List<string> names = new List<string>();
        Transform current = node;
        while (current != null && current != root)
        {
            names.Add(current.name);
            current = current.parent;
        }

        if (current != root) return null;
        names.Reverse();
        return names.ToArray();
    }

    static Transform FindSample(Transform root, RectSample sample)
    {
        if (sample.indexPath != null && sample.indexPath.Length == 0)
            return root;

        Transform byIndex = FindByIndex(root, sample.indexPath);
        if (byIndex != null && byIndex.name == sample.name)
            return byIndex;
        return FindByNamePath(root, sample.namePath);
    }

    static Transform FindByIndex(Transform root, int[] path)
    {
        if (root == null || path == null) return null;
        Transform current = root;
        for (int i = 0; i < path.Length; i++)
        {
            if (path[i] < 0 || path[i] >= current.childCount) return null;
            current = current.GetChild(path[i]);
        }

        return current;
    }

    static Transform FindByNamePath(Transform root, string[] names)
    {
        if (root == null || names == null) return null;
        Transform current = root;
        for (int i = 0; i < names.Length; i++)
        {
            Transform next = null;
            int matches = 0;
            for (int c = 0; c < current.childCount; c++)
            {
                Transform child = current.GetChild(c);
                if (child.name != names[i]) continue;
                next = child;
                matches++;
            }

            if (matches != 1) return null;
            current = next;
        }

        return current;
    }

    static string HierarchyPath(GameObject gameObject)
    {
        List<string> names = new List<string>();
        Transform current = gameObject.transform;
        while (current != null)
        {
            names.Add(current.name);
            current = current.parent;
        }

        names.Reverse();
        return string.Join("/", names.ToArray());
    }

    static GameObject FindByHierarchyPath(string path)
    {
        if (string.IsNullOrEmpty(path)) return null;
        string[] names = path.Split('/');
        if (names.Length == 0) return null;

        for (int s = 0; s < SceneManager.sceneCount; s++)
        {
            Scene scene = SceneManager.GetSceneAt(s);
            if (!scene.isLoaded) continue;
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                if (roots[i].name != names[0]) continue;
                Transform current = roots[i].transform;
                bool match = true;
                for (int n = 1; n < names.Length; n++)
                {
                    Transform next = null;
                    for (int c = 0; c < current.childCount; c++)
                    {
                        Transform child = current.GetChild(c);
                        if (child.name != names[n]) continue;
                        next = child;
                        break;
                    }

                    if (next == null)
                    {
                        match = false;
                        break;
                    }

                    current = next;
                }

                if (match) return current.gameObject;
            }
        }

        return null;
    }

    void SetStatus(string message, MessageType type)
    {
        status = message;
        statusType = type;
        Repaint();
    }

    static Sprite whiteSprite;

    static Sprite WhiteSprite()
    {
        if (whiteSprite != null) return whiteSprite;
        Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        texture.name = "UiAdjustDebugWhite";
        texture.hideFlags = HideFlags.HideAndDontSave;
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();
        whiteSprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 100f);
        whiteSprite.name = "UiAdjustDebugWhite";
        whiteSprite.hideFlags = HideFlags.HideAndDontSave;
        return whiteSprite;
    }
}

/// <summary>
/// 再生を止めたあと、書き戻したボタンのシーン上の Rect 上書きを外す。
/// </summary>
[InitializeOnLoad]
public static class UiAdjustDebugSceneRevert
{
    const string PendingKey = "Dlair.UiAdjustDebug.PendingRevert";

    [Serializable]
    public class PendingRect
    {
        public string id;
        public string name;
    }

    [Serializable]
    class PendingRectList
    {
        public PendingRect[] items;
    }

    static UiAdjustDebugSceneRevert()
    {
        EditorApplication.playModeStateChanged += OnPlayMode;
    }

    static void OnPlayMode(PlayModeStateChange change)
    {
        if (change == PlayModeStateChange.EnteredEditMode)
            TryRevert();
    }

    public static void Remember(List<PendingRect> added)
    {
        if (added == null || added.Count == 0) return;
        PendingRectList current = Load();
        List<PendingRect> merged = new List<PendingRect>();
        if (current.items != null)
        {
            for (int i = 0; i < current.items.Length; i++)
            {
                if (current.items[i] != null)
                    merged.Add(current.items[i]);
            }
        }

        for (int i = 0; i < added.Count; i++)
        {
            if (added[i] == null || string.IsNullOrEmpty(added[i].id)) continue;
            bool exists = false;
            for (int j = 0; j < merged.Count; j++)
            {
                if (merged[j].id == added[i].id)
                {
                    exists = true;
                    break;
                }
            }

            if (!exists) merged.Add(added[i]);
        }

        current.items = merged.ToArray();
        SessionState.SetString(PendingKey, JsonUtility.ToJson(current));
    }

    static void TryRevert()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        PendingRectList pending = Load();
        if (pending.items == null || pending.items.Length == 0) return;
        SessionState.EraseString(PendingKey);

        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("UI調整のシーン上書きを外す");
        bool reverted = false;

        for (int i = 0; i < pending.items.Length; i++)
        {
            PendingRect item = pending.items[i];
            if (item == null || string.IsNullOrEmpty(item.id)) continue;
            if (!GlobalObjectId.TryParse(item.id, out GlobalObjectId id)) continue;
            RectTransform rect = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(id) as RectTransform;
            if (rect == null)
            {
                Debug.LogWarning("UI調整デバッグ: シーン上の " + item.name + " を見つけられませんでした。プレファブは更新済みです。");
                continue;
            }

            if (!PrefabUtility.IsPartOfPrefabInstance(rect)) continue;
            if (!HasRectModification(rect)) continue;
            PrefabUtility.RevertObjectOverride(rect, InteractionMode.UserAction);
            EditorSceneManager.MarkSceneDirty(rect.gameObject.scene);
            reverted = true;
        }

        if (reverted)
            Undo.CollapseUndoOperations(group);
    }

    static bool HasRectModification(RectTransform rect)
    {
        GameObject outer = PrefabUtility.GetOutermostPrefabInstanceRoot(rect.gameObject);
        if (outer == null) return false;
        PropertyModification[] modifications = PrefabUtility.GetPropertyModifications(outer);
        if (modifications == null) return false;

        UnityEngine.Object source = PrefabUtility.GetCorrespondingObjectFromSource(rect);
        UnityEngine.Object original = PrefabUtility.GetCorrespondingObjectFromOriginalSource(rect);
        for (int i = 0; i < modifications.Length; i++)
        {
            UnityEngine.Object target = modifications[i].target;
            if (target == null) continue;
            if (target == source || target == original) return true;
        }

        return false;
    }

    static PendingRectList Load()
    {
        string json = SessionState.GetString(PendingKey, "");
        if (string.IsNullOrEmpty(json)) return new PendingRectList();
        PendingRectList list = JsonUtility.FromJson<PendingRectList>(json);
        return list ?? new PendingRectList();
    }
}
