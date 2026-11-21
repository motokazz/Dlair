using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 関連グラフの Label を木で並べ、クリックでそのラベルへ飛ぶ。
/// </summary>
public class StoryLabelTreePanel : MonoBehaviour
{
    private const float ToggleWidth = 28f;

    private static readonly List<StoryLabelTreePanel> instances = new List<StoryLabelTreePanel>();

    [Header("参照")]
    [Tooltip("このグラフから SubGraph / Goto を辿り、関連 Label をツリー表示します")]
    public StoryGraph rootGraph;
    public StoryPlayer storyPlayer;
    public RectTransform contentRoot;

    [Header("表示")]
    public GotoPanelOverride displayOverride = GotoPanelOverride.EntrySettings;
    public bool startExpanded = true;
    [Tooltip("各行の高さ。文字サイズより低いと文字が欠けます")]
    public float rowHeight = 36f;
    public int rowFontSize = 18;
    public Color rowColor = new Color(1f, 1f, 1f, 0.08f);
    public Color textColor = new Color(0.89f, 0.89f, 0.89f, 1f);
    public Color lockedTextColor = new Color(0.55f, 0.55f, 0.55f, 1f);

    [Header("余白")]
    [Tooltip("リスト全体（Content）の左余白")]
    public int contentPaddingLeft = 4;
    [Tooltip("リスト全体の右余白")]
    public int contentPaddingRight = 4;
    [Tooltip("リスト全体の上余白")]
    public int contentPaddingTop = 4;
    [Tooltip("リスト全体の下余白")]
    public int contentPaddingBottom = 4;
    [Tooltip("各行の左余白。インデント加算前の値です")]
    public int rowPaddingLeft = 8;
    [Tooltip("各行の右余白")]
    public int rowPaddingRight = 8;
    [Tooltip("各行の上余白")]
    public int rowPaddingTop = 0;
    [Tooltip("各行の下余白")]
    public int rowPaddingBottom = 0;
    [Tooltip("1階層深くなるごとの左インデント。0 なら子のラベルが親と同じ列に揃います")]
    public int indent = 0;

    private readonly List<RowView> rows = new List<RowView>();
    private TMP_InputField searchField;
    private RectTransform searchTextArea;
    private RectTransform listArea;
    private string searchQuery = string.Empty;

    private class RowView
    {
        public StoryLabelTreeNode node;
        public int depth;
        public bool expanded;
        public GameObject root;
        public GameObject childrenHost;
        public Button jumpButton;
        public LabelNode jumpLabel;
        public Button toggleButton;
        public TMP_Text toggleLabel;
        public TMP_Text titleLabel;
    }

    private void OnEnable()
    {
        if (!instances.Contains(this)) instances.Add(this);
        StoryVisitLog.OnChanged -= RefreshDisplay;
        StoryVisitLog.OnChanged += RefreshDisplay;
        EnsurePlayer();
        EnsureHierarchy();
        Rebuild();
    }

    private void OnDisable()
    {
        StoryVisitLog.OnChanged -= RefreshDisplay;
        instances.Remove(this);
    }

    private void OnValidate()
    {
        rowHeight = Mathf.Max(1f, rowHeight);
        contentPaddingLeft = Mathf.Max(0, contentPaddingLeft);
        contentPaddingRight = Mathf.Max(0, contentPaddingRight);
        contentPaddingTop = Mathf.Max(0, contentPaddingTop);
        contentPaddingBottom = Mathf.Max(0, contentPaddingBottom);
        rowPaddingLeft = Mathf.Max(0, rowPaddingLeft);
        rowPaddingRight = Mathf.Max(0, rowPaddingRight);
        rowPaddingTop = Mathf.Max(0, rowPaddingTop);
        rowPaddingBottom = Mathf.Max(0, rowPaddingBottom);
        indent = Mathf.Max(0, indent);

        if (!isActiveAndEnabled || !Application.isPlaying) return;
#if UNITY_EDITOR
        UnityEditor.EditorApplication.delayCall -= RebuildFromInspector;
        UnityEditor.EditorApplication.delayCall += RebuildFromInspector;
#endif
    }

#if UNITY_EDITOR
    private void RebuildFromInspector()
    {
        if (this == null || !isActiveAndEnabled || !Application.isPlaying) return;
        Rebuild();
    }
#endif

