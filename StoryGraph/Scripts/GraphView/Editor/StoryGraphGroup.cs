using System;
using System.Collections.Generic;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// グラフ編集画面のグループ。ノードとメモを入れ、グループの入れ子は受け付けない。
/// 説明は StoryGraphGroup.Summary。
/// </summary>
public class StoryGraphGroup : Group
{
    public const string Summary =
        "選択したノードとメモを、タイトル付きのグループにまとめます。グループの中にグループは入れられません。\n" +
        "タイトルはダブルクリックで変更できます。\n" +
        "左のボタンで中身を折りたたみます。外へつながる線は、折りたたんだグループの端に残ります。折りたたみはグラフに保存されます。";

    public string guid;
    public Action Changed;

    private readonly Dictionary<GraphElement, Rect> storedRects = new Dictionary<GraphElement, Rect>();
    private readonly Button foldButton;
    private readonly Label helpMark;
    private readonly TextField titleEditor;
    private readonly Label titleLabel;
    private readonly VisualElement contentPlaceholder;
    private readonly IVisualElementScheduledItem collapsedEdgePump;
    private bool applying;
    private bool editingTitle;
    private bool refreshingEdges;

    public bool collapsed { get; private set; }

    public StoryGraphGroup()
    {
        headerContainer.pickingMode = PickingMode.Position;
        contentPlaceholder = this.Q("contentContainerPlaceholder");
        titleLabel = this.Q<Label>("titleLabel");
        if (titleLabel != null)
        {
            titleLabel.tooltip = "ダブルクリックでタイトルを変更";
            titleLabel.pickingMode = PickingMode.Position;
            titleLabel.style.flexGrow = 1;
        }

        VisualElement titleContainer = this.Q("titleContainer");
        if (titleContainer != null)
        {
            titleContainer.style.flexGrow = 1;
            titleContainer.style.flexShrink = 1;
            titleContainer.style.minWidth = 48;
            titleContainer.pickingMode = PickingMode.Position;
        }

        TextField builtinTitle = this.Q<TextField>("titleField");
        if (builtinTitle != null)
        {
            builtinTitle.style.display = DisplayStyle.None;
        }

        titleEditor = new TextField();
        titleEditor.style.display = DisplayStyle.None;
        titleEditor.style.flexGrow = 1;
        titleEditor.style.minWidth = 48;
        titleEditor.style.marginLeft = 2;
        titleEditor.style.marginRight = 2;
        titleEditor.RegisterCallback<FocusOutEvent>(OnTitleFocusOut);
        titleEditor.RegisterCallback<KeyDownEvent>(OnTitleKeyDown);
        titleEditor.RegisterCallback<PointerDownEvent>(evt => evt.StopPropagation());
        titleEditor.RegisterCallback<MouseDownEvent>(evt => evt.StopPropagation());
        if (titleContainer != null)
        {
            titleContainer.Add(titleEditor);
        }
        else
        {
            headerContainer.Add(titleEditor);
        }

        foldButton = new Button
        {
            text = "▼",
            tooltip = "グループの折りたたみと展開",
            focusable = false
        };
        StyleHeaderControl(foldButton, 22f);
        foldButton.RegisterCallback<PointerDownEvent>(OnFoldPointerDown, TrickleDown.TrickleDown);
        foldButton.RegisterCallback<MouseDownEvent>(StopHeaderMouse, TrickleDown.TrickleDown);
        headerContainer.Insert(0, foldButton);

        helpMark = new Label("？")
        {
            tooltip = Summary,
            focusable = false
        };
        StyleHeaderControl(helpMark, 16f);
        helpMark.RegisterCallback<PointerDownEvent>(StopHeaderPointer, TrickleDown.TrickleDown);
        helpMark.RegisterCallback<MouseDownEvent>(StopHeaderMouse, TrickleDown.TrickleDown);
        headerContainer.Add(helpMark);

        RegisterCallback<PointerDownEvent>(OnHeaderPointerDown, TrickleDown.TrickleDown);
        RegisterCallback<MouseDownEvent>(OnHeaderMouseDown, TrickleDown.TrickleDown);
        RegisterCallback<GeometryChangedEvent>(_ =>
        {
            if (collapsed && !applying)
            {
                RefreshCollapsedEdges();
            }
        });
        RegisterCallback<DetachFromPanelEvent>(_ =>
        {
            collapsedEdgePump?.Pause();
            ShowStoredMembers();
        });

        collapsedEdgePump = schedule.Execute(RefreshCollapsedEdges).Every(32);
        collapsedEdgePump.Pause();
    }

