using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;

public class StoryFlowWindow : EditorWindow
{
    private StoryFlowGraphView graphView;
    public StoryFlowGraph currentGraph;

    [OnOpenAsset(1)]
    public static bool OnOpenAsset(EntityId entityId, int line)
    {
        string assetPath = AssetDatabase.GetAssetPath(entityId);
        StoryFlowGraph graph = AssetDatabase.LoadAssetAtPath<StoryFlowGraph>(assetPath);
        if (graph != null)
        {
            OpenWindow(graph);
            return true;
        }
        return false;
    }

    public static void OpenWindow(StoryFlowGraph graph)
    {
        StoryFlowWindow window = GetWindow<StoryFlowWindow>("Story Flow Editor");
        window.currentGraph = graph;
        window.GenerateGraph();
    }

    private void GenerateGraph()
    {
        rootVisualElement.Clear();

        graphView = new StoryFlowGraphView(this) { name = "Story Flow Graph" };
        graphView.StretchToParentSize();
        rootVisualElement.Add(graphView);

        Button saveButton = new Button(() => SaveData());
        saveButton.text = "💾 Save Graph";
        saveButton.style.height = 30;
        saveButton.style.width = 120;
        saveButton.style.backgroundColor = new StyleColor(new Color(0.2f, 0.5f, 0.2f));
        saveButton.style.position = Position.Absolute;
        saveButton.style.top = 10;
        saveButton.style.left = 10;
        rootVisualElement.Add(saveButton);

        LoadData();
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

        List<StoryFlowNodeUI> uiNodes = new List<StoryFlowNodeUI>();
        List<Edge> uiEdges = new List<Edge>();

        foreach (var element in graphView.graphElements)
        {
            if (element is StoryFlowNodeUI nodeUI) uiNodes.Add(nodeUI);
            if (element is Edge edge) uiEdges.Add(edge);
        }

        List<string> uiNodeGuids = new List<string>();
        foreach (var n in uiNodes) uiNodeGuids.Add(n.guid);

        var allAssets = AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(currentGraph));
        foreach (var asset in allAssets)
        {
            if (asset is BaseFlowNode assetNode)
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
            var outputNode = edge.output.node as StoryFlowNodeUI;
            var inputNode = edge.input.node as StoryFlowNodeUI;

            if (outputNode != null && inputNode != null)
            {
                currentGraph.links.Add(new FlowLinkData
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

        Dictionary<string, StoryFlowNodeUI> nodeDic = new Dictionary<string, StoryFlowNodeUI>();
        foreach (var nodeData in currentGraph.nodes)
        {
            if (nodeData == null) continue;
            StoryFlowNodeUI nodeUI = new StoryFlowNodeUI(nodeData);
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