using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine.UIElements;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Video;
using System;

public class StoryGraphView : GraphView
{
    private const string ClipboardMarker = "StoryGraphClipboard:";
    private const float PasteOffset = 40f;

    private StoryGraphWindow window;
    private string lastPasteData;
    private int pasteCount;

    public StoryGraph CurrentGraph => window != null ? window.currentGraph : null;

    public StoryGraphView(StoryGraphWindow window)
    {
        this.window = window;
        Insert(0, new GridBackground());
        this.AddManipulator(new ContentZoomer());
        this.AddManipulator(new ContentDragger());
        this.AddManipulator(new SelectionDragger());
        this.AddManipulator(new RectangleSelector());
        this.style.backgroundColor = new StyleColor(new Color(0.15f, 0.15f, 0.15f));

        focusable = true;
        RegisterCallback<PointerDownEvent>(_ => Focus());

        serializeGraphElements = SerializeGraph;
        unserializeAndPaste = UnserializeAndPaste;
        canPasteSerializedData = CanPaste;

        RegisterCallback<DragUpdatedEvent>(OnDragUpdated, TrickleDown.TrickleDown);
        RegisterCallback<DragPerformEvent>(OnDragPerform, TrickleDown.TrickleDown);

        this.AddManipulator(new ContextualMenuManipulator(menuEvent =>
        {
            Vector2 graphPos = GetGraphPosition(menuEvent.localMousePosition);

            menuEvent.menu.AppendAction("Create/Start Node", _ => CreateNodeUI<StartNode>(graphPos));
            menuEvent.menu.AppendAction("Create/Play Clip Node", _ => CreateNodeUI<PlayClipNode>(graphPos));
            menuEvent.menu.AppendAction("Create/Play Image Node", _ => CreateNodeUI<PlayImageNode>(graphPos));
            menuEvent.menu.AppendAction("Create/Condition Node", _ => CreateNodeUI<ConditionNode>(graphPos));
            menuEvent.menu.AppendAction("Create/Variable Operation Node", _ => CreateNodeUI<VariableOperationNode>(graphPos));
            menuEvent.menu.AppendAction("Create/Choice Node", _ => CreateNodeUI<ChoiceNode>(graphPos));
            menuEvent.menu.AppendAction("Create/Random Branch Node", _ => CreateNodeUI<RandomBranchNode>(graphPos));
            menuEvent.menu.AppendAction("Create/Redirect Node", _ => CreateNodeUI<RedirectNode>(graphPos));
            menuEvent.menu.AppendAction("Create/Label Node (行先)", _ =>
            {
                CreateNodeUI<LabelNode>(graphPos, node => node.labelName = GetNextLabelName());
            });
            menuEvent.menu.AppendAction("Create/Goto Node", _ => CreateNodeUI<GotoNode>(graphPos));
            menuEvent.menu.AppendAction("Create/Sub Graph Node", _ => CreateNodeUI<SubGraphNode>(graphPos));
            menuEvent.menu.AppendAction("Create/Exit Node", _ => CreateNodeUI<ExitNode>(graphPos));
        }));
    }

    public StoryNodeUI CreateNodeUI<T>(Vector2 position, Action<T> setup = null) where T : BaseNode
    {
        T nodeData = ScriptableObject.CreateInstance<T>();
        nodeData.guid = Guid.NewGuid().ToString();
        nodeData.position = position;
        setup?.Invoke(nodeData);
        return AddNodeFromData(nodeData);
    }

    public StoryNodeUI AddNodeFromData(BaseNode nodeData)
    {
        StoryNodeUI nodeUI = new StoryNodeUI(nodeData);
        AddElement(nodeUI);
        return nodeUI;
    }