    /// <summary>
    /// 折りたたみを切り替える。notify がオンのとき、保存側へ知らせる。
    /// </summary>
    public void SetCollapsed(bool value, bool notify)
    {
        bool changed = collapsed != value;
        if (changed && value)
        {
            RememberMembers();
        }

        collapsed = value;
        ApplyPresentation();

        if (notify && changed)
        {
            Changed?.Invoke();
        }
    }

    /// <summary>
    /// 折りたたみ中に覚えてあるメンバーの矩形。展開中は false。
    /// </summary>
    public bool TryGetStoredRect(GraphElement element, out Rect rect)
    {
        rect = default;
        if (!collapsed || element == null) return false;
        return storedRects.TryGetValue(element, out rect);
    }

    public override bool AcceptsElement(GraphElement element, ref string reasonWhyNotAccepted)
    {
        if (element is StoryGraphGroup)
        {
            reasonWhyNotAccepted = "グループを入れ子にはできません。";
            return false;
        }

        if (element is StoryNodeUI || element is StoryStickyNote)
        {
            return true;
        }

        return base.AcceptsElement(element, ref reasonWhyNotAccepted);
    }

    public override void SetPosition(Rect newPos)
    {
        if (!collapsed)
        {
            base.SetPosition(newPos);
            return;
        }

        Vector2 delta = newPos.position - GetPosition().position;
        SetScopePositionOnly(new Rect(newPos.position, GetPosition().size));
        RefreshCollapsedEdges();
        if (delta.sqrMagnitude < 0.0001f) return;

        foreach (GraphElement element in containedElements)
        {
            if (!storedRects.TryGetValue(element, out Rect rect)) continue;
            rect.position += delta;
            storedRects[element] = rect;
            element.SetPosition(rect);
            if (element is StoryNodeUI node && node.data != null)
            {
                node.data.position = rect.position;
            }
        }
    }

    protected override void OnElementsAdded(IEnumerable<GraphElement> elements)
    {
        base.OnElementsAdded(elements);
        if (!collapsed || elements == null) return;

        foreach (GraphElement element in elements)
        {
            Remember(element);
        }

        ApplyPresentation();
    }

    protected override void OnElementsRemoved(IEnumerable<GraphElement> elements)
    {
        List<GraphElement> removed = elements != null ? new List<GraphElement>(elements) : new List<GraphElement>();
        foreach (GraphElement element in removed)
        {
            element.style.display = DisplayStyle.Flex;
            if (storedRects.TryGetValue(element, out Rect rect))
            {
                element.SetPosition(rect);
                storedRects.Remove(element);
            }
        }

        base.OnElementsRemoved(removed);
        RestoreEdgesTouching(removed);
        if (collapsed)
        {
            RefreshCollapsedEdges();
        }
    }

    protected override void OnGroupRenamed(string oldName, string newName)
    {
        if (!string.IsNullOrWhiteSpace(newName)) return;

        string fallback = string.IsNullOrWhiteSpace(oldName) ? "Group" : oldName.Trim();
        if (title != fallback)
        {
            title = fallback;
        }
    }

    private void OnFoldPointerDown(PointerDownEvent evt)
    {
        if (evt.button != (int)MouseButton.LeftMouse) return;
        evt.StopImmediatePropagation();
        SetCollapsed(!collapsed, true);
    }

    private static void StopHeaderPointer(PointerDownEvent evt)
    {
        evt.StopImmediatePropagation();
    }

