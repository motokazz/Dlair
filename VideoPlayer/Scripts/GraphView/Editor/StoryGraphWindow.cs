using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;

public class StoryGraphWindow : EditorWindow
{
    private StoryGraphView graphView;
    public StoryGraph currentGraph;
    [SerializeField] private List<StoryGraph> navigationHistory = new List<StoryGraph>();
    [SerializeField] private bool followExecution = true;

    private VisualElement toolbarHost;
    private bool isViewReady;
    private bool isSaving;
    private bool saveScheduled;
    private bool isApplyingUndo;
    private string pendingUndoName;
    private string highlightedGuid;
    private StoryPlayer trackedPlayer;

    public bool IsApplyingUndo => isApplyingUndo;

    [OnOpenAsset(1)]
    public static bool OnOpenAsset(EntityId entityId, int line)
    {
        string assetPath = AssetDatabase.GetAssetPath(entityId);
        StoryGraph graph = AssetDatabase.LoadAssetAtPath<StoryGraph>(assetPath);
        if (graph != null)
        {
            OpenWindow(graph);
            return true;
        }
        return false;
    }

    public static void OpenWindow(StoryGraph graph)
    {
        StoryGraphWindow window = GetWindow<StoryGraphWindow>();
        window.ShowGraph(graph);
        window.Focus();
    }

    private void OnEnable()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        EditorApplication.update -= OnEditorUpdate;
        EditorApplication.update += OnEditorUpdate;
        Undo.undoRedoPerformed -= OnUndoRedoPerformed;
        Undo.undoRedoPerformed += OnUndoRedoPerformed;

