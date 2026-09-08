using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class StoryLabelTreePanel : MonoBehaviour
{
    private const float RowHeight = 36f;

    private static readonly List<StoryLabelTreePanel> instances = new List<StoryLabelTreePanel>();

    [Header("参照")]
    [Tooltip("このグラフから SubGraph / Goto を辿り、関連 Label をツリー表示します")]
    public StoryGraph rootGraph;
    public StoryPlayer storyPlayer;
    public RectTransform contentRoot;

    [Header("表示")]
    public GotoPanelOverride displayOverride = GotoPanelOverride.EntrySettings;
    public bool startExpanded = true;
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
    [Tooltip("1階層深くなるごとの左インデント")]
    public int indent = 22;

    private readonly List<RowView> rows = new List<RowView>();

    private class RowView
    {
        public StoryLabelTreeNode node;
        public int depth;
        public bool expanded;
        public GameObject root;
        public GameObject childrenHost;
        public Button jumpButton;
        public Button toggleButton;
        public TMP_Text toggleLabel;
        public TMP_Text titleLabel;
    }

    private void OnEnable()
    {
        if (!instances.Contains(this)) instances.Add(this);
        EnsurePlayer();
        EnsureHierarchy();
        Rebuild();
    }

    private void OnDisable()
    {
        instances.Remove(this);
    }

    private void OnValidate()
    {
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

        if (!tree.CanJump && tree.HasChildren)
        {
            for (int i = 0; i < tree.children.Count; i++)
            {
                CreateRows(tree.children[i], 0, contentRoot);
            }
        }
        else
        {
            CreateRows(tree, 0, contentRoot);
        }

        RefreshDisplay();
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
        float rowHeight = RowHeight + padTop + padBottom;

        LayoutElement layoutElement = root.GetComponent<LayoutElement>();
        layoutElement.minHeight = rowHeight;
        layoutElement.preferredHeight = rowHeight;
        layoutElement.flexibleWidth = 1f;

        HorizontalLayoutGroup rowLayout = root.GetComponent<HorizontalLayoutGroup>();
        rowLayout.padding = new RectOffset(
            padLeft + depth * Mathf.Max(0, indent),
            padRight,
            padTop,
            padBottom);
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

        if (node.HasChildren)
        {
            row.toggleButton = CreateInnerButton(root.transform, "Toggle", 28f, out row.toggleLabel);
            row.toggleLabel.text = row.expanded ? "-" : "+";
            row.toggleLabel.alignment = TextAlignmentOptions.Center;
            RowView captured = row;
            row.toggleButton.onClick.AddListener(() => Toggle(captured));
        }

        row.jumpButton = CreateInnerButton(root.transform, "Title", 0f, out row.titleLabel);
        row.jumpButton.GetComponent<LayoutElement>().flexibleWidth = 1f;
        row.titleLabel.text = node.title;
        row.titleLabel.alignment = TextAlignmentOptions.MidlineLeft;
        if (node.CanJump)
        {
            string labelName = node.label.GetDisplayName();
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

        layoutElement.minHeight = RowHeight - 4f;

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

    private void Toggle(RowView row)
    {
        row.expanded = !row.expanded;
        if (row.toggleLabel != null)
        {
            row.toggleLabel.text = row.expanded ? "-" : "+";
        }

        if (row.childrenHost != null)
        {
            row.childrenHost.SetActive(row.expanded);
        }
    }

    private void ApplyDisplay(RowView row)
    {
        if (row?.jumpButton == null || row.node == null) return;

        bool canJump = row.node.CanJump;
        bool unlocked = !canJump || StoryGotoPanel.IsUnlocked(row.node.label.GetDisplayName());
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
        ApplyContentPadding();
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