    private static void StopHeaderMouse(MouseDownEvent evt)
    {
        evt.StopImmediatePropagation();
    }

    private void ShowStoredMembers()
    {
        bool wasCollapsed = collapsed;
        List<GraphElement> members = new List<GraphElement>(containedElements);
        foreach (GraphElement element in members)
        {
            element.style.display = DisplayStyle.Flex;
            if (!wasCollapsed || !storedRects.TryGetValue(element, out Rect rect)) continue;
            element.SetPosition(rect);
        }

        RestoreEdgesTouching(members);
    }

    private void RememberMembers()
    {
        foreach (GraphElement element in containedElements)
        {
            Remember(element);
        }
    }

    private void Remember(GraphElement element)
    {
        if (element == null) return;

        Rect rect = element.GetPosition();
        if (element is StoryNodeUI node && node.data != null)
        {
            Vector2 position = node.TryGetLayoutPosition(out Vector2 live) ? live : node.data.position;
            rect.position = position;
            node.data.position = position;
        }
        else if (rect.width <= 1f || rect.height <= 1f)
        {
            if (TryInline(element.style.left, out float x)) rect.x = x;
            if (TryInline(element.style.top, out float y)) rect.y = y;
        }

        if (rect.width <= 1f)
        {
            rect.width = ReadInlineLength(element.style.width, element is StoryStickyNote ? StickyNote.defaultSize.x : rect.width);
        }

        if (rect.height <= 1f)
        {
            rect.height = ReadInlineLength(element.style.height, element is StoryStickyNote ? StickyNote.defaultSize.y : rect.height);
        }

        storedRects[element] = rect;
    }

    private void ApplyPresentation()
    {
        if (applying) return;
        applying = true;
        try
        {
            if (collapsed)
            {
                autoUpdateGeometry = false;
            }

            DisplayStyle display = collapsed ? DisplayStyle.None : DisplayStyle.Flex;
            foreach (GraphElement element in containedElements)
            {
                if (!collapsed && storedRects.TryGetValue(element, out Rect rect))
                {
                    element.SetPosition(rect);
                }

                element.style.display = display;
            }

            if (contentPlaceholder != null)
            {
                contentPlaceholder.style.display = display;
            }

            if (!collapsed)
            {
                storedRects.Clear();
            }

            if (collapsed)
            {
                style.minWidth = 140f;
            }
            else
            {
                style.minWidth = StyleKeyword.Auto;
                autoUpdateGeometry = true;
            }

            if (foldButton != null)
            {
                foldButton.text = collapsed ? "▶" : "▼";
            }

            if (collapsed)
            {
                collapsedEdgePump?.Resume();
                RefreshCollapsedEdges();
            }
            else
            {
                collapsedEdgePump?.Pause();
                RestoreEdgesTouching(containedElements);
            }
        }
        finally
        {
            applying = false;
        }
    }

    private void RefreshCollapsedEdges()
    {
        if (refreshingEdges || !collapsed) return;

        refreshingEdges = true;
        try
        {
            GraphView graphView = GetFirstAncestorOfType<GraphView>();
            if (graphView == null) return;

            Rect bounds = worldBound;
            if (bounds.width < 1f || bounds.height < 1f) return;

            foreach (GraphElement element in graphView.graphElements)
            {
                if (element is not Edge edge) continue;
                if (CollapsedOwner(edge.output) != this && CollapsedOwner(edge.input) != this) continue;
                edge.UpdateEdgeControl();
            }
        }
        finally
        {
            refreshingEdges = false;
        }
    }