        if (currentGraph != null && graphView == null)
        {
            GenerateGraph();
        }
        else
        {
            RefreshToolbar();
        }
    }

    private void OnDisable()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        EditorApplication.update -= OnEditorUpdate;
        Undo.undoRedoPerformed -= OnUndoRedoPerformed;
        if (saveScheduled)
        {
            EditorApplication.delayCall -= FlushUndoCommit;
            saveScheduled = false;
        }

        if (isViewReady && currentGraph != null && graphView != null &&
            !EditorApplication.isCompiling && !EditorApplication.isUpdating)
        {
            SaveData();
            Debug.Log("【自動セーブ】ウィンドウが閉じられたため、グラフを保存しました。");
        }

        isViewReady = false;
        trackedPlayer = null;
        highlightedGuid = null;
    }

    private void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        switch (state)
        {
            case PlayModeStateChange.ExitingEditMode:
            case PlayModeStateChange.ExitingPlayMode:
                if (isViewReady)
                {
                    SaveData();
                }
                break;
            case PlayModeStateChange.EnteredPlayMode:
                trackedPlayer = null;
                highlightedGuid = null;
                if (currentGraph != null && graphView == null)
                {
                    GenerateGraph();
                }
                else
                {
                    RefreshToolbar();
                }
                break;
            case PlayModeStateChange.EnteredEditMode:
                trackedPlayer = null;
                highlightedGuid = null;
                ClearExecutionHighlight();
                if (currentGraph != null && graphView == null)
                {
                    GenerateGraph();
                }
                else
                {
                    RefreshToolbar();
                }
                break;
        }
    }

    private void OnEditorUpdate()
    {
        if (!isViewReady || graphView == null) return;
        RefreshExecutionHighlight();
    }

    public void ShowGraph(StoryGraph graph)
    {
        if (graph == null) return;

        if (currentGraph == graph && graphView != null)
        {
            Focus();
            return;
        }

        if (currentGraph != null && graphView != null)
        {
            SaveData();
            RecordHistory(currentGraph, graph);
        }

        currentGraph = graph;
        GenerateGraph();
    }

    public void NavigateBack()
    {
        PruneMissingHistory();
        if (navigationHistory.Count == 0) return;

        StoryGraph previous = navigationHistory[navigationHistory.Count - 1];
        navigationHistory.RemoveAt(navigationHistory.Count - 1);
        if (previous == null) return;

        if (currentGraph != null && graphView != null)
        {
            SaveData();
        }

        currentGraph = previous;
        GenerateGraph();
    }

    public void CommitGraphFromView(string undoName, bool immediate = false)
    {
        if (isApplyingUndo || !isViewReady || currentGraph == null || graphView == null) return;

        if (!immediate)
        {
            if (string.IsNullOrEmpty(pendingUndoName))
            {
                pendingUndoName = undoName;
            }

            if (saveScheduled) return;
            saveScheduled = true;
            EditorApplication.delayCall += FlushUndoCommit;
            return;
        }

        ApplyUndoableSave(undoName);
    }

    private void FlushUndoCommit()
    {
        saveScheduled = false;
        if (isApplyingUndo || !isViewReady) return;
        ApplyUndoableSave(string.IsNullOrEmpty(pendingUndoName) ? "グラフを編集" : pendingUndoName);
        pendingUndoName = null;
    }

    private void ApplyUndoableSave(string undoName)
    {
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName(undoName);
        RecordGraphUndo(undoName);
        SaveGraphToAsset(writeToDisk: EditorApplication.isPlaying, undoable: true, undoName);
        Undo.CollapseUndoOperations(group);
    }

    public void RecordGraphUndo(string undoName)
    {
        if (currentGraph == null) return;

        Undo.RegisterCompleteObjectUndo(currentGraph, undoName);
        if (currentGraph.nodes == null) return;

        for (int i = 0; i < currentGraph.nodes.Count; i++)
        {
            BaseNode node = currentGraph.nodes[i];
            if (node != null)
            {
                Undo.RegisterCompleteObjectUndo(node, undoName);
            }
        }
    }

    private void OnUndoRedoPerformed()
    {
        if (!isViewReady || currentGraph == null || graphView == null) return;

        isApplyingUndo = true;
        try
        {
            Vector3 viewPos = graphView.viewTransform.position;
            Vector3 viewScale = graphView.viewTransform.scale;
            LoadData(frameAll: false);
            graphView.viewTransform.position = viewPos;
            graphView.viewTransform.scale = viewScale;
        }
        finally
        {
            isApplyingUndo = false;
        }
    }

    public void NotifyGraphStructureChanged()
    {
        CommitGraphFromView("グラフを編集");
    }

    private void RecordHistory(StoryGraph from, StoryGraph to)
    {
        PruneMissingHistory();

        int existing = navigationHistory.IndexOf(to);
        if (existing >= 0)
        {
            navigationHistory.RemoveRange(existing, navigationHistory.Count - existing);
            return;
        }

        if (from != null && (navigationHistory.Count == 0 || navigationHistory[navigationHistory.Count - 1] != from))
        {
            navigationHistory.Add(from);
        }
    }

    private void PruneMissingHistory()
    {
        navigationHistory.RemoveAll(graph => graph == null);
    }

    private void GenerateGraph()
    {
        isViewReady = false;
        highlightedGuid = null;

        rootVisualElement.Clear();
        rootVisualElement.style.flexDirection = FlexDirection.Column;
        rootVisualElement.focusable = true;
        rootVisualElement.UnregisterCallback<KeyDownEvent>(OnRootKeyDown);
        rootVisualElement.RegisterCallback<KeyDownEvent>(OnRootKeyDown);

        toolbarHost = new VisualElement { name = "toolbar-host" };
        toolbarHost.style.flexShrink = 0;
        rootVisualElement.Add(toolbarHost);
        RefreshToolbar();

        graphView = new StoryGraphView(this) { name = "Story Graph" };
        graphView.style.flexGrow = 1;
        graphView.style.flexShrink = 1;
        rootVisualElement.Add(graphView);

        titleContent = new GUIContent(currentGraph != null ? currentGraph.name : "Story Graph Editor");
        LoadData(frameAll: true);
        isViewReady = true;
    }

    private void RefreshToolbar()
    {
        if (toolbarHost == null) return;
        toolbarHost.Clear();
        toolbarHost.Add(BuildToolbar());
    }

    private void OnRootKeyDown(KeyDownEvent evt)
    {
        if (evt.keyCode == KeyCode.LeftArrow && evt.altKey)
        {
            NavigateBack();
            evt.StopPropagation();
        }
    }

    private VisualElement BuildToolbar()
    {
        VisualElement column = new VisualElement();
        column.style.flexShrink = 0;

        if (EditorApplication.isPlaying)
        {
            column.Add(BuildPlayModeBanner());
        }

        VisualElement toolbar = new VisualElement();
        toolbar.style.flexDirection = FlexDirection.Row;
        toolbar.style.alignItems = Align.Center;
        toolbar.style.flexShrink = 0;
        toolbar.style.height = 34;
        toolbar.style.backgroundColor = new StyleColor(new Color(0.16f, 0.16f, 0.16f));
        toolbar.style.paddingLeft = 8;
        toolbar.style.paddingRight = 8;
        toolbar.style.borderBottomWidth = 1;
        toolbar.style.borderBottomColor = new StyleColor(new Color(0.08f, 0.08f, 0.08f));

        Button saveButton = new Button(SaveData)
        {
            text = EditorApplication.isPlaying ? "💾 Save（再生中）" : "💾 Save",
            tooltip = EditorApplication.isPlaying
                ? "再生中の編集をアセットへ保存します。停止後も残ります。"
                : "グラフを保存します"
        };
        saveButton.style.height = 24;
        saveButton.style.backgroundColor = new StyleColor(new Color(0.2f, 0.5f, 0.2f));
        toolbar.Add(saveButton);

        PruneMissingHistory();
        Button backButton = new Button(NavigateBack)
        {
            text = "← 戻る",
            tooltip = "ひとつ前のグラフに戻ります（Alt+←）"
        };
        backButton.style.height = 24;
        backButton.style.marginLeft = 6;
        backButton.SetEnabled(navigationHistory.Count > 0);
        toolbar.Add(backButton);

        if (EditorApplication.isPlaying)
        {
            Toggle followToggle = new Toggle("実行を追従")
            {
                value = followExecution,
                tooltip = "再生中、実行中のノード／サブグラフへ自動で移動します"
            };
            followToggle.style.marginLeft = 10;
            followToggle.RegisterValueChangedCallback(evt => followExecution = evt.newValue);
            toolbar.Add(followToggle);
        }

        VisualElement crumbs = new VisualElement();
        crumbs.style.flexDirection = FlexDirection.Row;
        crumbs.style.alignItems = Align.Center;
        crumbs.style.flexGrow = 1;
        crumbs.style.marginLeft = 12;
        crumbs.style.overflow = Overflow.Hidden;

        List<StoryGraph> path = new List<StoryGraph>(navigationHistory);
        if (currentGraph != null) path.Add(currentGraph);

        for (int i = 0; i < path.Count; i++)
        {
            StoryGraph graph = path[i];
            if (graph == null) continue;

            if (i > 0)
            {
                Label separator = new Label("›")
                {
                    style =
                    {
                        marginLeft = 4,
                        marginRight = 4,
                        color = new StyleColor(new Color(0.65f, 0.65f, 0.65f)),
                        unityTextAlign = TextAnchor.MiddleCenter
                    }
                };
                crumbs.Add(separator);
            }

            bool isCurrent = graph == currentGraph;
            StoryGraph captured = graph;
            Button crumb = new Button(() => ShowGraph(captured))
            {
                text = graph.name,
                tooltip = isCurrent ? "いま開いているグラフ" : $"{graph.name} に戻る"
            };
            crumb.style.height = 22;
            crumb.style.paddingLeft = 6;
            crumb.style.paddingRight = 6;
            crumb.SetEnabled(!isCurrent);
            if (isCurrent)
            {
                crumb.style.backgroundColor = new StyleColor(new Color(0.28f, 0.38f, 0.52f));
                crumb.style.color = Color.white;
            }

            crumbs.Add(crumb);
        }

        toolbar.Add(crumbs);
        column.Add(toolbar);
        return column;
    }

    private VisualElement BuildPlayModeBanner()
    {
        VisualElement banner = new VisualElement();
        banner.style.flexDirection = FlexDirection.Row;
        banner.style.alignItems = Align.Center;
        banner.style.height = 26;
        banner.style.backgroundColor = new StyleColor(new Color(0.32f, 0.22f, 0.08f));
        banner.style.paddingLeft = 8;
        banner.style.paddingRight = 8;

        Label label = new Label("▶ Play Mode — ノード・接続の編集はアセットに保存され、次の進行から反映されます");
        label.style.color = new StyleColor(new Color(1f, 0.92f, 0.65f));
        label.style.unityFontStyleAndWeight = FontStyle.Bold;
        label.style.flexGrow = 1;
        banner.Add(label);
        return banner;
    }

    public void SaveGraphAfterEdit()
    {
        SaveGraphToAsset(writeToDisk: EditorApplication.isPlaying, undoable: false);
    }

    private void SaveData()
    {
        SaveGraphToAsset(writeToDisk: true, undoable: false);
    }

    private void SaveGraphToAsset(bool writeToDisk, bool undoable, string undoName = null)
    {
        if (isSaving || !isViewReady || currentGraph == null || graphView == null) return;

        isSaving = true;
        try
        {
            List<StoryNodeUI> uiNodes = new List<StoryNodeUI>();

            foreach (var element in graphView.graphElements)
            {
                if (element is StoryNodeUI nodeUI) uiNodes.Add(nodeUI);
            }

            List<string> uiNodeGuids = new List<string>();
            foreach (var n in uiNodes) uiNodeGuids.Add(n.guid);

            if (uiNodes.Count == 0 && currentGraph.nodes != null && currentGraph.nodes.Count > 0)
            {
                Debug.LogWarning("【StoryGraph】表示中のノードが取得できないため保存を中止しました。座標の破損を防ぐためです。");
                return;
            }

            bool writePositions = !LivePositionsLookCollapsed(uiNodes);

            if (!writePositions)
            {
                Debug.LogWarning("【StoryGraph】ノード座標が不正（全ノードが同一点）のため、既存の座標を保持して保存します。");
            }

            var allAssets = AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(currentGraph));
            foreach (var asset in allAssets)
            {
                if (asset is BaseNode assetNode)
                {
                    if (!uiNodeGuids.Contains(assetNode.guid))
                    {
                        if (undoable)
                        {
                            Undo.DestroyObjectImmediate(assetNode);
                        }
                        else
                        {
                            AssetDatabase.RemoveObjectFromAsset(assetNode);
                            DestroyImmediate(assetNode, true);
                        }
                    }
                }
            }

            currentGraph.nodes.Clear();
            foreach (var nodeUI in uiNodes)
            {
                if (nodeUI.data != null && writePositions && nodeUI.TryGetLayoutPosition(out Vector2 livePos))
                {
                    nodeUI.data.position = livePos;
                }

                if (!AssetDatabase.Contains(nodeUI.data))
                {
                    nodeUI.data.hideFlags = HideFlags.None;
                    AssetDatabase.AddObjectToAsset(nodeUI.data, currentGraph);
                    if (undoable)
                    {
                        Undo.RegisterCreatedObjectUndo(nodeUI.data, string.IsNullOrEmpty(undoName) ? "ノードを作成" : undoName);
                    }
                }
                else
                {
                    nodeUI.data.hideFlags &= ~HideFlags.DontSave;
                }

                currentGraph.nodes.Add(nodeUI.data);
                EditorUtility.SetDirty(nodeUI.data);
            }

            currentGraph.links.Clear();
            HashSet<string> seenLinks = new HashSet<string>();
            foreach (var nodeUI in uiNodes)
            {
                if (nodeUI == null || string.IsNullOrEmpty(nodeUI.guid)) continue;

                foreach (VisualElement child in nodeUI.outputContainer.Children())
                {
                    if (child is not Port outputPort) continue;

                    foreach (Edge edge in outputPort.connections)
                    {
                        StoryNodeUI inputNode = edge != null ? edge.input?.node as StoryNodeUI : null;
                        if (inputNode == null || string.IsNullOrEmpty(inputNode.guid)) continue;

                        string key = nodeUI.guid + "\n" + outputPort.portName + "\n" + inputNode.guid;
                        if (!seenLinks.Add(key)) continue;

                        currentGraph.links.Add(new StoryLinkData
                        {
                            baseNodeGuid = nodeUI.guid,
                            portName = outputPort.portName,
                            targetNodeGuid = inputNode.guid
                        });
                    }
                }
            }

            EditorUtility.SetDirty(currentGraph);
            if (writeToDisk)
            {
                AssetDatabase.SaveAssetIfDirty(currentGraph);
            }
        }
        finally
        {
            isSaving = false;
        }
    }

    private static bool LivePositionsLookCollapsed(List<StoryNodeUI> uiNodes)
    {
        if (uiNodes == null || uiNodes.Count < 2) return false;

        List<Vector2> live = new List<Vector2>(uiNodes.Count);
        List<Vector2> stored = new List<Vector2>(uiNodes.Count);

        for (int i = 0; i < uiNodes.Count; i++)
        {
            StoryNodeUI nodeUI = uiNodes[i];
            if (nodeUI == null || nodeUI.data == null) continue;

            stored.Add(SanitizePosition(nodeUI.data.position));
            if (nodeUI.TryGetLayoutPosition(out Vector2 livePos))
            {
                live.Add(livePos);
            }
        }

        if (live.Count < 2 || stored.Count < 2) return false;
        if (!AreClustered(live, 16f)) return false;
        if (AreClustered(stored, 16f)) return false;
        return true;
    }

    private static bool AreClustered(List<Vector2> points, float threshold)
    {
        if (points == null || points.Count == 0) return true;

        Vector2 min = points[0];
        Vector2 max = points[0];
        for (int i = 1; i < points.Count; i++)
        {
            Vector2 p = points[i];
            min = Vector2.Min(min, p);
            max = Vector2.Max(max, p);
        }

        return (max - min).sqrMagnitude <= threshold * threshold;
    }

    private void LoadData(bool frameAll = false)
    {
        if (currentGraph == null || graphView == null) return;

        NormalizePositionsAroundStart(currentGraph);

        StoryGraphEditorHooks.IgnoreGraphViewChange = true;
        try
        {
            List<GraphElement> elementsToDelete = new List<GraphElement>();
            foreach (var element in graphView.graphElements)
            {
                if (element is Node || element is Edge) elementsToDelete.Add(element);
            }
            graphView.DeleteElements(elementsToDelete);

            Dictionary<string, StoryNodeUI> nodeDic = new Dictionary<string, StoryNodeUI>();
            foreach (var nodeData in currentGraph.nodes)
            {
                if (nodeData == null) continue;
                StoryNodeUI nodeUI = new StoryNodeUI(nodeData);
                graphView.AddElement(nodeUI);
                nodeDic.Add(nodeUI.guid, nodeUI);
            }

            foreach (var link in currentGraph.links)
            {
                if (nodeDic.ContainsKey(link.baseNodeGuid) && nodeDic.ContainsKey(link.targetNodeGuid))
                {
                    var outputNode = nodeDic[link.baseNodeGuid];
                    var inputNode = nodeDic[link.targetNodeGuid];

                    Port outPort = null;
                    foreach (var child in outputNode.outputContainer.Children())
                    {
                        if (child is Port p && p.portName == link.portName) outPort = p;
                    }

                    Port inPort = null;
                    foreach (var child in inputNode.inputContainer.Children())
                    {
                        if (child is Port p)
                        {
                            inPort = p;
                            break;
                        }
                    }

                    if (outPort != null && inPort != null)
                    {
                        Edge edge = outPort.ConnectTo(inPort);
                        graphView.AddElement(edge);
                    }
                }
            }
        }
        finally
        {
            StoryGraphEditorHooks.IgnoreGraphViewChange = false;
        }

        if (frameAll)
        {
            ScheduleFrameAll();
        }
    }

    private static void NormalizePositionsAroundStart(StoryGraph graph)
    {
        if (graph == null || graph.nodes == null || graph.nodes.Count == 0) return;

        BaseNode origin = null;
        for (int i = 0; i < graph.nodes.Count; i++)
        {
            if (graph.nodes[i] is StartNode start)
            {
                origin = start;
                break;
            }
        }

        if (origin == null)
        {
            for (int i = 0; i < graph.nodes.Count; i++)
            {
                if (graph.nodes[i] != null)
                {
                    origin = graph.nodes[i];
                    break;
                }
            }
        }

        if (origin == null) return;

        Vector2 originPos = SanitizePosition(origin.position);
        bool originShifted = originPos.sqrMagnitude >= 0.01f;
        bool anyChanged = false;

        for (int i = 0; i < graph.nodes.Count; i++)
        {
            BaseNode node = graph.nodes[i];
            if (node == null) continue;

            Vector2 sanitized = SanitizePosition(node.position);
            Vector2 next = originShifted ? sanitized - originPos : sanitized;
            if ((node.position - next).sqrMagnitude < 0.0001f) continue;

            node.position = next;
            EditorUtility.SetDirty(node);
            anyChanged = true;
        }

        if (anyChanged)
        {
            EditorUtility.SetDirty(graph);
        }
    }

    private static Vector2 SanitizePosition(Vector2 position)
    {
        if (float.IsNaN(position.x) || float.IsInfinity(position.x)) position.x = 0f;
        if (float.IsNaN(position.y) || float.IsInfinity(position.y)) position.y = 0f;
        return position;
    }

    private void ScheduleFrameAll()
    {
        if (graphView == null) return;

        StoryGraphView view = graphView;
        view.schedule.Execute(() =>
        {
            if (view != graphView) return;
            view.FrameAll();
        }).ExecuteLater(1);
    }

    private void RefreshExecutionHighlight()
    {
        if (!EditorApplication.isPlaying)
        {
            if (highlightedGuid != null)
            {
                ClearExecutionHighlight();
            }
            return;
        }

        if (trackedPlayer == null)
        {
#if UNITY_2023_1_OR_NEWER
            trackedPlayer = Object.FindFirstObjectByType<StoryPlayer>();
#else
            trackedPlayer = Object.FindObjectOfType<StoryPlayer>();
#endif
        }

        if (trackedPlayer == null)
        {
            ClearExecutionHighlight();
            return;
        }

        StoryGraph executingGraph = trackedPlayer.ActiveGraph;
        BaseNode executingNode = trackedPlayer.CurrentNode;

        if (followExecution && executingGraph != null && executingGraph != currentGraph)
        {
            ShowGraph(executingGraph);
            return;
        }

        string guid = executingNode != null ? executingNode.guid : null;
        bool nodeChanged = highlightedGuid != guid;
        highlightedGuid = guid;

        foreach (var element in graphView.graphElements)
        {
            if (element is StoryNodeUI nodeUI)
            {
                nodeUI.SetExecuting(guid != null && nodeUI.guid == guid);
            }
        }

        if (followExecution && nodeChanged && !string.IsNullOrEmpty(guid))
        {
            FrameExecutingNode(guid);
        }
    }

    private void ClearExecutionHighlight()
    {
        highlightedGuid = null;
        if (graphView == null) return;

        foreach (var element in graphView.graphElements)
        {
            if (element is StoryNodeUI nodeUI)
            {
                nodeUI.SetExecuting(false);
            }
        }
    }

    private void FrameExecutingNode(string guid)
    {
        StoryNodeUI match = null;
        foreach (var element in graphView.graphElements)
        {
            if (element is StoryNodeUI nodeUI && nodeUI.guid == guid)
            {
                match = nodeUI;
                break;
            }
        }

        if (match == null) return;

        List<ISelectable> previous = new List<ISelectable>(graphView.selection);
        graphView.ClearSelection();
        graphView.AddToSelection(match);
        graphView.FrameSelection();
        graphView.ClearSelection();
        foreach (ISelectable selectable in previous)
        {
            graphView.AddToSelection(selectable);
        }
    }
}
