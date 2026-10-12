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
    private const string QueryPrefsKey = "Dlair.GotoDestinationPopup.Query";

    private static VisualElement currentOverlay;
    private static Controller current;
    private static string activePrefsKey = QueryPrefsKey;
    private static string lastQuery = EditorPrefs.GetString(QueryPrefsKey, string.Empty);

    public static void Close()
    {
        if (current != null)
        {
            Remember(current.Query);
            current = null;
        }

        if (currentOverlay == null) return;
        currentOverlay.RemoveFromHierarchy();
        currentOverlay = null;
    }

    private static void Remember(string query)
    {
        lastQuery = query ?? string.Empty;
        EditorPrefs.SetString(activePrefsKey, lastQuery);
    }

    /// <summary>
    /// 検索付きの選択一覧を開く。検索文字は prefsKey ごとに覚え、次に開いたときも入っている。
    /// </summary>
    public static void Show(VisualElement anchor, IReadOnlyList<string> choices, int selectedIndex, Action<int> onSelected, string queryPrefsKey = null, string searchTooltip = null)
    {
        if (anchor?.panel == null || choices == null || onSelected == null) return;

        Close();
        activePrefsKey = string.IsNullOrEmpty(queryPrefsKey) ? QueryPrefsKey : queryPrefsKey;
        lastQuery = EditorPrefs.GetString(activePrefsKey, string.Empty);

        VisualElement root = anchor.panel.visualTree;
        var controller = new Controller(root, anchor, choices, selectedIndex, onSelected, searchTooltip);
        current = controller;
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
        private readonly VisualElement listHost;
        private readonly VisualElement viewport;
        private readonly VisualElement content;
        private readonly VisualElement scrollbar;
        private readonly VisualElement thumb;
        private readonly int selectedIndex;

        private readonly Color textColor;
        private int highlight;
        private bool placing;
        private bool draggingThumb;
        private float dragStartY;
        private float dragStartOffset;
        private float listHeight = RowHeight;
        private float scrollOffset;

        public VisualElement Overlay { get; }

        public Controller(VisualElement root, VisualElement anchor, IReadOnlyList<string> choices, int selectedIndex, Action<int> onSelected, string searchTooltip)
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
            textColor = Color.white;
            Color popupBg = pro ? new Color(0.196f, 0.196f, 0.196f, 1f) : new Color(0.22f, 0.22f, 0.22f, 1f);
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
            search = new ToolbarSearchField
            {
                tooltip = string.IsNullOrEmpty(searchTooltip) ? "行先の名前で絞り込みます。" : searchTooltip
            };
            search.style.color = new StyleColor(textColor);
            search.style.flexGrow = 1;
            searchRow.Add(searchLabel);
            searchRow.Add(search);

            listHost = new VisualElement();
            listHost.style.flexDirection = FlexDirection.Row;
            listHost.style.flexGrow = 0;
            listHost.style.flexShrink = 0;
            listHost.style.alignItems = Align.Stretch;

            viewport = new VisualElement();
            viewport.style.flexGrow = 1;
            viewport.style.flexShrink = 1;
            viewport.style.overflow = Overflow.Hidden;
            viewport.style.position = Position.Relative;

            content = new VisualElement();
            content.style.position = Position.Absolute;
            content.style.left = 0;
            content.style.right = 0;
            content.style.flexDirection = FlexDirection.Column;
            content.style.flexGrow = 0;
            content.style.flexShrink = 0;
            content.style.justifyContent = Justify.FlexStart;
            viewport.Add(content);

            scrollbar = new VisualElement();
            scrollbar.style.width = 16;
            scrollbar.style.flexShrink = 0;
            scrollbar.style.marginLeft = 4;
            scrollbar.style.backgroundColor = new StyleColor(new Color(0.08f, 0.08f, 0.08f, 1f));
            scrollbar.style.borderTopLeftRadius = 3;
            scrollbar.style.borderTopRightRadius = 3;
            scrollbar.style.borderBottomLeftRadius = 3;
            scrollbar.style.borderBottomRightRadius = 3;

            thumb = new VisualElement();
            thumb.style.position = Position.Absolute;
            thumb.style.left = 2;
            thumb.style.right = 2;
            thumb.style.backgroundColor = new StyleColor(new Color(0.92f, 0.92f, 0.92f, 1f));
            thumb.style.borderTopLeftRadius = 3;
            thumb.style.borderTopRightRadius = 3;
            thumb.style.borderBottomLeftRadius = 3;
            thumb.style.borderBottomRightRadius = 3;
            scrollbar.Add(thumb);

            thumb.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button != 0) return;
                draggingThumb = true;
                dragStartY = evt.position.y;
                dragStartOffset = scrollOffset;
                thumb.CapturePointer(evt.pointerId);
                evt.StopPropagation();
            });
            thumb.RegisterCallback<PointerMoveEvent>(evt =>
            {
                if (!draggingThumb) return;
                float travel = ThumbTravel();
                float maxScroll = MaxScroll();
                if (travel <= 0.1f || maxScroll <= 0f) return;
                float dy = evt.position.y - dragStartY;
                SetScroll(dragStartOffset + dy / travel * maxScroll);
                evt.StopPropagation();
            });
            thumb.RegisterCallback<PointerUpEvent>(evt =>
            {
                if (!draggingThumb) return;
                draggingThumb = false;
                thumb.ReleasePointer(evt.pointerId);
                evt.StopPropagation();
            });
            scrollbar.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button != 0 || evt.target == thumb) return;
                float localY = scrollbar.WorldToLocal(evt.position).y;
                float page = listHeight;
                SetScroll(localY < thumb.layout.y ? scrollOffset - page : scrollOffset + page);
                evt.StopPropagation();
            });

            listHost.Add(viewport);
            listHost.Add(scrollbar);

            popup.Add(searchRow);
            popup.Add(listHost);
            Overlay.Add(popup);

            Overlay.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.target == Overlay)
                    GotoDestinationPopup.Close();
            });
            Overlay.RegisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);
            Overlay.RegisterCallback<WheelEvent>(OnWheel, TrickleDown.TrickleDown);
            popup.RegisterCallback<GeometryChangedEvent>(_ => Place());
            search.RegisterValueChangedCallback(evt =>
            {
                Remember(evt.newValue);
                Rebuild(lastQuery);
            });
        }

        public string Query => search != null ? search.value : string.Empty;

        public void Open()
        {
            search.SetValueWithoutNotify(lastQuery);
            Rebuild(lastQuery);
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
            SetScroll(scrollOffset + evt.delta.y * RowHeight);
            evt.StopImmediatePropagation();
        }

        private float ContentHeight()
        {
            return Mathf.Max(filtered.Count, 1) * RowHeight;
        }

        private float MaxScroll()
        {
            return Mathf.Max(0f, ContentHeight() - listHeight);
        }

        private float ThumbTravel()
        {
            float thumbHeight = thumb.resolvedStyle.height;
            if (float.IsNaN(thumbHeight) || thumbHeight < 1f)
                thumbHeight = thumb.layout.height;
            return Mathf.Max(0f, listHeight - thumbHeight);
        }

        private void SetScroll(float value)
        {
            scrollOffset = Mathf.Clamp(value, 0f, MaxScroll());
            content.style.top = -scrollOffset;
            PlaceThumb();
        }

        private void PlaceThumb()
        {
            float contentHeight = ContentHeight();
            bool overflow = contentHeight > listHeight + 0.5f;
            scrollbar.style.display = overflow ? DisplayStyle.Flex : DisplayStyle.None;
            scrollbar.style.height = listHeight;
            if (!overflow) return;

            float thumbHeight = Mathf.Clamp(listHeight * listHeight / contentHeight, 28f, listHeight);
            float travel = Mathf.Max(0f, listHeight - thumbHeight);
            float maxScroll = MaxScroll();
            float top = maxScroll <= 0f ? 0f : scrollOffset / maxScroll * travel;
            thumb.style.height = thumbHeight;
            thumb.style.top = top;
        }

        private void Rebuild(string query)
        {
            filtered.Clear();
            rows.Clear();
            content.Clear();
            scrollOffset = 0f;

            string needle = string.IsNullOrWhiteSpace(query) ? string.Empty : query.Trim();
            for (int i = 0; i < choices.Count; i++)
            {
                string display = choices[i] ?? string.Empty;
                if (needle.Length > 0 && display.IndexOf(needle, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;
                filtered.Add(i);
            }

            int rowCount = Mathf.Max(filtered.Count, 1);
            float contentHeight = rowCount * RowHeight;
            listHeight = Mathf.Min(rowCount, MaxVisibleRows) * RowHeight;
            listHost.style.height = listHeight;
            viewport.style.height = listHeight;
            content.style.height = contentHeight;
            content.style.top = 0;
            PlaceThumb();

            if (filtered.Count == 0)
            {
                var empty = new Label("該当なし");
                LockRow(empty);
                empty.style.paddingLeft = 6;
                empty.style.color = new StyleColor(Color.white);
                content.Add(empty);
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
                LockRow(row);
                row.tooltip = display;

                bool isCurrent = sourceIndex == selectedIndex;
                var label = new Label(isCurrent ? "✓  " + display : display)
                {
                    pickingMode = PickingMode.Ignore
                };
                label.style.color = new StyleColor(Color.white);
                label.style.flexGrow = 1;
                label.style.flexShrink = 0;
                label.style.unityTextAlign = TextAnchor.MiddleLeft;
                row.Add(label);
                row.RegisterCallback<PointerEnterEvent>(_ => SetHighlight(rowIndex));
                row.RegisterCallback<PointerUpEvent>(evt =>
                {
                    if (evt.button != 0) return;
                    Pick(sourceIndex);
                    evt.StopPropagation();
                });

                rows.Add(row);
                content.Add(row);
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

            float rowTop = highlight * RowHeight;
            float rowBottom = rowTop + RowHeight;
            if (rowTop < scrollOffset)
                SetScroll(rowTop);
            else if (rowBottom > scrollOffset + listHeight)
                SetScroll(rowBottom - listHeight);
            else
                PlaceThumb();
        }

        private void ApplyRowColor(int index)
        {
            bool on = index == highlight;
            rows[index].style.backgroundColor = on
                ? new StyleColor(new Color(0.24f, 0.46f, 0.72f, 1f))
                : new StyleColor(new Color(0f, 0f, 0f, 0f));
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

        private static void LockRow(VisualElement row)
        {
            row.style.flexGrow = 0;
            row.style.flexShrink = 0;
            row.style.height = RowHeight;
            row.style.minHeight = RowHeight;
            row.style.maxHeight = RowHeight;
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