    /// <summary>
    /// 折りたたみ中のグループ端。outputSide がオンなら右、オフなら左。
    /// </summary>
    public Vector2 EdgeAnchor(Edge edge, bool outputSide)
    {
        Rect bounds = worldBound;
        int index = 0;
        int count = 0;
        GraphView graphView = GetFirstAncestorOfType<GraphView>();
        if (graphView != null && edge != null)
        {
            foreach (GraphElement element in graphView.graphElements)
            {
                if (element is not Edge other) continue;
                StoryGraphGroup outputOwner = CollapsedOwner(other.output);
                StoryGraphGroup inputOwner = CollapsedOwner(other.input);
                if (outputOwner == this && inputOwner == this) continue;

                bool onSide = outputSide ? outputOwner == this : inputOwner == this;
                if (!onSide) continue;
                if (other == edge) index = count;
                count++;
            }
        }

        if (count < 1) count = 1;
        float y = bounds.yMin + bounds.height * (index + 1f) / (count + 1f);
        float x = outputSide ? bounds.xMax : bounds.xMin;
        return edge != null ? edge.WorldToLocal(new Vector2(x, y)) : new Vector2(x, y);
    }

    private void RestoreEdgesTouching(IEnumerable<GraphElement> elements)
    {
        if (elements == null) return;

        GraphView graphView = GetFirstAncestorOfType<GraphView>();
        HashSet<Node> nodes = new HashSet<Node>();
        foreach (GraphElement element in elements)
        {
            if (element is Node node)
            {
                nodes.Add(node);
            }

            if (graphView == null)
            {
                graphView = element.GetFirstAncestorOfType<GraphView>();
            }
        }

        if (graphView == null || nodes.Count == 0) return;

        foreach (GraphElement element in graphView.graphElements)
        {
            if (element is not Edge edge) continue;
            Node outputNode = edge.output != null ? edge.output.node : null;
            Node inputNode = edge.input != null ? edge.input.node : null;
            if (!nodes.Contains(outputNode) && !nodes.Contains(inputNode)) continue;

            edge.style.display = DisplayStyle.Flex;
            edge.UpdateEdgeControl();
        }

        HashSet<Node> captured = nodes;
        graphView.schedule.Execute(() =>
        {
            foreach (GraphElement element in graphView.graphElements)
            {
                if (element is not Edge edge) continue;
                Node outputNode = edge.output != null ? edge.output.node : null;
                Node inputNode = edge.input != null ? edge.input.node : null;
                if (!captured.Contains(outputNode) && !captured.Contains(inputNode)) continue;
                edge.UpdateEdgeControl();
            }
        });
    }

    private static StoryGraphGroup CollapsedOwner(Port port)
    {
        if (port == null || port.node == null) return null;
        return port.node.GetContainingScope() is StoryGraphGroup group && group.collapsed ? group : null;
    }

    private void OnHeaderPointerDown(PointerDownEvent evt)
    {
        if (evt.clickCount < 2 || evt.button != (int)MouseButton.LeftMouse) return;
        TryBeginTitleEdit(evt.target as VisualElement, evt);
    }

    private void OnHeaderMouseDown(MouseDownEvent evt)
    {
        if (evt.clickCount < 2 || evt.button != (int)MouseButton.LeftMouse) return;
        TryBeginTitleEdit(evt.target as VisualElement, evt);
    }

    private void TryBeginTitleEdit(VisualElement target, EventBase evt)
    {
        if (IsChrome(target) || !IsInHeader(target)) return;
        evt.StopImmediatePropagation();
        if (!editingTitle)
        {
            OpenTitleEditor();
        }
    }

    private bool IsInHeader(VisualElement element)
    {
        VisualElement current = element;
        while (current != null && current != this)
        {
            if (current == headerContainer) return true;
            current = current.parent;
        }

        return false;
    }

    private bool IsChrome(VisualElement element)
    {
        VisualElement current = element;
        while (current != null && current != this)
        {
            if (current == foldButton || current == helpMark || current == titleEditor) return true;
            current = current.parent;
        }

        return false;
    }

    private void OpenTitleEditor()
    {
        if (titleEditor == null || editingTitle) return;

        editingTitle = true;
        titleEditor.SetValueWithoutNotify(title ?? "Group");
        titleEditor.style.display = DisplayStyle.Flex;
        if (titleLabel != null)
        {
            titleLabel.style.display = DisplayStyle.None;
        }

        titleEditor.schedule.Execute(() =>
        {
            if (!editingTitle) return;
            titleEditor.Focus();
            VisualElement input = titleEditor.Q(TextField.textInputUssName);
            input?.Focus();
            titleEditor.SelectAll();
        });
    }

