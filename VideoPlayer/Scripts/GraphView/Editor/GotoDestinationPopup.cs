using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

public static class GotoDestinationPopup
{
    private const float RowHeight = 22f;
    private const int MaxVisibleRows = 12;
    private const float SearchRowHeight = 24f;

    private static VisualElement currentOverlay;

    public static void Close()
    {
        if (currentOverlay == null) return;
        currentOverlay.RemoveFromHierarchy();
        currentOverlay = null;
    }

    public static void Show(VisualElement anchor, IReadOnlyList<string> choices, int selectedIndex, Action<int> onSelected)
    {
        if (anchor?.panel == null || choices == null || onSelected == null) return;

        Close();

        VisualElement root = anchor.panel.visualTree;
        var controller = new Controller(root, anchor, choices, selectedIndex, onSelected);
        currentOverlay = controller.Overlay;
        root.Add(currentOverlay);
        controller.Open();
    }

    private sealed class Controller
    {
        private readonly VisualElement root;
        private readonly VisualElement anchor;
        private readonly IReadOnlyList<string> choices;
        private readonly Action<int> onSelected;
        private readonly List<int> filtered = new List<int>();
        private readonly List<VisualElement> rows = new List<VisualElement>();

        private readonly VisualElement popup;
        private readonly ToolbarSearchField search;
        private readonly ScrollView scroll;
        private readonly int selectedIndex;

        private readonly Color textColor;
        private readonly Color mutedColor;
        private int highlight;
        private bool placing;
        private float listHeight = RowHeight;

        public VisualElement Overlay { get; }