    public static void RefreshAll()
    {
        for (int i = 0; i < instances.Count; i++)
        {
            if (instances[i] != null) instances[i].RefreshDisplay();
        }
    }

    public void Rebuild()
    {
        EnsurePlayer();
        EnsureHierarchy();
        ApplyContentPadding();
        ClearRows();

        StoryGraph root = rootGraph != null ? rootGraph : (storyPlayer != null ? storyPlayer.graph : null);
        StoryLabelTreeNode tree = StoryLabelTree.Build(root);
        if (tree == null) return;

        List<StoryLabelTreeNode> graphs = FlattenConnectedGraphs(tree);
        for (int i = 0; i < graphs.Count; i++)
        {
            CreateRows(graphs[i], 0, contentRoot);
        }

        RefreshDisplay();
        ApplySearch();
    }

    public void RefreshDisplay()
    {
        for (int i = 0; i < rows.Count; i++)
        {
            ApplyDisplay(rows[i]);
        }
    }

    private void CreateRows(StoryLabelTreeNode node, int depth, Transform parent)
    {
        if (node == null) return;

        RowView row = CreateRow(node, depth, parent);
        rows.Add(row);

        if (!node.HasChildren) return;

        GameObject host = new GameObject("Children", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        host.layer = gameObject.layer;
        RectTransform hostRect = host.GetComponent<RectTransform>();
        hostRect.SetParent(parent, false);
        hostRect.anchorMin = new Vector2(0f, 1f);
        hostRect.anchorMax = new Vector2(1f, 1f);
        hostRect.pivot = new Vector2(0.5f, 1f);

        VerticalLayoutGroup layout = host.GetComponent<VerticalLayoutGroup>();
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.spacing = 2f;

        ContentSizeFitter fitter = host.GetComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        row.childrenHost = host;
        host.SetActive(row.expanded);

        for (int i = 0; i < node.children.Count; i++)
        {
            CreateRows(node.children[i], depth + 1, host.transform);
        }
    }

    private RowView CreateRow(StoryLabelTreeNode node, int depth, Transform parent)
    {
        GameObject root = new GameObject(node.title, typeof(RectTransform), typeof(Image), typeof(LayoutElement), typeof(HorizontalLayoutGroup));
        root.layer = gameObject.layer;
        RectTransform rect = root.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);

        Image image = root.GetComponent<Image>();
        image.color = rowColor;

        int padLeft = Mathf.Max(0, rowPaddingLeft);
        int padRight = Mathf.Max(0, rowPaddingRight);
        int padTop = Mathf.Max(0, rowPaddingTop);
        int padBottom = Mathf.Max(0, rowPaddingBottom);
        float totalHeight = rowHeight + padTop + padBottom;

        LayoutElement layoutElement = root.GetComponent<LayoutElement>();
        layoutElement.minHeight = totalHeight;
        layoutElement.preferredHeight = totalHeight;
        layoutElement.flexibleWidth = 1f;

        HorizontalLayoutGroup rowLayout = root.GetComponent<HorizontalLayoutGroup>();
        rowLayout.padding = new RectOffset(padLeft, padRight, padTop, padBottom);
        rowLayout.spacing = 4f;
        rowLayout.childAlignment = TextAnchor.MiddleLeft;
        rowLayout.childForceExpandWidth = false;
        rowLayout.childForceExpandHeight = true;
        rowLayout.childControlWidth = true;
        rowLayout.childControlHeight = true;

        RowView row = new RowView
        {
            node = node,
            depth = depth,
            expanded = startExpanded,
            root = root
        };

        int depthIndent = depth * Mathf.Max(0, indent);
        if (depthIndent > 0)
        {
            CreateSpacer(root.transform, "Indent", depthIndent);
        }

        if (node.HasChildren)
        {
            row.toggleButton = CreateInnerButton(root.transform, "Toggle", ToggleWidth, out row.toggleLabel);
            row.toggleLabel.text = row.expanded ? "-" : "+";
            row.toggleLabel.alignment = TextAlignmentOptions.Center;
            RowView captured = row;
            row.toggleButton.onClick.AddListener(() => Toggle(captured));
        }
        else
        {
            CreateSpacer(root.transform, "ToggleSpace", ToggleWidth);
        }

        row.jumpButton = CreateInnerButton(root.transform, "Title", 0f, out row.titleLabel);
        row.jumpButton.GetComponent<LayoutElement>().flexibleWidth = 1f;
        row.titleLabel.text = node.title;
        row.titleLabel.alignment = TextAlignmentOptions.MidlineLeft;
        row.jumpLabel = depth == 0 ? FindEntryLabel(node.graph) : node.label;
        if (row.jumpLabel != null && node.graph != null)
        {
            string labelName = row.jumpLabel.GetDisplayName();
            StoryGraph destGraph = node.graph;
            row.jumpButton.onClick.AddListener(() => JumpTo(labelName, destGraph));
        }

        return row;
    }

