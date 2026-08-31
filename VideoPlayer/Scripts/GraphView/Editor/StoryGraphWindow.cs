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
    private readonly List<StoryGraph> navigationHistory = new List<StoryGraph>();

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
        rootVisualElement.Clear();
        rootVisualElement.style.flexDirection = FlexDirection.Column;
        rootVisualElement.focusable = true;
        rootVisualElement.RegisterCallback<KeyDownEvent>(OnRootKeyDown);

        rootVisualElement.Add(BuildToolbar());

        graphView = new StoryGraphView(this) { name = "Story Graph" };
        graphView.style.flexGrow = 1;
        graphView.style.flexShrink = 1;
        rootVisualElement.Add(graphView);

        titleContent = new GUIContent(currentGraph != null ? currentGraph.name : "Story Graph Editor");
        LoadData();
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
            text = "💾 Save",
            tooltip = "グラフを保存します"
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
        return toolbar;
    }

    // ==========================================
    // ★追加：ウィンドウが閉じられる時に呼ばれる処理
    // ==========================================
    private void OnDisable()
    {
        // グラフが開かれていれば、強制的にセーブを実行する
        if (currentGraph != null && graphView != null)
        {
            SaveData();
            Debug.Log("【自動セーブ】ウィンドウが閉じられたため、グラフを保存しました。");
        }
    }

    // ==========================================
    // キャンバスの状態をファイルに保存する
    // ==========================================
    private void SaveData()
    {
        if (currentGraph == null || graphView == null) return; // 安全対策を追加

        List<StoryNodeUI> uiNodes = new List<StoryNodeUI>();
        List<Edge> uiEdges = new List<Edge>();

        foreach (var element in graphView.graphElements)
        {
            if (element is StoryNodeUI nodeUI) uiNodes.Add(nodeUI);
            if (element is Edge edge) uiEdges.Add(edge);
        }

        List<string> uiNodeGuids = new List<string>();
        foreach (var n in uiNodes) uiNodeGuids.Add(n.guid);

        var allAssets = AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(currentGraph));
        foreach (var asset in allAssets)
        {
            if (asset is BaseNode assetNode)
            {
                if (!uiNodeGuids.Contains(assetNode.guid))
                {
                    AssetDatabase.RemoveObjectFromAsset(assetNode);
                    DestroyImmediate(assetNode, true);
                }
            }
        }

        currentGraph.nodes.Clear();
        foreach (var nodeUI in uiNodes)
        {
            nodeUI.data.position = nodeUI.GetPosition().position;

            if (!AssetDatabase.Contains(nodeUI.data))
            {
                AssetDatabase.AddObjectToAsset(nodeUI.data, currentGraph);
            }
            currentGraph.nodes.Add(nodeUI.data);
        }

        currentGraph.links.Clear();
        foreach (var edge in uiEdges)
        {
            var outputNode = edge.output.node as StoryNodeUI;
            var inputNode = edge.input.node as StoryNodeUI;

            if (outputNode != null && inputNode != null)
            {
                currentGraph.links.Add(new StoryLinkData
                {
                    baseNodeGuid = outputNode.guid,
                    portName = edge.output.portName,
                    targetNodeGuid = inputNode.guid
                });
            }
        }

        EditorUtility.SetDirty(currentGraph);
        AssetDatabase.SaveAssets();
    }

    // ==========================================
    // ファイルからキャンバスを復元する
    // ==========================================
    private void LoadData()
    {
        if (currentGraph == null) return;

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
}