        public Controller(VisualElement root, VisualElement anchor, IReadOnlyList<string> choices, int selectedIndex, Action<int> onSelected)
        {
            this.root = root;
            this.anchor = anchor;
            this.choices = choices;
            this.onSelected = onSelected;
            this.selectedIndex = Mathf.Clamp(selectedIndex, 0, Math.Max(0, choices.Count - 1));
            highlight = this.selectedIndex;

            Overlay = new VisualElement { name = "goto-destination-popup-overlay", pickingMode = PickingMode.Position };
            Overlay.style.position = Position.Absolute;
            Overlay.style.left = 0;
            Overlay.style.top = 0;
            Overlay.style.right = 0;
            Overlay.style.bottom = 0;

            bool pro = EditorGUIUtility.isProSkin;
            textColor = pro ? new Color(0.9f, 0.9f, 0.9f) : new Color(0.13f, 0.13f, 0.13f);
            mutedColor = pro ? new Color(0.7f, 0.7f, 0.7f) : new Color(0.35f, 0.35f, 0.35f);
            Color popupBg = pro ? new Color(0.196f, 0.196f, 0.196f, 1f) : new Color(0.78f, 0.78f, 0.78f, 1f);
            Color popupBorder = pro ? new Color(0.08f, 0.08f, 0.08f, 1f) : new Color(0.55f, 0.55f, 0.55f, 1f);

            popup = new VisualElement { name = "goto-destination-popup", pickingMode = PickingMode.Position };
            popup.style.position = Position.Absolute;
            popup.style.flexDirection = FlexDirection.Column;
            popup.style.backgroundColor = new StyleColor(popupBg);
            popup.style.borderTopWidth = 1;
            popup.style.borderRightWidth = 1;
            popup.style.borderBottomWidth = 1;
            popup.style.borderLeftWidth = 1;
            popup.style.borderTopColor = new StyleColor(popupBorder);
            popup.style.borderRightColor = new StyleColor(popupBorder);
            popup.style.borderBottomColor = new StyleColor(popupBorder);
            popup.style.borderLeftColor = new StyleColor(popupBorder);
            popup.style.paddingTop = 4;
            popup.style.paddingRight = 4;
            popup.style.paddingBottom = 4;
            popup.style.paddingLeft = 4;
            popup.style.minWidth = 240;

            var searchRow = new VisualElement();
            searchRow.style.flexDirection = FlexDirection.Row;
            searchRow.style.alignItems = Align.Center;
            searchRow.style.marginBottom = 4;
            searchRow.style.height = SearchRowHeight;

            var searchLabel = new Label("検索")
            {
                style =
                {
                    minWidth = 36,
                    marginRight = 4,
                    unityTextAlign = TextAnchor.MiddleLeft,
                    color = new StyleColor(textColor)
                }
            };
            search = new ToolbarSearchField { tooltip = "行先の名前で絞り込みます。" };
            search.style.color = new StyleColor(textColor);
            search.style.flexGrow = 1;
            searchRow.Add(searchLabel);
            searchRow.Add(search);

            scroll = new ScrollView(ScrollViewMode.Vertical)
            {
                mouseWheelScrollSize = 18f,
                horizontalScrollerVisibility = ScrollerVisibility.Hidden,
                verticalScrollerVisibility = ScrollerVisibility.Auto
            };
            scroll.style.flexShrink = 0;

            popup.Add(searchRow);
            popup.Add(scroll);
            Overlay.Add(popup);

            Overlay.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.target == Overlay)
                    GotoDestinationPopup.Close();
            });
            Overlay.RegisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);
            Overlay.RegisterCallback<WheelEvent>(OnWheel, TrickleDown.TrickleDown);
            popup.RegisterCallback<GeometryChangedEvent>(_ => Place());
            search.RegisterValueChangedCallback(evt => Rebuild(evt.newValue));
        }

        public void Open()
        {
            Rebuild(string.Empty);
            Place();
            Overlay.BringToFront();
            search.schedule.Execute(() =>
            {
                ApplySearchTextColor();
                TextField text = search.Q<TextField>();
                if (text != null) text.Focus();
                else search.Focus();
            });
        }

        private void OnKeyDown(KeyDownEvent evt)
        {
            if (!string.IsNullOrEmpty(Input.compositionString))
                return;

            if (evt.keyCode == KeyCode.Escape)
            {
                GotoDestinationPopup.Close();
                evt.StopImmediatePropagation();
                return;
            }

            if (filtered.Count == 0) return;

            if (evt.keyCode == KeyCode.DownArrow)
            {
                SetHighlight(highlight + 1);
                evt.StopImmediatePropagation();
            }
            else if (evt.keyCode == KeyCode.UpArrow)
            {
                SetHighlight(highlight - 1);
                evt.StopImmediatePropagation();
            }
            else if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
            {
                Confirm();
                evt.StopImmediatePropagation();
            }
        }

        private void OnWheel(WheelEvent evt)
        {
            if (Mathf.Approximately(evt.delta.y, 0f)) return;

            Scroller scroller = scroll.verticalScroller;
            float direction = scroller.lowValue < scroller.highValue ? 1f : -1f;
            scroller.value += evt.delta.y * direction * scroll.mouseWheelScrollSize;
            evt.StopImmediatePropagation();
        }

        private void Rebuild(string query)
        {
            filtered.Clear();
            rows.Clear();
            scroll.contentContainer.Clear();

            string needle = string.IsNullOrWhiteSpace(query) ? string.Empty : query.Trim();
            for (int i = 0; i < choices.Count; i++)
            {
                string display = choices[i] ?? string.Empty;
                if (needle.Length > 0 && display.IndexOf(needle, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;
                filtered.Add(i);
            }

            int visible = Mathf.Max(filtered.Count, 1);
            listHeight = Mathf.Min(visible, MaxVisibleRows) * RowHeight;
            scroll.style.height = listHeight;
            scroll.verticalScrollerVisibility = filtered.Count > MaxVisibleRows
                ? ScrollerVisibility.AlwaysVisible
                : ScrollerVisibility.Auto;

            if (filtered.Count == 0)
            {
                var empty = new Label("該当なし")
                {
                    style =
                    {
                        height = RowHeight,
                        paddingLeft = 6,
                        unityTextAlign = TextAnchor.MiddleLeft,
                        color = new StyleColor(mutedColor)
                    }
                };
                scroll.contentContainer.Add(empty);
                highlight = -1;
                return;
            }

            int nextHighlight = 0;
            if (needle.Length == 0)
            {
                for (int i = 0; i < filtered.Count; i++)
                {
                    if (filtered[i] == selectedIndex)
                    {
                        nextHighlight = i;
                        break;
                    }
                }
            }

            for (int i = 0; i < filtered.Count; i++)
            {
                int sourceIndex = filtered[i];
                int rowIndex = i;
                string display = choices[sourceIndex] ?? string.Empty;
                var row = new VisualElement();
                row.style.flexDirection = FlexDirection.Row;
                row.style.alignItems = Align.Center;
                row.style.height = RowHeight;
                row.style.paddingLeft = 6;
                row.style.paddingRight = 6;
                row.tooltip = display;

                bool isCurrent = sourceIndex == selectedIndex;
                var label = new Label(isCurrent ? "✓  " + display : display)
                {
                    pickingMode = PickingMode.Ignore,
                    style =
                    {
                        flexGrow = 1,
                        overflow = Overflow.Hidden,
                        textOverflow = TextOverflow.Ellipsis,
                        whiteSpace = WhiteSpace.NoWrap,
                        unityTextAlign = TextAnchor.MiddleLeft,
                        color = new StyleColor(textColor)
                    }
                };
                row.Add(label);
                row.RegisterCallback<PointerEnterEvent>(_ => SetHighlight(rowIndex));
                row.RegisterCallback<PointerUpEvent>(evt =>
                {
                    if (evt.button != 0) return;
                    Pick(sourceIndex);
                    evt.StopPropagation();
                });

                rows.Add(row);
                scroll.contentContainer.Add(row);
            }

            highlight = -1;
            SetHighlight(nextHighlight);
        }

        private void SetHighlight(int index)
        {
            if (filtered.Count == 0)
            {
                highlight = -1;
                return;
            }

            index = Mathf.Clamp(index, 0, filtered.Count - 1);
            if (highlight == index && highlight >= 0 && highlight < rows.Count)
            {
                ApplyRowColor(highlight);
                return;
            }

            highlight = index;
            for (int i = 0; i < rows.Count; i++)
                ApplyRowColor(i);

            if (highlight < 0 || highlight >= rows.Count) return;

            VisualElement row = rows[highlight];
            scroll.schedule.Execute(() =>
            {
                if (row.panel != null)
                    scroll.ScrollTo(row);
            });
        }

        private void ApplyRowColor(int index)
        {
            bool on = index == highlight;
            rows[index].style.backgroundColor = on
                ? new StyleColor(new Color(0.17f, 0.36f, 0.53f, 1f))
                : new StyleColor(new Color(0f, 0f, 0f, 0f));

            Label label = rows[index].Q<Label>();
            if (label != null)
                label.style.color = new StyleColor(on ? Color.white : textColor);
        }

        private void ApplySearchTextColor()
        {
            var styleColor = new StyleColor(textColor);
            search.style.color = styleColor;
            int count = search.childCount;
            for (int i = 0; i < count; i++)
                PaintText(search[i], styleColor);
        }

        private static void PaintText(VisualElement element, StyleColor color)
        {
            if (element is TextElement)
                element.style.color = color;

            int count = element.childCount;
            for (int i = 0; i < count; i++)
                PaintText(element[i], color);
        }

        private void Confirm()
        {
            if (highlight < 0 || highlight >= filtered.Count) return;
            Pick(filtered[highlight]);
        }

        private void Pick(int sourceIndex)
        {
            Action<int> callback = onSelected;
            GotoDestinationPopup.Close();
            callback(sourceIndex);
        }

        private void Place()
        {
            if (placing || anchor.panel == null) return;
            placing = true;

            Rect anchorRect = anchor.worldBound;
            float width = Mathf.Max(280f, anchorRect.width);
            float height = popup.layout.height;
            if (float.IsNaN(height) || height < 1f)
                height = listHeight + SearchRowHeight + 12f;

            float rootWidth = root.layout.width > 1f ? root.layout.width : root.worldBound.width;
            float rootHeight = root.layout.height > 1f ? root.layout.height : root.worldBound.height;

            Vector2 below = root.WorldToLocal(new Vector2(anchorRect.xMin, anchorRect.yMax));
            float left = below.x;
            float top = below.y;
            if (left + width > rootWidth - 4f)
                left = Mathf.Max(4f, rootWidth - width - 4f);
            if (top + height > rootHeight - 4f)
            {
                Vector2 above = root.WorldToLocal(new Vector2(anchorRect.xMin, anchorRect.yMin));
                top = Mathf.Max(4f, above.y - height);
            }

            popup.style.width = width;
            popup.style.left = left;
            popup.style.top = top;
            placing = false;
        }
    }
}