    private Button CreateInnerButton(Transform parent, string name, float width, out TMP_Text label)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        go.layer = gameObject.layer;
        go.transform.SetParent(parent, false);

        Image image = go.GetComponent<Image>();
        image.color = Color.clear;

        LayoutElement layoutElement = go.GetComponent<LayoutElement>();
        if (width > 0f)
        {
            layoutElement.minWidth = width;
            layoutElement.preferredWidth = width;
            layoutElement.flexibleWidth = 0f;
        }
        else
        {
            layoutElement.minWidth = 40f;
            layoutElement.flexibleWidth = 1f;
        }

        layoutElement.minHeight = Mathf.Max(1f, rowHeight - 4f);

        GameObject textGo = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        textGo.layer = gameObject.layer;
        RectTransform textRect = textGo.GetComponent<RectTransform>();
        textRect.SetParent(go.transform, false);
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        label = textGo.GetComponent<TextMeshProUGUI>();
        label.font = TMP_Settings.defaultFontAsset;
        label.fontSize = rowFontSize;
        label.color = textColor;
        label.enableWordWrapping = false;
        label.overflowMode = TextOverflowModes.Ellipsis;
        label.raycastTarget = false;

        return go.GetComponent<Button>();
    }

    private static void CreateSpacer(Transform parent, string name, float width)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(LayoutElement));
        go.layer = parent.gameObject.layer;
        go.transform.SetParent(parent, false);

        LayoutElement layoutElement = go.GetComponent<LayoutElement>();
        layoutElement.minWidth = width;
        layoutElement.preferredWidth = width;
        layoutElement.flexibleWidth = 0f;
        layoutElement.minHeight = 1f;
    }

    private void Toggle(RowView row)
    {
        row.expanded = !row.expanded;
        ApplySearch();
    }

    private void OnSearchChanged(string value)
    {
        searchQuery = value ?? string.Empty;
        ApplySearch();
    }

    private void ApplySearch()
    {
        string query = string.IsNullOrWhiteSpace(searchQuery) ? string.Empty : searchQuery.Trim();
        bool filtering = query.Length > 0;
        int count = rows.Count;
        var visible = new bool[count];

        for (int i = count - 1; i >= 0; i--)
        {
            bool show = !filtering || TitleContains(rows[i].node, query);
            if (!show)
            {
                int depth = rows[i].depth;
                for (int j = i + 1; j < count && rows[j].depth > depth; j++)
                {
                    if (!visible[j]) continue;
                    show = true;
                    break;
                }
            }

            visible[i] = show;
        }

        if (filtering)
        {
            for (int i = 0; i < count; i++)
            {
                if (!TitleContains(rows[i].node, query)) continue;
                int depth = rows[i].depth;
                for (int j = i + 1; j < count && rows[j].depth > depth; j++)
                {
                    if (rows[j].depth != depth + 1) continue;
                    visible[j] = true;
                }
            }
        }

        for (int i = 0; i < count; i++)
        {
            RowView row = rows[i];
            if (row.root != null) row.root.SetActive(visible[i]);
            if (row.childrenHost == null) continue;

            bool descendant = false;
            if (filtering)
            {
                int depth = row.depth;
                for (int j = i + 1; j < count && rows[j].depth > depth; j++)
                {
                    if (!visible[j]) continue;
                    descendant = true;
                    break;
                }

                if (descendant) row.expanded = true;
            }

            bool showHost = filtering ? row.expanded && descendant : row.expanded;
            if (row.toggleLabel != null)
            {
                row.toggleLabel.text = showHost ? "-" : "+";
            }

            row.childrenHost.SetActive(showHost);
        }
    }

    private static List<StoryLabelTreeNode> FlattenConnectedGraphs(StoryLabelTreeNode root)
    {
        var graphs = new List<StoryLabelTreeNode>();
        CollectGraphHeaders(root, graphs, new HashSet<StoryGraph>());
        graphs.Sort(CompareTitle);

        for (int i = 0; i < graphs.Count; i++)
        {
            StoryLabelTreeNode graphNode = graphs[i];
            var labels = new List<StoryLabelTreeNode>();
            var seenLabels = new HashSet<LabelNode>();
            AddLabel(labels, seenLabels, graphNode.graph, graphNode.label);

            if (graphNode.children != null)
            {
                for (int c = 0; c < graphNode.children.Count; c++)
                {
                    StoryLabelTreeNode child = graphNode.children[c];
                    if (child == null || child.graph != graphNode.graph) continue;
                    AddLabel(labels, seenLabels, graphNode.graph, child.label);
                }
            }

            labels.Sort(CompareTitle);
            graphNode.children = labels;
        }

        return graphs;
    }

    private static void CollectGraphHeaders(StoryLabelTreeNode node, List<StoryLabelTreeNode> graphs, HashSet<StoryGraph> seen)
    {
        if (node == null) return;
        if (node.graph != null && seen.Add(node.graph))
        {
            graphs.Add(node);
        }

        if (node.children == null) return;
        for (int i = 0; i < node.children.Count; i++)
        {
            CollectGraphHeaders(node.children[i], graphs, seen);
        }
    }

    private static void AddLabel(List<StoryLabelTreeNode> labels, HashSet<LabelNode> seen, StoryGraph graph, LabelNode label)
    {
        if (graph == null || label == null || !seen.Add(label)) return;
        labels.Add(new StoryLabelTreeNode
        {
            title = label.GetDisplayName(),
            graph = graph,
            label = label
        });
    }

    private static int CompareTitle(StoryLabelTreeNode a, StoryLabelTreeNode b)
    {
        return string.Compare(a?.title ?? string.Empty, b?.title ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    private static bool TitleContains(StoryLabelTreeNode node, string query)
    {
        if (node == null || string.IsNullOrEmpty(node.title)) return false;
        return node.title.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static LabelNode FindEntryLabel(StoryGraph graph)
    {
        if (graph?.nodes == null) return null;

        StartNode start = null;
        LabelNode firstLabel = null;
        for (int i = 0; i < graph.nodes.Count; i++)
        {
            BaseNode current = graph.nodes[i];
            if (current is StartNode foundStart && start == null) start = foundStart;
            if (current is LabelNode label && firstLabel == null) firstLabel = label;
        }

        if (start == null) return firstLabel;

        var path = new List<string>();
        BaseNode cursor = start;
        for (int step = 0; cursor != null && step < 64; step++)
        {
            if (string.IsNullOrEmpty(cursor.guid) || path.Contains(cursor.guid)) break;
            path.Add(cursor.guid);
            cursor = graph.GetNextNode(cursor.guid, "Next");
        }

        LabelNode best = null;
        int bestPathIndex = int.MaxValue;
        int bestSteps = int.MaxValue;
        for (int i = 0; i < graph.nodes.Count; i++)
        {
            if (graph.nodes[i] is not LabelNode label) continue;
            if (!TryJoinStartPath(graph, label, path, 64, out int pathIndex, out int steps)) continue;
            if (pathIndex > bestPathIndex) continue;
            if (pathIndex == bestPathIndex && steps >= bestSteps) continue;
            bestPathIndex = pathIndex;
            bestSteps = steps;
            best = label;
        }

        return best != null ? best : firstLabel;
    }

    private static bool TryJoinStartPath(StoryGraph graph, BaseNode from, List<string> path, int limit, out int pathIndex, out int steps)
    {
        pathIndex = -1;
        steps = -1;
        BaseNode current = from;
        var seen = new HashSet<string>();
        for (int step = 0; current != null && step <= limit; step++)
        {
            if (!string.IsNullOrEmpty(current.guid))
            {
                int index = path.IndexOf(current.guid);
                if (index >= 0)
                {
                    pathIndex = index;
                    steps = step;
                    return true;
                }
            }

            if (string.IsNullOrEmpty(current.guid) || !seen.Add(current.guid)) return false;
            current = graph.GetNextNode(current.guid, "Next");
        }

        return false;
    }

    private void ApplyDisplay(RowView row)
    {
        if (row?.jumpButton == null || row.node == null) return;

        bool canJump = row.jumpLabel != null && row.node.graph != null;
        bool unlocked = !canJump || row.node.graph.IsLabelVisited(row.jumpLabel.GetDisplayName());
        bool interactable = canJump;

        if (displayOverride == GotoPanelOverride.UnlockedOnly)
        {
            interactable = canJump && unlocked;
        }
        else if (displayOverride == GotoPanelOverride.ShowAll)
        {
            interactable = canJump;
        }

        row.jumpButton.interactable = interactable;
        if (row.titleLabel != null)
        {
            row.titleLabel.color = interactable || !canJump ? textColor : lockedTextColor;
        }
    }

    private void JumpTo(string labelName, StoryGraph destGraph)
    {
        EnsurePlayer();
        if (storyPlayer == null)
        {
            Debug.LogError("【LabelTree】StoryPlayer が見つかりません。");
            return;
        }

        if (!storyPlayer.JumpFromOutside(labelName, destGraph))
        {
            Debug.LogWarning($"【LabelTree】行先 '{labelName}' へジャンプできませんでした。");
        }
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

    private void EnsureHierarchy()
    {
        ScrollRect scroll = GetComponent<ScrollRect>();
        if (contentRoot == null)
        {
            if (scroll == null)
            {
                Image background = gameObject.GetComponent<Image>();
                if (background == null) background = gameObject.AddComponent<Image>();
                background.color = new Color(0.12f, 0.12f, 0.12f, 0.85f);

                RectTransform self = transform as RectTransform;
                if (self != null)
                {
                    if (self.sizeDelta.x < 8f || self.sizeDelta.y < 8f)
                    {
                        self.anchorMin = new Vector2(0f, 1f);
                        self.anchorMax = new Vector2(0f, 1f);
                        self.pivot = new Vector2(0f, 1f);
                        self.anchoredPosition = Vector2.zero;
                        self.sizeDelta = new Vector2(280f, 520f);
                    }
                }

                GameObject viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
                viewport.layer = gameObject.layer;
                RectTransform viewportRect = viewport.GetComponent<RectTransform>();
                viewportRect.SetParent(transform, false);
                Stretch(viewportRect);
                viewport.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.02f);
                viewport.GetComponent<Mask>().showMaskGraphic = false;

                GameObject content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
                content.layer = gameObject.layer;
                contentRoot = content.GetComponent<RectTransform>();
                contentRoot.SetParent(viewportRect, false);
                contentRoot.anchorMin = new Vector2(0f, 1f);
                contentRoot.anchorMax = new Vector2(1f, 1f);
                contentRoot.pivot = new Vector2(0.5f, 1f);
                contentRoot.anchoredPosition = Vector2.zero;
                contentRoot.sizeDelta = new Vector2(0f, 0f);

                VerticalLayoutGroup layout = content.GetComponent<VerticalLayoutGroup>();
                layout.padding = MakeContentPadding();
                layout.spacing = 2f;
                layout.childAlignment = TextAnchor.UpperLeft;
                layout.childForceExpandWidth = true;
                layout.childForceExpandHeight = false;
                layout.childControlWidth = true;
                layout.childControlHeight = true;

                ContentSizeFitter fitter = content.GetComponent<ContentSizeFitter>();
                fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
                fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

                scroll = gameObject.AddComponent<ScrollRect>();
                scroll.viewport = viewportRect;
                scroll.content = contentRoot;
                scroll.horizontal = false;
                scroll.vertical = true;
                scroll.movementType = ScrollRect.MovementType.Clamped;
                scroll.scrollSensitivity = 24f;
            }
            else
            {
                contentRoot = scroll.content;
            }
        }

        if (scroll == null) scroll = GetComponent<ScrollRect>();
        EnsureScrollbar(scroll);
        EnsureSearchField(scroll);
        ApplyContentPadding();
    }

    private void EnsureSearchField(ScrollRect scroll)
    {
        if (searchField == null)
        {
            Transform existing = transform.Find("Search");
            if (existing != null) searchField = existing.GetComponent<TMP_InputField>();
        }

        if (searchField == null)
        {
            GameObject go = new GameObject("Search", typeof(RectTransform), typeof(Image));
            go.layer = gameObject.layer;
            go.SetActive(false);
            go.transform.SetParent(transform, false);

            GameObject textArea = new GameObject("Text Area", typeof(RectTransform), typeof(RectMask2D));
            textArea.layer = gameObject.layer;
            searchTextArea = textArea.GetComponent<RectTransform>();
            searchTextArea.SetParent(go.transform, false);

            TMP_Text placeholder = CreateSearchText(searchTextArea, "Placeholder", "検索");
            placeholder.raycastTarget = false;
            TMP_Text text = CreateSearchText(searchTextArea, "Text", string.Empty);

            searchField = go.AddComponent<TMP_InputField>();
            searchField.targetGraphic = go.GetComponent<Image>();
            searchField.textViewport = searchTextArea;
            searchField.textComponent = text;
            searchField.placeholder = placeholder;
            searchField.lineType = TMP_InputField.LineType.SingleLine;
            searchField.resetOnDeActivation = false;
            searchField.onValueChanged.AddListener(OnSearchChanged);
            go.SetActive(true);
        }

        if (searchTextArea == null && searchField.textViewport != null)
        {
            searchTextArea = searchField.textViewport;
        }

        ApplySearchChrome(scroll);
    }

    private TMP_Text CreateSearchText(RectTransform parent, string name, string text)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.layer = gameObject.layer;
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        Stretch(rect);

        TMP_Text label = go.GetComponent<TextMeshProUGUI>();
        label.text = text;
        label.font = TMP_Settings.defaultFontAsset;
        label.fontSize = rowFontSize;
        label.color = textColor;
        label.alignment = TextAlignmentOptions.MidlineLeft;
        label.enableWordWrapping = false;
        label.overflowMode = TextOverflowModes.Ellipsis;
        label.richText = false;
        return label;
    }

    private void ApplySearchChrome(ScrollRect scroll)
    {
        if (searchField == null) return;

        float height = Mathf.Max(1f, rowHeight);
        RectTransform rect = searchField.transform as RectTransform;
        if (rect != null)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(0f, height);
        }

        Image image = searchField.GetComponent<Image>();
        if (image != null) image.color = rowColor;

        int padLeft = Mathf.Max(0, rowPaddingLeft);
        int padRight = Mathf.Max(0, rowPaddingRight);
        int padTop = Mathf.Max(0, rowPaddingTop);
        int padBottom = Mathf.Max(0, rowPaddingBottom);
        if (searchTextArea != null)
        {
            searchTextArea.anchorMin = Vector2.zero;
            searchTextArea.anchorMax = Vector2.one;
            searchTextArea.offsetMin = new Vector2(padLeft, padBottom);
            searchTextArea.offsetMax = new Vector2(-padRight, -padTop);
        }

        searchField.pointSize = rowFontSize;
        searchField.customCaretColor = true;
        searchField.caretColor = textColor;
        if (searchField.textComponent != null)
        {
            searchField.textComponent.fontSize = rowFontSize;
            searchField.textComponent.color = textColor;
        }

        if (searchField.placeholder is TMP_Text placeholder)
        {
            placeholder.fontSize = rowFontSize;
            Color color = textColor;
            color.a *= 0.45f;
            placeholder.color = color;
        }

        PlaceListBelowSearch(scroll, height);
        if (rect != null) rect.SetAsLastSibling();
    }

    private void PlaceListBelowSearch(ScrollRect scroll, float height)
    {
        if (listArea == null)
        {
            Transform existing = transform.Find("ListArea");
            if (existing != null) listArea = existing as RectTransform;
        }

        if (listArea == null)
        {
            GameObject go = new GameObject("ListArea", typeof(RectTransform));
            go.layer = gameObject.layer;
            listArea = go.GetComponent<RectTransform>();
            listArea.SetParent(transform, false);
        }

        listArea.anchorMin = Vector2.zero;
        listArea.anchorMax = Vector2.one;
        listArea.pivot = new Vector2(0.5f, 0.5f);
        listArea.offsetMin = Vector2.zero;
        listArea.offsetMax = new Vector2(0f, -height);

        if (scroll == null) return;

        if (scroll.viewport != null && scroll.viewport.parent != listArea)
        {
            scroll.viewport.SetParent(listArea, false);
            Stretch(scroll.viewport);
        }

        RectTransform bar = scroll.verticalScrollbar != null ? scroll.verticalScrollbar.transform as RectTransform : null;
        if (bar != null && bar.parent != listArea)
        {
            bar.SetParent(listArea, false);
        }
    }

    private void ApplyContentPadding()
    {
        if (contentRoot == null) return;
        VerticalLayoutGroup layout = contentRoot.GetComponent<VerticalLayoutGroup>();
        if (layout == null) return;
        layout.padding = MakeContentPadding();
    }

    private RectOffset MakeContentPadding()
    {
        return new RectOffset(
            Mathf.Max(0, contentPaddingLeft),
            Mathf.Max(0, contentPaddingRight),
            Mathf.Max(0, contentPaddingTop),
            Mathf.Max(0, contentPaddingBottom));
    }

    private void EnsureScrollbar(ScrollRect scroll)
    {
        if (scroll == null || scroll.verticalScrollbar != null) return;

        const float barWidth = 12f;
        if (scroll.viewport != null)
        {
            Vector2 max = scroll.viewport.offsetMax;
            scroll.viewport.offsetMax = new Vector2(-barWidth, max.y);
        }

        GameObject barGo = new GameObject("Scrollbar", typeof(RectTransform), typeof(Image), typeof(Scrollbar));
        barGo.layer = gameObject.layer;
        RectTransform barRect = barGo.GetComponent<RectTransform>();
        barRect.SetParent(transform, false);
        barRect.anchorMin = new Vector2(1f, 0f);
        barRect.anchorMax = new Vector2(1f, 1f);
        barRect.pivot = new Vector2(1f, 1f);
        barRect.anchoredPosition = Vector2.zero;
        barRect.sizeDelta = new Vector2(barWidth, 0f);
        barGo.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.06f);

        GameObject sliding = new GameObject("Sliding Area", typeof(RectTransform));
        sliding.layer = gameObject.layer;
        RectTransform slidingRect = sliding.GetComponent<RectTransform>();
        slidingRect.SetParent(barRect, false);
        Stretch(slidingRect);

        GameObject handle = new GameObject("Handle", typeof(RectTransform), typeof(Image));
        handle.layer = gameObject.layer;
        RectTransform handleRect = handle.GetComponent<RectTransform>();
        handleRect.SetParent(slidingRect, false);
        Stretch(handleRect);
        handle.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.28f);

        Scrollbar bar = barGo.GetComponent<Scrollbar>();
        bar.handleRect = handleRect;
        bar.direction = Scrollbar.Direction.BottomToTop;
        bar.value = 1f;
        bar.size = 1f;

        scroll.verticalScrollbar = bar;
        scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;
        scroll.verticalScrollbarSpacing = 0f;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private void ClearRows()
    {
        rows.Clear();
        if (contentRoot == null) return;
        for (int i = contentRoot.childCount - 1; i >= 0; i--)
        {
            GameObject child = contentRoot.GetChild(i).gameObject;
            if (Application.isPlaying) Destroy(child);
            else DestroyImmediate(child);
        }
    }
}