    private void OnTitleFocusOut(FocusOutEvent evt)
    {
        if (evt.relatedTarget is VisualElement next && titleEditor != null && (next == titleEditor || titleEditor.Contains(next)))
        {
            return;
        }

        CommitTitleEdit();
    }

    private void OnTitleKeyDown(KeyDownEvent evt)
    {
        if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
        {
            CommitTitleEdit();
            evt.StopPropagation();
        }
        else if (evt.keyCode == KeyCode.Escape)
        {
            CancelTitleEdit();
            evt.StopPropagation();
        }
    }

    private void CommitTitleEdit()
    {
        if (!editingTitle || titleEditor == null) return;

        editingTitle = false;
        string next = (titleEditor.value ?? "").Trim();
        if (string.IsNullOrEmpty(next))
        {
            next = string.IsNullOrWhiteSpace(title) ? "Group" : title.Trim();
        }

        CloseTitleEditor();
        if (title != next)
        {
            title = next;
        }
    }

    private void CancelTitleEdit()
    {
        if (!editingTitle) return;
        editingTitle = false;
        CloseTitleEditor();
    }

    private void CloseTitleEditor()
    {
        if (titleEditor != null)
        {
            titleEditor.style.display = DisplayStyle.None;
        }

        if (titleLabel != null)
        {
            titleLabel.style.display = DisplayStyle.Flex;
        }
    }

    private static void StyleHeaderControl(VisualElement element, float size)
    {
        element.style.width = size;
        element.style.minWidth = size;
        element.style.height = size;
        element.style.marginLeft = 2f;
        element.style.marginRight = 2f;
        element.style.marginTop = 2f;
        element.style.marginBottom = 2f;
        element.style.paddingLeft = 0f;
        element.style.paddingRight = 0f;
        element.style.paddingTop = 0f;
        element.style.paddingBottom = 0f;
        element.style.unityTextAlign = TextAnchor.MiddleCenter;
        element.style.unityFontStyleAndWeight = FontStyle.Bold;
        element.style.fontSize = 11;
        element.style.flexShrink = 0;
    }

    private static bool TryInline(StyleLength length, out float value)
    {
        value = 0f;
        if (length.keyword != StyleKeyword.Undefined) return false;
        value = length.value.value;
        return true;
    }

    private static float ReadInlineLength(StyleLength length, float fallback)
    {
        if (length.keyword == StyleKeyword.Undefined && length.value.value > 1f)
        {
            return length.value.value;
        }

        return fallback;
    }
}

/// <summary>
/// 折りたたんだグループの端に、外へつながる線を寄せる。
/// </summary>
public class StoryGraphEdge : Edge
{
    private bool updating;

    public override bool UpdateEdgeControl()
    {
        if (updating) return false;

        updating = true;
        try
        {
            StoryGraphGroup outputOwner = CollapsedGroup(output);
            StoryGraphGroup inputOwner = CollapsedGroup(input);
            if (outputOwner != null && outputOwner == inputOwner)
            {
                style.display = DisplayStyle.None;
                return false;
            }

            if (resolvedStyle.display == DisplayStyle.None)
            {
                style.display = DisplayStyle.Flex;
            }

            bool updated = base.UpdateEdgeControl();
            if (edgeControl == null) return updated;

            if (outputOwner != null)
            {
                edgeControl.from = outputOwner.EdgeAnchor(this, true);
            }

            if (inputOwner != null)
            {
                edgeControl.to = inputOwner.EdgeAnchor(this, false);
            }

            if (outputOwner != null || inputOwner != null)
            {
                edgeControl.UpdateLayout();
            }

            return updated;
        }
        finally
        {
            updating = false;
        }
    }

    private static StoryGraphGroup CollapsedGroup(Port port)
    {
        if (port == null || port.node == null) return null;
        return port.node.GetContainingScope() is StoryGraphGroup group && group.collapsed ? group : null;
    }
}