    public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter nodeAdapter)
    {
        List<Port> compatiblePorts = new List<Port>();
        ports.ForEach(port =>
        {
            if (startPort != port && startPort.node != port.node && startPort.direction != port.direction)
            {
                compatiblePorts.Add(port);
            }
        });
        return compatiblePorts;
    }

    private Vector2 GetGraphPosition(Vector2 localMousePosition)
    {
        return contentViewContainer.WorldToLocal(localMousePosition);
    }

    private void OnDragUpdated(DragUpdatedEvent evt)
    {
        if (!IsEmptyGraphDropTarget(evt.target)) return;
        if (!HasDroppableDragAssets()) return;

        DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
        evt.StopPropagation();
    }

    private void OnDragPerform(DragPerformEvent evt)
    {
        if (!IsEmptyGraphDropTarget(evt.target)) return;
        if (!HasDroppableDragAssets()) return;

        DragAndDrop.AcceptDrag();
        Vector2 graphPos = contentViewContainer.WorldToLocal(evt.mousePosition);
        int created = 0;

        foreach (UnityEngine.Object obj in DragAndDrop.objectReferences)
        {
            Vector2 pos = graphPos + new Vector2(created * 220f, 0f);
            if (TryCreateNodeFromDrag(obj, pos))
            {
                created++;
            }
        }

        evt.StopPropagation();
    }

    private bool IsEmptyGraphDropTarget(IEventHandler target)
    {
        VisualElement element = target as VisualElement;
        if (element == null) return false;
        if (element is Node || element.GetFirstAncestorOfType<Node>() != null) return false;
        if (element is Edge || element.GetFirstAncestorOfType<Edge>() != null) return false;
        return true;
    }

    private bool HasDroppableDragAssets()
    {
        UnityEngine.Object[] refs = DragAndDrop.objectReferences;
        if (refs == null) return false;
        foreach (UnityEngine.Object obj in refs)
        {
            if (obj is VideoClip || obj is Texture2D || obj is Sprite) return true;
            if (obj is StoryGraph nested && !StoryGraph.WouldCreateCycle(window != null ? window.currentGraph : null, nested))
            {
                return true;
            }
        }
        return false;
    }

    private bool TryCreateNodeFromDrag(UnityEngine.Object obj, Vector2 position)
    {
        if (obj is StoryGraph nestedGraph)
        {
            if (StoryGraph.WouldCreateCycle(window != null ? window.currentGraph : null, nestedGraph))
            {
                Debug.LogError($"【SubGraph】循環参照になるためドロップできません: {nestedGraph.name}");
                return false;
            }

            CreateNodeUI<SubGraphNode>(position, node => node.subGraph = nestedGraph);
            return true;
        }

        if (obj is VideoClip clip)
        {
            CreateNodeUI<PlayClipNode>(position, node => node.clip = clip);
            return true;
        }

        Texture2D image = obj as Texture2D;
        if (image == null && obj is Sprite sprite)
        {
            image = sprite.texture;
        }

        if (image != null)
        {
            CreateNodeUI<PlayImageNode>(position, node => node.image = image);
            return true;
        }

        return false;
    }

    private string SerializeGraph(IEnumerable<GraphElement> elements)
    {
        ClipboardData clipboard = new ClipboardData();
        HashSet<string> copiedGuids = new HashSet<string>();

        foreach (GraphElement element in elements)
        {
            if (element is StoryNodeUI nodeUI && nodeUI.data != null)
            {
                copiedGuids.Add(nodeUI.guid);
                clipboard.nodes.Add(new ClipboardNode
                {
                    guid = nodeUI.guid,
                    typeName = nodeUI.data.GetType().AssemblyQualifiedName,
                    className = nodeUI.data.GetType().Name,
                    json = EditorJsonUtility.ToJson(nodeUI.data),
                    position = nodeUI.GetPosition().position
                });
            }
        }

        if (clipboard.nodes.Count == 0)
        {
            return string.Empty;
        }

        foreach (GraphElement element in graphElements)
        {
            if (element is Edge edge)
            {
                StoryNodeUI outputNode = edge.output != null ? edge.output.node as StoryNodeUI : null;
                StoryNodeUI inputNode = edge.input != null ? edge.input.node as StoryNodeUI : null;
                if (outputNode == null || inputNode == null) continue;
                if (!copiedGuids.Contains(outputNode.guid) || !copiedGuids.Contains(inputNode.guid)) continue;

                clipboard.links.Add(new StoryLinkData
                {
                    baseNodeGuid = outputNode.guid,
                    portName = edge.output.portName,
                    targetNodeGuid = inputNode.guid
                });
            }
        }

        return ClipboardMarker + JsonUtility.ToJson(clipboard);
    }

    private bool CanPaste(string data)
    {
        return !string.IsNullOrEmpty(data) && data.StartsWith(ClipboardMarker, StringComparison.Ordinal);
    }

    private void UnserializeAndPaste(string operationName, string data)
    {
        if (!TryParseClipboard(data, out ClipboardData clipboard))
        {
            return;
        }

        Vector2 offset = new Vector2(PasteOffset, PasteOffset);
        if (string.Equals(operationName, "Paste", StringComparison.OrdinalIgnoreCase))
        {
            if (data == lastPasteData)
            {
                pasteCount++;
            }
            else
            {
                pasteCount = 1;
                lastPasteData = data;
            }
            offset *= pasteCount;
        }

        Dictionary<string, string> guidMap = new Dictionary<string, string>();
        Dictionary<string, StoryNodeUI> newNodes = new Dictionary<string, StoryNodeUI>();

        ClearSelection();

        foreach (ClipboardNode item in clipboard.nodes)
        {
            Type type = ResolveNodeType(item);
            if (type == null || !typeof(BaseNode).IsAssignableFrom(type))
            {
                Debug.LogWarning($"[StoryGraph] コピーできないノード型です: {item.className}");
                continue;
            }

            BaseNode nodeData = ScriptableObject.CreateInstance(type) as BaseNode;
            if (nodeData == null) continue;

            if (!string.IsNullOrEmpty(item.json))
            {
                EditorJsonUtility.FromJsonOverwrite(item.json, nodeData);
            }

            string newGuid = Guid.NewGuid().ToString();
            if (!string.IsNullOrEmpty(item.guid))
            {
                guidMap[item.guid] = newGuid;
            }

            nodeData.guid = newGuid;
            nodeData.position = item.position + offset;
            nodeData.name = type.Name;

            if (nodeData is LabelNode pastedLabel)
            {
                HashSet<string> usedNames = CollectLabelNames();
                if (usedNames.Contains(pastedLabel.GetDisplayName()))
                {
                    pastedLabel.labelName = LabelNode.NextDefaultName(usedNames);
                }
            }

            StoryNodeUI nodeUI = AddNodeFromData(nodeData);
            AddToSelection(nodeUI);
            newNodes[newGuid] = nodeUI;
        }

        foreach (StoryNodeUI nodeUI in newNodes.Values)
        {
            if (nodeUI.data is not GotoNode gotoNode || string.IsNullOrEmpty(gotoNode.targetLabelGuid)) continue;
            if (!guidMap.TryGetValue(gotoNode.targetLabelGuid, out string remapped)) continue;

            gotoNode.targetLabelGuid = remapped;
            gotoNode.targetGraph = CurrentGraph;
        }

        foreach (StoryLinkData link in clipboard.links)
        {
            if (!guidMap.TryGetValue(link.baseNodeGuid, out string newBase)) continue;
            if (!guidMap.TryGetValue(link.targetNodeGuid, out string newTarget)) continue;
            if (!newNodes.TryGetValue(newBase, out StoryNodeUI outputNode)) continue;
            if (!newNodes.TryGetValue(newTarget, out StoryNodeUI inputNode)) continue;

            Edge edge = ConnectNodes(outputNode, link.portName, inputNode);
            if (edge != null)
            {
                AddToSelection(edge);
            }
        }
    }

    private static bool TryParseClipboard(string data, out ClipboardData clipboard)
    {
        clipboard = null;
        if (string.IsNullOrEmpty(data) || !data.StartsWith(ClipboardMarker, StringComparison.Ordinal))
        {
            return false;
        }

        string json = data.Substring(ClipboardMarker.Length);
        if (string.IsNullOrEmpty(json))
        {
            return false;
        }

        clipboard = JsonUtility.FromJson<ClipboardData>(json);
        return clipboard != null && clipboard.nodes != null && clipboard.nodes.Count > 0;
    }

    private static Type ResolveNodeType(ClipboardNode item)
    {
        Type type = null;
        if (!string.IsNullOrEmpty(item.typeName))
        {
            type = Type.GetType(item.typeName);
            if (type == null)
            {
                type = typeof(BaseNode).Assembly.GetType(item.typeName);
            }
        }

        if (type == null && !string.IsNullOrEmpty(item.className))
        {
            type = typeof(BaseNode).Assembly.GetType(item.className);
        }

        return type;
    }

    private Edge ConnectNodes(StoryNodeUI outputNode, string portName, StoryNodeUI inputNode)
    {
        Port outPort = null;
        foreach (VisualElement child in outputNode.outputContainer.Children())
        {
            if (child is Port port && port.portName == portName)
            {
                outPort = port;
                break;
            }
        }

        Port inPort = null;
        foreach (VisualElement child in inputNode.inputContainer.Children())
        {
            if (child is Port port)
            {
                inPort = port;
                break;
            }
        }

        if (outPort == null || inPort == null)
        {
            return null;
        }

        Edge edge = outPort.ConnectTo(inPort);
        AddElement(edge);
        return edge;
    }

    private string GetNextLabelName()
    {
        return LabelNode.NextDefaultName(CollectLabelNames());
    }

    private HashSet<string> CollectLabelNames()
    {
        HashSet<string> used = new HashSet<string>();
        if (CurrentGraph != null && CurrentGraph.nodes != null)
        {
            for (int i = 0; i < CurrentGraph.nodes.Count; i++)
            {
                if (CurrentGraph.nodes[i] is LabelNode saved && !string.IsNullOrEmpty(saved.labelName))
                {
                    used.Add(saved.GetDisplayName());
                }
            }
        }

        foreach (GraphElement element in graphElements)
        {
            if (element is StoryNodeUI nodeUI && nodeUI.data is LabelNode live)
            {
                used.Add(live.GetDisplayName());
            }
        }

        return used;
    }

    [Serializable]
    private class ClipboardData
    {
        public List<ClipboardNode> nodes = new List<ClipboardNode>();
        public List<StoryLinkData> links = new List<StoryLinkData>();
    }

    [Serializable]
    private class ClipboardNode
    {
        public string guid;
        public string typeName;
        public string className;
        public string json;
        public Vector2 position;
    }
}
