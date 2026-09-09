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
    private const float PasteViewMargin = 48f;

    private StoryGraphWindow window;
    private string lastPasteData;
    private int pasteCount;
    private Vector2 lastPointerPosition;

    public StoryGraph CurrentGraph => window != null ? window.currentGraph : null;

    public StoryGraphView(StoryGraphWindow window)
    {
        this.window = window;
        Insert(0, new GridBackground());
        SetupZoom(0.1f, 4f);
        this.AddManipulator(new ContentDragger());
        this.AddManipulator(new SelectionDragger());
        this.AddManipulator(new RectangleSelector());
        this.style.backgroundColor = new StyleColor(new Color(0.15f, 0.15f, 0.15f));

        focusable = true;
        RegisterCallback<PointerMoveEvent>(evt => lastPointerPosition = evt.position);
        RegisterCallback<PointerDownEvent>(evt =>
        {
            lastPointerPosition = evt.position;
            Focus();
        });

        serializeGraphElements = SerializeGraph;
        unserializeAndPaste = UnserializeAndPaste;
        canPasteSerializedData = CanPaste;

        graphViewChanged = OnGraphViewChanged;
        elementsAddedToGroup = (_, __) => NotifyAnnotationChanged("グループを更新");
        elementsRemovedFromGroup = (_, __) => NotifyAnnotationChanged("グループを更新");
        groupTitleChanged = (_, __) => NotifyAnnotationChanged("グループ名を変更");

        StoryGraphEditorHooks.BeginGraphEdit = undoName => window?.RecordGraphUndo(undoName);
        StoryGraphEditorHooks.EndGraphEdit = () => window?.SaveGraphAfterEdit();

        RegisterCallback<DragUpdatedEvent>(OnDragUpdated, TrickleDown.TrickleDown);
        RegisterCallback<DragPerformEvent>(OnDragPerform, TrickleDown.TrickleDown);

        this.AddManipulator(new ContextualMenuManipulator(menuEvent =>
        {
            Vector2 graphPos = GetGraphPosition(menuEvent.localMousePosition);

            menuEvent.menu.AppendAction("Create/Flow/Start Node", _ => CreateNodeUI<StartNode>(graphPos));
            menuEvent.menu.AppendAction("Create/Flow/Redirect Node", _ => CreateNodeUI<RedirectNode>(graphPos));
            menuEvent.menu.AppendAction("Create/Flow/Label Node (行先)", _ =>
            {
                CreateNodeUI<LabelNode>(graphPos, node => node.labelName = GetNextLabelName());
            });
            menuEvent.menu.AppendAction("Create/Flow/Goto Node", _ => CreateNodeUI<GotoNode>(graphPos));
            menuEvent.menu.AppendAction("Create/Flow/Sub Graph Node", _ => CreateNodeUI<SubGraphNode>(graphPos));
            menuEvent.menu.AppendAction("Create/Flow/Exit Node", _ => CreateNodeUI<ExitNode>(graphPos));

            menuEvent.menu.AppendAction("Create/Media/Play Clip Node", _ => CreateNodeUI<PlayClipNode>(graphPos));
            menuEvent.menu.AppendAction("Create/Media/Play Image Node", _ => CreateNodeUI<PlayImageNode>(graphPos));
            menuEvent.menu.AppendAction("Create/Media/Playback Speed Node", _ => CreateNodeUI<PlaybackSpeedNode>(graphPos));

            menuEvent.menu.AppendAction("Create/Variable/Variable Operation Node", _ => CreateNodeUI<VariableOperationNode>(graphPos));
            menuEvent.menu.AppendAction("Create/Variable/Bool Operation Node", _ => CreateNodeUI<BoolOperationNode>(graphPos));
            menuEvent.menu.AppendAction("Create/Variable/Condition Node", _ => CreateNodeUI<ConditionNode>(graphPos));
            menuEvent.menu.AppendAction("Create/Variable/Random Branch Node", _ => CreateNodeUI<RandomBranchNode>(graphPos));
            menuEvent.menu.AppendAction("Create/Variable/Condition Branch Node", _ => CreateNodeUI<ConditionBranchNode>(graphPos));

            menuEvent.menu.AppendAction("Create/UI/Choice Node", _ => CreateNodeUI<ChoiceNode>(graphPos));
            menuEvent.menu.AppendAction("Create/UI/Text Node", _ => CreateNodeUI<TextNode>(graphPos));
            menuEvent.menu.AppendAction("Create/Spawn/Prefab Node", _ => CreateNodeUI<SpawnPrefabNode>(graphPos));
            menuEvent.menu.AppendAction("Create/Memo", _ => CreateStickyNote(graphPos));
            menuEvent.menu.AppendSeparator();
            menuEvent.menu.AppendAction("Group Selection", _ => GroupSelection(), _ => CanGroupSelection()
                ? DropdownMenuAction.Status.Normal
                : DropdownMenuAction.Status.Disabled);
            menuEvent.menu.AppendAction("Ungroup", _ => UngroupSelection(), _ => CanUngroupSelection()
                ? DropdownMenuAction.Status.Normal
                : DropdownMenuAction.Status.Disabled);
        }));
    }

    public StoryNodeUI CreateNodeUI<T>(Vector2 position, Action<T> setup = null) where T : BaseNode
    {
        T nodeData = ScriptableObject.CreateInstance<T>();
        nodeData.guid = Guid.NewGuid().ToString();
        nodeData.position = position;
        setup?.Invoke(nodeData);
        StoryNodeUI nodeUI = AddNodeFromData(nodeData);
        window?.CommitGraphFromView("ノードを作成", true);
        return nodeUI;
    }

    public StoryStickyNote CreateStickyNote(Vector2 position)
    {
        StoryStickyNoteData data = new StoryStickyNoteData
        {
            guid = Guid.NewGuid().ToString(),
            title = "Memo",
            contents = "",
            position = new Rect(position, StickyNote.defaultSize),
            fontSize = (int)StickyNoteFontSize.Medium
        };
        StoryStickyNote note = AddStickyNote(data);
        window?.CommitGraphFromView("メモを作成", true);
        return note;
    }

    public void RestoreAnnotations(StoryGraph graph, Dictionary<string, StoryNodeUI> nodes)
    {
        if (graph == null) return;

        Dictionary<string, StoryStickyNote> stickies = new Dictionary<string, StoryStickyNote>();
        if (graph.stickyNotes != null)
        {
            for (int i = 0; i < graph.stickyNotes.Count; i++)
            {
                StoryStickyNoteData data = graph.stickyNotes[i];
                if (data == null) continue;
                StoryStickyNote note = AddStickyNote(data);
                if (!string.IsNullOrEmpty(note.guid))
                {
                    stickies[note.guid] = note;
                }
            }
        }

        if (graph.groups == null) return;
        for (int i = 0; i < graph.groups.Count; i++)
        {
            RestoreGroup(graph.groups[i], nodes, stickies);
        }
    }

    public void WriteAnnotationsTo(StoryGraph graph)
    {
        if (graph == null) return;

        if (graph.stickyNotes == null) graph.stickyNotes = new List<StoryStickyNoteData>();
        if (graph.groups == null) graph.groups = new List<StoryGroupData>();
        graph.stickyNotes.Clear();
        graph.groups.Clear();

        foreach (GraphElement element in graphElements)
        {
            if (element is StoryStickyNote note)
            {
                graph.stickyNotes.Add(CollectStickyNote(note));
            }
            else if (element is StoryGraphGroup group)
            {
                graph.groups.Add(CollectGroup(group));
            }
        }
    }

    private StoryStickyNote AddStickyNote(StoryStickyNoteData data)
    {
        StoryStickyNote note = new StoryStickyNote();
        note.guid = string.IsNullOrEmpty(data.guid) ? Guid.NewGuid().ToString() : data.guid;
        note.title = string.IsNullOrEmpty(data.title) ? "Memo" : data.title;
        note.contents = data.contents ?? "";
        Rect rect = data.position;
        if (rect.width < 8f) rect.width = StickyNote.defaultSize.x;
        if (rect.height < 8f) rect.height = StickyNote.defaultSize.y;
        note.SetPosition(rect);
        note.fontSize = (StickyNoteFontSize)data.fontSize;
        note.theme = (StickyNoteTheme)data.theme;
        note.Changed = () => NotifyAnnotationChanged("メモを編集");
        AddElement(note);
        return note;
    }

    private void RestoreGroup(StoryGroupData data, Dictionary<string, StoryNodeUI> nodes, Dictionary<string, StoryStickyNote> stickies)
    {
        if (data == null) return;

        StoryGraphGroup group = new StoryGraphGroup();
        group.guid = string.IsNullOrEmpty(data.guid) ? Guid.NewGuid().ToString() : data.guid;
        group.title = string.IsNullOrEmpty(data.title) ? "Group" : data.title;
        Rect rect = data.position;
        if (rect.width < 8f) rect.width = 220f;
        if (rect.height < 8f) rect.height = 140f;
        group.SetPosition(rect);
        AddElement(group);

        if (data.nodeGuids != null && nodes != null)
        {
            for (int i = 0; i < data.nodeGuids.Count; i++)
            {
                if (nodes.TryGetValue(data.nodeGuids[i], out StoryNodeUI nodeUI))
                {
                    group.AddElement(nodeUI);
                }
            }
        }

        if (data.stickyNoteGuids != null && stickies != null)
        {
            for (int i = 0; i < data.stickyNoteGuids.Count; i++)
            {
                if (stickies.TryGetValue(data.stickyNoteGuids[i], out StoryStickyNote note))
                {
                    group.AddElement(note);
                }
            }
        }
    }

    private void GroupSelection()
    {
        List<GraphElement> members = new List<GraphElement>();
        foreach (ISelectable selectable in selection)
        {
            if (selectable is StoryNodeUI || selectable is StoryStickyNote)
            {
                members.Add((GraphElement)selectable);
            }
        }

        if (members.Count == 0) return;

        StoryGraphGroup group = new StoryGraphGroup();
        group.guid = Guid.NewGuid().ToString();
        group.title = "Group";
        AddElement(group);
        foreach (GraphElement member in members)
        {
            group.AddElement(member);
        }

        window?.CommitGraphFromView("グループを作成", true);
    }

    private void UngroupSelection()
    {
        HashSet<Group> groups = new HashSet<Group>();
        foreach (ISelectable selectable in selection)
        {
            if (selectable is Group selectedGroup)
            {
                groups.Add(selectedGroup);
                continue;
            }

            if (selectable is GraphElement element)
            {
                Scope scope = element.GetFirstAncestorOfType<Scope>();
                if (scope is Group parentGroup)
                {
                    groups.Add(parentGroup);
                }
            }
        }

        foreach (Group group in groups)
        {
            List<GraphElement> members = new List<GraphElement>(group.containedElements);
            foreach (GraphElement member in members)
            {
                group.RemoveElement(member);
            }
            RemoveElement(group);
        }

        window?.CommitGraphFromView("グループ解除", true);
    }

    private bool CanGroupSelection()
    {
        foreach (ISelectable selectable in selection)
        {
            if (selectable is StoryNodeUI || selectable is StoryStickyNote)
            {
                return true;
            }
        }

        return false;
    }

    private bool CanUngroupSelection()
    {
        foreach (ISelectable selectable in selection)
        {
            if (selectable is Group) return true;
            if (selectable is GraphElement element && element.GetFirstAncestorOfType<Scope>() is Group)
            {
                return true;
            }
        }

        return false;
    }

    private StoryStickyNoteData CollectStickyNote(StoryStickyNote note)
    {
        return new StoryStickyNoteData
        {
            guid = note.guid,
            title = note.title,
            contents = note.contents,
            position = GetElementGraphRect(note),
            fontSize = (int)note.fontSize,
            theme = (int)note.theme
        };
    }

    private StoryGroupData CollectGroup(StoryGraphGroup group)
    {
        StoryGroupData data = new StoryGroupData
        {
            guid = group.guid,
            title = group.title,
            position = GetElementGraphRect(group)
        };

        foreach (GraphElement contained in group.containedElements)
        {
            if (contained is StoryNodeUI nodeUI && !string.IsNullOrEmpty(nodeUI.guid))
            {
                data.nodeGuids.Add(nodeUI.guid);
            }
            else if (contained is StoryStickyNote note && !string.IsNullOrEmpty(note.guid))
            {
                data.stickyNoteGuids.Add(note.guid);
            }
        }

        return data;
    }

    private Rect GetElementGraphRect(GraphElement element)
    {
        Rect rect = element.GetPosition();
        if (element.GetFirstAncestorOfType<Scope>() == null)
        {
            return rect;
        }

        Vector2 graphPos = contentViewContainer.WorldToLocal(element.worldBound.position);
        return new Rect(graphPos, rect.size);
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

    private GraphViewChange OnGraphViewChanged(GraphViewChange change)
    {
        if ((window != null && window.IsApplyingUndo) || StoryGraphEditorHooks.IgnoreGraphViewChange)
        {
            return change;
        }

        KeepGroupedNodesWhenDeletingGroup(change);

        bool structural = (change.edgesToCreate != null && change.edgesToCreate.Count > 0)
            || (change.elementsToRemove != null && change.elementsToRemove.Count > 0)
            || (change.movedElements != null && change.movedElements.Count > 0);
        bool moved = change.movedElements != null && change.movedElements.Count > 0;
        if (moved)
        {
            foreach (GraphElement element in change.movedElements)
            {
                if (element is not StoryNodeUI nodeUI || nodeUI.data == null) continue;
                if (nodeUI.TryGetLayoutPosition(out Vector2 livePos))
                {
                    nodeUI.data.position = livePos;
                }
            }
        }

        if (structural)
        {
            window?.CommitGraphFromView(DescribeGraphChange(change));
        }
        return change;
    }

    private static void KeepGroupedNodesWhenDeletingGroup(GraphViewChange change)
    {
        if (change.elementsToRemove == null || change.elementsToRemove.Count == 0) return;

        HashSet<GraphElement> keep = new HashSet<GraphElement>();
        foreach (GraphElement element in change.elementsToRemove)
        {
            if (element is not Group group) continue;
            foreach (GraphElement contained in group.containedElements)
            {
                if (contained is StoryNodeUI || contained is StoryStickyNote)
                {
                    keep.Add(contained);
                }
            }
        }

        if (keep.Count == 0) return;

        List<GraphElement> filtered = new List<GraphElement>();
        foreach (GraphElement element in change.elementsToRemove)
        {
            if (!keep.Contains(element))
            {
                filtered.Add(element);
            }
        }

        change.elementsToRemove = filtered;
    }

    private void NotifyAnnotationChanged(string undoName)
    {
        if ((window != null && window.IsApplyingUndo) || StoryGraphEditorHooks.IgnoreGraphViewChange) return;
        window?.CommitGraphFromView(undoName);
    }

    private static string DescribeGraphChange(GraphViewChange change)
    {
        bool removed = change.elementsToRemove != null && change.elementsToRemove.Count > 0;
        bool created = change.edgesToCreate != null && change.edgesToCreate.Count > 0;
        bool moved = change.movedElements != null && change.movedElements.Count > 0;

        if (removed && !created && !moved) return "グラフ要素を削除";
        if (created && !removed && !moved) return "ノードを接続";
        if (moved && !removed && !created) return "ノードを移動";
        return "グラフを編集";
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
        HashSet<string> copiedStickyGuids = new HashSet<string>();
        HashSet<GraphElement> expanded = new HashSet<GraphElement>();

        foreach (GraphElement element in elements)
        {
            expanded.Add(element);
            if (element is Group group)
            {
                foreach (GraphElement contained in group.containedElements)
                {
                    expanded.Add(contained);
                }
            }
        }

        foreach (GraphElement element in expanded)
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
                    position = GetElementGraphRect(nodeUI).position
                });
            }
            else if (element is StoryStickyNote note)
            {
                copiedStickyGuids.Add(note.guid);
                clipboard.stickyNotes.Add(CollectStickyNote(note));
            }
        }

        foreach (GraphElement element in expanded)
        {
            if (element is not StoryGraphGroup group) continue;

            ClipboardGroup groupCopy = new ClipboardGroup
            {
                title = group.title
            };
            foreach (GraphElement contained in group.containedElements)
            {
                if (contained is StoryNodeUI nodeUI && copiedGuids.Contains(nodeUI.guid))
                {
                    groupCopy.nodeGuids.Add(nodeUI.guid);
                }
                else if (contained is StoryStickyNote note && copiedStickyGuids.Contains(note.guid))
                {
                    groupCopy.stickyGuids.Add(note.guid);
                }
            }

            if (groupCopy.nodeGuids.Count > 0 || groupCopy.stickyGuids.Count > 0)
            {
                clipboard.groups.Add(groupCopy);
            }
        }

        if (clipboard.nodes.Count == 0 && clipboard.stickyNotes.Count == 0)
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
            offset = AdjustPasteOffsetIntoView(clipboard, offset);
        }

        Dictionary<string, string> guidMap = new Dictionary<string, string>();
        Dictionary<string, StoryNodeUI> newNodes = new Dictionary<string, StoryNodeUI>();
        Dictionary<string, StoryStickyNote> newStickies = new Dictionary<string, StoryStickyNote>();

        ClearSelection();

        if (clipboard.nodes != null)
        {
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
        }

        if (clipboard.stickyNotes != null)
        {
            foreach (StoryStickyNoteData item in clipboard.stickyNotes)
            {
                if (item == null) continue;
                string oldGuid = item.guid;
                StoryStickyNoteData copy = new StoryStickyNoteData
                {
                    guid = Guid.NewGuid().ToString(),
                    title = item.title,
                    contents = item.contents,
                    position = new Rect(item.position.position + offset, item.position.size),
                    fontSize = item.fontSize,
                    theme = item.theme
                };
                StoryStickyNote note = AddStickyNote(copy);
                AddToSelection(note);
                if (!string.IsNullOrEmpty(oldGuid))
                {
                    guidMap[oldGuid] = note.guid;
                    newStickies[note.guid] = note;
                }
            }
        }

        foreach (StoryNodeUI nodeUI in newNodes.Values)
        {
            if (nodeUI.data is not GotoNode gotoNode || string.IsNullOrEmpty(gotoNode.targetLabelGuid)) continue;
            if (!guidMap.TryGetValue(gotoNode.targetLabelGuid, out string remapped)) continue;

            gotoNode.targetLabelGuid = remapped;
            gotoNode.targetGraph = CurrentGraph;
        }

        if (clipboard.links != null)
        {
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

        if (clipboard.groups != null)
        {
            foreach (ClipboardGroup groupCopy in clipboard.groups)
            {
                if (groupCopy == null) continue;
                StoryGroupData restored = new StoryGroupData
                {
                    guid = Guid.NewGuid().ToString(),
                    title = string.IsNullOrEmpty(groupCopy.title) ? "Group" : groupCopy.title
                };

                if (groupCopy.nodeGuids != null)
                {
                    for (int i = 0; i < groupCopy.nodeGuids.Count; i++)
                    {
                        if (guidMap.TryGetValue(groupCopy.nodeGuids[i], out string newGuid))
                        {
                            restored.nodeGuids.Add(newGuid);
                        }
                    }
                }

                if (groupCopy.stickyGuids != null)
                {
                    for (int i = 0; i < groupCopy.stickyGuids.Count; i++)
                    {
                        if (guidMap.TryGetValue(groupCopy.stickyGuids[i], out string newStickyGuid))
                        {
                            restored.stickyNoteGuids.Add(newStickyGuid);
                        }
                    }
                }

                RestoreGroup(restored, newNodes, newStickies);
            }
        }

        window?.CommitGraphFromView("ノードを貼り付け", true);
    }

    private Vector2 AdjustPasteOffsetIntoView(ClipboardData clipboard, Vector2 offset)
    {
        if (!TryGetClipboardBounds(clipboard, out Rect sourceBounds))
        {
            return offset;
        }

        Rect viewRect = GetVisibleGraphRect();
        if (viewRect.width < 1f || viewRect.height < 1f)
        {
            return offset;
        }

        if (WouldPasteBeVisible(clipboard, offset, viewRect))
        {
            return offset;
        }

        Rect paddedView = ShrinkRect(viewRect, PasteViewMargin);
        Vector2 target = GetPreferredPasteAnchor(paddedView);
        Vector2 viewOffset = target - sourceBounds.center + offset;

        Rect pastedBounds = sourceBounds;
        pastedBounds.position += viewOffset;
        if (pastedBounds.width <= paddedView.width && pastedBounds.height <= paddedView.height)
        {
            float clampedX = Mathf.Clamp(pastedBounds.xMin, paddedView.xMin, paddedView.xMax - pastedBounds.width);
            float clampedY = Mathf.Clamp(pastedBounds.yMin, paddedView.yMin, paddedView.yMax - pastedBounds.height);
            viewOffset += new Vector2(clampedX - pastedBounds.xMin, clampedY - pastedBounds.yMin);
        }

        return viewOffset;
    }

    private static bool WouldPasteBeVisible(ClipboardData clipboard, Vector2 offset, Rect viewRect)
    {
        if (clipboard.nodes != null)
        {
            foreach (ClipboardNode item in clipboard.nodes)
            {
                if (viewRect.Contains(item.position + offset))
                {
                    return true;
                }
            }
        }

        if (clipboard.stickyNotes != null)
        {
            foreach (StoryStickyNoteData note in clipboard.stickyNotes)
            {
                if (note != null && viewRect.Contains(note.position.position + offset))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private Vector2 GetPreferredPasteAnchor(Rect viewRect)
    {
        if (worldBound.Contains(lastPointerPosition))
        {
            Vector2 mouseGraph = contentViewContainer.WorldToLocal(lastPointerPosition);
            if (viewRect.Contains(mouseGraph))
            {
                return mouseGraph;
            }
        }

        return viewRect.center;
    }

    private Rect GetVisibleGraphRect()
    {
        Rect world = worldBound;
        Vector2 min = contentViewContainer.WorldToLocal(new Vector2(world.xMin, world.yMin));
        Vector2 max = contentViewContainer.WorldToLocal(new Vector2(world.xMax, world.yMax));
        return Rect.MinMaxRect(
            Mathf.Min(min.x, max.x),
            Mathf.Min(min.y, max.y),
            Mathf.Max(min.x, max.x),
            Mathf.Max(min.y, max.y));
    }

    private static Rect ShrinkRect(Rect rect, float margin)
    {
        if (rect.width <= margin * 2f || rect.height <= margin * 2f)
        {
            return rect;
        }

        return Rect.MinMaxRect(rect.xMin + margin, rect.yMin + margin, rect.xMax - margin, rect.yMax - margin);
    }

    private static bool TryGetClipboardBounds(ClipboardData clipboard, out Rect bounds)
    {
        bounds = default;
        bool hasPoint = false;
        float xMin = float.MaxValue;
        float yMin = float.MaxValue;
        float xMax = float.MinValue;
        float yMax = float.MinValue;

        if (clipboard.nodes != null)
        {
            foreach (ClipboardNode item in clipboard.nodes)
            {
                IncludePoint(item.position, ref hasPoint, ref xMin, ref yMin, ref xMax, ref yMax);
            }
        }

        if (clipboard.stickyNotes != null)
        {
            foreach (StoryStickyNoteData note in clipboard.stickyNotes)
            {
                if (note == null) continue;
                IncludePoint(note.position.position, ref hasPoint, ref xMin, ref yMin, ref xMax, ref yMax);
            }
        }

        if (!hasPoint) return false;
        bounds = Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        return true;
    }

    private static void IncludePoint(Vector2 position, ref bool hasPoint, ref float xMin, ref float yMin, ref float xMax, ref float yMax)
    {
        xMin = Mathf.Min(xMin, position.x);
        yMin = Mathf.Min(yMin, position.y);
        xMax = Mathf.Max(xMax, position.x);
        yMax = Mathf.Max(yMax, position.y);
        hasPoint = true;
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
        return clipboard != null &&
            ((clipboard.nodes != null && clipboard.nodes.Count > 0) ||
             (clipboard.stickyNotes != null && clipboard.stickyNotes.Count > 0));
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
            if (type == null && item.typeName.IndexOf("ThresholdBranchNode", StringComparison.Ordinal) >= 0)
            {
                type = typeof(ConditionBranchNode);
            }
        }

        if (type == null && !string.IsNullOrEmpty(item.className))
        {
            type = typeof(BaseNode).Assembly.GetType(item.className);
            if (type == null && item.className == "ThresholdBranchNode")
            {
                type = typeof(ConditionBranchNode);
            }
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
        public List<StoryStickyNoteData> stickyNotes = new List<StoryStickyNoteData>();
        public List<ClipboardGroup> groups = new List<ClipboardGroup>();
    }

    [Serializable]
    private class ClipboardGroup
    {
        public string title;
        public List<string> nodeGuids = new List<string>();
        public List<string> stickyGuids = new List<string>();
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
